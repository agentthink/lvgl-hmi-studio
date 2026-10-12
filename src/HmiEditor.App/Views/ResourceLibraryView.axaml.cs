using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using HmiEditor.App.Resources;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace HmiEditor.App.Views;

/// <summary>
/// HMI 图库资源浏览器 —— 画廊式展示内置预置资源和用户自定义资源。
///
/// 功能：
///   - 按应用场景（工业/汽车/智能家居）分标签筛选
///   - 按资源类型（图标/模板/过程图形/状态指示）分类型筛选
///   - 文本搜索（名称/描述/标签）
///   - 画廊式卡片展示（图标 + 名称 + 分类色标签）
///   - 双击或拖拽放置到画布
/// </summary>
public partial class ResourceLibraryView : UserControl
{
    /// <summary>资源放置事件 —— 双击或拖拽到画布时触发。</summary>
    public event EventHandler<HmiResourceItem>? ResourcePlaced;

    /// <summary>拖拽数据格式标识 —— 用于 ResourceLibrary → DesignCanvas 的资源拖放。</summary>
    public const string ResourceDragFormat = "HmiResourceId";

    /// <summary>强类型拖拽格式（进程内）。</summary>
    public static readonly DataFormat<string> ResourceDragDataFormat =
        DataFormat.CreateInProcessFormat<string>(ResourceDragFormat);

    /// <summary>拖拽载荷前缀 —— 区分直接传递的控件类型字符串。</summary>
    private const string ResourceDragPrefix = "resourceId:";

    private readonly HmiResourceLibrary _library = new();
    private readonly ObservableCollection<HmiResourceItem> _filtered = new();

    // 筛选状态
    private ApplicationScenario? _selectedScenario;
    private ResourceItemType? _selectedType;
    private string _searchText = "";

    // 选中状态
    private HmiResourceItem? _selectedItem;
    private Border? _selectedCard;

    public Dock.Model.Mvvm.Controls.Tool? HostTool { get; set; }

    public ResourceLibraryView()
    {
        InitializeComponent();

        // 加载内置预置资源
        foreach (var r in PresetResourceLibrary.CreateBuiltIn())
            _library.BuiltInResources.Add(r);

        Gallery.Children.Clear();
        BuildScenarioBar();
        BuildTypeBar();
        ApplyFilter();
        UpdateCount();

        // 转发事件到 DockLayoutState 聚合器，确保 Dock 重建后仍能到达 MainWindow
        ResourcePlaced += (_, item) => DockLayoutState.RaiseResourcePlaced(this, item);

        // 注：SearchBox 的 TextChanged 在 .axaml 里声明过，这里不得再 += 一次（缺陷 #43）。
    }

