# HMI Editor 优化开发计划

> **基线**: v2.1.0
> **目标**: 对标主流工业 HMI 编辑器,从原型级提升至专业级
> **日期**: 2026-07-30
> **配套报告**: `2026-07-30-hmi-mainstream-comparison-report.md`
> **验证命令**: `dotnet build HmiEditor.sln` + `dotnet test`

---

## 总览:四阶段路线图

| 阶段 | 主题 | 周期 | 优先级 | 核心交付 |
|---|---|---|---|---|
| Phase R1 | 视觉系统 + 交互核心 | 2-3 周 | P0 | CommandBar + Dock + 拖放 + 类型编辑器 + 资源仪表盘 |
| Phase R2 | 编辑体验 + 复用 | 3-4 周 | P1 | 层管理 + 设计时预览 + 资源管理器 + 导航流 |
| Phase R3 | 数据绑定增强 | 2-3 周 | P1 | UDT + 表达式引擎 + 变量浏览器 + 交叉引用 |
| Phase R4 | 现代化与差异化 | 4-6 周 | P2 | AI 辅助 + SVG 导入 + Git 工作流 + 审计追踪 |

依赖已就绪(`Dock.Avalonia 12.1.0` + `FluentAvaloniaUI 3.0.2` 已入 csproj),Phase R1 无阻塞。

---

## Phase R1: 视觉系统 + 交互核心 (P0)

> 目标:第一印象达到专业级,核心交互对标主流工具。依赖已就绪,投入产出比最高。

### R1-T1: CommandBar 工具栏替代扁平按钮

**文件**:
- Modify: `src/HmiEditor.App/MainWindow.axaml`
- Modify: `src/HmiEditor.App/MainWindow.axaml.cs`

**要点**:
1. 将 `Grid.Row="1"` 的 `StackPanel` + `Button` 列表替换为 `fa:CommandBar`,按功能分组:文件 / 编辑 / 视图 / 插入 / 格式(上下文) / 工具
2. 选中控件时动态显示"格式"组(对齐/分布/排列),使用 `IsVisible` 绑定选中状态
3. 现有事件处理器直接复用(`OnNewProject`/`OnOpenProject` 等),无需改 Core
4. `DefaultLabelPosition="Right"`,图标使用 FluentAvalonia 内置 `PathIcon` 或 `Symbol`,禁止 emoji

**迁移检查**:对照 `MainWindow.axaml` 现有所有 `Btn*` 按钮,确保 CommandBar 覆盖全部(见既有 Phase R1 计划第十节检查清单,共 21 项)。

**验证**: `dotnet build` 0 error;逐项点击工具栏功能与重构前一致。

### R1-T2: Dock 可停靠布局启用

**文件**:
- Modify: `src/HmiEditor.App/MainWindow.axaml`
- Modify: `src/HmiEditor.App/MainWindow.axaml.cs`
- Create: `src/HmiEditor.App/Services/LayoutSerializer.cs`

**要点**:
1. 主体区用 `dock:DockControl` + `dockctrl:DockArea` 替代固定 Grid
2. 工具箱/属性/画布/项目浏览器/输出窗口包装为 `IDockable`,停靠方位:工具箱左、画布中、属性右、输出下
3. 现有 View(UserControl)直接作为 Dockable 内容,View 内部代码不改
4. 布局序列化:`LayoutSerializer` 保存/恢复到 `.layout.json`(独立于 `.hmi.json`)
5. 若 `Dock.Avalonia` 在 12.1 运行异常,降级为固定布局 + TabControl(ADR-001 备选)

**验证**: 拖拽面板可停靠/浮动/自动隐藏;关闭重开布局恢复;`dotnet run` 无崩溃。

### R1-T3: 工具箱拖放支持

**文件**:
- Modify: `src/HmiEditor.App/Views/ToolboxView.axaml.cs`
- Modify: `src/HmiEditor.App/Controls/DesignCanvas.cs`

