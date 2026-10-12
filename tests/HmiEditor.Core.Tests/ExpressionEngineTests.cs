using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// R3-T2: 表达式引擎单元测试。
/// 覆盖算术、比较、逻辑、三元运算、变量引用、UDT 字段、字符串操作。
/// </summary>
public class ExpressionEngineTests
{
    // ── 算术运算 ──

    [Fact]
    public void Evaluate_SimpleAddition_ReturnsSum()
    {
        var result = ExpressionEngine.Evaluate("2 + 3", _ => null);
        Assert.Equal(5, result.AsNumber());
    }

    [Fact]
    public void Evaluate_Subtraction_ReturnsDifference()
    {
        var result = ExpressionEngine.Evaluate("10 - 3", _ => null);
        Assert.Equal(7, result.AsNumber());
    }

    [Fact]
    public void Evaluate_Multiplication_ReturnsProduct()
    {
        var result = ExpressionEngine.Evaluate("4 * 5", _ => null);
        Assert.Equal(20, result.AsNumber());
    }

    [Fact]
    public void Evaluate_Division_ReturnsQuotient()
    {
        var result = ExpressionEngine.Evaluate("20 / 4", _ => null);
        Assert.Equal(5, result.AsNumber());
    }

    [Fact]
    public void Evaluate_DivisionByZero_ReturnsZero()
    {
        var result = ExpressionEngine.Evaluate("10 / 0", _ => null);
        Assert.Equal(0, result.AsNumber());
    }

    [Fact]
    public void Evaluate_Modulo_ReturnsRemainder()
    {
        var result = ExpressionEngine.Evaluate("10 % 3", _ => null);
        Assert.Equal(1, result.AsNumber());
    }

    [Fact]
    public void Evaluate_OperatorPrecedence_MultiplicationBeforeAddition()
    {
        var result = ExpressionEngine.Evaluate("2 + 3 * 4", _ => null);
        Assert.Equal(14, result.AsNumber());
    }

    [Fact]
    public void Evaluate_Parentheses_OverridePrecedence()
    {
        var result = ExpressionEngine.Evaluate("(2 + 3) * 4", _ => null);
        Assert.Equal(20, result.AsNumber());
    }

    [Fact]
    public void Evaluate_NegativeNumber()
    {
        var result = ExpressionEngine.Evaluate("-5 + 3", _ => null);
        Assert.Equal(-2, result.AsNumber());
    }

    [Fact]
    public void Evaluate_UnaryNegation()
    {
        var result = ExpressionEngine.Evaluate("-(5 + 3)", _ => null);
        Assert.Equal(-8, result.AsNumber());
    }

    // ── 比较运算 ──

