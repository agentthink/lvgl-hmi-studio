namespace HmiEditor.Core.Design;

/// <summary>
/// Gauge 主刻度的几何计算 —— 纯函数，供设计器渲染（<c>DesignCanvas.RenderGauge</c>）
/// 与测试共用。
///
/// <para>刻度沿 [<see cref="ArcLayout.StartAngle"/>, +<see cref="ArcLayout.SweepAngle"/>]
/// 均布（默认 135°..405°），角度取自 <see cref="ArcLayout"/> 共享常量，
/// 与代码生成端的 lv_arc rotation/bg_angles 同源，防止两处漂移。</para>
///
/// <para>之所以抽成 Core 纯函数：原来这套角度/端点算术内联在 4222 行的
/// <c>DesignCanvas</c> 里，只能靠「读源码 + grep 字符串」来守护，
/// 重构即碎且验证不了真实行为。抽出来后可做行为断言。</para>
/// </summary>
public static class GaugeTickLayout
{
    /// <summary>刻度数量下限：至少 2 条才能定义一段量程（分母不为零）。</summary>
    public const int MinTickCount = 2;

    /// <summary>刻度线端点（设计器坐标，y 向下）：从圆环内侧向外画。</summary>
    public readonly record struct Tick(double X1, double Y1, double X2, double Y2);

    /// <summary>把用户设置的刻度数收敛到合法范围（与渲染端一致）。</summary>
    public static int NormalizeCount(int tickCount) => Math.Max(MinTickCount, tickCount);

    /// <summary>
    /// 各刻度线的角度（度）。数量 = <see cref="NormalizeCount"/> 的结果，
    /// 首条 = <see cref="ArcLayout.StartAngle"/>，末条 = StartAngle + SweepAngle。
    /// </summary>
    public static double[] ComputeAngles(int tickCount)
    {
        var n = NormalizeCount(tickCount);
        var step = ArcLayout.SweepAngle / (n - 1);
        var angles = new double[n];
        for (var i = 0; i < n; i++)
            angles[i] = ArcLayout.StartAngle + i * step;
        return angles;
    }

    /// <summary>
    /// 计算全部刻度线端点。
    /// </summary>
    /// <param name="centerX">弧心 X（设计器坐标）。</param>
    /// <param name="centerY">弧心 Y（设计器坐标）。</param>
    /// <param name="radius">弧半径；刻度线外端落在圆环上。</param>
    /// <param name="tickLength">刻度线长度（从圆环向内延伸）。</param>
    /// <param name="tickCount">用户设置的主刻度数量，内部会收敛。</param>
    public static Tick[] ComputeTicks(
        double centerX, double centerY, double radius, double tickLength, int tickCount)
    {
        var angles = ComputeAngles(tickCount);
        var inner = radius - tickLength;
        var ticks = new Tick[angles.Length];
        for (var i = 0; i < angles.Length; i++)
        {
            // 角度 → 坐标统一走 ArcLayout.PointAt（全仓唯一映射），
            // 保证刻度与弧线用同一套 LVGL 角度约定，不会各自漂移。
            var (ox, oy) = ArcLayout.PointAt(centerX, centerY, radius, angles[i]);
            var (ix, iy) = ArcLayout.PointAt(centerX, centerY, inner, angles[i]);
            ticks[i] = new Tick(ix, iy, ox, oy);
        }
        return ticks;
    }
}
