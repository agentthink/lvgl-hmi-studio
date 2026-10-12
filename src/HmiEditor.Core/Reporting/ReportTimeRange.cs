using System;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// **报表的时间范围**（阶段 178）：两个**可空**端点 —— <c>null</c> 表示那一侧不设限。
///
/// <para><b>三条口径（都写进判据）：</b>① 端点是**闭区间**（<c>from &lt;= t &lt;= to</c>）——
/// 用户挑"到 12:00"时期望**包含** 12:00 那一条；② 一律按 **UTC** 比较（三个数据源的时间字段都是 UTC，
/// 见 <see cref="ReportExporter.FormatUtc"/>）；③ <c>from &gt; to</c> 是**调用方的编程错误**（抛）——
/// 静默给出一张空表会让用户以为"这段时间真没数据"。</para>
/// </summary>
public readonly struct ReportTimeRange
{
    /// <summary>构造一个闭区间（任一端可空 ＝ 该侧不设限）。</summary>
    public ReportTimeRange(DateTime? from, DateTime? to)
    {
        if (from is { } f && to is { } t && f > t)
            throw new ArgumentException($"时间范围的起点晚于终点（{f:O} > {t:O}）—— 界面上不该产生这种输入");

        From = from;
        To = to;
    }

    /// <summary>起点（含）；<c>null</c> ＝ 不限。</summary>
    public DateTime? From { get; }

    /// <summary>终点（含）；<c>null</c> ＝ 不限。</summary>
    public DateTime? To { get; }

    /// <summary>这个时刻在范围内吗（闭区间；UTC 比较）。</summary>
    public bool Contains(DateTime timestamp)
    {
        var utc = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();
        if (From is { } f && utc < ToUtc(f)) return false;
        if (To is { } t && utc > ToUtc(t)) return false;
        return true;
    }

    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
