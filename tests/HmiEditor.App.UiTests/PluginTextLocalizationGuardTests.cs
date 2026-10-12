using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hmi.Communication.Builtin;
using Hmi.Communication.Contracts;
using Hmi.Hardware.Builtin;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 插件程序集的**用户可见文案**守卫（P0-5，2026-09-18；登记表 §4 #27d 后半）。
///
/// <para><b>为什么单开一个守卫：</b>App 层的硬编码中文已清零并由
/// <c>HardcodedLocalizationGuardTests</c> 锁死，但那条守卫**只扫 <c>src/HmiEditor.App</c>**；
/// 而插件程序集（<c>Hmi.*.Builtin</c> / <c>Hmi.*.Loader</c>）里的文案同样会显示在界面上
/// —— 驱动名/描述/配置字段标签/校验结果都走"App 按约定推导键 → 查字典 → 查不到回落插件原文"。
/// 插件不在扫描面内时，新增一条插件文案不会有任何东西提醒你。</para>
///
/// <para><b>两条互补判据：</b></para>
/// <list type="number">
///   <item><b>键覆盖（零容忍）</b>：插件会显示的每一处文案，其推导键都必须在**两本字典**里能解析
///   —— 这条直接抓"算出来的键在字典里没有"（回落插件中文 = 英文界面显示中文）。</item>
///   <item><b>文本基线（只减不增）</b>：插件源码里剩下的硬编码中文按文件计数锁定，
///   涨了报"新增了插件文案"、降了报"必须同步改小基线"。</item>
/// </list>
///
/// <para>推导规则本身住在 <see cref="PluginTextKeys"/>（App 侧唯一来源），
/// 本守卫调它而不是抄一份 —— 抄一份必然与运行时漂移，而漂移的表现正是"守卫绿着、界面却是中文"。</para>
/// </summary>
public class PluginTextLocalizationGuardTests
{
    // ──────────────────────────────────────────
    //  键覆盖（零容忍）
    // ──────────────────────────────────────────

    /// <summary>插件会显示、且经 App 查字典的键（用**真实插件对象**推导，不用正则）。</summary>
    internal static List<(string Kind, string Key)> DerivedKeys()
    {
        var keys = new List<(string, string)>();

        // ① 通讯驱动：显示名 + 描述 + 每个配置字段的标签
        foreach (var driver in BuiltinDriverCatalog.CreateAll())
        {
            var id = driver.Metadata.Id;
            keys.Add(("driver-name", PluginTextKeys.DriverNameKey(id)));
            keys.Add(("driver-description", PluginTextKeys.DriverDescriptionKey(id)));
            foreach (var field in driver.GetConnectionConfigTemplate().Fields)
                keys.Add(("param", PluginTextKeys.FieldLabelKey(field.Key)));
        }

        // ② 硬件目标校验结果：真的把两种规则**触发出来**再收 Code（不是抄源码里的字面量）
        foreach (var target in new IHmiHardwareTargetProbe[] { new T113Probe(), new Rk3506Probe() })
        foreach (var code in target.TriggeredIssueCodes())
            keys.Add(("hw-issue", PluginTextKeys.IssueKey(code)));

        // ③ 通讯驱动校验结果（阶段 104，登记 #61x）：同样**真触发**，但比硬件族更进一步 ——
        //    本族的接口连 `Args` 一起收上来，占位符个数判据因此与产出方**同源**（见下面那条）。
        foreach (var code in TriggeredCommIssueCodes())
            keys.Add(("comm-issue", PluginTextKeys.CommIssueKey(code)));

        return keys;
    }

    [Fact]
    public void EveryPluginDerivedKey_ResolvesInBothLanguages()
    {
        var zh = LoadDictionary("zh-CN");
        var en = LoadDictionary("en-US");

        var missing = DerivedKeys()
            .Where(k => !zh.ContainsKey(k.Key) || !en.ContainsKey(k.Key))
            .Select(k => $"[{k.Kind}] {k.Key}"
                         + (zh.ContainsKey(k.Key) ? "" : "  缺 zh-CN")
                         + (en.ContainsKey(k.Key) ? "" : "  缺 en-US"))
            .Distinct()
            .ToList();

        Assert.True(
            missing.Count == 0,
            "以下插件文案的推导键在字典里查不到 —— 运行时会**回落插件里的中文原文**"
            + "（英文界面显示中文）。补两本字典的键即可：\n" + string.Join("\n", missing));
    }

