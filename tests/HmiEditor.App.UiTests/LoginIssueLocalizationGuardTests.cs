using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「**登录失败的原因必须由字典决定**」的守卫 —— 登记表 §4 <b>#61v</b>（阶段 99）。
///
/// <para><b>它守的是哪一类缺陷</b>：<c>AuthenticationService.Login</c> 改动前的签名是
/// <c>public string? Login(...)</c> —— 会话 id 与 <c>null</c> 两个取值，而**五种真失败
/// 全都返回 <c>null</c>**（用户不存在 / 账户锁定 / 账户禁用 / 账户过期 / 密码错误）。
/// 站点 <c>LoginDialog.OnLogin</c> 于是只能写死一句话：</para>
/// <code>ShowError(AppLocalization.Service["login.err"]);   // 「用户名或密码错误」</code>
/// <para>后果不是"不好看"，而是**用户做不出下一步**：这五档的处置**完全不同** ——
/// 锁定要等 <c>LockedUntilUtc</c> 到点、禁用要去找管理员解除、过期要找管理员续期、
/// 密码错只要重输。而**原因其实早就写进审计了**（<c>「用户不存在: X」</c> /
/// <c>「账户已锁定」</c> / <c>「密码错误 (n/m)」</c>），只是**送不回界面**。</para>
///
/// <para><b>处置 = 返回值带码</b>（与 <c>SvgParseError</c> / <c>CsvImportIssue</c> /
/// <c>ExpressionSyntaxError</c> / <c>RecipeCloneIssue</c> 同一条模板，见登记 #61f「模板已立」）：
/// <see cref="LoginResult"/> ＋ <see cref="LoginIssue"/> ＋
/// <c>ExternalText.Describe(LoginIssue)</c>，键统一走 <c>login.issue.&lt;码小写&gt;</c>。
/// ⚠ 它**不**走 <c>ExternalText.FailureReason</c> —— 那条路收的是 <c>Exception</c>，
/// 而本族产出方**不抛异常**（它把失败做成了返回值）。</para>
///
/// <para><b>⚠ 为什么不能只把站点换成码化出口就收工</b>：站点换成
/// <c>ExternalText.Describe(issue)</c> 而**产出方没给码** = 阶段 83 钉死的**改名式假闭环**
/// （用户所见一字不变，而 <c>ExceptionMessagePassthroughFaceTests</c> 那张面会绿着变空）。
/// 本刀的三半必须同时在：**产出方给码** ＋ **站点接住** ＋ **键进两本字典**。
/// 所以下面既有产出方形状判据、也有站点接线判据、还有码表三态闭包。</para>
///
/// <para><b>⚙ 主判据为什么是「毒化 + 两语言各渲染一遍」双见证</b>（阶段 97 立、本族照用）：</para>
/// <list type="number">
///   <item><b>毒化</b>（<see cref="Poison"/> 塞进 <c>Message</c> 当回落原文）：渲染里若冒头就说明
///     字典键没被查到、走了回落 —— 而且**与当前语言无关**；</item>
///   <item><b>两语言各渲染一遍</b>：断言渲染逐字等于「该语言字典模板填上真实参」——
///     它额外钉住**实参真的被填进去了**（<c>LocalizationService.Format</c> 在占位符与实参
///     个数不符时**不抛异常**，用户会看到字面的 <c>{0}</c>）。
///     ⚠ 这一条对本族**尤其要紧**：两档 <c>locked</c> / <c>locked_now</c> 的模板里
///     「等多久 / 锁多久」那句话，整个价值就在那个 <c>{0}</c> 上。</item>
/// </list>
///
/// <para><b>⚙ 本类会切换进程级全局语言 ⇒ 必须挂 <c>[Collection("Language-global")]</c></b>
/// （<c>DisableParallelization = true</c>）—— 否则与大队并行时会打红**别的**测试类，
/// 而症状随字典内容变、极难归因。判据是
/// <c>LocalizationRefreshUiTests.EveryTestClassThatSwitchesLanguage…</c>；切换必然 <c>finally</c> 还原。</para>
///
/// <para><b>⚙ 本族与 M16（配方克隆）有一处结构性不同</b>：那族的 <c>REJECTED</c> 是概率档
/// （<c>Guid[..8]</c> 撞车）⇒ 有 <see cref="NotProducible"/> 表、还要另配一条判据去钉
/// 「那个工厂确实挂在产出方失败分支上」（登记 #49：判据的担保方自己不能是死的）。
/// **本族 5 个码全部可确定性复现**（锁定的两条路都能手工摆出来）⇒
/// <see cref="NotProducible"/> 是**空的**，闭包退化成二态：声明 = 真实产出。
/// 那条「表必须是空的」本身写成断言 —— 否则将来有人往表里塞一条，
/// 就等于把某个死码合法化而不被发现。</para>
///
/// <para><b>诚实边界</b>：</para>
/// <list type="number">
///   <item>只断「渲染等于该语言的字典模板」，断不了**翻译质量**；</item>
///   <item><b>⚠「渲染 ≠ 中文回落」在 zh 下**不成立**、不许拿来当判据</b>（本判据第一版就在这里
///     红过一次）：本族的 zh 字典值与 Core 的中文回落**本来就是同一句话** —— 回落原文就是那句中文，
///     <c>bad_credentials</c> 更是**逐字相同**（那正是"与 <c>login.err</c> 合并"的设计）。
///     所以「字典真的在起作用」只能靠**毒化**那一路见证（换掉回落原文再渲染 ⇒ 冒头即没查到），
///     不能靠"两串不相等"。⚠ 这与 M16 不同：那族的 zh 字典值**重新措辞**过，
///     所以在 zh 下断言两者不等也能过 —— **这条差异不是判据强度，是字典内容的偶然**；</item>
///   <item><c>LoginDialog</c> 的 <c>catch</c> 里那句 <c>Format("login.failed", ex.Message)</c>
///     **刻意保留**：它接的是 <c>Task.Run</c> 里逃出来的**异常**（IO / 审计落盘失败），
///     产出方给不出码 ⇒ 换出口是改名式假闭环。那是有意留的基线行，见
///     <c>ExceptionMessagePassthroughFaceTests</c>（另：站点与服务端的两道输入门槛
///     阶段 98 已对齐成 <c>IsNullOrWhiteSpace</c>，那是 #61t 的顺带项、不是本项）；</item>
///   <item><c>BAD_CREDENTIALS</c> **刻意合并了「用户不存在」与「密码错误」**
///     （见 <see cref="LoginIssueCodes.BAD_CREDENTIALS"/>）：分开等于把界面变成
///     **用户名枚举**预言机，而改动前两者本来就显示同一句 ⇒ 分开属**范围外**的行为变更。
///     这一条由 <see cref="Factories"/> 的计数**钉住**（<c>BadCredentials</c> 恰好 2 处调用）；</item>
///   <item><c>AuthenticationService.BreakGlassLogin</c> 是 <c>Login</c> 的**第二个消费点**
///     （同类型内部、unqualified 调用 —— 量的那一遍靠 <c>grep '.Login('</c> **看不见它**，
///     是编译器点出来的）。它的 <c>string?</c> 契约与三档失败口径**刻意不动**：
///     失败原因（理由为空 / 不是管理员 / 密码错）不是登录原因，且它**没有界面消费方**
///     ⇒ 码化它今天没有用户可见收益（与 #61k 同类「判过的不做」）。</item>
/// </list>
/// </summary>
[Collection("Language-global")]
public class LoginIssueLocalizationGuardTests
{
    private readonly ITestOutputHelper _out;

