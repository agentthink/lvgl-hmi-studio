using Avalonia;
using HmiEditor.Core.Design;

namespace HmiEditor.App.Controls;

/// <summary>画布视口坐标上下文，集中封装缩放/平移后的坐标转换。</summary>
public readonly record struct CanvasCoordinateContext(double Zoom, double PanX, double PanY)
{
    public Point ScreenToModel(Point point)
    {
        var (x, y) = CoordinateTransform.ScreenToModel(point.X, point.Y, PanX, PanY, Zoom);
        return new Point(x, y);
    }

    public Point ModelToScreen(Point point)
    {
        var (x, y) = CoordinateTransform.ModelToScreen(point.X, point.Y, PanX, PanY, Zoom);
        return new Point(x, y);
    }

    public Rect ModelToScreen(Rect rect)
    {
        var topLeft = ModelToScreen(rect.Position);
        return new Rect(topLeft, new Size(rect.Width * Math.Max(Zoom, CoordinateTransform.MinZoom), rect.Height * Math.Max(Zoom, CoordinateTransform.MinZoom)));
    }
}
