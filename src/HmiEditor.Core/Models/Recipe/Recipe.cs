namespace HmiEditor.Core.Models.Recipe;

/// <summary>
/// 配方 —— 一组变量预设值的集合。
/// 对标 Weintek EasyBuilder Pro / Siemens WinCC 配方功能。
/// 每个配方代表"一种产品的生产工艺参数"。
/// </summary>
public class Recipe
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>配方名称（如"产品A-高温模式"）</summary>
    public string Name { get; set; } = "";

    /// <summary>所属分组（如"温度控制"）</summary>
    public string GroupName { get; set; } = "";

    /// <summary>版本号（每次修改递增）</summary>
    public int Version { get; set; } = 1;

    /// <summary>配方条目列表</summary>
    public List<RecipeItem> Items { get; set; } = new();

    /// <summary>配方描述</summary>
    public string? Description { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后修改时间</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>是否为预置配方（不可删除）</summary>
    public bool IsBuiltIn { get; set; }
}
