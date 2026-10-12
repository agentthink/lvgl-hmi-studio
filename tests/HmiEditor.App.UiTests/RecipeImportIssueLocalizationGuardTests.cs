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
/// 「**配方 CSV 导入的问题必须由字典决定、且必须真的摆到界面上**」的守卫 ——
/// 登记表 §4 <b>#61f</b>（阶段 100，本族第三刀；第一刀 CSV 变量导入 = M12／阶段 88，
/// 第二刀配方克隆 = M16／阶段 98）。
///
/// <para><b>它守的是哪一类缺陷</b>：<c>RecipeManager.ImportCsv</c> 改动前的签名是
/// <c>public int ImportCsv(string csvContent)</c> —— 那个 <c>int</c> 只说"成了几条"，
/// 而方法里藏着 <b>7 处静默</b>：<c>return 0</c> / <c>continue</c> 了事。最要命的两处是</para>
/// <list type="number">
///   <item><b>非数字的值静默丢掉一个配方项</b>（<c>double.TryParse</c> 失败 ⇒ 走 <c>else</c>
///     分支什么都不做）—— 界面照样报「成功导入 N 个配方」，用户不知道自己的参数少了一格；</item>
///   <item><b><c>AddRecipe</c> / <c>UpdateRecipe</c> 的 <c>bool</c> 被丢掉</b>，
///     而 <c>count++</c> 照样执行 ⇒ 「入库被拒」时计数**多算一条**。
///     ⚠ 这与 <c>CloneRecipe</c>（登记 <b>#61t②</b>）是**完全同一形状**的缺陷，
///     出现在**同一个文件**里 —— 两次都是「返回 <c>bool</c> 的守卫被当语句调用」。</item>
/// </list>
///
/// <para><b>处置 = 返回值带码</b>（模板见登记 #61f「模板已立」）：
/// <see cref="RecipeImportResult"/> ＋ <see cref="RecipeImportIssue"/> ＋
/// <c>ExternalText.Describe(RecipeImportIssue)</c>，键统一走
/// <c>recipe.import.issue.&lt;码小写&gt;</c>。
/// ⚠ 它**不**走 <c>ExternalText.FailureReason</c> —— 那条路收的是 <c>Exception</c>，
/// 而本族产出方**一处都不抛**（阶段 87/88/98 三次实测同一结论）。</para>
///
/// <para><b>⚙ 为什么单开 <c>recipe.import.issue.</c> 而不用既有的两族</b>：
/// <c>CsvImportIssueCodes</c>（M12）的 5 条措辞是**变量导入专属**（点名 Name/DataType/Address
/// 三列、<c>ROW_*_NAME</c> 指变量名）；<c>RecipeIssueCodes</c>（M16）描述**克隆**两个失败档。
/// 按**产出方分家**是本仓库既有约定（<c>package.issue.</c> / <c>system.issue.</c> /
/// <c>csv.issue.</c> / <c>recipe.issue.</c> …），复用要往里塞语义不符的码，
/// 反而把那一族的读数含义搅糊。⇒ 一条前缀一张码表的不变量保持住。</para>
///
/// <para><b>⚙ 复用 M16 的四件纯函数、不抄第三份</b>（<c>RecipeCloneIssueLocalizationGuardTests</c>）：
/// <see cref="RecipeCloneIssueLocalizationGuardTests.RenderRow"/> /
/// <c>RenderViolations</c> / <c>PairingViolations</c> / <c>ClosureViolations</c>。
/// 两族的成对性、三态闭包、渲染对照规则**一字不差**（键在 ＋ en 不含中文 ＋ 两侧不同 ＋
/// 占位符数 == 实参数；渲染必须等于该语言字典填参后的串）；抄第二份/第三份必然漂移，
/// 而漂移的表现正是"一个守卫绿、另一个红，看不出哪边对"（M12 起就是这么定的）。
/// ⚠ 唯一各算各的是「期望串」：本族的行号前缀在 <c>Describe</c> 里**加**，
/// 所以 <see cref="Expected"/> 必须自己套 <c>csv.line.prefix</c>。</para>
///
/// <para><b>⚙ 本类会切换进程级全局语言 ⇒ 必须挂 <c>[Collection("Language-global")]</c></b>
/// （<c>DisableParallelization = true</c>）—— 否则与大队并行时会打红**别的**测试类，
/// 而症状随字典内容变、极难归因。判据是
/// <c>LocalizationRefreshUiTests.EveryTestClassThatSwitchesLanguage…</c>；切换必然 <c>finally</c> 还原。</para>
///
/// <para><b>诚实边界</b>：</para>
/// <list type="number">
///   <item><see cref="RecipeImportIssueCodes.RECIPE_NOT_APPLIED"/> **没有"走真实产出路径"的见证**
///     —— 它要靠新建配方的 <c>Id</c>（<c>Guid[..8]</c>，2³² 里撞一次）真的撞上才发生，
///     是**概率事件、不可确定性复现**，而主判据不许有间发性（一次绿等于零信息）⇒ 它进
///     <see cref="NotProducible"/> 并写明理由，渲染见证由**工厂**
///     <c>RecipeImportIssue.RecipeNotApplied</c> 提供（与 M16 的 <c>REJECTED</c> 同一处置）。
///     **那个码必须存在**：守卫正是为那一天写的，改动前它被丢掉后**报成功**；
///     而"那个工厂确实挂在产出方的失败分支上"交给 <see cref="ProducerViolations"/> 钉住
///     —— 否则 <see cref="NotProducible"/> 会把一个**真死码**合法化（登记 #49）。</item>
///   <item>只断「渲染等于该语言的字典模板」，断不了**翻译质量**；</item>
///   <item>本判据**不断**"用户是否真的看见了" —— 那由 <see cref="ConsumerViolations"/> 与
///     <see cref="IssueBarPlacementViolations"/> 的**源码面**证据兜着
///     （消费点必须读 <c>result.Issues</c> ＋ 走渲染出口 ＋ 状态条不许嵌进 <c>ScrollViewer</c>），
///     不是端到端 UI 断言。</item>
/// </list>
///
/// <para><b>⚙ 本类外带一条「清单不许漂」的判据</b>：
/// <see cref="PrefixDocumentationViolations"/> 钉住
/// 「<c>ExternalText</c> 里枚举到的每一条 <c>*KeyPrefix</c> 都必须出现在它自己的类注释
/// 「键约定」清单里」。立它的直接原因是**同一件事已经漏过两次**：
/// M15（<c>user.issue.</c> / <c>sim.issue.</c>，阶段 97）与 M16（<c>recipe.issue.</c>，阶段 98）
/// 都是"加了族却忘了这份清单"，而当时**没有任何判据看着它** ⇒ 一直没红（登记 #61w 同类：
/// 手抄的清单没人看着就会漂）。</para>
/// </summary>
[Collection("Language-global")]
public class RecipeImportIssueLocalizationGuardTests
{
    private readonly ITestOutputHelper _out;

    public RecipeImportIssueLocalizationGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>毒化记号：拿它当"中文回落原文"，凡在渲染里冒头就说明字典键没被查到。</summary>
    internal const string Poison = "<<中文回落原文未被使用>>";

    /// <summary>CJK 汉字 —— 用来抓"en-US 的值其实没翻"，也用来判"英文界面下这一半是不是还冒中文"。</summary>
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>产出方：本刀把 7 处静默改成每处一条码（<c>RecipeManager.ImportCsv</c>）。</summary>
    internal const string Producer = "src/HmiEditor.Core/Services/RecipeManager.cs";

    /// <summary>站点：本刀给它的导入路径接上"明细摆到界面上"（改动前只有 <c>if (count &gt; 0)</c>）。</summary>
    internal static readonly string[] Sites =
    {
        "src/HmiEditor.App/Views/RecipeEditorView.axaml.cs",
    };

    /// <summary>问题状态条所在的 XAML（⚠ 用 <c>AppSourceScanner.ScanAppXaml()</c> 取，
    /// 那份语料的路径**相对 App 根**（<c>Views/…</c>），不是相对仓库根 —— 两套约定混用会静默"找不到文件"）。</summary>
    internal const string IssueBarXaml = "Views/RecipeEditorView.axaml";

    /// <summary>行号前缀模板的键（<b>与 <c>Lang.CsvLinePrefix</c> 必须是同一个值</b>；
    /// 一致性由 <see cref="LinePrefixKey_MatchesTheLangConstant"/> 钉住）。</summary>
    internal const string LinePrefixKey = "csv.line.prefix";

