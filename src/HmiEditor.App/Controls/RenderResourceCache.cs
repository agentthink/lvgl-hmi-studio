using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using HmiEditor.Core.Design;

namespace HmiEditor.App.Controls;

/// <summary>
/// 画布渲染资源缓存 —— 把渲染热路径上「每帧重建」的画刷 / 画笔 / 文本布局 / 弧几何收敛成复用。
///
/// <para><b>为什么需要：</b><c>DesignCanvas</c> 的渲染路径原先每次调用都新建对象，
/// 实测（见 <c>docs/2026-09-14-canvas-render-baseline.md</c>）单控件每帧约 9 KB、
/// 25 控件整屏 418 KB/帧，其中约 72% 是可以消除的重复分配：
/// <c>new SolidColorBrush</c> 616 B、<c>new Pen</c> 1264 B、
/// <c>new FormattedText</c> + 惰性文本布局 ≈ 3.4 KB、<c>StreamGeometry</c> ≈ 480 B。</para>
///
/// <para><b>为什么用 Immutable* 类型：</b><c>SolidColorBrush</c>/<c>Pen</c> 是可变的
/// AvaloniaObject；共享可变实例一旦被别处改色就会污染所有使用者。
/// <c>ImmutableSolidColorBrush</c>/<c>ImmutablePen</c> 是不可变的，可以安全共享。</para>
///
/// <para><b>键的取值：</b>全部来自「不可变的值」——颜色、字号、文本、矩形、角度。
/// 颜色是主题/用户配置的枚举值域，字号是有限的，因此缓存规模天然收敛。</para>
///
/// <para><b>线程模型：</b>渲染只在 UI 线程发生，因此用普通 Dictionary，不加锁。
/// 若将来出现离屏渲染线程，需要改为每线程独立缓存。</para>
///
/// <para><b>超限策略：</b>整体清空，不做 LRU。连续缩放时字号会逐帧变化导致频繁清空，
/// 但那只在拖拽缩放的瞬时发生，且清空只是退回到「和以前一样重建」，不会更差。</para>
///
/// <para><b>实例寿命（实测）：</b>画刷 / 画笔 / 弧几何是纯托管对象，可长期复用；
/// 但 <see cref="FormattedText"/> 背后是平台侧文本布局对象，会随后端重建或
/// headless 会话的每次 Dispatch 结束被释放 —— 复用陈旧实例读 <c>Width</c> 会抛
/// <c>ObjectDisposedException</c>。调用方必须能识别并重建，见 <see cref="InvalidateTexts"/>。</para>
/// </summary>
internal static class RenderResourceCache
{
    private const int MaxBrushEntries = 512;
    private const int MaxPenEntries = 512;
    private const int MaxTextEntries = 1024;
    private const int MaxGeometryEntries = 512;

    private static readonly Dictionary<BrushKey, ImmutableSolidColorBrush> Brushes = new();
    private static readonly Dictionary<PenKey, ImmutablePen> Pens = new();
    private static readonly Dictionary<TextKey, FormattedText> Texts = new();
    private static readonly Dictionary<GeometryKey, StreamGeometry> Geometries = new();

    private readonly record struct BrushKey(uint Color, double Opacity);

    private readonly record struct PenKey(
        uint Color, double Opacity, double Thickness, int LineCap, int LineJoin);

    private readonly record struct TextKey(
        string Text, double FontSize, uint Color, double Opacity, string Culture);

    private readonly record struct GeometryKey(
        double X, double Y, double Width, double Height, double StartAngle, double SweepAngle, bool LvglConvention);

    // ── 画刷 ─────────────────────────────────────────────────

    /// <summary>取一个可共享的实色画刷。<paramref name="opacity"/> 为 0..1。</summary>
    public static ImmutableSolidColorBrush Brush(Color color, double opacity = 1.0)
    {
        var key = new BrushKey(color.ToUInt32(), opacity);
        if (Brushes.TryGetValue(key, out var cached)) return cached;

        var brush = new ImmutableSolidColorBrush(color, opacity);
        if (Brushes.Count >= MaxBrushEntries) Brushes.Clear();
        Brushes[key] = brush;
        return brush;
    }

