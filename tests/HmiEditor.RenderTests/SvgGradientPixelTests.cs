using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media.Imaging;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.RenderTests;

/// <summary>
/// P1-3 **C 段的像素级判据**（补上 <c>SvgGradientCanvasTests</c> 只到"绘制指令级"的那个缺口）。
///
/// <para><b>这两者的差别是实质性的</b>：指令级断言的是"我往画刷里写了什么"，
/// 像素级断言的是"渲染出来是什么"。画刷端点用的 <c>RelativeUnit.Absolute</c> 到底相对于
/// 什么坐标系，指令级测试**看不出来**（它断言的正是自己写进去的那个值）—— 只有真渲染才暴露。</para>
///
/// <para><b>判据刻意做成实现无关的</b>：不断言精确插值色值（那取决于 Skia 在 sRGB 还是线性
/// 空间插值，换版本就可能变），而是断言 ①方向与单调性；②两端确实到达端点色；③**红→蓝渐变
/// 的中点必然 R==B** —— 这条在任何插值空间下都成立，且"端点接反""根本没画渐变""画成纯色"
/// 三种坏法都会让它红。</para>
/// </summary>
public class SvgGradientPixelTests
{
    private const string Xmlns = "http://www.w3.org/2000/svg";
    private const int ScreenW = 400, ScreenH = 300;

    /// <summary>红→蓝两点线性渐变（objectBoundingBox，默认 0%,0% → 100%,0%，水平）。</summary>
    private const string LinearDefs =
        "<defs><linearGradient id=\"g\">" +
        "<stop offset=\"0\" stop-color=\"#ff0000\"/><stop offset=\"1\" stop-color=\"#0000ff\"/>" +
        "</linearGradient></defs>";

    // 形状在屏幕上的位置：控件在 (40,20)、尺寸 200x200、viewBox 100x100（sx=sy=2），
    // 形状是 <rect 0 0 100 50> ⇒ 控件内 (0,0)-(200,100) ⇒ 屏幕 (40,20)-(240,120)。
    private const int ShapeLeft = 40, ShapeTop = 20, ShapeRight = 240, ShapeBottom = 120;

    /// <summary>取样行的 y（形状垂直中点，离上下边界各 50 px）。</summary>
    private const int MidY = 70;

    [Fact]
    public void LinearGradient_PixelsRunMonotonicallyFromRedToBlue()
    {
        RenderTestHost.Run(() =>
        {
            var pixels = Render(LinearSvg(rotated: false));

            // 5 个等距取样点，全部落在形状内部（离左右边界 ≥ 5 px，避开抗锯齿边缘）
            var xs = new[] { 45, 92, 140, 187, 235 };
            var samples = new List<(int X, byte R, byte G, byte B)>();
            foreach (var x in xs)
            {
                var (b, g, r, a) = PixelReader.At(pixels, ScreenW, x, MidY);
                Assert.Equal(255, a); // 形状不透明：命中的确实是被画上的像素，不是背景
                samples.Add((x, r, g, b));
            }

            // ① 方向：左端接近红、右端接近蓝。容差放到 40 —— 这里要证的是"端点没接反、确实到达了端点"，
            //    不是"插值实现是哪个色彩空间"。
            Assert.True(samples[0].R > 215 && samples[0].B < 40,
                $"左端应是红色附近，实测 R={samples[0].R} B={samples[0].B}");
            Assert.True(samples[^1].B > 215 && samples[^1].R < 40,
                $"右端应是蓝色附近，实测 R={samples[^1].R} B={samples[^1].B}");

            // ② 单调：R 严格递减、B 严格递增（"没画渐变/画成纯色"在这里就红了）
            for (var i = 1; i < samples.Count; i++)
            {
                Assert.True(samples[i].R < samples[i - 1].R,
                    $"R 应逐点递减：x={samples[i - 1].X} 时 {samples[i - 1].R}，x={samples[i].X} 时 {samples[i].R}");
                Assert.True(samples[i].B > samples[i - 1].B,
                    $"B 应逐点递增：x={samples[i - 1].X} 时 {samples[i - 1].B}，x={samples[i].X} 时 {samples[i].B}");
            }

            // ③ 中点：红→蓝的中点在**任何**插值空间下两通道都该相等（sRGB 空间 ⇒ 128/128，
            //    线性空间 ⇒ 188/188，但 R == B 恒成立）。这条是整套判据里最抗实现变化的一条。
            var mid = samples[2];
            Assert.True(Math.Abs(mid.R - mid.B) <= 12,
                $"红→蓝渐变的中点应 R≈B，实测 R={mid.R} B={mid.B}");

            // ④ 绿通道自始至终为 0（两个停靠点的绿分量都是 0）。若有人把停靠点顺序或取样搞错，
            //    混进了别的颜色来源，这里会红。
            foreach (var s in samples)
                Assert.True(s.G <= 8, $"绿通道应恒为 0，x={s.X} 实测 G={s.G}");
        });
    }

