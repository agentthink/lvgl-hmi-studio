# AI 生成取消链路：从"能编译、测得过、但按不了"到全链路带 token

> 2026-09-15（第九轮）。`MEMORY.md` §8「未开工」里登记为
> 「AI 助手 SSE 流式含取消链路缺口」。侦察后**把范围收窄**：
> SSE 流式（final_answer / 并行 tool_call）是**未实现的功能**，
> 而取消链路是**已实现但断在接缝上**的真缺陷 —— 先修后者。

## 1. 归因：`ct = default` 让"漏传 token"一路静默

整条 AI 层（`HmiEditor.AI`）的取消支持其实是**完整的**：
`AgentEngine` 轮间有检查点、`BatchIconGenerator` 每项开始前有检查点、
`LlmHttpProviderBase` 把取消与超时分开处理、`HttpClient` 请求也收 token。

断点在 **App 层的那道接缝**上，而这道接缝的结构让它**必然**断：

```csharp
// 修前：AiGenerateDialogBase
protected abstract Task<TResult?> GenerateAsync(string input, HmiProject project);
//                ↑ 签名里根本没有 CancellationToken ⇒ 2 个子类不可能传

// 修前：AiChatPanelView 四个分支
await _manager.GenerateTemplateAsync(input, CurrentProject);   // 用默认 token
```

三层缺陷叠在一起：

| 层 | 现象 | 为什么没人发现 |
|---|---|---|
| **类型系统** | `CancellationToken ct = default` 是**可选形参** | 漏传**编译通过**，0 警告 |
| **抽象签名** | 基类抽象方法`GenerateAsync` 没声明 token 形参 | 子类想传也没地方传 |
| **测试** | 无任何取消链路测试；`grep -n "Timeout" tests/HmiEditor.AI.Tests/` **零命中** | 行为测试不红 |

于是产生一条**在应用里永不可达的检查点**：
`BatchIconGenerator.GenerateBatchAsync` 里的 `ct.ThrowIfCancellationRequested()`，
其**唯一调用点**传的是默认 token（`CancellationToken.None`，`CanBeCanceled == false`）
⇒ 这行代码写完就从没执行过。与第八轮「判据错位 ⇒ 分支按构造不可达」同族：
**代码存在 ≠ 代码可达**。

### 1.1 用户能感知到的三个具体后果

1. **对话框的"取消"按钮只关窗，不停请求。** `CancelBtn.Click += (_, _) => Close(null);`
   ⇒ 窗口没了，在途 LLM 请求继续跑到超时（最坏 `(MaxRetries+1) × TimeoutSeconds`），
   完成后回调**继续写 `AiPreviewManager.Pending*`** ⇒ 可能覆盖掉后一次请求的结果。
2. **图标对话框 busy 时把取消按钮禁用了。** 两处 `CancelBtn.IsEnabled = false;`
   ⇒ 批量生成 20 个图标时用户**没有任何办法停**。
3. **聊天面板的取消按钮有时候什么也不做。** `_agentCts` 只用于 Agent 路径、
   非 Agent 四分支不传 token；且 `_agentCts` **从不 Dispose**（泄漏）。

### 1.2 顺带纠正两处记忆里的不准确记录

| 原记录 | 实际 |
|---|---|
| `OnAiCancelRequested` 是"中断在途请求的入口" | 那是 ActionBar 的**取消预览**按钮，与运行中的取消是**两条独立路径**；真正的中断入口是 `OnCancelAgentClicked` |
| `TimeoutSeconds=60` "对 Agent 偏短" | 60s 是**每次 HTTP 请求**的上限，且**超时算可重试错误** ⇒ 一次逻辑调用的最坏耗时 ≈ `(MaxRetries+1) × TimeoutSeconds` ≈ **3×60s**，针对的是**所有**调用而非只有 Agent |

## 2. 收口：六处改动

