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
/// CodeGenerator 分部 —— 控件专属参数发射 —— GenerateControlParameters 分发 + 各控件族 Emit*Parameters。
/// </summary>
public partial class CodeGenerator
{
    private void GenerateControlParameters(StringBuilder sb, HmiControl control, HmiTheme? theme)
    {
        var o = CIdentifier.ControlObject(control.Id);
        switch (control.Type)
        {
            case HmiControlType.Slider:
                EmitSliderParameters(sb, control, theme, o);
                break;
            case HmiControlType.Bar:
                EmitBarParameters(sb, control, theme, o);
                break;
            case HmiControlType.Dropdown:
                EmitDropdownParameters(sb, control, theme, o);
                break;
            case HmiControlType.TextArea:
                EmitTextAreaParameters(sb, control, theme, o);
                break;
            case HmiControlType.Gauge:
            case HmiControlType.Knob:
                EmitGaugeParameters(sb, control, theme, o);
                break;
            case HmiControlType.Led:
                EmitLedParameters(sb, control, theme, o);
                break;
            case HmiControlType.Spinner:
                EmitSpinnerParameters(sb, control, theme, o);
                break;
            case HmiControlType.Table:
                EmitTableParameters(sb, control, theme, o);
                break;
            case HmiControlType.TabView:
                EmitTabViewParameters(sb, control, theme, o);
                break;
            case HmiControlType.Label:
                EmitLabelParameters(sb, control, theme, o);
                break;
            case HmiControlType.Keyboard:
                EmitKeyboardParameters(sb, control, theme, o);
                break;
            case HmiControlType.Image:
                EmitImageParameters(sb, control, theme, o);
                break;
            case HmiControlType.Line:
                EmitLineParameters(sb, control, theme, o);
                break;
            case HmiControlType.Polyline:
                EmitPolylineParameters(sb, control, theme, o);
                break;
            case HmiControlType.Arc:
                EmitArcParameters(sb, control, theme, o);
                break;
        }

        // 勾选状态（Switch/CheckBox 由 LV_STATE_CHECKED 状态位表达）
        if (control.IsChecked && control.Type is HmiControlType.Switch or HmiControlType.CheckBox)
            sb.AppendLine($"    lv_obj_add_state({o}, LV_STATE_CHECKED);");

        // 指示器部件颜色（Slider/Bar/Switch/CheckBox/Gauge/Knob 的 LV_PART_INDICATOR；
        // CheckBox 的勾选框即 LV_PART_INDICATOR）
        // 与设计器语义一致：IndicatorColor 为空时回退到 TextColor（再回退 LVGL 默认主题色）
        var indicatorColor = !string.IsNullOrEmpty(control.IndicatorColor)
            ? control.IndicatorColor
            : control.TextColor;
        if (!string.IsNullOrEmpty(indicatorColor) &&
            control.Type is HmiControlType.Slider or HmiControlType.Bar or HmiControlType.Switch
                or HmiControlType.CheckBox or HmiControlType.Gauge or HmiControlType.Knob)
        {
            var colorExpr = $"lv_color_hex(0x{indicatorColor.TrimStart('#')})";
            sb.AppendLine($"    lv_obj_set_style_bg_color({o}, {colorExpr}, LV_PART_INDICATOR);");
            // CheckBox：主题默认在 LV_PART_INDICATOR|LV_STATE_CHECKED 上应用主色（优先级更高），
            // 必须同时设置该选择器，否则勾选色被主题蓝覆盖（与设计器勾选色不一致）。
            if (control.Type == HmiControlType.CheckBox)
            {
                sb.AppendLine($"    lv_obj_set_style_bg_color({o}, {colorExpr}, LV_PART_INDICATOR | LV_STATE_CHECKED);");
            }
            // Slider 滑块（knob）颜色与指示器一致（LVGL 默认主题 knob 为主题蓝，
            // 需显式设置才能与设计器 knob 颜色对齐）
            if (control.Type == HmiControlType.Slider)
            {
                sb.AppendLine($"    lv_obj_set_style_bg_color({o}, {colorExpr}, LV_PART_KNOB);");
            }
        }

        // 通用对象属性：不透明度（0-100 → lv_opa 0-255）
        if (control.Opacity < 99.5)
        {
            var opa = (int)Math.Round(Math.Clamp(control.Opacity, 0, 100) * 2.55);
            sb.AppendLine($"    lv_obj_set_style_opa({o}, {opa}, 0);");
        }

        // 通用对象属性：隐藏（IsVisible=false → LV_OBJ_FLAG_HIDDEN）
        if (!control.IsVisible)
            sb.AppendLine($"    lv_obj_add_flag({o}, LV_OBJ_FLAG_HIDDEN);");
    }

