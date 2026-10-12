namespace HmiEditor.Core.Services;

/// <summary>形状包围盒（形状自身坐标系，即 viewBox 空间、**未**应用 <see cref="Models.SvgShape.Transform"/>）。</summary>
public readonly record struct SvgBox(double X, double Y, double Width, double Height);

/// <summary>
/// LVGL 侧能否发射**真渐变**的判据结果。
///
/// <para>这个枚举存在的理由：LVGL v9 的软件渲染器里，只有「轴对齐矩形」这条绘制路径
/// （<c>lv_draw_rect</c> → <c>lv_draw_sw_fill</c>）实现了 <c>LINEAR/RADIAL/CONICAL</c> 渐变；
/// 多边形走的三角扇（<c>lv_draw_triangle</c>）只有 <c>HOR/VER</c> 两个分支，且色带是按
/// **每个三角形自己的包围盒**生成的 ⇒ 一个扇形会被拆成 N 份各自重来的渐变，语义本身就是错的。
/// 描边（<c>lv_draw_line_dsc_t</c>）则根本没有 <c>grad</c> 字段。</para>
/// </summary>
public enum SvgGradientEmit
{
    /// <summary>发不出真渐变 ⇒ 用 <c>Fill</c>/<c>Stroke</c> 里保存的降级纯色。</summary>
    SolidFallback,

    /// <summary>可发真渐变：轴对齐矩形，或等价于「radius = r 的圆角矩形」的圆。</summary>
    RectGradient
}

/// <summary>解析到形状局部坐标后的渐变几何（已含包围盒映射与 gradientTransform）。</summary>
public readonly record struct SvgResolvedGradient(
    Models.SvgGradientKind Kind,
    double X1, double Y1, double X2, double Y2,
    double Radius,
    double FocalX, double FocalY,
    bool HasFocal);

/// <summary>
/// 渐变几何的唯一实现 —— 把 SVG 的两种坐标语义（objectBoundingBox / userSpaceOnUse）
/// 统一换算成**形状局部像素坐标**，供画布渲染与 CodeGen 共用。
///
/// <para><b>为什么必须统一在这里</b>：包围盒映射、gradientTransform 的叠加顺序、
/// 百分比解析这几件事只要两侧各写一份，就会出现"画布上是斜的、设备上是正的"这类
/// 只在端到端才暴露的分叉。两侧唯一的差别应当只剩"用哪种 API 把同一个几何画出来"。</para>
/// </summary>
public static class SvgGradientGeometry
{
    /// <summary>
    /// LVGL 侧 <c>LV_GRADIENT_MAX_STOPS</c> 的默认值。
    /// 权威 <c>package/hmi-runtime/src-config/lv_conf.h</c> 未定义该项，走
    /// <c>lv_conf_internal.h</c> 的默认 <c>2</c>；发射超过这个数量的停靠点会**写到数组外**。
    /// </summary>
    public const int MaxStops = 2;

    /// <summary>圆与椭圆的等价判据容差（viewBox 单位）。</summary>
    private const double CircleEpsilon = 1e-9;

    /// <summary>
    /// 形状包围盒；几何不足（如空 Path）时返回 null。
    /// </summary>
    public static SvgBox? BoundingBox(Models.SvgShape shape)
    {
        var p = shape.Points;
        switch (shape.Kind)
        {
            case Models.SvgShapeKind.Rect:
                return p.Length >= 4 ? new SvgBox(p[0], p[1], p[2], p[3]) : null;

            case Models.SvgShapeKind.Circle:
                return p.Length >= 3 ? new SvgBox(p[0] - p[2], p[1] - p[2], p[2] * 2, p[2] * 2) : null;

            case Models.SvgShapeKind.Ellipse:
                return p.Length >= 4 ? new SvgBox(p[0] - p[2], p[1] - p[3], p[2] * 2, p[3] * 2) : null;

            case Models.SvgShapeKind.Line:
                if (p.Length < 4) return null;
                return new SvgBox(Math.Min(p[0], p[2]), Math.Min(p[1], p[3]),
                    Math.Abs(p[2] - p[0]), Math.Abs(p[3] - p[1]));

            case Models.SvgShapeKind.Polyline:
            case Models.SvgShapeKind.Polygon:
                return BoxOfPointList(p);

            case Models.SvgShapeKind.Path:
            {
                var subs = SvgPathFlattener.Flatten(shape.PathData);
                if (subs.Count == 0) return null;
                var all = new List<double>();
                foreach (var sub in subs) all.AddRange(sub.Points);
                return BoxOfPointList(all);
            }

            default:
                return null;
        }
    }

