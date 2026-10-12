using Hmi.Communication.Contracts;

namespace Hmi.Communication.Builtin;

/// <summary>
/// Modbus RTU 运行时驱动 —— 通过串口（RS-232/485）连接 Modbus RTU 从站。
///
/// 帧格式（GB/T 19582 / Modbus Serial Line Protocol）:
///   [SlaveId(1)] [FunctionCode(1)] [Data(N)] [CRC16 低字节] [CRC16 高字节]
///
/// 功能码与 TCP 驱动一致（PDU 相同）:
///   0x03 读保持寄存器 / 0x06 写单个寄存器 / 0x10 写多寄存器
///   0x01 读线圈 / 0x05 写单个线圈
///
/// 设计要点:
///   - 传输层抽象 (IRtuFrameTransport): 生产用串口, 测试用内存管道
///   - CRC16-Modbus (多项式 0xA001, 初始 0xFFFF) 校验
///   - 从站地址校验、异常响应解析
/// </summary>
public sealed class ModbusRtuRuntimeDriver : IRuntimeCommunicationDriver, IDisposable
{
    private readonly IRtuFrameTransport _transport;
    private byte _slaveId = 1;

    /// <summary>日志输出。</summary>
    public Action<string>? Log { get; set; }

    public ModbusRtuRuntimeDriver(IRtuFrameTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public bool IsConnected => _transport.IsOpen;

    public bool Connect(ConnectionConfig config, TimeSpan timeout)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        var p = config.Parameters;
        if (p.TryGetValue("slaveId", out var sid) && byte.TryParse(sid, out var slaveId))
            _slaveId = slaveId;

        try
        {
            var opened = _transport.Open();
            if (opened)
                Log?.Invoke($"[ModbusRtu] 串口已打开 (slave={_slaveId})");
            return opened;
        }
        catch (Exception ex)
        {
            throw new CommunicationRuntimeException($"串口打开失败: {ex.Message}", ex);
        }
    }

    public void Disconnect()
    {
        _transport.Close();
        Log?.Invoke("[ModbusRtu] 串口已关闭");
    }

    // ── 寄存器读写 ──

    public async Task<ushort[]> ReadHoldingRegistersAsync(
        ushort startAddress, ushort count, CancellationToken ct = default)
    {
        // 请求: addr(2) + count(2)，响应 PDU: [0]=func, [1]=byteCount, [2..]=数据
        var request = BuildFrame(0x03, startAddress, count);
        var response = await ExecuteFrameAsync(request, 5 + count * 2, ct);

        var data = new ushort[count];
        for (int i = 0; i < count; i++)
            data[i] = (ushort)((response[2 + i * 2] << 8) | response[3 + i * 2]);
        return data;
    }

