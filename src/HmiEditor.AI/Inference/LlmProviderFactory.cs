namespace HmiEditor.AI.Inference;

/// <summary>
/// LLM Provider 工厂 —— 根据配置创建对应 Provider 实例。
/// 支持 "claude" / "openai"(含自定义 OpenAI 兼容 endpoint), 预留 "ollama"。
/// </summary>
public static class LlmProviderFactory
{
    public static ILlmProvider Create(LlmProviderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.Provider.Trim().ToLowerInvariant() switch
        {
            "claude" => new ClaudeProvider(config),
            "openai" => new OpenAIProvider(config),
            _ => throw new NotSupportedException(
                $"不支持的 LLM Provider '{config.Provider}'。支持: claude / openai")
        };
    }
}
