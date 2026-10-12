using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「**面板重建失败必须说出来**」的守卫 —— 登记表 §4 <b>#61u</b>（阶段 98）。
///
/// <para><b>它守的是哪一类缺陷</b>：<c>MainWindow.OnDockLayoutChanged</c> 改动前长这样 ——</para>
/// <code>
/// try
/// {
///     StatusText.Text = LocService["status.reloading.panels"];
///     Dispatcher.UIThread.Post(() => { if (!_rebuildAgain) RebuildAllPanels(); … });
/// }
/// catch (Exception ex) { StatusText.Text = LocService.Format("status.reload.failed", ex.Message); }
/// </code>
/// <para>那个 <c>try</c> 里只有**一次赋值 ＋ 一次 <c>Post</c> 入队**，而 <c>Post</c> 只是入队、
/// **不抛** ⇒ 那条 <c>catch</c> **永不触发**（死 catch）。真正会抛的是回调里的
/// <c>RebuildAllPanels()</c>，它**没有任何人接** ⇒ 面板重建一旦失败，状态栏就**永久停在
/// 「正在重载面板...」**（比"显示英文原文"更糟：连原因都没有）。</para>
///
/// <para><b>处置</b>：把**重建连它的失败处理一起搬进回调**，出口收在
/// <see cref="MainWindow.TryRebuildPanels"/> / <c>RebuildFailureText</c> 这一对里。</para>
///
/// <para><b>⚠ 为什么这次**不**把出口换成 <c>ExternalText.FailureReason(ex)</c></b>
/// （本项与同阶段 #61t 的分叉点，下一个人最容易"顺手统一"）：这一处接的是
/// **面板重建内部**的意外异常（BCL / 内部原文），**产出方给不出码** ⇒ 换出口只会落进
/// 「回落原文」那一支、用户所见**一字不变** —— 那正是阶段 83 钉死的**改名式假闭环**
/// （登记 #61 的原始教训、#61b 的「C 类：不该本地化」）。本项的收益是
/// 「**异常不再被吞**」，不是换措辞；所以
/// <c>ExceptionMessagePassthroughFaceTests</c> 的基线里**照旧留着一行**（搬了文件、没消失）。</para>
///
/// <para><b>⚙ 判据为什么是「静态 helper 的行为」＋「接线形状」两半</b>：</para>
/// <list type="number">
///   <item><b>行为</b>：<c>TryRebuildPanels</c> 是 <c>internal static</c> 且两个依赖
///     （重建动作 / 写文案）都是**参数** —— 这是刻意的设计（见它的方法头）：判据喂一个
///     **会抛的委托**即可，不必驱动 Dispatcher。Avalonia 的 headless 派发有既知死区
///     （<c>ScrollViewer</c>+<c>Wrap</c> 首次布局不收敛 / 会话级 UI 线程单例），
///     让判据去驱动"Post 进队列再跑回调"是**自找挂死**。</item>
///   <item><b>形状</b>：行为判据只证明"那个 helper 是对的"，**证明不了生产路径在用它** ——
///     把 <c>PostPanelRebuild</c> 改回裸 <c>RebuildAllPanels()</c>，helper 还在、行为判据照样绿。
///     所以另有一条钉住 <c>OnDockLayoutChanged</c>→<c>PostPanelRebuild</c> 那一段的接线。</item>
/// </list>
///
/// <para><b>⚙ 本类会切换进程级全局语言 ⇒ 必须挂 <c>[Collection("Language-global")]</c></b>
/// （与 <c>UserAdminIssueLocalizationGuardTests</c> 同一条纪律）；切换必然 <c>finally</c> 还原。</para>
///
/// <para><b>诚实边界</b>：</para>
/// <list type="number">
///   <item>不驱动真 Dispatcher、不开真窗口 —— 断的是「重建抛异常时**写出去的是本地化的失败文案**」，
///     不是"浏览器/界面上肉眼看到了它"；</item>
///   <item>断不了 <c>status.reload.failed</c> 的**文案质量**；</item>
///   <item>形状判据的锚点是那两个方法名，改名会让它**报红并说明找不到锚点**
///     （fail-closed：宁可报错也不静默变绿）。</item>
/// </list>
/// </summary>
[Collection("Language-global")]
public class PanelRebuildFailureUiTests
{
    private readonly ITestOutputHelper _out;

    public PanelRebuildFailureUiTests(ITestOutputHelper output) => _out = output;

