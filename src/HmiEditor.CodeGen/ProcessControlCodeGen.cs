using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;
using System;
using System.Globalization;
using System.Text;

namespace HmiEditor.CodeGen;

/// <summary>
/// I7 过程图形控件的 LVGL 代码生成器。
/// Tank / Pipe / Valve 基于 lv_canvas 组合绘制，与 Svg 控件同路径（layer 绘制 API）。
/// 绘制内容与设计器 DesignCanvas.RenderTank/RenderPipe/RenderValve 一一对应，
/// 保证设计器所见 = 模拟器所得：
///   Tank   : 罐壁圆角矩形 + 液位填充（Value 进度）+ 液面高光线 + 右侧刻度
///   Pipe   : 主轴线段 + 流向箭头（含翼尖）+ 流动虚线段（PipeFlowSpeed>0）
///   Valve  : 四种阀型符号（Ball/Gate/Butterfly/Globe）+ 开/关状态色切换
/// 文本（位号/刻度标签）因 canvas 文本依赖字体资源，以注释声明。
///
/// 生成物分为两部分（调用方必须分别放到正确位置）：
///   - <see cref="GenerateCanvasCode"/>：控件创建 + 图层绘制，放在 create_screen_*() 函数体内；
///   - <see cref="GenerateUpdateFunction"/>：运行时更新函数，放在**文件作用域**
///     （放进函数体内会成为嵌套函数定义，GCC 报 "存储类无效"）。
///   - <see cref="GenerateCode"/>：两者拼接，仅供单元测试/兼容旧调用。
/// </summary>
public static class ProcessControlCodeGen
{
    /// <summary>生成 canvas 像素缓冲的文件级声明。</summary>
    public static string GenerateBufferDeclaration(HmiControl control)
    {
        var w = (int)Math.Max(control.Width, 1);
        var h = (int)Math.Max(control.Height, 1);
        return $"static uint8_t _buf_{CIdentifier.ForControlSymbol(control.Id)}[LV_CANVAS_BUF_SIZE({w}, {h}, 32, LV_DRAW_BUF_STRIDE_ALIGN)];";
    }

    /// <summary>兼容入口：canvas 绘制 + 更新函数拼接（两者作用域不同，生产代码请用拆分方法）。</summary>
    public static string GenerateCode(HmiControl control)
        => GenerateCanvasCode(control) + GenerateUpdateFunction(control);

    /// <summary>控件创建 + 图层绘制代码（放入 create_screen_*() 函数体内）。</summary>
    public static string GenerateCanvasCode(HmiControl control)
    {
        return control.Type switch
        {
            HmiControlType.Tank  => GenerateTankCode(control),
            HmiControlType.Pipe  => GeneratePipeCode(control),
            HmiControlType.Valve => GenerateValveCode(control),
            _ => $"// Unsupported control type: {control.Type}\n",
        };
    }

    /// <summary>运行时更新函数（必须放在文件作用域，不能嵌入其它函数体内）。</summary>
    public static string GenerateUpdateFunction(HmiControl control)
    {
        return control.Type switch
        {
            HmiControlType.Tank  => GenerateTankUpdate(control),
            HmiControlType.Pipe  => GeneratePipeUpdate(control),
            HmiControlType.Valve => GenerateValveUpdate(control),
            _ => string.Empty,
        };
    }

    // ── 公共绘制辅助 ───────────────────────────────────────────────────────

