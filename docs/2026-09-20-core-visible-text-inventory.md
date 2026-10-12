# Core 侧用户可见文案盘点（阶段 44）

> 日期：2026-09-20 · 上游：阶段 42 `docs/2026-09-20-core-script-message-localization.md`（同一条链路的上一环）
> 登记出处：阶段 42 §7 ③ / 阶段 43 §8 ② —— **「`HmiEditor.Core` 里其他用户可见文案未盘点」**

## 0 一句话

App 层的中文硬编码守卫早就有（阶段 19），扫描面只有 `src/HmiEditor.App`；
**`HmiEditor.Core` 里同样有文案直接显示在编辑器界面上，却完全在守卫之外**。
本轮把 Core 的 1070 条中文字面量**先分类、再计数**：405 条落在四条有理由的排除类别里，
剩下 665 条按「是否已核实会到界面」分两桶登记，其中**已核实的真缺口 101 条 / 15 条通道**（阶段 44 原为 61 / 6；+1 见 §8；+5 见 §9；§10 **只换消费方、两条数都不动**；**+6 见 §11.2** —— 阶段 91 的更新节当年就**没留下**，是本文件长期少算的一条通道；阶段 93 的 **−1** 见 §11；**+13 见 §12** —— 阶段 94 把误归 DATA 的 13 条归位（通道 ⑪），并把同一文件另外 35 条下拉标签拆去字典；**±0 见 §13** —— 阶段 95 只换通道 ② 的**消费者**（7 条措辞改走 `ReferenceTextKeys`），**条数与通道数都不动**；**+2 见 §15**（阶段 97 新增通道 `Services/UserAdminIssue.cs`）；**+2 见 §16**（阶段 98 新增通道 `Services/RecipeIssue.cs`）；**+5 见 §17**（阶段 99 新增通道 `Services/LoginIssue.cs`）；**+6 见 §18**（阶段 100 新增通道 `Services/RecipeImportIssue.cs`）；**+1 见 §19**（阶段 124 在**既有**通道 `Services/AuthenticationService.cs` 里新增一条审计详情 —— **通道数不动**）；**±0**（阶段 105 只动
`Pending` 桶 —— `Serialization/HmiPackageService.cs` 7 → **9**，**这一条链（`VisibleChannels`）
的条数与通道数都不动**；把"改了两条文案"当成"这条链要跟着 +2"正是本阶段真踩过的一脚，
见登记 **#62** / 专题 §105）—— ⚠ §15 / §16 那两节当年**只改了守卫常数**、没回来改本节与 §3 / §5，是阶段 99 一并归位的，**并把这两个数字钉在判据上**了（同一次归位还纠出守卫自己少算的那 1 条通道），见 §17）。
结论固化成守卫 —— 新写死的界面文案立刻红。

## 1 为什么不能照搬 App 的规则

App 守卫是「分类式」的：剥注释 → 取含 CJK 的字符串字面量 → 扣掉有理由的排除类别 → 其余全算待本地化。
这套规则在 App 里成立（那里剩下的确实都是界面文案），**在 Core 里会得到 1000+ 条假阳性**，
因为 Core 是库，同样含中文字面量，但绝大多数根本不是界面文案：

- 生成器往 `.c` / `.h` / 导出工程 `README.md` / CMake 脚本里写的注释（331 条）；
- 资源库预设条目、主题名、系统变量名、动作展示名（领域数据，会落盘）；
- 审计落盘记录、服务日志。

所以 Core 这边判定的出发点不是「它是中文」，而是「**这条字面量从哪个出口走**」。

## 2 盘点：1070 条怎么分

| 类别 | 条数 | 判定依据 | 为什么用户看不到 / 不该翻 |
|---|---|---|---|
| **生成产物文本** | 331 | 文件在 `CodeGen/`、`SimulatorProjectExporter*`、`*CodeGen.cs`，或接收者是 `sb` 的写入调用（`sb.AppendLine` / `Cmt(` / `printf(` / `fprintf(` / `message(` / `Raw(`） | 写进产物。**必须固定中文**：产物可复现性要求同一工程两次导出逐字节一致，跟着 UI 语言变会把 `tools/ReproHarness` 的 A/B 归一化直接搞坏 |
| **日志 / 调试出口** | 57 | `Log?.Invoke` / `Debug.WriteLine` / `Console.` / `Trace.` | Core 侧 `Log` 在编辑器里**唯一的订阅点**是 `PreviewRuntimeService`（`RuntimeBindingEngine` / `ActionExecutor`），出口 `DiagnosticLog` 在 `MainWindow` **不注入** ⇒ 只进 Debug；其余服务在 App 里**没有订阅点** |
| **开发者消息** | 17 | `throw new` / `Exception(` | 面向开发者；翻译后无法搜索 |
| **正则模式** | 0（今天） | `Regex` | 那是模式不是文案，翻译它会改变匹配行为 |
| **合计排除** | **405** | | |
| **已核实到界面** | **61** | 见 §3 | **真缺口** |
| **待定性** | **604** | 见 §4 | 逐文件登记，只减不增 |

> 盘点脚本：`.workbuddy/tmp/core-text-audit.py`（只读）。**判别纯属结构判定，不靠文件名猜语义** ——
> 下面 §4 那两个反例就是"靠名字猜"会猜错的直接证据。

## 3 十五条已核实通道（100 条真缺口）

每一行都注明了**消费方文件 + 那句"把它显示出来"的代码**，而且这两样都是判据（见 §5）。

