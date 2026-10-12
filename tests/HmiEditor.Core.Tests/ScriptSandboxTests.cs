using System.Text;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// R3-T5: 脚本沙箱执行器单元测试。
/// 覆盖赋值、变量引用、布尔 / 字符串表达式、if 语句、多语句、UDT 字段、
/// 错误处理、沙箱隔离、变量存储操作、输出字典与执行日志。
/// </summary>
public class ScriptSandboxTests
{
    private readonly ScriptSandboxExecutor _executor = new();

    // ── 简单赋值 ──

    [Fact]
    public void SimpleAssignment_ReturnsSum()
    {
        var result = _executor.Execute("result = 2 + 3");
        Assert.True(result.Success);
        Assert.Equal(5.0, (double)result.Outputs["result"]);
    }

    [Fact]
    public void Assignment_OperatorPrecedence()
    {
        var result = _executor.Execute("result = 2 + 3 * 4");
        Assert.True(result.Success);
        Assert.Equal(14.0, (double)result.Outputs["result"]);
    }

    [Fact]
    public void Assignment_WithWeightedSum()
    {
        _executor.SetVariable("tag1", 10.0);
        _executor.SetVariable("tag2", 20.0);
        var result = _executor.Execute("result = tag1 + tag2 * 0.5");
        Assert.True(result.Success);
        Assert.Equal(20.0, (double)result.Outputs["result"]);
    }

    // ── 变量引用 ──

    [Fact]
    public void VariableReference_ResolvesStoredValue()
    {
        _executor.SetVariable("tag1", 100.0);
        _executor.SetVariable("tag2", 50.0);
        var result = _executor.Execute("result = tag1 + tag2");
        Assert.True(result.Success);
        Assert.Equal(150.0, (double)result.Outputs["result"]);
    }

    [Fact]
    public void VariableReference_UpdatedValueVisibleInNextStatement()
    {
        var result = _executor.Execute("a = 10\nb = a + 5\nc = b * 2");
        Assert.True(result.Success);
        Assert.Equal(10.0, (double)result.Outputs["a"]);
        Assert.Equal(15.0, (double)result.Outputs["b"]);
        Assert.Equal(30.0, (double)result.Outputs["c"]);
    }

    // ── 布尔表达式 ──

    [Fact]
    public void BooleanExpression_True()
    {
        _executor.SetVariable("tag1", 150.0);
        var result = _executor.Execute("result = tag1 > 100");
        Assert.True(result.Success);
        Assert.True((bool)result.Outputs["result"]);
    }

    [Fact]
    public void BooleanExpression_False()
    {
        _executor.SetVariable("tag1", 50.0);
        var result = _executor.Execute("result = tag1 > 100");
        Assert.True(result.Success);
        Assert.False((bool)result.Outputs["result"]);
    }

    // ── 字符串表达式 ──

    [Fact]
    public void StringExpression_TernaryHigh()
    {
        _executor.SetVariable("tag1", 150.0);
        var result = _executor.Execute("result = tag1 > 100 ? \"high\" : \"low\"");
        Assert.True(result.Success);
        Assert.Equal("high", (string)result.Outputs["result"]);
    }

    [Fact]
    public void StringExpression_TernaryLow()
    {
        _executor.SetVariable("tag1", 50.0);
        var result = _executor.Execute("result = tag1 > 100 ? \"high\" : \"low\"");
        Assert.True(result.Success);
        Assert.Equal("low", (string)result.Outputs["result"]);
    }

    // ── If 语句 ──

    [Fact]
    public void IfStatement_TrueCondition_ExecutesBody()
    {
        _executor.SetVariable("temp", 85.0);
        var result = _executor.Execute("if temp > 80 then alarm = 1 end");
        Assert.True(result.Success);
        Assert.Equal(1.0, (double)result.Outputs["alarm"]);
    }

    [Fact]
    public void IfStatement_FalseCondition_SkipsBody()
    {
        _executor.SetVariable("temp", 70.0);
        var result = _executor.Execute("if temp > 80 then alarm = 1 end");
        Assert.True(result.Success);
        Assert.False(result.Outputs.ContainsKey("alarm"));
    }

    [Fact]
    public void IfStatement_MultilineForm()
    {
        _executor.SetVariable("temp", 85.0);
        var code = "if temp > 80 then\n  alarm = 1\nend";
        var result = _executor.Execute(code);
        Assert.True(result.Success);
        Assert.Equal(1.0, (double)result.Outputs["alarm"]);
    }

    [Fact]
    public void IfStatement_NestedIf()
    {
        _executor.SetVariable("a", 5.0);
        _executor.SetVariable("b", 3.0);
        var code = "if a > 0 then if b > 0 then c = 1 end end";
        var result = _executor.Execute(code);
        Assert.True(result.Success);
        Assert.Equal(1.0, (double)result.Outputs["c"]);
    }

    [Fact]
    public void IfStatement_NestedIfMultiline()
    {
        _executor.SetVariable("a", 5.0);
        _executor.SetVariable("b", 3.0);
        var code = "if a > 0 then\n  if b > 0 then\n    c = 1\n  end\nend";
        var result = _executor.Execute(code);
        Assert.True(result.Success);
        Assert.Equal(1.0, (double)result.Outputs["c"]);
    }

