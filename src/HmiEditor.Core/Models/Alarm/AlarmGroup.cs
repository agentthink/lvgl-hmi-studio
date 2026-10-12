using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models.Alarm;

/// <summary>
/// 报警分组 —— 用于报警列表的分类过滤和汇总显示。
/// 与 Siemens WinCC Alarm Logging 的区域分组概念对齐。
/// </summary>
public class AlarmGroup
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>分组名称（如"温度异常""通讯故障"）</summary>
    public string Name { get; set; } = "";

    /// <summary>分组内的报警列表</summary>
    public ObservableCollection<AlarmConfig> Alarms { get; set; } = new();

    /// <summary>此分组的默认确认策略</summary>
    public bool DefaultRequiresAck { get; set; } = true;
}
