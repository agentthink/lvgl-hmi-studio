using HmiEditor.AI.Models;
using HmiEditor.Core.Models;

namespace HmiEditor.AI.Services;

/// <summary>
/// 布局分析器 —— 基于规则的屏幕布局质量评估(不依赖 LLM,确定性可测试)。
///
/// 分析维度:
/// 1. 对齐检测: 控件水平/垂直未对齐(偏离共享对齐线 3px 以上)
/// 2. 间距均匀性: 相邻控件间距与组内平均间距偏差过大
/// 3. 安全边距: 控件离屏幕边缘过近(< 8px)
/// 4. 视觉层次: 是否有控件占据屏幕主导地位(面积占比)
/// 5. 颜色对比度: 文字与背景对比度是否达到 WCAG AA (4.5:1)
///
/// 输出: 0-100 评分 + 问题列表(标注可自动修复项)。
///
/// <para><b>每条问题都带 <see cref="AiIssueCodes"/> 里的码</b>：这些文案会显示在属性面板的
/// 「AI 建议」列表里（<c>PropertyPanelView.ShowAiSuggestions</c>），所以必须能按界面语言取字典。
/// 渲染统一走 <c>ExternalText.Resolve</c>，本类只负责"给出码 + 实参 + 中文回落"。</para>
///
/// <para><b>数值为什么先格式化再当实参传</b>：原写法是插值里的格式说明符（<c>{c.X:F0}</c>），
/// 而字典模板只能写 <c>{1}</c> —— 格式说明符留在调用点、值以字符串形态进实参，
/// 两侧渲染才逐字相同（<c>AiIssueLocalizationGuardTests</c> 会比这两份文本）。
/// 所以 <see cref="Fmt"/> / <see cref="Fmt1"/> 与被替换掉的说明符必须完全等价。</para>
/// </summary>
public static class LayoutAnalyzer
{
    /// <summary>对齐容差(px) —— 偏差小于该值视为已对齐。</summary>
    public const double AlignmentTolerance = 3.0;

    /// <summary>安全边距(px)。</summary>
    public const double SafeMargin = 8.0;

    /// <summary>最小可对比文本控件尺寸(避免对装饰性小控件误报)。</summary>
    private const double MinTextControlArea = 400;

    /// <summary>WCAG AA 正文对比度阈值。</summary>
    private const double ContrastThreshold = 4.5;

    /// <summary>
    /// 分析屏幕布局。
    /// </summary>
    public static LayoutAnalysisResult Analyze(HmiScreen screen)
    {
        if (screen is null || screen.Controls.Count == 0)
        {
            return new LayoutAnalysisResult
            {
                Score = 100,
                Issues = new List<LayoutIssue>
                {
                    MakeIssue(
                        LayoutIssueSeverity.Info,
                        AiIssueCodes.LAYOUT_SCREEN_TOO_SMALL,
                        "屏幕为空或控件过少, 布局评分不适用。",
                        Array.Empty<object>(),
                        autoFix: false,
                        control: null)
                }
            };
        }

        var issues = new List<LayoutIssue>();
        var controls = screen.Controls.ToList();

        CheckSafeMargins(screen, controls, issues);
        CheckAlignment(controls, issues);
        CheckSpacingUniformity(controls, issues);
        CheckVisualHierarchy(screen, controls, issues);
        CheckColorContrast(controls, issues);

        var score = ComputeScore(screen, issues);
        return new LayoutAnalysisResult { Score = score, Issues = issues };
    }

    // ── 1. 安全边距 ──

    private static void CheckSafeMargins(HmiScreen screen, List<HmiControl> controls, List<LayoutIssue> issues)
    {
        foreach (var c in controls)
        {
            if (c.X < SafeMargin)
            {
                issues.Add(MakeIssue(LayoutIssueSeverity.Warning, AiIssueCodes.LAYOUT_MARGIN_LEFT,
                    "控件「{0}」距屏幕左边缘 {1}px, 低于安全边距 {2}px",
                    new object[] { c.Name, Fmt(c.X), Fmt(SafeMargin) },
                    autoFix: true, c, $"left:{SafeMargin}"));
            }
            if (c.Y < SafeMargin)
            {
                issues.Add(MakeIssue(LayoutIssueSeverity.Warning, AiIssueCodes.LAYOUT_MARGIN_TOP,
                    "控件「{0}」距屏幕上边缘 {1}px, 低于安全边距 {2}px",
                    new object[] { c.Name, Fmt(c.Y), Fmt(SafeMargin) },
                    autoFix: true, c, $"top:{SafeMargin}"));
            }
            if (c.X + c.Width > screen.Width - SafeMargin)
            {
                issues.Add(MakeIssue(LayoutIssueSeverity.Warning, AiIssueCodes.LAYOUT_MARGIN_RIGHT,
                    "控件「{0}」距屏幕右边缘 {1}px, 低于安全边距 {2}px",
                    new object[] { c.Name, Fmt(screen.Width - c.X - c.Width), Fmt(SafeMargin) },
                    autoFix: true, c, $"right:{screen.Width - SafeMargin}"));
            }
            if (c.Y + c.Height > screen.Height - SafeMargin)
            {
                issues.Add(MakeIssue(LayoutIssueSeverity.Warning, AiIssueCodes.LAYOUT_MARGIN_BOTTOM,
                    "控件「{0}」距屏幕下边缘 {1}px, 低于安全边距 {2}px",
                    new object[] { c.Name, Fmt(screen.Height - c.Y - c.Height), Fmt(SafeMargin) },
                    autoFix: true, c, $"bottom:{screen.Height - SafeMargin}"));
            }
        }
    }

