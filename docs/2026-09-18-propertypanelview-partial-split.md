# PropertyPanelView 按域拆 partial（2026-09-18，P0-2）

## 1. 背景

`src/HmiEditor.App/Views/PropertyPanelView.axaml.cs` 长期是单文件 **2804 行**：

| 指标 | 拆分前 |
|---|---|
| 行数 | 2804 |
| 顶层成员数 | ~110 个方法 + 字段/事件 |
| partial 分部 | 1（仅 `.axaml.cs` 自身） |
| `#region` | 0 |

它同时承载屏幕属性、状态化样式、值显示配置、控件功能参数、图片/SVG 资源选择、
视觉状态编辑、变量绑定/表达式、动作管理、Faceplate 参数、属性变更写回、颜色选择器、
AI 布局建议 —— 12 个域挤在一个文件里，任何改动都要在 2800 行里定位。
09-14 审查（`docs/2026-09-14-optimization-directions.md` §3-B）点名它是"热点文件"，
但从未排期；MainWindow / DesignCanvas / CodeGenerator 均已治理完毕，它是最后一个
未治理的巨型单类。

**为什么现在能安全拆**：本项目在 09-13 已补齐 headless UI 测试网 ——
`PropertyPanelWiringTests`（70 个编辑器逐个驱动）等 230 项 App.UiTests 编码了属性面板的
真实行为；拆分类时它们就是安全网（与 DesignCanvas 拆分的先决条件相同）。

## 2. 拆分结果

| 文件 | 行数 | 原区间（1 基） | 职责 |
|---|---:|---:|---|
| `PropertyPanelView.axaml.cs` | 277 | 25–276 | 字段/事件/构造/公共入口（`Set*`、`BindControl`、`BindScreen` 之前的头） |
| `PropertyPanelView.Screen.cs` | 285 | 278–537 | 屏幕页面属性（预设/图层引用/背景/保存） |
| `PropertyPanelView.Display.cs` | 103 | 539–616 | 状态化样式入口 + 值显示配置 |
| `PropertyPanelView.Parameters.cs` | 279 | 618–871 | 控件功能参数（按类型显隐/重置/表格单元格） |
| `PropertyPanelView.Assets.cs` | 227 | 873–1074 | 图片 / SVG 资源选择 + 屏幕背景图片 |
| `PropertyPanelView.VisualStates.cs` | 354 | 1076–1404 | 对齐/字体/视觉状态图/状态保存 |
| `PropertyPanelView.Binding.cs` | 373 | 1406–1753 | 变量绑定 + I1 表达式绑定 + 联动规则 |
| `PropertyPanelView.Actions.cs` | 358 | 1755–2087 | 动作管理（chips/类型/目标/保存） |
| `PropertyPanelView.Faceplate.cs` | 364 | 2089–2427 | Faceplate 参数动态编辑器 |
| `PropertyPanelView.Changes.cs` | 291 | 2429–2694 | 属性变更写回（`ValidateAndWrite`/`SaveProperties`/颜色） |
| `PropertyPanelView.Ai.cs` | 132 | 2696–2804 | AI 布局建议（T4.4）+ 文件尾 `ActionChipModel` |

拆分后最大文件 **373 行**（原 2804），单文件认知负荷降到约 1/7.5。

**分域依据**：全部切分点落在原文的**段注释**（`// ── 屏幕页面属性──` 等 12 处）
所在行，物理区间连续、不改动任何成员顺序；`ActionChipModel`（文件尾独立类）随
最后一个分部 `Ai.cs` 保留在文件尾。

## 3. 拆分的机械保证（零丢失验证）

`tools/split-propertypanelview.py` 负责切分并自证零丢失，不靠人眼：

1. **边界行内容断言**：每个分段的段首/段末行必须与脚本里登记的原文逐字相等
   （防 off-by-one，任何边界漂移立即失败并指名期望/实际）；
2. **段间空行检查**：相邻两段之间必须恰好 1 个空行且为空；
3. **零丢失**：把各段 body 按序拼接，必须与原文类体（去除段间空行后）**逐行相等**，
   任何漏行/重行/错序都失败并给出首处差异。

运行输出：

```
边界断言: 全部通过 ✓
段间空行: 全部恰好 1 行且为空 ✓
OK  PropertyPanelView.axaml.cs      body  252 行 == 原 25-276
OK  PropertyPanelView.Screen.cs     body  260 行 == 原 278-537
...
OK  PropertyPanelView.Ai.cs         body  101 行 == 原 2696-2796
结论: 零丢失 ✓（类体各段逐行一致，去除段间空行）
```

## 4. 验证结果

| 验证项 | 结果 |
|---|---|
| `dotnet build HmiEditor.slnx -c Release --no-incremental` | **0 警告 0 错误** |
| 全量测试（7 程序集） | **2720/2720**（Core 1925、AI 315、App.UiTests 230、Device 87、Communication 75、Simulator 44、Hardware 44；基线 2716 ⇒ **+4**，来自行数守卫扩展） |
| App.UiTests（含 PropertyPanelWiringTests） | 230/230 全绿（行为网兜底） |

> 测试数 +4 是行数守卫自身的扩展（`SingleFileSizeGuardTests` 由 3 条基线改为
> 2 条相等 + 1 条上限 + 1 条自检），不是产品行为变化。

## 5. 后续

- **P0-3**：`SimulatorProjectExporter`（1540 行）同套路拆 partial —— 已有测试网
  （`SimulatorProjectExporterTests` 21 条）在手，脚本可复用。
- 行数守卫（`SingleFileSizeGuardTests`）已按"拆前/拆后"两类判据升级：
  单个巨型单类（MainWindow / SimulatorProjectExporter）走**相等判定**；
  已拆类（PropertyPanelView.*）走**分部上限**（500 行），防合并回巨型单类。
