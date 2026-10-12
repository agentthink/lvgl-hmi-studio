using System.Text;
using System.Text.RegularExpressions;
using MoonSharp.Interpreter;

namespace HmiEditor.Core.Services;

/// <summary>
/// Lua 语法校验器 —— 用 MoonSharp 做**真实语法解析**（只 <c>LoadString</c>，不执行任何语句），
/// 把解析失败转成「第 N 行 + 中文说明」。
///
/// <para><b>为什么在这里</b>：编辑器侧不做 Lua 执行（<see cref="ScriptSandboxExecutor"/> 是自研的
/// 简化子集，只依赖 <c>ExpressionEngine</c>），但**校验**可以借真解析器 ——
/// 这正是本类型存在的唯一理由，不引入任何执行能力。</para>
///
/// <para><b>方言边界（务必知情）</b>：MoonSharp 实现的是 <b>Lua 5.2</b>（= <see cref="LuaDialect.ParserBaseline"/>，
/// 库的硬边界，不是产品选择）。实测（2026-09-20，MoonSharp 2.0.0）确认 —— Lua 5.3 的整除 <c>//</c> 与位运算
/// <c>&amp; | ~ &lt;&lt; &gt;&gt;</c>、Lua 5.4 的属性语法 <c>local x &lt;const&gt; = 1</c> 全都会报
/// <c>unexpected symbol</c>；而 <c>goto</c>、<c>bit32</c>、<c>table.pack</c>、十六进制浮点 <c>0x1p4</c>
/// 均可通过。</para>
///
/// <para><b>两个方向都要知道，而且它们不对称</b>（完整表见 <see cref="LuaDialect.Features"/>）：</para>
/// <list type="bullet">
///   <item><b>假阳性</b>（编辑器拒、设备能跑）：5.3+ 的运算符。用户会抱怨，设备不出事。
///   <see cref="LuaDialect.Hints"/> 已把这类报错改写成可读的方言说明；</item>
///   <item><b>假阴性</b>（编辑器放行、设备炸）：<b>危险方向</b>。本类放行 <c>goto</c> / <c>bit32</c> /
///   <c>table.pack</c>，而 Tina SDK 自带的 Lua <b>5.1.5</b> 上这三样都不可用。
///   其中 <c>bit32</c> / <c>table.pack</c> 属 <see cref="LuaRejectStage.Runtime"/> ——
///   **语法完全合法，本类永远看不见**，靠"对齐版本"也解决不了。
///   ⇒ 本类**只负责语法层**：设备侧的真实可用性由 <see cref="LuaDialect.CheckRuntimeSymbols"/>
///   （运行前检查，产出**警告**而不是错误）承接，组装点是
///   <see cref="ScriptEngineService.Validate"/>。**别把符号检查塞进本类** ——
///   那是另一条通道，混进来之后"语法错"和"设备侧风险"就再也分不开了。</item>
/// </list>
///
/// <para>目标固件 Lua 版本**至今未定**（固件里没有 Lua）⇒ 只登记、不对齐；
/// 取证与触发条件见 <c>docs/2026-09-20-lua-dialect-alignment.md</c>。</para>
/// </summary>
public static class LuaSyntaxValidator
{
    /// <summary>
    /// 语法错的 Code **族前缀** —— 所有细分诊断码都以它开头（区别于模型级判据
    /// <c>EMPTY_NAME</c>/<c>EMPTY_CODE</c>/<c>NO_FUNCTION</c>，那三个不属于本族）。
    ///
    /// <para><b>为什么是"前缀 + 细分码"而不是一个笼统的 <c>LUA_SYNTAX_ERROR</c></b>：
    /// 界面要把诊断**原样显示给用户**，而"代码不完整可能缺 end""字符串没闭合"
    /// "这是 5.3 的运算符、此处应视为误报"是三句完全不同的话。笼统一个码就只能把区分塞进
    /// <c>ScriptError.Args</c>，而 <c>Args</c> 是**填模板用的**、不是给程序分派用的
    /// （两件事挤在一个通道里，消费方迟早要猜）。细分之后每个码各自对应一条字典模板，
    /// 也各自可被分派（将来的快速修复、AI 提示都以它为准）。</para>
    ///
    /// <para>判族一律用 <see cref="IsSyntaxError"/>，**不要**拿 Code 去 <c>==</c> 某个具体值
    /// —— 那样一细分就静默失效。</para>
    /// </summary>
    public const string SyntaxErrorCodePrefix = "LUA_SYNTAX_";

