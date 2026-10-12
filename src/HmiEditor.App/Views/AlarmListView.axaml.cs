using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Reporting;
using HmiEditor.Core.Services;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App;

namespace HmiEditor.App.Views;

/// <summary>
/// 报警列表（实时监控）对话框 —— 实时查看报警记录、确认报警。
/// </summary>
public partial class AlarmListView : ChromeWindow
{
    private readonly AlarmEngine _engine;
    private List<AlarmRecord> _allRecords = new();
    private string _stateFilter = AppLocalization.Service[Lang.AlarmFilterAll];
    private string _severityFilter = AppLocalization.Service[Lang.AlarmLevelAll];

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public AlarmListView()
    {
        _engine = null!;
        InitializeComponent();
    }

    public AlarmListView(AlarmEngine engine) : this()
    {
        _engine = engine;

        // 订阅报警事件 —— 实时刷新
        _engine.AlarmTriggered += OnAlarmEvent;
        _engine.AlarmAcknowledged += OnAlarmEvent;
        _engine.AlarmReturned += OnAlarmEvent;

        CmbStateFilter.SelectionChanged += OnFilterChanged;
        CmbSeverityFilter.SelectionChanged += OnFilterChanged;
        BtnAcknowledge.Click += OnAcknowledge;
        BtnClearReturned.Click += OnClearReturned;
        BtnExportReport.Click += OnExportReport;
        BtnExportBundle.Click += OnExportBundle;
        BtnReportDesigner.Click += OnEditReportTemplate;

        Opened += OnOpenedRefresh;
    }

    private void OnOpenedRefresh(object? sender, EventArgs e) => RefreshData();

    protected override void OnClosed(EventArgs e)
    {
        // 无参构造是 XAML 设计器路径（_engine 为 null，也没有订阅过任何事件）——
        // 退订必须容得下它，否则关闭设计器预览就是一次 NRE。见 WindowViewSmokeUiTests。
        if (_engine is not null)
        {
            // 生命周期对称：窗口关闭时退订引擎事件，避免引擎仍持有引用
            // 导致回调作用在已销毁的视觉树上（空引用/泄漏）。
            _engine.AlarmTriggered -= OnAlarmEvent;
            _engine.AlarmAcknowledged -= OnAlarmEvent;
            _engine.AlarmReturned -= OnAlarmEvent;
        }

        CmbStateFilter.SelectionChanged -= OnFilterChanged;
        CmbSeverityFilter.SelectionChanged -= OnFilterChanged;
        BtnAcknowledge.Click -= OnAcknowledge;
        BtnClearReturned.Click -= OnClearReturned;
        BtnExportReport.Click -= OnExportReport;
        BtnExportBundle.Click -= OnExportBundle;
        BtnReportDesigner.Click -= OnEditReportTemplate;
        Opened -= OnOpenedRefresh;

        base.OnClosed(e);
    }

