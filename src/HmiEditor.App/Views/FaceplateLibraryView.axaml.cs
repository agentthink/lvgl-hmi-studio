using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Data;
using Avalonia.VisualTree;
using HmiEditor.Core.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace HmiEditor.App.Views;

/// <summary>
/// Faceplate 模板库视图 —— 纯 XAML 视觉树 + ItemsSource 绑定模式。
///
/// 设计原则（"不管布局变化"）：
///   - 视觉树由 AXAML 一次性定义，永不重建（无 RecreateVisualTree）。
///   - 数据通过 ObservableCollection + ItemsSource 绑定驱动。
///   - Dock 重组时 View 实例不变，绑定不变；内容自然保持。
/// </summary>
public partial class FaceplateLibraryView : UserControl
{
    public event EventHandler<ControlTemplate>? TemplateSelected;
    public event EventHandler<ControlTemplate>? InstantiateRequested;
    public event EventHandler? CreateTemplateRequested;
    public event EventHandler<ControlTemplate>? EditTemplateRequested;
    public event EventHandler<ControlTemplate>? DeleteTemplateRequested;
    public event EventHandler? AiTemplateRequested;

    private readonly ObservableCollection<ControlTemplate> _allTemplates = new();
    private ControlTemplate? _selectedTemplate;
    private string _searchFilter = "";

    // ── 拖拽状态 ──
    private ControlTemplate? _dragTemplate;
    private Point _dragStartPoint;
    private bool _dragStarted;
    private PointerPressedEventArgs? _dragTriggerEvent;
    private const double DragThreshold = 4.0;

    /// <summary>
    /// 持有引用此 View 的 Tool 实例。由 HmiDockFactory 在创建布局时设置。
    /// </summary>
    public Dock.Model.Mvvm.Controls.Tool? HostTool { get; set; }

    public FaceplateLibraryView()
    {
        InitializeComponent();

        // ItemsSource 绑定 —— 数据驱动，Dock 重组时绑定不变
        TemplateListBox.ItemsSource = _allTemplates;

        // 模板卡片 ItemTemplate（代码构建，避免改 XAML）
        TemplateListBox.ItemTemplate = new FuncDataTemplate<ControlTemplate>((t, _) => BuildTemplateCard(t));

        // 转发事件到 DockLayoutState 聚合器，确保 Dock 重建后仍能到达 MainWindow
        InstantiateRequested += (_, t) => DockLayoutState.RaiseFaceplateInstantiateRequested(this, t);
        CreateTemplateRequested += (_, _) => DockLayoutState.RaiseFaceplateCreateTemplateRequested(this, EventArgs.Empty);
        EditTemplateRequested += (_, t) => DockLayoutState.RaiseFaceplateEditTemplateRequested(this, t);
        DeleteTemplateRequested += (_, t) => DockLayoutState.RaiseFaceplateDeleteTemplateRequested(this, t);
        AiTemplateRequested += (_, _) => DockLayoutState.RaiseFaceplateAiTemplateRequested(this, EventArgs.Empty);

        // 双击实例化
        TemplateListBox.DoubleTapped += (_, _) =>
        {
            if (TemplateListBox.SelectedItem is ControlTemplate t)
                InstantiateRequested?.Invoke(this, t);
        };

        // 注：SearchBox / TemplateListBox / 面板内 4 个按钮的事件接线**只在 .axaml 里声明一次**。
        // 这里曾经再 += 一遍（同一个 handler 接两路）—— Avalonia 的 += 不去重，两路都会执行：
        // AI 模板按钮会因此弹 4 个对话框（双接线 × 双订阅，见 docs 登记表 #43）。
        // tests 侧有静态守卫 scripts/check-xaml-duplicate-wiring.py 守这条。

        // 拖拽支持 —— 从模板列表拖拽到画布
        AttachDragHandlers(TemplateListBox);
    }

    // ── 拖拽 ──

