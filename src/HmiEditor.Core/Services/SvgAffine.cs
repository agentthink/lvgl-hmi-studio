using System.Globalization;

namespace HmiEditor.Core.Services;

/// <summary>
/// SVG 2×3 仿射变换的唯一实现 —— 画布渲染与 LVGL CodeGen **必须**都走这里。
///
/// <para>矩阵按 SVG <c>matrix(a,b,c,d,e,f)</c> 的顺序存放（列向量约定）：
/// <code>
///   x' = a·x + c·y + e
///   y' = b·x + d·y + f
/// </code>
/// 旋转是**屏幕方向（y 轴向下）的顺时针**，与 SVG 语义一致。</para>
///
/// <para><b>为什么这份实现必须唯一</b>：CodeGen 侧是在生成期把变换烘进坐标常量，
/// 渲染侧是交给 Avalonia 的 <c>Matrix</c>；两套实现一旦漂移，"画布所见"与"设备所得"
/// 就会在旋转/缩放的 SVG 上静默分叉，而这种分叉在任何单侧测试里都看不出来。</para>
/// </summary>
public static class SvgAffine
{
    /// <summary>
    /// 轴对齐判据的容差。作用于**无量纲**的 b/c 分量（它们只由 cos/sin/skew 与缩放系数相乘得到），
    /// 所以用绝对容差是安全的：1° 旋转的 |b| 已达 0.0175，与 1e-9 差着七个数量级。
    /// </summary>
    private const double AxisAlignedEpsilon = 1e-9;

    public static double[] Identity() => new double[] { 1, 0, 0, 1, 0, 0 };

    /// <summary>矩阵是否可视为恒等（用于"要不要走变换路径"的短路判据）。</summary>
    public static bool IsIdentity(double[]? m)
    {
        if (m == null) return true;
        if (m.Length < 6) return true;
        return Math.Abs(m[0] - 1) <= AxisAlignedEpsilon && Math.Abs(m[1]) <= AxisAlignedEpsilon
            && Math.Abs(m[2]) <= AxisAlignedEpsilon && Math.Abs(m[3] - 1) <= AxisAlignedEpsilon
            && Math.Abs(m[4]) <= AxisAlignedEpsilon && Math.Abs(m[5]) <= AxisAlignedEpsilon;
    }

    /// <summary>
    /// 是否**轴对齐**（只含平移/缩放，不含旋转与斜切）—— 这是"形状几何要不要提升成多边形"的**唯一判据**。
    ///
    /// <para>轴对齐时 <c>Rect</c> 变换后仍是轴对齐矩形，可以直接发 <c>lv_draw_rect</c> + <c>lv_area_t</c>；
    /// 一旦含旋转/斜切，<c>lv_area_t</c> 这种轴对齐表达就写不出来了，必须按点集逐点变换后走多边形。
    /// 判据取"宁可判为非轴对齐"：微小旋转被判为旋转只会多走点集路径（结果仍正确），
    /// 而反过来会把旋转形状画成轴对齐的，是**错**的。</para>
    /// </summary>
    public static bool IsAxisAligned(double[]? m)
    {
        if (m == null) return true;
        if (m.Length < 6) return true;
        return Math.Abs(m[1]) <= AxisAlignedEpsilon && Math.Abs(m[2]) <= AxisAlignedEpsilon;
    }

    /// <summary>
    /// 合成：返回「先应用 <paramref name="inner"/>、再应用 <paramref name="outer"/>」的等价矩阵。
    /// SVG 的 <c>transform="A B"</c> 就是 <c>Multiply(A, B)</c>。
    /// </summary>
    public static double[] Multiply(double[] outer, double[] inner)
    {
        double oa = outer[0], ob = outer[1], oc = outer[2], od = outer[3], oe = outer[4], of = outer[5];
        double ia = inner[0], ib = inner[1], ic = inner[2], id = inner[3], ie = inner[4], iF = inner[5];

        return new[]
        {
            oa * ia + oc * ib,
            ob * ia + od * ib,
            oa * ic + oc * id,
            ob * ic + od * id,
            oa * ie + oc * iF + oe,
            ob * ie + od * iF + of
        };
    }

