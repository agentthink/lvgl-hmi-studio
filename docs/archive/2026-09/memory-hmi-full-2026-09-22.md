# 智数 HMI 长期笔记

> 契约 + 指针。全文快照 `docs/archive/2026-09/memory-hmi-full-*.md`；**数字以 `project-status.md`、未闭环项以 `2026-09-17-open-items-register.md` 为准**。

## 1 设备 / 构建
- `/rom` 只读 ⇒ `/usr`、`/etc` 重启即失；唯一持久分区 `/mnt/UDISK`。
- 抓帧 `dd if=/dev/fb0` 只读首缓冲 ⇒ 先看 `fb0/pan`。
- 收帧唯一实现 `CommandFrameCollector`：静默从最后一段数据起算。⇒ `…channel-frame-completion-contract.md`
- 会话超时顺序 `评估 → 打点 → 判定`（破即不触发）。⇒ `…session-timeout-and-audit.md`
- Tina：`make package/hmi-runtime/compile V=s` + `pack`；SPI NAND 须 `openixSuit`；`lv_conf.h` 以 `src-config/` 为准。
- 同一 `.axaml` 只声明一次（重复键 ⇒ UI 测试死在 `SetupUnsafe`）。⇒ `…avalonia-resource-duplicate-key.md`

## 2 生成链契约（不能回退）
- 净化唯一 `CIdentifier.cs`；`obj_*` 无屏幕前缀 ⇒ 控件 id 工程内唯一；资源符号只在 `Export` 入口调一次。
- 发射清单唯一 `EnumerateRenderableControls` ⇒ 不遍历 `project.Screens[].Controls`；Faceplate 克隆换独立集合。
- 绑定：`BindingVisualProperty` + 发射点 `VisualBindingCodeGen`；`BgColor` 须同设 `bg_opa(COVER)`。⇒ `…expression-driven-visual.md`
- 颜色唯一 `PaletteDefaults.cs`；`apply_theme_styles()` 须在 `lv_screen_load` 后且不设 `bg_color`。
- 权限唯一 `ActionPermissionMap`/`SecurityConfigCodeGen`；判定只在生成头；门只在 `!= Designer` 发射。⇒ `…permission-model-sinking.md`
- 下沉/凭据：3 字段 → `generated_runtime.h`；编辑器凭据不下发；`hmi_login` 有表只跳屏不授权；AI enum 源 `PromptTemplateEngine`/`AiSchemaEnums`。⇒ `…runtime-*.md`
- 文案三层：Code 只做键推导/分派；实参走 `Args`；渲染唯一 `ExternalText`；`.Format` 与 XAML `{res:Loc}`（恒 0 实参）不得共用同键。⇒ `…localization-placeholder-closure.md`
- **角色名/描述＝界面文案，不是落盘数据**（阶段 66）：生成头 `HMI_SEC_ROLES_INIT` 只发 `Id/Name/Level/掩码`、`SecurityConfig` **无** `Roles` 字段 ⇒ 唯一键推导 `src/HmiEditor.App/Services/RoleTextKeys.cs`（表手写、用模型 `.Id`、表外 Id 回落 `role.Name`）；模型 `Name` 只留给生成头与调试日志。**别把描述改回模型**（那正是 #54：旧守卫按「DATA：落盘角色表输入」豁免了它）。

## 3 产物可复现 / 模拟器 / UI
- 非确定性已修：相位（FNV-1a + lowbias32 收尾）、Faceplate 克隆 id；A/B 须同提交。
- LVGL v9 角度：3 点钟 0° 顺时针。⇒ `…headless-render-and-simulator-pitfalls.md`
- Avalonia Grid 只设 `ColumnDefinitions` ⇒ `Row≥1` 全 clamp。
- Avalonia：`ItemsSource` 直接装模型对象且**无 `ItemTemplate`** ⇒ 兜底渲染 `ToString()`，实测拿到的是**类型全名**（`HmiEditor.Core.Models.User.UserRole`，不是「英文名」）⇒ 条目要建 `ComboBoxItem{Content,Tag}` 或给 `ItemTemplate`；`ComboBoxItem` **是控件**，两个下拉必须**各建一批**。
- AI 取消唯一接缝在 App 层 UI 入口；busy 时 `Cancel()` 不 `Close()`。Dock：`ViewLocator.CanResolve` 须同时是 `Match`/`Build` 的判据。

