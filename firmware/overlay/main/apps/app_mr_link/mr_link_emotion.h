/*
 * SPDX-License-Identifier: MIT
 */
#pragma once
#include "mr_link_server.h"
#include <cstdint>
#include <mutex>
#include <vector>

namespace mr_link {

/**
 * @brief Reflects "emotion" messages on the avatar and flashes Happy when a client connects.
 *
 * The server handlers run on the HTTP task, so they only record a request under a mutex.
 * update() applies it to the avatar and handles the revert timer; call it from onRunning().
 */
class EmotionController {
public:
    /** Subscribe to the server. Call once per open. */
    void attach(Server& server);
    /** Unsubscribe and forget pending requests. */
    void detach(Server& server);
    /** Apply pending requests and revert to Neutral when the time is up. Call from the main task. */
    void update();

private:
    std::vector<int> _handler_ids;

    std::mutex _mutex;  // guards the request below
    bool _has_request = false;
    int _request_emotion = 0;  // stackchan::avatar::Emotion as int, keeps this header light
    int _request_duration_ms = 0;

    // Main task only
    bool _reverting = false;
    int64_t _revert_at_us = 0;
};

}  // namespace mr_link
