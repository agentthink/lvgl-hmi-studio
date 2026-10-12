using System;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报表时间范围：档位 → 范围**（阶段 180 补齐 Core 侧判据）：快捷档的跨度、自定义起止日期的
/// "整天"口径、**偏移必须留住**、两端都空/起点晚于终点要抛，以及
/// <c>Compute(choice, now)</c> 遇到 <see cref="ReportRangeChoice.Custom"/> 要抛。
///
/// <para>⚠ 阶段 178 只把档位映射测在 App 侧（<c>ReportRangeSelectionGuardTests</c>）；
/// 阶段 180 加的是**纯逻辑**（日期 → UTC 区间），按本仓分工应当落在 Core.Tests ——
/// App 侧那份继续守"对话框接线 ＋ 文案"。</para>
/// </summary>
public class ReportRangeOptionsTests
{
    private static readonly DateTimeOffset Now = new(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

    /// <summary>"全部"⇒ 不设范围；其余快捷档 ⇒ 闭区间 <c>[now - 跨度, now]</c>（UTC）。</summary>
    [Fact]
    public void QuickChoices_MapToTheExpectedSpan()
    {
        Assert.Null(ReportRangeOptions.Compute(ReportRangeChoice.All, Now));

        var hour = ReportRangeOptions.Compute(ReportRangeChoice.LastHour, Now)!.Value;
        Assert.Equal(Now.UtcDateTime.AddHours(-1), hour.From);
        Assert.Equal(Now.UtcDateTime, hour.To);
        Assert.True(hour.Contains(Now.UtcDateTime));      // 右端算在内

        Assert.Equal(Now.UtcDateTime.AddHours(-24), ReportRangeOptions.Compute(ReportRangeChoice.Last24Hours, Now)!.Value.From);
        Assert.Equal(Now.UtcDateTime.AddDays(-7), ReportRangeOptions.Compute(ReportRangeChoice.Last7Days, Now)!.Value.From);
        Assert.Equal(Now.UtcDateTime.AddDays(-30), ReportRangeOptions.Compute(ReportRangeChoice.Last30Days, Now)!.Value.From);
    }

    /// <summary>
    /// **自定义日期 ⇒ 整天**：用户挑"从 9-28 到 10-01"指的是**两个整日**，不是
    /// 10-01 的 00:00。所以左端 = 起始日 00:00，右端 = 结束日**最后一刻**
    /// （次日 00:00 减 1 tick）。
    /// </summary>
    [Fact]
    public void Custom_CoversBothWholeDays()
    {
        var range = ReportRangeOptions.Compute(
            new ReportRangeSelection(
                ReportRangeChoice.Custom,
                new DateTimeOffset(new DateTime(2026, 9, 28), TimeSpan.FromHours(8)),
                new DateTimeOffset(new DateTime(2026, 10, 1), TimeSpan.FromHours(8))),
            Now)!.Value;

        // 东八区的 9-28 00:00 = 9-27 16:00Z；10-01 整天 ⇒ 到 10-01 15:59:59.9999999Z
        // （＝次日 00:00(+08) 减 1 tick —— 写成"减一 tick"而不是抄一个魔数，口径自己说话）
        Assert.Equal(new DateTime(2026, 9, 27, 16, 0, 0, DateTimeKind.Utc), range.From);
        Assert.Equal(
            new DateTimeOffset(new DateTime(2026, 10, 2), TimeSpan.FromHours(8)).UtcDateTime.AddTicks(-1),
            range.To);

        Assert.True(range.Contains(new DateTime(2026, 9, 27, 16, 0, 0, DateTimeKind.Utc)));       // 起始整点算在内
        Assert.False(range.Contains(new DateTime(2026, 9, 27, 15, 59, 59, DateTimeKind.Utc)));
        Assert.True(range.Contains(new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc)));       // 结束日的白天算在内
        Assert.False(range.Contains(new DateTime(2026, 10, 1, 16, 0, 0, DateTimeKind.Utc)));      // 次日 00:00(+08) 排在外
    }

