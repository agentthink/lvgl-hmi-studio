# 测试定时器 flaky 的根因与修复（2026-09-14）

## 现象

`dotnet test HmiEditor.slnx -c Release` 全量跑时，`HmiEditor.Core.Tests` 偶发 1 项红，
**每次红的用例都不同**，单独复跑该类则全绿。

连续三轮全量跑的实际记录：

| 轮次 | 红项 | 位置 |
|---|---|---|
| 1 | `AlarmEngineTests.Engine_DelayedTrigger_OnlyFiresAfterDelay` | `Assert.NotEmpty() Failure: Collection was empty` |
| 2 | `AlarmEngineTests.Engine_AlarmTriggeredEvent_Fires` | `AlarmEngineTests.cs:403` `Assert.True() Failure` |
| 3 | `AlarmEngineTests.Engine_ClearReturnedRecords_RemovesOnlyReturned` | — |

加上更早轮次观测到的 `AlarmEngineTests.Engine_*` 与
`DataLoggerTests.Logger_StartStop_UpdatesSampleCount`，**同一个类里共 4 个不同用例轮流红**。
单独复跑 `--filter FullyQualifiedName~AlarmEngineTests` → **27/27 全绿**。

「同一类里不同用例轮流红 + 单独跑全绿」是典型的**非确定性环境依赖**特征，不是产品缺陷。

## 归因

先排除产品侧：

```bash
grep -n "lock\|ConcurrentDictionary\|volatile\|Interlocked" src/HmiEditor.Core/Services/AlarmEngine.cs
# → _lock 保护 _alarmConfigs / _lastVariableValues / _conditionMetSince
# → _activeRecords 用 ConcurrentDictionary
```

**共享状态有锁保护，不是并发安全缺口。**

真正的根因在测试写法：

```csharp
engine.Start();
Thread.Sleep(100);              // ← 假定「100ms 足够跑完一轮」
Assert.True(triggered);
```

这条假设为什么站不住：

| 环节 | 事实 |
|---|---|
| 引擎调度 | `System.Threading.Timer`（`_evaluationTimer.Change(0, _evaluationIntervalMs)`） |
| 回调在哪跑 | **ThreadPool**（`Timer` 的既定行为，不是配置项） |
| 用例间隔 | `EvaluationIntervalMs` 50–500ms；`Thread.Sleep` 50–300ms，**无余量** |
| 外部干扰 | 全量并行跑时 7 个测试程序集同时抢 ThreadPool，回调可被推迟任意倍 |

于是「Sleep 结束 ⇒ 定时器至少跑完一轮」在并行负载下随机不成立 → 断言随机红。

### 与既有正确写法的对比

同一个文件里其实**早就有正确写法**，只是没推广：

```csharp
// AlarmEngineTests.Engine_TriggerThenReturn_CreatesReturnedRecord —— 本来就是对的
engine.Start();
Assert.True(SpinWait.SpinUntil(() => engine.GetAllRecords()
    .Any(r => r.AlarmName == "快速报警"), 1000));
```

即：**等产品状态，而不是等时间**。问题是这类写法在 8 个用例里只用了 2 个，
其余用固定 `Thread.Sleep`。

> ⚠️ **更正（2026-09-15）**：上面把 `Engine_TriggerThenReturn_CreatesReturnedRecord`
> 当成「**本来就是对的**」范式引用 —— **这个判断不准确**。它只是「比 `Thread.Sleep` 强」，
> 本身仍在漏网之列：
>
> | 它的毛病 | 后果 |
> |---|---|
> | 超时写死 `1000ms`（已推广的 `TestWait.Until` 用 `5000ms`） | 全量并行下 ThreadPool 被抢就超时 |
> | 没有失败消息（`Assert.True(false)` 看不出在等什么） | 红了两眼一抹黑 |
> | 同一个类里 3 处这样写，另 9 处已改 `TestWait.Until` | 正确写法**没推广到全文件** |
>
> 于是它**在全量并行下真的红了**（本轮 #18 轮，单跑 3/3 绿）—— 见文末「补修」。
> 这正是本文教训 2「按机制聚类，不要按用例逐个打补丁」的反面教材：
> 当年按**用例**扫了一遍，恰恰漏掉了这个"看起来已经对了"的。

## 修复

### 新增 `tests/HmiEditor.Core.Tests/Support/TestWait.cs`

