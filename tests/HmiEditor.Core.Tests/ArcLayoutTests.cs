using System;
using HmiEditor.Core.Design;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 角度约定的行为测试 —— 把「LVGL 是顺时针为正」这件事钉死。
///
/// <para><b>为什么值得单独写一组：</b>设计器曾经同时存在两套互相镜像的弧角度约定，
/// 而两边的注释都声称自己是「LVGL 那一套」，Arc 控件因此在设计器里上下颠倒
/// （详见 <c>docs/2026-09-14-arc-angle-convention.md</c>）。谁都能改注释，
/// 但改不动这里的断言 —— 约定一旦被翻回去，这组测试立刻红。</para>
///
/// <para>判据依据 LVGL v9 源码：<c>src/misc/lv_math.c</c> 的
/// <c>lv_trigo_sin(90)=+32767</c>、<c>(270)=−32768</c>，配合
/// <c>src/draw/lv_draw_arc.c</c> 的 <c>x = cx + r·cosθ</c>、<c>y = cy + r·sinθ</c>。</para>
/// </summary>
public class ArcLayoutTests
{
    // ── 角度 → 坐标：符号就是约定本身 ─────────────────────────────

    [Fact]
    public void PointAt_ZeroDegrees_IsThreeOClock()
    {
        var (x, y) = ArcLayout.PointAt(0, 0, 10, 0);

        Assert.Equal(10, x, 9);
        Assert.Equal(0, y, 9);
    }

    /// <summary>
    /// 核心断言：90° 必须落在屏幕<b>下方</b>（<c>y = +r</c>），即<b>顺时针为正</b>。
    ///
    /// <para>若有人按「逆时针为正」去「修正」实现，这里会变成 <c>y = -r</c>（上方）——
    /// 那正是本次修掉的镜像缺陷。</para>
    /// </summary>
    [Fact]
    public void PointAt_NinetyDegrees_IsAtTheBottom_SoAnglesIncreaseClockwise()
    {
        var (x, y) = ArcLayout.PointAt(0, 0, 10, 90);

        Assert.Equal(0, x, 9);
        Assert.Equal(10, y, 9);   // y 向下为正 → 屏幕下方
    }

    [Theory]
    [InlineData(0, 10, 0)]        // 3 点钟
    [InlineData(90, 0, 10)]       // 6 点钟（下）
    [InlineData(180, -10, 0)]     // 9 点钟
    [InlineData(270, 0, -10)]     // 12 点钟（上）
    [InlineData(45, 7.0710678, 7.0710678)]    // 4:30
    [InlineData(315, 7.0710678, -7.0710678)]  // 1:30
    public void PointAt_MatchesLvglClockwiseMapping(double angle, double ex, double ey)
    {
        var (x, y) = ArcLayout.PointAt(0, 0, 10, angle);

        Assert.Equal(ex, x, 6);
        Assert.Equal(ey, y, 6);
    }

    [Fact]
    public void PointAt_IgnoresCenterOffset_ButKeepsRadius()
    {
        var (x, y) = ArcLayout.PointAt(40, 70, 25, 90);

        Assert.Equal(40, x, 9);
        Assert.Equal(95, y, 9);
    }

    // ── 量程常量 ────────────────────────────────────────────────

    [Fact]
    public void Sweep_RunsFrom135To405_WhichIsTheUpperThreeQuarters()
    {
        Assert.Equal(135.0, ArcLayout.StartAngle, 9);
        Assert.Equal(270.0, ArcLayout.SweepAngle, 9);

        // 起点在左下(7:30)、终点 405°≡45° 在右下(4:30) → 下方 90° 留空
        var (sx, sy) = ArcLayout.PointAt(0, 0, 10, ArcLayout.StartAngle);
        Assert.True(sx < 0 && sy > 0, $"起点应落在左下，实得 ({sx}, {sy})");

        var (ex, ey) = ArcLayout.PointAt(0, 0, 10, ArcLayout.StartAngle + ArcLayout.SweepAngle);
        Assert.True(ex > 0 && ey > 0, $"终点应落在右下，实得 ({ex}, {ey})");
    }

    /// <summary>
    /// Knob 指针起点必须与固件 <c>lv_arc</c> 指示弧起点重合（rotation=135 + bg_angles(0,270)）。
    /// 原先写的 <c>-135</c> 落在 10:30 方向，比固件偏了 90°。
    /// </summary>
    [Fact]
    public void PointerStart_CoincidesWithTheArcStart()
    {
        Assert.Equal(ArcLayout.StartAngle, ArcLayout.PointerStart, 9);

        var (px, py) = ArcLayout.PointAt(0, 0, 10, ArcLayout.PointerStart);
        var (sx, sy) = ArcLayout.PointAt(0, 0, 10, ArcLayout.StartAngle);

        Assert.Equal(sx, px, 9);
        Assert.Equal(sy, py, 9);
    }

    [Fact]
    public void PointerDefault_IsSixtyPercentOfTheRange()
    {
        var expected = ArcLayout.StartAngle + 0.6 * ArcLayout.SweepAngle;

        Assert.Equal(ArcLayout.PointerDefault, expected, 9);
        Assert.Equal(ArcLayout.DefaultActiveSweep, 0.6 * ArcLayout.SweepAngle, 9);
    }

    [Fact]
    public void PointerSweep_NeverLeavesTheArcRange()
    {
        var min = ArcLayout.PointerStart;
        var max = ArcLayout.PointerStart + ArcLayout.SweepAngle;

        foreach (var progress in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
        {
            var angle = ArcLayout.PointerStart + progress * ArcLayout.SweepAngle;
            Assert.InRange(angle, min - 1e-9, max + 1e-9);
        }

        Assert.InRange(ArcLayout.PointerDefault, min - 1e-9, max + 1e-9);
    }
}