| # | Core 产出文件 | 条数 | 消费方 | 显示标记 |
|---|---|---|---|---|
| ① | `Services/GeneratedCodeValidator.cs` | 23 | `App/Services/ExternalText.cs` | `Describe(GeneratedCodeFinding)` ⇒ `Resolve(GenIssueKey(finding.Code), finding.Message, …)`（**阶段 89** 改：消息与定位**都**码化了 —— 18 条消息 ＋ 5 条定位的中文是 `gen.issue.*` / `gen.loc.*` 的**回落**） |
| ② | `Services/CrossReferenceAnalyzer.cs` | 9 | `App/Services/ReferenceTextKeys.cs` | `Lang.CrossRefDescScreenSwitch`（**阶段 93** 从 13 降到 9：其中 4 条是**定位串**、搬去通道 ⑩ —— 面板措辞一个字没动；**阶段 95** 把剩下 7 条的**消费者**从 `App/Views/CrossReferenceView.axaml` 的 `{Binding Description}` 换成出口层 `ReferenceTextKeys.Describe`：条数仍是 **9**，但这 9 条**一条都不再直显**） |
| ③ | `Services/AuthenticationService.cs` | 12 | `App/Views/AuditLogView.axaml.cs` | 订阅 `AuditLogged`，列表显示 `AuditLogEntry.Detail`（且 `ApplyFilter` 会拿它做筛选） |
| ④ | `Services/ScriptSandboxExecutor.cs` | 7 | `App/Views/ScriptEditorView.axaml.cs` | `result.Error` 与 `result.Log` 都拼进 `TxtRunOutput` |
| ⑤ | `Services/SvgParser.cs` | 3 | `App/Wiring.DockAggregate.cs` | `LocService.Format("asset.svg.parse.fail", …)`（阶段 54「只留总线」把宿主直订换成了总线，消费方随之改名） |
| ⑥ | `Services/GitWorkflowService.cs` | 3 | `App/Services/GitDialogService.cs` | `Loc.Format("status.git.commit.failed", error ?? "")` |
| ⑦ | `Services/SvgParseError.cs` | 1 | `App/Services/ExternalText.cs` | `Describe(SvgParseError)` ⇒ `Resolve(SvgIssueKey(error.Code), error.Message, …)`（阶段 87 新增：中文串是 `svg.issue.*` 的**回落**） |
| ⑧ | `Services/CsvVariableImporter.cs` | 5 | `App/Services/ExternalText.cs` | `Describe(CsvImportIssue)` ⇒ `Resolve(CsvIssueKey(issue.Code), issue.Message, …)`（阶段 88 新增：5 条是 `csv.issue.*` 的**回落**） |
| ⑨ | `Services/ExpressionSyntaxError.cs` | 6 | `App/Services/ExternalText.cs` | `Describe(ExpressionSyntaxError)` ⇒ `Resolve(ExprSyntaxKey(error.Code), error.Message, …)`（**阶段 91** 新增：6 条是 `expr.syntax.*` 的**回落**；⚠ 这一条**当年没进本表**，2026-09-23 阶段 93 补记 —— 见 §11.2） |
| ⑩ | `Services/DanglingRefLocation.cs` | 3 | `App/Services/ExternalText.cs` | `DescribeLocation(GeneratedCodeFinding)` ⇒ `Resolve(GenLocationKey(finding.LocationCode), finding.Location, …)`（**阶段 93** 新增：3 条是 `gen.loc.*` 的**回落**，由通道 ② 搬来；登记 #61l） |
| ⑪ | `Models/HmiAction.cs` | 13 | `App/Services/ActionTextKeys.cs` | `Lang.ActionDescSwitchScreen`（**阶段 94** 新增：13 条是 `action.desc.*` 的**回落**，正常路径走 `ActionTextKeys.Describe`。⚠ 同一批字面量**同时**是产物注释的原文 —— 产物那一侧**不计入本表**，本表记的是"到界面"；由 `Pending` 的 **DATA** 栏**归位**而来，登记 #61m） |
| ⑫ | `Services/UserAdminIssue.cs` | 2 | `App/Services/ExternalText.cs` | `UserIssueKey(userAdmin.Issue.Code)`（**阶段 97** 新增：2 条是 `user.issue.*` 的**回落**，**异常档** —— 走 `FailureReason` 的 ②′ 支；⚠ 这一条当时**没进本表**，2026-09-24 阶段 98 补记 —— 见 §16） |
| ⑬ | `Services/RecipeIssue.cs` | 2 | `App/Services/ExternalText.cs` | `RecipeIssueKey(issue.Code)`（**阶段 98** 新增：2 条是 `recipe.issue.*` 的**回落**，**返回值档** —— 走 `Describe(RecipeCloneIssue)`；与上一条**出口同文件、但不是同一条路**，消费标记不能互相顶替） |
| ⑭ | `Services/LoginIssue.cs` | 5 | `App/Services/ExternalText.cs` | `LoginIssueKey(issue.Code)`（**阶段 99** 新增：5 条是 `login.issue.*` 的**回落**，**返回值档** —— 走 `Describe(LoginIssue)`；⚠ 这一条当时**没进本表**（§16 只把它自己那两次归位到 13 / 89），2026-09-24 阶段 100 一起补记） |
| ⑮ | `Services/RecipeImportIssue.cs` | 6 | `App/Services/ExternalText.cs` | `RecipeImportIssueKey(issue.Code)`（**阶段 100** 新增：6 条是 `recipe.import.issue.*` 的**回落**，**返回值档** —— 走 `Describe(RecipeImportIssue)`；⚠ 与 ⑬ 的**键族不同**（`recipe.import.issue.` vs `recipe.issue.`）—— 同登记项 #61f 的两刀，消费标记不能互相顶替） |

**⑤⑥ 是同一类缺陷，和 App 守卫里那条「非首实参不豁免」一模一样**：
消息本身进了**已本地化模板的实参位**，模板翻了、参数没翻
⇒ 英文界面下这两处必然是**中英混排**（「Commit failed: 项目目录不是 Git 仓库」）。

## 4 待定性 604 条：两个反例比结论更值钱

盘点过程中有两条我**先入为主判成"用户可见"、读消费方之后被推翻**：

| 文件 | 条数 | 第一直觉 | 实测 |
|---|---|---|---|
| `Design/DefaultPropertyValidator.cs` | 10 | 属性面板的红字提示 ⇒ 显然可见 | ❌ `PropertyPanelView.Changes.cs:61`：消息**只进 `Debug.WriteLine`** 然后 `return`（保留原值），界面上**什么都不显示** |
| `Services/CsvVariableImporter.cs` | 16 | 导入失败的具体原因 ⇒ 显然可见 | ❌ 两个消费方（`VariableImportView.axaml.cs:96` / `CommunicationConfigView.axaml.cs`）**都只显示 `Errors.Count`**（一个数字），消息文本没人读 |

> 这正是本项目那条纪律的又一次生效：**结论要能指到一次 grep，不能靠名字推断**。
> 如果我按名字把它们算进"待本地化"，就会去翻一批**根本没人看到**的字符串。

其余待定性的归类（逐条理由写在守卫的行内注释里）：

| 归类 | 文件数 | 条数 | 说明 |
|---|---|---|---|
| 领域数据（会落盘 / 进产物） | 11 | 484 | 资源库预设 357、动作名 48、系统变量 34、主题名 15 … 翻它会让工程文件随 UI 语言变 |
| 只进 Debug / 无订阅点 | 4 | 17 | 上面那两个反例 + `VariableManager.IntegrityIssue.SuggestedFix`（`MainWindow.axaml.cs:1251` 只 `Debug.WriteLine`）+ 本地化服务的诊断串 |
| 数据在、但无产品消费方 | 3 | 36 | `CsvVariableImporter` 16、`ActionExecutor` 18（`PreviewRuntimeService` 只取 `bool`）、`RecipeManager.ValidateRecipe` 2（只在测试里被调） |
| 消费方是**生成器** | 1 | 4 | `ScreenLayerRefs` 的校验错误由 `CodeGenerator.Screen.cs:410` 写成 `// WARNING: 图层引用校验失败 …` 进生成代码 |
| 已由阶段 42 字典通道覆盖 | 2 | 11 | `LuaSyntaxValidator` / `ScriptEngineService` 的 `Message` 是回落文案，界面走 `script.issue.<code>` |
| 内部对照表 | 2 | 52 | `LuaDialect` 39（方言特性/符号对照）、`DesignTokens` 13（与设计稿「有意分歧」的说明） |

## 5 判据与负控

守卫：`tests/HmiEditor.Core.Tests/CoreVisibleTextInventoryGuardTests.cs`（9 条）。

| 判据 | 盯什么 |
|---|---|
| `Scanner_StillWorks_WhenExclusionsAreDisabled` | 仪器自检：关掉排除类别仍要扫到 ≥40 文件 / ≥900 条 —— 否则"扫不到"会被误读成"干净" |
| `NoUnclassifiedCoreChinese` | 台账与实扫**逐条相等**（既不能增，也不能"忘了改小"），差异按 缺失/新增/数量变 三类点名 |
| `VisibleChannels_EvidenceStillExists` | **证据防腐**：每个通道的消费方文件必须存在，且里面那句显示标记必须还在 |
| `VisibleChannels_AreCoveredByTheInventory` | 通道文件必须在扫描面上，且通道条数 ≤ 该文件实扫计数 |
| `VisibleChannels_CountIsWhatTheDocSays` | 通道条数之和 == **§3 表内条数之和**（阶段 91 起 **73**、阶段 93 起 **72**、阶段 94 起 **85**、**阶段 95 起仍 85**（只换消费方）；**阶段 97 起 87、阶段 98 起 89、阶段 99 起 94、阶段 100 起 100**；§6 是阶段 44 的快照，不参与镜像）⚠ 阶段 91 那次守卫常数动了、本表没动 ⇒ 「镜像」这句话腐烂了两个阶段，阶段 93 一起收（§11.2）；⚠ **阶段 97 又犯了一次**（常数 87、本节与 §0 / §3 仍写 85）⇒ 阶段 98 一起归位（§16） |
| `IsExcluded_*`（4 条） | 判定逻辑的方向：四类排除命中、真界面写法**不**被排除、不跨语句连坐、不在对象初始化器里继承兄弟属性的豁免 |

