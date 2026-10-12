# 停靠面板内部按钮的行为判据（阶段 52）

> 承接登记表 #41 —— 阶段 49 遗留① 的最后一块。
> 判据：`tests/HmiEditor.App.UiTests/DockPanelInvocationUiTests.cs`（提交 `d1b2269`）。

## 0 一句话

主窗口里**停靠的面板**内部的 22 个具名按钮（`Btn*`）现在有了行为判据：按实测分成
Popup 2 / Silent 15 / Broken 1 / Never 4 四桶，取实例走**面板自己的** NameScope，
并配 9 条负控。顺带查出**两个真实缺陷**（重复接线、一键修复崩溃），本轮只登记不修。

## 1 起因：第三种容器

阶段 49 把「按钮接线」分成几种容器，阶段 50 收了**宿主对话框**内部，阶段 51 收了**主窗口
自己的工具栏**（65 个）。剩下的是第三类：**主窗口里停靠的面板**。

面板与前两类都不一样：

| | 宿主对话框（阶段 50） | 主窗口工具栏（阶段 51） | 停靠面板（本阶段） |
|---|---|---|---|
| 打开方式 | 菜单项 → `ShowDialog` | 窗口自带 | 菜单项 `MenuPanelXxx` → Dock 布局 |
| 取实例 | 对话框自己的 NameScope | **主窗口** NameScope | **面板自己**的 NameScope |
| 观察锚点 | 宿主 `OwnedWindows` 增量 | 主窗口 `OwnedWindows` 增量 | 同类，但多数按钮不弹窗 |
| 惰性 | 无 | 无 | `DeferredContentPresenter` 惰性加载 |

登记表 #41 里我此前写的是「没有从菜单用生产路径打开这条路」—— **这句是错的**，
本阶段实测推翻：12 个 `MenuPanelXxx` 菜单项能把 12 个面板全部 `Build` 出来。

## 2 面取证（4 轮，全部实测）

| 轮 | 问题 | 读数 | 结论 |
|---|---|---|---|
| 1 | 默认布局有多少面板可用？ | **5/12** 解析；面板内 `Btn*` 仅 6 个；主窗口 `FindControl` **0 命中** | 未激活 tab 的面板不在 `ViewLocator` 活动注册表里；面板有**独立 NameScope** |
| 2 | 用生产路径能全打开吗？ | 点 12 个菜单项 ⇒ **12/12**；`Btn*` **22** 个；面板 `FindControl` **22/22**；12 个里 **7 个不在可视树** | 取实例走 `panel.FindControl`；"打开" ≠ 在可视树里 |
| 3 | 22 个点下去各自做什么？ | **19 静默 / 1 抛异常 / 2 弹窗**；`AiIcon` +1、`AiTemplate` **+4** | 桶由实测定；`AiTemplate` 的 4 个要查 |
| 4 | 4 从哪来？ | 反射数订阅者 + 框架语义实验 | **双接线(2 次 handler) × 双路订阅(2 个订阅者) = 4** |

第 4 轮的关键实验（一次性探针，已删除）直接在 Avalonia 12 上量框架语义：

| 接法 | handler 被调用 |
|---|---|
| `Click += H` | 1 次 |
| `Click += H` 两次（同方法组） | **2 次** |
| `Click += H` + `AddHandler(ClickEvent, H)` | **2 次** |
| `AddHandler` 两次 | **2 次** |

⇒ Avalonia **不去重**，"同一个 handler 接两路"就是执行两次。

## 3 判据形状

### 3.1 四桶

| 桶 | 个数 | 判据 | 成员 |
|---|---|---|---|
| `Popup` | 2 | 点后 `OwnedWindows` 新增里**含期望类型**的窗口 | `Assets.BtnAiIcon`→`AiIconDialog`；`Faceplate.BtnAiTemplate`→`AiTemplateDialog` |
| `Silent` | 15 | 无新窗口**且**无异常 | Layers 4 / Navigation 3 / Property 3 / ProjectExplorer 1 / Faceplate 3 / Assets 1 |
| `Broken` | 1 | **必然抛** `ArgumentNullException`（缺陷 #44 的回归守卫） | `Property.BtnAutoFix` |
| `Never` | 4 | 只核对能解析到实例，**不点** | 3 个 `BtnImport*` + `BtnChooseScreenBgImage`（都走 `IStorageProvider`） |

`Never` 的 4 个实测**也静默**（headless 的 `StorageProvider` 退化），但那是**平台桩**的行为，
不是接线的行为 ⇒ 不能混进 `Silent` 桶（那会污染"静默"的含义）。真机上点它们会打开系统文件选择器。

### 3.2 Popup 的数量：先不判，修完再判（阶段 53 更正）

**首版（阶段 52）**：`BtnAiTemplate` 实测弹 **4 个** —— 那是缺陷 #43 的症状。把 4 写进期望
等于把缺陷固化成规范 ⇒ 判据只断言「出现了期望**类型**的窗口」，数量另记读数。

**更正（阶段 53）**：数量本身是**一条很好的判据**，只是当时它混着待修缺陷。
双接线删掉后，剩余数量 = **订阅者个数 × 1**，而本类独占进程（#45 的规避）、
且只有一条建窗用例 ⇒ 每个面板视图只被构造一次、每次构造只往静态总线转发一次
⇒ **数量是确定的**（这正是 `record BtnSpec` 加 `ExpectCount` 的前提）。

实测链条：

| 阶段 | `BtnAiTemplate` 增量 | 说明 |
|---|---|---|
| 双接线 + 双路订阅 | **+4** | 2 × 2（阶段 52 的读数，只记了类型） |
| 删双接线后 | **+2** | 2 个订阅者各开一窗 ⇒ **判据红**，这就是"两路消费"的行为证据 |
| 再删宿主直订那一路 | **+1** | 绿 |
| 对照：`Assets.BtnAiIcon` | **+1** | 路由结构相同，但它有 `_suppressAiIconEvent` 防重入 |

⇒ 「多一个窗口」现在直接指向「多一路接线或订阅」。判据红时的消息里写着这句。

### 3.3 `Broken` 桶的设计取舍

`BtnAutoFix` 现在**必然崩**。两个选择：

- 塞进 `Never`（不点）⇒ 没有回归守卫，缺陷被修掉也没人知道判据该更新；
- 单立 `Broken` 桶，断言「**当前**抛 `ArgumentNullException`」⇒ 修好后这条会**红**，
  红信息里直接写着「缺陷像已修好，请把这一行移入 Silent 桶」。

选了后者：把"发现了用户可见崩溃"这件事钉进测试，比写在文档里难忽略得多。

### 3.4 三段断言共用同一个窗口

一条用例里跑三段，各自往 `errors` 里记：

1. **行为**：22 个按桶核对；
2. **枚举 ⇄ 表双向差集**：面板里真实枚举到的 `Btn*` 与分类表互相比对 ⇒ 抓"漏登记"与
   "登记了但枚举不到"；
3. **NameScope 归属**：`panel.FindControl` 应 22/22 命中、`window.FindControl` 应 **0** 命中。

第 3 段不是重复劳动 —— 阶段 51 的教训是「逻辑树看不见集合属性里的控件」，本层的对应事实是
「主窗口看不见面板内的 `x:Name`」。没有它，将来有人把取实例改回 `window.FindControl` 会得到
22 个 `null`，而报错会是"面板按钮没了"，不是"你换了面"。

## 4 顺带查出的两个真实缺陷（阶段 53 已修：#43 前半 / 见 §9）

### 4.1 #43：XAML 与 code-behind **双接线**（8 处 → 实测 **12 处**）

`FaceplateLibraryView.axaml:24` 有 `Click="OnAiTemplateClicked"`，
而 `FaceplateLibraryView.axaml.cs:80` 又有 `BtnAiTemplate.Click += OnAiTemplateClicked` ——
**同一个 handler 接了两路**，Avalonia 不去重 ⇒ handler 执行 2 次。

**口径更正（阶段 53）**：阶段 52 是"按 handler 名扫"且只扫 `Btn*` + `Click` ⇒ 只发现 8 处。
改成「**任意 `x:Name` 控件的任意事件属性** ⇄ code-behind 对同一控件同一事件 `+=`」后
（脚本 `scripts/check-xaml-duplicate-wiring.py`），实测 **12 处 / 5 文件**。
多出来的 4 处**全是非按钮事件** —— 它们幂等，所以用户看不见，但同样违反"一个事件只接一路"：

