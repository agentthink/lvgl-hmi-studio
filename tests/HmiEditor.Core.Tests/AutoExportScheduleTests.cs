using System;
using System.Linq;
using System.Reflection;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **定时/自动导出的排程判据**（阶段 184，登记 `#87`）：`now` 全部由判据给固定值
/// ⇒ 不碰线程、不睡觉、不 flaky。
///
/// <para>三条口径各有一组判据：起跑不立刻导 · 到期点算在内 · **错过的一律不补**。</para>
/// </summary>
public class AutoExportScheduleTests
{
    private static readonly DateTimeOffset Anchor = new(new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc));

    private static DateTimeOffset At(double minutes) => Anchor.AddMinutes(minutes);

    /// <summary>间隔 ≤ 0 ＝ 功能关闭（而不是"每毫秒导一次"）。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void DisabledInterval_IsNeverDue(int minutes)
    {
        var schedule = new AutoExportSchedule(minutes);

        Assert.False(schedule.IsEnabled);
        Assert.False(schedule.IsDue(null, Anchor, At(10_000)));
        Assert.False(schedule.IsDue(Anchor, Anchor, At(10_000)));
        Assert.Null(schedule.NextDue(null, Anchor, At(10_000)));
    }

    /// <summary>**起跑不立刻导**：第一次到期在"锚点 + 一个间隔"（免得太空里开一次应用就先写一份）。</summary>
    [Fact]
    public void FirstRun_IsOneIntervalAfterTheAnchor()
    {
        var schedule = new AutoExportSchedule(30);

        Assert.False(schedule.IsDue(null, Anchor, At(29.99)));
        Assert.True(schedule.IsDue(null, Anchor, At(30)));          // 正好到点 ⇒ 导（闭区间）
        Assert.True(schedule.IsDue(null, Anchor, At(30.01)));
        Assert.Equal(At(30), schedule.NextDue(null, Anchor, Anchor));
    }

    /// <summary>有了上次导出 ⇒ 一切以它为准（锚点不再说话）。</summary>
    [Fact]
    public void LastRun_TakesPrecedenceOverTheAnchor()
    {
        var schedule = new AutoExportSchedule(30);
        var lastRun = At(30);

        Assert.False(schedule.IsDue(lastRun, Anchor, At(59.99)));
        Assert.True(schedule.IsDue(lastRun, Anchor, At(60)));
        Assert.Equal(At(60), schedule.NextDue(lastRun, Anchor, At(30)));
    }

    /// <summary>时钟**回拨** ⇒ 不到期（这一段不导），下一次仍按上次导出往后算。</summary>
    [Fact]
    public void ClockRollback_IsNotDue()
    {
        var schedule = new AutoExportSchedule(30);
        var lastRun = At(60);

        Assert.False(schedule.IsDue(lastRun, Anchor, At(45)));       // 墙上时钟跳到上次导出之前
        Assert.False(schedule.IsDue(lastRun, Anchor, At(0)));
        Assert.Equal(At(90), schedule.NextDue(lastRun, Anchor, At(45)));
        Assert.True(schedule.IsDue(lastRun, Anchor, At(90)));        // 追回来之后照常
    }

    /// <summary>
    /// **错过的一律不补**（本类最重要的一条）：时钟被跳过头（休眠 / 改系统时间）时，
    /// <see cref="AutoExportSchedule.IsDue"/> 只说"现在导一次"，而 <c>NextDue</c>
    /// **直接跳到 now 之后的第一个到期点** —— 三个被错过的点一个都不返回。
    /// </summary>
    [Fact]
    public void NextDue_SkipsMissedPoints()
    {
        var schedule = new AutoExportSchedule(30);
        var lastRun = At(30);

        // 从 30 分一路跳到 140 分：路上有 60/90/120 三个到期点
        var now = At(140);
        Assert.True(schedule.IsDue(lastRun, Anchor, now));

        var next = schedule.NextDue(lastRun, Anchor, now);
        Assert.Equal(At(150), next);
        Assert.True(next > now, "「下一个到期点」必须严格在 now 之后");
    }

    /// <summary>
    /// **结构判据**：这个类型**刻意不提供**"错过了几个到期点 / 列出所有未跑的到期点"这类 API ——
    /// 没有它，调用方**写不出**"补跑一长串"的循环（这是"不补"最可靠的实现方式，比注释与自觉都硬）。
    /// </summary>
    [Fact]
    public void Schedule_ExposesNoWayToReplayMissedPoints()
    {
        var members = typeof(AutoExportSchedule)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => m.Name)
            .ToArray();

        Assert.DoesNotContain(members, n => n.Contains("Missed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, n => n.Contains("Overdue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, n => n.Contains("All", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, n => n.Contains("Enumerate", StringComparison.OrdinalIgnoreCase));

        // 面自检：真的有成员被枚举到（否则上面四条在空集上恒真）
        Assert.Contains("IsDue", members);
        Assert.Contains("NextDue", members);
    }

    /// <summary>多步推进：连跑 5 个周期，每一步都"只在到期点导一次"（这条是把服务层的循环在纯逻辑上先走一遍）。</summary>
    [Fact]
    public void SteadyState_ExportsExactlyOncePerInterval()
    {
        var schedule = new AutoExportSchedule(15);
        DateTimeOffset? lastRun = null;
        var exports = 0;

        for (var minute = 0; minute <= 90; minute++)
        {
            var now = At(minute);
            if (!schedule.IsDue(lastRun, Anchor, now)) continue;

            exports++;
            lastRun = now;   // 服务层就是这么记的：导完把 lastRun 记成"现在"
        }

        Assert.Equal(6, exports);   // 15/30/45/60/75/90 各一次
        Assert.Equal(At(90), lastRun);
    }

    /// <summary>时钟被跳到极远的未来 ⇒ 饱和返回、不抛（算术用 tick 整除 ＋ checked）。</summary>
    [Fact]
    public void AbsurdClockJump_SaturatesInsteadOfThrowing()
    {
        var schedule = new AutoExportSchedule(1);
        var farFuture = new DateTimeOffset(new DateTime(9999, 12, 31, 23, 59, 0, DateTimeKind.Utc));

        var next = schedule.NextDue(Anchor, Anchor, farFuture);

        Assert.NotNull(next);
        Assert.True(next > farFuture);
    }

    /// <summary>起点正好落在到期点上时，"下一个"必须**严格大于**它（浮点算法会在这里返回同一个点）。</summary>
    [Fact]
    public void NextDue_IsStrictlyAfterNow()
    {
        var schedule = new AutoExportSchedule(30);

        foreach (var minutes in new[] { 0.0, 30.0, 60.0, 90.0, 30.5, 61.25 })
        {
            var now = At(minutes);
            var next = schedule.NextDue(null, Anchor, now)!;
            Assert.True(next > now, $"now={now:O} 时 NextDue 返回了 {next:O}");
        }
    }
}
