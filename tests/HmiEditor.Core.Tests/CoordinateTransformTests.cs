using HmiEditor.Core.Design;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// FF5 验收测试 —— 拖放落点精度。
/// 验证坐标变换公式 model = (screen - pan) / zoom 在各种缩放和平移场景下的正确性。
/// 验收标准：落点偏差 < 5px。
/// </summary>
public class CoordinateTransformTests
{
    private const double Tolerance = 0.001; // 浮点比较容差

    // ── 测试矩阵（来自 Phase R1 实现计划 §4.3）──

    [Theory]
    [InlineData(400, 240, 1.0, 0, 0, 400, 240)]      // 1.0x, 无平移
    [InlineData(400, 240, 2.0, 0, 0, 200, 120)]      // 2.0x → 落点 = 鼠标 / 2
    [InlineData(400, 240, 0.5, 0, 0, 800, 480)]      // 0.5x → 落点 = 鼠标 * 2
    [InlineData(400, 240, 1.0, 100, 50, 300, 190)]   // 1.0x + 平移(100,50)
    [InlineData(400, 240, 2.0, 100, 50, 150, 95)]    // 2.0x + 平移 → (鼠标-pan)/2
    public void ScreenToModel_PlanMatrix(
        double screenX, double screenY,
        double zoom, double panX, double panY,
        double expectedX, double expectedY)
    {
        var (mx, my) = CoordinateTransform.ScreenToModel(screenX, screenY, panX, panY, zoom);

        Assert.InRange(mx, expectedX - Tolerance, expectedX + Tolerance);
        Assert.InRange(my, expectedY - Tolerance, expectedY + Tolerance);
    }

    // ── 扩展缩放范围（0.25x / 4.0x）──

    [Theory]
    [InlineData(200, 200, 0.25, 0, 0, 800, 800)]   // 0.25x → 4x 放大
    [InlineData(800, 800, 4.0, 0, 0, 200, 200)]   // 4.0x → 1/4 缩小
    public void ScreenToModel_ExtendedZoom(
        double screenX, double screenY,
        double zoom, double panX, double panY,
        double expectedX, double expectedY)
    {
        var (mx, my) = CoordinateTransform.ScreenToModel(screenX, screenY, panX, panY, zoom);

        Assert.InRange(mx, expectedX - Tolerance, expectedX + Tolerance);
        Assert.InRange(my, expectedY - Tolerance, expectedY + Tolerance);
    }

    // ── 精度验证：偏差 < 5px ──

    [Fact]
    public void ScreenToModel_PrecisionUnder5px_AcrossZoomLevels()
    {
        // 模拟鼠标在 (300, 200) 处释放，在各种缩放/平移下计算模型坐标
        // 然后反向变换回屏幕坐标，验证往返偏差 < 5px
        var testCases = new[]
        {
            (zoom: 1.0, panX: 0.0, panY: 0.0),
            (zoom: 0.25, panX: 0.0, panY: 0.0),
            (zoom: 0.5, panX: 0.0, panY: 0.0),
            (zoom: 2.0, panX: 0.0, panY: 0.0),
            (zoom: 4.0, panX: 0.0, panY: 0.0),
            (zoom: 1.0, panX: 100.0, panY: 50.0),
            (zoom: 2.0, panX: 100.0, panY: 50.0),
            (zoom: 0.5, panX: -50.0, panY: -30.0),
            (zoom: 3.0, panX: 200.0, panY: 100.0),
        };

        const double originalScreenX = 300;
        const double originalScreenY = 200;

        foreach (var (zoom, panX, panY) in testCases)
        {
            var (modelX, modelY) = CoordinateTransform.ScreenToModel(
                originalScreenX, originalScreenY, panX, panY, zoom);

            // 反向变换：模型 → 屏幕
            var (backX, backY) = CoordinateTransform.ModelToScreen(
                modelX, modelY, panX, panY, zoom);

            var deviation = Math.Sqrt(
                Math.Pow(backX - originalScreenX, 2) +
                Math.Pow(backY - originalScreenY, 2));

            Assert.True(deviation < 5.0,
                $"缩放={zoom}, 平移=({panX},{panY}) 时往返偏差 {deviation:F4}px 超过 5px 阈值");
        }
    }

