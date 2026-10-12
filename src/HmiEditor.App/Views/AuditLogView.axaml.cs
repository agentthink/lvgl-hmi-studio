using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Reporting;
using HmiEditor.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace HmiEditor.App.Views;

/// <summary>
/// I3: 审计日志面板 —— 展示认证/权限/导出等高风险操作的追加式审计轨迹。
/// 订阅 AuthenticationService.AuditLogged 实时刷新；支持文本筛选与手动刷新。
/// </summary>
public partial class AuditLogView : UserControl
{
    private AuthenticationService? _auth;
    private readonly ObservableCollection<AuditLogEntry> _entries = new();
    private EventHandler<AuditLogEntry>? _onAuditLogged;

    public AuditLogView()
    {
        InitializeComponent();
        AuditList.ItemsSource = _entries;
        RefreshBtn.Click += (_, _) => Refresh();
        ExportReportBtn.Click += OnExportReport;
        ExportBundleBtn.Click += OnExportBundle;
        DesignerBtn.Click += OnEditReportTemplate;
        FilterBox.TextChanged += (_, _) => ApplyFilter();
    }

    /// <summary>附加认证服务：订阅审计事件，并加载当前内存日志。</summary>
    public void Attach(AuthenticationService auth)
    {
        _auth = auth;

        // 幂等：重复 Attach 前先移除旧委托，避免多次订阅导致重复刷新。
        if (_onAuditLogged != null)
            _auth.AuditLogged -= _onAuditLogged;
        _onAuditLogged = (_, entry) =>
        {
            // 事件可能来自非 UI 线程；Post 到 UI 线程保证控件访问安全
            Dispatcher.UIThread.Post(() => AddEntry(entry));
        };
        _auth.AuditLogged += _onAuditLogged;
        Refresh();
    }

