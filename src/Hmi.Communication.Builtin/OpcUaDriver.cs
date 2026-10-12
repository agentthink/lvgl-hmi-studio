using Hmi.Communication.Contracts;

namespace Hmi.Communication.Builtin;

/// <summary>
/// OPC UA 驱动（设计时契约）—— OPC UA 客户端配置与运行时 C 代码生成。
///
/// 寻址格式: OPC UA 标准节点 ID（"ns=2;s=temp" / "ns=2;i=100" / "ns=0;i=2258"）。
/// 连接参数: endpointUrl(opc.tcp://host:port) + SecurityPolicy=None。
/// </summary>
public sealed class OpcUaDriver : ICommunicationDriver
{
    public CommunicationDriverMetadata Metadata => new(
        Id: "opc-ua",
        Name: "OPC UA",
        Version: "1.0.0",
        Description: "OPC UA 二进制协议客户端（None 安全模式，轮询读写）—— 对接西门子/倍福等支持 OPC UA 的控制器"
    );

    public IReadOnlyList<TransportType> SupportedTransports =>
        new[] { TransportType.Tcp };

    public AddressPattern AddressPattern => new(
        Pattern: "ns={namespace};s={name} | ns={namespace};i={id}",
        Example: "ns=2;s=Line3.Temp | ns=2;i=100",
        Description: "OPC UA 节点 ID（字符串/数值标识），默认端口 4840"
    );

    public IReadOnlyList<VariableDataType> SupportedDataTypes =>
        new[] { VariableDataType.Bool, VariableDataType.Int16, VariableDataType.UInt16,
                VariableDataType.Int32, VariableDataType.UInt32, VariableDataType.Float32,
                VariableDataType.Float64, VariableDataType.String };

    public ConnectionConfigTemplate GetConnectionConfigTemplate() => new(
        new ConfigField[]
        {
            new("endpointUrl", "端点地址", ConfigFieldType.Text, "opc.tcp://192.168.1.1:4840", null),
            new("securityPolicy", "安全策略", ConfigFieldType.Select, "None",
                new[] { "None" }),
            new("timeoutMs", "超时(ms)", ConfigFieldType.Number, "3000", null)
        }
    );

    public ValidationResult ValidateConnection(ConnectionConfig config)
    {
        var issues = new List<ValidationIssue>();
        var p = config.Parameters;

        if (!p.TryGetValue("endpointUrl", out var endpoint) || string.IsNullOrWhiteSpace(endpoint))
        {
            issues.Add(new(ValidationSeverity.Error, "MISSING_ENDPOINT", "端点地址不能为空"));
        }
        else if (!endpoint.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new(ValidationSeverity.Error, "INVALID_ENDPOINT",
                "端点必须以 opc.tcp:// 开头"));
        }

        return new ValidationResult(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    public Task<IReadOnlyList<TagInfo>> ImportTagsAsync(ConnectionConfig config) =>
        Task.FromResult<IReadOnlyList<TagInfo>>(Array.Empty<TagInfo>());

    public string GenerateRuntimeCode(IReadOnlyList<VariableBinding> bindings, ToolchainConfig toolchain) =>
        """
        // OPC UA runtime (轻量客户端, None 安全模式, 轮询)
        #include "opcua_client.h"
        opcua_connect("opc.tcp://<host>:4840");
        opcua_start_polling(500);
        """ + (bindings.Count > 0
            ? "\n// 绑定示例: opcua_read_float(\"ns=2;s=Line3.Temp\");"
            : "");
}
