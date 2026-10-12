using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 本地化键「占位符 ↔ 消费方式」的**闭合守卫** —— 键有三种消费方式，这里把两两关系钉死：
///
/// <list type="number">
///   <item><b>C3</b> 两本字典的占位符索引必须一致（前提断言）；</item>
///   <item><b>C1</b> 代码里 <c>Format(key, args…)</c> / <c>Service[key]</c> 的实参个数
///     必须与模板占位符个数相等（<c>索引器 == 0 个实参</c>）；</item>
///   <item><b>C2</b> XAML 里 <c>{res:Loc key}</c> 是**无实参**消费 ⇒ 被它引用的键
///     模板必须**不含**任何占位符；</item>
///   <item><b>C4</b> XAML 引用的键必须在两本字典里都存在。</item>
/// </list>
///
/// <para><b>为什么必须有 C1</b>：<see cref="LocalizationService.Format(string, object[])"/> 在
/// "对不上"时**不抛异常**，而是原样返回未格式化文本 ⇒ 两种缺陷都不会被编译器或运行期发现：
/// 模板 0 占位符却传了实参 ⇒ **实参被静默丢弃**（"导出失败" 从不说是为什么、
/// "运行中" 连计时都不显示）；模板有占位符却用索引器取 ⇒ 界面上直接出现 <c>{0}</c> 字面量。
/// 本仓 2026-09 实测：858 个被调用的键里 **26 个**踩了这两条。</para>
///
/// <para><b>为什么必须有 C2 —— 这是 C1 单独看不出来的那类</b>：同一个键完全可能
/// 被 XAML 无参引用**又**被代码带参调用（<c>LblTitle</c> 这类"XAML 给初始值、代码随后覆盖"
/// 的控件就是这样）。此时 C1 是绿的（代码那侧对得上），而 XAML 那侧渲染的是**原始模板** ⇒
/// 切换语言时 <c>LocalizedString</c> 的通知会把 <c>运行中 {0}</c> 推回控件，且若不处于运行态
/// 就再也不会被覆盖。所以"给模板补占位符"在这几个键上是**制造回归**，正确做法是
/// 另立一个 0 占位符的标签键 —— C2 就是逼出这个区分的那条判据。</para>
///
/// <para><b>判据来源唯一</b>：模板是否"对得上"只由
/// <see cref="LocalizationPlaceholders.MatchesArgumentCount"/> 回答，
/// 切实参只由 <see cref="LocalizedLogCallSiteTests.SplitTopLevelArguments"/> 负责，
/// 字典只由 <see cref="PluginTextLocalizationGuardTests.LoadDictionary"/> 读 ——
/// 本文件不重写任何一条（判据分家等于没守）。</para>
/// </summary>
public class LocalizedFormatCallSiteTests
{
    private readonly ITestOutputHelper _out;

    public LocalizedFormatCallSiteTests(ITestOutputHelper output) => _out = output;

