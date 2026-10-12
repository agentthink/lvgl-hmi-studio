using System.IO.Pipelines;
using Hmi.Communication.Builtin;
using Hmi.Communication.Contracts;
using Xunit;

namespace Hmi.Communication.Tests;

/// <summary>
/// Modbus RTU 运行时驱动单元测试 —— 使用内存管道模拟从站。
/// 验证帧编解码、CRC16 校验、寄存器/线圈读写、异常响应。
/// </summary>
public class ModbusRtuRuntimeDriverTests
{
    // ── CRC16 ──

    [Fact]
    public void ComputeCrc16_MatchesKnownVector()
    {
        // 标准测试向量: 01 03 00 00 00 01 → CRC16 = 0x0A84 (线上字节 84 0A)
        var data = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01 };
        var crc = ModbusRtuRuntimeDriver.ComputeCrc16(data);
        Assert.Equal(0x0A84, crc);
    }

    [Fact]
    public void ComputeCrc16_EmptyData_ReturnsFFFF()
    {
        Assert.Equal(0xFFFF, ModbusRtuRuntimeDriver.ComputeCrc16(ReadOnlySpan<byte>.Empty));
    }

    // ── 读保持寄存器 ──

    [Fact]
    public async Task ReadHoldingRegisters_ParsesValues_AndValidatesCrc()
    {
        var (server, driver) = CreateLoopback(async (req, responder) =>
        {
            Assert.Equal(0x03, req[1]);
            // 响应: slave + func + byteCount + 2 regs + CRC
            var pdu = new byte[] { 0x03, 0x04, 0x12, 0x34, 0xAB, 0xCD };
            await responder.RespondAsync(0x01, pdu);
        });

        var values = await driver.ReadHoldingRegistersAsync(0, 2);
        Assert.Equal(2, values.Length);
        Assert.Equal(0x1234, values[0]);
        Assert.Equal(0xABCD, values[1]);
    }

    [Fact]
    public async Task ReadHoldingRegisters_SendsCorrectFrame()
    {
        byte[]? captured = null;
        var (server, driver) = CreateLoopback(async (req, responder) =>
        {
            captured = req;
            await responder.RespondAsync(0x01, new byte[] { 0x03, 0x02, 0x00, 0x00 });
        });

        await driver.ReadHoldingRegistersAsync(0x0100, 1);

        // 帧: slave + func + addr + count + CRC
        Assert.Equal(0x01, captured![0]);
        Assert.Equal(0x03, captured[1]);
        Assert.Equal(0x01, captured[2]);
        Assert.Equal(0x00, captured[3]);
        Assert.Equal(0x00, captured[4]);
        Assert.Equal(0x01, captured[5]);
        Assert.Equal(8, captured.Length); // 1+1+2+2+2
    }

    // ── 写寄存器 ──

    [Fact]
    public async Task WriteSingleRegister_Works()
    {
        var (server, driver) = CreateLoopback(async (req, responder) =>
        {
            Assert.Equal(0x06, req[1]);
            await responder.RespondAsync(0x01, req[1..]);
        });

        await driver.WriteSingleRegisterAsync(10, 0x0F0F);
    }

    [Fact]
    public async Task WriteMultipleRegisters_Works()
    {
        var (server, driver) = CreateLoopback(async (req, responder) =>
        {
            Assert.Equal(0x10, req[1]);
            // 回显 addr + count
            await responder.RespondAsync(0x01, new byte[] { 0x10, req[2], req[3], req[4], req[5] });
        });

        await driver.WriteMultipleRegistersAsync(0, new ushort[] { 0x1111, 0x2222 });
    }

    // ── 线圈 ──

    [Fact]
    public async Task ReadCoils_UnpacksBits()
    {
        var (server, driver) = CreateLoopback(async (req, responder) =>
        {
            Assert.Equal(0x01, req[1]);
            await responder.RespondAsync(0x01, new byte[] { 0x01, 0x01, 0x55 });
        });

        var coils = await driver.ReadCoilsAsync(0, 8);
        Assert.True(coils[0]);
        Assert.False(coils[1]);
        Assert.True(coils[2]);
        Assert.False(coils[3]);
    }

    [Fact]
    public async Task WriteSingleCoil_OnSendsFF00()
    {
        byte[]? captured = null;
        var (server, driver) = CreateLoopback(async (req, responder) =>
        {
            captured = req;
            await responder.RespondAsync(0x01, req[1..]);
        });

        await driver.WriteSingleCoilAsync(5, true);

        Assert.Equal(0x05, captured![1]);
        Assert.Equal(0xFF, captured[4]);
        Assert.Equal(0x00, captured[5]);
    }

    // ── 异常与校验 ──

    [Fact]
    public async Task CrcMismatch_Throws()
    {
        var (server, driver) = CreateLoopback(async (req, responder) =>
        {
            // 故意发错误 CRC
            await responder.RespondRawAsync(new byte[] { 0x01, 0x03, 0x02, 0x00, 0x00, 0x00, 0x00 });
        });

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => driver.ReadHoldingRegistersAsync(0, 1));
        var runtimeEx = UnwrapCommunicationException(ex);
        Assert.NotNull(runtimeEx);
        Assert.Contains("CRC", runtimeEx!.Message);
    }

    [Fact]
    public async Task ModbusExceptionResponse_Throws()
    {
        var (server, driver) = CreateLoopback(async (req, responder) =>
        {
            // 异常响应: func|0x80, 异常码 2
            await responder.RespondAsync(0x01, new byte[] { 0x83, 0x02 });
        });

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => driver.ReadHoldingRegistersAsync(0, 1));
        var runtimeEx = UnwrapCommunicationException(ex);
        Assert.NotNull(runtimeEx);
        Assert.Contains("异常码 2", runtimeEx!.Message);
    }

    [Fact]
    public async Task WrongSlaveId_Throws()
    {
        var (server, driver) = CreateLoopback(async (req, responder) =>
        {
            // 响应用错 slaveId
            await responder.RespondAsync(0x02, new byte[] { 0x03, 0x02, 0x00, 0x00 });
        });

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => driver.ReadHoldingRegistersAsync(0, 1));
        Assert.NotNull(UnwrapCommunicationException(ex));
    }

    [Fact]
    public async Task ShortButCrcValidFrame_ThrowsCommunicationError_NotIndexOutOfRange()
    {
        // 阶段 120：`ExecuteFrameAsync` 的 `expectedFrameLength` 曾经**被收下却没用**。
        // 后果：一个 CRC 合法但偏短的帧会让调用方 `response[2 + i*2]` 抛裸
        // IndexOutOfRangeException，破坏「本驱动只抛 CommunicationRuntimeException」的契约
        // （TCP 版在调用点有这条检查，RTU 版是分叉出来的复制品）。
        var (server, driver) = CreateLoopback(async (req, responder) =>
        {
            // 请求 2 个寄存器（期望整帧 9 字节），却只回 1 个（整帧 7 字节）、CRC 合法
            await responder.RespondAsync(0x01, new byte[] { 0x03, 0x02, 0x00, 0x00 });
        });

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => driver.ReadHoldingRegistersAsync(0, 2));
        var runtimeEx = UnwrapCommunicationException(ex);
        Assert.NotNull(runtimeEx);                    // 旧实现这里是 null（抛的是 IndexOutOfRange）
        Assert.Contains("长度不足", runtimeEx!.Message);
    }

    [Fact]
    public void NotConnected_Throws()
    {
        var transport = new MemoryPipeTransport();
        var driver = new ModbusRtuRuntimeDriver(transport);
        Assert.False(driver.IsConnected);
    }

    [Fact]
    public void Connect_WithConfig_SetsSlaveId()
    {
        var transport = new MemoryPipeTransport();
        transport.Open();
        var driver = new ModbusRtuRuntimeDriver(transport);
        driver.Connect(new ConnectionConfig("modbus-rtu", new Dictionary<string, string>
        {
            ["slaveId"] = "5"
        }), TimeSpan.FromSeconds(1));

        Assert.True(driver.IsConnected);
        driver.Disconnect();
        Assert.False(driver.IsConnected);
    }

    // ── 辅助 ──

    private static (MemoryPipeTransport, ModbusRtuRuntimeDriver) CreateLoopback(
        Func<byte[], Responder, Task> handler)
    {
        var transport = new MemoryPipeTransport();
        transport.Open();

        // 从站模拟器: 循环读取请求 → 回调 handler → 发送响应
        _ = Task.Run(async () =>
        {
            var responder = new Responder(transport);
            while (transport.IsOpen)
            {
                var frame = await transport.ReceiveFromClientAsync(TimeSpan.FromSeconds(2));
                if (frame.Length == 0) break;
                try
                {
                    await handler(frame, responder);
                }
                catch (Exception)
                {
                    break; // 测试断言失败时静默结束
                }
            }
        });

        var driver = new ModbusRtuRuntimeDriver(transport);
        driver.Connect(new ConnectionConfig("modbus-rtu", new Dictionary<string, string>
        {
            ["slaveId"] = "1"
        }), TimeSpan.FromSeconds(1));

        return (transport, driver);
    }

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

    /// <summary>从站响应器 —— 构造带 CRC 的 RTU 帧。</summary>
    private sealed class Responder
    {
        private readonly MemoryPipeTransport _transport;

        public Responder(MemoryPipeTransport transport) => _transport = transport;

        /// <summary>响应: slave + pdu + CRC16。</summary>
        public Task RespondAsync(byte slaveId, byte[] pdu)
        {
            var frame = new byte[1 + pdu.Length + 2];
            frame[0] = slaveId;
            pdu.CopyTo(frame, 1);
            var crc = ModbusRtuRuntimeDriver.ComputeCrc16(frame.AsSpan(0, 1 + pdu.Length));
            frame[^2] = (byte)(crc & 0xFF);
            frame[^1] = (byte)(crc >> 8);
            return _transport.SendToClientAsync(frame);
        }

        /// <summary>原始字节响应（用于 CRC 错误测试）。</summary>
        public Task RespondRawAsync(byte[] frame) => _transport.SendToClientAsync(frame);
    }
}

