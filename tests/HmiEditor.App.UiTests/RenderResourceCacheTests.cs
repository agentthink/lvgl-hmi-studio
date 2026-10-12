using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 渲染资源缓存的行为测试。
///
/// <para>缓存是"提升性能但可能改变行为"的典型手法，所以这里盯两件事：
/// ① 同键必须复用、异键必须区分（避免把不同颜色/字号错当成同一个）；
/// ② **冷缓存与热缓存的绘制操作必须逐条一致** —— 这是"缓存没有改变渲染结果"的直接证据。</para>
///
/// <para><b>全部用例都跑在 headless UI 线程上</b>（<see cref="UiTestHost.Run"/>）：
/// 缓存是进程级静态状态，而渲染也在同一份状态上读写。
/// 如果从别的线程并发访问（xUnit 默认按测试类并行），会和渲染抢同一个 Dictionary；
/// 另外几何对象的 <c>Bounds</c> 需要平台渲染接口，离开 UI 线程会抛
/// <c>Unable to locate 'Avalonia.Platform.IPlatformRenderInterface'</c>。</para>
/// </summary>
public class RenderResourceCacheTests
{
    [Fact]
    public void Brush_SameKey_ReturnsSameInstance()
    {
        UiTestHost.Run(() =>
        {
            RenderResourceCache.Clear();
            Assert.Same(RenderResourceCache.Brush(Colors.Red), RenderResourceCache.Brush(Colors.Red));
        });
    }

    [Fact]
    public void Brush_OpacityIsPartOfTheKey()
    {
        UiTestHost.Run(() =>
        {
            RenderResourceCache.Clear();
            var solid = RenderResourceCache.Brush(Colors.Red);
            var translucent = RenderResourceCache.Brush(Colors.Red, 0.15);

            Assert.NotSame(solid, translucent);
            Assert.Equal(1.0, solid.Opacity, 6);
            Assert.Equal(0.15, translucent.Opacity, 6);
        });
    }

    [Fact]
    public void Pen_IsReusedPerColorThicknessAndCap()
    {
        UiTestHost.Run(() =>
        {
            RenderResourceCache.Clear();
            var p1 = RenderResourceCache.Pen(Colors.Blue, 2);
            var p2 = RenderResourceCache.Pen(Colors.Blue, 2);

            Assert.Same(p1, p2);
            Assert.NotSame(p1, RenderResourceCache.Pen(Colors.Blue, 3));
            Assert.NotSame(p1, RenderResourceCache.Pen(Colors.Blue, 2, PenLineCap.Round));
            Assert.NotSame(p1, RenderResourceCache.Pen(Colors.Blue, 2, PenLineCap.Flat, PenLineJoin.Round));
        });
    }

    [Fact]
    public void Pen_FromSolidBrush_SharesKeyWithColorOverload()
    {
        UiTestHost.Run(() =>
        {
            RenderResourceCache.Clear();
            var fromColor = RenderResourceCache.Pen(Colors.Blue, 2);
            var fromBrush = RenderResourceCache.Pen(RenderResourceCache.Brush(Colors.Blue), 2);

            // 同一个颜色 + 线宽，不管走哪个重载都该命中同一条缓存
            Assert.Same(fromColor, fromBrush);
        });
    }

    [Fact]
    public void Pen_FromTranslucentBrush_KeepsOpacity()
    {
        UiTestHost.Run(() =>
        {
            RenderResourceCache.Clear();
            var translucent = RenderResourceCache.Pen(RenderResourceCache.Brush(Colors.Blue, 0.9), 2);
            var opaque = RenderResourceCache.Pen(Colors.Blue, 2);

            Assert.NotSame(opaque, translucent);

            var brush = Assert.IsAssignableFrom<ISolidColorBrush>(translucent.Brush);
            Assert.Equal(0.9, brush.Opacity, 6);
        });
    }