    /// <summary>Lang 常量名 → 键值。反射取声明本身，不抄键清单。</summary>
    private static readonly Dictionary<string, string> KeyByConstant =
        typeof(Lang)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!, StringComparer.Ordinal);

    private static readonly Dictionary<string, string> DictZh =
        PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");

    private static readonly Dictionary<string, string> DictEn =
        PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    /// <summary>代码侧带实参的消费：<c>任意接收者.Format(</c>。</summary>
    private static readonly Regex FormatCall = new(@"\.Format\s*\(", RegexOptions.Compiled);

    /// <summary>代码侧无实参的消费：本地化服务的索引器。</summary>
    private static readonly Regex IndexerKey = new(
        @"(?:AppLocalization\.Service|LocService|Loc)\[\s*(?:""([^""]+)""|Lang\.(\w+))\s*\]",
        RegexOptions.Compiled);

    /// <summary>XAML 侧消费：<c>{res:Loc key}</c>（标记扩展无实参）。</summary>
    private static readonly Regex XamlLocRef = new(@"\{res:Loc\s+([^\s},]+)", RegexOptions.Compiled);

    /// <summary>
    /// 实参表达式是否"长得像个资源键" —— 用来区分「键写错了」与「首实参压根不是键」。
    ///
    /// <para><b><c>internal</c> 而不是 <c>private</c></b>：<c>ExceptionMessagePassthroughFaceTests</c>
    /// （阶段 82）判定"首实参是不是键"时**复用这一份**。另写一份的后果是两条守卫对同一个字面量
    /// 给出不同结论（比如 <c>string.Format("{0}", ex.Message)</c> 里的 <c>"{0}"</c>：
    /// 不做形状判定的那一份会把它当成键）。</para>
    /// </summary>
    internal static readonly Regex LooksLikeKey = new(@"^[a-z][A-Za-z0-9]*(\.[A-Za-z0-9]+)+$", RegexOptions.Compiled);

    /// <summary>问题清单最多列出多少条（超出只报数目，避免一次刷屏）。</summary>
    private const int MaxListed = 40;

    /// <summary>一次调用点。</summary>
    private sealed record Site(string Relative, int Line, string Key, int ArgumentCount, string Kind);

    /// <summary>把实参表达式解析成键：<c>Lang.X</c> 常量、或字符串字面量；其余返回 null。</summary>
    private static string? ResolveKey(string expression)
    {
        var e = expression.Trim();
        if (e.StartsWith("Lang.", StringComparison.Ordinal))
            return KeyByConstant.TryGetValue(e["Lang.".Length..], out var key) ? key : null;
        if (e.Length >= 2 && e[0] == '"' && e[^1] == '"')
            return e[1..^1];
        return null;
    }

    private static int LineOf(string code, int index) => code[..index].Count(c => c == '\n') + 1;

    /// <summary>扫 App 代码侧的全部键消费点。</summary>
    private static List<Site> CollectCodeSites()
    {
        var sites = new List<Site>();

        foreach (var (relative, text) in AppSourceScanner.ScanAppSources())
        {
            // 必须先剥注释：讲规则的注释里会出现 "Format(key, …)" 这种示例，
            // 不剥的话规则会把自己的文档判成违规。
            var code = SourceScanner.StripComments(text);

            foreach (Match m in FormatCall.Matches(code))
            {
                var args = LocalizedLogCallSiteTests.SplitTopLevelArguments(code, m.Index + m.Length - 1);
                if (args.Count == 0) continue;

                var key = ResolveKey(args[0]);
                if (key is null) continue;      // 首实参不是键（string.Format / 变量） —— 不属本判据的面

                sites.Add(new Site(relative, LineOf(code, m.Index), key, args.Count - 1, "Format"));
            }

            foreach (Match m in IndexerKey.Matches(code))
            {
                var key = m.Groups[1].Success ? m.Groups[1].Value : KeyByConstant.GetValueOrDefault(m.Groups[2].Value);
                if (key is null) continue;

                sites.Add(new Site(relative, LineOf(code, m.Index), key, 0, "[]"));
            }
        }

        return sites;
    }

    /// <summary>扫 XAML 侧的全部键引用（(文件, 行, 键)）。</summary>
    private static List<(string Relative, int Line, string Key)> CollectXamlRefs()
    {
        var refs = new List<(string, int, string)>();

        foreach (var (relative, text) in AppSourceScanner.ScanAppXaml())
        {
            // 屏蔽 XML 注释：注释里也会有 {res:Loc key} 的写法示例。
            // 屏蔽唯一实现走 TestSupport（保长度 **且保行数**）⇒ 下标与行号都对齐原串。
            var xaml = SourceScanner.StripXmlComments(text);
            foreach (Match m in XamlLocRef.Matches(xaml))
                refs.Add((relative, LineOf(text, m.Index), m.Groups[1].Value));
        }

        return refs;
    }

    private static string Report(string title, IEnumerable<string> problems, int total)
    {
        var list = problems.ToList();
        var shown = list.Take(MaxListed);
        var more = list.Count > MaxListed ? $"\n    …另有 {list.Count - MaxListed} 条未列出" : "";
        return $"{title}（{list.Count}/{total}）：\n    " + string.Join("\n    ", shown) + more;
    }

    // ──────────────────────────────────────────
    //  C3：两本字典的占位符必须一致
    // ──────────────────────────────────────────

    [Fact]
    public void BothDictionaries_HaveTheSamePlaceholders()
    {
        var keys = DictZh.Keys.Union(DictEn.Keys, StringComparer.Ordinal).ToList();
        var problems = new List<string>();

        foreach (var key in keys)
        {
            var zh = DictZh.GetValueOrDefault(key, "");
            var en = DictEn.GetValueOrDefault(key, "");
            var iz = LocalizationPlaceholders.Indexes(zh);
            var ie = LocalizationPlaceholders.Indexes(en);

            if (!iz.SequenceEqual(ie))
                problems.Add($"\"{key}\" 占位符不一致：zh {LocalizationPlaceholders.DescribeIndexes(zh)}"
                             + $" / en {LocalizationPlaceholders.DescribeIndexes(en)}");
        }

        // 自检：字典读空 / JSON 结构变了会让上面的循环空跑恒绿
        Assert.True(keys.Count >= 2000, $"只读到 {keys.Count} 个键 —— 字典加载可能失效");
        Assert.True(problems.Count == 0, Report("两本字典的占位符对不上", problems, keys.Count));
    }

    // ──────────────────────────────────────────
    //  C1：代码侧调用点实参数 == 模板占位符数
    // ──────────────────────────────────────────

    [Fact]
    public void FormatAndIndexerCallSites_MatchTheDictionaryPlaceholderCount()
    {
        var sites = CollectCodeSites();
        var problems = new List<string>();
        var formatSites = sites.Count(s => s.Kind == "Format");
        var indexerSites = sites.Count(s => s.Kind == "[]");

        // 读数（不是断言）：把两条面**变成可复跑的数字** —— 那两个下界是"扫描是否失效"的
        // 兜底，量级一旦贴近它们，改动前就必须先看这里的真值。登记 #61q 就是这条：
        // `indexerSites >= 700` 与实测值贴边，而阶段 99 删掉 `login.err` 那处索引器站点
        // 又把它往下推了 1 ⇒ 从此把它写进读数，别再靠"跑一次报错看消息里的数字"。
        JudgeReadings.Write(_out, "本地化调用点（C1 的两条面）",
            new List<string>
            {
                $"    .Format 站点 {formatSites} 处（下界 250） / 索引器站点 {indexerSites} 处"
                + $"（下界 700，登记 #61q） / 合计 {sites.Count} 处",
            });

        foreach (var site in sites)
        {
            if (!DictZh.TryGetValue(site.Key, out var zh))
            {
                // 首实参"长得像键"却不在字典里 —— 那也是界面显示键名，同一族缺陷；
                // 不像键的（string.Format 的模板字面量等）不是本判据的面，直接略过。
                if (LooksLikeKey.IsMatch(site.Key))
                    problems.Add($"{site.Relative}:{site.Line} \"{site.Key}\" 不在字典里（界面会直接显示键名）");
                continue;
            }

            var en = DictEn.GetValueOrDefault(site.Key, "");

            if (!LocalizationPlaceholders.MatchesArgumentCount(zh, site.ArgumentCount)
                || !LocalizationPlaceholders.MatchesArgumentCount(en, site.ArgumentCount))
            {
                var effect = site.ArgumentCount == 0
                    ? "索引器取无实参，界面会显示 {{0}} 字面量"
                    : "实参会被静默丢弃（Format 原样返回未格式化模板）";
                problems.Add($"{site.Relative}:{site.Line} \"{site.Key}\" 模板 zh"
                             + $"{LocalizationPlaceholders.DescribeIndexes(zh)} / en"
                             + $"{LocalizationPlaceholders.DescribeIndexes(en)}"
                             + $" 与实参 {site.ArgumentCount} 个对不上（{effect}）");
            }
        }

        // 自检：两条路径各自都要有量，任一正则失效都会让对应的下限先炸
        Assert.True(formatSites >= 250, $"只校验了 {formatSites} 处 .Format 调用 —— 扫描可能失效");
        Assert.True(indexerSites >= 700, $"只校验了 {indexerSites} 处索引器取值 —— 扫描可能失效");
        Assert.True(problems.Count == 0, Report("本地化调用点与模板占位符对不上", problems, sites.Count));
    }

    // ──────────────────────────────────────────
    //  C2：XAML 无参引用 ⇒ 模板必须无占位符
    // ──────────────────────────────────────────

    [Fact]
    public void XamlReferences_UsePlaceholderFreeTemplates()
    {
        var refs = CollectXamlRefs();
        var problems = new List<string>();

        foreach (var (relative, line, key) in refs)
        {
            if (!DictZh.TryGetValue(key, out var zh)) continue;   // 缺键由 C4 负责点名

            var indexes = LocalizationPlaceholders.Indexes(zh);
            if (indexes.Count > 0)
                problems.Add($"{relative}:{line} \"{key}\" 被 XAML 无实参引用，"
                             + $"模板却含 {LocalizationPlaceholders.DescribeIndexes(zh)}"
                             + " ⇒ 界面（含切换语言后的刷新）会显示占位符字面量");
        }

        Assert.True(refs.Count >= 1000, $"只扫到 {refs.Count} 处 {{res:Loc}} 引用 —— 扫描可能失效");
        Assert.True(problems.Count == 0, Report("XAML 无参引用了带占位符的模板", problems, refs.Count));
    }

    // ──────────────────────────────────────────
    //  C4：XAML 引用的键必须存在
    // ──────────────────────────────────────────

    [Fact]
    public void XamlReferencedKeys_ExistInBothDictionaries()
    {
        var refs = CollectXamlRefs();
        var problems = new List<string>();

        foreach (var (relative, line, key) in refs)
        {
            if (!DictZh.ContainsKey(key))
                problems.Add($"{relative}:{line} \"{key}\" 在 zh-CN 字典里没有（界面会直接显示键名）");
            else if (!DictEn.ContainsKey(key))
                problems.Add($"{relative}:{line} \"{key}\" 在 en-US 字典里没有（切到英文后显示键名）");
        }

        // 自检：XAML 面单独计，避免"只扫到 .cs 也绿"
        Assert.True(refs.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count() >= 800,
            $"XAML 只引用了 {refs.Select(r => r.Key).Distinct().Count()} 个去重键 —— 扫描可能失效");
        Assert.True(problems.Count == 0, Report("XAML 引用了字典里没有的键", problems, refs.Count));
    }

    // ──────────────────────────────────────────
    //  切分器的边界（与 LocalizedLogCallSiteTests 同源，只补一条索引器用例）
    // ──────────────────────────────────────────

    [Fact]
    public void IndexerScanner_ResolvesBothLiteralAndConstantKeys()
    {
        const string src = """
            A.Text = LocService["var.saved"];
            B.Text = AppLocalization.Service[Lang.AssetSvgNoShapesHint];
            C.Text = AppLocalization.Service.Format(Lang.SimRunningStats, a, b, c, d);
            """;
        var code = SourceScanner.StripComments(src);

        var indexerKeys = IndexerKey.Matches(code)
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : KeyByConstant.GetValueOrDefault(m.Groups[2].Value))
            .ToList();

        Assert.Equal(new[] { "var.saved", "asset.svg.no.shapes.hint" }, indexerKeys);

        var call = FormatCall.Match(code);
        var args = LocalizedLogCallSiteTests.SplitTopLevelArguments(code, call.Index + call.Length - 1);
        Assert.Equal("sim.running.stats", ResolveKey(args[0]));
        Assert.Equal(4, args.Count - 1);   // 4 个真占位符的键，实参数必须对得上
    }
}
