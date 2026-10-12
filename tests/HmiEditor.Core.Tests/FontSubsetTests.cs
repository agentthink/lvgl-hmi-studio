using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// I12: 字体子集化分析测试 —— 字符扫描、大小估算、字符集导出。
/// </summary>
public class FontSubsetTests
{
    private static HmiProject BuildProjectWithText()
    {
        var project = new HmiProject();
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1", Type = HmiControlType.Label, X = 0, Y = 0,
            Text = "温度 压力 正常"
        });
        screen.Controls.Add(new HmiControl
        {
            Id = "c2", Type = HmiControlType.Button, X = 0, Y = 0,
            Text = "START 启动"
        });
        project.Screens.Add(screen);
        return project;
    }

    // ── 字符扫描 ──

    [Fact]
    public void ScanProject_CollectsAllTextChars()
    {
        var project = BuildProjectWithText();
        var report = FontSubsetAnalyzer.ScanProject(project);

        // "温度 压力 正常" + "START 启动" → 温/度/空格/压/力/正/常/S/T/A/R/启/动
        // 唯一 13: CJK 8(温度压力正常启动) + ASCII 5(空格 S T A R)
        Assert.Equal(13, report.UniqueChars);
        Assert.Equal(5, report.AsciiChars);
        Assert.Equal(8, report.CjkChars);
    }

    [Fact]
    public void ScanProject_EmptyProject_ZeroChars()
    {
        var project = new HmiProject();
        var report = FontSubsetAnalyzer.ScanProject(project);
        Assert.Equal(0, report.UniqueChars);
        Assert.Equal(0, report.TotalChars);
    }

    [Fact]
    public void ScanProject_DeduplicatesRepeatedChars()
    {
        var project = new HmiProject();
        var screen = new HmiScreen();
        screen.Controls.Add(new HmiControl { Id = "a", Text = "AAABBB" });
        screen.Controls.Add(new HmiControl { Id = "b", Text = "ABC" });
        project.Screens.Add(screen);

        var report = FontSubsetAnalyzer.ScanProject(project);
        Assert.Equal(3, report.UniqueChars);  // A B C
        Assert.Equal(9, report.TotalChars);   // 6 + 3
    }

    [Fact]
    public void ScanProject_IncludesVariablesAndUdt()
    {
        var project = new HmiProject();
        project.VariableManager.Variables.Add(new Variable
        {
            Name = "Motor_Speed",
            Unit = "rpm",
            SimulatedValue = "1500"
        });
        var def = project.VariableManager.RegisterUdt("泵组");
        def.AddField("流量", "Float32");

        var report = FontSubsetAnalyzer.ScanProject(project);
        Assert.Contains('泵', report.Characters);
        Assert.Contains('流', report.Characters);
        Assert.Contains('量', report.Characters);
        Assert.Contains('M', report.Characters);
    }

    [Theory]
    [InlineData('中', true)]
    [InlineData('，', true)]    // 全角逗号 U+FF0C
    [InlineData('。', true)]    // 中文句号 U+3002
    [InlineData('A', false)]
    [InlineData('0', false)]
    [InlineData('é', false)]   // 拉丁扩展
    public void IsCjk_Classification(char c, bool expected)
        => Assert.Equal(expected, FontSubsetAnalyzer.IsCjk(c));

    // ── 大小估算 ──

    [Fact]
    public void EstimateSubsetBytes_CjkLargerThanAscii()
    {
        var asciiReport = new FontSubsetReport
        {
            UniqueChars = 100,
            Characters = Enumerable.Range(32, 100).Select(i => (char)i).ToList()
        };
        var cjkReport = new FontSubsetReport
        {
            UniqueChars = 100,
            Characters = Enumerable.Range(0, 100).Select(i => (char)('\u4E00' + i)).ToList()
        };

        var asciiBytes = FontSubsetAnalyzer.EstimateSubsetBytes(asciiReport, 20);
        var cjkBytes = FontSubsetAnalyzer.EstimateSubsetBytes(cjkReport, 20);

        // CJK 全角位图 ≈ (20×20) vs ASCII ≈ (11×20) → CJK 明显更大
        Assert.True(cjkBytes > asciiBytes * 1.5);
    }

    [Fact]
    public void EstimateSubsetBytes_ZeroChars_Zero()
    {
        var report = new FontSubsetReport();
        Assert.Equal(0, FontSubsetAnalyzer.EstimateSubsetBytes(report, 20));
    }

    [Fact]
    public void EstimateSubsetBytes_LargerFont_LargerSize()
    {
        var report = new FontSubsetReport
        {
            UniqueChars = 50,
            Characters = Enumerable.Range(32, 50).Select(i => (char)i).ToList()
        };
        var small = FontSubsetAnalyzer.EstimateSubsetBytes(report, 12);
        var large = FontSubsetAnalyzer.EstimateSubsetBytes(report, 32);
        Assert.True(large > small * 3);
    }

    [Fact]
    public void Analyze_TypicalHmi_SubsetFarSmallerThanFullCjk()
    {
        var project = BuildProjectWithText();
        var report = FontSubsetAnalyzer.Analyze(project);

        Assert.True(report.EstimatedSubsetBytes > 0);
        Assert.True(report.EstimatedSubsetBytes < FontSubsetAnalyzer.TypicalFullCjkFontBytes / 100);
    }

    // ── 导出 ──

    [Fact]
    public void ExportCharsetFile_WritesOneCharPerLine()
    {
        var project = BuildProjectWithText();
        var report = FontSubsetAnalyzer.ScanProject(project);
        var dir = TestTempDir.NewDir("hmi-font");
        try
        {
            var path = Path.Combine(dir, "charset.txt");
            FontSubsetAnalyzer.ExportCharsetFile(report, path);

            var lines = File.ReadAllLines(path);
            Assert.Equal(report.UniqueChars, lines.Length);
            Assert.Contains("温", lines);
            Assert.Contains("S", lines);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void ExportCharsetFile_EmptyReport_WritesEmpty()
    {
        var report = new FontSubsetReport();
        var dir = TestTempDir.NewDir("hmi-font");
        try
        {
            var path = Path.Combine(dir, "charset.txt");
            FontSubsetAnalyzer.ExportCharsetFile(report, path);
            Assert.Empty(File.ReadAllLines(path));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    [Fact]
    public void BuildLvFontConvCommand_ReferencesInputs()
    {
        var cmd = FontSubsetAnalyzer.BuildLvFontConvCommand(@"C:\fonts\wqy.ttf", "subset_font.c", 20, withCjk: true);
        Assert.Contains("lv_font_conv", cmd);
        Assert.Contains("wqy.ttf", cmd);
        Assert.Contains("--size 20", cmd);
        Assert.Contains("--symbols charset.txt", cmd);
        Assert.Contains("subset_font.c", cmd);
    }
}
