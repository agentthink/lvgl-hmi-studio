using System;
using System.Collections.Generic;
using System.Globalization;

namespace HmiEditor.Core.Services;

/// <summary>契约 M2 的一条报警消息（解析结果）。</summary>
/// <param name="State">报警状态（与设备/C# 的 <c>AlarmState</c> 同号：1=Active · 2=Acknowledged · 3=Returned · **4=搁置（保留）**）。</param>
/// <param name="Severity">严重度（与 <c>AlarmSeverity</c> 同号：0=Info · 1=Warning · 2=Critical · 3=Emergency）。</param>
/// <param name="TimestampMs">时间戳。</param>
/// <param name="TimeBase">时间基。</param>
public readonly record struct MqttAlarmMessage(int State, int Severity, long TimestampMs, MqttTimeBase TimeBase);

/// <summary>
/// **报警上行契约（M2）**（阶段 272；登记 `§2-#12` 的 M2）。
/// 设备侧唯一实现是 <c>firmware/t113/app/comm/mqtt_contract.c</c>；本类是它的对偶（解析 ＋ 同形状格式化）。
///
/// <para><b>主题</b>：<c>&lt;prefix&gt;/alarm/&lt;报警 ID&gt;</c>（报警 ID 不许含 <c>/</c>）<br/>
/// <b>payload</b>：<c>st=&lt;1..4&gt;;sev=&lt;0..3&gt;;t=&lt;毫秒&gt;;tb=&lt;boot|utc&gt;</c></para>
///
/// <para><b>为什么状态用 1..4 而不是直接绑 <c>AlarmState</c> 枚举</b>：A1=② 已决定"搁置设备侧也做"，
/// 而 <c>AlarmState</c> 现在**没有** Shelved 档 —— 用 <c>int</c> ＋ <see cref="ShelvedState"/> 常量，
/// 搁置落地时不必改契约（也不必先改枚举再改契约，那会拖出一次跨程序集连锁）。</para>
///
/// <para><b>0 为什么不可发布</b>：<c>Inactive</c> 不是一次"状态迁移"（设备侧 state==0 是复位态）——
/// 允许它发出来只会让远程端收到"某报警处于非激活"这种没有意义的事件。</para>
/// </summary>
public static class MqttAlarmContract
{
    /// <summary>主题里的固定段名。</summary>
    public const string AlarmSegment = "alarm";

    /// <summary>可发布的**最小**状态：<c>Active</c>。</summary>
    public const int MinState = 1;

    /// <summary>可发布的**最大**状态：**搁置保留位**（尚未产出，契约先留位）。</summary>
    public const int ShelvedState = 4;

    /// <summary>严重度上界（Emergency）。</summary>
    public const int MaxSeverity = 3;

    /// <summary>格式化主题：<c>&lt;prefix&gt;/alarm/&lt;报警 ID&gt;</c>。</summary>
    public static string FormatTopic(string prefix, string alarmId)
    {
        if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("前缀不能为空", nameof(prefix));
        if (string.IsNullOrEmpty(alarmId)) throw new ArgumentException("报警 ID 不能为空", nameof(alarmId));
        if (alarmId.Contains('/')) throw new ArgumentException("报警 ID 不能含 '/'（会改写主题层级）", nameof(alarmId));
        return $"{prefix}/{AlarmSegment}/{alarmId}";
    }

    /// <summary>解析主题（报警 ID 必须是**单段**）。</summary>
    public static bool TryParseTopic(string? topic, out string prefix, out string alarmId)
    {
        prefix = "";
        alarmId = "";
        if (string.IsNullOrEmpty(topic)) return false;

        var marker = "/" + AlarmSegment + "/";
        var at = topic.IndexOf(marker, StringComparison.Ordinal);
        if (at <= 0) return false;

        var id = topic[(at + marker.Length)..];
        if (id.Length == 0 || id.Contains('/')) return false;

        prefix = topic[..at];
        alarmId = id;
        return true;
    }

    /// <summary>格式化 payload。</summary>
    public static string FormatPayload(int state, int severity, long timestampMs, MqttTimeBase timeBase)
    {
        if (state < MinState || state > ShelvedState)
            throw new ArgumentOutOfRangeException(nameof(state), state, $"状态只允许 {MinState}…{ShelvedState}");
        if (severity < 0 || severity > MaxSeverity)
            throw new ArgumentOutOfRangeException(nameof(severity), severity, $"严重度只允许 0…{MaxSeverity}");
        if (timeBase == MqttTimeBase.Unknown)
            throw new ArgumentException("契约必须声明时间基（boot/utc）", nameof(timeBase));

        var token = timeBase == MqttTimeBase.Utc ? "utc" : "boot";
        return $"st={state.ToString(CultureInfo.InvariantCulture)};"
             + $"sev={severity.ToString(CultureInfo.InvariantCulture)};"
             + $"t={timestampMs.ToString(CultureInfo.InvariantCulture)};tb={token}";
    }

    /// <summary>解析 payload（未知键忽略；缺 <c>st</c>/<c>sev</c>/<c>tb</c> 或取值越界 ⇒ 失败）。</summary>
    public static bool TryParsePayload(string? payload, out MqttAlarmMessage message)
    {
        message = default;
        if (!MqttPayloadFields.TryParse(payload, out var map)) return false;

        if (!map.TryGetValue("st", out var st) ||
            !int.TryParse(st, NumberStyles.Integer, CultureInfo.InvariantCulture, out var state)) return false;
        if (state < MinState || state > ShelvedState) return false;

        if (!map.TryGetValue("sev", out var sev) ||
            !int.TryParse(sev, NumberStyles.Integer, CultureInfo.InvariantCulture, out var severity)) return false;
        if (severity < 0 || severity > MaxSeverity) return false;

        var timestamp = 0L;
        if (map.TryGetValue("t", out var t) &&
            !long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp)) return false;

        if (!map.TryGetValue("tb", out var tb)) return false;
        var timeBase = tb switch
        {
            "boot" => MqttTimeBase.Boot,
            "utc" => MqttTimeBase.Utc,
            _ => MqttTimeBase.Unknown,
        };
        if (timeBase == MqttTimeBase.Unknown) return false;

        message = new MqttAlarmMessage(state, severity, timestamp, timeBase);
        return true;
    }

    /// <summary>契约里的字段名（给测试与远程端共用，避免各写一份字面量）。</summary>
    internal static IReadOnlyList<string> RequiredKeys { get; } = new[] { "st", "sev", "tb" };
}