    /// <summary>打开 canvas 图层（缓冲绑定 + 透明底色）。</summary>
    /// <param name="id">
    /// **已净化**的控件 id（调用方须先过 <see cref="CIdentifier.ForControlSymbol"/>）：
    /// 本方法用它拼 <c>_buf_*</c>（文件级 static，声明在
    /// <see cref="GenerateBufferDeclaration"/>）与 <c>layer_*</c>（函数内局部）。
    /// </param>
    private static void OpenCanvas(StringBuilder sb, string id, HmiControl c, string parent)
    {
        var w = (int)Math.Max(c.Width, 1);
        var h = (int)Math.Max(c.Height, 1);
        // obj_* 是文件级 static（声明在 CodeGenerator.Screen.cs），只从原始 id 现场构造，
        // 不经 id 参数中转 —— 见 CIdentifier.ControlObject 的说明。
        var obj = CIdentifier.ControlObject(c.Id);
        sb.AppendLine($"    {obj} = lv_canvas_create({parent});");
        sb.AppendLine($"    lv_obj_set_pos({obj}, {Num(c.X)}, {Num(c.Y)});");
        sb.AppendLine($"    lv_obj_set_size({obj}, {w}, {h});");
        sb.AppendLine($"    lv_canvas_set_buffer({obj}, _buf_{id}, {w}, {h}, LV_COLOR_FORMAT_ARGB8888);");
        // LVGL 9.x 无 lv_canvas_set_px_opa；透明填充等价实现。
        sb.AppendLine($"    lv_canvas_fill_bg({obj}, lv_color_black(), LV_OPA_TRANSP);");
        sb.AppendLine($"    lv_obj_remove_flag({obj}, LV_OBJ_FLAG_SCROLLABLE);");
        sb.AppendLine($"    lv_layer_t layer_{id};");
        sb.AppendLine($"    lv_canvas_init_layer({obj}, &layer_{id});");
    }

    private static void CloseCanvas(StringBuilder sb, string id, HmiControl c)
        => sb.AppendLine($"    lv_canvas_finish_layer({CIdentifier.ControlObject(c.Id)}, &layer_{id});");

    /// <summary>lv_draw_rect：填充 + 可选描边 + 圆角。</summary>
    private static void AppendDrawRect(StringBuilder sb, string id, double x, double y,
        double w, double h, string fillHex, double radius, string? borderHex = null, double borderW = 0)
    {
        sb.AppendLine($"    {{");
        sb.AppendLine($"        lv_draw_rect_dsc_t dsc;");
        sb.AppendLine($"        lv_draw_rect_dsc_init(&dsc);");
        sb.AppendLine($"        dsc.bg_color = lv_color_hex(0x{Hex(fillHex)});");
        sb.AppendLine($"        dsc.bg_opa = LV_OPA_COVER;");
        sb.AppendLine($"        dsc.radius = {Num(radius)};");
        if (!string.IsNullOrEmpty(borderHex) && borderW > 0)
        {
            sb.AppendLine($"        dsc.border_color = lv_color_hex(0x{Hex(borderHex)});");
            sb.AppendLine($"        dsc.border_width = {Num(borderW)};");
            sb.AppendLine($"        dsc.border_opa = LV_OPA_COVER;");
        }
        sb.AppendLine($"        lv_area_t a = {{{Num(x)}, {Num(y)}, {Num(x + w)}, {Num(y + h)}}};");
        sb.AppendLine($"        lv_draw_rect(&layer_{id}, &dsc, &a);");
        sb.AppendLine($"    }}");
    }

    /// <summary>
    /// lv_draw_line：单段线段。
    /// LVGL 9.x 的 lv_draw_line 只接受 (layer, dsc)，几何必须写进 dsc.p1/p2
    /// （旧实现的 4 参形式在 9.x 中不存在，会 "too many arguments" 编译失败）。
    /// </summary>
    private static void AppendDrawLine(StringBuilder sb, string id,
        double x1, double y1, double x2, double y2, string colorHex, double width)
    {
        sb.AppendLine($"    {{");
        sb.AppendLine($"        lv_draw_line_dsc_t dsc;");
        sb.AppendLine($"        lv_draw_line_dsc_init(&dsc);");
        sb.AppendLine($"        dsc.color = lv_color_hex(0x{Hex(colorHex)});");
        sb.AppendLine($"        dsc.width = {Num(Math.Max(1, width))};");
        sb.AppendLine($"        dsc.opa = LV_OPA_COVER;");
        sb.AppendLine($"        dsc.p1.x = {Num(x1)}; dsc.p1.y = {Num(y1)};");
        sb.AppendLine($"        dsc.p2.x = {Num(x2)}; dsc.p2.y = {Num(y2)};");
        sb.AppendLine($"        lv_draw_line(&layer_{id}, &dsc);");
        sb.AppendLine($"    }}");
    }

