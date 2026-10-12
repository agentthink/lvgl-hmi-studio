using Hmi.Communication.Contracts;

namespace Hmi.Communication.Builtin;

/// <summary>
/// Modbus TCP 驱动 —— 基于以太网的 Modbus TCP 协议（端口 502）。
/// 寄存器寻址与 RTU 相同，使用 MBAP 报文头替代 CRC 校验。
/// </summary>
public sealed class ModbusTcpDriver : ICommunicationDriver
{
    public CommunicationDriverMetadata Metadata => new(
        Id: "modbus-tcp",
        Name: "Modbus TCP",
        Version: "1.0.0",
        Description: "基于以太网的 Modbus TCP 协议（IANA 端口 502），使用 MBAP 报文头"
    );

    public IReadOnlyList<TransportType> SupportedTransports =>
        new[] { TransportType.Tcp };

    public AddressPattern AddressPattern => new(
        Pattern: "0x: 00001-09999 | 1x: 10001-19999 | 3x: 30001-39999 | 4x: 40001-49999",
        Example: "40001",
        Description: "Modbus 寻址格式（与 RTU 相同），网络层使用 TCP/IP"
    );

    public IReadOnlyList<VariableDataType> SupportedDataTypes =>
        new[] { VariableDataType.Bool, VariableDataType.Int16, VariableDataType.UInt16,
                VariableDataType.Int32, VariableDataType.UInt32, VariableDataType.Float32 };

    public ConnectionConfigTemplate GetConnectionConfigTemplate() => new(
        new ConfigField[]
        {
            new("host", "IP 地址", ConfigFieldType.Text, "192.168.1.1", null),
            new("port", "端口号", ConfigFieldType.Number, "502", null),
            new("slaveId", "单元标识符", ConfigFieldType.Number, "1", null),
            new("timeoutMs", "超时(ms)", ConfigFieldType.Number, "3000", null)
        }
    );

    public ValidationResult ValidateConnection(ConnectionConfig config)
    {
        var issues = new List<ValidationIssue>();
        var p = config.Parameters;

        if (!p.TryGetValue("host", out var host) || string.IsNullOrWhiteSpace(host))
            issues.Add(new(ValidationSeverity.Error, "MISSING_HOST", "IP 地址不能为空"));

        if (p.TryGetValue("port", out var portStr) && int.TryParse(portStr, out var port))
        {
            if (port < 1 || port > 65535)
                issues.Add(new(ValidationSeverity.Error, "INVALID_PORT", "端口号必须在 1-65535 范围内"));
        }

        return new ValidationResult(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    public Task<IReadOnlyList<TagInfo>> ImportTagsAsync(ConnectionConfig config) =>
        Task.FromResult<IReadOnlyList<TagInfo>>(Array.Empty<TagInfo>());

    public string GenerateRuntimeCode(IReadOnlyList<VariableBinding> bindings, ToolchainConfig toolchain) =>
        "// Modbus TCP runtime\n" +
        "#include \"modbus_tcp.h\"\n" +
        "modbus_tcp_connect(host, port);\n" +
        "modbus_tcp_start_polling();\n";
}
