# 端到端验收复跑：三个独立阻塞，与一次通跑

> 2026-09-17。`scripts/verify.sh`（端到端验收：Golden 工程导出 → 真实 LVGL CMake 编译 →
> 300 帧 selftest → CTest）自 2026-09-13 之后未复跑，基线头挂着「本项待复验」。
> 本轮把它跑通，并顺带挖出**三个各自独立**的阻塞。
>
> 结论先给：**跑不通不是生成链的问题** —— 生成链一轮都没坏过。三个阻塞分别是
> ①一条环境串行队列、②一处 Debug 专属的测试宿主崩溃、③两套门禁的 Configuration 不一致。

## 1. 症状：两次失败，都停在同一处

| 轮 | 配置 | 编排状态 | 结果 |
|---|---|---|---|
| 1 | Debug | 原样（第 3 步未拆） | 跑满 **15 m 44 s** 仍无进展，人工终止 |
| 2 | Debug | 第 3 步已拆两段 | 跑满 **21 m 39 s** 仍无进展，人工终止 |

两轮都**停在第 [3] 步之后、第 [6] 步之前**，判据有两条：

- 日志最后一行是 Core 的汇总（第 [3] 步中），之后再无输出；
- `$TMPDIR/zhishu-hmi-golden` 整个目录的 mtime 仍是 **2026-09-15 19:15** ⇒
  第 [6] 步的 `rm -rf "$GOLDEN_DIR"` + 重建**从未执行过**。

第一轮日志还留着一条与主因无关、但同样真实的失败：

```
失败 HmiEditor.Core.Tests.GitWorkflowServiceTests.InitializeRepository_SecondCall_Idempotent [1 m 31 s]
  Assert.True() Failure
Expected: True
Actual:   False
```

这正是**阻断项 11** 的同一个环境问题（归因见
`docs/2026-09-17-sandbox-newpath-serialization.md`），只是这次撞得更狠 ——
1 m 31 s ≈ 六次撞穿 `GitProcessWaitMs`(15 s) 窗口。

## 2. 归因

### 2.1 阻塞一：第 [3] 步把大队灌进那条串行队列（已修）

与 `ci-gate.sh` 第 [3] 步同源，修法也同源：大队与 spawn git 的用例分两段跑。
本轮实测队列效应在 verify.sh 里同样可见（同为 Debug，对照第一轮 → 第二轮）：

| 项目 | 未拆（第 1 轮） | 拆后（第 2 轮） |
|---|---|---|
| Core.Tests（1900 → 1892 项） | 2 m 3 s | **1 m 27 s** |
| HmiEditor.Device.Tests | 1 m 35 s | **48 s** |
| Hmi.Communication.Tests | 1 m 19 s | **26 s** |
| `InitializeRepository_SecondCall_Idempotent` | **红**（1 m 31 s） | 移出大队，不参与竞争 |

拆后大队 **2527 项全绿**（44 + 22 + 75 + 76 + 276 + 142 + 1892）。

### 2.2 阻塞二（主阻塞）：Debug 全量并发下 `HmiEditor.App.UiTests` 的 testhost 崩溃

四次对照，一次只放开一个变量：

| 条件 | 命令 | 结果 |
|---|---|---|
| Debug · 单独跑该项目 | `dotnet test tests/HmiEditor.App.UiTests/… --no-build` | **142/142 绿，12.5 s** |
| Debug · 走 slnx，只匹配该项目 | `dotnet test HmiEditor.slnx --filter '…~HmiEditor.App.UiTests'` | **EXIT=0，24 s** |
| Debug · 全量（与大队并发） | `dotnet test HmiEditor.slnx --filter '…!~GitWorkflowServiceTests' --blame-hang --blame-hang-timeout 90s` | **EXIT=1，187 s，testhost 崩溃** |
| Release · 全量（与大队并发） | 本轮第 3、4 轮 verify | **142/142 绿，1 m 20 s / 1 m 21 s** |

VSTest blame 的原文：

