using System.Linq;
using Avalonia.Media;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// Gauge 刻度渲染的行为测试 —— 取代原先读 <c>DesignCanvas.cs</c> 源码做
/// <c>Assert.Contains("control.MajorTickCount", CanvasSource)</c> 的字符串守卫。
///
/// <para>旧守卫只能证明「源码里出现过这个属性名」；这里直接渲染并数画出来的刻度线，
/// 证明<b>真的按 MajorTickCount 画了那么多条</b>。渲染实现搬到哪个文件都不影响。</para>
///
/// <para>判据：录制绘制操作后，数「有画笔、无填充、包围盒最长边 ≤ 刻度线长度」的几何节点
/// （刻度线长 8*z=8px，而两条弧的包围盒是 160px 量级，区分度足够）。</para>
/// </summary>
public class GaugeTickRenderTests
{
    private static DesignCanvas MakeCanvas(int majorTickCount, double zoom = 1.0)
    {
        var screen = new HmiScreen { Width = 400, Height = 300 };
        screen.Controls.Add(new HmiControl
        {
            Id = "g1",
            Name = "g1",
            Type = HmiControlType.Gauge,
            Text = "50",
            X = 10, Y = 20, Width = 160, Height = 160,
            MinValue = 0, MaxValue = 100, Value = 50,
            MajorTickCount = majorTickCount,
            BgColor = "#0EA5E9",
            TextColor = "#111827",
            BorderWidth = 2,
        });
        return new DesignCanvas { Screen = screen, ZoomLevel = zoom };
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(20)]
    public void RenderGauge_DrawsExactlyMajorTickCountTicks(int majorTickCount)
    {
        UiTestHost.Run(() =>
        {
            var canvas = MakeCanvas(majorTickCount);
            var ops = CanvasRenderSpy.Record(canvas);

            Assert.Equal(majorTickCount, CanvasRenderSpy.CountStrokedSegments(ops));
        });
    }

    [Fact]
    public void RenderGauge_TickCountBelowMinimum_DrawsTwoTicks()
    {
        UiTestHost.Run(() =>
        {
            // MajorTickCount = 1 时分母为 0，必须收敛成 2 条而不是崩/画 1 条
            var ops = CanvasRenderSpy.Record(MakeCanvas(1));
            Assert.Equal(2, CanvasRenderSpy.CountStrokedSegments(ops));
        });
    }

    [Fact]
    public void RenderGauge_MoreTicks_MeansMoreDrawnSegments()
    {
        UiTestHost.Run(() =>
        {
            // 单调性：刻度数增长时画出的线段必须单调增长（防止「参数被吞掉」类回归）
            var few = CanvasRenderSpy.CountStrokedSegments(CanvasRenderSpy.Record(MakeCanvas(3)));
            var many = CanvasRenderSpy.CountStrokedSegments(CanvasRenderSpy.Record(MakeCanvas(16)));

            Assert.True(many > few, $"刻度 16 画出的线段数 {many} 未多于刻度 3 的 {few}");
        });
    }

    [Fact]
    public void RenderGauge_StillDrawsArcAndText_AlongsideTicks()
    {
        UiTestHost.Run(() =>
        {
            // 刻度不能再多也不能吞掉弧与数值文本
            const int ticks = 5;
            var ops = CanvasRenderSpy.Record(MakeCanvas(ticks));

            // 刻度线：短描边
            Assert.Equal(ticks, CanvasRenderSpy.CountStrokedSegments(ops));

            // 弧：长描边（背景弧 + 活动弧，包围盒远大于刻度线）
            var allStrokes = CanvasRenderSpy.CountStrokedSegments(ops, maxExtent: double.MaxValue);
            var longStrokes = allStrokes - ticks;
            Assert.True(longStrokes >= 2,
                $"长描边只有 {longStrokes} 条，Gauge 的背景弧/活动弧可能没画（全部描边 {allStrokes} 条）");

            // 数值文本仍被绘制
            var census = string.Join(", ", ops
                .GroupBy(d => d.GetType().Name)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key}:{g.Count()}"));
            Assert.True(ops.Any(d => d is GlyphRunDrawing),
                $"Gauge 数值文本没有被绘制。录到的绘制节点类型：{census}");
        });
    }
}