**要点**:
1. `ToolboxView` 的 `ListBox` 项注册 `PointerPressed`,启动 `DragDrop.DoDragDrop`,Payload 为 `HmiControlType`
2. `DesignCanvas` 注册 `DragOver`/`Drop` 事件,Drop 时将屏幕坐标转画布坐标(补偿缩放+平移):
   `canvasX = (screenX - panOffsetX) / zoomLevel`
3. 拖放时显示半透明幽灵预览(可选,首版可省略)
4. 保留现有点击备选(点击在画布中心创建),拖放为增强不替换

**验证**: 落点偏差 < 5px;覆盖 0.25x/1x/2x/4x 缩放 + 平移场景的单元测试(FF5)。

### R1-T4: 属性面板类型化编辑器

**文件**:
- Modify: `src/HmiEditor.App/Views/PropertyPanelView.axaml`
- Modify: `src/HmiEditor.App/Views/PropertyPanelView.axaml.cs`
- Create: `src/HmiEditor.App/Views/Editors/NumericEditor.axaml`
- Create: `src/HmiEditor.App/Views/Editors/ColorEditor.axaml`(复用 `ColorPickerPopup`)
- Create: `src/HmiEditor.App/Views/Editors/VariablePicker.axaml`(树形+搜索)

**编辑器映射**:

| 属性 | 当前 | 目标 |
|---|---|---|
| X/Y/Width/Height/Radius/BorderWidth | TextBox | `NumericUpDown`(范围+步进) |
| BgColor/TextColor/BorderColor | TextBox | `ColorEditor`(色块+取色器) |
| VariableBindingId | ComboBox | `VariablePicker`(树形+搜索) |
| IsChecked/布尔 | — | `ToggleSwitch` |
| BindingMode/Action | ComboBox | `ComboBox`(保持) |

**按控件类型分类**:Gauge 显示量程/精度/单位;Button 显示按下色/释放色;Label 显示文本。在 `UpdatePropertyGroups` 中按 `control.Type` 切换 `Expander` 可见性。

**验证**: 非法值 100% 被编辑器拦截(FF6);颜色选择器点击即时更新画布。

### R1-T5: 状态栏资源仪表盘集成

**文件**:
- Modify: `src/HmiEditor.App/MainWindow.axaml`(状态栏区域)
- Modify: `src/HmiEditor.App/MainWindow.axaml.cs`

**要点**:
1. 状态栏增加 Flash/RAM 双 `ProgressBar` + 百分比文本 + 目标硬件名
2. 调用已实现的 `ResourceEstimator.EstimateFlashUsage`/`EstimateRamUsage`
3. 从 `HardwareLoader` 获取 `ResourceConstraints`(FlashSize/RamSize)
4. 阈值变色:<70% 绿、70-90% 橙、>90% 红
5. 控件增删/属性变更时触发更新(订阅 `PropertyChanged`)

**验证**: 仪表盘百分比正确显示;超限变红(FF R1-FF5)。

### Phase R1 验收门禁

| # | 指标 | 阈值 | 检查 |
|---|---|---|---|
| R1-FF1 | 编译 | 0 error, 0 warning | `dotnet build HmiEditor.sln` |
| R1-FF2 | 功能等价 | 所有重构前功能可用 | 手动逐项测试 |
| R1-FF3 | 拖放精度 | 落点偏差 < 5px | 缩放+平移场景 |
| R1-FF4 | 属性类型安全 | 100% 非法值拦截 | 手动测试 |
| R1-FF5 | 资源仪表盘 | Flash/RAM 正确显示 | 手动验证 |
| R1-FF6 | 画布性能 | 100 控件 > 30 FPS | `dotnet run` 手动 |
| R1-FF7 | 向后兼容 | 旧 `.hmi.json` 可加载 | 加载旧项目 |

---

## Phase R2: 编辑体验 + 复用 (P1)

