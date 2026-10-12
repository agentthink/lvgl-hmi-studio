using System;
using System.Globalization;

namespace HmiEditor.Core.Services;

/// <summary>写校验结果码（与设备侧 <c>HMI_MQTT_WRITE_*</c> 同值）。</summary>
public enum MqttWriteResult
{
    Ok = 0,
    ReadOnly = -1,
    NotANumber = -2,
    OutOfRange = -3,
    BadTarget = -4,
}

/// <summary>
/// **远程写命令的值校验**（阶段 287；M5 设备端执行的一半）—— 设备侧唯一实现是
/// <c>firmware/t113/app/comm/mqtt_write_guard.c</c>；本类是它对偶。
///
/// <para>顺序是契约的一部分：**只读优先于"值是否合法"**（只读变量的值再合法也不许写）；
/// 量程两端**含**；<c>min &gt; max</c> 是**配置错**（<see cref="MqttWriteResult.BadTarget"/>），不是"值错"。</para>
/// </summary>
public static class MqttWriteGuard
{
    /// <summary>稳定码 token（审计/界面按它分派文案；<see cref="MqttWriteResult.Ok"/> ⇒ <c>null</c>）。</summary>
    public static string? Token(MqttWriteResult result) => result switch
    {
        MqttWriteResult.Ok => null,
        MqttWriteResult.ReadOnly => "read-only",
        MqttWriteResult.NotANumber => "not-a-number",
        MqttWriteResult.OutOfRange => "out-of-range",
        MqttWriteResult.BadTarget => "bad-target",
        _ => null,
    };

    /// <summary>
    /// 严格数字解析：整串都必须是数字（不许空白、不许尾随垃圾、不许 <c>nan</c>/<c>inf</c> 文本）。
    /// 与设备侧 <c>strtod</c> + 整串校验同口径。
    /// </summary>
    public static bool TryParseStrict(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var c in text)
            if (c is ' ' or '\t' or '\n' or '\r') return false;

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return false;
        if (double.IsNaN(v) || double.IsInfinity(v)) return false;

        value = v;
        return true;
    }

    /// <summary>纯判定（与设备侧同名同序）。</summary>
    public static MqttWriteResult Check(bool writable, double min, double max, string? valueText,
                                      out double value)
    {
        value = 0;

        if (min > max) return MqttWriteResult.BadTarget;
        if (!writable) return MqttWriteResult.ReadOnly;          // 只读优先
        if (!TryParseStrict(valueText, out value)) return MqttWriteResult.NotANumber;
        if (value < min || value > max) return MqttWriteResult.OutOfRange;
        return MqttWriteResult.Ok;
    }
}
