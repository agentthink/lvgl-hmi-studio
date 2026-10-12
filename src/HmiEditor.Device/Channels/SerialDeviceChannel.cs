using System.IO.Ports;
using System.Text;

namespace HmiEditor.Device.Channels;

/// <summary>
/// 串口通道 —— 通过 CH340 USB-TTL（115200 8N1）与设备控制台交互。
/// 遵循 busybox shell 行协议；文件传输使用 base64 + heredoc 分块（设备端纯 shell 即可接收，无需改固件）。
///
/// 并发契约：<see cref="SendCommandAsync"/> 与 <see cref="TransferFileAsync"/> 整帧串行化
/// （<c>SemaphoreSlim</c>），因为"开帧 → 写入 → 等本帧结束"必须是一个不可分割的临界区，
/// 否则并发命令会在同一端口上交错收发，导致响应张冠李戴。
/// 收帧判据本身只有一份实现 —— <see cref="CommandFrameCollector"/>，与 TCP 通道共用。
/// 调用方取消（<paramref name="ct"/>）原样抛出 <see cref="OperationCanceledException"/>；
/// 仅"等待超时"转换为 <see cref="DeviceChannelException"/>。
/// </summary>
public sealed class SerialDeviceChannel : IDeviceChannel
{
    private readonly SerialPort _port;
    private readonly CommandFrameCollector _frames = new();
    private CancellationTokenSource? _readCts;

    /// <summary>整帧串行化闸门（非重入）：命令与文件传输共用。</summary>
    private readonly SemaphoreSlim _commandGate = new(1, 1);

    /// <summary>串口波特率（T113 调试串口默认 115200）。</summary>
    public const int DefaultBaudRate = 115200;

    /// <summary>命令输出结束判定：帧内数据间隙的静默期（毫秒）。</summary>
    public int SilentTimeoutMs { get; set; } = 300;

    /// <summary>
    /// 首字节宽限窗口（毫秒）：写完命令后，过这么久仍**一字节未到**才判"本命令无输出"并返回空串。
    /// <para>刻意独立于 <see cref="SilentTimeoutMs"/>：后者管帧内行间隙，前者管设备响应起步延迟
    /// （shell 调度、进程抢占、主机满载都能把起步拖过 300 ms）。实际生效值取二者较大者。</para>
    /// </summary>
    public int FirstByteTimeoutMs { get; set; } = 1000;

    /// <summary>命令整体超时（秒）。</summary>
    public int CommandTimeoutSeconds { get; set; } = 10;

    public string Kind => "serial";

    public string Name { get; }

    public bool IsOpen { get; private set; }

