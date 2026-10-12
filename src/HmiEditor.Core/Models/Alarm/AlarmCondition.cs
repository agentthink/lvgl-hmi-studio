namespace HmiEditor.Core.Models.Alarm;

/// <summary>
/// 报警触发条件 —— 定义何时触发报警。
/// </summary>
public class AlarmCondition
{
    /// <summary>绑定的变量 ID（关联到 Variable.Id）</summary>
    public string VariableId { get; set; } = "";

    /// <summary>触发条件类型</summary>
    public AlarmConditionType Type { get; set; } = AlarmConditionType.HighLimit;

    /// <summary>阈值（HighLimit/LowLimit/Deviation 使用）</summary>
    public double Threshold { get; set; }

    /// <summary>死区（防抖动，条件解除时需回到 Threshold ± Deadband 范围）</summary>
    public double Deadband { get; set; }

    /// <summary>延迟触发（毫秒，条件持续满足超过此时间才触发）</summary>
    public int DelayMs { get; set; }

    /// <summary>位偏移（BitTrigger 使用，指定检查哪个位）</summary>
    public int BitOffset { get; set; }

    /// <summary>期望值（Deviation 使用，偏差 = |实际值 - ExpectedValue|）</summary>
    public double ExpectedValue { get; set; }
}
