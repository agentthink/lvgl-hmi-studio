using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 生成 C 代码的**结构化断言**工具 —— 用「是否存在这次调用」代替「是否包含这个子串」。
///
/// <para><b>为什么需要它：</b><c>Assert.Contains("lv_obj_set_pos(obj_c1, 10, 20)", code)</c>
/// 把断言绑死在生成器的**排版细节**上：多一个空格、少一个分号、把实参换行，
/// 行为完全正确的代码也会让测试变红；反过来，同样的子串出现在注释或字符串字面量里
/// 也会让测试变绿。本类把断言提升到语法层面：解析出真实的调用点，
/// 比较函数名与实参，忽略空白与可选的分号，且不把注释/字符串里的同名文本当成调用。</para>
///
/// <para><b>两种断言：</b></para>
/// <list type="bullet">
///   <item><see cref="AssertCall"/> —— 期望文本本身是一次调用
///     （<c>lv_label_set_text(obj_c1, "Hello HMI")</c>），按「函数名 + 实参序列」匹配。</item>
///   <item><see cref="AssertText"/> —— 期望文本不是单次调用（声明、复合表达式等），
///     退化为**空白与标点无关**的包含匹配。</item>
/// </list>
///
/// <para>两者都不改变断言的原意，只是不再依赖排版。</para>
/// </summary>
public static class CSource
{
    /// <summary>代码里出现的一处调用点：函数名 + 各实参原文。</summary>
    public sealed record CallSite(string Name, IReadOnlyList<string> Args)
    {
        /// <summary>规范化后的调用文本，便于失败信息里展示候选。</summary>
        public string Display => $"{Name}({string.Join(", ", Args)})";
    }

    // ───────────────────────── 解析 ─────────────────────────

    /// <summary>
    /// 解析代码中出现的全部调用点（含嵌套调用）。注释与字符串/字符字面量会被跳过，
    /// 因此注释里的 <c>lv_obj_create(...)</c> 不会被当成调用。
    /// </summary>
    public static IReadOnlyList<CallSite> ParseCalls(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var result = new List<CallSite>();
        CollectCalls(code, result);
        return result;
    }

    private static void CollectCalls(string text, List<CallSite> into)
    {
        var i = 0;
        while (i < text.Length)
        {
            if (TrySkipTrivia(text, ref i))
                continue;

            if (!IsIdentStart(text[i]))
            {
                i++;
                continue;
            }

            var nameStart = i;
            while (i < text.Length && IsIdentPart(text[i])) i++;
            var name = text[nameStart..i];

            // 只把「标识符**紧邻** (」视为调用：生成代码永远是 f(...) 形式，
            // 而 "Keyframes (reference)" 这类说明性文本、以及 if (x) 这类关键字都不会误判。
            if (i >= text.Length || text[i] != '(' || IsKeyword(name))
                continue;

            var args = ReadArguments(text, ref i);
            if (args is null)
                return; // 括号不配平：放弃剩余解析

            into.Add(new CallSite(name, args));
            // 实参里还可能嵌套调用（lv_color_hex(...) 等），递归展开。
            foreach (var arg in args)
                CollectCalls(arg, into);
        }
    }

    /// <summary>
    /// 读取一对括号内的实参原文；<paramref name="i"/> 指向 <c>(</c>，返回后指向 <c>)</c> 之后。
    /// 返回 <c>null</c> 表示括号不配平。
    /// </summary>
    private static List<string>? ReadArguments(string text, ref int i)
    {
        var args = new List<string>();
        i++; // 跳过 '('
        var depth = 1;
        var argStart = i;

        while (i < text.Length)
        {
            var c = text[i];
            if (TrySkipTrivia(text, ref i))
                continue;

            switch (c)
            {
                case '(':
                case '[':
                case '{':
                    depth++;
                    i++;
                    break;
                case ')':
                case ']':
                case '}':
                    depth--;
                    if (c == ')' && depth == 0)
                    {
                        var tail = text[argStart..i];
                        if (args.Count > 0 || !string.IsNullOrWhiteSpace(tail))
                            args.Add(tail.Trim());
                        i++;
                        return args;
                    }
                    i++;
                    break;
                case ',' when depth == 1:
                    args.Add(text[argStart..i].Trim());
                    i++;
                    argStart = i;
                    break;
                default:
                    i++;
                    break;
            }
        }

        return null;
    }

