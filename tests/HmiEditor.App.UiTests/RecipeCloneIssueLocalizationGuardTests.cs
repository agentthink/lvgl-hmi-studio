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
/// 「**配方克隆的失败原因必须由字典决定**」的守卫 —— 登记表 §4 <b>#61t</b>（阶段 98）。
///
/// <para><b>它守的是哪一类缺陷</b>：<c>RecipeManager.CloneRecipe</c> 改动前的签名是
/// <c>public Recipe CloneRecipe(...)</c> —— 返回类型**声明为非空**，失败档却写 <c>return null!</c>
/// （用 null-forgiving 压掉可空警告）。两处后果叠在一起：</para>
/// <list type="number">
///   <item>消费点 <c>RecipeEditorView.OnCloneRecipe</c> 写的是 <c>if (clone != null)</c>
///     而**没有 <c>else</c>** ⇒ 克隆失败时界面**一声不响**（点了按钮什么都没发生）；</item>
///   <item>更要紧的是它**丢掉了 <c>AddRecipe</c> 的返回值** —— 那个 <c>bool</c> 是作者专门为
///     「Id 已存在」写的守卫（<c>Recipe.Id</c> = <c>Guid[..8]</c>，**32 位**，撞车罕见但可达）
///     ⇒ 撞车时照样把 clone 当成功返回，界面提示**「已克隆」（撒谎）**。</item>
/// </list>
///
/// <para><b>处置 = 返回值带码</b>（与 <c>SvgParseError</c> / <c>CsvImportIssue</c> /
/// <c>ExpressionSyntaxError</c> 同一条模板，见登记 #61f「模板已立」）：
/// <see cref="RecipeCloneResult"/> ＋ <see cref="RecipeCloneIssue"/> ＋
/// <c>ExternalText.Describe(RecipeCloneIssue)</c>，键统一走 <c>recipe.issue.&lt;码小写&gt;</c>。
/// ⚠ 它**不**走 <c>ExternalText.FailureReason</c> —— 那条路收的是 <c>Exception</c>，
/// 而本族产出方**不抛异常**。</para>
///
/// <para><b>⚠ 为什么不能只把站点换成码化出口就收工</b>：站点换成
/// <c>ExternalText.Describe(issue)</c> 而**产出方没给码** = 阶段 83 钉死的**改名式假闭环**
/// （用户所见一字不变，而 <c>ExceptionMessagePassthroughFaceTests</c> 那张面会绿着变空）。
/// 本刀的两半必须同时在：产出方给码 ＋ 站点接住。所以下面既有产出方形状判据、也有站点接线判据。</para>
///
/// <para><b>⚙ 主判据为什么是「毒化 + 两语言各渲染一遍」双见证</b>（本族与自己前面几族的分叉点）：</para>
/// <list type="number">
///   <item><b>毒化</b>（<see cref="Poison"/> 塞进 <c>Message</c> 当回落原文）：渲染里若冒头就说明
///     字典键没被查到、走了回落 —— 与 <c>SvgParseIssueLocalizationGuardTests</c> 同形，
///     而且**与当前语言无关**（本族的 zh 字典值与回落原文**措辞不同** ⇒ 不能靠"等于回落"来判）；
///   <item><b>两语言各渲染一遍</b>（阶段 97 立的做法）：断言渲染逐字等于「该语言字典模板填上真实参」
///     —— 它额外钉住**实参真的被填进去了**（<c>LocalizationService.Format</c> 在占位符与实参
///     个数不符时**不抛异常**，用户会看到字面的 <c>{0}</c>）。
///     ⚠ 换了措辞就等于换了判据强度：只比"不发毒化"会让"漏填实参"通过。</item>
/// </list>
///
/// <para><b>⚙ 本类会切换进程级全局语言 ⇒ 必须挂 <c>[Collection("Language-global")]</c></b>
/// （<c>DisableParallelization = true</c>）—— 否则与大队并行时会打红**别的**测试类，
/// 而症状随字典内容变、极难归因。判据是
/// <c>LocalizationRefreshUiTests.EveryTestClassThatSwitchesLanguage…</c>；切换必然 <c>finally</c> 还原。</para>
///
/// <para><b>诚实边界</b>：</para>
/// <list type="number">
///   <item><see cref="RecipeIssueCodes.REJECTED"/> **没有"走真实产出路径"的见证** ——
///     它要靠 <c>Recipe.Id</c>（<c>Guid[..8]</c>，2³² 个取值里撞一次）真的撞上才发生，
///     是**概率事件、不可确定性复现**，而主判据不许有间发性（一次绿等于零信息）。
///     ⇒ 它进 <see cref="NotProducible"/> 并写明理由，渲染见证由**工厂**
///     <c>RecipeCloneIssue.Rejected</c> 提供（与 <c>SvgParseError.NoShapes</c> 由消费方构造同一处置）。
///     **那个码必须存在**：守卫正是为那一天写的，改动前它被丢掉后**报成功**。
///     ⚠ 两件事要分清，本类用 <see cref="Hit.ReachedByProducer"/> 把它们分开：
///     「**产出方真的会发出这个码吗**」（闭包问的，REJECTED 答不了）≠
///     「**这个码的键与两本字典通不通**」（渲染问的，工厂构造就够）。
///     并把"那个工厂确实挂在产出方的失败分支上"交给 <see cref="ProducerViolations"/> 钉住 ——
///     否则 <see cref="NotProducible"/> 会把一个**真死码**合法化（登记 #49）。</item>
///   <item>只断「渲染等于该语言的字典模板」，断不了**翻译质量**；</item>
///   <item>两个 <c>catch</c> 里的 <c>ex.Message</c>（<c>recipe.clone.failed</c>）**刻意保留**：
///     它包住的是 <c>RefreshGroups</c> / <c>RefreshRecipes</c> 这两行 UI 刷新，产出方给不出码
///     ⇒ 换出口是改名式假闭环。那是有意留的基线行，见
///     <c>ExceptionMessagePassthroughFaceTests</c> 的阶段 98 段。</item>
/// </list>
/// </summary>
[Collection("Language-global")]
public class RecipeCloneIssueLocalizationGuardTests
{
    private readonly ITestOutputHelper _out;

    public RecipeCloneIssueLocalizationGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>毒化记号：拿它当"中文回落原文"，凡在渲染里冒头就说明字典键没被查到。</summary>
    internal const string Poison = "<<中文回落原文未被使用>>";

    /// <summary>模板占位符索引（<c>{0}</c> / <c>{1}</c>）。与实参个数的对应是硬判据 ——
    /// <c>LocalizationService.Format</c> 在个数不符时**不抛异常**，用户会看到字面的 <c>{0}</c>。</summary>
    private static readonly Regex PlaceholderIndex = new(@"\{(\d+)\}", RegexOptions.Compiled);

    /// <summary>CJK 汉字 —— 用来抓"en-US 的值其实没翻"，也用来判"英文界面下这一半是不是还冒中文"。</summary>
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>产出方：本刀把它从「声明非空 + <c>return null!</c>」改成**返回值带码**。</summary>
    internal const string Producer = "src/HmiEditor.Core/Services/RecipeManager.cs";

    /// <summary>站点：本刀给它的失败档接上渲染出口（改动前**连 <c>else</c> 都没有**）。</summary>
    internal static readonly string[] Sites =
    {
        "src/HmiEditor.App/Views/RecipeEditorView.axaml.cs",
    };

    /// <summary>
    /// 码表的**第三态** —— <c>声明了</c> ∪ <c>真实产得出</c> 之外的合法去处：**现在产不出、
    /// 但必须留着**。与本仓库 M11/M12/M14 的处置同一形状（那里叫 <c>NotProducible</c>）。
    ///
    /// <para>⚠ 这张表是**差异表、不是豁免**：表内条目一旦**真的变得产得出**，判据会红，
    /// 逼维护者删掉这一条（否则它会退化成"能把缺陷合法化"的过期字段 —— 登记 #54 的病）。
    /// 表外多一条、少一条同样红。</para>
    /// </summary>
    internal static readonly Dictionary<string, string> NotProducible = new(StringComparer.Ordinal)
    {
        [RecipeIssueCodes.REJECTED] =
            "克隆体入库被拒：要靠 Recipe.Id（Guid[..8]）真的撞上既有 Id 才发生，"
            + "是概率事件、不可确定性复现（主判据不许有间发性：一次绿等于零信息）"
            + "⇒ 渲染见证由工厂 RecipeCloneIssue.Rejected 提供。"
            + "码必须留着：改动前 AddRecipe 的 bool 被丢掉 ⇒ 撞车时报「已克隆」（撒谎）。",
    };

    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    // ══════════════════════════ 取数（同源出口 —— 负控与 M16 都复用它） ══════════════════════════

