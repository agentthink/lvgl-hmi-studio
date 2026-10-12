# 工程包解包失败的「原因」那一半：诊断码 + 字典 + 可达机制 M9（登记 §4 **#59**，阶段 81）

> 登记表章节：`docs/2026-09-17-open-items-register.md` **§38**；状态章节：`docs/project-status.md` **阶段 81**。
> 上一阶段（同一片区域的另一半）：`docs/2026-09-22-project-file-open-failure-net.md`（阶段 80）。

## 81.1 一句话结论

`HmiPackageService.Unpack` 的失败**以前直接抛 `InvalidDataException`**，App 层把 `ex.Message` 原样拼进状态栏
⇒ 用户看到的是 .NET 的**原始英文异常消息**：① 非 ZIP 是 BCL 的
`End of Central Directory record could not be found.`；⑤ `manifest.json` 坏 JSON 会把
`System.Text.Json` 的内部措辞连 `LineNumber` / `BytePositionInLine` 一起抖到界面上。

本阶段把「原因」那一半**码化**：Core 交「**码 + 实参 + 中文回落原文**」三件东西，App 侧把
「码 ⇒ `package.issue.<码小写>` ⇒ 字典」收成唯一出口。因此

* 英文界面下不再出现中文（这是阶段 70 `ai.*` 的同一套形状，不是新发明）；
* 界面文案**由字典决定**，措辞改了判据跟着走（判据刻意不钉措辞）；
* 新增一条包失败**必然**要在字典里补键，否则守卫红 —— 覆盖由**机制 M9** 兜住。

落地：Core 2 个新文件（7 个诊断码 + 1 个异常类型）、`Unpack` 的七条前置校验失败全部码化、
App 三处接线、两本字典各 +7 键、新判据 **5 条 + 1 条自检**（另含 4 组内嵌负控与同场正控）、
外围负控 **5 组**、可达机制 **M9**（面 7，自己的负控 1 组）、**顺带修掉 Core 中文台账守卫的一处误分类**
（81.7）。门禁 **7 步全绿 / `GATE_EXIT=0`**。

## 81.2 缺口的确切形状

阶段 80 的探针（只写读数、不断言）把 9 条坏输入的状态栏原文逐字读了出来。
**要修的是 ①–⑦ 里「原因」那一段**，而它同时有**三个**毛病：

| # | 坏输入 | 修前状态栏文案（阶段 80 探针实测） | 命中点 | 毛病 |
|---|---|---|---|---|
| ① | 512 字节非 ZIP | `打开工程失败: End of Central Directory record could not be found.` | `ZipArchive` 构造（**BCL 抛的**） | 英文原文 |
| ② | 合法 ZIP，缺 `manifest.json` | `打开工程失败: 缺少 manifest.json，不是有效的 .hmi 工程包` | `ReadJsonEntry<manifest>` 返回 null | — |
| ③ | 合法 ZIP，`formatVersion=9999` | `打开工程失败: 不支持的工程包格式版本 9999（当前支持 1）` | 版本检查 | 无实参结构 |
| ④ | 合法 ZIP、版本对，缺 `project.json` | `打开工程失败: 缺少 project.json，工程包已损坏` | `ReadEntryText(project.json)` 返回 null | — |
| ⑤ | `manifest.json` 是坏 JSON | `打开工程失败: '0xE8' is an invalid start of a property name. Expected a '"'. …` | `JsonSerializer` 抛 | 英文原文 + **内部坐标** |
| ⑥ | 旧格式 `.hmi.json` 非 JSON | `打开工程失败: 工程文件解析失败` | `Deserialize` 返回 null | 与 ⑦ **同句** |
| ⑦ | 旧格式 `.hmi.json` 是 JSON `null` | `打开工程失败: 工程文件解析失败` | 同上 | 不同根因、同一句话 |
| ⑧ | 路径不存在 | `文件不存在: <路径>`（**不带**「打开工程失败」前缀） | `File.Exists` 早退 | 另一条代码路径 |
| ⑨ | 好包、`mode=000` | **打开成功** | root 绕过 DAC | 环境限制 ⇒ **#58** |

三个毛病分别是：**英文原文透传**（①⑤）、**文案里不含文件名**（用户不知道是哪个文件失败，状态栏看着
像"当前工程"出了问题）、**两条不同根因同一句话**（⑥⑦）。第三条属文案粒度、第二条属模板形状 ——
它们与本阶段的码化**不是同一件事**，如实转 **#60**（81.10）。

