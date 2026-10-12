using HmiEditor.AI.Inference;

namespace HmiEditor.AI.Tests;

/// <summary>
/// 测试用假 LLM Provider —— 返回预置响应, 记录收到的请求。
/// 用于端到端管线测试: Prompt 构建 → 生成器 → 校验。
/// </summary>
public sealed class FakeLlmProvider : ILlmProvider
{
    private readonly Func<LlmRequest, string> _responder;

    /// <summary>构造: 指定响应生成函数(可按请求内容返回不同响应)。</summary>
    public FakeLlmProvider(Func<LlmRequest, string> responder)
    {
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
    }

    /// <summary>收到的所有请求(供断言 Prompt 内容)。</summary>
    public List<LlmRequest> ReceivedRequests { get; } = new();

    /// <summary>最近一次请求的 System Prompt 文本。</summary>
    public string? LastSystemPrompt =>
        ReceivedRequests.LastOrDefault()?.Messages
            .FirstOrDefault(m => m.Role == LlmRoles.System)?.Content;

    /// <summary>最近一次请求的 User Prompt 文本。</summary>
    public string? LastUserPrompt =>
        ReceivedRequests.LastOrDefault()?.Messages
            .FirstOrDefault(m => m.Role == LlmRoles.User)?.Content;

    public string ProviderName => "fake";

    public Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default)
    {
        ReceivedRequests.Add(request);
        var content = _responder(request);
        return Task.FromResult(new LlmResponse
        {
            Content = content,
            InputTokens = 100,
            OutputTokens = 50
        });
    }
}
