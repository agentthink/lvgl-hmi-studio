using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HmiEditor.Core.Models.Alarm;

namespace HmiEditor.Core.Reporting;

/// <summary>KPI 排行里的一行（按次数或按驻留时长）。</summary>
/// <param name="AlarmConfigId">报警配置 ID。</param>
/// <param name="AlarmName">报警名（取记录里的冗余名，配置被删也不会丢上下文）。</param>
/// <param name="Count">出现次数。</param>
/// <param name="DwellSeconds">这些记录的平均驻留秒数（已返回的才算；没有已返回的 ⇒ 0）。</param>
public sealed record AlarmKpiRank(string AlarmConfigId, string AlarmName, int Count, double DwellSeconds);

/// <summary>
/// **职责文档完备率**（D1=③，阶段 271）：三个职责字段（原因/响应/后果）**都**填了的报警占比。
/// </summary>
/// <param name="Total">报警总数（停用的也算 —— 见 <see cref="AlarmPhilosophyReport"/> 的口径）。</param>
/// <param name="Documented">三个职责字段都非空白的报警数。</param>
/// <param name="Ratio">完备率（0…1）；<paramref name="Total"/> = 0 ⇒ **0**（不是 NaN、也不是 1）。</param>
public sealed record AlarmDocumentationCompleteness(int Total, int Documented, double Ratio);

/// <summary>一组报警记录的 KPI 汇总（ISA-18.2 的"报警系统性能"四件套）。</summary>
/// <param name="TotalRecords">记录总数。</param>
/// <param name="OpenRecords">仍在驻留（<see cref="AlarmState.Returned"/> 之前）的记录数。</param>
/// <param name="RatePerHour">报警率（次/小时）。</param>
/// <param name="AverageDwellSeconds">平均驻留秒数（**只统计已返回的**；一条都没返回 ⇒ null）。</param>
/// <param name="ChatterAlarmCount">抖动报警数（在 <c>chatterWindow</c> 内重复 ≥2 次的报警个数）。</param>
/// <param name="TopByCount">按次数排行（降序，同数按名称序）。</param>
/// <param name="TopByDwell">按平均驻留排行（降序，同值按名称序）。</param>
public sealed record AlarmKpiSummary(
    int TotalRecords,
    int OpenRecords,
    double RatePerHour,
    double? AverageDwellSeconds,
    int ChatterAlarmCount,
    IReadOnlyList<AlarmKpiRank> TopByCount,
    IReadOnlyList<AlarmKpiRank> TopByDwell);

/// <summary>
/// **报警系统 KPI**（ISA-18.2 切片㊂ 的"可算的那半"；登记 `§2-#11`）。
///
/// <para><b>为什么先做 KPI、后做搁置</b>：搁置（shelving）会改动报警**状态模型**，而设备侧生成的
/// C 代码目前只有 state 0/1/2/3（`CodeGenerator.Alarm.cs:176-185`）⇒ 那需要先立**设备契约**
/// （与 `§2-#12` 的 M1 同款做法）。KPI 只读记录、不动状态机，能立刻交付且**逐条可判**。</para>
///
/// <para><b>口径（写死并判据化）</b>：
/// <list type="bullet">
///   <item><b>报警率</b> = 记录数 / 窗口小时数；窗口 ≤ 0 ⇒ **0**（不是除零、也不是"无意义"）。</item>
///   <item><b>驻留</b> = <see cref="AlarmRecord.ReturnedAt"/> − <see cref="AlarmRecord.OccurredAt"/>；
///     **只统计已返回的**（未返回的记录若按 0 计，会把"报警还挂着"美化成"驻留很短"）。
///     一条都没返回 ⇒ <c>null</c>（不是 0）。</item>
///   <item><b>抖动</b> = 同一 <see cref="AlarmRecord.AlarmConfigId"/> 在 <c>chatterWindow</c> 内
///     出现过 ≥2 次（边界：恰好等于窗口 ⇒ **算**，闭区间）。</item>
///   <item><b>排行</b>降序 ＋ 同值时按名称序 ⇒ **确定性**（否则同一份记录两次跑出不同报表）。</item>
/// </list></para>
/// </summary>
public static class AlarmKpi
{
    /// <summary>默认抖动判定窗口（ISA-18.2 常用 1 分钟内的重复报）。</summary>
    public static readonly TimeSpan DefaultChatterWindow = TimeSpan.FromMinutes(1);

    /// <summary>报警率（次/小时）。窗口 ≤ 0 ⇒ 0。</summary>
    public static double RatePerHour(int count, TimeSpan window)
        => window <= TimeSpan.Zero ? 0 : count / window.TotalHours;

