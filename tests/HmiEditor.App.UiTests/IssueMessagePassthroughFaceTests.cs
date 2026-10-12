using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「**拿到 issues 列表、却直接渲染消息（回落原文）**」的站点面守卫（登记表 §4 <b>#61x</b>）。
///
/// <para><b>它与 <see cref="ExceptionMessagePassthroughFaceTests"/> 是互补的两条面，别当成重复</b>：
/// 那条守的是「`Exception.Message` 直通模板」，**只认 `Exception`** —— 本类守的是
/// 「记录体（有 `Code`）的 `Message` 被当成文案直显」，**根本没有异常**，所以那条面
/// 原理上看不见它（与阶段 88 记的「面没变小 ≠ 没做事」同一条教训）。</para>
///
/// <para><b>本仓库的既定形状（正确的那一种）</b>：记录体给「码 + 中文回落 + 实参」，
/// 界面侧按 <c>&lt;族前缀&gt;.&lt;码小写&gt;</c> 查字典、查不到回落原文 ——
/// 唯一实现是 <c>ExternalText.Resolve(key, issue.Message, issue.Args)</c>，
/// 各族只包一层薄封装（<c>Describe</c> / <c>LocalizeIssue</c>）。实测同族**八处**都走它：
/// <c>ExternalText.Describe</c> 的 SVG / AI / 配方 / 配方导入 / CSV / 登录六族，
/// 加 <c>ProjectSettingsView.LocalizeIssue</c>（硬件族 <c>hw.issue.*</c>）与
/// <c>UserAdminException</c> 的 `base(issue.Message)`（回落原文的正确用法）。</para>
///
/// <para><b>症状</b>：界面切成英文后，这一半**照旧中文** —— 因为那串是 Core 的**回落原文**，
/// 而回落按设计**必须可用**（字典缺键时才显示）⇒ 它必然是中文。
/// 只有在英文界面下才看得见（中文界面下字典值与回落原文**按设计逐字相同**）。</para>
///
/// <para><b>收官（阶段 104）：面 = 0 ⇒ 本面从「定量清单」升格为「零容忍」</b>。
/// 那唯一一站是 <c>CommunicationConfigView.OnTestConnection</c> 的
/// <c>string.Join(", ", result.Issues.Select(i => i.Message))</c>；阶段 104 给通讯族
/// 补齐了收口所需的四件：契约补 <c>Args</c>、键推导 <c>comm.issue.*</c>、字典两语各 +9 键、
/// 视图侧薄封装 <c>LocalizeIssue</c> ⇒ <c>.Select(i => i.Message)</c> 整条消失。</para>
///
/// <para><b>⚠ 为什么这**不是**改名式假闭环</b>：判「算不算收口」的标准是**产出方有没有码**。
/// 阶段 102 那次只定量、不动手，是因为通讯族的记录体与兄弟族**不同形** ——
/// <c>Hmi.Communication.Contracts.ValidationIssue(Severity, Code, Message)</c> **没有 `Args`**、
/// 消费点也**不读 `Code`**（直接 <c>i.Message</c>）⇒ 界面侧拿不到"该查哪个键"。
/// 阶段 104 把契约补成与 <c>Hmi.Hardware.Contracts.ValidationIssue</c> **同形**、并让
/// 四个驱动的 9 条码第一次有了"码 → 字典键"的对应（守卫
/// <c>PluginTextLocalizationGuardTests.CommIssueRegistry_MatchesWhatTheDriversActuallyProduce</c>
/// **双向**夹住那份码表）。</para>
///
/// <para>⚠ 基线清空之后，"变少也得改基线"那一半就退场了（<see cref="Diff"/> 仍然支持
/// 传入基线，由 NC6 用**合成基线**单独钉住 —— 免得哪天面又长出成员时无人发现）。</para>
///
/// <para><b>面的口径</b>（<see cref="CollectSites"/>）：<c>src/</c> 下全部项目的 <c>.cs</c> 里，
/// **同一语句**内既出现 <c>.Issues</c>、又出现 <c>.Message</c>。三个刻意的取舍：</para>
/// <list type="number">
///   <item><b>先剥注释</b> —— 否则规则会被自己的文档满足（登记 #48 踩过三次；本类的注释里
///     就写着正反两个例子）。</item>
///   <item><b>排除产出侧</b>（<c>.Issues.Add(</c> / <c>.Issues.Clear(</c>）—— 那是**往列表里装**
///     而不是读，装的时候顺带带个 <c>.Message</c> 不构成缺陷（实测全仓此类语句零命中，
///     但形状上必须先排除，否则将来一命中就是个假阳性）。</item>
///   <item><b>按语句切</b>（从 <c>.Issues</c> 到下一个 <c>;</c>）而不是按行 —— 链式调用会跨行。</item>
/// </list>
///
/// <para><b>🩸 它看不见什么（自陈，别当成覆盖到了）</b>：拿 <c>.Issues</c> 存进**中间变量**
/// 再读、或 <c>foreach</c> 拆开写（<c>var issues = r.Issues;</c> 然后 <c>issues[0].Message</c>）
/// ⇒ **不是站点**。这是**位置式**规则的通病（与 <c>EveryConveyedPathVariable…</c> 同一条边界）：
/// 做作用域解析要写第二份扫描，而"面变小"这件事由双向基线管住。负控 NC5 把这一档
/// **显式钉成"看不见"**，免得下一个人以为它被盖住了。</para>
/// </summary>
public class IssueMessagePassthroughFaceTests
{
    private readonly ITestOutputHelper _out;

