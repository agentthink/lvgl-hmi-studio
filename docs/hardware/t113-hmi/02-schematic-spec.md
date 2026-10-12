# T113-S3 HMI 主板 原理图设计规格

> 文档版本：V1.0　|　日期：2026-08-10
> 本文档给出**逐模块、逐网络**的原理图设计规格，可直接在 LCEDA 中照图录入。
> ⚠️ 引脚复用：表中引脚分组与复用以全志《T113-S3 用户手册》引脚复用表为最终依据；标注“以手册为准”的引脚请在录入前用官方手册核对。已标注的固件占用引脚（UART1/UART3/PWM2/PE0-PE1 等）与现有固件一致，不可更改。

---

## 1. 原理图设计规范（录入前必读）

- 图纸：A3 横向，共 8 页（分页见 §2）；全局网络标签命名统一（见 §2.3）。
- 电源符号：`12V~24V_IN`、`5V_SYS`、`3V3`、`1V8`、`0V9_CORE`、`1V5_DRAM`、`ISO_5V`、`VBUS5V`、`LED5V`。
- 地网络：`GND`（数字地）、`PGND`（Buck 功率地）、`AGND`（模拟，可选）、隔离侧 `ISO_GND`（RS485 隔离通道）。
- 位号规则：电源 U1-U5、SoC U6、存储 U7、PHY U8、串口 U9-U11、LED 驱动 U12、无源 R/C/L 按页分段编号（如 P1 电源页 R1xx/C1xx，P2 核心页 R2xx/C2xx…）。
- 每页放置：电源符号、地符号、网络标签、关键注释（标注“见 02-schematic-spec §x”）。

## 2. 原理图分页组织

| 页 | 名称 | 内容 |
| --- | --- | --- |
| P1 | 电源 | 5V 输入保护、3 路 Buck、1 路 LDO、上电时序 |
| P2 | SoC 核心 | T113-S3、24MHz/32.768kHz 晶振、复位、BOOT 选择、JTAG、去耦 |
| P3 | 存储与启动 | SPI NAND（W25N01GV）、FEL 按键、TF 卡座（DNP 预留） |
| P4 | 网络 | EMAC RMII、IP101GRI PHY、RJ45 |
| P5 | USB | USB0（Type-C）、USB1（USB-A）、VBUS 开关、ESD |
| P6 | 串口 | RS485-1（UART3 隔离）、COM1 DB9 双协议（RS232/RS485 隔离可切换）、调试串口 |
| P7 | 显示与触摸 | 50PIN RGB888 液晶（实际屏引脚表）、面板偏压（TPS65100）、背光 Boost、TPADC 电阻触摸 |
| P8 | 无线扩展与杂项 | SDIO+USB 无线排针、双色状态 LED、按键、RTC 电池 |

## 3. 电源设计（P1）

### 3.1 输入保护与宽压变换

```
J1 12~24V 端子 ──► Q1(P-MOS AOD4184 反接保护) ──► F1(自恢复保险丝 2A)
   ──► TVS1(SMCJ24A 双向, 3000W 峰值脉冲) ──► L1(共模电感 选件 DNP) ──► C1(220µF/35V 电解)+C2(10µF/25V)
   ──► U1 MP1584EN（宽压降压 4.5~28V → 5V_SYS，3A，SOP8）
        EN 由输入分压（12V 以上启动），SW ──► L2(10µH/3A) ──► 5V_SYS
        输出：C3(22µF/10V)+C4(10µF)+C5(100nF)；COMP 反馈网络按数据手册
5V_SYS ──► C6(100µF/10V 电解) + C7(10µF) + C8(100nF)
```
- **反接保护**：P-MOSFET（AOD4184，VGS 阈值低）串在输入正极，防电源反接（工业现场常见误接）。
- **浪涌防护**：SMCJ24A 双向 TVS 钳位 12~24V 输入浪涌（IEC 61000-4-5 等级）；必要时加输入共模电感（DNP 预留）。
- **宽压变换**：MP1584EN（4.5~28V 输入，3A 输出）将 12~24V 降至 5V_SYS；24V 输入下留足裕量，瞬态尖峰由 TVS 钳位。备选 **TPS5430**（5.5~36V，3A，SOP8）以获取更高浪涌裕量。
- 保险丝置于反接保护之后、TVS 之前；输入电解电容耐压 ≥35V。
- 若后续需 5V 直供（Type-C VBUS），可并联 D 肖特基 OR 供电位（DNP），不作为默认供电路径。