    /// <summary>
    /// lv_draw_arc：圆弧/圆（sweep 为扫过角度，360 表示整圆）。
    /// LVGL 9.x 同样只接受 (layer, dsc)，几何写进 dsc.center/radius/start_angle/end_angle。
    /// </summary>
    private static void AppendDrawArc(StringBuilder sb, string id,
        double cx, double cy, double r, double start, double sweep, string colorHex, double width)
    {
        sb.AppendLine($"    {{");
        sb.AppendLine($"        lv_draw_arc_dsc_t dsc;");
        sb.AppendLine($"        lv_draw_arc_dsc_init(&dsc);");
        sb.AppendLine($"        dsc.color = lv_color_hex(0x{Hex(colorHex)});");
        sb.AppendLine($"        dsc.width = {Num(Math.Max(1, width))};");
        sb.AppendLine($"        dsc.opa = LV_OPA_COVER;");
        sb.AppendLine($"        dsc.center.x = {(int)Math.Round(cx)}; dsc.center.y = {(int)Math.Round(cy)};");
        sb.AppendLine($"        dsc.radius = {(int)Math.Round(Math.Max(1, r))};");
        sb.AppendLine($"        dsc.start_angle = {Num(start)};");
        sb.AppendLine($"        dsc.end_angle = {Num(start + sweep)};");
        sb.AppendLine($"        lv_draw_arc(&layer_{id}, &dsc);");
        sb.AppendLine($"    }}");
    }

    /// <summary>多边形填充（Globe 阀三角形）：LVGL 9.x 无多边形 API，用三角扇近似。</summary>
    private static void AppendDrawPolygon(StringBuilder sb, string id,
        double[] pts, string colorHex)
    {
        if (pts.Length < 6) return;
        var n = pts.Length / 2;
        sb.AppendLine($"    {{");
        var sb2 = new StringBuilder();
        for (var i = 0; i + 1 < pts.Length; i += 2)
        {
            if (sb2.Length > 0) sb2.Append(", ");
            sb2.Append($"{{{Num(pts[i])}, {Num(pts[i + 1])}}}");
        }
        sb.AppendLine($"        lv_point_precise_t pts[] = {{{sb2}}};");
        sb.AppendLine($"        lv_draw_triangle_dsc_t dsc;");
        sb.AppendLine($"        lv_draw_triangle_dsc_init(&dsc);");
        sb.AppendLine($"        dsc.color = lv_color_hex(0x{Hex(colorHex)});");
        sb.AppendLine($"        dsc.opa = LV_OPA_COVER;");
        sb.AppendLine($"        for (int i = 1; i + 1 < {n}; i++) {{");
        sb.AppendLine($"            dsc.p[0] = pts[0];");
        sb.AppendLine($"            dsc.p[1] = pts[i];");
        sb.AppendLine($"            dsc.p[2] = pts[i + 1];");
        sb.AppendLine($"            lv_draw_triangle(&layer_{id}, &dsc);");
        sb.AppendLine($"        }}");
        sb.AppendLine($"    }}");
    }

    // ── Tank ─────────────────────────────────────────────────────────────────

