using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// <see cref="UiOperationGuard"/> 的本地化契约。
///
/// <para><b>为什么这是一条契约而不是风格</b>：<c>Run/RunAsync</c> 的第一个实参（操作名）
/// 会进**用户可见**的失败提示 —— <c>report($"{操作名}失败: {ex.Message}")</c>
/// 落到视图状态行或全局状态栏。传字符串字面量就等于在调用点写死中文，
/// 英文界面下会中英混排；而这**没有任何编译期约束**，所以用两条测试钉住：
/// ① 所有调用点传的是 <c>Lang.*</c> 键（源码扫描）；
/// ② 真的失败一次时，报出来的文案是"字典里的操作名 + 字典里的模板"（行为）。</para>
/// </summary>
[Collection("Language-global")]
public class UiOperationGuardContractTests
{
    /// <summary>
    /// 匹配"操作名"实参的调用点：<c>UiOperationGuard.Run/RunAsync</c> 与
    /// <c>MainWindow.ReportOperationFailure</c>（后者是同一个模式在本地的历史抄本）。
    /// 捕获第一个实参（到第一个逗号或右括号为止）。
    /// </summary>
    private static readonly Regex CallSite = new(
        @"(?:UiOperationGuard\.(?:Run|RunAsync)|ReportOperationFailure)\s*\(\s*([^,\)]+)",
        RegexOptions.Compiled);

    [Fact]
    public void EveryCallSite_PassesALocalizationKey()
    {
        var bad = new List<string>();
        var keyed = 0;

        foreach (var (relative, text) in AppSourceScanner.ScanAppSources())
        {
            foreach (Match m in CallSite.Matches(text))
            {
                var arg = m.Groups[1].Value.Trim();

                if (arg.StartsWith("Lang.", StringComparison.Ordinal)) { keyed++; continue; }

                // 方法**声明**里的形参（`string operationKey`）不是调用点
                if (arg.StartsWith("string ", StringComparison.Ordinal)) continue;

                bad.Add($"{relative}: {m.Value.Trim()}");
            }
        }

        // 自检：正则失配时下面的断言会变成空跑恒绿
        Assert.True(keyed >= 20, $"只扫到 {keyed} 处传了 Lang.* 键的调用 —— 扫描器可能失效");
        Assert.True(bad.Count == 0,
            "这些调用点的操作名不是本地化键（英文界面下会中英混排）：\n    " + string.Join("\n    ", bad));
    }

