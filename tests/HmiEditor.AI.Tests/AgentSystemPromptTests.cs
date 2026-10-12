using HmiEditor.AI.Agent;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// 系统提示词构建测试：紧凑工具清单格式 + 项目概览截断 + 批量工具指引 + XML/JSON 协议切换。
/// </summary>
public class AgentSystemPromptTests
{
    private static AgentTool Tool(string name, string desc, string? schema) =>
        new(name, desc, schema, execute: (_, _, _) => Task.FromResult(AgentToolResult.Success("ok")));

    [Fact]
    public void BuildToolList_CompactFormat_MarksRequiredAndEnums()
    {
        var tools = new[]
        {
            Tool("create_screen", "新建一个屏幕。",
                """{ "type":"object", "properties": { "name":{"type":"string"}, "width":{"type":"number"} }, "required":["name"] }"""),
            Tool("open_panel", "打开面板。",
                """{ "type":"object", "properties": { "panel_name":{"type":"string","enum":["Assets","Canvas"]} }, "required":["panel_name"] }""")
        };

        var list = AgentTool.BuildToolList(tools);

        var lines = list.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("- create_screen(name!, width): 新建一个屏幕。", lines[0].TrimEnd('\r'));
        Assert.StartsWith("- open_panel(panel_name![Assets|Canvas]): 打开面板。", lines[1].TrimEnd('\r'));
    }

    [Fact]
    public void BuildToolList_SignificantlySmallerThanJson()
    {
        // token 精简有效性：紧凑格式必须明显小于完整 JSON Schema 清单
        var tools = new[]
        {
            Tool("add_control", "向指定屏幕添加控件。",
                """{ "type":"object", "properties": { "screen_name":{"type":"string"}, "type":{"type":"string","enum":["Button","Label","Slider","Switch"]}, "name":{"type":"string"}, "x":{"type":"number"}, "y":{"type":"number"}, "width":{"type":"number"}, "height":{"type":"number"}, "text":{"type":"string"}, "bgColor":{"type":"string"}, "textColor":{"type":"string"}, "binding":{"type":"string"}, "radius":{"type":"number"}, "opacity":{"type":"number"} }, "required":["screen_name","type","name"] }"""),
            Tool("create_variable", "新建变量。",
                """{ "type":"object", "properties": { "name":{"type":"string"}, "data_type":{"type":"string"}, "address":{"type":"string"}, "connection_name":{"type":"string"}, "unit":{"type":"string"}, "poll_rate_ms":{"type":"number"} }, "required":["name"] }""")
        };

        var compact = AgentTool.BuildToolList(tools);
        var json = AgentTool.BuildToolListJson(tools);

        Assert.True(compact.Length < json.Length / 2,
            $"紧凑格式({compact.Length} 字符)应小于 JSON 格式({json.Length} 字符)的一半");
    }

    [Fact]
    public void Build_IncludesBatchToolGuidance()
    {
        var prompt = AgentSystemPrompt.Build(
            new[] { Tool("create_variables_batch", "批量新建变量。", null) }, new HmiProject());

        Assert.Contains("create_variables_batch", prompt);
        Assert.Contains("add_controls_batch", prompt);   // 行为准则中的批量指引
        Assert.Contains("auto_grid", prompt);
        Assert.DoesNotContain("控件类型枚举", prompt);    // 概览中冗余枚举已移除
    }

    [Fact]
    public void Build_IncludesBatchExamplesAndNonEmptyArgsRule()
    {
        var prompt = AgentSystemPrompt.Build(
            new[] { Tool("create_variables_batch", "批量新建变量。", null) }, new HmiProject());

        // 必须包含批量工具调用示例
        Assert.Contains("variables", prompt);
        Assert.Contains("controls", prompt);
        // 必须强调参数不能为空对象
        Assert.Contains("严禁输出空对象", prompt);
        Assert.Contains("必填参数必须提供有效值", prompt);
    }

    [Fact]
    public void Build_XmlProtocol_IncludesXmlExamplesAndNoJsonSchema()
    {
        var prompt = AgentSystemPrompt.Build(
            new[] { Tool("create_variables_batch", "批量新建变量。", null) }, new HmiProject(), useXmlProtocol: true);

        // XML 协议模式下应包含 XML 示例
        Assert.Contains("<tool_call>", prompt);
        Assert.Contains("<function=create_variables_batch>", prompt);
        Assert.Contains("<parameter=variables>", prompt);
        // 不应包含 JSON 协议特有的 strict 规则
        Assert.DoesNotContain("\"type\":\"tool_call\"", prompt);
        // 但应包含数组参数值说明
        Assert.Contains("数组参数", prompt);
    }

    [Fact]
    public void Build_JsonProtocol_Default()
    {
        var prompt = AgentSystemPrompt.Build(
            new[] { Tool("create_screen", "新建屏幕。", null) }, new HmiProject());

        // 默认 JSON 协议
        Assert.Contains("\"type\":\"tool_call\"", prompt);
        Assert.Contains("arguments", prompt);
    }

    [Fact]
    public void BuildProjectSummary_TruncatesScreenList()
    {
        var project = new HmiProject();
        for (var i = 1; i <= 20; i++)
            project.Screens.Add(new HmiScreen { Name = $"Screen_{i}" });

        var summary = AgentSystemPrompt.BuildProjectSummary(project);

        Assert.Contains("等 20 个", summary);             // 超过 15 个屏幕时截断
        Assert.DoesNotContain("Screen_16、Screen_17", summary);
    }
}
