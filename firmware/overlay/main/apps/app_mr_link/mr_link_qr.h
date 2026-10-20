/*
 * SPDX-License-Identifier: MIT
 */
#pragma once
#include <cstdint>
#include <memory>
#include <string>
#include <lvgl.h>

namespace smooth_ui_toolkit::lvgl_cpp {
class Qrcode;
}

namespace mr_link {

/**
 * @brief Full-screen QR page for the Quest app.
 *
 * The QR text is "ws://<ip>:<port>/ws?token=<token>", the URL the Quest connects to, and the QR also
 * marks the face position. Without a Wi-Fi IP address a guidance text is shown instead, and the page
 * switches to the QR by itself once an address is assigned (call update() periodically).
 *
 * Every method that touches LVGL takes LvglLockGuard itself, so callers may be on any task.
 */
class Qr {
public:
    Qr();
    ~Qr();

    /** Set what goes into the QR. Call before show(). */
    void setup(const std::string& token, uint16_t port);
    /** Show the page (rebuilds it if already shown). */
    void show();
    /** Remove the page and give the face back. No-op if not shown. */
    void hide();
    /** Cheap; call from the app loop. Rebuilds the page when the IP address changed. */
    void update();
    bool isShown() const;

    /** Current Wi-Fi station IPv4 address as text, empty if none. */
    static std::string getIp();
    /** The text encoded in the QR. Empty if ip is empty. */
    std::string makePayload(const std::string& ip) const;

private:
    void build();
    void destroy();

    std::string _token;
    uint16_t _port = 0;
    std::string _ip;  // address the page was built for
    bool _shown = false;
    int64_t _last_check_us = 0;
    lv_obj_t* _root = nullptr;
    std::unique_ptr<smooth_ui_toolkit::lvgl_cpp::Qrcode> _qrcode;
};

}  // namespace mr_link
