using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **XLSX 写出器判据**（阶段 173）—— 手写 SpreadsheetML，所以判据要**自己把 zip 拆开看**：
/// 部件齐不齐、XML 合不合法、值与列序对不对、数值单元格是不是数值、以及**产物确定**。
///
/// <para>⚠ 诚实边界：本类**没有**用第三方读回器（不引依赖）⇒ 它证明的是"结构合法 ＋ 内容正确"，
/// 不等于"Excel 一定能打开"；真正的互操作性证据见专题 §9.3 里记的那次人工打开。</para>
/// </summary>
public class XlsxReportWriterTests
{
    private static ReportTable Table(params string[][] rows) => new()
    {
        Title = "审计报表",
        Columns = new List<ReportColumn> { new("t", "时间"), new("n", "次数"), new("d", "详情") },
        Rows = rows.Select(r => (IReadOnlyList<string>)r).ToList(),
    };

    private static Dictionary<string, string> Parts(byte[] xlsx)
    {
        using var ms = new MemoryStream(xlsx);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(
            e => e.FullName,
            e => { using var s = e.Open(); using var r = new StreamReader(s); return r.ReadToEnd(); },
            StringComparer.Ordinal);
    }

    /// <summary>① 部件齐全且都是**合法 XML**（xlsx = zip ＋ 若干 XML）。</summary>
    [Fact]
    public void Write_ProducesRequiredParts_AllWellFormedXml()
    {
        var parts = Parts(XlsxReportWriter.Write(Table(new[] { "a", "1", "b" })));

        foreach (var required in new[]
                 {
                     "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml",
                     "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml",
                 })
            Assert.True(parts.ContainsKey(required), $"缺部件：{required}");

        foreach (var (name, xml) in parts)
        {
            var ex = Record.Exception(() => XDocument.Parse(xml));
            Assert.True(ex is null, $"部件 {name} 不是合法 XML：{ex?.Message}");
        }

        // 工作簿里得有我们的 sheet 名与 sheet1 的关系
        Assert.Contains("sheet name=\"Report\"", parts["xl/workbook.xml"], StringComparison.Ordinal);
        Assert.Contains("worksheets/sheet1.xml", parts["xl/_rels/workbook.xml.rels"], StringComparison.Ordinal);
    }

    /// <summary>② 表头与数据都进了 sheet，**顺序**与列序一致（按单元格引用逐格核对）。</summary>
    [Fact]
    public void Write_EmitsHeaderAndRowsInColumnOrder()
    {
        var parts = Parts(XlsxReportWriter.Write(Table(new[] { "2026-10-01 00:00:00Z", "3", "登录" })));
        var sheet = parts["xl/worksheets/sheet1.xml"];

        Assert.Contains("<c r=\"A1\" s=\"1\" t=\"inlineStr\"><is><t>时间</t></is></c>", sheet, StringComparison.Ordinal);
        Assert.Contains("<c r=\"B1\" s=\"1\" t=\"inlineStr\"><is><t>次数</t></is></c>", sheet, StringComparison.Ordinal);
        Assert.Contains("<c r=\"C1\" s=\"1\" t=\"inlineStr\"><is><t>详情</t></is></c>", sheet, StringComparison.Ordinal);

        Assert.Contains("<c r=\"A2\" t=\"inlineStr\"><is><t xml:space=\"preserve\">2026-10-01 00:00:00Z</t></is></c>", sheet, StringComparison.Ordinal);
        Assert.Contains("<c r=\"B2\" t=\"n\"><v>3</v></c>", sheet, StringComparison.Ordinal);                    // 数值 → 数值单元格
        Assert.Contains("<c r=\"C2\" t=\"inlineStr\"><is><t xml:space=\"preserve\">登录</t></is></c>", sheet, StringComparison.Ordinal);
    }

    /// <summary>③ 文本转义 ＋ 非法控制字符剔除（否则 Excel 报"文件损坏"）。</summary>
    [Fact]
    public void Write_EscapesXmlAndDropsIllegalControlChars()
    {
        var parts = Parts(XlsxReportWriter.Write(Table(new[] { "<a & b>", "1", "x\u0001y" })));
        var sheet = parts["xl/worksheets/sheet1.xml"];

        Assert.Contains("&lt;a &amp; b&gt;", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("x\u0001y", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("<a & b>", sheet, StringComparison.Ordinal);
    }

    /// <summary>④ 数值判定口径（**走公开的 <c>Write</c>**，不测内部谓词）：只有"纯数字形态"才当数值。</summary>
    [Fact]
    public void NumericDetection_IsNarrow()
    {
        var parts = Parts(XlsxReportWriter.Write(Table(
            new[] { "3", "-12.5", "x" },
            new[] { "007", "1.2.3", "y" })));
        var sheet = parts["xl/worksheets/sheet1.xml"];

        Assert.Contains("<c r=\"A2\" t=\"n\"><v>3</v></c>", sheet, StringComparison.Ordinal);
        Assert.Contains("<c r=\"B2\" t=\"n\"><v>-12.5</v></c>", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("<c r=\"A3\" t=\"n\"", sheet, StringComparison.Ordinal);   // 007 是编号
        Assert.Contains("<c r=\"A3\" t=\"inlineStr\"><is><t xml:space=\"preserve\">007</t></is></c>", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("<c r=\"B3\" t=\"n\"", sheet, StringComparison.Ordinal);   // 1.2.3 不是数
    }

    /// <summary>
    /// ⑤ **确定性**：同一张表两次导出逐字节相同（zip 条目顺序固定），**且**每个 zip 条目的时间戳
    /// 都不是"现在"——只比两次调用**抓不到**"实现里改读时钟"这种注入（同一秒内两次调用字节相同，
    /// 阶段 173 的负控 nc2 就是这么漏的）⇒ 再加这条结构断言。
    /// </summary>
    [Fact]
    public void Write_IsDeterministic()
    {
        var table = Table(new[] { "a", "1", "b" }, new[] { "c", "2", "d" });
        Assert.Equal(XlsxReportWriter.Write(table), XlsxReportWriter.Write(table));

        var now = DateTimeOffset.UtcNow;
        using var ms = new MemoryStream(XlsxReportWriter.Write(table));
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
            Assert.True(Math.Abs((entry.LastWriteTime.UtcDateTime - now.UtcDateTime).TotalHours) > 1,
                $"zip 条目 {entry.FullName} 的时间戳是「现在」（{entry.LastWriteTime}）—— 产物因此不确定");
    }

    /// <summary>⑥ 列引用换算越过 <c>Z</c>（**走公开的 <c>Write</c>**：27 列的表里最后一列应当是 <c>AA</c>）。</summary>
    [Fact]
    public void ColumnRef_GoesBeyondZ()
    {
        var columns = Enumerable.Range(0, 27).Select(i => new ReportColumn($"c{i}", $"C{i}")).ToList();
        var row = Enumerable.Range(0, 27).Select(i => i.ToString()).ToArray();
        var table = new ReportTable { Title = "wide", Columns = columns, Rows = new[] { (IReadOnlyList<string>)row } };

        var sheet = Parts(XlsxReportWriter.Write(table))["xl/worksheets/sheet1.xml"];

        Assert.Contains("<c r=\"Z1\" s=\"1\" t=\"inlineStr\"><is><t>C25</t></is></c>", sheet, StringComparison.Ordinal);
        Assert.Contains("<c r=\"AA1\" s=\"1\" t=\"inlineStr\"><is><t>C26</t></is></c>", sheet, StringComparison.Ordinal);
        Assert.Contains("<dimension ref=\"A1:AA2\"/>", sheet, StringComparison.Ordinal);
    }
}
