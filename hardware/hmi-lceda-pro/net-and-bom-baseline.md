# LCEDA-Pro 录图基线：网络与器件

> 这是录图顺序和网络命名基线，不是最终 BOM。所有标记 `TBD` 的值必须在厂家参数确认后冻结。

## P1 电源

| 位号 | 建议器件 | 作用 | 关键网络 |
|---|---|---|---|
| J1 | 2P terminal | 12~24V 输入 | VIN24_RAW/GND |
| F1 | 2A~3A resettable fuse | 输入保护 | VIN24_RAW→VIN24_F |
| Q1 | P-MOS reverse protection | 反接保护 | VIN24_F |
| D1 | SMBJ24A/SMCJ24A TBD | 浪涌/TVS | VIN24_F→GND |
| U1 | MP1584EN 或 TPS5430 TBD | 12~24V→5V | VCC5V |
| U2 | 3.3V Buck | T113 IO/外设 | VCC3V3 |
| U3 | 1.8V LDO/Buck | RTC/PLL/模拟 | VCC1V8 |
| U4 | 0.9V Buck | T113 core | VDD0V9 |
| U5 | 1.5V Buck | DRAM | VCC1V5 |

必须用 `EN_3V3`、`EN_0V9`、`EN_1V5` 明确表达上电顺序；不能像旧图一样全部直接使能。目标顺序：`VCC3V3 → VDD0V9 → VCC1V5`。

## P2/P3 T113 与存储

- U6：T113-S3，所有电源脚逐一去耦；EP/散热焊盘接 GND。
- X1：24MHz，按官方参考设计确认无源/有源方案。
- X2：32.768kHz，若 RTC 方案需要。
- SW1：RESET；SW2：FEL；SW3：BOOT。
- U7：SPI NAND W25N01GV 或兼容型号，WP#/HOLD# 上拉到 VCC3V3。
- J2：TF 卡座，SDIO/SMHC0，数据线上预留 22~33Ω 串阻和卡检测。

## P4 LCD_RGB888

- J3：50P FPC，严格按 `lcd-50pin-pinout.csv`。
- 24 根数据线：`LCD_B0..B7`、`LCD_G0..G7`、`LCD_R0..R7`。
- 控制：`LCD_MODE` 上拉到 VCC3V3；`LCD_DCLK`、`LCD_DE`、`LCD_HSYNC`、`LCD_VSYNC`、`LCD_RESET`。
- `LCD_SHLR`、`LCD_UPDN`：各放 0Ω 选择位，默认值 TBD。
- `LCD_DITH`：10k 下拉，是否启用以面板备注为准。
- 所有 LCD 高速线靠近 FPC 放置串阻预留位（默认 0Ω/DNP，值 TBD）。
- `LCD_DVDD`、`LCD_AVDD`、`LCD_VGH`、`LCD_VGL`、`LCD_VCOM` 必须分网，不可用通用 `3V3`/`5V` 标签替代。

## P5 背光和面板偏压

- U12：背光升压控制器，型号 TBD；输入 VCC5V，输出 `VLED_A/VLED_K`。
- 27 LED 的串并联、电流和最大电压以厂家电气规格确认；设置 `RCS_7IN` 可调/二选一焊盘。
- PWM：`LCD_BL_PWM`；使能：`LCD_BL_EN`；故障/过压测试点：`LCD_BL_FB`、`LCD_BL_OV`。
- U13：面板偏压 IC，型号 TBD；输出 `LCD_AVDD`、`LCD_VGH`、`LCD_VGL`、`LCD_VCOM`。
- U13 的电感、二极管、分压和电容必须使用对应芯片数据手册参考设计，不能只按功能框图画。

## P6 RTP_TOUCH

- J4：独立 4P 触摸 FPC；图片已确认针序：1=`Y1/Y-`、2=`X1/X+`、3=`Y2/Y+`、4=`X2/X-`。
- 信号：`TCH_XP`、`TCH_XN`、`TCH_YP`、`TCH_YN`。
- 连接关系：J4.1→TCH_YN/TP-Y2，J4.2→TCH_XP/TP-X1，J4.3→TCH_YP/TP-Y1，J4.4→TCH_XN/TP-X2；最终触摸坐标方向仍需软件校准确认。
- 每路预留 0Ω 串联位；ESD 阵列 DNP/选件；预留 TPADC 测试点。
- 不把 RTP 线接入 J3 的 50PIN。

## P7 USB

- J7：USB0 Type-C，CC1/CC2 各 5.1k 下拉（设备/UFP 默认）。
- U14：USB ESD，USB0 DP/DM 到 T113。
- J8：USB1 Host-A；U15：VBUS 限流开关，默认关闭/使能逻辑 TBD。
- 若要 HUB，另放 U16 USB HUB；第一版建议先保留 Host，不在没有需求时增加 HUB。

## P8 COMM

- U9：RS485 收发器，优先隔离版本：数字侧 `UART3_TXD/RXD`，隔离侧 `ISO_TX/ISO_RX`，电源 `ISO_5V/ISO_GND`。
- RS485 A/B：SM712/同类 TVS，120Ω 终端可焊，接线端子 J5。
- U10：RS232 收发器，UART2 或 UART0 具体复用以 T113 手册和固件为准，DB9 J6。
- U11：RMII PHY，IP101GRI/RTL8201F 二选一；确认 RMII 时钟方向后再画晶振/REFCLK。
- J9：RJ45 带网络变压器；差分对放置共模/ESD 选件位。

## P9 IO/扩展

- J10：调试串口 4P，`UART1_TXD/RXD`、VCC3V3、GND。
- LED：`STATUS_RED`、`STATUS_GREEN`，各 1k 串联。
- 按键：RESET/FEL/USER。
- J11：2x12 扩展，电源和未占用 GPIO 按 T113 手册复核后输出。
- TP：VIN24、VCC5V、VCC3V3、VCC1V8、VDD0V9、VCC1V5、LCD_AVDD、LCD_VGH、LCD_VGL、VLED_A、VLED_K、ISO_GND。
