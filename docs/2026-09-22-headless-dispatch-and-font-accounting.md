# headless 会话模型 / 派发闸门 / 字体分配记账

> 2026-09-22 从 `.workbuddy/memory/MEMORY.md` §4.2 **析出**（那份长期笔记要控字节，
> 细节移到这里）。全部是**实测**读数，不是推测；保留「现象 → 正解」的对照，
> 因为现象往往看起来像别的问题（内存不足、用例变慢、框架 bug）。
>
> 涉及阶段：56 / 57 / 58 / 59 / 60（2026-09-21~22）。未闭环项见
> `docs/2026-09-17-open-items-register.md` #45 / #47 / #47b。

## 1 会话模型：`PerTest` 隔离与 `s_uiThread`

- Avalonia 12.1.0 headless 是 **`PerTest` 隔离** ⇒ 每次 `UiTestHost.Run` 都跑
  `ResetGlobalState` + `SetupUnsafe`（**重建平台、新 Compositor**）。
- `s_uiThread` 的赋值是「Reset 后**第一个**构造 Dispatcher 的线程抢下」
  （`if (s_uiThread == null) s_uiThread = this;`，**不看线程身份**）。
  ⇒ **任何线程只要在窗口里读一次 `Dispatcher.UIThread` 就把它抢走**，
  会话线程随后的 `DefaultRenderLoop.Add → VerifyAccess()` 抛
  「different thread owns it」。这是 **#47** 的机制。
- 🩸 **`Dispatcher.UIThread` 的 getter 在 `s_uiThread == null` 时当场构造一个
  Dispatcher 并把它抢走** ⇒ **诊断/探针代码自己读一次就制造缺陷**。
  探针只能反射读内部 `TryGetUIThread()`。
- 判「某线程在不在会话线程上」用 `SessionThreadId`（普通 int 字段），
  **不能**用 `Dispatcher.UIThread.CheckAccess()` —— 读那个 getter 本身就是犯案动作。

## 2 并发派发：确定存在的竞态前提

- `DispatchCore` 在**调用线程**上直接跑 `EnterScope` + `ResetBeforeUnitTests` +
  `SetupUnsafe`，这三者都是**全进程级、无锁、无重入判定**；而 xunit 默认集合并行。
  ⇒ 实测 `maxInFlight = 20`、195 次派发里 149 次重叠。
- **已落地派发闸门**：`UiTestHost.Run/RunAsync` 外面包 `SemaphoreSlim`，
  带 2 min 超时（⇒ 自锁从「挂死」变「点名失败」）；同步路径用 `[ThreadStatic]`
  深度支持重入。效果：并发度 **20 → 1**，**代价为零**（程序集 19~22 s → 18~21 s）。
- 守卫：`HeadlessDispatchContractTests.ConcurrentDispatchRequests_AreNeverInFlightTogether`
  （自己起线程造并发条件；负控＝摘掉闸门 ⇒ 红）。
- **诊断默认关闭**：设 `HMI_DIAG_OUT=<file>` 时进程退出写
  `maxInFlight` / `overlapEvents` / `maxGateWaitMs` / `maxBodyMs`。
  后两个读数能区分「在闸门外排队」与「派发体自己跑」，配 `--logger trx`
  可指名到具体用例。**不设该环境变量时派发路径逐指令等价**（零副作用）。

## 3 挂死与「一次 141 秒」

- **量「前提」比复现「结果」划算**：偶发挂死只给一个比特 + 90 s 超时；
  `maxInFlight` 是确定性读数，直接量前提。
- **#45 的挂死本阶段 0/5 复现不出**（阶段 52 是 4/4）⇒ 根因**仍未确证**，
  「独占进程」的规避**保留**。
- ⚠ 提交后门禁里出现过**一次 UI 段 141 秒**：判据是**同一次 `[5]` 里其余 7 个
  程序集逐项逐毫秒相同** ⇒ 不是整体变慢，只可能是该进程内部。
  另已排除：内存（53.4 GB 可用）/ 窗口内有别的批次 / 定时器线程泄漏。
  成因**未证**；同一条命令补跑 **0/42**（合 51 份历史门禁 = 93 次观测 1 次，p̂≈1.1%）。
- 🩸 **墙钟必须带负载读数**，且要用**跑之前**那个 `/proc/loadavg`（跑完必然被自己顶高）：
  同一批连跑负载 `1.89 → 5.09`，同一个程序集就从 27 s 飘到 36 s
  ⇒ **没有负载读数的墙钟只能同批内部比较**。
