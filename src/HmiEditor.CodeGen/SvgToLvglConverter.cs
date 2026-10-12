using System.Globalization;
using System.Text;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;

namespace HmiEditor.CodeGen;

/// <summary>
/// I6: SVG → LVGL v9 代码转换器。
/// 将 HmiControl(Svg) 的形状列表生成为 lv_canvas 绘制代码:
///   - 文件级: canvas 像素缓冲静态数组
///   - 创建函数内: lv_canvas_create + lv_canvas_set_buffer + 图层绘制序列
/// 形状映射: Rect→lv_draw_rect, 线类→lv_draw_line, 面类(圆/椭圆/多边形/闭合路径)
///           →lv_draw_polygon 线段近似(复用 SvgPathFlattener)。
/// 曲线已由展平器细分为线段,保证嵌入式端无需 path 渲染支持。
/// </summary>
public static class SvgToLvglConverter
{
    /// <summary>生成 canvas 像素缓冲的文件级声明。</summary>
    public static string GenerateBufferDeclaration(HmiControl control)
    {
        var w = (int)Math.Max(control.Width, 1);
        var h = (int)Math.Max(control.Height, 1);
        // 必须按「字节」而不是 lv_color_t 个数分配：LVGL 9.x 的 lv_color_t 是 3 字节
        // (blue/green/red)，而 canvas 用 LV_COLOR_FORMAT_ARGB8888 需要 4 B/px。
        // 旧写法 `static lv_color_t buf[w*h]` 会少分配 25% → 越界写。
        // LV_CANVAS_BUF_SIZE 是 LVGL 官方推荐写法（lv_example_canvas_6.c / docs）。
        return $"static uint8_t buf_{CIdentifier.ForControlSymbol(control.Id)}[LV_CANVAS_BUF_SIZE({w}, {h}, 32, LV_DRAW_BUF_STRIDE_ALIGN)];";
    }

    /// <summary>生成控件创建 + 全部形状绘制代码（含图层生命周期）。</summary>
    public static string GenerateCanvasCode(HmiControl control, string parent = "screen")
    {
        var sb = new StringBuilder();
        // 控件 id 同时进三类符号名，构造规则必须统一走 CIdentifier：
        //   obj_<净化id>   文件级 static —— 此处赋值，CodeGenerator.Screen.cs 声明
        //   buf_<净化id>   文件级 static —— GenerateBufferDeclaration 声明，此处引用
        //   layer_<净化id> 函数内局部 —— 声明与引用都在本函数（经 id 参数传给各 Append*）
        // 前两类跨函数/跨文件，两处若用不同的 id 形态（裸 vs 净化）就会符号对不上。
        var obj = CIdentifier.ControlObject(control.Id);
        var id = CIdentifier.ForControlSymbol(control.Id);
        var w = (int)Math.Max(control.Width, 1);
        var h = (int)Math.Max(control.Height, 1);

        sb.AppendLine($"    {obj} = lv_canvas_create({parent});");
        sb.AppendLine($"    lv_obj_set_pos({obj}, {Num(control.X)}, {Num(control.Y)});");
        sb.AppendLine($"    lv_obj_set_size({obj}, {w}, {h});");
        sb.AppendLine($"    lv_canvas_set_buffer({obj}, buf_{id}, {w}, {h}, LV_COLOR_FORMAT_ARGB8888);");
        // LVGL 9.x 没有 lv_canvas_set_px_opa；用 fill_bg 透明填充等价实现。
        sb.AppendLine($"    lv_canvas_fill_bg({obj}, lv_color_black(), LV_OPA_TRANSP);");
        sb.AppendLine($"    lv_obj_remove_flag({obj}, LV_OBJ_FLAG_SCROLLABLE);");
        sb.AppendLine($"    lv_layer_t layer_{id};");
        sb.AppendLine($"    lv_canvas_init_layer({obj}, &layer_{id});");

        var vbW = control.SvgViewBoxWidth > 0 ? control.SvgViewBoxWidth : 100;
        var vbH = control.SvgViewBoxHeight > 0 ? control.SvgViewBoxHeight : 100;
        var sx = control.Width / vbW;
        var sy = control.Height / vbH;

        if (control.SvgShapes != null)
        {
            foreach (var shape in control.SvgShapes)
            {
                AppendShape(sb, id, shape, sx, sy);
            }
        }

        sb.AppendLine($"    lv_canvas_finish_layer({obj}, &layer_{id});");
        return sb.ToString();
    }

