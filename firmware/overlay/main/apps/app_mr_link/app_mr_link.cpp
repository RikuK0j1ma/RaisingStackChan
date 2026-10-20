/*
 * SPDX-License-Identifier: MIT
 */
#include "app_mr_link.h"
#include "mr_link_neck.h"
#include "mr_link_qr.h"
#include "mr_link_server.h"
#include <hal/hal.h>
#include <mooncake.h>
#include <mooncake_log.h>
#include <smooth_lvgl.hpp>

using namespace mooncake;
using namespace smooth_ui_toolkit::lvgl_cpp;

static std::unique_ptr<Button> _button_quit;
static mr_link::Qr _qr;

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
    // "emotion" messages and the Happy flash on connect are handled by EmotionController
    _emotion.attach(server);
    // Re-calibration: Quest asks for the QR again
    _handler_ids.push_back(server.addMessageHandler("showQr", [](const ArduinoJson::JsonDocument&) {
        mclog::tagInfo("MR LINK", "showQr");
        _qr.show();
    }));
    // Quest connected: the QR is no longer needed, back to the face
    _handler_ids.push_back(server.addConnectionHandler([](bool connected) {
        if (connected) {
            _qr.hide();
        }
    }));

    mr_link::Server::Config config;
    config.token = token;
    if (server.start(config)) {
        _qr.setup(token, config.port);
        _qr.show();
    }

    _neck.engage();
    _touch.attach(lv_screen_active());

    _button_quit = std::make_unique<Button>(lv_screen_active());
    _button_quit->setAlign(LV_ALIGN_TOP_RIGHT);  // off the QR
    _button_quit->label().setText("QUIT");
    _button_quit->onClick().connect([this]() { close(); });
}

void AppMrLink::onRunning()
{
    _qr.update();  // picks up a Wi-Fi address that arrives late
    _emotion.update();
    _neck.update();
    if (_touch.consume() && !_qr.isShown()) {
        _qr.show();  // touch brings the QR back (re-calibration)
    }
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
    _emotion.detach(server);
    _qr.hide();
    _neck.release();

    LvglLockGuard lock;
    _touch.detach();
    _button_quit.reset();
}
