using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **工程包解包失败文案**的本地化守卫（登记表 §4 <b>#59</b>：打开失败的原因那一半
/// 是 .NET 的原始英文异常消息）。
///
/// <para><b>它补的是哪半边</b>：<c>HmiPackageService.Unpack</c> 在 Core 层，而
/// <c>HardcodedLocalizationGuardTests</c> 的扫描面是 <c>src/HmiEditor.App</c> ——
/// Core 抛出来的东西完全在它视野之外。而解包失败的原因**会直接进状态栏**
/// （<c>UiOperationGuard.FailureDetail</c>）。码化之前，①非 ZIP 那一档用户看到的是
/// <c>End of Central Directory record could not be found.</c>，⑤ <c>manifest.json</c> 坏 JSON
/// 那一档看到的是 <c>System.Text.Json</c> 的内部措辞连 <c>LineNumber</c> / <c>BytePositionInLine</c>
/// 一起（阶段 80 探针实测）。没有这条守卫，新增一条包失败不会有任何东西提醒你补字典键。</para>
///
/// <para><b>四条互补判据</b>，输入全部来自**真实触发**（不写"预期 Code"清单 ——
/// 那等于把实现抄一遍，抄出来一旦与实现分家，守卫检查的就是清单而不是代码）：</para>
/// <list type="number">
///   <item><b>键覆盖（零容忍）</b>：每个真抛出来的 <c>Code</c>，其键
///   <c>package.issue.&lt;码小写&gt;</c> 都必须在**两本字典**里能解析；
///   ⚠ 抛出来的东西**不是** <c>HmiPackageException</c> 时（例如有人把某条改回抛
///   <c>InvalidDataException</c>），这里会拿 <c>!</c> + 类型名当"码"报出来 —— 见 <see cref="Hit"/>；</item>
///   <item><b>占位符个数 == 实参个数</b>：<c>Format</c> 在两者不符时**不抛异常**、只原样返回
///   ⇒ 用户会在状态栏看到 <c>{1}</c> 这种字面量。编译器看不见、运行时不抛，只有钉住才发现；</item>
///   <item><b>zh 模板渲染 == Core 回落原文</b>：这是"两份文案同步"的**自动**判据
///   （界面显示字典那份、字典缺键时显示回落那份，不同就等于同一句话有两个版本）；</item>
///   <item><b>渲染结果不得含 .NET 原文片段</b>：这是 #59 的**反回归**本身 ——
///   判据不钉措辞，但钉"这几种 .NET 内部措辞/字段名不许出现在用户可见文案里"。</item>
/// </list>
///
/// <para><b>它为什么刻意不钉文案措辞</b>：与阶段 80 那条端到端判据同一条理由 ——
/// 把当前措辞固化成契约，下次改文案要动判据；而 #59 要的恰恰是"这一半由字典决定"，
/// 所以这里钉的是**码 → 键 → 模板 → 渲染**这条链，措辞改了判据跟着走。</para>
///
/// <para><b><see cref="DeclaredCodes"/> 是 M9 的取数出口</b>：
/// <c>LocalizationKeyReachabilityGuardTests</c> 靠它算"包诊断码"这条可达机制的面 ——
/// 取数方式**只有这一份**（抄第二份必然漂移，而漂移的表现是"一个守卫绿、另一个红"）。</para>
/// </summary>
public class PackageIssueLocalizationGuardTests
{
    private readonly ITestOutputHelper _out;

    public PackageIssueLocalizationGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>Core 回落原文的语言侧对照：两本字典。</summary>
    private static Dictionary<string, string> Zod => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");

    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    /// <summary>
    /// 一次真实触发的读数：**码 + 实参 + 回落原文**。
    ///
    /// <para><paramref name="Code"/> 以 <c>!</c> 开头表示"抛出来的**不是**码化异常"
    /// （值是 <c>!</c> + 异常类型名）。它不是码，所以任何按码查字典的判据都会红，
    /// 而失败消息里能直接看见真实类型名 —— 比"静默漏掉一条"强得多。</para>
    /// </summary>
    internal sealed record Hit(string Code, IReadOnlyList<object>? Args, string Message)
    {
        /// <summary>是否来自码化异常（<c>!</c> 前缀 = 有人把它改回抛别的类型了）。</summary>
        internal bool IsCoded => !Code.StartsWith('!');
    }

