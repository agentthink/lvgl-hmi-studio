namespace HmiEditor.AI.Models;

/// <summary>布局问题严重级别。</summary>
public enum LayoutIssueSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>单个布局问题。</summary>
public sealed class LayoutIssue
{
    public LayoutIssueSeverity Severity { get; init; }

    /// <summary>
    /// **哪一类问题** —— 取值只许来自 <see cref="AiIssueCodes"/>。
    ///
    /// <para>界面靠它推导字典键（<c>ai.issue.&lt;码小写&gt;</c>）取本地化模板；
    /// 没有它，界面就只能显示下面那句写死的中文（英文界面下中英混排）。</para>
    /// </summary>
    public string Code { get; init; } = "";

    /// <summary>**中文回落原文** —— 字典缺键时兜底显示（由 <c>MakeIssue</c> 从模板 + 实参渲染）。</summary>
    public string Message { get; init; } = "";

    /// <summary>模板占位符实参；<c>null</c> 时模板不含占位符。</summary>
    public IReadOnlyList<object>? Args { get; init; }

    /// <summary>是否可自动修复（对齐、边距可修；对比度等需人工决策）。</summary>
    public bool AutoFix { get; init; }

    /// <summary>涉及的控件（用于一键修复定位）。</summary>
    public IReadOnlyList<HmiEditor.Core.Models.HmiControl>? Controls { get; init; }

    /// <summary>修复建议参数：对齐模式 / 间距调整等（由 AutoFix 消费者解析）。</summary>
    public string FixHint { get; init; } = "";
}

/// <summary>布局分析结果。</summary>
public sealed class LayoutAnalysisResult
{
    /// <summary>总体评分 0-100。</summary>
    public int Score { get; set; }

    public IReadOnlyList<LayoutIssue> Issues { get; init; } = Array.Empty<LayoutIssue>();

    /// <summary>是否有 Error 级问题。</summary>
    public bool HasErrors => Issues.Any(i => i.Severity == LayoutIssueSeverity.Error);

    /// <summary>是否有 Warning 级问题。</summary>
    public bool HasWarnings => Issues.Any(i => i.Severity == LayoutIssueSeverity.Warning);

    /// <summary>可自动修复的问题数。</summary>
    public int AutoFixCount => Issues.Count(i => i.AutoFix);
}
