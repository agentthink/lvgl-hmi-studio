using System.Collections.Concurrent;
using System.Threading;

namespace HmiEditor.Simulator;

/// <summary>
/// 通讯模拟器 —— 虚拟 Modbus 从站。
///
/// 模拟 PLC/RTU 的保持寄存器行为：
///   - 维护一组寄存器值 (Address → Value)
///   - 支持读/写操作
///   - 可注入噪声/波动模拟真实设备
///   - 记录寄存器变更历史用于测试验证
///
/// 对标 EasyBuilder Pro 的 EasySimulator 内置虚拟 Modbus 从站。
/// </summary>
public class CommunicationSimulator
{
    private readonly ConcurrentDictionary<int, double> _registers = new();
    private readonly List<RegisterChange> _changeLog = new();
    private readonly object _logLock = new();
    private readonly Random _rng = new();

    /// <summary>寄存器写入事件</summary>
    public event EventHandler<RegisterChange>? RegisterWritten;

    /// <summary>日志输出</summary>
    public Action<string>? Log { get; set; }

    /// <summary>写模式: Echo = 回显写入值, Invert = 取反, Respond = 返回固定值</summary>
    public WriteMode Mode { get; set; } = WriteMode.Echo;

    /// <summary>虚拟设备状态</summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>
    /// 模拟响应延迟（毫秒），0 = 无延迟。
    /// 用于贴近真实 PLC/RTU 的应答时延，验证通讯超时处理。
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// 随机响应失败率（0-1，默认 0）—— 模拟不可靠网络/偶发通讯错误。
    /// 命中时写操作被丢弃（不写入寄存器）并记录警告日志。
    /// </summary>
    public double ErrorRate { get; set; }

    // ── 寄存器操作 ──

    /// <summary>读取保持寄存器值</summary>
    public double ReadRegister(int address)
    {
        if (!IsConnected) throw new SimulatorNotConnectedException();
        ApplySimulatedDelay();
        return _registers.GetValueOrDefault(address, 0.0);
    }

    /// <summary>批量读取寄存器</summary>
    public Dictionary<int, double> ReadRegisters(IEnumerable<int> addresses)
    {
        var result = new Dictionary<int, double>();
        foreach (var addr in addresses)
            result[addr] = ReadRegister(addr);
        return result;
    }

    /// <summary>写入保持寄存器</summary>
    public void WriteRegister(int address, double value)
    {
        if (!IsConnected) throw new SimulatorNotConnectedException();
        ApplySimulatedDelay();

        // 错误率注入: 命中时丢弃本次写请求（模拟丢包/超时）
        if (ErrorRate > 0 && _rng.NextDouble() < ErrorRate)
        {
            Log?.Invoke($"[CommSim] 写入丢弃 (错误率 {ErrorRate:P0}, 地址={address})");
            return;
        }

        var oldValue = _registers.GetValueOrDefault(address, 0.0);
        var response = Mode switch
        {
            WriteMode.Echo => value,
            WriteMode.Invert => -value,
            WriteMode.Random => Math.Round(_rng.NextDouble() * 100, 2),
            WriteMode.Respond => value,  // no additional transform
            _ => value,
        };

        _registers[address] = response;

        var change = new RegisterChange
        {
            Address = address,
            OldValue = oldValue,
            NewValue = response,
            Timestamp = DateTime.UtcNow,
        };

        lock (_logLock)
        {
            _changeLog.Add(change);
        }

        RegisterWritten?.Invoke(this, change);
        Log?.Invoke($"[CommSim] 寄存器[{address}] = {response:F2} ({Mode})");
    }

    /// <summary>批量写入寄存器</summary>
    public void WriteRegisters(Dictionary<int, double> values)
    {
        foreach (var kv in values)
            WriteRegister(kv.Key, kv.Value);
    }

    /// <summary>获取寄存器变更日志</summary>
    public IReadOnlyList<RegisterChange> GetChangeLog(int? maxEntries = null)
    {
        lock (_logLock)
        {
            var entries = _changeLog.OrderByDescending(c => c.Timestamp).ToList();
            return maxEntries.HasValue ? entries.Take(maxEntries.Value).ToList() : entries;
        }
    }

    /// <summary>清空所有寄存器和日志</summary>
    public void Clear()
    {
        _registers.Clear();
        lock (_logLock)
        {
            _changeLog.Clear();
        }
        Log?.Invoke("[CommSim] 已清空");
    }

    /// <summary>模拟响应延迟（毫秒）</summary>
    private void ApplySimulatedDelay()
    {
        if (LatencyMs > 0)
            Thread.Sleep(LatencyMs);
    }

    /// <summary>断开连接</summary>
    public void Disconnect()
    {
        IsConnected = false;
        Log?.Invoke("[CommSim] 已断开");
    }

    /// <summary>重新连接</summary>
    public void Reconnect()
    {
        IsConnected = true;
        Log?.Invoke("[CommSim] 已重新连接");
    }
}

/// <summary>
/// 寄存器写入记录。
/// </summary>
public class RegisterChange
{
    public int Address { get; set; }
    public double OldValue { get; set; }
    public double NewValue { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 虚拟从站写入响应模式。
/// </summary>
public enum WriteMode
{
    /// <summary>回显写入值</summary>
    Echo,

    /// <summary>取反写入值</summary>
    Invert,

    /// <summary>随机响应</summary>
    Random,

    /// <summary>正常应答</summary>
    Respond,
}