    public IssueMessagePassthroughFaceTests(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// <c>.Issues</c> 成员访问，**排除产出侧**（<c>Add</c> / <c>Clear</c>）。
    /// 前瞻里用 <c>[ \t]</c> 而不是 <c>\s</c>：后者能跨行匹配，会把下一个语句的
    /// <c>.Add(</c> 当成紧跟其后的兄弟。
    /// </summary>
    internal static readonly Regex IssuesMember =
        new(@"\.Issues\b(?![ \t]*\.[ \t]*(?:Add|Clear)[ \t]*\()", RegexOptions.Compiled);

    /// <summary>
    /// 面：文件名 → 站点数。**阶段 104 起清空** —— 面已收官，本表从「定量清单」
    /// 升格为「零容忍」：任何一处命中都是**新缺陷**（<see cref="Diff"/> 会把它们全报成"新增"）。
    ///
    /// <para>清空掉的那一行：<c>CommunicationConfigView.axaml.cs</c>（原 1 处，见类注释）。
    /// ⚠ 别把它当成"豁免表"的反面随手加回来：这张表现在只该**保持空**；真要加成员，
    /// 那说明有人写出了本面要抓的形状。</para>
    /// </summary>
    internal static readonly Dictionary<string, int> Baseline = new(StringComparer.Ordinal);

    // ══════════════════════════ 面的取数（纯函数 —— 负控要复用） ══════════════════════════

    /// <summary>语料 → 站点清单。语料**当参数传入**，负控才能喂合成语料（不碰磁盘）。</summary>
    internal static List<string> CollectSites(IEnumerable<(string Relative, string Text)> sources)
    {
        var sites = new List<string>();

        foreach (var (relative, text) in sources)
        {
            var code = SourceScanner.StripComments(text);

            foreach (Match m in IssuesMember.Matches(code))
            {
                var from = m.Index + m.Length;
                var semi = code.IndexOf(';', from);
                var statement = semi < 0 ? code[from..] : code[from..semi];

                if (statement.Contains(".Message", StringComparison.Ordinal))
                    sites.Add(relative);
            }
        }

        return sites;
    }

    /// <summary>
    /// 面与基线的**双向**差异（多了红、少了也红 —— 少了必须同步改小基线）。
    ///
    /// <para>基线**当参数传入**（默认 <see cref="Baseline"/>）：与语料同一个理由 ——
    /// 负控要喂合成基线，否则"已消失"那一半在真语料上再也触发不到（阶段 104 基线已清空）。</para>
    /// </summary>
    internal static List<string> Diff(
        IEnumerable<string> sites,
        IReadOnlyDictionary<string, int>? baseline = null)
    {
        var want_ = baseline ?? Baseline;
        var actual = sites.GroupBy(f => f, StringComparer.Ordinal)
                          .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var (file, n) in actual.OrderBy(x => x.Key, StringComparer.Ordinal))
            if (!want_.TryGetValue(file, out var want))
                problems.Add($"**新增**  {file}  （{n} 处；基线里没有）");
            else if (want != n)
                problems.Add($"**变多**  {file}  {want} → {n}");

        foreach (var (file, n) in want_.OrderBy(x => x.Key, StringComparer.Ordinal))
            if (!actual.TryGetValue(file, out var got))
                problems.Add($"**已消失**  {file}  （基线 {n} 处；收口完成了请把这一行删掉）");
            else if (got < n)
                problems.Add($"**变少**  {file}  {n} → {got}");

        return problems;
    }

