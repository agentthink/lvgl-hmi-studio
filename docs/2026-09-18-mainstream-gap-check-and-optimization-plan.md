# zhishu-hmi 与主流软件差距核查与优化计划（2026-09-18）

> 审查日期：2026-09-18
> 审查基线：`main` @ `6d106da`，工作区干净（`git status` 无输出）
> 方法：全量构建 + 全量测试 + 固件 host 测试 + 逐条核对 07-30 主报告（G1~G20）与
> 08-02 增量报告（I1~I12）在**代码中的实际状态**（grep / 读码，不看文档状态标记）
> 前置文档：`docs/2026-07-30-hmi-mainstream-comparison-report.md`（主对标报告）、
> `docs/2026-08-02-hmi-mainstream-gap-incremental.md`（增量差距）、
> `docs/2026-08-08-hmi-control-research-comparison.md`（组态控件对照，对标对象含 WinCC Unified /
> FactoryTalk Optix / Ignition / Wonderware InTouch / AVEVA (Citect) / MCGS / **组态王 (KingView)**）、
> `docs/2026-09-14-optimization-directions.md`（上上轮优化方向）、
> `docs/2026-09-17-open-items-register.md`（未闭环项权威登记表）
> 说明：本文所有数字均为**本次现场实测**，标注了可复现命令；结论以代码为准。
> 对标口径：全景矩阵对照 WinCC Unified / FactoryTalk Optix / AVEVA / Ignition / TwinCAT（见 07-30 报告）；
> **组态王 (KingView) / InTouch** 的能力差异已在 08-08 控件调研逐项覆盖（过程图形库、报警语义、报表、
> 历史归档、脚本生态等）；本文 §5 的差距分类沿用该口径。按 08-08 调研 §6.3 的结论，
> 嵌入式定位下最贴近的对标对象是 **EasyBuilder Pro（威纶通，触摸屏组态标杆）**。

---

## 1. 实测基线（2026-09-18）

| 检查项 | 命令 | 结果 |
|---|---|---|
| 构建 | `dotnet build HmiEditor.slnx -c Release` | ✅ **0 错误 / 0 警告**（12.79s，13 项目） |
| 全量测试 | `dotnet test HmiEditor.slnx -c Release --no-build` | ✅ **2716/2716**（Core 1921、AI 315、App.UiTests 230、Device 87、Communication 75、Simulator 44、Hardware 44） |
| 固件 host 测试 | `bash firmware/t113/tests/run_host_tests.sh` | ✅ **8 步全部通过**（通讯/变量表、触摸、板级、crypto、会话与权限、凭据、会话超时与审计、运行时参数下沉） |

**对照 09-17 基线（2686 项）**：+30 项（Core 1915→1921、App.UiTests 227→230、Device 76→87、
Hardware 34→44），与本轮审查前登记表 §4 第 29 项（发布包 / 资源预算 / 用户文档）新增判据一致。

---

## 2. 07-30 主报告差距矩阵（G1~G20）逐条核对 —— **全部闭环**

> 核对方式：每条给出**代码证据**（文件/符号），不是"我记得做了"。
> 主报告原文见 `docs/2026-07-30-hmi-mainstream-comparison-report.md` §3.1。