## 81.3 设计：为什么是「码 + 实参 + 中文回落」三段式

Core 是库，**不引本地化**（依赖方向是 App → Core）⇒ 它**不可能**自己查字典。于是照抄
`AiIssueCodes`（阶段 70）/ `LuaSyntaxValidator` 的 `LUA_SYNTAX_*` 既有约定：

```
Core 侧                           App 侧
HmiPackageException {             ExternalText.FailureReason(ex)
  Code  = "ZIP_UNREADABLE"          ex is HmiPackageException p
  Args  = null / [包内版本, 当前版本]  ? Resolve("package.issue." + 小写(p.Code), p.Message, p.Args)
  Message = "工程包不是有效的 ZIP…"    : ex.Message        ← 未码化的仍回落原文
}                                 UiOperationGuard.FailureDetail(操作键, ex)
```

四条设计决定，逐条都有理由：

1. **码是 `public const string` 而不是 `static readonly`** —— 本地化守卫靠**反射枚举**拿
   「声明了哪些码」与「真正抛得出哪些码」做**双向差集**；反射要求 `IsLiteral && FieldType == typeof(string)`。
   声明了抛不出的 = 死码（它和它的字典键都永远不会被用到），抛出了没声明的 = 漏登记（键不在字典里 ⇒ 回落中文）。
2. **回落原文留在 Core** —— 字典缺键时宁可显示中文原文，也不显示一句丢掉全部信息的「操作失败」。
   代价是这批字面量落在 Core 的扫描面上（81.7 就由此而来）。
3. **`HmiPackageException` 继承 `Exception` 而不是 `InvalidDataException`** —— 后者在 .NET 里是
   `sealed`（实测 `CS0509`，且**全仓没有任何 `catch (InvalidDataException)` 落在解包路径上**）。
   这条实测写进了类注释。xunit 的 `Assert.Throws<T>` 是**精确**类型匹配 ⇒ 既有
   `HmiPackageServiceTests.Unpack_NonZip_Throws` 必须改成断言本类型 + `Code` + `Args == null`。
4. **`OpenZip` / `ReadJsonEntry` 各包一层** —— ① 的异常是 BCL 抛的、⑤ 的是 `System.Text.Json` 抛的，
   **不是我们抛的**。不包这两层，它们就绕过码化路径。`ReadJsonEntry` 还得区分
   「条目**不存在** ⇒ 返回 null」（含义由调用方定：必填项缺 = 另一种码）与
   「条目**在**、内容坏 JSON ⇒ 抛码化异常」。

### 码表（单一来源 `HmiPackageIssueCodes`）

| 码 | 实参 | 触发点 |
|---|---|---|
| `ZIP_UNREADABLE` | 0 | `OpenZip` 里 `ZipArchive` 构造抛 `InvalidDataException` |
| `MANIFEST_MISSING` | 0 | `manifest.json` 条目不存在 |
| `MANIFEST_INVALID` | 0 | `manifest.json` 在、内容非合法 JSON |
| `FORMAT_VERSION_UNSUPPORTED` | **2**（包内版本 / 当前支持版本） | 版本检查 |
| `PROJECT_MISSING` | 0 | `project.json` 条目不存在 |
| `PROJECT_INVALID` | 0 | `project.json` 在、解析不出工程数据 |
| `ASSETS_INVALID` | 0 | `assets.json` 在、内容非合法 JSON |

**刻意不码化的两条路径**（写在 `HmiPackageIssueCodes` 的类注释里，是**诚实边界**不是遗漏）：

| 路径 | 为什么不码化 |
|---|---|
| `targetAssetDirectory` 为空白 | 仍是 `ArgumentException` —— 这是**调用方的编程错误**，不是包坏了，界面上不该有对应文案 |
| `Directory.CreateDirectory` **之后**的写盘失败（资产数据损坏 / `ExtractToFile` 抛 IO） | 没有修法也没有判据（阶段 80 §8.6）—— 码化它等于给一条没人验的分支**假造"已覆盖"的外观** |

两条的可见表现都是回落 `ex.Message`。

### 字典（两本各 +7）

键名由 `ExternalText.PackageIssueKeyPrefix = "package.issue."` + 码**小写**推导（不手写第二份映射表）：

