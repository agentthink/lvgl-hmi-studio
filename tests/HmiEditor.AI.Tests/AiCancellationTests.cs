using System.Net;
using System.Net.Http;
using HmiEditor.AI.Agent;
using HmiEditor.AI.Generators;
using HmiEditor.AI.Inference;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// AI 链路的「取消 / 超时」契约守卫。
///
/// <para><b>为什么需要它</b>：AI 层内部的 token 传递本来就是完整的
/// （<see cref="ILlmProvider.ChatAsync"/> → <c>SendAsync</c>，<c>AgentEngine</c> 每轮开头有检查点），
/// 但**没有任何测试**碰过取消与超时语义（<c>grep -n "Timeout" tests/</c> 此前零命中），
/// 唯一的取消测试只覆盖"工具内取消传播"。
/// 于是 App 层把所有 UI 入口的 token 都漏掉时（对话框、聊天面板、图标对话框），
/// 这些检查点全都变成不可达的装饰 —— 取消按钮点了没反应，却没有任何红灯。</para>
///
/// <para><b>本文件钉住四条语义</b>：
/// ① <c>ct</c> 取消必须原样以 <see cref="OperationCanceledException"/> 冒泡（不能被吞成超时/网络错误）；
/// ② HttpClient 超时（<c>ct</c> 未取消）映射成"请求超时"的 <see cref="LlmProviderException"/>；
/// ③ 超时**是可重试错误**（现状语义，有意钉住 —— 改它要有意识）；
/// ④ 退避等待期间取消必须立刻生效，不再发起后续尝试。</para>
///
/// <para>App 层"到底有没有把 token 传下来"由 <c>AiCancellationWiringTests</c> 守。</para>
/// </summary>
public class AiCancellationTests
{
    private static LlmProviderConfig Config(int timeoutSeconds = 60, int maxRetries = 0) => new()
    {
        Provider = "openai",
        ApiKey = "test",
        Model = "m",
        TimeoutSeconds = timeoutSeconds,
        MaxRetries = maxRetries,
    };

    /// <summary>不带 schema 的请求 ⇒ 走 SendOnceAsync 单一路径，避免降级逻辑干扰。</summary>
    private static LlmRequest NoSchemaRequest() => new()
    {
        Messages = new List<LlmMessage> { new() { Role = LlmRoles.User, Content = "hi" } },
    };

