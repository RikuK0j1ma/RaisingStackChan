/*
 * SPDX-License-Identifier: MIT
 */
#pragma once
#include "mr_link_emotion.h"
#include "mr_link_neck.h"
#include "mr_link_touch.h"
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
    mr_link::EmotionController _emotion;
    mr_link::NeckLock _neck;
    mr_link::TouchTrigger _touch;
    std::vector<int> _handler_ids;  // mr_link::Server subscriptions made while open
};