| 键 | zh-CN 模板 |
|---|---|
| `package.issue.zip_unreadable` | 工程包不是有效的 ZIP 文件（已损坏或不是 .hmi 包） |
| `package.issue.manifest_missing` | 工程包里缺少 manifest.json，不是有效的 .hmi 工程包 |
| `package.issue.manifest_invalid` | manifest.json 不是有效的 JSON（工程包已损坏） |
| `package.issue.format_version_unsupported` | 不支持的工程包格式版本 `{0}`（当前支持 `{1}`） |
| `package.issue.project_missing` | 工程包里缺少 project.json（工程包已损坏） |
| `package.issue.project_invalid` | project.json 不是有效的工程数据（工程包已损坏） |
| `package.issue.assets_invalid` | assets.json 不是有效的 JSON（工程包已损坏） |

⚠ **zh 模板与 Core 回落原文必须逐字相同** —— 这不是巧合而是判据（81.5 第 3 条）：
界面显示字典那份、字典缺键时显示回落那份，两份不同就等于同一句话有两个版本。
字典是**逐行手术**补的（不用 `json.dump(sorted)`：那会产生 1701 行 diff），键数 2175 → **2182**、两本集合相等。

## 81.4 落地清单

| 层 | 文件 | 改动 |
|---|---|---|
| Core | `Serialization/HmiPackageIssueCodes.cs`（新） | 7 个 `public const string` + 类注释（含"为什么必须是 const、为什么两条不码化"） |
| Core | `Serialization/HmiPackageException.cs`（新） | `Code` / `Args`（`IReadOnlyList<object>?`，空则 `null`）/ `Message` = 回落原文 |
| Core | `Serialization/HmiPackageService.cs` | 七条失败路径码化；新增 `OpenZip` / `ReadJsonEntry<T>`；`<summary>` 补**失败契约**与**落点契约**两段 |
| App | `Services/ExternalText.cs` | `PackageIssueKeyPrefix` / `PackageIssueKey(code)` / `FailureReason(Exception)`；类注释「三条键约定」→「四条」 |
| App | `Services/UiOperationGuard.cs` | 新重载 `FailureDetail(string operationKey, Exception ex)`；`RunAsync` 里改传 `ex` 而不是 `ex.Message` |
| App | `MainWindow.axaml.cs` | 两处调用点 `ex.Message` → `ex`（**净 0 行**，行数基线 3013 不动） |
| 字典 | `Core/Resources/Strings.{zh-CN,en-US}.json` | 各 +7 行 |
| 判据 | `tests/HmiEditor.App.UiTests/PackageIssueLocalizationGuardTests.cs`（新） | 5 条判据 + 1 条自检 + 4 组内嵌负控（同场正控） |
| 判据 | `tests/HmiEditor.App.UiTests/LocalizationKeyReachabilityGuardTests.cs` | 八条机制 → **九条**，新增 **M9**（复用 `DeclaredCodes()`，不另抄取数方式）+ 面下界 `("M9-包诊断码", 7)` |
| 判据 | `tests/HmiEditor.Core.Tests/HmiPackageServiceTests.cs` | `Unpack_NonZip_Throws` 改断言 `HmiPackageException` + `Code == ZIP_UNREADABLE` + `Args == null` |
| 判据 | `tests/HmiEditor.App.UiTests/ProjectFileOpenFailureUiTests.cs` | 两处注释更新（阶段 80 时那半还是英文原文）。**反回归刻意不放在这里** —— 它是"只钉结构"的端到端判据 |

提交：**`7fed841`**，13 files，**+820 / −30**（2026-09-23T00:14:31+08:00）。

## 81.5 判据：5 条 + 自检（输入全部来自**真实触发**）

`PackageIssueLocalizationGuardTests` 的核心约束：**不写"预期 Code"清单** —— 那等于把实现抄一遍，
抄出来一旦与实现分家，守卫检查的就是清单而不是代码。所以 `Probe()` 真跑一遍
`HmiPackageService.Unpack`（7 条坏包 × `MemoryStream`），把 `Code` / `Args` / `Message` 读回来。

