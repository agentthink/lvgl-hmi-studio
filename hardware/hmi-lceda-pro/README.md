# HMI 主板 LCEDA-Pro 重设计（T113-S3 + SZCLX070H-500-IPS-G-RTP）

本目录是面向嘉立创 EDA 专业版（LCEDA-Pro）重新设计的 HMI 原理图基线，**不覆盖**旧版 `hardware/hmi-control-board-t113/`。

## 设计输入

- 主控：Allwinner T113-S3，eLQFP128，沿用现有 T113 固件资源。
- 输入：12~24V DC。
- 屏幕：`SZCLX070H-500-IPS-G-RTP`，7.0 inch、1024×600、RGB888、50PIN FPC。
- 触摸：独立四线电阻触摸 FPC，X+/X-/Y+/Y- 接 T113 TPADC；不占 LCD 50PIN。
- 工具：LCEDA-Pro。建议按本目录的分页和网络名手工放图，确认器件型号/封装后再导出网表和 PCB。

## 页面划分

| 页 | 名称 | 主要内容 |
|---|---|---|
| P1 | POWER_INPUT | 12~24V 输入、保险丝、反接保护、TVS、5V Buck、3V3/1V8/0V9/1V5 电源树 |
| P2 | SOC_CORE | T113-S3、电源引脚去耦、24MHz、32.768kHz、复位、FEL、BOOT、调试测试点 |
| P3 | STORAGE | SPI NAND、TF 卡、启动选择、卡检测、ESD/串阻 |
| P4 | LCD_RGB888 | 50PIN FPC、24bit RGB、MODE/DE/HSYNC/VSYNC/DCLK/RESET、扫描方向、DITH |
| P5 | LCD_POWER_BACKLIGHT | AVDD/VGH/VGL/VCOM 偏压、背光升压、PWM/EN、电流采样和测试点 |
| P6 | RTP_TOUCH | 独立四线电阻触摸 FPC、TPADC、串联保护电阻、ESD、校准测试点 |
| P7 | USB | USB0 Type-C OTG、USB1 Host、VBUS 负载开关、ESD、Type-C CC 电阻 |
| P8 | COMM | 隔离 RS485、RS232、以太网 RMII PHY、RJ45 磁性器件和 ESD |
| P9 | IO_EXPANSION | 状态 LED、复位/FEL/用户键、调试串口、扩展排针、电源测试点 |

## 已锁定的 LCD 50PIN

完整表格见 `lcd-50pin-pinout.csv`。屏幕资料明确给出：

- Pin 1/2=`VLED+`，Pin 3/4=`VLED-`。
- Pin 5/36/38/48=`GND`。
- Pin 6/46=`VCOM`，Pin 7=`DVDD`。
- Pin 8=`MODE`，高电平选择 DE 模式。
- Pin 9/10/11=`DE/VSYNC/HSYNC`。
- Pin 12~19=`B7..B0`，Pin 20~27=`G7..G0`，Pin 28~35=`R7..R0`。
- Pin 37=`DCLK`，下降沿锁存。
- Pin 39=`SHLR`，Pin 40=`UPDN`，Pin 41=`VGH`，Pin 42=`VGL`，Pin 43=`AVDD`，Pin 44=`RESET`。
- Pin 45/49/50=`NC`，Pin 47=`DITH`。

## 已确认与仍待确认的参数

根据 2024-03-21 版屏幕规格补充资料，以下参数已经确认：

- `DVDD=3.3V`（范围 3.0~3.6V）；
- `AVDD=9.0V`（范围 8.9~9.1V）；
- `VGH=18.0V`（范围 17.0~19.0V）；
- `VGL=-6.0V`（范围 -6.5~-5.5V）；
- `VCOM=3.15V`（范围 3.0~3.3V）；
- `VLED≈9.6V`（范围 9.0~10.8V）；
- 背光典型电流 `180mA`，单颗 LED `20mA`，27 颗白光 LED，建议恒流驱动。

仍需确认的项目：

1. 独立 RTP FPC 的机械尺寸和触点朝向；
2. RGB 时序的最终像素时钟/前后肩/极性（固件当前基线为 51MHz、1024×600、DE 模式）；
3. 背光 27 颗 LED 的串并联拓扑及升压控制器参考电路。

因此 P4/P5/P6 仍需保留参数注释、DNP/可调电阻位和测试点。LCD 偏压可以按上述额定值设计；RTP 电气针序已确认，但连接器封装、触点朝向和背光拓扑在厂家图纸确认前不能冻结。

## LCEDA-Pro 录图规则

1. 先新建 9 页层次原理图，页名和本 README 一致。
2. 所有跨页信号使用统一网络标签，不使用同名但不同含义的短标签。
3. LCD 数据统一使用 `LCD_B0..LCD_B7`、`LCD_G0..LCD_G7`、`LCD_R0..LCD_R7`。
4. 电源统一使用 `VIN24_F`、`VCC5V`、`VCC3V3`、`VCC1V8`、`VDD0V9`、`VCC1V5`、`LCD_AVDD`、`LCD_VGH`、`LCD_VGL`、`LCD_VCOM`、`VLED_A`、`VLED_K`。
5. 电阻触摸统一使用 `TCH_XP`、`TCH_XN`、`TCH_YP`、`TCH_YN`，不要把触摸线标在 LCD 50PIN 上。
6. 先在 LCEDA-Pro 内完成 ERC，再匹配真实封装；当前目录中的 CSV/网表若后续生成，只作为辅助，不能替代 ERC 和厂家规格确认。

## 设计状态

- [x] 新设计目录与分页基线
- [x] 屏幕 50PIN 接口表
- [x] T113-S3 引脚复用完整解析（基于 Datasheet V1.6 + board.dts）
- [x] 元器件选型与 LCSC 编号匹配
- [x] 背光 3S9P 拓扑确认（RCS1=0.56R，MT9284 VFB=104mV/180mA）
- [x] RMII PHY 接线确认（IP101GRI + 外部 25MHz 晶振）
- [x] SPI NAND 接线确认（PC2-PC7 Quad I/O）
- [ ] 屏幕电源/背光参数实物确认
- [x] RTP 独立 FPC 电气针序确认
- [ ] RTP 连接器机械封装和触点朝向确认
- [ ] LCEDA-Pro 原理图录入
- [ ] ERC 清零并导出网表
- [ ] 封装绑定与 PCB 约束
