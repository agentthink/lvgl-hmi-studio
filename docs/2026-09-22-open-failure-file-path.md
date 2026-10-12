# 打开失败的文案：文件名进实参 + 两条路径合模 + 原因必须过码化出口

> 登记表 §4 **#60** 的闭环专题（阶段 82，2026-09-22）。
> 上游：阶段 80（§37，端到端覆盖打开失败）→ 阶段 81（§38，#59 原因码化）→ **本阶段**。
> 数字一律可复跑：探针 `.workbuddy/tmp/probe60.txt`、站点面 `.workbuddy/tmp/s82-face-probe.py`、
> 负控日志 `.workbuddy/tmp/s82-nc-*.log`。

---

## 82.1 缺口形状（先量，不预设）

阶段 81 收尾时把 #60 记为「文案不含文件名 + 两条打开路径的模板本来就分叉」，并**刻意不动**
（理由：它改的是模板**形状**，会让阶段 80 那条端到端判据的断言面跟着变）。本阶段第一步是**量**：
写了一个双语对照探针（`_Probe60Tests`，量完即删），对 **7 条真实码化失败**逐个跑两条路径的**现状渲染**。

| 项 | 1008 路（最近工程 `StartWithOpenFileCore`） | 1168 路（文件选择器 `OpenProjectCore`） |
|---|---|---|
| 模板 | `ui.op.failed.detail` = `{0}失败: {1}`（**所有操作**共用） | `status.open.failed` = `打开失败: {0}`（**只有它用**） |
| zh 渲染 | `打开工程失败: <原因>` | `打开失败: <原因>` |
| 原因那一半经 `FailureReason`？ | **是** | **否**（直接 `ex.Message`） |
| 原因随语言切换 | **7/7** | **0/7** |
| en 界面下原因仍含汉字 | 0/7 | **7/7**（中英混排） |
| 模板里有文件名的槽位 | 无 | 无 |

两条读数的原样摘录（`.workbuddy/tmp/probe60.txt`）：

```
en  1008路: Open project failed: The package is not a valid ZIP file (corrupted, or not a .hmi package)
en  1168路: Open failed: 工程包不是有效的 ZIP 文件（已损坏或不是 .hmi 包）        ← 中英混排
zh 下两条路径的原因逐字相同 7 条（⇒ 分叉只在**模板**那一层，不在原因）
```

⇒ #60 实测含**三件**，不是两件：

| 编号 | 缺口 | 证据 |
|---|---|---|
| **60-A** | 文案不含文件名 —— 两条模板里都**没有**为文件留的槽位 | 上表最后一行 |
| **60-B** | 两条路径的模板分叉（zh 下 7/7 条措辞不同） | 「两路文案不同 7 条」 |
| **60-C** | 1168 路**绕过**码化出口 ⇒ 英文界面下原因那一半永远是中文 | 「1168 路随语言切换 0 条 / 含汉字 7/7」 |

**60-C 是阶段 81 的漏**：`UiOperationGuard` 那条 2 实参重载的注释自己写着
「收成一处的收益是『以后新增的失败出口默认就是码化的』」—— 而 1168 是**原有的、没被记起来**的那处
（提交 `7fed841` 的 diff 里只有 2 处 `MainWindow` 改动，加上 `UiOperationGuard` 内部的 1 处，
恰好是它写进提交信息的「三处」；**1168 不在其中**）。

---

## 82.2 两个先验判断被实测推翻（留着，因为它解释了两处"看起来该改却没改"）

**① 「2 个实参的码会在 1168 那条路上原样吐出 `{0}`/`{1}`」—— 实测 0 条泄漏。**
`FORMAT_VERSION_UNSUPPORTED` 有 2 个实参，走 1168 的现式
（`LocService.Format("status.open.failed", ex.Message)`）本应把未替换的占位符摆到用户面前。
实测没有：Core 的回落原文是**插值好的**（`HmiPackageService.cs:217`
写着 `$"不支持的工程包格式版本 {manifest.FormatVersion}（当前支持 {CurrentFormatVersion}）"`），
所以 `ex.Message` 本身就是一份**完整可用**的中文文案 —— 这正是阶段 81 那条设计要求的副产品
（「回落自身必须可用」，配套判据是「zh 模板渲染 == Core 回落原文」）。
⇒ 1168 的真后果**不是占位符泄漏，而是"它永远显示中文"**（60-C）。**先量，别按形状推后果。**

