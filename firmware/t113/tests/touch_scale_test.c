/*
 * touch_scale_test.c — 触摸坐标归一化的主机侧单元测试
 *
 * 不依赖 T113 硬件 / LVGL / Tina SDK：只编译纯函数 touch_scale.h。
 * 覆盖审查 P0-7 的核心回归：触摸控制器量程（800×480）≠ LCD 分辨率（1024×600）时，
 * 屏幕右缘 / 下缘必须可达；同时把「错误的旧量程」固化为护栏用例。
 */
#include <stdio.h>
#include <stdint.h>

#include "touch_scale.h"
#include "runtime_config.h"   /* 纯宏，无硬件依赖：用于断言板级量程与覆盖优先级 */

static int s_pass;
static int s_fail;

static void check_i32(const char *name, int32_t got, int32_t want)
{
    if (got == want) {
        s_pass++;
        printf("  PASS  %s\n", name);
    } else {
        s_fail++;
        printf("  FAIL  %s (got %d, want %d)\n", name, (int)got, (int)want);
    }
}

/* 板级量程断言：编译期即可校验，避免"驱动默认值/板级值"再次分叉 */
static void check_board_touch_range(void)
{
#ifdef HMI_TOUCH_SCALE_TEST_OVERRIDE_X
    /* 覆盖优先级实测：构建时以 -DHMI_TOUCH_MAX_X=<值> 传入 */
    check_i32("-D 覆盖优先于板级默认 (X)", HMI_TOUCH_MAX_X, HMI_TOUCH_SCALE_TEST_OVERRIDE_X);
#elif HMI_BOARD_WAVEPI
    /* WavePI-S6：板级 DTS ctp_screen_max_x=0x320 / ctp_screen_max_y=0x1e0 */
    check_i32("板级量程 X (WavePI/DTS 800)", HMI_TOUCH_MAX_X, 800);
    check_i32("板级量程 Y (WavePI/DTS 480)", HMI_TOUCH_MAX_Y, 480);
#else
    /* t113-mq_r：沿用历史默认（待该板实机确认） */
    check_i32("板级量程 X (mq_r 1024)", HMI_TOUCH_MAX_X, 1024);
    check_i32("板级量程 Y (mq_r 600)", HMI_TOUCH_MAX_Y, 600);
#endif
}

int main(void)
{
    printf("[touch_scale] 触摸坐标归一化\n");

    /* ── WavePI-S6：控制器 0..800 / 0..480 → LCD 1024×600 ── */
    check_i32("x=0   -> 0",            touch_scale_axis(0,   800, 1024), 0);
    check_i32("x=400 -> 512 (中点)",   touch_scale_axis(400, 800, 1024), 512);
    check_i32("x=800 -> 1024 (右缘可达)", touch_scale_axis(800, 800, 1024), 1024);
    check_i32("y=240 -> 300 (中点)",   touch_scale_axis(240, 480, 600), 300);
    check_i32("y=480 -> 600 (下缘可达)", touch_scale_axis(480, 480, 600), 600);

    /* ── 回归护栏：按 1024×600 归一（旧代码的兜底值）时右缘不可达 ── */
    check_i32("旧错误量程 x=800 -> 800（右缘不可达）",
              touch_scale_axis(800, 1024, 1024), 800);
    check_i32("旧错误量程 y=480 -> 480（下缘不可达）",
              touch_scale_axis(480, 600, 600), 480);

    /* ── 与面板同量程（mq_r 历史默认）：1:1 ── */
    check_i32("1024 量程 x=512 -> 512", touch_scale_axis(512, 1024, 1024), 512);
    check_i32("600 量程 y=300 -> 300",  touch_scale_axis(300, 600, 600), 300);

    /* ── 防御分支 ── */
    check_i32("max=0 视为 1:1",       touch_scale_axis(300, 0, 1024), 300);
    check_i32("负坐标夹到 0",          touch_scale_axis(-50, 800, 1024), 0);
    check_i32("超量程夹到 target",     touch_scale_axis(2000, 800, 1024), 1024);
    check_i32("target=0 返回 0",       touch_scale_axis(500, 800, 0), 0);

    /* ── 大屏/大量程不溢出（int64 中间量） ── */
    check_i32("大值不溢出 x=4000/4096 -> 4000",
              touch_scale_axis(4000, 4096, 4096), 4000);

    /* ── 板级量程 / -D 覆盖优先级 ── */
    check_board_touch_range();

    printf("=== 结果: %d 通过, %d 失败 ===\n", s_pass, s_fail);
    return s_fail == 0 ? 0 : 1;
}