| 原差距 | 本轮代码证据 | 结论 |
|---|---|---|
| G1 工具栏专业化 | `MainWindow.axaml` 使用 `CommandBar`（`fa:` 前缀 + `CommandBarElementContainer`） | ✅ 闭环 |
| G2 无拖放式控件添加 | `ToolboxView.axaml.cs` 调 `DragDrop.DoDragDropAsync`（另有 VariableBrowser/FaceplateLibrary/ResourceLibrary 三处拖放） | ✅ 闭环 |
| G3 属性面板缺类型化编辑器 | `PropertyPanelView.axaml` 含 `ColorPickerPopup`/`CheckBox`/`ComboBox`/`NumericUpDown`/`FANumberBox` | ✅ 闭环 |
| G4 状态栏无资源仪表盘 | `MainWindow.axaml` 含 `FlashBar`/`RamBar`；**且已升级**：预算取自 `IHardwareTarget.Resources` + `ResourceBudget` 判读（不再写死 8MB/64MB，见 09-18 资源预算实测） | ✅ 闭环（增强） |
| G5 Dock 布局未启用 | `HmiDockFactory.cs` + `dock:DockControl`（12 面板解析链），另有真实拖拽 headless 回归网 | ✅ 闭环 |
| G6 无层管理/Z序 | `LayerPanelView.axaml` + `LayerIndex` 绑定 | ✅ 闭环 |
| G7 无设计时数据预览 | `DesignCanvas` `PreviewValueProvider` / `PreviewModeChanged` | ✅ 闭环 |
| G8 变量绑定过于简单 | `VariableBindingData` + UDT 点号寻址 + 表达式绑定源（`ExpressionBindingEvaluator`） | ✅ 闭环（增强） |
| G9 无资源管理器 | `AssetManagerView.axaml` | ✅ 闭环 |
| G10 无导航流设计器 | `NavigationFlowView.axaml` | ✅ 闭环 |
| G11 无交叉引用浏览器 | `CrossReferenceView` + `CrossReferenceAnalyzer`（变量/屏幕/脚本三类反查） | ✅ 闭环 |
| G12 脚本仅校验无引擎 | `ScriptSandboxExecutor`（`hmi_read`/`hmi_write` 沙箱语句 + F6 调试运行） | ✅ 闭环 |
| G13 无表达式引擎 | `ExpressionEngine`（Core）+ `ExpressionBindingEvaluator` + 属性面板绑定源切换 | ✅ 闭环 |
| G14 无 SVG/矢量导入 | `SvgParser`（rect/circle/ellipse/line/polyline/polygon/path）+ `SvgToLvglConverter`（lv_canvas 绘制序列） | ✅ 闭环 |
| G15 无 AI 辅助生成 | `HmiEditor.AI`（Orchestrator + 5 生成器 + Agent 工具）+ **SSE 流式**（阶段 11 已闭环） | ✅ 闭环 |
| G16 无 Git 工作流 | `GitWorkflowService`（检测/初始化/暂存/提交/历史/状态）+ 工具菜单 4 项 + 状态栏 | ✅ 闭环 |
| G17 无审计追踪 | `AuditLogEntry` + `AuditLogStore`（JSONL append-only）+ `AuditLogView` + 设备端会话/超时审计 | ✅ 闭环 |
| G18 无响应式布局 | `ScreenAdaptiveMode`（None/Fit/Fill）+ CodeGen `adaptive_layout` 回调 | ✅ 闭环（控件级锚点未做，见 §4） |
| G19 无 UDT | `VariableUdt` + 变量管理 UDT 页签 + 全链路点号寻址（嵌套不支持，见 §4） | ✅ 闭环 |
| G20 图片无自动优化 | `ImageOptimizer`（色深/量化） | ✅ 闭环 |

---

## 3. 08-02 增量差距（I1~I12）核对 —— **全部闭环**

08-02 报告（`docs/2026-08-02-hmi-mainstream-gap-incremental.md`）把 P0/P1 差距列为
I1~I9 并当场闭环、I10~I12 列为路线图。本轮核对：**I10~I12 也已闭环**。

| 项 | 本轮代码证据 | 结论 |
|---|---|---|
| I10 AI 辅助生成 | `HmiEditor.AI/Generators/`（屏幕/模板/脚本/图标）+ `AiOrchestrator`；**SSE 流式已通 UI**（`SseReader` + `LlmChat.WithProgressAsync` + `ChatEntry.LivePreview`） | ✅ 闭环 |
| I11 设计-生成-验证闭环 | `GeneratedCodeValidator`（Error 级拦导出）+ `SimulatorProjectExporter` 一键导出 + **真实 LVGL CMake 编译**（verify.sh 7 步，09-17 复跑退出码 0） | ✅ 闭环 |
| I12 字体子集化 | `FontSubsetAnalyzer`（字符统计/预算对比/预览/charset.txt 导出）+ `FontSubsetView` | ✅ 闭环 |

