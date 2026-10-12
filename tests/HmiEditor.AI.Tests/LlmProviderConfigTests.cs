using HmiEditor.AI.Inference;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// <see cref="LlmProviderConfig"/> 默认值的**判据式**守卫。
///
/// <para>默认值本身不是"随便取的数"：<c>TimeoutSeconds</c> 被直接用作 <c>HttpClient.Timeout</c>，
/// 也就是"整个响应读完为止"的上限。取小了会在**生成到一半时掐断**（用户看到"AI 生成失败"，
/// 与模型质量无关，换个慢模型/慢中转就复发），而本项目没有这个字段的设置界面 ——
/// 默认值是唯一杠杆。所以用算式把它钉住，而不是靠注释里的形容词。</para>
/// </summary>
public class LlmProviderConfigTests
{
    /// <summary>Agent 单次请求的 token 上限（<c>AgentEngine</c> 里写死 8192）。</summary>
    private const int AgentMaxTokens = 8192;

    /// <summary>保守的慢速中转 / 本地模型吞吐（token/s）。</summary>
    private const double SlowRelayTokensPerSecond = 45.0;

    [Fact]
    public void DefaultTimeout_CoversLargestSingleRequest()
    {
        var neededSeconds = AgentMaxTokens / SlowRelayTokensPerSecond; // ≈ 182 s

        var config = new LlmProviderConfig();

        Assert.True(config.TimeoutSeconds >= neededSeconds,
            $"默认超时 {config.TimeoutSeconds}s 覆盖不住单次 {AgentMaxTokens} token 的生成" +
            $"（按 {SlowRelayTokensPerSecond} token/s 估算需 ≈{neededSeconds:F0}s）—— " +
            "取小了会在生成到一半时被 HttpClient 掐断，而本字段没有设置界面，默认值就是唯一杠杆。");
    }

    [Fact]
    public void DefaultRetriesAndProvider_AreStable()
    {
        // 与超时无关，但同一处默认值被误改时一起点名（重试 0 会让偶发 5xx 直接失败）
        var config = new LlmProviderConfig();
        Assert.Equal(2, config.MaxRetries);
        Assert.False(string.IsNullOrWhiteSpace(config.Provider));
    }
}