> 目标:建立复杂画面管理与设计时验证能力。

### R2-T1: 层管理器面板

**文件**:
- Create: `src/HmiEditor.App/Views/LayerPanelView.axaml` + `.cs`
- Modify: `src/HmiEditor.Core/Models/HmiControl.cs`(新增 `IsLocked`/`IsVisible`/`LayerIndex`)
- Modify: `src/HmiEditor.App/Controls/DesignCanvas.cs`(锁定不可选/隐藏不渲染)

**要点**:
1. 树形按 Z 序展示所有控件,支持锁定/隐藏/重命名/拖拽排序
2. `LayerIndex` 作为附加属性,不破坏现有 `Controls` 列表;Z 序由 `LayerIndex` + 列表位置共同决定
3. 锁定控件不可选中/拖拽;隐藏控件不渲染

**验证**: 复杂画面(20+控件)可锁定/隐藏/重排;`UndoRedoTests` 通过。

### R2-T2: 设计时数据预览

**文件**:
- Modify: `src/HmiEditor.App/Controls/DesignCanvas.cs`(预览模式)
- Modify: `src/HmiEditor.App/MainWindow.axaml.cs`(预览切换)
- Reuse: `src/HmiEditor.Simulator/VariableSimulator.cs`

**要点**:
1. 画布工具栏增加"预览模式"切换按钮
2. 预览模式下,绑定变量的控件显示模拟值(从 `VariableSimulator` 获取)
3. 两种模式:手动输入模拟值 / 从仿真引擎自动获取(正弦/随机)
4. 预览模式下控件不可编辑,仅显示

**验证**: 预览模式显示绑定值;退出预览恢复编辑态。

### R2-T3: 资源管理器面板

**文件**:
- Create: `src/HmiEditor.App/Views/AssetManagerView.axaml` + `.cs`
- Create: `src/HmiEditor.Core/Models/AssetLibrary.cs`

**要点**:
1. 统一管理图片/字体/语言资源,树形分类
2. 图片导入时记录尺寸/色深/引用计数
3. 字体子集化:按用到的字符裁剪(省 Flash),生成 LVGL `lv_font_t`
4. 资源引用校验:悬空引用警告
5. 集成 `ResourceEstimator` 显示资源预算

**验证**: 图片导入后可在控件中引用;悬空引用有警告。

### R2-T4: 图片资源自动优化

**文件**:
- Create: `src/HmiEditor.Core/Services/ImageOptimizer.cs`
- Modify: `src/HmiEditor.App/Views/AssetManagerView.axaml.cs`

**要点**:
1. 图片导入时按目标硬件 `MaxTextureSize` 和颜色深度自动压缩/量化
2. 三档选项:原图 / 优化(默认) / 自定义(色深+尺寸)
3. RGB565/RGB888/索引色转换,生成 LVGL `lv_img_dsc_t`

**验证**: 优化后图片体积减少;视觉质量可接受;可回退原图。

### R2-T5: 导航流设计器

**文件**:
- Create: `src/HmiEditor.App/Views/NavigationFlowView.axaml` + `.cs`
- Create: `src/HmiEditor.Core/Models/NavigationGraph.cs`(节点=屏幕,边=跳转)

**要点**:
1. 可视化画面跳转图(节点+连线),从控件的 `SwitchScreen` 动作自动生成边
2. 独立 JSON 文件存储,不侵入 `.hmi.json`
3. 双击节点跳转到对应屏幕编辑

**验证**: 导航图与实际跳转动作一致;新增跳转动作自动出现在图上。

### Phase R2 验收门禁

| # | 指标 | 阈值 | 检查 |
|---|---|---|---|
| R2-FF1 | 层管理 | 锁定/隐藏生效 | 手动测试 |
| R2-FF2 | Faceplate 一致性 | 展开+传播+深度+循环检测 | `dotnet test --filter Faceplate` |
| R2-FF3 | 资源估算偏差 | 与实际编译 < 15% | 交叉编译对比 |
| R2-FF4 | 向后兼容 | 旧项目加载 | `dotnet test --filter ProjectSerializer` |

