using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.Alarm;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报警优先级**（ISA-18.2 切片㊁；登记 `§2-#11`）：
/// `优先级 = f(严重度, 允许响应时间)` 纯函数的表驱动判据 ＋ **"优先级 ≠ 严重度"**这条核心主张。
///
/// <para>为什么必须有这张表：优先级若落成**可自由填的字段**，同一条报警在不同人手里会得到不同优先级
/// —— 这是 ISA-18.2 落地最常见的失败方式。本类把它锁成派生量。</para>
/// </summary>
public class AlarmPriorityTests
{
    private readonly ITestOutputHelper _out;

    public AlarmPriorityTests(ITestOutputHelper o) => _out = o;

    [Theory]
    // Emergency：≤60 s ⇒ P1，否则 P2
    [InlineData(AlarmSeverity.Emergency, 1_000, 1)]
    [InlineData(AlarmSeverity.Emergency, 60_000, 1)]
    [InlineData(AlarmSeverity.Emergency, 60_001, 2)]
    // Critical：≤5 min ⇒ P2，否则 P3
    [InlineData(AlarmSeverity.Critical, 60_000, 2)]
    [InlineData(AlarmSeverity.Critical, 300_000, 2)]
    [InlineData(AlarmSeverity.Critical, 300_001, 3)]
    // Warning：≤30 min ⇒ P3，否则 P4
    [InlineData(AlarmSeverity.Warning, 60_000, 3)]
    [InlineData(AlarmSeverity.Warning, 1_800_000, 3)]
    [InlineData(AlarmSeverity.Warning, 1_800_001, 4)]
    // Info：一律 P4
    [InlineData(AlarmSeverity.Info, 1, 4)]
    [InlineData(AlarmSeverity.Info, 86_400_000, 4)]
    public void From_MatchesTheDeclaredTable(AlarmSeverity severity, int allowedMs, int expected)
    {
        Assert.Equal(expected, AlarmPriority.From(severity, allowedMs));
        Assert.Equal(AlarmPriority.Label(expected), AlarmPriority.Label(AlarmPriority.From(severity, allowedMs)));
    }

    [Fact]
    public void PriorityIsNotSeverity_SameSeverityDifferentResponseTime_Differs()
    {
        // 这条就是 ISA-18.2 的核心主张：同一个"高"级别，允许响应时间不同 ⇒ 优先级不同。
        var urgent = new AlarmConfig { Severity = AlarmSeverity.Warning, AllowedResponseMs = 60_000 };
        var relaxed = new AlarmConfig { Severity = AlarmSeverity.Warning, AllowedResponseMs = 3_600_000 };

        Assert.Equal(AlarmSeverity.Warning, urgent.Severity);
        Assert.Equal(AlarmSeverity.Warning, relaxed.Severity);
        Assert.NotEqual(AlarmPriority.Of(urgent), AlarmPriority.Of(relaxed));
        Assert.True(AlarmPriority.Of(urgent) < AlarmPriority.Of(relaxed),   // 数值小＝更紧急
            "允许响应时间更短的报警必须拿到更高的优先级");

        JudgeReadings.Write(_out, "报警优先级 · 优先级 ≠ 严重度（§2-#11 切片㊁ · 阶段 267）", new[]
        {
            "同一严重度 Warning：允许 60 s ⇒ " + AlarmPriority.Label(AlarmPriority.Of(urgent))
                + "；允许 3600 s ⇒ " + AlarmPriority.Label(AlarmPriority.Of(relaxed)),
            "映射（1 = 最高）：Emergency ≤60s⇒P1 / Critical ≤5min⇒P2 / Warning ≤30min⇒P3 / Info⇒P4",
        });
    }

    [Fact]
    public void MissingAllowedResponseTime_FallsBackToTheSeverityDefault()
    {
        foreach (var severity in Enum.GetValues<AlarmSeverity>())
        {
            var alarm = new AlarmConfig { Severity = severity, AllowedResponseMs = null };
            Assert.Equal(AlarmPriority.From(severity, null), AlarmPriority.Of(alarm));
            Assert.Equal(AlarmPriority.From(severity, AlarmPriority.DefaultAllowedResponseMs(severity)),
                AlarmPriority.Of(alarm));
        }
    }

    [Fact]
    public void NonPositiveAllowedResponseTime_IsTreatedAsImmediate_NotAsRelaxed()
    {
        // 工程数据可能被手改坏（0 / 负数）—— 按"立即响应"处理，绝不允许它悄悄降级。
        Assert.Equal(1, AlarmPriority.From(AlarmSeverity.Emergency, 0));
        Assert.Equal(1, AlarmPriority.From(AlarmSeverity.Emergency, -5));
        Assert.Equal(2, AlarmPriority.From(AlarmSeverity.Critical, 0));
    }

    [Fact]
    public void StricterSeverity_NeverGetsALowerUrgency()
    {
        // 同一个允许响应时间下，严重度越严 ⇒ 优先级数值越小（＝更紧急），不得反过来。
        var severities = new[] { AlarmSeverity.Info, AlarmSeverity.Warning, AlarmSeverity.Critical, AlarmSeverity.Emergency };
        foreach (var allowed in new[] { 1, 30_000, 300_000, 1_800_000, 86_400_000 })
        {
            var numbers = severities.Select(s => AlarmPriority.From(s, allowed)).ToArray();
            for (var i = 1; i < numbers.Length; i++)
                Assert.True(numbers[i] <= numbers[i - 1],
                    $"响应时间 {allowed}：{severities[i - 1]} ⇒ P{numbers[i - 1]}，"
                    + $"但更严的 {severities[i]} 却得到 P{numbers[i]}");
        }
    }

    [Fact]
    public void Label_IsLanguageNeutral_AndRejectsOutOfRange()
    {
        Assert.Equal("P1", AlarmPriority.Label(1));
        Assert.Equal("P4", AlarmPriority.Label(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => AlarmPriority.Label(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AlarmPriority.Label(5));
    }
}
