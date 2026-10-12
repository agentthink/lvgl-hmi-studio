using System.Globalization;

namespace HmiEditor.Core.Services;

/// <summary>单个子路径（闭合状态 + 折线点集）。</summary>
public sealed class SvgSubPath
{
    public bool Closed { get; set; }
    public List<double> Points { get; } = new(); // x0, y0, x1, y1, ...
}

/// <summary>
/// I6: SVG path 命令展平器 —— 将 path 的 d 属性命令串转换为折线段点集。
/// 支持 M/L/H/V/C/S/Q/T/A/Z（含相对坐标），贝塞尔细分 12 段、二次曲线 8 段、
/// 椭圆弧采用 endpoint-to-center 转换后按角度采样。
/// 输出供设计画布渲染与 LVGL CodeGen 共用（统一曲线近似策略）。
/// </summary>
public static class SvgPathFlattener
{
    private const int CubicSegments = 12;
    private const int QuadraticSegments = 8;

    /// <summary>展平 path 命令串。无效输入返回空列表。</summary>
    public static List<SvgSubPath> Flatten(string? pathData)
    {
        var result = new List<SvgSubPath>();
        if (string.IsNullOrWhiteSpace(pathData)) return result;

        var tokens = Tokenize(pathData);
        if (tokens.Count == 0) return result;

        SvgSubPath? current = null;
        double cx = 0, cy = 0;      // 当前点
        double sx = 0, sy = 0;      // 子路径起点
        double px = 0, py = 0;      // 前一个控制点（S/T 用）
        bool prevCubic = false, prevQuad = false;

        int i = 0;
        while (i < tokens.Count)
        {
            var (cmd, num) = tokens[i];
            if (cmd == 0) break; // 数字 token 不应在命令位

            bool relative = char.IsLower(cmd);
            var upper = char.ToUpperInvariant(cmd);
            i++;

            if (upper == 'Z')
            {
                if (current != null && current.Points.Count >= 2)
                {
                    if (Math.Abs(cx - sx) > 1e-9 || Math.Abs(cy - sy) > 1e-9)
                    {
                        current.Points.Add(sx);
                        current.Points.Add(sy);
                    }
                    current.Closed = true;
                    cx = sx; cy = sy;
                }
                continue;
            }

            switch (upper)
            {
                case 'M':
                {
                    var coords = ReadPairs(tokens, ref i, relative, cx, cy);
                    if (coords.Count == 0) break;
                    cx = coords[0]; cy = coords[1];
                    sx = cx; sy = cy;
                    current = new SvgSubPath();
                    current.Points.Add(cx); current.Points.Add(cy);
                    result.Add(current);
                    for (int k = 2; k + 1 < coords.Count; k += 2)
                    {
                        cx = coords[k]; cy = coords[k + 1];
                        current.Points.Add(cx); current.Points.Add(cy);
                    }
                    break;
                }
                case 'L':
                {
                    var coords = ReadPairs(tokens, ref i, relative, cx, cy);
                    for (int k = 0; k + 1 < coords.Count; k += 2)
                    {
                        cx = coords[k]; cy = coords[k + 1];
                        current?.Points.Add(cx); current?.Points.Add(cy);
                    }
                    break;
                }
                case 'H':
                {
                    while (TryReadNumber(tokens, ref i, out var v))
                    {
                        cx = relative ? cx + v : v;
                        current?.Points.Add(cx); current?.Points.Add(cy);
                    }
                    break;
                }
                case 'V':
                {
                    while (TryReadNumber(tokens, ref i, out var v))
                    {
                        cy = relative ? cy + v : v;
                        current?.Points.Add(cx); current?.Points.Add(cy);
                    }
                    break;
                }
                case 'C':
                {
                    var coords = ReadPairs(tokens, ref i, relative, cx, cy);
                    for (int k = 0; k + 5 < coords.Count; k += 6)
                    {
                        var c1 = (coords[k], coords[k + 1]);
                        var c2 = (coords[k + 2], coords[k + 3]);
                        var end = (coords[k + 4], coords[k + 5]);
                        AppendCubic(current, cx, cy, c1, c2, end);
                        px = c2.Item1; py = c2.Item2; prevCubic = true; prevQuad = false;
                        cx = end.Item1; cy = end.Item2;
                    }
                    break;
                }
                case 'S':
                {
                    var coords = ReadPairs(tokens, ref i, relative, cx, cy);
                    for (int k = 0; k + 3 < coords.Count; k += 4)
                    {
                        var c1 = prevCubic ? (2 * cx - px, 2 * cy - py) : (cx, cy);
                        var c2 = (coords[k], coords[k + 1]);
                        var end = (coords[k + 2], coords[k + 3]);
                        AppendCubic(current, cx, cy, c1, c2, end);
                        px = c2.Item1; py = c2.Item2; prevCubic = true; prevQuad = false;
                        cx = end.Item1; cy = end.Item2;
                    }
                    break;
                }
                case 'Q':
                {
                    var coords = ReadPairs(tokens, ref i, relative, cx, cy);
                    for (int k = 0; k + 3 < coords.Count; k += 4)
                    {
                        var ctrl = (coords[k], coords[k + 1]);
                        var end = (coords[k + 2], coords[k + 3]);
                        AppendQuadratic(current, cx, cy, ctrl, end);
                        px = ctrl.Item1; py = ctrl.Item2; prevQuad = true; prevCubic = false;
                        cx = end.Item1; cy = end.Item2;
                    }
                    break;
                }
                case 'T':
                {
                    var coords = ReadPairs(tokens, ref i, relative, cx, cy);
                    for (int k = 0; k + 1 < coords.Count; k += 2)
                    {
                        var ctrl = prevQuad ? (2 * cx - px, 2 * cy - py) : (cx, cy);
                        var end = (coords[k], coords[k + 1]);
                        AppendQuadratic(current, cx, cy, ctrl, end);
                        prevQuad = true; prevCubic = false;
                        cx = end.Item1; cy = end.Item2;
                    }
                    break;
                }
                case 'A':
                {
                    while (i + 6 < tokens.Count)
                    {
                        if (!TryReadNumber(tokens, ref i, out var rx)) break;
                        if (!TryReadNumber(tokens, ref i, out var ry)) break;
                        if (!TryReadNumber(tokens, ref i, out var rot)) break;
                        if (!TryReadNumber(tokens, ref i, out var large)) break;
                        if (!TryReadNumber(tokens, ref i, out var sweep)) break;
                        if (!TryReadNumber(tokens, ref i, out var ex)) break;
                        if (!TryReadNumber(tokens, ref i, out var ey)) break;

                        var end = relative ? (cx + ex, cy + ey) : (ex, ey);
                        AppendArc(current, cx, cy, rx, ry, rot, large >= 0.5, sweep >= 0.5, end.Item1, end.Item2);
                        cx = end.Item1; cy = end.Item2;
                        prevCubic = false; prevQuad = false;
                    }
                    break;
                }
            }

            if (upper is not ('S' or 'T' or 'C' or 'Q' or 'A'))
            {
                prevCubic = false;
                prevQuad = false;
            }
        }

        return result;
    }