**负控（一次注入 ⇒ 两条独立判据各自点名）**：

```diff
- src/HmiEditor.Core/Services/SvgParser.cs        + if (svgText.Length > 999999) return (null, "SVG 太大，无法解析");
- src/HmiEditor.App/Views/ValidationResultView.axaml.cs   Text = finding.Message  →  Text = string.Empty
```

读数：**2 红 7 绿** ——

- `NoUnclassifiedCoreChinese`：`~ Services/SvgParser.cs: 登记 3，实扫 4`
- `VisibleChannels_EvidenceStillExists`：`通道 Services/GeneratedCodeValidator.cs 声称经由 …ValidationResultView.axaml.cs 显示，但那里已经没有标记：Text = finding.Message`
- 其余 7 条**保持绿** ⇒ 两类判据互相独立（不是同一条被数了两遍）。还原后 9 绿。

> ⚠ **阶段 89 起上面第二条注入已不可复现**：通道 ① 的消费标记从
> `ValidationResultView.axaml.cs` 的 `Text = finding.Message` 换到了
> `App/Services/ExternalText.cs` 的 `GenIssueKey(finding.Code)`（见 §10）
> ⇒ 再改视图那两行，这条判据**不会**响。替代注入与实测读数见 §10.4。

**规则收紧（一次真实的自我修正）**：`GeneratedWrite` 最初写成裸 `AppendLine`。首跑那条自检用例红，
暴露出它会把 **CSV 导出的列名**（`DataLogger` / `RecipeManager` / `CsvVariableImporter` 都在用 `AppendLine`）
一并放过 —— 而**排除是漏报方向**，宁可窄。改成要求接收者是 `sb` 之后基线**一条没变**
（说明今天没有别的文件靠这条宽规则才被排除），自检用例也改成钉住这个方向。

## 6 读数

| 项 | 值 |
|---|---|
| 提交 | 守卫 `e71fc1b`（1 file，**+396**，`19:14:07`） |
| 新增判据 | `CoreVisibleTextInventoryGuardTests` **9 条** |
| 扫描面 | `src/HmiEditor.Core` 48 个文件、1070 条含中文字面量 |
| 排除 | 405（生成产物 331 / 日志 57 / 开发者消息 17 / 正则 0） |
| 已核实通道 | 61（6 条通道） |
| 待定性 | 604（逐文件登记，只减不增） |
| 构建（门禁第 3 步，Release + 警告即失败） | **0 警告 / 0 错误** |
| 守卫（定向） | **9 通过 / 0 失败**；负控 **2 红 7 绿**，还原后 9 绿 |
| `Core.Tests` | 主段 2107 → **2116**（**+9** ＝本轮新增判据数），独立段仍 8 |
| `App.UiTests` | **262**（未动 App 测试） |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；八程序集合计 2946 → **2955**，**除 Core.Tests 外七项逐条一致** ⇒ 纯净增 |
| 门禁日志 | `.workbuddy/tmp/ci-gate-2026-09-20-coretext.log`，mtime **`19:15:04`** ⇒ 晚于提交 **57 秒**（时序合法） |

## 7 仍未闭环

| # | 项 | 状态 |
|---|---|---|
| ① | **61 条真缺口未本地化** | 本轮只盘点 + 立守卫。逐条改成「Code + Args + 字典」是后续批次；②③④⑥ 那几条的改法与阶段 42 同形 |
| ② | 604 条待定性里，`DATA` 那一类「要不要跟 UI 语言」是**产品决策** | 与 App 守卫里那 4 条落盘标识同类（登记表 §4 第 27c 项） |
| ③ | 两个守卫**各有一份扫描基础设施** | Core 守卫已改用 `SourceScanner.StripComments`（单遍状态机，有"长度/行数不变"的不变量测试）。App 守卫仍是自己那份从简的实现 —— **合并有行为风险**（更稳的剥注释可能让它多扫出字面量 ⇒ 当前为 0 的基线变红），需单独一轮：先量差异，再决定是否统一 |
| ④ | 通道证据只覆盖**六条已知通道** | 新增一条「Core 产出 ⇒ 界面显示」的链路，只要它的文件已登记，`NoUnclassifiedCoreChinese` 未必能察觉"这条是给界面看的"。**取证责任在人**，守卫保的是"登记过的东西没漂" |
| ⑤ | `ScriptSandboxExecutor` 的 `[Sandbox]` 前缀是硬编码 | 与 App 那 13 处 `[提示]` 标签同类（阶段 20 已处理过 App 侧），Core 侧尚未 |
| ⑥ | `ActionExecutor` 的 `Fail(...)` 消息链路是断的 | `PreviewRuntimeService` 只取 `bool`，消息经 `Log → DiagnosticLog`（未注入）⇒ 用户看不到任何动作失败原因。**这是功能缺口，不只是文案问题** |

## 8 阶段 87 更新：通道 6 → 7（61 → 62 条）

> 2026-09-23，阶段 87（闭环登记 §4 **#61a** 第一刀）。本文件其余部分仍是**阶段 44 的快照**
> （§6 的读数、§7 的未闭环项都不改 —— 它们记录的是那一刻的实况）；只有 §0 / §3 / §5 是活表。

**新增通道的来历**：`SvgParser.Parse` 的返回类型从 `(SvgDocument?, string?)` 换成
`(SvgDocument?, SvgParseError?)`（阶段 87，登记 #61a）—— 失败从**裸中文串**变成
「**码 + 中文回落原文 + 实参**」三段式记录。`SvgParseError.NoShapes.Message = "无可用形状"`
就是那 1 条新的中文字面量。

**它为什么算"到界面"**：`ExternalText.Describe(SvgParseError)` 用
`Resolve(SvgIssueKey(error.Code), error.Message, error.Args)` 渲染 —— 字典有 `svg.issue.*`
就用字典（中英文各自成套），**缺键才回落**这份中文。所以它与 ⑤ 是同一类：
中文串当**参数**进已本地化模板，英文界面下会中英混排。

| 项 | 阶段 44 | 阶段 87 |
|---|---|---|
| 通道数 | 6 | **7** |
| 通道条数之和 | 61 | **62** |
| `Services/SvgParser.cs` | 3 | 3（**没变**：三个串还在，只是搬进了 `SvgParseError` 的构造参数） |
| `Services/SvgParseError.cs` | — | **1**（新文件） |

⇒ 守卫 `VisibleChannels_CountIsWhatTheDocSays` 的常数同步成 **62**；
`NoUnclassifiedCoreChinese` 的差异报告是**它先点出来的**
（原文：`+ Services/SvgParseError.cs: 登记 0，实扫 1`）—— 这条守卫按设计工作了一次。

顺带修掉 §3 ⑤ 行的**过期消费方**：`App/Wiring.Canvas.cs` → `App/Wiring.DockAggregate.cs`
（阶段 54「只留总线」删掉了宿主对资产面板的直订）。守卫那边一直是新的，是文档停在了旧值。

## 9 阶段 88 更新：通道 7 → 8（62 → 67 条）

