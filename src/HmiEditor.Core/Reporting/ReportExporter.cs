using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// 报表导出器（阶段 172，报表能力 MVP）—— **零依赖**、纯函数、产物确定（可逐字节判）。
///
/// <para><b>两种产物、各管一头</b>：<see cref="ToCsv"/>（数据；Excel 里还能继续算）与
/// <see cref="ToHtml"/>（人看/打印；**自包含**：内联样式、零外链 ⇒ 离线也能打开与打印）。
/// 选型对比（QuestPDF / EPPlus / Scriban 各自为什么没选）见
/// <c>docs/2026-10-01-reporting-capability-mvp.md</c> §3。</para>
///
/// <para><b>确定性</b>：本类**不读时钟、不读环境**（没有 <c>DateTime.Now</c>、没有默认区域）——
/// 生成时间与语言标记都由调用方传进来 ⇒ 同一个 <see cref="ReportTable"/> 两次导出逐字节相同，
/// 判据因此能钉死它（"时间戳直接取 Now" 是四条负控之一）。</para>
/// </summary>
public static class ReportExporter
{
    /// <summary>CSV 行尾（RFC4180 要求 CRLF）。⚠ 刻意不同于 <c>string.AppendLine()</c>（随平台变）——
    /// 报表产物要跨平台逐字节一致。</summary>
    public const string Crlf = "\r\n";

    /// <summary>UTF-8 BOM（Excel 打开带 CJK 的 CSV 时需要它，否则按本地代码页解码成乱码）。</summary>
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    /// <summary>
    /// 导出 CSV（RFC4180）：逗号分隔、CRLF 行尾、单元格按需加引号（含 <c>,</c>/<c>"</c>/CR/LF 时，
    /// 内部引号翻倍）。第一行是**列名**（取自 <see cref="ReportColumn.DisplayName"/>）。
    /// </summary>
    public static string ToCsv(ReportTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var sb = new StringBuilder();
        AppendRow(sb, table.Columns.Select(c => c.DisplayName).ToList());
        foreach (var row in table.Rows) AppendRow(sb, row);
        return sb.ToString();
    }

    /// <summary>
    /// 导出 CSV 的**字节**（UTF-8 ＋ BOM ＋ <see cref="ToCsv"/>）。给"写盘"用：
    /// 判据与调用方都以字节为准，避免"字符串对了、编码错了"这种只有用户能发现的差异。
    /// </summary>
    public static byte[] ToCsvBytes(ReportTable table)
    {
        var payload = Encoding.UTF8.GetBytes(ToCsv(table));
        var bytes = new byte[Utf8Bom.Length + payload.Length];
        Buffer.BlockCopy(Utf8Bom, 0, bytes, 0, Utf8Bom.Length);
        Buffer.BlockCopy(payload, 0, bytes, Utf8Bom.Length, payload.Length);
        return bytes;
    }

    private static void AppendRow(StringBuilder sb, IReadOnlyList<string> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(EscapeCsv(cells[i] ?? string.Empty));
        }
        sb.Append(Crlf);
    }

    /// <summary>
    /// 单元格转义：**只有**含 <c>,</c> / <c>"</c> / CR / LF 时才加引号并把内部引号翻倍。
    /// 不含特殊字符时**不加**引号 —— 这一条有正控判据盯着（"全都加引号"也能让"转义了"看起来成立）。
    /// </summary>
    internal static string EscapeCsv(string value)
    {
        var needsQuotes = value.IndexOf(',') >= 0
                          || value.IndexOf('"') >= 0
                          || value.IndexOf('\r') >= 0
                          || value.IndexOf('\n') >= 0;
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    /// <summary>
    /// 导出**自包含** HTML：内联样式、<c>&lt;meta charset="utf-8"&gt;</c>、**零外链**
    /// （没有 <c>script src</c> / <c>link href</c> / <c>http(s)://</c> 资源）⇒ 离线可打开、可打印。
    ///
    /// <para>参数全部由调用方给（标题、生成时间文本、生成时间标签、语言标记）——
    /// 本方法**不读时钟、不读当前语言**，产物因此确定。</para>
    /// </summary>
    /// <param name="table">报表数据。</param>
    /// <param name="generatedAt">**已格式化**的生成时间（调用方负责格式化与时区口径）。</param>
    /// <param name="generatedLabel">"生成时间"这个标签（已本地化）。</param>
    /// <param name="langTag">HTML <c>lang</c> 属性（如 <c>zh-CN</c> / <c>en-US</c>）。</param>
    public static string ToHtml(ReportTable table, string generatedAt, string generatedLabel, string langTag)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(generatedAt);
        ArgumentNullException.ThrowIfNull(generatedLabel);
        ArgumentNullException.ThrowIfNull(langTag);

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>").Append('\n');
        sb.Append("<html lang=\"").Append(Html(langTag)).Append("\">").Append('\n');
        sb.Append("<head>").Append('\n');
        sb.Append("<meta charset=\"utf-8\">").Append('\n');
        sb.Append("<title>").Append(Html(table.Title)).Append("</title>").Append('\n');
        sb.Append("<style>").Append('\n');
        sb.Append("body{font-family:system-ui,'Segoe UI',sans-serif;margin:24px;color:#1b1b1b}").Append('\n');
        sb.Append("h1{font-size:18px;margin:0 0 4px}").Append('\n');
        sb.Append(".meta{color:#666;font-size:12px;margin-bottom:12px}").Append('\n');
        sb.Append("table{border-collapse:collapse;width:100%;font-size:12px}").Append('\n');
        sb.Append("th,td{border:1px solid #c8c8c8;padding:4px 8px;text-align:left;vertical-align:top}").Append('\n');
        sb.Append("th{background:#f0f0f0}").Append('\n');
        sb.Append("</style>").Append('\n');
        sb.Append("</head>").Append('\n');
        sb.Append("<body>").Append('\n');
        sb.Append("<h1>").Append(Html(table.Title)).Append("</h1>").Append('\n');
        sb.Append("<div class=\"meta\">").Append(Html(generatedLabel)).Append(": ")
          .Append(Html(generatedAt)).Append("</div>").Append('\n');
        sb.Append("<table>").Append('\n');
        sb.Append("<thead><tr>");
        foreach (var column in table.Columns)
            sb.Append("<th>").Append(Html(column.DisplayName)).Append("</th>");
        sb.Append("</tr></thead>").Append('\n');
        sb.Append("<tbody>").Append('\n');
        foreach (var row in table.Rows)
        {
            sb.Append("<tr>");
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var cell = i < row.Count ? row[i] : string.Empty;
                sb.Append("<td>").Append(Html(cell ?? string.Empty)).Append("</td>");
            }
            sb.Append("</tr>").Append('\n');
        }
        sb.Append("</tbody>").Append('\n');
        sb.Append("</table>").Append('\n');
        sb.Append("</body>").Append('\n');
        sb.Append("</html>").Append('\n');
        return sb.ToString();
    }

    /// <summary>HTML 文本转义（<c>&amp; &lt; &gt; "</c>）—— 报表单元格里出现 <c>&lt;script&gt;</c>
    /// 之类的内容时，不许它变成标签。</summary>
    internal static string Html(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    /// <summary>审计/报表通用的时间格式：**UTC ＋ 显式 <c>Z</c>**、不随区域变
    /// （诚实边界：本产品不做跨时区换算承诺，见专题 §6）。</summary>
    public static string FormatUtc(DateTime timestamp) =>
        timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
