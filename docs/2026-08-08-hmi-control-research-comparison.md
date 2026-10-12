# 组态控件系统性调研与主流 HMI 软件对比分析报告

> **日期**: 2026-08-08
> **调研对象**: 本应用（智数 HMI 编辑器，LVGL v9 嵌入式 C 代码生成）内部组态控件系统
> **对标软件**: WinCC Unified / FactoryTalk View (Optix) / Ignition Perspective / Wonderware InTouch / AVEVA Plant SCADA (Citect) / MCGS / 组态王 (KingView)
> **目的**: 为后续组态控件的选型或自主研发提供数据支持与决策依据

---

## 一、调研背景与方法

### 1.1 调研目标

1. 系统梳理本应用内部组态控件的**类型、功能特性、交互方式、可视化效果、性能指标、兼容性、开发难度及资源占用**。
2. 选取 7 款市场主流 HMI 软件进行对比，重点比较组态控件的**设计理念、功能完整性、易用性、扩展性、数据处理能力及行业应用案例**。
3. 形成结构化对比报告，明确各类组态控件的**优势与不足**，为选型或自主研发提供决策依据。

### 1.2 调研方法

- **内部调研**: 基于 `/home/chen/Git/zhishu-hmi` 代码库实际状态核对（模型、渲染、代码生成、运行时、固件），非文档声明。
- **外部调研**: 结合 2025-2026 各厂商公开技术文档、用户社区反馈与行业应用案例。

---

## 二、本应用内部组态控件调研

### 2.1 控件类型全览（19 种）