| # | 文件 | 改动 |
|---|---|---|
| 1 | `Controls/AiGenerateDialogBase.cs` | 抽象签名补 `CancellationToken ct`；新增 `_cts` / `InFlightToken` / `OnCancelClicked` / `CancelInFlight` / `OnClosed` 兜底 |
| 2 | `Views/AiScriptDialog.axaml.cs`、`AiTemplateDialog.axaml.cs` | `CancelBtn.Click` 由 `Close` 改接 `OnCancelClicked`；`GenerateAsync` 补形参并透传 |
| 3 | `Views/AiChatPanelView.axaml(.cs)` | 新增 `StopBtn`；删泄漏的 `_agentCts` 统一为 `_cts`；新增 `BeginRun`/`EndRun`/`OnStopClicked`；**四个非 Agent 分支全部补 token** |
| 4 | `Views/AiIconDialog.axaml.cs` | 新增 `BeginRun`/`OnEnd`/`OnCancelClicked`/`OnClosed`；删两处 `IsEnabled = false`；`GenerateBatchAsync(items, token)` 让不可达检查点变**可达** |
| 5 | `AI/Inference/LlmHttpProviderBase.cs` | 终端异常补根因（原"重试 N 次后仍失败"把"超时"吞掉） |
| 6 | 三处本地化 | 新增 `ai.cancelled` / `Lang.AiCancelled`（"已取消" / "Cancelled"） |

### 2.1 契约（写进 MEMORY §6）

- **生成入口必须传 token**：App 层 8 处 `Generate*Async` 调用点无一例外。
- **取消按钮在 busy 时 `Cancel()`，而非 `Close()`**：先停请求，再让用户自己关。
- **取消 ≠ 失败**：`OperationCanceledException` 必须与 `LlmProviderException` 分开 catch，
  文案走 `ai.cancelled` 而不是 `ai.gen.failed` —— 否则用户以为程序坏了。
- **`TimeoutSeconds` 是"每次请求"上限，超时还会重试**：不是用户等待上限。

### 2.2 三个实现细节（都是踩过的坑）

**① CTS 的释放要用局部引用收尾，不能靠字段。**

```csharp
finally
{
    // 用局部变量收尾：关窗兜底取消会把字段清掉，靠字段释放会漏（CTS 未被 Dispose）。
    if (ReferenceEquals(_cts, cts)) _cts = null;
    cts.Dispose();
    SetBusy(false);
}
```

首版让 `CancelInFlight()` 顺手把 `_cts` 置 null，`finally` 里的 `_cts?.Dispose()` 就漏了。
现约定：`CancelInFlight()` **只 Cancel()，不置空**；释放权归发起方（`finally` 的局部 `cts`）。

**② 图标批量取消时**不能假装"已生成的条目还在 `Results` 里"**：
`GenerateBatchAsync` 按 .NET 约定抛 OCE、不返回部分结果。

```csharp
catch (OperationCanceledException)
{
    // GenerateBatchAsync 按 .NET 约定抛 OCE、不返回部分结果，
    // 所以这里不能假装"已生成的在 Results 里"
    Results = new List<AssetItem>();
    StatusText.Text = AppLocalization.Service[Lang.AiCancelled];
    ResultText.Text = "";
}
```

（"保留已完成条目"需要给 `GenerateBatchAsync` 加 **partial-results 出口** —— 见 §6 遗留。）

**③ 测试要确定性取消时机，必须用同步 `IProgress<T>`。**
`Progress<T>` 把回调丢到 ThreadPool，取消时机就不可复现 ⇒ 引入 `SyncProgress<T>`（就地调用 `Report`）。

## 3. 守卫：三处，共 16 条

**分工原则**：**接线类**性质（"JSON / token 有没有传下去"）用**静态源码扫描**；
**运行时类**性质（"取消真的取消了哪个 token"）用**行为测试**。

| 文件 | 条数 | 覆盖什么 |
|---|---|---|
| `tests/HmiEditor.App.UiTests/AiCancellationWiringTests.cs` | 5 | 静态接线 |
| `tests/HmiEditor.App.UiTests/AiGenerateDialogCancellationUiTests.cs` | 5 | 对话框行为（headless） |
| `tests/HmiEditor.AI.Tests/AiCancellationTests.cs` | 6 | AI 层取消 / 超时语义 |

### 3.1 静态接线守卫（5）