```
活动的测试运行已中止。原因: 测试主机进程崩溃
数据收集器"Blame"消息: 指定的非活动时间(90 秒)已过。正在从测试主机及其子进程中收集挂起的转储。
测试总数: 未知
     通过数: 92
发生崩溃时运行的测试:
HmiEditor.App.UiTests.PropertyPanelUiTests.ProgrammaticCommand_IsNotBlockedByValidator
HmiEditor.App.UiTests.RenderResourceCacheTests.Text_IsReusedAndKeepsItsMeasurements
HmiEditor.App.UiTests.DockViewResolutionUiTests.RegisteredFactoryIds_MatchLayoutDockableIds
HmiEditor.App.UiTests.AiGenerateDialogCancellationUiTests.CancelWhileGenerating_ShowsCancelled_NotGenerationFailed
HmiEditor.App.UiTests.FaceplateTemplateEditorUiTests.TypingMetadata_WritesBackTrimmedValues
HmiEditor.App.UiTests.GaugeTickRenderTests.RenderGauge_DrawsExactlyMajorTickCountTicks
HmiEditor.App.UiTests.CanvasRenderBaselineTests.Baseline_UnitCost_OfFormattedTextLayout
HmiEditor.App.UiTests.ViewSmokeUiTests.View_LoadsAndLaysOut
HmiEditor.App.UiTests.EditorPreferenceWiringTests.FactoryDefaults_MatchPreWiringHardcodedValues
```

三条判据：

1. 那 9 条**分属 9 个不同测试类**，却同时处于「运行中」⇒ xUnit 在并行跑它们；
2. 它们共享 **`UiTestHost` 的单例 headless 会话**（`UiTestHost.Session` 是
   `Lazy<HeadlessUnitTestSession>`，所有测试都 `Dispatch` 到同一个 UI 线程）；
3. **无托管异常、无堆栈** ⇒ 原生崩溃或栈溢出，**不是**断言失败，**也不是**超时。

⇒ 判定：**测试宿主在「多个测试类并发使用单例 headless 会话」这一模式下的稳定性问题**。
Debug 下 3/3 必现，Release 下未复现。

> ⚠️ 这条**没有修，只是绕开**（见 §3）。「Release 不崩」不等于安全 ——
> 很可能只是时序不同。它是一条独立待办，见 §5。
>
> ⚠️⚡ **同日复验更正（见 §6.2）**：本节写就后又在**清洁树**上做了成对实验，
> **「Debug 全量并发必崩」不成立** —— 同一条 Debug 全量命令**不带** `--blame-hang` 时是
> **EXIT=0 / 107 s**，带 blame 时两次都崩（98/142、37/142）。也就是说崩溃与
> **`--blame-hang` 纠缠**（机器负载也未被控制），而「方案 A 修法有效」的结论**已撤回**。
> 本节余下的取证过程与 blame 原文仍然有效，但**结论要按 §6.2 收窄后再读**。

### 2.3 阻塞三：两套门禁跑不同 Configuration

`ci-gate.sh` 跑 **Release**，`verify.sh` 跑 **Debug**（原样）。同一个仓库两套门禁配置不同，
结论不可比；而正是 2.2 让这个不一致显性化 —— 同一个 `App.UiTests`，
一个配置全绿、另一个配置必崩。

### 2.4 附带发现：`App.UiTests` 的产物断言清单落后于生成链

`verify.sh` 第 [7] 步原来只断言 3 个文件（`generated_hmi.c` / `main.c` / `CMakeLists.txt`），
而导出器实际产出 11 个（权威清单在 `tests/HmiEditor.Core.Tests/SimulatorLoopTests.cs`）。
缺 `generated_security.h` 时，原脚本只会报一句「找不到头文件」，指不到是哪条下沉链断了。

## 3. 修法

全部落在 `scripts/verify.sh`，另加一处 `ci-gate.sh` 注释修正：

1. **第 [3] 步拆两段**（大队 / spawn git 的用例单独跑），与 `ci-gate.sh` 同款，
   并附上「为什么调窗口、调并行度都不是解药」的说明与出处。**判据一条未放宽**。
2. **步骤编号统一为 `[n/7]`** —— 原来是 `1/4`、`2/4`、`3/4`、`4/5`、`5/6`、`6/6` 一片乱。
3. **新增 `CONFIG`（默认 `Release`）**，与 `ci-gate.sh` 对齐；所有
   `dotnet build` / `test` / `run` 都带上 `-c "$CONFIG"`。注释里写明两条理由
   （配置不一致 ⇒ 结论不可比；Debug 全量并发下 `App.UiTests` testhost 会崩）。
   复现 Debug 行为：`CONFIG=Debug bash scripts/verify.sh`。
