# HMI_T113_SZCLX070H_R1 原理图 KiCad 导出包

由 `../generate.py` 数据驱动生成（源数据：t113-pins.csv / normalized-netlist.csv /
lceda-page-net-matrix.csv / lcd-t113-pin-handoff.csv / PIN-MUX-AND-COMPONENT-SELECTION.md）。

## 文件清单

| 文件 | 说明 |
|------|------|
| `HMI_T113_SZCLX070H_R1.kicad_pro` | KiCad 6+ 工程文件 |
| `HMI_T113_SZCLX070H_R1.kicad_sch` | 根图（9 个层级 sheet，A1 图纸） |
| `P1_POWER.kicad_sch` | 电源输入与电源树（J1/F1/Q1/D1/U1-U5/TP1-3） |
| `P2_SOC_CORE.kicad_sch` | T113-S3（128 脚全引脚）+ 24M/32k 晶振 + 去耦 + DZQ |
| `P3_STORAGE.kicad_sch` | W25N01GV SPI NAND + microSD |
| `P4_LCD_RGB888.kicad_sch` | 50P FPC（J3 全 50 脚按 lcd-50pin-pinout.csv）+ 控制电阻 |
| `P5_LCD_POWER_BACKLIGHT.kicad_sch` | TPS65100 偏压 + MT9284 背光升压 |
| `P6_RTP_TOUCH.kicad_sch` | 4 线电阻触摸 FPC + 0R 调试链 |
| `P7_USB.kicad_sch` | USB-C(USB0 OTG) + USB-A(USB1 Host) + SY6280AAAC 限流 |
| `P8_COMM.kicad_sch` | 隔离 RS485(ADUM1201+MAX3485+B0505S) + RS232(MAX3232+DB9) + 以太网(IP101GRI+HR911105A) |
| `P9_IO_EXPANSION.kicad_sch` | 调试排针 + 2x12 扩展 + 按键 + LED |
| `hmi.kicad_sym` | 全部 36 个符号的 KiCad 6 符号库（参考用；各页已内嵌 lib_symbols） |

## 已验证项（kicad-cli 10.0.5）

- 9 页逐页 + 根图网表导出：全部通过、零警告
- **80 项关键网络连通性验证通过**（LCD_B0..R7/G0..G7/B0..B7、DCLK/DE/VSYNC/HSYNC、
  TCH_x、NAND_x、SD_x、UART1-5、RMII 全部、USB0/1、RESET_N、RS485_A/B、
  VLED_A/K、LCD_VCOM/AVDD/VGH/VGL、BL_PWM、电源轨等）
- 根图 ERC：**0 错误**（仅剩 lib_symbol/footprint 库路径类提示，属预期——无外部库）
- 167 真实网络 + 72 故意 NC / 143 器件 / 150 唯一位号
- 静态连通性检查（check_nets.py）：9 页无意外网络合并
- 全部几何已对齐 1.27mm 栅格
- T113-S3 全 128 脚覆盖（109 连接 + 19 故意 NC，逐个 ball 核对无遗漏）

## 2026-08-20 复查修复记录

- **U10(MAX3232) pin7 T2OUT 短路修复**：NC 引脚原先画了 stub 导线且 no_connect
  落在导线端点，导致 pin7 的水平 stub 恰好穿过 C804 的 C2N 标签被短路进 C2N 网络。
  现改为 NC 引脚不画导线、no_connect 直接落引脚热点。复查 72 个 unconnected 网络
  逐一核对均为预期 NC（J11×12 / U6×19 / U11×8 / J9×9 / J5×6 / U10×3 / J3×3 / U12×3
  / J7×2 / 其余 U1/U2/U3/U4/U5/U13/Q1 各 1）。
- `.kicad_pro` 升级为 KiCad 8/9/10 格式（meta.version=3 + schematic.top_level_sheets）。
  注：kicad-cli 的 `sch export netlist` 不接受 .kicad_pro 输入（参考工程同样报错），
  属 kicad-cli 行为而非本工程问题；`.kicad_sch` 直接导出始终正常。

