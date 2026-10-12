namespace HmiEditor.AI.Inference;

/// <summary>
/// 可选的流式能力接口 —— 支持 SSE 增量输出的 Provider 实现它。
///
/// <para><b>为什么是独立接口而不是给 <see cref="ILlmProvider"/> 加两个成员：</b>
/// 流式是**能力**不是**契约**。加进主接口意味着每个实现（含测试替身、将来的本地模型）
/// 都被迫提供一份「假装流式」的桩，而调用方无法区分「真流式」与「桩」。
/// 独立接口让能力可探测：<c>provider is IStreamingLlmProvider</c>，不支持就回退
/// <see cref="ILlmProvider.ChatAsync"/>，**行为与今天完全一致**。</para>
///
/// <para><b>并发契约：</b><paramref name="onDelta"/> 在读取响应流的同一上下文里同步回调，
/// 不保证在 UI 线程；跨线程 UI 更新由调用方自己 marshal。一次调用内回调严格按到达顺序，
/// 但**不保证**与最终 <see cref="LlmResponse.Content"/> 的拼接结果逐字相同的前提是调用方
/// 不要自己重排 —— 实现保证等价（文本块按序拼接）。</para>
/// </summary>
public interface IStreamingLlmProvider : ILlmProvider
{
    /// <summary>
    /// 流式聊天：每收到一段增量文本立即回调 <paramref name="onDelta"/>，
    /// 返回与非流式 <see cref="ILlmProvider.ChatAsync"/> **语义等价**的完整响应
    /// （Content 为全部增量拼接、token 用量来自流内 usage 帧，缺失时为 0）。
    /// </summary>
    /// <remarks>
    /// 取消语义与 <see cref="ILlmProvider.ChatAsync"/> 一致：调用方取消抛
    /// <see cref="OperationCanceledException"/>，不包装成 Provider 异常。
    /// Provider 若确认不支持流式（例如中转对所有结构模式都返回 400），
    /// **应自行退回非流式并把完整内容作为单次增量回调**，而不是让调用方承担失败。
    /// </remarks>
    Task<LlmResponse> ChatStreamAsync(
        LlmRequest request,
        Action<string> onDelta,
        CancellationToken ct = default);
}
