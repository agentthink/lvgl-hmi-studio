using HmiEditor.AI.Generators;
using HmiEditor.AI.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// NlToScriptGenerator 测试 —— 脚本响应解析 + 模型映射。
/// </summary>
public class NlToScriptGeneratorTests
{
    [Fact]
    public void ParseResponse_PlainJson_ReturnsModel()
    {
        var json = """
            {
              "script": {
                "name": "温度报警",
                "description": "温度超过80度时报警",
                "category": "OnValueChanged",
                "code": "function on_value_changed_temperature()\n    local t = hmi_read(\"temperature_1\")\n    if t > 80 then\n        print(\"温度过高\")\n    end\nend"
              }
            }
            """;

        var parsed = NlToScriptGenerator.ParseResponse(json);
        Assert.NotNull(parsed);
        Assert.Equal("温度报警", parsed!.Script.Name);
        Assert.Equal("OnValueChanged", parsed.Script.Category);
        Assert.Contains("function on_value_changed_temperature", parsed.Script.Code);
    }

    [Fact]
    public void ParseResponse_FencedJson_StripsCodeBlockMarkers()
    {
        var json = "```json\n{ \"script\": { \"name\": \"A\", \"category\": \"Timer\", \"code\": \"function on_timer() end\" } }\n```";

        var parsed = NlToScriptGenerator.ParseResponse(json);
        Assert.NotNull(parsed);
        Assert.Equal("A", parsed!.Script.Name);
    }

    [Fact]
    public void ParseResponse_InvalidJson_ReturnsNull()
    {
        Assert.Null(NlToScriptGenerator.ParseResponse("{ not json"));
        Assert.Null(NlToScriptGenerator.ParseResponse(""));
    }

    [Fact]
    public void MapToHmiScript_AllFields_Mapped()
    {
        var generated = new AiGeneratedScript
        {
            Script = new AiScript
            {
                Name = "启停控制",
                Description = "按钮控制电机启停",
                Category = "OnClick",
                Code = "function on_click_start()\n    hmi_write(\"motor\", 1)\nend"
            }
        };

        var script = NlToScriptGenerator.MapToHmiScript(generated);
        Assert.Equal("启停控制", script.Name);
        Assert.Equal("OnClick", script.Category);
        Assert.Equal("按钮控制电机启停", script.Description);
        Assert.Contains("hmi_write", script.Code);
    }

    [Theory]
    [InlineData("onclick", "OnClick")]
    [InlineData("ONCLICK", "OnClick")]
    [InlineData("OnValueChanged", "OnValueChanged")]
    [InlineData("timer", "Timer")]
    [InlineData("alarm", "Alarm")]
    [InlineData("weird", "OnClick")]
    public void MapToHmiScript_CategoryNormalization(string input, string expected)
    {
        var generated = new AiGeneratedScript
        {
            Script = new AiScript { Name = "S", Category = input, Code = "function x() end" }
        };

        var script = NlToScriptGenerator.MapToHmiScript(generated);
        Assert.Equal(expected, script.Category);
    }

    [Fact]
    public void MapToHmiScript_NullGenerated_ReturnsDefaultScript()
    {
        var script = NlToScriptGenerator.MapToHmiScript(null);
        Assert.Equal("AI 生成脚本", script.Name);
        Assert.Equal("", script.Code);
    }
}
