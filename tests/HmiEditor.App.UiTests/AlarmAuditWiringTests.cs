using System;
using System.IO;
using System.Linq;
using HmiEditor.App.Services;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **搁置审计的接线**（阶段 278；契约 §7）—— 不构造 MainWindow，直接判"引擎发事件 ⇒ 审计落盘"。
///
/// <para>三条都要留痕：**成功** · **被拒（含未授权）** · **解除（含到期自动解除）**。
/// 去掉任何一条，事后都看不出"谁把报警静音了""报警为什么突然又开始响"。</para>
/// </summary>
[Collection("Language-global")]
public class AlarmAuditWiringTests
{
    private readonly ITestOutputHelper _out;

    public AlarmAuditWiringTests(ITestOutputHelper o) => _out = o;

    private static AlarmConfig Config() => new()
    {
        Id = "cfg1", Name = "温度高", Severity = AlarmSeverity.Critical, RequiresAck = true,
        Condition = new AlarmCondition { Type = AlarmConditionType.HighLimit, VariableId = "v1", Threshold = 100 },
    };

    private static (AlarmEngine Engine, AlarmRecord Record) Triggered()
    {
        var engine = new AlarmEngine();
        engine.RegisterAlarm(Config());
        engine.UpdateVariableValue("v1", 120);
        engine.EvaluateOnce();
        return (engine, engine.GetAllRecords().Single());
    }

    [Fact]
    public void ShelveDenyGrantAndExpiry_AllLandInTheAuditStore()
    {
        using var sandbox = new DataRootSandbox("alarm-audit");
        var auditPath = Path.Combine(sandbox.DataRoot, "audit.json");
        Directory.CreateDirectory(sandbox.DataRoot);

        var auth = new AuthenticationService { AuditLogFilePath = auditPath };
        var (engine, record) = Triggered();
        using (engine)
        using (AlarmAuditWiring.Attach(engine, auth))
        {
            // ① 未授权被拒
            engine.Shelve(record.Id, TimeSpan.FromMinutes(5), "alice", permitted: false);
            // ② 成功
            engine.Shelve(record.Id, TimeSpan.FromMinutes(5), "alice", "维护中");
            // ③ 到期自动解除
            engine.ExpireShelves(record.ShelvedUntil!.Value);

            // ⚠ 存储层的顺序是它自己的事（本阶段实测：`LoadAll` 给的是**最新在前**）——
            //    判据自己按时间排序，免得"断言顺序"悄悄变成"断言存储实现"
            var entries = AuditLogStore.LoadAll(auditPath).OrderBy(e => e.Timestamp).ToList();
            Assert.Equal(3, entries.Count);
            Assert.True(entries[0].Timestamp <= entries[1].Timestamp && entries[1].Timestamp <= entries[2].Timestamp);

            Assert.Equal(AuditAction.AlarmShelved, entries[0].Action);
            Assert.False(entries[0].Success);
            Assert.Contains("denied:forbidden", entries[0].Detail, StringComparison.Ordinal);

            Assert.Equal(AuditAction.AlarmShelved, entries[1].Action);
            Assert.True(entries[1].Success);
            Assert.Equal("alice", entries[1].Username);
            Assert.Contains("温度高", entries[1].Detail, StringComparison.Ordinal);
            Assert.Contains("维护中", entries[1].Detail, StringComparison.Ordinal);

            Assert.Equal(AuditAction.AlarmUnshelved, entries[2].Action);
            Assert.True(entries[2].Success);
            Assert.Contains("Expired", entries[2].Detail, StringComparison.Ordinal);

            JudgeReadings.Write(_out, "搁置审计落盘（阶段 278）", new[]
            {
                $"审计条目 {entries.Count} 条：{string.Join(" | ", entries.Select(e => $"{e.Action}/{(e.Success ? "ok" : "denied")}"))}",
                "① 未授权 → AlarmShelved/Success=false/denied:forbidden",
                "② 成功 → AlarmShelved/Success=true/含报警名与原因", 
                "③ 到期解除 → AlarmUnshelved/含 Expired",
            });
        }
    }

    [Fact]
    public void Detaching_StopsWriting()
    {
        using var sandbox = new DataRootSandbox("alarm-audit-detach");
        var auditPath = Path.Combine(sandbox.DataRoot, "audit.json");
        Directory.CreateDirectory(sandbox.DataRoot);

        var auth = new AuthenticationService { AuditLogFilePath = auditPath };
        var (engine, record) = Triggered();
        using (engine)
        {
            var sub = AlarmAuditWiring.Attach(engine, auth);
            engine.Shelve(record.Id, TimeSpan.FromMinutes(5), "alice");
            sub.Dispose();
            engine.Unshelve(record.Id, "alice");

            var entries = AuditLogStore.LoadAll(auditPath);
            Assert.Single(entries);                       // 解除那条没被写进去（已解绑）
            Assert.Equal(AuditAction.AlarmShelved, entries[0].Action);
        }
    }

    [Fact]
    public void Suppression_AlsoLandsInTheAuditStore()
    {
        using var sandbox = new DataRootSandbox("alarm-audit-suppress");
        var auditPath = Path.Combine(sandbox.DataRoot, "audit.json");
        Directory.CreateDirectory(sandbox.DataRoot);

        var auth = new AuthenticationService { AuditLogFilePath = auditPath };
        var (engine, _) = Triggered();
        using (engine)
        using (AlarmAuditWiring.Attach(engine, auth))
        {
            engine.Suppress("cfg1", "alice", "检修");
            engine.Unsuppress("cfg1", "alice");

            var entries = AuditLogStore.LoadAll(auditPath).OrderBy(e => e.Timestamp).ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal(AuditAction.AlarmSuppressed, entries[0].Action);
            Assert.True(entries[0].Success);
            Assert.Contains("检修", entries[0].Detail, StringComparison.Ordinal);
            Assert.Equal(AuditAction.AlarmUnsuppressed, entries[1].Action);
            Assert.True(entries[1].Success);
        }
    }

    [Fact]
    public void ActionDisplayName_HasLocalizedNamesForBothNewActions()
    {
        // 落到 `_ => action.ToString()` 会显示英文标识符（`UserProfileChanged` 当初就是这个毛病）
        foreach (var action in new[]
                 {
                     AuditAction.AlarmShelved, AuditAction.AlarmUnshelved,
                     AuditAction.AlarmSuppressed, AuditAction.AlarmUnsuppressed,
                 })
        {
            var name = HmiEditor.App.Views.UserManagerView.ActionDisplayName(action);
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.NotEqual(action.ToString(), name);
        }
    }
}
