using System.Net.Sockets;
using Hmi.Communication.Contracts;

namespace Hmi.Communication.Builtin;

/// <summary>
/// Modbus TCP 运行时驱动 —— 编辑器内真实连接 Modbus TCP 从站。
///
/// 协议实现（GB/T 19582 / Modbus Messaging Implementation Guide V1.0b）:
///   - MBAP 报文头: TransactionId(2) + ProtocolId(2)=0 + Length(2) + UnitId(1)
///   - 功能码: 0x03 读保持寄存器 / 0x06 写单个寄存器 / 0x10 写多寄存器
///            0x01 读线圈 / 0x05 写单个线圈
///   - 大端序，寄存器 16 位
///
/// 并发契约：一个 TCP 连接上同一时刻只允许一个事务（<c>SemaphoreSlim</c> 包住
/// "分配事务号 → 写请求 → 读完整响应"全过程）。此前锁只保护事务号分配、I/O 在锁外
/// 的 <c>Task.Run</c> 中执行，两个并发请求会在同一流上交错收发，响应张冠李戴。
///
/// 错误契约：连接/超时/协议错误/短帧统一抛 <see cref="CommunicationRuntimeException"/>，
/// 不把 <c>AggregateException</c> 或 <c>IndexOutOfRangeException</c> 泄漏给上层。
/// </summary>
public sealed class ModbusTcpRuntimeDriver : IRuntimeCommunicationDriver, IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private ushort _transactionId;
    private byte _unitId = 1;
    private int _timeoutMs = 3000;

    public bool IsConnected => _client?.Connected == true && _stream != null;

    /// <summary>日志输出。</summary>
    public Action<string>? Log { get; set; }

    /// <summary>连接超时（毫秒）。</summary>
    public int ConnectTimeoutMs { get; set; } = 3000;

    /// <summary>请求超时（毫秒）。</summary>
    public int TimeoutMs { get; set; } = 3000;

    public bool Connect(ConnectionConfig config, TimeSpan timeout)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        Disconnect();

        var p = config.Parameters;
        if (!p.TryGetValue("host", out var host) || string.IsNullOrWhiteSpace(host))
            throw new CommunicationRuntimeException("缺少连接参数: host");
        if (!p.TryGetValue("port", out var portStr) || !int.TryParse(portStr, out var port))
            port = 502;
        if (p.TryGetValue("slaveId", out var sid) && byte.TryParse(sid, out var slaveId))
            _unitId = slaveId;
        if (p.TryGetValue("timeoutMs", out var toStr) && int.TryParse(toStr, out var to))
        {
            _timeoutMs = to;
            TimeoutMs = to;
        }

        var effectiveTimeout = timeout > TimeSpan.Zero
            ? timeout
            : TimeSpan.FromMilliseconds(Math.Max(1, ConnectTimeoutMs));

        var client = new TcpClient();
        try
        {
            // 用 CancellationToken 而不是 connectTask.Wait(ms)：
            // 后者超时后底层连接任务仍在跑且异常无人观察，同时 Dispose 无法取消它。
            using var cts = new CancellationTokenSource(effectiveTimeout);
            client.ConnectAsync(host, port, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            client.Dispose();
            throw new CommunicationRuntimeException($"连接超时: {host}:{port}");
        }
        catch (Exception ex)
        {
            client.Dispose();
            throw new CommunicationRuntimeException($"连接失败 {host}:{port}: {ex.Message}", ex);
        }

        _client = client;
        _stream = client.GetStream();
        _stream.ReadTimeout = TimeoutMs;
        _stream.WriteTimeout = TimeoutMs;
        Log?.Invoke($"[ModbusTcp] 已连接 {host}:{port} (unit={_unitId})");
        return true;
    }

    public void Disconnect()
    {
        try { _stream?.Dispose(); } catch { /* 忽略 */ }
        try { _client?.Dispose(); } catch { /* 忽略 */ }
        _stream = null;
        _client = null;
        Log?.Invoke("[ModbusTcp] 已断开");
    }

    // ── 寄存器读写 ──

    public async Task<ushort[]> ReadHoldingRegistersAsync(
        ushort startAddress, ushort count, CancellationToken ct = default)
    {
        // 功能码 0x03: 请求 8 字节 (addr 2 + count 2)，响应 [0]=func, [1]=byteCount, [2..]=数据
        var response = await ExecuteFrameAsync(tid => BuildRequest(tid, 0x03, startAddress, count), ct)
            .ConfigureAwait(false);

        // 短帧必须报协议错误，而不是让 response[i] 抛 IndexOutOfRangeException
        var needed = 2 + count * 2;
        if (response.Length < needed)
            throw new CommunicationRuntimeException(
                $"0x03 响应长度不足: 期望 ≥{needed} 字节, 实际 {response.Length} 字节");

        var data = new ushort[count];
        for (int i = 0; i < count; i++)
            data[i] = (ushort)((response[2 + i * 2] << 8) | response[3 + i * 2]);
        return data;
    }

    public async Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken ct = default)
    {
        // 功能码 0x06: 请求 8 字节，响应回显请求（回显校验由 ExecuteFrameAsync 完成）
        await ExecuteFrameAsync(tid => BuildRequest(tid, 0x06, address, value), ct).ConfigureAwait(false);
    }

    public async Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, CancellationToken ct = default)
    {
        if (values == null || values.Length == 0) throw new ArgumentException("寄存器列表为空");
        if (values.Length > 123) throw new ArgumentException("单帧最多 123 个寄存器");

        // 功能码 0x10: addr(2) + count(2) + byteCount(1) + data(2*n)
        var body = new byte[6 + values.Length * 2];
        body[0] = (byte)(startAddress >> 8);
        body[1] = (byte)startAddress;
        body[2] = (byte)(values.Length >> 8);
        body[3] = (byte)values.Length;
        body[4] = (byte)(values.Length * 2);
        for (int i = 0; i < values.Length; i++)
        {
            body[5 + i * 2] = (byte)(values[i] >> 8);
            body[6 + i * 2] = (byte)values[i];
        }

        // 响应: addr(2) + count(2) = 8 字节帧
        await ExecuteFrameAsync(tid => BuildRequestWithBody(tid, 0x10, body), ct).ConfigureAwait(false);
    }

    // ── 线圈读写 ──

    public async Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, CancellationToken ct = default)
    {
        // 功能码 0x01: 响应 [0]=func, [1]=byteCount, [2..]=位打包
        var byteCount = (count + 7) / 8;
        var response = await ExecuteFrameAsync(tid => BuildRequest(tid, 0x01, startAddress, count), ct)
            .ConfigureAwait(false);

        var needed = 2 + byteCount;
        if (response.Length < needed)
            throw new CommunicationRuntimeException(
                $"0x01 响应长度不足: 期望 ≥{needed} 字节, 实际 {response.Length} 字节");

        var data = new bool[count];
        for (int i = 0; i < count; i++)
        {
            // 响应布局: [0]=func, [1]=byteCount, [2..]=位打包数据
            var byteIndex = 2 + i / 8;
            data[i] = (response[byteIndex] & (1 << (i % 8))) != 0;
        }
        return data;
    }

    public async Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken ct = default)
    {
        // 功能码 0x05: 0xFF00 = ON, 0x0000 = OFF
        await ExecuteFrameAsync(
            tid => BuildRequest(tid, 0x05, address, value ? (ushort)0xFF00 : (ushort)0x0000), ct)
            .ConfigureAwait(false);
    }

    // ── 报文编解码 ──

    /// <summary>构造标准请求：MBAP(7) + func(1) + addr(2) + value/count(2)。</summary>
    private byte[] BuildRequest(ushort tid, byte functionCode, ushort address, ushort value)
    {
        return
        [
            (byte)(tid >> 8), (byte)tid,                          // TransactionId
            0x00, 0x00,                                          // ProtocolId = 0
            0x00, 0x06,                                          // Length = 6 (unit + func + addr + value)
            _unitId,
            functionCode,
            (byte)(address >> 8), (byte)address,
            (byte)(value >> 8), (byte)value
        ];
    }

    /// <summary>构造带自定义体的请求（多寄存器写）。</summary>
    private byte[] BuildRequestWithBody(ushort tid, byte functionCode, byte[] body)
    {
        var frame = new byte[8 + body.Length];
        frame[0] = (byte)(tid >> 8);
        frame[1] = (byte)tid;
        frame[2] = 0x00;
        frame[3] = 0x00;
        frame[4] = (byte)((body.Length + 2) >> 8);
        frame[5] = (byte)(body.Length + 2);
        frame[6] = _unitId;
        frame[7] = functionCode;
        body.CopyTo(frame, 8);
        return frame;
    }

    /// <summary>
    /// 发送请求并读取响应帧（整段临界区串行化）。
    /// 校验 TransactionId/ProtocolId/UnitId 回显与功能码；
    /// 返回响应帧（不含 MBAP 头）。
    /// </summary>
    private async Task<byte[]> ExecuteFrameAsync(Func<ushort, byte[]> requestFactory, CancellationToken ct)
    {
        await _ioGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!IsConnected)
                throw new CommunicationRuntimeException("未连接设备");

            var tid = _transactionId++;
            var request = requestFactory(tid);

            // 异步 ReadAsync 不再受 Socket.ReadTimeout 约束，必须显式加超时，
            // 否则设备"接受连接但不响应"会让请求永久挂起。
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeoutMs);
            var ioToken = timeoutCts.Token;

            try
            {
                await _stream!.WriteAsync(request, ioToken).ConfigureAwait(false);
                await _stream.FlushAsync(ioToken).ConfigureAwait(false);

                // MBAP 头 7 字节
                var header = await ReadExactlyAsync(7, ioToken).ConfigureAwait(false);
                var respTid = (ushort)((header[0] << 8) | header[1]);
                var protocolId = (ushort)((header[2] << 8) | header[3]);
                var length = (ushort)((header[4] << 8) | header[5]);
                var respUnit = header[6];

                if (protocolId != 0)
                    throw new CommunicationRuntimeException($"协议标识错误: 0x{protocolId:X4}");
                if (respTid != tid)
                    throw new CommunicationRuntimeException($"事务号不匹配: 响应 0x{respTid:X4} != 请求 0x{tid:X4}");
                if (respUnit != _unitId)
                    throw new CommunicationRuntimeException($"单元标识不匹配: {respUnit} != {_unitId}");
                if (length < 2)
                    throw new CommunicationRuntimeException("响应帧长度非法");

                var body = await ReadExactlyAsync(length - 1, ioToken).ConfigureAwait(false); // 去掉 unit 字节
                var func = body[0];

                // 异常响应: 功能码最高位置 1
                if ((func & 0x80) != 0)
                {
                    var code = body.Length > 1 ? body[1] : 0;
                    throw new CommunicationRuntimeException($"Modbus 异常响应: 功能码 0x{func - 0x80:X2}, 异常码 {code}");
                }

                if (func != request[7])
                    throw new CommunicationRuntimeException($"功能码不匹配: 响应 0x{func:X2} != 请求 0x{request[7]:X2}");

                return body;
            }
            catch (CommunicationRuntimeException)
            {
                throw;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new CommunicationRuntimeException($"通讯超时({TimeoutMs}ms)");
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
            {
                throw new CommunicationRuntimeException($"通讯失败: {ex.Message}", ex);
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    /// <summary>精确读取指定字节数（支持取消）。读到 0 字节视为连接关闭。</summary>
    private async Task<byte[]> ReadExactlyAsync(int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await _stream!.ReadAsync(buffer.AsMemory(offset, count - offset), ct)
                .ConfigureAwait(false);
            if (read <= 0)
                throw new CommunicationRuntimeException("设备连接已关闭");
            offset += read;
        }
        return buffer;
    }

    public void Dispose()
    {
        Disconnect();
        _ioGate.Dispose();
    }
}