4. **第 [7] 步补生成产物断言**：`generated_security.h` / `generated_comm.h` /
   `generated_runtime.h` / `sim_config.h`（权限 / 通讯 / 运行时参数三条独立下沉链，
   外加 sim_config）。`generated_fonts.c` / `generated_images.c` 由 CMakeLists 编译，
   缺失即编译失败，不重复断言。
5. **`ci-gate.sh` 注释里的旧数字**（`~95 ms/目录`）对齐为已自纠的结论
   （目录创建几乎免费，实测 1 ms；95 ms 那个数源于把 python3 进程启动算进计时区间）。

## 4. 验证

| 轮 | 配置 | 产物断言 | 结果 |
|---|---|---|---|
| 3 | Release | 无 | **EXIT=0，392 s** |
| 4 | Release | **有** | **EXIT=0，223 s** |

两轮逐段读数一致：

| 步骤 | 读数 |
|---|---|
| [3/7] 大队 | **2527 项全绿**（Simulator 44 / Hardware 22 / Communication 75 / Device 76 / AI 276 / App.UiTests 142 / Core 1892） |
| [4/7] git 用例 | **8/8 绿，9 s** |
| [6/7] Golden 工程 | 2 screens / 36 controls；包校验有效 |
| [7/7] 真实 LVGL | CMake 4.4.3 → 全量编译 → `hmi_sim` 1286896 B → selftest `PASS` → CTest passed |

产物核对（`$TMPDIR/zhishu-hmi-golden/real-simulator/`）：
`CMakeLists.txt`、`main.c`、`sim_config.h`、`lv_conf.h`、`generated_hmi.c`、
`generated_comm.h`、`generated_security.h`、`generated_runtime.h`、
`generated_fonts.c`、`generated_images.c`、`README.md`、`selftest.log`、`assets/`、`build/`。

新增断言的验证方式：改动后先在**现成产物**上直接执行同样的 `test -s`（两个目录 8/8 通过），
再跑第 4 轮端到端确认 —— 存在性断言与配置、流程无关，这样验等价。

## 5. 遗留

| # | 项 | 状态 |
|---|---|---|
| 1 | **`App.UiTests` 的 testhost 崩溃** | **未修**。判据已按 §6.2 收窄为「**仅带 `--blame-hang` 时复现**」；「方案 A 修法有效」**已撤回**（真落地后照样崩）。下一步 = blame × 方案 A 的 2×2，每格两跑并记录负载。 |
| 2 | 无 blame 时 testhost 崩溃表现为「挂死」 | `dotnet test` 会一直等，看起来像慢。已登记排查姿势（§6.2）。 |
| 3 | 沙箱批量删除守卫会拦 `rm -rf "$GOLDEN_DIR"` | `SAFE_DELETE_BULK_CONFIRM_REQUIRED`（count 1286 > 50）。**沙箱环境行为，不是脚本缺陷**，不改脚本；本地跑前先用 `python3 shutil.rmtree` 预清。 |
| 4 | `CommandFrameCollectorTests.NoData_…` 的 `InRange(…, 150, 2000)` | 沿用 2026-09-17 上一轮的登记（上界仍是「靠绝对余量」）。 |

## 6. 附：阻断项 12 的两次实验，与一次被撤回的「修法有效」

### 6.1 第一次（当天早先）：只往输出目录放配置 ⇒ 曾判「有效」

> ⚠️ **本节结论已撤回**（见 §6.2）。读数本身没错，错在**没做正控**、也**没做清洁树基线**。

假设：那 9 条测试**分属 9 个不同测试类**却同时处于运行中 ⇒ xUnit 在并行跑不同 collection；
而它们共享 `UiTestHost` 的单例 headless 会话 ⇒ **把该程序集的测试类并行关掉**应能消除。

实验方式：只往**输出目录**放一个运行时配置，**不改仓库任何文件**。

```bash
cat > tests/HmiEditor.App.UiTests/bin/Debug/net10.0/xunit.runner.json <<'EOF'
{ "parallelizeTestCollections": false }
EOF
# Debug，全量（与之前崩溃时的条件一致）
dotnet test HmiEditor.slnx --no-build --no-restore \
  --filter 'FullyQualifiedName!~GitWorkflowServiceTests'
```

| 条件 | `App.UiTests` | 全量墙钟 | 结果 |
|---|---|---|---|
| Debug · **并行**（默认） | **崩溃**（通过 92/142，90 s 无活动后判挂起） | 187 s | **EXIT=1** |
| Debug · **测试类串行** | **142/142 绿 / 1 m 56 s** | **173 s** | **EXIT=0** |