> 2026-09-23，阶段 88（闭环登记 §4 **#61f** 第一刀）。同上：只动 §0 / §3 / §5 这三张活表。

**新增通道的来历**：`CsvVariableImporter.Parse` 的返回值 `ImportResult.Errors`
从 `List<string>`（**裸中文串**）换成 `List<CsvImportIssue>`（码 / 行号 / 中文回落 / 实参），
5 条回落原文（`TOO_FEW_LINES` / `NO_HEADER_COLUMNS` / `ROW_MISSING_NAME` /
`ROW_EMPTY_NAME` / `ROW_PARSE_FAILED`）就是那 5 条新的中文字面量。

**它为什么算"到界面"（而且此前不算）**：改动前那 5 条**在两条路上都到不了界面** ——
`VariableImportView` 只取 `Errors.Count`、`CommunicationConfigView` **连 `Errors` 都不读**
⇒ 那时它们在 `Pending` 里归类 **LATENT**（"数据在、但当前没有产品消费方"）。
本阶段给两条路都接上了 `ExternalText.Describe(CsvImportIssue)`（状态行带条数、`ToolTip` 逐条），
所以它们是**真的到界面**了 —— 通道 ⑧ 记的就是这件事。

| 项 | 阶段 44 | 阶段 87 | 阶段 88 |
|---|---|---|---|
| 通道数 | 6 | 7 | **8** |
| 通道条数之和 | 61 | 62 | **67** |
| `Services/CsvVariableImporter.cs` | 16（Pending / LATENT） | 16 | **5（通道）+ 11（Pending：`BuildColumnMap` 的同义词表）** |

⚠ 顺带纠一条**与代码不符的理由**：`Pending` 那条原来写「两个消费方都只显示 `Errors.Count`」——
对 `CommunicationConfigView` **不成立**（它整块丢弃）。这与 #54 是同一个病：
豁免表里的「理由」没人去代码里核，它就成了能把缺陷合法化的字段。本阶段把理由改对了。

⇒ 守卫 `VisibleChannels_CountIsWhatTheDocSays` 的常数同步成 **67**。

## 10 阶段 89 更新：**通道数与条数都不动，换的是通道 ① 的消费方**

> 2026-09-23，阶段 89（闭环登记 §4 **#61g** 第一刀）。同上：只动 §0 / §3 / §5 这三张活表。

**这一节与 §8 / §9 形状不同**：那两节都**新增了通道、抬高了条数之和**；这一节**两个数都没动** ——
`Services/GeneratedCodeValidator.cs` 还是 **23** 条，只是它们**到用户眼里的路径**换了。

**来历**：`GeneratedCodeValidator.Validate` 的每条 finding 都在 Core 里把 `Message` / `Location`
**直接拼成中文**，而唯一的文本出口（`ValidationResultView` 的 `Text = finding.Message` /
`Text = finding.Location`）**原样直显** ⇒ 界面切成英文时**整个对话框都是中文**。
阶段 89 给 finding 加了诊断码（`GeneratedCodeIssueCodes` 18 码 ＋ `FindingLocationCodes` 4 码），
两条通道都改走 `ExternalText.Describe(GeneratedCodeFinding)` / `DescribeLocation(...)`。

| 项 | 阶段 44 / 87 / 88 | 阶段 89 |
|---|---|---|
| 通道数 | 6 / 7 / 8 | **8（不动）** |
| 通道条数之和 | 61 / 62 / 67 | **67（不动）** |
| 通道 ① 的消费方 | `App/Views/ValidationResultView.axaml.cs` | **`App/Services/ExternalText.cs`** |
| 通道 ① 的标记 | `Text = finding.Message` | **`GenIssueKey(finding.Code)`** |
| `Services/GeneratedCodeValidator.cs` | 23 | **23** |

⚠ **「本地化了一个文件」不等于「台账条数要变小」**：那 23 条中文里，18 条是消息回落、
5 条是定位回落 —— 它们**按设计必须留在 Core**（字典缺键时才显示），
变的是**路径**而不是**数量**。把 23 改小才是造假。

⇒ 守卫 `VisibleChannels_CountIsWhatTheDocSays` 的常数 **67 不动**；`VisibleChannels_EvidenceStillExists`
的输入（消费方文件 ＋ 标记）换了，**判据本身没改一行**。

### 10.1 通道 ① 的消费方为什么是 `ExternalText` 而不是视图

因为**视图不再是"那句显示代码"的所在**：真正决定"显示成什么"的是出口
（码 ⇒ 键 ⇒ 模板 ⇒ 填实参）。这与 ⑦（`SvgParseError`）/ ⑧（`CsvVariableImporter`）**同一形状** ——
三者的消费标记都落在 `ExternalText.cs` 上，差别只在**标记串**：

| 通道 | 标记串 |
|---|---|
| ⑦ `SvgParseError.cs` | `SvgIssueKey(error.Code)` |
| ⑧ `CsvVariableImporter.cs` | `CsvIssueKey(issue.Code)` |
| ① `GeneratedCodeValidator.cs`（阶段 89 起） | `GenIssueKey(finding.Code)` |

### 10.2 定位的**三档**（本阶段唯一的新分治）

> ⚠ **阶段 93 起只剩两档** —— 「外来」那一档随 #61l 闭环**整档删除**（表 / 双向判定 / [Fact] / NC4 **五件一起删**），`FindingLocationCodes` **4 → 5**。收束表见 **§11.3**。
> 下面这张表是**阶段 89 当时的实况**，数字**保留不删**（它是那一刻的证词）。

| 档 | 数量 | 处置 |
|---|---|---|
| 有码 | 4 | `gen.loc.*`（`SCREEN` / `SCREEN_CONTROL` / `SCREEN_CONTROL_PROPERTY` / `GENERATED_CODE`） |
| 中性·直出 | 4 | `-` / 屏幕 id 列表 / `SecurityConfig.RuntimeUsers[..]` / `SecurityConfig.RuntimeUsers` —— **模型路径**，翻了反而对不上代码 |
| 外来 | 1 | `DANGLING_REFERENCE` 的串由 `CrossReferenceAnalyzer` 拼 ⇒ 登记 **#61l**，本刀不吞 |

后两档各由一张**双向**的表守着（往中性档塞中文定位 ⇒ 红；外来那张表一旦过期 ⇒ 也红），
判据在 `GenFindingLocalizationGuardTests`（11 条）。

### 10.3 可达性新增 **M13**

面 **21** = `GeneratedCodeIssueCodes` 18 码 ＋ `FindingLocationCodes` 4 码 − **1**（`GENERATOR_WARNING_COMMENT`
**刻意无键**：消息体是产物 `generated_ui.c` 里 `// WARNING:` 注释的原文，翻译它等于改写产物，登记 #61k）
⇒ 恰等于本阶段两本字典新增的 21 个键（**2199 → 2220**）。⚠ **阶段 93 起面是 22**（`FindingLocationCodes` 4 → 5 ⇒ 字典键 **2227**），见 §11.4。

### 10.4 替代负控（实测）

| 注入 | 期望红 | 实测 |
|---|---|---|
| 摘掉可达性 M13 取数段（保留下界行） | 主判据 ＋ 元判据 | **红 ×2**：21 个键逐条点名 ＋「M13-生成代码验证码：实测 0 < 下界 21」 |
| `ValidationResultView` 两条通道退回 `finding.Message` / `finding.Location` | 期望由 `GenFindingLocalizationGuardTests.ConsumerKeepsTheCodedShape` 接住 | **红 ×1**（4 条点名）；⚠ 点名它的**不是**本台账的守卫 —— 台账 9 条**全绿**，因为通道 ① 的标记已不在视图上（正是 §5 那条 ⚠ 讲的：这条注入今天动不了台账判据） |
| `ExternalText` 的 `GenIssueKey(finding.Code)` 改成等价的局部变量写法（标记串消失） | `VisibleChannels_EvidenceStillExists` | **红 ×1**，逐字点名「…但那里已经没有标记：`GenIssueKey(finding.Code)`」 |

