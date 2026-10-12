using System;
using System.Collections.Generic;
using Avalonia.Threading;
using HmiEditor.Core.Reporting;

namespace HmiEditor.App.Services;

/// <summary>自动导出的一组选项（由设置来；判据直接给一个纯数据）。</summary>
/// <param name="Enabled">功能开关。</param>
/// <param name="IntervalMinutes">间隔（分钟）；≤ 0 视为关闭。</param>
/// <param name="Directory">目标目录；空 ＝ 没配置 ⇒ 不导（**不弹选择器**：自动导出不该在后台弹窗）。</param>
public readonly record struct AutoExportOptions(bool Enabled, int IntervalMinutes, string Directory)
{
    /// <summary>三个条件都满足才写盘。</summary>
    public bool CanRun => Enabled && IntervalMinutes > 0 && !string.IsNullOrWhiteSpace(Directory);
}

/// <summary>一次自动导出要写的一个数据源。</summary>
public sealed record ReportBundleItem(string Source, ReportTable Table);

/// <summary>一拍自动导出的**结构化**结论（文案由组合根按语言拼 —— 服务本身不认识语言）。</summary>
public enum AutoExportOutcome
{
    /// <summary>还没到点（什么都没做）。</summary>
    NotDue,

    /// <summary>到点了，但开关/间隔/目录没配全 ⇒ 不导。</summary>
    NotConfigured,

    /// <summary>到点了，但一个数据源都没有 ⇒ 不写空包。</summary>
    NoSources,

    /// <summary>写成功了。</summary>
    Written,

    /// <summary>写盘抛了（异常消息在 <c>Detail</c> 里）—— **不打断后续节拍**。</summary>
    Failed,
}

/// <summary>一拍的结论。</summary>
/// <param name="Outcome">结论。</param>
/// <param name="FilesWritten">写出的文件数（<see cref="AutoExportOutcome.Written"/> 时有意义）。</param>
/// <param name="Detail">诊断细节（失败时的异常消息等）—— 不是本地化文案。</param>
public readonly record struct AutoExportTick(AutoExportOutcome Outcome, int FilesWritten, string? Detail);

/// <summary>一次写盘请求（目录 ＋ 要写的源 ＋ **缺的源** ＋ 时间戳）。</summary>
public readonly record struct AutoExportWriteRequest(
    string Directory,
    IReadOnlyList<ReportBundleItem> Items,
    IReadOnlyList<string> MissingSources,
    DateTimeOffset Timestamp);

/// <summary>写盘结果。</summary>
/// <param name="FilesWritten">写出的文件数。</param>
/// <param name="Skipped">记进索引 skipped 段的条目（渲染不出来 / 源当前不可用）。</param>
public readonly record struct AutoExportWriteResult(int FilesWritten, IReadOnlyList<string> Skipped);

/// <summary>
/// **定时/自动导出服务**（阶段 186，登记 `#87` 第三片）：把"到点没到点"（<see cref="AutoExportSchedule"/>）
/// 与"写哪些源、写到哪"接起来。
///
/// <para><b>依赖全部是回调</b>（时钟、选项、注册表、写盘）⇒ 判据给假的即可，
/// 不必等一个周期、不必碰真磁盘、不碰线程。真的那套由 <see cref="AutoExportWiring"/> 注入。</para>
///
/// <para><b>三条口径</b>：</para>
/// <list type="number">
/// <item><b>每个到期点只跑一次</b>：<see cref="TickOnce"/> 一旦认定"到点"，就**先**把
/// <c>LastRun</c> 记成此刻 —— 无论写成功还是写失败。失败后不重跑这一拍（否则每 30 秒的节拍会变成
/// 每 30 秒重试一次的刷屏机），下一个到期点自然重试。</item>
/// <item><b>没有源就不写空包</b>：一个数据源都没注册（或都取不到表）⇒ 报
/// <see cref="AutoExportOutcome.NoSources"/>，**不**产出只剩 <c>index.txt</c> 的目录。</item>
/// <item><b>写盘异常不打断节拍</b>：捕获后报 <see cref="AutoExportOutcome.Failed"/>（带异常消息），
/// 定时器继续走 —— 后台服务把异常抛到 Dispatcher 上只会静默杀掉整条链。</item>
/// </list>
///
/// <para><b>缺的源如实进索引</b>：<see cref="AutoExportWriteRequest.MissingSources"/> 由注册表给，
/// 写盘侧把它记进 <c>index.txt</c> 的 skipped 段 —— "这次少导了什么"必须在产物里看得见。</para>
/// </summary>
public sealed class AutoExportService : IDisposable
{
    /// <summary>现在几点（判据给固定值 ⇒ 不读墙上时钟）。</summary>
    public required Func<DateTimeOffset> Now { get; init; }

