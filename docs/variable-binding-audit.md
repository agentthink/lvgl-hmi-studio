# HMI 控件变量绑定审计报告

> 生成时间: 2026-08-01
> 范围: HmiEditor 全项目 — 变量绑定契约、控件兼容性矩阵、持久化、代码生成、预览仿真

## 1. 绑定模型概览

### 1.1 Producer / Consumer 架构（联动模型 R5）

```
┌─────────────────────────────────────────────────────────────┐
│  Producer: VariableManager                                  │
│  ├── Variables (用户变量, 按 Id 绑定, 兼容 Name)             │
│  ├── SystemVariables (系统变量, 按 Id 绑定)                  │
│  └── UdtDefinitions → UdtFieldVariable (按 "实例.字段" 绑定) │
└──────────────────────┬──────────────────────────────────────┘
                       │ VariableBindings: ObservableCollection<VariableBindingData>
                       │   ├── PropertyName (Text/Value/IsChecked)
                       │   ├── VariableId (Id / UDT compound id)
                       │   └── Direction (ReadOnly/WriteOnly/ReadWrite)
                       │ Actions: ObservableCollection<HmiAction>
                       ▼
┌─────────────────────────────────────────────────────────────┐
│  Consumer: HmiControl                                       │
│  ├── VariableBindings → 显示数据源 (PrimaryBindingVariable)  │
│  └── Actions          → 输入操作, 写动作 Target 联动至       │
│                         PrimaryWriteVariable (ApplyLinkage) │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 绑定标识格式

| 来源 | 格式 | 示例 | 设置方 | 稳定性 |
|------|------|------|--------|--------|
| 用户变量 | `Variable.Id` (M1 迁移) | `"v_temp"` | VariablePicker (Tag = Id) | ✅ 稳定 (改名不影响) |
| 系统变量 | `SystemVariable.Id` | `"sys.time.cur"` | VariablePicker (Tag = Id) | ✅ 稳定 |
| UDT 字段 | `实例名.字段名` | `"motor1.speed"` | VariablePicker (Tag = CompoundId) | ⚠️ 实例名可变 |

### 1.3 解析路径

`VariableManager.ResolveBinding(bindingId)` 处理逻辑:

1. 包含 `.` → UDT 字段访问: 按实例名查找 → 展开字段 → 返回 `UdtFieldVariable`
2. 无 `.` → 先按 `Variable.Id` 匹配, 再按 `Variable.Name` 匹配
3. 未匹配 → 返回 `null` (悬空绑定)

## 2. 控件类型绑定矩阵

### 2.1 直接绑定 (VariableBindingId)

`PropertyPanelView.LoadVariableBinding()` 控制哪些控件类型显示变量绑定面板:

| 控件类型 | 绑定面板 | 预览格式化 | 仿真信号 | 语义合理性 |
|----------|----------|-----------|----------|-----------|
| Label | ✅ | F2 (两位小数) | Sine | ✅ 文本显示 |
| Bar | ✅ | F1 + 进度归一化 | Sine | ✅ 进度条 |
| Gauge | ✅ | F1 + 进度归一化 | Sine | ✅ 仪表盘 |
| Knob | ✅ | F1 | Sine | ✅ 旋钮 |
| Led | ✅ | ON/OFF | Square | ✅ 指示灯 |
| Image | ✅ | F2 (fallthrough) | Sine | ❌ 图片无数值语义 |
| Line | ✅ | F2 (fallthrough) | Sine | ❌ 线条无数值语义 |
| TextArea | ✅ | F2 (fallthrough) | Sine | ⚠️ 可接受 |
| Table | ✅ | F2 (fallthrough) | Sine | ⚠️ 代码生成不完整 |
| Button | ❌ | — | — | N/A (用 Action) |
| Switch | ❌ | — | — | N/A (用 Action) |
| CheckBox | ❌ | — | — | N/A (用 Action) |
| Slider | ❌ | — | — | N/A (用 Action) |
| Dropdown | ❌ | — | — | N/A (用 Action) |
| Keyboard | ❌ | — | — | N/A |
| Spinner | ❌ | — | — | N/A |
| TabView | ❌ | — | — | N/A |
| Screen | ❌ | — | — | N/A |

### 2.2 动作绑定 (Action.Target)

`HmiAction.GetAvailableActions()` 按控件类型分发可用动作:

| 控件类型 | 可用动作 | Target 含义 | 变量交互 |
|----------|---------|-------------|---------|
| Button | SetBit, ResetBit, ToggleBit, MomentaryOn, MomentaryOff, SetVariable, ExecuteScript, SwitchScreen | 变量ID/名 | 读写 |
| Switch | ToggleBit, SetBit, ResetBit, SwitchScreen | 变量ID/名 | 读写 |
| CheckBox | ToggleBit, SetBit, ResetBit, SwitchScreen | 变量ID/名 | 读写 |
| Slider | SetVariable, SwitchScreen | 变量ID/名 | 写 |
| Dropdown | SetVariable, SwitchScreen | 变量ID/名 | 写 |
| TextArea | SetVariable, ExecuteScript, SwitchScreen | 变量ID/名 | 写 |
| Keyboard | SetVariable, ExecuteScript, SwitchScreen | 变量ID/名 | 写 |
| Table | SetVariable, ExecuteScript, SwitchScreen | 变量ID/名 | 写 |
| Label/Gauge/Knob/Led/Bar/Line/Image | SwitchScreen, ShowAlarms, ShowTrends, Login, Logout | 屏幕名 | 无 (只读显示) |
| Spinner/TabView/Screen | SwitchScreen, ShowAlarms, ShowTrends | 屏幕名 | 无 |

### 2.3 预览仿真覆盖

`PreviewValueResolver.IsDisplayControl()` + `MainWindow.StartPreviewMode()` 覆盖范围:

| 控件类型 | IsDisplayControl | 仿真注册 | 格式化分支 | 一致性 |
|----------|-----------------|---------|-----------|--------|
| Label | ✅ | ✅ (by bindingId) | F2 | ✅ |
| Gauge | ✅ | ✅ | F1 | ✅ |
| Bar | ✅ | ✅ | F1 + progress | ✅ |
| Knob | ✅ | ✅ | F1 | ✅ |
| Led | ✅ | ✅ | ON/OFF | ✅ |
| Slider | ✅ | ✅ | 整数 | ⚠️ UI 无绑定面板, 但预览仍处理 |
| Switch | ✅ | ✅ | ON/OFF | ⚠️ UI 无绑定面板, 但预览仍处理 |
| CheckBox | ✅ | ✅ | ON/OFF | ⚠️ UI 无绑定面板, 但预览仍处理 |
| Dropdown | ✅ | ✅ | 整数 | ⚠️ UI 无绑定面板, 但预览仍处理 |
| Image | ❌ | ✅ | — | ⚠️ 有绑定面板但不是显示控件 |
| Line | ❌ | ✅ | — | ⚠️ 有绑定面板但不是显示控件 |
| TextArea | ❌ | ✅ | — | ⚠️ 有绑定面板但不是显示控件 |
| Table | ✅ | ✅ | F2 | ⚠️ 有绑定面板且是显示控件, 但代码生成不完整 |

## 3. 兼容性矩阵

### 3.1 变量数据类型 vs 控件类型

**当前状态: 无校验** — 任何控件可绑定到任何数据类型的变量

| 控件类型 | 期望变量类型 | 当前校验 | 风险 |
|----------|-------------|---------|------|
| Led | Bool | ❌ 无 | 绑定 Float 时显示 "ON" (非零即 ON) |
| Gauge/Bar/Knob | Numeric (Int/Float) | ❌ 无 | 绑定 Bool 时仅显示 0.0/1.0 |
| Slider | Numeric | ❌ 无 | 绑定 Bool 时仅有 0/1 两档 |
| Label | Any | ❌ 无 | 绑定 Bool 时显示两位小数 |
| Table | Any | ❌ 无 | 无行列绑定机制 |

### 3.2 BindingMode 兼容性

| 模式 | 语义 | 当前执行 | 风险 |
|------|------|---------|------|
| ReadWrite (默认) | 控件可读可写变量 | ✅ 无限制 | — |
| ReadOnly | 控件仅显示, 不可写 | ❌ **未执行** | ReadOnly 控件仍可通过 Action 写入 |

## 4. 持久化分析

### 4.1 序列化路径

`ProjectSerializer` 使用 `System.Text.Json.JsonSerializer`:

```csharp
// JsonSerializerOptions: CamelCase, WriteIndented, JsonStringEnumConverter
// DefaultIgnoreCondition: WhenWritingNull
```

| 属性 | 类型 | JSON 字段名 | 序列化 | 反序列化 | 兼容性 |
|------|------|------------|--------|---------|--------|
| VariableBindingId | string? | `variableBindingId` | WhenWritingNull 跳过 null | 缺失时默认 null | ✅ 向后兼容 |
| BindingMode | string | `bindingMode` | 始终写入 (非 null) | 缺失时默认 "ReadWrite" | ✅ 向后兼容 |
| Action.Target | string? | `action.target` | WhenWritingNull 跳过 null | 缺失时默认 null | ✅ 向后兼容 |
| Action.Value | string? | `action.value` | WhenWritingNull 跳过 null | 缺失时默认 null | ✅ 向后兼容 |

### 4.2 Faceplate 克隆

`FaceplateExpander.CloneControl()` 复制以下绑定相关属性:

| 属性 | 克隆 | 替换 | 备注 |
|------|------|------|------|
| VariableBindingId | ✅ | ✅ 参数替换 | `{{Parameters.xxx}}` → 实际值 |
| BindingMode | ✅ | ❌ | 直接复制 |
| Action.Target | ✅ | ❌ | 直接复制 |
| Action.Value | ✅ | ❌ | 直接复制 |
| ParameterBindings | ✅ | — | 深拷贝 |

### 4.3 悬空引用检测

`CrossReferenceAnalyzer.GetAllReferencedVariableIds()` 收集所有引用的变量 ID, 但:

| 检测点 | 实现 | 调用时机 | 风险 |
|--------|------|---------|------|
| 保存时检测悬空引用 | ✅ **已修复 (P3)** | `SaveProject()` 调用 `FindDanglingReferences` | 状态栏显示警告计数 + Debug 输出详情 |
| 加载时检测悬空引用 | ✅ **已修复 (P3)** | 项目加载后调用 | 状态栏显示警告计数 + Debug 输出详情 |
| 删除变量前检查 | ✅ `CanSafelyDeleteVariable` | 手动调用 | 仅 UI 调用, 无强制 |

## 5. 代码生成分析

### 5.1 绑定输出生成

`CodeGenerator.GenerateScreenCode()` 对绑定的处理:

```csharp
// 跳过含 {{ 占位符的绑定 (未替换的 Faceplate 参数)
if (!string.IsNullOrEmpty(control.VariableBindingId) && !control.VariableBindingId.Contains("{{"))
{
    sb.AppendLine($"    // Bound variable: {control.VariableBindingId}");
}
```

| 场景 | 输出 | 问题 |
|------|------|------|
| 正常绑定 | `// Bound variable: temperature` | ✅ 注释形式, 无实际 C 代码 |
| UDT 字段绑定 | `// Bound variable: motor1.speed` | ✅ 注释形式 |
| 未替换的 Faceplate 占位符 | 静默跳过 | ❌ 绑定信息丢失, 无警告 |
| 空绑定 | 无输出 | ✅ 正确 |