    // ── 2. 对齐检测 ──

    private static void CheckAlignment(List<HmiControl> controls, List<LayoutIssue> issues)
    {
        // 水平对齐: 相同 Y (顶对齐) 或相同 Y+Height (底对齐) 或相同中心
        // 垂直对齐: 相同 X (左对齐) 或相同 X+Width (右对齐)
        CheckSharedAlignment(controls, issues,
            keySelector: c => c.Y, code: AiIssueCodes.LAYOUT_ALIGN_TOP, label: "顶部", isHorizontal: true);
        CheckSharedAlignment(controls, issues,
            keySelector: c => c.Y + c.Height, code: AiIssueCodes.LAYOUT_ALIGN_BOTTOM, label: "底部", isHorizontal: true);
        CheckSharedAlignment(controls, issues,
            keySelector: c => c.X, code: AiIssueCodes.LAYOUT_ALIGN_LEFT, label: "左侧", isHorizontal: false);
        CheckSharedAlignment(controls, issues,
            keySelector: c => c.X + c.Width, code: AiIssueCodes.LAYOUT_ALIGN_RIGHT, label: "右侧", isHorizontal: false);
    }

    /// <summary>检测共享对齐线上的控件, 若有偏离该线的成员则报告。</summary>
    private static void CheckSharedAlignment(
        List<HmiControl> controls,
        List<LayoutIssue> issues,
        Func<HmiControl, double> keySelector,
        string code,
        string label,
        bool isHorizontal)
    {
        var groups = controls
            .GroupBy(keySelector)
            .Where(g => g.Count() >= 2) // 至少 2 个控件共享一条对齐线
            .ToList();

        foreach (var group in groups)
        {
            var line = group.Key;
            // 在共享线上, 找"本该对齐却偏离"的其他控件
            var neighbors = controls
                .Where(c => !group.Contains(c))
                .Where(c =>
                {
                    var v = keySelector(c);
                    // 相邻范围: 与对齐线距离在 1~12px 内(太远不算"意图对齐")
                    var dist = Math.Abs(v - line);
                    return dist > AlignmentTolerance && dist <= 12;
                })
                .Take(2)
                .ToList();

            foreach (var off in neighbors)
            {
                var delta = Math.Abs(keySelector(off) - line);
                issues.Add(MakeIssue(LayoutIssueSeverity.Warning, code,
                    // 方向词（顶部/底部/左侧/右侧）是**同一条模板的参数化**，所以模板在这里拼；
                    // 4 个方向各自的字典模板里写的是已经展开好的那一份（守卫会比逐字一致性）。
                    $"控件「{{0}}」与「{{1}}」{label}未对齐, 偏差 {{2}}px",
                    new object[] { off.Name, group.First().Name, Fmt(delta) },
                    autoFix: true, off,
                    isHorizontal ? $"alignTop:{line}" : $"alignLeft:{line}"));
            }
        }
    }

    // ── 3. 间距均匀性 ──

    private static void CheckSpacingUniformity(List<HmiControl> controls, List<LayoutIssue> issues)
    {
        // 水平相邻: 同 Y 且 X 接近 (同一行)
        var rows = controls
            .GroupBy(c => Math.Round(c.Y / 10))
            .Where(g => g.Count() >= 3) // 至少 3 个才值得检查
            .ToList();

        foreach (var row in rows)
        {
            var sorted = row.OrderBy(c => c.X).ToList();
            var gaps = new List<double>();
            for (var i = 1; i < sorted.Count; i++)
            {
                gaps.Add(sorted[i].X - (sorted[i - 1].X + sorted[i - 1].Width));
            }
            if (gaps.Count < 2) continue;

            var avg = gaps.Average();
            if (avg <= 0) continue; // 重叠控件, 不做间距判断

            for (var i = 0; i < gaps.Count; i++)
            {
                var deviation = Math.Abs(gaps[i] - avg);
                if (avg > 4 && deviation > avg * 0.5 && deviation >= 6)
                {
                    var a = sorted[i];
                    var b = sorted[i + 1];
                    issues.Add(MakeIssue(LayoutIssueSeverity.Info, AiIssueCodes.LAYOUT_SPACING_UNEVEN,
                        "控件「{0}」与「{1}」间距 {2}px, 与同行平均间距 {3}px 偏差较大",
                        new object[] { a.Name, b.Name, Fmt(gaps[i]), Fmt(avg) },
                        autoFix: true, b, $"gap:{avg:F1}"));
                }
            }
        }
    }