**② 「阶段 80 那条端到端判据的断言面必须跟着改」—— 实测不用改。**
它钉的是**结构**（`StartsWith("打开工程失败: ")` + 原因非空），而本阶段把路径插在
「操作名之后、原因之前」，前缀原样保留 ⇒ 首跑即绿。这反而暴露了它的**边界**：
它对 60-A **原理上不敏感** —— 由 **NC-A** 正面证明（见 82.8）。

---

## 82.3 处置（三件一起收）

| 编号 | 处置 |
|---|---|
| **60-A** | 新立一条 **3 槽位**模板 `ui.op.failed.detail.file` = `{0}失败: {1} —— {2}`（操作名 / **目标完整路径** / 原因）。路径取**完整路径**而不是文件名，跟既有先例 `status.file.not.found`（`文件不存在: {0}`，调用点传 `filePath`） |
| **60-B** | **合模**：两条打开路径的失败渲染都改走 `UiOperationGuard.FailureDetail` 的**同一个** 3 实参重载；分叉出去的那条模板连同键与常量一并删除（82.6） |
| **60-C** | 出口内部一律过 `ExternalText.FailureReason(ex)` ⇒ 码化异常走字典、**随界面语言变**；未码化的仍回落 `ex.Message`（阶段 81 定的诚实边界不动） |

---

## 82.4 三个设计决定（每条给依据，不给偏好）

**① 文件名作为「实参」进模板，不拼进原因串。**
#60 的原文就是这么写的，理由可以实测化：拼接（`reason = path + " —— " + ex.Message`）也能"显示出来"，
但此后「哪一半是文件、哪一半是原因」在文案里**不可判** ⇒ 想换排版必须改拼接代码而不是改字典。
而且拼接版会**绕过** `C1`（`LocalizedFormatCallSiteTests`：`.Format` 的实参个数必须等于模板占位符数）
—— 那条判据看不见"原因串里藏了别的信息"。

**② 新立一条键，而不是给 `ui.op.failed.detail` 加槽位。**
后者是**所有操作**（新建/保存/导出/编译/下载…）通过 `ReportOperationFailure` 共用的通用模板；
给它加一个文件槽位等于要求**每一个**调用点都交出路径，而多数操作那时并没有路径。新键只服务打开类操作。

**③ 出口收在 `UiOperationGuard.FailureDetail(string, string?, Exception)`。**
`string?` 而不是必填，是因为有一类调用点**结构上就拿不到路径**：
`MainWindow.OpenProject` 的外层 catch 包着**文件选择器本身**（`filePath` 在函数内部声明，
不在它的作用域里）。给那种调用点编一个路径比留空更糟 ⇒ 留空是**正确**表达，出口负责回落。
**除它之外**凡路径在作用域内的失败渲染都带路径 —— `StartWithOpenFile` 的外层 catch
（`MainWindow.axaml.cs:991`，`filePath` 在作用域内）一并改成带路径版。

---

## 82.5 产品改动（5 文件，**+36 / −8**）

行数取自 `git diff --numstat`（提交前实测，非估）：

| 文件 | 改动 | numstat（增/删） |
|---|---|---|
| `Core/Resources/Strings.zh-CN.json` | **−1** `status.open.failed`、**+1** `ui.op.failed.detail.file` | 1 / 1 |
| `Core/Resources/Strings.en-US.json` | 同上 | 1 / 1 |
| `App/Resources/Lang.cs` | **−`StatusOpenFailed`**、**+`UiOpFailedDetailFile`** | 1 / 1 |
| `App/Services/UiOperationGuard.cs` | 新增 `FailureDetail(操作名, 目标路径?, 异常)` | **28 / 0**（含注释） |
| `App/MainWindow.axaml.cs` | 1008 / 1168 改走新出口；991 加路径实参；`ReportOperationFailure` 增可选 `targetPath` | 5 / 5（**净 0 行**，仍 **3013**） |
| **合计** | | **+36 / −8** |

键数仍 **2182**，两本字典集合相等。