---

## Phase R3: 数据绑定增强 (P1)

> 目标:变量绑定体系对标 FactoryTalk 标签 + Ignition 表达式。

### R3-T1: UDT 结构化标签

**文件**:
- Create: `src/HmiEditor.Core/Models/VariableUdt.cs`(类型定义 + 实例)
- Modify: `src/HmiEditor.Core/Models/Variable.cs`(支持 UDT 实例)
- Modify: `src/HmiEditor.Core/Models/VariableManager.cs`(UDT 注册表)
- Modify: `src/HmiEditor.App/Views/VariableManagerView.axaml`(UDT 编辑)

**要点**:
1. 定义 UDT(如 `Motor { speed, current, fault, run }`),实例化后一次绑定整组
2. 控件可绑定 UDT 字段:`VariableId = "motor1.speed"`
3. CodeGen 生成结构体数组 + 字段访问

**验证**: UDT 实例化后字段可绑定;`dotnet test` 通过。

### R3-T2: 表达式引擎

**文件**:
- Create: `src/HmiEditor.Core/Services/ExpressionEngine.cs`
- Modify: `src/HmiEditor.Core/Models/VariableBindingData.cs`(支持表达式)
- Modify: `src/HmiEditor.App/Views/PropertyPanelView.axaml`(表达式编辑器)

**要点**:
1. 支持简单表达式:`tag1 > 100 ? "red" : "green"`、`tag1 + tag2`、`tag1 && !tag2`
2. 属性可绑定表达式(颜色/可见性/文本/数值)
3. 设计时求值(配合 R2-T2 预览模式),CodeGen 转译为 C 表达式或定时刷新回调
4. 解析器用 `DataTable.Compute` 或轻量表达式库(避免重依赖)

**验证**: 表达式在设计时预览正确;生成代码中表达式被正确转译。

### R3-T3: 变量浏览器(树形+搜索+拖放)

**文件**:
- Create: `src/HmiEditor.App/Views/VariableBrowserView.axaml` + `.cs`

**要点**:
1. 树形展示所有变量(按分组/UDT 组织),支持搜索过滤
2. 支持拖放到控件属性(变量绑定字段),替代当前 ComboBox
3. 集成 `VariablePicker` 编辑器(R1-T4 复用)

**验证**: 拖放变量到控件属性即绑定;搜索过滤生效。

### R3-T4: 交叉引用浏览器

**文件**:
- Create: `src/HmiEditor.App/Views/CrossReferenceView.axaml` + `.cs`
- Create: `src/HmiEditor.Core/Services/CrossReferenceAnalyzer.cs`

**要点**:
1. 选定变量/画面/脚本,显示所有引用位置(控件属性、动作、脚本绑定)
2. 支持跳转到引用处
3. 删除变量前检查引用,提示影响

**验证**: 引用列表完整;跳转正确;删除有引用的变量有警告。

### R3-T5: 脚本运行时执行(编辑器内验证)

**文件**:
- Modify: `src/HmiEditor.Core/Services/ScriptEngineService.cs`(实现 `IScriptEngine`)
- Modify: `src/HmiEditor.App/Views/ScriptEditorView.axaml.cs`(运行按钮)

**要点**:
1. 集成 NLua 或 MoonSharp 在编辑器内执行 Lua 脚本
2. 事件触发时(按钮点击/值改变)执行绑定脚本,配合仿真变量验证效果
3. 沙箱限制:禁止文件 IO / 网络访问

**验证**: 脚本在仿真模式下执行;输出窗口显示执行结果与错误。