还原一律**写回原文**（不用 `copy2`），三处 `sha256` 前 / 后一致。
## 11 阶段 93 更新：通道 ② **13 → 9** ＋ 新增通道 **⑩**（67 → 72 条）

> 2026-09-23，阶段 93（闭环登记 §4 **#61l**）。同上：只动 §0 / §3 / §5 这三张活表。
> ⚠ **本节的 −1 也是"数字为什么会动"的第三个理由**（前两个：阶段 87/88 是**新造文案**、
> 阶段 91 是**原本被豁免挡在台账外的文案第一次被看见**）—— 这一次是**搬迁**。

### 11.1 通道 ② 的 4 条去哪了

`Services/CrossReferenceAnalyzer.cs` 的 13 条里，有 **4 条不是面板文案** ——
它们是 `FindDanglingReferences` 给 `GeneratedCodeFinding.Location` 用的**定位串**
（形如 `[屏名] 控件 '名' (属性)` / `[屏名] 控件 '名' 动作`），改动前**原样直出**、
`LocationCode` 留空 ⇒ 英文界面下这一半中英混排。

阶段 93 把它们改成 Core 记录体 `Services/DanglingRefLocation.cs`
（「定位码 ＋ 中文回落原文 ＋ 实参」三段式）⇒ **3 条去重后的文案**搬进新文件
（第 4 条是同一文案的第二个站点：动作字段有联动模型与回退旧字段两条路）。

| 通道 | 阶段 92 | 阶段 93 | 说明 |
|---|---|---|---|
| ② `Services/CrossReferenceAnalyzer.cs` | 13 | **9** | 面板措辞（`控件 名 绑定变量 (方向)` 这一类）**一个字没动** |
| ⑩ `Services/DanglingRefLocation.cs`（**新**） | —— | **3** | 消费方 `App/Services/ExternalText.cs`，标记 `DescribeLocation(GeneratedCodeFinding` |
| **通道条数之和** | 73 | **72** | 净 **−1** = 搬走 4、落新 3 |

### 11.2 ⚠ 顺带纠正：§3 从阶段 91 起就少了一条通道

`VisibleChannels_CountIsWhatTheDocSays` 的常数在阶段 91 就变成了 **73**
（= 阶段 88 的 67 ＋ `Services/ExpressionSyntaxError.cs` 的 **6** 条回落原文，登记 #61i），
而本文件的 §0 / §3 / §5 **都还停在 67 / 8 条**，**没有留下阶段 91 的更新节** ⇒
守卫那句「镜像的是专题 §3 那张活表」从那时起就是**不成立的**。

⇒ 本节一并把通道 **⑨** 补进 §3（6 条，阶段 91 的 `ExpressionSyntaxError.cs`），
并把三张活表的数字归位。**教训**：守卫的常数动了、活表没动，就是「镜像」这句话腐烂 ——
与阶段 92 归位 `LangConstantUsageGuardTests` 里停在阶段 89 的 **M6 269** 是同一种病
（那是**注释**里的数字腐烂，这是**活表**里的数字腐烂）。

### 11.3 定位的档位表（§10.2 的收束）

阶段 89 分了三档，阶段 93 把第三档**收掉了**：

| 档 | 阶段 89 | 阶段 93 | 处置 |
|---|---|---|---|
| 有码 | 4 | **5** | `gen.loc.*`（`SCREEN` / `SCREEN_CONTROL` / `SCREEN_CONTROL_PROPERTY` / **`SCREEN_CONTROL_ACTION`** / `GENERATED_CODE`） |
| 中性·直出 | 4 | **4** | `-` / 屏幕 id 列表 / `SecurityConfig.RuntimeUsers[..]` / `SecurityConfig.RuntimeUsers` —— **模型路径**，翻了反而对不上代码 |
| 外来 | 1 | **0** | `DANGLING_REFERENCE` 已码化 ⇒ 这一档**消失**。守它的那张 `ForeignLocation` 表、它的双向判定、`[Fact]` 与负控 NC4 **五件一起删**（空表 ＋ 恒真判定 = 没有负控的判据，按纪律不发布；留着会变成「把定位没码化合法化」的永久字段，登记 #54 的老病） |

⇒ `GenFindingLocalizationGuardTests` 从 **11 条**变成 **11 条**（删 1 条旧 `[Fact]` ＋ 加 1 条新的，
外加 `DanglingLocationViolations` 这条纯函数与接线面新增的 `LocationProducer` 档）。

### 11.4 可达机制 M13 的面

**21 → 22** = `GeneratedCodeIssueCodes` 18 码 ＋ `FindingLocationCodes` **5** 码 − 1
（`GENERATOR_WARNING_COMMENT` 刻意无键，登记 #61k）⇒ 恰等于两本字典的键
（**2226 → 2227**）。`UnusedBaseline` **1113 不动**（本族键靠 `GenIssueKey` / `GenLocationKey`
**推导**、没有 `Lang` 常量）。下界已在 `LocalizationKeyReachabilityGuardTests` 同步抬满（21 → 22）。

## 12 阶段 94 更新：新增通道 **⑪**（10 → 11 条通道 / 72 → 85 条）

> 2026-09-23，阶段 94（闭环 §4 **#61m**）。同上：只动 §0 / §3 / §5 这三张活表。
> ⚠ **本节的 +13 是「归类归位」，不是新增文案** —— 那 13 条**早就**在 `Models/HmiAction.cs` 里，
> 只是被错归进 `Pending` 的 **DATA** 栏（理由"翻它会让工程文件随 UI 语言变"，而**实测无一条落盘**：
> 落盘的是 `ActionType` / `Target` / `Value` 这些 Id）⇒ 与 **#54** 同病（豁免表的理由没人去代码里核）。
> 这是**第四个**"数字为什么会动"的理由（前三个：阶段 87/88 的**新造**、阶段 91 的**第一次被看见**、
> 阶段 93 的**搬迁**）—— 本次是**把误分类的字面量放到正确的那一栏**。

### 12.1 那 48 条一分为二：13 条进通道 ⑪，35 条**拆走了**

| 去处 | 条数 | 说明 |
|---|---|---|
| 进 `VisibleChannels` 通道 ⑪（本表新增） | **13** | `HmiAction.GetDescription()` 的 13 条 —— 它们是**交叉引用面板**的回落原文（正常路径走 `App/Services/ActionTextKeys.Describe`），且**同时**是产物注释的原文（那一侧**不**计入本表：本表记的是"到界面"） |
| 随 `ActionMeta.DisplayName` **拆去字典** | **35** | 6 张下拉表的标签；`Core/Models/HmiAction.cs` 里现在**实扫 13 条**（改动前 48）⇒ 面板那一侧改走 `action.type.*` |

⇒ `Pending` 里 `["Models/HmiAction.cs"] = 48` 那一行**整行删除**（它去了通道 ⑪），
`Pending` 里 `["Design/ActionTypeMeta.cs"] = 12` 的**数字不动、理由改成 GEN**
（原理由"动作元数据（图标/分组/展示名）"描述的是**两处共用**的真相；阶段 94 拆走后只剩
`CodeGenerator.Actions.cs` / `SecurityConfigCodeGen.cs` 两个**产物写入点**，与同栏的
`Services/ScreenLayerRefs.cs`（4 条）同类）。