    public SerialDeviceChannel(string portName, int baudRate = DefaultBaudRate)
    {
        Name = portName;
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 500,
            WriteTimeout = 2000
        };
    }

    public Task OpenAsync(CancellationToken ct = default)
    {
        if (IsOpen) return Task.CompletedTask;
        try
        {
            _port.Open();
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();
            IsOpen = true;

            // 后台读线程持续吸收字节流，供 SendCommandCoreAsync 取用
            _readCts?.Dispose();
            _readCts = new CancellationTokenSource();
            _ = ReadLoopAsync(_readCts.Token);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new DeviceChannelException($"打开串口 {Name} 失败: {ex.Message}", ex);
        }
    }

    public Task CloseAsync()
    {
        if (!IsOpen) return Task.CompletedTask;
        CancelReadLoop();
        try { _port.Close(); } catch { /* 关闭失败忽略 */ }
        IsOpen = false;
        return Task.CompletedTask;
    }

    public async Task<string> SendCommandAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        EnsureOpen();
        await _commandGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await SendCommandCoreAsync(command, timeout, ct).ConfigureAwait(false);
        }
        finally
        {
            _commandGate.Release();
        }
    }

    /// <summary>假设已持有 <see cref="_commandGate"/> 的命令实现（供文件传输在临界区内复用）。</summary>
    private async Task<string> SendCommandCoreAsync(string command, TimeSpan? timeout, CancellationToken ct)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(CommandTimeoutSeconds);
        var line = command.TrimEnd('\n').TrimEnd('\r');
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(effectiveTimeout);

        try
        {
            var output = await _frames.SendAsync(
                writeCommand: token =>
                {
                    token.ThrowIfCancellationRequested();
                    var payload = Encoding.UTF8.GetBytes(line + "\n");
                    _port.Write(payload, 0, payload.Length);
                    return Task.CompletedTask;
                },
                silentMs: SilentTimeoutMs,
                firstByteMs: FirstByteTimeoutMs,
                ct: linked.Token).ConfigureAwait(false);

            return CommandOutput.Sanitize(output);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 调用方未取消 → 是命令超时，统一为 DeviceChannelException
            throw new DeviceChannelException($"命令超时({effectiveTimeout.TotalSeconds:0.#}s): {line}");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            throw new DeviceChannelException($"串口读写失败: {ex.Message}", ex);
        }
    }

    public async Task TransferFileAsync(string localPath, string remotePath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        EnsureOpen();
        // 先探测（内部会走一次受闸门的命令），再独占闸门执行整段传输，
        // 避免传输过程中被另一条命令插入导致写入流被截断。
        var hasBase64 = await ProbeBase64Async(ct).ConfigureAwait(false);

        await _commandGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var transfer = new DeviceFileTransfer(
                sendCommand: (cmd, timeout, token) => SendCommandCoreAsync(cmd, timeout, token),
                writeLine: WriteLineAsync,
                hasBase64: hasBase64);
            await transfer.TransferFileAsync(localPath, remotePath, progress, ct).ConfigureAwait(false);
        }
        finally
        {
            _commandGate.Release();
        }
    }

    private bool? _hasBase64;

    /// <summary>探测设备端是否有 base64 命令（结果缓存）。</summary>
    private async Task<bool> ProbeBase64Async(CancellationToken ct)
    {
        if (_hasBase64 is bool cached) return cached;
        // 用退出码标记判定：回显为字面 "$?"，只有真实结果才会出现 "===0==="
        var output = await SendCommandAsync(
            "command -v base64 >/dev/null 2>&1; echo \"===$?===\"",
            TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
        _hasBase64 = output.Contains("===0===", StringComparison.Ordinal);
        return _hasBase64.Value;
    }

    private async Task WriteLineAsync(string line, CancellationToken ct)
    {
        var payload = Encoding.UTF8.GetBytes(line + "\n");
        _port.Write(payload, 0, payload.Length);
        // 串口 115200bps 传输约 10ms/KB，必须等 FIFO 排空再发下一行，否则命令被截断
        await Task.Delay(45, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 后台读取循环 —— 帧收集器的**唯一生产者**。
    /// 只把读到的字节交给收集器（由它打时间戳），帧结束由消费侧按"最后一段数据之后的静默"判定。
    /// 这里刻意<b>不</b>维护"数据静默"状态：生产者延迟翻转标志与消费侧的起算点不一致，
    /// 正是"首字节慢即被判无输出 + 响应串到下一条命令"的根因。
    /// </summary>
    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var n = _port.BytesToRead;
                if (n > 0)
                {
                    var buf = new byte[n];
                    _port.Read(buf, 0, n);
                    _frames.Append(Encoding.UTF8.GetString(buf));
                }
                else
                {
                    await Task.Delay(20, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
        catch
        {
            // 读取异常（端口关闭/拔出）→ 静默，后续命令将因 IsOpen=false 失败
        }
    }

    private void CancelReadLoop()
    {
        try { _readCts?.Cancel(); } catch { /* 已释放 */ }
        _readCts?.Dispose();
        _readCts = null;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new DeviceChannelException($"串口 {Name} 未打开");
    }

    public void Dispose()
    {
        CancelReadLoop();
        try { _port.Dispose(); } catch { /* 忽略 */ }
        _commandGate.Dispose();
    }
}
