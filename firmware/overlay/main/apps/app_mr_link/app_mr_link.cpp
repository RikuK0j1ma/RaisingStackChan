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
    setAppInfo().name = "MOGUMOGU";
}

void AppMrLink::onCreate()
{
    mclog::tagInfo(getAppInfo().name, "on create");
}

void AppMrLink::onOpen()
{
    mclog::tagInfo(getAppInfo().name, "on open");

    // Can take a while (config AP start); keep the LVGL lock free meanwhile
    _wifi.start();

    LvglLockGuard lock;

    // The face goes first: the QR page is added later and covers it until a Quest connects
    _face.create();

    // The token lives until reboot, so a QR shown again (re-calibration) keeps the same token
    static const std::string token = mr_link::Server::generateToken();
    auto& server = mr_link::GetServer();

    // Log what arrives. The emotion / showQr features subscribe the same way.
    // "emotion" messages and the Happy flash on connect are handled by EmotionController
    _emotion.attach(server);
    // "look" messages steer the neck while following the headset
    _neck.attach(server);
    // Re-calibration: Quest asks for the QR again
    _handler_ids.push_back(server.addMessageHandler("showQr", [](const ArduinoJson::JsonDocument&) {
        mclog::tagInfo("MOGUMOGU", "showQr");
        _qr.show();
    }));
    // Quest connected: the QR is no longer needed, the face underneath shows.
    // Disconnected: back to the QR (standby). A replaced client does not notify, so no flicker.
    _handler_ids.push_back(server.addConnectionHandler([](bool connected) {
        if (connected) {
            _qr.hide();
        } else {
            _qr.show();
        }
    }));

    _config = mr_link::Server::Config{};
    _config.token = token;
    // The QR page shows guidance until Wi-Fi gives an address (onRunning starts the server then)
    _qr.setup(token, _config.port);
    updateWifiGuide();
    _qr.show();
    startServerIfReady();

    _neck.engage();
    _touch.attach(lv_screen_active());

    _button_quit = std::make_unique<Button>(lv_screen_active());
    _button_quit->setAlign(LV_ALIGN_TOP_RIGHT);  // off the QR and the face
    _button_quit->label().setText("QUIT");
    _button_quit->onClick().connect([this]() { close(); });
}

// Without Wi-Fi the TCP/IP stack is not initialized and httpd_start() aborts in socket(). Wait for an IP address.
void AppMrLink::startServerIfReady()
{
    auto& server = mr_link::GetServer();
    if (server.isRunning() || mr_link::Qr::getIp().empty()) {
        return;
    }
    if (!server.start(_config)) {
        mclog::tagError(getAppInfo().name, "server start failed");
    }
}

void AppMrLink::updateWifiGuide()
{
    mr_link::Qr::Guide guide;
    switch (_wifi.state()) {
        case mr_link::WifiSetup::State::Config:
            guide.mode = mr_link::Qr::Guide::Mode::Config;
            guide.ap_ssid = _wifi.apSsid();
            guide.ap_url = _wifi.apUrl();
            break;
        case mr_link::WifiSetup::State::Connecting:
            guide.mode = mr_link::Qr::Guide::Mode::Connecting;
            break;
        case mr_link::WifiSetup::State::Failed:
            guide.mode = mr_link::Qr::Guide::Mode::Failed;
            break;
        default:
            break;
    }
    _qr.setGuide(guide);
}

void AppMrLink::onRunning()
{
    _wifi.update();
    updateWifiGuide();
    startServerIfReady();
    _qr.update();  // picks up a Wi-Fi address that arrives late
    _emotion.update();
    // Follow the headset only while a client is connected and the QR is hidden (QR detection needs a still face)
    _neck.setFollow(mr_link::GetServer().hasClient() && !_qr.isShown());
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
    _neck.detach(server);
    _qr.hide();
    _neck.release();
    _wifi.stop();

    LvglLockGuard lock;
    _face.destroy();
    _touch.detach();
    _button_quit.reset();
}