    /// <summary>该 Code 是否属于语法错族。</summary>
    public static bool IsSyntaxError(string? code)
        => code is not null && code.StartsWith(SyntaxErrorCodePrefix, StringComparison.Ordinal);

    // ① 方言差异 —— 基准的判定与设备目标不一致造成的**已知误报**（编辑器拒、设备能跑）。
    //    Code 按**运算符形态**命名，与"它是哪个版本引入的"解耦：版本走 Args
    //    （见 LuaDialectHint.Version），这样基准版本换了也不必改 Code 名。
    public const string SyntaxOpIntDivCode = SyntaxErrorCodePrefix + "OP_INTDIV";
    public const string SyntaxOpBitAndCode = SyntaxErrorCodePrefix + "OP_BITAND";
    public const string SyntaxOpBitOrCode = SyntaxErrorCodePrefix + "OP_BITOR";
    public const string SyntaxOpBitNotCode = SyntaxErrorCodePrefix + "OP_BITNOT";
    public const string SyntaxOpShiftRightCode = SyntaxErrorCodePrefix + "OP_SHIFTRIGHT";
    public const string SyntaxAngleAttrOrShiftLeftCode = SyntaxErrorCodePrefix + "ANGLE_ATTR_OR_SHIFTLEFT";

    // ② 解析器报错的分类（按 MoonSharp 的报错文本判定，**顺序即优先级**：具体在前）。
    public const string SyntaxMissingEndCode = SyntaxErrorCodePrefix + "MISSING_END";
    public const string SyntaxUnfinishedLongCode = SyntaxErrorCodePrefix + "UNFINISHED_LONG";
    public const string SyntaxUnfinishedStringCode = SyntaxErrorCodePrefix + "UNFINISHED_STRING";
    public const string SyntaxUnexpectedSymbolCode = SyntaxErrorCodePrefix + "UNEXPECTED_SYMBOL";
    public const string SyntaxMissingEqualsCode = SyntaxErrorCodePrefix + "MISSING_EQUALS";
    public const string SyntaxMissingNameCode = SyntaxErrorCodePrefix + "MISSING_NAME";
    public const string SyntaxExpectedCode = SyntaxErrorCodePrefix + "EXPECTED";
    public const string SyntaxGenericCode = SyntaxErrorCodePrefix + "GENERIC";

    /// <summary>
    /// 校验 Lua 源码。空内容视为**合法**（「内容不能为空」由调用方判，见 <c>EMPTY_CODE</c>）。
    /// </summary>
    public static ScriptValidationResult Validate(string? code)
    {
        var result = new ScriptValidationResult();
        if (string.IsNullOrWhiteSpace(code)) return result;

        try
        {
            // 只解析、不执行：LoadString 做词法 + 语法分析并构建语法树，不运行任何语句。
            _ = new Script().LoadString(code);
        }
        catch (SyntaxErrorException ex)
        {
            result.IsValid = false;
            result.Errors.Add(BuildSyntaxError(code, ex));
        }

        return result;
    }

    /// <summary>
    /// 源码里是否存在函数定义。
    ///
    /// <para>判据是「**剥掉注释后**仍能查到 <c>function</c> 关键字」。裸
    /// <c>Contains("function ")</c> 会被注释里的示例骗过 —— 实测
    /// <c>-- function ghost()\nlocal a = 1</c> 在裸判据下"有函数"，剥注释后才如实报「没有」。</para>
    ///
    /// <para><b>已知假阴性</b>：字符串字面量里的 <c>"function foo()"</c> 仍会被算作有函数
    /// （本方法不改写字符串内容）。危害小 —— 运行时 <c>lua_getglobal</c> 找不到函数会自己报错。</para>
    /// </summary>
    public static bool HasFunctionDefinition(string? code)
        => !string.IsNullOrWhiteSpace(code)
           && Regex.IsMatch(StripComments(code), @"\bfunction\b", RegexOptions.Singleline);

