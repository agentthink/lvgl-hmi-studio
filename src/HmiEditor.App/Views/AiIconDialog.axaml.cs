using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using HmiEditor.App.Resources;
using HmiEditor.App.Controls;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.AI.Generators;
using HmiEditor.AI.Inference;

namespace HmiEditor.App.Views;

/// <summary>
/// AI 生成对话框 —— 双模式: 图标生成 + 图片生成。
/// 图标模式: 每行 "名称: 描述" 批量生成 48×48 单色 SVG 图标并导入资源库。
/// 图片模式: 单张任意尺寸彩色 SVG 图片生成, 支持预览、保存 SVG/PNG、加入资源库。
/// 复用同一 AI 配置与生成链路, 资源入口按钮一键直达完整图片生成能力。
/// </summary>
public partial class AiIconDialog : ChromeWindow
{
    private readonly AiPreviewManager _manager;

    /// <summary>生成源（阶段 209 的注入缝）—— 生成相关的调用都走它，`_manager` 只留给既有的 Pending/应用路径。</summary>
    private readonly IIconGenerationSource _source;

    private bool _busy;

    /// <summary>
    /// 在途生成的取消源（busy 期间非空）。
    /// <para>此前取消按钮在 busy 时被**禁用**、且两个生成入口都不传 token ——
    /// <see cref="BatchIconGenerator.GenerateBatchAsync"/> 里那条
    /// <c>ct.ThrowIfCancellationRequested()</c> 检查点因此在应用里**永不可达**（唯一调用点传的是默认 token）。</para>
    /// </summary>
    private System.Threading.CancellationTokenSource? _cts;

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public AiIconDialog()
    {
        _manager = null!;
        _source = null!;
        InitializeComponent();
    }

    public AiIconDialog(AiPreviewManager manager)
        : this(manager, null)
    {
    }

    /// <summary>
    /// **注入缝构造**（阶段 209）：`source` 传 null ⇒ 用 <see cref="PreviewManagerIconSource"/>
    /// 包住 <paramref name="manager"/>（与加缝之前逐字相同）。判据用一个假源就能把
    /// `InputBox` / `ImageInputBox` / `WidthBox` / `HeightBox` 四格的读取点观测出来。
    /// ⚠ `internal` 与设备侧两处同因（不必多出一条公共构造 ⇒ 不惊动
    /// `WindowProductionPathUiTests` 的「双路径」元检查）。
    /// </summary>
    internal AiIconDialog(AiPreviewManager manager, IIconGenerationSource? source) : this()
    {
        _manager = manager;
        _source = source ?? new PreviewManagerIconSource(manager);

        InitializeVariantCombo();

        GenerateBtn.Click += OnGenerateClicked;
        CancelBtn.Click += OnCancelClicked;
        SaveSvgBtn.Click += OnSaveSvgClicked;
        SavePngBtn.Click += OnSavePngClicked;
        AddToLibraryBtn.Click += OnAddToLibraryClicked;
    }

    /// <summary>生成并保存的资源条目列表。</summary>
    public List<AssetItem> Results { get; private set; } = new();

    /// <summary>项目资源目录绝对路径(保存 SVG 用), 由调用方注入。</summary>
    public string? AssetDirectory { get; set; }

    /// <summary>当前是否为图片模式(否则为图标模式)。</summary>
    private bool _imageMode;

    /// <summary>最近一次图片生成结果(供预览/保存/入库)。</summary>
    private string? _pendingImageSvg;
    private int _pendingImageWidth;
    private int _pendingImageHeight;