`MainWindow` 净 0 行是**被逼出来的**：`SingleFileSizeGuardTests` 是相等判定，而 §26.5 的既有裁定是
「**压回行数、不调大基线**」。第一版把设计说明写成 `MainWindow` 的 `<param>` 块（+9 行）⇒ 直接撞线；
改成把可选参数的语义**下沉到出口那层**（`UiOperationGuard` 没有行数守卫）后归零。

---

## 82.6 删键的连锁（一次"双向闭合"的完整演示）

合模之后 `status.open.failed` 失去唯一使用点 ⇒ **必须**删干净，否则可达性守卫会报"取不到的键"：

| 步骤 | 对象 | 谁在盯着 |
|---|---|---|
| 1 | `MainWindow.axaml.cs:1168` 那处**字面量**消失 | 站点面守卫（J3）+ 本表 2/3 |
| 2 | 两本字典各删 `status.open.failed` | `LocalizationKeyReachabilityGuardTests`（不可达键报红） |
| 3 | `Lang.StatusOpenFailed` 常量删掉 | `LangKeyCoverageTests`（常量→字典的正向判定：常量与键同生共死） |
| 4 | `UnusedBaseline` **1124 → 1123** | `LangConstantUsageGuardTests`；实测报「1123 ≠ 1124 ⇒ 降了 = 清理了死常量，请把基线改成 1123」 |
| 5 | 分桶 **M6 273 → 272** | 同上（该常量的键原先只靠 M6 的整键字面量担保） |

⚠ 一处值得记的细节：**`status.open.failed` 原先只有 M6 担保**（1168 用的是字面量，不是常量）
⇒ 「删掉消费者」与「删掉键」是同一步的两面，谁都不能省。
另外新增的 `UiOpFailedDetailFile` **有代码引用** ⇒ 不进本集，所以可达性面
**M1 650 → 651**、**M6 386 → 385** 而常量基线反而降 1 —— 两者不矛盾：
前者数「**有**代码引用的常量」，后者数「**无**代码引用的常量」。

---

## 82.7 判据（4 条，两文件）

| # | 判据 | 形状 | 它单独能看见什么 |
|---|---|---|---|
| **J1** | `OpenFailureFilePathUiTests.EveryBadPackage_ShowsTheTargetFilePath_EndToEnd` | **端到端**（路径 1）：7 条真实坏包落成真实文件 → `StartWithOpenFile` → 状态栏**逐字等于**「操作名 + 完整路径 + 该档字典原因」 | 60-A + 60-B（路径 1） |
| **J2** | `…Outlet_KeepsPathAndLocalizedReason_InBothLanguages` | **出口 × 双语**：7 条码化失败 × {zh,en} 逐字比对 + en 下**不含汉字** | 60-A + 60-C（**路径 2 唯一的替代证据**） |
| **J4** | `…DivergentOpenFailureTemplate_IsGone` | 结构：`status.open.failed` 不得出现在两本字典或 `Lang` 里 | 60-B 不得回流 |
| **J3** | `ExceptionMessagePassthroughFaceTests.…_MatchesBaseline` | **源码面**：「异常原文直通本地化模板」的站点面（**37 站点 / 27 组合**；面＝**全 `src/`**，2026-09-23 收口，§82.12），**双向**相等判定 | 60-C 的**根因**，且不依赖任何行为入口 |

**期望值一律不从被测对象来**：`Expected()` 只用**字典模板 + `string.Format`** 拼出
（不调 `FailureDetail` / `FailureReason` —— 用被测对象算期望等于把判据写成恒真）；
真实的 `Code`/`Args` 来自 `PackageIssueLocalizationGuardTests.Probe()` 的**真实触发**，
坏包字节来自同一类的 `BadPackages()`（本阶段把它从 `private` 开放为 `internal`，**夹具只此一份**）。

**J1 的驱动每次"上弦"**：驱动前把状态栏置成唯一标记 —— 否则"状态栏里有这个路径"可能只是上一轮的残留。

**J3 的面口径**（`CollectSites`）：**`src/` 下全部项目**（12 个 / 381 个 `.cs`；2026-09-23 收口前
只扫 `src/HmiEditor.App`，见 §82.12）里每个 `.Format(…)` 调用，
首实参能解析成字典键、且后续实参里出现 `.Message`；**必须先剥注释**
（否则规则会被自己的文档满足 —— 本项目已踩三次，登记 #48）。
首实参的形状判定**复用** `LocalizedFormatCallSiteTests.LooksLikeKey`（本阶段把它开放为 `internal`）——
**这不是重构洁癖**：第一版自己写的"是不是双引号包起来的"判定，被负控 NC3 当场抓出
把 `string.Format("{0}", ex.Message)` 里的 `"{0}"` 当成了键。