### 5.2 动作代码生成

**当前状态: 未实现** — `CodeGenerator` 不生成 Action 相关的 C 代码 (如 `lv_obj_add_event_cb`)

### 5.3 控件 LVGL 映射

`ControlMappingRegistry.LvglCreateFunction` 完整映射:

| HmiControlType | LVGL 创建函数 | 代码生成覆盖 |
|----------------|-------------|-------------|
| Screen | `lv_obj_create` | ✅ |
| Button | `lv_button_create` | ✅ |
| Label | `lv_label_create` | ✅ |
| Slider | `lv_slider_create` | ✅ |
| Switch | `lv_switch_create` | ✅ |
| CheckBox | `lv_checkbox_create` | ✅ |
| Image | `lv_image_create` | ✅ |
| Bar | `lv_bar_create` | ✅ |
| Dropdown | `lv_dropdown_create` | ✅ |
| TextArea | `lv_textarea_create` | ✅ |
| Gauge | `lv_arc_create` | ✅ |
| Knob | `lv_arc_create` | ✅ |
| Keyboard | `lv_keyboard_create` | ✅ |
| Led | `lv_led_create` | ✅ |
| Spinner | `lv_spinner_create` | ✅ |
| TabView | `lv_tabview_create` | ✅ |
| Line | `lv_line_create` | ✅ |
| Table | `lv_table_create` | ✅ (但无行列生成) |

