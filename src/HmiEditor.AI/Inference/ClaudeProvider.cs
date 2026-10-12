using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HmiEditor.AI.Inference;

/// <summary>
/// Claude Provider —— Anthropic Messages API。
/// Structured Output 通过 tool_use 的 input_schema 约束 JSON Schema。
/// 流式输出 (SSE) 通过 <see cref="IStreamingLlmProvider"/> 提供。
/// </summary>
public sealed class ClaudeProvider : LlmHttpProviderBase, IStreamingLlmProvider
{
    private const string DefaultEndpoint = "https://api.anthropic.com/v1/messages";

    public ClaudeProvider(LlmProviderConfig config) : base(config)
    {
    }

    /// <summary>供测试注入自定义 HttpMessageHandler。</summary>
    internal ClaudeProvider(LlmProviderConfig config, HttpMessageHandler handler) : base(config, handler)
    {
    }

    public override string ProviderName => "claude";

    public override async Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default)
    {
        var endpoint = ResolveEndpoint();

        using var response = await SendWithRetryAsync(
            () => CreateRequest(endpoint, BuildRequestBody(request, stream: false)), ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        var parsed = Deserialize<ClaudeResponse>(responseText)
                     ?? throw new LlmProviderException("Claude API 响应解析失败");

        return new LlmResponse
        {
            Content = ExtractContent(parsed),
            InputTokens = parsed.Usage?.InputTokens ?? 0,
            OutputTokens = parsed.Usage?.OutputTokens ?? 0
        };
    }

    /// <summary>
    /// 流式聊天 (SSE)。与非流式语义等价：**结构化输出走 tool_use 时，正文取自
    /// <c>input_json_delta</c> 的累积**（不是 text block —— 那种情况下模型几乎不输出正文）。
    /// 中转/网关若不接受流式请求（400/422/5xx），退回非流式并把完整内容作为单次增量回调。
    /// </summary>
    public async Task<LlmResponse> ChatStreamAsync(
        LlmRequest request, Action<string> onDelta, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onDelta);

        var endpoint = ResolveEndpoint();

        try
        {
            return await StreamOnceAsync(endpoint, request, onDelta, ct);
        }
        catch (LlmProviderException ex) when (IsRequestShapeError(ex))
        {
            // 请求形态类错误只可能在读到正文之前抛出（HTTP 状态先于正文）⇒ 没有增量被回调过，
            // 回退不会重复吐字。鉴权(401/403)与网络错误不在此列：换形态必然再失败，徒增一次请求。
            // 流中途的 error 事件不带 HTTP 状态码 ⇒ 不被本 catch 捕获，只上抛（重试会重复正文）。
            try
            {
                var fallback = await ChatAsync(request, ct);
                if (fallback.Content.Length > 0) onDelta(fallback.Content);
                return fallback;
            }
            catch (LlmProviderException fallbackError)
            {
                throw new LlmProviderException(
                    $"Claude 流式请求失败，非流式回退亦失败: {fallbackError.Message}",
                    fallbackError.StatusCode, fallbackError);
            }
        }
    }

    private string ResolveEndpoint() =>
        string.IsNullOrWhiteSpace(Config.Endpoint) ? DefaultEndpoint : Config.Endpoint!;

    private async Task<LlmResponse> StreamOnceAsync(
        string endpoint, LlmRequest request, Action<string> onDelta, CancellationToken ct)
    {
        using var response = await SendWithRetryAsync(
            () => CreateRequest(endpoint, BuildRequestBody(request, stream: true)), ct);

        var text = new StringBuilder();          // 普通 text block 正文
        var toolJson = new StringBuilder();      // tool_use 的 input JSON 分片累积
        var sawToolUse = false;
        var inputTokens = 0;
        var outputTokens = 0;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await foreach (var frame in SseReader.ReadAsync(stream, leaveOpen: true, ct))
        {
            if (frame.Data == SseReader.DoneToken) break;

            ClaudeStreamEvent? evt;
            try
            {
                evt = Deserialize<ClaudeStreamEvent>(frame.Data);
            }
            catch (JsonException)
            {
                continue; // ping / 非 JSON 帧
            }

            if (evt is null) continue;

            switch (evt.Type)
            {
                case "error":
                    // 流中途报错（overloaded_error / api_error 等）。不带 HTTP 状态码 ⇒ 不重试。
                    throw new LlmProviderException(
                        $"Claude 流式响应中途报错: {evt.Error?.Message ?? evt.Error?.Type ?? "未知错误"}");

                case "message_start":
                    if (evt.Message?.Usage is { } startUsage)
                        inputTokens = startUsage.InputTokens;
                    break;

                case "content_block_start":
                    if (evt.ContentBlock?.Type == "tool_use") sawToolUse = true;
                    break;

                case "content_block_delta":
                    if (evt.Delta is not { } d) break;
                    if (d.Type == "text_delta" && !string.IsNullOrEmpty(d.Text))
                    {
                        text.Append(d.Text);
                        onDelta(d.Text);
                    }
                    else if (d.Type == "input_json_delta" && !string.IsNullOrEmpty(d.PartialJson))
                    {
                        sawToolUse = true;
                        toolJson.Append(d.PartialJson);
                        onDelta(d.PartialJson);
                    }
                    break;

                case "message_delta":
                    if (evt.Usage is { } endUsage)
                        outputTokens = endUsage.OutputTokens;
                    break;
            }
        }

        // 与非流式 ExtractContent 同序：优先 tool_use 的 input JSON，否则拼接文本块
        var content = sawToolUse ? toolJson.ToString() : text.ToString();

        return new LlmResponse
        {
            Content = content,
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };
    }

    private HttpRequestMessage CreateRequest(string endpoint, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.TryAddWithoutValidation("x-api-key", Config.ApiKey);
        req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        req.Content = JsonContent.Create(body);
        return req;
    }

    /// <summary>
    /// 构建请求体。Claude 使用 tool_use 实现结构化输出: 约束模型调用特定工具并返回
    /// input_schema 形状的 JSON；无 schema 时省略 tools/tool_choice（避免发送 null 字段被 API 拒绝）。
    /// </summary>
    private object BuildRequestBody(LlmRequest request, bool stream)
    {
        var hasSchema = !string.IsNullOrWhiteSpace(request.JsonSchema);

        var body = new Dictionary<string, object?>
        {
            ["model"] = Config.Model,
            ["max_tokens"] = request.MaxTokens,
            ["temperature"] = request.Temperature,
            ["messages"] = request.Messages.Select(m => new { role = m.Role, content = m.Content })
        };
        if (hasSchema)
        {
            body["tools"] = new[]
            {
                new
                {
                    name = "emit_schema",
                    description = "以严格 JSON 输出结果",
                    input_schema = ParseJsonSchema(request.JsonSchema!)
                }
            };
            body["tool_choice"] = new { type = "tool", name = "emit_schema" };
        }
        if (stream)
            body["stream"] = true;
        return body;
    }

    private static object ParseJsonSchema(string schema) =>
        System.Text.Json.JsonSerializer.Deserialize<object>(schema)
        ?? throw new LlmProviderException("JSON Schema 解析失败");

    private static string ExtractContent(ClaudeResponse response)
    {
        if (response.Content is null) return "";

        foreach (var block in response.Content)
        {
            if (block.Type == "tool_use" && block.Input is not null)
            {
                // tool_use.input 是 emit_schema 的入参对象(即 schema 形状的 JSON), 序列化回字符串
                return System.Text.Json.JsonSerializer.Serialize(block.Input);
            }
        }

        return string.Join("", response.Content
            .Where(b => b.Type == "text")
            .Select(b => b.Text ?? ""));
    }

    private sealed class ClaudeResponse
    {
        public List<ClaudeContentBlock>? Content { get; set; }
        public ClaudeUsage? Usage { get; set; }
    }

    private sealed class ClaudeContentBlock
    {
        public string Type { get; set; } = "";
        public string? Text { get; set; }
        public object? Input { get; set; }
    }

    private sealed class ClaudeUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    // ── 流式 (stream: true) 事件形状 ──
    private sealed class ClaudeStreamEvent
    {
        public string Type { get; set; } = "";
        public ClaudeStreamMessage? Message { get; set; }
        public ClaudeStreamContentBlock? ContentBlock { get; set; }
        public ClaudeStreamDelta? Delta { get; set; }
        public ClaudeUsage? Usage { get; set; }
        public ClaudeStreamError? Error { get; set; }
    }

    private sealed class ClaudeStreamError
    {
        public string? Type { get; set; }
        public string? Message { get; set; }
    }

    private sealed class ClaudeStreamMessage
    {
        public ClaudeUsage? Usage { get; set; }
    }

    private sealed class ClaudeStreamContentBlock
    {
        public string? Type { get; set; }
    }

    private sealed class ClaudeStreamDelta
    {
        public string? Type { get; set; }
        public string? Text { get; set; }

        [JsonPropertyName("partial_json")]
        public string? PartialJson { get; set; }
    }
}
