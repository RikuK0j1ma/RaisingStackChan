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
    _smooth_valid = false;
    _stale = false;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        _has_look = false;  // never act on a target from before the mode changed
    }
    _accept_look.store(follow);
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

    // Message: yaw right +, pitch up +, degrees. Servo: yaw left +, pitch up +, 0.1 degree.
    const float target_yaw =
        std::clamp(-yaw_deg * 10.0f, static_cast<float>(-kYawLimit), static_cast<float>(kYawLimit));
    const float target_pitch =
        std::clamp(pitch_deg * 10.0f, static_cast<float>(kPitchMin), static_cast<float>(kPitchMax));

    const float dt = _last_update_us == 0 ? 0.02f : static_cast<float>(now - _last_update_us) / 1e6f;
    _last_update_us = now;
    if (!_smooth_valid) {
        const auto current = motion.getCurrentAngles();
        _smooth_yaw = static_cast<float>(current.x);
        _smooth_pitch = static_cast<float>(current.y);
        _cmd_yaw = current.x;
        _cmd_pitch = current.y;
        _smooth_valid = true;
    }
    const float alpha = 1.0f - std::exp(-std::max(dt, 0.0f) / kSmoothingSeconds);
    _smooth_yaw += (target_yaw - _smooth_yaw) * alpha;
    _smooth_pitch += (target_pitch - _smooth_pitch) * alpha;

    if (now - _last_cmd_us < kCommandIntervalUs) {
        return;
    }
    const int yaw = static_cast<int>(std::lround(_smooth_yaw));
    const int pitch = static_cast<int>(std::lround(_smooth_pitch));
    if (std::abs(yaw - _cmd_yaw) < kCommandDeadband && std::abs(pitch - _cmd_pitch) < kCommandDeadband) {
        return;
    }
    motion.moveWithSpeed(yaw, pitch, kFollowSpeed);
    _cmd_yaw = yaw;
    _cmd_pitch = pitch;
    _last_cmd_us = now;
}

void NeckLock::release()
{
    if (!_engaged) {
        return;
    }
    _accept_look.store(false);
    _follow = false;
    GetStackChan().motion().setModifyLock(_prev_locked);
    _engaged = false;
}
