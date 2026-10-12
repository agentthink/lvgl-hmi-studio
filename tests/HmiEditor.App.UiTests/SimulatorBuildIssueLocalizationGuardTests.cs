using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「**模拟器编译 / 启动失败必须由字典决定、且必须真的摆到界面上**」的守卫 ——
/// 登记表 §4 <b>#61c</b>（阶段 103，本族第一刀；也是 <c>#61a</c> / <c>#61b</c> / <c>#61d</c> /
/// <c>#61t</c> 之后的第 N 支「产出方加码 + 站点换出口」）。
///
/// <para><b>它守的是哪一类缺陷</b>：<c>BuildRunDialog</c> 的「编译并运行」主流程只有**一个**
/// <c>catch (Exception ex)</c>，改动前它写的是</para>
/// <code>
/// AppendLog(AppLocalization.Service.Format(Lang.BuildrunLogError, ex.Message));
/// ShowErrors(new[] { ex.Message });
/// </code>
/// <para>而它接到的中文原文全部来自产出方**真抛**的 <c>InvalidOperationException</c> ——
/// 那个类型**没有码**（<c>SystemFailureCodes</c> 是探针实测过才加的表），
/// ⇒ <c>ExternalText.FailureReason</c> 只能落第 ④ 支回落原文 ⇒ <b>英文界面下这一半照旧中文</b>
/// （中文界面下则是作者口吻的「请安装 CMake 后重试（Linux: sudo apt install cmake）」）。</para>
///
/// <para><b>处置 = 产出方建码 + 站点带对象</b>：<see cref="SimulatorBuildIssueCodes"/>（3 码）
/// ＋ <see cref="SimulatorBuildException"/>（异常携带码 / 实参 / 中文回落），
/// 站点两处露出点共用**同一次** <c>ExternalText.FailureReason(ex)</c>，键统一走
/// <c>sim.build.issue.&lt;码小写&gt;</c>。
/// ⚠ <b>判「裸换算不算收口」的标准是「产出方有没有码」</b>（阶段 87 钉死）：本族的产出方在同一
/// 阶段被换了异常类型 ⇒ 是真迁移，不是**改名式假闭环**（后者用户所见一字不变、只有面绿着变空）。</para>
///
/// <para><b>⚙ 为什么单开一族而不用既有的 <c>sim.issue.</c></b>：那一族（M15，
/// <c>HmiEditor.Simulator.SimulatorIssueCodes</c>）描述的是「模拟器**未连接**」——
/// 通讯失败；本族描述**编译 / 启动**失败。按产出方分家是本仓库既有约定
/// （<c>package.issue.</c> / <c>system.issue.</c> / <c>csv.issue.</c> / <c>recipe.issue.</c> /
/// <c>recipe.import.issue.</c> …）⇒ 一条前缀一张码表的不变量保持住。</para>
///
/// <para><b>⚙ 复用 M16/M18 的四件纯函数、不抄第三份</b>（<c>RecipeCloneIssueLocalizationGuardTests</c>）：
/// <c>RenderRow</c> / <c>RenderViolations</c> / <c>PairingViolations</c> / <c>ClosureViolations</c>。
/// 成对性、三态闭包、渲染对照规则**一字不差**；抄第二份/第三份必然漂移，
/// 而漂移的表现正是「一个守卫绿、另一个红，看不出哪边对」。</para>
///
/// <para><b>⚙ 本类会切换进程级全局语言、还会暂时清掉 <c>PATH</c> ⇒ 必须挂
/// <c>[Collection("Language-global")]</c></b>（<c>DisableParallelization = true</c>，
/// 定义在 <c>LocalizationRefreshUiTests</c>）。两件全局量都**必然 <c>finally</c> 还原**：
/// 语言是 <c>AppLocalization.Service</c> 的静态单例，<c>PATH</c> / <c>CMAKE_EXECUTABLE</c>
/// 是进程环境（不清它们就撞不出「未找到 cmake」那一档 —— 本机与 CI 都装了 cmake，
/// 「靠环境恰好没有 cmake」不是判据）。</para>
///
/// <para><b>诚实边界</b>：</para>
/// <list type="number">
///   <item><see cref="SimulatorBuildIssueCodes.PROCESS_START_FAILED"/> **没有「走真实产出路径」的见证**
///     —— 它要靠 <c>Process.Start</c> **返回 <c>null</c>** 才发生，而该 API 在失败时通常是
///     **抛 <c>Win32Exception</c>**；「返回 null」是文档允许、实测撞不到的分支（要撞它得先造一个
///     能 <c>fork</c> 但 <c>exec</c> 失败的可执行文件，那是**平台相关的概率事件**）
///     ⇒ 进 <see cref="NotProducible"/> 并写明理由，渲染见证由**手工构造**的
///     <see cref="SimulatorBuildException"/> 提供。
///     **那个码必须留着**：界面文案「无法启动模拟器进程」正是用户最容易撞到的一档；
///     而「那个码确实挂在产出方的失败分支上」交给 <see cref="ProducerViolations"/> 钉住
///     —— 否则 <see cref="NotProducible"/> 会把一个**真死码**合法化（登记 #49）。</item>
///   <item>只断「渲染等于该语言的字典模板」，断不了**翻译质量**；</item>
///   <item>本判据**不断**「用户是否真的看见了」 —— 那由 <see cref="ProducerViolations"/> 的
///     **源码面**证据兜着（产出方必须抛带码异常、站点必须走渲染出口、**同源第二露出点**不许
///     再直接取 <c>ex.Message</c>），不是端到端 UI 断言。
///     🩸 最后那一条**非有不可**：<c>ExceptionMessagePassthroughFaceTests</c> 那张面的谓词
///     只认 <c>.Format(键, … .Message)</c> ⇒ <c>ShowErrors(new[] { ex.Message })</c>
///     **原理上不在它的视野里**（面看不见缺口）⇒ 只能在这里钉。</item>
/// </list>
/// </summary>
[Collection("Language-global")]
public class SimulatorBuildIssueLocalizationGuardTests
{
    private readonly ITestOutputHelper _out;

    public SimulatorBuildIssueLocalizationGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>毒化记号：拿它当「中文回落原文」，凡在渲染里冒头就说明字典键没被查到。</summary>
    internal const string Poison = "<<中文回落原文未被使用>>";

    /// <summary>CJK 汉字 —— 用来抓「en-US 的值其实没翻」。</summary>
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>产出方：本刀给它加了码（两条前置校验真抛 + 进程启动真抛）。</summary>
    internal static readonly string[] Producers =
    {
        "src/HmiEditor.App/Services/SimulatorBuildService.cs",
        "src/HmiEditor.App/Views/BuildRunDialog.axaml.cs",
    };

    /// <summary>站点：两处露出点同住这一个文件（<c>Format(Lang.BuildrunLogError, …)</c> 与
    /// 同源的 <c>ShowErrors(new[] { … })</c>）。</summary>
    internal const string Site = "src/HmiEditor.App/Views/BuildRunDialog.axaml.cs";

    /// <summary>
    /// 码表的**第三态** —— <c>声明了</c> ∪ <c>真实产得出</c> 之外的合法去处：**现在产不出、
    /// 但必须留着**。与 M11/M12/M14/M16/M18 的处置同一形状。
    ///
    /// <para>⚠ 这张表是**差异表、不是豁免**：表内条目一旦**真的变得产得出**，判据会红，
    /// 逼维护者删掉这一条（否则它会退化成「能把缺陷合法化」的过期字段 —— 登记 #54 的病）。
    /// 表外多一条、少一条同样红。</para>
    /// </summary>
    internal static readonly Dictionary<string, string> NotProducible = new(StringComparer.Ordinal)
    {
        [SimulatorBuildIssueCodes.PROCESS_START_FAILED] =
            "要靠 Process.Start 返回 null 才发生 —— 该 API 失败时通常抛 Win32Exception，"
            + "「返回 null」是文档允许、实测撞不到的分支（要撞它得先造一个能 fork 但 exec 失败的可执行文件，"
            + "平台相关的概率事件；主判据不许有间发性：一次绿等于零信息）"
            + "⇒ 渲染见证由手工构造的 SimulatorBuildException 提供。"
            + "码必须留着：界面文案「无法启动模拟器进程」正是用户最容易撞到的一档；"
            + "而「它确实挂在产出方的失败分支上」由 ProducerViolations 钉住（登记 #49）。",
    };

    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    // ══════════════════════════ 取数（同源出口 —— 负控与 M19 都复用它） ══════════════════════════

    /// <summary>
    /// 反射枚举 <see cref="SimulatorBuildIssueCodes"/> 里声明的全部码。
    ///
    /// <para><b>是同源出口</b>：本文件的多条判据与 <c>LocalizationKeyReachabilityGuardTests</c>
    /// 的 <b>M19</b> 都调它，不各写一份反射（抄两份必然漂移，漂移的表现是「一个守卫绿、另一个红」）。</para>
    ///
    /// <para>⚠ 这个类型是 <c>internal</c> ⇒ **必须**带 <c>BindingFlags.NonPublic</c>
    /// （少了它反射面为空 ⇒ 三态闭包**恒真**）。下面每条判据都带下界断言兜底。</para>
    /// </summary>
    internal static List<string> DeclaredCodes()
        => typeof(SimulatorBuildIssueCodes)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

    /// <summary>一次取到的失败：人读的标签 + 那个异常 + **它是不是由产出方真的抛出来的**。</summary>
    internal sealed record Hit(string Label, SimulatorBuildException Ex, bool ReachedByProducer);

    /// <summary>一次取数的结果：**命中** + **退化**（手感与 M18 的 <c>Probe</c> 一致）。</summary>
    internal sealed record Probe(List<Hit> Hits, List<string> Degraded);

