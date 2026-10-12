using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Hmi.Communication.Builtin;
using Hmi.Communication.Contracts;
using Xunit;

namespace Hmi.Communication.Tests;

/// <summary>
/// Modbus TCP 运行时驱动单元测试 —— 使用回环 socket 模拟从站。
/// 验证 MBAP 报文编解码、寄存器读写、线圈读写、异常响应。
/// </summary>
public class ModbusTcpRuntimeDriverTests
{
    // ── 读保持寄存器 ──

    [Fact]
    public async Task ReadHoldingRegisters_ReturnsParsedValues()
    {
        // 从站响应: 0x03, byteCount=2, 寄存器值 0x1234, 0xABCD
        using var server = CreateServer(async (req, stream) =>
        {
            Assert.Equal(0x03, req[7]);
            var tid = (ushort)((req[0] << 8) | req[1]);
            await WriteFrameAsync(stream,
                BuildMbap(tid, 0x0000, 9, 0x01),
                [0x03, 0x04, 0x12, 0x34, 0xAB, 0xCD]);
        });

        using var driver = CreateConnectedDriver(server.Port);
        var values = await driver.ReadHoldingRegistersAsync(0, 2);

        Assert.Equal(2, values.Length);
        Assert.Equal(0x1234, values[0]);
        Assert.Equal(0xABCD, values[1]);
    }

    [Fact]
    public async Task ReadHoldingRegisters_SendsCorrectRequest()
    {
        byte[]? captured = null;
        using var server = CreateServer(async (req, stream) =>
        {
            captured = req;
            var tid = (ushort)((req[0] << 8) | req[1]);
            await WriteFrameAsync(stream,
                BuildMbap(tid, 0x0000, 7, 0x01),
                [0x03, 0x02, 0x00, 0x00]);
        });

        using var driver = CreateConnectedDriver(server.Port);
        await driver.ReadHoldingRegistersAsync(0x0100, 1);

        Assert.NotNull(captured);
        // 请求体: func=0x03, addr=0x0100, count=1
        Assert.Equal(0x03, captured![7]);
        Assert.Equal(0x01, captured[8]);
        Assert.Equal(0x00, captured[9]);
        Assert.Equal(0x00, captured[10]);
        Assert.Equal(0x01, captured[11]);
    }

    // ── 写寄存器 ──

    [Fact]
    public async Task WriteSingleRegister_Works()
    {
        using var server = CreateServer(async (req, stream) =>
        {
            Assert.Equal(0x06, req[7]);
            var tid = (ushort)((req[0] << 8) | req[1]);
            // 回显请求
            await WriteFrameAsync(stream,
                BuildMbap(tid, 0x0000, 8, 0x01), req[7..]);
        });

        using var driver = CreateConnectedDriver(server.Port);
        await driver.WriteSingleRegisterAsync(10, 0x0F0F);
    }

    [Fact]
    public async Task WriteMultipleRegisters_Works()
    {
        using var server = CreateServer(async (req, stream) =>
        {
            Assert.Equal(0x10, req[7]);
            var tid = (ushort)((req[0] << 8) | req[1]);
            // 回显 addr + count
            await WriteFrameAsync(stream,
                BuildMbap(tid, 0x0000, 8, 0x01),
                [0x10, req[8], req[9], req[10], req[11]]);
        });

        using var driver = CreateConnectedDriver(server.Port);
        await driver.WriteMultipleRegistersAsync(0, new ushort[] { 0x1111, 0x2222 });
    }

    // ── 线圈 ──

    [Fact]
    public async Task ReadCoils_UnpacksBits()
    {
        // 8 个线圈: 0x55 = 0101 0101 (func, byteCount=1, data)
        using var server = CreateServer(async (req, stream) =>
        {
            var tid = (ushort)((req[0] << 8) | req[1]);
            await WriteFrameAsync(stream,
                BuildMbap(tid, 0x0000, 8, 0x01),
                [0x01, 0x01, 0x55]);
        });

        using var driver = CreateConnectedDriver(server.Port);
        var coils = await driver.ReadCoilsAsync(0, 8);

        Assert.Equal(8, coils.Length);
        Assert.True(coils[0]);
        Assert.False(coils[1]);
        Assert.True(coils[2]);
        Assert.False(coils[3]);
    }

