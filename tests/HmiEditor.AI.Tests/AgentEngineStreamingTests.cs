using HmiEditor.AI.Agent;
using HmiEditor.AI.Agent.Tools;
using HmiEditor.AI.Inference;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// Agent 引擎的流式接线测试：Provider 支持流式时走 SSE 并把增量回推；
/// 不支持时（老 Provider / 测试替身）仍必须每个回合回调一次完整正文。
/// </summary>
public class AgentEngineStreamingTests
{
    private static AgentTool WriteTool() => new(
        "create_test_item",
        "创建一个测试条目（写操作）。",
        execute: (_, ctx, _) =>
        {
            ctx.Project.Name = "changed-by-agent";
            return Task.FromResult(AgentToolResult.Success("已创建测试条目", true));
        });

    private static AgentEngine BuildEngine(ILlmProvider provider, HmiProject project) =>
        new(provider, new[] { WriteTool() },
            () => new AgentToolContext { Project = project, SessionId = "s" });

    [Fact]
    public async Task RunAsync_StreamingProvider_UsesStreaming_AndReportsDeltasInOrder()
    {
        var provider = new ScriptedStreamingProvider(
            """{"type":"tool_call","tool":"create_test_item","arguments":{}}""",
            """{"type":"final_answer","text":"完成"}""");
        var engine = BuildEngine(provider, new HmiProject());

        var deltas = new List<string>();
        var result = await engine.RunAsync("任务", "s", textProgress: new ListProgress(deltas));

        Assert.True(result.Completed);
        Assert.Equal("完成", result.FinalAnswer);
        // 两轮各走一次流式；一次都不该退回非流式
        Assert.Equal(2, provider.StreamCalls);
        Assert.Equal(0, provider.ChatAsyncCalls);
        // 增量按到达顺序拼接 = 两轮模型输出（逐字符回调）
        Assert.Equal(string.Concat(provider.AllResponses), string.Concat(deltas));
    }

    [Fact]
    public async Task RunAsync_NonStreamingProvider_StillReportsOneFullCallbackPerTurn()
    {
        // 不变量：老 Provider（含 FakeLlmProvider）也必须产生回调，
        // 否则 UI 的"运行中"预览在两个 Provider 之间行为不一致。
        var provider = new FakeLlmProvider(_ => """{"type":"final_answer","text":"完成"}""");
        var engine = BuildEngine(provider, new HmiProject());

        var deltas = new List<string>();
        var result = await engine.RunAsync("任务", "s", textProgress: new ListProgress(deltas));

        Assert.True(result.Completed);
        Assert.Single(deltas);
        Assert.Contains("完成", deltas[0]);
    }

    [Fact]
    public async Task RunAsync_NoTextProgressObserver_UsesNonStreamingPath()
    {
        // 没有增量观察者时不走 SSE（见 LlmChat 的不变量测试）——
        // 但 Provider 的 ChatAsync 仍必须能正常回答，否则等于"有没有人看"决定了任务能不能跑
        var provider = new ScriptedStreamingProvider("""{"type":"final_answer","text":"ok"}""");
        var engine = BuildEngine(provider, new HmiProject());

        var result = await engine.RunAsync("任务", "s");

        Assert.True(result.Completed);
        Assert.Equal("ok", result.FinalAnswer);
        Assert.Equal(1, provider.ChatAsyncCalls);
        Assert.Equal(0, provider.StreamCalls);
    }

    [Fact]
    public async Task RunAsync_StreamingFailure_SurfacesAsRunFailureNotSilentEmpty()
    {
        // 流式抛错不能被吞成"空回答"：否则用户看到的是"任务已完成"而项目什么都没变
        var provider = new ScriptedStreamingProvider { ThrowOnStream = new LlmProviderException("流式中断") };
        var engine = BuildEngine(provider, new HmiProject());

        var ex = await Assert.ThrowsAsync<LlmProviderException>(() =>
            engine.RunAsync("任务", "s", textProgress: new ListProgress(new List<string>())));

        Assert.Contains("流式中断", ex.Message);
    }

    private sealed class ListProgress : IProgress<string>
    {
        private readonly List<string> _sink;
        public ListProgress(List<string> sink) => _sink = sink;
        public void Report(string value) => _sink.Add(value);
    }

    /// <summary>按脚本逐轮回答、并把每个字符当一次增量的流式 Provider。</summary>
    private sealed class ScriptedStreamingProvider : IStreamingLlmProvider
    {
        private readonly Queue<string> _responses;

        public ScriptedStreamingProvider(params string[] responses) => _responses = new(responses);

        public string ProviderName => "scripted-streaming";
        public int StreamCalls { get; private set; }
        public int ChatAsyncCalls { get; private set; }
        public LlmProviderException? ThrowOnStream { get; init; }
        public List<string> AllResponses { get; } = new();

        public Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default)
        {
            ChatAsyncCalls++;
            if (ThrowOnStream is not null) throw ThrowOnStream;

            var content = NextResponse();
            AllResponses.Add(content);
            return Task.FromResult(new LlmResponse { Content = content, InputTokens = 1, OutputTokens = 1 });
        }

        public Task<LlmResponse> ChatStreamAsync(LlmRequest request, Action<string> onDelta, CancellationToken ct = default)
        {
            StreamCalls++;
            if (ThrowOnStream is not null) throw ThrowOnStream;

            var content = NextResponse();
            AllResponses.Add(content);
            foreach (var ch in content) onDelta(ch.ToString());
            return Task.FromResult(new LlmResponse { Content = content, InputTokens = 1, OutputTokens = 1 });
        }

        private string NextResponse() =>
            _responses.Count > 0 ? _responses.Dequeue() : """{"type":"final_answer","text":"兜底"}""";
    }
}
