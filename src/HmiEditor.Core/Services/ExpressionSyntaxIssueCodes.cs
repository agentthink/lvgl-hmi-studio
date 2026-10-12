namespace HmiEditor.Core.Services;

/// <summary>
/// 表达式**语法**失败诊断码表 —— 「码 + 中文回落原文（+ 实参）」三段式的码那一半
/// （阶段 91，登记 <b>#61i</b>）。与 <c>HmiPackageIssueCodes</c> / <c>SvgParseIssueCodes</c> /
/// <c>CsvImportIssueCodes</c> / <c>GeneratedCodeIssueCodes</c> / <c>ScriptError.Code</c> 同一套约定。
///
/// <para><b>为什么要有这张表</b>：<see cref="ExpressionEngine"/> 的语法错误以前是
/// <c>throw new FormatException("缺少右括号 ')'")</c> —— 消息是**中文裸串**，
/// 而 <c>GeneratedCodeValidator</c> 把它当 <c>{1}</c> 实参塞进
/// <c>gen.issue.expression_syntax</c>（<c>表达式语法错误: {0} ({1})</c>）⇒ 界面切成英文时
/// 括号里的原因那一半**仍是中文**（登记 <b>#61i</b> 的原始症状）。
/// 码化之后 App 层才能按用户语言渲染（出口 <c>ExternalText.Describe(ExpressionSyntaxError)</c>）。</para>
///
/// <para><b>与 <c>LuaSyntaxValidator</c> 的一处关键差别</b>：那个产出方拿到的只有
/// MoonSharp 的**字符串**消息 ⇒ 只能 <c>String.Contains</c> 反推码（脆弱，且实测被版本差异咬过）。
/// 本族的产出方是**我们自己的递归下降解析器**，它**当场就知道**是哪条规则失败的
/// ⇒ 码在抛错点直接给出，**不做字符串反推**。</para>
///
/// <para><b>它是「返回值带码」那一档</b>：<see cref="ExpressionEngine.Compile"/> 抛
/// <see cref="ExpressionSyntaxException"/> 只是因为它的返回契约是 AST；真正的消费入口是
/// <see cref="ExpressionEngine.TryCompile"/>（返回 <see cref="ExpressionSyntaxError"/>）。
/// 三条搬运路径共用同一套码：① 编辑器 UI（经 <c>ExternalText</c> 渲染）；
/// ② 产物注释（<c>CodeGenerator.Screen.cs</c> 的 <c>// WARNING:</c>，**用回落原文** ⇒ 与
/// <c>GENERATOR_WARNING_COMMENT</c> 同一类"判过的不做"）；③ 设计时预览（属性面板）。</para>
/// </summary>
public static class ExpressionSyntaxIssueCodes
{
    /// <summary>三元运算缺 <c>:</c>（<c>1 &gt; 0 ? "a"</c>）。0 实参。</summary>
    public const string TERNARY_MISSING_COLON = "TERNARY_MISSING_COLON";

    /// <summary>括号没有闭合（<c>(1+2</c>）。0 实参。</summary>
    public const string MISSING_RPAREN = "MISSING_RPAREN";

    /// <summary>解析器在该位置不认识这个符号。2 实参 = 该符号的原文、它在表达式里的位置（0 起）。</summary>
    public const string UNEXPECTED_SYMBOL = "UNEXPECTED_SYMBOL";