```csharp
public static void Until(Func<bool> condition, string what, int timeoutMs = 5000)
    => Xunit.Assert.True(
        SpinWait.SpinUntil(condition, timeoutMs),
        $"等待超时（{timeoutMs}ms）：{what}");
```

- 与既有 `Support/TestTempDir.cs` 同约定（命名空间 `HmiEditor.Core.Tests`，被各测试类直接引用）。
- 轮询退避由 `SpinWait` 自身完成（先 yield，再 `Sleep(1)`），CPU 占用可忽略。
- 超时 5s：远大于被等待的间隔（50–500ms），远小于挂死判定阈值。
- 失败信息带「在等什么」，把「等了 5 秒还是没等到 X」直接写进报错。

### 逐个用例改造

| 文件 | 用例 | 原写法 | 现写法 |
|---|---|---|---|
| `AlarmEngineTests` | `Engine_TriggerHighLimit_CreatesActiveRecord` | `Sleep(100)` | 等 Active 记录出现 |
| `AlarmEngineTests` | `Engine_AcknowledgeAlarm_SetsState` | `Sleep(100)` | 等该报警 Active |
| `AlarmEngineTests` | `Engine_DelayedTrigger_OnlyFiresAfterDelay` | `Sleep(50)` + `Sleep(300)` | **探针报警**（见下） |
| `AlarmEngineTests` | `Engine_AlarmTriggeredEvent_Fires` | `Sleep(100)` | 等事件标志 |
| `AlarmEngineTests` | `Engine_ClearReturnedRecords_RemovesOnlyReturned` | `Sleep(100)`×2 | 等「2 条 Active」→「转为 Returned」 |
| `AlarmEngineTests` | `Engine_Dispose_StopsAndClears` | `Sleep(100)` | 等记录已产生 |
| `AlarmEngineTests` | `Engine_MultipleAlarms_AllEvaluate` | `Sleep(200)` | 等 5 条全部 Active |
| `DataLoggerTests` | `Logger_StartStop_UpdatesSampleCount` | `Sleep(200)` | 等「≥1 个样本」→「再增 1 个」 |
| `Phase3IntegrationTests` | 4 个集成用例 | `Sleep(200)`×5 | 等报警 Active / Returned / 事件 |
| `VariableSimulatorTests` | `Simulator_ValuesUpdatedEvent_Fires` | `Sleep(100)` | 等事件标志 |
| `VariableSimulatorTests` | `Simulator_TickCount_IncreasesWhileRunning` | `Sleep(150)` | 等 `TickCount >= 3` |

`VariableSimulatorTests` 在另一个测试项目、且无共享测试支持文件，故用内联
`SpinWait.SpinUntil` + 一行注释说明同因，不新建基础设施。

### 难点：如何断言「某件事**没有**发生」

`Engine_DelayedTrigger_OnlyFiresAfterDelay`（`DelayMs = 200`）需要断言
「延迟内不应触发」。轮询等不到「没发生」，因为**没有任何可观测状态能证明评估已经跑过一轮**。

解法：加一个**探针报警** —— 同一变量（`var-z`）、同一阈值、但**无延迟**的兄弟报警。

```csharp
// 探针出现 ⇒ 引擎至少完成过一轮评估，且延迟报警的计起点已在该轮建立
TestWait.Until(() => engine.GetActiveAlarms().Any(r => r.AlarmName == "延迟探针"), ...);
// 此刻延迟尚未到点（同一轮 EvaluateAll 里两者一起评估）
Assert.DoesNotContain(engine.GetActiveAlarms(), r => r.AlarmName == "延迟报警");
// 延迟过后应触发
TestWait.Until(() => engine.GetActiveAlarms().Any(r => r.AlarmName == "延迟报警"), ...);
```

为什么这样仍然有敏感度（不会被「延迟逻辑被删掉」蒙过去）：探针与延迟报警在
**同一轮 `EvaluateAll`** 里被评估，延迟报警的 `_conditionMetSince` 在那一轮才建立；
若 `DelayMs` 被忽略，两个报警会在同一轮一起出现 → `DoesNotContain` 立刻红。

对比一个看似更简单、实则**丧失敏感度**的写法：

```csharp
// ❌ 不要这样
var sw = Stopwatch.StartNew();
Assert.True(SpinWait.SpinUntil(() => AnyAlarm(), 5000));
Assert.True(sw.ElapsedMilliseconds >= 200);   // 线程池饿死时首轮评估就在 500ms → 恒真
```

## 自测（守卫自己必须会失败）

