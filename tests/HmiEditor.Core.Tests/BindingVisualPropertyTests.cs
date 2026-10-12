using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// P1-1：表达式驱动视觉属性（颜色 / 可见性）。
///
/// 本文件锁两层：
/// <list type="bullet">
///   <item><b>注册表</b>：属性名 → 值域的唯一来源，以及「数值 → 颜色」与生成代码
///     <c>lv_color_hex((uint32_t)(expr) &amp; 0xFFFFFFu)</c> 的同规则换算；</item>
///   <item><b>设计时求值</b>：预览模式生效、非预览回退、旧字段不回退、语法错回退。</item>
/// </list>
/// 「颜色必须是数值而非 hex 字符串」是本设计的前提：C 是静态类型，三元里
/// <c>"#ff0000"</c> 与 <c>"#00ff00"</c> 若混入数值分支会直接编译不过 ⇒ 三端统一走数值。
/// </summary>
public class BindingVisualPropertyTests
{
    // ── 注册表：属性名 → 值域 ──

    [Fact]
    public void Find_RegisteredVisualProperties_ResolveToExpectedKind()
    {
        Assert.Equal(BindingVisualKind.Color, BindingVisualProperty.Find("TextColor")!.Kind);
        Assert.Equal(BindingVisualKind.Color, BindingVisualProperty.Find("BgColor")!.Kind);
        Assert.Equal(BindingVisualKind.Visibility, BindingVisualProperty.Find("IsVisible")!.Kind);
        Assert.Null(BindingVisualProperty.Find("Text"));
        Assert.Null(BindingVisualProperty.Find("Value"));
        Assert.Null(BindingVisualProperty.Find(null));
    }

    [Fact]
    public void IsVisual_ValueProperties_AreNotVisual()
    {
        // 值属性（Text/Value）必须落在视觉通路之外，否则刷新路径会重复消费同一条绑定。
        Assert.False(BindingVisualProperty.IsVisual("Text"));
        Assert.False(BindingVisualProperty.IsVisual("Value"));
        Assert.False(BindingVisualProperty.IsVisual("IsChecked"));
        Assert.False(BindingVisualProperty.IsVisual(""));
        Assert.False(BindingVisualProperty.IsVisual(null));

        Assert.True(BindingVisualProperty.IsVisual("TextColor"));
        Assert.True(BindingVisualProperty.IsVisual("IsVisible"));
    }

    [Fact]
    public void All_Registry_HasNoDuplicatePropertyNames()
    {
        var names = BindingVisualProperty.All.Select(p => p.PropertyName).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.NotEmpty(names);
    }

    // ── 数值 → 颜色：与 lv_color_hex((uint32_t)(expr) & 0xFFFFFFu) 同规则 ──

    [Theory]
    [InlineData(16711680d, "#FF0000")]   // 0xFF0000 红
    [InlineData(65280d, "#00FF00")]      // 0x00FF00 绿
    [InlineData(255d, "#0000FF")]        // 0x0000FF 蓝
    [InlineData(0d, "#000000")]
    [InlineData(16777215d, "#FFFFFF")]   // 0xFFFFFF
    [InlineData(-1d, "#FFFFFF")]         // 负数按位截断，与 C 的 (uint32_t)(-1) & 0xFFFFFF 一致
    [InlineData(33488896d, "#FF0000")]   // 0x1FF0000，高位被截断
    public void ColorFromNumber_MatchesGeneratedCodeTruncationRule(double value, string expected)
    {
        Assert.Equal(expected, BindingVisualProperty.ColorFromNumber(value));
    }

    [Fact]
    public void ColorFromNumber_NonFinite_ReturnsNull()
    {
        Assert.Null(BindingVisualProperty.ColorFromNumber(double.NaN));
        Assert.Null(BindingVisualProperty.ColorFromNumber(double.PositiveInfinity));
        Assert.Null(BindingVisualProperty.ColorFromNumber(double.NegativeInfinity));
    }

    // ── 设计时求值：颜色 ──

    [Fact]
    public void ResolveVisualColor_PreviewMode_ExpressionEvaluates()
    {
        // 经典场景：温度超限变红（0xFF0000），否则绿（0x00FF00）
        var control = ControlWith("TextColor", "temp > 100 ? 16711680 : 65280");

        var hot = PreviewValueResolver.ResolveVisualColor(control, name => name == "temp" ? 120 : null, "TextColor");
        var cool = PreviewValueResolver.ResolveVisualColor(control, name => name == "temp" ? 20 : null, "TextColor");

        Assert.Equal("#FF0000", hot);
        Assert.Equal("#00FF00", cool);
    }

