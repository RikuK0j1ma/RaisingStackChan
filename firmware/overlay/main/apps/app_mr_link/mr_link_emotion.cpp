/*
 * SPDX-License-Identifier: MIT
 */
#include "mr_link_emotion.h"
#include <esp_timer.h>
#include <mooncake.h>
#include <mooncake_log.h>
#include <smooth_lvgl.hpp>
#include <stackchan/stackchan.h>
#include <cstring>

using namespace mr_link;
using stackchan::avatar::Emotion;

static constexpr int kDefaultDurationMs = 3000;
static constexpr int kMaxDurationMs = 60000;
static constexpr int kConnectedDurationMs = 1500;
static const char* _tag = "MR LINK";

static bool parseEmotion(const char* value, Emotion& out)
{
    struct Entry {
        const char* name;
        Emotion emotion;
    };
    static constexpr Entry table[] = {
        {"neutral", Emotion::Neutral}, {"happy", Emotion::Happy}, {"angry", Emotion::Angry},
        {"sad", Emotion::Sad},         {"doubt", Emotion::Doubt}, {"sleepy", Emotion::Sleepy},
    };
    for (const auto& e : table) {
        if (std::strcmp(value, e.name) == 0) {
            out = e.emotion;
            return true;
        }
    }
    return false;
}

void EmotionController::attach(Server& server)
{
    // HTTP task: only record the request. The avatar is touched in update().
    _handler_ids.push_back(server.addMessageHandler("emotion", [this](const ArduinoJson::JsonDocument& doc) {
        const char* value = doc["value"] | "";
        Emotion emotion = Emotion::Neutral;
        if (!parseEmotion(value, emotion)) {
            mclog::tagWarn(_tag, "unknown emotion: {}", value);
            return;
        }
        int duration = doc["durationMs"] | kDefaultDurationMs;  // 0 or less: keep until the next message
        if (duration > kMaxDurationMs) {
            duration = kMaxDurationMs;
        }
        std::lock_guard<std::mutex> lock(_mutex);
        _has_request = true;
        _request_emotion = static_cast<int>(emotion);
        _request_duration_ms = duration;
    }));

    // A client has just said hello: flash Happy so the user notices the link is up.
    // TODO(#2): the QR view is dismissed by the QR feature on the same event.
    _handler_ids.push_back(server.addConnectionHandler([this](bool connected) {
        if (!connected) {
            return;
        }
        std::lock_guard<std::mutex> lock(_mutex);
        _has_request = true;
        _request_emotion = static_cast<int>(Emotion::Happy);
        _request_duration_ms = kConnectedDurationMs;
    }));
}

void EmotionController::detach(Server& server)
{
    for (int id : _handler_ids) {
        server.removeHandler(id);
    }
    _handler_ids.clear();

    {
        std::lock_guard<std::mutex> lock(_mutex);
        _has_request = false;
    }
    if (_reverting) {
        _reverting = false;
        auto& sc = stackchan::GetStackChan();
        if (sc.hasAvatar()) {
            LvglLockGuard lock;
            sc.avatar().setEmotion(Emotion::Neutral);
        }
    }
}

void EmotionController::update()
{
    bool has_request = false;
    int emotion = 0;
    int duration_ms = 0;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        if (_has_request) {
            has_request = true;
            emotion = _request_emotion;
            duration_ms = _request_duration_ms;
            _has_request = false;
        }
    }

    const bool timed_out = _reverting && esp_timer_get_time() >= _revert_at_us;
    if (!has_request && !timed_out) {
        return;
    }

    auto& sc = stackchan::GetStackChan();
    if (!sc.hasAvatar()) {
        return;
    }

    LvglLockGuard lock;
    if (has_request) {
        sc.avatar().setEmotion(static_cast<Emotion>(emotion));
        // A new request always resets the timer. Neutral or no duration needs no revert.
        _reverting = duration_ms > 0 && static_cast<Emotion>(emotion) != Emotion::Neutral;
        if (_reverting) {
            _revert_at_us = esp_timer_get_time() + static_cast<int64_t>(duration_ms) * 1000;
        }
    } else {
        sc.avatar().setEmotion(Emotion::Neutral);
        _reverting = false;
    }
}