`tests/HmiEditor.Core.Tests/TestWaitTests.cs`：

| 用例 | 断言什么 |
|---|---|
| `Until_KeepsWaiting_ForConditionThatBecomesTrueLater` | 条件 300ms 后才成立 → `Until` 仍通过（证明它真的会等，而不只是立刻取一次布尔值） |
| `Until_ThrowsOnTimeout_InsteadOfSilentlyPassing` | 条件恒假 → 抛 `TrueException`，且消息含「在等什么」（证明它不是恒真的摆设） |

## 未开工（同类但需产品侧改动）

这些断言依赖「经过的真实时间量」，单纯轮询解决不了，需要给被测对象注入时钟
（`TimeProvider` 或手动 `Tick()` 入口）：

| 位置 | 为什么不能靠轮询 |
|---|---|
| `VariableSimulatorTests.Simulator_ClockMultiplier_SpeedsUpSimulatedTime` | 断言 `InRange(ElapsedSeconds, 0.3, 0.7)`，而 `VariableSimulator.ElapsedSeconds => (DateTime.UtcNow - _startTime).TotalSeconds * ClockMultiplier` 是**墙上时钟** |
| `VariableSimulatorTests.Simulator_ClockMultiplier_DeceleratesSimulatedTime` | 同上，断言 `InRange(0.02, 0.11)` |
| `VariableSimulatorTests.Simulator_LastTickElapsedMs_IsMeasured` | 断言恒真（`>= 0`），无害但也无覆盖 |

**建议**：`VariableSimulator` 增加可注入的 `TimeProvider`（.NET 8+ 内置抽象），
`ElapsedSeconds` 改走它；测试用 `FakeTimeProvider` 推进时间，彻底与调度无关。
属独立议题（改产品代码），不在本次修复内。

### 已复核、确认无需改动的其余 `Thread.Sleep`

| 位置 | 判断 |
|---|---|
| `PreviewRuntimeServiceTests.cs:375` | 已是「轮询直到截止时间」写法，本身确定性 |
| `GitWorkflowServiceTests.cs:80` | `Directory.Delete` 失败重试的退避，故意 sleep |
| `AuthenticationTests.cs:383` | `Sleep(10)` 仅用于让两条审计日志时间戳可区分，断言是 `NotEmpty`，与时序无关 |

## 验证

修复后连续三轮全量跑（`env TMPDIR=<仓库外磁盘目录> dotnet test HmiEditor.slnx -c Release`）：

| 轮次 | Core.Tests | 其余 6 项目 | 失败 |
|---|---|---|---|
| 1 | 1370 ✅ | 全绿 | **0** |
| 2 | 1372 ✅ | 全绿 | **0** |
| 3 | 1372 ✅ | 全绿 | **0** |

> 第 1 轮 1370 项是因为该轮的编译早于 `TestWaitTests.cs` 落盘；
> 第 2/3 轮的 1372 = 1370 + 新增自测 2 项，两项均通过。

**判定对照**：

| | 修复前 | 修复后 |
|---|---|---|
| 连续 3 轮全量 | 3 次红，落在 **4 个不同用例** | **0 次红** |

合计 7 项目 **1901 项 0 失败**。Release 构建 0 警告 0 错误。

## 教训

1. **`Thread.Sleep` 等定时器回调是一种「时序断言伪装成行为断言」**。它测的是调度器，不是产品。
   凡是能看到「可观测的产品状态」的地方，就该等状态。
2. **同一失败根因会落在不同用例上**，所以「换个用例又红了」不是新问题 —— 要按失败**机制**
   聚类，而不是按失败**用例**逐个打补丁。
3. **负向断言（等「没发生」）比正向断言难做确定性**。要么用**探针**制造同步点，
   要么老实用真实的墙上时间并接受它测不出小改动 —— 不要用「等 5 秒后再断言没发生」蒙过去。
4. **写守卫的同时要写守卫的自测**，否则「永不失败的守卫」会被当成通过的证据。

---

## 补修：漏网的 3 处内联 `SpinWait` + 自测装置自身的 flaky（2026-09-15）

`9e52e62`（控件符号族收口）之后的验收里，**连撞两次同一个机制**，两次都被单独复跑"洗白"过：

| 轮 | 红项 | 单跑复现 |
|---|---|---|
| #18 | `AlarmEngineTests.Engine_TriggerThenReturn_CreatesReturnedRecord` | 该类 3/3 绿（380/331/379 ms） |
| 补修后第 1 轮 | `TestWaitTests.Until_KeepsWaiting_ForConditionThatBecomesTrueLater` | 该类 3/3 绿 |

