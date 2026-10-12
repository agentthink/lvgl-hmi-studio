using System;
using HmiEditor.Core.Models.User;

namespace HmiEditor.Core.Services;

/// <summary>主题分类（与设备侧 <c>hmi_mqtt_topic_kind_t</c> 同号）。</summary>
public enum MqttTopicKind
{
    Unknown = 0,
    Value = 1,
    Alarm = 2,
    Status = 3,
    Cmd = 4,
}

/// <summary>ACL 拒绝原因（稳定码；与设备侧宏同字面量）。</summary>
public static class MqttAclIssue
{
    public const string UnknownTopic = "unknown-topic";
    public const string WrongDirection = "wrong-direction";
    public const string Forbidden = "forbidden";
}

/// <summary>
/// **MQTT 主题级 ACL**（阶段 286；提案 §4「授权」那一行）—— 设备侧唯一实现是
/// <c>firmware/t113/app/comm/mqtt_acl_contract.c</c>；本类是它对偶。
///
/// <para>规则：**设备只往 `{value,alarm,status}` 写（上行），远程端只往 `cmd/` 写（下行）**；
/// 每类主题各自要求一个权限位（复用编辑器生成的 `HMI_PERM_*` 与 <see cref="Permission"/> 同源）。</para>
///
/// <para>三条硬规则：<b>A1 方向不能反</b>（远程往遥测主题写 ⇒ 拒）·
/// <b>A2 未授权即拒</b> · <b>A3 不认识的主题即拒</b>（绝不默认放行）。</para>
/// </summary>
public static class MqttTopicAcl
{
    /// <summary>主题分类：前缀必须**逐字符**匹配（"包含"不算），后缀形状也要对。</summary>
    public static MqttTopicKind Classify(string? topic, string? prefix)
    {
        if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(prefix)) return MqttTopicKind.Unknown;
        if (!topic.StartsWith(prefix, StringComparison.Ordinal)) return MqttTopicKind.Unknown;
        if (topic.Length <= prefix.Length || topic[prefix.Length] != '/') return MqttTopicKind.Unknown;

        var rest = topic[(prefix.Length + 1)..];
        if (rest.StartsWith("value/", StringComparison.Ordinal)) return rest.Length > 6 ? MqttTopicKind.Value : MqttTopicKind.Unknown;
        if (rest.StartsWith("alarm/", StringComparison.Ordinal)) return rest.Length > 6 ? MqttTopicKind.Alarm : MqttTopicKind.Unknown;
        if (rest == "status") return MqttTopicKind.Status;
        if (rest.StartsWith("cmd/", StringComparison.Ordinal)) return rest.Length > 4 ? MqttTopicKind.Cmd : MqttTopicKind.Unknown;
        return MqttTopicKind.Unknown;
    }

    /// <summary>命令目标的权限位：<c>ack/</c> ⇒ 确认 · <c>shelve/</c>|<c>unshelve/</c> ⇒ 搁置 · 其它 ⇒ 操作。</summary>
    public static Permission RequiredCommandPermission(string target) =>
        target.StartsWith("ack/", StringComparison.Ordinal) ? Permission.AcknowledgeAlarm
        : target.StartsWith("shelve/", StringComparison.Ordinal) ? Permission.ShelveAlarm
        : target.StartsWith("unshelve/", StringComparison.Ordinal) ? Permission.ShelveAlarm
        : Permission.OperateControl;

    /// <summary>
    /// 该主题在该方向要求的权限位；<c>null</c> = 方向不允许（A1）；<c>"unknown"</c> 用
    /// <see cref="RequirePermission"/> 的返回值区分（未知名 ⇒ 抛/拒由调用点决定，这里返回 <c>null</c> 且
    /// <paramref name="unknown"/> 为 true）。
    /// </summary>
    public static Permission? RequiredPermission(MqttTopicKind kind, bool isWrite, string? target, out bool unknown)
    {
        unknown = false;
        switch (kind)
        {
            case MqttTopicKind.Value:
                return isWrite ? null : Permission.ViewScreen;
            case MqttTopicKind.Alarm:
                return isWrite ? null : Permission.ViewAlarmHistory;
            case MqttTopicKind.Status:
                return isWrite ? null : Permission.ViewScreen;
            case MqttTopicKind.Cmd:
                if (!isWrite) return null;
                if (string.IsNullOrEmpty(target)) { unknown = true; return null; }
                return RequiredCommandPermission(target!);
            default:
                unknown = true;
                return null;
        }
    }

    /// <summary>端到端判定：<c>null</c> = 允许；否则是稳定码（见 <see cref="MqttAclIssue"/>）。</summary>
    public static string? Evaluate(string? topic, string? prefix, bool isWrite, Permission permMask)
    {
        var kind = Classify(topic, prefix);
        if (kind == MqttTopicKind.Unknown) return MqttAclIssue.UnknownTopic;

        var target = kind == MqttTopicKind.Cmd ? topic![(prefix!.Length + 1 + 4)..] : null;
        var required = RequiredPermission(kind, isWrite, target, out var unknown);

        if (unknown) return MqttAclIssue.UnknownTopic;
        if (required is null) return MqttAclIssue.WrongDirection;
        return (permMask & required.Value) == required.Value ? null : MqttAclIssue.Forbidden;
    }
}
