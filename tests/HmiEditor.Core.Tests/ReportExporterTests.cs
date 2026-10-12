using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报表导出器判据**（阶段 172，报表能力 MVP）。
///
/// <para>产物必须**确定**（逐字节可判）：本类因此能用"两次调用逐字节相同"这种最便宜的判法，
/// 而导出器也确实**不读时钟、不读环境**（生成时间由调用方传入）。</para>
/// </summary>
public class ReportExporterTests
{
    private static ReportTable Table(params string[][] rows) => new()
    {
        Title = "审计报表",
        Columns = new List<ReportColumn>
        {
            new("c1", "时间"),
            new("c2", "详情"),
        },
        Rows = rows.Select(r => (IReadOnlyList<string>)r).ToList(),
    };

    /// <summary>RFC4180 转义：含 <c>,</c>/<c>"</c>/CR/LF 的单元格加引号、内部引号翻倍。</summary>
    [Fact]
    public void ToCsv_EscapesRfc4180()
    {
        var csv = ReportExporter.ToCsv(Table(new[] { "2026-10-01 00:00:00Z", "a,b" },
                                             new[] { "2026-10-01 00:00:01Z", "say \"hi\"" },
                                             new[] { "2026-10-01 00:00:02Z", "line1\nline2" }));

        Assert.Contains("\"a,b\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"say \"\"hi\"\"\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"line1\nline2\"", csv, StringComparison.Ordinal);

        // **正控**：不含特殊字符时**不许**加引号 —— 否则"全都加引号"也能让"转义了"看起来成立
        Assert.Contains("2026-10-01 00:00:00Z,a", csv.Replace("\"a,b\"", "a,b"), StringComparison.Ordinal);
        Assert.DoesNotContain("\"2026-10-01", csv, StringComparison.Ordinal);
    }

    /// <summary>行尾是 CRLF（RFC4180），**不是**随平台的 <c>AppendLine()</c>。</summary>
    [Fact]
    public void ToCsv_UsesCrlfAndEndsEveryRow()
    {
        var csv = ReportExporter.ToCsv(Table(new[] { "t", "d" }));
        Assert.EndsWith(ReportExporter.Crlf, csv, StringComparison.Ordinal);
        Assert.Equal(2, csv.Split(ReportExporter.Crlf, StringSplitOptions.RemoveEmptyEntries).Length); // 表头 + 1 行
        Assert.DoesNotContain("\n", csv.Replace("\r\n", ""), StringComparison.Ordinal);                // 没有裸 LF
    }

    /// <summary>字节形态：UTF-8 ＋ BOM（Excel 打开带 CJK 的 CSV 需要它）。</summary>
    [Fact]
    public void ToCsvBytes_PrependsUtf8Bom()
    {
        var table = Table(new[] { "t", "中文" });
        var bytes = ReportExporter.ToCsvBytes(table);

        Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "CSV 字节流没有 UTF-8 BOM —— Excel 会把 CJK 解成乱码");
        Assert.Equal(Encoding.UTF8.GetBytes(ReportExporter.ToCsv(table)), bytes.Skip(3).ToArray());
    }

    /// <summary>确定性：同一张表两次导出**逐字节相同**（时间由调用方给 ⇒ 判据成立）。</summary>
    [Fact]
    public void ToCsv_IsDeterministic()
    {
        var table = Table(new[] { "t1", "d1" }, new[] { "t2", "d2" });
        Assert.Equal(ReportExporter.ToCsvBytes(table), ReportExporter.ToCsvBytes(table));
    }

    /// <summary>
    /// HTML **自包含**：零外链（离线可打开/打印）。**正控**：内联 <c>&lt;style&gt;</c> 必须在 ——
    /// 否则"把样式全删了"也能满足"没有外链"。
    /// </summary>
    [Fact]
    public void ToHtml_IsSelfContained()
    {
        var html = ReportExporter.ToHtml(Table(new[] { "t", "d" }), "2026-10-01 00:00:00Z", "生成时间", "zh-CN");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" src=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" href=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("<style>", html, StringComparison.Ordinal);          // 正控：内联样式在
        Assert.Contains("charset=\"utf-8\"", html, StringComparison.Ordinal);
        Assert.Contains("lang=\"zh-CN\"", html, StringComparison.Ordinal);
        Assert.Contains("生成时间: 2026-10-01 00:00:00Z", html, StringComparison.Ordinal);
    }

    /// <summary>HTML 文本转义：单元格里的 <c>&lt;b&gt;</c> 不许变成标签（否则报表内容能被"注入"排版）。</summary>
    [Fact]
    public void ToHtml_EscapesCellText()
    {
        var html = ReportExporter.ToHtml(Table(new[] { "t", "<b>x</b> & \"q\"" }), "now", "生成时间", "zh-CN");
        Assert.Contains("&lt;b&gt;x&lt;/b&gt; &amp; &quot;q&quot;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>x</b>", html, StringComparison.Ordinal);
    }
}
