using System.Globalization;
using System.Xml.Linq;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// I6: 轻量 SVG 解析器 —— 将 SVG 文件解析为类型化的 <see cref="SvgDocument"/>。
///
/// 支持范围（对标主流 HMI 工具矢量导入的核心子集）：
///   - 根元素: svg (viewBox / width / height)
///   - 形状: rect / circle / ellipse / line / polyline / polygon / path
///   - 样式: fill / stroke / stroke-width / fill-rule（属性或 style="..."）
///   - 分组: g 元素（样式与 transform 向子元素继承）
///   - 颜色: #RGB / #RRGGBB / 命名色 / none / rgb()
///   - 渐变: defs 下的 linearGradient / radialGradient（stop、href 继承、gradientUnits、gradientTransform），
///           由 fill/stroke 的 url(#id) 引用
///   - 变换: transform 属性（matrix / translate / scale / rotate / skewX / skewY，多段串联）
///
/// 明确不支持（解析时跳过）：图案 / 滤镜 / 嵌入位图 / 文本。
///
/// <para><b>两处"降级"是刻意设计、不是缺口</b>，都写在
/// <see cref="SvgShape.Fill"/> / <see cref="SvgGradientGeometry"/> 上：
/// ① 渐变形状仍会保留一个纯色 <c>Fill</c>，因为 LVGL 只有轴对齐矩形/圆能画真渐变；
/// ② <c>stop-opacity</c> / <c>spreadMethod</c> 被忽略（本项目颜色模型是 24 位 RGB、无 alpha）。</para>
///
/// 解析器无外部依赖（System.Xml.Linq），保持嵌入式工具链纯净。
/// </summary>
public static class SvgParser
{
    private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000000", ["white"] = "#ffffff", ["red"] = "#ff0000",
        ["green"] = "#008000", ["lime"] = "#00ff00", ["blue"] = "#0000ff",
        ["yellow"] = "#ffff00", ["cyan"] = "#00ffff", ["aqua"] = "#00ffff",
        ["magenta"] = "#ff00ff", ["fuchsia"] = "#ff00ff", ["gray"] = "#808080",
        ["grey"] = "#808080", ["silver"] = "#c0c0c0", ["maroon"] = "#800000",
        ["olive"] = "#808000", ["navy"] = "#000080", ["purple"] = "#800080",
        ["teal"] = "#008080", ["orange"] = "#ffa500", ["transparent"] = "none"
    };

    /// <summary>
    /// 解析 SVG 文本。成功返回 (文档, null)；失败返回 (null, <see cref="SvgParseError"/>)。
    ///
    /// <para><b>失败为什么返回对象而不是裸串</b>（阶段 87，登记 #61a）：裸中文串被两个
    /// App 站点直通模板（<c>asset.svg.parse.fail</c>）、又被 AI 侧包进裸异常
    /// （<c>InvalidOperationException($"SVG 生成无效: {error}")</c>）⇒ 英文界面下那一半照旧是中文。
    /// 改成「码 + 中文回落」之后，App 侧走 <c>ExternalText.Describe</c>（**返回值档**）、
    /// AI 侧走 <see cref="SvgParseException"/> + <c>ExternalText.FailureReason</c>（**异常档**）。</para>
    ///
    /// <para><b>返回类型换掉是故意的</b>：它让编译器**当场点出全部消费点** ——
    /// 实测只有 4 处把 <c>Error</c> 当字符串用（2 个 UI 站点 + 2 个 AI 生成器），
    /// 其余约 35 处是 <c>(doc, _)</c> 或 <c>Assert.Null/NotNull</c>，与类型无关。
    /// 若改成"另加一个带码重载、旧签名留着"，旧路仍可被误用，就还得再写一条静态守卫去盯它 ——
    /// 能用类型系统表达的约束不要退化成文本守卫（阶段 69 的正面用法）。</para>
    /// </summary>
    public static (SvgDocument? Document, SvgParseError? Error) Parse(string svgText)
    {
        if (string.IsNullOrWhiteSpace(svgText))
            return (null, new SvgParseError(SvgParseIssueCodes.CONTENT_EMPTY, "SVG 内容为空"));

        XDocument xdoc;
        try
        {
            xdoc = XDocument.Parse(svgText, LoadOptions.None);
        }
        catch (Exception ex)
        {
            // .NET 原文**不进模板**（英文 + 行列号），但留在回落里：排障靠它。
            return (null, new SvgParseError(SvgParseIssueCodes.XML_INVALID, $"XML 解析失败: {ex.Message}"));
        }

        var svgEl = xdoc.Root;
        if (svgEl == null || !string.Equals(svgEl.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
            return (null, new SvgParseError(SvgParseIssueCodes.ROOT_NOT_SVG, "根元素不是 <svg>"));

        var doc = new SvgDocument();
        ParseViewBox(svgEl, doc);

        // 渐变定义先扫一遍（SVG 允许 <defs> 出现在引用之后，所以不能边走边解析）
        var gradients = SvgGradientCollector.Collect(svgEl, doc.ViewBoxWidth, doc.ViewBoxHeight);

        // 解析形状（根级默认样式为空、无变换）
        ParseChildren(svgEl, doc, new StyleContext(), gradients);

        return (doc, null);
    }

    private static void ParseViewBox(XElement svgEl, SvgDocument doc)
    {
        var vb = GetAttr(svgEl, "viewBox");
        if (!string.IsNullOrEmpty(vb))
        {
            var nums = ParseNumberList(vb);
            if (nums.Count == 4)
            {
                doc.ViewBoxX = nums[0];
                doc.ViewBoxY = nums[1];
                doc.ViewBoxWidth = Math.Max(nums[2], 1);
                doc.ViewBoxHeight = Math.Max(nums[3], 1);
            }
        }

        doc.Width = TryParseDouble(GetAttr(svgEl, "width")) ?? doc.ViewBoxWidth;
        doc.Height = TryParseDouble(GetAttr(svgEl, "height")) ?? doc.ViewBoxHeight;
    }

    private static void ParseChildren(XElement parent, SvgDocument doc, StyleContext inherited,
        IReadOnlyDictionary<string, SvgGradient> gradients)
    {
        foreach (var el in parent.Elements())
        {
            var name = el.Name.LocalName.ToLowerInvariant();

            // 渐变定义本身不是图形；它已在 SvgGradientCollector 里收走并被形状按 id 引用
            if (name is "lineargradient" or "radialgradient" or "stop")
                continue;
            if (name == "defs")
            {
                // defs 里只可能有定义元素（不产生形状）；仍递归，由其内部按名跳过
                ParseChildren(el, doc, inherited, gradients);
                continue;
            }

            var style = StyleContext.ForElement(el, inherited);

            switch (name)
            {
                case "g":
                    ParseChildren(el, doc, style, gradients);
                    break;
                case "rect":
                    AddShape(doc, ParseRect(el, style), style, gradients);
                    break;
                case "circle":
                    AddShape(doc, ParseCircle(el, style), style, gradients);
                    break;
                case "ellipse":
                    AddShape(doc, ParseEllipse(el, style), style, gradients);
                    break;
                case "line":
                    AddShape(doc, ParseLine(el, style), style, gradients);
                    break;
                case "polyline":
                    AddShape(doc, ParsePolyline(el, style, close: false), style, gradients);
                    break;
                case "polygon":
                    AddShape(doc, ParsePolyline(el, style, close: true), style, gradients);
                    break;
                case "path":
                    AddShape(doc, ParsePath(el, style), style, gradients);
                    break;
                // 图案/滤镜/文本/图片等: 跳过
            }
        }
    }

    /// <summary>
    /// 收尾：挂渐变（并按约定把 <c>Fill</c>/<c>Stroke</c> 落成**降级纯色**）、挂仿射，再入列。
    ///
    /// <para>渐变引用解析失败（id 不存在）时回退到 <c>url(#id)</c> 之后紧跟的 fallback 颜色，
    /// 没有 fallback 就等于无填充 —— 与 SVG 2 的语义一致，且不会因为一个坏引用整图变黑。</para>
    /// </summary>
    private static void AddShape(SvgDocument doc, SvgShape? shape, StyleContext style,
        IReadOnlyDictionary<string, SvgGradient> gradients)
    {
        if (shape == null) return;

        shape.Transform = style.Affine;

        // 只有"可填充"形状才挂填充渐变：Line / Polyline 的 Fill 被解析器写成 "none"，
        // 若在这里按 url(#id) 覆盖就会把线变成有面填充的图形。
        if (style.FillGradientRef != null && shape.Fill is not "none")
        {
            shape.FillGradient = LookupGradient(style.FillGradientRef, gradients);
            shape.Fill = shape.FillGradient != null
                ? SvgGradientGeometry.SampleMidColor(shape.FillGradient)
                : style.FillFallback;
        }

        if (style.StrokeGradientRef != null)
        {
            shape.StrokeGradient = LookupGradient(style.StrokeGradientRef, gradients);
            shape.Stroke = shape.StrokeGradient != null
                ? SvgGradientGeometry.SampleMidColor(shape.StrokeGradient)
                : style.StrokeFallback;
        }

        doc.Shapes.Add(shape);
    }

    private static SvgGradient? LookupGradient(string id, IReadOnlyDictionary<string, SvgGradient> gradients)
        => gradients.TryGetValue(id, out var g) && g.Stops.Count > 0 ? g : null;

    private static SvgShape? ParseRect(XElement el, StyleContext style)
    {
        var x = TryParseDouble(GetAttr(el, "x")) ?? 0;
        var y = TryParseDouble(GetAttr(el, "y")) ?? 0;
        var w = TryParseDouble(GetAttr(el, "width")) ?? 0;
        var h = TryParseDouble(GetAttr(el, "height")) ?? 0;
        if (w <= 0 || h <= 0) return null;

        var rx = TryParseDouble(GetAttr(el, "rx"));
        var ry = TryParseDouble(GetAttr(el, "ry"));
        var points = rx.HasValue || ry.HasValue
            ? new[] { x, y, w, h, rx ?? 0, ry ?? 0 }
            : new[] { x, y, w, h };

        return new SvgShape
        {
            Kind = SvgShapeKind.Rect,
            Points = points,
            Fill = style.Fill,
            Stroke = style.Stroke,
            StrokeWidth = style.StrokeWidth
        };
    }

    private static SvgShape? ParseCircle(XElement el, StyleContext style)
    {
        var cx = TryParseDouble(GetAttr(el, "cx")) ?? 0;
        var cy = TryParseDouble(GetAttr(el, "cy")) ?? 0;
        var r = TryParseDouble(GetAttr(el, "r")) ?? 0;
        if (r <= 0) return null;

        return new SvgShape
        {
            Kind = SvgShapeKind.Circle,
            Points = new[] { cx, cy, r },
            Fill = style.Fill,
            Stroke = style.Stroke,
            StrokeWidth = style.StrokeWidth
        };
    }

    private static SvgShape? ParseEllipse(XElement el, StyleContext style)
    {
        var cx = TryParseDouble(GetAttr(el, "cx")) ?? 0;
        var cy = TryParseDouble(GetAttr(el, "cy")) ?? 0;
        var rx = TryParseDouble(GetAttr(el, "rx")) ?? 0;
        var ry = TryParseDouble(GetAttr(el, "ry")) ?? 0;
        if (rx <= 0 || ry <= 0) return null;

        return new SvgShape
        {
            Kind = SvgShapeKind.Ellipse,
            Points = new[] { cx, cy, rx, ry },
            Fill = style.Fill,
            Stroke = style.Stroke,
            StrokeWidth = style.StrokeWidth
        };
    }

    private static SvgShape? ParseLine(XElement el, StyleContext style)
    {
        return new SvgShape
        {
            Kind = SvgShapeKind.Line,
            Points = new[]
            {
                TryParseDouble(GetAttr(el, "x1")) ?? 0,
                TryParseDouble(GetAttr(el, "y1")) ?? 0,
                TryParseDouble(GetAttr(el, "x2")) ?? 0,
                TryParseDouble(GetAttr(el, "y2")) ?? 0
            },
            Fill = "none",
            Stroke = style.Stroke,
            StrokeWidth = style.StrokeWidth
        };
    }

    private static SvgShape? ParsePolyline(XElement el, StyleContext style, bool close)
    {
        var pts = ParseNumberList(GetAttr(el, "points") ?? "");
        if (pts.Count < 4) return null;

        return new SvgShape
        {
            Kind = close ? SvgShapeKind.Polygon : SvgShapeKind.Polyline,
            Points = pts.ToArray(),
            Fill = close ? style.Fill : "none",
            Stroke = style.Stroke,
            StrokeWidth = style.StrokeWidth
        };
    }

    private static SvgShape? ParsePath(XElement el, StyleContext style)
    {
        var d = GetAttr(el, "d");
        if (string.IsNullOrWhiteSpace(d)) return null;

        return new SvgShape
        {
            Kind = SvgShapeKind.Path,
            PathData = d,
            Fill = style.Fill,
            Stroke = style.Stroke,
            StrokeWidth = style.StrokeWidth
        };
    }

    // ── 样式解析 ──

    private sealed class StyleContext
    {
        public string? Fill { get; set; }
        public string? Stroke { get; set; }
        public double StrokeWidth { get; set; } = 1.0;

        /// <summary>fill="url(#id)" 引用到的渐变 id；null 表示纯色。</summary>
        public string? FillGradientRef { get; set; }

        /// <summary>stroke="url(#id)" 引用到的渐变 id。</summary>
        public string? StrokeGradientRef { get; set; }

        /// <summary>url(#id) 之后紧跟的 fallback 颜色（渐变解析不出来时才用）。</summary>
        public string? FillFallback { get; set; }
        public string? StrokeFallback { get; set; }

        /// <summary>
        /// 继承下来的仿射与本元素 transform 的合成结果（外层先应用）。null 表示全程无变换。
        /// </summary>
        public double[]? Affine { get; set; }

        public static StyleContext ForElement(XElement el, StyleContext inherited)
        {
            var ctx = new StyleContext
            {
                Fill = inherited.Fill,
                Stroke = inherited.Stroke,
                StrokeWidth = inherited.StrokeWidth,
                FillGradientRef = inherited.FillGradientRef,
                StrokeGradientRef = inherited.StrokeGradientRef,
                FillFallback = inherited.FillFallback,
                StrokeFallback = inherited.StrokeFallback,
                Affine = inherited.Affine
            };

            // style="fill:#ff0000; stroke:none" 解析
            var styleAttr = GetAttr(el, "style");
            if (!string.IsNullOrEmpty(styleAttr))
            {
                foreach (var decl in styleAttr.Split(';'))
                {
                    var idx = decl.IndexOf(':');
                    if (idx <= 0) continue;
                    var key = decl[..idx].Trim().ToLowerInvariant();
                    var value = decl[(idx + 1)..].Trim();
                    ApplyStyle(ctx, key, value);
                }
            }

            ApplyStyle(ctx, "fill", GetAttr(el, "fill"));
            ApplyStyle(ctx, "stroke", GetAttr(el, "stroke"));
            var sw = TryParseDouble(GetAttr(el, "stroke-width"));
            if (sw.HasValue) ctx.StrokeWidth = Math.Max(sw.Value, 0);

            // transform 是逐层叠加的：外层先应用 ⇒ Affine = 继承 · 本元素
            var own = SvgAffine.Parse(GetAttr(el, "transform"));
            if (own != null)
            {
                ctx.Affine = ctx.Affine == null ? own : SvgAffine.Multiply(ctx.Affine, own);
            }

            return ctx;
        }

        private static void ApplyStyle(StyleContext ctx, string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            switch (key)
            {
                case "fill":
                    ApplyPaint(value, out var fillRef, out var fillFallback);
                    ctx.FillGradientRef = fillRef;
                    ctx.FillFallback = fillFallback;
                    ctx.Fill = fillRef == null ? NormalizeColor(value) : null;
                    break;
                case "stroke":
                    ApplyPaint(value, out var strokeRef, out var strokeFallback);
                    ctx.StrokeGradientRef = strokeRef;
                    ctx.StrokeFallback = strokeFallback;
                    ctx.Stroke = strokeRef == null ? NormalizeColor(value) : null;
                    break;
                case "stroke-width":
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var sw))
                        ctx.StrokeWidth = Math.Max(sw, 0);
                    break;
            }
        }

        /// <summary>
        /// 识别 <c>url(#id)</c>（可带尾随 fallback，如 <c>url(#g) red</c>）。
        /// </summary>
        private static void ApplyPaint(string value, out string? gradientId, out string? fallbackColor)
        {
            gradientId = null;
            fallbackColor = null;

            var v = value.Trim();
            if (!v.StartsWith("url(", StringComparison.OrdinalIgnoreCase)) return;

            var close = v.IndexOf(')');
            if (close < 0) return;

            var inner = v[4..close].Trim().Trim('\'', '"');
            if (inner.StartsWith('#')) gradientId = inner[1..].Trim();

            var rest = v[(close + 1)..].Trim();
            fallbackColor = rest.Length > 0 ? NormalizeColor(rest) : null;
        }
    }

    /// <summary>归一化颜色为 #RRGGBB 或 "none"。无法解析返回 null（视为未设置）。</summary>
    public static string? NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();

        if (v.Equals("none", StringComparison.OrdinalIgnoreCase)) return "none";
        if (v.Equals("currentColor", StringComparison.OrdinalIgnoreCase)) return null;

        if (NamedColors.TryGetValue(v, out var named))
            return named;

        if (v.StartsWith('#'))
        {
            var hex = v[1..].Trim();
            if (hex.Length == 3)
            {
                hex = string.Concat(hex.Select(c => $"{c}{c}"));
            }
            if (hex.Length == 6 && hex.All(Uri.IsHexDigit))
                return "#" + hex.ToLowerInvariant();
            return null;
        }

        // rgb(r,g,b) 简写
        if (v.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && v.EndsWith(')'))
        {
            var parts = v[4..^1].Split(',');
            if (parts.Length == 3 &&
                int.TryParse(parts[0].Trim(), out var r) &&
                int.TryParse(parts[1].Trim(), out var g) &&
                int.TryParse(parts[2].Trim(), out var b))
            {
                return $"#{r:X2}{g:X2}{b:X2}".ToLowerInvariant();
            }
        }

        return null;
    }

    // ── 工具 ──

    internal static string? GetAttr(XElement el, string name)
    {
        var attr = el.Attributes().FirstOrDefault(a =>
            string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
        return attr?.Value;
    }

    private static double? TryParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().TrimEnd('p', 'x', 't', '%');
        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }

    internal static List<double> ParseNumberList(string text)
    {
        var result = new List<double>();
        var sb = new System.Text.StringBuilder();
        bool hasNumber = false;

        void Flush()
        {
            if (!hasNumber) return;
            var token = sb.ToString().Trim().TrimEnd(',');
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                result.Add(d);
            sb.Clear();
            hasNumber = false;
        }

        foreach (var c in text)
        {
            if (char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
            {
                sb.Append(c);
                hasNumber = true;
            }
            else
            {
                Flush();
            }
        }
        Flush();
        return result;
    }
}