| 文件 | 控件 | 事件（handler） |
|---|---|---|
| `AiChatPanelView.axaml` | `NewChatBtn` | `Click`（`OnNewChatClicked`） |
| `FaceplateLibraryView.axaml` | `SearchBox` | `TextChanged`（`OnSearchTextChanged`） |
| `FaceplateLibraryView.axaml` | `TemplateListBox` | `SelectionChanged`（`OnTemplateSelectionChanged`） |
| `FaceplateLibraryView.axaml` | `BtnAiTemplate` / `BtnEditTemplate` / `BtnDeleteTemplate` / `BtnNewTemplate` | `Click`（`OnAiTemplateClicked` / …） |
| `PropertyPanelView.axaml` | `CboActionTarget` | `SelectionChanged`（`OnActionTargetSelected`） |
| `ResourceLibraryView.axaml` | `SearchBox` | `TextChanged`（`OnSearchTextChanged`） |
| `SettingsDialog.axaml` | `BtnThemeDark` / `BtnThemeLight` / `BtnThemeOcean` | `Click`（`OnThemePresetClicked`） |

12 处**逐个复核：两侧 handler 全部同名**（`--json` 输出里可以逐条对照），
⇒ 删 code-behind 那半与保留 XAML 那半**完全等价**（XAML 编译期就生成了 `AddHandler`）。

完整因果链（`AiTemplate`）：

```
点 1 次
├─ 双接线 ⇒ handler 执行 2 次
└─ 每次 AiTemplateRequested 命中 2 个订阅者
   ├─ 视图构造里的转发器 → 静态 DockLayoutState.FaceplateAiTemplateRequested（1 个订阅者）
   └─ MainWindow.WireFaceplateAndAiEvents 的直订
⇒ 2 × 2 = 4 个 AiTemplateDialog
```

**对照**：`Assets.BtnAiIcon` 的路由结构完全相同（实例 2 个订阅者），但它是**单接线**，
且 `OpenAiIconDialogAsync` 有 `_suppressAiIconEvent` 防重入 ⇒ 只弹 1 个。
`MainWindow.axaml.cs:697` 的注释写着「实例事件与 DockLayoutState 聚合事件双路触发时只弹一次」
—— 作者知道这个双路，给 `AiIcon` 打了补丁，**`AiTemplate` 这条路漏了**。

用户可见症状：点一次"AI 模板"弹 4 个对话框；选中模板后点删除会弹 2 个确认框；
`NewChatBtn` 新建会话执行 2 次；主题按钮换主题执行 2 次。

**阶段 53 处置（已完成）**：12 处 code-behind 重复接线**全部删除**（XAML 已接同一 handler）
⇒ `BtnAiTemplate` 从 **4 → 2**；再删掉 `Wiring.Faceplate.cs` 里对
`FaceplatePanel.AiTemplateRequested` 的**直订**（保留"视图构造转发到静态总线"那一路）⇒ **2 → 1**。
判据 = `DockPanelInvocationUiTests` 的 `Popup` 桶断言**窗口数量**（见 §3.2 的更正）。
静态守卫 `scripts/check-xaml-duplicate-wiring.py` 已接进 `ci-gate.sh`（不新增门禁步数，仍 7 步）。
**剩下的"两路消费"不止这一处** ⇒ 见 §9 与登记表 #46。

### 4.2 #44：`BtnAutoFix` 一键修复必崩

```
RaiseEvent(Click) → OnAutoFixClicked
  → PropertyChanged?.Invoke(this, _currentControl!)      // PropertyPanelView.Ai.cs:120
  → MainWindow.HandlePropertyPanelChanged(control)
  → _propertyEditSnapshots.TryGetValue(control, ...)     // MainWindow.axaml.cs:2307
  ⇒ ArgumentNullException: Value cannot be null. (Parameter 'key')
```

`PropertyPanelView.Ai.cs:115` 的 `if (_currentControl != null)` 只保护了 `PropX/PropY` 的赋值，
**下一行的 `Invoke` 用了 `_currentControl!`** —— `!` 把编译期警告压掉了，运行期 null 照传。

触发条件：有屏幕级布局分析结果（`_lastAnalysis` 非 null ⇒ `BtnAutoFix` 可见）但**没选中控件**。
外层是 `async`/事件回调，异常直接冒到 UI 线程。

## 5 负控 9/9

脚本 `.workbuddy/tmp/nc-dockpanel.py`（默认只读，`--apply` 才落盘；每条注入后按 sha256 逐字节还原）。

| ID | 注入 | 期望 | 结果 |
|---|---|---|---|
| N1 | Popup 期望类型改成 `SettingsDialog` | red | 成立 |
| N2 | 把 `BtnMoveUp` 从 Silent 改成 Popup | red | 成立 |
| N3 | 把 `BtnAutoFix` 从 Broken 改成 Silent | red | 成立 |
| N4 | 表里删一项（枚举得到但表里没有） | red | 成立 |
| N5 | 表里加一个不存在的名字 | red | 成立 |
| N6 | 第 3 段改用主窗口的 `FindControl` | red | 成立 |
| N7 | 正控：主面下界抬到 99 | red | 成立（把读数逼出来） |
| N8 | 只改注释 | **绿** | 成立 |
| N9 | 删掉 `FaceplateLibraryView` 的 4 行重复接线 + 叠 N7 | red | 成立，**读数 `BtnAiTemplate +2`** |

**N9 是这一批里最值钱的一条**：它把「双接线是 4 的成因之一」从推断变成实测 ——
删掉那 4 行后弹窗数从 **4 变成 2**（剩下 2 个是双路订阅各弹一个，即 #43 的第二半）。
它同时证明我的判据对数量确实不敏感（判据自身仍绿，只有人为抬高的正控让它红）。

两个被改文件（测试 + `FaceplateLibraryView.axaml.cs`）每次均还原成功，`sha256` 前后一致。

## 6 测试隔离约束（写进类注释，否则下一个人必踩）

首跑时 4 条用例里 **2 条红**，红在**构造布局阶段**：

```
InvalidOperationException: The control StackPanel already has a visual parent
ScrollContentPresenter while trying to add it as a child of ScrollContentPresenter
```

根因不是被测代码：`ViewLocator._activeViews` 是**静态**字典，Dock layout 对象的 `Context`
又是 `ViewLocator.Build` 复用视图实例的依据 —— 两者都是**进程级**的。同一个进程里第二次
「建主窗口 + 打开面板」时，新窗口会拿到**已经挂在旧窗口可视树上**的视图实例。

阶段 51 的工具栏用例各建一个窗口却没问题，因为它们**从不打开面板** ⇒ `DeferredContentPresenter`
惰性，面板视图根本没 `Build`。

⇒ 约束：**本类只有一条建窗用例**，三段断言共用同一个窗口。另外 `DockLayoutState` 是静态事件总线
且从不解绑，用例开头用 `ResetStaticDockBus()` 清空静态事件订阅（新窗口构造时 `WireEvents()`
会重新订阅，所以清完仍是完整接线的），避免"前面已关闭窗口的 handler 照样跑"污染读数。

### 6.1 门禁复跑产出：与大队**同进程**时宿主僵死 ⇒ 独占进程（登记表 #45）

把文档写完跑门禁才发现它过不去。第一次当成"跑得久"、第二次当成"被会话回收"，做了取证才定性：

| 档 | 条件 | 读数 |
|---|---|---|
| A | 全量（本类与大队**同进程**） | **4/4 僵死**：零输出、零托管异常、零 CPU；blame 原文「**指定的非活动时间(90 秒)已过**」 |
| B | **同一命令**剔掉本类 | **321/321 绿 / 20 s**（`EXIT=0`） |
| C | **单独跑本类** | **3/3 绿**（2/2，各 1 s） |

档 A 的 blame 报告**单独点名**本类：
`发生崩溃时运行的测试: …DockPanelButtons_BehaveAsDeclared` —— 只此一条
（历史那次同类事故是 9 条分属 9 个类 = **并发集合**，两者形态不同）。

**读报告时别被 VSTest 的兜底话术带走**：它写的是"测试主机进程崩溃"，而同一个报告里数据收集器写的
是「指定**非活动**时间已过」⇒ 真实签名是**宿主卡住不响应**（僵死），不是崩溃。

机制线索：`UiTestHost` 的 headless 会话是**进程级静态单例、绑死在第一个会话的 UI 线程上**
（`UiTestHost.cs` 的注释说明同一进程里开不出第二套平台配置 —— 另一个渲染工程就是为此走**独立
testhost 进程**）。本类是仓库里**唯一**会一次打开 12 个停靠面板、并在秒级持续占用那条 UI 线程的用例。

**规避（明确不是修复）**：`scripts/ci-gate.sh` 测试那一步把它单独成段跑，与那 8 个 git 用例同形 ——
**不放宽任何断言**（它照跑、断言照旧，只是不再与大队同进程）。**根因未定**，进登记表 **#45**。

顺手排除掉一个假设：先把 `ResetStaticDockBus()` 改成 no-op 跑全量（实验 E1）**照样僵死**
⇒ "清全局静态订阅"不是成因。

**落地后复跑门禁**（已提交的树 `fd288e5`）：**`GATE_EXIT=0` / 7 步全绿**（步数不变 —— 新分段塞进
已有的"需要独占 / 空队列"那一步，否则一堆文档里的"门禁 N 步"全过期）；大队段 `App.UiTests`
**321**（23 s）+ 新段 **2**（1 s）= **323**；日志 `.workbuddy/tmp/ci-gate-2026-09-21-dockpanel5.log`，
mtime **`11:26:18`** 晚于提交 **`11:25:23`**（墙钟 50 s）。