- 🩸 **墙钟差异先按各步骤读数归因，再怀疑争用**：门禁 73 s vs 51 s 的真因是
  `[3]` 构建 **28.49 s vs 10.8 s**（真重建 vs 增量空构建），**不是**当时写的
  「并行编辑文档抢 CPU」（已在 59.8 就地更正）。

## 4 #47 的两条候选（均已排掉或已收口）

### 4.1 `UiTestHost.Pump` 在派发之外按调用线程取 Dispatcher

- `ForceRenderTimerTick` 的支路里有 `Dispatcher::get_CurrentDispatcher()`；
  `DispatcherTimer` 的**三个构造**也都读它（`HeadlessRenderTimer.StartCore`
  用**单参**构造建）⇒ 在派发之外调一次 `Pump(1)`，归属从 `null` 变成调用线程。
- 探针读数：`调用线程=13 / 会话线程=7 / before=null / after=13`。
- **正解**：把 `Pump` 纳入派发闸门；但**会话线程上必须旁路**（一律纳闸必自锁）。
- 判据 `Pump_NeverRunsWhileADispatchIsInFlight`（自造并发条件；
  负控＝摘闸门 ⇒ 12/12 红）。代价为零（331/331 绿、22 s）。

### 4.2 人为偷手的定量实验（阶段 56）

- 非会话线程高频读 `Dispatcher.UIThread` ⇒ **`PerTest` 下 99.81% 的派发死在
  平台初始化**（114146/114367；现场 `sessionThread=13, ownerThread=19`）；
  **`PerAssembly` 下 0/2,000,000**。
- ⇒ 窗口确由「每次派发重置」造成。**修法不落地（阶段 57 定论）**。
- 判据 `CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`
  （持有者只能是 `null` 或会话线程）＋ 读数口 `AmbientUIThreadOwnerId()`。
  **别断 `Assert.NotSame` 那一类实现细节**；比率先把机会固定住
  （按次数比会把「没机会输」读成「赢了」）。
- 结论：#47 的偶发红**仍未复现**，上述工作只排掉了候选、收紧了前提。

## 5 方法：IL 普查再求交集

🩸 想收「谁在派发之外碰 Avalonia」这个面，**先做 IL 普查，再与仓库入口求交**：

1. 全框架搜 `get_UIThread` / `get_CurrentDispatcher` ⇒ Avalonia.Base + Headless
   共 **61 个调用点 / 50 个「文件×方法×取法」组合**；
2. 再与「本仓库在派发之外的入口」求交集。

⇒ 候选从「整个框架」缩到**一条**。阶段 56 那句「真实套件里几乎没有线程在
派发之外碰 Avalonia」是**没普查过的推断，已被证伪**（确实在碰，
只是碰撞只落在窗口最窄的一段）。

## 6 渲染循环与定时器

- headless 走的是 **`HeadlessRenderTimer`**（挂在 Dispatcher 上的 `DispatcherTimer`），
  **不是** `SleepLoopRenderTimer` 那条独立后台线程 ——
  `AvaloniaHeadlessPlatformOptions::.ctor` 里 `ShouldRenderOnUIThread` **默认 true**
  （`ldc.i4.1`），全仓两处构造（`UiTestHost.cs:491` / `RenderTestHost.cs:48`）
  都没改它。⇒ **别再怀疑「渲染定时器按平台泄漏线程」**。
- `DefaultRenderLoop` **不是**静态单例（反编译核实：无静态字段、唯一构造点
  `RenderLoop.FromTimer`、`Add` 当场读 `Dispatcher.UIThread`）。
  「渲染循环绑死在第一个会话的 UI 线程上」是**错话**，已于 2026-09-21 全库更正（9 处）。
- `RenderTests` 单开一个程序集的理由是**平台配置是进程级的、换不了第二套**，
  与上面的渲染循环无关。

## 7 `DrawText` 的分配记账（基准判据的地基）

🩸 **框架 `DrawText` 的单次分配是「环境相关」的 ⇒ 基准判据别用绝对字节数跨配置。**

- 公式（2026-09-21 实测，阶段 58 定位）：

  ```
  dc.DrawText(ft, p) 的分配 = 2,604 + 416 × N   （字节）
  N = 本平台实例内解析过的、此前没见过的字体族名个数
  ```

- 机制：headless 的 `HeadlessFontManagerStub.TryCreateGlyphTypeface`
  **完全不看 `familyName`**、一律返回同一份 4 字形 `BareMinimum.ttf`，
  **却返回 `true`** ⇒ 每个没见过的族名都算「成功命中」、登记进管理器
  （`FontManager.Current.SystemFonts` 会随之变长）。
- ⇒ **`PerTest` 每次派发重建平台＝顺带清零字体表**；
  `PerAssembly` 下族名**跨用例累计** ⇒ 起点随执行序漂（阶段 56 那三条越线的来源）。