    /// <summary>可编程 handler：记录调用次数，行为由回调决定（可挂起、可返回状态码）。</summary>
    private class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) =>
            _responder = responder;

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return _responder(request, ct);
        }
    }

    /// <summary>一直挂着、只响应取消的 handler（模拟"服务端迟迟不回"）。</summary>
    private sealed class HangingHandler : Handler
    {
        public HangingHandler(TaskCompletionSource? entered = null)
            : base(async (_, ct) =>
            {
                entered?.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            })
        {
        }
    }

    /// <summary>同步 IProgress —— <see cref="Progress{T}"/> 会把回调丢到 ThreadPool，取消时机就不确定了。</summary>
    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _onReport;

        public SyncProgress(Action<T> onReport) => _onReport = onReport;

        public void Report(T value) => _onReport(value);
    }

    // ── ① 请求在途时取消 ──

    [Fact]
    public async Task ChatAsync_CancelledWhileRequestInFlight_ThrowsOperationCanceled_NotTimeout()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var provider = new OpenAIProvider(Config(), new HangingHandler(entered));

        using var cts = new CancellationTokenSource();
        var call = provider.ChatAsync(NoSchemaRequest(), cts.Token);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); // 确认真的进了 HTTP 调用
        cts.Cancel();

        // 关键：必须是 OCE 冒泡，而不是被 catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        // 之类逻辑吞成"超时/网络错误" —— 吞掉之后 UI 就没法区分"用户取消"与"失败"。
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    // ── ② HTTP 超时（ct 未取消）映射 ──

    [Fact]
    public async Task ChatAsync_HttpTimeout_MapsToTimeoutLlmProviderException()
    {
        using var provider = new OpenAIProvider(
            Config(timeoutSeconds: 1, maxRetries: 0), new HangingHandler());

        var ex = await Assert.ThrowsAsync<LlmProviderException>(() => provider.ChatAsync(NoSchemaRequest()));

        Assert.Contains("超时", ex.Message);
    }

    // ── ③ 超时是可重试错误（现状语义，有意钉住） ──

    [Fact]
    public async Task ChatAsync_Timeout_IsRetried_BecauseTimeoutCountsAsRetryable()
    {
        var handler = new HangingHandler();
        using var provider = new OpenAIProvider(Config(timeoutSeconds: 1, maxRetries: 1), handler);

        await Assert.ThrowsAsync<LlmProviderException>(() => provider.ChatAsync(NoSchemaRequest()));

        // 1 次原始 + 1 次重试 = 2 次请求。
        // 这条**不是**在说"重试超时是对的"，而是把现状钉住：
        // 单次逻辑调用的最坏耗时 = (MaxRetries + 1) × TimeoutSeconds + 退避，
        // 默认配置下 ≈ 3×60s —— 所以"60 秒超时"并不是用户实际等待的上限。
        Assert.Equal(2, handler.Calls);
    }

    // ── ④ 退避等待期间取消 ──

    [Fact]
    public async Task ChatAsync_CancelledDuringBackoff_StopsBeforeNextAttempt()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler((_, _) =>
        {
            entered.TrySetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom"),
            });
        });
        using var provider = new OpenAIProvider(Config(maxRetries: 5), handler);

        using var cts = new CancellationTokenSource();
        var call = provider.ChatAsync(NoSchemaRequest(), cts.Token);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); // 第一次尝试已返回 5xx
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.Equal(1, handler.Calls); // 退避里的 Task.Delay(backoff, ct) 立刻抛，不再打第二次
    }

    // ── ⑤ Agent 多轮循环的轮间检查点（此前只测过"工具内取消"） ──

    [Fact]
    public async Task AgentEngine_CancelledBetweenSteps_DoesNotStartNextLlmCall()
    {
        var provider = new FakeLlmProvider(_ =>
            """{"type":"tool_call","tool":"noop","arguments":{}}""");
        var tool = new AgentTool(
            "noop",
            "空操作（测试用）。",
            execute: (_, _, _) => Task.FromResult(AgentToolResult.Success("ok")));
        var engine = new AgentEngine(provider, new[] { tool },
            () => new AgentToolContext { Project = new HmiProject(), SessionId = "cancel-between" });

        using var cts = new CancellationTokenSource();
        // 第一步的进度一回来就取消 —— 同步 IProgress，保证取消一定发生在下一步之前。
        var progress = new SyncProgress<AgentRunStep>(_ => cts.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.RunAsync("执行任务", "cancel-between", stepProgress: progress, ct: cts.Token));

        Assert.Single(provider.ReceivedRequests); // 第 2 步的 LLM 调用从未发生
    }

    // ── ⑥ 批量图标：中途取消不再做后续条目（把不可达的检查点变可达） ──

    [Fact]
    public async Task BatchIconGenerator_CancelledAfterFirstItem_StopsBeforeSecond()
    {
        var provider = new FakeLlmProvider(_ =>
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 48 48\"><path d=\"M0 0\"/></svg>");
        var generator = new IconGenerator(provider, new PromptTemplateEngine());
        var batch = new BatchIconGenerator(generator);

        using var cts = new CancellationTokenSource();
        var events = 0;
        batch.ProgressChanged += (_, _) =>
        {
            // 第一个进度事件（第 0 项开始前）就取消
            if (++events == 1) cts.Cancel();
        };

        var items = new (string, string)[] { ("泵", "泵"), ("电机", "电机"), ("阀门", "阀门") };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            batch.GenerateBatchAsync(items, cts.Token));

        // 第 0 项已经进了生成（FakeLlmProvider 不理会 ct），第 1 项在循环开头的
        // ThrowIfCancellationRequested 处被拦住 ⇒ 恰好 1 次请求。
        Assert.Single(provider.ReceivedRequests);
    }
}