    private void OnAlarmEvent(object? sender, AlarmRecord record)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(RefreshData);
    }

    private void RefreshData()
    {
        _allRecords = _engine.GetAllRecords().ToList();
        ApplyFilters();
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CmbStateFilter.SelectedItem is ComboBoxItem stateItem)
            _stateFilter = stateItem.Content?.ToString() ?? AppLocalization.Service[Lang.AlarmFilterAll];

        if (CmbSeverityFilter.SelectedItem is ComboBoxItem sevItem)
            _severityFilter = sevItem.Content?.ToString() ?? AppLocalization.Service[Lang.AlarmLevelAll];

        ApplyFilters();
    }

    /// <summary>
    /// **当前筛选条件下的记录** —— 面板显示的就是这一份，导出（<see cref="OnExportReport"/>）也用它
    /// （⇒ "导出所见"不是另写一套筛选；两处口径分叉是这类功能的经典缺陷）。
    /// </summary>
    private List<AlarmRecord> FilteredRecords()
    {
        IEnumerable<AlarmRecord> filtered = _allRecords;

        if (_stateFilter == AppLocalization.Service[Lang.AlarmFilterActive])
            filtered = filtered.Where(r => r.State == AlarmState.Active);
        else if (_stateFilter == AppLocalization.Service[Lang.AlarmFilterAcked])
            filtered = filtered.Where(r => r.State == AlarmState.Acknowledged);
        else if (_stateFilter == AppLocalization.Service[Lang.AlarmFilterCleared])
            filtered = filtered.Where(r => r.State == AlarmState.Returned);

        if (_severityFilter == AppLocalization.Service[Lang.AlarmLevelInfo])
            filtered = filtered.Where(r => r.Severity == AlarmSeverity.Info);
        else if (_severityFilter == AppLocalization.Service[Lang.AlarmLevelWarning])
            filtered = filtered.Where(r => r.Severity == AlarmSeverity.Warning);
        else if (_severityFilter == AppLocalization.Service[Lang.AlarmLevelCritical])
            filtered = filtered.Where(r => r.Severity == AlarmSeverity.Critical);
        else if (_severityFilter == AppLocalization.Service[Lang.AlarmLevelEmergency])
            filtered = filtered.Where(r => r.Severity == AlarmSeverity.Emergency);

        return filtered.OrderByDescending(r => r.OccurredAt).ToList();
    }

    private void ApplyFilters()
    {
        RenderAlarmList(FilteredRecords());
        UpdateStatusBar();
    }

    private void RenderAlarmList(List<AlarmRecord> records)
    {
        AlarmList.Items.Clear();
        foreach (var record in records)
        {
            AlarmList.Items.Add(CreateAlarmItem(record));
        }
        EmptyHint.IsVisible = records.Count == 0;
    }

    private static ListBoxItem CreateAlarmItem(AlarmRecord record)
    {
        var severityColor = GetSeverityColor(record.Severity);
        var indicatorColor = new SolidColorBrush(severityColor);

        var severityLetter = record.Severity switch
        {
            AlarmSeverity.Info => AppLocalization.Service[Lang.AlarmSevShortInfo],
            AlarmSeverity.Warning => AppLocalization.Service[Lang.AlarmSevShortWarning],
            AlarmSeverity.Critical => AppLocalization.Service[Lang.AlarmSevShortCritical],
            AlarmSeverity.Emergency => AppLocalization.Service[Lang.AlarmSevShortEmergency],
            _ => "?",
        };

        var stateText = record.State switch
        {
            AlarmState.Active => AppLocalization.Service["alarm.filter.active"],
            AlarmState.Acknowledged => AppLocalization.Service["alarm.filter.acked"],
            AlarmState.Returned => AppLocalization.Service["alarm.filter.cleared"],
            _ => AppLocalization.Service[Lang.AlarmStateInactive],
        };

        var timeText = record.OccurredAt.ToLocalTime().ToString("MM-dd HH:mm:ss");
        var triggerText = record.RawValue ?? record.TriggerValue.ToString("F2");
        var ackByText = record.AcknowledgedBy ?? "-";

        // 严重级别指示器 —— 彩色方块 + 字母
        var indicator = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(4),
            Background = indicatorColor,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = severityLetter,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            },
        };

        var timeBlock = new TextBlock
        {
            Text = timeText,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        var nameBlock = new TextBlock
        {
            Text = record.AlarmName,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        var stateBlock = new TextBlock
        {
            Text = stateText,
            Foreground = GetStateBrush(record.State),
            FontSize = 13,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        var triggerBlock = new TextBlock
        {
            Text = triggerText,
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 13,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        var ackBlock = new TextBlock
        {
            Text = ackByText,
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 13,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("28,100,*,100,80,80"),
            Margin = new Thickness(8, 6),
        };

        contentGrid.Children.Add(indicator);
        Grid.SetColumn(indicator, 0);

        contentGrid.Children.Add(timeBlock);
        Grid.SetColumn(timeBlock, 1);

        contentGrid.Children.Add(nameBlock);
        Grid.SetColumn(nameBlock, 2);

        contentGrid.Children.Add(stateBlock);
        Grid.SetColumn(stateBlock, 3);

        contentGrid.Children.Add(triggerBlock);
        Grid.SetColumn(triggerBlock, 4);

        contentGrid.Children.Add(ackBlock);
        Grid.SetColumn(ackBlock, 5);

        return new ListBoxItem
        {
            Content = contentGrid,
            Tag = record,
            Padding = new Thickness(0),
        };
    }

    private static Color GetSeverityColor(AlarmSeverity severity) => severity switch
    {
        AlarmSeverity.Info => Color.Parse("#1e3a5f"),
        AlarmSeverity.Warning => Color.Parse("#5c4a1e"),
        AlarmSeverity.Critical => Color.Parse("#5c2e1e"),
        AlarmSeverity.Emergency => Color.Parse("#5c1e1e"),
        _ => Color.Parse("#1a2332"),
    };

    private static IBrush GetStateBrush(AlarmState state) => state switch
    {
        AlarmState.Active => new SolidColorBrush(Color.Parse("#ef4444")),
        AlarmState.Acknowledged => new SolidColorBrush(Color.Parse("#fbbf24")),
        AlarmState.Returned => new SolidColorBrush(Color.Parse("#22c55e")),
        _ => new SolidColorBrush(Color.Parse("#7c8aa0")),
    };

    private void UpdateStatusBar()
    {
        var total = _allRecords.Count;
        var active = _allRecords.Count(r => r.State == AlarmState.Active);
        var acked = _allRecords.Count(r => r.State == AlarmState.Acknowledged);
        StatusText.Text = AppLocalization.Service.Format(Lang.AlarmStatusSummary, total, active, acked);
    }

    /// <summary>**导出报表包**（阶段 179）：当前筛选后的报警记录 × 四种格式 ＋ <c>index.txt</c>；全列全时段。</summary>
    private void OnExportBundle(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpExportBundle, async () =>
        {
            var records = FilteredRecords().ToList();
            // ⚠ **本路径刻意不套模板**（报表包＝留档：全列全时段）；模板只管「导出报表」那一条。
            var table = AlarmReport.From(records, AlarmReportText.Localizer);
            var message = await ReportExport.SaveBundleAsync(
                this, ReportSourceKeys.Alarm, table, records.Count,
                AppLocalization.Service[Lang.AlarmReportExportEmpty]);

            if (message is not null) UiOperationGuard.UiSink?.Invoke(message);
        });

    /// <summary>
    /// **导出报警记录报表**（阶段 174，报表能力第二片）：把**当前筛选后显示的那些**报警记录导成
    /// CSV / Excel / PDF / 自包含 HTML。
    ///
    /// <para>「导出所见的那些」与审计面板同一条语义（用户在两个筛选框里过滤后，拿到的就是看到的那些）。
    /// 选格式与写盘都在 <see cref="ReportExport"/>（**唯一一处**）—— 本视图只提供数据源与文案键。</para>
    /// </summary>
    private void OnExportReport(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpExportAlarmReport, async () =>
        {
            var records = FilteredRecords().ToList();
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;   // 无宿主（判据/设计器）⇒ 静默返回，不抛

            if (await ReportExport.ChooseRangeAsync(this) is not { } rangeSelection) return;   // 取消 ⇒ 中止导出
            var range = ReportRangeOptions.Compute(rangeSelection, DateTimeOffset.UtcNow);
            // 先套**报表设计器**存的列布局（顺序/改名/隐藏），再让用户在这次导出里勾选
            var table = ReportExport.ApplyTemplate(
                AlarmReport.From(records, AlarmReportText.Localizer, range), ReportSourceKeys.Alarm);
            if (await ReportExport.ChooseColumnsAsync(this, table) is not { } chosen) return;   // 取消 ⇒ 中止导出
            table = chosen;

            var message = await ReportExport.SaveAsync(
                topLevel, table, Lang.AlarmReportSaveTitle,
                AppLocalization.Service[Lang.AlarmReportExportEmpty], records.Count);

            if (message is not null) UiOperationGuard.UiSink?.Invoke(message);
        });

    /// <summary>**报表设计器**（阶段 183）：编辑本数据源的列布局模板（顺序/列名/隐藏）。</summary>
    private void OnEditReportTemplate(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpReportDesigner, async () =>
            await ReportExport.EditTemplateAsync(
                this, ReportSourceKeys.Alarm, AlarmReport.Columns(AlarmReportText.Localizer)));

    private void OnAcknowledge(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (AlarmList.SelectedItem is ListBoxItem item && item.Tag is AlarmRecord record)
        {
            if (_engine.AcknowledgeAlarm(record.Id))
            {
                RefreshData();
            }
        }
    }

    private void OnClearReturned(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _engine.ClearReturnedRecords();
        RefreshData();
    }
}