| # | 用例 | 钉的东西 | 为什么不能省 |
|---|---|---|---|
| 1 | `EveryTriggeredCode_ResolvesInBothLanguages` | 每个真抛出来的码，键在两本字典里都能解析 | 缺键 ⇒ 英文界面显示中文 |
| 2 | `PlaceholderCount_MatchesArgsCount` | 模板占位符个数 == `Args` 个数 | `Format` 在两者不符时**不抛异常、只原样返回** ⇒ 用户会看到 `{1}` 字面量；编译器看不见、运行时不抛 |
| 3 | `ChineseTemplate_RendersToTheCoreFallback` | zh 模板渲染 == Core 回落原文（逐字） | "两份文案同步"的**自动**判据 |
| 4 | `RenderedReason_IsNotTheRawDotNetMessage` | 渲染结果**不含** .NET 原文记号 | **#59 的反回归本身** |
| 5 | `FailureReason_PrefersTheDictionaryOverTheFallback` | `ExternalText.FailureReason` **真的查了字典** | 见下 |
| — | `Probe_CoversEveryDeclaredCode`（自检） | 声明的码都能触发 + 触发的都不是码化异常 | 死码 / 漏登记双向 |

第 4 条钉的是**记号**而不是措辞（措辞可以改）：`Central Directory`（BCL 的 ZIP 报错）、
`is an invalid start of` / `LineNumber` / `BytePositionInLine`（`System.Text.Json` 的内部措辞与坐标）、
`System.Text.Json`。这五样都是阶段 80 探针**实测**到的原文，不是猜的。

**第 5 条为什么必须存在**：zh 字典的值与 Core 回落原文**按设计逐字相同** ⇒
「渲染出来是中文」这件事**无法区分**「查到了字典」与「回落了原文」。构造办法是**毒化回落**
（把 `Message` 换成 `<<回落未被使用>>`）：码是真码、模板在字典里 ⇒ 渲染结果必须是**字典那份**、
毒化串一个都不许出现。这是"同一条注入下两种写法一个绿一个红"的形状，不是自证。

### 内嵌负控（4 组 + 正控同场）

`NegativeControls_EachBreakIsCaughtByTheRightJudgment` 先跑**正控**（真字典下三条纯函数判据都空集 ——
否则"报出来了"可能只是判据恒真），再逐条注入「只打红一条、另外几条看不见」的破坏，
证明四条判据**互不替代**：删键 ⇒ 只有键覆盖看得见；模板多一个 `{9}` ⇒ 只有占位符判据抓得到；
文案改歪 ⇒ 只有"渲染 == 回落原文"抓得到；把回落换成阶段 80 实测的 BCL 原文 ⇒ 只有反回归判据抓得到。

### `Hit` 的 `!` 前缀约定

读数里 `Code` 以 `!` 开头表示"抛出来的**不是**码化异常"（值是 `!` + 异常类型名）。
它不是码 ⇒ 任何按码查字典的判据都会红，而失败消息里能直接看见真实类型名 ——
比"静默漏掉一条"强得多。这条约定在负控 NC2 里正是判红的载体。

### M9：为什么必须有独立机制

新键的模板**带占位符**，而 **M6 的「整键字面量」扫描原理上看不见**它们
（源码里只有 `PackageIssueKeyPrefix = "package.issue."` 这个**前缀**）⇒ 不能靠 M6 兜底。
`Unpack` 的失败发生在 Core 层，而 `HardcodedLocalizationGuardTests` 的扫描面只有 `src/HmiEditor.App`。

M9 的取数方式**复用** `PackageIssueLocalizationGuardTests.DeclaredCodes()`（与 M8 复用
`AiIssueLocalizationGuardTests.DeclaredCodes()` 同一理由：抄两份必然漂移，
而漂移的表现是"一个守卫绿、另一个红"，看不出哪边对）。

## 81.6 负控

### 阶段 81 外围负控 5 组（`nc81.py`，逐条还原 SHA256 一致）

| # | 注入 | 打在哪条腿 | 判红谓词 | 读数 |
|---|---|---|---|---|
| NC1 | App 渲染改回 `ex.Message`（透传 .NET 原文） | App 渲染腿 | `没有把码渲染成字典文案` | 失败 1 |
| NC2 | 非 ZIP 那档改回抛 `InvalidDataException`（不码化） | Core 码化腿 | `有人把某条改回抛别的类型了` | 失败 6 |
| NC3 | 字典删掉 `package.issue.zip_unreadable` 一条 | 字典腿 | `缺 zh-CN` | 失败 5 |
| NC4 | `ZIP_UNREADABLE` 的**回落原文**换成 manifest 那句（码与文案错配） | 一致性腿 | `zh 字典模板渲染后与 Core 的回落原文不一致` | 失败 2 |
| NC5 | assets 那条改用一个**已用过**的码 ⇒ `ASSETS_INVALID` 变成死码 | 自检腿 | `声明了却触发不出来` | 失败 3 |

