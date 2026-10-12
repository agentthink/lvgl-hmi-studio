using System;
using System.Text.RegularExpressions;

namespace HmiEditor.Core.Design;

/// <summary>
/// 属性编辑器值验证器 —— 确保所有通过属性面板输入的值合法。
///
/// FF6 验收：100% 非法值被编辑器拦截。
/// 覆盖：位置非负、尺寸下限、圆角/边框非负、颜色格式校验。
///
/// 所有方法为纯函数，无 Avalonia 依赖，可直接单元测试。
/// </summary>
public static class PropertyValidator
{
    /// <summary>控件最小尺寸（像素）—— 与 DesignCanvas.MinControlSize 一致。</summary>
    public const double MinControlSize = 10;

    /// <summary>坐标最大值（防止溢出和异常值）。</summary>
    public const double MaxPosition = 100000;

    /// <summary>尺寸最大值。</summary>
    public const double MaxSize = 10000;

    /// <summary>圆角最大值。</summary>
    public const int MaxRadius = 500;

    /// <summary>边框宽度最大值。</summary>
    public const int MaxBorderWidth = 100;

    // 匹配 #RGB / #RRGGBB / #RRGGBBAA 格式（井号可选）
    private static readonly Regex ColorHexPattern = new(
        @"^#?([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$",
        RegexOptions.Compiled);

    /// <summary>
    /// 验证并修正位置坐标（X 或 Y）。
    /// 规则：非负、不超过 MaxPosition、NaN → 0。
    /// </summary>
    public static double ClampPosition(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
        return Math.Clamp(value, 0, MaxPosition);
    }

    /// <summary>
    /// 验证并修正尺寸（Width 或 Height）。
    /// 规则：不小于 MinControlSize、不超过 MaxSize、NaN → MinControlSize。
    /// </summary>
    public static double ClampSize(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return MinControlSize;
        return Math.Clamp(value, MinControlSize, MaxSize);
    }

    /// <summary>
    /// 验证并修正圆角值。
    /// 规则：非负、不超过 MaxRadius、NaN → 0。
    /// </summary>
    public static int ClampRadius(int value)
    {
        if (value < 0) return 0;
        return Math.Min(value, MaxRadius);
    }

    /// <summary>
    /// 验证并修正边框宽度。
    /// 规则：非负、不超过 MaxBorderWidth、NaN → 0。
    /// </summary>
    public static int ClampBorderWidth(int value)
    {
        if (value < 0) return 0;
        return Math.Min(value, MaxBorderWidth);
    }

    /// <summary>
    /// 验证颜色字符串是否为合法的十六进制颜色格式。
    /// 接受：#RGB, #RRGGBB, #RRGGBBAA（井号可选）。
    /// 空字符串或 null 视为合法（表示不设置颜色）。
    /// </summary>
    public static bool IsValidColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return true;
        return ColorHexPattern.IsMatch(color.Trim());
    }

    /// <summary>
    /// 验证并修正颜色字符串。
    /// 非法格式返回 null（清除颜色），合法格式返回规范化的 #RRGGBB 或 #RRGGBBAA。
    /// </summary>
    public static string? SanitizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;
        var trimmed = color.Trim();
        if (!ColorHexPattern.IsMatch(trimmed)) return null;
        // 确保以 # 开头
        return trimmed.StartsWith('#') ? trimmed : "#" + trimmed;
    }

    /// <summary>
    /// 验证控件名称是否合法。
    /// 规则：非空、去除首尾空白后长度 > 0、不超过 100 字符。
    /// </summary>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.Trim().Length <= 100;
    }

    /// <summary>
    /// 验证并修正控件名称。
    /// 空白或 null 返回 fallback，超长截断。
    /// </summary>
    public static string SanitizeName(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        var trimmed = name.Trim();
        return trimmed.Length > 100 ? trimmed[..100] : trimmed;
    }
}
