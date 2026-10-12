using HmiEditor.AI.Generators;
using HmiEditor.AI.Inference;
using HmiEditor.AI.Validation;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// 端到端管线测试 —— 用 FakeLlmProvider 模拟 LLM 响应,
/// 验证完整链路: Prompt 构建 → LLM 调用 → 解析 → 模型映射 → 输出校验。
/// </summary>
public class AiPipelineEndToEndTests
{
    private static readonly PromptTemplateEngine Prompts = new();

    // ── 屏幕生成端到端 ──

    [Fact]
    public async Task ScreenPipeline_FullFlow_GeneratesValidScreen()
    {
        var provider = new FakeLlmProvider(_ => """
            {
              "screen": {
                "name": "电机监控",
                "width": 800, "height": 480, "bgColor": "#0f172a",
                "controls": [
                  { "type": "Button", "name": "启动", "x": 20, "y": 20, "width": 90, "height": 40, "text": "启动" },
                  { "type": "Gauge", "name": "转速表", "x": 20, "y": 90, "width": 200, "height": 200, "variableBindingId": "motor_speed" }
                ]
              }
            }
            """);

        var generator = new NlToScreenGenerator(provider, Prompts);
        var project = new HmiProject();
        project.VariableManager.Variables.Add(new Variable { Name = "motor_speed", DataType = "Int16" });

        var result = await generator.GenerateAsync("设计一个电机监控屏幕", project);

        Assert.Equal("电机监控", result.Screen.Name);
        Assert.Equal(2, result.Screen.Controls.Count);
        Assert.Equal(HmiControlType.Button, result.Screen.Controls[0].Type);
        Assert.Equal(100, result.InputTokens);

        // Prompt 应包含系统提示和用户输入
        Assert.NotNull(provider.LastSystemPrompt);
        Assert.Contains("电机监控", provider.LastUserPrompt);
        Assert.Contains("motor_speed", provider.LastUserPrompt); // 变量上下文注入

        // 输出校验通过
        var validation = AiOutputValidator.ValidateScreen(result.Screen, project);
        Assert.False(validation.HasErrors);
    }

    [Fact]
    public async Task ScreenPipeline_BadLlmOutput_FailsValidation()
    {
        // 非法背景色 → 校验失败
        var provider = new FakeLlmProvider(_ => """{ "screen": { "name": "坏屏", "bgColor": "not-a-color", "controls": [] } }""");
        var generator = new NlToScreenGenerator(provider, Prompts);

        var result = await generator.GenerateAsync("测试", new HmiProject());
        var validation = AiOutputValidator.ValidateScreen(result.Screen, new HmiProject());
        Assert.True(validation.HasErrors);
    }

    [Fact]
    public async Task ScreenPipeline_InvalidJson_FallsBackToDefaultScreen()
    {
        // 无效 JSON → 生成器回退默认屏幕(不抛异常, 由校验器兜底)
        var provider = new FakeLlmProvider(_ => "这不是 JSON");
        var generator = new NlToScreenGenerator(provider, Prompts);

        var result = await generator.GenerateAsync("测试", new HmiProject());
        Assert.Equal("AI 生成屏幕", result.Screen.Name);
        Assert.Empty(result.Screen.Controls);
    }

    // ── 模板生成端到端 ──

    [Fact]
    public async Task TemplatePipeline_FullFlow_GeneratesValidTemplate()
    {
        var provider = new FakeLlmProvider(_ => """
            {
              "template": {
                "name": "电机控制面板",
                "description": "启停控制",
                "category": "电机控制",
                "controls": [
                  { "type": "Button", "name": "启动", "x": 0, "y": 0, "width": 80, "height": 40, "text": "{{Parameters.btnText}}" }
                ],
                "parameters": [
                  { "name": "btnText", "displayName": "按钮文本", "type": "String", "defaultValue": "启动" }
                ]
              }
            }
            """);

        var generator = new NlToTemplateGenerator(provider, Prompts);
        var result = await generator.GenerateAsync("生成电机控制模板", new HmiProject());

        Assert.Equal("电机控制面板", result.Template.Name);
        Assert.Single(result.Template.Parameters);

        // 校验: 占位符引用已声明参数 → 通过
        var validation = AiOutputValidator.ValidateTemplate(result.Template);
        Assert.False(validation.HasErrors);
    }

    [Fact]
    public async Task TemplatePipeline_ParameterPlaceholder_ResolvedByValidation()
    {
        var provider = new FakeLlmProvider(_ => """
            {
              "template": {
                "name": "T", "controls": [
                  { "type": "Button", "name": "B", "x": 0, "y": 0, "width": 50, "height": 30, "text": "{{Parameters.undeclared}}" }
                ], "parameters": []
              }
            }
            """);

        var generator = new NlToTemplateGenerator(provider, Prompts);
        var result = await generator.GenerateAsync("测试", new HmiProject());
        var validation = AiOutputValidator.ValidateTemplate(result.Template);

        // 未声明参数引用 → 警告
        Assert.Contains(validation.Issues, i => i.Severity == AiValidationSeverity.Warning);
    }

    // ── 脚本生成端到端 ──

    [Fact]
    public async Task ScriptPipeline_FullFlow_GeneratesValidScript()
    {
        var provider = new FakeLlmProvider(_ => """
            {
              "script": {
                "name": "温度报警",
                "description": "超温报警",
                "category": "OnValueChanged",
                "code": "function on_value_changed_temperature()\n    local t = hmi_read(\"temperature_1\")\n    if t > 80 then\n        hmi_set_color(\"led1\", \"#ef4444\")\n    end\nend"
              }
            }
            """);

        var generator = new NlToScriptGenerator(provider, Prompts);
        var project = new HmiProject();
        project.VariableManager.Variables.Add(new Variable { Name = "temperature_1", DataType = "Float" });

        var result = await generator.GenerateAsync("温度超过80时报警", project);

        Assert.Equal("温度报警", result.Script.Name);
        Assert.Equal("OnValueChanged", result.Script.Category);
        Assert.Contains("temperature_1", provider.LastUserPrompt); // 变量列表注入

        var validation = AiOutputValidator.ValidateScript(result.Script);
        Assert.False(validation.HasErrors);
    }

