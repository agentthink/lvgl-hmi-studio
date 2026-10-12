using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报警记录报表判据**（阶段 174）：列序固定、时间口径确定、**级别/状态走键**（不许直显枚举名）、
/// 触发值优先用原始字符串，且 Core 不认识语言。
/// </summary>
public class AlarmReportTests
{
    private static readonly Dictionary<string, string> Fake = new()
    {
        [AlarmReport.TitleKey] = "TITLE",
        [AlarmReport.ColumnKeys[0]] = "C_TIME",
        [AlarmReport.ColumnKeys[1]] = "C_NAME",
        [AlarmReport.ColumnKeys[2]] = "C_SEV",
        [AlarmReport.ColumnKeys[3]] = "C_STATE",
        [AlarmReport.ColumnKeys[4]] = "C_TRIGGER",
        [AlarmReport.ColumnKeys[5]] = "C_ACK",
        ["alarm.level.critical"] = "SEV_CRIT",
        ["alarm.state.active"] = "STATE_ACTIVE",
        ["alarm.state.returned"] = "STATE_RETURNED",
    };

    private static string Localize(string key) => Fake.TryGetValue(key, out var v) ? v : "?" + key;

    [Fact]
    public void ColumnOrderAndNames_ComeFromKeys()
    {
        var table = AlarmReport.From(Array.Empty<AlarmRecord>(), Localize);

        Assert.Equal(new[] { "C_TIME", "C_NAME", "C_SEV", "C_STATE", "C_TRIGGER", "C_ACK" },
            table.Columns.Select(c => c.DisplayName).ToArray());
        Assert.Equal(AlarmReport.ColumnKeys, table.Columns.Select(c => c.Key).ToArray());
        Assert.Equal("TITLE", table.Title);
    }

    /// <summary>取值列：级别/状态**走键**（不是枚举名）、时间 UTC＋Z、触发值用原始字符串。</summary>
    [Fact]
    public void RowCells_LocalizeSeverityAndState_AndKeepRawTriggerValue()
    {
        var record = new AlarmRecord
        {
            OccurredAt = new DateTime(2026, 10, 1, 9, 8, 7, DateTimeKind.Utc),
            AlarmName = "温度高",
            Severity = AlarmSeverity.Critical,
            State = AlarmState.Active,
            TriggerValue = 123.4567,
            RawValue = "123.4567 ℃",
            AcknowledgedBy = "op1",
        };

        var row = AlarmReport.From(new[] { record }, Localize).Rows.Single();

        Assert.Equal("2026-10-01 09:08:07Z", row[0]);
        Assert.Equal("温度高", row[1]);
        Assert.Equal("SEV_CRIT", row[2]);        // ← 键，不是 "Critical"
        Assert.Equal("STATE_ACTIVE", row[3]);    // ← 键，不是 "Active"
        Assert.Equal("123.4567 ℃", row[4]);      // ← 原始字符串优先
        Assert.Equal("op1", row[5]);
        Assert.DoesNotContain("Critical", string.Join("|", row), StringComparison.Ordinal);
        Assert.DoesNotContain("Active", string.Join("|", row), StringComparison.Ordinal);
    }

    /// <summary>没有原始字符串时退回按不变文化格式化的数值（不随区域变）。</summary>
    [Fact]
    public void TriggerValue_FallsBackToInvariantNumber()
    {
        var record = new AlarmRecord
        {
            OccurredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Severity = AlarmSeverity.Info,
            State = AlarmState.Returned,
            TriggerValue = 12.5,
            RawValue = null,
        };

        var row = AlarmReport.From(new[] { record }, Localize).Rows.Single();
        Assert.Equal("12.5", row[4]);
    }

    /// <summary>级别/状态的键映射：四个级别、四个状态各自成一键（**不许**互相串）。</summary>
    [Fact]
    public void SeverityAndStateKeys_AreDistinct()
    {
        var severities = new[]
        {
            AlarmSeverity.Info, AlarmSeverity.Warning, AlarmSeverity.Critical, AlarmSeverity.Emergency,
        }.Select(AlarmReport.SeverityKey).ToArray();
        Assert.Equal(4, severities.Distinct().Count());
        Assert.All(severities, k => Assert.StartsWith("alarm.level.", k, StringComparison.Ordinal));

        var states = new[]
        {
            AlarmState.Inactive, AlarmState.Active, AlarmState.Acknowledged, AlarmState.Returned,
        }.Select(AlarmReport.StateKey).ToArray();
        Assert.Equal(4, states.Distinct().Count());
        Assert.All(states, k => Assert.StartsWith("alarm.state.", k, StringComparison.Ordinal));
    }

    /// <summary>确定性：同一批记录两次造表逐单元格相同。</summary>
    [Fact]
    public void From_IsDeterministic()
    {
        var records = new[]
        {
            new AlarmRecord { OccurredAt = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc), AlarmName = "a", Severity = AlarmSeverity.Warning, State = AlarmState.Inactive, TriggerValue = 1, RawValue = "1" },
            new AlarmRecord { OccurredAt = new DateTime(2026, 2, 3, 4, 5, 7, DateTimeKind.Utc), AlarmName = "b", Severity = AlarmSeverity.Emergency, State = AlarmState.Returned, TriggerValue = 2, RawValue = null },
        };

        Assert.Equal(
            ReportExporter.ToCsv(AlarmReport.From(records, Localize)),
            ReportExporter.ToCsv(AlarmReport.From(records, Localize)));
    }
}
