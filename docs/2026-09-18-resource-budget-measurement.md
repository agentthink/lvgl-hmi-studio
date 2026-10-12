# 资源预算「实测 vs 预算」—— 登记表 §4 第 29 项

> 日期：2026-09-18 ｜ 关联：`docs/2026-09-17-open-items-register.md` §4 第 29 项
> （"`ResourceConstraints` 只有声明值，无『实测 vs 预算』判据"）

---

## 0. 结论

给资源预算补上**真实读数**后，第一次比对就推翻了 T113 目标里写了很久的一个声明值：
**Flash 预算写成 16 MiB，而板上是 128 MiB 的 SPI NAND、真实整包 54.8 MiB**。
同时发现编辑器仪表盘根本没用目标的预算，而是写死 `8MB/64MB` 两个"默认值"。

本轮交付：① 修正声明值并用硬件证据锁住；② 仪表盘改用目标契约的预算；
③ 新增实测工具 `tools/BudgetCheck`（整包 vs 预算、UDISK 剩余 vs UI 产物）；
④ 判读规则收敛到 `ResourceBudget` 一处（阈值与仪表盘旧配色同源）。

## 1. 实测读数（2026-09-18，本机 SDK 构建产物）

```
$ dotnet run --project tools/BudgetCheck -- --so .dbg/libhmi_ui_deployed.so
== 资源预算实测（allwinner-t113）==
[①] .../firmware/Tina-SDK/out/t113-mq_r/tina_t113-mq_r_uart1.img
     实测 54.8 MiB vs 预算 128.0 MiB ⇒ 42.8% [Ok]
[②] 分区表 sys_partition.fex: mbr 8.0 MiB + 固定分区 90.9 MiB ⇒ UDISK 剩余 29.1 MiB
     UI 部署（libhmi_ui_deployed.so 21.6 KiB）= 21.6 KiB / UDISK 29.1 MiB ⇒ 0.1% [Ok]
```

| 轴 | 实测 | 预算（声明值） | 占比 | 判读 |
| --- | --- | --- | --- | --- |
| t113-mq_r 整包（Tina-SDK） | 57,490,432 B = 54.8 MiB | 128 MiB | 42.8% | Ok |
| t113-mq_r 整包（Tina-Linux 那次构建） | 60.8 MiB | 128 MiB | 47.5% | Ok |
| t113-bingpi_m2 整包（WavePI-S6） | 34.3 ~ 38.2 MiB | 128 MiB | 27 ~ 30% | Ok |
| `libhmi_ui.so` + UDISK 剩余（mq_r） | 21.6 KiB / 29.1 MiB | — | 0.1% | Ok |

> 两个 SDK 副本的 UDISK 剩余不同（29.1 MiB vs 21.2 MiB），因为 Tina-Linux 那次多切了
> 一个 16,128 扇区（≈7.9 MiB）的 `recovery` 分区。工具都按**实际分区表**算，并打印所用路径。

## 2. 被推翻的声明：16 MiB → 128 MiB

`AllwinnerT113Target.Resources.FlashSizeBytes` 原为 `16 * 1024 * 1024`，注释写"16MB SPI Flash"；
同族错值还抄进了 `firmware/t113/README.md`（"16MB SPI NAND"）与
`firmware/t113/docs/version-info.md`（"与编辑器对齐 … 16MB Flash"）。

三条独立证据（写进 `AllwinnerT113Target` 的注释，可逐条复核）：

1. **硬件型号**：`hardware/hmi-control-board-t113/lceda-import/hmi-board.csv` U8 = `W25N01GVZEIG`
   —— 1 Gbit SPI NAND = **128 MiB**；板级 `hardware/hmi-control-board-t113/README.md` 写明
   "SPI NAND 128MB，启动盘"。
2. **真实整包体积**：`out/t113-mq_r/tina_t113-mq_r_uart1.img` = 57,490,432 B（54.8 MiB）
   —— 16 MiB 根本装不下，128 MiB 装得下。
3. **分区表**：`device/config/chips/t113/configs/mq_r/sys_partition.fex` 固定分区合计
   186,178 扇区（≈90.9 MiB）+ mbr 16,384 扇区，剩余即 UDISK。

## 3. 判据清单

| 判据 | 位置 | 会不会红 |
| --- | --- | --- |
| 声明 Flash == BOM/README 的容量文本（型号 `W25N01GVZEIG`、`SPI NAND 128MB`），且两侧固件 README 不再抄别的数 | `tests/Hmi.Hardware.Tests/ResourceBudgetTests.cs` | 改常量/改文档任一侧即红 |
| 判读阈值语义：<70% Ok、≥70% Elevated、≥90% NearLimit、>100% Over、恰好 100% 仍算装得下 | 同上（Theory 7 组） | 改阈值即红 |
| 未声明预算（budget ≤ 0）判为 `Unknown`，不是"通过" | 同上 | 把 Unknown 改成 Ok 即红 |
| 分区表解析：mbr/固定分区/剩余分区（`size = 0`）、注释与缺字段容忍、表溢出时剩余钳到 0 | `tests/HmiEditor.Device.Tests/FirmwarePartitionTableTests.cs` | — |
| 仪表盘预算来自目标契约、不得写死分母 | `tests/HmiEditor.App.UiTests/ResourceDashboardWiringTests.cs` | 写回 `8 * 1024 * 1024` 即红 |
| 真实体积对比 | `tools/BudgetCheck`（`scripts/resource-budget.sh`） | 超预算退出码 1 |

## 4. 负向验证

| NV | 注入 | 命中 |
| --- | --- | --- |
| A | `RockchipRK3506Target` 不动，把 `AllwinnerT113Target.FlashSizeBytes` 改回 `16L * 1024 * 1024` | `T113DeclaredFlash_MatchesTheBoardBomAndReadme`（容量文本 128 ≠ 16） |
| B | 把 `README.md` 的 `SPI NAND 128MB` 改成 `SPI NAND 64MB` | 同上（取到的文本值 64 ≠ 常量 128） |
| C | 把 `ResourceBudget.NearLimitPercent` 从 90 改成 95 | `Evaluate_UsesTheSharedThresholds` 的 `900 → NearLimit` 那组 |
| D | 把仪表盘分母写回 `8 * 1024 * 1024L` | `Dashboard_TakesTheBudgetFromTheHardwareTarget_NotFromHardcodedTotals` |
| E | 把 `FirmwarePartitionTable` 的剩余钳位 `Math.Max(0, …)` 去掉 | `Parse_ClampsRemainderToZero_WhenTableOverflowsTheChip` |

## 5. 还没做的（如实登记）

- **RAM 预算实测**：`RamSizeBytes = 128 MiB` 仍是声明值（DDR3 容量）。真实读数要设备侧
  跑起来后读 `free`/`/proc/meminfo` 与 LVGL 堆峰值，本机没有在跑的板子 ⇒ 未实测。
- **RK3506 的 8 MiB**：仓库里没有该板固件镜像、原理图或 BOM，无法像 T113 一样三条证据钉住。
  已在 `RockchipRK3506Target` 注释里明确标注"未经实测核对"，不当作已验证预算使用。
- **估算器校准**：`ResourceEstimator` 的每控件系数与实测 `.so` 体积尚未做过偏差标定
  （ADR FF3 的目标是偏差 < 15%）。本轮先把"实测"这条路修通；系数校准需要多组工程语料，
  属独立议题。
- **CI 常态化**：Tina SDK（几十 GB）不入库，CI 上没有整包可测 ⇒ 实测由
  `scripts/resource-budget.sh` 在有产物的机器上跑；门禁里常驻的是上面的文本/语义判据。
