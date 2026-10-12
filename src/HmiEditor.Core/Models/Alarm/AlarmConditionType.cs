namespace HmiEditor.Core.Models.Alarm;

/// <summary>
/// 报警触发条件类型 —— 对标主流 HMI/SCADA 软件（WinCC / 组态王 / MCGS）的报警类型。
/// </summary>
public enum AlarmConditionType
{
    /// <summary>上限触发：变量值 ≥ Threshold（H 高报）</summary>
    HighLimit = 0,

    /// <summary>下限触发：变量值 ≤ Threshold（L 低报）</summary>
    LowLimit = 1,

    /// <summary>位触发：变量特定位为 1</summary>
    BitTrigger = 2,

    /// <summary>状态变化：变量值从一种状态变为另一种（偏差类，与 Deviation 同语义）</summary>
    StateChange = 3,

    /// <summary>偏差触发：变量值与期望值的偏差超过 ±Threshold</summary>
    Deviation = 4,

    /// <summary>上上限触发：变量值 ≥ Threshold（HH 高高报，比上限更严重）</summary>
    HighHighLimit = 5,

    /// <summary>下下限触发：变量值 ≤ Threshold（LL 低低报，比下限更严重）</summary>
    LowLowLimit = 6,

    /// <summary>变化率触发：单位时间变化量超过 Threshold</summary>
    RateOfChange = 7,
}
