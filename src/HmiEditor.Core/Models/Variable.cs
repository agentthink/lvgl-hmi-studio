namespace HmiEditor.Core.Models;

using System.Collections.ObjectModel;
using HmiEditor.Core.Models.Alarm;

/// <summary>
/// HMI 变量 —— 控件与设备寄存器之间的数据中间层。
/// R3-T1: 支持 UDT 实例 —— 当 UdtTypeId 非空时，此变量为 UDT 实例，
/// 控件通过 "实例名.字段名" 格式绑定到具体字段。
/// </summary>
public class Variable : IIdentifiedModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string DataType { get; set; } = "Int16";
    public string ConnectionId { get; set; } = "";
    public string GroupId { get; set; } = "";

    /// <summary>
    /// 归属子设备（从站）Id。空表示直接挂在连接下（无子设备层级）。
    /// 非空时须指向 ConnectionId 对应连接下的某个 DeviceSlave。
    /// </summary>
    public string SlaveId { get; set; } = "";
    public int PollRateMs { get; set; } = 500;
    public double Scale { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public string? Unit { get; set; }
    public string? SimulatedValue { get; set; }

    // ── 变量级报警设置（参考主流 HMI 软件：WinCC/组态王/MCGS）──

    /// <summary>
    /// 变量报警配置集合。每个变量可配置多个报警（上限/下限/上上限/位触发等），
    /// 保存变量时写入；代码生成按变量聚合注册到报警引擎。
    /// </summary>
    public ObservableCollection<VariableAlarmConfig> Alarms { get; set; } = new();

    // ── R3-T1: UDT 支持 ──

    /// <summary>
    /// UDT 类型定义 Id。非空时表示此变量是某个 UdtDefinition 的实例。
    /// 空字符串表示普通标量变量（默认，向后兼容）。
    /// </summary>
    public string UdtTypeId { get; set; } = "";

    /// <summary>是否为 UDT 实例。</summary>
    public bool IsUdtInstance => !string.IsNullOrEmpty(UdtTypeId);
}
