/*
 * SPDX-License-Identifier: MIT
 */
#include "mr_link_wifi_scan.h"
#include <cctype>
#include <cstdint>
#include <cstring>
#include <esp_heap_caps.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <hal/board/hal_bridge.h>
#include <linux/videodev2.h>
#include <mooncake_log.h>
#include <quirc.h>

namespace mr_link {

static const std::string _tag = "MOGUMOGU/wifiscan";

// quirc_decode() keeps a ~9 KB bit stream on the stack
static constexpr uint32_t kTaskStackBytes = 24 * 1024;
static constexpr UBaseType_t kTaskPriority = 2;
// Pause between frames so the scan does not starve the UI
static constexpr int kFrameIntervalMs = 50;

bool parseWifiQr(const std::string& text, WifiCredentials& out)
{
    static const std::string kPrefix = "WIFI:";
    if (text.compare(0, kPrefix.size(), kPrefix) != 0) {
        return false;
    }

    std::string type, ssid, password;
    size_t i = kPrefix.size();
    while (i < text.size()) {
        // "<key>:<value>;" where '\' escapes \ ; , : "
        const size_t colon = text.find(':', i);
        if (colon == std::string::npos) {
            break;
        }
        const std::string key = text.substr(i, colon - i);
        std::string value;
        i = colon + 1;
        while (i < text.size() && text[i] != ';') {
            if (text[i] == '\\' && i + 1 < text.size()) {
                ++i;
            }
            value += text[i++];
        }
        ++i;  // the ';'

        if (key == "T") {
            type = value;
        } else if (key == "S") {
            ssid = value;
        } else if (key == "P") {
            password = value;
        }
    }

    for (auto& c : type) {
        c = static_cast<char>(std::toupper(static_cast<unsigned char>(c)));
    }

    if (ssid.empty() || ssid.size() > 32) {
        mclog::tagWarn(_tag, "Wi-Fi QR ignored: bad SSID");
        return false;
    }
    if (type == "WEP") {
        mclog::tagWarn(_tag, "Wi-Fi QR ignored: WEP is not supported");
        return false;
    }
    if (type == "NOPASS" || (type.empty() && password.empty())) {
        password.clear();
    } else if (password.size() < 8 || password.size() > 64) {
        mclog::tagWarn(_tag, "Wi-Fi QR ignored: WPA password must be 8-64 characters");
        return false;
    }

    out.ssid = ssid;
    out.password = password;
    return true;
}

// Camera frame -> 8 bit gray for quirc. Returns false for a format it does not know.
static bool toGray(const uint8_t* src, size_t size, int w, int h, int format, uint8_t* dst)
{
    const size_t pixels = static_cast<size_t>(w) * h;
    switch (format) {
        case V4L2_PIX_FMT_GREY:
            if (size < pixels) return false;
            memcpy(dst, src, pixels);
            return true;
        case V4L2_PIX_FMT_YUYV:  // Y U Y V: luma is every other byte
            if (size < pixels * 2) return false;
            for (size_t i = 0; i < pixels; i++) dst[i] = src[i * 2];
            return true;
        case V4L2_PIX_FMT_RGB565: {
            if (size < pixels * 2) return false;
            const uint16_t* p = reinterpret_cast<const uint16_t*>(src);
            for (size_t i = 0; i < pixels; i++) {
                const uint32_t r = (p[i] >> 11) & 0x1F, g = (p[i] >> 5) & 0x3F, b = p[i] & 0x1F;
                // 5/6/5 bits -> 8 bits, weights 0.30 / 0.59 / 0.11
                dst[i] = static_cast<uint8_t>(((r << 3) * 77 + (g << 2) * 150 + (b << 3) * 29) >> 8);
            }
            return true;
        }
        case V4L2_PIX_FMT_RGB24:  // channel order does not matter for a plain average
            if (size < pixels * 3) return false;
            for (size_t i = 0; i < pixels; i++) {
                dst[i] = static_cast<uint8_t>((src[i * 3] + src[i * 3 + 1] + src[i * 3 + 2]) / 3);
            }
            return true;
        default:
            return false;
    }
}

WifiQrScanner::~WifiQrScanner()
{
    stop();
}

bool WifiQrScanner::start()
{
    if (_alive) {
        return true;
    }
    if (hal_bridge::board_get_camera() == nullptr) {
        mclog::tagWarn(_tag, "no camera, Wi-Fi QR scan unavailable");
        return false;
    }
    {
        std::lock_guard<std::mutex> lock(_mutex);
        _has_found = false;
    }
    _run = true;
    _alive = true;
    if (xTaskCreate(taskEntry, "wifi_qr_scan", kTaskStackBytes, this, kTaskPriority, nullptr) != pdPASS) {
        mclog::tagError(_tag, "scan task start failed");
        _run = false;
        _alive = false;
        return false;
    }
    mclog::tagInfo(_tag, "scanning for a Wi-Fi QR");
    return true;
}

void WifiQrScanner::stop()
{
    _run = false;
    // The task leaves after the frame it is waiting for (a few 100 ms at most)
    for (int i = 0; i < 100 && _alive; i++) {
        vTaskDelay(pdMS_TO_TICKS(20));
    }
    if (_alive) {
        mclog::tagWarn(_tag, "scan task did not stop in time");
    }
}

bool WifiQrScanner::isRunning() const
{
    return _alive;
}

bool WifiQrScanner::poll(WifiCredentials& out)
{
    std::lock_guard<std::mutex> lock(_mutex);
    if (!_has_found) {
        return false;
    }
    out = _found;
    _has_found = false;
    return true;
}

void WifiQrScanner::taskEntry(void* arg)
{
    auto* self = static_cast<WifiQrScanner*>(arg);
    self->run();
    self->_alive = false;
    vTaskDelete(nullptr);
}

void WifiQrScanner::run()
{
    auto* camera = hal_bridge::board_get_camera();
    quirc* qr = quirc_new();
    auto* code = static_cast<quirc_code*>(heap_caps_calloc(1, sizeof(quirc_code), MALLOC_CAP_8BIT));
    auto* data = static_cast<quirc_data*>(heap_caps_calloc(1, sizeof(quirc_data), MALLOC_CAP_8BIT));
    int cur_w = 0, cur_h = 0;

    while (_run && camera != nullptr && qr != nullptr && code != nullptr && data != nullptr) {
        if (!camera->StreamCaptures()) {
            vTaskDelay(pdMS_TO_TICKS(100));
            continue;
        }

        const int w = camera->GetFrameWidth();
        const int h = camera->GetFrameHeight();
        if (w != cur_w || h != cur_h) {
            if (quirc_resize(qr, w, h) < 0) {
                mclog::tagError(_tag, "quirc_resize failed ({}x{})", w, h);
                break;
            }
            cur_w = w;
            cur_h = h;
        }

        uint8_t* gray = quirc_begin(qr, nullptr, nullptr);
        if (!toGray(camera->GetFrameData(), camera->GetFrameSize(), w, h, camera->GetFrameFormat(), gray)) {
            mclog::tagError(_tag, "unsupported camera format 0x{:x}", camera->GetFrameFormat());
            quirc_end(qr);
            break;
        }
        quirc_end(qr);

        for (int i = 0, n = quirc_count(qr); i < n && _run; i++) {
            quirc_extract(qr, i, code);
            quirc_decode_error_t err = quirc_decode(code, data);
            if (err != QUIRC_SUCCESS) {
                quirc_flip(code);  // a mirrored image
                err = quirc_decode(code, data);
            }
            if (err != QUIRC_SUCCESS) {
                continue;
            }

            WifiCredentials creds;
            const std::string text(reinterpret_cast<const char*>(data->payload), data->payload_len);
            if (parseWifiQr(text, creds)) {
                mclog::tagInfo(_tag, "Wi-Fi QR read: SSID \"{}\"", creds.ssid);
                std::lock_guard<std::mutex> lock(_mutex);
                _found = creds;
                _has_found = true;
                _run = false;
                break;
            }
        }

        vTaskDelay(pdMS_TO_TICKS(kFrameIntervalMs));
    }

    if (qr != nullptr) quirc_destroy(qr);
    heap_caps_free(code);
    heap_caps_free(data);
}

}  // namespace mr_link
