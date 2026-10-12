using System;
using System.Globalization;

namespace HmiEditor.Core.Services;

/// <summary>契约 M2 的一条状态消息（解析结果）。</summary>
/// <param name="Online">在线（<c>on=1</c>）。</param>
/// <param name="TimestampMs">时间戳（遗嘱那一档没有 ⇒ 0）。</param>
/// <param name="TimeBase">时间基（遗嘱那一档没有 ⇒ <see cref="MqttTimeBase.Unknown"/>）。</param>
/// <param name="HasTimestamp">是否带了时间戳（遗嘱 <c>on=0</c> **不带** —— 设备已掉线，没人能给它打时间）。</param>
public readonly record struct MqttStatusMessage(bool Online, long TimestampMs, MqttTimeBase TimeBase, bool HasTimestamp);

/// <summary>
/// **设备状态上行契约（M2）**（阶段 272；登记 `§2-#12` 的 M2）。
///
/// <para><b>主题</b>：<c>&lt;prefix&gt;/status</c>（retained）<br/>
/// <b>payload</b>：<c>on=&lt;0|1&gt;;t=&lt;毫秒&gt;;tb=&lt;boot|utc&gt;</c>；
/// **遗嘱（LWT）用 <see cref="LwtPayload"/>**＝<c>on=0</c>（无时间戳）。</para>
///
/// <para><b>为什么离线/遗嘱两条要用同一个常量</b>：设备掉线时是 **broker 代发**遗嘱，
/// 而设备自己正常下线（如停机脚本）会主动发布离线 payload —— 两条如果拼法不同，远程端就得判两种形状。
/// 设备侧 <c>hmi_mqtt_format_status_payload(online=0, …)</c> 已经**忽略时间戳**直接输出遗嘱常量
/// （见 `mqtt_contract.c`），这里保持同一口径。</para>
/// </summary>
public static class MqttStatusContract
{
    /// <summary>主题里的固定段名。</summary>
    public const string StatusSegment = "status";

    /// <summary>遗嘱 payload（**逐字节**与设备侧 <c>HMI_MQTT_STATUS_LWT_PAYLOAD</c> 相同）。</summary>
    public const string LwtPayload = "on=0";

    /// <summary>格式化主题。</summary>
    public static string FormatTopic(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("前缀不能为空", nameof(prefix));
        return $"{prefix}/{StatusSegment}";
    }

    /// <summary>解析主题（<c>&lt;prefix&gt;/status</c>，前缀可含 <c>/</c>）。</summary>
    public static bool TryParseTopic(string? topic, out string prefix)
    {
        prefix = "";
        if (string.IsNullOrEmpty(topic)) return false;

        var marker = "/" + StatusSegment;
        if (!topic.EndsWith(marker, StringComparison.Ordinal)) return false;

        var head = topic[..^marker.Length];
        if (head.Length == 0) return false;

        prefix = head;
        return true;
    }

    /// <summary>格式化 payload（<paramref name="online"/> = false ⇒ 输出 <see cref="LwtPayload"/>，忽略时间戳）。</summary>
    public static string FormatPayload(bool online, long timestampMs, MqttTimeBase timeBase)
    {
        if (!online) return LwtPayload;
        if (timeBase == MqttTimeBase.Unknown)
            throw new ArgumentException("在线状态必须声明时间基（boot/utc）", nameof(timeBase));

        var token = timeBase == MqttTimeBase.Utc ? "utc" : "boot";
        return $"on=1;t={timestampMs.ToString(CultureInfo.InvariantCulture)};tb={token}";
    }

    /// <summary>解析 payload：在线档要时间基；**遗嘱档 <c>on=0</c> 无时间戳也算合法**。</summary>
    public static bool TryParsePayload(string? payload, out MqttStatusMessage message)
    {
        message = default;
        if (!MqttPayloadFields.TryParse(payload, out var map)) return false;
        if (!map.TryGetValue("on", out var on)) return false;

        if (on == "0")
        {
            message = new MqttStatusMessage(false, 0, MqttTimeBase.Unknown, HasTimestamp: false);
            return true;
        }

        if (on != "1") return false;
        if (!map.TryGetValue("tb", out var tb)) return false;

        var timeBase = tb switch
        {
            "boot" => MqttTimeBase.Boot,
            "utc" => MqttTimeBase.Utc,
            _ => MqttTimeBase.Unknown,
        };
        if (timeBase == MqttTimeBase.Unknown) return false;

        var timestamp = 0L;
        if (map.TryGetValue("t", out var t) &&
            !long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp)) return false;

        message = new MqttStatusMessage(true, timestamp, timeBase, HasTimestamp: map.ContainsKey("t"));
        return true;
    }
}
