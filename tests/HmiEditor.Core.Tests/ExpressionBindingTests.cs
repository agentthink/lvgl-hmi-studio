using HmiEditor.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// I1: 表达式绑定单元测试。
/// 覆盖绑定求值器（变量/表达式源）、变量提取、C 转译、预览解析、运行时标签、CodeGen 发射。
/// </summary>
public class ExpressionBindingTests
{
    // ── ExpressionBindingEvaluator ──

    [Fact]
    public void EvaluateBinding_ExpressionKind_NumericTernary_Evaluates()
    {
        var binding = new VariableBindingData
        {
            Kind = BindingSourceKind.Expression,
            Expression = "temp > 100 ? 1 : 0",
            Direction = BindingDirection.ReadOnly
        };

        var result = ExpressionBindingEvaluator.EvaluateBinding(binding,
            name => name == "temp" ? ExpressionResult.Number(150) : null);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.AsNumber());
    }

    [Fact]
    public void EvaluateBinding_ExpressionKind_StringResult_ReturnsString()
    {
        var binding = new VariableBindingData
        {
            Kind = BindingSourceKind.Expression,
            Expression = "temp > 100 ? \"high\" : \"low\"",
            Direction = BindingDirection.ReadOnly
        };

        var result = ExpressionBindingEvaluator.EvaluateBinding(binding,
            name => name == "temp" ? ExpressionResult.Number(50) : null);

        Assert.NotNull(result);
        Assert.Equal("low", result.Value.StringValue);
    }

    [Fact]
    public void EvaluateBinding_VariableKind_UsesVariableId()
    {
        var binding = new VariableBindingData
        {
            Kind = BindingSourceKind.Variable,
            VariableId = "motor1.speed",
            Direction = BindingDirection.ReadOnly
        };

        var result = ExpressionBindingEvaluator.EvaluateBinding(binding,
            name => name == "motor1.speed" ? ExpressionResult.Number(42) : null);

        Assert.NotNull(result);
        Assert.Equal(42, result.Value.AsNumber());
    }

    [Fact]
    public void EvaluateBinding_InvalidExpression_ReturnsNull()
    {
        var binding = new VariableBindingData
        {
            Kind = BindingSourceKind.Expression,
            Expression = "temp >> 100",
            Direction = BindingDirection.ReadOnly
        };

        var result = ExpressionBindingEvaluator.EvaluateBinding(binding, _ => null);

        Assert.Null(result);
    }

    [Fact]
    public void EvaluateBinding_EmptyExpression_ReturnsNull()
    {
        var binding = new VariableBindingData
        {
            Kind = BindingSourceKind.Expression,
            Expression = "  ",
            Direction = BindingDirection.ReadOnly
        };

        var result = ExpressionBindingEvaluator.EvaluateBinding(binding, _ => null);

        Assert.Null(result);
    }

    // ── ExtractVariables ──

    [Fact]
    public void ExtractVariables_ExpressionKind_ReturnsAllReferencedNames()
    {
        var binding = new VariableBindingData
        {
            Kind = BindingSourceKind.Expression,
            Expression = "a + b > c ? d : 0"
        };

        var names = ExpressionBindingEvaluator.ExtractVariables(binding);

        Assert.Contains("a", names);
        Assert.Contains("b", names);
        Assert.Contains("c", names);
        Assert.Contains("d", names);
        Assert.Equal(4, names.Count);
    }

    [Fact]
    public void ExtractVariables_VariableKind_ReturnsSingleId()
    {
        var binding = new VariableBindingData
        {
            Kind = BindingSourceKind.Variable,
            VariableId = "motor1.speed"
        };

        var names = ExpressionBindingEvaluator.ExtractVariables(binding);

        Assert.Single(names);
        Assert.Contains("motor1.speed", names);
    }

    // ── C 表达式转译 ──

    [Fact]
    public void ToCExpression_Ternary_TranslatesWithTagAccess()
    {
        var c = ExpressionEngine.ToCExpression("temp > 100 ? \"high\" : \"low\"");

        Assert.Equal("((hmi_get_variable_value(\"temp\") > 100) ? \"high\" : \"low\")", c);
    }

    [Fact]
    public void ToCExpression_LogicalOperators_MapToC()
    {
        var c = ExpressionEngine.ToCExpression("a >= 1 && !b");

        Assert.Equal("((hmi_get_variable_value(\"a\") >= 1) && (!hmi_get_variable_value(\"b\")))", c);
    }

    [Fact]
    public void ToCExpression_InvalidExpression_ReturnsNull()
    {
        Assert.Null(ExpressionEngine.ToCExpression("a ?? b"));
    }

    // ── PreviewValueResolver ──

    [Fact]
    public void Preview_ExpressionBinding_StringTernaryShowsResult()
    {
        var control = new HmiControl
        {
            Type = HmiControlType.Label,
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = "Text",
                    Kind = BindingSourceKind.Expression,
                    Expression = "a > 1 ? \"on\" : \"off\"",
                    Direction = BindingDirection.ReadOnly
                }
            }
        };

        var text = PreviewValueResolver.ResolveDisplayText(control,
            name => name == "a" ? 2 : null);

        Assert.Equal("on", text);
    }

    [Fact]
    public void Preview_ExpressionBinding_NumericShowsFormatted()
    {
        var control = new HmiControl
        {
            Type = HmiControlType.Gauge,
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = "Value",
                    Kind = BindingSourceKind.Expression,
                    Expression = "b * 2",
                    Direction = BindingDirection.ReadOnly
                }
            }
        };

        var text = PreviewValueResolver.ResolveDisplayText(control,
            name => name == "b" ? 25 : null);

        Assert.Equal("50.0", text);
    }

    [Fact]
    public void Preview_ExpressionBinding_InvalidFallsBackToNull()
    {
        var control = new HmiControl
        {
            Type = HmiControlType.Label,
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = "Text",
                    Kind = BindingSourceKind.Expression,
                    Expression = "a >> 1",
                    Direction = BindingDirection.ReadOnly
                }
            }
        };

        var text = PreviewValueResolver.ResolveDisplayText(control,
            name => name == "a" ? 2 : null);

        Assert.Null(text);
    }

    [Fact]
    public void Preview_ExpressionBinding_ByPropertyName()
    {
        var control = new HmiControl
        {
            Type = HmiControlType.Label,
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = "Value",
                    Kind = BindingSourceKind.Expression,
                    Expression = "x + 1",
                    Direction = BindingDirection.ReadOnly
                }
            }
        };

        var byValue = PreviewValueResolver.ResolveDisplayText(control,
            name => name == "x" ? 41 : null, "Value");
        var byOther = PreviewValueResolver.ResolveDisplayText(control,
            name => name == "x" ? 41 : null, "Text");

        Assert.Equal("42.00", byValue);
        Assert.Null(byOther);
    }

    // ── RuntimeBindingEngine ──

    [Fact]
    public void RuntimeTags_ExpressionBinding_RegistersReferencedVariables()
    {
        var project = new HmiProject { LvglVersion = "9.2" };
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "a", Name = "a", Address = "100", ConnectionId = "c1"
        });
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "b", Name = "b", Address = "101", ConnectionId = "c1"
        });

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1", Type = HmiControlType.Label,
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = "Text",
                    Kind = BindingSourceKind.Expression,
                    Expression = "a + b > 10 ? 1 : 0",
                    Direction = BindingDirection.ReadOnly
                }
            }
        });
        project.Screens.Add(screen);

        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        Assert.Equal(2, engine.TagCount);
        Assert.NotNull(engine.FindTag("a"));
        Assert.NotNull(engine.FindTag("b"));
    }

    // ── CodeGen ──

    [Fact]
    public void GenerateScreenCode_ExpressionBinding_EmitsExpressionCommentWithC()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s2", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1", Name = "Label1", Type = HmiControlType.Label,
            X = 10, Y = 20, Width = 200, Height = 30,
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = "Text",
                    Kind = BindingSourceKind.Expression,
                    Expression = "temp > 100 ? 1 : 0",
                    Direction = BindingDirection.ReadOnly
                }
            }
        });

        var code = generator.GenerateScreenCode(screen);

        Assert.Contains("// Expression binding: Text = temp > 100 ? 1 : 0", code);
        Assert.Contains("hmi_get_variable_value(\"temp\")", code);
    }

    [Fact]
    public void GenerateScreenCode_InvalidExpressionBinding_EmitsWarning()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s3", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1", Name = "Label1", Type = HmiControlType.Label,
            X = 10, Y = 20, Width = 200, Height = 30,
            VariableBindings =
            {
                new VariableBindingData
                {
                    PropertyName = "Text",
                    Kind = BindingSourceKind.Expression,
                    Expression = "a >> 1",
                    Direction = BindingDirection.ReadOnly
                }
            }
        });

        var code = generator.GenerateScreenCode(screen);

        Assert.Contains("WARNING: Invalid expression for 'Text'", code);
    }
}
