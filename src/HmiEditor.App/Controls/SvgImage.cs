using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Controls;

/// <summary>
/// SVG 图片显示控件 —— 解析 SVG 文本并直接渲染到 DrawingContext。
/// 用于 AI 助手面板中预览生成的 SVG 图片。
/// </summary>
public sealed class SvgImage : Control
{
    /// <summary>SVG 文本（设置后自动解析并刷新）。</summary>
    public static readonly StyledProperty<string?> SvgTextProperty =
        AvaloniaProperty.Register<SvgImage, string?>(nameof(SvgText));

    private SvgDocument? _doc;
    private double _scale = 1.0;

    public string? SvgText
    {
        get => GetValue(SvgTextProperty);
        set => SetValue(SvgTextProperty, value);
    }

    static SvgImage()
    {
        AffectsRender<SvgImage>(SvgTextProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SvgTextProperty)
        {
            ParseSvg();
            InvalidateMeasure();
        }
    }

    private void ParseSvg()
    {
        var text = SvgText;
        if (string.IsNullOrWhiteSpace(text))
        {
            _doc = null;
            return;
        }

        var (doc, _) = SvgParser.Parse(text);
        _doc = doc;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_doc is null or { Shapes.Count: 0 })
            return new Size(0, 0);

        var vbW = _doc.ViewBoxWidth > 0 ? _doc.ViewBoxWidth : _doc.Width;
        var vbH = _doc.ViewBoxHeight > 0 ? _doc.ViewBoxHeight : _doc.Height;
        if (vbW <= 0 || vbH <= 0) return new Size(0, 0);

        // 按可用空间等比缩放，最大 200x200
        var maxW = Math.Min(availableSize.Width, 200);
        var maxH = Math.Min(availableSize.Height, 200);
        if (maxW <= 0 || maxH <= 0) return new Size(vbW, vbH);

        _scale = Math.Min(maxW / vbW, maxH / vbH);
        return new Size(vbW * _scale, vbH * _scale);
    }

    public override void Render(DrawingContext context)
    {
        if (_doc is null or { Shapes.Count: 0 }) return;

        var vbW = _doc.ViewBoxWidth > 0 ? _doc.ViewBoxWidth : _doc.Width;
        var vbH = _doc.ViewBoxHeight > 0 ? _doc.ViewBoxHeight : _doc.Height;
        if (vbW <= 0 || vbH <= 0) return;

        var sx = (Bounds.Width > 0 ? Bounds.Width : vbW * _scale) / vbW;
        var sy = (Bounds.Height > 0 ? Bounds.Height : vbH * _scale) / vbH;

        foreach (var shape in _doc.Shapes)
        {
            var fill = shape.Fill is not null and not "none"
                ? ParseSvgColor(shape.Fill) : (Color?)null;
            var stroke = shape.Stroke is not null and not "none"
                ? ParseSvgColor(shape.Stroke) : (Color?)null;

            IBrush? fillBrush = fill.HasValue ? new SolidColorBrush(fill.Value) : null;
            IPen? strokePen = stroke.HasValue
                ? new Pen(new SolidColorBrush(stroke.Value), Math.Max(shape.StrokeWidth * sx, 0.5))
                : null;

            var geometry = BuildGeometry(shape, sx, sy);
            if (geometry != null)
                context.DrawGeometry(fillBrush, strokePen, geometry);
        }
    }

    /// <summary>从 SvgShape 构建 Avalonia Geometry（复用 DesignCanvas 同款逻辑）。</summary>
    private static Geometry? BuildGeometry(SvgShape shape, double sx, double sy)
    {
        switch (shape.Kind)
        {
            case SvgShapeKind.Rect:
            {
                var p = shape.Points;
                if (p.Length < 4) return null;
                var radius = p.Length >= 6 ? Math.Abs(p[4] * sx) : 0;
                return new RectangleGeometry(
                    new Rect(p[0] * sx, p[1] * sy, p[2] * sx, p[3] * sy),
                    radius, radius);
            }
            case SvgShapeKind.Circle:
            {
                var p = shape.Points;
                if (p.Length < 3) return null;
                var cx = p[0] * sx; var cy = p[1] * sy; var r = p[2] * sx;
                return new EllipseGeometry(new Rect(cx - r, cy - r, r * 2, r * 2));
            }
            case SvgShapeKind.Ellipse:
            {
                var p = shape.Points;
                if (p.Length < 4) return null;
                var cx = p[0] * sx; var cy = p[1] * sy;
                var rx = p[2] * sx; var ry = p[3] * sy;
                return new EllipseGeometry(new Rect(cx - rx, cy - ry, rx * 2, ry * 2));
            }
            case SvgShapeKind.Line:
            {
                var p = shape.Points;
                if (p.Length < 4) return null;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(p[0] * sx, p[1] * sy), false);
                    ctx.LineTo(new Point(p[2] * sx, p[3] * sy));
                    ctx.EndFigure(false);
                }
                return g;
            }
            case SvgShapeKind.Polyline:
            case SvgShapeKind.Polygon:
                return BuildPolygonGeometry(shape.Points, sx, sy,
                    shape.Kind == SvgShapeKind.Polygon);
            case SvgShapeKind.Path:
            {
                var subPaths = SvgPathFlattener.Flatten(shape.PathData);
                if (subPaths.Count == 0) return null;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    foreach (var sub in subPaths)
                    {
                        if (sub.Points.Count < 2) continue;
                        ctx.BeginFigure(
                            new Point(sub.Points[0] * sx, sub.Points[1] * sy),
                            sub.Closed);
                        for (int i = 2; i + 1 < sub.Points.Count; i += 2)
                            ctx.LineTo(new Point(sub.Points[i] * sx, sub.Points[i + 1] * sy));
                        ctx.EndFigure(sub.Closed);
                    }
                }
                return g;
            }
        }
        return null;
    }

    private static Geometry? BuildPolygonGeometry(double[] pts, double sx, double sy, bool closed)
    {
        if (pts.Length < 4) return null;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(pts[0] * sx, pts[1] * sy), closed);
            for (int i = 2; i + 1 < pts.Length; i += 2)
                ctx.LineTo(new Point(pts[i] * sx, pts[i + 1] * sy));
            ctx.EndFigure(closed);
        }
        return g;
    }

    /// <summary>解析 SVG 颜色字符串为 Avalonia Color。</summary>
    private static Color? ParseSvgColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = SvgParser.NormalizeColor(value);
        if (string.IsNullOrEmpty(normalized) || normalized == "none") return null;
        // #RRGGBB
        if (normalized!.StartsWith('#') && normalized.Length == 7)
        {
            var r = byte.Parse(normalized[1..3], System.Globalization.NumberStyles.HexNumber);
            var g = byte.Parse(normalized[3..5], System.Globalization.NumberStyles.HexNumber);
            var b = byte.Parse(normalized[5..7], System.Globalization.NumberStyles.HexNumber);
            return Color.FromArgb(255, r, g, b);
        }
        return null;
    }
}