    /// <summary>
    /// 跳过注释、字符串与字符字面量。返回 true 表示已跳过（<paramref name="i"/> 已前移）。
    /// </summary>
    private static bool TrySkipTrivia(string code, ref int i)
    {
        var c = code[i];

        if (c == '/' && i + 1 < code.Length && code[i + 1] == '/')
        {
            while (i < code.Length && code[i] != '\n') i++;
            return true;
        }

        if (c == '/' && i + 1 < code.Length && code[i + 1] == '*')
        {
            i += 2;
            while (i + 1 < code.Length && !(code[i] == '*' && code[i + 1] == '/')) i++;
            i = Math.Min(i + 2, code.Length);
            return true;
        }

        if (c == '"' || c == '\'')
        {
            var quote = c;
            i++;
            while (i < code.Length)
            {
                if (code[i] == '\\') { i += 2; continue; }
                if (code[i] == quote) { i++; break; }
                i++;
            }
            return true;
        }

        return false;
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>C 语言关键字：它们后面也跟括号，但不是函数调用。</summary>
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "if", "else", "for", "while", "do", "switch", "case", "return",
        "sizeof", "typeof", "defined", "static_assert", "_Static_assert",
    };

    private static bool IsKeyword(string name) => Keywords.Contains(name);

    /// <summary>
    /// **去 trivia**：注释与字符串/字符字面量都换成空格，其余原样保留 —— 用于「扫生成代码里的
    /// 词法形态」这类判据（与 <see cref="ParseCalls"/> 同一口径：**注释不算代码**）。
    ///
    /// <para><b>为什么需要它（2026-10-01，阶段 240 的事故）</b>：生成物头部有一行**追溯戳**
    /// <c>HmiEditor.CodeGen &lt;版本&gt; (git &lt;短哈希&gt;)</c>（`CodeGenerator.GeneratorStamp`，
    /// 故意留着便于现场反查构建）。当 HEAD 的短哈希末尾恰好是「数字 + f」的形状（如
    /// <c>580599f</c>）时，`ActionCallbacksCodeGenTests` 里那条「整数紧邻 f 后缀 ⇒ 非法浮点常量」
    /// 的扫描会**把它当代码**读成非法字面量 ⇒ **判据按提交哈希 flaky**（同一份代码换个哈希就红）。
    /// 词法扫描的输入口径统一走本方法即可根治。</para>
    /// </summary>
    public static string WithoutTrivia(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var sb = new StringBuilder(code.Length);
        var i = 0;
        while (i < code.Length)
        {
            var before = i;
            if (TrySkipTrivia(code, ref i))
            {
                sb.Append(' ', Math.Max(1, i - before));
                continue;
            }

            sb.Append(code[i]);
            i++;
        }

        return sb.ToString();
    }

    // ───────────────────────── 规范化 ─────────────────────────

    /// <summary>
    /// 规范化表达式：字符串/字符字面量原样保留，其余部分把空白折叠为单空格、
    /// 并去掉标点两侧的空格。用于「排版无关」的比较。
    /// </summary>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];

            if (c == '"' || c == '\'')
            {
                var quote = c;
                sb.Append(c);
                i++;
                while (i < text.Length)
                {
                    sb.Append(text[i]);
                    if (text[i] == '\\') { i++; if (i < text.Length) sb.Append(text[i]); i++; continue; }
                    if (text[i] == quote) { i++; break; }
                    i++;
                }
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                sb.Append(' ');
                continue;
            }

            if (IsTightPunctuation(c))
            {
                TrimTrailingSpace(sb);
                sb.Append(c);
                i++;
                // 吃掉标点后的空白
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString().Trim();
    }

    private static bool IsTightPunctuation(char c) => c is '(' or ')' or '[' or ']' or ',' or ';';

    private static void TrimTrailingSpace(StringBuilder sb)
    {
        while (sb.Length > 0 && sb[^1] == ' ') sb.Length--;
    }

    // ───────────────────────── 断言 ─────────────────────────

    /// <summary>
    /// 断言代码中存在一次调用：函数名相同、实参个数相同、每个实参规范化后相同。
    /// 期望文本末尾的分号可写可不写。
    /// </summary>
    /// <param name="code">生成的 C 代码。</param>
    /// <param name="expectedCall">形如 <c>lv_obj_set_pos(obj_c1, 10, 20)</c> 的期望调用。</param>
    public static void AssertCall(string code, string expectedCall)
    {
        var want = ParseSingleCall(expectedCall);
        var calls = ParseCalls(code);

        var matched = calls.Any(c => SameCall(c, want));
        Assert.True(matched, BuildCallFailure(code, expectedCall, want, calls, expectAbsent: false));
    }

    /// <summary>断言代码中**不存在**该调用（同样是函数名 + 实参的结构化匹配）。</summary>
    public static void AssertNoCall(string code, string expectedCall)
    {
        var want = ParseSingleCall(expectedCall);
        var calls = ParseCalls(code);

        var matched = calls.Any(c => SameCall(c, want));
        Assert.False(matched, BuildCallFailure(code, expectedCall, want, calls, expectAbsent: true));
    }

