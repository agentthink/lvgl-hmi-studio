# HMI Editor 主流对标分析与优化报告

> **版本**: v2.1.0 现状评估
> **日期**: 2026-07-30
> **对标对象**: WinCC Unified / FactoryTalk Optix / AVEVA / Ignition Perspective / EasyBuilder Pro / TwinCAT HMI / mapp View
> **研究范围**: 2025-2026 最新能力与趋势

---

## 一、项目现状评估

### 1.1 技术基线

| 维度 | 现状 |
|---|---|
| 框架 | .NET 10 + Avalonia 12.1 + C# |
| 目标平台 | LVGL v9 on ARM (Allwinner T113 / Rockchip RK3506 / STM32F767) |
| 解决方案 | 16 个项目 (12 src + 4 tests) |
| 测试 | 310 用例, 100% 通过, 0 警告 |
| 核心交付 | LVGL v9 C 代码生成 (Scriban 模板) |

### 1.2 已建成能力清单

以下能力已落地实现,构成项目核心竞争力:

**画布与交互**
- Ctrl+滚轮缩放(以鼠标为中心)、中键平移、坐标补偿
- 8 手柄缩放、对齐参考线吸附、网格吸附
- 多选(Ctrl+点击切换)、框选(Ctrl+拖拽)、包围盒显示
- 撤销/重做(Command 模式双栈, Move/Add/Delete/PropertyChange)

**控件与组件**
- 18 种控件类型(含 Gauge/Knob/LED/Spinner/Table/TabView/Keyboard/Line)
- 每种控件有 LVGL v9 API 映射(`lv_arc_create`/`lv_led_create` 等)
- Faceplate 参数化复合组件系统:`ControlTemplate` + `TemplateParameter` + `FaceplateExpander`
- 模板版本戳、参数占位符 `{{Parameters.X}}`、循环依赖检测、最大展开深度 8 层

**子系统**
- 动画编辑器(7 种缓动、关键帧时间轴、播放预览)
- 主题/样式编辑器(10 色调色板、全局尺寸)
- 屏幕管理器(多屏幕 CRUD)
- 国际化(zh-CN/en-US, fallback 机制)
- 变量与动作系统(17 个系统变量、SwitchScreen/SetVariable/ExecuteScript 等动作)
- 报警引擎、趋势配置、配方管理、Lua 脚本校验、用户认证
- 离线仿真(变量仿真 + 通信仿真)
- 资源估算引擎(`ResourceEstimator`, 17 种控件 Flash/RAM 系数表)

**基础设施**
- 硬件抽象层:内置 Allwinner/Rockchip + DLL 热插拔(Tier 2) + JSON 配置驱动(Tier 3)
- 通信驱动:Modbus RTU/TCP + MQTT
- Linux ARM64 NativeAOT 交叉编译 + WSL2 工作流
- 构建/清理脚本(进程隔离 + 残余清理)

### 1.3 半完成项(依赖已就绪,实现未落地)

| 项 | 依赖状态 | 实现状态 | 证据 |
|---|---|---|---|
| Dock 可停靠布局 | `Dock.Avalonia 12.1.0` 已入 csproj | MainWindow 仍是固定 Grid + StackPanel | `MainWindow.axaml` 无 `dock:DockControl` |
| CommandBar 工具栏 | `FluentAvaloniaUI 3.0.2` 已入 csproj | 工具栏仍为扁平 Button 列表 | 使用 `Button` 而非 `fa:CommandBarButton` |
| 类型化属性编辑器 | `ColorPickerPopup.cs` 已存在 | 属性面板仍大量使用 TextBox | `PropertyPanelView` 未用 NumericUpDown |
| 资源仪表盘 | `ResourceEstimator` 已实现 | 状态栏未集成 Flash/RAM 进度条 | `MainWindow` 状态栏仅文本 |

---

## 二、主流 HMI 软件能力矩阵(2026 更新)

### 2.1 对标对象演进

本年度研究纳入两个重要变化:

1. **新一代平台上位**:WinCC Unified(替代经典 WinCC Advanced)、FactoryTalk Optix(替代 FactoryTalk View SE/ME)、AVEVA System Platform OMI 成为各厂商主推方向,全面转向 HTML5/Web 技术栈。
2. **AI 与 Git 进入工业工程**:TIA Portal V21(2025 年底)引入 Engineering Copilot(AI 生成画面/代码)与原生 Git 集成,标志工业 HMI 工程进入"可 diff、AI 辅助"时代。

