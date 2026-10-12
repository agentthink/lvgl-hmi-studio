using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HmiEditor.Core.Services;

/// <summary>payload 里 `tb=` 声明的**时间基**。</summary>
public enum MqttTimeBase
{
    /// <summary>旧格式（裸数值）—— 既没有时间戳也没有时间基，解析时只能标"未知"。</summary>
    Unknown = 0,

    /// <summary>开机时长 ms（设备无 RTC 时给的就是它）。</summary>
    Boot = 1,

    /// <summary>UTC epoch ms（墙钟）。</summary>
    Utc = 2,
}

/// <summary>契约 v1 的一条值消息（解析结果）。</summary>
/// <param name="Value">值。</param>
/// <param name="TimestampMs">时间戳（含义由 <paramref name="TimeBase"/> 决定）。</param>
/// <param name="TimeBase">时间基；旧格式为 <see cref="MqttTimeBase.Unknown"/>。</param>
/// <param name="Quality">质量位：true=好值。</param>
/// <param name="IsLegacy">是否为**改造前**的裸数值 payload（`%g`，无时间戳/质量位）。</param>
public readonly record struct MqttValueMessage(
    double Value, long TimestampMs, MqttTimeBase TimeBase, bool Quality, bool IsLegacy);

/// <summary>
/// 远程访问的**主题 / payload 契约 v1**（阶段 264；登记 `§2-#12` 的 M1）。
///
/// <para><b>设备侧的唯一实现</b>是 <c>firmware/t113/app/comm/mqtt_contract.c</c>；本类是它的
/// **对偶实现（解析侧 + 同形状的格式化侧）**。两侧靠**同一张样例表**逐字节判：
/// `firmware/t113/tests/hmi_comm_test.c` 的 <c>k_contract_samples[]</c> 与
/// <c>MqttValueContractTests</c> 的表由判据比对（漂移即红）。</para>
///
/// <para><b>契约</b>：主题 <c>&lt;prefix&gt;/val/&lt;变量名&gt;</c>（prefix 可含 `/`，变量名**不许**含 `/`）；
/// payload <c>v=&lt;值&gt;;t=&lt;毫秒&gt;;tb=&lt;utc|boot&gt;;q=&lt;0|1&gt;</c>。
/// 值用 <c>%.6g</c> 口径（与 C 侧逐字节一致，见 <see cref="FormatNumber"/>）。</para>
///
/// <para><b>为什么把 `tb=` 写死进契约</b>：设备侧没有 RTC，能给的只有开机时长 ⇒ 若契约不声明时间基，
/// 远程端会把"开机 3 秒"当成"1970 年"或"2026 年"。这与 <c>DataLogger.cs</c> 记的 CSV 时间基问题是
/// **同一个坑**（那里的表头写成 `timestamp_utc_ms` 就是为了不冒充 `timestamp_ms`）。</para>
/// </summary>
public static class MqttValueContract
{
    /// <summary>主题里值段的固定名字。</summary>
    public const string ValueSegment = "val";

    private static readonly string[] RequiredKeys = { "v" };

    /// <summary>格式化主题：<c>&lt;prefix&gt;/val/&lt;变量名&gt;</c>。</summary>
    /// <exception cref="ArgumentException">前缀/变量名为空，或变量名含 `/`（会改写主题层级）。</exception>
    public static string FormatValueTopic(string prefix, string variableName)
    {
        if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("前缀不能为空", nameof(prefix));
        if (string.IsNullOrEmpty(variableName)) throw new ArgumentException("变量名不能为空", nameof(variableName));
        if (variableName.Contains('/')) throw new ArgumentException("变量名不能含 '/'（会改写主题层级）", nameof(variableName));
        return $"{prefix}/{ValueSegment}/{variableName}";
    }