第 2 条尤其值得记：**「用来证明 `TestWait.Until` 会等待」的那条装置，自身是 flaky 的。**
它的置位写成 `Task.Run(async () => { await Task.Delay(300); signal = true; })`，
而 `Task.Delay` 的续体同样排在 **ThreadPool** 上 —— 全量并行时 7 个程序集同抢它，
300ms 的延迟被推迟到 5s 超时之外。

> 旁证：那一轮 `Core.Tests` 耗时 **1m14s**，而正常轮次 41–45s —— 多出来的正是超时等待。

### 按机制重扫（不是再看用例名）

```bash
grep -rn "SpinWait.SpinUntil" tests/                            # 硬编码内联等待
grep -rn "Task\.Delay\|Task\.Run\(async\|new Thread\(" tests/   # ThreadPool 续体
```

### 处置表

| 位置 | 判定 | 处置 |
|---|---|---|
| `Support/TestWait.cs:42` | `TestWait.Until` 自身的实现 | 不动 |
| `AlarmEngineTests.cs:285 / 289 / 397` | **1000ms + 无失败消息**（同文件另 9 处早已走 `TestWait.Until`） | **改** → `TestWait.Until`（带「在等什么」） |
| `TestWaitTests.cs:19` | 自测装置用 `Task.Delay` 置位 → 自身 ThreadPool 敏感 | **改** → 专用 `Thread` 置位；并**新增**一条零时序依赖用例 |
| `VariableSimulatorTests.cs:163 / 231` | 5000ms **且**有失败消息，注释已写明"等产品状态、不等固定时长" | 不改 |
| `DeviceChannelTests.cs:146` | `Task.WhenAny(closeTask, Task.Delay(2000))` —— 带兜底超时的**完成等待**（任务完成即返回，不等满） | 不改 |
| `UiOperationGuardTests.cs:235` | `for + await Task.Delay(10)` —— **async 轮询**等状态 | **不改**，见下 |
| `AgentEngineTests.cs:69` | `await Task.Delay(30s, ct)` —— 模拟长耗时操作以配合取消测试，不是等条件 | 不改 |
| `Modbus{Tcp,Rtu}RuntimeDriverTests` 若干 | 制造并发／延迟流量 | 不改 |

两条"不改"的理由必须写明，避免下一个人"顺手统一"：

- **`VariableSimulatorTests`**：它属于 `HmiEditor.Simulator.Tests`，而 `TestWait` 在
  `HmiEditor.Core.Tests` 命名空间 —— **跨项目不可见**。要统一得先把 `TestWait` 搬进
  `tests/HmiEditor.TestSupport/`（独立议题，改动面更大）。而它写法本身已满足纪律
  （5000ms + 失败消息 + 注释记录同因），不构成 flaky 风险。**无收益的统一只会扩大改动面。**
- **`UiOperationGuardTests`**：这是 `async` 测试里的轮询，**不能**换成 `TestWait.Until` ——
  后者是同步阻塞（`SpinWait.SpinUntil`），在 async 上下文里会占住一个线程池线程，
  并行跑时**反而加剧** ThreadPool 饥饿，且有死锁风险。
  **async 上下文的正确写法就是 `for + await Task.Delay`。**

### 改造内容

`AlarmEngineTests`（3 处）：

```csharp
// 前：Assert.True(SpinWait.SpinUntil(() => engine.GetAllRecords().Any(...), 1000));
// 后：
TestWait.Until(() => engine.GetAllRecords().Any(r => r.AlarmName == "快速报警"),
    "var-x=100 超过阈值 50 后应产生报警记录");
```

`TestWaitTests` —— 装置改用专用线程，并补一条**机制**用例：

```csharp
var setter = new Thread(() => { Thread.Sleep(300); signal = true; }) { IsBackground = true };
setter.Start();
TestWait.Until(() => signal, "条件应在超时前成立");
setter.Join();

// 新增：零时序依赖地证明「condition 被反复调用」（固定 Sleep 写法在这种实现下必然红）
var calls = 0;
TestWait.Until(() => ++calls >= 3, "第 3 次轮询时应成立");
```

两条互补：一条证「**能**等到」，一条证「**为什么**能」（机制证据，且不可能 flaky）。

### 验收