- 已否证的解释：隔离级别（`PerAssembly` 单独跑与 `PerTest` 逐字节同）、
  缓存污染（预灌 600 条互异文本读数不动、`invalΔ` 恒 0）、集合并行（关掉只差 6%）。
- 🩸 **判据别抄常数**：`afterReset <= clean` 这种绝对比较被 42 字节漂移踩红
  （干净值单独跑 2,604 / 整队 2,232）；要写成**只用本次运行自己两个数**的相对形式。
- 守卫：`CanvasRenderBaselineTests.Precondition_PerDispatchPlatformReset_KeepsTextBudgetsComparable`
  （一次用例三次派发，正控 + 负控已实测）。
- **决定：`PerTest` 保持不动、`CanvasRenderBaselineTests` 预算一个字不改**
  （放宽容差＝拿未解释读数换判据）。
- **副作用（写测试时要记住）**：测试里所有文本都是 **4 字形 `.notdef`**
  （`'标'` / `'A'` 都查不到）⇒ 别指望测试能验真字渲染。
- **仍未闭环**：`416` 由哪个字段产生（哪张 `Dictionary` 每次绘制被重放）**未定位**；
  阶段 57 记的 3,465 对不上任何整数档，可能另叠了贡献。
  「测试平台未兜底 `FontManagerOptions.DefaultFamilyName`」**明确定为不落地**
  （桩忽略族名，改了既渲染不出真字、也阻止不了阶梯增长）。

## 8 输入 / 事件：写「行为判据」时的三条实测事实（阶段 77）

写「编辑字段 ⇒ 界面跟着变」这类**行为**判据时踩到的三条。与派发闸门、字体记账无关，
但同属「**headless 里到底怎么驱动**」这一类，所以记在这里（阶段 77 的用例
`tests/HmiEditor.App.UiTests/ListRowInPlaceRefreshUiTests.cs` 是它们的消费者）。

### 8.1 🩸 程序化设 `TextBox.Text` **不触发** `TextChanged`

- 实测（Avalonia 12.1.0）：裸 `TextBox`，**未接树** 与 **已 `Show()` + `Pump()`** 各一组，
  各设一次 `Text` ⇒ `TextChanged` **0 次**（而 `Text` 属性本身**确实变了**）。
- ⇒ 用它驱动产品里 `TxtX.TextChanged += … → SyncXxxFromUi()` 那条链，**整链根本不会跑**。
  本阶段第一版判据正是这么写的：两条用例都停在「模型没变」，红得**与产品无关**
  （`_currentAnimation` 已装上、`_isUpdating == false` —— 两个前提都用反射验过）。
- **正解＝真实输入**：`Show()` → `UiTestHost.Pump()` → `Focus()` → `UiTestHost.Pump()` →
  `KeyTextInput("文本")` → `UiTestHost.Pump()`。仓库先例：
  `HeadlessInputAndDialogUiTests.KeyboardInput_GoesToFocusedTextBox`、`PropertyPanelUiTests`。
- **顺带更正一个常见误解**：产品里 `TxtX.Text = model.X` 这类**装载**代码本身不会引发回写 ⇒
  `_isUpdating` 那类守卫真正挡的是 `ComboBox.SelectedIndex` / `CheckBox.IsChecked`
  这些**会**程序化派发的事件（它们与 `TextBox.Text` 不同）。
- 这条事实已被一条判据钉住：`FrameworkFact_ProgrammaticTextIsSilent_TypingIsNot`
  —— 框架若改了这个行为，那两条行为判据会一起红，而这条会先告诉你是**框架**变了、不是产品坏了。

### 8.2 `KeyTextInput` 在**光标处插入**，不是追加

- 实测：`Text == "程序化"`（光标默认在 0）时 `KeyTextInput("真实输入")` ⇒ `"真实输入程序化"`。
- ⇒ 要断言**确定**文本，先清空。清空可以用程序化赋值 —— **正因为 8.1 它不触发 `TextChanged`**，
  于是不会把「装载」误当成「编辑」。

### 8.3 判据自身的坑：`SelectedIndex = 0` 可能是**空操作**

- 若构造期已经预选了第 0 行，再设一次 `SelectedIndex = 0` **不产生变更** ⇒ `SelectionChanged`
  不派发 ⇒ 视图的「当前项」字段一直是 `null` ⇒ 后续编辑**静默无效**。
- ⇒ 测多级联动列表（分组 → 配方 → 参数）时，逐级**先置 `-1` 再置 `0`**。
  症状很像产品缺陷（「编辑了没反应」），其实是判据把「选中」当成了「已选中」。