**08-02 报告自留的"限制"（本轮仍成立，转登记）**：
- I7 OPC UA：无签名/加密、无订阅推送（轮询）、无浏览节点树 —— 本轮 grep `Browse|Subscribe` 于 `Hmi.Communication.Builtin/OpcUa/` **0 命中**，限制仍在；
- I6 SVG：**渐变与 transform 已于 2026-09-19 做掉**（A 段 `a72ce8d` 解析/几何 + 阶段 32 CodeGen 发射；`SvgParser.cs:16-18` 已是"支持"而不是"跳过"）；**图案/滤镜/嵌入位图/文本仍解析时跳过**（`SvgParser.cs:20`）。剩余的实质限制转移到"渲染栈写不出来"那一层（真渐变只有轴对齐矩形/圆），见 `docs/2026-09-19-svg-gradient-and-transform.md`；
- I4 UDT：嵌套暂不支持（`VariableUdt.cs` 无嵌套字段），限制仍在；
- I9 自适应：控件级锚点（左/中/右）留待后续，限制仍在；
- I1 表达式：08-02 明确写"后续可扩展**表达式驱动颜色/可见性**" —— 本轮 grep `Expression` 相关样式字段 **0 命中**，未做。

---

## 4. 09-14 优化方向（P0/P1/P2）执行情况核对

`docs/2026-09-14-optimization-directions.md` 的 P0 五项**当日已完成**（归档/删 Renderer/工程配置/
临时目录/README）。P1/P2 本轮逐项核对：

| 原项 | 本轮实测 | 结论 |
|---|---|---|
| P1-1 画布性能专项（Brush/Pen 缓存） | `RenderResourceCache`（266 行，Immutable 画刷/画笔/文本/弧几何四级缓存，`docs/2026-09-14-canvas-render-baseline.md` 记录基线）；`DesignCanvas*.cs` 内 `new SolidColorBrush` **0 处**、`new Pen(` 仅 2 处 | ✅ 闭环 |
| P1-2 按域拆 DesignCanvas | `public partial class DesignCanvas`，5 个文件（990+983+374+968+984=**4299 行**） | ✅ 闭环 |
| P1-3 消灭源码 grep 式测试 | 当时的口径是「剩余 `File.ReadAllText` 全部读**生成产物**（`main.c`/`sim_config.h`/`CMakeLists.txt`/主题 json/审计日志）」（`AppThemeServiceTests:137` 读运行时写的主题文件、`SecurityConfigConsumptionTests:326` 读审计日志）。⚠ **阶段 121 更正：这句「无一读产品源码」不成立** —— 实测 **40+ 个测试文件**会读 `src/` 的**源码**（结构守卫类：`AiSchemaEnumContractTests` 枚举全部 `src/**/*.cs`、`SingleFileSizeGuardTests` 读文件行数、`ColorDepthSourceOfTruthTests`、`ScreenFactoryTests` 等）。它们是**有意保留的形状判据**（改结构就该红），与「读生成产物做行为断言」是两类东西；但原文那句把「面」说成了 0，属失实 | ⚠ **更正后仍算闭环，但口径改了**：源码读入只允许出现在**带 `--selftest`/元判据的结构守卫**里；行为断言一律读生成产物 |
| P1-4 给 SimulatorProjectExporter 补测试 | `tests/HmiEditor.Core.Tests/SimulatorProjectExporterTests.cs` 存在 | ✅ 闭环 |
| P2-1 抽 IR（步骤③：宏统一） | `CResourceDeclarations` 为 `IMG_PATH_`/`FONT_PATH_` 唯一实现，CodeGen 与 Exporter 均调用（30+ 处） | ✅ 步骤③闭环（完整 `IControlEmitter` 未做，见 §5-A4） |
| P2-2 权限/凭据闭环 | 已闭环（阶段 4/6）：`generated_security.h` + 设备端 SHA-256 校验 + 动作回调权限门 | ✅ 闭环 |
| P2-3 设计-代码单一事实源 | `design-previews/` 仅 2 `.design` + 1 `.css`，**编辑器仍不消费**（grep `design-previews` 于 `src/` 0 命中） | ✅ **已闭环（2026-09-18，P0-4）** —— 见 `docs/2026-09-18-design-code-single-source.md`：先量出「设计稿与实现主题本就有意分歧」⇒ 不改值对齐，改为 `DesignTokens` 目录 + 8 条守卫（CSS↔目录闭合 / 设计层无裸 hex / `.design` 结构受检），负向验证 3/3 |
| P2-4 Dock 拖拽空白收尾 | 标签拖拽可行性已证 + 3 条回归网（`DockDragRegressionUiTests`）；**其余路线**（文档区/浮动窗口/多屏）未覆盖，原始症状成因仍未定论 | ⚠️ 部分闭环 |
| P2-5 AI 流式响应 | SSE 端到端已闭环（阶段 11）；**并行 tool_call 未做**（登记表 §3 #12） | ⚠️ 主体闭环 |