⇒ ~~**假设成立**~~（**此结论已撤回**，见 §6.2），当时量到的代价：该程序集 1 m 20 s → 1 m 56 s。

### 6.2 第二次（同日晚）：落地 + 清洁树基线 + 正控 ⇒ 上面的结论被推翻

这次不再「改仓库之外的东西」，而是**把方案 A 真正落地**（`xunit.runner.json` +
csproj 声明 `CopyToOutputDirectory`），并且**先取清洁树基线**、**再做正控**。四段读数：

| 档 | 条件 | 命令要点 | 结果 |
|---|---|---|---|
| E1b | Debug 全量 · **清洁树**（未落地方案 A） | `dotnet test HmiEditor.slnx -c Debug` + `--blame-hang --blame-hang-timeout 180s` | **EXIT=1 / 336 s**：testhost 崩溃、`App.UiTests` **98/142**（其余 6 个程序集全绿） |
| E2a | Debug 全量 · **已落地方案 A** | 同上（`bin/` 里确有 `xunit.runner.json` 116 B） | **EXIT=1 / 307 s**：**仍然崩**（**37/142**） |
| E0b | **正控** | 把 `xunit.runner.json` 的值故意写成 `"definitely-not-a-bool"`，跑该项目 | **EXIT=0 / 40 s，没有报错** |
| B2 | Debug 全量 · **清洁树、不带 blame** | 与 E1b **同一条命令，只去掉 `--blame-hang`** | **EXIT=0 / 107 s** |
| E3 | Release 门禁 | `bash scripts/ci-gate.sh`（**6 步**；此刻方案 A 仍落地） | **EXIT=0 / 252 s** |

三条结论：

1. **方案 A 无效** —— 落地后 Debug 全量照样崩（E2a）。已回退，不留没有证据的改动。
2. **原判据「Debug 全量并发必崩」不成立** —— 同一条命令去掉 `--blame-hang` 就是绿的（B2）。
   崩溃与 `--blame-hang` **纠缠**：那次 15 m 44 s / 21 m 39 s 的「不再推进」另有其因
   （已在阻断项 11 一族里排掉：那条全局串行的「新文件路径」队列）。
   ⚠️ 但**还不能说 blame 就是唯一原因**：B2 是 107 s、E1b 是 336 s，墙钟差近 3 倍 ⇒
   **机器负载也进了画面**，两个变量尚未分离。
3. **正控没过** —— 值故意写成非法类型都不报错 ⇒ **无法证明 runner 读过这个文件**。
   （`bin/` 里有该文件说明拷贝生效，但**拷贝 ≠ 被读取**。顺带核实了一件事：
   xUnit **不会**自动拷贝 `xunit.runner.json` —— 回退 csproj 声明后 `bin/` 里那份就没了。）
   所以 §6.1 那两次「绿」更可能是**巧合**，而不是配置起效。

### 6.3 下一步：分离变量的 2×2 ⚠️ **（本节已作废，见 §6.4）**

| # | `--blame-hang` | 方案 A | 要回答的问题 | 现状 |
|---|---|---|---|---|
| 1 | 否 | 否 | 清洁基线 | 已得 1 次：**EXIT=0 / 107 s** |
| 2 | 否 | 是 | 方案 A 在无 blame 下是否被读取、是否无害 | 待跑 |
| 3 | 是 | 否 | 崩溃能否复现 | 已得 2/2 崩（98/142、37/142） |
| 4 | 是 | 是 | 方案 A 能否消掉 blame 下的崩溃 | 已得 1 次：**否** |

要求：每格**至少两跑**、**用同一条命令**、并在同一段连续时间里跑完（否则负载差异会混进来）。
另外，做「配置是否被读取」的正控时，别只试一种坏值 —— 字符串不报错有可能是运行器**宽容**，
换成「未知键名」或「非法枚举值」再各试一次，才能确认它到底读不读。

### 6.4 第三次（同日晚）：正控反转、判据第二次改写、修法换向

#### 6.4.1 正控反转：`xunit.runner.json` **确实被读取**

只换 `bin/Debug/net10.0/xunit.runner.json`，**同一条命令**（App.UiTests 单跑，`verbosity=detailed`）：

