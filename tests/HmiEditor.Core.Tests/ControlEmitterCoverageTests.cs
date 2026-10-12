using HmiEditor.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 控件发射器**输出覆盖**：拆 <c>CodeGenerator</c> 前补的回归网。
///
/// <para><b>为什么单独建这一类：</b>盘点发现生成器的许多发射路径<b>零输出断言</b> ——
/// 用「API 锚点」法（从每个顶层成员体内抽 <c>lv_*</c>/<c>hmi_*</c>/<c>LV_*</c> 名，
/// 再看测试语料里是否出现）扫出：<c>lv_label_set_long_mode</c>、
/// <c>lv_spinner_set_anim_params</c>、<c>lv_tabview_*</c>、<c>lv_dropdown_set_selected</c>、
/// <c>LV_IMAGE_ALIGN_COVER</c>、<c>lv_bar_set_mode</c>、<c>lv_slider_set_mode</c>、
/// <c>lv_table_set_row_count</c>、<c>lv_arc_set_range</c>、<c>lv_obj_set_style_arc_opa</c>、
/// <c>lv_textarea_set_max_length</c>、<c>lv_display_get_vertical_resolution</c> 等
/// 在测试里<b>一次都没出现</b>。</para>
///
/// <para>这些发射器都是可达的（<c>GenerateControlParameters</c> 按 <c>control.Type</c> 分发），
/// 缺口集中在<b>条件分支</b>：只在某个配置字段非空/大于零时才发射。
/// 既有测试没喂那些配置，所以分支从没被执行过 —— 正是「唯一有分支的逻辑没被测过」。</para>
///
/// <para>顺带钉住一个盘点发现（**已清**）：<c>LV_IMAGE_ALIGN_COVER</c> 只出现在守卫条件里、
/// <b>从不被产出</b>（<c>StretchModeKind</c> 没有 Cover，<c>StretchModeMap.Parse("cover")</c>
/// 落默认 Stretch）→ 不可达死代码，无 UI 暴露故非用户可见缺陷。
/// 该字符串比较分支已改为按 <c>StretchModeKind</c> 的枚举判断并删除不可达半支；
/// 现由 <c>Image_CoverString_…</c> 与 <c>Image_EveryStretchModeInput_…</c>（全输入包络）守住。</para>
///
/// <para>本类的目的是让后续「按域拆 partial」的纯搬移有行为网兜底；
/// 不是替代产物逐字节等价校验（那个更硬，见 docs）。</para>
/// </summary>
public class ControlEmitterCoverageTests
{
    // ───────────────────────── fixture ─────────────────────────

    private static HmiControl Ctl(HmiControlType type, string id, Action<HmiControl>? cfg = null)
    {
        var c = new HmiControl
        {
            Id = id, Name = id, Type = type,
            X = 10, Y = 20, Width = 100, Height = 40,
        };
        cfg?.Invoke(c);
        return c;
    }