    public LoginIssueLocalizationGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>毒化记号：拿它当"中文回落原文"，凡在渲染里冒头就说明字典键没被查到。</summary>
    internal const string Poison = "<<中文回落原文未被使用>>";

    /// <summary>模板占位符索引（<c>{0}</c> / <c>{1}</c>）。与实参个数的对应是硬判据 ——
    /// <c>LocalizationService.Format</c> 在个数不符时**不抛异常**，用户会看到字面的 <c>{0}</c>。</summary>
    private static readonly Regex PlaceholderIndex = new(@"\{(\d+)\}", RegexOptions.Compiled);

    /// <summary>CJK 汉字 —— 用来抓"en-US 的值其实没翻"，也用来判"英文界面下这一半是不是还冒中文"。</summary>
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>产出方：本刀把它从「<c>string?</c> + 五种失败都 <c>return null</c>」改成**返回值带码**。</summary>
    internal const string Producer = "src/HmiEditor.Core/Services/AuthenticationService.cs";

    /// <summary>站点：本刀把它的失败档从写死 <c>login.err</c> 改成按码分派。</summary>
    internal static readonly string[] Sites =
    {
        "src/HmiEditor.App/Views/LoginDialog.axaml.cs",
    };

    /// <summary>
    /// 码表的**第三态** —— 本族**刻意是空的**。
    ///
    /// <para>M16（配方克隆）要用它装概率档（<c>Guid[..8]</c> 撞车）；本族 5 个码
    /// **全部可确定性复现**，所以一条都不该进来。<c>ClosureViolations</c> 照旧收这张表，
    /// 「表必须是空的」由 <see cref="DeclaredCodes_AreEitherProducedOrExplicitlyNotProducible"/>
    /// 直接断言 —— 那条断言是**防退化**用的：将来若有人往空表里塞一条，
    /// 等于把某个死码合法化，而闭包判据本身不会红。</para>
    /// </summary>
    internal static readonly Dictionary<string, string> NotProducible = new(StringComparer.Ordinal);

    /// <summary>
    /// 产出方里**每个工厂各该出现几次** —— 这张表是「码 → 产出分支」的**接线见证**。
    ///
    /// <para>⚠ <c>BadCredentials</c> 是 **2**，不是 1：那正是「用户不存在」与「密码错误」
    /// **共用同一个码**的设计（见类注释的诚实边界 ④）。它掉到 1 或涨到 3 都说明
    /// **分派口径被改了** —— 分开会让界面变成用户名枚举预言机，合并更多会让审计与界面脱节。</para>
    /// </summary>
    internal static readonly (string Factory, string Code, int Expected)[] Factories =
    {
        ("LoginIssue.BadCredentials(", LoginIssueCodes.BAD_CREDENTIALS, 2),
        ("LoginIssue.Locked(", LoginIssueCodes.LOCKED, 1),
        ("LoginIssue.LockedNow(", LoginIssueCodes.LOCKED_NOW, 1),
        ("LoginIssue.Disabled(", LoginIssueCodes.DISABLED, 1),
        ("LoginIssue.Expired(", LoginIssueCodes.EXPIRED, 1),
    };

    /// <summary>
    /// **合成语料**：一个形状完全合规的产出方 / 站点。
    ///
    /// <para>⚠ 负控的正控**必须用合成的语料**（见 <see cref="NegativeControls_EachBreakIsCaughtByTheRightJudgment"/>
    /// 的 NC0）：拿真仓库当正控，一旦真仓库坏了，负控会跟着红，
    /// 那时分不清是"注入生效"还是"仓库真坏了"。真语料对照单独放在 NC6。</para>
    /// </summary>
    private const string CompliantProducer = """
        public LoginResult Login(string username, string password, string? source = null)
        {
            var user = GetUserByUsername(username);
            if (user == null) return LoginResult.Failed(LoginIssue.BadCredentials());
            if (user.IsLocked) return LoginResult.Failed(LoginIssue.Locked(RemainingLockMinutes(user)));
            if (!user.IsEnabled) return LoginResult.Failed(LoginIssue.Disabled());
            if (user.ExpiresAt.HasValue) return LoginResult.Failed(LoginIssue.Expired());
            if (!VerifyPassword(password, user.PasswordHash))
            {
                var justLocked = ++user.FailedLoginAttempts >= MaxLoginAttempts;
                return LoginResult.Failed(justLocked
                    ? LoginIssue.LockedNow(30)
                    : LoginIssue.BadCredentials());
            }
            return LoginResult.Ok("sid");
        }
        """;

    /// <summary>合成站点：形状与 <see cref="Sites"/> 里那个真站点一致。</summary>
    private const string CompliantSite = """
        var result = await Task.Run(() => _authService.Login(username, password));
        if (result.SessionId is { } sessionId) { SessionId = sessionId; Close(); }
        else if (result.Issue is { } issue) { ShowError(ExternalText.Describe(issue)); }
        """;

    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    // ══════════════════════════ 取数（同源出口 —— 负控与 M17 都复用它） ══════════════════════════

