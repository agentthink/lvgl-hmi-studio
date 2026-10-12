namespace HmiEditor.Core.Services;

/// <summary>
/// 一条表达式**语法**失败 —— 「**码 + 中文回落原文**（+ 可选实参）」三段式
/// （阶段 91，登记 <b>#61i</b>），与 <see cref="SvgParseError"/> / <c>CsvImportIssue</c> /
/// <c>AiValidationIssue</c> / <c>ScriptError</c> 同形。
///
/// <para><b>为什么是<b>记录</b>而不是异常</b>：<see cref="ExpressionEngine.TryCompile"/> 的契约是
/// "返回成功与否 + 失败记录"（<c>out</c>），它服务的是校验器与属性面板 —— 那里
/// **不需要异常**。需要异常的那条路（<see cref="ExpressionEngine.Compile"/> 的返回契约是 AST）
/// 由 <see cref="ExpressionSyntaxException"/> 把本对象**包起来**，
/// 于是「码」只有一个来源、两种搬运方式。</para>
///
/// <para><b><see cref="Message"/> 是回落、不是文案</b>：字典缺键时原样显示它
/// （宁可显示中文，也不能把码名显示给用户）。它同时被**产物注释**那条路用 ——
/// <c>CodeGenerator.Screen.cs</c> 写进 <c>generated_ui.c</c> 的
/// <c>// WARNING: Invalid expression for '…': {回落} (skipped)</c> 里，
/// 那里放的是读 C 代码的人要看的信息，**不随界面语言变**（与
/// <c>GeneratedCodeIssueCodes.GENERATOR_WARNING_COMMENT</c> 同一类"判过的不做"）。</para>
/// </summary>
/// <param name="Code">诊断码，取自 <see cref="ExpressionSyntaxIssueCodes"/> 的常量值。</param>
/// <param name="Message">**产出方自带的中文回落原文**。</param>
/// <param name="Args">模板占位符实参；<c>null</c> = 模板不含占位符。</param>
public sealed record ExpressionSyntaxError(string Code, string Message, IReadOnlyList<object>? Args = null)
{
    /// <summary>三元运算缺 <c>:</c>。</summary>
    public static readonly ExpressionSyntaxError TernaryMissingColon =
        new(ExpressionSyntaxIssueCodes.TERNARY_MISSING_COLON, "三元运算缺少 ':'");

    /// <summary>括号没有闭合。</summary>
    public static readonly ExpressionSyntaxError MissingRParen =
        new(ExpressionSyntaxIssueCodes.MISSING_RPAREN, "缺少右括号 ')'");

    /// <summary>
    /// 表达式为空 / 纯空白。
    ///
    /// <para>⚠ 回落原文**刻意不是** .NET 的
    /// <c>Value cannot be null. (Parameter 'expression')</c>：那句话对用户没有任何额外信息
    /// （空就是空），而它是"产出方自带、字典缺键时可显示"的那一份 ⇒ 中文回落更可用。
    /// 与 <see cref="Generic"/> 的区别正在这里 —— 那一档的英文原文**含排障信息**，必须留着。</para>
    /// </summary>
    public static readonly ExpressionSyntaxError Empty =
        new(ExpressionSyntaxIssueCodes.EMPTY_EXPRESSION, "表达式为空");

    /// <summary>解析器在该位置不认识这个符号。</summary>
    /// <param name="symbol">符号的原文（可能为空串 = 走到了输入末尾）。</param>
    /// <param name="pos">该符号在表达式里的位置（0 起）。</param>
    public static ExpressionSyntaxError UnexpectedSymbol(string symbol, int pos)
        => new(ExpressionSyntaxIssueCodes.UNEXPECTED_SYMBOL,
            $"意外的符号: {symbol} (位置 {pos})",
            new object[] { symbol, pos });

    /// <summary>
    /// 表达式在输入结束处就没写完。
    ///
    /// <para>与 <see cref="UnexpectedSymbol"/> 分家的理由见
    /// <see cref="ExpressionSyntaxIssueCodes.UNEXPECTED_END"/>（未知字符也被标成 EOF，
    /// 真正的判据是空 Value）。</para>
    /// </summary>
    /// <param name="pos">输入结束的位置（0 起，即表达式长度）。</param>
    public static ExpressionSyntaxError UnexpectedEnd(int pos)
        => new(ExpressionSyntaxIssueCodes.UNEXPECTED_END,
            $"表达式意外结束 (位置 {pos})",
            new object[] { pos });

    /// <summary>数字字面量形状不对（<c>1.2.3</c>）。</summary>
    /// <param name="token">该记号的原文。</param>
    public static ExpressionSyntaxError MalformedNumber(string token)
        => new(ExpressionSyntaxIssueCodes.MALFORMED_NUMBER,
            $"数字格式不正确: {token}",
            new object[] { token });

    /// <summary>
    /// **兜底**：六档之外的解析器异常。
    ///
    /// <para>回落原文 / 实参都是 <paramref name="rawMessage"/> —— 它可能是 .NET 的**英文**原文，
    /// 但那一档的用户可见框由字典模板给出（<c>expr.syntax.generic</c>：
    /// <c>解析表达式失败：{0}</c>），**原文当实参**保留下来 ⇒ 信息不丢，
    /// 与 <c>ExternalText.FailureReason</c> 第 ④ 支同一条取舍。</para>
    /// </summary>
    /// <param name="rawMessage">原始异常消息。</param>
    public static ExpressionSyntaxError Generic(string rawMessage)
        => new(ExpressionSyntaxIssueCodes.GENERIC, rawMessage, new object[] { rawMessage });
}
