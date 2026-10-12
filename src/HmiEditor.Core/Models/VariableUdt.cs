using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models;

/// <summary>
/// UDT 字段定义 —— 描述结构化变量类型中的一个成员。
/// </summary>
public class UdtField
{
    /// <summary>字段名（如 "speed"、"current"）。</summary>
    public string Name { get; set; } = "";

    /// <summary>字段数据类型（如 "Float"、"Bool"、"Int16"）。</summary>
    public string DataType { get; set; } = "Int16";

    /// <summary>默认值（实例化时填充到变量）。</summary>
    public string? DefaultValue { get; set; }

    /// <summary>字段描述/注释。</summary>
    public string? Description { get; set; }
}

/// <summary>
/// UDT 定义 —— 用户自定义结构化变量类型。
/// 例如 Motor { speed: Float, current: Float, fault: Bool, run: Bool }。
/// 实例化后，控件的 VariableBindingId 使用点号访问字段：motor1.speed。
/// </summary>
public class UdtDefinition : IIdentifiedModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>类型名（如 "Motor"、"Valve"）。</summary>
    public string Name { get; set; } = "";

    /// <summary>类型描述。</summary>
    public string? Description { get; set; }

    /// <summary>字段定义列表。</summary>
    public ObservableCollection<UdtField> Fields { get; set; } = new();

    // ── 字段操作 ──

    public UdtField AddField(UdtField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        Fields.Add(field);
        return field;
    }

    public UdtField AddField(string name, string dataType, string? defaultValue = null)
    {
        var field = new UdtField
        {
            Name = name,
            DataType = dataType,
            DefaultValue = defaultValue
        };
        Fields.Add(field);
        return field;
    }

    public bool RemoveField(UdtField field) => Fields.Remove(field);

    public UdtField? FindField(string name) =>
        Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal));

    // ── 实例化 ──

    /// <summary>
    /// 基于此 UDT 定义创建一个变量实例。
    /// 返回的 Variable 的 UdtTypeId 指向本定义，
    /// 控件通过 "实例名.字段名" 格式绑定到具体字段。
    /// </summary>
    public Variable CreateInstance(string instanceName, string groupId = "")
    {
        return new Variable
        {
            Name = instanceName,
            DataType = $"UDT:{Name}",
            UdtTypeId = Id,
            GroupId = groupId
        };
    }

    /// <summary>展开 UDT 实例为字段级伪变量列表（用于变量浏览器展示和绑定解析）。</summary>
    public List<UdtFieldVariable> ExpandInstance(Variable instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var result = new List<UdtFieldVariable>(Fields.Count);
        foreach (var field in Fields)
        {
            result.Add(new UdtFieldVariable
            {
                InstanceName = instance.Name,
                FieldName = field.Name,
                CompoundId = $"{instance.Name}.{field.Name}",
                DataType = field.DataType,
                Address = $"{instance.Address}.{field.Name}",
                ConnectionId = instance.ConnectionId,
                GroupId = instance.GroupId,
                SlaveId = instance.SlaveId,
                Scale = instance.Scale,
                Offset = instance.Offset,
                Unit = instance.Unit,
                UdtField = field
            });
        }
        return result;
    }
}

/// <summary>
/// UDT 字段级伪变量 —— 由 UDT 实例展开得到，用于变量浏览器展示和绑定解析。
/// 不存储在 Variables 集合中，而是按需展开。
/// </summary>
public class UdtFieldVariable
{
    /// <summary>所属 UDT 实例名（如 "motor1"）。</summary>
    public string InstanceName { get; set; } = "";

    /// <summary>字段名（如 "speed"）。</summary>
    public string FieldName { get; set; } = "";

    /// <summary>复合绑定 Id（如 "motor1.speed"）。</summary>
    public string CompoundId { get; set; } = "";

    public string DataType { get; set; } = "Int16";
    public string Address { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public string GroupId { get; set; } = "";

    /// <summary>归属子设备（从站）Id（继承自 UDT 实例）。</summary>
    public string SlaveId { get; set; } = "";
    public double Scale { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public string? Unit { get; set; }

    /// <summary>关联的 UDT 字段定义。</summary>
    public UdtField? UdtField { get; set; }
}
