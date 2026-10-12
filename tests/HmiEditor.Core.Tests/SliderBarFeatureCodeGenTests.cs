using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// Slider/Bar 显示刻度（ShowTicks）与显示指针（ShowPointer）代码生成单元测试。
/// </summary>
public class SliderBarFeatureCodeGenTests
{
    private static HmiControl MakeSlider(Action<HmiControl>? config = null)
    {
        var c = new HmiControl
        {
            Id = "sl1", Name = "Slider1", Type = HmiControlType.Slider,
            X = 0, Y = 0, Width = 300, Height = 20,
            MinValue = 0, MaxValue = 100, StepValue = 10
        };
        config?.Invoke(c);
        return c;
    }

    private static HmiControl MakeBar(Action<HmiControl>? config = null)
    {
        var c = new HmiControl
        {
            Id = "bar1", Name = "Bar1", Type = HmiControlType.Bar,
            X = 0, Y = 0, Width = 300, Height = 20,
            MinValue = 0, MaxValue = 100, StepValue = 10
        };
        config?.Invoke(c);
        return c;
    }

    private static string GenScreen(HmiControl control)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(control);
        return new CodeGenerator().GenerateScreenCode(screen);
    }

    [Fact]
    public void Slider_ShowPointerFalse_HidesKnob()
    {
        var code = GenScreen(MakeSlider(s => s.ShowPointer = false));
        CSource.AssertCode(code, "lv_obj_set_style_opa(obj_sl1, LV_OPA_TRANSP, LV_PART_KNOB);");
    }

    [Fact]
    public void Slider_ShowPointerNull_KeepsKnobByDefault()
    {
        // 默认（null）按类型默认显示 knob，不生成隐藏代码；knob 颜色/描边与设计器一致
        var code = GenScreen(MakeSlider());
        Assert.DoesNotContain("LV_OPA_TRANSP, LV_PART_KNOB", code);
        // knob 底色 = 主题主色兜底（theme null → PaletteDefaults.Primary）
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_sl1, lv_color_hex(0x2563eb), LV_PART_KNOB);");
        CSource.AssertCode(code, "lv_obj_set_style_border_color(obj_sl1, lv_color_hex(0x0f172a), LV_PART_KNOB);");
        // knob 顶部小三角真实生成（高对比：主色亮度暗 → 亮色三角；lv_canvas 逐像素绘制）
        CSource.AssertCode(code, "_knobtri_sl1 = lv_canvas_create(obj_sl1);");
        CSource.AssertCode(code, "lv_draw_buf_create(14, 8, LV_COLOR_FORMAT_ARGB8888, 0)");
        CSource.AssertCode(code, "lv_canvas_set_draw_buf(_knobtri_sl1, _tribuf__knobtri_sl1);");
        CSource.AssertCode(code, "lv_canvas_set_px(_knobtri_sl1, _x, _y, lv_color_hex(0xe2e8f0), LV_OPA_COVER);");
        CSource.AssertCode(code, "lv_obj_align(_knobtri_sl1, LV_ALIGN_LEFT_MID, -10, -5);");
    }

    [Fact]
    public void Bar_ShowPointerTrue_EmitsRealCursorAndTriangle()
    {
        var code = GenScreen(MakeBar(b => b.ShowPointer = true));
        CSource.AssertCode(code, "_cur_bar1 = lv_obj_create(obj_bar1);");
        CSource.AssertCode(code, "lv_obj_set_style_radius(_cur_bar1, LV_RADIUS_CIRCLE, 0);");
        CSource.AssertCode(code, "lv_obj_align(_cur_bar1, LV_ALIGN_LEFT_MID, -8, 0);");
        CSource.AssertCode(code, "_curtri_bar1 = lv_canvas_create(obj_bar1);");
    }

    [Fact]
    public void Bar_ShowPointerNull_NoCursorByDefault()
    {
        // 默认（null）按类型默认 Bar 无指针，不生成游标
        var code = GenScreen(MakeBar());
        Assert.DoesNotContain("_cur_bar1", code);
        Assert.DoesNotContain("_curtri_bar1", code);
    }

    [Fact]
    public void Slider_ShowTicksTrue_EmitsRealTicksWithValues()
    {
        // 0-100 步长 10 → 11 条刻度（含两端），真实生成 lv_obj 刻度 + lv_label 数值标签
        var code = GenScreen(MakeSlider(s => s.ShowTicks = true));
        Assert.Contains("/* Slider 刻度（轨道上方外侧）: 11 条 */", code);
        CSource.AssertCode(code, "lv_obj_t* _tick_sl1_0 = lv_obj_create(obj_sl1);");
        CSource.AssertCode(code, "lv_obj_align(_tick_sl1_0, LV_ALIGN_TOP_LEFT, 0, -5);");
        // 数值标签：首刻度 0、末刻度 100
        CSource.AssertCode(code, "lv_label_set_text(_ticklbl_sl1_0, \"0\");");
        CSource.AssertCode(code, "lv_label_set_text(_ticklbl_sl1_10, \"100\");");
        // 数值标签在刻度线上方、居中于刻度；使用嵌入 CJK 子集（montserrat_12 无中文字形 → 标题含中文时 tofu）
        CSource.AssertCode(code, "lv_obj_align(_ticklbl_sl1_0, LV_ALIGN_TOP_MID, -150, -16);");
        CSource.AssertCode(code, "lv_obj_set_style_text_font(_ticklbl_sl1_0, &lv_font_simsun_14_cjk, 0);");
    }

    [Fact]
    public void Slider_ShowTicksFalse_NoTickObjects()
    {
        var code = GenScreen(MakeSlider());
        Assert.DoesNotContain("_tick_sl1_", code);
        Assert.DoesNotContain("刻度（轨道上方外侧）", code);
    }

    [Fact]
    public void Bar_ShowTicksTrue_EmitsRealTicks()
    {
        var code = GenScreen(MakeBar(b => b.ShowTicks = true));
        Assert.Contains("/* Bar 刻度（轨道上方外侧）: 11 条 */", code);
        CSource.AssertCode(code, "lv_obj_t* _tick_bar1_0 = lv_obj_create(obj_bar1);");
        CSource.AssertCode(code, "lv_label_set_text(_ticklbl_bar1_0, \"0\");");
    }

    [Fact]
    public void Slider_ValueChanged_EmitsCallbackRegistration()
    {
        // 值变化（触摸拖动/代码设值）时 knob 顶部小三角跟随移动：必须注册 LV_EVENT_VALUE_CHANGED 回调
        var code = GenScreen(MakeSlider());
        CSource.AssertCode(code, "lv_obj_add_event_cb(obj_sl1, _slider_sl1_val_cb, LV_EVENT_VALUE_CHANGED, NULL);");
    }

    [Fact]
    public void Bar_ShowPointerTrue_ValueChanged_EmitsCallbackRegistration()
    {
        // Bar 游标+小三角跟随：ShowPointer=true 时注册值变化回调
        var code = GenScreen(MakeBar(b => b.ShowPointer = true));
        CSource.AssertCode(code, "lv_obj_add_event_cb(obj_bar1, _bar_bar1_val_cb, LV_EVENT_VALUE_CHANGED, NULL);");
    }

    [Fact]
    public void PointerValueCallbacks_ProjectLevel_EmitsCallbackFunctions()
    {
        // 文件级回调函数（项目级生成，跨 create_screen 访问 static 三角对象）：
        // Slider 回调按 (v-min)/(max-min)*w 更新 _knobtri_ 位置；Bar 回调更新 _cur_ + _curtri_。
        var project = new HmiProject();
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(MakeSlider());                       // ShowPointer null → 有 knob 三角
        screen.Controls.Add(MakeBar(b => b.ShowPointer = true)); // ShowPointer true → 有游标+三角
        project.Screens.Add(screen);
        var code = new CodeGenerator().GenerateSimulatorModule(project);
        CSource.AssertCode(code, "static void _slider_sl1_val_cb(lv_event_t* e)");
        CSource.AssertCode(code, "lv_obj_align(_knobtri_sl1, LV_ALIGN_LEFT_MID, x, -5);");
        CSource.AssertCode(code, "static void _bar_bar1_val_cb(lv_event_t* e)");
        CSource.AssertCode(code, "lv_obj_align(_cur_bar1, LV_ALIGN_LEFT_MID, x - 7, 0);");
        CSource.AssertCode(code, "lv_obj_align(_curtri_bar1, LV_ALIGN_LEFT_MID, x - 7, -2);");
    }
}
