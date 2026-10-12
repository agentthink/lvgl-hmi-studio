using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;
using SkiaSharp;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **PDF 写出器判据**（阶段 173）—— 走 SkiaSharp 的 PDF 后端，所以判据看的是"**结构 ＋ 确定性 ＋
/// 文本真的进去了**"，而不是逐字节对拍一个常量（字体与 Skia 版本一变，字节就变）。
///
/// <para>字体**用仓库自带的 CJK 字体文件**（<c>firmware/t113/…/fangsong.ttf</c>）⇒ 判据与机器上装了什么字体
/// 无关（这是"可判"的前提；系统兜底那条由 <see cref="PdfReportWriter.ResolveTypeface"/> 负责，
/// 它的失败路径在 UI 里有明确提示）。</para>
/// </summary>
public class PdfReportWriterTests
{
    private static readonly Lazy<SKTypeface> RepoFont = new(() => SKTypeface.FromFile(Path.Combine(
        SourceScanner.RepoRoot, "firmware", "t113", "tina", "package", "hmi-runtime", "files", "fangsong.ttf"))
        ?? throw new InvalidOperationException("仓库自带的 CJK 字体打不开 —— PDF 判据的前提不成立"));

    private static readonly DateTimeOffset FixedCreation = new(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

    private static ReportTable Table(int rows)
    {
        var data = Enumerable.Range(0, rows)
            .Select(i => (IReadOnlyList<string>)new[] { $"2026-10-01 00:00:{i % 60:00}Z", $"用户{i}", "登录", "成功", "127.0.0.1", $"详情 {i}" })
            .ToList();
        return new ReportTable
        {
            Title = "审计报表",
            Columns = new List<ReportColumn>
            {
                new("user.mgr.audit.time", "时间"), new("user.mgr.audit.user", "用户"),
                new("user.mgr.audit.action", "操作"), new("user.mgr.audit.result", "结果"),
                new("user.mgr.audit.source", "来源"), new("user.mgr.audit.detail", "详情"),
            },
            Rows = data,
        };
    }

    private static byte[] Pdf(ReportTable table) =>
        PdfReportWriter.Write(table, "2026-10-01 00:00:00Z", "生成时间", RepoFont.Value, FixedCreation);

    private static string AsLatin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    /// <summary>前提自检：仓库字体真的带中文glyph（否则"PDF 能显示中文"这条判据失去意义）。</summary>
    [Fact]
    public void RepoFont_ReallyHasCjkGlyphs()
    {
        var tf = RepoFont.Value;
        foreach (var ch in new[] { '审', '计', '报', '表' })
            Assert.True(tf.GetGlyph(ch) > 0, $"仓库字体缺少字形：{ch}（PDF 判据的前提不成立）");
    }

    [Fact]
    public void Write_ProducesPdfStructure()
    {
        var pdf = Pdf(Table(3));
        var text = AsLatin1(pdf);

        Assert.StartsWith("%PDF-1.", text, StringComparison.Ordinal);
        Assert.Contains("%%EOF", text, StringComparison.Ordinal);
        Assert.Contains("/Type /Catalog", text, StringComparison.Ordinal);
        Assert.Contains("/Type /Pages", text, StringComparison.Ordinal);
        Assert.Contains("/ToUnicode", text, StringComparison.Ordinal);        // 文本可提取（不是纯图形）
        Assert.Contains("/FontDescriptor", text, StringComparison.Ordinal);   // 字体描述符在
        // ⚠ 刻意**不**断言 raw 文本里有 `/FontFile2`：Skia 把字体字典/字体流放进**压缩对象流**，
        //    raw 扫描看不到它（实测 raw 里只有 `/Subtype` / `/ToUnicode` / `/FontDescriptor`）。
        //    "字体真的被嵌进去了"改由下面的**体积下限**判据钉（实测：纯拉丁 14.0 KB、4 个汉字 18.1 KB、
        //    60 个汉字 19.5 KB —— 小文本却有十几 KB ⇒ 那只能是字体载荷）。
    }

    /// <summary>
    /// **字体载荷确实在产物里**（体积下限）：一行小文本却有十几 KB ⇒ 只能是嵌入的字体。
    /// 实测基线（阶段 173）：纯拉丁 14,006 B · 4 个汉字 18,144 B · 60 个汉字 19,473 B。
    /// ⚠ 只钉**下限**，不钉"子集随字形数线性增长" —— 实测 4→60 个汉字只从 18.1 KB 到 19.5 KB
    /// （Skia 的子集粒度不是逐字形线性的），拿增长当判据会红得莫名其妙。
    /// </summary>
    [Fact]
    public void Write_ContainsEmbeddedFontPayload()
    {
        Assert.True(Pdf(Table(1)).Length > 8_000,
            $"PDF 只有 {Pdf(Table(1)).Length} B —— 没有嵌入字体载荷（中文报表会是豆腐块）");
    }

    /// <summary>
    /// **确定性**：固定 creation ＋ 同一 typeface ⇒ 两次导出逐字节相同。
    ///
    /// <para>⚠ 只比两次调用**抓不到**"实现里改读时钟"这种注入（同一秒内两次调用会得到同样的字节，
    /// 阶段 173 的负控 nc5 就是这么漏的）⇒ 再加一条**结构断言**：元数据里的 creation 必须是**传进去的那个值**。
    /// 实测 Skia 写成 <c>CreationDate (D:20261001000000+08'00')</c>（本地偏移，故只钉日期时间部分）。</para>
    /// </summary>
    [Fact]
    public void Write_IsDeterministic_WithPinnedCreation()
    {
        var pdf = Pdf(Table(5));
        Assert.Equal(pdf, Pdf(Table(5)));

        Assert.Contains("CreationDate (D:20261001000000", AsLatin1(pdf), StringComparison.Ordinal);
    }

    /// <summary>**文本真的进了产物**：只改一个单元格，字节必须不同（否则"导出"只是画了个空表）。</summary>
    [Fact]
    public void Write_ChangesWhenCellTextChanges()
    {
        var a = Table(1);
        var b = new ReportTable { Title = a.Title, Columns = a.Columns, Rows = new[] { (IReadOnlyList<string>)new[] { "x", "y", "z", "w", "v", "详情甲" } } };
        var c = new ReportTable { Title = a.Title, Columns = a.Columns, Rows = new[] { (IReadOnlyList<string>)new[] { "x", "y", "z", "w", "v", "详情乙" } } };

        Assert.NotEqual(Pdf(b), Pdf(c));
    }

    /// <summary>分页：行数超过每页容量时页数按 <see cref="PdfReportWriter.RowsPerPage"/> 增长。</summary>
    [Fact]
    public void Write_PaginatesWhenRowsExceedPageCapacity()
    {
        var perPage = PdfReportWriter.RowsPerPage;
        Assert.True(perPage >= 10, $"每页行数太少（{perPage}），分页判据没意义");

        static int Pages(byte[] pdf) =>
            Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page[^s]").Count;

        var one = Pages(Pdf(Table(perPage)));
        var two = Pages(Pdf(Table(perPage + 1)));

        Assert.Equal(1, one);
        Assert.Equal(2, two);
    }

    /// <summary>Emoji/控制字符之类**字体里没有**的字符不许把写出器弄崩（报表可能带用户输入）。</summary>
    [Fact]
    public void Write_SurvivesCharactersMissingFromTheFont()
    {
        var table = new ReportTable
        {
            Title = "怪字符",
            Columns = new List<ReportColumn> { new("c", "值") },
            Rows = new[] { (IReadOnlyList<string>)new[] { "🛠\u0001ok" } },
        };

        var pdf = Pdf(table);
        Assert.StartsWith("%PDF-1.", AsLatin1(pdf), StringComparison.Ordinal);
    }
}
