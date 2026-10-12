using HmiEditor.Core.Design;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// FF6 验收测试 —— 属性编辑器类型安全。
/// 验收标准：100% 非法值被编辑器拦截。
/// 覆盖：位置非负、尺寸下限、圆角/边框非负、颜色格式、名称合法性。
/// </summary>
public class PropertyValidatorTests
{
    // ── ClampPosition ──

    [Theory]
    [InlineData(0, 0)]           // 零值合法
    [InlineData(100, 100)]       // 正常值
    [InlineData(99999, 99999)]   // 边界值
    public void ClampPosition_ValidValue_PassesThrough(double input, double expected)
    {
        Assert.Equal(expected, PropertyValidator.ClampPosition(input));
    }

    [Theory]
    [InlineData(-1)]      // 负值 → 0
    [InlineData(-100)]    // 大负值 → 0
    [InlineData(-0.1)]   // 微小负值 → 0
    public void ClampPosition_Negative_ClampsToZero(double input)
    {
        Assert.Equal(0, PropertyValidator.ClampPosition(input));
    }

    [Fact]
    public void ClampPosition_NaN_ReturnsZero()
    {
        Assert.Equal(0, PropertyValidator.ClampPosition(double.NaN));
    }

    [Fact]
    public void ClampPosition_Infinity_ReturnsZero()
    {
        Assert.Equal(0, PropertyValidator.ClampPosition(double.PositiveInfinity));
        Assert.Equal(0, PropertyValidator.ClampPosition(double.NegativeInfinity));
    }

    [Fact]
    public void ClampPosition_OverMax_ClampsToMax()
    {
        Assert.Equal(PropertyValidator.MaxPosition,
            PropertyValidator.ClampPosition(PropertyValidator.MaxPosition + 1));
    }

    // ── ClampSize ──

    [Theory]
    [InlineData(10, 10)]      // 最小值
    [InlineData(100, 100)]    // 正常值
    [InlineData(5000, 5000)]  // 大尺寸
    public void ClampSize_ValidValue_PassesThrough(double input, double expected)
    {
        Assert.Equal(expected, PropertyValidator.ClampSize(input));
    }

    [Theory]
    [InlineData(9)]       // 低于最小值
    [InlineData(0)]       // 零
    [InlineData(-5)]      // 负值
    [InlineData(-100)]    // 大负值
    public void ClampSize_BelowMinimum_ClampsToMin(double input)
    {
        Assert.Equal(PropertyValidator.MinControlSize,
            PropertyValidator.ClampSize(input));
    }

    [Fact]
    public void ClampSize_NaN_ReturnsMin()
    {
        Assert.Equal(PropertyValidator.MinControlSize,
            PropertyValidator.ClampSize(double.NaN));
    }

    [Fact]
    public void ClampSize_Infinity_ReturnsMin()
    {
        Assert.Equal(PropertyValidator.MinControlSize,
            PropertyValidator.ClampSize(double.PositiveInfinity));
        Assert.Equal(PropertyValidator.MinControlSize,
            PropertyValidator.ClampSize(double.NegativeInfinity));
    }

    [Fact]
    public void ClampSize_OverMax_ClampsToMax()
    {
        Assert.Equal(PropertyValidator.MaxSize,
            PropertyValidator.ClampSize(PropertyValidator.MaxSize + 1));
    }