    [Fact]
    public void ResolveVisualColor_NotPreviewMode_ReturnsNull()
    {
        // 设计态显示的永远是设计值 —— 提供器为 null 即「非预览」，必须回退。
        var control = ControlWith("TextColor", "temp > 100 ? 16711680 : 65280");

        Assert.Null(PreviewValueResolver.ResolveVisualColor(control, valueProvider: null, "TextColor"));
    }

    [Fact]
    public void ResolveVisualColor_NoBindingForThatProperty_ReturnsNull()
    {
        // 控件只绑了 Text（值属性），请求 TextColor 不得命中那条绑定。
        var control = ControlWith("Text", "temp");

        Assert.Null(PreviewValueResolver.ResolveVisualColor(control, name => 42, "TextColor"));
    }

    [Fact]
    public void ResolveVisualColor_SyntaxError_ReturnsNull()
    {
        var control = ControlWith("TextColor", "temp >");

        Assert.Null(PreviewValueResolver.ResolveVisualColor(control, name => 42, "TextColor"));
    }

    [Fact]
    public void ResolveVisualColor_DoesNotFallBackToLegacyBindingField()
    {
        // 旧字段 VariableBindingId 只承载「主显示绑定」。若视觉通路也吃它，
        // 控件一旦配了旧字段，TextColor 就会把显示数值（如 42）当成颜色用。
        var control = ControlWith("Text", "temp", legacyVariableId: "speed");

        Assert.Null(PreviewValueResolver.ResolveVisualColor(control, name => 42, "TextColor"));
        // 值通路仍然照旧命中旧字段 —— 本次改动不能破坏既有回退行为。
        Assert.NotNull(PreviewValueResolver.ResolveDisplayText(control, name => 42));
    }

    [Fact]
    public void ResolveVisualColor_VariableSource_NumberIsUsedAsRgb()
    {
        // 变量源（非表达式）绑到颜色属性同样成立：变量值就是 24 位 RGB。
        var control = ControlWith("BgColor", expression: null, variableId: "lampColor", kind: BindingSourceKind.Variable);
        control.BgColor = "#123456";

        Assert.Equal("#FF0000",
            PreviewValueResolver.ResolveVisualColor(control, name => name == "lampColor" ? 16711680 : null, "BgColor"));
    }

    // ── 设计时求值：可见性 ──

    [Fact]
    public void ResolveVisualVisibility_NonZeroIsVisible()
    {
        var control = ControlWith("IsVisible", "alarm");

        Assert.True(PreviewValueResolver.ResolveVisualVisibility(control, name => name == "alarm" ? 1 : null));
        Assert.False(PreviewValueResolver.ResolveVisualVisibility(control, name => name == "alarm" ? 0 : null));
    }

    [Fact]
    public void ResolveVisualVisibility_NoBinding_ReturnsNull()
    {
        // 回退判据：null 让调用方用静态 IsVisible，false 会误把控件藏起来。
        var control = ControlWith("TextColor", "temp");

        Assert.Null(PreviewValueResolver.ResolveVisualVisibility(control, name => 1));
        Assert.Null(PreviewValueResolver.ResolveVisualVisibility(control, valueProvider: null));
    }

    [Fact]
    public void ResolveVisualVisibility_ComparisonExpression_WorksLikeGeneratedCode()
    {
        var control = ControlWith("IsVisible", "level >= 3");

        Assert.True(PreviewValueResolver.ResolveVisualVisibility(control, name => name == "level" ? 5 : null));
        Assert.False(PreviewValueResolver.ResolveVisualVisibility(control, name => name == "level" ? 1 : null));
    }

    // ── 负控：视觉通路不得吞掉值属性 ──

    [Fact]
    public void ValuePropertyBinding_IsNotConsumedByVisualPath()
    {
        var control = ControlWith("Value", "speed * 2");

        Assert.Null(PreviewValueResolver.ResolveVisualColor(control, name => 10, "TextColor"));
        Assert.Null(PreviewValueResolver.ResolveVisualColor(control, name => 10, "BgColor"));
        Assert.Null(PreviewValueResolver.ResolveVisualVisibility(control, name => 10));
    }

    // ── helper ──

    private static HmiControl ControlWith(
        string propertyName,
        string? expression,
        string variableId = "",
        BindingSourceKind kind = BindingSourceKind.Expression,
        string? legacyVariableId = null)
    {
        var control = new HmiControl
        {
            Id = "ctrl_visual",
            Type = HmiControlType.Label,
            VariableBindingId = legacyVariableId,
        };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = propertyName,
            VariableId = variableId,
            Kind = kind,
            Expression = expression,
            Direction = BindingDirection.ReadOnly,
        });
        return control;
    }
}
