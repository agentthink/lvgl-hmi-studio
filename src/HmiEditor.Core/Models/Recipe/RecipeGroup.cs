using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models.Recipe;

/// <summary>
/// 配方分组 —— 用于配方管理界面的分类树展示。
/// </summary>
public class RecipeGroup
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>分组名称</summary>
    public string Name { get; set; } = "";

    /// <summary>分组内的配方列表</summary>
    public ObservableCollection<Recipe> Recipes { get; set; } = new();
}
