# T113-S3 引脚复用完整解析与元器件选型

> 2026-08-20 生成。基于 T113-S3 Datasheet V1.6 + board.dts + T113-S3 User Manual V1.1 + 开源参考设计。

---

## 一、关键设计决策

### 1.1 RGB18 vs RGB24 模式

固件 board.dts 实际使用 `rgb18_pins_a`（PD0-PD21），不是 `rgb24_pins_a`（PD0-PD21 + PB2-PB7）。

| 模式 | 数据引脚 | PB2-PB7 | 颜色深度 | 当前固件 |
|------|----------|---------|----------|----------|
| RGB24 | PD0-PD21 + PB2-PB7 | 被 LCD 占用 | 24bit/16.7M色 | 否 |
| RGB18 | PD0-PD21 | **空闲** | 18bit+抖动/262K色 | **是** |

**结论**：原理图仍按 RGB24 接线（所有 24 根数据线接到 LCD FPC），但需标注 PB2-PB7 与 CAN/I2C/IR 的复用关系。固件用 RGB18 时 PB 空闲可分配给 CAN0/CAN1/UART3/TWI0/TWI1/TWI3。

### 1.2 固件 lcd_frm 差异

| 来源 | lcd_frm | 含义 |
|------|---------|------|
| board.dts (1024x600) | `<1>` | 使能 RGB666 抖动 |
| 录图指南 LCEDA-PRO-CAPTURE-GUIDE.md | `0` | 禁用抖动 |

**结论**：以固件为准 `lcd_frm=1`。更新录图指南。

### 1.3 PE9 冲突

DTS 中 PE9 被定义为 GPIO LED（default-state="off"），但原理图用 PE9 作为 LCD_RESET（低有效）。DTS 中 LED default="off" 会输出 LOW，导致 LCD 持续复位。

**结论**：固件需删除 `gpio_pe9` LED 定义，PE9 专用于 LCD_RESET。

---

## 二、T113-S3 全引脚分配表

### 2.1 PB 端口（CAN/I2C/IR/LCD 复用区）

| 引脚 | 当前固件功能 | LCD RGB24 复用 | 冲突说明 |
|------|-------------|---------------|----------|
| PB0 | (空闲/IR1) | - | 可用于扩展 GPIO / IR |
| PB1 | (空闲) | - | 可用于扩展 GPIO |
| PB2 | CAN0_TX | LCD_B0 | RGB24 时被 LCD 占用 |
| PB3 | CAN0_RX | LCD_B1 | RGB24 时被 LCD 占用 |
| PB4 | CAN1_TX / TWI1-SCK | LCD_G0 | RGB24 时被 LCD 占用 |
| PB5 | CAN1_RX / TWI1-SDA / PWM0 | LCD_G1 | RGB24 时被 LCD 占用 |
| PB6 | UART3-TX / TWI3-SCK | LCD_R0 | RGB24 时被 LCD 占用 |
| PB7 | UART3-RX / IR-RX / TWI3-SDA | LCD_R1 | RGB24 时被 LCD 占用 |
| PB8 | (空闲/DMIC) | - | 可用于扩展 GPIO |
| PB9 | (空闲) | - | 可用于扩展 GPIO |
| PB10 | USB1-VBUS-EN (GPIO) | - | USB Host 电源使能 |
| PB11 | (空闲/TWI0-SDA/DMIC) | - | 可用于扩展 |
| PB12 | (空闲/DMIC) | - | 可用于扩展 |

### 2.2 PC 端口（SPI NAND 启动区）

| 引脚 | 功能 | 网络 | 说明 |
|------|------|------|------|
| PC0 | (空闲/LEDC) | - | 可用于扩展 |
| PC1 | (空闲) | - | 可用于扩展 |
| PC2 | SPI0-CLK | NAND_CLK | SPI NAND 时钟 |
| PC3 | SPI0-CS | NAND_CS | SPI NAND 片选（上拉） |
| PC4 | SPI0-MOSI / BOOT-SEL0 | NAND_DI | SPI NAND 数据输入 |
| PC5 | SPI0-MISO / BOOT-SEL1 | NAND_DO | SPI NAND 数据输出 |
| PC6 | SPI0-WP | NAND_WP | 写保护（10k 上拉到 VCC3V3） |
| PC7 | SPI0-HOLD | NAND_HOLD | 保持（10k 上拉到 VCC3V3） |