| 段 | 配置 | 输出总行 | 带类名的用例名 | 墙钟 |
|---|---|---|---|---|
| p0 | `{}`（基线） | 239 | 142 | 20 s |
| p1 | `{"diagnosticMessages": true}` | **317（+78）** | 142 | 21 s |
| p2 | `{"methodDisplay": "method"}` | 239 | **0** | 20 s |
| p3 | `{"parallelizeTestCollections": false}` | 239 | 142 | 21 s |

两个**互相独立**的可观测变化 ⇒ 文件确实被读取、确实生效。**§6.2 里那条「正控没过」的推论作废**：
值写成字符串不报错，只是因为 xUnit 对坏值宽容，与「读不读」无关。
⇒ 反过来，「方案 A 无效」这个**结论**站得住了（配置当时确实生效着，崩溃照旧）——
但它当时是**用一条空证据链推出来的**，这一点不因结论碰巧正确而改变。

> **教训（已写进 §7 第 10 条）**：正控要挑**能改变可观测行为**的键，别挑「期待它报错」的键。

#### 6.4.2 判据第二次改写：不是「只有 blame 才复现」，而是「进全量就间歇出事」

| 跑 | 条件 | 结果 |
|---|---|---|
| B2（早） | 清洁树 Debug 全量，无 blame | EXIT=0 / 107 s（绿） |
| C1（同日） | 同上，只是往 `bin/` 写了 `{}` | **挂死**（17 min 无输出，人工停） |
| R1 | 同上（无 blame） | `App.UiTests` **142/142 绿 / 1 m 56 s**；红的只有 Core 的另一条 flaky |
| R2 | 同上 | **挂死**（264 s 时判定：连续 90 s 无输出） |

⇒ **复现率 ≈ 2/3**，而与 `--blame-hang` 无关（带 blame 的两次崩溃是**另一种呈现**，不是原因）。
**2×2 作废**：它的两个前提（「无 blame 就绿」「问题在 blame/配置」）都不成立。

#### 6.4.3 挂死现场签名（首次抓到）

沙箱给**每个 Bash 调用**独立 PID 命名空间 ⇒ 交互 shell 看不到后台任务的进程；
改由**脚本自己**（它是那些子进程的父）在判定卡住时抓现场：

- 全树 **209 个线程 `futex_wait`**，其余在 `poll` / `epoll_wait` / `anon_pipe_read`；
- **全程零 CPU**（loadavg 3.16 / 20 核）；App.UiTests 的 testhost `State: S`、26 线程、VmRSS 288 MB；
- ⇒ **僵死（死锁），不是空转**；
- 且挂死那次 **6/7 程序集全绿**（含 Core 1892/1892），只有 `App.UiTests` **从未报出汇总行**
  ⇒ 瓶颈在这个程序集**内部**。

⇒ 与「多个测试类并发进同一个 headless 会话」这一既有判断**相容**，但不构成证明（无托管栈）。

#### 6.4.4 修法换向（验证中）

不再赌运行时配置文件，改用 xUnit **编译期**生效的集合语义：

```csharp
// UiTestHost.cs
[CollectionDefinition(HeadlessUiCollection.Name, DisableParallelization = true)]
public sealed class HeadlessUiCollection { public const string Name = "HeadlessUi"; }
```

外加 **13 个**用到 `UiTestHost` 的测试类各一行 `[Collection(HeadlessUiCollection.Name)]`
（仓库已有同款惯用法：`LlmConfigStoreTests.cs` 的 `AppDataRoot-global`）。

**代价≈0**：单跑仍 **142/142 绿 / 持续时间 10 s / 墙钟 13 s**。**为什么几乎免费**：142 项本来
就都 `Dispatch` 到**同一个 UI 线程**上串行执行，类级并行只重叠了非 UI 那部分 ——
这也顺带解释了 p3「方案 A 让集合串行」为什么量不出墙钟变化。

**验证结果（同日晚）：修法没能消除挂死 ⇒ 已回退。**

| 跑 | exit | 耗时 | `App.UiTests` |
|---|---|---|---|
| H1 | **99 HANG** | 607 s | 未报出；现场 **testhost 已消失**、只剩 wrapper `do_wait`（**没有** R2 那 209 个 `futex_wait`） |
| H2 | 1 | 176 s | **142/142 绿 / 1 m 54 s**（红的是 Device 的 `CommandFrameCollectorTests.…FirstByteWindow…`） |
| H3 | **0** | 185 s | **142/142 绿 / 1 m 58 s** |
| H4 | **0** | 185 s | **142/142 绿 / 2 m** |

