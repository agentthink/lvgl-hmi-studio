using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.Alarm;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// **报警记录报表**（阶段 174，报表能力第二片）：把 <see cref="AlarmRecord"/> 翻成一张
/// <see cref="ReportTable"/>，与 <see cref="AuditLogReport"/> 同形（Core 只给键、App 推显示文本）。
///
/// <para><b>列序固定</b>：时间 / 报警名称 / 级别 / 状态 / 触发值 / 确认人。
/// 级别与状态这类**枚举**不许直显枚举名（既有教训：`#61o` 的"枚举直显"），一律走
/// <paramref name="localize"/>（键由 <see cref="SeverityKey"/> / <see cref="StateKey"/> 给）。</para>
/// </summary>
public static class AlarmReport
{
    /// <summary>列键（顺序即列序）。列名**复用**既有词：审计页的时间、`alarm.name`、`alarm.severity`、
    /// `alarm.trigger.value`、`alarm.ack.by`。</summary>
    public static readonly string[] ColumnKeys =
    {
        "user.mgr.audit.time",
        "alarm.name",
        "alarm.severity",
        "alarm.report.col.state",
        "alarm.trigger.value",
        "alarm.ack.by",
    };

    /// <summary>标题键。</summary>
    public const string TitleKey = "alarm.report.title";

    /// <summary>
    /// 状态列名键。⚠ 与"级别名/状态名"不同：这条是**列名**（"状态"），
    /// 而 `alarm.state.*` 那几个是**取值**。
    /// </summary>
    public const string StateColumnKey = "alarm.report.col.state";

    /// <summary>严重级别 → 键。</summary>
    public static string SeverityKey(AlarmSeverity severity) => severity switch
    {
        AlarmSeverity.Info => "alarm.level.info",
        AlarmSeverity.Warning => "alarm.level.warning",
        AlarmSeverity.Critical => "alarm.level.critical",
        AlarmSeverity.Emergency => "alarm.level.emergency",
        _ => "alarm.level.info",
    };

    /// <summary>状态 → 键（`Inactive` 复用既有 `alarm.state.inactive`；另三条本阶段新增）。</summary>
    public static string StateKey(AlarmState state) => state switch
    {
        AlarmState.Active => "alarm.state.active",
        AlarmState.Acknowledged => "alarm.state.acknowledged",
        AlarmState.Returned => "alarm.state.returned",
        AlarmState.Shelved => "alarm.state.shelved",
        _ => "alarm.state.inactive",
    };

    /// <summary>**列清单**（阶段 183）：与 <see cref="From"/> 共用同一份构造（报表设计器只要列）。</summary>
    public static IReadOnlyList<ReportColumn> Columns(Func<string, string> localize)
    {
        ArgumentNullException.ThrowIfNull(localize);
        return ColumnKeys.Select(k => new ReportColumn(k, localize(k))).ToList();
    }

    /// <summary>造表。时间走 <see cref="ReportExporter.FormatUtc"/>（UTC ＋ 显式 <c>Z</c>）；
    /// 触发值优先用原始字符串（<see cref="AlarmRecord.RawValue"/>），没有才退回按不变文化格式化的数值。</summary>
    public static ReportTable From(IEnumerable<AlarmRecord> records, Func<string, string> localize,
        ReportTimeRange? range = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(localize);

        var columns = Columns(localize).ToList();
        var rows = new List<IReadOnlyList<string>>();

        foreach (var record in records)
        {
            if (range is { } r && !r.Contains(record.OccurredAt)) continue;   // 时间范围（闭区间，UTC）
            var trigger = string.IsNullOrEmpty(record.RawValue)
                ? record.TriggerValue.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
                : record.RawValue!;

            rows.Add(new[]
            {
                ReportExporter.FormatUtc(record.OccurredAt),
                record.AlarmName ?? string.Empty,
                localize(SeverityKey(record.Severity)),
                localize(StateKey(record.State)),
                trigger,
                record.AcknowledgedBy ?? string.Empty,
            });
        }

        return new ReportTable { Title = localize(TitleKey), Columns = columns, Rows = rows };
    }
}