### 3.2 电源树与上电时序

| 网络 | 电压 | 负载 | 器件 | 使能来源 |
| --- | --- | --- | --- | --- |
| 5V_SYS | 5V / 3A | 全板中间总线 | U1 MP1584EN（宽压 12~24V→5V） | 输入分压（≥12V 启动） |
| 3V3 | 3.3V / 2A | VCC_IO、存储、PHY、串口、LCD 逻辑 | U2 SY8089A1AAC | 5V_SYS 经 RC（EN≥1.2V 阈值） |
| 0V9_CORE | 0.9V / 2A | VCC_CPU | U3 SY8089A1AAC | 3V3 输出（电阻分压给 EN） |
| 1V5_DRAM | 1.5V / 1A | VCC_DRAM（内置 DDR3） | U4 SY8088AAC | 0V9_CORE 输出（电阻分压给 EN） |
| 1V8 | 1.8V / 0.5A | VCC_PLL、AVCC、EFUSE、RTC | U5 ME6211A18 LDO | 3V3 输入（LDO 无 EN 时直连） |
| ISO_5V | 5V 隔离（每路） | 隔离侧 RS485 收发器 | U17/U19 B0505S-1W ×2 | 5V_SYS 输入，受协议/使能控制（见 §6.4） |

**上电顺序硬性要求：3V3 → 0V9_CORE → 1V5_DRAM。**
实测教训（见项目记忆）：DRAM 电压若早于核心电压上电会导致 DDR3 初始化失败、系统卡死在 uboot。本设计通过使能链强制顺序：`5V_SYS →(U2 EN) 3V3 →(U3 EN) 0V9_CORE →(U4 EN) 1V5_DRAM`。

- U2/U3 使能：EN 脚接 5V_SYS/3V3 经 10k+0.1µF RC 软启动；U4 的 EN 由 0V9_CORE 经 100k/47k 分压（阈值约 0.6V 时 0.9V 已建立 80%+）。
- 每路输出：电感（U2/U3 2.2µH，U4 4.7µH，额定电流≥2×输出电流）、反馈电阻按 `Vout=0.6×(1+RH/RL)` 计算、输入/输出陶瓷电容阵列（10µF+100nF+10nF）。

### 3.3 各路滤波与 SoC 去耦（遵循《T113-S3 硬件设计指南》）

| 电源组 | 引脚数 | 每引脚附近电容 | 汇总电容 |
| --- | --- | --- | --- |
| VCC_IO（3V3） | ~20 | 100nF | 板级：10µF×4 + 100nF×4 |
| VCC_CPU（0V9） | ~12 | 100nF + 10nF | 10µF×4 |
| VCC_DRAM（1V5） | 3 | 100nF | 10µF×3 + 1µF×3 |
| VCC_PLL/AVCC（1V8） | ~8 | 100nF | 10µF×2；AVCC 加磁珠（600Ω/100MHz） |

> 去耦电容尽量靠近对应电源引脚，VCC_CPU 与 VCC_DRAM 的电容阵列不得省略（DDR3 稳定性关键）。

### 3.4 电源指示灯

```
5V_SYS ──► R(1k) ──► D(绿色 0603) ──► GND   （PWR_LED）
```

## 4. T113-S3 核心电路（P2）

### 4.1 主晶振与 RTC

```
X1：24MHz 无源晶振（2016 封装，20pF 负载）
  OSC_IN  ──► X1(1) ── C(20pF) ── GND
  OSC_OUT ──► X1(3) ── C(20pF) ── GND
X2：32.768kHz 晶振（3215 封装，12.5pF）
  32K_IN/32K_OUT 各接 12.5pF 到 GND
```

### 4.2 复位与 BOOT

