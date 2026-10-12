using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Animation;
using Xunit;

namespace HmiEditor.Core.Tests;

public class CodeGenTests
{
    [Fact]
    public void GenerateScreenCode_EmptyScreen_ContainsCreateFunction()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "test1", Name = "Test", Width = 800, Height = 480 };

        var code = generator.GenerateScreenCode(screen);

        Assert.Contains("create_screen_test1", code);
        Assert.Contains("lv_obj_create", code);
        Assert.Contains("lv_obj_set_size", code);
    }

    [Fact]
    public void GenerateFullProject_EmptyProject_ContainsMainFunction()
    {
        var generator = new CodeGenerator();
        var project = new HmiProject { Name = "Test", LvglVersion = "9.2" };

        var code = generator.GenerateFullProject(project);

        CSource.AssertCode(code, "int main(void)");
        CSource.AssertCode(code, "lv_init()");
        CSource.AssertCode(code, "lv_task_handler()");
        CSource.AssertCode(code, "lv_tick_inc(5)");
    }

    [Fact]
    public void GenerateFullProject_WithScreen_GeneratesScreenFunction()
    {
        var generator = new CodeGenerator();
        var project = new HmiProject { LvglVersion = "9.2" };
        project.Screens.Add(new HmiScreen
        {
            Id = "s1", Name = "Main", Width = 800, Height = 480
        });

        var code = generator.GenerateFullProject(project);

        Assert.Contains("create_screen_s1", code);
        Assert.Contains("lv_screen_load", code);
    }

    [Fact]
    public void GenerateScreenCode_ControlWithText_GeneratesLabelSetText()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s2", Name = "Main", Width = 800, Height = 480 };
        var ctrl = new HmiControl
        {
            Id = "c1", Name = "Label1", Type = HmiControlType.Label,
            X = 10, Y = 20, Width = 200, Height = 30,
            Text = "Hello HMI"
        };
        screen.Controls.Add(ctrl);

        var code = generator.GenerateScreenCode(screen);

        Assert.Contains("lv_label_create", code);
        CSource.AssertCode(code, "lv_label_set_text(obj_c1, \"Hello HMI\")");
        CSource.AssertCode(code, "lv_obj_set_pos(obj_c1, 10, 20)");
    }

    [Fact]
    public void GenerateScreenCode_ControlWithBgColor_GeneratesStyleCall()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s3", Name = "Main", Width = 800, Height = 480 };
        var ctrl = new HmiControl
        {
            Id = "c2", Name = "Btn1", Type = HmiControlType.Button,
            X = 0, Y = 0, Width = 100, Height = 40,
            BgColor = "#38bdf8", Radius = 6
        };
        screen.Controls.Add(ctrl);

        var code = generator.GenerateScreenCode(screen);

        Assert.Contains("lv_button_create", code);
        Assert.Contains("38bdf8", code);
        Assert.Contains("lv_obj_set_style_radius", code);
    }

    [Fact]
    public void GenerateScreenCode_Control_EmitsFileScopeStaticDeclaration()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s4", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c5", Name = "Btn", Type = HmiControlType.Button,
            X = 10, Y = 10, Width = 80, Height = 30
        });

        var code = generator.GenerateScreenCode(screen);

        // 文件级 static 声明必须在 create_screen 函数之前
        var staticDecl = "static lv_obj_t* obj_c5;";
        Assert.Contains(staticDecl, code);

        var staticLineIdx = code.IndexOf(staticDecl, StringComparison.Ordinal);
        var funcIdx = code.IndexOf("create_screen_s4", StringComparison.Ordinal);
        Assert.True(staticLineIdx >= 0 && funcIdx >= 0 && staticLineIdx < funcIdx,
            "static declaration must appear before the create_screen function");
    }

    [Fact]
    public void GenerateScreenCode_Control_UsesAssignmentNotDeclaration()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s5", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c6", Name = "Label", Type = HmiControlType.Label,
            X = 0, Y = 0, Width = 100, Height = 20
        });

        var code = generator.GenerateScreenCode(screen);

        // 函数内应为赋值（obj_c6 = ...），而非声明（lv_obj_t* obj_c6 = ...）
        Assert.DoesNotContain("lv_obj_t* obj_c6 =", code);
        CSource.AssertCode(code, "obj_c6 = lv_label_create(screen)");
    }

    [Fact]
    public void GenerateFullProject_WithMultipleScreens_GeneratesAllScreens()
    {
        var generator = new CodeGenerator();
        var project = new HmiProject { LvglVersion = "9.2" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });
        project.Screens.Add(new HmiScreen { Id = "s2", Name = "Settings", Width = 480, Height = 272 });

        var code = generator.GenerateFullProject(project);

        Assert.Contains("create_screen_s1", code);
        Assert.Contains("create_screen_s2", code);
    }

    [Fact]
    public void LvglApiWriter_WriteCreateCall_ReturnsValidFunction()
    {
        var ctrl = new HmiControl
        {
            Id = "t1", Name = "Test", Type = HmiControlType.Button
        };

        var call = LvglApiWriter.WriteCreateCall(ctrl);
        Assert.Equal("lv_button_create(screen)", call);
    }

    [Fact]
    public void GenerateSharedLibraryProject_ThemeUsesProjectFontAsset()
    {
        // 回归（2026-08-16 实机）：主题代码未传资产库 → 主题字体回落 LV_FONT_DEFAULT
        // （montserrat 无中文字形）→ 实机全部中文渲染为 tofu 方框。
        var generator = new CodeGenerator();
        var project = new HmiProject { Name = "So", LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "home", Name = "Home", Width = 1024, Height = 600 });
        var assets = new AssetLibrary();
        assets.Assets.Add(new AssetItem
        {
            Id = "font_cjk",
            Type = AssetType.Font,
            FilePath = "font_cjk.ttc",
            ReferenceCount = 1
        });

        var code = generator.GenerateSharedLibraryProject(project, assets);

        CSource.AssertCode(code, "lv_obj_set_style_text_font(scr, font_font_cjk, 0)");
        CSource.AssertNoCode(code, "lv_obj_set_style_text_font(scr, LV_FONT_DEFAULT, 0)");
        CSource.AssertCode(code, "hmi_resolve_font(\"font_cjk\"");
        // 回归（2026-08-16 实机）：init 时必须按传入 root_scr 应用主题可继承默认 ——
        // 工程屏幕不继承初始化时 LVGL 默认屏的样式，否则中文字体回落 montserrat。
        CSource.AssertCode(code, "static void hmi_ui_apply_theme(lv_obj_t* root_scr)");
        CSource.AssertCode(code, "lv_obj_set_style_text_font(root_scr, font_font_cjk, 0)");
    }

    [Fact]
    public void GenerateSharedLibraryProject_DoesNotRedefineScreenLayerRefType()
    {
        // 回归：screen_manager.h 已定义 screen_layer_ref_t，生成代码重复 typedef
        // 在 -std=c99 下是 "conflicting types" 编译错误，导致 .so 无法交叉编译。
        var generator = new CodeGenerator();
        var project = new HmiProject { Name = "So", LvglVersion = "9.5" };
        project.Screens.Add(new HmiScreen { Id = "home", Name = "Home", Width = 1024, Height = 600 });

        var code = generator.GenerateSharedLibraryProject(project);

        Assert.DoesNotContain("typedef struct screen_layer_ref", code);
        Assert.Contains("screen_layer_ref_t HMI_LAYER_REFS", code);
        Assert.Contains("hmi_ui_get_layers", code);
        Assert.Contains("hmi_ui_get_api", code);
    }

    [Fact]
    public void GenerateSharedLibraryProject_WithReferencedLayer_EmitLayerRefsTable()
    {
        var generator = new CodeGenerator();
        var project = new HmiProject { Name = "So", LvglVersion = "9.5" };
        var layer = new HmiScreen { Id = "layer1", Name = "公共图层", Width = 1024, Height = 600, IsPublicScreen = true };
        var screen = new HmiScreen { Id = "home", Name = "Home", Width = 1024, Height = 600 };
        project.Screens.Add(layer);
        project.Screens.Add(screen);
        screen.ReferencedLayerIds.Add("layer1");

        var code = generator.GenerateSharedLibraryProject(project);

        Assert.Contains("HMI_LAYER_REF_COUNT = 1", code);
        Assert.Contains("\"layer1\"", code);
    }

    [Fact]
    public void LvglApiWriter_WriteTextCall_ButtonWithText_ReturnsLabelSetText()
    {
        var ctrl = new HmiControl
        {
            Id = "t2", Name = "Test", Type = HmiControlType.Button,
            Text = "OK"
        };

        var call = LvglApiWriter.WriteTextCall(ctrl, "obj_t2");
        CSource.AssertCode(call, "lv_label_set_text(obj_t2, \"OK\")");
    }

    // ═══════════════════════════════════════════════
    //  动画代码生成测试
    // ═══════════════════════════════════════════════

    [Fact]
    public void GenerateAnimationCode_EmptyList_ReturnsNoAnimationsComment()
    {
        var generator = new CodeGenerator();
        var code = generator.GenerateAnimationCode(new List<AnimationConfig>());

        Assert.Contains("No animations configured", code);
    }

    [Fact]
    public void GenerateAnimationCode_SingleAnimation_GeneratesStartFunction()
    {
        var generator = new CodeGenerator();
        var anim = new AnimationConfig
        {
            Id = "a1b2c3d4",
            Name = "Fade In",
            ControlId = "btn01",
            PropertyName = "Opacity",
            StartValue = 0,
            EndValue = 255,
            DurationMs = 500,
            DelayMs = 100,
            PathType = AnimationPathType.EaseInOut,
            RepeatCount = 0,
            IsEnabled = true,
            Trigger = "OnLoad"
        };

        var code = generator.GenerateAnimationCode(new List<AnimationConfig> { anim });

        Assert.Contains("start_anim_a1b2c3d4", code);
        Assert.Contains("lv_anim_init", code);
        Assert.Contains("lv_anim_set_exec_cb", code);
        CSource.AssertCode(code, "lv_anim_set_values(&a, 0.0, 255.0)");
        CSource.AssertCode(code, "lv_anim_set_duration(&a, 500)");
        CSource.AssertCode(code, "lv_anim_set_delay(&a, 100)");
        Assert.Contains("lv_anim_path_ease_in_out", code);
        Assert.Contains("lv_anim_start", code);
        Assert.Contains("apply_animations", code);
    }

    [Fact]
    public void GenerateAnimationCode_DisabledAnimation_NotIncluded()
    {
        var generator = new CodeGenerator();
        var anim = new AnimationConfig
        {
            Name = "Disabled",
            IsEnabled = false,
            Trigger = "OnLoad"
        };

        var code = generator.GenerateAnimationCode(new List<AnimationConfig> { anim });

        Assert.Contains("No animations configured", code);
    }

    [Fact]
    public void GenerateAnimationCode_WithKeyframes_GeneratesKeyframeComments()
    {
        var generator = new CodeGenerator();
        var anim = new AnimationConfig
        {
            Id = "kf01",
            Name = "Bounce",
            ControlId = "obj1",
            PropertyName = "Y",
            StartValue = 0,
            EndValue = 100,
            DurationMs = 1000,
            IsEnabled = true,
            Trigger = "OnLoad"
        };
        anim.Keyframes.Add(new AnimationKeyframe { TimePercent = 0, Value = 0 });
        anim.Keyframes.Add(new AnimationKeyframe { TimePercent = 50, Value = 80, PathType = AnimationPathType.Bounce });
        anim.Keyframes.Add(new AnimationKeyframe { TimePercent = 100, Value = 100 });

        var code = generator.GenerateAnimationCode(new List<AnimationConfig> { anim });

        CSource.AssertCode(code, "Keyframes (reference)");
        Assert.Contains("0% -> 0.0", code);
        Assert.Contains("50% -> 80.0", code);
        Assert.Contains("100% -> 100.0", code);
        Assert.Contains("lv_anim_path_bounce", code);
    }

    [Fact]
    public void GenerateAnimationCode_OnClickTrigger_GeneratesEventComment()
    {
        var generator = new CodeGenerator();
        var anim = new AnimationConfig
        {
            Id = "clk01",
            Name = "Click Pulse",
            ControlId = "btn1",
            PropertyName = "Width",
            StartValue = 100,
            EndValue = 120,
            DurationMs = 200,
            IsEnabled = true,
            Trigger = "OnClick"
        };

        var code = generator.GenerateAnimationCode(new List<AnimationConfig> { anim });

        Assert.Contains("start_anim_clk01", code);
        Assert.Contains("OnClick: call start_anim_clk01", code);
    }

    [Fact]
    public void GenerateAnimationCode_InfiniteRepeat_GeneratesInfiniteMacro()
    {
        var generator = new CodeGenerator();
        var anim = new AnimationConfig
        {
            Id = "loop01",
            Name = "Pulse Loop",
            PropertyName = "Opacity",
            StartValue = 0,
            EndValue = 255,
            DurationMs = 800,
            RepeatCount = -1,
            IsEnabled = true,
            Trigger = "OnLoad"
        };

        var code = generator.GenerateAnimationCode(new List<AnimationConfig> { anim });

        Assert.Contains("LV_ANIM_REPEAT_INFINITE", code);
    }

    [Fact]
    public void GenerateAnimationCode_NoControlId_UsesScreenActive()
    {
        var generator = new CodeGenerator();
        var anim = new AnimationConfig
        {
            Id = "noctrl",
            Name = "Screen Fade",
            ControlId = "",
            PropertyName = "Opacity",
            IsEnabled = true,
            Trigger = "OnLoad"
        };

        var code = generator.GenerateAnimationCode(new List<AnimationConfig> { anim });

        CSource.AssertCode(code, "lv_screen_active()");
    }

    [Fact]
    public void GenerateFullProject_WithAnimations_IncludesApplyAnimations()
    {
        var generator = new CodeGenerator();
        var project = new HmiProject { LvglVersion = "9.2" };
        project.Screens.Add(new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 });
        project.AnimationConfigs.Add(new AnimationConfig
        {
            Id = "fade01",
            Name = "Fade",
            ControlId = "s1",
            PropertyName = "Opacity",
            IsEnabled = true,
            Trigger = "OnLoad"
        });

        var code = generator.GenerateFullProject(project);

        CSource.AssertCode(code, "apply_animations()");
        Assert.Contains("start_anim_fade01", code);
    }

    [Fact]
    public void GenerateFullProject_NoAnimations_DoesNotCallApplyAnimations()
    {
        var generator = new CodeGenerator();
        var project = new HmiProject { LvglVersion = "9.2" };

        var code = generator.GenerateFullProject(project);

        CSource.AssertNoCode(code, "apply_animations()");
    }

    // ── I10: 基础绘图图形代码生成 ──

    [Fact]
    public void GenerateScreenCode_Rectangle_EmitsObjCreateWithBgAndBorder()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s10", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c10", Name = "Rect1", Type = HmiControlType.Rectangle,
            X = 10, Y = 10, Width = 160, Height = 120,
            BgColor = "#334155", BorderWidth = 2, BorderColor = "#64748b", Radius = 6
        });

        var code = generator.GenerateScreenCode(screen);

        Assert.Contains("lv_obj_create", code);
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_c10, lv_color_hex(0x334155), 0)");
        CSource.AssertCode(code, "lv_obj_set_style_border_width(obj_c10, 2, 0)");
        CSource.AssertCode(code, "lv_obj_set_style_radius(obj_c10, 6, 0)");
    }

    [Fact]
    public void GenerateScreenCode_Ellipse_EmitsCanvasWithBuffer()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s11", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c11", Name = "Ellipse1", Type = HmiControlType.Ellipse,
            X = 10, Y = 10, Width = 140, Height = 100,
            BgColor = "#334155", BorderWidth = 2, BorderColor = "#64748b"
        });

        var code = generator.GenerateScreenCode(screen);

        CSource.AssertCode(code, "static uint8_t _shapebuf_c11[LV_CANVAS_BUF_SIZE(140, 100, 32, LV_DRAW_BUF_STRIDE_ALIGN)];");
        Assert.Contains("lv_canvas_create", code);
        Assert.Contains("lv_draw_triangle", code);   // 椭圆填充（三角扇近似，LVGL 9.x 无 lv_draw_polygon）
        Assert.Contains("lv_draw_line", code);
        Assert.DoesNotContain("lv_draw_polygon", code);
    }

    [Fact]
    public void GenerateScreenCode_Polyline_EmitsLinePoints()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s12", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c12", Name = "Poly1", Type = HmiControlType.Polyline,
            X = 10, Y = 10, Width = 160, Height = 80,
            BgColor = "#ffffff", BorderWidth = 2,
            Points = new List<PointData> { new(0, 0), new(80, 40), new(160, 10) }
        });

        var code = generator.GenerateScreenCode(screen);

        Assert.Contains("lv_line_create", code);
        Assert.Contains("_pts_c12[] = { { 0, 0 }, { 80, 40 }, { 160, 10 } }", code);
        CSource.AssertCode(code, "lv_line_set_points(obj_c12, _pts_c12, 3)");
        CSource.AssertCode(code, "lv_obj_set_style_line_color(obj_c12, lv_color_hex(0xffffff), 0)");
    }

    [Fact]
    public void GenerateScreenCode_Arc_EmitsLvArcAnglesAndColors()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s13", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "c13", Name = "Arc1", Type = HmiControlType.Arc,
            X = 10, Y = 10, Width = 120, Height = 120,
            BgColor = "#334155", IndicatorColor = "#38bdf8",
            MinValue = 0, MaxValue = 360, Value = 270, BorderWidth = 10
        });

        var code = generator.GenerateScreenCode(screen);

        Assert.Contains("lv_arc_create", code);
        CSource.AssertCode(code, "lv_arc_set_bg_angles(obj_c13, 0, 360)");
        CSource.AssertCode(code, "lv_arc_set_value(obj_c13, 270)");
        CSource.AssertCode(code, "lv_obj_set_style_arc_width(obj_c13, 10, LV_PART_MAIN)");
        CSource.AssertCode(code, "lv_color_hex(0x38bdf8)");
    }

    // ── I7: 过程图形控件代码生成（设计器 = 模拟器）──

    [Fact]
    public void GenerateScreenCode_Tank_EmitsCanvasLiquidAndScale()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s20", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "t1", Name = "Tank1", Type = HmiControlType.Tank,
            X = 10, Y = 10, Width = 140, Height = 200,
            BgColor = "#64748b", IndicatorColor = "#0ea5e9",
            MinValue = 0, MaxValue = 100, Value = 60,
            TankShowScale = true, TankTickCount = 5
        });

        var code = generator.GenerateScreenCode(screen);

        CSource.AssertCode(code, "static uint8_t _buf_t1[LV_CANVAS_BUF_SIZE(140, 200, 32, LV_DRAW_BUF_STRIDE_ALIGN)];");
        Assert.Contains("lv_canvas_create", code);
        Assert.Contains("lv_draw_rect", code);          // 罐壁 + 液位填充
        Assert.Contains("lv_draw_line", code);          // 液面高光线 + 刻度
        // 液位进度 60% → 液面顶 y = 200-4-0.6*(192) = 80.8
        Assert.Contains("0x0EA5E9", code);
    }

    [Fact]
    public void GenerateScreenCode_Pipe_EmitsLineArrowAndFlow()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s21", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(new HmiControl
        {
            Id = "p1", Name = "Pipe1", Type = HmiControlType.Pipe,
            X = 10, Y = 10, Width = 160, Height = 8,
            BgColor = "#64748b", IndicatorColor = "#38bdf8",
            PipeDirection = "Horizontal", PipeFlowSpeed = 2000, BorderWidth = 4
        });

        var code = generator.GenerateScreenCode(screen);

        Assert.Contains("lv_canvas_create", code);
        Assert.Contains("lv_draw_line", code);   // 主轴 + 箭头 + 流动段
        Assert.Contains("0x38BDF8", code);       // 箭头/流动色
        Assert.Contains("lv_canvas_finish_layer", code);
    }

    [Fact]
    public void GenerateScreenCode_Valve_EmitsSymbolByType()
    {
        var generator = new CodeGenerator();
        var screen = new HmiScreen { Id = "s22", Name = "Main", Width = 800, Height = 480 };
        // 球阀（默认）：圆体 + 手柄
        screen.Controls.Add(new HmiControl
        {
            Id = "v1", Name = "Valve1", Type = HmiControlType.Valve,
            X = 10, Y = 10, Width = 60, Height = 60,
            BgColor = "#ef4444", IndicatorColor = "#22c55e", Value = 1
        });
        // 截止阀：三角形
        screen.Controls.Add(new HmiControl
        {
            Id = "v2", Name = "Valve2", Type = HmiControlType.Valve,
            X = 80, Y = 10, Width = 60, Height = 60,
            ValveType = "Globe", Value = 0
        });

        var code = generator.GenerateScreenCode(screen);

        Assert.Contains("lv_draw_arc", code);       // 球阀圆体
        Assert.Contains("lv_draw_triangle", code);  // 截止阀三角形（LVGL 9.x 无 lv_draw_polygon）
        Assert.Contains("lv_draw_line", code);      // 管道线/手柄/阀杆
        Assert.DoesNotContain("lv_draw_polygon", code);
    }
}
