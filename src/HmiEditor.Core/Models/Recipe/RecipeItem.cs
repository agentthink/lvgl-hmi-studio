namespace HmiEditor.Core.Models.Recipe;

/// <summary>
/// 配方条目 —— 一个变量的预设值配置。
/// </summary>
public class RecipeItem
{
    /// <summary>绑定的变量 ID</summary>
    public string VariableId { get; set; } = "";

    /// <summary>条目名称（如"目标温度"）</summary>
    public string Label { get; set; } = "";

    /// <summary>默认值</summary>
    public double DefaultValue { get; set; }

    /// <summary>最小值（校验用）</summary>
    public double MinValue { get; set; } = double.MinValue;

    /// <summary>最大值（校验用）</summary>
    public double MaxValue { get; set; } = double.MaxValue;

    /// <summary>单位（如"°C"、"bar"）</summary>
    public string? Unit { get; set; }

    /// <summary>数据类型</summary>
    public string DataType { get; set; } = "Float";

    /// <summary>排序序号</summary>
    public int SortOrder { get; set; }
}