    private static void AppendCubic(SvgSubPath? path, double x0, double y0,
        (double, double) c1, (double, double) c2, (double, double) end)
    {
        if (path == null) return;
        for (int s = 1; s <= CubicSegments; s++)
        {
            double t = s / (double)CubicSegments;
            double mt = 1 - t;
            double x = mt * mt * mt * x0 + 3 * mt * mt * t * c1.Item1 + 3 * mt * t * t * c2.Item1 + t * t * t * end.Item1;
            double y = mt * mt * mt * y0 + 3 * mt * mt * t * c1.Item2 + 3 * mt * t * t * c2.Item2 + t * t * t * end.Item2;
            path.Points.Add(x); path.Points.Add(y);
        }
    }

    private static void AppendQuadratic(SvgSubPath? path, double x0, double y0,
        (double, double) ctrl, (double, double) end)
    {
        if (path == null) return;
        for (int s = 1; s <= QuadraticSegments; s++)
        {
            double t = s / (double)QuadraticSegments;
            double mt = 1 - t;
            double x = mt * mt * x0 + 2 * mt * t * ctrl.Item1 + t * t * end.Item1;
            double y = mt * mt * y0 + 2 * mt * t * ctrl.Item2 + t * t * end.Item2;
            path.Points.Add(x); path.Points.Add(y);
        }
    }

