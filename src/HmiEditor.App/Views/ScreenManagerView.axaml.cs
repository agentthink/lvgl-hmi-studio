using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App;

namespace HmiEditor.App.Views;

/// <summary>
/// 屏幕管理器对话框 —— 列出所有屏幕，支持新建/删除/切换。
/// </summary>
public partial class ScreenManagerView : ChromeWindow
{
    private readonly ObservableCollection<HmiScreen> _screens;

    /// <summary>
    /// 用户选择了要切换到的屏幕。
    /// </summary>
    public event EventHandler<HmiScreen>? ScreenSwitchRequested;

    /// <summary>批量更新 UI 时抑制变更事件。</summary>
    private bool _isUpdating;

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public ScreenManagerView()
    {
        _screens = null!;
        InitializeComponent();
    }

    public ScreenManagerView(HmiProject project, HmiScreen currentScreen) : this()
    {
        _screens = project.Screens;
        RefreshList(currentScreen);

        BtnAddScreen.Click += (_, _) => OnAddScreen(project);
        BtnSwitchScreen.Click += (_, _) => OnSwitchScreen();

        // 屏幕属性：宽/高
        NumScreenWidth.ValueChanged += (_, _) => OnSizeChanged();
        NumScreenHeight.ValueChanged += (_, _) => OnSizeChanged();

        // I9: 自适应模式选择 → 保存到选中屏幕
        CboAdaptiveMode.SelectionChanged += (_, _) => OnAdaptiveModeChanged();
    }

    /// <summary>I9: 将模式变更写回选中屏幕并刷新列表标注。</summary>
    private void OnAdaptiveModeChanged()
    {
        // 🩸 `_isUpdating`：挡住 `OnSelectionChanged()` 里那句**程序性**写入（详见那里的注释）。
        //   阶段 115 之前这里没有守卫 —— 与 `OnPublicChanged` 不对称，而后果比它重得多：
        //   本处理器末尾会 `RefreshList()`，也就是 `ScreenList.Items.Clear()`，
        //   而那一下可能正落在**处理 ScreenList 选中变化的中途** ⇒ 重入清空 ⇒
        //   `ArgumentOutOfRangeException`（第五层 `SelMoves("ScreenList", 1)` 首跑实测）。
        if (_isUpdating) return;
        if (ScreenList.SelectedItem is not ListBoxItem item || item.Tag is not HmiScreen screen)
            return;

        var tag = (CboAdaptiveMode.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        screen.AdaptiveMode = tag switch
        {
            "Fit" => ScreenAdaptiveMode.Fit,
            "Fill" => ScreenAdaptiveMode.Fill,
            _ => ScreenAdaptiveMode.None
        };
        StatusText.Text = AppLocalization.Service.Format(Lang.ScreenStatusAdaptive, screen.Name, screen.AdaptiveMode);
        RefreshList(screen);
    }

    /// <summary>
    /// 公共页面设置变更 → 写回选中屏幕（多用户共享查看 + 访问权限）。
    /// </summary>
    private void OnPublicChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdating) return;
        if (ScreenList.SelectedItem is not ListBoxItem item || item.Tag is not HmiScreen screen)
            return;

        screen.IsPublicScreen = ChkPublic.IsChecked == true;
        if (screen.IsPublicScreen)
        {
            var tag = (CboPublicAccess.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            screen.PublicAccess = tag switch
            {
                "Public" => ScreenPublicAccess.Public,
                "AdminOnly" => ScreenPublicAccess.AdminOnly,
                _ => ScreenPublicAccess.Viewers
            };
        }
        CboPublicAccess.IsEnabled = screen.IsPublicScreen;

        StatusText.Text = screen.IsPublicScreen
            ? AppLocalization.Service.Format(Lang.ScreenStatusPublic, screen.Name)
            : "";
        RefreshList(screen);
    }

    /// <summary>将宽/高变更写回选中屏幕并刷新列表标注。</summary>
    private void OnSizeChanged()
    {
        if (_isUpdating) return;
        if (ScreenList.SelectedItem is not ListBoxItem item || item.Tag is not HmiScreen screen)
            return;

        screen.Width = (int)(NumScreenWidth.Value ?? 0);
        screen.Height = (int)(NumScreenHeight.Value ?? 0);
        RefreshList(screen);
    }