### 2.3 PD 端口（LCD 数据/控制区，全占用）

| 引脚 | 功能 | 网络 | 面板 Pin |
|------|------|------|----------|
| PD0-PD5 | LCD0-D2..D7 | LCD_B2..LCD_B7 | Pin17..12 |
| PD6-PD11 | LCD0-D10..D15 | LCD_G2..LCD_G7 | Pin25..20 |
| PD12-PD17 | LCD0-D18..D23 | LCD_R2..LCD_R7 | Pin33..28 |
| PD18 | LCD0-CLK | LCD_DCLK | Pin37 |
| PD19 | LCD0-DE | LCD_DE | Pin9 |
| PD20 | LCD0-HSYNC | LCD_HSYNC | Pin11 |
| PD21 | LCD0-VSYNC | LCD_VSYNC | Pin10 |
| PD22 | PWM7 | LCD_BL_PWM | 背光调光 |

### 2.4 PE 端口（UART/CSI/Ethernet 复用区）

| 引脚 | 功能 | 网络/外设 | 说明 |
|------|------|----------|------|
| PE0 | GPIO-LED | STATUS_GREEN | 状态指示灯 |
| PE1 | GPIO-LED | STATUS_RED | 状态指示灯 |
| PE2 | UART2-TX | UART2_TXD | RS232 TX |
| PE3 | UART2-RX | UART2_RXD | RS232 RX |
| PE4 | UART4-TX | UART4_TXD | 扩展 UART |
| PE5 | UART4-RX | UART4_RXD | 扩展 UART |
| PE6 | UART5-TX | UART5_TXD | 扩展 UART |
| PE7 | UART5-RX | UART5_RXD | 扩展 UART |
| PE8 | GPIO-LED / MDC | - | LED 或以太网 MDC 备选 |
| PE9 | **LCD_RESET** | LCD_RESET | 面板复位（低有效，需删 DTS LED 定义） |
| PE10 | (空闲/EPHY-25M) | - | 可输出 25MHz 给 PHY（DTS 未启用） |
| PE11 | (空闲) | - | 可用于扩展 |
| PE12 | TWI2-SCK | TWI2_SCL | 电容触摸 I2C 时钟 |
| PE13 | TWI2-SDA | TWI2_SDA | 电容触摸 I2C 数据 |
| PE14-PE17 | (空闲/I2S) | - | 可用于扩展 |

### 2.5 PF 端口（SD 卡区）

| 引脚 | 功能 | 网络 | 说明 |
|------|------|------|------|
| PF0 | SDC0-D1 | SD_D1 | SD 卡数据 1 |
| PF1 | SDC0-D0 | SD_D0 | SD 卡数据 0 |
| PF2 | SDC0-CLK | SD_CLK | SD 卡时钟 |
| PF3 | SDC0-CMD | SD_CMD | SD 卡命令 |
| PF4 | SDC0-D3 | SD_D3 | SD 卡数据 3 |
| PF5 | SDC0-D2 | SD_D2 | SD 卡数据 2 |
| PF6 | GPIO (CD) | SD_DET | 卡检测（上拉） |

### 2.6 PG 端口（Ethernet/UART 复用区，全占用）

