using System;

namespace HmiEditor.Core.Design;

/// <summary>
/// 画布坐标变换工具 —— 屏幕视口坐标与模型坐标之间的双向转换。
///
/// FF5 验收：拖放落点精度 < 5px（缩放补偿后）。
/// 公式：
///   model = (screen - pan) / zoom
///   screen = model * zoom + pan
///
/// 所有方法为纯函数，无 Avalonia 依赖，可直接单元测试。
/// </summary>
public static class CoordinateTransform
{
    /// <summary>最小有效缩放值（防止除零和极端值）。</summary>
    public const double MinZoom = 0.01;

    /// <summary>
    /// 将屏幕/视口坐标转换为画布模型坐标。
    /// 补偿缩放和平移偏移。
    /// </summary>
    /// <param name="screenX">视口 X 坐标（鼠标位置）</param>
    /// <param name="screenY">视口 Y 坐标（鼠标位置）</param>
    /// <param name="panX">平移偏移 X</param>
    /// <param name="panY">平移偏移 Y</param>
    /// <param name="zoom">缩放级别（>0）</param>
    /// <returns>模型坐标 (X, Y) —— 即控件在 HmiScreen 中的逻辑坐标</returns>
    public static (double X, double Y) ScreenToModel(
        double screenX, double screenY,
        double panX, double panY,
        double zoom)
    {
        var z = Math.Max(zoom, MinZoom);
        return ((screenX - panX) / z, (screenY - panY) / z);
    }

    /// <summary>
    /// 将画布模型坐标转换为屏幕/视口坐标。
    /// </summary>
    public static (double X, double Y) ModelToScreen(
        double modelX, double modelY,
        double panX, double panY,
        double zoom)
    {
        var z = Math.Max(zoom, MinZoom);
        return (modelX * z + panX, modelY * z + panY);
    }

    /// <summary>
    /// 判断给定的屏幕坐标是否命中某个控件（模型坐标矩形）。
    /// 用于 HitTestControl 逻辑的可测试版本。
    /// </summary>
    /// <param name="screenX">视口 X 坐标</param>
    /// <param name="screenY">视口 Y 坐标</param>
    /// <param name="ctrlX">控件模型 X</param>
    /// <param name="ctrlY">控件模型 Y</param>
    /// <param name="ctrlW">控件模型宽度</param>
    /// <param name="ctrlH">控件模型高度</param>
    /// <param name="panX">平移偏移 X</param>
    /// <param name="panY">平移偏移 Y</param>
    /// <param name="zoom">缩放级别</param>
    /// <returns>true 如果屏幕坐标落在控件矩形内</returns>
    public static bool HitTest(
        double screenX, double screenY,
        double ctrlX, double ctrlY,
        double ctrlW, double ctrlH,
        double panX, double panY,
        double zoom)
    {
        var z = Math.Max(zoom, MinZoom);
        // 控件在视口中的矩形
        var viewX = ctrlX * z + panX;
        var viewY = ctrlY * z + panY;
        var viewW = ctrlW * z;
        var viewH = ctrlH * z;

        return screenX >= viewX && screenX <= viewX + viewW
            && screenY >= viewY && screenY <= viewY + viewH;
    }
}