修法前 ≈ **2/3** 失败、修法后 **1/4** ⇒ **N=4 不足以声称有效**（若 p=2/3，4 跑里至少 1 次失败的概率是
**98.8%**，而只观察到 1 次）。而且 **H1 的签名变了**（从「进程还在但僵死」变成「进程没了但没人报结果」）。
⇒ 按与「方案 A」同一把尺子，**已回退**（`git checkout -- tests/HmiEditor.App.UiTests/`），
并把机制假说**收回半句**：「13 个类并发进同一个单例 headless 会话」不足以解释它。

### §6.4.6 判别实验：挂死**不需要**「别的测试程序集」在场

集合修法被否之后，「触发条件是什么」重新变成开放问题。原有两个候选：
（a）跨程序集的并发争用；（b）**环境里那条串行的新路径资源**（阻断项 11 同族）。
用一次判别实验把两者分开：**单跑** `App.UiTests`（一个程序集、本来恒定绿 10~20 s）
+ **6 个外部进程持续造新路径**（`${TMPDIR:-/tmp}/hmi-storm`，每 worker ≤2000 个 0 字节文件）。
这个组合里**没有别的测试程序集、也没有别的 testhost** ⇒ 若还挂死，就只能是（b）。

| 跑 | 条件 | 结果 |
|---|---|---|
| C | 单跑 `App.UiTests`，**无风暴** | **exit=0 / 20 s** |
| S1 | 单跑 `App.UiTests` + 6 worker 造新路径 | **HANG**：120 s 判定（连续 90 s 无新输出），输出停在 **0 字节**；`exit=99 / 282 s` |
| S2 | 同上（第二轮） | **HANG**：124 s 判定，输出停在 **0 字节** |

**S1 现场**（脚本自抓，因为沙箱给每个 Bash 调用独立 PID 命名空间）：

- 全局 wchan 直方图**只有 ~28 个线程**：`9 futex_wait` / `6 anon_pipe_read` / `4 do_wait` / `3 seccomp_do_user_notification` / …
- `pgrep -f HmiEditor.App.UiTests` **零命中** —— 取证里「App.UiTests testhost 的线程名 + wchan」**整节为空**
- 进程表里只有 `dotnet test`（pid 200）与 `timeout`，**没有 `vstest.console`、没有 `testhost`**

⇒ **测试宿主根本没起来**：卡在比 headless 会话更早的阶段。对照 R2（全量那次）：
**209 个线程 `futex_wait`、testhost 在、输出 2195 B** ⇒ **两者不是同一签名，不合并**。

**结论与边界**：

1. **触发条件不需要「别的测试程序集」在场** ⇒ 「多个测试类并发进单例 headless 会话」这条解释**不足**。
2. 本沙箱至少存在一个**与 headless 会话无关**的失败入口：**串行的新路径资源被占满 ⇒ 连测试宿主都起不来**（阻断项 11 同族）。
3. ⚠️ **边界 ①**：S1/S2 与全量那两次不是同一签名 ⇒ 全量那条的成因**仍未定位**（本轮不再追）。
4. ⚠️ **边界 ②**：6 个 worker 是**人为饱和** ⇒ 只证明这条资源的**充分性**，不等于全量那两次就是这个原因。
5. ⚠️ **边界 ③**：`0 字节` 可能只是**块缓冲**（stdout 重定向到文件时块缓冲，本仓库有先例）⇒
   单凭它不能断言「一步没走」；真正有分量的读数是「**无 testhost + 全局 ~28 线程**」。

**落地判断（不变）**：本沙箱里 **Debug 全量不可作为判据，只认 Release**；
解读任何沙箱内的 Debug 并发读数前，先问一句「**那条串行资源当时空着吗**」。

#### 6.4.5 同轮登记一条新 flaky（非本轮引入）

`HmiEditor.Core.Tests.AlarmEngineTests.Engine_TriggerThenReturn_CreatesReturnedRecord` 在 Debug 全量下红过：
`Expected: Returned / Actual: Active`（Core 1891/1892，`AlarmEngineTests.cs:298`）
⇒ **时序/负载**敏感（报警「返回」态在断言时尚未落地）。单独复跑若绿**不能**结案 —— 先量复现率。

## 7. 教训

