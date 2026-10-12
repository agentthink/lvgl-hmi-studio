# LCEDA-Pro 录图执行顺序

## 0. 建工程

工程名建议：`HMI_T113_SZCLX070H_R1`。

创建 9 张原理图页：

1. `P1_POWER_INPUT`
2. `P2_SOC_CORE`
3. `P3_STORAGE`
4. `P4_LCD_RGB888`
5. `P5_LCD_POWER_BACKLIGHT`
6. `P6_RTP_TOUCH`
7. `P7_USB`
8. `P8_COMM`
9. `P9_IO_EXPANSION`

根图只放 9 个层次页，不在根图重复画电路。

## 1. 录图原则

- 先放连接器和主 IC，再放电源、去耦、保护和测试点。
- 每个跨页网络使用全局标签，标签名必须与 `lceda-page-net-matrix.csv` 完全一致。
- 所有电源轨分别放置测试点和 PWR_FLAG/ERC 电源驱动标记。
- `ISO_GND` 与 `GND` 不能使用同名标签或铜连接。
- LCD 50PIN 必须依据 `lcd-50pin-pinout.csv`，禁止套用旧版 RGB666 FPC 符号。
- RTP 四线不能放到 LCD 50PIN，必须走 P6 独立连接器。

## 2. P1 电源顺序

输入链路：

```text
J1 VIN24_RAW → F1 → Q1 反接保护 → VIN24_F → TVS/滤波 → U1 → VCC5V
VCC5V → U2 → VCC3V3 → U4 EN → VDD0V9 → U5 EN → VCC1V5
VCC3V3 → U3 → VCC1V8
```

ERC 上电顺序检查必须确认：

```text
VCC3V3 先于 VDD0V9
VDD0V9 先于 VCC1V5
```

不能把 U4/U5 的 EN 直接并到输入电压。

## 3. P4 LCD 数据接线

严格按如下方向接线：

```text
T113 D0  = PB2  → LCD_B0 → J3 Pin19
T113 D1  = PB3  → LCD_B1 → J3 Pin18
T113 D2  = PD0  → LCD_B2 → J3 Pin17
...
T113 D7  = PD5  → LCD_B7 → J3 Pin12

T113 D8  = PB4  → LCD_G0 → J3 Pin27
...
T113 D15 = PD11 → LCD_G7 → J3 Pin20

T113 D16 = PB6  → LCD_R0 → J3 Pin35
...
T113 D23 = PD17 → LCD_R7 → J3 Pin28
```

软件同步要求（以 board.dts 实际值为准）：

```text
lcd_rb_swap = 1
lcd_frm = 1          # 固件实际值，使能 RGB666 抖动（非 0）
lcd_pwm_ch = 7
lcd_pwm_freq = 50000
lcd_pwm_pol = 1
lcd_dclk_freq = 51   # MHz
```

## 4. P4 LCD 电源/控制

- `LCD_DVDD` 接 3.3V，J3 Pin7。
- `LCD_AVDD` 目标 9.0V，J3 Pin43。
- `LCD_VGH` 目标 18.0V，J3 Pin41。
- `LCD_VGL` 目标 -6.0V，J3 Pin42。
- `LCD_VCOM` 目标 3.15V，J3 Pin6/46。
- `LCD_MODE` 10k 上拉到 3.3V，J3 Pin8。
- `LCD_DITH` 10k 下拉到 GND，J3 Pin47。
- `LCD_RESET` 10k 上拉 + 100nF 到 GND，低有效。
- `LCD_SHLR`/`LCD_UPDN` 使用 0Ω 选择焊盘，默认方向按屏厂 Note 4。

## 5. P5 背光

规格目标：`VLED≈9.6V`，典型总电流 `180mA`，单颗 LED `20mA`。

- 使用恒流升压方案，不能只用固定电压输出。
- `RCS1` 必须按实际 LED 串并联拓扑计算。
- `LCD_BL_PWM` 接 T113 `PD22/PWM7`。
- 输出增加 `VLED_A`、`VLED_K` 测试点。
- 如果 LED 串并联拓扑未从屏厂背光图确认，`RCS1` 保留两档 DNP/可选焊盘。

## 6. P6 RTP

四线触摸连接器针序已由用户粘贴图片确认：

```text
J4 Pin 1 = Y1/Y- → TCH_YN → TP-Y2
J4 Pin 2 = X1/X+ → TCH_XP → TP-X1
J4 Pin 3 = Y2/Y+ → TCH_YP → TP-Y1
J4 Pin 4 = X2/X- → TCH_XN → TP-X2
```

连接器机械尺寸、FPC 触点朝向、ESD 位置仍需实物/封装图确认。X/Y 正负方向最终需通过软件校准验证。

## 7. ERC / 投板前检查

- [ ] T113 每个电源脚有正确电源轨和去耦。
- [ ] RGB24 24 根数据线逐一与 `t113-lcd-net-assignment.csv` 对照。
- [ ] LCD Pin45/49/50 使用 `No Connect`，不是悬空无标记。
- [ ] GND、PGND、AGND、ISO_GND 分区正确。
- [ ] RS485 隔离器两侧没有共地。
- [ ] USB Type-C CC1/CC2 电阻方向正确。
- [ ] RMII REFCLK 方向与最终 PHY 型号一致（IP101GRI 用外部 25MHz 晶振，PHY 产生 50MHz 给 MAC）。
- [ ] SPI NAND 的 CS/CLK/DI/DO 与 T113 手册一致（PC2-PC7）。
- [ ] 上电顺序 VCC3V3→VDD0V9→VCC1V5。
- [ ] 面板偏压输出不超过确认范围。
- [ ] 背光输出采用恒流，180mA 作为典型目标（3S9P 拓扑，RCS1=0.56R，MT9284 VFB=104mV）。
- [ ] RTP FPC 针序已获屏厂确认。
- [ ] **PE9 专用于 LCD_RESET**，DTS 中 gpio_pe9 LED 定义需删除（否则输出 LOW 持续复位 LCD）。
- [ ] **PB2-PB7 与 CAN0/CAN1/UART3/TWI0/TWI1/TWI3 复用**：RGB24 时被 LCD 占用；RGB18（当前固件）时空闲。
- [ ] LCEDA-Pro ERC 无真实错误。
- [ ] 所有器件封装已绑定，不能依赖空 Footprint 字段。
