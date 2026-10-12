using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **BCL / 系统层失败文案**的本地化守卫（登记表 §4 <b>#61e</b>：给 BCL 异常**类型**建码）。
///
/// <para><b>它补的是哪半边</b>：<c>ExternalText.FailureReason</c> 此前只在
/// <c>ex is HmiPackageException</c> 上分叉 ⇒ 凡 BCL 抛的异常都回落 <c>ex.Message</c>，
/// 而那是**英文**（阶段 84 / 85 探针把 <c>CurrentUICulture</c> 切中英文实测：
/// 消息**一字不变**）⇒ 中文界面下长期中英混排（<c>保存失败: Access to the path '…' is denied.</c>）。
/// 原文**不可翻译** ⇒ 唯一出路是**按异常类型码化**。</para>
///
/// <para><b>触发面：真的去撞 BCL，不写"预期码清单"</b>（与
/// <c>PackageIssueLocalizationGuardTests</c> 同一条纪律）。清单会与实现分家，
/// 而分家之后守卫检查的是**清单**、不是代码 —— 那是"被自己的清单喂活"的恒绿判据。
/// 每个场景都在 <c>TestTempDir</c> 里**真实触发**一次 IO 失败，拿到的异常就是读数。</para>
///
/// <list type="number">
///   <item><b>类型 → 码 → 键</b>：每个真触发的异常都能取到码，且键在**两本字典**都能解析；</item>
///   <item><b>成对：zh 渲染中文 / en 渲染英文</b> —— 切到 en 必须**换一份文案**（不是同一句），
///   且两侧的字符脚本各自正确。<b>光看一侧会在"根本没渲染出东西"时也成立</b>
///   （阶段 82 的成对断言教训）；</item>
///   <item><b>两侧都不含 BCL 原文 / .NET 内部记号</b>（<c>Access to the path</c> /
///   <c>Could not find file</c> / <c>LineNumber</c> …）；</item>
///   <item><b>码表有序性的不变量</b>：祖先不许出现在后代**之后** —— 破坏它**没有编译错误、
///   只是静默匹配错**（<c>IOException</c> 会吃掉 <c>FileNotFoundException</c>）；</item>
///   <item><b>诚实边界</b>：没实测过的类型仍然回落原文（不许把"看着像 IO"的类型塞进码表）。</item>
/// </list>
///
/// <para><b>⚠ 它管不着的那件事</b>：<b>"原文不许丢"</b>（登记表要求的判据②）**不在这里** ——
/// 它要换全局 <c>AppLog.Current</c>、跑 <c>UiOperationGuard.RunAsync</c>，而那两个全局量
/// 归 <c>tests/HmiEditor.Core.Tests/UiOperationGuardTests.cs</c> 的 <c>AppLog-global</c> 集合管
/// （那里已有 <c>RecordingLogger</c> 与"禁止并行"的约定）。**同一件事只留一条判据**，
/// 分成两处写必然漂移 ⇒ 那条在那边。</para>
///
/// <para><b>为什么必须挂 <c>Language-global</c> 集合</b>：成对判据要
/// <c>AppLocalization.Service.SwitchLanguage</c>，而动的是**静态单例** ——
/// 仓库既定做法就是加入 <c>[Collection("Language-global")]</c>（<c>DisableParallelization = true</c>，
/// 定义在 <c>LocalizationRefreshUiTests</c>），而不是自己发明一套隔离。</para>
/// </summary>
[Collection("Language-global")]
public class SystemIssueLocalizationGuardTests
{
    private readonly ITestOutputHelper _out;

    public SystemIssueLocalizationGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>一次真实触发的读数：**标签 + 异常本身**（原文要留着做泄漏比对）。</summary>
    internal sealed record Hit(string Label, Exception? Ex)
    {
        /// <summary>该场景真的抛了吗（<c>null</c> = 没抛 ⇒ 探针失效，不是"这档不存在"）。</summary>
        internal bool Threw => Ex is not null;

        /// <summary>取码（<c>null</c> = 类型不在码表里）。</summary>
        internal string? Code => Ex is null ? null : SystemFailureCodes.CodeFor(Ex);
    }

