namespace HmiEditor.Core.Models;

using HmiEditor.Core.Design;

/// <summary>
/// 全局主题/样式配置 —— 影响所有屏幕的控件外观。
/// 对应 LVGL 的 lv_style_t 体系，编辑器中提供统一的主题色管理。
/// 默认值与 PaletteDefaults 同源（单一颜色来源）。
/// </summary>
public class HmiTheme
{
    /// <summary>主色调（按钮、滑块、开关激活色）</summary>
    public string PrimaryColor { get; set; } = PaletteDefaults.Primary;

    /// <summary>次色调（导出、确认操作）</summary>
    public string SecondaryColor { get; set; } = PaletteDefaults.Secondary;

    /// <summary>面板背景色（工具箱、属性面板）</summary>
    public string SurfaceColor { get; set; } = PaletteDefaults.Surface;

    /// <summary>画布背景色</summary>
    public string BackgroundColor { get; set; } = PaletteDefaults.Background;

    /// <summary>文本主色</summary>
    public string TextColor { get; set; } = PaletteDefaults.Text;

    /// <summary>禁用/次要文本色</summary>
    public string TextSecondaryColor { get; set; } = PaletteDefaults.TextSecondary;

    /// <summary>基础字号 (px)</summary>
    public int FontSize { get; set; } = 14;

    /// <summary>控件默认圆角 (px)</summary>
    public int CornerRadius { get; set; } = 6;

    /// <summary>控件默认边框宽度 (px)</summary>
    public int BorderWidth { get; set; } = 1;

    /// <summary>边框颜色</summary>
    public string BorderColor { get; set; } = PaletteDefaults.Border;

    /// <summary>成功色</summary>
    public string SuccessColor { get; set; } = PaletteDefaults.Success;

    /// <summary>警告色</summary>
    public string WarningColor { get; set; } = PaletteDefaults.Warning;

    /// <summary>错误/危险色</summary>
    public string ErrorColor { get; set; } = PaletteDefaults.Error;

    /// <summary>主题名称</summary>
    public string Name { get; set; } = "Dark Industrial";

    /// <summary>
    /// 将 #RRGGBB 格式转为 LVGL 颜色宏调用，如 "lv_color_hex(0x2563eb)"。
    /// </summary>
    public string ToLvglColor(string? hex) =>
        string.IsNullOrEmpty(hex) ? $"lv_color_hex(0x{PaletteDefaults.Primary.TrimStart('#')})"
            : $"lv_color_hex(0x{hex.TrimStart('#')})";
}
