using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using HmiEditor.App.Controls;
using HmiEditor.App.Design;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App;
using HmiEditor.App.Views.Editors;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace HmiEditor.App.Views;

public partial class PropertyPanelView : UserControl
{
    // ── 图片 / SVG 资源选择 ──

    /// <summary>填充图片资源下拉（来自全局 AssetLibrary 的 Image 资产），按当前引用恢复选中。</summary>
    private void PopulateImageCombo(string? selectedId)
    {
        ImageAssetCombo.Items.Clear();
        ImageAssetCombo.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service[Lang.PropParamImageNone],
            Tag = (string?)null
        });

        var library = DockLayoutState.AssetLibrary;
        if (library != null)
        {
            foreach (var asset in library.Assets.Where(a => a.Type == AssetType.Image))
            {
                ImageAssetCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{asset.Name} ({asset.Width}x{asset.Height})",
                    Tag = asset.Id
                });
            }
        }
        SelectComboByTag(ImageAssetCombo, selectedId, 0);
    }

    // ── 屏幕背景图片 ──

    /// <summary>填充屏幕背景图片下拉（来自全局 AssetLibrary 的 Image 资产），按当前引用恢复选中。</summary>
    private void PopulateScreenBgImageCombo(string? selectedId)
    {
        ScreenBgImageCombo.Items.Clear();
        ScreenBgImageCombo.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service[Lang.PropScreenBgImageNone],
            Tag = (string?)null
        });

        var library = DockLayoutState.AssetLibrary;
        if (library != null)
        {
            foreach (var asset in library.Assets.Where(a => a.Type == AssetType.Image))
            {
                ScreenBgImageCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{asset.Name} ({asset.Width}x{asset.Height})",
                    Tag = asset.Id
                });
            }
        }
        SelectComboByTag(ScreenBgImageCombo, selectedId, 0);
    }

    /// <summary>屏幕背景图片下拉变更 → 写回并刷新画布。</summary>
    private void OnScreenBgImageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || CurrentScreen == null || Parent == null) return;
        SaveScreenProperties();
    }

    /// <summary>清除屏幕背景图片 → 写回并刷新画布。</summary>
    private void OnClearScreenBgImageClicked(object? sender, RoutedEventArgs e)
    {
        if (_isUpdating || CurrentScreen == null) return;
        ScreenBgImageCombo.SelectedIndex = 0;
        SaveScreenProperties();
    }

    /// <summary>
    /// 从本地文件系统选择图片作为屏幕背景：导入资源库 → 选中并刷新画布。
    /// </summary>
    private async void OnChooseScreenBgImageClicked(object? sender, RoutedEventArgs e)
    {
        if (CurrentScreen == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AppLocalization.Service[Lang.PropScreenBgImageChoose],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(AppLocalization.Service[Lang.PropScreenBgImageChoose])
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

        var path = files[0].Path.LocalPath;
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

        var library = DockLayoutState.AssetLibrary;
        if (library == null) return;

        library.AddAsset(new AssetItem
        {
            Name = name,
            Type = AssetType.Image,
            FilePath = path,
            SizeBytes = new System.IO.FileInfo(path).Length,
            Width = width,
            Height = height,
            ColorDepth = 32
        });

        // 刷新下拉并选中新导入的图片（抑制中间事件，随后统一保存）
        _isUpdating = true;
        try { PopulateScreenBgImageCombo(library.Assets[^1].Id); }
        finally { _isUpdating = false; }
        SaveScreenProperties();
    }

    /// <summary>填充 SVG 资源下拉（来自全局 AssetLibrary 的 Svg 资产）。</summary>
    private void PopulateSvgCombo()
    {
        SvgAssetCombo.Items.Clear();
        SvgAssetCombo.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service[Lang.PropParamSvgNone],
            Tag = (string?)null
        });

        var library = DockLayoutState.AssetLibrary;
        if (library != null)
        {
            foreach (var asset in library.Assets.Where(a => a.Type == AssetType.Svg))
            {
                SvgAssetCombo.Items.Add(new ComboBoxItem
                {
                    Content = $"{asset.Name} ({asset.Width}x{asset.Height})",
                    Tag = asset.Id
                });
            }
        }
        SvgAssetCombo.SelectedIndex = 0;
    }

    /// <summary>Image 控件选择图片资源 → 立即写入 ImageAssetId 并刷新画布。</summary>
    private void OnImageAssetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _currentControl == null || !ImageAssetCombo.IsVisible || Parent == null) return;
        _currentControl.ImageAssetId = GetComboTag(ImageAssetCombo);
        PropertyChanged?.Invoke(this, _currentControl);
    }

    /// <summary>Svg 控件选择 SVG 资源 → 解析形状数据写入控件并刷新画布。</summary>
    private async void OnSvgAssetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _currentControl == null || !SvgAssetCombo.IsVisible || Parent == null) return;

        var tag = GetComboTag(SvgAssetCombo);
        if (string.IsNullOrEmpty(tag))
        {
            _currentControl.SvgShapes = null;
            PropertyChanged?.Invoke(this, _currentControl);
            return;
        }

        var asset = DockLayoutState.AssetLibrary?.Assets.FirstOrDefault(a => a.Id == tag);
        if (asset == null) return;

        try
        {
            var svgText = await System.IO.File.ReadAllTextAsync(asset.FilePath);
            var (doc, error) = SvgParser.Parse(svgText);
            if (doc == null || doc.Shapes.Count == 0)
            {
                return;
            }
            _currentControl.SvgShapes = doc.Shapes;
            _currentControl.SvgViewBoxWidth = doc.ViewBoxWidth;
            _currentControl.SvgViewBoxHeight = doc.ViewBoxHeight;
            PropertyChanged?.Invoke(this, _currentControl);
        }
        catch
        {
            // 解析失败保持原形状，不打断编辑
        }
    }
}
