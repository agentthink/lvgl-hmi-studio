namespace HmiEditor.Core.Models.Trend;

/// <summary>
/// 趋势数据采样点 —— 一条采样记录（多变量值 + 时间戳）。
/// 引擎内部的环形缓冲区基本单元。
/// </summary>
public class TrendSample
{
    /// <summary>采样时间戳</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>变量值列表（与 DataLogConfig.VariableIds 顺序对齐）</summary>
    public List<double> Values { get; set; } = new();

    /// <summary>是否为压缩采样点（变化量编码）</summary>
    public bool IsCompressed { get; set; } = false;
}