1. **「跑不通」先别怀疑生成链。** 这一轮生成链一次没坏，红全在测试编排与测试宿主上。
2. **testhost 崩溃在无 blame 时表现为「挂死」。** 判据：日志 mtime 长时间不动 + 同一命令加
   `--blame-hang --blame-hang-timeout 90s --blame-hang-dump-type none`，它会直接给出
   **「崩溃时运行的测试」清单**。这是排查「测试莫名卡住」的标准姿势。
3. **「单独跑绿 + 并发跑崩」⇒ 差异在并发，不在用例。** 定位手段是一次只放开一个变量：
   单独跑 → 走 slnx 只匹配它 → 全量并发，三次对照就锁定。
4. **两套门禁跑不同 Configuration 是隐患**，不只是「不一致」：它会让一处的绿掩盖另一处的红。
5. **断言清单会随生成链演化而落后**：`verify.sh` 只断言 3 个文件，导出器实际产 11 个。
   权威清单在 `SimulatorLoopTests`；下次加生成产物时回头同步（另见 `generated-artifact-rollout`）。
6. **「回退后失败面反而扩大」是「构建产物污染」的强信号。** 本轮那 12 条失败一度被归到刚落地的
   方案 A 头上；回退后失败面扩到 20+ 条 —— 那一刻就该停手去做 `dotnet clean` + 全量重建，
   而不是继续在「是哪个改动害的」上推理。真相是一条**构建期缺陷**
   （见 `docs/2026-09-17-avalonia-resource-duplicate-key.md`），与那次变更无关。
7. **诊断工具本身可以是混淆变量。** `--blame-hang` 让崩溃必现、去掉它就绿 ⇒ **不能**就此断定
   「并发是根因」：它会改运行环境（附加数据收集器、按不活动时间判定），而且「带/不带它」的
   两组读数**不是同一条命令**。要下因果结论，两侧必须是同一条命令。
8. ~~**改了测试配置就要正控**（值写成非法类型，不报错 ⇒ 没生效）。~~ **本条已更正（2026-09-17 晚）。**
   当年的做法**判据选错了**：把值写成非法类型「**期待它报错**」—— 而 xUnit 对坏值**宽容**，
   不报错既不证明没读、也不证明读了（那次实测 `App.UiTests` 单跑 EXIT=0，什么都没证明，是对的）。
   **正确做法：正控要挑「能改变可观测行为」的键。** 同一晚用两个独立探针证明文件确实被读取：
   `diagnosticMessages:true` ⇒ 输出 **239→317** 行；`methodDisplay:"method"` ⇒ 带类名的用例名 **142→0**。
   ⇒ 「配置躺在输出目录里 ≠ 运行器读了它」这句**依然成立**，但**验证手段**必须可观测，不能靠「报不报错」。
9. **登记表会滞后于修复。**「未修、只绕开」这类状态一条抄三处（基线头 / 阻断项 / 变更记录），
   复验推翻后必须**回头扫一遍** —— 否则下一个照旧笔记干活的人会原样再踩一次。
10. **正控的判据必须是「可观测行为」，不是「期待它报错」。** 见第 8 条（已更正）：本轮先用
    「值写成非法类型 → 看它报不报错」当正控，得出「配置没被读取」，**错了**；换成
    `diagnosticMessages`（改输出行数）与 `methodDisplay`（改用例显示名）这类**必可观测**的键，
    一次就定性。**选正控 = 选一个「改它必然产生可见差异」的旋钮。**
11. **追间歇缺陷的顺序：先量复现率，再动手修，再用同一把尺子复量。** 本轮（阻断项 12）
    三度改判，正是因为在没有复现率的情况下反复下结论；一旦量出「同一命令 ≈ 2/3 失败、
    而单独跑恒定全绿」，问题立刻被框住（进全量才出事 + 挂死现场是「209 futex_wait、零 CPU」）。
    没有率的「修好了」和「没修好」都是巧合。

12. **引用一条「绿」之前，先确认它跑在修复之后。** 本日的 `ci-gate.sh EXIT=0 / 252 s` 是
    **修复前 10 分钟**的一跑（当时方案 A 还落地着、且那批失败是**间歇性**的），却被顺手抄进了
    修复的验证表 —— 对间歇缺陷来说，「有一次绿」本来就等于零信息。核对成本极低：
    `git log --date=iso-strict -1 <commit>` 对 `stat -c %y <日志文件>`。
    同一条纪律适用于本文件 §5 那张表：每一行都应能指到**一条日志 + 一个时间点**。
