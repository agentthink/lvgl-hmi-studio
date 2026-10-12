using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models.Alarm;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **搁置的生成代码**（阶段 276；契约 `docs/2026-10-01-alarm-shelving-contract.md` §10 第 ②）：
/// state 4 的迁移 · **搁置中不允许确认** · 权限门 · 时长上下界 · 到期恢复。
///
/// <para>判据形状沿用本仓对生成代码的老规矩：**逐字节断言关键行**（不是"包含某个词"就算过），
/// 并且**顺序也判**（"拒绝确认"必须在 `state == 1` 那一支之前 —— 放到后面就永远不会命中）。</para>
/// </summary>
public class AlarmShelveCodeGenTests
{
    private readonly ITestOutputHelper _out;

    public AlarmShelveCodeGenTests(ITestOutputHelper o) => _out = o;

    private static string Generate()
    {
        var group = new AlarmGroup { Id = "g1", Name = "温度组" };
        group.Alarms.Add(new AlarmConfig
        {
            Id = "al1",
            Name = "温度高",
            Severity = AlarmSeverity.Critical,
            RequiresAck = true,
            Condition = new AlarmCondition
            {
                Type = AlarmConditionType.HighLimit, Threshold = 80, Deadband = 2, DelayMs = 0,
            },
        });

        return new CodeGenerator().GenerateAlarmCode(new List<AlarmGroup> { group });
    }

