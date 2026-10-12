namespace HmiEditor.AI.Inference;

/// <summary>
/// LLM 聊天消息角色。
/// </summary>
public static class LlmRoles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
}

/// <summary>
/// LLM 聊天消息。
/// </summary>
public record LlmMessage
{
    /// <summary>角色: "system" | "user" | "assistant"。</summary>
    public string Role { get; init; } = LlmRoles.User;

    public string Content { get; init; } = "";
}

/// <summary>
/// LLM 请求。统一请求模型,由各 Provider 翻译为其原生 API 协议。
/// </summary>
public record LlmRequest
{
    public List<LlmMessage> Messages { get; init; } = new();

    /// <summary>采样温度,结构化生成建议 0.2。</summary>
    public double Temperature { get; init; } = 0.2;

    public int MaxTokens { get; init; } = 4096;

    /// <summary>可选 JSON Schema 约束输出 (Structured Output)。</summary>
    public string? JsonSchema { get; init; }
}

/// <summary>
/// LLM 响应。统一响应模型,由各 Provider 解析其原生响应后填充。
/// </summary>
public record LlmResponse
{
    public string Content { get; init; } = "";

    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }
}

/// <summary>
/// LLM Provider 统一接口。所有 Provider (Claude / OpenAI / 自定义 OpenAI 兼容服务) 必须实现此接口。
/// 上层生成器只依赖此接口,切换 Provider 零代码改动。
/// </summary>
public interface ILlmProvider
{
    /// <summary>Provider 标识名: "claude" | "openai" | "ollama"。</summary>
    string ProviderName { get; }

    Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default);
}