    /// <summary>「退化」的统一红消息：点名**哪条路**没给出带码的失败档、以及正确动作。</summary>
    internal static string DegradedMessage(IReadOnlyList<string> degraded)
        => "这几条路径本该抛**带码异常**，却没有 ⇒ 产出方退化了（登记 #61c / 阶段 103）。"
           + "正确动作是让产出方在每一处失败 `throw new SimulatorBuildException("
           + "SimulatorBuildIssueCodes.…, \"<中文回落原文>\", <实参…>)`，"
           + "**不是**把它改回裸 `InvalidOperationException(\"<中文>\")`（那样码没地方放、"
           + "站点换出口就是改名式假闭环）：\n  - " + string.Join("\n  - ", degraded);

    /// <summary>
    /// 走一遍**产出方**：两条可确定性复现的真抛路径（真的调 <c>SimulatorBuildService.BuildAsync</c>）
    /// ＋ 一条手工构造的见证（见 <see cref="NotProducible"/>）。
    ///
    /// <para>⚠ 必须真的调产出方 —— 手搓异常的话，「码表与产出方脱节」这件事就永远测不到。</para>
    /// </summary>
    internal static Probe ProbeFailures()
    {
        var hits = new List<Hit>();
        var degraded = new List<string>();

        void Reach(string label, Func<SimulatorBuildException?> run, string expectedCode)
        {
            SimulatorBuildException? caught;
            try
            {
                caught = run();
            }
            catch (Exception ex)
            {
                degraded.Add($"{label}：抛出的是 {ex.GetType().Name} 而不是 SimulatorBuildException "
                    + $"（「{ex.Message}」）⇒ 码没地方放，英文界面下这一半还是中文");
                return;
            }

            if (caught is null)
            {
                degraded.Add($"{label}：**没有抛** ⇒ 这一档不在可达路径上了（前置条件变了 / 探针失效）");
                return;
            }

            if (!string.Equals(caught.Code, expectedCode, StringComparison.Ordinal))
            {
                degraded.Add($"{label}：拿到的是 {caught.Code}，期望 {expectedCode}");
                return;
            }

            hits.Add(new Hit(label, caught, ReachedByProducer: true));
        }

        var root = TestTempDir.NewDir("simbuild-issue-probe");

        // ① LVGL 源码无效：一个**真实存在、但没有 lvgl.h** 的目录（不是编一个不存在的路径 ——
        //    那样测的是「文件系统说没有」，而这里要测的正是产出方那条前置校验）。
        var bogusLvgl = Path.Combine(root, "not-a-lvgl");
        Directory.CreateDirectory(bogusLvgl);
        Reach("BuildAsync(lvglDir 无效)",
            () => TryBuild(Path.Combine(root, "work-a"), bogusLvgl),
            SimulatorBuildIssueCodes.LVGL_SOURCE_INVALID);

        // ② 未找到 cmake：先把 lvglDir 造**有效**（放齐两个文件），再临时清掉 PATH / CMAKE_EXECUTABLE。
        var realLvgl = Path.Combine(root, "lvgl");
        Directory.CreateDirectory(realLvgl);
        File.WriteAllText(Path.Combine(realLvgl, "lvgl.h"), "");
        File.WriteAllText(Path.Combine(realLvgl, "CMakeLists.txt"), "");
        Reach("BuildAsync(找不到 cmake)",
            () => TryBuildWithoutCmake(Path.Combine(root, "work-b"), realLvgl),
            SimulatorBuildIssueCodes.CMAKE_NOT_FOUND);

        // ③ 进程启动失败：概率档（见 NotProducible）⇒ 手工构造，只进渲染面。
        hits.Add(new Hit("SimulatorBuildException(PROCESS_START_FAILED)（手工构造）",
            new SimulatorBuildException(SimulatorBuildIssueCodes.PROCESS_START_FAILED,
                "无法启动模拟器进程（Process.Start 返回 null）"),
            ReachedByProducer: false));

        return new Probe(hits, degraded);
    }

    /// <summary>真的调一次产出方，只接 <see cref="SimulatorBuildException"/>；别的异常照旧往上抛
    /// （那样 <see cref="ProbeFailures"/> 才能把它记成「退化」而不是静默当成功）。</summary>
    private static SimulatorBuildException? TryBuild(string workDir, string lvglDir)
    {
        try
        {
            SimulatorBuildService.BuildAsync(new HmiProject(), workDir, lvglDir).GetAwaiter().GetResult();
            return null;
        }
        catch (SimulatorBuildException ex)
        {
            return ex;
        }
    }

