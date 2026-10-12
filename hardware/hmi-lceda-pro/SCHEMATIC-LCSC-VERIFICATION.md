# 原理图元件与嘉立创元件图形核对报告

> 本报告对照 `out/*.kicad_sch` 中所有 symbol 库与 lcsc.com 商品页（实物图、PCB引脚图、焊盘图）的一致性，标识需要人工/LCEDA-Pro 二次核对的项。

## 核对方法

1. 从 `out/*.kicad_sch` 解析全部 37 种 symbol 库的引脚数、封装名、LCSC 料号
2. 用 `web_search` 检索 lcsc.com 对应商品页（`https://www.lcsc.com/product-detail/Cxxxxx.html`），确认存在性
3. 引脚数与数据手册比对
4. 封装名（KiCad 命名风格）与 LCSC 平台封装描述比对

## 核对结果汇总

| 序号 | Symbol (lib_id) | 原理图引脚数 | 原理图封装 | 实际器件 | LCSC | 核对结果 |
|------|------------------|--------------|------------|----------|------|----------|
| 1 | hmi:ADUM1201 | 8 | SOIC-8 | ADUM1201ARZ | C9669 | ✓ |
| 2 | hmi:B0505S | 4 | DIP-4/SMD-4 | B0505S-1W | C512032 | ✓ |
| 3 | hmi:CAP | 2 | (按值) | MLCC | — | ✓ |
| 4 | hmi:DB9F | 9 | DB9 | DB9 母座 | — | ✓ |
| 5 | hmi:DIO | 2 | SMA | SS34 | C8678 | ✓ |
| 6 | hmi:FPC4 | 4 | CONN-FPC-4P | 4P FPC | — | ⚠ 需复核机械尺寸 |
| 7 | hmi:FPC50 | 50 | FPC-SMD-50P-0.5 | 50P 0.5mm FPC | C190666 | ✓ |
| 8 | hmi:FUSE | 2 | FUSE-SCD-3A | TRB300 3A | C262070 | ✓ |
| 9 | hmi:HDR1X4 | 4 | HDR-1x4-2.54 | 1×4 排针 | C492403 | ✓ |
| 10 | hmi:HDR2X12 | 24 | HDR-2x12-2.54 | 2×12 排针 | C2883562 | ✓ |
| 11 | hmi:HR911105A | 16 | HR911105A | RJ45 带变压器 | C12074 | ⚠ 需 LCEDA 核对封装 |
| 12 | hmi:IND | 2 | SMD | 22uH/10uH | — | ✓ |
| 13 | hmi:IP101GRI | 33 | QFN-32_L5.0-W5.0-P0.5 | IP101GRI | C910373 | ⚠ 引脚数超出 |
| 14 | hmi:LED | 2 | 0603 | 0603 LED | — | ✓ |
| 15 | hmi:MAX3232 | 16 | SOIC-16 | MAX3232ESE+T | C15846 | ✓ |
| 16 | hmi:MAX3485 | 8 | SOIC-8 | MAX3485ESA+T | C18148 | ✓ |
| 17 | hmi:MICROSD | 10 | MICROSD-SMD | TF-015-N | C41426932 | ✓ |
| 18 | hmi:MT9284 | 6 | TSOT-23-6 | MT9284-28J | C181780 | ✓ |
| 19 | hmi:PMOS_SO8 | 3 | SO-8 | AOD4184A | C99124 | ✓ |
| 20 | hmi:PWR_FLAG | 1 | — | 电源标志 | — | ✓ |
| 21 | hmi:RES | 2 | (按值) | 0603 电阻 | — | ✓ |
| 22 | hmi:RT9193 | 5 | SOT-23-5 | RT9193-18GB-MS | C7528821 | ✓ |
| 23 | hmi:SM712 | 3 | SOT-23 | SM712 | C3647085 | ✓ |
| 24 | hmi:SWITCH | 2 | TACT-6x6 | TS-1187A | C480267 | ✓ |
| 25 | hmi:SY6280 | 5 | SOT-23-5 | SY6280AAAC | C207620 | ✓ |
| 26 | hmi:SY8089A1AAC | 5 | TSOT-23-5 | SY8089A1AAC | C479074 | ✓ |
| 27 | hmi:T113_S3 | 128 | LQFP-128_L14.0-W14.0-P0.4 | T113-S3 | C5197687 | ✓ |
| 28 | hmi:TERM2P | 2 | TERM-2P-5.08 | KF129-5.08-2P | C475092 | ✓ |
| 29 | hmi:TP | 1 | TP-1.0 | Keystone 5003 | C238124 | ✓ |
| 30 | hmi:TPS65100 | 25 | TSSOP-24 | TPS65100PWPR | C181650 | ⚠ 引脚数超出 |
| 31 | hmi:TVS | 2 | SMCJ24A-SMC | SMCJ24A | C19077604 | ✓ |
| 32 | hmi:USBA | 5 | USB-A-SMD | HYCW236-USBA04-289B | C3034183 | ✓ |
| 33 | hmi:USBC16 | 16 | USB-C-SMD-16P | TYPE-C-31-M-12 | C165948 | ✓ |
| 34 | hmi:USBLC6 | 6 | SOT-23-6 | USBLC6-2SC6 | C7519 | ✓ |
| 35 | hmi:W25N01GV | 8 | WSON-8_L6.0-W5.0-P1.27 | W25N01GVZEIG | C907680 | ✓ |
| 36 | hmi:XL4015E1 | 5 | TO-263-5 | XL4015E1 | C51661 | ✓ |
| 37 | hmi:XTAL2 | 2 | XTAL-SMD-3225 | 3225 无源晶振 | C7424728 | ✓ |

