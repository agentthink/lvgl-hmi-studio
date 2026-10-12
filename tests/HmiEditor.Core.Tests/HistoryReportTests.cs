using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **历史数据报表判据**（阶段 175）：列是**动态的**（时间 ＋ 每个变量一列）、变量列名用 Id 原文、
/// 值按采样顺序对齐（短的补空**而不是 0**）、数值走不变文化、**行数封顶取最近的**。
/// </summary>
public class HistoryReportTests
{
    private static readonly string[] Ids = { "Temp", "Pressure" };

    private static string Localize(string key) => key switch
    {
        HistoryReport.TitleKey => "TITLE",
        HistoryReport.TimeColumnKey => "C_TIME",
        _ => key,   // 变量 Id 原样
    };

    private static TrendSample Sample(string iso, params double[] values) => new()
    {
        Timestamp = DateTime.Parse(iso, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal),
        Values = values.ToList(),
    };

    [Fact]
    public void Columns_AreTimePlusVariableIdsInSampleOrder()
    {
        var table = HistoryReport.From(Array.Empty<TrendSample>(), Ids, Localize);

        Assert.Equal(new[] { HistoryReport.TimeColumnKey, "Temp", "Pressure" },
            table.Columns.Select(c => c.Key).ToArray());
        Assert.Equal(new[] { "C_TIME", "Temp", "Pressure" },       // 变量列名 = Id 原文（数据标识符不翻）
            table.Columns.Select(c => c.DisplayName).ToArray());
        Assert.Equal("TITLE", table.Title);
    }

    [Fact]
    public void Rows_AlignValuesToVariableOrder_AndLeaveMissingAsEmpty()
    {
        var samples = new[]
        {
            Sample("2026-10-01T00:00:00Z", 1.5, 2.25),
            Sample("2026-10-01T00:00:01Z", 3),          // 只有第一个变量的值
        };

        var rows = HistoryReport.From(samples, Ids, Localize).Rows;

        Assert.Equal(new[] { "2026-10-01 00:00:00Z", "1.5", "2.25" }, rows[0]);
        Assert.Equal("2026-10-01 00:00:01Z", rows[1][0]);
        Assert.Equal("3", rows[1][1]);
        // ⚠ 缺的值是**空**，不是 0 —— 0 是"确实采到 0"，两者必须可区分
        Assert.Equal(string.Empty, rows[1][2]);

        // 反向断言：不许出现"用 0 凑齐"的形状
        Assert.DoesNotContain(rows.SelectMany(r => r), v => v == "0");
    }

    /// <summary>数值格式不随区域变（千分位/小数点都不许受当前文化影响）。</summary>
    [Fact]
    public void Numbers_UseInvariantFormatting()
    {
        var rows = HistoryReport.From(new[] { Sample("2026-10-01T00:00:00Z", 1234.5678, -0.5) }, Ids, Localize).Rows;
        Assert.Equal("1234.5678", rows[0][1]);
        Assert.Equal("-0.5", rows[0][2]);
    }

    /// <summary>封顶：只保留**最近**的 maxRows 条（不是最早的那些）。</summary>
    [Fact]
    public void Truncation_KeepsTheLatestRows()
    {
        var samples = Enumerable.Range(0, 10)
            .Select(i => Sample($"2026-10-01T00:00:{i:00}Z", i, i)).ToArray();

        var rows = HistoryReport.From(samples, Ids, Localize, maxRows: 3).Rows;

        Assert.Equal(3, rows.Count);
        Assert.Equal("2026-10-01 00:00:07Z", rows[0][0]);
        Assert.Equal("2026-10-01 00:00:09Z", rows[2][0]);

        // 正控：maxRows ≤ 0 = 不封顶
        Assert.Equal(10, HistoryReport.From(samples, Ids, Localize, maxRows: 0).Rows.Count);
    }

    /// <summary>没有变量（还没配画笔）时只有时间列 —— 不许崩、也不许造出一张空列表头都没有的表。</summary>
    [Fact]
    public void WithoutVariables_OnlyTheTimeColumnRemains()
    {
        var table = HistoryReport.From(new[] { Sample("2026-10-01T00:00:00Z") }, Array.Empty<string>(), Localize);
        Assert.Single(table.Columns);
        Assert.Single(table.Rows);
        Assert.Single(table.Rows[0]);
    }

    [Fact]
    public void From_IsDeterministic()
    {
        var samples = new[] { Sample("2026-10-01T00:00:00Z", 1, 2), Sample("2026-10-01T00:00:01Z", 3, 4) };
        Assert.Equal(
            ReportExporter.ToCsv(HistoryReport.From(samples, Ids, Localize)),
            ReportExporter.ToCsv(HistoryReport.From(samples, Ids, Localize)));
    }
}
