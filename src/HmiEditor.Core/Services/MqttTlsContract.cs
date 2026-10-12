using System;
using System.Collections.Generic;

namespace HmiEditor.Core.Services;

/// <summary>MQTT 传输层安全模式（与设备侧 `hmi_mqtt_tls_mode_t` 同号同 token）。</summary>
public enum MqttTlsMode
{
    /// <summary>明文 —— **只有显式配置**才允许（绝不自动降级到这里）。</summary>
    Off = 0,

    /// <summary>预共享密钥：无证书、无时钟依赖（设备无 RTC ⇒ 推荐首选）。</summary>
    Psk = 1,

    /// <summary>X.509：需要正确的时钟（设备无 RTC ⇒ 必须先校时）。</summary>
    Ca = 2,
}

/// <summary>
/// **MQTT TLS 配置契约**（阶段 282；`C4=②` 的 M4 第一块）—— 设备侧唯一实现是
/// <c>firmware/t113/app/comm/mqtt_tls_contract.c</c>；本类是它的对偶（编辑器侧）。
///
/// <para><b>红线</b>（评估 `docs/2026-10-01-device-tls-assessment.md` §5）：**绝不静默降级为明文** ——
/// 未知模式**拒绝**（不是当 `off`）、`psk`/`ca` 缺件**拒绝**（不是退回 `off`）。</para>
///
/// <para><b>键</b>（写在 <c>comm_config.ini</c>）：<c>mqtt.tls=off|psk|ca</c>（缺省 ⇒ off，向后兼容）、
/// <c>mqtt.tls.psk_id</c>、<c>mqtt.tls.psk_file</c>（密钥**不进 ini**，单独文件便于设 0600）、
/// <c>mqtt.tls.ca_path</c>。</para>
/// </summary>
public static class MqttTlsContract
{
    /// <summary>ini 里的键名（生成器与解析器共用，免得两处拼字符串）。</summary>
    public const string ModeKey = "mqtt.tls";
    public const string PskIdKey = "mqtt.tls.psk_id";
    public const string PskFileKey = "mqtt.tls.psk_file";
    public const string CaPathKey = "mqtt.tls.ca_path";
    public const string LocalPortKey = "mqtt.tls.local_port";

    /// <summary>解析模式串（**只认小写** `<c>off</c>`/`<c>psk</c>`/`<c>ca</c>`；未知 ⇒ false）。</summary>
    public static bool TryParseMode(string? text, out MqttTlsMode mode)
    {
        switch (text)
        {
            case "off": mode = MqttTlsMode.Off; return true;
            case "psk": mode = MqttTlsMode.Psk; return true;
            case "ca": mode = MqttTlsMode.Ca; return true;
            default: mode = MqttTlsMode.Off; return false;   // 未知**不许**当 off
        }
    }

    /// <summary>模式 → token；未知枚举值抛（与设备侧返回 NULL 同义：没有合法 token）。</summary>
    public static string ModeToken(MqttTlsMode mode) => mode switch
    {
        MqttTlsMode.Off => "off",
        MqttTlsMode.Psk => "psk",
        MqttTlsMode.Ca => "ca",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知的 TLS 模式"),
    };

    /// <summary>
    /// 校验必需项：`psk` ⇒ 身份与密钥文件都非空；`ca` ⇒ CA 路径非空；`off` ⇒ 忽略其余。
    /// **缺件 = 失败**（红线：不许"缺证书 ⇒ 退回明文"）。
    /// </summary>
    public static bool Validate(MqttTlsMode mode, string? pskId, string? pskFile, string? caPath) => mode switch
    {
        MqttTlsMode.Off => true,
        MqttTlsMode.Psk => !string.IsNullOrEmpty(pskId) && !string.IsNullOrEmpty(pskFile),
        MqttTlsMode.Ca => !string.IsNullOrEmpty(caPath),
        _ => false,
    };

