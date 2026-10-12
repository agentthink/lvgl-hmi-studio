namespace HmiEditor.AI.Agent;

/// <summary>
/// 单步 Agent 运行记录（一次工具调用）。
/// </summary>
public sealed class AgentRunStep
{
    public required string ToolName { get; init; }

    /// <summary>工具参数摘要（JSON，用于展示）。</summary>
    public string Arguments { get; init; } = "";

    public bool Ok { get; init; }

    /// <summary>执行结果摘要（展示 + 回填给模型）。</summary>
    public string Summary { get; init; } = "";

    public string? Error { get; init; }

    /// <summary>是否为写操作（Changed）。</summary>
    public bool Changed { get; init; }

    /// <summary>执行前项目快照（由 App 层在执行器闭包中填充，用于回滚）。</summary>
    public string? SnapshotBefore { get; set; }
}

/// <summary>
/// 一次 Agent 运行的整体结果。
/// </summary>
public sealed class AgentRunResult
{
    /// <summary>本次运行会话 ID（供回滚定位快照）。</summary>
    public string SessionId { get; set; } = "";

    /// <summary>所有已执行步骤（按顺序）。</summary>
    public List<AgentRunStep> Steps { get; } = new();

    /// <summary>模型最终回复文本（final_answer.text）。</summary>
    public string FinalAnswer { get; set; } = "";

    /// <summary>模型最终回复摘要（final_answer.summary）。</summary>
    public string FinalSummary { get; set; } = "";

    /// <summary>是否正常完成（模型给出 final_answer 或步骤执行完毕）。</summary>
    public bool Completed { get; set; }

    /// <summary>是否因错误中断（某步失败且无法继续）。</summary>
    public bool Failed => !Completed;

    /// <summary>累计 token 消耗。</summary>
    public int TotalInputTokens { get; set; }

    public int TotalOutputTokens { get; set; }

    public int TotalTokens => TotalInputTokens + TotalOutputTokens;

    /// <summary>本次运行的最大步数上限(供 UI 显示"已用 X/N 步")。</summary>
    public int MaxSteps { get; set; } = 30;

    /// <summary>本轮累积的全部 LLM 消息(含 system + 历史 + 本轮新增)，供宿主合并会话历史。宿主合并时跳过 system。</summary>
    public List<HmiEditor.AI.Inference.LlmMessage> Messages { get; set; } = new();

    /// <summary>是否有任何写操作发生（决定是否显示"回滚"入口）。</summary>
    public bool HasChanges => Steps.Any(s => s.Changed);
}