/// <summary>内存管道 RTU 传输 —— 客户端/从站双端内存队列。</summary>
public sealed class MemoryPipeTransport : IRtuFrameTransport
{
    private readonly Pipe _clientToServer = new();
    private readonly Pipe _serverToClient = new();
    private bool _open;

    public bool IsOpen => _open;

    public bool Open()
    {
        _open = true;
        return true;
    }

    public void Close() => _open = false;

    // 客户端侧（驱动使用）
    public async Task SendBytesAsync(byte[] data, CancellationToken ct = default)
    {
        var writer = _clientToServer.Writer;
        await writer.WriteAsync(data, ct);
        await writer.FlushAsync(ct);
    }

    public async Task<byte[]> ReceiveFrameAsync(CancellationToken ct = default)
    {
        var reader = _serverToClient.Reader;
        var result = await reader.ReadAsync(ct);
        var buffer = System.Buffers.BuffersExtensions.ToArray(result.Buffer);
        reader.AdvanceTo(result.Buffer.End);
        return buffer;
    }

    // 从站侧（测试模拟器使用）
    public async Task<byte[]> ReceiveFromClientAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var reader = _clientToServer.Reader;
            var result = await reader.ReadAsync(cts.Token);
            var buffer = System.Buffers.BuffersExtensions.ToArray(result.Buffer);
            reader.AdvanceTo(result.Buffer.End);
            return buffer;
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<byte>();
        }
    }

    public Task SendToClientAsync(byte[] data)
    {
        var writer = _serverToClient.Writer;
        return writer.WriteAsync(data).AsTask();
    }

    public void Dispose() => Close();
}
