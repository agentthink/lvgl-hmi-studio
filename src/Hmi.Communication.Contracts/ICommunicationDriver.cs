namespace Hmi.Communication.Contracts;

/// <summary>
/// 通讯协议驱动插件契约 —— 所有工业协议驱动必须实现此接口。
/// 与 IHardwareTarget 模式一致：编辑器通过此接口与通讯协议交互。
/// </summary>
public interface ICommunicationDriver
{
    CommunicationDriverMetadata Metadata { get; }
    IReadOnlyList<TransportType> SupportedTransports { get; }
    AddressPattern AddressPattern { get; }
    IReadOnlyList<VariableDataType> SupportedDataTypes { get; }

    ConnectionConfigTemplate GetConnectionConfigTemplate();
    ValidationResult ValidateConnection(ConnectionConfig config);
    Task<IReadOnlyList<TagInfo>> ImportTagsAsync(ConnectionConfig config);
    string GenerateRuntimeCode(IReadOnlyList<VariableBinding> bindings, ToolchainConfig toolchain);
}
