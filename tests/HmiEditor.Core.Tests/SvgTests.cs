using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// I6: SVG 解析 / path 展平 / CodeGen 转换单元测试。
/// </summary>
public class SvgTests
{
    // ── SvgParser ──

    [Fact]
    public void Parse_BasicShapes_ProducesShapes()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 100">
              <rect x="10" y="10" width="80" height="40" fill="#ff0000"/>
              <circle cx="150" cy="30" r="20" fill="blue" stroke="black" stroke-width="2"/>
              <ellipse cx="100" cy="80" rx="40" ry="10" fill="none" stroke="green"/>
              <line x1="0" y1="90" x2="200" y2="90" stroke="#0000ff"/>
            </svg>
            """;

        var (doc, error) = SvgParser.Parse(svg);

        Assert.Null(error);
        Assert.NotNull(doc);
        Assert.Equal(4, doc!.Shapes.Count);
        Assert.Equal(200, doc.ViewBoxWidth);
        Assert.Equal(100, doc.ViewBoxHeight);

        var rect = doc.Shapes[0];
        Assert.Equal(SvgShapeKind.Rect, rect.Kind);
        Assert.Equal("#ff0000", rect.Fill);
        Assert.Equal(10, rect.Points[0]);

        var circle = doc.Shapes[1];
        Assert.Equal(SvgShapeKind.Circle, circle.Kind);
        Assert.Equal("#0000ff", circle.Fill);
        Assert.Equal("#000000", circle.Stroke);
        Assert.Equal(2, circle.StrokeWidth);

        Assert.Equal(SvgShapeKind.Ellipse, doc.Shapes[2].Kind);
        Assert.Equal(SvgShapeKind.Line, doc.Shapes[3].Kind);
    }

    [Fact]
    public void Parse_PathAndPolygon_ProducesShapes()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <path d="M10 10 L90 10 L50 80 Z" fill="#00ff00"/>
              <polygon points="0,0 20,0 10,20" fill="none" stroke="red"/>
              <polyline points="5,5 15,5 10,15"/>
              <g fill="#123456">
                <rect x="0" y="0" width="5" height="5"/>
              </g>
            </svg>
            """;

        var (doc, error) = SvgParser.Parse(svg);

        Assert.Null(error);
        Assert.NotNull(doc);
        Assert.Equal(4, doc!.Shapes.Count);

        var path = doc.Shapes[0];
        Assert.Equal(SvgShapeKind.Path, path.Kind);
        Assert.Equal("#00ff00", path.Fill);
        Assert.Contains("M10 10", path.PathData);

        var polygon = doc.Shapes[1];
        Assert.Equal(SvgShapeKind.Polygon, polygon.Kind);
        Assert.Equal("none", polygon.Fill);
        Assert.Equal("#ff0000", polygon.Stroke);

        var polyline = doc.Shapes[2];
        Assert.Equal(SvgShapeKind.Polyline, polyline.Kind);
        Assert.Equal("none", polyline.Fill);

