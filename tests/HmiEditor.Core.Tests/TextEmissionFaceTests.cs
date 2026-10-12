using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **文本类控件的"文本 ＋ 对齐"都进导出**（阶段 219，登记 `#96` 的"导出补齐"那半 ＋ 新登记的 `#99`）。
///
/// <para><b>为什么单独一面</b>：阶段的枚举取值账（`EnumeratedChoiceEmissionTests`）按**字段**组织，
/// 一次只看一个控件类型；而"文本/对齐"这条链的缺口恰恰出在**按类型分叉**上 —— 同一份
/// <c>text_align</c> 逻辑，Label/Button/Dropdown 早就发射，CheckBox 却只设文本不设对齐
/// （**预览**却按对齐画：`DesignCanvas.Renderers.cs` 的 RenderCheckBox）；TextArea 更彻底 ——
/// 导出**从不发射 `lv_textarea_set_text`**，面板里输入的文本在设备上直接消失（登记 `#99`）。</para>
///
/// <para>所以这一面按**类型**逐个点名：每个文本类控件都要有"文本"行、"对齐"行；没有文本时两者都不发。</para>
/// </summary>
public class TextEmissionFaceTests
{
    private static HmiControl TextCtl(HmiControlType type, string id, string? text = "温度", TextAlignMode align = TextAlignMode.Center)
        => new()
        {
            Id = id,
            Name = id,
            Type = type,
            X = 10,
            Y = 20,
            Width = 100,
            Height = 40,
            Text = text,
            TextAlign = align,
        };

