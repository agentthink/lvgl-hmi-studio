# T113 板级 dts 配置说明

依据《Linux-T113开发文档-硬件接口》，固件涉及的硬件接口默认分配与切换方法如下。
dts 位于 SDK 内 `device/config/chips/t113/configs/mq_r/`（`board.dts`、`uboot-board.dts`）。

## 1. 串口分配（默认，勿改）

| 串口 | 节点 | 管脚 | 用途 |
| --- | --- | --- | --- |
| UART1 | /dev/ttyS1 | — | debug 串口（115200，烧录/调试） |
| UART2 | /dev/ttyS2 | PE2/PE3 | 扩展 GPIO 串口 |
| UART3 | /dev/ttyS3 | PG8/PG9 | **RS485**（本固件 Modbus RTU 默认） |
| UART4 | /dev/ttyS4 | PE4/PE5 | 扩展 GPIO 串口 |
| UART5 | /dev/ttyS5 | PE6/PE7 | 扩展 GPIO 串口 |

### 1.1 UART3 RS485 模式启用（**必须**，否则 F4 Modbus RTU 失效）

UART3 物理管脚 PG8/PG9 默认即按 uart3 复用（`uart3_pins_a`，dts 已配 `status = "okay"`），
但 sunxi-uart 驱动的 RS485 硬件模式默认未启用，需在 dts 中显式声明：

```dts
&uart3 {
        pinctrl-names = "default", "sleep";
        pinctrl-0 = <&uart3_pins_a>;
        pinctrl-1 = <&uart3_pins_b>;
        status = "okay";
        linux,rs485-enabled-at-boot-time;     /* ← 关键：启用硬件 RS485 模式 */
};
```

**为什么需要这个属性**（实测发现）：

