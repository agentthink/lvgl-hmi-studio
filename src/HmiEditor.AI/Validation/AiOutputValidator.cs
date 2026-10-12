using System.Text.RegularExpressions;
using HmiEditor.Core.Models;

namespace HmiEditor.AI.Validation;

/// <summary>
/// AI 输出验证结果级别。
/// </summary>
public enum AiValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// 单条验证结果。
///
/// <para><b>三个字段各管什么</b>（与 Core 的 <c>ScriptError</c> / 插件的 <c>ValidationIssue</c>
/// 同一套分法）：</para>
/// <list type="bullet">
///   <item><see cref="Code"/> —— **哪一类问题**，取值只许来自 <see cref="AiIssueCodes"/>。
///   界面靠它推导字典键（<c>ai.issue.&lt;码小写&gt;</c>）取本地化模板；</item>
///   <item><see cref="Message"/> —— **中文回落原文**，字典缺键时兜底显示
///   （宁可显示中文也不把键名甩给用户）。它由 <see cref="Of"/> 从回落模板 + 实参渲染出来，
///   所以永远与实参自洽；</item>
///   <item><see cref="Args"/> —— 模板占位符的实参。为 <c>null</c> 时模板不含占位符。
///   没有它，界面只能显示一句不含控件名/具体数值的笼统提示（信息量下降）。</item>
/// </list>
///
/// <para><b>为什么 <see cref="Code"/> 不能省</b>：省掉它就只剩一句中文，
/// 而中文既做不了键（改一个字的措辞就换了一个键）、又无法被守卫枚举 ⇒
/// "新加一条用户可见文案"不会有任何东西提醒你补字典。有了码，
/// <c>AiIssueLocalizationGuardTests</c> 才能把「声明了哪些码」与「真正产出了哪些码」
/// 做双向差集。</para>
/// </summary>
public sealed record AiValidationIssue(
    AiValidationSeverity Severity,
    string Code,
    string Message,
    IReadOnlyList<object>? Args = null)
{
    /// <summary>
    /// 按 <b>回落模板 + 实参</b>构造 —— 唯一的构造入口。
    ///
    /// <para>刻意不让调用点直接写渲染好的中文：那样 <see cref="Message"/> 与 <see cref="Args"/>
    /// 会各写一遍，而它们是同一句话的两个版本，必然有一处先过期。这里由模板渲染，
    /// 于是本地化守卫那条「zh 字典模板渲染后必须逐字等于回落原文」的判据
    /// 比的就是**同源的**两份文本。</para>
    ///
    /// <para>实参**不要预格式化**（传 <c>c.X</c> 而不是 <c>c.X.ToString()</c>，
    /// 除非回落模板里写了 <c>F0</c> 这类格式说明符）—— <c>string.Format</c> 用的默认
    /// culture 与字典侧渲染完全相同，两侧才可比。</para>
    /// </summary>
    internal static AiValidationIssue Of(
        AiValidationSeverity severity,
        string code,
        string fallbackTemplate,
        params object[] args)
        => new(severity,
               code,
               args.Length == 0 ? fallbackTemplate : string.Format(fallbackTemplate, args),
               args.Length == 0 ? null : args);
}

/// <summary>
/// 验证汇总。
/// </summary>
public sealed class AiValidationResult
{
    public List<AiValidationIssue> Issues { get; } = new();

    /// <summary>是否有阻断性问题 (Error 级别)。</summary>
    public bool HasErrors => Issues.Any(i => i.Severity == AiValidationSeverity.Error);

    /// <summary>是否有警告。</summary>
    public bool HasWarnings => Issues.Any(i => i.Severity == AiValidationSeverity.Warning);
}

/// <summary>
/// AI 输出验证器 —— LLM 安全关键防线。
/// 对 LLM 生成的屏幕做确定性校验(类型白名单/坐标边界/颜色格式/变量存在性),
/// 防止不可信模型输出污染项目模型。Error 级问题阻断应用,Warning 级提示用户。
/// </summary>
public static class AiOutputValidator
{
    private static readonly Regex ColorPattern = new(
        @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$",
        RegexOptions.Compiled);

