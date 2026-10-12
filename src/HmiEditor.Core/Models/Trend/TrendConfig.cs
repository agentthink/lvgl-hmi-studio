namespace HmiEditor.Core.Models.Trend;

/// <summary>
/// 趋势图配置 —— 一个 LVGL Chart 控件的完整配置。
/// 可包含多条曲线（TrendPen），支持实时/历史模式。
/// </summary>
public class TrendConfig
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>关联的控件 ID（lv_chart 控件）</summary>
    public string ControlId { get; set; } = "";

    /// <summary>趋势图名称</summary>
    public string Name { get; set; } = "";

    /// <summary>时间窗口（毫秒），默认 60 秒</summary>
    public int TimeWindowMs { get; set; } = 60000;

    /// <summary>最大数据点数（环形缓冲区容量）</summary>
    public int MaxPoints { get; set; } = 600;

    /// <summary>曲线列表</summary>
    public List<TrendPen> Pens { get; set; } = new();

    /// <summary>是否显示图例</summary>
    public bool ShowLegend { get; set; } = true;

    /// <summary>是否显示网格</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>是否显示 X 轴刻度</summary>
    public bool ShowXAxis { get; set; } = true;

    /// <summary>是否显示 Y 轴刻度</summary>
    public bool ShowYAxis { get; set; } = true;

    /// <summary>背景色</summary>
    public string? BgColor { get; set; }

    /// <summary>是否启用实时刷新</summary>
    public bool IsRealtime { get; set; } = true;

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;
}
