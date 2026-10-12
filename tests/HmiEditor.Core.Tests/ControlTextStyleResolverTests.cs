using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 行为测试：控件文本样式解析（状态样式优先 → 控件默认 → 主题/调色板兜底）。
///
/// 这是对原 <c>DesignCanvasTextColorGuardTests</c> 的替代 ——
/// 旧测试读 <c>DesignCanvas.cs</c> 源码做 grep 断言，重构即碎、行为不变也会碎，
/// 且无法验证真实行为；规则抽到 <see cref="ControlTextStyleResolver"/> 后可直接断言结果。
/// </summary>
public class ControlTextStyleResolverTests
{
    private static HmiControl MakeControl(string? textColor) => new()
    {
        Id = "c1",
        Type = HmiControlType.Label,
        TextColor = textColor,
        TextAlign = TextAlignMode.Left,
        TextVAlign = TextVAlignMode.Top,
    };

    // ── 文本颜色回退链 ──

    [Fact]
    public void StateStyleTextColor_Wins()
    {
        var control = MakeControl("#111111");
        control.VisualStates.Add(new ControlVisualState
        {
            State = VisualState.Checked,
            TextColor = "#eeeeee",
        });

        var stateStyle = control.GetStateStyle(VisualState.Checked);
        var hex = ControlTextStyleResolver.PreferHex(stateStyle, control, "#222222");

        Assert.Equal("#eeeeee", hex);
    }

    [Fact]
    public void FallsBackToControlTextColor_WhenNoStateStyle()
    {
        var control = MakeControl("#111111");
        var hex = ControlTextStyleResolver.PreferHex(null, control, "#222222");
        Assert.Equal("#111111", hex);
    }

    [Fact]
    public void FallsBackToTheme_ThenNull()
    {
        var control = MakeControl(null);

        Assert.Equal("#222222", ControlTextStyleResolver.PreferHex(null, control, "#222222"));
        // 三者皆无 → null，交由渲染层用调色板兜底色
        Assert.Null(ControlTextStyleResolver.PreferHex(null, control, null));
        Assert.Null(ControlTextStyleResolver.PreferHex(null, control, ""));
    }

    // ── 对齐回退链 ──

    [Fact]
    public void NormalState_UsesControlAlignment()
    {
        var control = MakeControl(null);
        control.VisualStates.Add(new ControlVisualState
        {
            State = VisualState.Normal,
            TextAlign = TextAlignMode.Right,
            TextVAlign = TextVAlignMode.Bottom,
        });
        var stateStyle = control.GetStateStyle(VisualState.Normal);

        // Normal 态不看状态样式，直接取控件值
        Assert.Equal(TextAlignMode.Left, ControlTextStyleResolver.HorizontalAlign(stateStyle, control, VisualState.Normal));
        Assert.Equal(TextVAlignMode.Top, ControlTextStyleResolver.VerticalAlign(stateStyle, control, VisualState.Normal));
    }

    [Fact]
    public void NonNormalState_PrefersStateStyleAlignment()
    {
        var control = MakeControl(null);
        control.VisualStates.Add(new ControlVisualState
        {
            State = VisualState.Checked,
            TextAlign = TextAlignMode.Right,
            TextVAlign = TextVAlignMode.Bottom,
        });
        var stateStyle = control.GetStateStyle(VisualState.Checked);

        Assert.Equal(TextAlignMode.Right, ControlTextStyleResolver.HorizontalAlign(stateStyle, control, VisualState.Checked));
        Assert.Equal(TextVAlignMode.Bottom, ControlTextStyleResolver.VerticalAlign(stateStyle, control, VisualState.Checked));
    }

    [Fact]
    public void NonNormalState_FallsBackToControlAlignment_WhenStateStyleSilent()
    {
        var control = MakeControl(null);
        control.VisualStates.Add(new ControlVisualState { State = VisualState.Checked });
        var stateStyle = control.GetStateStyle(VisualState.Checked);

        Assert.Equal(TextAlignMode.Left, ControlTextStyleResolver.HorizontalAlign(stateStyle, control, VisualState.Checked));
        Assert.Equal(TextVAlignMode.Top, ControlTextStyleResolver.VerticalAlign(stateStyle, control, VisualState.Checked));
    }
}
