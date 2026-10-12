using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// **XLSX 写出器**（阶段 173，报表能力）—— **手写 SpreadsheetML、零新依赖**
/// （只用 BCL 的 <see cref="ZipArchive"/>）。
///
/// <para><b>为什么不用 ClosedXML / EPPlus</b>（选型见
/// <c>docs/2026-10-01-reporting-capability-mvp.md</c> §9）：报表只需要"一张表 ＋ 表头格式化"，
/// 而 xlsx 本身就是 **一个 zip ＋ 几份 XML** ⇒ 手写的代价远小于引入依赖（后者还要带上
/// DocumentFormat.OpenXml 与 SixLabors 的许可问题）。副产品是**产物确定**：
/// zip 条目时间戳写死、条目顺序固定 ⇒ 同一张表两次导出**逐字节相同**（判据钉着）。</para>
///
/// <para><b>范围（刻意不做）</b>：公式、多 sheet、图表、单元格样式矩阵、日期格式 —— 报表 MVP 用不上；
/// 数值单元格会**自动识别**（让 Excel 能求和），其余一律按文本写（<c>inlineStr</c>，不建 sharedStrings）。</para>
/// </summary>
public static class XlsxReportWriter
{
    /// <summary>zip 内所有条目的固定时间戳 —— 产物的确定性靠它（否则两次导出字节不同）。</summary>
    private static readonly DateTimeOffset FixedTimestamp =
        new(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    /// <summary>纯数字形态（整/小数、可带负号、不许有前导零）—— 只有它才写成数值单元格。</summary>
    private static readonly Regex NumericCell = new(@"^-?(0|[1-9]\d*)(\.\d+)?$", RegexOptions.Compiled);

    /// <summary>把一张报表写成 xlsx 字节。</summary>
    public static byte[] Write(ReportTable table, string sheetName = "Report")
    {
        ArgumentNullException.ThrowIfNull(table);

        var sheetXml = BuildSheetXml(table);
        var workbookXml = BuildWorkbookXml(Xml(sheetName));

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "[Content_Types].xml", ContentTypes);
            AddEntry(zip, "_rels/.rels", RootRels);
            AddEntry(zip, "xl/workbook.xml", workbookXml);
            AddEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
            AddEntry(zip, "xl/styles.xml", Styles);
            AddEntry(zip, "xl/worksheets/sheet1.xml", sheetXml);
        }
        return ms.ToArray();
    }

    private static void AddEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = FixedTimestamp;                  // ⚠ 确定性：不许用"现在"
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    // ── 固定部件 ──

    private const string ContentTypes =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
        "</Types>";

    private const string RootRels =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private const string WorkbookRels =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "</Relationships>";

    /// <summary>两种格式：0 = 默认、1 = 表头（加粗 ＋ 浅灰底）。够报表用，不铺开样式矩阵。</summary>
    private const string Styles =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
        "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
        "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
        "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill>" +
        "<fill><patternFill patternType=\"gray125\"/></fill>" +
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF0F0F0\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
        "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
        "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
        "<cellXfs count=\"2\">" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
        "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>" +
        "</cellXfs>" +
        "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
        "</styleSheet>";

    private static string BuildWorkbookXml(string escapedSheetName) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
        $"<sheets><sheet name=\"{escapedSheetName}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
        "</workbook>";

    private static string BuildSheetXml(ReportTable table)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

        var lastColumn = ColumnRef(Math.Max(table.Columns.Count, 1) - 1);
        var lastRow = Math.Max(table.Rows.Count + 1, 1);
        sb.Append($"<dimension ref=\"A1:{lastColumn}{lastRow}\"/>");

        // 冻结首行 ＋ 自动筛选（报表的"能用"下限）
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\">");
        sb.Append("<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>");
        sb.Append("</sheetView></sheetViews>");
        sb.Append($"<sheetFormatPr defaultRowHeight=\"15\"/>");
        sb.Append("<cols>");
        for (var c = 0; c < table.Columns.Count; c++)
            sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{ColumnWidth(table, c)}\" customWidth=\"1\"/>");
        sb.Append("</cols>");
        sb.Append($"<sheetData>");

        // 表头（样式 1 = 加粗/浅灰底）
        sb.Append("<row r=\"1\">");
        for (var c = 0; c < table.Columns.Count; c++)
            sb.Append($"<c r=\"{ColumnRef(c)}1\" s=\"1\" t=\"inlineStr\"><is><t>{Xml(table.Columns[c].DisplayName)}</t></is></c>");
        sb.Append("</row>");

        // 数据行
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            sb.Append($"<row r=\"{r + 2}\">");
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var value = c < row.Count ? row[c] ?? string.Empty : string.Empty;
                var reference = $"{ColumnRef(c)}{r + 2}";
                sb.Append(NumericCell.IsMatch(value)
                    ? $"<c r=\"{reference}\" t=\"n\"><v>{value}</v></c>"
                    : $"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Xml(value)}</t></is></c>");
            }
            sb.Append("</row>");
        }

        sb.Append("</sheetData>");
        if (table.Columns.Count > 0 && table.Rows.Count > 0)
            sb.Append($"<autoFilter ref=\"A1:{lastColumn}{table.Rows.Count + 1}\"/>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    /// <summary>列宽：按该列"表头 ＋ 前若干行的最长值"估（夹在 10~60 之间）—— 让产物打开就能看，
    /// 又**不依赖**任何字体度量（确定性）。</summary>
    private static int ColumnWidth(ReportTable table, int column)
    {
        var max = table.Columns[column].DisplayName.Length;
        for (var r = 0; r < table.Rows.Count && r < 50; r++)
        {
            var row = table.Rows[r];
            if (column < row.Count) max = Math.Max(max, (row[column] ?? string.Empty).Length);
        }
        return Math.Clamp(max + 2, 10, 60);
    }

    /// <summary>0 → <c>A</c>、25 → <c>Z</c>、26 → <c>AA</c>（报表列数不大，但别写死 26 的上界）。</summary>
    internal static string ColumnRef(int index)
    {
        var sb = new StringBuilder();
        for (var i = index; i >= 0; i = i / 26 - 1)
            sb.Insert(0, (char)('A' + i % 26));
        return sb.ToString();
    }

    /// <summary>XML 文本转义（含控制字符剔除 —— Excel 不接受非法 XML 字符，会报"文件损坏"）。</summary>
    internal static string Xml(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&apos;"); break;
                default:
                    // XML 1.0 合法字符：Tab/LF/CR ＋ >= 0x20（其余控制字符一律丢掉，别写进 xlsx）
                    if (ch == '\t' || ch == '\n' || ch == '\r' || ch >= 0x20) sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>数值判定（判据用它对齐口径）。</summary>
    internal static bool IsNumericCell(string value) => NumericCell.IsMatch(value);
}