- 复位：`RST` 脚接 10k 上拉 + 100nF 到地 + 按键（SMD 轻触）到地。
- BOOT 启动源选择：`BSP0/BSP1/BSP2`（BOOT_SEL）按 **SPI NAND 启动**配置上/下拉电阻，具体取值以全志官方《T113-S3 硬件设计指南》启动章节表格为准（录入时照官方表格放置 0Ω/上拉/下拉）。
- FEL 按键：按官方参考设计接法（触发 BootROM FEL 模式，用于 PhoenixSuit 通过 Type-C/USB0 烧录）；同时保留 `FEL` 引出测试点。
- JTAG（可选）：TMS/TCK/TDI/TDO 4P 1.27mm 排针，引脚复用以手册为准。

### 4.3 芯片本体与去耦

- U6 T113-S3（eLQFP128，含 EP 散热焊盘）：**EP 焊盘必须接 GND**，焊盘下方打热过孔阵列（0.3mm 孔，间距 1.0mm，见 `04` §6）。
- 所有电源/地引脚逐一按 §3.3 表加去耦电容。
- 未用 IO：按《硬件设计指南》要求处理（通常悬空或按手册推荐上下拉），禁止出现浮空输入导致的漏电/误触发（逐项对照手册 GPIO 处理表）。

## 5. 引脚分配总表（核心设计表）

> 引脚列 = SoC 物理引脚上的复用功能。**加粗**项与现有固件强绑定，不可改。

| 功能模块 | SoC 接口 | 引脚（复用） | 说明 |
| --- | --- | --- | --- |
| 调试串口 | UART1 | **PG6(TX)/PG7(RX)** | 115200 控制台，4P 排针引出 |
| RS485-1 | UART3 | **PG8(TX)/PG9(RX)** | Modbus RTU 默认口，**电气隔离**（MAX3485） |
| COM1 双协议 | UART2 | PE2(TX)/PE3(RX) | **DB9：RS232 / RS485(隔离) 可切换** |
| 协议切换 | GPIO | PE8（SHDN/使能控制，以手册为准） | RS232/RS485 模式选择（拨码可硬件强制） |
| 以太网 RMII | EMAC | PG0(TX_EN)/PG1(TXD0)/PG2(TXD1)/PG3(RXD0)/PG4(RXD1)/PG5(CRS_DV)/PG12(MDC)/PG13(MDIO)/PG11(25M_CLK,选件) | IP101GRI，PG14/PG15 备用（以手册为准） |
| USB0 OTG | USB0 | USB0-DP/USB0-DM + ID(可选) | Type-C |
| USB1 Host | USB1 | USB1-DP/USB1-DM | USB-A |
| RGB 液晶 | DE0（RGB888 24bit） | 数据 **PB2-PB7 + PD0-PD17**；控制 **PD18(CLK)/PD19(DE)/PD20(HSYNC)/PD21(VSYNC)** | 位序与映射见 §6.5，已按 SDK `sun8iw20p1.dtsi rgb24_pins_a` 核对 |
| 电阻触摸 | TPADC | TP0-TP3（4 线 X+/X-/Y+/Y-） | 直连 4 线电阻屏 |
| 背光调光 | PWM7 | **PD22（pwm7）** | → 背光 Boost 调光脚，与 mq_r dts `lcd_pwm_ch=7` 一致 |
| SPI NAND | SPI0 | PC0(CLK)/PC1(MOSI)/PC2(MISO)/PC3(CS)（以手册为准） | W25N01GV |
| WiFi 预留 | SMHC1/SDIO1 | SDIO1-CLK/CMD/D0-D3 + RESET + WAKE（以手册为准） | 无线排针 |
| 5G 预留 | USB1（Host 分支） | USB1-DP/DM + VBUS | 无线排针 |
| 状态灯 | LEDC GPIO | **PE0(红)/PE1(绿)** | 双色灯，兼容现有固件 |
| 预留 GPIO | GPIO | PE9、PG10 等 | 蜂鸣器/使能/备用输入 |
| 存储 I2C（DNP） | TWI1 | 以手册为准 | 板载 EEPROM 预留 |
| 空闲串口 | UART5 | PE6(TX)/PE7(RX) | 预留（备用调试/扩展） |

### 5.1 冲突规避说明

