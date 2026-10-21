/*
 * SPDX-License-Identifier: MIT
 */
#include "mr_link_neck.h"
#include <stackchan/stackchan.h>

using namespace mr_link;

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
}

void NeckLock::update()
{
    if (!_engaged) {
        return;
    }
    auto& motion = GetStackChan().motion();
    if (!motion.isModifyLocked()) {
        motion.setModifyLock(true);
        motion.moveWithSpeed(kYawAngle, kPitchAngle, kMoveSpeed);
    }
}

void NeckLock::release()
{
    if (!_engaged) {
        return;
    }
    GetStackChan().motion().setModifyLock(_prev_locked);
    _engaged = false;
}
