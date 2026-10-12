using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HmiEditor.AI;
using HmiEditor.AI.Agent;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.AI.Inference;
using HmiEditor.AI.Validation;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Views;

/// <summary>
/// AI 助手面板 —— 自然语言生成 HMI 屏幕的统一入口。
/// 交互流程: 配置 → 发送 → 生成 → 预览/应用/取消(非破坏性)。
/// </summary>
public partial class AiChatPanelView : UserControl
{
    /// <summary>预览请求: 在画布上临时显示生成结果。</summary>
    public event EventHandler<HmiScreen>? PreviewRequested;

    /// <summary>应用请求: 将生成结果正式加入项目。</summary>
    public event EventHandler<HmiScreen>? ApplyRequested;

    /// <summary>模板应用请求: 将生成的 Faceplate 模板加入模板库。</summary>
    public event EventHandler<ControlTemplate>? TemplateApplyRequested;

    /// <summary>脚本应用请求: 将生成的 Lua 脚本加入脚本列表。</summary>
    public event EventHandler<HmiEditor.Core.Models.Scripting.HmiScript>? ScriptApplyRequested;

    /// <summary>取消请求: 恢复预览前的状态。</summary>
    public event EventHandler? CancelRequested;

    /// <summary>配置变更事件(保存成功后)。</summary>
    public event EventHandler<LlmProviderConfig>? ConfigChanged;

    private readonly AiPreviewManager _manager = null!;
    private readonly ObservableCollection<ChatEntry> _messages = new();
    private bool _busy;
    private string? _lastAgentInput;

    /// <summary>
    /// 在途 AI 请求的取消源（Agent 与非 Agent 共用；<see cref="_busy"/> 保证同一时刻只有一个）。
    /// <para>此前 Agent 路径有 <c>_agentCts</c>、非 Agent 路径**一个 token 都不传** ——
    /// 同一个面板里"取消"时灵时不灵，且两个 CTS 字段各自管理生命周期（旧的从不 Dispose）。</para>
    /// </summary>
    private System.Threading.CancellationTokenSource? _cts;

    /// <summary>
    /// Agent 宿主（由 MainWindow 注入）。null 或未配置时 Agent 模式不可用。
    /// </summary>
    public AgentHost? AgentHost { get; set; }

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public AiChatPanelView()
    {
        InitializeComponent();
    }

    public AiChatPanelView(AiPreviewManager manager)
    {
        InitializeComponent();
        _manager = manager;

        MessageList.ItemsSource = _messages;

        if (_manager != null)
            LoadConfigIntoUi();

        // 转发事件到 DockLayoutState 聚合器，确保 Dock 重建后仍能到达 MainWindow
        PreviewRequested += (_, screen) => DockLayoutState.RaiseAiPreviewRequested(this, screen);
        ApplyRequested += (_, screen) => DockLayoutState.RaiseAiApplyRequested(this, screen);
        TemplateApplyRequested += (_, template) => DockLayoutState.RaiseAiTemplateApplyRequested(this, template);
        ScriptApplyRequested += (_, script) => DockLayoutState.RaiseAiScriptApplyRequested(this, script);
        CancelRequested += (_, _) => DockLayoutState.RaiseAiCancelRequested(this, EventArgs.Empty);

        // 注：NewChatBtn 的 Click 在 .axaml 里声明过（Click="OnNewChatClicked"），
        // 这里**不得**再 += 一次 —— Avalonia 的 += 不去重，两路都会执行（缺陷 #43）。
        SendBtn.Click += OnSendClicked;
        SaveConfigBtn.Click += OnSaveConfigClicked;
        PreviewBtn.Click += (_, _) => RequestPreview();
        ApplyBtn.Click += (_, _) => RequestApply();
        CancelBtn.Click += (_, _) => RequestCancel();
        StopBtn.Click += OnStopClicked;

        InputBox.KeyDown += (_, e) =>
        {
            // Ctrl+Enter 发送
            if (e.Key == Avalonia.Input.Key.Enter &&
                (e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) != 0)
            {
                e.Handled = true;
                OnSendClicked(this, new RoutedEventArgs());
            }
        };
    }

