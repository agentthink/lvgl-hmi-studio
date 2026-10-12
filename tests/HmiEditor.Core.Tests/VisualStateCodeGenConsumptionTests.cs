using System.Linq;
using HmiEditor.CodeGen;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **「视觉状态字段」的消费账**（阶段 202，登记 `#92`）—— `ControlVisualState` 有 7 个可编辑字段，
/// 这一面把"面板给用户设的每一格，**谁**消费了"钉死：**预览**（`ControlTextStyleResolver`）与
/// **代码生成**（`GenerateVisualStates`）两侧分开判。
///
/// <para><b>为什么需要它</b>：这块在阶段 202 之前**一条判据都没有**
/// （全仓 `grep LV_STATE_PRESSED tests/` 为空）—— 面板改了、预览变了，导出丢没丢没人看着。
/// 追下去量出的账：</para>
///
/// <list type="bullet">
/// <item><b>样式选择器能带的</b>（`lv_obj_set_style_*` 可带 `LV_STATE_*`）：`BgColor` / `TextColor` /
/// `BorderColor` / `TextAlign` ⇒ 真正发射；</item>
/// <item><b>图片</b>：`ImageAssetId` ⇒ 发射 `lv_image_set_src`（并在资源不在库中时用注释声明跳过）；</item>
/// <item><b>结构上表达不了的</b>：`Text`（切换文本要运行时事件回调）与 **`TextVAlign`**
/// （LVGL 没有 vertical-align 样式；Normal 态是用 `LV_SIZE_CONTENT` + 重定位的**变换**实现的，
/// 而变换带不了状态选择器）⇒ **只能声明**，由运行时回调实现 —— 这两条在导出里都必须留下声明注释，
/// 不能**静默丢失**。</item>
/// </list>
/// </summary>
public class VisualStateCodeGenConsumptionTests
{
    /// <summary>造一个带 `Pressed` 状态条目的屏幕；该条目把 7 个字段**全部**填上。</summary>
    private static HmiScreen MakeScreen(Func<HmiControl, ControlVisualState> state)
    {
        var control = new HmiControl
        {
            Id = "btn1",
            Type = HmiControlType.Button,
            Name = "Button1",
            Text = "默认文本",
            X = 10,
            Y = 20,
            Width = 120,
            Height = 40,
            BgColor = "#111111",
            TextColor = "#222222",
            BorderColor = "#333333",
            TextAlign = TextAlignMode.Left,
            TextVAlign = TextVAlignMode.Top,
            ImageAssetId = "img_main",
        };
        control.VisualStates.Add(state(control));

        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        screen.Controls.Add(control);
        return screen;
    }

    /// <summary>把状态条目的 7 个字段填满（默认态取与控件不同的值，便于区分是哪一侧发射的）。</summary>
    private static ControlVisualState FullState(HmiControl control) => new()
    {
        State = VisualState.Pressed,
        BgColor = "#AA0001",
        TextColor = "#AA0002",
        BorderColor = "#AA0003",
        ImageAssetId = "img_pressed",
        Text = "按下时的文本",
        TextAlign = TextAlignMode.Center,
        TextVAlign = TextVAlignMode.Bottom,
    };

    [Fact]
    public void StyleCarriableFields_AreEmittedWithTheStateSelector()
    {
        var code = new CodeGenerator().GenerateScreenCode(MakeScreen(FullState));

        // 四色/对齐：都作为 `lv_obj_set_style_*(..., LV_STATE_PRESSED)` 发射
        Assert.Contains("lv_obj_set_style_bg_color(", code);
        Assert.Contains("lv_obj_set_style_text_color(", code);
        Assert.Contains("lv_obj_set_style_border_color(", code);
        Assert.Contains("lv_obj_set_style_text_align(", code);
        Assert.Contains("LV_STATE_PRESSED", code);

        // 状态里的色值真的进了输出（不是控件默认色）
        Assert.Contains("aa0001", code, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aa0002", code, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aa0003", code, System.StringComparison.OrdinalIgnoreCase);

        // 图片：`GenerateScreenCode` 不传可用资源集合（null ⇒ 不过滤）⇒ 直接发射
        Assert.Contains("lv_image_set_src(", code);
        Assert.Contains("img_pressed", code);
    }

    [Fact]
    public void StateImage_NotInAssetLibrary_IsSkippedWithADeclaration()
    {
        // 传一个**不含**该资源 id 的可用集合 ⇒ 走"跳过"分支，但必须留下声明（不静默丢）
        var screen = MakeScreen(FullState);
        var code = new CodeGenerator().GenerateScreenCode(
            screen, availableImageIds: new System.Collections.Generic.HashSet<string> { "img_other" });

        Assert.Contains("跳过：图片资源 img_pressed 不在 AssetLibrary 中", code);
    }

