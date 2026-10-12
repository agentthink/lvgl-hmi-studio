using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报警搁置**（阶段 274）—— 契约 §9 的验收规格第 1/2/3/4 条：
/// 迁移矩阵逐格 · 到期自动解除 · 权限 · 时长上下界。
///
/// <para>契约文件：<c>docs/2026-10-01-alarm-shelving-contract.md</c>。</para>
/// </summary>
public class AlarmShelveTests
{
    private readonly ITestOutputHelper _out;

    public AlarmShelveTests(ITestOutputHelper o) => _out = o;

    private static AlarmConfig Config(string id = "cfg1", string varId = "v1", bool requiresAck = true) => new()
    {
        Id = id,
        Name = "温度高",
        Severity = AlarmSeverity.Critical,
        RequiresAck = requiresAck,
        Condition = new AlarmCondition { Type = AlarmConditionType.HighLimit, VariableId = varId, Threshold = 100 },
    };

    /// <summary>
    /// 确定性触发：**不用 <c>Start()</c>**（它 `Change(0, …)` 会立刻在 ThreadPool 上跑一轮，
    /// 与手工评估交错 ⇒ 判据变飘），只走手工入口 <see cref="AlarmEngine.EvaluateOnce"/>。
    /// </summary>
    private static (AlarmEngine Engine, AlarmRecord Record) TriggeredViaEvaluation()
    {
        var engine = new AlarmEngine();
        engine.RegisterAlarm(Config());
        engine.UpdateVariableValue("v1", 120);
        engine.EvaluateOnce();
        return (engine, engine.GetAllRecords().Single());
    }

    [Theory]
    // Inactive ⇒ 不能搁置
    [InlineData(AlarmState.Inactive, AlarmShelveIssue.NotActive)]
    // Returned ⇒ 不能搁置（它本来就不播报）
    [InlineData(AlarmState.Returned, AlarmShelveIssue.Returned)]
    // Shelved ⇒ 不能重复搁置
    [InlineData(AlarmState.Shelved, AlarmShelveIssue.AlreadyShelved)]
    public void CanShelve_RejectsTheDeclaredStates(AlarmState state, string expectedCode)
    {
        Assert.False(AlarmShelvePolicy.CanShelve(state, TimeSpan.FromMinutes(5), out var code));
        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public void CanShelve_AllowsActiveAndAcknowledged_ButOnlyInsideTheDurationBounds()
    {
        foreach (var state in new[] { AlarmState.Active, AlarmState.Acknowledged })
        {
            Assert.True(AlarmShelvePolicy.CanShelve(state, AlarmShelvePolicy.MinDuration, out _));
            Assert.True(AlarmShelvePolicy.CanShelve(state, AlarmShelvePolicy.MaxDuration, out _));
            Assert.True(AlarmShelvePolicy.CanShelve(state, TimeSpan.FromHours(1), out _));

            // 边界外一秒都不行
            Assert.False(AlarmShelvePolicy.CanShelve(state, AlarmShelvePolicy.MinDuration - TimeSpan.FromSeconds(1), out var low));
            Assert.Equal(AlarmShelveIssue.DurationOutOfRange, low);
            Assert.False(AlarmShelvePolicy.CanShelve(state, AlarmShelvePolicy.MaxDuration + TimeSpan.FromSeconds(1), out var high));
            Assert.Equal(AlarmShelveIssue.DurationOutOfRange, high);
            Assert.False(AlarmShelvePolicy.CanShelve(state, TimeSpan.Zero, out _));
        }

        Assert.Equal(TimeSpan.FromMinutes(1), AlarmShelvePolicy.MinDuration);
        Assert.Equal(TimeSpan.FromHours(8), AlarmShelvePolicy.MaxDuration);
    }

    [Fact]
    public void CanAcknowledge_RejectsShelved_ThatIsTheWholePoint()
    {
        // 契约最要紧的一条：搁置中不允许确认，否则操作员以为"我已处理"而报警被静音。
        Assert.False(AlarmShelvePolicy.CanAcknowledge(AlarmState.Shelved, out var code));
        Assert.Equal(AlarmShelveIssue.Shelved, code);

        Assert.True(AlarmShelvePolicy.CanAcknowledge(AlarmState.Active, out _));
        Assert.False(AlarmShelvePolicy.CanAcknowledge(AlarmState.Acknowledged, out var acked));
        Assert.Equal(AlarmShelveIssue.AlreadyAcked, acked);
        Assert.False(AlarmShelvePolicy.CanAcknowledge(AlarmState.Returned, out _));
        Assert.False(AlarmShelvePolicy.CanAcknowledge(AlarmState.Inactive, out _));
    }

    [Fact]
    public void EveryIssueCode_HasItsOwnLiteralKey()
    {
        var codes = new[]
        {
            AlarmShelveIssue.NotActive, AlarmShelveIssue.Returned, AlarmShelveIssue.AlreadyAcked,
            AlarmShelveIssue.Shelved, AlarmShelveIssue.AlreadyShelved, AlarmShelveIssue.DurationOutOfRange,
            AlarmShelveIssue.Forbidden, AlarmShelveIssue.NotFound,
        };

        var keys = codes.Select(AlarmShelveIssue.Key).ToArray();
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, k => Assert.StartsWith("alarm.shelve.issue.", k, StringComparison.Ordinal));

        // 未登记的码**回落**到 forbidden（拒绝优先的保守口径）—— 这条口径本身也要判
        Assert.Equal("alarm.shelve.issue.forbidden", AlarmShelveIssue.Key(AlarmShelveIssue.Forbidden));
        Assert.Equal("alarm.shelve.issue.not_found", AlarmShelveIssue.Key(AlarmShelveIssue.NotFound));
        Assert.Equal("alarm.shelve.issue.forbidden", AlarmShelveIssue.Key("some-unregistered-code"));
    }