### 6.2 阶段 59：竞态前提实测并已消除；但挂死本身**复现不出**（0/5）⇒ 规避保留

**先量"前提"，不去复现偶发挂死。** 复现要等 90 秒超时、还只拿到"挂/不挂"一个比特；
而它的**前提**（同一时刻有几个派发在飞）是**确定性**的，可以直接量。

`UiTestHost` 加了一个**默认关闭**的诊断（设 `HMI_DIAG_OUT=<文件>` 时在进程退出写汇总，
不设只多两次 `Interlocked` 自增）。读数：

| 配置 | `dispatches` | `maxInFlight` | `overlapEvents` |
|---|---|---|---|
| 默认（集合并行，闸门**未加**） | 195 | **20** | **149** |
| `--settings serial.runsettings`（强制串行） | 195 | **1** | **0** |
| 加闸门后（集合并行照旧） | 196 | **1** | **0** |

第一行与第二行**同一套代码、同一条命令**，唯一变量是 xunit 是否并行 ⇒ 计数器既**敏感**又**可分**。
第一行还留了首次重叠现场：`线程 [11, 21, 19, 28, 22] 同时在飞` ⇒ 确实是**不同测试的线程**
（本机 `nproc`=20，xunit 起 20 个 worker，所以是 20）。

**机制（IL 核实，`Avalonia.Headless.dll`）**：`HeadlessUnitTestSession.DispatchCore`
在**调用线程**上直接执行 `EnsureIsolatedApplication()`，其实现是

```
scope = AvaloniaLocator.EnterScope();
Dispatcher.ResetBeforeUnitTests();
_appBuilder.SetupUnsafe();
```

—— **三行全是进程级操作，全程无锁、无重入判定**。语义上这也站不住：这个 API 的模型是
**一个会话一条 UI 线程**（`Dispatch` 单数），并发派发等于同时存在 20 条"UI 线程"。

**落地：派发闸门**（`UiTestHost.Run/RunAsync` 包一层 `SemaphoreSlim`）。
选它而不是"给 30 个测试类各加 `[Collection("headless")]`"，是因为后者**漏一个就静默退回竞态**，
而入口只有一个、闸门**忘不掉**。三条设计：

1. **带 2 分钟超时** —— 万一自锁，抛一条点名异常（带 `DescribeSessionState()` 现场），
   把"挂死"变成"失败"；
2. **同步路径支持同线程重入**（`[ThreadStatic]` 深度），派发体里再调 `Run` 不自锁；
3. **异步路径只做普通互斥**（续体可能换线程）—— 已核实两个使用点都不在派发体里再调 `RunAsync`。

**代价实测为零**：程序集 **19~22 s → 18~21 s**。

**负控（已实测）**：临时摘掉闸门 ⇒ 新守卫
`HeadlessDispatchContractTests.ConcurrentDispatchRequests_AreNeverInFlightTogether` **红**，
报 `本进程历史最大值 20`；恢复后绿。这条守卫**自己起 20 个线程制造并发**，
所以它同时是"闸门有效"和"计数器活着"两条的正控。

**但挂死本身本轮复现不出**：用门禁 `[5]` 那条**原命令**、只去掉「排除本类」，跑 **5 次 = 5/5 绿**。
对照阶段 52 记的是「全量 4/4 挂」。候选解释（**未验证**）：阶段 53 / 54 删掉 12 处双接线（#43）
与 28 处宿主直订（#46）后，这一类里每次交互的处理器执行次数减半，
曾把 UI 线程占满数秒的那部分负载可能随之消失。

⇒ **规避照旧保留**。理由是纪律：0/5 在统计上什么都说明不了（真实 p=0.5 时 5 连绿仍有 3% 概率；
rule of three 只把 p 上界压到 0.6）。撤销规避要另立一次复现率研究。
不过机制层面已经变了：并发重建平台这条竞态既然被闸门堵上，"同进程僵死"就少了一条最可疑的成因。

**顺带更正阶段 57 的一条空转证据**：57.2 里「C 集合并行」用的 `-p:ParallelizeTestCollections=false`
是 **MSBuild 属性**，而 xunit 只从 `xunit.runner.json` / `.runsettings` 读并行度，
全仓**没有任何构建文件**把它转发给 xunit ⇒ **那一刀从未执行**（不是"否证"）。
正确开关是 `.runsettings`，本次实测有效（20 → 1）。教训：**"实验跑了"不等于"自变量真的被改了"** ——
一次只放开一个变量之外，还要**验证变量真的动了**（本例拿并发度读数自证）。

### 6.3 阶段 59 补记之二：提交后门禁里 `[5]` 的 UI 段出现一次 **141 秒**（成因未证，已排除四条）

**读数**（`ci-gate-59-post.log`，mtime `19:17:21`，晚于两笔提交）：

| 程序集 | 提交前（59.8） | 提交后（59.9） |
|---|---|---|
| 其余 7 个（`Simulator` / `Hardware` / `Communication` / `Device` / `Render` / `AI` / `Core`） | 44/150ms、44/226ms、75/2s、87/4s、4/8s、315/9s、2116/16s | **逐项逐毫秒相同** |
| **`App.UiTests`** | 328 条 / **24 s** | 328 条 / **2 m 21 s** |

门禁本身**7 步全绿、`GATE_EXIT=0`、0 警告、计数 3023 与提交前逐程序集相同**。
把 `.workbuddy/tmp/` 里 56 份历史门禁日志的同一读数排开，**取值范围 13~31 秒**（最长三次：
55 的 31 s、55-post 的 29 s、56 的 28 s）⇒ 这一次越出上界约 **4.5 倍**，是列表里唯一一次。

**这张表本身就是判据**：其余 7 个程序集一条不差 ⇒ **不是机器整体变慢**，
只可能是 `App.UiTests` 这一个进程**内部**的事。顺着这条线排除掉三条：

| 假设 | 判据 | 结论 |
|---|---|---|
| 机器整体 CPU 竞争 | 同一次 `[5]` 里 7 个程序集逐项逐毫秒相同 | **排除** |
| 内存压力 / 换页 | 每跑采样 `MemAvailable` 最小值 **54.4 GB**（64 GB 机器） | **排除** |
| 门禁窗口里有别的测试批在跑 | `stat` 全部阶段 59 测量日志，最后一份 `19:12:00`；门禁 `19:14:37` 才启动 | **排除** |
| 渲染定时器按平台**泄漏线程** | IL 一行否掉，见下 | **排除** |

最后一条本来是**最像**的：`SleepLoopRenderTimer.LoopProc` 是一条独立后台线程
（`new Thread(LoopProc)` + `IsBackground=true` + `_wakeEvent.WaitOne(帧间隔)`），
停下来的唯一路径是 `Tick` 被置 null；而 `PerTest` 是**逐派发重建平台**，
216 次派发若各留一条 60 fps 的线程，正好能解释"只有这一个进程慢"。
否掉它的是一行 IL —— `AvaloniaHeadlessPlatformOptions::.ctor`：

```il
IL_0001:  ldc.i4.s   60        // Fps = 60
IL_0009:  ldc.i4.1             // ShouldRenderOnUIThread = **true**（关键）
IL_0010:  ldc.i4.1             // UseHeadlessDrawing = true
```

`ShouldRenderOnUIThread` 为 true 时走的是 **`HeadlessRenderTimer`**（`StartCore` 建的是
`DispatcherTimer(DispatcherPriority::UiThreadRender)` + `Interval = 1/Fps` + `_forceTick` 委托），
**不是独立线程**。仓库侧核实：全仓只有两处构造 `AvaloniaHeadlessPlatformOptions`
（`UiTestHost.cs:491` / `RenderTestHost.cs:48`），**都只设 `UseHeadlessDrawing`**，
没人设 `ShouldRenderOnUIThread` 或 `Fps` ⇒ 全走默认 `true`。
**⇒ 假设不成立，也就不必去"加线程计数再跑 30 轮"。**
顺带露出下一个该查的点：那枚 `DispatcherTimer` 是**注册在 Dispatcher 上**的，
`PerTest` 每次重置 Dispatcher 之后旧平台留下的计时器归谁 —— 是候选，不是结论。

**复现率**（同一条命令，唯一变量是时间）：`App.UiTests` 单跑 **36 次全绿**（20~23 s）
＋ `[5]` 原命令 **6 次全绿**（27~36 s）；合 51 份历史门禁 = **93 次观测里 1 次异常**
（`p̂ ≈ 1.1%`，rule of three 给 95% 上界约 3.2%；单看补跑 42 次是 7.1%）。
⇒ 靠再跑几轮抓不到它，这条经验与 §6.2 对 #45 的处置是同一条。

**处置**：① **不撤结论、不放宽任何断言**（异常那一跑 328/328 仍全绿，计数与提交前相同）；
② 把 `UiTestHost` 那个默认关闭的诊断（`HMI_DIAG_OUT`）从"一个总时长"扩成**能区分的两段**：

