using System.Collections.Generic;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **状态文本 / 状态垂直对齐的运行时回调**（阶段 229，登记 `#92` 的功能那半）。
///
/// <para><b>缺的是什么</b>：面板上给非 Normal 态设的 `Text`/`TextVAlign`，预览照着渲染，
/// 但导出**只留一行注释**（阶段 202 把"连注释都没有"的账补上了，功能仍然没有）——
/// LVGL 的静态样式表达不了"按下换文本"，垂直对齐更是连状态化样式属性都没有。
/// 这一阶段生成 `LV_EVENT_STATE_CHANGED` 回调，把这些设置在设备上**真的生效**。</para>
///
/// <para><b>零影响</b>：没有任何非 Normal 态覆盖（或覆盖在"做不到"的组合上）⇒ 不生成函数、
/// 不注册事件，生成物与从前逐字节相同。</para>
/// </summary>
public class VisualStateRuntimeTextTests
{
    private static HmiScreen ScreenWith(HmiControl control)
    {
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(control);
        return screen;
    }

    private static HmiControl Ctl(string id, HmiControlType type = HmiControlType.Label)
        => new() { Id = id, Name = id, Type = type, X = 10, Y = 20, Width = 100, Height = 40 };

    private static string Gen(HmiControl control) => new CodeGenerator().GenerateScreenCode(ScreenWith(control), null);

    /// <summary>**零影响**：没有状态覆盖 ⇒ 一条回调都不该有。</summary>
    [Fact]
    public void WithoutStateOverrides_NoCallbackIsGenerated()
    {
        var control = Ctl("c1");
        control.Text = "默认";
        control.VisualStates.Add(new ControlVisualState { State = VisualState.Pressed, BgColor = "#FF0000" });

        var code = Gen(control);

        CSource.AssertNoText(code, "LV_EVENT_STATE_CHANGED");
        CSource.AssertNoText(code, "_vs_state_");
    }

    /// <summary>只有**静态样式**能表达的东西（水平对齐/颜色）不需要回调 —— 防止多发。</summary>
    [Fact]
    public void StateTextAlignOnly_StillNeedsNoCallback()
    {
        var control = Ctl("c1");
        control.Text = "默认";
        control.VisualStates.Add(new ControlVisualState { State = VisualState.Pressed, TextAlign = TextAlignMode.Left });

        var code = Gen(control);

        CSource.AssertNoText(code, "LV_EVENT_STATE_CHANGED");
        CSource.AssertCode(code, "lv_obj_set_style_text_align(obj_c1, LV_TEXT_ALIGN_LEFT, LV_STATE_PRESSED);");
    }

    /// <summary>Label 的状态文本：生成回调 ＋ 注册，并在回到默认态时**复位基础文本**。</summary>
    [Fact]
    public void LabelStateText_IsSwitchedAtRuntime()
    {
        var control = Ctl("c1");
        control.Text = "默认";
        control.VisualStates.Add(new ControlVisualState { State = VisualState.Pressed, Text = "按下" });

        var code = Gen(control);

        CSource.AssertText(code, "static void _vs_state_c1(lv_event_t* e)");
        CSource.AssertText(code, "lv_state_t st = lv_obj_get_state(obj);");
        CSource.AssertCode(code, "lv_obj_add_event_cb(obj_c1, _vs_state_c1, LV_EVENT_STATE_CHANGED, NULL);");
        CSource.AssertText(code, "if (st & LV_STATE_PRESSED)");
        CSource.AssertCode(code, "lv_label_set_text(obj, \"按下\");");
        // 复位分支：基础文本必须写回去（否则"按下改的字"会一直留着）
        CSource.AssertCode(code, "lv_label_set_text(obj, \"默认\");");
    }

    /// <summary>多个状态 ⇒ `else if` 链，各写各的文本。</summary>
    [Fact]
    public void MultipleStates_AreChainedAsElseIf()
    {
        var control = Ctl("c1");
        control.Text = "默认";
        control.VisualStates.Add(new ControlVisualState { State = VisualState.Pressed, Text = "按下" });
        control.VisualStates.Add(new ControlVisualState { State = VisualState.Disabled, Text = "禁用" });

        var code = Gen(control);

        CSource.AssertText(code, "else if (st & LV_STATE_DISABLED)");
        CSource.AssertCode(code, "lv_label_set_text(obj, \"按下\");");
        CSource.AssertCode(code, "lv_label_set_text(obj, \"禁用\");");
    }

