using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HmiEditor.AI.Inference;

/// <summary>
/// OpenAI Provider —— OpenAI Chat Completions API (OpenAI 兼容协议)。
/// 支持自定义 Endpoint:
///   - 官方 OpenAI: https://api.openai.com/v1
///   - 国内中转/代理 / One API / vLLM / LiteLLM: 任意 OpenAI 兼容地址
///   - Azure OpenAI: https://resource.openai.azure.com/openai/deployments/{deployment}
/// Structured Output 通过 response_format.json_schema 约束。
/// 流式输出 (SSE) 通过 <see cref="IStreamingLlmProvider"/> 提供。
/// </summary>
public sealed class OpenAIProvider : LlmHttpProviderBase, IStreamingLlmProvider
{
    private const string DefaultEndpoint = "https://api.openai.com/v1";

    public OpenAIProvider(LlmProviderConfig config) : base(config)
    {
    }

    /// <summary>供测试注入自定义 HttpMessageHandler。</summary>
    internal OpenAIProvider(LlmProviderConfig config, HttpMessageHandler handler) : base(config, handler)
    {
    }

    public override string ProviderName => "openai";

    public override async Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default)
    {
        var endpoint = ResolveEndpoint();
        var hasSchema = !string.IsNullOrWhiteSpace(request.JsonSchema);

        // 三层降级策略（兼容中转服务对 response_format 支持不一的现实）:
        //   1. strict json_schema（官方 OpenAI 结构化输出）
        //   2. json_object（宽松 JSON，中转普遍支持）
        //   3. 无 response_format（最老的中转/模型）
        // 仅对参数类错误 (400/422) 降级; 鉴权(401/403)与网络/限流错误不降级。
        var responseText = hasSchema
            ? await SendWithFormatDegradationAsync(endpoint, request, ct)
            : await SendOnceAsync(endpoint, request, responseFormat: null, ct);

        var parsed = Deserialize<OpenAiResponse>(responseText)
                     ?? throw new LlmProviderException("OpenAI API 响应解析失败");

        var content = parsed.Choices?.FirstOrDefault()?.Message?.Content ?? "";
        return new LlmResponse
        {
            Content = content,
            InputTokens = parsed.Usage?.PromptTokens ?? 0,
            OutputTokens = parsed.Usage?.CompletionTokens ?? 0
        };
    }

    /// <summary>
    /// 流式聊天 (SSE)。与非流式走同一套三层降级。
    ///
    /// <para><b>为什么降级不会重复吐字：</b>降级只可能发生在**响应体尚未读取**时 ——
    /// HTTP 状态先于正文到达，<c>SendWithRetryAsync</c> 在非 2xx 时直接抛出，
    /// 此时一个增量都没回调过。**流中途**的失败（例如中转在 200 之后发
    /// <c>{"error":…}</c> 帧）带不上 HTTP 状态码，因此天然不被降级捕获，只会向上抛
    /// —— 这是刻意的：换形态重试会把已经显示出去的正文再吐一遍。</para>
    ///
    /// <para>若所有结构模式都被拒，说明该中转根本不接受流式请求 ⇒ 退回非流式并把完整内容
    /// 作为单次增量回调（能力降级，行为仍正确）。</para>
    /// </summary>
    public async Task<LlmResponse> ChatStreamAsync(
        LlmRequest request, Action<string> onDelta, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onDelta);

        var endpoint = ResolveEndpoint();
        var hasSchema = !string.IsNullOrWhiteSpace(request.JsonSchema);
        var modes = hasSchema ? new[] { "json_schema", "json_object", "none" } : new[] { "none" };

        string? lastError = null;

        foreach (var mode in modes)
        {
            try
            {
                return await StreamOnceAsync(endpoint, request, BuildResponseFormat(mode, request.JsonSchema), onDelta, ct);
            }
            catch (LlmProviderException ex) when (IsRequestShapeError(ex))
            {
                // 参数类错误(400/422/5xx)：换一种结构模式重试（网络/限流已由 SendWithRetryAsync 内部重试）
                lastError = ex.Message;
            }
        }

        // 所有结构模式都被拒 ⇒ 中转不接受流式；退回非流式（仍可能经 ChatAsync 自身降级成功）
        try
        {
            var fallback = await ChatAsync(request, ct);
            if (fallback.Content.Length > 0) onDelta(fallback.Content);
            return fallback;
        }
        catch (LlmProviderException ex)
        {
            throw new LlmProviderException(
                $"LLM API 流式请求失败 (json_schema / json_object / 无约束 均被拒绝)，非流式回退亦失败: {ex.Message}",
                ex.StatusCode, ex);
        }
    }

    /// <summary>解析接口地址: 已含 /chat/completions 时原样使用，否则追加。</summary>
    private string ResolveEndpoint()
    {
        var baseEndpoint = string.IsNullOrWhiteSpace(Config.Endpoint) ? DefaultEndpoint : Config.Endpoint!.TrimEnd('/');
        return baseEndpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? baseEndpoint
            : $"{baseEndpoint}/chat/completions";
    }

    /// <summary>按三层策略发送: json_schema → json_object → 无约束, 参数错误自动降级。</summary>
    private async Task<string> SendWithFormatDegradationAsync(string endpoint, LlmRequest request, CancellationToken ct)
    {
        string? lastError = null;

        foreach (var mode in new[] { "json_schema", "json_object", "none" })
        {
            try
            {
                return await SendOnceAsync(endpoint, request, BuildResponseFormat(mode, request.JsonSchema), ct);
            }
            catch (LlmProviderException ex) when (IsRequestShapeError(ex))
            {
                // 参数类错误(400/422) → 记录后降级重试; 其余异常直接上抛
                lastError = ex.Message;
            }
        }

        throw new LlmProviderException(
            $"LLM API 请求失败 (json_schema / json_object / 无约束 均被拒绝): {lastError}");
    }

    /// <summary>按模式构造 response_format；"none" 返回 null（不发送该字段）。</summary>
    private static object? BuildResponseFormat(string mode, string? schema) => mode switch
    {
        "json_schema" => new
        {
            type = "json_schema",
            json_schema = new
            {
                name = "hmi_generation",
                strict = true,
                schema = ParseJsonSchema(schema!)
            }
        },
        "json_object" => new { type = "json_object" },
        _ => null
    };

    /// <summary>构建请求体。responseFormat 为 null 时不发送 response_format 字段。</summary>
    private object BuildRequestBody(LlmRequest request, object? responseFormat, bool stream = false)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = Config.Model,
            ["messages"] = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            ["temperature"] = request.Temperature,
            ["max_tokens"] = request.MaxTokens,
        };
        if (responseFormat is not null)
            body["response_format"] = responseFormat;
        if (stream)
        {
            body["stream"] = true;
            // 非流式响应总带 usage；流式默认不带 ⇒ 显式索取，否则 UI 的 token 读数会凭空消失
            body["stream_options"] = new { include_usage = true };
        }
        return body;
    }

    /// <summary>发送请求并返回响应文本。</summary>
    private async Task<string> SendOnceAsync(string endpoint, LlmRequest request, object? responseFormat, CancellationToken ct)
    {
        using var response = await SendWithRetryAsync(
            () => CreateRequest(endpoint, BuildRequestBody(request, responseFormat)), ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>发送流式请求并逐段回调增量，返回拼接后的完整响应。</summary>
    private async Task<LlmResponse> StreamOnceAsync(
        string endpoint, LlmRequest request, object? responseFormat, Action<string> onDelta, CancellationToken ct)
    {
        using var response = await SendWithRetryAsync(
            () => CreateRequest(endpoint, BuildRequestBody(request, responseFormat, stream: true)), ct);

        var content = new StringBuilder();
        var inputTokens = 0;
        var outputTokens = 0;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await foreach (var frame in SseReader.ReadAsync(stream, leaveOpen: true, ct))
        {
            if (frame.Data == SseReader.DoneToken) break;

            OpenAiStreamChunk? chunk;
            try
            {
                chunk = Deserialize<OpenAiStreamChunk>(frame.Data);
            }
            catch (JsonException)
            {
                continue; // 中转可能插入非 JSON 的心跳/空帧 —— 跳过而不是让整次生成失败
            }

            if (chunk is null) continue;

            // 中转在 HTTP 200 之后于流中途报错（上游断流/超额等）时的约定形态。
            // 不带 HTTP 状态码 ⇒ 不会被结构模式降级捕获 ⇒ 只上抛，绝不重试：
            // 重试会把已经显示出去的正文再吐一遍，用户看到的是正文重复。
            if (chunk.Error is { } error)
                throw new LlmProviderException(
                    $"LLM API 流式响应中途报错: {error.Message ?? error.Type ?? "未知错误"}");

            var delta = chunk.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(delta))
            {
                content.Append(delta);
                onDelta(delta);
            }

            if (chunk.Usage is { } usage)
            {
                inputTokens = usage.PromptTokens;
                outputTokens = usage.CompletionTokens;
            }
        }

        return new LlmResponse
        {
            Content = content.ToString(),
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };
    }

    private HttpRequestMessage CreateRequest(string endpoint, object requestBody)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Config.ApiKey}");
        req.Content = JsonContent.Create(requestBody);
        return req;
    }

    /// <summary>检测 JSON Schema 是否包含 array 类型(该类型部分中转服务不支持)。</summary>
    internal static bool ContainsSchemaArray(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema)) return false;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(schema);
            return ContainsArray(doc.RootElement);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool ContainsArray(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out var typeProp) &&
                typeProp.ValueKind == System.Text.Json.JsonValueKind.String &&
                string.Equals(typeProp.GetString(), "array", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            foreach (var prop in element.EnumerateObject())
            {
                if (ContainsArray(prop.Value)) return true;
            }
        }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsArray(item)) return true;
            }
        }
        return false;
    }

    private static object ParseJsonSchema(string schema) =>
        System.Text.Json.JsonSerializer.Deserialize<object>(schema)
        ?? throw new LlmProviderException("JSON Schema 解析失败");

    private sealed class OpenAiResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
        public OpenAiUsage? Usage { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        public string? Content { get; set; }
    }

    private sealed class OpenAiUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
    }

    // ── 流式 (stream: true) 响应形状 ──
    // {"choices":[{"delta":{"content":"..."}}]} ... {"choices":[],"usage":{...}} [DONE]
    private sealed class OpenAiStreamChunk
    {
        public List<OpenAiStreamChoice>? Choices { get; set; }
        public OpenAiUsage? Usage { get; set; }
        public OpenAiStreamError? Error { get; set; }
    }

    private sealed class OpenAiStreamError
    {
        public string? Message { get; set; }
        public string? Type { get; set; }
    }

    private sealed class OpenAiStreamChoice
    {
        public OpenAiStreamDelta? Delta { get; set; }
    }

    private sealed class OpenAiStreamDelta
    {
        public string? Content { get; set; }
    }
}
