using System;
using System.Collections.Generic;
using System.Linq;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// **报警系统 KPI 报表**（ISA-18.2 切片㊂；登记 `§2-#11`）—— 把 <see cref="AlarmKpiSummary"/>
/// 翻成一张两列（指标 / 值）的 <see cref="ReportTable"/>，走既有报告框架（CSV / HTML / xlsx）。
///
/// <para><b>为什么要有它</b>：KPI 纯函数若没有落点，运维拿不到东西 —— 本仓的报表框架已经在
/// （`ReportExporter`），加一张表比新造一条导出链便宜，也自动获得"逐字节可判"的好处。</para>
///
/// <para><b>未返回的记录不算驻留</b>：平均驻留一行在"一条都没返回"时显示 <c>—</c>
/// （与 <see cref="AlarmPhilosophyReport"/> 的"留空列"口径不同 —— 那里是**用户没填**，
/// 这里是**算不出来**，两者不该用同一个符号）。</para>
/// </summary>
public static class AlarmKpiReport
{
    /// <summary>列键：指标 / 值。</summary>
    public static readonly string[] ColumnKeys = { "alarm.kpi.col.metric", "alarm.kpi.col.value" };

    /// <summary>标题键。</summary>
    public const string TitleKey = "alarm.kpi.title";

    /// <summary>行键（顺序即行序）。</summary>
    public static readonly string[] MetricKeys =
    {
        "alarm.kpi.total",
        "alarm.kpi.open",
        "alarm.kpi.rate.per.hour",
        "alarm.kpi.avg.dwell.seconds",
        "alarm.kpi.chatter",
        "alarm.kpi.documented.ratio",
    };

    /// <summary>算不出来时的占位（**只**用于"无法计算"，不用于"用户未填"）。</summary>
    public const string NotComputable = "—";

    public static IReadOnlyList<ReportColumn> Columns(Func<string, string> localize)
    {
        ArgumentNullException.ThrowIfNull(localize);
        return ColumnKeys.Select(k => new ReportColumn(k, localize(k))).ToList();
    }

    /// <summary>
    /// 造表：**六行**（总数 / 未恢复 / 报警率 / 平均驻留 / 抖动报警数 / 职责文档完备率）。
    /// 完备率需要报警**定义**（不只是记录）⇒ <paramref name="alarms"/> 传工程里的报警配置；
    /// 传 null ⇒ 该行显示 <see cref="NotComputable"/>。
    /// </summary>
    public static ReportTable From(AlarmKpiSummary summary, IEnumerable<Models.Alarm.AlarmConfig>? alarms,
                                   Func<string, string> localize)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(localize);

        var completeness = alarms is null ? null : AlarmKpi.DocumentationCompleteness(alarms);

        var values = new[]
        {
            summary.TotalRecords.ToString(System.Globalization.CultureInfo.InvariantCulture),
            summary.OpenRecords.ToString(System.Globalization.CultureInfo.InvariantCulture),
            AlarmKpi.Format(summary.RatePerHour),
            summary.AverageDwellSeconds is { } dwell ? AlarmKpi.Format(dwell) : NotComputable,
            summary.ChatterAlarmCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            completeness is null
                ? NotComputable
                : $"{AlarmKpi.Format(completeness.Ratio * 100)}%（{completeness.Documented}/{completeness.Total}）",
        };

        var rows = MetricKeys.Select((key, i) => (IReadOnlyList<string>)new[] { localize(key), values[i] }).ToList();

        return new ReportTable { Title = localize(TitleKey), Columns = Columns(localize), Rows = rows };
    }
}
