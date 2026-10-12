using System.Text.Json;
using System.Net.Http;
using HmiEditor.AI.Agent;
using HmiEditor.AI.Agent.Tools;
using HmiEditor.AI.Inference;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// Agent 引擎多轮工具调用循环测试：tool_call → 执行 → 结果回填 → final_answer。
/// </summary>
public class AgentEngineTests
{
    private static AgentTool WriteTool() => new(
        "create_test_item",
        "创建一个测试条目（写操作）。",
        execute: (_, ctx, _) =>
        {
            ctx.Project.Name = "changed-by-agent";
            return Task.FromResult(AgentToolResult.Success("已创建测试条目", true));
        });

    [Fact]
    public async Task RunAsync_RejectsInvalidSessionAndStepConfiguration()
    {
        var provider = new FakeLlmProvider(_ =>
            """{"type":"final_answer","text":"完成"}""");
        var engine = new AgentEngine(provider, new[] { WriteTool() },
            () => new AgentToolContext { Project = new HmiProject(), SessionId = "invalid" });

        await Assert.ThrowsAsync<ArgumentException>(() => engine.RunAsync("任务", ""));

        engine.MaxSteps = 0;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => engine.RunAsync("任务", "valid"));
    }

    [Fact]
    public async Task RunAsync_SetsSessionIdBeforeCreatingContext()
    {
        var observedSessionId = "";
        var provider = new FakeLlmProvider(_ =>
            """{"type":"final_answer","text":"完成","summary":"ok"}""");
        var project = new HmiProject();
        AgentEngine? engine = null;
        // 使用闭包模拟宿主从 AgentEngine.SessionId 读取工具上下文。
        engine = new AgentEngine(provider, new[] { WriteTool() }, () =>
        {
            observedSessionId = engine!.SessionId;
            return new AgentToolContext { Project = project, SessionId = engine.SessionId };
        });
        var result = await engine.RunAsync("检查项目", "session-current");

        Assert.True(result.Completed);
        Assert.Equal("session-current", observedSessionId);
    }

    [Fact]
    public async Task RunAsync_PropagatesCancellationFromTool()
    {
        var provider = new FakeLlmProvider(_ =>
            """{"type":"tool_call","tool":"cancelable","arguments":{}}""");
        var tool = new AgentTool(
            "cancelable",
            "可取消测试工具",
            execute: async (_, _, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return AgentToolResult.Success("不应到达");
            });
        var engine = new AgentEngine(provider, new[] { tool },
            () => new AgentToolContext { Project = new HmiProject(), SessionId = "cancel" });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.RunAsync("执行可取消工具", "cancel", ct: cts.Token));
    }

    [Fact]
    public async Task RunAsync_RejectsConcurrentExecution()
    {
        var provider = new FakeLlmProvider(_ =>
            """{"type":"final_answer","text":"完成","summary":"ok"}""");
        var engine = new AgentEngine(provider, new[] { WriteTool() },
            () => new AgentToolContext { Project = new HmiProject(), SessionId = "concurrent" });

        // 第一个运行在 Provider 返回前保持挂起，第二个运行必须立即拒绝。
        var firstGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedProvider = new BlockingProvider(firstGate);
        engine = new AgentEngine(blockedProvider, new[] { WriteTool() },
            () => new AgentToolContext { Project = new HmiProject(), SessionId = "concurrent" });
        var first = engine.RunAsync("第一个任务", "run-1");
        await blockedProvider.Started.Task;

        var second = engine.RunAsync("第二个任务", "run-2");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => second);
        Assert.Contains("已有任务正在运行", error.Message);

        firstGate.SetResult(true);
        var completed = await first;
        Assert.True(completed.Completed);
    }

    [Fact]
    public async Task RunAsync_RejectsNonObjectToolArgumentsAndLetsModelRecover()
    {
        var calls = 0;
        var provider = new FakeLlmProvider(_ => ++calls == 1
            ? "{\"type\":\"tool_call\",\"tool\":\"create_test_item\",\"arguments\":[1,2]}"
            : "{\"type\":\"final_answer\",\"text\":\"已修正\"}");
        var engine = new AgentEngine(provider, new[] { WriteTool() },
            () => new AgentToolContext { Project = new HmiProject(), SessionId = "args" });

        var result = await engine.RunAsync("执行任务", "args");

        Assert.True(result.Completed);
        var step = Assert.Single(result.Steps);
        Assert.False(step.Ok);
        Assert.Contains("JSON 对象", step.Summary);
    }

    [Fact]
    public async Task RunAsync_ExecutesToolCall_ThenFinalAnswer()
    {
        var callCount = 0;
        var provider = new FakeLlmProvider(_ =>
        {
            callCount++;
            return callCount == 1
                ? """{"type":"tool_call","tool":"create_test_item","arguments":{}}"""
                : """{"type":"final_answer","text":"任务完成","summary":"done"}""";
        });

        var project = new HmiProject();
        var engine = new AgentEngine(provider, new[] { WriteTool() },
            () => new AgentToolContext { Project = project, SessionId = "s1" });

        var result = await engine.RunAsync("帮我创建测试条目", "s1");

        Assert.True(result.Completed);
        Assert.Single(result.Steps);
        Assert.Equal("create_test_item", result.Steps[0].ToolName);
        Assert.True(result.Steps[0].Ok);
        Assert.True(result.Steps[0].Changed);
        Assert.Equal("任务完成", result.FinalAnswer);
        Assert.Equal("changed-by-agent", project.Name);
        Assert.True(result.TotalTokens > 0);
    }

    [Fact]
    public async Task RunAsync_UnknownTool_GivesModelCorrectionChance()
    {
        var callCount = 0;
        var provider = new FakeLlmProvider(_ =>
        {
            callCount++;
            return callCount switch
            {
                1 => """{"type":"tool_call","tool":"no_such_tool","arguments":{}}""",
                2 => """{"type":"tool_call","tool":"create_test_item","arguments":{}}""",
                _ => """{"type":"final_answer","text":"好了","summary":"ok"}"""
            };
        });

        var project = new HmiProject();
        var engine = new AgentEngine(provider, new[] { WriteTool() },
            () => new AgentToolContext { Project = project, SessionId = "s2" });

        var result = await engine.RunAsync("任务", "s2");

        Assert.Equal(2, result.Steps.Count);
        Assert.False(result.Steps[0].Ok);       // 未知工具步骤失败
        Assert.True(result.Steps[1].Ok);        // 修正后成功
        Assert.True(result.Completed);
    }

    [Fact]
    public async Task RunAsync_QueryTool_IsReadOnly_NotChanged()
    {
        var provider = new FakeLlmProvider(req =>
            req.Messages.Count(m => m.Role == LlmRoles.User) == 1
                ? """{"type":"tool_call","tool":"list_screens","arguments":{}}"""
                : """{"type":"final_answer","text":"完成","summary":"ok"}""");

        var project = new HmiProject();
        var tools = ProjectQueryTools.Create().Append(WriteTool()).ToArray();
        var engine = new AgentEngine(provider, tools,
            () => new AgentToolContext { Project = project, SessionId = "s3" });

        var result = await engine.RunAsync("列出屏幕", "s3");

        var queryStep = Assert.Single(result.Steps);
        Assert.Equal("list_screens", queryStep.ToolName);
        Assert.False(queryStep.Changed);        // 查询工具不标记变更
        Assert.Equal("Untitled", project.Name); // 项目未被修改
    }

    [Fact]
    public async Task RunAsync_TruncatedXmlToolCall_ExecutesAndCompletes()
    {
        // 回归：模型输出残缺 XML（<tool_call> 开标签丢失，仅剩 </tool_call> 结尾），
        // 引擎应能解析并执行 list_screens，随后正常完成，而非报"无法解析为有效协议"。
        var provider = new FakeLlmProvider(req =>
            req.Messages.Count(m => m.Role == LlmRoles.User) == 1
                ? """
                  <function=list_screens>
                  </function>
                  </tool_call>
                  """
                : """{"type":"final_answer","text":"完成","summary":"ok"}""");

        var project = new HmiProject();
        var tools = ProjectQueryTools.Create().Append(WriteTool()).ToArray();
        var engine = new AgentEngine(provider, tools,
            () => new AgentToolContext { Project = project, SessionId = "s5" });

        var result = await engine.RunAsync("列出屏幕", "s5");

        Assert.True(result.Completed);          // 未被误判为无法解析/最大步数
        var queryStep = Assert.Single(result.Steps);
        Assert.Equal("list_screens", queryStep.ToolName);
        Assert.True(queryStep.Ok);
    }

    [Fact]
    public void TryParse_RejectsInvalidOutput_AndAcceptsToolCall()
    {
        Assert.Null(AgentOutput.TryParse("not json at all", out _, out _));

        var kind = AgentOutput.TryParse(
            """{"type":"tool_call","tool":"create_screen","arguments":{"name":"主界面"}}""",
            out var call, out _);
        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.Equal("create_screen", call!.Name);
        Assert.Equal("主界面", AgentOutput.GetString(call.Arguments, "name"));

        // 容错 markdown 围栏
        var kind2 = AgentOutput.TryParse(
            "```json\n{\"type\":\"final_answer\",\"text\":\"ok\"}\n```",
            out _, out var text);
        Assert.Equal(AgentOutputKind.FinalAnswer, kind2);
        Assert.Equal("ok", text);
    }

    [Fact]
    public void TryParse_ArgumentsOptional_DefaultsEmpty()
    {
        var kind = AgentOutput.TryParse(
            """{"type":"tool_call","tool":"create_screen"}""",
            out var call, out _);
        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.Equal(JsonValueKind.Object, call!.Arguments.ValueKind);
        Assert.Equal("", AgentOutput.GetString(call.Arguments, "name", ""));
    }

    [Fact]
    public void TryParse_ExtractsJsonFromPrefixedText()
    {
        // 中转服务无 response_format 强约束时，模型常输出带解释前缀的文本
        var kind = AgentOutput.TryParse(
            "好的，我来创建屏幕：{\"type\":\"tool_call\",\"tool\":\"create_screen\",\"arguments\":{\"name\":\"主界面\"}} 请确认",
            out var call, out _);
        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.Equal("create_screen", call!.Name);
        Assert.Equal("主界面", AgentOutput.GetString(call.Arguments, "name"));
    }

    [Fact]
    public void TryParse_PicksProtocolJsonAmongMultipleCandidates()
    {
        // 思维链场景：模型先输出一段"思考"JSON，再输出协议 JSON → 应命中后者
        var kind = AgentOutput.TryParse(
            "让我想想：{\"reasoning\":\"需要先查询项目\",\"steps\":1} 然后执行：{\"type\":\"tool_call\",\"tool\":\"list_screens\",\"arguments\":{}}",
            out var call, out _);
        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.Equal("list_screens", call!.Name);
    }

    [Fact]
    public void TryParse_AcceptsClaudeXmlToolCall()
    {
        // 用户实际遇到的输出：Claude/中转模型 XML 风格工具调用（OpenAI 兼容通道原样返回）
        var kind = AgentOutput.TryParse(
            """
            <tool_call>
            <function=open_panel>
            <parameter=panel_name>
            Assets
            </parameter>
            </function>
            </tool_call>
            """,
            out var call, out _);
        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.Equal("open_panel", call!.Name);
        Assert.Equal("Assets", AgentOutput.GetString(call.Arguments, "panel_name"));
    }

    [Fact]
    public void TryParse_AcceptsXmlAttributeStyle()
    {
        // XML 属性风格（<function name=".."><parameter name="..">..</parameter></function>）
        var kind = AgentOutput.TryParse(
            "<tool_call><function name=\"create_screen\"><parameter name=\"name\">主界面</parameter></function></tool_call>",
            out var call, out _);
        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.Equal("create_screen", call!.Name);
        Assert.Equal("主界面", AgentOutput.GetString(call.Arguments, "name"));
    }

    [Fact]
    public void TryParse_AcceptsTruncatedXmlToolCall_MissingOpenTag()
    {
        // 回归：模型输出被截断，<tool_call> 开标签丢失，仅剩 </tool_call> 结尾 + function 块
        var kind = AgentOutput.TryParse(
            """
            <function=list_screens>
            </function>
            </tool_call>
            """,
            out var call, out _);
        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.Equal("list_screens", call!.Name);
    }

    [Fact]
    public void TryParse_AcceptsOrphanFunctionBlock_NoWrapperTags()
    {
        // 回归：无任何 <tool_call> 包裹，整段输出就是 function 调用（截断最严重场景）
        var kind = AgentOutput.TryParse(
            "<function=open_panel><parameter=panel_name>Assets</parameter></function>",
            out var call, out _);
        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.Equal("open_panel", call!.Name);
        Assert.Equal("Assets", AgentOutput.GetString(call.Arguments, "panel_name"));
    }

    [Fact]
    public void TryParse_IgnoresBracesInsideStrings()
    {
        // 提取 JSON 时必须跳过字符串值内的花括号（如脚本代码含 { }）
        var kind = AgentOutput.TryParse(
            "{\"type\":\"final_answer\",\"text\":\"if x then y={1,2} end\"}",
            out _, out var text);
        Assert.Equal(AgentOutputKind.FinalAnswer, kind);
        Assert.Equal("if x then y={1,2} end", text);
    }

    [Fact]
    public async Task RunAsync_UnparseableOutput_ReportsParseFailure_NotMaxSteps()
    {
        // 回归：模型连续输出无法解析内容时，应报告"无法解析"而非误导性的"最大步数"
        var provider = new FakeLlmProvider(_ => "这不是 JSON，我是来解释一下的……");
        var project = new HmiProject();
        var engine = new AgentEngine(provider, new[] { WriteTool() },
            () => new AgentToolContext { Project = project, SessionId = "s4" });

        var result = await engine.RunAsync("任务", "s4");

        Assert.False(result.Completed);
        Assert.Empty(result.Steps);
        Assert.Contains("无法解析", result.FinalAnswer);
        Assert.DoesNotContain("已达到最大步骤数", result.FinalAnswer);
    }

    [Fact]
    public async Task RunAsync_StepsExhausted_ReportsExecutedSteps()
    {
        // 回归：工具一直在执行但模型始终不输出 final_answer → 耗尽步数时应说明已执行的步骤
        var provider = new FakeLlmProvider(_ =>
            """{"type":"tool_call","tool":"create_test_item","arguments":{}}""");
        var project = new HmiProject();
        var engine = new AgentEngine(provider, new[] { WriteTool() },
            () => new AgentToolContext { Project = project, SessionId = "s5" });

        var result = await engine.RunAsync("任务", "s5");

        Assert.False(result.Completed);
        Assert.Equal(30, result.Steps.Count); // 默认 MaxSteps
        Assert.Contains("已达到最大步骤数", result.FinalAnswer);
        Assert.Contains("已创建测试条目", result.FinalAnswer); // 包含最后一步执行结果
    }

    [Fact]
    public async Task RunAsync_WithPriorHistory_IncludesContextMessages()
    {
        // 会话记忆：传入历史(user/assistant)后，引擎发送给 LLM 的 messages 应包含这些历史，
        // 以支持跨轮指代与多轮对话（P0-1）。
        string? seenHistoryUser = null;
        var provider = new FakeLlmProvider(req =>
        {
            seenHistoryUser = req.Messages
                .FirstOrDefault(m => m.Role == LlmRoles.User && m.Content == "之前的请求")?
                .Content;
            return """{"type":"final_answer","text":"done","summary":"ok"}""";
        });

        var project = new HmiProject();
        var engine = new AgentEngine(provider, new[] { WriteTool() },
            () => new AgentToolContext { Project = project, SessionId = "conv1" });

        var priorHistory = new List<LlmMessage>
        {
            new() { Role = LlmRoles.User, Content = "之前的请求" },
            new() { Role = LlmRoles.Assistant, Content = """{"type":"final_answer","text":"之前的结果"}""" }
        };

        var result = await engine.RunAsync("继续上次的任务", "conv1", priorHistory);

        Assert.True(result.Completed);
        Assert.Equal("之前的请求", seenHistoryUser); // 历史被纳入发送给 LLM 的 messages
        Assert.True(result.Messages.Count >= 3);      // system + 历史2 + 新 user
        // 回传的 Messages 应供宿主合并历史
        Assert.Contains(result.Messages, m => m.Role == LlmRoles.User && m.Content == "继续上次的任务");
    }

    private sealed class BlockingProvider : ILlmProvider
    {
        private readonly TaskCompletionSource<bool> _gate;
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingProvider(TaskCompletionSource<bool> gate) => _gate = gate;
        public string ProviderName => "blocking";

        public async Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default)
        {
            Started.TrySetResult(true);
            await _gate.Task.WaitAsync(ct);
            return new LlmResponse
            {
                Content = "{\"type\":\"final_answer\",\"text\":\"完成\"}"
            };
        }
    }
}