    /// <summary>取一个可共享的实色画刷（画刷来源是 <see cref="ISolidColorBrush"/>）。</summary>
    public static ImmutableSolidColorBrush Brush(ISolidColorBrush brush)
        => Brush(brush.Color, brush.Opacity);

    // ── 画笔 ─────────────────────────────────────────────────

    /// <summary>
    /// 取一个可共享的实线画笔（无虚线样式 —— 虚线只用于框选 / 拖放幽灵，
    /// 只在拖拽过程中渲染，不属每帧热路径）。
    /// </summary>
    public static ImmutablePen Pen(Color color, double thickness,
        PenLineCap lineCap = PenLineCap.Flat, PenLineJoin lineJoin = PenLineJoin.Miter)
    {
        var key = new PenKey(color.ToUInt32(), 1.0, thickness, (int)lineCap, (int)lineJoin);
        if (Pens.TryGetValue(key, out var cached)) return cached;

        var pen = new ImmutablePen(
            new ImmutableSolidColorBrush(color), thickness, null, lineCap, lineJoin, 10);
        if (Pens.Count >= MaxPenEntries) Pens.Clear();
        Pens[key] = pen;
        return pen;
    }

    /// <summary>
    /// 取一个可共享的实线画笔。
    /// 传入实色画刷时按「颜色 + 透明度 + 线宽 + 线帽 + 拐角」复用；
    /// 传入非实色画刷（例如主题渐变）时退回普通 <see cref="Pen"/> 不缓存，保证行为不变。
    /// </summary>
    public static IPen Pen(IBrush brush, double thickness,
        PenLineCap lineCap = PenLineCap.Flat, PenLineJoin lineJoin = PenLineJoin.Miter)
    {
        if (brush is not ISolidColorBrush solid)
        {
            return new Pen(brush, thickness) { LineCap = lineCap, LineJoin = lineJoin };
        }

        var key = new PenKey(solid.Color.ToUInt32(), solid.Opacity, thickness, (int)lineCap, (int)lineJoin);
        if (Pens.TryGetValue(key, out var cached)) return cached;

        var pen = new ImmutablePen(
            new ImmutableSolidColorBrush(solid.Color, solid.Opacity),
            thickness, null, lineCap, lineJoin, 10);
        if (Pens.Count >= MaxPenEntries) Pens.Clear();
        Pens[key] = pen;
        return pen;
    }

    // ── 文本布局 ─────────────────────────────────────────────