    private static void AppendShape(StringBuilder sb, string id, SvgShape shape, double sx, double sy)
    {
        // 渐变判据必须在**烘焙之前**用原始形状判定：烘焙会把旋转消掉（Transform=null），
        // 之后再判就永远得到"轴对齐"，旋转矩形会被错误地按渐变矩形发出去。
        var gradientOk = shape.FillGradient != null
            && SvgGradientGeometry.Decide(shape, shape.FillGradient) == SvgGradientEmit.RectGradient;

        var effective = SvgShapeBake.Transform(shape);
        var strokeScale = SvgShapeBake.Scale(shape.Transform);

        var fill = effective.Fill is not null and not "none";
        var stroke = effective.Stroke is not null and not "none";
        if (!fill && !stroke) return;

        var sw = effective.StrokeWidth * strokeScale;

        switch (effective.Kind)
        {
            case SvgShapeKind.Rect:
                if (fill && gradientOk &&
                    TryEmitGradientRoundRect(sb, id, effective, shape.Transform, sx, sy, out var radius))
                {
                    if (stroke) AppendRectStroke(sb, id, effective, sx, sy, sw);
                    break;
                }
                AppendRect(sb, id, effective, sx, sy, fill, stroke, sw);
                break;
            case SvgShapeKind.Circle:
                if (fill && gradientOk &&
                    TryEmitGradientRoundRect(sb, id, effective, shape.Transform, sx, sy, out _))
                {
                    if (stroke) AppendCircleStroke(sb, id, effective, sx, sy, sw);
                    break;
                }
                AppendCircle(sb, id, effective, sx, sy, fill, stroke, sw);
                break;
            case SvgShapeKind.Ellipse:
                if (fill && gradientOk && SvgGradientGeometry.IsCircle(effective) &&
                    TryEmitGradientRoundRect(sb, id, effective, shape.Transform, sx, sy, out _))
                {
                    if (stroke) AppendEllipseStroke(sb, id, effective, sx, sy, sw);
                    break;
                }
                AppendEllipse(sb, id, effective, sx, sy, fill, stroke, sw);
                break;
            case SvgShapeKind.Line:
                AppendLine(sb, id, effective, sx, sy, sw);
                break;
            case SvgShapeKind.Polyline:
                AppendPolyline(sb, id, effective, sx, sy, fill: false, stroke, sw);
                break;
            case SvgShapeKind.Polygon:
                AppendPolyline(sb, id, effective, sx, sy, fill, stroke, sw);
                break;
            case SvgShapeKind.Path:
                AppendPath(sb, id, effective, sx, sy, fill, stroke, sw);
                break;
        }
    }

