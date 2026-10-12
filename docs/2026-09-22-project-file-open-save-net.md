# 主窗口「打开 / 保存」路径的行为判据（阶段 79）

> 背景登记项：`docs/2026-09-17-open-items-register.md` §4 第 37 项遗留 ③「文件打开/保存路径未覆盖」。
> 本专题记录**量形状**的过程与结论（含两条被否证的判据形状）、5 条判据、5 组负控。
> 长版叙述在登记表 §36 / `project-status.md` 阶段 79；本篇偏「现场与读数」。

---

## 1 缺口的确切形状

| 层 | 已有判据 | 覆盖到哪 |
|---|---|---|
| 能构造 / 能布局 | `MainWindowConstructionUiTests`（阶段 46） | 构造 + 布局 |
| 接线闭合（143 个具名控件） | `MainWindowWiringUiTests`（阶段 46） | 名字被碰过 |
| 点了有反应（菜单 48 / 工具栏） | `MenuInvocationUiTests`、`MainWindowToolbarInvocationUiTests` | `OwnedWindows` 增量 + 类型 |
| **真的读写磁盘** | **无** | — |

而且**不是"漏了一两个用例"，是这一层被明确跳过**：
`MainWindowToolbarInvocationUiTests` 里

```csharp
Skip("BtnSaveProject", "保存工程：写工程文件到磁盘"),
```

⇒ 工具栏保存按钮从来没被点过。这就是登记表那一行「未覆盖」的具体内容。

## 2 两条真接缝（其余都是"选择器外壳"）

| 入口 | 位置 | 性质 |
|---|---|---|
| `StartWithOpenFile(string filePath)` | `MainWindow.axaml.cs:988` | **唯一不弹选择器**的打开入口（注释原文：「直接打开指定路径的 .hmi 文件，无需弹出文件选择对话框」）；splash「最近工程」用它 |
| `SaveProjectCore()` 的后半段 | `MainWindow.axaml.cs:1178` | `_currentHmiPath is not null` ⇒ **不弹框**直接 `SaveToPath` |

`OpenProjectCore`（:1136）/ `SaveProjectCore` 前半段是**选择器外壳**。

### 🩸 headless 下的选择器是「静默空返回」

实测：点保存（未绑定路径时）**不抛、不弹窗、状态栏不变、文件不动**。
⇒ `OwnedWindows` 在「走了选择器」与「走了直接写盘」两种分支下**都是 0**，
所以「没弹窗」**不可判** —— 它不能当判据，只能当读数。

## 3 驱动：`async void` 的 headless 落地

`StartWithOpenFile` 是 `async void`；headless 里靠
`Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded)` 清队驱动。

### 🩸 别把「轮数」当「时间预算」（本阶段最大的方法论教训）

第一版写「清 N 轮」，用 90 轮 —— 结果 **90 轮 ≈ 几毫秒**，线程池还没把续体调度回来，
于是把「还没跑完」误读成「卡住」，白查了两轮。

改成**墙钟**驱动（清队 + `Thread.Sleep(1)`，直到谓词成立或预算用尽）后：

| 动作 | 轮数 | 墙钟 |
|---|---|---|
| 打开（含解包 / 装面板 / 切屏） | 28 | **114~115 ms** |
| 保存（已知路径） | 2 | **7~8 ms** |
| 打开不存在的路径 | 1 | 0 ms |

⇒ `Drive` 用 5000 ms 预算（约 40 倍余量），**`Done=false` 时调用方必须显式报红**，
不让后面的断言去猜。

### 终态谓词为什么用 `Title`

| 候选 | 行号 | 问题 |
|---|---|---|
| `_currentProject != null` | `OpenHmiFile:1044` 就赋值 | **中途态**（本类第一版据此误判过「打开没落地」） |
| **`Title` 含工程名** | `:1062` 的 `SetUnsavedChanges(false)` 与 `:1063` 是**收尾** | ✅ 用这个 |

## 4 两条「有意行为」⇒ 判据不能用「逐字节等价」

| 行为 | 位置 | 实测 |
|---|---|---|
| 打开时注入 **4 个预置模板**（按 Id/Name 去重） | `InitializeTemplateManager`（:790-806） | 第一次保存把 `"templates": []` 撑成 4 个模板：`project.json` **10866 → 25836** 字符 |
| 保存补上原包没有的 `safeAreaPadding { left/top/right/bottom/isZero }` | 序列化 schema | +123 字符 |

两条都不是缺陷（分别是功能与 schema 演进）；
⇒ **「打开 → 原样保存 → 逐字节等价」在这条链路上必然红，且红在有意的行为上**。
判据改用**幂等**：S1 == S2（实测 25836 == 25836，逐字节相同）。

## 5 判据（`ProjectFileOpenSaveRoundTripUiTests`，5 条）