    /// <summary>
    /// 同上，但先把 <c>PATH</c> 与 <c>CMAKE_EXECUTABLE</c> 指到**一个没有 cmake 的空目录**
    /// （否则只要本机装了 cmake，「未找到 cmake」就永远走不到）。
    ///
    /// <para>⚠ 这两件是**进程级全局量** ⇒ 必然 <c>finally</c> 还原；本类靠
    /// <c>[Collection("Language-global")]</c> 与别的测试类串行（见类注释）。</para>
    /// </summary>
    private static SimulatorBuildException? TryBuildWithoutCmake(string workDir, string lvglDir)
    {
        var savedPath = Environment.GetEnvironmentVariable("PATH");
        var savedCmake = Environment.GetEnvironmentVariable("CMAKE_EXECUTABLE");
        var emptyBin = TestTempDir.NewDir("simbuild-no-cmake-bin");

        try
        {
            Environment.SetEnvironmentVariable("PATH", emptyBin);
            Environment.SetEnvironmentVariable("CMAKE_EXECUTABLE", null);
            return TryBuild(workDir, lvglDir);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", savedPath);
            Environment.SetEnvironmentVariable("CMAKE_EXECUTABLE", savedCmake);
        }
    }

    /// <summary>把一条命中的失败换成**毒化回落**的同码异常（保留码 / 实参，只换回落原文）。</summary>
    internal static SimulatorBuildException Poisoned(SimulatorBuildException ex)
        => new(ex.Code, Poison, ex.Args?.ToArray() ?? Array.Empty<object>());

    /// <summary>把**实参**从渲染结果里掩掉 —— 实参是**用户数据**（目录路径），
    /// 拿 <see cref="Cjk"/> 直接扫渲染串会把它误判成「没翻」。</summary>
    internal static string MaskArgs(string rendered, SimulatorBuildException ex)
    {
        var s = rendered;
        if (ex.Args is not { Count: > 0 } args) return s;

        foreach (var arg in args)
        {
            var t = arg as string ?? Convert.ToString(arg, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(t))
                s = s.Replace(t!, "<<ARG>>", StringComparison.Ordinal);
        }

        return s;
    }

    // ══════════════════════════ 判据逻辑（纯函数 —— 负控直接调它们） ══════════════════════════

    /// <summary>
    /// **产出方不许退化**：两个产出方文件里，每一条码**恰好挂在 1 处**失败分支上、
    /// 不许再抛裸 <c>InvalidOperationException</c>；站点必须走渲染出口，且**同源第二露出点**
    /// 不许再直接取 <c>ex.Message</c>。
    ///
    /// <para><b>为什么最后一条非有不可</b>：<c>ExceptionMessagePassthroughFaceTests</c> 的面
    /// 只认 <c>.Format(键, … .Message)</c> —— <c>ShowErrors(new[] { ex.Message })</c>
    /// **原理上不在那张面的视野里**。改动前那个 <c>catch</c> 有两处露出点，只码化
    /// <c>AppendLog</c> 那一处的话，错误摘要面板里读到的仍是中文原文
    /// （🩸「面的边界 ≠ 没有缺口」）。⇒ 这一条只能在这里钉，且必须用**形状签名**
    /// （整段 `<c>ShowErrors(new[] { ex.Message })</c>`），不是「包含 ex.Message」——
    /// 后者会让「留着原来那行、又新写一处渲染」通过。</para>
    ///
    /// <para>剥注释是必须的：讲规则的注释里会写出反例（登记 #48 踩过三次）。
    /// 另外「每条码恰好 1 次」是 <see cref="NotProducible"/> 那张表的**担保方**
    /// （登记 #49：判据的担保方自己不能是死的）—— <c>PROCESS_START_FAILED</c> 之所以可以
    /// 「声明了但产不出」，前提是它真的挂在产出方的失败分支上；否则它就是个真死码，
    /// 而「进表写个理由」会把它合法化。</para>
    /// </summary>
    internal static List<string> ProducerViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();
        var corpus = sources.ToList();

        foreach (var file in Producers)
            if (!corpus.Any(s => string.Equals(s.Relative, file, StringComparison.Ordinal)))
                bad.Add($"{file}：语料里找不到这个文件（扫描面失效？）");
        if (bad.Count > 0) return bad;

        var code = string.Join("\n", Producers
            .Select(f => SourceScanner.StripComments(
                corpus.First(s => string.Equals(s.Relative, f, StringComparison.Ordinal)).Text)));

        foreach (var declared in DeclaredCodes())
        {
            var used = CountOccurrences(code, "SimulatorBuildIssueCodes." + declared);
            if (used != 1)
                bad.Add($"`SimulatorBuildIssueCodes.{declared}` 出现 {used} 次，应为 **1** ⇒ "
                    + (used == 0
                        ? "那条失败又退回了**没有码**的形状（用户读到的仍是中文原文）"
                        : "同一个码挂了两处失败分支 ⇒ 「一条码一个语义」不成立"
                          + "（要分家就新开一条码，别复用）"));
        }

        if (code.Contains("throw new InvalidOperationException", StringComparison.Ordinal))
            bad.Add("产出方还在抛裸 `InvalidOperationException` ⇒ 码没地方放"
                + "（改动前那三条中文抛点正是这个形状）。正确写法："
                + "`throw new SimulatorBuildException(SimulatorBuildIssueCodes.…, \"<中文回落原文>\", <实参…>)`");

