namespace HmiEditor.Core.Design;

/// <summary>
/// Gauge/Knob/Arc 的角度量程常量与「角度 → 坐标」映射 —— 设计器渲染（<c>DesignCanvas</c>）
/// 与代码生成（<c>CodeGenerator</c> 的 lv_arc rotation/bg_angles）共享，防止两处漂移。
///
/// <para><b>角度约定 = LVGL 的约定：3 点钟方向为 0°，顺时针为正。</b>
/// 屏幕坐标 y 向下时表现为 <c>x = cx + r·cosθ</c>、<c>y = cy + r·sinθ</c>，
/// 于是 θ=90° 落在屏幕<b>下方</b>，角度递增即<b>顺时针</b>。
/// 依据 LVGL v9 源码：<c>src/misc/lv_math.c</c> 里 <c>lv_trigo_sin(90)=+32767</c>、
/// <c>(270)=−32768</c>；<c>src/draw/lv_draw_arc.c</c> 用上面这组式子把角度转成坐标。</para>
///
/// <para><b>曾经的坑（2026-09-14 修）</b>：本类注释一度写成「逆时针为正」，而
/// <c>DesignCanvas.DrawLvglArcGeometry</c> 按 <c>cy − r·sin</c> 实现（竖直镜像），
/// 导致 Arc 控件的设计器渲染与模拟器上下颠倒（见
/// <c>docs/2026-09-14-arc-angle-convention.md</c>）。约定与实现现已统一到
/// <see cref="PointAt"/>：任何一处再改动角度映射，<c>ArcLayoutTests</c> 都会拦下来。</para>
/// </summary>
public static class ArcLayout
{
    /// <summary>弧线起始角（度）。270° 量程从 135° 扫到 405°（等效 45°），即上方 3/4 圆。</summary>
    public const double StartAngle = 135.0;

    /// <summary>弧线扫角（度）。</summary>
    public const double SweepAngle = 270.0;

    /// <summary>
    /// Knob 指针量程起点（度）= <see cref="StartAngle"/>。
    /// 固件的 Knob 是 <c>lv_arc</c>（rotation=135 + bg_angles(0,270)），指示弧自 135° 起随时间增长，
    /// 指针起点必须与之重合 —— 原先写的 <c>-135</c> 落在 10:30 方向，比固件偏了 90°。
    /// </summary>
    public const double PointerStart = StartAngle;

    /// <summary>默认指针角度（度）：编辑态无绑定值时取量程 60% 位置。</summary>
    public const double PointerDefault = StartAngle + 0.6 * SweepAngle;

    /// <summary>默认活动弧扫角（度）= 量程 × 0.6。</summary>
    public const double DefaultActiveSweep = 0.6 * SweepAngle;

    /// <summary>弧线默认宽度（px，与设计器 12*z 一致；用户可经 BorderWidth 调）。</summary>
    public const int ArcWidthDefault = 12;

    /// <summary>
    /// 角度 → 坐标（LVGL 约定：0° 在 3 点钟、顺时针为正）。
    ///
    /// <para>这是全仓唯一的角度映射实现：设计器折线弧、Gauge 刻度、精确圆弧
    /// （Avalonia <c>ArcTo</c>）的起止点都走它，避免再出现两套互相镜像的约定。</para>
    /// </summary>
    public static (double X, double Y) PointAt(
        double centerX, double centerY, double radius, double angleDegrees)
    {
        var rad = angleDegrees * Math.PI / 180.0;
        return (centerX + radius * Math.Cos(rad), centerY + radius * Math.Sin(rad));
    }
}