    /// <summary>
    /// 反射枚举 <see cref="RecipeIssueCodes"/> 里声明的全部码。
    ///
    /// <para><b>是同源出口</b>：本文件的多条判据与 <c>LocalizationKeyReachabilityGuardTests</c>
    /// 的 <b>M16</b> 都调它，不各写一份反射（抄两份必然漂移，漂移的表现是"一个守卫绿、另一个红"）。</para>
    ///
    /// <para>⚠ "反射到 0 个码"会让三态闭包**恒真** ⇒ 下面每条判据都带下界断言兜底。</para>
    /// </summary>
    internal static List<string> DeclaredCodes()
        => typeof(RecipeIssueCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// 一次取到的失败：人读的标签 + 那个 <see cref="RecipeCloneIssue"/> + **它是不是由产出方真的产出来的**。
    ///
    /// <para><b>为什么必须区分这一维</b>（本判据第一版实测红出来的）：渲染/成对判据要的是"这一档
    /// 的码 → 键 → 两本字典通不通"，由**工厂**构造就够；而**三态闭包**问的是另一个问题 ——
    /// "产出方今天真的会发出这个码吗"。把两者混成一个集合，会让 <c>REJECTED</c>（概率档、
    /// 由工厂构造）既算"产得出"、又在 <see cref="NotProducible"/> 里，于是那条
    /// 「豁免过期」判据**自己把自己打红**。分开之后：<c>ReachedByProducer = false</c> 的那些
    /// 只进渲染面，不进闭包。</para>
    /// </summary>
    internal sealed record Hit(string Label, RecipeCloneIssue Issue, bool ReachedByProducer);

    /// <summary>
    /// 一次取数的结果：**命中** + **退化**。
    /// </summary>
    /// <param name="Hits">拿到的失败档（两条路径应当全在；见 <see cref="Hit.ReachedByProducer"/>）。</param>
    /// <param name="Degraded">本该返回失败档、却返回了成功（或两档都空）的路径 —— 产出方退化的直接证据。</param>
    internal sealed record Probe(List<Hit> Hits, List<string> Degraded);

    /// <summary>
    /// 「退化」的统一红消息：点名**哪条路**没给出失败档、以及正确动作。
    ///
    /// <para>记成数据再判，红才自带信息（本项目对判据的一贯要求）—— 直接断言
    /// <c>Hits.Count == n</c> 时看不出是哪条路退化、也看不出该修产出方还是修站点。</para>
    /// </summary>
    internal static string DegradedMessage(IReadOnlyList<string> degraded)
        => "这几条路径本该返回**带码的失败档**，却没有 ⇒ 产出方退化了（登记 #61t / 阶段 98）。"
           + "正确动作是让产出方**给码**（`RecipeCloneResult.Failed(...)`），"
           + "**不是**把站点改回 `return null` + 静默：\n  - " + string.Join("\n  - ", degraded);

    /// <summary>
    /// 两条路径取数：① 走**真实产出路径**（源配方不在）；② 走**工厂**（入库被拒，概率档）。
    ///
    /// <para>⚠ ① 若手搓 <c>RecipeCloneIssue</c>，"码表与产出方脱节"这件事就永远测不到 ——
    /// 所以它必须真的调一次 <c>RecipeManager.CloneRecipe</c>，并断言拿回来的是
    /// <c>RecipeCloneResult.Failed</c> 那一档（而不是 <c>Ok</c> 或"两档都空"）。</para>
    /// </summary>
    internal static Probe ProbeFailures()
    {
        var hits = new List<Hit>();
        var degraded = new List<string>();

        // ① 源配方不在：界面持有的选中项已失效（别处删掉了 / 工程重载过）——
        //    这是用户真能撞到的运行条件，走真实产出路径。
        var manager = new RecipeManager();
        var result = manager.CloneRecipe("no-such-recipe", "副本");

        if (result.Issue is { } missing && result.Recipe is null)
            hits.Add(new Hit("RecipeManager.CloneRecipe(源配方不在)", missing, ReachedByProducer: true));
        else
            degraded.Add($"RecipeManager.CloneRecipe(源配方不在)：返回的是 "
                + $"Recipe={(result.Recipe is null ? "null" : "非null")} / "
                + $"Issue={(result.Issue is null ? "null" : "非null")}，"
                + "应为 Failed 那一档（Issue 非空且 Recipe 为空）");

        // ② 入库被拒：概率档（见 NotProducible）—— 由**工厂**构造，
        //    这样它的码 → 键 → 两本字典仍被完整见证，只是"能不能真撞上"不在本判据的能力范围内。
        hits.Add(new Hit("RecipeCloneIssue.Rejected（工厂构造）", RecipeCloneIssue.Rejected("deadbeef"),
            ReachedByProducer: false));

        return new Probe(hits, degraded);
    }

    /// <summary>一次渲染对照：标签 + 语言 + 实渲染 + 该语言字典**填参后**的期望串 + 该语言下的回落原文。</summary>
    internal sealed record RenderRow(
        string Label, string Language, string Rendered, string Key, string? Expected, string Fallback);

    /// <summary>
    /// 某本字典里这个键**填上实参**之后的串；键不在字典里返回 <c>null</c>。
    /// </summary>
    internal static string? Expected(Dictionary<string, string> dict, string key, IReadOnlyList<object>? args)
    {
        if (!dict.TryGetValue(key, out var body)) return null;
        return args is { Count: > 0 } a
            ? string.Format(CultureInfo.InvariantCulture, body, a.ToArray())
            : body;
    }

    // ══════════════════════════ 判据逻辑（纯函数 —— 负控直接调它们） ══════════════════════════

    /// <summary>
    /// 渲染必须**不冒毒化**、且**逐字等于「该语言字典模板填上真实参」**；键不在该语言字典里 ⇒ 点名。
    ///
    /// <para>两条一起判，因为它们的见证面不重合：毒化见证"没走回落"，期望串比对见证
    /// "实参真的被填进去了"（<c>Format</c> 在个数不符时不抛异常）。少任何一条都会漏掉一类缺陷。</para>
    /// </summary>
    internal static List<string> RenderViolations(IEnumerable<RenderRow> rows, string poison)
    {
        var bad = new List<string>();

        foreach (var r in rows)
        {
            if (r.Expected is null)
            {
                bad.Add($"{r.Label} @{r.Language}：字典里没有 {r.Key} ⇒ 该语言下用户读到的是中文回落原文"
                    + $"「{r.Fallback}」");
                continue;
            }

            if (r.Rendered.Contains(poison, StringComparison.Ordinal))
            {
                bad.Add($"{r.Label} @{r.Language}：渲染里出现**中文回落原文** ⇒ 字典键 {r.Key} 没被查到"
                    + "（查到了就不该回落）");
                continue;
            }

            if (!string.Equals(r.Rendered, r.Expected, StringComparison.Ordinal))
                bad.Add($"{r.Label} @{r.Language}：渲染「{r.Rendered}」≠ 该语言字典填参后的"
                    + $"「{r.Expected}」⇒ 有别的来源（漏填实参 / 码映射错位 / 渲染没走本键）");
        }

        return bad;
    }

    /// <summary>两本字典对每个码的完备性、成对性，以及**占位符个数与实参个数的对应**。</summary>
    internal static List<string> PairingViolations(
        IEnumerable<(string Key, int ArgCount)> rows, Dictionary<string, string> zh, Dictionary<string, string> en)
    {
        var bad = new List<string>();

        foreach (var (key, argCount) in rows)
        {
            if (!zh.TryGetValue(key, out var z)) { bad.Add($"{key}：zh-CN 缺键"); continue; }
            if (!en.TryGetValue(key, out var e)) { bad.Add($"{key}：en-US 缺键"); continue; }

            if (Cjk.IsMatch(e))
                bad.Add($"{key}：en-US 的值里有中文字符 ⇒ 没翻，或直接抄了 zh");

            if (string.Equals(z, e, StringComparison.Ordinal))
                bad.Add($"{key}：两本字典的值一字不差 ⇒ 至少有一本没翻");

            foreach (var (lang, value) in new[] { ("zh-CN", z), ("en-US", e) })
            {
                var count = PlaceholderIndex.Matches(value)
                    .Select(m => m.Groups[1].Value)
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                if (count != argCount)
                    bad.Add($"{key}：{lang} 的模板有 {count} 个占位符，而这一档的实参是 {argCount} 个 "
                        + "⇒ Format 在个数不符时**不抛异常**，界面上会渲染出字面的 {{0}}／丢参数");
            }
        }

        return bad;
    }

    /// <summary>
    /// 码表的**三态闭包**：<c>声明 = 真实产得出 ∪ 明确产不出（且写了理由）</c>。
    ///
    /// <para>四档都要报（阶段 88 立的口径，本族照用）：</para>
    /// <list type="bullet">
    ///   <item>声明了、既产不出也不在表里 ⇒ **死码**（它和它的字典键永远不会被用到）；</item>
    ///   <item>产出了、但没声明 ⇒ **漏登记**（键不会进字典，界面回落中文原文）；</item>
    ///   <item>**表里说产不出、其实产得出** ⇒ 那条豁免过期了，必须删（登记 #54 的病）；</item>
    ///   <item>表里记了一个根本没声明的码 ⇒ 抄写漂移。</item>
    /// </list>
    /// </summary>
    internal static List<string> ClosureViolations(
        IEnumerable<string> declared, IEnumerable<string> produced, IReadOnlyDictionary<string, string> notProducible)
    {
        var d = new HashSet<string>(declared, StringComparer.Ordinal);
        var p = new HashSet<string>(produced, StringComparer.Ordinal);
        var np = new HashSet<string>(notProducible.Keys, StringComparer.Ordinal);
        var bad = new List<string>();

        foreach (var c in d.Except(p).Except(np).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"声明了但真实产不出、也没写进 NotProducible：{c} ⇒ 死码，它和它的字典键都永远不会被用到");

        foreach (var c in p.Except(d).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"产出了但没声明：{c} ⇒ 键不会进字典，界面会回落中文原文");

        foreach (var c in np.Intersect(p).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"NotProducible 里说「产不出」、实际却产得出：{c} ⇒ 那条豁免过期了，删掉它"
                + "（豁免表里的理由没人核就会变成把缺陷合法化的字段，登记 #54）");

        foreach (var c in np.Except(d).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"NotProducible 里记了 {c}，但码表里根本没有这个码 ⇒ 抄写漂移");

        return bad;
    }

    /// <summary>
    /// **产出方不许退化**：<c>CloneRecipe</c> 必须返回带码的 <see cref="RecipeCloneResult"/>，
    /// 且**必须消费** <c>AddRecipe</c> 的返回值。
    ///
    /// <para><b>为什么这几条非有不可</b>：站点换成 <c>ExternalText.Describe(issue)</c> 之后，
    /// 产出方若退回「非空返回类型 + <c>return null!</c>」，那个 <c>issue</c> 就永远为 <c>null</c>
    /// ⇒ 失败档分支变成死代码，而**站点形状是"对"的**（它确实接了失败档）⇒ 又是"面绿着、收益为零"。
    /// 另一半是 <c>AddRecipe</c> 的那个 <c>bool</c>：改动前的真缺陷就是**丢掉它**，
    /// 症状是界面提示「已克隆」而实际没入库（<c>Sites</c> 的判据看不出这一点）。</para>
    ///
    /// <para>剥注释是必须的：讲规则的注释里会写出反例（登记 #48 踩过三次）。
    /// ⚠ 用「<c>AddRecipe(clone)</c> 出现次数 == <c>if (!AddRecipe(clone))</c> 出现次数」判"没被丢掉"，
    /// 而不是判"包含某个串"—— 后者会让"留着那个 <c>if</c> 却又多写一处裸调用"通过。</para>
    ///
    /// <para>另外还要两个**工厂各自恰好出现 1 次** —— 那是 <see cref="NotProducible"/> 的担保方
    /// （登记 #49）：<c>REJECTED</c> 之所以可以"声明了但产不出"，前提是它真的挂在产出方的
    /// 失败分支上；否则它就是个真死码，而"进表写个理由"会把它合法化。</para>
    /// </summary>
    internal static List<string> ProducerViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();

        var hit = sources.FirstOrDefault(s => string.Equals(s.Relative, Producer, StringComparison.Ordinal));
        if (hit.Text is null) { bad.Add($"{Producer}：语料里找不到这个文件（扫描面失效？）"); return bad; }

        var code = SourceScanner.StripComments(hit.Text);

        if (!code.Contains("public RecipeCloneResult CloneRecipe(", StringComparison.Ordinal))
            bad.Add($"{Producer}：CloneRecipe 的签名不再是 `public RecipeCloneResult CloneRecipe(` "
                + "⇒ 失败档的**码**没地方放（非空返回类型的形状下，失败只能靠 `return null!` 撒谎）");

        if (code.Contains("return null!", StringComparison.Ordinal))
            bad.Add($"{Producer}：又出现 `return null!` ⇒ 「声明非空 + 压掉警告」正是改动前的形状，"
                + "消费点会以为「绝不会 null」、连 else 都不写");

        var calls = CountOccurrences(code, "AddRecipe(clone)");
        var guarded = CountOccurrences(code, "if (!AddRecipe(clone))");

        if (calls == 0)
            bad.Add($"{Producer}：找不到 `AddRecipe(clone)` ⇒ 克隆体入库这一步可能被删了（扫描面失效？）");
        else if (calls != guarded)
            bad.Add($"{Producer}：`AddRecipe(clone)` 出现 {calls} 次、而 `if (!AddRecipe(clone))` 只有 "
                + $"{guarded} 次 ⇒ 有 {calls - guarded} 处**把它返回的 bool 丢掉**了。"
                + "那个 bool 是「Id 已存在」的守卫（Recipe.Id = Guid[..8]，撞车罕见但可达）"
                + "⇒ 丢了它，撞车时界面会提示「已克隆」（撒谎）");

        // 两个工厂都必须被产出方**真的用到**。这一条是 NotProducible 那张表的**担保方**
        // （登记 #49：判据的担保方自己不能是死的）：REJECTED 之所以能"声明了但产不出"，
        // 前提是它确实挂在产出方的失败分支上 —— 否则它就是个真正没人用的死码，
        // 而"进 NotProducible 写个理由"会把它合法化。
        foreach (var (factory, issueCode) in new[]
                 {
                     ("RecipeCloneIssue.SourceMissing(", RecipeIssueCodes.SOURCE_MISSING),
                     ("RecipeCloneIssue.Rejected(", RecipeIssueCodes.REJECTED),
                 })
        {
            var used = CountOccurrences(code, factory);
            if (used != 1)
                bad.Add($"{Producer}：`{factory}` 出现 {used} 次，应为 **1** "
                    + $"⇒ 码 {issueCode} 没有挂在产出方的失败分支上（它就是个真死码，"
                    + "不许靠 NotProducible 写个理由把它合法化）");
        }

        return bad;
    }