    [Fact]
    public void KeyDerivation_SeesTheRealPluginSurface()
    {
        // 自检：推导面必须真的覆盖到四类（防枚举失效后退化成空跑）。
        var keys = DerivedKeys();
        Assert.True(keys.Count(k => k.Kind == "driver-name") >= 4, "驱动名键 < 4");
        Assert.True(keys.Count(k => k.Kind == "driver-description") >= 4, "驱动描述键 < 4");
        Assert.True(keys.Count(k => k.Kind == "param") >= 15, $"配置字段键只有 {keys.Count(k => k.Kind == "param")}");
        Assert.True(keys.Count(k => k.Kind == "hw-issue") >= 2, "硬件校验 Code < 2");
        Assert.True(keys.Count(k => k.Kind == "comm-issue") >= 9,
            $"通讯校验 Code 只有 {keys.Count(k => k.Kind == "comm-issue")} 条（应有 9）"
            + " —— 触发面漏了规则，或某个驱动没被探针覆盖");
        // 参数推导本身的自检（timeoutMs → timeout.ms）
        Assert.Equal("timeout.ms", PluginTextKeys.CamelToDotted("timeoutMs"));
        Assert.Equal("baud", PluginTextKeys.CamelToDotted("baud"));
        Assert.Equal("node.id.format", PluginTextKeys.CamelToDotted("nodeIdFormat"));
    }

    /// <summary>
    /// **硬件族**带占位符的校验模板必须与插件实际传的 <c>Args</c> 个数一致。
    /// （通讯族是**同族第二例**，走下面那条**同源**判据 —— 两个族刻意不同形，别合并。）
    ///
    /// <para>为什么要有这条：<c>Format</c> 在占位符与实参对不上时**不抛异常**，
    /// 只原样返回未格式化文本 —— 用户会在界面上看到 <c>{1}</c> 这种字面量。
    /// 编译器看不见、运行时不抛，只有钉住才能发现。（"对不上"的定义与运行期同源，
    /// 见 <see cref="LocalizationPlaceholders"/>；索引有洞那种连"原样返回"都兜不住，
    /// 会直接抛 <c>FormatException</c>。）</para>
    /// </summary>
    [Fact]
    public void IssueTemplates_PlaceholderCountsMatchPluginArgs()
    {
        // 显式"决定表"：新增带数据的规则时必须在这里登记占位符个数。
        var expected = new (string Code, int Placeholders)[]
        {
            ("RESOLUTION_EXCEEDS_DEFAULT", 3),   // 画面名 + 宽 + 高
            ("LVGL_VERSION_MISMATCH", 0),        // 无占位符（模板自带全部信息）
        };

        var zh = LoadDictionary("zh-CN");
        var en = LoadDictionary("en-US");
        foreach (var (code, count) in expected)
        {
            var key = PluginTextKeys.IssueKey(code);
            foreach (var (lang, dict) in new[] { ("zh-CN", zh), ("en-US", en) })
            {
                Assert.True(dict.ContainsKey(key), $"{lang} 缺键 {key}");
                // 判据与运行期**同源**（LocalizationPlaceholders）：这里自己数一遍"出现次数"就是
                // 阶段 43 修掉的那个缺陷 —— 守卫按一套规则、运行期按另一套，守卫绿着界面却是花括号。
                Assert.True(LocalizationPlaceholders.MatchesArgumentCount(dict[key], count),
                    $"{lang} {key} 的模板占位符 {LocalizationPlaceholders.DescribeIndexes(dict[key])} "
                    + $"与登记值 {count} 个实参对不上（不等会让用户看到 {{0}} 字面量，索引有洞还会抛 FormatException）");
            }
        }
    }

    // ──────────────────────────────────────────
    //  通讯族校验码（阶段 104，登记 #61x）
    // ──────────────────────────────────────────

