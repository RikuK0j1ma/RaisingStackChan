/*
 * SPDX-License-Identifier: MIT
 */
#include "mr_link_wifi.h"
#include <esp_timer.h>
#include <mooncake_log.h>
#include <ssid_manager.h>
#include <wifi_manager.h>

namespace mr_link {

static const std::string _tag = "MOGUMOGU/wifi";

// A saved network that does not connect in this time (moved house, wrong password) opens the config AP
static constexpr int64_t kConnectTimeoutUs = 30 * 1000 * 1000;

static bool hasSavedNetwork()
{
    return !SsidManager::GetInstance().GetSsidList().empty();
}

void WifiSetup::start()
{
    auto& wifi = WifiManager::GetInstance();
    if (!wifi.IsInitialized()) {
        WifiManagerConfig config;
        config.ssid_prefix = "MOGUMOGU";
        config.language = "ja-JP";  // language of the setup web page
        if (!wifi.Initialize(config)) {
            mclog::tagError(_tag, "Wi-Fi init failed");
            _state = State::Failed;
            return;
        }
    }

    if (wifi.IsConnected()) {
        _state = State::Connected;
    } else if (wifi.IsConfigMode()) {
        _state = State::Config;
    } else if (hasSavedNetwork()) {
        beginStation();
    } else {
        beginConfig();
    }
}

void WifiSetup::beginStation()
{
    mclog::tagInfo(_tag, "connecting to a saved network");
    _state = State::Connecting;
    _station_started_us = esp_timer_get_time();
    WifiManager::GetInstance().StartStation();
}

void WifiSetup::beginConfig()
{
    auto& wifi = WifiManager::GetInstance();
    mclog::tagInfo(_tag, "starting the config AP");
    wifi.StartConfigAp();  // stops the station if it is active
    _started_config = true;
    _state = wifi.IsConfigMode() ? State::Config : State::Failed;
}

void WifiSetup::update()
{
    auto& wifi = WifiManager::GetInstance();
    switch (_state) {
        case State::Connecting:
            if (wifi.IsConnected()) {
                mclog::tagInfo(_tag, "connected: {}", wifi.GetIpAddress());
                _state = State::Connected;
            } else if (esp_timer_get_time() - _station_started_us >= kConnectTimeoutUs) {
                mclog::tagWarn(_tag, "connect timeout");
                wifi.StopStation();
                beginConfig();
            }
            break;
        case State::Config:
            // The web page ends the config mode once the user saved a network
            if (!wifi.IsConfigMode()) {
                if (hasSavedNetwork()) {
                    beginStation();
                } else {
                    beginConfig();
                }
            }
            break;
        case State::Connected:
            if (!wifi.IsConnected()) {
                // Lost the network. The station retries by itself; show the connecting page meanwhile.
                _state = State::Connecting;
                _station_started_us = esp_timer_get_time();
            }
            break;
        case State::Idle:
        case State::Failed:
            break;
    }
}

void WifiSetup::stop()
{
    auto& wifi = WifiManager::GetInstance();
    if (_started_config && wifi.IsInitialized() && wifi.IsConfigMode()) {
        wifi.StopConfigAp();
    }
    _started_config = false;
    _state = State::Idle;
}

WifiSetup::State WifiSetup::state() const
{
    return _state;
}

std::string WifiSetup::apSsid() const
{
    return WifiManager::GetInstance().GetApSsid();
}

std::string WifiSetup::apUrl() const
{
    return WifiManager::GetInstance().GetApWebUrl();
}

}  // namespace mr_link
