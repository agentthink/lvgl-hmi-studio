namespace HmiEditor.Core.Models;

/// <summary>
/// 变量组 —— 用于组织分类变量（如 "PLC1_Tags", "HMI_Internal"）。
/// </summary>
public class VariableGroup : IIdentifiedModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "default";
}