---

## 5. 当前真实差距（2026-09-18 视角）

**一句话**：07-30 报告的全部 20 项差距与 08-02 报告的 12 项差距**已全部闭环**；剩下的不再是
"缺功能"，而是三类：**深度（对标主流软件的能力上限）**、**工程化（治理后回涨与未治理的单类）**、
**需先立功能/产品决策/硬件（登记表 §2/§3 既有项）**。

### 5.1 深度差距 —— 对标 2026 主流软件的能力上限

| # | 差距 | 主流对标（2026） | 本轮证据 | 分类 |
|---|---|---|---|---|
| A1 | ~~**表达式不驱动视觉**~~：**已于 2026-09-19 闭环**（阶段 31），`TextColor`/`BgColor`/`IsVisible` 可绑表达式，设计时 + CodeGen 两端同一张注册表 | WinCC / Ignition 绑定表达式可驱动任意属性 | ✅ 见 `docs/2026-09-19-expression-driven-visual.md`；剩余 4 条局限（未覆盖的视觉属性 / 24 位 RGB / 属性下拉只列 4 项 / 无实机）在该文 §7 | 已闭环 |
| A2 | **OPC UA 深度**：无浏览节点树（变量导入要手填 NodeId）、无订阅推送（轮询）、无签名/加密 | TIA / TwinCAT / Optix 标配 Browse/Subscribe/Security | `OpcUa/` grep `Browse|Subscribe` 0 命中 | 新功能 |
| A3 | **SVG 能力面**：~~渐变~~/图案/滤镜/~~transform~~ —— **渐变与 transform 已支持（2026-09-19）**，图案/滤镜/嵌入位图/文本仍解析即跳过 | WinCC Unified / AVEVA 全矢量 | `SvgParser.cs:16-18`（支持）/ `:20`（不支持）；发射侧受渲染栈限制，见 `docs/2026-09-19-svg-gradient-and-transform.md` | 剩余部分：新功能 |
| A4 | **生成链仍无统一发射器**：`SimulatorProjectExporter` **1540 行**（09-14 时 1312，仍在涨）、`CodeGenerator` 150 行 + **15 个 partial**；同一控件在设计器/模拟器/固件/导出仍有平行实现（画布 `DesignCanvas.*` 与生成器 `CodeGenerator.*` 各一套） | 无直接对标（架构项） | `wc -l` 实测；`RefreshBindingsParityTests` 只锁了刷新路径 | 架构 |
| A5 | **控件库规模**：26 种（含 Screen），主流 40~80+；过程图形仍缺 **Motor/泵/搅拌器**等符号（08-08 调研的 P0 项） | WinCC 80+ Faceplate / Wonderware 符号库 | `HmiControlType` 枚举 26 项；grep `Motor` 于控件枚举 0 命中 | 新功能 |
| A6 | **报表/归档**：仅 CSV 导出 + 环形缓冲，无报表模板/打印/长期 Historian | Ignition 报表模块 / Plant SCADA 归档 | 08-08 调研 §5.2 仍成立 | 产品方向 |
| A7 | **报警语义单薄**：无 Plant SCADA 级的原因/响应/后果分级与联动工作流 | Wonderware / Citect | 08-08 调研 §5.2 仍成立 | 产品方向 |
| A8 | **多语言仅 2 种**（zh-CN/en-US），无任意语言数 + 导出；en-US 为机器直译，**未做母语级逐字校对** | 主流任意语言数 + 语言导出 | `Strings.*.json` 2250/2250 键集一致、占位符一致，但译文质量未经校对 | 工程+产品 |
| A9 | **无远程访问/Web 部署**（Access Anywhere 类） | Plant SCADA Access Anywhere / WinCC Unified Web | 08-08 调研 §5.2 仍成立 | 产品方向（**嵌入式定位可明确不做**） |