    /// <summary>
    /// 通讯族**已登记**的码（码 → 实参个数）。
    ///
    /// <para>为什么是一张显式表、而不是去反射驱动源码里的字符串字面量：本族的码住在
    /// **插件程序集**（<c>Hmi.Communication.Builtin</c>），App 侧拿不到它们的声明面 ——
    /// 只能**真触发**一遍校验规则把码收上来（与硬件族的 <c>T113Probe</c> / <c>Rk3506Probe</c>
    /// 同一个做法）。而"触发面"天生会漏（换个人写探针就可能漏掉一条规则）
    /// ⇒ 用这张表**双向**夹住它：驱动里多出一个码而没登记 ⇒ 红；登记表里留着一个再也产不出的
    /// 码 ⇒ 也红。⚠ 这是同族**第三态为空**的情形 —— 9 条全部产得出，所以没有
    /// <c>NotProducible</c> 那一栏（真出现了就得补上，别把死码合法化）。</para>
    /// </summary>
    internal static readonly (string Code, int ArgCount)[] CommIssueRegistry =
    {
        ("MISSING_HOST", 0),
        ("INVALID_PORT", 0),
        ("MISSING_ENDPOINT", 0),
        ("INVALID_ENDPOINT", 0),
        ("MISSING_BROKER", 0),
        ("MISSING_CLIENT_ID", 0),
        ("MISSING_PORT", 0),
        ("INVALID_SLAVE_ID", 0),
        ("UNUSUAL_BAUD", 1),
        // 阶段 283：MQTT TLS 四码（文案里**没有占位符** —— 出问题的取值走 Detail，不进文案）
        ("INVALID_TLS_MODE", 0),
        ("TLS_PSK_INCOMPLETE", 0),
        ("TLS_CA_MISSING", 0),
        ("TLS_PORT_LOOKS_PLAINTEXT", 0),
    };

    /// <summary>
    /// 探针配置：驱动 Id → 一份能踩中它**全部**规则的参数。
    ///
    /// <para>同一个驱动可能有好几条规则，而其中一条要"另一个字段是对的"才轮得到
    /// （<c>opc-ua</c> 的 <c>INVALID_ENDPOINT</c> 只在端点**非空**时才判；
    /// <c>mqtt</c> 的 <c>INVALID_PORT</c> 只在端口**能解析成整数**时才判）
    /// ⇒ 同一驱动出现两次，每次换一份参数把另一支触发出来。</para>
    /// </summary>
    private static readonly (string DriverId, Dictionary<string, string> Parameters)[] CommProbeConfigs =
    {
        // host 缺失（空表）＋ port 越界 ⇒ MISSING_HOST / INVALID_PORT
        ("modbus-tcp", new() { ["port"] = "99999" }),
        // slaveId 缺失 ＋ port 缺失 ＋ baud 非常用 ⇒ INVALID_SLAVE_ID / MISSING_PORT / UNUSUAL_BAUD
        ("modbus-rtu", new() { ["baud"] = "12345" }),
        // 端点缺失（空表）⇒ MISSING_ENDPOINT
        ("opc-ua", new()),
        // 端点存在但格式错 ⇒ 才轮到 INVALID_ENDPOINT
        ("opc-ua", new() { ["endpointUrl"] = "http://not-opc" }),
        // broker / clientId 缺失（空表）⇒ MISSING_BROKER / MISSING_CLIENT_ID
        ("mqtt", new()),
        // broker / clientId 补齐 ＋ port 越界 ⇒ 才轮到 INVALID_PORT
        ("mqtt", new() { ["broker"] = "b", ["clientId"] = "c", ["port"] = "0" }),
        // ── 阶段 283：TLS 四码（每份参数只踩一条，免得互相遮蔽）──
        // 模式取值非法 ⇒ INVALID_TLS_MODE
        ("mqtt", new() { ["broker"] = "b", ["clientId"] = "c", ["tls"] = "ssl" }),
        // psk 只填了身份 ⇒ TLS_PSK_INCOMPLETE
        ("mqtt", new() { ["broker"] = "b", ["clientId"] = "c", ["tls"] = "psk", ["tlsPskId"] = "id" }),
        // ca 没填路径 ⇒ TLS_CA_MISSING
        ("mqtt", new() { ["broker"] = "b", ["clientId"] = "c", ["tls"] = "ca" }),
        // psk 填齐但端口是明文端口 ⇒ TLS_PORT_LOOKS_PLAINTEXT（警告，允许保存）
        ("mqtt", new()
        {
            ["broker"] = "b", ["clientId"] = "c", ["port"] = "1883",
            ["tls"] = "psk", ["tlsPskId"] = "id", ["tlsPskFile"] = "/k",
        }),
    };