    /// <summary>当前项目的上下文引用(由 MainWindow 注入, 生成时读取变量/主题)。</summary>
    public HmiProject? CurrentProject { get; set; }

    private void LoadConfigIntoUi()
    {
        var config = _manager.Config;
        ProviderBox.SelectedIndex = config.Provider.Equals("claude", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        EndpointBox.Text = config.Endpoint ?? "";
        ModelBox.Text = config.Model;
        ApiKeyBox.Text = config.ApiKey;
        UpdateConfigStatus();
    }

    private void UpdateConfigStatus()
    {
        ConfigStatusText.Text = _manager.IsConfigured ? AppLocalization.Service[Lang.AiStatusConfigured] : AppLocalization.Service["gen.not.configured"];
        ConfigStatusText.Foreground = Avalonia.Media.Brush.Parse(_manager.IsConfigured ? "#22c55e" : "#f59e0b");
    }

    private void OnSaveConfigClicked(object? sender, RoutedEventArgs e)
    {
        var config = new LlmProviderConfig
        {
            Provider = ProviderBox.SelectedIndex == 1 ? "claude" : "openai",
            Endpoint = string.IsNullOrWhiteSpace(EndpointBox.Text) ? null : EndpointBox.Text.Trim(),
            Model = ModelBox.Text?.Trim() ?? "",
            ApiKey = ApiKeyBox.Text?.Trim() ?? ""
        };

        if (_manager.UpdateConfig(config))
        {
            ConfigStatusText.Text = AppLocalization.Service["ai.saved"];
            ConfigChanged?.Invoke(this, config);
        }
        else
        {
            ConfigStatusText.Text = AppLocalization.Service["ai.save.failed"];
            ConfigStatusText.Foreground = Avalonia.Media.Brush.Parse("#ef4444");
        }
    }

    // ── 在途请求的取消链路（Agent 与非 Agent 共用同一条） ──

    /// <summary>开始一次运行：登记取消源 + 亮出停止按钮，返回要往下传的 token。</summary>
    private System.Threading.CancellationToken BeginRun()
    {
        _cts?.Dispose(); // 防御性收尾：正常情况下 _busy 已挡住并发
        _cts = new System.Threading.CancellationTokenSource();
        StopBtn.IsVisible = true;
        return _cts.Token;
    }

    /// <summary>结束一次运行：释放取消源 + 收起停止按钮。</summary>
    private void EndRun()
    {
        _cts?.Dispose();
        _cts = null;
        StopBtn.IsVisible = false;
    }

    /// <summary>取消在途请求（停止按钮与 Agent 运行中按钮共用）。</summary>
    private void CancelInFlight() => _cts?.Cancel();

    private void OnStopClicked(object? sender, RoutedEventArgs e) => CancelInFlight();

    private async void OnSendClicked(object? sender, RoutedEventArgs e)
    {
        var input = InputBox.Text?.Trim();
        if (string.IsNullOrEmpty(input) || _busy) return;

        _messages.Add(new ChatEntry { RoleDisplay = AppLocalization.Service[Lang.AiRoleYou], Content = input });
        InputBox.Text = "";
        SendBtn.IsEnabled = false;
        _busy = true;

        // 非 Agent 路径的流式预览宿主（在下方 try 里赋值；外层 finally 负责收尾）
        ChatEntry? liveEntry = null;

        try
        {
            if (!_manager.IsConfigured)
            {
                _messages.Add(new ChatEntry
                {
                    RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                    Content = AppLocalization.Service["ai.expand.config"],
                    Detail = AppLocalization.Service[Lang.AiSupportHint]
                });
                return;
            }

            // 智能体模式：全程自动执行工具链，完成后展示轨迹并支持一键回滚
            if (AgentModeToggle.IsChecked == true)
            {
                if (AgentHost is { IsConfigured: true })
                {
                    await RunAgentAsync(input);
                    return;
                }

                _messages.Add(new ChatEntry
                {
                    RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                    Content = AppLocalization.Service["ai.agent.not.ready"]
                });
                return;
            }

            if (CurrentProject is null)
            {
                _messages.Add(new ChatEntry
                {
                    RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                    Content = AppLocalization.Service["gen.create.or.open.project"]
                });
                return;
            }

            var token = BeginRun();

            try
            {
                liveEntry = new ChatEntry { RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant], Content = AppLocalization.Service[Lang.AiGenerating] };
                _messages.Add(liveEntry);

                // 模型增量 → 运行中气泡的实时预览（节流重绘，理由同 Agent 路径）
                var streamBuffer = new System.Text.StringBuilder();
                var lastStreamRefresh = 0L;
                var textProgress = new Progress<string>(delta =>
                {
                    streamBuffer.Append(delta);
                    liveEntry.IsStreaming = true;
                    liveEntry.LivePreview = Tail(streamBuffer, 600);
                    liveEntry.StreamingStatus = AppLocalization.Service.Format(Lang.AiStreamingStatus, streamBuffer.Length);

                    var now = Environment.TickCount64;
                    if (now - lastStreamRefresh < 120) return;
                    lastStreamRefresh = now;
                    ForceRefreshMessages();
                    MessageList.ScrollIntoView(liveEntry);
                });

                // 意图分类: 模板意图 → 生成 Faceplate, 脚本意图 → 生成 Lua, 图片意图 → 生成 SVG, 否则生成屏幕
                var intent = AiOrchestrator.ClassifyIntent(input);
                if (intent == AiOrchestrator.AiIntent.Template)
                {
                    var template = await _manager.GenerateTemplateAsync(input, CurrentProject, token, textProgress);
                    UpdatePendingTemplateMessage(template);
                }
                else if (intent == AiOrchestrator.AiIntent.Script)
                {
                    var script = await _manager.GenerateScriptAsync(input, CurrentProject, token, textProgress);
                    UpdatePendingScriptMessage(script);
                }
                else if (intent == AiOrchestrator.AiIntent.Image)
                {
                    var svg = await _manager.GenerateImageAsync(input, CurrentProject, token, textProgress);
                    UpdatePendingImageMessage(svg);
                }
                else
                {
                    var screen = await _manager.GenerateScreenAsync(input, CurrentProject, token, textProgress);
                    UpdatePendingMessage(screen);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // 用户点了停止：这是预期结果，不能报"生成失败"。
                _manager.ClearPending();
                ActionBar.IsVisible = false;
                _messages.Add(new ChatEntry
                {
                    RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                    Content = AppLocalization.Service[Lang.AiCancelled]
                });
            }
        }
        catch (Exception ex)
        {
            // 错误处理: 完整异常链展示给用户(便于定位 API 状态码/响应体/解析失败),
            // 同时写调试日志, 避免"网络错误"这类泛化文案掩盖真实原因。
            var detail = BuildAiErrorDetail(ex);
            System.Diagnostics.Debug.WriteLine($"[AI] 生成失败: {ex}");

            // 清理待处理状态, 避免残留上次成功的 Pending 导致 ActionBar 误操作旧结果
            _manager.ClearPending();
            ActionBar.IsVisible = false;

            _messages.Add(new ChatEntry
            {
                RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                Content = AppLocalization.Service.Format("ai.gen.failed", ExternalText.FailureReason(ex)),
                Detail = string.IsNullOrEmpty(detail)
                    ? AppLocalization.Service[Lang.AiErrNetwork]
                    : detail
            });
        }
        finally
        {
            // 生成已结束：撤掉运行中气泡的流式预览（结果在紧随其后的新气泡里）
            if (liveEntry is not null)
            {
                liveEntry.IsStreaming = false;
                liveEntry.LivePreview = null;
                liveEntry.StreamingStatus = null;
                ForceRefreshMessages();
            }

            _busy = false;
            SendBtn.IsEnabled = true;
            EndRun();
            MessageList.ScrollIntoView(_messages.LastOrDefault()!);
        }
    }

    /// <summary>运行智能体任务：先创建可更新的占位条目，逐步推送工具轨迹，跑完再填最终总结。</summary>
    private async Task RunAgentAsync(string input)
    {
        _lastAgentInput = input;
        var agentEntry = new ChatEntry
        {
            RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
            Content = AppLocalization.Service["ai.agent.running"],
            Steps = new ObservableCollection<ChatAgentStep>(),
            IsRunning = true
        };
        var token = BeginRun();
        _messages.Add(agentEntry);
        MessageList.ScrollIntoView(agentEntry);

        // 流式正文预览的状态。全部只在 UI 线程上读写（Progress 在捕获的同步上下文回调）。
        var streamBuffer = new System.Text.StringBuilder();
        var lastStreamRefresh = 0L;

        // 逐步轨迹：每步工具执行完即回推到 UI(Progress 在捕获的 UI 同步上下文回调)
        var progress = new Progress<AgentRunStep>(step =>
        {
            // 这一步意味着「上一轮模型输出已解析完」⇒ 撤掉该轮的流式预览，
            // 否则预览里的原始 JSON 会一直挂在气泡上，与下面的步骤卡片重复。
            streamBuffer.Clear();
            agentEntry.IsStreaming = false;
            agentEntry.LivePreview = null;
            agentEntry.StreamingStatus = null;

            agentEntry.Steps!.Add(new ChatAgentStep
            {
                ToolDisplay = AgentToolFactory.ToolDisplayNames.TryGetValue(step.ToolName, out var display)
                    ? display
                    : step.ToolName,
                Arguments = step.Arguments,
                Result = step.Error ?? step.Summary,
                Ok = step.Ok,
                Changed = step.Changed
            });
            MessageList.ScrollIntoView(agentEntry);
        });

        // 模型正文增量：实时预览 + 节流重绘。
        // ForceRefreshMessages 会重挂 ItemsSource（ChatEntry 无 INPC），逐字调用会把 UI 拖垮，
        // 因此按时间节流；最后一次增量由 finally 兜底刷新。
        var textProgress = new Progress<string>(delta =>
        {
            streamBuffer.Append(delta);
            agentEntry.IsStreaming = true;
            agentEntry.LivePreview = Tail(streamBuffer, 600);
            agentEntry.StreamingStatus = AppLocalization.Service.Format(Lang.AiStreamingStatus, streamBuffer.Length);

            var now = Environment.TickCount64;
            if (now - lastStreamRefresh < 120) return;
            lastStreamRefresh = now;
            ForceRefreshMessages();
            MessageList.ScrollIntoView(agentEntry);
        });

        try
        {
            var run = await AgentHost!.RunAsync(input, progress, textProgress, token);

            var tokenUsage = run.TotalTokens > 0
                ? AppLocalization.Service.Format(Lang.AiTokenUsage, run.TotalInputTokens, run.TotalOutputTokens)
                : null;
            var stepsInfo = AppLocalization.Service.Format(Lang.AiChatSteps, run.Steps.Count, run.MaxSteps);
            var detail = string.IsNullOrEmpty(tokenUsage) ? stepsInfo : $"{stepsInfo} · {tokenUsage}";

            // Agent 生成的图片
            if (AgentHost!.GeneratedImages.Count > 0)
            {
                var last = AgentHost.GeneratedImages[^1];
                agentEntry.ImageSvg = last.Svg;
                agentEntry.ImageWidth = last.Width;
                agentEntry.ImageHeight = last.Height;
            }

            agentEntry.Content = string.IsNullOrWhiteSpace(run.FinalAnswer)
                ? (run.HasChanges ? AppLocalization.Service[Lang.AiChatTaskDone] : AppLocalization.Service[Lang.AiChatTaskFailed])
                : run.FinalAnswer;
            agentEntry.AgentSessionId = run.SessionId;
            agentEntry.AgentHasChanges = run.HasChanges;
            agentEntry.Detail = detail;
            // 非集合属性(Content/AgentHasChanges 等)无 INPC，需重挂触发重绘
            ForceRefreshMessages();
            MessageList.ScrollIntoView(agentEntry);
        }
        catch (OperationCanceledException)
        {
            agentEntry.Content = AppLocalization.Service[Lang.AiCancelled];
            ForceRefreshMessages();
            MessageList.ScrollIntoView(agentEntry);
        }
        catch (Exception ex)
        {
            var detail = BuildAiErrorDetail(ex);
            System.Diagnostics.Debug.WriteLine($"[Agent] 运行失败: {ex}");
            agentEntry.Content = AppLocalization.Service.Format("ai.gen.failed", ExternalText.FailureReason(ex));
            agentEntry.Detail = string.IsNullOrEmpty(detail) ? AppLocalization.Service[Lang.AiErrNetwork] : detail;
            agentEntry.CanRetry = true;
            ForceRefreshMessages();
            MessageList.ScrollIntoView(agentEntry);
        }
        finally
        {
            // 运行结束：撤掉流式预览（最终正文已在 Content 里），并收起运行中状态
            agentEntry.IsStreaming = false;
            agentEntry.LivePreview = null;
            agentEntry.StreamingStatus = null;
            agentEntry.IsRunning = false;
            EndRun();
            _busy = false;
            SendBtn.IsEnabled = true;
            ForceRefreshMessages();
            MessageList.ScrollIntoView(_messages.LastOrDefault()!);
        }
    }

    /// <summary>新会话：取消在途请求、清空 Agent 跨轮上下文与界面消息。</summary>
    private void OnNewChatClicked(object? sender, RoutedEventArgs e)
    {
        CancelInFlight();
        AgentHost?.StartNewConversation();
        _messages.Clear();
        ActionBar.IsVisible = false;
    }

    /// <summary>
    /// 重试上次失败的 Agent 任务(复用上次输入)。
    /// <see cref="RunAgentAsync"/> 内部已把失败写进会话气泡，所以这里只需保证
    /// 异常不逃逸到 async void 的兜底路径（日志仍会由 UiOperationGuard 落盘）。
    /// </summary>
    private void OnRetryClicked(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpRetryAgent, async () =>
        {
            if (string.IsNullOrEmpty(_lastAgentInput) || _busy) return;
            await RunAgentAsync(_lastAgentInput);
        });

    /// <summary>取消当前正在运行的 Agent 任务。</summary>
    private void OnCancelAgentClicked(object? sender, RoutedEventArgs e) => CancelInFlight();

    /// <summary>一键回滚：恢复本次 Agent 运行前的项目状态。</summary>
    private void OnRollbackClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ChatEntry entry } btn ||
            entry.AgentSessionId is not { } sessionId ||
            AgentHost is not { } host)
        {
            return;
        }

        if (host.Rollback(sessionId))
        {
            entry.AgentHasChanges = false;
            ForceRefreshMessages();
            _messages.Add(new ChatEntry
            {
                RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                Content = AppLocalization.Service["ai.agent.rolled.back"]
            });
            MessageList.ScrollIntoView(_messages.LastOrDefault()!);
        }
    }

    /// <summary>强制刷新消息列表（ChatEntry 无 INPC，需重挂 ItemsSource 触发模板重绘）。</summary>
    private void ForceRefreshMessages()
    {
        var items = MessageList.ItemsSource;
        MessageList.ItemsSource = null;
        MessageList.ItemsSource = items;
    }

    /// <summary>
    /// 取流式预览的尾部窗口。模型一轮可能吐出几十 KB（整屏 JSON），
    /// 把全文塞进一个 <c>SelectableTextBlock</c> 只会拖慢布局 —— 预览只看"最近在写什么"。
    /// </summary>
    private static string Tail(System.Text.StringBuilder sb, int max)
    {
        if (sb.Length <= max) return sb.ToString();
        return "…" + sb.ToString(sb.Length - max, max);
    }

    private void UpdatePendingMessage(HmiScreen? screen)
    {
        var pending = _manager.PendingValidation;
        if (screen is null && pending is not null && pending.HasErrors)
        {
            _messages.Add(new ChatEntry
            {
                RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                Content = AppLocalization.Service[Lang.AiErrValidationScreen],
                Detail = string.Join("\n", pending.Issues.Select(i => "• " + ExternalText.Describe(i)))
            });
            ActionBar.IsVisible = false;
            return;
        }

        if (screen is null)
        {
            _messages.Add(new ChatEntry
            {
                RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                Content = AppLocalization.Service[Lang.AiErrInvalidScreen]
            });
            ActionBar.IsVisible = false;
            return;
        }

        var warningText = pending?.HasWarnings == true
            ? "\n\n" + AppLocalization.Service.Format(Lang.AiWarningCount, pending!.Issues.Count(i => i.Severity == AiValidationSeverity.Warning)) + ":\n" +
              string.Join("\n", pending.Issues
                  .Where(i => i.Severity == AiValidationSeverity.Warning)
                  .Select(i => "• " + ExternalText.Describe(i)))
            : "";

        _messages.Add(new ChatEntry
        {
            RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
            Content = AppLocalization.Service.Format(Lang.AiGeneratedScreen, screen.Name, screen.Controls.Count, warningText),
            Detail = _manager.LastTokens.Input + _manager.LastTokens.Output > 0
                ? AppLocalization.Service.Format(Lang.AiTokenUsage, _manager.LastTokens.Input, _manager.LastTokens.Output)
                : null
        });
        ActionBar.IsVisible = true;
        MessageList.ScrollIntoView(_messages.Last());
    }

    private void UpdatePendingTemplateMessage(ControlTemplate? template)
    {
        var pending = _manager.PendingValidation;
        if (template is null && pending is not null && pending.HasErrors)
        {
            _messages.Add(new ChatEntry
            {
                RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                Content = AppLocalization.Service[Lang.AiErrValidationTemplate],
                Detail = string.Join("\n", pending.Issues.Select(i => "• " + ExternalText.Describe(i)))
            });
            ActionBar.IsVisible = false;
            return;
        }

        if (template is null)
        {
            _messages.Add(new ChatEntry
            {
                RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                Content = AppLocalization.Service[Lang.AiErrInvalidTemplate]
            });
            ActionBar.IsVisible = false;
            return;
        }

        var warningText = pending?.HasWarnings == true
            ? "\n\n" + AppLocalization.Service.Format(Lang.AiWarningCount, pending!.Issues.Count(i => i.Severity == AiValidationSeverity.Warning)) + ":\n" +
              string.Join("\n", pending.Issues
                  .Where(i => i.Severity == AiValidationSeverity.Warning)
                  .Select(i => "• " + ExternalText.Describe(i)))
            : "";

        _messages.Add(new ChatEntry
        {
            RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
            Content = AppLocalization.Service.Format(Lang.AiGeneratedTemplate, template.Name, template.Controls.Count, template.Parameters.Count, warningText),
            Detail = _manager.LastTokens.Input + _manager.LastTokens.Output > 0
                ? AppLocalization.Service.Format(Lang.AiTokenUsage, _manager.LastTokens.Input, _manager.LastTokens.Output)
                : null
        });
        ActionBar.IsVisible = true;
        MessageList.ScrollIntoView(_messages.Last());
    }

    private void UpdatePendingScriptMessage(HmiEditor.Core.Models.Scripting.HmiScript? script)
    {
        var pending = _manager.PendingValidation;
        if (script is null && pending is not null && pending.HasErrors)
        {
            _messages.Add(new ChatEntry
            {
                RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                Content = AppLocalization.Service[Lang.AiErrValidationScript],
                Detail = string.Join("\n", pending.Issues.Select(i => "• " + ExternalText.Describe(i)))
            });
            ActionBar.IsVisible = false;
            return;
        }

        if (script is null)
        {
            _messages.Add(new ChatEntry
            {
                RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                Content = AppLocalization.Service[Lang.AiErrInvalidScript]
            });
            ActionBar.IsVisible = false;
            return;
        }

        _messages.Add(new ChatEntry
        {
            RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
            Content = AppLocalization.Service.Format(Lang.AiGeneratedScript, script.Name, script.Category, script.Code.Length),
            Detail = _manager.LastTokens.Input + _manager.LastTokens.Output > 0
                ? AppLocalization.Service.Format(Lang.AiTokenUsage, _manager.LastTokens.Input, _manager.LastTokens.Output)
                : null
        });
        ActionBar.IsVisible = true;
        MessageList.ScrollIntoView(_messages.Last());
    }

    private void UpdatePendingImageMessage(string? svg)
    {
        var pending = _manager.PendingValidation;
        if (string.IsNullOrEmpty(svg) && pending is not null && pending.HasErrors)
        {
            _messages.Add(new ChatEntry
            {
                RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                Content = AppLocalization.Service[Lang.AiErrValidationScreen],
                Detail = string.Join("\n", pending.Issues.Select(i => "• " + ExternalText.Describe(i)))
            });
            ActionBar.IsVisible = false;
            return;
        }

        if (string.IsNullOrEmpty(svg))
        {
            _messages.Add(new ChatEntry
            {
                RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
                Content = AppLocalization.Service[Lang.AiChatImageFailed]
            });
            ActionBar.IsVisible = false;
            return;
        }

        var (w, h) = _manager.PendingImageSize;
        _messages.Add(new ChatEntry
        {
            RoleDisplay = AppLocalization.Service[Lang.AiRoleAssistant],
            Content = AppLocalization.Service.Format(Lang.AiChatImageGenerated, w, h),
            ImageSvg = svg,
            ImageWidth = w,
            ImageHeight = h,
            Detail = _manager.LastTokens.Input + _manager.LastTokens.Output > 0
                ? AppLocalization.Service.Format(Lang.AiTokenUsage, _manager.LastTokens.Input, _manager.LastTokens.Output)
                : null
        });
        ActionBar.IsVisible = false;
        MessageList.ScrollIntoView(_messages.Last());
    }

    private void RequestPreview()
    {
        if (_manager.PendingScreen is not { } screen) return;
        PreviewRequested?.Invoke(this, screen);
    }

    private void RequestApply()
    {
        if (_manager.PendingScreen is { } screen)
        {
            ApplyRequested?.Invoke(this, screen);
            ClearPendingState();
        }
        else if (_manager.PendingTemplate is { } template)
        {
            TemplateApplyRequested?.Invoke(this, template);
            ClearPendingState();
        }
        else if (_manager.PendingScript is { } script)
        {
            ScriptApplyRequested?.Invoke(this, script);
            ClearPendingState();
        }
    }

    private void RequestCancel()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
        ClearPendingState();
    }

    private void ClearPendingState()
    {
        _manager.ClearPending();
        ActionBar.IsVisible = false;
    }

    /// <summary>保存 SVG 文件。</summary>
    private async void OnSaveSvgClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ChatEntry entry } || !entry.HasImage) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = AppLocalization.Service[Lang.AiImageSaveSvg],
            DefaultExtension = "svg",
            SuggestedFileName = $"hmi_image_{DateTime.Now:yyyyMMdd_HHmmss}",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(AppLocalization.Service[Lang.AiIconFilterSvg]) { Patterns = new[] { "*.svg" } }
            }
        });

        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new System.IO.StreamWriter(stream);
            await writer.WriteAsync(entry.ImageSvg!);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AI] 保存 SVG 失败: {ex.Message}");
        }
    }

    /// <summary>保存 PNG 文件。</summary>
    private async void OnSavePngClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ChatEntry entry } || !entry.HasImage) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = AppLocalization.Service[Lang.AiImageSavePng],
            DefaultExtension = "png",
            SuggestedFileName = $"hmi_image_{DateTime.Now:yyyyMMdd_HHmmss}",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(AppLocalization.Service[Lang.AiIconFilterPng]) { Patterns = new[] { "*.png" } }
            }
        });

        if (file is null) return;

        try
        {
            var w = entry.ImageWidth > 0 ? entry.ImageWidth : 200;
            var h = entry.ImageHeight > 0 ? entry.ImageHeight : 200;
            var bitmap = SvgRasterizer.RenderToBitmap(entry.ImageSvg!, w, h);
            if (bitmap is null) return;

            await using var stream = await file.OpenWriteAsync();
            bitmap.Save(stream, new PngBitmapEncoderOptions());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AI] 保存 PNG 失败: {ex.Message}");
        }
    }

    /// <summary>拼接异常链摘要(最多 3 层), 供界面展示定位根因。</summary>
    private static string BuildAiErrorDetail(Exception ex)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (string.IsNullOrWhiteSpace(e.Message)) continue;
            parts.Add(e.Message);
            if (parts.Count >= 3) break;
        }
        return string.Join(" → ", parts);
    }
}
