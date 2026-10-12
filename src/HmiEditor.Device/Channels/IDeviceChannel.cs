namespace HmiEditor.Device.Channels;

/// <summary>
/// 设备通道异常 —— 通道打开/命令/传输失败时抛出。
/// </summary>
public sealed class DeviceChannelException : Exception
{
    public DeviceChannelException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>
/// 设备通信通道抽象 —— 编辑器与 T113 设备（串口 / 以太网）交互的统一接口。
/// 所有通道都遵循"控制台行协议"：发送一行命令，读取回显直至静默超时。
/// </summary>
public interface IDeviceChannel : IDisposable
{
    /// <summary>通道显示名（如 "COM3" / "tcp://192.168.1.100:23"）。</summary>
    string Name { get; }

    bool IsOpen { get; }

    /// <summary>通道类型（用于 UI 展示）。</summary>
    string Kind { get; }

    Task OpenAsync(CancellationToken ct = default);

    Task CloseAsync();

    /// <summary>
    /// 发送一行命令并读取全部回显（直到静默期结束或超时）。
    /// 返回去除行尾空白后的完整输出。
    /// </summary>
    Task<string> SendCommandAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default);

    /// <summary>
    /// 向设备传输一个本地文件到指定路径。
    /// 串口通道使用带校验的二进制流协议；网络通道使用 HTTP/TCP 传输。
    /// </summary>
    Task TransferFileAsync(string localPath, string remotePath, IProgress<double>? progress = null, CancellationToken ct = default);
}