| 新读数 | 回答什么 |
|---|---|
| `maxGateWaitMs`（＋第几次派发） | **在闸门外排队**排了多久 ⇒ 指向闸门 / 线程池调度 |
| `maxBodyMs`（＋第几次派发） | **某个派发体自己**跑了多久 ⇒ 指向被派发的那段代码或框架 |

基线（单跑程序集 **36 次**）：`maxGateWaitMs` **3,404~5,885 ms**、`maxBodyMs` **3,144~5,751 ms**、
最慢用例 `RenderResourceCacheTests.TextIsStillDrawn_WhenCachedFormattedTextOutlivesItsDispatch`
**4.7~5.2 s**（前 6 次里 6/6 都在榜首或次席）。配 `--logger trx` 即可指名到用例。

**仍未闭环**：141 秒的**成因未证**。下一步是等它再出现一次（现场已留），而不是继续赌复现。

## 7 不抓什么

1. **不判副作用**：切语言、写状态栏、改内存对象都不可见，也不该在这层判；
2. **不做真实命中测试** ⇒ 按钮"可不可达"不在面内（7 个面板不在可视树里也能取到实例并触发）；
3. **走 `IStorageProvider` 的按钮不点**（4 个）—— headless 下退化为静默，观测到的是平台桩；
4. **窗口数量只判 `Popup` 桶的"本次增量"**（见 §3.2 的更正），`Silent` 桶仍要求"零新窗口"。
   静态事件总线的订阅**从不解绑** ⇒ 订阅者个数会随"本进程此前建过几个主窗口 / 面板实例"增长，
   所以这条判据成立的**前提是本类独占进程**（#45 的规避）。

## 8 仍未闭环

- **#43 前半已修**（§4.1）：12 处双接线全删 + `AiTemplate` 的宿主直订删除；
  `Popup` 桶的判据从"只判类型"升级为"判**数量**"。
- **#44 已闭环（阶段 54）**，而且是**被 #46 顺手修掉的**：null 仍会被发出（`_currentControl!` 一个字没动），
  但唯一剩下的消费方（`PropertyPanelView` 构造里的转发器）**带 null 守卫** ⇒ 崩溃消失。
  判据自带的「修好后请把这一行移入 Silent 桶」提示**正好生效**，已移桶并把 `Why` 改成
  「**不许把实例直订加回来**」。残留：无选中控件时现在是**静默无操作**（见 §10.4）。
- **#46 已闭环（阶段 54，按路线 A）**：实测删除 **28 处**（口径又放宽一次，26 → 28，见 §10.1），
  视图侧转发**一处未动**；读数 `BtnAiTemplate` **+4 → +2 → +1**。新静态守卫进门禁（见 §10.3）。
- **负控 N9 已过期**：它注入的正是被删掉的那 4 行 `BtnAiTemplate` 接线 ⇒ 现在注入回去
  不再是"复现缺陷"（而是"往 XAML 已接的地方再加一路"，仍会红，但语义变了）。
  重做时应改成对 `ExpectCount` 的正控，或挑一个"XAML 未声明"的控件以覆盖合法形态。
- 静态守卫的**两条腿都齐了**：XAML 声明面（阶段 53 的 `check-xaml-duplicate-wiring.py`）+
  视图事件面（阶段 54 的 `check-dock-bus-only-subscription.py`，见 §10.3）。
- 登记表 #41 里「没有从菜单用生产路径打开这条路」那句已更正（本文档 §1）。
- 阶段 49 遗留②（跳过桶 14 项无行为判据）、#39 构造路径那半（其余 26 个窗口）、#42（绿判据无读数）
  —— 状态不变，仍开着。
- `LangConstantUsageGuardTests` 的教训顺带记一笔：它是"名字即引用"的扫描，
  **注释里出现独立词也会算引用**。类注释里写 `<c>MenuPanel*</c>` 会让未引用数从 1267 掉到 1266
  （`MenuPanel` 恰好是同名 Lang 常量）⇒ 判据红。选择改注释而不是放宽基线。
- **#45（与大队同进程时宿主僵死）**：已用「独占进程」规避，**根因未定**（见 §6.1）。
  待查方向：并发用例对**同一条 UI 线程 / 渲染定时器**的争用 —— `UiTestHost.Pump` 内部不 marshal，
  直接调平台层的强制渲染定时器推进，而这条线程被本类连续占用同一个量级（秒级）。
  根治可试：①把一次打开的面板数降到 3~5 个，看是否还僵死（定位"量"还是"某一步"）；
  ②把 12 个面板的那段取证挪进独立 testhost。
- **#47（阶段 54 门禁复跑产出；机制已定位，2026-09-21）**：`DockViewResolutionUiTests.Match_AgreesWithBuild_ForEveryDockable`
  间歇红 —— 抛在**平台初始化**那一步（`HeadlessUnitTestSession.EnsureIsolatedApplication` →
  `AppBuilder.SetupUnsafe()` → `AvaloniaHeadlessPlatform.Initialize` → `Compositor..ctor` →
  `DefaultRenderLoop.Add` → `Dispatcher.VerifyAccess()`）。
- **更正一句错话**：此处与 `UiTestHost.cs` 的 `TestAppBuilder` 注释原先都写「`DefaultRenderLoop` 是
  **进程级静态单例、绑死在第一个会话的 UI 线程上**」。反编译 Avalonia **12.1.0** 核实**不成立**：
  `DefaultRenderLoop` **没有任何静态字段**（只有 `_items`/`_itemsCopy`/`_tick`/`_timer`/`_timerLock`/
  `_inTick`/`_hasItems`/`_running`/`_wakeupPending`），全程序集唯一的构造点是 `RenderLoop.FromTimer(IRenderTimer)`；
  它的 `Add` 是**当场读** `Dispatcher.UIThread` 再 `VerifyAccess()`，**并非构造时捕获线程**。
  ⇒ `RenderTests` 需要独立 testhost 的**真正理由**是「平台配置是进程级的、换不了第二套」，不是渲染循环被绑线程。
- **机制（逐条 IL 核实）**：

  | # | 事实 | 后果 |
  |---|---|---|
  | ① | `StartNew(Type)` 的隔离级别是 `PerTest`（枚举值 1）⇒ `EnsureIsolatedApplication` **每次派发**都执行 `AvaloniaLocator.EnterScope()` + `Dispatcher.ResetBeforeUnitTests()` + `AppBuilder.SetupUnsafe()` | 每次 `UiTestHost.Run` 都重建整个平台（含新 `Compositor`）；**每个窗口**每次派发都开一次 |
  | ② | `ResetGlobalState()` 把 `Dispatcher.s_uiThread` 与 `s_currentThreadDispatcher` 清成 `null`，派发收尾时又重置一次 | **实测：派发结束后 `s_uiThread` 仍为 null ⇒ 窗口大部分时间是开着的** |
  | ③ | `Dispatcher..ctor` 在 `s_globalLock` 内做 `if (s_uiThread == null) s_uiThread = this;` | **谁先构造谁拿走，不看线程** |
  | ④ | `CheckAccess()` = `ReferenceEquals(Thread.CurrentThread, _thread)` | 判定纯粹是线程比较 |
  | ⑤ | `DefaultRenderLoop.Add` 当场读 `Dispatcher.UIThread` | 会话线程读到的可能是别人抢下的 dispatcher |

  ⇒ 只要**任何线程**在 ①→③ 的窗口里读一次 `Dispatcher.UIThread`，它就抢下 `s_uiThread`，
  会话线程随后的 `Add` 必然抛「different thread owns it」——与实测异常消息逐字吻合。

- **复现率（门禁大队原命令）= 0/13**（本次连跑 10 次 + 之前 3 次；历史命中 1 次）
  ⇒ **低于 1/13，不足以验证任何修法**。**已排除的候选**：派发体内 `await` 续体逃逸到 worker 线程
  （`Task.Yield` / `Task.Delay` 两条探针**都回到派发线程**，见 `DispatchContinuationContractTests`）。
- **已就位的哨兵**：`DispatcherOwnershipContractTests`（3 条；比 `DockViewResolutionUiTests` 便宜得多、
  红了自带现场读数）＋ `UiTestHost` 的失败现场自证。
  ⚠ 其中一条陷阱值得单记：**`Dispatcher.UIThread` 的 getter 在 `s_uiThread == null` 时会当场构造一个
  `Dispatcher` 并抢走 `s_uiThread`** ⇒ **任何"读一下环境状态"的诊断代码自己就会制造这个缺陷**。
  所以诊断只反射读内部 `TryGetUIThread()`（不构造、不清空），并有一条判据钉住（读数里出现
  `checkAccessOnCallingThread=True` 即判红）。
- **遗留**：抢 `s_uiThread` 的「先到者」仍未确证。候选修法（`PerTest` → `PerAssembly` 隔离，
  或 `[ModuleInitializer]` 预热会话）**都必须先有可复现信号才动手** —— 反向同理：
  「连续多次不红」同样不构成修好的证据。