    /// <summary>
    /// 码表的**第三态** —— <c>声明了</c> ∪ <c>真实产得出</c> 之外的合法去处：**现在产不出、
    /// 但必须留着**。与 M11/M12/M14/M16 的处置同一形状。
    ///
    /// <para>⚠ 这张表是**差异表、不是豁免**：表内条目一旦**真的变得产得出**，判据会红，
    /// 逼维护者删掉这一条（否则它会退化成"能把缺陷合法化"的过期字段 —— 登记 #54 的病）。
    /// 表外多一条、少一条同样红。</para>
    /// </summary>
    internal static readonly Dictionary<string, string> NotProducible = new(StringComparer.Ordinal)
    {
        [RecipeImportIssueCodes.RECIPE_NOT_APPLIED] =
            "本行没进管理器：要靠新建配方的 Id（Guid[..8]）真的撞上既有 Id 才发生，"
            + "是概率事件、不可确定性复现（主判据不许有间发性：一次绿等于零信息）"
            + "⇒ 渲染见证由工厂 RecipeImportIssue.RecipeNotApplied 提供。"
            + "码必须留着：改动前 AddRecipe / UpdateRecipe 的 bool 被丢掉、count 照样 ++ "
            + "⇒ 撞车时界面报「成功导入 N 个配方」而实际少一条（与 #61t② CloneRecipe 同一形状）。",
    };

    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    // ══════════════════════════ 取数（同源出口 —— 负控与 M18 都复用它） ══════════════════════════

    /// <summary>
    /// 反射枚举 <see cref="RecipeImportIssueCodes"/> 里声明的全部码。
    ///
    /// <para><b>是同源出口</b>：本文件的多条判据与 <c>LocalizationKeyReachabilityGuardTests</c>
    /// 的 <b>M18</b> 都调它，不各写一份反射（抄两份必然漂移，漂移的表现是"一个守卫绿、另一个红"）。</para>
    ///
    /// <para>⚠ <c>RecipeImportIssueCodes</c> 是 <c>public</c>，所以不带 <c>BindingFlags.NonPublic</c>
    /// 也对 —— 但"反射到 0 个码"会让三态闭包**恒真**，所以下面每条判据都带下界断言兜底。</para>
    /// </summary>
    internal static List<string> DeclaredCodes()
        => typeof(RecipeImportIssueCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    /// <summary>一次取到的失败：人读的标签 + 那个 <see cref="RecipeImportIssue"/> +
    /// **它是不是由产出方真的产出来的**（工厂构造的那一档只进渲染面，见 <see cref="ProbeFailures"/>）。</summary>
    internal sealed record Hit(string Label, RecipeImportIssue Issue, bool ReachedByProducer);

    /// <summary>一次取数的结果：**命中** + **退化**。
    /// <see cref="Degraded"/> 是本族特有的收口 —— 手感与 M16 逐条比对式断言一致，
    /// 但这里产出方有 5 条路径，逐个断言会让"哪条路退化了"从红消息里消失。</summary>
    internal sealed record Probe(List<Hit> Hits, List<string> Degraded);

    /// <summary>「退化」的统一红消息：点名**哪条路**没给出带码的失败档、以及正确动作。
    /// 记成数据再判，红才自带信息（直接断言 <c>Hits.Count == n</c> 时看不出该修产出方还是修站点）。</summary>
    internal static string DegradedMessage(IReadOnlyList<string> degraded)
        => "这几条路径本该返回**带码的问题**，却没有 ⇒ 产出方退化了（登记 #61f / 阶段 100）。"
           + "正确动作是让产出方在每一处失败**产一条 `RecipeImportIssue`**（码 + 中文回落原文），"
           + "**不是**把它改回 `return 0` / `continue` 了事：\n  - " + string.Join("\n  - ", degraded);

    /// <summary>
    /// 五条**可确定性复现**的真实产出路径 ＋ 一条工厂构造 → 逐个走一遍
    /// <see cref="RecipeManager.ImportCsv"/>。
    ///
    /// <para>⚠ 必须真的调产出方（手搓 <see cref="RecipeImportIssue"/> 的话，
    /// "码表与产出方脱节"这件事就永远测不到）。而且**每条路径的输入都必须是"用户真会喂进来的
    /// 那份 CSV"**，不是为触发而编造的 —— 下面 5 条依次对应：空文件 / 表头列数不足 /
    /// 坏行 / 空列名 / 某一格不是数字。最后一条是**本项的头号症状**（配方项静默少一个）。</para>
    /// </summary>
    internal static Probe ProbeFailures()
    {
        var hits = new List<Hit>();
        var degraded = new List<string>();

        void Reach(string label, Func<RecipeImportResult> run, string expectedCode)
        {
            RecipeImportResult result;
            try
            {
                result = run();
            }
            catch (Exception ex)
            {
                degraded.Add($"{label}：产出方**抛了异常**（{ex.GetType().Name}: {ex.Message}） "
                    + "⇒ 契约是「返回 (导入条数, 问题表)」，产出方一处都不该抛");
                return;
            }

            var matched = result.Issues
                .FirstOrDefault(i => string.Equals(i.Code, expectedCode, StringComparison.Ordinal));

            if (matched is null)
            {
                degraded.Add($"{label}：没拿到 {expectedCode} —— 实际拿到 "
                    + $"[{string.Join(", ", result.Issues.Select(i => i.Code))}]（共 {result.Issues.Count} 条）"
                    + " ⇒ 那一处静默又回来了（改动前它正是 `return 0` / `continue` 了事）");
                return;
            }

            hits.Add(new Hit(label, matched, ReachedByProducer: true));
        }

        // ① 空内容 ⇒ 连表头带数据都不到两行（文件级，无行号）。
        Reach("ImportCsv(空内容)", () => new RecipeManager().ImportCsv(""),
            RecipeImportIssueCodes.TOO_FEW_LINES);

        // ② 表头只有两列 ⇒ 配方 CSV 的前三列（名/版本/分组）认不出（文件级，实参 = 表头原文）。
        Reach("ImportCsv(表头只有两列)", () => new RecipeManager().ImportCsv("A,B\n1,2\n"),
            RecipeImportIssueCodes.NO_HEADER_COLUMNS);

        // ③ 数据行列数不足 3 ⇒ 取不到配方名（行级）。
        Reach("ImportCsv(坏行)", () => new RecipeManager().ImportCsv(
                "RecipeName,RecipeVersion,GroupName,v1\n坏行\n"),
            RecipeImportIssueCodes.ROW_COLUMNS_MISSING);

        // ④ 表头里有一个空列名 ⇒ 那一列的值整列跳过（行级，实参 = 列号）。
        Reach("ImportCsv(空列名)", () => new RecipeManager().ImportCsv(
                "RecipeName,RecipeVersion,GroupName,,v2\n配方1,1,温度,1.0,2.0\n"),
            RecipeImportIssueCodes.EMPTY_COLUMN_NAME);

        // ⑤ ⚠ **本项的头号症状**：某一格写成 abc ⇒ 那个**配方项**被静默丢掉，
        //    而改动前界面照样报「成功导入 1 个配方」（实参 = 变量名 / 原值）。
        Reach("ImportCsv(值不是数字)", () => new RecipeManager().ImportCsv(
                "RecipeName,RecipeVersion,GroupName,v1\n配方1,1,温度,abc\n"),
            RecipeImportIssueCodes.VALUE_NOT_A_NUMBER);

        // ⑥ 入库被拒：概率档（见 NotProducible）⇒ 由**工厂**构造。
        //    这样它的码 → 键 → 两本字典仍被完整见证，只是"能不能真撞上"不在本判据的能力范围内。
        hits.Add(new Hit("RecipeImportIssue.RecipeNotApplied（工厂构造）",
            RecipeImportIssue.RecipeNotApplied(2, "配方1"), ReachedByProducer: false));

        return new Probe(hits, degraded);
    }

    /// <summary>某本字典里这个键**填上实参、套上行号前缀**之后的串；键（或前缀键）不在字典里返回 <c>null</c>。</summary>
    internal static string? Expected(Dictionary<string, string> dict, string key, IReadOnlyList<object>? args, int line)
    {
        if (!dict.TryGetValue(key, out var body)) return null;

        if (args is { Count: > 0 } a)
            body = string.Format(CultureInfo.InvariantCulture, body, a.ToArray());

        if (line > 0)
        {
            // ⚠ 行号前缀在 App 侧渲染时加（Line 是**结构化字段**、刻意不进 Message）
            //   ⇒ 期望串必须自己套，否则"行号这一维"没人管。
            if (!dict.TryGetValue(LinePrefixKey, out var prefix)) return null;
            body = string.Format(CultureInfo.InvariantCulture, prefix, line, body);
        }

        return body;
    }

    // ══════════════════════════ 判据逻辑（纯函数 —— 负控直接调它们） ══════════════════════════

    /// <summary>
    /// **产出方不许退化**：<c>ImportCsv</c> 必须返回带码的 <see cref="RecipeImportResult"/>、
    /// 6 条工厂各恰好挂在 1 处失败分支上、且**不许再把 <c>AddRecipe</c> / <c>UpdateRecipe</c>
    /// 的返回值当语句丢掉**。
    ///
    /// <para><b>为什么最后一条非有不可</b>：改动前 <c>count++</c> 与"入库成功"是**脱钩的**
    /// —— <c>AddRecipe(recipe);</c> 丢掉 <c>bool</c>，于是入库被拒时计数多算一条、
    /// 而没有任何渠道说出来。它与 <c>CloneRecipe</c>（#61t②）**同形同在同一个文件里**，
    /// 所以这条判据用"**语句形式的裸调用**"当形状签名（正则锚在行的首尾），
    /// 而不是判"包含某个串" —— 后者会让"留着那个 <c>if (!applied)</c> 却又多写一处裸调用"通过。</para>
    ///
    /// <para>剥注释是必须的：讲规则的注释里会写出反例（登记 #48 踩过三次）。
    /// 另外"6 条工厂各恰好 1 次"是 <see cref="NotProducible"/> 那张表的**担保方**
    /// （登记 #49：判据的担保方自己不能是死的）—— <c>RECIPE_NOT_APPLIED</c> 之所以可以
    /// "声明了但产不出"，前提是它真的挂在产出方的失败分支上；否则它就是个真死码，
    /// 而"进表写个理由"会把它合法化。</para>
    /// </summary>
    internal static List<string> ProducerViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();

        var hit = sources.FirstOrDefault(s => string.Equals(s.Relative, Producer, StringComparison.Ordinal));
        if (hit.Text is null) { bad.Add($"{Producer}：语料里找不到这个文件（扫描面失效？）"); return bad; }

        var code = SourceScanner.StripComments(hit.Text);

        if (!code.Contains("public RecipeImportResult ImportCsv(", StringComparison.Ordinal))
            bad.Add($"{Producer}：ImportCsv 的签名不再是 `public RecipeImportResult ImportCsv(` "
                + "⇒ 失败档的**码**没地方放（`int` 的形状下，7 处静默只能靠 `return 0` / `continue` 盖住）");

        if (code.Contains("public int ImportCsv(", StringComparison.Ordinal))
            bad.Add($"{Producer}：又出现 `public int ImportCsv(` ⇒ 那个 int 只说「成了几条」，"
                + "说不出「坏在哪几行、为什么」—— 正是改动前的形状");

        foreach (var factory in new[]
                 {
                     "RecipeImportIssue.TooFewLines(",
                     "RecipeImportIssue.NoHeaderColumns(",
                     "RecipeImportIssue.RowColumnsMissing(",
                     "RecipeImportIssue.EmptyColumnName(",
                     "RecipeImportIssue.ValueNotANumber(",
                     "RecipeImportIssue.RecipeNotApplied(",
                 })
        {
            var used = CountOccurrences(code, factory);
            if (used != 1)
                bad.Add($"{Producer}：`{factory}` 出现 {used} 次，应为 **1** ⇒ 那一处失败又退回了静默"
                    + "（每一条码都必须恰好挂在产出方的一处失败分支上）");
        }

        var added = CountOccurrences(code, "issues.Add(RecipeImportIssue.");
        if (added < 6)
            bad.Add($"{Producer}：只有 {added} 处 `issues.Add(RecipeImportIssue.`（要求 ≥ 6）"
                + " ⇒ 有失败档只构造了 issue 却没进问题表（用户照样看不到）");

        var discarded = DiscardedApplyCall.Matches(code).Count;
        if (discarded > 0)
            bad.Add($"{Producer}：还有 {discarded} 处把 `AddRecipe(recipe);` / `UpdateRecipe(recipe);` "
                + "当**语句**调用 ⇒ 那个 `bool` 又被丢掉了（「入库被拒」这条路重新变静默，"
                + "而 `count++` 照样执行 ⇒ 计数多算一条；与 #61t② CloneRecipe 同一形状）。"
                + "正确写法：`var applied = existing != null ? UpdateRecipe(recipe) : AddRecipe(recipe);` "
                + "＋ `if (!applied) {{ issues.Add(RecipeImportIssue.RecipeNotApplied(...)); continue; }}`");

        return bad;
    }

