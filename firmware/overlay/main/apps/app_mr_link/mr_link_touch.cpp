/*
 * SPDX-License-Identifier: MIT
 */
#include "mr_link_touch.h"

using namespace mr_link;

void TouchTrigger::attach(lv_obj_t* obj)
{
    detach();
    _tapped = false;
    _obj = obj;
    lv_obj_add_event_cb(_obj, onEvent, LV_EVENT_CLICKED, this);
}

void TouchTrigger::detach()
{
    if (_obj) {
        lv_obj_remove_event_cb_with_user_data(_obj, onEvent, this);
        _obj = nullptr;
    }
}

bool TouchTrigger::consume()
{
    return _tapped.exchange(false);
}

void TouchTrigger::onEvent(lv_event_t* e)
{
    auto* self = static_cast<TouchTrigger*>(lv_event_get_user_data(e));
    if (self) {
        self->_tapped = true;
    }
}