    private static SvgBox? BoxOfPointList(IReadOnlyList<double> pts)
    {
        if (pts.Count < 2) return null;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        for (var i = 0; i + 1 < pts.Count; i += 2)
        {
            minX = Math.Min(minX, pts[i]);
            maxX = Math.Max(maxX, pts[i]);
            minY = Math.Min(minY, pts[i + 1]);
            maxY = Math.Max(maxY, pts[i + 1]);
        }
        return new SvgBox(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// 能不能发真渐变。判据是「形状类型 × 轴对齐」两条，**不比对任何字符串**。
    /// </summary>
    public static SvgGradientEmit Decide(Models.SvgShape shape, Models.SvgGradient? gradient)
    {
        if (gradient == null || gradient.Stops.Count == 0) return SvgGradientEmit.SolidFallback;

        // 含旋转/斜切 ⇒ lv_area_t 这种轴对齐表达写不出来
        if (!SvgAffine.IsAxisAligned(shape.Transform)) return SvgGradientEmit.SolidFallback;

        return shape.Kind switch
        {
            Models.SvgShapeKind.Rect => SvgGradientEmit.RectGradient,
            Models.SvgShapeKind.Circle => SvgGradientEmit.RectGradient,
            // rx == ry 的椭圆就是圆 —— 圆的绘制在 LVGL 里是 radius = r 的矩形
            Models.SvgShapeKind.Ellipse when IsCircle(shape) => SvgGradientEmit.RectGradient,
            _ => SvgGradientEmit.SolidFallback
        };
    }

    /// <summary>椭圆是否等价于圆（rx == ry）。</summary>
    public static bool IsCircle(Models.SvgShape shape)
    {
        if (shape.Kind == Models.SvgShapeKind.Circle) return true;
        if (shape.Kind != Models.SvgShapeKind.Ellipse || shape.Points.Length < 4) return false;
        return Math.Abs(shape.Points[2] - shape.Points[3]) <= CircleEpsilon;
    }

    /// <summary>
    /// 把渐变坐标解析到形状局部坐标。
    ///
    /// <para>叠加顺序：objectBoundingBox 时 <c>M = Translate(box) · Scale(box) · gradientTransform</c>；
    /// userSpaceOnUse 时 <c>M = userTransform · gradientTransform</c>（坐标已是 viewBox 绝对量，
    /// 百分比由解析器就地换算）。</para>
    ///
    /// <para><b>userSpaceOnUse 必须带上 <paramref name="userTransform"/></b>：SVG 规定该模式下
    /// 渐变坐标取自"引用它的那个元素的用户坐标系"，而元素的 <c>transform</c> 正是这个坐标系的一部分。
    /// 漏掉它，<c>&lt;g transform="translate(50,0)"&gt;</c> 里的形状会移动而渐变留在原地 ——
    /// 这类偏差只在端到端比对时才看得出来。objectBoundingBox 不受影响：<c>box</c> 已经是被引用
    /// 形状变换**之后**的包围盒，跟着盒子走天然就对。</para>
    ///
    /// <para>调用方传形状**烘焙前**的 <c>Transform</c>：烘焙会把矩阵清成 null，之后再传就没有这个信息了。</para>
    /// </summary>
    public static SvgResolvedGradient Resolve(Models.SvgGradient g, SvgBox box, double[]? userTransform = null)
    {
        var m = SvgAffine.Identity();
        if (!g.UserSpace)
        {
            m = SvgAffine.Multiply(SvgAffine.Translate(box.X, box.Y),
                                   SvgAffine.Scale(box.Width, box.Height));
        }
        else if (userTransform != null && !SvgAffine.IsIdentity(userTransform))
        {
            m = userTransform;
        }
        if (g.Transform != null) m = SvgAffine.Multiply(m, g.Transform);

        var c = g.Coords;
        if (g.Kind == Models.SvgGradientKind.Linear)
        {
            // 默认 0%,0% → 100%,0%（水平）
            var x1 = c.Length > 0 ? c[0] : 0;
            var y1 = c.Length > 1 ? c[1] : 0;
            var x2 = c.Length > 2 ? c[2] : 1;
            var y2 = c.Length > 3 ? c[3] : 0;
            var p1 = SvgAffine.Apply(m, x1, y1);
            var p2 = SvgAffine.Apply(m, x2, y2);
            return new SvgResolvedGradient(g.Kind, p1.X, p1.Y, p2.X, p2.Y, 0, 0, 0, false);
        }
        else
        {
            // 默认 50%,50%,50%
            var cx = c.Length > 0 ? c[0] : 0.5;
            var cy = c.Length > 1 ? c[1] : 0.5;
            var r = c.Length > 2 ? c[2] : 0.5;
            var center = SvgAffine.Apply(m, cx, cy);
            // 半径用 x 方向的像长。均匀缩放（圆、正方形）时与 y 方向相等；
            // 非均匀包围盒下 SVG 的径向本是椭圆，而 lv_grad_radial_init 只能表达圆 ——
            // 这种情形按 x 方向近似，并记在局限里。
            var onCircle = SvgAffine.Apply(m, cx + r, cy);
            var radius = Math.Sqrt(Math.Pow(onCircle.X - center.X, 2) + Math.Pow(onCircle.Y - center.Y, 2));

            var hasFocal = c.Length >= 5;
            var focal = hasFocal ? SvgAffine.Apply(m, c[3], c[4]) : center;
            return new SvgResolvedGradient(g.Kind, center.X, center.Y, 0, 0, radius,
                focal.X, focal.Y, hasFocal);
        }
    }

    /// <summary>
    /// 降级纯色 —— 取渐变 50% 处的颜色。
    ///
    /// <para>取中点而不是首停靠点：多数图标把主色放在中间，取首位会让降级结果明显偏暗/偏亮。
    /// "none" 停靠点不参与插值（本项目颜色是 24 位 RGB、没有 alpha，无法表达半透明）。</para>
    /// </summary>
    public static string SampleMidColor(Models.SvgGradient g)
    {
        var stops = g.Stops.Where(s => !string.Equals(s.Color, "none", StringComparison.OrdinalIgnoreCase))
                           .OrderBy(s => s.Offset)
                           .ToList();
        if (stops.Count == 0) return "none";
        if (stops.Count == 1) return stops[0].Color;

        for (var i = 0; i + 1 < stops.Count; i++)
        {
            var a = stops[i];
            var b = stops[i + 1];
            if (0.5 >= a.Offset && 0.5 <= b.Offset)
            {
                var span = b.Offset - a.Offset;
                var t = span <= double.Epsilon ? 0 : (0.5 - a.Offset) / span;
                return InterpolateHex(a.Color, b.Color, t);
            }
        }
        return stops[^1].Color;
    }

    /// <summary>
    /// 停靠点归一化：按 offset 升序、钳到 0..1，超过 <paramref name="maxStops"/> 时取**首尾**。
    ///
    /// <para>取首尾而不是"等距重采样"：LVGL 的 <c>fracs</c> 能表达任意位置，
    /// 首尾两点保住了渐进方向的整体观感；重采样会造出一批原图没有的颜色。</para>
    /// </summary>
    public static List<Models.SvgGradientStop> NormalizeStops(Models.SvgGradient g, int maxStops = MaxStops)
    {
        var sorted = g.Stops
            .OrderBy(s => Math.Clamp(s.Offset, 0, 1))
            .ToList();
        if (sorted.Count <= maxStops) return sorted;
        return new List<Models.SvgGradientStop> { sorted[0], sorted[^1] };
    }

    /// <summary>两个 #RRGGBB 按比例线性插值（t=0 取 a，t=1 取 b）。</summary>
    public static string InterpolateHex(string a, string b, double t)
    {
        if (!TryParseHex(a, out var ca)) return b;
        if (!TryParseHex(b, out var cb)) return a;
        var k = Math.Clamp(t, 0, 1);
        byte Mix(byte x, byte y) => (byte)Math.Round(x + (y - x) * k);
        return $"#{Mix(ca.R, cb.R):x2}{Mix(ca.G, cb.G):x2}{Mix(ca.B, cb.B):x2}";
    }

    private static bool TryParseHex(string color, out (byte R, byte G, byte B) rgb)
    {
        rgb = default;
        if (string.IsNullOrEmpty(color) || color[0] != '#') return false;
        var hex = color[1..];
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => $"{c}{c}"));
        if (hex.Length != 6 || !hex.All(Uri.IsHexDigit)) return false;
        rgb = (Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..], 16));
        return true;
    }
}
