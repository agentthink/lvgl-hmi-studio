using Hmi.Communication.Contracts;

namespace Hmi.Communication.Builtin;

/// <summary>
/// Modbus RTU 驱动 —— 基于 RS-232/485 串行链路的 Modbus RTU 协议。
/// 支持线圈(0x)、离散输入(1x)、保持寄存器(4x)、输入寄存器(3x)。
/// </summary>
public sealed class ModbusRtuDriver : ICommunicationDriver
{
    public CommunicationDriverMetadata Metadata => new(
        Id: "modbus-rtu",
        Name: "Modbus RTU",
        Version: "1.0.0",
        Description: "基于 RS-232/485 串行链路的 Modbus RTU 协议，支持线圈、离散输入、保持寄存器、输入寄存器"
    );

    public IReadOnlyList<TransportType> SupportedTransports =>
        new[] { TransportType.Serial };

    public AddressPattern AddressPattern => new(
        Pattern: "0x: 00001-09999 | 1x: 10001-19999 | 3x: 30001-39999 | 4x: 40001-49999",
        Example: "40001",
        Description: "Modbus 寻址格式：首位数字表示寄存器类型（0=线圈 1=离散输入 3=输入寄存器 4=保持寄存器），后四位为偏移地址"
    );

    public IReadOnlyList<VariableDataType> SupportedDataTypes =>
        new[] { VariableDataType.Bool, VariableDataType.Int16, VariableDataType.UInt16,
                VariableDataType.Int32, VariableDataType.UInt32, VariableDataType.Float32 };

    public ConnectionConfigTemplate GetConnectionConfigTemplate() => new(
        new ConfigField[]
        {
            new("port", "串口号", ConfigFieldType.Select, "COM1",
                new[] { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6" }),
            new("baud", "波特率", ConfigFieldType.Select, "9600",
                new[] { "4800", "9600", "19200", "38400", "57600", "115200" }),
            new("dataBits", "数据位", ConfigFieldType.Select, "8",
                new[] { "7", "8" }),
            new("stopBits", "停止位", ConfigFieldType.Select, "1",
                new[] { "1", "2" }),
            new("parity", "校验位", ConfigFieldType.Select, "None",
                new[] { "None", "Even", "Odd" }),
            new("slaveId", "从站地址", ConfigFieldType.Number, "1", null),
            new("timeoutMs", "超时(ms)", ConfigFieldType.Number, "1000", null)
        }
    );

    public ValidationResult ValidateConnection(ConnectionConfig config)
    {
        var issues = new List<ValidationIssue>();
        var p = config.Parameters;

        if (!p.TryGetValue("slaveId", out var sid) || !int.TryParse(sid, out var slaveId) || slaveId < 1 || slaveId > 247)
            issues.Add(new(ValidationSeverity.Error, "INVALID_SLAVE_ID", "从站地址必须在 1-247 范围内"));

        if (!p.TryGetValue("port", out var port) || string.IsNullOrWhiteSpace(port))
            issues.Add(new(ValidationSeverity.Error, "MISSING_PORT", "串口号不能为空"));

        var validBauds = new[] { "4800", "9600", "19200", "38400", "57600", "115200" };
        if (p.TryGetValue("baud", out var baud) && !validBauds.Contains(baud))
            issues.Add(new(ValidationSeverity.Warning, "UNUSUAL_BAUD", $"波特率 {baud} 不是常用值",
                new object[] { baud }));

        return new ValidationResult(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    public Task<IReadOnlyList<TagInfo>> ImportTagsAsync(ConnectionConfig config) =>
        Task.FromResult<IReadOnlyList<TagInfo>>(Array.Empty<TagInfo>());

    public string GenerateRuntimeCode(IReadOnlyList<VariableBinding> bindings, ToolchainConfig toolchain) =>
        "// Modbus RTU runtime\n" +
        "#include \"modbus_rtu.h\"\n" +
        "modbus_rtu_init(port, baud, slave_id);\n" +
        "modbus_rtu_start_polling();\n";
}
