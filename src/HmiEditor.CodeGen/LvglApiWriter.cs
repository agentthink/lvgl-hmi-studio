using HmiEditor.Core.Mapping;
using HmiEditor.Core.Models;

namespace HmiEditor.CodeGen;

/// <summary>
/// LVGL API 编写器 —— 将 HmiControl 属性转换为 LVGL C API 调用字符串。
/// </summary>
public static class LvglApiWriter
{
    public static string WriteCreateCall(HmiControl control)
    {
        var createFunc = ControlMappingRegistry.GetCreateFunction(control.Type);
        var parent = control.Type == HmiControlType.Screen ? "lv_scr_act()" : "screen";
        return $"{createFunc}({parent})";
    }

    public static string WriteStyleCalls(HmiControl control, string objName)
    {
        var lines = new List<string>();

        foreach (var mapping in ControlMappingRegistry.CommonPropertyMappings)
        {
            var value = mapping.AvaloniaPropertyName switch
            {
                "X" => control.X,
                "Y" => control.Y,
                "Width" => control.Width,
                "Height" => control.Height,
                "Opacity" => (double?)null,
                "BgColor" => control.BgColor,
                "TextColor" => control.TextColor,
                "Radius" => control.Radius > 0 ? control.Radius : (object?)null,
                "BorderWidth" => control.BorderWidth > 0 ? control.BorderWidth : (object?)null,
                "BorderColor" => control.BorderColor,
                _ => null
            };

            if (value == null) continue;

            var formatSpec = mapping.LvglPropertyName switch
            {
                "x" or "y" or "w" or "h" => $"{mapping.LvglSetterFormat}({objName}, {value})",
                "opacity" or "radius" or "border_width" => $"{mapping.LvglSetterFormat}({objName}, {value}, 0)",
                "bg_color" or "text_color" or "border_color"
                    => $"{mapping.LvglSetterFormat}({objName}, lv_color_hex(0x{value?.ToString()?.TrimStart('#') ?? "000000"}), 0)",
                _ => ""
            };

            if (!string.IsNullOrEmpty(formatSpec))
                lines.Add($"    {formatSpec};");
        }

        return string.Join("\n", lines);
    }

    public static string WriteTextCall(HmiControl control, string objName)
    {
        if (string.IsNullOrEmpty(control.Text)) return "";
        return control.Type switch
        {
            HmiControlType.CheckBox
                => $"    lv_checkbox_set_text({objName}, \"{control.Text}\");",
            HmiControlType.Label or HmiControlType.Button
                => $"    lv_label_set_text({objName}, \"{control.Text}\");",
            _ => ""
        };
    }
}
