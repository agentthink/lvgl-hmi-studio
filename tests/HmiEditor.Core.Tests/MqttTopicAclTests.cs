using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **MQTT 主题级 ACL**（阶段 286；提案 §4「授权」那一行）—— 编辑器侧对偶 ＋ 跨语言同源守卫。
/// 三条硬规则：**A1 方向不能反** · **A2 未授权即拒** · **A3 不认识的主题即拒**。
/// </summary>
public class MqttTopicAclTests
{
    private readonly ITestOutputHelper _out;

    public MqttTopicAclTests(ITestOutputHelper o) => _out = o;

    private const string Prefix = "hmi/s1";

    private static string DeviceSource() =>
        File.ReadAllText(Path.Combine(SourceScanner.RepoRoot, "firmware/t113/tests/hmi_comm_test.c"));

    [Fact]
    public void Samples_MatchTheDeviceHostTestTable()
    {
        var block = Regex.Match(DeviceSource(), @"k_acl_samples\[\] = \{(?<body>.*?)\n\};", RegexOptions.Singleline);
        Assert.True(block.Success, "C 侧 k_acl_samples[] 没找到");

        var row = new Regex(
            @"\{\s*""(?<topic>[^""]*)""\s*,\s*(?<wr>[01])\s*,\s*(?<mask>0x[0-9A-Fa-f]+)\s*,\s*(?<expect>-?[01])\s*,\s*""(?<reason>[^""]*)""\s*\}",
            RegexOptions.Compiled);

        var samples = row.Matches(block.Groups["body"].Value).Select(m => (
            Topic: m.Groups["topic"].Value,
            IsWrite: m.Groups["wr"].Value == "1",
            Mask: (Permission)uint.Parse(m.Groups["mask"].Value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            Expect: int.Parse(m.Groups["expect"].Value, CultureInfo.InvariantCulture),
            Reason: m.Groups["reason"].Value)).ToList();

        Assert.True(samples.Count >= 20, $"只解析出 {samples.Count} 条 ACL 样例（要求 ≥20）");

        foreach (var s in samples)
        {
            var issue = MqttTopicAcl.Evaluate(s.Topic, Prefix, s.IsWrite, s.Mask);
            Assert.Equal(s.Expect == 0, issue is null);
            if (s.Expect != 0) Assert.Equal(s.Reason, issue);
        }

        JudgeReadings.Write(_out, "MQTT 主题 ACL · 跨语言同源样例（阶段 286）", new[]
        {
            $"设备侧 ACL 样例 {samples.Count} 条（逐条比对允许/拒绝与原因码）",
            "A1 远程写遥测主题 ⇒ wrong-direction · A2 权限不够 ⇒ forbidden · A3 前缀/形状不符 ⇒ unknown-topic",
        }.Concat(samples.Select(s => $"        · {(s.IsWrite ? "写" : "读")} {s.Topic} mask=0x{(uint)s.Mask:X3} ⇒ {(s.Expect == 0 ? "允许" : s.Reason)}")).ToList());
    }

    [Fact]
    public void PermissionBits_MatchTheGeneratedHeader()
    {
        // ACL 用到的权限位必须与生成头同源（否则"能确认 ≠ 能搁置"这条会静默失效）。
        // ⚠ 设备 ACL 模块**只能**吃生成头（不得手写权限位 —— SecurityConfigCodeGenTests 有守卫，
        //    本模块第一版写了 `#define HMI_PERM_* 0x…` 兜底，当场被它拦下）。
        var aclImpl = File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, "firmware/t113/app/comm/mqtt_acl_contract.c"));
        Assert.Contains("#include \"generated_security.h\"", aclImpl, StringComparison.Ordinal);
        // 只在**行首**找 `#define HMI_PERM_`（注释里提到它不算 —— 我第一版就是被自己的注释绊了一下：
        // 断言必须与守卫同一口径，否则"注释里写了什么"会变成判据的一部分）
        Assert.False(Regex.IsMatch(aclImpl, @"(?m)^\s*#define\s+HMI_PERM_"),
            "设备 ACL 模块不得手写权限位（唯一来源是 generated_security.h）");