## 4 测试纪律 / 环境
- 临时目录/随机红见技能 `dotnet-test-tempdir-hygiene`；跑 `git` 的目录须在工作树外。数据根改名：读回退、写不回退。
- 跑全量必走 `ci-gate.sh`；「卡住」看日志 mtime。引用「绿」前先核时序（`stat -c %y` 对 `git log`）。
- **`HMI_READINGS_OUT` 读数文件的块顺序不稳定**（按 scope **追加**，先后由测试派发顺序决定）⇒ 两次对比**先 `sort` 再 diff**，否则会把"仅顺序不同"读成"读数变了"（阶段 65 实测：同一次提交前后两次跑，内容逐行相同、顺序不同）。
- 守卫与被守对象必须同源；**做不出负控的判据不发布**。
- **判据别量自己不该管的量**（阶段 66 发现 / 阶段 67 闭环，登记 #55）：`Phase3IntegrationTests.cs:480` 断言 `samples[^1].Values[0] > 100`，把「确实记下了 150」与「其后没有别的采样」捆在一起 —— 而后者由 `DataLogger` 自己的 1000ms 定时器决定（`Start()` 排一个「立即到期」的回调塞**全 0.0 占位采样**，`DataLogger.cs:232`），**回调派发被拖后**就红 ⇒ 门禁并行 3 次红 1 次、串行 5/5 绿、失败那次用例只跑 **22 ms**（不是用例变慢，是派发延迟）。**修法**＝断言只留「**存在**一条 > 100 的采样」（`Any(...)`）；**别用 `Stop()` 去关窗口**（已排队的回调仍会跑，`OnSampleTick` 只查 `_disposed`）。⇒ 写断言前先问「**这件事该由这条判据管吗**」。
- **间歇红/假红的复现器：把「假红探测器」与「旧写法必红」配成一对** —— 对**同一个注入**，新写法必须**绿**、旧写法必须**红**，才排除了「注入太软没打中」；只用其一，绿了也说不清是修好了还是注入没打中（#55 的 NC1/NC1b 就是这一对）。
- **`{StaticResource}` 键缺失＝静默**；赋 `TextBox.Text` 不触发 `TextChanged`；折叠 `Expander` 内控件须先 `Show()`；静态守卫须先剥注释。⇒ `…xaml-resource-key-closure.md`
- headless：主窗口与 32 窗口可构造；`ShowDialog` 不阻塞、弹窗同步进 `OwnedWindows`。⇒ `…mainwindow-construction-net.md`
- 动作面判据方法论全在技能 `static-guard-authoring`（分桶 + 双向差集；集合属性元素不进逻辑树 ⇒ 取实例走 `FindControl`；两路都接同一 handler ＝ 缺陷；有静态注册表 ⇒ 一类只许一条建窗用例；开面板判据类须独占进程 #45；绿判据无读数 ⇒ 已立**读数出口 `JudgeReadings`**（两条通道：判据类注入 `ITestOutputHelper` 按需看 + `HMI_READINGS_OUT` 追加落盘、门禁 minimal 下也拿得到；**默认零副作用、刻意不做成断言**；#42 已闭环，静态接线守卫按目录枚举 `*InvocationUiTests.cs`））。落地 ⇒ `…menu-`、`…dialog-inner-`、`…mainwindow-toolbar-`、`…dockpanel-invocation-net.md`（四层 170 按钮；各缺陷号状态以登记表为准）
- **headless 会话（Avalonia 12.1.0）＝ `PerTest` 隔离** ⇒ 每次 `UiTestHost.Run` 都 `ResetGlobalState` + `SetupUnsafe`（重建平台、新 Compositor）。`s_uiThread` 是「Reset 后**第一个**构造 Dispatcher 的线程」抢下的（`if (s_uiThread == null) s_uiThread = this;`，不看线程）⇒ **任何线程在窗口里读一次 `Dispatcher.UIThread` 就抢走它** ⇒ 会话线程随后的 `DefaultRenderLoop.Add → VerifyAccess()` 抛「different thread owns it」（#47）。
  - 🩸 **并发派发是确定存在的竞态前提**（2026-09-21 阶段 59）：`DispatchCore` 在**调用线程**上直接跑 `EnterScope` + `ResetBeforeUnitTests` + `SetupUnsafe`（全进程级、**无锁无重入判定**），而 xunit 默认集合并行 ⇒ 实测 `maxInFlight=20`、195 次派发里 149 次重叠。**已落地派发闸门**（`UiTestHost.Run/RunAsync` 包 `SemaphoreSlim`，带 2 min 超时 ⇒ 自锁变点名失败；同步路径 `[ThreadStatic]` 深度支持重入）⇒ 并发度 **20 → 1**、**代价为零**（程序集 19~22 s → 18~21 s）。守卫 `HeadlessDispatchContractTests.ConcurrentDispatchRequests_AreNeverInFlightTogether`（自己起线程造并发；负控：摘掉闸门 ⇒ 红）。诊断默认关闭：`HMI_DIAG_OUT=<file>` ⇒ 进程退出写 `maxInFlight`/`overlapEvents`。
  - **量"前提"比复现"结果"划算**：偶发挂死只给一个比特 + 90 s 超时；`maxInFlight` 是确定性读数。**#45 的挂死本轮 0/5 复现不出**（阶段 52 是 4/4）⇒ 独占进程的规避**保留**，#45 根因**仍未确证**。
  - ⚠ **提交后门禁里出现过一次 UI 段 141 秒**（2026-09-21 阶段 59 补记）：判据是**同一次 `[5]` 里其余 7 个程序集逐项逐毫秒相同** ⇒ 不是整体变慢、只可能是该进程内部（另排除内存 53.4 GB 可用 / 窗口内有别的批次 / 定时器线程泄漏）。成因**未证**；同一条命令补跑 **0/42**（合 51 份历史门禁 = 93 次观测 1 次，p̂≈1.1%）。诊断已从"一个总时长"扩成**能区分的两段**：`maxGateWaitMs`（在闸门外排队）与 `maxBodyMs`（派发体自己跑），都带"第几次派发"，配 `--logger trx` 可指名到用例；**默认关闭**（不设 `HMI_DIAG_OUT` 时派发路径逐指令等价）。基线（单跑 36 次）：3,404~5,885 ms / 3,144~5,751 ms。
  - 顺带的静态结论：headless 走的是 **`HeadlessRenderTimer`**（挂在 Dispatcher 上的 `DispatcherTimer`），**不是** `SleepLoopRenderTimer` 那条独立后台线程 —— `AvaloniaHeadlessPlatformOptions::.ctor` 里 `ShouldRenderOnUIThread` **默认 true**（`ldc.i4.1`），全仓两处构造（`UiTestHost.cs:491` / `RenderTestHost.cs:48`）都没改它。⇒ 别再怀疑"渲染定时器按平台泄漏线程"。
  - 🩸 **墙钟必须带负载读数**（用**跑之前**那个 `/proc/loadavg`；跑完时必然被自己顶高）：同一批连跑负载 `1.89→5.09`，同一个程序集就从 27 s 飘到 36 s ⇒ **没有负载读数的墙钟只能同批内部比较**。另：**墙钟差异先按各步骤读数归因，再怀疑争用** —— 门禁 73 s vs 51 s 的真因是 `[3]` 构建 **28.49 s vs 10.8 s**（真重建 vs 增量空构建），**不是**当时写的"并行编辑文档抢 CPU"（已在 59.8 就地更正）。
