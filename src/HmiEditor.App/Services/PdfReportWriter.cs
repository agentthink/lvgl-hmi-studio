using System;
using System.Collections.Generic;
using System.IO;
using HmiEditor.Core.Reporting;
using SkiaSharp;

namespace HmiEditor.App.Services;

/// <summary>
/// **PDF 写出器**（阶段 173，报表能力）—— 走 **SkiaSharp 的 PDF 后端**（MIT 许可，
/// 且它**本来就在依赖闭包里**：Avalonia 的 Skia 渲染栈），因此**没有新增第三方库**。
///
/// <para><b>为什么不是手写 PDF</b>：中文报表要**嵌入字体**才能显示；手写 PDF 得自己实现
/// TrueType 子集化 ＋ CID 字体 ＋ ToUnicode CMap，代价远大于用已在场的 Skia。
/// 实测（阶段 173 探针）：Skia 会**按用到的字形子集化嵌入**（含 <c>/ToUnicode</c>），
/// 一个只含一行中文的报表 PDF 约 11 KB；并且在**固定 creation** 时**两次导出逐字节相同**。</para>
///
/// <para><b>范围（刻意不做）</b>：单元格**不换行**（超宽按列宽裁剪）、不做页眉页脚图形、
/// 不做分栏/图表。分页按"每页行数"算，页脚只有"第 x / y 页"。</para>
/// </summary>
public static class PdfReportWriter
{
    private const float PageWidth = 595f;    // A4 纵向（pt）
    private const float PageHeight = 842f;
    private const float Margin = 40f;
    private const float HeaderHeight = 18f;
    private const float RowHeight = 16f;

    /// <summary>每页能放多少数据行（页脚留 24pt）。</summary>
    internal static int RowsPerPage =>
        Math.Max(1, (int)((PageHeight - Margin * 2 - 46f - 24f) / RowHeight));

    /// <summary>
    /// 生成 PDF 字节。
    /// <paramref name="creation"/> **由调用方给**（不读时钟）：产物因此可判（两次导出逐字节相同）。
    /// </summary>
    /// <param name="table">报表数据。</param>
    /// <param name="generatedAt">已格式化的生成时间（与 CSV/HTML 用同一个字符串）。</param>
    /// <param name="generatedLabel">"生成时间"标签（已本地化）。</param>
    /// <param name="typeface">嵌入用的字体（中文必须给 CJK 字体；调用方负责解析）。</param>
    /// <param name="creation">PDF 元数据里的创建时间（固定值 ⇒ 确定性）。</param>
    public static byte[] Write(
        ReportTable table,
        string generatedAt,
        string generatedLabel,
        SKTypeface typeface,
        DateTimeOffset creation)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(typeface);

        using var ms = new MemoryStream();
        var metadata = new SKDocumentPdfMetadata
        {
            Creation = creation.UtcDateTime,
            Producer = "HMI Editor",
            Title = table.Title,
        };

        using (var document = SKDocument.CreatePdf(ms, metadata))
        {
            var columns = table.Columns;
            var columnWidth = columns.Count == 0
                ? PageWidth - Margin * 2
                : (PageWidth - Margin * 2) / columns.Count;
            var pages = Math.Max(1, (int)Math.Ceiling(table.Rows.Count / (double)RowsPerPage));

            for (var page = 0; page < pages; page++)
            {
                var canvas = document.BeginPage(PageWidth, PageHeight);
                var y = Margin;

                // 标题
                using (var titleFont = new SKFont(typeface, 14f))
                using (var paint = new SKPaint { Color = new SKColor(0x1B, 0x1B, 0x1B), IsAntialias = true })
                {
                    canvas.DrawText(table.Title, Margin, y + 12f, SKTextAlign.Left, titleFont, paint);
                }
                y += HeaderHeight;

                using (var metaFont = new SKFont(typeface, 9f))
                using (var paint = new SKPaint { Color = new SKColor(0x66, 0x66, 0x66), IsAntialias = true })
                {
                    canvas.DrawText($"{generatedLabel}: {generatedAt}", Margin, y + 8f, SKTextAlign.Left, metaFont, paint);
                }
                y += HeaderHeight;

                // 表头底纹
                using (var fill = new SKPaint { Color = new SKColor(0xF0, 0xF0, 0xF0) })
                {
                    canvas.DrawRect(new SKRect(Margin, y, PageWidth - Margin, y + RowHeight), fill);
                }

                using var cellFont = new SKFont(typeface, 9f);
                using var cellPaint = new SKPaint { Color = new SKColor(0x1B, 0x1B, 0x1B), IsAntialias = true };

                for (var c = 0; c < columns.Count; c++)
                {
                    DrawCell(canvas, cellFont, cellPaint,
                        columns[c].DisplayName, Margin + c * columnWidth, y, columnWidth);
                }
                y += RowHeight;

                var first = page * RowsPerPage;
                var last = Math.Min(table.Rows.Count, first + RowsPerPage);
                for (var r = first; r < last; r++)
                {
                    var row = table.Rows[r];
                    for (var c = 0; c < columns.Count; c++)
                    {
                        var value = c < row.Count ? row[c] ?? string.Empty : string.Empty;
                        DrawCell(canvas, cellFont, cellPaint, value, Margin + c * columnWidth, y, columnWidth);
                    }
                    y += RowHeight;
                }

                // 页脚
                using (var footerFont = new SKFont(typeface, 8f))
                using (var paint = new SKPaint { Color = new SKColor(0x88, 0x88, 0x88), IsAntialias = true })
                {
                    var footer = $"{page + 1} / {pages}";
                    canvas.DrawText(footer, PageWidth - Margin, PageHeight - Margin + 10f, SKTextAlign.Right, footerFont, paint);
                }

                document.EndPage();
            }

            document.Close();
        }

        return ms.ToArray();
    }

    /// <summary>画一个单元格：**一行、超宽裁剪**（不换行 —— MVP 的诚实边界，见类注释）。</summary>
    private static void DrawCell(SKCanvas canvas, SKFont font, SKPaint paint, string text, float x, float y, float width)
    {
        canvas.Save();
        canvas.ClipRect(new SKRect(x, y, x + width - 4f, y + RowHeight));
        canvas.DrawText(text, x + 2f, y + 11f, SKTextAlign.Left, font, paint);
        canvas.Restore();
    }

    /// <summary>
    /// 解析一个可嵌入的字体：优先**指定字体族**（如工程的 <c>EditorConfig.DefaultFontFamily</c>），
    /// 否则用系统里能显示中文的字体；都没有则返回 <c>null</c>（调用方据此给用户一句明确提示，
    /// 而不是生成一份"全是豆腐块"的报表）。
    ///
    /// <para>⚠ 诚实边界：字体来自**本机** ⇒ PDF 的观感随机器变（产物字节也随之变）。
    /// 判据里的确定性用的是"**同一台机器、同一个 typeface、固定 creation**"口径，没有跨机器承诺。</para>
    /// </summary>
    public static SKTypeface? ResolveTypeface(string? preferredFamily = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredFamily))
        {
            var byFamily = SKFontManager.Default.MatchFamily(preferredFamily);
            if (byFamily is not null) return byFamily;
        }

        // 系统兜底：找一个真的能画"审"的字体（否则中文报表毫无意义）
        return SKFontManager.Default.MatchCharacter('审');
    }
}
