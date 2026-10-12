# 本地化键「占位符 ↔ 消费方式」闭环（阶段 45）

> 提交 `82c36e0`（守卫 + 修复同一提交）；日期 2026-09-20。
> 上游：阶段 43（占位符判据唯一实现 `LocalizationPlaceholders`）、阶段 44（Core 可见文案盘点）。

## 0. 一句话

`LocalizationService.Format` 在"占位符与实参对不上"时**不抛异常、原样返回模板**，所以
这类错误编译器看不见、运行期不报错 —— 只能靠静态判据挡。本轮把**三种消费方式两两之间**的
关系全部钉死，并修掉实测出的 50 处（33 处调用点 + 17 处 XAML 引用）。

## 1. 为什么「调用点判据」单独不够（本轮最重要的一条）

本地化键有两种消费方式：

| 消费方式 | 形态 | 实参数 |
|---|---|---|
| 代码侧 | `LocService.Format(key, args…)` / `LocService[key]` | 0…n |
| XAML 侧 | `{res:Loc key}`（`LocExtension` → `LocalizedString.Value`） | **恒为 0** |

`LocExtension.ProvideValue` 返回的是绑到 `LocalizedString.Value` 的绑定，而 `LocalizedString`
取的是**原始模板**（索引器语义）。于是只要某个键**同时**被 XAML 无参引用、又被代码带参调用，
`C1`（调用点判据）是**绿的**，而 XAML 那侧渲染的是带 `{0}` 的原始串。

实测 4 个键踩了这条（`var.list.title` / `var.udt.title` / `faceplate.contains.controls` /
`sim.running`），形态完全一致：**XAML 给初始值、代码随后用带参版本覆盖同一个控件**
（`LblTitle` / `LblUdtTitle` / `TxtControlsSummary` / `RunTimeText`）。

危险性在于：**"给模板补 `{0}`" 这种最自然的修法，在这 4 个键上是制造回归。** 判据 `C2`
把这条逼出来了 —— 负控实测（往 `sim.running` 注入 `{0}`）点名：

```
Views/SimulatorView.axaml:252 "sim.running" 被 XAML 无实参引用，模板却含 [{0}]
⇒ 界面（含切换语言后的刷新）会显示占位符字面量
```

即 `RunTimeText`（`SimulatorView.axaml:250`）就是那个控件。正确修法是**另立 0 占位符的兄弟键**：

| 键（XAML 用） | 兄弟键（代码用） | zh / en |
|---|---|---|
| `var.list.title` | `var.list.title.count` | `变量列表 ({0})` / `Variable List ({0})` |
| `var.udt.title` | `var.udt.title.count` | `UDT 类型管理 ({0})` / `UDT Type Management ({0})` |
| `faceplate.contains.controls` | `faceplate.contains.controls.count` | `包含 {0} 个控件` / `{0} controls` |
| `sim.running` | `sim.running.elapsed` | `运行中 {0}` / `Running {0}` |
| `status.target` | `status.target.label` | `目标:` / `Target:`（XAML 换成标签键） |

## 2. 三处根因分类

| 类 | 根因 | 处数 | 界面表现 |
|---|---|---|---|
| **C1-a** | 模板缺占位符（22 个键） | 29 | 实参被**静默丢弃**：`导出失败` 从不说是为什么、`写入成功` 不显示值、`运行中` 连计时都不显示 |
| **C1-b** | 索引器取含占位符的模板（4 个键） | 4 | 界面**直显 `{0}`**（`已删除类型: {0}`、`打开失败: {0}`） |
| **C1-c** | 实参比占位符多 1 个（1 个键） | 1 | 多出的实参被丢弃（`var.udt.added.field` 丢了 `dataType`） |
| **C2** | XAML 无参引用含占位符的模板 | 3 | 界面直显 `产物目录：{0}` / `目标: {0}` / `{0} 项` |
| **C4-a** | XAML 键名拼写漂移（漏层级点号） | 13 | 界面直显键名 `prop.param.valvetype.ball` |
| **C4-b** | XAML 引用了真不存在的键 | 1 | 同上（`prop.param.valve.type.globe` 截止阀） |

`C4` 的 13 处**不是"缺文案"，是"引用写错"** —— 正确键早已存在，只是 XAML 漏了点分隔：

| XAML 原引用 | 既有正确键 |
|---|---|
| `prop.param.tankshowscale` / `.tanktickcount` | `prop.param.tank.showscale` / `.tickcount` |
| `prop.param.pipedirection`(+`.horizontal`/`.vertical`) | `prop.param.pipe.direction`(+…) |
| `prop.param.pipeflowdirection`(+`.forward`/`.reverse`) | `prop.param.pipe.flowdirection`(+…) |
| `prop.param.pipeflowspeed` | `prop.param.pipe.flowspeed` |
| `prop.param.valvetype`(+`.ball`/`.gate`/`.butterfly`) | `prop.param.valve.type`(+…) |

> 这一条值得单独记：如果只看"键不存在"就去补 14 条文案，会**造出 13 组同义重复键**，
> 而且两边文案此后各改各的。判断依据是"给这个界面找既有同义键"，不是"缺什么补什么"。

## 3. 修法

| 动作 | 位置 | 量 |
|---|---|---|
| 模板补占位符 / 改写 | `Strings.{zh-CN,en-US}.json` | 19 个键 × 2 语言 |
| 新增键 | 同上 | 9 个键 × 2 语言（2285 → **2294**） |
| 调用点改键 / 改走 `Format` | `VariableManagerView`(4) `FaceplateTemplateEditorView`(1) `SimulatorView`(2) `MainWindow`(1) | 8 处 |
| XAML 改引用 | `MainWindow.axaml` `DownloadToHmiView.axaml` `ToolboxView.axaml` `PropertyPanelView.axaml`(14) | 17 处 |

