using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Reporting;
using HmiEditor.Core.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **主报警表（alarm philosophy）报表 ＋ ISA-18.2 职责字段**（阶段 266；登记 `§2-#11` 切片 ㊀）。
///
/// <para>四个判据面对应方案 §2 切片 ㊀：① 模型 JSON 往返（含"老工程没有这三个字段"）·
/// ② 报表**列序与内容**（含空值策略：留空列，不写 `null`/`N/A`）· ③ 停用报警也进表 ·
/// ④ 级别列**复用** <see cref="AlarmReport.SeverityKey"/>（不抄第二份映射）。</para>
/// </summary>
public class AlarmPhilosophyReportTests
{
    private readonly ITestOutputHelper _out;

    public AlarmPhilosophyReportTests(ITestOutputHelper o) => _out = o;

    /// <summary>本地化替身：返回 `«键»`，让列名/取值在断言里一眼看出键。</summary>
    private static string Loc(string key) => $"«{key}»";

    [Fact]
    public void Columns_FollowTheDeclaredKeyOrder_AndAllHaveLabels()
    {
        var columns = AlarmPhilosophyReport.Columns(Loc);

        Assert.Equal(AlarmPhilosophyReport.ColumnKeys, columns.Select(c => c.Key).ToArray());
        Assert.Equal(new[]
        {
            "alarm.name", "alarm.severity", "alarm.priority", "alarm.cause", "alarm.response",
            "alarm.consequence", "alarm.philosophy.linked.control",
        }, columns.Select(c => c.Key).ToArray());
        Assert.All(columns, c => Assert.False(string.IsNullOrWhiteSpace(c.DisplayName)));
    }

    [Fact]
    public void EmptyDutyFields_AreBlankCells_NotPlaceholders()
    {
        // 老工程：三个字段全 null（等价于 JSON 里根本没有这些键）
        var alarm = new AlarmConfig { Name = "电机过载", Severity = AlarmSeverity.Critical };
        Assert.Null(alarm.Cause);
        Assert.Null(alarm.Response);
        Assert.Null(alarm.Consequence);

        var table = AlarmPhilosophyReport.From(new[] { alarm }, Loc);
        var row = Assert.Single(table.Rows);

        Assert.Equal("电机过载", row[0]);
        Assert.Equal("«alarm.level.critical»", row[1]);
        Assert.Equal(AlarmPriority.Label(AlarmPriority.Of(alarm)), row[2]);   // 优先级列恒有值（推导量）
        foreach (var cell in new[] { row[3], row[4], row[5], row[6] })
        {
            Assert.Equal("", cell);                       // 空值策略：留空列
            Assert.DoesNotContain("null", cell, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("N/A", cell, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("—", cell, StringComparison.Ordinal);
        }

        JudgeReadings.Write(_out, "主报警表 · 空值策略（§2-#11 切片㊀ · 阶段 266）", new[]
        {
            "列序：" + string.Join(" / ", AlarmPhilosophyReport.ColumnKeys),
            "老工程（三字段 null）⇒ 原因/响应/后果/关联控件四格均为空串（不含 null / N/A / —）",
        });
    }

    [Fact]
    public void Rows_CarryTheDutyFields_AndTrimWhitespace()
    {
        var alarm = new AlarmConfig
        {
            Name = "  温度高  ",
            Severity = AlarmSeverity.Warning,
            Cause = "  冷却水阀关断  ",
            Response = " 打开备用泵并通知班长 ",
            Consequence = "反应釜超温",
            LinkedControlId = "lblTemp",
            IsEnabled = false,                              // 停用也必须进表
        };

        var row = Assert.Single(AlarmPhilosophyReport.From(new[] { alarm }, Loc).Rows);

        Assert.Equal("温度高", row[0]);                     // 去首尾空白
        Assert.Equal("«alarm.level.warning»", row[1]);
        Assert.Equal(AlarmPriority.Label(AlarmPriority.Of(alarm)), row[2]);
        Assert.Equal("冷却水阀关断", row[3]);
        Assert.Equal("打开备用泵并通知班长", row[4]);
        Assert.Equal("反应釜超温", row[5]);
        Assert.Equal("lblTemp", row[6]);
    }

    [Fact]
    public void SeverityColumn_ReusesTheAlarmReportMapping()
    {
        foreach (var severity in Enum.GetValues<AlarmSeverity>())
        {
            var alarm = new AlarmConfig { Name = "x", Severity = severity };
            var row = Assert.Single(AlarmPhilosophyReport.From(new[] { alarm }, Loc).Rows);
            Assert.Equal($"«{AlarmReport.SeverityKey(severity)}»", row[1]);
        }
    }

    [Fact]
    public void Export_ProducesACsvWithTheDutyColumns()
    {
        var alarm = new AlarmConfig
        {
            Name = "电机过载", Severity = AlarmSeverity.Critical,
            Cause = "负载过大", Response = "降载并检查", Consequence = "绕线烧毁",
        };

        var csv = ReportExporter.ToCsv(AlarmPhilosophyReport.From(new[] { alarm }, Loc));

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);                      // 表头 + 1 行
        Assert.Contains("«alarm.cause»", lines[0], StringComparison.Ordinal);
        Assert.Contains("«alarm.consequence»", lines[0], StringComparison.Ordinal);
        Assert.Contains("负载过大", lines[1], StringComparison.Ordinal);
        Assert.Contains("绕线烧毁", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void DutyFields_SurviveProjectJsonRoundTrip_AndOldProjectsStillLoad()
    {
        var project = new HmiProject();
        var group = new AlarmGroup { Name = "温度组" };
        group.Alarms.Add(new AlarmConfig
        {
            Id = "a1", Name = "温度高",
            Cause = "冷却失效", Response = "开备用泵", Consequence = "超温停车",
        });
        project.AlarmGroups.Add(group);

        var json = ProjectSerializer.Serialize(project);
        Assert.Contains("\"cause\":", json, StringComparison.Ordinal);          // camelCase ＋ 有值 ⇒ 落盘
        Assert.Contains("冷却失效", json, StringComparison.Ordinal);

        var back = ProjectSerializer.Deserialize(json);
        Assert.NotNull(back);
        var restored = back!.AlarmGroups.Single().Alarms.Single();
        Assert.Equal("冷却失效", restored.Cause);
        Assert.Equal("开备用泵", restored.Response);
        Assert.Equal("超温停车", restored.Consequence);

        // 老工程口径：JSON 里**根本没有**这三个键 ——
        // 序列化器是 `WhenWritingNull`（`JsonOptionsDefaults.cs:53`）⇒ 三个字段为 null 的报警
        // 序列化后就不带这些键；再反序列化必须得到 null（不是空串、更不许抛）。
        var legacyProject = new HmiProject();
        var legacyGroup = new AlarmGroup { Name = "老组" };
        legacyGroup.Alarms.Add(new AlarmConfig { Id = "old1", Name = "老报警" });
        legacyProject.AlarmGroups.Add(legacyGroup);

        var legacyJson = ProjectSerializer.Serialize(legacyProject);
        Assert.DoesNotContain("\"cause\"", legacyJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"response\"", legacyJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"consequence\"", legacyJson, StringComparison.Ordinal);

        var legacyBack = ProjectSerializer.Deserialize(legacyJson);
        var legacyAlarm = Assert.Single(legacyBack!.AlarmGroups).Alarms.Single();
        Assert.Null(legacyAlarm.Cause);
        Assert.Null(legacyAlarm.Response);
        Assert.Null(legacyAlarm.Consequence);
    }
}