    /// <summary>同一场景在三种形式下的读数：原文 / zh 渲染 / en 渲染。</summary>
    internal sealed record Rendered(string Label, string Code, string Raw, string Zh, string En);

    // ══════════════════════════ 触发面 ══════════════════════════

    /// <summary>
    /// 真实撞一遍 BCL —— 场景覆盖本仓库实际调用面（<c>File.*</c> / <c>Directory.*</c> /
    /// <c>ZipArchive</c> / <c>JsonDocument</c>），与探针
    /// <c>tools/probe-bcl-exception-types.cs</c> 的 A–F 段一一对应。
    /// </summary>
    internal static List<Hit> Probe()
    {
        var root = TestTempDir.NewDir("sysissue-probe");
        var dirAsFile = Path.Combine(root, "adir");
        Directory.CreateDirectory(dirAsFile);
        var plain = Path.Combine(root, "plain.txt");
        File.WriteAllText(plain, "x");
        var missing = Path.Combine(root, "nope", "x.txt");

        var hits = new List<Hit>();

        void Catch(string label, Action act)
        {
            try
            {
                act();
                hits.Add(new Hit(label, null));
            }
            catch (Exception ex)
            {
                hits.Add(new Hit(label, ex));
            }
        }

        Catch("File.WriteAllText 目标是目录", () => File.WriteAllText(dirAsFile, "x"));
        Catch("File.ReadAllText 文件不存在", () => File.ReadAllText(Path.Combine(root, "ghost.txt")));
        Catch("File.WriteAllText 父目录不存在", () => File.WriteAllText(missing, "x"));
        Catch("File.Copy 目标目录不存在", () => File.Copy(plain, missing));
        Catch("Directory.CreateDirectory 撞同名文件", () => Directory.CreateDirectory(plain));
        Catch("Directory.Delete 非空目录", () =>
        {
            File.WriteAllText(Path.Combine(dirAsFile, "inner.txt"), "x");
            Directory.Delete(dirAsFile);
        });
        Catch("整条路径超长", () =>
        {
            var deep = root;
            for (var i = 0; i < 40; i++) deep = Path.Combine(deep, new string('b', 200));
            File.WriteAllText(Path.Combine(deep, "x.txt"), "x");
        });
        Catch("ZipArchive 撞非 ZIP 字节", () =>
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes("this is not a zip archive at all"));
            using var _ = new ZipArchive(ms, ZipArchiveMode.Read);
        });
        Catch("JsonDocument.Parse 坏 JSON", () => JsonDocument.Parse("{ 这不是 JSON"));

        return hits;
    }

    /// <summary>
    /// 反射枚举 <see cref="SystemFailureCodes"/> 里声明的全部码。
    ///
    /// <para><b>是同源出口</b>：本文件的多条判据与
    /// <c>LocalizationKeyReachabilityGuardTests</c> 的 **M10** 都调它，不各写一份反射
    /// —— 抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"，看不出哪边对。</para>
    ///
    /// <para>⚠ 这个类型是 <c>internal</c> ⇒ 必须带 <c>BindingFlags.NonPublic</c>
    /// （<c>HmiPackageIssueCodes</c> 是 <c>public</c>，那边不带也过 —— 照抄会**反射到 0 个码**，
    /// 而"0 个码"会让双向闭包判据**恒真**）。下面那条下界断言就是防这个的。</para>
    /// </summary>
    internal static List<string> DeclaredCodes()
        => typeof(SystemFailureCodes)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(v => v is not null)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

    /// <summary>单一来源的码表（判据按参数收它，负控才能塞坏形状）。</summary>
    internal static (Type ExceptionType, string Code)[] TypeMap() => SystemFailureCodes.TypeMap;

    /// <summary>阶段 84 / 85 探针**实测**到的 .NET 原文记号（不是猜的）。</summary>
    private static readonly string[] RawDotNetMarkers =
    {
        "Access to the path",
        "Could not find file",
        "Could not find a part of the path",
        "already exists",
        "Directory not empty",
        "No space left on device",
        "End of Central Directory",
        "is an invalid start of",
        "LineNumber",
        "BytePositionInLine",
        "too long, or a component",
    };

    // ══════════════════════════ 判据 ══════════════════════════

    /// <summary>自检：探针的每个场景都必须真的抛 —— 没抛说明场景失效了（不是"这档不存在"）。</summary>
    [Fact]
    public void Probe_ActuallyTriggersEveryScenario()
    {
        var silent = Probe().Where(h => !h.Threw).Select(h => h.Label).ToList();

        Assert.True(silent.Count == 0,
            "以下探针场景**没有抛** —— 触发面失效了（依赖它的判据会变成恒真）：\n"
            + string.Join("\n", silent));
    }

    /// <summary>每个真触发的异常都要取到码，且键在**两本字典**都能解析。</summary>
    [Fact]
    public void EveryTriggeredType_MapsToAKeyResolvableInBothLanguages()
    {
        var hits = Probe().Where(h => h.Threw).ToList();
        var missing = new List<string>();

        foreach (var hit in hits)
        {
            if (hit.Code is null)
            {
                missing.Add($"{hit.Label}：{hit.Ex!.GetType().Name} **不在码表里** ⇒ 会回落英文原文");
                continue;
            }

            var key = ExternalText.SystemIssueKey(hit.Code);
            foreach (var lang in new[] { "zh-CN", "en-US" })
                if (!PluginTextLocalizationGuardTests.LoadDictionary(lang).ContainsKey(key))
                    missing.Add($"{hit.Label}：{lang} 缺键 {key}");
        }

        _out.WriteLine("场景 | 异常类型 | 码 | 原文");
        foreach (var hit in hits)
            _out.WriteLine($"  {hit.Label,-34} | {hit.Ex!.GetType().Name,-28} | {hit.Code ?? "(无码)",-22} | {hit.Ex.Message}");

        Assert.True(hits.Count >= 8, $"只触发到 {hits.Count} 个场景 —— 触发面可能失效了");
        Assert.True(missing.Count == 0,
            "以下系统失败**取不到可翻译的文案**（登记 #61e）：\n" + string.Join("\n", missing));
    }

    /// <summary>
    /// <b>成对</b>：切到 zh 必须渲染中文、切到 en 必须渲染**另一份**英文。
    ///
    /// <para><b>为什么必须成对</b>：只断言"渲染出来是中文"在两种情形下也成立 ——
    /// ① 字典根本缺键（回落的可能是中文原文）；② 语言切换压根没生效（两次都拿同一份）。
    /// 两侧都断言就排除了这两样（阶段 82 的成对断言教训）。</para>
    /// </summary>
    [Fact]
    public void BothLanguages_RenderDistinctly_AndInTheirOwnScript()
    {
        var hits = Probe().Where(h => h.Threw && h.Code is not null).ToList();
        var rows = RenderBothWays(hits);
        var bad = rows.Select(r => PairingViolation(r.Code, r.Zh, r.En))
                      .Where(v => v is not null)
                      .Select(v => v!)
                      .ToList();

        Assert.True(hits.Count >= 8, $"只触发到 {hits.Count} 个场景 —— 触发面可能失效了");
        Assert.True(bad.Count == 0,
            "系统失败文案不是**成对**的（登记 #61e 的判据①，两侧都要成立）：\n" + string.Join("\n", bad));
    }

    /// <summary>两侧的文案里都不许出现 BCL 原文或 .NET 内部记号（<b>#61e 的反回归</b>）。</summary>
    [Fact]
    public void RenderedReason_NeverLeaksTheRawDotNetMessage()
    {
        var hits = Probe().Where(h => h.Threw && h.Code is not null).ToList();
        var leaked = LeakViolations(RenderBothWays(hits));

        Assert.True(leaked.Count == 0,
            "失败原因那一半把 BCL 的原始异常消息透传到了用户可见文案上：\n" + string.Join("\n", leaked));
    }

    /// <summary>
    /// <b>接线判据</b>：<c>ExternalText.FailureReason</c> 必须**真的走字典**，不是回落。
    ///
    /// <para>构造：给一个**码表里有的类型**喂一条**毒化消息** ⇒ 渲染结果里一个毒化字符都不许出现。
    /// 这分辨了"查到了字典"与"回落了原文"（只看"渲染出来是中文"分辨不了 —— 二者都可能）。
    /// 断言"必须是两本字典之一"而不是"必须等于 zh"：判据不该假设应用当前语言。</para>
    /// </summary>
    [Fact]
    public void FailureReason_PrefersTheDictionaryOverTheRawMessage()
    {
        const string poison = "<<原文未被使用>>";
        var bad = new List<string>();

        foreach (var code in DeclaredCodes())
        {
            var rendered = ExternalText.FailureReason(ExceptionFor(code, poison));
            var key = ExternalText.SystemIssueKey(code);
            var candidates = new[] { "zh-CN", "en-US" }
                .Select(l => PluginTextLocalizationGuardTests.LoadDictionary(l).GetValueOrDefault(key))
                .Where(v => v is not null)
                .ToList();

            if (rendered.Contains(poison, StringComparison.Ordinal))
                bad.Add($"{code}：渲染结果回落到了原文（「{rendered}」）—— 字典没被查到");
            else if (!candidates.Contains(rendered))
                bad.Add($"{code}：渲染结果既不是 zh 也不是 en 的字典值（「{rendered}」）");
        }

        Assert.True(bad.Count == 0,
            "FailureReason 没有把系统失败的码渲染成字典文案：\n" + string.Join("\n", bad));
    }

    /// <summary>
    /// <b>码表有序性的不变量</b>：某条目的异常类型**不许**是它**前面**某条的派生类型
    /// —— 因为 <c>CodeFor</c> 返回第一个命中的，祖先排在前面会静默吃掉后代
    /// （<c>IOException</c> 会吃掉 <c>FileNotFoundException</c>）。
    /// 这个错误**没有编译错误**，而且"每个类型都取到了码"这类判据**看不见它**
    /// （取到的码是错的、不是空的）⇒ 必须单独钉住。
    /// </summary>
    [Fact]
    public void TypeMap_AncestorsNeverAppearAfterTheirDescendants()
    {
        var violations = OrderingViolations(TypeMap());

        Assert.True(violations.Count == 0,
            "码表的顺序坏了 —— CodeFor 返回第一个命中的条目，祖先排在前面会**静默吃掉**后代：\n"
            + string.Join("\n", violations));
    }

    /// <summary>
    /// 码表与常量声明**双向闭合**：声明了没人挂 = 死码（它和它的字典键永远用不到）；
    /// 表里挂了没声明 = 指向一个不存在的码。
    /// </summary>
    [Fact]
    public void DeclaredCodesAndTypeMap_AreBidirectionallyClosed()
    {
        var declared = DeclaredCodes();

        Assert.True(declared.Count >= 5,
            $"反射只枚举到 {declared.Count} 个码 —— 取数方式可能失效了"
            + "（⚠ SystemFailureCodes 是 internal ⇒ 少了 BindingFlags.NonPublic 会返回 0 个）");

        var violations = ClosureViolations(TypeMap(), declared);
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>
    /// <b>诚实边界</b>：码表**只管实测过的类型** —— 别的异常仍然回落原文
    /// （把"看着像 IO"的类型塞进码表而不补探针场景，是这条边界要防的事）。
    /// </summary>
    [Fact]
    public void UnmappedType_StillFallsBackToTheRawMessage()
    {
        const string raw = "some very specific unmapped failure text";
        var ex = new InvalidOperationException(raw);

        Assert.Null(SystemFailureCodes.CodeFor(ex));
        Assert.Equal(raw, ExternalText.FailureReason(ex));
    }

    // ══════════════════════════ 纯函数（负控要复用） ══════════════════════════

    /// <summary>
    /// 把每个场景在 zh / en 各渲染一遍。
    ///
    /// <para><b>语言切换只在这里做</b>（两次，不是每场景两次）—— 切的是静态单例，
    /// 本类靠 <c>[Collection("Language-global")]</c> 与别的类串行，并且**必然还原**。</para>
    /// </summary>
    internal static List<Rendered> RenderBothWays(IReadOnlyList<Hit> hits)
    {
        var original = AppLocalization.Service.CurrentLanguage;
        try
        {
            AppLocalization.Service.SwitchLanguage("zh-CN");
            var zh = hits.ToDictionary(h => h.Label, h => ExternalText.FailureReason(h.Ex!), StringComparer.Ordinal);
            AppLocalization.Service.SwitchLanguage("en-US");
            var en = hits.ToDictionary(h => h.Label, h => ExternalText.FailureReason(h.Ex!), StringComparer.Ordinal);

            return hits
                .Select(h => new Rendered(h.Label, h.Code!, h.Ex!.Message, zh[h.Label], en[h.Label]))
                .ToList();
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }
    }

    /// <summary>
    /// 泄漏违规（空列表 = 通过）：渲染结果里不许出现<b>整条</b>原文，也不许出现 .NET 原文记号。
    ///
    /// <para><b>两支不是同一条判据的两种写法</b>：①「整条照抄」抓的是<b>根本没去查字典</b>；
    /// ②「记号」抓的是<b>查了字典但把原文拼进了模板</b>（<c>"保存失败: Access to the path…"</c>）。
    /// 只留①会在"包一层壳"时漏掉，只留②会在"原文里恰好没有记号词"时漏掉。</para>
    ///
    /// <para><b>为什么必须是纯函数</b>：负控要注入"渲染层照抄原文"这种形状才能证明它**不可被成对判据替代**
    /// （en 侧本来就该是英文 ⇒ 脚本检查对它无话可说）。断言记号表本身不等于验判据。</para>
    /// </summary>
    internal static List<string> LeakViolations(IReadOnlyList<Rendered> rows)
    {
        var leaked = new List<string>();

        foreach (var row in rows)
            foreach (var (lang, rendered) in new[] { ("zh-CN", row.Zh), ("en-US", row.En) })
            {
                if (rendered.Contains(row.Raw, StringComparison.Ordinal))
                {
                    leaked.Add($"{lang} {row.Code}：整条原文都被透传了（「{rendered}」）");
                    continue;
                }

                foreach (var marker in RawDotNetMarkers)
                    if (rendered.Contains(marker, StringComparison.Ordinal))
                        leaked.Add($"{lang} {row.Code}：「{rendered}」里出现了 .NET 原文记号「{marker}」");
            }

        return leaked;
    }

    /// <summary>成对性违规（<c>null</c> = 通过）。抽成纯函数是为了让负控能直接验它。</summary>
    internal static string? PairingViolation(string code, string zh, string en)
    {
        if (string.IsNullOrWhiteSpace(zh) || string.IsNullOrWhiteSpace(en))
            return $"{code}：有一侧渲染为空（zh「{zh}」/ en「{en}」）";
        if (zh == en)
            return $"{code}：两侧渲染**完全相同**（「{zh}」）⇒ 语言切换没生效，或字典值没分开";
        if (!HasCjk(zh))
            return $"{code}：zh 渲染的不是中文（「{zh}」）";
        if (HasCjk(en))
            return $"{code}：en 渲染里还有中文（「{en}」）⇒ 英文用户看到中文";
        return null;
    }

    /// <summary>有没有汉字（粗判足够：字典里 zh 值必然含汉字、en 值不该含）。</summary>
    internal static bool HasCjk(string text)
        => text.Any(c => c >= 0x4E00 && c <= 0x9FFF);

    /// <summary>按码造一个该类型的异常（用于接线判据的毒化消息）。</summary>
    internal static Exception ExceptionFor(string code, string message)
    {
        var type = TypeMap().Where(e => e.Code == code).Select(e => e.ExceptionType).FirstOrDefault()
                   ?? throw new InvalidOperationException($"码表里没有码 {code}");
        return (Exception)Activator.CreateInstance(type, message)!;
    }

    /// <summary>有序性违规：某条目是它前面某条的派生类型。</summary>
    internal static List<string> OrderingViolations((Type ExceptionType, string Code)[] map)
    {
        var bad = new List<string>();
        for (var i = 0; i < map.Length; i++)
            for (var j = 0; j < i; j++)
                if (map[i].ExceptionType.IsSubclassOf(map[j].ExceptionType))
                    bad.Add($"  {map[j].ExceptionType.Name}（第 {j + 1} 条）是 "
                            + $"{map[i].ExceptionType.Name}（第 {i + 1} 条）的基类 ⇒ 后者永远匹配不到");
        return bad;
    }

    /// <summary>双向闭包违规：声明未挂 / 挂未声明。</summary>
    internal static List<string> ClosureViolations(
        (Type ExceptionType, string Code)[] map, IReadOnlyList<string> declared)
    {
        var mapped = map.Select(e => e.Code).ToHashSet(StringComparer.Ordinal);
        var bad = new List<string>();

        foreach (var code in declared.Where(c => !mapped.Contains(c)).OrderBy(c => c, StringComparer.Ordinal))
            bad.Add($"  {code}：声明了却没有任何类型挂它 ⇒ **死码**（它和它的字典键永远用不到）");

        foreach (var code in mapped.Where(c => !declared.Contains(c, StringComparer.Ordinal)).OrderBy(c => c, StringComparer.Ordinal))
            bad.Add($"  {code}：码表挂了它，但 SystemFailureCodes 里**没有声明**这个常量");

        return bad;
    }

    // ══════════════════════════ 负控（正控同场） ══════════════════════════

    /// <summary>
    /// 7 组注入，每组都**先跑正控**（真码表 + 真字典下所有纯函数判据都空）——
    /// 否则"报出来了"可能只是判据恒真。注入选的都是"能单独打红某一条、别的看不见"的形状，
    /// 用来证明这几条判据**互不替代**。
    ///
    /// <para>⚠ 判据只能由**它们自己**判红：每组里除了"被注入的那条"要非空，
    /// 其余几条都必须**空** —— 否则"这一条抓到了"其实是被别条顺手带出来的。</para>
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        var map = TypeMap();
        var declared = DeclaredCodes();
        var zh = PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
        var en = PluginTextLocalizationGuardTests.LoadDictionary("en-US");
        var victim = ExternalText.SystemIssueKey(SystemFailureCodes.UNAUTHORIZED_ACCESS);

        // ── 正控 ──
        Assert.Empty(OrderingViolations(map));
        Assert.Empty(ClosureViolations(map, declared));
        Assert.Null(PairingViolation(victim, zh[victim], en[victim]));
        Assert.Contains(SystemFailureCodes.UNAUTHORIZED_ACCESS, declared);

        // ── NC1 删一张字典的键 ⇒ 只有"键可解析"那一腿看得见 ──
        Assert.True(zh.ContainsKey(victim), $"探针结果里应当有 {victim}");
        var zhBackup = zh[victim];
        zh.Remove(victim);
        Assert.False(zh.ContainsKey(victim));
        Assert.Empty(OrderingViolations(map));          // 有序性看不见它
        Assert.Empty(ClosureViolations(map, declared)); // 闭包也看不见它
        zh[victim] = zhBackup;

        // ── NC2 模板多一个占位符 ⇒ 键还在，只有占位符判据抓得到 ──
        zh[victim] = zhBackup + " {9}";
        Assert.False(HmiEditor.Core.Services.LocalizationPlaceholders.MatchesArgumentCount(zh[victim], 0));
        Assert.True(zh.ContainsKey(victim));            // 键覆盖**看不见**它
        zh[victim] = zhBackup;

        // ── NC3 两本字典取同一个值 ⇒ 只有"成对"抓得到 ──
        var enBackup = en[victim];
        en[victim] = zh[victim];
        Assert.NotNull(PairingViolation(victim, zh[victim], en[victim]));
        Assert.Empty(OrderingViolations(map));          // 顺序看不见它
        Assert.Empty(ClosureViolations(map, declared)); // 闭包也看不见它
        en[victim] = enBackup;

        // ── NC3b en 里混进中文 ⇒ 同一条判据的另一半 ──
        en[victim] = "没有权限访问该文件";
        Assert.NotNull(PairingViolation(victim, zh[victim], en[victim]));
        en[victim] = enBackup;

        // ── NC4 渲染层把 BCL 原文当文案（= #61e 落地前的**原形状**）──
        // ⚠ 这一组刻意**不动字典、不动码表**：它模拟的是"渲染器压根没去查字典"。
        // 于是成对 / 有序性 / 闭包三条**全都看不见**（它们的输入面是字典和码表），
        // 抓得到它的只有泄漏判据 —— 而泄漏判据恰恰不能由它们替代。
        const string raw = "Access to the path '/tmp/x' is denied.";

        // 正控（本组）：干净的渲染 ⇒ 两支都不报（否则下面的"报出来了"可能只是判据恒真）
        Assert.Empty(LeakViolations(new[]
        {
            new Rendered("NC4", SystemFailureCodes.UNAUTHORIZED_ACCESS, raw, zh[victim], en[victim]),
        }));

        // ① 整条照抄 ⇒ 走"整条原文都被透传"那一支
        var copied = new[]
        {
            new Rendered("NC4", SystemFailureCodes.UNAUTHORIZED_ACCESS, raw, zh[victim], raw),
        };
        Assert.Contains(LeakViolations(copied),
            v => v.Contains("整条原文都被透传", StringComparison.Ordinal));

        // ② 包了一层壳但也带记号 ⇒ 走"记号"那一支
        //   （刻意让**场景自己的原文** ≠ 泄漏语句，否则会被①那一支截走、②永远跑不到）
        var embedded = new[]
        {
            new Rendered("NC4", SystemFailureCodes.IO_ERROR,
                "Could not find file '/tmp/y'.", zh[victim], "Failed to save: " + raw),
        };
        Assert.Contains(LeakViolations(embedded),
            v => v.Contains("Access to the path", StringComparison.Ordinal));

        // 成对判据对①**无话可说** —— en 侧本来就该是英文，脚本检查看不见它（这就是它不可替代的理由）
        Assert.Null(PairingViolation(victim, zh[victim], raw));
        // 只有"两侧都被原文占满"这种字面形状，成对才靠"两侧相同"那一支抓到
        // （⇒ 成对抓得住是因为**巧合**，不是因为它在盯着原文）
        Assert.NotNull(PairingViolation(victim, raw, raw));

        Assert.Empty(OrderingViolations(map));          // 泄漏**看不见**有序性
        Assert.Empty(ClosureViolations(map, declared)); // 也看不见闭包

        // ── NC5 把基类挪到前面 ⇒ 只有有序性判据抓得到，且它**真的改变匹配结果** ──
        var shuffled = new[] { (typeof(IOException), SystemFailureCodes.IO_ERROR) }
            .Concat(map.Where(e => e.ExceptionType != typeof(IOException)))
            .ToArray();
        Assert.NotEmpty(OrderingViolations(shuffled));
        Assert.Empty(ClosureViolations(shuffled, declared));   // 闭包看不见顺序

        var fileNotFound = new FileNotFoundException("boom");
        // 乱序表：被基类吃掉 ⇒ 拿到 IO_ERROR（**这正是"顺序即语义"的证据**）
        Assert.Equal(SystemFailureCodes.IO_ERROR, SystemFailureCodes.CodeFor(shuffled, fileNotFound));
        // 真表：拿到自己的码 —— 两条一起才说明顺序**真的**在起作用
        Assert.Equal(SystemFailureCodes.FILE_NOT_FOUND, SystemFailureCodes.CodeFor(map, fileNotFound));

        // ── NC6 声明一个没人挂的码 ⇒ 只有双向闭包抓得到 ──
        var withGhost = declared.Concat(new[] { "GHOST_CODE" }).OrderBy(c => c, StringComparer.Ordinal).ToList();
        Assert.Contains(ClosureViolations(map, withGhost), d => d.Contains("GHOST_CODE", StringComparison.Ordinal));
        Assert.Empty(OrderingViolations(map));          // 顺序看不见它
        Assert.Null(PairingViolation(victim, zh[victim], en[victim]));  // 成对也看不见它
    }
}

