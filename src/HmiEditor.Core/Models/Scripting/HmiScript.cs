namespace HmiEditor.Core.Models.Scripting;

/// <summary>
/// 脚本 —— 一段可执行的 Lua 代码片段。
/// </summary>
public class HmiScript
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>脚本名称</summary>
    public string Name { get; set; } = "";

    /// <summary>脚本内容（Lua 源码）</summary>
    public string Code { get; set; } = "";

    /// <summary>分类（OnClick / OnValueChanged / OnPress / Timer / Alarm）</summary>
    public string Category { get; set; } = "OnClick";

    /// <summary>脚本描述</summary>
    public string? Description { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后修改时间</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>脚本版本</summary>
    public int Version { get; set; } = 1;
}