    /// <summary>
    /// **真触发**四个内置设计期驱动的校验规则，把 <see cref="ValidationIssue"/> 收上来
    /// （含 <c>Args</c> —— 占位符个数判据要读它）。
    /// </summary>
    internal static List<ValidationIssue> TriggeredCommIssues()
    {
        var byId = new Dictionary<string, ICommunicationDriver>(StringComparer.Ordinal);
        foreach (var driver in BuiltinDriverCatalog.CreateAll())
            byId[driver.Metadata.Id] = driver;

        var issues = new List<ValidationIssue>();
        foreach (var (driverId, parameters) in CommProbeConfigs)
        {
            if (!byId.TryGetValue(driverId, out var driver))
                throw new InvalidOperationException(
                    $"探针里的驱动 Id `{driverId}` 不在 BuiltinDriverCatalog 里 —— 驱动改名了？");
            issues.AddRange(driver.ValidateConnection(new ConnectionConfig(driverId, parameters)).Issues);
        }
        return issues;
    }

    /// <summary>触发面收上来的去重码（排序，确定性）—— 可达性机制 <b>M20</b> 也用它。</summary>
    internal static List<string> TriggeredCommIssueCodes() =>
        TriggeredCommIssues()
            .Select(i => i.Code)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// 登记表与触发面**逐条对应**（双向）—— 这条是 <see cref="CommIssueRegistry"/> 的担保方。
    /// </summary>
    [Fact]
    public void CommIssueRegistry_MatchesWhatTheDriversActuallyProduce()
    {
        var produced = TriggeredCommIssueCodes();
        var declared = CommIssueRegistry.Select(e => e.Code)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        var undeclared = produced.Where(c => !declared.Contains(c, StringComparer.Ordinal)).ToList();
        var neverProduced = declared.Where(c => !produced.Contains(c, StringComparer.Ordinal)).ToList();

        Assert.True(undeclared.Count == 0 && neverProduced.Count == 0,
            "通讯族校验码的登记表与驱动实际产出的码对不上（登记 #61x）。\n"
            + "  · **驱动产得出、表里没有** ⇒ 两本字典多半也没有它的键"
            + "（英文界面显示驱动自带的中文原文）：\n      "
            + string.Join("\n      ", undeclared)
            + "\n  · **表里有、驱动再也产不出** ⇒ 探针漏了规则，或那条规则被删了"
            + "（两种情况都要人来判，别顺手删表）：\n      "
            + string.Join("\n      ", neverProduced));
    }

