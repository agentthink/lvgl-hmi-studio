using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **视图可达性守卫**（阶段 135，登记 `#71` / `#73`）：<c>Views/</c> 下每个带 <c>x:Class</c> 的
/// <c>.axaml</c>，都必须在**产品代码里被构造过** —— 要么 <c>new X(…)</c>（含限定名
/// <c>new Views.X(…)</c>），要么在某个 <c>.axaml</c> 里被当标签用（<c>&lt;ns:X …/&gt;</c>，
/// 例：<c>PropertyPanelView.axaml</c> 里的 <c>&lt;editors:VariablePicker/&gt;</c>）。
/// 两者都没有 ⇒ 必须进 <see cref="Unreachable"/> 账本，逐条写明理由与**触发条件**。
///
/// <para><b>为什么需要它（这条守卫是被一次真事故逼出来的）：</b>阶段 128 我按"面前控件最少"挑中
/// <c>VariableBrowserView</c>、把它升格成第五层的第 12 个宿主、判据全绿、负控也全红在期望的腿上
/// —— **回头核对才发现它全仓零构造、两个公开事件零订阅**（登记 `#71`）：判一个用户到不了的面板，
/// 等于把"本层覆盖了多少"记成假的。当时那条纪律是**人肉**的（我在总结里把它写成"第 ⑤ 条判据"），
/// 人肉纪律会忘 ⇒ 本守卫把它变成**机器检查**。</para>
///
/// <para><b>判据两侧都断言：</b>① 扫到而**不在**账本里 ⇒ 红（新出现一个到不了的视图）；
/// ② 账本里有而它**已经能被构造** ⇒ 红（陈账要清，逼人删掉那一行）。</para>
///
/// <para><b>阶段 137 补记：账本已清空，反方向因此多出一档。</b>两个死视图（`#71` /
/// `#73`）都由用户拍板**删掉** ⇒ 账本 `<see cref="Unreachable"/>` 现在是**空**的
/// （账本空 ≠ 守卫没用：它守的是"下一个到不了的视图"）。⚠ 顺带撞出反方向的一个**报错不准**：
/// 账本里记着一个**已被删除**的视图时，原先那句「它**现在有构造点**了」会把人指去找一个
/// **根本不存在的构造点** —— 「视图没了」与「视图接上了」是两件事，必须分开报。
/// 于是对账逻辑抽成 <see cref="Reconcile"/> 并补了一条**它自己的**自检
/// （<see cref="TheLedgerReconciliation_SeesAllThreeCases"/>）：账本清空之后，
/// 反方向在真实仓库里已经没有活见证，只能靠合成语料钉住。</para>
///
/// <para><b>不抓什么（诚实边界）：</b></para>
/// <list type="number">
///   <item><b>不判"构造了但用不到"</b> —— 它只回答"有没有构造点"，运行期可达性（哪个菜单能打开它）
///     是另一件事。</item>
///   <item><b>不判反射 / ViewLocator 按名字构造</b> —— 本仓的面板由 <c>HmiDockFactory</c> 显式
///     <c>new</c>，没有按类型名反射构造的路径（本阶段实测）；若将来引入，本守卫会把这些视图报成
///     "到不了" ⇒ 那时应当在 <see cref="Unreachable"/> 里登记并写明"经 ViewLocator 构造"，
///     或给扫描器补一条形态。</item>
///   <item><b>不判"注释/字符串里提到过"</b> —— <c>.cs</c> 走 <see cref="SourceScanner.MaskLiterals"/>
///     （注释与字面量内容都抹成空格）、<c>.axaml</c> 走 <see cref="SourceScanner.StripXmlComments"/>；
///     这两条由 <see cref="TheScanner_SeesBothConstructionForms_AndOnlyThose"/> 的合成语料钉着。</item>
/// </list>
/// </summary>
public class UnreachableViewGuardTests
{
    private readonly ITestOutputHelper _output;

    public UnreachableViewGuardTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// **到不了的视图账本**：既没有构造点、也不在别处 XAML 里被当标签用的视图。
    ///
    /// <para>⚠ 每条必须有 <c>Reason</c>（为什么它到不了）与 <c>Trigger</c>（**什么情况下这行必须被删掉**）
    /// —— 没有触发条件的账本行等于永久豁免。</para>
    /// </summary>
    /// <para>🩸 <b>阶段 137 起它是空的</b> —— 阶段 135 登记的两条（`VariableBrowserView` / `#71`、
    /// `PrototypeCanvas` / `#73`）都由用户拍板**删掉**（两个视图 ＋ 各自的本地化键 ＋ 文案，
    /// 见登记表 §59）。这正是本账本**设计上的**归宿：**它不是清理清单，而是"变动必被点名"的账本**
    /// —— 条目只该有"进"和"出"两条路，而"出"就是被删或被接上。
    /// ⚠ 空账本不等于守卫失去意义：它现在守的是**下一个**到不了的视图。也正因如此，
    /// 反方向（"账本里的条目已失效"）在真实仓库里**没有活见证**了 ⇒ 那部分逻辑抽成
    /// <see cref="Reconcile"/> 并用合成语料自检（见类文档）。</para>
    private static readonly (string View, string Reason, string Trigger)[] Unreachable =
        Array.Empty<(string View, string Reason, string Trigger)>();