⚠ **NC2 的自我纠错（顺序本身是判据的一部分）**：自检第一版把
「声明了触发不到」写在「抛出了非码化异常」**前面** ⇒ 注入 NC2 时先命中的是
"`ZIP_UNREADABLE` 触发不到"，方向对、**指认不准**（真正的原因在上游：那条路径不再码化了）⇒
驱动判 `wrong-leg`。修法是把「抛出了非码化异常」提到前面，**并把顺序写进注释**。

### 可达机制 M9 自己的负控 1 组（`nc81c.py`）

摘掉 M9 那一块 ⇒ **2 条判据红**（`EveryDictionaryKey_IsReachableBySomeMechanism` +
`MechanismFaces_AreNonEmpty`），错误消息里 **7/7** 个 `package.issue.*` 键被逐条点名，还原逐字节一致。

这条同时回答两件事：① **M9 是承重的**（不是摆设）；② 那 7 条键**没有**被别的机制（M1–M8）隐形担保 ——
否则摘掉 M9 它们仍会被别的机制捞住、判据不红。**后者正是 #49 那个病**（担保方自己不必是活的）。

### 阶段 80 的六组负控**重跑**

产品换了异常类型、判据文件也动过 ⇒ 上一阶段的六组必须**重验**。
读数：`nc80.py` **5/5**（NC2/NC3/NC5 各「失败 1 / 通过 2」，期望串逐条命中）、
`nc80b.py` 的 NC6 **两层都要红**（Core 腿「目标目录被建出来了」失败 1 / 通过 0；
App 腿「失败的打开留下了工作区目录」失败 1 / 通过 2），还原全部逐字节一致。
落盘：`.workbuddy/tmp/s81-nc80-rerun.txt`。

## 81.7 🩸 门禁比我先发现的：Core 中文台账守卫的 **DevMessage 误分类**

**第一次提交前门禁红了**（这是我"本地已跑绿"之后）：

```
失败 HmiEditor.Core.Tests.CoreVisibleTextInventoryGuardTests.NoUnclassifiedCoreChinese
错误消息:
  Core 侧中文字面量台账与实扫不一致。…
  + Serialization/HmiPackageService.cs: 登记 0，实扫 2
```

不猜、先量。复刻守卫的扫描口径（四个 `Regex` + "往回找包裹调用"的状态机）逐条打印判定，
**实测**出真实形状 —— 那个文件里有 **8 条同族「回落文案」，只因写法不同被分到了两个类别**：

| 写法 | 判定 | 条数 |
|---|---|---|
| `ReadJsonEntry<…>(zip, ManifestEntry, CODE, "中文")`（**实参位**） | **计入**（实扫 2） | 2 |
| `?? throw new HmiPackageException(CODE, "中文")` | 被 `DevMessage` 的 `throw new` **豁免** | 5 |
| `catch { throw new HmiPackageException(ZIP_UNREADABLE, "中文") }` | 同上 | 1 |
| `throw new ArgumentException("资产目录不能为空")` | 同上（**这条是编程错误，确实该豁免**） | 1 |

**病根**：`DevMessage` 这一类的理由写着「面向开发者，翻译后反而无法搜索」，而
`HmiPackageException.Message` **按设计是面向用户的回落文案**（界面正常走字典码）⇒ 逻辑上不属于这一类。
后果不是"少算几条"，是**台账数字会随纯重构漂移** —— 谁把 `throw` 抽成 helper，数字就自己变。

**处置**（按守卫自己的文档「若确认某条属于上面的排除类别，应改进分类规则而不是调数字」）：
新增一条**优先级更高**的规则 `UserFacingException`（`HmiPackageException`）—— head 命中它就**先判计入**，
再走原来的四类豁免；台账登记 **7**（另一条 `ArgumentException` 照旧留在豁免里）。
配套：新增自检 `IsExcluded_DoesNotSwallowUserFacingExceptionFallbacks`（**两侧都断言**：
`HmiPackageException` 的回落文案要计入 —— 写在 `throw` 里和写进 `new` 里两种写法都测；
`ArgumentException` / `InvalidOperationException` 照旧豁免 —— 否则这条规则会被"看着像异常就放过"满足）。