| 用例 | 断言的可分辨内容 |
|---|---|
| `Open_LoadsPackageContentIntoTheWindow` | 标题含包内工程名 + 状态栏 `控件: 2 \| 屏幕: 2`（与**从 ZIP 直读**的真值比）+ 状态栏指向包内第一屏 |
| `SaveWithKnownPath_WritesBackToTheSameFileWithoutPicking` | 哨兵 mtime 被改（`SaveToPath` 里 `File.Move` 那一步的证据）+ 状态栏 `已保存: demo.hmi` + 无 `.tmp` 残留 |
| `EditThenSave_ChangeSurvivesOnDiskAndNothingElseChanges` | 点 `BtnQuickButton` 加控件 → 保存 → **直接解包磁盘**：首屏控件 +1、原有控件/屏幕/变量/作者/版本**一项不变** |
| `SaveIsIdempotent_AcrossOpenSaveCycles` | 两个窗口两轮：S1 与 S2 的 `project.json` **逐字节相同**；且第二轮**真的写了盘**（哨兵被改，防「平凡幂等」） |
| `OpenMissingPath_ReportsThePathAndKeepsCurrentProject` | 状态栏 == `文件不存在: <那个路径>` + `Title` 不变 + 不弹窗 |

夹具：2 屏 / 首屏 2 个具名控件（液位表、启动钮）/ 1 变量 / `ProjectVersion=7.7.7` / 非默认作者 ——
**刻意不用 `new HmiProject()`**，否则「打开真的装了内容」与「打开只是没崩」分不开。

### 隔离目录必须选 `TestTempDir.NewIsolatedDir`

打开/保存都会走 `RefreshGitState → GitWorkflowService.GetState(_projectDirectory)`，
那里**起 git 进程**做 `rev-parse`；落在仓库里会向上发现父仓库的 `.git`，
把「普通目录」判成仓库、再多跑几条 git 命令。

## 6 负控（5 组，`.workbuddy/tmp/nc79/`）

注入一律作用于**真实产品文件**；还原用**写回原文**（不用 `shutil.copy2`），
每次还原后 `sha256` 与注入前一致（`314000…78e2`）。

| 档 | 注入 | 结果 |
|---|---|---|
| NC1 | `SaveToPath` 里给 `Description` 追加**随机 8 字符**（每存一次加一层料） | **只红** `SaveIsIdempotent` |
| NC2 | 状态栏换成 `status.save.failed` | **只红** `SaveWithKnownPath` |
| NC3 | `projectJson` 序列化成空壳（`new HmiProject { Name = … }`） | 红 `EditThenSave`（+ 附带幂等，差异是空壳的 `createdAt/lastModifiedAt` 时间戳） |
| NC4 | 不存在路径时不报路径 | **只红** `OpenMissingPath`（两条断言同时命中：状态栏无路径 + 状态栏文案不符） |
| **NC5** | **在 NC3 的注入下，跑既有的 `MainWindowToolbarInvocationUiTests`** | **3/3 照样绿**（而 `Skip("BtnSaveProject", …)` 就在那个文件里） |

**NC5 是本阶段最有价值的一条**：它把「旧那一层原理上看不见保存路径」从**推断**变成**实测** ——
保存把工程写成空壳，旧的工具栏判据一条都不红。

### 🩸 负控「绿」时先别下结论：先验证注入真的改变了可观测行为

NC1 前两版**注入无效**：
① `if (A && B && true)` 是**恒等变换**；② 加固定后缀 `-nc1` ⇒ **两侧同加、自收敛**。
第三版改成「每次保存追加随机 8 字符」才打中幂等。
⇒ 负控绿有两个来源（判据有盲区 / 注入没生效），必须先排掉后者。

### 先判结构、再取 `[0]`

NC3 让 `EditThenSave` 抛了裸 `ArgumentOutOfRangeException`（`after.Screens[0]` 越界）
—— 可读的判据消息被换成了异常。改成「先判 `Screens.Count == 0` 再取 `[0]`」后，
失败消息变成「落盘包里一个屏幕都没有 ⇒ 保存把工程写成了空壳」。

### 判据的输入面要自证

几条保存判据的输入是「文件 mtime 变没变」，前提是**哨兵真的立住了**。
时间戳精度不足 / 读回取整时 `LastWriteTimeUtc != sentinel` **恒真** ⇒ 假绿。
⇒ `SetSentinel(path)` 打 `2001-01-01` 之后**回读自检**，不一致就报红。

### 读数必须写在断言之前

`Assert.True` 的红路径会跳过其后的一切 —— 第一版把 `JudgeReadings.Write` 放在断言**之后**，
于是失败消息里写着「见读数」而读数根本没落盘。5 处全部前移（`.workbuddy/tmp/fix_read79.py`）。

## 7 仍未闭环（诚实边界）

① 判据只覆盖**这两条入口**（`StartWithOpenFile` + `BtnSaveProject`）；菜单里的
   「另存为 / 导出」等仍只有静态绊线。
② 「打开后工程被标脏」（`_hasUnsavedChanges = true`、标题带 `*`）是**产品行为问题**，
   已取证触发链（`OpenHmiFile:1060 LoadProjectIntoPanels` → 视图事件 → 静态总线 →
   `WireDockAggregateEvents` → `SetUnsavedChanges(true)`，且「只清队不推进渲染」也照样发生
   ⇒ 必然发生在 `:1062` 的 `SetUnsavedChanges(false)` **之后**），
   但**刻意不断言它**（拿它当断言会把缺陷固化），另行登记。
③ 「原包 → 逐字节等价」被两条有意行为否掉 ⇒ 判据强度上限是**幂等 + 内容清单**；
   若将来模板注入变成"按需"或 `safeAreaPadding` 不再补，可考虑升级为更强判据。