### #47 补记（2026-09-21，阶段 56）：**信号造出来了，修法已验证但未落地**

上面那套机制此前**只是 IL 推断**（0/13 从未复现）。用一个临时探针把它变成实测：
开一条**非会话线程高频读 `Dispatcher.UIThread`**，同时主线程连跑派发，**墙钟固定 5s**，
并带正控（派发体内计数器 `bodies`）：

| 隔离级别 | 偷手读数 | 派发次数 | **跑到的派发体** | 失败 |
|---|---|---|---|---|
| **PerTest**（现状） | 986,175,686 | 114,367 | **221** | **114,146 = 99.81%** |
| **PerAssembly** | 560,548,259 | 2,000,000（撞上限） | **2,000,000** | **0** |

- **机制实测成立**：失败现场逐字是 `callingThread=5, sessionThread=13,
  ownerThread=19（**非会话线程＝偷手**）` —— 窗外那一次读确实把 `s_uiThread` 抢走，
  会话线程随后的 `DefaultRenderLoop.Add` 必然抛 `different thread owns it`。
- `bodies=221 = 114367 − 114146` ⇒ **失败的派发死在平台初始化、根本没进派发体**；
  也解释了「11.4 万次派发只花 5 秒」（失败很便宜，别读成"派发很快"）。
- ⇒ **#47 的「偶发」不是窗口窄，而是真实套件里几乎没有线程在派发之外碰 Avalonia。**
  **⚠ 这半句已被阶段 60 推翻（2026-09-21，见「#47 补记之四」）**：它当时是个**推断**，没做普查。
  阶段 60 把框架侧取 Dispatcher 的调用点普查了一遍（61 处）再与仓库求交集，抓到**一条确定会犯案的** ——
  `UiTestHost.Pump` 在派发之外会把 `s_uiThread` 抢给调用线程（实测 `Pump 前归属=null / Pump 后归属=13`）。
  ⇒ 「偶发」的真实原因不是"没人碰"，而是**"碰了，但要在窗口最窄的那一小段里撞上"**
  （阶段 59 的闸门又把"并发派发"这条堵掉了）。原句留着作为更正痕迹。
- **修法够得着**：`StartNew(Type, AvaloniaTestIsolationLevel)` / `AvaloniaTestIsolationAttribute`
  / `GetOrStartForAssembly(Assembly)` 都在（反射转储）。换成 `PerAssembly` 后
  `App.UiTests` **326 项 324 绿**，两条红里一条是本判据断在实现细节（已改成隔离级别无关的
  偷手报警器），另一条 `CanvasRenderBaselineTests` 的分配预算**系统性上抬且解释不清**
  （`Slider 4221>4000`、`TabView 5757>4000`、`Tank 13067>12000`，而它有 50 轮 warmup、
  且在同一个派发内取差值）⇒ **不调基线、暂不切换隔离级别**。
- **判据改成点名偷手**：`CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`
  （持有者必须是 `null` 或会话线程 —— 隔离级别无关，且是偷手报警器）＋ 诊断新增
  `sessionThread=` 与 `ownerThread=（非会话线程＝偷手）` ＋ 只读读数口
  `AmbientUIThreadOwnerId()`。**负控 1/1**：人为偷手 `thiefThread=19` 被如实读出。

### #47 补记之二（2026-09-21，阶段 57）：**那条「解释不清的上抬」查清了 —— 是框架 `DrawText` 的环境相关成本，于是定论「不落地」**

阶段 56 拦住落地的最后一条是「预算判据系统性上抬且解释不清」。本阶段把它查到底，
**只做判别实验、不动判据阈值、不动隔离级别**：

| 假设 | 判别实验 | 结果 |
|---|---|---|
| 隔离级别本身 | `PerAssembly` 下**只**跑该测试类 | **否**：与 `PerTest` 单独跑**逐字节相同**（2,804 / 3,280 / 3,004 / 3,635） |
| 共享缓存被污染 | 先画 600 条互异文本再测 | **否**：读数**一格未动**；`invalΔ` 全程 0 | 
| 集合并行 | `-p:ParallelizeTestCollections=false` | **否**：只差约 6%（噪声内） |
| 测试平台字体未兜底 | 加生产入口那条 `FontManagerOptions` | **部分（约三成）**：越线 4 类 → 1 类，单类基线不动 |

同代码定点对照（`N=2000`，同一段 `Baseline_UnitCost_OfDrawingContextOps`）：

| 环境 | `new FormattedText` | `DrawText`（2 字） | `DrawText`（10 字） |
|---|---|---|---|
| 单类（`PerTest` 或 `PerAssembly`） | 384 | **2,604** | **5,932** |
| 整队同进程 | 384 | **3,465~3,856** | **11,146** |

其余绘图操作（`FillRectangle`/`DrawRectangle`/`DrawLine`/`DrawGeometry`/`Push*`）**四个环境全部为 0**；
25 类型整表里**凡不画文本的一格不动、凡画文本的一律上抬**。
⇒ 上抬是**框架字形/文本布局构建**那一段的环境相关成本（具体字段未定位，下一步逐条 IL 核实）。

⇒ **定论：不落地 `PerAssembly`。** 只要框架那部分能翻倍，同一个提交就会随运行方式绿/红 ——
放宽容差＝拿一个未解释的读数换掉一个能抓「每帧新建画刷」的判据，收紧＝整队必红。
`PerTest` 之所以平时看不见，是因为它**每次派发重建平台**（55.2 那条链）顺带清掉了抬升态。
详见 `project-status.md` 阶段 57。

### #47 补记之三（2026-09-21，阶段 58）：**抬升定位到底 —— 「不同字体族名个数」的确定性阶梯；补记之二的末句由推断变成实测**

补记之二末句写的是「`PerTest` 每次派发重建平台**顺带清掉了抬升态**」—— 当时那是**推断**。
本阶段把它做成实测，并顺带把「抬升」本身从一个说不清的「翻倍」变成一条**可写下来的阶梯**。

**① 抬升不是翻倍，是阶梯**（全新进程，`dc.DrawText(ft, p)`，2 字）：

| 事件 | 读数 |
|---|---|
| 空白连测 6 次（中间什么都不做，连字体都不读） | **2,604** 恒（第 1 次的 2,755 是进程内首测热身伪影，此后取两次较小值排掉） |
| 解析 `Typeface.Default` **自身** | 2,604 |
| 只 `new` 6 个不同 `FontFamily`、**一次都不解析** | 2,604 |
| 解析 1 / 2 / 3 / 4 / 5 / 6 / 7 个**没见过**的族名 | 3,020 / 3,436 / 3,852 / 4,268 / 4,684 / 5,100 / **5,516** |
| **负控**：同一个族名**重复解析 ×6** | **5,516 纹丝不动** |
| 解析**已在表里**的族（逐个，共 9 个） | 5,516 |
| **画**那段文本本身 | 2,812 |

⇒ 规律：**`2,604 + 416 × N`**，`N` = **本平台实例内解析过的、此前没见过的族名个数**。
负控把「解析**次数**」与「**不同**族名**个数**」明确分开；
「只构造族对象不解析也不动」则说明触发点是**解析**。

**② 根因在框架，已 IL 核实**（`Avalonia.Headless.dll` 的 `HeadlessFontManagerStub`）：
`TryCreateGlyphTypeface(familyName, …)` **完全不看 `familyName`**，一律加载
`resm:Avalonia.Headless.BareMinimum.ttf` 并返回 `true` ⇒ 「未命中」与「命中」的**返回码一样**，
只有留在管理器里的名字不同（`TryGetFamilyTypefaces` 则恒返回 `false`）。
实测 `FontManager.Current.SystemFonts` 会随请求**变长**（3 → 9 条，内容正是被请求过的那些名字
+ `BareMinimum` / `Default`）。副作用：测试里所有文本都按 **4 字形 `BareMinimum`** 画
（`'标'`、`'A'` 都是 `.notdef`）。

**③ 于是阶段 56 的间歇性闭合**：A/B 两份**完全相同**的实验跑在同一个类里 ——
A 出口 **5,516**、**B 入口 2,604** ⇒ `PerTest` **每次派发**重建平台、**字体表随之清零**；
`PerAssembly` 平台只建一次、族名**跨用例累计** ⇒ 后面用例的起点取决于前面跑过什么。
这也解释了补记之二第一行那个「`PerAssembly` 单独跑与 `PerTest` 逐字节相同」——
那一次**没有任何别的用例贡献新族名**。

**④ 落地（不放宽预算、不改隔离级别）**：`CanvasRenderBaselineTests` 新增
`Precondition_PerDispatchPlatformReset_KeepsTextBudgetsComparable` ——
一次用例三次派发、与执行顺序无关：①量干净起点 ②**正控**：解析 4 个全新族名后断言成本确实被抬高
（≥100 B；机制若变，后半段就不说明任何问题） ③断言下一次派发**回到干净起点**。
断言的是**关系**而不是 `416` 这个常数（后者是框架实现细节）。**负控已实测**：切成
`PerAssembly` ⇒ 红在③（起点停在 3,938 = 2,604 + 416×4）。