    /// <summary>
    /// 反射枚举 <see cref="LoginIssueCodes"/> 里声明的全部码。
    ///
    /// <para><b>是同源出口</b>：本文件的多条判据与 <c>LocalizationKeyReachabilityGuardTests</c>
    /// 的 <b>M17</b> 都调它，不各写一份反射（抄两份必然漂移，漂移的表现是"一个守卫绿、另一个红"）。</para>
    ///
    /// <para>⚠ "反射到 0 个码"会让三态闭包**恒真** ⇒ 下面每条判据都带下界断言兜底。</para>
    /// </summary>
    internal static List<string> DeclaredCodes()
        => typeof(LoginIssueCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// 一次取到的失败：人读的标签 + 那个 <see cref="LoginIssue"/> + **它是不是由产出方真的产出来的**。
    ///
    /// <para>本族**每一档都走真实产出路径**（<see cref="ProbeFailures"/> 手工摆出五种运行条件），
    /// 所以今天这一维恒为 <c>true</c>；留着它是因为 M16 的那条教训 ——
    /// 「**产出方真的会发出这个码吗**」（闭包问的）≠「**码 → 键 → 两本字典通不通**」（渲染问的）
    /// 是两个问题，一旦将来出现概率档就要分开。判据里对它带下界断言，
    /// 防止取数退化成"手搓"而闭包恒真。</para>
    /// </summary>
    internal sealed record Hit(string Label, LoginIssue Issue, bool ReachedByProducer);

    /// <summary>一次取数的结果：**命中** + **退化**。</summary>
    /// <param name="Hits">拿到的失败档（五档应当全在）。</param>
    /// <param name="Degraded">本该返回某个码、却返回了别的码（或返回了成功）的路径 —— 产出方退化的直接证据。</param>
    internal sealed record Probe(List<Hit> Hits, List<string> Degraded);

    /// <summary>
    /// 「退化」的统一红消息：点名**哪条路**没给出带码失败档、以及正确动作。
    ///
    /// <para>记成数据再判，红才自带信息（本项目对判据的一贯要求）—— 直接断言
    /// <c>Hits.Count == n</c> 时看不出是哪条路退化、也看不出该修产出方还是修站点。</para>
    /// </summary>
    internal static string DegradedMessage(IReadOnlyList<string> degraded)
        => "这几条路径本该返回**带码的失败档**，却没有 ⇒ 产出方退化了（登记 #61v / 阶段 99）。"
           + "正确动作是让产出方**给码**（`LoginResult.Failed(LoginIssue.…)`），"
           + "**不是**把站点改回 `return null` + 一句写死的「用户名或密码错误」：\n  - "
           + string.Join("\n  - ", degraded);

    /// <summary>
    /// **五种运行条件各摆一次**，每次都真的调一次 <c>AuthenticationService.Login</c>。
    ///
    /// <para>⚠ 手搓 <c>LoginIssue</c> 的话，"码表与产出方脱节"这件事就永远测不到 ——
    /// 所以每一档都必须真的调一次 <c>Login</c>，并断言拿回来的是
    /// <c>LoginResult.Failed</c> 那一档（而不是 <c>Ok</c>、也不是"别的码"）。</para>
    ///
    /// <para>⚠ 各档**各自新建一个服务实例**：五档的检查是**互斥分支**（锁定 → 禁用 → 过期 → 密码），
    /// 复用同一个实例会让后一档被前一档的副作用挡住（例如先摆"禁用"再想测"密码错误"就永远
    /// 走不到密码那一步）。</para>
    /// </summary>
    internal static Probe ProbeFailures()
    {
        var hits = new List<Hit>();
        var degraded = new List<string>();

        void Take(string label, LoginResult result, string expectedCode, string how)
        {
            if (result.SessionId is null && result.Issue is { } issue)
            {
                if (string.Equals(issue.Code, expectedCode, StringComparison.Ordinal))
                    hits.Add(new Hit(label, issue, ReachedByProducer: true));
                else
                    degraded.Add($"{label}：返回的码是 `{issue.Code}`，应为 `{expectedCode}`（{how}）");
            }
            else
            {
                degraded.Add($"{label}：返回的是 SessionId="
                    + $"{(result.SessionId is null ? "null" : "非null")} / "
                    + $"Issue={(result.Issue is null ? "null" : "非null")}，"
                    + $"应为 Failed 那一档（Issue 非空且 SessionId 为空）（{how}）");
            }
        }

        // ① 密码错误 —— 预置账户 admin/admin，喂错密码。
        {
            using var auth = new AuthenticationService();
            Take("密码错误", auth.Login("admin", "wrong-password"),
                LoginIssueCodes.BAD_CREDENTIALS, "预置账户 admin/admin 喂错密码");
        }

        // ② 用户不存在 —— 与「密码错误」**共用同一个码**（见类注释④）。
        {
            using var auth = new AuthenticationService();
            Take("用户不存在", auth.Login("no-such-user-99", "whatever"),
                LoginIssueCodes.BAD_CREDENTIALS, "喂一个库里没有的用户名");
        }

        // ③ 账户已锁定 —— 摆 `IsLocked = true` ＋ 未到期的 `LockedUntilUtc`。
        //    ⚠ 不走「连错 N 次」那条路：那条的下一档是 LOCKED_NOW（本动作的**后果**），
        //    这一档要的是**已经**锁着、再来一次时的措辞（「请 N 分钟后再试」）。
        {
            using var auth = new AuthenticationService();
            var user = auth.GetUserByUsername("admin")!;
            user.IsLocked = true;
            user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(10);
            Take("账户已锁定", auth.Login("admin", "admin"),
                LoginIssueCodes.LOCKED, "admin 已锁定且锁定未到期，即使密码正确");
        }

        // ④ 本次失败把账户顶到锁定 —— `MaxLoginAttempts = 1`，一次错密码即触发。
        {
            using var auth = new AuthenticationService { MaxLoginAttempts = 1 };
            Take("本次失败触发锁定", auth.Login("admin", "wrong-password"),
                LoginIssueCodes.LOCKED_NOW, "MaxLoginAttempts=1 时喂错密码");
        }

        // ⑤ 账户已禁用 —— 走公开 API `SetUserEnabled`，不用手改字段。
        {
            using var auth = new AuthenticationService();
            var user = auth.GetUserByUsername("admin")!;
            auth.SetUserEnabled(user.Id, false);
            Take("账户已禁用", auth.Login("admin", "admin"),
                LoginIssueCodes.DISABLED, "SetUserEnabled(admin, false) 后即使密码正确");
        }

        // ⑥ 账户已过期 —— `ExpiresAt` 没有公开 setter 的 API，只能摆字段（它就是给临时账户用的）。
        {
            using var auth = new AuthenticationService();
            auth.GetUserByUsername("admin")!.ExpiresAt = DateTime.UtcNow.AddDays(-1);
            Take("账户已过期", auth.Login("admin", "admin"),
                LoginIssueCodes.EXPIRED, "ExpiresAt 设在昨天，即使密码正确");
        }

        return new Probe(hits, degraded);
    }

    /// <summary>一次渲染对照：标签 + 语言 + 实渲染 + 键 + 该语言字典**填参后**的期望串 + 该语言下的回落原文。</summary>
    internal sealed record RenderRow(
        string Label, string Language, string Rendered, string Key, string? Expected, string Fallback);

    /// <summary>某本字典里这个键**填上实参**之后的串；键不在字典里返回 <c>null</c>。</summary>
    internal static string? Expected(Dictionary<string, string> dict, string key, IReadOnlyList<object>? args)
    {
        if (!dict.TryGetValue(key, out var body)) return null;
        return args is { Count: > 0 } a
            ? string.Format(CultureInfo.InvariantCulture, body, a.ToArray())
            : body;
    }

    /// <summary>统一的"红要自带信息"包装（本项目的一贯要求：红了要指到哪一条腿）。</summary>
    private static void AssertClean(List<string> problems, string what)
        => Assert.True(problems.Count == 0, $"{what}：\n  - " + string.Join("\n  - ", problems));

    // ══════════════════════════ 判据逻辑（纯函数 —— 负控直接调它们） ══════════════════════════

    /// <summary>
    /// 渲染必须**不冒毒化**、且**逐字等于「该语言字典模板填上真实参」**；键不在该语言字典里 ⇒ 点名。
    ///
    /// <para>两条一起判，因为它们的见证面不重合：毒化见证"没走回落"，期望串比对见证
    /// "实参真的被填进去了"（<c>Format</c> 在个数不符时不抛异常）。少任何一条都会漏掉一类缺陷。</para>
    /// </summary>
    internal static List<string> RenderViolations(IEnumerable<RenderRow> rows, string poison)
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

            if (r.Rendered.Contains(poison, StringComparison.Ordinal))
            {
                bad.Add($"{r.Label} @{r.Language}：渲染里出现**中文回落原文** ⇒ 字典键 {r.Key} 没被查到"
                    + "（查到了就不该回落）");
                continue;
            }

            if (!string.Equals(r.Rendered, r.Expected, StringComparison.Ordinal))
                bad.Add($"{r.Label} @{r.Language}：渲染「{r.Rendered}」≠ 该语言字典填参后的"
                    + $"「{r.Expected}」⇒ 有别的来源（漏填实参 / 码映射错位 / 渲染没走本键）");
        }