- EMAC 选 **PG 组**：不与 UART3（PG8/PG9）、UART1（PG6/PG7）、LEDC（PE 组）冲突。
- RGB888 24bit 用 **PB2-PB7 + PD0-PD17（数据）+ PD18-PD21（控制）**：占用 PB2/PB3（原 TWI0/UART4）与 PB6/PB7（原 CIR/CTP），本设计不使用这些功能，无冲突；PD22 空闲用于 PWM7 背光。
- 电阻触摸走专用 TPADC：不与任何 GPIO 复用，省外部芯片。
- ⚠️ 若最终手册显示 SDIO1/EMAC 引脚存在复用冲突，优先保证固件强绑定项（UART1/UART3/PWM7-PD22/PE0-PE1），其余按手册调整并同步更新 `firmware/t113/tina/dts/` 的 board.dts。

## 6. 各功能模块逐网连接

### 6.1 存储（P3）

```
W25N01GVZEIG (U7, WSON8/8x6mm)
  CS#   ◄── SPI0_CS   (SoC PC3)
  CLK   ◄── SPI0_CLK  (SoC PC0)
  DI    ◄── SPI0_MOSI (SoC PC1)
  DO    ──► SPI0_MISO (SoC PC2)
  WP#   ──► 3V3（10k 上拉，禁用写保护）
  HOLD# ──► 3V3（10k 上拉）
  VCC   ──► 3V3，100nF+10µF 去耦
  GND   ──► GND
```
- 8 脚封装 6×5mm 亦可（GD5F1GQ 等），原理图按选型替换。
- TF 卡座（DNP 预留）：SMHC0 引脚（以手册为准），电路按标准 SD 卡参考：CLK/CMD/D0-D3 + CD + VDD(3V3) + GND，靠近卡座放 33Ω 串阻。

### 6.2 以太网（P4）

```
IP101GRI (U8, QFN32)
  TXD0/TXD1/TX_EN  ◄── EMAC_RMII_TXD0/TXD1/TX_EN  (PG1/PG2/PG0)
  RXD0/RXD1        ──► EMAC_RMII_RXD0/RXD1        (PG3/PG4)
  CRS_DV           ──► EMAC_RMII_CRS_DV           (PG5)
  MDC              ◄── EMAC_MDC                    (PG12)
  MDIO             ◄─► EMAC_MDIO                   (PG13)
  REF_CLK(50M)     ──► EMAC_RMII_REF_CLK（PG14/PG15，以手册为准）
  XI/XO            25MHz 晶振（或 SoC 提供 25M，见下）
  RESET#           ◄── PHY_RST_N（PG7 或 GPIO，10k 上拉 + 100nF）
  3V3 电源：100nF×4 + 10µF；RX+/RX-/TX+/TX- 4 对差分 → HR911105A
```
- **时钟方案二选一**：
  - A（推荐）：PHY 自带 25MHz 晶振，`use_ephy25m=0`，RMII REF_CLK 由 PHY 产生 50MHz 给 SoC。
  - B：SoC `use_ephy25m=1` 输出 25MHz（PG11/PE10，以手册为准）给 PHY XI。
  - 两种方案在 `board.dts` 的 `gmac0` 节点通过 `use_ephy25m` 配置，已有多篇量产板实测验证（IP101GR 适配）。
- RJ45：HR911105A（带变压器与 LED），TX±/RX± 差分对，变压器中心抽头按 PHY 数据手册接 3.3V/0.1µF 或直接耦合。
- PHY 地址：MDIO 上拉/下拉设置 IP101GRI 地址（默认 0x1 或按板定义，与 dts 一致）。

### 6.3 USB（P5）

```
USB0（Type-C，P6）
  Type-C 座：D+/D- → USB0_DP/USB0_DM（经 ESD 芯片 USBLC6-2SC6）
  CC1/CC2：各 5.1k 到 GND（UFP/Sink 模式）
  VBUS → D1 肖特基 → 5V_SYS（兼作供电）
  VBUS 检测（可选）：分压 → GPADC 或 GPIO
  ID/OTG 检测：按参考设计（Type-C 无 ID，用 CC 判断；或固定 Device/Host 模式引脚配置）
  USB0-DP/DM 差分对 90Ω 阻抗，串 0Ω/22Ω（按 PHY 眼图调整）

USB1（Host，USB-A，P5）
  USB-A 座：D+/D- → USB1_DP/USB1_DM（经 USBLC6-2SC6）
  VBUS ← U14 SY6280AAAC（USB 限流开关 1.5A）← 5V_SYS，EN ← GPIO（默认上拉使能）
  VBUS 与 5V_SYS 之间加 100µF 电解
```