| 引脚 | 功能 | 网络 | 说明 |
|------|------|------|------|
| PG0 | RMII-CRS_DV | ETH_CRS_DV | 以太网载波/接收数据有效 |
| PG1 | RMII-RXD0 | ETH_RXD0 | 以太网接收数据 0 |
| PG2 | RMII-RXD1 | ETH_RXD1 | 以太网接收数据 1 |
| PG3 | RMII-TXCK | ETH_REFCLK | 以太网发送时钟（REF_CLK） |
| PG4 | RMII-TXD0 | ETH_TXD0 | 以太网发送数据 0 |
| PG5 | RMII-TXD1 | ETH_TXD1 | 以太网发送数据 1 |
| PG6 | UART1-TX | UART1_TXD | 调试串口 TX |
| PG7 | UART1-RX | UART1_RXD | 调试串口 RX |
| PG8 | UART3-TX | UART3_TXD | RS485 TX |
| PG9 | UART3-RX | UART3_RXD | RS485 RX |
| PG10 | PWM3 (CPU 电压) | PWM3_VDD_CPU | CPU 核心 DVFS PWM |
| PG11 | GPIO (PHY-RST) | ETH_RST | PHY 复位 |
| PG12 | RMII-TXEN | ETH_TXEN | 以太网发送使能 |
| PG13 | RMII-RXER | ETH_RXER | 以太网接收错误 |
| PG14 | MDC | ETH_MDC | PHY 管理时钟 |
| PG15 | MDIO | ETH_MDIO | PHY 管理数据 |

### 2.7 RTP 引脚（专用 ADC）

| 引脚 | 功能 | 网络 | RTP FPC |
|------|------|------|---------|
| TP-X1 | TPADC-X+ | TCH_XP | J4 Pin2 |
| TP-X2 | TPADC-X- | TCH_XN | J4 Pin4 |
| TP-Y1 | TPADC-Y+ | TCH_YP | J4 Pin3 |
| TP-Y2 | TPADC-Y- | TCH_YN | J4 Pin1 |

### 2.8 扩展排针可用引脚

RGB18 模式下（当前固件配置），以下引脚可用于扩展排针 J11：

| 引脚 | 可用功能 | 备注 |
|------|---------|------|
| PB0 | IR1-TX / GPIO | 扩展 IR 或 GPIO |
| PB1 | GPIO | 扩展 GPIO |
| PB2 | CAN0-TX / TWI0-SDA / UART4-TX | CAN0 或 I2C0 |
| PB3 | CAN0-RX / TWI0-SCK / UART4-RX | CAN0 或 I2C0 |
| PB4 | CAN1-TX / TWI1-SCK / UART5-TX | CAN1 或 I2C1 |
| PB5 | CAN1-RX / TWI1-SDA / UART5-RX | CAN1 或 I2C1 |
| PB8 | GPIO / DMIC | 扩展 GPIO |
| PB9 | GPIO | 扩展 GPIO |
| PE4 | UART4-TX | 扩展 UART |
| PE5 | UART4-RX | 扩展 UART |
| PE6 | UART5-TX | 扩展 UART |
| PE7 | UART5-RX | 扩展 UART |
| PE10 | EPHY-25M / GPIO | 扩展 GPIO |
| PE11 | GPIO | 扩展 GPIO |
| PE14 | I2S / GPIO | 扩展 GPIO |
| PE15 | I2S / GPIO | 扩展 GPIO |

建议 J11 (2x12) 引脚分配：
```
Pin 1:  VCC3V3    Pin 2:  GND
Pin 3:  PB2/CAN0_TX Pin 4: PB3/CAN0_RX
Pin 5:  PB4/CAN1_TX Pin 6: PB5/CAN1_RX
Pin 7:  PB0/GPIO   Pin 8:  PB1/GPIO
Pin 9:  PE4/UART4_TX Pin 10: PE5/UART4_RX
Pin 11: PE6/UART5_TX Pin 12: PE7/UART5_RX
Pin 13: PE10/GPIO  Pin 14: PE11/GPIO
Pin 15: PB8/GPIO   Pin 16: PB9/GPIO
Pin 17: PE14/GPIO  Pin 18: PE15/GPIO
Pin 19: GND        Pin 20: VCC3V3
Pin 21: NC         Pin 22: NC
Pin 23: NC         Pin 24: NC
```

---

## 三、元器件选型与 LCSC 编号

### 3.1 P1 电源输入与电源树

