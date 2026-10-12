using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Reporting;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报警系统 KPI**（ISA-18.2 切片㊂ 可算的那半；登记 `§2-#11`）：
/// 报警率 / 平均驻留 / 抖动 / 两个排行，全部是记录上的**纯函数** ⇒ 逐条可判。
/// </summary>
public class AlarmKpiTests
{
    private readonly ITestOutputHelper _out;

    public AlarmKpiTests(ITestOutputHelper o) => _out = o;

    private static readonly DateTime T0 = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>造一条记录：<paramref name="occurredMinutes"/> 起，<paramref name="dwellSeconds"/> 后返回（null ⇒ 未返回）。</summary>
    private static AlarmRecord Rec(string configId, string name, double occurredMinutes,
                                   double? dwellSeconds, AlarmState? state = null)
    {
        var occurred = T0.AddMinutes(occurredMinutes);
        var returned = dwellSeconds is { } d ? occurred.AddSeconds(d) : (DateTime?)null;
        return new AlarmRecord
        {
            AlarmConfigId = configId,
            AlarmName = name,
            OccurredAt = occurred,
            ReturnedAt = returned,
            State = state ?? (returned.HasValue ? AlarmState.Returned : AlarmState.Active),
        };
    }

    [Fact]
    public void RatePerHour_DividesByTheWindow_AndIsZeroForNonPositiveWindows()
    {
        Assert.Equal(2.0, AlarmKpi.RatePerHour(12, TimeSpan.FromHours(6)), 6);
        Assert.Equal(60.0, AlarmKpi.RatePerHour(1, TimeSpan.FromMinutes(1)), 6);
        Assert.Equal(0.0, AlarmKpi.RatePerHour(5, TimeSpan.Zero), 6);
        Assert.Equal(0.0, AlarmKpi.RatePerHour(5, TimeSpan.FromHours(-1)), 6);
    }

    [Fact]
    public void AverageDwell_CountsOnlyReturnedRecords_AndIsNullWhenNoneReturned()
    {
        var records = new[]
        {
            Rec("a", "温度高", 0, 10),
            Rec("a", "温度高", 30, 20),
            Rec("b", "压力低", 60, null),      // 未返回 ⇒ 不进平均（按 0 计会把"还挂着"美化成"驻留很短"）
        };

        Assert.Equal(15.0, AlarmKpi.AverageDwellSeconds(records)!.Value, 6);
        Assert.Null(AlarmKpi.AverageDwellSeconds(new[] { Rec("b", "压力低", 0, null) }));
        Assert.Null(AlarmKpi.AverageDwellSeconds(Array.Empty<AlarmRecord>()));
    }

    [Fact]
    public void Chatter_CountsEachAlarmAtMostOnce_AndUsesAClosedWindow()
    {
        // "a" 三条，最大间隔 60 s = 窗口 ⇒ **算**抖动（闭区间）
        var a = new[] { Rec("a", "A", 0, null), Rec("a", "A", 1, null), Rec("a", "A", 2, null) };
        // "b" 两条间隔 60.5 s ⇒ 不算
        var b = new[] { Rec("b", "B", 0, null), Rec("b", "B", 1.0084, null) };

        Assert.Equal(1, AlarmKpi.ChatterAlarmCount(a, TimeSpan.FromMinutes(1)));
        // 同一个报警出现三次 ⇒ 仍然只算 **1 个**抖动报警（不按次数累加）
        Assert.Equal(1, AlarmKpi.ChatterAlarmCount(
            a.Concat(new[] { Rec("a", "A", 3, null) }), TimeSpan.FromMinutes(1)));
        // 两条间隔 60.5 s ⇒ 不算（超过窗口）
        Assert.Equal(0, AlarmKpi.ChatterAlarmCount(new[] { Rec("b", "B", 0, null), Rec("b", "B", 1.0084, null) },
            TimeSpan.FromMinutes(1)));
        Assert.Equal(0, AlarmKpi.ChatterAlarmCount(a, TimeSpan.Zero));   // 窗口 ≤0 ⇒ 0
    }

    [Fact]
    public void Compute_ReportsCountsRateAndBothRankings_Deterministically()
    {
        var records = new[]
        {
            Rec("a", "温度高", 0, 10),
            Rec("a", "温度高", 5, 20),
            Rec("a", "温度高", 10, 30),
            Rec("b", "压力低", 0, 60),
            Rec("c", "液位高", 0, null),      // 未返回
        };

        var summary = AlarmKpi.Compute(records, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1), topN: 2);

        Assert.Equal(5, summary.TotalRecords);
        Assert.Equal(1, summary.OpenRecords);                        // 只有 "c" 未返回
        Assert.Equal(5.0, summary.RatePerHour, 6);
        Assert.Equal(30.0, summary.AverageDwellSeconds!.Value, 6);   // (10+20+30+60)/4
        // 这份夹具里 "a" 的三条相隔 5 分钟 ⇒ **不抖**（抖动的正例在 Chatter_… 那条里）；
        // 写清楚是为了避免"看到 0 就以为算错了"。
        Assert.Equal(0, summary.ChatterAlarmCount);

        // 按次数：a=3 首位；b/c 同为 1 ⇒ 按名称序（压力低 < 液位高）
        Assert.Equal(new[] { "a", "b" }, summary.TopByCount.Select(r => r.AlarmConfigId).ToArray());
        // 按平均驻留：b=60 首位；a=20 次位
        Assert.Equal(new[] { "b", "a" }, summary.TopByDwell.Select(r => r.AlarmConfigId).ToArray());
        Assert.Equal(3, summary.TopByCount[0].Count);
        Assert.Equal(20.0, summary.TopByCount[0].DwellSeconds, 6);