    /// <summary>状态垂直对齐：Label 走"内容高度 ＋ 重定位"，且该分支**不**改文本。</summary>
    [Fact]
    public void LabelStateVAlign_RepositionsAtRuntime()
    {
        var control = Ctl("c1");
        control.Text = "默认";
        control.VisualStates.Add(new ControlVisualState { State = VisualState.Pressed, TextVAlign = TextVAlignMode.Bottom });

        var code = Gen(control);

        CSource.AssertText(code, "lv_obj_set_height(obj, LV_SIZE_CONTENT);");
        CSource.AssertText(code, "lv_obj_update_layout(obj);");
        CSource.AssertText(code, "lv_obj_get_height(obj)");
        CSource.AssertText(code, "lv_obj_set_y(obj, 20 + 40 - _vs_h_c1);");
        // ⚠ 只判**该状态那个分支**里没有文本切换（那个状态只覆盖了对齐）；
        //   复位分支当然会写回基础文本 —— 第一版整段判"没有 set_text"，被复位分支当场打红。
        var fn = code[code.IndexOf("static void _vs_state_c1", System.StringComparison.Ordinal)..];
        fn = fn[..fn.IndexOf("static lv_obj_t* create_screen", System.StringComparison.Ordinal)];
        var pressedStart = fn.IndexOf("if (st & LV_STATE_PRESSED)", System.StringComparison.Ordinal);
        var nextBranch = fn.IndexOf("else", pressedStart, System.StringComparison.Ordinal);
        var pressedBranch = fn[pressedStart..nextBranch];
        CSource.AssertNoCode(pressedBranch, "lv_label_set_text(obj, \"");
    }

    /// <summary>CheckBox / Button 各用各自的文本 API（Button 是子 label，带空指针保护）。</summary>
    [Fact]
    public void OtherTextBearingTypes_UseTheirOwnTextApi()
    {
        var check = Ctl("chk", HmiControlType.CheckBox);
        check.Text = "默认";
        check.VisualStates.Add(new ControlVisualState { State = VisualState.Checked, Text = "已选" });
        CSource.AssertCode(Gen(check), "lv_checkbox_set_text(obj, \"已选\");");

        var button = Ctl("btn", HmiControlType.Button);
        button.Text = "默认";
        button.VisualStates.Add(new ControlVisualState { State = VisualState.Pressed, Text = "按下" });
        var buttonCode = Gen(button);
        CSource.AssertText(buttonCode, "lv_obj_t* _vs_lbl = lv_obj_get_child(obj, 0);");
        CSource.AssertText(buttonCode, "if (_vs_lbl) lv_label_set_text(_vs_lbl, \"按下\");");
    }

    /// <summary>
    /// **做不到的组合不注册**：Slider 的状态文本保持"仅声明"注释（不生成空转回调），
    /// 这样"零影响"不会被一个什么都不做的回调破坏。
    /// </summary>
    [Fact]
    public void UnsupportedCombination_StaysACommentAndIsNotRegistered()
    {
        var control = Ctl("sld", HmiControlType.Slider);
        control.VisualStates.Add(new ControlVisualState { State = VisualState.Pressed, Text = "按下" });

        var code = Gen(control);

        CSource.AssertNoText(code, "LV_EVENT_STATE_CHANGED");
        CSource.AssertText(code, "不支持运行时改文本，本行**仅声明**");
    }

    /// <summary>面自检：回调函数定义必须排在 `create_screen_*` **之前**（C 里先用后定义不行）。</summary>
    [Fact]
    public void CallbackDefinition_ComesBeforeTheScreenFactory()
    {
        var control = Ctl("c1");
        control.Text = "默认";
        control.VisualStates.Add(new ControlVisualState { State = VisualState.Pressed, Text = "按下" });

        var code = Gen(control);

        var defIndex = code.IndexOf("static void _vs_state_c1", System.StringComparison.Ordinal);
        var factoryIndex = code.IndexOf("static lv_obj_t* create_screen_s1_inner", System.StringComparison.Ordinal);
        var registerIndex = code.IndexOf("lv_obj_add_event_cb(obj_c1", System.StringComparison.Ordinal);

        Assert.True(defIndex >= 0 && factoryIndex > defIndex,
            $"回调定义({defIndex})必须排在 create_screen_*({factoryIndex}) 之前");
        Assert.True(registerIndex > factoryIndex,
            $"注册({registerIndex})应在控件建好之后（create_screen_* 于 {factoryIndex}）");
    }
}
