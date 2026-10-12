namespace HmiEditor.Core.Models;

/// <summary>
/// 子设备（从站）配置 —— 一个通讯连接下可挂多个子设备（从站）。
/// 参考主流组态软件（组态王/WinCC/MCGS）的层级结构：
/// 连接（物理通道）→ 协议（驱动）→ 子设备（从站，如 Modbus 站号）→ 子设备变量。
/// 例：Modbus RTU 连接 COM1 下可挂 站号1/站号2/站号3 等多个从站，
/// 每个从站有独立的站号与参数，变量通过 SlaveId 归属到具体子设备。
/// </summary>
public class DeviceSlave
{
    /// <summary>唯一标识。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>子设备名称（如"1号从站""变频器1"）。</summary>
    public string Name { get; set; } = "";

    /// <summary>所属连接 Id（关联 DeviceConnection.Id）。</summary>
    public string ConnectionId { get; set; } = "";

    /// <summary>从站地址/站号（Modbus slave id；其他协议可为设备地址/单元号）。</summary>
    public int SlaveAddress { get; set; } = 1;

    /// <summary>子设备专用参数（如功能码集合、数据类型默认值等；键值对，缺省继承连接参数）。</summary>
    public Dictionary<string, string> Config { get; set; } = new();

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>描述/备注。</summary>
    public string? Description { get; set; }
}