### 6.4 串口（P6）

**COM1 DB9 双协议口（RS232 / RS485 可切换，RS485 电气隔离）：**

DB9 引脚定义（Pin1/4/2/3/5，兼容主流 HMI 品牌 COM 口习惯）：

| DB9 Pin | 信号 | DB9 Pin | 信号 |
| --- | --- | --- | --- |
| 1 | RS485-B（-） | 4 | RS485-A（+） |
| 2 | RS232-TXD | 5 | GND |
| 3 | RS232-RXD | 6~9 | NC |

```
UART2 TX/RX（PE2/PE3）
  ├─ RS232 支路：→ MAX3232ESE (U11, SOIC16) → DB9(2/3)
  │    MAX3232 需 4×0.1µF 电荷泵电容；SHDN# 由模式控制（RS485 模式时禁用）
  └─ RS485 支路（隔离）：→ ADUM1201 (U16, SOIC8, 2 通道) → ISO 侧 MAX3485EESA (U10)
       ADUM1201：ch1=UART2_TX→ISO_TX，ch2=UART2_RX←ISO_RO
       ISO 侧供电：U17 B0505S（5V→5V 隔离）→ ISO_5V / ISO_GND
       MAX3485 A/B → DB9(4/1)；A/B 间 120Ω 终端（可焊）；A、B 各接 SM712 TVS

模式切换（GPIO + 拨码双保险）：
  GPIO_MODE (PE8)：HIGH=RS485 模式，LOW=RS232 模式
    · RS485 模式：使能 B0505S（EN 或 P-MOS 开关）→ ISO 侧 MAX3485 上电；
      MAX3232 SHDN#=LOW（输出高阻）
    · RS232 模式：B0505S 关闭（MAX3485 无电=高阻）；MAX3232 SHDN#=HIGH 使能
  S1 拨码开关（可选）：硬件强制 RS232/RS485，优先级高于 GPIO（防现场误切）
  DE/RE# 方向：ISO 侧 MAX3485 RE#/DE 并接，由 TX 经 RC 整形自动方向（标准半双工做法）；
             若需显式 RTS 控制，改用三通道隔离器 ADUM1301 增加 DE 通道
```

**RS485-1（UART3，Modbus RTU 固件默认口，电气隔离）：**

```
UART3 TX/RX（PG8/PG9）→ ADUM1201 (U18) → ISO 侧 MAX3485EESA (U9) → 端子 P1（A+/B）
  ADUM1201：ch1=UART3_TX→ISO_TX，ch2=UART3_RX←ISO_RO
  ISO 侧供电：U19 B0505S → ISO_5V / ISO_GND（独立于 COM1 隔离通道）
  MAX3485 A/B → P1；120Ω 终端可焊；A、B 各接 SM712 TVS
  RE#/DE：由 UART3 TX 自动方向（与现有固件 RS485 模式一致）；也可用 ttyS3 RTS GPIO
  注：固件 /dev/ttyS3 行为不变（dts 已配 linux,rs485-enabled-at-boot-time）
```

**调试串口：** UART1 TX/RX → 4P 2.54mm 排针（1=GND, 2=RX, 3=TX, 4=3V3 可选）。

> 隔离要求：ISO_GND 与 GND 严格分离（不得共地），隔离器件下方 PCB 净空（见 `04` §5）。B0505S 输入侧并联 10µF+100nF，输出侧 10µF+100nF。

### 6.5 显示与触摸（P7）

**50PIN 0.5mm FPC 液晶接口定义（P7）——已按所选屏幕 SZCLX070H-500-IPS-G-RTP 实际规格书锁定：**

> 屏幕：7.0″ IPS 1024×600，16.7M 色（RGB888），正常黑，透射；工作温度 -20~+70℃；
> 背光 27 颗白光 LED（20mA/颗，3 串×9 并，VLED≈9.6V）；触摸：4 线电阻触摸（**独立 FPC，不在 50PIN 内**）。