        // 确定性：同一份记录再算一次，排行逐项相同
        var again = AlarmKpi.Compute(records, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1), topN: 2);
        Assert.Equal(summary.TopByCount, again.TopByCount);
        Assert.Equal(summary.TopByDwell, again.TopByDwell);

        JudgeReadings.Write(_out, "报警 KPI · 汇总（§2-#11 切片㈢ · 阶段 268）", new[]
        {
            $"总数 {summary.TotalRecords} / 未恢复 {summary.OpenRecords} / 报警率 {AlarmKpi.Format(summary.RatePerHour)} 次/小时",
            $"平均驻留 {AlarmKpi.Format(summary.AverageDwellSeconds ?? 0)} s（只算已返回）/ 抖动报警 {summary.ChatterAlarmCount} 个",
            "按次数 Top：" + string.Join(" > ", summary.TopByCount.Select(r => $"{r.AlarmName}({r.Count})")),
            "按驻留 Top：" + string.Join(" > ", summary.TopByDwell.Select(r => $"{r.AlarmName}({AlarmKpi.Format(r.DwellSeconds)}s)")),
        });
    }

    [Fact]
    public void Compute_WithTopNZero_GivesNoRankings()
    {
        var summary = AlarmKpi.Compute(new[] { Rec("a", "A", 0, 1) }, TimeSpan.FromHours(1), topN: 0);
        Assert.Empty(summary.TopByCount);
        Assert.Empty(summary.TopByDwell);
    }

    [Fact]
    public void KpiReport_RendersSixMetricRows_AndUsesTheNotComputablePlaceholder()
    {
        var summary = AlarmKpi.Compute(
            new[] { Rec("a", "A", 0, null) },        // 一条都没返回 ⇒ 驻留算不出来
            TimeSpan.FromHours(1), topN: 5);

        var table = AlarmKpiReport.From(summary, alarms: null, key => $"«{key}»");

        Assert.Equal(AlarmKpiReport.ColumnKeys, table.Columns.Select(c => c.Key).ToArray());
        Assert.Equal(AlarmKpiReport.MetricKeys, table.Rows.Select(r => r[0].Trim('«', '»')).ToArray());
        Assert.Equal(6, table.Rows.Count);
        Assert.Equal(AlarmKpiReport.NotComputable, table.Rows[3][1]);       // 平均驻留 = —
        Assert.Equal(AlarmKpiReport.NotComputable, table.Rows[5][1]);       // 没传报警定义 ⇒ 完备率也算不出来

        // 导出走既有报告框架：表头是两列、标题是 KPI 标题键、第一行是指标行
        var csv = ReportExporter.ToCsv(table);
        Assert.Contains("«alarm.kpi.col.metric»", csv, StringComparison.Ordinal);
        Assert.Contains("«alarm.kpi.col.value»", csv, StringComparison.Ordinal);
        Assert.Contains("«alarm.kpi.total»", csv, StringComparison.Ordinal);
        Assert.Contains(AlarmKpiReport.NotComputable, csv, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentationCompleteness_RequiresAllThreeFields_AndIsZeroForEmptySet()
    {
        var full = new AlarmConfig { Name = "全填", Cause = "冷却失效", Response = "开备用泵", Consequence = "超温停车" };
        var partial = new AlarmConfig { Name = "只填一个", Cause = "冷却失效" };              // 不算"填了"
        var blank = new AlarmConfig { Name = "空白", Cause = "  ", Response = "  ", Consequence = "  " };

        var one = AlarmKpi.DocumentationCompleteness(new[] { full });
        Assert.Equal(1, one.Total);
        Assert.Equal(1, one.Documented);
        Assert.Equal(1.0, one.Ratio, 6);

        var three = AlarmKpi.DocumentationCompleteness(new[] { full, partial, blank });
        Assert.Equal(3, three.Total);
        Assert.Equal(1, three.Documented);
        Assert.Equal(1.0 / 3.0, three.Ratio, 6);

        var empty = AlarmKpi.DocumentationCompleteness(Array.Empty<AlarmConfig>());
        Assert.Equal(0, empty.Total);
        Assert.Equal(0.0, empty.Ratio, 6);        // 空集 ⇒ 0（不是 NaN，也不是"全完备"）
    }

    [Fact]
    public void KpiReport_WithAlarmDefinitions_ShowsTheCompletenessRow()
    {
        var summary = AlarmKpi.Compute(Array.Empty<AlarmRecord>(), TimeSpan.FromHours(1), topN: 0);
        var alarms = new[]
        {
            new AlarmConfig { Name = "a", Cause = "c", Response = "r", Consequence = "k" },
            new AlarmConfig { Name = "b" },      // 没填 ⇒ 完备率 1/2 = 50%
        };

        var table = AlarmKpiReport.From(summary, alarms, key => $"«{key}»");

        Assert.Equal(6, table.Rows.Count);
        Assert.Equal("50%（1/2）", table.Rows[5][1]);

        JudgeReadings.Write(_out, "报警 KPI · 职责文档完备率（D1=③ · 阶段 271）", new[]
        {
            "口径：原因/响应/后果**三个都非空白**才算填了；空集 ⇒ 0（不是 NaN/全完备）",
            "本例：2 条报警、1 条填全 ⇒ " + table.Rows[5][1],
        });
    }
}
