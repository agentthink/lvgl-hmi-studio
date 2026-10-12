using System.Text;
using System.Text.RegularExpressions;

namespace HmiEditor.CodeGen;

/// <summary>
/// LVGL 兼容性检查器 —— 生成代码后扫描 C 源码，
/// 检测 9.x 中已废弃/改名的 API，自动替换为当前版本写法，并将无法自动修复的问题以注释提示。
/// 仅当项目 LvglVersion 为 9.x 时生效（8.x 下这些旧 API 属正常用法）。
/// </summary>
public static class LvglApiCompatChecker
{
    /// <summary>
    /// 编辑器**认得的** LVGL 版本（升序，最后一项为最新）。
    ///
    /// <para>这份清单原来长在 <c>ProjectSettingsView.BuildLvglVersionItems()</c> 里 ——
    /// 但"哪些版本是合法的"是**版本兼容知识**，应该和解释这些版本的检查器放在一起；
    /// 放在视图里的话，别的入口（校验、目标插件对照）想用就得再抄一份。收在这里之后，
    /// 跨边界守卫可以拿它跟固件头文件里的 <c>HMI_UI_LVGL_VERSION</c> 直接比
    /// （见 <c>HmiEditor.Core.Tests.TargetContractVersionTests</c>）。</para>
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedLvglVersions =
        new[] { "8.3", "9.0", "9.1", "9.2", "9.3", "9.4", "9.5" };

    /// <summary>可安全自动替换的旧 API → LVGL 9.5 正确 API。</summary>
    private static readonly (string Old, string New)[] AutoReplacements =
    {
        // 9.x 改名
        ("lv_table_set_col_cnt(", "lv_table_set_column_count("),
        ("lv_table_add_cell_ctrl(", "lv_table_set_cell_ctrl("),
        ("lv_tabview_rename_tab(", "lv_tabview_set_tab_text("),
        ("lv_dropdown_set_selected_show_highlight(", "lv_dropdown_set_selected_highlight("),
        ("lv_slider_set_left_value(", "lv_slider_set_start_value("),

        // v8 → v9 常量改名
        ("LV_ZOOM_IMAGE_NONE", "LV_SCALE_NONE"),
        ("LV_TABLE_CELL_CTRL_MERGE)", "LV_TABLE_CELL_CTRL_MERGE_RIGHT)"),

        // v8 lv_img_* → v9 lv_image_*
        ("lv_img_create", "lv_image_create"),
        ("lv_img_set_src", "lv_image_set_src"),
        ("lv_img_set_scale", "lv_image_set_scale"),
        ("lv_img_set_rotation", "lv_image_set_rotation"),
        ("lv_img_set_antialias", "lv_image_set_antialias"),
        ("lv_img_set_zoom", "lv_image_set_scale"),
    };

    /// <summary>v9 已移除/不存在的 API → 提示与替代方案（无法安全自动替换）。</summary>
    private static readonly (string Pattern, string Suggestion)[] RemovedApis =
    {
        ("lv_bar_set_anim_time",
            "动画时长改用样式属性 lv_obj_set_style_anim_duration(obj, ms, LV_PART_MAIN)，值动画经 lv_bar_set_value(obj, v, LV_ANIM_ON) 触发"),
        ("lv_switch_set_anim_time",
            "旋钮动画时长改用样式属性 lv_obj_set_style_anim_duration(obj, ms, LV_PART_MAIN)"),
        ("lv_textarea_set_cursor_blink_time",
            "光标闪烁周期改用样式属性 anim_time（作用于 LV_PART_CURSOR）"),
        ("lv_line_set_point_count",
            "该 API 不存在；点数由 lv_line_set_points(obj, pts, count) 决定，读取用 lv_line_get_point_count"),
        ("lv_image_set_scale_mode",
            "该 API 不存在；缩放用 lv_image_set_scale/scale_x/scale_y，对齐用 lv_image_set_inner_align"),
        ("lv_tabview_set_tab_bar_buttons_show",
            "该 API 不存在；隐藏页签栏用 lv_obj_add_flag(lv_tabview_get_tab_bar(tv), LV_OBJ_FLAG_HIDDEN)"),
        ("lv_checkbox_set_checked",
            "该 API 不存在；勾选/取消用 lv_obj_add_state(cb, LV_STATE_CHECKED) / lv_obj_remove_state(cb, LV_STATE_CHECKED)"),
        ("lv_canvas_draw_",
            "v9 移除 lv_canvas_draw_* 系列；改用 lv_canvas_init_layer + lv_draw_rect/text/arc/line(&layer, &dsc) + lv_canvas_finish_layer"),
        ("lv_slider_set_knob_area",
            "该 API 从未存在；旋钮尺寸用 LV_PART_KNOB 样式（width/height/padding）控制"),
        ("lv_canvas_set_px_opa",
            "LVGL 9.x 无此 API；整幅透明填充用 lv_canvas_fill_bg(obj, lv_color_black(), LV_OPA_TRANSP)，单像素用 lv_canvas_set_px(obj, x, y, color, opa)"),
        ("lv_draw_polygon",
            "LVGL 9.x 无多边形绘制 API；用 lv_draw_triangle 三角扇近似，或改用 lv_canvas_set_px 逐像素"),
        ("lv_obj_set_hidden",
            "LVGL 9.x 已移除；显示/隐藏用 lv_obj_remove_flag(obj, LV_OBJ_FLAG_HIDDEN) / lv_obj_add_flag(obj, LV_OBJ_FLAG_HIDDEN)"),
        ("lv_obj_set_click_focusable",
            "LVGL 9.x 已移除；改用 lv_obj_add_flag(obj, LV_OBJ_FLAG_CLICK_FOCUSABLE) / lv_obj_remove_flag(...)"),
        ("lv_obj_set_click(",
            "LVGL 9.x 已移除；可点击性用 LV_OBJ_FLAG_CLICKABLE 标志（lv_obj_add_flag/lv_obj_remove_flag）"),
    };

