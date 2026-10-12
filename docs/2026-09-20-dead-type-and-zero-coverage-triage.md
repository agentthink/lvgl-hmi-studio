# 0% 覆盖清单的排查：三个桶、4 个死类型、一条被证伪的判据

承接 `docs/2026-09-20-coverage-tooling.md`（阶段 36）的「仍未闭环 ④：整文件 0% 的清单未逐个查证」。
登记表 **#33 ④**、`docs/project-status.md` **阶段 37**。

覆盖率基线：**20349 / 46162 = 44.1%**（8 份报告、408 个产品文件）。本文处理的是剩下的那 55.9% 里
**整文件为 0** 的部分：**94 个文件 / 16755 行**。

## 1. 先做工具，再下结论

新增 `tools/triage-zero-coverage.py`：把 0% 文件按「**有没有人引用它**」分桶（无人引用 / 活但零测试 /
测试引用了却没执行）。工具自带**正控**（已知被引用的类型必须报出引用数），引用数报 0 就该怀疑工具而不是代码。

分桶结果：

| 桶 | 文件 | 行数 | 性质 |
|---|---|---|---|
| 无人引用 | **1** | 75 | 死代码候选 |
| 活但零测试 | 45 | 7753 | 活代码，没有任何测试执行它 |
| 仅被"读源码文本"的守卫测试提到 | 48 | 8927 | 测试**提到**它，但从未执行 |

按程序集：`HmiEditor.App` 67 个 / 14223 行、`HmiEditor.AI` 13 / 1081、`Hmi.Communication.Builtin` 9 / 1065、
`HmiEditor.Device` 3 / 291、`HmiEditor.Scripting` 1 / 75、`Hmi.Hardware.Loader` 1 / 20。

## 2. 本轮最大的产出：**排查工具自己踩了 5 个误报源**

工具第一版用「**外部引用为 0**」判死活，给出 **104 个**"死类型" —— 几乎全是误报。
逐个查证后收敛到「**全仓出现次数 == 1**」（只有声明那一行），仍踩了 4 个坑。全部过程记在这里，
因为每一条都值得下一轮直接绕开：

| # | 误报源 | 实例 | 决定性证据 | 处理 |
|---|---|---|---|---|
| ① | 文件名 ≠ 类型名：`X.axaml.cs` 的 `stem` 是 **`X.axaml`** | `AnimationEditorView` 被判"无人引用" | 它被 `Wiring.Panels.cs:234` `new` 着 | 类型名**从文件内容解析**，不从文件名猜 |
| ② | **分部类文件** `类名.区域.cs`（`Wiring.Canvas.cs`） | 被当成名为 `Wiring.Canvas` 的类型 | 同目录 `Wiring.*.cs` 是一组 partial | 同上 |
| ③ | **扩展方法容器**：用的时候写 `x.IfNullOrEmpty(...)`，类名一次都不出现 | `StringIfEmpty` / `EnumParseExtensions` 类名各只出现 1 次 | `IfNullOrEmpty` 用了 **30** 处、`ParseOrDefault` **5** 处 | 候选若声明了扩展方法且方法名还有别的出现 ⇒ 判活 |
| ④ | **字面量里嵌别的语言** | `timespec` 被当成一个 C# 类型 | 来自生成器的 `sb.AppendLine("struct timespec ts;")` | `SourceScanner.MaskLiterals`（抹掉字面量内容） |
| ⑤ | **引用写在插值字符串的孔里** | `LvglMacros` 被判死（全仓只有一处引用） | `$"…{LvglMacros.LabelLongMode(x)}…"` —— 整串抹掉就把唯一引用抹没了 | 抹字面量**但保留 `{…}` 孔**（孔里是真代码） |

还有一条**走不通的方向**，同样值得记：为了修 ⑤ 试过"改用保留字面量的文本去计数" ——
结果 4 个死类型**全部被判活**，因为**守卫自己的基线里就写着这些名字**
（`"HmiProperty",` 是一处字符串字面量，各 +1）。**判据被自己的清单喂活 = 恒绿。**
两个方向都试过，只有「抹文本、留插值孔」成立。

> 教训与阶段 36 同族：**判据的失败模式不止"恒真/恒假"，还有"被自己的数据喂活"。**
> 任何"我把待检名字写在测试里"的守卫都要问一句：这份清单本身会不会被算成一次引用？

## 3. 最大的那一块：`MainWindow` 从未被任何测试构造过

48 个文件之所以"测试提到却没执行"，是因为提到它们的都是**读源码文本**的守卫测试
（`HardcodedLocalizationGuardTests`、`AnimationPathSingleSourceTests`、`AppPathsSingleSourceGuardTests` 这类
按行读 `.cs` 做规则检查的），它们只把文件当文本读，从不执行其中任何一行。

其中最大的一项是 **`MainWindow`**：

| 事实 | 证据 |
|---|---|
| `src/HmiEditor.App/MainWindow.axaml.cs` **2370 行**，在**每一份**报告里都是 `0/2370` | 逐报告查过（含 App.UiTests 那份） |
| `src/HmiEditor.App/MainWindow.axaml` **368 行**，同样 0% | 同上 |
| **`grep -rn "new MainWindow(" tests/` 零命中** | 没有任何测试构造过主窗口 |

对比：App 的覆盖是**高度集中**的 —— `PropertyPanelView.axaml` 878/883、`ProjectSettingsView.axaml.cs`
624/688、`App.axaml` 570/570。也就是说 **255 条 UI 测试驱动的是"被 wiring 直接实例化的视图"，
主窗口那一层整个在网外**；App 那 71% 未覆盖里的大头就是它。