    private void AttachDragHandlers(ListBox listBox)
    {
        listBox.AddHandler(InputElement.PointerPressedEvent, OnTemplateListPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        listBox.AddHandler(InputElement.PointerMovedEvent, OnTemplateListPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        listBox.AddHandler(InputElement.PointerReleasedEvent, OnTemplateListPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnTemplateListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var template = FindTemplateItem(e.Source);
        if (template == null) return;
        _dragTemplate = template;
        _dragStartPoint = e.GetPosition(this);
        _dragStarted = false;
        _dragTriggerEvent = e;
        e.Handled = true;
    }

    private async void OnTemplateListPointerMoved(object? sender, PointerEventArgs e)
    {
        try
        {
            if (_dragTemplate == null || _dragStarted) return;
            var pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _dragStartPoint.X) <= DragThreshold && Math.Abs(pos.Y - _dragStartPoint.Y) <= DragThreshold)
                return;
            _dragStarted = true;
            var item = DataTransferItem.Create(DesignCanvas.TemplateDragDataFormat, _dragTemplate.Id);
            var data = new DataTransfer();
            data.Add(item);
            await DragDrop.DoDragDropAsync(_dragTriggerEvent!, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FaceplateLibrary] 拖拽异常: {ex}");
        }
    }

    private void OnTemplateListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // 未拖拽时为单击 → 选中（ListBox 已自动处理选中）
        _dragTemplate = null;
        _dragStarted = false;
        _dragTriggerEvent = null;
    }

    private static ControlTemplate? FindTemplateItem(object? source)
    {
        var visual = source as Avalonia.Visual;
        while (visual != null)
        {
            if (visual is Control c && c.DataContext is ControlTemplate t) return t;
            visual = visual.GetVisualParent();
        }
        return null;
    }

    /// <summary>从外部加载模板数据（ObservableCollection 自动通知 UI）。</summary>
    public void LoadTemplates(IEnumerable<ControlTemplate> templates)
    {
        _allTemplates.Clear();
        foreach (var t in templates)
            _allTemplates.Add(t);
        ApplySearchFilter();
    }

