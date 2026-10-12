using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// Arc / Knob 的<b>绘制方向</b>行为测试 —— 走 <see cref="DesignCanvas.Render"/> 的真实绘制操作，
/// 断言弧画在哪个象限。
///
/// <para><b>为什么值一组：</b>2026-09-14 修掉一个镜像缺陷 —— Arc 控件的设计器渲染曾按
/// <c>cy − r·sin</c> 画（LVGL 是 <c>cy + r·sin</c>），同一个 <c>MinValue/MaxValue</c>
/// 配置在设计器里与屏幕上正好上下颠倒。修完必须把「画在哪」钉住，
/// 否则谁都能再把符号翻回去而不被发现（见 docs/2026-09-14-arc-angle-convention.md）。</para>
///
/// <para><b>为什么不用像素断言：</b>本用例走 headless 的绘制录放器即可 —— Arc 控件的弧是
/// 折线几何（<c>asPolyline: true</c>），不是被 headless 桩掉的 <c>ArcTo</c>，
/// 所以 <c>Bounds</c> 真实可用，比截图像素更稳、更快。</para>
/// </summary>
public class ArcControlRenderTests
{
    private const double ControlSize = 180;

    /// <summary>控件矩形与圆心（ZoomLevel=1，屏幕原点即画布原点）。</summary>
    private static (Rect Rect, Point Center) Layout()
    {
        var rect = new Rect(20, 20, ControlSize, ControlSize);
        return (rect, rect.Center);
    }

    private static List<Drawing> RecordArc(double min, double max, int value)
    {
        var screen = new HmiScreen { Width = 260, Height = 260, BgColor = "#0B1220" };
        screen.Controls.Add(new HmiControl
        {
            Id = "arc", Name = "arc", Type = HmiControlType.Arc,
            X = 20, Y = 20, Width = ControlSize, Height = ControlSize,
            MinValue = min, MaxValue = max, Value = value,
            BgColor = "#374151", IndicatorColor = "#22C55E", BorderWidth = 14,
        });
        return CanvasRenderSpy.Record(new DesignCanvas { Screen = screen, ZoomLevel = 1.0 });
    }

    /// <summary>取「有画笔、无填充」的描边几何 —— 弧线画法就是描边（无填充）。</summary>
    private static List<Rect> StrokedBounds(IEnumerable<Drawing> ops) =>
        ops.OfType<GeometryDrawing>()
            .Where(d => d.Pen is not null && d.Brush is null && d.Geometry is not null)
            .Select(d => d.Geometry!.Bounds)
            .ToList();

    /// <summary>
    /// 0°..90° 的四分之一弧：LVGL 里从 3 点钟顺时针扫到 6 点钟 → 必须落在<b>右下</b>象限。
    /// 镜像缺陷下会落在右上象限（<c>Bounds.Y</c> 从 cy 变成 cy−r）。
    /// </summary>
    [Fact]
    public void ArcTrack_ZeroToNinety_DrawsInTheBottomRightQuadrant()
    {
        UiTestHost.Run(() =>
        {
            var (_, center) = Layout();
            var bounds = StrokedBounds(RecordArc(0, 90, 0));
            var half = ControlSize / 2;

            var inBottomRight = bounds.Any(b =>
                Near(b.X, center.X) && Near(b.Y, center.Y) &&
                Near(b.Width, half) && Near(b.Height, half));

            Assert.True(inBottomRight,
                $"0..90° 的轨道弧没有落在右下象限（原点应为 {center}、边长 {half}）。录到的描边包围盒：{Dump(bounds)}");
        });
    }

    /// <summary>
    /// 0..270° 的轨道 + 0..135° 的指示弧：指示弧从 3 点钟顺时针扫到 7:30，必须整个落在<b>下半</b>。
    /// 镜像缺陷下会整个落在上半。
    /// </summary>
    [Fact]
    public void ArcIndicator_SweepsClockwiseFromThreeOClock()
    {
        UiTestHost.Run(() =>
        {
            var (_, center) = Layout();
            var bounds = StrokedBounds(RecordArc(0, 270, 135));

            // 存在一条「够宽、够高、完全在下半」的描边 = 指示弧
            var hasLowerHalfArc = bounds.Any(b =>
                b.Width > ControlSize * 0.5 && b.Height > ControlSize * 0.4 &&
                b.Y >= center.Y - 1);
            Assert.True(hasLowerHalfArc,
                $"指示弧没有落在下半平面（顺时针方向错了？）。录到的描边包围盒：{Dump(bounds)}");

            // 反向证据：不该存在「够宽、够高、完全在上半」的弧
            var hasUpperHalfArc = bounds.Any(b =>
                b.Width > ControlSize * 0.5 && b.Height > ControlSize * 0.4 &&
                b.Y + b.Height <= center.Y + 1);
            Assert.False(hasUpperHalfArc,
                $"出现了整个落在上半平面的弧 —— 方向被翻回去了。录到的描边包围盒：{Dump(bounds)}");
        });
    }

    /// <summary>
    /// Knob 指针起点必须与固件 <c>lv_arc</c> 指示弧起点一致：Value = MinValue 时指向 135°，
    /// 即圆心<b>左下</b>方向（<c>Bounds</c> 的 X 小于圆心、底边等于圆心 Y）。
    /// 原先 <c>PointerStart = -135</c> 会指向 10:30（左上）。
    /// </summary>
    [Fact]
    public void KnobPointer_AtMinimumValue_PointsDownLeft()
    {
        UiTestHost.Run(() =>
        {
            var screen = new HmiScreen { Width = 260, Height = 260, BgColor = "#0B1220" };
            screen.Controls.Add(new HmiControl
            {
                Id = "knob", Name = "knob", Type = HmiControlType.Knob,
                X = 20, Y = 20, Width = ControlSize, Height = ControlSize,
                MinValue = 0, MaxValue = 100, Value = 0,
                BgColor = "#F59E0B",
            });

            var ops = CanvasRenderSpy.Record(new DesignCanvas { Screen = screen, ZoomLevel = 1.0 });
            var (_, center) = Layout();

            // 指针是一条从圆心画出的线：短、有画笔、无填充
            var pointer = StrokedBounds(ops)
                .Where(b => b.Width > 1 && b.Height > 1
                            && b.Width < ControlSize * 0.6 && b.Height < ControlSize * 0.6)
                .ToList();

            Assert.True(pointer.Count > 0, $"没录到指针线段。录到的描边包围盒：{Dump(StrokedBounds(ops))}");

            var p = pointer[0];
            Assert.True(p.X + p.Width <= center.X + 1,
                $"指针应指向左下（包围盒右缘不得越过圆心 X={center.X}），实得 {p}");
            Assert.True(p.Y >= center.Y - 1 && p.Y + p.Height > center.Y,
                $"指针应自圆心向下延伸（包围盒顶边 = 圆心 Y={center.Y}），实得 {p}");
        });
    }

    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) <= 1.5;

    private static string Dump(IEnumerable<Rect> rects) =>
        string.Join(" | ", rects.Select(r => $"{r.X:F0},{r.Y:F0} {r.Width:F0}x{r.Height:F0}"));
}
