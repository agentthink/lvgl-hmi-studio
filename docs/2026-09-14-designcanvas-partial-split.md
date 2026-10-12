# DesignCanvas 按域拆 partial（2026-09-14）

## 1. 背景

`src/HmiEditor.App/Controls/DesignCanvas.cs` 长期是单文件 **4163 行**、**零 partial、零 region**：

| 指标 | 拆分前 |
|---|---|
| 行数 | 4163 |
| 顶层成员数 | 336（class 内）+ 4（文件尾事件参数类） |
| partial 分部 | 0 |
| `#region` | 0 |

它同时承载了依赖属性、渲染主循环、25 个控件渲染器、指针交互、选择、拖放、控件创建、剪贴板、对齐、辅助线 —— 任何一次改动都要在 4000 行里定位。

**为什么拖了这么久才动**：拆文件本身是纯机械操作，但一旦拆错，编译器只能发现"符号丢失"，发现不了"渲染语义变了"。所以在动手前先补齐了行为测试网：

- `ArcControlRenderTests`（3 项，真实绘制操作 + 象限判定）
- `GaugeTickRenderTests`（8 项）
- `CanvasRenderBaselineTests`（含每帧分配回归门禁）
- `RenderResourceCacheTests`（缓存复用 + R3 寿命陷阱守卫）
- `PropertyPanelWiringTests`（70 个编辑器逐个驱动）

这些测试现在**编码了正确的渲染语义**，拆分类时它们就是安全网。

## 2. 拆分结果

| 文件 | 行数 | 原区间 | 职责 |
|---|---:|---|---|
| `DesignCanvas.cs` | 957 | 1–890 + 4098–4163 | 依赖属性/字段/事件、构造与 `OnPropertyChanged`、`Render` 主循环、控件分发（`RenderControl`/`RenderControlCore`）、颜色解析、文件尾事件参数类 |
| `DesignCanvas.Renderers.cs` | 984 | 891–1849 | Button/Switch/CheckBox/Image/Dropdown/Gauge/Knob/Led/Spinner/Line/Rectangle/Ellipse/Polyline/Arc/Keyboard/TabView/Table + 文本对齐/圆弧几何 helper |
| `DesignCanvas.IndustrialRenderers.cs` | 374 | 1850–2199 | Svg / Tank / Pipe / Valve 系列 + `BuildShapeGeometry`/`BuildPolygonGeometry`/`ParseSvgColor` |
| `DesignCanvas.Input.cs` | 963 | 2200–3138 | 命中测试、`OnPointer*` 重写、框选、长按选中、选择状态、`SnapToGrid`、拖放（Toolbox/Faceplate/资源） |
| `DesignCanvas.Editing.cs` | 983 | 3139–4095 | `AddControl`/`AddControlAt`/`BuildControl`、绘制工具提交、坐标换算、剪贴板与变换、对齐分布、对齐辅助线 |

拆分后最大文件 **984 行**（原 4163），单文件认知负荷降到约 1/4。

**分域依据**：`IndustrialRenderers` 单独成域，是因为 Svg / Tank / Pipe / Valve 共用同一套 SVG path 几何解析（`BuildShapeGeometry` 等），拿掉它们会让 Renderers 域的依赖关系变模糊；Svg 与工业设备在"由矢量路径构成图形"这一点上同源。

## 3. 拆分点选取原则

切分点**全部落在两个成员之间的空行/注释处**，不在成员内部切断。每个切分点都逐行 dump 核对过：

| 切分点 | 上下文 |
|---:|---|
| 890 | `ThemeFallback` 表达式体结束后的空行 |
| 1849 | `DrawPointerTriangle` 结束后空行 → 1850 `// ── I6: SVG 矢量渲染 ──` |
| 2199 | `ParseSvgColor` 结束后空行 → 2200 `// ── 命中测试 ──` |
| 3138 | `HandleDrop` 结束后空行 → 3139 `AddControl` 的 `/// <summary>` |
| 4096/4097 | `class DesignCanvas` 闭括号 + 空行（各分部文件自带 `}`，此两行由生成器丢弃） |

格式细节：段末空行并入前段并去除，使 `}` 紧贴最后一名成员 —— 与原文件 4095/4096 的风格一致；`partial` 各文件头部保留完整 `using` 块（不改内容，保证 diff 可审计）。

## 4. 零丢失验证

拆完不能只靠"编译过了"。用脚本把每个新文件的 **body** 与原文件对应区间**逐行比对**（`git show HEAD:...` 取原文件）：

```
原文件 4163 行（HEAD）
OK  DesignCanvas.cs                        body 865 行 == 原 25-890
OK  DesignCanvas.Renderers.cs              body 958 行 == 原 891-1849
OK  DesignCanvas.IndustrialRenderers.cs    body 349 行 == 原 1850-2199
OK  DesignCanvas.Input.cs                  body 938 行 == 原 2200-3138
OK  DesignCanvas.Editing.cs                body 957 行 == 原 3139-4095
OK  （core 尾部）                            args 类 66 行 == 原 4098-4163
结论: 零丢失 ✓
```

即：5 个文件的正文与原文**逐字符一致**，唯一变化是各类文件头（`using` + `namespace` + 文档注释 + `public partial class`）与 `partial` 关键字。

## 5. 验证结果

| 验证项 | 结果 |
|---|---|
| `dotnet build -c Release` | **0 警告 0 错误** |
| `HmiEditor.Core.Tests` | 1309 项 |
| `HmiEditor.AI.Tests` | 237 项 |
| `HmiEditor.App.UiTests` | **91 项全绿**（渲染行为测试网全过） |
| `Hmi.Communication.Tests` | 75 项 |
| `HmiEditor.Device.Tests` | 63 项 |
| `HmiEditor.Simulator.Tests` | 41 项 |
| `Hmi.Hardware.Tests` | 22 项 |
| **合计** | **1838 项** |

> 全量首跑 `Core.Tests` 出现 2 项失败（`DataLoggerTests.Logger_StartStop_UpdatesSampleCount`、`AlarmEngineTests.Engine_AlarmTriggeredEvent_Fires`），**单独重跑两项均通过** → 时序 flaky，与本改动无关（均在 Core 层，不引用 App）。

## 6. 后续

- **P1-4**：给 `SimulatorProjectExporter`（1312 行、596 处 `AppendLine`）补专门测试类 —— 这是抽 IR 统一发射器的前置纪律，与本次同类：先有行为网，再动刀。
- 本次只做结构搬家，**未改任何渲染/交互语义**；`RenderResourceCache`、`ArcLayout` 等既有约定不受影响。