    /// <summary>
    /// **站点必须接住失败档**：`RecipeEditorView` 恰好 **1** 处码化出口，
    /// 且有 `result.Issue is { } issue` 分支、不再有旧形状的 `if (clone != null)`。
    ///
    /// <para><b>为什么数"恰好 1"而不是"至少 1"</b>（阶段 90 的规矩）：该视图只有一个克隆站点，
    /// "至少 1"会让"接了但接在别处 / 接重了"静默通过。</para>
    /// </summary>
    internal static List<string> SiteViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();

        foreach (var file in Sites)
        {
            var hit = sources.FirstOrDefault(s => string.Equals(s.Relative, file, StringComparison.Ordinal));
            if (hit.Text is null) { bad.Add($"{file}：语料里找不到这个文件（扫描面失效？）"); continue; }

            var code = SourceScanner.StripComments(hit.Text);

            var coded = CountOccurrences(code, "ExternalText.Describe(issue)");
            if (coded != 1)
                bad.Add($"{file}：`ExternalText.Describe(issue)` 出现 {coded} 次，应为 **1**（该视图只有 1 个克隆站点）"
                    + " ⇒ 0 次 = 失败档没接（改动前就是这样：`if (clone != null)` 连 else 都没写）；"
                    + ">1 次 = 出口有第二个来源");

            if (!code.Contains("result.Issue is { } issue", StringComparison.Ordinal))
                bad.Add($"{file}：没有 `result.Issue is {{ }} issue` 这条失败档分支 ⇒ "
                    + "克隆失败时界面依旧一声不响（点了按钮什么都没发生）");

            if (code.Contains("if (clone != null)", StringComparison.Ordinal))
                bad.Add($"{file}：还留着旧形状的 `if (clone != null)` ⇒ "
                    + "那是「返回类型声明非空」时代的写法，它自带「失败档不存在」的前提");
        }