        return bad;
    }

    /// <summary>两本字典对每个码的完备性、成对性，以及**占位符个数与实参个数的对应**。</summary>
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

    /// <summary>
    /// 码表的**三态闭包**：<c>声明 = 真实产得出 ∪ 明确产不出（且写了理由）</c>。
    ///
    /// <para>四档都要报（阶段 88 立的口径，本族照用）：</para>
    /// <list type="bullet">
    ///   <item>声明了、既产不出也不在表里 ⇒ **死码**；</item>
    ///   <item>产出了、但没声明 ⇒ **漏登记**（键不会进字典，界面回落中文原文）；</item>
    ///   <item>**表里说产不出、其实产得出** ⇒ 那条豁免过期了，必须删（登记 #54 的病）；</item>
    ///   <item>表里记了一个根本没声明的码 ⇒ 抄写漂移。</item>
    /// </list>
    /// </summary>
    internal static List<string> ClosureViolations(
        IEnumerable<string> declared, IEnumerable<string> produced, IReadOnlyDictionary<string, string> notProducible)
    {
        var d = new HashSet<string>(declared, StringComparer.Ordinal);
        var p = new HashSet<string>(produced, StringComparer.Ordinal);
        var np = new HashSet<string>(notProducible.Keys, StringComparer.Ordinal);
        var bad = new List<string>();

        foreach (var c in d.Except(p).Except(np).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"声明了但真实产不出、也没写进 NotProducible：{c} ⇒ 死码，它和它的字典键都永远不会被用到");

        foreach (var c in p.Except(d).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"产出了但没声明：{c} ⇒ 键不会进字典，界面会回落中文原文");

        foreach (var c in np.Intersect(p).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"NotProducible 里说「产不出」、实际却产得出：{c} ⇒ 那条豁免过期了，删掉它"
                + "（豁免表里的理由没人核就会变成把缺陷合法化的字段，登记 #54）");

        foreach (var c in np.Except(d).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"NotProducible 里记了 {c}，但码表里根本没有这个码 ⇒ 抄写漂移");

        return bad;
    }

    /// <summary>
    /// **产出方不许退化**：<c>Login</c> 必须返回带码的 <see cref="LoginResult"/>，
    /// 且五个工厂都真的挂在失败分支上。
    ///
    /// <para><b>为什么这几条非有不可</b>：站点换成 <c>ExternalText.Describe(issue)</c> 之后，
    /// 产出方若退回「<c>string?</c> + 五档都 <c>return null</c>」，那个 <c>issue</c> 就永远为
    /// <c>null</c> ⇒ 失败档分支变成死代码，而**站点形状是"对"的**（它确实接了失败档）
    /// ⇒ 又是"面绿着、收益为零"。<see cref="Factories"/> 的逐个计数就是这条的见证：
    /// 每个码都必须在产出方里**真的被发出**，否则它就是个死码。</para>
    ///
    /// <para>剥注释是必须的：讲规则的注释里会写出反例（登记 #48 踩过三次 ——
    /// 守卫自己的文档把对象"写活"）。用**计数相等**而不是"包含某个串"：
    /// 后者会让"把码加进注释"或"多写一处裸调用"静默通过。</para>
    /// </summary>
    internal static List<string> ProducerViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();

        var hit = sources.FirstOrDefault(s => string.Equals(s.Relative, Producer, StringComparison.Ordinal));
        if (hit.Text is null) { bad.Add($"{Producer}：语料里找不到这个文件（扫描面失效？）"); return bad; }

        var code = SourceScanner.StripComments(hit.Text);

        if (!code.Contains("public LoginResult Login(", StringComparison.Ordinal))
            bad.Add($"{Producer}：Login 的签名不再是 `public LoginResult Login(` "
                + "⇒ 失败档的**码**没地方放（`string?` 的形状下，五种失败只能一起返回 null，"
                + "界面无从分辨）");

        if (code.Contains("return null!", StringComparison.Ordinal))
            bad.Add($"{Producer}：出现 `return null!` ⇒ 「声明非空 + 压掉警告」是改动前 CloneRecipe 那个形状，"
                + "消费点会以为「绝不会 null」、连失败档都不写");

        if (code.Contains("return session.Id;", StringComparison.Ordinal))
            bad.Add($"{Producer}：出现 `return session.Id;` ⇒ 那是「成功返回字符串、失败返回 null」的旧形状，"
                + "它自带「五档失败不分家」的前提");

        var ok = CountOccurrences(code, "LoginResult.Ok(");
        if (ok != 1)
            bad.Add($"{Producer}：`LoginResult.Ok(` 出现 {ok} 次，应为 **1**（成功只有一个出口）");

        var failed = CountOccurrences(code, "LoginResult.Failed(");
        if (failed < Factories.Length)
            bad.Add($"{Producer}：`LoginResult.Failed(` 只出现 {failed} 次，至少应有 **{Factories.Length}** 处"
                + "（五个失败档各一处）⇒ 有失败档仍在 `return null`");

        foreach (var (factory, issueCode, expected) in Factories)
        {
            var used = CountOccurrences(code, factory);
            if (used != expected)
                bad.Add($"{Producer}：`{factory}` 出现 {used} 次，应为 **{expected}** "
                    + $"⇒ 码 {issueCode} 的分派口径变了（它必须挂在产出方的失败分支上；"
                    + $"`{LoginIssueCodes.BAD_CREDENTIALS}` 是 2 处：用户不存在 ＋ 密码错误**共用**，见类注释④）");
        }

        // 「锁定还剩几分钟」必须真的**算出来送上去** —— 这一档的全部价值就在那个数字上。
        if (CountOccurrences(code, "LoginIssue.Locked(RemainingLockMinutes(user))") != 1)
            bad.Add($"{Producer}：找不到 `LoginIssue.Locked(RemainingLockMinutes(user))` ⇒ "
                + "「请 N 分钟后再试」里的 N 没有来源（要么没算、要么送了常量 —— 用户看到的数字就是错的）");

        return bad;
    }

    /// <summary>
    /// **站点必须接住失败档**：<c>LoginDialog</c> 恰好 **1** 处码化出口，
    /// 有 <c>result.Issue is { } issue</c> 分支，且**不再**把旧键当文案用。
    ///
    /// <para><b>为什么数"恰好 1"而不是"至少 1"</b>（阶段 90 的规矩）：该对话框只有一个登录站点，
    /// "至少 1"会让"接了但接在别处 / 接重了"静默通过。</para>
    /// </summary>
    internal static List<string> SiteViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();

        foreach (var file in Sites)
        {
            var hit = sources.FirstOrDefault(s => string.Equals(s.Relative, file, StringComparison.Ordinal));
            if (hit.Text is null) { bad.Add($"{file}：语料里找不到这个文件（扫描面失效？）"); continue; }

            var code = SourceScanner.StripComments(hit.Text);

            var coded = CountOccurrences(code, "ExternalText.Describe(issue)");
            if (coded != 1)
                bad.Add($"{file}：`ExternalText.Describe(issue)` 出现 {coded} 次，应为 **1**"
                    + "（该对话框只有 1 个登录站点）⇒ 0 次 = 失败档没接（改动前就是这样：只能写死一句"
                    + "「用户名或密码错误」）；>1 次 = 出口有第二个来源");

            if (!code.Contains("result.Issue is { } issue", StringComparison.Ordinal))
                bad.Add($"{file}：没有 `result.Issue is {{ }} issue` 这条失败档分派 ⇒ "
                    + "登录失败时界面依旧只有一句通用提示（锁定 / 禁用 / 过期都看不出区别）");

            if (code.Contains("login.err", StringComparison.Ordinal))
                bad.Add($"{file}：还在用旧键 `login.err` ⇒ 那是「五档失败不分家」时代的写法；"
                    + "该键已随本阶段从两本字典退场（继续引用它会让界面直接显示键名）");
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
    /// 主判据：**每一条失败**在**两种语言下各渲染一遍**，结果都必须来自该语言的字典模板。
    ///
    /// <para>毒化那一路：把 <c>Message</c> 换成 <see cref="Poison"/> 再渲染 —— 回落原文**没进字典**
    /// 才是"字典在决定文案"的证明。⚠ 不能拿真原文比对：本族 zh 字典值与回落原文**常常逐字相同**
    /// （见类注释的诚实边界 ②），拿真原文只能测"今天恰好翻了没"，测不到"渲染这一半确实由字典决定"。</para>
    /// </summary>
    [Fact]
    public void EveryFailure_RendersTheDictionaryTemplate_InBothLanguages()
    {
        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));
        var hits = probe.Hits;
        Assert.True(hits.Count >= 5,
            $"只取到 {hits.Count} 条失败档（应有 6 个运行条件、5 个不同的码：密码错误 / 用户不存在共用"
            + " `bad_credentials`，另加已锁定 / 本次触发锁定 / 已禁用 / 已过期）"
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
                    // 毒化：保留码与实参，只把回落原文换掉。
                    var poisoned = new LoginIssue(hit.Issue.Code, Poison, hit.Issue.Args);
                    var key = ExternalText.LoginIssueKey(hit.Issue.Code);
                    rows.Add(new RenderRow(hit.Label, lang, ExternalText.Describe(poisoned),
                        key, Expected(dict, key, hit.Issue.Args), hit.Issue.Message));
                }
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        JudgeReadings.Write(_out, "登录失败的失败原因（#61v · login.issue.*）",
            rows.Select(r => $"    {r.Label,-22} {r.Language} ⇒ {r.Rendered}").ToList());

        // 自检：语言真的切动了。静默没切会让 en 那一遍渲染成中文 —— 那时下面那条期望比对
        // 已经会红，但红在"渲染 ≠ 期望"上，指错方向。
        foreach (var hit in hits)
        {
            var key = ExternalText.LoginIssueKey(hit.Issue.Code);
            var zhExpected = Expected(Zh, key, hit.Issue.Args);
            var enExpected = Expected(En, key, hit.Issue.Args);
            var zhRow = rows.Single(r => r.Label == hit.Label && r.Language == "zh-CN");
            var enRow = rows.Single(r => r.Label == hit.Label && r.Language == "en-US");

            var expectedDiffers = zhExpected is not null && enExpected is not null
                && !string.Equals(zhExpected, enExpected, StringComparison.Ordinal);
            var renderedDiffers = !string.Equals(zhRow.Rendered, enRow.Rendered, StringComparison.Ordinal);

            Assert.True(renderedDiffers || !expectedDiffers,
                $"{hit.Label}：zh 与 en 渲染一模一样「{zhRow.Rendered}」，可两本字典的值并不相同"
                + " ⇒ 语言切换没生效（渲染没跟着当前语言走）");
        }

        var problems = RenderViolations(rows, Poison);
        Assert.True(problems.Count == 0,
            "有失败原因没走字典（登记 #61v / 阶段 99）。正确动作是补 `login.issue.<码小写>` 的字典键，"
            + "**不是**把裸中文串再拼回去：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **收益的正面见证**：真失败（不毒化）在**英文界面下不许再出现中文**。
    ///
    /// <para>它与主判据的见证面不重合：主判据证明"渲染由字典决定"，
    /// 这条证明"英文用户读到的东西真的不含中文" —— 后者正是 #61 那一族缺陷的用户可见症状。
    /// 用 <see cref="Cjk"/> 当尺子、不写死措辞：换文案不该打红它，字典缺键/值没翻才会。</para>
    /// </summary>
    [Fact]
    public void RealFailures_InEnglish_CarryNoChinese()
    {
        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));

        var original = AppLocalization.Service.CurrentLanguage;
        var rows = new List<(string Label, string Rendered, string Fallback)>();
        try
        {
            AppLocalization.Service.SwitchLanguage("en-US");
            foreach (var hit in probe.Hits)
                rows.Add((hit.Label, ExternalText.Describe(hit.Issue), hit.Issue.Message));
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        JudgeReadings.Write(_out, "登录失败 · 英文界面下的渲染（#61v）",
            rows.Select(r => $"    {r.Label,-22} en ⇒ {r.Rendered}").ToList());

        var bad = rows.Where(r => Cjk.IsMatch(r.Rendered))
            .Select(r => $"    {r.Label}：英文界面下渲染出中文「{r.Rendered}」（回落原文是「{r.Fallback}」）"
                + " ⇒ 该语言的字典键没查到")
            .ToList();

        Assert.True(bad.Count == 0,
            "英文界面下这几档仍是中文（登记 #61v / 阶段 99）—— 用户可见的收益就落在这条上：\n"
            + string.Join("\n", bad));
    }

    /// <summary>两本字典齐 + 成对 + **占位符个数与实参个数一一对应**。</summary>
    [Fact]
    public void BothDictionaries_CarryEveryCode_AndAreActuallyPaired()
    {
        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));
        var rows = probe.Hits
            .Select(h => (ExternalText.LoginIssueKey(h.Issue.Code), ArgCount: h.Issue.Args?.Count ?? 0))
            .ToList();
        var distinct = rows.Select(r => r.Item1).Distinct(StringComparer.Ordinal).ToList();

        Assert.True(distinct.Count >= 5,
            $"只取到 {distinct.Count} 个键（应有 5：`login.issue.bad_credentials` / `locked` / `locked_now` /"
            + " `disabled` / `expired`）—— 取数失效，下面的判定会恒真");

        var problems = PairingViolations(rows, Zh, En);
        JudgeReadings.Write(_out, "登录失败 · 两本字典（#61v）",
            distinct.Select(k => $"    {k}").ToList());
        AssertClean(problems, "两本字典对不上（登记 #61v / 阶段 99）");
    }

    /// <summary>码表**三态闭包**：声明的码要么真的产得出，要么在 <see cref="NotProducible"/> 里写了理由。</summary>
    [Fact]
    public void DeclaredCodes_AreEitherProducedOrExplicitlyNotProducible()
    {
        var declared = DeclaredCodes();
        Assert.True(declared.Count >= 5,
            $"只反射到 {declared.Count} 个码 —— 取数失效（反射面空了，三态闭包会恒真）");

        var probe = ProbeFailures();
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));

        // ⚠ 闭包只看**产出方真的发得出来**的那些（见 Hit.ReachedByProducer 的文档）。
        var produced = probe.Hits
            .Where(h => h.ReachedByProducer)
            .Select(h => h.Issue.Code)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(produced.Count >= 5,
            $"没有任何一档是走真实产出路径拿到的（{probe.Hits.Count} 条命中全是手搓）"
            + " —— 取数退化成手搓，三态闭包会恒真");

        JudgeReadings.Write(_out, "登录失败的码表（#61v）",
            new List<string>
            {
                $"    声明 {declared.Count} 个 / 真实产出 {produced.Count} 个 / 明确产不出 {NotProducible.Count} 个："
                + $"产出 = {string.Join(", ", produced.OrderBy(x => x, StringComparer.Ordinal))}",
                $"    DeclaredCodes() = {string.Join(", ", declared)}",
            });

        Assert.True(NotProducible.Count == 0,
            $"NotProducible 表里有 {NotProducible.Count} 条，而本族 5 个码**全部可确定性复现**"
            + " ⇒ 这张表必须为空。往空表里塞一条 = 把某个死码合法化（登记 #54 的病），"
            + "而闭包判据自己不会红 —— 所以这条断言单独钉住它。"
            + "若某天真的出现概率档，把这条改成 >= 1 并给那条写理由，"
            + "同时补一条「工厂确实挂在失败分支上」的判据（见 "
            + "RecipeCloneIssueLocalizationGuardTests 对 REJECTED 的处置，登记 #49）。");

        var problems = ClosureViolations(declared, produced, NotProducible);
        AssertClean(problems, "码表三态闭包破了（登记 #61v / 阶段 99）");
    }

    /// <summary>产出方必须**返回值带码**，五个工厂都真的挂在失败分支上。</summary>
    [Fact]
    public void Producer_ReturnsCodedResult_AndFiresEveryCode()
    {
        var problems = ProducerViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "登录产出方的形状（#61v）",
            new List<string> { $"    {Producer}" });

        Assert.True(problems.Count == 0,
            "产出方必须给码 —— 否则站点换成码化出口会**收益为零**"
            + "（面绿着、用户看到的一字不变）：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>站点必须接住失败档，且不许留旧写法。</summary>
    [Fact]
    public void Site_RendersThroughTheCodedOutlet()
    {
        var problems = SiteViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "登录站点的接线（#61v）",
            Sites.Select(f => $"    {f}").ToList());

        Assert.True(problems.Count == 0,
            "站点没真收口（登记 #61v / 阶段 99）—— 直通面变小不等于收益，要看产出方有没有码：\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **诚实边界正面断言**（两半，缺一不可）：
    /// ① 一条**没有码**的 <c>LoginIssue</c>（字典里没有它的键）照旧回落原文
    /// ⇒ 「码才是收益的唯一来源」，与主判据成对；
    /// ② 有码的那些在**英文**界面下必定**不再是那句中文回落**。
    ///
    /// <para>⚠ ② **只对 en-US 成立、不许对 zh 断言**：本族的 zh 字典值与 Core 的中文回落
    /// **本来就是同一句话**（回落原文就是那句中文，<c>bad_credentials</c> 逐字相同）
    /// ⇒ 在 zh 下断言「渲染 ≠ 回落」会把**正确的实现**判红（本判据第一版就是这么红的）。
    /// 判"字典真的在起作用"要靠主判据的**毒化**那一路，不能靠"两串不相等"。</para>
    /// </summary>
    [Fact]
    public void UncodedIssue_FallsBack_AndCodedOnesDifferFromFallbackInEnglish()
    {
        var bare = new LoginIssue("NOT_A_REAL_CODE", "库里没有这个码的回落原文", new object[] { "x" });
        Assert.Equal(bare.Message, ExternalText.Describe(bare));

        var hits = ProbeFailures().Hits;
        var original = AppLocalization.Service.CurrentLanguage;
        try
        {
            AppLocalization.Service.SwitchLanguage("en-US");
            foreach (var hit in hits)
            {
                var rendered = ExternalText.Describe(hit.Issue);
                Assert.True(!string.Equals(rendered, hit.Issue.Message, StringComparison.Ordinal),
                    $"{hit.Label}：英文界面下渲染出的还是那句中文回落「{rendered}」"
                    + " ⇒ 该语言的字典键没查到");
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }
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
        var degraded = probe.Degraded;
        Assert.True(degraded.Count == 0, DegradedMessage(degraded));
        var hits = probe.Hits;

        var keys = hits.Select(h => (Key: ExternalText.LoginIssueKey(h.Issue.Code),
            ArgCount: h.Issue.Args?.Count ?? 0)).ToList();
        var key0 = keys[0].Key;

        var declared = DeclaredCodes();

        // ── NC0 · 正控（**必须全合成**）──
        //   ⚠ 正控里不许引用真仓库：真仓库坏了，正控会跟着红，那时分不清
        //   "注入生效"还是"仓库真坏了"。真语料对照单独放 NC6。
        var clean = hits.Select(h => new RenderRow(h.Label, "zh-CN",
            Expected(zh, ExternalText.LoginIssueKey(h.Issue.Code), h.Issue.Args)!,
            ExternalText.LoginIssueKey(h.Issue.Code),
            Expected(zh, ExternalText.LoginIssueKey(h.Issue.Code), h.Issue.Args),
            h.Issue.Message)).ToList();

        AssertClean(PairingViolations(keys, zh, en), "NC0 正控破了（两本字典成对性）");
        AssertClean(RenderViolations(clean, Poison), "NC0 正控破了（渲染对照）");
        Assert.All(clean, r => Assert.NotNull(r.Expected));
        // ⚠ 本族 NotProducible 是空的 ⇒ `produced` 必须给**全部**声明码，
        //   否则闭包会把其余几个报成"死码"（本判据第一版就是这么红的）。
        AssertClean(ClosureViolations(declared, declared, NotProducible), "NC0 正控破了（三态闭包）");
        AssertClean(ProducerViolations(new[] { (Producer, CompliantProducer) }), "NC0 正控破了（产出方形状）");
        AssertClean(SiteViolations(new[] { (Sites[0], CompliantSite) }), "NC0 正控破了（站点接线）");

        // ── NC1 · 成对性 ──
        var enCopy1 = new Dictionary<string, string>(en, StringComparer.Ordinal) { [key0] = zh[key0] };
        Assert.Contains(PairingViolations(keys, zh, enCopy1), v => v.Contains("中文字符", StringComparison.Ordinal));

        var enCopy2 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy2.Remove(key0);
        Assert.Contains(PairingViolations(keys, zh, enCopy2), v => v.Contains("缺键", StringComparison.Ordinal));

        // 占位符：**必须先挑一个「本来就 0 占位符」的键**再塞 {0}（个数才对得上 ⇒ 注入无效，
        // 阶段 97 的负控第一版就栽在这里：负控绿着，证明不了判据在盯占位符）。
        var noArgKey = keys.First(k => k.ArgCount == 0).Key;
        Assert.Contains(noArgKey, new[]
        {
            ExternalText.LoginIssueKey(LoginIssueCodes.BAD_CREDENTIALS),
            ExternalText.LoginIssueKey(LoginIssueCodes.DISABLED),
            ExternalText.LoginIssueKey(LoginIssueCodes.EXPIRED),
        });
        var zhCopy0 = new Dictionary<string, string>(zh, StringComparer.Ordinal) { [noArgKey] = zh[noArgKey] + " {0}" };
        Assert.Contains(PairingViolations(keys, zhCopy0, en), v => v.Contains("占位符", StringComparison.Ordinal));

        // 反向：给一个**本来 1 占位符**的键去掉那个占位符 ⇒ 个数照样对不上（两档都要报）。
        var oneArgKey = keys.First(k => k.ArgCount == 1).Key;
        var zhCopy1 = new Dictionary<string, string>(zh, StringComparer.Ordinal) { [oneArgKey] = "没有占位符的模板" };
        Assert.Contains(PairingViolations(keys, zhCopy1, en), v => v.Contains("占位符", StringComparison.Ordinal));

        // ── NC2 · 渲染对照：三档都要报 ──
        // ① 毒化冒头 ⇒ 点名"没被查到"
        Assert.Contains(
            RenderViolations(new[] { new RenderRow("演示", "en-US", "x" + Poison, key0, "期望串", "回落原文") }, Poison),
            v => v.Contains("中文回落原文", StringComparison.Ordinal));

        // ② 期望串对了、渲染换了别的说法 ⇒ 点名"有别的来源"
        Assert.Contains(
            RenderViolations(new[] { new RenderRow("演示", "en-US", "换个说法", key0, "期望串", "回落原文") }, Poison),
            v => v.Contains("有别的来源", StringComparison.Ordinal));

        // ③ 该语言字典里没这个键 ⇒ 点名字典缺键（不是静默通过）
        Assert.Contains(
            RenderViolations(new[] { new RenderRow("演示", "en-US", "回落原文", key0, null, "回落原文") }, Poison),
            v => v.Contains("字典里没有", StringComparison.Ordinal));

        // ── NC3 · 三态闭包：四档都要报（前提是「声明 = 产出」这个正控已经绿）──
        // ① 声明了、产不出、也不在表里 ⇒ 死码
        Assert.Contains(
            ClosureViolations(declared, new[] { LoginIssueCodes.BAD_CREDENTIALS },
                new Dictionary<string, string>(StringComparer.Ordinal)),
            v => v.Contains("死码", StringComparison.Ordinal));

        // ② 产出了没声明 ⇒ 漏登记
        Assert.Contains(
            ClosureViolations(declared, declared.Append("NOT_A_REAL_CODE"), NotProducible),
            v => v.Contains("没声明", StringComparison.Ordinal));

        // ③ 表里说产不出、其实产得出 ⇒ 豁免过期（登记 #54 的病）
        var withPoisonedTable = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LoginIssueCodes.DISABLED] = "演示",
        };
        Assert.Contains(
            ClosureViolations(declared, declared, withPoisonedTable),
            v => v.Contains("过期", StringComparison.Ordinal));

        // ④ 表里记了一个根本没声明的码 ⇒ 抄写漂移
        var drifted = new Dictionary<string, string>(StringComparer.Ordinal) { ["NOT_A_REAL_CODE"] = "演示" };
        Assert.Contains(
            ClosureViolations(declared, declared, drifted),
            v => v.Contains("抄写漂移", StringComparison.Ordinal));

        // ── NC4 · 产出方（合成语料，**目标文件必须在**）──
        static (string Relative, string Text)[] Corpus(string body)
            => new[] { (Producer, body) };

        // ① 退回「string? + 五档都 return null」⇒ 签名 / 旧成功返回 / 工厂计数都被点名
        var regressed = ProducerViolations(Corpus("""
            public string? Login(string username, string password, string? source = null)
            {
                var user = GetUserByUsername(username);
                if (user == null) return null;
                var session = new UserSession();
                return session.Id;
            }
            """));
        Assert.Contains(regressed, v => v.Contains("public LoginResult Login(", StringComparison.Ordinal));
        Assert.Contains(regressed, v => v.Contains("return session.Id;", StringComparison.Ordinal));
        Assert.Contains(regressed, v => v.Contains("LoginResult.Failed(", StringComparison.Ordinal));

        // ② 五档齐、但**锁定那个数字没算**（送常量 30）⇒ 点名"数字没有来源"
        var constMinutes = ProducerViolations(Corpus(CompliantProducer.Replace(
            "LoginIssue.Locked(RemainingLockMinutes(user))", "LoginIssue.Locked(30)", StringComparison.Ordinal)));
        Assert.Contains(constMinutes, v => v.Contains("RemainingLockMinutes", StringComparison.Ordinal));

        // ③ 把「用户不存在」拆成再发一次 `BadCredentials` ⇒ 计数 2 → 3，点名分派口径
        var threeWays = ProducerViolations(Corpus(CompliantProducer.Replace(
            "LoginIssue.BadCredentials());\n    if (user.IsLocked)",
            "LoginIssue.BadCredentials());\n    if (false) return LoginResult.Failed(LoginIssue.BadCredentials());\n    if (user.IsLocked)",
            StringComparison.Ordinal)));
        Assert.Contains(threeWays, v => v.Contains("BadCredentials", StringComparison.Ordinal));

        // ④ 语料里缺这个文件 ⇒ 必须报"找不到"（扫描面失效时会静默全绿）
        Assert.Contains(
            ProducerViolations(Array.Empty<(string, string)>()),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ⑤ 注释里写的反例不算（规则不许被自己的文档满足，登记 #48）
        AssertClean(ProducerViolations(Corpus(CompliantProducer
            + "\n// 反例（不许这么写）：return session.Id; 以及 LoginResult.Ok(x); LoginResult.Failed(y);")),
            "NC4⑤ 注释里的反例被当成了真实代码（剥注释失效）");

        // ── NC5 · 站点接线（合成语料）──
        // ① 失败档没接（旧形状：写死 login.err）⇒ 两条都点名
        var unwired = SiteViolations(new[] { (Sites[0],
            "var sessionId = _authService.Login(u, p);\n"
            + "if (sessionId != null) { SessionId = sessionId; Close(); }\n"
            + "else { ShowError(AppLocalization.Service[\"login.err\"]); }") });
        Assert.Contains(unwired, v => v.Contains("出现 0 次", StringComparison.Ordinal));
        Assert.Contains(unwired, v => v.Contains("login.err", StringComparison.Ordinal));

        // ② 接了但**没走码化出口**（自己读 issue.Message）⇒ 点名出口 0 次
        Assert.Contains(
            SiteViolations(new[] { (Sites[0],
                "if (result.Issue is { } issue) { ShowError(issue.Message); }") }),
            v => v.Contains("出现 0 次", StringComparison.Ordinal));

        // ③ 语料里缺这个文件 ⇒ 必须报"找不到"
        Assert.Contains(
            SiteViolations(Array.Empty<(string, string)>()),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ── NC6 · 真语料对照（**故意留在 NC0 之外**：它照的是真仓库，真仓库坏了它就该红；
        //      那时它与主判据一起红，不该被误读成"注入生效"）──
        AssertClean(SiteViolations(AppSourceScanner.ScanAllProductSources()),
            "NC6 真语料对照破了（真仓库的站点接线）");
    }
}
