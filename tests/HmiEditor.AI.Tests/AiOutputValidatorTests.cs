using HmiEditor.AI.Validation;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// AiOutputValidator 测试 —— 覆盖所有验证规则。
/// </summary>
public class AiOutputValidatorTests
{
    private static HmiScreen ValidScreen() => new()
    {
        Name = "T",
        Width = 800,
        Height = 480,
        Controls =
        {
            new HmiControl { Type = HmiControlType.Button, Name = "B1", X = 10, Y = 10, Width = 100, Height = 40 }
        }
    };

    [Fact]
    public void ValidateScreen_ValidScreen_NoErrors()
    {
        var result = AiOutputValidator.ValidateScreen(ValidScreen(), new HmiProject());
        Assert.False(result.HasErrors);
        Assert.DoesNotContain(result.Issues, i => i.Severity == AiValidationSeverity.Error);
    }

    [Fact]
    public void ValidateScreen_NullScreen_HasError()
    {
        var result = AiOutputValidator.ValidateScreen(null!, new HmiProject());
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateScreen_IllegalType_HasError()
    {
        var screen = ValidScreen();
        screen.Controls.Add(new HmiControl { Type = (HmiControlType)999, Name = "Bad", X = 0, Y = 0, Width = 10, Height = 10 });
        var result = AiOutputValidator.ValidateScreen(screen, new HmiProject());
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateScreen_NegativeCoordinates_HasError()
    {
        var screen = ValidScreen();
        screen.Controls.Add(new HmiControl { Type = HmiControlType.Label, Name = "N", X = -5, Y = 0, Width = 10, Height = 10 });
        var result = AiOutputValidator.ValidateScreen(screen, new HmiProject());
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateScreen_InvalidColor_HasError()
    {
        var screen = ValidScreen();
        screen.Controls[0].BgColor = "blue";  // 非 HEX
        var result = AiOutputValidator.ValidateScreen(screen, new HmiProject());
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateScreen_ValidColor_NoError()
    {
        var screen = ValidScreen();
        screen.Controls[0].BgColor = "#2563eb";
        var result = AiOutputValidator.ValidateScreen(screen, new HmiProject());
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ValidateScreen_OutOfBounds_Warning()
    {
        var screen = ValidScreen();
        screen.Controls[0].X = 900;  // 超出 800 宽度
        var result = AiOutputValidator.ValidateScreen(screen, new HmiProject());
        Assert.Contains(result.Issues, i => i.Severity == AiValidationSeverity.Warning && i.Message.Contains("超出屏幕边界"));
    }

    [Fact]
    public void ValidateScreen_UnknownVariable_Warning()
    {
        var screen = ValidScreen();
        screen.Controls[0].VariableBindingId = "ghost_var";
        var project = new HmiProject();
        var result = AiOutputValidator.ValidateScreen(screen, project);
        Assert.Contains(result.Issues, i => i.Severity == AiValidationSeverity.Warning && i.Message.Contains("不存在"));
    }

    [Fact]
    public void ValidateScreen_KnownVariable_NoWarning()
    {
        var screen = ValidScreen();
        var variable = new Variable { Name = "motor_speed", DataType = "Int16" };
        screen.Controls[0].VariableBindingId = variable.Id;
        var project = new HmiProject();
        project.VariableManager.Variables.Add(variable);

        var result = AiOutputValidator.ValidateScreen(screen, project);
        Assert.DoesNotContain(result.Issues, i => i.Message.Contains("不存在"));
    }

    [Fact]
    public void ValidateScreen_HeavyOverlap_Warning()
    {
        var screen = ValidScreen();
        screen.Controls.Add(new HmiControl { Type = HmiControlType.Button, Name = "B2", X = 30, Y = 10, Width = 100, Height = 40 });
        // B2 与 B1 (10,10,100,40) 重叠区域 80x40 = 3200, 较小控件面积 4000 → 80%, 超过 50%
        var result = AiOutputValidator.ValidateScreen(screen, new HmiProject());
        Assert.Contains(result.Issues, i => i.Severity == AiValidationSeverity.Warning && i.Message.Contains("严重重叠"));
    }

    [Fact]
    public void ValidateScreen_EmptyControls_Warning()
    {
        var result = AiOutputValidator.ValidateScreen(new HmiScreen { Width = 800, Height = 480 }, new HmiProject());
        Assert.Contains(result.Issues, i => i.Message.Contains("不包含任何控件"));
    }

    [Theory]
    [InlineData("#fff", true)]
    [InlineData("#ffffff", true)]
    [InlineData("#ffffffff", true)]
    [InlineData("blue", false)]
    [InlineData("#ggg", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidColor_VariousFormats(string? color, bool expected)
    {
        Assert.Equal(expected, AiOutputValidator.IsValidColor(color));
    }
}