    /// <summary>面板重建失败走的状态栏键。**故意是字符串字面量**（与生产代码一致）。</summary>
    internal const string StatusKey = "status.reload.failed";

    /// <summary>接线所在文件（相对仓库根）。</summary>
    internal const string Producer = "src/HmiEditor.App/MainWindow.axaml.cs";

    /// <summary>
    /// 接线切片的两端锚点：从 <c>OnDockLayoutChanged</c> 的声明起、到 <c>PostPanelRebuild</c> 的末行止。
    ///
    /// <para>⚠ 这两个锚点**必须唯一**：找不到就报红并说明"锚点没了"（fail-closed），
    /// 而不是退化成"扫不到 ⇒ 没违规 ⇒ 绿"的假门禁。</para>
    /// </summary>
    internal const string SliceStart = "private void OnDockLayoutChanged()";
    internal const string SliceEnd = "DispatcherPriority.Background);";

    /// <summary>接线必须长这样 —— 重建经 helper、文案经状态栏 setter。</summary>
    internal const string RequiredWiring = "TryRebuildPanels(RebuildAllPanels, t => StatusText.Text = t)";

    private static readonly Regex WordTry = new(@"\btry\b", RegexOptions.Compiled);

    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    // ══════════════════════════ 判据逻辑（纯函数 —— 负控直接调它） ══════════════════════════

    /// <summary>某本字典里这个键**填上实参**之后的串；键不在字典里返回 <c>null</c>。</summary>
    internal static string? Expected(Dictionary<string, string> dict, string key, params object[] args)
    {
        if (!dict.TryGetValue(key, out var body)) return null;
        return args.Length > 0
            ? string.Format(CultureInfo.InvariantCulture, body, args)
            : body;
    }

    /// <summary>
    /// **接线形状**：<c>OnDockLayoutChanged</c>→<c>PostPanelRebuild</c> 那一段里，
    /// 重建必须**经 <c>TryRebuildPanels</c>**、且**不再有裸调用**；另外不许再出现真 <c>try</c>。
    ///
    /// <para><b>为什么"不许有裸调用"是硬判据</b>：改动前的缺陷就是这么来的 ——
    /// 失败处理在回调**外面**（死 catch），回调里裸调 <c>RebuildAllPanels()</c>。
    /// 行为判据看不见这一半（helper 仍然正确）。</para>
    ///
    /// <para><b>为什么还禁 <c>try</c></b>：那一段唯一的合法做法就是"把重建交出去"。
    /// 出现 <c>try</c> 意味着又有人开始在那儿就地兜异常 —— 而 <c>Post</c> 入队不抛的坑
    /// 正是这么踩的（阶段 83 的"宽捕获换出口"是另一个方向的同一种错）。</para>
    /// </summary>
    internal static List<string> WiringViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();

        var hit = sources.FirstOrDefault(s => string.Equals(s.Relative, Producer, StringComparison.Ordinal));
        if (hit.Text is null) { bad.Add($"{Producer}：语料里找不到这个文件（扫描面失效？）"); return bad; }

        var code = SourceScanner.StripComments(hit.Text);

        var i = code.IndexOf(SliceStart, StringComparison.Ordinal);
        if (i < 0) { bad.Add($"{Producer}：找不到 `{SliceStart}`（锚点没了 / 方法改名）⇒ 接线判据失效"); return bad; }

        var j = code.IndexOf(SliceEnd, i, StringComparison.Ordinal);
        if (j < 0) { bad.Add($"{Producer}：在这一段里找不到 `{SliceEnd}`（锚点没了 / 方法改名）⇒ 接线判据失效"); return bad; }

        var slice = code[i..(j + SliceEnd.Length)];

        var wired = CountOccurrences(slice, RequiredWiring);
        if (wired != 1)
            bad.Add($"{Producer}：`{RequiredWiring}` 出现 {wired} 次，应为 **1** ⇒ "
                + "重建没经 TryRebuildPanels（0 次 = 失败处理又跑到回调外面去了，"
                + "也就是那条**永不触发**的 catch；>1 次 = 同一段里接了两次）");

        var bare = CountOccurrences(slice, "RebuildAllPanels()");
        if (bare != 0)
            bad.Add($"{Producer}：这一段里还有 {bare} 处**裸调** `RebuildAllPanels()` ⇒ "
                + "它抛出的异常会从回调里穿出去，状态栏永久停在「正在重载面板...」（登记 #61u）");

