/*
 * SPDX-License-Identifier: MIT
 */
#include "mr_link_neck.h"
#include <esp_timer.h>
#include <mooncake_log.h>
#include <stackchan/stackchan.h>
#include <algorithm>
#include <cmath>

using namespace mr_link;

namespace {

/** Critically damped spring step (Unity SmoothDamp). pos/vel in place. */
void smooth_damp(float& pos, float& vel, float target, float smooth_s, float max_speed, float dt)
{
    smooth_s = std::max(smooth_s, 0.0001f);
    const float omega = 2.0f / smooth_s;
    const float x = omega * dt;
    const float e = 1.0f / (1.0f + x + 0.48f * x * x + 0.235f * x * x * x);
    const float max_change = max_speed * smooth_s;
    const float change = std::clamp(pos - target, -max_change, max_change);
    const float goal = pos - change;
    const float temp = (vel + omega * change) * dt;
    vel = (vel - omega * temp) * e;
    pos = goal + (change + temp) * e;
}

}  // namespace

void NeckLock::attach(Server& server)
{
    // HTTP task: validate and record only. The servos are driven from update().
    _handler_id = server.addMessageHandler("look", [this](const ArduinoJson::JsonDocument& doc) {
        if (!_accept_look.load()) {
            return;  // QR shown or not following: ignore
        }
        if (!doc["yaw"].is<float>() || !doc["pitch"].is<float>()) {
            return;
        }
        const float yaw = doc["yaw"].as<float>();
        const float pitch = doc["pitch"].as<float>();
        if (!std::isfinite(yaw) || !std::isfinite(pitch) || std::fabs(yaw) > kMaxInputDeg ||
            std::fabs(pitch) > kMaxInputDeg) {
            return;
        }
        std::lock_guard<std::mutex> lock(_mutex);
        _has_look = true;
        _target_yaw_deg = yaw;
        _target_pitch_deg = pitch;
        _last_look_us = esp_timer_get_time();
    });
}

void NeckLock::detach(Server& server)
{
    if (_handler_id >= 0) {
        server.removeHandler(_handler_id);
        _handler_id = -1;
    }
    _accept_look.store(false);
}

void NeckLock::setServoSync(bool enabled)
{
    auto& motion = GetStackChan().motion();
    motion.yawServo().setAutoAngleSyncEnabled(enabled);
    motion.pitchServo().setAutoAngleSyncEnabled(enabled);
}

void NeckLock::engage()
{
    if (_engaged) {
        return;
    }
    auto& motion = GetStackChan().motion();
    _prev_locked = motion.isModifyLocked();
    motion.setModifyLock(true);
    motion.moveWithSpeed(kYawAngle, kPitchAngle, kMoveSpeed);
    _engaged = true;
    _follow = false;
    _accept_look.store(false);
}

void NeckLock::setFollow(bool follow)
{
    if (!_engaged || follow == _follow) {
        return;
    }
    _follow = follow;
    mclog::tagInfo("MOGUMOGU/neck", "follow headset: {}", follow ? "on" : "off");
    _smooth_valid = false;
    _stale = false;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        _has_look = false;  // never act on a target from before the mode changed
    }
    _accept_look.store(follow);
    setServoSync(!follow);
    if (!follow) {
        GetStackChan().motion().moveWithSpeed(kYawAngle, kPitchAngle, kMoveSpeed);
    }
}

void NeckLock::update()
{
    if (!_engaged) {
        return;
    }
    auto& motion = GetStackChan().motion();
    if (!motion.isModifyLocked()) {
        motion.setModifyLock(true);
        if (!_follow) {
            motion.moveWithSpeed(kYawAngle, kPitchAngle, kMoveSpeed);
        }
    }
    if (_follow) {
        applyFollow();
    }
}

void NeckLock::applyFollow()
{
    bool has_look = false;
    float yaw_deg = 0.0f;
    float pitch_deg = 0.0f;
    int64_t last_look_us = 0;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        has_look = _has_look;
        yaw_deg = _target_yaw_deg;
        pitch_deg = _target_pitch_deg;
        last_look_us = _last_look_us;
    }
    if (!has_look) {
        return;
    }

    auto& motion = GetStackChan().motion();
    const int64_t now = esp_timer_get_time();

    if (now - last_look_us > kLookTimeoutUs) {
        if (!_stale) {
            motion.stop();  // hold where it is until "look" resumes
            _stale = true;
            _smooth_valid = false;
        }
        return;
    }
    _stale = false;

    // Message: yaw right +, pitch up +, degrees. Servo: 0.1 degree (yaw sign: see kYawSign).
    const float target_yaw =
        std::clamp(kYawSign * yaw_deg * 10.0f, static_cast<float>(-kYawLimit), static_cast<float>(kYawLimit));
    const float target_pitch =
        std::clamp(pitch_deg * 10.0f, static_cast<float>(kPitchMin), static_cast<float>(kPitchMax));

    if (!_smooth_valid) {
        const auto current = motion.getCurrentAngles();
        _pos_yaw = _held_yaw = static_cast<float>(current.x);
        _pos_pitch = _held_pitch = static_cast<float>(current.y);
        _vel_yaw = _vel_pitch = 0.0f;
        _cmd_yaw = current.x;
        _cmd_pitch = current.y;
        _accum_s = 0.0f;
        _last_update_us = now;
        _smooth_valid = true;
    }

    // Deadband on the target: small headset jitter does not move the neck
    if (std::fabs(target_yaw - _held_yaw) > kTargetDeadband) {
        _held_yaw = target_yaw;
    }
    if (std::fabs(target_pitch - _held_pitch) > kTargetDeadband) {
        _held_pitch = target_pitch;
    }

    // Fixed-step physics so the motion does not depend on how often update() is called
    _accum_s += static_cast<float>(now - _last_update_us) / 1e6f;
    _last_update_us = now;
    int steps = 0;
    while (_accum_s >= kStepSeconds) {
        _accum_s -= kStepSeconds;
        if (steps < kMaxStepsPerUpdate) {
            smooth_damp(_pos_yaw, _vel_yaw, _held_yaw, kYawSmoothSeconds, kYawMaxSpeed, kStepSeconds);
            smooth_damp(_pos_pitch, _vel_pitch, _held_pitch, kPitchSmoothSeconds, kPitchMaxSpeed, kStepSeconds);
            ++steps;
        }
    }
    if (steps == 0) {
        return;
    }

    const int yaw = static_cast<int>(std::lround(_pos_yaw));
    const int pitch = static_cast<int>(std::lround(_pos_pitch));
    if (std::abs(yaw - _cmd_yaw) < kMoveCompleteDeadband && std::abs(pitch - _cmd_pitch) < kMoveCompleteDeadband) {
        return;
    }
    motion.moveWithSpeed(yaw, pitch, kServoFollowSpeed);
    _cmd_yaw = yaw;
    _cmd_pitch = pitch;
}

void NeckLock::release()
{
    if (!_engaged) {
        return;
    }
    _accept_look.store(false);
    _follow = false;
    setServoSync(true);
    GetStackChan().motion().setModifyLock(_prev_locked);
    _engaged = false;
}
