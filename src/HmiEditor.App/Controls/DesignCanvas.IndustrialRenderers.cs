using Avalonia;
using HmiEditor.App.Design;
using HmiEditor.App.Resources;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using System.IO;
using System.Linq;

namespace HmiEditor.App.Controls;

/// <summary>
/// DesignCanvas 分部 —— SVG 矢量渲染与工业设备图形。
/// Svg / Tank / Pipe / Valve 系列共用同一套 SVG path 几何解析
/// （BuildShapeGeometry / BuildPolygonGeometry / ParseSvgColor），故同域放置。
/// </summary>
public partial class DesignCanvas : Control
{
    // ── I6: SVG 矢量渲染 ──

    private static void RenderSvg(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        if (control.SvgShapes == null || control.SvgShapes.Count == 0)
        {
            // 无形状数据 → 占位轮廓
            var pen = RenderResourceCache.Pen(DesignPalette.SvgPlaceholder, 1 * z);
            context.DrawRectangle(null, pen, rect);
            return;
        }

        var vbW = control.SvgViewBoxWidth > 0 ? control.SvgViewBoxWidth : 100;
        var vbH = control.SvgViewBoxHeight > 0 ? control.SvgViewBoxHeight : 100;
        var sx = rect.Width / vbW;
        var sy = rect.Height / vbH;

        // 图形坐标以控件原点为基准绘制，再整体平移到控件位置，保证随控件移动/缩放
        using (context.PushTransform(Matrix.CreateTranslation(rect.X, rect.Y)))
        {
            foreach (var shape in control.SvgShapes)
            {
                // ① 仿射：与生成侧吃**同一份几何**，烘焙只有 SvgShapeBake 一处实现。
                //    画布本可以直接让 Avalonia 去旋转，但不走那条路 —— 两台渲染栈必须同源，
                //    否则"画布所见 == 设备所得"只能靠肉眼对（旋转变体还会连判据一起分叉）。
                var effective = SvgShapeBake.Transform(shape);
                var strokeScale = SvgShapeBake.Scale(shape.Transform);

                // ② 渐变判据必须用**烘焙前**的形状：SvgShapeBake.Transform 会把 Transform 清成 null，
                //    之后再判就永远得到"轴对齐"，含旋转的矩形会被错误地按渐变画出来。
                var gradientOk = shape.FillGradient != null
                    && SvgGradientGeometry.Decide(shape, shape.FillGradient) == SvgGradientEmit.RectGradient;

                var fill = effective.Fill is not null and not "none" ? ParseSvgColor(effective.Fill) : null;
                var stroke = effective.Stroke is not null and not "none" ? ParseSvgColor(effective.Stroke) : null;

                // 发不出真渐变（降级形状、描边渐变、停靠点不足）时，Fill/Stroke 里存的就是 50% 降级纯色
                // —— 解析器写的，画布照用即可，与设备一致。
                IBrush? fillBrush = gradientOk ? BuildSvgGradientBrush(shape, effective, sx, sy) : null;
                if (fillBrush is null && fill.HasValue)
                    fillBrush = RenderResourceCache.Brush(fill.Value);

                IPen? strokePen = stroke.HasValue
                    ? RenderResourceCache.Pen(stroke.Value, Math.Max(effective.StrokeWidth * strokeScale * sx, 0.5))
                    : null;

                var geometry = BuildShapeGeometry(effective, sx, sy);
                if (geometry != null)
                    context.DrawGeometry(fillBrush, strokePen, geometry);
            }
        }
    }

