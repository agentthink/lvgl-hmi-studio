using System;
using System.Collections.Generic;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报表打包的纯事**（阶段 179）：文件名清洗与索引文本。时间由调用方给 ⇒ 两件都确定、可判。
/// </summary>
public class ReportBundleTests
{
    private static readonly DateTimeOffset T = new(new DateTime(2026, 10, 1, 12, 34, 56, DateTimeKind.Utc));

    [Fact]
    public void FileName_IsPrefixedByTimestamp()
    {
        Assert.Equal("audit-20261001-123456.csv", ReportBundle.FileName("audit", T, ".csv"));
        Assert.Equal("history-20261001-123456.xlsx", ReportBundle.FileName("history", T, "xlsx"));   // 缺前导点也认
    }

    /// <summary>路径非法字符与空白清成 <c>-</c>；全是非法字符时回落到 <c>report</c>（中文保留：它只是文件名）。</summary>
    [Fact]
    public void FileName_SanitizesUnsafeCharacters()
    {
        Assert.Equal("a-b-c-20261001-123456.csv", ReportBundle.FileName("a/b\\c", T, ".csv"));
        Assert.Equal("审计报表-20261001-123456.pdf", ReportBundle.FileName("审计报表", T, ".pdf"));
        Assert.Equal("report-20261001-123456.csv", ReportBundle.FileName("  ", T, ".csv"));
    }

    [Fact]
    public void IndexText_ListsFilesAndSkippedReasons_Deterministically()
    {
        var written = new List<(string, int)> { ("a.csv", 3), ("a.xlsx", 3) };
        var skipped = new List<string> { ".pdf: 没有可嵌入的字体" };

        var text = ReportBundle.IndexText(written, skipped);

        Assert.Equal("file\trows\na.csv\t3\na.xlsx\t3\n\nskipped\n.pdf: 没有可嵌入的字体\n", text);
        // 确定性：同样输入两次逐字节相同
        Assert.Equal(text, ReportBundle.IndexText(written, skipped));

        // 没有跳过项时**不**留一个空的 skipped 段
        Assert.Equal("file\trows\na.csv\t3\n", ReportBundle.IndexText(new List<(string, int)> { ("a.csv", 3) }, Array.Empty<string>()));
    }
}