    /// <summary>校验生成的屏幕。</summary>
    public static AiValidationResult ValidateScreen(HmiScreen screen, HmiProject? project)
    {
        var result = new AiValidationResult();
        if (screen is null)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.SCREEN_EMPTY, "生成的屏幕为空"));
            return result;
        }

        ValidateDimensions(screen, result);
        ValidateBackgroundColor(screen, result);
        ValidateControls(screen, result, project);
        return result;
    }

    /// <summary>校验生成的 Lua 脚本。</summary>
    public static AiValidationResult ValidateScript(HmiEditor.Core.Models.Scripting.HmiScript script)
    {
        var result = new AiValidationResult();
        if (script is null)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.SCRIPT_EMPTY, "生成的脚本为空"));
            return result;
        }

        if (string.IsNullOrWhiteSpace(script.Name))
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.SCRIPT_NAME_EMPTY, "脚本名称为空"));
        }

        if (string.IsNullOrWhiteSpace(script.Code))
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.SCRIPT_CODE_EMPTY, "脚本代码为空"));
            return result;
        }

        if (script.Code.Length > 8192)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Warning, AiIssueCodes.SCRIPT_TOO_LONG, "脚本代码过长(>8KB), 建议拆分"));
        }

        // 函数定义检查
        if (!script.Code.Contains("function ", StringComparison.Ordinal))
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.SCRIPT_NO_FUNCTION, "脚本缺少 function 函数定义"));
        }

        // 禁止的敏感 API 检查 (LLM 安全: 生成代码不得越出 HMI API 边界)
        var forbidden = new[] { "os.execute", "io.open", "io.write", "io.read", "os.remove", "require" };
        foreach (var f in forbidden)
        {
            if (script.Code.Contains(f, StringComparison.OrdinalIgnoreCase))
            {
                result.Issues.Add(AiValidationIssue.Of(
                    AiValidationSeverity.Error, AiIssueCodes.SCRIPT_FORBIDDEN_API,
                    "脚本包含被禁止的敏感调用: '{0}'", f));
            }
        }

        // 分类检查
        var knownCategories = new[] { "OnClick", "OnValueChanged", "Timer", "Alarm" };
        if (!knownCategories.Contains(script.Category, StringComparer.OrdinalIgnoreCase))
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Warning, AiIssueCodes.SCRIPT_UNKNOWN_CATEGORY,
                "脚本分类 '{0}' 未知, 已回退 OnClick", script.Category));
        }

        return result;
    }

    /// <summary>校验生成的 Faceplate 模板。</summary>
    public static AiValidationResult ValidateTemplate(ControlTemplate template)
    {
        var result = new AiValidationResult();
        if (template is null)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.TEMPLATE_EMPTY, "生成的模板为空"));
            return result;
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.TEMPLATE_NAME_EMPTY, "模板名称为空"));
        }

        if (template.Controls.Count == 0)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.TEMPLATE_NO_CONTROLS, "模板不包含任何控件"));
        }
        else if (template.Controls.Count > 15)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Warning, AiIssueCodes.TEMPLATE_TOO_MANY_CONTROLS,
                "模板控件过多 ({0} 个), 建议不超过 15 个", template.Controls.Count));
        }

        var validTypes = Enum.GetNames<HmiControlType>();
        foreach (var c in template.Controls)
        {
            if (!validTypes.Contains(c.Type.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                result.Issues.Add(AiValidationIssue.Of(
                    AiValidationSeverity.Error, AiIssueCodes.CONTROL_TYPE_INVALID,
                    "控件 '{0}' 类型非法: {1}", c.Name, c.Type));
            }

            if (c.Width < 1 || c.Height < 1)
            {
                result.Issues.Add(AiValidationIssue.Of(
                    AiValidationSeverity.Error, AiIssueCodes.CONTROL_SIZE_INVALID,
                    "控件 '{0}' 尺寸非法: {1}x{2}", c.Name, c.Width, c.Height));
            }

            if (!string.IsNullOrEmpty(c.BgColor) && !IsValidColor(c.BgColor))
            {
                result.Issues.Add(AiValidationIssue.Of(
                    AiValidationSeverity.Error, AiIssueCodes.CONTROL_BGCOLOR_INVALID,
                    "控件 '{0}' 背景色格式非法: '{1}'", c.Name, c.BgColor));
            }
        }

        // 参数校验: 名称唯一性
        var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in template.Parameters)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
            {
                result.Issues.Add(AiValidationIssue.Of(
                    AiValidationSeverity.Error, AiIssueCodes.PARAM_NAME_EMPTY, "存在参数名为空的参数定义"));
                continue;
            }
            if (!paramNames.Add(p.Name))
            {
                result.Issues.Add(AiValidationIssue.Of(
                    AiValidationSeverity.Error, AiIssueCodes.PARAM_NAME_DUPLICATE,
                    "参数名重复: '{0}'", p.Name));
            }
        }

        // 控件引用的参数是否已声明 (仅警告, 允许用户在模板编辑器补充)
        foreach (var c in template.Controls)
        {
            var referenced = ReferencedParameters(c);
            foreach (var param in referenced)
            {
                if (!paramNames.Contains(param))
                {
                    result.Issues.Add(AiValidationIssue.Of(
                        AiValidationSeverity.Warning, AiIssueCodes.PARAM_UNDECLARED,
                        "控件 '{0}' 引用了未声明的参数 '{{Parameters.{1}}}'", c.Name, param));
                }
            }
        }

        return result;
    }

    /// <summary>提取控件属性中引用的参数名列表。</summary>
    private static IEnumerable<string> ReferencedParameters(HmiControl c)
    {
        var values = new[] { c.Text, c.BgColor, c.TextColor, c.VariableBindingId };
        foreach (var v in values)
        {
            if (string.IsNullOrEmpty(v)) continue;
            foreach (System.Text.RegularExpressions.Match m in PlaceholderRegex.Matches(v))
            {
                yield return m.Groups[1].Value;
            }
        }
    }

    private static readonly System.Text.RegularExpressions.Regex PlaceholderRegex = new(
        @"\{\{Parameters\.(\w+)\}\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static void ValidateDimensions(HmiScreen screen, AiValidationResult result)
    {
        if (screen.Width < 1 || screen.Height < 1)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.SCREEN_SIZE_INVALID,
                "屏幕尺寸非法: {0}x{1}", screen.Width, screen.Height));
        }
    }

    private static void ValidateBackgroundColor(HmiScreen screen, AiValidationResult result)
    {
        if (!string.IsNullOrEmpty(screen.BgColor) && !IsValidColor(screen.BgColor))
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.SCREEN_BGCOLOR_INVALID,
                "屏幕背景色格式非法: '{0}' (应为 #RRGGBB)", screen.BgColor));
        }
    }

    private static void ValidateControls(HmiScreen screen, AiValidationResult result, HmiProject? project)
    {
        if (screen.Controls.Count == 0)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Warning, AiIssueCodes.SCREEN_NO_CONTROLS, "生成的屏幕不包含任何控件"));
            return;
        }

        if (screen.Controls.Count > 20)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Warning, AiIssueCodes.SCREEN_TOO_MANY_CONTROLS,
                "控件数量过多 ({0} 个),建议不超过 20 个", screen.Controls.Count));
        }

        var validTypes = Enum.GetNames<HmiControlType>();
        foreach (var c in screen.Controls)
        {
            ValidateControlBounds(c, screen, result);

            // 类型白名单
            if (!validTypes.Contains(c.Type.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                result.Issues.Add(AiValidationIssue.Of(
                    AiValidationSeverity.Error, AiIssueCodes.CONTROL_TYPE_INVALID,
                    "控件 '{0}' 类型非法: {1}", c.Name, c.Type));
            }

            // 颜色格式
            if (!string.IsNullOrEmpty(c.BgColor) && !IsValidColor(c.BgColor))
            {
                result.Issues.Add(AiValidationIssue.Of(
                    AiValidationSeverity.Error, AiIssueCodes.CONTROL_BGCOLOR_INVALID,
                    "控件 '{0}' 背景色格式非法: '{1}'", c.Name, c.BgColor));
            }
            if (!string.IsNullOrEmpty(c.TextColor) && !IsValidColor(c.TextColor))
            {
                result.Issues.Add(AiValidationIssue.Of(
                    AiValidationSeverity.Error, AiIssueCodes.CONTROL_TEXTCOLOR_INVALID,
                    "控件 '{0}' 文字色格式非法: '{1}'", c.Name, c.TextColor));
            }

            // 变量存在性 (仅警告 —— 用户可在后续手动绑定)
            var binding = c.PrimaryBindingVariable;
            if (!string.IsNullOrEmpty(binding) && project?.VariableManager is not null)
            {
                var exists = project.VariableManager.Variables.Any(v =>
                    string.Equals(v.Id, binding, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    result.Issues.Add(AiValidationIssue.Of(
                        AiValidationSeverity.Warning, AiIssueCodes.CONTROL_VARIABLE_MISSING,
                        "控件 '{0}' 绑定的变量 '{1}' 在工程中不存在", c.Name, binding));
                }
            }
        }

        // 控件重叠检查 (仅警告, 严重重叠才提示)
        ValidateOverlaps(screen, result);
    }

    private static void ValidateControlBounds(HmiControl c, HmiScreen screen, AiValidationResult result)
    {
        if (c.Width < 1 || c.Height < 1)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.CONTROL_SIZE_INVALID,
                "控件 '{0}' 尺寸非法: {1}x{2}", c.Name, c.Width, c.Height));
        }
        if (c.X < 0 || c.Y < 0)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Error, AiIssueCodes.CONTROL_POSITION_INVALID,
                "控件 '{0}' 坐标非法: ({1}, {2})", c.Name, c.X, c.Y));
        }
        if (c.X + c.Width > screen.Width || c.Y + c.Height > screen.Height)
        {
            result.Issues.Add(AiValidationIssue.Of(
                AiValidationSeverity.Warning, AiIssueCodes.CONTROL_OUT_OF_BOUNDS,
                "控件 '{0}' 超出屏幕边界", c.Name));
        }
    }

    private static void ValidateOverlaps(HmiScreen screen, AiValidationResult result)
    {
        var controls = screen.Controls.ToList();
        for (var i = 0; i < controls.Count; i++)
        {
            for (var j = i + 1; j < controls.Count; j++)
            {
                var a = controls[i];
                var b = controls[j];

                var overlapW = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
                var overlapH = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
                if (overlapW <= 0 || overlapH <= 0) continue;

                // 严重重叠: 重叠面积超过较小控件 50%
                var overlapArea = overlapW * overlapH;
                var smallerArea = Math.Min(a.Width * a.Height, b.Width * b.Height);
                if (smallerArea > 0 && overlapArea > smallerArea * 0.5)
                {
                    result.Issues.Add(AiValidationIssue.Of(
                        AiValidationSeverity.Warning, AiIssueCodes.CONTROL_OVERLAP,
                        "控件 '{0}' 与 '{1}' 严重重叠", a.Name, b.Name));
                }
            }
        }
    }

    /// <summary>校验颜色 HEX 格式: #RGB / #RRGGBB / #RRGGBBAA。</summary>
    internal static bool IsValidColor(string? color)
    {
        return !string.IsNullOrEmpty(color) && ColorPattern.IsMatch(color);
    }
}
