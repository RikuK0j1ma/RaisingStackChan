/*
 * SPDX-License-Identifier: MIT
 */
#pragma once
#include <atomic>
#include <mutex>
#include <string>

namespace mr_link {

/** Network settings carried by a Wi-Fi share QR ("WIFI:T:WPA;S:<ssid>;P:<password>;;"). */
struct WifiCredentials {
    std::string ssid;
    std::string password;  // empty for an open network
};

/**
 * @brief Parse the text of a Wi-Fi share QR (the format phones show for "share Wi-Fi").
 * @return false if it is not a Wi-Fi QR, or the network cannot be joined (no SSID, WEP, short WPA password).
 */
bool parseWifiQr(const std::string& text, WifiCredentials& out);

/**
 * @brief Reads Wi-Fi share QRs with the StackChan camera.
 *
 * start() launches a low priority task that grabs camera frames, decodes QR codes (quirc) and keeps the
 * first valid Wi-Fi QR. The app loop calls poll(), which returns true once when one was read.
 * stop() ends the task. The camera is shared with nothing else while MOGUMOGU is open.
 */
class WifiQrScanner {
public:
    ~WifiQrScanner();

    /** False if the board has no camera. No-op while already running. */
    bool start();
    void stop();
    bool isRunning() const;

    /** True once per read QR. Cheap; call from the app loop. */
    bool poll(WifiCredentials& out);

private:
    static void taskEntry(void* arg);
    void run();

    std::atomic<bool> _run{false};
    std::atomic<bool> _alive{false};  // the task has not returned yet
    std::mutex _mutex;
    WifiCredentials _found;
    bool _has_found = false;
};

}  // namespace mr_link
