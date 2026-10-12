# HMI Editor 深度对标分析与 UI/交互重构架构决策

> **状态**: PROPOSED  
> **阶段**: Design (重构前架构决策)  
> **责任**: 用户 + 本地构建检查 (`dotnet build HmiEditor.sln`)  
> **日期**: 2026-07-30  
> **适用专家**: architecture-decisions

---

## 一、决策问题

**核心问题**: 如何在保持现有 LVGL v9 代码生成能力和嵌入式硬件抽象不变的前提下,将 HMI Editor 的 UI 界面和交互逻辑从"能用的原型级"重构为"对标西门子 WinCC / 罗克韦尔 FactoryTalk View 的专业级工业 HMI 编辑器"?

### 目标

1. **视觉专业感** — 达到主流工业 HMI 编辑器的第一印象标准(可停靠面板、Ribbon/专业工具栏、一致的视觉语言)
2. **交互效率** — 实现拖放式设计流程、上下文敏感的属性编辑、实时数据预览
3. **组件复用** — 建立参数化复合组件系统(Faceplate/Global Object),降低重复设计成本
4. **嵌入式约束感知** — 在设计时即反馈 Flash/RAM 资源占用,避免部署时才发现超限
5. **可扩展架构** — UI 层与 Core 解耦,支持未来新增控件类型/通信协议/硬件平台时无需大改界面

### 非目标

- 不改变底层 LVGL v9 代码生成引擎的核心架构
- 不替换 Avalonia UI 框架
- 不引入多用户协同编辑(单机工具定位)
- 不实现 SCADA 级实时数据采集(编辑器定位,非运行时)

### 约束

| 约束 | 说明 |
|---|---|
| 框架 | .NET 10 + Avalonia 12.1,不可更换 |
| 目标 | LVGL v9 on ARM (Allwinner T113 / Rockchip RK3506 / STM32F767) |
| 资源 | 嵌入式目标 Flash 8-32MB, RAM 64-256MB,编辑器须感知 |
| 兼容 | 现有 `.hmi.json` 项目文件格式须向后兼容 |
| 单机 | 无服务端,所有计算在客户端完成 |

---

## 二、主流 HMI 软件深度对比

### 2.1 对比对象

| 软件 | 厂商 | 定位 | 参考价值 |
|---|---|---|---|
| **WinCC (TIA Portal)** | Siemens | 统一工程框架, PLC+HMI 一体化 | 面板系统、Faceplate、库概念 |
| **FactoryTalk View** | Rockwell | Machine/Site Edition, 全球对象 | Global Objects、动画对象、标签体系 |
| **InTouch / AVEVA Edge** | AVEVA (Wonderware) | 图形化组态, SmartSymbol | SmartSymbol 参数化组件 |
| **Ignition Perspective** | Inductive Automation | Web-based, 模块化 | 模板系统、UDT、Python 脚本 |
| **EasyBuilder Pro** | Weintek | cMT 系列 HMI 原生工具 | 轻量化编辑器交互、宏系统 |
| **mapp View** | B&R | Web-based (HTML5), 组件化 | Widget 系统、OPC UA 原生 |

### 2.2 核心能力矩阵

| 能力维度 | WinCC TIA | FactoryTalk View | InTouch AVEVA | Ignition Perspective | EasyBuilder Pro | B&R mapp View | **本项目 (当前)** |
|---|---|---|---|---|---|---|---|
| **面板布局** | 可停靠+可保存布局 | 可停靠+多窗口 | 固定+浮动窗口 | Web 响应式 | 固定三栏 | 固定+可配置 | **固定四栏** |
| **工具栏** | Ribbon | Ribbon+工具条 | 工具条 | Web 工具栏 | 工具条 | 工具条 | **扁平按钮列表+emoji** |
| **控件库** | 80+ 含 Faceplate | 60+ 含 Global Object | 50+ 含 SmartSymbol | 40+ 含 Template | 30+ | 20+ Widget | **18 种, 无复合组件** |
| **属性编辑** | 分类+类型编辑器 | 分类+动画对象 | 属性窗口 | 属性+扩展 | 扁平属性 | 属性窗口 | **扁平 TextBox** |
| **画布交互** | 拖放+对齐+层级 | 拖放+对齐+层级 | 拖放+对齐 | 拖放+响应式 | 拖放+对齐 | 拖放 | **点击添加+对齐(无拖放)** |
| **层级管理** | 层管理器+Z序 | 层+锁定 | 层+锁定 | 层+锁定 | Z序 | 层 | **无层管理** |
| **变量绑定** | PLC标签+HMI标签+表达式 | 标签(直接/间接) | 标记名字典 | UDT+标签 | 地址+标签 | OPC UA 变量 | **VariableId+Mode** |
| **复合组件** | Faceplate(参数化) | Global Object | SmartSymbol | Template | 宏 | Widget | **有 Children 字段未启用** |
| **导航设计** | 导航编辑器 | 画面导航 | 窗口导航 | 页面导航 | 画面切换 | 页面导航 | **线性屏幕列表** |
| **资源管理** | 资源管理器+预算 | 资源浏览器 | 资源管理 | 项目资源 | 图片库 | 资源管理 | **无资源管理** |
| **脚本系统** | VBScript/C# | VBA | QuickScript.NET | Python(Jython) | 宏(Basic) | mapp Code | **Lua(独立)** |
| **报警系统** | ProDiag+等级+组 | Alarm+摘要 | Alarm 模型 | Alarm Journal | 报警表 | Alarm | **基础配置+列表** |
| **趋势/历史** | Trend+归档 | Trend+DataLog | HistTrend | Easy Chart | 趋势图 | Trend | **配置存在, 无实时预览** |
| **仿真** | PLCSIM+HMI 仿真 | FactoryTalk Test | WindowViewer | Preview 模式 | 离线仿真 | mapp 模拟 | **变量+通信仿真** |
| **代码生成** | 原生编译 | 原生编译 | 运行时引擎 | 运行时引擎 | 编译下载 | 编译 | **LVGL C 代码生成** |
| **多语言** | 项目语言+文本导出 | 语言切换 | 语言切换 | 语言切换 | 多语言 | 多语言 | **中/英 JSON** |
| **版本管理** | TIA 版本控制 | 工程版本 | 库版本 | 项目继承 | 项目版本 | 版本管理 | **无** |

### 2.3 交互模型深度分析

#### WinCC TIA Portal — 统一工程体验

- **Ribbon 工具栏**: 上下文敏感,选中控件时自动切换到"格式"选项卡,显示对齐、分布、排列工具
- **Faceplate 机制**: 将一组控件封装为参数化组件,定义接口属性(Input/Output),实例化时传入不同参数实现"一处定义,多处复用"
- **库系统**: 全局库 + 项目库,支持拖放实例化,库元素修改可传播到所有实例
- **交叉引用**: 变量/画面/脚本之间的引用关系可视化,便于影响分析
- **任务卡**: 右侧侧边栏显示当前上下文的相关任务(如选中控件时显示"格式""动画""事件"任务卡)

