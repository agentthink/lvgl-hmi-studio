using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// P1-3 B 段：渐变的 **CodeGen 发射**。
///
/// <para>本文件钉住四类契约，每一类都能单独失败：</para>
/// <list type="number">
///   <item>能发真渐变的形状（轴对齐矩形、圆）走 <c>lv_draw_rect</c> + <c>lv_grad_*</c>，
///         且**不再**发降级纯色；</item>
///   <item>端点坐标**相对绘制区域左上角**（LVGL 的 setup 用区域宽高换算百分比、get_line 收区域相对量）；</item>
///   <item>其余形状（多边形、Path、描边、含旋转）退回 <see cref="SvgGradientGeometry.SampleMidColor"/>
///         采出来的纯色，一个 <c>lv_grad_</c> 都不许出现；</item>
///   <item>仿射**烘进几何**：平移/缩放改坐标，含旋转则把矩形提升成多边形。</item>
/// </list>
///
/// <para>渐变元数据一律经 <c>url(#id)</c> 让 <c>SvgParser</c> 挂上去 —— 直接手搓
/// <see cref="SvgShape.FillGradient"/> 会绕开「Fill 落成降级纯色」这条，测出来的降级颜色就是假的。</para>
///
/// <para>断言一律走 <see cref="CSource"/>：含实参的文本用字符串包含来断言，会被缩进/换行漂移误伤，
/// 也可能被注释里的同名字符串误救（<c>TestAssertionStyleGuardTests</c> 专门守这条）。</para>
/// </summary>
public class SvgGradientCodeGenTests
{
    // viewBox 100x100 → 控件 200x200 ⇒ sx = sy = 2
    private static HmiControl MakeControl(params SvgShape[] shapes) => new()
    {
        Id = "svg1",
        Type = HmiControlType.Svg,
        Name = "Logo",
        X = 0, Y = 0, Width = 200, Height = 200,
        SvgViewBoxWidth = 100,
        SvgViewBoxHeight = 100,
        SvgShapes = shapes.ToList()
    };

    /// <summary>解析 SVG 并取出唯一形状；多形状/解析失败立刻炸（属于测试写法错误）。</summary>
    private static SvgShape ShapeOf(string svg)
    {
        var (doc, error) = SvgParser.Parse(svg);
        Assert.Null(error);
        return Assert.Single(doc!.Shapes);
    }

    private static string CodeOf(string svg) => SvgToLvglConverter.GenerateCanvasCode(MakeControl(ShapeOf(svg)));

    /// <summary>多形状版本的 <see cref="CodeOf"/>（<see cref="ShapeOf"/> 会断言只有一个形状）。</summary>
    private static string CodeOfAll(string svg)
    {
        var (doc, error) = SvgParser.Parse(svg);
        Assert.Null(error);
        Assert.NotEmpty(doc!.Shapes);
        return SvgToLvglConverter.GenerateCanvasCode(MakeControl(doc.Shapes.ToArray()));
    }

    /// <summary>红→蓝两点渐变，取 50% 得到 #800080。</summary>
    private const string RedBlueStops = """
        <stop offset="0" stop-color="#ff0000"/>
        <stop offset="1" stop-color="#0000ff"/>
        """;

    // ────────────────────── 1. 矩形：真渐变 ──────────────────────