    private static void AppendArc(SvgSubPath? path, double x0, double y0,
        double rx, double ry, double rotDeg, bool large, bool sweep, double x1, double y1)
    {
        if (path == null) return;

        rx = Math.Abs(rx); ry = Math.Abs(ry);
        const double tau = Math.PI * 2;

        if (rx == 0 || ry == 0 || (Math.Abs(x0 - x1) < 1e-9 && Math.Abs(y0 - y1) < 1e-9))
        {
            path.Points.Add(x1); path.Points.Add(y1);
            return;
        }

        double phi = rotDeg * Math.PI / 180;
        double cosP = Math.Cos(phi), sinP = Math.Sin(phi);

        double dx2 = (x0 - x1) / 2, dy2 = (y0 - y1) / 2;
        double x1p = cosP * dx2 + sinP * dy2;
        double y1p = -sinP * dx2 + cosP * dy2;

        double lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
        if (lambda > 1)
        {
            double s = Math.Sqrt(lambda);
            rx *= s; ry *= s;
        }

        double rx2 = rx * rx, ry2 = ry * ry;
        double num = Math.Max(0, rx2 * ry2 - rx2 * y1p * y1p - ry2 * x1p * x1p);
        double den = rx2 * y1p * y1p + ry2 * x1p * x1p;
        double coef = den == 0 ? 0 : Math.Sqrt(num / den);
        if (large == sweep) coef = -coef;

        double cxp = coef * (rx * y1p / ry);
        double cyp = coef * (-ry * x1p / rx);

        double cx = cosP * cxp - sinP * cyp + (x0 + x1) / 2;
        double cy = sinP * cxp + cosP * cyp + (y0 + y1) / 2;

        double ux = (x1p - cxp) / rx, uy = (y1p - cyp) / ry;
        double vx = (-x1p - cxp) / rx, vy = (-y1p - cyp) / ry;

        double startAngle = AngleBetween(1, 0, ux, uy);
        double deltaAngle = AngleBetween(ux, uy, vx, vy);
        if (!sweep && deltaAngle > 0) deltaAngle -= tau;
        if (sweep && deltaAngle < 0) deltaAngle += tau;

        int segments = Math.Max(8, (int)Math.Ceiling(Math.Abs(deltaAngle) / (Math.PI / 16)));
        for (int s = 1; s <= segments; s++)
        {
            double a = startAngle + deltaAngle * s / segments;
            double ex = cx + rx * Math.Cos(a) * cosP - ry * Math.Sin(a) * sinP;
            double ey = cy + rx * Math.Cos(a) * sinP + ry * Math.Sin(a) * cosP;
            path.Points.Add(ex); path.Points.Add(ey);
        }
    }

    private static double AngleBetween(double ux, double uy, double vx, double vy)
    {
        double dot = ux * vx + uy * vy;
        double len = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
        if (len == 0) return 0;
        double angle = Math.Acos(Math.Clamp(dot / len, -1, 1));
        return (ux * vy - uy * vx) < 0 ? -angle : angle;
    }

    // ── 词法：命令字母与数字（数字保留字符串值，逗号/空白分隔）──

    private sealed record Token(char Cmd, string? Num);

    private static List<Token> Tokenize(string data)
    {
        var result = new List<Token>();
        var sb = new System.Text.StringBuilder();
        bool hasNumber = false;

        void Flush()
        {
            if (hasNumber)
            {
                result.Add(new Token('N', sb.ToString()));
                sb.Clear();
                hasNumber = false;
            }
        }

        foreach (var c in data)
        {
            if (char.IsLetter(c))
            {
                Flush();
                result.Add(new Token(c, null));
            }
            else if (char.IsDigit(c) || c is '-' or '+' or '.' or 'e' or 'E')
            {
                sb.Append(c);
                hasNumber = true;
            }
            else
            {
                Flush(); // 逗号、空白等分隔
            }
        }
        Flush();
        return result;
    }

    /// <summary>读取下一组相对/绝对坐标对（直到非数字 token），返回绝对坐标列表。</summary>
    private static List<double> ReadPairs(List<Token> tokens, ref int i,
        bool relative, double cx, double cy)
    {
        var result = new List<double>();
        while (i < tokens.Count && tokens[i].Cmd == 'N')
        {
            if (!TryParseNum(tokens[i].Num, out var v)) break;
            i++;
            result.Add(relative ? cx + v : v);

            if (i < tokens.Count && tokens[i].Cmd == 'N')
            {
                if (!TryParseNum(tokens[i].Num, out var vy)) break;
                i++;
                result.Add(relative ? cy + vy : vy);
            }
            else
            {
                break;
            }
        }
        return result;
    }

    private static bool TryReadNumber(List<Token> tokens, ref int i, out double value)
    {
        value = 0;
        if (i >= tokens.Count || tokens[i].Cmd != 'N') return false;
        if (!TryParseNum(tokens[i].Num, out value)) return false;
        i++;
        return true;
    }

    private static bool TryParseNum(string? s, out double value)
    {
        value = 0;
        return s != null &&
               double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
