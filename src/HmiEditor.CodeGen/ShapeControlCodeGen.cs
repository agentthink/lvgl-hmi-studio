using System.Globalization;
using System.Text;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;

namespace HmiEditor.CodeGen;

/// <summary>
/// I10: 基础绘图图形的 LVGL 代码生成器。
/// Ellipse 基于 lv_canvas 组合绘制（与 Svg/过程图形同路径）：
///   - 填充：采样 32 段椭圆 → lv_draw_polygon 多边形近似
///   - 描边：闭合采样折线 → lv_draw_line
/// Rectangle / Polyline / Arc 无需 canvas（lv_obj / lv_line / lv_arc），
/// 由 CodeGenerator 通用路径生成。
/// </summary>
public static class ShapeControlCodeGen
{
    /// <summary>生成 canvas 像素缓冲的文件级声明。</summary>
    public static string GenerateBufferDeclaration(HmiControl control)
    {
        var w = (int)Math.Max(control.Width, 1);
        var h = (int)Math.Max(control.Height, 1);
        return $"static uint8_t _shapebuf_{CIdentifier.ForControlSymbol(control.Id)}[LV_CANVAS_BUF_SIZE({w}, {h}, 32, LV_DRAW_BUF_STRIDE_ALIGN)];";
    }

    /// <summary>生成单个绘图图形的 C 创建代码（Ellipse）。</summary>
    public static string GenerateCode(HmiControl control, string parent = "screen")
    {
        return control.Type switch
        {
            HmiControlType.Ellipse => GenerateEllipseCode(control, parent),
            _ => $"// Unsupported shape control type: {control.Type}",
        };
    }

    /// <summary>
    /// 椭圆：canvas 图层内绘制填充 + 描边椭圆（内接于控件矩形）。
    /// 填充色 = BgColor（兜底 Surface 深灰），描边 = BorderColor（兜底 Border 灰）。
    /// </summary>
    private static string GenerateEllipseCode(HmiControl c, string parent)
    {
        var sb = new StringBuilder();
        // 控件 id 同时进三类符号名，构造规则必须统一走 CIdentifier：
        //   obj_<净化id>      文件级 static —— 此处赋值，CodeGenerator.Screen.cs 声明
        //   _shapebuf_<净化id> 文件级 static —— GenerateBufferDeclaration 声明，此处引用
        //   layer_<净化id>     函数内局部 —— 声明与引用都在本函数
        // 前两类跨函数/跨文件，两处若用不同的 id 形态（裸 vs 净化）就会符号对不上。
        var obj = CIdentifier.ControlObject(c.Id);
        var id = CIdentifier.ForControlSymbol(c.Id);
        var w = (int)Math.Max(c.Width, 1);
        var h = (int)Math.Max(c.Height, 1);
        var cx = c.Width / 2.0;
        var cy = c.Height / 2.0;
        var rx = Math.Max(0.5, c.Width / 2.0);
        var ry = Math.Max(0.5, c.Height / 2.0);

        sb.AppendLine($"    {obj} = lv_canvas_create({parent});");
        sb.AppendLine($"    lv_obj_set_pos({obj}, {Num(c.X)}, {Num(c.Y)});");
        sb.AppendLine($"    lv_obj_set_size({obj}, {w}, {h});");
        sb.AppendLine($"    lv_canvas_set_buffer({obj}, _shapebuf_{id}, {w}, {h}, LV_COLOR_FORMAT_ARGB8888);");
        // LVGL 9.x 无 lv_canvas_set_px_opa；透明填充等价实现。
        sb.AppendLine($"    lv_canvas_fill_bg({obj}, lv_color_black(), LV_OPA_TRANSP);");
        sb.AppendLine($"    lv_obj_remove_flag({obj}, LV_OBJ_FLAG_SCROLLABLE);");
        sb.AppendLine($"    lv_layer_t layer_{id};");
        sb.AppendLine($"    lv_canvas_init_layer({obj}, &layer_{id});");

        var hasFill = !string.IsNullOrEmpty(c.BgColor);
        var hasStroke = c.BorderWidth > 0;

        // 采样椭圆：32 段，闭合
        var pts = SvgShapeBake.SampleEllipse(cx, cy, rx, ry);

        if (hasFill)
        {
            var n = pts.Count / 2;
            sb.AppendLine($"    {{");
            sb.AppendLine($"        lv_draw_triangle_dsc_t dsc;");
            sb.AppendLine($"        lv_draw_triangle_dsc_init(&dsc);");
            sb.AppendLine($"        dsc.color = lv_color_hex(0x{Hex(c.BgColor!)});");
            sb.AppendLine($"        dsc.opa = LV_OPA_COVER;");
            AppendPointsArray(sb, pts);
            // LVGL 9.x 无 lv_draw_polygon；三角扇近似填充。
            sb.AppendLine($"        for (int i = 1; i + 1 < {n}; i++) {{");
            sb.AppendLine($"            dsc.p[0] = pts[0];");
            sb.AppendLine($"            dsc.p[1] = pts[i];");
            sb.AppendLine($"            dsc.p[2] = pts[i + 1];");
            sb.AppendLine($"            lv_draw_triangle(&layer_{id}, &dsc);");
            sb.AppendLine($"        }}");
            sb.AppendLine($"    }}");
        }

        if (hasStroke)
        {
            // 闭合描边：追加起点使轮廓闭合
            var closed = new List<double>(pts);
            closed.Add(pts[0]);
            closed.Add(pts[1]);
            sb.AppendLine($"    {{");
            AppendPointsArray(sb, closed);
            sb.AppendLine($"        lv_draw_line_dsc_t dsc;");
            sb.AppendLine($"        lv_draw_line_dsc_init(&dsc);");
            sb.AppendLine($"        dsc.color = lv_color_hex(0x{Hex(c.BorderColor ?? "64748b")});");
            sb.AppendLine($"        dsc.width = {Num(Math.Max(1, c.BorderWidth))};");
            sb.AppendLine($"        dsc.opa = LV_OPA_COVER;");
            // LVGL 9.x：lv_draw_line(layer, dsc)，折线走 dsc.points/point_cnt
            sb.AppendLine($"        dsc.points = pts; dsc.point_cnt = {closed.Count / 2};");
            sb.AppendLine($"        lv_draw_line(&layer_{id}, &dsc);");
            sb.AppendLine($"    }}");
        }

        sb.AppendLine($"    lv_canvas_finish_layer({obj}, &layer_{id});");
        return sb.ToString();
    }

    private static void AppendPointsArray(StringBuilder sb, List<double> pts)
    {
        var sb2 = new StringBuilder();
        for (var i = 0; i + 1 < pts.Count; i += 2)
        {
            if (sb2.Length > 0) sb2.Append(", ");
            sb2.Append($"{{{Num(pts[i])}, {Num(pts[i + 1])}}}");
        }
        sb.AppendLine($"        lv_point_precise_t pts[] = {{{sb2}}};");
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
}