    /// <summary>当前选项。</summary>
    public required Func<AutoExportOptions> Options { get; init; }

    /// <summary>当前可用的数据源（顺序按 <see cref="ReportSourceKeys.All"/>）。</summary>
    public required Func<IReadOnlyList<string>> RegisteredSources { get; init; }

    /// <summary>已知但当前不可用的数据源（进索引的 skipped 段）。</summary>
    public required Func<IReadOnlyList<string>> MissingSources { get; init; }

    /// <summary>取某个源的表；取不到 ⇒ <c>null</c>。</summary>
    public required Func<string, ReportTable?> BuildSource { get; init; }

    /// <summary>写盘（真的那套是 <c>ReportExport.WriteMultiBundle</c>）。</summary>
    public required Func<AutoExportWriteRequest, AutoExportWriteResult> Write { get; init; }

    /// <summary>结论出口（组合根在这里拼一句话给状态栏/日志）。</summary>
    public Action<AutoExportTick>? Report { get; init; }

    /// <summary>节拍长度（默认 30 秒 —— 到期粒度是分钟级，"多久看一眼"不必更密）。</summary>
    public TimeSpan TickInterval { get; init; } = TimeSpan.FromSeconds(30);

    private DateTimeOffset? _anchor;
    private DateTimeOffset? _lastRun;
    private DispatcherTimer? _timer;

    /// <summary>成功写盘的次数（判据与界面读数）。</summary>
    public int Runs { get; private set; }

    /// <summary>上次导出时刻（<c>null</c> ＝ 还没导过）。</summary>
    public DateTimeOffset? LastRunUtc => _lastRun;

    /// <summary>最后一拍的结论。</summary>
    public AutoExportTick? LastTick { get; private set; }

    /// <summary>起跑锚点（第一次 <see cref="Start"/> 或第一次 <see cref="TickOnce"/> 时记下）。</summary>
    public DateTimeOffset? AnchorUtc => _anchor;

    /// <summary>开节拍（UI 线程；判据不调它，直接调 <see cref="TickOnce"/>）。</summary>
    public void Start()
    {
        _anchor ??= Now();

        _timer ??= new DispatcherTimer { Interval = TickInterval };
        _timer.Tick -= OnTimerTick;
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    /// <summary>停节拍（不重置 <c>LastRun</c>：再开时不该把已经导过的那个周期重导一遍）。</summary>
    public void Stop() => _timer?.Stop();

    public void Dispose() => Stop();

    private void OnTimerTick(object? sender, EventArgs e) => TickOnce();

    /// <summary>
    /// **走一拍**：到点就导**一次**。返回"这一拍真的写盘了吗"（判据与读数都用它）。
    /// </summary>
    public bool TickOnce()
    {
        var options = Options();
        var now = Now();
        _anchor ??= now;

        var schedule = new AutoExportSchedule(options.Enabled ? options.IntervalMinutes : 0);
        if (!schedule.IsDue(_lastRun, _anchor.Value, now))
        {
            Publish(new AutoExportTick(AutoExportOutcome.NotDue, 0, null));
            return false;
        }

        // ⚠ **先消费这个到期点**：写失败也不重跑这一拍（否则节拍会退化成"每 30 秒重试"），
        //    下一个到期点自然再试一次。
        _lastRun = now;

        if (!options.CanRun)
        {
            Publish(new AutoExportTick(AutoExportOutcome.NotConfigured, 0, null));
            return false;
        }

        var items = new List<ReportBundleItem>();
        foreach (var source in RegisteredSources())
        {
            var table = BuildSource(source);
            if (table is not null) items.Add(new ReportBundleItem(source, table));
        }

        var missing = MissingSources();
        if (items.Count == 0)
        {
            Publish(new AutoExportTick(AutoExportOutcome.NoSources, 0, null));
            return false;
        }

        try
        {
            var result = Write(new AutoExportWriteRequest(options.Directory, items, missing, now));
            Runs++;
            Publish(new AutoExportTick(AutoExportOutcome.Written, result.FilesWritten, null));
            return true;
        }
        catch (Exception ex)
        {
            // 后台节拍里把异常抛出去只会静默杀掉整条链 ⇒ 记下来、继续走
            Publish(new AutoExportTick(AutoExportOutcome.Failed, 0, ex.Message));
            return false;
        }
    }

    private void Publish(AutoExportTick tick)
    {
        LastTick = tick;
        Report?.Invoke(tick);
    }
}
