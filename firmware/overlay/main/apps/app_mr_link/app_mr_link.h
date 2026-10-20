/*
 * SPDX-License-Identifier: MIT
 */
#pragma once
#include <mooncake.h>
#include <vector>

/**
 * @brief MR Link: shows a QR code for the Quest app and reacts to its commands
 *
 */
class AppMrLink : public mooncake::AppAbility {
public:
    AppMrLink();

    void onCreate() override;
    void onOpen() override;
    void onRunning() override;
    void onClose() override;

private:
    std::vector<int> _handler_ids;  // mr_link::Server subscriptions made while open
};