| 位号 | 元器件 | 型号 | LCSC | 封装 | 参数 | 说明 |
|------|--------|------|------|------|------|------|
| J1 | DC 端子 | KF129-5.08-2P | C475092 | TERM-2P-5.08 | 12-24V 2P 5.08mm | DC 输入端子（~¥0.17 有货） |
| F1 | 自恢复保险丝 | TRB300 | C262070 | SMD | 3A 30V | TLC，有货；输入过流保护 |
| Q1 | P-MOS 反接保护 | AOD4184A | C99124 | SO-8 | -40V P-MOS | 与原理图一致；AOS 有货 |
| D1 | TVS 管 | SMCJ24A | C19077604 | SMC | 24V 工作 400W | 浪涌保护（R+O，有货） |
| U1 | 5V 降压 | XL4015E1 | C51661 | TO-263-5 | 8-36V→5V/5A | 宽输入 5V Buck |
| U2 | 3.3V 降压 | SY8089A1AAC | C479074 | TSOT-23-5 | 5V→3.3V/2A | 同步整流 Buck |
| U3 | 1.8V LDO | RT9193-18GB-MS | C7528821 | SOT-23-5 | 3.3V→1.8V/300mA | LDO（MSKSEMI ¥0.16，有货） |
| U4 | 0.9V CPU 核心 | SY8089A1AAC | C479074 | TSOT-23-5 | 5V→0.9V/2A + PWM 调压 | DVFS Buck，PWM3 控制反馈 |
| U5 | 1.5V DRAM | SY8089A1AAC | C479074 | TSOT-23-5 | 5V→1.5V/2A | DDR3 供电 |
| TP1 | 测试点 | Keystone 5003 | C238124 | TP-1.0 | VIN24_F | 测试点（有货） |
| TP2 | 测试点 | Keystone 5003 | C238124 | TP-1.0 | VCC5V | 测试点（有货） |
| TP3 | 测试点 | Keystone 5003 | C238124 | TP-1.0 | VCC3V3 | 测试点（有货） |

电源树上电顺序：VIN24_F → VCC5V → VCC3V3 → VCC1V8 → VDD0V9 → VCC1V5

### 3.2 P2 SoC 核心

| 位号 | 元器件 | 型号 | LCSC | 封装 | 说明 |
|------|--------|------|------|------|------|
| U6 | T113-S3 | T113-S3 | C5197687 | eLQFP-128 14x14mm | Allwinner，有货；价格以下单页实时为准 |
| X1 | 24MHz 晶振 | 3225-24-12-10-10/4B | C7424728 | SMD-3225 | LIMING，24MHz/12pF，有货 |
| X2 | 32.768kHz | 3215 无源 | C32346 | SMD-3215 | RTC 时钟（EPSON Q13FC13500004 ¥1.26/49万有货） |
| SW1 | 复位按键 | TS-1187A-C-A-B | C480267 | TACT-6x6 | XKB，有货；低有效复位 |
| SW2 | FEL 按键 | TS-1187A-C-A-B | C480267 | TACT-6x6 | XKB，有货；USB0 恢复下载 |
| - | 去耦电容 | 100nF x N | C1525 | 0402/0603 | 每对电源引脚 1 颗 |
| - | 储能电容 | 10uF x N | C19702 | 0603 | 每个电源域 1 颗 |

### 3.3 P3 存储

| 位号 | 元器件 | 型号 | LCSC | 封装 | 说明 |
|------|--------|------|------|------|------|
| U7 | SPI NAND | W25N01GVZEIG | C907680 | WSON-8 6x5mm | 1Gbit SPI NAND（LCSC 型号页已核对） |
| J2 | TF 卡座 | TF-015-N 自弹式 | C41426932 | MICROSD-SMD | SDC0 4-bit（JXTCONN，有货） |
| R_WP | 10k 上拉 | 0603 10kΩ | C25803 | 0603 | NAND WP/HOLD 上拉 |
| R_HOLD | 10k 上拉 | 0603 10kΩ | C25803 | 0603 | NAND HOLD 上拉 |

### 3.4 P4 LCD RGB888