    /// <summary>
    /// 附加到视觉树时：从 DockLayoutState 自初始化模板数据。
    /// ViewLocator 工厂模式每次 Build 创建新实例，MainWindow 构造期的 LoadTemplates
    /// 只会写入未显示的旧实例；实际显示的视图在此处从共享状态恢复数据。
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DockLayoutState.Templates != null)
            LoadTemplates(DockLayoutState.Templates);
    }

    public void RefreshTemplate(ControlTemplate template)
    {
        var index = _allTemplates.ToList().FindIndex(t => t.Id == template.Id);
        if (index >= 0)
            _allTemplates[index] = template;
    }

    public ControlTemplate? GetSelectedTemplate() => _selectedTemplate;

    // ── 搜索过滤 ──

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (Parent == null) return;
        _searchFilter = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        var filtered = string.IsNullOrEmpty(_searchFilter)
            ? _allTemplates.AsEnumerable()
            : _allTemplates.Where(t =>
                t.Name.ToLowerInvariant().Contains(_searchFilter) ||
                t.Category.ToLowerInvariant().Contains(_searchFilter) ||
                t.Description.ToLowerInvariant().Contains(_searchFilter));

        TemplateListBox.ItemsSource = filtered;
    }

    // ── 模板卡片视觉树 ──

    private static Control BuildTemplateCard(ControlTemplate? template)
    {
        var t = template!;
        var paramCount = t.Parameters.Count;
        var hasParams = paramCount > 0;
        var catColor = GetCategoryColor(t.Category);

        return new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(8, 8, 10, 8),
            BorderBrush = HmiEditor.App.Theme.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("32,*,Auto"),
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Border
                    {
                        Width = 32, Height = 32,
                        CornerRadius = new CornerRadius(4),
                        Background = HmiEditor.App.Theme.BgDeep,
                        BorderBrush = catColor,
                        BorderThickness = new Thickness(1),
                        Child = new Avalonia.Controls.Shapes.Path
                        {
                            Data = StreamGeometry.Parse(GetCategoryIconPath(t.Category)),
                            Width = 20, Height = 20,
                            Stretch = Stretch.Uniform,
                            Stroke = catColor,
                            StrokeThickness = 1.2,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    new StackPanel
                    {
                        [Grid.ColumnProperty] = 1,
                        Margin = new Thickness(10, 0, 4, 0),
                        Spacing = 1,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = t.Name,
                                Foreground = HmiEditor.App.Theme.TextPrimary,
                                FontSize = 12,
                                FontWeight = FontWeight.Medium
                            },
                            new TextBlock
                            {
                                Text = $"{t.Category} · v{t.Version}" +
                                       (hasParams ? $" · {paramCount}P" : ""),
                                Foreground = HmiEditor.App.Theme.TextMuted,
                                FontSize = 10
                            }
                        }
                    },
                    hasParams ? new Border
                    {
                        [Grid.ColumnProperty] = 2,
                        Background = HmiEditor.App.Theme.BgSelected,
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(6, 2),
                        Child = new TextBlock
                        {
                            Text = $"{paramCount}P",
                            FontSize = 10,
                            Foreground = catColor,
                            FontFamily = new FontFamily("Consolas, Cascadia Code, monospace")
                        }
                    } : new Border { [Grid.ColumnProperty] = 2, Width = 0 }
                }
            }
        };
    }

    // 模板类别 → 矢量图标（Path 数据，viewBox 0 0 24 24，跨字体可靠渲染）
    private static string GetCategoryIconPath(string category)
    {
        var loc = AppLocalization.Service;
        if (string.Equals(category, loc["fp.cat.industrial"], StringComparison.Ordinal)) return "M12,5 A7,7 0 1 0 12,19 A7,7 0 1 0 12,5 Z M12,9 A3,3 0 1 0 12,15 A3,3 0 1 0 12,9 Z"; // 工业：齿轮（同心圆）
        if (string.Equals(category, loc["fp.cat.piping"], StringComparison.Ordinal)) return "M5,8 L19,8 M5,16 L19,16";                                    // 管路：双线
        if (string.Equals(category, loc["fp.cat.instrument"], StringComparison.Ordinal)) return "M4,15 A8,8 0 0 1 20,15 M12,15 L12,10";                     // 仪表：弧+指针
        if (string.Equals(category, loc["fp.cat.safety"], StringComparison.Ordinal)) return "M12,4 L21,20 L3,20 Z M12,9.5 L12,14 M12,16.5 L12,17.5";          // 安全：三角+感叹
        if (string.Equals(category, loc["fp.cat.general"], StringComparison.Ordinal)) return "M4,4 L20,4 L20,20 L4,20 Z M4,12 L20,12 M12,4 L12,20";           // 通用：网格
        return "M4,4 L20,4 L20,20 L4,20 Z M8,8 L16,8 L16,16 L8,16 Z";                                                                                       // 其他：嵌套方框
    }

    // 模板类别 → 语义色画刷（卡片图标边框、图标笔触、参数徽章统一使用分类色）
    private static IBrush GetCategoryColor(string category)
    {
        var loc = AppLocalization.Service;
        if (string.Equals(category, loc["fp.cat.industrial"], StringComparison.Ordinal)) return HmiEditor.App.Theme.CatIndustrial;
        if (string.Equals(category, loc["fp.cat.piping"], StringComparison.Ordinal)) return HmiEditor.App.Theme.CatPiping;
        if (string.Equals(category, loc["fp.cat.instrument"], StringComparison.Ordinal)) return HmiEditor.App.Theme.CatInstrument;
        if (string.Equals(category, loc["fp.cat.safety"], StringComparison.Ordinal)) return HmiEditor.App.Theme.CatSafety;
        if (string.Equals(category, loc["fp.cat.general"], StringComparison.Ordinal)) return HmiEditor.App.Theme.CatGeneral;
        return HmiEditor.App.Theme.TextSecondary; // 其他：灰色
    }

    // ── 事件处理 ──

    private void OnTemplateSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Parent == null) return;
        if (TemplateListBox.SelectedItem is ControlTemplate template)
        {
            _selectedTemplate = template;
            TemplateSelected?.Invoke(this, template);
        }
        else
        {
            _selectedTemplate = null;
        }

        BtnEditTemplate.IsEnabled = _selectedTemplate != null;
        BtnDeleteTemplate.IsEnabled = _selectedTemplate != null;
    }

    private void OnNewTemplateClicked(object? sender, RoutedEventArgs e) =>
        CreateTemplateRequested?.Invoke(this, EventArgs.Empty);

    private void OnAiTemplateClicked(object? sender, RoutedEventArgs e) =>
        AiTemplateRequested?.Invoke(this, EventArgs.Empty);

    private void OnEditTemplateClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedTemplate != null)
            EditTemplateRequested?.Invoke(this, _selectedTemplate);
    }

    /// <summary>
    /// 删除图元模板。本视图没有自己的状态行，失败由 UiOperationGuard 回落到
    /// 主窗口状态栏（并落盘日志），不再出现「点了没反应」。
    /// </summary>
    private void OnDeleteTemplateClicked(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpDeleteTemplate, async () =>
        {
            if (_selectedTemplate == null) return;

            // 删除为不可逆操作，先确认。
            var ok = await AppDialogs.ConfirmAsync(
                this, AppLocalization.Service.Format("faceplate.confirm.delete", _selectedTemplate.Name));
            if (!ok) return;

            DeleteTemplateRequested?.Invoke(this, _selectedTemplate);
        });
}