## 导入嘉立创 EDA 专业版

1. 打开已创建的空工程 `HMI_T113_SZCLX070H_R1`
2. 文件 → 导入 → KiCad（或 原理图 → 导入 KiCad 原理图）
3. 选择本目录的 `HMI_T113_SZCLX070H_R1.kicad_pro`（或逐页导入各 `.kicad_sch`）
4. 导入后核对：每页器件数量、全局网络标签、ERC
5. 按页绑定封装（footprints.csv）；注意 FPC 连接器触点朝向、T113 暴露焊盘

> 各页符号已内嵌 `lib_symbols`，无需先装 hmi.kicad_sym；该库仅供 KiCad 侧参考。

## 位号勘误（相对 PIN-MUX 文档，均为避免重号/非标准位号）

| 文档位号 | 本图位号 | 原因 |
|----------|----------|------|
| R401-R404 (P6 RTP 串联) | **R601-R604** | 与 P4 LCD 串联电阻 R401-R424 重号 |
| RCS1 (P5 采样电阻) | **R510** | 非标准位号格式 |
| U9A/U9B/U9_PWR (P8) | **U9/U16/U14** | 子单元位号无父器件，KiCad 批注报错 |
| C1-C4 (P8 MAX3232 泵电容) | **C803-C806** | 与 P1 去耦电容重号 |

## 2026-09 嘉立创料号复核（全部已修正）

逐项对照立创商城核实全部 LCSC 料号，**发现并修正 11 处料号与实际器件不符**（原编号会买错料）：

| 位号 | 器件 | 原料号（错误） | 现料号（核实） | 说明 |
|------|------|----------------|----------------|------|
| U2/U4/U5 | SY8089A1AAC | C511567 | **C479074** | 原编号为停产「红灯」产品 |
| U3 | RT9193-18GB-MS | C91993 | **C7528821** | 原编号是 TE 连接器；MSKSEMI ¥0.16 有货 |
| U9 | ADUM1201ARZ-RL7 | C25226 | **C9669** | 原编号是 Cleveland 铣刀；ADI 有货 |
| U10 | MAX3232ESE+T | C5757 | **C15846** | 原编号是 EVERLIGHT LED；MAXIM 有货 |
| U13 | MT9284-28J | C99058 | **C181780** | 西安航天民芯 ¥0.23 / 47513 有货 |
| U14 | B0505S-1W | C83467 | **C512032** | 原编号是 Sunlord 33µH 功率电感；RLT ~¥4 有货 |
| U16 | MAX3485ESA+T | C7593 | **C18148** | 原编号是 TI NE555DR；MAXIM ~¥7.5（廉价 UMW C668204 ~¥0.4） |
| X2 | 32.768kHz | C32380 | **C32346** | EPSON Q13FC13500004 ¥1.26 / 49 万有货 |
| TP1-3 | 测试点 | C2381714 | **C238124** | Keystone 5003 有货 |
| R_SHLR/R_UPDN/R401-404 | 0Ω 0603 | C166918 | **已选** | RALEC 0Ω 0603，有货 |
| X1 | 24MHz 晶振 | C7424728 | **已选** | LIMING 3225/12pF，有货 |
| X_ETH | 25MHz 晶振 | C5155479 | **已选** | Slkor 3225/20pF，有货 |

**新补充的在售料号**（原先无编号）：J1 KF129-5.08-2P=C475092(¥0.17)；J3 BOOMELE 0.5-50P=C190666(¥0.82)；J2 TF-015-N=C41426932；J10 1x4排针=C492403(¥0.088)；J11 X5521WV-2x12=C2883562(¥2.8)；D1 SMCJ24A=C19077604(R+O)；Q1 AOD4184A=C99124；RCS1 0.56R=VISHAY RCWE0805R560FKEA C2174717(¥0.20)。

