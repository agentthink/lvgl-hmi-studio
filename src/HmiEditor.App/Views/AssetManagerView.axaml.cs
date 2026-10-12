using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using HmiEditor.Core.Models;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace HmiEditor.App.Views;

/// <summary>
/// 资源管理器面板（R2-T3）—— 管理图片/字体/语言资源。
/// 按 AssetType 分组展示，标记悬空引用（ReferenceCount==0），
/// 顶部显示资源预算概览（总字节数）。
/// </summary>
public partial class AssetManagerView : UserControl
{
    private AssetLibrary? _library;
    private readonly ObservableCollection<object> _items = new();

    /// <summary>
    /// 资源工作目录（MainWindow 注入，通常为 .hmi 解包目录）。
    /// 导入资源时文件复制到 &lt;目录&gt;/assets/ 下，确保资源随工程打包。
    /// </summary>
    public string? AssetDirectory { get; set; }

    /// <summary>资源选中事件 —— 通知宿主编辑选中的资源。</summary>
    public event EventHandler<AssetItem?>? AssetSelected;

    /// <summary>
    /// I6: SVG 资源应用请求 —— 双击 SVG 资源时触发。
    /// 宿主应将其形状应用到当前选中的 Svg 控件。
    /// </summary>
    public event EventHandler<AssetItem>? SvgApplyRequested;

    /// <summary>
    /// I12: 字体子集化分析请求 —— 双击字体资源时触发。
    /// 宿主(拥有项目引用)打开分析对话框。
    /// </summary>
    public event EventHandler<AssetItem>? FontSubsetRequested;

    /// <summary>
    /// T4.1/T4.2: AI 图标生成请求 —— 宿主打开生成对话框。
    /// </summary>
    public event EventHandler? AiIconRequested;

    public AssetManagerView()
    {
        InitializeComponent();
        AssetList.ItemsSource = _items;
        AssetList.DoubleTapped += OnAssetDoubleTapped;

        // 转发事件到 DockLayoutState 聚合器，确保 Dock 重建后仍能到达 MainWindow
        AssetSelected += (_, asset) => DockLayoutState.RaiseAssetSelected(this, asset);
        SvgApplyRequested += (_, asset) => DockLayoutState.RaiseSvgApplyRequested(this, asset);
        FontSubsetRequested += (_, asset) => DockLayoutState.RaiseFontSubsetRequested(this, asset);
        AiIconRequested += (_, _) => DockLayoutState.RaiseAiIconRequested(this, EventArgs.Empty);
    }

    private void OnAiIconClicked(object? sender, RoutedEventArgs e)
    {
        AiIconRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>加载资源库数据并刷新展示。</summary>
    public void LoadAssets(AssetLibrary library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        Refresh();
    }

    /// <summary>刷新资源列表（按 AssetType 分组，重排选中项）。</summary>
    public void Refresh()
    {
        var selectedId = (AssetList.SelectedItem is AssetRow ar) ? ar.Asset.Id : null;

        _items.Clear();

        if (_library is null)
        {
            UpdateBudget();
            return;
        }

        foreach (AssetType type in Enum.GetValues(typeof(AssetType)))
        {
            var group = _library.Assets
                .Where(a => a.Type == type)
                .OrderBy(a => a.Name)
                .ToList();
            if (group.Count == 0) continue;

            _items.Add(new AssetGroupHeader
            {
                Title = AppLocalization.Service.Format(Lang.AssetGroupHeader, GetTypeName(type), group.Count)
            });

            foreach (var asset in group)
                _items.Add(new AssetRow(asset));
        }

        // 恢复选中项
        if (selectedId is not null)
        {
            var target = _items.OfType<AssetRow>().FirstOrDefault(r => r.Asset.Id == selectedId);
            if (target is not null)
                AssetList.SelectedItem = target;
        }

        UpdateBudget();
    }

    // ── 选中 ──

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var row = AssetList.SelectedItem as AssetRow;
        AssetSelected?.Invoke(this, row?.Asset);
    }

    // ── 导入占位 ──

    // ── 图片导入 ──

    /// <summary>
    /// 将资源源文件复制到资源工作目录的 assets/ 子目录，返回可用的 FilePath。
    /// 工作目录未设置或复制失败时回退原路径。
    /// </summary>
    private string CopyToAssetDirectory(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(AssetDirectory) || string.IsNullOrWhiteSpace(sourcePath))
            return sourcePath;

