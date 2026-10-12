/*
 * board_profile_test.c — 板级配置（board_profile.h）的主机侧单元测试
 *
 * 不依赖硬件：board_profile.h 是纯宏，可直接在主机上按不同 HMI_BOARD_WAVEPI 编译校验。
 * 覆盖两类断言：
 *   1) 具体取值 —— WavePI(mq_r) 各自的触摸设备/量程、串口、RS485、串口设置页列表；
 *   2) 跨字段不变量 —— 与板型无关的一致性约束，正是本项重构要防住的漂移类型
 *      （例如"stty 列表与下拉菜单条数不一致"、"RS485 软件模式却没给方向 GPIO"）。
 * 由 run_host_tests.sh 在 WavePI / mq_r / -D 覆盖三种配置下各跑一遍。
 */
#include <stdio.h>
#include <stdint.h>
#include <string.h>

#include "board_profile.h"

static int s_pass;
static int s_fail;

static void check_str(const char *name, const char *got, const char *want)
{
    if (got != NULL && strcmp(got, want) == 0) {
        s_pass++;
        printf("  PASS  %s\n", name);
    } else {
        s_fail++;
        printf("  FAIL  %s (got \"%s\", want \"%s\")\n", name,
               got ? got : "(null)", want);
    }
}

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

static void check_true(const char *name, int cond)
{
    if (cond) {
        s_pass++;
        printf("  PASS  %s\n", name);
    } else {
        s_fail++;
        printf("  FAIL  %s\n", name);
    }
}

/* 统计 \n 分隔的条目数："a\nb\nc" -> 3 */
static int menu_entry_count(const char *menu)
{
    int n = 1;
    const char *p;
    if (menu == NULL || *menu == '\0') return 0;
    for (p = menu; *p; ++p)
        if (*p == '\n') n++;
    return n;
}

/* ── 与板型无关的不变量 ── */
static void check_invariants(void)
{
    static const char *const devs[] = HMI_SERIAL_DEVS_STTY;
    int stty_count = (int)(sizeof(devs) / sizeof(devs[0]));
    int menu_count = menu_entry_count(HMI_SERIAL_DEVS_MENU);

    /* 串口设置页两处列表必须同样条数（否则下拉选中项与 stty 目标错位） */
    check_i32("不变量: stty 列表条数 == 菜单条数", stty_count, menu_count);
    check_true("不变量: 默认串口序号在范围内",
               HMI_SERIAL_DEVS_DEFAULT >= 0 && HMI_SERIAL_DEVS_DEFAULT < menu_count);

    /* 触摸量程必须为正且不超出合理范围（0 会让归一化退化） */
    check_true("不变量: 触摸量程 X > 0", HMI_TOUCH_MAX_X > 0);
    check_true("不变量: 触摸量程 Y > 0", HMI_TOUCH_MAX_Y > 0);

    /* RS485：软件方向控制（!HMI_BOARD_RS485_HW）必须给出方向 GPIO，反之必须为 0 */
    if (HMI_BOARD_RS485_HW)
        check_i32("不变量: 硬件 RS485 时方向 GPIO=0", HMI_RS485_GPIO_PIN, 0);
    else
        check_true("不变量: GPIO 方向控制时必须给出 GPIO 号", HMI_RS485_GPIO_PIN > 0);

    /* 调试口与 RS485 口不能是同一个设备节点 */
    check_true("不变量: 调试口 != RS485 口",
               strcmp(HMI_DEV_UART_DEBUG, HMI_DEV_UART_RS485) != 0);

    /* 板名非空 */
    check_true("不变量: 板名非空", HMI_BOARD_NAME[0] != '\0');
}

int main(void)
{
    printf("[board_profile] 板级配置 (HMI_BOARD_WAVEPI=%d)\n", HMI_BOARD_WAVEPI);

#ifdef HMI_BOARD_PROFILE_TEST_OVERRIDE
    /* -D 覆盖场景：只验证覆盖生效与不变量，不锁具体板型取值 */
    (void)check_str;   /* 该分支不用字符串断言，显式标记以免 -Wunused-function */
    check_i32("-D 覆盖: HMI_TOUCH_MAX_X 生效", HMI_TOUCH_MAX_X, HMI_BOARD_PROFILE_TEST_OVERRIDE);
    check_true("-D 覆盖: 板名仍非空", HMI_BOARD_NAME[0] != '\0');
#elif HMI_BOARD_WAVEPI
    check_str("板名", HMI_BOARD_NAME, "WavePI-S6 (bingpi_m2, 7\" 1024x600)");
    check_str("触摸设备 (电容 event2)", HMI_TOUCH_DEV, "/dev/input/event2");
    check_i32("触摸量程 X (DTS 0x320)", HMI_TOUCH_MAX_X, 800);
    check_i32("触摸量程 Y (DTS 0x1e0)", HMI_TOUCH_MAX_Y, 480);
    check_str("调试串口", HMI_DEV_UART_DEBUG, "/dev/ttyS3");
    check_str("RS485 串口", HMI_DEV_UART_RS485, "/dev/ttyS4");
    check_i32("RS485 方向 GPIO (PG8=200)", HMI_RS485_GPIO_PIN, 200);
    check_i32("RS485 模式 (0=GPIO 方向)", HMI_BOARD_RS485_HW, 0);
    check_i32("G2D 能力 (WavePI 不可用)", HMI_BOARD_G2D_CAPABLE, 0);
    check_str("串口设置页第 1 项", ((const char *const[])HMI_SERIAL_DEVS_STTY)[0], "/dev/ttyS0");
    check_i32("串口设置页默认项 (RS485)", HMI_SERIAL_DEVS_DEFAULT, 3);
#else
    check_str("板名", HMI_BOARD_NAME, "t113-mq_r (7\" 1024x600)");
    check_str("触摸设备 (电阻 event0)", HMI_TOUCH_DEV, "/dev/input/event0");
    check_i32("触摸量程 X (沿用历史默认)", HMI_TOUCH_MAX_X, 1024);
    check_i32("触摸量程 Y (沿用历史默认)", HMI_TOUCH_MAX_Y, 600);
    check_str("调试串口", HMI_DEV_UART_DEBUG, "/dev/ttyS1");
    check_str("RS485 串口", HMI_DEV_UART_RS485, "/dev/ttyS3");
    check_i32("RS485 方向 GPIO (硬件模式=0)", HMI_RS485_GPIO_PIN, 0);
    check_i32("RS485 模式 (1=硬件)", HMI_BOARD_RS485_HW, 1);
    check_i32("G2D 能力 (mq_r 可用)", HMI_BOARD_G2D_CAPABLE, 1);
    check_str("串口设置页第 1 项", ((const char *const[])HMI_SERIAL_DEVS_STTY)[0], "/dev/ttyS2");
    check_i32("串口设置页默认项 (RS485)", HMI_SERIAL_DEVS_DEFAULT, 1);
#endif

    check_invariants();

    printf("=== 结果: %d 通过, %d 失败 ===\n", s_pass, s_fail);
    return s_fail == 0 ? 0 : 1;
}
