using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.Alarm;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// **主报警表（alarm philosophy）报表**（阶段 266；登记 `§2-#11` 切片 ㊀）。
///
/// <para><b>它是什么</b>：ISA-18.2 要求每条报警在**主报警表**里写清三件事 ——
/// **原因**（为什么会报）/ **响应**（操作员该做什么）/ **后果**（不处理会怎样）。
/// 这张表把工程的报警定义直接导成机读版（工程交付物），列＝
/// 名称 / 级别 / **优先级** / 原因 / 响应 / 后果 / 关联控件。</para>
///
/// <para><b>为什么必须有这张表</b>：`AlarmConfig` 的三个职责字段若没有消费者就是**死字段**
/// （本仓有守卫盯"模型字段没人读"）。它同时是"职责字段"这一片的判据面之一。</para>
///
/// <para><b>空值策略（写死并判据化）</b>：未填写 ⇒ **留空列**，**不写** <c>null</c> / <c>N/A</c> / <c>—</c>
/// —— 老工程没有这三个字段，导出里出现字面量 <c>null</c> 会被误读成"值就是 null"。</para>
///
/// <para><b>与 <see cref="AlarmReport"/> 的分工</b>：那张是**运行时记录**报表
/// （时间/状态/确认人，来自 <see cref="AlarmRecord"/>）；这张是**静态定义**报表
/// （来自 <see cref="AlarmConfig"/>）。级别列的枚举映射**复用** <see cref="AlarmReport.SeverityKey"/>
/// —— 不抄第二份映射（抄第二份必然漂移）。</para>
/// </summary>
public static class AlarmPhilosophyReport
{
    /// <summary>列键（顺序即列序）：名称 / 级别 / 优先级 / 原因 / 响应 / 后果 / 关联控件。</summary>
    public static readonly string[] ColumnKeys =
    {
        "alarm.name",
        "alarm.severity",
        "alarm.priority",
        "alarm.cause",
        "alarm.response",
        "alarm.consequence",
        "alarm.philosophy.linked.control",
    };

    /// <summary>标题键。</summary>
    public const string TitleKey = "alarm.philosophy.title";

    /// <summary>
    /// 单元格口径：空白 ⇒ **空串**（不是 `null`/`N/A`）；非空白 ⇒ 去掉首尾空白后的原文。
    /// </summary>
    public static string Cell(string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();

    /// <summary>**列清单**（与 <see cref="From"/> 共用同一份构造：报表设计器只要列）。</summary>
    public static IReadOnlyList<ReportColumn> Columns(Func<string, string> localize)
    {
        ArgumentNullException.ThrowIfNull(localize);
        return ColumnKeys.Select(k => new ReportColumn(k, localize(k))).ToList();
    }

    /// <summary>
    /// 造表。⚠ **停用的报警也进表**（<see cref="AlarmConfig.IsEnabled"/> = false 不跳过）：
    /// 主报警表是**工程文档**（谁负责、为什么报），停用是"当下不生效"，不是"这条报警不存在" ——
    /// 这与运行时记录报表的取舍不同。
    /// </summary>
    public static ReportTable From(IEnumerable<AlarmConfig> alarms, Func<string, string> localize)
    {
        ArgumentNullException.ThrowIfNull(alarms);
        ArgumentNullException.ThrowIfNull(localize);

        var columns = Columns(localize).ToList();
        var rows = new List<IReadOnlyList<string>>();

        foreach (var alarm in alarms)
        {
            rows.Add(new[]
            {
                Cell(alarm.Name),
                localize(AlarmReport.SeverityKey(alarm.Severity)),   // 复用同一份枚举映射
                AlarmPriority.Label(AlarmPriority.Of(alarm)),          // 优先级 = f(严重度, 允许响应时间)
                Cell(alarm.Cause),
                Cell(alarm.Response),
                Cell(alarm.Consequence),
                Cell(alarm.LinkedControlId),
            });
        }

        return new ReportTable { Title = localize(TitleKey), Columns = columns, Rows = rows };
    }
}