/// <summary>
/// 换全局 <c>AppLog.Current</c> 的测试必须**独占**，否则别的测试产生的日志会串进断言。
///
/// <para><b>为什么要在这里再声明一个</b>：集合定义是**按程序集**生效的 ——
/// <c>tests/HmiEditor.Core.Tests/UiOperationGuardTests.cs</c> 里那个同名集合管不到本程序集
/// （App 的 internal 只对 <c>HmiEditor.App.UiTests</c> 可见，所以那条判据搬不过来）。</para>
/// </summary>
[CollectionDefinition("AppLog-global-app", DisableParallelization = true)]
public sealed class AppLogGlobalCollectionInAppUiTests;

/// <summary>
/// <b>「原文不许丢」</b>（登记表 §4 <b>#61e</b> 的判据②）：原因那一半码化之后，
/// BCL 的原文**仍然必须落盘**，否则排障信息永久损失。
///
/// <para><b>为什么这条必须单独存在</b>：它是 #61e 的**唯一代价** —— 码化把原因换成短句，
/// 于是<b>界面上的原文没了</b>。判据①（成对 + 不泄漏）看着这条时是**看不见**的：
/// 把原文彻底丢掉，判据①照样全绿。⇒ 必须有另一条断言"日志里有原文"。</para>
///
/// <para><b>两侧都断言</b>（与阶段 82 的成对教训同形）：① 界面上是码化短句、**不含** BCL 原文；
/// ② 日志里**有**那条例外（含原文）。只断言①会在"根本没渲染出东西"时也成立；
/// 只断言②会在"原文照旧透传到界面"时也成立。</para>
/// </summary>
[Collection("AppLog-global-app")]
public class SystemFailureRawMessageIsStillLoggedTests
{
    /// <summary>记录日志的测试替身（异常也收 —— 原文就靠它证明没丢）。</summary>
    private sealed class RecordingLogger : HmiEditor.Core.Diagnostics.IAppLogger
    {
        internal List<string> Errors { get; } = new();
        internal List<Exception?> Exceptions { get; } = new();

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message, Exception? exception = null) { }

