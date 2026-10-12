namespace HmiEditor.CodeGen;

/// <summary>
/// LVGL v9 宏名映射工具 —— 将编辑器配置字符串映射为 LVGL v9 头文件中的枚举/宏名。
///
/// 注意：v9 相对 v8 的差异——
///   1. 长文本模式枚举去掉了 v8 的 _MODE_ 后缀（如 LV_LABEL_LONG_MODE_WRAP → LV_LABEL_LONG_WRAP）；
///   2. DOTS 更名为 DOT（LV_LABEL_LONG_MODE_DOTS → LV_LABEL_LONG_DOT）。
/// </summary>
public static class LvglMacros
{
    /// <summary>
    /// 将 Label 长文本模式配置映射为 LVGL v9 枚举。
    /// 支持 Wrap/Dots/Scroll/ScrollCircular/Clip；空值或未知值返回默认换行。
    /// </summary>
    public static string LabelLongMode(string? mode) => mode switch
    {
        "Dots" => "LV_LABEL_LONG_DOT",
        "Scroll" => "LV_LABEL_LONG_SCROLL",
        "ScrollCircular" => "LV_LABEL_LONG_SCROLL_CIRCULAR",
        "Clip" => "LV_LABEL_LONG_CLIP",
        _ => "LV_LABEL_LONG_WRAP"
    };
}