### 12.2 通道 ② 的数字**没动**（9 条），但含义动了 —— 下一刀才收

`Services/CrossReferenceAnalyzer.cs` 仍是 **9 条**：动作那 2 条的中文**留在 Core 作回落原文**
（与"回落必须留在 Core"的全局口径一致）。⚠ 但它在面板上的**消费方换人了** ——
`{Binding Description}` 现在指向 `VisibleChannels` 通道 ② 所记的 `App/Views/CrossReferenceView.axaml`
里的**行对象** `App/Views/ReferenceRow.cs`：动作行走字典、其余 7 条仍走回落。
⇒ §5 的 `VisibleChannels_EvidenceStillExists` 那条标记**仍然成立**（模板五个绑定逐字未动），
但「9 条全部直显」已不再准确（2 条不再直显）⇒ **其余 7 条另开登记 #61n**。

### 12.3 三张活表的新读数

| 表 | 阶段 93 | 阶段 94 |
|---|---|---|
| §0 / §3 通道数 | 10 | **11** |
| §0 / §3 条数 | 72 | **85**（＋13） |
| §5 `VisibleChannels_CountIsWhatTheDocSays` 常数 | 72 | **85** |
| 守卫源码常数 `Assert.Equal(...)` | 72 | **85**（＋第四次"数字为什么动"的来历注释） |

## 13 阶段 95 更新：**通道数与条数都不动**，换的只是通道 ② 的消费方

> 2026-09-23，阶段 95（闭环 §4 **#61n**）。同上：只动 §0 / §3 / §5 这三张活表。

⚠ **本节与前四次的差别**：阶段 87/88 是**新造**、阶段 91 是**第一次被看见**、阶段 93 是**搬迁**、
阶段 94 是**归类归位** —— 本阶段是**第五种**「数字为什么会动」的理由：
**数字根本没动**，动的是一行里的**消费方**。

| 项 | 阶段 94 | 阶段 95 |
|---|---|---|
| 通道 ②（`Services/CrossReferenceAnalyzer.cs`） | 9 条 → 消费方 `App/Views/CrossReferenceView.axaml` / 标记 `{Binding Description}` | **9 条** → 消费方 **`App/Services/ReferenceTextKeys.cs`** / 标记 **`Lang.CrossRefDescScreenSwitch`** |
| 通道数（§0 / §3） | 11 | **11**（不动） |
| 条数之和（§0 / §3 / §5 常数） | 85 | **85**（不动） |

### 13.1 为什么 9 条**一条没少**却都不再直显

`CrossReferenceAnalyzer` 的 9 处 `Description = ...` 中文**全部留在 Core** —— 它们是
① 字典缺键时的**回落原文**、② 「不经界面」的消费者看到的值（Core 测试、未来的导出工具）。
界面侧新增的是它们的**孪生**：`App/Services/ReferenceTextKeys.cs` 按
`ReferenceDescriptionKind` 推键、7 支 `switch` **无默认支**（新增具名成员不接文案 ⇒ **CS8509**）。
⇒ 与通道 ①/⑦/⑧/⑨/⑩ 同形：**「中文在 Core 作回落、界面走字典」**。
两份的一致性由 `ReferenceDescriptionLocalizationGuardTests` 的 **J2 相等式**钉住（zh 渲染逐字等于 Core 回落）。

### 13.2 分派依据不是 `ReferenceType`（本阶段唯一结构性决定）

登记 #61n 假设「收法是按 `ReferenceType` 分派」—— **做不到**：同一个 `VariableBinding` 下有两种措辞
（联动模型「绑定变量 (方向)」/ 回退旧字段「绑定到变量」），同一个 `ActionTarget` 下也有两种
（「动作 {0}」/「{0} → 切换到 {1}」）。⇒ Core 新增 `ReferenceDescriptionKind`（7 值，`Unset = 0` 承「忘填」）。
⚠ 若按类型分派，那两对措辞会被粘到同一份模板上（表现为「永远有一半行显示错的那句」），
而**编译 / 字典守卫 / 本台账**三条**全都不会响**。

### 13.3 顺带纠正登记的一处假设

登记 #61n 点名的「表达式引用」在代码里**没有产出点**：`grep -rn ExpressionVariable src/`
只命中枚举声明自身（`Services/CrossReferenceAnalyzer.cs:19`）⇒ 另开登记 **#61p**，
本阶段**不删**该成员（删它要先量「有没有历史工程文件读它」）。

## §14 第六种「数字为什么不动」：出口层改动**碰不到** Core 台账（阶段 96）

§13 记的是「数字没动」的第五种理由（量的时候就没变）。§14 是**另一种**：这次**真的改了三个
渲染点、还新建了一个出口**，而 Core 侧**一条中文都没动** ⇒ 台账通道数、条数、`UnusedBaseline`、
M6 面、直通面**全部原样**：

| 面 | 阶段 95 | 阶段 96 | 为什么不动 |
|---|---|---|---|
| Core 中文台账（通道 / 条数） | 不变 | **不变** | 本刀改的是 **App 侧渲染点**，Core 那 9 条 `Description` 回落一字未动 |
| 直通面 | 15 · 14 | **15 · 14** | 本刀不碰 `ex.Message` 直通模板那一路 |
| M6 面（源码字面量） | 373 | **373** | 没有新增/删除源码字面量 |
| `UnusedBaseline` | 1113 | **1113** | 新常量**全都有代码引用**；没有复用别人的零引用常量 |
| M1 面 | 695 | **702** | **只有这一处动** —— ＋7 ＝ 新增常量数 |

⇒ 这是「**面读数是证词、不是目标**」的**第九例**：本刀的收益（用户可见的三列不再中英混排、
两份手工表收敛成一处、一处已分叉被修）在那几条既有面上**一条都看不见**，只能靠新判据
（K1–K8）与门禁日志的扫描面读数（＋1 个 `.cs` / ＋8 条判据）来取证。

⚠ 把「M1 面 ＋7」当成「面变大了」是**读错了** —— 那一面数的是「有代码引用的 `Lang` 常量」，
新常量本来就该被引用；真正要看的是 `UnusedBaseline` **没动**。

## §15 真新增（不是搬迁、不是归类）：通道 ＋1、条数 85 → 87（阶段 97）

前两节（§13 / §14）记的都是「**数字为什么不动**」。§15 是相反的第三种：**本刀真的往 Core 里
加了新的中文**，台账必须涨。

| 面 | 阶段 96 | 阶段 97 | 为什么这么动 |
|---|---|---|---|
| Core 中文台账（通道 / 条数） | 11 / 85 | **12 / 87** | 新增通道 `Services/UserAdminIssue.cs` ＝ **2 条**（两个码的中文回落） |
| 直通面 | 15 · 14 | **12 · 11** | −3 站改走出口（`sim.reg.read/write` ＋ `user.mgr.create`） |
| M6 面（源码字面量） | 373 | **373** | 不动 |
| `UnusedBaseline` | 1113 | **1113** | 不动（3 个码没有 `Lang` 常量，键由出口推导） |
| M1 面 | 702 | **703** | ＋1 ＝ 新增的 `Lang.UserMgrPwdResetFailed` |

**新通道**（与「搬迁 / 归类」两类都不同 —— 那两类的通道会此消彼长，这一条**是净增**）：

| 文件 | 条数 | 消费方 | 消费方式 |
|---|---|---|---|
| `Services/UserAdminIssue.cs` | **2** | `src/HmiEditor.App/Services/ExternalText.cs` | `UserIssueKey(userAdmin.Issue.Code)` |

