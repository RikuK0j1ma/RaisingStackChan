/*
 * SPDX-License-Identifier: MIT
 */
#pragma once

namespace mr_link {

/**
 * @brief Keeps the neck still and facing front while MR LINK runs.
 *
 * The Quest app places the sphere relative to the face position it got from the QR, so the head must
 * not move. IdleMotionModifier and HeadPetModifier skip their motion while Motion::isModifyLocked()
 * is true, so engaging sets that lock (no official file is edited) and release() restores the
 * previous value.
 *
 * Call from the app task (the one that runs the AppAbility callbacks).
 */
class NeckLock {
public:
    /** Yaw / pitch the head is held at. (0, 0) is Motion::goHome(): facing front, level. */
    static constexpr int kYawAngle = 0;
    static constexpr int kPitchAngle = 0;
    static constexpr int kMoveSpeed = 300;  // 0-1000

    /** Lock modifiers out and move the head to the fixed angles. */
    void engage();
    /** Cheap; call from the app loop. Re-asserts the lock if another modifier (e.g. IMU shake) released it. */
    void update();
    /** Give the neck back to the modifiers as it was before engage(). No-op if not engaged. */
    void release();

private:
    bool _engaged = false;
    bool _prev_locked = false;
};

}  // namespace mr_link
