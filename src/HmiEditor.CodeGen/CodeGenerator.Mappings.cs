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
/// CodeGenerator 分部 —— 枚举/值 → LVGL 常量映射与查询 —— 表格列宽、文本对齐、九宫格对齐、图片缩放模式、颜色亮度、字体表达式、缓动路径。
/// </summary>
public partial class CodeGenerator
{
    /// <summary>
    /// 计算表格每列的宽度表达式（C 常量或 LV_COORD_MAX），供 lv_table_set_column_width 使用。
    /// 语义：空规格 → 均分填满控件宽度（左右均衡）；"auto" → LV_COORD_MAX（按内容自适应）；
    /// 数值 → 绝对 px；Fill 列 → 均分「剩余空间」（控件宽 − 已指定 px 列总和）。
    /// </summary>
    private static IReadOnlyList<string> TableColumnWidthExpr(HmiControl control)
    {
        var cols = Math.Max(1, control.ColumnCount);
        var widths = TableLayoutHelper.ParseColumnWidths(control.ColumnWidths, cols);

        double fixedSum = 0;
        var fillCount = 0;
        foreach (var w in widths)
        {
            if (w > 0) fixedSum += w;
            else if (w == TableLayoutHelper.Fill) fillCount++;
        }

        var fillEach = fillCount > 0 ? Math.Max(0, (control.Width - fixedSum) / fillCount) : 0;
        if (fillEach < 1) fillEach = Math.Max(1, control.Width / cols);   // 兜底：固定列已超宽时退回均分

        var result = new List<string>(cols);
        foreach (var w in widths)
        {
            if (w == TableLayoutHelper.Auto) result.Add("LV_COORD_MAX");
            else if (w > 0) result.Add(((int)w).ToString());
            else result.Add(((int)fillEach).ToString());
        }
        return result;
    }

    /// <summary>文本水平对齐 → LVGL text_align 常量。</summary>
    private static string LvglTextAlign(TextAlignMode mode) => mode switch
    {
        TextAlignMode.Left => "LV_TEXT_ALIGN_LEFT",
        TextAlignMode.Right => "LV_TEXT_ALIGN_RIGHT",
        _ => "LV_TEXT_ALIGN_CENTER"
    };

    /// <summary>水平/垂直对齐 → LVGL 9 宫格 LV_ALIGN_* 常量。</summary>
    private static string LvglAlign(TextAlignMode h, TextVAlignMode v) => (v, h) switch
    {
        (TextVAlignMode.Top, TextAlignMode.Left) => "LV_ALIGN_TOP_LEFT",
        (TextVAlignMode.Top, TextAlignMode.Center) => "LV_ALIGN_TOP_MID",
        (TextVAlignMode.Top, TextAlignMode.Right) => "LV_ALIGN_TOP_RIGHT",
        (TextVAlignMode.Middle, TextAlignMode.Left) => "LV_ALIGN_LEFT_MID",
        (TextVAlignMode.Middle, TextAlignMode.Center) => "LV_ALIGN_CENTER",
        (TextVAlignMode.Middle, TextAlignMode.Right) => "LV_ALIGN_RIGHT_MID",
        (TextVAlignMode.Bottom, TextAlignMode.Left) => "LV_ALIGN_BOTTOM_LEFT",
        (TextVAlignMode.Bottom, TextAlignMode.Center) => "LV_ALIGN_BOTTOM_MID",
        _ => "LV_ALIGN_BOTTOM_RIGHT"
    };

    /// <summary>屏幕背景图片缩放模式 → LVGL lv_image_align_t。</summary>
    private static string LvglImageAlign(ScreenBgImageMode mode) => mode switch
    {
        ScreenBgImageMode.Stretch => "LV_IMAGE_ALIGN_STRETCH",
        ScreenBgImageMode.Tile => "LV_IMAGE_ALIGN_TILE",
        ScreenBgImageMode.Center => "LV_IMAGE_ALIGN_CENTER",
        _ => "LV_IMAGE_ALIGN_STRETCH"
    };

    /// <summary>图片控件缩放模式 → LVGL lv_image_align_t（解析与设计器共用 StretchModeMap）。</summary>
    private static string LvglImageAlign(string? stretchMode) =>
        LvglImageAlign(StretchModeMap.Parse(stretchMode));

    /// <summary>
    /// 缩放模式语义 → LVGL lv_image_align_t。
    ///
    /// <para>接受<b>已解析的枚举</b>而非字符串：调用方需要按模式做分支时（如 CONTAIN 的版本兜底）
    /// 直接用同一个 kind 判断，避免"先在别处比对生成的字符串"——那种写法在映射表变化时静默失效
    /// （曾有一处 `imgAlign is "…CONTAIN" or "…COVER"`，其中 COVER 永远不可能命中）。</para>
    /// </summary>
    private static string LvglImageAlign(StretchModeKind kind) => kind switch
    {
        StretchModeKind.Contain => "LV_IMAGE_ALIGN_CONTAIN",
        StretchModeKind.Tile => "LV_IMAGE_ALIGN_TILE",
        StretchModeKind.Center => "LV_IMAGE_ALIGN_CENTER",
        _ => "LV_IMAGE_ALIGN_STRETCH"
    };