---

## 82.8 负控（三组**整块回退**，还原逐字节一致）

注入方式：直接改产品文件 → 跑同一批 4 类判据 → 还原 → `sha256` 比对。

| 注入 | J1 | J2 | J4 | J3 | **阶段 80 端到端** |
|---|---|---|---|---|---|
| **NC-A** 路径缺失（两条打开路径改回不带 `targetPath` 的出口） | **红** | 绿 | 绿 | 绿 | **绿** |
| **NC-B** 绕过码化出口（出口把 `FailureReason(ex)` 换成 `ex.Message`） | 绿 | **红** | 绿 | **红** | 绿 |
| **NC-C** 合模回流（`status.open.failed` 装回两本字典 + `Lang`） | 绿 | 绿 | **红** | 绿 | 绿 |

三组注入的**感应集合两两不同** ⇒ 四条判据互不替代。另有两条独立的守卫在 NC-C 下顺带转红：
`LangConstantUsageGuardTests.UnusedLangConstants_MatchBaseline`（常量回来 ⇒ 1124 ≠ 1123）。

**两条最重的证据**（都是"反面"读出来的）：

1. **NC-A 下阶段 80 那条端到端判据照样绿** —— 实测证明「**旧那一层原理上看不见 60-A**」。
   它钉的是结构（前缀 + 原因非空），而本阶段的修法恰好保留了前缀 ⇒ 它对"文案里有没有文件名"
   完全不敏感。与阶段 79 的 NC5（"保存把工程写成空壳"时旧判据 3/3 照绿）同款构造。
2. **NC-B 下 J1 照样绿** —— 原因是 zh 下 `ex.Message` 与 zh 字典值**按设计逐字相同**
   （阶段 81 的第三条判据就是钉这件事）⇒ **端到端判据在 zh 界面上无法区分"走了字典"与"回落了原文"**。
   60-C 必须由 J2（英文界面）与 J3（源码面）分别守。

---

## 82.9 覆盖不到的边界（实测的**硬**边界，不是基建欠缺）

`OpenProjectCore` 那条**选文件**的一步在 headless 里**不可驱动**。这不是"没写桩"，是框架封死：

| 尝试 | 实测结果 |
|---|---|
| 实现 `IStorageProvider` 做桩 | `CS0535` + 编译器原文：**`This interface or abstract class is -not- implementable by user code !`**（整族 `IStorageProvider` / `IStorageFile` / `IStorageFolder` / `IStorageItem` 都带这个标记） |
| 继承 `NoopStorageProvider` 覆盖 `OpenFilePickerAsync` | `CS0122`：**`NoopStorageProvider` 不可访问（internal）** |

顺带探明（留给将来可用）：接缝是 `Avalonia.Controls.Platform.IStorageProviderFactory`，
成员签名 `IStorageProvider CreateProvider(TopLevel)`，由 `TopLevel.StorageProvider` 的 **getter** 惰性解析
（该属性**只有 getter**，无 `set_StorageProvider`）。但既然"返回什么 provider"本身不可实现，
这条路在当前版本上走不通。

⇒ **路径 2 的"选文件"没被覆盖，也不假装覆盖**；它的**失败渲染**那一半与路径 1 同源，
由 J1/J2/J3 覆盖，其中"它确实走同一个出口"由 **J3 在源码面**上钉住（谁把它改回自己渲染，那里会红）。

---

## 82.10 门禁与对拍

### 提交前（`bash .workbuddy/tmp/s82-gate1.sh` → `scripts/ci-gate.sh` 七步）

日志 `.workbuddy/tmp/s82-gate1.log`，mtime **2026-09-23 00:44:31**；**`GATE_EXIT=0`**。

