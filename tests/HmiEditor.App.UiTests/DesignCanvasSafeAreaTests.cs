using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using HmiEditor.App.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **屏幕安全区 ⇒ 画布参考线**（阶段 221，登记 `#98` 的"接线"那半 —— 先接**预览**这条链）。
///
/// <para><b>此前</b>：`ScreenDefaultsConfig.SafeAreaPadding` 只有两个设置面板读写，画布/导出都不看它
/// （阶段 216 量出来的"死设置"）。这一阶段把**安全区**接进画布：非零内边距 ⇒ 画一圈**虚线参考线**
/// （只影响显示、不动几何）。</para>
///
/// <para><b>三道</b>：① 参考线几何算得对（画布坐标 ＝ 模型 × 缩放）；② 渲染**真的画了它**
/// （用渲染期计数 `SafeGuidesDrawnCount`，不是只看"算得出来"）；③ 工程的 `SafeAreaPadding`
/// 经 <see cref="DesignCanvasView.ApplyProjectScreenDefaults"/> 真的进到画布。</para>
///
/// <para>⚠ `DpiScale`（屏幕 DPI 缩放）这一阶段**没接**：它要接进画布坐标换算与渲染比例，
/// 牵动命中测试，单独做（专题里记着）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class DesignCanvasSafeAreaTests
{
    private static HmiScreen Screen1075() => new() { Id = "s1", Name = "Main", Width = 800, Height = 480 };

    [Fact]
    public void NoPadding_MeansNoGuides()
    {
        var canvas = new DesignCanvas { Screen = Screen1075() };

        Assert.Empty(canvas.SafeAreaGuides());
    }

    [Fact]
    public void Padding_ProducesFourGuidesInCanvasCoordinates()
    {
        var canvas = new DesignCanvas
        {
            Screen = Screen1075(),
            ZoomLevel = 2.0,
            SafePadding = new Thickness(10, 20, 30, 40),   // 左 10 上 20 右 30 下 40（模型像素）
        };

        var guides = canvas.SafeAreaGuides();

        // 画布坐标 ＝ 模型 × 缩放：内缩矩形 (10,20)–(770,440) ⇒ ×2 ⇒ (20,40)–(1540,880)
        Assert.Equal(4, guides.Count);
        Assert.Equal(new Point(20, 40), guides[0].From);
        Assert.Equal(new Point(1540, 40), guides[0].To);
        Assert.Equal(new Point(1540, 880), guides[2].From);
        Assert.Equal(new Point(20, 880), guides[2].To);
    }

    [Fact]
    public void InvertedPadding_IsNormalised()
    {
        // 左边距比屏幕还宽（脏数据）⇒ 不能画出"负宽"的矩形
        var canvas = new DesignCanvas
        {
            Screen = Screen1075(),
            SafePadding = new Thickness(900, 500, 0, 0),
        };

        var guides = canvas.SafeAreaGuides();

        Assert.Equal(4, guides.Count);
        // ⚠ 第一版写的是 `To.X >= From.X || To.Y >= From.Y`（或）—— 那**判不出**反向线段
        //   （(900,500)→(800,500) 的 Y 相等、或为真 ⇒ 判据放过）。负控 nc3 当场把这个弱点暴露出来，
        //   改成**逐条判方向**：上边指向右、右边指向下。
        Assert.True(guides[0].To.X > guides[0].From.X,
            $"上边反向：{guides[0].From} → {guides[0].To}");
        Assert.True(guides[1].To.Y > guides[1].From.Y,
            $"右边反向：{guides[1].From} → {guides[1].To}");
    }

    /// <summary>
    /// **渲染真的画了参考线**：跑一次真实渲染，断言渲染期计数 ＝ 4；没有内边距时 ＝ 0。
    /// （只断言"算得出来"是不够的 —— 渲染里漏掉那行调用，用户什么也看不到。）
    /// </summary>
    [Fact]
    public void Render_DrawsTheGuides()
    {
        UiTestHost.Run(() =>
        {
            var canvas = new DesignCanvas
            {
                Screen = Screen1075(),
                SafePadding = new Thickness(10, 10, 10, 10),
            };
            var window = new Window { Width = 900, Height = 600, Content = canvas };
            window.Show();
            UiTestHost.Pump(5);

            try
            {
                // ⚠ headless 下 `InvalidateVisual()` + `Pump()` **不会**真的跑 `Render`
                //   （UI 测试宿主不产像素）⇒ 用 `RenderTargetBitmap` 强制走一遍真实渲染
                //   （与 CanvasRenderBaselineTests 同一手法）。
                RenderOnce(window);
                Assert.Equal(4, canvas.SafeGuidesDrawnCount);

                canvas.SafePadding = default;
                RenderOnce(window);
                Assert.Equal(0, canvas.SafeGuidesDrawnCount);
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>强制走一遍真实渲染（headless 不产像素 ⇒ 用 RenderTargetBitmap 取 DrawingContext）。</summary>
    private static void RenderOnce(Window window)
    {
        using var rtb = new RenderTargetBitmap(new PixelSize(900, 600));
        rtb.Render(window);
        UiTestHost.Pump(2);
    }

    /// <summary>工程的 `SafeAreaPadding` 真的进到画布（宿主那条线：`ApplyProjectScreenDefaults`）。</summary>
    [Fact]
    public void ProjectSafeArea_ReachesTheCanvas()
    {
        UiTestHost.Run(() =>
        {
            var project = new HmiProject { Name = "P" };
            project.ScreenDefaultsConfig.SafeAreaPadding = new ScreenPadding { Left = 8, Top = 12, Right = 16, Bottom = 20 };

            var view = new DesignCanvasView();
            // 宿主那条线（阶段 221）：`SetScreen(screen, project)` 一次到位
            // （⚠ 不在 `MainWindow` 里多写一行 —— 那个文件有"行数不许涨"的治理守卫）
            view.SetScreen(Screen1075(), project);

            Assert.Equal(new Thickness(8, 12, 16, 20), view.Canvas.SafePadding);

            // 没有安全区配置 ⇒ 归零（不残留上一次的参考线）
            view.ApplyProjectScreenDefaults(new HmiProject { Name = "Q" });
            Assert.Equal(default, view.Canvas.SafePadding);
        });
    }
}