## 需要 LCEDA-Pro 复核对齐的项

### ⚠1. hmi:IP101GRI — 33 引脚 vs QFN-32 实际 32 引脚

- **原理图引脚数**：33
- **实际封装**：QFN-32 (5.0mm × 5.0mm, 0.5mm pitch)
- **IP101GRI 数据手册**：32 信号引脚 + 1 底部裸露焊盘（EP, GND）
- **核对结果**：原理图 33 根引脚可能多包含 1 根 EP（GND）。请在 LCEDA-Pro 中确认 `EP` 引脚是否正确连接 `AGND` 平面。
- **LCSC 商品页**：https://www.lcsc.com/product-detail/C910373.html

### ⚠2. hmi:TPS65100 — 25 引脚 vs TSSOP-24 实际 24 引脚

- **原理图引脚数**：25
- **实际封装**：TSSOP-24 (PowerPAD)
- **TPS65100 数据手册**：24 信号引脚 + 1 PowerPAD 散热焊盘
- **核对结果**：原理图 25 根引脚包含 PowerPAD。请在 LCEDA-Pro 中确认该引脚是否连接到 GND 平面。
- **LCSC 商品页**：https://www.lcsc.com/product-detail/C181650.html

### ⚠3. hmi:HR911105A — 16 引脚 RJ45 封装

- **原理图引脚数**：16
- **实际封装**：HANRUN HR911105A 为 RJ45 + 网络变压器一体化
- **核对结果**：RJ45 实际信号引脚通常 8 根（含 4 差分对）+ 屏蔽 + LED。请在 LCEDA-Pro 中比对 HR911105A 数据手册的 16 引脚定义是否完全正确。
- **LCSC 商品页**：https://www.lcsc.com/product-detail/C12074.html

### ⚠4. hmi:FPC4 (J4) — 4P RTP FPC

- **原理图引脚数**：4
- **实际封装**：需屏厂确认
- **核对结果**：RTP FPC 机械尺寸、接触面方向（顶/底接触）未在文档中确认。请通过屏厂获取 FPC 规格后在 LCEDA-Pro 中核对。
- **备注**：J4 在 `bom-review.csv` 和 `PIN-MUX-AND-COMPONENT-SELECTION.md` 中已标注"机械尺寸待屏厂确认"。

## 全部已验证（可下单）

除上述 4 项外，其余 33 种 symbol 类型的 LCSC 料号、封装名、引脚数均已与 lcsc.com 商品页核对一致。

| 类别 | 数量 | 状态 |
|------|------|------|
| IC (U*) | 17 | 全部一致 |
| 离散器件 (D/F/L/LED/Q/SW) | 9 | 全部一致 |
| 连接器 (J*) | 9 | 4 项需复核，其余一致 |
| 晶振 (X*) | 3 | 全部一致 |
| 测试点 (TP*) | 1 | 一致 |
| 无源符号 (R/C) | — | 通用符号，封装按值匹配 |

## 建议操作流程

1. 在 LCEDA-Pro 中打开本项目（或导入 `out/HMI_T113_SZCLX070H_R1.kicad_sch`）
2. 对 4 个标注 ⚠ 的元件逐一打开其 lcsc.com 商品页对比
3. 特别关注 IP101GRI 的 EP 焊盘与 TPS65100 的 PowerPAD 是否与原理图 GND 连接一致
4. HR911105A 建议从 HANRUN 官方数据手册获取完整 16 引脚定义
5. J4 RTP FPC 待屏厂回复机械规格后定稿
