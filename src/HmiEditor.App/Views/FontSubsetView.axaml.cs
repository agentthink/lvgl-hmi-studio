using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Views;

/// <summary>
/// I12: 字体子集化分析对话框 —— 展示项目实际字符统计与
/// LVGL 子集化内嵌字体的 Flash 预算,支持导出 lv_font_conv 输入。
/// </summary>
public partial class FontSubsetView : ChromeWindow
{
    private readonly HmiProject _project;
    private FontSubsetReport? _report;

    /// <summary>
    /// **平台"另存为"选择器的注入缝**（阶段 258）：非 null 时导出这一步走它、不碰
    /// <see cref="TopLevel.StorageProvider"/>。
    ///
    /// <para><b>为什么要有这条缝</b>：headless 下 `SaveFilePickerAsync` 的桩返回 null
    /// ⇒ `ExportCharset()` 在 `if (file == null) return;` 处早退 ⇒ **"导出的文件里到底写了什么"这条腿
    /// 原理上够不到**（只能记 Inert「平台桩」）。加缝之后判据能给一个沙箱路径，于是
    /// `FontSubsetAnalyzer.ExportCharsetFile(report, path)` 真的落盘、内容可逐字节判。</para>
    ///
    /// <para>⚠ `internal`：不必多出一条**公共**构造 ⇒ 不惊动 `WindowProductionPathUiTests`
    /// 的「双路径」元检查（它只数公共构造）；与阶段 209/254 同一处置。</para>
    /// </summary>
    private readonly System.Func<System.Threading.Tasks.Task<string?>>? _pickSavePath;

    public FontSubsetView()
    {
        _project = null!;
        InitializeComponent();
    }

    public FontSubsetView(HmiProject project) : this()
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        AnalyzeAndRender();

        BtnExportCharset.Click += async (_, _) => await ExportCharset();
        BtnClose.Click += (_, _) => Close();
    }

    /// <summary>注入缝构造（阶段 258）：<paramref name="pickSavePath"/> 传 null ⇒ 与加缝之前逐字相同。</summary>
    internal FontSubsetView(HmiProject project,
                            System.Func<System.Threading.Tasks.Task<string?>>? pickSavePath) : this(project)
    {
        _pickSavePath = pickSavePath;
    }

    private void AnalyzeAndRender()
    {
        _report = FontSubsetAnalyzer.Analyze(_project, fontSizePx: 20, bpp: 16);

        TotalCharsText.Text = _report.TotalChars.ToString("N0");
        UniqueCharsText.Text = _report.UniqueChars.ToString("N0");
        AsciiCharsText.Text = _report.AsciiChars.ToString("N0");
        CjkCharsText.Text = _report.CjkChars.ToString("N0");

        SubsetSizeText.Text = FontSubsetAnalyzer.FormatBytes(_report.EstimatedSubsetBytes);
        FullSizeText.Text = FontSubsetAnalyzer.FormatBytes(FontSubsetAnalyzer.TypicalFullCjkFontBytes);

        if (_report.UniqueChars > 0)
        {
            var savings = 1.0 - (double)_report.EstimatedSubsetBytes / FontSubsetAnalyzer.TypicalFullCjkFontBytes;
            SavingsText.Text = $"{savings * 100:0.#}%";
        }
        else
        {
            SavingsText.Text = "—";
        }

        // 字符预览: 全部字符紧凑拼接(最多 300 字符)
        var preview = new string(_report.Characters.Take(300).ToArray());
        CharsPreviewText.Text = _report.UniqueChars == 0
            ? AppLocalization.Service[Lang.FontNoVisibleText]
            : preview;
    }

    private async System.Threading.Tasks.Task ExportCharset()
    {
        if (_report == null) return;

        if (_pickSavePath is not null)
        {
            var chosen = await _pickSavePath();
            if (string.IsNullOrEmpty(chosen)) return;
            WriteCharset(chosen);
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = AppLocalization.Service[Lang.FontExportTitle],
            SuggestedFileName = "charset.txt",
            DefaultExtension = "txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(AppLocalization.Service[Lang.FontFiletypeTxt]) { Patterns = new[] { "*.txt" } }
            }
        });
        if (file == null) return;

        WriteCharset(file.Path.LocalPath);
    }

    /// <summary>把字符集写到给定路径（两条路径共用：平台选择器 / 注入缝）。</summary>
    private void WriteCharset(string outputPath)
    {
        try
        {
            FontSubsetAnalyzer.ExportCharsetFile(_report!, outputPath);
            ExportHintText.Text = AppLocalization.Service.Format(Lang.FontExported, _report!.UniqueChars);
        }
        catch (Exception ex)
        {
            ExportHintText.Text = UiOperationGuard.FailureDetail(Lang.UiOpExportCharset, outputPath, ex);
        }
    }
}