这条改动的**副作用面已量化**：全仓引用 `HmiPackageException` 的只有 `HmiPackageService.cs`
（`grep` 核过）⇒ 只有该文件的计数会变，其余文件一个数不动。

### 这条规则的负控 5 组（`nc81b.py`，含**成对腿**）

| # | 注入 | 期望 | 实测 |
|---|---|---|---|
| NC-A | 整块回退新规则 | 必**红** | 失败 2，台账 `登记 7，实扫 2` ✓ |
| **NC-B** | 把 `throw` 抽成 helper（**正是造成误分类的那个形状**）+ **新规则** | 必**绿** | 绿 ✓ |
| **NC-B'** | **同一注入** + 回退新规则 | 必**红** | 失败 2，台账 `登记 7，实扫 3` ✓ |
| NC-C | 注入第 8 条用户面回落文案 | 必**红** | 失败 1，台账 `登记 7，实扫 8` ✓ |
| NC-D | 注入一条**开发者消息**中文（`InvalidOperationException`） | 必**绿** | 绿 ✓ |

NC-B / NC-B' 是**成对**的：**同一条注入**下新写法必绿、旧写法必红 ⇒ 证明新规则真的堵住了那个漂移洞，
而不是"换了个数字凑对"。NC-D 证明没有把 `DevMessage` 整类取消。还原全部逐字节一致；
`NC-A`/`NC-B'` 同时让那条新自检红 —— 自检与台账**两条腿都不空转**。

⚠ **驱动器自身的一处纠错**：第一版把判红谓词写成「stdout 里没有 `已通过!`」，于是三个真红被判成
`unknown`（实际红行的形状是 `  失败 <用例名>`，**没有冒号**；带冒号的是汇总行 `失败!  - 失败: N`）。
按本项目纪律 **认不出必须算 `unknown`、不算成立** ⇒ 修好解析口径后**重跑**，5/5 才是证据。

## 81.8 未闭环（诚实边界）

1. **未码化的两条路径仍回落 `ex.Message`**（81.3 的表）—— 其中「`Directory.CreateDirectory` 之后的
   写盘失败」既没有修法也没有判据。这是**有意的**：宁可显示原文，也不给一条没人验的分支造"已覆盖"的外观。
2. **#60（新登记）**：失败文案**不含文件名**；且两条打开路径的失败模板**本来就分叉** ——
   `MainWindow.StartWithOpenFileCore` 用 `ui.op.failed.detail`（`打开工程失败: {1}`），
   而 `OpenProjectCore` 用 `status.open.failed`（`打开失败: {0}`）。本阶段**只改「原因」那一半**，
   没有动模板形状（那会让端到端判据的断言面跟着变，属另一件事）。
3. **⑥⑦ 两条旧格式失败仍是同一句话**（`工程文件解析失败`）—— 属上面 #60 的粒度问题，
   而且那条路径在 App 层（`ProjectSerializer.Deserialize` 返回 null），不在本阶段的 Core 码化面内。
4. **端到端可见文案没有单独再读一遍**：本阶段给的是「字典模板 + 实测实参」的渲染值，
   由判据同口径算出（`FailureReason_PrefersTheDictionaryOverTheFallback` 用毒化回落证明
   `FailureReason` 走的确实是这条出口）。阶段 80 那条端到端判据仍**只断言结构**
   （`打开工程失败: <非空原因>`），措辞变了它不红 —— 这是**刻意**的。
5. **`MainWindow.axaml.cs` 行数基线 3013 不动**（本阶段两处改动净 0 行）——
   按登记表 §26.5 的既有裁定：**压回行数，不调大基线**。

## 81.9 门禁与验证

