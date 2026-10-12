using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「**CSV 变量导入的失败原因必须由字典决定**」的守卫 —— 登记表 §4 <b>#61f</b> 的第一刀（阶段 88）。
///
/// <para><b>它守的是哪一类缺陷</b>：<c>CsvVariableImporter.Parse</c> 是纯函数，失败做成
/// <c>ImportResult.Errors</c> <b>返回</b>。改动前那是 <c>List&lt;string&gt;</c> 的**裸中文串**，
/// 而它的两个消费方**一个只取条数、一个整块丢弃**（阶段 88 逐站实测）：</para>
/// <list type="number">
///   <item><c>VariableImportView</c> 只显示 <c>Errors.Count</c>（状态行「| N 个警告」）；</item>
///   <item><c>CommunicationConfigView</c> 的「子设备变量导入」路径**连 <c>Errors</c> 都不读** ——
///     于是 CSV 里有坏行时坏行被**静默丢掉**，用户看不到是哪几行。</item>
/// </list>
/// <para>⇒ 这件事既不是「没翻译」也不是「翻译错了」，是**失败原因根本到不了界面**。
/// 码化之后 <see cref="ExternalText.Describe(CsvImportIssue)"/> 才能把「哪一行、为什么」
/// 按用户语言渲染出来。</para>
///
/// <para><b>为什么它必须与「直通面守卫」区分开</b>：<c>ExceptionMessagePassthroughFaceTests</c>
/// 数的是 <c>Format(键, ex.Message)</c> 那种站点；本族的产出方**根本不抛异常**
/// （阶段 87/88 两次实测：<c>CsvVariableImporter</c> / <c>GeneratedCodeValidator</c> /
/// <c>RecipeManager</c> 在 Core 里 0 处 <c>throw </c>）⇒ 它在站点面上**看不见**、
/// 换成 <c>FailureReason(ex)</c> 也是改名式假闭环。所以「返回值带码」这一族必须有自己的判据。</para>
///
/// <para><b>码集中在 Core、键集中在 App</b>：<c>HmiEditor.Core</c> 拿不到字典服务
/// （依赖方向 App → Core）⇒ 它只声明**稳定的码** + **中文回落原文**，键由
/// <c>ExternalText.CsvIssueKey</c> 统一推导（<c>csv.issue.&lt;码小写&gt;</c>）。
/// 与 <c>SvgParseIssueCodes</c>（M11）/ <c>HmiPackageIssueCodes</c>（M9）/
/// <c>SystemFailureCodes</c>（M10）/ <c>AiIssueCodes</c>（M8）同一套约定。</para>
///
/// <para><b>诚实边界（本判据断不了的事）</b>：① 它只断「渲染等于字典模板」，断不了
/// **翻译质量**；② <see cref="CsvImportIssueCodes.ROW_PARSE_FAILED"/> 那一档的
/// <see cref="CsvImportIssue.Args"/> 带的是 **BCL 原文**（英文）—— 那一半英文是**有意留的**
/// （原文本身就是排障信息），本判据只钉「它没被顺手删掉」；
/// ③ 本判据**不断**「用户是否真的看见了」—— 那由 <see cref="ConsumersKeepTheCodedShape"/> 的
/// 源码面证据兜着（消费点必须同时出现 <c>Errors</c> 与渲染出口），不是端到端 UI 断言。</para>
/// </summary>
[Collection("Language-global")]
public class CsvImportIssueLocalizationGuardTests
{
    private readonly ITestOutputHelper _out;

    public CsvImportIssueLocalizationGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>毒化记号：拿它当"中文回落原文"，凡在渲染里冒头就说明字典键没被查到。</summary>
    private const string Poison = "<<中文回落原文未被使用>>";

    /// <summary>模板占位符（<c>{0}</c> 这种）。本族 5 个码里 <c>NO_HEADER_COLUMNS</c> 与
    /// <c>ROW_PARSE_FAILED</c> 各带 1 个实参，其余 3 个是 0 实参。</summary>
    private static readonly Regex Placeholder = new(@"\{\d", RegexOptions.Compiled);

    /// <summary>CJK 汉字 —— 用来抓「en-US 的值其实没翻」与「中文回落自身可用」。</summary>
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>产出方 —— 码必须由它真实发出，不许退回裸字符串列表。</summary>
    private const string Producer = "src/HmiEditor.Core/Services/CsvVariableImporter.cs";

