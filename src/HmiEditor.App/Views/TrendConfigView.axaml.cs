using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Reporting;
using HmiEditor.Core.Services;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App;
using HmiEditor.App.Controls;

namespace HmiEditor.App.Views;

/// <summary>
/// 趋势图配置和显示对话框 —— 管理趋势配置、笔配置，并简化绘制趋势曲线。
/// </summary>
public partial class TrendConfigView : ChromeWindow
{
    private readonly ObservableCollection<TrendConfig> _trends;
    private TrendConfig? _currentTrend;
    private TrendPen? _currentPen;
    private DataLogger? _logger;
    private bool _isRecording;
    private bool _suppressPenEvents;
    private bool _suppressDisplayEvents;
    private System.Threading.Timer? _refreshTimer;

    /// <summary>预定义颜色列表（#RRGGBB 格式）</summary>
    private static readonly string[] PredefinedColors =
    [
        "#38bdf8", "#f87171", "#34d399", "#fbbf24",
        "#a78bfa", "#fb923c", "#2dd4bf", "#f472b6",
        "#818cf8", "#c084fc", "#f97316", "#10b981"
    ];

    /// <summary>
    /// 时间窗口选项（毫秒）。⚠ 刻意用**方法**而不是静态数组：标签要按**当前语言**解析，
    /// 而静态字段初始化只跑一次 ⇒ 会把语言冻结在首次使用那一刻（同族缺陷见登记表 §12.3 的
    /// <c>ConfigFieldInventory</c>「静态字段里提前解析」。本视图此前正是这个形状）。
    /// </summary>
    private static (string Label, int Ms)[] TimeWindowOptions() =>
    [
        (AppLocalization.Service.Format(Lang.TrendWindowSec, 30), 30000),
        (AppLocalization.Service.Format(Lang.TrendWindowSec, 60), 60000),
        (AppLocalization.Service.Format(Lang.TrendWindowSec, 120), 120000),
        (AppLocalization.Service.Format(Lang.TrendWindowSec, 300), 300000),
    ];

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public TrendConfigView()
    {
        _trends = null!;
        InitializeComponent();
    }

