using System.IO.Ports;

namespace Hmi.Communication.Builtin;

/// <summary>
/// Modbus RTU 帧传输层抽象 —— 发送原始字节并接收一帧。
///
/// 生产实现: SerialRtuTransport（SerialPort）
/// 测试实现: 内存管道（无真实串口可回环测试协议编解码）
/// </summary>
public interface IRtuFrameTransport : IDisposable
{
    /// <summary>发送原始字节。</summary>
    Task SendBytesAsync(byte[] data, CancellationToken ct = default);

    /// <summary>
    /// 接收一帧。RTU 帧以 3.5 字符静默间隔为界，读取直到帧间间隔。
    /// 返回收到的字节（不含帧间间隔）。
    /// </summary>
    Task<byte[]> ReceiveFrameAsync(CancellationToken ct = default);

    /// <summary>打开传输通道。已打开时无操作。</summary>
    bool Open();

    /// <summary>关闭传输通道。</summary>
    void Close();

    /// <summary>是否打开。</summary>
    bool IsOpen { get; }
}

/// <summary>串口传输层 —— 基于 System.IO.Ports.SerialPort 的 RTU 帧传输。</summary>
public sealed class SerialRtuTransport : IRtuFrameTransport
{
    private readonly SerialPort _port;
    private readonly byte[] _readBuffer = new byte[1];

    /// <summary>帧间静默间隔（毫秒）—— 3.5 字符时间，9600 波特率约 4ms。</summary>
    public int InterFrameDelayMs { get; set; } = 8;

    /// <summary>接收整体超时（毫秒）。</summary>
    public int ReceiveTimeoutMs { get; set; } = 1000;

    public SerialRtuTransport(string portName, int baudRate,
        int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
    {
        _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
        {
            ReadTimeout = ReceiveTimeoutMs,
            WriteTimeout = 1000
        };
    }

    public bool IsOpen => _port.IsOpen;

    public bool Open()
    {
        if (_port.IsOpen) return true;
        _port.Open();
        _port.DiscardInBuffer();
        _port.DiscardOutBuffer();
        return _port.IsOpen;
    }

    public void Close()
    {
        if (_port.IsOpen)
            _port.Close();
    }

    public async Task SendBytesAsync(byte[] data, CancellationToken ct = default)
    {
        if (!_port.IsOpen) throw new InvalidOperationException("串口未打开");
        await _port.BaseStream.WriteAsync(data, ct);
        await _port.BaseStream.FlushAsync(ct);
    }

    public async Task<byte[]> ReceiveFrameAsync(CancellationToken ct = default)
    {
        if (!_port.IsOpen) throw new InvalidOperationException("串口未打开");

        var frame = new List<byte>(16);
        var lastByteTime = DateTime.UtcNow;
        var startTime = lastByteTime;

        // 读第一个字节（阻塞，带超时）
        try
        {
            var first = await _port.BaseStream.ReadAsync(_readBuffer, ct);
            if (first <= 0) return Array.Empty<byte>();
            frame.Add(_readBuffer[0]);
        }
        catch (TimeoutException)
        {
            return Array.Empty<byte>();
        }

        // 持续读取直到帧间静默间隔
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // 整体超时保护
            if ((DateTime.UtcNow - startTime).TotalMilliseconds > ReceiveTimeoutMs)
                break;

            if (_port.BytesToRead > 0)
            {
                var read = await _port.BaseStream.ReadAsync(_readBuffer, ct);
                if (read > 0)
                {
                    frame.Add(_readBuffer[0]);
                    lastByteTime = DateTime.UtcNow;
                }
            }
            else
            {
                // 无数据: 检查是否已达帧间静默间隔
                if ((DateTime.UtcNow - lastByteTime).TotalMilliseconds >= InterFrameDelayMs)
                    break;
                await Task.Delay(2, ct);
            }
        }

        return frame.ToArray();
    }

    public void Dispose() => Close();
}
