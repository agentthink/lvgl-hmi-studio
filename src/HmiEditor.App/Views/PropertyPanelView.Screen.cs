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
    // ═══════════════════════════════════════════════
    //  屏幕页面属性（未选中控件时显示，参考 Figma/Sketch/XD）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 绑定当前屏幕到属性面板。由 MainWindow 在切换屏幕时设置 <see cref="CurrentScreen"/>，
    /// 画布空白区域点击（BindControl(null)）时自动切换到屏幕属性。
    /// </summary>
    public void BindScreen(HmiScreen screen)
    {
        _isUpdating = true;
        try
        {
            ScreenName.Text = screen.Name;
            ScreenWidth.Value = screen.Width;
            ScreenHeight.Value = screen.Height;
            ScreenBgColor.Text = screen.BgColor ?? "";
            PopulateScreenBgImageCombo(screen.BgImageAssetId);
            SelectComboByTag(ScreenBgImageModeCombo, screen.BgImageMode.ToString());
            ScreenBgImageOpacity.Value = screen.BgImageOpacity;
            PopulateScreenPresets(screen);
            SelectComboByTag(ScreenOrientationCombo, screen.Width >= screen.Height ? "Landscape" : "Portrait");
            SelectComboByTag(ScreenAdaptiveCombo, screen.AdaptiveMode.ToString());
            ScreenDpi.Value = _currentProject?.ScreenDefaultsConfig.DpiScale ?? 1.0;
            var pad = _currentProject?.ScreenDefaultsConfig.SafeAreaPadding;
            SafeLeft.Value = pad?.Left ?? 0;
            SafeTop.Value = pad?.Top ?? 0;
            SafeRight.Value = pad?.Right ?? 0;
            SafeBottom.Value = pad?.Bottom ?? 0;
            ScreenIsBoot.IsChecked = _currentProject?.RuntimeConfig.BootScreenId == screen.Id;
            ScreenIsPublic.IsChecked = screen.IsPublicScreen;
            ScreenPublicAccessCombo.IsEnabled = screen.IsPublicScreen;
            SelectComboByTag(ScreenPublicAccessCombo, screen.PublicAccess.ToString());
            PopulateScreenLayerRefs(screen);
            IsEnabled = true;
            UpdateScreenSwatches();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>填充屏幕尺寸预设下拉（含"自定义"兜底项）。</summary>
    private void PopulateScreenPresets(HmiScreen screen)
    {
        var presets = new (int W, int H, string Name)[]
        {
            (480,  272,  "480 × 272"),
            (640,  360,  "640 × 360"),
            (800,  480,  "800 × 480"),
            (1024, 600,  "1024 × 600"),
            (1024, 768,  "1024 × 768"),
            (1280, 720,  "1280 × 720"),
            (1280, 800,  "1280 × 800"),
            (1920, 1080, "1920 × 1080"),
            (0,    0,    AppLocalization.Service[Lang.ScreenCustom]),
        };

        ScreenPresetCombo.Items.Clear();
        ComboBoxItem? match = null;
        foreach (var p in presets)
        {
            var item = new ComboBoxItem { Content = p.Name, Tag = p };
            ScreenPresetCombo.Items.Add(item);
            if (p.W == (int)screen.Width && p.H == (int)screen.Height)
                match = item;
        }
        ScreenPresetCombo.SelectedItem = match ?? ScreenPresetCombo.Items[^1];
    }

    /// <summary>
    /// 重建"引用图层"选择区：列出所有公共页面（排除自身与已嵌套图层的页面）。
    /// 公共页面自身不能引用图层（仅支持一层嵌套）。
    /// </summary>
    private void PopulateScreenLayerRefs(HmiScreen screen)
    {
        ScreenLayerRefsSection.IsVisible = !screen.IsPublicScreen;
        ScreenLayerRefsPanel.Children.Clear();
        if (screen.IsPublicScreen || _currentProject == null) return;

        var candidates = _currentProject.Screens
            .Where(s => s.IsPublicScreen
                        && s.Id != screen.Id
                        && s.ReferencedLayerIds.Count == 0)
            .ToList();

        foreach (var candidate in candidates)
        {
            var cb = new CheckBox
            {
                Content = candidate.Name,
                Tag = candidate.Id,
                IsChecked = screen.ReferencedLayerIds.Contains(candidate.Id),
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 12
            };
            cb.IsCheckedChanged += (_, _) =>
            {
                if (_isUpdating) return;
                if (cb.IsChecked == true)
                {
                    if (!screen.ReferencedLayerIds.Contains(candidate.Id))
                        screen.ReferencedLayerIds.Add(candidate.Id);
                }
                else
                {
                    screen.ReferencedLayerIds.Remove(candidate.Id);
                }
                ScreenPropertyChanged?.Invoke(this, screen);
            };
            ScreenLayerRefsPanel.Children.Add(cb);
        }
    }

    /// <summary>按 Tag 匹配选中 ComboBox 项（找不到时不改动）。</summary>
    private static void SelectComboByTag(ComboBox combo, string? tag)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    /// <summary>屏幕尺寸预设选择 → 应用宽高。</summary>
    private void OnScreenPresetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || CurrentScreen == null || Parent == null) return;
        if (ScreenPresetCombo.SelectedItem is ComboBoxItem item && item.Tag is (int w, int h, _) && w > 0 && h > 0)
        {
            _isUpdating = true;
            ScreenWidth.Value = w;
            ScreenHeight.Value = h;
            _isUpdating = false;
            OnScreenPropertyChanged(sender, e);
        }
    }

    /// <summary>方向切换 → 互换宽高。</summary>
    private void OnScreenOrientationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || CurrentScreen == null || Parent == null) return;
        if (ScreenOrientationCombo.SelectedItem is ComboBoxItem item)
        {
            var orientation = item.Tag?.ToString();
            var wantsLandscape = orientation == "Landscape";
            var isLandscape = CurrentScreen.Width >= CurrentScreen.Height;
            if (wantsLandscape != isLandscape)
            {
                _isUpdating = true;
                var w = ScreenHeight.Value;
                ScreenWidth.Value = ScreenHeight.Value;
                ScreenHeight.Value = w;
                _isUpdating = false;
                OnScreenPropertyChanged(sender, e);
            }
        }
    }

    /// <summary>屏幕 FANumberBox 数值变更统一入口。</summary>
    private void OnScreenNumberChanged()
    {
        if (_isUpdating || CurrentScreen == null) return;
        SaveScreenProperties();
    }

    /// <summary>屏幕属性（文本/下拉/复选框）变更 → 保存。</summary>
    private void OnScreenPropertyChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdating || CurrentScreen == null || Parent == null) return;
        SaveScreenProperties();
    }

    /// <summary>将屏幕属性面板的值写回 HmiScreen 并触发刷新。</summary>
    private void SaveScreenProperties()
    {
        if (CurrentScreen == null) return;

        CurrentScreen.Name = PropertyValidator.SanitizeName(ScreenName.Text, CurrentScreen.Name);
        CurrentScreen.Width = (int)PropertyValidator.ClampSize(ScreenWidth.Value);
        CurrentScreen.Height = (int)PropertyValidator.ClampSize(ScreenHeight.Value);
        CurrentScreen.BgColor = PropertyValidator.SanitizeColor(ScreenBgColor.Text);
        CurrentScreen.BgImageAssetId = GetComboTag(ScreenBgImageCombo);
        CurrentScreen.BgImageMode = GetComboTag(ScreenBgImageModeCombo) switch
        {
            "Tile" => ScreenBgImageMode.Tile,
            "Center" => ScreenBgImageMode.Center,
            _ => ScreenBgImageMode.Stretch
        };
        CurrentScreen.BgImageOpacity = (int)(ScreenBgImageOpacity.Value);

        if (ScreenAdaptiveCombo.SelectedItem is ComboBoxItem adaptiveItem)
        {
            CurrentScreen.AdaptiveMode = adaptiveItem.Tag?.ToString() switch
            {
                "Fit" => ScreenAdaptiveMode.Fit,
                "Fill" => ScreenAdaptiveMode.Fill,
                _ => ScreenAdaptiveMode.None
            };
        }

        // DPI / 安全区域存储于工程级默认模板（模型中无屏幕级字段）
        if (_currentProject != null)
        {
            _currentProject.ScreenDefaultsConfig.DpiScale = ScreenDpi.Value;
            var pad = _currentProject.ScreenDefaultsConfig.SafeAreaPadding
                      ?? (_currentProject.ScreenDefaultsConfig.SafeAreaPadding = new ScreenPadding());
            pad.Left = SafeLeft.Value;
            pad.Top = SafeTop.Value;
            pad.Right = SafeRight.Value;
            pad.Bottom = SafeBottom.Value;
        }

        // 设为启动屏幕
        if (_currentProject != null)
        {
            _currentProject.RuntimeConfig.BootScreenId = ScreenIsBoot.IsChecked == true
                ? CurrentScreen.Id
                : (_currentProject.RuntimeConfig.BootScreenId == CurrentScreen.Id ? null : _currentProject.RuntimeConfig.BootScreenId);
        }

        // 公共页面：多用户共享查看 + 访问权限控制
        CurrentScreen.IsPublicScreen = ScreenIsPublic.IsChecked == true;
        if (ScreenPublicAccessCombo.SelectedItem is ComboBoxItem accessItem)
        {
            CurrentScreen.PublicAccess = accessItem.Tag?.ToString() switch
            {
                "Public" => ScreenPublicAccess.Public,
                "AdminOnly" => ScreenPublicAccess.AdminOnly,
                _ => ScreenPublicAccess.Viewers
            };
        }
        ScreenPublicAccessCombo.IsEnabled = CurrentScreen.IsPublicScreen;

        // 公共页面变更后刷新"引用图层"可见性（公共页面自身不能引用图层）
        PopulateScreenLayerRefs(CurrentScreen);

        UpdateScreenSwatches();
        ScreenPropertyChanged?.Invoke(this, CurrentScreen);
    }

    private void UpdateScreenSwatches()
    {
        try
        {
            if (!string.IsNullOrEmpty(ScreenBgColor.Text))
            {
                var hex = ScreenBgColor.Text.StartsWith('#') ? ScreenBgColor.Text : "#" + ScreenBgColor.Text;
                SwatchScreenBg.Background = new SolidColorBrush(Color.Parse(hex));
            }
        }
        catch
        {
            // 忽略无效颜色值
        }
    }
}
