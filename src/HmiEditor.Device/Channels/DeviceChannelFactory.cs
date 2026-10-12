using HmiEditor.Device.Abstractions;

namespace HmiEditor.Device.Channels;

/// <summary>
/// **默认通道工厂**（阶段 206 的注入缝实现）—— 就是把 <c>DeviceManagerView</c> 原先那两行
/// 就地 <c>new</c> 搬到这里，行为零变化：串口走 <see cref="SerialDeviceChannel"/>、
/// 以太网走 <see cref="TcpDeviceChannel"/>。
/// </summary>
public sealed class DeviceChannelFactory : IDeviceChannelFactory
{
    /// <inheritdoc />
    public IDeviceChannel CreateSerial(string portName, int baudRate) => new SerialDeviceChannel(portName, baudRate);

    /// <inheritdoc />
    public IDeviceChannel CreateTcp(string host, int port) => new TcpDeviceChannel(host, port);
}
