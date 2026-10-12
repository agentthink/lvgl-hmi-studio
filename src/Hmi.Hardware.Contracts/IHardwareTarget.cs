using HmiEditor.Core.Models;

namespace Hmi.Hardware.Contracts;

/// <summary>
/// 硬件支持的物理通讯接口类型。
/// </summary>
public enum CommunicationInterface { Serial, Ethernet, CanBus, WiFi, Bluetooth }

/// <summary>
/// 硬件目标插件契约 —— 所有芯片插件必须实现此接口。
/// 编辑器通过此接口与硬件目标交互，不直接依赖具体实现。
/// </summary>
public interface IHardwareTarget
{
    HardwareTargetMetadata Metadata { get; }
    DisplayConfig Display { get; }
    ResourceConstraints Resources { get; }
    LvglConfig LvglConfig { get; }
    ToolchainConfig Toolchain { get; }

    /// <summary>该硬件平台支持的物理通讯接口</summary>
    IReadOnlyList<CommunicationInterface> RecommendedInterfaces { get; }

    /// <summary>该硬件平台推荐的通讯协议驱动 ID</summary>
    IReadOnlyList<string> SupportedCommunicationDrivers { get; }

    ValidationResult ValidateProject(HmiProject project);
    IReadOnlyDictionary<string, object> GetTemplateParameters();
    string GenerateInitializationCode(HmiProject project);
}
