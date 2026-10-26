/*
 * SPDX-License-Identifier: MIT
 */
#include "mr_link_face.h"
#include <hal/hal.h>
#include <mooncake_log.h>
#include <smooth_lvgl.hpp>
#include <stackchan/stackchan.h>
#include <memory>

using namespace mr_link;

void Face::create()
{
    if (_created) {
        return;
    }
    LvglLockGuard lock;

    auto avatar = std::make_unique<stackchan::avatar::DefaultAvatar>();
    avatar->init(lv_screen_active());
    // The panel is full screen and clickable. Let taps reach the screen so TouchTrigger sees them.
    if (auto* panel = avatar->getPanel()) {
        lv_obj_add_flag(panel->get(), LV_OBJ_FLAG_EVENT_BUBBLE);
    }
    GetStackChan().attachAvatar(std::move(avatar));
    _created = true;
}

void Face::destroy()
{
    if (!_created) {
        return;
    }
    LvglLockGuard lock;
    GetStackChan().resetAvatar();
    _created = false;
}