- 🩸 **`-p:` 改不动 xunit**：`ParallelizeTestCollections` 只从 `xunit.runner.json` / `.runsettings` 的 `<xUnit>` 读，MSBuild 属性**没有任何转发**⇒ 阶段 57 那条「集合并行」否证**从未执行**（已于阶段 59 就地作废）。**一次只放开一个变量之外，还要验证变量真的动了**。
- ⚠ `Dispatcher.UIThread` 的 getter 在 `s_uiThread == null` 时**当场构造**一个 Dispatcher 并抢走它 ⇒ **诊断/探针代码别读它**（只反射读内部 `TryGetUIThread()`），否则观测动作自己制造缺陷。
- #47 **实测（阶段 56）**：人为偷手（非会话线程高频读 `Dispatcher.UIThread`）⇒ **PerTest 下 99.81% 的派发死在平台初始化**（114146/114367，现场 `sessionThread=13, ownerThread=19（非会话线程＝偷手）`）；**`PerAssembly` 下 0/2,000,000** ⇒ 窗口确由「每次派发重置」造成。**修法不落地（阶段 57 定论，见下条）**。判据＝`CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread`（持有者只能是 `null`／会话线程）＋ 读数口 `AmbientUIThreadOwnerId()`/`SessionThreadId`；**别断 `Assert.NotSame` 那类实现细节**。比比率先把机会固定住（按次数比会把「没机会输」读成「赢了」）。
- #47 **偷手抓到一类（阶段 60）**：**`UiTestHost.Pump` 在派发之外按调用线程取 Dispatcher**。`ForceRenderTimerTick` 的支路里有 `Dispatcher::get_CurrentDispatcher()`，`DispatcherTimer` 的**三个构造**也都读它（`HeadlessRenderTimer.StartCore` 用**单参**构造建）⇒ 在派发之外调一次 `Pump(1)`，归属从 `null` 变调用线程（探针 `调用线程=13 / 会话线程=7 / before=null / after=13`）。**已把 `Pump` 纳入派发闸门**，但会话线程上**必须旁路**（一律纳闸必自锁）；判"在不在会话线程上"用 `SessionThreadId`（普通 int），**不能**用 `Dispatcher.UIThread.CheckAccess()` —— 读那个 getter 本身就是犯案动作。判据 `Pump_NeverRunsWhileADispatchIsInFlight`（自造并发条件；负控摘闸门 ⇒ 12/12 红）。**代价为零**（331/331 绿、22 s，对基线 20~23 s）。⇒ #47 的偶发红**仍未复现**，本阶段只排掉一条候选。
- 🩸 **想收「谁在派发之外碰 Avalonia」这个面，先做 IL 普查再求交集**（阶段 60 方法）：搜 `get_UIThread` / `get_CurrentDispatcher` ⇒ Avalonia.Base + Avalonia.Headless 共 **61 个调用点 / 50 个「文件×方法×取法」组合**，再与「仓库在派发之外的入口」求交 ⇒ 候选从「全框架」缩到一条。**阶段 56 那句「真实套件里几乎没有线程在派发之外碰 Avalonia」是没普查过的推断，已被证伪**（确实在碰，只是碰撞只落在窗口最窄的一段）。
- ⚠ **框架 `DrawText` 的单次分配是「环境相关」的**（2026-09-21 实测）⇒ **基准判据别用绝对字节数跨配置**。同一次 `dc.DrawText(ft,p)`：**单类跑 2,604 B**、**整队同进程跑 3,465~3,856 B**；`new FormattedText` 两处都是 384，`FillRectangle`/`DrawRectangle`/`DrawLine`/`DrawGeometry`/`Push*` 两处都是 **0** ⇒ 上抬**只在文本路径**。已否证：隔离级别（`PerAssembly` 单独跑与 `PerTest` 逐字节同）、缓存污染（预灌 600 条互异文本读数不动、`invalΔ` 恒 0）、集合并行（关掉只差 6%）。**⇒ 决定：`PerTest` 保持不动、`CanvasRenderBaselineTests` 预算一个字不改**（放宽容差＝拿未解释读数换判据）。
  - **根因已定位（2026-09-21，阶段 58）**：`dc.DrawText(ft,p)` = **`2,604 + 416 × N`**，`N` = **本平台实例内解析过的、此前没见过的字体族名个数**。headless 的 `HeadlessFontManagerStub.TryCreateGlyphTypeface` **完全不看 `familyName`**、一律返回同一份 4 字形 `BareMinimum.ttf`，**却返回 `true`** ⇒ 每个没见过的族名都算「成功命中」登记进管理器（`FontManager.Current.SystemFonts` 会随之变长）。**⇒ `PerTest` 每次派发重建平台＝顺带清零字体表**；`PerAssembly` 下族名**跨用例累计** ⇒ 起点随执行序漂（阶段 56 那三条越线的来源）。🩸 **判据别抄常数**：`afterReset <= clean` 这种绝对比较被 42 字节漂移踩红（干净值单独跑 2,604 / 整队 2,232）；要写成**只用本次运行自己两个数**的相对形式。守卫 `CanvasRenderBaselineTests.Precondition_PerDispatchPlatformReset_KeepsTextBudgetsComparable`（一次用例三次派发，正控+负控已实测）。**副作用**：测试里所有文本都是 **4 字形 `.notdef`**（`'标'`/`'A'` 都查不到），别指望测试能验真字渲染。
  - **仍未闭环**：`416` 由哪个字段产生（哪张 `Dictionary` 每次绘制被重放）未定位；阶段 57 记的 3,465 对不上任何整数档，可能另叠了贡献。**「测试平台未兜底 `FontManagerOptions.DefaultFamilyName`」→ 明确定为不落地**（桩忽略族名，改了既渲染不出真字也阻止不了阶梯增长）。
