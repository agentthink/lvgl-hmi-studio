using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HmiEditor.Core.Design;
using HmiEditor.Core.Mapping;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Animation;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using HmiEditor.Core.CodeGen;

namespace HmiEditor.CodeGen;

/// <summary>
/// CodeGenerator 分部 —— Slider/Bar 刻度与指针指示器 —— 刻度数与刻度发射、knob/游标小三角、文件级指针声明与值回调。
/// </summary>
public partial class CodeGenerator
{
    /// <summary>
    /// 计算 Slider/Bar 刻度线数量（与设计器 ShowTicks 渲染同规则）：
    /// 步长档位数 + 1（含两端），钳制在 2-21 条。
    /// </summary>
    private static int SliderTickCount(HmiControl control)
        => control.MaxValue > control.MinValue && control.StepValue > 0
            ? Math.Clamp((int)((control.MaxValue - control.MinValue) / control.StepValue) + 1, 2, 21)
            : 0;

    /// <summary>
    /// 生成 Slider/Bar 刻度线 + 数值标签（轨道上方外侧，lv_obj 刻度 + lv_label 数值）。
    /// 与设计器 ShowTicks 渲染语义一致（刻度 y 偏移 -5、数值标签在刻度线上方 -16、居中于刻度）。
    /// </summary>
    private void AppendSliderTicks(StringBuilder sb, string objName, HmiControl control, HmiTheme? theme)
    {
        if (!control.ShowTicks) return;
        var tickCount = SliderTickCount(control);
        if (tickCount < 2) return;

        // 刻度/标签颜色：TextColor → 主题次要文本色 → Slate400（与设计器 ThemeFallback 同源）
        var tickColor = !string.IsNullOrEmpty(control.TextColor)
            ? (theme?.ToLvglColor(control.TextColor) ?? $"lv_color_hex(0x{control.TextColor.TrimStart('#')})")
            : (theme?.ToLvglColor(theme.TextSecondaryColor)
                ?? $"lv_color_hex(0x{PaletteDefaults.Slate400.TrimStart('#')})");

        var w = control.Width;
        sb.AppendLine($"    /* {control.Type} 刻度（轨道上方外侧）: {tickCount} 条 */");
        var tickName = CIdentifier.Prefixed("_tick_", control.Id);
        var tickLblName = CIdentifier.Prefixed("_ticklbl_", control.Id);
        for (var i = 0; i < tickCount; i++)
        {
            var ratio = tickCount > 1 ? (double)i / (tickCount - 1) : 0;
            var x = (int)Math.Round(w * ratio);
            var label = BindingFormat.FormatTick(control.MinValue + i * control.StepValue);
            sb.AppendLine($"    lv_obj_t* {tickName}_{i} = lv_obj_create({objName});");
            sb.AppendLine($"    lv_obj_remove_style_all({tickName}_{i});");
            sb.AppendLine($"    lv_obj_set_size({tickName}_{i}, 1, 5);");
            sb.AppendLine($"    lv_obj_align({tickName}_{i}, LV_ALIGN_TOP_LEFT, {x}, -5);");
            sb.AppendLine($"    lv_obj_set_style_bg_color({tickName}_{i}, {tickColor}, 0);");
            sb.AppendLine($"    lv_obj_set_style_bg_opa({tickName}_{i}, LV_OPA_COVER, 0);");
            sb.AppendLine($"    lv_obj_t* {tickLblName}_{i} = lv_label_create({objName});");
            sb.AppendLine($"    lv_label_set_text({tickLblName}_{i}, \"{EscapeCString(label)}\");");
            sb.AppendLine($"    lv_obj_set_style_text_color({tickLblName}_{i}, {tickColor}, 0);");
            // 刻度标签：使用嵌入 CJK 子集而非 montserrat_12（无中文字形 → 标题含中文时 tofu）
            var tickFont = _soMode ? "&lv_font_source_han_sans_sc_14_cjk" : "&lv_font_simsun_14_cjk";
            sb.AppendLine($"    lv_obj_set_style_text_font({tickLblName}_{i}, {tickFont}, 0);");
            sb.AppendLine($"    lv_obj_align({tickLblName}_{i}, LV_ALIGN_TOP_MID, {x - (int)(w / 2)}, -16);");
        }
    }

