using System.Globalization;
using System.Xml.Linq;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// 把 SVG 里的 <c>&lt;linearGradient&gt;</c> / <c>&lt;radialGradient&gt;</c> 定义收成
/// <c>id → <see cref="SvgGradient"/></c> 表。
///
/// <para><b>为什么要单独一遍扫描</b>：SVG 允许 <c>&lt;defs&gt;</c> 出现在引用之后
/// （<c>&lt;rect fill="url(#g)"/&gt;</c> 写在 <c>&lt;defs&gt;</c> 之前是合法的、也很常见）。
/// 边走边解析会得到一批"此刻还没定义"的悬空引用。</para>
///
/// <para><b>href 继承</b>（<c>xlink:href</c> / <c>href</c>）按"最近者优先"沿链条合并属性，
/// 并用 visited 集合做环守卫 —— 自引用/互引用的 SVG 在野外确实存在，不守卫就是死循环。
/// 停靠点按 SVG 语义单独继承：只有**自己一个 stop 都没有**时才用被引用者的。</para>
/// </summary>
internal static class SvgGradientCollector
{
    public static Dictionary<string, SvgGradient> Collect(XElement root, double viewportWidth, double viewportHeight)
    {
        var raw = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var el in root.DescendantsAndSelf())
        {
            var name = el.Name.LocalName.ToLowerInvariant();
            if (name is not ("lineargradient" or "radialgradient")) continue;

            var id = SvgParser.GetAttr(el, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            raw[id.Trim()] = el;
        }

        var result = new Dictionary<string, SvgGradient>(StringComparer.Ordinal);
        foreach (var (id, el) in raw)
            result[id] = Build(el.Name.LocalName.ToLowerInvariant(), id, raw, viewportWidth, viewportHeight);
        return result;
    }

    private static SvgGradient Build(string ownName, string id, Dictionary<string, XElement> raw,
        double viewportWidth, double viewportHeight)
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        XElement? stopSource = null;
        var visited = new HashSet<string>(StringComparer.Ordinal);

        var cursor = id;
        while (cursor != null && raw.TryGetValue(cursor, out var el) && visited.Add(cursor))
        {
            foreach (var a in el.Attributes())
            {
                // 最近者优先：链上前面的元素已经写过的属性不再被后面的覆盖
                if (!attrs.ContainsKey(a.Name.LocalName)) attrs[a.Name.LocalName] = a.Value;
            }
            if (stopSource == null && el.Elements().Any(c => IsStop(c))) stopSource = el;

            var href = SvgParser.GetAttr(el, "href");
            cursor = href != null && href.TrimStart().StartsWith('#') ? href.Trim()[1..].Trim() : null;
        }

        var userSpace = string.Equals(Read(attrs, "gradientUnits"), "userSpaceOnUse", StringComparison.OrdinalIgnoreCase);
        var gradient = new SvgGradient
        {
            Kind = ownName == "lineargradient" ? SvgGradientKind.Linear : SvgGradientKind.Radial,
            UserSpace = userSpace,
            Transform = SvgAffine.Parse(Read(attrs, "gradientTransform"))
        };

        if (gradient.Kind == SvgGradientKind.Linear)
        {
            var inX = userSpace ? viewportWidth : 1.0;
            var inY = userSpace ? viewportHeight : 1.0;
            gradient.Coords = new[]
            {
                Length(Read(attrs, "x1") ?? "0%", inX),
                Length(Read(attrs, "y1") ?? "0%", inY),
                Length(Read(attrs, "x2") ?? "100%", inX),
                Length(Read(attrs, "y2") ?? "0%", inY)
            };
        }
        else
        {
            var inX = userSpace ? viewportWidth : 1.0;
            var inY = userSpace ? viewportHeight : 1.0;
            // userSpaceOnUse 下 r 的百分比按规范取「归一化对角线」长度
            var inR = userSpace
                ? Math.Sqrt((viewportWidth * viewportWidth + viewportHeight * viewportHeight) / 2.0)
                : 1.0;

            var cx = Length(Read(attrs, "cx") ?? "50%", inX);
            var cy = Length(Read(attrs, "cy") ?? "50%", inY);
            var r = Length(Read(attrs, "r") ?? "50%", inR);

            var fx = Read(attrs, "fx");
            var fy = Read(attrs, "fy");
            gradient.Coords = fx != null || fy != null
                ? new[] { cx, cy, r, Length(fx ?? "50%", inX), Length(fy ?? "50%", inY) }
                : new[] { cx, cy, r };
        }

        if (stopSource != null) gradient.Stops = ReadStops(stopSource);
        return gradient;
    }

    private static bool IsStop(XElement el)
        => string.Equals(el.Name.LocalName, "stop", StringComparison.OrdinalIgnoreCase);

    private static string? Read(IReadOnlyDictionary<string, string> attrs, string name)
        => attrs.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    /// <summary>
    /// 把 <c>50%</c> 形式按 <paramref name="axisLength"/> 换算成绝对量；<c>%</c> 之外的值原样使用。
    /// objectBoundingBox 模式下 axisLength = 1，于是百分比天然落成 0..1 的相对值。
    /// </summary>
    private static double Length(string value, double axisLength)
    {
        var v = value.Trim();
        if (v.EndsWith('%'))
        {
            var pct = SvgParser.ParseNumberList(v);
            if (pct.Count > 0) return pct[0] / 100.0 * axisLength;
            return 0;
        }
        var nums = SvgParser.ParseNumberList(v);
        return nums.Count > 0 ? nums[0] : 0;
    }

    private static List<SvgGradientStop> ReadStops(XElement gradientEl)
    {
        var stops = new List<SvgGradientStop>();
        foreach (var el in gradientEl.Elements())
        {
            if (!IsStop(el)) continue;

            var color = SvgParser.GetAttr(el, "stop-color");
            var styleAttr = SvgParser.GetAttr(el, "style");
            if (!string.IsNullOrEmpty(styleAttr))
            {
                foreach (var decl in styleAttr.Split(';'))
                {
                    var idx = decl.IndexOf(':');
                    if (idx <= 0) continue;
                    var key = decl[..idx].Trim();
                    var value = decl[(idx + 1)..].Trim();
                    if (key.Equals("stop-color", StringComparison.OrdinalIgnoreCase)) color = value;
                    // stop-opacity 刻意忽略：本项目颜色模型是 24 位 RGB、无 alpha 通道
                }
            }

            stops.Add(new SvgGradientStop
            {
                Offset = ParseOffset(SvgParser.GetAttr(el, "offset")),
                Color = SvgParser.NormalizeColor(color) ?? "#000000"
            });
        }

        return stops.OrderBy(s => s.Offset).ToList();
    }

    private static double ParseOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var v = value.Trim();
        double offset;
        if (v.EndsWith('%'))
        {
            offset = SvgParser.ParseNumberList(v) is { Count: > 0 } n ? n[0] / 100.0 : 0;
        }
        else if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out offset))
        {
            return 0;
        }
        return Math.Clamp(offset, 0, 1);
    }
}