**仍未闭环**：`416` 具体由哪个字段产生（哪张 `Dictionary`/`List` 每次绘制被重放）**未定位**；
补记之二里那个 **3,465** 对不上任何整数档（与 3,436 差 29），可能另叠了贡献，
而本轮**没有复现到那个态**。详见 `project-status.md` 阶段 58。

### #47 补记之四（2026-09-21，阶段 60）：**偷手抓到了 —— `UiTestHost.Pump` 在派发之外「按调用线程取 Dispatcher」**

阶段 59 把「并发派发」这条堵掉之后，59.7 留的下一步是**「窗口期里还有谁在派发之外碰 Avalonia」**。
本阶段按这条线走，而且先做**普查**再动手，不猜"哪行看起来可疑"：

在 `Avalonia.Base` + `Avalonia.Headless` 的 IL 里搜 `Dispatcher::get_UIThread` 与
`get_CurrentDispatcher`（这两个 getter 在 `s_uiThread == null` 时**当场构造**一个 Dispatcher 并抢走它）
—— **61 个调用点，落在 50 个「文件 × 方法 × 取法」组合里**。与本仓库相关的：

| 调用点 | 取法 | 与仓库的关系 |
|---|---|---|
| `AvaloniaHeadlessPlatform.ForceRenderTimerTick` | `get_CurrentDispatcher` | **`UiTestHost.Pump` 调的就是它** ← 本次抓到的 |
| `DispatcherTimer` 的**三个**构造函数 | `get_CurrentDispatcher` | `HeadlessRenderTimer.StartCore` 用**单参**构造建它 |
| `DefaultRenderLoop.Add` / `Remove` | `get_UIThread` | #47 的**受害点**（阶段 56 已核实） |
| `HeadlessWindowImpl.Show` / `Hide` / `Activate` / `Resize`、`LayoutManager.ExecuteLayoutPass` 等 3 处、`ValueStore` / 绑定观察者 6 处 | `get_UIThread` | 仓库里都只在派发体**内部**出现 ⇒ 安全 |
| `HeadlessUnitTestSession.<DispatchCore>b0` | `get_UIThread` | **正常路径**：会话线程在派发体里自己取 |

**实测读数**（临时探针，读完即删）：

```
[探针读数] 调用线程=13 / 会话线程=7 / Pump 前归属=null / Pump 后归属=13
```

⇒ 在派发之外调**一次** `Pump(1)`，`s_uiThread` 的归属就从 `null` 变成**调用线程**。
这是**确定**的，不是偶发 —— 而 `Pump` 原先**不在派发闸门里**，仓库里几十处又在 `Run` 返回**之后**才调它。
它若落在在飞派发的窗口里，会话线程随后的 `DefaultRenderLoop.Add → VerifyAccess()` 就抛
"different thread owns it"，也就是 #47 那条"偶发红、抛点在平台初始化"。

**修法**：`Pump` 纳入同一把闸门；**会话线程上旁路**（绝大多数 `Pump` 在派发体**里面**，
那时闸门被派发的调用者持有，去 `Wait` 必然自锁；而会话线程"按调用线程取"取到的正好是它自己，
不但无害还是想要的状态）。判据 `Pump_NeverRunsWhileADispatchIsInFlight` **自己制造条件**
（一个体内睡 400 ms 的派发 + 本线程连打 12 次 `Pump`），**负控实测 12/12 转红**
（现场 `dispatch#1, sessionThread=17, callingThread=20`）。

**代价实测为零**：整个 `App.UiTests` **331/331 绿、22 秒**（加闸前 36 次的基线是 20~23 秒）。

**#47 仍未复现**：本阶段只是**又排掉一条候选**，没有证到"红不再出现"——
照 59.6 的纪律：不赌复现，靠把候选一条条排掉。

## 9 顺着 #43 挖出来的更大一层：**事件双路消费**（登记表 #46）

### 9.1 现象

双接线**已经删掉**之后，点一次 `BtnAiTemplate` 仍然弹 **2 个** `AiTemplateDialog`（判据读数 `+2`）。
两个来自**两个订阅者**：

```
FaceplateLibraryView.AiTemplateRequested（视图的 public event）
├─ 订阅者 A：视图构造函数里的转发器 → DockLayoutState.FaceplateAiTemplateRequested
│             └─ MainWindow 订阅总线（Wiring.DockAggregate.cs）  ⇒ 开 1 个窗
└─ 订阅者 B：MainWindow 直订视图（Wiring.Faceplate.cs）          ⇒ 再开 1 个窗
```

### 9.2 这不是"漏删"，是**有意的设计**（三处注释为证）

| 位置 | 原文 |
|---|---|
| `Wiring.Canvas.cs:135` | 「**双路订阅**：实例事件（初始实例）+ DockLayoutState 聚合事件（Dock 重建后新实例）」 |
| `Wiring.Canvas.cs:46-47` | 紧邻两行 `ToolboxPanel.ToolItemSelected += OnToolItemSelected;` / `DockLayoutState.ToolItemSelected += OnToolItemSelected;` |
| `Wiring.DockAggregate.cs:41`（原文） | 「每个聚合事件与上方对应的实例事件执行相同的处理逻辑，**通过防重入标志避免双触发**」 |

设计意图 =「两路都接，再用防重入标志压掉第二次」。这个设计**从根上就不成立**，实测有两条独立原因：

1. **标志确实存在，但位置错了。** 实测**有 4 个** `_suppress*` 标志（`MainWindow.axaml.cs:144-147`：
   `_suppressSelectionEvent` / `_suppressPropertyEvent` / `_suppressLayerEvent` / `_suppressAiIconEvent`），
   不是 1 个 —— 但它们**只在总线侧被检查**（`Wiring.DockAggregate.cs` 的聚合处理器里），
   对**实例直订那一侧无效** ⇒ 挡不住双路，最多只能压掉总线侧那一次。
   所以 `AiIconRequested` 读数一直 `+1` 不是"只有它做了标志"，而是"**总线侧那一次被压掉、只剩实例直订那一次**"。
   > ⚠ 更正：本文档阶段 53 版本此处写「只有 `AiIconRequested` 一处真做了标志」，**是错的**。
   > 是阶段 54 逐字段核对时发现的（`git log` 可查到改写前的原文）。
2. **这 4 个标志本来防的也不是双路。** 它们防的是**处理器自身引发的回环** ——
   `BindControl` → `PropertyChanged` → 再次 `SelectControl` → `LayerSelectionChanged` …
   ⇒ **删掉实例直订之后必须原样保留**，否则递归会重新打开（见 §10.2）。

### 9.3 完整清单：**28 处**（阶段 54 已全部删除）

| 面板 | 事件 | 视图侧转发 | 宿主侧直订 |
|---|---|---|---|
| Faceplate | Instantiate / CreateTemplate / EditTemplate / DeleteTemplate | `FaceplateLibraryView.axaml.cs:65-68` | `Wiring.Faceplate.cs:40/57/63/69` |
| Faceplate | AiTemplate | `…:69` | ~~`Wiring.Faceplate.cs:76`~~ **已删** |
| AiChat | Preview / Apply / TemplateApply / ScriptApply / Cancel | `AiChatPanelView.axaml.cs:77-81` | `Wiring.Faceplate.cs:111-115` |
| Assets | AssetSelected / SvgApply / FontSubset / AiIcon | `AssetManagerView.axaml.cs:56-59` | `Wiring.Canvas.cs:208/217/229/232` |
| Explorer | SelectionChanged / DeleteRequested | `ProjectExplorerView.axaml.cs:50-51` | `Wiring.Canvas.cs:279/288` |
| Navigation | NodeActivated / GraphChanged | `NavigationFlowView.axaml.cs:41-42` | `Wiring.Canvas.cs:266/276` |
| Toolbox | ToolItemSelected | `ToolboxView.axaml.cs:69` | `Wiring.Canvas.cs:46` |
| ResourceLibrary | ResourcePlaced | `ResourceLibraryView.axaml.cs:71` | `Wiring.Canvas.cs:136` |
| Canvas | ZoomLevelChanged / PreviewModeChanged / PreviewControlClicked | `DesignCanvasView.axaml.cs:121/194/169`（同一处**既 invoke 实例事件、又 raise 总线**） | `Wiring.Faceplate.cs:96`、`Wiring.Panels.cs:159/166` |
| Property | PropertyChanged / ScreenPropertyChanged | `PropertyPanelView.axaml.cs:58/65` | `Wiring.Canvas.cs:160/170` |
| Layers | SelectionChanged / LayerChanged | `LayerPanelView.axaml.cs:33/35` | `Wiring.Canvas.cs:180/196` |
| Canvas（**内层控件**） | `<CanvasPanel.Canvas>` 的 SelectionChanged / ControlPropertyChanged | —（`Canvas` 是 `CanvasPanel` 暴露的**内层控件**，订阅写在其上） | `Wiring.Canvas.cs:141/150` |