    // 与 Attach 对称：Dock 面板分离时解除订阅，避免服务持有引用导致泄漏。
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_auth != null && _onAuditLogged != null)
            _auth.AuditLogged -= _onAuditLogged;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>重新从服务加载日志（最新在前）。</summary>
    public void Refresh()
    {
        if (_auth == null) return;

        var all = _auth.GetAuditLog(1000);

        // 批量重载：先分离 ItemsSource，避免 Clear+Add 触发逐条 UI 通知（千级条目）。
        AuditList.ItemsSource = null;
        _entries.Clear();
        foreach (var entry in all)
            _entries.Add(entry);

        if (string.IsNullOrEmpty(FilterBox.Text?.Trim()))
            AuditList.ItemsSource = _entries;
        else
            ApplyFilter();
    }

    private void AddEntry(AuditLogEntry entry)
    {
        _entries.Insert(0, entry);
        ApplyFilter();
    }

    /// <summary>
    /// **导出报表**（阶段 172，报表能力 MVP 的第一条数据源）：把**当前列表里显示的那些**审计条目
    /// 导成 CSV（数据）或**自包含 HTML**（人看/打印）。
    ///
    /// <para>「导出所见的那些」而不是"库里全部"：用户在筛选框里过滤后点导出，期望拿到的就是看到的那些
    /// —— 与面板的筛选语义一致（<c>AuditList.ItemsSource</c> 就是那一份）。</para>
    ///
    /// <para>失败走 <see cref="UiOperationGuard"/>（本视图没有自己的状态行 ⇒ 回落到主窗口状态栏 ＋ 落盘），
    /// 成功也报一句（否则"点了没反应"）。产物格式按所选扩展名判：<c>.html</c> ⇒ HTML，其余 ⇒ CSV。
    /// 生成时间在**调用点**取一次并显式格式化（UTC ＋ <c>Z</c>）—— 导出器本身不读时钟，产物可判。</para>
    /// </summary>
    /// <summary>
    /// **导出报表**（阶段 172 立、阶段 174 把写盘逻辑提成 <see cref="ReportExport"/>）：
    /// 把**当前列表里显示的那些**审计条目导成 CSV / Excel / PDF / 自包含 HTML。
    ///
    /// <para>「导出所见的那些」而不是"库里全部"：用户在筛选框里过滤后点导出，期望拿到的就是看到的那些
    /// —— 与面板的筛选语义一致（<c>AuditList.ItemsSource</c> 就是那一份）。</para>
    ///
    /// <para>选格式与写盘都在 <see cref="ReportExport"/>（**唯一一处**）；失败走
    /// <see cref="UiOperationGuard"/> 回落到主窗口状态栏 ＋ 落盘。</para>
    /// </summary>
    /// <summary>
    /// **导出报表包**（阶段 179）：把**当前列表里显示的那些**审计条目一次性打成四种格式（＋<c>index.txt</c>）
    /// 落到用户选的目录里。
    ///
    /// <para>与"导出报表"的分工：那一条会先问**范围/列**（精细挑一份）；这一条**全列、全时段、四种格式**
    /// 一起出（留档/交付），因此**不弹**那两个对话框 —— 两条路都各有各的用处。</para>
    /// </summary>
    private void OnExportBundle(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpExportBundle, async () =>
        {
            var entries = ((AuditList.ItemsSource as IEnumerable<AuditLogEntry>) ?? _entries).ToList();
            // ⚠ **本路径刻意不套模板**：报表包是"留档/交付"（全列全时段），模板只管「导出报表」那一条
            //     —— 判据 ReportDesignerGuardTests 钉着这个分工，别顺手加一行。
            var table = AuditLogReport.From(entries, AuditReportText.Localizer);
            var message = await ReportExport.SaveBundleAsync(
                this, ReportSourceKeys.Audit, table, entries.Count,
                AppLocalization.Service[Lang.AuditReportExportEmpty]);

            if (message is not null) UiOperationGuard.UiSink?.Invoke(message);
        });

    private void OnExportReport(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpExportAuditReport, async () =>
        {
            var entries = ((AuditList.ItemsSource as IEnumerable<AuditLogEntry>) ?? _entries).ToList();
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;   // 无宿主（判据/设计器）⇒ 静默返回，不抛

            if (await ReportExport.ChooseRangeAsync(this) is not { } rangeSelection) return;   // 取消 ⇒ 中止导出
            var range = ReportRangeOptions.Compute(rangeSelection, DateTimeOffset.UtcNow);
            // 先套**报表设计器**存的列布局（顺序/改名/隐藏），再让用户在这次导出里勾选
            var table = ReportExport.ApplyTemplate(
                AuditLogReport.From(entries, AuditReportText.Localizer, range), ReportSourceKeys.Audit);
            if (await ReportExport.ChooseColumnsAsync(this, table) is not { } chosen) return;   // 取消 ⇒ 中止导出
            table = chosen;
            var message = await ReportExport.SaveAsync(
                topLevel, table, Lang.AuditReportSaveTitle,
                AppLocalization.Service[Lang.AuditReportExportEmpty], entries.Count);

            if (message is not null) UiOperationGuard.UiSink?.Invoke(message);
        });

    /// <summary>
    /// **报表设计器**（阶段 183）：编辑本数据源的列布局模板（顺序/列名/隐藏）。
    /// 只要**列清单**、不要行 —— 与导出共用 <c>AuditLogReport.Columns</c> 这一份构造。
    /// </summary>
    private void OnEditReportTemplate(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpReportDesigner, async () =>
            await ReportExport.EditTemplateAsync(
                this, ReportSourceKeys.Audit, AuditLogReport.Columns(AuditReportText.Localizer)));

    private void ApplyFilter()
    {
        var filter = FilterBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(filter))
        {
            AuditList.ItemsSource = _entries;
            return;
        }

        AuditList.ItemsSource = _entries.Where(e =>
            e.Username.Contains(filter, System.StringComparison.OrdinalIgnoreCase)
            || e.Action.ToString().Contains(filter, System.StringComparison.OrdinalIgnoreCase)
            || e.Detail.Contains(filter, System.StringComparison.OrdinalIgnoreCase));
    }
}
