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
 * @brief What the face shows. The first six are the official emotions as they are;
 * the others combine an emotion with decorators (drool, hearts, blush).
 */
enum class Mood : int {
    Neutral = 0,
    Happy,
    Angry,
    Sad,
    Doubt,
    Sleepy,
    Hungry,     // chasing food: excited face with drool
    Needy,      // chasing the player: sad face with worried drops
    Satisfied,  // just ate: chews, then a happy face with hearts
};

/**
 * @brief Reflects "emotion" messages on the avatar and flashes Happy when a client connects.
 *
 * A message with durationMs > 0 is shown for that long and then the face goes back to the base mood.
 * A message with no duration (0 or less) sets the base mood: it is shown right away, or after the
 * timed one ends if one is showing. Unity sends the base mood whenever StackChan's state changes.
 *
 * The server handlers run on the HTTP task, so they only record a request under a mutex.
 * update() applies it to the avatar and handles the revert timer; call it from onRunning().
 */
class EmotionController {
public:
    /** Subscribe to the server. Call once per open. */
    void attach(Server& server);
    /** Unsubscribe, forget pending requests and remove the decorators. */
    void detach(Server& server);
    /** Apply pending requests and revert to the base mood when the time is up. Call from the main task. */
    void update();

private:
    /** Set emotion and decorators for a mood. Caller holds the LVGL lock. */
    void show(Mood mood);
    void clearDecorators();
    /** Chew animation right after a Satisfied face is shown. Caller is the main task. */
    void munch();

    std::vector<int> _handler_ids;

    std::mutex _mutex;  // guards the request below
    bool _has_request = false;
    int _request_mood = 0;  // Mood as int
    int _request_duration_ms = 0;

    // Main task only
    Mood _base = Mood::Neutral;
    bool _reverting = false;
    int64_t _revert_at_us = 0;
    Mood _shown = Mood::Neutral;
    int64_t _munch_start_us = 0;
    bool _munching = false;
    std::vector<int> _decorator_ids;
};

}  // namespace mr_link
