using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 回归测试：确保图片自动缩放、文本字号、文本对齐、背景色四项设计器设置
/// 在模拟器代码生成链路中正确输出。
/// </summary>
public class SimulatorFixVerification
{
    private readonly ITestOutputHelper _out;
    public SimulatorFixVerification(ITestOutputHelper o) => _out = o;

    [Fact]
    public void Synthetic_AllFourFixes_GenerateCorrectLvgl()
    {
        var fontId = "fnt1";
        var project = new HmiProject { Name = "Verify", LvglVersion = "9.5" };
        project.Theme.FontSize = 14; // 主题默认字号
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        // 1) Label：自定义字号 + 背景色 + 文本色 + 右对齐 + 顶对齐
        scr.Controls.Add(new HmiControl
        {
            Id = "lbl1", Name = "L1", Type = HmiControlType.Label,
            X = 10, Y = 10, Width = 200, Height = 60,
            Text = "测试文本ABC",
            FontFamily = fontId, FontSize = 28,
            BgColor = "#334455", TextColor = "#ff0000",
            TextAlign = TextAlignMode.Right, TextVAlign = TextVAlignMode.Top
        });

        // 2) Image：Fill（拉伸填充）
        scr.Controls.Add(new HmiControl
        {
            Id = "img1", Name = "I1", Type = HmiControlType.Image,
            X = 50, Y = 100, Width = 120, Height = 90,
            StretchMode = "Fill", ImageAssetId = "pic1"
        });

        var lib = new AssetLibrary();
        lib.AddAsset(new AssetItem { Id = fontId, Name = "MyFont", Type = AssetType.Font, FilePath = "/tmp/none.ttf" });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, lib);

        // 背景色 → bg_opa=LV_OPA_COVER（否则 Label 默认透明看不见）
        CSource.AssertCode(code, "lv_obj_set_style_bg_opa(obj_lbl1, LV_OPA_COVER, 0)");
        // 文本色生效
        CSource.AssertCode(code, "lv_obj_set_style_text_color(obj_lbl1,");
        // 水平对齐 → RIGHT
        Assert.Contains("LV_TEXT_ALIGN_RIGHT", code);
        // 垂直对齐 → content-size + 重定位
        CSource.AssertCode(code, "lv_obj_set_height(obj_lbl1, LV_SIZE_CONTENT)");
        // 自定义字号字体 → FreeType 动态字体变量，且字号 != 主题默认
        Assert.Contains("#if LV_USE_FREETYPE", code);
        CSource.AssertCode(code, "lv_obj_set_style_text_font(obj_lbl1, &font_fnt1_28, 0)");
        // 图片 Fill → STRETCH
        CSource.AssertCode(code, "lv_image_set_inner_align(obj_img1, LV_IMAGE_ALIGN_STRETCH)");
        // 变尺寸字体外部声明
        Assert.Contains("extern lv_font_t font_fnt1_28;", code);