    public static (double X, double Y) Apply(double[] m, double x, double y)
        => (m[0] * x + m[2] * y + m[4], m[1] * x + m[3] * y + m[5]);

    public static double[] Translate(double tx, double ty) => new double[] { 1, 0, 0, 1, tx, ty };

    public static double[] Scale(double sx, double sy) => new double[] { sx, 0, 0, sy, 0, 0 };

    /// <summary>绕原点的顺时针旋转（度）。</summary>
    public static double[] Rotate(double degrees)
    {
        var r = degrees * Math.PI / 180.0;
        var cos = Math.Cos(r);
        var sin = Math.Sin(r);
        return new double[] { cos, sin, -sin, cos, 0, 0 };
    }

    /// <summary>绕 (cx, cy) 的顺时针旋转（度）。</summary>
    public static double[] RotateAbout(double degrees, double cx, double cy)
        => Multiply(Translate(cx, cy), Multiply(Rotate(degrees), Translate(-cx, -cy)));

    /// <summary>X 方向斜切（度）。</summary>
    public static double[] SkewX(double degrees)
        => new double[] { 1, 0, Math.Tan(degrees * Math.PI / 180.0), 1, 0, 0 };

    /// <summary>Y 方向斜切（度）。</summary>
    public static double[] SkewY(double degrees)
        => new double[] { 1, Math.Tan(degrees * Math.PI / 180.0), 0, 1, 0, 0 };

    /// <summary>
    /// 解析 <c>transform</c> 属性（可含多段，如 <c>"translate(10,20) scale(2) rotate(30)"</c>）。
    ///
    /// <para><b>返回 null 的两种情形语义相同 —— 都按"无变换"处理</b>：属性缺失/空，
    /// 或整串无法解析（未知命令名、缺少括号、参数个数不对）。不抛异常是刻意的：
    /// 导入外部 SVG 时宁可少一个变换，也不能因为一个畸形属性让整个导入失败
    /// （与 <c>PropertyPanelView</c> 导入路径"解析失败保持原形状"的既有约定一致）。</para>
    /// </summary>
    public static double[]? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var matrix = Identity();
        var sawAny = false;
        var i = 0;

        while (i < text.Length)
        {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ',')) i++;
            if (i >= text.Length) break;

            var nameStart = i;
            while (i < text.Length && char.IsLetter(text[i])) i++;
            if (i == nameStart) return null;                     // 不是 "名字(" 的形状
            var name = text[nameStart..i];

            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i >= text.Length || text[i] != '(') return null;

            var close = text.IndexOf(')', i);
            if (close < 0) return null;

            var args = SvgParser.ParseNumberList(text[(i + 1)..close]);
            i = close + 1;

            var segment = FromCommand(name, args);
            if (segment == null) return null;

            matrix = Multiply(matrix, segment);
            sawAny = true;
        }

        return sawAny ? matrix : null;
    }

    private static double[]? FromCommand(string name, List<double> a)
    {
        switch (name.ToLowerInvariant())
        {
            case "matrix":
                return a.Count == 6 ? a.ToArray() : null;
            case "translate":
                if (a.Count == 1) return Translate(a[0], 0);
                if (a.Count == 2) return Translate(a[0], a[1]);
                return null;
            case "scale":
                if (a.Count == 1) return Scale(a[0], a[0]);
                if (a.Count == 2) return Scale(a[0], a[1]);
                return null;
            case "rotate":
                if (a.Count == 1) return Rotate(a[0]);
                if (a.Count == 3) return RotateAbout(a[0], a[1], a[2]);
                return null;
            case "skewx":
                return a.Count == 1 ? SkewX(a[0]) : null;
            case "skewy":
                return a.Count == 1 ? SkewY(a[0]) : null;
            default:
                return null;
        }
    }

    /// <summary>格式化为可读串（调试/文档用，不参与判据）。</summary>
    public static string Describe(double[]? m)
    {
        if (m == null || m.Length < 6) return "(identity)";
        return string.Create(CultureInfo.InvariantCulture,
            $"matrix({m[0]:0.###},{m[1]:0.###},{m[2]:0.###},{m[3]:0.###},{m[4]:0.###},{m[5]:0.###})");
    }
}