    /// <summary>平均驻留秒数（只统计已返回的）；没有已返回的 ⇒ null。</summary>
    public static double? AverageDwellSeconds(IEnumerable<AlarmRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var dwells = records.Where(r => r.ReturnedAt.HasValue).Select(DwellOf).ToList();
        return dwells.Count == 0 ? null : dwells.Average();
    }

    /// <summary>抖动报警数（同一报警在窗口内 ≥2 次）。</summary>
    public static int ChatterAlarmCount(IEnumerable<AlarmRecord> records, TimeSpan chatterWindow)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (chatterWindow <= TimeSpan.Zero) return 0;

        var chatter = 0;
        foreach (var group in records.Where(r => !string.IsNullOrEmpty(r.AlarmConfigId))
                                     .GroupBy(r => r.AlarmConfigId, StringComparer.Ordinal))
        {
            var times = group.Select(r => r.OccurredAt).OrderBy(t => t).ToList();
            for (var i = 1; i < times.Count; i++)
            {
                if (times[i] - times[i - 1] <= chatterWindow)   // 闭区间
                {
                    chatter++;
                    break;                                       // 同一报警只算一次
                }
            }
        }

        return chatter;
    }

    /// <summary>
    /// 汇总。窗口用于报警率；<paramref name="topN"/> 用于两个排行（≤0 ⇒ 不给排行）。
    /// </summary>
    public static AlarmKpiSummary Compute(
        IEnumerable<AlarmRecord> records,
        TimeSpan window,
        TimeSpan? chatterWindow = null,
        int topN = 10)
    {
        ArgumentNullException.ThrowIfNull(records);

        var list = records.ToList();
        var chatter = ChatterAlarmCount(list, chatterWindow ?? DefaultChatterWindow);

        var ranks = list.Where(r => !string.IsNullOrEmpty(r.AlarmConfigId))
            .GroupBy(r => r.AlarmConfigId, StringComparer.Ordinal)
            .Select(g => new AlarmKpiRank(
                g.Key,
                g.Select(r => r.AlarmName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "",
                g.Count(),
                AverageDwellSeconds(g) ?? 0))
            .ToList();

        return new AlarmKpiSummary(
            list.Count,
            list.Count(r => r.State != AlarmState.Returned),
            RatePerHour(list.Count, window),
            AverageDwellSeconds(list),
            chatter,
            topN <= 0 ? Array.Empty<AlarmKpiRank>() : Rank(ranks, topN, byDwell: false),
            topN <= 0 ? Array.Empty<AlarmKpiRank>() : Rank(ranks, topN, byDwell: true));
    }

    /// <summary>降序 ＋ 同值按名称序（**确定性**：同一份记录两次跑必须给出同一个顺序）。</summary>
    private static IReadOnlyList<AlarmKpiRank> Rank(IReadOnlyList<AlarmKpiRank> ranks, int topN, bool byDwell)
        => (byDwell
                ? ranks.OrderByDescending(r => r.DwellSeconds).ThenBy(r => r.AlarmName, StringComparer.Ordinal)
                : ranks.OrderByDescending(r => r.Count).ThenBy(r => r.AlarmName, StringComparer.Ordinal))
            .ThenBy(r => r.AlarmConfigId, StringComparer.Ordinal)
            .Take(topN)
            .ToList();

    private static double DwellOf(AlarmRecord record) =>
        (record.ReturnedAt!.Value - record.OccurredAt).TotalSeconds;

    /// <summary>
    /// 职责文档完备率（D1=③）：三个字段**都**非空白才算"填了" ——
    /// 只填一个字段的报警在 ISA-18.2 里不算合格的主报警表条目。
    /// </summary>
    public static AlarmDocumentationCompleteness DocumentationCompleteness(IEnumerable<AlarmConfig> alarms)
    {
        ArgumentNullException.ThrowIfNull(alarms);

        var total = 0;
        var documented = 0;
        foreach (var alarm in alarms)
        {
            total++;
            if (IsFilled(alarm.Cause) && IsFilled(alarm.Response) && IsFilled(alarm.Consequence)) documented++;
        }

        return new AlarmDocumentationCompleteness(total, documented, total == 0 ? 0 : (double)documented / total);
    }

    private static bool IsFilled(string? text) => !string.IsNullOrWhiteSpace(text);

    /// <summary>格式化一个 KPI 值（不变文化：报表里 `1.5` 不许变成 `1,5`）。</summary>
    public static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
