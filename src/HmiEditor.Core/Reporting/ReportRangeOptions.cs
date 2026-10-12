using System;

namespace HmiEditor.Core.Reporting;

/// <summary>报表时间范围的**快捷档**（阶段 178）：界面上给这几个常见跨度，避免让用户去挑两个日期。</summary>
public enum ReportRangeChoice
{
    /// <summary>全部（不设范围）。</summary>
    All,

    /// <summary>最近 1 小时。</summary>
    LastHour,

    /// <summary>最近 24 小时。</summary>
    Last24Hours,

    /// <summary>最近 7 天。</summary>
    Last7Days,

    /// <summary>最近 30 天。</summary>
    Last30Days,

    /// <summary>自定义起止日期（两端可空 ＝ 该侧不设限）。</summary>
    Custom,
}

/// <summary>
/// **用户在范围对话框里的选择**（阶段 180）：快捷档，或**自定义起止日期**（两端可空 ＝ 该侧不设限）。
/// <paramref name="CustomFrom"/>/<paramref name="CustomTo"/> 是**日期选择器给的带偏移时刻**
/// （Avalonia 的 <c>DatePicker.SelectedDate</c> 就是 <c>DateTimeOffset?</c>）—— 偏移必须留着，
/// 否则"用户按本地日期挑的"会被当成 UTC（见 <see cref="ReportRangeOptions.Compute"/>）。
/// </summary>
public readonly record struct ReportRangeSelection(
    ReportRangeChoice Choice,
    DateTimeOffset? CustomFrom = null,
    DateTimeOffset? CustomTo = null);

/// <summary>
/// **快捷档 → <see cref="ReportTimeRange"/></b> 的纯映射（阶段 178）：<c>now</c> 由调用方传入
/// ⇒ 判据能用固定时刻核对（不读时钟）。
///
/// <para>口径：<c>All</c> ⇒ <c>null</c>（不设范围）；其余 ⇒ 闭区间 <c>[now - 跨度, now]</c>，
/// 一律 **UTC**。自定义起止日期走 <see cref="Compute(ReportRangeSelection, DateTimeOffset)"/>（阶段 180）。</para>
/// </summary>
public static class ReportRangeOptions
{
    /// <summary>界面上按这个顺序列出来。</summary>
    public static readonly ReportRangeChoice[] All =
    {
        ReportRangeChoice.All,
        ReportRangeChoice.LastHour,
        ReportRangeChoice.Last24Hours,
        ReportRangeChoice.Last7Days,
        ReportRangeChoice.Last30Days,
        ReportRangeChoice.Custom,
    };

    /// <summary>档位 → 文案键（Core 只给键，App 推显示文本 —— 与本仓其它报表同形）。</summary>
    public static string LabelKey(ReportRangeChoice choice) => choice switch
    {
        ReportRangeChoice.LastHour => "report.range.last.hour",
        ReportRangeChoice.Last24Hours => "report.range.last.24h",
        ReportRangeChoice.Last7Days => "report.range.last.7d",
        ReportRangeChoice.Last30Days => "report.range.last.30d",
        ReportRangeChoice.Custom => "report.range.custom",
        _ => "report.range.all",
    };

    /// <summary>
    /// **按用户的选择算范围**（阶段 180）：快捷档走 <see cref="Compute(ReportRangeChoice, DateTimeOffset)"/>；
    /// <c>Custom</c> 时用两端日期 —— **左边含当天 00:00、右边含当天最后一刻**（用户挑"到 10-01"指的是**整天**），
    /// 并按**各自带的偏移**转成 UTC（Avalonia 日期选择器给的就是带偏移的时刻）。
    /// 两端都空的"自定义"是**调用方的编程错误**（抛）：静默当成"全部"会让用户以为设了范围。
    /// </summary>
    public static ReportTimeRange? Compute(ReportRangeSelection selection, DateTimeOffset now)
    {
        if (selection.Choice != ReportRangeChoice.Custom)
            return Compute(selection.Choice, now);

        if (selection.CustomFrom is null && selection.CustomTo is null)
            throw new ArgumentException("自定义范围两端都空 —— 界面上不该产生这种输入（要【全部】就选那一档）");

        var from = selection.CustomFrom is { } f
            ? new DateTimeOffset(f.Date, f.Offset).UtcDateTime               // 当天 00:00（按用户偏移）
            : (DateTime?)null;
        var to = selection.CustomTo is { } t
            ? new DateTimeOffset(t.Date, t.Offset).AddDays(1).AddTicks(-1).UtcDateTime   // 当天最后一刻
            : (DateTime?)null;

        return new ReportTimeRange(from, to);
    }

    /// <summary>
    /// 档位 → 时间范围（<c>All</c> ⇒ <c>null</c>）。
    ///
    /// <para><b><see cref="ReportRangeChoice.Custom"/> 在这里**抛**</b>（阶段 180）：自定义要的是
    /// **两个日期**，这个重载拿不到 —— 静默按 <c>All</c> 返回 <c>null</c> 正是最坏的形状
    /// （用户以为设了范围，导出却给了全部，且没有任何提示）。调用方应当改用
    /// <see cref="Compute(ReportRangeSelection, DateTimeOffset)"/>。</para>
    /// </summary>
    public static ReportTimeRange? Compute(ReportRangeChoice choice, DateTimeOffset now)
    {
        if (choice == ReportRangeChoice.Custom)
            throw new ArgumentException(
                "Custom 档需要两个日期 —— 请用 Compute(ReportRangeSelection, now)", nameof(choice));

        var to = now.UtcDateTime;
        return choice switch
        {
            ReportRangeChoice.LastHour => new ReportTimeRange(to.AddHours(-1), to),
            ReportRangeChoice.Last24Hours => new ReportTimeRange(to.AddHours(-24), to),
            ReportRangeChoice.Last7Days => new ReportTimeRange(to.AddDays(-7), to),
            ReportRangeChoice.Last30Days => new ReportTimeRange(to.AddDays(-30), to),
            _ => null,
        };
    }
}