    /// <summary>
    /// 表达式在**输入结束处**就没写完（如 <c>(1+2</c>：还等着一个 primary，输入已经没了）。
    /// 1 实参 = 输入结束的位置（0 起，即表达式长度）。
    ///
    /// <para>⚠ 它是**阶段 91 量出来的第 6 档**（登记里只写了 3 处抛错）。为什么必须与
    /// <see cref="UNEXPECTED_SYMBOL"/> 分家：tokenizer 把**未知字符**（如 <c>'@'</c>）
    /// 也降级成 <c>TokenType.EOF</c>（<c>ReadOperator</c> 的 <c>_ =&gt; (TokenType.EOF, 1)</c> 分支）
    /// ⇒ 只看记号类型分不出来。真正"输入结束"的判据是 <b>Value 为空串</b>
    /// —— 那是 <c>Tokenize</c> 末尾无条件追加的哨兵，而未知字符的 Value 是那个字符本身。
    /// 合成一档的话，用户看到的是 <c>意外的符号:  (位置 4)</c>（实参是空串）。</para>
    /// </summary>
    public const string UNEXPECTED_END = "UNEXPECTED_END";

    /// <summary>
    /// 数字字面量的形状不对（如 <c>1.2.3</c>）。1 实参 = 该记号的原文。
    ///
    /// <para>⚠ 这一档**阶段 91 才量出来**：tokenizer 按 <c>[0-9.]+</c> 收集数字
    /// （<c>ReadNumber</c>），而解析时写的是 <c>double.Parse</c> ⇒ 这类输入会抛
    /// <c>FormatException</c>，消息是 **.NET 的英文原文**
    /// （<c>The input string '1.2.3' was not in a correct format.</c>）⇒ 中英界面下都是英文。
    /// 处置不是"catch 它然后翻译"，而是把 <c>double.Parse</c> 换成 <c>double.TryParse</c>、
    /// 在**同处**给出码（否则就成了解析器里唯一"靠捕获兜"的地方）。</para>
    /// </summary>
    public const string MALFORMED_NUMBER = "MALFORMED_NUMBER";

    /// <summary>
    /// 表达式是空串或纯空白。0 实参。
    ///
    /// <para>⚠ 也是阶段 91 才量出来的第 4 档：<c>Compile</c> 对空表达式抛
    /// <c>ArgumentNullException(nameof(expression))</c> ⇒ 消息是 **.NET 英文**
    /// （<c>Value cannot be null. (Parameter 'expression')</c>）。可达性实测：
    /// <c>GeneratedCodeValidator</c> 写的是 <c>binding.Expression ?? ""</c> ⇒
    /// "Kind = Expression 但表达式为空"的工程会**真的**走到这里
    /// （属性面板那条路有前置判空，走不到）。</para>
    /// </summary>
    public const string EMPTY_EXPRESSION = "EMPTY_EXPRESSION";

    /// <summary>
    /// **兜底码**：解析器抛出的、上面六档都不认识的异常。
    ///
    /// <para>命名跟随 <c>LuaSyntaxValidator.SyntaxGenericCode</c>（<c>LUA_SYNTAX_GENERIC</c>）
    /// 的既有约定 —— <b>刻意不叫 <c>UNEXPECTED</c></b>：那个名字与
    /// <see cref="UNEXPECTED_SYMBOL"/> 只差一个词，而两者含义毫不相干
    /// （一个是"解析器不认识这个记号"、一个是"防线里的东西"）。</para>
    ///
    /// <para>⚠ 它**结构上到不了**（因此进
    /// <c>ExpressionSyntaxIssueLocalizationGuardTests</c> 的
    /// <c>NotProducible</c> 表、**双向**守着）：解析器现在全部失败都抛
    /// <see cref="ExpressionSyntaxException"/>，外加 <c>Compile</c> 那一条空表达式的
    /// <c>ArgumentNullException</c>；tokenizer 的四个读取方法**一个 <c>throw</c> 都没有**，
    /// <c>Current</c> 有 <c>TokenType.EOF</c> 哨兵兜底（<c>Tokenize</c> 末尾无条件追加），
    /// 数字改走 <c>double.TryParse</c> 后也不再抛 ⇒ <c>TryCompile</c> 的
    /// <c>catch (Exception)</c> 只是**防线**。**不删码**（防线要留着）、
    /// 也**不塞进可产出集**（那是判据造假）—— 阶段 88 立下的三态闭包。</para>
    /// </summary>
    public const string GENERIC = "GENERIC";
}