    [Fact]
    public void RadialGradient_CenterIsBrighterThanEdge()
    {
        RenderTestHost.Run(() =>
        {
            var pixels = Render(
                $"<svg xmlns=\"{Xmlns}\" viewBox=\"0 0 100 100\" width=\"100\" height=\"100\">" +
                "<defs><radialGradient id=\"r\">" +
                "<stop offset=\"0\" stop-color=\"#ffffff\"/><stop offset=\"1\" stop-color=\"#000000\"/>" +
                "</radialGradient></defs>" +
                "<circle cx=\"50\" cy=\"50\" r=\"50\" fill=\"url(#r)\"/></svg>");

            // 圆占满整个控件 ⇒ 屏幕圆心在 (140,120)、半径 100。
            var (_, _, centerR, centerA) = PixelReader.At(pixels, ScreenW, 140, 120);
            var (_, _, edgeR, edgeA) = PixelReader.At(pixels, ScreenW, 230, 120); // 距圆心 90，靠近边缘

            Assert.Equal(255, centerA);
            Assert.Equal(255, edgeA);
            Assert.True(centerR > 230, $"径向渐变中心应接近白，实测 R={centerR}");
            Assert.True(edgeR < 60, $"径向渐变边缘应接近黑，实测 R={edgeR}");
        });
    }

    [Fact]
    public void RotatedShape_FallsBackToUniformSolidColor()
    {
        RenderTestHost.Run(() =>
        {
            // 含旋转 ⇒ 设备端发不出真渐变（lv_area_t 写不出倾斜矩形）⇒ **两边都必须**画 50% 降级纯色。
            // 这条是"像素级能抓、指令级抓不到"的典型：只要画布画了渐变，同一形状内的像素就不再均匀。
            var pixels = Render(LinearSvg(rotated: true));

            // 在旋转后仍然落在形状内部的一小组点上取样，断言**完全一致**。
            var probes = new[] { (90, 60), (100, 70), (110, 80) };
            var (b0, g0, r0, a0) = PixelReader.At(pixels, ScreenW, probes[0].Item1, probes[0].Item2);
            Assert.Equal(255, a0);

            foreach (var (x, y) in probes)
            {
                var (b, g, r, a) = PixelReader.At(pixels, ScreenW, x, y);
                Assert.Equal(255, a);
                Assert.True(Math.Abs(r - r0) <= 3 && Math.Abs(g - g0) <= 3 && Math.Abs(b - b0) <= 3,
                    $"降级后应整块同色：({probes[0].Item1},{probes[0].Item2}) 是 " +
                    $"({r0},{g0},{b0})，({x},{y}) 是 ({r},{g},{b})");
            }

            // 且确实是**降级色**（红蓝 50% 中点、绿分量 0），不是"随便一个纯色"
            Assert.True(Math.Abs(r0 - b0) <= 20, $"降级色应 R≈B，实测 R={r0} B={b0}");
            Assert.True(r0 is > 90 and < 200, $"降级色亮度应在中段，实测 R={r0}");
            Assert.True(g0 <= 8, $"降级色绿通道应为 0，实测 G={g0}");
        });
    }

    // ── helpers ──

    private static string LinearSvg(bool rotated)
    {
        var transform = rotated ? " transform=\"rotate(30 50 25)\"" : "";
        return $"<svg xmlns=\"{Xmlns}\" viewBox=\"0 0 100 100\" width=\"100\" height=\"100\">" +
               LinearDefs +
               $"<rect x=\"0\" y=\"0\" width=\"100\" height=\"50\" fill=\"url(#g)\"{transform}/></svg>";
    }

    /// <summary>
    /// 屏幕 400x300，SVG 控件建在 (40,20)、尺寸 200x200 —— 与指令级那组测试**同一套坐标**，
    /// 这样两条判据盯的是同一个几何，出问题时能直接对照。
    /// </summary>
    private static DesignCanvas BuildCanvas(string svgText)
    {
        var (doc, error) = SvgParser.Parse(svgText);
        Assert.Null(error);
        Assert.NotNull(doc);

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = ScreenW, Height = ScreenH };
        screen.Controls.Add(new HmiControl
        {
            Id = "icon", Name = "icon", Type = HmiControlType.Svg,
            X = ShapeLeft, Y = ShapeTop, Width = 200, Height = 200,
            SvgShapes = doc.Shapes,
            SvgViewBoxWidth = doc.ViewBoxWidth > 0 ? doc.ViewBoxWidth : 100,
            SvgViewBoxHeight = doc.ViewBoxHeight > 0 ? doc.ViewBoxHeight : 100,
        });
        return new DesignCanvas { Screen = screen, ZoomLevel = 1.0 };
    }

    /// <summary>真光栅化渲染并读回整张位图（BGRA）。</summary>
    private static byte[] Render(string svgText)
    {
        var canvas = BuildCanvas(svgText);
        using var bitmap = new RenderTargetBitmap(new PixelSize(ScreenW, ScreenH));
        using (var dc = bitmap.CreateDrawingContext())
            canvas.Render(dc);
        // 必须先 dispose 绘制上下文再读回：Skia 的绘制是延迟到 flush 时落地的。
        return PixelReader.ReadAll(bitmap);
    }
}
