/*
 * SPDX-License-Identifier: MIT
 */
#pragma once

namespace mr_link {

/**
 * @brief The face MOGUMOGU shows once a Quest is connected.
 *
 * The face is the official DefaultAvatar (eyes and mouth) attached to GetStackChan(), so
 * EmotionController can change its expression. It is created under the QR page: create() it
 * first, then build the QR page (a full-screen object added later covers it). Hiding the QR
 * page reveals the face. To change how the face looks, change only mr_link_face.cpp.
 *
 * Takes LvglLockGuard itself.
 */
class Face {
public:
    /** Create the avatar on the active screen and attach it. No-op if it already exists. */
    void create();
    /** Destroy the avatar. No-op if it does not exist. */
    void destroy();
    bool exists() const
    {
        return _created;
    }

private:
    bool _created = false;
};

}  // namespace mr_link
