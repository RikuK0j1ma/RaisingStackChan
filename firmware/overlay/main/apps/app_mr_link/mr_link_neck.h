/*
 * SPDX-License-Identifier: MIT
 */
#pragma once
#include "mr_link_server.h"
#include <atomic>
#include <cstdint>
#include <mutex>

namespace mr_link {

/**
 * @brief Owns the neck while MOGUMOGU runs: held still facing front, or following the Quest headset.
 *
 * Fixed (default): the Quest app places the sphere relative to the face position it got from the QR, so
 * the head must not move while the QR is shown. Follow: while a client is connected and the QR is
 * hidden, "look" messages ({"type":"look","yaw":<deg>,"pitch":<deg>}, yaw right +, pitch up +,
 * 0 = facing front) steer the head toward the headset.
 *
 * IdleMotionModifier and HeadPetModifier skip their motion while Motion::isModifyLocked() is true, so
 * engage() sets that lock (no official file is edited) and release() restores the previous value.
 * The lock stays set in both modes.
 *
 * engage() / setFollow() / update() / release() are for the app task (the one that runs the AppAbility
 * callbacks). The "look" handler runs on the HTTP task and only records the target.
 */
class NeckLock {
public:
    /** Yaw / pitch the head is held at. (0, 0) is Motion::goHome(): facing front, level. */
    static constexpr int kYawAngle = 0;
    static constexpr int kPitchAngle = 0;
    static constexpr int kMoveSpeed = 300;  // 0-1000

    /** Servo angles are in 0.1 degree. Targets are clamped to this range (the servos clamp to their own limits too). */
    static constexpr int kYawLimit = 1200;
    static constexpr int kPitchMin = 0;
    static constexpr int kPitchMax = 800;
    /** Largest |degrees| accepted from a message. Anything beyond is dropped as garbage. */
    static constexpr float kMaxInputDeg = 360.0f;

    static constexpr int kFollowSpeed = 400;               // 0-1000
    static constexpr float kSmoothingSeconds = 0.12f;      // low-pass time constant of the target
    static constexpr int64_t kCommandIntervalUs = 50000;   // at most 20 servo commands per second
    static constexpr int kCommandDeadband = 3;             // 0.1 degree; smaller changes are not sent
    /** No "look" for this long: stop in place (Quest froze while the connection is still alive). */
    static constexpr int64_t kLookTimeoutUs = 1000000;

    /** Subscribe to "look". Call once per open. */
    void attach(Server& server);
    void detach(Server& server);

    /** Lock modifiers out and move the head to the fixed angles. */
    void engage();
    /**
     * Follow (true) or hold front (false). Going to false moves the head back to the front.
     * No-op if the mode does not change or the neck is not engaged.
     */
    void setFollow(bool follow);
    bool isFollowing() const
    {
        return _follow;
    }
    /** Cheap; call from the app loop. Re-asserts the lock if another modifier (e.g. IMU shake) released it, and steers while following. */
    void update();
    /** Give the neck back to the modifiers as it was before engage(). No-op if not engaged. */
    void release();

private:
    void applyFollow();

    bool _engaged = false;
    bool _prev_locked = false;
    int _handler_id = -1;

    // Shared with the HTTP task
    std::atomic<bool> _accept_look{false};
    std::mutex _mutex;  // guards the target below
    bool _has_look = false;
    float _target_yaw_deg = 0.0f;
    float _target_pitch_deg = 0.0f;
    int64_t _last_look_us = 0;

    // App task only
    bool _follow = false;
    bool _smooth_valid = false;
    bool _stale = false;
    float _smooth_yaw = 0.0f;  // 0.1 degree, servo direction
    float _smooth_pitch = 0.0f;
    int _cmd_yaw = 0;
    int _cmd_pitch = 0;
    int64_t _last_update_us = 0;
    int64_t _last_cmd_us = 0;
};

}  // namespace mr_link
