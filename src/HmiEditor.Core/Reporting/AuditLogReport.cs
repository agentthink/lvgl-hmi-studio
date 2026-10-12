using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.User;

namespace HmiEditor.Core.Reporting;

/// <summary>
/// **审计日志报表**（阶段 172，报表能力 MVP 的第一条数据源）：把
/// <see cref="AuditLogEntry"/> 翻成一张 <see cref="ReportTable"/>。
///
/// <para><b>列序固定</b>（判据按 <see cref="ColumnKeys"/> 对齐）：时间 / 用户 / 操作 / 结果 / 来源 / 详情。
/// ⚠ 与选型文档初稿写的"时间/用户/**角色**/动作/结果/详情"有一处出入：**模型里没有角色字段**
/// （<see cref="AuditLogEntry"/> 有 <c>UserId/Username/Action/Detail/Success/Source/SessionId/Metadata</c>）
/// ⇒ 按**实际字段**定列，改动记在专题里（不硬凑一个不存在的列）。</para>
///
/// <para><b>Core 不认识语言</b>：列名与"成功/失败"都通过 <paramref name="localize"/> 由调用方（App）提供 ——
/// 这也是本仓既有的「Core 给 Id、App 推键」形状（同 <c>ActionTextKeys</c>）。</para>
/// </summary>
public static class AuditLogReport
{
    /// <summary>
    /// 列键（**顺序即报表列顺序**）。⚠ **复用**用户管理器审计页那一组词
    /// （<c>user.mgr.audit.*</c>）—— 同一组"时间/用户/操作/结果/来源/详情"不再造第二套
    /// （本仓纪律：同一概念一套措辞；阶段 170 刚把术语统一过，没理由再分叉）。
    /// </summary>
    public static readonly string[] ColumnKeys =
    {
        "user.mgr.audit.time",
        "user.mgr.audit.user",
        "user.mgr.audit.action",
        "user.mgr.audit.result",
        "user.mgr.audit.source",
        "user.mgr.audit.detail",
    };

    /// <summary>标题键。</summary>
    public const string TitleKey = "audit.report.title";

    /// <summary>"成功" / "失败" 的键（按 <c>Success</c> 取）—— 复用既有的通用词
    /// <c>gen.success</c> / <c>gen.failed</c>，不另造。</summary>
    public const string ResultOkKey = "gen.success";

    /// <inheritdoc cref="ResultOkKey"/>
    public const string ResultFailKey = "gen.failed";

    /// <summary>
    /// **列清单**（阶段 183）：报表设计器只要列、不要行 ⇒ 这条与 <see cref="From"/> 共用同一份构造，
    /// 免得"设计与导出"两份列清单漂移（判据 <c>ReportColumnUniverseTests</c> 逐源对拍）。
    /// </summary>
    public static IReadOnlyList<ReportColumn> Columns(Func<string, string> localize)
    {
        ArgumentNullException.ThrowIfNull(localize);
        return ColumnKeys.Select(k => new ReportColumn(k, localize(k))).ToList();
    }

    /// <summary>
    /// 造表。<paramref name="localize"/> 把键翻成显示文本（App 侧传 <c>AppLocalization.Service[...]</c>；
    /// 判据传一个假字典即可 ⇒ 不用切进程级语言）。
    /// 时间走 <see cref="ReportExporter.FormatUtc"/>（UTC ＋ 显式 Z，不随区域变）。
    /// </summary>
    public static ReportTable From(IEnumerable<AuditLogEntry> entries, Func<string, string> localize,
        ReportTimeRange? range = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(localize);

        var columns = Columns(localize).ToList();
        var rows = new List<IReadOnlyList<string>>();

        foreach (var entry in entries)
        {
            if (range is { } r && !r.Contains(entry.Timestamp)) continue;   // 时间范围（闭区间，UTC）
            rows.Add(new[]
            {
                ReportExporter.FormatUtc(entry.Timestamp),
                entry.Username ?? string.Empty,
                entry.Action.ToString(),
                localize(entry.Success ? ResultOkKey : ResultFailKey),
                entry.Source ?? string.Empty,
                entry.Detail ?? string.Empty,
            });
        }

        return new ReportTable
        {
            Title = localize(TitleKey),
            Columns = columns,
            Rows = rows,
        };
    }
}