这不是缺陷登记，是一个**覆盖面的形状**：改 MainWindow 的构造函数、菜单装配、
文件打开/保存路径时，**没有任何自动判据会红**。要不要补、补到哪一层，是产品决策（见 §6）。

## 4. 4 个真死类型（每个都做过成员级核对）

`tools/triage-zero-coverage.py` 的「无人引用」桶只剩 1 个，但**判死不能只看外部引用**（见 §2 的③）——
所以又对每个候选做了成员名 grep，最终确认 4 个类型**全仓只出现在它自己的声明行**：

| 类型 | 位置 | 行数 | 备注 |
|---|---|---|---|
| `HmiProperty` | `src/HmiEditor.Core/Models/HmiProperty.cs` | 9 | Core 模型，无人使用 |
| `ResourceLibraryVersionInfo` | `src/HmiEditor.Core/Models/ResourceLibraryVersionInfo.cs` | 22 | 资源库版本元数据，无人使用 |
| `LuaScriptValidator` | `src/HmiEditor.Scripting/LuaScriptValidator.cs` | 154 | 见下 |
| `LuaSnippets` | `src/HmiEditor.Scripting/LuaSnippets.cs` | 140 | 见下 |

**`HmiEditor.Scripting` 整个程序集的内容都是这两个类型** —— 而 `HmiEditor.App.csproj` 仍然
`ProjectReference` 着它。即：**一个被引用的程序集里，100% 的代码没人用**（该程序集覆盖率 0/82 行）。

**不擅自删**：删代码要看是否有人在用 / 计划用（`LuaScriptValidator` 像是给脚本编辑器预留的校验入口），
这属产品决策。处理方式与阶段 36 一致：**登记 + 用守卫锁住**，见 §5。

## 5. 把结论固化成判据：`DeadTypeGuardTests`

`tests/HmiEditor.Core.Tests/DeadTypeGuardTests.cs`（Core.Tests 已有若干"读源码文本"的守卫，这是同族）。

判据：类型名在**全仓**（src + tests 的 `.cs`，加 `.axaml`）出现次数 **`== 1`** ⇒ 只存在于声明 ⇒ 登记。
**相等判定**（比 `SingleFileSizeGuardTests` 的约定）：比基线多 = 新增死代码 / 基线被误删 / 口径坏了；
比基线少 = 清理了死类型但没同步改小基线。两种都红。

四个 `[Fact]` 里**三个是元判据**（守"上面那条判据本身有没有坏"）：

| 测试 | 守什么 |
|---|---|
| `DeadTypes_MatchBaseline` | 主判据（相等判定）；失败时把每个名字的「被声明 / 出现次数 / 判定」逐行打出来 |
| `Scan_ExtractsDeclarationsFromBothCsAndAxaml` | 语料不能为空（`.cs` > 300、`.axaml` > 20）⇒ 防"声明集合为空 ⇒ 死类型恒为空 ⇒ 恒绿" |
| `Scan_DoesNotTreatLiteralTextAsDeclaration` | 字面量里的 C 代码不算声明（`timespec` 不在声明集合里，且原文里确实有那串字面量） |
| `Scan_KeepsExtensionMethodContainersAlive` | 误报源③⑤：`StringIfEmpty` / `EnumParseExtensions` / `LvglMacros` 必须判活；`MainWindow` / `CIdentifier` / `PaletteDefaults` 不许进清单 |

配套改动：`SourceScanner.MaskLiterals`（新增）—— 在既有"剥注释"状态机上加一层"抹字面量内容、
**保留插值孔**"，并维持原来的不变量（输出与输入**长度、行数相同**）。

### 负控 2 组（都精确命中，无附带）

| # | 注入 | 期望 | 实测 |
|---|---|---|---|
| 1 | 从基线里删掉 `LuaSnippets` 一行 | 红，并点名 `LuaSnippets` | ✅ `新增（代码里有、基线没有）= [LuaSnippets]`，实况行显示「被声明=True 出现次数=1 判定=死」 |
| 2 | 往 `src/HmiEditor.Core/Models/HmiProperty.cs` 追加一个 `internal class ZzzDeadProbe` | 红，并点名 `ZzzDeadProbe` | ✅ `新增 = [ZzzDeadProbe]` |

**还原**：负控 1 用 sha256 逐字节比对（守卫测试文件当时未入库，`git checkout` 救不了它）；
负控 2 用 `git status --short` 确认工作树干净。还原后复跑 **4/4 通过**。

## 6. 仍未闭环

| # | 项 | 说明 |
|---|---|---|
| ① | **4 个死类型删不删** | 产品决策（见 §4）。删了就把 `ExpectedDeadTypes` 改小（守卫会提醒） |
| ② | **`HmiEditor.Scripting` 程序集去留** | 它的全部内容就是那 2 个死类型，但 App 仍 `ProjectReference` 它 —— 要么补用途、要么整程序集下架 |
| ③ | **`MainWindow` 那一层要不要进网** | 见 §3。最低成本的一步是"能构造出主窗口并走通菜单装配"，属新立的 UI 测试能力 |
| ④ | 扫描的**已知边界** | `$"""…"""` 原始插值串整体抹掉（孔规则不同），可能漏判；相等判定会把漏判立刻报出来，不会静默 |
| ⑤ | 「无人引用」判据**只对 src 生效** | 测试类天然"只出现在声明行"（xUnit 反射发现），所以测试侧不设判据；`HmiEditor.TestSupport` 里的助手类型同理未纳入 |
