using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **审计报表数据源判据**（阶段 172）：列序固定、时间口径确定、结果按字典键渲染、
/// 且**Core 不硬编语言**（列名/标题/结果全走 <c>localize</c>）。
/// </summary>
public class AuditLogReportTests
{
    /// <summary>假字典：只记"键 → 文本"，用来证明所有显示文本都**经键**而来（不必切进程级语言）。</summary>
    private static readonly Dictionary<string, string> Fake = new()
    {
        [AuditLogReport.TitleKey] = "TITLE",
        [AuditLogReport.ColumnKeys[0]] = "C_TIME",
        [AuditLogReport.ColumnKeys[1]] = "C_USER",
        [AuditLogReport.ColumnKeys[2]] = "C_ACTION",
        [AuditLogReport.ColumnKeys[3]] = "C_RESULT",
        [AuditLogReport.ColumnKeys[4]] = "C_SOURCE",
        [AuditLogReport.ColumnKeys[5]] = "C_DETAIL",
        [AuditLogReport.ResultOkKey] = "R_OK",
        [AuditLogReport.ResultFailKey] = "R_FAIL",
    };

    private static string Localize(string key) => Fake.TryGetValue(key, out var v) ? v : "?" + key;

    [Fact]
    public void ColumnOrderAndNames_ComeFromKeys()
    {
        var table = AuditLogReport.From(Array.Empty<AuditLogEntry>(), Localize);

        Assert.Equal(new[] { "C_TIME", "C_USER", "C_ACTION", "C_RESULT", "C_SOURCE", "C_DETAIL" },
            table.Columns.Select(c => c.DisplayName).ToArray());
        Assert.Equal(AuditLogReport.ColumnKeys, table.Columns.Select(c => c.Key).ToArray());
        Assert.Equal("TITLE", table.Title);
    }

    [Fact]
    public void RowCells_FollowTheSameColumnOrder_WithUtcTimeAndLocalizedResult()
    {
        var entry = new AuditLogEntry
        {
            Timestamp = new DateTime(2026, 10, 1, 12, 34, 56, DateTimeKind.Utc),
            Username = "op1",
            Action = AuditAction.Login,
            Success = false,
            Source = "127.0.0.1",
            Detail = "bad password",
        };

        var row = AuditLogReport.From(new[] { entry }, Localize).Rows.Single();

        Assert.Equal("2026-10-01 12:34:56Z", row[0]);   // UTC + 显式 Z，不随区域变
        Assert.Equal("op1", row[1]);
        Assert.Equal("Login", row[2]);
        Assert.Equal("R_FAIL", row[3]);                  // 结果走键
        Assert.Equal("127.0.0.1", row[4]);
        Assert.Equal("bad password", row[5]);
    }

    /// <summary>确定性：同一批输入两次造表逐字节（逐单元格）相同。</summary>
    [Fact]
    public void From_IsDeterministic()
    {
        var entries = new[]
        {
            new AuditLogEntry { Timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), Username = "a", Success = true },
            new AuditLogEntry { Timestamp = new DateTime(2026, 1, 2, 3, 4, 6, DateTimeKind.Utc), Username = "b", Success = false },
        };

        var a = ReportExporter.ToCsv(AuditLogReport.From(entries, Localize));
        var b = ReportExporter.ToCsv(AuditLogReport.From(entries, Localize));
        Assert.Equal(a, b);
    }
}