    /// <summary>两个消费方 —— 它们必须**都**接渲染出口（缺陷本质就是"有人把它丢了"）。</summary>
    private static readonly string[] Consumers =
    {
        "src/HmiEditor.App/Views/VariableImportView.axaml.cs",
        "src/HmiEditor.App/Views/CommunicationConfigView.axaml.cs",
    };

    /// <summary>行号前缀模板的键（与 <c>Lang.CsvLinePrefix</c> 同一把）。</summary>
    private const string LinePrefixKey = "csv.line.prefix";

    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    // ══════════════════════════ 取数（同源出口 —— 负控与 M12 都复用它） ══════════════════════════

    /// <summary>
    /// 反射枚举 <see cref="CsvImportIssueCodes"/> 里声明的全部码。
    ///
    /// <para><b>是同源出口</b>：本文件的多条判据与 <c>LocalizationKeyReachabilityGuardTests</c>
    /// 的 **M12** 都调它，不各写一份反射 —— 抄两份必然漂移，而漂移的表现是
    /// "一个守卫绿、另一个红"，看不出哪边对。</para>
    ///
    /// <para>⚠ <c>CsvImportIssueCodes</c> 是 <c>public</c>，所以不带 <c>BindingFlags.NonPublic</c>
    /// 也对（<c>SystemFailureCodes</c> 是 <c>internal</c>，那边**必须**带）——
    /// 但"反射到 0 个码"会让双向闭包**恒真**，所以本文件有一条下界断言兜底。</para>
    /// </summary>
    internal static List<string> DeclaredCodes()
        => typeof(CsvImportIssueCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    /// <summary>一次真实失败：人读的标签 + 走**真实产出路径**拿到的对象。</summary>
    internal sealed record Hit(string Label, CsvImportIssue Issue);

    /// <summary>
    /// 4 个码各自走**真实产出路径**（不是手搓 <see cref="CsvImportIssue"/>）——
    /// 手搓的话，"码表与产出方脱节"这件事就永远测不到。
    /// </summary>
    internal static List<Hit> RealIssues()
    {
        var hits = new List<Hit>();

        var tooFew = CsvVariableImporter.Parse("");
        hits.Add(new Hit("Parse(空内容)", tooFew.Errors.Single(e => e.Code == CsvImportIssueCodes.TOO_FEW_LINES)));

        var noHeader = CsvVariableImporter.Parse("A,B\n1,2\n");
        hits.Add(new Hit("Parse(表头认不出)", noHeader.Errors.Single(e => e.Code == CsvImportIssueCodes.NO_HEADER_COLUMNS)));

        var missingName = CsvVariableImporter.Parse("DataType,Address\nInt,MW10\n");
        hits.Add(new Hit("Parse(行里没有 Name 列)", missingName.Errors.Single(e => e.Code == CsvImportIssueCodes.ROW_MISSING_NAME)));

        var emptyName = CsvVariableImporter.Parse("Name,DataType,Address\n,Int,MW10\n");
        hits.Add(new Hit("Parse(行里 Name 为空)", emptyName.Errors.Single(e => e.Code == CsvImportIssueCodes.ROW_EMPTY_NAME)));

        return hits;
    }

    /// <summary>
    /// 声明了、但**当前输入空间里构造不出真实触发**的兜底分支 —— 逐条带理由。
    ///
    /// <para><b>为什么允许存在</b>：把「产不出」的码塞进 <c>ProducedCodes</c> 才是造假；
    /// 允许它进这张表 = 把**判据的不完备**明写出来（与 <c>KeptUnreachable</c> 同一个做法），
    /// 而不是把缺陷合法化。⚠ 它是**双向**的：一旦哪天真能产出，
    /// <see cref="ClosureViolations"/> 就红，逼着把这一行摘掉。</para>
    ///
    /// <para><b><see cref="CsvImportIssueCodes.ROW_PARSE_FAILED"/> 的取证</b>（阶段 88 逐行读代码）：
    /// 行循环体里每个操作都**不可能抛** —— 字符串按 <c>Length</c> 迭代、每个列索引都有
    /// <c>idx &lt; fields.Length</c> 前置保护、数值解析走 <c>double/int.TryParse</c>、
    /// 对象构造是普通属性赋值 ⇒ 这个 <c>catch</c> 是**防御性兜底**。
    /// 它不是死代码（防的是将来往循环体里加一句会抛的代码），所以码留着、只是进不了 `产出` 那一档。</para>
    /// </summary>
    internal static readonly (string Code, string Why)[] NotProducible =
    {
        (CsvImportIssueCodes.ROW_PARSE_FAILED,
            "防御性兜底：行循环体里没有会抛的操作（Length 边界 + idx < fields.Length 保护 + TryParse）"),
    };

    // ══════════════════════════ 判据逻辑（纯函数 —— 负控直接调它们） ══════════════════════════

    /// <summary>
    /// 两本字典对每个码的完备性与**成对性**：键在、en 不含中文、两侧不一字不差、
    /// 模板占位符个数**等于该码真实带的实参个数**（后者由 <paramref name="argCounts"/> 给，
    /// 来自 <see cref="RealIssues"/> 的**实测**，不是手抄的一份期望表）。
    ///
    /// <para><b>它是同族共用的纯函数</b>（阶段 89 起）：<paramref name="keyFor"/> 默认走
    /// <c>csv.issue.</c>，<c>GenFindingLocalizationGuardTests</c> 传 <c>gen.issue.</c> ——
    /// 两族的成对性规则**一字不差**（键在 + en 不含中文 + 两侧不同 + 占位符数 == 实参数），
    /// 抄第二份必然漂移，而漂移的表现正是"一个守卫绿、另一个红、看不出哪边对"。
    /// 同族同样直接复用的还有 <see cref="ClosureViolations"/> / <see cref="LeakViolations"/> /
    /// <see cref="CountPlaceholders"/>（三者本来就与码族无关）。</para>
    /// </summary>
    internal static List<string> PairingViolations(
        IEnumerable<string> codes, Dictionary<string, int> argCounts,
        Dictionary<string, string> zh, Dictionary<string, string> en,
        Func<string, string>? keyFor = null)
    {
        var bad = new List<string>();

        foreach (var code in codes)
        {
            var key = (keyFor ?? ExternalText.CsvIssueKey)(code);

            if (!zh.TryGetValue(key, out var z)) { bad.Add($"{key}：zh-CN 缺键"); continue; }
            if (!en.TryGetValue(key, out var e)) { bad.Add($"{key}：en-US 缺键"); continue; }

            if (!argCounts.TryGetValue(code, out var expected))
            { bad.Add($"{code}：没有实参个数读数 —— 取数面漏了这个码（判定会恒真）"); continue; }

            // 占位符个数 ≠ 实参个数时 LocalizationService.Format **不抛异常**、原样返回模板
            // ⇒ 用户会在界面上看到字面的 {0}。这条是"字典与产出方对不上"的唯一静态出口。
            var zhN = CountPlaceholders(z);
            var enN = CountPlaceholders(e);
            if (zhN != expected || enN != expected)
                bad.Add($"{key}：占位符 zh={zhN} / en={enN}，但产出方实际传 {expected} 个实参 ⇒ 界面上会渲染出字面的花括号");

            if (Cjk.IsMatch(e))
                bad.Add($"{key}：en-US 的值里有中文字符 ⇒ 没翻，或直接抄了 zh");

            if (string.Equals(z, e, StringComparison.Ordinal))
                bad.Add($"{key}：两本字典的值一字不差 ⇒ 至少有一本没翻");
        }

        return bad;
    }

    /// <summary>数一个模板里有几个不同的占位符索引（<c>{0}</c> 与 <c>{1}</c> 算两个）。</summary>
    internal static int CountPlaceholders(string template)
        => Regex.Matches(template, @"\{(\d+)\}")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Count();

    /// <summary>
    /// 码表的**三态闭包**：声明了产不出（且没进兜底表）= 死码；产出了没声明 = 漏登记；
    /// 兜底表里的码**已经能产出** = 那张表过期了（双向）。
    /// </summary>
    internal static List<string> ClosureViolations(
        IEnumerable<string> declared, IEnumerable<string> produced, IEnumerable<string> notProducible)
    {
        var d = new HashSet<string>(declared, StringComparer.Ordinal);
        var p = new HashSet<string>(produced, StringComparer.Ordinal);
        var n = new HashSet<string>(notProducible, StringComparer.Ordinal);
        var bad = new List<string>();

        foreach (var c in d.Except(p).Except(n).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"声明了但真实产不出：{c} ⇒ 死码，它和它的字典键都永远不会被用到");

        foreach (var c in p.Except(d).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"产出了但没声明：{c} ⇒ 键不会进字典，界面会回落中文原文");

        foreach (var c in n.Intersect(p).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"兜底表里的 {c} **已经能真实产出** ⇒ 请把它从 NotProducible 摘掉（那张表过期了）");

        foreach (var c in n.Except(d).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"兜底表里的 {c} 根本没声明 ⇒ 表里多了一行");

        return bad;
    }

    /// <summary>
    /// 渲染结果必须**等于字典模板**（含行号前缀）、且**不许出现回落原文**。
    /// <paramref name="rows"/> 的 <c>Candidates</c> 是两本字典各自算出的期望串
    /// （当前语言未知 ⇒ 取并集判定）。
    /// </summary>
    internal static List<string> LeakViolations(
        IEnumerable<(string Label, string Rendered, string Key, string[] Candidates)> rows, string poison)
    {
        var bad = new List<string>();

        foreach (var (label, rendered, key, candidates) in rows)
        {
            if (candidates.Length == 0) { bad.Add($"{label}：两本字典都没有 {key}"); continue; }

            if (rendered.Contains(poison, StringComparison.Ordinal))
                bad.Add($"{label}：渲染里出现**中文回落原文** ⇒ 字典键 {key} 没被查到（查到了就不该回落）");
            else if (!candidates.Contains(rendered, StringComparer.Ordinal))
                bad.Add($"{label}：渲染 {rendered} 既不等于 zh 也不算 en 的期望串 ⇒ 有别的来源");
        }

        return bad;
    }

    /// <summary>
    /// **源码面**：产出方必须还在发码、两个消费方必须都**读 <c>Errors</c> 并走渲染出口**。
    ///
    /// <para><b>为什么这条非有不可</b>：本缺陷的本质不是"少翻译一个词"，而是
    /// **有人把失败原因丢了** —— 只补一个渲染出口、消费点不接线，界面症状一字不变
    /// （正是 <c>CommunicationConfigView</c> 改动前的样子：方法里连 <c>result.Errors</c>
    /// 都不出现）。主判据证明"渲染走字典"，这条证明"**有人真的调它**"，缺一不可。</para>
    ///
    /// <para>剥注释是必须的：讲规则的注释里会写出反例（登记 #48 踩过三次）——
    /// 本阶段自己在那两个消费点的注释里就写了两遍 <c>result.Errors</c>，
    /// 不剥注释的话这条判据会被自己的文档满足。</para>
    /// </summary>
    internal static List<string> WiringViolations(
        IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();
        var map = sources.ToDictionary(s => s.Relative, s => SourceScanner.StripComments(s.Text),
            StringComparer.Ordinal);

        // ① 产出方：码要真的发出来，且不许退回裸字符串列表
        if (!map.TryGetValue(Producer, out var producer))
            bad.Add($"{Producer}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            var emitted = Regex.Matches(producer, @"new CsvImportIssue\(CsvImportIssueCodes\.").Count;
            if (emitted < 5)
                bad.Add($"{Producer}：只有 {emitted} 处发码站点（要求 ≥ 5）⇒ 有失败又退回裸字符串了");

            if (producer.Contains("List<string> Errors", StringComparison.Ordinal))
                bad.Add($"{Producer}：ImportResult.Errors 又退回 List<string> 了 ⇒ 码这一层被拆掉");
        }

        // ② 两个消费方：必须同时出现「读 Errors」与「走渲染出口」
        foreach (var file in Consumers)
        {
            if (!map.TryGetValue(file, out var consumer))
            { bad.Add($"{file}：语料里找不到这个文件（扫描面失效？）"); continue; }

            if (!consumer.Contains(".Errors", StringComparison.Ordinal))
                bad.Add($"{file}：不再读 Errors ⇒ 失败原因又被丢掉了（这正是本项缺陷本来的样子）");

            if (!consumer.Contains("ExternalText.Describe", StringComparison.Ordinal))
                bad.Add($"{file}：没有再走 ExternalText.Describe ⇒ 理由会以中文回落原文直接上屏");
        }

        return bad;
    }

    /// <summary>两本字典各算一份"期望串"：有行号的先套前缀模板，带实参的先填实参。</summary>
    private static string Expected(Dictionary<string, string> dict, CsvImportIssue issue)
    {
        var key = ExternalText.CsvIssueKey(issue.Code);
        if (!dict.TryGetValue(key, out var body))
            return "";

        if (issue.Args is { Count: > 0 } args)
            body = string.Format(CultureInfo.InvariantCulture, body, args.ToArray());

        if (issue.Line > 0)
        {
            if (!dict.TryGetValue(LinePrefixKey, out var prefix))
                return "";
            body = string.Format(CultureInfo.InvariantCulture, prefix, issue.Line, body);
        }

        return body;
    }

    // ══════════════════════════ 判据 ══════════════════════════

    /// <summary>
    /// 主判据：**每一条真实失败**经 <see cref="ExternalText.Describe(CsvImportIssue)"/>
    /// 渲染后，结果都必须来自字典（含行号前缀），**不许出现回落原文**。
    ///
    /// <para><b>为什么用毒化而不是拿真原文比对</b>：拿真原文只能测"今天恰好翻了没"，
    /// 毒化测的是**"渲染这一半确实由字典决定"** —— 后者才是这条链路的性质。</para>
    /// </summary>
    [Fact]
    public void EveryIssue_RendersTheDictionaryTemplate_NeverTheChineseFallback()
    {
        var zh = Zh;
        var en = En;
        var rows = new List<(string Label, string Rendered, string Key, string[] Candidates)>();

        foreach (var hit in RealIssues())
        {
            var key = ExternalText.CsvIssueKey(hit.Issue.Code);
            var candidates = new[] { Expected(zh, hit.Issue), Expected(en, hit.Issue) }
                .Where(v => v.Length > 0)
                .ToArray();

            var poisoned = new CsvImportIssue(hit.Issue.Code, Poison, hit.Issue.Line, hit.Issue.Args);
            rows.Add(($"{hit.Label} · L{hit.Issue.Line}", ExternalText.Describe(poisoned), key, candidates));
        }

        JudgeReadings.Write(_out, "CSV 变量导入失败原因（#61f · csv.issue.*）",
            rows.Select(r => $"    {r.Label,-34} {r.Key,-34} ⇒ {r.Rendered}").ToList());

        var problems = LeakViolations(rows, Poison);
        Assert.True(problems.Count == 0,
            "有失败原因没走字典（登记 #61f / 阶段 88）。正确动作是补 `csv.issue.<码小写>` 的字典键，"
            + "**不是**把裸中文串再拼回去（那会让英文界面下中英混排），也不是让消费点丢掉 Errors：\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 切到两种语言各渲染一遍：**中文那一遍必须有汉字、英文那一遍必须一个汉字都没有**，
    /// 且两遍不同。
    ///
    /// <para>与主判据成对：主判据走"两本字典的期望串取并集"，分不出"渲染出的是哪一本"；
    /// 这条把语言真的切过去，钉住「en-US 不是抄的 zh」。</para>
    ///
    /// <para>⚠ 动的是**静态单例** ⇒ 必须挂 <c>Language-global</c> 集合与其他动语言的类串行，
    /// 并且**必然还原**（<c>finally</c>）。</para>
    /// </summary>
    [Fact]
    public void BothLanguages_RenderDistinctly_AndInTheirOwnScript()
    {
        var hits = RealIssues();
        var original = AppLocalization.Service.CurrentLanguage;

        try
        {
            AppLocalization.Service.SwitchLanguage("zh-CN");
            var zhRenders = hits.Select(h => ExternalText.Describe(
                new CsvImportIssue(h.Issue.Code, Poison, h.Issue.Line, h.Issue.Args))).ToList();
            Assert.All(zhRenders, r => Assert.Matches(Cjk, r));

            AppLocalization.Service.SwitchLanguage("en-US");
            var enRenders = hits.Select(h => ExternalText.Describe(
                new CsvImportIssue(h.Issue.Code, Poison, h.Issue.Line, h.Issue.Args))).ToList();

            JudgeReadings.Write(_out, "两种语言下的渲染（#61f）",
                hits.Select((h, i) => $"    L{h.Issue.Line} zh={zhRenders[i]} | en={enRenders[i]}").ToList());

            for (var i = 0; i < enRenders.Count; i++)
            {
                Assert.False(Cjk.IsMatch(enRenders[i]),
                    $"en-US 下渲染出汉字：「{enRenders[i]}」⇒ 该码的 en 值没翻或是抄的 zh");
                Assert.NotEqual(zhRenders[i], enRenders[i]);
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }
    }

    /// <summary>两本字典对每个码都齐 + 成对 + **占位符个数等于产出方实测的实参个数**。</summary>
    [Fact]
    public void BothDictionaries_CarryEveryCode_AndAreActuallyPaired()
    {
        var codes = DeclaredCodes();
        Assert.True(codes.Count >= 5,
            $"只反射到 {codes.Count} 个码 —— 取数失效（反射面空了，下面的判定会恒真）");

        // 实参个数**实测**：产得出的码从真实失败里读；兜底那一档读不到 ⇒ 由下面这条兜住。
        var argCounts = RealIssues()
            .GroupBy(h => h.Issue.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Issue.Args?.Count ?? 0, StringComparer.Ordinal);

        // ⚠ 兜底那一档没有真实失败可量 ⇒ 它的实参个数只能从产出方源码里数
        //   （`new object[] { ex.Message }`），由 ProducerCarriesTheExceptionMessageWithoutLosingIt 兜底。
        foreach (var (code, _) in NotProducible)
            argCounts.TryAdd(code, 1);

        var problems = PairingViolations(codes, argCounts, Zh, En);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>码表**三态闭包**：声明的码都真的产得出（或如实进兜底表），产出的码都声明了。</summary>
    [Fact]
    public void DeclaredCodes_AndProducedCodes_AreTheSameSet()
    {
        var produced = RealIssues().Select(h => h.Issue.Code).Distinct(StringComparer.Ordinal).ToList();
        var problems = ClosureViolations(
            DeclaredCodes(), produced, NotProducible.Select(n => n.Code));

        JudgeReadings.Write(_out, "CSV 导入码表（#61f）",
            new List<string>
            {
                $"    声明 {DeclaredCodes().Count} 个 / 真实产出 {produced.Count} 个"
                + $" / 兜底 {NotProducible.Length} 个：{string.Join(", ", produced.OrderBy(x => x, StringComparer.Ordinal))}",
            });

        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **兜底那一档不许变成免费通行证**：它必须①仍在产出方源码里被真的使用；
    /// ②那处仍然把异常原文当实参（排障信息没被顺手删掉）。
    /// </summary>
    [Fact]
    public void ProducerCarriesTheExceptionMessageWithoutLosingIt()
    {
        var sources = AppSourceScanner.ScanAllProductSources();
        var producer = sources.FirstOrDefault(s => s.Relative == Producer);
        Assert.False(producer.Text is null, $"语料里找不到 {Producer}（扫描面失效？）");

        var code = SourceScanner.StripComments(producer.Text);
        Assert.Contains("CsvImportIssueCodes.ROW_PARSE_FAILED", code, StringComparison.Ordinal);
        Assert.Contains("new object[] { ex.Message }", code, StringComparison.Ordinal);
    }

    /// <summary>产出方还在发码；两个消费方都**读 <c>Errors</c> 且走渲染出口**（缺陷的反回归钉）。</summary>
    [Fact]
    public void ConsumersKeepTheCodedShape()
    {
        var problems = WiringViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "CSV 导入失败原因的接线面（#61f）",
            new[] { Producer }.Concat(Consumers).Select(f => $"    {f}").ToList());

        Assert.True(problems.Count == 0,
            "失败原因到界面的这条链断了（登记 #61f）。**只补渲染出口不算收口** —— "
            + "消费点不接线时用户所见一字不变：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **诚实边界**：<see cref="CsvImportIssue.Message"/> 仍是中文回落（字典缺键时的兜底），
    /// 所以它必须**自身可用** —— 每条都含汉字，不是空串、不是码名。
    /// 这条正面断言下来，免得下一个人把回落清成空串（那样字典一缺键用户就什么也看不到）。
    /// </summary>
    [Fact]
    public void FallbackMessages_StayUsableChinese()
    {
        foreach (var hit in RealIssues())
        {
            Assert.False(string.IsNullOrWhiteSpace(hit.Issue.Message), $"{hit.Issue.Code}：回落原文是空的");
            Assert.Matches(Cjk, hit.Issue.Message);
            Assert.DoesNotContain(hit.Issue.Code, hit.Issue.Message, StringComparison.Ordinal);
            Assert.True(hit.Issue.Line >= 0, "行号不许是负数");
        }
    }

    /// <summary>
    /// 负控 5 组，两两成对 —— 每组都要求"该红的红、该绿的绿"，
    /// 否则证明不了判据真的在盯那件事（本项目对负控的一贯要求）。
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        var codes = DeclaredCodes();
        var zh = Zh;
        var en = En;
        var argCounts = RealIssues()
            .GroupBy(h => h.Issue.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Issue.Args?.Count ?? 0, StringComparer.Ordinal);
        foreach (var (code, _) in NotProducible)
            argCounts.TryAdd(code, 1);

        var key0 = ExternalText.CsvIssueKey(codes[0]);
        var produced = RealIssues().Select(h => h.Issue.Code).ToList();
        var notProducible = NotProducible.Select(n => n.Code).ToList();

        // ── NC1 · 成对性：正控绿 ──
        Assert.Empty(PairingViolations(codes, argCounts, zh, en));
        Assert.Empty(ClosureViolations(codes, produced, notProducible));

        // NC1a：en 的值改回中文（未翻）⇒ 点名；正控不受影响
        var enCopy1 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy1[key0] = zh[key0];
        Assert.Contains(PairingViolations(codes, argCounts, zh, enCopy1),
            v => v.Contains("中文字符", StringComparison.Ordinal));

        // NC1b：摘掉一个键 ⇒ 点名缺键（字典键不是"加了就完事"的装饰）
        var enCopy2 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy2.Remove(key0);
        Assert.Contains(PairingViolations(codes, argCounts, zh, enCopy2),
            v => v.Contains("缺键", StringComparison.Ordinal));

        // NC1c：实参个数读错一个（把带 1 实参的那个码当成 0 实参）⇒ 占位符那一支点名 ——
        //       这正是"字典与产出方对不上"会渲染出字面 {0} 的那条路。
        var withArgs = RealIssues().First(h => (h.Issue.Args?.Count ?? 0) > 0).Issue.Code;
        var argCopy = new Dictionary<string, int>(argCounts, StringComparer.Ordinal) { [withArgs] = 0 };
        Assert.Contains(PairingViolations(codes, argCopy, zh, en),
            v => v.Contains("占位符", StringComparison.Ordinal));

        // ── NC2 · 三态闭包：**三支都要报** ──
        Assert.Contains(ClosureViolations(codes.Concat(new[] { "NOT_A_REAL_CODE" }), produced, notProducible),
            v => v.Contains("死码", StringComparison.Ordinal));
        Assert.Contains(ClosureViolations(codes, produced.Concat(new[] { "NOT_A_REAL_CODE" }), notProducible),
            v => v.Contains("没声明", StringComparison.Ordinal));
        // 兜底表过期：它列的码"已经能产出" ⇒ 必须点名（否则那张表会变成永久豁免）
        Assert.Contains(ClosureViolations(codes, produced, new[] { produced[0] }),
            v => v.Contains("已经能真实产出", StringComparison.Ordinal));
        // 兜底表里塞一个没声明的码 ⇒ 也得点名
        Assert.Contains(ClosureViolations(codes, produced, new[] { "NOT_A_REAL_CODE" }),
            v => v.Contains("根本没声明", StringComparison.Ordinal));

        // ── NC3 · 渲染泄漏：回落原文冒头 ⇒ 点名；字典期望串 ⇒ 绿 ──
        var hit0 = RealIssues()[0];
        var leak = new[]
        {
            ("演示", $"导入: {Poison}", key0, new[] { Expected(zh, hit0.Issue) }),
        };
        Assert.Contains(LeakViolations(leak, Poison), v => v.Contains("中文回落原文", StringComparison.Ordinal));

        var clean = new[]
        {
            ("演示", Expected(zh, hit0.Issue), key0, new[] { Expected(zh, hit0.Issue) }),
        };
        Assert.Empty(LeakViolations(clean, Poison));

        // 行号前缀忘了套 ⇒ 期望串与渲染串不等 ⇒ 一样点名（否则"行号"这一维没人管）。
        // ⚠ 必须挑一个**行级**样本：码表按序数排序后第一个是文件级的 NO_HEADER_COLUMNS（Line=0），
        //    拿它测这条会静默跳过（第一版就差点这么写）。
        var rowHit = RealIssues().First(h => h.Issue.Line > 0);
        var rowKey = ExternalText.CsvIssueKey(rowHit.Issue.Code);
        var noPrefix = new[]
        {
            ("演示", zh[rowKey], rowKey, new[] { Expected(zh, rowHit.Issue) }),
        };
        Assert.NotEqual(zh[rowKey], Expected(zh, rowHit.Issue)); // 前置：这一对本来就该不同
        Assert.Contains(LeakViolations(noPrefix, Poison), v => v.Contains("有别的来源", StringComparison.Ordinal));

        // ── NC4 · 接线面：三个文件各有一种坏法 ──
        // ⚠ 合成语料必须**每个文件都在**（Relative 一字不差）：WiringViolations 把"语料里缺文件"
        //   也算违规（那是它的下界 —— 扫描面失效时会静默全绿）。下面 ④ **故意**踩那一支。
        var all = new[] { Producer }.Concat(Consumers).ToArray();
        // ⚠ 合规的产出方语料必须**真的有 ≥5 处发码站点**：本函数把"发码站点太少"也算违规，
        //   只写一行会踩到那一支（第一版就是这么红的 —— 红在"发码站点"而不是"无话可说"）。
        var goodProducer = string.Concat(Enumerable.Repeat(
            "var e = new CsvImportIssue(CsvImportIssueCodes.TOO_FEW_LINES, \"x\");\n", 5));
        var goodConsumer = "foreach (var i in result.Errors) s += ExternalText.Describe(i);";
        (string Relative, string Text)[] Corpus(string target, string body, string[] all)
            => all.Select(f => (f, f == target ? body : f == Producer ? goodProducer : goodConsumer)).ToArray();

        // ① 产出方退回裸字符串列表 ⇒ 两条都点名（发码站点太少 + 类型退回）
        var dropped = WiringViolations(Corpus(Producer, "public record R(List<string> Errors);", all));
        Assert.Contains(dropped, v => v.Contains("发码站点", StringComparison.Ordinal));
        Assert.Contains(dropped, v => v.Contains("List<string>", StringComparison.Ordinal));

        // ② 合规写法 ⇒ 无话可说（证明它不是"永远返回违规"的判据）
        Assert.Empty(WiringViolations(Corpus(Producer, goodProducer, all)));

        // ③ 消费方把 Errors 丢了（本缺陷本来的样子）⇒ 点名；只丢渲染出口也点名
        Assert.Contains(WiringViolations(Corpus(Consumers[0], "var n = result.Variables.Count;", all)),
            v => v.Contains("不再读 Errors", StringComparison.Ordinal));
        Assert.Contains(WiringViolations(Corpus(Consumers[0], "foreach (var i in result.Errors) s += i.Message;", all)),
            v => v.Contains("ExternalText.Describe", StringComparison.Ordinal));

        // ④ 语料里**缺**一个目标文件 ⇒ 必须报"找不到" ——
        //    否则扫描面整体失效（返回空语料）时会**静默全绿**：这是本函数唯一的下界，不能省。
        //    ⚠ 不能用 Corpus(...)：它按 f == target 分支，保证清单上的文件**都在** ⇒ 恰好绕过去。
        Assert.Contains(WiringViolations(new[] { (Producer, goodProducer) }),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ⑤ 注释里写的反例不算（规则不许被自己的文档满足，登记 #48）——
        //    该消费方**既有合规写法、又在注释里写一句"我们不读 result.Errors"** ⇒ 剥注释后只该看前者。
        Assert.Empty(WiringViolations(Corpus(Consumers[1],
            goodConsumer + "\n// 反例：这里**不**读 result.Errors，也不用 ExternalText.Describe", all)));
    }
}
