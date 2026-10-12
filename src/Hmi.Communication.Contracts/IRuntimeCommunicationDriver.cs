namespace Hmi.Communication.Contracts;

/// <summary>
/// 运行时通讯驱动契约（可选接口）—— 支持编辑器内实时读写设备的驱动实现此接口。
///
/// 与 ICommunicationDriver（设计时/代码生成契约）互补:
///   - ICommunicationDriver: 元数据、连接校验、标签导入、运行时 C 代码生成
///   - IRuntimeCommunicationDriver: 编辑器内真实连接设备并读写数据
///
/// 驱动通过 `driver is IRuntimeCommunicationDriver` 检测是否支持运行时连接。
/// 实现者可自行决定传输层（TCP/串口/共享内存等）。
/// </summary>
public interface IRuntimeCommunicationDriver
{
    /// <summary>尝试连接设备。失败时抛出 ConnectionException 或返回 false。</summary>
    bool Connect(ConnectionConfig config, TimeSpan timeout);

    /// <summary>断开连接。</summary>
    void Disconnect();

    /// <summary>是否已连接。</summary>
    bool IsConnected { get; }

    /// <summary>读取保持寄存器（Modbus 功能码 0x03）。</summary>
    /// <param name="startAddress">起始地址（0 基）。</param>
    /// <param name="count">寄存器数量。</param>
    Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count, CancellationToken ct = default);

    /// <summary>写单个保持寄存器（Modbus 功能码 0x06）。</summary>
    Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken ct = default);

    /// <summary>写多个保持寄存器（Modbus 功能码 0x10）。</summary>
    Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, CancellationToken ct = default);

    /// <summary>读取线圈（Modbus 功能码 0x01）。</summary>
    Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, CancellationToken ct = default);

    /// <summary>写单个线圈（Modbus 功能码 0x05）。</summary>
    Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken ct = default);
}

/// <summary>运行时通讯异常 —— 连接失败、超时、协议错误等。</summary>
public class CommunicationRuntimeException : Exception
{
    public CommunicationRuntimeException(string message) : base(message) { }
    public CommunicationRuntimeException(string message, Exception inner) : base(message, inner) { }
}