## 6. 发现的问题与风险

### 6.1 高优先级问题

| # | 问题 | 位置 | 影响 | 状态 |
|---|------|------|------|------|
| P1 | **BindingMode "ReadOnly" 未执行** | PropertyPanelView | ReadOnly 控件可通过 Action 写入变量 | ✅ **已修复** — ReadOnly 时过滤写操作动作, 切换时清除已有写动作 |
| P2 | **无变量类型兼容性校验** | VariablePicker | Led 可绑定到 Float 变量, Gauge 可绑定到 Bool | ✅ **已修复** — 按控件类型过滤可选变量数据类型 |
| P3 | **悬空引用无保存时校验** | MainWindow/ProjectSerializer | 可保存指向已删变量的绑定 | ✅ **已修复** — 保存和加载时调用 FindDanglingReferences, 状态栏警告 |

### 6.2 中优先级问题

| # | 问题 | 位置 | 影响 | 状态 |
|---|------|------|------|------|
| M1 | **用户变量用 Name 绑定 (可变)** | VariablePicker L149 | 变量改名后绑定断裂 | ✅ **已修复** — 改用 Id 绑定, ResolveDisplayName 回退解析 |
| M2 | **Image/Line 支持变量绑定但无语义** | PropertyPanelView L136-139 | 用户可绑定但无实际效果 | ✅ **已修复** — 从绑定面板列表中移除 |
| M3 | **Slider/Switch/CheckBox/Dropdown 预览不完整** | PreviewValueResolver vs PropertyPanelView | 预览系统处理但 UI 无法设置绑定 | ✅ **已修复** — 添加到绑定面板, 与预览一致 |
| M4 | **动作代码生成未实现** | CodeGenerator | 生成的 C 代码无事件回调 | ✅ **已修复** — 生成 lv_obj_add_event_cb + 12 种动作回调 |

