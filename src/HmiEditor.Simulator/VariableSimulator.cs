using System.Collections.Concurrent;
using HmiEditor.Core.Models;

namespace HmiEditor.Simulator;

/// <summary>
/// 变量模拟器 —— 为变量集合生成模拟值。
///
/// 支持 7 种信号模式:
///   Constant / Sine / Square / Ramp / Increment / Random / Manual
///
/// 设计要点 (dev-environment-parity):
///   - VariableSimulator 通过模拟值替代真实硬件变量，
///     使 HMI 编辑器在不连接设备时也能验证行为
///   - 6 种信号模式覆盖大多数工业场景
///   - 可添加噪声模拟真实传感器信号
/// </summary>
public class VariableSimulator : IDisposable
{
    private readonly ConcurrentDictionary<string, SimulatedVariable> _variables = new();
    private readonly Timer? _tickTimer;
    private readonly TimeProvider _time;
    private volatile bool _running;
    private long _startTimestamp;
    private volatile bool _hasStarted;

    /// <summary>变量值更新事件</summary>
    public event EventHandler<Dictionary<string, double>>? ValuesUpdated;

    /// <summary>日志输出</summary>
    public Action<string>? Log { get; set; }

    /// <summary>更新间隔（毫秒）</summary>
    public int TickIntervalMs { get; set; } = 100;

    /// <summary>
    /// 时钟倍速（0.1-10，默认 1.0）—— 加速/减速仿真时间轴。
    /// 例如 2.0 表示每秒仿真时间前进 2 秒，便于快速验证长时间波形。
    /// </summary>
    public double ClockMultiplier { get; set; } = 1.0;

    /// <summary>
    /// 当前模拟时间（秒，已按时钟倍速缩放）。
    ///
    /// 必须走 <see cref="TimeProvider.GetTimestamp"/> 的**单调**时间轴，而不是墙上时钟：
    /// 墙上时钟会被 NTP 校时前后跳动，轻则波形回卷、重则仿真时间变负
    /// —— 与固件侧「LVGL tick 必须 CLOCK_MONOTONIC」是同一条契约（见 §固件）。
    ///
    /// 未 <see cref="Start"/> 时为 0（旧实现用 <c>DateTime.MinValue</c> 做基准，
    /// 此时会返回 ~6.4e10 秒的垃圾值）。
    /// </summary>
    public double ElapsedSeconds => _hasStarted
        ? _time.GetElapsedTime(_startTimestamp).TotalSeconds * ClockMultiplier
        : 0;

    /// <summary>仿真是否运行中</summary>
    public bool IsRunning => _running;

    /// <summary>已执行 tick 次数（运行统计）</summary>
    public long TickCount { get; private set; }

    /// <summary>最近一次 tick 生成全部模拟值耗时（毫秒）</summary>
    public double LastTickElapsedMs { get; private set; }

    /// <summary>当前注册的模拟变量数</summary>
    public int VariableCount => _variables.Count;

    /// <param name="timeProvider">
    /// 时钟来源。默认 <see cref="TimeProvider.System"/>（其 <c>GetTimestamp</c> 即
    /// <c>Stopwatch</c> 的 <c>CLOCK_MONOTONIC</c>）；测试注入假时钟以摆脱墙上时钟依赖。
    /// </param>
    public VariableSimulator(TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _tickTimer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    // ── 配置管理 ──

    /// <summary>注册一个模拟变量</summary>
    public void RegisterVariable(SimulatedVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        _variables[variable.VariableId] = variable;
        Log?.Invoke($"[VariableSim] 注册变量: {variable.VariableId} ({variable.Signal})");
    }

    /// <summary>批量注册变量</summary>
    public void RegisterVariables(IEnumerable<SimulatedVariable> variables)
    {
        foreach (var v in variables)
            RegisterVariable(v);
    }

    /// <summary>解除注册</summary>
    public void UnregisterVariable(string variableId)
    {
        _variables.TryRemove(variableId, out _);
    }

    /// <summary>获取当前所有模拟变量配置</summary>
    public IReadOnlyList<SimulatedVariable> GetVariables() =>
        _variables.Values.ToList();

    /// <summary>设置手动模式变量的值</summary>
    public void SetManualValue(string variableId, double value)
    {
        if (_variables.TryGetValue(variableId, out var v) && v.Signal == SignalType.Manual)
        {
            v.ManualValue = value;
            Log?.Invoke($"[VariableSim] 手动值: {variableId} = {value:F2}");
        }
    }

    // ── 运行控制 ──

    /// <summary>启动模拟（把仿真时间轴基准重置到当前单调时间戳）</summary>
    public void Start()
    {
        // 先写基准、再放开 _hasStarted：volatile 写的 release 语义保证
        // 读到 _hasStarted==true 的线程必然也能读到新基准。
        _startTimestamp = _time.GetTimestamp();
        _hasStarted = true;
        TickCount = 0;
        LastTickElapsedMs = 0;
        _running = true;
        _tickTimer?.Change(0, TickIntervalMs);
        Log?.Invoke($"[VariableSim] 启动 (间隔={TickIntervalMs}ms, 倍速={ClockMultiplier:F1}x, 变量={VariableCount})");
    }

    /// <summary>停止模拟</summary>
    public void Stop()
    {
        _running = false;
        _tickTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        Log?.Invoke("[VariableSim] 停止");
    }

    /// <summary>获取当前所有模拟值</summary>
    public Dictionary<string, double> GetCurrentValues()
    {
        var t = ElapsedSeconds;
        var result = new Dictionary<string, double>();
        foreach (var kv in _variables)
        {
            result[kv.Key] = kv.Value.GetValue(t);
        }
        return result;
    }

    /// <summary>清空所有变量</summary>
    public void Clear()
    {
        _variables.Clear();
        Log?.Invoke("[VariableSim] 已清空");
    }

    public void Dispose()
    {
        Stop();
        _tickTimer?.Dispose();
        Clear();
        Log?.Invoke("[VariableSim] 已释放");
    }

    private void OnTick(object? state)
    {
        if (!_running) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var values = GetCurrentValues();
        LastTickElapsedMs = sw.Elapsed.TotalMilliseconds;
        TickCount++;
        ValuesUpdated?.Invoke(this, values);
    }
}