    /// <summary>
    /// 生成 Slider knob 顶部高对比小三角指针（border 三角技巧：0x0 对象 + LV_BORDER_SIDE_TOP，
    /// 尖端朝上指向轨道上方刻度，与设计器 DrawPointerTriangle 一致）。knob 位置随值更新见 sim_refresh_bindings。
    /// </summary>
    private static void AppendKnobPointerTriangle(StringBuilder sb, string objName, HmiControl control,
        HmiTheme? theme, string knobColorHex)
    {
        var knobR = Math.Min(control.Width, control.Height) / 2.0;
        var progress = control.MaxValue > control.MinValue
            ? Math.Clamp((control.Value - control.MinValue) / (control.MaxValue - control.MinValue), 0, 1)
            : 0;
        var knobX = (int)Math.Round(progress * control.Width) - 10;   // 左边缘=knob中心-三角半宽(7)；knob 圆心实机= w*ratio-3（LVGL indic_area 减1+position 截断+绘制近似，2026-08-14 实测校准）
        var triY = (int)Math.Round(-knobR * 0.88 + 4);   // 尖端在圆内上部（对应设计器 tipY = cy - r*0.88；canvas 尖端在顶部偏移 +4）
        AppendTriangle(sb, objName, CIdentifier.Prefixed("_knobtri_", control.Id), knobX, triY, knobColorHex);
    }

    /// <summary>生成 Bar 末端游标上的高对比小三角指针（尖端朝上指向刻度）。位置随值更新见 sim_refresh_bindings。</summary>
    private static void AppendCursorPointerTriangle(StringBuilder sb, string objName, HmiControl control,
        string cursorColorHex, int cursorD)
    {
        var r = cursorD / 2.0;
        var progress = control.MaxValue > control.MinValue
            ? Math.Clamp((control.Value - control.MinValue) / (control.MaxValue - control.MinValue), 0, 1)
            : 0;
        var curX = (int)Math.Round(progress * control.Width) - 1 - 7;   // 三角左边缘=indicator末端(w*ratio-1)-三角半宽(7)
        var triY = (int)Math.Round(-r * 0.88 + 4);
        AppendTriangle(sb, objName, CIdentifier.Prefixed("_curtri_", control.Id), curX, triY, cursorColorHex);
    }

