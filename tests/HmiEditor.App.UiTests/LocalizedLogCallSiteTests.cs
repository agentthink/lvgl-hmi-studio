using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 带段落标签的日志辅助方法 <c>LogTagged(tagKey, messageKey, args…)</c> 的**实参个数**守卫。
///
/// <para><b>为什么需要它</b>：<see cref="LocalizationService.Format"/> 在"占位符个数与实参个数
/// 不符"时**不抛异常**，而是原样返回未格式化的字符串（并只在 <c>Debug.WriteLine</c> 里留一句 ——
/// <c>Debug</c> 在 Release 下被编译掉）⇒ 用户会在日志面板里看到
/// <c>已刷新串口列表 ({0} 个)</c> 这种字面量。这类错误编译器看不见、运行时不抛，
/// 只有人盯着日志面板才可能发现 —— 所以用一条静态判据把它钉住。</para>
/// </summary>
public class LocalizedLogCallSiteTests
{
    /// <summary>Lang 常量名 → 键值。</summary>
    private static readonly Dictionary<string, string> KeyByConstant =
        typeof(Lang).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!, StringComparer.Ordinal);

    private static readonly Regex Call = new(@"LogTagged\s*\(", RegexOptions.Compiled);

    /// <summary>从 <paramref name="openParen"/> 处的 <c>(</c> 起，按**顶层逗号**切分实参。</summary>
    internal static List<string> SplitTopLevelArguments(string text, int openParen)
    {
        var args = new List<string>();
        var sb = new StringBuilder();
        var depth = 0;
        var inString = false;

        void Flush()
        {
            var a = sb.ToString().Trim();
            if (a.Length > 0) args.Add(a);
            sb.Clear();
        }

        for (var i = openParen; i < text.Length; i++)
        {
            var c = text[i];

            if (inString)
            {
                // 字符串里的逗号/括号**不是分隔符**，但字符本身要照抄（否则实参会凭空消失）
                if (c == '\\' && i + 1 < text.Length) { sb.Append(c).Append(text[i + 1]); i++; continue; }
                if (c == '"') inString = false;
                sb.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    sb.Append(c);
                    continue;
                case '(':
                    depth++;
                    if (depth == 1) { sb.Clear(); continue; }   // 最外层括号本身不算内容
                    break;
                case ')':
                    depth--;
                    if (depth == 0) { Flush(); return args; }
                    break;
                case ',' when depth == 1:
                    Flush();
                    continue;
            }

            if (depth >= 1) sb.Append(c);
        }

        return args;
    }

    [Fact]
    public void LogTaggedCallSites_MatchTheDictionaryPlaceholderCount()
    {
        var svc = new LocalizationService();
        svc.InitializeDefaults();

        var problems = new List<string>();
        var validated = 0;

        foreach (var (relative, text) in AppSourceScanner.ScanAppSources())
        {
            foreach (Match m in Call.Matches(text))
            {
                var args = SplitTopLevelArguments(text, m.Index + m.Length - 1);
                // 方法定义本身没有 "Lang." 形状的实参，跳过
                if (args.Count < 2 || !args[1].StartsWith("Lang.", StringComparison.Ordinal)) continue;

                var constName = args[1]["Lang.".Length..];
                if (!KeyByConstant.TryGetValue(constName, out var key))
                {
                    problems.Add($"{relative}: Lang.{constName} 不是已声明的常量");
                    continue;
                }

                var value = svc.GetString(key, "zh-CN");
                var actual = args.Count - 2;                 // 前两个实参是 tag 与 message 键
                validated++;

                // 判据与运行期**同源**（LocalizationPlaceholders）—— 曾在这里自己按"出现次数"数，
                // 与 Format 的口径不一致；守卫与被守的对象用两套规则等于没守。
                if (!LocalizationPlaceholders.MatchesArgumentCount(value, actual))
                    problems.Add($"{relative}: \"{key}\" 模板占位符 "
                                 + $"{LocalizationPlaceholders.DescribeIndexes(value)} 与实参 {actual} 个对不上 "
                                 + $"（Format 会原样返回未格式化文本，用户会看到 {{0}}）");
            }
        }

        // 自检：正则/切分失效时上面的循环会空跑恒绿
        Assert.True(validated >= 15, $"只校验了 {validated} 处 LogTagged 调用 —— 扫描器可能失效");
        Assert.True(problems.Count == 0, string.Join("\n    ", problems));
    }

    [Fact]
    public void SplitTopLevelArguments_HandlesNestingAndStrings()
    {
        const string src = """LogTagged(A, B, F(x, y), "a,b", C.D);""";
        var m = Call.Match(src);
        var args = SplitTopLevelArguments(src, m.Index + m.Length - 1);

        Assert.Equal(new[] { "A", "B", "F(x, y)", "\"a,b\"", "C.D" }, args);
    }
}
