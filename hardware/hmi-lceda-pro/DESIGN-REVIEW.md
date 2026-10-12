# HMI_T113_SZCLX070H_R1 设计审查记录

## 当前基线

- 主控：T113-S3。
- 输入：12~24V DC。
- LCD：SZCLX070H-500-IPS-G-RTP，1024×600 RGB888，DE 模式，DCLK 下降沿锁存。
- LCD 电源：DVDD 3.3V，AVDD 9.0V，VGH 18.0V，VGL -6.0V，VCOM 3.15V。
- 背光：27 颗白光 LED，VLED 9.6V 典型，180mA 典型，单颗 20mA，恒流驱动。
- RTP：独立 4 线，J4.1=Y1/Y-，J4.2=X1/X+，J4.3=Y2/Y+，J4.4=X2/X-。

## 已生成并验证

- `lcd-50pin-pinout.csv`：50 个 LCD 引脚连续且已按规格表核对。
- `t113-lcd-net-assignment.csv`：38 条 T113 RGB/RTP 分配，RGB888 数据和 PWM7/PD22 已核对。
- `lceda-page-net-matrix.csv`：66 条 P1~P9 页面连接记录。
- `normalized-netlist.csv`：110 条展开后的逐网络连接记录。
- `lcd-t113-pin-handoff.csv/.md`：50 条 LCD 逐脚交接表。
- `capture-package/P1..P9.csv`：9 页 LCEDA-Pro 录图源表。
- `project.json`：工程元数据和 confirmed/pending 状态。
- `bom-review.csv`：按原理图参考号汇总的 33 项审核 BOM。
- `RELEASE-MANIFEST.sha256`：45 个交接文件的 SHA256 清单。

运行验证：

```text
project validated: 66 matrix rows, 110 normalized connections, 50 pin handoff rows, 50 LCD pins, 38 SoC assignments, 9 pages
RTP electrical pinout validated; connector mechanical footprint remains pending
LCD 50PIN: validated
T113 RGB888/RTP assignment: validated
```

## 不得直接投板的项目

1. LCEDA-Pro 原生工程尚未在 GUI 中录入，当前产物是确定性的录图源包，不是假造的专有工程格式。
2. RTP FPC 的机械封装、触点朝向和装配尺寸未确认。
3. 背光 27 LED 的真实串并联拓扑未从背光电路图确认；MT9284/SY7208 和 RCS 不能仅凭典型电压电流冻结。
4. `IP101GRI`/`RTL8201F` 最终 PHY、封装、RMII REFCLK 方向和 PHY 地址未冻结。
5. T113 非 RGB 外设的官方复用表仍需逐项复核，特别是 SPI NAND、UART、RS485 DE、RS232、以太网和扩展排针。
6. 所有器件仍需在 LCEDA-Pro 绑定真实 LCSC/封装号。
7. 必须在 LCEDA-Pro 实际工程内运行 ERC；脚本验证不等于电气 ERC。

## 录图完成判定

只有以下条件全部满足，才可以把目标标记完成：

- [ ] LCEDA-Pro 原生工程保存并可重新打开。
- [ ] P1~P9 全部录入，器件参考号与 `bom-review.csv` 一致。
- [ ] 50PIN LCD 逐脚连线并完成 NC 标记。
- [ ] RGB888 24 根数据线逐根通过 T113 引脚表复核。
- [ ] RTP J4 针序和机械封装确认。
- [ ] 背光恒流拓扑和 RCS 计算完成。
- [ ] PHY 和非显示引脚复用完成官方手册复核。
- [ ] 所有封装绑定。
- [ ] ERC 无真实错误。
- [ ] 导出网表/BOM，并与源矩阵比对无漏网。