    public TrendConfigView(ObservableCollection<TrendConfig> trends) : this()
    {
        _trends = trends;

        InitializeColorCombo();
        InitializeTimeWindowCombo();

        // 默认选中第一个趋势
        if (_trends.Count > 0)
            _currentTrend = _trends[0];

        RefreshTrendList();
        RefreshPenList();
        UpdatePenEditor();
        UpdateTrendDisplay();

        BtnAddTrend.Click += (_, _) => OnAddTrend();
        BtnDeleteTrend.Click += (_, _) => OnDeleteTrend();
        BtnAddPen.Click += (_, _) => OnAddPen();
        BtnDeletePen.Click += (_, _) => OnDeletePen();
        BtnToggleRecord.Click += (_, _) => OnToggleRecord();
        BtnExportReport.Click += OnExportReport;
        BtnExportBundle.Click += OnExportBundle;
        BtnReportDesigner.Click += OnEditReportTemplate;

        // 订阅 ChartPanel 的绘制事件
        ChartPanel.PointerPressed += (_, _) => { };

        // 定时刷新图表（当记录时）
        _refreshTimer = new System.Threading.Timer(_ =>
        {
            if (_isRecording && _logger != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => InvalidateChart());
            }
        }, null, 0, 500);
    }

    // ──────────────────────────────────────────────
    // 初始化
    // ──────────────────────────────────────────────

    private void InitializeColorCombo()
    {
        PenColorCombo.Items.Clear();
        foreach (var color in PredefinedColors)
        {
            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };

            var swatch = new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(0),
                Background = ParseColor(color)
            };

            var text = new TextBlock
            {
                Text = color,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            panel.Children.Add(swatch);
            panel.Children.Add(text);

            var item = new ComboBoxItem
            {
                Content = panel,
                Tag = color,
                Background = Brushes.Transparent
            };
            PenColorCombo.Items.Add(item);
        }

        if (PenColorCombo.Items.Count > 0)
            PenColorCombo.SelectedIndex = 0;
    }

    private void InitializeTimeWindowCombo()
    {
        TimeWindowCombo.Items.Clear();
        foreach (var (label, ms) in TimeWindowOptions())
        {
            TimeWindowCombo.Items.Add(new ComboBoxItem
            {
                Content = label,
                Tag = ms,          // ⚠ 取值靠 Tag，不靠下标 —— 见 SyncTimeWindowComboToModel 的"自定义"项
                Background = Brushes.Transparent,
                Foreground = HmiEditor.App.Theme.TextPrimary
            });
        }

        // 默认选中 60 秒
        TimeWindowCombo.SelectedIndex = 1;
    }

    /// <summary>
    /// 把下拉同步到**模型值**（阶段 124，#67②）。
    ///
    /// <para><b>旧行为：</b>只在这 4 个预设档里找匹配；`TimeWindowMs` 不在表里（例如工程文件里的
    /// 45000）时一个都不匹配 ⇒ <c>SelectedIndex</c> **保持上一条趋势的选中项** ⇒ 下拉显示的值
    /// 不是模型的值；更糟的是用户点同一项不会再触发事件（值没变）⇒ **纠正都纠正不了**。</para>
    ///
    /// <para><b>现在：</b>先移除上次可能加的自定义项（防累积）→ 命中预设就选它 → 否则**追加**
    /// 一个「自定义（N ms）」项并选中它。⇒ 显示永远与模型一致，且不改用户数据。</para>
    ///
    /// <para>整段写在抑噪窗口内：程序性写入不许反过来触发 <see cref="OnTimeWindowChanged"/>
    /// （与同文件 <c>_suppressDisplayEvents</c> / <c>_suppressPenEvents</c> 同一纪律）。</para>
    /// </summary>
    private void SyncTimeWindowComboToModel()
    {
        if (_currentTrend == null) return;

        _suppressDisplayEvents = true;
        try
        {
            // 移除上一次同步加进来的自定义项（Tag 不是预设档的就是它）
            for (int i = TimeWindowCombo.Items.Count - 1; i >= 0; i--)
            {
                if (TimeWindowCombo.Items[i] is ComboBoxItem existing &&
                    existing.Tag is int tagMs &&
                    !TimeWindowOptions().Any(o => o.Ms == tagMs))
                {
                    TimeWindowCombo.Items.RemoveAt(i);
                }
            }

            var modelMs = _currentTrend.TimeWindowMs;
            foreach (var item in TimeWindowCombo.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag is int ms && ms == modelMs)
                {
                    TimeWindowCombo.SelectedItem = cbi;
                    return;
                }
            }

            var custom = new ComboBoxItem
            {
                Content = AppLocalization.Service.Format(Lang.TrendTimeWindowCustom, modelMs),
                Tag = modelMs,
                Background = Brushes.Transparent,
                Foreground = HmiEditor.App.Theme.TextPrimary
            };
            TimeWindowCombo.Items.Add(custom);
            TimeWindowCombo.SelectedItem = custom;
        }
        finally
        {
            _suppressDisplayEvents = false;
        }
    }

    // ──────────────────────────────────────────────
    // 趋势列表操作
    // ──────────────────────────────────────────────

    private void RefreshTrendList()
    {
        TrendList.Items.Clear();

        foreach (var trend in _trends)
        {
            var item = CreateTrendListItem(trend);
            TrendList.Items.Add(item);

            if (_currentTrend != null && trend.Id == _currentTrend.Id)
                TrendList.SelectedItem = item;
        }

        UpdateTrendDisplay();
    }

    private ListBoxItem CreateTrendListItem(TrendConfig trend)
    {
        var nameText = new TextBlock
        {
            Text = trend.Name,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var infoText = new TextBlock
        {
            Text = AppLocalization.Service.Format(Lang.TrendPenCount, trend.Pens.Count),
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 11
        };

        var enabledBadge = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = trend.IsEnabled
                ? new SolidColorBrush(UiPalette.ConnectedBright)
                : new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var infoPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children = { nameText, infoText }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(10, 6)
        };
        grid.Children.Add(enabledBadge);
        Grid.SetColumn(enabledBadge, 0);
        grid.Children.Add(infoPanel);
        Grid.SetColumn(infoPanel, 1);

        return new ListBoxItem
        {
            Content = grid,
            Tag = trend,
            Padding = new Thickness(0)
        };
    }

    private void OnAddTrend()
    {
        var trend = new TrendConfig
        {
            Name = AppLocalization.Service.Format(Lang.TrendDefaultName, _trends.Count + 1),
            TimeWindowMs = 60000,
            MaxPoints = 600,
            Pens = new List<TrendPen>(),
            ShowLegend = true,
            ShowGrid = true,
            ShowXAxis = true,
            ShowYAxis = true,
            IsEnabled = true,
            IsRealtime = true
        };

        _trends.Add(trend);
        _currentTrend = trend;
        RefreshTrendList();
        RefreshPenList();
        UpdatePenEditor();
    }

    private void OnDeleteTrend()
    {
        if (_currentTrend == null) return;

        _trends.Remove(_currentTrend);
        _currentTrend = _trends.Count > 0 ? _trends[0] : null;
        _currentPen = null;
        RefreshTrendList();
        RefreshPenList();
        UpdatePenEditor();
        UpdateTrendDisplay();
    }

    private void OnTrendSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ListBoxItem item && item.Tag is TrendConfig trend)
        {
            _currentTrend = trend;
            _currentPen = trend.Pens.Count > 0 ? trend.Pens[0] : null;
            RefreshPenList();
            UpdatePenEditor();
            UpdateTrendDisplay();
        }
    }

    // ──────────────────────────────────────────────
    // 笔列表操作
    // ──────────────────────────────────────────────

    private void RefreshPenList()
    {
        PenList.Items.Clear();

        if (_currentTrend == null) return;

        foreach (var pen in _currentTrend.Pens)
        {
            var item = CreatePenListItem(pen);
            PenList.Items.Add(item);

            if (_currentPen != null && pen.VariableId == _currentPen.VariableId &&
                pen.Label == _currentPen.Label)
                PenList.SelectedItem = item;
        }
    }

    private ListBoxItem CreatePenListItem(TrendPen pen)
    {
        var colorSwatch = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(0),
            Background = ParseColor(pen.Color),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var labelText = new TextBlock
        {
            Text = string.IsNullOrEmpty(pen.Label) ? AppLocalization.Service[Lang.TrendPenUnnamed] : pen.Label,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var varText = new TextBlock
        {
            Text = pen.VariableId,
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 11,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        var chkEnabled = new CheckBox
        {
            IsChecked = pen.IsEnabled,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        chkEnabled.IsCheckedChanged += (_, _) =>
        {
            pen.IsEnabled = chkEnabled.IsChecked ?? true;
            InvalidateChart();
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            Margin = new Thickness(8, 4)
        };
        grid.Children.Add(colorSwatch);
        Grid.SetColumn(colorSwatch, 0);
        grid.Children.Add(labelText);
        Grid.SetColumn(labelText, 1);
        grid.Children.Add(varText);
        Grid.SetColumn(varText, 2);
        grid.Children.Add(chkEnabled);
        Grid.SetColumn(chkEnabled, 3);

        return new ListBoxItem
        {
            Content = grid,
            Tag = pen,
            Padding = new Thickness(0)
        };
    }

    private void OnAddPen()
    {
        if (_currentTrend == null) return;

        // 找一个未使用的颜色
        var usedColors = _currentTrend.Pens.Select(p => p.Color).ToHashSet();
        var color = PredefinedColors.FirstOrDefault(c => !usedColors.Contains(c)) ?? "#38bdf8";

        var pen = new TrendPen
        {
            Label = AppLocalization.Service.Format(Lang.TrendPenDefaultName, _currentTrend.Pens.Count + 1),
            VariableId = "",
            Color = color,
            LineWidth = 2,
            ShowPoints = false,
            IsEnabled = true
        };

        _currentTrend.Pens.Add(pen);
        _currentPen = pen;
        RefreshPenList();
        UpdatePenEditor();
        InvalidateChart();
    }

    private void OnDeletePen()
    {
        if (_currentTrend == null || _currentPen == null) return;

        _currentTrend.Pens.Remove(_currentPen);
        _currentPen = _currentTrend.Pens.Count > 0 ? _currentTrend.Pens[0] : null;
        RefreshPenList();
        UpdatePenEditor();
        InvalidateChart();
    }

    private void OnPenSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ListBoxItem item && item.Tag is TrendPen pen)
        {
            _currentPen = pen;
            UpdatePenEditor();
        }
    }

    // ──────────────────────────────────────────────
    // 笔属性编辑
    // ──────────────────────────────────────────────

    private void UpdatePenEditor()
    {
        _suppressPenEvents = true;

        if (_currentPen == null)
        {
            PenLabelInput.Text = "";
            PenVariableInput.Text = "";
            PenLineWidthInput.Text = "";
            PenShowPointsCheck.IsChecked = false;
            PenYMinInput.Text = "";
            PenYMaxInput.Text = "";
            PenEnabledCheck.IsChecked = false;
            PenColorPreview.Background = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            PenLabelInput.IsEnabled = false;
            PenVariableInput.IsEnabled = false;
            PenLineWidthInput.IsEnabled = false;
            PenShowPointsCheck.IsEnabled = false;
            PenYMinInput.IsEnabled = false;
            PenYMaxInput.IsEnabled = false;
            PenEnabledCheck.IsEnabled = false;
            PenColorCombo.IsEnabled = false;
        }
        else
        {
            PenLabelInput.Text = _currentPen.Label;
            PenVariableInput.Text = _currentPen.VariableId;
            PenLineWidthInput.Text = _currentPen.LineWidth.ToString();
            PenShowPointsCheck.IsChecked = _currentPen.ShowPoints;
            PenYMinInput.Text = _currentPen.YAxisMin?.ToString() ?? "";
            PenYMaxInput.Text = _currentPen.YAxisMax?.ToString() ?? "";
            PenEnabledCheck.IsChecked = _currentPen.IsEnabled;
            PenColorPreview.Background = ParseColor(_currentPen.Color);

            // 选中颜色
            var colorIndex = Array.IndexOf(PredefinedColors, _currentPen.Color);
            PenColorCombo.SelectedIndex = colorIndex >= 0 ? colorIndex : 0;

            PenLabelInput.IsEnabled = true;
            PenVariableInput.IsEnabled = true;
            PenLineWidthInput.IsEnabled = true;
            PenShowPointsCheck.IsEnabled = true;
            PenYMinInput.IsEnabled = true;
            PenYMaxInput.IsEnabled = true;
            PenEnabledCheck.IsEnabled = true;
            PenColorCombo.IsEnabled = true;
        }

        _suppressPenEvents = false;
    }

    private void OnPenPropertyChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_suppressPenEvents || _currentPen == null) return;

        _currentPen.Label = PenLabelInput.Text ?? "";
        _currentPen.VariableId = PenVariableInput.Text ?? "";

        if (int.TryParse(PenLineWidthInput.Text, out var lw) && lw > 0)
            _currentPen.LineWidth = lw;

        _currentPen.ShowPoints = PenShowPointsCheck.IsChecked ?? false;

        if (double.TryParse(PenYMinInput.Text, out var yMin))
            _currentPen.YAxisMin = yMin;
        else
            _currentPen.YAxisMin = null;

        if (double.TryParse(PenYMaxInput.Text, out var yMax))
            _currentPen.YAxisMax = yMax;
        else
            _currentPen.YAxisMax = null;

        _currentPen.IsEnabled = PenEnabledCheck.IsChecked ?? true;

        RefreshPenList();
        InvalidateChart();
    }

    private void OnPenColorChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressPenEvents || _currentPen == null) return;

        if (PenColorCombo.SelectedItem is ComboBoxItem item && item.Tag is string colorStr)
        {
            _currentPen.Color = colorStr;
            PenColorPreview.Background = ParseColor(colorStr);
            RefreshPenList();
            InvalidateChart();
        }
    }

    // ──────────────────────────────────────────────
    // 时间窗口
    // ──────────────────────────────────────────────

    private void OnTimeWindowChanged(object? sender, SelectionChangedEventArgs e)
    {
        // 与同族（OnDisplayOptionChanged / OnPenColorChanged）一致的抑噪守卫。
        // ⚠ #67① 复核结论：**今天无可观察差异**（程序性赋值只在"选中值 == 模型值"时发生 ⇒ 空写），
        //   补守卫是为一致性；本视图真正的行为判据是 #67② 的"表外值 ⇒ 自定义项"。
        if (_suppressDisplayEvents || _currentTrend == null) return;

        // ⚠ 取值靠 **Tag**、不靠下标：表外值时下拉里会多一个「自定义（N ms）」项，下标映射会错位。
        if (TimeWindowCombo.SelectedItem is ComboBoxItem item && item.Tag is int ms)
        {
            _currentTrend.TimeWindowMs = ms;
            InvalidateChart();
        }
    }

    // ──────────────────────────────────────────────
    // 显示选项
    // ──────────────────────────────────────────────

    private void OnDisplayOptionChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // `_suppressDisplayEvents`：挡住上面 UpdateTrendDisplay() 的**程序性**同步（详见那里的注释）
        if (_suppressDisplayEvents || _currentTrend == null) return;

        _currentTrend.ShowLegend = ChkShowLegend.IsChecked ?? true;
        _currentTrend.ShowGrid = ChkShowGrid.IsChecked ?? true;
        _currentTrend.ShowXAxis = ChkShowXAxis.IsChecked ?? true;
        _currentTrend.ShowYAxis = ChkShowYAxis.IsChecked ?? true;

        InvalidateChart();
    }

    // ──────────────────────────────────────────────
    // 记录控制
    // ──────────────────────────────────────────────

    private void OnToggleRecord()
    {
        if (_currentTrend == null) return;

        if (_isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    /// <summary>**导出报表包**（阶段 179）：采样数据 × 四种格式 ＋ <c>index.txt</c>；全列全时段。</summary>
    private void OnExportBundle(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpExportBundle, async () =>
        {
            var samples = _logger?.GetAllSamples() ?? (IReadOnlyList<TrendSample>)Array.Empty<TrendSample>();
            var variableIds = _logger?.Config.VariableIds ?? EnabledVariableIds();
            // ⚠ **本路径刻意不套模板**（报表包＝留档：全列全时段）；模板只管「导出报表」那一条。
            var table = BuildHistoryTable();
            var message = await ReportExport.SaveBundleAsync(
                this, ReportSourceKeys.History, table, samples.Count,
                AppLocalization.Service[Lang.TrendReportExportEmpty]);

            if (message is not null) UiOperationGuard.UiSink?.Invoke(message);
        });

    /// <summary>
    /// **导出历史数据报表**（阶段 175，报表能力第三片）：把**记录缓冲里的采样**导成
    /// CSV / Excel / PDF / 自包含 HTML。
    ///
    /// <para>列名取 <see cref="DataLogger.Config"/> 的 <c>VariableIds</c>（**采样时**的顺序，不是当前画笔的）——
    /// 用户中途改画笔不会改已采样值的对齐，用当前画笔会让列与值**错位且不报错**；
    /// 还没开记录（没有 logger）时退回当前趋势的启用画笔。</para>
    ///
    /// <para>选格式与写盘都在 <see cref="ReportExport"/>（唯一一处）；行数封顶在
    /// <see cref="HistoryReport"/>（默认只导最近 <see cref="HistoryReport.DefaultMaxRows"/> 条）。</para>
    /// </summary>
    private void OnExportReport(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpExportTrendReport, async () =>
        {
            var samples = _logger?.GetAllSamples() ?? (IReadOnlyList<TrendSample>)Array.Empty<TrendSample>();
            var variableIds = _logger?.Config.VariableIds ?? EnabledVariableIds();

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;   // 无宿主（判据/设计器）⇒ 静默返回，不抛

            // CSV 走**设备列序**那一份（时间基写在表头里）；xlsx/pdf/html 仍走报表列
            if (await ReportExport.ChooseRangeAsync(this) is not { } rangeSelection) return;   // 取消 ⇒ 中止导出
            var range = ReportRangeOptions.Compute(rangeSelection, DateTimeOffset.UtcNow);
            // 先套**报表设计器**存的列布局（顺序/改名/隐藏），再让用户在这次导出里勾选
            var table = ReportExport.ApplyTemplate(
                HistoryReport.From(
                    samples, variableIds, HistoryReportText.Localizer,
                    csvOverride: _logger?.ExportCsvBytes(), range: range),
                ReportSourceKeys.History);
            if (await ReportExport.ChooseColumnsAsync(this, table) is not { } chosen) return;   // 取消 ⇒ 中止导出
            table = chosen;
            var message = await ReportExport.SaveAsync(
                topLevel, table, Lang.TrendReportSaveTitle,
                AppLocalization.Service[Lang.TrendReportExportEmpty], samples.Count);

            if (message is not null) UiOperationGuard.UiSink?.Invoke(message);
        });

    /// <summary>
    /// **历史数据的那张表**（阶段 185）：报表包导出与"运行期数据源注册表"共用**同一处**构造 ——
    /// 两处各写一份的话，定时导出与手动导出会给出**不同的列**（列名取变量顺序，一漂移就错位，且不报错）。
    ///
    /// <para>列名取 <see cref="DataLogger.Config"/> 的 <c>VariableIds</c>（**采样时**的顺序，
    /// 不是当前画笔的）；还没开记录（没有 logger）时退回当前趋势的启用画笔。</para>
    /// </summary>
    private ReportTable BuildHistoryTable()
    {
        var samples = _logger?.GetAllSamples() ?? (IReadOnlyList<TrendSample>)Array.Empty<TrendSample>();
        var variableIds = _logger?.Config.VariableIds ?? EnabledVariableIds();
        return HistoryReport.From(samples, variableIds, HistoryReportText.Localizer,
            csvOverride: _logger?.ExportCsvBytes());
    }

    /// <summary>
    /// **报表设计器**（阶段 183）：编辑历史数据报表的列布局模板（顺序/列名/隐藏）。
    /// 列是**动态的**：时间列 ＋ 每个变量一列 —— 变量顺序取**记录时**那一份（同导出那条口径）。
    /// </summary>
    private void OnEditReportTemplate(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpReportDesigner, async () =>
            await ReportExport.EditTemplateAsync(
                this, ReportSourceKeys.History,
                HistoryReport.Columns(_logger?.Config.VariableIds ?? EnabledVariableIds(), HistoryReportText.Localizer)));

    /// <summary>
    /// **当前趋势里启用的画笔变量 Id**（顺序即采样值的对齐顺序）—— "开记录"与"导报表"共用这一处，
    /// 不许两处各推一份（顺序一旦分叉，报表的列与值就**错位**，而且不会报错）。
    /// </summary>
    private List<string> EnabledVariableIds() =>
        _currentTrend?.Pens
            .Where(p => p.IsEnabled && !string.IsNullOrEmpty(p.VariableId))
            .Select(p => p.VariableId)
            .Distinct()
            .ToList() ?? new List<string>();

    private void StartRecording()
    {
        if (_currentTrend == null) return;
        if (_currentTrend.Pens.Count == 0) return;

        var varIds = EnabledVariableIds();
        if (varIds.Count == 0) return;

        var logConfig = new DataLogConfig
        {
            SampleIntervalMs = 1000,
            MaxRecords = _currentTrend.MaxPoints,
            VariableIds = varIds,
            FullPolicy = LogFullPolicy.OverwriteOldest,
            IsEnabled = true
        };

        _logger?.Dispose();
        _logger = new DataLogger(logConfig);
        _logger.SampleAdded += (_, sample) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => InvalidateChart());
        };
        _logger.Start();

        // **历史数据进注册表**（阶段 185）：此刻起"这个源"才有数据 ⇒ 定时/自动导出能取到它。
        // 委托读的是**当下**的缓冲（`BuildHistoryTable` 每次现取），不是注册时的快照。
        App.ReportSources.Register(ReportSourceKeys.History, BuildHistoryTable);

        _isRecording = true;
        BtnToggleRecord.Content = AppLocalization.Service[Lang.TrendStopRecord];
        BtnToggleRecord.Classes.Set("recording", true);
        ChartEmptyHint.IsVisible = false;
    }

    private void StopRecording()
    {
        _isRecording = false;

        _logger?.Stop();
        BtnToggleRecord.Content = AppLocalization.Service["trend.start.record"];
        BtnToggleRecord.Classes.Set("recording", false);

        if (_logger != null && _logger.SampleCount == 0)
            ChartEmptyHint.IsVisible = true;
    }

    // ──────────────────────────────────────────────
    // 趋势图更新
    // ──────────────────────────────────────────────

    private void UpdateTrendDisplay()
    {
        if (_currentTrend != null)
        {
            TrendTitle.Text = _currentTrend.Name;
            ChartEmptyHint.IsVisible = (_logger == null || _logger.SampleCount == 0) && !_isRecording;

            // 同步复选框
            // ⚠ **必须抑噪**：这四枚是**双向**的（`IsCheckedChanged` → OnDisplayOptionChanged），
            //   而下面是一枚一枚地写 ⇒ **写到某一枚**就会触发处理器，处理器却**一次读全部四枚**
            //   ⇒ 把「控制件当时的残值」当成用户意图写回模型，随后那几枚又从**已被写坏**的模型刷回来
            //   ⇒ 不再触发（自我巩固）⇒ 错误被静默锁死。实测两处现场（阶段 114 首跑
            //   `.workbuddy/tmp/s114-post.log`）：① 构造函数里这一遍 ⇒ 第一条趋势的
            //   「网格 / X 轴 / Y 轴」被写成**关**（打开对话框本身就把用户的趋势改了）；
            //   ② BtnAddTrend ⇒ 新趋势的「X 轴 / Y 轴」被写成**关**（`关X轴趋势` 1→2）。
            //   ⇒ 被写坏的是**哪几位取决于残值**，形状不固定 ⇒ 只能靠守卫兜，不能靠人肉 review。
            //   与 UpdatePenEditor() 的 `_suppressPenEvents` 同一个道理 —— 那里作者加了守卫，这里漏了。
            _suppressDisplayEvents = true;
            ChkShowLegend.IsChecked = _currentTrend.ShowLegend;
            ChkShowGrid.IsChecked = _currentTrend.ShowGrid;
            ChkShowXAxis.IsChecked = _currentTrend.ShowXAxis;
            ChkShowYAxis.IsChecked = _currentTrend.ShowYAxis;
            _suppressDisplayEvents = false;

            // 同步时间窗口（表外值会追加一个「自定义（N ms）」项，见 SyncTimeWindowComboToModel）
            SyncTimeWindowComboToModel();
        }
        else
        {
            TrendTitle.Text = AppLocalization.Service["trend.chart"];
            ChartEmptyHint.IsVisible = true;
        }

        InvalidateChart();
    }

    /// <summary>
    /// 刷新趋势图表绘制。
    /// </summary>
    private void InvalidateChart()
    {
        if (ChartPanel is TrendChartPanel chartPanel)
        {
            chartPanel.Invalidate();
        }
    }

    // ──────────────────────────────────────────────
    // 工具方法
    // ──────────────────────────────────────────────

    /// <summary>
    /// 将 #RRGGBB 颜色字符串解析为 SolidColorBrush。
    /// </summary>
    private static SolidColorBrush ParseColor(string? colorStr)
    {
        if (string.IsNullOrEmpty(colorStr) || colorStr.Length < 7 || colorStr[0] != '#')
            return new SolidColorBrush(Color.FromRgb(56, 189, 248));

        try
        {
            var r = Convert.ToByte(colorStr[1..3], 16);
            var g = Convert.ToByte(colorStr[3..5], 16);
            var b = Convert.ToByte(colorStr[5..7], 16);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(56, 189, 248));
        }
    }

    /// <summary>
    /// 在 Panel 的 Render 事件中绘制简化趋势图。
    /// 在窗口初始化后，替换 ChartPanel 为可绘制的自定义面板。
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 替换 ChartPanel 为自定义绘制面板
        var parent = ChartPanel.Parent as Grid;
        if (parent != null)
        {
            var idx = parent.Children.IndexOf(ChartPanel);
            if (idx >= 0)
            {
                var customPanel = new TrendChartPanel
                {
                    Name = "ChartDrawingPanel",
                    ChartBackground = Brushes.Transparent
                };
                customPanel.SetBindingData(_currentTrend, _logger, _currentTrend?.Pens ?? new());
                parent.Children.RemoveAt(idx);
                parent.Children.Insert(idx, customPanel);
                ChartPanel = customPanel;

                // 同步空提示的 Z-Index
                ChartEmptyHint.IsVisible = true;
            }
        }

        InvalidateChart();
    }

    // ──────────────────────────────────────────────
    // 清理
    // ──────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
        // 注销在**释放之前**：注销之后定时导出再也不会拿到一个已被释放的 logger。
        // （停记录 ≠ 注销：停记录后缓冲还在、仍可导出；只有窗口关掉（logger 释放）才注销。）
        App.ReportSources.Unregister(ReportSourceKeys.History);

        _logger?.Dispose();
        _logger = null;
        base.OnClosed(e);
    }
}