| Pin | 信号 | 说明 | Pin | 信号 | 说明 |
| --- | --- | --- | --- | --- | --- |
| 1 | VLED+ | LED 阳极 | 26 | G1 | Green data |
| 2 | VLED+ | LED 阳极 | 27 | G0 | Green data (LSB) |
| 3 | VLED- | LED 阴极 | 28 | R7 | Red data (MSB) |
| 4 | VLED- | LED 阴极 | 29 | R6 | Red data |
| 5 | GND | 地 | 30 | R5 | Red data |
| 6 | VCOM | 公共电压（偏压） | 31 | R4 | Red data |
| 7 | DVDD | 数字电源 **3V3** | 32 | R3 | Red data |
| 8 | MODE | DE/SYNC 选择（**H=DE 模式**） | 33 | R2 | Red data |
| 9 | DE | 数据使能 | 34 | R1 | Red data |
| 10 | VSYNC | 垂直同步 | 35 | R0 | Red data (LSB) |
| 11 | HSYNC | 水平同步 | 36 | GND | 地 |
| 12 | B7 | Blue data (MSB) | 37 | DCLK | 时钟（**下降沿锁存**） |
| 13 | B6 | Blue data | 38 | GND | 地 |
| 14 | B5 | Blue data | 39 | SHLR | 左右扫描方向（Note 4） |
| 15 | B4 | Blue data | 40 | UPDN | 上下扫描方向（Note 5） |
| 16 | B3 | Blue data | 41 | VGOH | 栅极开启电压 **18V**（偏压） |
| 17 | B2 | Blue data | 42 | VGL | 栅极关闭电压 **-6V**（偏压） |
| 18 | B1 | Blue data | 43 | AVDD | 模拟电源 **9V**（偏压） |
| 19 | B0 | Blue data (LSB) | 44 | RESET | 全局复位（**低有效**，RC+上拉） |
| 20 | G7 | Green data (MSB) | 45 | NC | 悬空 |
| 21 | G6 | Green data | 46 | VCOM | 公共电压（偏压） |
| 22 | G5 | Green data | 47 | DITH | 抖动使能（**默认拉低**） |
| 23 | G4 | Green data | 48 | GND | 地 |
| 24 | G3 | Green data | 49 | NC | 悬空 |
| 25 | G2 | Green data | 50 | NC | 悬空 |

**与 T113 的连接（P7 接线）——24bit RGB888（按 SDK `rgb24_pins_a` 实际引脚）：**

```
数据（24 位）：
  D0=PB2  D1=PB3  D2=PD0  D3=PD1  D4=PD2  D5=PD3  D6=PD4  D7=PD5    ← 蓝组 B7..B0（rb_swap=1）
  D8=PB4  D9=PB5  D10=PD6 D11=PD7 D12=PD8 D13=PD9 D14=PD10 D15=PD11 ← 绿组 G7..G0
  D16=PB6 D17=PB7 D18=PD12 D19=PD13 D20=PD14 D21=PD15 D22=PD16 D23=PD17 ← 红组 R7..R0
控制：
  LCD_CLK=PD18 → Pin37 DCLK（下降沿锁存）
  LCD_DE =PD19 → Pin9  DE
  LCD_HSYNC=PD20 → Pin11 HSYNC
  LCD_VSYNC=PD21 → Pin10 VSYNC
背光：PWM7（PD22）→ BL_PWM；屏 RESET ← GPIO(PE9)+RC

位序规则：T113 D0=LSB，屏 B7（Pin12）为 MSB → 接 D7；B0（Pin19）→ D0。G/R 同理。
颜色对应：D0-D7=蓝、D8-D15=绿、D16-D23=红（内核 dts `lcd_rb_swap=<1>`），
          若 PCB 按 D0-D7=红接线则 `lcd_rb_swap=<0>`（详见 firmware/t113/tina/dts/LCD_RGB24_1024x600.md）

控制/偏压脚：
  MODE（Pin8）  ──► 10k 上拉至 3V3（DE 模式）
  RESET（Pin44）──► GPIO(PE9) + RC（10k 上拉 + 100nF 到地，上电自动复位稳定）
  DITH（Pin47） ──► 10k 下拉至 GND（禁用内部抖动）
  SHLR（Pin39）/UPDN（Pin40）──► 0Ω 选择接 3V3/GND（默认接 GND，正扫；按屏 Note 4/5 调整）
  DVDD（Pin7）  ──► 3V3
  AVDD/VGOH/VGL/VCOM（Pin43/41/42/6/46）──► U13 面板偏压（见下）
  VLED±（Pin1~4）──► U12 背光 Boost 输出
```

