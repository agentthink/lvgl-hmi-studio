using HmiEditor.Core.Services;

namespace HmiEditor.Simulator;

/// <summary>
/// 通讯运行时值源 —— 将 CommunicationSimulator（寄存器级虚拟从站）
/// 适配为 IRuntimeValueSource（变量级值源）。
///
/// 桥接关系（对标 WinCC 运行时 / EasyBuilder EasySimulator）:
///   RuntimeBindingEngine.ReadValue("v_temp")
///     → CommunicationRuntimeSource.TryRead("v_temp")
///       → 变量→地址解析 ("v_temp" → "40001")
///       → 地址→寄存器号 ("40001" → 40001)
///         → CommunicationSimulator.ReadRegister(40001)   ← 虚拟 PLC 寄存器
///
/// 两级解析可分别注入:
///   variableAddressResolver: variableId → 地址字符串（通常来自 RuntimeTag.Address）
///   addressParser:           地址字符串 → 寄存器号（支持 Modbus 区号前缀等格式）
/// 任一环节解析失败 → TryRead/TryWrite 返回 false（上层回退模拟信号）。
/// </summary>
public class CommunicationRuntimeSource : IRuntimeValueSource
{
    private readonly CommunicationSimulator _simulator;
    private readonly Func<string, string?>? _variableAddressResolver;
    private readonly Func<string, int?> _addressParser;

    /// <summary>日志输出。</summary>
    public Action<string>? Log { get; set; }

    public CommunicationRuntimeSource(
        CommunicationSimulator simulator,
        Func<string, string?>? variableAddressResolver = null,
        Func<string, int?>? addressParser = null)
    {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        _variableAddressResolver = variableAddressResolver;
        _addressParser = addressParser ?? DefaultAddressParser;
    }

    public bool IsConnected => _simulator.IsConnected;
    public string Name => "CommunicationSimulator";

    /// <summary>默认地址→寄存器解析 —— 提取数字部分；"站号:地址" 先剥离站号前缀。</summary>
    public static int? DefaultAddressParser(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        // 显示用地址可能带 "站号:" 前缀（如 "1:40001"）——剥离后按纯地址提取数字，
        // 否则会拼成 "140001" 错误寄存器号。
        var colon = address.IndexOf(':');
        if (colon > 0 && colon < address.Length - 1 && address[..colon].All(char.IsDigit))
            address = address[(colon + 1)..];
        var digits = new string(address.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : null;
    }

    public bool TryRead(string variableId, out double value)
    {
        value = 0;
        if (!_simulator.IsConnected)
        {
            Log?.Invoke($"[CommSource] 读取失败: 未连接 ({variableId})");
            return false;
        }

        var register = ResolveRegister(variableId);
        if (register == null)
        {
            Log?.Invoke($"[CommSource] 读取失败: 无地址映射 ({variableId})");
            return false;
        }

        try
        {
            value = _simulator.ReadRegister(register.Value);
            Log?.Invoke($"[CommSource] 读取 {variableId} → 寄存器[{register.Value}] = {value:F2}");
            return true;
        }
        // ⚠ 断连异常有**两种**：`InvalidOperationException` 与
        // `SimulatorNotConnectedException`（它直接继承 Exception）。只catch前者是**死 catch**：
        // `IsConnected` 判断与调用之间发生断连时，异常会逃出 TryRead/TryWrite 进渲染路径
        // （阶段 121 修复）。这里的契约是"失败返回 false"，不是"把异常抛给渲染"。
        catch (Exception ex) when (ex is InvalidOperationException or SimulatorNotConnectedException)
        {
            return false;
        }
    }

    public bool TryWrite(string variableId, double value)
    {
        if (!_simulator.IsConnected)
        {
            Log?.Invoke($"[CommSource] 写入失败: 未连接 ({variableId})");
            return false;
        }

        var register = ResolveRegister(variableId);
        if (register == null)
        {
            Log?.Invoke($"[CommSource] 写入失败: 无地址映射 ({variableId})");
            return false;
        }

        try
        {
            _simulator.WriteRegister(register.Value, value);
            Log?.Invoke($"[CommSource] 写入 {variableId} → 寄存器[{register.Value}] = {value:F2}");
            return true;
        }
        // ⚠ 断连异常有**两种**：`InvalidOperationException` 与
        // `SimulatorNotConnectedException`（它直接继承 Exception）。只catch前者是**死 catch**：
        // `IsConnected` 判断与调用之间发生断连时，异常会逃出 TryRead/TryWrite 进渲染路径
        // （阶段 121 修复）。这里的契约是"失败返回 false"，不是"把异常抛给渲染"。
        catch (Exception ex) when (ex is InvalidOperationException or SimulatorNotConnectedException)
        {
            return false;
        }
    }

    /// <summary>解析变量 → 寄存器号（两级：变量→地址，地址→寄存器）。</summary>
    private int? ResolveRegister(string variableId)
    {
        // 优先使用显式地址映射
        if (_variableAddressResolver != null)
        {
            var address = _variableAddressResolver(variableId);
            if (!string.IsNullOrEmpty(address))
                return _addressParser(address);
            return null;
        }

        // 无映射时直接尝试解析变量 Id 中的数字（变量地址即寄存器号场景）
        return _addressParser(variableId);
    }
}