核心枚举定义于 [HmiControlType.cs](file:///home/chen/Git/zhishu-hmi/src/HmiEditor.Core/Models/HmiControlType.cs)，LVGL 映射见 [ControlMappingRegistry.cs](file:///home/chen/Git/zhishu-hmi/src/HmiEditor.Core/Mapping/ControlMappingRegistry.cs)。

| 类别 | 控件类型 | LVGL v9 映射 | 定位 |
|---|---|---|---|
| 基础控件 | Screen | `lv_obj_create` | 容器 |
| | Button | `lv_button_create` | 按钮 |
| | Label | `lv_label_create` | 文本标签 |
| | Slider | `lv_slider_create` | 滑块 |
| | Switch | `lv_switch_create` | 开关 |
| | CheckBox | `lv_checkbox_create` | 复选 |
| | Image | `lv_image_create` | 图片 |
| | Bar | `lv_bar_create` | 进度条 |
| | Dropdown | `lv_dropdown_create` | 下拉框 |
| | TextArea | `lv_textarea_create` | 文本输入 |
| Wave 2 增强 | Gauge | `lv_arc_create` | 仪表盘 |
| | Knob | `lv_arc_create` | 旋钮 |
| | Keyboard | `lv_keyboard_create` | 软键盘 |
| | Led | `lv_led_create` | LED 指示灯 |
| | Spinner | `lv_spinner_create` | 加载动画 |
| | TabView | `lv_tabview_create` | 选项卡 |
| | Line | `lv_line_create` | 线条 |
| | Table | `lv_table_create` | 表格 |
| I6 矢量 | Svg | `lv_canvas_create`（专用分支） | SVG 矢量图形 |

**尚未实现**（对标主流工具存在，本应用缺失）: Tank（罐体）、Pipe（管道）、Valve（阀门）、Meter（流量计/电表）等**过程行业专用图形控件**——这些在 WinCC/Wonderware 中通常作为图形符号库存在。

### 2.2 功能特性

**基础样式体系**（[HmiControl.cs](file:///home/chen/Git/zhishu-hmi/src/HmiEditor.Core/Models/HmiControl.cs)）:
- 几何：X/Y/Width/Height、层管理（IsLocked/IsVisible/LayerIndex）、透明度 Opacity
- 样式：BgColor/TextColor/Radius/BorderWidth/BorderColor/IndicatorColor
- **四态视觉状态**：Normal/Pressed/Checked/Disabled 状态覆盖（对标 LV_STATE_*）
- **值显示格式化**：ValueDisplayType（Auto/Bool/Int/Float/String）、ScaleFactor、小数位、前后缀

**数据绑定与联动模型**（[RuntimeBindingEngine.cs](file:///home/chen/Git/zhishu-hmi/src/HmiEditor.Core/Services/RuntimeBindingEngine.cs)）:
- 多绑定集合（VariableBindings）+ 多动作集合（Actions），替代旧单字段模型
- BindingMode（ReadWrite/ReadOnly/WriteOnly）
- **工程值↔原始值换算**：`eng = raw * scale + offset`
- **UDT 结构化标签**：点号寻址 `motor1.speed` → 运行时标签表 → CodeGen `TAG_motor1_speed`
- **表达式绑定**：自研递归下降解析器（算术/比较/逻辑/三元），可转译 C 表达式

**动作系统**（[HmiAction.cs](file:///home/chen/Git/zhishu-hmi/src/HmiEditor.Core/Models/HmiAction.cs)）:
- 写变量：SetVariable/SetBit/ResetBit/ToggleBit/MomentaryOn/MomentaryOff
- 画面：SwitchScreen；脚本：ExecuteScript；系统：ShowAlarms/ShowTrends/Login/Logout
- 触发事件：OnClick/OnValueChange/OnPress/OnRelease；按控件类型分发可用动作

**动画系统**（`Models/Animation/`）:
- 7 种缓动：Linear/EaseInOut/EaseIn/EaseOut/Bounce/Overshoot/Step
- 关键帧时间轴（时间百分比+逐帧独立缓动）
- 动画属性：X/Y/Width/Height/Opacity/Radius/BorderWidth/Value
- 触发方式：OnLoad/OnClick/OnValueChange/OnPress/OnRelease；循环次数（-1 无限）

**报警系统**（`Models/Alarm/`）:
- 5 种触发条件：HighLimit/LowLimit/BitTrigger/StateChange/Deviation
- 死区防抖动、延迟触发、位偏移
- 4 级严重级别：Info/Warning/Critical/Emergency（含确认策略差异）
- 状态机：Inactive→Active→Acknowledged→Returned
- 报警分组、报警记录、关联脚本/控件联动

**趋势与数据记录**（`Models/Trend/`）:
- 时间窗口、环形缓冲区（默认 600 点）、多曲线笔（TrendPen）
- 图例/网格/坐标轴显示开关、实时刷新
- 数据记录：采样间隔、满策略（OverwriteOldest/StopWhenFull）、CSV 导出、压缩（RLE/差值编码）

**脚本系统**（[HmiEditor.Scripting](file:///home/chen/Git/zhishu-hmi/src/HmiEditor.Scripting)）:
- Lua 脚本（MoonSharp 校验）、4 种预置模板、脚本绑定
- 沙箱执行（SandboxExecutor）支持 `hmi_read`/`hmi_write`/`hmi_set_control_property`
- AI 辅助：自然语言→脚本生成、Lua 补全

> **后续复核（2026-09-20）**：本节有两处与**实际不符**，原文保留作历史快照。
> ① **程序集归属错了** —— 脚本功能的模型 / 服务 / UI / 代码生成全在 **`HmiEditor.Core`** 与
> `HmiEditor.CodeGen`（`HmiScript`·`ScriptBinding`·`ScriptTemplate` / `ScriptEngineService`·
> `ScriptSandboxExecutor` / `ScriptEditorView` / `lvgl_v9_script.scriban`），**不在** `HmiEditor.Scripting`；
> ② **「MoonSharp 校验」从未接入产品** —— 编辑器实际走的是 `ScriptEngineService.Validate` 的三条
> **弱判据**（名称非空 / 内容非空 / 代码含 `function `），MoonSharp 那份只会被 `HmiEditor.Scripting` 内部用到，
> 而该程序集全仓 **0 条引用使用**（唯一引用是 App 里引了从不用的 `ProjectReference`）⇒ 已按死代码登记
> （登记表 **#36**）。取证见 `docs/2026-09-20-scripting-assembly-triage.md`。
>
> **再复核（2026-09-20 同日，稍后）** —— 上条 ② **已再次失效**：当天拍板「编辑器侧 Lua 校验做强」后，
> MoonSharp 真解析**搬进了 `HmiEditor.Core`**（`Core/Services/LuaSyntaxValidator.cs`），
> 由 `ScriptEngineService.Validate` **真实调用**，`HmiEditor.Scripting` 程序集整机下架。
> ⇒ **MoonSharp 校验现在是产品能力**，本节 94 行那半句「（MoonSharp 校验）」**恢复为正确陈述**。
> 准确措辞：编辑器侧 Lua 校验 = **MoonSharp 真解析（Lua 5.2 方言）+ 行号 + 中文说明 + 方言提示**；
> 原先被点名的「弱判据」只剩 `NO_FUNCTION` 一项，且它已升级为**剥注释后查 `\bfunction\b`**（防注释里的假函数骗过）。
> 实施与读数见 `docs/2026-09-20-lua-syntax-validation.md`；登记表 **#36 已闭环**。

**安全与权限**:
- 10 项位掩码权限 + 0-15 级角色模型（Operator=3/Engineer=7/Administrator=15）
- SHA-256+Salt 密码哈希、连续失败锁定、过期时间
- 审计日志（JSONL append-only 持久化 + 面板展示）
- 4 种操作模式：Designer/Operator/Maintenance/ReadOnly

**配方管理**: 分组/版本/条目校验（最小/最大值/单位）/预置保护

**国际化**: zh-CN/en-US 双语言 JSON 资源 + 切换机制 + 本地化工具链

### 2.3 交互方式

| 交互维度 | 现状 |
|---|---|
| 控件添加 | 工具箱拖放（DragDrop）+ 画布定位 |
| 画布操作 | Ctrl+滚轮缩放（以鼠标为中心）、中键平移、8 手柄缩放、对齐参考线吸附、网格吸附、多选/框选 |
| 属性编辑 | 类型化编辑器：颜色选择器/复选框/下拉/NumericUpDown/表达式输入/变量选择 |
| 层级管理 | 层锁定/隐藏/层级面板 |
| 设计时预览 | 预览模式（PreviewValueResolver 纯函数解析，不修改模型） |
| 交叉引用 | 变量/屏幕/脚本三类反查面板 |
| 导航流设计 | 可视化导航流设计器 |
| 脚本调试 | F6 沙箱调试运行（Lua 回调翻译为沙箱语句，输出日志与变量） |
| 验证闭环 | 生成代码静态质量门（Ctrl+Shift+V）+ 一键导出模拟器工程 |

### 2.4 可视化效果

- **渲染引擎**：编辑器端 Avalonia 设计器渲染 + 运行时 LVGL v9 C 代码渲染
- **矢量能力**：SVG 解析器（rect/circle/ellipse/line/polyline/polygon/path）+ 画布矢量渲染 + CodeGen 生成 `lv_canvas` 绘制序列
- **主题系统**：10 色调色板、全局尺寸、四态样式覆盖
- **自适应布局**：None/Fit（等比居中）/Fill（拉伸），依据 display 实际分辨率重算
- **设计时数据预览**：模拟值/变量仿真驱动控件显示

### 2.5 性能指标与资源占用

| 维度 | 现状 |
|---|---|
| 资源估算 | ResourceEstimator（17 种控件 Flash/RAM 系数表）+ 状态栏 Flash/RAM 仪表盘 |
| 数据缓冲 | 环形缓冲区策略（趋势 600 点/数据记录满策略） |
| 图片优化 | ImageOptimizer 图片优化服务 |
| 字体优化 | FontSubsetAnalyzer 字符集扫描 + 子集化估算（中文 HMI Flash 降一个量级） |
| 生成代码优化 | CodeGenOptimization 策略（Size/Speed/Readability 三档） |
| 固件加速 | G2D 硬件加速驱动、帧缓冲、内存管理（sunxifb/sunximem/g2d_driver） |
| 目标平台 | Allwinner T113-S3（ARM Cortex-A7, 800x480, 128MB RAM, 16MB Flash） |

### 2.6 兼容性

- **平台**：编辑器跨平台（Avalonia：Win/macOS/Linux）；固件目标 ARM Linux
- **输出形态**：Executable（完整 C 程序）/ SharedLibrary（`libhmi_ui.so`，dlopen 热加载，OTA 热更新）
- **构建系统**：CMake / Makefile / PlatformIO 三选一
- **LVGL 兼容**：LvglApiCompatChecker 静态检查 LVGL 9.x API 兼容性；ABI 版本 + LVGL 版本双重校验
- **工程文件**：JSON（`.hmi.json`），天然 Git 友好；旧字段自动迁移
- **通信协议**：Modbus RTU/TCP、MQTT、自包含轻量 OPC UA 客户端（无签名/加密、轮询模式）

### 2.7 开发难度评估（内部视角）

| 维度 | 评级 | 说明 |
|---|---|---|
| 新增控件类型 | ★★☆☆☆（易） | 枚举 + LVGL 创建函数映射 + 属性面板分支即可接入 |
| 复合组件 | ★★★☆☆（中） | Faceplate 参数化模板 + 展开器（最大深度 8 层） |
| 表达式/动画 | ★★★☆☆（中） | 自研解析器/动画模型已成型，扩展缓动需加枚举与求值 |
| 过程图形控件 | ★★★★☆（难） | Tank/Pipe/Valve 需图形符号库 + 动态填充算法，当前完全缺失 |
| 运行时适配 | ★★★☆☆（中） | 需同步修改固件侧 C 代码生成模板 |

---

## 三、主流 HMI 软件对比分析

### 3.1 平台概览

| 平台 | 开发商 | 架构 | 设计理念 |
|---|---|---|---|
| **WinCC Unified** | 西门子 | Web 原生（HTML5），TIA Portal 集成 | 统一工程环境，ISA-101 高性能 HMI |
| **FactoryTalk View/Optix** | 罗克韦尔 | Windows C/S + Optix（HTML5） | 设备驱动型设计，与 ControlLogix 深度绑定 |
| **Ignition Perspective** | Inductive Automation | Java 网关 + Web 客户端 | 开放标签模型，标签与设备解耦 |
| **Wonderware InTouch** | AVEVA | Windows C/S，ArchestrA 框架 | 复合对象驱动，图形建模强 |
| **AVEVA Plant SCADA** | AVEVA | Windows C/S，Cicode | 对象+标签混合模型，报警管理完善 |
| **MCGS** | 昆仑通态 | Windows C/S | 模块化组态，三层架构（设备/用户/策略） |
| **组态王 KingView** | 亚控科技 | Windows C/S | 变量驱动，4500+ 免费驱动 |

### 3.2 设计理念对比

| 平台 | 核心设计理念 | 优势 | 局限 |
|---|---|---|---|
| WinCC Unified | TIA 统一工程 + Faceplate + ISA-101 | 西门子生态无缝，工程一致性最高 | 非西门子设备集成需中间件 |
| FactoryTalk | 标签直连 PLC + 预制面框 | AB 生态内配置效率极高 | 跨品牌能力弱 |
| Ignition | 标签/设备解耦 + UDT + Python | 最开放，跨品牌最强，无限标签 | Python 脚本性能受限 |
| InTouch | 复合对象（Compound Object）封装设备 | 流程行业图形建模成熟 | Windows 平台锁定 |
| Plant SCADA | 情境感知 + 8 种报警原因/响应/后果 | 大型过程控制报警最完善 | Cicode 非标准，学习曲线陡 |
| MCGS | 全中文 + 设备驱动库（700+） | 中文用户上手最快，性价比高 | 国际化弱，大型项目扩展有限 |
| 组态王 | 变量数据库 + 通信驱动 + 云商店 | 国内中小项目市占率最高 | 高可用/跨平台不足 |

### 3.3 功能完整性对比

| 功能维度 | WinCC | FactoryTalk | Ignition | InTouch | Plant SCADA | MCGS | 组态王 | **本项目** |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| 图形 HMI | ★★★★★ | ★★★★ | ★★★★★ | ★★★★★ | ★★★★★ | ★★★ | ★★★★ | ★★★★ |
| 报警管理 | ★★★★ | ★★★★ | ★★★★ | ★★★★ | ★★★★★ | ★★★ | ★★★ | ★★★★ |
| 趋势曲线 | ★★★★ | ★★★ | ★★★★★ | ★★★★ | ★★★★ | ★★★★ | ★★★★ | ★★★★ |
| 历史数据 | ★★★★ | ★★★★ | ★★★★★ | ★★★★ | ★★★★ | ★★★ | ★★★ | ★★★★ |
| 报表系统 | ★★★★ | ★★★★ | ★★★★★ | ★★★★ | ★★★★ | ★★★ | ★★★ | ★★（CSV 导出） |
| 配方管理 | ★★★ | ★★★★ | ★★★ | ★★★★ | ★★★ | ★★★★ | ★★★★ | ★★★★ |
| 冗余高可用 | ★★★★★ | ★★★★ | ★★★★ | ★★★ | ★★★★★ | ★★★ | ★★★ | ★★★ |
| Web/Mobile | ★★★★★ | ★★★ | ★★★★★ | ★★★ | ★★★ | ★★★ | ★★★ | ★★（无 Web 运行时） |
| 脚本编程 | ★★★★ | ★★★ | ★★★★★ | ★★★★ | ★★★★ | ★★★ | ★★★ | ★★★★（Lua） |
| 安全权限 | ★★★★ | ★★★★ | ★★★★★ | ★★★★ | ★★★★ | ★★★ | ★★★ | ★★★★ |
| 通信协议 | ★★★★ | ★★★★ | ★★★★★ | ★★★★ | ★★★★ | ★★★★ | ★★★★ | ★★★★ |
| **代码生成** | 原生编译 | 原生编译 | 运行时引擎 | 运行时引擎 | 运行时引擎 | 编译下载 | 运行时引擎 | **LVGL C 生成（独有）** |

### 3.4 易用性对比

| 平台 | 学习曲线 | 中文支持 | 开发效率 | 上手难度 |
|---|---|---|---|---|
| WinCC Unified | 中等 | 有 | 高（TIA 统一） | 中等 |
| FactoryTalk | 中等 | 有限 | 高（AB 生态内） | 中等 |
| Ignition | 较低 | 有限 | 高（Web 原生） | 较低 |
| InTouch | 中等 | 有限 | 高（复合对象） | 中等 |
| Plant SCADA | 较高 | 有限 | 高（情境感知） | 较高 |
| MCGS | 低 | **优秀** | 中 | **低** |
| 组态王 | 低 | **优秀** | 中 | **低** |
| **本项目** | 低 | **优秀** | 高（AI 辅助+代码生成） | 低 |

### 3.5 扩展性与开放性对比

| 平台 | 开放协议 | 二次开发 | 云/IoT | 跨平台 | 控件扩展 |
|---|---|---|---|---|---|
| WinCC | OPC UA/DA, REST, GraphQL | JS, .NET | Industrial Edge | Windows 为主 | Faceplate 80+ |
| FactoryTalk | OPC UA, MQTT | C#, .NET | DataMosaix | Windows | Global Object 60+ |
| Ignition | OPC UA, MQTT, Sparkplug B, 300+ 驱动 | Python, SQL, JS | 原生 MQTT Engine | **Win/macOS/Linux** | Template 40+ |
| InTouch | OPC UA/DA, SuiteLink | QuickScript .NET, ActiveX | 有限 | Windows | 复合对象 |
| Plant SCADA | OPC UA, IPv6, BACnet | Cicode, SQL | Access Anywhere | Windows | SmartSymbol 50+ |
| MCGS | Modbus, OPC UA/DA, DDE | VB/C# 脚本 | 网络版 | Windows | 元件库 |
| 组态王 | Modbus, OPC UA/DA, MQTT | C/C++ 脚本 | 云版 8.0 | Windows | 图库 |
| **本项目** | **OPC UA + Modbus + MQTT（自包含零依赖）** | **C# 插件（DLL/JSON 热插拔）** | MQTT 原生 | **Win/macOS/Linux（编辑器）** | **19 控件+Faceplate+SVG** |

### 3.6 数据处理能力对比

| 平台 | 实时数据库 | 历史归档 | 最大标签数 | 报警处理 |
|---|---|---|---|---|
| WinCC | SQL Server | 高压缩长期归档 | 12 服务器/32 客户端 | ISA-18.2 |
| FactoryTalk | RSLinx | FT Historian | 无硬性限制 | 分级报警 |
| Ignition | 内置 Tag 引擎 | SQL 历史模块 | **无限** | 事件/报警分类 |
| InTouch | I/O Server | Wonderware Historian | 按授权 | 报警汇总 |
| Plant SCADA | I/O 独立服务器 | 内置归档 | 75~400,000 | **8 种原因/响应/后果** |
| MCGS | 内部数据库 | ODBC | 无硬性限制 | 分级报警 |
| 组态王 | 内部数据库 | SQL 直写 | 按授权 | 分级报警 |
| **本项目** | 变量缓存表 | 环形缓冲+CSV | 无硬性限制（固件 TAG 表） | 5 种条件+4 级+状态机 |

### 3.7 行业应用案例

| 平台 | 典型行业 | 代表性应用 |
|---|---|---|
| WinCC | 汽车制造、化工、电力 | 大众生产线、中石化炼化 |
| FactoryTalk | 离散制造、食品饮料 | 通用装配线、可口可乐灌装 |
| Ignition | 水处理、制药、物流 | 全球 10,000+ 部署 |
| InTouch | 石化、电力、制药 | 壳牌炼油厂、辉瑞制药 |
| Plant SCADA | 能源、交通、水务 | 荷兰 A2 隧道、欧洲输电网 |
| MCGS | 楼宇自控、教学、中小制造 | 高校实验室、水泵站 |
| 组态王 | 市政环保、制造业 | 污水处理厂、锅炉房 |
| **本项目** | 嵌入式设备、边缘控制器 | 目标：T113/RK3506/STM32 级 MCU HMI |

---

## 四、横向对比：本应用 vs 主流（组态控件维度）

### 4.1 核心差距矩阵

| 对比维度 | 主流标杆 | 本应用现状 | 差距等级 |
|---|---|---|---|
| **过程图形库** | Tank/Pipe/Valve/Motor 等符号库（WinCC/Wonderware 标配） | 无，仅 19 种基础控件 | **高** |
| **报表系统** | 专业报表引擎（Ignition 内置报表） | 仅 CSV 导出 | 中 |
| **Web/Mobile 运行时** | 各厂商 Web 化（HTML5） | 无 Web 运行时，仅嵌入式 | 中（定位差异） |
| **控件数量** | 40-80+ 模板/对象 | 19 种 + Faceplate 自定义 | 中 |
| **历史归档** | 专用 Historian（高压缩） | 环形缓冲 + CSV | 中 |
| **报警原因/响应** | Plant SCADA 8 种定义 | 5 种触发条件 | 低-中 |
| **UDT/结构化标签** | 主流标配 | ✅ 已闭环（点号寻址） | 已对齐 |
| **SVG/矢量** | 主流标配 | ✅ 已闭环（SVG 导入） | 已对齐 |
| **表达式引擎** | Ignition/WinCC 标配 | ✅ 已闭环（属性面板接入） | 已对齐 |
| **AI 辅助** | TIA V21 Copilot 刚起步 | ✅ 已实现（屏幕/模板/脚本生成） | **领先** |
| **代码生成** | 运行时引擎或专有编译 | ✅ **LVGL C 代码生成（独有赛道）** | **领先** |
| **自适应布局** | TwinCAT/WinCC Unified | ✅ 已闭环（Fit/Fill） | 已对齐 |
| **Git 工作流** | TIA V21 原生 Git | ✅ 已闭环（GitWorkflowService） | 已对齐 |

### 4.2 交互方式对比

| 交互模式 | WinCC | FactoryTalk | Ignition | MCGS/组态王 | 本项目 |
|---|---|---|---|---|---|
| 控件添加 | 拖放 | 拖放 | 拖放 | 拖放 | ✅ 拖放 |
| 属性编辑 | 类型化面板 | 类型化面板 | 类型化面板 | 类型化 | ✅ 类型化面板 |
| 多选 | 框选+Ctrl | 框选+Ctrl | 框选+Shift | 框选 | ✅ 框选+Ctrl |
| 缩放/平移 | Ctrl+滚轮/中键 | Ctrl+滚轮/中键 | 按钮+滚轮 | 按钮 | ✅ Ctrl+滚轮/中键 |
| 层管理 | 层面板 | 层面板 | 层面板 | Z序 | ✅ 层面板 |
| 设计时预览 | ✅ | ✅ | ✅ | ✅ | ✅ 预览模式 |
| 脚本调试 | ✅ | ✅ | ✅ | 有限 | ✅ 沙箱 F6 |

---

## 五、优势与不足总结

### 5.1 本应用组态控件优势

1. **独有赛道**：LVGL v9 C 代码生成，面向 MCU 级嵌入式（T113/RK3506/STM32），主流桌面工具不覆盖此赛道。
2. **代码生成闭环**：设计→生成→静态校验（质量门）→模拟器工程导出，全链路已验证（842 用例）。
3. **嵌入式资源感知**：ResourceEstimator + 字体子集化 + 图片优化 + 代码优化策略，设计时即反馈 Flash/RAM 占用。
4. **开放插件架构**：通信驱动（Modbus/OPC UA/MQTT 自包含零依赖）+ 硬件目标 DLL/JSON 热插拔。
5. **工程体验已对齐主流**：拖放/类型编辑器/层管理/设计时预览/交叉引用/审计/Git 工作流全部落地。
6. **AI 辅助领先**：自然语言→屏幕/模板/脚本生成，主流工具刚起步。
7. **安全模型完整**：0-15 级角色 + SHA-256 + 审计日志，对标 Pro-face GP-Pro EX。
8. **数据链路完整**：报警状态机 + 趋势环形缓冲 + 数据记录压缩 + UDT 全链路。

### 5.2 本应用组态控件不足

1. **过程行业图形控件缺失**（最高优先级）：Tank/Pipe/Valve/Motor/泵等符号库 + 动态填充（液位/流向动画）完全缺失，无法直接支撑化工/水处理等场景。
2. **控件数量偏少**：19 种 vs 主流 40-80+，Gauge/Knob 共用 `lv_arc`，深层差异化不足。
3. **报表能力弱**：仅 CSV 导出，无专业报表模板/打印。
4. **Web/Mobile 运行时缺失**：无 HTML5 运行时，仅嵌入式屏端。
5. **历史归档弱**：环形缓冲 + CSV 替代专用 Historian，长期存储能力不足。
6. **报警语义单薄**：无 Plant SCADA 级的原因/响应/后果分级与联动工作流。
7. **脚本生态**：Lua（MoonSharp 校验 —— 该表述**一度被判「有误」、同日又恢复**：2026-09-20 先查得「从未接入产品」，同日拍板「做强」后真解析已搬进 `HmiEditor.Core` 并由 `ScriptEngineService.Validate` 真实调用 ⇒ **现为产品能力**（Lua 5.2 方言）；两段复核均在本文「脚本系统」节）vs 主流 Python/JavaScript/C#，生态与技能储备有差距。
8. **无远程访问/Web 部署**：无 Access Anywhere 类能力。

### 5.3 主流 HMI 软件共性趋势（2026）

1. **全面 Web 化**：WinCC Unified / Optix / Ignition Perspective 均转向 HTML5 技术栈。
2. **AI 工程化**：TIA V21 Copilot、AI 生成画面/代码进入工业工程。
3. **Git 原生集成**：工程文件可 diff、可协作、可回滚。
4. **面向对象深化**：UDT/Global Object/SmartSymbol/Faceplate 成为复合组件标准。
5. **IoT/边缘融合**：MQTT/Sparkplug B 原生支持，云边协同。
6. **高性能 HMI 设计规范**：ISA-101 灰度/静默/金字塔布局成为行业标准。

---

## 六、决策依据与建议

### 6.1 自主研发 vs 选型判断

| 决策维度 | 结论 |
|---|---|
| 基础控件（19 种） | ✅ **自研已具备**，达嵌入式 HMI 主流水准 |
| 过程图形库 | ⚠️ **需自主研发或引入开源符号库**（本应用定位嵌入式，第三方商业控件不适用） |
| 报表/归档 | ⚠️ 视行业需求决定，嵌入式场景 CSV+SD 卡可满足大多数需求 |
| 通信协议 | ✅ 自包含零依赖方案（Modbus/OPC UA/MQTT）为嵌入式独有优势，应持续强化 |

### 6.2 建议优先级（对标主流补齐路线）

| 优先级 | 行动项 | 对标依据 |
|---|---|---|
| **P0** | 新增过程图形控件：Tank（液位填充）/Pipe（流向）/Valve（状态色）/Motor，含动态填充算法与 CodeGen | WinCC/Wonderware 符号库 |
| **P0** | 扩展仪表类控件差异化：Gauge 刻度/多指针/色区（避免与 Knob 共用 arc 的局限） | WinCC 仪表库 |
| **P1** | 报警语义增强：增加原因/响应/后果关联与联动工作流 | Plant SCADA 8 种定义 |
| **P1** | 报表增强：简单报表模板（日/月报）+ 打印 | Ignition 报表模块 |
| **P2** | Lua 生态增强或引入更强脚本（调研是否有嵌入式 Python 方案） | Ignition Jython |
| **P2** | 远程访问方案（调研 LVGL 屏端远程画面方案） | Plant SCADA Access Anywhere |

### 6.3 战略定位建议

> **坚持"嵌入式 LVGL C 代码生成"细分赛道**，不与主流桌面/Web HMI 正面竞争；
> 重点强化：过程图形库补齐 + 代码生成质量 + 资源感知闭环 + AI 生成差异化；
> 对标对象建议锁定 EasyBuilder Pro（WEINTEK，触摸屏组态标杆）而非桌面 SCADA。

---

## 七、参考文档索引

| 文档 | 内容 |
|---|---|
| [2026-07-30-hmi-mainstream-comparison-report.md](file:///home/chen/Git/zhishu-hmi/docs/2026-07-30-hmi-mainstream-comparison-report.md) | 主对标报告（能力矩阵+差距分级） |
| [2026-08-02-hmi-mainstream-gap-incremental.md](file:///home/chen/Git/zhishu-hmi/docs/2026-08-02-hmi-mainstream-gap-incremental.md) | 增量差距分析（P0-P2 落地状态） |
| [2026-07-30-hmi-optimization-dev-plan.md](file:///home/chen/Git/zhishu-hmi/docs/2026-07-30-hmi-optimization-dev-plan.md) | 优化开发计划 |
| 本报告 | 组态控件本体系统性调研 + 全面对比 + 决策依据 |