**性价比提示**：
- W25N01GVZEIG C907680 ~¥27 偏贵 —— 降本替代 GD5F1GQ4UBYIGR C492071 ~¥7.9（1Gbit SPI NAND、WSON-8 兼容，需固件验证）。
- MAX3485/MAX3232/ADUM1201 均有国产兼容替代（UMW C668204 / lingxingic C41430944 / JSMSEMI C30587325），价格约为原厂 1/5~1/15。
- 已完成选型：J7 USB-C=C165948、J8 USB-A=C3034183、0Ω 0603=C166918、24MHz=C7424728、25MHz=C5155479、6x6 轻触=C480267、SM712=C3647085、USB ESD=C7519、SS34=C8678、22µH=C44447583、22µF=C45783、3A PTC=C262070。
- 仍需工程确认：J4 RTP FPC 的机械尺寸/接触面，以及 R_LIMIT 6.8k 的最终限流目标；这些属于机械/电气参数确认，不是 LCSC 料号缺失。

## 设计勘误与待办（相对源文档）

1. **MT9284 RCS1=0.56R**（VFB=104mV/180mA，E24），非文档预估的 1.1R —— 已按 0.56R 录入
2. **IP101GRI LCSC = C910373**（WIZNET），文档写 C50326 有误
3. **HR911105A LCSC = C12074**（HANRUN），文档写 C7633 有误
4. **PE14/PE15 不在 T113-S3 eLQFP-128 封装上**（t113-pins.csv 仅有 PE0-PE13），
   J11 Pin7/8 已改 NC；PIN-MUX 文档 J11 分配表按更大封装编写，需整体复核
5. **PB10 不在封装上**：USB1_VBUS_EN 无法接 PB10，当前经 R705 10k 上拉 VCC5V 常开；
   如需 GPIO 控制建议改接 PE10（J11 Pin3）
6. **FEL_N 未接 SoC**：T113 无专用 FEL 引脚，SW2+上拉悬空，量产方案待定
7. **USER_KEY 经 R904 0R 接 PE8**（唯一空闲 GPIO）
8. **RS485_DE 方向控制**：默认 10k 下拉（接收态），需改 GPIO/RTS 驱动（见 P8 注释）
9. **U15 已由 FP6276 替换为 SY6280AAAC（LCSC C207620）**：SOT-23-5 引脚为 1=OUT/2=GND/3=ISET/4=EN/5=IN，R703=6.8k，限流约 1A
10. **J7 USB-C 的旧编号 C165951 不可用**：该编号实际对应 Vishay 0603 电容；需在 LCEDA 按 TYPE-C 16P 重新选择真实连接器
11. **U7 LCSC 已修正为 C907680，U12 TPS65100PWPR 已修正为 C181650**
12. **XL4015 无 EN 引脚**：EN_3V3/EN_0V9/EN_1V5 时序经各 SY8089 的 EN 脚 RC 延时实现
13. **U4 (0V9) DVFS**：PWM3(PG10) 经 R103/C103 RC 滤波注入 FB 分压点，分压值待定（TBD）
14. TPS65100 反馈分压 R501-R509/C515 等数值标 TBD，按目标电压
    （AVDD 9V/VGH 18V/VGL -6V/VCOM 3.15V）依数据手册计算后填入
15. 音频/TV/GPADC 等未用引脚已标 NC；VCC-TVOUT/TVIN 接 VCC3V3 供电
16. 24MHz 晶振接 DXIN/DXOUT（ball22/23），32.768k 接 X32KIN/X32KOUT（ball24/25）
17. R401-R424（22R RGB 串联匹配，选配/DNP）未画入；如需 SI 匹配在 PCB 阶段加入

## 重新生成 / 校验

```bash
python3 generate.py      # 重新生成全部文件到 out/
python3 check_nets.py    # 静态连通性检查（网络合并检测）
kicad-cli sch export netlist --format kicadsexpr \
    out/HMI_T113_SZCLX070H_R1.kicad_sch -o out/root.net
kicad-cli sch erc --severity-error \
    out/HMI_T113_SZCLX070H_R1.kicad_sch --output out/erc.rpt
```
