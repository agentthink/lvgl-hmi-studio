using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// I9: 自适应布局计算器 —— 纯函数，编辑器预览与 CodeGen 共用同一套缩放语义。
///
/// 输入: 设计分辨率(baseW×baseH) → 目标分辨率(targetW×targetH) + 模式。
/// 输出: (ScaleX, ScaleY, OffsetX, OffsetY)，即"设计坐标 → 目标坐标"的仿射映射:
///       x' = x * ScaleX + OffsetX, y' = y * ScaleY + OffsetY
/// </summary>
public static class AdaptiveLayoutCalculator
{
    /// <summary>计算结果。</summary>
    public readonly record struct AdaptiveTransform(
        double ScaleX, double ScaleY, double OffsetX, double OffsetY)
    {
        /// <summary>控件矩形在设计坐标系 → 目标坐标系。</summary>
        public (double X, double Y, double W, double H) Map(
            double x, double y, double w, double h)
            => (x * ScaleX + OffsetX, y * ScaleY + OffsetY, w * ScaleX, h * ScaleY);
    }

    /// <summary>
    /// 计算自适应变换。
    /// None 模式或无有效尺寸时返回恒等变换 (1,1,0,0)。
    /// </summary>
    public static AdaptiveTransform Compute(
        double baseWidth, double baseHeight,
        double targetWidth, double targetHeight,
        ScreenAdaptiveMode mode)
    {
        if (mode == ScreenAdaptiveMode.None)
            return new AdaptiveTransform(1, 1, 0, 0);

        if (baseWidth <= 0 || baseHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
            return new AdaptiveTransform(1, 1, 0, 0);

        var sx = targetWidth / baseWidth;
        var sy = targetHeight / baseHeight;

        if (mode == ScreenAdaptiveMode.Fill)
            return new AdaptiveTransform(sx, sy, 0, 0);

        // Fit: 等比缩放 + 居中
        var s = Math.Min(sx, sy);
        var ox = (targetWidth - baseWidth * s) / 2.0;
        var oy = (targetHeight - baseHeight * s) / 2.0;
        return new AdaptiveTransform(s, s, ox, oy);
    }

    /// <summary>从 "WxH" 文本解析目标分辨率（如 "1024x600"），失败返回 null。</summary>
    public static (double Width, double Height)? TryParseResolution(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split('x', '×', 'X');
        if (parts.Length != 2) return null;
        if (!double.TryParse(parts[0], out var w) || !double.TryParse(parts[1], out var h))
            return null;
        if (w <= 0 || h <= 0) return null;
        return (w, h);
    }
}
