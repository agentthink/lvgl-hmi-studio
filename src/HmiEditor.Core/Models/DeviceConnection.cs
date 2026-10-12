namespace HmiEditor.Core.Models;

using System.Collections.ObjectModel;

/// <summary>
/// 设备连接配置 —— 一个 HMI 项目中与一个 PLC/设备的通讯连接（物理通道 + 协议）。
/// 一条连接下可挂多个子设备（<see cref="DeviceSlave"/>，如 Modbus 多从站），
/// 变量通过 SlaveId 归属到具体子设备。
/// </summary>
public class DeviceConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "PLC1";
    public string DriverId { get; set; } = "modbus-rtu";
    public Dictionary<string, string> Config { get; set; } = new();
    public bool IsEnabled { get; set; } = true;

    /// <summary>连接下的子设备（从站）列表。</summary>
    public ObservableCollection<DeviceSlave> Slaves { get; set; } = new();
}
