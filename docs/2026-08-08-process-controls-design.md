# 过程图形控件实现方案：Tank / Pipe / Valve

> **日期**: 2026-08-08
> **依据**: [2026-08-08-hmi-control-research-comparison.md](./2026-08-08-hmi-control-research-comparison.md) P0 建议
> **范围**: 三个过程行业专用控件的**数据结构、渲染逻辑、交互方式**及全链路集成
> **遵循现有架构**: 扁平属性按类型取用 + ControlMappingRegistry 映射 + GenerateControlParameters 分派 + DesignCanvas 专用渲染器

---

## 一、总体设计决策

### 1.1 控件类型

在 [HmiControlType.cs](file:///home/chen/Git/zhishu-hmi/src/HmiEditor.Core/Models/HmiControlType.cs) 末尾追加（I7 过程图形区段，对齐现有 Wave2/I6 注释风格）：

```csharp
// I7: 过程图形控件
Tank,    // 罐体（垂直液位填充 + 刻度）
Pipe,    // 管道（线缆 + 流向动画）
Valve,   // 阀门（状态色切换，可交互）
```

### 1.2 属性策略（沿用扁平模型，不新增子模型）

遵循 `HmiControl` 现有"功能参数按类型取用"约定，**最大化复用已有属性**，仅新增 4 个类型专属字段：

| 属性 | 类型 | 复用/新增 | 适用控件 | 说明 |
|---|---|---|---|---|
| `MinValue` / `MaxValue` | double | 复用 | Tank | 液位量程（默认 0..100） |
| `Value` | int | 复用 | Tank/Valve | 编辑态默认液位/开关值 |
| `BgColor` | string | 复用 | 三者 | Tank 液体色 / Pipe 线色 / Valve 阀体色 |
| `IndicatorColor` | string | 复用 | Tank/Valve | Tank 液面高亮 / Valve 开状态色 |
| `TextColor` | string | 复用 | Tank | 刻度文字颜色 |
| `BorderWidth` | int | 复用 | Tank/Valve | Tank 罐壁厚 / Valve 线宽 |
| `VariableBindings` | 集合 | 复用 | 三者 | 液位/状态数据绑定 |
| `Actions` | 集合 | 复用 | Valve | 点击切换开关（写变量） |
| **`TankShowScale`** | bool | **新增** | Tank | 是否显示刻度（默认 true） |
| **`TankTickCount`** | int | **新增** | Tank | 主刻度数（默认 5，2-10） |
| **`PipeDirection`** | string | **新增** | Pipe | `"Horizontal"` / `"Vertical"`（默认 Horizontal） |
| **`PipeFlowDirection`** | string | **新增** | Pipe | `"Forward"` / `"Reverse"`（默认 Forward） |
| **`PipeFlowSpeed`** | int | **新增** | Pipe | 流向动画周期 ms（默认 2000，0=静态无动画） |
| **`ValveType`** | string | **新增** | Valve | `"Ball"` / `"Gate"` / `"Butterfly"` / `"Globe"`（默认 Ball） |

新增字段置于 `HmiControl.cs` 的"控件功能参数"区段末尾，均带默认值保证旧项目 JSON 反序列化兼容（与现有 `LineCap`/`KeyboardMode` 等 nullable/默认值模式一致）。

---

## 二、数据结构

### 2.1 模型扩展（HmiControl.cs 新增字段）

```csharp
// ── I7: 过程图形控件参数（Tank/Pipe/Valve 按类型取用，JSON 默认值向后兼容）──

/// <summary>是否显示液位刻度（Tank）。默认 true。</summary>
public bool TankShowScale { get; set; } = true;

/// <summary>主刻度数量（Tank，2-10）。默认 5。</summary>
public int TankTickCount { get; set; } = 5;

/// <summary>管道方向（Pipe：Horizontal/Vertical）。默认 Horizontal。</summary>
public string? PipeDirection { get; set; }

/// <summary>流向（Pipe：Forward/Reverse）。默认 Forward。</summary>
public string? PipeFlowDirection { get; set; }

/// <summary>流向动画周期 ms（Pipe，0=静态无动画）。默认 2000。</summary>
public int PipeFlowSpeed { get; set; } = 2000;

/// <summary>阀门类型（Valve：Ball/Gate/Butterfly/Globe）。默认 Ball。</summary>
public string? ValveType { get; set; }
```

### 2.2 序列化兼容

`ProjectSerializer` 无需改动——新增字段带默认值，旧项目 JSON 缺失时反序列化自动回退默认。验证：新增 `ProcessControlSerializationTests`，断言"旧 JSON（无新字段）→ 反序列化 → 默认值正确"。

---

## 三、渲染逻辑

### 3.1 设计器渲染（Avalonia / DesignCanvas.cs）

在 [DesignCanvas.cs](file:///home/chen/Git/zhishu-hmi/src/HmiEditor.App/Controls/DesignCanvas.cs) 的 `RenderControl` switch 中追加 3 个 case（对齐现有 Wave2 模式）：

```csharp
case HmiControlType.Tank:  RenderTank(context, control, rect, z);  return;
case HmiControlType.Pipe:  RenderPipe(context, control, rect, z);  return;
case HmiControlType.Valve: RenderValve(context, control, rect, z); return;
```

#### 3.1.1 RenderTank（罐体 + 液位填充）

```
结构（自外向内）:
┌─────────────────┐  ← 罐壁：矩形边框（BorderWidth 复用），Radius=2
│  ╷              │
│  │  液体区域     │  ← 液位填充：从底部向上，高度 = 归一化液位 × (rect.Height - 边距)
│  ▼              │
└─────────────────┘  ← 底部微收窄（可选，用 2 条斜线模拟罐底）
 刻度（TankShowScale=true 时）: 右侧等分 TankTickCount 条短刻度线 + 百分比标签
```

渲染要点：
- **液位来源**：`IsPreviewMode` → `PreviewValueResolver.ResolveProgress(control, PreviewValueProvider, MinValue, MaxValue)`；编辑态 → `(Value - MinValue) / (MaxValue - MinValue)`（与 RenderGauge 完全同模式）。
- **液体颜色**：`IndicatorColor ?? BgColor`，默认 `#38bdf8`（天蓝）；液面用半透明渐变提升质感。
- **刻度**：`TankTickCount` 条短横线（TextColor），右侧附百分比文本（`F0` 整数）。

#### 3.1.2 RenderPipe（管道 + 流向）

```
Horizontal:  ──────▶──────   Vertical:  │  ▲  │
渲染：主线段（BgColor，线宽 BorderWidth 默认 4）沿控件主轴贯穿
     + 流向箭头（PipeFlowDirection=Forward 时 ▶ / ▼，Reverse 反向）
     + 流动虚线段（PipeFlowSpeed>0 时：沿主轴分布的短粗线，预览态静态示意）
```

渲染要点：
- `PipeDirection == "Vertical"` 时主轴为 Y，箭头方向对应 `PipeFlowDirection`。
- 预览态箭头位置固定（静态示意），**动态流动由运行时 LVGL 动画实现**（见 3.2.2）。
- 端点样式（端帽）：默认 flat，未来可复用 `LineCap`。

#### 3.1.3 RenderValve（阀门符号 + 状态色）

| ValveType | 符号示意 |
|---|---|
| Ball | ◯ 圆 + 垂直手柄杆（开=手柄与管道垂直，关=平行） |
| Gate | ▭ 矩形闸板（开=抬起，关=落下） |
| Butterfly | ◉ 圆盘 + 中心轴（开=圆盘与管道平行，关=垂直） |
| Globe | ◌ 圆 + 斜杆（开=杆沿管道，关=斜 45°） |

渲染要点：
- **状态来源**：`IsPreviewMode` → `PreviewValueResolver.ResolveDisplayText(...)` 返回 `"ON"`/`"OFF"` 判定；编辑态 → `Value != 0`。
- **开状态色**：`IndicatorColor ?? "#22c55e"`（绿）；**关状态色**：`BgColor ?? "#ef4444"`（红）。
- **线宽**：复用 `BorderWidth`（默认 3）。
- 附加文本：默认显示 `Text ?? Name`（阀门位号，如 "V-101"）。

### 3.2 运行时渲染（LVGL v9 / CodeGenerator.cs）

LVGL v9 **无原生 Tank/Pipe/Valve 控件**，采用 **`lv_canvas` 组合方案**（与现有 Svg 控件同路径，复用 [SvgToLvglConverter](file:///home/chen/Git/zhishu-hmi/src/HmiEditor.CodeGen/SvgToLvglConverter.cs) 的像素缓冲声明模式）。

#### 3.2.1 映射注册（ControlMappingRegistry.cs）

```csharp
[HmiControlType.Tank]  = "lv_canvas_create",  // 实际走 I7 专用生成分支
[HmiControlType.Pipe]  = "lv_canvas_create",
[HmiControlType.Valve] = "lv_canvas_create",
```

#### 3.2.2 生成代码策略（CodeGenerator.cs）

在 `GenerateScreenCode` 循环中追加 I7 分支（与现有 Svg 分支并列）：

```
Tank:
  1. lv_canvas_create(screen) + 像素缓冲声明（文件级 static，复用 GenerateBufferDeclaration 模式）
  2. lv_canvas_draw_rect：罐壁（边框色）+ 底部支撑
  3. 刻度：lv_canvas_draw_line × TankTickCount（右侧）或 lv_label 组
  4. 液位层：lv_canvas_draw_rect 按初始 Value 绘制液体区域
  5. 运行时更新：生成 update_tank_{id}(float value) 函数 —— 清空液位层 + 重绘，由
     hmi_runtime 的变量刷新 tick 调用（复用 alarm_evaluate 的 tick 机制，见 hmi_ui_api.h）

Pipe:
  1. lv_canvas 绘制主轴线段（BgColor，BorderWidth）
  2. 流动块：2-3 个短粗线段（IndicatorColor）沿主轴排列
  3. 动画：PipeFlowSpeed>0 时生成 lv_anim_t 驱动流动块 x/y 循环位移
     （lv_anim 无限循环 repeat_count=LV_ANIM_REPEAT_INFINITE，对齐现有动画生成器模式）
  4. PipeFlowDirection=Reverse 时位移方向取反

Valve:
  1. lv_canvas 绘制 ValveType 对应符号（静态形状，随状态切换颜色）
  2. 状态色：生成 update_valve_{id}(bool open) —— open 时符号+手柄用 IndicatorColor(绿)，
     关闭用 BgColor(红)
  3. 交互：生成事件回调 on_valve_{id}_clicked() —— 写绑定变量（Value 取反），
     复用现有 AppendEventRegistration 机制（Actions 集合配置）
```

**静态形状绘制序列**（canvas draw 调用）集中到新增 `ProcessControlCodeGen.cs`（对齐 `SvgToLvglConverter.cs` 的模块化模式），包含：
- `GenerateTankCode(HmiControl)` → 罐体 + 刻度 + 初始液位 + update 函数
- `GeneratePipeCode(HmiControl)` → 主线 + 流动块 + 动画
- `GenerateValveCode(HmiControl)` → 符号 + 状态 update + 交互回调

### 3.3 预览解析（PreviewValueResolver.cs）

| 方法 | 修改 |
|---|---|
| `IsDisplayControl` | 追加 `HmiControlType.Tank`、`HmiControlType.Valve`（Pipe 无绑定显示，不加） |
| `FormatValue` | Tank/Valve 归入布尔/进度类：`Tank => F1`（液位百分值），`Valve => ON/OFF` |
| `ResolveProgress` | Tank 直接复用（量程参数传入 MinValue/MaxValue）—— **无需改动** |

---

## 四、交互方式

### 4.1 控件添加

[ToolboxView.axaml.cs](file:///home/chen/Git/zhishu-hmi/src/HmiEditor.App/Views/ToolboxView.axaml.cs) `PopulateItems()` 追加 3 项（新分类 `TbCatProcess` "过程图形"）：

| 图标 | 名称 | 分类 |
|---|---|---|
| 🛢 | 罐体 (Tank) | 过程图形 |
| ═ | 管道 (Pipe) | 过程图形 |
| ⚙ | 阀门 (Valve) | 过程图形 |

[DesignCanvas.cs](file:///home/chen/Git/zhishu-hmi/src/HmiEditor.App/Controls/DesignCanvas.cs) `AddControl` 追加默认值：

```csharp
HmiControlType.Tank  => (140.0, 200.0),  // 默认竖罐
HmiControlType.Pipe  => (160.0, 8.0),    // 默认横管
HmiControlType.Valve => (60.0, 60.0),    // 方形符号区
```

默认 BgColor：Tank `#0ea5e9`（液体）、Pipe `#64748b`（管身）、Valve `#ef4444`（关状态红）。

### 4.2 属性面板（PropertyPanelView）

`LoadControlParameters` 按类型显示专属参数行（对齐现有 Slider/Gauge 的分段显示模式）：

| 控件 | 显示参数行 |
|---|---|
| Tank | MinValue、MaxValue、IndicatorColor（液体色）、TankShowScale（勾选）、TankTickCount |
| Pipe | PipeDirection（下拉：水平/垂直）、PipeFlowDirection（下拉：正向/反向）、PipeFlowSpeed（数值） |
| Valve | ValveType（下拉：球阀/闸阀/蝶阀/截止阀）、IndicatorColor（开状态色）、MinValue/MaxValue（0/1） |

- **Tank**：绑定为 ReadOnly（显示型）——`VariablePicker` 打开、`BindingMode` 固定 ReadOnly。
- **Valve**：绑定 ReadWrite + 动作列表预置 `ToggleBit`（默认 Target=主写变量），与现有 Switch 一致。
- **Pipe**：不显示绑定区（纯装饰动画）。

### 4.3 数据绑定与运行时更新

```
Tank 绑定 "tank1.level" (Float, ReadOnly)
   │
   ▼ RuntimeBindingEngine 标签表
   ▼ hmi_runtime tick → update_tank_tank1(value) → lv_canvas 液位重绘

Valve 绑定 "valve1.open" (Bool, ReadWrite) + 动作 ToggleBit(valve1.open)
   │
   ▼ 点击 → on_valve_valve1_clicked() → hmi_write(bit flip)
   ▼ 状态变化 → update_valve_valve1(open) → 符号颜色切换
```

### 4.4 键盘/触摸

- Tank/Pipe：无焦点（`LV_OBJ_FLAG_CLICKABLE` 清除），纯显示。
- Valve：可聚焦（点击切换），支持键盘空格触发（LVGL 默认焦点导航）。

---

## 五、涉及文件清单

| 文件 | 变更 |
|---|---|
| `src/HmiEditor.Core/Models/HmiControlType.cs` | +3 枚举 |
| `src/HmiEditor.Core/Models/HmiControl.cs` | +5 字段 |
| `src/HmiEditor.Core/Mapping/ControlMappingRegistry.cs` | +3 canvas 映射 |
| `src/HmiEditor.CodeGen/ProcessControlCodeGen.cs` | **新增**：Tank/Pipe/Valve 代码生成 |
| `src/HmiEditor.CodeGen/CodeGenerator.cs` | +I7 生成分支（对齐 Svg 分支） |
| `src/HmiEditor.App/Controls/DesignCanvas.cs` | +3 case、+3 渲染方法、AddControl 默认值 |
| `src/HmiEditor.App/Views/ToolboxView.axaml.cs` | +3 工具箱项 |
| `src/HmiEditor.App/Views/PropertyPanelView.axaml.cs` | +专属参数行 |
| `src/HmiEditor.App/Resources/Lang.cs` + `Strings.zh-CN.json`/`en-US.json` | +本地化键 |
| `src/HmiEditor.Core/Design/PreviewValueResolver.cs` | IsDisplayControl/FormatValue +2 类型 |
| `tests/HmiEditor.Core.Tests/ProcessControlTests.cs` | **新增**：模型默认值/序列化/CodeGen 断言 |

---

## 六、测试计划（对齐现有适应度函数风格）

| 测试 | 断言 |
|---|---|
| `ProcessControlDefaultsTests` | 新控件默认尺寸/字段默认值正确 |
| `ProcessControlSerializationTests` | 旧 JSON 反序列化 → 新字段取默认值（向后兼容） |
| `ProcessControlCodeGenTests` | Tank 生成 canvas + update 函数；Pipe 生成 lv_anim；Valve 生成 toggle 回调（引用 [TrendAndAlarmCodeGenTests](file:///home/chen/Git/zhishu-hmi/tests/HmiEditor.Core.Tests/TrendAndAlarmCodeGenTests.cs) 的断言风格） |
| `ProcessControlPreviewTests` | Tank 预览液位进度 = 绑定值归一化；Valve 预览 = ON/OFF |
| `ProcessControlValidatorTests` | 液位量程非法（Max<=Min）、TickCount 越界 → GeneratedCodeValidator 报错 |

---

## 七、实施顺序（每步可独立编译验证）

1. **模型层**：枚举 + 字段 + 序列化测试（`dotnet test HmiEditor.Core.Tests`）
2. **设计器**：DesignCanvas 3 渲染方法 + 工具箱/默认值 + 预览解析（可视化立即可见）
3. **属性面板**：专属参数行 + 本地化键（中英双语）
4. **代码生成**：ProcessControlCodeGen + CodeGenerator 分支 + 生成测试
5. **验证闭环**：GeneratedCodeValidator 扩展校验 + 模拟器工程导出验证