        var header = File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, "firmware/t113/tests/fixtures/generated_security.h"));

        Assert.Contains($"#define HMI_PERM_VIEW_SCREEN        0x{(uint)Permission.ViewScreen:X3}u", header, StringComparison.Ordinal);
        Assert.Contains($"#define HMI_PERM_OPERATE_CONTROL    0x{(uint)Permission.OperateControl:X3}u", header, StringComparison.Ordinal);
        Assert.Contains($"#define HMI_PERM_ACKNOWLEDGE_ALARM  0x{(uint)Permission.AcknowledgeAlarm:X3}u", header, StringComparison.Ordinal);
        Assert.Contains($"#define HMI_PERM_VIEW_ALARM_HISTORY 0x{(uint)Permission.ViewAlarmHistory:X3}u", header, StringComparison.Ordinal);
        Assert.Contains($"#define HMI_PERM_SHELVE_ALARM       0x{(uint)Permission.ShelveAlarm:X3}u", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_RequiresExactPrefixAndShape()
    {
        Assert.Equal(MqttTopicKind.Value, MqttTopicAcl.Classify("hmi/s1/value/v1", Prefix));
        Assert.Equal(MqttTopicKind.Alarm, MqttTopicAcl.Classify("hmi/s1/alarm/a1", Prefix));
        Assert.Equal(MqttTopicKind.Status, MqttTopicAcl.Classify("hmi/s1/status", Prefix));
        Assert.Equal(MqttTopicKind.Cmd, MqttTopicAcl.Classify("hmi/s1/cmd/v1", Prefix));

        // "包含"不算匹配；空目标/空后缀不算；多余后缀不算
        Assert.Equal(MqttTopicKind.Unknown, MqttTopicAcl.Classify("hmi/s1values/v1", Prefix));
        Assert.Equal(MqttTopicKind.Unknown, MqttTopicAcl.Classify("hmi/s1status", Prefix));
        Assert.Equal(MqttTopicKind.Unknown, MqttTopicAcl.Classify("hmi/s1/statusx", Prefix));
        Assert.Equal(MqttTopicKind.Unknown, MqttTopicAcl.Classify("hmi/s1/value/", Prefix));
        Assert.Equal(MqttTopicKind.Unknown, MqttTopicAcl.Classify("hmi/s1/status/extra", Prefix));
        Assert.Equal(MqttTopicKind.Unknown, MqttTopicAcl.Classify("hmi/s1/cmd/", Prefix));
        Assert.Equal(MqttTopicKind.Unknown, MqttTopicAcl.Classify(null, Prefix));
        Assert.Equal(MqttTopicKind.Unknown, MqttTopicAcl.Classify("hmi/s1/status", null));
    }

    [Fact]
    public void RoleMasks_BehaveAsTheContractSays()
    {
        var operatorMask = UserRole.BuiltInOperator.Permissions;
        var engineerMask = UserRole.BuiltInEngineer.Permissions;

        // 控制命令：Operator（有 OperateControl）可写；无该位的角色不可
        Assert.Null(MqttTopicAcl.Evaluate("hmi/s1/cmd/v1", Prefix, isWrite: true, operatorMask));
        Assert.Equal(MqttAclIssue.Forbidden,
            MqttTopicAcl.Evaluate("hmi/s1/cmd/v1", Prefix, isWrite: true, Permission.ViewScreen));

        // 确认命令：要 AcknowledgeAlarm（Operator 有）
        Assert.Null(MqttTopicAcl.Evaluate("hmi/s1/cmd/ack/a1", Prefix, true, operatorMask));
        Assert.Equal(MqttAclIssue.Forbidden,
            MqttTopicAcl.Evaluate("hmi/s1/cmd/ack/a1", Prefix, true, Permission.OperateControl));

        // 搁置命令：要 ShelveAlarm —— **能确认 ≠ 能搁置**（预置角色里两者同权，但掩码上可分开）
        Assert.Null(MqttTopicAcl.Evaluate("hmi/s1/cmd/shelve/a1", Prefix, true, operatorMask));
        Assert.Equal(MqttAclIssue.Forbidden,
            MqttTopicAcl.Evaluate("hmi/s1/cmd/shelve/a1", Prefix, true, Permission.AcknowledgeAlarm));
        Assert.Null(MqttTopicAcl.Evaluate("hmi/s1/cmd/unshelve/a1", Prefix, true, engineerMask));

        // 读遥测：Operator 有 ViewScreen ⇒ 允许；读报警历史：Operator 有 ViewAlarmHistory ⇒ 允许
        Assert.Null(MqttTopicAcl.Evaluate("hmi/s1/value/v1", Prefix, false, operatorMask));
        Assert.Null(MqttTopicAcl.Evaluate("hmi/s1/alarm/a1", Prefix, false, operatorMask));
        Assert.Equal(MqttAclIssue.Forbidden,
            MqttTopicAcl.Evaluate("hmi/s1/alarm/a1", Prefix, false, Permission.ViewScreen));

        // 设备方向：远程不许写遥测（哪怕权限全开）
        Assert.Equal(MqttAclIssue.WrongDirection,
            MqttTopicAcl.Evaluate("hmi/s1/value/v1", Prefix, true, Permission.All));
        Assert.Equal(MqttAclIssue.WrongDirection,
            MqttTopicAcl.Evaluate("hmi/s1/alarm/a1", Prefix, true, Permission.All));
        Assert.Equal(MqttAclIssue.WrongDirection,
            MqttTopicAcl.Evaluate("hmi/s1/status", Prefix, true, Permission.All));
        Assert.Equal(MqttAclIssue.WrongDirection,
            MqttTopicAcl.Evaluate("hmi/s1/cmd/v1", Prefix, false, Permission.All));
    }
}