| 项 | 读数 |
|---|---|
| 提交前**第一次** | `.workbuddy/tmp/s81-gate1.log`，**00:07:54** ⇒ **红**：`Core.Tests` 2117/2118，`CoreVisibleTextInventoryGuardTests.NoUnclassifiedCoreChinese`（即 81.7） |
| 提交前**第二次** | `.workbuddy/tmp/s81-gate1b.log`，**00:13:37** ⇒ **7 步全绿 / `GATE_EXIT=0`**，0 警告 0 错误 |
| 提交 | **`7fed841`**，13 files，**+820 / −30**，**2026-09-23T00:14:31+08:00** |
| 提交后 | `.workbuddy/tmp/s81-gate2.log`，**00:15:50** ⇒ **晚于提交** ✓（`GATE_EXIT=0` / 7 步全绿） |
| 对拍 | 两次汇总 **各 26 行**（`cmp81-pre.txt` vs `cmp81-post.txt`）：**25 行逐字相同**，唯一差异是路径卫生那一步的扫描面文件数 `已检查 638 个文件 → 640` —— 该步按 `git ls-files 'src/*'` 取面（`tests/` 有意排除），提交把**两个新的 `src/*.cs`** 纳入面内 ⇒ **差异完全解释** |

逐程序集（两次相同）：Hardware 44 / Simulator 44 / Communication 75 / Device 87 / RenderTests 4 /
AI 315 / **Core 2119** / **App.UiTests 433**；独占段 **Core 8 + App.UiTests 2**；固件 host 测试 9 段全过。

阶段 80 → 81 的计数变化，两条都**正好等于新增判据条数**：
**Core 2118 → 2119**（+1 = 台账守卫的新自检 `IsExcluded_DoesNotSwallowUserFacingExceptionFallbacks`）、
**App.UiTests 426 → 433**（+7 = `PackageIssueLocalizationGuardTests` 的 7 条）。

### 九条机制的面（实测，`HMI_READINGS_OUT` 必须给**绝对**路径）

| 机制 | 面 | 机制 | 面 |
|---|---|---|---|
| M1 `Lang` 常量 | **650** | M6 源码字面量（代理） | **386** |
| M2 `.axaml` `{res:Loc}` | 1113 | M7 枚举名派生 | 12 |
| M3 插件族派生 | 26 | M8 AI 诊断码 | 41 |
| M4 `cfg.note` 清单 | 30 | **M9 包诊断码** | **7** |
| M5 脚本诊断码 | 19 | | |

M6 那一档的配套读数：残差键 **391** / M6 面（判据口径，剥注释语料）**386** / 旧口径（原文语料）**387** /
注释洞收益 **1**（`param.baud.rate`）。

⚠ 上一轮记过一句「HEAD 对照：M1 649 / M6 387」，本轮复查发现**那份 HEAD 读数从未落盘**
（当时 `HMI_READINGS_OUT` 用了**相对**路径 ⇒ 文件没生成）⇒ **那个对照不作数**，
本文件不再引用它。替代取证是 81.6 里 M9 自己的负控：**摘掉 M9 后那 7 条键被逐条点名** ⇒
它们**只**由 M9 担保、没有被别的机制隐形担保（这比"两个面的数字比对"更直接）。

## 81.10 新增登记 #60

| 项 | 内容 |
|---|---|
| **#60** | **打开失败的文案不含文件名；且两条打开路径的失败模板本来就分叉** —— `MainWindow.StartWithOpenFileCore` 走 `ui.op.failed.detail`（`打开工程失败: {1}`），`OpenProjectCore` 走 `status.open.failed`（`打开失败: {0}`）。修法方向：把文件名作为**实参**进模板（而不是拼进原因串），并决定两条路径要不要合成一条模板。本阶段**刻意不动** —— 它改的是模板**形状**，会让阶段 80 那条端到端判据的断言面跟着变（一次只放开一个变量） |
| 定性钉细（#58） | 「权限不足」档仍是**环境限制**（`id -u=0`）⇒ 没有判据，**也不假装有** |
| 定性钉细（阶段 80 §8.6） | 「解包中途失败留半填充目录」**仍未闭环**：本阶段码化了七条**前置校验**失败，这条在 `CreateDirectory` **之后** ⇒ 不在码化面内，判据与修法都没有。✅ **已闭环（2026-09-26，阶段 105）** —— 补 **#62**：2 码（`RESLIB_INVALID` / `ASSET_EXTRACT_FAILED`）＋ 回滚自己建出来的目录 ＋ 判据（含两条正控）；本条上面的措辞**保留原样**（它是阶段 81 当时的记录）。专题 ⇒ `docs/2026-09-26-unpack-midfailure-rollback.md` |
