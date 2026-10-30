/*
 * SPDX-License-Identifier: MIT
 */
#pragma once
#include <stackchan/avatar/avatar/decorator.h>
#include <lvgl.h>
#include <smooth_lvgl.hpp>
#include <cstdint>
#include <memory>

namespace mr_link {

/**
 * @brief A drop of drool that grows at the corner of the mouth and falls, repeatedly.
 *
 * The official decorators have no drool, so this one draws a rounded bar with plain LVGL.
 * Positions are relative to the screen center (the default mouth is at about y = 26).
 */
class DroolDecorator : public stackchan::avatar::Decorator {
public:
    explicit DroolDecorator(lv_obj_t* parent);

    void _update() override;
    void setVisible(bool visible) override;

private:
    std::unique_ptr<uitk::lvgl_cpp::Container> _drop;
    uint32_t _started_at = 0;
};

}  // namespace mr_link
