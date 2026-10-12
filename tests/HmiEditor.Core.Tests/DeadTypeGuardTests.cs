using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 死类型守卫：<c>src/</c> 里**声明之后再没人提过**的类型必须登记在册。
///
/// <para><b>判据（不是"外部引用为 0"）：</b>类型名在全仓（src + tests 的 C#，加
/// .axaml）出现次数 <b>== 1</b> —— 只有那一行声明，连它自己的文件里都没再用过。
/// 这条比"外部引用为 0"可靠得多：后者会把**文件内的私有/嵌套辅助类型**全部判死
/// （<c>ClaudeResponse</c>、<c>Parser</c>、<c>TokenType</c> 就在自己声明的那一行当返回类型用着），
/// 实测那一版给出 <b>104</b> 个候选，几乎全是误报。</para>
///
/// <para><b>三类已识别的误报源（各自都有元判据守着）：</b></para>
/// <list type="number">
///   <item><b>扩展方法容器</b> —— 用的时候写 <c>x.IfNullOrEmpty(...)</c>，类名一次都不出现
///   （<c>StringIfEmpty</c> 的类名只出现 1 次，而 <c>IfNullOrEmpty</c> 用了 30 处）。
///   处理：候选若声明了扩展方法、且该方法名在全仓还有别的出现 ⇒ 判活。</item>
///   <item><b>字面量里嵌别的语言</b> —— 生成器逐行写 C 代码
///   （<c>sb.AppendLine("struct timespec ts;")</c>），剥注释后那条
///   <c>struct timespec</c> 在"声明扫描"眼里就是一条 C# 声明。
///   处理：**声明**提取用 <see cref="SourceScanner.MaskLiterals"/>（连字面量一起抹掉）。</item>
///   <item><b>引用写在插值字符串的孔里</b> —— <c>$"…{LvglMacros.LabelLongMode(x)}…"</c>
///   （<c>LvglMacros</c> 只出现在这一处）。整串抹掉就会把这处唯一引用抹成零 ⇒ 活类型判死。
///   处理：<see cref="SourceScanner.MaskLiterals"/> **保留插值孔**、只抹字面量文本。
///   反向的坑：若改用"保留字面量"的文本计数，本守卫基线里的登记项（形如 <c>"TypeName",</c>）
///   会给每个名字各 +1 把它们全"用活" ⇒ 判据恒绿。两个方向都试过，只有"抹文本留孔"成立。</item>
/// </list>
///
/// <para><b>为什么用相等判定：</b>比基线多 = 新增死代码（或扫描口径坏了）；比基线少 =
/// 有人清理了死类型但没同步本表 —— 两种都必须红，否则这张表会退化成过期抄写
/// （同 <c>SingleFileSizeGuardTests</c> 的约定）。</para>
///
/// <para>出处：覆盖率读数排查（<c>docs/2026-09-20-coverage-tooling.md</c> §7、
/// <c>docs/project-status.md</c> 阶段 37）。</para>
/// </summary>
public class DeadTypeGuardTests
{
    private static readonly Regex DeclRe =
        new(@"\b(?:class|record|struct|interface|enum)\s+([A-Za-z_]\w*)", RegexOptions.Compiled);

    private static readonly Regex XClassRe =
        new(@"x:Class=""(?:[\w.]+\.)?([A-Za-z_]\w*)""", RegexOptions.Compiled);

    private static readonly Regex IdentRe = new(@"[A-Za-z_]\w*", RegexOptions.Compiled);

    /// <summary>
    /// 扩展方法的形态：<c>名字( this 参数…</c>（允许泛型参数表夹在中间）。
    /// 命中即说明"这个类的用法不需要写出类名"。
    /// </summary>
    private static readonly Regex ExtensionMethodRe =
        new(@"\b([A-Za-z_]\w*)\s*(?:<[^<>()]{0,60}>)?\s*\(\s*this\s", RegexOptions.Compiled);

    /// <summary>框架按名字调用（入口点、Avalonia 应用类），没人"引用"它们。</summary>
    private static readonly HashSet<string> FrameworkInvoked =
        new(StringComparer.Ordinal) { "Program", "App" };