    [Fact]
    public void Evaluate_GreaterThan_True()
    {
        var result = ExpressionEngine.Evaluate("5 > 3", _ => null);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_GreaterThan_False()
    {
        var result = ExpressionEngine.Evaluate("3 > 5", _ => null);
        Assert.False(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_LessThanOrEqual_True()
    {
        var result = ExpressionEngine.Evaluate("3 <= 3", _ => null);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_Equality_True()
    {
        var result = ExpressionEngine.Evaluate("5 == 5", _ => null);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_Inequality_True()
    {
        var result = ExpressionEngine.Evaluate("5 != 3", _ => null);
        Assert.True(result.AsBoolean());
    }

    // ── 逻辑运算 ──

    [Fact]
    public void Evaluate_LogicalAnd_BothTrue_ReturnsTrue()
    {
        var result = ExpressionEngine.Evaluate("1 && 1", _ => null);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_LogicalAnd_OneFalse_ReturnsFalse()
    {
        var result = ExpressionEngine.Evaluate("1 && 0", _ => null);
        Assert.False(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_LogicalOr_BothFalse_ReturnsFalse()
    {
        var result = ExpressionEngine.Evaluate("0 || 0", _ => null);
        Assert.False(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_LogicalOr_OneTrue_ReturnsTrue()
    {
        var result = ExpressionEngine.Evaluate("0 || 1", _ => null);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_LogicalNot_True()
    {
        var result = ExpressionEngine.Evaluate("!0", _ => null);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_LogicalNot_False()
    {
        var result = ExpressionEngine.Evaluate("!1", _ => null);
        Assert.False(result.AsBoolean());
    }

    // ── 三元运算 ──

    [Fact]
    public void Evaluate_Ternary_TrueCondition_ReturnsTrueBranch()
    {
        var result = ExpressionEngine.Evaluate("5 > 3 ? 100 : 200", _ => null);
        Assert.Equal(100, result.AsNumber());
    }

    [Fact]
    public void Evaluate_Ternary_FalseCondition_ReturnsFalseBranch()
    {
        var result = ExpressionEngine.Evaluate("3 > 5 ? 100 : 200", _ => null);
        Assert.Equal(200, result.AsNumber());
    }

    [Fact]
    public void Evaluate_Ternary_WithStringLiterals()
    {
        var result = ExpressionEngine.Evaluate("1 > 0 ? \"red\" : \"green\"", _ => null);
        Assert.Equal("red", result.ToString());
    }

    [Fact]
    public void Evaluate_Ternary_Nested()
    {
        var result = ExpressionEngine.Evaluate("1 > 0 ? (2 > 1 ? \"a\" : \"b\") : \"c\"", _ => null);
        Assert.Equal("a", result.ToString());
    }

    // ── 变量引用 ──

    [Fact]
    public void Evaluate_VariableReference_ResolvesValue()
    {
        var vars = new Dictionary<string, double> { ["temperature"] = 75.5 };
        var result = ExpressionEngine.Evaluate("temperature", vars);
        Assert.Equal(75.5, result.AsNumber());
    }

    [Fact]
    public void Evaluate_VariableInExpression()
    {
        var vars = new Dictionary<string, double> { ["tag1"] = 100, ["tag2"] = 50 };
        var result = ExpressionEngine.Evaluate("tag1 + tag2", vars);
        Assert.Equal(150, result.AsNumber());
    }

    [Fact]
    public void Evaluate_VariableComparison()
    {
        var vars = new Dictionary<string, double> { ["temp"] = 85 };
        var result = ExpressionEngine.Evaluate("temp > 80 ? 1 : 0", vars);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_UnknownVariable_DefaultsToZero()
    {
        var result = ExpressionEngine.Evaluate("unknownVar + 5", _ => null);
        Assert.Equal(5, result.AsNumber());
    }

    // ── UDT 字段引用 ──

    [Fact]
    public void Evaluate_UdtFieldReference_ResolvesValue()
    {
        var result = ExpressionEngine.Evaluate(
            "motor1.speed > 100",
            name => name == "motor1.speed" ? ExpressionResult.Number(150) : null);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_UdtFieldInArithmetic()
    {
        var result = ExpressionEngine.Evaluate(
            "motor1.speed + motor1.current",
            name => name switch
            {
                "motor1.speed" => ExpressionResult.Number(100),
                "motor1.current" => ExpressionResult.Number(5),
                _ => null
            });
        Assert.Equal(105, result.AsNumber());
    }

    // ── 字符串操作 ──

    [Fact]
    public void Evaluate_StringConcatenation()
    {
        var result = ExpressionEngine.Evaluate("\"Hello\" + \" \" + \"World\"", _ => null);
        Assert.Equal("Hello World", result.ToString());
    }

    [Fact]
    public void Evaluate_StringComparison()
    {
        var result = ExpressionEngine.Evaluate("\"red\" == \"red\"", _ => null);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_StringNotEqual()
    {
        var result = ExpressionEngine.Evaluate("\"red\" != \"green\"", _ => null);
        Assert.True(result.AsBoolean());
    }

    // ── 复合表达式 ──

    [Fact]
    public void Evaluate_ComplexExpression1()
    {
        // (tag1 > 100 && tag2 < 50) ? "alarm" : "ok"
        var result = ExpressionEngine.Evaluate(
            "(tag1 > 100 && tag2 < 50) ? \"alarm\" : \"ok\"",
            name => name switch
            {
                "tag1" => ExpressionResult.Number(120),
                "tag2" => ExpressionResult.Number(30),
                _ => null
            });
        Assert.Equal("alarm", result.ToString());
    }

    [Fact]
    public void Evaluate_ComplexExpression2()
    {
        // !tag1 && (tag2 + tag3 > 200)
        var result = ExpressionEngine.Evaluate(
            "!tag1 && (tag2 + tag3 > 200)",
            name => name switch
            {
                "tag1" => ExpressionResult.Number(0),
                "tag2" => ExpressionResult.Number(100),
                "tag3" => ExpressionResult.Number(150),
                _ => null
            });
        Assert.True(result.AsBoolean());
    }

    // ── 变量名提取 ──

    [Fact]
    public void ExtractVariableNames_SimpleExpression()
    {
        var names = ExpressionEngine.ExtractVariableNames("tag1 + tag2");
        Assert.Contains("tag1", names);
        Assert.Contains("tag2", names);
        Assert.Equal(2, names.Count);
    }

    [Fact]
    public void ExtractVariableNames_UdtField()
    {
        var names = ExpressionEngine.ExtractVariableNames("motor1.speed > 100");
        Assert.Contains("motor1.speed", names);
        Assert.Single(names);
    }

    [Fact]
    public void ExtractVariableNames_NoDuplicates()
    {
        var names = ExpressionEngine.ExtractVariableNames("tag1 + tag1 * tag1");
        Assert.Single(names);
        Assert.Contains("tag1", names);
    }

    [Fact]
    public void ExtractVariableNames_EmptyExpression_ReturnsEmpty()
    {
        var names = ExpressionEngine.ExtractVariableNames("");
        Assert.Empty(names);
    }

    // ── 验证 ──

    [Fact]
    public void TryCompile_ValidExpression_ReturnsTrue()
    {
        var ok = ExpressionEngine.TryCompile("tag1 > 100 ? \"red\" : \"green\"", out var error);
        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void TryCompile_InvalidExpression_ReturnsFalse()
    {
        var ok = ExpressionEngine.TryCompile("tag1 +", out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCompile_EmptyExpression_ReturnsFalse()
    {
        var ok = ExpressionEngine.TryCompile("", out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCompile_MissingClosingParen_ReturnsFalse()
    {
        var ok = ExpressionEngine.TryCompile("(tag1 + tag2", out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCompile_TernaryMissingColon_ReturnsFalse()
    {
        var ok = ExpressionEngine.TryCompile("1 > 0 ? \"a\"", out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    // ── 字典求值便捷重载 ──

    [Fact]
    public void Evaluate_WithDictionary_ResolvesVariables()
    {
        var vars = new Dictionary<string, double>
        {
            ["temp"] = 85,
            ["pressure"] = 120
        };
        var result = ExpressionEngine.Evaluate("temp > 80 && pressure > 100", vars);
        Assert.True(result.AsBoolean());
    }

    // ── 边界情况 ──

    [Fact]
    public void Evaluate_OnlyNumber_ReturnsNumber()
    {
        var result = ExpressionEngine.Evaluate("42", _ => null);
        Assert.Equal(42, result.AsNumber());
    }

    [Fact]
    public void Evaluate_TrueKeyword_ReturnsOne()
    {
        var result = ExpressionEngine.Evaluate("true", _ => null);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_FalseKeyword_ReturnsZero()
    {
        var result = ExpressionEngine.Evaluate("false", _ => null);
        Assert.False(result.AsBoolean());
    }

    [Fact]
    public void Evaluate_DecimalNumber()
    {
        var result = ExpressionEngine.Evaluate("3.14 * 2", _ => null);
        Assert.Equal(6.28, result.AsNumber(), 2);
    }

    [Fact]
    public void Evaluate_EscapedStringCharacters()
    {
        var result = ExpressionEngine.Evaluate("\"line1\\nline2\"", _ => null);
        Assert.Equal("line1\nline2", result.ToString());
    }

    // ── 实际 HMI 场景 ──

    [Fact]
    public void HmiScenario_ColorByValue()
    {
        // 温度 > 80 显示红色，否则绿色
        var vars = new Dictionary<string, double> { ["temperature"] = 85 };
        var result = ExpressionEngine.Evaluate("temperature > 80 ? \"red\" : \"green\"", vars);
        Assert.Equal("red", result.ToString());
    }

    [Fact]
    public void HmiScenario_VisibilityByAlarm()
    {
        // 报警位为真时显示控件
        var vars = new Dictionary<string, double> { ["alarmActive"] = 1 };
        var result = ExpressionEngine.Evaluate("alarmActive == 1", vars);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void HmiScenario_MultiConditionAlarm()
    {
        // 温度过高 AND 压力过高
        var vars = new Dictionary<string, double> { ["temp"] = 90, ["pressure"] = 150 };
        var result = ExpressionEngine.Evaluate("temp > 80 && pressure > 100", vars);
        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void HmiScenario_MotorUdtField()
    {
        // 电机 UDT：速度 + 电流超出阈值报警
        var result = ExpressionEngine.Evaluate(
            "motor1.speed > 1000 || motor1.current > 50",
            name => name switch
            {
                "motor1.speed" => ExpressionResult.Number(1200),
                "motor1.current" => ExpressionResult.Number(30),
                _ => null
            });
        Assert.True(result.AsBoolean());
    }
}
