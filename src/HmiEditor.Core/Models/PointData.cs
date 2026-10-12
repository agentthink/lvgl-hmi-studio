namespace HmiEditor.Core.Models;

/// <summary>
/// 绘图顶点 —— 用于 Polyline（折线）等需要多点几何的控件。
/// 坐标为控件局部坐标系（0..Width / 0..Height），与控件尺寸解耦，
/// 缩放控件时顶点按比例跟随（渲染与代码生成均按 Width/Height 归一化映射）。
/// </summary>
public record PointData(double X, double Y);