    /// <summary>Slider 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitSliderParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        sb.AppendLine($"    lv_slider_set_range({o}, {(int)control.MinValue}, {(int)control.MaxValue});");
        sb.AppendLine($"    lv_slider_set_value({o}, {control.Value}, LV_ANIM_OFF);");
        // LVGL 9 子对象默认裁剪到父边界：刻度/指针三角在轨道上方（负偏移），需允许溢出显示
        sb.AppendLine($"    lv_obj_add_flag({o}, LV_OBJ_FLAG_OVERFLOW_VISIBLE);");
        if (!string.IsNullOrEmpty(control.ValueMode))
            sb.AppendLine($"    lv_slider_set_mode({o}, LV_SLIDER_MODE_{control.ValueMode.ToUpperInvariant()});");
        // 消除 LV_PART_MAIN 默认 padding，确保指示器（进度填充）充满控件整个宽高，
        // 与设计器从 rect.X 开始按 rect.Width * progress 填充的行为一致。
        sb.AppendLine($"    lv_obj_set_style_pad_all({o}, 0, LV_PART_MAIN);");
        // 消除 LV_PART_KNOB 默认 padding（主题 pad_all=6dpx 会使 knob 大于控件高度），
        // 使 knob 直径 = 控件高度，与设计器 knob 渲染一致。
        sb.AppendLine($"    lv_obj_set_style_pad_all({o}, 0, LV_PART_KNOB);");
        // 指示器圆角：LVGL 默认主题给 LV_PART_INDICATOR 加 LV_RADIUS_CIRCLE（胶囊），
        // 与设计器 Radius<=0 时用 Height/2 一致；但用户自定义 Radius>0 时需显式覆盖。
        {
            var indicRadius = control.Radius > 0 ? control.Radius : (int)(control.Height / 2);
            sb.AppendLine($"    lv_obj_set_style_radius({o}, {indicRadius}, LV_PART_INDICATOR);");
        }
        // 指示器颜色：与设计器一致（IndicatorColor ?? TextColor 兜底主题主色），
        // 覆盖 LVGL 默认主题的指示器默认色（material blue），避免模拟器与设计器颜色不同。
        {
            var indColorHex = !string.IsNullOrEmpty(control.IndicatorColor)
                ? control.IndicatorColor.TrimStart('#')
                : (!string.IsNullOrEmpty(control.TextColor) ? control.TextColor.TrimStart('#')
                    : (theme?.PrimaryColor ?? PaletteDefaults.Primary).TrimStart('#'));
            sb.AppendLine($"    lv_obj_set_style_bg_color({o}, lv_color_hex(0x{indColorHex}), LV_PART_INDICATOR);");
            sb.AppendLine($"    lv_obj_set_style_bg_opa({o}, LV_OPA_COVER, LV_PART_INDICATOR);");
        }
        // 轨道背景：未配置 BgColor 时保持透明（与设计器一致，仅显示进度条）；
        // LVGL 默认主题会给 slider 轨道填背景色，需显式置 0。
        // 配置了 BgColor 时由通用样式块（selector 0）设置用户颜色，这里不覆盖；
        // 仅 Radius=0 时补默认胶囊圆角（与设计器语义一致）。
        if (string.IsNullOrEmpty(control.BgColor))
        {
            sb.AppendLine($"    lv_obj_set_style_bg_opa({o}, LV_OPA_TRANSP, LV_PART_MAIN);");
        }
        else if (control.Radius <= 0)
        {
            sb.AppendLine($"    lv_obj_set_style_radius({o}, {(int)(control.Height / 2)}, LV_PART_MAIN);");
        }
        // 指针（knob）显隐：ShowPointer 显式 false 时隐藏滑块（LV_PART_KNOB 透明）；
        // 显示时设置与设计器一致的 knob 样式（IndicatorColor??TextColor 兜底主题主色 + 深描边 + 圆形）
        // 并真实生成顶部小三角指针（border 三角技巧，尖端朝上指向刻度，与设计器 DrawPointerTriangle 一致）。
        if (control.ShowPointer == false)
        {
            sb.AppendLine($"    lv_obj_set_style_opa({o}, LV_OPA_TRANSP, LV_PART_KNOB);");
        }
        else
        {
            var knobColorHex = !string.IsNullOrEmpty(control.IndicatorColor)
                ? control.IndicatorColor.TrimStart('#')
                : (!string.IsNullOrEmpty(control.TextColor) ? control.TextColor.TrimStart('#')
                    : (theme?.PrimaryColor ?? PaletteDefaults.Primary).TrimStart('#'));
            sb.AppendLine($"    lv_obj_set_style_bg_color({o}, lv_color_hex(0x{knobColorHex}), LV_PART_KNOB);");
            sb.AppendLine($"    lv_obj_set_style_bg_opa({o}, LV_OPA_COVER, LV_PART_KNOB);");
            sb.AppendLine($"    lv_obj_set_style_radius({o}, LV_RADIUS_CIRCLE, LV_PART_KNOB);");
            sb.AppendLine($"    lv_obj_set_style_border_width({o}, 1, LV_PART_KNOB);");
            sb.AppendLine($"    lv_obj_set_style_border_color({o}, lv_color_hex(0x{PaletteDefaults.Slate900.TrimStart('#')}), LV_PART_KNOB);");
            AppendKnobPointerTriangle(sb, o, control, theme, knobColorHex);
        }
        // 刻度线 + 数值标签：真实生成 lv_obj 刻度 + lv_label 数值（与设计器 ShowTicks 一致）
        AppendSliderTicks(sb, o, control, theme);
        // 值变化（触摸拖动/代码设值）时 knob 顶部小三角跟随移动（设备/模拟器共用）
        if (control.ShowPointer != false)
            sb.AppendLine($"    lv_obj_add_event_cb({o}, _slider_{CIdentifier.ForControlSymbol(control.Id)}_val_cb, LV_EVENT_VALUE_CHANGED, NULL);");
    }

    /// <summary>Bar 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitBarParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        sb.AppendLine($"    lv_bar_set_range({o}, {(int)control.MinValue}, {(int)control.MaxValue});");
        sb.AppendLine($"    lv_bar_set_value({o}, {control.Value}, LV_ANIM_OFF);");
        // LVGL 9 子对象默认裁剪到父边界：刻度/游标三角在轨道上方（负偏移），需允许溢出显示
        sb.AppendLine($"    lv_obj_add_flag({o}, LV_OBJ_FLAG_OVERFLOW_VISIBLE);");
        if (!string.IsNullOrEmpty(control.ValueMode))
            sb.AppendLine($"    lv_bar_set_mode({o}, LV_BAR_MODE_{control.ValueMode.ToUpperInvariant()});");
        // 消除 LV_PART_MAIN 默认 padding，确保指示器（进度填充）充满控件整个宽高，
        // 与设计器从 rect.X 开始按 rect.Width * progress 填充的行为一致。
        sb.AppendLine($"    lv_obj_set_style_pad_all({o}, 0, LV_PART_MAIN);");
        // 指示器圆角：LVGL 默认主题给 LV_PART_INDICATOR 加 LV_RADIUS_CIRCLE（胶囊），
        // 与设计器 Radius<=0 时用 Height/2 一致；但用户自定义 Radius>0 时需显式覆盖。
        {
            var indicRadius = control.Radius > 0 ? control.Radius : (int)(control.Height / 2);
            sb.AppendLine($"    lv_obj_set_style_radius({o}, {indicRadius}, LV_PART_INDICATOR);");
        }
        // 指示器颜色：与设计器一致（IndicatorColor ?? TextColor 兜底主题主色），
        // 覆盖 LVGL 默认主题的指示器默认色（material blue），避免模拟器与设计器颜色不同。
        {
            var indColorHex = !string.IsNullOrEmpty(control.IndicatorColor)
                ? control.IndicatorColor.TrimStart('#')
                : (!string.IsNullOrEmpty(control.TextColor) ? control.TextColor.TrimStart('#')
                    : (theme?.PrimaryColor ?? PaletteDefaults.Primary).TrimStart('#'));
            sb.AppendLine($"    lv_obj_set_style_bg_color({o}, lv_color_hex(0x{indColorHex}), LV_PART_INDICATOR);");
            sb.AppendLine($"    lv_obj_set_style_bg_opa({o}, LV_OPA_COVER, LV_PART_INDICATOR);");
        }
        // 轨道背景：未配置 BgColor 时保持透明（与设计器一致，仅显示进度条）；
        // 配置了 BgColor 时由通用样式块设置用户颜色，这里不覆盖；Radius=0 时补默认胶囊圆角。
        if (string.IsNullOrEmpty(control.BgColor))
        {
            sb.AppendLine($"    lv_obj_set_style_bg_opa({o}, LV_OPA_TRANSP, LV_PART_MAIN);");
        }
        else if (control.Radius <= 0)
        {
            sb.AppendLine($"    lv_obj_set_style_radius({o}, {(int)(control.Height / 2)}, LV_PART_MAIN);");
        }
        // 末端游标：ShowPointer 显式 true 时真实生成游标（圆形子对象 + 高对比小三角），
        // 位置随值更新见 sim_refresh_bindings（LVGL lv_bar 无原生 knob）。
        if (control.ShowPointer == true)
        {
            var curColorHex = !string.IsNullOrEmpty(control.IndicatorColor)
                ? control.IndicatorColor.TrimStart('#')
                : (!string.IsNullOrEmpty(control.TextColor) ? control.TextColor.TrimStart('#')
                    : (theme?.PrimaryColor ?? PaletteDefaults.Primary).TrimStart('#'));
            var curD = (int)Math.Round(Math.Min(control.Width, control.Height) * 0.7);
            var progress = control.MaxValue > control.MinValue
                ? Math.Clamp((control.Value - control.MinValue) / (control.MaxValue - control.MinValue), 0, 1)
                : 0;
            var curX = (int)Math.Round(progress * control.Width) - 1 - curD / 2;   // 游标左边缘=indicator末端(w*ratio-1)-游标半径
            // ⚠️ 这里**不能**写 `lv_obj_t* _cur_x = ...`：同名文件级 static 由
            // GeneratePointerDeclarations 声明（条件与本分支完全相同：Bar && ShowPointer），
            // 再加类型前缀会**遮蔽**它，于是 static 永远是 NULL，而 sim_refresh_bindings
            // 里 `lv_obj_align(_cur_x, ...)` 没有空指针保护 → 空指针解引用。
            var cur = CIdentifier.Prefixed("_cur_", control.Id);
            sb.AppendLine($"    {cur} = lv_obj_create({o});");
            sb.AppendLine($"    lv_obj_remove_style_all({cur});");
            sb.AppendLine($"    lv_obj_set_size({cur}, {curD}, {curD});");
            sb.AppendLine($"    lv_obj_set_style_radius({cur}, LV_RADIUS_CIRCLE, 0);");
            sb.AppendLine($"    lv_obj_set_style_bg_color({cur}, lv_color_hex(0x{curColorHex}), 0);");
            sb.AppendLine($"    lv_obj_set_style_bg_opa({cur}, LV_OPA_COVER, 0);");
            sb.AppendLine($"    lv_obj_set_style_border_width({cur}, 1, 0);");
            sb.AppendLine($"    lv_obj_set_style_border_color({cur}, lv_color_hex(0x{PaletteDefaults.Slate900.TrimStart('#')}), 0);");
            sb.AppendLine($"    lv_obj_align({cur}, LV_ALIGN_LEFT_MID, {curX}, 0);");
            AppendCursorPointerTriangle(sb, o, control, curColorHex, curD);
        }
        // 刻度线 + 数值标签：真实生成 lv_obj 刻度 + lv_label 数值（与设计器 ShowTicks 一致）
        AppendSliderTicks(sb, o, control, theme);
        // 值变化（代码设值/绑定刷新）时游标+小三角跟随（设备/模拟器共用）
        if (control.ShowPointer == true)
            sb.AppendLine($"    lv_obj_add_event_cb({o}, _bar_{CIdentifier.ForControlSymbol(control.Id)}_val_cb, LV_EVENT_VALUE_CHANGED, NULL);");
    }

    /// <summary>Dropdown 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitDropdownParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        if (!string.IsNullOrEmpty(control.Options))
            sb.AppendLine($"    lv_dropdown_set_options({o}, \"{EscapeCString(control.Options)}\");");
        sb.AppendLine($"    lv_dropdown_set_selected({o}, {control.SelectedIndex});");
        sb.AppendLine($"    lv_obj_set_style_text_align({o}, {LvglTextAlign(control.TextAlign)}, 0);");
        // 下拉列表弹窗：list 是独立对象（lv_dropdownlist_constructor 未设置字体，
        // 继承 screen 默认 montserrat 无中文字形 → 选项方框），运行时继承主字体。
        sb.AppendLine($"    lv_obj_set_style_text_font(lv_dropdown_get_list({o}), lv_obj_get_style_text_font({o}, 0), 0);");
        // 右侧箭头：LV_SYMBOL_DOWN 是 PUA 符号（U+F040），FreeType 中文字体（Noto CJK）
        // 无该字形 → 箭头方框；LV_PART_INDICATOR 改用内置符号字体（montserrat 含符号区）。
        sb.AppendLine($"    lv_obj_set_style_text_font({o}, LV_FONT_DEFAULT, LV_PART_INDICATOR);");
        if (string.IsNullOrEmpty(control.BgColor) && theme != null)
        {
            sb.AppendLine($"    lv_obj_set_style_bg_color({o}, {theme.ToLvglColor(theme.SurfaceColor)}, 0);");
            sb.AppendLine($"    lv_obj_set_style_bg_opa({o}, 255, 0);");
            sb.AppendLine($"    lv_obj_set_style_radius({o}, {theme.CornerRadius}, 0);");
            sb.AppendLine($"    lv_obj_set_style_border_width({o}, {theme.BorderWidth}, 0);");
            sb.AppendLine($"    lv_obj_set_style_border_color({o}, {theme.ToLvglColor(theme.BorderColor)}, 0);");
        }
    }

    /// <summary>TextArea 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitTextAreaParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        if (!string.IsNullOrEmpty(control.PlaceholderText))
            sb.AppendLine($"    lv_textarea_set_placeholder_text({o}, \"{EscapeCString(control.PlaceholderText)}\");");
        if (control.MaxLength > 0)
            sb.AppendLine($"    lv_textarea_set_max_length({o}, {control.MaxLength});");
        if (control.PasswordMode)
            sb.AppendLine($"    lv_textarea_set_password_mode({o}, true);");
        // 软键盘（ABI V3）：点击聚焦弹出宿主软键盘；
        // KeyboardMode=Number → 数字键盘（宿主 hmi_set_ta_keyboard_mode）。
        // 模拟器模式无宿主符号：生成 CLICK_FOCUSABLE（模拟器 main.c 软键盘服务接管），
        // Number 输入框用 user_data 标记，模拟器按标记弹数字键盘。
        sb.AppendLine($"    lv_obj_add_flag({o}, LV_OBJ_FLAG_CLICK_FOCUSABLE);");
        if (_soMode && !string.IsNullOrEmpty(control.KeyboardMode))
        {
            var kbMode = control.KeyboardMode.Equals("Number", StringComparison.OrdinalIgnoreCase)
                ? "num" : "text";
            sb.AppendLine($"    hmi_set_ta_keyboard_mode({o}, \"{kbMode}\");   /* Keyboard mode: {control.KeyboardMode} */");
        }
        else if (!_soMode && control.KeyboardMode?.Equals("Number", StringComparison.OrdinalIgnoreCase) == true)
        {
            sb.AppendLine($"    lv_obj_set_user_data({o}, (void*)1);   /* Keyboard mode: Number（模拟器数字键盘） */");
        }
        // 输入框背景：未配置 BgColor 时使用主题面板色（与 Dropdown 一致），
        // 避免 LVGL 默认浅色主题的白色卡片背景与设计器深色不一致。
        if (string.IsNullOrEmpty(control.BgColor) && theme != null)
        {
            sb.AppendLine($"    lv_obj_set_style_bg_color({o}, {theme.ToLvglColor(theme.SurfaceColor)}, 0);");
            sb.AppendLine($"    lv_obj_set_style_bg_opa({o}, 255, 0);");
            sb.AppendLine($"    lv_obj_set_style_radius({o}, {theme.CornerRadius}, 0);");
            sb.AppendLine($"    lv_obj_set_style_border_width({o}, {theme.BorderWidth}, 0);");
            sb.AppendLine($"    lv_obj_set_style_border_color({o}, {theme.ToLvglColor(theme.BorderColor)}, 0);");
        }
    }

    /// <summary>Gauge/Knob 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitGaugeParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        // 形态还原设计器：270° 弧（135° 起始）+ 背景弧 + 指示弧 + 中心数值文本。
        // lv_arc 默认 0° 在 3 点钟方向，rotation=135 使弧线起始于左下 135°，
        // bg_angles(0,270) 定义 270° 背景弧，指示弧由 set_value 按 range 映射。
        // 角度常量与设计器 DesignCanvas 共用 ArcLayout，防止两处漂移。
        sb.AppendLine($"    lv_arc_set_rotation({o}, {ArcLayout.StartAngle:0});");
        sb.AppendLine($"    lv_arc_set_bg_angles({o}, 0, {ArcLayout.SweepAngle:0});");
        sb.AppendLine($"    lv_arc_set_range({o}, {(int)control.MinValue}, {(int)control.MaxValue});");
        sb.AppendLine($"    lv_arc_set_value({o}, {control.Value});");
        // 弧线宽度（轨道厚度）：默认 12px（与设计器 12*z 一致），用户可经 BorderWidth 调
        var arcW = control.BorderWidth > 0 ? control.BorderWidth : ArcLayout.ArcWidthDefault;
        sb.AppendLine($"    lv_obj_set_style_arc_width({o}, {arcW}, LV_PART_MAIN);");
        sb.AppendLine($"    lv_obj_set_style_arc_width({o}, {arcW}, LV_PART_INDICATOR);");
        // 背景弧颜色：轨道底色（BG 弧），取 BgColor 兜底主题 Surface 色，与设计器 bgArc 一致
        var bgArcColor = !string.IsNullOrEmpty(control.BgColor)
            ? control.BgColor.TrimStart('#') : PaletteDefaults.Surface.TrimStart('#');
        sb.AppendLine($"    lv_obj_set_style_arc_color({o}, lv_color_hex(0x{bgArcColor}), LV_PART_MAIN);");
        sb.AppendLine($"    lv_obj_set_style_arc_opa({o}, LV_OPA_COVER, LV_PART_MAIN);");
        // 指示弧颜色：IndicatorColor ?? TextColor（与通用指示器一致），兜底主题蓝
        var gaugeInd = !string.IsNullOrEmpty(control.IndicatorColor)
            ? control.IndicatorColor.TrimStart('#')
            : (!string.IsNullOrEmpty(control.TextColor) ? control.TextColor.TrimStart('#') : PaletteDefaults.Primary.TrimStart('#'));
        sb.AppendLine($"    lv_obj_set_style_arc_color({o}, lv_color_hex(0x{gaugeInd}), LV_PART_INDICATOR);");
        sb.AppendLine($"    lv_obj_set_style_arc_opa({o}, LV_OPA_COVER, LV_PART_INDICATOR);");

        if (control.Type == HmiControlType.Gauge)
        {
            // Gauge：隐藏 knob（设计器无滑块），中心显示数值文本（子 Label）
            sb.AppendLine($"    lv_obj_set_style_opa({o}, LV_OPA_TRANSP, LV_PART_KNOB);");
            var gaugeLbl = CIdentifier.Prefixed("_gauge_lbl_", control.Id);
            sb.AppendLine($"    lv_obj_t* {gaugeLbl} = lv_label_create({o});");
            var gaugeText = EscapeCString(control.Text ?? control.Name ?? "0");
            sb.AppendLine($"    lv_label_set_text({gaugeLbl}, \"{gaugeText}\");");
            sb.AppendLine($"    lv_obj_center({gaugeLbl});");
            sb.AppendLine($"    lv_obj_set_style_text_color({gaugeLbl}, {theme?.ToLvglColor(control.TextColor) ?? $"lv_color_hex(0x{PaletteDefaults.Text.TrimStart('#')})"}, 0);");
            // 中心数值文本：使用嵌入 CJK 子集而非 LV_FONT_DEFAULT（默认 Montserrat 无中文字形 → 中文显示 tofu）
            var gaugeFont = _soMode ? "&lv_font_source_han_sans_sc_14_cjk" : "&lv_font_simsun_14_cjk";
            sb.AppendLine($"    lv_obj_set_style_text_font({gaugeLbl}, {gaugeFont}, 0);");
            // 刻度线（MajorTickCount 主刻度）——LVGL 无弧刻度 API，以注释声明运行时绘制
            sb.AppendLine($"    // Gauge 主刻度: {Math.Max(2, control.MajorTickCount)} 段（运行时在 arc 上叠加）");
        }
        else
        {
            // Knob：显示旋钮（指针形 knob），与设计器指针语义一致
            sb.AppendLine($"    lv_obj_set_style_opa({o}, LV_OPA_COVER, LV_PART_KNOB);");
            var knobColor = !string.IsNullOrEmpty(control.IndicatorColor)
                ? control.IndicatorColor.TrimStart('#') : PaletteDefaults.Primary.TrimStart('#');
            sb.AppendLine($"    lv_obj_set_style_bg_color({o}, lv_color_hex(0x{knobColor}), LV_PART_KNOB);");
        }
    }

    /// <summary>Led 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitLedParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        if (!string.IsNullOrEmpty(control.BgColor))
            sb.AppendLine($"    lv_led_set_color({o}, lv_color_hex(0x{control.BgColor.TrimStart('#')}));");
        sb.AppendLine($"    lv_led_set_brightness({o}, {control.Brightness});");
    }

    /// <summary>Spinner 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitSpinnerParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        // v9 起 lv_spinner_create 仅一个父参数，动画参数经 set_anim_params 设置
        sb.AppendLine($"    lv_spinner_set_anim_params({o}, {Math.Max(100, control.SpinSpeed)}, 60);");
    }

    /// <summary>Table 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitTableParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        sb.AppendLine($"    lv_table_set_row_count({o}, {Math.Max(1, control.RowCount)});");
        sb.AppendLine($"    lv_table_set_column_count({o}, {Math.Max(1, control.ColumnCount)});");
        // 列宽：均分填满（默认）/ 指定 px / auto 自适应（LV_COORD_MAX）
        {
            var colWidths = TableColumnWidthExpr(control);
            for (var i = 0; i < colWidths.Count; i++)
                sb.AppendLine($"    lv_table_set_column_width({o}, {i}, {colWidths[i]});");
        }
        // 行高：固定行高（LV_PART_ITEMS min=max，作用于每一行）
        if (control.RowHeight > 0)
        {
            var rh = (int)control.RowHeight;
            sb.AppendLine($"    lv_obj_set_style_min_height({o}, {rh}, LV_PART_ITEMS);");
            sb.AppendLine($"    lv_obj_set_style_max_height({o}, {rh}, LV_PART_ITEMS);");
        }
        // 单元格网格线（LV_PART_ITEMS 边框）—— 与设计器网格一致；
        // LVGL 无主题时表格默认无单元格边框，需显式设置。
        {
            var gridBorder = string.IsNullOrEmpty(control.BorderColor) ? PaletteDefaults.Border : control.BorderColor;
            var gridExpr = theme?.ToLvglColor(gridBorder) ?? $"lv_color_hex(0x{gridBorder.TrimStart('#')})";
            sb.AppendLine($"    lv_obj_set_style_border_width({o}, 1, LV_PART_ITEMS);");
            sb.AppendLine($"    lv_obj_set_style_border_color({o}, {gridExpr}, LV_PART_ITEMS);");
            sb.AppendLine($"    lv_obj_set_style_border_side({o}, LV_BORDER_SIDE_FULL, LV_PART_ITEMS);");
        }
        // 单元格背景：显式设为表格背景色 —— 覆盖 LVGL 无主题时 LV_PART_ITEMS
        // 默认渲染的白色单元格，保证与设计器背景一致（"背景对不上"修复）。
        // 未设置 BgColor 时用中性深色 #1e293b，与设计器兜底色 (30,41,59) 完全一致，
        // 避免无背景表格在模拟器中显示刺眼白色。
        {
            var cellBgColor = string.IsNullOrEmpty(control.BgColor) ? PaletteDefaults.Slate800 : control.BgColor;
            var cellBg = theme?.ToLvglColor(cellBgColor) ?? $"lv_color_hex(0x{cellBgColor.TrimStart('#')})";
            sb.AppendLine($"    lv_obj_set_style_bg_color({o}, {cellBg}, LV_PART_ITEMS);");
            sb.AppendLine($"    lv_obj_set_style_bg_opa({o}, LV_OPA_COVER, LV_PART_ITEMS);");
        }
        if (!string.IsNullOrEmpty(control.HeaderTexts))
        {
            var headers = control.HeaderTexts.Split(',', StringSplitOptions.TrimEntries);
            for (var i = 0; i < headers.Length && i < Math.Max(1, control.ColumnCount); i++)
                sb.AppendLine($"    lv_table_set_cell_value({o}, 0, {i}, \"{EscapeCString(headers[i])}\");");
        }
        // 单元格内容：静态文本立即填充；绑定单元格由 sim_refresh_bindings 实时刷新
        if (control.Cells != null)
        {
            foreach (var cell in control.Cells)
            {
                if (cell.IsBound || cell.Row < 0 || cell.Col < 0) continue;
                if (string.IsNullOrEmpty(cell.Text)) continue;
                sb.AppendLine($"    lv_table_set_cell_value({o}, {cell.Row}, {cell.Col}, \"{EscapeCString(cell.Text)}\");");
            }
        }
    }

    /// <summary>TabView 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitTabViewParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        if (!string.IsNullOrEmpty(control.TabTitles))
        {
            foreach (var tab in control.TabTitles.Split(',', StringSplitOptions.TrimEntries))
                sb.AppendLine($"    lv_tabview_add_tab({o}, \"{EscapeCString(tab)}\");");
        }
        if (control.SelectedIndex > 0)
            sb.AppendLine($"    lv_tabview_set_active({o}, {control.SelectedIndex}, LV_ANIM_OFF);");
    }

    /// <summary>Label 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitLabelParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        if (!string.IsNullOrEmpty(control.LabelLongMode))
            sb.AppendLine($"    lv_label_set_long_mode({o}, {LvglMacros.LabelLongMode(control.LabelLongMode)});");
    }

    /// <summary>
    /// Keyboard 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。
    ///
    /// <para>🩸 <b>阶段 203 修 `#93`</b>：这里原先**只发一行注释**（<c>// Keyboard mode: X</c>）——
    /// 面板给"键盘模式"设了值（`Default` / `Number` / `Special`），导出的代码里**什么都不设**
    /// （`lv_keyboard` 默认 `TEXT_LOWER`）⇒ **用户改了不生效**。LVGL 有
    /// <c>lv_keyboard_set_mode</c>，且本仓模拟器侧早就在用 `LV_KEYBOARD_MODE_NUMBER` /
    /// `LV_KEYBOARD_MODE_TEXT_LOWER`（`SimulatorProjectExporter.MainC.cs`）⇒ 这一格是**能表达**的，
    /// 补上即可（与 `#92` 那种"结构上表达不了"不同）。</para>
    /// </summary>
    private void EmitKeyboardParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        if (string.IsNullOrEmpty(control.KeyboardMode)) return;

        var mode = control.KeyboardMode.Trim().ToLowerInvariant() switch
        {
            "number" => "LV_KEYBOARD_MODE_NUMBER",
            "special" => "LV_KEYBOARD_MODE_SPECIAL",
            _ => "LV_KEYBOARD_MODE_TEXT_LOWER",   // Default / Text / 未知 ⇒ LVGL 的默认模式
        };
        sb.AppendLine($"    lv_keyboard_set_mode({o}, {mode});   /* Keyboard mode: {control.KeyboardMode} */");
    }

    /// <summary>Image 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitImageParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        // 分支按「已解析的缩放模式语义」判，不比对生成出来的字符串：
        // 后者在映射表变化时静默失效 —— 这里曾用一个字符串相等判断代替枚举判断，
        // 其中「COVER」那半条件**永不可达**（StretchModeMap 没有 Cover 语义，UI 也只暴露 None/Fill/Contain）。
        var kind = StretchModeMap.Parse(control.StretchMode);
        var imgAlign = LvglImageAlign(kind);
        // LV_IMAGE_ALIGN_CONTAIN 是 LVGL v9.3.0 才引入的枚举值（枚举，非宏，不能用 #if defined 判断）。
        // 老版本(9.0~9.2)没有这个值，直接用会编译报错，需按版本宏兜底：< 9.3 回退 STRETCH。
        if (kind == StretchModeKind.Contain)
        {
            sb.AppendLine($"#if LV_VERSION_CHECK(9, 3, 0)");
            sb.AppendLine($"    lv_image_set_inner_align({o}, {imgAlign});");
            sb.AppendLine("#else");
            sb.AppendLine($"    lv_image_set_inner_align({o}, LV_IMAGE_ALIGN_STRETCH); /* fallback: LVGL < 9.3 无 {imgAlign} */");
            sb.AppendLine("#endif");
        }
        else
        {
            sb.AppendLine($"    lv_image_set_inner_align({o}, {imgAlign});");
        }
    }

    /// <summary>Line 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitLineParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        // lv_line 默认 line_width=0 不可见，必须显式设置端点坐标/颜色/宽度，
        // 否则模拟器中 Line 控件完全消失（设计器语义：左上→右下对角线，
        // 颜色取 BgColor 默认白，宽度取 BorderWidth 默认 2）。
        var lineColor = string.IsNullOrEmpty(control.BgColor) ? PaletteDefaults.LineFallback : control.BgColor;
        var lineWidth = control.BorderWidth > 0 ? control.BorderWidth : 2;
        var lcExpr = theme?.ToLvglColor(lineColor)
            ?? $"lv_color_hex(0x{lineColor.TrimStart('#')})";
        var linePts = CIdentifier.Prefixed("_pts_", control.Id);
        sb.AppendLine($"    static lv_point_precise_t {linePts}[] = {{ {{ 0, 0 }}, {{ {control.Width:0.#}, {control.Height:0.#} }} }};");
        sb.AppendLine($"    lv_line_set_points({o}, {linePts}, 2);");
        sb.AppendLine($"    lv_obj_set_style_line_color({o}, {lcExpr}, 0);");
        sb.AppendLine($"    lv_obj_set_style_line_width({o}, {lineWidth}, 0);");
        // LVGL lv_line 仅支持圆头/平头两种端点；Round → line_rounded=true，Square/Butt → 平头（默认）
        if (control.LineCap?.Equals("Round", StringComparison.OrdinalIgnoreCase) == true)
            sb.AppendLine($"    lv_obj_set_style_line_rounded({o}, true, 0);");
        else if (!string.IsNullOrEmpty(control.LineCap))
            Cmt(sb, $"    // Line cap: {control.LineCap} (LVGL 仅支持 Round/平头，Square 以平头表达)");
    }

    /// <summary>Polyline 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitPolylineParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        // I10: 折线 —— 顶点来自 control.Points（控件局部坐标），
        // 未配置时回退为一条对角线（与设计器 ResolvePolylinePoints 语义一致）。
        var lineColor = string.IsNullOrEmpty(control.BgColor) ? PaletteDefaults.LineFallback : control.BgColor;
        var lineWidth = control.BorderWidth > 0 ? control.BorderWidth : 2;
        var lcExpr = theme?.ToLvglColor(lineColor)
            ?? $"lv_color_hex(0x{lineColor.TrimStart('#')})";

        var pts = control.Points ?? new List<PointData>
        {
            new(0, 0),
            new(control.Width, control.Height),
        };
        var sbPts = new StringBuilder();
        foreach (var p in pts)
        {
            if (sbPts.Length > 0) sbPts.Append(", ");
            // 阶段 228（#98 进导出第二半）：折线点也是**几何** ⇒ 乘屏幕 DPI（默认 1.0 ⇒ 不变）
            sbPts.Append($"{{ {(p.X * _dpiScale).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}, {(p.Y * _dpiScale).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} }}");
        }
        var polyPts = CIdentifier.Prefixed("_pts_", control.Id);
        sb.AppendLine($"    static lv_point_precise_t {polyPts}[] = {{ {sbPts} }};");
        sb.AppendLine($"    lv_line_set_points({o}, {polyPts}, {Math.Max(2, pts.Count)});");
        sb.AppendLine($"    lv_obj_set_style_line_color({o}, {lcExpr}, 0);");
        sb.AppendLine($"    lv_obj_set_style_line_width({o}, {lineWidth}, 0);");
        if (control.LineCap?.Equals("Round", StringComparison.OrdinalIgnoreCase) == true)
            sb.AppendLine($"    lv_obj_set_style_line_rounded({o}, true, 0);");
    }

    /// <summary>Arc 控件专属参数生成（自 GenerateControlParameters 按控件族拆分）。</summary>
    private void EmitArcParameters(StringBuilder sb, HmiControl control, HmiTheme? theme, string o)
    {
        // I10: 圆弧 —— 轨道弧（MinValue..MaxValue 角度范围）+ 指示弧（Value 进度）。
        // 颜色与设计器一致：轨道 BgColor 兜底 Surface，指示 IndicatorColor??TextColor 兜底主色。
        var arcW = control.BorderWidth > 0 ? control.BorderWidth : ArcLayout.ArcWidthDefault;
        sb.AppendLine($"    lv_arc_set_rotation({o}, 0);");
        sb.AppendLine($"    lv_arc_set_bg_angles({o}, {(int)control.MinValue}, {(int)control.MaxValue});");
        sb.AppendLine($"    lv_arc_set_range({o}, {(int)control.MinValue}, {(int)Math.Max(control.MinValue + 1, control.MaxValue)});");
        sb.AppendLine($"    lv_arc_set_value({o}, {(int)control.Value});");
        sb.AppendLine($"    lv_obj_set_style_arc_width({o}, {arcW}, LV_PART_MAIN);");
        sb.AppendLine($"    lv_obj_set_style_arc_width({o}, {arcW}, LV_PART_INDICATOR);");
        var arcTrack = !string.IsNullOrEmpty(control.BgColor)
            ? control.BgColor.TrimStart('#') : PaletteDefaults.Surface.TrimStart('#');
        sb.AppendLine($"    lv_obj_set_style_arc_color({o}, lv_color_hex(0x{arcTrack}), LV_PART_MAIN);");
        sb.AppendLine($"    lv_obj_set_style_arc_opa({o}, LV_OPA_COVER, LV_PART_MAIN);");
        var arcInd = !string.IsNullOrEmpty(control.IndicatorColor)
            ? control.IndicatorColor.TrimStart('#')
            : (!string.IsNullOrEmpty(control.TextColor) ? control.TextColor.TrimStart('#')
                : PaletteDefaults.Primary.TrimStart('#'));
        sb.AppendLine($"    lv_obj_set_style_arc_color({o}, lv_color_hex(0x{arcInd}), LV_PART_INDICATOR);");
        sb.AppendLine($"    lv_obj_set_style_arc_opa({o}, LV_OPA_COVER, LV_PART_INDICATOR);");
        // 纯图形圆弧：隐藏 knob 与对象背景（Arc 无交互滑块）
        sb.AppendLine($"    lv_obj_set_style_opa({o}, LV_OPA_TRANSP, LV_PART_KNOB);");
        sb.AppendLine($"    lv_obj_set_style_bg_opa({o}, LV_OPA_TRANSP, 0);");
    }
}