    /// <summary>
    /// 断言规范化后的期望文本出现在规范化后的代码里。用于不是单次调用的期望
    /// （变量声明、带下标的表达式、片段等）：仍然比原始 <c>Contains</c> 宽容排版。
    /// </summary>
    public static void AssertText(string code, string expectedFragment)
    {
        var haystack = Normalize(code);
        var needle = Normalize(StripTrailingSemicolon(expectedFragment));
        Assert.True(
            haystack.Contains(needle, StringComparison.Ordinal),
            $"生成代码中未找到（忽略排版）:\n  {expectedFragment}\n实际代码片段:\n{ExcerptAround(haystack, needle)}");
    }

    /// <summary>断言规范化后的期望文本**不**出现在规范化后的代码里。</summary>
    public static void AssertNoText(string code, string expectedFragment)
    {
        var haystack = Normalize(code);
        var needle = Normalize(StripTrailingSemicolon(expectedFragment));
        Assert.False(
            haystack.Contains(needle, StringComparison.Ordinal),
            $"生成代码中不应出现（忽略排版）:\n  {expectedFragment}");
    }

    /// <summary>
    /// 智能断言：期望文本能解析为单次调用时按调用匹配，否则退化为排版无关的文本包含。
    /// 迁移旧 <c>Assert.Contains</c> 时用它可以逐条替换而不必分类。
    /// </summary>
    public static void AssertCode(string code, string expected)
    {
        if (TryParseSingleCall(expected, out _))
            AssertCall(code, expected);
        else
            AssertText(code, expected);
    }

    /// <summary><see cref="AssertCode"/> 的否定形式。</summary>
    public static void AssertNoCode(string code, string expected)
    {
        if (TryParseSingleCall(expected, out _))
            AssertNoCall(code, expected);
        else
            AssertNoText(code, expected);
    }

    // ───────────────────────── 内部工具 ─────────────────────────

    private static string StripTrailingSemicolon(string text)
    {
        var t = text.Trim();
        return t.EndsWith(';') ? t[..^1] : t;
    }

    /// <summary>把期望文本解析为单次调用；不是调用则抛出（属于测试写法错误，应立即暴露）。</summary>
    private static CallSite ParseSingleCall(string expected)
    {
        if (!TryParseSingleCall(expected, out var call))
            throw new ArgumentException(
                $"期望文本不是单次调用，请改用 AssertText/AssertCode：{expected}", nameof(expected));
        return call;
    }

    private static bool TryParseSingleCall(string expected, out CallSite call)
    {
        call = null!;
        var expr = StripTrailingSemicolon(expected).Trim();
        if (expr.Length == 0 || !IsIdentStart(expr[0]))
            return false;

        var i = 0;
        while (i < expr.Length && IsIdentPart(expr[i])) i++;
        var name = expr[..i];
        if (i >= expr.Length || expr[i] != '(' || IsKeyword(name))
            return false;

        var args = ReadArguments(expr, ref i);
        if (args is null)
            return false;

        // 整个表达式必须到此结束（说明它本身就是一次调用，而不是某个更大的表达式）。
        while (i < expr.Length && char.IsWhiteSpace(expr[i])) i++;
        if (i != expr.Length)
            return false;

        call = new CallSite(name, args);
        return true;
    }

    private static bool SameCall(CallSite actual, CallSite expected)
    {
        if (!string.Equals(actual.Name, expected.Name, StringComparison.Ordinal))
            return false;
        if (actual.Args.Count != expected.Args.Count)
            return false;

        for (var i = 0; i < actual.Args.Count; i++)
        {
            if (!Normalize(actual.Args[i]).Equals(Normalize(expected.Args[i]), StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string BuildCallFailure(
        string code, string expectedCall, CallSite want, IReadOnlyList<CallSite> calls, bool expectAbsent)
    {
        var sameName = calls.Where(c => c.Name == want.Name).Select(c => c.Display).Distinct().ToList();
        var candidates = sameName.Count == 0
            ? "（代码中没有任何同名调用）"
            : string.Join("\n", sameName.Take(10).Select(c => "    " + c));

        return $"生成代码中{(expectAbsent ? "不应出现" : "未找到")}调用:\n  {expectedCall}\n"
             + $"同名调用（规范化后，最多 10 条）:\n{candidates}";
    }

    private static string ExcerptAround(string haystack, string needle)
    {
        var at = haystack.IndexOf(needle, StringComparison.Ordinal);
        if (at < 0)
        {
            var head = haystack.Length > 400 ? haystack[..400] : haystack;
            return head;
        }

        var start = Math.Max(0, at - 80);
        var len = Math.Min(haystack.Length - start, needle.Length + 160);
        return haystack.Substring(start, len);
    }
}