    /// <summary>附加到视觉树时：从 DockLayoutState 恢复共享库状态（如果存在）。</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // 共享库：如果 DockLayoutState 存在共享库实例，则从其恢复
        if (DockLayoutState.ResourceLibrary != null)
        {
            // 从共享库加载数据到本地视图
            LoadLibrary(DockLayoutState.ResourceLibrary);
        }
    }

    /// <summary>从外部库加载数据（合并内置 + 自定义）。</summary>
    public void LoadLibrary(HmiResourceLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        _library.BuiltInResources.Clear();
        foreach (var r in library.BuiltInResources)
            _library.BuiltInResources.Add(r);
        _library.CustomResources.Clear();
        foreach (var r in library.CustomResources)
            _library.CustomResources.Add(r);
        ApplyFilter();
        UpdateCount();
    }

    // ── 筛选栏构建 ──

    private void BuildScenarioBar()
    {
        ScenarioBar.Children.Clear();

        // "全部" 标签
        ScenarioBar.Children.Add(CreateScenarioTag(null, AppLocalization.Service["reslib.scenario.all"]));
        // 各场景标签
        foreach (var s in System.Enum.GetValues<ApplicationScenario>())
        {
            var label = AppLocalization.Service[$"reslib.scenario.{s.ToString().ToLowerInvariant()}"];
            ScenarioBar.Children.Add(CreateScenarioTag(s, label));
        }
    }

    private ToggleButton CreateScenarioTag(ApplicationScenario? scenario, string label)
    {
        var btn = new ToggleButton
        {
            Content = label,
            Margin = new Thickness(0, 0, 4, 0),
            Padding = new Thickness(8, 3),
            FontSize = 11,
            Background = HmiEditor.App.Theme.BgDeep,
            Foreground = HmiEditor.App.Theme.TextSecondary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            IsChecked = scenario == null, // "全部" 默认选中
        };
        btn.Click += (_, _) =>
        {
            _selectedScenario = scenario;
            // 排他选中
            foreach (var child in ScenarioBar.Children.OfType<ToggleButton>())
                child.IsChecked = child == btn;
            ApplyFilter();
            UpdateCount();
        };
        return btn;
    }

    private void BuildTypeBar()
    {
        TypeBar.Children.Clear();

        // "全部类型" 标签
        TypeBar.Children.Add(CreateTypeTag(null, AppLocalization.Service["reslib.type.all"]));
        // 各类型标签
        foreach (var t in System.Enum.GetValues<ResourceItemType>())
        {
            var label = AppLocalization.Service[$"reslib.type.{t.ToString().ToLowerInvariant()}"];
            TypeBar.Children.Add(CreateTypeTag(t, label));
        }
    }

    private ToggleButton CreateTypeTag(ResourceItemType? type, string label)
    {
        var btn = new ToggleButton
        {
            Content = label,
            Margin = new Thickness(0, 0, 4, 0),
            Padding = new Thickness(6, 2),
            FontSize = 10,
            Background = HmiEditor.App.Theme.BgDeep,
            Foreground = HmiEditor.App.Theme.TextSecondary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            IsChecked = type == null,
        };
        btn.Click += (_, _) =>
        {
            _selectedType = type;
            // 排他选中
            foreach (var child in TypeBar.Children.OfType<ToggleButton>())
                child.IsChecked = child == btn;
            ApplyFilter();
            UpdateCount();
        };
        return btn;
    }

    // ── 筛选逻辑 ──

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (Parent == null) return;
        _searchText = SearchBox.Text?.Trim() ?? "";
        ApplyFilter();
        UpdateCount();
    }

    private void ApplyFilter()
    {
        var query = _library.AllResources;

        // 场景筛选
        if (_selectedScenario.HasValue)
            query = query.Where(r => r.Scenario == _selectedScenario.Value);

        // 类型筛选
        if (_selectedType.HasValue)
            query = query.Where(r => r.Type == _selectedType.Value);

        // 文本搜索
        if (!string.IsNullOrEmpty(_searchText))
        {
            var kw = _searchText.ToLowerInvariant();
            query = query.Where(r =>
                r.Name.Contains(kw, System.StringComparison.OrdinalIgnoreCase) ||
                r.DisplayName.Contains(kw, System.StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(kw, System.StringComparison.OrdinalIgnoreCase) ||
                r.Tags.Any(t => t.Contains(kw, System.StringComparison.OrdinalIgnoreCase)));
        }

        _filtered.Clear();
        foreach (var item in query)
            _filtered.Add(item);

        RebuildGallery();
    }

    private void UpdateCount()
    {
        CountLabel.Text = AppLocalization.Service.Format("reslib.count", _filtered.Count, _library.AllResources.Count());
    }

    // ── 画廊构建 ──

    private void RebuildGallery()
    {
        Gallery.Children.Clear();
        _selectedCard = null;

        foreach (var item in _filtered)
        {
            var card = BuildResourceCard(item);
            Gallery.Children.Add(card);
        }
    }

    private Border BuildResourceCard(HmiResourceItem item)
    {
        var catColor = TryParseBrush(item.PrimaryColor) ?? HmiEditor.App.Theme.TextSecondary;

        // 图标区域
        var iconBox = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(4),
            Background = HmiEditor.App.Theme.BgDeep,
            BorderBrush = catColor,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 4),
            Child = BuildIconContent(item, catColor),
        };

        // 名称
        var nameText = new TextBlock
        {
            Text = item.DisplayName,
            FontSize = 11,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 70,
        };

        // 分类标签
        var catText = new TextBlock
        {
            Text = item.SubCategory,
            FontSize = 9,
            Foreground = HmiEditor.App.Theme.TextMuted,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 70,
        };

        var card = new Border
        {
            Classes = { "galleryCard" },
            Width = 80,
            Height = 76,
            Padding = new Thickness(4),
            Tag = item,
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { iconBox, nameText, catText }
            }
        };

        // 点击选中
        card.PointerPressed += (_, e) =>
        {
            SelectCard(card, item);
            e.Handled = true;
        };

        // 双击放置
        card.DoubleTapped += (_, _) =>
        {
            ResourcePlaced?.Invoke(this, item);
        };

        // 拖拽支持
        AttachCardDrag(card, item);

        return card;
    }

    private void SelectCard(Border card, HmiResourceItem item)
    {
        _selectedCard?.Classes.Remove("selected");
        _selectedCard = card;
        card.Classes.Add("selected");
        _selectedItem = item;
    }

    // ── 拖拽支持 ──

    private Point _dragStartPoint;
    private bool _dragStarted;
    private PointerPressedEventArgs? _dragTriggerEvent;
    private const double DragThreshold = 4.0;

    private void AttachCardDrag(Border card, HmiResourceItem item)
    {
        card.PointerPressed += (_, e) =>
        {
            _dragStartPoint = e.GetPosition(this);
            _dragStarted = false;
            _dragTriggerEvent = e;
        };

        card.PointerMoved += async (_, e) =>
        {
            if (_dragTriggerEvent == null) return;
            var pos = e.GetPosition(this);
            if (System.Math.Abs(pos.X - _dragStartPoint.X) <= DragThreshold &&
                System.Math.Abs(pos.Y - _dragStartPoint.Y) <= DragThreshold)
                return;

            if (_dragStarted) return;
            _dragStarted = true;

            // 触发拖放 —— 使用资源 ID 作为拖拽数据
            // 格式：resourceId:"<资源ID>"，DesignCanvas 据此识别为资源拖放
            var payload = $"{ResourceDragPrefix}{item.Id}";
            var dataItem = DataTransferItem.Create(ResourceDragDataFormat, payload);
            var data = new DataTransfer();
            data.Add(dataItem);
            try
            {
                await DragDrop.DoDragDropAsync(_dragTriggerEvent, data, DragDropEffects.Copy);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceLibrary] 拖拽异常: {ex.Message}");
            }
        };

        card.PointerReleased += (_, _) =>
        {
            _dragStarted = false;
            _dragTriggerEvent = null;
        };
    }

    // ── 工具方法 ──

    private static IBrush? TryParseBrush(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        try
        {
            var color = Color.Parse(hex);
            return new SolidColorBrush(color);
        }
        catch { return null; }
    }

    // 构建图标内容：有 IconPathData 时用 Path 矢量图标，否则回退到 Unicode glyph
    private Control BuildIconContent(HmiResourceItem item, IBrush catColor)
    {
        // 矢量 SVG Path 优先
        if (!string.IsNullOrWhiteSpace(item.IconPathData))
        {
            return new Viewbox
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Avalonia.Controls.Shapes.Path
                {
                    Data = Geometry.Parse(item.IconPathData),
                    Fill = catColor,
                    Stroke = catColor,
                    StrokeThickness = 1.5,
                    Stretch = Stretch.UniformToFill,
                },
            };
        }

        // 回退：Unicode 字符
        return new TextBlock
        {
            Text = GetResourceGlyph(item.Type),
            FontSize = 16,
            Foreground = catColor,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    // 资源类型 → 显示符号（Unicode 字符，跨字体可靠）—— 当 IconPathData 为空时使用
    private static string GetResourceGlyph(ResourceItemType type) => type switch
    {
        ResourceItemType.Icon => "\u25CF",              // ● 实心圆
        ResourceItemType.SvgIcon => "\u25C6",           // ◆ 菱形
        ResourceItemType.Template => "\u25A0",          // ■ 方块
        ResourceItemType.StatusIndicator => "\u25CB",  // ○ 空心圆
        ResourceItemType.ProcessGraphic => "\u25B2",   // ▲ 三角
        ResourceItemType.PanelLayout => "\u25A4",       // ◤ 面板
        ResourceItemType.ChartSeries => "\u25BC",       // ▼ 倒三角
        _ => "\u25AA",                                   // ▪ 小方块
    };
}
