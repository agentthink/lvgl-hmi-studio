using System;

namespace HmiEditor.Core.Models.Alarm;

/// <summary>
/// **搁置的规矩**（阶段 274；契约 §4/§6）—— 全部是纯函数，于是"能不能"这件事**逐条可判**，
/// 不必等到引擎跑起来。
///
/// <para><b>时长上下界</b>：<see cref="MinDuration"/> = 1 分钟（低于它人手都来不及，也能防误触即静音）、
/// <see cref="MaxDuration"/> = 8 小时（一个班次量级；ISA-18.2 里搁置是**短期**手段）。
/// **不存在"无限搁置"** —— 那等于关掉报警。</para>
///
/// <para><b>最要紧的一条</b>：<see cref="CanAcknowledge"/> 在 <see cref="AlarmState.Shelved"/> 上**拒绝**
/// —— 搁置期间"确认"会让操作员以为"我已处理"，而实际上报警被静音了。解锁顺序必须是
/// **先解除搁置、再确认**。</para>
/// </summary>
public static class AlarmShelvePolicy
{
    /// <summary>最短搁置时长。</summary>
    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(1);

    /// <summary>最长搁置时长。</summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(8);

    /// <summary>搁置时长是否在 <c>[1 min, 8 h]</c> 内（闭区间）。</summary>
    public static bool IsDurationAllowed(TimeSpan duration) =>
        duration >= MinDuration && duration <= MaxDuration;

    /// <summary>能不能搁置；不能时给出码。</summary>
    public static bool CanShelve(AlarmState state, TimeSpan duration, out string? issueCode)
    {
        issueCode = null;

        switch (state)
        {
            case AlarmState.Inactive:
                issueCode = AlarmShelveIssue.NotActive;
                return false;
            case AlarmState.Returned:
                issueCode = AlarmShelveIssue.Returned;
                return false;
            case AlarmState.Shelved:
                issueCode = AlarmShelveIssue.AlreadyShelved;
                return false;
        }

        if (!IsDurationAllowed(duration))
        {
            issueCode = AlarmShelveIssue.DurationOutOfRange;
            return false;
        }

        return true;
    }

    /// <summary>能不能确认；不能时给出码（**搁置中一律拒**）。</summary>
    public static bool CanAcknowledge(AlarmState state, out string? issueCode)
    {
        issueCode = state switch
        {
            AlarmState.Shelved => AlarmShelveIssue.Shelved,
            AlarmState.Acknowledged => AlarmShelveIssue.AlreadyAcked,
            AlarmState.Returned => AlarmShelveIssue.Returned,
            AlarmState.Inactive => AlarmShelveIssue.NotActive,
            _ => null,
        };

        return issueCode is null;
    }

    /// <summary>能不能解除搁置（只有搁置中能解除）；不能时给出码。</summary>
    public static bool CanUnshelve(AlarmState state, out string? issueCode)
    {
        issueCode = state == AlarmState.Shelved ? null : AlarmShelveIssue.NotActive;
        return issueCode is null;
    }
}
