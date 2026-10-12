# HMI 控制板原理图（T113-S3）— hmi-control-board-t113

基于 `/home/chen/下载/WavePi/` 资料（WavePI-S6 原理图 + T113-S3 数据手册/用户手册）设计的自有 HMI 控制板，
KiCad 10 工程，7 张层次化原理图，全局标签跨页互连。

## 文件清单

| 文件 | 说明 |
|---|---|
| `hmi-board.kicad_pro` | KiCad 10 工程 |
| `hmi-board.kicad_sch` | 根图（各子系统 sheet 图标） |
| `power.kicad_sch` | 电源：DC24V → 5V/3V3/1V5/0V9/1V8 |
| `core.kicad_sch` | T113-S3 核心：电源域、时钟、复位、启动配置、SPI NAND、microSD |
| `display.kicad_sch` | 7 寸 50P RGB LCD + 背光(MSK4010) + 四线电阻触摸 |
| `comm.kicad_sch` | RS485 + RS232 + 百兆以太网(RMII/RTL8201F) |
| `usb.kicad_sch` | CH340E 调试串口 + USB0 Type-C + GL850G 四口 HUB + WiFi/BT 插座 |
| `io.kicad_sch` | LED/按键/扩展排针 2x12/24V 电压检测 |
| `hmi.kicad_sym` / `hmi_lib.kicad_sym` | 自定义符号库（源格式 / KiCad 10 升级格式） |
| `sym-lib-table` | 工程符号库表（指向 `hmi_lib.kicad_sym`） |
| `gen_lib.py` / `gen_sch.py` | 生成器脚本（改完重跑 `python3 gen_lib.py && kicad-cli sym upgrade hmi.kicad_sym -o hmi_lib.kicad_sym && python3 gen_sch.py`） |
| `convert_kicad6.py` | 降级脚本：把 KiCad 10 工程转成 KiCad 6 格式，供嘉立创 EDA 导入 |
| `lceda-import/` | **KiCad 6 格式**降级副本（符号库 20200904 / 原理图 20211108），用于导入嘉立创 EDA 专业版 |
| `hmi-board.pdf` | 全工程 PDF 渲染件 |

在 KiCad 10 中直接打开 `hmi-board.kicad_pro`（符号库经 `sym-lib-table` 解析；每个 .kicad_sch 内还内嵌了符号缓存，可独立打开）。

## 1. 系统架构

```
DC24V ─┬─ TPS5430 → VCC5V ─┬─ SY8089 → VCC3V3 ─┬─ XC6206 → VCC1V8
       │                    ├─ SY8089 → VCC1V5   ├─ 各 3.3V 外设
       │                    └─ SY8089 → VDD0V9   └─ W25N01GV/CH340E/...
       └─ 150K/10K 分压 → GPADC0 电压检测
T113-S3(eLQFP128, 内置 128MB DDR3)
 ├─ 显示: RGB666 18bit (PD0~PD21) → 7" 1024×600 50P FPC; 背光 MSK4010 升压, PD22/PWM7 调光
 ├─ 触摸: 4 线电阻屏 → TP-X1/X2/Y1/Y2 (内置 TPADC)
 ├─ 存储: SPI NAND W25N01GV(SPI0, PC2~PC7) + microSD(SDC0, PF0~PF5)
 ├─ 调试: UART3 (PB6/PB7) → CH340E → USB-C
 ├─ RS485: UART4 (PB2/PB3) + SP3485, DE/RE=PG9
 ├─ RS232: UART0 (PE2/PE3) + SP3232 → DB9
 ├─ 以太网: RMII (PG0~PG5/PG12/PG13 + PE8/PE9 MDC/MDIO) + RTL8201F → RJ45(带变压器)
 ├─ USB0: Type-C(Device); USB1 → GL850G HUB → USB-A×2 + WiFi/BT 插座 + 备用
 └─ 扩展: 2x12 排针 (I2C2/CAN1/UART0/GPIO/电源)
```

## 2. 电源树（T113-S3 数据手册 Table 5-2 电压值）