    // ══════════════════════════ 触发面 ══════════════════════════

    /// <summary>
    /// 跑一遍**真实解包路径**，收集全部失败读数。
    ///
    /// <para>落点用一个真实但不含资产的目录（<c>TestTempDir</c>）。前 7 条在
    /// <c>Directory.CreateDirectory(assetDir)</c> **之前**失败，一个文件都收不到
    /// （这正是阶段 80 那条落点契约判据钉的东西）；后 2 条（阶段 105 补）是**中途失败**，
    /// 原先会在落点里留下 <c>&lt;目标&gt;/assets/</c> ⇒ 现在回滚掉。**每条一个独立落点**：
    /// 共用一个的话，"某条留下了东西"会污染后来者，读数也就分不清是谁留的。</para>
    /// </summary>
    internal static List<Hit> Probe()
    {
        var root = TestTempDir.NewDir("pkgissue-probe");
        var hits = new List<Hit>();
        var index = 0;

        foreach (var (_, bytes) in BadPackages())
        {
            var target = Path.Combine(root, $"case-{index++}");
            using var input = new MemoryStream(bytes);
            try
            {
                HmiPackageService.Unpack(input, target);
                hits.Add(new Hit("!未抛异常", null, "（坏包被当成了好包）"));
            }
            catch (HmiPackageException ex)
            {
                hits.Add(new Hit(ex.Code, ex.Args, ex.Message));
            }
            catch (Exception ex)
            {
                // 有人把某条改回抛别的东西（比如 InvalidDataException）⇒ 记成非码化，让判据红。
                hits.Add(new Hit("!" + ex.GetType().Name, null, ex.Message));
            }
        }

        return hits;
    }