**面板偏压（U13，AVDD 9V / VGH 18V / VGL -6V / VCOM ~3.1V）：**

```
5V_SYS ──► U13 TPS65100（或国产 SG2001 兼容）
   ├─► AVDD 9.0V（FB 分压设定）→ Pin43
   ├─► VGOH 18V（电荷泵/倍压） → Pin41
   ├─► VGL -6.0V（负压）       → Pin42
   └─► VCOM ~3.1V（可调分压）  → Pin6、Pin46
  外围：储能电感 22µH + 肖特基×3 + 分压电阻 + 电容（按 TPS65100 参考电路）
```
> ⚠️ **按屏确认**：规格书 5.1 明确列出 AVDD/VGH/VGL/VCOM 电压要求，本设计默认提供；
> 若所购屏驱动 IC 内置 DC-DC（同规格部分屏），U13 整组可 DNP，引脚悬空即可——下单前与创力信确认。
> VCOM 两脚（Pin6/46）在屏内通常相连，只需一路 VCOM 输出。

**双分辨率显示配置（7″ 与 10″ 均为 1024×600）：**

| 配置 | 面板 | 分辨率 | 像素时钟 | 背光 | 接线 |
| --- | --- | --- | --- | --- | --- |
| A（已选） | 7″ SZCLX070H-500-IPS-G-RTP | 1024×600 @60Hz | ~51.2MHz | **27 颗：3串×9并，VLED≈9.6V@180mA** | RGB888（50PIN 实际引脚表见上） |
| B（备选） | 10″ 1024×600 | 1024×600 @60Hz | ~51.2MHz | 通常 24~30 颗（串/并按屏） | RGB888（同 50PIN 接口） |

- **硬件兼容**：两屏同为 1024×600 RGB888，**内核 DTS 时序一致**（仅背光 Rcs 与偏压按屏微调），硬件无需改版。
- **背光适配**：Boost（U12 MT9284-28J/SY7208AABC）输出 ~10V/200mA；`Rcs` 电流采样电阻设两档焊盘（`Rcs_7″` / `Rcs_10″`，DNP 二选一），按所选面板 LED 串并联与电流装焊。
- **固件/编辑器适配**（复用现有机制，无新增硬件）：
  - 内核 DTS `lcd0` 节点：1024×600 时序（像素时钟 ~51.2MHz、RGB888、DE 模式、极性按屏手册）；
  - 编辑器：`AdaptiveLayoutCalculator` + `ScreenAdaptiveMode` 按 1024×600 导出自适应布局；`ProjectSettingsView` 设置屏幕默认分辨率；
  - 触摸：`touch_evdev.c` 的 `HMI_TOUCH_MAX_X/Y` 按 1024×600 校准。
- **结构**：7″/10″ 面板共用 50PIN FPC 位；安装孔按 10″ 外框预留，7″ 用转接垫（当前 7″ 屏 Active Area 154.21×85.92mm）。

**背光 Boost（U12 MT9284-28J 或 SY7208AABC）：**
```
5V_SYS ──► U12 VIN ──► 电感 10µH ──► SW ──► 输出 ~10V/200mA ──► FPC VLED±（27 颗：3串×9并）
PWM2（SoC）──► U12 DIM/PWM 调光脚（BL_PWM）
GPIO ──► U12 EN 使能（BL_EN）
电流采样电阻 Rcs 设定串电流 20mA（总 180mA；两档焊盘适配 7″/10″）
输出整流肖特基 + 输出电容 10µF；输入 10µF+100nF
```

**电阻触摸（TPADC 直连，独立 4 线 FPC）：**
```
屏触摸 FPC（4 线）：X+ / X- / Y+ / Y- ──► T113 TP0-TP3（专用引脚）
每路串 0Ω（调试可断）并加 ESD（可选用排阻位预留）
内核 `sunxi-tpadc`/`ads7846` 兼容驱动注册为 /dev/input/event0（现有固件已支持）
坐标校准：HMI_TOUCH_MAX_X/Y = 1024×600
```

### 6.6 无线扩展（P8）

**2.54mm 2×6 排针（P8）定义：**