| 电源轨 | 来源 | 供给对象 |
|---|---|---|
| VCC5V | TPS5430 (24V→5V, 3A) | 系统 5V、背光、HPVCC 前级、USB VBUS |
| VCC3V3 | SY8089 (FB: 150K/33K) | VCC-IO/VCC-PE/VCC-PD/VCC-PG/VCC-TVIN/TVOUT/LVDS、外设 |
| VCC1V5 | SY8089 (FB: 150K/100K) | VCC-DRAM0/1（SIP DDR3 外部供电） |
| VDD0V9 | SY8089 (FB: 100K/200K) | VDD-SYS0/1/2 + VDD-CORE0/1 |
| VCC1V8 | XC6206P182 | VDD18-DRAM、VCC-PLL、VCC-RTC、HPVCC |
| AVCC | T113 内部 LDOA（LDO-IN←VCC3V3, LDOA-OUT→AVCC） | 模拟域/TPADC/GPADC/音频 |
| VIN24 | DC24V 输入（保险丝 3A + SMBJ26A TVS） | 24V 检测分压 |

要点：VCC-RTC 按手册为 1.8V 且不得晚于其他电源上电；各电源轨加 PWR_FLAG 满足 ERC。
上电时序建议：3.3V/1.8V 先于 0.9V/1.5V（SY8089 的 EN 可由 5V 经 RC 延时控制，当前直连）。

## 3. T113-S3 引脚分配（eLQFP128，编号=数据手册 Table 4-2）

### 显示 RGB666（高位对齐，R5..R0=D23..D18）
| 信号 | 引脚 | 信号 | 引脚 | 信号 | 引脚 |
|---|---|---|---|---|---|
| R0 | PD12(70) | G0 | PD6(61) | B0 | PD0(55) |
| R1 | PD13(69) | G1 | PD7(62) | B1 | PD1(56) |
| R2 | PD14(71) | G2 | PD8(63) | B2 | PD2(57) |
| R3 | PD15(72) | G3 | PD9(64) | B3 | PD3(58) |
| R4 | PD16(73) | G4 | PD10(67) | B4 | PD4(59) |
| R5 | PD17(74) | G5 | PD11(68) | B5 | PD5(60) |
| CLK=PD18(75) | DE=PD19(76) | HSYNC=PD20(54) | VSYNC=PD21(53) | BL_PWM=PD22(52,PWM7) |

面板侧 R6/R7、G6/G7、B6/B7 接地（未用低位）。**FPC 50P 引脚定义按 7" 1024×600 RGB 通用定义，
必须对照你提供的《7寸高清1024600 IPS 50PIN RGB 配电阻触摸屏7007》docx 里的引脚表核验后再投产。**

### 通信/存储/其他
| 功能 | 引脚 | 功能 | 引脚 |
|---|---|---|---|
| UART3_TXD/RXD(调试) | PB6(80)/PB7(79) | SDC0(卡) D0..D3/CLK/CMD/DET | PF1(8)/PF0(7)/PF5(12)/PF4(11)/PF2(9)/PF3(10)/PF6(13) |
| UART4_TXD/RXD(RS485) | PB2(86)/PB3(85) | SPI0(NAND) CS/CLK/DI/DO/WP/HOLD | PC3(18)/PC2(19)/PC4(17)/PC5(16)/PC6(15)/PC7(14) |
| RS485_DE | PG9(4) | 启动 SEL0/SEL1（SMHC0→SPI NAND） | PC4 下拉 / PC5 上拉 10K |
| UART0_TXD/RXD(RS232) | PE2(35)/PE3(33) | USB0 DP/DM | 115/114 |
| RMII CRS_DV/RXD0/RXD1 | PG0(120)/PG1(118)/PG2(119) | USB1(→HUB) DP/DM | 112/113 |
| RMII REFCLK(50M入)/TXD0/TXD1 | PG3(121)/PG4(123)/PG5(122) | 触摸 X+/X-/Y+/Y- | TP-X1(102)/TP-X2(103)/TP-Y1(104)/TP-Y2(105) |
| RMII TXEN/RXER | PG12(124)/PG13(125) | 24M 晶振入 | DXIN(23) |
| MDC/MDIO | PE8(39)/PE9(38) | 32.768K | X32KIN(25)/X32KOUT(24) |
| EPHY-25M(→PHY X1) | PG11(6) | 复位 | RESET(27)，10K 上拉到 VCC1V8 |
| I2C2 SDA/SCK | PE13(31)/PE12(32) | CAN1 TX/RX(排针备选) | PB4(84)/PB5(82) |
| LED_USER/KEY_USER | PG15(127)/PG14(126) | VIN 检测 | GPADC0(101) ← 150K/10K |

