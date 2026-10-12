using System.Collections.Concurrent;
using Hmi.Communication.Contracts;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Services;

/// <summary>
/// Modbus 运行时值源 —— 将真实 Modbus 从站（通过 IRuntimeCommunicationDriver）适配为 IRuntimeValueSource。
///
/// 地址映射（Modbus 标准区号寻址）:
///   0x 线圈       00001-09999  → 偏移 0-9998    (功能码 0x01/0x05)
///   1x 离散输入   10001-19999  → 偏移 0-9998    (读 0x02，未实现写)
///   3x 输入寄存器 30001-39999  → 偏移 0-9998    (读 0x04，未实现写)
///   4x 保持寄存器 40001-49999  → 偏移 0-9998    (功能码 0x03/0x06/0x10)
///
/// 标签地址由 RuntimeTag.Address 提供，例如 "40001" → 保持寄存器偏移 0。
/// 未连接的地址区间/读失败 → TryRead 返回 false（上层回退模拟信号）。
///
/// <para><b>取值不阻塞 UI</b>：<see cref="TryRead"/> 由渲染路径逐帧调用，只读**后台泵**
/// 采到的快照；真实 Modbus 请求在泵里以 <see cref="RefreshIntervalMs"/> 为周期发出。</para>
/// </summary>
public class ModbusRuntimeSource : IRuntimeValueSource, IDisposable
{
    private readonly IRuntimeCommunicationDriver _driver;
    private readonly Func<string, string?> _addressResolver;

    /// <summary>最近一次采到的**原始值**（按变量 Id）。</summary>
    private readonly ConcurrentDictionary<string, double> _cache = new(StringComparer.Ordinal);

    /// <summary>出现过的变量 —— 后台泵只刷新这些。</summary>
    private readonly ConcurrentDictionary<string, byte> _wanted = new(StringComparer.Ordinal);

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _pump;

    /// <summary>后台刷新周期（毫秒）。预览取值是"快照"，不需要每帧一次网络往返。</summary>
    private const int RefreshIntervalMs = 200;

    /// <summary>日志输出。</summary>
    public Action<string>? Log { get; set; }

    public ModbusRuntimeSource(
        IRuntimeCommunicationDriver driver,
        Func<string, string?> addressResolver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _addressResolver = addressResolver ?? throw new ArgumentNullException(nameof(addressResolver));
        _pump = Task.Run(() => PumpAsync(_cts.Token));
    }

    public bool IsConnected => _driver.IsConnected;
    public string Name => "Modbus";

    /// <summary>
    /// 读取最近一次采到的值。**这里不做任何网络 I/O** —— 真正的请求在后台泵里。
    ///
    /// <para><b>为什么必须这样：</b>本方法由渲染路径调用（`DesignCanvas.Render` 对每个带标签的
    /// 显示控件、**每一帧一次**）。旧实现直接 `GetAwaiter().GetResult()` 发 Modbus 请求：
    /// 接上真实设备后，每个控件每帧都要等一个完整往返，而预览超时是 1500ms ⇒
    /// **UI 无限冻结**（阶段 120 修复）。现在首帧可能还没有值（返回 false ⇒ 上层回退模拟信号），
    /// 之后按泵的节奏（200ms）更新。</para>
    /// </summary>
    public bool TryRead(string variableId, out double value)
    {
        _wanted.TryAdd(variableId, 0);
        if (!_driver.IsConnected)
        {
            value = 0;
            return false;
        }
        return _cache.TryGetValue(variableId, out value);
    }

