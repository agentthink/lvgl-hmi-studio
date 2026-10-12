using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`ScreenManagerView` 的「数字框 → 屏幕尺寸」驱动面**（阶段 195）——
/// 把「数字框普查面」（<see cref="AppNumberBoxWiringCensusTests"/>）里
/// <c>ScreenManagerView.axaml</c> 的那 2 条 `ValueChanged` 接线从 `census` 升级成 `perBox`。
///
/// <para><b>缺口形状</b>：<c>NumScreenWidth</c> / <c>NumScreenHeight</c> 都接在同一个
/// <c>OnSizeChanged()</c> 上（把值写进**列表里选中的那块屏**），在此之前**零判据** ——
/// 摘掉任意一条接线，或把"没选中屏就 return"这道前提改掉，都不会红。</para>
///
/// <para><b>读的是模型</b>：`HmiScreen.Width` / `Height`（不是那两个框）——
/// 手柄自己改的框值不构成见证。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class ScreenManagerNumberBoxWiringTests
{
    /// <summary>
    /// 一条手势：把 <paramref name="Value"/> 写进 <paramref name="Name"/>，断言屏幕模型读到 <paramref name="Expected"/>。
    /// ⚠ 手势值**必须与起点值都不同**：`ScreenFactory` 的兜底尺寸是 **1024×600**（阶段 195 踩过 ——
    /// 第一版就写 1024/600，于是"写同值 ⇒ 事件不发 ⇒ 断言照样通过"成了**假绿**：
    /// 负控 nc2 摘掉接线时它没红，只有扫描面下限红了）。
    /// </summary>
    private sealed record Spec(string Name, string Setting, Func<HmiScreen, double> Read, double Value, double Expected);

    private static readonly Spec[] Specs =
    {
        new("NumScreenWidth", "HmiScreen.Width", s => s.Width, 1280, 1280),
        new("NumScreenHeight", "HmiScreen.Height", s => s.Height, 720, 720),
    };

    [Fact]
    public void EverySpec_WritesTheSelectedScreenAsDeclared()
    {
        using var sandbox = new DataRootSandbox("screenmanager-numberbox");

        UiTestHost.Run(() =>
        {
            var project = new HmiProject { Name = "夹具工程" };
            var screen = ScreenFactory.Create(project, name: "主画面");
            project.Screens.Add(screen);

            var view = new ScreenManagerView(project, screen);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检①：`OnSizeChanged` 首行判"列表里选中了哪块屏" —— 没选中就 return（什么都没驱动）
                var list = ControlWiringFace.Find<ListBox>(view, "ScreenList");
                Assert.Single(list.Items);
                Assert.Equal(0, list.SelectedIndex);

                // 前提自检②：起点必须是 ScreenFactory 的兜底尺寸，且**每个手势值都与起点不同**
                //   （否则"写同值不发事件"会让断言变成假绿 —— 正是这一条在阶段 195 抓出了第一版的错）
                Assert.Equal(ScreenFactory.FallbackWidth, screen.Width);
                Assert.Equal(ScreenFactory.FallbackHeight, screen.Height);
                foreach (var spec in Specs)
                    Assert.True(spec.Value != spec.Read(screen),
                        $"{spec.Name} 的手势值 {spec.Value} 与起点值相同 ⇒ 事件不会发，这条腿是空转");

                // 前提自检③：两个框回填的也是模型值
                Assert.Equal(screen.Width, ControlWiringFace.NumberValue(view, "NumScreenWidth"));
                Assert.Equal(screen.Height, ControlWiringFace.NumberValue(view, "NumScreenHeight"));

                foreach (var spec in Specs)
                {
                    ControlWiringFace.WriteNumber(view, spec.Name, spec.Value);
                    var after = spec.Read(screen);
                    Assert.True(spec.Expected == after,
                        $"{spec.Name}（{spec.Setting}）驱动 {spec.Value} 后读到 {after}，期望 {spec.Expected}");
                }

                // 反面见证：两条接线各自只写自己那一格（改宽不会顺手改高）
                ControlWiringFace.WriteNumber(view, "NumScreenWidth", 800);
                Assert.Equal(800, screen.Width);
                Assert.Equal(720, screen.Height);

                Assert.Same(screen, project.Screens[0]);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>**单文件双向差集**：两个具名数字框都有驱动条目（这个文件没有"没接线"的框）。</summary>
    [Fact]
    public void EveryNumberBoxAndWiring_IsDrivenByTheTable()
    {
        ControlWiringFace.AssertFullyDrivenNumberBoxes(
            "ScreenManagerView.axaml", Specs.Select(s => s.Name).ToArray(), scanFloor: 2);
    }
}