    /// <summary>
    /// 取一个可共享的 <see cref="FormattedText"/>。
    /// 复用实例还能顺带省掉「首次读 Width/Height 触发惰性布局」的分配（实测 ≈ 3 KB/次）。
    /// 前景画刷请走 <see cref="Brush(Color, double)"/>，保证键可复用。
    /// </summary>
    public static FormattedText Text(string text, double fontSize, IBrush foreground)
    {
        var (color, opacity) = foreground switch
        {
            ISolidColorBrush s => (s.Color, s.Opacity),
            _ => (Colors.Transparent, 0.0),
        };

        var culture = CultureInfo.CurrentCulture.Name;
        var key = new TextKey(text, fontSize, color.ToUInt32(), opacity, culture);
        if (Texts.TryGetValue(key, out var cached)) return cached;

        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, fontSize, Brush(color, opacity));
        if (Texts.Count >= MaxTextEntries) Texts.Clear();
        Texts[key] = ft;
        return ft;
    }

    // ── 弧几何 ───────────────────────────────────────────────

    /// <summary>
    /// 取一个可共享的弧几何。纯函数式构造（只依赖矩形与角度），可以安全复用。
    /// 角度一律走 <see cref="ArcLayout.PointAt"/>（LVGL 约定：0° 在 3 点钟、顺时针为正）。
    /// </summary>
    /// <param name="asPolyline">
    /// true 用密集折线近似（与 <c>lv_draw_arc</c> 的栅格化同一套角度映射，headless 下
    /// <c>Bounds</c> 真实可用）；false 用 Avalonia 椭圆弧 <c>ArcTo</c>（更精确，但
    /// <c>UseHeadlessDrawing=true</c> 时 <c>ArcTo</c> 是桩实现、只记端点，<c>Bounds</c> 不可信）。
    /// </param>
    public static StreamGeometry Arc(Rect rect, double startAngle, double sweepAngle, bool asPolyline)
    {
        var key = new GeometryKey(
            rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle, asPolyline);
        if (Geometries.TryGetValue(key, out var cached)) return cached;

        var geometry = asPolyline
            ? BuildArcPolyline(rect, startAngle, sweepAngle)
            : BuildEllipseArc(rect, startAngle, sweepAngle);

        if (Geometries.Count >= MaxGeometryEntries) Geometries.Clear();
        Geometries[key] = geometry;
        return geometry;
    }

    /// <summary>精确椭圆弧（<c>Rect.Center</c> + <c>ArcTo</c>，顺时针）。</summary>
    private static StreamGeometry BuildEllipseArc(Rect rect, double startAngle, double sweepAngle)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var cx = rect.Center.X;
            var cy = rect.Center.Y;
            var rx = rect.Width / 2;
            var ry = rect.Height / 2;

            var (sx, sy) = ArcLayout.PointAt(cx, cy, 1, startAngle);
            var (ex, ey) = ArcLayout.PointAt(cx, cy, 1, startAngle + sweepAngle);

            ctx.BeginFigure(new Point(cx + (sx - cx) * rx, cy + (sy - cy) * ry), false);
            ctx.ArcTo(
                new Point(cx + (ex - cx) * rx, cy + (ey - cy) * ry),
                new Size(rx, ry), 0, Math.Abs(sweepAngle) > 180, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        return geometry;
    }

    /// <summary>密集折线近似弧（64 段）。</summary>
    private static StreamGeometry BuildArcPolyline(Rect rect, double startAngle, double sweepAngle)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var cx = rect.Center.X;
            var cy = rect.Center.Y;
            var rx = rect.Width / 2;
            var ry = rect.Height / 2;

            const int segments = 64;
            for (var i = 0; i <= segments; i++)
            {
                var a = startAngle + sweepAngle * i / segments;
                // 用单位圆的映射取方向，再按 rx/ry 缩放，保证与 PointAt 同一套约定
                var (ux, uy) = ArcLayout.PointAt(cx, cy, 1, a);
                var pt = new Point(cx + (ux - cx) * rx, cy + (uy - cy) * ry);
                if (i == 0)
                    ctx.BeginFigure(pt, false);
                else
                    ctx.LineTo(pt);
            }
            ctx.EndFigure(false);
        }
        return geometry;
    }

    // ── 诊断 / 测试用 ────────────────────────────────────────

    /// <summary>
    /// 丢弃全部文本缓存项。
    ///
    /// <para><b>为什么需要「失效」而不只是「清空」：</b><see cref="FormattedText"/> 持有
    /// 平台侧的文本布局对象，后者会随渲染后端重建 / 字体服务重载 / headless 会话的
    /// 每次 Dispatch 结束而被释放。此后复用缓存实例去读 <c>Width</c>（触发惰性布局）
    /// 或绘制会抛 <c>ObjectDisposedException</c>。</para>
    ///
    /// <para>这批实例是同一批次创建的、寿命一致，因此整批丢弃即可；下次取用会重建。
    /// 调用方见 <c>DesignCanvas.DrawAlignedText</c>：捕获异常 → 丢弃 → 重建重试一次。</para>
    /// </summary>
    public static void InvalidateTexts()
    {
        if (Texts.Count == 0) return;
        Texts.Clear();
        TextInvalidations++;
        System.Diagnostics.Debug.WriteLine(
            $"[RenderResourceCache] 文本布局实例已失效，丢弃 {TextInvalidations} 批缓存（平台侧对象被释放）。");
    }

    /// <summary>当前各项缓存条目数（调试与性能测试用）。</summary>
    public static (int Brushes, int Pens, int Texts, int Geometries) Counts
        => (Brushes.Count, Pens.Count, Texts.Count, Geometries.Count);

    /// <summary>
    /// 文本缓存被判定失效并整批丢弃的累计次数。
    /// 恒为 0 说明实例寿命足够；非 0 说明发生过后端/会话重建（诊断线索）。
    /// </summary>
    public static int TextInvalidations { get; private set; }

    /// <summary>清空全部缓存（测试用，验证「冷缓存 / 热缓存」两条路径行为一致）。</summary>
    public static void Clear()
    {
        Brushes.Clear();
        Pens.Clear();
        Texts.Clear();
        Geometries.Clear();
        TextInvalidations = 0;
    }
}