### 2.2 核心能力对比矩阵

| 能力维度 | WinCC Unified | FactoryTalk Optix | AVEVA OMI | Ignition Perspective | EasyBuilder Pro | TwinCAT HMI | **本项目 v2.1.0** |
|---|---|---|---|---|---|---|---|
| **工程模型** | 项目树+Faceplate | 面向对象+C# | Galaxy 对象仓库 | View+Component+Props | 画面+元件 | VS+WYSIWYG | **屏幕+控件+模板** |
| **面板布局** | 可停靠+保存 | 可停靠 | 可停靠 | Web 响应式 | 固定三栏 | VS停靠 | **固定四栏(待 Dock)** |
| **工具栏** | Ribbon | Ribbon | 工具条 | Web 工具栏 | 工具条 | VS工具栏 | **扁平按钮(待 CommandBar)** |
| **控件库** | 80+ Faceplate | 60+ Global Obj | 50+ SmartSymbol | 40+ Template | 30+ | 20+ Widget | **18 种+Faceplate** |
| **属性编辑** | 分类+类型编辑器 | 分类+类型 | 属性窗口 | 属性+扩展 | 类型化编辑器 | 属性配置 | **Expander 分组+TextBox** |
| **画布交互** | 拖放+对齐+层级 | 拖放+对齐 | 拖放+对齐 | 拖放+响应式 | 拖放+对齐 | 拖放+属性 | **点击添加+对齐+缩放** |
| **层级管理** | 层管理器+Z序 | 层+锁定 | 层+锁定 | 层+锁定 | Z序 | 层 | **无** |
| **变量绑定** | PLC+HMI+结构变量 | 标签+UDT | UDT+对象属性 | UDT+标签+表达式 | 地址+标签 | ADS符号+OPC UA | **VariableId+Mode** |
| **复合组件** | Faceplate(参数化) | Global Object | SmartSymbol+UDT | Template+Embedded View | 宏 | Widget | **Faceplate(已实现)** |
| **脚本系统** | JavaScript | C# | QuickScript.NET | Jython | 宏(Basic) | JS+C++/.NET | **Lua(校验)** |
| **表达式引擎** | 动态值+脚本 | 表达式 | 表达式 | 绑定表达式 | — | 表达式 | **无** |
| **仿真/预览** | PLCSIM+HMI仿真 | Optix仿真 | WindowViewer | Designer实时预览 | 离线仿真(LB-12358) | 实时预览 | **变量+通信仿真** |
| **设计时数据预览** | 有 | 有 | 有 | 有 | 有 | 有 | **无** |
| **资源管理** | 资源管理器+预算 | 资源浏览器 | 资源管理 | 项目资源 | 图片库 | 资源树 | **估算引擎(无UI集成)** |
| **多语言** | 项目语言+导出 | 语言切换 | 语言切换 | 语言切换 | 多语言 | 多语言 | **中/英 JSON** |
| **SVG/矢量导入** | 动态SVG | SVG | SVG转对象 | SVG组件 | 位图 | SVG | **无** |
| **版本控制** | Git(V21原生) | 云协作 | Galaxy版本 | 项目继承 | 项目版本 | VS Git | **无(文件已JSON化)** |
| **AI 辅助** | Engineering Copilot | 规划中 | — | 社区探索 | — | AI 助手 | **无** |
| **审计/合规** | Audit | Audit | Audit | Audit Journal | 事件登录 | TF2400(FDA 21 CFR) | **用户认证(无审计)** |
| **通信** | OPC UA+MQTT(V21) | OPC UA+MQTT | OPC UA | OPC+MQTT传输 | 多协议 | OPC UA+ADS+DB | **Modbus+MQTT** |
| **代码生成** | 原生编译 | 原生编译 | 运行时引擎 | 运行时引擎 | 编译下载 | 运行时引擎 | **LVGL C 代码生成** |

### 2.3 交互模式对比速查