    [Fact]
    public void Text_IsDeclaredAsRuntimeOnly()
    {
        var code = new CodeGenerator().GenerateScreenCode(MakeScreen(FullState));

        // 文本切换：LVGL 静态样式表达不了 ⇒ 阶段 202 先**声明**、阶段 229 起**真的生成回调**；
        // 这条判据（原"声明 as runtime-only"）现在同时钉住"注释还在"与"回调注册在"
        Assert.Contains("// State text: \"按下时的文本\"", code);
        Assert.Contains("运行时由 _vs_state_ 回调切换", code);
        CSource.AssertCode(code, "lv_obj_add_event_cb(obj_btn1, _vs_state_btn1, LV_EVENT_STATE_CHANGED, NULL);");
    }

    [Fact]
    public void VerticalAlign_IsDeclaredAsNotExpressibleAsAStyle()
    {
        var code = new CodeGenerator().GenerateScreenCode(MakeScreen(FullState));

        // 🩸 #92：垂直对齐**结构上**带不了状态选择器（Normal 态那条路是"重定位变换"）
        //   ⇒ 导出必须留下声明（此前**连注释都没有**：面板设了、预览变了、导出静默丢）
        Assert.Contains("// State vertical align: Bottom", code);
        Assert.Contains("运行时由 _vs_state_ 回调重定位", code);

        // 反面见证：它**不是**以 `lv_obj_set_style_*(..., LV_STATE_*)` 的形式发射的
        var alignLines = code.Split('\n').Where(l => l.Contains("text_align")).ToArray();
        Assert.NotEmpty(alignLines);
        Assert.DoesNotContain(alignLines, l => l.Contains("VerticalAlign"));
    }

    [Fact]
    public void StateWithOnlyTextAlign_UsesTheModeMapping()
    {
        // 只设水平对齐 ⇒ 映射到 LVGL 的 TEXT_ALIGN（Center）
        var code = new CodeGenerator().GenerateScreenCode(MakeScreen(_ => new ControlVisualState
        {
            State = VisualState.Pressed,
            TextAlign = TextAlignMode.Center,
        }));

        Assert.Contains("lv_obj_set_style_text_align(", code);
        Assert.Contains("LV_TEXT_ALIGN_CENTER", code);
        Assert.Contains("LV_STATE_PRESSED", code);
    }

    [Fact]
    public void EmptyStateEntry_EmitsNothingButTheComment()
    {
        // 全空的状态条目：不该发射任何样式（只是"占位"）
        var code = new CodeGenerator().GenerateScreenCode(MakeScreen(_ => new ControlVisualState
        {
            State = VisualState.Checked,
        }));

        // 代码行（非注释）里**没有**任何带 `LV_STATE_CHECKED` 的样式发射
        var codeLines = code.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("/*"))
            .ToArray();
        Assert.DoesNotContain(codeLines, l => l.Contains("LV_STATE_CHECKED"));

        // 控件默认色照常发射（与状态无关），但**没有** `LV_STATE_CHECKED` 那一段
        Assert.Contains(codeLines, l => l.Contains("lv_obj_set_style_bg_color(") && l.TrimEnd().EndsWith(", 0);"));
    }

    // ── 预览那一侧（同一批字段，消费方不同）──────────────────────────────

    [Fact]
    public void Preview_ResolvesStateFields_ThatCodeGenCannotExpress()
    {
        var control = new HmiControl
        {
            Id = "btn1",
            Text = "默认",
            TextAlign = TextAlignMode.Left,
            TextVAlign = TextVAlignMode.Top,
            TextColor = "#222222",
        };
        var state = new ControlVisualState
        {
            State = VisualState.Pressed,
            Text = "按下",
            TextAlign = TextAlignMode.Right,
            TextVAlign = TextVAlignMode.Bottom,
            TextColor = "#AA0002",
        };

        // 预览侧：状态样式优先 —— 这三格**都能看出来**（与"导出表达不了"形成对照）
        Assert.Equal("#AA0002", ControlTextStyleResolver.PreferHex(state, control, null));
        Assert.Equal(TextAlignMode.Right, ControlTextStyleResolver.HorizontalAlign(state, control, VisualState.Pressed));
        Assert.Equal(TextVAlignMode.Bottom, ControlTextStyleResolver.VerticalAlign(state, control, VisualState.Pressed));

        // Normal 预览态：一律取控件值（状态条目**不**参与）
        Assert.Equal(TextAlignMode.Left, ControlTextStyleResolver.HorizontalAlign(state, control, VisualState.Normal));
        Assert.Equal(TextVAlignMode.Top, ControlTextStyleResolver.VerticalAlign(state, control, VisualState.Normal));
    }

    [Fact]
    public void NormalStateEntry_IsSkippedByCodeGen()
    {
        // `Normal` 条目由"控件默认字段"那条路处理（生成器里 `lvState == null ⇒ continue`）——
        // 这一条把"Normal 条目不会二次发射"钉住（与面板侧的口径一致：Normal = 控件字段）
        var code = new CodeGenerator().GenerateScreenCode(MakeScreen(control => new ControlVisualState
        {
            State = VisualState.Normal,
            BgColor = "#ABCDEF",
        }));

        // 六位色值是**去掉 `#`** 发射的（`lv_color_hex(0x…)`）
        Assert.DoesNotContain("0xABCDEF", code, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0x111111", code, System.StringComparison.OrdinalIgnoreCase);   // 控件默认色照常发射
    }

}