        try
        {
            var assetsDir = System.IO.Path.Combine(AssetDirectory, "assets");
            System.IO.Directory.CreateDirectory(assetsDir);
            var fileName = $"{Guid.NewGuid():N}"[..8] + System.IO.Path.GetExtension(sourcePath);
            var dest = System.IO.Path.Combine(assetsDir, fileName);
            System.IO.File.Copy(sourcePath, dest, overwrite: true);
            return dest;
        }
        catch
        {
            return sourcePath;
        }
    }

    private async void OnImportImage(object? sender, RoutedEventArgs e)
    {
        if (_library is null)
        {
            StatusText.Text = AppLocalization.Service["asset.create.or.open"];
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AppLocalization.Service[Lang.AssetImportImgTitle],
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(AppLocalization.Service[Lang.AssetFiletypeImg])
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                },
                new FilePickerFileType(AppLocalization.Service[Lang.AssetFiletypeAll])
                {
                    Patterns = new[] { "*" }
                }
            }
        });

        if (files.Count == 0) return;

        // 承接变量（#61b / 阶段 86 同款做法）：失败点落在**某一个**文件上，而 catch 在
        // foreach 之外 ⇒ 路径必须提到 try 外才拿得到（写在 try 内会 CS0103）。
        // ⚠ 别用 null 糊过去 —— 那就把「带上对象」又丢了（UiOperationGuardContractTests 盯着）。
        string? currentPath = null;
        try
        {
            var added = 0;
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                currentPath = path;   // 必须在任何可能抛出的语句**之前**
                var name = System.IO.Path.GetFileNameWithoutExtension(path);

                // 读取像素尺寸（解析失败时保持 0，不影响导入）
                var width = 0;
                var height = 0;
                try
                {
                    using var bmp = new Avalonia.Media.Imaging.Bitmap(path);
                    width = bmp.PixelSize.Width;
                    height = bmp.PixelSize.Height;
                }
                catch
                {
                    // 忽略尺寸解析失败
                }

                _library.AddAsset(new AssetItem
                {
                    Name = name,
                    Type = AssetType.Image,
                    FilePath = CopyToAssetDirectory(path),
                    SizeBytes = new System.IO.FileInfo(path).Length,
                    Width = width,
                    Height = height,
                    ColorDepth = 32
                });
                added++;
            }

            Refresh();
            StatusText.Text = AppLocalization.Service.Format(Lang.AssetImgImported, added);
        }
        catch (Exception ex)
        {
            // 批量档（#61a 尾巴，阶段 101）：旧登记写「一次导入多个文件 ⇒ 那个文件结构上
            // 不存在」——**实测不成立**：path 就在 foreach 里，承接变量一提就拿到。
            // 本 try 里真会抛的是 new FileInfo(path).Length（BCL IO，有码；
            // CopyToAssetDirectory 内部已静默吞、尺寸解析另有内层 catch）
            // ⇒ 与同文件 SVG／字体两档同款三实参出口（阶段 86）。
            StatusText.Text = UiOperationGuard.FailureDetail(Lang.UiOpImportImage, currentPath, ex);
        }
    }

    // ── I6: SVG 导入与应用 ──

    private async void OnImportSvg(object? sender, RoutedEventArgs e)
    {
        if (_library is null)
        {
            StatusText.Text = AppLocalization.Service["asset.create.or.open"];
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AppLocalization.Service[Lang.AssetImportSvgTitle],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(AppLocalization.Service[Lang.AssetFiletypeSvg])
                {
                    Patterns = new[] { "*.svg" }
                },
                new FilePickerFileType(AppLocalization.Service[Lang.AssetFiletypeAll])
                {
                    Patterns = new[] { "*" }
                }
            }
        });

        if (files.Count != 1) return;

        try
        {
            var path = files[0].Path.LocalPath;
            var svgText = await System.IO.File.ReadAllTextAsync(path);
            var (doc, error) = SvgParser.Parse(svgText);

            if (doc == null || doc.Shapes.Count == 0)
            {
                // 返回值档（登记 #61a，阶段 87）：走 ExternalText.Describe，不直通 error.Message。
                StatusText.Text = AppLocalization.Service.Format("asset.svg.parse.fail",
                    error is null
                        ? AppLocalization.Service[Lang.AssetSvgNoShapes]
                        : ExternalText.Describe(error));
                return;
            }

            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var item = new AssetItem
            {
                Name = name,
                Type = AssetType.Svg,
                FilePath = CopyToAssetDirectory(path),
                SizeBytes = new System.IO.FileInfo(path).Length,
                Width = (int)Math.Ceiling(doc.Width),
                Height = (int)Math.Ceiling(doc.Height),
                ColorDepth = 0
            };
            _library.AddAsset(item);
            Refresh();
            StatusText.Text = AppLocalization.Service.Format(Lang.AssetSvgImported, name, doc.Shapes.Count);
        }
        catch (Exception ex)
        {
            StatusText.Text = UiOperationGuard.FailureDetail(Lang.UiOpImportSvg, files[0].Path.LocalPath, ex);
        }
    }

    private void OnAssetDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (AssetList.SelectedItem is not AssetRow row) return;

        if (row.Asset.Type == AssetType.Svg)
        {
            SvgApplyRequested?.Invoke(this, row.Asset);
        }
        else if (row.Asset.Type == AssetType.Font)
        {
            FontSubsetRequested?.Invoke(this, row.Asset);
        }
    }

    // ── I12: 字体导入 + 子集化分析 ──

    private async void OnImportFont(object? sender, RoutedEventArgs e)
    {
        if (_library is null)
        {
            StatusText.Text = AppLocalization.Service["asset.create.or.open"];
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AppLocalization.Service[Lang.AssetImportFontTitle],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(AppLocalization.Service[Lang.AssetFiletypeFont])
                {
                    Patterns = new[] { "*.ttf", "*.otf", "*.ttc" }
                },
                new FilePickerFileType(AppLocalization.Service[Lang.AssetFiletypeAll])
                {
                    Patterns = new[] { "*" }
                }
            }
        });

        if (files.Count != 1) return;

        try
        {
            var path = files[0].Path.LocalPath;
            var name = System.IO.Path.GetFileNameWithoutExtension(path);

            var item = new AssetItem
            {
                Name = name,
                Type = AssetType.Font,
                FilePath = CopyToAssetDirectory(path),
                SizeBytes = new System.IO.FileInfo(path).Length,
                Width = 0,
                Height = 0,
                ColorDepth = 0
            };
            _library.AddAsset(item);
            Refresh();
            StatusText.Text = AppLocalization.Service.Format(Lang.AssetFontImported, name, AssetRow.FormatBytes(item.SizeBytes));
        }
        catch (Exception ex)
        {
            StatusText.Text = UiOperationGuard.FailureDetail(Lang.UiOpImportFont, files[0].Path.LocalPath, ex);
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        Refresh();
        StatusText.Text = AppLocalization.Service[Lang.AssetRefreshed];
    }

    // ── 资源预算概览 ──

    private void UpdateBudget()
    {
        var assets = (_library?.Assets ?? Enumerable.Empty<AssetItem>()).ToList();
        var total = assets.Sum(a => a.SizeBytes);
        var count = assets.Count;
        var orphans = assets.Count(a => a.ReferenceCount <= 0);

        var orphanNote = orphans > 0 ? AppLocalization.Service.Format(Lang.AssetOrphanNote, orphans) : "";
        BudgetText.Text = AppLocalization.Service.Format("asset.budget", count, AssetRow.FormatBytes(total)) + orphanNote;
    }

    // ── 辅助 ──

    private static string GetTypeName(AssetType type) => type switch
    {
        AssetType.Image => AppLocalization.Service[Lang.AssetTypeImage],
        AssetType.Font => AppLocalization.Service[Lang.AssetTypeFont],
        AssetType.Language => AppLocalization.Service[Lang.AssetTypeLanguage],
        AssetType.Svg => AppLocalization.Service[Lang.AssetTypeSvg],
        _ => type.ToString()
    };
}