#### FactoryTalk View — Global Object 参数化

- **Global Object**: 创建参数化对象,通过"占位符"标签(#1, #2...)定义参数,实例化时替换为实际标签
- **动画对象**: 每个控件可绑定多个动画(颜色变化、位置移动、可见性、填充量),独立于控件本身配置
- **标签浏览器**: 树形结构展示所有标签,支持直接拖放到控件属性
- **显示画面层级**: 支持覆盖画面(Overlay)、弹出画面(Popup)、嵌入画面(Embedded)

#### Ignition Perspective — 现代 Web 架构

- **Template 系统**: 参数化视图模板,支持嵌套和继承,修改模板自动更新所有实例
- **UDT (User Defined Type)**: 将相关标签组织为自定义数据类型,实例化后一次绑定
- **会话模型**: 一个项目可定义多种会话(桌面/移动/大屏),自适应布局
- **事件脚本**: 组件事件(mouseClicked, propertyChanged)直接绑定 Python 脚本

#### Weintek EasyBuilder Pro — 轻量高效

- **直接地址绑定**: 控件直接绑定 PLC 地址(如 D100, M50),无需中间变量层
- **宏系统**: 简单 Basic 语法宏,用于数据搬运和简单逻辑
- **设计时仿真**: 离线仿真直接在编辑器内运行,无需外部 PLC
- **控件属性面板**: 类型化编辑器(颜色选择器、下拉框、数值输入),比纯文本框高效

---

## 三、当前项目状态评估

### 3.1 架构现状

```
zhishuHMI 解决方案 (8 个项目)
├── HmiEditor.App          ← Avalonia UI 层 (Views + Controls)
│   ├── MainWindow.axaml   ← 四面板固定布局 + 扁平工具栏
│   ├── Views/             ← 18 个 View (工具箱/属性/画布/各种编辑器)
│   └── Controls/          ← DesignCanvas / SelectionAdorner / UndoRedo / Template
├── HmiEditor.Core         ← 领域模型 (控件/变量/报警/趋势/配方/脚本/用户)
├── HmiEditor.CodeGen      ← LVGL v9 C 代码生成 (Scriban 模板)
├── HmiEditor.Renderer     ← 设计面渲染器
├── HmiEditor.Scripting    ← Lua 脚本校验和代码片段
├── HmiEditor.Simulator    ← 变量/通信仿真
├── Hmi.Hardware.*         ← 硬件抽象 (Allwinner/Rockchip/STM32)
└── Hmi.Communication.*    ← 通信驱动 (Modbus RTU/TCP, MQTT)
```

### 3.2 已有能力(优势)

1. **画布核心交互完备**: Ctrl+滚轮缩放(以鼠标为中心)、中键平移、Ctrl+框选、8手柄缩放、对齐参考线吸附、网格吸附
2. **撤销/重做系统**: Command 模式双栈,支持 Move/Add/Delete/PropertyChange 四种命令
3. **18 种控件**: 含 Gauge/Knob/LED/Spinner/Table/TabView 等工业控件,每种有 LVGL 映射
4. **多选+组合操作**: Ctrl+点击切换选中,多选时显示包围盒
5. **变量绑定基础**: VariableBindingId + BindingMode (ReadOnly/ReadWrite)
6. **嵌入式感知**: HardwareTarget 抽象层含 ResourceConstraints (FlashSize, RamSize, MaxTextureSize)
7. **代码生成**: Scriban 模板生成 LVGL v9 C 代码,7 种模板文件
8. **完整子系统**: 报警引擎、趋势配置、配方管理、Lua 脚本、用户认证、离线仿真

### 3.3 关键差距(按影响优先级排序)

| # | 差距 | 影响 | 主流对标 | 严重度 |
|---|---|---|---|---|
| G1 | **无拖放式控件添加** — 点击工具箱直接在画布中心生成,无法拖放到精确位置 | 设计效率低,定位需二次拖拽 | 所有主流 HMI 均支持拖放 | P0 |
| G2 | **工具栏不专业** — emoji + 彩色按钮,扁平 StackPanel,无 Ribbon/分组 | 第一印象差,功能密度低 | WinCC/FactoryTalk Ribbon | P0 |
| G3 | **属性面板为纯 TextBox** — 颜色、枚举、布尔值都用文本输入,无类型化编辑器 | 编辑效率低,易出错 | EasyBuilder 类型编辑器 | P0 |
| G4 | **无层管理/Z序控制** — 无法锁定、隐藏、调整控件叠放顺序 | 复杂画面难以管理 | WinCC/FactoryTalk 层管理 | P1 |
| G5 | **无复合组件系统** — HmiControl.Children 存在但未启用,无参数化 Faceplate | 重复设计成本高 | WinCC Faceplate / FactoryTalk Global Object | P1 |
| G6 | **无资源管理器** — 图片/字体/语言资源无统一管理,无资源预算 | 嵌入式部署易超限 | WinCC 资源管理器 | P1 |
| G7 | **无导航流设计器** — 屏幕间跳转靠代码/脚本,无可视化导航图 | 画面流不直观 | WinCC 导航编辑器 | P1 |
| G8 | **变量绑定过于简单** — 仅 VariableId+Mode,无地址绑定/表达式/UDT | 无法直接对接 PLC 地址 | FactoryTalk 标签体系 | P1 |
| G9 | **无设计时数据预览** — 画布上控件显示静态文本,不模拟绑定变量的值 | 设计阶段无法验证显示效果 | Ignition Preview 模式 | P2 |
| G10 | **无动画对象系统** — 控件无独立的动画配置(颜色变化/可见性/填充量) | 动态行为与控件耦合 | FactoryTalk 动画对象 | P2 |
| G11 | **工具箱无搜索分组** — 当前有搜索,但无拖放源支持 | 控件发现效率可接受 | WinCC 库浏览器 | P2 |
| G12 | **代码生成无资源预算** — 生成 C 代码不反馈 Flash/RAM 估算 | 部署时才发现超限 | WinCC 编译报告 | P2 |

---

## 四、架构决策记录 (ADR)

### ADR-001: UI 层重构为 MVVM + 可停靠面板 + Ribbon 工具栏

**状态**: PROPOSED

#### 上下文 / 驱动力

| 驱动力 | 理由 |
|---|---|
| 专业感差距 | 当前 emoji 工具栏与 WinCC/FactoryTalk 的 Ribbon 差距显著,影响用户信任度 |
| 交互效率 | 固定四栏布局无法适应不同工作流(如全屏画布设计 vs 属性密集编辑) |
| 可扩展性 | 新增功能(导航设计器/资源管理器)需要面板位置,固定布局无法容纳 |
| Avalonia 生态 | Avalonia 12.1 生态中,Dock 有 `Dock.Avalonia` 可选;Ribbon 无完整 12.x 兼容库,但 `FluentAvalonia` 的 `CommandBar` 可作为 Ribbon 替代方案 |

#### 考虑的替代方案

| 方案 | 描述 | 拒绝原因 |
|---|---|---|
| A. 保持固定布局,仅美化样式 | 不改布局结构,只换颜色/图标/字体 | 无法解决功能密度问题,工具栏仍会超长 |
| B. 多窗口 MDI | 每个编辑器独立窗口,可自由排列 | 工业用户习惯单窗口工作流,多窗口管理成本高 |
| **C. 可停靠面板 + Ribbon** (采纳) | Dock 面板可拖拽/停靠/自动隐藏 + Ribbon 上下文选项卡 | 兼顾灵活性和专业感,对标 WinCC 体验 |
| D. Web 化重构 | 用 Ignition 模式重写为 Web 应用 | 超出范围,破坏现有 Avalonia 投资和离线能力 |

#### 决策

采用方案 **C: 可停靠面板 + Ribbon 工具栏**:

1. **Dock 布局系统选型**: 采用 `Dock.Avalonia` (v12.0.0.2, 兼容 Avalonia 12.x, 活跃维护)
   - 工具箱/属性/画布/项目浏览器/输出窗口等面板均使用 `Dockable` 容器承载
   - 支持拖拽停靠、浮动窗口、自动隐藏、标签页分组
   - 布局状态通过 `Dock.Avalonia` 的序列化 API 保存/恢复
2. **Ribbon 工具栏替代扁平按钮栏**: 采用 `FluentAvalonia` 的 `CommandBar` (v3.0.2, 明确支持 Avalonia 12.1)
   - 顶部放置 `CommandBar` 作为主工具栏,按功能分组: 文件 / 编辑 / 视图 / 插入 / 格式(上下文) / 工具
   - 选中控件时动态显示/隐藏"格式"选项卡,提供对齐、分布、排列工具
   - 不满足完整 Ribbon 规范,但覆盖 80% 的 Ribbon 交互价值,且与 Avalonia 12.1 兼容
3. **MVVM 重构**: Views 绑定 ViewModels,消除 code-behind 中的业务逻辑,为测试和状态管理奠基
4. **布局持久化**: 用户可保存/恢复自定义面板布局

#### 后果

| 正面 | 负面 |
|---|---|
| 专业感显著提升,对标 WinCC | 引入 `Dock.Avalonia` 和 `FluentAvalonia` 两个外部依赖 |
| 面板可自由配置,适应不同工作流 | MVVM 重构涉及所有 View 的改造,工作量大 |
| 新功能有面板位置可放 | `CommandBar` 非完整 Ribbon,极端复杂场景下功能密度可能不足 |
| 布局持久化提升用户粘性 | 布局序列化增加项目文件复杂度 |
| `FluentAvalonia` 与 Avalonia 12.1 明确兼容,降低版本冲突风险 | `Dock.Avalonia` v12.0.0.2 标注兼容 12.0,须在 12.1 上实测验证 |

#### 可逆性

- **成本**: 中等。Ribbon 和 Dock 是 UI 层改动,不触及 Core/CodeGen
- **重新考虑触发**:
  - `Dock.Avalonia` 维护停滞或无法在 Avalonia 12.1 稳定运行 → 降级为固定布局 + Tab
  - `FluentAvalonia.CommandBar` 无法满足工具栏需求 → 自研简化 Ribbon 或等待 `Ribbon.Avalonia` 升级到 12.x

---

### ADR-002: 建立参数化复合组件(Faceplate)系统

**状态**: PROPOSED

#### 上下文 / 驱动力

| 驱动力 | 理由 |
|---|---|
| 重复设计成本 | 工业画面中"启动按钮+指示灯+标签"组合反复出现,当前须逐个添加 |
| HmiControl.Children 已存在 | 数据模型已预留父子关系,但 DesignCanvas 渲染和交互未利用 |
| 对标 WinCC Faceplate | 参数化组件是专业 HMI 编辑器的核心差异化能力 |

#### 考虑的替代方案

| 方案 | 描述 | 拒绝原因 |
|---|---|---|
| A. 不做复合组件,保持原子控件 | 每次手动添加多个控件 | 重复劳动,无法对标主流工具 |
| B. 简单组合(Group) | 仅支持分组,不支持参数化 | 无法实现"一处修改,全局更新" |
| **C. Faceplate 模板系统** (采纳) | 定义组件模板(含参数接口),实例化时传入参数,模板修改传播到所有实例 | 完整对标 WinCC Faceplate / FactoryTalk Global Object |
| D. 代码级继承 | 在 C# 中定义控件继承关系 | 过度工程化,用户无法在编辑器中自定义 |

#### 决策

采用方案 **C: Faceplate 模板系统**,并确定以下参数接口规范与 CodeGen 集成方案:

1. **ControlTemplate 模型扩展**: 在现有 `ControlTemplate.cs` 基础上增加参数定义和版本字段
   - `Parameters`: 模板参数列表(名称/显示名/方向/类型/默认值/描述)
   - `Version`: 模板版本戳(整数或字符串),实例可选择跟随最新版或固定版本
   - 当 `Parameters` 为空时,行为退化为当前简单组合模板,保持向后兼容
2. **参数接口规范**:
   - 参数方向: `Input`(外部传入,内部只读) / `Output`(内部写入,外部可读) / `InOut`
   - 参数类型: `String` / `Number` / `Boolean` / `Color` / `VariableId`
   - 模板内部通过占位符引用参数: `"{{Parameters.参数名}}"`,可出现在 `Text`、`BgColor`、`TextColor`、`BorderColor`、`VariableBindingId` 等字符串属性中
3. **Faceplate 实例模型**: 复用 `HmiControl`,新增实例字段
   - `TemplateId`: 引用的模板 ID
   - `TemplateVersion`: 固定版本或 `null` 表示跟随最新
   - `ParameterBindings`: 参数名→实际值的绑定列表
   - 实例本身在画布上表现为一个矩形占位,展开后才参与渲染和代码生成
4. **CodeGen 集成 —— 展开阶段**:
   - 在 `CodeGenerator.GenerateScreenCode` 之前,调用 `FaceplateExpander.Expand(screen, templateResolver)`
   - 展开器按 Z 序将每个 Faceplate 实例替换为模板内部控件的副本,坐标平移到实例位置
   - 对副本执行参数替换:将 `"{{Parameters.X}}"` 替换为绑定值
   - 支持嵌套 Faceplate,但限制最大展开深度(默认 8 层),防止循环依赖
5. **模板修改传播**:
   - 模板保存时自动递增 `Version`
   - 实例默认跟随最新版本;用户可显式"固定版本"
   - 打开项目时检测实例版本与模板版本差异,提示更新或忽略
6. **模板库**: 项目级 + 全局级模板库,支持导入导出

#### 后果

| 正面 | 负面 |
|---|---|
| 重复设计成本降低 60%+ (典型工业画面) | 模板系统增加模型复杂度 |
| 修改一处,全局更新 | 实例版本管理需要额外机制 |
| 对标 WinCC Faceplate,专业度提升 | 渲染引擎需支持"展开模板"逻辑 |
| 模板可导入导出,促进复用 | 代码生成需处理模板实例展开 |
| 复用现有 `HmiControl` 和 `ControlTemplate`,改动范围可控 | 参数占位符语法增加字符串处理开销 |
| CodeGen 展开后与不使用 Faceplate 的输出等价,便于验证 | 嵌套展开和循环依赖需要额外校验 |

#### 可逆性

- **成本**: 中等偏高。涉及 Core 模型、Canvas 渲染、CodeGen 三层改动,但由于复用现有 `HmiControl`/`ControlTemplate`,Core 层改动集中在新增字段和展开器
- **重新考虑触发**:
  - 模板参数系统过于复杂导致用户难以使用 → 降级为简单 Group(仅组合,无参数化)
  - CodeGen 展开后无法与手工布局输出保持等价 → 回退到实例化时"烘焙"为普通控件,放弃模板链接

---

### ADR-003: 属性面板改为分类化类型编辑器

**状态**: PROPOSED

#### 上下文 / 驱动力

| 驱动力 | 理由 |
|---|---|
| 编辑效率 | 纯 TextBox 输入颜色码(#RRGGBB)易出错,布尔值用文本输入反直觉 |
| 类型安全 | 无输入校验,非法值直接写入模型 |
| 对标差距 | EasyBuilder/WinCC 均使用类型化编辑器(颜色选择器/下拉/勾选/滑块) |

#### 决策

将属性面板从纯 TextBox 改为**分类化类型编辑器系统**:

1. **按控件类型分类属性组**: 不同控件类型显示不同属性组(如 Gauge 显示量程/精度/单位,Button 显示按下色/释放色)
2. **类型化编辑器**:
   - 颜色属性 → ColorPicker 弹出器(已有 `ColorPickerPopup.cs`)
   - 枚举属性 → ComboBox 下拉
   - 布尔属性 → ToggleSwitch/CheckBox
   - 数值属性 → NumericUpDown(含范围/步进)
   - 字体属性 → FontPicker(字体名/大小/粗细)
   - 变量绑定 → VariablePicker(树形选择+搜索)
3. **属性面板 MVVM 化**: 用 `PropertyGrid` 模式,根据选中控件类型动态生成编辑器

#### 后果

| 正面 | 负面 |
|---|---|
| 编辑效率提升,减少输入错误 | 需要为每种属性类型实现编辑器(工作量) |
| 颜色选择器已有基础(ColorPickerPopup) | 动态属性生成逻辑较复杂 |
| 不同控件类型显示不同属性,更专业 | 属性元数据需要维护(可反射驱动) |

---

### ADR-004: 画布交互升级 — 拖放 + 层管理 + 实时预览

**状态**: PROPOSED

#### 上下文 / 驱动力

| 驱动力 | 理由 |
|---|---|
| 拖放是 HMI 编辑器标配 | 当前"点击添加到画布中心"无法精确定位,所有主流工具均支持拖放 |
| 层管理缺失 | 复杂画面(20+控件)无法管理 Z 序、锁定、可见性 |
| 设计时数据预览 | 设计阶段看不到控件实际显示效果(如变量值绑定后的数值显示) |

#### 决策

1. **拖放式控件添加**:
   - 工具箱项支持 `DragDrop` (Avalonia `DragDrop.DoDragDrop`)
   - 画布接收 Drop 事件,在鼠标落点创建控件
   - 拖放时显示半透明幽灵预览
2. **层管理器面板**:
   - 新增 Layer 面板(可停靠),树形展示所有控件按 Z 序排列
   - 支持锁定(Lock)、隐藏(Hide)、重命名、拖拽排序
   - 锁定控件不可选中/拖拽,隐藏控件不渲染
3. **设计时数据预览**:
   - 画布工具栏增加"预览模式"切换按钮
   - 预览模式下,绑定变量的控件显示模拟值(从 Simulator 获取)
   - 支持"手动输入模拟值"和"从仿真引擎获取"两种模式

#### 后果

| 正面 | 负面 |
|---|---|
| 拖放定位精确,减少二次操作 | DragDrop 实现需处理缩放/平移坐标变换 |
| 层管理使复杂画面可维护 | 层模型需要扩展 HmiControl (IsLocked/IsVisible/LayerIndex) |
| 设计时即可验证显示效果 | 预览模式需要 Simulator 联动,增加耦合 |

---

### ADR-005: 嵌入式资源预算系统

**状态**: PROPOSED

#### 上下文 / 驱动力

| 驱动力 | 理由 |
|---|---|
| 嵌入式目标资源受限 | Allwinner T113: 128MB Flash / 128MB RAM; STM32F767: 2MB Flash / 512KB RAM |
| 当前无资源感知 | 代码生成不反馈资源占用,部署时才发现超限 |
| HardwareTarget 已有约束模型 | `ResourceConstraints` 类已定义 FlashSize/RamSize/MaxTextureSize,但未利用 |

#### 决策

1. **资源估算引擎**: 在 CodeGen 层增加 `ResourceEstimator`,根据项目控件数量/类型/图片资源估算 Flash 和 RAM 占用
2. **实时资源仪表盘**: 状态栏或侧边面板显示当前项目的资源使用率(进度条),超限时红色警告
3. **图片资源优化**: 图片导入时自动压缩/量化到目标硬件支持的颜色深度和最大尺寸
4. **代码生成报告**: 导出 C 代码时生成 `resource_report.txt`,列出各模块资源占用明细

#### 后果

| 正面 | 负面 |
|---|---|
| 部署前即可发现资源超限 | 估算精度依赖于经验系数,可能偏差 |
| 图片自动优化减少手动处理 | 图片压缩增加编辑器处理时间 |
| 对标 WinCC 编译报告 | 需要维护各控件类型的资源系数表 |

---

## 五、系统映射

### 5.1 目标架构(重构后)

```
┌─────────────────────────────────────────────────────────────────┐
│                     HmiEditor.App (UI 层)                       │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  Ribbon 工具栏 (上下文敏感)                                │  │
│  │  [文件] [编辑] [视图] [插入] [格式*] [工具] [* = 上下文]   │  │
│  ├───────────┬───────────────┬───────────────┬───────────────┤  │
│  │ Dock:工具箱 │ Dock:画布      │ Dock:属性面板  │ Dock:层管理   │  │
│  │ (拖放源)   │ (拖放目标)     │ (类型编辑器)   │ (Z序/锁定)    │  │
│  │           │               │               │               │  │
│  │ - 控件库   │ - 设计画布     │ - 分类属性     │ - 层树        │  │
│  │ - 模板库   │ - 预览模式     │ - 颜色选择器   │ - 锁定/隐藏   │  │
│  │ - 搜索     │ - 对齐参考线   │ - 变量选择器   │ - 拖拽排序    │  │
│  ├───────────┴───────────────┴───────────────┴───────────────┤  │
│  │ Dock:项目浏览器 │ Dock:资源管理器 │ Dock:导航流 │ Dock:输出    │  │
│  │ (屏幕/变量/    │ (图片/字体/    │ (画面跳转   │ (编译/资源   │  │
│  │  报警/配方)    │  语言/预算)    │  可视化图)  │  报告)      │  │
│  └───────────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  ViewModels (MVVM) — 每个View绑定VM,消除code-behind业务逻辑│  │
│  └───────────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                    HmiEditor.Core (领域层)                      │
│  ┌─────────────┬──────────────┬──────────────┬────────────────┐ │
│  │ Models      │ Services     │ Mapping      │ Serialization  │ │
│  │ - HmiControl│ - AlarmEngine│ - ControlMap │ - ProjectSer   │ │
│  │ - Faceplate │ - DataLogger│ - TemplateMap│ - TemplateSer  │ │
│  │ - Layer     │ - ResourceEst│              │ - LayoutSer    │ │
│  │ - Animation │ - Simulator  │              │                │ │
│  └─────────────┴──────────────┴──────────────┴────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│  HmiEditor.CodeGen    │ HmiEditor.Renderer │ HmiEditor.Scripting│
│  - LVGL v9 模板       │ - 设计面渲染        │ - Lua 校验         │
│  - 资源估算           │ - Faceplate 展开    │ - 脚本绑定         │
│  - 多文件生成         │ - 层合成渲染        │                    │
├─────────────────────────────────────────────────────────────────┤
│  Hmi.Hardware.* (Allwinner/Rockchip/STM32)                      │
│  Hmi.Communication.* (Modbus RTU/TCP, MQTT)                     │
└─────────────────────────────────────────────────────────────────┘
```

### 5.2 数据流

```
用户操作 → ViewModel 命令 → Core Model 变更 → 事件通知
    ↓                                    ↓
DesignCanvas 重渲染               ProjectSerializer 持久化
    ↓                                    ↓
SelectionAdorner 更新             资源估算仪表盘更新
    ↓
CodeGen (导出时) → LVGL C 代码 + resource_report.txt
```

### 5.3 有界上下文映射

| 上下文 | 职责 | 模型/语言 | 上游 | 下游 | 关系 |
|---|---|---|---|---|---|
| UI Layout | 面板布局/工具栏/状态 | Avalonia XAML + VM | Core Models | — | 消费者 (conformist) |
| Canvas Interaction | 画布渲染/选中/拖放/缩放 | DrawingContext + 坐标变换 | Core Models | UI Layout | 共享内核 (Shared Kernel) |
| Component Template | Faceplate 定义/实例化/参数 | ControlTemplate + Parameter | Core Models | CodeGen, Canvas | 客户/供应商 (Customer/Supplier) |
| Property Editing | 属性分类/类型编辑器/校验 | PropertyDescriptor + Editor | Core Models | Canvas | 消费者 (conformist) |
| Resource Budget | 资源估算/图片优化/报告 | ResourceConstraints + Estimator | Hardware.*, CodeGen | UI Layout | 反腐层 (ACL) |
| Code Generation | LVGL C 代码生成/模板渲染 | Scriban + C 代码 | Core Models, Component Template | — | 客户/供应商 |
| Layer Management | Z序/锁定/可见性/层组 | Layer + HmiControl 扩展 | Core Models | Canvas | 共享内核 |
| Navigation Flow | 画面跳转/过渡/导航图 | NavigationGraph + Edge | Core Models | CodeGen | 消费者 |

---

## 六、分阶段重构计划

### Phase R1 (P0): 视觉系统 + 拖放交互 (2-3 周)

> 目标: 第一印象达到专业级,核心交互效率对标主流工具

| 任务 | 改动文件 | 对标 |
|---|---|---|
| Ribbon 工具栏替代扁平按钮栏 | `MainWindow.axaml` + 新增 `Ribbon/` 目录 | WinCC TIA |
| 工具箱拖放支持(DragDrop) | `ToolboxView.axaml.cs` + `DesignCanvas.cs` | 所有主流 |
| 属性面板类型化编辑器(颜色/下拉/勾选/数值) | `PropertyPanelView.axaml` + 新增 `Editors/` | EasyBuilder |
| 属性面板按控件类型分类 | `PropertyPanelView.axaml.cs` + `PropertyDescriptor.cs` | WinCC TIA |
| 状态栏资源仪表盘(Flash/RAM) | `MainWindow.axaml` + `ResourceEstimator.cs` | WinCC 编译 |

### Phase R2 (P1): 组件系统 + 层管理 (3-4 周)

> 目标: 建立参数化复用能力和复杂画面管理能力

| 任务 | 改动文件 | 对标 |
|---|---|---|
| Faceplate 模板系统(定义/实例化/参数) | `ControlTemplate.cs` + `TemplateManager.cs` + 新增 `FaceplateEditorView` | WinCC Faceplate |
| 层管理器面板(Lock/Hide/Z序/拖拽排序) | 新增 `LayerPanelView.axaml` + `Layer.cs` | WinCC/FactoryTalk |
| HmiControl 扩展(IsLocked/IsVisible/LayerIndex/ZOrder) | `HmiControl.cs` + `DesignCanvas.cs` | 通用 |
| 导航流设计器(画面跳转可视化图) | 新增 `NavigationFlowView.axaml` + `NavigationGraph.cs` | WinCC 导航 |
| 资源管理器面板(图片/字体/语言) | 新增 `AssetManagerView.axaml` + `AssetLibrary.cs` | WinCC 资源 |

### Phase R3 (P1): 数据绑定增强 + 设计时预览 (2-3 周)

> 目标: 变量绑定体系对标 FactoryTalk 标签,设计时即可验证显示效果

| 任务 | 改动文件 | 对标 |
|---|---|---|
| 变量绑定增强(地址绑定/表达式/UDT) | `Variable.cs` + `VariableBindingData.cs` + `PropertyPanelView` | FactoryTalk 标签 |
| 设计时数据预览模式 | `DesignCanvas.cs` + `SimulatorView` 联动 | Ignition Preview |
| 动画对象系统(颜色/可见性/填充量独立配置) | 新增 `AnimationObject.cs` + `AnimationPanelView` | FactoryTalk 动画 |
| 变量浏览器(树形+搜索+拖放到属性) | 新增 `VariableBrowserView.axaml` | FactoryTalk 标签浏览器 |

### Phase R4 (P2): 代码生成增强 + 专业化 (2-3 周)

> 目标: 代码生成对标 WinCC 编译报告,完善专业功能

| 任务 | 改动文件 | 对标 |
|---|---|---|
| 资源估算引擎(Flash/RAM 占用明细) | `ResourceEstimator.cs` + `CodeGenerator.cs` | WinCC 编译 |
| 多文件 C 代码生成(LVGL 项目结构) | `CodeGenerator.cs` + `LvglApiWriter.cs` | 通用 |
| 图片资源自动压缩/量化 | `AssetLibrary.cs` + 新增 `ImageOptimizer.cs` | 通用 |
| 交叉引用浏览器(变量/画面/脚本引用) | 新增 `CrossReferenceView.axaml` | WinCC 交叉引用 |
| 布局持久化(保存/恢复面板布局) | 新增 `LayoutSerializer.cs` | WinCC 布局 |

---

## 七、适应度函数 (Fitness Functions)

> 架构不变量,须可测试

| # | 属性 | 指标 | 阈值/规则 | 测量源 | 评估频率 | 失败响应 | 本地检查路径 |
|---|---|---|---|---|---|---|---|
| FF1 | 依赖方向 | UI→Core 引用方向 | UI 项目不可被 Core 项目引用 | `dotnet build` + 项目引用图 | 每次 CI | 阻止合并 | `dotnet build HmiEditor.sln` |
| FF2 | 项目文件兼容 | `.hmi.json` 向后兼容 | 新版本可加载旧版本项目文件 | 单元测试 `ProjectSerializerTests` | 每次提交 | 阻止合并 | `dotnet test --filter ProjectSerializer` |
| FF3 | 资源预算 | Flash 占用估算偏差 | 估算值与实际编译值偏差 < 15% | 编译后二进制大小 vs 估算 | 每次导出 | 警告+记录 | 手动验证 |
| FF4 | 画布性能 | 100 控件渲染帧率 | > 30 FPS (设计画布) | Avalonia `RenderTimer` | 手动验证 | 优化渲染路径 | `dotnet run` + 手动测试 |
| FF5 | 拖放正确性 | 拖放落点精度 | 控件创建位置与鼠标落点偏差 < 5px(缩放补偿后) | 手动测试 | Phase R1 完成时 | 修复坐标变换 | 手动验证 |
| FF6 | 属性编辑类型安全 | 非法值拦截率 | 100% 非法值被编辑器拦截 | 单元测试 | 每次提交 | 阻止合并 | `dotnet test --filter PropertyEditor` |
| FF7 | Faceplate 实例一致性 | 模板修改传播、展开等价性、嵌套深度、参数绑定完整性、循环依赖检测 | 详见附录 E.9 | 单元测试 `FaceplateTests` | 每次提交 | 阻止合并 | `dotnet test --filter Faceplate` |
| FF8 | LVGL 代码生成 | 生成代码可编译 | 生成的 C 代码在 LVGL v9 模拟器中可编译 | 交叉编译验证 | 每次导出 | 阻止发布 | 交叉编译 `arm-none-eabi-gcc` |

---

## 八、风险登记

| # | 风险 | 可能性 | 影响 | 缓解 | 记录 |
|---|---|---|---|---|---|
| R1 | Avalonia Dock/Ribbon 库不成熟或停止维护 | 中 | 高 | **已评估**: Dock 采用 `Dock.Avalonia` v12.0.0.2 (兼容 12.x, 活跃维护); Ribbon 采用 `FluentAvalonia.CommandBar` 替代(明确支持 12.1); 保留自研兜底方案 | 评估完成,见附录 D |
| R2 | MVVM 重构引入大量回归 bug | 高 | 中 | 分批重构(先新功能用 MVVM,旧功能逐步迁移);保持 code-behind 兼容期 | 每个 View 迁移后手动验证 |
| R3 | Faceplate 参数系统过于复杂 | 中 | 中 | 先实现简单参数(字符串/数值/颜色),再逐步扩展;提供模板向导 | 用户反馈后迭代 |
| R4 | 资源估算偏差大 | 中 | 中 | 积累各控件类型的实际占用数据,定期校准系数表 | 每次交叉编译后记录对比 |
| R5 | 拖放坐标变换在缩放/平移后不正确 | 高 | 中 | 单元测试覆盖 0.25x/1x/4x 缩放 + 平移场景;参考现有 HitTestControl 补偿逻辑 | FF5 验证 |
| R6 | 层管理模型与现有 Controls 列表冲突 | 中 | 中 | 层索引作为附加属性,不破坏现有 Controls 列表顺序;Z 序由 LayerIndex + 列表位置共同决定 | 设计评审 |
| R7 | 导航流设计器增加项目文件复杂度 | 低 | 低 | 导航图作为独立 JSON 文件存储,不侵入主项目文件 | 设计评审 |
| R8 | 图片自动压缩导致质量损失 | 中 | 低 | 提供"原图/优化/自定义"三档选项,默认优化但可回退 | 用户可配置 |

---

## 九、决策总结表

| ADR | 决策 | 默认方案 | 被拒替代 | 异常条件 |
|---|---|---|---|---|
| ADR-001 | 可停靠面板 + Ribbon | Dock + Ribbon | 固定布局美化 / 多窗口 MDI / Web 重写 | Avalonia Dock 库不可用时降级为固定布局+Tab |
| ADR-002 | Faceplate 模板系统 | 参数化模板 | 简单 Group / 代码继承 | 复杂度过高时降级为 Group |
| ADR-003 | 分类化类型编辑器 | PropertyGrid 模式 | 纯 TextBox(现状) | — |
| ADR-004 | 拖放 + 层管理 + 预览 | 拖放 + Layer + Preview | 点击添加(现状) | 拖放性能差时保留点击备选 |
| ADR-005 | 资源预算系统 | 估算引擎 + 仪表盘 | 无资源感知(现状) | 估算偏差>25%时仅显示定性提示 |

---

## 十、后续检查

1. ~~**Phase R1 启动前**: 评估 Avalonia Dock/Ribbon 候选库,确认技术可行性(影响 ADR-001)~~ ✅ 已完成: Dock 选用 `Dock.Avalonia`, Ribbon 选用 `FluentAvalonia.CommandBar`, 详见附录 D
2. ~~**Phase R2 启动前**: 设计 Faceplate 参数接口规范,确认与 CodeGen 的兼容性(影响 ADR-002)~~ ✅ 已完成: 参数接口、`ControlTemplate`/`HmiControl` 扩展、`FaceplateExpander` 展开器及验证器设计,详见附录 E

---

## 附录 A: 当前工具栏问题清单

当前 `MainWindow.axaml` 工具栏存在以下具体问题:

1. 使用 emoji 作为图标(🎨 📊 📜 📋 📥 ▶ 🗑)— 在不同系统渲染不一致,不专业
2. 按钮背景色各异(紫色/青色/蓝色/绿色/橙色)— 无统一视觉语言
3. 所有功能按钮平铺在一行 — 功能密度高但无分组,认知负担大
4. 对齐工具用 Unicode 符号(⫷ ⫸ ⫯ ⫰ ↔ ↕)— 不同字体下显示差异大
5. 缩放控件(+ − 100%)与功能按钮混在一起 — 逻辑分组不清

## 附录 B: 当前属性面板问题清单

当前 `PropertyPanelView.axaml` 存在以下具体问题:

1. 所有属性用 `TextBox` 输入 — 颜色码、数值、布尔值都用文本
2. 颜色输入为 `PlaceholderText="#RRGGBB"` — 用户需手写十六进制,易出错
3. 变量绑定为 `ComboBox` 但无搜索 — 变量多时难以定位
4. 属性不区分控件类型 — Gauge 和 Button 显示相同属性组
5. 无输入校验 — 非法值直接写入模型,可能影响代码生成

## 附录 C: 主流 HMI 交互模式速查

| 交互模式 | WinCC | FactoryTalk | Ignition | EasyBuilder | 本项目 |
|---|---|---|---|---|---|
| 控件添加 | 拖放 | 拖放 | 拖放 | 拖放 | **点击** |
| 属性编辑 | 双击/面板 | 面板 | 面板 | 双击/面板 | **面板** |
| 多选 | 框选+Ctrl | 框选+Ctrl | 框选+Shift | 框选 | **框选+Ctrl** |
| 对齐 | 工具栏+参考线 | 工具栏+参考线 | 工具栏 | 工具栏 | **工具栏+参考线** |
| 缩放 | Ctrl+滚轮 | Ctrl+滚轮 | 按钮+滚轮 | 按钮 | **Ctrl+滚轮** |
| 平移 | 中键/空格+拖拽 | 中键 | 空格+拖拽 | 滚轮 | **中键** |
| 层管理 | 层面板 | 层面板 | 层面板 | Z序面板 | **无** |
| 属性分类 | 按类型 | 按类型 | 按类型 | 按类型 | **无分类** |

## 附录 D: Avalonia 12.1 Dock/Ribbon 第三方库评估

### D.1 评估范围

针对 ADR-001 中"可停靠面板 + Ribbon 工具栏"的技术选型,在 Avalonia 12.1 生态下对 Dock 和 Ribbon 两类第三方库进行了调研。

### D.2 Dock 库评估

| 库 | 版本 | Avalonia 兼容 | 维护状态 | 评估结论 |
|---|---|---|---|---|
| **Dock.Avalonia** | v12.0.0.2 | 12.0 (接近 12.1) | 活跃 (最后提交 2026-06-19) | **推荐采用**。功能完整(停靠/浮动/自动隐藏/布局序列化),与目标版本差距最小 |
| 自研 Dock | — | 完全可控 | 内部维护 | 备选方案,成本较高,仅在外部库不可行时启用 |

### D.3 Ribbon 库评估

| 库 | 版本 | Avalonia 兼容 | Ribbon 完整度 | 评估结论 |
|---|---|---|---|---|
| **FluentAvalonia (CommandBar)** | v3.0.2 | **12.1** | 中等 (非完整 Ribbon,但覆盖主要场景) | **推荐采用**。明确支持 Avalonia 12.1,CommandBar 可作为 Ribbon 的轻量替代 |
| Ribbon.Avalonia | v0.9.0 | 11.2.2 (无 12.x) | 高 (完整 Ribbon) | **不采用**。版本滞后,升级到 12.x 时间不确定 |
| Semi.Avalonia | v12.0.3 | 12.1 | 无 Ribbon 控件 | **不采用**。仅提供主题/组件库,无 Ribbon 能力 |
| 自研 Ribbon | — | 完全可控 | 按需实现 | 长期备选,短期成本过高 |

### D.4 最终选型

| 能力 | 选定库 | 版本 | 说明 |
|---|---|---|---|
| Dock 布局 | `Dock.Avalonia` | v12.0.0.2 | 工具箱/属性/画布/项目浏览器/输出窗口均以 Dockable 承载 |
| Ribbon 工具栏 | `FluentAvalonia.CommandBar` | v3.0.2 | 作为主工具栏,按功能分组,选中控件时显示上下文"格式"选项卡 |

### D.5 集成要点

1. **项目引用**: 在 `HmiEditor.App.csproj` 添加 `Dock.Avalonia` 和 `FluentAvalonia` 的 PackageReference,版本与 Avalonia 12.1 锁定
2. **主题融合**: `FluentAvalonia` 自带 Fluent 主题,需与现有深色工业主题协调(可覆盖其主题刷色)
3. **Dock 面板封装**: 将现有 `ToolboxView`、`PropertyPanelView`、`ProjectExplorerView` 等包装为 `IDockable`,纳入 `DockControl`
4. **CommandBar 命令绑定**: 将现有菜单/工具栏命令抽象为 `ICommand`,绑定到 `CommandBar` 的 `CommandBarButton` / `CommandBarToggleButton`
5. **兼容性验证**: `Dock.Avalonia` v12.0.0.2 虽标注 12.0,但需在 Avalonia 12.1 实际构建运行,作为 Phase R1 的首个任务

### D.6 风险与备选

| 风险 | 触发条件 | 备选方案 |
|---|---|---|
| `Dock.Avalonia` 在 12.1 上不稳定 | 构建或运行时崩溃/渲染异常 | 固定布局 + TabControl 降级 |
| `CommandBar` 功能密度不足 | 工具栏按钮过多导致拥挤 | 自研简化 Ribbon 或等待 `Ribbon.Avalonia` 12.x 升级 |
| `FluentAvalonia` 主题冲突 | 深色工业主题与 Fluent 主题不兼容 | 仅使用其 `CommandBar` 控件,单独覆盖样式 |

## 附录 E: Faceplate 参数接口规范与 CodeGen 兼容性

### E.1 设计目标

在 Phase R2 启动前,明确 Faceplate(参数化复合组件)的参数接口、实例模型和代码生成集成方式,确保:

1. 模板参数化能力完整覆盖工业常见场景(文本、颜色、数值、变量绑定)
2. CodeGen 在生成 C 代码前能将 Faceplate 实例展开为等价的普通控件列表
3. 展开后的输出与不使用 Faceplate 手工布局的输出等价,便于验证
4. 现有 `.hmi.json` 项目文件格式向后兼容

### E.2 参数接口模型

```csharp
// 参数方向
public enum FaceplateParameterKind { Input, Output, InOut }

// 参数类型
public enum FaceplateParameterType
{
    String,      // 任意文本
    Number,      // 浮点数,可带范围/步进
    Boolean,     // true/false
    Color,       // #RRGGBB 颜色码
    VariableId   // 变量绑定引用
}

// 模板参数定义
public class FaceplateParameter
{
    public string Name { get; set; } = "";           // 占位符中使用的标识符
    public string DisplayName { get; set; } = "";    // UI 显示名称
    public FaceplateParameterKind Kind { get; set; } = FaceplateParameterKind.Input;
    public FaceplateParameterType Type { get; set; } = FaceplateParameterType.String;
    public string? DefaultValue { get; set; }        // 默认值(字符串形式)
    public string? Description { get; set; }
}

// 实例中的参数绑定
public class FaceplateBinding
{
    public string ParameterName { get; set; } = "";
    public string Value { get; set; } = "";
}
```

### E.3 ControlTemplate 扩展

在现有 `ControlTemplate.cs` 上新增字段,保持向后兼容:

```csharp
public class ControlTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "New Template";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "通用";

    // 原有字段:模板包含的控件定义(相对坐标)
    public ObservableCollection<HmiControl> Controls { get; set; } = new();

    // 新增:参数定义。为空时退化为简单组合模板
    public ObservableCollection<FaceplateParameter> Parameters { get; set; } = new();

    // 新增:版本戳,模板保存时自动递增
    public int Version { get; set; } = 1;
}
```

### E.4 HmiControl 扩展(Faceplate 实例)

复用 `HmiControl`,新增可选字段表示 Faceplate 实例:

```csharp
public class HmiControl
{
    // ... 现有字段 ...

    // 当 TemplateId 非空时,表示这是一个 Faceplate 实例
    public string? TemplateId { get; set; }

    // null 表示跟随模板最新版本;非 null 表示固定版本
    public int? TemplateVersion { get; set; }

    // 参数绑定列表
    public ObservableCollection<FaceplateBinding> ParameterBindings { get; set; } = new();
}
```

### E.5 参数占位符语法

模板内部控件通过 `"{{Parameters.参数名}}"` 引用参数,支持出现在以下字符串属性:

- `Text`
- `BgColor`
- `TextColor`
- `BorderColor`
- `VariableBindingId`

**示例**:

```json
{
  "type": "Label",
  "text": "{{Parameters.Title}}",
  "bgColor": "{{Parameters.RunColor}}"
}
```

绑定值直接替换占位符,不添加额外引号或转义。CodeGen 阶段按属性语义校验值合法性(如 `BgColor` 必须是有效颜色码)。

### E.6 CodeGen 集成 —— Faceplate 展开器

在 `CodeGenerator.GenerateScreenCode` 之前插入展开阶段:

```csharp
public static class FaceplateExpander
{
    public const int MaxExpansionDepth = 8;

    public static void Expand(HmiScreen screen, ITemplateResolver resolver)
    {
        var expanded = new ObservableCollection<HmiControl>();
        foreach (var control in screen.Controls)
        {
            if (!string.IsNullOrEmpty(control.TemplateId))
            {
                var template = resolver.Resolve(control.TemplateId, control.TemplateVersion);
                var instantiated = Instantiate(template, control, 0, resolver);
                foreach (var c in instantiated) expanded.Add(c);
            }
            else
            {
                expanded.Add(control);
            }
        }
        screen.Controls = expanded;
    }

    private static IEnumerable<HmiControl> Instantiate(
        ControlTemplate template, HmiControl instance,
        int depth, ITemplateResolver resolver)
    {
        if (depth > MaxExpansionDepth)
            throw new InvalidOperationException($"Faceplate 嵌套展开超过最大深度 {MaxExpansionDepth}");

        var bindings = instance.ParameterBindings.ToDictionary(b => b.ParameterName, b => b.Value);

        foreach (var source in template.Controls)
        {
            var clone = CloneControl(source);
            ApplyBindings(clone, bindings);

            // 坐标平移到实例位置
            clone.X += instance.X;
            clone.Y += instance.Y;

            // 嵌套 Faceplate 递归展开
            if (!string.IsNullOrEmpty(clone.TemplateId))
            {
                var nestedTemplate = resolver.Resolve(clone.TemplateId, clone.TemplateVersion);
                foreach (var nested in Instantiate(nestedTemplate, clone, depth + 1, resolver))
                    yield return nested;
            }
            else
            {
                yield return clone;
            }
        }
    }
}
```

展开后,`screen.Controls` 中只包含普通控件,`CodeGenerator` 无需感知 Faceplate。

### E.7 兼容性策略

| 场景 | 处理方式 |
|---|---|
| 旧项目无 `TemplateId` | 忽略,行为不变 |
| 模板 `Parameters` 为空 | 退化为简单组合模板,仅复制控件 |
| 实例绑定缺失参数 | 使用参数默认值;无默认值则报错,阻止生成 |
| 模板版本低于实例固定版本 | 报错,提示模板不存在 |
| 嵌套深度超过限制 | 报错,提示可能存在循环依赖 |
| 占位符出现在不支持属性 | 保留原字符串,CodeGen 可能生成错误代码,需在验证阶段拦截 |

### E.8 验证阶段

在 CodeGen 之前增加 `FaceplateValidator`:

1. 检查所有 `TemplateId` 可解析
2. 检查所有 `Input` 参数有绑定或默认值
3. 检查占位符引用的参数存在于模板定义中
4. 检查颜色/数值类型参数的绑定值格式合法
5. 检测循环依赖(通过模板引用图 DFS)

### E.9 Fitness Functions

| # | 属性 | 指标 | 阈值/规则 | 测量源 | 评估频率 | 失败响应 | 本地检查路径 |
|---|---|---|---|---|---|---|---|
| FF-FP1 | 展开等价性 | 相同布局的 Faceplate 实例展开后与普通控件生成的 C 代码差异 | 除控件 ID 和注释外,功能代码一致 | `CodeGenTests` 对比展开前后输出 | 每次提交 | 阻止合并 | `dotnet test --filter Faceplate` |
| FF-FP2 | 最大嵌套深度 | 展开时递归深度 | ≤ 8 | `FaceplateExpander` 运行期断言 | 每次导出 | 抛出异常并中止生成 | `dotnet test --filter Faceplate` |
| FF-FP3 | 向后兼容 | 旧 `.hmi.json` 加载 | 无 `TemplateId`/`ParameterBindings` 的项目可正常加载和生成 | `ProjectSerializerTests` | 每次提交 | 阻止合并 | `dotnet test --filter ProjectSerializer` |
| FF-FP4 | 参数绑定完整 | 缺失 `Input` 参数绑定 | 0 个缺失 | `FaceplateValidator` | 每次导出 | 错误提示,中止生成 | `dotnet test --filter Faceplate` |
| FF-FP5 | 循环依赖检测 | 模板引用图中环数量 | 0 个环 | `FaceplateValidator` | 每次导出 | 错误提示,中止生成 | `dotnet test --filter Faceplate` |

### E.10 风险登记

| # | 风险 | 可能性 | 影响 | 缓解 | 记录 |
|---|---|---|---|---|---|
| FP1 | 参数类型不匹配导致生成错误 C 代码 | 中 | 高 | 在 `FaceplateValidator` 中校验类型格式;颜色/数值使用专用编辑器 | 附录 E.8 |
| FP2 | 模板循环依赖导致无限递归 | 低 | 高 | 限制最大展开深度;导出前检测模板引用图环 | FF-FP2 / FF-FP5 |
| FP3 | 模板修改后实例未同步,导致运行时行为不一致 | 中 | 中 | 默认跟随最新版本;项目打开时检测版本差异并提示 | ADR-002 决策 |
| FP4 | 大量 Faceplate 实例展开后控件数量爆炸,影响 CodeGen 性能 | 中 | 中 | 限制单次展开最大控件数(如 10,000);在验证阶段预估 | Phase R2 实现时评估 |
| FP5 | 占位符语法与现有文本内容冲突 | 低 | 中 | 使用 `"{{Parameters.X}}"` 双花括号语法,与常规文本区分;提供转义机制 | 设计阶段确定 |