    [Fact]
    public void LinearGradient_OnRect_EmitsGradientAndSolidFillIsReplaced()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs><linearGradient id="g1">{RedBlueStops}</linearGradient></defs>
              <rect x="0" y="0" width="50" height="50" fill="url(#g1)"/>
            </svg>
            """);

        // 默认 objectBoundingBox 轴向 0%→100%：包围盒 (0,0,50,50) 映射出 (0,0)→(50,0)，再 ×2
        CSource.AssertCode(code, "lv_grad_linear_init(&dsc.bg_grad, 0, 0, 100, 0, LV_GRAD_EXTEND_PAD)");
        CSource.AssertCode(code, "lv_color_t grad_colors[] = {lv_color_hex(0xFF0000), lv_color_hex(0x0000FF)};");
        CSource.AssertCode(code, "uint8_t grad_fracs[] = {0, 255};");
        CSource.AssertCode(code, "lv_grad_init_stops(&dsc.bg_grad, grad_colors, NULL, grad_fracs, 2)");
        CSource.AssertCode(code, "lv_draw_rect(&layer_svg1, &dsc, &a)");
        CSource.AssertCode(code, "dsc.radius = 0;");

        // 渐变顶上之后不许再发降级纯色（AppendRectFill 是唯一发 dsc.bg_color 的地方）
        Assert.DoesNotContain("dsc.bg_color", code);
    }

    /// <summary>
    /// 局部 <c>lv_grad_dsc_t</c> 一旦被整体赋值就绕过了 <c>lv_draw_rect_dsc_init</c> 的
    /// <c>lv_memzero</c>，而结构体尾部的 <c>void * state</c> 是渲染器的句柄字段
    /// （<c>*_cleanup</c> 会 <c>lv_free</c> 它）⇒ 必须直接写进已清零的 <c>dsc.bg_grad</c>。
    /// </summary>
    [Fact]
    public void LinearGradient_WritesIntoDscField_NoLocalDescriptor()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs><linearGradient id="g1">{RedBlueStops}</linearGradient></defs>
              <rect x="0" y="0" width="50" height="50" fill="url(#g1)"/>
            </svg>
            """);

        Assert.DoesNotContain("lv_grad_dsc_t", code);
        Assert.DoesNotContain("&grad", code);
        Assert.DoesNotContain("bg_grad = grad", code);

        // 每个 lv_grad_* 调用的第一个实参都应当是 dsc.bg_grad
        var gradientCalls = code.Split('\n')
            .Where(l => l.Contains("lv_grad_") && l.Contains('('))
            .ToList();
        Assert.NotEmpty(gradientCalls);
        foreach (var line in gradientCalls)
            Assert.Contains("&dsc.bg_grad", line);
    }

    /// <summary>
    /// 端点相对**绘制区域原点**，不是画布绝对坐标。
    /// <para>同一份渐变，形状整体平移 ⇒ 区域原点跟着走，发射出来的数字因此**不变**。
    /// 若哪天改成绝对坐标，这里会立刻变成 40/140。</para>
    /// </summary>
    [Fact]
    public void LinearGradient_EndpointsAreRelativeToAreaOrigin()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs><linearGradient id="g1">{RedBlueStops}</linearGradient></defs>
              <rect x="20" y="10" width="50" height="25" fill="url(#g1)"/>
            </svg>
            """);

        // 区域 = (20,10,50,25) × 2 = {40, 20, 140, 70}
        CSource.AssertCode(code, "lv_area_t a = {40, 20, 140, 70};");
        // 端点 = 包围盒映射后的 (20,10)→(70,10)，各减去区域原点 (40,20) 再 ×2
        CSource.AssertCode(code, "lv_grad_linear_init(&dsc.bg_grad, 0, 0, 100, 0, LV_GRAD_EXTEND_PAD)");
    }

    // ────────────────────── 2. 圆：radius 圆角矩形 ──────────────────────

    [Fact]
    public void LinearGradient_OnCircle_EmitsRoundRectWithRadius()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs><linearGradient id="g1">{RedBlueStops}</linearGradient></defs>
              <circle cx="50" cy="50" r="25" fill="url(#g1)"/>
            </svg>
            """);

        // 圆在 LVGL 里就是 radius = r 的矩形：包围盒 (25,25,50,50) → 区域 {50,50,150,150}，radius 25×2
        CSource.AssertCode(code, "lv_area_t a = {50, 50, 150, 150};");
        CSource.AssertCode(code, "dsc.radius = 50;");
        CSource.AssertCode(code, "lv_grad_linear_init(&dsc.bg_grad, 0, 0, 100, 0, LV_GRAD_EXTEND_PAD)");

        // 不走多边形三角扇那条降级路
        Assert.DoesNotContain("lv_draw_triangle", code);
        Assert.DoesNotContain("dsc.bg_color", code);
    }

    // ────────────────────── 3. 径向 ──────────────────────

    [Fact]
    public void RadialGradient_EmitsRadialInitAndFocal()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs>
                <radialGradient id="g1" cx="0.5" cy="0.5" r="0.5" fx="0.25" fy="0.25">
                  {RedBlueStops}
                </radialGradient>
              </defs>
              <rect x="0" y="0" width="50" height="50" fill="url(#g1)"/>
            </svg>
            """);

        // 包围盒 (0,0,50,50) → 圆心 (25,25)、半径 25、焦点 (12.5,12.5)；×2 后
        // to 点是「落在结束圆上的一点」，半径 = |to - center|
        CSource.AssertCode(code, "lv_grad_radial_init(&dsc.bg_grad, 50, 50, 100, 50, LV_GRAD_EXTEND_PAD)");
        CSource.AssertCode(code, "lv_grad_radial_set_focal(&dsc.bg_grad, 25, 25, 0)");
        Assert.DoesNotContain("lv_grad_linear_init", code);
    }

    [Fact]
    public void RadialGradient_WithoutFocal_OmitsSetFocal()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs><radialGradient id="g1" cx="0.5" cy="0.5" r="0.5">{RedBlueStops}</radialGradient></defs>
              <rect x="0" y="0" width="50" height="50" fill="url(#g1)"/>
            </svg>
            """);

        CSource.AssertCode(code, "lv_grad_radial_init(&dsc.bg_grad, 50, 50, 100, 50, LV_GRAD_EXTEND_PAD)");
        Assert.DoesNotContain("lv_grad_radial_set_focal", code);
    }

    /// <summary>
    /// r 解析成 0 时必须抬到 1：径向 setup 里有 <c>LV_ASSERT(r_end != 0)</c>，
    /// 且下面紧跟着 <c>(1 &lt;&lt; 24) / dr</c>。
    /// </summary>
    [Fact]
    public void RadialGradient_ZeroRadius_ClampedToOnePixel()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs><radialGradient id="g1" cx="0.5" cy="0.5" r="0">{RedBlueStops}</radialGradient></defs>
              <rect x="0" y="0" width="50" height="50" fill="url(#g1)"/>
            </svg>
            """);

        CSource.AssertCode(code, "lv_grad_radial_init(&dsc.bg_grad, 50, 50, 51, 50, LV_GRAD_EXTEND_PAD)");
    }

    // ────────────────────── 4. 降级：发不出真渐变 ──────────────────────

    [Fact]
    public void Gradient_OnPolygon_FallsBackToSampledSolidColor()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs><linearGradient id="g1">{RedBlueStops}</linearGradient></defs>
              <polygon points="0,0 40,0 20,40" fill="url(#g1)"/>
            </svg>
            """);

        // 三角扇只有 HOR/VER 两个分支，且色带按每个三角形自己的包围盒生成 ⇒ 语义本身就是错的
        Assert.Contains("lv_draw_triangle_dsc_t dsc;", code);
        CSource.AssertCode(code, "dsc.color = lv_color_hex(0x800080);");
        AssertNoGradientApis(code);
    }

    /// <summary>描边没有渐变字段（<c>lv_draw_line_dsc_t</c> 里就没有 <c>grad</c>）。</summary>
    [Fact]
    public void StrokeGradient_FallsBackToSampledSolidStroke()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs><linearGradient id="g1">{RedBlueStops}</linearGradient></defs>
              <rect x="0" y="0" width="50" height="25" fill="none"
                    stroke="url(#g1)" stroke-width="2"/>
            </svg>
            """);

        CSource.AssertCode(code, "dsc.color = lv_color_hex(0x800080);");
        CSource.AssertCode(code, "dsc.width = 2;");
        AssertNoGradientApis(code);
    }

    [Fact]
    public void Gradient_OnPath_FallsBackToSampledSolidColor()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs><linearGradient id="g1">{RedBlueStops}</linearGradient></defs>
              <path d="M0 0 L50 0 L25 50 Z" fill="url(#g1)"/>
            </svg>
            """);

        Assert.Contains("lv_draw_triangle_dsc_t dsc;", code);
        CSource.AssertCode(code, "dsc.color = lv_color_hex(0x800080);");
        AssertNoGradientApis(code);
    }

    /// <summary>
    /// 非均匀缩放把圆拉成椭圆 ⇒ 真径向渐变是椭圆形的，而 <c>lv_grad_radial_init</c> 只能表达圆。
    /// <c>Decide</c> 在**烘焙前**看到的是 <c>Circle</c>、于是放行，靠椭圆分支里的
    /// <c>IsCircle</c> 复检把它拦下来。
    /// </summary>
    [Fact]
    public void Gradient_OnNonUniformlyScaledCircle_FallsBackToSolid()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs><linearGradient id="g1">{RedBlueStops}</linearGradient></defs>
              <circle cx="50" cy="50" r="25" fill="url(#g1)" transform="scale(2,1)"/>
            </svg>
            """);

        CSource.AssertCode(code, "dsc.color = lv_color_hex(0x800080);");
        AssertNoGradientApis(code);
    }

    /// <summary>含旋转 ⇒ <c>lv_area_t</c> 这种轴对齐表达写不出倾斜矩形，整条路退回纯色。</summary>
    [Fact]
    public void Gradient_OnRotatedRect_FallsBackToSolidAndPromotesToPolygon()
    {
        var code = CodeOf($"""
            <svg viewBox="0 0 100 100">
              <defs><linearGradient id="g1">{RedBlueStops}</linearGradient></defs>
              <rect x="0" y="0" width="50" height="50" fill="url(#g1)" transform="rotate(30 25 25)"/>
            </svg>
            """);

        Assert.Contains("lv_draw_triangle_dsc_t dsc;", code);
        CSource.AssertCode(code, "dsc.color = lv_color_hex(0x800080);");
        AssertNoGradientApis(code);
        // 提升成多边形后不再有矩形描述符
        Assert.DoesNotContain("lv_draw_rect_dsc_t", code);
    }

    // ────────────────────── 5. 仿射烘焙 ──────────────────────

    [Fact]
    public void Translate_OnSolidRect_BakesIntoCoordinates()
    {
        var code = CodeOf("""
            <svg viewBox="0 0 100 100">
              <rect x="0" y="0" width="50" height="25" fill="#ff0000" transform="translate(10,20)"/>
            </svg>
            """);

        // (0,0,50,25) 平移 (10,20) ⇒ (10,20,50,25)，×2 ⇒ {20,40,120,90}
        CSource.AssertCode(code, "lv_area_t a = {20, 40, 120, 90};");
        CSource.AssertCode(code, "dsc.bg_color = lv_color_hex(0xFF0000);");
    }

    /// <summary>描边宽度按 √|det| 缩放：纯旋转不变粗，缩放 2 倍变 2 倍。</summary>
    [Fact]
    public void ScaledTransform_ScalesStrokeWidth()
    {
        var code = CodeOf("""
            <svg viewBox="0 0 100 100">
              <rect x="0" y="0" width="50" height="25" fill="none"
                    stroke="#00ff00" stroke-width="2" transform="scale(2)"/>
            </svg>
            """);

        CSource.AssertCode(code, "dsc.width = 4;");
    }

    /// <summary>纯旋转的 √|det| = 1 ⇒ 描边不许变粗。</summary>
    [Fact]
    public void RotationOnlyTransform_DoesNotScaleStrokeWidth()
    {
        var code = CodeOf("""
            <svg viewBox="0 0 100 100">
              <rect x="0" y="0" width="50" height="25" fill="none"
                    stroke="#00ff00" stroke-width="3" transform="rotate(45 25 12.5)"/>
            </svg>
            """);

        CSource.AssertCode(code, "dsc.width = 3;");
    }

    /// <summary>Path 的几何在字符串里、<c>BakeTransform</c> 展平不了 ⇒ 矩阵在展平后由 <c>AppendPath</c> 补上。</summary>
    [Fact]
    public void Translate_OnPath_AppliesMatrixToFlattenedPoints()
    {
        var code = CodeOf("""
            <svg viewBox="0 0 100 100">
              <path d="M0 0 L10 0" fill="none" stroke="#00ff00" stroke-width="1"
                    transform="translate(5,5)"/>
            </svg>
            """);

        // (0,0)→(5,5)→×2=(10,10)； (10,0)→(15,5)→×2=(30,10)
        CSource.AssertCode(code, "lv_point_precise_t pts[] = {{10, 10}, {30, 10}};");
    }

    // ────────────────────── 6. userSpaceOnUse × 元素 transform ──────────────────────

    /// <summary>
    /// SVG 规定 <c>gradientUnits="userSpaceOnUse"</c> 的坐标取自**引用它的那个元素的用户坐标系**，
    /// 而元素自己的 <c>transform</c> 正是这个坐标系的一部分。
    /// <para>漏掉它 ⇒ 形状移走了、渐变留在原地。判据取的是「相对区域原点」之后的数值，
    /// 所以这里断言的是 0/40 而不是 60/100。</para>
    /// </summary>
    [Fact]
    public void UserSpaceGradient_InTranslatedGroup_MovesWithShape()
    {
        var code = CodeOf("""
            <svg viewBox="0 0 100 100">
              <defs>
                <linearGradient id="g1" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="20" y2="0">
                  <stop offset="0" stop-color="#ff0000"/>
                  <stop offset="1" stop-color="#0000ff"/>
                </linearGradient>
              </defs>
              <g transform="translate(30,0)">
                <rect x="0" y="0" width="20" height="20" fill="url(#g1)"/>
              </g>
            </svg>
            """);

        // 形状被烘到 (30,0,20,20) ⇒ 区域 {60,0,100,40}；渐变端点随之平移到 30→50，再减去区域原点
        CSource.AssertCode(code, "lv_area_t a = {60, 0, 100, 40};");
        CSource.AssertCode(code, "lv_grad_linear_init(&dsc.bg_grad, 0, 0, 40, 0, LV_GRAD_EXTEND_PAD)");
    }

    /// <summary>对照组：同一份渐变、同一个相对位置，没有 transform 时数字必须完全相同。</summary>
    [Fact]
    public void UserSpaceGradient_NoTransform_SameRelativeEndpoints()
    {
        var code = CodeOf("""
            <svg viewBox="0 0 100 100">
              <defs>
                <linearGradient id="g1" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="20" y2="0">
                  <stop offset="0" stop-color="#ff0000"/>
                  <stop offset="1" stop-color="#0000ff"/>
                </linearGradient>
              </defs>
              <rect x="0" y="0" width="20" height="20" fill="url(#g1)"/>
            </svg>
            """);

        CSource.AssertCode(code, "lv_area_t a = {0, 0, 40, 40};");
        CSource.AssertCode(code, "lv_grad_linear_init(&dsc.bg_grad, 0, 0, 40, 0, LV_GRAD_EXTEND_PAD)");
    }

    // ────────────────────── 7. 白名单自洽 ──────────────────────

    /// <summary>
    /// 生成的 <c>lv_grad_*</c> 必须都在 <c>GeneratedCodeValidator.KnownLvglApis</c> 里，
    /// 否则校验器会给每个渐变控件刷一条"未知的 LVGL API 调用"警告 —— 而那条警告一旦变成噪音，
    /// 真正的拼写错误就没人看了。
    /// </summary>
    [Fact]
    public void EmittedGradientCalls_AreAllInLvglApiWhitelist()
    {
        var code = CodeOfAll($"""
            <svg viewBox="0 0 100 100">
              <defs>
                <linearGradient id="g1">{RedBlueStops}</linearGradient>
                <radialGradient id="g2" cx="0.5" cy="0.5" r="0.4" fx="0.3" fy="0.3">{RedBlueStops}</radialGradient>
              </defs>
              <rect x="0" y="0" width="50" height="25" fill="url(#g1)"/>
              <circle cx="75" cy="75" r="20" fill="url(#g2)"/>
            </svg>
            """);

        // 正控：确认这两条渐变路真的发出去了，否则下面的断言是空转
        Assert.Contains("lv_grad_linear_init", code);
        Assert.Contains("lv_grad_radial_init", code);
        Assert.Contains("lv_grad_radial_set_focal", code);

        var unknown = GeneratedCodeValidator.ExtractLvglCalls(code)
            .Where(a => !GeneratedCodeValidator.KnownLvglApiNames.Contains(a))
            .OrderBy(a => a)
            .ToList();

        Assert.Empty(unknown);
    }

    private static void AssertNoGradientApis(string code)
    {
        Assert.DoesNotContain("lv_grad_", code);
        Assert.DoesNotContain("bg_grad", code);
    }
}
