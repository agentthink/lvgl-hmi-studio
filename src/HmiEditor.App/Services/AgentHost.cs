using HmiEditor.AI.Agent;
using HmiEditor.AI.Generators;
using HmiEditor.AI.Inference;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Recipe;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// Agent 与编辑器宿主的桥接回调 —— 由 MainWindow 提供，绑定项目操作与 UI 刷新。
/// </summary>
public sealed class AgentHostBridge
{
    /// <summary>获取当前项目（每次工具执行时实时获取）。</summary>
    public required Func<HmiProject> GetProject { get; init; }

    /// <summary>替换当前项目对象（回滚后调用）。</summary>
    public required Action<HmiProject> ReplaceProject { get; init; }

    /// <summary>标记项目有未保存变更。</summary>
    public required Action MarkChanged { get; init; }

    /// <summary>刷新所有依赖项目数据的面板（画布/图层/资源树/属性/导航等）。</summary>
    public required Action RefreshPanels { get; init; }

    /// <summary>打开指定 Dock 面板（按 Id）。</summary>
    public required Action<string> OpenPanel { get; init; }

    /// <summary>画布切换当前屏幕（按名称）。</summary>
    public required Action<string> SwitchScreen { get; init; }

    /// <summary>获取认证服务（用户管理工具）。</summary>
    public required Func<AuthenticationService> GetAuthService { get; init; }

    /// <summary>获取配方管理器。</summary>
    public required Func<RecipeManager> GetRecipeManager { get; init; }

    /// <summary>保存项目（复用主窗口保存流程）。</summary>
    public required Func<Task> SaveProject { get; init; }

    /// <summary>导出 LVGL 代码（复用主窗口导出流程）。</summary>
    public required Func<Task> ExportCode { get; init; }
}

/// <summary>
/// 会话级变更快照 —— 记录会话中首次写操作前的项目状态，支持一键回滚。
/// </summary>
public sealed class AgentSnapshotTracker
{
    private string? _firstSnapshot;
    private string _firstTool = "";

    /// <summary>写操作执行前调用（仅记录首个快照，即回滚基准点）。</summary>
    public void BeforeWrite(HmiProject project, string toolName)
    {
        if (_firstSnapshot is not null) return;
        _firstSnapshot = ProjectSerializer.Serialize(project);
        _firstTool = toolName;
    }

    /// <summary>是否记录过快照（存在可回滚的基准）。</summary>
    public bool CanRollback => _firstSnapshot is not null;

    /// <summary>回滚基准快照 JSON。</summary>
    public string? FirstSnapshot => _firstSnapshot;

    public string FirstTool => _firstTool;

    /// <summary>从快照反序列化出项目。</summary>
    public HmiProject? Restore() =>
        _firstSnapshot is null ? null : ProjectSerializer.Deserialize(_firstSnapshot);
}

/// <summary>
/// Agent 宿主 —— App 层组装 Agent 引擎 + 工具集 + 快照回滚。
/// </summary>
public sealed class AgentHost : IDisposable
{
    private readonly AgentHostBridge _bridge;
    private readonly Dictionary<string, AgentSnapshotTracker> _trackers = new();
    private ILlmProvider? _provider;
    private AgentEngine? _engine;
    private AgentSnapshotTracker? _currentTracker;
    private readonly SemaphoreSlim _runGate = new(1, 1);

    /// <summary>当前会话的跨轮对话历史(非 system 消息)，供 Agent 续接上下文。</summary>
    private readonly List<LlmMessage> _conversationHistory = new();

    /// <summary>当前会话稳定 ID(工具上下文复用)；null 表示尚未开始会话。</summary>
    private string? _conversationId;

    /// <summary>本次运行中生成的图片列表 (SVG, Width, Height)。</summary>
    public List<(string Svg, int Width, int Height)> GeneratedImages { get; } = new();

    /// <summary>Agent 最大工具调用步数(默认 30)。可在 SetProvider 前调整以适配复杂任务。</summary>
    public int MaxAgentSteps { get; set; } = 30;