    private static string AppProjectDirectory => Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.App");

    /// <summary>产品侧全部 <c>.cs</c> / <c>.axaml</c>（递归、去 bin/obj）。</summary>
    private static List<string> SourceFiles() =>
        Directory.EnumerateFiles(AppProjectDirectory, "*.*", SearchOption.AllDirectories)
            .Where(p => !SourceScanner.IsBuildOutput(p))
            .Where(p => p.EndsWith(".cs", StringComparison.Ordinal) || p.EndsWith(".axaml", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    /// <summary>带 <c>x:Class</c> 的 <c>.axaml</c> ⇒ 「视图类简单名 → 该 .axaml 路径」。</summary>
    internal static Dictionary<string, string> ViewsByClassName()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(AppProjectDirectory, "*.axaml", SearchOption.AllDirectories)
                     .Where(p => !SourceScanner.IsBuildOutput(p)))
        {
            var m = Regex.Match(File.ReadAllText(path), @"x:Class=""([\w.]+)""");
            if (!m.Success) continue;                       // 资源字典等没有 x:Class
            var simple = m.Groups[1].Value.Split('.')[^1];
            if (simple == "App") continue;                  // 应用类不是视图
            map[simple] = path;
        }

        return map;
    }

    /// <summary>
    /// 视图 <paramref name="viewName"/> 在**产品代码**里有没有构造点
    /// （<paramref name="ownAxaml"/> 是它自己的 <c>.axaml</c>，跳过不算）。
    /// </summary>
    internal static bool HasConstructionSite(string viewName, string ownAxaml, IEnumerable<string> sourceFiles)
    {
        var name = Regex.Escape(viewName);
        var newInCode = new Regex(@"\bnew\s+(?:[\w.]+\.)?" + name + @"\s*[\(<]");
        var tagInXaml = new Regex(@"<[\w.]*:?" + name + @"[\s/>]");

        foreach (var path in sourceFiles)
        {
            if (string.Equals(path, ownAxaml, StringComparison.Ordinal)) continue;

            if (path.EndsWith(".cs", StringComparison.Ordinal))
            {
                // 注释与**字面量内容**都抹掉 ⇒ "注释/字符串里提到过"不算构造点
                if (newInCode.IsMatch(SourceScanner.MaskLiterals(File.ReadAllText(path)))) return true;
            }
            else if (tagInXaml.IsMatch(SourceScanner.StripXmlComments(File.ReadAllText(path))))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// **对账**：把「扫到的视图（简单名 → 它自己的 .axaml）/ 有构造点的视图 / 账本」三者的不一致
    /// 摊成可读的 offender 列表。
    ///
    /// <para>抽出来是为了**它自己有自检**（阶段 137：账本清空之后，反方向在真实仓库里没有活见证）。
    /// 三档必须在**文案上**分开 —— 它们的处置动作完全不同：</para>
    /// <list type="number">
    ///   <item>扫到、却没构造点、又不在账本里 ⇒ 要么接上、要么登记（**新的**到不了的视图）；</item>
    ///   <item>账本里有、而它已经有构造点 ⇒ 陈账要清（**接上了**）；</item>
    ///   <item>账本里有、而扫描面里**根本没有这个视图** ⇒ 视图被**删了**，本行也要删。
    ///     🩸 这一档是阶段 137 撞出来的：删掉两个死视图时，账本里要是还留着它们，原先那句
    ///     「它**现在有构造点**了」会把人指去找一个**根本不存在的构造点**。</item>
    /// </list>
    /// </summary>
    internal static List<string> Reconcile(
        IReadOnlyDictionary<string, string> viewsByClass,
        IReadOnlyCollection<string> constructed,
        IEnumerable<string> registeredViews)
    {
        var constructedSet = constructed.ToHashSet(StringComparer.Ordinal);
        var registered = registeredViews.ToHashSet(StringComparer.Ordinal);
        var unreachable = viewsByClass.Keys
            .Where(v => !constructedSet.Contains(v))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

        var offenders = new List<string>();
        foreach (var v in unreachable.Where(v => !registered.Contains(v)))
            offenders.Add($"{v}（{SourceScanner.Relative(viewsByClass[v])}）：**在产品代码里找不到任何构造点** —— "
                          + "要么把它接上（`new` 或某个 .axaml 里用它），要么在 UnreachableViewGuardTests."
                          + "Unreachable 里登记一条并写明**理由 ＋ 触发条件**（别让一个用户到不了的视图"
                          + "悄悄成为某个判据的宿主）");
        foreach (var v in registered.Where(v => !unreachable.Contains(v, StringComparer.Ordinal))
                     .OrderBy(v => v, StringComparer.Ordinal))
            offenders.Add(viewsByClass.ContainsKey(v)
                ? $"{v}：账本里有它，但它**现在有构造点**了 —— 陈账要清"
                  + "（接上之后那一行必须删掉，否则账本会变成永久豁免）"
                : $"{v}：账本里有它，但**这个视图已经不存在了**（扫描面里找不到带 `x:Class` 的它）—— "
                  + "视图删掉之后账本那一行也必须删。"
                  + "⚠ 别把它报成「它已经有构造点了」：那会让人去找一个**根本不存在的构造点**");

        return offenders;
    }

    [Fact]
    public void EveryView_IsConstructedSomewhere_OrListedInTheLedger()
    {
        var views = ViewsByClassName();
        var files = SourceFiles();
        Assert.True(views.Count >= 40, $"只扫到 {views.Count} 个带 x:Class 的 .axaml —— 扫描面塌了，本判据会恒真");
        Assert.True(files.Count >= 100, $"只扫到 {files.Count} 个产品源文件 —— 扫描面塌了，本判据会恒真");

        var constructed = views.Keys
            .Where(v => HasConstructionSite(v, views[v], files))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();
        Assert.True(constructed.Count >= 40,
            $"只认出 {constructed.Count} 个视图有构造点 —— 扫描器或产品结构变了，下面的断言会恒真");

        var offenders = Reconcile(views, constructed, Unreachable.Select(x => x.View));

        JudgeReadings.Write(_output, "视图可达性", new[]
        {
            $"视图 {views.Count} 个 / 产品源文件 {files.Count} 个；有构造点 {constructed.Count} 个；"
            + $"到不了 {views.Count - constructed.Count} 个（账本 {Unreachable.Length} 条）",
            "账本：" + (Unreachable.Length == 0
                ? "**空**（阶段 137 起 —— 两个死视图都已下架；下一行由本守卫点名）"
                : string.Join("；", Unreachable.Select(x => x.View))),
            "对账逻辑自检：TheLedgerReconciliation_SeesAllThreeCases（三档：未登记 / 已接上 / **文件已删**）",
        });

        Assert.True(offenders.Count == 0,
            $"视图可达性与账本不一致：{Environment.NewLine}" + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// 扫描器自检（合成语料，**两侧都断言**）：两种构造形态都认得出，四种**不该**算作构造点的形态
    /// 一个都不许进（注释里提到 / 字符串字面量里提到 / 它自己的 .axaml / 别的类型名的前缀命中）。
    /// </summary>
    [Fact]
    public void TheScanner_SeesBothConstructionForms_AndOnlyThose()
    {
        // ⚠ 用共享的 TestTempDir（本机 /tmp 仅 10 MB，直接 Path.GetTempPath() 会被
        //   TestTempDirUsageGuardTests 当场拦下 —— 阶段 135 实测，它确实拦住了我第一版）
        var tmp = TestTempDir.NewDir("unreachable-scan");
        try
        {
            var own = Path.Combine(tmp, "Widget.axaml");
            File.WriteAllText(own, "<UserControl x:Class=\"X.Widget\"/>");

            var byNew = Path.Combine(tmp, "a.cs");
            File.WriteAllText(byNew, "var w = new Widget();\n");
            var byQualifiedNew = Path.Combine(tmp, "b.cs");
            File.WriteAllText(byQualifiedNew, "var w = new Views.Widget(1, 2);\n");
            var byXamlTag = Path.Combine(tmp, "c.axaml");
            File.WriteAllText(byXamlTag, "<StackPanel><editors:Widget x:Name=\"W\"/></StackPanel>\n");

            var commentOnly = Path.Combine(tmp, "d.cs");
            File.WriteAllText(commentOnly, "// 这里提到过 Widget\n/* Widget */\n");
            var stringOnly = Path.Combine(tmp, "e.cs");
            File.WriteAllText(stringOnly, "var s = \"Widget\";\n");
            var prefixOnly = Path.Combine(tmp, "f.cs");
            File.WriteAllText(prefixOnly, "var w = new WidgetFactory();\nvar u = new Widgets();\n");
            var xamlCommentOnly = Path.Combine(tmp, "g.axaml");
            File.WriteAllText(xamlCommentOnly, "<!-- <editors:Widget/> -->\n<TextBlock/>\n");

            // ① 正控三档：两种 `new` 形态 ＋ XAML 标签形态，各自单独就该认出
            Assert.True(HasConstructionSite("Widget", own, new[] { byNew }));
            Assert.True(HasConstructionSite("Widget", own, new[] { byQualifiedNew }));
            Assert.True(HasConstructionSite("Widget", own, new[] { byXamlTag }));

            // ② 反控四档：都不算构造点
            Assert.False(HasConstructionSite("Widget", own, new[] { commentOnly }));
            Assert.False(HasConstructionSite("Widget", own, new[] { stringOnly }));
            Assert.False(HasConstructionSite("Widget", own, new[] { prefixOnly }));
            Assert.False(HasConstructionSite("Widget", own, new[] { xamlCommentOnly }));

            // ③ 它自己的 .axaml 不算（跳过自己那份文件）
            Assert.False(HasConstructionSite("Widget", own, new[] { own }));

            JudgeReadings.Write(_output, "视图可达性/自检", new[]
            {
                "三种构造形态：new Widget() / new Views.Widget(…) / <editors:Widget/>",
                "四种不算：注释、字符串字面量、类型名前缀（WidgetFactory/Widgets）、XAML 注释、以及它自己的 .axaml",
            });
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* 收尾尽力而为 */ }
        }
    }

    /// <summary>
    /// **对账逻辑自己的自检**（阶段 137 加）——三档反控 ＋ 一档正控。
    ///
    /// <para>为什么非加不可：账本清空之后，「账本里的条目已失效」这个方向在**真实仓库里**
    /// 没有活见证（要触发它得先去改坏账本）。合成语料是唯一能把它钉住的地方 ——
    /// 尤其第三档（视图**已删**）必须与第二档（视图**已接上**）在**文案上**分开。</para>
    /// </summary>
    [Fact]
    public void TheLedgerReconciliation_SeesAllThreeCases()
    {
        // ⚠ 每一档都**只留**它要判的那一个条件（否则 offender 会串档 —— 首跑实测：
        //   ② 那档一开始把「没登记的孤儿」也带上了，于是收到 2 条而不是 1 条）。
        var both = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LiveOk"] = "/x/LiveOk.axaml",          // 有构造点
            ["LiveOrphan"] = "/x/LiveOrphan.axaml",  // 没有构造点
        };
        var onlyOk = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LiveOk"] = "/x/LiveOk.axaml",
        };

        // ① 扫到却既没构造点、又不在账本里 ⇒ 点名「找不到任何构造点」
        var caseA = Reconcile(both, new[] { "LiveOk" }, Array.Empty<string>());
        Assert.Single(caseA);
        Assert.Contains("LiveOrphan", caseA[0], StringComparison.Ordinal);
        Assert.Contains("找不到任何构造点", caseA[0], StringComparison.Ordinal);

        // ② 账本里有、而它已经有构造点 ⇒ 点名「现在有构造点」（陈账要清）
        var caseB = Reconcile(onlyOk, new[] { "LiveOk" }, new[] { "LiveOk" });
        Assert.Single(caseB);
        Assert.Contains("现在有构造点", caseB[0], StringComparison.Ordinal);

        // ③ 账本里的视图**已经不存在了** ⇒ 必须是**另一句**（不能说「现在有构造点」）
        var caseC = Reconcile(onlyOk, new[] { "LiveOk" }, new[] { "DeletedLongAgo" });
        Assert.Single(caseC);
        Assert.Contains("已经不存在了", caseC[0], StringComparison.Ordinal);
        Assert.DoesNotContain("现在有构造点", caseC[0], StringComparison.Ordinal);

        // ④ 正控：既没构造点、又在账本里 ⇒ 一个 offender 都没有
        var caseD = Reconcile(both, new[] { "LiveOk" }, new[] { "LiveOrphan" });
        Assert.Empty(caseD);

        JudgeReadings.Write(_output, "视图可达性/对账自检", new[]
        {
            "① 未登记 ⇒「找不到任何构造点」；② 账本 + 有构造点 ⇒「现在有构造点」（陈账要清）；",
            "③ 账本 + 视图已删 ⇒「这个视图已经不存在了」（**另一句**，阶段 137 撞出来的报错不准）；④ 账本 + 仍到不了 ⇒ 0 offender",
        });
    }
}