    /// <summary>
    /// 基线：当前"只出现在声明行"的 <c>src/</c> 类型（相等判定）。
    ///
    /// <para><b>2026-09-20 清空</b> —— 原 4 项已全部处置完：
    /// <c>LuaScriptValidator</c> / <c>LuaSnippets</c> 随 <c>HmiEditor.Scripting</c> 下架
    /// （前者的 MoonSharp 真解析迁入 <c>HmiEditor.Core/Services/LuaSyntaxValidator.cs</c> 成为活代码，
    /// 后者的钩子名与运行时契约对不上故删除）；<c>HmiProperty</c> / <c>ResourceLibraryVersionInfo</c>
    /// 经**成员级 + 字符串 + 全仓（含 bin/obj）**核对确认无人引用后删除。</para>
    ///
    /// <para><b>空基线为什么不是恒绿：</b>本判据的价值不在"这张表非空"，而在
    /// <see cref="ScanCorpus"/> 真的扫到了东西。三条元判据在守这件事 ——
    /// 语料规模下限（<c>.cs &gt; 300</c> / <c>.axaml &gt; 20</c>）、声明两条路都要出结果
    /// （<c>MainWindow</c> / <c>AnimationEditorView</c>）、活类型与三个误报源类型**必须判活**。
    /// 扫描面一塌，先红的是那三条，不是本表。</para>
    ///
    /// <para>于是本表现在的语义更强：**<c>src/</c> 里不允许存在无人引用的类型** ——
    /// 新写一个没人用的类型 ⇒ 红并点名。</para>
    /// </summary>
    private static readonly string[] ExpectedDeadTypes = { };

    [Fact]
    public void DeadTypes_MatchBaseline()
    {
        var corpus = ScanCorpus();

        var actual = corpus.DeadTypes.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var expected = ExpectedDeadTypes.OrderBy(x => x, StringComparer.Ordinal).ToArray();

        var added = actual.Except(expected, StringComparer.Ordinal).ToArray();
        var removed = expected.Except(actual, StringComparer.Ordinal).ToArray();

        if (added.Length == 0 && removed.Length == 0) return;

        // 报错时把"每个预期名字的实况"一起打出来 —— 只有"清单不一致"这句话没法定位
        // （是没被声明？还是次数不是 1？还是被扩展方法判活了？）
        var detail = string.Join("\n", expected.Concat(added).Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .Select(n =>
            {
                var declared = corpus.DeclaredTypes.Contains(n);
                var cnt = corpus.IdentifierCount(n);
                var live = corpus.IsLive(n);
                return $"    {n,-28} 被声明={declared,-5} 出现次数={cnt,-4} "
                     + $"判定={(live ? "活" : "死")}";
            }));

        Assert.Fail(
            "死类型清单与基线不一致（相等判定）：\n"
            + $"  新增（代码里有、基线没有）= [{string.Join(", ", added)}]"
            + "  ← 三种可能：① 新写了没人用的类型；② 基线被误删了一条；"
            + "③ 扫描口径坏了（先看上面三条元判据）\n"
            + $"  消失（基线有、代码里没有）= [{string.Join(", ", removed)}]"
            + "  ← 死类型被清理了，必须同时把 ExpectedDeadTypes 改小\n"
            + $"  实况：\n{detail}\n"
            + $"  语料：.cs {corpus.CsFileCount} 个、.axaml {corpus.AxamlFileCount} 个、"
            + $"声明到的类型 {corpus.DeclaredTypes.Count} 个\n"
            + $"  实际死类型：{string.Join(", ", actual)}");
    }

    // ── 元判据：守"上面的判据本身有没有坏" ─────────────────────────────

    [Fact]
    public void Scan_ExtractsDeclarationsFromBothCsAndAxaml()
    {
        var corpus = ScanCorpus();

        // 声明提取本身不能是空的（否则 DeadTypes 恒为空 = 恒绿）
        Assert.True(corpus.CsFileCount > 300, $"src+tests 的 .cs 只扫到 {corpus.CsFileCount} 个，扫描面不对");
        Assert.True(corpus.AxamlFileCount > 20, $".axaml 只扫到 {corpus.AxamlFileCount} 个，x:Class 那条路没接上");

        // .cs 声明与 .axaml 的 x:Class 两条路都要出结果
        Assert.Contains("MainWindow", corpus.DeclaredTypes);
        Assert.Contains("AnimationEditorView", corpus.DeclaredTypes);
    }

