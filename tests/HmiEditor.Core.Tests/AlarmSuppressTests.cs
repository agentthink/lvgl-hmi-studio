using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报警抑制**（阶段 280；契约 §2 S1 的对照面）—— 抑制＝**不产生记录**（不是产生后隐藏）。
///
/// <para>与搁置的分工：搁置是"已经报了的先别吵"（有状态、有到期）；抑制是"压根不要报"（无状态、无到期）。
/// 所以本判据的核心一句是：**抑制期间条件满足 ⇒ 记录数不增**；解除后**下一轮立刻产生**。</para>
/// </summary>
public class AlarmSuppressTests
{
    private readonly ITestOutputHelper _out;

    public AlarmSuppressTests(ITestOutputHelper o) => _out = o;

    private static AlarmConfig Config() => new()
    {
        Id = "cfg1", Name = "温度高", Severity = AlarmSeverity.Critical, RequiresAck = true,
        Condition = new AlarmCondition { Type = AlarmConditionType.HighLimit, VariableId = "v1", Threshold = 100 },
    };

    private static AlarmEngine Engine()
    {
        var engine = new AlarmEngine();
        engine.RegisterAlarm(Config());
        engine.UpdateVariableValue("v1", 120);      // 条件满足
        return engine;
    }

    [Fact]
    public void SuppressedAlarm_ProducesNoRecord_AndProducesOneAfterUnsuppress()
    {
        using var engine = Engine();

        engine.Suppress("cfg1", "alice", "设备检修中");
        Assert.True(engine.IsSuppressed("cfg1"));

        engine.EvaluateOnce();
        Assert.Empty(engine.GetAllRecords());          // ⚠ 核心：**不产生记录**

        engine.Unsuppress("cfg1", "alice");
        Assert.False(engine.IsSuppressed("cfg1"));

        engine.EvaluateOnce();
        var record = Assert.Single(engine.GetAllRecords());
        Assert.Equal(AlarmState.Active, record.State);

        JudgeReadings.Write(_out, "报警抑制（阶段 280）", new[]
        {
            "抑制期间：IsSuppressed=true、EvaluateOnce 后记录数 0（不产生记录，也不是产生后隐藏）",
            "解除后：下一轮求值立刻产生 1 条 Active 记录",
        });
    }

    [Fact]
    public void SuppressedAlarm_DoesNotAdvanceAnExistingRecord()
    {
        using var engine = Engine();
        engine.EvaluateOnce();                          // 先产生一条 Active
        var record = Assert.Single(engine.GetAllRecords());

        engine.Suppress("cfg1", "alice");
        engine.UpdateVariableValue("v1", 10);           // 条件解除
        engine.EvaluateOnce();

        // 抑制期间**不推进**既有记录（否则历史里会出现"抑制期间恢复"这种没发生过的迁移）
        Assert.Equal(AlarmState.Active, record.State);
        Assert.Null(record.ReturnedAt);
    }

    [Fact]
    public void SuppressAndUnsuppress_AuditAndCodes()
    {
        using var engine = Engine();
        var audits = new List<AlarmSuppressAudit>();
        engine.SuppressionAudited += (_, e) => audits.Add(e);

        engine.Suppress("cfg1", "alice", "检修");                       // 成功
        engine.Suppress("cfg1", "alice");                               // 已抑制 ⇒ 拒
        engine.Suppress("cfg1", "alice", permitted: false);             // 未授权 ⇒ 拒（且要留痕）
        engine.Unsuppress("cfg1", "bob");                               // 成功
        engine.Unsuppress("cfg1", "bob");                               // 没在抑制 ⇒ 拒

        Assert.Equal(5, audits.Count);
        Assert.True(audits[0].Granted && audits[0].Suppressed);
        Assert.Equal("检修", audits[0].Reason);
        Assert.Equal("alice", audits[0].By);

        Assert.False(audits[1].Granted);
        Assert.Equal(AlarmShelveIssue.AlreadyShelved, audits[1].IssueCode);

        Assert.False(audits[2].Granted);
        Assert.Equal(AlarmShelveIssue.Forbidden, audits[2].IssueCode);   // 未授权也留痕

        Assert.True(audits[3].Granted && !audits[3].Suppressed);
        Assert.Equal("bob", audits[3].By);

        Assert.False(audits[4].Granted);
        Assert.Equal(AlarmShelveIssue.NotActive, audits[4].IssueCode);

        Assert.Equal("cfg1", audits[0].AlarmConfigId);
        Assert.Equal("温度高", audits[0].AlarmName);
    }

    [Fact]
    public void GetSuppressedAlarmIds_IsDeterministic()
    {
        var engine = new AlarmEngine();
        engine.RegisterAlarm(new AlarmConfig { Id = "b", Name = "B" });
        engine.RegisterAlarm(new AlarmConfig { Id = "a", Name = "A" });
        using (engine)
        {
            engine.Suppress("b");
            engine.Suppress("a");
            Assert.Equal(new[] { "a", "b" }, engine.GetSuppressedAlarmIds());
        }
    }
}