| 用例 | 断言 |
|---|---|
| `EveryDialogSubclass_OverridesGenerateAsync_WithCancellationToken` | 每个子类重写的签名都带 token |
| `DialogBase_AbstractSignature_DeclaresCancellationToken` | 基类抽象方法声明了 token |
| `EveryGenerateCallSite_PassesCancellationToken` | 所有调用点都传 token |
| `NoCancelButton_IsWiredToCloseOnly` | 正则 `CancelBtn\.Click\s*\+=\s*[^;]*\bClose\s*\(` **零命中** |
| `Scanner_ActuallyCoversTheCancelPlumbingFiles` | 5 个相关文件必须在扫描面内 |

后两条是**守卫自身的守卫**：静态扫描最容易"扫了个空所以全绿"。
`EveryDialogSubclass…` 带扫描面自检（`>= 2`），`EveryGenerateCallSite…` 带自检（`>= 7` 处）。

### 3.2 对话框行为守卫（5）

内部 `ProbeDialog : AiGenerateDialogBase<string>` 记录收到的 token、`ClosedFired`、
`StatusText`、`IsBusy`，暴露 `ClickGenerate/ClickCancel/SimulateClosed`。

| 用例 | 断言 |
|---|---|
| `CancelWhileGenerating_CancelsTheTokenTheRequestIsUsing` | 取消后**该请求正在用的那个 token** 真被取消 |
| `CancelWhileGenerating_KeepsTheDialogOpen` | 对话框**不关**（用户自己关） |
| `CancelWhileGenerating_ShowsCancelled_NotGenerationFailed` | 文案是"已取消"而非"生成失败" |
| `CancelledRun_EndsBusyState_SoTheUserCanRetry` | 取消后能重试（`IsBusy == false`） |
| `ClosingTheDialogWhileGenerating_CancelsInFlightGeneration` | 关窗兜底取消在途请求 |

第 1、2 条**原本是一条**（"取消 token 且不关窗"），NV-a 只红 1 条暴露出精度问题 ——
**一条断言只验一件事**，拆开后两组 NV 各自只打中一条。

`DrainContinuations()` = 8×(`UiTestHost.Pump(1)` + `Dispatcher.UIThread.RunJobs()`)。

### 3.3 AI 层行为守卫（6）

| 用例 | 断言 |
|---|---|
| `ChatAsync_CancelledWhileRequestInFlight_ThrowsOperationCanceled_NotTimeout` | OCE 冒泡，**不被吞成"超时"** |
| `ChatAsync_HttpTimeout_MapsToTimeoutLlmProviderException` | 真超时映射为含"超时"的 `LlmProviderException` |
| `ChatAsync_Timeout_IsRetried_BecauseTimeoutCountsAsRetryable` | 钉住现状：超时走重试（2 次请求） |
| `ChatAsync_CancelledDuringBackoff_StopsBeforeNextAttempt` | 退避期取消 ⇒ `Calls == 1` |
| `AgentEngine_CancelledBetweenSteps_DoesNotStartNextLlmCall` | 轮间取消 ⇒ `Assert.Single(provider.ReceivedRequests)` |
| `BatchIconGenerator_CancelledAfterFirstItem_StopsBeforeSecond` | 每项检查点生效 ⇒ `Single(...)` |

设施：`Handler : HttpMessageHandler`（**非 sealed**，供 `HangingHandler` 继承）、
`HangingHandler`（请求永不返回，用来制造"在途"）、`SyncProgress<T>`。

## 4. 负向验证：7 组，红集两两不同

每组只做「**只改这一件事**的等价替换」，且注入**一定编得过**（编译红不是测试红）。

| 组 | 注入 | 红 |
|---|---|---|
| a | `OnCancelClicked` 退回「只 `Close`」= 本轮修掉的缺陷复现 | 1 |
| a2 | 取消按钮 busy 时**什么都不做**（把"取消请求"这个动作整个拿掉） | 3 |
| b | 非 Agent 四个分支退回不传 token | 1 |
| c | 超时不再算可重试（立即抛）—— 反方向改现状语义 | 1 |
| d | 删掉 Agent 多轮循环的**轮间检查点** | 1 |
| e | 删掉批量图标的**每项开始前**检查点 | 1 |
| f | 删掉**关窗兜底取消** | 1 |

- **a2 红 3 条**、其余各 1 条，且**命中的用例名两两不同** ⇒ 守卫覆盖的是多件独立的事，
  不是一个断言被反复触发。