    [Fact]
    public void Scan_DoesNotTreatLiteralTextAsDeclaration()
    {
        var corpus = ScanCorpus();

        // 生成器把 C 代码写在字符串里：剥注释救不了它，MaskLiterals 才救得了。
        Assert.DoesNotContain("timespec", corpus.DeclaredTypes);

        // 同一处反向确认：那个词确实在源文件里（只是被抹掉了），不是"文件里根本没有"
        var mainC = Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.Core", "Services",
            "SimulatorProjectExporter.MainC.cs");
        Assert.Contains("struct timespec", File.ReadAllText(mainC));
        Assert.DoesNotContain("timespec", SourceScanner.ReadMasked(mainC));
    }

    [Fact]
    public void Scan_KeepsExtensionMethodContainersAlive()
    {
        var corpus = ScanCorpus();

        // 误报源①：类名只出现一次，但它的扩展方法被用了 30 / 5 处
        Assert.True(corpus.IdentifierCount("IfNullOrEmpty") > 5,
            "IfNullOrEmpty 的出现次数异常 —— 扩展方法判活的依据没了");
        Assert.True(corpus.IdentifierCount("ParseOrDefault") > 1,
            "ParseOrDefault 的出现次数异常 —— 扩展方法判活的依据没了");

        Assert.DoesNotContain("StringIfEmpty", corpus.DeadTypes);
        Assert.DoesNotContain("EnumParseExtensions", corpus.DeadTypes);

        // 误报源③：引用写在**插值字符串的孔**里（$"…{LvglMacros.LabelLongMode(x)}…"）。
        // 所以"声明用抹掉字面量的文本、计数用保留字面量的文本"两份文本缺一不可 ——
        // 只抹一份（第一版就是这样）会把孔里的真引用一起抹掉，整个类型被判成死的。
        var lvglMacrosCount = corpus.IdentifierCount("LvglMacros");
        Assert.True(lvglMacrosCount > 1,
            $"LvglMacros 出现 {lvglMacrosCount} 次 —— 插值孔里的引用没被计入");
        Assert.DoesNotContain("LvglMacros", corpus.DeadTypes);

        // 活代码不能被误判成死代码
        Assert.DoesNotContain("MainWindow", corpus.DeadTypes);
        Assert.DoesNotContain("AnimationEditorView", corpus.DeadTypes);
        Assert.DoesNotContain("CIdentifier", corpus.DeadTypes);
        Assert.DoesNotContain("PaletteDefaults", corpus.DeadTypes);
    }

    // ── 扫描 ─────────────────────────────────────────────────────────

    private sealed class Corpus
    {
        public required IReadOnlySet<string> DeclaredTypes { get; init; }
        public required IReadOnlyList<string> DeadTypes { get; init; }
        public required IReadOnlyDictionary<string, int> Counts { get; init; }
        public required IReadOnlyDictionary<string, bool> Liveness { get; init; }
        public required int CsFileCount { get; init; }
        public required int AxamlFileCount { get; init; }

        public int IdentifierCount(string name) => Counts.TryGetValue(name, out var n) ? n : 0;

        /// <summary>该类型被判活？（不在声明集合里时返回 false，但那种情况下也不会是候选）</summary>
        public bool IsLive(string name) => Liveness.TryGetValue(name, out var live) && live;
    }

    private static Corpus ScanCorpus()
    {
        var csFiles = SourceScanner.ProductSources()                       // src/**/*.cs
            .Concat(SourceScanner.Enumerate(Path.Combine(SourceScanner.RepoRoot, "tests")))
            .ToList();

        var axamlFiles = new[] { Path.Combine(SourceScanner.RepoRoot, "src"),
                                 Path.Combine(SourceScanner.RepoRoot, "tests") }
            .SelectMany(d => Directory.EnumerateFiles(d, "*.axaml", SearchOption.AllDirectories))
            .Where(f => !SourceScanner.IsBuildOutput(f))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        // 文本：一份就够 —— .cs 用 MaskLiterals（抹掉注释与字面量内容，**保留插值孔**），
        // .axaml 原样（x:Class / <local:Type>）。
        // 三个坑都是让这一份文本"既能当声明源、又能当计数源"的理由：
        //   ① 不抹字面量 ⇒ 生成器里嵌的 C 代码（`"struct timespec ts;"`）被当成 C# 声明；
        //   ② 抹掉插值孔 ⇒ `$"…{LvglMacros.LabelLongMode(x)}…"` 这处唯一引用被抹掉，活类型判死；
        //   ③ 反过来若改用"保留字面量"的文本计数 ⇒ 本守卫基线里的登记项（`"TypeName",`）
        //      会给每个基线名字各 +1，把它们全部"用活"，判据变成恒绿。
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in csFiles) texts[f] = SourceScanner.ReadMasked(f);
        foreach (var f in axamlFiles) texts[f] = File.ReadAllText(f);

        // 声明（只认 src/）：.cs 的声明关键字 + .axaml 的 x:Class
        var declared = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (path, text) in texts)
        {
            var rel = SourceScanner.Relative(path);
            if (!rel.StartsWith("src/", StringComparison.Ordinal)) continue;

            var names = path.EndsWith(".axaml", StringComparison.Ordinal)
                ? XClassRe.Matches(text).Select(m => m.Groups[1].Value)
                : DeclRe.Matches(text).Select(m => m.Groups[1].Value);

            foreach (var n in names)
            {
                if (!declared.TryGetValue(n, out var where)) declared[n] = where = new List<string>();
                if (!where.Contains(rel)) where.Add(rel);
            }
        }

        // 计数一遍过全语料。**必须把扩展方法名一起纳入统计面** ——
        // 「类名没人写但扩展方法在用」正是误报源①，方法名不在 counts 里那条判活就永远失效。
        var interesting = new HashSet<string>(declared.Keys, StringComparer.Ordinal);
        foreach (var text in texts.Values)
            foreach (Match m in ExtensionMethodRe.Matches(text))
                interesting.Add(m.Groups[1].Value);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var n in interesting) counts[n] = 0;
        foreach (var text in texts.Values)
            foreach (Match m in IdentRe.Matches(text))
                if (counts.ContainsKey(m.Value)) counts[m.Value]++;

        // 判活三条：① 出现次数不是 1（被用过，或在 .axaml 里也声明了）；② 框架按名字调用；
        //            ③ 声明了**有人在用的扩展方法**（误报源①）
        var liveness = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (name, where) in declared)
        {
            liveness[name] = counts[name] != 1
                             || FrameworkInvoked.Contains(name)
                             || HasLiveExtensionMethod(texts, where, counts, name);
        }
        var dead = liveness.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();

        return new Corpus
        {
            DeclaredTypes = declared.Keys.ToHashSet(StringComparer.Ordinal),
            DeadTypes = dead,
            Counts = counts,
            Liveness = liveness,
            CsFileCount = csFiles.Count,
            AxamlFileCount = axamlFiles.Count,
        };
    }

    /// <summary>误报源①：类名没人写，但它的扩展方法名有人在用。</summary>
    private static bool HasLiveExtensionMethod(
        IReadOnlyDictionary<string, string> texts,
        IReadOnlyList<string> declaringFiles,
        IReadOnlyDictionary<string, int> counts,
        string typeName)
    {
        foreach (var rel in declaringFiles)
        {
            var abs = Path.Combine(SourceScanner.RepoRoot,
                rel.Replace('/', Path.DirectorySeparatorChar));
            if (!texts.TryGetValue(abs, out var text)) continue;

            foreach (Match m in ExtensionMethodRe.Matches(text))
            {
                var method = m.Groups[1].Value;
                if (method == typeName) continue;          // 构造器不是扩展方法
                if (counts.TryGetValue(method, out var n) && n > 1) return true;
            }
        }
        return false;
    }
}