    private static string GenerateTankCode(HmiControl c)
    {
        var sb = new StringBuilder();
        // 本类的 id 一律取「已净化」形态，因为它要进四类 C 符号名：
        //   obj_<id>    文件级 static，CodeGenerator.Screen.cs 声明 ← 由 CIdentifier.ControlObject 构造
        //   _buf_<id>   文件级 static，GenerateBufferDeclaration 声明、OpenCanvas 引用
        //   layer_<id>  函数内局部，声明与引用同在 OpenCanvas
        //   update_tank_<id> 等函数名
        // 其中前三类跨函数/跨文件；两处若用不同的 id 形态（裸 vs 净化）就会符号对不上。
        var id = CIdentifier.ForControlSymbol(c.Id);
        var W = c.Width;
        var H = c.Height;
        var margin = 4.0;

        OpenCanvas(sb, id, c, "screen");

        // 罐壁：圆角矩形边框（与设计器 RenderTank wallPen 一致：BgColor 兜底 Border）
        var wallColor = !string.IsNullOrEmpty(c.BgColor) ? c.BgColor.TrimStart('#') : "64748b";
        var wallW = Math.Max(1.5, c.BorderWidth > 0 ? c.BorderWidth : 1.5);
        AppendDrawRect(sb, id, 0, 0, W, H, wallColor, c.Radius, wallColor, wallW);

        // 液位填充：从底部向上，进度 = (Value-Min)/(Max-Min)，颜色 IndicatorColor 兜底 TankLiquid
        var liquidColor = !string.IsNullOrEmpty(c.IndicatorColor) ? c.IndicatorColor.TrimStart('#') : "0ea5e9";
        var range = c.MaxValue - c.MinValue;
        var progress = range > 0 ? Math.Clamp((c.Value - c.MinValue) / range, 0, 1) : 0.5;
        var liquidH = Math.Max(1, progress * (H - margin * 2));
        var liquidTop = H - margin - liquidH;
        AppendDrawRect(sb, id, margin, liquidTop, W - margin * 2, liquidH, liquidColor, 0);

        // 液面高光线（进度非 0/100 时）
        if (progress > 0.001 && progress < 0.999)
        {
            var meniscusColor = !string.IsNullOrEmpty(c.IndicatorColor) ? c.IndicatorColor.TrimStart('#') : "38bdf8";
            AppendDrawLine(sb, id, margin, liquidTop, W - margin, liquidTop, meniscusColor, 1);
        }

        // 右侧刻度（TankShowScale && TankTickCount>=2），颜色 TextColor 兜底白
        if (c.TankShowScale && c.TankTickCount >= 2)
        {
            var tickColor = !string.IsNullOrEmpty(c.TextColor) ? c.TextColor.TrimStart('#') : "ffffff";
            var scaleH = 5.0;
            for (var i = 0; i <= c.TankTickCount; i++)
            {
                var ratio = (double)i / c.TankTickCount;
                var y = H - margin - ratio * (H - margin * 2);
                AppendDrawLine(sb, id, W - margin, y, W - margin + scaleH, y, tickColor, 1);
            }
            // 刻度标签（100%/0%）—— canvas 文本依赖字体资源，运行时以注释声明
            sb.AppendLine($"    // Tank 刻度标签 100%/0%：运行时经 lv_canvas_draw_text 绘制（需字体资源）");
        }

        // 位号文字 —— 设计器居中显示 Text；canvas 文本依赖字体，注释声明
        if (!string.IsNullOrEmpty(c.Text))
            sb.AppendLine($"    // Tank 位号 \"{EscapeC(c.Text)}\"：运行时居中绘制（canvas 文本需字体资源）");

        CloseCanvas(sb, id, c);
        return sb.ToString();
    }

    private static string GenerateTankUpdate(HmiControl c)
    {
        var sb = new StringBuilder();
        var id = CIdentifier.ForControlSymbol(c.Id);
        var H = c.Height;
        var margin = 4.0;
        // 运行时更新函数：液位随绑定值刷新
        sb.AppendLine($"static void update_tank_{id}(double value)");
        sb.AppendLine("{");
        sb.AppendLine($"    (void)value;");
        sb.AppendLine($"    lv_obj_invalidate({CIdentifier.ControlObject(c.Id)});");
        sb.AppendLine($"    // 运行时重绘液位：高度 = (value - {c.MinValue:0.#}) / ({c.MaxValue:0.#} - {c.MinValue:0.#}) * ({H - margin * 2:0.#})");
        sb.AppendLine("}");
        sb.AppendLine();
        return sb.ToString();
    }

    // ── Pipe ─────────────────────────────────────────────────────────────────