### 6.3 低优先级问题

| # | 问题 | 位置 | 影响 | 建议 |
|---|------|------|------|------|
| L1 | **Table 代码生成不完整** | CodeGenerator | 无行列/单元格生成 | ✅ **已修复** — 生成行列数和表头单元格 |
| L2 | **Faceplate 未替换占位符静默丢弃** | CodeGenerator L558 | 绑定信息在生成代码中丢失 | ✅ **已修复** — 输出 WARNING 注释 |
| L3 | **预览仿真信号类型有限** | MainWindow L983-990 | 所有数值控件用相同 Sine 信号 | ✅ **已修复** — 按 DataType 选择信号和参数 |
| R5 | **绑定与动作独立不联动** | 全模型 | 控件绑定变量 A 但动作写入变量 B | ✅ **已修复** — 联动模型: VariableBindings + Actions 集合, 写动作 Target 自动链接 PrimaryWriteVariable |

### 6.4 联动模型 (R5) 完成清单

| 文件 | 改动 |
|------|------|
| `HmiControl.cs` | 新增 `VariableBindings` / `Actions` 集合 + `PrimaryBindingVariable` / `PrimaryWriteVariable` |
| `HmiAction.cs` | 新增 `IsWriteAction` / `AutoLinkTarget` / `WriteActionTypes` |
| `VariableBindingData.cs` | 移除冗余 `ControlId`, 保留 PropertyName/VariableId/Direction |
| `ProjectSerializer.cs` | 反序列化自动迁移旧字段 → 新集合 |
| `PreviewValueResolver.cs` | 用 PrimaryBindingVariable + 按属性名解析重载 |
| `CrossReferenceAnalyzer.cs` | 遍历集合, 双轨回退旧字段 |
| `NavigationGraph.cs` | 遍历 Actions 集合提取 SwitchScreen |
| `CodeGenerator.cs` | 多动作唯一回调名 + 多绑定注释 |
| `FaceplateExpander.cs` | 克隆/替换 VariableBindings 与 Actions 集合 |
| `PropertyPanelView.axaml.cs` | UI 联动: ApplyLinkageRules + ReadOnly 同步方向 |
| `TemplateManager.cs` / `FaceplateTemplateEditorView.axaml.cs` | 克隆时复制集合 |
| `BindingLinkageTests.cs` | 新增 16 项联动模型专项测试 |

## 7. 绑定生命周期完整链路

