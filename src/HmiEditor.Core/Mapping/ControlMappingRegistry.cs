using HmiEditor.Core.Models;

namespace HmiEditor.Core.Mapping;

public record PropertyMapping(
    string AvaloniaPropertyName,
    string LvglPropertyName,
    string LvglSetterFormat
);

public static class ControlMappingRegistry
{
    public static readonly Dictionary<HmiControlType, string> LvglCreateFunction = new()
    {
        [HmiControlType.Screen]   = "lv_obj_create",
        [HmiControlType.Button]   = "lv_button_create",
        [HmiControlType.Label]    = "lv_label_create",
        [HmiControlType.Slider]   = "lv_slider_create",
        [HmiControlType.Switch]   = "lv_switch_create",
        [HmiControlType.CheckBox] = "lv_checkbox_create",
        [HmiControlType.Image]    = "lv_image_create",
        [HmiControlType.Bar]      = "lv_bar_create",
        [HmiControlType.Dropdown] = "lv_dropdown_create",
        [HmiControlType.TextArea] = "lv_textarea_create",

        // Wave 2 新增
        [HmiControlType.Gauge]    = "lv_arc_create",
        [HmiControlType.Knob]     = "lv_arc_create",
        [HmiControlType.Keyboard] = "lv_keyboard_create",
        [HmiControlType.Led]      = "lv_led_create",
        [HmiControlType.Spinner]  = "lv_spinner_create",
        [HmiControlType.TabView]  = "lv_tabview_create",
        [HmiControlType.Line]     = "lv_line_create",
        [HmiControlType.Table]    = "lv_table_create",
        [HmiControlType.Svg]      = "lv_canvas_create",   // 兜底;实际走 CodeGen 专用 canvas 分支

        // I7 新增: 过程图形控件（均走 canvas + ProcessControlCodeGen 专用生成分支）
        [HmiControlType.Tank]     = "lv_canvas_create",
        [HmiControlType.Pipe]     = "lv_canvas_create",
        [HmiControlType.Valve]    = "lv_canvas_create",

        // I10 新增: 基础绘图图形
        [HmiControlType.Rectangle] = "lv_obj_create",   // 矩形: 通用 bg/radius/border 样式
        [HmiControlType.Ellipse]   = "lv_canvas_create", // 椭圆: canvas + ShapeControlCodeGen 专用生成分支
        [HmiControlType.Polyline]  = "lv_line_create",  // 折线: lv_line 多点
        [HmiControlType.Arc]       = "lv_arc_create",   // 圆弧: lv_arc 角度弧
    };

    public static readonly List<PropertyMapping> CommonPropertyMappings = new()
    {
        new("X", "x", "lv_obj_set_x"),
        new("Y", "y", "lv_obj_set_y"),
        new("Width", "w", "lv_obj_set_width"),
        new("Height", "h", "lv_obj_set_height"),
        new("Opacity", "opacity", "lv_obj_set_style_opa"),
        new("BgColor", "bg_color", "lv_obj_set_style_bg_color"),
        new("TextColor", "text_color", "lv_obj_set_style_text_color"),
        new("Radius", "radius", "lv_obj_set_style_radius"),
        new("BorderWidth", "border_width", "lv_obj_set_style_border_width"),
        new("BorderColor", "border_color", "lv_obj_set_style_border_color"),
    };

    public static string GetCreateFunction(HmiControlType type)
    {
        return LvglCreateFunction.TryGetValue(type, out var func)
            ? func
            : "lv_obj_create";
    }

    public static IReadOnlyList<PropertyMapping> GetMappingsForType(HmiControlType type)
    {
        return type switch
        {
            HmiControlType.Label or HmiControlType.Button or HmiControlType.CheckBox
                => CommonPropertyMappings,
            HmiControlType.Slider or HmiControlType.Bar or HmiControlType.Dropdown
                => CommonPropertyMappings,
            HmiControlType.Switch or HmiControlType.Image or HmiControlType.TextArea
                => CommonPropertyMappings,
            _ => CommonPropertyMappings,
        };
    }
}