⚠ **它为什么必须登记**：这两个回落是**给用户看的**（`UserAdminException.Message` 会作为
`FailureReason` 的回落落到界面），不是 `DevMessage`。判据
`NoUnclassifiedCoreChinese` 是在**注入态门禁**那一跑里以
`+ Services/UserAdminIssue.cs: 登记 0，实扫 2` **点名**抓到的 —— 换句话说：
**不登记就红**，不是靠人记着补。

⚠ 与 §14 对照着看更清楚：阶段 96 改的是 **App 侧渲染点**（Core 一条中文没动 ⇒ 台账原样），
阶段 97 改的是 **Core 侧产出方**（真加了中文 ⇒ 台账必须涨）。**两张表放在一起，
「面读数是证词」这句话才有边界**：读数动或不动，都要能指到一条**具体**的原因。

## §16 真新增（第二次）：通道 **12 → 13**、条数 **87 → 89**（阶段 97 / 98 一起归位）

> ⚠ 本节先还一笔**旧账**：§15（阶段 97）把守卫常数改成了 **87**，但本文件 §0 / §3 / §5 三处
> **都还停在 85 / 11** ⇒ 本文件自己那句「镜像 §3 那张活表」烂了一个阶段
> （与 §11.2 记的"腐烂了两个阶段"是同一类病）。本节把 **阶段 97 与阶段 98** 两次改动
> 一起归位：**13 条通道 / 89 条**。

## §17 该带的数字没跟上：守卫自己少算 1 条通道（阶段 99 一并归位 ＋ **第一次上判据**）

**发现方式**：阶段 99 要归位本文件 §0 的两个数字时，顺手把「守卫行首句」「守卫归位链末段」
「本文件 §0」三处的数与 `VisibleChannels` 表逐条对了一遍 —— 守卫两处都写 **13 条通道**，
而表里**实实在在是 14 条**（`new(...)` 条目 **14** 个、`Sum(c => c.Count)` = **94**）。

**按提交逐个数的结果**（`git show <c>:<file>` ＋ 数条目；脚本 `.workbuddy/tmp/s99-docdrift.sh`）：

| 提交 | 阶段 | 表里真实通道数 | 守卫行首句 | 守卫归位链末段 | 本文件 §0 |
|---|---|---|---|---|---|
| `301c11c` | 94（#61m） | **11** | 11 | —（无此句） | 11 |
| `be8dbde` | 97（#61d） | **12** | 11（**没跟上**） | — | 11（没跟上） |
| `549fb29` | 98（#61t） | **13** | **12**（那一行把"加了通道"写成「**通道数不变**」） | — | 11（仍没跟上） |
| `04e6eef` | 99（#61v） | **14** | 13（顺着上一行的 12 ＋1） | 13 / 94 | **13**（把阶段 98 的真值补上，没算自己那一跳） |

⚠ **两处互相矛盾也没人红**：阶段 98 那一跳之后，本文件一直比守卫**多算 1**（本文件 13 / 守卫 12）、
而真值比两者**都多 1**。⇒ 根因不是"谁算错了"，是**这两个数字两处手抄、且没有任何判据看着**。
⚠ 连带：内容批的**提交消息**也抄了那个漂过的数（写「12/89 → 13/94」），真值 **13 → 14**
（提交消息不可改，如实记在专题 §99.8）。

**为什么它值一节**：这两处都写着「文档里的数字要能指到一条日志」，而**数字自己漂了 3 个提交**
没人提醒 ⇒ 阶段 99 把它们**钉在算出来的值上**：判据 `VisibleChannels_CountIsWhatTheDocSays`
从「只断言 94」扩成「**三处** × **两个**数字都必须等于算出来的值」（本文件 §0 摘要句也在其中）
—— 这是本表**第一次**由判据担保（此前只有"条数"那一半有 `Assert.Equal(94)` 看着）。
落地时踩到两处：① 三处**语序相反**（本文件 §0 是「条数 / 通道」、守卫是「通道 / 条数」）
⇒ 必须用**具名组**取数，按位置取会自己报假异常；② `VisibleChannels` 是**数组**，
`.Count` 会解析成 LINQ 扩展方法 → 方法组 → **CS8917** ⇒ 必须 `.Length`。
生产负控 **3 支 3/3**（三支都只让那条判据红、且点名到位），写回原文 `sha256` 逐字节一致。

**归位后的新数字**：

| 量 | 值 | 来历 |
|---|---|---|
| 通道数 | **14** | `VisibleChannels` 条目数（阶段 99 新增 `Services/LoginIssue.cs`） |
| 条数 | **94** | 阶段 98 的 89 ＋ `Services/LoginIssue.cs` **5 条** |
| 这 5 条的来源 | **新造**（与阶段 87 / 88 / 91 / 97 / 98 同源 —— 不是搬迁、不是归位、也不是"第一次被看见"） | `AuthenticationService` 里原本只有**审计口吻的短句**（`账户已锁定` / `密码错误 (n/m)` / `用户不存在: X`），那些是给审计日志看的；界面侧原本只有一句**写死的中文**（`login.err`，五档失败共用）⇒ Core 里这 5 句是**真的新写的** |
| 判据 | `VisibleChannels_CountIsWhatTheDocSays`（三处文档 × 两个数字）＋ 内嵌负控 | 见专题 §99.8 |


| 面 | 阶段 96 | 阶段 97 | 阶段 98 | 为什么这么动 |
|---|---|---|---|---|
| Core 中文台账（通道 / 条数） | 11 / 85 | 12 / 87 | **13 / 89** | 阶段 97 ＋`Services/UserAdminIssue.cs`（2）；阶段 98 ＋`Services/RecipeIssue.cs`（2）—— **两次都是净增** |
| 直通面 | 15 · 14 | 12 · 11 | **12 · 11** | 阶段 97 −3 站；阶段 98 **条数一个没动**（`status.reload.failed` 那一行换了文件） |
| M6 面（源码字面量） | 373 | 373 | **373** | 不动 |
| `UnusedBaseline` | 1113 | 1113 | **1113** | 不动（阶段 97 ＋1 `Lang` 常量 / 阶段 98 ＋0；两族的键都由出口推导） |
| M1 面 | 702 | 703 | **703** | 阶段 97 ＋1（`Lang.UserMgrPwdResetFailed`）；阶段 98 ＋0 |
| 可达机制条数 | 14 | 15 | **16** | 阶段 97 新增 **M15**（面 3）/ 阶段 98 新增 **M16**（面 2） |

**两条新通道**（与 §12 的"归位"、§13 的"换消费者"都不同 —— 这两条是**净增**）：

| # | 文件 | 条数 | 消费方 | 消费方式 |
|---|---|---|---|---|
| ⑫ | `Services/UserAdminIssue.cs` | **2** | `src/HmiEditor.App/Services/ExternalText.cs` | `UserIssueKey(userAdmin.Issue.Code)`（**异常档**：`FailureReason` 的 ②′ 支） |
| ⑬ | `Services/RecipeIssue.cs` | **2** | `src/HmiEditor.App/Services/ExternalText.cs` | `RecipeIssueKey(issue.Code)`（**返回值档**：`Describe(RecipeCloneIssue)`） |

⚠ ⑫⑬ 两条的**出口在同一个文件里、但不是同一条路**：⑫ 走 `FailureReason(Exception)`
（收的是**异常**），⑬ 走 `Describe(...)`（产出方**不抛**，失败是**返回值**）。
⇒ 消费标记**不能**互相顶替，判据是逐条比 `Marker` 字符串的。