/// <summary>分组标题行（视图模型，供 XAML DataTemplate 按类型选择）。</summary>
public class AssetGroupHeader
{
    public string Title { get; set; } = "";
}

/// <summary>资源展示行（视图模型），携带原始 AssetItem 引用。</summary>
public class AssetRow
{
    private static readonly IBrush OrphanBrush = HmiEditor.App.Theme.Warning;
    private static readonly IBrush NormalRefBrush = HmiEditor.App.Theme.TextSecondary;

    public AssetItem Asset { get; }
    public string Name { get; }
    public string DimsDisplay { get; }
    public string DepthDisplay { get; }
    public string SizeDisplay { get; }
    public string RefDisplay { get; }
    public IBrush RefBrush { get; }

    public AssetRow(AssetItem asset)
    {
        Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        Name = asset.Name;

        DimsDisplay = asset.Type == AssetType.Image && (asset.Width > 0 || asset.Height > 0)
            ? $"{asset.Width}×{asset.Height}"
            : "—";

        DepthDisplay = asset.ColorDepth > 0 ? $"{asset.ColorDepth}bit" : "—";
        SizeDisplay = FormatBytes(asset.SizeBytes);

        var isOrphan = asset.ReferenceCount <= 0;
        RefDisplay = isOrphan ? AppLocalization.Service[Lang.AssetRefOrphan] : AppLocalization.Service.Format(Lang.AssetRefCount, asset.ReferenceCount);
        RefBrush = isOrphan ? OrphanBrush : NormalRefBrush;
    }

    /// <summary>字节数格式化（B / KB / MB）。</summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F2} MB";
    }
}
