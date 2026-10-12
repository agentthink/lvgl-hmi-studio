using System;
using System.Collections.Generic;
using System.Linq;

namespace HmiEditor.Simulator;

/// <summary>
/// 变量模拟值生成器的信号模式。
/// </summary>
public enum SignalType
{
    /// <summary>常数 — 固定输出指定值</summary>
    Constant,

    /// <summary>正弦波 — sin(2πft)</summary>
    Sine,

    /// <summary>方波 — sign(sin(2πft))</summary>
    Square,

    /// <summary>锯齿波/斜坡 — ((t * freq) % 2 - 1) * amplitude</summary>
    Ramp,

    /// <summary>递增 — 从最小值到最大值循环</summary>
    Increment,

    /// <summary>随机 — [min, max] 均匀分布</summary>
    Random,

    /// <summary>手动 — 由外部 SetValue 控制</summary>
    Manual,
}

/// <summary>
/// 单个模拟变量的配置。
/// </summary>
public class SimulatedVariable
{
    /// <summary>变量 Id（与 VariableManager.Variable.Id 一致）。</summary>
    public string VariableId { get; set; } = "";

    /// <summary>变量名称（冗余缓存，用于调试展示）。</summary>
    public string VariableName { get; set; } = "";

    public SignalType Signal { get; set; } = SignalType.Constant;
    public double Amplitude { get; set; } = 100.0;
    public double Offset { get; set; } = 0.0;
    public double Frequency { get; set; } = 0.1;  // Hz
    public double MinValue { get; set; } = 0.0;
    public double MaxValue { get; set; } = 100.0;
    public double ManualValue { get; set; }
    public double Noise { get; set; }  // ± noise amplitude

    /// <summary>获取当前模拟值</summary>
    public double GetValue(double timeSeconds)
    {
        if (Signal == SignalType.Manual)
            return ManualValue;

        double raw;
        switch (Signal)
        {
            case SignalType.Constant:
                raw = Amplitude;
                break;
            case SignalType.Sine:
                raw = Math.Sin(2 * Math.PI * Frequency * timeSeconds) * Amplitude + Offset;
                break;
            case SignalType.Square:
                raw = Math.Sign(Math.Sin(2 * Math.PI * Frequency * timeSeconds)) * Amplitude + Offset;
                break;
            case SignalType.Ramp:
                raw = ((timeSeconds * Frequency % 2.0) - 1.0) * Amplitude + Offset;
                break;
            case SignalType.Increment:
            {
                var phase = (timeSeconds * Frequency) % 1.0;
                raw = MinValue + phase * (MaxValue - MinValue);
                break;
            }
            case SignalType.Random:
            {
                var rnd = ((long)(timeSeconds * 1000) * 2654435761L) & 0x7FFFFFFF;
                var normalized = (double)rnd / int.MaxValue;
                raw = MinValue + normalized * (MaxValue - MinValue);
                break;
            }
            default:
                raw = Amplitude;
                break;
        }

        // Add noise
        if (Noise > 0)
        {
            var noise = (Random.Shared.NextDouble() - 0.5) * 2 * Noise;
            raw += noise;
        }

        return Math.Clamp(raw, MinValue, MaxValue);
    }
}
