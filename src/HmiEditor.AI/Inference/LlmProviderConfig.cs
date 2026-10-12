namespace HmiEditor.AI.Inference;

/// <summary>
/// LLM Provider 统一配置,支持 Claude / OpenAI / 自定义 OpenAI 兼容 endpoint。
/// 由 appsettings.json 或用户设置对话框填充,禁止在 Prompt 中出现密钥。
/// </summary>
public class LlmProviderConfig
{
    /// <summary>Provider 类型: "claude" | "openai" | "ollama"。</summary>
    public string Provider { get; set; } = "claude";

    /// <summary>API Key。Claude 使用 x-api-key,OpenAI 兼容使用 Bearer token。</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>模型名称,如 claude-sonnet-4-20250514 / gpt-4o / qwen2.5。</summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// 自定义 API Endpoint (OpenAI 兼容协议时使用)。
    /// 官方 OpenAI: https://api.openai.com/v1
    /// 国内中转/代理: 任意 OpenAI 兼容地址
    /// Azure OpenAI: https://resource.openai.azure.com/openai/deployments/{deployment}
    /// Claude 时可为空,使用官方地址。
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// 单次请求超时秒数（默认 <b>240</b>）。
    ///
    /// <para><b>为什么从 60 提到 240（2026-09-17）：</b>它被直接用作
    /// <c>HttpClient.Timeout</c>，即**整个响应读完为止**的上限（含流式读完整条 SSE 流）。
    /// 而 Agent 的单次请求 <c>MaxTokens = 8192</c>：按慢速中转/本地模型 ~45 token/s 估算
    /// 需要 **约 182 s**，60 s 会在**生成到一半时被掐断**，用户看到的是"AI 生成失败"
    /// （与模型质量无关，换一个慢一点的模型或中转就会复发）。取 240 s 留约 30% 余量。
    /// 本项目没有这个字段的设置界面，所以默认值就是唯一的杠杆
    /// （<c>LlmProviderConfigTests.DefaultTimeout_CoversLargestSingleRequest</c> 按同一算式钉住它）。</para>
    ///
    /// <para>放大超时的代价可控：长任务期间界面有流式增量与停止按钮
    /// （见 <c>docs/2026-09-17-llm-streaming-sse.md</c>），用户随时可取消，不必等超时。</para>
    /// </summary>
    public int TimeoutSeconds { get; set; } = 240;

    /// <summary>重试次数,默认 2。</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// 当 Provider 为 openai 兼容通道且不支持 json_schema/json_object 结构化输出时，
    /// 强制使用 XML 风格协议（对弱模型/中转服务更稳定）。
    /// Claude 原生 tool_use 结构化输出稳定，无需开启。
    /// </summary>
    public bool ForceXmlProtocol { get; set; }
}
