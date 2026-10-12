using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Threading;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Animation;
using HmiEditor.App.Resources;
using HmiEditor.App;
using HmiEditor.App.Controls;

namespace HmiEditor.App.Views;

/// <summary>
/// 动画编辑器对话框 —— 为控件创建和编辑 LVGL 动画效果。
/// 左右分栏设计：左侧动画列表 / 右侧编辑面板。
/// </summary>
public partial class AnimationEditorView : ChromeWindow
{
    private readonly ObservableCollection<AnimationConfig> _animations;
    private readonly HmiProject _project;
    private AnimationConfig? _currentAnimation;
    private AnimationKeyframe? _selectedKeyframe;
    private bool _isUpdating;
    private int _animationCounter;

    private readonly List<Border> _timelineDots = new();
    private readonly string[] _properties = { "X", "Y", "Width", "Height", "Opacity", "Radius", "BorderWidth", "Value" };
    private readonly string[] _triggers = { "OnLoad", "OnClick", "OnValueChange", "OnPress", "OnRelease" };

    // 时间轴交互状态
    private DispatcherTimer? _playbackTimer;
    private double _playheadPercent;
    private bool _isPlaying;
    private bool _isDraggingKeyframe;
    private AnimationKeyframe? _draggedKeyframe;
    private Line? _playheadLine;
    private TextBlock? _playheadValueLabel;
    private Canvas? _trackCanvas;

    /// <summary>
    /// 当动画列表发生变更时触发。
    /// </summary>
    public event EventHandler? AnimationListChanged;

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public AnimationEditorView()
    {
        _animations = null!;
        _project = null!;
        InitializeComponent();
    }

    public AnimationEditorView(ObservableCollection<AnimationConfig> animations, HmiProject project) : this()
    {
        _animations = animations;
        _project = project;

        PopulateComboBoxes();
        RefreshAnimationList();
        EditPanelVisible(false);
        KeyframeEditPanel.IsVisible = false;

        BtnNewAnimation.Click += (_, _) => OnNewAnimation();
        BtnDeleteAnimation.Click += (_, _) => OnDeleteAnimation();
        AnimationList.SelectionChanged += OnAnimationSelectionChanged;
        CmbTriggerFilter.SelectionChanged += (_, _) => RefreshAnimationList();

        BtnAddKeyframe.Click += (_, _) => OnAddKeyframe();
        BtnDeleteKeyframe.Click += (_, _) => OnDeleteKeyframe();
        BtnPlayPreview.Click += (_, _) => TogglePlayback();
        KeyframeList.SelectionChanged += OnKeyframeSelectionChanged;

        WireAnimationEditEvents();
        WireKeyframeEditEvents();

        Closed += (_, _) => StopPlayback();
    }

    // ═══════════════════════════════════════════════
    //  初始化
    // ═══════════════════════════════════════════════

    private void PopulateComboBoxes()
    {
        // 触发方式筛选
        CmbTriggerFilter.ItemsSource = new[] { AppLocalization.Service[Lang.AnimFilterAll], "OnLoad", "OnClick", "OnValueChange", "OnPress", "OnRelease" };
        CmbTriggerFilter.SelectedIndex = 0;

        // 动画属性
        CmbAnimationProperty.ItemsSource = _properties;
        CmbAnimationProperty.SelectedIndex = 4; // Opacity

        // 触发方式
        CmbTrigger.ItemsSource = _triggers;
        CmbTrigger.SelectedIndex = 0;

        // 缓动函数（中文选项）
        CmbPathType.ItemsSource = new[]
        {
            AppLocalization.Service[Lang.AnimEasingLinear],
            AppLocalization.Service[Lang.AnimEasingEaseInOut],
            AppLocalization.Service[Lang.AnimEasingEaseIn],
            AppLocalization.Service[Lang.AnimEasingEaseOut],
            AppLocalization.Service[Lang.AnimEasingElastic],
            AppLocalization.Service[Lang.AnimEasingBounce],
            AppLocalization.Service[Lang.AnimEasingStep]
        };
        CmbPathType.SelectedIndex = 1;

        // 关键帧缓动函数
        CmbKeyframePathType.ItemsSource = new[]
        {
            AppLocalization.Service[Lang.AnimEasingGlobal],
            AppLocalization.Service[Lang.AnimEasingLinear],
            AppLocalization.Service[Lang.AnimEasingEaseInOut],
            AppLocalization.Service[Lang.AnimEasingEaseIn],
            AppLocalization.Service[Lang.AnimEasingEaseOut],
            AppLocalization.Service[Lang.AnimEasingElastic],
            AppLocalization.Service[Lang.AnimEasingBounce],
            AppLocalization.Service[Lang.AnimEasingStep]
        };
        CmbKeyframePathType.SelectedIndex = 0;
    }

