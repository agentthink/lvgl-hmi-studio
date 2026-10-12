using HmiEditor.AI.Inference;

namespace HmiEditor.AI.Agent;

/// <summary>
/// Agent 引擎 —— 多轮工具调用循环。
/// 每轮: LLM 输出 tool_call / final_answer → 执行工具 → 结果回填 → 继续，直到 final_answer 或达到最大步骤。
/// 纯 JSON 输出协议，对 OpenAI / Claude / 中转兼容服务均可用。
/// </summary>
public sealed class AgentEngine
{
    private readonly ILlmProvider _provider;
    private readonly IReadOnlyList<AgentTool> _tools;
    private readonly Func<AgentToolContext> _contextFactory;
    private int _running;

    /// <summary>最大工具调用轮数，防止失控循环。</summary>
    public int MaxSteps { get; set; } = 30;

    /// <summary>当前运行会话 ID（由 RunAsync 设置，供工具上下文使用）。</summary>
    public string SessionId { get; private set; } = "";

    public AgentEngine(
        ILlmProvider provider,
        IEnumerable<AgentTool> tools,
        Func<AgentToolContext> contextFactory)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _tools = tools.ToArray();
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        if (_tools.Count == 0) throw new ArgumentException("至少需要注册一个工具", nameof(tools));
    }

    /// <summary>
    /// 运行 Agent 任务。
    /// </summary>
    /// <summary>当 Provider 不支持结构化输出时，强制使用 XML 风格协议（对弱模型更稳定）。</summary>
    public bool ForceXmlProtocol { get; set; }

    public async Task<AgentRunResult> RunAsync(
        string userInput,
        string sessionId,
        List<LlmMessage>? priorHistory = null,
        IProgress<AgentRunStep>? stepProgress = null,
        IProgress<string>? textProgress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            throw new ArgumentException("用户输入不能为空", nameof(userInput));
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("会话 ID 不能为空", nameof(sessionId));
        if (MaxSteps <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxSteps), "最大工具调用步数必须大于 0。");

        // 一个引擎实例对应一个 UI Agent 会话；禁止并发运行，避免 SessionId、历史和工具快照互相覆盖。
        if (Interlocked.Exchange(ref _running, 1) != 0)
            throw new InvalidOperationException("Agent 已有任务正在运行，请等待当前任务结束或先取消。");

        try
        {
            // 先设置会话 ID，再创建工具上下文；工具在首次调用前即可获得当前会话。
            SessionId = sessionId;
        var context = _contextFactory();
        var systemPrompt = AgentSystemPrompt.Build(_tools, context.Project, ForceXmlProtocol);

        // 会话记忆: system 每轮重建(工具清单/项目摘要可能变化), 历史 non-system 消息复用以支持跨轮指代与多轮对话。
        var messages = new List<LlmMessage>
        {
            new() { Role = LlmRoles.System, Content = systemPrompt }
        };
        if (priorHistory is { Count: > 0 })
            messages.AddRange(priorHistory);
        messages.Add(new() { Role = LlmRoles.User, Content = userInput });

        var result = new AgentRunResult();
        var parseErrors = 0;
        var aborted = false;

        for (var stepIndex = 0; stepIndex < MaxSteps; stepIndex++)
        {
            ct.ThrowIfCancellationRequested();

            var response = await LlmChat.WithProgressAsync(_provider, new LlmRequest
            {
                Messages = messages,
                JsonSchema = ForceXmlProtocol ? null : AgentOutput.Schema,
                Temperature = 0.2,
                MaxTokens = 8192
            }, textProgress, ct);

            result.TotalInputTokens += response.InputTokens;
            result.TotalOutputTokens += response.OutputTokens;

            var kind = AgentOutput.TryParse(response.Content, out var call, out var finalText);

            if (kind == AgentOutputKind.FinalAnswer)
            {
                result.FinalAnswer = finalText ?? "";
                result.FinalSummary = finalText ?? "";
                result.Completed = true;
                break;
            }

            if (kind == AgentOutputKind.ToolCall && call is not null)
            {
                parseErrors = 0;
                if (call.Arguments.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    var malformed = BuildErrorStep(call.Name, call.Arguments.GetRawText(),
                        "工具参数必须是 JSON 对象，请重新输出 arguments 对象。");
                    result.Steps.Add(malformed);
                    stepProgress?.Report(malformed);
                    messages.Add(new LlmMessage { Role = LlmRoles.Assistant, Content = response.Content });
                    messages.Add(new LlmMessage { Role = LlmRoles.User, Content = malformed.Summary });
                    continue;
                }

                var tool = _tools.FirstOrDefault(t => t.Name == call.Name);
                if (tool is null)
                {
                    var step = BuildErrorStep(call.Name, call.Arguments.GetRawText(),
                        $"工具 '{call.Name}' 不存在。可用工具: {string.Join(", ", _tools.Select(t => t.Name))}");
                result.Steps.Add(step);
                stepProgress?.Report(step);
                messages.Add(new LlmMessage { Role = LlmRoles.Assistant, Content = response.Content });
                messages.Add(new LlmMessage { Role = LlmRoles.User, Content = step.Summary + " 请重新输出 tool_call 或 final_answer。" });
                    continue;
                }

                var execStep = await ExecuteToolAsync(tool, call, ct);
                result.Steps.Add(execStep);
                stepProgress?.Report(execStep);

                messages.Add(new LlmMessage { Role = LlmRoles.Assistant, Content = response.Content });
                messages.Add(new LlmMessage { Role = LlmRoles.User, Content = $"工具 {call.Name} 执行结果: {execStep.Summary}" });
                continue;
            }

            // 输出无法解析 → 给模型一次修正机会（上限 2 次）
            if (++parseErrors > 2)
            {
                result.FinalAnswer = "模型输出无法解析为有效协议，任务已中止。请重试或简化请求。" +
                                     $"模型最后输出: {Truncate(response.Content, 200)}";
                result.Completed = false;
                aborted = true;
                break;
            }
            messages.Add(new LlmMessage { Role = LlmRoles.Assistant, Content = response.Content });
            messages.Add(new LlmMessage
            {
                Role = LlmRoles.User,
                Content = ForceXmlProtocol
                    ? "你的输出不符合协议：必须为 XML 风格工具调用，形如 <tool_call><function=工具名><parameter=参数名>值</parameter></function></tool_call> 或 <final_answer>总结</final_answer>。" +
                          $"你刚才输出的是: {Truncate(response.Content, 400)}。请只输出协议 XML，不要附带任何解释文字或 markdown。"
                    : "你的输出不符合协议：必须为纯 JSON，形如 {\"type\":\"tool_call\",\"tool\":\"...\",\"arguments\":{}} 或 {\"type\":\"final_answer\",\"text\":\"...\"}。" +
                          $"你刚才输出的是: {Truncate(response.Content, 400)}。请只输出协议 JSON，不要附带任何解释文字或 markdown。"
            });
        }

        // 兜底：循环耗尽仍未完成时给出准确文案。
        // 注意：不得覆盖 parseErrors 提前中止（aborted）时的具体原因。
        if (!result.Completed && !aborted)
        {
            result.FinalAnswer = result.Steps.Count > 0
                ? $"已达到最大步骤数({MaxSteps})，任务未完成。已执行 {result.Steps.Count} 步工具调用，最后一步: {result.Steps[^1].Summary}"
                : $"已达到最大步骤数({MaxSteps})，任务未完成。模型未产生任何有效的工具调用结果。";
        }

            result.MaxSteps = MaxSteps;
            result.Messages = messages;
            return result;
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    private static string Truncate(string s, int max)
    {
        s = s.Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }

    private async Task<AgentRunStep> ExecuteToolAsync(AgentTool tool, AgentToolCall call, CancellationToken ct)
    {
        var context = _contextFactory();
        try
        {
            var toolResult = await tool.ExecuteAsync(call, context, ct);
            return new AgentRunStep
            {
                ToolName = call.Name,
                Arguments = call.Arguments.GetRawText(),
                Ok = toolResult.Ok,
                Summary = toolResult.Summary,
                Error = toolResult.Error,
                Changed = toolResult.Changed && !tool.IsReadOnly
            };
        }
        catch (OperationCanceledException)
        {
            // 取消必须传播到宿主，不能被转换成普通工具失败，否则 UI 无法进入取消态。
            throw;
        }
        catch (Exception ex)
        {
            return BuildErrorStep(call.Name, call.Arguments.GetRawText(), $"工具执行异常: {ex.Message}");
        }
    }

    private static AgentRunStep BuildErrorStep(string toolName, string args, string message) => new()
    {
        ToolName = toolName,
        Arguments = args,
        Ok = false,
        Summary = message,
        Error = message
    };
}