| 交互模式 | WinCC | FactoryTalk | Ignition | EasyBuilder | TwinCAT | 本项目 |
|---|---|---|---|---|---|---|
| 控件添加 | 拖放 | 拖放 | 拖放 | 拖放 | 拖放 | **点击** |
| 属性编辑 | 面板+类型化 | 面板+类型化 | 面板+类型化 | 双击+类型化 | 属性配置 | **面板+TextBox** |
| 多选 | 框选+Ctrl | 框选+Ctrl | 框选+Shift | 框选 | 框选 | **框选+Ctrl** |
| 缩放 | Ctrl+滚轮 | Ctrl+滚轮 | 按钮+滚轮 | 按钮 | — | **Ctrl+滚轮** |
| 平移 | 中键/空格 | 中键 | 空格+拖拽 | 滚轮 | — | **中键** |
| 层管理 | 层面板 | 层面板 | 层面板 | Z序面板 | 层 | **无** |
| 数据预览 | 设计时 | 设计时 | 设计时 | 设计时 | 设计时 | **无** |

---

## 三、差距分析

### 3.1 差距分级(按影响排序)

将差距分为三档:P0(影响第一印象与核心效率)、P1(影响专业度与复用)、P2(前瞻能力与现代化)。

#### P0 — 第一印象与核心交互(必须补齐)

| # | 差距 | 当前状态 | 主流对标 | 影响 |
|---|---|---|---|---|
| G1 | **工具栏未专业化** | 扁平 Button 列表,依赖已就绪但未用 CommandBar | WinCC Ribbon / TwinCAT VS 工具栏 | 第一印象差,功能密度低 |
| G2 | **无拖放式控件添加** | 点击工具箱在画布中心生成 | 所有主流均支持拖放 | 定位需二次拖拽,效率低 |
| G3 | **属性面板缺类型化编辑器** | 数值/颜色/布尔均用 TextBox | EasyBuilder/WinCC 类型编辑器 | 易出错,效率低 |
| G4 | **状态栏无资源仪表盘** | ResourceEstimator 已实现但未集成 UI | WinCC 编译报告 | 嵌入式部署易超限 |
| G5 | **Dock 布局未启用** | 依赖已入 csproj,布局仍固定 | WinCC/FactoryTalk 可停靠 | 无法适应不同工作流 |

#### P1 — 专业度与复用(应补齐)

| # | 差距 | 当前状态 | 主流对标 | 影响 |
|---|---|---|---|---|
| G6 | **无层管理/Z序控制** | 无锁定/隐藏/层级面板 | WinCC/FactoryTalk 层管理 | 复杂画面难管理 |
| G7 | **无设计时数据预览** | 控件显示静态文本 | Ignition/EasyBuilder 预览 | 设计阶段无法验证显示 |
| G8 | **变量绑定过于简单** | 仅 VariableId+Mode,无地址/表达式/UDT | FactoryTalk 标签+UDT | 无法直接对接 PLC 结构 |
| G9 | **无资源管理器面板** | 图片/字体/语言无统一管理 UI | WinCC 资源管理器 | 资源散落,难维护 |
| G10 | **无导航流设计器** | 屏幕跳转靠脚本 | WinCC 导航编辑器 | 画面流不直观 |
| G11 | **无交叉引用浏览器** | 变量/画面/脚本引用不可视 | WinCC 交叉引用 | 影响分析困难 |
| G12 | **脚本仅校验无引擎** | Lua 校验+片段,无运行时执行 | Ignition Jython 事件脚本 | 无法在编辑器内验证脚本效果 |

#### P2 — 前瞻能力与现代化(2026 新趋势)

| # | 差距 | 当前状态 | 主流对标 | 影响 |
|---|---|---|---|---|
| G13 | **无表达式引擎** | 属性无法写 `tag>100?red:green` | Ignition/WinCC 表达式绑定 | 动态视觉需脚本实现 |
| G14 | **无 SVG/矢量导入** | 仅位图 | WinCC/AVEVA SVG 转对象 | 复杂图形难复刻,Flash 占用大 |
| G15 | **无 AI 辅助生成** | 全手动布局 | TIA V21 Engineering Copilot | 效率瓶颈,门槛高 |
| G16 | **无 Git 工作流支持** | 文件已 JSON 化,但无 diff 友好性保障 | TIA V21 原生 Git | 多人协作/回滚困难 |
| G17 | **无审计追踪** | 有用户认证,无操作审计 | TwinCAT TF2400 / B&R mapp Audit | 不满足合规场景 |
| G18 | **无响应式/多分辨率布局** | 固定坐标 | TwinCAT/WinCC Unified 自适应 | 多屏适配需重复设计 |
| G19 | **无 UDT 结构化标签** | 变量为扁平表 | AVEVA/WinCC UDT | 电机等复合对象需逐字段绑定 |
| G20 | **图片无自动优化** | 无压缩/量化 | 通用 | Flash 易超限 |

