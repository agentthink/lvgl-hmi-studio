namespace HmiEditor.Core.Models.Trend;

/// <summary>
/// 趋势图曲线笔 —— 绑定一个变量到趋势图的一条曲线。
/// 对标 WinCC Trend Control 的曲线配置。
/// </summary>
public class TrendPen
{
    /// <summary>曲线名称（显示在图例中）</summary>
    public string Label { get; set; } = "";

    /// <summary>绑定的变量 ID</summary>
    public string VariableId { get; set; } = "";

    /// <summary>曲线颜色（#RRGGBB）</summary>
    public string Color { get; set; } = "#38bdf8";

    /// <summary>线宽（px）</summary>
    public int LineWidth { get; set; } = 2;

    /// <summary>是否显示数据点标记</summary>
    public bool ShowPoints { get; set; } = false;

    /// <summary>Y 轴范围最小值（auto = null）</summary>
    public double? YAxisMin { get; set; }

    /// <summary>Y 轴范围最大值（auto = null）</summary>
    public double? YAxisMax { get; set; }

    /// <summary>是否启用此曲线</summary>
    public bool IsEnabled { get; set; } = true;
}