/// <summary>
/// 自定义绘制面板 —— 用于渲染趋势折线图。
/// </summary>
internal class TrendChartPanel : Control
{
    private TrendConfig? _trend;
    private DataLogger? _logger;
    private List<TrendPen> _pens = new();

    public IBrush? ChartBackground { get; set; }

    public void SetBindingData(TrendConfig? trend, DataLogger? logger, List<TrendPen> pens)
    {
        _trend = trend;
        _logger = logger;
        _pens = pens;
    }

    public void Invalidate()
    {
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var chartRect = new Rect(0, 0, bounds.Width, bounds.Height);

        // 背景
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(13, 21, 38)), chartRect);

        // 边距
        const double marginLeft = 50;
        const double marginRight = 16;
        const double marginTop = 16;
        const double marginBottom = 30;

        var plotRect = new Rect(
            marginLeft, marginTop,
            bounds.Width - marginLeft - marginRight,
            bounds.Height - marginTop - marginBottom
        );

        if (plotRect.Width <= 0 || plotRect.Height <= 0) return;

        // 绘制边框
        context.DrawRectangle(
            new Pen(new SolidColorBrush(Color.FromRgb(42, 58, 82)), 1),
            plotRect
        );

        // 网格
        var gridColor = new SolidColorBrush(Color.FromRgb(30, 41, 59));
        var gridPen = new Pen(gridColor, 0.5);

        // 水平网格线（4 条）
        for (int i = 1; i <= 4; i++)
        {
            var y = plotRect.Y + plotRect.Height * i / 5;
            context.DrawLine(gridPen, new Point(plotRect.X, y), new Point(plotRect.Right, y));
        }

        // 垂直网格线（6 条）
        for (int i = 1; i <= 6; i++)
        {
            var x = plotRect.X + plotRect.Width * i / 7;
            context.DrawLine(gridPen, new Point(x, plotRect.Y), new Point(x, plotRect.Bottom));
        }

        // Y 轴标签
        var labelBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        var labelPen = new Pen(labelBrush, 0.5);
        var yTicks = new[] { 100, 80, 60, 40, 20, 0 };
        for (int i = 0; i < yTicks.Length; i++)
        {
            var y = plotRect.Bottom - plotRect.Height * i / (yTicks.Length - 1);
            context.DrawLine(labelPen, new Point(plotRect.X, y), new Point(plotRect.X - 4, y));

            var ftb = new FormattedText(
                yTicks[i].ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                10,
                labelBrush
            );
            context.DrawText(ftb, new Point(plotRect.X - 44, y - 7));
        }

        // 绘制折线
        if (_logger == null || _logger.SampleCount == 0)
        {
            // 无数据——在图表区域内绘制提示文字
            var hintText = new FormattedText(
                AppLocalization.Service["trend.run.sim"],
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                14,
                new SolidColorBrush(Color.FromRgb(71, 85, 105))
            );
            context.DrawText(hintText, new Point(
                plotRect.X + plotRect.Width / 2 - 100,
                plotRect.Y + plotRect.Height / 2 - 10
            ));
            return;
        }

        // 获取数据
        var samples = _logger.GetAllSamples();
        if (samples.Count < 2) return;

        // 计算实际显示的时间窗口
        var timeRange = _trend?.TimeWindowMs ?? 60000;
        var now = samples[^1].Timestamp;
        var windowStart = now.AddMilliseconds(-timeRange);

        // 筛选窗口内的数据
        var windowedSamples = samples
            .Where(s => s.Timestamp >= windowStart)
            .ToList();

        if (windowedSamples.Count < 2) return;

        var firstTime = windowedSamples[0].Timestamp;
        var lastTime = windowedSamples[^1].Timestamp;
        var timeSpan = (lastTime - firstTime).TotalMilliseconds;
        if (timeSpan <= 0) timeSpan = 1;

        // 对每支启用的笔绘制折线
        int penIndex = 0;
        foreach (var pen in _pens)
        {
            if (!pen.IsEnabled) { penIndex++; continue; }
            if (penIndex >= (windowedSamples[0].Values?.Count ?? 0)) { penIndex++; continue; }

            var penColor = ParseColor(pen.Color);
            var strokeThickness = pen.LineWidth > 0 ? pen.LineWidth : 1;
            var strokePen = new Pen(penColor, strokeThickness);

            // 计算 Y 轴范围
            var values = windowedSamples
                .Where(s => penIndex < s.Values.Count)
                .Select(s => s.Values[penIndex])
                .ToList();

            if (values.Count == 0) { penIndex++; continue; }

            var yMin = pen.YAxisMin ?? values.Min();
            var yMax = pen.YAxisMax ?? values.Max();
            if (Math.Abs(yMax - yMin) < 0.001) { yMax = yMin + 10; yMin -= 1; }

            var yRange = yMax - yMin;

            // 绘制折线
            var points = new List<Point>();
            for (int i = 0; i < windowedSamples.Count; i++)
            {
                if (penIndex >= windowedSamples[i].Values.Count) continue;

                var t = (windowedSamples[i].Timestamp - firstTime).TotalMilliseconds / timeSpan;
                var x = plotRect.X + t * plotRect.Width;

                var val = windowedSamples[i].Values[penIndex];
                var yNorm = (val - yMin) / yRange;
                var y = plotRect.Bottom - yNorm * plotRect.Height;

                points.Add(new Point(x, y));
            }

            if (points.Count >= 2)
            {
                // 绘制线段
                for (int i = 0; i < points.Count - 1; i++)
                {
                    context.DrawLine(strokePen, points[i], points[i + 1]);
                }

                // 绘制数据点
                if (pen.ShowPoints)
                {
                    var dotBrush = new SolidColorBrush(penColor.Color);
                    foreach (var pt in points)
                    {
                        context.DrawEllipse(dotBrush, null, new Point(pt.X, pt.Y), 2.5, 2.5);
                    }
                }
            }

            penIndex++;
        }
    }

    private static SolidColorBrush ParseColor(string? colorStr)
    {
        if (string.IsNullOrEmpty(colorStr) || colorStr.Length < 7 || colorStr[0] != '#')
            return new SolidColorBrush(Color.FromRgb(56, 189, 248));

        try
        {
            var r = Convert.ToByte(colorStr[1..3], 16);
            var g = Convert.ToByte(colorStr[3..5], 16);
            var b = Convert.ToByte(colorStr[5..7], 16);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(56, 189, 248));
        }
    }
}
