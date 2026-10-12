using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.Simulator;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「**用户管理 / 模拟器的失败原因必须由字典决定**」的守卫 —— 登记表 §4 <b>#61d</b> 的第二刀（阶段 97）。
///
/// <para><b>它守的是哪一类缺陷</b>：#61d 那 7 个键原先一律被当成"异常原文直通"，
/// 阶段 97 逐站读了产出方之后，实测分成三档（见 <c>docs/2026-09-23-exception-passthrough-triage.md</c> §97）：</para>
/// <list type="number">
///   <item><b>本刀处理的一档（真异常路径、产出方在本仓库）</b> ——
///     <c>AuthenticationService.CreateUser</c> / <c>SetUserPassword</c> 抛**自建中文**
///     <c>ArgumentException</c>（角色未注册 / 密码长度不足）⇒ 中文界面下是**作者口吻**
///     （「请先 RegisterRole 或修正 SecurityConfig.DefaultRoleId」）、英文界面下照旧中文；
///     <c>CommunicationSimulator.Read/WriteRegister</c> 抛**英文硬编码**
///     <c>InvalidOperationException("Simulator not connected")</c> ⇒ 中文界面下混英文。
///     两者都换成**带码异常**（<see cref="UserAdminException"/> / <see cref="SimulatorNotConnectedException"/>）。</item>
///   <item><b>返回值无原因档</b>（<c>login.failed</c> / <c>recipe.clone.failed</c>）——
///     真失败走 <c>return null</c>（甚至 <c>return null!</c>），站点根本看不到原因
///     ⇒ 要的是"给失败档建码"，**不是**翻译 <c>ex.Message</c>；</item>
///   <item><b>死 catch 档</b>（<c>status.reload.failed</c>）—— <c>catch</c> 包的是
///     <c>Dispatcher.UIThread.Post</c>（**入队不抛**）⇒ 那条 catch 永不触发，
///     而真正的重建异常在回调里**无人接**。</item>
/// </list>
/// <para>②③ 两档本刀**不处理**，只登记（照实写在 §97.9）—— 别把本判据读成"#61d 已经清完"。</para>
///
/// <para><b>⚠ 与「直通面守卫」是一对，缺一不可</b>（阶段 87 立的规矩，本刀照用）：
/// <c>ExceptionMessagePassthroughFaceTests</c> 只数"还有多少站点把 <c>ex.Message</c> 直通模板" ——
/// 站点不再那样写就从面上消失，而"换成什么"它不管。本刀把那 3 个站点换成
/// <c>ExternalText.FailureReason(ex)</c> 是**真收口**，只因为**同刀给产出方加了码**；
/// 产出方没给码时换它就是阶段 83 钉死的**改名式假闭环**。</para>
///
/// <para><b>⚠ 主判据为什么是「切两种语言各渲染一遍、逐字比对」，而不是同族惯用的毒化探针</b>
/// （本族与 <c>SvgParseIssue</c> / <c>CsvImportIssue</c> / <c>ExpressionSyntaxIssue</c> 三族的分叉点，改动前先读这段）：</para>
/// <list type="number">
///   <item><c>SimulatorNotConnectedException</c> 的中文回落是**类内 <c>private const</c>**（刻意不公开，
///     见该类型文档），类型又是 <c>sealed</c> ⇒ 毒化探针**构造不出来**（不做"为测开放生产 API"这种事）；</item>
///   <item>更要紧的是：三档里有**两档**（<c>user.issue.password_too_short</c> /
///     <c>sim.issue.not_connected</c>）的 **zh 字典值刻意与回落原文一字不差** ——
///     即"用户所见零变化"是**设计目标**（见 <c>PasswordFallbackMatchesZh_…</c> 那条判据）。
///     ⇒ 在中文那一遍里，`渲染 == 回落` 与 `渲染 == 字典值` 是同一件事，
///     毒化探针在这里**证明不了任何东西**。</item>
/// </list>
/// <para>换成的做法是：**每种语言各渲染一遍，断言渲染逐字等于「该语言字典模板填上真实参」**。
/// 它同时钉住三件事 —— ① 该语言下键存在（缺键 ⇒ 渲染是中文回落 ⇒ 与 en 期望不符）；
/// ② 实参真的被填进去了（<c>Format</c> 在占位符与实参个数不符时**不抛异常**）；
/// ③ 码 → 键的映射没错位。其中 **en 那一遍是关键的见证**：两本字典的 en 值不含汉字
/// （由 <see cref="PairingViolations"/> 双向钉住），而回落原文一定是中文 ⇒
/// **en 渲染等于 en 字典值这件事，本身就排除了"回落"这条路**。
/// 中文那一遍对"字典值 == 回落"的两档**不具备见证力**，这是本判据的诚实边界，写在 <c>§97.9</c>。</para>
///
/// <para><b>⚙ 本类会切换进程级全局语言 ⇒ 必须挂 <c>[Collection("Language-global")]</c></b>
/// （<c>DisableParallelization = true</c>）—— 否则与大队并行时会打红**别的**测试类，
/// 而症状随字典内容变、极难归因。判据是
/// <c>LocalizationRefreshUiTests.EveryTestClassThatSwitchesLanguage…</c>；切换必然 <c>finally</c> 还原。</para>
///
/// <para><b>诚实边界</b>：① 只断「渲染等于该语言的字典模板」，断不了**翻译质量**；
/// ② 两档 <c>ArgumentException.ThrowIfNullOrWhiteSpace</c>（用户名/密码空白）**刻意保持不动** ——
/// 那是**调用方编程错误**，与 <c>SystemFailureCodes</c> 刻意不码化 <c>ArgumentException</c>
/// 是同一条边界（在那里码化会连带吞掉 <c>ArgumentNullException</c> 家族）；
/// ③ 「密码长度按字符数还是按字节数」是另一个议题（#20b），本判据不管。</para>
/// </summary>
[Collection("Language-global")]
public class UserAdminIssueLocalizationGuardTests
{
    private readonly ITestOutputHelper _out;

    public UserAdminIssueLocalizationGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>模板占位符索引（<c>{0}</c> / <c>{1}</c>）。与实参个数的对应是硬判据 ——
    /// <c>LocalizationService.Format</c> 在个数不符时**不抛异常**，用户会看到字面的 <c>{0}</c>。</summary>
    private static readonly Regex PlaceholderIndex = new(@"\{(\d+)\}", RegexOptions.Compiled);

    /// <summary>CJK 汉字 —— 用来抓"en-US 的值其实没翻"。</summary>
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>产出方：本刀换掉裸异常的**两个**文件（都在 <c>src/</c> 下、路径相对仓库根）。</summary>
    private static readonly string[] Producers =
    {
        "src/HmiEditor.Core/Services/AuthenticationService.cs",
        "src/HmiEditor.Simulator/CommunicationSimulator.cs",
    };

    /// <summary>站点：本刀换渲染出口的**两个**视图（相对仓库根）。</summary>
    private static readonly string[] Sites =
    {
        "src/HmiEditor.App/Views/UserManagerView.axaml.cs",
        "src/HmiEditor.App/Views/SimulatorView.axaml.cs",
    };

    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    // ══════════════════════════ 取数（同源出口 —— 负控与 M15 都复用它） ══════════════════════════

    /// <summary>
    /// 反射枚举 <see cref="UserAdminIssueCodes"/> 里声明的全部码。
    ///
    /// <para><b>是同源出口</b>：本文件的多条判据与 <c>LocalizationKeyReachabilityGuardTests</c>
    /// 的 <b>M15</b> 都调它，不各写一份反射（抄两份必然漂移，漂移的表现是"一个守卫绿、另一个红"）。</para>
    ///
    /// <para>⚠ "反射到 0 个码"会让双向闭包**恒真** ⇒ 下面每条判据都带下界断言兜底。</para>
    /// </summary>
    internal static List<string> DeclaredCodes()
        => typeof(UserAdminIssueCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// 反射枚举 <see cref="SimulatorIssueCodes"/> 里声明的全部码。
    ///
    /// <para>⚠ 码与中文回落**刻意分居两个类**：回落住 <c>SimulatorNotConnectedException</c>
    /// 内部（<c>private const</c>）。若它也是 <c>public const string</c> 且同住一族，
    /// 反射会把"回落原文"也当成一条码 ⇒ 键不存在 ⇒ 机制面虚高。</para>
    /// </summary>
    internal static List<string> DeclaredSimulatorCodes()
        => typeof(SimulatorIssueCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    /// <summary>一次真实失败：人读的标签 + **真实产出路径拿到的异常** + 它应当命中的字典键
    /// + 实参 + 该异常的**中文回落原文**（= <c>Exception.Message</c>）。</summary>
    internal sealed record Hit(
        string Label, Exception Failure, string Key, IReadOnlyList<object>? Args, string Fallback);

    /// <summary>一次取数的结果：**命中** + **逃逸**。</summary>
    /// <param name="Hits">走目标类型分支拿到的失败（4 条路径应当全在）。</param>
    /// <param name="Escaped">本该抛**带码异常**、却抛了**别的类型** —— 产出方退化的直接证据。</param>
    internal sealed record Probe(List<Hit> Hits, List<string> Escaped);

    /// <summary>
    /// 「逃逸」的统一红消息：点名**哪条路**抛了**什么类型**、以及正确动作。
    ///
    /// <para><b>为什么要有这一格</b>（NC1 实测出来的）：产出方退回裸异常时，裸异常会从取数函数里
    /// 穿出去，把四条判据一起打成同一个栈 —— 那时**看不出是哪条路退化**，也看不出该修产出方还是修站点。
    /// 记成数据再判，红才自带信息（本项目对判据的一贯要求）。</para>
    /// </summary>
    internal static string EscapeMessage(IReadOnlyList<string> escaped)
        => "这几条路径本该抛**带码异常**，却抛了别的类型 ⇒ 产出方退化了（登记 #61d / 阶段 97）。"
           + "正确动作是给**产出方**加码（`UserAdminException` / `SimulatorNotConnectedException`），"
           + "**不是**把站点改回 `ex.Message`：\n  - " + string.Join("\n  - ", escaped);

    /// <summary>
    /// 4 条路径（3 个码：用户管理 2 + 模拟器 1）各自走**真实产出路径** ——
    /// 不手搓异常，否则"码表与产出方脱节"这件事永远测不到。
    ///
    /// <para>⚠ 每处都先 catch **目标类型**、再 catch 其余：产出方若退回裸异常，那条路径既不产出命中、
    /// 也不会让裸异常从这里穿出去 —— 而是落进 <see cref="Probe.Escaped"/>，由 <see cref="EscapeMessage"/> 点名
    /// （裸异常穿出去会把四条判据打成同一个栈，看不出哪条路退化）。
    /// 由 <see cref="ClosureViolations"/> 报"声明了但真实产不出"（不是静默少一条）。</para>
    /// </summary>
    internal static Probe ProbeFailures()
    {
        var hits = new List<Hit>();
        var escaped = new List<string>();

        // ① 角色未注册：工程配置写错（DefaultRoleId 指向不存在的角色）
        using (var auth = new AuthenticationService())
        {
            auth.ApplySecurityConfig(new SecurityConfig
            {
                DefaultRoleId = "role-does-not-exist",
                PasswordMinLength = 4,
            });
            try
            {
                auth.CreateUser("probe", "Probe", "pass1234");
            }
            catch (UserAdminException ex)
            {
                hits.Add(new Hit("CreateUser(DefaultRoleId 未注册)", ex,
                    ExternalText.UserIssueKey(ex.Issue.Code), ex.Issue.Args, ex.Message));
            }
            catch (Exception ex)
            {
                escaped.Add($"CreateUser(DefaultRoleId 未注册)：抛了 {ex.GetType().Name}（应为 UserAdminException）");
            }
        }

        // ② 密码长度不足：走 SetUserPassword（两个站点共用的那个产出方）
        using (var auth = new AuthenticationService())
        {
            auth.ApplySecurityConfig(new SecurityConfig
            {
                DefaultRoleId = "role-operator",
                PasswordMinLength = 8,
            });
            var user = auth.CreateUser("probe2", "Probe 2", "pass1234");
            try
            {
                auth.SetUserPassword(user, "short");
            }
            catch (UserAdminException ex)
            {
                hits.Add(new Hit("SetUserPassword(密码过短)", ex,
                    ExternalText.UserIssueKey(ex.Issue.Code), ex.Issue.Args, ex.Message));
            }
            catch (Exception ex)
            {
                escaped.Add($"SetUserPassword(密码过短)：抛了 {ex.GetType().Name}（应为 UserAdminException）");
            }
        }

        // ③④ 模拟器未连接：两个抛点是同一档码（读 / 写），各自走一遍
        var sim = new CommunicationSimulator { IsConnected = false };

        try
        {
            sim.ReadRegister(0);
        }
        catch (SimulatorNotConnectedException ex)
        {
            hits.Add(new Hit("CommunicationSimulator.ReadRegister(未连接)", ex,
                ExternalText.SimulatorIssueKey(ex.Code), null, ex.Message));
        }
        catch (Exception ex)
        {
            escaped.Add($"CommunicationSimulator.ReadRegister(未连接)：抛了 {ex.GetType().Name}（应为 SimulatorNotConnectedException）");
        }

        try
        {
            sim.WriteRegister(0, 1.0);
        }
        catch (SimulatorNotConnectedException ex)
        {
            hits.Add(new Hit("CommunicationSimulator.WriteRegister(未连接)", ex,
                ExternalText.SimulatorIssueKey(ex.Code), null, ex.Message));
        }
        catch (Exception ex)
        {
            escaped.Add($"CommunicationSimulator.WriteRegister(未连接)：抛了 {ex.GetType().Name}（应为 SimulatorNotConnectedException）");
        }

        return new Probe(hits, escaped);
    }

    /// <summary>一次渲染对照：标签 + 语言 + 实渲染 + 该语言字典**填参后**的期望串 + 该语言下的回落原文。</summary>
    internal sealed record RenderRow(
        string Label, string Language, string Rendered, string Key, string? Expected, string Fallback);

    /// <summary>
    /// 某本字典里这个键**填上实参**之后的串；键不在字典里返回 <c>null</c>。
    ///
    /// <para>与 <c>PackageIssueLocalizationGuardTests</c> / <c>ExpressionSyntaxIssueLocalizationGuardTests</c>
    /// 的 <c>Expected</c> 同形（那两族的键同样带实参）—— 本仓库既定做法，不另发明一套。</para>
    /// </summary>
    internal static string? Expected(Dictionary<string, string> dict, string key, IReadOnlyList<object>? args)
    {
        if (!dict.TryGetValue(key, out var body)) return null;
        return args is { Count: > 0 } a
            ? string.Format(CultureInfo.InvariantCulture, body, a.ToArray())
            : body;
    }

    // ══════════════════════════ 判据逻辑（纯函数 —— 负控直接调它们） ══════════════════════════

    /// <summary>
    /// 渲染必须**逐字等于「该语言字典模板填上真实参」**；键不在该语言字典里 ⇒ 点名。
    ///
    /// <para><b>为什么不比"等于回落原文"</b>：三档里有两档的 zh 字典值**刻意等于**回落原文
    /// （零变化是设计目标）⇒ 那个比法在中文那一遍恒真、看不出任何东西。
    /// 比"等于该语言期望串"则两边都有见证力：中文那遍见证实参填充与映射，
    /// 英文那遍连"键存不存在"一起见证（见类文档）。</para>
    /// </summary>
    internal static List<string> RenderViolations(IEnumerable<RenderRow> rows)
    {
        var bad = new List<string>();

        foreach (var r in rows)
        {
            if (r.Expected is null)
            {
                bad.Add($"{r.Label} @{r.Language}：字典里没有 {r.Key} ⇒ 该语言下用户读到的是中文回落原文"
                    + $"「{r.Fallback}」");
                continue;
            }

            if (!string.Equals(r.Rendered, r.Expected, StringComparison.Ordinal))
                bad.Add($"{r.Label} @{r.Language}：渲染「{r.Rendered}」≠ 该语言字典填参后的"
                    + $"「{r.Expected}」⇒ 有别的来源（漏填实参 / 码映射错位 / 渲染没走本键）");
        }

        return bad;
    }

    /// <summary>两本字典对每条码的完备性、成对性，以及**占位符个数与实参个数的对应**。</summary>
    internal static List<string> PairingViolations(
        IEnumerable<(string Key, int ArgCount)> rows, Dictionary<string, string> zh, Dictionary<string, string> en)
    {
        var bad = new List<string>();

        foreach (var (key, argCount) in rows)
        {
            if (!zh.TryGetValue(key, out var z)) { bad.Add($"{key}：zh-CN 缺键"); continue; }
            if (!en.TryGetValue(key, out var e)) { bad.Add($"{key}：en-US 缺键"); continue; }

            if (Cjk.IsMatch(e))
                bad.Add($"{key}：en-US 的值里有中文字符 ⇒ 没翻，或直接抄了 zh");

            if (string.Equals(z, e, StringComparison.Ordinal))
                bad.Add($"{key}：两本字典的值一字不差 ⇒ 至少有一本没翻");

            foreach (var (lang, value) in new[] { ("zh-CN", z), ("en-US", e) })
            {
                var count = PlaceholderIndex.Matches(value)
                    .Select(m => m.Groups[1].Value)
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                if (count != argCount)
                    bad.Add($"{key}：{lang} 的模板有 {count} 个占位符，而这一档的实参是 {argCount} 个 "
                        + "⇒ Format 在个数不符时**不抛异常**，界面上会渲染出字面的 {{0}}／丢参数");
            }
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
    /// **产出方不许退化**：两个产出方必须抛带码异常，且不许再把裸中文/英文模板拼进异常。
    ///
    /// <para><b>为什么非有不可</b>：站点换成 <c>FailureReason(ex)</c> 之后，<c>ex</c> 若退回裸
    /// <c>ArgumentException</c> / <c>InvalidOperationException</c>，<c>FailureReason</c> 接不住
    /// （<c>SystemFailureCodes</c> 的表里没有这两个类型）⇒ 走第 ④ 支渲染原文，
    /// 而**直通面那张面是绿的**（站点形状确实变了）⇒ 收益为零。</para>
    ///
    /// <para>剥注释是必须的：讲规则的注释里会写出反例（登记 #48 踩过三次）。
    /// ⚠ <c>AuthenticationService</c> 的带码抛出**恰好 2 处** —— 这个数字同时管住反向：
    /// 不许顺手把 <c>ThrowIfNullOrWhiteSpace</c> 那两档（调用方编程错误）也换成带码异常。</para>
    /// </summary>
    internal static List<string> ProducerViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();

        static string? CodeOf(IEnumerable<(string Relative, string Text)> src, string file)
        {
            var hit = src.FirstOrDefault(s => string.Equals(s.Relative, file, StringComparison.Ordinal));
            return hit.Text is null ? null : SourceScanner.StripComments(hit.Text);
        }

        var auth = CodeOf(sources, Producers[0]);
        if (auth is null) bad.Add($"{Producers[0]}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            var coded = CountOccurrences(auth, "throw new UserAdminException(");
            if (coded != 2)
                bad.Add($"{Producers[0]}：`throw new UserAdminException(` 出现 {coded} 次，应为 **2**（角色未注册 / 密码长度不足）"
                    + " ⇒ 少一处 = 那条路会回落中文原文；多一处 = 把调用方编程错误也码化了");

            if (auth.Contains("$\"角色不存在: ", StringComparison.Ordinal)
                || auth.Contains("$\"密码长度不足", StringComparison.Ordinal))
                bad.Add($"{Producers[0]}：又把裸中文原文模板拼进异常了 ⇒ 码这条路被绕开");
        }

        var sim = CodeOf(sources, Producers[1]);
        if (sim is null) bad.Add($"{Producers[1]}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            var coded = CountOccurrences(sim, "throw new SimulatorNotConnectedException()");
            if (coded != 2)
                bad.Add($"{Producers[1]}：`throw new SimulatorNotConnectedException()` 出现 {coded} 次，应为 **2**（读 / 写各一处）");

            if (sim.Contains("\"Simulator not connected\"", StringComparison.Ordinal))
                bad.Add($"{Producers[1]}：又出现英文硬编码的裸消息 `\"Simulator not connected\"` ⇒ 码这条路被绕开");
        }

        return bad;
    }

    /// <summary>
    /// **站点必须走带码出口**：两个视图各 **2** 处 <c>ExternalText.FailureReason(ex)</c>，
    /// 且都不许再出现 <c>, ex.Message)</c> 的直通写法。
    ///
    /// <para><b>为什么数"恰好 2"而不是"至少 1"</b>（阶段 90 的规矩）：<c>UserManagerView</c> 有
    /// **两个**消费同一产出方的站点（创建用户 / 重置密码），只数"至少 1"会让"第二个站点没接"这件事
    /// 静默通过 —— 而那正是本刀顺手修掉的真缺陷（重置密码原本**一个 catch 都没有**，
    /// 密码过短时异常直接冒泡出事件处理器）。</para>
    /// </summary>
    internal static List<string> SiteViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();

        foreach (var file in Sites)
        {
            var hit = sources.FirstOrDefault(s => string.Equals(s.Relative, file, StringComparison.Ordinal));
            if (hit.Text is null) { bad.Add($"{file}：语料里找不到这个文件（扫描面失效？）"); continue; }

            var code = SourceScanner.StripComments(hit.Text);

            var coded = CountOccurrences(code, "ExternalText.FailureReason(ex)");
            if (coded != 2)
                bad.Add($"{file}：`ExternalText.FailureReason(ex)` 出现 {coded} 次，应为 **2**（该视图有 2 个站点）");

            if (code.Contains(", ex.Message)", StringComparison.Ordinal))
                bad.Add($"{file}：仍有 `, ex.Message)` 的直通写法 ⇒ 那个站点没真收口");
        }

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
    /// 主判据：**每一条真实失败**在**两种语言下各渲染一遍**，结果都必须逐字等于
    /// 「该语言字典模板填上真实参」。
    /// </summary>
    /// <summary>
    /// **判据（阶段 168，登记 `#83`）**：`user.issue.password_too_short` 的**文案单位**必须与
    /// **执行口径**一致 —— 两侧都是「UTF-8 字节」。
    ///
    /// <para>病根（修复前实测）：产出方 <c>AuthenticationService.SetUserPassword</c> 用
    /// <c>plainPassword.Length</c>（UTF-16 码元）判，而 <c>SecurityConfig.PasswordMinLength</c>
    /// 的语义、编辑器与设备端 `strlen` 都是**字节** ⇒ 同一个口令在「用户管理 → 重置密码」与
    /// 「工程设置 → 运行时用户」两条产品路径上结论相反；文案还写着「个字符」，
    /// 对 CJK 用户是**说谎**（输入 1 个汉字，提示"当前 3"）。</para>
    /// </summary>
    [Fact]
    public void PasswordTooShort_CopyNamesBytes_MatchingTheEnforcementUnit()
    {
        foreach (var (lang, dict, unit, wrongUnit) in new[]
                 {
                     ("zh-CN", Zh, "字节", "个字符"),
                     ("en-US", En, "bytes", "characters"),
                 })
        {
            var text = dict["user.issue.password_too_short"];
            Assert.True(text.Contains(unit, StringComparison.OrdinalIgnoreCase),
                $"{lang} 的 `user.issue.password_too_short` 没写单位「{unit}」：{text}");
            Assert.False(text.Contains(wrongUnit, StringComparison.OrdinalIgnoreCase),
                $"{lang} 的 `user.issue.password_too_short` 还写着「{wrongUnit}」—— 执行口径是字节，这是对用户说谎：{text}");
        }

        // 前提自检：产出方的**回落原文**也必须按字节说（否则字典改了、回落仍是"字符"）
        var fallback = UserAdminIssue.PasswordTooShort(6, 4).Message;
        Assert.True(fallback.Contains("字节", StringComparison.Ordinal),
            "回落原文没按字节说：" + fallback);
        Assert.False(fallback.Contains("个字符", StringComparison.Ordinal),
            "回落原文还写着「个字符」，与执行口径相反：" + fallback);
    }

    /// <summary>
    /// **判据（阶段 168）**：口令下限的**两个执行点**都必须按 UTF-8 字节数判 ——
    /// 这是「文案说字节」那半边的**因**：源码里只要有一处退回码元口径，上面那条文案判据就变成谎话。
    /// 用**去注释**后的正文判（注释里正讨论这件事，踩过"注释把计数喂饱"的坑）。
    /// </summary>
    [Fact]
    public void PasswordMinimum_IsCountedInBytes_AtBothEnforcementSites()
    {
        var auth = SourceScanner.StripComments(System.IO.File.ReadAllText(System.IO.Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.Core", "Services", "AuthenticationService.cs")));
        Assert.True(auth.Contains("Encoding.UTF8.GetByteCount(plainPassword)", StringComparison.Ordinal),
            "`AuthenticationService.SetUserPassword` 没按 UTF-8 字节数判 —— 与模型语义/编辑器/设备端分叉（#83）");
        Assert.False(auth.Contains("plainPassword.Length < cfg.PasswordMinLength", StringComparison.Ordinal),
            "`AuthenticationService.SetUserPassword` 又退回 UTF-16 码元口径了（#83）");

        var view = SourceScanner.StripComments(System.IO.File.ReadAllText(System.IO.Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", "ProjectSettingsView.axaml.cs")));
        Assert.True(view.Contains("Encoding.UTF8.GetByteCount(password)", StringComparison.Ordinal),
            "`ProjectSettingsView.OnSetRuntimeUserPassword` 没按 UTF-8 字节数判（#83）");
    }

    [Fact]
    public void EveryFailure_RendersTheDictionaryTemplate_InBothLanguages()
    {
        var probe = ProbeFailures();
        var escaped = probe.Escaped;
        Assert.True(escaped.Count == 0, EscapeMessage(escaped));
        var hits = probe.Hits;
        Assert.True(hits.Count >= 4,
            $"只取到 {hits.Count} 条真实失败（应有 4：角色未注册 / 密码过短 / 模拟器读 / 模拟器写）"
            + " —— 取数失效，下面的判定会恒真");

        var rows = new List<RenderRow>();
        var original = AppLocalization.Service.CurrentLanguage;

        try
        {
            foreach (var (lang, dict) in new[] { ("zh-CN", Zh), ("en-US", En) })
            {
                AppLocalization.Service.SwitchLanguage(lang);
                foreach (var hit in hits)
                {
                    rows.Add(new RenderRow(hit.Label, lang, ExternalText.FailureReason(hit.Failure),
                        hit.Key, Expected(dict, hit.Key, hit.Args), hit.Fallback));
                }
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        JudgeReadings.Write(_out, "用户管理 / 模拟器的失败原因（#61d · user.issue.* + sim.issue.*）",
            rows.Select(r => $"    {r.Label,-46} {r.Language} ⇒ {r.Rendered}").ToList());

        // 自检：语言真的切动了。静默没切（比如 SwitchLanguage 被架空）会让 en 那一遍渲染成中文，
        // 那时下面那条期望比对其实已经会红 —— 但红在"渲染 ≠ 期望"上，指错方向。
        foreach (var hit in hits)
        {
            var zhRow = rows.Single(r => r.Label == hit.Label && r.Language == "zh-CN");
            var enRow = rows.Single(r => r.Label == hit.Label && r.Language == "en-US");

            var expectedDiffers = zhRow.Expected is not null && enRow.Expected is not null
                && !string.Equals(zhRow.Expected, enRow.Expected, StringComparison.Ordinal);
            var renderedDiffers = !string.Equals(zhRow.Rendered, enRow.Rendered, StringComparison.Ordinal);

            Assert.True(renderedDiffers || !expectedDiffers,
                $"{hit.Label}：zh 与 en 渲染一模一样「{zhRow.Rendered}」，可两本字典的值并不相同"
                + " ⇒ 语言切换没生效（渲染没跟着当前语言走）");
        }

        var problems = RenderViolations(rows);
        Assert.True(problems.Count == 0,
            "有失败原因没走字典（登记 #61d / 阶段 97）。正确动作是补 `user.issue.<码小写>` / "
            + "`sim.issue.<码小写>` 的字典键，**不是**把裸原文再拼回去：\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>两本字典齐 + 成对 + **占位符个数与实参个数一一对应**。</summary>
    [Fact]
    public void BothDictionaries_CarryEveryCode_AndAreActuallyPaired()
    {
        var probe = ProbeFailures();
        var escaped = probe.Escaped;
        Assert.True(escaped.Count == 0, EscapeMessage(escaped));
        var rows = probe.Hits.Select(h => (h.Key, ArgCount: h.Args?.Count ?? 0)).ToList();
        var distinct = rows.Select(r => r.Key).Distinct(StringComparer.Ordinal).ToList();

        Assert.True(distinct.Count >= 3,
            $"只取到 {distinct.Count} 个键（应有 3：两个 `user.issue.*` ＋ 一个 `sim.issue.*`）—— 取数失效，下面的判定会恒真");

        var problems = PairingViolations(rows, Zh, En);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>码表**双向闭包**：声明的码都真的产得出，产出的码都声明了。</summary>
    [Fact]
    public void DeclaredCodes_AndProducedCodes_AreTheSameSet()
    {
        var declared = DeclaredCodes().Concat(DeclaredSimulatorCodes()).ToList();
        var probe = ProbeFailures();
        var escaped = probe.Escaped;
        Assert.True(escaped.Count == 0, EscapeMessage(escaped));
        var produced = probe.Hits
            .Select(h => h.Key.StartsWith(ExternalText.UserIssueKeyPrefix, StringComparison.Ordinal)
                ? h.Key[ExternalText.UserIssueKeyPrefix.Length..].ToUpperInvariant()
                : h.Key[ExternalText.SimulatorIssueKeyPrefix.Length..].ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        JudgeReadings.Write(_out, "用户管理 / 模拟器码表（#61d）",
            new List<string>
            {
                $"    声明 {declared.Count} 个 / 真实产出 {produced.Count} 个：{string.Join(", ", produced.OrderBy(x => x, StringComparer.Ordinal))}",
            });

        var problems = ClosureViolations(declared, produced);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>两个产出方必须**抛带码异常**（这也是那 3 个站点换出口的前提）。</summary>
    [Fact]
    public void Producers_ThrowTheCodedException_NotABareOne()
    {
        var problems = ProducerViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "用户管理 / 模拟器失败的抛出形状（#61d）",
            Producers.Select(f => $"    {f}").ToList());

        Assert.True(problems.Count == 0,
            "产出方必须带码 —— 否则那 3 个站点换成 FailureReason(ex) 会**收益为零**"
            + "（面绿着、用户看到的中文/英文原文一字不变）：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>三个站点（两个视图共 4 处）必须走带码出口，且不许留 <c>ex.Message</c> 直通。</summary>
    [Fact]
    public void Sites_RenderThroughTheCodedOutlet()
    {
        var problems = SiteViolations(AppSourceScanner.ScanAllProductSources());

        Assert.True(problems.Count == 0,
            "站点没真收口（登记 #61d / 阶段 97）—— 直通面变小不等于收益，要看产出方有没有码：\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **诚实边界正面断言**：没码的异常照旧回落原文 ⇒ 「码才是收益的唯一来源」
    /// （与主判据成对：主判据说"带码的走字典"，这条说"没码的照旧"）。
    ///
    /// <para>拿<b>角色档</b>做"带上码就换了个说法"的那一半：它的 zh / en 字典值**都**与回落原文不同
    /// ⇒ 这条断言不随当前语言变（密码档在 zh 下与回落原文一字不差，是**刻意**的，见下一条判据）。</para>
    /// </summary>
    [Fact]
    public void BareException_StillFallsBackToTheRawMessage_SoTheCodeIsWhatMatters()
    {
        var bare = new ArgumentException("角色不存在: role-typo（请先 RegisterRole 或修正 SecurityConfig.DefaultRoleId）");
        Assert.Equal(bare.Message, ExternalText.FailureReason(bare));

        var coded = new UserAdminException(UserAdminIssue.RoleNotRegistered("role-typo"));
        Assert.NotEqual(coded.Message, ExternalText.FailureReason(coded));
    }

    /// <summary>
    /// **两个方向刻意不同**（本刀唯一的用户可见变化，登记在 §97.9）：
    /// <list type="bullet">
    ///   <item>密码档：zh 字典值 **逐字等于**回落原文 ⇒ 用户所见一字未变（相等式，证明"零变化"）；</item>
    ///   <item>角色档：zh 字典值**刻意不同于**回落原文 —— 回落里那句「请先 RegisterRole 或修正
    ///     SecurityConfig.DefaultRoleId」是**对开发者说的**，界面文案改成用户口吻
    ///     ⇒ 这是**用户可见变化**，必须显式钉住，免得下一个人以为"两边该一致"。</item>
    /// </list>
    /// <para>⚠ 两边都要先**填参**再比：字典值是模板（<c>{0}</c>），回落原文已经把实参写死了。</para>
    /// </summary>
    [Fact]
    public void PasswordFallbackMatchesZh_WhileRoleFallbackDeliberatelyDoesNot()
    {
        var zh = Zh;

        var tooShort = UserAdminIssue.PasswordTooShort(8, 3);
        var tooShortExpected = Expected(zh, ExternalText.UserIssueKey(tooShort.Code), tooShort.Args);
        Assert.Equal(tooShort.Message, tooShortExpected);

        var role = UserAdminIssue.RoleNotRegistered("role-typo");
        var roleExpected = Expected(zh, ExternalText.UserIssueKey(role.Code), role.Args);
        Assert.NotEqual(role.Message, roleExpected);
        Assert.Contains("role-typo", role.Message, StringComparison.Ordinal);
        Assert.Contains("role-typo", roleExpected!, StringComparison.Ordinal);
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
        var escaped = probe.Escaped;
        Assert.True(escaped.Count == 0, EscapeMessage(escaped));
        var hits = probe.Hits;
        var rows = hits.Select(h => (h.Key, ArgCount: h.Args?.Count ?? 0)).ToList();
        var key0 = rows[0].Key;

        // ── NC0 · 正控（**必须合成**：外部负控会让它跟着红）──
        Assert.Empty(PairingViolations(rows, zh, en));

        var clean = hits.Select(h => new RenderRow(h.Label, "zh-CN", Expected(zh, h.Key, h.Args)!, h.Key,
            Expected(zh, h.Key, h.Args), h.Fallback)).ToList();
        Assert.Empty(RenderViolations(clean));
        Assert.All(clean, r => Assert.NotNull(r.Expected));

        // ── NC1 · 成对性 ──
        var enCopy1 = new Dictionary<string, string>(en, StringComparer.Ordinal) { [key0] = zh[key0] };
        Assert.Contains(PairingViolations(rows, zh, enCopy1), v => v.Contains("中文字符", StringComparison.Ordinal));

        var enCopy2 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy2.Remove(key0);
        Assert.Contains(PairingViolations(rows, zh, enCopy2), v => v.Contains("缺键", StringComparison.Ordinal));

        // 占位符：**挑一个 0 实参档**塞 {0} ⇒ 点名（个数不符会渲染出字面的 {0}）。
        // ⚠ 不能挑 rows[0]：那档本来就 1 个实参、1 个占位符，塞进去个数照样对得上 ⇒ 注入无效
        //（第一版就栽在这里：负控绿着，证明不了判据在盯占位符）。
        var zeroArgKey = rows.First(r => r.ArgCount == 0).Key;
        var zhCopy = new Dictionary<string, string>(zh, StringComparer.Ordinal) { [zeroArgKey] = zh[zeroArgKey] + " {0}" };
        Assert.Contains(PairingViolations(rows, zhCopy, en), v => v.Contains("占位符", StringComparison.Ordinal));

        // ── NC2 · 渲染对照：两侧都要报 ──
        // ① 期望串对了、渲染换了别的说法 ⇒ 点名"有别的来源"
        Assert.Contains(
            RenderViolations(new[] { new RenderRow("演示", "en-US", "换个说法", key0, "期望串", "回落原文") }),
            v => v.Contains("有别的来源", StringComparison.Ordinal));

        // ② 该语言字典里没这个键 ⇒ 点名字典缺键（不是静默通过）
        Assert.Contains(
            RenderViolations(new[] { new RenderRow("演示", "en-US", "回落原文", key0, null, "回落原文") }),
            v => v.Contains("字典里没有", StringComparison.Ordinal));

        // ── NC3 · 双向闭包：**两侧都要报** ──
        var declared = DeclaredCodes().Concat(DeclaredSimulatorCodes()).ToList();
        var produced = new[] { "ROLE_NOT_REGISTERED", "PASSWORD_TOO_SHORT", "NOT_CONNECTED" };
        Assert.Empty(ClosureViolations(declared, produced));
        Assert.Contains(ClosureViolations(declared, produced.Append("NOT_A_REAL_CODE")),
            v => v.Contains("没声明", StringComparison.Ordinal));
        Assert.Contains(ClosureViolations(declared.Append("NOT_A_REAL_CODE"), produced),
            v => v.Contains("死码", StringComparison.Ordinal));

        // ── NC4 · 产出方（合成语料，**每个目标文件都要在**）──
        var all = AppSourceScanner.ScanAllProductSources();
        static (string Relative, string Text)[] Corpus(string target, string body)
            => new[] { (Producers[0], ""), (Producers[1], "") }
                .Select(t => (t.Item1, t.Item1 == target ? body : Compliant(t.Item1)))
                .ToArray();

        static string Compliant(string file) => file == Producers[0]
            ? "throw new UserAdminException(a);\nthrow new UserAdminException(b);"
            : "throw new SimulatorNotConnectedException();\nthrow new SimulatorNotConnectedException();";

        Assert.Empty(ProducerViolations(Corpus(Producers[0], Compliant(Producers[0]))));

        // ① 退回裸异常 + 裸中文模板 ⇒ 两条都点名
        var caught = ProducerViolations(Corpus(Producers[0],
            "throw new ArgumentException($\"角色不存在: {id}\");\nthrow new UserAdminException(a);"));
        Assert.Contains(caught, v => v.Contains("出现 1 次", StringComparison.Ordinal));
        Assert.Contains(caught, v => v.Contains("裸中文原文模板", StringComparison.Ordinal));

        // ② 模拟器退回英文硬编码 ⇒ 点名
        Assert.Contains(
            ProducerViolations(Corpus(Producers[1],
                "throw new InvalidOperationException(\"Simulator not connected\");\nthrow new SimulatorNotConnectedException();")),
            v => v.Contains("Simulator not connected", StringComparison.Ordinal));

        // ③ 语料里缺一个文件 ⇒ 必须报"找不到"（本函数唯一的下界：扫描面失效时会静默全绿）
        Assert.Contains(
            ProducerViolations(new[] { (Producers[1], Compliant(Producers[1])) }),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ④ 注释里写的反例不算（规则不许被自己的文档满足，登记 #48）
        Assert.Empty(ProducerViolations(Corpus(Producers[0],
            Compliant(Producers[0])
            + "\n// 反例（不许这么写）：throw new ArgumentException($\"角色不存在: x\");")));

        // ── NC5 · 站点接线（注入 + 真语料对照）──
        Assert.Empty(SiteViolations(all));
        Assert.Contains(
            SiteViolations(new[] { (Sites[0], "Format(Lang.UserMgrCreateFailed, ex.Message)") }),
            v => v.Contains("出现 0 次", StringComparison.Ordinal));
    }
}