- `DefaultRenderLoop` **不是**静态单例（反编译核实：无静态字段、唯一构造点 `RenderLoop.FromTimer`、`Add` 当场读 `Dispatcher.UIThread`）。「渲染循环绑死在第一个会话的 UI 线程上」这句错话已于 2026-09-21 全库更正（9 处）；`RenderTests` 单开程序集的理由是**平台配置进程级、换不了第二套**。
- headless 挂死：`ScrollViewer` 包 `Wrap` 的 `TextBlock` 布局不收敛（#40）。
- **接线两类"双路"**：① 控件事件 `.axaml` 声明 + `.axaml.cs` 再 `+=`（Avalonia **不去重**）⇒ 判据 `check-xaml-duplicate-wiring.py`（门禁 [1d]、fail-closed、`--selftest` 三档）；② 视图事件：视图转发到静态总线 **+** 宿主直订视图（**28 处**，总线侧全有订阅；非幂等 5 处会写坏数据）⇒ #46 **已闭环（阶段 54，留总线删直订）**，守卫 `check-dock-bus-only-subscription.py` 接门禁 `[1e]`（4 个 `_suppress*` 标志只在总线侧检查，不是"只有 AiIcon 一处做防重入"）。读数＝弹窗数 `BtnAiTemplate` +4→+2→+1。
- **本地化键可达性守卫（2026-09-21，阶段 62，登记 #35 的「字典侧」）**：`LocalizationKeyReachabilityGuardTests` ＝ 字典键必须落在**七条机制的并集**里，或进 `KeptUnreachable`（**两侧都做相等判定 ⇒ 反向也会红**，防过期抄写）。七条：M1 `Lang` 常量（反射）/ M2 axaml `{res:Loc}` / M3 插件族派生（**复用**既有守卫的 `DerivedKeys()`，不另抄规则）/ M4 `cfg.note` 清单 / M5 脚本诊断码 / **M7 枚举名小写派生** / **M6 整键字面量＝代理、刻意排最后**。🩸 **代理机制必须写明"它看不见什么"**：`$"reslib.type.{t.ToString().ToLowerInvariant()}"` 这类**模板拼接** M1–M6 全看不见（首跑 31 个不可达里 12 个是它）⇒ **表不全就报红**，不要悄悄放过。豁免表**不许硬塞**（19 个键全判陈旧 ⇒ `KeptUnreachable` 留**空**）。本守卫是**第一个接上 `JudgeReadings` 出口**的（读数 189 行 / 11 块由门禁自己写出）⇒ 那条出口**是可扩展的**，不是给那四个类定制的一次性东西。
  - 🩸 **M1 只问「常量还在不在」，不问「常量有没有人读」⇒ 死常量给死键做担保**（2026-09-21 阶段 63 查出，登记 **#49**）：`project.settings.category.editor` 就是这个 specimen（分类清单 `ProjectSettingsView.axaml.cs:325-330` **恰好 6 项、独缺 `editor`**，而同族另外 6 个常量**就在那 6 行里被代码引用**；该键全仓只出现在两本字典 + `Lang.cs` + **补键工具自己的映射表**）。⇒ 阶段 62 那句「字典侧已闭环」的准确说法是**模掉「常量存在」的闭环**。正解 = 把 M1 收紧成「**有代码引用**的常量的值」（与 #48 的口径**组合**）；**#49 未修**（会一次性 evict 一批键，每条都要按 ≥2 个独立面取证 ⇒ 与 #35 常量侧一起做）。**通用教训**：判据若允许「**输入表/清单本身**」作为满足证据，它就会被那张表自己养着 —— 补键工具的 `VALUE_MAP` 不是可达性证据。
