using System;
using System.IO;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using System.Collections.Generic;
using Avalonia.Platform.Storage;
using HmiEditor.App.Resources;
using HmiEditor.Core.Reporting;
using SkiaSharp;

namespace HmiEditor.App.Services;

/// <summary>
/// **报表导出的公共出口**（阶段 174）：把"选格式 → 渲染 → 写盘 → 回报"收成**唯一一处**，
/// 各个报表面板（审计、报警、将来的历史数据）只提供数据源与自己的文案键。
///
/// <para><b>为什么必须收成一处</b>：四格式（CSV / XLSX / PDF / HTML）的写盘分支原先写在审计面板里；
/// 再加一个报警面板就会**抄第二份**，而"同一实现两份必然漂移"是本仓反复踩过的坑
/// （见登记 §12.7 那对抄了两遍的失败模板）。判据钉着"面板里不许再出现写盘 switch"。</para>
///
/// <para><b>可判性</b>：渲染那一半（<see cref="Render"/>）是**纯函数**（不弹选择器、不读时钟——
/// 时间与 creation 都由调用方传入）⇒ 判据能直接对四种扩展名逐一对拍；
/// 只有 <see cref="SaveAsync"/> 需要 <see cref="TopLevel"/>（文件选择器）。</para>
/// </summary>
public static class ReportExport
{
    /// <summary>渲染结果：成功给字节与建议扩展名；失败给**已经本地化**的提示（调用方直接显示）。</summary>
    /// <param name="Bytes">产物字节；失败时为 <c>null</c>。</param>
    /// <param name="Message">失败提示（已本地化）；成功时为 <c>null</c>。</param>
    public readonly record struct Result(byte[]? Bytes, string? Message);

    /// <summary>支持的扩展名（文件选择器与渲染分支共用同一张表）。</summary>
    public static readonly string[] Extensions = { ".csv", ".xlsx", ".pdf", ".html" };

    /// <summary>
    /// **按扩展名渲染**（纯函数）。<paramref name="extension"/> 不是已知格式时按 CSV 渲染
    /// （与"默认格式 = CSV"一致）。PDF 需要可嵌入字体：<paramref name="typeface"/> 为 <c>null</c> 时
    /// **不产出豆腐块**，而是回一句明确提示。
    /// </summary>
    public static Result Render(
        ReportTable table,
        string extension,
        string generatedAt,
        string generatedLabel,
        string langTag,
        string typefaceMissingMessage,
        SKTypeface? typeface,
        DateTimeOffset creation)
    {
        ArgumentNullException.ThrowIfNull(table);

        switch (extension.ToLowerInvariant())
        {
            case ".html":
                return new Result(System.Text.Encoding.UTF8.GetBytes(
                    ReportExporter.ToHtml(table, generatedAt, generatedLabel, langTag)), null);

            case ".xlsx":
                return new Result(XlsxReportWriter.Write(table), null);

            case ".pdf":
                if (typeface is null) return new Result(null, typefaceMissingMessage);
                return new Result(PdfReportWriter.Write(table, generatedAt, generatedLabel, typeface, creation), null);

            default:
                // 报表自带 CSV 形态（如历史数据要跟设备 SD 卡 CSV 对齐）时优先用它的
                return new Result(table.CsvOverride ?? ReportExporter.ToCsvBytes(table), null);
        }
    }

    /// <summary>
    /// **要不要先让用户选列**（阶段 177）：只有**一列**时没什么可选的 ⇒ 直接导出（不给用户白弹一次对话框）。
    /// </summary>
    public static bool NeedsColumnChoice(ReportTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.Columns.Count > 1;
    }