**总线侧 28 处全都有订阅** ⇒ 两路都是活的，删哪一路都不会断链。

> 上表末行是阶段 54 补上的：此前那版清单只列到「Canvas 3 个」，**漏了两处** ——
> 因为把"面板视图"当成了唯一的接收者形态，而这两处的接收者（`CanvasPanel.Canvas`）是视图**内部**的控件。
> 这不是笔误，是**口径**问题：判据（以及本清单）若只认"以 `Panel` 结尾的名字"就会漏掉内层控件。

**取证口径被放宽了四次，每次都多出一批**（值得记下来）：

| 口径 | 命中 |
|---|---|
| ① 只扫 `Btn*` + `Click`（阶段 52） | 8 处（双接线） |
| ② 放宽到"任意 `x:Name` 控件的任意事件"（阶段 53） | **12 处**（多出 4 处非按钮事件） |
| ③ 再放宽到"块内 `RaiseXxx` 调用"（不是 `+=` 形式） | 才把 `PropertyPanelView` / `LayerPanelView` 捞上来 |
| ④ 再放宽到"**宿主订阅内层控件的事件**"（阶段 54） | 阵列出 **28 处**（多出 `CanvasPanel.Canvas.*` 两处） |

### 9.4 后果分级（"要不要立刻修"的关键）

| 级别 | 事件 | 后果 |
|---|---|---|
| **写数据 ×2** | `AiChat.ApplyRequested` | `OnAiApplyRequested` 里 `_currentProject.Screens.Add(screen)` **无去重** ⇒ 同屏加入工程两次 |
| | `Faceplate.InstantiateRequested` | 双击模板一次 ⇒ 画布上出现**两个** Faceplate 实例，各进一次撤销栈 |
| | `ResourceLibrary.ResourcePlaced` | 双击资源 ⇒ 放**两个** |
| | `Toolbox.ToolItemSelected` | 点一次工具箱项 ⇒ 插入走两遍 |
| **开窗 ×2** | `Faceplate.EditTemplateRequested` | 选中模板后点"编辑" ⇒ 开**两个**模板编辑器 |
| | `Assets.FontSubsetRequested` | 双击字体资源 ⇒ 两个子集化对话框 |
| 幂等（当前无可见症状） | `Explorer.SelectionChanged`、`NavNodeActivated`、`NavGraphChanged`、`AssetSelected`、`LayerSelectionChanged`、`ZoomLevelChanged`、`PreviewModeChanged`、`CancelRequested`… | 重复赋值 / 重复 `SwitchToScreen` / 重复 `SetUnsavedChanges(true)` |

⚠ 末行**不代表它们没问题**：幂等只是**当前实现恰好如此**，一旦将来带上计数或日志就会暴露。

### 9.5 两条处置路线（**阶段 54 已拍板 A**）

| | A. 删宿主直订（保留总线这一路） | B. 保留双路，给每处补防重入标志 |
|---|---|---|
| 改动量 | 删 **28** 处（其中 1 处需先补回漂移，见 §9.6） | 加 ~28 个标志字段 + 28 处 try/finally |
| 依据 | 视图转发是**每个实例构造时**建立的 ⇒ Dock 重建后自愈；直订订的是**构造 MainWindow 那一刻**的实例 ⇒ 重建即失效（注释里"确保 Dock 重建后仍能到达 MainWindow"要防的正是这个） | 与该文件现有注释的设计意图一致 |
| 风险 | 推翻一个**有意**的架构选择 | 标志是**状态**：`async` handler 跨 `await` 持有标志 ⇒ 会压掉**合法**的第二次触发；且 28 个标志必然再次不一致（实测现在就 4 个做了，还只做在总线侧一半路上） |
| 判据 | 可加静态守卫（"已转发到总线的事件不得再被直订"）⇒ 零基线 | 静态判不了"标志写得对不对"，只能逐处行为测 |

**倾向 A**（唯一实现、无标志维护），但这是架构决策 ⇒ 阶段 53 只做取证，未擅自全改。

> **阶段 54 拍板：走 A。** 实施记录见 **§10**。

### 9.6 顺带抓到的**副本漂移**（已修）

`ProjectExplorerPanel.SelectionChanged`（实例直订那一份）有 `SetTransformButtonsVisible(control != null)`，
而总线那一份**没有** —— 同一逻辑的两份副本**已经开始走样**。删直订前必须先补齐（已补，提交 `9216509`）。

这条是「**唯一实现**」原则的实证：**两份副本不会一直等价，它们会各自演化** ——
现在行为还"对"，只因为实例直订那份还在兜着。

## 10 阶段 54 处置（路线 A 实施）— 2026-09-21

### 10.1 口径再放宽一次：26 → 28

删之前最后一轮全仓枚举，把三处此前没算进来的捞了出来（口径见 §9.3 表）：

| 项 | 命中 |
|---|---|
| 阶段 53 登记 | 27 处（其中 `AiTemplate` 已修 ⇒ 待处置 26） |
| 阶段 54 实测 | **28 处**（待处置 27 + 已删 1） |
| 多出来的两处 | `Wiring.Canvas.cs:141` `CanvasPanel.Canvas.SelectionChanged` / `:150` `CanvasPanel.Canvas.ControlPropertyChanged` |

多出的原因：**接收者形态**比"面板视图"更宽 —— `CanvasPanel.Canvas` 是视图暴露的**内层控件**，
宿主订阅写在其上。这一形态此前两条判据都没覆盖（既不是 `Btn*`，也不是视图成员）。

### 10.2 改了什么

| 文件 | 改动 |
|---|---|
| `Wiring.Faceplate.cs` | 删 10 处：Faceplate 4 + `CanvasPanel.ZoomLevelChanged` + AiChat 5。**保留** `AiChatPanel.ConfigChanged`（唯一单路，该视图无转发） |
| `Wiring.Canvas.cs` | 删 16 处：Toolbox 1 / ResourceLibrary 1 / Canvas 内层 2 / Property 2 / Layers 2 / Assets 4 / Navigation 2 / Explorer 2 |
| `Wiring.Panels.cs` | 删 2 处：`PreviewModeChanged` / `PreviewControlClicked`（后者**非幂等**：`ExecuteAction(control, "OnClick")` 跑两遍 ⇒ 切屏/写变量/记日志执行两次） |
| `MainWindow.axaml.cs` | 删两个已成死代码的实例侧包装（`OnCanvasSelectionChanged` / `OnPropertyPanelChanged`）；给 4 个由总线转发进的方法补 `<remarks>` |
| 全部删除处 | 均加注释写明「视图构造已转发到总线 / 为何保留 / 读数变化」 |

**4 个 `_suppress*` 标志原样保留**，理由见 §9.2 第 2 条：它们防的是**处理器自身引发的回环**
（`BindControl → PropertyChanged`、`SelectControl → LayerSelectionChanged`），
与"双路"是两件事 —— 删直订**不会**让它们变多余，删掉反而会把递归重新打开。

⚠ 一处必须留的**唯一单路**：`AiChatPanel.ConfigChanged`。该视图**没有**向总线转发，
若按"视图事件一律不直订"的机械规则删掉，这条链路会**静默断掉**（判据对它无感知）。

### 10.3 新静态守卫：`scripts/check-dock-bus-only-subscription.py`

判据形状 = 「宿主消费面板视图事件**只能经 `DockLayoutState`**」。与阶段 53 的
`check-xaml-duplicate-wiring.py` 是**同一族缺陷的两种形态**（那个守 XAML 声明面，这个守视图事件面）。

| 设计点 | 做法 | 为什么 |
|---|---|---|
| 接收者判定 | **由声明推导**（`XxxView` 类型的成员），不猜名字后缀 | 首版用"以 `Panel` 结尾"⇒ **实测误报 4 处**（`NumLeftPanel`/`NumRightPanel`/`NumBottomPanel` 是 `NumericUpDown`，`ChartPanel` 是 `TrendChartPanel`） |
| 噪声剥离 | `strip_noise()` 把注释**与字符串/字符字面量**抹成等长空白 | 判据不锚行首（行内接线也是接线）⇒ `var s = "Panel.X += Y";` 会误报，实测过 |
| 白名单 | `ALLOW = {("AiChatPanel","ConfigChanged"): 理由}`，脚本连同理由打印 | 允许**有理由的**例外，但必须写下来 |
| fail-closed | ① `command -v python3`；② 推导出的视图成员数 < `MIN_VIEW_MEMBERS`(8) 直接失败 | 声明解析一失效，判据会退化成**恒绿**的假门禁 |
| 负控 | `--selftest` **8 档**：3 红（面板直订 / 三点链 / `GetActiveView` 上直订）+ 5 绿（只订总线 / 视图自己的转发 / 白名单 / 名字带 `Panel` 的非视图 / 注释与字符串里的反例） | 只做"该红 ⇒ 1 条"是**半个负控** |