        // g 分组样式继承
        var grouped = doc.Shapes[3];
        Assert.Equal("#123456", grouped.Fill);
    }

    [Fact]
    public void Parse_StyleAttribute_AppliesStyles()
    {
        const string svg = """
            <svg viewBox="0 0 100 100">
              <rect x="0" y="0" width="10" height="10" style="fill:#abcdef; stroke:#010203; stroke-width:3"/>
            </svg>
            """;

        var (doc, error) = SvgParser.Parse(svg);

        Assert.Null(error);
        Assert.NotNull(doc);
        var rect = doc!.Shapes[0];
        Assert.Equal("#abcdef", rect.Fill);
        Assert.Equal("#010203", rect.Stroke);
        Assert.Equal(3, rect.StrokeWidth);
    }

    [Fact]
    public void Parse_ShortHexAndRgb_AreNormalized()
    {
        Assert.Equal("#aabbcc", SvgParser.NormalizeColor("#abc"));
        Assert.Equal("#ff0000", SvgParser.NormalizeColor("rgb(255, 0, 0)"));
        Assert.Equal("none", SvgParser.NormalizeColor("none"));
    }
    [Fact]
    public void Parse_InvalidXml_ReturnsError()
    {
        var (doc, error) = SvgParser.Parse("<svg><rect></svg>");
        Assert.Null(doc);
        Assert.NotNull(error);
    }

    [Fact]
    public void Parse_NonSvgRoot_ReturnsError()
    {
        var (doc, error) = SvgParser.Parse("<html></html>");
        Assert.Null(doc);
        Assert.NotNull(error);
    }

    // ── SvgPathFlattener ──

    [Fact]
    public void Flatten_LineAndClose_ProducesClosedSubPath()
    {
        var subPaths = SvgPathFlattener.Flatten("M10 10 L90 10 L50 80 Z");

        Assert.Single(subPaths);
        var sub = subPaths[0];
        Assert.True(sub.Closed);
        // 起点 + L + L + Z 闭合回起点
        Assert.Equal(8, sub.Points.Count);
        Assert.Equal(10, sub.Points[0]);
        Assert.Equal(10, sub.Points[1]);
        Assert.Equal(90, sub.Points[2]);
        Assert.Equal(10, sub.Points[3]);
        Assert.Equal(50, sub.Points[4]);
        Assert.Equal(80, sub.Points[5]);
        // 闭合回到起点
        Assert.Equal(10, sub.Points[6]);
        Assert.Equal(10, sub.Points[7]);
    }

    [Fact]
    public void Flatten_CubicBezier_ProducesSegmentedCurve()
    {
        var subPaths = SvgPathFlattener.Flatten("M0 0 C0 100 100 100 100 0");

        Assert.Single(subPaths);
        var pts = subPaths[0].Points;
        // 1 起点 + 12 细分段
        Assert.Equal(13 * 2, pts.Count);
        // 终点正确
        Assert.Equal(100, pts[^2]);
        Assert.Equal(0, pts[^1]);
        // 中间点应偏离直线（曲线验证）
        double midY = pts[12]; // t=0.5 处的 y
        Assert.True(midY > 20, $"曲线中点 y 应显著偏离直线, got {midY}");
    }

    [Fact]
    public void Flatten_RelativeCommands_ResolveCorrectly()
    {
        var subPaths = SvgPathFlattener.Flatten("M10 10 l20 0 l0 20");

        Assert.Single(subPaths);
        var pts = subPaths[0].Points;
        Assert.Equal(30, pts[2]); // 10 + 20
        Assert.Equal(10, pts[3]);
        Assert.Equal(30, pts[4]);
        Assert.Equal(30, pts[5]); // 10 + 20
    }

    [Fact]
    public void Flatten_Arc_ProducesSegmentsEndingAtTarget()
    {
        var subPaths = SvgPathFlattener.Flatten("M0 0 A50 50 0 0 1 100 0");

        Assert.Single(subPaths);
        var pts = subPaths[0].Points;
        Assert.True(pts.Count >= 8 * 2, "圆弧应生成至少 8 段");
        // 终点允许浮点误差
        Assert.True(Math.Abs(pts[^2] - 100) < 0.01, $"终点 x={pts[^2]}");
        Assert.True(Math.Abs(pts[^1]) < 0.01, $"终点 y={pts[^1]}");
    }

    [Fact]
    public void Flatten_Empty_ReturnsEmpty()
    {
        Assert.Empty(SvgPathFlattener.Flatten(null));
        Assert.Empty(SvgPathFlattener.Flatten(""));
    }

    // ── SvgToLvglConverter ──

    private static HmiControl MakeSvgControl(params SvgShape[] shapes)
    {
        return new HmiControl
        {
            Id = "svg1",
            Type = HmiControlType.Svg,
            Name = "Logo",
            X = 10, Y = 20, Width = 200, Height = 100,
            SvgViewBoxWidth = 100,
            SvgViewBoxHeight = 50,
            SvgShapes = shapes.ToList()
        };
    }

    [Fact]
    public void GenerateBufferDeclaration_EmitsStaticArray()
    {
        var code = SvgToLvglConverter.GenerateBufferDeclaration(MakeSvgControl());
        // 必须按字节分配（lv_color_t 在 LVGL 9.x 是 3 字节，ARGB8888 需 4 B/px）
        CSource.AssertCode(code, "static uint8_t buf_svg1[LV_CANVAS_BUF_SIZE(200, 100, 32, LV_DRAW_BUF_STRIDE_ALIGN)];");
        Assert.DoesNotContain("lv_color_t", code);
    }

    [Fact]
    public void GenerateCanvasCode_RectFill_EmitsDrawRect()
    {
        var control = MakeSvgControl(new SvgShape
        {
            Kind = SvgShapeKind.Rect,
            Points = new[] { 0.0, 0, 50, 25 },
            Fill = "#ff0000"
        });

        var code = SvgToLvglConverter.GenerateCanvasCode(control);

        CSource.AssertCode(code, "lv_canvas_create(screen)");
        CSource.AssertCode(code, "lv_canvas_set_buffer(obj_svg1, buf_svg1, 200, 100, LV_COLOR_FORMAT_ARGB8888)");
        Assert.Contains("lv_draw_rect_dsc_t dsc;", code);
        CSource.AssertCode(code, "dsc.bg_color = lv_color_hex(0xFF0000);");
        CSource.AssertCode(code, "lv_canvas_finish_layer(obj_svg1, &layer_svg1);");
    }

    [Fact]
    public void GenerateCanvasCode_Line_EmitsDrawLine()
    {
        var control = MakeSvgControl(new SvgShape
        {
            Kind = SvgShapeKind.Line,
            Points = new[] { 0.0, 0, 100, 50 },
            Fill = "none",
            Stroke = "#00ff00",
            StrokeWidth = 2
        });

        var code = SvgToLvglConverter.GenerateCanvasCode(control);

        Assert.Contains("lv_draw_line_dsc_t dsc;", code);
        CSource.AssertCode(code, "dsc.color = lv_color_hex(0x00FF00);");
        Assert.Contains("dsc.width = 2;", code);
        // LVGL 9.x：lv_draw_line(layer, dsc)，端点写在 dsc.p1/p2
        // viewBox 100x50 → 控件 200x100，缩放 2x：{0,0}->{0,0}，{100,50}->{200,100}
        Assert.Contains("dsc.p1.x = 0; dsc.p1.y = 0;", code);
        Assert.Contains("dsc.p2.x = 200; dsc.p2.y = 100;", code);
        CSource.AssertCode(code, "lv_draw_line(&layer_svg1, &dsc);");
        CSource.AssertNoCode(code, "lv_draw_line(&layer_svg1, &dsc,");
    }

    [Fact]
    public void GenerateCanvasCode_PathFill_EmitsTriangleFan()
    {
        var control = MakeSvgControl(new SvgShape
        {
            Kind = SvgShapeKind.Path,
            PathData = "M0 0 L50 0 L25 50 Z",
            Fill = "#123456"
        });

        var code = SvgToLvglConverter.GenerateCanvasCode(control);

        // LVGL 9.x 无 lv_draw_polygon：改用 lv_draw_triangle 三角扇近似
        Assert.Contains("lv_draw_triangle_dsc_t dsc;", code);
        CSource.AssertCode(code, "dsc.color = lv_color_hex(0x123456);");
        CSource.AssertCode(code, "lv_draw_triangle(&layer_svg1, &dsc);");
        // M0 0 → L50 0 → L25 50 → Z(闭合回起点)= 4 点 → 2 个三角形
        CSource.AssertCode(code, "for (int i = 1; i + 1 < 4; i++)");
        Assert.DoesNotContain("lv_draw_polygon", code);
    }

    [Fact]
    public void GenerateScreenCode_SvgControl_EmitsCanvasPath()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(MakeSvgControl(new SvgShape
        {
            Kind = SvgShapeKind.Circle,
            Points = new[] { 50.0, 25, 10 },
            Fill = "#38bdf8"
        }));

        var code = generator.GenerateScreenCode(screen);

        CSource.AssertCode(code, "static uint8_t buf_svg1[LV_CANVAS_BUF_SIZE(200, 100, 32, LV_DRAW_BUF_STRIDE_ALIGN)];");
        CSource.AssertCode(code, "obj_svg1 = lv_canvas_create(screen);");
        // 圆填充采样 32 段 → 三角扇 32 个三角形
        CSource.AssertCode(code, "for (int i = 1; i + 1 < 32; i++)");
        CSource.AssertCode(code, "lv_draw_triangle(&layer_svg1, &dsc);");
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesShapes()
    {
        var control = MakeSvgControl(new SvgShape
        {
            Kind = SvgShapeKind.Rect,
            Points = new[] { 1.0, 2, 30, 40 },
            Fill = "#ff0000",
            Stroke = "#000000",
            StrokeWidth = 1.5
        });

        var json = System.Text.Json.JsonSerializer.Serialize(control);
        var back = System.Text.Json.JsonSerializer.Deserialize<HmiControl>(json);

        Assert.NotNull(back);
        Assert.Equal(HmiControlType.Svg, back!.Type);
        Assert.NotNull(back.SvgShapes);
        Assert.Single(back.SvgShapes);
        Assert.Equal(SvgShapeKind.Rect, back.SvgShapes[0].Kind);
        Assert.Equal("#ff0000", back.SvgShapes[0].Fill);
        Assert.Equal(1.5, back.SvgShapes[0].StrokeWidth);
        Assert.Equal(100, back.SvgViewBoxWidth);
    }
}