```
                    ┌─────────────────┐
                    │ VariableManager  │
                    │ (Producer)       │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ VariablePicker  │
                    │ (UI 选择)        │
                    └────────┬────────┘
                             │ VariableSelected 事件
                    ┌────────▼────────┐
                    │ PropertyPanel   │
                    │ .OnVariableSelected │
                    └────────┬────────┘
                             │ control.VariableBindingId = varName
                    ┌────────▼────────┐
                    │ HmiControl      │
                    │ (Model)         │
                    └────────┬────────┘
                             │
           ┌─────────────────┼─────────────────┐
           │                 │                 │
   ┌───────▼───────┐ ┌──────▼──────┐ ┌───────▼───────┐
   │ ProjectSerial │ │ Preview     │ │ CodeGen       │
   │ (持久化)       │ │ ValueResolver│ │ (C 代码生成)   │
   └───────┬───────┘ └──────┬──────┘ └───────┬───────┘
           │                │                 │
   ┌───────▼───────┐ ┌──────▼──────┐ ┌───────▼───────┐
   │ JSON 文件      │ │ CanvasPanel │ │ .c 源文件     │
   │ variableBindingId│ │ 预览渲染    │ │ // Bound var │
   └───────────────┘ └─────────────┘ └───────────────┘
                             │
                    ┌────────▼────────┐
                    │ CrossReference  │
                    │ Analyzer        │
                    │ (引用检查)       │
                    └─────────────────┘
```

### 7.1 链路完整性检查

| 环节 | 实现 | 状态 | 缺失 |
|------|------|------|------|
| 变量定义 → 控件绑定 | VariablePicker → PropertyPanel | ✅ | — |
| 控件绑定 → 持久化 | ProjectSerializer (JSON) | ✅ | — |
| 持久化 → 加载恢复 | JsonSerializer.Deserialize | ✅ | — |
| 控件绑定 → 预览 | PreviewValueResolver + Simulator | ✅ | — |
| 控件绑定 → 代码生成 | CodeGenerator (注释形式) | ⚠️ | 无实际绑定 C 代码 |
| 控件绑定 → 引用分析 | CrossReferenceAnalyzer | ✅ | — |
| 绑定类型校验 | VariablePicker FilterControlType | ✅ **已修复** | — |
| 绑定模式执行 | PropertyPanelView PopulateActions | ✅ **已修复** | — |
| 悬空引用检测 | FindDanglingReferences | ✅ **已修复** | 保存/加载时调用 |

## 8. 修复进度

### Phase 1 (已完成 ✅)
1. ~~**P1**: 在 `PropertyPanelView` 中 BindingMode="ReadOnly" 时过滤写操作动作, 切换时清除已有写动作, 显示提示文本~~
2. ~~**P3**: 在 `MainWindow.SaveProject()` 和项目加载后调用 `CrossReferenceAnalyzer.FindDanglingReferences`, 状态栏显示警告~~
3. ~~**P2**: 在 `VariablePicker.RebuildTree()` 中按控件类型过滤可选变量数据类型~~
4. ~~**M2**: 从 `PropertyPanelView.LoadVariableBinding()` 的 `isDisplayCtrl` 列表中移除 `Image` 和 `Line`~~
5. ~~**L2**: 在 `CodeGenerator` 中对未替换的 Faceplate 占位符输出 WARNING 注释~~

### Phase 2 (已完成 ✅)
6. ~~**M1**: `VariablePicker` 改用 `Variable.Id` 作为绑定标识, `Name` 仅用于显示~~ — 含 `ResolveDisplayName` 回退解析和 Faceplate 参数编辑器同步修改
7. ~~**M3**: 添加 Slider/Switch/CheckBox/Dropdown 到绑定面板~~ — 与 `PreviewValueResolver.IsDisplayControl` 已有覆盖一致
8. ~~**M4**: 在 `CodeGenerator` 中生成 Action 事件回调 C 代码~~ — `lv_obj_add_event_cb` + 12 种动作类型回调函数, 含 MomentaryOn/Off 双事件
9. ~~**L1**: 完善 Table 控件行列/单元格代码生成~~ — 生成 `lv_table_set_row_cnt` / `lv_table_set_col_cnt` / `lv_table_set_cell_value`
10. ~~**L3**: 按变量 DataType 选择预览仿真信号~~ — `ResolveVariableDataType` 方法, Bool→方波(0-1), Int→方波(0-100), Float→正弦(5-95)