    private static string GeneratePipeCode(HmiControl c)
    {
        var sb = new StringBuilder();
        var id = CIdentifier.ForControlSymbol(c.Id);
        var W = c.Width;
        var H = c.Height;
        var vertical = c.PipeDirection == "Vertical";

        OpenCanvas(sb, id, c, "screen");

        var pipeColor = !string.IsNullOrEmpty(c.BgColor) ? c.BgColor.TrimStart('#') : "64748b";
        var lineW = Math.Max(3, c.BorderWidth > 0 ? c.BorderWidth : 4);
        var arrowColor = !string.IsNullOrEmpty(c.IndicatorColor) ? c.IndicatorColor.TrimStart('#') : "38bdf8";
        var dir = c.PipeFlowDirection == "Reverse" ? -1 : 1;

        if (vertical)
        {
            var cx = W / 2;
            AppendDrawLine(sb, id, cx, 0, cx, H, pipeColor, lineW);
            // 流向箭头（与设计器 RenderPipe 一致：主轴箭头 + 翼尖）
            var midY = H / 2;
            AppendDrawLine(sb, id, cx, midY - 6 * dir, cx, midY + 6 * dir, arrowColor, lineW);
            var tip = midY + 8 * dir;
            AppendDrawLine(sb, id, cx - 4, tip - 4 * dir, cx, tip, arrowColor, lineW);
            AppendDrawLine(sb, id, cx + 4, tip - 4 * dir, cx, tip, arrowColor, lineW);
        }
        else
        {
            var cy = H / 2;
            AppendDrawLine(sb, id, 0, cy, W, cy, pipeColor, lineW);
            var midX = W / 2;
            AppendDrawLine(sb, id, midX - 6 * dir, cy, midX + 6 * dir, cy, arrowColor, lineW);
            var tip = midX + 8 * dir;
            AppendDrawLine(sb, id, tip - 4 * dir, cy - 4, tip, cy, arrowColor, lineW);
            AppendDrawLine(sb, id, tip - 4 * dir, cy + 4, tip, cy, arrowColor, lineW);
        }

        // 流动虚线段（PipeFlowSpeed>0 时静态示意，与设计器 RenderPipe 一致）
        if (c.PipeFlowSpeed > 0)
        {
            var flowColor = !string.IsNullOrEmpty(c.IndicatorColor) ? c.IndicatorColor.TrimStart('#') : "0ea5e9";
            var flowW = lineW * 0.9;
            const double seg = 8, gap = 6;
            if (vertical)
            {
                var cx = W / 2;
                for (var y = H; y > 0; y -= seg + gap)
                    AppendDrawLine(sb, id, cx, y, cx, y - seg * dir, flowColor, flowW);
            }
            else
            {
                var cy = H / 2;
                for (double x = 0; x < W; x += seg + gap)
                    AppendDrawLine(sb, id, x, cy, x + seg * dir, cy, flowColor, flowW);
            }
        }

        // 位号文字
        if (!string.IsNullOrEmpty(c.Text))
            sb.AppendLine($"    // Pipe 位号 \"{EscapeC(c.Text)}\"：运行时居中绘制（canvas 文本需字体资源）");

        CloseCanvas(sb, id, c);
        return sb.ToString();
    }

    private static string GeneratePipeUpdate(HmiControl c)
    {
        var sb = new StringBuilder();
        var id = CIdentifier.ForControlSymbol(c.Id);
        // 运行时更新：流向动画由 lv_anim 驱动
        if (c.PipeFlowSpeed > 0)
        {
            sb.AppendLine($"// 流动动画周期: {c.PipeFlowSpeed}ms（运行时由 lv_anim 驱动，方向: {(c.PipeFlowDirection == "Reverse" ? "反向" : "正向")}）");
        }
        sb.AppendLine($"static void update_pipe_{id}(bool enabled)");
        sb.AppendLine("{");
        sb.AppendLine("    (void)enabled;");
        sb.AppendLine($"    lv_obj_invalidate({CIdentifier.ControlObject(c.Id)});");
        sb.AppendLine("}");
        sb.AppendLine();
        return sb.ToString();
    }

    // ── Valve ────────────────────────────────────────────────────────────────

