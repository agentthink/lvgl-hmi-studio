namespace Hmi.Communication.Contracts;

public enum TransportType { Serial, Tcp, Udp, Can, I2C, Spi, Custom }
public enum VariableDataType
{
    Bool, Int8, UInt8, Int16, UInt16, Int32, UInt32,
    Float32, Float64, String, ByteArray
}
public enum ConfigFieldType { Text, Number, Select, File, Checkbox }
public enum CommunicationInterface { Serial, Ethernet, CanBus, WiFi, Bluetooth }

public record CommunicationDriverMetadata(
    string Id,
    string Name,
    string Version,
    string Description
);

public record AddressPattern(
    string Pattern,
    string Example,
    string Description
);

public record ConnectionConfig(
    string DriverId,
    Dictionary<string, string> Parameters
);

public record ConfigField(
    string Key,
    string Label,
    ConfigFieldType Type,
    string? Default,
    IReadOnlyList<string>? Options
);

public record ConnectionConfigTemplate(
    IReadOnlyList<ConfigField> Fields
);

public record TagInfo(
    string Address,
    string Name,
    VariableDataType DataType,
    string? Description,
    bool IsReadOnly
);

public record VariableBinding(
    string ControlId,
    string PropertyName,
    string VariableId,
    BindingDirection Direction
);

public enum BindingDirection { ReadOnly, WriteOnly, ReadWrite }