    /// <summary>填充视觉风格变体下拉框(选项: 自动多样 + 7 种固定变体)。</summary>
    private void InitializeVariantCombo()
    {
        var loc = AppLocalization.Service;
        VariantCombo.ItemsSource = new VariantOption[]
        {
            new(loc[Lang.AiIconVariantAuto], null),                               // 自动多样(轮转)
            new(loc[Lang.AiIconVariantLinear], IconStyleVariant.Linear),
            new(loc[Lang.AiIconVariantFilled], IconStyleVariant.Filled),
            new(loc[Lang.AiIconVariantOutline], IconStyleVariant.Outline),
            new(loc[Lang.AiIconVariantRounded], IconStyleVariant.Rounded),
            new(loc[Lang.AiIconVariantMinimal], IconStyleVariant.Minimal),
            new(loc[Lang.AiIconVariantGeometric], IconStyleVariant.Geometric),
            new(loc[Lang.AiIconVariantAccent], IconStyleVariant.Accent),
        };
        VariantCombo.SelectedIndex = 0; // 默认自动多样
        VariantCombo.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<VariantOption>(
            (item, _) => new TextBlock { Text = item.Label, FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
    }

    // ── 模式切换 ──

    private void OnIconModeClicked(object? sender, RoutedEventArgs e)
    {
        SwitchMode(imageMode: false);
    }

    private void OnImageModeClicked(object? sender, RoutedEventArgs e)
    {
        SwitchMode(imageMode: true);
    }

    private void SwitchMode(bool imageMode)
    {
        if (_imageMode == imageMode) return;
        _imageMode = imageMode;

        IconPanel.IsVisible = !imageMode;
        ImagePanel.IsVisible = imageMode;
        IconModeBtn.IsChecked = !imageMode;
        ImageModeBtn.IsChecked = imageMode;

        // 视觉反馈: 选中模式高亮
        IconModeBtn.Background = HmiEditor.App.Theme.Brush(imageMode ? "HmiBgElevatedBrush" : "HmiBgSelectedBrush");
        ImageModeBtn.Background = HmiEditor.App.Theme.Brush(imageMode ? "HmiBgSelectedBrush" : "HmiBgElevatedBrush");

        // 同步窗口标题
        var title = AppLocalization.Service[imageMode ? Lang.WinAiImage : Lang.WinAiIcon];
        Title = title;
        TitleText.Text = title;

        // 切换模式时清空状态
        StatusText.Text = "";
        ResultText.Text = "";
        HideImageResultUi();
    }

    /// <summary>隐藏图片模式的结果 UI（预览 + 操作按钮）。</summary>
    private void HideImageResultUi()
    {
        PreviewBorder.IsVisible = false;
        PreviewImage.SvgText = null;
        SaveSvgBtn.IsVisible = false;
        SavePngBtn.IsVisible = false;
        AddToLibraryBtn.IsVisible = false;
    }

    // ── 生成入口 ──

    /// <summary>
    /// 生成入口。两个生成方法内部已把失败写到本对话框状态行，
    /// 这里只需保证异常不逃逸出事件处理器（UiOperationGuard 负责落盘日志）。
    /// </summary>
    private void OnGenerateClicked(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpGenIcon, async () =>
        {
            if (_busy) return;

            if (_imageMode)
            {
                await GenerateImageAsync();
                return;
            }

            await GenerateIconsAsync();
        }, message => StatusText.Text = message);

    // ── 在途生成的取消链路 ──

    /// <summary>开始一次生成：登记取消源，返回要往下传的 token。</summary>
    private System.Threading.CancellationToken BeginRun()
    {
        _cts?.Dispose();
        _cts = new System.Threading.CancellationTokenSource();
        return _cts.Token;
    }

    /// <summary>结束一次生成：释放取消源。</summary>
    private void EndRun()
    {
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// 取消按钮：busy 时真取消（批量生成会在下一项开始前停住，已在途的那一项安全收尾），
    /// 非 busy 时按原语义关闭并返回已有结果。
    /// </summary>
    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy && _cts is { } cts)
        {
            cts.Cancel();
            StatusText.Text = AppLocalization.Service[Lang.AiCancelled];
            return;
        }

        Close(Results);
    }

    /// <summary>关闭对话框时兜底取消，避免"关窗后还在跑"。幂等。</summary>
    protected override void OnClosed(EventArgs e)
    {
        try { _cts?.Cancel(); }
        catch (ObjectDisposedException) { }
        base.OnClosed(e);
    }

    // ── 图标模式: 批量生成 ──

    private async Task GenerateIconsAsync()
    {
        var input = InputBox.Text?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        if (!_source.IsConfigured)
        {
            StatusText.Text = AppLocalization.Service["ai.not.configured"];
            return;
        }

        if (!_source.HasIconGenerator)
        {
            StatusText.Text = AppLocalization.Service[Lang.AiIconGeneratorUnavailable];
            return;
        }

        // AssetDirectory 为空（未打开项目）时落到**持久**产物目录。
        // 原实现落系统临时目录，注释写的是"保证生成结果不丢失"，而临时目录恰是会被系统
        // 清理、且本机只有 10 MB tmpfs 的地方 —— 用户"保存"的图标会真的消失。
        var assetDirectory = string.IsNullOrEmpty(AssetDirectory)
            ? AppPaths.AssetDir("ai-icons")
            : AssetDirectory;

        _busy = true;
        GenerateBtn.IsEnabled = false;
        ProgressBar1.IsVisible = true;
        ResultText.Text = "";

        var items = BatchIconGenerator.ParseItemList(input);
        if (items.Count == 0)
        {
            StatusText.Text = AppLocalization.Service[Lang.AiIconNoItems];
            _busy = false;
            GenerateBtn.IsEnabled = true;
            return;
        }

        var token = BeginRun();

        try
        {
            Directory.CreateDirectory(assetDirectory);
            // 从下拉框读取用户选择的变体: 自动多样(null) 或 固定变体
            var fixedVariant = (VariantCombo.SelectedItem as VariantOption)?.Variant;
            var generated = await _source.GenerateIconsAsync(items, fixedVariant, token);

            var ok = 0;
            var failed = 0;
            Results = new List<AssetItem>();
            foreach (var item in generated)
            {
                if (!item.Success)
                {
                    failed++;
                    continue;
                }

                var fileName = SanitizeFileName(item.Name) + ".svg";
                var filePath = Path.Combine(assetDirectory, fileName);
                await File.WriteAllTextAsync(filePath, item.SvgText);

                Results.Add(new AssetItem
                {
                    Name = item.Name,
                    Type = AssetType.Svg,
                    FilePath = filePath,
                    SizeBytes = new FileInfo(filePath).Length,
                    Width = 48,
                    Height = 48,
                    ColorDepth = 0
                });
                ok++;
            }

            StatusText.Text = AppLocalization.Service.Format(Lang.AiIconDone, ok, failed);
            ResultText.Text = Results.Count > 0
                ? AppLocalization.Service.Format(Lang.AiIconGenerated, string.Join(", ", Results.Select(r => r.Name)))
                : AppLocalization.Service[Lang.AiIconNoneGenerated];
            if (ok > 0) Close(Results);
        }
        catch (OperationCanceledException)
        {
            // 用户取消：GenerateBatchAsync 按 .NET 约定抛 OCE、**不返回部分结果**，
            // 所以这里不能假装"已生成的在 Results 里" —— 只把状态说清楚。
            // （保留已生成条目需要给它加 partial-results 出口，属独立产品决策，见文档遗留项。）
            Results = new List<AssetItem>();
            StatusText.Text = AppLocalization.Service[Lang.AiCancelled];
            ResultText.Text = "";
        }
        catch (Exception ex)
        {
            StatusText.Text = AppLocalization.Service.Format("ai.gen.failed", ExternalText.FailureReason(ex));
        }
        finally
        {
            EndRun();
            _busy = false;
            GenerateBtn.IsEnabled = true;
            ProgressBar1.IsVisible = false;
        }
    }

    // ── 图片模式: 单张彩色 SVG ──

    private async Task GenerateImageAsync()
    {
        var input = ImageInputBox.Text?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        if (!_source.IsConfigured)
        {
            StatusText.Text = AppLocalization.Service["ai.not.configured"];
            return;
        }

        if (!_source.HasImageGenerator)
        {
            StatusText.Text = AppLocalization.Service[Lang.AiImageGeneratorUnavailable];
            return;
        }

        // 从尺寸输入框读取画布大小（32~1024 由 ImageGenerator 内部钳制）
        var width = (int)(WidthBox.Value ?? 200);
        var height = (int)(HeightBox.Value ?? 200);

        _busy = true;
        GenerateBtn.IsEnabled = false;
        ProgressBar1.IsVisible = true;
        ResultText.Text = "";
        HideImageResultUi();

        var token = BeginRun();

        try
        {
            var result = await _source.GenerateImageAsync(input, width, height, token);
            _pendingImageSvg = result.SvgText;
            _pendingImageWidth = result.Width;
            _pendingImageHeight = result.Height;

            PreviewImage.SvgText = result.SvgText;
            PreviewBorder.IsVisible = true;
            SaveSvgBtn.IsVisible = true;
            SavePngBtn.IsVisible = true;
            AddToLibraryBtn.IsVisible = true;

            StatusText.Text = AppLocalization.Service.Format(Lang.AiImageGenerated, result.Width, result.Height);
            ResultText.Text = $"Token: {result.InputTokens} in / {result.OutputTokens} out";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = AppLocalization.Service[Lang.AiCancelled];
        }
        catch (Exception ex)
        {
            StatusText.Text = AppLocalization.Service.Format("ai.gen.failed", ExternalText.FailureReason(ex));
        }
        finally
        {
            EndRun();
            _busy = false;
            GenerateBtn.IsEnabled = true;
            ProgressBar1.IsVisible = false;
        }
    }

    // ── 图片模式: 保存 / 入库 ──

    /// <summary>保存 SVG 文件到用户指定位置。</summary>
    private async void OnSaveSvgClicked(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_pendingImageSvg)) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = AppLocalization.Service[Lang.AiImageSaveSvg],
            DefaultExtension = "svg",
            SuggestedFileName = $"hmi_image_{DateTime.Now:yyyyMMdd_HHmmss}",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType(AppLocalization.Service[Lang.AiIconFilterSvg]) { Patterns = new[] { "*.svg" } }
            }
        });

        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(_pendingImageSvg);
        }
        catch (Exception ex)
        {
            StatusText.Text = AppLocalization.Service.Format("ai.gen.failed", ExternalText.FailureReason(ex));
        }
    }

    /// <summary>将当前 SVG 光栅化为 PNG 并保存。</summary>
    private async void OnSavePngClicked(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_pendingImageSvg)) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = AppLocalization.Service[Lang.AiImageSavePng],
            DefaultExtension = "png",
            SuggestedFileName = $"hmi_image_{DateTime.Now:yyyyMMdd_HHmmss}",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType(AppLocalization.Service[Lang.AiIconFilterPng]) { Patterns = new[] { "*.png" } }
            }
        });

        if (file is null) return;

        try
        {
            var w = _pendingImageWidth > 0 ? _pendingImageWidth : 200;
            var h = _pendingImageHeight > 0 ? _pendingImageHeight : 200;
            var bitmap = SvgRasterizer.RenderToBitmap(_pendingImageSvg, w, h);
            if (bitmap is null)
            {
                StatusText.Text = AppLocalization.Service.Format("ai.gen.failed", AppLocalization.Service[Lang.AiGenFailedPngRender]);
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            bitmap.Save(stream, new PngBitmapEncoderOptions());
        }
        catch (Exception ex)
        {
            StatusText.Text = AppLocalization.Service.Format("ai.gen.failed", ExternalText.FailureReason(ex));
        }
    }

    /// <summary>将生成的 SVG 写入资源目录并返回 AssetItem 关闭对话框。</summary>
    private async void OnAddToLibraryClicked(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_pendingImageSvg) || _busy) return;

        // AssetDirectory 为空（未打开项目）时落到持久产物目录（理由同 ai-icons）
        var assetDirectory = string.IsNullOrEmpty(AssetDirectory)
            ? AppPaths.AssetDir("ai-images")
            : AssetDirectory;

        try
        {
            Directory.CreateDirectory(assetDirectory);
            var fileName = $"ai_image_{DateTime.Now:yyyyMMdd_HHmmss}.svg";
            var filePath = Path.Combine(assetDirectory, fileName);
            await File.WriteAllTextAsync(filePath, _pendingImageSvg);

            Results = new List<AssetItem>
            {
                new()
                {
                    Name = AppLocalization.Service.Format(Lang.AiImageAssetName, _pendingImageWidth, _pendingImageHeight),
                    Type = AssetType.Svg,
                    FilePath = filePath,
                    SizeBytes = new FileInfo(filePath).Length,
                    Width = _pendingImageWidth,
                    Height = _pendingImageHeight,
                    ColorDepth = 0
                }
            };
            Close(Results);
        }
        catch (Exception ex)
        {
            StatusText.Text = AppLocalization.Service.Format("ai.gen.failed", ExternalText.FailureReason(ex));
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "icon" : cleaned;
    }
}

/// <summary>视觉风格变体下拉框选项。Variant 为 null 表示"自动多样"(轮转)。</summary>
public sealed record VariantOption(string Label, IconStyleVariant? Variant);