    /// <summary>
    /// 发射矩形/圆角矩形的**真渐变**填充。返回 false 表示发不出来（调用方退回纯色）。
    ///
    /// <para>两种"发不出来"必须在这里挡住：① 停靠点少于 2 个 ——
    /// <c>lv_grad_init_stops</c> 里有 <c>LV_ASSERT(num_stops &gt; 1)</c>；
    /// ② 半径取整成 0 —— 径向 setup 里有 <c>LV_ASSERT(r_end != 0)</c>（取 max(1, r) 兜住）。</para>
    ///
    /// <para><b>端点坐标必须相对绘制区域左上角</b>：<c>lv_draw_sw_grad_linear_setup</c> 里
    /// <c>start.x = lv_pct_to_px(start.x, wdt)</c> 用的是**区域宽高**，而 <c>..._get_line</c>
    /// 拿到的是 <c>clipped - bg_coords</c>（区域相对量）；两端不同源会让渐变整体偏移一个区域原点。径向同理。</para>
    ///
    /// <para><b>直接写进 <c>dsc.bg_grad</c>，不要先造一个局部 <c>lv_grad_dsc_t</c> 再整体赋值</b>：
    /// <c>lv_draw_rect_dsc_init</c> 会 <c>lv_memzero</c> 整个 dsc（并把 bg_grad 预置成白→黑两点），
    /// 而四个 <c>lv_grad_*_init</c> **只写自己那几个字段** —— 结构体尾部还有一个
    /// <c>void * state</c>（渲染器拿它挂预计算结果句柄，<c>*_cleanup</c> 会 <c>lv_free</c> 它）。
    /// 用未初始化的局部量再整体赋值，等于把这个野指针搬进去；写进已被清零的 dsc 字段则天然是 NULL。</para>
    /// </summary>
    private static bool TryEmitGradientRoundRect(StringBuilder sb, string id, SvgShape shape,
        double[]? elementTransform, double sx, double sy, out double canvasRadius)
    {
        canvasRadius = 0;
        var gradient = shape.FillGradient;
        if (gradient == null) return false;

        var stops = SvgGradientGeometry.NormalizeStops(gradient);
        if (stops.Count < 2) return false;

        var box = SvgGradientGeometry.BoundingBox(shape);
        if (box == null) return false;

        double radius;
        if (shape.Kind == SvgShapeKind.Rect)
        {
            var p = shape.Points;
            if (p.Length < 4) return false;
            radius = p.Length >= 6 ? p[4] * sx : 0;
        }
        else
        {
            var p = shape.Points;
            if (p.Length < 3) return false;
            radius = p[2] * sx;          // Circle，或 rx == ry 的 Ellipse
        }

        var box2 = box.Value;
        var areaX = box2.X * sx;
        var areaY = box2.Y * sy;
        var areaW = box2.Width * sx;
        var areaH = box2.Height * sy;

        var g = SvgGradientGeometry.Resolve(gradient, box2, elementTransform);

        sb.AppendLine($"    {{");
        sb.AppendLine($"        lv_draw_rect_dsc_t dsc;");
        sb.AppendLine($"        lv_draw_rect_dsc_init(&dsc);");

        if (g.Kind == SvgGradientKind.Linear)
        {
            var x1 = g.X1 * sx - areaX;
            var y1 = g.Y1 * sy - areaY;
            var x2 = g.X2 * sx - areaX;
            var y2 = g.Y2 * sy - areaY;
            sb.AppendLine($"        lv_grad_linear_init(&dsc.bg_grad, {Int(x1)}, {Int(y1)}, {Int(x2)}, {Int(y2)}, LV_GRAD_EXTEND_PAD);");
        }
        else
        {
            var cx = g.X1 * sx - areaX;
            var cy = g.Y1 * sy - areaY;
            var r = Math.Max(1, g.Radius * sx);
            // 半径写进 to 点：lv_grad_radial_init 把它存为 end_extent，setup 里
            // r_end = |end_extent - end| 必须非 0（有 LV_ASSERT）。
            sb.AppendLine($"        lv_grad_radial_init(&dsc.bg_grad, {Int(cx)}, {Int(cy)}, {Int(cx + r)}, {Int(cy)}, LV_GRAD_EXTEND_PAD);");
            if (g.HasFocal)
            {
                // focal 半径 = 0：SVG 的 fx/fy 是点，LVGL 的 focal 是圆 —— 半径 0 即 r_start = 0（同心起点）。
                sb.AppendLine($"        lv_grad_radial_set_focal(&dsc.bg_grad, {Int(g.FocalX * sx - areaX)}, {Int(g.FocalY * sy - areaY)}, 0);");
            }
        }

        var colors = string.Join(", ", stops.Select(s => $"lv_color_hex(0x{Hex(s.Color)})"));
        var fracs = string.Join(", ", stops.Select(s => ((int)Math.Round(s.Offset * 255)).ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine($"        lv_color_t grad_colors[] = {{{colors}}};");
        sb.AppendLine($"        uint8_t grad_fracs[] = {{{fracs}}};");
        // opa 传 NULL ⇒ lv_grad_init_stops 给每个停靠点填 LV_OPA_COVER。
        // 全部不透明时 lv_draw_rect 判定 bg_cover = true，走不带遮罩的快路。
        sb.AppendLine($"        lv_grad_init_stops(&dsc.bg_grad, grad_colors, NULL, grad_fracs, {stops.Count});");
        sb.AppendLine($"        dsc.radius = {Num(radius)};");
        sb.AppendLine($"        lv_area_t a = {{{Num(areaX)}, {Num(areaY)}, {Num(areaX + areaW)}, {Num(areaY + areaH)}}};");
        sb.AppendLine($"        lv_draw_rect(&layer_{id}, &dsc, &a);");
        sb.AppendLine($"    }}");

        canvasRadius = radius;
        return true;
    }

    private static void AppendRect(StringBuilder sb, string id, SvgShape shape,
        double sx, double sy, bool fill, bool stroke, double sw)
    {
        if (fill) AppendRectFill(sb, id, shape, sx, sy);
        if (stroke) AppendRectStroke(sb, id, shape, sx, sy, sw);
    }

    private static void AppendRectFill(StringBuilder sb, string id, SvgShape shape, double sx, double sy)
    {
        var p = shape.Points;
        if (p.Length < 4) return;
        double x = p[0], y = p[1], w = p[2], h = p[3];
        double rx = p.Length >= 6 ? p[4] : 0;
        if (shape.Fill is null) return;

        sb.AppendLine($"    {{");
        sb.AppendLine($"        lv_draw_rect_dsc_t dsc;");
        sb.AppendLine($"        lv_draw_rect_dsc_init(&dsc);");
        sb.AppendLine($"        dsc.bg_color = lv_color_hex(0x{Hex(shape.Fill)});");
        sb.AppendLine($"        dsc.bg_opa = LV_OPA_COVER;");
        sb.AppendLine($"        dsc.radius = {Num(rx * sx)};");
        sb.AppendLine($"        lv_area_t a = {{{Num(x * sx)}, {Num(y * sy)}, {Num((x + w) * sx)}, {Num((y + h) * sy)}}};");
        sb.AppendLine($"        lv_draw_rect(&layer_{id}, &dsc, &a);");
        sb.AppendLine($"    }}");
    }

    private static void AppendRectStroke(StringBuilder sb, string id, SvgShape shape,
        double sx, double sy, double sw)
    {
        var p = shape.Points;
        if (p.Length < 4) return;
        double x = p[0], y = p[1], w = p[2], h = p[3];
        if (shape.Stroke is null) return;

        sb.AppendLine($"    {{");
        sb.AppendLine($"        lv_point_precise_t pts[] = {{{{{Num(x * sx)}, {Num(y * sy)}}}, {{{Num((x + w) * sx)}, {Num(y * sy)}}}, {{{Num((x + w) * sx)}, {Num((y + h) * sy)}}}, {{{Num(x * sx)}, {Num((y + h) * sy)}}}, {{{Num(x * sx)}, {Num(y * sy)}}}}};");
        AppendLineDsc(sb, shape.Stroke, sw);
        // LVGL 9.x：lv_draw_line(layer, dsc)，折线走 dsc.points/point_cnt
        sb.AppendLine($"        dsc.points = pts; dsc.point_cnt = 5;");
        sb.AppendLine($"        lv_draw_line(&layer_{id}, &dsc);");
        sb.AppendLine($"    }}");
    }

    private static void AppendCircle(StringBuilder sb, string id, SvgShape shape,
        double sx, double sy, bool fill, bool stroke, double sw)
    {
        var p = shape.Points;
        if (p.Length < 3) return;

        if (fill && shape.Fill is not null)
        {
            AppendPolygonFill(sb, id, SvgShapeBake.SampleEllipse(p[0], p[1], p[2], p[2]), sx, sy, shape.Fill);
        }
        if (stroke) AppendCircleStroke(sb, id, shape, sx, sy, sw);
    }

    private static void AppendCircleStroke(StringBuilder sb, string id, SvgShape shape,
        double sx, double sy, double sw)
    {
        var p = shape.Points;
        if (p.Length < 3 || shape.Stroke is null) return;
        double cx = p[0], cy = p[1], r = p[2];

        sb.AppendLine($"    {{");
        sb.AppendLine($"        lv_draw_arc_dsc_t dsc;");
        sb.AppendLine($"        lv_draw_arc_dsc_init(&dsc);");
        sb.AppendLine($"        dsc.color = lv_color_hex(0x{Hex(shape.Stroke)});");
        sb.AppendLine($"        dsc.width = {Num(sw)};");
        sb.AppendLine($"        dsc.opa = LV_OPA_COVER;");
        sb.AppendLine($"        dsc.center.x = {(int)Math.Round(cx * sx)}; dsc.center.y = {(int)Math.Round(cy * sy)};");
        sb.AppendLine($"        dsc.radius = {(int)Math.Round(Math.Max(1, r * sx))};");
        sb.AppendLine($"        dsc.start_angle = 0;");
        sb.AppendLine($"        dsc.end_angle = 360;");
        sb.AppendLine($"        lv_draw_arc(&layer_{id}, &dsc);");
        sb.AppendLine($"    }}");
    }

    private static void AppendEllipse(StringBuilder sb, string id, SvgShape shape,
        double sx, double sy, bool fill, bool stroke, double sw)
    {
        var p = shape.Points;
        if (p.Length < 4) return;

        if (fill && shape.Fill is not null)
        {
            AppendPolygonFill(sb, id, SvgShapeBake.SampleEllipse(p[0], p[1], p[2], p[3]), sx, sy, shape.Fill);
        }
        if (stroke) AppendEllipseStroke(sb, id, shape, sx, sy, sw);
    }

    private static void AppendEllipseStroke(StringBuilder sb, string id, SvgShape shape,
        double sx, double sy, double sw)
    {
        var p = shape.Points;
        if (p.Length < 4 || shape.Stroke is null) return;
        double cx = p[0], cy = p[1], rx = p[2], ry = p[3];

        var pts = SvgShapeBake.SampleEllipse(cx, cy, rx, ry);
        // 闭合描边: 追加起点
        pts.Add(pts[0]);
        pts.Add(pts[1]);
        AppendLinePolyline(sb, id, pts, sx, sy, shape.Stroke, sw);
    }

    private static void AppendLine(StringBuilder sb, string id, SvgShape shape,
        double sx, double sy, double sw)
    {
        var p = shape.Points;
        if (p.Length < 4) return;
        if (shape.Stroke is null || shape.Stroke == "none") return;

        sb.AppendLine($"    {{");
        AppendLineDsc(sb, shape.Stroke, sw);
        sb.AppendLine($"        dsc.p1.x = {Num(p[0] * sx)}; dsc.p1.y = {Num(p[1] * sy)};");
        sb.AppendLine($"        dsc.p2.x = {Num(p[2] * sx)}; dsc.p2.y = {Num(p[3] * sy)};");
        sb.AppendLine($"        lv_draw_line(&layer_{id}, &dsc);");
        sb.AppendLine($"    }}");
    }

    private static void AppendPolyline(StringBuilder sb, string id, SvgShape shape,
        double sx, double sy, bool fill, bool stroke, double sw)
    {
        var pts = new List<double>(shape.Points);
        if (pts.Count < 4) return;

        if (fill && shape.Fill is not null)
        {
            var closed = new List<double>(pts);
            closed.Add(pts[0]);
            closed.Add(pts[1]);
            AppendPolygonFill(sb, id, closed, sx, sy, shape.Fill);
        }
        if (stroke && shape.Stroke is not null)
        {
            var closed = new List<double>(pts);
            closed.Add(pts[0]);
            closed.Add(pts[1]);
            AppendLinePolyline(sb, id, closed, sx, sy, shape.Stroke, sw);
        }
    }

    private static void AppendPath(StringBuilder sb, string id, SvgShape shape,
        double sx, double sy, bool fill, bool stroke, double sw)
    {
        var subPaths = SvgPathFlattener.Flatten(shape.PathData);
        foreach (var sub in subPaths)
        {
            if (sub.Points.Count < 4) continue;

            // Path 的仿射在这里应用（几何是字符串，BakeTransform 展平不了 ⇒ 它把 Path 原样返回）
            var pts = shape.Transform == null
                ? new List<double>(sub.Points)
                : new List<double>(SvgShapeBake.ApplyToPoints(sub.Points.ToArray(), shape.Transform));

            if (fill && shape.Fill is not null)
            {
                AppendPolygonFill(sb, id, new List<double>(pts), sx, sy, shape.Fill);
            }
            if (stroke && shape.Stroke is not null)
            {
                AppendLinePolyline(sb, id, new List<double>(pts), sx, sy, shape.Stroke, sw);
            }
        }
    }

    private static void AppendPolygonFill(StringBuilder sb, string id,
        List<double> pts, double sx, double sy, string color)
    {
        int n = pts.Count / 2;
        if (n < 3) return;

        // LVGL 9.x 没有 lv_draw_polygon；用「以首顶点为中心的三角扇」近似填充
        // （凸多边形完全等价，凹多边形可能有轻微过绘，优于无法编译）。
        sb.AppendLine($"    {{");
        sb.AppendLine($"        lv_draw_triangle_dsc_t dsc;");
        sb.AppendLine($"        lv_draw_triangle_dsc_init(&dsc);");
        sb.AppendLine($"        dsc.color = lv_color_hex(0x{Hex(color)});");
        sb.AppendLine($"        dsc.opa = LV_OPA_COVER;");
        AppendPointsArray(sb, pts, sx, sy);
        sb.AppendLine($"        for (int i = 1; i + 1 < {n}; i++) {{");
        sb.AppendLine($"            dsc.p[0] = pts[0];");
        sb.AppendLine($"            dsc.p[1] = pts[i];");
        sb.AppendLine($"            dsc.p[2] = pts[i + 1];");
        sb.AppendLine($"            lv_draw_triangle(&layer_{id}, &dsc);");
        sb.AppendLine($"        }}");
        sb.AppendLine($"    }}");
    }

    private static void AppendLinePolyline(StringBuilder sb, string id,
        List<double> pts, double sx, double sy, string color, double sw)
    {
        if (pts.Count < 4) return;

        sb.AppendLine($"    {{");
        AppendPointsArray(sb, pts, sx, sy);
        AppendLineDsc(sb, color, sw);
        sb.AppendLine($"        dsc.points = pts; dsc.point_cnt = {pts.Count / 2};");
        sb.AppendLine($"        lv_draw_line(&layer_{id}, &dsc);");
        sb.AppendLine($"    }}");
    }

    private static void AppendPointsArray(StringBuilder sb, List<double> pts,
        double sx, double sy)
    {
        var sb2 = new StringBuilder();
        for (int i = 0; i + 1 < pts.Count; i += 2)
        {
            if (sb2.Length > 0) sb2.Append(", ");
            sb2.Append($"{{{Num(pts[i] * sx)}, {Num(pts[i + 1] * sy)}}}");
        }
        sb.AppendLine($"        lv_point_precise_t pts[] = {{{sb2}}};");
    }

    private static void AppendLineDsc(StringBuilder sb, string color, double sw)
    {
        sb.AppendLine($"        lv_draw_line_dsc_t dsc;");
        sb.AppendLine($"        lv_draw_line_dsc_init(&dsc);");
        sb.AppendLine($"        dsc.color = lv_color_hex(0x{Hex(color)});");
        sb.AppendLine($"        dsc.width = {Num(sw)};");
        sb.AppendLine($"        dsc.opa = LV_OPA_COVER;");
    }

    private static string Hex(string color)
    {
        var c = color.TrimStart('#');
        if (c.Length == 3)
            c = string.Concat(c.Select(ch => $"{ch}{ch}"));
        return c.ToUpperInvariant();
    }

    private static string Num(double v)
        => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>整数像素 —— 渐变端点/圆心是 <c>int32_t</c>，与 <c>lv_area_t</c> 同一坐标系。</summary>
    private static string Int(double v)
        => ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
}
