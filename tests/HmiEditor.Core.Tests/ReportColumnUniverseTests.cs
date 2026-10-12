using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **列清单只有一个来源**（阶段 183）：报表设计器（只要列）与导出（列 ＋ 行）必须给出**同一份**
/// 列清单 —— 漂移的后果是"设计器里排好的顺序在导出时对不上"，而且**不会报错**。
/// 所以三个数据源逐个对拍：<c>XxxReport.Columns(localize)</c> ≡ <c>XxxReport.From(...).Columns</c>。
/// </summary>
public class ReportColumnUniverseTests
{
    /// <summary>假字典（判据不切进程级语言；回显键名由一个前缀标记出来，便于识别"没走 localize"）。</summary>
    private static string L(string key) => $"L:{key}";

    private static void AssertSameColumns(IReadOnlyList<ReportColumn> expected, IReadOnlyList<ReportColumn> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Key, actual[i].Key);
            Assert.Equal(expected[i].DisplayName, actual[i].DisplayName);
        }
    }

    [Fact]
    public void AuditColumns_MatchTheTable()
    {
        var columns = AuditLogReport.Columns(L);
        AssertSameColumns(columns, AuditLogReport.From(Array.Empty<AuditLogEntry>(), L).Columns);

        // 正控：列名真的走过 localize（不是把键当名字显出去），且列数与 ColumnKeys 对齐
        Assert.Equal(AuditLogReport.ColumnKeys.Length, columns.Count);
        Assert.All(columns, c => Assert.Equal($"L:{c.Key}", c.DisplayName));
    }

    [Fact]
    public void AlarmColumns_MatchTheTable()
    {
        var columns = AlarmReport.Columns(L);
        AssertSameColumns(columns, AlarmReport.From(Array.Empty<AlarmRecord>(), L).Columns);

        Assert.Equal(AlarmReport.ColumnKeys.Length, columns.Count);
        Assert.All(columns, c => Assert.Equal($"L:{c.Key}", c.DisplayName));
    }

    /// <summary>历史数据的列是**动态的**：时间列 ＋ 每个变量一列（列名＝变量 Id，**不翻译**）。</summary>
    [Fact]
    public void HistoryColumns_MatchTheTable_AndKeepTheVariableOrder()
    {
        var ids = new[] { "tank_level", "pump_speed" };

        var columns = HistoryReport.Columns(ids, L);
        AssertSameColumns(columns, HistoryReport.From(Array.Empty<TrendSample>(), ids, L).Columns);

        Assert.Equal(3, columns.Count);
        Assert.Equal(HistoryReport.TimeColumnKey, columns[0].Key);
        Assert.Equal("tank_level", columns[1].Key);
        Assert.Equal("tank_level", columns[1].DisplayName);   // 变量 Id 是数据标识符 ⇒ 不翻译
        Assert.Equal("pump_speed", columns[2].Key);

        // 变量顺序就是列序（顺序一旦分叉，报表的列与值会**错位**且不报错）
        var reversed = HistoryReport.Columns(new[] { "pump_speed", "tank_level" }, L);
        Assert.Equal("pump_speed", reversed[1].Key);
        Assert.Equal("tank_level", reversed[2].Key);
    }

    [Fact]
    public void Columns_RejectNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => AuditLogReport.Columns(null!));
        Assert.Throws<ArgumentNullException>(() => AlarmReport.Columns(null!));
        Assert.Throws<ArgumentNullException>(() => HistoryReport.Columns(new[] { "v" }, null!));
        Assert.Throws<ArgumentNullException>(() => HistoryReport.Columns(null!, L));
    }

    /// <summary>
    /// **设计与导出串起来**（端到端那条最小闭环）：把 <c>Columns</c> 给的清单做成模板（改序 ＋ 改名 ＋ 隐藏）
    /// 再套到报表上 —— 这是设计器的真实用法，判据在 Core 侧先把它跑通。
    /// </summary>
    [Fact]
    public void TemplateBuiltFromTheColumnUniverse_AppliesToTheTable()
    {
        var columns = HistoryReport.Columns(new[] { "a", "b", "c" }, L);
        var template = new ReportTemplate(new[]
        {
            new ReportTemplateEntry("c", "第三列"),
            new ReportTemplateEntry("a", Hidden: true),
        });

        var table = HistoryReport.From(Array.Empty<TrendSample>(), new[] { "a", "b", "c" }, L)
            .ApplyTemplate(template);

        // 原列序是 [时间, a, b, c]；模板列了 c（排最前）、隐藏 a ⇒ 没列到的"时间"与 b 按原序接在后面
        Assert.Equal(new[] { "c", HistoryReport.TimeColumnKey, "b" }, table.Columns.Select(x => x.Key).ToArray());
        Assert.Equal(new[] { "第三列", $"L:{HistoryReport.TimeColumnKey}", "b" }, table.Columns.Select(x => x.DisplayName).ToArray());

        // 前提自检：清单里确实有 4 列（含时间列）——否则上面的断言是"在空集上成立"
        Assert.Equal(4, columns.Count);
    }
}
