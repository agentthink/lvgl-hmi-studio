using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HmiEditor.App.Services;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「**SVG 解析失败的失败原因必须由字典决定**」的守卫 —— 登记表 §4 <b>#61a</b> 的第一刀（阶段 87）。
///
/// <para><b>它守的是哪一类缺陷</b>：<c>SvgParser.Parse</c> 的失败以前是**裸中文字符串**，
/// 两条路把它送到用户眼前，两条路都是中英混排：</para>
/// <list type="number">
///   <item><b>返回值档</b> —— App 的「应用 SVG 资源」两处站点直接
///     <c>Format("asset.svg.parse.fail", error)</c>（<c>Wiring.DockAggregate</c> /
///     <c>AssetManagerView</c>）；</item>
///   <item><b>异常档</b> —— <c>IconGenerator</c> / <c>ImageGenerator</c> 把它包进
///     <c>InvalidOperationException($"SVG 生成无效: {error}")</c>，再由 <c>ai.gen.failed</c>
///     那 8 处的宽捕获直通 <c>ex.Message</c>。</item>
/// </list>
///
/// <para><b>⚠ 本判据与「直通面守卫」是一对，缺一不可</b>（阶段 87 实测）：
/// <c>ExceptionMessagePassthroughFaceTests</c> 数的是"还有多少站点把 <c>ex.Message</c> 直通模板" ——
/// 一个站点只要**不再那样写**就从那张面上消失，而"换成什么"它不管。
/// 本阶段把那 8 处换成 <c>ExternalText.FailureReason(ex)</c>（**真收口**，因为同阶段给产出方加了码），
/// 但**同样的动作在产出方没有码时就是改名式假闭环**（阶段 83 钉死的那种）。
/// ⇒ 两张面要一起看：<b>直通面变小 × 本判据证明产出方有码且渲染真的走字典</b> = 真迁移。</para>
///
/// <para><b>码集中在 Core、键集中在 App</b>：<c>HmiEditor.Core</c> 拿不到字典服务
/// （依赖方向 App → Core）⇒ 它只声明**稳定的码** + **中文回落原文**，键由
/// <c>ExternalText.SvgIssueKey</c> 统一推导（<c>svg.issue.&lt;码小写&gt;</c>）。
/// 与 <c>HmiPackageIssueCodes</c>（M9）/ <c>SystemFailureCodes</c>（M10）/
/// <c>AiIssueCodes</c>（M8）同一套约定。</para>
///
/// <para><b>为什么这个族是「返回值 + 异常」两条搬运方式</b>：A 类的产出方是**纯函数**
/// （<c>SvgParser</c> / <c>GeneratedCodeValidator</c> / <c>CsvVariableImporter</c> /
/// <c>RecipeManager</c>），它们把失败做成**返回值**、根本不抛异常 ⇒
/// 「加码表 + 带码异常」这个模板在它们的站点上无处安放（站点 catch 到的是 IO / UI 的异常）。
/// 所以 SVG 这一族走「返回值带码」：<see cref="SvgParseError"/> 可直接返回
/// （渲染走 <see cref="ExternalText.Describe(SvgParseError)"/>），
/// 需要穿过 AI 层返回契约的那条路用 <see cref="SvgParseException"/> 把它包起来。
/// **两条路的码只有一个来源、键只有一套** —— 这就是本判据存在的前提。</para>
///
/// <para><b>诚实边界（本判据断不了的事）</b>：① 它只断「渲染等于字典模板」，断不了
/// **翻译质量**；② <see cref="SvgParseIssueCodes.XML_INVALID"/> 那一档的 .NET 原文
/// **故意不进模板**（英文 + 行列号）—— 本判据只钉「原文还在
/// <see cref="SvgParseError.Message"/> 里」（排障不丢信息），不加要求它进界面。</para>
/// </summary>
public class SvgParseIssueLocalizationGuardTests
{
    private readonly ITestOutputHelper _out;

    public SvgParseIssueLocalizationGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>毒化记号：拿它当"中文回落原文"，凡在渲染里冒头就说明字典键没被查到。</summary>
    private const string Poison = "<<中文回落原文未被使用>>";

    /// <summary>模板占位符（<c>{0}</c> 这种）。本族 4 个码全是 **0 实参**。</summary>
    private static readonly Regex Placeholder = new(@"\{\d", RegexOptions.Compiled);

    /// <summary>CJK 汉字 —— 用来抓"en-US 的值其实没翻"。</summary>
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>AI 侧的两个产出方 —— 它们必须抛**带码**异常，不许退回裸 BCL 异常 + 中文串。</summary>
    private static readonly string[] AiProducers =
    {
        "src/HmiEditor.AI/Generators/IconGenerator.cs",
        "src/HmiEditor.AI/Generators/ImageGenerator.cs",
    };

    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    // ══════════════════════════ 取数（同源出口 —— 负控与 M11 都复用它） ══════════════════════════

    /// <summary>
    /// 反射枚举 <see cref="SvgParseIssueCodes"/> 里声明的全部码。
    ///
    /// <para><b>是同源出口</b>：本文件的多条判据与 <c>LocalizationKeyReachabilityGuardTests</c>
    /// 的 **M11** 都调它，不各写一份反射 —— 抄两份必然漂移，而漂移的表现是
    /// "一个守卫绿、另一个红"，看不出哪边对。</para>
    ///
    /// <para>⚠ <c>SvgParseIssueCodes</c> 是 <c>public</c>，所以不带 <c>BindingFlags.NonPublic</c>
    /// 也对（<c>SystemFailureCodes</c> 是 <c>internal</c>，那边**必须**带）——
    /// 但"反射到 0 个码"会让双向闭包**恒真**，所以下面有一条下界断言兜底。</para>
    /// </summary>
    internal static List<string> DeclaredCodes()
        => typeof(SvgParseIssueCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    /// <summary>一次真实失败：人读的标签 + 走**真实产出路径**拿到的对象。</summary>
    internal sealed record Hit(string Label, SvgParseError Error);

    /// <summary>
    /// 4 个码各自走**真实产出路径**（不是手搓 <c>SvgParseError</c>）——
    /// 手搓的话，"码表与产出方脱节"这件事就永远测不到。
    /// </summary>
    internal static List<Hit> RealFailures()
    {
        var hits = new List<Hit>();

        var empty = SvgParser.Parse("   ");
        hits.Add(new Hit("SvgParser.Parse(空白内容)", empty.Error!));

        var notSvg = SvgParser.Parse("<html><body/></html>");
        hits.Add(new Hit("SvgParser.Parse(根不是 svg)", notSvg.Error!));

        var badXml = SvgParser.Parse("<svg><rect></svg>");
        hits.Add(new Hit("SvgParser.Parse(坏 XML)", badXml.Error!));

        // 第 4 个码由**消费方**构造：解析成功但一条形状都没有（空 <svg/> 是合法输入，
        // 所以 SvgParser 对它返回成功 —— 只有 AI 那条路把它当失败）。
        hits.Add(new Hit("SvgParseError.NoShapes（消费方构造）", SvgParseError.NoShapes));

        return hits;
    }

    // ══════════════════════════ 判据逻辑（纯函数 —— 负控直接调它们） ══════════════════════════

    /// <summary>两本字典对每个码的完备性与**成对性**。</summary>
    internal static List<string> PairingViolations(
        IEnumerable<string> codes, Dictionary<string, string> zh, Dictionary<string, string> en)
    {
        var bad = new List<string>();

        foreach (var code in codes)
        {
            var key = ExternalText.SvgIssueKey(code);

            if (!zh.TryGetValue(key, out var z)) { bad.Add($"{key}：zh-CN 缺键"); continue; }
            if (!en.TryGetValue(key, out var e)) { bad.Add($"{key}：en-US 缺键"); continue; }

            // 4 个码全是 0 实参 ⇒ 模板里多一个占位符就会在界面上渲染出字面的 {0}
            // （LocalizationService.Format 在"占位符个数 ≠ 实参个数"时不抛异常、只原样返回）。
            if (Placeholder.IsMatch(z) || Placeholder.IsMatch(e))
                bad.Add($"{key}：模板里有占位符，但这一族全是 0 实参 ⇒ 界面上会渲染出字面的 {{0}}");

            if (Cjk.IsMatch(e))
                bad.Add($"{key}：en-US 的值里有中文字符 ⇒ 没翻，或直接抄了 zh");

            if (string.Equals(z, e, StringComparison.Ordinal))
                bad.Add($"{key}：两本字典的值一字不差 ⇒ 至少有一本没翻");
        }

        return bad;
    }

    /// <summary>码表的**双向闭包**：声明了产不出 = 死码；产出了没声明 = 漏登记。</summary>
    internal static List<string> ClosureViolations(IEnumerable<string> declared, IEnumerable<string> produced)
    {
        var d = new HashSet<string>(declared, StringComparer.Ordinal);
        var p = new HashSet<string>(produced, StringComparer.Ordinal);
        var bad = new List<string>();

        foreach (var c in d.Except(p).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"声明了但真实产不出：{c} ⇒ 死码，它和它的字典键都永远不会被用到");

        foreach (var c in p.Except(d).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"产出了但没声明：{c} ⇒ 键不会进字典，界面会回落中文原文");

        return bad;
    }

    /// <summary>
    /// **产出方不许退化**：AI 侧那两个生成器必须抛带码的 <see cref="SvgParseException"/>，
    /// 且不许再把裸中文原文模板拼进异常。
    ///
    /// <para><b>为什么这条非有不可</b>：那两处与 <c>ai.gen.failed</c> 的 8 个站点是**成对**的 ——
    /// 站点换成 <c>ExternalText.FailureReason(ex)</c> 之后，<c>ex</c> 若退回裸
    /// <c>InvalidOperationException</c>，<c>FailureReason</c> 接不住、走第 ④ 支渲染中文原文，
    /// 而**直通面那张面是绿的**（站点形状确实变了）⇒ 又是"面绿着、收益为零"。
    /// 本函数 + <see cref="LeakViolations"/> 合起来才把这条路钉死。</para>
    ///
    /// <para>剥注释是必须的：讲规则的注释里会写出反例（登记 #48 踩过三次）。</para>
    /// </summary>
    internal static List<string> ProducerViolations(
        IEnumerable<(string Relative, string Text)> sources, IEnumerable<string> files)
    {
        var bad = new List<string>();

        foreach (var file in files)
        {
            var hit = sources.FirstOrDefault(s => string.Equals(s.Relative, file, StringComparison.Ordinal));
            if (hit.Text is null) { bad.Add($"{file}：语料里找不到这个文件（扫描面失效？）"); continue; }

            var code = SourceScanner.StripComments(hit.Text);

            if (!code.Contains("throw new SvgParseException(", StringComparison.Ordinal))
                bad.Add($"{file}：没有抛带码的 SvgParseException ⇒ 这条路的失败原因会回落中文原文");

            if (code.Contains("SVG 生成无效", StringComparison.Ordinal))
                bad.Add($"{file}：又把裸中文原文模板拼进异常了 ⇒ 码这条路被绕开");
        }

        return bad;
    }

    /// <summary>
    /// 渲染结果必须**等于字典模板**、且**不许出现回落原文**。
    /// <paramref name="rows"/> 的 <c>Candidates</c> 是两本字典的值（当前语言未知 ⇒ 取并集判定）。
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
                bad.Add($"{label}：渲染 {rendered} 既不等于 zh 也不等于 en 的字典值 ⇒ 有别的来源");
        }