    // ── 往返一致性 ──

    [Fact]
    public void ScreenToModel_ModelToScreen_RoundTrip_IsIdentity()
    {
        var rand = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            var origX = rand.NextDouble() * 1000;
            var origY = rand.NextDouble() * 1000;
            var panX = rand.NextDouble() * 200 - 100;
            var panY = rand.NextDouble() * 200 - 100;
            var zoom = 0.25 + rand.NextDouble() * 3.75; // 0.25 ~ 4.0

            var (mx, my) = CoordinateTransform.ScreenToModel(origX, origY, panX, panY, zoom);
            var (bx, by) = CoordinateTransform.ModelToScreen(mx, my, panX, panY, zoom);

            Assert.InRange(bx, origX - Tolerance, origX + Tolerance);
            Assert.InRange(by, origY - Tolerance, origY + Tolerance);
        }
    }

    // ── 边界情况 ──

    [Fact]
    public void ScreenToModel_ZeroZoom_DoesNotDivideByZero()
    {
        // zoom=0 应使用 MinZoom 保护，不抛出异常
        var (mx, my) = CoordinateTransform.ScreenToModel(100, 200, 0, 0, 0);

        Assert.False(double.IsNaN(mx));
        Assert.False(double.IsNaN(my));
        Assert.False(double.IsInfinity(mx));
        Assert.False(double.IsInfinity(my));
    }

    [Fact]
    public void ScreenToModel_NegativeZoom_UsesSafeFallback()
    {
        var (mx, my) = CoordinateTransform.ScreenToModel(100, 200, 0, 0, -1.0);

        Assert.False(double.IsNaN(mx));
        Assert.False(double.IsNaN(my));
    }

    // ── HitTest 测试 ──

    [Fact]
    public void HitTest_PointInsideControl_ReturnsTrue()
    {
        // 控件在 (100, 100) 位置，尺寸 80x40，1x 缩放，无平移
        var hit = CoordinateTransform.HitTest(140, 120, 100, 100, 80, 40, 0, 0, 1.0);
        Assert.True(hit);
    }

    [Fact]
    public void HitTest_PointOutsideControl_ReturnsFalse()
    {
        var hit = CoordinateTransform.HitTest(300, 300, 100, 100, 80, 40, 0, 0, 1.0);
        Assert.False(hit);
    }

    [Fact]
    public void HitTest_WithZoom2x_PointMapsCorrectly()
    {
        // 2x 缩放：控件视口矩形 = (200, 200, 160, 80)
        // 鼠标在 (250, 230) 应命中
        var hit = CoordinateTransform.HitTest(250, 230, 100, 100, 80, 40, 0, 0, 2.0);
        Assert.True(hit);
    }

    [Fact]
    public void HitTest_WithPan_PointAdjustedCorrectly()
    {
        // 平移 (50, 30)：控件视口矩形 = (150, 130, 80, 40)
        // 鼠标在 (160, 140) 应命中
        var hit = CoordinateTransform.HitTest(160, 140, 100, 100, 80, 40, 50, 30, 1.0);
        Assert.True(hit);
    }

    [Fact]
    public void HitTest_BoundaryEdge_InclusiveRange()
    {
        // 控件视口矩形 = (100, 100, 80, 40)
        // 左上角 (100, 100) 应命中（包含边界）
        var hitCorner = CoordinateTransform.HitTest(100, 100, 100, 100, 80, 40, 0, 0, 1.0);
        Assert.True(hitCorner);

        // 右下角 (180, 140) 应命中
        var hitFarCorner = CoordinateTransform.HitTest(180, 140, 100, 100, 80, 40, 0, 0, 1.0);
        Assert.True(hitFarCorner);
    }
}
