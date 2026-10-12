/*
 * touch_scale.h — 触摸坐标归一化（纯函数，无任何依赖）
 *
 * 为什么单独成头文件：坐标换算的正确性——尤其是「触摸控制器量程 ≠ LCD 分辨率」
 * 时屏幕边缘是否可达——必须能被主机侧单元测试覆盖，而不是只能靠实机试着点。
 * touch_evdev.c 与 tests/touch_scale_test.c 共用本实现。
 *
 * 背景（2026-09-13 审查 P0-7）：WavePI-S6 的 GT911 按板级 DTS 上报 0..800 / 0..480，
 * 驱动却按 0..1024 / 0..600 归一，导致 1024×600 屏右侧 224px、底部 120px 触摸不可达。
 */
#ifndef HMI_TOUCH_SCALE_H
#define HMI_TOUCH_SCALE_H

#include <stdint.h>

/*
 * 把触摸控制器上报的原始坐标 v 从 [0, max] 线性映射到 [0, target]。
 *
 * - max <= 0 视为「未配置量程」：按 target 归一（1:1，不缩放）；
 * - target <= 0 返回 0；
 * - 结果夹取到 [0, target]，避免设备异常值时给出画布外坐标；
 * - 中间量用 int64，避免 v * target 在大屏/大量程下溢出。
 */
static inline int32_t touch_scale_axis(int32_t v, int32_t max, int32_t target)
{
    int32_t span;
    int64_t scaled;

    if (target <= 0) return 0;

    span = (max > 0) ? max : target;
    if (span <= 0) return 0;

    scaled = ((int64_t)v * (int64_t)target) / (int64_t)span;
    if (scaled < 0) return 0;
    if (scaled > (int64_t)target) return target;
    return (int32_t)scaled;
}

#endif /* HMI_TOUCH_SCALE_H */