    [Fact]
    public async Task RunAsync_ReportsTheLocalizedFailureMessage()
    {
        string? reported = null;

        await UiOperationGuard.RunAsync(
            Lang.UiOpPickFile,
            () => throw new InvalidOperationException("boom"),
            msg => reported = msg);

        Assert.NotNull(reported);
        // 报出来的应当是"字典里的操作名 + 字典里的模板"拼出来的
        var expected = AppLocalization.Service.Format(
            Lang.UiOpFailedDetail, AppLocalization.Service[Lang.UiOpPickFile], "boom");
        Assert.Equal(expected, reported);

        // 键没落进字典时索引器会回退成键名 —— 用它判定"文案真的来自字典"
        Assert.DoesNotContain(Lang.UiOpPickFile, reported!, StringComparison.Ordinal);
        Assert.Contains("boom", reported!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 失败文案的两条模板是**共享**的：<c>UiOperationGuard</c> 与
    /// <c>MainWindow.ReportOperationFailure</c> 都用它们。
    ///
    /// <para>这条钉住"用的是哪两条模板"：若有人把 <see cref="UiOperationGuard.FailureDetail"/>
    /// 改成别的模板（或忘了带原因参数），两处措辞就会分叉 —— 而这正是本地化之前的状态
    /// （同一句话在两层各写了一遍）。</para>
    /// </summary>
    [Fact]
    public void FailureFormatters_UseTheSharedTemplatesAndResolveTheOperationName()
    {
        var op = AppLocalization.Service[Lang.UiOpSaveProject];

        var summary = UiOperationGuard.FailureSummary(Lang.UiOpSaveProject);
        var detail = UiOperationGuard.FailureDetail(Lang.UiOpSaveProject, "disk full");

        Assert.Equal(AppLocalization.Service.Format(Lang.UiOpFailed, op), summary);
        Assert.Equal(AppLocalization.Service.Format(Lang.UiOpFailedDetail, op, "disk full"), detail);

        // 键没进字典时索引器/Format 会回退成键名 —— 用它判定"文案真的来自字典"
        Assert.DoesNotContain(Lang.UiOpSaveProject, summary, StringComparison.Ordinal);
        Assert.Contains("disk full", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Cancellation_IsNotReportedAsFailure()
    {
        // 用户取消（关对话框、取消下载）不是失败，不该弹提示 —— 顺手钉住这条既有语义
        string? reported = null;

        await UiOperationGuard.RunAsync(
            Lang.UiOpPickFile,
            () => throw new OperationCanceledException(),
            msg => reported = msg);

        Assert.Null(reported);
    }

    // ═══════════════ 判据：「带上对象」出口必须真的带对象（阶段 86 / #61b） ═══════════════

    /// <summary><c>FailureDetail(...)</c> 的调用点。</summary>
    private static readonly Regex DetailCallSite = new(@"FailureDetail\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// **定义面**：<c>UiOperationGuard</c> 自己那个文件。判据的输入面必须把它整个剔掉 ——
    /// 见 <see cref="ConcretePathViolations"/> 的说明。
    /// </summary>
    private const string DefinitionFace = "src/HmiEditor.App/Services/UiOperationGuard.cs";

    /// <summary>
    /// 语料 → 违规清单（空 = 通过）。语料与定义面**都当参数传入** ⇒ 负控能喂合成语料（不碰磁盘）。
    ///
    /// <para><b>为什么必须把定义面剔掉</b>：两实参重载被三实参重载**在内部调用**
    /// （<c>targetPath is null</c> 的那个实现分支）—— 那是正确实现，不是违规。
    /// 把定义面留在输入里，判据就只能靠一张"文件白名单"去豁免它，而
    /// **判据的输入面含定义面 ⇒ 判据被那张表养着**（登记 #55 的教训）。</para>
    /// </summary>
    internal static List<string> ConcretePathViolations(
        IEnumerable<(string Relative, string Text)> sources, string definitionFace)
    {
        var bad = new List<string>();

        foreach (var (relative, text) in sources)
        {
            if (string.Equals(relative, definitionFace, StringComparison.Ordinal)) continue;

            // 先剥注释：讲规则的注释里会写出 `FailureDetail(op, null, ex)` 这种**反例**
            // （本文件的类注释里就写着），不剥的话规则会被自己的文档满足（登记 #48 踩过三次）。
            var code = SourceScanner.StripComments(text);

            foreach (Match m in DetailCallSite.Matches(code))
            {
                var args = LocalizedLogCallSiteTests.SplitTopLevelArguments(code, m.Index + m.Length - 1);
                if (args.Count == 0) continue;

                // 方法**声明**（`public static string FailureDetail(string operationKey, …)`）不是调用点
                if (args[0].StartsWith("string ", StringComparison.Ordinal)) continue;

                if (args.Count < 3)
                {
                    bad.Add($"{relative}：只给了 {args.Count} 个实参 ⇒ 落到**不带文件**的模板，"
                          + $"用户看不到是哪个文件（`{m.Value.Trim()}…`）");
                    continue;
                }

                if (args[1] is "null" or "default")
                    bad.Add($"{relative}：目标路径实参写成了 `{args[1]}` ⇒ "
                          + "面绿着，而用户看到的文案里**没有文件**");
            }
        }

        return bad;
    }

    /// <summary>
    /// **#61b 的"带上对象"判据**：凡走三实参出口 <c>FailureDetail(操作名, 目标路径, 异常)</c> 的调用点，
    /// 目标路径必须是**具体表达式**（不得是 <c>null</c> 字面量），也不得退回两实参重载。
    ///
    /// <para><b>为什么需要它 —— <c>ExceptionMessagePassthroughFaceTests</c> 看不见的那一半</b>：
    /// 那张面数的是"还有多少站点是 <c>Format(键, ex.Message)</c> 形状"。一个站点只要**不再那样写**
    /// 就从面上消失 —— 至于它换成了 <c>FailureDetail(op, path, ex)</c> 还是
    /// <c>FailureDetail(op, null, ex)</c>，面**完全看不见**。而后者用户看到的文案里没有文件，
    /// 恰是 #61b 要修的东西 ⇒ 迁移就成了"面绿着、收益为零"的**第二次假闭环**
    /// （第一次是阶段 83 钉下的"改名式假闭环"）。</para>
    ///
    /// <para><b>为什么不许退回两实参重载</b>：<c>FailureDetail(string, Exception)</c> 在外部站点
    /// 用它 = 明确宣告"这次失败不告诉你哪个文件"，那需要**理由**而不是默认动作。
    /// 唯一「结构上拿不到路径」的调用点（<c>MainWindow.OpenProject</c> 外层 catch —— 那一刻
    /// 用户还没选文件）走的是两实参重载，**不经过本判据的面**，所以这里没有白名单。</para>
    ///
    /// <para><b>白名单为空，且这不是遗漏</b>：阶段 86 实测 <c>src/</c> 全部 <b>11</b> 处外部调用点
    /// 都是三实参 + 非空表达式（<c>MainWindow</c> ×4 / <c>BuildPipelineService</c> ×3 /
    /// <c>AssetManagerView</c> ×2 / <c>CommunicationConfigView</c> / <c>RecipeEditorView</c>）。
    /// ⇒ 本判据若变红，正确动作是**去拿那个路径**（拿不到就把承接变量提到 <c>try</c> 外），
    /// 不是往白名单里加一行。探针读数见专题 <c>docs/2026-09-23-exception-passthrough-triage.md</c> §86。</para>
    /// </summary>
    [Fact]
    public void EveryDetailCallSite_PassesAConcreteTargetPath()
    {
        var sources = AppSourceScanner.ScanAllProductSources();

        // 自检 ①：定义面必须真在语料里 —— 否则上面那行排除是**空操作**，
        // 判据会转而去点名定义面里那几处合法的两实参调用（红，但红得没有信息）。
        Assert.True(sources.Any(s => s.Relative == DefinitionFace),
            $"语料里找不到定义面 {DefinitionFace} —— 排除成了空操作，`ScanAllProductSources` 的面变了");

        // 自检 ②：面必须真的扫到调用点 —— 正则失配时下面那条断言会变成空跑恒绿
        var sites = sources.Where(s => s.Relative != DefinitionFace)
            .Sum(s => DetailCallSite.Matches(SourceScanner.StripComments(s.Text)).Count);
        Assert.True(sites >= 10, $"只扫到 {sites} 处 FailureDetail 调用点 —— 扫描口径可能失效了");

        var problems = ConcretePathViolations(sources, DefinitionFace);

        Assert.True(problems.Count == 0,
            "这些站点走了三实参出口，却没有给出**具体的目标路径**（阶段 86 / #61b）：\n    "
            + string.Join("\n    ", problems)
            + "\n\n正确动作是去拿那个路径（拿不到就把承接变量提到 try 外），不是往白名单里加一行。");
    }

    /// <summary>
    /// 负控（5 组）：把违规形状**注入合成语料**，判据必须点名它；合规形状必须**无话可说**。
    ///
    /// <para>⚠ ③ 与 ④ 是**成对**的，缺一不可：只测"抓得到"等于没测假阳性 ——
    /// 一条永远返回违规的判据也能过 ①②。⑤ 钉住"规则不许被自己的文档满足"（登记 #48）。</para>
    /// </summary>
    [Fact]
    public void ConcretePathViolations_FlagsEachInjectedShape()
    {
        const string Def = "src/HmiEditor.App/Services/UiOperationGuard.cs";

        static List<(string Relative, string Text)> Corpus(params string[] texts) =>
            texts.Select((t, i) => ($"src/HmiEditor.App/Fake{i}.cs", t)).ToList();

        // ① 三实参里填 null ⇒ 点名"没有文件"那一支
        var nulled = ConcretePathViolations(
            Corpus("var s = UiOperationGuard.FailureDetail(Lang.UiOpSaveProject, null, ex);"), Def);
        Assert.Single(nulled);
        Assert.Contains("没有文件", nulled[0], StringComparison.Ordinal);

        // ② 退回两实参重载 ⇒ 点名"不带文件"那一支（与 ① 走的是**不同**分支）
        var twoArg = ConcretePathViolations(
            Corpus("var s = UiOperationGuard.FailureDetail(Lang.UiOpSaveProject, ex);"), Def);
        Assert.Single(twoArg);
        Assert.Contains("不带文件", twoArg[0], StringComparison.Ordinal);

        // ③ 合规形状 ⇒ 无话可说（两个调用点都要扫到，别只看第一条）
        var ok = ConcretePathViolations(
            Corpus("var s = UiOperationGuard.FailureDetail(Lang.UiOpSaveProject, path, ex);",
                   "var t = UiOperationGuard.FailureDetail(Lang.UiOpExportCode, file.Path.LocalPath, ex);"), Def);
        Assert.Empty(ok);

        // ④ **同一个注入**，只改"它在哪个文件里"：定义面里不判、别的文件里必判
        //    —— 这条同时证明"排除真的生效"，而不是那段文本本身无害
        var sameShape = "report(FailureDetail(operationKey, ex));";
        Assert.Empty(ConcretePathViolations(new List<(string, string)> { (Def, sameShape) }, Def));
        Assert.Single(ConcretePathViolations(
            new List<(string, string)> { ("src/HmiEditor.App/Fake.cs", sameShape) }, Def));

        // ⑤ 注释里写的反例不算违规（规则不许被自己的文档满足）
        var commented = ConcretePathViolations(
            Corpus("// 反例：FailureDetail(Lang.UiOpSaveProject, null, ex) 不该出现"), Def);
        Assert.Empty(commented);
    }

    // ══════ 判据：承接变量必须被赋成**真值**（阶段 101 / #61a 尾巴） ══════

    /// <summary><c>string? X = null;</c> 形态的**承接变量**声明（目标路径提到 <c>try</c> 外的那种）。</summary>
    private static readonly Regex ConveyedDeclaration = new(
        @"\bstring\?\s+([A-Za-z_]\w*)\s*=\s*null\s*;", RegexOptions.Compiled);

    /// <summary>实参是不是一个**裸标识符**（不是 <c>a.b</c> / <c>files[0]</c> / 字面量）。</summary>
    private static readonly Regex BareIdentifier = new(@"^[A-Za-z_]\w*$", RegexOptions.Compiled);

    /// <summary>一次评审的结果：**被评到的站点数** ＋ **违规清单**。</summary>
    internal sealed record ConveyedPathReview(int Sites, List<string> Violations);

    /// <summary>
    /// 语料 → 承接变量评审。语料**当参数传入** ⇒ 负控能喂合成语料（不碰磁盘）。
    ///
    /// <para><b>它补的是哪一半</b>：<see cref="ConcretePathViolations"/> 只盯**字面量**
    /// <c>null</c> / <c>default</c>（形状可判），而阶段 86 / 90 / 101 迁出来的站点里有一档传的是
    /// **承接变量**（<c>string? X = null;</c> 提到 <c>try</c> 外、循环里再赋值）—— 那种写法下
    /// "有没有真的赋上"**面完全看不见**：阶段 101 用真仓库注入实测过（把 <c>AssetManagerView</c> 的
    /// <c>currentPath = path;</c> 改成 <c>= null;</c> ⇒ 构建通过、<c>App.UiTests</c> **全绿 0 红**）。
    /// ⇒ 本条判据落地时一并更正了 <c>ExternalText.FailureReason</c> 里"那件事另有一条判据盯着"
    /// 那句 —— 它当时只说对了一半。</para>
    ///
    /// <para><b>规则（位置式，不做作用域分析）</b>：对每个 <c>FailureDetail(op, X, ex)</c>，若
    /// <c>X</c> 是裸标识符、且同文件里在**它之前**有 <c>string? X = null;</c> —— 要求声明与调用点
    /// 之间至少有**一次赋成真值**的赋值（<c>X = …</c>，右侧不是 <c>null</c> / <c>default</c>）。
    /// 为什么用位置而不是作用域：同文件可以有两个同名局部（<c>CommunicationConfigView</c> 就有两处
    /// <c>targetPath</c>），位置规则会把第二个方法那次赋值排除在第一个站点之外 —— 正是想要的；
    /// 真正的作用域解析要做到 Roslyn 那个量级，与收益不成比例。</para>
    ///
    /// <para>⚠ <b>它看不见什么</b>：对**字段**（<c>_selectedFilePath</c>）与**参数**（<c>filePath</c>）
    /// 不适用 —— 它们没有 <c>= null</c> 声明，本判据**不入面**（那是对的：它们不靠"承接"）。
    /// ⚠ 本判据**不剔定义面**：筛子挂在"有 <c>string? X = null;</c> 声明"上，
    /// <c>UiOperationGuard.cs</c> 里的形参 <c>targetPath</c> 恰好没有这种声明 ⇒ 天然不入面。</para>
    /// </summary>
    internal static ConveyedPathReview ReviewConveyedPathVariables(
        IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();
        var sites = 0;

        foreach (var (relative, text) in sources)
        {
            var code = SourceScanner.StripComments(text);
            if (!code.Contains("FailureDetail", StringComparison.Ordinal)) continue;

            var lines = code.Split('\n');

            var decls = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var i = 0; i < lines.Length; i++)
                foreach (Match d in ConveyedDeclaration.Matches(lines[i]))
                {
                    var name = d.Groups[1].Value;
                    if (!decls.TryGetValue(name, out var list))
                        decls[name] = list = new List<int>();
                    list.Add(i);
                }

            if (decls.Count == 0) continue;

            foreach (Match m in DetailCallSite.Matches(code))
            {
                var args = LocalizedLogCallSiteTests.SplitTopLevelArguments(code, m.Index + m.Length - 1);
                if (args.Count < 3) continue;

                // 方法**声明**（`public static string FailureDetail(string operationKey, …)`）不是调用点
                if (args[0].StartsWith("string ", StringComparison.Ordinal)) continue;

                var target = args[1].Trim();
                if (!BareIdentifier.IsMatch(target)) continue;
                if (!decls.TryGetValue(target, out var declared)) continue;

                var callLine = LineNumberOf(code, m.Index);
                var before = declared.Where(d => d < callLine).ToList();
                if (before.Count == 0) continue;   // 同名声明在调用点之后 ⇒ 不是这一处
                sites++;

                var declLine = before.Max();

                // ⚠ `=(?!=)` 挡 `==`；前瞻必须**紧跟**在 `=` 之后 —— 中间若先放一个 `\s*`，
                // 它可以匹配**空串**，于是前瞻看到的是那个空格、把 `p = null;` 判成"赋了真值"
                // （阶段 101 负控 ② 当场抓出来的洞；洞的写法是 `\s*=(?!=)\s*(?!null\b…)`）。
                var assignment = new Regex(
                    $@"\b{Regex.Escape(target)}\s*=(?!=)(?!\s*(?:null|default)\b)", RegexOptions.Compiled);

                var assigned = false;
                for (var i = declLine + 1; i < callLine; i++)
                    if (assignment.IsMatch(lines[i])) { assigned = true; break; }

                if (!assigned)
                    bad.Add($"{relative}：目标路径是承接变量 `{target}`，但它的声明（第 {declLine + 1} 行）"
                          + "与这处 `FailureDetail` 之间没有**赋成真值**的赋值"
                          + " ⇒ 它一直是 null，用户看不到是哪个文件");
            }
        }

        return new ConveyedPathReview(sites, bad);
    }

    /// <summary><paramref name="index"/> 在 <paramref name="text"/> 里的**零基**行号。</summary>
    private static int LineNumberOf(string text, int index)
    {
        var n = 0;
        for (var i = 0; i < index && i < text.Length; i++)
            if (text[i] == '\n') n++;
        return n;
    }

    /// <summary>
    /// **#61b「带上对象」的第二条判据**（阶段 101）：凡目标路径传的是**承接变量**的站点，
    /// 该变量必须被**赋成真值**。
    ///
    /// <para>立项依据是**实测**不是推理：阶段 101 的 NC-A 往真仓库注入
    /// <c>currentPath = null;</c> ⇒ <c>App.UiTests</c> **全绿 0 红** ⇒ 这一档当时无人守。</para>
    /// </summary>
    [Fact]
    public void EveryConveyedPathVariable_IsGivenARealValue()
    {
        var review = ReviewConveyedPathVariables(AppSourceScanner.ScanAllProductSources());

        // 自检：扫描口径失效时下面那条断言会变成空跑恒绿
        Assert.True(review.Sites >= 3,
            $"只评审到 {review.Sites} 处「承接变量当目标路径」的站点 —— 扫描口径可能失效了"
            + "（阶段 101 实测 4 处：BuildPipelineService.targetDir / AssetManagerView.currentPath /"
            + " CommunicationConfigView.targetPath ×2）");

        Assert.True(review.Violations.Count == 0,
            "这些站点的目标路径是承接变量，而它没有被赋成真值（阶段 101）：\n    "
            + string.Join("\n    ", review.Violations));
    }

    /// <summary>
    /// 负控（6 组）：把违规形状**注入合成语料**，判据必须点名它；合规形状必须**无话可说**。
    ///
    /// <para>①②③ 三组成对：只测"抓得到"会漏掉假阳性，只测"放过"会漏掉恒真。
    /// ④ 钉住**位置**在起作用（不是"文件里出现过就算"）；⑤ 钉"规则不许被自己的文档满足"；
    /// ⑥ 钉**同名声明在调用点之后**那一档不误报（同文件两个同名局部）。</para>
    /// </summary>
    [Fact]
    public void ReviewConveyedPathVariableViolations_FlagsEachInjectedShape()
    {
        static List<(string, string)> Corpus(params string[] texts) =>
            texts.Select((t, i) => ($"src/HmiEditor.App/Fake{i}.cs", t)).ToList();

        const string Call = "x = UiOperationGuard.FailureDetail(Lang.UiOpSaveProject, p, ex);";

        // ① 声明之后**一次赋值都没有** ⇒ 点名（阶段 101 生产负控 NC-A 的合成版）
        var noAssign = ReviewConveyedPathVariables(Corpus("string? p = null;\n" + Call));
        Assert.Equal(1, noAssign.Sites);
        Assert.Single(noAssign.Violations);
        Assert.Contains("一直是 null", noAssign.Violations[0], StringComparison.Ordinal);

        // ② 只赋了 null ⇒ 同样点名（"赋过了"不算，"赋成真值"才算）
        var assignsNull = ReviewConveyedPathVariables(Corpus("string? p = null;\np = null;\n" + Call));
        Assert.Single(assignsNull.Violations);

        // ③ 赋成真值 ⇒ 无话可说
        var ok = ReviewConveyedPathVariables(
            Corpus("string? p = null;\np = file.Path.LocalPath;\n" + Call));
        Assert.Equal(1, ok.Sites);
        Assert.Empty(ok.Violations);

        // ④ 赋值在**调用点之后** ⇒ 仍点名（位置在起作用）
        var after = ReviewConveyedPathVariables(Corpus("string? p = null;\n" + Call + "\np = f;\n"));
        Assert.Single(after.Violations);

        // ⑤ 注释里写的反例不算违规（规则不许被自己的文档满足）
        var commented = ReviewConveyedPathVariables(Corpus("// 反例：string? p = null; 之后直接 " + Call));
        Assert.Empty(commented.Violations);

        // ⑥ 声明在调用点**之后**（同文件两个同名局部）⇒ 不误报
        var laterDecl = ReviewConveyedPathVariables(
            Corpus(Call + "\nstring? p = null;\np = f;\n"));
        Assert.Equal(0, laterDecl.Sites);
        Assert.Empty(laterDecl.Violations);
    }

    // ══════ 判据：「带路径」出口不许让目标路径出现两次（阶段 90 / #61h） ══════

    /// <summary>
    /// <paramref name="path"/> 在 <paramref name="rendered"/> 里出现了几次（非重叠计数）。
    ///
    /// <para><b>不用正则</b>：路径里可能有 <c>.</c> / <c>(</c> / <c>[</c> / <c>+</c> 这些正则元字符
    /// （本机临时目录名里就出现过），转义漏一个就会让判据变成恒真或恒假。</para>
    /// </summary>
    internal static int PathOccurrences(string rendered, string path)
    {
        if (path.Length == 0) return 0;

        var count = 0;
        for (var i = rendered.IndexOf(path, StringComparison.Ordinal);
             i >= 0;
             i = rendered.IndexOf(path, i + path.Length, StringComparison.Ordinal))
            count++;

        return count;
    }

    /// <summary>
    /// **违规清单**（空 = 通过）：<paramref name="render"/> 渲出来的文案里，目标路径必须**恰好 1 次**。
    ///
    /// <para><b>它钉的是哪一条推理</b>：阶段 84 反对在 BCL / IO 站点上用三实参重载的**唯一**理由，
    /// 就是「BCL 的 IO 原文自带目标路径」（<c>Access to the path '…' is denied.</c>）⇒ 再包一层
    /// 「—— &lt;路径&gt;」路径会出现**两次**，其中一次还是 <c>.tmp</c> 兄弟路径
    /// （<c>MainWindow.SaveToPath</c> 写的是 <c>path + ".tmp"</c>），**比不带更困惑**。
    /// 阶段 85（#61e）给 BCL 类型建了码、码化短句**不带实参** ⇒ 那条理由消失，阶段 86 / 90 才敢迁。
    /// 但「它消失了」到现在为止一直只是**推理**：谁哪天给 <c>SystemFailureCodes</c> 的短句加个
    /// <c>{0}</c>、或把三实参重载的第三个实参换回 <c>ex.Message</c>，路径就**静默**又出现两次 ——
    /// 而**没有任何现成判据会红**：直通面只认 <c>Format(键, ex.Message)</c> 那个形状
    /// （见到 <c>FailureDetail(op, path, ex)</c> 就当作"已迁"），
    /// <c>EveryDetailCallSite_PassesAConcreteTargetPath</c> 只认「有没有给路径」、不认「给了几遍」。</para>
    ///
    /// <para><b><paramref name="render"/> 当参数传入</b>：负控要喂**旧的坏形状**（原因仍带路径）
    /// 才能证明这条判据不是恒真 —— 见 <see cref="DuplicatePathViolations_FlagsTheOldShapes"/>。</para>
    /// </summary>
    internal static List<string> DuplicatePathViolations(
        IEnumerable<(string OperationKey, string Path, Exception Ex)> cases,
        Func<string, string, Exception, string> render)
    {
        var bad = new List<string>();

        foreach (var (operationKey, path, ex) in cases)
        {
            var rendered = render(operationKey, path, ex);
            var n = PathOccurrences(rendered, path);

            if (n != 1)
                bad.Add($"「{rendered}」里目标路径出现了 {n} 次（应为 1 次）");
        }

        return bad;
    }

    /// <summary>
    /// 一个**真实** BCL 失败：把 <c>File.ReadAllText</c> 打在**真的不存在**的文件上，
    /// 让运行时自己把路径写进原文（**不手搓 <c>Message</c>** —— 手搓等于把前提自己写进夹具，
    /// 「BCL 原文自带路径」就变成自证）。
    ///
    /// <para>两条断言是**前提重测**，不是装饰：它们若哪天变红，说明 .NET 的原文不再带路径、
    /// 或者码表顺序被改了 ⇒ 本判据的立项理由需要**重新量**，而不是把断言改掉。</para>
    /// </summary>
    private static (string Path, Exception Ex) RealMissingFileFailure()
    {
        var path = Path.Combine(TestTempDir.NewDir("uidetail-path-once"), "does-not-exist.csv");
        var ex = Record.Exception(() => File.ReadAllText(path));

        Assert.NotNull(ex);
        Assert.Contains(path, ex!.Message, StringComparison.Ordinal);
        Assert.Equal(SystemFailureCodes.FILE_NOT_FOUND, SystemFailureCodes.CodeFor(ex));

        return (path, ex);
    }

    /// <summary>
    /// **端到端**：真实 BCL 异常 + 真实路径走过三实参出口 ⇒
    /// ① 渲染里路径**恰好 1 次**；② 中英两语言下**操作名与原因那半各是自己的语言**（不混排）。
    ///
    /// <para>② 的三个数也是实测：两语言的操作名不同、两语言的原因短句不同、英文侧**不含汉字**。
    /// 只钉"路径 1 次"而不管语言，就漏掉了这次迁移的**另一半**收益（原因那一半可翻译）——
    /// 而那一半恰恰是 #61e 存在的理由。</para>
    ///
    /// <para>⚠ 本类因此进 <c>[Collection("Language-global")]</c>：切换语言动的是**静态单例**，
    /// 不与别的切语言类串行就是竞态。</para>
    /// </summary>
    [Fact]
    public void PathAppearsExactlyOnce_AndEachHalfInItsOwnLanguage()
    {
        var (path, ex) = RealMissingFileFailure();
        var cases = new[] { (Lang.UiOpImportVariables, path, ex) };

        var original = AppLocalization.Service.CurrentLanguage;
        string zh, en, zhOp, enOp, zhReason, enReason;
        List<string> zhBad, enBad;
        try
        {
            AppLocalization.Service.SwitchLanguage("zh-CN");
            zhOp = AppLocalization.Service[Lang.UiOpImportVariables];
            zhReason = ExternalText.FailureReason(ex);
            zh = UiOperationGuard.FailureDetail(Lang.UiOpImportVariables, path, ex);
            zhBad = DuplicatePathViolations(cases, UiOperationGuard.FailureDetail);

            AppLocalization.Service.SwitchLanguage("en-US");
            enOp = AppLocalization.Service[Lang.UiOpImportVariables];
            enReason = ExternalText.FailureReason(ex);
            en = UiOperationGuard.FailureDetail(Lang.UiOpImportVariables, path, ex);
            enBad = DuplicatePathViolations(cases, UiOperationGuard.FailureDetail);
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        // ① 生产渲染器必须**无话可说**
        //
        // ⚠ 这里**刻意不用 <c>Assert.Empty</c>**：xunit 会把长字符串截断
        // （阶段 90 负控实测报出来的是 `Collection: ["「导入变量失败: /home/…/u"···]`），
        // 「出现了 2 次」这个关键读数**还没被看到就没了** —— 红必须自带信息。
        Assert.True(zhBad.Count == 0,
            "三实参出口渲出来的**中文**文案里，目标路径出现了不该出现的次数：\n    "
            + string.Join("\n    ", zhBad));
        Assert.True(enBad.Count == 0,
            "三实参出口渲出来的**英文**文案里，目标路径出现了不该出现的次数：\n    "
            + string.Join("\n    ", enBad));

        // ② 三件各自就位（操作名 / 路径 / 原因），且两半都真的来自字典
        Assert.Contains(zhOp, zh, StringComparison.Ordinal);
        Assert.Contains(path, zh, StringComparison.Ordinal);
        Assert.Contains(zhReason, zh, StringComparison.Ordinal);
        Assert.Contains(enOp, en, StringComparison.Ordinal);
        Assert.Contains(path, en, StringComparison.Ordinal);
        Assert.Contains(enReason, en, StringComparison.Ordinal);

        // ③ 不混排：中文侧有汉字；英文侧**一个汉字都不许有**（操作名与原因两半都算）
        Assert.NotEqual(zhOp, enOp);
        Assert.NotEqual(zhReason, enReason);
        Assert.True(SystemIssueLocalizationGuardTests.HasCjk(zh), $"中文渲染里没有汉字：{zh}");
        Assert.False(SystemIssueLocalizationGuardTests.HasCjk(en),
            $"英文渲染里混进了汉字（英文界面下中英混排）：{en}");
    }

    /// <summary>
    /// 负控（**成对**）：路径出现两次的**两种真实旧形状**必须被点名，
    /// 而生产渲染器必须无话可说。
    ///
    /// <para>缺任一侧都证明不了它：只测「抓得到」⇒ 一条**恒红**的判据也能过；
    /// 只测「生产侧绿」⇒ 一条**恒绿**的判据也能过。</para>
    ///
    /// <para>两个坏形状都不是编的：① <b>原因那半换回 <c>ex.Message</c></b> —— 正是阶段 84
    /// 反对的那一幕（"BCL 原文自带路径"），也是有人写坏三实参重载时最可能落成的样子；
    /// ② <b>把路径拼进原因串</b>（<c>path + ": " + ex.Message</c>）—— 是 #60 明文写下过的
    /// 那条反例（"三件不各占一个实参"）。两者的落点不同：① 走的仍是三实参模板，
    /// ② 走的是不带文件的两实参模板 + 手工拼接。</para>
    /// </summary>
    [Fact]
    public void DuplicatePathViolations_FlagsTheOldShapes()
    {
        var (path, ex) = RealMissingFileFailure();
        var cases = new[] { (Lang.UiOpImportVariables, path, ex) };

        var original = AppLocalization.Service.CurrentLanguage;
        List<string> production, reasonIsRawMessage, concatenated;
        try
        {
            AppLocalization.Service.SwitchLanguage("zh-CN");

            production = DuplicatePathViolations(cases, UiOperationGuard.FailureDetail);

            reasonIsRawMessage = DuplicatePathViolations(cases, (op, p, e) =>
                AppLocalization.Service.Format(Lang.UiOpFailedDetailFile,
                    AppLocalization.Service[op], p, e.Message));

            concatenated = DuplicatePathViolations(cases, (op, p, e) =>
                UiOperationGuard.FailureDetail(op, p + ": " + e.Message));
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        Assert.Empty(production);

        Assert.Single(reasonIsRawMessage);
        Assert.Contains("出现了 2 次", reasonIsRawMessage[0], StringComparison.Ordinal);

        Assert.Single(concatenated);
        Assert.Contains("出现了 2 次", concatenated[0], StringComparison.Ordinal);
    }
}
