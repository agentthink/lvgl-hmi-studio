using HmiEditor.AI.Models;
using HmiEditor.AI.Services;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// LayoutAnalyzer 测试 —— 布局质量分析。
/// </summary>
public class LayoutAnalyzerTests
{
    private static HmiControl Ctrl(string name, double x, double y, double w = 80, double h = 40,
        string? text = null, string? bg = null, string? fg = null) => new()
    {
        Name = name, X = x, Y = y, Width = w, Height = h,
        Text = text, BgColor = bg, TextColor = fg
    };

    [Fact]
    public void Analyze_EmptyScreen_ReturnsFullScoreWithInfo()
    {
        var result = LayoutAnalyzer.Analyze(new HmiScreen());
        Assert.Equal(100, result.Score);
        Assert.Contains(result.Issues, i => i.Severity == LayoutIssueSeverity.Info);
    }

    [Fact]
    public void Analyze_WellAlignedScreen_NoMarginIssues()
    {
        var screen = new HmiScreen { Width = 800, Height = 480 };
        screen.Controls.Add(Ctrl("A", 20, 20));
        screen.Controls.Add(Ctrl("B", 120, 20));
        screen.Controls.Add(Ctrl("C", 220, 20));

        var result = LayoutAnalyzer.Analyze(screen);
        Assert.DoesNotContain(result.Issues, i => i.Message.Contains("安全边距"));
    }

    [Fact]
    public void Analyze_CtrlTooCloseToEdge_ReportsMargin()
    {
        var screen = new HmiScreen { Width = 800, Height = 480 };
        screen.Controls.Add(Ctrl("A", 2, 20)); // X=2 < 8

        var result = LayoutAnalyzer.Analyze(screen);
        Assert.Contains(result.Issues, i => i.Message.Contains("安全边距") && i.AutoFix);
    }

    [Fact]
    public void Analyze_MisalignedControls_ReportsAlignment()
    {
        var screen = new HmiScreen { Width = 800, Height = 480 };
        screen.Controls.Add(Ctrl("A", 20, 20));
        screen.Controls.Add(Ctrl("B", 120, 20));
        screen.Controls.Add(Ctrl("C", 220, 26)); // Y 偏差 6px > 3px

        var result = LayoutAnalyzer.Analyze(screen);
        Assert.Contains(result.Issues, i => i.Message.Contains("未对齐") && i.AutoFix);
    }

    [Fact]
    public void Analyze_AlignedWithinTolerance_NoAlignmentIssue()
    {
        var screen = new HmiScreen { Width = 800, Height = 480 };
        screen.Controls.Add(Ctrl("A", 20, 20));
        screen.Controls.Add(Ctrl("B", 120, 21)); // 偏差 1px, 容忍

        var result = LayoutAnalyzer.Analyze(screen);
        Assert.DoesNotContain(result.Issues, i => i.Message.Contains("未对齐"));
    }

    [Fact]
    public void Analyze_UnevenSpacing_ReportsInfo()
    {
        var screen = new HmiScreen { Width = 800, Height = 480 };
        screen.Controls.Add(Ctrl("A", 20, 20));
        screen.Controls.Add(Ctrl("B", 220, 20)); // 间距 120
        screen.Controls.Add(Ctrl("C", 280, 20)); // 间距 40, 偏差大
        screen.Controls.Add(Ctrl("D", 340, 20)); // 间距 40

        var result = LayoutAnalyzer.Analyze(screen);
        Assert.Contains(result.Issues, i => i.Message.Contains("间距") && i.AutoFix);
    }

    [Fact]
    public void Analyze_LowContrast_ReportsWarning()
    {
        var screen = new HmiScreen { Width = 800, Height = 480 };
        // 白底浅灰字, 对比度低
        screen.Controls.Add(Ctrl("T", 20, 20, 200, 60, text: "标题文本", bg: "#FFFFFF", fg: "#BBBBBB"));

        var result = LayoutAnalyzer.Analyze(screen);
        Assert.Contains(result.Issues, i => i.Message.Contains("对比度") && !i.AutoFix);
    }

    [Fact]
    public void Analyze_HighContrast_NoIssue()
    {
        var screen = new HmiScreen { Width = 800, Height = 480 };
        // 白底黑字, 对比度 21:1
        screen.Controls.Add(Ctrl("T", 20, 20, 200, 60, text: "标题文本", bg: "#FFFFFF", fg: "#000000"));

        var result = LayoutAnalyzer.Analyze(screen);
        Assert.DoesNotContain(result.Issues, i => i.Message.Contains("对比度"));
    }

    [Fact]
    public void Analyze_SingleSmallControl_NoHierarchyInfo()
    {
        var screen = new HmiScreen { Width = 800, Height = 480 };
        screen.Controls.Add(Ctrl("S", 20, 20, 50, 20));

        var result = LayoutAnalyzer.Analyze(screen);
        // 面积比 100/384000 < 0.08 → 有层次建议
        Assert.Contains(result.Issues, i => i.Message.Contains("层次"));
    }

    [Theory]
    [InlineData("#FFFFFF", 1.0)]
    [InlineData("#000000", 0.0)]
    [InlineData("#808080", 0.2158)]
    public void RelativeLuminance_KnownValues(string hex, double expected)
    {
        var parsed = LayoutAnalyzer.ParseHexColor(hex);
        Assert.NotNull(parsed);
        var luminance = LayoutAnalyzer.RelativeLuminance(parsed!.Value);
        Assert.Equal(expected, luminance, 3);
    }

    [Fact]
    public void ParseHexColor_Invalid_ReturnsNull()
    {
        Assert.Null(LayoutAnalyzer.ParseHexColor(null));
        Assert.Null(LayoutAnalyzer.ParseHexColor(""));
        Assert.Null(LayoutAnalyzer.ParseHexColor("red"));
        Assert.Null(LayoutAnalyzer.ParseHexColor("#GGGGGG"));
    }

    [Fact]
    public void ParseHexColor_ShortForm_Expands()
    {
        var parsed = LayoutAnalyzer.ParseHexColor("#fff");
        Assert.NotNull(parsed);
        Assert.Equal(1.0, parsed.Value.R, 3);
    }

    [Fact]
    public void ContrastRatio_BlackOnWhite_Is21()
    {
        var fg = LayoutAnalyzer.ParseHexColor("#000000")!.Value;
        var bg = LayoutAnalyzer.ParseHexColor("#FFFFFF")!.Value;
        Assert.Equal(21.0, LayoutAnalyzer.ContrastRatio(fg, bg), 1);
    }

    [Fact]
    public void Analyze_ScoresBelow100_WhenIssuesExist()
    {
        var screen = new HmiScreen { Width = 800, Height = 480 };
        screen.Controls.Add(Ctrl("A", 2, 2)); // 双重边距问题
        screen.Controls.Add(Ctrl("B", 20, 20, 60, 30, text: "低对比", bg: "#FFFFFF", fg: "#CCCCCC"));

        var result = LayoutAnalyzer.Analyze(screen);
        Assert.True(result.Score < 100);
        Assert.True(result.Score >= 0);
    }
}