    /// <summary>
    /// 按 <see cref="SvgGradientGeometry.Resolve"/> 的结果造 Avalonia 渐变刷。返回 null 表示
    /// **发不出真渐变**，调用方退回纯色（与生成侧退回 <c>SvgGradientEmit.SolidFallback</c> 对称）。
    ///
    /// <para><b>坐标空间</b>：<c>Resolve</c> 的输出在"烘焙后的形状局部 viewBox 坐标"里，
    /// 所以这里乘 <paramref name="sx"/>/<paramref name="sy"/> 之后，与 <see cref="BuildShapeGeometry"/>
    /// 造出来的几何点**同处一个空间**。这一点是必须的：<c>PushTransform</c> 在录制/合成时表现为
    /// 父级 <c>DrawingGroup.Transform</c>、子节点坐标仍是局部的，画刷跟着几何一起被父级变换搬运
    /// —— 所以这里**不能**再加 <c>rect.X/rect.Y</c>，加了会偏移一个控件原点。</para>
    ///
    /// <para>径向按**单一半径**建刷（<c>RadiusX == RadiusY == Radius * sx</c>）：设备侧
    /// <c>lv_grad_radial_init</c> 只能表达圆、半径取 x 方向；画布若按 <c>sy</c> 另取一个，
    /// 非正方形控件上就会画成椭圆，两边当场不一致。</para>
    /// </summary>
    private static IBrush? BuildSvgGradientBrush(SvgShape shape, SvgShape effective, double sx, double sy)
    {
        var gradient = shape.FillGradient;
        if (gradient == null) return null;

        // 与发射侧同一条下限：lv_grad_init_stops 里有 LV_ASSERT(num_stops > 1)，少于 2 个停靠点发不出
        var stops = SvgGradientGeometry.NormalizeStops(gradient);
        if (stops.Count < 2) return null;

        var box = SvgGradientGeometry.BoundingBox(effective);
        if (box is null) return null;

        // 传**烘焙前**的 Transform：它正是 userSpaceOnUse 语义里"引用它的元素的用户坐标系"
        var g = SvgGradientGeometry.Resolve(gradient, box.Value, shape.Transform);

        var converted = new GradientStops();
        foreach (var s in stops)
        {
            var color = ParseSvgColor(s.Color);
            if (!color.HasValue) return null;
            converted.Add(new GradientStop(color.Value, Math.Clamp(s.Offset, 0, 1)));
        }

        if (gradient.Kind == SvgGradientKind.Linear)
        {
            // Avalonia 没有 WPF 那个 MappingMode：绝对/相对由每个点的 RelativeUnit 表达
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(g.X1 * sx, g.Y1 * sy, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(g.X2 * sx, g.Y2 * sy, RelativeUnit.Absolute),
                GradientStops = converted,
            };
        }

        var radius = Math.Max(1, g.Radius * sx);
        return new RadialGradientBrush
        {
            Center = new RelativePoint(g.X1 * sx, g.Y1 * sy, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(
                (g.HasFocal ? g.FocalX : g.X1) * sx,
                (g.HasFocal ? g.FocalY : g.Y1) * sy,
                RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(radius, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(radius, RelativeUnit.Absolute),
            GradientStops = converted,
        };
    }

    // ── I7: 过程图形控件渲染器 ──

    /// <summary>
    /// 罐体渲染：矩形罐壁 + 液位填充（从底部向上）+ 右侧刻度线。
    /// 预览模式下液位由绑定值驱动（ResolveProgress），编辑态由控件 Value 驱动。
    /// </summary>
    private static void RenderTank(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        const double margin = 4.0;
        var outerRect = rect;
        var innerRect = new Rect(
            outerRect.X + margin * z,
            outerRect.Y + margin * z,
            outerRect.Width - margin * 2 * z,
            outerRect.Height - margin * 2 * z);

        // 罐壁（矩形边框，圆角）
        var wallColor = ParseColorHex(control.BgColor, DesignPalette.Border);
        var wallPen = RenderResourceCache.Pen(wallColor, Math.Max(1.5, control.BorderWidth * z));
        context.DrawRectangle(null, wallPen, outerRect, (float)(control.Radius * z));

        // 液位填充（编辑态由控件 Value 驱动；预览态同 RenderGauge，此处设计时暂无 valueProvider）
        double fillProgress = control.MaxValue > control.MinValue
            ? Math.Clamp((control.Value - control.MinValue) / (control.MaxValue - control.MinValue), 0, 1)
            : 0.5;

        // 从底部向上填充
        var liquidTop = innerRect.Bottom - fillProgress * innerRect.Height;
        var liquidRect = new Rect(innerRect.X, liquidTop, innerRect.Width,
            Math.Max(fillProgress * innerRect.Height, z));
        var liquidColor = ParseColorHex(control.IndicatorColor, DesignPalette.TankLiquid);
        context.FillRectangle(RenderResourceCache.Brush(liquidColor, 0.75), liquidRect, 0);

        // 液面高光线
        if (fillProgress > 0.001 && fillProgress < 0.999)
        {
            var meniscusPen = RenderResourceCache.Pen(ParseColorHex(control.IndicatorColor, DesignPalette.SkyAccent), z);
            context.DrawLine(meniscusPen, new Point(innerRect.X, liquidTop), new Point(innerRect.Right, liquidTop));
        }

        // 右侧刻度
        if (control.TankShowScale && control.TankTickCount >= 2)
        {
            var tickColor = ParseColorHex(control.TextColor, DesignPalette.Text);
            var tickPen = RenderResourceCache.Pen(tickColor, z);
            var scaleH = 5 * z;
            for (int i = 0; i <= control.TankTickCount; i++)
            {
                var ratio = (double)i / control.TankTickCount;
                var y = innerRect.Bottom - ratio * innerRect.Height;
                context.DrawLine(tickPen, new Point(innerRect.Right, y), new Point(innerRect.Right + scaleH, y));
            }
            // 刻度标签（上下百分比）
            var ft100 = RenderResourceCache.Text(DefaultTexts.ScaleTop, 9 * z, RenderResourceCache.Brush(tickColor));
            context.DrawText(ft100, new Point(innerRect.Right + scaleH + 2 * z, innerRect.Y));
            var ft0 = RenderResourceCache.Text(DefaultTexts.ScaleBottom, 9 * z, RenderResourceCache.Brush(tickColor));
            context.DrawText(ft0, new Point(innerRect.Right + scaleH + 2 * z, innerRect.Bottom - ft0.Height));
        }

        // 位号文字
        var label = !string.IsNullOrEmpty(control.Text) ? control.Text : Services.ControlNameDefaults.Display(control);
        if (!string.IsNullOrEmpty(label))
        {
            var tickColor = ParseColorHex(control.TextColor, DesignPalette.Text);
            DrawCenteredText(context, label, outerRect, 10 * z, tickColor);
        }
    }

    /// <summary>
    /// 管道渲染：主线段（横向或纵向）+ 流向箭头 + 流动虚线段。
    /// PipeDirection=Vertical 时主轴为 Y；PipeFlowSpeed>0 时绘制流动示意。
    /// </summary>
    private static void RenderPipe(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var pipeColor = ParseColorHex(control.BgColor, DesignPalette.Slate500);
        var lineW = Math.Max(3, control.BorderWidth > 0 ? control.BorderWidth : 4) * z;
        var pipePen = RenderResourceCache.Pen(pipeColor, lineW);
        var arrowColor = ParseColorHex(control.IndicatorColor, DesignPalette.SkyAccent);
        var arrowPen = RenderResourceCache.Pen(arrowColor, lineW);
        var dir = control.PipeFlowDirection == "Reverse" ? -1 : 1;

        if (control.PipeDirection == "Vertical")
        {
            context.DrawLine(pipePen, new Point(rect.Center.X, rect.Top), new Point(rect.Center.X, rect.Bottom));
            var midY = rect.Center.Y;
            context.DrawLine(arrowPen, new Point(rect.Center.X, midY - 6 * z * dir), new Point(rect.Center.X, midY + 6 * z * dir));
            var tip = new Point(rect.Center.X, midY + 8 * z * dir);
            context.DrawLine(arrowPen, new Point(tip.X - 4 * z, tip.Y - 4 * z * dir), new Point(tip.X, tip.Y));
            context.DrawLine(arrowPen, new Point(tip.X + 4 * z, tip.Y - 4 * z * dir), new Point(tip.X, tip.Y));
        }
        else
        {
            context.DrawLine(pipePen, new Point(rect.Left, rect.Center.Y), new Point(rect.Right, rect.Center.Y));
            var midX = rect.Center.X;
            context.DrawLine(arrowPen, new Point(midX - 6 * z * dir, rect.Center.Y), new Point(midX + 6 * z * dir, rect.Center.Y));
            var tip = new Point(midX + 8 * z * dir, rect.Center.Y);
            context.DrawLine(arrowPen, new Point(tip.X - 4 * z * dir, tip.Y - 4 * z), new Point(tip.X, tip.Y));
            context.DrawLine(arrowPen, new Point(tip.X - 4 * z * dir, tip.Y + 4 * z), new Point(tip.X, tip.Y));
        }

        // 流动虚线段（PipeFlowSpeed>0 时静态示意）
        if (control.PipeFlowSpeed > 0)
        {
            var flowColor = ParseColorHex(control.IndicatorColor, DesignPalette.TankLiquid);
            var flowPen = RenderResourceCache.Pen(RenderResourceCache.Brush(flowColor, 0.9), lineW * 0.9);
            double seg = 8 * z, gap = 6 * z;
            if (control.PipeDirection == "Vertical")
            {
                var cx = rect.Center.X;
                for (double y = rect.Bottom; y > rect.Top; y -= seg + gap)
                    context.DrawLine(flowPen, new Point(cx, y), new Point(cx, y - seg * dir));
            }
            else
            {
                var cy = rect.Center.Y;
                for (double x = rect.Left; x < rect.Right; x += seg + gap)
                    context.DrawLine(flowPen, new Point(x, cy), new Point(x + seg * dir, cy));
            }
        }

        var label = !string.IsNullOrEmpty(control.Text) ? control.Text : Services.ControlNameDefaults.Display(control);
        if (!string.IsNullOrEmpty(label))
            DrawCenteredText(context, label, rect, 9 * z,
                ParseColorHex(control.TextColor, DesignPalette.Text));
    }

    /// <summary>
    /// 阀门渲染：四种阀型符号（Ball/Gate/Butterfly/Globe）+ 开/关状态色切换。
    /// 开=IndicatorColor(绿)，关=BgColor(红)。
    /// </summary>
    private static void RenderValve(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        var center = rect.Center;
        var lineW = Math.Max(2, control.BorderWidth > 0 ? control.BorderWidth : 3) * z;
        bool open = control.Value != 0;
        var bodyColor = ParseColorHex(control.BgColor, DesignPalette.Error);
        var openColor = ParseColorHex(control.IndicatorColor, DesignPalette.Success);
        var penColor = open ? openColor : bodyColor;
        var pen = RenderResourceCache.Pen(penColor, lineW);

        switch (control.ValveType?.ToLowerInvariant() ?? "ball")
        {
            case "gate":      RenderGateValve(context, pen, rect, center, lineW, open, z); break;
            case "butterfly": RenderButterflyValve(context, pen, rect, center, lineW, open, z); break;
            case "globe":     RenderGlobeValve(context, pen, rect, center, lineW, open, z); break;
            default:          RenderBallValve(context, pen, rect, center, lineW, open, z); break;
        }

        var label = !string.IsNullOrEmpty(control.Text) ? control.Text : Services.ControlNameDefaults.Display(control);
        if (!string.IsNullOrEmpty(label))
            DrawCenteredText(context, label, rect, 9 * z, penColor);
    }

    private static void RenderBallValve(DrawingContext context, IPen pen, Rect rect,
        Point center, double lineW, bool open, double z)
    {
        var r = Math.Min(rect.Width, rect.Height) / 2 * 0.55;
        context.DrawEllipse(null, pen, new Rect(center.X - r, center.Y - r, r * 2, r * 2));
        context.DrawLine(pen, new Point(rect.Left + 2 * z, center.Y), new Point(rect.Right - 2 * z, center.Y));
        var h = Math.Min(rect.Width, rect.Height) / 2 * 0.7;
        if (open)
            context.DrawLine(pen, new Point(center.X, center.Y - h), new Point(center.X, center.Y + h));
        else
            context.DrawLine(pen, new Point(center.X - h, center.Y), new Point(center.X + h, center.Y));
    }

    private static void RenderGateValve(DrawingContext context, IPen pen, Rect rect,
        Point center, double lineW, bool open, double z)
    {
        var r = Math.Min(rect.Width, rect.Height) / 2 * 0.5;
        context.DrawRectangle(null, pen, new Rect(center.X - r, center.Y - r, r * 2, r * 2));
        var plateY = open ? center.Y - r + 2 * z : center.Y;
        context.DrawLine(pen, new Point(center.X - r + 3 * z, plateY), new Point(center.X + r - 3 * z, plateY));
        context.DrawLine(pen, new Point(center.X, center.Y - r - 2 * z), new Point(center.X, plateY));
        context.DrawLine(pen, new Point(rect.Left + 2 * z, center.Y), new Point(rect.Right - 2 * z, center.Y));
    }

    private static void RenderButterflyValve(DrawingContext context, IPen pen, Rect rect,
        Point center, double lineW, bool open, double z)
    {
        var r = Math.Min(rect.Width, rect.Height) / 2 * 0.55;
        context.DrawEllipse(null, pen, new Rect(center.X - r, center.Y - r, r * 2, r * 2));
        var diskW = open ? r * 1.6 : r * 0.25;
        var diskH = open ? r * 0.25 : r * 1.6;
        context.DrawRectangle(null, pen, new Rect(center.X - diskW / 2, center.Y - diskH / 2, diskW, diskH));
        context.DrawLine(pen, new Point(center.X, center.Y - r - 2 * z), new Point(center.X, center.Y + r + 2 * z));
    }

    private static void RenderGlobeValve(DrawingContext context, IPen pen, Rect rect,
        Point center, double lineW, bool open, double z)
    {
        var r = Math.Min(rect.Width, rect.Height) / 2 * 0.5;
        var triH = r * 0.8;
        var triW = r * 0.6;
        // 下三角形（两个角朝下）
        var g1 = new StreamGeometry();
        using (var g = g1.Open()) { g.BeginFigure(new Point(center.X - triW, center.Y + triH), false); g.LineTo(new Point(center.X, center.Y), true); g.LineTo(new Point(center.X + triW, center.Y + triH), true); }
        context.DrawGeometry(null, pen, g1);
        // 上三角形（两个角朝上）
        var g2 = new StreamGeometry();
        using (var g = g2.Open()) { g.BeginFigure(new Point(center.X - triW, center.Y - triH), false); g.LineTo(new Point(center.X, center.Y), true); g.LineTo(new Point(center.X + triW, center.Y - triH), true); }
        context.DrawGeometry(null, pen, g2);
        // 阀杆：开=沿管道水平，关=斜 45°
        var rodLen = r * 0.9;
        if (open)
            context.DrawLine(pen, new Point(center.X - rodLen, center.Y), new Point(center.X + rodLen, center.Y));
        else
            context.DrawLine(pen, new Point(center.X - rodLen * 0.7, center.Y - rodLen * 0.7),
                              new Point(center.X + rodLen * 0.7, center.Y + rodLen * 0.7));
    }

    private static Geometry? BuildShapeGeometry(SvgShape shape, double sx, double sy)
    {
        // sx/sy 基于 rect 尺寸计算（已包含缩放 z），坐标直接乘 sx/sy 即可，避免双重缩放
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
                // Path 的仿射在**展平之后**应用 —— 几何在 PathData 字符串里，SvgShapeBake.Transform
                // 展平不了、会把 Path 原样返回（与生成侧 AppendPath 同一约定）。
                // 漏掉这一步，<g transform> 里的 path 会留在原地，而设备端已经把它搬走了。
                var m = shape.Transform;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    foreach (var sub in subPaths)
                    {
                        if (sub.Points.Count < 2) continue;
                        var pts = m is null
                            ? sub.Points.ToArray()
                            : SvgShapeBake.ApplyToPoints(sub.Points.ToArray(), m);
                        ctx.BeginFigure(new Point(pts[0] * sx, pts[1] * sy), sub.Closed);
                        for (int i = 2; i + 1 < pts.Length; i += 2)
                            ctx.LineTo(new Point(pts[i] * sx, pts[i + 1] * sy));
                        ctx.EndFigure(sub.Closed);
                    }
                }
                return g;
            }
            default:
                return null;
        }
    }

    private static Geometry BuildPolygonGeometry(double[] points, double sx, double sy,
        bool closed)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            if (points.Length < 2) return g;
            ctx.BeginFigure(new Point(points[0] * sx, points[1] * sy), closed);
            for (int i = 2; i + 1 < points.Length; i += 2)
                ctx.LineTo(new Point(points[i] * sx, points[i + 1] * sy));
            ctx.EndFigure(closed);
        }
        return g;
    }

    private static Color? ParseSvgColor(string? color)
    {
        if (string.IsNullOrEmpty(color) || color == "none") return null;
        try
        {
            var hex = color.TrimStart('#');
            if (hex.Length == 3)
                hex = string.Concat(hex.Select(c => $"{c}{c}"));
            return Color.Parse("#" + hex);
        }
        catch
        {
            return null;
        }
    }
}
