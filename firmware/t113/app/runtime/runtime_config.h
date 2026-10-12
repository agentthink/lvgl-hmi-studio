/*
 * runtime_config.h — HMI 运行时配置
 *
 * 板级差异（触摸设备/量程、串口分配、RS485 方向、G2D 能力、串口设置页列表）
 * 已全部收敛到 board_profile.h —— 本文件只保留与板无关的运行参数，
 * 并转出 board_profile.h 的宏以便既有 include 者继续使用。
 */
#ifndef HMI_RUNTIME_CONFIG_H
#define HMI_RUNTIME_CONFIG_H

#include "board_profile.h"

/* ── 开机设置入口窗口（毫秒） ──
 * 开机后该时长内单击屏幕任意位置进入系统设置；窗口超时未点击、或退出
 * 设置页后，自动进入工程运行页（第 0 屏）。设 0 禁用开机窗口。 */
#define HMI_SETTINGS_BOOT_WINDOW_MS  5000

/* ── I2C（复用串口管脚，需按 dts 说明切换，见 tina/dts/README.md） ── */
#define HMI_DEV_I2C0        "/dev/i2c-0"
#define HMI_DEV_I2C1        "/dev/i2c-1"
#define HMI_DEV_I2C3        "/dev/i2c-3"

/* ── Modbus RTU（RS485） ── */
#define HMI_MBUS_RTU_DEV    HMI_DEV_UART_RS485
#define HMI_MBUS_RTU_BAUD   9600
#define HMI_MBUS_RTU_PARITY 'N'
#define HMI_MBUS_RTU_TMO_MS 300            /* 单帧响应超时 */

/* ── Modbus TCP（eth0） ── */
#define HMI_MBUS_TCP_HOST   "192.168.100.10"
#define HMI_MBUS_TCP_PORT   502
#define HMI_MBUS_TCP_TMO_MS 3000

/* ── MQTT（eth0/wlan0，需先联网） ──
 *
 * ⚠ **下面这对是出厂演示默认口令（hmi / hmi123），不是生产凭据**：本仓库不含任何现场口令。
 *   现场部署**必须**改掉 —— 改设备上的 `/mnt/UDISK/hmi/comm_config.ini`（优先，见 comm_core.c 的
 *   运行时覆盖）或改这里重编固件；同时应在 broker 侧开 allow_anonymous=false 并配主题 ACL。
 *   （2026-10-11 阶段 296 的卫生项：给硬编码默认口令加"部署必改"标注；守卫见
 *     tests/HmiEditor.Core.Tests/ShippedCredentialHygieneTests.cs）
 */
#define HMI_MQTT_HOST       "192.168.100.10"
#define HMI_MQTT_PORT       1883
#define HMI_MQTT_CLIENT_ID  "hmi-t113"
#define HMI_MQTT_USER       "hmi"
#define HMI_MQTT_PASS       "hmi123"       /* ⚠ 出厂默认，部署必改 */
#define HMI_MQTT_TOPIC_PRE  "hmi/t113"     /* 发布: <pre>/val/<tag>  订阅: <pre>/set/<tag> */
#define HMI_MQTT_KEEPALIVE  30             /* 秒 */

/* ── 变量表轮询（毫秒；块读由 variable_table.c 自动合并） ──
 *
 * 值优先取编辑器下发的 generated_runtime.h（RuntimeConfig.ScanIntervalMs）；
 * 缺这份头时回退下面的历史默认值 —— 于是没带生成头的工程编译得过、行为与历史一致，
 * 带上就自动跟随工程设置里的「扫描周期」。
 *
 * 与 generated_security.h 的回退有一条**语义差别**，别照抄那边：权限位在功能关闭时
 * 所有引用都被编译掉了，所以那边回退分支刻意不提供任何数值；而轮询周期是采集循环
 * **无条件要用**的参数，必须有个值，所以这里回退到默认值。 */
#if defined(__has_include)
#  if __has_include("generated_runtime.h")
#    include "generated_runtime.h"
#  endif
#endif

#ifndef HMI_POLL_INTERVAL_MS
#  define HMI_POLL_INTERVAL_MS 100         /* 无生成头时的默认周期 */
#endif

/* ── GPIO（PE0/PE1/PE8/PE9 普通 GPIO，见 tina/dts/README.md） ── */
#define HMI_GPIO_PE0        128             /* 4*32+0 */
#define HMI_GPIO_PE1        129
#define HMI_GPIO_PE8        136
#define HMI_GPIO_PE9        137

/* ── PWM（PWM2/PWM3，需在 dts 使能，见 tina/dts/README.md） ── */
#define HMI_PWM_CHIP        0               /* /sys/class/pwm/pwmchip0 */
#define HMI_PWM2            "2"             /* export 编号 */
#define HMI_PWM3            "3"

#endif /* HMI_RUNTIME_CONFIG_H */