    private void RefreshList(HmiScreen? selectedScreen = null)
    {
        ScreenList.Items.Clear();

        // 被引用为图层的公共页面集合（用于列表徽标标注）
        var referenced = _screens.SelectMany(s => s.ReferencedLayerIds).ToHashSet();

        foreach (var screen in _screens)
        {
            var item = CreateScreenListItem(screen, referenced);
            ScreenList.Items.Add(item);

            if (selectedScreen != null && screen.Id == selectedScreen.Id)
            {
                ScreenList.SelectedItem = item;
            }
        }

        EmptyHint.IsVisible = _screens.Count == 0;
        BtnSwitchScreen.IsVisible = _screens.Count > 0;
    }

    private ListBoxItem CreateScreenListItem(HmiScreen screen, HashSet<string> referencedLayerIds)
    {
        var nameText = new TextBlock
        {
            Text = screen.Name,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        // 公共页面徽标（名称旁蓝色小标签）
        var nameRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6
        };
        nameRow.Children.Add(nameText);
        if (screen.IsPublicScreen)
        {
            nameRow.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(36, 108, 191)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = AppLocalization.Service[Lang.ScreenPublicItemBadge],
                    FontSize = 10,
                    Foreground = Brushes.White
                }
            });
        }
        if (referencedLayerIds.Contains(screen.Id))
        {
            nameRow.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = AppLocalization.Service[Lang.ScreenLayerBadge],
                    FontSize = 10,
                    Foreground = Brushes.White
                }
            });
        }

        var infoText = new TextBlock
        {
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 11,
            Text = AppLocalization.Service.Format(Lang.ScreenItemInfo, screen.Width, screen.Height, screen.Controls.Count) +
                   (screen.AdaptiveMode != ScreenAdaptiveMode.None
                       ? AppLocalization.Service.Format(Lang.ScreenItemAdaptive, screen.AdaptiveMode)
                       : "") +
                   (screen.IsPublicScreen
                       ? " · " + PublicAccessLabel(screen.PublicAccess)
                       : "")
        };

        var thumbnail = new Border
        {
            Width = 64,
            Height = 40,
            Background = HmiEditor.App.Theme.BgDeep,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 12, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = HmiEditor.App.Theme.InputBorder,
        };

        var deleteBtn = new Button
        {
            Content = "✕",
            Background = new SolidColorBrush(Color.FromRgb(59, 31, 31)),
            Foreground = HmiEditor.App.Theme.Danger,
            BorderThickness = new Thickness(0),
            Width = 28,
            Height = 28,
            FontSize = 14,
            Tag = screen,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        deleteBtn.Click += (_, _) => OnDeleteScreen(screen);

        var infoPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children = { nameRow, infoText }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
            Margin = new Thickness(12, 8)
        };
        grid.Children.Add(thumbnail);
        Grid.SetColumn(thumbnail, 0);
        grid.Children.Add(infoPanel);
        Grid.SetColumn(infoPanel, 1);
        grid.Children.Add(deleteBtn);
        Grid.SetColumn(deleteBtn, 3);

        return new ListBoxItem
        {
            Content = grid,
            Tag = screen,
            Padding = new Thickness(0)
        };
    }

    private void OnAddScreen(HmiProject project)
    {
        // 统一走 ScreenFactory：尺寸取自 ScreenDefaultsConfig，同时把
        // 自适应模式/背景色一并带上（此前这里只抄了宽高，另两个字段被漏掉）。
        var newScreen = ScreenFactory.Create(project);
        _screens.Add(newScreen);
        RefreshList(newScreen);
        StatusText.Text = AppLocalization.Service.Format(Lang.ScreenCreated, newScreen.Name);
    }

    private void OnDeleteScreen(HmiScreen screen)
    {
        if (_screens.Count <= 1)
        {
            StatusText.Text = AppLocalization.Service["screen.minCount"];
            return;
        }

        _screens.Remove(screen);
        RefreshList(_screens.FirstOrDefault());
        StatusText.Text = AppLocalization.Service.Format("screen.deleted", screen.Name);
    }

    private void OnSwitchScreen()
    {
        if (ScreenList.SelectedItem is ListBoxItem item && item.Tag is HmiScreen screen)
        {
            ScreenSwitchRequested?.Invoke(this, screen);
            Close();
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        BtnSwitchScreen.IsEnabled = ScreenList.SelectedItem != null;

        // 加载选中屏幕的宽高到输入框
        if (ScreenList.SelectedItem is ListBoxItem item && item.Tag is HmiScreen screen)
        {
            _isUpdating = true;
            NumScreenWidth.Value = (decimal)screen.Width;
            NumScreenHeight.Value = (decimal)screen.Height;
            _isUpdating = false;

            // I9: 加载选中屏幕的自适应模式到下拉框
            var targetIndex = screen.AdaptiveMode switch
            {
                ScreenAdaptiveMode.Fit => 1,
                ScreenAdaptiveMode.Fill => 2,
                _ => 0
            };
            // 加载公共页面设置（开关 + 访问级别）
            // 🩸 下面一整段都是**程序性同步**（把选中屏的属性载入控件），必须整体落在 `_isUpdating`
            //   窗口内 —— 包括那句 `CboAdaptiveMode.SelectedIndex`：阶段 115 之前它被放在窗口**外面**
            //   （与 `OnPublicChanged` 的两句刚好相反），于是它会真的触发 `OnAdaptiveModeChanged`
            //   → `RefreshList()` → `ScreenList.Items.Clear()`，而那一下正在 `ScreenList` 自己的
            //   选中变化处理中途 ⇒ 重入清空 ⇒ 抛 `ArgumentOutOfRangeException`。
            _isUpdating = true;
            if (CboAdaptiveMode.SelectedIndex != targetIndex)
                CboAdaptiveMode.SelectedIndex = targetIndex;
            ChkPublic.IsChecked = screen.IsPublicScreen;
            CboPublicAccess.IsEnabled = screen.IsPublicScreen;
            var accessIndex = screen.PublicAccess switch
            {
                ScreenPublicAccess.Public => 0,
                ScreenPublicAccess.AdminOnly => 2,
                _ => 1
            };
            if (CboPublicAccess.SelectedIndex != accessIndex)
                CboPublicAccess.SelectedIndex = accessIndex;
            _isUpdating = false;

            // 加载引用图层配置
            RefreshLayerRefs(screen);
        }
    }

    /// <summary>
    /// 重建"引用图层"选择区：列出所有公共页面（排除自身与已嵌套图层的页面）。
    /// 公共页面自身不能引用图层（仅支持一层嵌套）。
    /// </summary>
    private void RefreshLayerRefs(HmiScreen screen)
    {
        LayerRefsPanel.Children.Clear();
        LayerRefsHint.IsVisible = !screen.IsPublicScreen;
        if (screen.IsPublicScreen) return;

        var candidates = _screens
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
            cb.IsCheckedChanged += OnLayerRefToggled;
            LayerRefsPanel.Children.Add(cb);
        }
    }

    /// <summary>引用图层开关变更 → 写回选中屏幕的 ReferencedLayerIds。</summary>
    private void OnLayerRefToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdating) return;
        if (ScreenList.SelectedItem is not ListBoxItem item || item.Tag is not HmiScreen screen) return;
        if (sender is not CheckBox cb || cb.Tag is not string layerId) return;

        if (cb.IsChecked == true)
        {
            if (!screen.ReferencedLayerIds.Contains(layerId))
                screen.ReferencedLayerIds.Add(layerId);
        }
        else
        {
            screen.ReferencedLayerIds.Remove(layerId);
        }

        StatusText.Text = AppLocalization.Service.Format(Lang.ScreenStatusLayers, screen.Name);
        RefreshList(screen);
    }

    /// <summary>公共页面访问级别的本地化标签。</summary>
    private static string PublicAccessLabel(ScreenPublicAccess access) => access switch
    {
        ScreenPublicAccess.Public => AppLocalization.Service[Lang.EnumPublicAccessPublic],
        ScreenPublicAccess.AdminOnly => AppLocalization.Service[Lang.EnumPublicAccessAdmin],
        _ => AppLocalization.Service[Lang.EnumPublicAccessViewers]
    };
}
