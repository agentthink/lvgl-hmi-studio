using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报表时间范围判据**（阶段 178）：闭区间、UTC 比较、起点晚于终点要抛，
/// 以及三个数据源各自按**自己的**时间字段过滤（历史数据还要**先过滤再封顶**）。
/// </summary>
public class ReportTimeRangeTests
{
    private static readonly DateTime T10 = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T12 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T14 = new(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc);

    private static string L(string key) => key;

    [Fact]
    public void Contains_IsInclusiveOnBothEnds_AndUtc()
    {
        var range = new ReportTimeRange(T10, T12);

        Assert.True(range.Contains(T10));    // 闭区间：端点**算**在内
        Assert.True(range.Contains(T12));
        Assert.True(range.Contains(new DateTime(2026, 10, 1, 11, 0, 0, DateTimeKind.Utc)));
        Assert.False(range.Contains(new DateTime(2026, 10, 1, 12, 0, 1, DateTimeKind.Utc)));
        Assert.False(range.Contains(new DateTime(2026, 10, 1, 9, 59, 59, DateTimeKind.Utc)));

        // 任一端为空 ＝ 该侧不设限
        Assert.True(new ReportTimeRange(null, null).Contains(T14));
        Assert.True(new ReportTimeRange(T12, null).Contains(T14));
        Assert.False(new ReportTimeRange(null, T12).Contains(T14));
    }

    /// <summary>起点晚于终点 ⇒ 抛（静默给空表会让用户以为"这段时间真没数据"）。</summary>
    [Fact]
    public void Constructor_FromAfterTo_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ReportTimeRange(T12, T10));
    }

    [Fact]
    public void AuditReport_FiltersByItsOwnTimestamp()
    {
        var entries = new[]
        {
            new AuditLogEntry { Timestamp = T10, Username = "a" },
            new AuditLogEntry { Timestamp = T12, Username = "b" },
            new AuditLogEntry { Timestamp = T14, Username = "c" },
        };

        var rows = AuditLogReport.From(entries, L, new ReportTimeRange(T12, T12)).Rows.Single();
        Assert.Equal("b", rows[1]);

        Assert.Equal(3, AuditLogReport.From(entries, L).Rows.Count);        // 不传范围 ⇒ 全要
    }

    [Fact]
    public void AlarmReport_FiltersByOccurredAt()
    {
        var records = new[]
        {
            new AlarmRecord { OccurredAt = T10, AlarmName = "a", RawValue = "1" },
            new AlarmRecord { OccurredAt = T14, AlarmName = "c", RawValue = "3" },
        };

        var rows = AlarmReport.From(records, L, new ReportTimeRange(T12, null)).Rows.Single();
        Assert.Equal("c", rows[1]);
    }

    /// <summary>
    /// **顺序**：历史数据必须**先按范围过滤、再封顶** —— 反过来会得到"最近 N 条里的那些"。
    ///
    /// <p>⚠ 场景必须挑得**能区分两种顺序**：范围要覆盖**超出前 maxRows 条之外**的数据。
    /// 第一版把范围设成"最早那两条"（maxRows=3），于是"先封顶"与"先过滤"结果**一样** ⇒
    /// 判据根本抓不到（阶段 178 的负控 nc3 就是这么漏的：注入"先封顶再过滤"它照样绿）。
    /// 现在取**最近 5 条**里的最近 3 条：先封顶的话先被 Take(3) 砍成最早 3 条、再过滤 ⇒ **空**。</p>
    /// </summary>
    [Fact]
    public void HistoryReport_FiltersBeforeCapping()
    {
        // 10 条：0..9 分钟
        var samples = Enumerable.Range(0, 10)
            .Select(i => new TrendSample
            {
                Timestamp = new DateTime(2026, 10, 1, 0, i, 0, DateTimeKind.Utc),
                Values = new List<double> { i },
            })
            .ToArray();

        // 范围 = 最近 5 条（5..9 分钟），maxRows = 3
        var range = new ReportTimeRange(
            new DateTime(2026, 10, 1, 0, 5, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 1, 0, 9, 0, DateTimeKind.Utc));

        var rows = HistoryReport.From(samples, new[] { "v" }, L, maxRows: 3, range: range).Rows;

        // 先过滤（5..9）再取最近 3 条 ⇒ 7/8/9 分钟
        Assert.Equal(3, rows.Count);
        Assert.Equal("2026-10-01 00:07:00Z", rows[0][0]);
        Assert.Equal("2026-10-01 00:09:00Z", rows[2][0]);

        // 正控：不传范围时封顶照旧生效（取**最近** 3 条）
        var capped = HistoryReport.From(samples, new[] { "v" }, L, maxRows: 3).Rows;
        Assert.Equal(3, capped.Count);
        Assert.Equal("2026-10-01 00:07:00Z", capped[0][0]);
        Assert.Equal("2026-10-01 00:09:00Z", capped[2][0]);
    }
}