    /// <summary>
    /// 已移除/不存在的 API 模式（只读暴露，供自洽性测试使用）。
    /// 末尾带 <c>(</c> 的条目按前缀匹配；测试据此断言它
    /// 与 <c>GeneratedCodeValidator.KnownLvglApiNames</c> 不冲突。
    /// </summary>
    public static IReadOnlyCollection<string> RemovedApiPatterns { get; } =
        RemovedApis.Select(p => p.Pattern).ToArray();

    /// <summary>
    /// 对生成的 C 代码执行兼容性处理：自动替换废弃 API，并将检测结果以注释头插入。
    /// 无问题时不改动原代码。
    /// </summary>
    /// <param name="code">生成的 C 代码。</param>
    /// <param name="lvglVersion">项目声明的 LVGL 版本（如 "9.5" / "8.3"）。</param>
    public static string Process(string code, string lvglVersion)
    {
        if (string.IsNullOrEmpty(code)) return code;

        // 仅对 9.x 项目启用检查（8.x 下旧 API 属正常用法）
        if (!IsVersion9(lvglVersion)) return code;

        var issues = new List<string>();
        var result = code;

        // 1. 自动替换可安全修复的旧 API
        foreach (var (old, replacement) in AutoReplacements)
        {
            if (!result.Contains(old, StringComparison.Ordinal)) continue;
            issues.Add($"自动替换 {old}(…) → {replacement}(…)");
            result = result.Replace(old, replacement);
        }

        // 2. 检测已移除/不存在的 API（提示，不自动改）
        foreach (var (pattern, suggestion) in RemovedApis)
        {
            if (result.Contains(pattern, StringComparison.Ordinal))
                issues.Add($"已废弃 API「{pattern}」：{suggestion}");
        }

        // 3. 特殊检测：lv_spinner_create 多参形式（v8 写法，v9 仅单参）
        if (Regex.IsMatch(result, @"lv_spinner_create\(\s*[^,]+,\s*[^,]+,"))
            issues.Add("已废弃 API「lv_spinner_create(多参)」：v9 仅 lv_spinner_create(parent)，动画参数用 lv_spinner_set_anim_params(obj, time, angle)");

        if (issues.Count == 0) return result;

        var header = new StringBuilder();
        header.AppendLine("/*");
        header.AppendLine(" * ── LVGL 兼容性检查（HmiEditor 自动生成） ──");
        foreach (var issue in issues)
            header.AppendLine($" *  - {issue}");
        header.AppendLine(" * ────────────────────────────────────────");
        header.AppendLine(" */");
        header.AppendLine();
        return header + result;
    }

    /// <summary>判断版本号是否为 9.x。</summary>
    private static bool IsVersion9(string version)
    {
        if (string.IsNullOrEmpty(version)) return false;
        var major = version.Split('.', '-')[0].Trim();
        return major == "9";
    }
}
