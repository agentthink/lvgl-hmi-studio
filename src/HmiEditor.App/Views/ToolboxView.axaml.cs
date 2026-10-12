using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using HmiEditor.App.Resources;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace HmiEditor.App.Views;

/// <summary>
/// 工具箱视图 —— 仅承载控件拖拽列表（模板库已迁移至独立的 Faceplate 面板）。
///
/// [dock-blank-after-drag] 最终方案：
/// 每次 Build 创建新实例（工厂模式），OnAttachedToVisualTree 中绑定事件到 DockLayoutState，
/// 数据从 DockLayoutState 读取。事件通过 DockLayoutState 事件聚合器转发到 MainWindow。
/// </summary>
public partial class ToolboxView : UserControl
{
    public event EventHandler<HmiControlType>? ToolItemSelected;

    private readonly ObservableCollection<ToolboxItem> _allItems = new();

    private ToolboxItem? _dragItem;
    private Point _dragStartPoint;
    private bool _dragStarted;
    private PointerPressedEventArgs? _dragTriggerEvent;
    private const double DragThreshold = 4.0;

    public Dock.Model.Mvvm.Controls.Tool? HostTool { get; set; }

    // 持有转发委托以便在 Detached 时对称解绑（Dock 面板会被反复重建，避免事件累积）。
    private EventHandler<HmiControlType>? _forwardToolSelected;

    /// <summary>将焦点移入工具箱搜索框并选中现有筛选条件。</summary>
    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    public ToolboxView()
    {
        InitializeComponent();
        PopulateItems();

        ToolboxList.ItemsSource = _allItems;
        ToolboxList.SelectedIndex = 0;
        ResultCountText.Text = AppLocalization.Service.Format("tb.search.count", _allItems.Count);

        AttachDragHandlers(ToolboxList);
    }