    [Fact]
    public async Task WriteSingleCoil_OnSendsFF00()
    {
        byte[]? captured = null;
        using var server = CreateServer(async (req, stream) =>
        {
            captured = req;
            var tid = (ushort)((req[0] << 8) | req[1]);
            await WriteFrameAsync(stream,
                BuildMbap(tid, 0x0000, 8, 0x01), req[7..]);
        });

        using var driver = CreateConnectedDriver(server.Port);
        await driver.WriteSingleCoilAsync(5, true);

        Assert.Equal(0x05, captured![7]);
        Assert.Equal(0xFF, captured[10]);
        Assert.Equal(0x00, captured[11]);
    }

    // ── 异常与校验 ──

    [Fact]
    public async Task ModbusExceptionResponse_Throws()
    {
        using var server = CreateServer(async (req, stream) =>
        {
            var tid = (ushort)((req[0] << 8) | req[1]);
            // 异常响应: 功能码 | 0x80, 异常码 2 (非法数据地址)
            await WriteFrameAsync(stream,
                BuildMbap(tid, 0x0000, 5, 0x01),
                [0x83, 0x02]);
        });

        using var driver = CreateConnectedDriver(server.Port);
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => driver.ReadHoldingRegistersAsync(0, 1));
        var runtimeEx = UnwrapCommunicationException(ex);
        Assert.NotNull(runtimeEx);
        Assert.Contains("异常码 2", runtimeEx!.Message);
    }

    [Fact]
    public async Task WrongUnitId_Throws()
    {
        using var server = CreateServer(async (req, stream) =>
        {
            var tid = (ushort)((req[0] << 8) | req[1]);
            await WriteFrameAsync(stream,
                BuildMbap(tid, 0x0000, 7, 0x02), // unit=0x02 != 请求的 0x01
                [0x03, 0x02, 0x00, 0x00]);
        });

        using var driver = CreateConnectedDriver(server.Port);
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => driver.ReadHoldingRegistersAsync(0, 1));
        Assert.NotNull(UnwrapCommunicationException(ex));
    }

    [Fact]
    public void Connect_ToClosedPort_Throws()
    {
        // 先建一个 server 拿到端口，关闭后再连接 → 端口不可用
        int closedPort;
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            closedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        var driver = new ModbusTcpRuntimeDriver { ConnectTimeoutMs = 1000 };
        Assert.Throws<CommunicationRuntimeException>(() =>
            driver.Connect(new ConnectionConfig("modbus-tcp", new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = closedPort.ToString()
            }), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task DisconnectedDriver_Read_Throws()
    {
        var driver = new ModbusTcpRuntimeDriver();
        Assert.False(driver.IsConnected);
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => driver.ReadHoldingRegistersAsync(0, 1));
        Assert.NotNull(UnwrapCommunicationException(ex));
    }

    // ── 短帧 / 超时 / 并发（2026-09-13 审查 P1-5 回归） ──

    [Fact]
    public async Task ReadHoldingRegisters_ShortFrame_ThrowsProtocolError()
    {
        // 声明 2 个寄存器却只回 0 字节数据 → 必须是协议错误，而不是 IndexOutOfRangeException
        using var server = CreateServer(async (req, stream) =>
        {
            var tid = (ushort)((req[0] << 8) | req[1]);
            await WriteFrameAsync(stream, BuildMbap(tid, 0x0000, 0, 0x01), [0x03, 0x04]);
        });

        using var driver = CreateConnectedDriver(server.Port);
        var ex = await Assert.ThrowsAsync<CommunicationRuntimeException>(
            () => driver.ReadHoldingRegistersAsync(0, 2));
        Assert.Contains("长度不足", ex.Message);
    }

    [Fact]
    public async Task ReadCoils_ShortFrame_ThrowsProtocolError()
    {
        using var server = CreateServer(async (req, stream) =>
        {
            var tid = (ushort)((req[0] << 8) | req[1]);
            await WriteFrameAsync(stream, BuildMbap(tid, 0x0000, 0, 0x01), [0x01, 0x02]);
        });

        using var driver = CreateConnectedDriver(server.Port);
        var ex = await Assert.ThrowsAsync<CommunicationRuntimeException>(
            () => driver.ReadCoilsAsync(0, 16));
        Assert.Contains("长度不足", ex.Message);
    }

    [Fact]
    public async Task UnresponsiveDevice_TimesOut_AsCommunicationRuntimeException()
    {
        // 假从站接受连接但永不回帧：必须在超时后报错，而不是永久挂起
        using var server = CreateServer((req, stream) => Task.CompletedTask);

        using var driver = CreateConnectedDriver(server.Port);
        driver.TimeoutMs = 300;

        var ex = await Assert.ThrowsAsync<CommunicationRuntimeException>(
            () => driver.ReadHoldingRegistersAsync(0, 1));
        Assert.Contains("超时", ex.Message);
    }

    [Fact]
    public async Task ConcurrentRequests_DoNotCrossResponses()
    {
        // 把请求地址原样回填为寄存器值，响应延迟 50ms 放大交错窗口
        using var server = CreateServer(async (req, stream) =>
        {
            var tid = (ushort)((req[0] << 8) | req[1]);
            var addr = (ushort)((req[8] << 8) | req[9]);
            await Task.Delay(50);
            await WriteFrameAsync(stream, BuildMbap(tid, 0x0000, 0, 0x01),
                [0x03, 0x02, (byte)(addr >> 8), (byte)addr]);
        });

        using var driver = CreateConnectedDriver(server.Port);
        var t1 = driver.ReadHoldingRegistersAsync(0x0011, 1);
        var t2 = driver.ReadHoldingRegistersAsync(0x0022, 1);
        var results = await Task.WhenAll(t1, t2);

        Assert.Equal((ushort)0x0011, results[0][0]);
        Assert.Equal((ushort)0x0022, results[1][0]);
    }

    [Fact]
    public async Task ConcurrentRequests_AreSerialized()
    {
        // 从站对每个请求并发处理并各自延迟 150ms（模拟真实并发从站）：
        //   整帧串行化 → 客户端一次只发一个 → 总耗时 ≈300ms；
        //   若并发交错（旧实现）→ 两个延迟重叠 → ≈150ms。
        using var server = CreateServer((req, stream) =>
        {
            _ = Task.Run(async () =>
            {
                var tid = (ushort)((req[0] << 8) | req[1]);
                await Task.Delay(150);
                await WriteFrameAsync(stream, BuildMbap(tid, 0x0000, 0, 0x01),
                    [0x03, 0x02, 0x00, 0x01]);
            });
            return Task.CompletedTask;
        });

        using var driver = CreateConnectedDriver(server.Port);

        // 先**单独**量一次（同一台并发从站、同样的 150ms 延迟）作为基准。
        var one = Stopwatch.StartNew();
        await driver.ReadHoldingRegistersAsync(0, 1);
        one.Stop();

        var sw = Stopwatch.StartNew();
        await Task.WhenAll(
            driver.ReadHoldingRegistersAsync(0, 1),
            driver.ReadHoldingRegistersAsync(1, 1));
        sw.Stop();

        // 🩸 阶段 123：原判据是 `sw >= 250`（绝对毫秒）—— 负载会把**交错**实现也推过下界
        //   ⇒ 假绿。改成**比值**：整帧串行化 ⇒ 两个并发 ≈ 2×单个；交错 ⇒ ≈ 1×。
        //   负载同时抬高分子与分母，比值基本不动。
        Assert.True(sw.Elapsed >= one.Elapsed * 1.6,
            $"并发请求未被整帧串行化（单个 {one.ElapsedMilliseconds}ms，两个并发 {sw.ElapsedMilliseconds}ms，应 ≥1.6×）");
    }

    // ── 辅助 ──

    /// <summary>解包 Task 异常层级，定位 CommunicationRuntimeException。</summary>
    private static CommunicationRuntimeException? UnwrapCommunicationException(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            if (current is CommunicationRuntimeException cre) return cre;
            current = current.InnerException;
        }
        return null;
    }

    // ── 辅助 ──

    /// <summary>启动回环 TCP 从站模拟器。</summary>
    private static LoopbackServer CreateServer(Func<byte[], NetworkStream, Task> handler)
    {
        var server = new LoopbackServer(handler);
        server.Start();
        return server;
    }

    /// <summary>连接回环从站并返回已连接驱动。</summary>
    /// <summary>建立并连接驱动。窗口刻意给得很宽（见下方注释），需要短窗口的负例自行覆写。</summary>
    private static ModbusTcpRuntimeDriver CreateConnectedDriver(int port)
    {
        // 判据靠相对大小成立，不靠绝对余量：回环从站在全量门禁（7 个测试宿主并行）下
        // 会出现秒级调度延迟，原先 2 s 的响应窗口被负载吃掉（2026-09-16 门禁实测：
        // ReadCoils_UnpacksBits 报「通讯超时(2000ms)」，同文件单跑 15/15 绿 / 4 s）。
        // 正例窗口统一拉到 10 s；需要「必须在超时后报错」的负例各自显式覆写更短的值
        // （UnresponsiveDevice_TimesOut_… 设 300 ms），因此扩窗不影响任何负例判据。
        var driver = new ModbusTcpRuntimeDriver { ConnectTimeoutMs = 10000, TimeoutMs = 10000 };
        driver.Connect(new ConnectionConfig("modbus-tcp", new Dictionary<string, string>
        {
            ["host"] = "127.0.0.1",
            ["port"] = port.ToString(),
            ["slaveId"] = "1"
        }), TimeSpan.FromSeconds(10));
        return driver;
    }

    /// <summary>构造 MBAP 头 + 体。</summary>
    private static byte[] BuildMbap(ushort tid, ushort pid, ushort length, byte unit) =>
        [(byte)(tid >> 8), (byte)tid, (byte)(pid >> 8), (byte)pid,
         (byte)(length >> 8), (byte)length, unit];

    /// <summary>写完整 MBAP 帧（含长度字段补全）。</summary>
    private static async Task WriteFrameAsync(NetworkStream stream, byte[] mbap, byte[] body)
    {
        // mbap[4..6] 是长度 —— 修正为 body.Length + 1 (unit)
        mbap[4] = (byte)((body.Length + 1) >> 8);
        mbap[5] = (byte)(body.Length + 1);
        await stream.WriteAsync(mbap);
        await stream.WriteAsync(body);
        await stream.FlushAsync();
    }

    /// <summary>简易回环 Modbus TCP 从站。</summary>
    private sealed class LoopbackServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<byte[], NetworkStream, Task> _handler;
        private readonly CancellationTokenSource _cts = new();
        private Task? _acceptTask;
        private readonly List<TcpClient> _clients = new();

        public int Port { get; }

        public LoopbackServer(Func<byte[], NetworkStream, Task> handler)
        {
            _handler = handler;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        public void Start()
        {
            _acceptTask = AcceptLoopAsync(_cts.Token);
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException) { return; }
                catch (SocketException) { return; }

                _clients.Add(client);
                _ = HandleClientAsync(client, ct);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            try
            {
                var stream = client.GetStream();
                while (!ct.IsCancellationRequested)
                {
                    // 读 MBAP 头 7 字节
                    var header = new byte[7];
                    var read = await ReadExactlyAsync(stream, header, ct);
                    if (read < 7) return;

                    // Length 字段 = 1(unit) + PDU 字节数 → PDU = length - 1
                    var length = (header[4] << 8) | header[5];
                    if (length < 2) return;

                    // 读 PDU（func + data），不含 unit
                    var body = new byte[length - 1];
                    await ReadExactlyAsync(stream, body, ct);

                    // 重建完整请求: header(7, 含 unit) + PDU
                    var request = new byte[7 + body.Length];
                    header.CopyTo(request, 0);
                    body.CopyTo(request, 7);

                    // 重要: 模拟器必须回帧。若 handler 抛异常则断开，客户端超时。
                    try
                    {
                        await _handler(request, stream);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[LoopbackServer] handler error: {ex.Message}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LoopbackServer] connection error: {ex.Message}");
            }
        }

        private static async Task<int> ReadExactlyAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
                if (read <= 0) break;
                offset += read;
            }
            return offset;
        }

        public void Dispose()
        {
            _cts.Cancel();
            foreach (var c in _clients) c.Dispose();
            _listener.Stop();
            _acceptTask?.Wait(100);
        }
    }
}