    // ══════════════════════════ 判据 ══════════════════════════

    [Fact]
    public void IssueMessageRenderedWithoutCode_MatchesBaseline()
    {
        var sites = CollectSites(AppSourceScanner.ScanAllProductSources());
        var problems = Diff(sites);

        var readings = sites.GroupBy(f => f, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"    {g.Key}  {g.Count()}")
            .ToList();
        readings.Insert(0, $"    站点 {sites.Count} 个 / 文件 {readings.Count} 个");
        JudgeReadings.Write(_out, "拿到 issues 列表却直接渲染回落原文（#61x 定量）", readings);

        Assert.True(problems.Count == 0,
            "「拿到 issues 列表却直接渲染回落原文」的站点面变了（登记 #61x）。\n"
            + "**修法方向**：走本仓库既定的码化出口 `ExternalText.Resolve(<族>IssueKey(issue.Code), "
            + "issue.Message, issue.Args)`（薄封装同 `ExternalText.Describe` / "
            + "`ProjectSettingsView.LocalizeIssue`）⇒ 界面切英文时那一半也跟着翻。\n"
            + "  · **新增**的那一处 ⇒ 先确认它的记录体**有没有 `Code`**：有 ⇒ 接出口；"
            + "**没有** ⇒ 那是「先给产出方建码」（`SvgParseError` / `CsvImportIssue` 那一路）。\n"
            + "  · ⚠ 面已在**阶段 104 收官（基线清空 = 零容忍）** ⇒ 这里**没有**"
            + "「变少也得改基线」那一半了：任何一处命中都是新缺陷。\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 自检 + **收官后的主判据**：语料与面都必须真的读到东西（否则「面 == 空基线」会在规则
    /// 失配时恒真），且面必须是**空的**。
    /// </summary>
    [Fact]
    public void Face_IsRetired_AndTheRuleIsStillAlive()
    {
        var sources = AppSourceScanner.ScanAllProductSources().ToList();
        var sites = CollectSites(sources);

        Assert.True(sources.Count >= 300, $"只扫到 {sources.Count} 个产品源文件 —— 扫描面失效了");
        Assert.True(sources.Sum(s => s.Text.Length) >= 400_000,
            $"语料总长只有 {sources.Sum(s => s.Text.Length)} 字符 —— 读到的可能不是真源码");

        // 规则必须**真在语料里命中过「拿 issues 列表」这个形状**：正则一失配，面会变成 0，
        // 而"0 == 基线的 1" 是**不相等**的 ⇒ 那条判据会红，但它红得**指不出原因**（会读成
        // "站点没了"）。这条自检把"规则还活着"单独钉住。下界刻意不贴实测值（只做量级判断）。
        var issueReads = sources.Sum(s => IssuesMember.Matches(SourceScanner.StripComments(s.Text)).Count);
        Assert.True(issueReads >= 5, $"语料里只命中 {issueReads} 处 Issues 读取 —— 规则可能失配了");

        // 有码的产出方也必须真在面里 —— 否则这条面守的是一个仓库里不存在的东西。
        // （两类记录体：通讯族的 `ValidationSeverity` 与硬件族的同名枚举。）
        var producers = sources.Count(s => s.Text.Contains("ValidationSeverity.Error", StringComparison.Ordinal));
        Assert.True(producers >= 3, $"只找到 {producers} 个文件在构造校验错误 —— 产出方可能不在面里");

        // 阶段 104 **收官**：这一档从"定量清单"翻成"零容忍" —— 面必须是空的。
        // ⚠ 它替换掉了原先的 `sites.Count >= 1`：那一条量的是"解析口径还活着"，
        // 而那件事现在由上面的 `issueReads >= 5` 单独担保（一件事一条断言，别塞在一起）。
        Assert.Empty(sites);
    }

    /// <summary>
    /// 合成语料的负控（不碰磁盘）。每支**单独**证明一件事，含一条**期望"看不见"**的对照。
    /// ⚠ 语料一律先落成局部常量再断言：`TestAssertionStyleGuardTests` 会把
    /// 「同一行里既有 `Assert.Contains(`、又有含 `(` 与 `,` 的字面量」判成精确调用文本断言。
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        const string Path_ = "Views/Synthetic.cs";

        // ── NC1：违规形状 ⇒ 是站点，且 Diff 报「新增」 ──
        const string Bad =
            "void M() { var t = string.Join(\", \", r.Issues.Select(i => i.Message)); }";
        var badSites = CollectSites(new[] { (Path_, Bad) });
        var badProblems = Diff(badSites);
        Assert.Single(badSites);
        Assert.Contains(badProblems, p => p.Contains("新增", StringComparison.Ordinal));

        // ── NC2：同一处改成码化出口 ⇒ 不再是站点（证明谓词就是 `.Message`） ──
        const string Good =
            "void M() { var t = string.Join(\", \", r.Issues.Select(i => ExternalText.Describe(i))); }";
        Assert.Empty(CollectSites(new[] { (Path_, Good) }));

        // ── NC3：同一行写在注释里 ⇒ 不是站点（证明剥注释生效） ──
        var commented = "// " + Bad + " 不许这么写";
        Assert.Empty(CollectSites(new[] { (Path_, commented) }));

        // ── NC4：产出侧的 `.Issues.Add(…)` ⇒ 不是站点（证明前瞻排除了 `Add` / `Clear`） ──
        const string ProducerSide =
            "void M() { r.Issues.Add(AiValidationIssue.Of(k, ex.Message)); }";
        Assert.Empty(CollectSites(new[] { (Path_, ProducerSide) }));

        // ── NC5：**期望"看不见"**的一档（中转变量）—— 钉住上面自陈的那条盲区 ──
        // 它**不是**要修的形状（走出口才是），而是"位置式规则看不见它"的实证：
        // 将来若有人把规则改成作用域感知，这一支会翻成"是站点"，那时请连同类注释一起改。
        const string ViaLocalVariable =
            "void M() { var issues = r.Issues; var t = issues[0].Message; }";
        Assert.Empty(CollectSites(new[] { (Path_, ViaLocalVariable) }));

        // ── NC6：面缩小时**也必须报**（"双向"那一半）——
        //    阶段 104 基线清空后，真语料上再也触发不到这一半 ⇒ 喂**合成基线**单独钉住它
        //    （与语料同一个理由：判据的输入当参数传入，负控才不必去改共享状态）。
        var syntheticBaseline = new Dictionary<string, int>(StringComparer.Ordinal) { [Path_] = 1 };
        Assert.Contains(Diff(Array.Empty<string>(), syntheticBaseline),
            p => p.Contains("已消失", StringComparison.Ordinal));

        // 反向：基线**为空**时不许报"已消失"（否则收官后这条判据会自说自话地红）
        Assert.Empty(Diff(Array.Empty<string>()));
    }
}
