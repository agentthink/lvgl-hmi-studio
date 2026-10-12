using System;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// **定时/自动导出的排程口径**（阶段 184，登记 `#87`）：纯函数，<c>now</c> 一律由调用方给
/// ⇒ 判据能用固定时刻核对"到点没到点"，不必真的等一个周期，也不必碰线程或计时器。
///
/// <para><b>三处口径（逐条都有判据）</b>：</para>
/// <list type="number">
/// <item><b>起跑不立刻导</b>：第一次到期在 **锚点 + 一个间隔**（锚点由服务在启动时记一次 <c>now</c>）——
/// 否则每次开应用都会先导一份，而"我什么都没干它就开始写文件"是用户最不想要的那种自动化。</item>
/// <item><b>到期点算在内</b>：<c>now == 到期点</c> 就该导（闭区间，与报表时间范围同口径）。</item>
/// <item><b>错过的一律不补</b>：时钟被跳过头（休眠、改系统时间、应用挂起）时，
/// <see cref="IsDue"/> 只回答"现在该导一次"，<see cref="NextDue"/> 直接**跳到 now 之后的第一个到期点** ——
/// 这个类型**刻意不提供"错过了几个到期点"**这种 API，因为没有它，调用方**写不出**"补跑一长串"的循环。
/// 补跑的代价是用户完全无法预期的：一觉睡醒发现导出目录里多了 20 份同样的报表。</item>
/// </list>
///
/// <para>时钟**回拨**（<c>now &lt; lastRun</c>）⇒ 不到期：这一段不导，等墙上时钟追回来。
/// 静默重复导出一份"看起来更新"的文件更糟。</para>
/// </summary>
/// <param name="IntervalMinutes">间隔（分钟）；≤ 0 ⇒ **功能关闭**（不是"每毫秒导一次"）。</param>
public sealed record AutoExportSchedule(int IntervalMinutes)
{
    /// <summary>间隔 &gt; 0 才算开着（设置里关掉功能的表达方式就是 0）。</summary>
    public bool IsEnabled => IntervalMinutes > 0;

    /// <summary>
    /// **现在该不该导一次**。<paramref name="lastRunUtc"/> 为空 ＝ 还没导过 ⇒ 看是否过了
    /// <paramref name="anchorUtc"/> + 一个间隔；不为空 ⇒ 看距上次导出是否满一个间隔。
    /// （<paramref name="lastRunUtc"/> 优先于锚点：锚点只在"从没导过"时说话。）
    /// </summary>
    public bool IsDue(DateTimeOffset? lastRunUtc, DateTimeOffset anchorUtc, DateTimeOffset nowUtc)
    {
        if (!IsEnabled) return false;

        var dueAt = (lastRunUtc ?? anchorUtc).AddMinutes(IntervalMinutes);
        return nowUtc >= dueAt;   // 闭区间：正好到点也算（与报表时间范围同口径）
    }

    /// <summary>
    /// **now 之后的下一个到期点**（严格大于 <paramref name="nowUtc"/>）；
    /// 功能关掉 ⇒ <c>null</c>。错过多个点时不返回"最早那个没跑的"，而是**下一个未来点**（见类注释第 ③ 条）。
    ///
    /// <para>步数用 **tick 整除**算（不用浮点分钟）：整除天然是 floor，且"恰好整数倍"这种边界上
    /// 不会像 <c>TotalMinutes / IntervalMinutes</c> 那样可能落到整数下方（那会返回一个
    /// **不大于 now** 的"下一个"到期点）。⚠ 诚实边界：这一条**没有行为判据** —— 浮点版在
    /// <see cref="DateTimeOffset"/> 能达到的范围内几乎总是给出同样的结果（差异只在极端量级上出现，
    /// 而那种场景本身不稳定）⇒ 它属于"靠实现选择保证、靠注释讲清楚"的那一类。
    /// 时钟被跳到极远（比如年份 9999）时按 <see cref="DateTimeOffset.MaxValue"/> 饱和返回 —— 不抛。</para>
    /// </summary>
    public DateTimeOffset? NextDue(DateTimeOffset? lastRunUtc, DateTimeOffset anchorUtc, DateTimeOffset nowUtc)
    {
        if (!IsEnabled) return null;

        var interval = TimeSpan.FromMinutes(IntervalMinutes);
        var @base = lastRunUtc ?? anchorUtc;

        if (nowUtc < @base) return @base.Add(interval);   // 时钟回拨：下一次仍按上次导出往后算

        var steps = (nowUtc - @base).Ticks / interval.Ticks + 1;   // 整除＝floor；+1 ⇒ 严格大于 now
        try
        {
            return @base.AddTicks(checked(interval.Ticks * steps));
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
        {
            return DateTimeOffset.MaxValue;   // 远到没有意义的未来点：饱和，不抛
        }
    }
}
