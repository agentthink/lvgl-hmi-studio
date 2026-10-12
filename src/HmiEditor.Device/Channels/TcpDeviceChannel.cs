using System.Net.Sockets;
using System.Text;

namespace HmiEditor.Device.Channels;

/// <summary>
/// 以太网 TCP 通道 —— 通过设备端 telnetd/调试服务（默认端口 23）交互。
/// 命令遵循 busybox shell 行协议；文件传输使用 base64 + heredoc 分块（与串口通道一致）。
///
/// 并发契约：<see cref="SendCommandAsync"/> 与 <see cref="TransferFileAsync"/> 整帧串行化
/// （<c>SemaphoreSlim</c>），因为"开帧 → 写入 → 等本帧结束"必须是一个不可分割的临界区，
/// 否则并发命令会在同一 socket 上交错收发，导致响应张冠李戴。
/// 收帧判据本身只有一份实现 —— <see cref="CommandFrameCollector"/>，与串口通道共用。
/// 调用方取消（<paramref name="ct"/>）原样抛出 <see cref="OperationCanceledException"/>；
/// 仅"等待超时"转换为 <see cref="DeviceChannelException"/>，便于上层区分"用户取消"与"设备无响应"。
/// </summary>
public sealed class TcpDeviceChannel : IDeviceChannel
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly CommandFrameCollector _frames = new();

    /// <summary>整帧串行化闸门（非重入）：命令与文件传输共用。</summary>
    private readonly SemaphoreSlim _commandGate = new(1, 1);

    private CancellationTokenSource? _readCts;

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

    public string Kind => "ethernet";

    public string Name => $"{_host}:{_port}";

    public bool IsOpen => _stream is not null && _client is not null;

    public TcpDeviceChannel(string host, int port = 23)
    {
        _host = host;
        _port = port;
    }

    public async Task OpenAsync(CancellationToken ct = default)
    {
        if (IsOpen) return;
        try
        {
            _client = new TcpClient();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(8));
            await _client.ConnectAsync(_host, _port, linked.Token).ConfigureAwait(false);
            _client.NoDelay = true;
            _stream = _client.GetStream();
            _stream.ReadTimeout = 500;

            // 读循环必须可取消：半开连接会让 ReadAsync 永久阻塞，
            // 之前没有 CancellationToken，CloseAsync 也无法终止它。
            _readCts?.Dispose();
            _readCts = new CancellationTokenSource();
            _ = ReadLoopAsync(_readCts.Token);
        }
        catch (Exception ex) when (ex is not DeviceChannelException)
        {
            throw new DeviceChannelException($"连接设备 {Name} 失败: {ex.Message}", ex);
        }
    }

    public Task CloseAsync()
    {
        CancelReadLoop();
        try { _client?.Close(); } catch { /* 关闭失败忽略 */ }
        _client = null;
        _stream = null;
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
                writeCommand: async token =>
                {
                    var payload = Encoding.UTF8.GetBytes(line + "\n");
                    await _stream!.WriteAsync(payload, token).ConfigureAwait(false);
                    await _stream.FlushAsync(token).ConfigureAwait(false);
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
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            throw new DeviceChannelException($"网络读写失败: {ex.Message}", ex);
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
        await _stream!.WriteAsync(payload, ct).ConfigureAwait(false);
        await Task.Delay(20, ct).ConfigureAwait(false); // 避免设备端 shell 处理不过来
    }

    /// <summary>
    /// 后台读取循环 —— 帧收集器的**唯一生产者**。
    /// 只把读到的字节交给收集器（由它打时间戳），帧结束由消费侧按"最后一段数据之后的静默"判定。
    /// 这里刻意<b>不</b>维护"数据静默"状态：生产者延迟翻转标志与消费侧的起算点不一致，
    /// 正是"首字节慢即被判无输出 + 响应串到下一条命令"的根因。
    /// </summary>
    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buf = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested && _stream is { } stream)
            {
                var n = await stream.ReadAsync(buf, ct).ConfigureAwait(false);
                if (n == 0)
                {
                    await Task.Delay(50, ct).ConfigureAwait(false);
                    continue;
                }
                _frames.Append(Encoding.UTF8.GetString(buf, 0, n));
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
        catch
        {
            // 连接断开 → 静默，命令将因 IsOpen=false 失败
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
        if (_stream is null || _client is null)
            throw new DeviceChannelException($"设备连接 {Name} 未建立");
    }

    public void Dispose()
    {
        CancelReadLoop();
        try { _client?.Close(); } catch { /* 忽略 */ }
        _client = null;
        _stream = null;
        _commandGate.Dispose();
    }
}