| 位号 | 元器件 | 型号 | LCSC | 封装 | 说明 |
|------|--------|------|------|------|------|
| J3 | 50P FPC 座 | BOOMELE 0.5-50P | C190666 | FPC-SMD-50P-0.5 | LCD 50PIN FPC 连接器（~¥0.82 有货） |
| R_MODE | 10k 上拉 | 0603 10kΩ | C25803 | 0603 | LCD_MODE 拉高选 DE 模式 |
| R_DITH | 10k 下拉 | 0603 10kΩ | C25803 | 0603 | LCD_DITH 拉低 |
| R_RESET_PU | 10k 上拉 | 0603 10kΩ | C25803 | 0603 | LCD_RESET 上拉 |
| C_RESET | 100nF | 0603 | C1525 | 0603 | LCD_RESET 去耦 |
| R_SHLR | 0Ω 选择 | 0603 0Ω | C166918 | 0603 | RALEC RTT0360R0FTP，0Ω，有货 |
| R_UPDN | 0Ω 选择 | 0603 0Ω | C166918 | 0603 | RALEC RTT0360R0FTP，0Ω，有货 |
| R401-R424 | 22Ω 串联 | 0603 22Ω | C2759746 | 0603 | UNI-ROYAL CQ03WAF2202T5E，22Ω；有货，选配 |

### 3.5 P5 LCD 电源与背光

| 位号 | 元器件 | 型号 | LCSC | 封装 | 说明 |
|------|--------|------|------|------|------|
| U12 | LCD 偏压 | TPS65100PWPR | C181650 | TSSOP-24 | AVDD 9V/VGH 18V/VGL -6V/VCOM 3.15V（LCSC 型号页已核对） |
| U13 | 背光升压 | MT9284-28J | C181780 | TSOT-23-6 | 恒流升压 5V→9.6V/180mA（西安航天民芯 ¥0.23/47513有货） |
| RCS1 | 电流采样 | 0.56Ω 1% | C2174717 | 0805 | VISHAY RCWE0805R560FKEA ¥0.20 有货（或 YAGEO RL0805FR-070R56L ~¥0.05）；按 VFB=104mV/180mA |
| L_BL | 升压电感 | ZENR2016T220M | C44447583 | SMD | 22uH 背光升压电感（有货，替换原3.3uH C285817） |
| D_BL | 肖特基 | SS34 | C8678 | SMA | MDD SS34，40V/3A，有货 |
| C_BL_IN | 10uF | CL10A106KP8NNNC | C19702 | 0805 | Samsung 10uF，有货 |
| C_BL_OUT | 22uF | CL21A226MAQNNNE | C45783 | 0805 | Samsung 22uF，有货；不再误用电感料号 |

背光拓扑确认：27 颗 LED，VLED=9.6V，总电流 180mA → 3 串 9 并（3S9P），每串 3 颗 3.2V=9.6V，每串 20mA。

### 3.6 P6 电阻触摸

| 位号 | 元器件 | 型号 | LCSC | 封装 | 说明 |
|------|--------|------|------|------|------|
| J4 | RTP FPC | 4P FPC | 待确认 | CONN-FPC-4P | 机械尺寸待屏厂确认 |
| R401-R404 | 0Ω 串联 | 0603 0Ω | C166918 | 0603 | RALEC RTT0360R0FTP，0Ω，有货 |
| D401 | ESD 保护 | ESDA6V8-1U2 | C100005 | SOT-23-6 | 4 通道 ESD（选配/DNP） |

### 3.7 P7 USB

| 位号 | 元器件 | 型号 | LCSC | 封装 | 说明 |
|------|--------|------|------|------|------|
| J7 | USB Type-C | TYPE-C-31-M-12 | C165948 | USB-C-SMD-16P | USB0 OTG；韩荣，约171070件库存 |
| R_CC1 | 5.1k 下拉 | 0603 5.1kΩ | C23186 | 0603 | CC1 下拉（UFP） |
| R_CC2 | 5.1k 下拉 | 0603 5.1kΩ | C23186 | 0603 | CC2 下拉（UFP） |
| D_ESD0 | USB ESD | USBLC6-2SC6 | C7519 | SOT-23-6 | ST 原装，有货 |
| J8 | USB-A 母座 | HYCW236-USBA04-289B | C3034183 | USB-A-SMD | HOAUC，有货；下单前确认机械尺寸 |
| U15 | 负载开关 | SY6280AAAC | C207620 | SOT-23-5 | USB1 Host 限流开关，1=OUT/2=GND/3=ISET/4=EN/5=IN |
| R_LIMIT | 限流设定 | 6.8kΩ 1% | 待确认 | 0603 | SY6280 ISET，Ilim=6800/Rset，约 1A |
| D_ESD1 | USB ESD | USBLC6-2SC6 | C7519 | SOT-23-6 | ST 原装，有货 |

