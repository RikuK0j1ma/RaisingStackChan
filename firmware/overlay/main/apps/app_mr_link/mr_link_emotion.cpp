/*
 * SPDX-License-Identifier: MIT
 */
#include "mr_link_emotion.h"
#include "mr_link_drool.h"
#include <esp_timer.h>
#include <mooncake.h>
#include <mooncake_log.h>
#include <smooth_lvgl.hpp>
#include <stackchan/stackchan.h>
#include <cstring>

using namespace mr_link;
using namespace stackchan::avatar;

static constexpr int kMaxDurationMs = 60000;
static constexpr int kConnectedDurationMs = 1500;
static const char* _tag = "MOGUMOGU";

static bool parseMood(const char* value, Mood& out)
{
    struct Entry {
        const char* name;
        Mood mood;
    };
    static constexpr Entry table[] = {
        {"neutral", Mood::Neutral}, {"happy", Mood::Happy},   {"angry", Mood::Angry},
        {"sad", Mood::Sad},         {"doubt", Mood::Doubt},   {"sleepy", Mood::Sleepy},
        {"hungry", Mood::Hungry},   {"needy", Mood::Needy},   {"satisfied", Mood::Satisfied},
    };
    for (const auto& e : table) {
        if (std::strcmp(value, e.name) == 0) {
            out = e.mood;
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
        Mood mood = Mood::Neutral;
        if (!parseMood(value, mood)) {
            mclog::tagWarn(_tag, "unknown emotion: {}", value);
            return;
        }
        int duration = doc["durationMs"] | 0;  // 0 or less: this is the base mood, kept until the next one
        if (duration > kMaxDurationMs) {
            duration = kMaxDurationMs;
        }
        std::lock_guard<std::mutex> lock(_mutex);
        _has_request = true;
        _request_mood = static_cast<int>(mood);
        _request_duration_ms = duration;
    }));

    // A client has just said hello: flash Happy so the user notices the link is up.
    // The QR view is dismissed by AppMrLink on the same event.
    _handler_ids.push_back(server.addConnectionHandler([this](bool connected) {
        if (!connected) {
            return;
        }
        std::lock_guard<std::mutex> lock(_mutex);
        _has_request = true;
        _request_mood = static_cast<int>(Mood::Happy);
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
    _base = Mood::Neutral;
    _reverting = false;
    auto& sc = GetStackChan();
    if (sc.hasAvatar()) {
        LvglLockGuard lock;
        show(Mood::Neutral);
    }
}

void EmotionController::clearDecorators()
{
    auto& avatar = GetStackChan().avatar();
    for (int id : _decorator_ids) {
        avatar.removeDecorator(id);
    }
    _decorator_ids.clear();
}

void EmotionController::show(Mood mood)
{
    auto& avatar = GetStackChan().avatar();
    clearDecorators();

    Emotion emotion = Emotion::Neutral;
    switch (mood) {
        case Mood::Happy:
        case Mood::Hungry:
        case Mood::Satisfied:
            emotion = Emotion::Happy;
            break;
        case Mood::Angry:
            emotion = Emotion::Angry;
            break;
        case Mood::Sad:
        case Mood::Needy:
            emotion = Emotion::Sad;
            break;
        case Mood::Doubt:
            emotion = Emotion::Doubt;
            break;
        case Mood::Sleepy:
            emotion = Emotion::Sleepy;
            break;
        default:
            break;
    }
    avatar.setEmotion(emotion);

    lv_obj_t* screen = lv_screen_active();
    switch (mood) {
        case Mood::Hungry:
            _decorator_ids.push_back(avatar.addDecorator(std::make_unique<DroolDecorator>(screen)));
            break;
        case Mood::Needy:
            _decorator_ids.push_back(avatar.addDecorator(std::make_unique<ShyDecorator>(screen)));
            break;
        case Mood::Satisfied:
            _decorator_ids.push_back(avatar.addDecorator(std::make_unique<HeartDecorator>(screen)));
            _decorator_ids.push_back(avatar.addDecorator(std::make_unique<ShyDecorator>(screen)));
            break;
        default:
            break;
    }
}

void EmotionController::update()
{
    bool has_request = false;
    Mood mood = Mood::Neutral;
    int duration_ms = 0;
    {
        std::lock_guard<std::mutex> lock(_mutex);
        if (_has_request) {
            has_request = true;
            mood = static_cast<Mood>(_request_mood);
            duration_ms = _request_duration_ms;
            _has_request = false;
        }
    }

    const bool timed_out = _reverting && esp_timer_get_time() >= _revert_at_us;
    if (!has_request && !timed_out) {
        return;
    }

    auto& sc = GetStackChan();
    if (!sc.hasAvatar()) {
        return;
    }

    LvglLockGuard lock;
    if (has_request) {
        if (duration_ms > 0 && mood != Mood::Neutral) {
            // Timed: show it now and come back to the base mood. A new timed one resets the timer.
            show(mood);
            _reverting = true;
            _revert_at_us = esp_timer_get_time() + static_cast<int64_t>(duration_ms) * 1000;
        } else {
            // Base mood: shown now unless a timed one is still showing.
            _base = mood;
            if (!_reverting) {
                show(mood);
            }
        }
    } else {
        _reverting = false;
        show(_base);
    }
}
