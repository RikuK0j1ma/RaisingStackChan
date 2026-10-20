/*
 * SPDX-License-Identifier: MIT
 */
#pragma once
#include <atomic>
#include <lvgl.h>

namespace mr_link {

/**
 * @brief Turns a tap on an LVGL object into a flag the app loop can poll.
 *
 * The LVGL callback only sets the flag; the caller acts on it from its own loop, so no LVGL call is
 * made from inside the event handler.
 */
class TouchTrigger {
public:
    /** Start listening on obj. Call with LvglLockGuard held. */
    void attach(lv_obj_t* obj);
    /** Stop listening. Call with LvglLockGuard held. No-op if not attached. */
    void detach();
    /** True once per tap. */
    bool consume();

private:
    static void onEvent(lv_event_t* e);

    lv_obj_t* _obj = nullptr;
    std::atomic<bool> _tapped{false};
};

}  // namespace mr_link