    /// <summary>
    /// 文件级声明指针/游标对象（Slider knob 三角 _knobtri_、Bar 游标 _cur_ + 三角 _curtri_），
    /// create_screen_* 赋值、sim_refresh_bindings 更新位置（跨函数访问需 static 全局）。
    /// </summary>
    private static string GeneratePointerDeclarations(HmiProject project)
    {
        var sb = new StringBuilder();
        // 必须遍历 Faceplate **展开后**的控件清单 —— 与 create_screen_* 用的是同一份。
        // 用原始 project.Screens[].Controls 时，模板里的 Slider/Bar 克隆体会发射
        // `_cur_<cloneId> = lv_obj_create(o);` 却没有同名的文件级 static 声明 → C 编译错误。
        foreach (var c in EnumerateRenderableControls(project))
        {
            var cid = CIdentifier.ForControlSymbol(c.Id);
            if (c.Type == HmiControlType.Slider && c.ShowPointer != false)
                sb.AppendLine($"static lv_obj_t* _knobtri_{cid} = NULL;");
            else if (c.Type == HmiControlType.Bar && c.ShowPointer == true)
            {
                sb.AppendLine($"static lv_obj_t* _cur_{cid} = NULL;");
                sb.AppendLine($"static lv_obj_t* _curtri_{cid} = NULL;");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 文件级生成 Slider/Bar 值变化回调：触摸拖动或代码设值时，按当前值更新
    /// knob 顶部小三角（_knobtri_）/ Bar 游标（_cur_ + _curtri_）水平位置，
    /// 使指针跟随 knob/指示器末端移动（设备 SharedLibrary 与模拟器共用；
    /// 模拟器另有 sim_refresh_bindings 正弦波轮询，二者不冲突）。
    /// 位置公式与创建时初始 align 及 sim_refresh_bindings 完全一致。
    /// </summary>
    private static string AppendPointerValueCallbacks(HmiProject project)
    {
        var sb = new StringBuilder();
        // 同 GeneratePointerDeclarations：必须走展开后的清单，否则克隆控件的
        // `_bar_<cloneId>_val_cb` / `_slider_<cloneId>_val_cb` 被 add_event_cb 引用却从未定义
        // → C 编译错误（隐式函数声明）。
        foreach (var c in EnumerateRenderableControls(project))
        {
            var cid = CIdentifier.ForControlSymbol(c.Id);
            var min = (int)c.MinValue;
            var max = (int)c.MaxValue;
            var w = (int)c.Width;
            if (c.Type == HmiControlType.Slider && c.ShowPointer != false)
            {
                var knobR = Math.Min(c.Width, c.Height) / 2.0;
                var triY = (int)Math.Round(-knobR * 0.88 + 4);
                sb.AppendLine("/* Slider knob 顶部小三角指针跟随（值变化时更新位置） */");
                sb.AppendLine($"static void _slider_{cid}_val_cb(lv_event_t* e) {{");
                sb.AppendLine("    lv_obj_t* obj = lv_event_get_target(e);");
                sb.AppendLine("    int v = lv_slider_get_value(obj);");
                sb.AppendLine($"    int x = (int)((v - {min}) / {CFloat(max - min)} * {w} + 0.5f) - 10;   // LVGL knob 圆心=w*ratio-3（实机校准），三角左边缘=圆心-半宽(7)");
                sb.AppendLine($"    if (_knobtri_{cid}) lv_obj_align(_knobtri_{cid}, LV_ALIGN_LEFT_MID, x, {triY});");
                sb.AppendLine("}");
            }
            else if (c.Type == HmiControlType.Bar && c.ShowPointer == true)
            {
                var curD = (int)Math.Round(Math.Min(c.Width, c.Height) * 0.7);
                var r = curD / 2.0;
                var triY = (int)Math.Round(-r * 0.88 + 4);
                sb.AppendLine("/* Bar 末端游标+小三角指针跟随（值变化时更新位置） */");
                sb.AppendLine($"static void _bar_{cid}_val_cb(lv_event_t* e) {{");
                sb.AppendLine("    lv_obj_t* obj = lv_event_get_target(e);");
                sb.AppendLine("    int v = lv_bar_get_value(obj);");
                sb.AppendLine($"    int x = (int)((v - {min}) / {CFloat(max - min)} * {w} + 0.5f) - 1;   // indicator 末端 = w*ratio-1（lv_bar.c anim_cur_value_x-=1）");
                sb.AppendLine($"    if (_cur_{cid}) lv_obj_align(_cur_{cid}, LV_ALIGN_LEFT_MID, x - {curD / 2}, 0);");
                sb.AppendLine($"    if (_curtri_{cid}) lv_obj_align(_curtri_{cid}, LV_ALIGN_LEFT_MID, x - 7, {triY});");
                sb.AppendLine("}");
            }
        }
        return sb.ToString();
    }
    /// 说明：LVGL 9 中 0x0 对象的 border 三角技巧不渲染（空 area 被跳过），改用 canvas；
    /// buffer 必须用 lv_draw_buf_create（自动对齐，静态数组手动对齐不足会越界卡死）。
    /// </summary>
    private static void AppendTriangle(StringBuilder sb, string objName, string varName,
        int x, int y, string baseColorHex)
    {
        var triColorHex = IsHexLight(baseColorHex) ? PaletteDefaults.Slate900.TrimStart('#')
            : PaletteDefaults.Text.TrimStart('#');
        sb.AppendLine($"    /* 小三角指针（尖端朝上，lv_canvas 绘制，与设计器 DrawPointerTriangle 一致） */");
        sb.AppendLine($"    lv_draw_buf_t* _tribuf_{varName} = lv_draw_buf_create(14, 8, LV_COLOR_FORMAT_ARGB8888, 0);");
        sb.AppendLine($"    {varName} = lv_canvas_create({objName});");
        sb.AppendLine($"    lv_canvas_set_draw_buf({varName}, _tribuf_{varName});");
        sb.AppendLine($"    lv_canvas_fill_bg({varName}, lv_color_hex(0x000000), LV_OPA_TRANSP);");
        sb.AppendLine($"    for (int _y = 0; _y < 7; _y++) {{");
        sb.AppendLine($"        int _hw = _y;");
        sb.AppendLine($"        for (int _x = 7 - _hw; _x <= 7 + _hw; _x++) {{");
        sb.AppendLine($"            lv_canvas_set_px({varName}, _x, _y, lv_color_hex(0x{triColorHex}), LV_OPA_COVER);");
        sb.AppendLine($"        }}");
        sb.AppendLine($"    }}");
        sb.AppendLine($"    lv_obj_align({varName}, LV_ALIGN_LEFT_MID, {x}, {y});");
    }
}
