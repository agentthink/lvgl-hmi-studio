namespace HmiEditor.Core.Models.Alarm;

/// <summary>
/// 报警严重级别 —— 四个等级对应不同的显示颜色和响应策略。
/// </summary>
public enum AlarmSeverity
{
    /// <summary>信息：蓝色，不需要确认</summary>
    Info = 0,

    /// <summary>警告：黄色，需要确认</summary>
    Warning = 1,

    /// <summary>严重：橙色，需要立即确认</summary>
    Critical = 2,

    /// <summary>紧急：红色，需要立即确认且触发联动动作</summary>
    Emergency = 3,
}