    /// <summary>
    /// 带占位符的通讯族模板必须与**产出方真的传的**实参个数一致。
    ///
    /// <para>与硬件族那条（<see cref="IssueTemplates_PlaceholderCountsMatchPluginArgs"/>，
    /// 靠一张手写的"决定表"）**刻意不同形**：通讯族的探针连 <see cref="ValidationIssue.Args"/>
    /// 一起收上来 ⇒ 判据与产出方**同源**（硬件族那边只能收 Code）。登记表仍然保留，
    /// 但它担保的是"驱动的码没被漏掉"，不是"占位符个数"。</para>
    ///
    /// <para>为什么必须有这条：<c>Format</c> 在占位符与实参对不上时**不抛异常**，
    /// 只原样返回未格式化文本 —— 用户会在界面上看到 <c>{0}</c> 字面量。</para>
    /// </summary>
    [Fact]
    public void CommIssueTemplates_MatchTheArgumentCountsTheDriversPass()
    {
        var zh = LoadDictionary("zh-CN");
        var en = LoadDictionary("en-US");
        var problems = new List<string>();

        foreach (var group in TriggeredCommIssues().GroupBy(i => i.Code, StringComparer.Ordinal))
        {
            var code = group.Key;
            var entry = CommIssueRegistry.FirstOrDefault(e => e.Code == code);
            if (entry.Code is null)
            {
                problems.Add($"{code}：登记表里没有它（另一条判据会点名）");
                continue;
            }

            var passed = group.Select(i => i.Args?.Count ?? 0).Distinct().OrderBy(n => n).ToList();
            if (passed.Count != 1 || passed[0] != entry.ArgCount)
                problems.Add($"{code}：登记 {entry.ArgCount} 个实参，实测传 {string.Join(" / ", passed)} 个");

            var key = PluginTextKeys.CommIssueKey(code);
            foreach (var (lang, dict) in new[] { ("zh-CN", zh), ("en-US", en) })
            {
                if (!dict.TryGetValue(key, out var template))
                {
                    problems.Add($"{lang} 缺键 {key}");
                    continue;
                }
                if (!LocalizationPlaceholders.MatchesArgumentCount(template, entry.ArgCount))
                    problems.Add($"{lang} {key} 的模板占位符 "
                        + $"{LocalizationPlaceholders.DescribeIndexes(template)}"
                        + $" 与 {entry.ArgCount} 个实参对不上");
            }
        }

        Assert.True(problems.Count == 0,
            "通讯族校验模板的占位符与驱动实际传的实参对不上（登记 #61x）：\n  - "
            + string.Join("\n  - ", problems));
    }

    // ──────────────────────────────────────────
    //  插件族校验文案的**出口面**（阶段 104）
    // ──────────────────────────────────────────

    /// <summary>两个插件族各自的站点与它该调的键推导（出口面的**输入清单**）。</summary>
    internal static readonly (string Relative, string Derivation)[] PluginIssueSites =
    {
        ("Views/ProjectSettingsView.axaml.cs", "PluginTextKeys.IssueKey("),
        ("Views/CommunicationConfigView.axaml.cs", "PluginTextKeys.CommIssueKey("),
    };

    /// <summary>
    /// 出口面取数（**纯函数** —— 负控要复用；语料与站点清单都当参数传入）。
    ///
    /// <para>三条要求：薄封装调**它那一族的**键推导、走**唯一出口**
    /// <c>ExternalText.Resolve</c>、**不许自己拼前缀字面量**。
    /// 第三条最容易犯：前缀是个**没有编译期反馈**的自由变量 —— 视图里手写
    /// <c>"comm.issue."</c> 与 <c>PluginTextKeys.CommIssueKey</c> 算出来的键**看起来一样**，
    /// 但推导规则一分家，守卫按一个查字典、运行时按另一个查，两边都"自洽"，
    /// 红的却谁也不是（本项目在 <c>ExpressionSyntaxKey</c> 上吃过这个亏）。</para>
    /// </summary>
    internal static List<string> OutletViolations(
        IEnumerable<(string Relative, string Text)> corpus,
        IEnumerable<(string Relative, string Derivation)> sites)
    {
        var byName = corpus.ToDictionary(c => c.Relative, c => c.Text, StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var (relative, derivation) in sites)
        {
            if (!byName.TryGetValue(relative, out var raw))
            {
                problems.Add($"{relative}：语料里找不到（扫描面失效？）");
                continue;
            }

            var text = SourceScanner.StripComments(raw);
            if (!text.Contains(derivation, StringComparison.Ordinal))
                problems.Add($"{relative}：薄封装没有调 {derivation} ⇒ 键推导可能被抄成第二份");
            if (!text.Contains("ExternalText.Resolve(", StringComparison.Ordinal))
                problems.Add($"{relative}：没有走唯一出口 ExternalText.Resolve");

            foreach (var prefix in new[] { "\"hw.issue.\"", "\"comm.issue.\"" })
                if (text.Contains(prefix, StringComparison.Ordinal))
                    problems.Add($"{relative}：自己拼了前缀字面量 {prefix} —— 前缀必须由 PluginTextKeys 推导");
        }
        return problems;
    }

