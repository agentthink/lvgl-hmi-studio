using System;

namespace HmiEditor.Core.Models.Alarm;

/// <summary>
/// **报警优先级**（ISA-18.2；登记 `§2-#11` 切片㊁）。
///
/// <para><b>ISA-18.2 的关键一条：优先级 ≠ 严重度</b>。严重度说的是"后果有多重"，
/// 优先级说的是"要多快响应" —— 同一个"高"级别，若允许响应时间只有 1 分钟，它就比
/// 允许 30 分钟的那条更该被先处理。所以本类把优先级定义成
/// <c>f(严重度, 允许响应时间)</c> 的**纯函数**，而不是给 <see cref="AlarmConfig"/> 加一个可自由填的数字
/// （那样两个运维员填出来的同一条报警会有不同优先级，ISA-18.2 里这是最常见的落地失败）。</para>
///
/// <para><b>方向</b>：**1 = 最高**（与 ISA-18.2 的 P1…P4 记法一致，也和"先看 P1"的现场习惯一致）。
/// 映射表（`AllowedResponseMs` 为 null ⇒ 用该严重度的**默认**响应时间）：</para>
///
/// <list type="table">
///   <item><term>Emergency</term><description>≤ 60 s ⇒ **P1**；否则 P2</description></item>
///   <item><term>Critical</term><description>≤ 5 min ⇒ **P2**；否则 P3</description></item>
///   <item><term>Warning</term><description>≤ 30 min ⇒ **P3**；否则 P4</description></item>
///   <item><term>Info</term><description>一律 **P4**</description></item>
/// </list>
///
/// <para><b>为什么不落成存字段</b>：优先级是可推导量。存下来就会出现"改了严重度但优先级没跟着改"
/// 这类陈旧值（本仓 `AlarmEngineStaleValueGuardTests` 记的就是同一种病）。</para>
/// </summary>
public static class AlarmPriority
{
    /// <summary>最高优先级（数值最小＝最紧急）。</summary>
    public const int Highest = 1;

    /// <summary>最低优先级。</summary>
    public const int Lowest = 4;

    /// <summary>各严重度的默认允许响应时间（ms）—— 未填 <see cref="AlarmConfig.AllowedResponseMs"/> 时用它。</summary>
    public static int DefaultAllowedResponseMs(AlarmSeverity severity) => severity switch
    {
        AlarmSeverity.Emergency => 60_000,          // 1 min
        AlarmSeverity.Critical => 5 * 60_000,       // 5 min
        AlarmSeverity.Warning => 30 * 60_000,       // 30 min
        _ => int.MaxValue,                          // Info：不设时限
    };

    /// <summary>
    /// 优先级（1…4）。<paramref name="allowedResponseMs"/> 为 null ⇒ 用
    /// <see cref="DefaultAllowedResponseMs"/>；≤ 0 视作"立即"（按最严处理，不抛 —— 工程数据可能手改坏）。
    /// </summary>
    public static int From(AlarmSeverity severity, int? allowedResponseMs)
    {
        var allowed = allowedResponseMs ?? DefaultAllowedResponseMs(severity);
        if (allowed <= 0) allowed = 1;              // 手改坏的数据按"立即响应"处理，不让它悄悄降级

        return severity switch
        {
            AlarmSeverity.Emergency => allowed <= 60_000 ? 1 : 2,
            AlarmSeverity.Critical => allowed <= 5 * 60_000 ? 2 : 3,
            AlarmSeverity.Warning => allowed <= 30 * 60_000 ? 3 : 4,
            _ => Lowest,
        };
    }

    /// <summary>一条报警的优先级（便捷重载）。</summary>
    public static int Of(AlarmConfig alarm)
    {
        ArgumentNullException.ThrowIfNull(alarm);
        return From(alarm.Severity, alarm.AllowedResponseMs);
    }

    /// <summary>
    /// 显示记法：ISA-18.2 习惯的 **`P1`…`P4`** —— 语言无关，所以**不**进字典
    /// （进字典反而会让"P1"在英文界面变成别的写法，两份报表对不上）。
    /// </summary>
    public static string Label(int priority) =>
        priority is >= Highest and <= Lowest ? $"P{priority}" : throw new ArgumentOutOfRangeException(
            nameof(priority), priority, $"优先级只允许 {Highest}…{Lowest}");
}