> **后续复核（2026-09-20）**：R3-T5 **已实施，但要点 1 的实现方式与计划不同** —— 落地的是
> `src/HmiEditor.Core/Services/ScriptSandboxExecutor.cs`，它**只依赖 `ExpressionEngine`**
> （自研简化子集：赋值 + `if/then/else/end` + 纯表达式；注释写明「无 NLua 依赖」
> 「因为表达式引擎本身不支持函数调用，从根本上杜绝了副作用」）⇒ **当时的选择是不引入真 Lua 解释器**。
> 同期产出的 `HmiEditor.Scripting`（MoonSharp 版校验器 + 4 个模板）**从未接入任何调用点**，
> 全仓 **0 条** `using HmiEditor.Scripting`，已按死代码登记（登记表 **#36**）。
> 取证与去留建议见 `docs/2026-09-20-scripting-assembly-triage.md`。
>
> **再复核（2026-09-20 同日，稍后）** —— 上段描述的是**取证时的状态**（当时确实 0 引用），
> 但同日的决策是「**不强依赖真 Lua 执行、只把真解析要过来**」：`HmiEditor.Scripting` **整机下架**，
> 其中唯一的 MoonSharp 真解析**搬进 `HmiEditor.Core`**（`LuaSyntaxValidator`）并被
> `ScriptEngineService.Validate` 真实调用 ⇒ **要点 1「编辑器内执行 Lua」仍未做，但「编辑器内解析 Lua」已做**。
> 两者的差别是刻意的：`LoadString` **只解析不执行**，沙箱问题因此不进入本轮范围（要点 3 的沙箱限制由
> 既有的 `ScriptSandboxExecutor` 承担）。实施见 `docs/2026-09-20-lua-syntax-validation.md`。

### Phase R3 验收门禁

| # | 指标 | 阈值 | 检查 |
|---|---|---|---|
| R3-FF1 | UDT 字段绑定 | 字段可独立绑定 | `dotnet test --filter Udt` |
| R3-FF2 | 表达式求值 | 设计时与运行时一致 | 手动 + 单元测试 |
| R3-FF3 | 交叉引用完整 | 无遗漏引用 | `dotnet test --filter CrossReference` |
| R3-FF4 | 脚本沙箱 | IO/网络被拦截 | 单元测试 |

---

## Phase R4: 现代化与差异化 (P2)

> 目标:引入 2026 趋势能力,建立差异化护城河。分独立子项,可按需取舍。

### R4-T1: AI 辅助画面生成(战略亮点)

**文件**:
- Create: `src/HmiEditor.Core/Services/ScreenTemplateLibrary.cs`(规则模板)
- Create: `src/HmiEditor.App/Views/AiGenerateDialog.axaml` + `.cs`
- Modify: `src/HmiEditor.App/MainWindow.axaml`(菜单入口)

**分阶段实现**:
1. **阶段 A — 规则模板生成**:预设常见画面模板(电机控制、阀门、仪表盘、报警列表),用户选择类型+参数,自动生成控件组合。无 AI 依赖,立即可做。
2. **阶段 B — LLM 集成(可选)**:自然语言描述需求("生成一个含启动停止按钮+状态灯+转速表的电机控制画面"),调用 LLM 生成画面 JSON。需用户配置 API Key。
3. **阶段 C — 上下文感知**:基于现有变量表/UDT 自动推荐画面布局。

**验证**: 规则模板生成画面可用;生成结果可二次编辑。

### R4-T2: SVG/矢量图形导入

**文件**:
- Create: `src/HmiEditor.Core/Services/SvgImporter.cs`
- Modify: `src/HmiEditor.App/Views/AssetManagerView.axaml.cs`

**要点**:
1. 解析 SVG,转换为 LVGL `lv_draw_vector` 或路径数据
2. 支持绑定属性(颜色/可见性由变量驱动)
3. 相比位图大幅节省 Flash,且无损缩放
4. 首版支持基础图形(矩形/圆/路径/文本),复杂渐变降级为位图

