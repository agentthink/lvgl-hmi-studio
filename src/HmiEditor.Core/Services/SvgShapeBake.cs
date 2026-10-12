using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// 形状仿射的**烘焙**（唯一实现）—— 把 <see cref="SvgShape.Transform"/> 落到坐标上，
/// 产出后续环节直接消费的「有效形状」。
///
/// <para><b>为什么必须只有一份</b>：设备与画布是两台渲染栈，谁都没有等价的矩阵概念，
/// 只有把变换落到坐标上，"画布所见"与"设备所得"才可能一致。若生成侧与画布各写一份，
/// 就会出现"画布是斜的、设备是正的"这类只在端到端才暴露的分叉 —— 更糟的是判据
/// （<see cref="SvgAffine.IsAxisAligned"/>）一旦分叉，连"该不该降级成纯色"都会两边不一致。</para>
///
/// <para>轴对齐（只含平移/缩放）⇒ 保留原语义：矩形还是矩形、圆还是圆（非均匀缩放把圆
/// 拉成椭圆时改 <c>Kind</c>，这是消费期的事、不动模型）。含旋转/斜切 ⇒ 把矩形提升为
/// 四边形、圆/椭圆采样成多边形。画布侧**即便 Avalonia 本可以直接旋转也不走那条路**：
/// 两台渲染栈必须吃同一份几何，否则"一致"只能靠肉眼对。</para>
///
/// <para>Path 例外：它的几何在 <see cref="SvgShape.PathData"/> 字符串里，烘焙要先展平，
/// 所以原样返回、由调用方在展平后应用矩阵（见 <see cref="ApplyToPoints"/>）。</para>
/// </summary>
public static class SvgShapeBake
{
    /// <summary>
    /// 把形状的仿射烘进几何，返回**新的**有效形状（原形状不动）。<see cref="SvgShape.Transform"/>
    /// 为 null/单位阵、或形状是 Path 时，原样返回入参本身。
    ///
    /// <para><b>调用契约（回退即错）</b>：渐变判据必须在**烘焙之前**用原始形状判
    /// （<see cref="SvgGradientGeometry.Decide"/>）。烘焙会把 <c>Transform</c> 清成 null，
    /// 之后再判就永远得到"轴对齐"，旋转矩形会被错误地按渐变矩形发出去。</para>
    /// </summary>
    public static SvgShape Transform(SvgShape shape)
    {
        if (SvgAffine.IsIdentity(shape.Transform)) return shape;
        if (shape.Kind == SvgShapeKind.Path) return shape;

        var m = shape.Transform!;
        var clone = shape.ShallowCopy();
        clone.Transform = null;

        if (SvgAffine.IsAxisAligned(m))
        {
            switch (clone.Kind)
            {
                case SvgShapeKind.Rect:
                {
                    var p = clone.Points;
                    if (p.Length < 4) return clone;
                    var a = SvgAffine.Apply(m, p[0], p[1]);
                    var b = SvgAffine.Apply(m, p[0] + p[2], p[1] + p[3]);
                    var w = Math.Abs(b.X - a.X);
                    var h = Math.Abs(b.Y - a.Y);
                    var x = Math.Min(a.X, b.X);
                    var y = Math.Min(a.Y, b.Y);
                    clone.Points = p.Length >= 6
                        ? new[] { x, y, w, h, p[4] * Math.Abs(m[0]), p[5] * Math.Abs(m[3]) }
                        : new[] { x, y, w, h };
                    return clone;
                }
                case SvgShapeKind.Circle:
                {
                    var p = clone.Points;
                    if (p.Length < 3) return clone;
                    var c = SvgAffine.Apply(m, p[0], p[1]);
                    var rx = p[2] * Math.Abs(m[0]);
                    var ry = p[2] * Math.Abs(m[3]);
                    if (Math.Abs(rx - ry) <= 1e-9)
                    {
                        clone.Points = new[] { c.X, c.Y, rx };
                        return clone;
                    }
                    clone.Kind = SvgShapeKind.Ellipse;
                    clone.Points = new[] { c.X, c.Y, rx, ry };
                    return clone;
                }
                case SvgShapeKind.Ellipse:
                {
                    var p = clone.Points;
                    if (p.Length < 4) return clone;
                    var c = SvgAffine.Apply(m, p[0], p[1]);
                    clone.Points = new[] { c.X, c.Y, p[2] * Math.Abs(m[0]), p[3] * Math.Abs(m[3]) };
                    return clone;
                }
                default:
                    clone.Points = ApplyToPoints(clone.Points, m);
                    return clone;
            }
        }

        switch (clone.Kind)
        {
            case SvgShapeKind.Rect:
            {
                var p = clone.Points;
                if (p.Length < 4) return clone;
                clone.Kind = SvgShapeKind.Polygon;
                clone.Points = ApplyToPoints(new[]
                {
                    p[0], p[1],
                    p[0] + p[2], p[1],
                    p[0] + p[2], p[1] + p[3],
                    p[0], p[1] + p[3]
                }, m);
                return clone;
            }
            case SvgShapeKind.Circle:
            case SvgShapeKind.Ellipse:
            {
                var p = clone.Points;
                var (cx, cy, rx, ry) = clone.Kind == SvgShapeKind.Circle
                    ? (p[0], p[1], p[2], p[2])
                    : (p[0], p[1], p[2], p[3]);
                clone.Kind = SvgShapeKind.Polygon;
                clone.Points = ApplyToPoints(SampleEllipse(cx, cy, rx, ry).ToArray(), m);
                return clone;
            }
            default:
                clone.Points = ApplyToPoints(clone.Points, m);
                return clone;
        }
    }

    /// <summary>对「x0,y0,x1,y1,…」点列就地应用仿射，返回新数组。</summary>
    public static double[] ApplyToPoints(double[] pts, double[] m)
    {
        var result = new double[pts.Length];
        for (var i = 0; i + 1 < pts.Length; i += 2)
        {
            var (x, y) = SvgAffine.Apply(m, pts[i], pts[i + 1]);
            result[i] = x;
            result[i + 1] = y;
        }
        return result;
    }

    /// <summary>
    /// 仿射的**均匀**缩放因子 —— 用来缩放描边宽度。取 √|det|：纯旋转 = 1（描边不该变粗），
    /// 缩放 2 倍 = 2。非均匀缩放下这是近似值，与半径"取 x 方向"的近似同源。
    /// </summary>
    public static double Scale(double[]? m)
    {
        if (m == null || m.Length < 6) return 1.0;
        var det = Math.Abs(m[0] * m[3] - m[1] * m[2]);
        return det <= double.Epsilon ? 1.0 : Math.Sqrt(det);
    }

    /// <summary>
    /// 椭圆采样成点列（32 段）—— 几何采样的**唯一实现**：烘焙把圆/椭圆提升成多边形时用它，
    /// 生成侧发射圆/椭圆描边时也用它（此前 <c>SvgToLvglConverter</c> 与
    /// <c>ShapeControlCodeGen</c> 各有一份逐字相同的私有副本）。
    /// </summary>
    public static List<double> SampleEllipse(double cx, double cy, double rx, double ry)
    {
        var pts = new List<double>();
        const int segments = 32;
        for (var i = 0; i < segments; i++)
        {
            var a = i * Math.PI * 2 / segments;
            pts.Add(cx + rx * Math.Cos(a));
            pts.Add(cy + ry * Math.Sin(a));
        }
        return pts;
    }
}
