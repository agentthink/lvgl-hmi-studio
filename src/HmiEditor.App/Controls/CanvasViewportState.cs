using Avalonia;

namespace HmiEditor.App.Controls;

/// <summary>画布视口状态与鼠标中心缩放计算，避免缩放逻辑散落在事件处理器中。</summary>
public readonly record struct CanvasViewportState(double Zoom, double PanX, double PanY)
{
    public CanvasViewportState ZoomAt(Point anchor, double requestedZoom)
    {
        var oldZoom = Math.Clamp(Zoom, 0.25, 4.0);
        var newZoom = Math.Clamp(requestedZoom, 0.25, 4.0);
        if (Math.Abs(oldZoom - newZoom) < double.Epsilon) return this;
        var ratio = newZoom / oldZoom;
        return new CanvasViewportState(newZoom,
            anchor.X - (anchor.X - PanX) * ratio,
            anchor.Y - (anchor.Y - PanY) * ratio);
    }
}
