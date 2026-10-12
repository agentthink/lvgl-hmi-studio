using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// I5: 脚本调试运行单元测试。
/// 覆盖 Lua→沙箱翻译、else 分支、外部值源回退、项目变量种子化、定时器模板端到端运行。
/// </summary>
public class ScriptDebugRunTests
{
    // ── Lua → 沙箱翻译 ──

    [Fact]
    public void PrepareLuaForSandbox_StripsCommentsAndFunctionWrapper()
    {
        var code = """
            -- Button OnClick callback
            -- hmi_read/vmi_write APIs
            function on_click()
                result = 2 + 3
            end
            """;

        var prepared = ScriptSandboxExecutor.PrepareLuaForSandbox(code);

        Assert.NotNull(prepared);
        Assert.DoesNotContain("function", prepared!);
        Assert.DoesNotContain("--", prepared!);
        Assert.Contains("result = 2 + 3", prepared);
    }

    [Fact]
    public void PrepareLuaForSandbox_TranslatesHmiWrite()
    {
        var code = """
            function on_click()
                hmi_write("MOTOR_RUN", 1)
            end
            """;

        var prepared = ScriptSandboxExecutor.PrepareLuaForSandbox(code);

        Assert.NotNull(prepared);
        Assert.Contains("MOTOR_RUN = 1", prepared!);
        Assert.DoesNotContain("hmi_write", prepared!);
    }

    [Fact]
    public void PrepareLuaForSandbox_TranslatesHmiReadAndNot()
    {
        var code = """
            function on_click()
                local current = hmi_read("VALUE")
                hmi_write("VALUE", not current)
            end
            """;

        var prepared = ScriptSandboxExecutor.PrepareLuaForSandbox(code);

        Assert.NotNull(prepared);
        Assert.Contains("current = VALUE", prepared!);
        Assert.Contains("VALUE = ! current", prepared!);
        Assert.DoesNotContain("hmi_read", prepared!);
        Assert.DoesNotContain("local", prepared!);
    }

    [Fact]
    public void PrepareLuaForSandbox_NoFunction_ReturnsNull()
    {
        var prepared = ScriptSandboxExecutor.PrepareLuaForSandbox("x = 1");
        Assert.Null(prepared);
    }

    // ── else 分支 ──

    [Fact]
    public void Execute_IfElse_TrueBranch_RunsThenBody()
    {
        var executor = new ScriptSandboxExecutor();
        executor.SetVariable("a", 1.0);

        var result = executor.Execute("if a > 0 then x = 10 else x = 20 end");

        Assert.True(result.Success);
        Assert.Equal(10.0, (double)result.Outputs["x"]);
    }

    [Fact]
    public void Execute_IfElse_FalseBranch_RunsElseBody()
    {
        var executor = new ScriptSandboxExecutor();
        executor.SetVariable("a", 0.0);

        var result = executor.Execute("if a > 0 then x = 10 else x = 20 end");

        Assert.True(result.Success);
        Assert.Equal(20.0, (double)result.Outputs["x"]);
    }

    [Fact]
    public void Execute_IfElse_NestedIf_TopLevelElseIsCorrect()
    {
        var executor = new ScriptSandboxExecutor();
        executor.SetVariable("a", 0.0);
        executor.SetVariable("b", 1.0);

        // a=0 → 外层 else → x=3；内层 if/else 不应干扰
        var result = executor.Execute("if a > 0 then if b > 0 then x = 1 else x = 2 end else x = 3 end");

        Assert.True(result.Success);
        Assert.Equal(3.0, (double)result.Outputs["x"]);
    }

    // ── 外部值源回退 ──

    [Fact]
    public void Execute_ExternalValueSource_ResolvesUnknownVariable()
    {
        var executor = new ScriptSandboxExecutor
        {
            ExternalValueSource = name => name == "TEMP_VAR" ? 160 : null
        };

        var result = executor.Execute("flag = TEMP_VAR > 150 ? 1 : 0");

        Assert.True(result.Success);
        Assert.Equal(1.0, (double)result.Outputs["flag"]);
    }

    // ── 项目变量种子化 ──

    [Fact]
    public void SeedProjectVariables_SeedsIdNameAndUdtFields()
    {
        var project = new HmiProject { LvglVersion = "9.2" };
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v1", Name = "speed", SimulatedValue = "42"
        });

        var def = new UdtDefinition { Name = "Motor" };
        def.Fields.Add(new UdtField { Name = "speed", DataType = "Float" });
        def.Fields.Add(new UdtField { Name = "run", DataType = "Bool" });
        project.VariableManager.UdtDefinitions.Add(def);
        var instance = project.VariableManager.CreateUdtInstance(def, "motor1");

        var executor = new ScriptSandboxExecutor();
        executor.SeedProjectVariables(project);

        Assert.Equal(42.0, executor.GetVariable("v1"));
        Assert.Equal(42.0, executor.GetVariable("speed"));
        Assert.Equal(0.0, executor.GetVariable("motor1.speed"));
        Assert.Equal(0.0, executor.GetVariable("motor1.run"));
        Assert.NotNull(instance);
    }

    // ── 端到端：定时器模板 ──

    [Fact]
    public void DebugRun_TimerTemplate_WithSeededVariables_SetsAlarmTrigger()
    {
        var template = HmiEditor.Core.Models.Scripting.ScriptTemplate.Timer;
        var prepared = ScriptSandboxExecutor.PrepareLuaForSandbox(template.Code);
        Assert.NotNull(prepared);

        var executor = new ScriptSandboxExecutor();
        executor.SetVariable("TEMP_VAR", 160.0);

        var result = executor.Execute(prepared!);

        Assert.True(result.Success);
        Assert.Equal(1.0, (double)result.Outputs["ALARM_TRIGGER"]);
    }

    [Fact]
    public void DebugRun_TimerTemplate_BelowThreshold_NoWrite()
    {
        var template = HmiEditor.Core.Models.Scripting.ScriptTemplate.Timer;
        var prepared = ScriptSandboxExecutor.PrepareLuaForSandbox(template.Code);
        Assert.NotNull(prepared);

        var executor = new ScriptSandboxExecutor();
        executor.SetVariable("TEMP_VAR", 100.0);

        var result = executor.Execute(prepared!);

        Assert.True(result.Success);
        Assert.False(result.Outputs.ContainsKey("ALARM_TRIGGER"));
    }
}
