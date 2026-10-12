using HmiEditor.AI.Validation;
using HmiEditor.Core.Models.Scripting;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// AiOutputValidator 脚本校验测试。
/// </summary>
public class AiOutputValidatorScriptTests
{
    private static HmiScript ValidScript() => new()
    {
        Name = "温度报警",
        Category = "OnValueChanged",
        Code = "function on_value_changed_temperature()\n    local t = hmi_read(\"temperature_1\")\n    if t > 80 then\n        print(\"温度过高\")\n    end\nend"
    };

    [Fact]
    public void ValidateScript_ValidScript_NoErrors()
    {
        var result = AiOutputValidator.ValidateScript(ValidScript());
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ValidateScript_NullScript_HasError()
    {
        var result = AiOutputValidator.ValidateScript(null!);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateScript_EmptyName_HasError()
    {
        var script = ValidScript();
        script.Name = " ";
        var result = AiOutputValidator.ValidateScript(script);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateScript_EmptyCode_HasError()
    {
        var script = ValidScript();
        script.Code = "";
        var result = AiOutputValidator.ValidateScript(script);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ValidateScript_NoFunction_HasError()
    {
        var script = ValidScript();
        script.Code = "local x = 1";
        var result = AiOutputValidator.ValidateScript(script);
        Assert.True(result.HasErrors);
    }

    [Theory]
    [InlineData("os.execute('rm -rf /')")]
    [InlineData("io.open('/etc/passwd')")]
    [InlineData("os.remove('file')")]
    [InlineData("require('socket')")]
    public void ValidateScript_ForbiddenCalls_HasError(string code)
    {
        var script = ValidScript();
        script.Code = "function f()\n" + code + "\nend";
        var result = AiOutputValidator.ValidateScript(script);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Issues, i => i.Message.Contains("禁止"));
    }

    [Fact]
    public void ValidateScript_UnknownCategory_Warning()
    {
        var script = ValidScript();
        script.Category = "WeirdCategory";
        var result = AiOutputValidator.ValidateScript(script);
        Assert.Contains(result.Issues, i => i.Severity == AiValidationSeverity.Warning && i.Message.Contains("分类"));
    }

    [Fact]
    public void ValidateScript_OverlongCode_Warning()
    {
        var script = ValidScript();
        script.Code = "function f() end -- " + new string('x', 9000);
        var result = AiOutputValidator.ValidateScript(script);
        Assert.Contains(result.Issues, i => i.Severity == AiValidationSeverity.Warning && i.Message.Contains("过长"));
    }
}