实测：`--selftest` **8/8**；真实扫描 **0 违规**（121 个 `.cs`、识别 24 个视图成员、白名单命中 1 条）。

接进 `scripts/ci-gate.sh` **`[1e]`** 段（塞在 `[1d]` 之后、**不新增 banner** ⇒ 门禁**步数仍 7**）。

### 10.4 顺带闭环 #44，以及它的残留

`BtnAutoFix` 的一键修复崩溃**随实例直订删除而消失**：null 仍会被发出（`_currentControl!` 一个字没动），
但唯一剩下的消费方（`PropertyPanelView` 构造里的转发器）**带 null 守卫**。

- 行为判据里 `BtnAutoFix` 从 `Broken` 桶**移入** `Silent` 桶（判据自带的提示正好生效），
  `Why` 改写成「**不许把实例直订加回来**」—— 谁加回去，这行立刻变红。
- **残留语义**（未闭环）：无选中控件时现在是**静默无操作**，与 `ShowAiSuggestions(CurrentScreen)` 的原意
  （无选中控件 ⇒ 走**屏幕级**建议）**未对齐**。登记为独立条目。

### 10.5 验证读数

| 项 | 读数 |
|---|---|
| 提交 | `5cabdbb`，`2026-09-21T13:13:54+08:00`（9 files / **+477 −230**） |
| 门禁 | **7 步全绿 / `GATE_EXIT=0`**；日志 `.workbuddy/tmp/ci-gate-46-post.log` mtime **`13:14:47`** ⇒ **晚于**提交（时序核对通过） |
| 测试 | **3016**（8 程序集大队 **3006** + 独占两段 **8 + 2**） |
| 构建 | **0 警告 0 错误** |
| 弹窗读数 | `BtnAiTemplate` **+4 → +2 → +1**；`BtnAiIcon` **+1**（一直是 1，因为它本来就只有一半路活了） |
| `DockPanelInvocationUiTests` | **2/2 绿**（**20 静默 / 0 抛异常 / 2 弹窗**） |

**被既有判据拦下的两处**（都不是新守卫的问题，记下来备用）：

| 判据 | 症状 | 处置 |
|---|---|---|
| `SingleFileSizeGuardTests` | `MainWindow.axaml.cs` 3020 ≠ 基线 3013（新加注释顶破） | **把注释压回一行**让行数回到 3013 —— **没有**调大基线 |
| `CoreVisibleTextInventoryGuardTests` | SvgParser 通道的消费方路径随代码搬到 `Wiring.DockAggregate.cs` | 更新清单里的路径（标记 `asset.svg.parse.fail`） |

另有一处**判据自身的反模式**被发现：`BucketTable_IsSelfConsistent` 里的 `broken != 1`
把「当前有 1 个未修缺陷」写成期望 ⇒ 缺陷一修好就反过来把修好的人挡在门外。**已删该断言**，分布只作读数。

### 10.6 新开 #47（本阶段门禁复跑产出）

见 §8。一句话：`DockViewResolutionUiTests.Match_AgreesWithBuild_ForEveryDockable` 间歇红，
抛点与 #45 **同族**（都在 headless 会话 / 平台初始化这条路径上），但**不是**原先以为的「进程级静态渲染循环」—— 机制见本节上面的 #47 小节。
复现率 1 命中、按原命令连跑 3 次 **0/3** ⇒ 低概率竞态，**根因未定、暂不动**。

---

## 11 补记（2026-10-01，阶段 166）：`#81` 的假阳 —— 哨兵在**闸门之外**读数

### 11.1 那条红是怎么来的（`#81`）

阶段 164 的门禁首跑，`DispatcherOwnershipContractTests.CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`
红过一次：`ownerThread=5, sessionThread=33`；同一棵树下一跑全绿、该程序集单独连跑 4 次也全绿。

### 11.2 机制（**确定性复现**出来了，不再是假说）

`UiTestHost.Pump` 有一条**合法的**、文档里明写的路径：**在派发之外**调它时，它会按**调用线程**
取 Dispatcher（`HeadlessRenderTimer.StartCore` 用单参构造建 `DispatcherTimer`），于是
`s_uiThread` 的归属翻成那个调用线程；而**一次派发结束后 `s_uiThread` 是 `null`**
（PerTest 隔离的 scope 退出时又重置了一遍 ⇒「窗口大部分时间是开着的」）。

实测读数（临时探针，一个后台线程只做了一次合法的 `Pump(1)`）：

```
派发后 session=7；现场=dispatch#1, ..., sessionThread=7, ambientUIThread=s_uiThread=null（窗口开着）
后台线程 Pump 完成=True
Pump 之后 owner=19 session=7 ⇒ 假阳形状（owner≠session）=True
```

⇒ 旧哨兵**在闸门之外**读数，读到的是"**别的测试类合法碰平台**留下的归属"，
报出来的却是"存在派发之外的 Avalonia 访问（偷手）"。**现场没有任何非法访问。**

### 11.3 修法：把「派发 ＋ 读数」收进**同一段闸门**

`UiTestHost.ObserveAmbientOwnerOutsideDispatch()`：持闸门 → 派发两次 → 仍持闸门时读归属。
闸门把别人的派发/`Pump` 挡在外面 ⇒ 这个读数只可能看到三件事：自己的会话线程（正常）、
`null`（刚派发完，窗口开着）、或**一个绕过闸门去碰 Avalonia 的线程**（`#47` 真正要抓的偷手）。
**检测力不降，假阳没了。** 哨兵改用它；消息措辞也从「**派发之外的** Avalonia 访问」
改成「**绕过闸门的** Avalonia 访问」（前者已经不准确）。

### 11.4 判据与负控

- 判据 `CrossDispatchRead_IsNotDisturbedByOtherThreadsLegalPlatformTouches`：一个后台线程**合法地**
  反复碰平台（派发之外 `Pump(1)`，闸门由它自己取、**不写** `_sessionThreadId`），
  同时对本线程的观察入口连读 **40** 次 —— 必须**一次都不**报"归属 ≠ 会话线程"。
- ⚠ 判据**只断言"修法那一半"**（无条件、确定性），**不**断言"假阳形状一定出现"：
  那个形状依赖"**窗口开着**"（派发结束后 `s_uiThread == null`），而它在完整套件里**不稳定**
  （同一进程里有时读到归属已是本线程）—— 第一版判据把"形状出现"也写成断言，**偶发红**
  （`owner=48, session=48`，因为那时后台 Pump 没能抢到已被本线程持有的 `s_uiThread`）。
  踩过的第二个坑：为了强制开窗去反射调 `Dispatcher.ResetBeforeUnitTests()`（**进程级**重置），
  在跨类并行下等于给别人添乱 —— **删掉**，不在判据里做进程级操作。
- 负控 2 支（`.workbuddy/tmp/s166-nc.py`，还原 SHA256 逐字节相同）：
  nc1 观察入口在**闸门之外**先读（= 旧哨兵形态）· nc2 派发＋读数都做了、但**没持外层闸门** ⇒
  两支都红在"假阳没修掉"。

### 11.5 顺带那条窄窗（登记 `#82`）：**复核后前提不成立**（阶段 167）

`Pump` 判断「是不是已经在派发体内」用的是 `Environment.CurrentManagedThreadId == SessionThreadId`，
而 `SessionThreadId` 是「**最后一次派发**的线程」。当时（阶段 166）怀疑：别的线程正处于
`EnsureIsolatedApplication()` 的「reset → 写 `_sessionThreadId`」之间时，本线程若**恰好等于上一次派发的线程**，
就会跳过闸门去碰平台。

**阶段 167 复核：够不着**，实测读数（判据 `PumpGateSkip_IsReachableOnlyFromTheDedicatedSessionThread`）：

```
派发体线程分布：t13×11            # 从 3 个不同调用线程（t5/t19/t20）各派发若干次
调用线程分布：t5×1, t19×5, t20×5  # 派发体**全部**跑在同一个 t13 上
非派发线程的 Pump 跳过闸门次数 = 0
正控：派发体里 Pump 的跳过闸门次数 = 1（计数器不是死的）
```

⇒ **有一个专用会话线程**，`SessionThreadId` 记的就是它；**别的线程不可能等于它** ⇒
从非派发线程调 `Pump` 一律走闸门，那条窄窗**没有入口**。（先前用锤击法量过 30,486 次 `Pump` ×
1,419 次派发、`PumpUngatedEvents` 恒 0，也印证同一点。）⇒ `#82` 按「**复核后不成立（前提不成立）**」结案。

⚠ 顺带量到一件事：**那条跳过支路不是优化，是必需的** —— 派发体会在**持有闸门**时调 `Pump`，
若 `Pump` 那时去抢闸门就是抢自己握着的锁。s167 负控实测：把判定改成「恒不跳过」立刻自锁
（`派发闸门等待超时（疑似自锁）：sessionThread=13, callingThread=13`，2 分钟后才抛）。