    /// <summary>
    /// 剥掉 Lua 注释：行注释 <c>--</c> 到行尾、块注释 <c>--[[ … ]]</c> 与 <c>--[=[ … ]=]</c>。
    /// <b>换行符一律保留</b>（行号不漂移），字符串字面量原样保留。
    /// </summary>
    public static string StripComments(string code)
    {
        if (string.IsNullOrEmpty(code)) return code ?? "";

        var sb = new StringBuilder(code.Length);
        int i = 0, n = code.Length;

        while (i < n)
        {
            char c = code[i];

            // 字符串字面量：整体抄过去（含转义）
            if (c is '"' or '\'')
            {
                char quote = c;
                sb.Append(c);
                i++;
                while (i < n)
                {
                    if (code[i] == '\\' && i + 1 < n)
                    {
                        sb.Append(code[i]).Append(code[i + 1]);
                        i += 2;
                        continue;
                    }
                    sb.Append(code[i]);
                    if (code[i] == quote) { i++; break; }
                    i++;
                }
                continue;
            }

            if (c == '-' && i + 1 < n && code[i + 1] == '-')
            {
                // 块注释：--[ 后面跟 N 个 '=' 再跟 '['
                int lb = i + 2, eq = 0;
                while (lb + eq < n && code[lb + eq] == '=') eq++;
                if (lb + eq + 1 < n && code[lb + eq] == '[' && code[lb + eq + 1] == '[')
                {
                    string closer = "]" + new string('=', eq) + "]";
                    int end = code.IndexOf(closer, lb + eq + 2, StringComparison.Ordinal);
                    int stop = end < 0 ? n : end + closer.Length;
                    // 保留区间内的换行，避免行号漂移
                    for (int k = i; k < stop; k++)
                        if (code[k] == '\n') sb.Append('\n');
                    i = stop;
                    continue;
                }

                // 行注释：到行尾（不消费换行）
                while (i < n && code[i] != '\n') i++;
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    // ── 错误消息解析与中文化 ──

    /// <summary>
    /// 解析 MoonSharp 的 <c>DecoratedMessage</c>，形如：
    /// <code>chunk_1:(3,14): unexpected symbol near '&amp;'
    ///chunk_1:(2,12-16): unfinished string near 'abc'</code>
    /// <b>注意不能用 <c>Message</c></b> —— 它只有 <c>unexpected symbol near '&amp;'</c>，<b>不含坐标</b>；
    /// 上一版实现正是拿 <c>Message</c> 去找坐标 ⇒ 行号恒为 0（已修）。
    /// </summary>
    private static readonly Regex DecoratedRe = new(
        @"\((?'line'\d+),(?'col'\d+)(?:-\d+)?\)\s*:\s*(?'msg'.*)$",
        RegexOptions.Singleline);

    private static ScriptError BuildSyntaxError(string code, SyntaxErrorException ex)
    {
        string decorated = ex.DecoratedMessage ?? "";
        var m = DecoratedRe.Match(decorated);

        int line, column;
        string raw;

        if (m.Success)
        {
            line = int.Parse(m.Groups["line"].Value);
            column = int.Parse(m.Groups["col"].Value);
            raw = m.Groups["msg"].Value.Trim();
        }
        else
        {
            // 兜底：格式变了也不谎报第 0 行 —— 取总行数（解析器报错多落在末尾附近）
            line = Math.Max(1, code.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').Length);
            column = 0;
            raw = (ex.Message ?? decorated).Trim();
        }

        var (diagCode, args, message) = Diagnose(raw);

        return new ScriptError
        {
            Line = line,
            Column = column,
            Code = diagCode,
            // 不含行号 —— Line 已经是结构化字段，由消费方统一渲染（见 ScriptError 的类型注释）
            Message = message,
            Args = args.Length > 0 ? args : null,   // 空数组与 null 等价，统一成 null
        };
    }

    /// <summary>
    /// 把解析器的英文报错**分类**成「诊断 Code + 模板实参 + 中文回落」。
    ///
    /// <para><b>为什么分类而不是只翻一句</b>：这些报错对用户是三件不同的事 ——
    /// ① 方言误报（"这是 5.3 的运算符，若设备版本更高就该无视"）、
    /// ② 确实写漏了（缺 <c>end</c> / 缺引号 / 缺 <c>=</c>）、
    /// ③ 解析器说不上来的（兜底）。第 ① 类**不该**催用户改代码，第 ② 类**应该**。
    /// 混成一句笼统的"语法错误"就等于把这三件事抹平，而界面只有一行位置。</para>
    ///
    /// <para><b>匹配顺序即优先级，具体在前</b>：<c>'=&lt;name&gt;' expected</c> 也含 "expected"、
    /// <c>unfinished long string</c> 也含 "unfinished string" —— 顺序一乱就静默归错类，
    /// 而且错得看不出来（文案仍然是"合理"的）。</para>
    ///
    /// <para><b>方言文案来自 <see cref="LuaDialect.Hints"/></b>（单一事实源）：基准版本一变，
    /// 文案自动跟着变，这里不写死版本号（写死过一次就会有两处走样）。版本标签当 <c>Args</c>
    /// 传给模板，字典侧同样不写死 —— 标签本身是语言无关的（"Lua 5.2" 换语言也不变）。</para>
    /// </summary>
    private static (string Code, object[] Args, string Message) Diagnose(string raw)
    {
        foreach (var h in LuaDialect.Hints)
        {
            if (raw.Contains(h.Marker, StringComparison.Ordinal))
                return (h.Code,
                        new object[] { LuaDialect.LabelOf(h.Version), LuaDialect.ParserBaselineLabel },
                        h.Fallback);
        }

        // 实测 MoonSharp 2.0.0 报的是 '<eof>'（不是 '<eos>'）—— 上一版只查 '<eos>'
        // ⇒ 这条「缺少 end」的映射从未命中过。两个都收，避免再被版本差异咬一次。
        if (raw.Contains("'<eos>'", StringComparison.Ordinal) ||
            raw.Contains("'<eof>'", StringComparison.Ordinal))
            return (SyntaxMissingEndCode, Array.Empty<object>(), "代码不完整，可能缺少 end");

        if (raw.Contains("unfinished long string", StringComparison.Ordinal) ||
            raw.Contains("unfinished long comment", StringComparison.Ordinal))
            return (SyntaxUnfinishedLongCode, Array.Empty<object>(), "长字符串 / 长注释没有闭合（缺少 ]]）");

        if (raw.Contains("unfinished string", StringComparison.Ordinal))
            return (SyntaxUnfinishedStringCode, Array.Empty<object>(), "字符串没有闭合（缺少结尾的引号）");

        if (raw.Contains("unexpected symbol", StringComparison.Ordinal))
            return (SyntaxUnexpectedSymbolCode, new object[] { raw }, $"出现了意外的符号：{raw}");

        if (raw.Contains("'=' expected", StringComparison.Ordinal))
            return (SyntaxMissingEqualsCode, Array.Empty<object>(), "缺少赋值符号 '='");

        if (raw.Contains("'<name>' expected", StringComparison.Ordinal))
            return (SyntaxMissingNameCode, Array.Empty<object>(), "缺少名称（变量名或函数名）");

        if (raw.Contains("expected", StringComparison.Ordinal))
            return (SyntaxExpectedCode, new object[] { raw }, $"缺少必要的语法元素：{raw}");

        return (SyntaxGenericCode, new object[] { raw }, $"存在语法问题：{raw}");
    }
}