| Pin | 网络 | Pin | 网络 |
| --- | --- | --- | --- |
| 1 | SDIO1_CLK | 2 | 3V3_WIFI（开关控制） |
| 3 | SDIO1_CMD | 4 | GND |
| 5 | SDIO1_D0 | 6 | WIFI_EN（GPIO） |
| 7 | SDIO1_D1 | 8 | WIFI_WAKE（GPIO/INT） |
| 9 | SDIO1_D2 | 10 | 5V_MOD（4G/5G 模块供电） |
| 11 | SDIO1_D3 | 12 | USB1_DP/DM（5G 模块 USB 分支） |

- WiFi：SDIO1 + 使能/唤醒 GPIO（兼容 RTL8723DS/RTL8189FTV/正基 AP 系列模块，电源由 3V3 经负载开关（如 SY6280）控制）。
- 4G/5G：USB1 Host 引出（USB1-DP/DM 分支 + 5V_MOD），模块复位/开关机由 GPIO 控制（可选排针扩展）。
- SDIO1 具体引脚以手册为准；与 EMAC（PG 组）共用时注意复用冲突，录入前核对。

### 6.7 状态指示与按键（P8）

```
双色 LED（红/绿，共阴，0805 双色）
  PE0 ──► R(1k) ──► 红──┐
  PE1 ──► R(1k) ──► 绿──┴── GND
按键：RST（复位）、FEL（烧录）、USER（用户，GPIO 输入 10k 上拉）
```

## 7. ESD / EMC 防护元件汇总

| 接口 | 器件 | 网络位置 |
| --- | --- | --- |
| 12~24V 电源 | SMCJ24A 双向 TVS（3000W 浪涌）+ P-MOS 反接保护 + 自恢复保险丝 + LC 滤波 | 输入端 |
| Type-C/USB-A | USBLC6-2SC6（每口 1 颗） | D+/D- |
| RJ45 | 内置变压器隔离 + TVS（如 SM712 用于 PHY 侧，可省） | 差分对 |
| RS485 A/B（RS485-1 与 COM1） | SM712（双向 TVS）+ 120Ω 终端（可焊） | 端子/DB9 内侧 |
| RS232 | 无需外部 TVS（MAX3232 内置 ±15kV） | — |
| LCD FPC | 数据线可选排阻 ESD（0402 阵列，预留） | FPC 内侧 |

> 隔离通道（ADUM1201 + B0505S）：防止 RS485 现场地环路与共模噪声进入系统；隔离栅两侧地严格分离（见 `04` §5.1）。

## 8. 原理图检查清单（ERC 前逐项核对）

- [ ] 所有电源网络无悬空，极性正确（DRAM 1.5V、核心 0.9V 不得接反/接错）
- [ ] 12~24V 输入：反接保护方向、TVS 极性、保险丝位置正确；输入电容耐压 ≥35V
- [ ] 上电时序链正确：3V3→0V9_CORE→1V5_DRAM（EN 连接正确）；MP1584EN 先于后级建立 5V_SYS
- [ ] T113 EP 焊盘接地；全部电源引脚有去耦电容
- [ ] 24MHz/32.768kHz 晶振负载电容正确
- [ ] BOOT_SEL 电阻按 SPI NAND 启动配置（对照官方表）
- [ ] UART1/UART3/PWM2/PE0-PE1 与现有固件一致（不可改动）
- [ ] COM1 协议切换：GPIO_MODE 与 MAX3232 SHDN#、B0505S 使能逻辑一致（RS485 模式两者互斥）
- [ ] RS485 隔离：ADUM1201 通道方向正确（TX→ISO、RX←ISO）、B0505S 输入输出极性、ISO_GND 与 GND 分离
- [ ] 以太网 MDC/MDIO 上拉（IP101GRI 手册要求）、PHY 地址与 dts 一致
- [ ] USB D+/D- 差分对无串扰源；VBUS 开关使能默认正确
- [ ] 所有 IC 电源去耦齐全；RS485 终端电阻可选位正确
- [ ] 背光 Boost：Rcs 两档焊盘（7″/10″）逻辑正确，输出电压满足所选面板 LED 串压
- [ ] ERC 无未连接电源/输出短接/单点网络误报（逐一确认）