    /// <summary>本机终止代理的回环地址（明文监听只许绑它）。</summary>
    public const string Localhost = "127.0.0.1";

    /// <summary>INNI 缺 <c>mqtt.tls.local_port</c> 时代理的默认监听端口。</summary>
    public const int DefaultLocalPort = 1883;

    /// <summary>端口文本 → 端口号：空/NULL ⇒ <paramref name="defaultPort"/>；非法（非数字 / 0 / &gt;65535）⇒ false。</summary>
    public static bool TryParsePort(string? text, int defaultPort, out int port)
    {
        port = defaultPort;
        if (string.IsNullOrEmpty(text)) return true;
        if (text.Length > 5) return false;
        foreach (var c in text)
            if (c is < '0' or > '9') return false;
        return int.TryParse(text, out port) && port is >= 1 and <= 65535;
    }

    /// <summary>
    /// **MQTT 客户端该连哪里**（设备侧 <c>hmi_mqtt_tls_resolve_endpoint</c> 的对偶）：
    /// <c>off</c> ⇒ 直连 broker；<c>psk</c>/<c>ca</c> ⇒ 连本机代理（<c>127.0.0.1:local_port</c>）。
    /// ⚠ TLS 开着时**绝不**把 broker 地址交给客户端 —— 那就是绕过代理、明文上行。
    /// </summary>
    public static bool TryResolveEndpoint(MqttTlsMode mode, string? brokerHost, int brokerPort,
                                        string? tlsLocalPortText, out string host, out int port)
    {
        host = "";
        port = 0;

        if (string.IsNullOrEmpty(brokerHost)) return false;

        if (mode == MqttTlsMode.Off)
        {
            if (brokerPort is < 1 or > 65535) return false;
            host = brokerHost!;
            port = brokerPort;
            return true;
        }

        if (mode is not (MqttTlsMode.Psk or MqttTlsMode.Ca)) return false;
        if (!TryParsePort(tlsLocalPortText, DefaultLocalPort, out port)) return false;
        host = Localhost;
        return true;
    }

    /// <summary>回环判定（设备侧 <c>hmi_mqtt_tls_assert_no_bypass</c> 的一半）。</summary>
    public static bool IsLoopback(string? host) =>
        host is "127.0.0.1" or "::1" or "localhost";

    /// <summary>
    /// **不许绕过代理**：TLS 开着时客户端只能连回环（<c>off</c> 直连是显式配置的结果，恒真）。
    /// </summary>
    public static bool AssertNoBypass(MqttTlsMode mode, string? clientHost) => mode switch
    {
        MqttTlsMode.Off => true,
        MqttTlsMode.Psk or MqttTlsMode.Ca => IsLoopback(clientHost),
        _ => false,
    };

    /// <summary>
    /// 从 ini 的键值对解析出 TLS 配置。<b>缺 <see cref="ModeKey"/> ⇒ <see cref="MqttTlsMode.Off"/></b>
    /// （老工程向后兼容）；但**键存在而取值非法 ⇒ 失败**（不降级）。
    /// </summary>
    public static bool TryParse(IReadOnlyDictionary<string, string> ini, out MqttTlsMode mode,
                               out string? issue)
    {
        ArgumentNullException.ThrowIfNull(ini);
        issue = null;

        if (!ini.TryGetValue(ModeKey, out var text))
        {
            mode = MqttTlsMode.Off;
            return true;
        }

        if (!TryParseMode(text, out mode))
        {
            issue = "unknown-mode";
            return false;
        }

        ini.TryGetValue(PskIdKey, out var pskId);
        ini.TryGetValue(PskFileKey, out var pskFile);
        ini.TryGetValue(CaPathKey, out var caPath);

        if (!Validate(mode, pskId, pskFile, caPath))
        {
            issue = mode == MqttTlsMode.Psk ? "psk-incomplete" : "ca-missing";
            return false;
        }

        return true;
    }
}