        return bad;
    }

    // ══════════════════════════ 判据 ══════════════════════════

    /// <summary>
    /// 主判据：**每一条真实失败**，走**两条搬运方式**（<c>Describe</c> 返回值档 /
    /// <c>FailureReason</c> 异常档）渲染，结果都必须来自字典。
    ///
    /// <para><b>为什么用毒化而不是拿真原文比对</b>：拿真原文只能测"今天恰好翻了没"，
    /// 毒化测的是**"渲染这一半确实由字典决定"** —— 后者才是这条链路的性质。</para>
    /// </summary>
    [Fact]
    public void EveryFailure_RendersTheDictionaryTemplate_NeverTheChineseFallback()
    {
        var zh = Zh;
        var en = En;
        var rows = new List<(string Label, string Rendered, string Key, string[] Candidates)>();

        foreach (var hit in RealFailures())
        {
            var key = ExternalText.SvgIssueKey(hit.Error.Code);
            var candidates = new[] { zh.GetValueOrDefault(key), en.GetValueOrDefault(key) }
                .Where(v => v is not null)
                .Select(v => v!)
                .ToArray();

            var poison = new SvgParseError(hit.Error.Code, Poison, hit.Error.Args);

            rows.Add(($"{hit.Label} · Describe", ExternalText.Describe(poison), key, candidates));
            rows.Add(($"{hit.Label} · FailureReason",
                ExternalText.FailureReason(new SvgParseException(poison)), key, candidates));
        }

        JudgeReadings.Write(_out, "SVG 解析失败的失败原因（#61a · svg.issue.*）",
            rows.Select(r => $"    {r.Label,-38} {r.Key,-26} ⇒ {r.Rendered}").ToList());

        var problems = LeakViolations(rows, Poison);
        Assert.True(problems.Count == 0,
            "有失败原因没走字典（登记 #61a / 阶段 87）。正确动作是补 `svg.issue.<码小写>` 的字典键，"
            + "**不是**把裸中文串再拼回去（那会让英文界面下中英混排）：\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>两本字典对每个码都齐 + 成对（en 不许含中文、两侧不许一字不差、0 实参不许有占位符）。</summary>
    [Fact]
    public void BothDictionaries_CarryEveryCode_AndAreActuallyPaired()
    {
        var codes = DeclaredCodes();
        Assert.True(codes.Count >= 3, $"只反射到 {codes.Count} 个码 —— 取数失效（反射面空了，下面的判定会恒真）");

        var problems = PairingViolations(codes, Zh, En);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **原文不许丢**：<see cref="SvgParseIssueCodes.XML_INVALID"/> 那一档的
    /// <see cref="SvgParseError.Message"/> 里必须还带 .NET 的原始消息（行号列号都在里面）。
    ///
    /// <para>原文**故意不进界面**（它是英文、且把解析器内部坐标抖给用户，登记 #59），
    /// 所以这条是**唯一**能钉住"排障信息没被顺手删掉"的地方。期望值不写死措辞 ——
    /// 现场 catch 一次 <see cref="XDocument.Parse"/> 拿它，措辞随 .NET 版本改时判据跟着走。</para>
    /// </summary>
    [Fact]
    public void XmlInvalid_StillCarriesTheDotNetOriginal()
    {
        const string bad = "<svg><rect></svg>";

        string raw = "";
        try { XDocument.Parse(bad, LoadOptions.None); }
        catch (Exception ex) { raw = ex.Message; }

        Assert.False(string.IsNullOrWhiteSpace(raw), "前置：这个输入在 .NET 上应当抛（否则下面整条断的是空的）");

        var error = SvgParser.Parse(bad).Error;
        Assert.NotNull(error);
        Assert.Equal(SvgParseIssueCodes.XML_INVALID, error!.Code);
        Assert.Contains(raw, error.Message, StringComparison.Ordinal);
    }

    /// <summary>码表**双向闭包**：声明的码都真的产得出，产出的码都声明了。</summary>
    [Fact]
    public void DeclaredCodes_AndProducedCodes_AreTheSameSet()
    {
        var produced = RealFailures().Select(h => h.Error.Code).Distinct(StringComparer.Ordinal).ToList();
        var problems = ClosureViolations(DeclaredCodes(), produced);

        JudgeReadings.Write(_out, "SVG 解析失败码表（#61a）",
            new List<string> { $"    声明 {DeclaredCodes().Count} 个 / 真实产出 {produced.Count} 个：{string.Join(", ", produced.OrderBy(x => x, StringComparer.Ordinal))}" });

        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **诚实边界**：产出方若退回**裸异常**，这一半就仍是中文原文 —— 本判据把这个事实**正面断言**下来，
    /// 免得下一个人以为"只要站点换成 <c>FailureReason</c> 就算收口了"。
    ///
    /// <para>它与主判据成对：主判据说"带码的走字典"，这条说"没码的照旧回落"。
    /// 只断前者 ⇒ 会以为覆盖全部；只断后者 ⇒ 毫无收益。</para>
    /// </summary>
    [Fact]
    public void BareException_StillFallsBackToTheRawMessage_SoTheCodeIsWhatMatters()
    {
        var bare = new InvalidOperationException("SVG 生成无效: 根元素不是 <svg>");
        Assert.Equal(bare.Message, ExternalText.FailureReason(bare));

        // 而同一个原因一旦带上码，渲染就换成了字典短句 —— 这两句合起来就是"码是收益的唯一来源"。
        var coded = new SvgParseException(new SvgParseError(SvgParseIssueCodes.ROOT_NOT_SVG, bare.Message));
        Assert.NotEqual(bare.Message, ExternalText.FailureReason(coded));
    }

    /// <summary>AI 侧两个产出方必须**抛带码异常**（这也是 <c>ai.gen.failed</c> 8 处换出口的前提）。</summary>
    [Fact]
    public void AiGenerators_ThrowTheCodedException_NotABareBclOne()
    {
        var problems = ProducerViolations(AppSourceScanner.ScanAllProductSources(), AiProducers);

        JudgeReadings.Write(_out, "AI 侧 SVG 失败的抛出形状（#61a）",
            AiProducers.Select(f => $"    {f}").ToList());

        Assert.True(problems.Count == 0,
            "AI 侧的 SVG 失败必须带码 —— 否则 ai.gen.failed 那 8 处换成 FailureReason(ex) 会**收益为零**"
            + "（面绿着、用户看到的中文原文一字不变）：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 负控 4 组，两两成对 —— 每组都要求"该红的红、该绿的绿"，
    /// 否则证明不了判据真的在盯那件事（本项目对负控的一贯要求）。
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        var codes = DeclaredCodes();
        var zh = Zh;
        var en = En;

        // ── NC1 · 成对性：正控绿 ──
        Assert.Empty(PairingViolations(codes, zh, en));
        Assert.Empty(ClosureViolations(codes, RealFailures().Select(h => h.Error.Code)));

        // NC1a：en 的值改回中文（未翻）⇒ 点名；正控不受影响
        var enCopy1 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        var key0 = ExternalText.SvgIssueKey(codes[0]);
        enCopy1[key0] = zh[key0];
        Assert.Contains(PairingViolations(codes, zh, enCopy1), v => v.Contains("中文字符", StringComparison.Ordinal));

        // NC1b：摘掉一个键 ⇒ 点名缺键（字典键不是"加了就完事"的装饰）
        var enCopy2 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy2.Remove(key0);
        Assert.Contains(PairingViolations(codes, zh, enCopy2), v => v.Contains("缺键", StringComparison.Ordinal));

        // NC1c：模板里塞一个 {0}（这一族全是 0 实参）⇒ 点名占位符
        var zhCopy = new Dictionary<string, string>(zh, StringComparer.Ordinal);
        zhCopy[key0] += " {0}";
        Assert.Contains(PairingViolations(codes, zhCopy, en), v => v.Contains("占位符", StringComparison.Ordinal));

        // ── NC2 · 双向闭包：**两侧都要报** ──
        Assert.Contains(ClosureViolations(codes.Concat(new[] { "NOT_A_REAL_CODE" }), codes),
            v => v.Contains("死码", StringComparison.Ordinal));
        Assert.Contains(ClosureViolations(codes, codes.Concat(new[] { "NOT_A_REAL_CODE" })),
            v => v.Contains("没声明", StringComparison.Ordinal));

        // ── NC3 · 产出方 ──
        // ⚠ ①②④ 的合成语料必须**每个文件都在**（Relative 一字不差）：本函数把"语料里缺文件"
        // 也算违规（那是它的下界 —— 扫描面失效时会静默全绿），只喂一个文件会踩到那一支；
        // ③ **故意**踩那一支，用来证明这个下界真的在。
        const string coded = "throw new SvgParseException(error ?? SvgParseError.NoShapes);";
        static (string Relative, string Text)[] Corpus(string target, string body)
            => AiProducers.Select(f => (f, f == target ? body : coded)).ToArray();

        // ① 退回裸异常 + 中文原文模板 ⇒ **两条**都点名（"没有抛带码异常" + "又拼起裸原文"）
        var caught = ProducerViolations(
            Corpus(AiProducers[0], "throw new InvalidOperationException($\"SVG 生成无效: x\");"), AiProducers);
        Assert.Contains(caught, v => v.Contains("没有抛带码的 SvgParseException", StringComparison.Ordinal));
        Assert.Contains(caught, v => v.Contains("裸中文原文模板", StringComparison.Ordinal));

        // ② 合规写法 ⇒ 无话可说（证明它不是"永远返回违规"的判据）
        Assert.Empty(ProducerViolations(Corpus(AiProducers[0], coded), AiProducers));

        // ③ 语料里**缺**一个目标文件 ⇒ 必须报"找不到" ——
        //    否则扫描面整体失效（返回空语料）时会**静默全绿**：这是本函数唯一的下界，不能省。
        //    ⚠ 不能用 Corpus(...)：它按 f == target 分支，无论 target 传谁都保证清单上的文件**都在**，
        //    只是内容不同 ⇒ 恰好把这一支绕过去了（第一版就栽在这里）。
        Assert.Contains(
            ProducerViolations(new[] { (AiProducers[1], coded) }, AiProducers),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ④ 注释里写的反例不算（规则不许被自己的文档满足，登记 #48）：
        //    该文件**既有合规的带码抛出、又有一段把反例写进注释** ⇒ 剥注释后只该看前者。
        //    ⚠ 不能只喂一段注释：那样剥完是**空代码**，"必须抛带码异常"那一支会先亮
        //    （第一版就这么写的，红在隔壁那条上，反而没验到"注释不算"这件事）。
        Assert.Empty(ProducerViolations(
            Corpus(AiProducers[1],
                coded + "\n// 反例（不许这么写）：throw new InvalidOperationException(\"SVG 生成无效: x\");"),
            AiProducers));

        // ── NC4 · 泄漏：回落原文冒头 ⇒ 点名；字典值 ⇒ 绿 ──
        var leak = new[] { ("演示", $"导入失败: {Poison}", key0, new[] { zh[key0] }) };
        Assert.Contains(LeakViolations(leak, Poison), v => v.Contains("中文回落原文", StringComparison.Ordinal));

        var clean = new[] { ("演示", zh[key0], key0, new[] { zh[key0] }) };
        Assert.Empty(LeakViolations(clean, Poison));
    }
}
