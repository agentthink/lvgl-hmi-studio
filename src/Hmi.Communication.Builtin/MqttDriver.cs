using Hmi.Communication.Contracts;

using HmiEditor.Core.Services;

namespace Hmi.Communication.Builtin;

/// <summary>
/// MQTT 驱动 —— 基于发布-订阅模式的物联网协议。
/// 适用于云端/局域网数据交换，支持 QoS 0/1/2。
/// </summary>
public sealed class MqttDriver : ICommunicationDriver
{
    public CommunicationDriverMetadata Metadata => new(
        Id: "mqtt",
        Name: "MQTT",
        Version: "1.0.0",
        Description: "基于发布-订阅模式的轻量级物联网协议 (MQTT v3.1.1)，支持 QoS 0/1/2"
    );

    public IReadOnlyList<TransportType> SupportedTransports =>
        new[] { TransportType.Tcp };

    public AddressPattern AddressPattern => new(
        Pattern: "{topic_path}",
        Example: "plant1/line3/motor1/temperature",
        Description: "MQTT Topic 路径（主题），支持通配符 +/# "
    );

    public IReadOnlyList<VariableDataType> SupportedDataTypes =>
        new[] { VariableDataType.Bool, VariableDataType.Int32, VariableDataType.Float32,
                VariableDataType.Float64, VariableDataType.String, VariableDataType.ByteArray };

    public ConnectionConfigTemplate GetConnectionConfigTemplate() => new(
        new ConfigField[]
        {
            new("broker", "Broker 地址", ConfigFieldType.Text, "localhost", null),
            new("port", "端口号", ConfigFieldType.Number, "1883", null),
            new("clientId", "客户端 ID", ConfigFieldType.Text, "hmi-editor-001", null),
            new("username", "用户名", ConfigFieldType.Text, "", null),
            new("password", "密码", ConfigFieldType.Text, "", null),
            new("keepAlive", "Keep Alive(s)", ConfigFieldType.Number, "60", null),
            new("qos", "默认 QoS", ConfigFieldType.Select, "1",
                new[] { "0", "1", "2" }),
            // ── TLS（阶段 283；契约 MqttTlsContract，评估 docs/2026-10-01-device-tls-assessment.md）──
            //   键名是**连接参数名**（短名，与 broker/port 同一层）；落到 ini 时才变成 mqtt.tls.*
            new("tls", "传输层安全", ConfigFieldType.Select, "off",
                new[] { "off", "psk", "ca" }),
            new("tlsPskId", "PSK 身份", ConfigFieldType.Text, "", null),
            new("tlsPskFile", "PSK 密钥文件", ConfigFieldType.Text, "", null),
            new("tlsCaPath", "CA 证书路径", ConfigFieldType.Text, "", null)
        }
    );

    public ValidationResult ValidateConnection(ConnectionConfig config)
    {
        var issues = new List<ValidationIssue>();
        var p = config.Parameters;

        if (!p.TryGetValue("broker", out var broker) || string.IsNullOrWhiteSpace(broker))
            issues.Add(new(ValidationSeverity.Error, "MISSING_BROKER", "Broker 地址不能为空"));

        if (p.TryGetValue("port", out var portStr) && int.TryParse(portStr, out var port))
        {
            if (port < 1 || port > 65535)
                issues.Add(new(ValidationSeverity.Error, "INVALID_PORT", "端口号必须在 1-65535 范围内"));
        }

        if (!p.TryGetValue("clientId", out var cid) || string.IsNullOrWhiteSpace(cid))
            issues.Add(new(ValidationSeverity.Error, "MISSING_CLIENT_ID", "客户端 ID 不能为空"));

        // ── TLS（阶段 283）：走唯一契约 MqttTlsContract，**缺件 = 保存不了** ──
        //    这是"现场配错 ⇒ 设备静默明文"这条路的最后一道闸（红线见评估 §5）
        p.TryGetValue("tls", out var tlsText);
        if (!MqttTlsContract.TryParseMode(string.IsNullOrEmpty(tlsText) ? "off" : tlsText, out var tlsMode))
        {
            issues.Add(new(ValidationSeverity.Error, "INVALID_TLS_MODE",
                $"传输层安全只支持 off / psk / ca（当前：{tlsText}）"));
        }
        else
        {
            p.TryGetValue("tlsPskId", out var pskId);
            p.TryGetValue("tlsPskFile", out var pskFile);
            p.TryGetValue("tlsCaPath", out var caPath);

            if (!MqttTlsContract.Validate(tlsMode, pskId, pskFile, caPath))
            {
                issues.Add(tlsMode == MqttTlsMode.Psk
                    ? new(ValidationSeverity.Error, "TLS_PSK_INCOMPLETE",
                        "PSK 模式必须同时填「PSK 身份」与「PSK 密钥文件」")
                    : new(ValidationSeverity.Error, "TLS_CA_MISSING",
                        "CA 模式必须填「CA 证书路径」"));
            }

            // 端口与实际安全档不匹配：**警告**（可以自建非标端口，但不能一声不响）
            if (tlsMode != MqttTlsMode.Off && p.TryGetValue("port", out var portText) &&
                portText == "1883")
            {
                issues.Add(new(ValidationSeverity.Warning, "TLS_PORT_LOOKS_PLAINTEXT",
                    $"TLS 已开启但端口是 1883（明文端口）——确认 broker 真在这个端口上终结 TLS"));
            }
        }

        return new ValidationResult(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    public Task<IReadOnlyList<TagInfo>> ImportTagsAsync(ConnectionConfig config) =>
        Task.FromResult<IReadOnlyList<TagInfo>>(Array.Empty<TagInfo>());

    public string GenerateRuntimeCode(IReadOnlyList<VariableBinding> bindings, ToolchainConfig toolchain) =>
        "// MQTT runtime\n" +
        "#include \"mqtt_client.h\"\n" +
        "mqtt_connect(broker, port, client_id);\n" +
        "// subscribe to topics...\n" +
        "mqtt_subscribe(topics, topic_count);\n";
}
