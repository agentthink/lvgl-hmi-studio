using HmiEditor.AI.Validation;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// AiOutputValidator 模板校验测试。
/// </summary>
public class AiOutputValidatorTemplateTests
{
    private static ControlTemplate ValidTemplate() => new()
    {
        Name = "电机面板",
        Controls =
        {
            new HmiControl { Type = HmiControlType.Button, Name = "启动", X = 0, Y = 0, Width = 80, Height = 40, Text = "{{Parameters.btnText}}" }
        },
        Parameters =
        {
            new TemplateParameter { Name = "btnText", DisplayName = "按钮文本", Type = ParameterType.String }
        }
    };

    [Fact]
    public void ValidateTemplate_ValidTemplate_NoErrors()
    {
        var result = AiOutputValidator.ValidateTemplate(ValidTemplate());
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ValidateTemplate_NullTemplate_HasError()
    {
        var result = AiOutputValidator.ValidateTemplate(null!);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateTemplate_EmptyName_HasError()
    {
        var template = ValidTemplate();
        template.Name = "  ";
        var result = AiOutputValidator.ValidateTemplate(template);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateTemplate_NoControls_HasError()
    {
        var template = new ControlTemplate { Name = "Empty" };
        var result = AiOutputValidator.ValidateTemplate(template);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateTemplate_IllegalType_HasError()
    {
        var template = ValidTemplate();
        template.Controls.Add(new HmiControl { Type = (HmiControlType)999, Name = "Bad", X = 0, Y = 0, Width = 10, Height = 10 });
        var result = AiOutputValidator.ValidateTemplate(template);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateTemplate_InvalidColor_HasError()
    {
        var template = ValidTemplate();
        template.Controls[0].BgColor = "red";
        var result = AiOutputValidator.ValidateTemplate(template);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateTemplate_DuplicateParameter_HasError()
    {
        var template = ValidTemplate();
        template.Parameters.Add(new TemplateParameter { Name = "btnText", DisplayName = "重复", Type = ParameterType.String });
        var result = AiOutputValidator.ValidateTemplate(template);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateTemplate_UndefinedParameterReference_Warning()
    {
        var template = ValidTemplate();
        template.Controls[0].Text = "{{Parameters.notDeclared}}";
        var result = AiOutputValidator.ValidateTemplate(template);
        Assert.Contains(result.Issues, i => i.Severity == AiValidationSeverity.Warning && i.Message.Contains("未声明的参数"));
    }

    [Fact]
    public void ValidateTemplate_DeclaredParameterReference_NoWarning()
    {
        var result = AiOutputValidator.ValidateTemplate(ValidTemplate());
        Assert.DoesNotContain(result.Issues, i => i.Message.Contains("未声明的参数"));
    }

    [Fact]
    public void ValidateTemplate_TooManyControls_Warning()
    {
        var template = ValidTemplate();
        for (var i = 0; i < 16; i++)
        {
            template.Controls.Add(new HmiControl { Type = HmiControlType.Label, Name = $"L{i}", X = i, Y = 0, Width = 10, Height = 10 });
        }
        var result = AiOutputValidator.ValidateTemplate(template);
        Assert.Contains(result.Issues, i => i.Severity == AiValidationSeverity.Warning && i.Message.Contains("控件过多"));
    }
}