**验证**: 导入的 SVG 在画布渲染;CodeGen 生成对应 LVGL 绘制代码。

### R4-T3: Git 友好工程工作流

**文件**:
- Create: `docs/git-workflow.md`(规范)
- Create: `src/HmiEditor.App/Services/ProjectDiffService.cs`(可选)
- Modify: `src/HmiEditor.Core/Serialization/ProjectSerializer.cs`(确保稳定字段顺序)

**要点**:
1. 确保 `.hmi.json` 序列化字段顺序稳定(可 diff)
2. 资源(图片/字体)单独文件存储,不内嵌 JSON
3. 提供 `.gitattributes`(JSON 文本 diff)+ `.gitignore`(bin/obj)
4. 文档:分支策略、画面级合并约定、冲突处理

**验证**: 修改一个控件属性,diff 仅显示该字段变更。

### R4-T4: 审计追踪与电子签名

**文件**:
- Create: `src/HmiEditor.Core/Models/User/AuditLog.cs`
- Create: `src/HmiEditor.Core/Services/AuditService.cs`
- Modify: `src/HmiEditor.Core/Services/AuthenticationService.cs`

**要点**:
1. 记录关键操作:项目打开/保存/导出、变量修改、用户登录/登出、权限变更
2. JSON 日志文件,支持 CSV/HTML 导出
3. 电子签名:敏感操作需二次确认(用户名+密码)
4. 对标 TwinCAT TF2400 / B&R mapp Audit

**验证**: 关键操作有审计记录;导出格式正确。

### R4-T5: 响应式/多分辨率布局

**文件**:
- Modify: `src/HmiEditor.Core/Models/HmiControl.cs`(布局属性)
- Modify: `src/HmiEditor.App/Controls/DesignCanvas.cs`(多分辨率预览)
- Modify: `src/HmiEditor.CodeGen/Templates/lvgl_v9_screen.scriban`

**要点**:
1. 控件支持布局模式:固定坐标 / 百分比 / 锚点 / Flex/Grid
2. CodeGen 生成 `LV_LAYOUT_FLEX`/`LV_LAYOUT_GRID` 代码
3. 多分辨率预览:同一画面切换不同目标分辨率查看效果
4. 对标 TwinCAT/WinCC Unified 自适应

**验证**: Flex 布局在不同分辨率下自适应;生成代码含 `lv_layout` 调用。

### Phase R4 验收门禁

| # | 指标 | 阈值 | 检查 |
|---|---|---|---|
| R4-FF1 | AI 模板生成 | 生成画面可用可编辑 | 手动验证 |
| R4-FF2 | SVG 导入 | 渲染正确 + 代码生成 | 手动 + 编译 |
| R4-FF3 | Git diff 友好 | 单字段修改 diff 干净 | git diff 验证 |
| R4-FF4 | 审计完整 | 关键操作 100% 记录 | `dotnet test --filter Audit` |
| R4-FF5 | LVGL 代码可编译 | 生成 C 在 LVGL v9 模拟器编译 | 交叉编译 |

---

## 跨阶段适应度函数(架构不变量)

| # | 属性 | 阈值 | 检查 | 频率 |
|---|---|---|---|---|
| FF1 | 依赖方向 | UI 不可被 Core 引用 | `dotnet build` | 每次 CI |
| FF2 | 项目文件兼容 | 新版可加载旧 `.hmi.json` | `dotnet test --filter ProjectSerializer` | 每次提交 |
| FF3 | 资源估算偏差 | < 15% | 编译对比 | 每次导出 |
| FF4 | 画布性能 | 100 控件 > 30 FPS | 手动 | 阶段验收 |
| FF5 | 拖放精度 | < 5px | 单元测试 | R1 完成 |
| FF6 | 属性类型安全 | 100% 拦截非法值 | 单元测试 | 每次提交 |
| FF7 | Faceplate 一致性 | 展开+传播+深度+循环 | `dotnet test --filter Faceplate` | 每次提交 |
| FF8 | LVGL 生成可编译 | C 代码可交叉编译 | `arm-none-eabi-gcc` | 每次导出 |