两个细节：

1. `VariableManagerView` 的 `DeleteUdt` / `DeleteField` 里，名字变量在**索引器之前就被置 `null`**
   （`_selectedUdt = null` / `_selectedField = null`），所以不能直接补实参 —— 先把名字捕获到局部变量。
2. `MainWindow.axaml.cs:1021` 的 `throw new InvalidDataException(...)` 不能复用
   `status.open.failed`（模板 `打开失败: {0}`）—— 异常消息会被外层 `catch` 再包一层
   `status.open.failed`，出现"打开失败: 打开失败: …"。改用一个 0 占位符键
   `status.open.parse.failed`（`工程文件解析失败`）。

## 4. 判据

`tests/HmiEditor.App.UiTests/LocalizedFormatCallSiteTests.cs`（新增，298 行，5 条）

| 判据 | 面 | 量 | 自检下限 |
|---|---|---|---|
| `BothDictionaries_HaveTheSamePlaceholders`（C3） | 两字典全键 | 2294 | ≥ 2000 |
| `FormatAndIndexerCallSites_MatchTheDictionaryPlaceholderCount`（C1） | App `.cs` | 1118（306 Format + 812 索引器） | ≥ 250 / ≥ 700 |
| `XamlReferences_UsePlaceholderFreeTemplates`（C2） | App `.axaml` | 1306 处引用 | ≥ 1000 |
| `XamlReferencedKeys_ExistInBothDictionaries`（C4） | 同上 | 1113 去重键 | ≥ 800 |
| `IndexerScanner_ResolvesBothLiteralAndConstantKeys` | 自检 | — | — |

**复用而非重写**：判据用 `LocalizationPlaceholders.MatchesArgumentCount`（与运行期同源）、
切实参用 `LocalizedLogCallSiteTests.SplitTopLevelArguments`、字典用
`PluginTextLocalizationGuardTests.LoadDictionary`、健常量用反射取 `Lang` 声明。

**扫描面**：给 `AppSourceScanner` 加了 `ScanAppXaml()`。本地化键有两种消费方式，
只扫 `.cs` 会**整类漏掉**第 2 种 —— 这正是本轮第一个发现（C2）的来源。

**与既有守卫的分工**：`LocalizedLogCallSiteTests` 只覆盖 `LogTagged(tagKey, messageKey, args…)`
（它的 `Call` 正则是 `LogTagged\s*\(`，`.Format(` 不匹配），`C1` 不重复覆盖它；两者互补。

## 5. 负控（三条互相独立）

备份 → 注入 → 跑 → 还原 → 复跑，前后 sha256 核对（脚本 `.workbuddy/tmp/negative-control.py`）。

| 注入 | 目标判据 | 实测点名 |
|---|---|---|
| `var.saved` 两语言模板都去掉 `{0}` | C1 | `Views/VariableManagerView.axaml.cs:464 "var.saved" 模板 zh无占位符 / en无占位符 与实参 1 个对不上` |
| `sim.running` 两语言都加 `{0}` | C2 | `Views/SimulatorView.axaml:252 "sim.running" 被 XAML 无实参引用，模板却含 [{0}]` |
| `ToolboxView` 改引用 `tb.controls.listX` | C4 | `Views/ToolboxView.axaml:40 / :76 "tb.controls.listX" 在 zh-CN 字典里没有` |

- 注入后：**3 红**，且 `C3` 与自检用例仍绿 ⇒ 三条判据互不串味、也没被连带打红。
- 还原后：4 个文件 sha256 **逐文件一致**，复跑 5/5 绿。

> 刻意让每条注入**只**触发一条判据：`var.saved` 两语言同改 ⇒ C3（跨语言一致性）不参与；
> `sim.running` 的代码侧调用点本轮已改走 `sim.running.elapsed` ⇒ C1 不参与。

## 6. 读数

| 项 | 修前 | 修后 |
|---|---|---|
| C1 不符调用点 | 33 | **0** |
| C2 无参引用含占位符 | 3 | **0** |
| C4 XAML 缺键 | 14 | **0** |
| C3 两语言占位符不一致 | 0 | 0 |
| 字典键数 | 2285 | 2294（zh/en 键集一致） |
| `App.UiTests` | 262 | **267**（+5 = 新增判据数） |

## 7. 仍未闭环

1. **占位符语义写错**（阶段 43 登记）：`{0}` 是位置占位符，调用点交换两个实参编译器与判据都看不见 ——
   根治要命名占位符，是独立议题。
2. **`ExternalText.Resolve(key, fallback, args)` 的调用点**（`ProjectSettingsView.axaml.cs:315`）
   实参是 `issue.Args` 数组，**静态不可校验** —— 本判据的面之外，量：1 处。
3. **核心两本字典的键集 parity** 由 `Core.Tests/LocalizationTests` 负责，本文件只断言**占位符** parity；
   两处各守一半属有意分工，但"注释里提到"不等于"判据在同处"，后续若合并守卫应一并收口。
4. **App 侧与 Core 侧仍是两份扫描基础设施**（`AppSourceScanner` vs `TestSupport/SourceScanner`），
   阶段 44 已登记；本轮又给 App 那份加了 `.axaml` 面，合并成本略升。
5. `C1` 只校验**个数**，不校验实参的**类型/来源**（例如把路径传给 `{0:N2}`）。
6. `Lang.cs` 里 `CompileOutput` / `TbSearchCount` 这类常量现在**只被 XAML 以字面量键使用**，
   `.cs` 侧无引用 —— 常量成了纯转介点；`C4` 能挡住键消失，但挡不住常量与 XAML 各自漂移。