    [Fact]
    public void PluginIssueOutlet_IsSingle_AndViewsDoNotSpellThePrefix()
    {
        Assert.True(PluginIssueSites.Length >= 2,
            "出口清单少于此处的族数 —— 这条判据会退化成空跑");

        var problems = OutletViolations(AppSourceScanner.ScanAppSources(), PluginIssueSites);

        Assert.True(problems.Count == 0,
            "插件族校验文案的出口面变了（登记 #61x）：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>合成语料的负控 —— 每支**单独**证明一条规则真的在起作用。</summary>
    [Fact]
    public void OutletNegativeControls_EachBreakIsCaughtByTheRightRule()
    {
        const string Path_ = "Views/Synthetic.axaml.cs";
        var sites = new[] { (Path_, "PluginTextKeys.CommIssueKey(") };

        // NC1：形状正确 ⇒ 零违规（正控 —— 没有它，下面三支可能只是"永远报错"）
        const string Good =
            "static string L(ValidationIssue i) => ExternalText.Resolve("
            + "PluginTextKeys.CommIssueKey(i.Code), i.Message, i.Args);";
        Assert.Empty(OutletViolations(new[] { (Path_, Good) }, sites));

        // NC2：键推导被抄成第二份（直接拼前缀，绕过 PluginTextKeys） ⇒ 命中
        const string NoDerivation =
            "static string L(ValidationIssue i) => ExternalText.Resolve("
            + "\"comm.issue.\" + i.Code.ToLower(), i.Message, i.Args);";
        var byDerivation = OutletViolations(new[] { (Path_, NoDerivation) }, sites);
        Assert.Contains(byDerivation, p => p.Contains("PluginTextKeys.CommIssueKey", StringComparison.Ordinal));

        // NC3：不走唯一出口（自己查字典） ⇒ 命中
        const string NoOutlet =
            "static string L(ValidationIssue i) => "
            + "AppLocalization.Service[PluginTextKeys.CommIssueKey(i.Code)];";
        Assert.Contains(OutletViolations(new[] { (Path_, NoOutlet) }, sites),
            p => p.Contains("ExternalText.Resolve", StringComparison.Ordinal));

        // NC4：违规写法只出现在**注释**里、代码本身是对的 ⇒ 零违规
        //（证明剥注释生效 —— 否则规则会被自己的文档满足：注释里写一句前缀字面量就红了）。
        // ⚠ 这条负控的**语料本体必须是正确的**：把整个文件注释掉**会**报违规，而且**该报**
        //    —— 剥注释之后那份语料确实"什么都没有调"，把它算成通过才是缺陷。
        var goodWithCommentedBreaks =
            Good + "\n// 关键：不许写 \"" + "comm.issue." + "\" 这样的前缀字面量";
        Assert.Empty(OutletViolations(new[] { (Path_, goodWithCommentedBreaks) }, sites));
    }

    // ──────────────────────────────────────────
    //  文本基线（只减不增）
    // ──────────────────────────────────────────

    private static readonly string[] PluginRoots =
    {
        Path.Combine("src", "Hmi.Hardware.Builtin"),
        Path.Combine("src", "Hmi.Communication.Builtin"),
        Path.Combine("src", "Hmi.Hardware.Contracts"),
        Path.Combine("src", "Hmi.Communication.Contracts"),
        Path.Combine("src", "Hmi.Hardware.Loader"),
        Path.Combine("src", "Hmi.Communication.Loader"),
    };

    /// <summary>
    /// 每个插件的硬编码中文条数基线（相等判定：涨=新增文案；降=必须同步改小基线）。
    ///
    /// <para><b>为什么不是 0</b>：这 49 条全部是**插件自带文案**，分三类
    /// （逐条审过，不是"懒得清"）：
    /// <list type="number">
    ///   <item><b>驱动元数据</b>（描述/字段标签，约 40 条）：住在插件程序集里，
    ///   插件拿不到 App 字典。它们的**显示**已由 App 侧查键接管
    ///   （<c>driver.*.description</c> / <c>param.*</c> 键已全量覆盖，见上面的键覆盖用例），
    ///   这里的原文只作**回落**——按契约 <c>ConfigField.Label</c>/<c>Metadata.Description</c>
    ///   非空，删不掉。要把它们也搬走需要改**插件契约**（字段只带 Key 不带 Label），
    ///   属独立议题。</item>
    ///   <item><b>校验回落文案</b>（T113/RK3506 各 1 条）：同上，<c>ValidationIssue.Message</c>
    ///   是契约必填；界面显示的是 <c>hw.issue.*</c> 本地化模板（本轮已补齐，含占位符）。</item>
    ///   <item><b>运维日志 kind 标签</b>（<c>HardwareTargetRegistry</c> 2 条）：只在 <c>ILogger</c>
    ///   消息里当参数用，**非界面文案**，判据按语句判定时抓不到 sink 名 ⇒ 故意多报、锁定。</item>
    /// </list></para>
    ///
    /// <para>基线的价值：**再新增**插件文案会让这条用例变红（这是扫插件程序集的目的），
    /// 而不是假装插件已经清干净。</para>
    /// </summary>
    internal static readonly Dictionary<string, int> Baseline = new(StringComparer.Ordinal)
    {
        { "src/Hmi.Communication.Builtin/ModbusRtuDriver.cs", 12 },
        { "src/Hmi.Communication.Builtin/MqttDriver.cs", 19 },
        { "src/Hmi.Communication.Builtin/ModbusTcpDriver.cs", 8 },
        { "src/Hmi.Communication.Builtin/OpcUaDriver.cs", 8 },
        { "src/Hmi.Communication.Builtin/ModbusRtuRuntimeDriver.cs", 2 },
        { "src/Hmi.Communication.Builtin/ModbusTcpRuntimeDriver.cs", 2 },
        { "src/Hmi.Communication.Builtin/OpcUaRuntimeDriver.cs", 2 },
        { "src/Hmi.Hardware.Loader/HardwareTargetRegistry.cs", 2 },
        { "src/Hmi.Hardware.Builtin/AllwinnerT113Target.cs", 1 },
        { "src/Hmi.Hardware.Builtin/RockchipRK3506Target.cs", 1 },
    };

    [Fact]
    public void PluginAssemblies_HaveNoNewHardcodedChinese()
    {
        var actual = ScanPluginChinese();
        var drift = new List<string>();
        foreach (var (file, count) in actual.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var baseline = Baseline.TryGetValue(file, out var b) ? b : 0;
            if (count != baseline)
                drift.Add($"{file}: 实测 {count} ≠ 基线 {baseline}");
        }
        foreach (var file in Baseline.Keys.Where(f => !actual.ContainsKey(f)))
            drift.Add($"{file}: 基线里登记了但实测 0 条（文件被删或判据失效）");

        Assert.True(drift.Count == 0,
            "插件程序集的硬编码中文与基线不符。涨了 = 新增了插件文案（请走键或改小基线）；"
            + "降了 = 必须同步改小 Baseline（否则守卫变成过期抄写）：\n" + string.Join("\n", drift));
    }

    [Fact]
    public void Scanner_ItselfFindsPluginText()
    {
        // 自检：判据必须真能扫到东西（关掉排除项后应显著更多），否则它是"永远绿"的摆设。
        var withExclusions = ScanPluginChinese().Values.Sum();
        var raw = CountChineseLiterals(StripExclusionsDisabled: true).Values.Sum();
        Assert.True(raw >= withExclusions, "关闭排除后条数不应减少");
        Assert.True(raw >= 20, $"插件扫描面太小（raw={raw}）—— 判据可能失效");
    }

    private static Dictionary<string, int> ScanPluginChinese() => CountChineseLiterals(StripExclusionsDisabled: false);

    private static readonly Regex ChineseInLiteral =
        new("\"((?:[^\"\\\\\n]|\\\\.)*)\"", RegexOptions.Compiled);

    private static readonly Regex HasCjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>
    /// 语句级排除类别（与仓库既有约定一致）：
    /// 异常消息、<c>Debug/Console/Trace</c> 调试输出、<c>ILogger</c> 运维日志、正则模式。
    ///
    /// <para><b>判据按"语句"而不是"按行"</b>：<c>throw new X(\n  "中文")</c> 这类多行实参
    /// 会把文案推到下一行，按行判定必然漏报 —— 这条教训在 App 守卫上踩过一次（见登记表 §12.1）。
    /// 这里从字面量位置往回退到最近的 <c>; { }</c>，拿"语句头"去匹配排除类别。</para>
    ///
    /// <para><b>故意保留的"多报"</b>：<c>Log?.Invoke("…")</c>（运行时驱动日志，可能进设备日志面板）
    /// 与 <c>LoadTargetFiles(files, "JSON 配置")</c>（kind 标签，只在日志里用）都计入基线 ——
    /// 宁可多报，也不让"可能用户可见"的文案漏在扫描面外。</para>
    /// </summary>
    private static readonly Regex ExcludedStatement = new(
        @"throw new|Debug\.|Console\.|Trace\.|DiagnosticLog|Regex"
        + @"|_logger\??\.Log(Information|Warning|Debug|Error|Trace|Critical)",
        RegexOptions.Compiled);

    private static Dictionary<string, int> CountChineseLiterals(bool StripExclusionsDisabled)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var root in PluginRoots)
        {
            var dir = Path.Combine(SourceScanner.RepoRoot, root);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
                         .Where(f => !SourceScanner.IsBuildOutput(f))
                         .OrderBy(f => f, StringComparer.Ordinal))
            {
                var code = SourceScanner.StripComments(File.ReadAllText(file)); // 保长度、保行数
                var count = 0;
                foreach (Match m in ChineseInLiteral.Matches(code))
                {
                    if (!HasCjk.IsMatch(m.Groups[1].Value)) continue;
                    if (!StripExclusionsDisabled && IsExcludedStatement(code, m.Index)) continue;
                    count++;
                }
                if (count > 0) result[SourceScanner.Relative(file)] = count;
            }
        }
        return result;
    }

    /// <summary>字面量所在语句（回退到最近的 <c>; { }</c> 之后）是否命中排除类别。</summary>
    private static bool IsExcludedStatement(string code, int literalStart)
    {
        var start = literalStart;
        while (start > 0 && code[start - 1] != ';' && code[start - 1] != '{' && code[start - 1] != '}')
            start--;
        return ExcludedStatement.IsMatch(code[start..literalStart]);
    }

    // ──────────────────────────────────────────
    //  工具
    // ──────────────────────────────────────────

    /// <summary>
    /// 读一本字典。**internal 而不是 private**：Core 脚本校验的守卫
    /// （<c>ScriptIssueLocalizationGuardTests</c>）要用同一份实现 ——
    /// 两处各写一份加载方式，必然有一份先失效，而失效的表现是
    /// "字典里明明有键、守卫却报缺"，会让人去改字典而不是改判据。
    /// </summary>
    internal static Dictionary<string, string> LoadDictionary(string lang)
    {
        var path = Path.Combine(SourceScanner.RepoRoot,
            "src", "HmiEditor.Core", "Resources", $"Strings.{lang}.json");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
               ?? throw new InvalidOperationException($"无法解析 {path}");
    }

    /// <summary>把"触发某块板的校验规则"收成可枚举的小接口（避免测试里抄规则名）。</summary>
    private interface IHmiHardwareTargetProbe
    {
        IReadOnlyList<string> TriggeredIssueCodes();
    }

    private sealed class T113Probe : IHmiHardwareTargetProbe
    {
        public IReadOnlyList<string> TriggeredIssueCodes()
        {
            var target = new AllwinnerT113Target();
            var project = new HmiProject { LvglVersion = "8.3" }; // 与板载 9.5 主版本不同 → Error
            project.Screens.Add(new HmiScreen { Name = "Big", Width = 4096, Height = 4096 });
            return target.ValidateProject(project).Issues.Select(i => i.Code).Distinct().ToList();
        }
    }

    private sealed class Rk3506Probe : IHmiHardwareTargetProbe
    {
        public IReadOnlyList<string> TriggeredIssueCodes()
        {
            var target = new RockchipRK3506Target();
            var project = new HmiProject();
            project.Screens.Add(new HmiScreen { Name = "Big", Width = 4096, Height = 4096 });
            return target.ValidateProject(project).Issues.Select(i => i.Code).Distinct().ToList();
        }
    }
}