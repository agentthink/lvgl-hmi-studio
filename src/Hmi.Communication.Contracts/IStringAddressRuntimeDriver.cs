namespace Hmi.Communication.Contracts;

/// <summary>
/// 字符串地址运行时驱动（可选接口）—— 支持按"字符串地址"直接读写值的驱动实现此接口。
///
/// 与 <see cref="IRuntimeCommunicationDriver"/>（Modbus 寄存器/线圈语义）互补：
/// OPC UA（节点 ID 如 "ns=2;s=temp"）、MQTT（topic 映射）等非寄存器寻址协议
/// 无法用 ushort 地址表达，因此提供字符串地址通道。
///
/// 上层值源（ModbusRuntimeSource 等）优先检测 `driver is IStringAddressRuntimeDriver`，
/// 存在则直接透传标签地址，无需区号解析。
/// </summary>
public interface IStringAddressRuntimeDriver
{
    /// <summary>按地址读取工程值。读取失败返回 null（上层回退模拟）。</summary>
    Task<double?> ReadValueAsync(string address, CancellationToken ct = default);

    /// <summary>按地址写入工程值。返回是否成功。</summary>
    Task<bool> WriteValueAsync(string address, double value, CancellationToken ct = default);
}