    private static string GenScreen(HmiTheme? theme, params HmiControl[] controls)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 1024, Height = 600 };
        foreach (var c in controls) screen.Controls.Add(c);
        return new CodeGenerator().GenerateScreenCode(screen, theme);
    }

    private static string GenScreen(params HmiControl[] controls) => GenScreen(null, controls);

    private static string GenScreenWith(HmiScreen screen, HmiTheme? theme = null)
        => new CodeGenerator().GenerateScreenCode(screen, theme);

    // ───────────────────────── Label：长文本模式映射 ─────────────────────────

    [Theory]
    [InlineData("Dots", "LV_LABEL_LONG_DOT")]
    [InlineData("Scroll", "LV_LABEL_LONG_SCROLL")]
    [InlineData("ScrollCircular", "LV_LABEL_LONG_SCROLL_CIRCULAR")]
    [InlineData("Clip", "LV_LABEL_LONG_CLIP")]
    [InlineData("Wrap", "LV_LABEL_LONG_WRAP")]
    [InlineData("UnknownMode", "LV_LABEL_LONG_WRAP")]   // 未知值 → 默认换行
    public void Label_LongMode_MapsToLvglV9Enum(string mode, string expected)
    {
        var code = GenScreen(Ctl(HmiControlType.Label, "l1", c => c.LabelLongMode = mode));
        CSource.AssertCode(code, $"lv_label_set_long_mode(obj_l1, {expected});");
    }

    [Fact]
    public void Label_NoLongMode_EmitsNoLongModeCall()
    {
        var code = GenScreen(Ctl(HmiControlType.Label, "l1"));
        CSource.AssertNoCode(code, "lv_label_set_long_mode(obj_l1, LV_LABEL_LONG_WRAP);");
    }

    // ───────────────────────── Spinner ─────────────────────────

    [Fact]
    public void Spinner_DefaultSpinSpeed_EmitsAnimParamsWithSixtyDegreeArc()
    {
        var code = GenScreen(Ctl(HmiControlType.Spinner, "sp1"));   // SpinSpeed 默认 1000
        CSource.AssertCode(code, "lv_spinner_set_anim_params(obj_sp1, 1000, 60);");
    }

    [Theory]
    [InlineData(10, 100)]    // 低于下限 → 抬到 100
    [InlineData(0, 100)]
    [InlineData(100, 100)]
    [InlineData(250, 250)]
    public void Spinner_SpinSpeed_ClampedToFloorOf100(int configured, int expected)
    {
        var code = GenScreen(Ctl(HmiControlType.Spinner, "sp1", c => c.SpinSpeed = configured));
        CSource.AssertCode(code, $"lv_spinner_set_anim_params(obj_sp1, {expected}, 60);");
    }

    // ───────────────────────── TabView ─────────────────────────

    [Fact]
    public void TabView_Titles_SplitIntoAddTabCalls()
    {
        var code = GenScreen(Ctl(HmiControlType.TabView, "tv1", c => c.TabTitles = "参数,曲线,报警"));
        CSource.AssertCode(code, "lv_tabview_add_tab(obj_tv1, \"参数\");");
        CSource.AssertCode(code, "lv_tabview_add_tab(obj_tv1, \"曲线\");");
        CSource.AssertCode(code, "lv_tabview_add_tab(obj_tv1, \"报警\");");
    }

    [Fact]
    public void TabView_Titles_AreTrimmedPerEntry()
    {
        var code = GenScreen(Ctl(HmiControlType.TabView, "tv1", c => c.TabTitles = "  A , B  ,C "));
        CSource.AssertCode(code, "lv_tabview_add_tab(obj_tv1, \"A\");");
        CSource.AssertCode(code, "lv_tabview_add_tab(obj_tv1, \"B\");");
        CSource.AssertCode(code, "lv_tabview_add_tab(obj_tv1, \"C\");");
    }

    [Fact]
    public void TabView_NoTitles_EmitsNoAddTab()
    {
        var code = GenScreen(Ctl(HmiControlType.TabView, "tv1"));
        CSource.AssertNoText(code, "lv_tabview_add_tab(obj_tv1,");
    }

    [Fact]
    public void TabView_SelectedIndexZero_EmitsNoSetActive()
    {
        var code = GenScreen(Ctl(HmiControlType.TabView, "tv1", c => c.TabTitles = "A,B"));
        CSource.AssertNoText(code, "lv_tabview_set_active(obj_tv1,");
    }

    [Fact]
    public void TabView_SelectedIndexPositive_EmitsSetActiveWithAnimOff()
    {
        var code = GenScreen(Ctl(HmiControlType.TabView, "tv1", c => { c.TabTitles = "A,B,C"; c.SelectedIndex = 2; }));
        CSource.AssertCode(code, "lv_tabview_set_active(obj_tv1, 2, LV_ANIM_OFF);");
    }

    // ───────────────────────── Dropdown ─────────────────────────

    [Fact]
    public void Dropdown_EmitsSetSelected_EvenWhenIndexIsZero()
    {
        var code = GenScreen(Ctl(HmiControlType.Dropdown, "dd1"));
        CSource.AssertCode(code, "lv_dropdown_set_selected(obj_dd1, 0);");
    }

    [Fact]
    public void Dropdown_SelectedIndex_IsEmittedAsConfigured()
    {
        var code = GenScreen(Ctl(HmiControlType.Dropdown, "dd1", c => c.SelectedIndex = 3));
        CSource.AssertCode(code, "lv_dropdown_set_selected(obj_dd1, 3);");
    }

    [Fact]
    public void Dropdown_Options_EmitsSetOptions()
    {
        var code = GenScreen(Ctl(HmiControlType.Dropdown, "dd1", c => c.Options = "手动,自动,检修"));
        CSource.AssertCode(code, "lv_dropdown_set_options(obj_dd1, \"手动,自动,检修\");");
    }

    [Fact]
    public void Dropdown_NoOptions_EmitsNoSetOptions()
    {
        var code = GenScreen(Ctl(HmiControlType.Dropdown, "dd1"));
        CSource.AssertNoText(code, "lv_dropdown_set_options(obj_dd1,");
    }

    [Fact]
    public void Dropdown_ListFont_InheritsFromMainPart()
    {
        // 下拉弹窗 list 是独立对象，不设置会继承 screen 默认字体 → 中文变方框
        var code = GenScreen(Ctl(HmiControlType.Dropdown, "dd1"));
        CSource.AssertCode(code,
            "lv_obj_set_style_text_font(lv_dropdown_get_list(obj_dd1), lv_obj_get_style_text_font(obj_dd1, 0), 0);");
    }

    [Fact]
    public void Dropdown_IndicatorFont_UsesBuiltinSymbolFont()
    {
        // LV_SYMBOL_DOWN 是 PUA 符号，中文字体无该字形；指示器改用内置符号字体
        var code = GenScreen(Ctl(HmiControlType.Dropdown, "dd1"));
        CSource.AssertCode(code, "lv_obj_set_style_text_font(obj_dd1, LV_FONT_DEFAULT, LV_PART_INDICATOR);");
    }

    [Fact]
    public void Dropdown_NoBgColor_UsesThemeSurfaceColors()
    {
        var theme = new HmiTheme();
        var code = GenScreen(theme, Ctl(HmiControlType.Dropdown, "dd1"));

        CSource.AssertCode(code, $"lv_obj_set_style_bg_color(obj_dd1, lv_color_hex(0x{theme.SurfaceColor.TrimStart('#')}), 0);");
        CSource.AssertCode(code, "lv_obj_set_style_bg_opa(obj_dd1, 255, 0);");
        CSource.AssertCode(code, $"lv_obj_set_style_radius(obj_dd1, {theme.CornerRadius}, 0);");
        CSource.AssertCode(code, $"lv_obj_set_style_border_width(obj_dd1, {theme.BorderWidth}, 0);");
        CSource.AssertCode(code, $"lv_obj_set_style_border_color(obj_dd1, lv_color_hex(0x{theme.BorderColor.TrimStart('#')}), 0);");
    }

    [Fact]
    public void Dropdown_ExplicitBgColor_SkipsThemeFallback()
    {
        var theme = new HmiTheme();
        var code = GenScreen(theme, Ctl(HmiControlType.Dropdown, "dd1", c => c.BgColor = "#123456"));
        CSource.AssertNoText(code, $"lv_obj_set_style_bg_color(obj_dd1, lv_color_hex(0x{theme.SurfaceColor.TrimStart('#')}), 0);");
    }

    [Fact]
    public void Dropdown_NoTheme_EmitsNoBgColorAtAll()
    {
        var code = GenScreen(Ctl(HmiControlType.Dropdown, "dd1"));
        CSource.AssertNoText(code, "lv_obj_set_style_bg_color(obj_dd1,");
    }

    // ───────────────────────── Image：LVGL 版本兜底 ─────────────────────────

    [Fact]
    public void Image_Contain_EmitsVersionGuardWithStretchFallback()
    {
        // CONTAIN 是 LVGL 9.3 才有的枚举值（枚举非宏，不能用 #if defined 判断）；
        // < 9.3 必须回退 STRETCH，否则编译报错
        var code = GenScreen(Ctl(HmiControlType.Image, "i1", c => c.StretchMode = "Contain"));

        CSource.AssertText(code, "#if LV_VERSION_CHECK(9, 3, 0)");
        CSource.AssertCode(code, "lv_image_set_inner_align(obj_i1, LV_IMAGE_ALIGN_CONTAIN);");
        CSource.AssertText(code, "#else");
        CSource.AssertText(code,
            "lv_image_set_inner_align(obj_i1, LV_IMAGE_ALIGN_STRETCH); /* fallback: LVGL < 9.3 无 LV_IMAGE_ALIGN_CONTAIN */");
        CSource.AssertText(code, "#endif");
    }

    [Fact]
    public void Image_CoverString_FallsBackToPlainStretch_BecauseMapHasNoCoverKind()
    {
        // 钉住事实：StretchModeMap.Parse("cover") 不在 switch 里 → 落默认 StretchModeKind.Stretch
        // → LV_IMAGE_ALIGN_STRETCH，且不发射版本兜底。
        //
        // 这里曾是死代码的现场：EmitImageParameters 用「生成出来的字符串」做分支，
        // 其中 COVER 那半**永不可达**（LvglImageAlign 只返回 STRETCH/CONTAIN/TILE/CENTER）。
        // 现在分支按 StretchModeKind 判（枚举、编译器可查），不可达的那半已删。
        // 将来若给 StretchModeKind 增加 Cover，本用例会先红 —— 逼作者同时决定版本兜底与 UI 选项。
        var code = GenScreen(Ctl(HmiControlType.Image, "i1", c => c.StretchMode = "Cover"));

        CSource.AssertCode(code, "lv_image_set_inner_align(obj_i1, LV_IMAGE_ALIGN_STRETCH);");
        CSource.AssertNoText(code, "#if LV_VERSION_CHECK(9, 3, 0)");
        CSource.AssertNoText(code, "LV_IMAGE_ALIGN_COVER");
    }

    [Theory]
    [InlineData(null, "LV_IMAGE_ALIGN_STRETCH", false)]
    [InlineData("", "LV_IMAGE_ALIGN_STRETCH", false)]
    [InlineData("Fill", "LV_IMAGE_ALIGN_STRETCH", false)]
    [InlineData("Stretch", "LV_IMAGE_ALIGN_STRETCH", false)]
    [InlineData("None", "LV_IMAGE_ALIGN_CENTER", false)]
    [InlineData("Center", "LV_IMAGE_ALIGN_CENTER", false)]
    [InlineData("Tile", "LV_IMAGE_ALIGN_TILE", false)]
    [InlineData("Cover", "LV_IMAGE_ALIGN_STRETCH", false)]
    [InlineData("cover", "LV_IMAGE_ALIGN_STRETCH", false)]
    [InlineData("解析不了的模式", "LV_IMAGE_ALIGN_STRETCH", false)]
    [InlineData("Contain", "LV_IMAGE_ALIGN_CONTAIN", true)]
    [InlineData("contain", "LV_IMAGE_ALIGN_CONTAIN", true)]
    public void Image_EveryStretchModeInput_EmitsExactAlign_AndGuardShapeMatches(
        string? mode, string align, bool expectVersionGuard)
    {
        // 全输入包络：所有可解析/不可解析的输入 → 发射的对齐宏 + 版本兜底形状。
        // 只有 CONTAIN 需要 9.3 版本兜底（LV_IMAGE_ALIGN_CONTAIN 是 v9.3 才有的枚举值）。
        var code = GenScreen(Ctl(HmiControlType.Image, "i1", c => c.StretchMode = mode));

        CSource.AssertCode(code, $"lv_image_set_inner_align(obj_i1, {align});");
        if (expectVersionGuard)
        {
            CSource.AssertText(code, "#if LV_VERSION_CHECK(9, 3, 0)");
            CSource.AssertText(code, "#else");
        }
        else
        {
            CSource.AssertNoText(code, "#if LV_VERSION_CHECK(9, 3, 0)");
        }
    }

    // ───────────────────────── Bar / Slider：ValueMode ─────────────────────────

    [Theory]
    [InlineData("normal", "LV_BAR_MODE_NORMAL")]
    [InlineData("symmetrical", "LV_BAR_MODE_SYMMETRICAL")]
    [InlineData("range", "LV_BAR_MODE_RANGE")]
    public void Bar_ValueMode_MapsToUppercasedLvglMacro(string mode, string expected)
    {
        var code = GenScreen(Ctl(HmiControlType.Bar, "b1", c => { c.ValueMode = mode; c.MinValue = 0; c.MaxValue = 100; }));
        CSource.AssertCode(code, $"lv_bar_set_mode(obj_b1, {expected});");
    }

    [Fact]
    public void Bar_NoValueMode_EmitsNoSetMode()
    {
        var code = GenScreen(Ctl(HmiControlType.Bar, "b1"));
        CSource.AssertNoText(code, "lv_bar_set_mode(obj_b1,");
    }

    [Theory]
    [InlineData("normal", "LV_SLIDER_MODE_NORMAL")]
    [InlineData("range", "LV_SLIDER_MODE_RANGE")]
    public void Slider_ValueMode_MapsToUppercasedLvglMacro(string mode, string expected)
    {
        var code = GenScreen(Ctl(HmiControlType.Slider, "sl1", c => c.ValueMode = mode));
        CSource.AssertCode(code, $"lv_slider_set_mode(obj_sl1, {expected});");
    }

    [Fact]
    public void Slider_NoValueMode_EmitsNoSetMode()
    {
        var code = GenScreen(Ctl(HmiControlType.Slider, "sl1"));
        CSource.AssertNoText(code, "lv_slider_set_mode(obj_sl1,");
    }

    // ───────────────────────── Table：行列数 ─────────────────────────

    [Fact]
    public void Table_RowAndColumnCount_AreEmitted()
    {
        var code = GenScreen(Ctl(HmiControlType.Table, "t1", c => { c.RowCount = 5; c.ColumnCount = 4; }));
        CSource.AssertCode(code, "lv_table_set_row_count(obj_t1, 5);");
        CSource.AssertCode(code, "lv_table_set_column_count(obj_t1, 4);");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void Table_NonPositiveCounts_AreClampedToOne(int configured, int expected)
    {
        var code = GenScreen(Ctl(HmiControlType.Table, "t1", c => { c.RowCount = configured; c.ColumnCount = configured; }));
        CSource.AssertCode(code, $"lv_table_set_row_count(obj_t1, {expected});");
        CSource.AssertCode(code, $"lv_table_set_column_count(obj_t1, {expected});");
    }

    [Fact]
    public void Table_HeaderTexts_AreEmittedAsRowZeroCells()
    {
        var code = GenScreen(Ctl(HmiControlType.Table, "t1", c =>
        {
            c.RowCount = 3; c.ColumnCount = 2; c.HeaderTexts = "温度,压力";
        }));
        CSource.AssertCode(code, "lv_table_set_cell_value(obj_t1, 0, 0, \"温度\");");
        CSource.AssertCode(code, "lv_table_set_cell_value(obj_t1, 0, 1, \"压力\");");
    }

    // ───────────────────────── Arc：角度范围与退化区间 ─────────────────────────

    [Fact]
    public void Arc_AnglesAndRange_AreEmitted()
    {
        var code = GenScreen(Ctl(HmiControlType.Arc, "a1", c => { c.MinValue = 0; c.MaxValue = 270; c.Value = 90; c.BorderWidth = 8; }));

        CSource.AssertCode(code, "lv_arc_set_rotation(obj_a1, 0);");
        CSource.AssertCode(code, "lv_arc_set_bg_angles(obj_a1, 0, 270);");
        CSource.AssertCode(code, "lv_arc_set_range(obj_a1, 0, 270);");
        CSource.AssertCode(code, "lv_arc_set_value(obj_a1, 90);");
        CSource.AssertCode(code, "lv_obj_set_style_arc_width(obj_a1, 8, LV_PART_MAIN);");
        CSource.AssertCode(code, "lv_obj_set_style_arc_width(obj_a1, 8, LV_PART_INDICATOR);");
    }

    [Fact]
    public void Arc_ZeroBorderWidth_FallsBackToArcLayoutDefault()
    {
        var code = GenScreen(Ctl(HmiControlType.Arc, "a1", c => { c.MinValue = 0; c.MaxValue = 270; c.BorderWidth = 0; }));
        CSource.AssertCode(code, $"lv_obj_set_style_arc_width(obj_a1, {ArcLayout.ArcWidthDefault}, LV_PART_MAIN);");
    }

    [Fact]
    public void Arc_DegenerateRange_KeepsMinStrictlyBelowMax()
    {
        // LVGL 要求 range 的 min < max；配置成 MIN==MAX 时必须抬到 MIN+1，否则断言失败/除零
        var code = GenScreen(Ctl(HmiControlType.Arc, "a1", c => { c.MinValue = 90; c.MaxValue = 90; }));
        CSource.AssertCode(code, "lv_arc_set_range(obj_a1, 90, 91);");
    }

    [Fact]
    public void Arc_OpaAndColors_FollowPaletteFallbackChain()
    {
        var code = GenScreen(Ctl(HmiControlType.Arc, "a1", c => { c.MinValue = 0; c.MaxValue = 270; }));

        CSource.AssertCode(code, "lv_obj_set_style_arc_opa(obj_a1, LV_OPA_COVER, LV_PART_MAIN);");
        CSource.AssertCode(code, "lv_obj_set_style_arc_opa(obj_a1, LV_OPA_COVER, LV_PART_INDICATOR);");
        // 轨道兜底 Surface、指示兜底 Primary（两者都未配置时）
        CSource.AssertCode(code, $"lv_obj_set_style_arc_color(obj_a1, lv_color_hex(0x{PaletteDefaults.Surface.TrimStart('#')}), LV_PART_MAIN);");
        CSource.AssertCode(code, $"lv_obj_set_style_arc_color(obj_a1, lv_color_hex(0x{PaletteDefaults.Primary.TrimStart('#')}), LV_PART_INDICATOR);");
        // 纯图形圆弧：隐藏 knob 与对象背景
        CSource.AssertCode(code, "lv_obj_set_style_opa(obj_a1, LV_OPA_TRANSP, LV_PART_KNOB);");
        CSource.AssertCode(code, "lv_obj_set_style_bg_opa(obj_a1, LV_OPA_TRANSP, 0);");
    }

    [Fact]
    public void Arc_IndicatorColor_OverridesTextColorAndPalette()
    {
        var code = GenScreen(Ctl(HmiControlType.Arc, "a1", c =>
        {
            c.MinValue = 0; c.MaxValue = 270;
            c.TextColor = "#111111";
            c.IndicatorColor = "#abcdef";
        }));
        CSource.AssertCode(code, "lv_obj_set_style_arc_color(obj_a1, lv_color_hex(0xabcdef), LV_PART_INDICATOR);");
    }

    // ───────────────────────── TextArea ─────────────────────────

    [Fact]
    public void TextArea_PlaceholderMaxLengthPassword_AreEmitted()
    {
        var code = GenScreen(Ctl(HmiControlType.TextArea, "ta1", c =>
        {
            c.PlaceholderText = "请输入";
            c.MaxLength = 16;
            c.PasswordMode = true;
        }));

        CSource.AssertCode(code, "lv_textarea_set_placeholder_text(obj_ta1, \"请输入\");");
        CSource.AssertCode(code, "lv_textarea_set_max_length(obj_ta1, 16);");
        CSource.AssertCode(code, "lv_textarea_set_password_mode(obj_ta1, true);");
    }

    [Fact]
    public void TextArea_ZeroMaxLengthAndNoPassword_EmitsNeither()
    {
        var code = GenScreen(Ctl(HmiControlType.TextArea, "ta1"));
        CSource.AssertNoText(code, "lv_textarea_set_max_length(obj_ta1,");
        CSource.AssertNoText(code, "lv_textarea_set_password_mode(obj_ta1,");
    }

    [Fact]
    public void TextArea_AlwaysClickFocusable_ForSoftKeyboard()
    {
        var code = GenScreen(Ctl(HmiControlType.TextArea, "ta1"));
        CSource.AssertCode(code, "lv_obj_add_flag(obj_ta1, LV_OBJ_FLAG_CLICK_FOCUSABLE);");
    }

    [Fact]
    public void TextArea_SimulatorNumberMode_MarksUserData()
    {
        // 模拟器模式无宿主符号：用 user_data=(void*)1 标记，模拟器软键盘按标记弹数字键盘
        var code = GenScreen(Ctl(HmiControlType.TextArea, "ta1", c => c.KeyboardMode = "Number"));
        CSource.AssertText(code, "lv_obj_set_user_data(obj_ta1, (void*)1);");
        CSource.AssertNoText(code, "hmi_set_ta_keyboard_mode(obj_ta1,");
    }

    [Fact]
    public void TextArea_NoKeyboardMode_EmitsNoUserDataMarker()
    {
        var code = GenScreen(Ctl(HmiControlType.TextArea, "ta1"));
        CSource.AssertNoText(code, "lv_obj_set_user_data(obj_ta1,");
    }

    [Fact]
    public void TextArea_NoBgColor_UsesThemeSurfaceColors()
    {
        var theme = new HmiTheme();
        var code = GenScreen(theme, Ctl(HmiControlType.TextArea, "ta1"));
        CSource.AssertCode(code, $"lv_obj_set_style_bg_color(obj_ta1, lv_color_hex(0x{theme.SurfaceColor.TrimStart('#')}), 0);");
        CSource.AssertCode(code, $"lv_obj_set_style_radius(obj_ta1, {theme.CornerRadius}, 0);");
    }

    // ───────────────────────── 通用参数：主题字号 / 字重 / 刻度 ─────────────────────────

    [Fact]
    public void Arc_WithShowTicks_AlsoEmitsTickLabels()
    {
        // ShowTicks 属条件分支：Arc 走 EmitArcParameters，刻度由 AppendSliderTicks 负责
        var code = GenScreen(Ctl(HmiControlType.Slider, "sl1", c =>
        {
            c.MinValue = 0; c.MaxValue = 100; c.StepValue = 25; c.ShowTicks = true;
        }));
        CSource.AssertCode(code, $"lv_obj_add_flag(obj_sl1, LV_OBJ_FLAG_OVERFLOW_VISIBLE);");
        CSource.AssertText(code, "lv_obj_align(_ticklbl_sl1_");
    }

    [Fact]
    public void Slider_NoShowTicks_EmitsNoTickLabels()
    {
        var code = GenScreen(Ctl(HmiControlType.Slider, "sl1", c => { c.MinValue = 0; c.MaxValue = 100; }));
        CSource.AssertNoText(code, "lv_obj_align(_ticklbl_sl1_");
    }

    // ───────────────────────── 自适应布局 ─────────────────────────

    private static HmiScreen ScreenWith(ScreenAdaptiveMode mode, params HmiControl[] controls)
    {
        var screen = new HmiScreen
        {
            Id = "s1", Name = "Main", Width = 1024, Height = 600,
            AdaptiveMode = mode,
        };
        foreach (var c in controls) screen.Controls.Add(c);
        return screen;
    }

    [Fact]
    public void AdaptiveLayout_None_EmitsNoFunction()
    {
        var code = GenScreenWith(ScreenWith(ScreenAdaptiveMode.None, Ctl(HmiControlType.Label, "l1")));
        CSource.AssertNoText(code, "adaptive_layout_s1");
    }

    [Fact]
    public void AdaptiveLayout_Fit_EmitsUniformScaleAndCenteringOffset()
    {
        var code = GenScreenWith(ScreenWith(ScreenAdaptiveMode.Fit, Ctl(HmiControlType.Label, "l1")));

        CSource.AssertText(code, "static void adaptive_layout_s1(lv_obj_t* screen)");
        CSource.AssertText(code, "lv_coord_t base_w = 1024, base_h = 600;");
        CSource.AssertText(code, "lv_coord_t act_w = lv_display_get_horizontal_resolution(lv_obj_get_display(screen));");
        CSource.AssertText(code, "lv_coord_t act_h = lv_display_get_vertical_resolution(lv_obj_get_display(screen));");
        CSource.AssertText(code, "float s = sx < sy ? sx : sy;  /* 等比缩放 */");
        // Fit 模式用 s 同时缩放位置与尺寸
        CSource.AssertText(code, "lv_obj_set_pos(obj_l1, (lv_coord_t)(10 * s + ox), (lv_coord_t)(20 * s + oy));");
        CSource.AssertText(code, "lv_obj_set_size(obj_l1, (lv_coord_t)(100 * s), (lv_coord_t)(40 * s));");
    }

    [Fact]
    public void AdaptiveLayout_Fill_EmitsZeroOffsetAndPerAxisScale()
    {
        var code = GenScreenWith(ScreenWith(ScreenAdaptiveMode.Fill, Ctl(HmiControlType.Label, "l1")));

        CSource.AssertText(code, "float ox = 0.0f, oy = 0.0f;  /* Fill: 拉伸铺满 */");
        CSource.AssertText(code, "lv_obj_set_pos(obj_l1, (lv_coord_t)(10 * sx + ox), (lv_coord_t)(20 * sy + oy));");
        CSource.AssertText(code, "lv_obj_set_size(obj_l1, (lv_coord_t)(100 * sx), (lv_coord_t)(40 * sy));");
    }

    [Fact]
    public void AdaptiveLayout_SkipsSvgAndEllipseControls()
    {
        // Svg/Ellipse 走 canvas，尺寸由绘制定义（像素缓冲固定），不能随分辨率重设。
        // 注意断言必须带 (lv_coord_t) 收敛到自适应函数体：控件创建处也有 lv_obj_set_size(obj_e1, ...)。
        var code = GenScreenWith(ScreenWith(ScreenAdaptiveMode.Fit,
            Ctl(HmiControlType.Label, "l1"),
            Ctl(HmiControlType.Image, "i1"),
            Ctl(HmiControlType.Ellipse, "e1")));

        CSource.AssertText(code, "lv_obj_set_size(obj_l1, (lv_coord_t)");
        CSource.AssertText(code, "lv_obj_set_size(obj_i1, (lv_coord_t)");
        CSource.AssertNoText(code, "lv_obj_set_size(obj_e1, (lv_coord_t)");
    }

    // ───────────────────────── 可见性 / 不透明度 ─────────────────────────

    [Fact]
    public void Control_Invisible_AddsHiddenFlag()
    {
        var code = GenScreen(Ctl(HmiControlType.Label, "l1", c => c.IsVisible = false));
        CSource.AssertCode(code, "lv_obj_add_flag(obj_l1, LV_OBJ_FLAG_HIDDEN);");
    }

    [Fact]
    public void Control_Opaque_EmitsNoOpacityCall()
    {
        var code = GenScreen(Ctl(HmiControlType.Label, "l1", c => c.Opacity = 100));
        CSource.AssertNoText(code, "lv_obj_set_style_opa(obj_l1,");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 127)]    // 注意 50*2.55 双精度是 127.4999…→ Round 得 127，不是直觉的 128
    [InlineData(99, 252)]
    public void Control_TranslucentOpacity_MapsPercentToOpa(int percent, int expected)
    {
        // 0-100 百分比 → lv_opa 0-255
        var code = GenScreen(Ctl(HmiControlType.Label, "l1", c => c.Opacity = percent));
        CSource.AssertCode(code, $"lv_obj_set_style_opa(obj_l1, {expected}, 0);");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(99.5)]   // 阈值边界：Opacity < 99.5 才发射
    public void Control_OpacityAtOrAboveThreshold_EmitsNoOpacityCall(double opacity)
    {
        var code = GenScreen(Ctl(HmiControlType.Label, "l1", c => c.Opacity = opacity));
        CSource.AssertNoText(code, "lv_obj_set_style_opa(obj_l1,");
    }

    // ───────────────────────── 指示器颜色（按控件族分支） ─────────────────────────

    [Fact]
    public void Slider_IndicatorColor_AlsoAppliesToKnob()
    {
        var code = GenScreen(Ctl(HmiControlType.Slider, "sl1", c => c.IndicatorColor = "#ff8800"));
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_sl1, lv_color_hex(0xff8800), LV_PART_INDICATOR);");
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_sl1, lv_color_hex(0xff8800), LV_PART_KNOB);");
    }

    [Fact]
    public void CheckBox_IndicatorColor_AlsoAppliesToCheckedSelector()
    {
        // 不设 CHECKED 选择器会被主题主色覆盖成蓝色
        var code = GenScreen(Ctl(HmiControlType.CheckBox, "cb1", c => c.IndicatorColor = "#ff8800"));
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_cb1, lv_color_hex(0xff8800), LV_PART_INDICATOR);");
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_cb1, lv_color_hex(0xff8800), LV_PART_INDICATOR | LV_STATE_CHECKED);");
    }

    [Fact]
    public void Label_IndicatorColor_IsNotApplied()
    {
        // 指示器色只对 Slider/Bar/Switch/CheckBox/Gauge/Knob 生效
        var code = GenScreen(Ctl(HmiControlType.Label, "l1", c => c.IndicatorColor = "#ff8800"));
        CSource.AssertNoText(code, "lv_obj_set_style_bg_color(obj_l1, lv_color_hex(0xff8800), LV_PART_INDICATOR)");
    }

    [Fact]
    public void Slider_IndicatorColorEmpty_FallsBackToTextColor()
    {
        var code = GenScreen(Ctl(HmiControlType.Slider, "sl1", c => { c.IndicatorColor = null; c.TextColor = "#00ff00"; }));
        CSource.AssertCode(code, "lv_obj_set_style_bg_color(obj_sl1, lv_color_hex(0x00ff00), LV_PART_INDICATOR);");
    }

    [Fact]
    public void CheckBox_IsChecked_AddsCheckedState()
    {
        var code = GenScreen(Ctl(HmiControlType.CheckBox, "cb1", c => c.IsChecked = true));
        CSource.AssertCode(code, "lv_obj_add_state(obj_cb1, LV_STATE_CHECKED);");
    }

    [Fact]
    public void Label_IsChecked_EmitsNoCheckedState()
    {
        // 勾选状态只对 Switch/CheckBox 有意义
        var code = GenScreen(Ctl(HmiControlType.Label, "l1", c => c.IsChecked = true));
        CSource.AssertNoText(code, "lv_obj_add_state(obj_l1, LV_STATE_CHECKED)");
    }

    // ───────────────────────── 阶段 203 补：此前零输出断言的发射路径 ─────────────────────────
    // 阶段 203 想新建一张"面板字段 → 导出产物"的消费账，**先查账**才发现本类早就用同样的
    // 「API 锚点法」建过这一面 ⇒ 停手、只补它没覆盖的那些（不重复记账）。判定口径：
    // 在**本文件**里 grep 那个 LVGL API 名，0 命中才算"零输出断言"。

    [Fact]
    public void Slider_RangeAndValue_AreEmitted()
    {
        var code = GenScreen(Ctl(HmiControlType.Slider, "sl1", c => { c.MinValue = 10; c.MaxValue = 200; c.Value = 42; }));
        CSource.AssertCode(code, "lv_slider_set_range(obj_sl1, 10, 200);");
        CSource.AssertCode(code, "lv_slider_set_value(obj_sl1, 42, LV_ANIM_OFF);");
    }

    [Fact]
    public void Bar_RangeAndValue_AreEmitted()
    {
        var code = GenScreen(Ctl(HmiControlType.Bar, "b1", c => { c.MinValue = 0; c.MaxValue = 100; c.Value = 42; }));
        CSource.AssertCode(code, "lv_bar_set_range(obj_b1, 0, 100);");
        CSource.AssertCode(code, "lv_bar_set_value(obj_b1, 42, LV_ANIM_OFF);");
    }

    [Fact]
    public void Bar_ShowPointer_EmitsCursorObjectAndFollowCallback()
    {
        // LVGL 的 lv_bar 没有原生游标 ⇒ 本仓自建一个圆点 + 值变化回调让它跟随
        var code = GenScreen(Ctl(HmiControlType.Bar, "b1", c => { c.ShowPointer = true; c.Width = 200; c.Height = 20; }));
        CSource.AssertCode(code, "_cur_b1 = lv_obj_create(obj_b1);");
        CSource.AssertCode(code, "lv_obj_add_event_cb(obj_b1, _bar_");
    }

    [Fact]
    public void Bar_ShowPointerUnset_EmitsNoCursorObject()
    {
        var code = GenScreen(Ctl(HmiControlType.Bar, "b1", c => c.Value = 10));
        CSource.AssertNoText(code, "_cur_b1 = lv_obj_create");
    }

    [Fact]
    public void Led_BrightnessAndColor_AreEmitted()
    {
        var code = GenScreen(Ctl(HmiControlType.Led, "l1", c => { c.Brightness = 7; c.BgColor = "#112233"; }));
        CSource.AssertCode(code, "lv_led_set_color(obj_l1, lv_color_hex(0x112233));");
        CSource.AssertCode(code, "lv_led_set_brightness(obj_l1, 7);");
    }

    [Fact]
    public void Gauge_ArcRangeAndMajorTicks_AreEmittedOrDeclared()
    {
        // Gauge 走 lv_arc：范围/值真发射；**主刻度** LVGL 无 API ⇒ 以注释声明段数（不静默丢）
        var code = GenScreen(Ctl(HmiControlType.Gauge, "g1", c => { c.MinValue = 0; c.MaxValue = 100; c.Value = 42; c.MajorTickCount = 6; }));
        CSource.AssertCode(code, "lv_arc_set_range(obj_g1, 0, 100);");
        CSource.AssertCode(code, "lv_arc_set_value(obj_g1, 42);");
        CSource.AssertCode(code, "// Gauge 主刻度: 6 段");
    }

    [Fact]
    public void Gauge_MajorTickCountBelowTwo_IsClampedInTheDeclaration()
    {
        var code = GenScreen(Ctl(HmiControlType.Gauge, "g1", c => c.MajorTickCount = 0));
        CSource.AssertCode(code, "// Gauge 主刻度: 2 段");
    }

    [Fact]
    public void Knob_KeepsVisibleKnobPart()
    {
        var code = GenScreen(Ctl(HmiControlType.Knob, "k1", c => { c.MinValue = 0; c.MaxValue = 100; }));
        CSource.AssertCode(code, "lv_arc_set_range(obj_k1, 0, 100);");
        CSource.AssertCode(code, "lv_obj_set_style_opa(obj_k1, LV_OPA_COVER, LV_PART_KNOB);");
    }

    [Theory]
    [InlineData("Number", "LV_KEYBOARD_MODE_NUMBER")]
    [InlineData("Special", "LV_KEYBOARD_MODE_SPECIAL")]
    [InlineData("Default", "LV_KEYBOARD_MODE_TEXT_LOWER")]   // Default/Text/未知 ⇒ LVGL 默认模式
    public void Keyboard_Mode_IsEmittedAsLvglMode(string mode, string expected)
    {
        // 🩸 阶段 203 修 #93：此前这里**只发一行注释**（面板能设、导出不生效）；
        //    LVGL 有 lv_keyboard_set_mode ⇒ 补齐（与 #92 那种"结构上表达不了"不同）
        var code = GenScreen(Ctl(HmiControlType.Keyboard, "kb1", c => c.KeyboardMode = mode));
        CSource.AssertCode(code, $"lv_keyboard_set_mode(obj_kb1, {expected});");
        CSource.AssertCode(code, "lv_keyboard_create");
    }

    [Fact]
    public void Keyboard_NoMode_EmitsNoModeCall()
    {
        var code = GenScreen(Ctl(HmiControlType.Keyboard, "kb1"));
        CSource.AssertNoText(code, "lv_keyboard_set_mode(obj_kb1");
    }

    [Fact]
    public void Line_RoundCap_IsEmitted_SquareCapIsDeclared()
    {
        var round = GenScreen(Ctl(HmiControlType.Line, "ln1", c => c.LineCap = "Round"));
        CSource.AssertCode(round, "lv_obj_set_style_line_rounded(obj_ln1, true, 0);");

        // LVGL 只有圆头/平头 ⇒ Square 以平头表达，并以注释**声明**（不静默丢语义）
        var square = GenScreen(Ctl(HmiControlType.Line, "ln2", c => c.LineCap = "Square"));
        CSource.AssertCode(square, "// Line cap: Square (LVGL 仅支持 Round/平头");
    }

    [Fact]
    public void Polyline_Points_AreEmittedAsPointArray()
    {
        var code = GenScreen(Ctl(HmiControlType.Polyline, "pl1", c => c.Points = new List<PointData>
        {
            new(0, 0), new(10, 20), new(30, 40),
        }));
        CSource.AssertCode(code, "lv_line_set_points(obj_pl1, _pts_pl1, 3);");
        CSource.AssertCode(code, "{ 10, 20 }");
    }

    [Fact]
    public void Table_ColumnWidthsAndRowHeight_AreEmitted()
    {
        var code = GenScreen(Ctl(HmiControlType.Table, "tb1", c =>
        {
            c.ColumnCount = 2;
            c.ColumnWidths = "80,auto";
            c.RowHeight = 24;
        }));
        CSource.AssertCode(code, "lv_table_set_column_width(obj_tb1, 0, 80);");
        CSource.AssertCode(code, "lv_table_set_column_width(obj_tb1, 1, LV_COORD_MAX);");   // auto ⇒ 自适应
        CSource.AssertCode(code, "lv_obj_set_style_min_height(obj_tb1, 24, LV_PART_ITEMS);");
        CSource.AssertCode(code, "lv_obj_set_style_max_height(obj_tb1, 24, LV_PART_ITEMS);");
    }

    [Fact]
    public void Table_RowHeightZero_EmitsNoHeightStyle()
    {
        var code = GenScreen(Ctl(HmiControlType.Table, "tb1", c => { c.ColumnCount = 1; c.RowHeight = 0; }));
        CSource.AssertNoText(code, "lv_obj_set_style_min_height(obj_tb1,");
    }
}