    /// <summary>后台刷新泵：按周期把 <see cref="_wanted"/> 里的变量各读一次，写进缓存。</summary>
    private async Task PumpAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_driver.IsConnected)
                {
                    foreach (var id in _wanted.Keys)
                    {
                        if (ct.IsCancellationRequested) break;
                        var read = await ReadOnceAsync(id, ct).ConfigureAwait(false);
                        if (read.HasValue) _cache[id] = read.Value;
                    }
                }
            }
            catch
            {
                // 驱动整体抛错也不许把泵打死：记都不记（单变量失败已在 ReadOnceAsync 里吞掉），下一轮再试。
            }

            try
            {
                await Task.Delay(RefreshIntervalMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<double?> ReadOnceAsync(string variableId, CancellationToken ct)
    {
        var address = _addressResolver(variableId);
        if (string.IsNullOrEmpty(address)) return null;

        try
        {
            if (TryParseModbusAddress(address, out var area, out var offset))
            {
                switch (area)
                {
                    case ModbusArea.HoldingRegister:
                        var regs = await _driver.ReadHoldingRegistersAsync((ushort)offset, 1, ct).ConfigureAwait(false);
                        return regs.Length > 0 ? regs[0] : null;
                    case ModbusArea.Coil:
                        var coils = await _driver.ReadCoilsAsync((ushort)offset, 1, ct).ConfigureAwait(false);
                        return coils.Length > 0 ? (coils[0] ? 1 : 0) : null;
                    default:
                        return null; // 离散输入/输入寄存器：读未实现
                }
            }

            // 非标准地址（无区号）: 尝试按保持寄存器偏移解析
            if (int.TryParse(address, out var directOffset))
            {
                var regs = await _driver.ReadHoldingRegistersAsync((ushort)directOffset, 1, ct).ConfigureAwait(false);
                return regs.Length > 0 ? regs[0] : null;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    public bool TryWrite(string variableId, double value)
    {
        if (!_driver.IsConnected) return false;

        var address = _addressResolver(variableId);
        if (string.IsNullOrEmpty(address)) return false;

        // I7: 字符串地址驱动（OPC UA 等）—— 节点寻址直接透传
        if (_driver is IStringAddressRuntimeDriver stringDriver)
        {
            try
            {
                return stringDriver.WriteValueAsync(address, value).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                return false;
            }
        }

        if (TryParseModbusAddress(address, out var area, out var offset))
        {
            try
            {
                switch (area)
                {
                    case ModbusArea.HoldingRegister:
                        _driver.WriteSingleRegisterAsync((ushort)offset, ToUInt16(value)).GetAwaiter().GetResult();
                        return true;
                    case ModbusArea.Coil:
                        _driver.WriteSingleCoilAsync((ushort)offset, value > 0.5).GetAwaiter().GetResult();
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 非标准地址: 按保持寄存器
        if (int.TryParse(address, out var directOffset))
        {
            try
            {
                _driver.WriteSingleRegisterAsync((ushort)directOffset, ToUInt16(value)).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        return false;
    }

    // ── 地址解析 ──

    /// <summary>Modbus 数据区类型。</summary>
    public enum ModbusArea { Coil, DiscreteInput, InputRegister, HoldingRegister }

    /// <summary>
    /// 解析 Modbus 区号地址 → (区, 偏移)。
    /// 支持 "40001"、"4x0001"、标准 "0x"/"1x"/"3x"/"4x" 前缀。
    /// </summary>
    public static bool TryParseModbusAddress(string address, out ModbusArea area, out int offset)
    {
        area = ModbusArea.HoldingRegister;
        offset = 0;

        if (string.IsNullOrWhiteSpace(address)) return false;
        address = address.Trim().ToLowerInvariant();

        // 形式: 4x0001 / 0x0001
        if (address.Contains('x'))
        {
            var parts = address.Split('x');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[1], out offset)) return false;

            switch (parts[0])
            {
                case "0": area = ModbusArea.Coil; return true;
                case "1": area = ModbusArea.DiscreteInput; return true;
                case "3": area = ModbusArea.InputRegister; return true;
                case "4": area = ModbusArea.HoldingRegister; return true;
                default: return false;
            }
        }

        // 形式: 40001 / 00001
        if (address.Length >= 5 && int.TryParse(address, out var full))
        {
            var firstDigit = address[0];
            offset = full % 10000;
            switch (firstDigit)
            {
                case '0': area = ModbusArea.Coil; return true;
                case '1': area = ModbusArea.DiscreteInput; return true;
                case '3': area = ModbusArea.InputRegister; return true;
                case '4': area = ModbusArea.HoldingRegister; return true;
                default: return false;
            }
        }

        return false;
    }

    /// <summary>double → 16 位无符号（饱和转换）。</summary>
    private static ushort ToUInt16(double value)
    {
        if (value <= 0) return 0;
        if (value >= ushort.MaxValue) return ushort.MaxValue;
        return (ushort)Math.Round(value);
    }

    /// <summary>
    /// 停止后台泵。**必须被调用**：预览换源（真实设备 ↔ 虚拟从站）与 Stop/Dispose 时都要释放，
    /// 否则每进一次预览就多留一个常驻 Task。
    /// </summary>
    public void Dispose()
    {
        try { _cts.Cancel(); } catch { /* 已释放 */ }
        _cts.Dispose();
    }
}