    [Fact]
    public void IfStatement_BooleanVariableCondition()
    {
        _executor.SetVariable("flag", true);
        var result = _executor.Execute("if flag then alarm = 1 end");
        Assert.True(result.Success);
        Assert.Equal(1.0, (double)result.Outputs["alarm"]);
    }

    // ── 多语句 ──

    [Fact]
    public void MultipleStatements_NewlineSeparated()
    {
        var result = _executor.Execute("a = 1\nb = 2\nc = a + b");
        Assert.True(result.Success);
        Assert.Equal(3.0, (double)result.Outputs["c"]);
    }

    [Fact]
    public void MultipleStatements_SemicolonSeparated()
    {
        var result = _executor.Execute("a = 1; b = 2; c = a + b");
        Assert.True(result.Success);
        Assert.Equal(3.0, (double)result.Outputs["c"]);
    }

    [Fact]
    public void MultipleStatements_MixedSeparators()
    {
        var result = _executor.Execute("a = 1; b = 2\nc = a + b");
        Assert.True(result.Success);
        Assert.Equal(3.0, (double)result.Outputs["c"]);
    }

    // ── UDT 字段引用 ──

    [Fact]
    public void UdtFieldReference_ResolvesValue()
    {
        _executor.SetVariable("motor1.speed", 150.0);
        var result = _executor.Execute("result = motor1.speed");
        Assert.True(result.Success);
        Assert.Equal(150.0, (double)result.Outputs["result"]);
    }

    [Fact]
    public void UdtFieldInExpression()
    {
        _executor.SetVariable("motor1.speed", 100.0);
        _executor.SetVariable("motor1.current", 5.0);
        var result = _executor.Execute("result = motor1.speed + motor1.current");
        Assert.True(result.Success);
        Assert.Equal(105.0, (double)result.Outputs["result"]);
    }

    [Fact]
    public void UdtFieldAssignment()
    {
        var result = _executor.Execute("motor1.speed = 200");
        Assert.True(result.Success);
        Assert.Equal(200.0, (double)result.Outputs["motor1.speed"]);
        Assert.Equal(200.0, _executor.GetVariable("motor1.speed"));
    }

    // ── 错误处理 ──