| 步 | 读数 |
|---|---|
| ① 脚本语法 | ✅ 28 个脚本 |
| ② 静态卫生 | ✅ 无硬编码家目录（640 文件）· 无重复接线（40 `.axaml`）· 面板事件只经总线（122 `.cs` / 24 视图成员）· 无按位置定位控件（381 `.cs`，接线 0 / 读取 0） |
| ③ `dotnet build` Release | ✅ **0 警告 / 0 错误**（出包工具链那两段也是 0 / 0） |
| ④ 出包自检 | ✅ 6 个载荷文件，固件 1.1.0 / ABI 3 |
| ⑤⑥ `dotnet test` 三段 | ✅ 三段之和 = 全量，**3137** 通过 / 0 失败（`App.UiTests` **433 → 439**，即本阶段新增的 6 条 `[Fact]`） |
| ⑦ 固件 host 测试 | ✅ 通过（含 `LV_USE_DRAW_SW_COMPLEX_GRADIENTS` / `LV_GRADIENT_MAX_STOPS` 三处同源检查） |

**时序核对**（本项目必做的一步）：门禁日志 mtime `00:44:31` **晚于**全部源码/判据文件的最后一次改动
（最晚的是 `Lang.cs` 与两本字典，`00:42:37`）**1 分 54 秒** ⇒ 这次绿**确实覆盖了本阶段的所有改动**，
不是"改完之前跑的那一次"。另：`RLIMIT_AS 硬限: unlimited`（§5 记的「新路径资源」阻断项本次不存在，
无需走 `.workbuddy/tmp/run-outside.sh`）。

### 提交后

| 项 | 读数 |
|---|---|
| 提交 | **`d764e23`**（10 files，**+614 / −16**，`2026-09-23T01:06:25+08:00`）—— 代码 + 判据一批；登记表与本文档**不进这一批** |
| 门禁日志 | `.workbuddy/tmp/s82-gate2.log`，mtime **01:07:48** ⇒ **晚于提交 1 分 23 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 逐程序集（两次相同） | Hardware 44 / Simulator 44 / Communication 75 / Device 87 / RenderTests 4 / AI 315 / **Core 2119** / **App.UiTests 439**；独占段 Core 8 + App.UiTests 2 |
| 计数增量 | `App.UiTests` **433 → 439**（+6 = 本阶段新增的 6 条 `[Fact]`）；三段之和 = 全量 **3137** |
| 对拍 | 两次汇总**各 67 行**读数（`s82-cmp.py`：剥墙钟 + `sort` 后逐行 diff）⇒ **0 差异**（带行号与去行号两个口径都是 0）—— 比阶段 81 那次更干净（阶段 81 有 1 行差异：路径卫生扫描面随提交变化，本次两跑都在同一批源码上） |

### 文档批 + 第三次门禁（验「纯文档提交不改任何读数」）

| 项 | 读数 |
|---|---|
| 提交 | **`802d1b7`**（3 files，**+504 / −1**，`2026-09-23T01:08:45+08:00`）—— 登记表 §39 + `project-status` 阶段 82 + 本文档 |
| 门禁日志 | `.workbuddy/tmp/s82-gate3.log`，mtime **01:10:05** ⇒ **晚于提交 1 分 20 秒** ✓；`GATE_EXIT=0` / 7 步全绿 / 0 警告 0 错误 |
| 对拍（gate2 vs gate3） | 读数**各 67 行**，**去行号口径 0 差异**；带行号口径 **66/67** 逐字相同 —— 唯一差异是 `[5]` 段在 gate3 的日志里整体行号 **+1**（日志多一行）⇒ **读数内容一字未变**，符合「纯文档提交不该动任何读数」 |

---

## 82.11 未闭环

1. **新增登记 #61**：「异常原文直通本地化模板」的站点面**还有 37 处**，分布在 **27 个 (文件, 键) 组合**上
   （保存/导出/编译/下载/AI 生成/资源导入/登录…）。
   **迁移前后都量过**（`.workbuddy/tmp/s82-face-delta.py`，同一份规则分别吃 `git show HEAD:<path>` 与工作树语料）：
   **迁移前 38 站点 / 28 组合 → 迁移后 37 站点 / 27 组合**，差集**恰好一条**
   （`MainWindow.axaml.cs:1168 · status.open.failed`），反方向**一条都没有**。
   这条差集同时是两个独立实现的交叉核对：python 探针 37/27 与 C# 守卫 `JudgeReadings` 的 37/27 **逐条吻合**
   （探针口径未排除 `obj/**/*.g.cs` 这 9 个生成文件，读数不变 ⇒ 生成物里不含这类站点）。
   阶段 81 的码化收口只覆盖了**经由 `UiOperationGuard`** 的那条路 ⇒ 其余直通点界面切成英文时
   原因那一半仍是中文（与 60-C 同病）。**本阶段刻意不迁**（一次只放开一个变量；且那是 27 个文件的改动面）。
   面已由 J3 的基线**逐条登记**：新增即红、减少也必须同步。
   **2026-09-23 补记**：J3 的面已从 `src/HmiEditor.App` 扩到**全 `src/`**（见 §82.12），
   实测基线**不变** ⇒ 上面这 37/27 同时也是全产品代码的读数。
