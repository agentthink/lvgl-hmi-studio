/*
 * board_profile.h — 板级差异的**唯一事实源**
 *
 * 所有随目标板变化的值集中在这里，由 HMI_BOARD_WAVEPI 派生：
 *   0 = t113-mq_r（旧板）
 *   1 = WavePI-S6 7 寸（bingpi_m2）
 *
 * 覆盖方式（优先级从高到低）：
 *   1) 编译期：-DHMI_BOARD_WAVEPI=0/1（构建脚本按 BOARD 注入，见 tina/build.sh +
 *      tina/package/hmi-runtime/Makefile），单项也可 -DHMI_TOUCH_MAX_X=... 直接覆盖；
 *   2) 本文件默认值（未注入时的兜底）。
 *
 * 为什么单独成文件（2026-09-13 审查阶段 1 第 7 项）：
 *   板级差异此前散落在 runtime_config.h、set_serial.c 的 #if 分支，以及构建脚本
 *   对已被跟踪源文件执行 `sed -i` 的"隐式配置"里 —— 后者会污染工作区、让构建不可复现。
 *   现在板级差异只在这里改一处，构建改为纯编译期注入（不再改写源文件）。
 */
#ifndef HMI_BOARD_PROFILE_H
#define HMI_BOARD_PROFILE_H

/* ── 目标板选择 ── */
#ifndef HMI_BOARD_WAVEPI
#define HMI_BOARD_WAVEPI   1
#endif

#if HMI_BOARD_WAVEPI
#  define HMI_BOARD_NAME       "WavePI-S6 (bingpi_m2, 7\" 1024x600)"
#  define HMI_BOARD_G2D_CAPABLE 0   /* WavePI 的 sunxifb G2D 走 ION，与 7 寸屏/内存布局不兼容 */
#  define HMI_BOARD_RS485_HW    0   /* 0=GPIO 方向控制（SP485 DE/RE）；1=硬件 RS485（dts + MCR） */
#else
#  define HMI_BOARD_NAME       "t113-mq_r (7\" 1024x600)"
#  define HMI_BOARD_G2D_CAPABLE 1   /* mq_r 的 ION/G2D 正常，可恢复硬件加速（需实机验证后再开） */
#  define HMI_BOARD_RS485_HW    1
#endif

/* 注意：G2D 的**实际开关**仍是 drivers/sunximem.h 的 USE_SUNXIFB_G2D
 * （当前全局 0：WavePI 必须禁用，而 mq_r 重新启用需单独实机验证，故未按本能力位自动切换）。
 * HMI_BOARD_G2D_CAPABLE 用于表达"这块板是不是硬件上支持"。 */

/* ── 显示（7 寸 1024×600 RGB，与 tina/dts LCD_RGB_1024X600 对齐） ── */
#define HMI_LCD_WIDTH       1024
#define HMI_LCD_HEIGHT      600

/* ── 触摸设备 ──
 * WavePI-S6 官方 7 寸电容屏（GT911 @ I2C 0x14）→ /dev/input/event2
 * mq_r 电阻屏（TPADC rtp）→ /dev/input/event0 */
#if HMI_BOARD_WAVEPI
#  define HMI_TOUCH_DEV     "/dev/input/event2"
#else
#  define HMI_TOUCH_DEV     "/dev/input/event0"
#endif
#define HMI_TOUCH_DEV_CAP   "/dev/input/event2"
#define HMI_TOUCH_DEV_RES   "/dev/input/event0"

/* ── 触摸控制器上报量程（≠ LCD 分辨率）──
 * 必须按「触摸控制器实际上报的坐标范围」归一，否则屏幕边缘点不到：
 *   WavePI-S6：板级 DTS 明确 ctp_screen_max_x=0x320(800) / ctp_screen_max_y=0x1e0(480)
 *     （GT911 官方量程）→ 800×480。曾按 1024×600 归一，导致右侧 224px/底部 120px 触摸不可达。
 *   t113-mq_r：TPADC 电阻屏，沿用历史默认 1024×600（该板未取得 DTS 量程，待实机确认）。
 * 实机若仍有边缘偏差，优先用 /etc/hmi/touch_cal.conf 的 sx/sy 线性补偿（无需重编）。 */
