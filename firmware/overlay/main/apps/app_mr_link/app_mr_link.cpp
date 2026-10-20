/*
 * SPDX-License-Identifier: MIT
 */
#include "app_mr_link.h"
#include <hal/hal.h>
#include <mooncake.h>
#include <mooncake_log.h>
#include <smooth_lvgl.hpp>

using namespace mooncake;
using namespace smooth_ui_toolkit::lvgl_cpp;

static std::unique_ptr<Button> _button_quit;

AppMrLink::AppMrLink()
{
    setAppInfo().name = "MR LINK";
}

void AppMrLink::onCreate()
{
    mclog::tagInfo(getAppInfo().name, "on create");
}

void AppMrLink::onOpen()
{
    mclog::tagInfo(getAppInfo().name, "on open");

    LvglLockGuard lock;

    // TODO: show the QR code (ip, port, token) and start the WebSocket server
    _button_quit = std::make_unique<Button>(lv_screen_active());
    _button_quit->setAlign(LV_ALIGN_CENTER);
    _button_quit->label().setText("QUIT");
    _button_quit->onClick().connect([this]() { close(); });
}

void AppMrLink::onRunning()
{
}

void AppMrLink::onClose()
{
    mclog::tagInfo(getAppInfo().name, "on close");

    LvglLockGuard lock;
    _button_quit.reset();
}
