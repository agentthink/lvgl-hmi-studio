using Avalonia;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Controls;

/// <summary>画布框选策略。纯选择规则与视觉/指针事件解耦。</summary>
public static class CanvasSelectionPolicy
{
    public static IReadOnlyList<HmiControl> SelectFromRubberBand(
        IEnumerable<HmiControl> controls,
        Rect selectionRect,
        bool leftToRight,
        double zoom,
        double panX,
        double panY)
    {
        var z = Math.Max(zoom, 0.01);
        var selected = new List<HmiControl>();
        foreach (var control in controls)
        {
            if (!control.IsVisible || control.IsLocked) continue;
            var rect = new Rect(control.X * z + panX, control.Y * z + panY,
                control.Width * z, control.Height * z);
            var matches = leftToRight
                ? selectionRect.Contains(rect.TopLeft) && selectionRect.Contains(rect.BottomRight)
                : selectionRect.Intersects(rect);
            if (matches) selected.Add(control);
        }
        return selected;
    }
}