    [Fact]
    public async Task ScriptPipeline_ForbiddenApi_BlockedByValidation()
    {
        var provider = new FakeLlmProvider(_ => """
            {
              "script": {
                "name": "恶意脚本", "category": "OnClick",
                "code": "function on_click_hack()\n    os.execute('rm -rf /')\nend"
              }
            }
            """);

        var generator = new NlToScriptGenerator(provider, Prompts);
        var result = await generator.GenerateAsync("测试", new HmiProject());
        var validation = AiOutputValidator.ValidateScript(result.Script);

        Assert.True(validation.HasErrors);
        Assert.Contains(validation.Issues, i => i.Message.Contains("禁止"));
    }

    // ── 图标生成端到端 ──

    [Fact]
    public async Task IconPipeline_FullFlow_GeneratesNormalizedSvg()
    {
        var provider = new FakeLlmProvider(_ => """
            ```xml
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">
              <path d="M24 8 L40 8 L40 24 L24 24 Z"/>
              <circle cx="32" cy="40" r="12"/>
            </svg>
            ```
            """);

        var generator = new IconGenerator(provider, Prompts);
        var result = await generator.GenerateAsync("水泵图标");

        Assert.Contains("viewBox=\"0 0 48 48\"", result.SvgText);
        Assert.Contains("width=\"48\"", result.SvgText);
        Assert.Equal(2, result.ShapeCount);
        Assert.DoesNotContain("width=\"64\"", result.SvgText);
    }

    [Fact]
    public async Task IconPipeline_InvalidSvg_Throws()
    {
        var provider = new FakeLlmProvider(_ => "这不是SVG");
        var generator = new IconGenerator(provider, Prompts);

        // 阶段 87（登记 #61a）：SVG 生成失败改成**带码**的 SvgParseException。
        // 这里连码一起断 —— 码是收益的唯一来源（ai.gen.failed 那 8 处靠它走字典），
        // 只断类型的话，"抛个带码异常但码是空的"照样绿。
        var ex = await Assert.ThrowsAsync<SvgParseException>(
            () => generator.GenerateAsync("测试"));
        Assert.Equal(SvgParseIssueCodes.XML_INVALID, ex.Error.Code);
    }

    // ── 编排器端到端 (意图分流) ──

    [Fact]
    public async Task OrchestratorPipeline_ScreenIntent_RoutesCorrectly()
    {
        var provider = new FakeLlmProvider(_ => """
            {
              "screen": { "name": "测试屏", "controls": [
                { "type": "Label", "name": "L", "x": 0, "y": 0, "width": 10, "height": 10, "text": "hi" }
              ] }
            }
            """);
        var orchestrator = new AiOrchestrator(
            new NlToScreenGenerator(provider, Prompts),
            new NlToTemplateGenerator(provider, Prompts),
            new NlToScriptGenerator(provider, Prompts));

        var result = await orchestrator.ProcessAsync("设计一个测试屏", new HmiProject());

        Assert.Equal("Screen", result.Intent);
        Assert.NotNull(result.Screen);
        Assert.Null(result.Template);
        Assert.Null(result.Script);
    }

    [Fact]
    public async Task OrchestratorPipeline_TemplateIntent_RoutesCorrectly()
    {
        var provider = new FakeLlmProvider(_ => """
            {
              "template": { "name": "T", "controls": [], "parameters": [] }
            }
            """);
        var orchestrator = new AiOrchestrator(
            new NlToScreenGenerator(provider, Prompts),
            new NlToTemplateGenerator(provider, Prompts),
            new NlToScriptGenerator(provider, Prompts));

        var result = await orchestrator.ProcessAsync("生成一个阀门模板", new HmiProject());

        Assert.Equal("Template", result.Intent);
        Assert.NotNull(result.Template);
        Assert.Null(result.Screen);
    }

    [Fact]
    public async Task OrchestratorPipeline_ScriptIntent_RoutesCorrectly()
    {
        var provider = new FakeLlmProvider(_ => """
            {
              "script": { "name": "S", "category": "Timer", "code": "function on_timer() end" }
            }
            """);
        var orchestrator = new AiOrchestrator(
            new NlToScreenGenerator(provider, Prompts),
            new NlToTemplateGenerator(provider, Prompts),
            new NlToScriptGenerator(provider, Prompts));

        var result = await orchestrator.ProcessAsync("写个定时器脚本", new HmiProject());

        Assert.Equal("Script", result.Intent);
        Assert.NotNull(result.Script);
        Assert.Equal("Timer", result.Script!.Category);
    }

    [Fact]
    public async Task OrchestratorPipeline_UnknownIntent_ReturnsInfo()
    {
        var provider = new FakeLlmProvider(_ => "{}");
        var orchestrator = new AiOrchestrator(
            new NlToScreenGenerator(provider, Prompts),
            new NlToTemplateGenerator(provider, Prompts),
            new NlToScriptGenerator(provider, Prompts));

        var result = await orchestrator.ProcessAsync("今天天气怎么样", new HmiProject());

        Assert.Equal("Unknown", result.Intent);
        Assert.NotNull(result.Validation);
        Assert.Contains(result.Validation.Issues, i => i.Severity == AiValidationSeverity.Info);
    }
}
