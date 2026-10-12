using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// **报表打包**（阶段 179）：一个数据源 × 四种格式，落到一个目录里，并附一份 <c>index.txt</c>。
///
/// <para>为什么要有它：单格式导出解决"我要一份 CSV"，而留档/交付常常要"**同一批数据的四种形态
/// 放一起**"（人看的 HTML/PDF ＋ 还能继续算的 CSV/Excel）。本类只管**文件名与索引文本**这两件
/// 纯事（时间由调用方给 ⇒ 确定、可判）；真正写盘在 App 侧（要 Skia 字体渲染 PDF）。</para>
/// </summary>
public static class ReportBundle
{
    /// <summary>索引文件名（固定，不带时间戳 —— 一个目录一次打包只有一份）。</summary>
    public const string IndexFileName = "index.txt";

    /// <summary>
    /// 报表文件名：<c>&lt;kind&gt;-&lt;yyyyMMdd-HHmmss&gt;&lt;ext&gt;</c>。
    /// <paramref name="kind"/> 里的路径非法字符与空白清成 <c>-</c>（中文保留：它只是文件名，不是路径分隔）；
    /// <paramref name="timestamp"/> 由调用方给（**不读时钟** ⇒ 判据可判）。
    /// </summary>
    public static string FileName(string kind, DateTimeOffset timestamp, string extension)
    {
        var safe = new StringBuilder();
        foreach (var ch in kind ?? string.Empty)
        {
            safe.Append(char.IsWhiteSpace(ch) || ch == '/' || ch == '\\' || ch == ':' || ch == '*'
                        || ch == '?' || ch == '"' || ch == '<' || ch == '>' || ch == '|'
                ? '-'
                : ch);
        }

        var stem = safe.ToString().Trim('-');
        if (stem.Length == 0) stem = "report";

        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return $"{stem}-{timestamp.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}{ext}";
    }

    /// <summary>
    /// 索引文本：一行一个产物（<c>文件名 行数</c>），末尾列出**没导出的**格式与原因
    /// （例如"没有可嵌入的字体"）。行尾统一 <c>\n</c>、结尾也带一个 —— 与报表 CSV 的确定性口径一致。
    /// </summary>
    public static string IndexText(
        IReadOnlyList<(string FileName, int Rows)> written,
        IReadOnlyList<string> skipped)
    {
        ArgumentNullException.ThrowIfNull(written);
        ArgumentNullException.ThrowIfNull(skipped);

        var sb = new StringBuilder();
        sb.Append("file\trows\n");
        foreach (var (name, rows) in written)
            sb.Append(name).Append('\t').Append(rows.ToString(CultureInfo.InvariantCulture)).Append('\n');

        if (skipped.Count > 0)
        {
            sb.Append("\nskipped\n");
            foreach (var reason in skipped) sb.Append(reason).Append('\n');
        }

        return sb.ToString();
    }
}