    public AgentHost(AgentHostBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    /// <summary>设置 LLM Provider（与 AiPreviewManager 共用同一实例）。null = Agent 未配置。</summary>
    /// <param name="provider">LLM Provider 实例。</param>
    /// <param name="forceXmlProtocol">当 Provider 不支持结构化输出时，强制使用 XML 风格协议（对弱模型/中转服务更稳定）。</param>
    public void SetProvider(ILlmProvider? provider, bool forceXmlProtocol = false)
    {
        _provider = provider;
        if (_provider is null)
        {
            _engine = null;
            return;
        }

        var prompts = new PromptTemplateEngine();
        var imageGenerator = new ImageGenerator(_provider, prompts);
        var tools = AgentToolFactory.CreateAll(
            _bridge, BeforeWrite, AfterWrite,
            imageGenerator, OnImageGenerated);
        var engine = new AgentEngine(_provider, tools, () => new AgentToolContext
        {
            Project = _bridge.GetProject(),
            SessionId = _engine!.SessionId
        })
        {
            ForceXmlProtocol = forceXmlProtocol,
            MaxSteps = MaxAgentSteps
        };
        _engine = engine;
    }

    /// <summary>图片生成回调 —— 由 generate_image 工具调用。</summary>
    private void OnImageGenerated(string svg, int width, int height)
    {
        GeneratedImages.Add((svg, width, height));
    }

    /// <summary>是否已配置（有 Provider 且引擎可用）。</summary>
    public bool IsConfigured => _provider is not null && _engine is not null;

    /// <summary>运行 Agent 任务（全程自动执行；返回后可查询 tracker 回滚）。</summary>
    /// <remarks>同一会话内多次调用自动续接历史上下文(会话级记忆)；调用 <see cref="StartNewConversation"/> 可清空上下文。</remarks>
    public async Task<AgentRunResult> RunAsync(string userInput, CancellationToken ct = default)
        => await RunAsync(userInput, stepProgress: null, ct);

    /// <summary>运行 Agent 任务，并逐步推送工具调用轨迹(每步执行完即回调)。</summary>
    public async Task<AgentRunResult> RunAsync(string userInput, IProgress<AgentRunStep>? stepProgress, CancellationToken ct = default)
        => await RunAsync(userInput, stepProgress, textProgress: null, ct);

    /// <summary>
    /// 运行 Agent 任务，并逐步推送工具调用轨迹 + 模型正文增量（流式）。
    /// <paramref name="textProgress"/> 为 null 时不走 SSE，直接用非流式 <c>ChatAsync</c>
    /// （没有观察者时多一层解析只是白付；Provider 不支持流式时同样自动退回非流式）。
    /// </summary>
    public async Task<AgentRunResult> RunAsync(
        string userInput,
        IProgress<AgentRunStep>? stepProgress,
        IProgress<string>? textProgress,
        CancellationToken ct = default)
    {
        if (_engine is null)
            throw new InvalidOperationException("Agent 未配置 LLM Provider");
        if (!await _runGate.WaitAsync(0, ct))
            throw new InvalidOperationException("Agent 已有任务正在运行，请等待当前任务结束或先取消。");

        try
        {
            // 会话级稳定 ID(供工具上下文复用); 每次运行生成独立 runId(供回滚定位)
            _conversationId ??= Guid.NewGuid().ToString("N")[..8];
            var runId = Guid.NewGuid().ToString("N")[..8];
            _currentTracker = new AgentSnapshotTracker();
            _trackers[runId] = _currentTracker;
            GeneratedImages.Clear();

            var result = await _engine.RunAsync(userInput, sessionId: _conversationId,
                priorHistory: _conversationHistory, stepProgress: stepProgress,
                textProgress: textProgress, ct: ct);
            result.SessionId = runId;

            // 合并本轮累积消息到会话历史(跳过 system —— system 每轮由引擎重建)
            _conversationHistory.Clear();
            foreach (var msg in result.Messages)
                if (msg.Role != LlmRoles.System)
                    _conversationHistory.Add(msg);
            TrimConversationHistory();

            return result;
        }
        finally
        {
            _runGate.Release();
        }
    }

    /// <summary>开启新会话：清空跨轮对话历史，后续 RunAsync 从空白上下文开始（不影响已存回滚快照）。</summary>
    public void StartNewConversation()
    {
        _conversationHistory.Clear();
        _conversationId = null;
    }

    /// <summary>会话历史裁剪：保留最近 N 条往返 + 总字符上限，控制 token 消耗。</summary>
    private void TrimConversationHistory()
    {
        const int MaxMessages = 24;
        const int MaxChars = 12000;

        while (_conversationHistory.Count > MaxMessages)
            _conversationHistory.RemoveAt(0);

        var chars = 0;
        foreach (var m in _conversationHistory) chars += m.Content.Length;
        while (chars > MaxChars && _conversationHistory.Count > 2)
        {
            var removed = _conversationHistory[0];
            _conversationHistory.RemoveAt(0);
            chars -= removed.Content.Length;
            // 尽量成对移除(user + assistant)，避免历史头尾角色失衡
            if (_conversationHistory.Count > 0 && chars > MaxChars)
            {
                removed = _conversationHistory[0];
                _conversationHistory.RemoveAt(0);
                chars -= removed.Content.Length;
            }
        }
    }

    /// <summary>写工具执行前的快照钩子（由工具闭包调用）。</summary>
    private void BeforeWrite(HmiProject project, string toolName) =>
        _currentTracker?.BeforeWrite(project, toolName);

    /// <summary>写工具成功后的刷新钩子（由工具闭包调用）。</summary>
    private void AfterWrite()
    {
        _bridge.MarkChanged();
        _bridge.RefreshPanels();
    }

    /// <summary>获取某次运行的快照追踪器。</summary>
    public AgentSnapshotTracker? GetTracker(string sessionId) =>
        _trackers.GetValueOrDefault(sessionId);

    /// <summary>
    /// 回滚某次运行的全部更改（恢复到该次运行首次写操作前的项目状态）。
    /// 返回是否成功。
    /// </summary>
    public bool Rollback(string sessionId)
    {
        if (!_trackers.TryGetValue(sessionId, out var tracker) || !tracker.CanRollback)
            return false;

        var restored = tracker.Restore();
        if (restored is null) return false;

        _bridge.ReplaceProject(restored);
        _bridge.RefreshPanels();
        _bridge.MarkChanged();
        _trackers.Remove(sessionId);
        return true;
    }

    public void Dispose()
    {
        _trackers.Clear();
        _engine = null;
        _provider = null;
    }
}
