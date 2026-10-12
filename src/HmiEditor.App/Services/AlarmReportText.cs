using System;
using HmiEditor.App.Resources;
using HmiEditor.Core.Reporting;

namespace HmiEditor.App.Services;

/// <summary>
/// **报警记录报表的显示文本**（阶段 174）—— 与 <see cref="AuditReportText"/> 同形：
/// Core 只给键，具体用哪个 <see cref="Lang"/> 常量在这里定（键因此**可达**，登记 `#48` 的纪律）。
/// </summary>
public static class AlarmReportText
{
    /// <summary>列键 → 列名（列名复用既有词：时间/报警名称/级别/触发值/确认人 ＋ 本阶段新增的"状态"）。</summary>
    public static string ColumnName(string coreKey) => coreKey switch
    {
        "user.mgr.audit.time" => AppLocalization.Service[Lang.UserMgrAuditTime],
        "alarm.name" => AppLocalization.Service[Lang.AlarmName],
        "alarm.severity" => AppLocalization.Service[Lang.AlarmSeverity],
        "alarm.report.col.state" => AppLocalization.Service[Lang.AlarmReportColState],
        "alarm.trigger.value" => AppLocalization.Service[Lang.AlarmTriggerValue],
        "alarm.ack.by" => AppLocalization.Service[Lang.AlarmAckBy],
        _ => coreKey,   // 未映射不许静默：把键显示出来（判据会红）
    };

    /// <summary>标题。</summary>
    public static string Title => AppLocalization.Service[Lang.AlarmReportTitle];

    /// <summary>
    /// 把报表需要的显示文本收成 <c>localize</c> 委托（标题 ＋ 列名 ＋ 级别名 ＋ 状态名）。
    /// **逐键显式映射**（不写"兜底直接查字典"）：这样每个键都在产品代码里以 <see cref="Lang"/> 常量出现
    /// ⇒ 键可达性守卫（登记 `#48`）看得见它们；未映射的键回落成键名，判据会立刻红。
    /// </summary>
    public static Func<string, string> Localizer => key => key switch
    {
        AlarmReport.TitleKey => Title,
        "alarm.level.info" => AppLocalization.Service[Lang.AlarmLevelInfo],
        "alarm.level.warning" => AppLocalization.Service[Lang.AlarmLevelWarning],
        "alarm.level.critical" => AppLocalization.Service[Lang.AlarmLevelCritical],
        "alarm.level.emergency" => AppLocalization.Service[Lang.AlarmLevelEmergency],
        "alarm.state.inactive" => AppLocalization.Service[Lang.AlarmStateInactive],
        "alarm.state.active" => AppLocalization.Service[Lang.AlarmStateActive],
        "alarm.state.acknowledged" => AppLocalization.Service[Lang.AlarmStateAcknowledged],
        "alarm.state.returned" => AppLocalization.Service[Lang.AlarmStateReturned],
        _ => ColumnName(key),
    };
}