2. **⑥⑦ 仍是同一句话**（旧格式非 JSON / 内容是 JSON `null` 都落 `status.open.failed` 之外的
   `status.open.parse.failed` = `工程文件解析失败`）—— 两者根因不同、措辞相同。
   修法要区分 `ProjectSerializer.Deserialize` 的两种 `null`（属 Core API 改动），不在本阶段面内。
3. **`MainWindow.axaml.cs` 仍 3013 行**（按 §26.5 既有裁定：压回行数、不调大基线）。
4. **`OpenProject` 的外层 catch 走不带路径的模板** —— 这是**正确**表达（选择器可能还没返回，
   结构上拿不到路径），不是残余；已在出口的注释里写明。

---

## 82.12 补记（2026-09-23）：J3 的面收口到全 `src/`

**动机**：§82.11 曾记「J3 的面只覆盖 `src/HmiEditor.App`，Core / AI 的同类站点不在面内」——
当时是**无已知缺口**（Core 不引本地化、AI 走 `AiIssueCodes`），但那是**巧合**不是判据：
Core / AI 里**新写**一处 `Format(键, ex.Message)`，守卫看不见。

**处置**（只动判据与扫描器，**产品代码 0 行**）：

| 项 | 前 | 后 |
|---|---|---|
| 面 | `AppSourceScanner.ScanAppSources()`（App 122 个 `.cs`） | `AppSourceScanner.ScanAllProductSources()`（**12 项目 / 381 个 `.cs`**；枚举唯一走 `SourceScanner.ProductSources()`） |
| 站点路径 | 项目相对（`Views/…`） | **仓库相对**（`src/HmiEditor.App/Views/…`）⇒ 读数里直接可见"属于哪个项目" |
| 自检 | 文件数 / 语料长度 / 站点数下界 | 追加**「面必须覆盖每一个含 .cs 的项目」**：拿 `SourceScanner.ProductSources()`（另一条枚举器）与判据语料**对拍** |

**为什么那条新自检是必需的**：只数文件数挡不住"面收窄回 App"——App 一个项目就 **122** 个文件，
足以骗过任何总数下界；而"基线相等判定"对此**完全无感**（负控实测：面收窄后基线那条**照样绿**）。

**实测**：先把 python 探针（复刻同一口径）吃一遍全 `src/` ⇒ **Core / AI 各 0 处**；
C# 守卫同口径 ⇒ 仍是 **37 站点 / 27 组合**，基线**一字未动**。即"扩面"没有引入任何新债。

**负控**（`.workbuddy/tmp/nc-j3-face.sh`，把 `ScanAllProductSources` 偷偷改成只留 App）：

| 段 | 期望 | 实测 |
|---|---|---|
| A（注入后） | 只有**自检**红 | **1 红 2 绿** `Face_IsNotDegenerate` FAIL：`只扫到 122 个产品源文件 —— 扫描面失效了`；基线判据与负控判据**绿** |
| 还原 | 逐字节一致 | `sha256` 前后一致（写回原文，不用 `copy2`） |
| B（还原后） | 3 绿 | **3 通过 / 0 失败** |

**门禁**（`scripts/ci-gate.sh`，7 步）—— 这一轮门禁本身也产出了一条缺陷，过程照实记：