    // ── 4. 视觉层次 ──

    private static void CheckVisualHierarchy(HmiScreen screen, List<HmiControl> controls, List<LayoutIssue> issues)
    {
        var screenArea = screen.Width * screen.Height;
        if (screenArea <= 0) return;

        var largest = controls.OrderByDescending(c => c.Width * c.Height).FirstOrDefault();
        if (largest is null) return;

        var largestArea = largest.Width * largest.Height;
        var ratio = largestArea / screenArea;

        // 屏幕面积 < 2000px²(如空屏+1小控件) 不适用层次检查
        if (screenArea < 4000) return;

        if (ratio < 0.08)
        {
            issues.Add(MakeIssue(LayoutIssueSeverity.Info, AiIssueCodes.LAYOUT_NO_HIERARCHY,
                "最大控件「{0}」仅占屏幕 {1}%, 建议增强主视觉层次",
                new object[] { largest.Name, Fmt(ratio * 100) },
                autoFix: false, largest));
        }
    }

    // ── 5. 颜色对比度 (WCAG AA) ──

    private static void CheckColorContrast(List<HmiControl> controls, List<LayoutIssue> issues)
    {
        foreach (var c in controls)
        {
            // 仅检查有文字且面积足够的控件
            var hasText = !string.IsNullOrEmpty(c.Text) && c.Text.Length > 1;
            if (!hasText || c.Width * c.Height < MinTextControlArea) continue;

            var fg = ParseHexColor(c.TextColor);
            var bg = ParseHexColor(c.BgColor);
            if (fg is null || bg is null) continue;

            var ratio = ContrastRatio(fg.Value, bg.Value);
            if (ratio < ContrastThreshold)
            {
                issues.Add(MakeIssue(LayoutIssueSeverity.Warning, AiIssueCodes.LAYOUT_LOW_CONTRAST,
                    "控件「{0}」文字/背景对比度 {1}:1, 低于 WCAG AA 标准 ({2}:1)",
                    new object[] { c.Name, Fmt1(ratio), Fmt1(ContrastThreshold) },
                    autoFix: false, c));
            }
        }
    }

    // ── 评分 ──

    private static int ComputeScore(HmiScreen screen, List<LayoutIssue> issues)
    {
        var penalty = 0;
        foreach (var issue in issues)
        {
            penalty += issue.Severity switch
            {
                LayoutIssueSeverity.Error => 15,
                LayoutIssueSeverity.Warning => 6,
                _ => 2
            };
        }
        return Math.Clamp(100 - penalty, 0, 100);
    }

    // ── 工具 ──

    /// <summary>
    /// 造一条问题。**<see cref="LayoutIssue.Message"/> 由回落模板 + 实参渲染**，
    /// 所以它与 <see cref="LayoutIssue.Args"/> 永远自洽（两者各写一遍必然会有一处先过期）。
    /// </summary>
    private static LayoutIssue MakeIssue(
        LayoutIssueSeverity severity,
        string code,
        string fallbackTemplate,
        IReadOnlyList<object> args,
        bool autoFix,
        HmiControl? control,
        string fixHint = "")
    {
        return new LayoutIssue
        {
            Severity = severity,
            Code = code,
            Message = args.Count == 0 ? fallbackTemplate : string.Format(fallbackTemplate, args.ToArray()),
            Args = args.Count == 0 ? null : args,
            AutoFix = autoFix,
            Controls = control is null ? null : new[] { control },
            FixHint = fixHint
        };
    }

    /// <summary>与插值里的 <c>{v:F0}</c> 等价（含当前 culture）。</summary>
    private static string Fmt(double v) => v.ToString("F0", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>与插值里的 <c>{v:F1}</c> 等价（含当前 culture）。</summary>
    private static string Fmt1(double v) => v.ToString("F1", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>解析 #RRGGBB 十六进制颜色; 非法返回 null。</summary>
    internal static (double R, double G, double B)? ParseHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var h = hex.Trim().TrimStart('#');
        if (h.Length == 3)
        {
            h = string.Concat(h.Select(ch => $"{ch}{ch}"));
        }
        if (h.Length != 6) return null;
        if (!int.TryParse(h[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) ||
            !int.TryParse(h[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) ||
            !int.TryParse(h[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return null;
        }
        return (r / 255.0, g / 255.0, b / 255.0);
    }

    /// <summary>WCAG 相对亮度计算。</summary>
    internal static double RelativeLuminance((double R, double G, double B) c)
    {
        double Linearize(double channel)
        {
            return channel <= 0.03928
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
        var r = Linearize(c.R);
        var g = Linearize(c.G);
        var b = Linearize(c.B);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>WCAG 对比度 (1:1 ~ 21:1)。</summary>
    internal static double ContrastRatio((double R, double G, double B) fg, (double R, double G, double B) bg)
    {
        var l1 = RelativeLuminance(fg);
        var l2 = RelativeLuminance(bg);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }
}