1. Linux 5.4 tty 层 `tty_port_ioctl()` 对 `TIOCSRS485` 的标准处理要求
   `uart_port.rs485_config` 回调；Allwinner `sunxi-uart` 驱动没设置该回调
   （它用自己的 `sw_uart_ioctl` 路径处理 TIOCSRS485），导致应用层
   `ioctl(fd, TIOCSRS485, ...)` 在到达 sunxi-uart 驱动前就被 tty 层以
   `ENOTTY` 拒绝（见 [sunxi-uart.c:1374](file:///home/chen/Tina-Linux/Tina-Linux/lichee/linux-5.4/drivers/tty/serial/sunxi-uart.c#L1374)）。
2. sunxi-uart 驱动支持 dts 属性 `linux,rs485-enabled-at-boot-time`
   （[sunxi-uart.c:2068](file:///home/chen/Tina-Linux/Tina-Linux/lichee/linux-5.4/drivers/tty/serial/sunxi-uart.c#L2068)）：
   probe 时设置 `rs485conf.flags |= SER_RS485_ENABLED`；应用 `open /dev/ttyS3`
   触发 `sw_uart_startup()` → `sw_uart_config_rs485()` 写硬件寄存器
   `SUNXI_UART_MCR_MODE_RS485` + `SUNXI_UART_RS485_RXBFA`（9th-bit NMM 模式，
   无需独立 RTS 引脚）。

应用层 [modbus_rtu.c:125](../../app/comm/modbus_rtu.c#L125) 的 TIOCSRS485 调用保留作为
跨平台兼容降级路径（其它平台若支持 TIOCSRS485 则生效）；在 T113 上失败时静默降级，
RS485 模式由 dts 在启动时启用。

补丁文件见 `patches/0001-t113-mq_r-uart3-enable-rs485-at-boot-time.patch`，
应用方式：

```sh
cd <TINA_ROOT>
patch -p1 < <zhishu-hmi>/firmware/t113/tina/dts/patches/0001-t113-mq_r-uart3-enable-rs485-at-boot-time.patch
# 重编内核 + dtb（dts 编译到内核镜像中，pack 后烧录生效）
make -j$(nproc) && pack
```



## 2. GPIO：PE0/PE1/PE8/PE9

- 系统默认经 **ledc** 初始化为输出（本固件 `gpio_sunxi.c` 直接操作电平）。
- 需要**输入捕获**：删除 dts 中 ledc 对对应管脚的占用配置 → 重编译镜像 →
  在应用层自行完成初始化（本固件 `gpio_read` 已实现读取）。

## 3. I2C 复用串口管脚（如启用 /dev/i2c-3）

以 i2c-3 使用 PE6/PE7 为例（原为 UART5）：

```
device/config/chips/t113/configs/mq_r/board.dts
  - 删除/屏蔽 uart5 节点对 PE6/PE7 的 pinctrl 占用
  + 使能 i2c3 节点（twi3），pinctrl 选用 PE 组 I2C 复用
```

改后重编译、烧录，出现 `/dev/i2c-3`，应用直接读写（本固件 `i2c_sunxi.c`）。

## 4. PWM：PWM2 / PWM3 / PWM7

- 在 `board.dts` 使能 pwm 节点并确认对应 `pwmchipN` 编号
  （默认 `pwmchip0`，本固件 `runtime_config.h` 可调）。
- **新 HMI 板（SZCLX070H 7″ 1024×600）**：LCD 背光 PWM 为 **PWM7（PD22）**，
  `lcd0` 节点 `lcd_pwm_ch = <7>`，硬件 BL_PWM 接 PD22（与旧板 PWM2/PD18 不同，勿混用）。
- 旧板参考：PWM2 接 LCD 背光，用 `pwm_set_duty_percent` 调亮度。

## 5. 触摸节点选择

- 电容触摸：/dev/input/event2（默认，`runtime_config.h` 与 `config/lv_drv_conf.h` 一致）
- 电阻触摸：/dev/input/event0，需同时修改：
  1. `runtime_config.h` 的 `HMI_TOUCH_DEV`
  2. `config/lv_drv_conf.h` 的 `EVDEV_NAME`
  3. 触摸**控制器上报量程** `HMI_TOUCH_MAX_X/Y` —— 现已统一在
     `app/runtime/runtime_config.h` 按 `HMI_BOARD_WAVEPI` 给出（不再在
     `touch_evdev.c` 里兜底），可用 `-DHMI_TOUCH_MAX_X/Y=...` 覆盖。
     WavePI-S6 按板级 DTS 的 `ctp_screen_max_x=0x320(800)` /
     `ctp_screen_max_y=0x1e0(480)` 取 800×480；若换屏，务必同步 dts 与这两个宏。
     实机若仍有边缘偏差，优先用 `/etc/hmi/touch_cal.conf` 的 `sx/sy` 线性补偿
     （无需重编固件）。
- **注意**：24bit RGB888 占用 PB2-PB7，与 CTP 节点（gt9xxnew_ts）的 PB6/PB7 冲突，
  使用电阻触摸（`&rtp`）时需禁用 `&ctp` 节点（详见 `LCD_RGB24_1024x600.md`）。

## 6. 5 寸屏切换

修改两个文件（《硬件接口》原文指引）：
- `device/config/chips/t113/configs/mq_r/uboot-board.dts`
- `device/config/chips/t113/configs/mq_r/board.dts`

## 7. 分区调整（镜像/软件包变大时）

新增软件包导致打包失败时，调整分区：
- 文件：`device/config/chips/t113/configs/mq_r/sys_partition.fex`
- 增大 rootfs/文件系统分区大小后重新 `make && pack`

## 8. 网络（eth0 / wlan0）

- 有线默认 IP：192.168.100.100（改 `/etc/config/network`，见《开箱上手》）
- WIFI（STA）：修改 `/etc/wpa_supplicant.conf` 的 ssid/psk，执行
  `wpa_supplicant -i wlan0 -Dnl80211 -c /etc/wpa_supplicant.conf -B && udhcpc -i wlan0 &`
- 本固件 Modbus TCP / MQTT 目标地址在 `runtime_config.h` 配置。

## 9. 1024×600 RGB888 屏配置（SZCLX070H-500-IPS-G-RTP）

新 HMI 板选用的 7″ 1024×600 IPS 屏为 **RGB888（24bit）**，与 mq_r 默认 rgb18 配置不同。
完整修改方案（linux-5.4/board.dts + uboot-board.dts 的 lcd0 节点、rgb24 引脚映射、
`lcd_rb_swap`/`lcd_frm`、CTP 禁用、编译与调试步骤）见 **`LCD_RGB24_1024x600.md`**。
要点：`LCD_RGB_1024X600` 宏已开启；只需将 lcd0 的 `rgb18_pins_a/b` 改为 `rgb24_pins_a/b`、
`lcd_frm=0`、按接线设置 `lcd_rb_swap`，并禁用 `&ctp`（PB6/PB7 被 rgb24 占用）。