    [Fact]
    public void Text_IsReusedAndKeepsItsMeasurements()
    {
        UiTestHost.Run(() =>
        {
            RenderResourceCache.Clear();
            var brush = RenderResourceCache.Brush(Colors.White);

            var a = RenderResourceCache.Text("标签", 14, brush);
            var b = RenderResourceCache.Text("标签", 14, brush);

            Assert.Same(a, b);
            Assert.Equal(a.Width, b.Width, 6);
            Assert.Equal(a.Height, b.Height, 6);

            // 复用实例读 Width 应显著便宜于「重新构造 + 读 Width」——
            // 构造后的首次读会触发惰性文本布局（实测约 3 KB），复用省掉的正是这一笔。
            //
            // 注意：不能把「构造」那一侧写成 RenderResourceCache.Text(...) + 读 Width，
            // 因为 PerCall 的热身会把那个键捂热，此后每轮都只是读缓存 —— 两侧恒为 0，
            // 断言就永远失败。基线必须绕过缓存、直接 new（这才是复用的对照物）。
            var constructAndRead = PerCall(500, () =>
            {
                var ft = new FormattedText("标签", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 14, brush);
                _ = ft.Width;
            });
            var cachedRead = PerCall(500, () => _ = b.Width);

            Assert.True(cachedRead * 2 < constructAndRead,
                $"复用读取 Width 的分配（{cachedRead:N0} B）没有明显低于构造+首次读（{constructAndRead:N0} B）");
        });
    }

    [Fact]
    public void Text_SizeColorAndContentArePartOfTheKey()
    {
        UiTestHost.Run(() =>
        {
            RenderResourceCache.Clear();
            var white = RenderResourceCache.Brush(Colors.White);
            var red = RenderResourceCache.Brush(Colors.Red);

            var baseline = RenderResourceCache.Text("标签", 14, white);
            Assert.NotSame(baseline, RenderResourceCache.Text("标签", 16, white));
            Assert.NotSame(baseline, RenderResourceCache.Text("标签", 14, red));
            Assert.NotSame(baseline, RenderResourceCache.Text("其它", 14, white));
        });
    }

    [Fact]
    public void Arc_IsReusedPerRectAndAngles()
    {
        UiTestHost.Run(() =>
        {
            RenderResourceCache.Clear();
            var rect = new Rect(0, 0, 100, 100);

            var a = RenderResourceCache.Arc(rect, 135, 270, asPolyline: true);
            var b = RenderResourceCache.Arc(rect, 135, 270, asPolyline: true);

            Assert.Same(a, b);
            Assert.NotSame(a, RenderResourceCache.Arc(rect, 135, 270, asPolyline: false));
            Assert.NotSame(a, RenderResourceCache.Arc(rect, 135, 180, asPolyline: true));
            Assert.NotSame(a, RenderResourceCache.Arc(new Rect(0, 0, 80, 80), 135, 270, asPolyline: true));
        });
    }

    /// <summary>
    /// 弧的方向必须是 LVGL 的方向：135° 起顺时针扫 270° → 经过最左 / 最上 / 最右，
    /// <b>下方 90° 留空</b>。
    ///
    /// <para>这是 2026-09-14 修的缺陷的回归守卫。当时 <c>DrawLvglArcGeometry</c> 用
    /// <c>cy − r·sin</c> 实现，画出来是竖直镜像（弧口朝下、下方铺满、上方留空），
    /// Arc 控件的设计器渲染与模拟器正好上下颠倒。判据取「<c>Bounds.Y</c> 是否触到顶」
    /// —— 镜像后 <c>Bounds.Y</c> 会从 0 跳到 14.6，区分度足够。</para>
    ///
    /// <para>用折线版（<c>asPolyline: true</c>）断言：headless 下 Avalonia 的 <c>ArcTo</c>
    /// 是桩实现（只记端点、不算弧），<c>asPolyline: false</c> 的 <c>Bounds</c> 不可信。</para>
    /// </summary>
    [Fact]
    public void LvglArc_LeavesTheBottomOpen()
    {
        UiTestHost.Run(() =>
        {
            var rect = new Rect(0, 0, 100, 100);          // 圆心 (50,50)、r=50
            var arc = RenderResourceCache.Arc(rect, 135, 270, asPolyline: true);
            var b = arc.Bounds;

            // 最左(180°)、最右(360°) 在采样点上 → 横向铺满整框
            Assert.Equal(100, b.Width, 1);
            // 最上(270°) 在采样点上 → 触到顶
            Assert.Equal(0, b.Y, 1);
            // 起点(135°) 的 y = cy + r·sin135° = 50 + 35.36 = 85.36 → 下沿只到 85.36，
            // 即下方 90°（从 4:30 到 7:30 之间）留空
            Assert.Equal(85.36, b.Y + b.Height, 1);
        });
    }