- 🩸 **判据必须自治：不许对共享资源做前后快照比较**（2026-09-21，阶段 61 的判据被这条打红）：`Assert.Equal(before, Directory.GetFiles(TestTempDir.Root).Length)` 里 `TestTempDir.Root` 是**全测试进程共享、被并行写入**的目录 ⇒ 并发下随机红（门禁实测 `1423 → 1424`，**单跑全绿、进门禁就红**）。⇒ 改成**按内容找标记**（唯一字符串，并行用例造不出它）；扫描面**刻意不递归**（`Root` 按 `HMI_TEST_TMPDIR → TMPDIR → Path.GetTempPath() → 仓库 .hmi-test-tmp/` 探测，递归在别的机器上可能走进整个 `/tmp`）。**同类的还有全局计数器 / 进程级静态。**
- 🩸 **负控脚本的还原别用 `shutil.copy2`**（它连 **mtime** 一起复制）：还原后源文件比上次编译产物还"旧" ⇒ MSBuild 判定无需重建 ⇒ **正控实际跑的是负控那次的二进制，于是正控假红**。还原要用「写回原文」（写完 mtime ＝ 现在）。任何"注入 → 跑 → 还原 → 跑"的负控都适用。
- 🩸 **仓库根不能做全量 `os.walk`**：`firmware/` 下挂着**整套 Tina SDK**（`Tina-Linux` + `Tina-SDK`，含 qtwebengine / chromium / 工具链）＝ **1,743,815 个文件**；全量 walk + 逐个 `open()` 会被沙箱 **SIGTERM（退出码 137）且输出 0 字节**（Python 块缓冲没来得及 flush ⇒ 看起来像"什么都没干"）。第二层元凶：**点目录**（`.nuget` / `.Trash-0` / `.hmi-test-tmp`）里有沙箱挂住的 bind-mount，`open()` 会**卡死**。⇒ 分析面一律 `src/ + tests/ + docs/ + tools/ …`，**排除点目录与 `firmware/`**。
- ⚠ **「名字即引用」类判据会被自己的文档/字面量满足**（**已踩三次**：§16.6 的 `MenuPanel`、阶段 62 引 P0-5 的常量名、`Assert.DoesNotContain("ProjectSettingsCategoryEditor", code)`）⇒ **#48 已闭环（阶段 63）**：`LangConstantUsageGuardTests` 改成三档 `ReferenceFace{ Raw, NoComments, Code }` 显式口径，生产判据走 `Code`（`.cs` → `SourceScanner.MaskLiterals`：**剥注释 + 抹字面量内容、保留 `{…}` 插值孔**；`.axaml` → `StripXmlComments`），**基线 1267 → 1272**。🩸 **收益为 0 也要如实报**：注释侧实测**0 例**（全仓注释里 9 处 `Lang.X` 提到的常量全都另有代码引用）⇒ 那一档是**保险**、不是修活缺陷；**字面量侧才是活着的（5 例）**。⇒ 通用契约：**「引用」＝代码里的标识符；散文、失败信息、CSV 表头、控件名字符串、合成源码夹具都不算。** 两条配套判据：① 语义判据**两侧都断言**（旧口径**确实**被散文满足＝病根，新口径不会；片段一律用 `FakeXxx` 这种**不存在的名字**，用具名会重演 #48）；② **单调性**（三档只许变窄 ＋ 必须逐级**真的**变窄 ⇒ 否则口径收紧根本没生效）。修正后的规矩：**注释里现在可以放心写常量名了。**