    [Fact]
    public void InvalidExpression_ReturnsError()
    {
        var result = _executor.Execute("result = 2 +");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void MissingClosingParen_ReturnsError()
    {
        var result = _executor.Execute("result = (2 + 3");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void TernaryMissingColon_ReturnsError()
    {
        var result = _executor.Execute("result = 1 > 0 ? \"a\"");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void IfMissingThen_ReturnsError()
    {
        var result = _executor.Execute("if x > 0 alarm = 1 end");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void IfMissingEnd_ReturnsError()
    {
        var result = _executor.Execute("if x > 0 then alarm = 1");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void UndefinedIdentifier_ResolvesToZero()
    {
        var result = _executor.Execute("result = unknownVar + 5");
        Assert.True(result.Success);
        Assert.Equal(5.0, (double)result.Outputs["result"]);
    }

    [Fact]
    public void DivisionByZero_ReturnsZero()
    {
        var result = _executor.Execute("result = 10 / 0");
        Assert.True(result.Success);
        Assert.Equal(0.0, (double)result.Outputs["result"]);
    }

    // ── 沙箱隔离 ──

    [Fact]
    public void Sandbox_NoFileAccess()
    {
        // File.read 被当作未知变量引用（= 0），不会触发任何文件 IO
        var result = _executor.Execute("result = File.read");
        Assert.True(result.Success);
        Assert.Equal(0.0, (double)result.Outputs["result"]);
    }

    [Fact]
    public void Sandbox_NoFunctionCallCapability()
    {
        // 表达式引擎不支持函数调用，sin 被当作未知变量（= 0）
        var result = _executor.Execute("result = sin(1)");
        Assert.True(result.Success);
        Assert.Equal(0.0, (double)result.Outputs["result"]);
    }

    [Fact]
    public void Sandbox_StringWithSemicolonNotSplit()
    {
        var result = _executor.Execute("result = \"a;b\"");
        Assert.True(result.Success);
        Assert.Equal("a;b", (string)result.Outputs["result"]);
    }

    [Fact]
    public void Sandbox_KeywordInsideStringNotTreatedAsKeyword()
    {
        var result = _executor.Execute("if 1 then result = \"end\" end");
        Assert.True(result.Success);
        Assert.Equal("end", (string)result.Outputs["result"]);
    }

    // ── 变量存储操作 ──

    [Fact]
    public void SetVariable_Double_GetVariable_ReturnsValue()
    {
        _executor.SetVariable("x", 42.5);
        Assert.Equal(42.5, _executor.GetVariable("x"));
    }

    [Fact]
    public void SetVariable_Bool_InExpression()
    {
        _executor.SetVariable("flag", true);
        var result = _executor.Execute("result = flag");
        Assert.True(result.Success);
        Assert.True((bool)result.Outputs["result"]);
    }

    [Fact]
    public void SetVariable_String_InExpression()
    {
        _executor.SetVariable("name", "test");
        var result = _executor.Execute("result = name");
        Assert.True(result.Success);
        Assert.Equal("test", (string)result.Outputs["result"]);
    }

    [Fact]
    public void GetVariable_Unknown_ReturnsNull()
    {
        Assert.Null(_executor.GetVariable("doesNotExist"));
    }

    [Fact]
    public void Clear_RemovesAllVariables()
    {
        _executor.SetVariable("a", 1.0);
        _executor.SetVariable("b", 2.0);
        Assert.Equal(1.0, _executor.GetVariable("a"));
        _executor.Clear();
        Assert.Null(_executor.GetVariable("a"));
        Assert.Null(_executor.GetVariable("b"));
    }

    [Fact]
    public void Clear_ResetsStateForNextExecution()
    {
        _executor.SetVariable("a", 99.0);
        _executor.Clear();
        var result = _executor.Execute("result = a + 1");
        Assert.True(result.Success);
        Assert.Equal(1.0, (double)result.Outputs["result"]);
    }

    // ── 输出字典 ──

    [Fact]
    public void Outputs_ContainsModifiedVariables()
    {
        var result = _executor.Execute("result = 5");
        Assert.True(result.Outputs.ContainsKey("result"));
    }

    [Fact]
    public void Outputs_DoesNotContainPresetVariables()
    {
        _executor.SetVariable("tag1", 100.0);
        var result = _executor.Execute("result = tag1 + 50");
        Assert.True(result.Outputs.ContainsKey("result"));
        Assert.False(result.Outputs.ContainsKey("tag1"));
    }

    [Fact]
    public void Outputs_ContainsAllModifiedAcrossStatements()
    {
        var result = _executor.Execute("a = 1\nb = 2\nc = 3");
        Assert.Equal(3, result.Outputs.Count);
        Assert.Contains("a", result.Outputs.Keys);
        Assert.Contains("b", result.Outputs.Keys);
        Assert.Contains("c", result.Outputs.Keys);
    }

    // ── 执行日志 ──

    [Fact]
    public void Log_ContainsTraceMessages()
    {
        var result = _executor.Execute("result = 2 + 3");
        Assert.True(result.Log.Count > 0);
        Assert.Contains(result.Log, l => l.Contains("result"));
    }

    [Fact]
    public void Log_RecordsIfConditionResult()
    {
        _executor.SetVariable("temp", 85.0);
        var result = _executor.Execute("if temp > 80 then alarm = 1 end");
        Assert.Contains(result.Log, l => l.Contains("if") && l.Contains("True"));
    }

    [Fact]
    public void Log_ErrorRecordedOnFailure()
    {
        var result = _executor.Execute("result = 2 +");
        Assert.False(result.Success);
        Assert.Contains(result.Log, l => l.Contains("错误") || l.Contains("error") || l.Contains("Error"));
    }

    // ── HmiScript 对象执行 ──

    [Fact]
    public void Execute_HmiScript_UsesCode()
    {
        var script = new HmiScript { Name = "TestScript", Code = "result = 2 + 3" };
        var result = _executor.Execute(script);
        Assert.True(result.Success);
        Assert.Equal(5.0, (double)result.Outputs["result"]);
        Assert.Contains(result.Log, l => l.Contains("TestScript"));
    }

    [Fact]
    public void Execute_HmiScript_NullCode_Succeeds()
    {
        var script = new HmiScript { Name = "Empty", Code = "" };
        var result = _executor.Execute(script);
        Assert.True(result.Success);
        Assert.Empty(result.Outputs);
    }

    // ── 边界情况 ──

    [Fact]
    public void EmptyCode_SucceedsWithNoOutputs()
    {
        var result = _executor.Execute("");
        Assert.True(result.Success);
        Assert.Empty(result.Outputs);
    }

    [Fact]
    public void WhitespaceOnlyCode_Succeeds()
    {
        var result = _executor.Execute("   \n  \t \n ");
        Assert.True(result.Success);
    }

    [Fact]
    public void BareExpression_DoesNotProduceOutput()
    {
        var result = _executor.Execute("2 + 3");
        Assert.True(result.Success);
        Assert.Empty(result.Outputs);
    }

    [Fact]
    public void ComparisonExpression_NotTreatedAsAssignment()
    {
        // '==' 不应被误判为赋值
        _executor.SetVariable("a", 5.0);
        _executor.SetVariable("b", 5.0);
        var result = _executor.Execute("a == b");
        Assert.True(result.Success);
        Assert.Empty(result.Outputs);
    }

    // ── 超时保护 ──

    [Fact]
    public void Timeout_AbortsLongRunningScript()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 20000; i++)
            sb.Append("a = a + 1\n");
        var code = sb.ToString();

        _executor.TimeoutMs = 1;
        var result = _executor.Execute(code);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("超时", result.Error);
    }

    [Fact]
    public void DefaultTimeout_DoesNotAbortNormalScript()
    {
        var result = _executor.Execute("result = 2 + 3");
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }
}
