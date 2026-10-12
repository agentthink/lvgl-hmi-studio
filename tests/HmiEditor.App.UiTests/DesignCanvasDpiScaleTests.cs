using System;
using Avalonia;
using Avalonia.Controls;
using HmiEditor.App.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **屏幕 DPI 缩放 ⇒ 画布有效缩放**（阶段 222，登记 `#98` 的后半）。
///
/// <para><b>为什么单独一面</b>：DPI 缩放必须同时作用在**渲染**与**命中/坐标换算**上 ——
/// 只改一边的症状是"画得对、拖拽错位"（或反过来），而这种错位在静态看代码时看不出来。
/// 所以这一面成对判：① `EffectiveZoom = ZoomLevel × DpiScale`；② 坐标换算（拖拽走的
/// 就是这条链）；③ **渲染侧**的几何（安全区参考线按有效缩放算）；④ 默认 1.0 时一切照旧。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class DesignCanvasDpiScaleTests
{
    private static HmiScreen Screen() => new() { Id = "s1", Name = "Main", Width = 800, Height = 480 };

    [Fact]
    public void DefaultDpiScale_KeepsEverythingAsBefore()
    {
        var canvas = new DesignCanvas { Screen = Screen(), ZoomLevel = 1.5 };

        Assert.Equal(1.0, canvas.DpiScale);
        Assert.Equal(1.5, canvas.EffectiveZoom);

        Assert.Equal(new Point(100, 50), canvas.ViewportToModel(new Point(150, 75)));
    }

    [Fact]
    public void DpiScale_MultipliesTheEffectiveZoom()
    {
        var canvas = new DesignCanvas { Screen = Screen(), ZoomLevel = 2.0, DpiScale = 1.5 };

        Assert.Equal(3.0, canvas.EffectiveZoom);
    }

    /// <summary>
    /// **坐标换算成对缩放**（这就是拖拽的链）：DPI 2× 时视口 (200,100) 对应模型 (100,50)；
    /// 再叠平移 50 也一样对得上。
    /// </summary>
    [Fact]
    public void DpiScale_ScalesTheCoordinateConversion()
    {
        var canvas = new DesignCanvas
        {
            Screen = Screen(),
            ZoomLevel = 1.0,
            DpiScale = 2.0,
        };

        Assert.Equal(new Point(100, 50), canvas.ViewportToModel(new Point(200, 100)));
        Assert.Equal(new Point(200, 100), canvas.ModelToViewport(new Point(100, 50)));

        canvas.PanOffsetX = 50;
        canvas.PanOffsetY = 20;
        Assert.Equal(new Point(75, 40), canvas.ViewportToModel(new Point(200, 100)));
    }

    /// <summary>DPI 值非正（脏数据）⇒ 按 1 处理，绝不让换算出 `Infinity`。</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-2.0)]
    public void NonPositiveDpiScale_IsTreatedAsOne(double dpi)
    {
        var canvas = new DesignCanvas { Screen = Screen(), ZoomLevel = 1.0, DpiScale = dpi };

        Assert.Equal(1.0, canvas.EffectiveZoom);
        var model = canvas.ViewportToModel(new Point(120, 60));
        Assert.True(double.IsFinite(model.X) && double.IsFinite(model.Y), $"换算出了非有限值：{model}");
        Assert.Equal(new Point(120, 60), model);
    }

    /// <summary>
    /// **渲染侧也认这个缩放**：安全区参考线按 `EffectiveZoom` 计算 ——
    /// 与坐标换算（命中侧）成对，这才叫"接线接全了"。
    /// </summary>
    [Fact]
    public void RenderSideGeometry_AlsoUsesTheDpiScale()
    {
        var canvas = new DesignCanvas
        {
            Screen = Screen(),
            ZoomLevel = 1.0,
            DpiScale = 2.0,
            SafePadding = new Thickness(10, 10, 10, 10),
        };

        var guides = canvas.SafeAreaGuides();

        Assert.Equal(4, guides.Count);
        Assert.Equal(new Point(20, 20), guides[0].From);          // 10 × (1 × 2)
        Assert.Equal(new Point(1580, 20), guides[0].To);          // (800-10) × 2
    }

    /// <summary>宿主那条线：工程的 `DpiScale` 经 `SetScreen(screen, project)` 进到画布。</summary>
    [Fact]
    public void ProjectDpiScale_ReachesTheCanvas()
    {
        UiTestHost.Run(() =>
        {
            var project = new HmiProject { Name = "P" };
            project.ScreenDefaultsConfig.DpiScale = 1.75;

            var view = new DesignCanvasView();
            view.SetScreen(Screen(), project);
            Assert.Equal(1.75, view.Canvas.DpiScale);

            // 换成没有配置的工程 ⇒ 回到 1.0（不残留）
            view.SetScreen(Screen(), new HmiProject { Name = "Q" });
            Assert.Equal(1.0, view.Canvas.DpiScale);
        });
    }
}