    private static string GenerateValveCode(HmiControl c)
    {
        var sb = new StringBuilder();
        var id = CIdentifier.ForControlSymbol(c.Id);
        var W = c.Width;
        var H = c.Height;

        OpenCanvas(sb, id, c, "screen");

        var lineW = Math.Max(2, c.BorderWidth > 0 ? c.BorderWidth : 3);
        var open = c.Value != 0;
        var bodyColor = !string.IsNullOrEmpty(c.BgColor) ? c.BgColor.TrimStart('#') : "ef4444";
        var openColor = !string.IsNullOrEmpty(c.IndicatorColor) ? c.IndicatorColor.TrimStart('#') : "22c55e";
        var penColor = open ? openColor : bodyColor;

        var cx = W / 2;
        var cy = H / 2;
        var r = Math.Min(W, H) / 2;

        switch (c.ValveType?.ToLowerInvariant() ?? "ball")
        {
            case "gate":
                // 闸阀：矩形闸体 + 闸板（开=顶部 / 关=中部）+ 阀杆 + 管道线
                var gr = r * 0.5;
                AppendDrawRect(sb, id, cx - gr, cy - gr, gr * 2, gr * 2, penColor, 0, penColor, lineW);
                var plateY = open ? cy - gr + 2 : cy;
                AppendDrawLine(sb, id, cx - gr + 3, plateY, cx + gr - 3, plateY, penColor, lineW);
                AppendDrawLine(sb, id, cx, cy - gr - 2, cx, plateY, penColor, lineW);
                AppendDrawLine(sb, id, 2, cy, W - 2, cy, penColor, lineW);
                break;
            case "butterfly":
                // 蝶阀：圆体 + 蝶盘（开=水平 / 关=垂直）+ 阀杆
                var br = r * 0.55;
                AppendDrawArc(sb, id, cx, cy, br, 0, 360, penColor, lineW);
                var diskW = open ? br * 1.6 : br * 0.25;
                var diskH = open ? br * 0.25 : br * 1.6;
                AppendDrawRect(sb, id, cx - diskW / 2, cy - diskH / 2, diskW, diskH, penColor, 0, penColor, lineW);
                AppendDrawLine(sb, id, cx, cy - br - 2, cx, cy + br + 2, penColor, lineW);
                break;
            case "globe":
                // 截止阀：上下两个背对三角形 + 阀杆（开=水平 / 关=斜45°）
                var triH = r * 0.4;
                var triW = r * 0.3;
                AppendDrawPolygon(sb, id, new[] { cx - triW, cy + triH, cx, cy, cx + triW, cy + triH }, penColor);
                AppendDrawPolygon(sb, id, new[] { cx - triW, cy - triH, cx, cy, cx + triW, cy - triH }, penColor);
                var rodLen = r * 0.45;
                if (open)
                    AppendDrawLine(sb, id, cx - rodLen, cy, cx + rodLen, cy, penColor, lineW);
                else
                    AppendDrawLine(sb, id, cx - rodLen * 0.7, cy - rodLen * 0.7, cx + rodLen * 0.7, cy + rodLen * 0.7, penColor, lineW);
                break;
            default:
                // 球阀：圆体 + 管道线 + 手柄（开=垂直 / 关=水平）
                var ballR = r * 0.55;
                AppendDrawArc(sb, id, cx, cy, ballR, 0, 360, penColor, lineW);
                AppendDrawLine(sb, id, 2, cy, W - 2, cy, penColor, lineW);
                var h = r * 0.7;
                if (open)
                    AppendDrawLine(sb, id, cx, cy - h, cx, cy + h, penColor, lineW);
                else
                    AppendDrawLine(sb, id, cx - h, cy, cx + h, cy, penColor, lineW);
                break;
        }

        // 位号文字
        if (!string.IsNullOrEmpty(c.Text))
            sb.AppendLine($"    // Valve 位号 \"{EscapeC(c.Text)}\"：运行时居中绘制（canvas 文本需字体资源）");

        CloseCanvas(sb, id, c);
        return sb.ToString();
    }

    private static string GenerateValveUpdate(HmiControl c)
    {
        var sb = new StringBuilder();
        var id = CIdentifier.ForControlSymbol(c.Id);
        var open = c.Value != 0;
        var bodyColor = !string.IsNullOrEmpty(c.BgColor) ? c.BgColor.TrimStart('#') : "ef4444";
        var openColor = !string.IsNullOrEmpty(c.IndicatorColor) ? c.IndicatorColor.TrimStart('#') : "22c55e";
        // 运行时更新：状态色切换
        sb.AppendLine($"static void update_valve_{id}(bool open)");
        sb.AppendLine("{");
        sb.AppendLine("    (void)open;");
        sb.AppendLine($"    lv_obj_invalidate({CIdentifier.ControlObject(c.Id)});");
        sb.AppendLine($"    // 状态色: {(open ? "OPEN" : "CLOSED")} → {(open ? openColor : bodyColor)}");
        sb.AppendLine("}");
        sb.AppendLine();
        return sb.ToString();
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

    private static string EscapeC(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", " ").Replace("\n", " ");
}
