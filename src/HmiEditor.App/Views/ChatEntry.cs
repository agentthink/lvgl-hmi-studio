using System.Collections.ObjectModel;

namespace HmiEditor.App.Views;

/// <summary>
/// Agent 运行中的单步工具调用记录（用于轨迹展示）。
/// </summary>
public sealed class ChatAgentStep
{
    /// <summary>工具中文名。</summary>
    public string ToolDisplay { get; init; } = "";

    /// <summary>工具参数摘要（JSON）。</summary>
    public string Arguments { get; init; } = "";

    /// <summary>执行结果摘要。</summary>
    public string Result { get; init; } = "";

    /// <summary>是否执行成功。</summary>
    public bool Ok { get; init; } = true;

    /// <summary>是否为写操作（修改了项目）。</summary>
    public bool Changed { get; init; }
}

/// <summary>
/// AI 助手面板对话消息条目。
/// 属性为可写(set)，以支持 Agent 运行中逐步追加轨迹与流式更新正文。
/// </summary>
public sealed class ChatEntry
{
    public string RoleDisplay { get; set; } = "";
    public string Content { get; set; } = "";
    public string? Detail { get; set; }

    /// <summary>Agent 运行步骤轨迹（Agent 模式消息专用）。ObservableCollection 以支持逐步追加自动刷新。</summary>
    public ObservableCollection<ChatAgentStep>? Steps { get; set; }

    /// <summary>Agent 本次运行会话 ID（用于回滚）。</summary>
    public string? AgentSessionId { get; set; }

    /// <summary>Agent 运行是否产生了写操作（决定显示回滚按钮）。</summary>
    public bool AgentHasChanges { get; set; }

    /// <summary>是否可重试(Agent 运行失败时显示重试按钮, 复用上次输入)。</summary>
    public bool CanRetry { get; set; }

    /// <summary>是否运行中(显示取消按钮)。</summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// 运行中的流式正文预览（模型增量输出的尾部片段）。
    /// 运行结束时清空 —— 最终正文进 <see cref="Content"/>，预览只是"还在动"的证据。
    /// </summary>
    public string? LivePreview { get; set; }

    /// <summary>流式状态行，形如「已接收 1234 字」。仅在接收增量时显示。</summary>
    public string? StreamingStatus { get; set; }

    /// <summary>是否正在接收流式增量（决定预览区可见性）。</summary>
    public bool IsStreaming { get; set; }

    public bool HasLivePreview => IsStreaming && !string.IsNullOrEmpty(LivePreview);

    /// <summary>生成的 SVG图片文本（图片生成意图时设置）。</summary>
    public string? ImageSvg { get; set; }

    /// <summary>生成图片的宽度。</summary>
    public int ImageWidth { get; set; }

    /// <summary>生成图片的高度。</summary>
    public int ImageHeight { get; set; }

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    public bool HasSteps => Steps is { Count: > 0 };

    public bool HasImage => !string.IsNullOrEmpty(ImageSvg);
}