⚠ **来历的差异**（三次"新造"的强弱不同，值得记一笔）：

1. 阶段 87/88/91：**新造回落文案**（原来根本没有可读的失败原因）；
2. 阶段 97：**新造** —— 那 2 条改动前是不进本表的裸 `ArgumentException`（中文，但既不可达也无键）；
3. 阶段 98：**更空的新造** —— 改动前 `CloneRecipe` 失败走 `return null!`、消费点**连 `else` 都没写**
   ⇒ 界面上**一个字都没有**（阶段 97 至少还有裸中文异常原文可得）。

⚠ 两次都由**注入态门禁**以 `+ <文件>: 登记 0，实扫 2` **点名**抓到 —— 换句话说：**不登记就红**，
不是靠人记着补。本阶段还顺带确认：`VisibleChannels_CountIsWhatTheDocSays` 的常数必须
与 §3 表内条数之和一致（**89**），**"镜像"这句话两边都要动**。

## §18 真新增（第四次）：通道 **14 → 15**、条数 **94 → 100**（阶段 100）

**这一次是"净增"还是"归位"**：**净增** —— 6 条中文是**新写的**，改动前 `RecipeManager.ImportCsv`
对坏行 / 非数字的值 / 表头空列名**一个字都不说**（只 `return` 一个 `int`，站点拿它报
「成功导入 N 个配方」）⇒ 界面侧**连一句可读的失败原因都没有**。

| 量 | 值 | 来历 |
|---|---|---|
| 通道数 | **15** | `VisibleChannels` 条目数（阶段 100 新增 `Services/RecipeImportIssue.cs`） |
| 条数 | **100** | 阶段 99 的 94 ＋ `Services/RecipeImportIssue.cs` **6 条** |
| 这 6 条的来源 | **新造**（与阶段 87 / 88 / 91 / 97 / 98 / 99 同源） | 改动前那 7 处静默里**没有一条**带可读文案：S1/S2 共用一句站点硬编码的「CSV 文件格式无效或数据为空」（那是 **App 侧**写死的，不在 Core 台账里），S3/S4/S5/S7 **连一句话都没有** |
| 判据 | `RecipeImportIssueLocalizationGuardTests` **11 条** ＋ `VisibleChannels_CountIsWhatTheDocSays`（三处文档 × 两个数字） | 见专题 §100.4 / §99.8 |

**新通道**（与 §12 的"归位"、§13 的"换消费者"、§15–§17 的"净增"同类 —— 它是**第四次净增**）：

| # | 文件 | 条数 | 消费方 | 消费方式 |
|---|---|---|---|---|
| ⑮ | `Services/RecipeImportIssue.cs` | **6** | `src/HmiEditor.App/Services/ExternalText.cs` | `RecipeImportIssueKey(issue.Code)`（**返回值档**：`Describe(RecipeImportIssue)`，与 ⑬⑭ **同一条路**） |

⚠ **⑮ 走的是 ⑬（`RecipeIssue`）/ ⑭（`LoginIssue`）那条路**：产出方**不抛异常**，失败是**返回值**
⇒ 与 ⑫（`UserAdminIssue`，走 `FailureReason(Exception)`）**不是同一条路**，
消费标记**不能互相顶替**（判据逐条比 `Marker` 字符串）。

⚠ **⑮ 与前三次"净增"的一个差别**：这 6 条里的 `TOO_FEW_LINES` / `NO_HEADER_COLUMNS` /
`ROW_COLUMNS_MISSING` / `VALUE_NOT_A_NUMBER` 是**结构化失败**（带行号、带变量名、带原值），
而出口是**唯一**的 —— 行号由 `Lang.CsvLinePrefix` 统一加前缀，**行号不进 `Message`**
（与 `CsvImportIssue` 阶段 88 的处置逐条相同）。

**归位后的新数字**：

| 面 | 阶段 98 | 阶段 99 | 阶段 100 | 为什么这么动 |
|---|---|---|---|---|
| Core 中文台账（通道 / 条数） | 13 / 89 | 14 / 94 | **15 / 100** | 阶段 100 新增 `Services/RecipeImportIssue.cs`（6）—— **净增** |
| 直通面 | 12 · 11 | 12 · 11 | **12 · 11** | **条数一个没动** ⇒ **第十二例「面读数是证词、不是目标」** |
| M6 面（源码字面量） | 373 | 372 | **372** | 不动（7 个新键：1 个有 `Lang` 常量走 M1、6 个由前缀拼出走 M18） |
| `UnusedBaseline` | 1113 | 1112 | **1112** | 不动（新增的 `Lang.RecipeCsvImportWarnings` 有引用） |
| M1 面 | 703 | 703 | **704** | ＋1 ＝ 新增的 `Lang.RecipeCsvImportWarnings` |
| 可达机制条数 | 16 | 17 | **18** | 阶段 100 新增 **M18**（面 6） |

⚠ **本节的两次"点名"比数字更值钱**：本阶段新写的两个判据**第一次跑**就把两处**历史遗漏**
点了出来 —— `NoUnclassifiedCoreChinese` 点名 `Services/LoginIssue.cs`（**5 条**，阶段 99；
§16 当年只把自己那两次归位到 13 / 89、**没算自己那一跳**），
`EveryKeyPrefix_IsDocumentedInTheClassComment` 点名 `ExternalText` 类注释里
**M15（阶段 97）的清单条目从来就没写**。⇒ **新判据的第一次跑是一次免费的全库体检**：
它点出来的东西**比它老 1–3 个阶段**，不是它写错了，是此前**没有任何判据看着那两处**。
两处都在阶段 100 的内容批里补齐（通道 ⑭ 与那条 `<item>`）。

## §19 真新增（第五次，形状与前面都不同）：**通道数不动**、条数 **100 → 101**（阶段 124）

阶段 124 落地 `#66②` 时新写了一条审计详情：

```csharp
user.DisplayName = next;
AppendAudit(AuditAction.UserProfileChanged, userId, user.Username,
    $"变更显示名: {old} → {next}", true);   // ← 这一条进了 Core 中文台账
```

**为什么它算"到界面"**：`AuditLogView` 订阅 `AuditLogged`、列表项显示 `AuditLogEntry.Detail`
—— 与同文件既有的 `启用账户` / `禁用账户` / `变更角色: a → b` 走的是**同一条通道**
（本文件 §0 的第 ⑨ 条，`VisibleChannels` 里记 `Services/AuthenticationService.cs`）。

**为什么不改通道数**：前面七次（§15 / §16 / §17 / §18 每次 +1 通道）都是**新增一个失败原因通道**
（`ExternalText` 那一家族：码在 Core、键在 App、字典缺键才回落中文）；这一次是**已有通道里多一条**
⇒ 通道仍 15、条数 +1。

**为什么这不是"该本地化却没本地化"**：审计详情与 `ExternalText` 那一族**性质不同** ——
它要落盘成 JSONL、要能跨语言对账（同一条记录不该因为界面语言不同而不同），所以它**刻意**是
按审计口吻写的中文短句。这正是本表要求"新增中文要么本地化、要么登记一条并写明归类理由"里
**后一半**的用法：归类理由是"审计口吻、不随 UI 语言变"，代码注释与台账两处都写清了。

🩸 **它是门禁实测点名出来的，不是预估**：阶段 124 的门禁在
`NoUnclassifiedCoreChinese` 上报 `Services/AuthenticationService.cs: 登记 12，实扫 13`。
⇒ 又一次印证本文件的老话：**这条判据的第一次跑是一次免费的全库体检**
（它点出来的是"新写的界面文案在台账里没有位置"，而且是**在提交之前**）。
