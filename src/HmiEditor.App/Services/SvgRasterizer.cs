using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HmiEditor.App.Controls;

namespace HmiEditor.App.Services;

/// <summary>
/// SVG → 位图光栅化器 —— 将 SVG 文本渲染为 PNG 位图，供导出使用。
/// 利用 <see cref="SvgImage"/> 控件的渲染能力 + RenderTargetBitmap 实现。
/// </summary>
public static class SvgRasterizer
{
    /// <summary>
    /// 将 SVG 文本渲染为 PNG 位图。
    /// </summary>
    /// <param name="svgText">SVG XML 文本</param>
    /// <param name="pixelWidth">输出像素宽度</param>
    /// <param name="pixelHeight">输出像素高度</param>
    /// <returns>PNG 位图，失败返回 null</returns>
    public static Bitmap? RenderToBitmap(string svgText, int pixelWidth, int pixelHeight)
    {
        if (string.IsNullOrWhiteSpace(svgText) || pixelWidth <= 0 || pixelHeight <= 0)
            return null;

        var visual = new SvgImage { SvgText = svgText };
        var size = new Size(pixelWidth, pixelHeight);
        visual.Measure(size);
        visual.Arrange(new Rect(size));

        try
        {
            var rtb = new RenderTargetBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(96, 96));
            rtb.Render(visual);
            return rtb;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将 SVG 文本导出为 PNG 文件。
    /// </summary>
    /// <param name="svgText">SVG XML 文本</param>
    /// <param name="filePath">输出 PNG 文件路径</param>
    /// <param name="pixelWidth">输出像素宽度</param>
    /// <param name="pixelHeight">输出像素高度</param>
    /// <returns>是否成功</returns>
    public static bool ExportToPng(string svgText, string filePath, int pixelWidth, int pixelHeight)
    {
        var bitmap = RenderToBitmap(svgText, pixelWidth, pixelHeight);
        if (bitmap is null) return false;

        try
        {
            bitmap.Save(filePath, new PngBitmapEncoderOptions());
            return true;
        }
        catch
        {
            return false;
        }
    }
}
