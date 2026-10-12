using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// P1-3 **C 段**：SVG 渐变与 <c>transform</c> 在画布上的行为测试。
///
/// <para>取代"读源码看有没有调 <c>SvgGradientGeometry</c>"式的断言：这里走真实的
/// <see cref="DesignCanvas.Render"/>，用 <see cref="CanvasRenderSpy"/> 录下实际绘制操作，
/// 断言**画刷类型 / 坐标 / 几何包围盒**。</para>
///
/// <para><b>为什么用操作录放而不是比像素</b>：本文件所在的宿主（<see cref="UiTestHost"/>）是
/// <c>UseHeadlessDrawing = true</c>，<c>DrawingContext</c> 是个 no-op、不产生像素。
/// 像素级判据在**另一个程序集** <c>HmiEditor.RenderTests</c>（Skia 后端 + 独立 testhost 进程 ——
/// 平台配置是进程级的、同进程换不了第二套（**2026-09-21 更正**：原写「渲染循环是进程级静态单例」，反编译 Avalonia 12.1.0 核实不成立，见专题 §8 的 #47））。
/// 两者互补：这边盯**绘制指令**（快、稳、可逐项断言画刷类型与坐标），
/// 那边盯**落地的像素**（能抓"指令写对了、但坐标系被理解错了"这类错）。</para>
///
/// <para><b>坐标空间是判据的一部分</b>：<c>PushTransform</c> 在录制/合成里表现为父级
/// <c>DrawingGroup.Transform</c>，子节点（含其画刷）坐标仍是**局部**的 —— 所以渐变端点必须
/// 与 <c>BuildShapeGeometry</c> 造出的几何点同处一个空间，**不能**再加控件原点
/// <c>rect.X/rect.Y</c>。测试 1 专门盯这条。</para>
/// </summary>
public class SvgGradientCanvasTests
{
    private const string Xmlns = "http://www.w3.org/2000/svg";

    /// <summary>红→蓝两点线性渐变（objectBoundingBox，默认 0%,0% → 100%,0% 水平）。</summary>
    private const string LinearDefs =
        "<defs><linearGradient id=\"g\">" +
        "<stop offset=\"0\" stop-color=\"#ff0000\"/><stop offset=\"1\" stop-color=\"#0000ff\"/>" +
        "</linearGradient></defs>";

    /// <summary>50% 处的降级色：红蓝中点在 24 位 RGB 下是 #800080。</summary>
    private static readonly Color MidColor = Color.FromRgb(0x80, 0x00, 0x80);

