namespace HmiEditor.AI.Inference;

/// <summary>
/// 统一的「带增量回调的 LLM 调用」入口 —— 流式与降级的判定只此一处。
///
/// <para><b>为什么必须收口：</b>「Provider 支不支持流式 → 走哪条路 → 非流式也要补一次回调」
/// 是三条独立判据。任何调用方自己写一遍，都会在有人新增一个 Provider 时出现
/// 「Agent 走流式、画布生成却不走」这类分叉 —— 而分叉的表现是**界面时灵时不灵**，
/// 不是编译错误。项目里已有多次「同一判据两份实现后漂移」的教训
/// （见 <c>docs/project-status.md</c> 阶段 6 / 阶段 9）。</para>
///
/// <para><b>不变量：</b>只要传了 <paramref name="textProgress"/>，无论 Provider 是否支持流式，
/// 都**至少**会收到一次回调（非流式时是完整正文一次性回调）。调用方因此不需要写两套 UI 逻辑。</para>
/// </summary>
public static class LlmChat
{
    /// <summary>
    /// 调用 Provider 取一次响应。<paramref name="textProgress"/> 非空且 Provider 实现
    /// <see cref="IStreamingLlmProvider"/> 时走 SSE 增量；否则单次调用并回调完整正文。
    /// 两条路径返回的响应语义相同（Content 为完整正文）。
    /// </summary>
    public static async Task<LlmResponse> WithProgressAsync(
        ILlmProvider provider,
        LlmRequest request,
        IProgress<string>? textProgress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        if (textProgress is not null && provider is IStreamingLlmProvider streaming)
            return await streaming.ChatStreamAsync(request, delta => textProgress.Report(delta), ct);

        var response = await provider.ChatAsync(request, ct);
        if (textProgress is not null && response.Content.Length > 0)
            textProgress.Report(response.Content);
        return response;
    }
}
