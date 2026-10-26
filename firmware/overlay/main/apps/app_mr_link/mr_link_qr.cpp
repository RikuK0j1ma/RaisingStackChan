/*
 * SPDX-License-Identifier: MIT
 */
#include "mr_link_qr.h"
#include <hal/hal.h>
#include <esp_netif.h>
#include <esp_timer.h>
#include <mooncake_log.h>
#include <smooth_lvgl.hpp>

namespace mr_link {

using smooth_ui_toolkit::lvgl_cpp::Qrcode;

static const std::string _tag = "MOGUMOGU/qr";

// 320x240 screen. The whole screen is white (quiet zone), the QR fills the height. The payload always needs
// QR version 4 (33 modules, ECC medium), and LVGL scales by whole pixels, so 231 = 33 modules x 7 px.
static constexpr int kQrSize = 231;
// Backlight while the QR is shown (percent). At full brightness the camera blooms the white and the modules
// merge, so the QR is dimmed; the previous level is restored when the QR goes away.
static constexpr uint8_t kQrBrightness = 40;
// Wi-Fi setup page: three text lines on top leave less room for the QR
static constexpr int kSetupBoxSize = 148;
static constexpr int kSetupQrSize = 128;
static constexpr int64_t kCheckIntervalUs = 1000 * 1000;

Qr::Qr() = default;

Qr::~Qr() = default;

void Qr::setup(const std::string& token, uint16_t port)
{
    LvglLockGuard lock;
    _token = token;
    _port = port;
}

std::string Qr::getIp()
{
    esp_netif_t* netif = esp_netif_get_handle_from_ifkey("WIFI_STA_DEF");
    if (netif == nullptr) {
        return {};
    }
    esp_netif_ip_info_t info = {};
    if (esp_netif_get_ip_info(netif, &info) != ESP_OK || info.ip.addr == 0) {
        return {};
    }
    char buf[16] = {};
    esp_ip4addr_ntoa(&info.ip, buf, sizeof(buf));
    return buf;
}

std::string Qr::makePayload(const std::string& ip) const
{
    if (ip.empty()) {
        return {};
    }
    return "ws://" + ip + ":" + std::to_string(_port) + "/ws?token=" + _token;
}

bool Qr::isShown() const
{
    return _shown;
}

void Qr::setGuide(const Guide& guide)
{
    _guide = guide;
}

std::string Qr::viewKey() const
{
    return getIp() + "|" + std::to_string(static_cast<int>(_guide.mode)) + "|" + _guide.ap_ssid;
}

void Qr::show()
{
    LvglLockGuard lock;
    destroy();
    _shown = true;
    _last_check_us = esp_timer_get_time();
    build();
}

void Qr::hide()
{
    LvglLockGuard lock;
    destroy();
    _shown = false;
    setQrBrightness(false);
}

void Qr::setQrBrightness(bool on)
{
    if (on) {
        if (!_brightness_saved) {
            _saved_brightness = GetHAL().getBackLightBrightness();
            _brightness_saved = true;
        }
        GetHAL().setBackLightBrightness(kQrBrightness, false);
    } else if (_brightness_saved) {
        GetHAL().setBackLightBrightness(_saved_brightness, false);
        _brightness_saved = false;
    }
}

void Qr::update()
{
    if (!_shown) {
        return;
    }
    const int64_t now = esp_timer_get_time();
    if (now - _last_check_us < kCheckIntervalUs) {
        return;
    }
    _last_check_us = now;
    if (viewKey() == _key) {
        return;
    }
    LvglLockGuard lock;
    if (!_shown) {
        return;
    }
    destroy();
    build();
}

void Qr::destroy()
{
    // Caller holds the LVGL lock. Reset the Qrcode wrapper first, then delete the parent (frees the children).
    _qrcode.reset();
    if (_root != nullptr) {
        lv_obj_delete(_root);
        _root = nullptr;
    }
}

void Qr::buildWifiSetup(lv_obj_t* label)
{
    // Caller holds the LVGL lock. The config AP is open, so a Wi-Fi QR joins it from the phone camera.
    const std::string text = "Wi-Fi setup\nJoin: " + _guide.ap_ssid + "\nThen open " + _guide.ap_url +
                             "\nOr show a Wi-Fi QR to my camera";
    lv_label_set_text(label, text.c_str());
    lv_obj_align(label, LV_ALIGN_TOP_MID, 0, 4);

    lv_obj_t* box = lv_obj_create(_root);
    lv_obj_set_size(box, kSetupBoxSize, kSetupBoxSize);
    lv_obj_align(box, LV_ALIGN_BOTTOM_MID, 0, -4);
    lv_obj_remove_flag(box, LV_OBJ_FLAG_SCROLLABLE);
    lv_obj_set_style_radius(box, 0, 0);
    lv_obj_set_style_border_width(box, 0, 0);
    lv_obj_set_style_pad_all(box, 0, 0);
    lv_obj_set_style_bg_color(box, lv_color_hex(0xFFFFFF), 0);
    lv_obj_set_style_bg_opa(box, LV_OPA_COVER, 0);

    _qrcode = std::make_unique<Qrcode>(box);
    _qrcode->setSize(kSetupQrSize);
    _qrcode->setDarkColor(lv_color_hex(0x000000));
    _qrcode->setLightColor(lv_color_hex(0xFFFFFF));
    _qrcode->update("WIFI:T:nopass;S:" + _guide.ap_ssid + ";;");
    _qrcode->align(LV_ALIGN_CENTER, 0, 0);
}

void Qr::build()
{
    // Caller holds the LVGL lock.
    _ip = getIp();
    _key = viewKey();

    _root = lv_obj_create(lv_screen_active());
    lv_obj_set_size(_root, lv_pct(100), lv_pct(100));
    lv_obj_align(_root, LV_ALIGN_CENTER, 0, 0);
    lv_obj_remove_flag(_root, LV_OBJ_FLAG_SCROLLABLE);
    lv_obj_set_style_radius(_root, 0, 0);
    lv_obj_set_style_border_width(_root, 0, 0);
    lv_obj_set_style_pad_all(_root, 0, 0);
    lv_obj_set_style_bg_color(_root, lv_color_hex(0x202020), 0);
    lv_obj_set_style_bg_opa(_root, LV_OPA_COVER, 0);

    // The standard Montserrat fonts have no Japanese glyphs, so the text is English.
    lv_obj_t* label = lv_label_create(_root);
    lv_obj_set_style_text_font(label, &lv_font_montserrat_14, 0);
    lv_obj_set_style_text_color(label, lv_color_hex(0xFFFFFF), 0);
    lv_obj_set_style_text_align(label, LV_TEXT_ALIGN_CENTER, 0);

    if (_ip.empty()) {
        mclog::tagWarn(_tag, "no IP address, showing guidance");
        setQrBrightness(false);
        switch (_guide.mode) {
            case Guide::Mode::Config:
                buildWifiSetup(label);
                break;
            case Guide::Mode::Connecting:
                lv_label_set_text(label, "Connecting to Wi-Fi...");
                lv_obj_align(label, LV_ALIGN_CENTER, 0, 0);
                break;
            default:
                lv_label_set_text(label, "Wi-Fi is not available.\nRestart StackChan and try again.");
                lv_obj_align(label, LV_ALIGN_CENTER, 0, 0);
                break;
        }
        return;
    }

    // The QR page is all white with the QR only (no text): every pixel goes to the QR.
    lv_obj_delete(label);
    lv_obj_set_style_bg_color(_root, lv_color_hex(0xFFFFFF), 0);

    // Pure black on pure white for the best contrast
    _qrcode = std::make_unique<Qrcode>(_root);
    _qrcode->setSize(kQrSize);
    _qrcode->setDarkColor(lv_color_hex(0x000000));
    _qrcode->setLightColor(lv_color_hex(0xFFFFFF));
    _qrcode->update(makePayload(_ip));
    _qrcode->align(LV_ALIGN_CENTER, 0, 0);
    setQrBrightness(true);

    mclog::tagInfo(_tag, "showing QR for {}:{}", _ip, _port);
}

}  // namespace mr_link
