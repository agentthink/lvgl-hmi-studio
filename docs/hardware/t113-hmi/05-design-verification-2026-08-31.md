# HMI 硬件设计验证报告（2026-08-31）

## 检查对象

当前检查基线为 `hardware/hmi-lceda-pro/kicad-export/out/` 下的
`HMI_T113_SZCLX070H_R1` 九页层次原理图；旧版
`hardware/hmi-control-board-t113/` 不作为本次投板基线。

## 自动验证结果

| 项目 | 结果 |
|---|---|
| 层次原理图根图 ERC（KiCad 10.0.6） | 0 errors / 0 warnings |
| `check_nets.py` 静态连通性 | P1-P9 全部通过，无意外网络合并 |
| 网表导出 | 239 nets：167 个实际网络、72 个预期 NC 网络 |
| 器件/位号 | 143 个器件、150 个唯一位号 |
| T113-S3 引脚覆盖 | 109 个已连接 + 19 个 NC，128/128，无遗漏 |
| LCD 50PIN | 50 脚均有表示，NC 与电源脚正确 |
| RGB/RTP SoC 分配 | 38 条分配全部匹配基线 |
| NAND / microSD | 与 W25N01GV、标准 microSD 针脚一致 |
| RS485 | ADUM1201 + MAX3485 + B0505S 信号/隔离电源链路一致 |
| RMII | IP101GRI 的 TX/RX、REFCLK、MDC/MDIO、复位链路一致 |
| RS232 / USB | MAX3232、USB-C USB0、USB-A USB1 连线一致 |

单独对 P1-P9 做 ERC 会出现跨页电源/驱动提示；这是单页运行造成的上下文缺失，不能替代根图层次 ERC。根图 ERC 才是本工程的有效结果。

## 已核对的关键细节

- LCD_MODE：R401 10k 上拉至 VCC3V3，选择 DE 模式。
- LCD_DITH：R402 10k 下拉至 GND。
- LCD_SHLR / LCD_UPDN：R404/R405 下拉至 GND。
- LCD_RESET：PE9，经 R403 上拉、C401 去耦。
- MT9284：PWM 直接接 pin 4；`LCD_BL_EN` 是预留的独立上拉网络，不是遗漏。
- MAX3232 pin 7/8/9 保持 NC，已确认此前 pin 7 短接问题不存在。
- T113 ball 与引脚名映射已按 `refs/t113-pins.csv` 复核，未发现左右槽位反转问题。

## 投板前必须关闭的风险

1. TPS65100 的 AVDD/VGH/VGL/VCOM 反馈及补偿参数仍为 TBD；必须按数据手册和面板实测值计算并复核。
2. MT9284 pin 5 OVP 分压未完成；当前为 NC，不能直接量产。
3. 背光 27 LED 的 3S9P 拓扑需屏厂背光图确认；R510 当前为 0.56 ohm。
4. RTP 连接器的机械尺寸、触点朝向和 FPC 装配方向未确认。
5. LCD FPC 的上接/下接方向需按实物确认。
6. FP6276 用作 USB1 限流/负载开关属于非典型用法，建议评估改用专用负载开关（如 SY6280 类）。
7. RS485_DE 当前默认下拉为接收态，需确认固件 GPIO/RTS 驱动方案。
8. FEL 按键网络未接入 T113 专用 FEL 引脚；量产烧录/救砖方案需定稿。
9. PE9 已用于 LCD_RESET，固件必须删除冲突的 `gpio_pe9` LED 定义，否则可能持续复位 LCD。
10. 固件当前基线为 RGB18，而原理图保留 RGB24 物理布线（PB2-PB7 接 LCD 低位）；需在实机确认 RGB666 位对齐、抖动和 PB 复用方案。
11. J11 扩展排针中的 PE14/PE15、PB10 等历史分配需以 eLQFP-128 实际封装和最终 pin-mux 重新确认。
12. 所有器件封装、T113 暴露焊盘、FPC 连接器方向及 PCB 走线约束仍需在 LCEDA-Pro/PCB 阶段完成。

## 结论

原理图的**结构连通性和关键引脚映射通过验证**，但目前只能作为原理图交接/录图基线，**不具备直接投板条件**。投板前重点应放在 LCD 偏压与背光电源参数、FPC 机械方向、USB1 负载开关、RS485_DE、FEL 及 RGB18/RGB24 固件一致性。