        public void Error(string message, Exception? exception = null)
        {
            Errors.Add(message);
            Exceptions.Add(exception);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task
        CodedSystemFailure_UiGetsTheCodedText_WhileTheRawMessageStillLandsInTheLog()
    {
        var previousLogger = HmiEditor.Core.Diagnostics.AppLog.Current;
        var previousSink = UiOperationGuard.UiSink;
        var logger = new RecordingLogger();
        HmiEditor.Core.Diagnostics.AppLog.Current = logger;

        try
        {
            var shown = new List<string>();
            const string raw = "Access to the path '/tmp/whatever.hmi.tmp' is denied.";
            var ex = new UnauthorizedAccessException(raw);

            // 前提：这个类型在码表里（否则本测试测的是回落分支、不是码化分支）
            Assert.Equal(SystemFailureCodes.UNAUTHORIZED_ACCESS, SystemFailureCodes.CodeFor(ex));

            await UiOperationGuard.RunAsync("保存工程", () => throw ex, shown.Add);

            // ── ① 界面：码化短句，**不含**原文 ──
            Assert.Single(shown);
            Assert.DoesNotContain("Access to the path", shown[0], StringComparison.Ordinal);
            Assert.DoesNotContain(raw, shown[0], StringComparison.Ordinal);
            var coded = ExternalText.FailureReason(ex);
            Assert.NotEmpty(coded);
            Assert.NotEqual(raw, coded);
            Assert.Contains(coded, shown[0], StringComparison.Ordinal);

            // ── ② 日志：原文仍然在（异常对象本身，未被替换）──
            Assert.Single(logger.Errors);
            Assert.Single(logger.Exceptions);
            Assert.Contains(logger.Exceptions,
                e => e is not null && e.Message.Contains(raw, StringComparison.Ordinal));
        }
        finally
        {
            HmiEditor.Core.Diagnostics.AppLog.Current = previousLogger;
            UiOperationGuard.UiSink = previousSink;
        }
    }
}