---

## 风险登记

| # | 风险 | 可能 | 影响 | 缓解 |
|---|---|---|---|---|
| R1 | Dock.Avalonia 在 12.1 不稳定 | 中 | 高 | R1-T2 首先编译验证;失败降级固定布局+TabControl |
| R2 | MVVM 重构引入回归 | 高 | 中 | 壳层优先,View 内部逐步迁移;保持 code-behind 兼容期 |
| R3 | 表达式引擎性能差 | 中 | 低 | 设计时缓存求值结果;CodeGen 静态转译 |
| R4 | SVG 解析复杂度 | 中 | 中 | 首版仅基础图形;复杂降级位图 |
| R5 | AI 集成依赖外部服务 | 低 | 中 | 阶段 A 规则模板无依赖先行;LLM 为可选增强 |
| R6 | 拖放坐标变换错误 | 高 | 中 | 单元测试覆盖多缩放+平移场景(FF5) |
| R7 | 资源估算偏差 | 中 | 中 | 积累实际数据定期校准系数表 |
| R8 | Lua 运行时沙箱逃逸 | 低 | 高 | 禁用 IO/网络 API;白名单内置函数 |

---

## 工作量估算

| 阶段 | 任务数 | 预估 | 核心交付 |
|---|---|---|---|
| Phase R1 | 5 | 2-3 周 | CommandBar+Dock+拖放+类型编辑器+资源仪表盘 |
| Phase R2 | 5 | 3-4 周 | 层管理+预览+资源管理器+图片优化+导航流 |
| Phase R3 | 5 | 2-3 周 | UDT+表达式+变量浏览器+交叉引用+脚本执行 |
| Phase R4 | 5 | 4-6 周 | AI 生成+SVG+Git+审计+响应式 |
| **合计** | **20** | **11-16 周** | 专业级嵌入式 HMI 编辑器 |

---

## 实施顺序与依赖

```
Phase R1 (P0, 串行内并行)
  R1-T1 CommandBar ─┬─→ R1-T3 拖放(依赖画布面板)
  R1-T2 Dock ───────┘
  R1-T4 类型编辑器(依赖属性面板位置)
  R1-T5 资源仪表盘(依赖状态栏位置)
        │
        ▼
Phase R2 (P1)
  R2-T1 层管理 ──→ R2-T2 设计时预览(复用 Simulator)
  R2-T3 资源管理器 ──→ R2-T4 图片优化
  R2-T5 导航流(独立)
        │
        ▼
Phase R3 (P1)
  R3-T1 UDT ──→ R3-T2 表达式引擎(依赖变量体系)
  R3-T3 变量浏览器(复用 R1-T4 VariablePicker)
  R3-T4 交叉引用(独立)
  R3-T5 脚本执行(依赖 Simulator)
        │
        ▼
Phase R4 (P2, 各子项独立可取舍)
  R4-T1 AI 生成(A 阶段无依赖先行)
  R4-T2 SVG 导入(依赖 R2-T3 资源管理器)
  R4-T3 Git 工作流(独立)
  R4-T4 审计追踪(依赖用户认证)
  R4-T5 响应式布局(依赖 CodeGen)
```

---

## 建议立即启动

**Phase R1 全部 5 项**,因为:
1. 依赖(Dock/FluentAvalonia)已入 csproj,无阻塞
2. 投入产出比最高:直接决定"专业感"第一印象
3. 改动集中在 UI 壳层,不触及 Core/CodeGen,回归风险可控
4. 每项独立可验证,失败可回退

启动前唯一前置:在 `dotnet build` 中确认 `Dock.Avalonia 12.1.0` + `FluentAvaloniaUI 3.0.2` 在 Avalonia 12.1 上编译通过(R1-T2 Step 0)。
