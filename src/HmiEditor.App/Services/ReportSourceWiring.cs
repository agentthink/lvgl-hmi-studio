using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Reporting;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// **把应用级数据源注册进 <see cref="ReportSourceRegistry"/>**（阶段 185，登记 `#87` 第二片）。
///
/// <para>为什么单开一个文件、而不是写在 <c>MainWindow</c> 里：那两处接线本身只有两三行，但
/// "为什么在这里注册、委托体读的是哪一刻的状态、谁负责注销"这段话必须写在代码旁边 ——
/// 而 <c>MainWindow.axaml.cs</c> 有**行数基线**（治理不能回涨，见 <c>SingleFileSizeGuardTests</c>）。
/// 于是照本仓的老做法：**逻辑与解释一起搬出去，调用点只留一行**。</para>
///
/// <para><b>委托体读的是"当下"</b>（不是在注册时抓一份快照）：定时导出（186）取数那一刻的服务状态
/// 才是用户要的；抓快照会让"导出的永远是启动那一刻的数据"，而且**不报错**。</para>
///
/// <para><b>只由生产路径调用</b>：<c>MainWindow()</c>（设计器/冒烟面）不注册 —— 那一路上
/// <c>_settingsService</c> 都是 <c>null!</c>，注册进去只会让注册表里挂着一个半成品窗口。</para>
/// </summary>
public static class ReportSourceWiring
{
    /// <summary>注册审计与报警两个应用级数据源（由 <c>MainWindow</c> 的生产构造调用）。</summary>
    public static void RegisterAppLevelSources(AuthenticationService authService, AlarmEngine alarmEngine)
    {
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(alarmEngine);

        App.ReportSources.Register(ReportSourceKeys.Audit,
            () => AuditLogReport.From(authService.GetAuditLog(), AuditReportText.Localizer));
        App.ReportSources.Register(ReportSourceKeys.Alarm,
            () => AlarmReport.From(alarmEngine.GetAllRecords(), AlarmReportText.Localizer));

        // 数据源就位之后才起步定时导出：否则第一拍会撞上"一个源都没有"。
        AutoExportWiring.Start();
    }

    /// <summary>
    /// 注销上面那两个源（由 <c>CleanupOnShutdown</c> 调用）：注销之后委托不再持有已释放的服务。
    /// 历史采样那条由 <c>TrendConfigView</c> 自己注销（见那边的 <c>StartRecording</c> / <c>OnClosed</c>）。
    /// </summary>
    public static void UnregisterAppLevelSources()
    {
        // 先停表再注销：反过来会让竞态中的那一拍读到"正在被撤走的"源
        AutoExportWiring.Stop();

        App.ReportSources.Unregister(ReportSourceKeys.Audit);
        App.ReportSources.Unregister(ReportSourceKeys.Alarm);
    }
}