- a / a2 的区别正说明了 §3.2 那次拆分：拆之前 a 与 a2 都只红 1 条且**红的是同一条**
  （"取消 token 且不关窗"全塞在一起）⇒ 无法区分"没取消 token"与"关了窗"。
- **NV-f 为何只有 1 条红（刻意）**：`CancelWhileGenerating_*` 依赖 `ProbeDialog.Close(null)`，
  而 `Close()` 在**未 Show 的 `Window`** 上会抛（历史上已实测）⇒ f 打的是刻意避开 `Close` 的
  `ClosingTheDialogWhileGenerating_...`。**1 条红符合预期**。

还原后 16 条全绿。

## 5. 验证

- `dotnet build`：**0 警告 0 错误**。
- `AiCancellationTests` **6/6**、`AiCancellationWiringTests` **5/5**、
  `AiGenerateDialogCancellationUiTests` **5/5**。
- 全量：**2199 通过 / 0 失败**（7 项目：44 + 22 + 75 + 63 + 272 + 1604 + 119；
  基线 2183，**+16**：`AI.Tests` 266 → 272、`App.UiTests` 109 → 119）。
- `scripts/ci-gate.sh` 5 步全过（**独占运行**，不与 NV 并行）。

## 6. 遗留

1. **批量图标"保留已生成条目"**：需要给 `BatchIconGenerator.GenerateBatchAsync` 加
   partial-results 出口（返回值或回调）。属**产品决策**（取消后要不要留半成品），未改。
2. **SSE 流式未实现**：final_answer 增量、并行 tool_call、流式下的取消（现有取消是"请求级"，
   流式需要"帧级"中断）。
3. **`hmi_actions.c` 权限/凭据校验**留待接入 `UserRole`（声明式延后）。
4. **`design-previews` 无共享模型**（各预览各写一套）。
5. **`TemplateGenerationSchema.category`** 用 enum 硬约束自由字段（AI 产出易被拒）。
6. **产品代码往系统临时目录写固定文件名** —— 本轮验证时**实测到的间歇失败**，根因已确证：

   `src/HmiEditor.Device/Updates/HmiDeployService.cs:124-125`

   ```csharp
   var tmpCfg = Path.Combine(Path.GetTempPath(), "zhishu_hmi_comm_config.ini");
   await File.WriteAllTextAsync(tmpCfg, runtimeConfigText, ct);
   ```

   **固定文件名 + 共享系统临时目录**，两个独立问题：
   - `/tmp`（本机 10 MB tmpfs）被占满时，写这一行直接抛
     `IOException: No space left on device` ⇒
     `HmiDeployServiceTests.DeployAsync_RuntimeConfigText_UploadsCommConfig` 间歇红；
   - 文件名固定 ⇒ 两个 IDE 实例同时下载会互相覆盖（且该文件**从不清理**）。

   **实测证据**（3 次全量跑中红 1 次，单独复跑全绿 ⇒ 符合"间歇失败"判据）：

   ```
   System.IO.IOException : No space left on device : '/tmp/zhishu_hmi_comm_config.ini'
     at HmiEditor.Device.Updates.HmiDeployService.DeployAsync ... HmiDeployService.cs:line 125
   ```

   **判据（可复现）**：填满 `/tmp`（10 MB）后单跑该用例**必红**，清空后必绿。

   **为什么长期没被发现**：`tests/HmiEditor.Core.Tests/TestTempDirUsageGuardTests.cs` 的扫描面是
   `FindTestsRoot()` ⇒ **只扫 `tests/`，不扫 `src/`**。它守的是"测试不得直接用系统临时目录"
   （实测历史上 14 文件 190 处绕过），而**产品侧**对系统临时目录的依赖落在扫描面之外 ——
   于是"测试的断言依赖产品往 `/tmp` 写文件"这件事，两侧守卫都没覆盖。

   环境侧的另一半：`firmware/t113/tests/run_host_tests.sh` 把 3 个 gcc 产物写到固定
   `/tmp/hmi_comm_test`、`/tmp/hmi_touch_scale_test`、`/tmp/hmi_board_profile_test`，
   且**从不清理** —— `ci-gate.sh` 第 4 步就跑它，紧接着的全量测试很容易踩到满 `/tmp`。