### 3.2 SWOT 分析

**优势 (Strengths)**
- 嵌入式 C 代码生成是独有差异化:主流工具多为运行时引擎或专有编译,本项目生成可移植 LVGL v9 C 代码,适配 MCU 级目标
- 架构分层清晰:UI/Core/CodeGen/Hardware/Communication 解耦,扩展性强
- 硬件抽象完整:Allwinner/Rockchip/STM32 + 插件机制 + JSON 配置驱动
- Faceplate 系统已落地:参数化复合组件对标 WinCC Faceplate
- 资源估算引擎已实现:嵌入式感知能力强于多数桌面工具
- 测试覆盖扎实:310 用例,0 警告,工程化程度高
- 项目文件 JSON 化:天然适合 Git diff(虽未正式支持工作流)

**劣势 (Weaknesses)**
- UI 壳层停留在原型级:依赖已就绪但 CommandBar/Dock 未实际启用,第一印象落后
- 交互效率低:无拖放、无层管理、属性编辑纯文本
- 数据绑定浅:无表达式、无 UDT、无地址直绑
- 设计时无数据预览:无法在编辑阶段验证运行效果
- 脚本能力弱:仅校验无执行,无法闭环验证

**机会 (Opportunities)**
- AI 辅助生成是弯道超车点:主流工具刚起步(TIA V21 Copilot),本项目可借代码生成优势率先实现"自然语言→LVGL 画面"
- 嵌入式蓝海:主流工具聚焦 PC/Web 运行时,MCU 级 LVGL 代码生成赛道竞争少
- Git 友好文件格式:JSON 已就绪,补齐工作流即可领先传统二进制工程工具

**威胁 (Threats)**
- LVGL 生态自身在演进(LVGL 9.x→10),API 变动风险
- 主流工具若下沉到嵌入式代码生成(如 WinCC Unified 已支持 HTML5→可能延伸)
- 嵌入式目标碎片化:不同 MCU 的 Flash/RAM/显示驱动差异大

---

## 四、对标结论

### 4.1 定位坐标

本项目在"嵌入式 LVGL C 代码生成"细分赛道无直接竞品,应定位为:

> **面向 MCU 级嵌入式 HMI 的专业级可视化编辑器** — 对标 WinCC/FactoryTalk 的工程体验,输出可移植 LVGL v9 C 代码

### 4.2 核心竞争力护城河

需持续强化的三个护城河:
1. **代码生成质量** — 生成代码的可读性、资源效率、与手写 LVGL 等价
2. **嵌入式感知** — 设计时即反馈资源占用,图片自动优化,字体子集化
3. **工程体验** — 拖放 + 类型编辑器 + 层管理 + 设计时预览,达到主流工具水准

### 4.3 优先级判断

- **P0 必须优先**:工具栏/Dock/拖放/类型编辑器/资源仪表盘是"专业感"门槛,依赖已就绪,投入产出比最高
- **P1 紧随其后**:层管理/数据预览/UDT/资源管理器补齐专业度
- **P2 战略投入**:表达式引擎/AI 生成/SVG 导入是差异化与未来竞争力,但可分阶段

### 4.4 与既有 ADR 的关系

本报告基于既有 `2026-07-30-hmi-mainstream-comparison-refactor-adr.md`,在其基础上:
- 更新对标矩阵(纳入 WinCC Unified/Optix/TwinCAT,移除过时定位)
- 校准实际实现状态(区分"已落地/半完成/未开始")
- 新增 2026 趋势差距(AI/Git/UDT/SVG/表达式/审计)
- 重新排序优先级(以实际状态而非假设为依据)

详细开发计划见 `2026-07-30-hmi-optimization-dev-plan.md`。