        var tries = WordTry.Matches(slice).Count;
        if (tries != 0)
            bad.Add($"{Producer}：这一段里还有 {tries} 处 `try` ⇒ 失败处理又被写在回调外面了"
                + "（`Dispatcher.UIThread.Post` 只是入队、**不抛** ⇒ 那个 catch 永不触发）");

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
    /// **收益正面见证**：重建抛异常时 —— 返回 <c>false</c>、且写出去的是
    /// **该语言的 <c>status.reload.failed</c> 模板填上真实原因**（两种语言各渲染一遍）。
    ///
    /// <para>毒化/回落都不用在这里：这一处**刻意保留 <c>ex.Message</c>**（产出方给不出码），
    /// 所以"渲染里含原因原文"是**设计目标**、不是缺陷。要钉的是另一半：
    /// ① 键真的被查到了（两本字典各等于自己的模板 ⇒ 缺键会红）；
    /// ② 实参真的被填进去了（<c>Format</c> 在个数不符时**不抛异常**）。</para>
    /// </summary>
    [Fact]
    public void OnFailure_ReturnsFalseAndWritesTheLocalizedReason_InBothLanguages()
    {
        var boom = new InvalidOperationException("面板重建炸了");
        string? written = null;

        var ok = MainWindow.TryRebuildPanels(() => throw boom, t => written = t);

        Assert.False(ok, "重建抛异常时 TryRebuildPanels 必须返回 false（调用点据此提前 return，不再写「重载完成」）");
        Assert.True(written is not null,
            "重建抛异常时**必须**写出失败文案 —— 改动前这里什么都没有，状态栏永久停在「正在重载面板...」（登记 #61u）");

        var rows = new List<(string Language, string Rendered, string? Expected)>();
        var original = AppLocalization.Service.CurrentLanguage;

        try
        {
            foreach (var (lang, dict) in new[] { ("zh-CN", Zh), ("en-US", En) })
            {
                AppLocalization.Service.SwitchLanguage(lang);
                rows.Add((lang, MainWindow.RebuildFailureText(boom),
                    Expected(dict, StatusKey, boom.Message)));
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        JudgeReadings.Write(_out, "面板重建失败的状态栏文案（#61u · status.reload.failed）",
            rows.Select(r => $"    {r.Language} ⇒ {r.Rendered}").ToList());

        var bad = rows.Where(r => r.Expected is null)
            .Select(r => $"    {r.Language}：字典里没有 {StatusKey} ⇒ 该语言下用户读到的是拼出来的中文/原文")
            .Concat(rows.Where(r => r.Expected is not null
                                    && !string.Equals(r.Rendered, r.Expected, StringComparison.Ordinal))
                .Select(r => $"    {r.Language}：渲染「{r.Rendered}」≠ 该语言字典填参后的「{r.Expected}」"))
            .ToList();

        Assert.True(bad.Count == 0,
            "面板重建失败的文案没走字典（登记 #61u / 阶段 98）—— 本项的收益是「异常不再被吞」，"
            + "键与实参照旧要齐：\n" + string.Join("\n", bad));

        // 自检：语言真的切动了。静默没切会让 en 那一遍渲染成中文 ——
        // 那时上面的期望比对已经会红，但红在"渲染 ≠ 期望"上，指错方向。
        Assert.True(!string.Equals(rows[0].Rendered, rows[1].Rendered, StringComparison.Ordinal),
            $"zh 与 en 渲染一模一样「{rows[0].Rendered}」⇒ 语言切换没生效（渲染没跟着当前语言走）");
    }

    /// <summary>
    /// 成功档：返回 <c>true</c>、**一个字都不写**。写这一条是为了钉住"
    /// <c>false</c> 那条路径上的返回值**有意义**" —— 只断"失败时返回 false"的话，
    /// 一个永远返回 <c>false</c> 的实现也能过。
    /// </summary>
    [Fact]
    public void OnSuccess_ReturnsTrueAndWritesNothing()
    {
        var wrote = 0;

        var ok = MainWindow.TryRebuildPanels(() => { }, _ => wrote++);

        Assert.True(ok, "重建成功时 TryRebuildPanels 必须返回 true（调用点据此继续写「重载完成」）");
        Assert.Equal(0, wrote);
    }

    /// <summary>生产路径必须**真的在用**那个 helper。</summary>
    [Fact]
    public void ProductionPath_WiresTheRebuildThroughTheHelper()
    {
        var problems = WiringViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "面板重建的接线（#61u）", new List<string>
        {
            $"    {Producer}",
            $"    切片：`{SliceStart}` → `{SliceEnd}`",
            $"    必需接线：{RequiredWiring}",
        });

        Assert.True(problems.Count == 0,
            "生产路径的接线不对（登记 #61u / 阶段 98）—— 行为判据只证明 helper 是对的，"
            + "证明不了它在被用：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 负控 —— 每组都要求"该红的红、该绿的绿"（本项目对负控的一贯要求）。
    /// 注入全部走**合成语料**，不动真仓库。
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        static string Corpus(string body) => "class C {\n" + body + "\n}\n";

        var compliant = Corpus(
            "    " + SliceStart + "\n"
            + "    {\n"
            + "        StatusText.Text = LocService[\"status.reloading.panels\"];\n"
            + "        PostPanelRebuild(rebuildAgain: true);\n"
            + "    }\n\n"
            + "    private void PostPanelRebuild(bool rebuildAgain) =>\n"
            + "        Dispatcher.UIThread.Post(\n"
            + "            () =>\n"
            + "            {\n"
            + "                if (!" + RequiredWiring + ") return;\n"
            + "                if (rebuildAgain) PostPanelRebuild(rebuildAgain: false);\n"
            + "                else StatusText.Text = LocService[\"status.panels.reloaded\"];\n"
            + "            },\n"
            + "            rebuildAgain ? DispatcherPriority.Loaded : DispatcherPriority.Background);\n");

        // ── NC0 · 正控（**必须合成**：外部负控会让它跟着红）──
        Assert.Empty(WiringViolations(new[] { (Producer, compliant) }));

        // ── NC1 · 退回"裸调 + 回调外的死 catch"（改动前的形状）⇒ 三条都点名 ──
        var regressed = WiringViolations(new[] { (Producer, Corpus(
            "    " + SliceStart + "\n"
            + "    {\n"
            + "        StatusText.Text = LocService[\"status.reloading.panels\"];\n"
            + "        try { Dispatcher.UIThread.Post(() => RebuildAllPanels()); }\n"
            + "        catch (Exception ex) { StatusText.Text = LocService.Format(\"status.reload.failed\", ex.Message); }\n"
            + "    }\n\n"
            + "    private void PostPanelRebuild(bool rebuildAgain) =>\n"
            + "        Dispatcher.UIThread.Post(() => { }, DispatcherPriority.Background);\n")) });
        Assert.Contains(regressed, v => v.Contains("出现 0 次", StringComparison.Ordinal));
        Assert.Contains(regressed, v => v.Contains("裸调", StringComparison.Ordinal));
        Assert.Contains(regressed, v => v.Contains("`try`", StringComparison.Ordinal));

        // ── NC2 · 锚点没了 ⇒ 必须报"找不到"，不许静默变绿（fail-closed）──
        Assert.Contains(
            WiringViolations(new[] { (Producer, Corpus("    private void SomethingElse() { }")) }),
            v => v.Contains("找不到", StringComparison.Ordinal));

        // 只改掉后半段锚点（方法还在、末行没了）⇒ 也要点名
        Assert.Contains(
            WiringViolations(new[] { (Producer, Corpus(
                "    " + SliceStart + "\n    {\n        PostPanelRebuild(true);\n    }\n")) }),
            v => v.Contains($"找不到 `{SliceEnd}`", StringComparison.Ordinal));

        // ── NC3 · 语料里缺这个文件 ⇒ 也要报"找不到" ──
        Assert.Contains(
            WiringViolations(Array.Empty<(string, string)>()),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ── NC4 · 注释里写的反例不算（规则不许被自己的文档满足，登记 #48）──
        Assert.Empty(WiringViolations(new[] { (Producer,
            compliant + "\n// 反例（不许这么写）：裸调 RebuildAllPanels(); 并 try { } catch\n") }));

        // ── NC5 · 行为判据的正控（合成，不碰 Dispatcher）──
        // 一个永远返回 false 的实现会让"成功档"红 —— 那条判据确实在量"返回值有意义"。
        Assert.True(MainWindow.TryRebuildPanels(() => { }, _ => { }));
        Assert.False(MainWindow.TryRebuildPanels(() => throw new InvalidOperationException("x"), _ => { }));
    }
}