    /// <summary>
    /// **偏移必须留住**：同一个"10-01"在 +08:00 与 +00:00 下是**两个不同的 UTC 时刻**
    /// （相差 8 小时）。若实现图省事按 <c>DateTime.Date</c> 直接当 UTC，这条会红。
    /// </summary>
    [Fact]
    public void Custom_HonoursThePickerOffset()
    {
        var day = new DateTime(2026, 10, 1);

        var east8 = ReportRangeOptions.Compute(
            new ReportRangeSelection(ReportRangeChoice.Custom, new DateTimeOffset(day, TimeSpan.FromHours(8))), Now)!.Value;
        var utc = ReportRangeOptions.Compute(
            new ReportRangeSelection(ReportRangeChoice.Custom, new DateTimeOffset(day, TimeSpan.Zero)), Now)!.Value;

        Assert.Equal(new DateTime(2026, 9, 30, 16, 0, 0, DateTimeKind.Utc), east8.From);
        Assert.Equal(day, utc.From);
        Assert.Equal(TimeSpan.FromHours(8), utc.From!.Value - east8.From!.Value);
    }

    /// <summary>只给一端 ⇒ 另一端不设限（"从某天起" / "到某天为止"两种单边用法）。</summary>
    [Fact]
    public void Custom_AllowsOneSidedRanges()
    {
        var fromOnly = ReportRangeOptions.Compute(
            new ReportRangeSelection(ReportRangeChoice.Custom, new DateTimeOffset(new DateTime(2026, 10, 1))), Now)!.Value;
        Assert.NotNull(fromOnly.From);
        Assert.Null(fromOnly.To);
        Assert.True(fromOnly.Contains(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var toOnly = ReportRangeOptions.Compute(
            new ReportRangeSelection(ReportRangeChoice.Custom, null, new DateTimeOffset(new DateTime(2026, 10, 1))), Now)!.Value;
        Assert.Null(toOnly.From);
        Assert.NotNull(toOnly.To);
        Assert.True(toOnly.Contains(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    /// <summary>两端都空的"自定义"是调用方的编程错误 ⇒ 抛（静默当"全部"会让用户以为设了范围）。</summary>
    [Fact]
    public void Custom_WithBothEndsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => ReportRangeOptions.Compute(
            new ReportRangeSelection(ReportRangeChoice.Custom), Now));
    }

    /// <summary>起点晚于终点 ⇒ 抛（<see cref="ReportTimeRange"/> 的构造契约，这里核对真的传下去了）。</summary>
    [Fact]
    public void Custom_FromAfterTo_Throws()
    {
        Assert.Throws<ArgumentException>(() => ReportRangeOptions.Compute(
            new ReportRangeSelection(
                ReportRangeChoice.Custom,
                new DateTimeOffset(new DateTime(2026, 10, 2)),
                new DateTimeOffset(new DateTime(2026, 10, 1))),
            Now));
    }

    /// <summary>
    /// **<c>Compute(choice, now)</c> 不接受自定义档**：它拿不到两个日期，静默按"全部"返回
    /// <c>null</c> 是最坏的形状（用户以为设了范围，导出却给了全部且毫无提示）⇒ 必须抛，
    /// 让调用方改用 <see cref="ReportRangeSelection"/> 那个重载。
    /// </summary>
    [Fact]
    public void ChoiceOverload_RejectsCustom()
    {
        var ex = Assert.Throws<ArgumentException>(() => ReportRangeOptions.Compute(ReportRangeChoice.Custom, Now));
        Assert.Contains("ReportRangeSelection", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>界面上列出的每一档都能被两个重载之一消化（不许出现"列了但算不出"的档位）。</summary>
    [Fact]
    public void EveryListedChoice_IsComputable()
    {
        foreach (var choice in ReportRangeOptions.All)
        {
            if (choice == ReportRangeChoice.Custom)
                Assert.Throws<ArgumentException>(() => ReportRangeOptions.Compute(choice, Now));
            else
                Assert.Null(Record.Exception(() => ReportRangeOptions.Compute(choice, Now)));
        }
    }
}