    [Fact]
    public void ShelveBounds_ComeFromTheEditorPolicy_NotHandWritten()
    {
        var code = Generate();

        // **单源**：生成器发射的两个宏必须等于编辑器策略的毫秒值（改一边不改另一边 ⇒ 这里红）
        var min = ((long)AlarmShelvePolicy.MinDuration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        var max = ((long)AlarmShelvePolicy.MaxDuration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

        CSource.AssertText(code, $"#define HMI_ALARM_SHELVE_MIN_MS {min}ULL");
        CSource.AssertText(code, $"#define HMI_ALARM_SHELVE_MAX_MS {max}ULL");
        Assert.Equal("60000", min);
        Assert.Equal("28800000", max);
    }

    [Fact]
    public void RecordStruct_HasTheTwoShelveFields_AndStateCommentMentionsShelved()
    {
        var code = Generate();

        CSource.AssertText(code, "uint32_t shelved_until;");
        CSource.AssertText(code, "uint8_t  shelved_under;");
        CSource.AssertText(code, "4=Shelved");
    }

    [Fact]
    public void Acknowledge_RefusesWhileShelved_AndTheRefusalComesFirst()
    {
        var code = Generate();

        const string refusal = "ALARM_TRACE(\"[config id=%u] acknowledge denied: shelved\", (unsigned)config_id);";
        const string ackBranch = "if (r->state == 1) {   /* 仅 Active 可被确认 */";

        CSource.AssertText(code, refusal);
        CSource.AssertText(code, ackBranch);

        // ⚠ **顺序也判**：拒绝那一支必须在确认支之前，否则永远命中不到（搁置中的 state==1 判据永不成立）
        Assert.True(code.IndexOf(refusal, StringComparison.Ordinal) < code.IndexOf(ackBranch, StringComparison.Ordinal),
            "「搁置中拒绝确认」必须排在 `state == 1` 那一支**之前**");
    }

    [Fact]
    public void ShelveEntry_GatesPermissionAndBounds_AndRejectsTheDeclaredStates()
    {
        var code = Generate();

        CSource.AssertText(code, "uint8_t alarm_shelve(uint16_t config_id, uint32_t duration_ms, const char* by) {");
        CSource.AssertText(code, "if (!hmi_has_permission(HMI_PERM_SHELVE_ALARM)) {");
        CSource.AssertText(code, "shelve denied: duration out of range");
        CSource.AssertText(code, "if (r->state == 0 || r->state == 3 || r->state == 4) {");
        CSource.AssertText(code, "r->shelved_under = r->state;");
        CSource.AssertText(code, "r->shelved_until = lv_tick_get() + duration_ms;");
        CSource.AssertText(code, "r->state = 4;");
    }

    [Fact]
    public void UnshelveEntry_RestoresTheStateBeforeShelving()
    {
        var code = Generate();

        CSource.AssertText(code, "uint8_t alarm_unshelve(uint16_t config_id, const char* by) {");
        CSource.AssertText(code, "unshelve denied: not shelved");
        CSource.AssertText(code, "r->state = r->shelved_under;");
        CSource.AssertText(code, "r->shelved_until = 0;");
    }

    [Fact]
    public void CheckFunction_SkipsEvaluationWhileShelved_AndResumesOnExpiry()
    {
        var code = Generate();

        CSource.AssertText(code, "if (r->state == 4) {");
        CSource.AssertText(code, "if ((int32_t)(lv_tick_get() - r->shelved_until) >= 0) {");
        CSource.AssertText(code, "shelve expired -> resumed state %d");
        CSource.AssertText(code, "return;   /* 仍在搁置期 */");

        // 到期恢复这一块必须在**条件评估之前**（否则搁置期间的评估会先把状态改掉）
        Assert.True(code.IndexOf("/* 仍在搁置期 */", StringComparison.Ordinal)
                    < code.IndexOf("uint8_t triggered = 0;", StringComparison.Ordinal),
            "搁置/到期那段必须排在 `uint8_t triggered = 0;` 之前");

        JudgeReadings.Write(_out, "搁置生成代码 · 关键行（阶段 276）", new[]
        {
            "界限宏与 AlarmShelvePolicy 同源：HMI_ALARM_SHELVE_MIN_MS=60000ULL / MAX=28800000ULL",
            "确认：搁置中拒绝（且排在 state==1 之前）· 搁置：权限门 + 时长界 + 拒绝 state 0/3/4 · 解除：恢复 shelved_under",
            "评估：state==4 时先判到期（(int32_t)(now-until)>=0，防 tick 回绕），未到期直接 return（不评估、不改对外状态）",
        });
    }

    [Fact]
    public void DeviceAudit_IsWrittenOnGrantDenyAndUnshelve()
    {
        var code = Generate();

        // **四条都要留痕**（契约 §7）：成功 · 被拒（权限/时长/状态）· 手动解除 —— 且走设备审计入口
        // `hmi_audit`（唯一落点：串口 + append-only 文件），**不是** `ALARM_TRACE`（那个能整段编掉）。
        CSource.AssertText(code, "extern void hmi_audit(const char* event, const char* user, const char* role, const char* detail);");
        CSource.AssertText(code, "hmi_audit(\"alarm-shelve\", by, \"\", _audit_detail);");
        CSource.AssertText(code, "hmi_audit(\"alarm-shelve-denied\", by, \"\", \"no permission\");");
        CSource.AssertText(code, "hmi_audit(\"alarm-shelve-denied\", by, \"\", \"duration out of range\");");
        CSource.AssertText(code, "hmi_audit(\"alarm-shelve-denied\", by, \"\", _audit_detail);");
        CSource.AssertText(code, "hmi_audit(\"alarm-unshelve\", by, \"\", _audit_detail);");
        CSource.AssertText(code, "hmi_audit(\"alarm-unshelve-denied\", by, \"\", \"not shelved\");");

        // 到期自动解除也要留痕（否则事后看不出"报警为什么突然又开始响"）——这条是**按报警**生成的
        CSource.AssertText(code, "hmi_audit(\"alarm-unshelve-expired\", NULL, \"\", \"al1 expired\");");

        // ⚠ 审计**不是** ALARM_TRACE：后者默认 `ALARM_TRACE_ENABLE 0`，编掉就一条不留
        var auditLines = code.Split('\n').Where(l => l.Contains("hmi_audit(", StringComparison.Ordinal)).ToList();
        Assert.True(auditLines.Count >= 7, $"审计写入点只有 {auditLines.Count} 处（要求 ≥7）");
        Assert.All(auditLines, l => Assert.DoesNotContain("ALARM_TRACE", l, StringComparison.Ordinal));

        JudgeReadings.Write(_out, "设备侧搁置审计（阶段 279）", new[]
        {
            $"生成代码里的 hmi_audit 调用点：{auditLines.Count} 处",
            "成功 · 被拒(无权限/时长越界/状态) · 手动解除 · 到期解除 都有 —— 且都不走可编掉的 ALARM_TRACE",
        });
    }

    [Fact]
    public void EveryEmittedShelveLineIsUnique_NoAccidentalDuplication()
    {
        // 防"生成两遍"这类复制粘贴事故：关键行各只出现一次
        var code = Generate();
        foreach (var needle in new[]
                 {
                     "uint8_t alarm_shelve(uint16_t config_id, uint32_t duration_ms, const char* by) {",
                     "uint8_t alarm_unshelve(uint16_t config_id, const char* by) {",
                     "if (!hmi_has_permission(HMI_PERM_SHELVE_ALARM)) {",
                 })
        {
            var count = code.Split(needle).Length - 1;
            Assert.Equal(1, count);
        }
    }
}
