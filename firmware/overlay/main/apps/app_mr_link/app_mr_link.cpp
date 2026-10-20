/*
 * SPDX-License-Identifier: MIT
 */
#include "app_mr_link.h"
#include "mr_link_server.h"
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

    // The token lives until reboot, so a QR shown again (re-calibration) keeps the same token
    static const std::string token = mr_link::Server::generateToken();
    auto& server = mr_link::GetServer();

    // Log what arrives. The emotion / showQr features subscribe the same way.
    _handler_ids.push_back(server.addMessageHandler("emotion", [](const ArduinoJson::JsonDocument& doc) {
        mclog::tagInfo("MR LINK", "emotion: value={} durationMs={}", doc["value"] | "", doc["durationMs"] | 0);
    }));
    _handler_ids.push_back(server.addMessageHandler(
        "showQr", [](const ArduinoJson::JsonDocument&) { mclog::tagInfo("MR LINK", "showQr"); }));

    mr_link::Server::Config config;
    config.token = token;
    if (server.start(config)) {
        // TODO(#2): show the QR code (ip, port, token) instead of logging
        mclog::tagInfo("MR LINK", "token: {}", token);
    }

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

    auto& server = mr_link::GetServer();
    for (int id : _handler_ids) {
        server.removeHandler(id);
    }
    _handler_ids.clear();
    server.stop();

    LvglLockGuard lock;
    _button_quit.reset();
}