### 3.8 P8 通信

| 位号 | 元器件 | 型号 | LCSC | 封装 | 说明 |
|------|--------|------|------|------|------|
| U9A | 数字隔离器 | ADUM1201ARZ-RL7 | C9669 | SOIC-8 | RS485 隔离数据（ADI，有货；国产 JSMSEMI C30587325 便宜） |
| U9B | RS485 收发器 | MAX3485ESA+T | C18148 | SOIC-8 | RS485 隔离侧收发器（MAXIM ~¥7.5；廉价替代 UMW C668204 ~¥0.4 有货） |
| U9_PWR | 隔离电源 | B0505S-1W | C512032 | DIP-4/SMD-4 | 5V→5V 隔离电源（RLT ~¥4 有货；HI-LINK C39142807 备选） |
| R_TERM | 120Ω 终端 | 0603WAF1200T5E | C22787 | 0603 | UNI-ROYAL，120Ω，有货；RS485 终端（选配） |
| D_PROT | RS485 保护 | SM712 | C3647085 | SOT-23 | MSKSEMI，有货；RS485 总线保护 |
| U10 | RS232 | MAX3232ESE+T | C15846 | SOIC-16 | RS232 电平转换（MAXIM 有货；廉价 lingxingic C41430944） |
| C1-C4 | 1uF 泵电容 | 0603 1uF | C15849 | 0603 | MAX3232 电荷泵电容 |
| DB9 | RS232 连接器 | DB9 母座 | - | DB9 | RS232 接口（板边） |
| U11 | 以太网 PHY | IP101GRI | C910373 | QFN-32 | RMII PHY（LCSC 型号页已核对） |
| X_ETH | 25MHz 晶振 | SL322525M20P | C5155479 | SMD-3225 | Slkor，25MHz/20pF，有货；按 PHY 参考设计确认负载电容 |
| R_PHY_AD | 10k 下拉 | 0603 10kΩ | C25803 | 0603 | PHY 地址设定 |
| J9 | RJ45 带磁性 | HR911105A | C12074 | RJ45-MAG | 网口带网络变压器（LCSC 在售） |

### 3.9 P9 IO 扩展

| 位号 | 元器件 | 型号 | LCSC | 封装 | 说明 |
|------|--------|------|------|------|------|
| J10 | 调试排针 | 1x4 2.54mm | C492403 | HDR-1x4-2.54 | UART1 TX/RX/GND/3V3（~¥0.088 有货） |
| J11 | 扩展排针 | 2x12 2.54mm | C2883562 | HDR-2x12-2.54 | CAN/GPIO/UART 扩展（XKB X5521WV-2x12 ~¥2.8 有货） |
| SW3 | 用户按键 | TS-1187A-C-A-B | C480267 | TACT-6x6 | XKB，有货；USER_KEY |
| LED1 | 红色 LED | 0603 红 | C84265 | 0603 | STATUS_RED (PE1) |
| LED2 | 绿色 LED | 0603 绿 | C72043 | 0603 | STATUS_GREEN (PE0) |
| R_LED1 | 1k 限流 | 0603 1kΩ | C21190 | 0603 | LED1 限流 |
| R_LED2 | 1k 限流 | 0603 1kΩ | C21190 | 0603 | LED2 限流 |
| R_SW1 | 10k 上拉 | 0603 10kΩ | C25803 | 0603 | RESET_N 上拉 |
| R_SW2 | 10k 上拉 | 0603 10kΩ | C25803 | 0603 | FEL_N 上拉 |
| R_SW3 | 10k 上拉 | 0603 10kΩ | C25803 | 0603 | USER_KEY 上拉 |