        return bad;
    }

    private static int CountOccurrences(string text, string needle)
    {
        var n = 0;
        var i = 0;
        while ((i = text.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            n++;
            i += needle.Length;
        }
        return n;
    }

    // ══════════════════════════ 判据 ══════════════════════════

    /// <summary>
    /// 主判据：**每一条失败**在**两种语言下各渲染一遍**，结果都必须来自该语言的字典模板。
    ///
    /// <para>毒化那一路：把 <c>Message</c> 换成 <see cref="Poison"/> 再渲染 —— 回落原文**没进字典**
    /// 才是"字典在决定文案"的证明。⚠ 不能拿真原文比对：本族 zh 字典值与回落原文**措辞不同**，
    /// 拿真原文只能测"今天恰好翻了没"，测不到"渲染这一半确实由字典决定"。</para>
    /// </summary>
    [Fact]
    public void EveryFailure_RendersTheDictionaryTemplate_InBothLanguages()
    {
        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));
        var hits = probe.Hits;
        Assert.True(hits.Count >= 2,
            $"只取到 {hits.Count} 条失败档（应有 2：源配方不在 / 入库被拒）—— 取数失效，下面的判定会恒真");

        var rows = new List<RenderRow>();
        var original = AppLocalization.Service.CurrentLanguage;

        try
        {
            foreach (var (lang, dict) in new[] { ("zh-CN", Zh), ("en-US", En) })
            {
                AppLocalization.Service.SwitchLanguage(lang);
                foreach (var hit in hits)
                {
                    // 毒化：保留码与实参，只把回落原文换掉。
                    var poisoned = new RecipeCloneIssue(hit.Issue.Code, Poison, hit.Issue.Args);
                    rows.Add(new RenderRow(hit.Label, lang, ExternalText.Describe(poisoned),
                        ExternalText.RecipeIssueKey(hit.Issue.Code),
                        Expected(dict, ExternalText.RecipeIssueKey(hit.Issue.Code), hit.Issue.Args),
                        hit.Issue.Message));
                }
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        JudgeReadings.Write(_out, "配方克隆失败的失败原因（#61t · recipe.issue.*）",
            rows.Select(r => $"    {r.Label,-40} {r.Language} ⇒ {r.Rendered}").ToList());

        // 自检：语言真的切动了。静默没切会让 en 那一遍渲染成中文 —— 那时下面那条期望比对
        // 已经会红，但红在"渲染 ≠ 期望"上，指错方向。
        foreach (var hit in hits)
        {
            var key = ExternalText.RecipeIssueKey(hit.Issue.Code);
            var zhExpected = Expected(Zh, key, hit.Issue.Args);
            var enExpected = Expected(En, key, hit.Issue.Args);
            var zhRow = rows.Single(r => r.Label == hit.Label && r.Language == "zh-CN");
            var enRow = rows.Single(r => r.Label == hit.Label && r.Language == "en-US");

            var expectedDiffers = zhExpected is not null && enExpected is not null
                && !string.Equals(zhExpected, enExpected, StringComparison.Ordinal);
            var renderedDiffers = !string.Equals(zhRow.Rendered, enRow.Rendered, StringComparison.Ordinal);

            Assert.True(renderedDiffers || !expectedDiffers,
                $"{hit.Label}：zh 与 en 渲染一模一样「{zhRow.Rendered}」，可两本字典的值并不相同"
                + " ⇒ 语言切换没生效（渲染没跟着当前语言走）");
        }

        var problems = RenderViolations(rows, Poison);
        Assert.True(problems.Count == 0,
            "有失败原因没走字典（登记 #61t / 阶段 98）。正确动作是补 `recipe.issue.<码小写>` 的字典键，"
            + "**不是**把裸中文串再拼回去：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **收益的正面见证**：真失败（不毒化）在**英文界面下不许再出现中文**。
    ///
    /// <para>它与主判据的见证面不重合：主判据证明"渲染由字典决定"，
    /// 这条证明"英文用户读到的东西真的不含中文" —— 后者正是 #61 那一族缺陷的用户可见症状。
    /// 用 <see cref="Cjk"/> 当尺子、不写死措辞：换文案不该打红它，字典缺键/值没翻才会。</para>
    /// </summary>
    [Fact]
    public void RealFailures_InEnglish_CarryNoChinese()
    {
        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));

        var original = AppLocalization.Service.CurrentLanguage;
        var rows = new List<(string Label, string Rendered, string Fallback)>();
        try
        {
            AppLocalization.Service.SwitchLanguage("en-US");
            foreach (var hit in probe.Hits)
                rows.Add((hit.Label, ExternalText.Describe(hit.Issue), hit.Issue.Message));
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        JudgeReadings.Write(_out, "配方克隆失败 · 英文界面下的渲染（#61t）",
            rows.Select(r => $"    {r.Label,-40} en ⇒ {r.Rendered}").ToList());

        var bad = rows.Where(r => Cjk.IsMatch(r.Rendered))
            .Select(r => $"    {r.Label}：英文界面下渲染出中文「{r.Rendered}」（回落原文是「{r.Fallback}」）"
                + " ⇒ 该语言的字典键没查到")
            .ToList();

        Assert.True(bad.Count == 0,
            "英文界面下这两档仍是中文（登记 #61t / 阶段 98）—— 用户可见的收益就落在这条上：\n"
            + string.Join("\n", bad));
    }

    /// <summary>两本字典齐 + 成对 + **占位符个数与实参个数一一对应**。</summary>
    [Fact]
    public void BothDictionaries_CarryEveryCode_AndAreActuallyPaired()
    {
        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));
        var rows = probe.Hits
            .Select(h => (ExternalText.RecipeIssueKey(h.Issue.Code), ArgCount: h.Issue.Args?.Count ?? 0))
            .ToList();
        var distinct = rows.Select(r => r.Item1).Distinct(StringComparer.Ordinal).ToList();

        Assert.True(distinct.Count >= 2,
            $"只取到 {distinct.Count} 个键（应有 2：`recipe.issue.rejected` ＋ `recipe.issue.source_missing`）"
            + " —— 取数失效，下面的判定会恒真");

        var problems = PairingViolations(rows, Zh, En);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>码表**三态闭包**：声明的码要么真的产得出，要么在 <see cref="NotProducible"/> 里写了理由。</summary>
    [Fact]
    public void DeclaredCodes_AreEitherProducedOrExplicitlyNotProducible()
    {
        var declared = DeclaredCodes();
        Assert.True(declared.Count >= 2,
            $"只反射到 {declared.Count} 个码 —— 取数失效（反射面空了，三态闭包会恒真）");
        Assert.True(NotProducible.Count >= 1,
            "NotProducible 表空了 —— 要么 REJECTED 真的变得产得出（那就该把它从表里删掉、并补一条真实见证），"
            + "要么反射面失效（那个码没被反射到）");

        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));

        // ⚠ 闭包只看**产出方真的发得出来**的那些（见 Hit.ReachedByProducer 的文档）——
        // 工厂构造的那一档只进渲染面。混在一起会让 NotProducible 那条判据自己把自己打红。
        var produced = probe.Hits
            .Where(h => h.ReachedByProducer)
            .Select(h => h.Issue.Code)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(produced.Count >= 1,
            $"没有任何一档是走真实产出路径拿到的（{probe.Hits.Count} 条命中全是工厂构造）"
            + " —— 取数退化成手搓，三态闭包会恒真");

        JudgeReadings.Write(_out, "配方克隆的码表（#61t）",
            new List<string>
            {
                $"    声明 {declared.Count} 个 / 真实产出 {produced.Count} 个 / 明确产不出 {NotProducible.Count} 个："
                + $"产出 = {string.Join(", ", produced.OrderBy(x => x, StringComparer.Ordinal))}；"
                + $"产不出 = {string.Join(", ", NotProducible.Keys.OrderBy(x => x, StringComparer.Ordinal))}",
                $"    渲染见证 {probe.Hits.Count} 条（其中工厂构造 {probe.Hits.Count(h => !h.ReachedByProducer)} 条）",
            });

        var problems = ClosureViolations(declared, produced, NotProducible);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>产出方必须**返回值带码**，且不许丢掉 <c>AddRecipe</c> 的守卫。</summary>
    [Fact]
    public void Producer_ReturnsCodedResult_AndConsumesTheAddGuard()
    {
        var problems = ProducerViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "配方克隆产出方的形状（#61t）", new List<string> { $"    {Producer}" });

        Assert.True(problems.Count == 0,
            "产出方必须给码 —— 否则那个站点换成码化出口会**收益为零**"
            + "（面绿着、用户看到的一字不变）：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>站点必须接住失败档，且不许留旧形状。</summary>
    [Fact]
    public void Site_RendersThroughTheCodedOutlet()
    {
        var problems = SiteViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "配方克隆站点的接线（#61t）",
            Sites.Select(f => $"    {f}").ToList());

        Assert.True(problems.Count == 0,
            "站点没真收口（登记 #61t / 阶段 98）—— 直通面变小不等于收益，要看产出方有没有码：\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **诚实边界正面断言**：一条**没有码**的 <c>RecipeCloneIssue</c>（字典里没有它的键）
    /// 照旧回落原文 ⇒ 「码才是收益的唯一来源」，与主判据成对。
    /// </summary>
    [Fact]
    public void IssueWithoutAKey_StillFallsBackToTheRawMessage()
    {
        var bare = new RecipeCloneIssue("NOT_A_REAL_CODE", "库里没有这个码的回落原文", new object[] { "x" });
        Assert.Equal(bare.Message, ExternalText.Describe(bare));

        var coded = RecipeCloneIssue.SourceMissing("no-such-recipe");
        Assert.NotEqual(coded.Message, ExternalText.Describe(coded));
    }

    /// <summary>
    /// 负控 —— 每组都要求"该红的红、该绿的绿"（本项目对负控的一贯要求）。
    /// 注入全部走**纯函数**（合成语料 / 内存副本），不动真仓库。
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        var zh = Zh;
        var en = En;
        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));
        var hits = probe.Hits;

        var keys = hits.Select(h => (Key: ExternalText.RecipeIssueKey(h.Issue.Code),
            ArgCount: h.Issue.Args?.Count ?? 0)).ToList();
        var key0 = keys[0].Key;

        // ── NC0 · 正控（**必须合成**：外部负控会让它跟着红）──
        Assert.Empty(PairingViolations(keys, zh, en));

        var clean = hits.Select(h => new RenderRow(h.Label, "zh-CN",
            Expected(zh, ExternalText.RecipeIssueKey(h.Issue.Code), h.Issue.Args)!,
            ExternalText.RecipeIssueKey(h.Issue.Code),
            Expected(zh, ExternalText.RecipeIssueKey(h.Issue.Code), h.Issue.Args),
            h.Issue.Message)).ToList();
        Assert.Empty(RenderViolations(clean, Poison));
        Assert.All(clean, r => Assert.NotNull(r.Expected));

        // ── NC1 · 成对性 ──
        var enCopy1 = new Dictionary<string, string>(en, StringComparer.Ordinal) { [key0] = zh[key0] };
        Assert.Contains(PairingViolations(keys, zh, enCopy1), v => v.Contains("中文字符", StringComparison.Ordinal));

        var enCopy2 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy2.Remove(key0);
        Assert.Contains(PairingViolations(keys, zh, enCopy2), v => v.Contains("缺键", StringComparison.Ordinal));

        // 占位符：本族**两个码都是 1 实参** ⇒ 塞第二个占位符即个数不符。
        // ⚠ 不能挑"本来就 1 占位符"的键去塞 {0}：个数照样对得上 ⇒ 注入无效
        //（阶段 97 的负控第一版就栽在这里：负控绿着，证明不了判据在盯占位符）。
        var zhCopy = new Dictionary<string, string>(zh, StringComparer.Ordinal) { [key0] = zh[key0] + " {1}" };
        Assert.Contains(PairingViolations(keys, zhCopy, en), v => v.Contains("占位符", StringComparison.Ordinal));

        // ── NC2 · 渲染对照：三档都要报 ──
        // ① 毒化冒头 ⇒ 点名"没被查到"
        Assert.Contains(
            RenderViolations(new[] { new RenderRow("演示", "en-US", "x" + Poison, key0, "期望串", "回落原文") }, Poison),
            v => v.Contains("中文回落原文", StringComparison.Ordinal));

        // ② 期望串对了、渲染换了别的说法 ⇒ 点名"有别的来源"
        Assert.Contains(
            RenderViolations(new[] { new RenderRow("演示", "en-US", "换个说法", key0, "期望串", "回落原文") }, Poison),
            v => v.Contains("有别的来源", StringComparison.Ordinal));

        // ③ 该语言字典里没这个键 ⇒ 点名字典缺键（不是静默通过）
        Assert.Contains(
            RenderViolations(new[] { new RenderRow("演示", "en-US", "回落原文", key0, null, "回落原文") }, Poison),
            v => v.Contains("字典里没有", StringComparison.Ordinal));

        // ── NC3 · 三态闭包：**四档都要报** ──
        var declared = DeclaredCodes();
        var produced = new[] { RecipeIssueCodes.SOURCE_MISSING };
        Assert.Empty(ClosureViolations(declared, produced, NotProducible));

        // ① 声明了、产不出、也不在表里 ⇒ 死码
        Assert.Contains(
            ClosureViolations(declared, produced, new Dictionary<string, string>(StringComparer.Ordinal)),
            v => v.Contains("死码", StringComparison.Ordinal));

        // ② 产出了没声明 ⇒ 漏登记
        Assert.Contains(
            ClosureViolations(declared, produced.Append("NOT_A_REAL_CODE"), NotProducible),
            v => v.Contains("没声明", StringComparison.Ordinal));

        // ③ 表里说产不出、其实产得出 ⇒ 豁免过期（登记 #54 的病）
        Assert.Contains(
            ClosureViolations(declared, produced.Append(RecipeIssueCodes.REJECTED), NotProducible),
            v => v.Contains("过期", StringComparison.Ordinal));

        // ④ 表里记了一个根本没声明的码 ⇒ 抄写漂移
        var drifted = new Dictionary<string, string>(NotProducible, StringComparer.Ordinal)
        {
            ["NOT_A_REAL_CODE"] = "演示",
        };
        Assert.Contains(
            ClosureViolations(declared, produced, drifted),
            v => v.Contains("抄写漂移", StringComparison.Ordinal));

        // ── NC4 · 产出方（合成语料，**目标文件必须在**）──
        var all = AppSourceScanner.ScanAllProductSources();
        static (string Relative, string Text)[] Corpus(string body)
            => new[] { (Producer, body) };

        const string compliant = """
            public RecipeCloneResult CloneRecipe(string recipeId, string newName)
            {
                var source = _recipes.GetValueOrDefault(recipeId);
                if (source == null) return RecipeCloneResult.Failed(RecipeCloneIssue.SourceMissing(recipeId));
                var clone = new Recipe { Name = newName };
                if (!AddRecipe(clone)) return RecipeCloneResult.Failed(RecipeCloneIssue.Rejected(clone.Id));
                return RecipeCloneResult.Ok(clone);
            }
            """;
        Assert.Empty(ProducerViolations(Corpus(compliant)));

        // ① 退回「声明非空 + return null!」⇒ 两条都点名
        var regressed = ProducerViolations(Corpus("""
            public Recipe CloneRecipe(string recipeId, string newName)
            {
                if (recipeId is null) return null!;
                return new Recipe();
            }
            """));
        Assert.Contains(regressed, v => v.Contains("public RecipeCloneResult CloneRecipe(", StringComparison.Ordinal));
        Assert.Contains(regressed, v => v.Contains("return null!", StringComparison.Ordinal));
        Assert.Contains(regressed, v => v.Contains("AddRecipe(clone)", StringComparison.Ordinal));

        // ② 留着那个 if、却又多写一处裸调用 ⇒ 点名"丢掉了 bool"（"包含某个串"式的判据会漏掉这条）
        var dropped = ProducerViolations(Corpus("""
            public RecipeCloneResult CloneRecipe(string recipeId, string newName)
            {
                var clone = new Recipe { Name = newName };
                AddRecipe(clone);
                if (!AddRecipe(clone)) return RecipeCloneResult.Failed(RecipeCloneIssue.Rejected(clone.Id));
                return RecipeCloneResult.Ok(clone);
            }
            """));
        Assert.Contains(dropped, v => v.Contains("丢掉", StringComparison.Ordinal));

        // ③ 语料里缺这个文件 ⇒ 必须报"找不到"（扫描面失效时会静默全绿）
        Assert.Contains(
            ProducerViolations(Array.Empty<(string, string)>()),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ④ 注释里写的反例不算（规则不许被自己的文档满足，登记 #48）
        Assert.Empty(ProducerViolations(Corpus(compliant
            + "\n// 反例（不许这么写）：return null!; 以及裸 AddRecipe(clone);")));

        // ── NC5 · 站点接线（注入 + 真语料对照）──
        Assert.Empty(SiteViolations(all));

        // ① 失败档没接（旧形状）⇒ 两条都点名
        var unwired = SiteViolations(new[] { (Sites[0],
            "var clone = _recipeManager.CloneRecipe(a, b);\nif (clone != null) { ShowStatus(clone.Name); }") });
        Assert.Contains(unwired, v => v.Contains("出现 0 次", StringComparison.Ordinal));
        Assert.Contains(unwired, v => v.Contains("if (clone != null)", StringComparison.Ordinal));

        // ② 接了但**没走码化出口**（自己拼原文）⇒ 点名出口 0 次
        Assert.Contains(
            SiteViolations(new[] { (Sites[0],
                "if (result.Issue is { } issue) { ShowStatus(issue.Message); }") }),
            v => v.Contains("出现 0 次", StringComparison.Ordinal));
    }
}
