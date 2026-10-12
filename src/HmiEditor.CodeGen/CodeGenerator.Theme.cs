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
/// CodeGenerator 分部 —— 主题样式初始化发射 —— GenerateThemeCode。
/// </summary>
public partial class CodeGenerator
{
    /// <summary>
    /// 生成主题样式初始化的 C 代码 (LVGL v9 兼容)。
    /// v9 使用 lv_obj_set_style_* 直接在 screen 上设置部件级样式作为默认值，
    /// 子控件可通过继承或覆盖获得主题外观。
    /// </summary>
    public string GenerateThemeCode(HmiTheme theme, AssetLibrary? assetLibrary = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// ── Theme Styles ──");
        sb.AppendLine($"// Name: {EscapeComment(theme.Name)}");
        
        // 获取资产库中的字体资源
        var fontAssets = assetLibrary?.Assets.Where(a => a.Type == AssetType.Font).ToList() ?? new List<AssetItem>();
        string fontToUse;
        
        if (fontAssets.Count > 0)
        {
            var sanitizedId = CIdentifier.ForAsset(fontAssets[0].Id);
            // .so 模式：font_<id> 是 hmi_ui_init_fonts 解析出的指针变量（不带 &）；
            // 模拟器模式：extern lv_font_t 对象（带 &）。
            fontToUse = _soMode
                ? CResourceDeclarations.FontVariable(sanitizedId)
                : $"&{CResourceDeclarations.FontVariable(sanitizedId)}";
        }
        else
        {
            // 无自定义字体时，回退到嵌入的 CJK 子集字体（而非 LV_FONT_DEFAULT=Montserrat，
            // 后者无中文字形会导致中文方框）。
            // .so(固件) 用 Source Han Sans SC 14px 嵌入子集；模拟器用 SimSun 14px 嵌入子集。
            fontToUse = _soMode ? "&lv_font_source_han_sans_sc_14_cjk" : "&lv_font_simsun_14_cjk";
        }
        
        sb.AppendLine("void apply_theme_styles(void)");
        sb.AppendLine("{");
        sb.AppendLine("    lv_obj_t* scr = lv_screen_active();");
        sb.AppendLine();
        sb.AppendLine("    /* 屏幕背景色由各屏 create_screen_* 按「自定义优先、否则主题色」设置，");
        sb.AppendLine("     * 这里不再覆盖，避免用户为屏幕设置的 BgColor 被主题色无条件替换。 */");
        sb.AppendLine("    /* 可继承的默认文本样式：LVGL 的 text_color/text_font 具有继承标志，");
        sb.AppendLine("     * 设置到屏幕后自动传递给所有子控件。");
        sb.AppendLine("     * 背景/圆角/边框/指示器等非继承属性由控件创建处按类型单独应用。 */");
        sb.AppendLine($"    lv_obj_set_style_text_color(scr, {theme.ToLvglColor(theme.TextColor)}, 0);");
        sb.AppendLine($"    lv_obj_set_style_text_font(scr, {fontToUse}, 0);");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