| 项 | 结果 |
|---|---|
| `AlarmEngineTests` 单跑 | 27/27 绿（332 ms） |
| `TestWaitTests` 单跑 | 3/3 绿（410 ms） |
| 全量连续 3 轮 | **3 轮均 2092/2092 全绿**（Core 1563 / AI 237 / App.UiTests 91 / Comm 75 / Device 63 / Simulator 41 / Hardware 22），`[FAIL]` 计数均为 0 |

Core 从 1562 → 1563 是新增的 `Until_PollsConditionRepeatedly_NotJustOnce`。

**判定对照**：

| | 补修前 | 补修后 |
|---|---|---|
| `AlarmEngineTests` 内联 `SpinWait` 且 1000ms | **3 处** | 0 处 |
| 自测装置依赖 ThreadPool | 是（`Task.Delay`） | 否（专用 `Thread`） |
| 全量并行偶发红 | 实测有（#18 轮、补修前第 1 轮） | 连续 3 轮 0 红 |

### 再记三条教训

1. **「这个文件里已经有正确写法」≠「这个文件里全都是正确写法」。**
   `TestWait.cs` 的注释当时已记录「实测 `AlarmEngineTests` 轮番红过 4 个不同用例」，
   但那是按**红过的用例**列的清单，漏掉了一个"当时还没红过"的。
   正确做法是按**写法特征**扫（`grep SpinWait.SpinUntil`），而不是按**失败记录**扫。
2. **用来证明「守卫会等待」的装置，本身不能依赖被验证的那套调度机制。**
   自测装置若也吃 ThreadPool，就会出现"平时全绿、并行时红"的怪象 ——
   而它是**最不该 flaky 的文件**（用例名字就叫 `Until_KeepsWaiting_…`）。
3. **`async` 测试与 `sync` 测试的等待方案不同，不能互相套用。**
   sync 用基于 `SpinWait` 的 `TestWait.Until`；async 用 `for + await Task.Delay`。
   把后者换成前者会引入死锁风险。

---

## 收尾：阻断项 9 关闭（2026-09-16）

`docs/project-status.md` 当前阻断项 9 登记的两条间歇失败在本日**关闭** —— 两条根因在本仓库里**都已经修了**，
只是状态文档没跟上：

| 用例 | 根因 | 修法 |
|---|---|---|
| `AlarmEngineTests.Engine_ClearReturnedRecords_RemovesOnlyReturned` | 固定 `Thread.Sleep(100)` 等**定时器回调**（回调排 ThreadPool ⇒ 全量并行时延迟可为间隔的任意倍） | `TestWait.Until` 等**产品状态**；该类现 13 处 `TestWait.Until` / 0 处 `Thread.Sleep` |
| `MockImageLoadingTests.GeneratedImagesC_WindowsPath_ConvertedToUnix` | 本机 `/tmp` 仅 10 MB tmpfs，全量并行写满 | `TestTempDir` 统一收口（回退 `.hmi-test-tmp/`）；该用例本身**纯同步**，无时序依赖 |

**判据（不接受「读代码看起来修了」）**：以默认并行度连跑 **6 次全量 ⇒ 6/6 全绿（每次 2248 通过 / 0 失败）**。
顺带把本文件与其它文档登记过的定时器类 flaky 一并带进 soak（`DataLoggerTests.Logger_StartStop_UpdatesSampleCount`、
`AlarmEngineTests.Engine_DelayedTrigger_OnlyFiresAfterDelay`、`BuildPipelineServiceTests.CompileAsync_…`、
`DeviceChannelTests.ConcurrentCommands_…`），**零复现**。

> 教训：**登记表会滞后于修复。** 本文 2026-09-15 的「补修」一轮已经清掉 3 处内联 `SpinWait`，
> 但 `project-status.md` 的「根因尚未定位」一直挂到 09-16。⇒ **修完 flaky 要回头扫一遍"登记它的文档"**，
> 否则下次有人会因为一句过期的「不稳定」而不敢信任全量绿。

---

## 补记（2026-09-27，阶段 116）：这条用例又红了，但**不是**本文那一档

`AlarmEngineTests.Engine_TriggerThenReturn_CreatesReturnedRecord` 在阶段 116 的回填批门禁里红了一次
（`.workbuddy/tmp/s116-gate4.log`），`HmiEditor.Core.Tests` 整程序集复跑 12 次又红一次（`s116-soak-r2.log`）。
**形状与本文前面两处（2026-09-14 / 09-15）处理的那一档不同**，所以单独记 —— 免得下一个人拿本文上半篇的
结论去套：

