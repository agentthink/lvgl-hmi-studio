using System;
using System.Collections.Generic;
using Avalonia.Media;
using HmiEditor.App.Controls;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 画布渲染录放器：把 <see cref="DesignCanvas.Render"/> 产生的绘制操作录制下来，
/// 供断言「真的画了什么」。
///
/// <para><b>为什么需要它：</b>headless（<c>UseHeadlessDrawing = true</c>）的
/// <c>DrawingContext</c> 是个 no-op —— 绘制调用不产生任何可观察副作用，也没法计数。
/// 但 Avalonia 的 <see cref="DrawingGroup"/> 自带一个**录制型** DrawingContext：
/// <c>Open()</c> 拿到的 context 会把每次绘制追加成子节点，之后可以枚举。
/// 于是「画了几条刻度线」「缓存前后绘制操作是否一致」这类问题都能实测。</para>
///
/// <para>实测节点类型：几何 → <see cref="GeometryDrawing"/>（<c>Geometry</c> 是
/// 不透明的 <c>PlatformGeometry</c>，需靠 <c>Bounds</c> 区分）；
/// 文本 → <c>GlyphRunDrawing</c>；<c>PushTransform</c>/<c>PushOpacity</c> → 嵌套 <see cref="DrawingGroup"/>。</para>
/// </summary>
internal static class CanvasRenderSpy
{
    /// <summary>渲染一次并展开（含嵌套组）返回全部绘制节点。</summary>
    public static List<Drawing> Record(DesignCanvas canvas)
    {
        var group = new DrawingGroup();
        using (var ctx = group.Open())
        {
            canvas.Render(ctx);
        }
        return Flatten(group);
    }

    /// <summary>递归展开嵌套 <see cref="DrawingGroup"/>（对应 Push* 作用域）。</summary>
    public static List<Drawing> Flatten(DrawingGroup group)
    {
        var all = new List<Drawing>();
        Walk(group, all);
        return all;
    }

    private static void Walk(DrawingGroup group, List<Drawing> sink)
    {
        foreach (var child in group.Children)
        {
            sink.Add(child);
            if (child is DrawingGroup nested)
                Walk(nested, sink);
        }
    }

    /// <summary>
    /// 数「描边短线段」：只有画笔没有填充、且包围盒最长边不超过 <paramref name="maxExtent"/>。
    /// 用于把刻度线（长 8px）从圆弧（长 100+px）里区分出来。
    /// </summary>
    public static int CountStrokedSegments(IEnumerable<Drawing> drawings, double maxExtent = 8.5)
    {
        var n = 0;
        foreach (var d in drawings)
        {
            if (d is not GeometryDrawing gd) continue;
            if (gd.Pen is null || gd.Brush is not null) continue;
            if (gd.Geometry is not { } geometry) continue;

            var b = geometry.Bounds;
            if (Math.Max(b.Width, b.Height) <= maxExtent) n++;
        }
        return n;
    }

    /// <summary>绘制操作的稳定指纹，用于断言「改了实现但渲染结果没变」。</summary>
    public static string Fingerprint(IEnumerable<Drawing> drawings)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var d in drawings)
        {
            switch (d)
            {
                case GeometryDrawing gd:
                    var b = gd.Geometry?.Bounds ?? default;
                    sb.Append("G:")
                      .Append(gd.Pen is null ? "-" : gd.Pen.Brush?.ToString() ?? "?")
                      .Append('/').Append(gd.Pen?.Thickness ?? 0)
                      .Append('/').Append(gd.Brush is null ? "-" : gd.Brush.ToString())
                      .Append('/').Append(Round(b.X)).Append(',').Append(Round(b.Y))
                      .Append(',').Append(Round(b.Width)).Append(',').Append(Round(b.Height))
                      .Append(';');
                    break;
                case DrawingGroup:
                    sb.Append("S;");
                    break;
                default:
                    sb.Append(d.GetType().Name).Append(';');
                    break;
            }
        }
        return sb.ToString();
    }

    private static string Round(double v) => Math.Round(v, 3).ToString("0.###");

    /// <summary>渲染一次并把操作指纹算出来。</summary>
    public static string FingerprintOf(DesignCanvas canvas) => Fingerprint(Record(canvas));
}