### 5.2 工程化差距 —— 治理后回涨与未治理的单类（本轮新发现）

| # | 现象 | 本轮实测 | 性质 |
|---|---|---|---|
| B1 | **MainWindow 治理后回涨**：09-13 七步治理后文档记录 **2928 行**，本轮实测 **3013 行**（+85）—— 阶段 13~30 的本地化/设置接线又加回去了；且**没有行数守卫**，再涨无人知 | `wc -l MainWindow.axaml.cs` = 3013（09-13 基线 `913bfc7` 同一工具实测 2943，两种口径都指向"回涨 ~70~85 行"） | 新发现 |
| B2 | **PropertyPanelView 仍是第二大单类**：2804 行，09-14 已点名"热点文件"但从未排期拆分（对比：MainWindow/DesignCanvas/CodeGenerator 均已治理） | `wc -l` 实测 | 新发现 |
| B3 | **SimulatorProjectExporter 结构未治理**：1540 行单文件（09-14 时 1312），有测试但无 partial 拆分；是 Core 层唯一未被治理的大文件 | `wc -l` 实测 | 新发现 |
| B4 | **无覆盖率工具**：全仓 grep `coverlet|CollectCoverage|CoverageThreshold` 0 命中；2716 项测试但"哪些路径没测"不可见 | 本轮 grep | 新发现 |

> **后续复核（2026-09-20）**：B1~B4 **已全部闭环** —— B1 `SingleFileSizeGuardTests`（相等判定）、
> B2 `PropertyPanelView` 拆 11 个 partial（09-18）、B3 `SimulatorProjectExporter` 拆 5 个 partial（09-18）、
> B4 覆盖率工具链（登记表 §4 #33、`docs/project-status.md` 阶段 36、专题 `docs/2026-09-20-coverage-tooling.md`；
> 基线 **20349 / 46162 = 44.1%**）。上表保留为 09-18 当轮的实测快照。

### 5.3 登记表既有仍开着项（2026-09-18 复核，全部转引权威登记表）

- **需产品决策 8 项**（登记表 §2）：`TemplateGenerationSchema.category` 硬 enum、批量图标半成品去留、
  `ScanIntervalMs` 语义重复、`EnableHmiReboot` 面板标注、旧 `%APPDATA%/HmiEditor` 清理、
  `TempRoot` 清理策略、工程双目录合并、多实例共享目录 —— 均需产品拍板，非技术债。
- **需先立功能 6 项**（登记表 §3）：5 个 `RuntimeConfig` 未下发字段（心跳帧/任务池/崩溃转储）、
  `EnableTamperDetection` 零消费、两个死字段补执行路径、**并行 tool_call**、**AI Schema 高级属性**
  （Tank 液位 / Pipe 流向不在生成范围 —— 本轮确认模型有 `PipeFlowDirection/Speed` 但 AI schema 无）、
  极端迟到串帧。
- **软件侧可验证残留**（登记表 §4）：#20b 设备端按字符数判（产品决策+迁移）、#26 其余拖拽路线、
  #27 en-US 校对 + AI 层扫描面、#27b 术语统一（产品决策）、#27d 插件程序集本地化、
  #28 LVGL 升级实机回归（需实机）、#29 残留（RAM 预算实测 / RK3506 预算 / ResourceEstimator 校准 /
  Windows 发布包与签名）。
- **环境/本地残留 4 项**（登记表 §5）：Debug 全量 `App.UiTests` 挂死、`.dbg/` 207MB、用户家目录
  Mock 工程、NuGet 审计只读缓存 —— 均非仓库缺陷。

---

## 6. 优化计划（按投入产出排序）

### P0 —— 软件侧可验证、低风险，每项半天~1 天

