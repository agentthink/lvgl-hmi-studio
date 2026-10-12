using System.Net;
using System.Net.Http;
using System.Text;
using HmiEditor.AI.Inference;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// Provider 流式 (SSE) 行为测试 —— OpenAI 与 Claude 两条协议各覆盖：增量顺序与拼接、
/// token 用量、请求体真的带 stream 标志、结构模式降级不重复吐字、以及取消传播。
/// 全部用注入的 <see cref="HttpMessageHandler"/>，不触网。
/// </summary>
public class ProviderStreamingTests
{
    // ────────────────────────── OpenAI ──────────────────────────

    private static LlmRequest OpenAiRequest(string? schema = null) => new()
    {
        Messages = new List<LlmMessage> { new() { Role = LlmRoles.User, Content = "hi" } },
        JsonSchema = schema
    };

    private static HttpResponseMessage Sse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task OpenAi_ChatStreamAsync_AccumulatesDeltasInOrder_AndParsesUsage()
    {
        const string sse =
            "data: {\"choices\":[{\"delta\":{\"content\":\"{\\\"screen\\\":\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"{\\\"name\\\":\\\"m1\\\"}}\"}}]}\n\n" +
            "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":7}}\n\n" +
            "data: [DONE]\n\n";

        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "k", Model = "m" },
            new StubHandler(_ => Sse(sse)));

        var deltas = new List<string>();
        var result = await provider.ChatStreamAsync(OpenAiRequest(), deltas.Add);

        Assert.Equal(2, deltas.Count);
        Assert.Equal("{\"screen\":", deltas[0]);
        Assert.Equal("{\"name\":\"m1\"}}", deltas[1]);
        // 增量拼接必须等于最终正文；否则「边看边长、跑完变样」是最难查的一类缺陷
        Assert.Equal(string.Concat(deltas), result.Content);
        Assert.Equal(12, result.InputTokens);
        Assert.Equal(7, result.OutputTokens);
    }

    [Fact]
    public async Task OpenAi_ChatStreamAsync_SendsStreamFlagAndUsageOption()
    {
        string? body = null;
        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "k", Model = "m" },
            new StubHandler(req =>
            {
                body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return Sse("data: [DONE]\n\n");
            }));

        await provider.ChatStreamAsync(OpenAiRequest(), _ => { });

        Assert.NotNull(body);
        Assert.Contains("\"stream\":true", body);
        // 不带 stream_options.include_usage 就拿不到 token 用量，UI 上的消耗读数会凭空消失
        Assert.Contains("include_usage", body);
        Assert.True(body!.Contains("true", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenAi_ChatStreamAsync_JsonSchemaRejected400_DegradesWithoutDuplicatingDeltas()
    {
        var requests = 0;
        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "k", Model = "m", MaxRetries = 0 },
            new StubHandler(_ =>
            {
                requests++;
                // 第 1 次 (strict json_schema) 被中转拒绝；第 2 次 (json_object) 成功
                return requests == 1
                    ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"no strict\"}") }
                    : Sse("data: {\"choices\":[{\"delta\":{\"content\":\"ok\"}}]}\n\ndata: [DONE]\n\n");
            }));

        var deltas = new List<string>();
        var result = await provider.ChatStreamAsync(OpenAiRequest(TestSchema), deltas.Add);

        Assert.Equal(2, requests);
        // 首轮一个字节都没出去，所以降级后只应看到一次增量
        Assert.Equal(new[] { "ok" }, deltas);
        Assert.Equal("ok", result.Content);
    }

    [Fact]
    public async Task OpenAi_ChatStreamAsync_AllFormatsRejected_FallsBackToNonStreaming()
    {
        var streamAttempts = 0;
        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "k", Model = "m", MaxRetries = 0 },
            new StubHandler(req =>
            {
                var isStream = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult()
                    .Contains("\"stream\":true", StringComparison.Ordinal);

                if (isStream)
                {
                    streamAttempts++;
                    return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"stream unsupported\"}") };
                }

                // 非流式回退成功
                return Json("""{"choices":[{"message":{"content":"非流式回退正文"}}],"usage":{"prompt_tokens":3,"completion_tokens":2}}""");
            }));

        var deltas = new List<string>();
        var result = await provider.ChatStreamAsync(OpenAiRequest(TestSchema), deltas.Add);

        // 三种结构模式各试一次流式
        Assert.Equal(3, streamAttempts);
        // 回退路径也必须产生一次回调，否则调用方得为「无增量」再写一套 UI 逻辑
        Assert.Equal(new[] { "非流式回退正文" }, deltas);
        Assert.Equal("非流式回退正文", result.Content);
    }

    [Fact]
    public async Task OpenAi_ChatStreamAsync_MalformedFrame_IsSkippedNotFatal()
    {
        // 中转插入的心跳/坏帧不应让整次生成失败
        const string sse =
            "data: not-json\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"keep\"}}]}\n\n" +
            "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1}}\n\n";

        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "k", Model = "m" },
            new StubHandler(_ => Sse(sse)));

        var deltas = new List<string>();
        var result = await provider.ChatStreamAsync(OpenAiRequest(), deltas.Add);

        Assert.Equal(new[] { "keep" }, deltas);
        Assert.Equal(1, result.InputTokens);
    }

    [Fact]
    public async Task OpenAi_ChatStreamAsync_EmptyContentDeltas_AreNotReported()
    {
        // 首帧常带 role 而 content 为 null/空；回调空串会让 UI 反复空刷新
        const string sse =
            "data: {\"choices\":[{\"delta\":{\"role\":\"assistant\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"最后\"}}]}\n\n";

        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "k", Model = "m" },
            new StubHandler(_ => Sse(sse)));

        var deltas = new List<string>();
        var result = await provider.ChatStreamAsync(OpenAiRequest(), deltas.Add);

        Assert.Equal(new[] { "最后" }, deltas);
        Assert.Equal("最后", result.Content);
    }

    [Fact]
    public async Task OpenAi_ChatStreamAsync_Cancellation_PropagatesAsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        var content = new GatedSseContent("data: {\"choices\":[{\"delta\":{\"content\":\"a\"}}]}\n\n");

        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "k", Model = "m" },
            new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        // 收到第一个增量就取消；后续读取阻塞在网关上 ⇒ 必须抛 OperationCanceledException
        // （而不是被包装成 LlmProviderException，否则 UI 无法区分"用户取消"与"设备无响应"）
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.ChatStreamAsync(OpenAiRequest(), _ => cts.Cancel(), cts.Token));

        Assert.NotNull(ex);
        content.Release();
    }

    // ────────────────────────── Claude ──────────────────────────

    private static HttpResponseMessage ClaudeSse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") };

    [Fact]
    public async Task Claude_ChatStreamAsync_AccumulatesTextDeltas_AndParsesUsage()
    {
        const string sse =
            "event: message_start\n" +
            "data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":9,\"output_tokens\":1}}}\n\n" +
            "event: content_block_delta\n" +
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"你好\"}}\n\n" +
            "event: content_block_delta\n" +
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"，世界\"}}\n\n" +
            "event: message_delta\n" +
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":42}}\n\n" +
            "event: message_stop\n" +
            "data: {\"type\":\"message_stop\"}\n\n";

        var provider = new ClaudeProvider(
            new LlmProviderConfig { Provider = "claude", ApiKey = "k", Model = "m" },
            new StubHandler(_ => ClaudeSse(sse)));

        var deltas = new List<string>();
        var result = await provider.ChatStreamAsync(OpenAiRequest(), deltas.Add);

        Assert.Equal(new[] { "你好", "，世界" }, deltas);
        Assert.Equal("你好，世界", result.Content);
        Assert.Equal(9, result.InputTokens);
        Assert.Equal(42, result.OutputTokens);
    }

    [Fact]
    public async Task Claude_ChatStreamAsync_ToolUseInputJsonDeltas_BecomeContent()
    {
        // 结构化输出走 tool_use：正文在 input_json_delta 里，text block 基本为空。
        // 取错来源会让生成结果恒为空 —— 这是 Claude 流式最容易写错的一处。
        const string sse =
            "event: message_start\n" +
            "data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":5,\"output_tokens\":1}}}\n\n" +
            "event: content_block_start\n" +
            "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"tool_use\",\"name\":\"emit_schema\"}}\n\n" +
            "event: content_block_delta\n" +
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"{\\\"screen\\\":\"}}\n\n" +
            "event: content_block_delta\n" +
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"{\\\"name\\\":\\\"s1\\\"}}\"}}\n\n" +
            "event: message_delta\n" +
            "data: {\"type\":\"message_delta\",\"usage\":{\"output_tokens\":20}}\n\n" +
            "event: message_stop\n" +
            "data: {\"type\":\"message_stop\"}\n\n";

        var provider = new ClaudeProvider(
            new LlmProviderConfig { Provider = "claude", ApiKey = "k", Model = "m" },
            new StubHandler(_ => ClaudeSse(sse)));

        var deltas = new List<string>();
        var result = await provider.ChatStreamAsync(OpenAiRequest(TestSchema), deltas.Add);

        Assert.Equal(2, deltas.Count);
        Assert.Equal("{\"screen\":{\"name\":\"s1\"}}", result.Content);
        Assert.Equal(string.Concat(deltas), result.Content);
        Assert.Equal(20, result.OutputTokens);
    }

    [Fact]
    public async Task Claude_ChatStreamAsync_RequestCarriesStreamFlag()
    {
        string? body = null;
        var provider = new ClaudeProvider(
            new LlmProviderConfig { Provider = "claude", ApiKey = "k", Model = "m" },
            new StubHandler(req =>
            {
                body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return ClaudeSse("event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n");
            }));

        await provider.ChatStreamAsync(OpenAiRequest(), _ => { });

        Assert.NotNull(body);
        Assert.Contains("\"stream\":true", body);
    }

    [Fact]
    public async Task Claude_ChatStreamAsync_Rejected_FallsBackToNonStreaming()
    {
        var streamAttempts = 0;
        var provider = new ClaudeProvider(
            new LlmProviderConfig { Provider = "claude", ApiKey = "k", Model = "m", MaxRetries = 0 },
            new StubHandler(req =>
            {
                var isStream = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult()
                    .Contains("\"stream\":true", StringComparison.Ordinal);
                if (isStream)
                {
                    streamAttempts++;
                    return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"stream not supported\"}") };
                }

                return Json("""{"content":[{"type":"text","text":"回退正文"}],"usage":{"input_tokens":2,"output_tokens":3}}""");
            }));

        var deltas = new List<string>();
        var result = await provider.ChatStreamAsync(OpenAiRequest(), deltas.Add);

        Assert.Equal(1, streamAttempts);
        Assert.Equal(new[] { "回退正文" }, deltas);
        Assert.Equal("回退正文", result.Content);
        Assert.Equal(2, result.InputTokens);
        Assert.Equal(3, result.OutputTokens);
    }

    [Fact]
    public async Task Claude_ChatStreamAsync_AuthFailure_DoesNotRetryNonStreaming()
    {
        // 401 是鉴权问题：换请求形态必然再失败，不该再打一次
        var requests = 0;
        var provider = new ClaudeProvider(
            new LlmProviderConfig { Provider = "claude", ApiKey = "bad", Model = "m", MaxRetries = 0 },
            new StubHandler(_ =>
            {
                requests++;
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{\"error\":\"bad key\"}") };
            }));

        await Assert.ThrowsAsync<LlmProviderException>(() => provider.ChatStreamAsync(OpenAiRequest(), _ => { }));

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task OpenAi_ChatStreamAsync_MidStreamErrorFrame_ThrowsAndIsNotRetried()
    {
        // 中转在 HTTP 200 之后于流中途报错：绝不能换结构模式重试 ——
        // 重试会把已经显示出去的正文再吐一遍（用户看到正文重复）。
        var requests = 0;
        var provider = new OpenAIProvider(
            new LlmProviderConfig { Provider = "openai", ApiKey = "k", Model = "m", MaxRetries = 0 },
            new StubHandler(_ =>
            {
                requests++;
                return Sse(
                    "data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\n" +
                    "data: {\"error\":{\"message\":\"upstream reset\"}}\n\n");
            }));

        var deltas = new List<string>();
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() =>
            provider.ChatStreamAsync(OpenAiRequest(TestSchema), deltas.Add));

        Assert.Contains("upstream reset", ex.Message);
        Assert.Equal(new[] { "partial" }, deltas); // 只吐了一次
        Assert.Equal(1, requests);                 // 没有降级重试
    }

    [Fact]
    public async Task Claude_ChatStreamAsync_MidStreamErrorEvent_ThrowsAndIsNotRetried()
    {
        var requests = 0;
        var provider = new ClaudeProvider(
            new LlmProviderConfig { Provider = "claude", ApiKey = "k", Model = "m", MaxRetries = 0 },
            new StubHandler(_ =>
            {
                requests++;
                return ClaudeSse(
                    "event: content_block_delta\n" +
                    "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"片段\"}}\n\n" +
                    "event: error\n" +
                    "data: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"服务过载\"}}\n\n");
            }));

        var deltas = new List<string>();
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() =>
            provider.ChatStreamAsync(OpenAiRequest(), deltas.Add));

        Assert.Contains("服务过载", ex.Message);
        Assert.Equal(new[] { "片段" }, deltas);
        Assert.Equal(1, requests);
    }

    // ────────────────────────── 共用测试设施 ──────────────────────────

    private const string TestSchema =
        """{"type":"object","properties":{"screen":{"type":"object"}}}""";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_responder(request));
    }

    /// <summary>先吐一段就挂起（模拟连接未关、后续数据未到）的响应内容，用于取消与流式性判据。</summary>
    private sealed class GatedSseContent : HttpContent
    {
        private readonly string _first;
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public GatedSseContent(string first) => _first = first;

        public void Release() => _gate.TrySetResult();

        protected override Task<Stream> CreateContentReadStreamAsync() =>
            Task.FromResult<Stream>(new GateStream(_first, _gate.Task));

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        private sealed class GateStream : Stream
        {
            private readonly byte[] _first;
            private readonly Task _gate;
            private int _pos;

            public GateStream(string first, Task gate)
            {
                _first = Encoding.UTF8.GetBytes(first);
                _gate = gate;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
            {
                if (_pos < _first.Length)
                {
                    var n = Math.Min(buffer.Length, _first.Length - _pos);
                    _first.AsMemory(_pos, n).CopyTo(buffer);
                    _pos += n;
                    return n;
                }

                await _gate.WaitAsync(ct);
                return 0;
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => _pos; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