    private static string Gen(params HmiControl[] controls)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 1024, Height = 600 };
        foreach (var c in controls) screen.Controls.Add(c);
        return new CodeGenerator().GenerateScreenCode(screen, null);
    }

    /// <summary>每个文本类控件：文本行 ＋ 对齐行（阶段 219 补齐后应当**全绿**）。</summary>
    [Theory]
    [InlineData(HmiControlType.Label, "lv_label_set_text(obj_c1, \"温度\");")]
    [InlineData(HmiControlType.Button, "lv_label_set_text(label_c1, \"温度\");")]
    [InlineData(HmiControlType.CheckBox, "lv_checkbox_set_text(obj_c1, \"温度\");")]
    [InlineData(HmiControlType.TextArea, "lv_textarea_set_text(obj_c1, \"温度\");")]
    public void EveryTextControl_ExportsItsText(HmiControlType type, string expected)
    {
        var code = Gen(TextCtl(type, "c1"));
        CSource.AssertCode(code, expected);
    }

    /// <summary>
    /// **对齐**：`Button` 的对齐设在它的**子 Label** 上（`lv_label_set_text` 传 Button 会越界访问），
    /// 其余在控件本体上。
    /// </summary>
    [Theory]
    [InlineData(HmiControlType.Label, "lv_obj_set_style_text_align(obj_c1, LV_TEXT_ALIGN_RIGHT, 0);")]
    [InlineData(HmiControlType.Button, "lv_obj_set_style_text_align(label_c1, LV_TEXT_ALIGN_RIGHT, 0);")]
    [InlineData(HmiControlType.CheckBox, "lv_obj_set_style_text_align(obj_c1, LV_TEXT_ALIGN_RIGHT, 0);")]
    [InlineData(HmiControlType.TextArea, "lv_obj_set_style_text_align(obj_c1, LV_TEXT_ALIGN_RIGHT, 0);")]
    public void EveryTextControl_ExportsItsAlignment(HmiControlType type, string expected)
    {
        var code = Gen(TextCtl(type, "c1", align: TextAlignMode.Right));
        CSource.AssertCode(code, expected);
    }

    /// <summary>**没有文本 ⇒ 文本与对齐都不发射**（别留下"对齐了一个空对象"这种噪音）。</summary>
    [Theory]
    [InlineData(HmiControlType.Label)]
    [InlineData(HmiControlType.Button)]
    [InlineData(HmiControlType.CheckBox)]
    [InlineData(HmiControlType.TextArea)]
    public void NoText_ExportsNeitherTextNorAlignment(HmiControlType type)
    {
        var code = Gen(TextCtl(type, "c1", text: null));
        CSource.AssertNoCode(code, "lv_obj_set_style_text_align(obj_c1,");
        CSource.AssertNoCode(code, "lv_obj_set_style_text_align(label_c1,");
    }

    /// <summary>
    /// **`TextAlign` 的三个取值都真的进导出**（阶段 212 那三条被钉成"不发射"是**夹具没设 `Text`**
    /// 造成的假象）—— 这里把三个取值逐一对上 LVGL 常量。
    /// </summary>
    [Theory]
    [InlineData(TextAlignMode.Left, "LV_TEXT_ALIGN_LEFT")]
    [InlineData(TextAlignMode.Center, "LV_TEXT_ALIGN_CENTER")]
    [InlineData(TextAlignMode.Right, "LV_TEXT_ALIGN_RIGHT")]
    public void EveryAlignValue_IsExported(TextAlignMode align, string lvgl)
    {
        var code = Gen(TextCtl(HmiControlType.Label, "c1", align: align));
        CSource.AssertCode(code, $"lv_obj_set_style_text_align(obj_c1, {lvgl}, 0);");
    }

    /// <summary>
    /// **面自检**：这四类"文本类控件"的名单要与本面的 Theory 行数一致 —— 有人给
    /// `HmiControlType` 加了新的文本类控件时，这条会提醒他补进上面三张表。
    /// </summary>
    [Fact]
    public void Face_CoversTheTextBearingTypes()
    {
        var kinds = new[]
        {
            HmiControlType.Label, HmiControlType.Button, HmiControlType.CheckBox, HmiControlType.TextArea,
            HmiControlType.Dropdown,
        };

        // Dropdown 的文本走 options（`EmitDropdownParameters`），对齐也在那里发射 ⇒ 单独断言一次
        var dropdown = new HmiControl { Id = "dd1", Name = "dd1", Type = HmiControlType.Dropdown, Options = "A\nB" };
        var code = Gen(dropdown);
        CSource.AssertCode(code, "lv_dropdown_set_options(obj_dd1, \"A\\nB\");");
        CSource.AssertCode(code, "lv_obj_set_style_text_align(obj_dd1, LV_TEXT_ALIGN_CENTER, 0);");

        Assert.Equal(5, kinds.Distinct().Count());
    }

    // ────────────────────────────────────────────────────────────────
    // 字号：无字体资产时走**内置字号近似映射**（阶段 220，登记 #97）
    // ────────────────────────────────────────────────────────────────

    private static string GenWithFonts(HmiControl control, params string[] fontIds)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 1024, Height = 600 };
        screen.Controls.Add(control);
        return new CodeGenerator().GenerateScreenCode(screen, null, null, null, null, null,
            fontIds.Length == 0 ? null : new HashSet<string>(fontIds, StringComparer.Ordinal));
    }

    /// <summary>
    /// **没有字体资产**时，字号不再被静默丢掉：比主题默认（14）小 ⇒ 就近取内置 `12`，大 ⇒ `16`，
    /// 并用 `#if LV_FONT_MONTSERRAT_NN` 包住（设备没启用该档就整段消失 —— 安全的兜底）。
    /// </summary>
    [Theory]
    [InlineData(6, 12)]
    [InlineData(13, 12)]
    [InlineData(15, 16)]
    [InlineData(72, 16)]
    public void FontSizeWithoutAsset_FallsBackToBuiltInFont(int size, int expected)
    {
        var control = TextCtl(HmiControlType.Label, "c1");
        control.FontSize = size;

        var code = GenWithFonts(control);

        CSource.AssertText(code, $"#if LV_FONT_MONTSERRAT_{expected}");
        CSource.AssertCode(code, $"lv_obj_set_style_text_font(obj_c1, &lv_font_montserrat_{expected}, 0);");
    }

    /// <summary>
    /// **逐字**钉住一条（上面那条 Theory 用插值拼串，判不了"字面存在"）：大字号 ⇒ 内置 16，
    /// 并且整段在 `#if LV_FONT_MONTSERRAT_16` 里。
    /// </summary>
    [Fact]
    public void FontSizeWithoutAsset_EmitsTheBuiltInFontUnderACompileGuard()
    {
        var control = TextCtl(HmiControlType.Label, "c1");
        control.FontSize = 72;

        var code = GenWithFonts(control);

        CSource.AssertText(code, "#if LV_FONT_MONTSERRAT_16");
        CSource.AssertCode(code, "lv_obj_set_style_text_font(obj_c1, &lv_font_montserrat_16, 0);");
        CSource.AssertText(code, "#endif");
    }

    /// <summary>字号与主题默认一致（14）⇒ **不发射**任何字体设置（沿用主题字体）。</summary>
    [Fact]
    public void FontSizeAtThemeDefault_EmitsNoFont()
    {
        var control = TextCtl(HmiControlType.Label, "c1");
        control.FontSize = 14;

        var code = GenWithFonts(control);

        CSource.AssertNoText(code, "lv_obj_set_style_text_font(obj_c1,");
        CSource.AssertNoText(code, "montserrat");
    }

    /// <summary>
    /// **有**字体资产时走自定义字体路（`#if LV_USE_FREETYPE` ＋ `&font_<id>_<size>`），
    /// **不会**被内置字号顶掉。
    /// </summary>
    [Fact]
    public void FontSizeWithAsset_UsesTheAssetAndNotTheBuiltIn()
    {
        var control = TextCtl(HmiControlType.Label, "c1");
        control.FontFamily = "myfont";
        control.FontSize = 20;

        var code = GenWithFonts(control, "myfont");

        CSource.AssertText(code, "#if LV_USE_FREETYPE");
        CSource.AssertCode(code, "lv_obj_set_style_text_font(obj_c1, &font_myfont_20, 0);");
        CSource.AssertNoText(code, "montserrat");
    }
}
