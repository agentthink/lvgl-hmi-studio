namespace Hmi.Hardware.Contracts;

public record HardwareTargetMetadata(
    string Id,
    string Name,
    string Vendor,
    string Architecture,
    string Version
);