| | 本文那一档（`Thread.Sleep` 等回调 / 内联 `SpinWait` 1000ms） | 2026-09-27 这一档 |
|---|---|---|
| 红在哪 | `TestWait.Until` / `SpinWait.SpinUntil` **那一行** | **`:298` 的断言** |
| 失败消息 | `等待超时（5000ms）：…` | `Assert.Equal() Failure: Expected: Returned / Actual: Active` |
| 用例耗时 | 显著偏长（吃满超时窗口） | **正常**（161 / 566 / 267 ms） |
| 归因 | 回调排 ThreadPool ⇒ 等的时间不够 | **未定**：`:290` 的 `Until` **已过** ⇒ 「看到 Returned」之后记录又成了 Active |

**已否掉的一条**：临时探针把同一序列连跑 **300 轮**（`ZzProbeS116`，取证完即删，`s116-probe1.log`）
实测 **重复记录 0 次 / 单轮最大记录数 1** ⇒ 不是「无重入保护 ⇒ 同一配置两条 Active 记录」。
**仍开着的一条（代码可达、未直接取证）**：`EvaluateAll` 入口把 `_lastVariableValues` **拷成快照**
（`AlarmEngine.cs:233`），该次评估若在「值已改回 0」之后才走到 `EvaluateCondition`，仍按旧值判 ⇒
走 `AlarmEngine.cs:304-311` 把**已 Returned 的记录翻回 Active**，与 `:298` 的读数吻合。
三条红的时间戳都落在**进程启动 6.6–10 s** 窗口（正常整程序集跑 17–23 s）⇒ 怀疑与**线程池爬坡**同相。

⚠ **处置纪律照旧**：单跑绿**不算**洗白 —— 本文 2026-09-15 那一节就是为**同一条用例**写的。
完整登记见 `docs/2026-09-17-open-items-register.md` 第 **70** 项。

---

## 收口（2026-09-27，阶段 117）：这一档已确定性钉死并修掉 —— 且**更正了上面猜的通路**

上面那节结尾写下的「仍开着的一条（代码可达、未直接取证）」**已经取证并修掉了**；
取证结果同时**更正了那一段猜的通路**：

| | 阶段 116 那节猜的 | 阶段 117 实测 |
|---|---|---|
| 记录怎么变的 | `AlarmEngine.cs:304-311` 把**已 Returned 的那条翻回** `Active` | **新建一条** `Active` —— 记录数 **1 → 2** |
| `304-311` 那支 | 「翻回」的落点 | **死支**：它上面的查询条件 `r.State != AlarmState.Returned` 已把 Returned 排除 ⇒ 永不可达 |

**确定性取证**（关键：**不靠时序运气**）—— 反射直调 private `EvaluateCondition`，自己喂「持旧值」的输入：

| 步骤 | 记录数 | `First()` 取到 |
|---|---|---|
| ① 值＝100 评估一次 | 1 | `Active` |
| ② 值＝0 评估一次 | 1 | `Returned`（同一条） |
| ③ **又持旧值 100** | **2** | **`Active`（新建的）** ← 与 `:298` 的 `Expected: Returned / Actual: Active` 一字不差 |

另：连喂 5 次「持旧值」只留 **1** 条 ⇒ 新建的**必要条件是「已 Returned ＋ 持旧值」同时在场**。

**修法**：`EvaluateAll` 不再抓入口快照；`EvaluateCondition` **就地读** `_lastVariableValues`（`lock` 内）。
候选里的「`Interlocked.CompareExchange` 重入保护」**刻意不加** —— 探针证实它不是本档成因，不为「顺手」扩大改动面。

**判据**：`AlarmEngineStaleValueGuardTests`（三条腿，其中前两条「不许有」、
第三条「必须有」是它们的**反向配重** —— 删掉就地读会让前两条更绿）＋ **内嵌负控 3 支 / 正控 1 支全合成**
＋ **生产负控**（缺陷态注回真文件 ⇒ 判据红、三条腿逐条点名、还原 SHA 与 `git diff` 指纹双双回到注入前）。

⚠ **本文的处置纪律仍然成立**：单跑绿不算洗白。所以本阶段「修好了」**不靠**统计跑数 ——
靠那条把「时序」变成「输入」的**确定性**读数；修后的 **12 跑配对读数**（`s117-soak-summary.txt`：**12/12 绿**，每跑 `2136 / 2136`）只用来证明**没引入新红**。