    [Fact]
    public void ColdAndWarmRender_ProduceIdenticalDrawingOperations()
    {
        UiTestHost.Run(() =>
        {
            var canvas = MakeMixedCanvas();

            RenderResourceCache.Clear();
            var cold = CanvasRenderSpy.FingerprintOf(canvas);
            var warm = CanvasRenderSpy.FingerprintOf(canvas);
            var warmAgain = CanvasRenderSpy.FingerprintOf(canvas);

            Assert.Equal(cold, warm);
            Assert.Equal(cold, warmAgain);
            Assert.True(cold.Length > 100, $"录到的绘制操作太少，指纹为：{cold}");
        });
    }

    /// <summary>
    /// 回归：跨 headless Dispatch 复用缓存实例时，文本必须仍然画得出来。
    ///
    /// <para><b>原缺陷（实测根因 R3）：</b><see cref="FormattedText"/> 背后的平台文本对象会随
    /// 每次 Dispatch 结束被释放。下一次复用该缓存实例读 <c>Width</c>（触发惰性布局）时抛
    /// <c>ObjectDisposedException</c>，而当时 <c>DrawAlignedText</c> 是个裸的 <c>catch { }</c>
    /// —— 于是整屏文字**静默消失**，现场没有任何线索（分配量证据显示"确实没画"，
    /// 但录制节点里连 <c>GlyphRunDrawing</c> 都没有）。</para>
    ///
    /// <para>修法：捕获 → <see cref="RenderResourceCache.InvalidateTexts"/> → 重建重试一次。
    /// 本用例先跑一次 Dispatch 把缓存捂热，再在<b>后续各自独立的 Dispatch</b> 里断言文本仍被绘制 ——
    /// 正是原缺陷的触发条件。</para>
    /// </summary>
    [Fact]
    public void TextIsStillDrawn_WhenCachedFormattedTextOutlivesItsDispatch()
    {
        UiTestHost.Run(() =>
        {
            RenderResourceCache.Clear();
            _ = CanvasRenderSpy.Record(MakeTextCanvas());   // 捂热：在本次 Dispatch 内建好缓存
        });

        for (var round = 1; round <= 3; round++)
        {
            var ops = UiTestHost.Run(() => CanvasRenderSpy.Record(MakeTextCanvas()));
            Assert.True(ops.Any(d => d is GlyphRunDrawing),
                $"第 {round} 轮独立 Dispatch 里文本没被绘制 —— 缓存实例失效后没有自愈。"
                + $"缓存计数 {RenderResourceCache.Counts}，文本失效次数 {RenderResourceCache.TextInvalidations}");
        }
    }

    /// <summary>只有一段文本的最小画布 —— 用来盯"文字还在不在"。</summary>
    private static DesignCanvas MakeTextCanvas()
    {
        var screen = new HmiScreen { Width = 300, Height = 120 };
        screen.Controls.Add(new HmiControl
        {
            Id = "t1",
            Name = "t1",
            Type = HmiControlType.Label,
            Text = "文本",
            X = 10, Y = 10, Width = 200, Height = 40,
            TextColor = "#E5E7EB",
            BgColor = "#0EA5E9",
            BorderWidth = 2,
        });
        return new DesignCanvas { Screen = screen, ZoomLevel = 1.0 };
    }

    /// <summary>量单次调用的托管分配字节数（先热身，再取精确计数器差值）。</summary>
    private static double PerCall(int iterations, Action body)
    {
        for (var i = 0; i < 200; i++) body();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++) body();
        return (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations;
    }

    private static DesignCanvas MakeMixedCanvas()
    {
        var screen = new HmiScreen { Width = 500, Height = 400, BgColor = "#111827" };
        var types = new[]
        {
            HmiControlType.Label, HmiControlType.Button, HmiControlType.Gauge,
            HmiControlType.Slider, HmiControlType.Tank, HmiControlType.Arc,
        };
        for (var i = 0; i < types.Length; i++)
        {
            screen.Controls.Add(new HmiControl
            {
                Id = $"c{i}",
                Name = $"c{i}",
                Type = types[i],
                Text = "标签",
                X = 10 + i * 60,
                Y = 20,
                Width = 120,
                Height = 80,
                MinValue = 0,
                MaxValue = 100,
                Value = 50,
                MajorTickCount = 5,
                BgColor = "#0EA5E9",
                IndicatorColor = "#22C55E",
                TextColor = "#E5E7EB",
                BorderWidth = 2,
            });
        }
        return new DesignCanvas { Screen = screen, ZoomLevel = 1.0 };
    }
}