    [Fact]
    public void Shelve_SetsStateAndAuditFields_AndRaisesTheEvent()
    {
        var (engine, record) = TriggeredViaEvaluation();
        using (engine)
        {
            AlarmRecord? shelved = null;
            engine.AlarmShelved += (_, r) => shelved = r;

            var result = engine.Shelve(record.Id, TimeSpan.FromMinutes(30), "alice", "维护中");

            Assert.True(result.Ok);
            Assert.Equal(AlarmState.Shelved, record.State);
            Assert.Equal("alice", record.ShelvedBy);
            Assert.NotNull(record.ShelvedAt);
            Assert.NotNull(record.ShelvedUntil);
            Assert.Equal("维护中", record.ShelveReason);
            Assert.Same(record, shelved);

            // 搁置中：不在播报面，但在"搁置面"与全量里
            Assert.Empty(engine.GetActiveAlarms());
            Assert.Same(record, engine.GetShelvedAlarms().Single());
            Assert.Same(record, engine.GetAllRecords().Single());
        }
    }

    [Fact]
    public void AcknowledgeWhileShelved_IsRejectedWithTheCode_AndDoesNotChangeState()
    {
        var (engine, record) = TriggeredViaEvaluation();
        using (engine)
        {
            engine.Shelve(record.Id, TimeSpan.FromMinutes(30), "alice");

            var result = engine.TryAcknowledgeAlarm(record.Id, "bob");

            Assert.False(result.Ok);
            Assert.Equal(AlarmShelveIssue.Shelved, result.IssueCode);
            Assert.Equal(AlarmState.Shelved, record.State);
            Assert.Null(record.AcknowledgedAt);
            Assert.False(engine.AcknowledgeAlarm(record.Id, "bob"));   // 布尔版同样拒
        }
    }

    [Fact]
    public void Unshelve_RestoresActive_WhenTheConditionIsStillMet()
    {
        var (engine, record) = TriggeredViaEvaluation();
        using (engine)
        {
            engine.Shelve(record.Id, TimeSpan.FromMinutes(30), "alice");
            var unshelvedReason = "";
            engine.AlarmUnshelved += (_, r) => unshelvedReason = r.UnshelveReason ?? "";

            var result = engine.Unshelve(record.Id, "alice");

            Assert.True(result.Ok);
            Assert.Equal(AlarmState.Active, record.State);      // 条件仍满足（v1=120 > 100）
            Assert.Equal("Manual", record.UnshelveReason);
            Assert.Equal("alice", record.UnshelvedBy);
            Assert.Equal("Manual", unshelvedReason);
            Assert.Single(engine.GetActiveAlarms());
        }
    }

    [Fact]
    public void Unshelve_ReturnsReturned_WhenTheConditionClearedWhileShelved()
    {
        var (engine, record) = TriggeredViaEvaluation();
        using (engine)
        {
            engine.Shelve(record.Id, TimeSpan.FromMinutes(30), "alice");
            engine.UpdateVariableValue("v1", 10);               // 搁置期间条件解除

            engine.Unshelve(record.Id, "alice");

            Assert.Equal(AlarmState.Returned, record.State);
            Assert.NotNull(record.ReturnedAt);
        }
    }

    [Fact]
    public void ExpireShelves_AutoReleases_AndWritesTheExpiredReason()
    {
        var (engine, record) = TriggeredViaEvaluation();
        using (engine)
        {
            engine.Shelve(record.Id, TimeSpan.FromMinutes(5), "alice");
            var until = record.ShelvedUntil!.Value;

            // 还没到点 ⇒ 不动
            Assert.Empty(engine.ExpireShelves(until - TimeSpan.FromSeconds(1)));
            Assert.Equal(AlarmState.Shelved, record.State);

            // 到点（闭区间边界也算到期）⇒ 自动解除 ＋ 审计写 Expired
            var expired = engine.ExpireShelves(until);
            Assert.Same(record, Assert.Single(expired));
            Assert.Equal("Expired", record.UnshelveReason);
            Assert.Null(record.UnshelvedBy);
            Assert.NotNull(record.UnshelvedAt);
            Assert.NotEqual(AlarmState.Shelved, record.State);

            JudgeReadings.Write(_out, "报警搁置 · 到期自动解除（阶段 274）", new[]
            {
                "搁置 5 min；在 until-1s 扫描 ⇒ 不动；在 until（闭区间）扫描 ⇒ 解除且 UnshelveReason=Expired",
                $"解除后状态 {record.State}（条件仍满足时回到 Active）",
            });
        }
    }

