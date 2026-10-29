/*
 * SPDX-License-Identifier: MIT
 */
#include "mr_link_drool.h"
#include <hal/hal.h>

using namespace mr_link;
using namespace uitk::lvgl_cpp;

// Corner of the mouth; the drop hangs down from here.
static constexpr int kDropX = 24;
static constexpr int kDropTopY = 36;
static constexpr int kDropWidth = 10;
static constexpr int kMinLength = 6;
static constexpr int kMaxLength = 30;
static constexpr uint32_t kGrowMs = 1400;  // grows for this long, then falls
static constexpr uint32_t kFallMs = 500;
static constexpr int kFallDistance = 50;
static const lv_color_t kDropColor = lv_color_hex(0x75E1FF);

DroolDecorator::DroolDecorator(lv_obj_t* parent)
{
    _drop = std::make_unique<Container>(parent);
    _drop->setAlign(LV_ALIGN_CENTER);
    _drop->setBorderWidth(0);
    _drop->setPaddingAll(0);
    _drop->setRadius(kDropWidth / 2);
    _drop->setBgColor(kDropColor);
    _drop->setScrollbarMode(LV_SCROLLBAR_MODE_OFF);
    _drop->removeFlag(LV_OBJ_FLAG_CLICKABLE);
    _drop->removeFlag(LV_OBJ_FLAG_SCROLLABLE);
    _drop->setSize(kDropWidth, kMinLength);
    _drop->setPos(kDropX, kDropTopY + kMinLength / 2);
    _started_at = GetHAL().millis();
}

void DroolDecorator::_update()
{
    if (!_visible) {
        return;
    }
    const uint32_t t = (GetHAL().millis() - _started_at) % (kGrowMs + kFallMs);
    int length = kMinLength;
    int fall = 0;
    if (t < kGrowMs) {
        length = kMinLength + (kMaxLength - kMinLength) * static_cast<int>(t) / static_cast<int>(kGrowMs);
    } else {
        length = kMaxLength;
        fall = kFallDistance * static_cast<int>(t - kGrowMs) / static_cast<int>(kFallMs);
    }
    _drop->setSize(kDropWidth, length);
    // Aligned at the center, so pos is the center of the bar; keep its top fixed while it grows.
    _drop->setPos(kDropX, kDropTopY + length / 2 + fall);
}

void DroolDecorator::setVisible(bool visible)
{
    Element::setVisible(visible);
    if (_drop) {
        _drop->setHidden(!visible);
    }
}