    // ── ClampRadius ──

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 10)]
    [InlineData(100, 100)]
    public void ClampRadius_ValidValue_PassesThrough(int input, int expected)
    {
        Assert.Equal(expected, PropertyValidator.ClampRadius(input));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void ClampRadius_Negative_ClampsToZero(int input)
    {
        Assert.Equal(0, PropertyValidator.ClampRadius(input));
    }

    [Fact]
    public void ClampRadius_OverMax_ClampsToMax()
    {
        Assert.Equal(PropertyValidator.MaxRadius,
            PropertyValidator.ClampRadius(PropertyValidator.MaxRadius + 1));
    }

    // ── ClampBorderWidth ──

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(50, 50)]
    public void ClampBorderWidth_ValidValue_PassesThrough(int input, int expected)
    {
        Assert.Equal(expected, PropertyValidator.ClampBorderWidth(input));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void ClampBorderWidth_Negative_ClampsToZero(int input)
    {
        Assert.Equal(0, PropertyValidator.ClampBorderWidth(input));
    }

    [Fact]
    public void ClampBorderWidth_OverMax_ClampsToMax()
    {
        Assert.Equal(PropertyValidator.MaxBorderWidth,
            PropertyValidator.ClampBorderWidth(PropertyValidator.MaxBorderWidth + 1));
    }

    // ── IsValidColor ──

    [Theory]
    [InlineData("#FF0000")]      // 标准 6 位
    [InlineData("#FF0000FF")]    // 8 位带 alpha
    [InlineData("#F00")]          // 3 位简写
    [InlineData("FF0000")]        // 无井号
    [InlineData("f00")]           // 小写简写
    [InlineData("#abcdef")]       // 小写完整
    [InlineData("#ABCDEF")]       // 大写完整
    [InlineData("#aBcDeF")]       // 混合大小写
    public void IsValidColor_ValidHex_ReturnsTrue(string color)
    {
        Assert.True(PropertyValidator.IsValidColor(color));
    }

    [Theory]
    [InlineData(null)]           // null → 合法（不设置颜色）
    [InlineData("")]              // 空 → 合法
    [InlineData("   ")]          // 空白 → 合法
    public void IsValidColor_EmptyOrNull_ReturnsTrue(string? color)
    {
        Assert.True(PropertyValidator.IsValidColor(color));
    }

    [Theory]
    [InlineData("red")]           // 颜色名称
    [InlineData("#GGG")]          // 非法字符
    [InlineData("#12")]           // 太短
    [InlineData("#12345")]        // 长度不对
    [InlineData("#1234567")]      // 7 位
    [InlineData("rgb(255,0,0)")]  // RGB 函数
    [InlineData("#XYZXYZ")]       // 非法字符
    public void IsValidColor_InvalidFormat_ReturnsFalse(string color)
    {
        Assert.False(PropertyValidator.IsValidColor(color));
    }

    // ── SanitizeColor ──

    [Fact]
    public void SanitizeColor_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(PropertyValidator.SanitizeColor(null));
        Assert.Null(PropertyValidator.SanitizeColor(""));
        Assert.Null(PropertyValidator.SanitizeColor("   "));
    }

    [Fact]
    public void SanitizeColor_WithoutHash_PrefixesHash()
    {
        Assert.Equal("#FF0000", PropertyValidator.SanitizeColor("FF0000"));
        Assert.Equal("#F00", PropertyValidator.SanitizeColor("F00"));
    }

    [Fact]
    public void SanitizeColor_WithHash_PreservesHash()
    {
        Assert.Equal("#FF0000", PropertyValidator.SanitizeColor("#FF0000"));
    }

    [Fact]
    public void SanitizeColor_InvalidFormat_ReturnsNull()
    {
        Assert.Null(PropertyValidator.SanitizeColor("red"));
        Assert.Null(PropertyValidator.SanitizeColor("#GGG"));
        Assert.Null(PropertyValidator.SanitizeColor("not-a-color"));
    }

    [Fact]
    public void SanitizeColor_TrimsWhitespace()
    {
        Assert.Equal("#FF0000", PropertyValidator.SanitizeColor("  #FF0000  "));
        Assert.Equal("#FF0000", PropertyValidator.SanitizeColor("  FF0000  "));
    }

    // ── IsValidName ──

    [Theory]
    [InlineData("Button1")]
    [InlineData("My Control")]
    [InlineData("A")]
    public void IsValidName_ValidName_ReturnsTrue(string name)
    {
        Assert.True(PropertyValidator.IsValidName(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidName_EmptyOrNull_ReturnsFalse(string? name)
    {
        Assert.False(PropertyValidator.IsValidName(name));
    }

    [Fact]
    public void IsValidName_TooLong_ReturnsFalse()
    {
        var longName = new string('A', 101);
        Assert.False(PropertyValidator.IsValidName(longName));
    }

    [Fact]
    public void IsValidName_Exactly100Chars_ReturnsTrue()
    {
        var name = new string('A', 100);
        Assert.True(PropertyValidator.IsValidName(name));
    }

    // ── SanitizeName ──

    [Fact]
    public void SanitizeName_Null_ReturnsFallback()
    {
        Assert.Equal("Default", PropertyValidator.SanitizeName(null, "Default"));
    }

    [Fact]
    public void SanitizeName_Empty_ReturnsFallback()
    {
        Assert.Equal("Default", PropertyValidator.SanitizeName("", "Default"));
        Assert.Equal("Default", PropertyValidator.SanitizeName("   ", "Default"));
    }

    [Fact]
    public void SanitizeName_ValidName_ReturnsTrimmed()
    {
        Assert.Equal("Button1", PropertyValidator.SanitizeName("  Button1  ", "Default"));
    }

    [Fact]
    public void SanitizeName_TooLong_Truncates()
    {
        var longName = new string('A', 150);
        var result = PropertyValidator.SanitizeName(longName, "Default");
        Assert.Equal(100, result.Length);
    }

    // ── FF6 综合验收：100% 非法值被拦截 ──

    [Fact]
    public void FF6_AllIllegalValues_Intercepted()
    {
        // 模拟属性面板可能接收到的所有非法值类型，
        // 确保每一个都被 PropertyValidator 正确拦截/修正

        // 负位置 → 0
        Assert.Equal(0, PropertyValidator.ClampPosition(-50));

        // 过小尺寸 → 最小值
        Assert.Equal(PropertyValidator.MinControlSize, PropertyValidator.ClampSize(5));

        // NaN 尺寸 → 最小值（不会导致控件消失）
        Assert.Equal(PropertyValidator.MinControlSize, PropertyValidator.ClampSize(double.NaN));

        // 负圆角 → 0
        Assert.Equal(0, PropertyValidator.ClampRadius(-5));

        // 负边框 → 0
        Assert.Equal(0, PropertyValidator.ClampBorderWidth(-3));

        // 非法颜色 → null（不会导致渲染崩溃）
        Assert.Null(PropertyValidator.SanitizeColor("not-a-color"));

        // 空名称 → fallback（不会导致控件丢失名称）
        Assert.Equal("Fallback", PropertyValidator.SanitizeName("", "Fallback"));

        // 极端大值被限制
        Assert.Equal(PropertyValidator.MaxPosition,
            PropertyValidator.ClampPosition(double.MaxValue));
        Assert.Equal(PropertyValidator.MaxSize,
            PropertyValidator.ClampSize(double.MaxValue));
    }
}
