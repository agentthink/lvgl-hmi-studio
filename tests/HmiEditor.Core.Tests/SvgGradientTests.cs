using System.Text.Json;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// P1-3：SVG 渐变（linear/radial、href 继承、gradientUnits、gradientTransform）与
/// transform（matrix/translate/scale/rotate/skew）的解析、几何、判据与降级。
///
/// <para>本文件的判据集中在两件事上：① 解析出来的几何**数值**对不对；
/// ② "能不能发真渐变"的判据（<see cref="SvgGradientGeometry.Decide"/>）在
/// 轴对齐/旋转、矩形/圆/多边形这几种组合上是否按预期分流。</para>
/// </summary>
public class SvgGradientTests
{
    // ────────────────────────── 解析：渐变 ──────────────────────────

    [Fact]
    public void Parse_LinearGradient_AttachesToShapeWithMidColorFallback()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <defs>
                <linearGradient id="g1">
                  <stop offset="0" stop-color="#ff0000"/>
                  <stop offset="1" stop-color="#0000ff"/>
                </linearGradient>
              </defs>
              <rect x="0" y="0" width="50" height="50" fill="url(#g1)"/>
            </svg>
            """;

        var (doc, error) = SvgParser.Parse(svg);

        Assert.Null(error);
        var shape = Assert.Single(doc!.Shapes);
        Assert.NotNull(shape.FillGradient);
        Assert.Equal(SvgGradientKind.Linear, shape.FillGradient!.Kind);
        Assert.Equal(2, shape.FillGradient.Stops.Count);
        // Fill 必须落成降级纯色（取渐变中点）：LVGL 上非矩形的渐变会退回这个颜色
        Assert.Equal("#800080", shape.Fill);
    }

    [Fact]
    public void Parse_LinkHrefGradient_InheritsStopsAndOverridesAttributes()
    {
        const string svg = """
            <svg xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 100 100">
              <defs>
                <linearGradient id="base">
                  <stop offset="0" stop-color="#000000"/>
                  <stop offset="1" stop-color="#ffffff"/>
                </linearGradient>
                <linearGradient id="derived" xlink:href="#base" x1="0" y1="0" x2="0" y2="1"/>
              </defs>
              <rect x="0" y="0" width="10" height="10" fill="url(#derived)"/>
            </svg>
            """;

        var (doc, error) = SvgParser.Parse(svg);

        Assert.Null(error);
        var g = Assert.Single(doc!.Shapes).FillGradient;
        Assert.NotNull(g);
        // 停靠点从被引用者继承
        Assert.Equal(2, g!.Stops.Count);
        Assert.Equal("#000000", g.Stops[0].Color);
        // 自己的 x2/y2 覆盖默认值 ⇒ 竖直方向（0,0 → 0,1）
        Assert.Equal(0, g.Coords[2]);
        Assert.Equal(1, g.Coords[3]);
    }

    [Fact]
    public void Parse_PlainHrefGradient_AlsoInherits()
    {
        // SVG 2 的 href（不带 xlink 前缀）同样必须支持
        const string svg = """
            <svg viewBox="0 0 100 100">
              <defs>
                <linearGradient id="base">
                  <stop offset="0" stop-color="#111111"/>
                  <stop offset="1" stop-color="#222222"/>
                </linearGradient>
                <linearGradient id="derived" href="#base"/>
              </defs>
              <rect x="0" y="0" width="10" height="10" fill="url(#derived)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        Assert.Equal(2, doc!.Shapes[0].FillGradient!.Stops.Count);
    }

    [Fact]
    public void Parse_GradientUnits_UserSpaceResolvesAgainstViewport()
    {
        const string svg = """
            <svg viewBox="0 0 200 100">
              <defs>
                <linearGradient id="g" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="100%" y2="0">
                  <stop offset="0" stop-color="#000000"/>
                  <stop offset="1" stop-color="#ffffff"/>
                </linearGradient>
              </defs>
              <rect x="0" y="0" width="50" height="50" fill="url(#g)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var g = doc!.Shapes[0].FillGradient!;
        Assert.True(g.UserSpace);
        // 100% 在 userSpaceOnUse 下按 viewBox 宽度（200）换算成绝对量
        Assert.Equal(200, g.Coords[2]);
    }

    [Fact]
    public void Parse_BoundingBoxGradient_KeepsRelativeCoordinates()
    {
        const string svg = """
            <svg viewBox="0 0 200 100">
              <defs>
                <linearGradient id="g" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0" stop-color="#000000"/>
                  <stop offset="1" stop-color="#ffffff"/>
                </linearGradient>
              </defs>
              <rect x="0" y="0" width="50" height="50" fill="url(#g)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var g = doc!.Shapes[0].FillGradient!;
        Assert.False(g.UserSpace);
        Assert.Equal(0, g.Coords[0]);
        Assert.Equal(1, g.Coords[2]);   // 100% ⇒ 1（相对值，不乘 viewBox）
        Assert.Equal(1, g.Coords[3]);
    }

    [Fact]
    public void Parse_RadialGradient_DefaultsToCenterHalfRadius()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <defs>
                <radialGradient id="r">
                  <stop offset="0" stop-color="#ffffff"/>
                  <stop offset="1" stop-color="#000000"/>
                </radialGradient>
              </defs>
              <circle cx="50" cy="50" r="40" fill="url(#r)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var g = doc!.Shapes[0].FillGradient!;
        Assert.Equal(SvgGradientKind.Radial, g.Kind);
        Assert.Equal(0.5, g.Coords[0], 6);
        Assert.Equal(0.5, g.Coords[1], 6);
        Assert.Equal(0.5, g.Coords[2], 6);
    }

    [Fact]
    public void Parse_RadialGradient_FocalPointIsCarriedWhenPresent()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <defs>
                <radialGradient id="r" cx="50%" cy="50%" r="50%" fx="20%" fy="30%">
                  <stop offset="0" stop-color="#ffffff"/>
                  <stop offset="1" stop-color="#000000"/>
                </radialGradient>
              </defs>
              <circle cx="50" cy="50" r="40" fill="url(#r)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var g = doc!.Shapes[0].FillGradient!;
        Assert.Equal(5, g.Coords.Length);
        Assert.Equal(0.2, g.Coords[3], 6);
        Assert.Equal(0.3, g.Coords[4], 6);
    }

    [Fact]
    public void Parse_UnknownGradientId_UsesSvgFallbackColor()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" fill="url(#missing) #ff0000"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var shape = Assert.Single(doc!.Shapes);
        Assert.Null(shape.FillGradient);
        Assert.Equal("#ff0000", shape.Fill);
    }

    [Fact]
    public void Parse_UnknownGradientIdWithoutFallback_MeansNoFill()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" fill="url(#missing)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        Assert.Null(Assert.Single(doc!.Shapes).Fill);
    }

    [Fact]
    public void Parse_GradientOnStroke_AttachesStrokeGradient()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <defs>
                <linearGradient id="s">
                  <stop offset="0" stop-color="#ff0000"/>
                  <stop offset="1" stop-color="#0000ff"/>
                </linearGradient>
              </defs>
              <rect x="0" y="0" width="10" height="10" fill="none" stroke="url(#s)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var shape = Assert.Single(doc!.Shapes);
        Assert.NotNull(shape.StrokeGradient);
        Assert.Equal("#800080", shape.Stroke);
        Assert.Equal("none", shape.Fill);
    }

    [Fact]
    public void Parse_LineWithGradientFill_KeepsFillNone()
    {
        // 线不是可填充形状：即使写了 fill="url(#g)" 也不能把它变成有面填充的图形
        const string svg = """
            <svg viewBox="0 0 100 100">
              <defs>
                <linearGradient id="g">
                  <stop offset="0" stop-color="#ff0000"/>
                  <stop offset="1" stop-color="#0000ff"/>
                </linearGradient>
              </defs>
              <line x1="0" y1="0" x2="10" y2="10" fill="url(#g)" stroke="url(#g)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var shape = Assert.Single(doc!.Shapes);
        Assert.Null(shape.FillGradient);
        Assert.Equal("none", shape.Fill);
        Assert.NotNull(shape.StrokeGradient);
    }

    [Fact]
    public void Parse_DefsAppearingAfterUse_StillResolves()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" fill="url(#late)"/>
              <defs>
                <linearGradient id="late">
                  <stop offset="0" stop-color="#ff0000"/>
                  <stop offset="1" stop-color="#0000ff"/>
                </linearGradient>
              </defs>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        Assert.NotNull(Assert.Single(doc!.Shapes).FillGradient);
    }

    [Fact]
    public void Parse_GradientHrefCycle_TerminatesWithoutGradient()
    {
        // 互引用的 SVG 在野外存在：必须不挂死，且因为链条上没有 stop ⇒ 视为无渐变
        const string svg = """
            <svg viewBox="0 0 100 100">
              <defs>
                <linearGradient id="a" href="#b"/>
                <linearGradient id="b" href="#a"/>
              </defs>
              <rect x="0" y="0" width="10" height="10" fill="url(#a) #123456"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var shape = Assert.Single(doc!.Shapes);
        Assert.Null(shape.FillGradient);
        Assert.Equal("#123456", shape.Fill);
    }

    [Fact]
    public void Parse_MultiStopGradient_KeepsAllStopsInModel()
    {
        // 模型不截断（截断到 LVGL 上限发生在发射期，由 NormalizeStops 负责）
        const string svg = """
            <svg viewBox="0 0 100 100">
              <defs>
                <linearGradient id="g">
                  <stop offset="0" stop-color="#000000"/>
                  <stop offset="0.5" stop-color="#808080"/>
                  <stop offset="1" stop-color="#ffffff"/>
                </linearGradient>
              </defs>
              <rect x="0" y="0" width="10" height="10" fill="url(#g)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        Assert.Equal(3, doc!.Shapes[0].FillGradient!.Stops.Count);
    }

    [Fact]
    public void Parse_GradientTransform_IsCaptured()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <defs>
                <linearGradient id="g" gradientTransform="rotate(45)">
                  <stop offset="0" stop-color="#000000"/>
                  <stop offset="1" stop-color="#ffffff"/>
                </linearGradient>
              </defs>
              <rect x="0" y="0" width="10" height="10" fill="url(#g)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var t = doc!.Shapes[0].FillGradient!.Transform;
        Assert.NotNull(t);
        Assert.False(SvgAffine.IsAxisAligned(t));
    }

    // ────────────────────────── 解析：transform ──────────────────────────

    [Fact]
    public void Parse_TranslateTransform_IsRecordedOnShape()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" transform="translate(5,7)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var m = Assert.Single(doc!.Shapes).Transform;
        Assert.NotNull(m);
        var (x, y) = SvgAffine.Apply(m!, 0, 0);
        Assert.Equal(5, x, 6);
        Assert.Equal(7, y, 6);
    }

    [Fact]
    public void Parse_TransformChain_ComposesOuterFirst()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" transform="translate(10,20) scale(2)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var m = doc!.Shapes[0].Transform!;
        // 先缩放后平移：p=(1,1) ⇒ (2,2) ⇒ (12,22)
        var (x, y) = SvgAffine.Apply(m, 1, 1);
        Assert.Equal(12, x, 6);
        Assert.Equal(22, y, 6);
    }

    [Fact]
    public void Parse_RotateAboutCenter_PivotsAroundThatPoint()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <rect x="10" y="20" width="10" height="10" transform="rotate(90 10 10)"/>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var m = doc!.Shapes[0].Transform!;
        // 支点不动
        var (px, py) = SvgAffine.Apply(m, 10, 10);
        Assert.Equal(10, px, 6);
        Assert.Equal(10, py, 6);
        // 支点右侧 10 的点 ⇒ 屏幕坐标（y 向下）顺时针旋转后到下方
        var (qx, qy) = SvgAffine.Apply(m, 20, 10);
        Assert.Equal(10, qx, 6);
        Assert.Equal(20, qy, 6);
    }

    [Fact]
    public void Parse_GroupTransform_IsInheritedAndComposedWithChild()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <g transform="translate(10,10)">
                <rect x="0" y="0" width="10" height="10" transform="scale(2)"/>
              </g>
            </svg>
            """;

        var (doc, _) = SvgParser.Parse(svg);
        var m = doc!.Shapes[0].Transform!;
        var (x, y) = SvgAffine.Apply(m, 1, 1);
        Assert.Equal(12, x, 6);
        Assert.Equal(12, y, 6);
    }

    [Fact]
    public void Parse_MalformedTransform_IsIgnoredWithoutThrowing()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" transform="translate(1"/>
              <rect x="0" y="0" width="10" height="10" transform="foo(1)"/>
              <rect x="0" y="0" width="10" height="10" transform="rotate(1,2)"/>
            </svg>
            """;

        // 畸形 transform 只让"这一个变换"失效，不能让整个导入失败
        var (doc, error) = SvgParser.Parse(svg);
        Assert.Null(error);
        Assert.Equal(3, doc!.Shapes.Count);
        Assert.All(doc.Shapes, s => Assert.Null(s.Transform));
    }

    // ────────────────────────── SvgAffine 判据 ──────────────────────────

    [Theory]
    [InlineData("translate(10,20) scale(2,3)", true)]
    [InlineData("matrix(1,0,0,1,5,5)", true)]
    [InlineData("rotate(0)", true)]
    [InlineData("rotate(1)", false)]
    [InlineData("rotate(90)", false)]
    [InlineData("skewX(0.5)", false)]
    [InlineData("skewY(10)", false)]
    public void Affine_IsAxisAligned_DistinguishesTranslateScaleFromRotateSkew(string text, bool expected)
    {
        var m = SvgAffine.Parse(text);
        Assert.Equal(expected, SvgAffine.IsAxisAligned(m));
    }

    [Fact]
    public void Affine_MatrixCommand_KeepsSvgOrder()
    {
        var m = SvgAffine.Parse("matrix(1,0,0,1,3,4)");
        Assert.NotNull(m);
        var (x, y) = SvgAffine.Apply(m!, 10, 10);
        Assert.Equal(13, x, 6);
        Assert.Equal(14, y, 6);
    }

    [Fact]
    public void Affine_MissingOrEmptyTransform_IsNull()
    {
        Assert.Null(SvgAffine.Parse(null));
        Assert.Null(SvgAffine.Parse("   "));
        Assert.True(SvgAffine.IsAxisAligned(null));
        Assert.True(SvgAffine.IsIdentity(null));
    }

    // ────────────────────────── 渐变几何与发射判据 ──────────────────────────

    private static SvgGradient TwoStopGradient(SvgGradientKind kind = SvgGradientKind.Linear) => new()
    {
        Kind = kind,
        Coords = kind == SvgGradientKind.Linear ? new double[] { 0, 0, 1, 0 } : new double[] { 0.5, 0.5, 0.5 },
        Stops =
        {
            new SvgGradientStop { Offset = 0, Color = "#000000" },
            new SvgGradientStop { Offset = 1, Color = "#ffffff" }
        }
    };

    [Theory]
    [InlineData(SvgShapeKind.Rect, true)]
    [InlineData(SvgShapeKind.Circle, true)]
    [InlineData(SvgShapeKind.Ellipse, true)]      // rx == ry ⇒ 等价于圆
    [InlineData(SvgShapeKind.Polygon, false)]
    [InlineData(SvgShapeKind.Path, false)]
    [InlineData(SvgShapeKind.Polyline, false)]
    public void Decide_ShapeKind_DrivesGradientCapability(SvgShapeKind kind, bool expectRealGradient)
    {
        var shape = new SvgShape
        {
            Kind = kind,
            // 圆/椭圆都写成「rx == ry」，多边形的点随便给
            Points = kind switch
            {
                SvgShapeKind.Rect => new double[] { 0, 0, 10, 10 },
                SvgShapeKind.Circle => new double[] { 5, 5, 5 },
                SvgShapeKind.Ellipse => new double[] { 5, 5, 5, 5 },
                _ => new double[] { 0, 0, 10, 0, 5, 10 }
            }
        };

        var actual = SvgGradientGeometry.Decide(shape, TwoStopGradient());

        Assert.Equal(
            expectRealGradient ? SvgGradientEmit.RectGradient : SvgGradientEmit.SolidFallback,
            actual);
    }

    [Fact]
    public void Decide_RotatedRect_FallsBackToSolid()
    {
        // 旋转过的矩形在 LVGL 侧写不出 lv_area_t ⇒ 必须降级
        var shape = new SvgShape
        {
            Kind = SvgShapeKind.Rect,
            Points = new double[] { 0, 0, 10, 10 },
            Transform = SvgAffine.Parse("rotate(30)")
        };

        Assert.Equal(SvgGradientEmit.SolidFallback, SvgGradientGeometry.Decide(shape, TwoStopGradient()));
    }

    [Fact]
    public void Decide_TranslatedRect_StillEmitsGradient()
    {
        // 纯平移/缩放不影响轴对齐 ⇒ 仍可发真渐变
        var shape = new SvgShape
        {
            Kind = SvgShapeKind.Rect,
            Points = new double[] { 0, 0, 10, 10 },
            Transform = SvgAffine.Parse("translate(5,5) scale(2)")
        };

        Assert.Equal(SvgGradientEmit.RectGradient, SvgGradientGeometry.Decide(shape, TwoStopGradient()));
    }

    [Fact]
    public void Decide_NonSquareEllipse_FallsBackToSolid()
    {
        var shape = new SvgShape { Kind = SvgShapeKind.Ellipse, Points = new double[] { 5, 5, 8, 3 } };
        Assert.Equal(SvgGradientEmit.SolidFallback, SvgGradientGeometry.Decide(shape, TwoStopGradient()));
    }

    [Fact]
    public void Decide_NoStops_FallsBackToSolid()
    {
        var shape = new SvgShape { Kind = SvgShapeKind.Rect, Points = new double[] { 0, 0, 10, 10 } };
        var gradient = TwoStopGradient();
        gradient.Stops.Clear();

        Assert.Equal(SvgGradientEmit.SolidFallback, SvgGradientGeometry.Decide(shape, gradient));
    }

    [Fact]
    public void BoundingBox_IsComputedForEachShapeKind()
    {
        Assert.Equal(new SvgBox(10, 20, 30, 40),
            SvgGradientGeometry.BoundingBox(new SvgShape { Kind = SvgShapeKind.Rect, Points = new double[] { 10, 20, 30, 40 } })!.Value);

        Assert.Equal(new SvgBox(0, 0, 20, 20),
            SvgGradientGeometry.BoundingBox(new SvgShape { Kind = SvgShapeKind.Circle, Points = new double[] { 10, 10, 10 } })!.Value);

        Assert.Equal(new SvgBox(1, 2, 8, 6),
            SvgGradientGeometry.BoundingBox(new SvgShape { Kind = SvgShapeKind.Polygon, Points = new double[] { 1, 2, 9, 2, 5, 8 } })!.Value);

        // 路径按展平后的点集取包围盒
        var pathBox = SvgGradientGeometry.BoundingBox(new SvgShape
        {
            Kind = SvgShapeKind.Path,
            PathData = "M0 0 L10 0 L10 20 Z"
        });
        Assert.NotNull(pathBox);
        Assert.Equal(10, pathBox!.Value.Width, 6);
        Assert.Equal(20, pathBox.Value.Height, 6);
    }

    [Fact]
    public void BoundingBox_EmptyGeometry_IsNull()
    {
        Assert.Null(SvgGradientGeometry.BoundingBox(new SvgShape { Kind = SvgShapeKind.Path, PathData = null }));
        Assert.Null(SvgGradientGeometry.BoundingBox(new SvgShape { Kind = SvgShapeKind.Polyline, Points = Array.Empty<double>() }));
        Assert.Null(SvgGradientGeometry.BoundingBox(new SvgShape { Kind = SvgShapeKind.Rect, Points = new double[] { 1, 2 } }));
    }

    [Fact]
    public void Resolve_ObjectBoundingBox_Linear_MapsOntoShapeBox()
    {
        var g = TwoStopGradient();
        var r = SvgGradientGeometry.Resolve(g, new SvgBox(10, 20, 100, 50));

        // 0,0 → 1,0 在包围盒 (10,20,100,50) 上 ⇒ (10,20) → (110,20)
        Assert.Equal(10, r.X1, 6);
        Assert.Equal(20, r.Y1, 6);
        Assert.Equal(110, r.X2, 6);
        Assert.Equal(20, r.Y2, 6);
    }

    [Fact]
    public void Resolve_UserSpace_Linear_UsesCoordinatesAsIs()
    {
        var g = TwoStopGradient();
        g.UserSpace = true;
        g.Coords = new double[] { 5, 6, 7, 8 };

        var r = SvgGradientGeometry.Resolve(g, new SvgBox(10, 20, 100, 50));

        Assert.Equal(5, r.X1, 6);
        Assert.Equal(6, r.Y1, 6);
        Assert.Equal(7, r.X2, 6);
        Assert.Equal(8, r.Y2, 6);
    }

    [Fact]
    public void Resolve_GradientTransform_IsAppliedAfterBoundingBox()
    {
        var g = TwoStopGradient();
        g.Transform = SvgAffine.Translate(0.5, 0);   // 在 0..1 相对空间里平移半格

        var r = SvgGradientGeometry.Resolve(g, new SvgBox(0, 0, 100, 50));

        // 0 ⇒ 0.5 相对 ⇒ 50px；1 ⇒ 1.5 相对 ⇒ 150px
        Assert.Equal(50, r.X1, 6);
        Assert.Equal(150, r.X2, 6);
    }

    [Fact]
    public void Resolve_Radial_OnSquareBox_RadiusScalesUniformly()
    {
        var g = TwoStopGradient(SvgGradientKind.Radial);
        var r = SvgGradientGeometry.Resolve(g, new SvgBox(0, 100, 40, 40));

        Assert.Equal(20, r.X1, 6);          // 圆心 50% ⇒ x=20
        Assert.Equal(120, r.Y1, 6);         // y=100+20
        Assert.Equal(20, r.Radius, 6);      // r=50% ⇒ 0.5*40
        Assert.False(r.HasFocal);
    }

    [Fact]
    public void Resolve_Radial_WithFocal_ReportsFocal()
    {
        var g = TwoStopGradient(SvgGradientKind.Radial);
        g.Coords = new double[] { 0.5, 0.5, 0.5, 0.25, 0.25 };

        var r = SvgGradientGeometry.Resolve(g, new SvgBox(0, 0, 100, 100));

        Assert.True(r.HasFocal);
        Assert.Equal(25, r.FocalX, 6);
        Assert.Equal(25, r.FocalY, 6);
    }

    // ────────────────────────── 降级色与停靠点归一 ──────────────────────────

    [Fact]
    public void SampleMidColor_InterpolatesAcrossStops()
    {
        var g = TwoStopGradient();
        Assert.Equal("#808080", SvgGradientGeometry.SampleMidColor(g));
    }

    [Fact]
    public void SampleMidColor_UsesSegmentContainingMidpoint()
    {
        // 中间那个停靠点恰好落在 0.5 ⇒ 取的是它，而不是最后一个停靠点
        var g = new SvgGradient
        {
            Stops =
            {
                new SvgGradientStop { Offset = 0, Color = "#000000" },
                new SvgGradientStop { Offset = 0.5, Color = "#123456" },
                new SvgGradientStop { Offset = 1, Color = "#ffffff" }
            }
        };

        Assert.Equal("#123456", SvgGradientGeometry.SampleMidColor(g));
    }

    [Fact]
    public void SampleMidColor_InterpolatesInsideSegmentThatSpansMidpoint()
    {
        // 中点落在 [0.4, 1] 段内 ⇒ 必须在该段内插值，而不是取端点
        var g = new SvgGradient
        {
            Stops =
            {
                new SvgGradientStop { Offset = 0.4, Color = "#000000" },
                new SvgGradientStop { Offset = 1, Color = "#ffffff" }
            }
        };

        // t = (0.5-0.4)/0.6 = 1/6 ⇒ 255/6 ≈ 42.5 ⇒ 42
        Assert.Equal("#2a2a2a", SvgGradientGeometry.SampleMidColor(g));
    }

    [Fact]
    public void SampleMidColor_IgnoresNoneStopsAndFallsBackToLast()
    {
        var g = new SvgGradient
        {
            Stops = { new SvgGradientStop { Offset = 0, Color = "none" } }
        };

        Assert.Equal("none", SvgGradientGeometry.SampleMidColor(g));
    }

    [Fact]
    public void NormalizeStops_SortsClampsAndKeepsEndpoints()
    {
        var g = new SvgGradient
        {
            Stops =
            {
                new SvgGradientStop { Offset = 1, Color = "#cccccc" },
                new SvgGradientStop { Offset = 0, Color = "#111111" },
                new SvgGradientStop { Offset = 0.5, Color = "#888888" }
            }
        };

        var stops = SvgGradientGeometry.NormalizeStops(g);

        // LVGL 的 LV_GRADIENT_MAX_STOPS 默认是 2 ⇒ 取首尾（保留 offset 表达真实位置）
        Assert.Equal(2, stops.Count);
        Assert.Equal("#111111", stops[0].Color);
        Assert.Equal("#cccccc", stops[1].Color);
    }

    [Fact]
    public void NormalizeStops_TwoStopsAreKeptAsIs()
    {
        var g = TwoStopGradient();
        Assert.Equal(2, SvgGradientGeometry.NormalizeStops(g).Count);
    }

    // ────────────────────────── 深拷贝独立性 ──────────────────────────

    [Fact]
    public void ShapeShallowCopy_DoesNotShareGradientOrTransform()
    {
        var shape = new SvgShape
        {
            Kind = SvgShapeKind.Rect,
            Points = new double[] { 0, 0, 10, 10 },
            FillGradient = TwoStopGradient(),
            Transform = SvgAffine.Translate(1, 1)
        };
        shape.FillGradient!.Transform = SvgAffine.Scale(2, 2);

        var copy = shape.ShallowCopy();

        // 改副本的每一处引用型内容，源都必须纹丝不动
        copy.FillGradient!.Stops[0].Color = "#ff0000";
        copy.FillGradient.Coords[0] = 99;
        copy.FillGradient.Transform![0] = 99;
        copy.Transform![4] = 99;
        copy.Points[0] = 99;

        Assert.Equal("#000000", shape.FillGradient!.Stops[0].Color);
        Assert.Equal(0, shape.FillGradient.Coords[0]);
        Assert.Equal(2, shape.FillGradient.Transform![0]);
        Assert.Equal(1, shape.Transform![4]);
        Assert.Equal(0, shape.Points[0]);
    }

    [Fact]
    public void ShapeShallowCopy_NullReferencesStayNull()
    {
        var copy = new SvgShape { Kind = SvgShapeKind.Rect }.ShallowCopy();
        Assert.Null(copy.Transform);
        Assert.Null(copy.FillGradient);
        Assert.Null(copy.StrokeGradient);
    }

    // ────────────────────────── JSON 往返 ──────────────────────────

    [Fact]
    public void Serialization_RoundTrip_PreservesGradientAndTransform()
    {
        var shape = new SvgShape
        {
            Kind = SvgShapeKind.Rect,
            Points = new double[] { 0, 0, 10, 10 },
            Fill = "#808080",
            FillGradient = TwoStopGradient(),
            Transform = SvgAffine.Parse("translate(3,4)")
        };
        shape.FillGradient!.UserSpace = true;
        shape.FillGradient.Transform = SvgAffine.Scale(2, 2);

        var json = JsonSerializer.Serialize(shape);
        var back = JsonSerializer.Deserialize<SvgShape>(json);

        Assert.NotNull(back);
        Assert.NotNull(back!.FillGradient);
        Assert.Equal(SvgGradientKind.Linear, back.FillGradient!.Kind);
        Assert.True(back.FillGradient.UserSpace);
        Assert.Equal(2, back.FillGradient.Stops.Count);
        Assert.Equal("#ffffff", back.FillGradient.Stops[1].Color);
        Assert.Equal(2, back.FillGradient.Transform![0]);
        Assert.NotNull(back.Transform);
        Assert.Equal(3, back.Transform![4], 6);
    }
}