    private void PopulateControlComboBox()
    {
        // 从项目所有画面中收集控件
        var items = new List<ControlItem>();
        foreach (var screen in _project.Screens)
        {
            foreach (var ctrl in FlattenControls(screen.Controls))
            {
                items.Add(new ControlItem
                {
                    Id = ctrl.Id,
                    // 显示名走本地化别名、类型走 `ControlTypeTextKeys`（选中项靠 `Id` 解析，这一串只是给人看的）
                    // —— 登记 #61r（阶段 159）＋ #61o 的剩余面（阶段 160）
                    Display = $"[{screen.Name}] {Services.ControlNameDefaults.Display(ctrl)} ({Services.ControlTypeTextKeys.Describe(ctrl.Type)})"
                });
            }
        }

        CmbTargetControl.ItemsSource = items;
    }

    private static IEnumerable<HmiControl> FlattenControls(ObservableCollection<HmiControl> controls)
    {
        foreach (var ctrl in controls)
        {
            yield return ctrl;
            if (ctrl.Children.Count > 0)
            {
                foreach (var child in FlattenControls(ctrl.Children))
                    yield return child;
            }
        }
    }

    private void WireAnimationEditEvents()
    {
        TxtAnimationName.TextChanged += (_, _) => SyncAnimationFromUi();
        CmbTargetControl.SelectionChanged += (_, _) =>
        {
            if (_isUpdating) return;
            SyncAnimationFromUi();
            RefreshTimeline();
        };
        CmbAnimationProperty.SelectionChanged += (_, _) => SyncAnimationFromUi();
        TxtDescription.TextChanged += (_, _) => SyncAnimationFromUi();
        ChkEnabled.IsCheckedChanged += (_, _) => SyncAnimationFromUi();
        CmbTrigger.SelectionChanged += (_, _) => SyncAnimationFromUi();
        TxtStartValue.TextChanged += (_, _) => SyncAnimationFromUi();
        TxtEndValue.TextChanged += (_, _) => SyncAnimationFromUi();
        TxtDuration.TextChanged += (_, _) => SyncAnimationFromUi();
        TxtDelay.TextChanged += (_, _) => SyncAnimationFromUi();
        CmbPathType.SelectionChanged += (_, _) => SyncAnimationFromUi();
        TxtRepeatCount.TextChanged += (_, _) => SyncAnimationFromUi();
    }

    private void WireKeyframeEditEvents()
    {
        TxtKeyframeTime.TextChanged += (_, _) => SyncKeyframeFromUi();
        TxtKeyframeValue.TextChanged += (_, _) => SyncKeyframeFromUi();
        CmbKeyframePathType.SelectionChanged += (_, _) => SyncKeyframeFromUi();
    }

    // ═══════════════════════════════════════════════
    //  动画列表管理
    // ═══════════════════════════════════════════════

