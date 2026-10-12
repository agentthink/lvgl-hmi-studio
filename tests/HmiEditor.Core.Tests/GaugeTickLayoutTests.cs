using System;
using System.Linq;
using HmiEditor.Core.Design;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// Gauge 主刻度几何的行为测试。
///
/// <para>取代原先「读 <c>DesignCanvas.cs</c> 源码 + <c>Assert.Contains("control.MajorTickCount")</c>"
/// 的字符串守卫 —— 那种写法既验证不了角度是否真的均布、也验证不了端点算得对不对，
/// 而且把渲染实现搬个位置就会碎。</para>
/// </summary>
public class GaugeTickLayoutTests
{
    private const double Eps = 1e-9;

    [Fact]
    public void ComputeAngles_TwoTicks_CoversFullSweep()
    {
        var angles = GaugeTickLayout.ComputeAngles(2);

        Assert.Equal(2, angles.Length);
        Assert.Equal(ArcLayout.StartAngle, angles[0], 9);
        Assert.Equal(ArcLayout.StartAngle + ArcLayout.SweepAngle, angles[1], 9);
    }

    [Fact]
    public void ComputeAngles_DefaultSweep_StartsAt135AndEndsAt405()
    {
        var angles = GaugeTickLayout.ComputeAngles(5);

        Assert.Equal(5, angles.Length);
        Assert.Equal(135.0, angles[0], 9);
        Assert.Equal(405.0, angles[^1], 9);
    }

    [Fact]
    public void ComputeAngles_AreEvenlySpacedAcrossSweep()
    {
        foreach (var n in new[] { 2, 3, 5, 8, 21 })
        {
            var angles = GaugeTickLayout.ComputeAngles(n);
            Assert.Equal(n, angles.Length);

            var expectedStep = ArcLayout.SweepAngle / (n - 1);
            for (var i = 1; i < n; i++)
                Assert.Equal(expectedStep, angles[i] - angles[i - 1], 9);

            // 首尾必须正好落在量程两端，不能漂移到整圆
            Assert.Equal(ArcLayout.StartAngle, angles[0], 9);
            Assert.Equal(ArcLayout.StartAngle + ArcLayout.SweepAngle, angles[^1], 9);
        }
    }

    [Fact]
    public void ComputeAngles_AreStrictlyIncreasing()
    {
        var angles = GaugeTickLayout.ComputeAngles(21);
        for (var i = 1; i < angles.Length; i++)
            Assert.True(angles[i] > angles[i - 1],
                $"第 {i} 条刻度角度 {angles[i]} 未大于前一条 {angles[i - 1]}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-7)]
    public void ComputeAngles_CountBelowMinimum_ClampsToTwo(int tickCount)
    {
        // 少于 2 条时 step 的分母为 0，必须收敛 —— 与渲染端 Math.Max(2, ...) 语义一致
        Assert.Equal(GaugeTickLayout.MinTickCount, GaugeTickLayout.NormalizeCount(tickCount));

        var angles = GaugeTickLayout.ComputeAngles(tickCount);
        Assert.Equal(2, angles.Length);
        Assert.All(angles, a => Assert.False(double.IsNaN(a)));
        Assert.Equal(ArcLayout.StartAngle, angles[0], 9);
        Assert.Equal(ArcLayout.StartAngle + ArcLayout.SweepAngle, angles[1], 9);
    }

    [Fact]
    public void ComputeTicks_OuterEndsOnRing_InnerEndsInwardByTickLength()
    {
        const double cx = 40, cy = 70, radius = 50, tickLen = 8;
        const int count = 8;

        var ticks = GaugeTickLayout.ComputeTicks(cx, cy, radius, tickLen, count);

        Assert.Equal(count, ticks.Length);
        foreach (var t in ticks)
        {
            var outer = Math.Sqrt(Math.Pow(t.X2 - cx, 2) + Math.Pow(t.Y2 - cy, 2));
            var inner = Math.Sqrt(Math.Pow(t.X1 - cx, 2) + Math.Pow(t.Y1 - cy, 2));

            Assert.Equal(radius, outer, 9);
            Assert.Equal(radius - tickLen, inner, 9);
        }
    }

    [Fact]
    public void ComputeTicks_FirstTick_IsAtStartAngleInYDownCoordinates()
    {
        // 设计器坐标系 y 向下：135° → 左下方向 (cos135, sin135) = (-√2/2, +√2/2)
        var ticks = GaugeTickLayout.ComputeTicks(0, 0, 100, 0, 2);

        var half = Math.Sqrt(2) / 2;
        Assert.Equal(-100 * half, ticks[0].X2, 9);
        Assert.Equal(+100 * half, ticks[0].Y2, 9);

        // 末条 405°（≡45°）→ 右下方向
        Assert.Equal(+100 * half, ticks[1].X2, 9);
        Assert.Equal(+100 * half, ticks[1].Y2, 9);
    }

    [Fact]
    public void ComputeTicks_TickDirections_MatchAngles()
    {
        const double cx = 0, cy = 0, radius = 10, tickLen = 2;
        var angles = GaugeTickLayout.ComputeAngles(5);
        var ticks = GaugeTickLayout.ComputeTicks(cx, cy, radius, tickLen, 5);

        for (var i = 0; i < angles.Length; i++)
        {
            var rad = angles[i] * Math.PI / 180;
            Assert.Equal(Math.Cos(rad) * radius, ticks[i].X2, 9);
            Assert.Equal(Math.Sin(rad) * radius, ticks[i].Y2, 9);
        }
    }

    [Fact]
    public void ComputeTicks_TickSegments_AreAllOnTheSweepArc()
    {
        // 回归防护：刻度不得越出 [StartAngle, StartAngle+SweepAngle] 的 270° 弧
        const double cx = 10, cy = 20, radius = 60, tickLen = 8;
        var minAngle = ArcLayout.StartAngle;
        var maxAngle = ArcLayout.StartAngle + ArcLayout.SweepAngle;

        var ticks = GaugeTickLayout.ComputeTicks(cx, cy, radius, tickLen, 21);

        foreach (var t in ticks)
        {
            var deg = Math.Atan2(t.Y2 - cy, t.X2 - cx) * 180 / Math.PI;
            if (deg < 0) deg += 360;            // 归一化到 [0,360)
            var normalized = deg < 90 ? deg + 360 : deg;  // 405° ≡ 45°

            Assert.InRange(normalized, minAngle - 1e-6, maxAngle + 1e-6);
        }
    }
}