| 跑次 | 日志 | 结果 |
|---|---|---|
| 1 | `s83-gate1.log`（挂死现场另存 `s83-gate1-hang.log`） | **挂死**在 App.UiTests 段：宿主 `futex_wait`、`utime` 不涨、零输出 ⚠ **该文件已于 2026-09-23T10:19:55 被阶段 83（定性阶段）的门禁覆盖**（`s83-gate1.log` 这个名字被两个工作流撞了；本表引用的挂死现场请以 **`s83-gate1-hang.log`** 为准，详见 `docs/2026-09-23-exception-passthrough-triage.md` §83.7） |
| 2 | `s83-gate2.log` | 同样挂死 ⇒ **挂了两次才去 blame**（第一次差点记成"环境噪声"） |
| 3 | `s83-gate3.log` | 不再挂（App.UiTests **35 s / 439 条**），但 **1 条已知间歇红**：`DispatcherOwnershipContractTests.CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`（**#47**，登记为 ≈2/23 的窗口，与本轮改动无关） |
| 4 | `s83-gate4.log` | **7 步全绿 / `GATE_EXIT=0`**、**0 警告 0 错误**；三段之和 = 全量 **3137**（大队 `App.UiTests` **439**；独占段 Core 8 + App.UiTests 2） |
| 5 | `s83-gate5.log` | **自行作废**：运行中改了一行注释（规矩：门禁必须晚于最后一次改动） |
| 6 | `s83-gate6.log` | 冻结树复跑 **7 步全绿 / `GATE_EXIT=0`**，逐程序集读数与 gate4 相同 |
| 7 | `s83-gate7.log` | 记完 gate6 后的**最终冻结树**再跑一次 ⇒ **7 步全绿 / `GATE_EXIT=0`** |

**提交与对拍**：代码批 **`f1e0f20`**（3 files，+94 / −36，`09:59:28`）⇒ 提交后 `s83-gate8.log`
（mtime **10:00:37**，晚于提交 **1 分 09 秒**）**7 步全绿 / `GATE_EXIT=0`**；
与冻结树那次（gate7）**剥墙钟 + 排序后逐字相同**（各 67 行读数、**0 差异**；带行号与去行号两口径都是 0）。

**文档批 + 第三次门禁**：**`d24bfe2`**（5 files，+226 / −7，`10:00:52`）⇒ `s83-gate9.log`
（mtime **10:01:55**，晚于提交 **1 分 03 秒**）**7 步全绿 / `GATE_EXIT=0`**，与 gate8
**剥墙钟 + 排序后逐字相同**（各 67 行、0 差异）⇒ 印证「纯文档提交不改任何读数」。

**后续防护（同批）**：`scripts/ci-gate.sh` 三段测试已加
`--blame-hang --blame-hang-timeout ${TEST_HANG_TIMEOUT:-180s} --blame-hang-dump-type none`
⇒ 再出现挂死会**中止并点名**，不必再靠人工 blame。**负控成对**：旧口径（原命令 + `timeout 90`）
⇒ `A_EXIT=124`、日志零人名；新口径（`TEST_HANG_TIMEOUT=30s bash scripts/ci-gate.sh`）
⇒ `B_EXIT=1` 且点名 `NcGateHangScratchTests.HangForever`。加装后 gate10 与 gate9 读数
**剥墙钟 + 排序后 0 差异**。详见 `project-status.md` §82.12。

**挂死的根因是一条判据在读 FIFO**（不是被测代码）：`TMPDIR` / `HMI_TEST_TMPDIR` 都没设 ⇒
`TestTempDir.Root` 就是 `/tmp`，而 `/tmp` 顶层有 **211** 个 FIFO/socket；`Directory.EnumerateFiles`
把它们当文件返回，`JudgeReadingsTests.Write_WithNeitherSink_IsSilentAndSafe` 里的
`File.ReadAllText` 打开 FIFO **永久阻塞**。blame-hang 点名到该用例（`.workbuddy/tmp/s83-uitests-blame.log`），
处置＝只读非空普通文件（`new FileInfo(f).Length == 0 ⇒ skip`；`stat` 不打开文件、FIFO `st_size=0`）。
**负控仍有牙**：往 `/tmp` 顶层放一个非空含 marker 的文件 ⇒ 该条红；删掉 ⇒ 7/7 绿 / 60 ms / 不挂
（`.workbuddy/tmp/jr-fifo-nc.sh`）。教训进技能 `dotnet-test-tempdir-hygiene`；汇总见 `project-status.md` §82.12。
