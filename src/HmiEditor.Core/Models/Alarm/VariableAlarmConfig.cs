namespace HmiEditor.Core.Models.Alarm;

/// <summary>
/// 变量级报警配置 —— 挂在单个 Variable 上，参考主流 HMI/SCADA 软件（WinCC / 组态王 / MCGS）的
/// 变量报警设置模型：一个变量可配置多个报警（上限、下限、上上限、下下限、位触发、偏差等），
/// 每个报警独立具备 类型/阈值/死区/延迟/级别/确认/使能/文本。
/// 与 AlarmConfig（工程级报警分组）的区别：本配置以变量为归属，无需手动建组，
/// 保存变量时自动写入变量，代码生成时按变量聚合到报警引擎。
/// </summary>
public class VariableAlarmConfig : IIdentifiedModel
{
    /// <summary>唯一标识（8 位十六进制）。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>报警名称（如"温度过高""压力下限"）。</summary>
    public string Name { get; set; } = "";

    /// <summary>触发条件类型（上限/下限/上上限/下下限/位触发/偏差/状态变化）。</summary>
    public AlarmConditionType Type { get; set; } = AlarmConditionType.HighLimit;

    /// <summary>阈值（上限/下限/偏差用）。</summary>
    public double Threshold { get; set; }

    /// <summary>死区（防抖动；条件解除时需回到 Threshold ± Deadband 范围）。</summary>
    public double Deadband { get; set; }

    /// <summary>延迟触发（毫秒；条件持续满足超过此时间才触发）。</summary>
    public int DelayMs { get; set; }

    /// <summary>位偏移（BitTrigger 用，指定检查变量的哪个位）。</summary>
    public int BitOffset { get; set; }

    /// <summary>期望值（Deviation/StateChange 用）。</summary>
    public double ExpectedValue { get; set; }

    /// <summary>严重级别（Info/Warning/Critical/Emergency）。</summary>
    public AlarmSeverity Severity { get; set; } = AlarmSeverity.Warning;

    /// <summary>是否需要确认。</summary>
    public bool RequiresAck { get; set; } = true;

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>报警文本模板（显示在报警列表；支持 {value} 占位符，运行时替换为当前值）。</summary>
    public string? Message { get; set; }

    /// <summary>关联的脚本 ID（可选，触发时执行）。</summary>
    public string? ScriptId { get; set; }

    /// <summary>转换为工程级 AlarmConfig（用于报警引擎注册/代码生成）。</summary>
    public AlarmConfig ToAlarmConfig(string variableId, string groupId = "")
    {
        return new AlarmConfig
        {
            Id = Id,
            Name = string.IsNullOrWhiteSpace(Name) ? $"变量报警-{variableId}" : Name,
            Description = Message,
            Severity = Severity,
            GroupId = groupId,
            RequiresAck = RequiresAck,
            IsEnabled = IsEnabled,
            ScriptId = ScriptId,
            Condition = new AlarmCondition
            {
                VariableId = variableId,
                Type = Type,
                Threshold = Threshold,
                Deadband = Deadband,
                DelayMs = DelayMs,
                BitOffset = BitOffset,
                ExpectedValue = ExpectedValue,
            }
        };
    }

    /// <summary>从工程级 AlarmConfig 反向填充（编辑已有报警时）。</summary>
    public static VariableAlarmConfig FromAlarmConfig(AlarmConfig src)
    {
        return new VariableAlarmConfig
        {
            Id = src.Id,
            Name = src.Name,
            Severity = src.Severity,
            RequiresAck = src.RequiresAck,
            IsEnabled = src.IsEnabled,
            ScriptId = src.ScriptId,
            Message = src.Description,
            Type = src.Condition?.Type ?? AlarmConditionType.HighLimit,
            Threshold = src.Condition?.Threshold ?? 0,
            Deadband = src.Condition?.Deadband ?? 0,
            DelayMs = src.Condition?.DelayMs ?? 0,
            BitOffset = src.Condition?.BitOffset ?? 0,
            ExpectedValue = src.Condition?.ExpectedValue ?? 0,
        };
    }
}
