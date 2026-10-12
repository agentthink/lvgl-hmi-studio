using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// I9: 多分辨率/自适应布局测试 —— 计算器纯函数、CodeGen 注入、序列化往返。
/// </summary>
public class AdaptiveLayoutTests
{
    // ── 计算器 ──

    [Fact]
    public void Compute_NoneMode_IdentityTransform()
    {
        var t = AdaptiveLayoutCalculator.Compute(800, 480, 1024, 600, ScreenAdaptiveMode.None);
        Assert.Equal(1, t.ScaleX);
        Assert.Equal(1, t.ScaleY);
        Assert.Equal(0, t.OffsetX);
        Assert.Equal(0, t.OffsetY);
    }

    [Fact]
    public void Compute_Fill_SameAspect_NoOffset()
    {
        // 800x480 → 1024x600 同比例 5:3
        var t = AdaptiveLayoutCalculator.Compute(800, 480, 1024, 600, ScreenAdaptiveMode.Fill);
        Assert.Equal(1024.0 / 800, t.ScaleX, 6);
        Assert.Equal(600.0 / 480, t.ScaleY, 6);
        Assert.Equal(0, t.OffsetX);
        Assert.Equal(0, t.OffsetY);
    }

    [Fact]
    public void Compute_Fit_WideToPortrait_LetterboxCentered()
    {
        // 800x480 → 480x800 (竖屏): s = min(0.6, 1.667) = 0.6
        var t = AdaptiveLayoutCalculator.Compute(800, 480, 480, 800, ScreenAdaptiveMode.Fit);
        Assert.Equal(0.6, t.ScaleX, 6);
        Assert.Equal(0.6, t.ScaleY, 6);
        Assert.Equal((480 - 800 * 0.6) / 2, t.OffsetX, 6);   // 96
        Assert.Equal((800 - 480 * 0.6) / 2, t.OffsetY, 6);   // 256
    }

    [Fact]
    public void Compute_Map_ControlRectTransformed()
    {
        // 800x480 → 1024x600 Fill: 控件(10,20,100,40)
        var t = AdaptiveLayoutCalculator.Compute(800, 480, 1024, 600, ScreenAdaptiveMode.Fill);
        var (x, y, w, h) = t.Map(10, 20, 100, 40);
        Assert.Equal(10 * 1024.0 / 800, x, 6);
        Assert.Equal(20 * 600.0 / 480, y, 6);
        Assert.Equal(100 * 1024.0 / 800, w, 6);
        Assert.Equal(40 * 600.0 / 480, h, 6);
    }

    [Fact]
    public void Compute_InvalidSizes_Identity()
    {
        var t = AdaptiveLayoutCalculator.Compute(0, 480, 1024, 600, ScreenAdaptiveMode.Fit);
        Assert.Equal(1, t.ScaleX);
        Assert.Equal(0, t.OffsetX);
    }

    [Theory]
    [InlineData("1024x600", 1024, 600)]
    [InlineData("480×800", 480, 800)]
    [InlineData("1280X800", 1280, 800)]
    public void TryParseResolution_Valid(string text, double w, double h)
    {
        var r = AdaptiveLayoutCalculator.TryParseResolution(text);
        Assert.NotNull(r);
        Assert.Equal(w, r!.Value.Width);
        Assert.Equal(h, r.Value.Height);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("800")]
    [InlineData("0x480")]
    public void TryParseResolution_Invalid(string? text)
    {
        Assert.Null(AdaptiveLayoutCalculator.TryParseResolution(text));
    }

    // ── CodeGen ──

    [Fact]
    public void GenerateScreenCode_NoneMode_NoAdaptiveCode()
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl { Id = "c1", Type = HmiControlType.Button, X = 10, Y = 20, Width = 100, Height = 40 });

        var code = new CodeGenerator().GenerateScreenCode(screen);
        Assert.DoesNotContain("adaptive_layout", code);
        Assert.DoesNotContain("lv_display_get_horizontal_resolution", code);
    }

    [Fact]
    public void GenerateScreenCode_FitMode_EmitsLayoutFunctionAndCall()
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.AdaptiveMode = ScreenAdaptiveMode.Fit;
        screen.Controls.Add(new HmiControl { Id = "c1", Type = HmiControlType.Button, X = 10, Y = 20, Width = 100, Height = 40 });

        var code = new CodeGenerator().GenerateScreenCode(screen);

        CSource.AssertCode(code, "static void adaptive_layout_s1(lv_obj_t* screen)");
        Assert.Contains("lv_display_get_horizontal_resolution", code);
        Assert.Contains("float s = sx < sy ? sx : sy;", code);
        CSource.AssertCode(code, "adaptive_layout_s1(screen);");
        // 控件坐标用 Fit 的统一比例 s
        CSource.AssertCode(code, "lv_obj_set_pos(obj_c1, (lv_coord_t)(10 * s + ox), (lv_coord_t)(20 * s + oy));");
        CSource.AssertCode(code, "lv_obj_set_size(obj_c1, (lv_coord_t)(100 * s), (lv_coord_t)(40 * s));");
    }

    [Fact]
    public void GenerateScreenCode_FillMode_UsesIndependentScales()
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.AdaptiveMode = ScreenAdaptiveMode.Fill;
        screen.Controls.Add(new HmiControl { Id = "c1", Type = HmiControlType.Label, X = 0, Y = 0, Width = 200, Height = 50 });

        var code = new CodeGenerator().GenerateScreenCode(screen);

        Assert.Contains("float ox = 0.0f, oy = 0.0f;", code);
        CSource.AssertCode(code, "(lv_coord_t)(0 * sx + ox)");
        CSource.AssertCode(code, "(lv_coord_t)(200 * sx)");
        Assert.DoesNotContain("float s = sx < sy", code);
    }

    // ── 序列化 ──

    [Fact]
    public void Serialization_AdaptiveMode_RoundTrips()
    {
        var project = new HmiProject();
        var screen = new HmiScreen { Name = "Main", Width = 800, Height = 480 };
        screen.AdaptiveMode = ScreenAdaptiveMode.Fill;
        project.Screens.Add(screen);

        var json = ProjectSerializer.Serialize(project);
        Assert.Contains("\"adaptiveMode\": \"fill\"", json);

        var back = ProjectSerializer.Deserialize(json);
        Assert.NotNull(back);
        Assert.Equal(ScreenAdaptiveMode.Fill, back!.Screens[0].AdaptiveMode);
    }

    [Fact]
    public void Serialization_OldProjectWithoutMode_DefaultsToNone()
    {
        // 旧项目 JSON 无 adaptiveMode 字段 → 默认 None（兼容）
        var json = """{"screens":[{"id":"s1","name":"Old","width":800,"height":480,"controls":[]}]}""";
        var back = ProjectSerializer.Deserialize(json);
        Assert.NotNull(back);
        Assert.Equal(ScreenAdaptiveMode.None, back!.Screens[0].AdaptiveMode);
    }
}