        _out.WriteLine("SYNTHETIC code-gen OK — all four fixes present");
    }

    [Fact]
    public void ImageStretchModes_MapToLvglInnerAlign()
    {
        var project = new HmiProject { Name = "VerifyStretch", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        // Contain / Center / None / 默认(null) 四种情况
        scr.Controls.Add(new HmiControl
        {
            Id = "img_contain", Name = "IC", Type = HmiControlType.Image,
            X = 10, Y = 10, Width = 120, Height = 90, StretchMode = "Contain", ImageAssetId = "pic1"
        });
        scr.Controls.Add(new HmiControl
        {
            Id = "img_center", Name = "IC2", Type = HmiControlType.Image,
            X = 10, Y = 110, Width = 120, Height = 90, StretchMode = "Center", ImageAssetId = "pic1"
        });
        scr.Controls.Add(new HmiControl
        {
            Id = "img_none", Name = "IN", Type = HmiControlType.Image,
            X = 10, Y = 210, Width = 120, Height = 90, StretchMode = "None", ImageAssetId = "pic1"
        });
        scr.Controls.Add(new HmiControl
        {
            Id = "img_default", Name = "ID", Type = HmiControlType.Image,
            X = 10, Y = 310, Width = 120, Height = 90, ImageAssetId = "pic1" // StretchMode=null
        });

        var lib = new AssetLibrary();
        lib.AddAsset(new AssetItem { Id = "pic1", Name = "Pic", Type = AssetType.Image, FilePath = "/tmp/none.png" });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, lib);

        // Contain → LV_IMAGE_ALIGN_CONTAIN（9.5 走真实分支）
        CSource.AssertCode(code, "lv_image_set_inner_align(obj_img_contain, LV_IMAGE_ALIGN_CONTAIN);");
        // Center/None → LV_IMAGE_ALIGN_CENTER（无版本宏，直接生成）
        CSource.AssertCode(code, "lv_image_set_inner_align(obj_img_center, LV_IMAGE_ALIGN_CENTER);");
        CSource.AssertCode(code, "lv_image_set_inner_align(obj_img_none, LV_IMAGE_ALIGN_CENTER);");
        // null → 默认 STRETCH
        CSource.AssertCode(code, "lv_image_set_inner_align(obj_img_default, LV_IMAGE_ALIGN_STRETCH);");
    }

    [Fact]
    public void BorderColor_GeneratesLvglBorderColor()
    {
        var project = new HmiProject { Name = "VerifyBorder", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        scr.Controls.Add(new HmiControl
        {
            Id = "btn_border", Name = "BB", Type = HmiControlType.Button,
            X = 10, Y = 10, Width = 120, Height = 40,
            Text = "OK", BorderWidth = 2, BorderColor = "#ff8800"
        });
        scr.Controls.Add(new HmiControl
        {
            Id = "btn_noborder", Name = "BN", Type = HmiControlType.Button,
            X = 10, Y = 60, Width = 120, Height = 40,
            Text = "NO", BorderWidth = 0, BorderColor = "#ff8800" // width=0 时不生成颜色
        });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        // 有边框色 → 生成 border_color
        CSource.AssertCode(code, "lv_obj_set_style_border_width(obj_btn_border, 2, 0)");
        CSource.AssertCode(code, "lv_obj_set_style_border_color(obj_btn_border, lv_color_hex(0xff8800), 0)");
        // width=0 → 该控件不生成任何 border_* 样式
        CSource.AssertNoCode(code, "border_width(obj_btn_noborder");
        CSource.AssertNoCode(code, "border_color(obj_btn_noborder");
    }

    [Fact]
    public void LineCap_Round_GeneratesLineRoundStyle()
    {
        var project = new HmiProject { Name = "VerifyLineCap", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        scr.Controls.Add(new HmiControl
        {
            Id = "line_round", Name = "LR", Type = HmiControlType.Line,
            X = 10, Y = 10, Width = 120, Height = 2, LineCap = "Round", BgColor = "#ff0000"
        });
        scr.Controls.Add(new HmiControl
        {
            Id = "line_flat", Name = "LF", Type = HmiControlType.Line,
            X = 10, Y = 20, Width = 120, Height = 2, LineCap = "Butt"
        });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        // lv_line 默认不可见：必须生成端点坐标/颜色/宽度（设计器语义：左上→右下对角线）
        Assert.Contains("lv_point_precise_t _pts_line_round[]", code);
        CSource.AssertCode(code, "lv_line_set_points(obj_line_round, _pts_line_round, 2);");
        CSource.AssertCode(code, "lv_obj_set_style_line_color(obj_line_round, lv_color_hex(0xff0000), 0);");
        CSource.AssertCode(code, "lv_obj_set_style_line_width(obj_line_round, 2, 0);");
        // Round → lv_obj_set_style_line_rounded(true)
        CSource.AssertCode(code, "lv_obj_set_style_line_rounded(obj_line_round, true, 0)");
        // Butt → 不生成 line_rounded（平头为 LVGL 默认）
        CSource.AssertNoCode(code, "line_rounded(obj_line_flat");
    }

    [Fact]
    public void ScreenBgColor_NotOverriddenByThemeStyles()
    {
        var project = new HmiProject { Name = "VerifyBg", LvglVersion = "9.5" };
        var scr = new HmiScreen
        {
            Id = "scr1", Name = "Main", Width = 800, Height = 480,
            BgColor = "#101418" // 用户自定义屏幕背景色
        };
        project.Screens.Add(scr);

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        // create_screen 中按用户设置生成背景色
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(screen, lv_color_hex(0x101418), 0)");
        // apply_theme_styles 不再覆盖屏幕背景色（保留文本样式）
        // 只截取 apply_theme_styles 函数体（下一个 "static lv_obj_t*" 前）
        var themeStart = code.IndexOf("void apply_theme_styles");
        var themeEnd = code.IndexOf("static lv_obj_t*", themeStart);
        var themeBlock = themeEnd > themeStart
            ? code.Substring(themeStart, themeEnd - themeStart)
            : code.Substring(themeStart);
        Assert.DoesNotContain("lv_obj_set_style_bg_color", themeBlock);
        Assert.Contains("lv_obj_set_style_text_color", themeBlock);
    }

    [Fact]
    public void SliderBar_TransparentTrackWhenNoBgColor()
    {
        var project = new HmiProject { Name = "VerifySb", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        // Slider/Bar 未配置 BgColor：轨道透明（与设计器一致，仅显示进度条）
        scr.Controls.Add(new HmiControl
        {
            Id = "sld1", Name = "S1", Type = HmiControlType.Slider,
            X = 10, Y = 10, Width = 300, Height = 20, MinValue = 0, MaxValue = 100, Value = 50
        });
        scr.Controls.Add(new HmiControl
        {
            Id = "bar1", Name = "B1", Type = HmiControlType.Bar,
            X = 10, Y = 40, Width = 300, Height = 20, MinValue = 0, MaxValue = 100, Value = 80
        });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        // 未配置 BgColor → LV_PART_MAIN 轨道透明，且不得用主题 SurfaceColor 填充
        CSource.AssertCode(code, "lv_obj_set_style_bg_opa(obj_sld1, LV_OPA_TRANSP, LV_PART_MAIN);");
        CSource.AssertCode(code, "lv_obj_set_style_bg_opa(obj_bar1, LV_OPA_TRANSP, LV_PART_MAIN);");
    }

    [Fact]
    public void SliderBar_IndicatorFallsBackToTextColor_AndKnobMatches()
    {
        var project = new HmiProject { Name = "VerifySb2", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        // 只设 TextColor：指示器必须回退 TextColor（与设计器 IndicatorColor ?? TextColor 语义一致）
        scr.Controls.Add(new HmiControl
        {
            Id = "sld_red", Name = "SR", Type = HmiControlType.Slider,
            X = 10, Y = 10, Width = 300, Height = 20, MinValue = 0, MaxValue = 100, Value = 50,
            TextColor = "#ff0000"
        });
        // 设 IndicatorColor + BgColor + Radius：用户颜色必须生效，不得被主题 SurfaceColor 覆盖
        scr.Controls.Add(new HmiControl
        {
            Id = "sld_grn", Name = "SG", Type = HmiControlType.Slider,
            X = 10, Y = 40, Width = 300, Height = 20, MinValue = 0, MaxValue = 100, Value = 30,
            BgColor = "#1e293b", IndicatorColor = "#22c55e", Radius = 4
        });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        // TextColor 兜底 → INDICATOR 红色
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_sld_red, lv_color_hex(0xff0000), LV_PART_INDICATOR);");
        // Slider knob 颜色与指示器一致
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_sld_red, lv_color_hex(0xff0000), LV_PART_KNOB);");
        // 用户 BgColor 生效（0x1e293b），轨道圆角尊重用户 Radius=4，指示器绿色
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_sld_grn, lv_color_hex(0x1e293b), 0);");
        CSource.AssertCode(code, "lv_obj_set_style_radius(obj_sld_grn, 4, 0);");
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_sld_grn, lv_color_hex(0x22c55e), LV_PART_INDICATOR);");
        // 未配置 BgColor 的 slider 不得生成主题 SurfaceColor 轨道（防覆盖回归）
        Assert.DoesNotContain("theme.SurfaceColor", code.Replace("//", "").Split("obj_sld_grn")[0]);
    }

    [Fact]
    public void CheckBox_IndicatorColor_AppliedToCheckedState()
    {
        var project = new HmiProject { Name = "VerifyCb", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        scr.Controls.Add(new HmiControl
        {
            Id = "cb1", Name = "CB1", Type = HmiControlType.CheckBox,
            X = 10, Y = 10, Width = 150, Height = 30, IsChecked = true,
            Text = "启用", IndicatorColor = "#22c55e", TextColor = "#ffffff"
        });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        // CheckBox 勾选框颜色：普通 INDICATOR + Checked 态双选择器（否则被主题蓝覆盖）
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_cb1, lv_color_hex(0x22c55e), LV_PART_INDICATOR);");
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_cb1, lv_color_hex(0x22c55e), LV_PART_INDICATOR | LV_STATE_CHECKED);");
        // TextColor 兜底（无 IndicatorColor 时）
        scr.Controls.Clear();
        scr.Controls.Add(new HmiControl
        {
            Id = "cb2", Name = "CB2", Type = HmiControlType.CheckBox,
            X = 10, Y = 50, Width = 150, Height = 30, IsChecked = true,
            Text = "自动", TextColor = "#ff0000"
        });
        var code2 = gen.GenerateSimulatorModule(project, null);
        CSource.AssertCode(code2, "lv_obj_set_style_bg_color(obj_cb2, lv_color_hex(0xff0000), LV_PART_INDICATOR | LV_STATE_CHECKED);");
    }

    [Fact]
    public void TextArea_UsesThemeSurfaceColor_WhenNoBgColor()
    {
        var project = new HmiProject { Name = "VerifyTa", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        scr.Controls.Add(new HmiControl
        {
            Id = "ta1", Name = "TA1", Type = HmiControlType.TextArea,
            X = 10, Y = 10, Width = 200, Height = 60, PlaceholderText = "请输入...",
            TextColor = "#e2e8f0"
        });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        // 未设 BgColor：使用主题 SurfaceColor 背景（避免 LVGL 浅色主题白底与设计器深色不一致）
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_ta1, lv_color_hex(0x1a2332), 0);");
        CSource.AssertCode(code, "lv_obj_set_style_bg_opa(obj_ta1, 255, 0);");
    }

    [Fact]
    public void Gauge_Generates270DegreeArcWithCenterLabel()
    {
        var project = new HmiProject { Name = "VerifyGauge", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        scr.Controls.Add(new HmiControl
        {
            Id = "g1", Name = "G1", Type = HmiControlType.Gauge,
            X = 10, Y = 10, Width = 180, Height = 180,
            MinValue = 0, MaxValue = 100, Value = 65,
            Text = "65", IndicatorColor = "#22c55e", MajorTickCount = 6
        });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        // 270° 弧形态：rotation=135 + bg_angles(0,270)（设计器 135° 起始 270° 弧）
        CSource.AssertCode(code, "lv_arc_set_rotation(obj_g1, 135);");
        CSource.AssertCode(code, "lv_arc_set_bg_angles(obj_g1, 0, 270);");
        // 指示弧颜色
        CSource.AssertCode(code, "lv_obj_set_style_arc_color(obj_g1, lv_color_hex(0x22c55e), LV_PART_INDICATOR);");
        // Gauge 隐藏 knob + 中心数值 label
        CSource.AssertCode(code, "lv_obj_set_style_opa(obj_g1, LV_OPA_TRANSP, LV_PART_KNOB);");
        CSource.AssertCode(code, "lv_label_create(obj_g1)");
        // 不填充中心背景（BgColor 用于弧色而非方块底色）
        CSource.AssertNoCode(code, "lv_obj_set_style_bg_opa(obj_g1, LV_OPA_COVER, 0);");
    }

    [Fact]
    public void BoundControls_GenerateSimRefreshBindings()
    {
        var project = new HmiProject { Name = "VerifyBind", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        // Label + Bar 绑定同一变量
        scr.Controls.Add(new HmiControl
        {
            Id = "lbl_t", Name = "温度", Type = HmiControlType.Label,
            X = 10, Y = 10, Width = 200, Height = 40, Text = "温度: 0℃"
        });
        scr.Controls[0].VariableBindings.Add(new VariableBindingData
        {
            VariableId = "var_t", Direction = BindingDirection.ReadOnly, PropertyName = "Text"
        });
        scr.Controls.Add(new HmiControl
        {
            Id = "bar_t", Name = "温度柱", Type = HmiControlType.Bar,
            X = 10, Y = 60, Width = 300, Height = 24, MinValue = 0, MaxValue = 100,
            IndicatorColor = "#ef4444"
        });
        scr.Controls[1].VariableBindings.Add(new VariableBindingData
        {
            VariableId = "var_t", Direction = BindingDirection.ReadOnly, PropertyName = "Value"
        });

        // 变量表（供 Unit 解析）
        var varMgr = project.VariableManager;
        var conn = new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" };
        varMgr.Connections.Add(conn);
        varMgr.Variables.Add(new Variable
        {
            Id = "var_t", Name = "温度", Address = "40001", DataType = "Float",
            ConnectionId = "c1", Unit = "℃"
        });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        // sim_refresh_bindings 生成：Label 文本刷新 + Bar 值刷新（同一变量多控件逐个刷新）
        CSource.AssertCode(code, "void sim_refresh_bindings(uint32_t tick)");
        // 统一语义：小数位来自 ValueDecimalPlaces（默认 2），与设计器预览一致
        CSource.AssertCode(code, "lv_label_set_text_fmt(obj_lbl_t, \"%.2f℃\", ((double)(v)));");
        CSource.AssertCode(code, "lv_bar_set_value(obj_bar_t, (int32_t)v, LV_ANIM_OFF);");
        CSource.AssertCode(code, "lv_obj_invalidate(obj_lbl_t);");
    }

    [Fact]
    public void TagTable_UsesValidCFloatLiterals()
    {
        var project = new HmiProject { Name = "VerifyTagFloat", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);
        scr.Controls.Add(new HmiControl
        {
            Id = "lbl1", Name = "L1", Type = HmiControlType.Label,
            X = 10, Y = 10, Width = 200, Height = 40, Text = "0"
        });
        scr.Controls[0].VariableBindings.Add(new VariableBindingData
        {
            VariableId = "v1", Direction = BindingDirection.ReadOnly, PropertyName = "Text"
        });
        var varMgr = project.VariableManager;
        var conn = new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" };
        varMgr.Connections.Add(conn);
        varMgr.Variables.Add(new Variable
        {
            Id = "v1", Name = "V1", Address = "40001", DataType = "Float", ConnectionId = "c1",
            Scale = 1, Offset = 0
        });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        // C 浮点字面量合法：禁止 "50f" / "1f"（整数常量 f 后缀非法）
        Assert.Contains("1.0f, 0.0f", code);
        // 模拟刷新引用 TAG 常量（防未使用警告）
        CSource.AssertCode(code, "(void)TAG_v1;");
        // 模拟刷新中的正弦表达式使用合法字面量
        Assert.Matches(@"float v = \d+\.\d+f \+ \d+\.\d+f \* sinf", code);
    }

    [Fact]
    public void CFloat_SmallFractionRange_GeneratesValidLiterals()
    {
        // 回归防护：旧 CFloat 用 "0.##" 格式化，非整数 < 0.005 会被四舍五入成 "0" → 生成 "0f" 非法 C 字面量。
        // 触发路径：Slider/Bar 绑定控件的 MaxValue-MinValue 为小数值（如 0.003）时 sim_refresh_bindings 的 amp/除数。
        var project = new HmiProject { Name = "VerifySmallFloat", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);
        scr.Controls.Add(new HmiControl
        {
            Id = "sld1", Name = "S1", Type = HmiControlType.Slider,
            X = 10, Y = 10, Width = 300, Height = 30,
            MinValue = 0, MaxValue = 0.003
        });
        scr.Controls[0].VariableBindings.Add(new VariableBindingData
        {
            VariableId = "v1", Direction = BindingDirection.ReadOnly, PropertyName = "Value"
        });
        var varMgr = project.VariableManager;
        var conn = new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" };
        varMgr.Connections.Add(conn);
        varMgr.Variables.Add(new Variable
        {
            Id = "v1", Name = "V1", Address = "40001", DataType = "Float", ConnectionId = "c1",
            Scale = 1, Offset = 0
        });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        // 正弦幅度/除数必须是含小数点的合法 C 浮点字面量（0.003f 而非 0f）
        Assert.Matches(@"float v = \d+\.\d+f \+ \d+\.\d+f \* sinf", code);
        Assert.Contains("0.003f", code);
        Assert.DoesNotContain("/ 0f ", code);
    }

    [Fact]
    public void BoundControls_BooleanRefresh_IsGenerated()
    {
        var project = new HmiProject { Name = "VerifyBoolBind", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "Main", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        // Led + Switch + CheckBox 各绑定同一 bool 变量
        scr.Controls.Add(new HmiControl
        {
            Id = "led1", Name = "运行灯", Type = HmiControlType.Led,
            X = 10, Y = 10, Width = 24, Height = 24, MinValue = 0, MaxValue = 1
        });
        scr.Controls[0].VariableBindings.Add(new VariableBindingData
        {
            VariableId = "v_run", Direction = BindingDirection.ReadOnly, PropertyName = "Value"
        });
        scr.Controls.Add(new HmiControl
        {
            Id = "sw1", Name = "开关", Type = HmiControlType.Switch,
            X = 50, Y = 10, Width = 48, Height = 24, MinValue = 0, MaxValue = 1
        });
        scr.Controls[1].VariableBindings.Add(new VariableBindingData
        {
            VariableId = "v_run", Direction = BindingDirection.ReadWrite, PropertyName = "Value"
        });
        scr.Controls.Add(new HmiControl
        {
            Id = "cb1", Name = "勾选", Type = HmiControlType.CheckBox,
            X = 120, Y = 10, Width = 80, Height = 24, MinValue = 0, MaxValue = 1
        });
        scr.Controls[2].VariableBindings.Add(new VariableBindingData
        {
            VariableId = "v_run", Direction = BindingDirection.ReadWrite, PropertyName = "Value"
        });

        // Arc（Gauge/Knob/Arc 同用 lv_arc_set_value）
        scr.Controls.Add(new HmiControl
        {
            Id = "arc1", Name = "弧", Type = HmiControlType.Arc,
            X = 220, Y = 10, Width = 48, Height = 48
        });
        scr.Controls[3].VariableBindings.Add(new VariableBindingData
        {
            VariableId = "v_arc", Direction = BindingDirection.ReadOnly, PropertyName = "Value"
        });

        var varMgr = project.VariableManager;
        var conn = new DeviceConnection { Id = "c1", Name = "PLC", DriverId = "modbus-tcp" };
        varMgr.Connections.Add(conn);
        varMgr.Variables.Add(new Variable
        {
            Id = "v_run", Name = "运行", Address = "00001", DataType = "Bool", ConnectionId = "c1"
        });
        varMgr.Variables.Add(new Variable
        {
            Id = "v_arc", Name = "弧值", Address = "40002", DataType = "Float", ConnectionId = "c1"
        });

        var gen = new CodeGenerator();
        var code = gen.GenerateSimulatorModule(project, null);

        CSource.AssertCode(code, "void sim_refresh_bindings(uint32_t tick)");
        // 布尔判定统一为“非零即 ON”（BindingSemantics.BoolThreshold）
        CSource.AssertCode(code, "lv_led_set_brightness(obj_led1, (v) != 0.0f ? 255 : 0);");
        CSource.AssertCode(code, "lv_obj_add_state(obj_sw1, LV_STATE_CHECKED);");
        CSource.AssertCode(code, "lv_obj_remove_state(obj_sw1, LV_STATE_CHECKED);");
        CSource.AssertCode(code, "lv_obj_add_state(obj_cb1, LV_STATE_CHECKED);");
        CSource.AssertCode(code, "lv_obj_remove_state(obj_cb1, LV_STATE_CHECKED);");
        CSource.AssertCode(code, "lv_arc_set_value(obj_arc1, (int32_t)v);");
    }
}