    /// <summary>「裸语句形式的入库调用」—— 形状签名：整行只有 <c>AddRecipe(recipe);</c> / <c>UpdateRecipe(recipe);</c>。</summary>
    private static readonly Regex DiscardedApplyCall =
        new(@"(?m)^[ \t]*(Add|Update)Recipe\(recipe\);[ \t]*$", RegexOptions.Compiled);

    /// <summary>
    /// **站点必须接住问题表**：<c>RecipeEditorView</c> 恰好 **1** 处导入接线
    /// （<c>ShowCsvIssues(result.Issues)</c>），且渲染只走 <c>ExternalText.Describe</c>。
    ///
    /// <para><b>为什么数"恰好 1"而不是"至少 1"</b>（阶段 90 的规矩）：该视图只有一个导入站点，
    /// "至少 1"会让"接了但接在别处 / 接重了"静默通过。</para>
    ///
    /// <para><b>为什么还要禁 <c>.Message</c> 直上屏</b>：<see cref="RecipeImportIssue.Message"/>
    /// 是**中文回落原文**、不是文案。直接把它塞进 <c>TxtCsvIssues.Text</c> 会让英文界面下
    /// 中英混排 —— 那正是本族缺陷的用户可见症状，只是换了个位置发生。</para>
    /// </summary>
    internal static List<string> ConsumerViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();

        foreach (var file in Sites)
        {
            var hit = sources.FirstOrDefault(s => string.Equals(s.Relative, file, StringComparison.Ordinal));
            if (hit.Text is null) { bad.Add($"{file}：语料里找不到这个文件（扫描面失效？）"); continue; }

            var code = SourceScanner.StripComments(hit.Text);

            if (!code.Contains("result.Issues", StringComparison.Ordinal))
                bad.Add($"{file}：不再读 `result.Issues` ⇒ 问题明细又被丢掉了"
                    + "（改动前那一半正是「只拿 int / 只显示条数」）");

            if (!code.Contains("ExternalText.Describe", StringComparison.Ordinal))
                bad.Add($"{file}：没有再走 `ExternalText.Describe` ⇒ 问题会以中文回落原文直接上屏");

            var wired = CountOccurrences(code, "ShowCsvIssues(result.Issues)");
            if (wired != 1)
                bad.Add($"{file}：`ShowCsvIssues(result.Issues)` 出现 {wired} 次，应为 **1**"
                    + "（该视图只有 1 个导入站点）⇒ 0 次 = 明细没接线（用户所见一字不变）；"
                    + ">1 次 = 有第二个来源");

            if (NoDescribeInBar.IsMatch(code))
                bad.Add($"{file}：`TxtCsvIssues.Text` 直接取了 `.Message` ⇒ 那是**中文回落原文**，"
                    + "英文界面下会中英混排（必须经 `ExternalText.Describe` 渲染）");
        }

