using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Views;
using HmiEditor.Core.Models.Animation;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`AnimationEditorView` 的 `TextBox → 动画模型` 驱动面**（阶段 190）——
/// 把「TextBox 接线普查面」（<see cref="AppTextBoxWiringCensusTests"/>）里
/// <c>AnimationEditorView.axaml</c> 的那 9 条接线从 `census` 升级成 `driven`。
///
/// <para><b>缺口形状</b>：阶段 136 的宿主面（<c>HostButtonEffectUiTests</c>）驱动的是这个视图的
/// **选择族与按钮族**（下拉 / 列表 / 复选框 / 按钮）；9 个文本输入框
/// （<c>TxtAnimationName</c> / <c>TxtDescription</c> / 四个数值 / <c>TxtRepeatCount</c> /
/// 两个关键帧框）**一条都没被驱动过** —— 摘掉任意一条 <c>TextChanged</c> 接线，
/// 全仓没有一条判据会红（程序化写 <c>TextBox.Text</c> 不触发 <c>TextChanged</c>，
/// 阶段 187 实测）。</para>
///
/// <para><b>两条写入口径都在这里被写进期望值</b>：动画字段走 <c>SyncAnimationFromUi()</c>
/// （描述 <b>Trim</b>、时长/延时 <c>Math.Max(0,…)</c>、重复次数不裁剪），
/// 关键帧走 <c>SyncKeyframeFromUi()</c>（时间 <b>钳到 0…100</b>）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class AnimationEditorTextBoxWiringTests
{
    /// <summary>一条手势：把 <paramref name="Text"/> 写进 <paramref name="Name"/>，断言动画模型读到 <paramref name="Expected"/>。</summary>
    /// <param name="Unchanged">true ⇒ 期望**一字不变**（解析拒绝后的原值）。</param>
    private sealed record Spec(
        string Name,
        string Setting,
        Func<WindowDepot, string> Read,
        string Text,
        string Expected,
        bool Unchanged = false);

    private static readonly Spec[] Specs =
    {
        // ── 动画基本信息（Name 不裁剪；Description **Trim**）──
        new("TxtAnimationName", "Name", d => Anim(d).Name, "呼吸灯渐亮", "呼吸灯渐亮"),
        new("TxtDescription", "Description", d => Anim(d).Description ?? "",
            "  呼吸灯描述  ", "呼吸灯描述"),

        // ── 四个数值：走 InvariantCulture 解析 ──
        new("TxtStartValue", "StartValue", d => Num(Anim(d).StartValue), "12.5", "12.5"),
        new("TxtEndValue", "EndValue", d => Num(Anim(d).EndValue), "204.5", "204.5"),
        new("TxtDuration", "DurationMs", d => Anim(d).DurationMs.ToString(CultureInfo.InvariantCulture), "2500", "2500"),
        new("TxtDelay", "DelayMs", d => Anim(d).DelayMs.ToString(CultureInfo.InvariantCulture), "120", "120"),
        new("TxtRepeatCount", "RepeatCount",
            d => Anim(d).RepeatCount.ToString(CultureInfo.InvariantCulture), "3", "3"),

        // ── 解析拒绝：非法文本不进模型 ──
        new("TxtDuration", "DurationMs",
            d => Anim(d).DurationMs.ToString(CultureInfo.InvariantCulture), "abc", "", Unchanged: true),
        new("TxtRepeatCount", "RepeatCount",
            d => Anim(d).RepeatCount.ToString(CultureInfo.InvariantCulture), "很多次", "", Unchanged: true),

        // ── 时长的下界：负数被 `Math.Max(0, dur)` 抬回 0（不是原样写进去）──
        new("TxtDuration", "DurationMs",
            d => Anim(d).DurationMs.ToString(CultureInfo.InvariantCulture), "-5", "0"),

        // ── 关键帧：时间**钳到 0…100**、值原样 ──
        new("TxtKeyframeTime", "Keyframe[0].TimePercent",
            d => Num(Keyframe(d).TimePercent), "37", "37"),
        new("TxtKeyframeTime", "Keyframe[0].TimePercent",
            d => Num(Keyframe(d).TimePercent), "150", "100"),
        new("TxtKeyframeValue", "Keyframe[0].Value",
            d => Num(Keyframe(d).Value), "0.75", "0.75"),
    };

    private static AnimationConfig Anim(WindowDepot depot) => depot.Animations[0];

    private static AnimationKeyframe Keyframe(WindowDepot depot) => Anim(depot).Keyframes[0];

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void EverySpec_WritesTheAnimationModelAsDeclared()
    {
        using var sandbox = new DataRootSandbox("animation-textbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);

            var window = new AnimationEditorView(depot.Animations, depot.Project);
            window.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检①：选中动画 ⇒ 右侧面板被**模型**回填（`SyncAnimationFromUi` 首行判
                // `_currentAnimation == null` 就 return ⇒ 不选中等于什么都没驱动）
                ControlWiringFace.Find<ListBox>(window, "AnimationList").SelectedIndex = 0;
                UiTestHost.Pump(2);
                Assert.Equal("呼吸灯", ControlWiringFace.Find<TextBox>(window, "TxtAnimationName").Text);

                // 前提自检②：加一条关键帧并选中它（关键帧那两条接线的前提是 `_selectedKeyframe != null`）
                ControlWiringFace.Find<Button>(window, "BtnAddKeyframe")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                UiTestHost.Pump(2);
                Assert.Single(Anim(depot).Keyframes);

                ControlWiringFace.Find<ListBox>(window, "KeyframeList").SelectedIndex = 0;
                UiTestHost.Pump(2);
                Assert.True(ControlWiringFace.Find<Border>(window, "KeyframeEditPanel").IsVisible,
                    "关键帧面板没打开 ⇒ 关键帧那两条手势不成立");

                foreach (var spec in Specs)
                {
                    var before = spec.Read(depot);
                    ControlWiringFace.Write(window, spec.Name, spec.Text);
                    var after = spec.Read(depot);

                    if (spec.Unchanged)
                    {
                        Assert.True(string.Equals(before, after, StringComparison.Ordinal),
                            $"{spec.Name}（{spec.Setting}）本应**不动**：'{spec.Text}' 之后从 '{before}' 变成了 '{after}'");
                        continue;
                    }

                    Assert.True(string.Equals(spec.Expected, after, StringComparison.Ordinal),
                        $"{spec.Name}（{spec.Setting}）驱动 '{spec.Text}' 后读到 '{after}'，期望 '{spec.Expected}'");
                }

                // 反面见证：驱动只落在**这一个**动画上，集合结构没被顺手改动
                Assert.Same(Anim(depot), depot.Animations[0]);
                Assert.Single(depot.Animations);
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：`AnimationEditorView.axaml` 的每个具名 `TextBox` 与每条 `TextChanged` 接线
    /// 都必须在本表里（反面同样成立）。
    /// </summary>
    [Fact]
    public void EveryTextBoxAndWiring_IsDrivenByTheTable()
    {
        ControlWiringFace.AssertFullyDriven(
            "AnimationEditorView.axaml", Specs.Select(s => s.Name).ToArray(), scanFloor: 9);
    }
}