    /// <summary>判断 hex 颜色亮度是否偏亮（挑选高对比三角形前景色，与设计器 IsLightColor 同规则）。</summary>
    private static bool IsHexLight(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length < 6) return false;
        try
        {
            var r = Convert.ToInt32(hex.Substring(0, 2), 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return (0.299 * r + 0.587 * g + 0.114 * b) > 140;
        }
        catch { return false; }
    }

    /// <summary>
    /// 根据控件字体设置返回 LVGL 字体表达式。
    /// 规则：FontFamily 指向可用字体资产时优先使用；若同时指定 FontSize 且与主题默认不同，
    /// 在 FreeType 模拟器下使用 `font_{id}_{size}` 变量（由 generated_fonts.c 创建），
    /// 否则回退到基础字体变量 `font_{id}`；无自定义字体时返回 null，表示使用主题默认字体。
    /// </summary>
    /// <summary>
    /// **无自定义字体资产时的内置字号近似映射**（阶段 220，登记 `#97`）。
    ///
    /// <para>背景：`GetControlFontExpression` 在没有字体资产时返回 null（"用主题默认字体"）——
    /// 于是面板里把字号从 14 调到 20，**预览变大、导出没变**。这里给它一条兜底：
    /// 用 **LVGL 内置 `lv_font_montserrat_{12,14,16}`**（导出/模拟器的 lv_conf 只启用这三档，
    /// 见 `SimulatorProjectExporter.Configs.cs`）就近近似 —— 比主题默认**小**取 12、**大**取 16。</para>
    ///
    /// <para>返回 null 的两种情况：① 控件指定了字体族（走资产路，不该被内置字号顶掉）；
    /// ② 字号与主题默认一致（本来就不该发射任何字体设置）。调用方要用
    /// <c>#if LV_FONT_MONTSERRAT_NN</c> 包住发射 —— 设备端没启用该档时整段编译消失（安全）。</para>
    /// </summary>
    private int? BuiltInFontSizeFor(HmiControl control, HmiTheme? theme)
    {
        if (!string.IsNullOrEmpty(control.FontFamily)) return null;

        // 阶段 228：主题默认与请求字号都按屏幕 DPI 换算后再比（两边一起乘 ⇒ 语义不变）
        var themeSize = (int)Math.Round((theme?.FontSize ?? HmiControl.DefaultFontSize) * _dpiScale);
        var size = (int)Math.Round((control.FontSize > 0 ? control.FontSize : (theme?.FontSize ?? HmiControl.DefaultFontSize)) * _dpiScale);
        if (size == themeSize) return null;

        return size < themeSize ? 12 : 16;
    }

    private string? GetControlFontExpression(HmiControl control, HmiTheme? theme, HashSet<string>? availableFontIds)
    {
        var family = control.FontFamily;
        // 阶段 228（#98 进导出第二半）：字号是**几何量** ⇒ 先乘屏幕 DPI 再比较/取字体变体
        var rawSize = control.FontSize > 0 ? control.FontSize : (theme?.FontSize ?? 14);
        var rawThemeSize = theme?.FontSize ?? 14;
        var size = (int)Math.Round(rawSize * _dpiScale);
        var themeSize = (int)Math.Round(rawThemeSize * _dpiScale);

        // 未指定字体族且字号与主题默认一致 → 使用主题字体（返回 null 由调用方决定）
        if (string.IsNullOrEmpty(family) && size == themeSize)
            return null;

        // 未指定字体族但字号不同 → 尝试使用第一个可用字体资产生成变尺寸字体
        if (string.IsNullOrEmpty(family))
        {
            var firstFontId = availableFontIds?.FirstOrDefault();
            if (string.IsNullOrEmpty(firstFontId)) return null;
            family = firstFontId;
        }

        var sanitizedId = CIdentifier.ForAsset(family);
        if (availableFontIds != null && !availableFontIds.Contains(sanitizedId))
            return null;

        if (size != themeSize)
            return _soMode
                ? CResourceDeclarations.FontVariantVariable(sanitizedId, size)
                : $"&{CResourceDeclarations.FontVariantVariable(sanitizedId, size)}";
        return _soMode
            ? CResourceDeclarations.FontVariable(sanitizedId)
            : $"&{CResourceDeclarations.FontVariable(sanitizedId)}";
    }
}