/// <summary>
/// 429 限流重试策略回归测试。
/// </summary>
public class LlmHttpRetryTests
{
    private class TestProvider : LlmHttpProviderBase
    {
        public TestProvider(LlmProviderConfig config, HttpMessageHandler handler) : base(config, handler) { }
        public override string ProviderName => "test";
        public override Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<HttpResponseMessage> TestSendAsync(Func<HttpRequestMessage> factory, CancellationToken ct) => SendWithRetryAsync(factory, ct);
    }

    [Fact]
    public async Task SendWithRetry_429WithoutRetryAfter_UsesLongerBackoffAndFriendlyMessage()
    {
        var callCount = 0;
        var handler = new TestHttpHandler((req, ct) =>
        {
            callCount++;
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests)
            {
                ReasonPhrase = "Too Many Requests"
            });
        });

        var provider = new TestProvider(new LlmProviderConfig { MaxRetries = 1 }, handler);
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() =>
            provider.TestSendAsync(() => new System.Net.Http.HttpRequestMessage(HttpMethod.Post, "http://test"), default));

        Assert.Equal(2, callCount); // 初始 + 1 次重试
        Assert.Contains("API 限流", ex.Message);
        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Fact]
    public async Task SendWithRetry_429WithRetryAfter_RespectsHeaderValue()
    {
        var callCount = 0;
        var handler = new TestHttpHandler((req, ct) =>
        {
            callCount++;
            var resp = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests)
            {
                ReasonPhrase = "Too Many Requests"
            };
            resp.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
            return Task.FromResult(resp);
        });

        var provider = new TestProvider(new LlmProviderConfig { MaxRetries = 1 }, handler);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() =>
            provider.TestSendAsync(() => new System.Net.Http.HttpRequestMessage(HttpMethod.Post, "http://test"), default));
        sw.Stop();

        Assert.Equal(2, callCount);
        Assert.True(sw.Elapsed >= TimeSpan.FromSeconds(2), $"应等待至少 Retry-After(2s)+缓冲, 实际 {sw.Elapsed}");
        Assert.Contains("Retry-After: 2s", ex.InnerException?.Message ?? "");
        Assert.Equal(3, ex.RetryAfterSeconds); // 2s + 1s 缓冲
    }

    [Fact]
    public async Task SendWithRetry_500_UsesStandardBackoff()
    {
        var callCount = 0;
        var handler = new TestHttpHandler((req, ct) =>
        {
            callCount++;
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));
        });

        var provider = new TestProvider(new LlmProviderConfig { MaxRetries = 1 }, handler);
        var ex = await Assert.ThrowsAsync<LlmProviderException>(() =>
            provider.TestSendAsync(() => new System.Net.Http.HttpRequestMessage(HttpMethod.Post, "http://test"), default));

        Assert.Equal(2, callCount);
        Assert.Contains("重试 1 次后仍失败", ex.Message);
    }

    private class TestHttpHandler : System.Net.Http.DelegatingHandler
    {
        private readonly Func<System.Net.Http.HttpRequestMessage, CancellationToken, Task<System.Net.Http.HttpResponseMessage>> _handler;
        public TestHttpHandler(Func<System.Net.Http.HttpRequestMessage, CancellationToken, Task<System.Net.Http.HttpResponseMessage>> handler) : base(new System.Net.Http.HttpClientHandler())
        {
            _handler = handler;
        }
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }

    // ── BuildArgsDoc JSON 类型推断回归 ──

    [Fact]
    public void TryParse_XmlWithJsonArrayParameter_PreservesArrayType()
    {
        var xml = """
            <tool_call>
            <function=create_variables_batch>
            <parameter=variables>[{"name":"Temp_Set_7","data_type":"Float","unit":"°C"},{"name":"Temp_Current_7","data_type":"Float","unit":"°C"}]</parameter>
            </function>
            </tool_call>
            """;

        var kind = AgentOutput.TryParse(xml, out var toolCall, out _);

        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.NotNull(toolCall);
        Assert.Equal("create_variables_batch", toolCall!.Name);
        Assert.True(toolCall.Arguments.TryGetProperty("variables", out var varsEl));
        Assert.Equal(JsonValueKind.Array, varsEl.ValueKind);
        Assert.Equal(2, varsEl.GetArrayLength());
        Assert.Equal("Temp_Set_7", AgentOutput.GetString(varsEl[0], "name"));
        Assert.Equal("Float", AgentOutput.GetString(varsEl[0], "data_type"));
    }

    [Fact]
    public void TryParse_XmlWithJsonObjectParameter_PreservesObjectType()
    {
        var xml = """
            <tool_call>
            <function=add_controls_batch>
            <parameter=screen_name>TemperatureScreen</parameter>
            <parameter=controls>[{"type":"Label","name":"lblTemp","x":10,"y":20}]</parameter>
            <parameter=auto_grid>{"columns":2,"x0":20}</parameter>
            </function>
            </tool_call>
            """;

        var kind = AgentOutput.TryParse(xml, out var toolCall, out _);

        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.NotNull(toolCall);
        Assert.True(toolCall!.Arguments.TryGetProperty("controls", out var ctrlsEl));
        Assert.Equal(JsonValueKind.Array, ctrlsEl.ValueKind);
        Assert.True(toolCall.Arguments.TryGetProperty("auto_grid", out var gridEl));
        Assert.Equal(JsonValueKind.Object, gridEl.ValueKind);
        Assert.Equal(2, AgentOutput.GetInt(gridEl, "columns"));
    }

    [Fact]
    public void TryParse_XmlWithEscapedJsonParameter_UnescapesAndParses()
    {
        // 模型输出中 JSON 键值被转义为 \u0022，必须能正确解析
        var xml = """
            <tool_call>
            <function=create_variables_batch>
            <parameter=variables>[{\u0022name\u0022:\u0022Temp_Set_7\u0022,\u0022data_type\u0022:\u0022Float\u0022,\u0022unit\u0022:\u0022°C\u0022},{\u0022name\u0022:\u0022Temp_Current_7\u0022,\u0022data_type\u0022:\u0022Float\u0022,\u0022unit\u0022:\u0022°C\u0022}]</parameter>
            </function>
            </tool_call>
            """;

        var kind = AgentOutput.TryParse(xml, out var toolCall, out _);

        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.NotNull(toolCall);
        Assert.Equal("create_variables_batch", toolCall!.Name);
        Assert.True(toolCall.Arguments.TryGetProperty("variables", out var varsEl));
        Assert.Equal(JsonValueKind.Array, varsEl.ValueKind);
        Assert.Equal(2, varsEl.GetArrayLength());
        Assert.Equal("Temp_Set_7", AgentOutput.GetString(varsEl[0], "name"));
        Assert.Equal("Float", AgentOutput.GetString(varsEl[0], "data_type"));
        Assert.Equal("Temp_Current_7", AgentOutput.GetString(varsEl[1], "name"));
    }

    [Fact]
    public void TryParse_XmlWithEscapedObjectParameter_UnescapesAndParses()
    {
        var xml = """
            <tool_call>
            <function=add_controls_batch>
            <parameter=screen_name>TemperatureScreen</parameter>
            <parameter=controls>[{\u0022type\u0022:\u0022Label\u0022,\u0022name\u0022:\u0022lblTemp\u0022,\u0022x\u0022:10,\u0022y\u0022:20}]</parameter>
            <parameter=auto_grid>{\u0022columns\u0022:2,\u0022x0\u0022:20}</parameter>
            </function>
            </tool_call>
            """;

        var kind = AgentOutput.TryParse(xml, out var toolCall, out _);

        Assert.Equal(AgentOutputKind.ToolCall, kind);
        Assert.NotNull(toolCall);
        Assert.True(toolCall!.Arguments.TryGetProperty("controls", out var ctrlsEl));
        Assert.Equal(JsonValueKind.Array, ctrlsEl.ValueKind);
        Assert.True(toolCall.Arguments.TryGetProperty("auto_grid", out var gridEl));
        Assert.Equal(JsonValueKind.Object, gridEl.ValueKind);
        Assert.Equal(2, AgentOutput.GetInt(gridEl, "columns"));
        Assert.Equal("Label", AgentOutput.GetString(ctrlsEl[0], "type"));
    }
}