    public async Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken ct = default)
    {
        var request = BuildFrame(0x06, address, value);
        await ExecuteFrameAsync(request, 8, ct);
    }

    public async Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, CancellationToken ct = default)
    {
        if (values == null || values.Length == 0) throw new ArgumentException("寄存器列表为空");
        if (values.Length > 123) throw new ArgumentException("单帧最多 123 个寄存器");

        // body: addr(2) + count(2) + byteCount(1) + data(2*n)
        var body = new byte[5 + values.Length * 2];
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

        var request = BuildFrameWithBody(0x10, body);
        await ExecuteFrameAsync(request, 8, ct);
    }

    // ── 线圈读写 ──

    public async Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, CancellationToken ct = default)
    {
        var request = BuildFrame(0x01, startAddress, count);
        var byteCount = (count + 7) / 8;
        var response = await ExecuteFrameAsync(request, 5 + byteCount, ct);

        var data = new bool[count];
        for (int i = 0; i < count; i++)
            // 响应 PDU: [0]=func, [1]=byteCount, [2..]=位打包数据
            data[i] = (response[2 + i / 8] & (1 << (i % 8))) != 0;
        return data;
    }

    public async Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken ct = default)
    {
        var request = BuildFrame(0x05, address, value ? (ushort)0xFF00 : (ushort)0x0000);
        await ExecuteFrameAsync(request, 8, ct);
    }

    // ── 帧编解码 ──

    /// <summary>构造标准 RTU 帧：slave + func + addr(2) + value(2) + CRC16。</summary>
    private byte[] BuildFrame(byte functionCode, ushort address, ushort value)
    {
        var pdu = new byte[5];
        pdu[0] = functionCode;
        pdu[1] = (byte)(address >> 8);
        pdu[2] = (byte)address;
        pdu[3] = (byte)(value >> 8);
        pdu[4] = (byte)value;
        return WrapFrame(pdu);
    }

    /// <summary>构造带自定义体的 RTU 帧（多寄存器写）。</summary>
    private byte[] BuildFrameWithBody(byte functionCode, byte[] body)
    {
        var pdu = new byte[1 + body.Length];
        pdu[0] = functionCode;
        body.CopyTo(pdu, 1);
        return WrapFrame(pdu);
    }

    /// <summary>组装完整帧: slave + PDU + CRC16(低,高)。</summary>
    private byte[] WrapFrame(byte[] pdu)
    {
        var frame = new byte[1 + pdu.Length + 2];
        frame[0] = _slaveId;
        pdu.CopyTo(frame, 1);
        var crc = ComputeCrc16(frame.AsSpan(0, 1 + pdu.Length));
        frame[^2] = (byte)(crc & 0xFF);
        frame[^1] = (byte)(crc >> 8);
        return frame;
    }

    /// <summary>
    /// 发送请求并读取响应帧。
    /// 校验从站地址、功能码回显、CRC16、**整帧长度**。
    /// 返回响应 PDU（func + data，不含 slave/CRC）。
    /// </summary>
    /// <param name="expectedFrameLength">
    /// 期望的**整帧**长度（unit + PDU + CRC，如 fn3 读 n 个寄存器 = 5 + 2n）。
    /// ⚠ 这个参数曾经被收下却**从未使用**：一个 CRC 合法但偏短的帧会让调用方
    /// `response[2 + i*2]` 抛裸 <see cref="IndexOutOfRangeException"/>，
    /// 破坏本驱动「只抛 <see cref="CommunicationRuntimeException"/>」的契约
    /// （TCP 版在调用点有这条检查，RTU 版是分叉出来的复制品 —— 阶段 120 修复）。
    /// </param>
    private async Task<byte[]> ExecuteFrameAsync(byte[] request, int expectedFrameLength, CancellationToken ct)
    {
        if (!IsConnected)
            throw new CommunicationRuntimeException("未连接设备");

        await _transport.SendBytesAsync(request, ct);
        var frame = await _transport.ReceiveFrameAsync(ct);

        if (frame.Length < 5)
            throw new CommunicationRuntimeException($"响应帧过短: {frame.Length} 字节");

        // 从站地址校验
        if (frame[0] != _slaveId)
            throw new CommunicationRuntimeException($"从站地址不匹配: {frame[0]} != {_slaveId}");

        // CRC16 校验
        var receivedCrc = (ushort)(frame[^1] << 8 | frame[^2]);
        var computedCrc = ComputeCrc16(frame.AsSpan(0, frame.Length - 2));
        if (receivedCrc != computedCrc)
            throw new CommunicationRuntimeException($"CRC 校验失败: 收到 0x{receivedCrc:X4} != 计算 0x{computedCrc:X4}");

        // PDU = frame[1..^2]
        var pdu = frame.AsSpan(1, frame.Length - 3).ToArray();
        var func = pdu[0];

        // 异常响应: 功能码最高位置 1（长度校验要放在它**之后**，否则 5 字节的异常帧会被误判成"太短"）
        if ((func & 0x80) != 0)
        {
            var code = pdu.Length > 1 ? pdu[1] : 0;
            throw new CommunicationRuntimeException($"Modbus 异常响应: 功能码 0x{func - 0x80:X2}, 异常码 {code}");
        }

        if (func != request[1])
            throw new CommunicationRuntimeException($"功能码不匹配: 响应 0x{func:X2} != 请求 0x{request[1]:X2}");

        if (frame.Length < expectedFrameLength)
            throw new CommunicationRuntimeException(
                $"响应帧长度不足: 期望 {expectedFrameLength} 字节, 实际 {frame.Length} 字节");

        return pdu;
    }

    /// <summary>
    /// CRC16-Modbus 计算（多项式 0xA001，初始 0xFFFF）。
    /// </summary>
    public static ushort ComputeCrc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 1) != 0)
                    crc = (ushort)((crc >> 1) ^ 0xA001);
                else
                    crc >>= 1;
            }
        }
        return crc;
    }

    public void Dispose() => Disconnect();
}