---

## 四、以太网 RMII 接线确认

根据 DTS `phy-mode = "rmii"` 和 `use_ephy25m = <0>`：

| T113 引脚 | RMII 功能 | 方向 | PHY (IP101GRI) 引脚 | 说明 |
|-----------|----------|------|---------------------|------|
| PG0 | CRS_DV | I/O | CRS_DV | 载波/数据有效 |
| PG1 | RXD0 | IN | RXD0 | 接收数据 0 |
| PG2 | RXD1 | IN | RXD1 | 接收数据 1 |
| PG3 | TXCK/REF_CLK | OUT/IN | REF_CLK | 50MHz 参考时钟 |
| PG4 | TXD0 | OUT | TXD0 | 发送数据 0 |
| PG5 | TXD1 | OUT | TXD1 | 发送数据 1 |
| PG12 | TXEN | OUT | TXEN | 发送使能 |
| PG13 | RXER | IN | RXER | 接收错误 |
| PG14 | MDC | OUT | MDC | 管理时钟 |
| PG15 | MDIO | I/O | MDIO | 管理数据 |
| PG11 | GPIO | OUT | RST | PHY 硬件复位 |

REF_CLK 方向：IP101GRI 可在 PHY 端用 25MHz 晶振产生 50MHz REF_CLK 输出给 MAC。DTS `use_ephy25m=0` 表示 SoC 不输出 25MHz，PHY 用外部 25MHz 晶振自产生 50MHz REF_CLK。

PHY 地址：IP101GRI 默认地址由 PHYAD0/PHYAD1 引脚决定（建议 0x01 或 0x00）。

---

## 五、SPI NAND 接线确认

根据 DTS `spi0_pins_a` 和 `spi0_pins_b`：

| T113 引脚 | SPI 功能 | NAND 网络 | W25N01GV 引脚 | 说明 |
|-----------|---------|-----------|-------------|------|
| PC2 | SPI0-CLK | NAND_CLK | CLK | 时钟 |
| PC3 | SPI0-CS | NAND_CS | CS# | 片选（上拉） |
| PC4 | SPI0-MOSI | NAND_DI | DI/IO0 | 数据输入 |
| PC5 | SPI0-MISO | NAND_DO | DO/IO1 | 数据输出 |
| PC6 | SPI0-WP | NAND_WP | WP#/IO2 | 写保护（上拉 VCC3V3） |
| PC7 | SPI0-HOLD | NAND_HOLD | HOLD#/IO3 | 保持（上拉 VCC3V3） |

启动配置：PC4=BOOT-SEL0，PC5=BOOT-SEL1。SPI NAND 启动时 BOOT-SEL[1:0]=00。正常工作时不影响（SPI 模式覆盖 Boot 引脚功能）。

DTS 配置：`spi-rx-bus-width=<4>` 和 `spi-tx-bus-width=<4>`，启用 4-bit Quad I/O 模式。WP 和 HOLD 在 Quad 模式下变为 IO2/IO3 双向数据线。

---

## 六、UART 分配确认

| UART | T113 引脚 | 用途 | DTS 状态 | 备注 |
|------|----------|------|---------|------|
| UART0 | - | - | 禁用 | 被 PF2/PF4 复用，SD 卡优先 |
| UART1 | PG6(TX)/PG7(RX) | 调试串口 | **okay** | 调试输出 |
| UART2 | PE2(TX)/PE3(RX) | RS232 | **okay** | RS232 通信 |
| UART3 | PG8(TX)/PG9(RX) | RS485 | **okay** | 硬件 RS485 模式 |
| UART4 | PE4(TX)/PE5(RX) | 扩展 | **okay** | 扩展排针 |
| UART5 | PE6(TX)/PE7(RX) | 扩展 | **okay** | 扩展排针 |