| # | 事项 | 涉及 | 验证与完成标准 |
|---|---|---|---|
| P0-1 | **MainWindow 行数守卫**：把"单文件行数"变成门禁判据（防治理回涨无人知；阈值取现值 3013，只许降不许涨，如同本地化守卫的"相等判定"） | `scripts/ci-gate.sh` + 测试守卫 | 门禁加一行判据；再涨 1 行即红；负向验证（故意加行 → 红） ✅ **已完成（2026-09-18）** |
| P0-2 | **PropertyPanelView 按域拆 partial**（复制 DesignCanvas 的套路：只加 partial 不改行为） | `PropertyPanelView.axaml.cs` → `PropertyPanelView.*.cs` | 测试零改动通过；文件行数下降；类本身大小不变说明写进提交 ✅ **已完成（2026-09-18）** |
| P0-3 | **SimulatorProjectExporter 按域拆 partial**（已测试网在手：`SimulatorProjectExporterTests` 21 条） | `SimulatorProjectExporter.cs` → partial | 生成输出对 Golden 工程逐字节不变；测试零改动通过 ✅ **已完成（2026-09-18）** |
| P0-4 | **设计-代码单一事实源收尾**（09-14 P2-3）：`colors_and_type.css` tokens 映射进 `PaletteDefaults`；`.design` 升级为结构化模型 | `design-previews/`、`src/HmiEditor.Core/Design/PaletteDefaults.cs` | 设计预览色板改动单向流入编辑器；无裸 hex 残留由测试锁定 ✅ **已完成（2026-09-18）** |
| P0-5 | **en-US 校对 + 插件扫描面扩展**（登记表 #27/#27d 后半）：① zh/en 两本字典机器预检（术语表、长度比、占位符 —— 占位符已一致）；② `HardcodedLocalizationGuard` 扫描面扩到插件程序集；③ 老 `hw.issue.*` 规则文案逐个补键 | `Strings.*.json`、守卫测试、`Hmi.Hardware.Builtin` | ✅ **已完成（2026-09-18）**，见 `docs/2026-09-18-plugin-text-and-lang-usage-guards.md`：键推导收成单一来源 `App/Services/PluginTextKeys.cs`；补 8 键 ×2 语言；三道守卫（插件键覆盖**零容忍** / 插件文本基线 49 条 / Lang 常量使用面 1267 锁定）；**先量事实即找到 4 处活的回落缺陷**（`param.baud` 被错写成 `param.baud.rate`、驱动描述无键路径等）；**长度比机检经实测判定不可判**（`>3 且 >60` 命中 45 条全是中英文紧凑度差异；`>4 且 >80` 为 0）⇒ 不发布；负向验证 **4/4** |

**为什么先做这组**：全部零行为风险（P0-1/P0-2/P0-3 是纯结构 + 守卫），P0-4 是唯一仍开着的
架构级软件侧项，P0-5 是"永远做不完"的本地化的最后一次可机检收口。

### P1 —— 需要新功能或独立一轮，每项一周内

| # | 事项 | 涉及 | 验证与完成标准 |
|---|---|---|---|
| P1-1 | **表达式驱动颜色/可见性**（对标 WinCC/Ignition 动态视觉）：属性可绑 `表达式`，设计时求值 + CodeGen 生成 `lv_style`/运行期标签表 | `ExpressionEngine`、`PropertyPanelView` 绑定源、`CodeGenerator`、`BindingSemantics` | ✅ **已完成（2026-09-19，阶段 31）**，见 `docs/2026-09-19-expression-driven-visual.md` |
| P1-2 | **OPC UA Browse 节点树**（变量导入主场景）：Browse 服务 → 节点树 → 导入变量；订阅推送列为第二阶段 | `Hmi.Communication.Builtin/OpcUa/`、`VariableImportView` | 连真实/模拟 OPC UA 服务器可浏览并导入；新增编解码 + 端到端测试 |
| P1-3 | **SVG 渐变/transform**：`SvgParser` 支持线性/径向渐变与 transform（平移/缩放/旋转），CodeGen 同步 | `SvgParser`、`SvgAffine`、`SvgGradientGeometry`、`SvgToLvglConverter`；**`DesignCanvas.Renderers` 属 C 段、未做** | **A+B 段已完成（2026-09-19）**，见 `docs/2026-09-19-svg-gradient-and-transform.md`（A 段 `a72ce8d` + 阶段 32）。⚠ **完成标准按实测修正**：原文写"渲染 + CodeGen 同步"，但 LVGL 软件渲染器只在**轴对齐矩形/圆**上支持 LINEAR/RADIAL（三角扇只有 `HOR`/`VER` 且色带按三角形自身包围盒生成；`lv_draw_line_dsc_t` 无 `grad` 字段）⇒ 真渐变只在矩形/圆上发得出，多边形/Path/描边/含旋转一律降级成 50% 采样纯色。**C 段（画布渲染）未开工 ⇒ "画布所见"与"设备所得"当前仍不一致，本项未闭环** |
| P1-4 | **AI Schema 高级属性**（登记表 §3 #13）：Tank 液位 / Pipe 流向等逐控件族扩展 schema + 映射 | `AiSchemaEnums`、`AgentToolFactory`、`CodeGenerator.ControlParameters` | AI 生成带高级属性的控件时属性生效；`ControlPipelineCompletenessTests` 保持绿 |
| P1-5 | **并行 tool_call**（登记表 §3 #12）：一次输出多个 tool_call + 引擎调度 + 结果回填 | `AgentEngine`、`AgentOutput`、两 Provider | 一次请求可并行执行多个工具；失败/取消语义与流式一致 |
| P1-6 | **Dock 其余拖拽路线**（登记表 §4 #26）：文档区/浮动窗口/多屏重组，headless 驱动指针事件 | `DockDragRegressionUiTests` 扩展 | 新路线各 1+ 条回归；原始空白症状若复现则当场定性 |
| P1-7 | **过程图形控件补齐**（08-08 调研 P0）：Motor / 泵 / 搅拌器符号 + 动态填充/旋转动画 | `HmiControlType`、工具箱、渲染、CodeGen | 新控件在设计器/模拟器/固件三端可用；Golden 工程扩展覆盖 |

