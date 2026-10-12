using System;

namespace HmiEditor.Core.Design;

/// <summary>
/// 控件旋转/镜像变换的纯数学实现（不依赖 UI 框架，可单元测试）。
/// 语义与 DesignCanvas.BuildControlTransform（Avalonia Matrix 版）完全一致：
/// 围绕控件中心 (w/2, h/2)，先镜像（水平/垂直）后旋转；y 向下坐标系，正角度 = 顺时针。
/// </summary>
public static class TransformMath
{
    /// <summary>
    /// 把控件局部点 (x, y)（0 ≤ x ≤ w, 0 ≤ y ≤ h）变换到旋转/镜像后的目标点。
    /// </summary>
    public static (double X, double Y) TransformPoint(
        double x, double y, double w, double h, double rotationDeg, bool mirrorX, bool mirrorY)
    {
        var cx = w / 2.0;
        var cy = h / 2.0;
        var lx = x - cx;
        var ly = y - cy;

        // 镜像（关于中心翻转）
        if (mirrorX) lx = -lx;
        if (mirrorY) ly = -ly;

        // 旋转（正角度 = 顺时针，y 向下坐标系）
        if (rotationDeg != 0)
        {
            var rad = rotationDeg * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            var rx = lx * cos - ly * sin;
            var ry = lx * sin + ly * cos;
            lx = rx;
            ly = ry;
        }

        return (lx + cx, ly + cy);
    }
}