    /// <summary>
    /// 反射枚举 <see cref="HmiPackageIssueCodes"/> 里声明的全部码。
    ///
    /// <para><b>是同源出口</b>：本文件的三条判据与
    /// <c>LocalizationKeyReachabilityGuardTests</c> 的 M9 都调它，不各写一份反射。</para>
    /// </summary>
    internal static List<string> DeclaredCodes()
        => typeof(HmiPackageIssueCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(v => v is not null)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// 9 条坏包 —— 与 <c>HmiPackageServiceTests.Unpack_BrokenPackage_LeavesNoTargetDirectoryBehind</c>
    /// 的前 5 条同形状，另加 <c>project.json</c> / <c>assets.json</c> 各一条坏 JSON
    /// （那两条不走 <c>ProjectSerializer</c> 的吞异常路径，是**另外**两个码），
    /// 以及**中途失败**两条（阶段 105：<c>reslib.json</c> 坏 JSON / 资产指向目录条目）。
    ///
    /// <para><b><c>internal</c> 而不是 <c>private</c></b>：<c>OpenFailureFilePathUiTests</c>（阶段 82）
    /// 要把同一批字节**落成真实文件**再走端到端打开路径。夹具只此一份 ——
    /// 另抄一份的后果是"两条判据测的其实是两批不同的坏包"。</para>
    /// </summary>
    internal static (string Label, byte[] Bytes)[] BadPackages() => new[]
    {
        ("非 ZIP（伪随机字节）", NonZipBytes(256)),
        ("缺 manifest.json", ZipBytes(("project.json", ProjectJson("无清单")))),
        ("manifest 版本不符（9999）", ZipBytes(
            ("manifest.json", ManifestJson(9999)), ("project.json", ProjectJson("版本不符")))),
        ("缺 project.json", ZipBytes(("manifest.json", ManifestJson(1)))),
        ("manifest.json 是坏 JSON", ZipBytes(
            ("manifest.json", "{ 这不是 JSON"), ("project.json", ProjectJson("坏清单")))),
        ("project.json 是坏 JSON", ZipBytes(
            ("manifest.json", ManifestJson(1)), ("project.json", "{ 这不是 JSON"))),
        ("assets.json 是坏 JSON", ZipBytes(
            ("manifest.json", ManifestJson(1)), ("project.json", ProjectJson("坏资产清单")),
            ("assets.json", "{ 这不是 JSON"))),
        // ── 中途失败两条（CreateDirectory 之后，阶段 105 补）──
        ("reslib.json 是坏 JSON", ZipBytes(
            ("manifest.json", ManifestJson(1)), ("project.json", ProjectJson("坏图库")),
            ("reslib.json", "{ 这不是 JSON"))),
        ("资产条目是目录条目（assets/）", ZipBytes(
            ("manifest.json", ManifestJson(1)), ("project.json", ProjectJson("目录条目")),
            ("assets.json", AssetsJson("a1", "assets/")), ("assets/", ""))),
    };

    // ── 坏包夹具（与 Core 侧 HmiPackageServiceTests 同形状）──

    /// <summary>定长伪随机字节；前 4 字节刻意不是 <c>PK\x03\x04</c>。</summary>
    private static byte[] NonZipBytes(int size)
    {
        var data = new byte[size];
        for (var i = 0; i < size; i++) data[i] = (byte)((i * 131 + 17) % 251);
        return data;
    }

    /// <summary>
    /// 内存里手搓一个 ZIP。**用 <c>params</c> 而不是收数组**：元组元素的可空性**不协变** ——
    /// 收数组时调用点推 <c>(string, string)[]</c>、形参要 <c>(string, string?)[]</c> ⇒ <c>CS8620</c>
    /// （阶段 80 实测：独立跑全绿，是被门禁的「基线 0 警告」拦下来的）。
    /// </summary>
    private static byte[] ZipBytes(params (string Name, string? Text)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in entries)
            {
                if (text is null) continue;
                var entry = zip.CreateEntry(name);
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(text);
            }
        }
        return ms.ToArray();
    }

    private static string ManifestJson(int formatVersion)
        => JsonSerializer.Serialize(
            new HmiPackageManifest { FormatVersion = formatVersion, ProjectName = "坏包" },
            JsonOptionsDefaults.Default);

    private static string ProjectJson(string name)
        => ProjectSerializer.Serialize(new HmiProject { Name = name, ProjectVersion = "1.0.0" });

    /// <summary><c>assets.json</c> 夹具（与 Core 侧同形状：走真实序列化，不手写 JSON）。</summary>
    private static string AssetsJson(string id, string filePath)
    {
        var lib = new AssetLibrary();
        lib.AddAsset(new AssetItem { Id = id, Name = id, Type = AssetType.Image, FilePath = filePath });
        return JsonSerializer.Serialize(lib, JsonOptionsDefaults.Default);
    }

    // ══════════════════════════ 四条判据 ══════════════════════════

    [Fact]
    public void EveryTriggeredCode_ResolvesInBothLanguages()
    {
        var hits = Probe();
        var missing = MissingKeys(hits, Zod, En);

        WriteReadings(hits);
        Assert.True(missing.Count == 0,
            "以下工程包失败码的键在字典里查不到 —— 运行时会**回落 Core 的中文原文**"
            + "（英文界面显示中文）。补两本字典的键即可（码 → package.issue.<码小写>）：\n"
            + string.Join("\n", missing));
    }

    [Fact]
    public void PlaceholderCount_MatchesArgsCount()
    {
        var hits = Probe();
        var drift = PlaceholderDrift(hits, Zod, En);

        Assert.True(drift.Count == 0,
            "字典模板的占位符个数与 Core 传的 Args 个数不符 —— Format 不抛异常、只原样返回，"
            + "用户会在状态栏看到 {1} 这种字面量：\n" + string.Join("\n", drift));
    }

    [Fact]
    public void ChineseTemplate_RendersToTheCoreFallback()
    {
        var hits = Probe();
        var drift = FallbackDrift(hits, Zod);

        Assert.True(drift.Count == 0,
            "zh 字典模板渲染后与 Core 的回落原文不一致 —— 这两份必须逐字相同"
            + "（同一句话不能有两个版本）：\n" + string.Join("\n", drift));
    }

    /// <summary>
    /// <b>#59 的反回归</b>：用户可见的那一半文案里，**不许**出现 .NET 的原文片段。
    ///
    /// <para>判据不钉措辞（措辞可以改），但钉这几样"一旦出现就说明有人又把异常原文透传了"的记号：
    /// <c>Central Directory</c>（BCL 的 ZIP 报错）、<c>is an invalid start of</c> /
    /// <c>BytePositionInLine</c> / <c>LineNumber</c>（<c>System.Text.Json</c> 的内部措辞与坐标）。
    /// 这几样都是阶段 80 探针**实测**到的原文，不是猜的。</para>
    /// </summary>
    [Fact]
    public void RenderedReason_IsNotTheRawDotNetMessage()
    {
        var hits = Probe().Where(h => h.IsCoded).ToList();
        var leaked = new List<string>();

        foreach (var hit in hits)
        {
            var rendered = Rendered(Zod, hit);
            foreach (var marker in RawDotNetMarkers)
                if (rendered.Contains(marker, StringComparison.Ordinal))
                    leaked.Add($"{hit.Code}：「{rendered}」里出现了 .NET 原文记号「{marker}」");
        }

        Assert.True(hits.Count >= 9, $"只触发到 {hits.Count} 条码化失败 —— 触发面可能失效了");

        Assert.True(leaked.Count == 0,
            "失败原因那一半把 .NET 的原始异常消息透传到了用户可见文案上（登记 #59 的原形状）：\n"
            + string.Join("\n", leaked));
    }

    /// <summary>
    /// <b>接线判据</b>：<c>ExternalText.FailureReason</c> 必须**真的查了字典**。
    ///
    /// <para><b>为什么不能只看"文案对不对"</b>：zh 字典的值与 Core 回落原文**按设计逐字相同**，
    /// 所以"渲染出来是中文"这件事**无法区分**「查到了字典」与「回落了原文」。
    /// 这里用一个**毒化回落**（把回落改成 <c>&lt;&lt;回落未被使用&gt;&gt;</c>）来分辨：
    /// 码是真码、模板在字典里 ⇒ 渲染结果必须是**字典那份**、毒化串一个都不许出现。
    /// 这是"同一条注入下两种写法一个绿一个红"的构造，不是自证。</para>
    /// </summary>
    [Fact]
    public void FailureReason_PrefersTheDictionaryOverTheFallback()
    {
        var poisoned = new List<string>();

        foreach (var hit in Probe().Where(h => h.IsCoded))
        {
            var ex = new HmiPackageException(hit.Code, PoisonedFallback, hit.Args?.ToArray());
            var rendered = ExternalText.FailureReason(ex);

            if (rendered.Contains(PoisonedFallback, StringComparison.Ordinal))
                poisoned.Add($"{hit.Code}：渲染结果回落到了原文（「{rendered}」）—— 字典没被查到");
            else if (rendered != Render(Zod, PackageKey(hit.Code), hit.Args))
                poisoned.Add($"{hit.Code}：渲染结果既不是字典那份、也不是回落（「{rendered}」）");
        }

        Assert.True(poisoned.Count == 0,
            "FailureReason 没有把码渲染成字典文案：\n" + string.Join("\n", poisoned));
    }

    /// <summary>
    /// 自检：声明过的码必须**都能被真实触发**。
    ///
    /// <para>触发不出来的码是**死码** —— 它和它的字典键都永远没人会用，而"看起来有覆盖"
    /// 会让人以为测到了。反过来，抛出了没声明的码不会漏：<see cref="Probe"/> 直接读异常的
    /// <c>Code</c>，而 <see cref="MissingKeys"/> 会拿它去查字典 ⇒ 必然缺键。</para>
    /// </summary>
    [Fact]
    public void Probe_CoversEveryDeclaredCode()
    {
        var triggered = Probe().Select(h => h.Code).ToHashSet(StringComparer.Ordinal);
        var declared = DeclaredCodes();

        // 防空转：反射一旦失效会返回空集，"声明了没人产出"那条就恒真了。
        Assert.True(declared.Count >= 5,
            $"反射只枚举到 {declared.Count} 个码 —— 判据的取数方式可能失效了");

        // ⚠ **顺序是判据的一部分**：先报"抛出了非码化异常"，再报"声明了触发不到"。
        // 反了的话，有人把某条改回抛 InvalidDataException 时，先命中的是"ZIP_UNREADABLE 触发不到"
        // —— 方向对、指认不准（真正的原因在上游：那条路径不再码化了）。这是负控 NC2 实测到的：
        // 第一版顺序下 NC2 被判成 wrong-leg，因为报出来的谓词不是"有人把某条改回抛别的类型了"。
        var uncoded = Probe().Where(h => !h.IsCoded).ToList();
        Assert.True(uncoded.Count == 0,
            "以下失败**不是**码化异常（有人把某条改回抛别的类型了）—— 用户会看到 .NET 原文：\n"
            + string.Join("\n", uncoded.Select(h => $"  {h.Code}  |  {h.Message}")));

        var unexpected = declared.Where(c => !triggered.Contains(c)).ToList();

        Assert.True(unexpected.Count == 0,
            "以下码声明了却触发不出来 —— 要么坏包样本没覆盖到它，要么它是**死代码**"
            + "（它和它的字典键都永远不会被用到）：\n" + string.Join("\n", unexpected)
            + "\n\n实际触发到的（码 | 回落文案）：\n"
            + string.Join("\n", Probe().Select(h => $"  {h.Code}  |  {h.Message}")));
    }

    // ══════════════════════════ 判据实现（纯函数 —— 负控要复用） ══════════════════════════

    private const string PoisonedFallback = "<<回落未被使用>>";

    /// <summary>阶段 80 探针**实测**到的 .NET 原文记号（不是猜的）。</summary>
    private static readonly string[] RawDotNetMarkers =
    {
        "Central Directory",
        "is an invalid start of",
        "BytePositionInLine",
        "LineNumber",
        "System.Text.Json",
    };

    private static string PackageKey(string code) => ExternalText.PackageIssueKey(code);

    /// <summary>字典文本 + 实参 → 用户可见文案（与 <c>Format</c> 同口径）。</summary>
    private static string Render(Dictionary<string, string> dict, string key, IReadOnlyList<object>? args)
    {
        if (!dict.TryGetValue(key, out var template)) return "";
        return args is { Count: > 0 } a ? string.Format(template, a.ToArray()) : template;
    }

    /// <summary>一条读数的用户可见文案（字典那份；缺键时用回落原文，好让消息里看得见）。</summary>
    private static string Rendered(Dictionary<string, string> dict, Hit hit)
    {
        var byDict = Render(dict, PackageKey(hit.Code), hit.Args);
        return byDict.Length > 0 ? byDict : hit.Message;
    }

    private static List<string> MissingKeys(
        IReadOnlyList<Hit> hits, Dictionary<string, string> zh, Dictionary<string, string> en)
        => hits
            .Select(h => PackageKey(h.Code))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .Where(k => !zh.ContainsKey(k) || !en.ContainsKey(k))
            .Select(k => k + (zh.ContainsKey(k) ? "" : "  缺 zh-CN") + (en.ContainsKey(k) ? "" : "  缺 en-US"))
            .ToList();

    private static List<string> PlaceholderDrift(
        IReadOnlyList<Hit> hits, Dictionary<string, string> zh, Dictionary<string, string> en)
    {
        var drift = new List<string>();
        foreach (var hit in hits)
        {
            var key = PackageKey(hit.Code);
            var expected = hit.Args?.Count ?? 0;

            foreach (var (lang, dict) in new[] { ("zh-CN", zh), ("en-US", en) })
            {
                if (!dict.TryGetValue(key, out var template)) { drift.Add($"{lang} {key}: 缺键"); continue; }
                if (!LocalizationPlaceholders.MatchesArgumentCount(template, expected))
                {
                    drift.Add($"{lang} {key}: 模板 {LocalizationPlaceholders.DescribeIndexes(template)} "
                              + $"与 Core 传的 {expected} 个实参对不上");
                }
            }
        }
        return drift.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static List<string> FallbackDrift(IReadOnlyList<Hit> hits, Dictionary<string, string> zh)
    {
        var drift = new List<string>();
        foreach (var hit in hits)
        {
            var key = PackageKey(hit.Code);
            if (!zh.TryGetValue(key, out var template)) { drift.Add($"{key}: 缺键"); continue; }

            var rendered = Render(zh, key, hit.Args);
            if (rendered != hit.Message)
                drift.Add($"{key}\n    字典渲染: {rendered}\n    Core 回落: {hit.Message}");
        }
        return drift.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private void WriteReadings(IReadOnlyList<Hit> hits)
    {
        var readings = hits
            .Select(h => $"    {h.Code,-30} 实参 {h.Args?.Count ?? 0}  |  {h.Message}")
            .ToList();
        readings.Insert(0, $"    声明 {DeclaredCodes().Count} 个码 / 真实触发 {hits.Count} 条");
        JudgeReadings.Write(_out, "工程包失败诊断码（码 | 实参个数 | 回落文案）", readings);
    }

    // ══════════════════════════ 负控（正控同场） ══════════════════════════

    /// <summary>
    /// 4 组注入，每组都**先跑正控**（真字典下三条纯函数判据都空）—— 否则"报出来了"
    /// 可能只是判据恒真。
    ///
    /// <para>注入选的都是"能把某一条判据单独打红、另外几条看不见"的形状，
    /// 用来证明四条判据**互不替代**：</para>
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        var hits = Probe();
        var zh = Zod;
        var en = En;

        // 正控：真字典下三条都是空集
        Assert.Empty(MissingKeys(hits, zh, en));
        Assert.Empty(PlaceholderDrift(hits, zh, en));
        Assert.Empty(FallbackDrift(hits, zh));

        // NC1 删键 ⇒ 只有键覆盖看得见（模板还在、占位符也对）
        var victim = PackageKey(hits.First(h => h.IsCoded).Code);
        Assert.True(zh.ContainsKey(victim), $"探针结果里应当有 {victim}");
        var backup = zh[victim];
        zh.Remove(victim);
        Assert.Contains(MissingKeys(hits, zh, en), d => d.Contains(victim, StringComparison.Ordinal));
        Assert.Empty(PlaceholderDrift(hits.Where(h => PackageKey(h.Code) != victim).ToList(), zh, en));

        // NC2 模板多一个占位符 ⇒ 键还在、只有占位符判据抓得到
        zh[victim] = backup + " {9}";
        Assert.Contains(PlaceholderDrift(hits, zh, en), d => d.Contains(victim, StringComparison.Ordinal));

        // NC3 文案改歪 ⇒ 只有"渲染 == 回落原文"抓得到
        zh[victim] = backup + "（改歪）";
        Assert.Contains(FallbackDrift(hits, zh), d => d.Contains(victim, StringComparison.Ordinal));

        zh[victim] = backup;

        // NC4 "又透传了 .NET 原文" ⇒ 反回归判据单独抓得到（这是 #59 的原形状）。
        //     构造：把回落原文换成阶段 80 实测的那句 BCL 原文。
        var raw = "End of Central Directory record could not be found.";
        Assert.Contains(RawDotNetMarkers, m => raw.Contains(m, StringComparison.Ordinal));

        var asFallback = new Hit(hits.First(h => h.IsCoded).Code, null, raw);
        var leakDict = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PackageKey(asFallback.Code)] = raw,
        };
        Assert.Contains(RawDotNetMarkers, m => Rendered(leakDict, asFallback).Contains(m, StringComparison.Ordinal));
        Assert.Empty(MissingKeys(new[] { asFallback }, leakDict, leakDict));   // 键覆盖**看不见**它

        // NC5 未码化异常（有人改回 InvalidDataException）⇒ 自检那条腿抓到，且名字里带真实类型
        var uncoded = new Hit("!" + nameof(InvalidDataException), null, raw);
        Assert.False(uncoded.IsCoded);

        // 它推不出真键 ⇒ 键覆盖**必然报出来**，且消息里带着真实的异常类型名。
        // ⚠ 第一版这里写的是 Assert.Empty —— 那是我**期望串写错了**：`!` 前缀的伪码本来
        // 就该被判成"查不到键"（这正是"有人漏登记时会被抓"的形状）。
        var uncodedMissing = MissingKeys(new[] { uncoded }, zh, en);
        Assert.Single(uncodedMissing);
        // 类型名在**码**里保持原样、在**报告的键**里被 PackageIssueKey 小写化了 —— 两条都断言，
        // 顺便把"键推导会小写"这件事钉进负控（第一版只断言了驼峰那种写法，于是红在这里）。
        Assert.Contains("InvalidDataException", uncoded.Code, StringComparison.Ordinal);
        Assert.Contains("invaliddataexception", uncodedMissing[0], StringComparison.Ordinal);
    }
}