### P2 —— 需产品决策 / 硬件 / 长期，转入登记表

- **产品决策**（登记表 §2 的 8 项 + §4 的 #20b/#27b）：逐项给出决策建议后再动手。
- **需先立功能**（登记表 §3 的 5 个未下发字段 / 防篡改 / 死字段）：统一心跳帧 → 任务池 → 崩溃转储
  三步骨架。
- **需硬件/实机**：RAM 预算实测、RK3506 预算、LVGL 升级实机回归、ResourceEstimator 系数校准、
  Windows 发布包与签名 —— 全部登记并注明前置条件。
- **产品方向（建议明确不做或远期）**：A6 报表模板/打印、A7 报警语义分级、A9 远程访问/Web 部署
  —— 嵌入式 LVGL C 代码生成定位下投入产出比低，建议在 README/定位文档中**明确写出"不做"及理由**，
  避免每轮审查都重新评估（08-08 调研 §6.3 已建议锁定 EasyBuilder Pro 为对标对象）。

---

## 7. 置信度与未覆盖项

- **已亲自复现**：构建 0/0、2716 项测试、固件 host 8 步、G1~G20 / I1~I12 全部代码证据、
  DesignCanvas partial 与 RenderResourceCache 现状、剩余 `File.ReadAllText` 均为读生成产物、
  本地化字典 2250/2250 键集与占位符一致、MainWindow 3013 行（09-13 基线对比）、
  PropertyPanelView 2804 行、SimulatorProjectExporter 1540 行、`design-previews` 零消费、
  OPC UA 无 Browse/Subscribe、SvgParser 跳过渐变/transform、UDT 无嵌套、表达式无视觉驱动、
  控件枚举 26 项无 Motor。

  > **2026-09-19 追注**：上面这串"已亲自复现"是 **09-18 那一刻的快照**，其中两条此后已被代码推翻 ——
  > 「SvgParser 跳过渐变/transform」与「表达式无视觉驱动」都已做掉（A 段 `a72ce8d`、阶段 31、阶段 32）。
  > 快照保留原样是为了不篡改当时的复核记录；**引用本节事实前先核对 `docs/project-status.md` 的对应阶段**。
- **未覆盖**：端到端 `verify.sh`（需真实 LVGL + 网络不受沙箱影响，本机 `/tmp` 10MB 需指定 TMPDIR；
  09-17 已复跑退出码 0，本轮未重跑）、T113 交叉编译与实机验证、AI Provider 联网行为、
  en-US 译文质量的人工校对（机检只能查结构不能查"译得对不对"）。
- **建议下一轮**：按 §6 P0 五件套执行（每件零风险、可合并提交），随后 P1-1（表达式驱动视觉）
  是投入产出比最高的新功能 —— 它是 08-02 就明确"后续可扩展"、且主流软件 2026 标配的能力。