注意：UART3 在 DTS 中启用 `linux,rs485-enabled-at-boot-time`，驱动会自动控制 RTS 引脚用于 RS485 方向控制。但在当前 PG8/PG9 配置中只用了 TX/RX，没有 RTS。如果需要自动方向控制，需要将 RS485 DE/RE 控制接到额外 GPIO 或使用支持自动方向的收发器。

---

## 七、CAN 分配确认

| CAN | T113 引脚 | DTS 状态 | 条件 | 备注 |
|-----|----------|---------|------|------|
| CAN0 | PB2(TX)/PB3(RX) | **okay** | RGB18 模式（当前固件） | 与 LCD_B0/B1 复用 |
| CAN1 | PB4(TX)/PB5(RX) | **okay** | RGB18 模式（当前固件） | 与 LCD_G0/G1 复用 |

RGB24 模式下 CAN0/CAN1 不可用。CAN 收发器需外部 CAN PHY 芯片（如 TJA1050/SN65HVD230），当前 BOM 未包含 CAN 收发器。

---

## 八、LCD 时序参数（固件实际值）

来自 board.dts 1024x600 配置：

| 参数 | 值 | 说明 |
|------|-----|------|
| lcd_x | 1024 | 水平分辨率 |
| lcd_y | 600 | 垂直分辨率 |
| lcd_dclk_freq | 51 | 像素时钟 MHz |
| lcd_hbp | 160 | 水平后肩+同步宽度 |
| lcd_ht | 1344 | 水平总周期 |
| lcd_hspw | 20 | 水平同步脉冲宽度 |
| lcd_vbp | 23 | 垂直后肩+同步宽度 |
| lcd_vt | 635 | 垂直总周期 |
| lcd_vspw | 3 | 垂直同步脉冲宽度 |
| lcd_if | 0 | HV（SYNC+DE）并行 RGB |
| lcd_hv_if | 0 | 并行 RGB |
| lcd_hv_clk_phase | 0 | 0 度相位 |
| lcd_hv_sync_polarity | 0 | VS/HS 低有效 |
| lcd_frm | 1 | 使能 RGB666 抖动 |
| lcd_pwm_ch | 7 | PWM 通道 7 |
| lcd_pwm_freq | 50000 | 50kHz 背光 PWM |
| lcd_pwm_pol | 1 | 高有效 |
| lcd_backlight | 50 | 默认亮度 50/255 |

---

## 九、LCEDA-Pro 录图操作步骤

### 步骤 1：创建工程
1. 打开 LCEDA Pro
2. 文件 → 新建工程 → 命名 `HMI_T113_SZCLX070H_R1`
3. 在工程中创建 9 张原理图页（P1-P9），页名见 README

### 步骤 2：每页录图顺序
1. 先放连接器和主 IC
2. 放电源/去耦/保护器件
3. 按网络名连接，使用全局网络标签
4. 添加 ERC 电源标志（PWR_FLAG）
5. 标注 NC 引脚

### 步骤 3：ERC 检查
- 运行 LCEDA-Pro ERC
- 逐项检查 LCEDA-PRO-CAPTURE-GUIDE.md 第 7 节检查清单
- 修正所有错误后保存

### 步骤 4：封装绑定
- 按 footprints.csv 绑定每个器件封装
- 确认 T113-S3 eLQFP-128 封装暴露焊盘处理
- 确认 FPC 连接器触点朝向

### 步骤 5：导出
- 导出网表和 BOM
- 与 normalized-netlist.csv 比对无漏网
- 准备 PCB 约束

---

## 十、仍需实物确认的项目

1. **RTP FPC 机械封装**：4P FPC 连接器触点朝向、间距、装配尺寸
2. **LCD FPC 触点朝向**：50P FPC 上接/下接
3. **背光 LED 拓扑**：3S9P 推断需屏厂背光图确认
4. **RTP X/Y 方向**：最终需软件校准验证
5. **CAN 收发器**：如需 CAN 功能，增加 TJA1050 或 SN65HVD230 收发器芯片
6. **T113-S3 eLQFP-128 暴露焊盘**：底部散热焊盘是否需要接地
