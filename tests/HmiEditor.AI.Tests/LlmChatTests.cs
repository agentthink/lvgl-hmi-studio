using HmiEditor.AI.Inference;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// <see cref="LlmChat.WithProgressAsync"/> 的**不变量**测试：
/// 只要传了增量回调，无论 Provider 支不支持流式，都至少收到一次回调；
/// 不传回调时不应强制走流式（老 Provider 与老调用方的行为必须原样保留）。
/// </summary>
public class LlmChatTests
{
    private static LlmRequest Request() => new()
    {
        Messages = new List<LlmMessage> { new() { Role = LlmRoles.User, Content = "hi" } }
    };

    [Fact]
    public async Task WithProgressAsync_StreamingProvider_ForwardsEachDelta()
    {
        var provider = new StreamingFake();
        var deltas = new List<string>();

        var result = await LlmChat.WithProgressAsync(provider, Request(), new ListProgress(deltas));

        Assert.Equal(new[] { "A", "B" }, deltas);
        Assert.Equal("AB", result.Content);
        Assert.Equal(1, provider.ChatStreamAsyncCalls);
        Assert.Equal(0, provider.ChatAsyncCalls);
    }

    [Fact]
    public async Task WithProgressAsync_NonStreamingProvider_ReportsFullContentOnce()
    {
        // 不变量：非流式 Provider 也必须回调完整正文，
        // 否则「运行中气泡」在真流式/假流式之间行为不一致，UI 得写两套。
        var provider = new PlainFake();
        var deltas = new List<string>();

        var result = await LlmChat.WithProgressAsync(provider, Request(), new ListProgress(deltas));

        Assert.Equal(new[] { "FULL" }, deltas);
        Assert.Equal("FULL", result.Content);
    }

    [Fact]
    public async Task WithProgressAsync_NoProgressObserver_UsesChatAsyncEvenOnStreamingProvider()
    {
        // 没人观察增量时不该走 SSE：多一层解析只在需要时才付
        var provider = new StreamingFake();

        var result = await LlmChat.WithProgressAsync(provider, Request(), textProgress: null);

        Assert.Equal("FULL", result.Content);
        Assert.Equal(1, provider.ChatAsyncCalls);
        Assert.Equal(0, provider.ChatStreamAsyncCalls);
    }

    [Fact]
    public async Task WithProgressAsync_NonStreamingProvider_EmptyContent_ReportsNothing()
    {
        // 空正文不该产生空回调（会让 UI 空刷新，也会让"已接收 0 字"的进度条闪一下）
        var provider = new PlainFake { Content = "" };
        var deltas = new List<string>();

        await LlmChat.WithProgressAsync(provider, Request(), new ListProgress(deltas));

        Assert.Empty(deltas);
    }

    [Fact]
    public async Task WithProgressAsync_NullProvider_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            LlmChat.WithProgressAsync(null!, Request(), textProgress: null));
    }

    /// <summary>同步收集型 <see cref="IProgress{T}"/>（<c>Progress&lt;T&gt;</c> 会走同步上下文，测试里反而不可控）。</summary>
    private sealed class ListProgress : IProgress<string>
    {
        private readonly List<string> _sink;
        public ListProgress(List<string> sink) => _sink = sink;
        public void Report(string value) => _sink.Add(value);
    }

    private sealed class StreamingFake : IStreamingLlmProvider
    {
        public string ProviderName => "streaming-fake";
        public int ChatAsyncCalls { get; private set; }
        public int ChatStreamAsyncCalls { get; private set; }

        public Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default)
        {
            ChatAsyncCalls++;
            return Task.FromResult(new LlmResponse { Content = "FULL", InputTokens = 1, OutputTokens = 2 });
        }

        public Task<LlmResponse> ChatStreamAsync(LlmRequest request, Action<string> onDelta, CancellationToken ct = default)
        {
            ChatStreamAsyncCalls++;
            onDelta("A");
            onDelta("B");
            return Task.FromResult(new LlmResponse { Content = "AB", InputTokens = 3, OutputTokens = 4 });
        }
    }

    private sealed class PlainFake : ILlmProvider
    {
        public string ProviderName => "plain-fake";
        public string Content { get; init; } = "FULL";

        public Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default) =>
            Task.FromResult(new LlmResponse { Content = Content, InputTokens = 1, OutputTokens = 2 });
    }
}
