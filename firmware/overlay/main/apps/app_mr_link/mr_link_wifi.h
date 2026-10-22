/*
 * SPDX-License-Identifier: MIT
 */
#pragma once
#include "mr_link_wifi_scan.h"
#include <cstdint>
#include <string>

namespace mr_link {

/**
 * @brief Wi-Fi setup without the StackChan World app.
 *
 * Uses the official WifiManager (esp-wifi-connect):
 * - Saved networks exist: connect to them as a station.
 * - None saved, or the connection times out: start the configuration AP (open, "MOGUMOGU-XXXX").
 *   The phone joins it and enters SSID / password on the web page at the AP address. The page saves
 *   them to NVS and ends the config mode, then this class connects as a station.
 * - While the config AP is up the camera also looks for a Wi-Fi share QR (the one phones show for
 *   "share Wi-Fi"). A QR that is read is saved the same way and the station connects right away.
 *
 * Call start() on open, update() from the app loop (cheap) and stop() on close.
 */
class WifiSetup {
public:
    enum class State {
        Idle,
        Connecting,  // station mode, waiting for an IP address
        Config,      // configuration AP is up: show apSsid() / apUrl()
        Connected,
        Failed,  // the Wi-Fi driver could not be initialized (e.g. owned by another feature)
    };

    void start();
    void update();
    void stop();

    State state() const;
    /** Valid in State::Config. */
    std::string apSsid() const;
    std::string apUrl() const;

private:
    void beginStation();
    void beginConfig();
    void joinScanned(const WifiCredentials& creds);

    State _state = State::Idle;
    int64_t _station_started_us = 0;
    bool _started_config = false;
    WifiQrScanner _scanner;
};

}  // namespace mr_link