        if (!code.Contains("ExternalText.FailureReason(ex)", StringComparison.Ordinal))
            bad.Add($"{Site}：没有再走 `ExternalText.FailureReason(ex)` ⇒ 码化出口被绕过，"
                + "用户读到的还是产出方的中文回落原文（英文界面下中英混排）");

        if (code.Contains("ShowErrors(new[] { ex.Message })", StringComparison.Ordinal))
            bad.Add($"{Site}：`ShowErrors` 又直接取 `ex.Message` ⇒ **同源第二露出点**没跟着码化"
                + "（🩸 它不在「异常原文直通」那张面上 —— 那张面的谓词只认 `.Format(` ⇒ 面看不见它，"
                + "所以这件事只能在这里钉住）。正确写法：与 `AppendLog` 共用**同一次** "
                + "`var reason = ExternalText.FailureReason(ex);`");

        return bad;
    }

    /// <summary>数子串出现次数（本文件私有的小工具 —— 与 M18 的那一份不共享：
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

    // ══════════════════════════ 判据 ══════════════════════════

    /// <summary>
    /// 主判据：**每一条真实失败**在**两种语言下各渲染一遍**，结果都必须来自该语言的字典模板。
    ///
    /// <para>毒化那一路：把回落原文换成 <see cref="Poison"/> 再渲染 —— 回落原文**没进字典**
    /// 才是「字典在决定文案」的证明。⚠ 不能拿真原文比对：本族 zh 字典值与回落原文
    /// 措辞**刻意不同**（字典 = 面向用户；回落 = 作者口吻 + 排障细节），
    /// 拿真原文只能测「今天恰好翻了没」，测不到「渲染这一半确实由字典决定」。</para>
    /// </summary>
    [Fact]
    public void EveryCode_RendersTheDictionaryTemplate_InBothLanguages()
    {
        var probe = ProbeFailures();
        Assert.True(probe.Degraded.Count == 0, DegradedMessage(probe.Degraded));
        var hits = probe.Hits;
        Assert.True(hits.Count >= 3,
            $"只取到 {hits.Count} 条失败（应有 3：2 条真实产出路径 ＋ 1 条手工构造）"
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
                    var key = ExternalText.SimulatorBuildIssueKey(hit.Ex.Code);
                    rows.Add(new RecipeCloneIssueLocalizationGuardTests.RenderRow(
                        hit.Label, lang,
                        ExternalText.FailureReason(Poisoned(hit.Ex)),
                        key,
                        RecipeCloneIssueLocalizationGuardTests.Expected(dict, key, hit.Ex.Args),
                        hit.Ex.Message));
                }
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        JudgeReadings.Write(_out, "模拟器编译 / 启动失败（#61c · sim.build.issue.*）",
            rows.Select(r => $"    {r.Label,-44} {r.Language} ⇒ {r.Rendered}").ToList());

        // 自检：语言真的切动了。静默没切会让 en 那一遍渲染成中文 —— 那时下面那条期望比对
        // 已经会红，但红在「渲染 ≠ 期望」上，指错方向。
        foreach (var hit in hits)
        {
            var key = ExternalText.SimulatorBuildIssueKey(hit.Ex.Code);
            var zhExpected = RecipeCloneIssueLocalizationGuardTests.Expected(Zh, key, hit.Ex.Args);
            var enExpected = RecipeCloneIssueLocalizationGuardTests.Expected(En, key, hit.Ex.Args);
            var zhRow = rows.Single(r => r.Label == hit.Label && r.Language == "zh-CN");
            var enRow = rows.Single(r => r.Label == hit.Label && r.Language == "en-US");

            var expectedDiffers = zhExpected is not null && enExpected is not null
                && !string.Equals(zhExpected, enExpected, StringComparison.Ordinal);
            var renderedDiffers = !string.Equals(zhRow.Rendered, enRow.Rendered, StringComparison.Ordinal);

            Assert.True(renderedDiffers || !expectedDiffers,
                $"{hit.Label}：zh 与 en 渲染一模一样「{zhRow.Rendered}」，可两本字典的值并不相同"
                + " ⇒ 语言切换没生效（渲染没跟着当前语言走）");
        }

        var problems = RecipeCloneIssueLocalizationGuardTests.RenderViolations(rows, Poison);
        Assert.True(problems.Count == 0,
            "有失败没走字典（登记 #61c / 阶段 103）。正确动作是补 "
            + "`sim.build.issue.<码小写>` 的字典键，**不是**把产出方的裸中文串再拼回去"
            + "（那会让英文界面下中英混排）：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **收益的正面见证**：真失败（不毒化）在**英文界面下不许再出现中文**。
    ///
    /// <para>与主判据的见证面不重合：主判据证明「渲染由字典决定」，这条证明「英文用户读到的
    /// 东西真的不含中文」—— 后者正是 #61 那一族缺陷的用户可见症状。
    /// 实参要先 <see cref="MaskArgs"/> 掩掉（它是**用户数据**：目录路径，本来就可能是中文）；
    /// 第二半扫 en 的**模板本体**（不受实参污染的那把尺子）。两个面少任何一个都会漏掉一类。</para>
    /// </summary>
    [Fact]
    public void RealFailures_InEnglish_CarryNoChinese()
    {
        var probe = ProbeFailures();
        Assert.True(probe.Degraded.Count == 0, DegradedMessage(probe.Degraded));

        var original = AppLocalization.Service.CurrentLanguage;
        var rows = new List<(string Label, string Masked, string Raw, string Fallback)>();
        try
        {
            AppLocalization.Service.SwitchLanguage("en-US");
            foreach (var hit in probe.Hits)
            {
                var rendered = ExternalText.FailureReason(hit.Ex);
                rows.Add((hit.Label, MaskArgs(rendered, hit.Ex), rendered, hit.Ex.Message));
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        JudgeReadings.Write(_out, "模拟器编译 / 启动失败 · 英文界面下的渲染（#61c）",
            rows.Select(r => $"    {r.Label,-44} en ⇒ {r.Raw}").ToList());

        var bad = rows.Where(r => Cjk.IsMatch(r.Masked))
            .Select(r => $"    {r.Label}：英文界面下渲染出中文「{r.Raw}」（回落原文是「{r.Fallback}」）"
                + " ⇒ 该语言的字典键没查到")
            .ToList();

        foreach (var hit in probe.Hits)
        {
            var key = ExternalText.SimulatorBuildIssueKey(hit.Ex.Code);
            if (En.TryGetValue(key, out var template) && Cjk.IsMatch(template))
                bad.Add($"    {key}：en-US 的**模板本体**含汉字「{template}」 ⇒ 这一条没翻（或直接抄了 zh）");
        }

        Assert.True(bad.Count == 0,
            "英文界面下这些失败仍是中文（登记 #61c / 阶段 103）—— 用户可见的收益就落在这条上：\n"
            + string.Join("\n", bad));
    }

    /// <summary>两本字典齐 + 成对 + **占位符个数与实参个数一一对应**（规则复用 M16/M18 的纯函数）。</summary>
    [Fact]
    public void BothDictionaries_CarryEveryCode_AndAreActuallyPaired()
    {
        var probe = ProbeFailures();
        Assert.True(probe.Degraded.Count == 0, DegradedMessage(probe.Degraded));

        var rows = probe.Hits
            .Select(h => (Key: ExternalText.SimulatorBuildIssueKey(h.Ex.Code), ArgCount: h.Ex.Args?.Count ?? 0))
            .ToList();
        var distinct = rows.Select(r => r.Key).Distinct(StringComparer.Ordinal).ToList();

        Assert.True(distinct.Count >= 3,
            $"只取到 {distinct.Count} 个键（应有 3：lvgl_source_invalid / cmake_not_found / "
            + "process_start_failed）—— 取数失效，下面的判定会恒真");

        var problems = RecipeCloneIssueLocalizationGuardTests.PairingViolations(rows, Zh, En);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>码表**三态闭包**：声明的码要么真的产得出，要么在 <see cref="NotProducible"/> 里写了理由。</summary>
    [Fact]
    public void DeclaredCodes_AreEitherProducedOrExplicitlyNotProducible()
    {
        var declared = DeclaredCodes();
        Assert.True(declared.Count >= 3,
            $"只反射到 {declared.Count} 个码 —— 取数失效（反射面空了，三态闭包会恒真）");
        Assert.True(NotProducible.Count >= 1,
            "NotProducible 表空了 —— 要么 PROCESS_START_FAILED 真的变得产得出（那就该把它从表里删掉、"
            + "并补一条真实见证），要么反射面失效（那个码没被反射到）");

        var probe = ProbeFailures();
        Assert.True(probe.Degraded.Count == 0, DegradedMessage(probe.Degraded));

        // ⚠ 闭包只看**产出方真的发得出来**的那些（见 Hit.ReachedByProducer）——
        // 手工构造的那一档只进渲染面。混在一起会让 NotProducible 那条判据自己把自己打红。
        var produced = probe.Hits
            .Where(h => h.ReachedByProducer)
            .Select(h => h.Ex.Code)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(produced.Count >= 2,
            $"走真实产出路径拿到的失败只有 {produced.Count} 条（应有 2）"
            + " —— 取数退化成手搓，三态闭包会恒真");

        JudgeReadings.Write(_out, "模拟器编译 / 启动失败的码表（#61c）",
            new List<string>
            {
                $"    声明 {declared.Count} 个 / 真实产出 {produced.Count} 个 / 明确产不出 {NotProducible.Count} 个："
                + $"产出 = {string.Join(", ", produced.OrderBy(x => x, StringComparer.Ordinal))}；"
                + $"产不出 = {string.Join(", ", NotProducible.Keys.OrderBy(x => x, StringComparer.Ordinal))}",
                $"    渲染见证 {probe.Hits.Count} 条（其中手工构造 {probe.Hits.Count(h => !h.ReachedByProducer)} 条）",
            });

        var problems = RecipeCloneIssueLocalizationGuardTests.ClosureViolations(declared, produced, NotProducible);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>产出方必须抛带码异常；站点必须走渲染出口；**同源第二露出点**也要一起码化。</summary>
    [Fact]
    public void Producer_ThrowsCodedExceptions_AndBothExposurePointsGoThroughTheOutlet()
    {
        var problems = ProducerViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "模拟器编译 / 启动失败产出方与站点的形状（#61c）",
            Producers.Concat(new[] { Site }).Distinct(StringComparer.Ordinal)
                .Select(f => $"    {f}").ToList());

        Assert.True(problems.Count == 0,
            "产出方必须给码 —— 否则站点换成码化出口会**收益为零**"
            + "（面绿着、用户看到的一字不变）：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **诚实边界正面断言**：一条**没有码**的 <see cref="SimulatorBuildException"/>
    /// （字典里没有它的键）照旧回落原文 ⇒ 「码才是收益的唯一来源」，与主判据成对。
    /// </summary>
    [Fact]
    public void FailureWithoutAKey_StillFallsBackToTheRawMessage()
    {
        var bare = new SimulatorBuildException("NOT_A_REAL_CODE", "库里没有这个码的回落原文");
        var rendered = ExternalText.FailureReason(bare);

        Assert.DoesNotContain("NOT_A_REAL_CODE", rendered, StringComparison.Ordinal);
        Assert.Contains(bare.Message, rendered, StringComparison.Ordinal);

        // 有码的那一档**必须**不是回落原文（否则「码化」这件事根本没有发生）
        var real = ProbeFailures();
        Assert.True(real.Degraded.Count == 0, DegradedMessage(real.Degraded));
        Assert.All(real.Hits, h => Assert.NotEqual(h.Ex.Message, ExternalText.FailureReason(h.Ex)));
    }

    /// <summary>
    /// 负控 —— 每组都要求「该红的红、该绿的绿」（本项目对负控的一贯要求）。
    /// 注入全部走**纯函数**（合成语料 / 内存副本 / 手工构造），不动真仓库。
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        var zh = Zh;
        var en = En;
        var probe = ProbeFailures();
        Assert.True(probe.Degraded.Count == 0, DegradedMessage(probe.Degraded));
        var hits = probe.Hits;

        var keys = hits
            .Select(h => (Key: ExternalText.SimulatorBuildIssueKey(h.Ex.Code), ArgCount: h.Ex.Args?.Count ?? 0))
            .ToList();
        var key0 = keys[0].Key;

        // ── NC0 · 正控（**必须全合成**：外部负控会让它跟着红）──
        Assert.Empty(RecipeCloneIssueLocalizationGuardTests.PairingViolations(keys, zh, en));

        var clean = hits.Select(h =>
        {
            var key = ExternalText.SimulatorBuildIssueKey(h.Ex.Code);
            return new RecipeCloneIssueLocalizationGuardTests.RenderRow(
                h.Label, "zh-CN",
                RecipeCloneIssueLocalizationGuardTests.Expected(zh, key, h.Ex.Args)!, key,
                RecipeCloneIssueLocalizationGuardTests.Expected(zh, key, h.Ex.Args), h.Ex.Message);
        }).ToList();
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

        // 占位符个数：本族里 lvgl_source_invalid 是**1** 实参、另两条是 0
        // ⇒ 往那个 1 实参的键上再塞一个 {1} 即个数不符。
        // ⚠ 不能挑 0 实参的键去塞 —— 本族里它照样会被抓到，但那测的是另一档；
        //   也不能挑「本来就 2 占位符」的键（本族没有，M18 的注释记着这个坑）。
        var oneArgKey = ExternalText.SimulatorBuildIssueKey(
            hits.First(h => (h.Ex.Args?.Count ?? 0) == 1).Ex.Code);
        var zhCopy = new Dictionary<string, string>(zh, StringComparer.Ordinal)
        {
            [oneArgKey] = zh[oneArgKey] + " {1}",
        };
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

        // ── NC3 · 三态闭包（四档都要报）──
        var declared = DeclaredCodes();
        // ⚠ 正控必须喂**真实产出那一档的全部 2 个码**：只喂一个会让另一个被判「死码」，
        //    正控自己先红（红在「死码」上、指错方向）。这里是**全合成**的输入（取自读数）。
        var produced = hits.Where(h => h.ReachedByProducer)
            .Select(h => h.Ex.Code).Distinct(StringComparer.Ordinal).ToArray();
        Assert.True(produced.Length == 2, $"真实产出那一档应恰好 2 个码，实测 {produced.Length}");
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
                declared, produced.Append(SimulatorBuildIssueCodes.PROCESS_START_FAILED), NotProducible),
            v => v.Contains("过期", StringComparison.Ordinal));

        var drifted = new Dictionary<string, string>(NotProducible, StringComparer.Ordinal)
        {
            ["NOT_A_REAL_CODE"] = "演示",
        };
        Assert.Contains(
            RecipeCloneIssueLocalizationGuardTests.ClosureViolations(declared, produced, drifted),
            v => v.Contains("抄写漂移", StringComparison.Ordinal));

        // ── NC4 · 产出方与站点（合成语料，**两个目标文件都必须在**、且内容各按各的）──
        // ⚠ 两个文件**不能喂同一段 body**：那样每条码会在两份语料里各出现一次 ⇒
        //    「恰好 1 次」那条判据把**正控自己**打红（本判据第一版就是这么红的，
        //    红在「出现 2 次」上、指的方向还是错的）。真实形状正是「码在产出方、
        //    渲出口在站点」—— 第三条码（进程启动）本来就住在站点那个文件里。
        const string producerBody = """
            var lvgl = Path.Combine(lvglDir, "lvgl.h");
            if (!File.Exists(lvgl)) throw new SimulatorBuildException(SimulatorBuildIssueCodes.LVGL_SOURCE_INVALID, "中文回落", lvglDir);
            if (string.IsNullOrEmpty(FindCmake())) throw new SimulatorBuildException(SimulatorBuildIssueCodes.CMAKE_NOT_FOUND, "中文回落");
            """;

        const string siteBody = """
            catch (Exception ex)
            {
                var reason = ExternalText.FailureReason(ex);
                AppendLog(AppLocalization.Service.Format(Lang.BuildrunLogError, reason));
                ShowErrors(new[] { reason });
            }

            void Start(string exe)
            {
                if (p == null) throw new SimulatorBuildException(SimulatorBuildIssueCodes.PROCESS_START_FAILED, "中文回落");
            }
            """;

        static (string Relative, string Text)[] Corpus(string producer, string site) => new[]
        {
            (Producers[0], producer),
            (Producers[1], site),
        };

        Assert.Empty(ProducerViolations(Corpus(producerBody, siteBody)));

        // ① 某条码没有挂上（又退回没码的形状）⇒ 点名「出现 0 次」；改挂到另一条码上 ⇒ 那条变「2 次」
        var missing = ProducerViolations(Corpus(producerBody.Replace(
            "SimulatorBuildIssueCodes.CMAKE_NOT_FOUND", "SimulatorBuildIssueCodes.LVGL_SOURCE_INVALID",
            StringComparison.Ordinal), siteBody));
        Assert.Contains(missing, v => v.Contains("SimulatorBuildIssueCodes.CMAKE_NOT_FOUND",
            StringComparison.Ordinal));
        Assert.Contains(missing, v => v.Contains("出现 0 次", StringComparison.Ordinal));
        Assert.Contains(missing, v => v.Contains("出现 2 次", StringComparison.Ordinal));

        // ② 又抛裸 InvalidOperationException ⇒ 形状签名点名
        var bare = ProducerViolations(Corpus(producerBody
            + "\nvoid N() { throw new InvalidOperationException(\"又变回没码的形状\"); }\n", siteBody));
        Assert.Contains(bare, v => v.Contains("裸 `InvalidOperationException`", StringComparison.Ordinal));

        // ③ 同源第二露出点又直接取 ex.Message ⇒ 必须点名
        //    （「异常原文直通」那张面对它**无话可说** —— 谓词只认 `.Format(`）
        var second = ProducerViolations(Corpus(producerBody, siteBody.Replace(
            "ShowErrors(new[] { reason });", "ShowErrors(new[] { ex.Message });", StringComparison.Ordinal)));
        Assert.Contains(second, v => v.Contains("第二露出点", StringComparison.Ordinal));

        // ④ 站点不走渲染出口 ⇒ 点名
        var noOutlet = ProducerViolations(Corpus(producerBody, siteBody.Replace(
            "var reason = ExternalText.FailureReason(ex);", "var reason = ex.Message;", StringComparison.Ordinal)));
        Assert.Contains(noOutlet, v => v.Contains("ExternalText.FailureReason(ex)", StringComparison.Ordinal));

        // ⑤ 语料里缺产出方文件 ⇒ 必须报「找不到」（扫描面失效时会静默全绿）
        Assert.Contains(ProducerViolations(Array.Empty<(string, string)>()),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ⑥ 注释里写的反例不算（规则不许被自己的文档满足，登记 #48）
        Assert.Empty(ProducerViolations(Corpus(producerBody
            + "\n// 反例（不许这么写）：throw new InvalidOperationException(\"中文\"); "
            + "ShowErrors(new[] { ex.Message });", siteBody)));

        // ── NC5 · 真语料对照（合成语料绿 ≠ 真仓库绿）──
        Assert.Empty(ProducerViolations(AppSourceScanner.ScanAllProductSources()));
    }
}