#ifndef HMI_TOUCH_MAX_X
#if HMI_BOARD_WAVEPI
#define HMI_TOUCH_MAX_X     800
#else
#define HMI_TOUCH_MAX_X     1024
#endif
#endif
#ifndef HMI_TOUCH_MAX_Y
#if HMI_BOARD_WAVEPI
#define HMI_TOUCH_MAX_Y     480
#else
#define HMI_TOUCH_MAX_Y     600
#endif
#endif

/* ── 串口（按目标板分配） ── */
#if HMI_BOARD_WAVEPI
#define HMI_DEV_UART_DEBUG  "/dev/ttyS3"    /* UART3 debug 串口（PB6/PB7→CH340E） */
#define HMI_DEV_UART_RS485  "/dev/ttyS4"    /* UART4 RS485 半双工（PB2/PB3→SP485） */
#define HMI_DEV_UART_EXT0   "/dev/ttyS0"    /* UART0 PE2/PE3（mangopi 复用） */
#define HMI_DEV_UART_EXT1   "/dev/ttyS1"    /* UART1 PG6~PG9 */
#define HMI_DEV_UART_EXT2   "/dev/ttyS2"    /* UART2 PC0/PC1 */
#else
#define HMI_DEV_UART_DEBUG  "/dev/ttyS1"    /* UART1 debug 串口 */
#define HMI_DEV_UART_RS485  "/dev/ttyS3"    /* UART3 RS485 半双工 */
#define HMI_DEV_UART_EXT2   "/dev/ttyS2"    /* UART2 PE2/PE3 */
#define HMI_DEV_UART_EXT4   "/dev/ttyS4"    /* UART4 PE4/PE5 */
#define HMI_DEV_UART_EXT5   "/dev/ttyS5"    /* UART5 PE6/PE7 */
#endif

/* ── 串口设置页的可选串口（stty 目标 + 下拉菜单文案 + 默认项）──
 * 两处必须同序；RS485 口在菜单里带 "(RS485)" 标注。 */
#if HMI_BOARD_WAVEPI
#define HMI_SERIAL_DEVS_STTY  { "/dev/ttyS0", "/dev/ttyS1", "/dev/ttyS2", "/dev/ttyS4" }
#define HMI_SERIAL_DEVS_MENU  "/dev/ttyS0\n/dev/ttyS1\n/dev/ttyS2\n/dev/ttyS4 (RS485)"
#define HMI_SERIAL_DEVS_DEFAULT 3
#else
#define HMI_SERIAL_DEVS_STTY  { "/dev/ttyS2", "/dev/ttyS3", "/dev/ttyS4", "/dev/ttyS5" }
#define HMI_SERIAL_DEVS_MENU  "/dev/ttyS2\n/dev/ttyS3 (RS485)\n/dev/ttyS4\n/dev/ttyS5"
#define HMI_SERIAL_DEVS_DEFAULT 1
#endif

/* ── RS485 方向控制 ──
 * HMI_RS485_GPIO_PIN：
 *   0    = 硬件 RS485（mq_r：dts linux,rs485-enabled-at-boot-time + /dev/mem 写 MCR）
 *   >0   = GPIO 方向控制（WavePi：SP485 DE/RE 由 GPIO 控制，发送前拉高/发送后拉低）
 * sysfs 编号：G 组基址 6*32=192 → PG8=200 */
#ifndef HMI_RS485_GPIO_PIN
#if HMI_BOARD_WAVEPI
#define HMI_RS485_GPIO_PIN  200             /* PG8 */
#else
#define HMI_RS485_GPIO_PIN  0
#endif
#endif

#endif /* HMI_BOARD_PROFILE_H */