    [Fact]
    public void Shelve_WithoutPermission_IsRejectedAndLogged()
    {
        var (engine, record) = TriggeredViaEvaluation();
        using (engine)
        {
            var logs = new System.Collections.Generic.List<string>();
            engine.Log = logs.Add;

            var result = engine.Shelve(record.Id, TimeSpan.FromMinutes(5), "alice", permitted: false);

            Assert.False(result.Ok);
            Assert.Equal(AlarmShelveIssue.Forbidden, result.IssueCode);
            Assert.Equal(AlarmState.Active, record.State);
            Assert.Contains(logs, l => l.Contains("forbidden", StringComparison.Ordinal));   // 未授权**也留痕**
        }
    }

    [Fact]
    public void Shelve_Boundaries_AreRejectedByTheEngineToo()
    {
        var (engine, record) = TriggeredViaEvaluation();
        using (engine)
        {
            var tooShort = engine.Shelve(record.Id, TimeSpan.FromSeconds(30));
            Assert.False(tooShort.Ok);
            Assert.Equal(AlarmShelveIssue.DurationOutOfRange, tooShort.IssueCode);

            var tooLong = engine.Shelve(record.Id, TimeSpan.FromHours(9));
            Assert.False(tooLong.Ok);
            Assert.Equal(AlarmShelveIssue.DurationOutOfRange, tooLong.IssueCode);

            Assert.True(engine.Shelve(record.Id, AlarmShelvePolicy.MaxDuration).Ok);
            // 已搁置再搁置 ⇒ 拒 already-shelved
            var again = engine.Shelve(record.Id, TimeSpan.FromMinutes(5));
            Assert.Equal(AlarmShelveIssue.AlreadyShelved, again.IssueCode);
        }
    }

    [Fact]
    public void ShelveAudit_IsRaisedForGrantedDeniedAndExpired()
    {
        var (engine, record) = TriggeredViaEvaluation();
        using (engine)
        {
            var audits = new List<AlarmShelveAudit>();
            engine.ShelveAudited += (_, e) => audits.Add(e);

            // ① 被拒（无权限）也要留痕
            engine.Shelve(record.Id, TimeSpan.FromMinutes(5), "alice", permitted: false);
            // ② 被拒（时长越界）
            engine.Shelve(record.Id, TimeSpan.FromSeconds(30), "alice");
            // ③ 成功
            engine.Shelve(record.Id, TimeSpan.FromMinutes(5), "alice", "维护中");
            // ④ 到期自动解除
            engine.ExpireShelves(record.ShelvedUntil!.Value);

            Assert.Equal(4, audits.Count);

            Assert.True(audits[0].Shelved);
            Assert.False(audits[0].Granted);
            Assert.Equal(AlarmShelveIssue.Forbidden, audits[0].IssueCode);
            Assert.Equal("alice", audits[0].By);

            Assert.Equal(AlarmShelveIssue.DurationOutOfRange, audits[1].IssueCode);

            Assert.True(audits[2].Shelved && audits[2].Granted);
            Assert.Equal("维护中", audits[2].Reason);
            Assert.NotNull(audits[2].Until);
            Assert.Equal(record.AlarmConfigId, audits[2].AlarmConfigId);
            Assert.Equal("温度高", audits[2].AlarmName);

            Assert.False(audits[3].Shelved);            // 解除事件
            Assert.True(audits[3].Granted);
            Assert.Equal("Expired", audits[3].Reason);  // ⚠ 到期自动解除也留痕
            Assert.Null(audits[3].By);

            JudgeReadings.Write(_out, "搁置审计事件（阶段 278）", new[]
            {
                "四次调用 ⇒ 四条事件：被拒(无权限) · 被拒(时长越界) · 成功 · 到期解除",
                "到期那条 Reason=Expired 且 By=null —— 事后能看出「报警为什么突然又开始响」",
            });
        }
    }

    [Fact]
    public void UnknownRecord_GivesNotFound()
    {
        var (engine, _) = TriggeredViaEvaluation();
        using (engine)
        {
            Assert.Equal(AlarmShelveIssue.NotFound, engine.Shelve("nope", TimeSpan.FromMinutes(5)).IssueCode);
            Assert.Equal(AlarmShelveIssue.NotFound, engine.Unshelve("nope").IssueCode);
            Assert.Equal(AlarmShelveIssue.NotFound, engine.TryAcknowledgeAlarm("nope").IssueCode);
        }
    }
}
