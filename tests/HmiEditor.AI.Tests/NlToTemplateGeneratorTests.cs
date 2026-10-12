using HmiEditor.AI.Generators;
using HmiEditor.AI.Models;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// NlToTemplateGenerator 测试 —— 模板响应解析 + 模型映射。
/// </summary>
public class NlToTemplateGeneratorTests
{
    [Fact]
    public void ParseResponse_PlainJson_ReturnsModel()
    {
        var json = """
            {
              "template": {
                "name": "电机控制面板",
                "description": "三相电机启停控制",
                "category": "电机控制",
                "controls": [
                  { "type": "Button", "name": "启动", "x": 0, "y": 0, "width": 80, "height": 40, "text": "启动" }
                ],
                "parameters": [
                  { "name": "motorName", "displayName": "电机名称", "type": "String", "defaultValue": "M1" }
                ]
              }
            }
            """;

        var parsed = NlToTemplateGenerator.ParseResponse(json);
        Assert.NotNull(parsed);
        Assert.Equal("电机控制面板", parsed!.Template.Name);
        Assert.Equal("电机控制", parsed.Template.Category);
        Assert.Single(parsed.Template.Controls);
        Assert.Single(parsed.Template.Parameters);
        Assert.Equal("motorName", parsed.Template.Parameters[0].Name);
    }

    [Fact]
    public void ParseResponse_FencedJson_StripsCodeBlockMarkers()
    {
        var json = "```json\n{ \"template\": { \"name\": \"A\", \"controls\": [], \"parameters\": [] } }\n```";

        var parsed = NlToTemplateGenerator.ParseResponse(json);
        Assert.NotNull(parsed);
        Assert.Equal("A", parsed!.Template.Name);
    }

    [Fact]
    public void ParseResponse_InvalidJson_ReturnsNull()
    {
        Assert.Null(NlToTemplateGenerator.ParseResponse("{ not json"));
        Assert.Null(NlToTemplateGenerator.ParseResponse(""));
    }

    [Fact]
    public void MapToControlTemplate_ParameterTypes_MappedCorrectly()
    {
        var generated = new AiGeneratedTemplate
        {
            Template = new AiTemplate
            {
                Name = "T",
                Controls = new List<AiTemplateControl>
                {
                    new() { Type = "Button", Name = "B", X = 0, Y = 0, Width = 50, Height = 30, Text = "{{Parameters.btnText}}" },
                    new() { Type = "Led", Name = "L", X = 60, Y = 0, Width = 20, Height = 20, Binding = "{{Parameters.status}}" }
                },
                Parameters = new List<AiTemplateParameter>
                {
                    new() { Name = "btnText", DisplayName = "按钮文本", Type = "String", DefaultValue = "启动" },
                    new() { Name = "status", DisplayName = "状态变量", Type = "VariableId", DefaultValue = "" }
                }
            }
        };

        var template = NlToTemplateGenerator.MapToControlTemplate(generated, new HmiProject());
        Assert.Equal(2, template.Controls.Count);
        Assert.Equal(HmiControlType.Button, template.Controls[0].Type);
        Assert.Equal("{{Parameters.btnText}}", template.Controls[0].Text);
        Assert.Equal(HmiControlType.Led, template.Controls[1].Type);
        Assert.Equal("{{Parameters.status}}", template.Controls[1].VariableBindingId);

        Assert.Equal(2, template.Parameters.Count);
        Assert.Equal(ParameterType.String, template.Parameters[0].Type);
        Assert.Equal(ParameterType.VariableId, template.Parameters[1].Type);
    }

    [Fact]
    public void MapToControlTemplate_VariableName_ResolvedToId()
    {
        var project = new HmiProject();
        var variable = new Variable { Name = "motor_speed", DataType = "Int16" };
        project.VariableManager.Variables.Add(variable);

        var generated = new AiGeneratedTemplate
        {
            Template = new AiTemplate
            {
                Name = "T",
                Controls = new List<AiTemplateControl>
                {
                    new() { Type = "Gauge", Name = "G", X = 0, Y = 0, Width = 100, Height = 100, Binding = "motor_speed" }
                },
                Parameters = new List<AiTemplateParameter>()
            }
        };

        var template = NlToTemplateGenerator.MapToControlTemplate(generated, project);
        Assert.Equal(variable.Id, template.Controls[0].VariableBindingId);
    }

    [Fact]
    public void MapToControlTemplate_UnknownType_FallsBackToLabel()
    {
        var generated = new AiGeneratedTemplate
        {
            Template = new AiTemplate
            {
                Name = "T",
                Controls = new List<AiTemplateControl>
                {
                    new() { Type = "Hologram", Name = "H", X = 0, Y = 0, Width = 10, Height = 10 }
                },
                Parameters = new List<AiTemplateParameter>()
            }
        };

        var template = NlToTemplateGenerator.MapToControlTemplate(generated, new HmiProject());
        Assert.Equal(HmiControlType.Label, template.Controls[0].Type);
    }

    [Theory]
    [InlineData("String", ParameterType.String)]
    [InlineData("string", ParameterType.String)]
    [InlineData("Number", ParameterType.Number)]
    [InlineData("Boolean", ParameterType.Boolean)]
    [InlineData("Color", ParameterType.Color)]
    [InlineData("VariableId", ParameterType.VariableId)]
    [InlineData("Unknown", ParameterType.String)]
    public void ParseParameterType_AllVariants(string input, ParameterType expected)
    {
        Assert.Equal(expected, NlToTemplateGenerator.ParseParameterType(input));
    }

    [Fact]
    public void MapToControlTemplate_NullGenerated_ReturnsDefaultTemplate()
    {
        var template = NlToTemplateGenerator.MapToControlTemplate(null, new HmiProject());
        Assert.Equal("AI 生成模板", template.Name);
        Assert.Empty(template.Controls);
    }
}