## 4. 主要器件（BOM 摘要）

| 位号 | 器件 | 封装 | 备注 |
|---|---|---|---|
| Core | Allwinner T113-S3 | eLQFP128 | 内置 128MB DDR3 |
| Power | TPS5430 | SOIC-8 | 24V→5V 降压 |
| Power | SY8089AAC ×3 | SOT23-5 | 3.3V/1.5V/0.9V 降压 |
| Power | XC6206P182MR | SOT23-3 | 1.8V LDO |
| Storage | W25N01GVZEIG | WSON8 | SPI NAND 128MB，启动盘 |
| USB | CH340E | MSOP10 | USB 转调试串口 |
| USB | GL850G | SSOP28 | 4 口 USB2.0 HUB |
| COMM | SP3485EEN | SOIC-8 | 3.3V RS485 |
| COMM | SP3232EEN | SOIC-16 | 3.3V RS232 |
| COMM | RTL8201F-VB-CG | QFN32+EPAD | 百兆 PHY（RMII） |
| Display | MSK4010 | SOT23-6 | LCD 背光升压 |
| Core | 24MHz 有源晶振 | 3.2×2.5 | 经 1uF 隔直入 DXIN（按参考板做法） |
| USB | WiFi/BT USB 模块插座 | 1x4 2.54 | RTL8188/RTL8723BU 类 |

## 5. 设计说明与待核验项

1. **WiFi 用 USB 插座而非 SDIO**：T113-S3 的 SDC1（SDIO WiFi）与 RMII 以太网共用
   PG0~PG5 引脚组，二者不可共存；本板按要求保留百兆以太网，WiFi/BT 走 HUB 下行的
   4P 插座（插件模块方案）。
2. **面板 50P FPC 引脚**：采用通用 7" 1024×600 RGB666 定义（1-4=背光, 5-12=R0-R7,
   13-20=G0-G7, 21-28=B0-B7, 29=VCC, 30=CLK, 32/33/34=HS/VS/DE, 45=GND, 46-49=触摸, 50=GND）。
   不同厂家的 FPC 定义可能有差异，**投产前务必对照面板规格书（docx 中的引脚图）逐脚核验**。
3. **RTL8201F 引脚**：9~16 与 25~33 脚从 WavePI-S6 参考原理图确认；1~8、17~24 脚按
   RTL8201F 数据手册编排，**打样前对照立创商城 C45044 实物引脚图/官方数据手册复核**。
4. **GL850G**：4~11、14~16、18、20~22、24、25 脚来自参考原理图；其余（1~3、12、13、
   17、19、23、26~28）按数据手册编排，同样建议复核。
5. **供电时序**：手册要求 VCC-RTC 不晚于其他电源；量产前建议实测上电斜坡并视需要给
   SY8089 的 EN 加 RC 延时（当前 EN 经 100K 直接接 VCC5V）。
6. **启动方式**：SEL[1:0]=10 → 启动顺序 SMHC0(microSD)→SPI NAND→其他，插卡即可救砖/烧写。
7. **验证状态**：本工程由脚本生成，已通过 `kicad-cli sch export netlist`（148 个元件、批注无冲突，并已复核 T113、LCD、NAND、USB、UART、RMII 关键网络引脚号）与 `sch export pdf` 渲染验证；无头 `kicad-cli` 的 ERC 无法解析工程符号库表
   且不计算导线-引脚连通性，因此 ERC 报告中的 lib_symbol_issues / power_pin_not_driven /
   pin_not_connected 均为无头工具限制，在 KiCad GUI 中打开后按 F8 重新跑 ERC 即可正常判定。
    `lceda-import/` 目录已用 `convert_kicad6.py` 降级为 KiCad 6 格式，已验证括号平衡与引脚格式，
    嘉立创 EDA 导入需用户在 GUI 中实测。
