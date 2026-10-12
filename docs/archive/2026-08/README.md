# 归档：根目录一次性产物（2026-08）

这里存放 2026-08 期间直接写在仓库根目录、后被 `docs/2026-09-13-project-review-and-optimization.md` §5 P2-3
判定为「临时产物」的文件。2026-09-14 统一归档到此目录，**内容未做任何修改**，仅移动位置。

归档理由：根目录只应保留工程入口文件（`HmiEditor.slnx`、`.gitignore`、`.gitattributes`、`README.md`）。
这些文件都是**一次性任务的产出**，其结论已被 `docs/` 下的正式文档吸收或取代。

## 诊断 / 排查报告

| 文件 | 主题 | 状态 |
|---|---|---|
| `overview.md` | 实为 T30 Slider/Bar 设计器-模拟器修复记录（文件名名不副实） | 已落地 |
| `hmi-design-runtime-diagnosis.md` | 设计页 vs 运行页的根因诊断 | 部分被 `hmi-design-simulator-fix-plan.md` 取代 |
| `hmi-design-simulator-fix-plan.md` | 设计-运行不一致修复计划 T1–T11 | 基本落地（T5/T8 待办） |
| `simulator-stretch-mode-diagnosis.md` | 模拟器画布/缩放诊断 | 已落地 |
| `designer-hardcode-audit.md` | 设计器硬编码与接口暴露审计（约 120+ 处） | 部分整改，P0–P2 仍未全做 |
| `debug-dock-blank-after-drag.md` | Dock 拖拽后工具箱空白调试 | **OPEN，未解决** |

## 验证截图（对应上述报告的实测证据）

`sim-slider-bar-verify.png`、`sim-slider-ticks-verify.png`、`sim-stretch-1024x600.png`、
`sim-stretch-1280x800.png`、`sim-stretch-800x480.png`、`sim-table-bg-verify.png`、
`sim-transform-mirror-verify.png`、`sim-transform-rotate-verify.png`

> 引用关系：`overview.md` 以同名相对路径引用 `sim-slider-bar-verify.png`，
> 两者同在本目录，相对路径保持不变。

## 硬件调试脚本与接线图

| 文件 | 说明 |
|---|---|
| `rs485_diag.py` | RS485 链路诊断脚本 |
| `rs485_test.py` | RS485 收发测试脚本 |
| `rs485_pinout.png` | RS485 接线图 |
| `inspect_dock.csx` | Dock 面板结构检查脚本（dotnet-script） |

## 注意事项

- 这些文件**不再维护**，仅作考古与证据追溯使用。
- 当前项目状态请看 `docs/project-status.md`（状态基线，数字必须来自实测）。
- 最新一轮优化方向见 `docs/2026-09-14-optimization-directions.md`。