        return bad;
    }

    /// <summary><c>TxtCsvIssues.Text = … .Message …</c> 这种"绕过渲染出口"的赋值。</summary>
    private static readonly Regex NoDescribeInBar =
        new(@"TxtCsvIssues\.Text\s*=[^;]*\.Message", RegexOptions.Compiled);

    /// <summary>
    /// **问题状态条不许嵌进 <c>ScrollViewer</c>** —— 那是个已经量过的 headless 布局死区。
    ///
    /// <para><b>为什么要一条静态判据守着这件事</b>：本仓库实测过
    /// 「<c>ScrollViewer</c>（垂直可滚动）＋ 多行 <c>TextWrapping.Wrap</c> 的 <c>TextBlock</c>」
    /// 在 <c>Avalonia.Headless</c> 下**首次布局不收敛、直接挂死**（
    /// <c>docs/2026-09-22-headless-scrollviewer-wrap-hang.md</c> 用例 6：把它换成 <c>Border</c> 即收敛）。
    /// 这个状态条恰好是"多行 Wrap 文本 ＋ 可变长度"，是**最容易被人顺手挪进 ScrollViewer**
    /// 的形状（"放不下就让它滚"是本能反应）⇒ 判据必须挡在源码面。</para>
    ///
    /// <para>⚠ 输入面是 <b><c>.axaml</c></b> 语料，路径相对 App 根；判断方式是"切到
    /// <c>CsvIssueBar</c> 之前，<c>&lt;ScrollViewer</c> 的开标签比闭标签多" ⇒ 说明它落在
    /// 一个未闭合的 <c>ScrollViewer</c> 里。<b>先剥 XML 注释</b>：本文件自己的注释里
    /// 就写着 <c>ScrollViewer</c> 这个词（注册 #48：判据不许被自己的文档满足）。</para>
    /// </summary>
    internal static List<string> IssueBarPlacementViolations(
        IEnumerable<(string Relative, string Text)> xamlSources)
    {
        var bad = new List<string>();

        var hit = xamlSources.FirstOrDefault(s =>
            s.Relative.EndsWith(IssueBarXaml, StringComparison.Ordinal));
        if (hit.Text is null) { bad.Add($"{IssueBarXaml}：语料里找不到这个文件（扫描面失效？）"); return bad; }

        var text = StripXmlComments(hit.Text);
        var bar = text.IndexOf("CsvIssueBar", StringComparison.Ordinal);
        if (bar < 0)
        {
            bad.Add($"{IssueBarXaml}：找不到 `CsvIssueBar` ⇒ 问题状态条被删了/改名了"
                + "（判据自己也就失效了 —— 它必须与前一条接线判据同时成立）");
            return bad;
        }

        // ① 状态条本身必须是个 Border（不是 ScrollViewer / 别的容器）——
        //    找到它所在的那一行，看开标签名。
        var lineStart = text.LastIndexOf('\n', Math.Max(bar - 1, 0)) + 1;
        var lineEnd = text.IndexOf('\n', bar);
        var line = lineEnd < 0 ? text[lineStart..] : text[lineStart..lineEnd];
        if (!line.Contains("<Border", StringComparison.Ordinal))
            bad.Add($"{IssueBarXaml}：`CsvIssueBar` 不在 `<Border` 行上（实际那一行是「{line.Trim()}」）"
                + " ⇒ 如果它被挪进了 `ScrollViewer`，headless 下首次布局不收敛、会挂死");

        // ② 祖先里不许有未闭合的 ScrollViewer。
        var before = text[..bar];
        var opens = CountOccurrences(before, "<ScrollViewer");
        var closes = CountOccurrences(before, "</ScrollViewer>");
        if (opens > closes)
            bad.Add($"{IssueBarXaml}：`CsvIssueBar` 落在**未闭合的 `ScrollViewer` 里面**"
                + $"（之前开 {opens} 个、闭 {closes} 个）⇒ 「ScrollViewer 垂直可滚动 ＋ "
                + "多行 Wrap TextBlock」= headless 首次布局不收敛的触发条件，会挂死"
                + "（docs/2026-09-22-headless-scrollviewer-wrap-hang.md 用例 6：`ScrollViewer → Border` 即收敛）");

        return bad;
    }

    /// <summary>剥 <c>&lt;!-- … --&gt;</c>。⚠ 只用于"数标签"这类形状判断；
    /// 与 <c>SourceScanner.StripComments</c>（C# 的 <c>//</c> / <c>/* */</c>）**不是**一回事，
    /// 两者不可互相替代。</summary>
    internal static string StripXmlComments(string xaml)
        => Regex.Replace(xaml, "<!--.*?-->", string.Empty, RegexOptions.Singleline);

    /// <summary>
    /// 「**<c>ExternalText</c> 里声明的每一条键前缀都必须出现在它自己的类注释「键约定」清单里**」
    /// —— 这一类"加了族忘了清单"已经漏过**两次**（M15 的 <c>user.issue.</c>/<c>sim.issue.</c>
    /// 阶段 97、M16 的 <c>recipe.issue.</c> 阶段 98），而立这条判据之前**没有任何判据看着它**
    /// ⇒ 两次都没红（登记 #61w 同类：手抄的清单没人核就会漂）。
    ///
    /// <para><b>⚠ 输入面只许是"类注释那一段"，不许是整个文件</b>（判据四条铁律 ③：
    /// 输入面不得含**定义面**）：前缀字面量**本来就在 <c>const</c> 声明里出现**，
    /// 把整份文件当输入面 ⇒ 每条前缀都"在清单里"，判据恒真。切片的正确性由
    /// <see cref="ClassCommentSlice"/> 与主判据里的
    /// <c>Assert.DoesNotContain("internal const string", slice)</c> 一起钉住。</para>
    ///
    /// <para>🩸 <b>锚点必须是「清单条目的形状」，不能是"这个名字出现过"</b>（阶段 100 负控 E 实测打穿）：
    /// 第一版写的是 <c>classComment.Contains(p)</c>，而把整条清单条目抹掉、只留一句**散文旁白**
    /// （「第一例是上面 <c>&lt;c&gt;user.issue.&lt;/c&gt;</c> 那一条」）之后，判据**照旧全绿**
    /// —— 旁白里也写着这个名字。这就是第八种失败模式（判据被自己的文档满足）的又一个入口。
    /// ⇒ 改为要求出现 <b><paramref name="prefixes"/> 的成员 ＋ <c>&amp;lt;</c></b>，
    /// 即清单条目的转义形状（<c>&lt;c&gt;xxx.issue.&lt;码小写&gt;&lt;/c&gt;</c>）：
    /// 旁白只会写 <c>&lt;c&gt;user.issue.&lt;/c&gt;</c>、写不出 <c>&amp;lt;</c>。
    /// 附带收益：**定义面**（<c>= "user.issue.";</c>）里也没有 <c>&amp;lt;</c>
    /// ⇒ 这条判据对"输入面含定义面"天然免疫。</para>
    ///
    /// <para>⚠ 仍刻意用"形状出现"而不是"逐条比对清单条目"：清单条目的**措辞**
    /// （「AI 诊断」「工程包解包失败」…）是人写的散文，把它拆成结构等于再造一份会漂的清单。
    /// 本判据只问一件可判的事：**每个前缀有没有以清单条目的形状出现过**。</para>
    /// </summary>
    internal static List<string> PrefixDocumentationViolations(
        IEnumerable<string> prefixes, string classComment)
    {
        var bad = new List<string>();

        foreach (var p in prefixes)
        {
            if (!classComment.Contains(p + "&lt;", StringComparison.Ordinal))
                bad.Add($"`{p}`：`ExternalText` 的类注释「键约定」清单里**没有这一条** "
                    + "（按「前缀 ＋ `&lt;`」的清单条目形状找；只有散文旁白不算）"
                    + " ⇒「加了族忘了清单」又犯了一次（M15 / M16 两次这么漏过，当时没有任何判据看着它）");
        }

        return bad;
    }

    /// <summary>类注释那一段（切到 <c>internal static class ExternalText</c> 之前）。
    /// 找不到边界返回空串 ⇒ 下面每条前缀都会被点名（fail-closed，不是静默全绿）。</summary>
    internal const string DocBoundary = "internal static class ExternalText";

    /// <summary>定义面的形状签名 —— 切片里**不许**出现它（铁律 ③）。</summary>
    internal const string DefinitionFace = "internal const string";

    /// <summary>切出类注释那一段。⚠ 返回空串时的语义是"输入面失效"，判据会红，不是全绿。</summary>
    internal static string ClassCommentSlice(string fileText)
    {
        var i = fileText.IndexOf(DocBoundary, StringComparison.Ordinal);
        return i < 0 ? string.Empty : fileText[..i];
    }

    /// <summary>
    /// 反射枚举 <c>ExternalText</c> 里声明的全部 <c>*KeyPrefix</c> 常量。
    ///
    /// <para>⚠ <c>ExternalText</c> 是 <c>internal</c> ⇒ **必须**带 <c>BindingFlags.NonPublic</c>，
    /// 否则反射面为空、上面那条判据恒真（与 M10 的 <c>SystemFailureCodes</c> 同一个坑）。</para>
    /// </summary>
    internal static List<string> DeclaredPrefixes()
        => typeof(ExternalText)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string)
                        && f.Name.EndsWith("KeyPrefix", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    /// <summary>数子串出现次数（本文件私有的小工具 —— 与 M16 的那一份不共享：
    /// 它是 9 行的纯字符串工具、不含判据语义，没有漂移风险）。</summary>
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

    /// <summary>
    /// 把**实参**从渲染结果里掩掉 —— 实参是**用户数据**（配方名 / 表头原文 / 原值），
    /// 本来就可能是中文，拿 <see cref="Cjk"/> 直接扫渲染串会把它误判成"没翻"
    /// （本判据第一版就是这么红的：<c>Recipe "配方1" was rejected…</c> 里的 <c>配方1</c>
    /// 是测试数据，不是文案）。
    ///
    /// <para>⚠ 掩掉之后**只剩模板那一部分**在受检 —— 这正是"文案有没有翻"该问的问题。
    /// 模板本体的汉字另有 <see cref="RealIssues_InEnglish_CarryNoChinese"/> 里的第二半扫。</para>
    ///
    /// <para>🩸 **掩码本身必须是纯 ASCII**：第一版用的是 <c>«实参»</c>，
    /// 而「实参」两个字正在 <see cref="Cjk"/> 的扫描范围里 ⇒ 掩完照样被判"有中文"，
    /// 于是这条判据对**每一个带实参的码**都红 —— 红得理直气壮、指的方向却是错的。
    /// 「**量具自身的字面量不许落在被量的字母表里**」。（同 <c>Poison</c> 不许含中文的道理。）</para>
    /// </summary>
    internal static string MaskArgs(string rendered, RecipeImportIssue issue)
    {
        var s = rendered;
        if (issue.Args is not { Count: > 0 } args) return s;

        foreach (var arg in args)
        {
            var t = arg as string ?? Convert.ToString(arg, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(t))
                s = s.Replace(t!, "<<ARG>>", StringComparison.Ordinal);
        }

        return s;
    }

    // ══════════════════════════ 判据 ══════════════════════════

    /// <summary>
    /// 主判据：**每一条真实问题**在**两种语言下各渲染一遍**，结果都必须来自该语言的字典模板。
    ///
    /// <para>毒化那一路：把 <c>Message</c> 换成 <see cref="Poison"/> 再渲染 —— 回落原文**没进字典**
    /// 才是"字典在决定文案"的证明。⚠ 不能拿真原文比对：本族 zh 字典值与回落原文**措辞不同**，
    /// 拿真原文只能测"今天恰好翻了没"，测不到"渲染这一半确实由字典决定"。</para>
    /// </summary>
    [Fact]
    public void EveryIssue_RendersTheDictionaryTemplate_InBothLanguages()
    {
        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));
        var hits = probe.Hits;
        Assert.True(hits.Count >= 6,
            $"只取到 {hits.Count} 条问题（应有 6：5 条真实产出路径 ＋ 1 条工厂构造）"
            + " —— 取数失效，下面的判定会恒真");

        var rows = new List<RecipeCloneIssueLocalizationGuardTests.RenderRow>();
        var original = AppLocalization.Service.CurrentLanguage;

        try
        {
            foreach (var (lang, dict) in new[] { ("zh-CN", Zh), ("en-US", En) })
            {
                AppLocalization.Service.SwitchLanguage(lang);
                foreach (var hit in hits)
                {
                    var key = ExternalText.RecipeImportIssueKey(hit.Issue.Code);
                    // 毒化：保留码 / 行号 / 实参，只把回落原文换掉。
                    var poisoned = new RecipeImportIssue(hit.Issue.Code, Poison, hit.Issue.Line, hit.Issue.Args);
                    rows.Add(new RecipeCloneIssueLocalizationGuardTests.RenderRow(
                        $"L{hit.Issue.Line} {hit.Label}", lang, ExternalText.Describe(poisoned), key,
                        Expected(dict, key, hit.Issue.Args, hit.Issue.Line), hit.Issue.Message));
                }
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        JudgeReadings.Write(_out, "配方 CSV 导入的问题（#61f · recipe.import.issue.*）",
            rows.Select(r => $"    {r.Label,-46} {r.Language} ⇒ {r.Rendered}").ToList());

        // 自检：语言真的切动了。静默没切会让 en 那一遍渲染成中文 —— 那时下面那条期望比对
        // 已经会红，但红在"渲染 ≠ 期望"上，指错方向。
        foreach (var hit in hits)
        {
            var key = ExternalText.RecipeImportIssueKey(hit.Issue.Code);
            var zhExpected = Expected(Zh, key, hit.Issue.Args, hit.Issue.Line);
            var enExpected = Expected(En, key, hit.Issue.Args, hit.Issue.Line);
            var label = $"L{hit.Issue.Line} {hit.Label}";
            var zhRow = rows.Single(r => r.Label == label && r.Language == "zh-CN");
            var enRow = rows.Single(r => r.Label == label && r.Language == "en-US");

            var expectedDiffers = zhExpected is not null && enExpected is not null
                && !string.Equals(zhExpected, enExpected, StringComparison.Ordinal);
            var renderedDiffers = !string.Equals(zhRow.Rendered, enRow.Rendered, StringComparison.Ordinal);

            Assert.True(renderedDiffers || !expectedDiffers,
                $"{label}：zh 与 en 渲染一模一样「{zhRow.Rendered}」，可两本字典的值并不相同"
                + " ⇒ 语言切换没生效（渲染没跟着当前语言走）");
        }

        var problems = RecipeCloneIssueLocalizationGuardTests.RenderViolations(rows, Poison);
        Assert.True(problems.Count == 0,
            "有问题没走字典（登记 #61f / 阶段 100）。正确动作是补 "
            + "`recipe.import.issue.<码小写>` 的字典键，**不是**把裸中文串再拼回去"
            + "（那会让英文界面下中英混排），也不是让消费点丢掉 Issues：\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **收益的正面见证**：真问题（不毒化）在**英文界面下不许再出现中文**。
    ///
    /// <para>与主判据的见证面不重合：主判据证明"渲染由字典决定"，
    /// 这条证明"英文用户读到的东西真的不含中文" —— 后者正是 #61 那一族缺陷的用户可见症状。
    /// 用 <see cref="Cjk"/> 当尺子、不写死措辞：换文案不该打红它，字典缺键/值没翻才会。</para>
    ///
    /// <para><b>⚙ 两个见证面必须分开</b>（本判据第一版就是在这里红的）：</para>
    /// <list type="number">
    ///   <item><b>渲染串</b>要先 <see cref="MaskArgs"/> 把实参掩掉再扫 —— 实参是**用户数据**
    ///     （配方名 / 表头原文 / 原值），本来就可能是中文；直接扫会把
    ///     <c>Recipe "配方1" was rejected…</c> 里的 <c>配方1</c> 误判成"没翻"；</item>
    ///   <item><b>en 模板本体</b>（字典里的值）单独扫一遍 —— 这一半才真正区分"翻了"与"没翻"，
    ///     而且不受实参污染。两个面少任何一个都会漏掉一类缺陷。</item>
    /// </list>
    /// </summary>
    [Fact]
    public void RealIssues_InEnglish_CarryNoChinese()
    {
        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));

        var original = AppLocalization.Service.CurrentLanguage;
        var rows = new List<(string Label, string Masked, string Raw, string Fallback)>();
        try
        {
            AppLocalization.Service.SwitchLanguage("en-US");
            foreach (var hit in probe.Hits)
            {
                var rendered = ExternalText.Describe(hit.Issue);
                rows.Add(($"L{hit.Issue.Line} {hit.Label}", MaskArgs(rendered, hit.Issue),
                    rendered, hit.Issue.Message));
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        JudgeReadings.Write(_out, "配方 CSV 导入 · 英文界面下的渲染（#61f）",
            rows.Select(r => $"    {r.Label,-46} en ⇒ {r.Raw}").ToList());

        var bad = rows.Where(r => Cjk.IsMatch(r.Masked))
            .Select(r => $"    {r.Label}：英文界面下渲染出中文「{r.Raw}」（回落原文是「{r.Fallback}」）"
                + " ⇒ 该语言的字典键没查到")
            .ToList();

        // 第二半：en 的**模板本体**不许含汉字（不受实参污染的那把尺子）。
        foreach (var hit in probe.Hits)
        {
            var key = ExternalText.RecipeImportIssueKey(hit.Issue.Code);
            if (En.TryGetValue(key, out var template) && Cjk.IsMatch(template))
                bad.Add($"    {key}：en-US 的**模板本体**含汉字「{template}」 ⇒ 这一条没翻（或直接抄了 zh）");
        }

        Assert.True(bad.Count == 0,
            "英文界面下这些问题仍是中文（登记 #61f / 阶段 100）—— 用户可见的收益就落在这条上：\n"
            + string.Join("\n", bad));
    }

    /// <summary>两本字典齐 + 成对 + **占位符个数与实参个数一一对应**（规则复用 M16 的纯函数）。</summary>
    [Fact]
    public void BothDictionaries_CarryEveryCode_AndAreActuallyPaired()
    {
        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));

        var rows = probe.Hits
            .Select(h => (Key: ExternalText.RecipeImportIssueKey(h.Issue.Code), ArgCount: h.Issue.Args?.Count ?? 0))
            .ToList();
        var distinct = rows.Select(r => r.Key).Distinct(StringComparer.Ordinal).ToList();

        Assert.True(distinct.Count >= 6,
            $"只取到 {distinct.Count} 个键（应有 6：too_few_lines / no_header_columns / "
            + "row_columns_missing / empty_column_name / value_not_a_number / recipe_not_applied）"
            + " —— 取数失效，下面的判定会恒真");

        var problems = RecipeCloneIssueLocalizationGuardTests.PairingViolations(rows, Zh, En);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>码表**三态闭包**：声明的码要么真的产得出，要么在 <see cref="NotProducible"/> 里写了理由。</summary>
    [Fact]
    public void DeclaredCodes_AreEitherProducedOrExplicitlyNotProducible()
    {
        var declared = DeclaredCodes();
        Assert.True(declared.Count >= 6,
            $"只反射到 {declared.Count} 个码 —— 取数失效（反射面空了，三态闭包会恒真）");
        Assert.True(NotProducible.Count >= 1,
            "NotProducible 表空了 —— 要么 RECIPE_NOT_APPLIED 真的变得产得出（那就该把它从表里删掉、"
            + "并补一条真实见证），要么反射面失效（那个码没被反射到）");

        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));

        // ⚠ 闭包只看**产出方真的发得出来**的那些（见 Hit.ReachedByProducer）——
        // 工厂构造的那一档只进渲染面。混在一起会让 NotProducible 那条判据自己把自己打红。
        var produced = probe.Hits
            .Where(h => h.ReachedByProducer)
            .Select(h => h.Issue.Code)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(produced.Count >= 5,
            $"走真实产出路径拿到的失败只有 {produced.Count} 条（应有 5）"
            + " —— 取数退化成手搓，三态闭包会恒真");

        JudgeReadings.Write(_out, "配方 CSV 导入的码表（#61f）",
            new List<string>
            {
                $"    声明 {declared.Count} 个 / 真实产出 {produced.Count} 个 / 明确产不出 {NotProducible.Count} 个："
                + $"产出 = {string.Join(", ", produced.OrderBy(x => x, StringComparer.Ordinal))}；"
                + $"产不出 = {string.Join(", ", NotProducible.Keys.OrderBy(x => x, StringComparer.Ordinal))}",
                $"    渲染见证 {probe.Hits.Count} 条（其中工厂构造 {probe.Hits.Count(h => !h.ReachedByProducer)} 条）",
            });

        var problems = RecipeCloneIssueLocalizationGuardTests.ClosureViolations(declared, produced, NotProducible);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>产出方必须**返回值带码**，且不许把入库守卫的 <c>bool</c> 当语句丢掉。</summary>
    [Fact]
    public void Producer_ReturnsCodedResult_AndConsumesTheApplyGuards()
    {
        var problems = ProducerViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "配方 CSV 导入产出方的形状（#61f）",
            new List<string> { $"    {Producer}（6 条工厂各 1 次 ＋ 不许裸语句形式的入库调用）" });

        Assert.True(problems.Count == 0,
            "产出方必须给码 —— 否则站点换成码化出口会**收益为零**"
            + "（面绿着、用户看到的一字不变）：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>站点必须接住问题表：读 <c>result.Issues</c> ＋ 走码化出口 ＋ 恰好一处接线。</summary>
    [Fact]
    public void Consumer_ShowsTheIssuesThroughTheCodedOutlet()
    {
        var problems = ConsumerViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "配方 CSV 导入站点的接线（#61f）", Sites.Select(f => $"    {f}").ToList());

        Assert.True(problems.Count == 0,
            "问题明细到界面的这条链断了（登记 #61f / 阶段 100）。**只补渲染出口不算收口** —— "
            + "消费点不接线时用户所见一字不变：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>问题状态条不许嵌进 <c>ScrollViewer</c>（headless 首次布局不收敛的死区）。</summary>
    [Fact]
    public void IssueBar_IsNotNestedInAScrollViewer()
    {
        var problems = IssueBarPlacementViolations(AppSourceScanner.ScanAppXaml());

        JudgeReadings.Write(_out, "问题状态条的位置（#61f）", new List<string> { $"    {IssueBarXaml}" });

        Assert.True(problems.Count == 0,
            "问题状态条的位置踩了 headless 布局死区（登记 #61f / 阶段 100）：\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 「**每条前缀都必须在类注释的清单里**」—— 立这条的起因是**同一件事已经漏过两次**
    /// （M15 / M16）。本判据第一次跑就应当红出那两条（<c>user.issue.</c> / <c>sim.issue.</c>），
    /// 那就是它的**真实仓库正控**。
    ///
    /// <para>⚠ 断言里那句 <c>Assert.DoesNotContain(DefinitionFace, slice)</c> 是**输入面自检**
    /// （判据四条铁律 ③）：前缀字面量本来就在 <c>const</c> 声明里出现，
    /// 输入面一旦含定义面，下面那条判据恒真。</para>
    /// </summary>
    [Fact]
    public void EveryKeyPrefix_IsDocumentedInTheClassComment()
    {
        var prefixes = DeclaredPrefixes();
        // 阶段 103 起 **15** 条（新增 sim.build.issue.）。与 M8~M18 同口径取下界满值：
        // 少一条就说明取数出口改名/挪走了，这条判据（以及它担保的清单）一起失效。
        Assert.True(prefixes.Count >= 15,
            $"只反射到 {prefixes.Count} 条 `*KeyPrefix`（应有 ≥ 15）"
            + " ⇒ 反射面失效（最可能：少了 BindingFlags.NonPublic，ExternalText 是 internal）");

        var file = AppSourceScanner.ScanAllProductSources()
            .FirstOrDefault(s => s.Relative.EndsWith("Services/ExternalText.cs", StringComparison.Ordinal));
        Assert.False(file.Text is null, "语料里找不到 Services/ExternalText.cs（扫描面失效？）");

        var slice = ClassCommentSlice(file.Text!);
        Assert.Contains("键约定", slice, StringComparison.Ordinal);
        Assert.DoesNotContain(DefinitionFace, slice, StringComparison.Ordinal);

        var problems = PrefixDocumentationViolations(prefixes, slice);

        JudgeReadings.Write(_out, "ExternalText 的键前缀 → 类注释清单（#61f 附带判据）",
            prefixes.Select(p => $"    {p,-24} 清单里{(slice.Contains(p, StringComparison.Ordinal) ? "有" : "**没有**")}")
                .ToList());

        Assert.True(problems.Count == 0,
            "有键前缀没写进 ExternalText 的类注释「键约定」清单 —— 这类" + "「加了族忘了清单」"
            + "已经漏过两次（M15 user.issue./sim.issue. 阶段 97、M16 recipe.issue. 阶段 98），"
            + "当时没有任何判据看着它：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 行号前缀的键**只有一个来源**：<see cref="LinePrefixKey"/> 与
    /// <c>Lang.CsvLinePrefix</c> 必须是同一个值 —— 否则守卫按一个字面量查字典、
    /// 而 <c>Describe</c> 按另一个查，两边都"自洽"，红的却谁也不是。
    /// </summary>
    [Fact]
    public void LinePrefixKey_MatchesTheLangConstant()
    {
        Assert.Equal(Lang.CsvLinePrefix, LinePrefixKey);
        Assert.True(Zh.ContainsKey(LinePrefixKey), $"zh-CN 字典里没有 {LinePrefixKey}");
        Assert.True(En.ContainsKey(LinePrefixKey), $"en-US 字典里没有 {LinePrefixKey}");
    }

    /// <summary>
    /// **诚实边界正面断言**：一条**没有码**的 <see cref="RecipeImportIssue"/>（字典里没有它的键）
    /// 照旧回落原文 ⇒ 「码才是收益的唯一来源」，与主判据成对。
    /// ⚠ 顺带钉住"行号是结构化字段"：<c>Line = 0</c> 时**不加**前缀，<c>Line &gt; 0</c> 时加。
    /// </summary>
    [Fact]
    public void IssueWithoutAKey_StillFallsBackToTheRawMessage()
    {
        var bare = new RecipeImportIssue("NOT_A_REAL_CODE", "库里没有这个码的回落原文", 7, new object[] { "x" });
        var rendered = ExternalText.Describe(bare);
        Assert.DoesNotContain("NOT_A_REAL_CODE", rendered, StringComparison.Ordinal);
        Assert.Contains(bare.Message, rendered, StringComparison.Ordinal);

        var coded = RecipeImportIssue.ValueNotANumber(3, "v1", "abc");
        Assert.NotEqual(coded.Message, ExternalText.Describe(coded));

        // 行号：0 ⇒ 不加前缀（文件级问题）；>0 ⇒ 加（且内容里含行号本身）
        var noLine = new RecipeImportIssue("NOT_A_REAL_CODE", "文件级", 0);
        Assert.Equal("文件级", ExternalText.Describe(noLine));
        Assert.NotEqual("文件级", rendered);
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

        var keys = hits.Select(h => (Key: ExternalText.RecipeImportIssueKey(h.Issue.Code),
            ArgCount: h.Issue.Args?.Count ?? 0)).ToList();
        var key0 = keys[0].Key;

        // ── NC0 · 正控（**必须全合成**：外部负控会让它跟着红）──
        Assert.Empty(RecipeCloneIssueLocalizationGuardTests.PairingViolations(keys, zh, en));

        var clean = hits.Select(h => new RecipeCloneIssueLocalizationGuardTests.RenderRow(
            $"L{h.Issue.Line} {h.Label}", "zh-CN",
            Expected(zh, ExternalText.RecipeImportIssueKey(h.Issue.Code), h.Issue.Args, h.Issue.Line)!,
            ExternalText.RecipeImportIssueKey(h.Issue.Code),
            Expected(zh, ExternalText.RecipeImportIssueKey(h.Issue.Code), h.Issue.Args, h.Issue.Line),
            h.Issue.Message)).ToList();
        Assert.Empty(RecipeCloneIssueLocalizationGuardTests.RenderViolations(clean, Poison));
        Assert.All(clean, r => Assert.NotNull(r.Expected));

        // ── NC1 · 成对性（三档）──
        var enCopy1 = new Dictionary<string, string>(en, StringComparer.Ordinal) { [key0] = zh[key0] };
        Assert.Contains(RecipeCloneIssueLocalizationGuardTests.PairingViolations(keys, zh, enCopy1),
            v => v.Contains("中文字符", StringComparison.Ordinal));

        var enCopy2 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy2.Remove(key0);
        Assert.Contains(RecipeCloneIssueLocalizationGuardTests.PairingViolations(keys, zh, enCopy2),
            v => v.Contains("缺键", StringComparison.Ordinal));

        // 占位符个数：本族里 `empty_column_name` 与 `no_header_columns` 都是 1 实参
        // ⇒ 再塞一个 {1} 即个数不符。⚠ 不能挑"本来就 2 占位符"的键去塞：
        // 个数照样对得上 ⇒ 注入无效（阶段 97 负控第一版就栽在这里）。
        var oneArgKey = ExternalText.RecipeImportIssueKey(
            hits.First(h => (h.Issue.Args?.Count ?? 0) == 1).Issue.Code);
        var zhCopy = new Dictionary<string, string>(zh, StringComparer.Ordinal) { [oneArgKey] = zh[oneArgKey] + " {1}" };
        Assert.Contains(RecipeCloneIssueLocalizationGuardTests.PairingViolations(keys, zhCopy, en),
            v => v.Contains("占位符", StringComparison.Ordinal));

        // ── NC2 · 渲染对照（三档都要报）──
        Assert.Contains(
            RecipeCloneIssueLocalizationGuardTests.RenderViolations(
                new[] { new RecipeCloneIssueLocalizationGuardTests.RenderRow(
                    "演示", "en-US", "x" + Poison, key0, "期望串", "回落原文") }, Poison),
            v => v.Contains("中文回落原文", StringComparison.Ordinal));

        Assert.Contains(
            RecipeCloneIssueLocalizationGuardTests.RenderViolations(
                new[] { new RecipeCloneIssueLocalizationGuardTests.RenderRow(
                    "演示", "en-US", "换个说法", key0, "期望串", "回落原文") }, Poison),
            v => v.Contains("有别的来源", StringComparison.Ordinal));

        Assert.Contains(
            RecipeCloneIssueLocalizationGuardTests.RenderViolations(
                new[] { new RecipeCloneIssueLocalizationGuardTests.RenderRow(
                    "演示", "en-US", "回落原文", key0, null, "回落原文") }, Poison),
            v => v.Contains("字典里没有", StringComparison.Ordinal));

        // 行号前缀忘了套 ⇒ 期望串与渲染串不等 ⇒ 一样点名（否则"行号"这一维没人管）。
        // ⚠ 必须挑一个**行级**样本：TOO_FEW_LINES（Line=0）拿它测这条会静默跳过。
        var rowHit = hits.First(h => h.Issue.Line > 0);
        var rowKey = ExternalText.RecipeImportIssueKey(rowHit.Issue.Code);
        Assert.NotEqual(zh[rowKey], Expected(zh, rowKey, rowHit.Issue.Args, rowHit.Issue.Line)); // 前置：本来就该不同
        Assert.Contains(
            RecipeCloneIssueLocalizationGuardTests.RenderViolations(
                new[] { new RecipeCloneIssueLocalizationGuardTests.RenderRow(
                    "演示(行级)", "zh-CN", zh[rowKey], rowKey,
                    Expected(zh, rowKey, rowHit.Issue.Args, rowHit.Issue.Line), rowHit.Issue.Message) }, Poison),
            v => v.Contains("有别的来源", StringComparison.Ordinal));

        // ── NC3 · 三态闭包（四档都要报）──
        var declared = DeclaredCodes();
        // ⚠ 正控必须喂**真实产出那一档的全部 5 个码**：只喂一个会让其余 4 个被判"死码"，
        //    正控自己先红（本判据第一版就是这么红的，红在"死码"上、指错方向）。
        //    这里是**全合成**的输入（取自 ProbeFailures 的读数），不依赖上面的真仓库对照。
        var produced = hits.Where(h => h.ReachedByProducer)
            .Select(h => h.Issue.Code).Distinct(StringComparer.Ordinal).ToArray();
        Assert.True(produced.Length == 5, $"真实产出那一档应恰好 5 个码，实测 {produced.Length}");
        Assert.Empty(RecipeCloneIssueLocalizationGuardTests.ClosureViolations(declared, produced, NotProducible));

        Assert.Contains(
            RecipeCloneIssueLocalizationGuardTests.ClosureViolations(
                declared, produced, new Dictionary<string, string>(StringComparer.Ordinal)),
            v => v.Contains("死码", StringComparison.Ordinal));

        Assert.Contains(
            RecipeCloneIssueLocalizationGuardTests.ClosureViolations(
                declared, produced.Append("NOT_A_REAL_CODE"), NotProducible),
            v => v.Contains("没声明", StringComparison.Ordinal));

        Assert.Contains(
            RecipeCloneIssueLocalizationGuardTests.ClosureViolations(
                declared, produced.Append(RecipeImportIssueCodes.RECIPE_NOT_APPLIED), NotProducible),
            v => v.Contains("过期", StringComparison.Ordinal));

        var drifted = new Dictionary<string, string>(NotProducible, StringComparer.Ordinal)
        {
            ["NOT_A_REAL_CODE"] = "演示",
        };
        Assert.Contains(
            RecipeCloneIssueLocalizationGuardTests.ClosureViolations(declared, produced, drifted),
            v => v.Contains("抄写漂移", StringComparison.Ordinal));

        // ── NC4 · 产出方（合成语料，**目标文件必须在**）──
        static (string Relative, string Text)[] Corpus(string body) => new[] { (Producer, body) };

        const string compliant = """
            public RecipeImportResult ImportCsv(string csvContent)
            {
                var issues = new List<RecipeImportIssue>();
                var lines = csvContent.Split('\n');
                if (lines.Length < 2) { issues.Add(RecipeImportIssue.TooFewLines()); return new RecipeImportResult(0, issues); }
                if (h.Length < 3) { issues.Add(RecipeImportIssue.NoHeaderColumns(lines[0])); return new RecipeImportResult(0, issues); }
                if (c.Length < 3) { issues.Add(RecipeImportIssue.RowColumnsMissing(lineNumber)); continue; }
                if (string.IsNullOrEmpty(varId)) { issues.Add(RecipeImportIssue.EmptyColumnName(lineNumber, j + 1)); continue; }
                else { issues.Add(RecipeImportIssue.ValueNotANumber(lineNumber, varId, raw)); }
                var applied = existing != null ? UpdateRecipe(recipe) : AddRecipe(recipe);
                if (!applied) { issues.Add(RecipeImportIssue.RecipeNotApplied(lineNumber, recipeName)); continue; }
                return new RecipeImportResult(count, issues);
            }
            """;
        Assert.Empty(ProducerViolations(Corpus(compliant)));

        // ① 退回 `int` 形状 ⇒ 两条都点名
        var regressed = ProducerViolations(Corpus("public int ImportCsv(string csvContent) { return 0; }"));
        Assert.Contains(regressed, v => v.Contains("public RecipeImportResult ImportCsv(", StringComparison.Ordinal));
        Assert.Contains(regressed, v => v.Contains("public int ImportCsv(", StringComparison.Ordinal));
        Assert.Contains(regressed, v => v.Contains("issues.Add(RecipeImportIssue.", StringComparison.Ordinal));

        // ② 少一条工厂 ⇒ 点名"出现 0 次"（那个码会退化成静默）
        var missingFactory = ProducerViolations(Corpus(compliant.Replace(
            "issues.Add(RecipeImportIssue.ValueNotANumber(lineNumber, varId, raw));", string.Empty,
            StringComparison.Ordinal)));
        Assert.Contains(missingFactory, v => v.Contains("RecipeImportIssue.ValueNotANumber(", StringComparison.Ordinal));
        Assert.Contains(missingFactory, v => v.Contains("出现 0 次", StringComparison.Ordinal));

        // ③ 入库守卫的 bool 又被当语句丢掉 ⇒ 点名"当语句调用"
        //    （"包含某个串"式的判据会漏掉这条：那个 if 还在，只是多了一处裸调用）
        var dropped = ProducerViolations(Corpus(compliant
            + "\n                AddRecipe(recipe);\n                UpdateRecipe(recipe);\n"));
        Assert.Contains(dropped, v => v.Contains("当**语句**调用", StringComparison.Ordinal));

        // ④ 语料里缺这个文件 ⇒ 必须报"找不到"（扫描面失效时会静默全绿）
        Assert.Contains(ProducerViolations(Array.Empty<(string, string)>()),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ⑤ 注释里写的反例不算（规则不许被自己的文档满足，登记 #48）
        Assert.Empty(ProducerViolations(Corpus(compliant
            + "\n// 反例（不许这么写）：public int ImportCsv(...) ／ 裸 AddRecipe(recipe);")));

        // ── NC5 · 站点接线（注入 + 真语料对照）──
        var all = AppSourceScanner.ScanAllProductSources();
        Assert.Empty(ConsumerViolations(all));

        var unwired = ConsumerViolations(new[] { (Sites[0],
            "var count = _recipeManager.ImportCsv(content);\nif (count > 0) { ShowStatus(count); }") });
        Assert.Contains(unwired, v => v.Contains("result.Issues", StringComparison.Ordinal));
        Assert.Contains(unwired, v => v.Contains("出现 0 次", StringComparison.Ordinal));

        var rawMessage = ConsumerViolations(new[] { (Sites[0],
            "ShowCsvIssues(result.Issues);\nTxtCsvIssues.Text = result.Issues[0].Message;") });
        Assert.Contains(rawMessage, v => v.Contains("中文回落原文", StringComparison.Ordinal));

        // ── NC6 · 状态条位置：嵌进 ScrollViewer ⇒ 点名；Border ⇒ 绿 ──
        static (string Relative, string Text)[] Xaml(string body) => new[] { (IssueBarXaml, body) };

        var inScroll = IssueBarPlacementViolations(Xaml(
            "<ScrollViewer>\n<Border x:Name=\"CsvIssueBar\">\n</Border>\n"));
        Assert.Contains(inScroll, v => v.Contains("未闭合的 `ScrollViewer`", StringComparison.Ordinal));

        Assert.Empty(IssueBarPlacementViolations(Xaml(
            "<ScrollViewer>\n</ScrollViewer>\n<Border x:Name=\"CsvIssueBar\">\n</Border>\n")));

        // ⚠ 注释里写着反例也不算（本文件自己的注释里就有 ScrollViewer 这个词）：
        //    这一段注释里带一个**未闭合**的 ScrollViewer 开标签，剥 XML 注释后必须判绿。
        Assert.Empty(IssueBarPlacementViolations(Xaml(
            "<!-- 反例：<ScrollViewer>\n（这里故意不闭合）\n-->\n<Border x:Name=\"CsvIssueBar\">\n")));

        // 名字被删/改了 ⇒ 判据自己失效，必须点名（不能静默全绿）
        Assert.Contains(IssueBarPlacementViolations(Xaml("<Border x:Name=\"Other\" />")),
            v => v.Contains("找不到 `CsvIssueBar`", StringComparison.Ordinal));

        // ── NC7 · 键前缀清单（锚点是**清单条目的形状**，不是"名字出现过"）──
        var prefixes = DeclaredPrefixes();
        Assert.True(prefixes.Count >= 15, $"只反射到 {prefixes.Count} 条前缀 —— 取数失效");

        // ⚠ **定义面当输入面**：const 声明写的是 `= "user.issue.";`，**没有** `&lt;`
        //   ⇒ 收紧后的判据**逐条点名**。（收紧前它在定义面下恒真 —— 而那正是负控 E 打穿的路：
        //   把清单条目抹掉、只留旁白，判据照旧全绿。收紧顺带把这一路堵死了。）
        var definitionFaceText = string.Concat(
            prefixes.Select((p, i) => $"internal const string P{i}KeyPrefix = \"{p}\";\n"));
        Assert.Equal(prefixes.Count, PrefixDocumentationViolations(prefixes, definitionFaceText).Count);

        // 🩸 **散文旁白不算**：只留一句写着这个名字的旁白、没有清单条目形状 ⇒ 必须点名
        //    （负控 E 就是走这条路穿过去的；这条断言把它钉住）
        var proseOnly = definitionFaceText + "<item>第一例是上面 <c>user.issue.</c> 那一条</item>\n";
        var byProse = PrefixDocumentationViolations(new[] { ExternalText.UserIssueKeyPrefix }, proseOnly);
        Assert.True(byProse.Count == 1
                    && byProse[0].Contains(ExternalText.UserIssueKeyPrefix, StringComparison.Ordinal),
            "只有散文旁白（没有 `前缀 ＋ &lt;` 的清单条目形状）时应当点名 —— 没点名说明这条判据"
            + "又被自己的文档喂饱了（第八种失败模式）");

        // 有清单条目形状 ⇒ 绿（证明它不是"永远返回违规"的判据）
        Assert.Empty(PrefixDocumentationViolations(new[] { ExternalText.UserIssueKeyPrefix },
            proseOnly + "<item><c>user.issue.&lt;码小写&gt;</c></item>\n"));

        // 切片边界漏了 ⇒ 输入面为空 ⇒ 每条前缀都被点名（fail-closed，不是静默全绿）
        Assert.Equal(prefixes.Count, PrefixDocumentationViolations(prefixes, ClassCommentSlice("没有边界")).Count);
    }
}