    /// <summary>解析主题。<c>&lt;prefix&gt;/val/&lt;变量名&gt;</c>，变量名必须是**单段**。</summary>
    public static bool TryParseValueTopic(string? topic, out string prefix, out string variableName)
    {
        prefix = "";
        variableName = "";
        if (string.IsNullOrEmpty(topic)) return false;

        var marker = "/" + ValueSegment + "/";
        var at = topic.IndexOf(marker, StringComparison.Ordinal);
        if (at <= 0) return false;                                  // 前缀必须非空

        var name = topic[(at + marker.Length)..];
        if (name.Length == 0 || name.Contains('/')) return false;    // 变量名单段

        prefix = topic[..at];
        variableName = name;
        return true;
    }

    /// <summary>格式化 payload：<c>v=&lt;值&gt;;t=&lt;毫秒&gt;;tb=&lt;utc|boot&gt;;q=&lt;0|1&gt;</c>。</summary>
    public static string FormatValuePayload(double value, long timestampMs, MqttTimeBase timeBase, bool quality)
    {
        if (timeBase == MqttTimeBase.Unknown)
            throw new ArgumentException("契约 v1 必须声明时间基（boot/utc）", nameof(timeBase));

        var token = timeBase == MqttTimeBase.Utc ? "utc" : "boot";
        return $"v={FormatNumber(value)};t={timestampMs.ToString(CultureInfo.InvariantCulture)};tb={token};q={(quality ? 1 : 0)}";
    }

    /// <summary>
    /// 解析 payload。未知键**忽略**（向前兼容）；缺 <c>v</c> ⇒ 失败；
    /// **裸数值**（改造前的 `%g`）⇒ 成功但 <see cref="MqttValueMessage.IsLegacy"/> = true。
    /// </summary>
    public static bool TryParseValuePayload(string? payload, out MqttValueMessage message)
    {
        message = default;
        if (string.IsNullOrWhiteSpace(payload)) return false;

        var text = payload.Trim();
        if (!text.Contains(';'))
        {
            // 旧格式：`%g` 裸数值 —— 接受，但明确标成 legacy（没有时间戳/质量位可给）
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var legacy)) return false;
            message = new MqttValueMessage(legacy, 0, MqttTimeBase.Unknown, true, IsLegacy: true);
            return true;
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in text.Split(';'))
        {
            if (part.Length == 0) continue;
            var eq = part.IndexOf('=');
            if (eq <= 0) return false;
            map[part[..eq]] = part[(eq + 1)..];
        }

        foreach (var key in RequiredKeys)
            if (!map.ContainsKey(key)) return false;

        if (!double.TryParse(map["v"], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return false;

        var timestamp = 0L;
        if (map.TryGetValue("t", out var t) &&
            !long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp)) return false;

        var timeBase = MqttTimeBase.Unknown;
        if (map.TryGetValue("tb", out var tb))
        {
            timeBase = tb switch
            {
                "boot" => MqttTimeBase.Boot,
                "utc" => MqttTimeBase.Utc,
                _ => MqttTimeBase.Unknown,
            };
            if (timeBase == MqttTimeBase.Unknown) return false;      // 契约 v1 里 `tb` 只许这两个值
        }

        var quality = true;
        if (map.TryGetValue("q", out var q))
        {
            if (q == "0") quality = false;
            else if (q == "1") quality = true;
            else return false;
        }

        message = new MqttValueMessage(value, timestamp, timeBase, quality, IsLegacy: false);
        return true;
    }

    /// <summary>
    /// 数值写法 = C 的 <c>%.6g</c>（**逐字节一致**是本契约的一半）。
    ///
    /// <para>C# 的 <c>"G6"</c> 与 C 的 <c>%.6g</c> 在有效位数与切换阈值上一致，
    /// 只有指数部分的大小写不同（C# 是 <c>1E+06</c>、C 是 <c>1e+06</c>）⇒ 这里把 <c>E</c> 归一成 <c>e</c>。
    /// 这条等价性**不是推理**：同源样例表里有 <c>v=1e+06</c> 一档，两侧判据都拿它逐字节比对。</para>
    /// </summary>
    public static string FormatNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentException("契约不接受 NaN/Infinity（C 侧 %.6g 会产出 nan/inf，远程端无法消费）", nameof(value));

        var text = value.ToString("G6", CultureInfo.InvariantCulture);
        return text.Replace("E", "e", StringComparison.Ordinal);
    }
}