    [Fact]
    public void LinearGradient_OnAxisAlignedRect_DrawsGradientBrushInGeometrySpace()
    {
        UiTestHost.Run(() =>
        {
            var drawings = Record(
                $"""<svg xmlns="{Xmlns}" viewBox="0 0 100 100" width="100" height="100">""" +
                LinearDefs +
                """<rect x="0" y="0" width="100" height="50" fill="url(#g)"/></svg>""");

            var brush = Assert.Single(GradientBrushes(drawings));
            var linear = Assert.IsType<LinearGradientBrush>(brush);

            Assert.Equal(2, linear.GradientStops.Count);
            Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), linear.GradientStops[0].Color);
            Assert.Equal(Color.FromRgb(0x00, 0x00, 0xFF), linear.GradientStops[1].Color);

            // 控件建在 (40,20)、尺寸 200x200 ⇒ sx = sy = 2；物件占满宽度 ⇒ 端点 0 → 200
            Assert.Equal(RelativeUnit.Absolute, linear.StartPoint.Unit);
            Assert.Equal(0d, linear.StartPoint.Point.X, 3);
            Assert.Equal(0d, linear.StartPoint.Point.Y, 3);
            Assert.Equal(200d, linear.EndPoint.Point.X, 3);
            Assert.Equal(0d, linear.EndPoint.Point.Y, 3);
        });
    }

    [Fact]
    public void LinearGradient_OnRotatedRect_FallsBackToSolidMidColor()
    {
        UiTestHost.Run(() =>
        {
            // 含旋转 ⇒ 设备侧发不出真渐变（lv_area_t 写不出倾斜矩形）⇒ 两边都必须画 50% 降级纯色
            var drawings = Record(
                $"""<svg xmlns="{Xmlns}" viewBox="0 0 100 100" width="100" height="100">""" +
                LinearDefs +
                """<rect x="0" y="0" width="100" height="50" fill="url(#g)" transform="rotate(30)"/></svg>""");

            Assert.Empty(GradientBrushes(drawings));

            var solid = Assert.Single(SolidFillColors(drawings));
            Assert.Equal(MidColor, solid);
        });
    }

    [Fact]
    public void LinearGradient_OnGradientStrokedShape_StaysSolid()
    {
        UiTestHost.Run(() =>
        {
            // 描边渐变：lv_draw_line_dsc_t 根本没有 grad 字段 ⇒ 设备恒发纯色，画布也必须纯色
            var drawings = Record(
                $"""<svg xmlns="{Xmlns}" viewBox="0 0 100 100" width="100" height="100">""" +
                LinearDefs +
                """<rect x="0" y="0" width="100" height="50" fill="none" stroke="url(#g)" stroke-width="4"/></svg>""");

            Assert.Empty(GradientBrushes(drawings));

            // 按几何宽度锁定**形状**那条绘制：屏幕自身也画了一条纯描边边框
            // （指纹 `G:#ff3c465a/1/-`），不过滤就会把它一起断言进来。
            // 形状几何 = 100(viewBox) × sx(2) = 200。
            var penColors = drawings.OfType<GeometryDrawing>()
                .Where(d => d.Brush is null && d.Pen?.Brush is not null && d.Geometry is not null)
                .Where(d => Math.Abs(d.Geometry!.Bounds.Width - 200) < 0.5)
                .Select(d => ((ISolidColorBrush)d.Pen!.Brush!).Color)
                .ToList();

            Assert.Equal(MidColor, Assert.Single(penColors));
        });
    }

    [Fact]
    public void GroupTransform_IsBakedIntoGeometry()
    {
        UiTestHost.Run(() =>
        {
            var drawings = Record(
                $"""<svg xmlns="{Xmlns}" viewBox="0 0 100 100" width="100" height="100">""" +
                """<g transform="translate(50,10)"><rect x="0" y="0" width="20" height="20" fill="#00ff00"/></g></svg>""");

            var bounds = Assert.Single(FilledGeometryBounds(drawings));
            // 烘焙后矩形落在 (50,10)，乘 sx=sy=2 ⇒ (100,20)，尺寸 40x40
            Assert.Equal(100d, bounds.X, 3);
            Assert.Equal(20d, bounds.Y, 3);
            Assert.Equal(40d, bounds.Width, 3);
            Assert.Equal(40d, bounds.Height, 3);
        });
    }

    [Fact]
    public void GroupTransform_OnPath_IsAppliedAfterFlattening()
    {
        UiTestHost.Run(() =>
        {
            // Path 的几何在 PathData 字符串里，烘焙展平不了 ⇒ 必须在展平之后应用矩阵。
            // 漏掉这一步，<g transform> 里的 path 会留在原地，而设备端已经把它搬走了。
            var drawings = Record(
                $"""<svg xmlns="{Xmlns}" viewBox="0 0 100 100" width="100" height="100">""" +
                """<g transform="translate(20,0)"><path d="M0 0 L10 0 L10 10 Z" fill="#00ff00"/></g></svg>""");

            var bounds = Assert.Single(FilledGeometryBounds(drawings));
            Assert.Equal(40d, bounds.X, 3);
            Assert.Equal(0d, bounds.Y, 3);
            Assert.Equal(20d, bounds.Width, 3);
        });
    }

    [Fact]
    public void RadialGradient_OnCircle_UsesSingleRadius()
    {
        UiTestHost.Run(() =>
        {
            var drawings = Record(
                $"""<svg xmlns="{Xmlns}" viewBox="0 0 100 100" width="100" height="100">""" +
                "<defs><radialGradient id=\"r\">" +
                "<stop offset=\"0\" stop-color=\"#ffffff\"/><stop offset=\"1\" stop-color=\"#000000\"/>" +
                "</radialGradient></defs>" +
                """<circle cx="50" cy="50" r="25" fill="url(#r)"/></svg>""");

            var radial = Assert.IsType<RadialGradientBrush>(Assert.Single(GradientBrushes(drawings)));

            // r = 0.5 × 包围盒 50 = 25，再乘 sx=2 ⇒ 50
            Assert.Equal(50d, radial.RadiusX.Scalar, 3);
            // **单一半径**：lv_grad_radial_init 只能表达圆；若按 sy 另取一个，非正方形控件上两边就不一致
            Assert.Equal(radial.RadiusX.Scalar, radial.RadiusY.Scalar, 6);
            Assert.Equal(100d, radial.Center.Point.X, 3);
            Assert.Equal(100d, radial.Center.Point.Y, 3);
        });
    }

    // ── helpers ──

    /// <summary>
    /// 屏幕 400x300，SVG 控件建在 (40,20)、尺寸 200x200。
    /// **控件刻意不贴左上角**：渐变端点的坐标空间对不对，全看这里能不能暴露"多加了控件原点"。
    /// </summary>
    private static DesignCanvas BuildCanvas(string svgText)
    {
        var (doc, error) = SvgParser.Parse(svgText);
        // 解析必须**无错误**：渐变/transform 若退回"跳过"，这里会先炸，
        // 而不是等画布上少了个效果、或生成代码里少了一段才发现。
        Assert.Null(error);
        Assert.NotNull(doc);
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 400, Height = 300 };
        var control = new HmiControl
        {
            Id = "icon", Name = "icon", Type = HmiControlType.Svg,
            X = 40, Y = 20, Width = 200, Height = 200,
            SvgShapes = doc.Shapes,
            SvgViewBoxWidth = doc.ViewBoxWidth > 0 ? doc.ViewBoxWidth : 100,
            SvgViewBoxHeight = doc.ViewBoxHeight > 0 ? doc.ViewBoxHeight : 100,
        };
        screen.Controls.Add(control);
        return new DesignCanvas { Screen = screen, ZoomLevel = 1.0 };
    }

    private static List<Drawing> Record(string svgText) => CanvasRenderSpy.Record(BuildCanvas(svgText));

    private static IEnumerable<IBrush> GradientBrushes(IEnumerable<Drawing> drawings) =>
        drawings.OfType<GeometryDrawing>().Select(d => d.Brush).OfType<GradientBrush>();

    private static IEnumerable<Color> SolidFillColors(IEnumerable<Drawing> drawings) =>
        drawings.OfType<GeometryDrawing>().Select(d => d.Brush).OfType<ISolidColorBrush>()
            .Select(b => b.Color);

    private static IEnumerable<Avalonia.Rect> FilledGeometryBounds(IEnumerable<Drawing> drawings) =>
        drawings.OfType<GeometryDrawing>()
            .Where(d => d.Brush is not null && d.Geometry is not null)
            .Select(d => d.Geometry!.Bounds);
}