## 5 配置诚实性
- 应用级默认值＝原硬编码常量；`null`＝下游默认；持久化须 `Dictionary<string, JsonElement>` + `Materialize()`。

## 6 判据与字典（阶段 63/64 定下的）
- **判据的输入面不得含「定义面」**：`M6`（整键字面量扫描）的语料里留着 `Lang.cs` ⇒ 对任何有常量的键**恒真**；`M1` 枚举全部常量值 ⇒ **死常量给死键做担保**。只收一条时自我满足只是**换条机制搬家**（M6 面 113 → 539）。⇒ 键可达性 = **有代码引用**的常量值 ∪ 七条机制（六条精确 + M6 代理）；`KeptUnreachable` 8 条（4 `param.*` 插件兜底 / 4 `role.*` 界面未接线）每条带理由 + 反向判定防过期。
- **收紧口径时先查它的下界/基线**：两处 `1800` 因口径换了而被撞穿（M1 元判据 → 600；常量自检 → 1500）。下界必须跟着口径走。
- **字典处置链**：删字典键 ⇒ **必须连 `Lang` 常量一起删**（`LangKeyCoverageTests` 常量→字典正向）+ 清 `scripts/sync_localization.py` 的 `VALUE_MAP`（**双向**工具，多余的键会被反向生成常量送回来）+ 同步 `UnusedBaseline`。`param.*` 一族**只增不删**（字段 Key 来自外部插件，`PluginLoadContext` 可加载外部程序集）。词典 2132 键 / Lang 常量 1772 / UnusedBaseline 1129。
- **读数出口的第二个用法（比报数字硬得多）**：要证「可达面零损伤」，别只报本次读数 —— 与**上一阶段同一条命令**的读数文件**逐行 diff**（阶段 64 门禁：189 行里只差 `M1 1915→643`、`M6 113→388` 两行，其余 187 行**逐字节相同**）。⚠ 面读数**跨口径不可比大小**：M6 是**涨**的但**不是**「发现了更多」—— 它的输入集是前面机制的**残差键**，M1 收紧把键释放给了它（中间态 539，排除定义面后 388）。
- **`UnusedBaseline` **不是**清理清单（阶段 65 量出构成）**：1129 个「无代码引用的常量」= M2(axaml) **828** / M6(源码字面量) **274** / M3(插件族派生) **18** / 无任何机制 **9**（= `KeptUnreachable`，两侧交叉核对）。前三桶的**键都活着**，只是消费方式不是"读常量" ⇒ 常量是**键空间镜像**，基线语义 = 「**新增一个没人读的常量即红**」。字典 2132 键 / 有常量 1772 / **无常量 360**。
- **`scripts/sync_localization.py` 与上面那条基线方向相反（#52）**：第 2 步会给「字典有、`Lang.cs` 没有」的键**生成常量**（干跑实测 **360** 个 ⇒ 跑一次基线 1129 → ≈1489 ⇒ 判据红 ⇒ 会被"改大基线"消掉）。**已闭环（选「只堵陷阱，不动基线」）**：工具**默认只干跑**（落盘要 `--write`）、「生成常量」另要 `--add-consts`；默认值本身由 `SyncTool_DefaultsToDryRunAndGatesConstantGeneration` 盯着（**锚点必须落在赋值表达式上** —— 落在 `--write` 这类旗标字符串上，会被工具自己的文件头注释满足）。方向 A/B（常量集合是否与键集合同构）仍留作决策项。
- **接线后必须删差异表行**（阶段 66）：`KeptUnreachable` 是**判据的例外**不是豁免 —— 双向判定 `EntriesAreStillUnreachableAndStillPresent` 会在「变得可达」或「从字典消失」时红。接上 3 条 `role.*` ⇒ 表 9 → **5**，常量「无任何机制」桶同步 9 → **5**（两侧**一致地变小**，`UnusedBaseline` 1129 → 1125，**未手改任何断言**）。
- **字典 JSON 不是排序存放的**（按功能分块追加）⇒ `json.dump(dict(sorted(...)))` 重写会产生 **1701 行** diff。做法：逐行手术 + `assert t.count(old) == 1` + 备份 + 回读；判据形状＝「删的行 ⊆ {那一行}、加的行 ⊆ {新增行}」（相邻夹缝处删除块与插入块会**重叠成一个 replace**，属正常）。
- **负控判红的谓词**必须匹配测试自己的汇总行（`失败: N`）**并且**取到 `错误消息:` 那一行；写成「`stdout` 里没有『已通过!』」会把**编译失败**也算红（阶段 21 的坑，阶段 66 又踩一次）；认不出的归 `unknown`，与 `build-fail` 同样**不算数**。判据里「**有没有**」的断言要排在「**对不对**」之前，否则负控只报泛泛的 `Strings differ`。
- **子串定位要带换行边界**：`## 验证记录` 是 `### 验证记录` 的子串 ⇒ `str.index` 找到的是别处的三级标题，自检报**假失败**（阶段 66 踩到）。

- **掩码按机制选**：常量使用面 = 剥注释 **+** 抹字面量；键可达性 **M6 = 只剥注释**（它找的就是字面量，抹掉等于删掉这条机制）。`KeptUnreachable` 现 **9 条** —— `param.baud.rate` 是「陈旧键 5 个」的第 5 个，此前只被**两句 XML 文档注释**撑着（其中一句就是守卫自己的类注释）。