    /// <summary>
    /// **把用户勾选的列作用到表上**（纯函数，判据直接打它）：实际就是
    /// <see cref="ReportTable.WithColumns"/> —— 顺序沿用原表；勾选的键为空时由 Core 抛
    /// （对话框那边"一列不勾 ⇒ 确定不可点"，两侧一致）。
    /// </summary>
    public static ReportTable ApplyColumnSelection(ReportTable table, IReadOnlyList<string> selectedKeys)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.WithColumns(selectedKeys);
    }

    /// <summary>打包结果：写了哪些文件、跳过了哪些（带原因）、出错信息（成功为 <c>null</c>）。</summary>
    public readonly record struct BundleResult(
        IReadOnlyList<string> Written,
        IReadOnlyList<string> Skipped,
        string? Error);

    /// <summary>
    /// **把一个数据源打成四种格式写进一个目录**（阶段 179）＋附一份 <c>index.txt</c>：
    /// 每个扩展名走同一套 <see cref="Render"/>；**渲染不出来的格式记进 <c>Skipped</c> 而不是静默丢**
    /// （典型是"没有可嵌入字体 ⇒ PDF 跳过"）。
    ///
    /// <para>文件名与索引文本都来自 <see cref="ReportBundle"/>（时间由调用方给 ⇒ 两次打包**逐字节相同**）；
    /// 本方法是**纯 IO ＋ 纯渲染**（不弹选择器），判据可以直接在临时目录里跑。</para>
    /// </summary>
    public static BundleResult WriteBundle(
        string directory,
        string fileKind,
        ReportTable table,
        DateTimeOffset now,
        string generatedAt,
        string generatedLabel,
        string langTag,
        string typefaceMissingMessage,
        SKTypeface? typeface) =>
        WriteMultiBundle(
            directory,
            new[] { new ReportBundleItem(fileKind, table) },
            Array.Empty<string>(),
            missingSourceMessage: _ => "",
            now, generatedAt, generatedLabel, langTag, typefaceMissingMessage, typeface);

    /// <summary>
    /// **把若干数据源打成四种格式写进一个目录**（阶段 186）：定时/自动导出要的就是这一形态 ——
    /// 一次写出**当前所有可用数据源**的报表 ＋ 一份 <c>index.txt</c>。
    ///
    /// <para>与单源那条 <see cref="WriteBundle"/> **共用同一份实现**（后者只是它的一个元素）：文件名、
    /// 索引格式、跳过原因因此不可能分叉。两个新增口径：</para>
    /// <list type="bullet">
    /// <item><b>渲染不出来的格式带上文件名</b>（多源时只写扩展名会分不清是哪个源跳过了）；</item>
    /// <item><b>缺的源如实进 skipped</b>：<paramref name="missingSources"/> 里的每个源按
    /// <paramref name="missingSourceMessage"/>（**源名 → 一句话**）记一行
    /// （"这次少导了什么"必须在产物里看得见）。⚠ 这里收的是**函数**不是模板字符串：
    /// 那条文案带 <c>{0}</c>，若让写盘侧拿一个模板去查索引器，界面上会显示字面量 <c>{0}</c>
    /// —— `LocalizedFormatCallSiteTests` 正是抓这个的（阶段 186 真抓到过一次）。</item>
    /// </list>
    /// </summary>
    public static BundleResult WriteMultiBundle(
        string directory,
        IReadOnlyList<ReportBundleItem> items,
        IReadOnlyList<string> missingSources,
        Func<string, string> missingSourceMessage,
        DateTimeOffset now,
        string generatedAt,
        string generatedLabel,
        string langTag,
        string typefaceMissingMessage,
        SKTypeface? typeface)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(missingSources);

        var written = new List<(string FileName, int Rows)>();
        var skipped = new List<string>();

        try
        {
            Directory.CreateDirectory(directory);
            foreach (var item in items)
            {
                foreach (var extension in Extensions)
                {
                    var name = ReportBundle.FileName(item.Source, now, extension);
                    var rendered = Render(item.Table, extension, generatedAt, generatedLabel, langTag,
                        typefaceMissingMessage, typeface, now);
                    if (rendered.Bytes is null)
                    {
                        skipped.Add($"{name}: {rendered.Message}");
                        continue;
                    }

                    File.WriteAllBytes(Path.Combine(directory, name), rendered.Bytes);
                    written.Add((name, item.Table.Rows.Count));
                }
            }

            foreach (var source in missingSources)
                skipped.Add(missingSourceMessage(source));   // 模板里带 {0} ⇒ 由**调用方**按语言格式化（守卫钉着）

            File.WriteAllText(
                Path.Combine(directory, ReportBundle.IndexFileName),
                ReportBundle.IndexText(written, skipped),
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            return new BundleResult(written.Select(w => w.FileName).ToList(), skipped, null);
        }
        catch (Exception ex)
        {
            return new BundleResult(written.Select(w => w.FileName).ToList(), skipped, ex.Message);
        }
    }

    /// <summary>
    /// **弹目录选择器再打包**：取消返回 <c>null</c>；成功/失败都给一句给用户看的话
    /// （<see cref="BundleResult.Error"/> 非空时把它交给 <see cref="UiOperationGuard"/>）。
    /// </summary>
    public static async Task<string?> SaveBundleAsync(
        Control owner, string fileKind, ReportTable table, int rowCount, string emptyMessage)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(table);

        if (rowCount == 0) return emptyMessage;   // 各数据源自己的"没有可导出的…"
        if (TopLevel.GetTopLevel(owner) is not Window window) return null;

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = AppLocalization.Service[Lang.ReportBundleSaveTitle],
            AllowMultiple = false,
        });

        var directory = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(directory)) return null;

        var result = WriteBundle(
            directory, fileKind, table, DateTimeOffset.Now,
            ReportExporter.FormatUtc(DateTime.UtcNow), AppLocalization.Service[Lang.AuditReportGenerated],
            AppLocalization.Service.CurrentLanguage, AppLocalization.Service[Lang.ReportExportNoFont],
            PdfReportWriter.ResolveTypeface());

        if (result.Error is not null)
            throw new InvalidOperationException(result.Error);   // 交给 UiOperationGuard 收口（带操作名）

        return AppLocalization.Service.Format(Lang.ReportBundleOk, result.Written.Count, directory);
    }

    /// <summary>
    /// **某数据源当前存的列布局模板**（阶段 183）：没设过 ⇒ <c>null</c>。
    /// 存储是**应用级偏好**（`App.SettingsService`，用户拍板 —— 见
    /// <c>SettingsService.ReportTemplateSettings</c> 的类注释）。
    /// </summary>
    public static ReportTemplate? TemplateFor(string sourceKey) =>
        App.SettingsService?.ReportTemplates.Get(sourceKey);

    /// <summary>
    /// **套用该数据源的模板**（「导出报表」这条路的唯一入口）：没设过模板 ⇒ **原样返回**同一张表。
    ///
    /// <para>⚠ 三个面板**都**要走这里（不许各自读设置），这样"套用点"只有一处；
    /// 而 <c>ReportSourceKeys</c> 保证面板传下去的键与存储键是同一批字符串。</para>
    /// </summary>
    public static ReportTable ApplyTemplate(ReportTable table, string sourceKey)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.ApplyTemplate(TemplateFor(sourceKey));
    }

    /// <summary>
    /// **弹一次报表设计器并保存**（阶段 183）：返回用户是否保存了（取消/无宿主 ⇒ <c>false</c>，
    /// 且**不写设置** —— 取消不该留痕）。
    ///
    /// <para>传进去的是**列清单**而不是整张表：设计器只需要列（历史数据的行可能上千）。
    /// 列清单由各数据源的 <c>XxxReport.Columns(...)</c> 给，与导出处是同一份构造。</para>
    /// </summary>
    public static async Task<bool> EditTemplateAsync(
        Control owner, string sourceKey, IReadOnlyList<ReportColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(columns);

        // 无宿主（判据/设计器）⇒ 什么都不做：宁可不设模板，也不弹一个没有主人的模态框
        if (TopLevel.GetTopLevel(owner) is not Window window) return false;

        var dialog = new Views.ReportDesignerDialog(columns, TemplateFor(sourceKey));
        var template = await dialog.ShowDialog<ReportTemplate?>(window);
        if (template is null) return false;   // 取消

        var settings = App.SettingsService;
        if (settings is null) return false;

        settings.ReportTemplates.Set(sourceKey, template);
        return true;
    }

    /// <summary>
    /// **弹一次时间范围对话框**（阶段 178；阶段 180 起返回带日期的**选择**）：返回用户选的
    /// 档位（自定义档另带两个日期）；取消 ⇒ <c>null</c>。
    /// 范围本身由调用方用**自己的 now** 算（<see cref="ReportRangeOptions.Compute(ReportRangeSelection, DateTimeOffset)"/>）——
    /// "当前时刻"只有一个来源，判据也能用固定时刻核对。
    /// </summary>
    public static async Task<ReportRangeSelection?> ChooseRangeAsync(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        // 无宿主（判据/设计器）⇒ 视作取消：宁可不导出，也不弹一个没有主人的模态框
        if (TopLevel.GetTopLevel(owner) is not Window window) return null;

        var dialog = new Views.ReportRangeDialog(new ReportRangeSelection(ReportRangeChoice.All));
        return await dialog.ShowDialog<ReportRangeSelection?>(window);
    }

    /// <summary>
    /// **弹一次列选择对话框**：取消返回 <c>null</c>（调用方据此中止导出）；只有一列时**不弹**，
    /// 原样返回。窗口自己负责"至少勾一列"（确定按钮在零勾选时不可点）。
    /// </summary>
    public static async Task<ReportTable?> ChooseColumnsAsync(Control owner, ReportTable table)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(table);

        if (!NeedsColumnChoice(table)) return table;

        // 无宿主（判据/设计器）⇒ 视作取消：宁可不导出，也不弹一个没有主人的模态框
        if (TopLevel.GetTopLevel(owner) is not Window window) return null;

        var dialog = new Views.ReportColumnDialog(table.Columns);
        var keys = await dialog.ShowDialog<IReadOnlyList<string>?>(window);
        return keys is null ? null : ApplyColumnSelection(table, keys);
    }

    /// <summary>文件类型选择器里的四种过滤名（顺序即选择器顺序）。</summary>
    public static FilePickerFileType[] FileTypes() => new[]
    {
        Filter(Lang.ReportFilterCsv, "*.csv"),
        Filter(Lang.ReportFilterXlsx, "*.xlsx"),
        Filter(Lang.ReportFilterPdf, "*.pdf"),
        Filter(Lang.ReportFilterHtml, "*.html"),
    };

    private static FilePickerFileType Filter(string key, string pattern) =>
        new(AppLocalization.Service[key]) { Patterns = new[] { pattern } };

    /// <summary>
    /// **选格式并写盘**。返回给用户的提示文案（成功/失败/空）；用户取消返回 <c>null</c>。
    /// <paramref name="emptyMessage"/> 由调用方给（"没有可导出的审计记录"/"…报警记录"各说各的）。
    /// </summary>
    public static async Task<string?> SaveAsync(
        TopLevel topLevel,
        ReportTable table,
        string saveTitleKey,
        string emptyMessage,
        int entryCount)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        if (entryCount == 0) return emptyMessage;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = AppLocalization.Service[saveTitleKey],
            DefaultExtension = ".csv",
            FileTypeChoices = FileTypes(),
        });

        if (file is null) return null;

        var rendered = Render(
            table,
            Path.GetExtension(file.Name),
            ReportExporter.FormatUtc(DateTime.UtcNow),
            AppLocalization.Service[Lang.AuditReportGenerated],
            AppLocalization.Service.CurrentLanguage,
            AppLocalization.Service[Lang.ReportExportNoFont],
            PdfReportWriter.ResolveTypeface(),
            DateTimeOffset.Now);

        if (rendered.Bytes is null) return rendered.Message;

        using (var stream = await file.OpenWriteAsync())
            await stream.WriteAsync(rendered.Bytes);

        return AppLocalization.Service.Format(Lang.ReportExportOk, file.Name);
    }
}
