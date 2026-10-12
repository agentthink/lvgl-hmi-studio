using System;
using HmiEditor.App.Resources;
using HmiEditor.Core.Reporting;

namespace HmiEditor.App.Services;

/// <summary>
/// **把定时/自动导出接到真实世界上**（阶段 186，登记 `#87`）：<see cref="AutoExportService"/> 的依赖全是回调，
/// 这里注入真的那几个 —— 设置、注册表、写盘、状态栏出口 —— 并开/关节拍。
///
/// <para><b>生命周期跟着 <see cref="ReportSourceWiring"/> 走</b>：应用级数据源注册时起步、注销时停表。
/// 这样做还有一个现实原因：<c>MainWindow.axaml.cs</c> 有**行数基线**（治理不能回涨），
/// 于是"接线"这类新增逻辑一律落在这种小文件里，调用点只留一行。</para>
///
/// <para><b>为什么用 <c>DispatcherTimer</c></b>：<c>TickOnce</c> 会读三个数据源（审计/报警/历史采样），
/// 那些集合都由 UI 线程维护 ⇒ 在 UI 线程上取数最安全（本仓 <c>_autoSaveTimer</c> 也是这个形状）。
/// 30 秒看一眼、分钟级才真的导一次，代价可以忽略。</para>
/// </summary>
public static class AutoExportWiring
{
    private static AutoExportService? _service;

    /// <summary>当前实例（判据读读数用；没起步时为 <c>null</c>）。</summary>
    public static AutoExportService? Current => _service;

    /// <summary>起步（重复调用是空操作）。</summary>
    public static void Start()
    {
        if (_service is not null) return;

        _service = new AutoExportService
        {
            Now = () => DateTimeOffset.UtcNow,
            Options = () => App.SettingsService?.AutoExport.Options() ?? default,
            RegisteredSources = () => App.ReportSources.Registered,
            MissingSources = () => App.ReportSources.Missing,
            BuildSource = source => App.ReportSources.TryBuild(source),
            Write = WriteReal,
            Report = Report,
        };

        _service.Start();
    }

    /// <summary>停表（没起步过是空操作）。</summary>
    public static void Stop()
    {
        _service?.Dispose();
        _service = null;
    }

    /// <summary>真的写盘：多源打包（四种格式 ＋ <c>index.txt</c>）；失败由异常带回给服务收口。</summary>
    private static AutoExportWriteResult WriteReal(AutoExportWriteRequest request)
    {
        var result = ReportExport.WriteMultiBundle(
            request.Directory,
            request.Items,
            request.MissingSources,
            source => AppLocalization.Service.Format(Lang.ReportAutoExportMissingSource, source),
            request.Timestamp,
            ReportExporter.FormatUtc(request.Timestamp.UtcDateTime),
            AppLocalization.Service[Lang.AuditReportGenerated],
            AppLocalization.Service.CurrentLanguage,
            AppLocalization.Service[Lang.ReportExportNoFont],
            PdfReportWriter.ResolveTypeface());

        if (result.Error is not null)
            throw new InvalidOperationException(result.Error);   // 交给服务的 catch → 状态栏，不打断节拍

        return new AutoExportWriteResult(result.Written.Count, result.Skipped);
    }

    /// <summary>结构化结论 → 一句给用户看的话；**"还没到点"不打扰用户**（空串 ⇒ 不写状态栏）。</summary>
    private static void Report(AutoExportTick tick)
    {
        var message = Describe(tick);
        if (message.Length > 0) UiOperationGuard.UiSink?.Invoke(message);
    }

    /// <summary>结论 → 文案（判据直接调它核对四种结论各自的说法）。</summary>
    public static string Describe(AutoExportTick tick) => tick.Outcome switch
    {
        AutoExportOutcome.Written =>
            AppLocalization.Service.Format(Lang.ReportAutoExportOk, tick.FilesWritten),
        AutoExportOutcome.Failed =>
            AppLocalization.Service.Format(Lang.ReportAutoExportFailed, tick.Detail ?? ""),
        AutoExportOutcome.NotConfigured => AppLocalization.Service[Lang.ReportAutoExportNotConfigured],
        AutoExportOutcome.NoSources => AppLocalization.Service[Lang.ReportAutoExportNoSources],
        _ => "",   // NotDue：每 30 秒往状态栏写一句"还没到点"是骚扰
    };
}