    /// <summary>
    /// 附加到视觉树时：绑定事件到 DockLayoutState（事件聚合），从 DockLayoutState 读取数据。
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // 将事件转发到 DockLayoutState 事件聚合器
        _forwardToolSelected ??= (s, type) => DockLayoutState.RaiseToolItemSelected(this, type);
        ToolItemSelected += _forwardToolSelected;
    }

    // 与 Attached 对称：Dock 面板重建/分离时解除事件转发，避免重复实例累积订阅。
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_forwardToolSelected != null)
            ToolItemSelected -= _forwardToolSelected;
        base.OnDetachedFromVisualTree(e);
    }

    private void AttachDragHandlers(ListBox listBox)
    {
        listBox.AddHandler(InputElement.PointerPressedEvent, OnToolboxListPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        listBox.AddHandler(InputElement.PointerMovedEvent, OnToolboxListPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        listBox.AddHandler(InputElement.PointerReleasedEvent, OnToolboxListPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    // 控件类型 → 矢量图标（Path 数据，viewBox 0 0 24 24，跨字体可靠渲染）
    //
    // ⚠ 与 Assets/Icons.axaml 的关系（阶段 72 起有判据）：26 个类型里 19 个在那里有**镜像键**
    //   （IconQuick<T> 14 个 / IconDraw<T> 5 个），几何**必须逐字一致** —— 同一个控件在
    //   「主窗口绘图工具栏」与「工具箱」里长得不一样，用户是能同时看到的。
    //   曾经有 4 条分叉过：Rectangle / Ellipse / Polyline / Arc 在这里是「收缩一格的版本」
    //   （5..19 / 半径 7），而字典里是 4..20 / 半径 8。
    //   守它的判据：ToolboxIconDictionaryTests。**改这里任何一条几何前先看那里。**
    //
    // ⚠ internal 只为判据可调用（HmiEditor.App.csproj 的 InternalsVisibleTo App.UiTests），
    //   不是给产品代码用的构造 API。
    internal static string ControlIcon(HmiControlType type) => type switch
    {
        HmiControlType.Screen     => "M3,4 L21,4 L21,18 L3,18 Z",                            // 容器：矩形框
        HmiControlType.Button     => "M4,7 L20,7 L20,17 L4,17 Z",                            // 按钮：圆角矩形
        HmiControlType.Slider     => "M4,12 L20,12 M14,12 m-3,0 a3,3 0 1,0 6,0 a3,3 0 1,0 -6,0", // 滑条：横线+圆点
        HmiControlType.Switch     => "M6,5 L18,5 A7,7 0 0 1 18,19 L6,19 A7,7 0 0 1 6,5 Z",   // 开关：胶囊
        HmiControlType.CheckBox   => "M4,4 L20,4 L20,20 L4,20 Z M7,12 L11,16 L17,8",         // 复选：方框+勾
        HmiControlType.Label      => "M4,8 L20,8 M4,12 L20,12 M4,16 L14,16",                 // 标签：文本行
        HmiControlType.Image      => "M4,5 L20,5 L20,19 L4,19 Z M7,16 L11,11 L14,14 L17,9",   // 图片：框+山
        HmiControlType.Bar        => "M5,19 L5,9 L8,9 L8,19 Z M10,19 L10,5 L13,5 L13,19 Z M15,19 L15,12 L18,12 L18,19 Z", // 柱状
        HmiControlType.Dropdown   => "M4,6 L20,6 L20,16 L4,16 Z M6,19 L12,14 L18,19 Z",       // 下拉：框+箭头
        HmiControlType.TextArea   => "M6,3 L14,3 L19,8 L19,21 L6,21 Z M14,3 L14,8 L19,8",     // 文本域：文档
        HmiControlType.Gauge      => "M4,15 A8,8 0 0 1 20,15 M12,15 L12,11 L14.5,13",         // 仪表：弧+指针
        HmiControlType.Knob       => "M12,4 A8,8 0 1 0 12,20 A8,8 0 1 0 12,4 Z M12,12 L16,9", // 旋钮：圆+指针
        HmiControlType.Keyboard   => "M4,7 L20,7 L20,17 L4,17 Z M6,9.5 L6,11 M9.5,9.5 L9.5,11 M13,9.5 L13,11 M16.5,9.5 L16.5,11 M7,14 L17,14", // 键盘
        HmiControlType.Led        => "M12,5 A7,7 0 1 0 12,19 A7,7 0 1 0 12,5 Z",             // LED：圆
        HmiControlType.Spinner    => "M12,4 A8,8 0 0 1 20,12 M12,4 L12,9",                    // 加载：弧+针
        HmiControlType.TabView    => "M4,6 L20,6 L20,18 L4,18 Z M4,6 L20,6 M4,6 L4,10",       // 页签：容器+标签
        HmiControlType.Line       => "M4,18 L20,6",                                           // 直线
        HmiControlType.Table      => "M4,4 L20,4 L20,20 L4,20 Z M4,10 L20,10 M4,16 L20,16 M10,4 L10,20", // 表格
        HmiControlType.Svg        => "M12,4 L20,12 L12,20 L4,12 Z",                           // SVG：菱形
        HmiControlType.Tank       => "M6,5 L18,5 L18,15 A6,6 0 0 1 6,15 Z",                   // 罐体：圆柱
        HmiControlType.Pipe       => "M5,8 L19,8 M5,16 L19,16",                               // 管道：双线
        HmiControlType.Valve      => "M12,4 L20,12 L12,20 L4,12 Z M12,4 L12,20",              // 阀门：菱形+线
        // I10: 基础绘图图形
        HmiControlType.Rectangle  => "M4,4 L20,4 L20,20 L4,20 Z",                             // 矩形：方框
        HmiControlType.Ellipse    => "M12,4 A8,8 0 1 0 12,20 A8,8 0 1 0 12,4 Z",              // 椭圆：圆
        HmiControlType.Polyline   => "M4,17 L9,9 L13,13 L20,5",                              // 折线：多段线
        HmiControlType.Arc        => "M5,13 A7,7 0 1 0 12,6 M12,6 L17,6",                     // 圆弧：弧+端点
        _ => "M4,4 L20,4 L20,20 L4,20 Z",
    };

    private void PopulateItems()
    {
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Screen)),     Services.ControlTypeTextKeys.Describe(HmiControlType.Screen),    AppLocalization.Service[Lang.TbCatContainer], HmiControlType.Screen));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Button)),     Services.ControlTypeTextKeys.Describe(HmiControlType.Button),    AppLocalization.Service[Lang.TbCatInput], HmiControlType.Button));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Slider)),     Services.ControlTypeTextKeys.Describe(HmiControlType.Slider),    AppLocalization.Service[Lang.TbCatInput], HmiControlType.Slider));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Switch)),     Services.ControlTypeTextKeys.Describe(HmiControlType.Switch),    AppLocalization.Service[Lang.TbCatInput], HmiControlType.Switch));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.CheckBox)),   Services.ControlTypeTextKeys.Describe(HmiControlType.CheckBox),  AppLocalization.Service[Lang.TbCatInput], HmiControlType.CheckBox));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Label)),      Services.ControlTypeTextKeys.Describe(HmiControlType.Label),     AppLocalization.Service[Lang.TbCatDisplay], HmiControlType.Label));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Image)),      Services.ControlTypeTextKeys.Describe(HmiControlType.Image),     AppLocalization.Service[Lang.TbCatDisplay], HmiControlType.Image));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Bar)),        Services.ControlTypeTextKeys.Describe(HmiControlType.Bar),       AppLocalization.Service[Lang.TbCatChart], HmiControlType.Bar));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Dropdown)),   Services.ControlTypeTextKeys.Describe(HmiControlType.Dropdown),  AppLocalization.Service[Lang.TbCatSelect], HmiControlType.Dropdown));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.TextArea)),   Services.ControlTypeTextKeys.Describe(HmiControlType.TextArea),  AppLocalization.Service[Lang.TbCatInput], HmiControlType.TextArea));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Gauge)),      Services.ControlTypeTextKeys.Describe(HmiControlType.Gauge),     AppLocalization.Service[Lang.TbCatChart], HmiControlType.Gauge));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Knob)),       Services.ControlTypeTextKeys.Describe(HmiControlType.Knob),      AppLocalization.Service[Lang.TbCatInput], HmiControlType.Knob));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Keyboard)),   Services.ControlTypeTextKeys.Describe(HmiControlType.Keyboard),  AppLocalization.Service[Lang.TbCatInput], HmiControlType.Keyboard));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Led)),        Services.ControlTypeTextKeys.Describe(HmiControlType.Led),       AppLocalization.Service[Lang.TbCatDisplay], HmiControlType.Led));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Spinner)),    Services.ControlTypeTextKeys.Describe(HmiControlType.Spinner),   AppLocalization.Service[Lang.TbCatSpecial], HmiControlType.Spinner));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.TabView)),    Services.ControlTypeTextKeys.Describe(HmiControlType.TabView),   AppLocalization.Service[Lang.TbCatContainer], HmiControlType.TabView));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Line)),       Services.ControlTypeTextKeys.Describe(HmiControlType.Line),      AppLocalization.Service[Lang.TbCatDisplay], HmiControlType.Line));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Table)),      Services.ControlTypeTextKeys.Describe(HmiControlType.Table),     AppLocalization.Service[Lang.TbCatContainer], HmiControlType.Table));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Svg)),        Services.ControlTypeTextKeys.Describe(HmiControlType.Svg),       AppLocalization.Service[Lang.TbCatDisplay], HmiControlType.Svg));
        // I7: 过程图形控件
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Tank)),       Services.ControlTypeTextKeys.Describe(HmiControlType.Tank),      AppLocalization.Service[Lang.TbCatProcess], HmiControlType.Tank));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Pipe)),       Services.ControlTypeTextKeys.Describe(HmiControlType.Pipe),      AppLocalization.Service[Lang.TbCatProcess], HmiControlType.Pipe));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Valve)),      Services.ControlTypeTextKeys.Describe(HmiControlType.Valve),     AppLocalization.Service[Lang.TbCatProcess], HmiControlType.Valve));
        // I10: 基础绘图图形
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Rectangle)),  Services.ControlTypeTextKeys.Describe(HmiControlType.Rectangle), AppLocalization.Service[Lang.TbCatDraw], HmiControlType.Rectangle));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Ellipse)),    Services.ControlTypeTextKeys.Describe(HmiControlType.Ellipse),   AppLocalization.Service[Lang.TbCatDraw], HmiControlType.Ellipse));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Polyline)),   Services.ControlTypeTextKeys.Describe(HmiControlType.Polyline),  AppLocalization.Service[Lang.TbCatDraw], HmiControlType.Polyline));
        _allItems.Add(new(StreamGeometry.Parse(ControlIcon(HmiControlType.Arc)),        Services.ControlTypeTextKeys.Describe(HmiControlType.Arc),       AppLocalization.Service[Lang.TbCatDraw], HmiControlType.Arc));
    }

    // ── 拖拽 ──

    private void OnToolboxListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var item = FindToolboxItem(e.Source);
        if (item == null) return;
        _dragItem = item;
        _dragStartPoint = e.GetPosition(this);
        _dragStarted = false;
        _dragTriggerEvent = e;
        e.Handled = true;
    }

    private async void OnToolboxListPointerMoved(object? sender, PointerEventArgs e)
    {
        try
        {
            if (_dragItem == null || _dragStarted) return;
            var pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _dragStartPoint.X) <= DragThreshold && Math.Abs(pos.Y - _dragStartPoint.Y) <= DragThreshold)
                return;
            _dragStarted = true;
            var item = DataTransferItem.Create(DesignCanvas.DragDataFormat, _dragItem.ControlType.ToString());
            var data = new DataTransfer();
            data.Add(item);
            await DragDrop.DoDragDropAsync(_dragTriggerEvent!, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Toolbox] 拖拽异常: {ex}");
        }
    }

    private void OnToolboxListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragItem != null && !_dragStarted)
            ToolItemSelected?.Invoke(this, _dragItem.ControlType);
        _dragItem = null;
        _dragStarted = false;
        _dragTriggerEvent = null;
    }

    private static ToolboxItem? FindToolboxItem(object? source)
    {
        var visual = source as Visual;
        while (visual != null)
        {
            if (visual is Control c && c.DataContext is ToolboxItem ti) return ti;
            visual = visual.GetVisualParent();
        }
        return null;
    }

    // ── 事件 ──

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (Parent == null) return;
        var filter = SearchBox.Text?.Trim() ?? "";
        var previousType = (ToolboxList.SelectedItem as ToolboxItem)?.ControlType;
        ClearSearchButton.IsVisible = filter.Length > 0;
        var filtered = string.IsNullOrEmpty(filter)
            ? _allItems.ToList()
            : _allItems.Where(i =>
                    i.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    i.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    i.ControlType.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        ToolboxList.ItemsSource = filtered;
        EmptyStateBorder.IsVisible = filtered.Count == 0;
        ToolboxList.IsVisible = filtered.Count > 0;
        ResultCountText.Text = filtered.Count == 0
            ? ""
            : AppLocalization.Service.Format("tb.search.count", filtered.Count);
        if (filtered.Count > 0)
        {
            var preservedIndex = previousType.HasValue
                ? filtered.FindIndex(i => i.ControlType == previousType.Value)
                : -1;
            ToolboxList.SelectedIndex = preservedIndex >= 0
                ? preservedIndex
                : ToolboxList.SelectedIndex >= 0
                    ? Math.Clamp(ToolboxList.SelectedIndex, 0, filtered.Count - 1)
                    : 0;
            if (ToolboxList.SelectedIndex >= 0)
                ToolboxList.ScrollIntoView(ToolboxList.SelectedIndex);
        }
    }

    private void OnClearSearchClick(object? sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.L && HasOnlyModifier(e, KeyModifiers.Control))
        {
            FocusSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.A && HasOnlyModifier(e, KeyModifiers.Control))
        {
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if ((e.Key == Key.Down || e.Key == Key.Up) && ToolboxList.IsVisible && ToolboxList.ItemCount > 0)
        {
            ToolboxList.Focus();
            ToolboxList.SelectedIndex = e.Key == Key.Up
                ? ToolboxList.ItemCount - 1
                : Math.Max(0, ToolboxList.SelectedIndex);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ToolboxList.ItemCount > 0)
        {
            if (ToolboxList.SelectedIndex < 0)
                ToolboxList.SelectedIndex = 0;
            if (ToolboxList.SelectedItem is ToolboxItem item)
                ToolItemSelected?.Invoke(this, item.ControlType);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(SearchBox.Text))
                SearchBox.Clear();
            else if (ToolboxList.IsVisible && ToolboxList.ItemCount > 0)
                ToolboxList.Focus();
            e.Handled = true;
        }
    }

    private void OnToolboxListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ToolboxList.SelectedItem is ToolboxItem item)
        {
            ToolItemSelected?.Invoke(this, item.ControlType);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchBox.Focus();
            e.Handled = true;
        }
        // 输入可打印字符时：焦点转回搜索框并开始输入，实现即输即筛（type-ahead 过滤）。
        else if (IsPrintableKey(e.Key) &&
                 (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0)
        {
            SearchBox.Focus();
            // 将字符插入当前光标位置，而不是覆盖已有筛选条件。
            if (e.KeySymbol is { Length: 1 } symbol)
            {
                var text = SearchBox.Text ?? string.Empty;
                var caret = Math.Clamp(SearchBox.CaretIndex, 0, text.Length);
                SearchBox.Text = text.Insert(caret, symbol);
                SearchBox.CaretIndex = caret + symbol.Length;
            }
            e.Handled = true;
        }
    }

    private static bool HasOnlyModifier(KeyEventArgs e, KeyModifiers modifier)
        => (e.KeyModifiers & modifier) != 0 &&
           (e.KeyModifiers & ~modifier) == 0;

    // 判断是否为可输入字符键（字母/数字/空格，排除控制与导航键）。
    private static bool IsPrintableKey(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return true;
        if (key >= Key.D0 && key <= Key.D9) return true;
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return true;
        return key == Key.Space || key == Key.OemMinus || key == Key.OemPlus ||
               key == Key.OemPeriod || key == Key.OemComma || key == Key.OemQuestion;
    }
}

public record ToolboxItem(
    Geometry Icon,      // 矢量图标（Path 数据解析后的几何，viewBox 0 0 24 24）
    string DisplayName,
    string Category,
    HmiControlType ControlType
);