    private void RefreshAnimationList()
    {
        AnimationList.Items.Clear();

        var filter = CmbTriggerFilter.SelectedItem as string;
        var filtered = filter == null || filter == AppLocalization.Service[Lang.AnimFilterAll]
            ? _animations.ToList()
            : _animations.Where(a => a.Trigger == filter).ToList();

        foreach (var anim in filtered)
        {
            AnimationList.Items.Add(CreateAnimationItem(anim));
        }

        EmptyAnimationHint.IsVisible = _animations.Count == 0;
        BtnDeleteAnimation.IsEnabled = _animations.Count > 0;

        if (_currentAnimation != null && AnimationList.Items.Count > 0)
        {
            foreach (var item in AnimationList.Items.OfType<ListBoxItem>())
            {
                if (item.Tag is AnimationConfig a && a.Id == _currentAnimation.Id)
                {
                    AnimationList.SelectedItem = item;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 动画列表行。行内需要就地刷新的四个控件（状态圆点 / 名称 / 触发方式 / 属性）
    /// 在**构造时**捕获。此前是在 <c>AnimationList.Items</c> 里按 <c>Tag</c> 找到行，
    /// 再按 <c>stack.Children[0]</c> → <c>topRow.Children[1]</c> 这类**下标**一层层钻进去取 ——
    /// 结构一变就**静默不再刷新**（不抛、不报错）。
    /// <see cref="Control.Tag"/> 仍是 <see cref="AnimationConfig"/>。
    /// </summary>
    private sealed class AnimationRow : ListBoxItem
    {
        public AnimationRow(AnimationConfig animation, Border statusDot, TextBlock nameText,
                            TextBlock triggerText, TextBlock propertyText)
        {
            Animation = animation;
            StatusDot = statusDot;
            NameText = nameText;
            TriggerText = triggerText;
            PropertyText = propertyText;
            Tag = animation;
        }

        public AnimationConfig Animation { get; }

        public Border StatusDot { get; }

        public TextBlock NameText { get; }

        public TextBlock TriggerText { get; }

        public TextBlock PropertyText { get; }
    }

    private ListBoxItem CreateAnimationItem(AnimationConfig animation)
    {
        // 启用状态圆点
        var statusDot = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = animation.IsEnabled
                ? new SolidColorBrush(UiPalette.ConnectedBright)
                : new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var nameText = new TextBlock
        {
            Text = animation.Name,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var triggerText = new TextBlock
        {
            Text = $"[{animation.Trigger}]",
            Foreground = HmiEditor.App.Theme.Link,
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };

        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { statusDot, nameText, triggerText }
        };

        var propertyText = new TextBlock
        {
            Text = animation.PropertyName,
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 11,
            Margin = new Thickness(16, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var infoPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children = { topRow, propertyText }
        };

        return new AnimationRow(animation, statusDot, nameText, triggerText, propertyText)
        {
            Content = infoPanel,
            Padding = new Thickness(10, 8)
        };
    }

    private void OnAnimationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (AnimationList.SelectedItem is ListBoxItem item && item.Tag is AnimationConfig anim)
        {
            _currentAnimation = anim;
            LoadAnimationToEditPanel(anim);
            RefreshTimeline();
        }
        else
        {
            _currentAnimation = null;
            EditPanelVisible(false);
            KeyframeList.Items.Clear();
            ClearTimelineDots();
        }
    }

    private void OnNewAnimation()
    {
        _animationCounter++;

        var anim = new AnimationConfig
        {
            Name = AppLocalization.Service.Format(Lang.AnimDefaultName, _animationCounter),
            ControlId = "",
            PropertyName = "Opacity",
            StartValue = 0,
            EndValue = 255,
            DurationMs = 500,
            DelayMs = 0,
            PathType = AnimationPathType.EaseInOut,
            RepeatCount = 0,
            IsEnabled = true,
            Trigger = "OnLoad",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        _animations.Add(anim);
        RefreshAnimationList();

        // 选中新创建的动画
        foreach (var listItem in AnimationList.Items.OfType<ListBoxItem>())
        {
            if (listItem.Tag is AnimationConfig a && a.Id == anim.Id)
            {
                AnimationList.SelectedItem = listItem;
                break;
            }
        }

        AnimationListChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeleteAnimation()
    {
        if (_currentAnimation == null) return;

        var confirm = new ConfirmDeleteDialog
        {
            Message = AppLocalization.Service.Format(Lang.AnimConfirmDelete, _currentAnimation.Name),
        };

        confirm.Closed += (_, _) =>
        {
            if (confirm.Confirmed)
            {
                var id = _currentAnimation.Id;
                _animations.Remove(_currentAnimation);
                _currentAnimation = null;
                RefreshAnimationList();
                EditPanelVisible(false);
                KeyframeList.Items.Clear();
                ClearTimelineDots();
                AnimationListChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        confirm.ShowDialog(this);
    }

    // ═══════════════════════════════════════════════
    //  动画编辑面板
    // ═══════════════════════════════════════════════

    private void EditPanelVisible(bool visible)
    {
        // 切换右侧面板的整体可见性通过控制 ScrollViewer 内容动态实现
        // 此处仍保持 UI 可编辑状态，通过清空禁用字段来模拟隐藏
    }

    private void LoadAnimationToEditPanel(AnimationConfig anim)
    {
        _isUpdating = true;

        TxtAnimationName.Text = anim.Name;
        TxtDescription.Text = anim.Description ?? "";
        ChkEnabled.IsChecked = anim.IsEnabled;

        // 目标控件 ComboBox
        PopulateControlComboBox();
        if (CmbTargetControl.ItemsSource is List<ControlItem> controlItems)
        {
            var idx = controlItems.FindIndex(c => c.Id == anim.ControlId);
            CmbTargetControl.SelectedIndex = idx >= 0 ? idx : -1;
        }

        // 动画属性
        var propIdx = Array.IndexOf(_properties, anim.PropertyName);
        CmbAnimationProperty.SelectedIndex = propIdx >= 0 ? propIdx : 0;

        // 触发方式
        var triggerIdx = Array.IndexOf(_triggers, anim.Trigger);
        CmbTrigger.SelectedIndex = triggerIdx >= 0 ? triggerIdx : 0;

        // 数值字段
        TxtStartValue.Text = anim.StartValue.ToString(CultureInfo.InvariantCulture);
        TxtEndValue.Text = anim.EndValue.ToString(CultureInfo.InvariantCulture);
        TxtDuration.Text = anim.DurationMs.ToString();
        TxtDelay.Text = anim.DelayMs.ToString();
        TxtRepeatCount.Text = anim.RepeatCount.ToString();

        // 缓动函数
        CmbPathType.SelectedIndex = (int)anim.PathType;

        _isUpdating = false;
    }

    private void SyncAnimationFromUi()
    {
        if (_isUpdating || _currentAnimation == null) return;

        _currentAnimation.Name = TxtAnimationName.Text ?? "";
        _currentAnimation.Description = TxtDescription.Text?.Trim();

        _currentAnimation.IsEnabled = ChkEnabled.IsChecked ?? true;

        if (CmbTargetControl.SelectedItem is ControlItem ctrlItem)
            _currentAnimation.ControlId = ctrlItem.Id;

        if (CmbAnimationProperty.SelectedItem is string prop)
            _currentAnimation.PropertyName = prop;

        if (CmbTrigger.SelectedItem is string trigger)
            _currentAnimation.Trigger = trigger;

        if (double.TryParse(TxtStartValue.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var sv))
            _currentAnimation.StartValue = sv;

        if (double.TryParse(TxtEndValue.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var ev))
            _currentAnimation.EndValue = ev;

        if (int.TryParse(TxtDuration.Text, out var dur))
            _currentAnimation.DurationMs = Math.Max(0, dur);

        if (int.TryParse(TxtDelay.Text, out var delay))
            _currentAnimation.DelayMs = Math.Max(0, delay);

        if (int.TryParse(TxtRepeatCount.Text, out var repeat))
            _currentAnimation.RepeatCount = repeat;

        _currentAnimation.PathType = (AnimationPathType)CmbPathType.SelectedIndex;

        _currentAnimation.ModifiedAt = DateTime.UtcNow;

        // 刷新列表项显示
        foreach (var row in AnimationList.Items.OfType<AnimationRow>())
        {
            if (row.Animation != _currentAnimation) continue;

            row.NameText.Text = _currentAnimation.Name;
            row.TriggerText.Text = $"[{_currentAnimation.Trigger}]";
            row.PropertyText.Text = _currentAnimation.PropertyName;
            row.StatusDot.Background = _currentAnimation.IsEnabled
                ? new SolidColorBrush(UiPalette.ConnectedBright)
                : new SolidColorBrush(Color.FromRgb(107, 114, 128));
            break;
        }

        UpdateCCodePreview();
    }

    // ═══════════════════════════════════════════════
    //  关键帧管理
    // ═══════════════════════════════════════════════

    private void OnAddKeyframe()
    {
        if (_currentAnimation == null) return;

        // 计算一个合理的时间点
        var existingTimes = _currentAnimation.Keyframes.Select(k => k.TimePercent).ToList();
        double newTime = 50;
        if (existingTimes.Count > 0)
        {
            newTime = Math.Min(100, existingTimes.Max() + 10);
            if (newTime > 100) newTime = 50;
        }

        var kf = new AnimationKeyframe
        {
            TimePercent = newTime,
            Value = _currentAnimation.StartValue + (newTime / 100.0) * (_currentAnimation.EndValue - _currentAnimation.StartValue),
            Description = ""
        };

        _currentAnimation.Keyframes.Add(kf);
        SortKeyframes();
        RefreshKeyframeList();
        RefreshTimeline();
        UpdateCCodePreview();
    }

    private void OnDeleteKeyframe()
    {
        if (_currentAnimation == null || _selectedKeyframe == null) return;

        _currentAnimation.Keyframes.Remove(_selectedKeyframe);
        _selectedKeyframe = null;
        KeyframeEditPanel.IsVisible = false;
        RefreshKeyframeList();
        RefreshTimeline();
        UpdateCCodePreview();
    }

    private void OnKeyframeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (KeyframeList.SelectedItem is ListBoxItem item && item.Tag is AnimationKeyframe kf)
        {
            _selectedKeyframe = kf;
            LoadKeyframeToEditPanel(kf);
        }
        else
        {
            KeyframeEditPanel.IsVisible = false;
            _selectedKeyframe = null;
        }
    }

    private void LoadKeyframeToEditPanel(AnimationKeyframe kf)
    {
        _isUpdating = true;

        KeyframeEditPanel.IsVisible = true;
        TxtKeyframeTime.Text = kf.TimePercent.ToString("F0");
        TxtKeyframeValue.Text = kf.Value.ToString("F1");

        // 关键帧缓动函数（第一个为"使用全局设置"）
        CmbKeyframePathType.SelectedIndex = kf.PathType.HasValue
            ? (int)kf.PathType.Value + 1
            : 0;

        _isUpdating = false;
    }

    private void SyncKeyframeFromUi()
    {
        if (_isUpdating || _selectedKeyframe == null) return;

        // 先抓住「在编的那一条」（阶段 136 修）：下面 RefreshKeyframeList() 会 Items.Clear()，
        // 那一下会引发 SelectionChanged（选中 → null）⇒ OnKeyframeSelectionChanged 走 else 分支
        // 把 _selectedKeyframe 置 null、并把 KeyframeEditPanel.IsVisible 置 false。
        // 本方法尾部那个「选中排序后的关键帧」的循环原先比对的就是 _selectedKeyframe ——
        // 它刚被自己触发的清空置成 null ⇒ 循环永远不命中（死代码），
        // 症状是「改一下缓动下拉，关键帧面板就收起来、选中也没了」。
        // 抓一个局部引用、并让三处写入都落在它上面，这一跳才闭合。
        var keyframe = _selectedKeyframe;

        if (double.TryParse(TxtKeyframeTime.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
            keyframe.TimePercent = Math.Clamp(time, 0, 100);

        if (double.TryParse(TxtKeyframeValue.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            keyframe.Value = val;

        keyframe.PathType = CmbKeyframePathType.SelectedIndex switch
        {
            0 => null,
            >= 1 => (AnimationPathType)(CmbKeyframePathType.SelectedIndex - 1),
            _ => null
        };

        SortKeyframes();
        RefreshKeyframeList();
        RefreshTimeline();
        UpdateCCodePreview();

        // 选中排序后的关键帧（⚠ 比对的是上面抓的局部引用，不是此刻已被清空的 _selectedKeyframe）
        foreach (var listItem in KeyframeList.Items.OfType<ListBoxItem>())
        {
            if (listItem.Tag == keyframe)
            {
                KeyframeList.SelectedItem = listItem;
                break;
            }
        }
    }

    private void SortKeyframes()
    {
        if (_currentAnimation == null) return;

        var sorted = _currentAnimation.Keyframes.OrderBy(k => k.TimePercent).ToList();
        _currentAnimation.Keyframes.Clear();
        foreach (var kf in sorted)
            _currentAnimation.Keyframes.Add(kf);
    }

    private void RefreshKeyframeList()
    {
        KeyframeList.Items.Clear();
        if (_currentAnimation == null) return;

        foreach (var kf in _currentAnimation.Keyframes)
        {
            KeyframeList.Items.Add(CreateKeyframeItem(kf));
        }
    }

    private ListBoxItem CreateKeyframeItem(AnimationKeyframe kf)
    {
        var timeText = new TextBlock
        {
            Text = $"{kf.TimePercent:F0}%",
            Foreground = HmiEditor.App.Theme.Link,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 60
        };

        var valueText = new TextBlock
        {
            Text = kf.Value.ToString("F1"),
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 60
        };

        var pathText = new TextBlock
        {
            Text = kf.PathType.HasValue
                ? AnimationPathHelper.GetDisplayName(kf.PathType.Value)
                : "—",
            Foreground = HmiEditor.App.Theme.TextSecondary,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 100
        };

        var descText = new TextBlock
        {
            Text = kf.Description ?? "",
            Foreground = HmiEditor.App.Theme.TextSecondary,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("80,80,120,*,50"),
            ColumnSpacing = 2
        };
        grid.Children.Add(timeText);
        grid.Children.Add(valueText);
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(pathText);
        Grid.SetColumn(pathText, 2);
        grid.Children.Add(descText);
        Grid.SetColumn(descText, 3);

        return new ListBoxItem
        {
            Content = grid,
            Tag = kf,
            Padding = new Thickness(4, 3)
        };
    }

    // ═══════════════════════════════════════════════
    //  可视化时间轴
    // ═══════════════════════════════════════════════

    private void RefreshTimeline()
    {
        ClearTimelineDots();
        TimelineTickGrid.Children.Clear();
        TimelineTrackBorder.Child = null;

        if (_currentAnimation == null) return;

        var trackWidth = TimelineTrackBorder.Bounds.Width > 0
            ? TimelineTrackBorder.Bounds.Width
            : 300; // 默认宽度

        // 绘制时间刻度
        var tickCanvas = new Canvas();
        foreach (var pct in new[] { 0, 25, 50, 75, 100 })
        {
            var x = trackWidth * pct / 100.0;

            var tick = new Line
            {
                StartPoint = new Point(x, 0),
                EndPoint = new Point(x, 6),
                Stroke = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                StrokeThickness = 1
            };
            tickCanvas.Children.Add(tick);

            var label = new TextBlock
            {
                Text = $"{pct}%",
                Foreground = HmiEditor.App.Theme.TextSecondary,
                FontSize = 9,
                Margin = new Thickness(x - 8, 8, 0, 0)
            };
            tickCanvas.Children.Add(label);
        }

        TimelineTickGrid.Children.Add(tickCanvas);

        // 绘制时间轴轨道
        _trackCanvas = new Canvas();
        var trackHeight = 60.0;
        var trackCenterY = trackHeight / 2;

        // 背景线条
        var bgLine = new Line
        {
            StartPoint = new Point(0, trackCenterY),
            EndPoint = new Point(trackWidth, trackCenterY),
            Stroke = HmiEditor.App.Theme.InputBorder,
            StrokeThickness = 2
        };
        _trackCanvas.Children.Add(bgLine);

        // 关键帧范围高亮
        if (_currentAnimation.Keyframes.Count >= 2)
        {
            var sorted = _currentAnimation.Keyframes.OrderBy(k => k.TimePercent).ToList();
            var first = sorted.First();
            var last = sorted.Last();
            var x1 = trackWidth * first.TimePercent / 100.0;
            var x2 = trackWidth * last.TimePercent / 100.0;

            var highlight = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(60, 37, 99, 235)),
                Width = x2 - x1,
                Height = 8,
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(highlight, x1);
            Canvas.SetTop(highlight, trackCenterY - 4);
            _trackCanvas.Children.Add(highlight);
        }

        // 绘制值变化曲线
        if (_currentAnimation.Keyframes.Count >= 2)
        {
            var sorted = _currentAnimation.Keyframes.OrderBy(k => k.TimePercent).ToList();
            var allValues = sorted.Select(k => k.Value).ToList();
            var minVal = allValues.Min();
            var maxVal = allValues.Max();
            var valRange = Math.Abs(maxVal - minVal);
            if (valRange < 0.001) valRange = 1;

            var curveHeight = 40.0;
            var curveTop = trackCenterY - curveHeight / 2;

            var points = new List<Point>();
            foreach (var kf in sorted)
            {
                var x = trackWidth * kf.TimePercent / 100.0;
                var normalized = (kf.Value - minVal) / valRange;
                var y = curveTop + curveHeight * (1 - normalized);
                points.Add(new Point(x, y));
            }

            var curve = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromArgb(100, 96, 165, 250)),
                StrokeThickness = 1.5,
                StrokeDashArray = new AvaloniaList<double> { 3, 2 }
            };
            _trackCanvas.Children.Add(curve);
        }

        // 绘制关键帧标记点
        foreach (var kf in _currentAnimation.Keyframes)
        {
            var x = trackWidth * kf.TimePercent / 100.0;

            var dot = new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(8),
                Background = HmiEditor.App.Theme.AccentHover,
                BorderBrush = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
                BorderThickness = new Thickness(2),
                Tag = kf,
                Cursor = new Cursor(StandardCursorType.SizeWestEast)
            };

            // 内部值显示
            dot.Child = new TextBlock
            {
                Text = $"{kf.TimePercent:F0}",
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Canvas.SetLeft(dot, x - 8);
            Canvas.SetTop(dot, trackCenterY - 8);

            // 点击选中 + 拖拽移动
            dot.PointerPressed += (sender, e) =>
            {
                if (sender is Border b && b.Tag is AnimationKeyframe clickedKf)
                {
                    // 选中此关键帧
                    foreach (var li in KeyframeList.Items.OfType<ListBoxItem>())
                    {
                        if (li.Tag == clickedKf)
                        {
                            KeyframeList.SelectedItem = li;
                            break;
                        }
                    }

                    // 开始拖拽
                    _isDraggingKeyframe = true;
                    _draggedKeyframe = clickedKf;
                    b.Background = HmiEditor.App.Theme.Warning;
                    e.Pointer.Capture(b);
                }
                e.Handled = true;
            };

            dot.PointerMoved += (sender, e) =>
            {
                if (!_isDraggingKeyframe || _draggedKeyframe == null || sender is not Border b) return;

                var pos = e.GetPosition(_trackCanvas);
                var newPct = Math.Clamp(pos.X / trackWidth * 100.0, 0, 100);

                // 不允许两个关键帧完全重叠
                var tooClose = _currentAnimation.Keyframes
                    .Where(k => k != _draggedKeyframe)
                    .Any(k => Math.Abs(k.TimePercent - newPct) < 0.5);
                if (tooClose) return;

                _draggedKeyframe.TimePercent = newPct;

                // 实时更新圆点位置
                Canvas.SetLeft(b, trackWidth * newPct / 100.0 - 8);
                if (b.Child is TextBlock tb)
                    tb.Text = $"{newPct:F0}";

                // 更新关键帧编辑面板
                if (!_isUpdating)
                {
                    _isUpdating = true;
                    TxtKeyframeTime.Text = newPct.ToString("F0");
                    _isUpdating = false;
                }

                e.Handled = true;
            };

            dot.PointerReleased += (sender, e) =>
            {
                if (!_isDraggingKeyframe || _draggedKeyframe == null) return;

                if (sender is Border b)
                {
                    b.Background = HmiEditor.App.Theme.AccentHover;
                    e.Pointer.Capture(null);
                }

                _isDraggingKeyframe = false;
                _draggedKeyframe = null;

                // 拖拽结束后排序并刷新
                SortKeyframes();
                RefreshKeyframeList();
                UpdateCCodePreview();

                // 重绘时间轴以更新曲线
                RefreshTimeline();

                e.Handled = true;
            };

            _timelineDots.Add(dot);
            _trackCanvas.Children.Add(dot);
        }

        // 播放头竖线
        _playheadLine = new Line
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, trackHeight),
            Stroke = HmiEditor.App.Theme.Warning,
            StrokeThickness = 2,
            IsVisible = false
        };
        _trackCanvas.Children.Add(_playheadLine);

        // 播放头值标签
        _playheadValueLabel = new TextBlock
        {
            Foreground = HmiEditor.App.Theme.Warning,
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            IsVisible = false
        };
        _trackCanvas.Children.Add(_playheadValueLabel);

        // 双击轨道添加关键帧
        _trackCanvas.PointerPressed += (sender, e) =>
        {
            if (e.ClickCount == 2 && sender is Canvas cv)
            {
                var pos = e.GetPosition(cv);
                var pct = Math.Clamp(pos.X / trackWidth * 100.0, 0, 100);
                AddKeyframeAtPercent(pct);
                e.Handled = true;
            }
        };

        TimelineTrackBorder.Child = _trackCanvas;
    }

    private void ClearTimelineDots()
    {
        _timelineDots.Clear();
    }

    // ═══════════════════════════════════════════════
    //  双击添加关键帧
    // ═══════════════════════════════════════════════

    private void AddKeyframeAtPercent(double percent)
    {
        if (_currentAnimation == null) return;

        // 检查是否与现有关键帧过近
        if (_currentAnimation.Keyframes.Any(k => Math.Abs(k.TimePercent - percent) < 0.5))
            return;

        // 通过插值计算该位置的值
        var value = InterpolateValue(percent);

        var kf = new AnimationKeyframe
        {
            TimePercent = percent,
            Value = value,
            Description = ""
        };

        _currentAnimation.Keyframes.Add(kf);
        SortKeyframes();
        RefreshKeyframeList();
        RefreshTimeline();
        UpdateCCodePreview();

        // 选中新添加的关键帧
        foreach (var listItem in KeyframeList.Items.OfType<ListBoxItem>())
        {
            if (listItem.Tag == kf)
            {
                KeyframeList.SelectedItem = listItem;
                break;
            }
        }
    }

    // ═══════════════════════════════════════════════
    //  播放预览
    // ═══════════════════════════════════════════════

    private void TogglePlayback()
    {
        if (_currentAnimation == null) return;

        if (_isPlaying)
        {
            StopPlayback();
        }
        else
        {
            StartPlayback();
        }
    }

    private void StartPlayback()
    {
        if (_currentAnimation == null) return;

        _isPlaying = true;
        _playheadPercent = 0;
        BtnPlayPreview.Content = AppLocalization.Service[Lang.AnimStop];

        // 根据动画时长计算 timer 间隔（约 30fps）
        var durationMs = Math.Max(100, _currentAnimation.DurationMs);
        var intervalMs = Math.Max(16, durationMs / 30);

        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(intervalMs)
        };
        _playbackTimer.Tick += OnPlaybackTick;
        _playbackTimer.Start();

        // 显示播放头
        if (_playheadLine != null) _playheadLine.IsVisible = true;
        if (_playheadValueLabel != null) _playheadValueLabel.IsVisible = true;
    }

    private void StopPlayback()
    {
        _isPlaying = false;
        _playbackTimer?.Stop();
        _playbackTimer = null;
        BtnPlayPreview.Content = AppLocalization.Service["anim.play.preview"];

        // 隐藏播放头
        if (_playheadLine != null) _playheadLine.IsVisible = false;
        if (_playheadValueLabel != null) _playheadValueLabel.IsVisible = false;
    }

    private void OnPlaybackTick(object? sender, EventArgs e)
    {
        if (_currentAnimation == null || !_isPlaying)
        {
            StopPlayback();
            return;
        }

        // 推进播放头
        var durationMs = Math.Max(100, _currentAnimation.DurationMs);
        var intervalMs = _playbackTimer?.Interval.TotalMilliseconds ?? 33;
        var step = 100.0 * intervalMs / durationMs;
        _playheadPercent += step;

        if (_playheadPercent >= 100)
        {
            _playheadPercent = 100;
            UpdatePlayheadVisual();
            StopPlayback();
            return;
        }

        UpdatePlayheadVisual();
    }

    private void UpdatePlayheadVisual()
    {
        if (_trackCanvas == null || _playheadLine == null || _currentAnimation == null) return;

        var trackWidth = TimelineTrackBorder.Bounds.Width > 0
            ? TimelineTrackBorder.Bounds.Width
            : 300;
        var x = trackWidth * _playheadPercent / 100.0;

        _playheadLine.StartPoint = new Point(x, 0);
        _playheadLine.EndPoint = new Point(x, 60);

        // 更新值标签
        if (_playheadValueLabel != null)
        {
            var value = InterpolateValue(_playheadPercent);
            _playheadValueLabel.Text = $"{_playheadPercent:F0}% → {value:F1}";
            Canvas.SetLeft(_playheadValueLabel, x + 4);
            Canvas.SetTop(_playheadValueLabel, 2);
        }
    }

    /// <summary>
    /// 根据时间百分比插值计算属性值。
    /// 有关键帧时在相邻关键帧间线性插值，无关键帧时在 StartValue→EndValue 间线性插值。
    /// </summary>
    private double InterpolateValue(double percent)
    {
        if (_currentAnimation == null) return 0;

        if (_currentAnimation.Keyframes.Count == 0)
        {
            return _currentAnimation.StartValue +
                   (percent / 100.0) * (_currentAnimation.EndValue - _currentAnimation.StartValue);
        }

        var sorted = _currentAnimation.Keyframes.OrderBy(k => k.TimePercent).ToList();

        // 在第一个关键帧之前
        if (percent <= sorted[0].TimePercent)
            return sorted[0].Value;

        // 在最后一个关键帧之后
        if (percent >= sorted[^1].TimePercent)
            return sorted[^1].Value;

        // 在两个关键帧之间
        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var k1 = sorted[i];
            var k2 = sorted[i + 1];
            if (percent >= k1.TimePercent && percent <= k2.TimePercent)
            {
                var t = (percent - k1.TimePercent) / (k2.TimePercent - k1.TimePercent);
                return k1.Value + t * (k2.Value - k1.Value);
            }
        }

        return sorted[^1].Value;
    }

    // ═══════════════════════════════════════════════
    //  LVGL C 代码预览
    // ═══════════════════════════════════════════════

    private void UpdateCCodePreview()
    {
        if (_currentAnimation == null)
        {
            TxtCCodePreview.Text = "";
            return;
        }

        var anim = _currentAnimation;
        var code = GenerateCCode(anim);
        TxtCCodePreview.Text = code;
    }

    /// <summary>
    /// 预览用的 C 代码 —— <b>直接调真实发射器</b>，预览与产物同源。
    ///
    /// <para>历史缺陷：这里曾自己手拼一套形似产物、实则对不上的伪代码 ——
    /// 控件变量写成 <c>ui_{id}</c>（产物是 <c>obj_{净化id}</c>，用户照抄会抄到不存在的符号）、
    /// 没有 <c>start_anim_*</c> 包装函数与 <c>apply_animations()</c> 入口、缓动映射还各留了一份
    /// 私有 switch。现在预览即 <see cref="CodeGenerator.GenerateAnimationCode"/> 的输出。</para>
    ///
    /// <para>有意保留的副作用：动画未启用时预览显示 <c>// No animations configured.</c> ——
    /// 那正是这个动画对产物的真实贡献。</para>
    /// </summary>
    private static string GenerateCCode(AnimationConfig anim) =>
        new CodeGenerator().GenerateAnimationCode(new[] { anim });

    // ═══════════════════════════════════════════════
    //  内部辅助类型
    // ═══════════════════════════════════════════════

    private class ControlItem
    {
        public string Id { get; set; } = "";
        public string Display { get; set; } = "";

        public override string ToString() => Display;
    }

    /// <summary>
    /// 删除确认对话框。
    /// </summary>
    private class ConfirmDeleteDialog : Window
    {
        public bool Confirmed { get; private set; }
        public string Message { get; set; } = "";

        public ConfirmDeleteDialog()
        {
            Width = 380;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = HmiEditor.App.Theme.BgWindow;
            WindowDecorations = WindowDecorations.BorderOnly;

            var messageText = new TextBlock
            {
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 13,
                Margin = new Thickness(16, 16, 16, 12),
                TextWrapping = TextWrapping.Wrap
            };

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(16, 0, 16, 16)
            };

            var cancelBtn = new Button
            {
                Content = AppLocalization.Service["gen.cancel"],
                Background = HmiEditor.App.Theme.BgElevated,
                Foreground = HmiEditor.App.Theme.TextSecondary,
                BorderThickness = new Thickness(1),
                BorderBrush = HmiEditor.App.Theme.InputBorder,
                Padding = new Thickness(14, 6),
                FontSize = 12
            };

            var okBtn = new Button
            {
                Content = AppLocalization.Service["gen.confirm.delete"],
                Background = HmiEditor.App.Theme.Danger,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 6),
                FontSize = 12
            };

            cancelBtn.Click += (_, _) => Close();
            okBtn.Click += (_, _) =>
            {
                Confirmed = true;
                Close();
            };

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(okBtn);

            var titleBar = new Border
            {
                Background = HmiEditor.App.Theme.BgPanel,
                Height = 32,
                Child = new TextBlock
                {
                    Text = AppLocalization.Service["gen.confirm.delete"],
                    Foreground = HmiEditor.App.Theme.TextPrimary,
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0)
                }
            };

            var contentPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children = { messageText, btnPanel }
            };

            var root = new Border
            {
                Background = HmiEditor.App.Theme.BgWindow,
                Padding = new Thickness(0),
                Child = new DockPanel
                {
                    Children = { titleBar, contentPanel }
                }
            };

            Content = root;

            Loaded += (_, _) =>
            {
                messageText.Text = Message;
            };
        }
    }
}
