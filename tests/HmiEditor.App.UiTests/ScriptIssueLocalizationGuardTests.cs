using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HmiEditor.App.Services;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// Core 侧**脚本校验文案**的本地化守卫。
///
/// <para><b>为什么单开一条</b>：App 层的硬编码中文由 <c>HardcodedLocalizationGuardTests</c> 锁死，
/// 但那条守卫的扫描面是 <c>src/HmiEditor.App</c> —— <c>HmiEditor.Core</c> 里产出的中文
/// 完全在它视野之外。而 Core 的脚本校验文案**会直接显示给用户**（脚本编辑器校验状态栏那一行
/// 加 ToolTip），英文界面下就成了"本地化的前缀 + 中文正文"。没有这条守卫，
/// 新增一条 Core 文案不会有任何东西提醒你。</para>
///
/// <para><b>三条互补判据</b>，输入全部来自<b>真实触发</b>的结果（不抄字面量清单）：</para>
/// <list type="number">
///   <item><b>键覆盖（零容忍）</b>：每个真实产生的 <c>Code</c>，其键
///   <c>script.issue.&lt;code 小写&gt;</c> 都必须在**两本字典**里能解析 ——
///   查不到就会回落 Core 的中文原文，也就是"英文界面显示中文"；</item>
///   <item><b>占位符个数 == 实参个数</b>：<c>Format</c> 在两者不符时**不抛异常**、只原样返回
///   —— 用户会在状态栏看到 <c>{1}</c> 这种字面量。编译器看不见、运行时不抛，只有钉住才发现；</item>
///   <item><b>zh 模板渲染 == Core 回落原文</b>：这是"两份文案同步"的**自动**判据
///   （对照 <c>hw.issue</c> 那边手写的"占位符决定表"—— 手写清单会漂移，而漂移了它自己不会说）。</item>
/// </list>
///
/// <para><b>还有一条自检</b> <see cref="Probe_CoversEveryDeclaredCode"/>：反射枚举 Core 里
/// 声明过的所有 <c>LUA_SYNTAX_*</c> / <c>LUA_RUNTIME_SYMBOL_*</c> 常量，断言每个都能被真实触发。
/// 触发不出来的 Code 是**死代码** —— 它和它的字典键都永远没人会用，而"看起来有覆盖"
/// 会让人以为测到了。</para>
/// </summary>
public class ScriptIssueLocalizationGuardTests
{
    // ──────────────────────────────────────────
    //  触发面
    // ──────────────────────────────────────────

    /// <summary>
    /// 跑一遍真实路径，收集所有会产生的 <see cref="ScriptError"/>。
    ///
    /// <para>刻意**不写"预期 Code"**：那等于把实现抄一遍，而抄出来的清单一旦与实现分家，
    /// 守卫检查的就是清单而不是代码了。覆盖完整性交给
    /// <see cref="Probe_CoversEveryDeclaredCode"/> 兜底。</para>
    /// </summary>
    internal static List<ScriptError> Probe()
    {
        var engine = new ScriptEngineService();
        var all = new List<ScriptError>();

        // ① 模型级判据
        all.AddRange(engine.Validate(new HmiScript { Name = "", Code = "function f() end" }).Errors);
        all.AddRange(engine.Validate(new HmiScript { Name = "S", Code = "" }).Errors);
        all.AddRange(engine.Validate(new HmiScript { Name = "S", Code = "local a = 1" }).Errors);

        // ② 语法族
        foreach (var snippet in SyntaxSnippets())
            all.AddRange(engine.Validate(new HmiScript { Name = "S", Code = snippet }).Errors);

        // ③ 运行前检查 —— 未定态（设备目标的真实状态，经由引擎产生）
        all.AddRange(engine.Validate(new HmiScript
        {
            Name = "S",
            Code = "function on_click() local a = bit32.band(1, 3) end",
        }).Warnings);

        // ④ 运行前检查 —— 已定态（目标 = SDK 自带的 5.1）。这一态在设备目标确定之前
        //    不会经由 Validate 产生（LuaDialect.Target 恒为 Unknown），但它**必须**有字典键
        //    —— 目标一旦定下来就会用上。所以这里直接调判定函数取，而不是等它自己出现。
        foreach (var h in LuaDialect.CheckRuntimeSymbols("local a = bit32.band(1, 3)", LuaVersion.Lua51))
        {
            var (code, args) = LuaDialect.DescribeHit(h, LuaVersion.Lua51);
            all.Add(new ScriptError
            {
                Line = h.Line,
                Code = code,
                Message = LuaDialect.ExplainHit(h, LuaVersion.Lua51),
                Args = args,
            });
        }

        return all;
    }

    /// <summary>语法族样本（每条针对一类解析器报错；覆盖完整性由自检兜底）。</summary>
    private static IEnumerable<string> SyntaxSnippets() => new[]
    {
        // 方言差异（编辑器基准 5.2 拒、设备上可能能跑 —— 已知误报）
        "function f() local a = 7 // 2 end",
        "function f() local a = 1 & 3 end",
        "function f() local a = 1 | 3 end",
        "function f() local a = ~1 end",
        "function f() local a = 8 >> 2 end",
        "function f() local x <const> = 1 end",
        // 确实写漏了
        "function f()\n  if true then\n    local a = 1\n  end\n",
        "function f()\n  local s = \"abc\nend",
        "function f() local s = [[abc end",
        "function f() local a end",
        "function f(1) end",
        "function f() local a = 1 + end",
        // 下面三条专门构造"报错点**不在文件末尾**"的 expected 类报错：
        // 落在末尾的会带 '<eof>'，从而被 MISSING_END 那条规则先接走 ——
        // 少了这一组，MISSING_EQUALS / MISSING_NAME / EXPECTED 三个 Code 永远触发不到。
        "function f() local a b = 1 end",
        "function f() local = 1 end",
        "function f() if true do end end",
        // 其他符号 / 兜底
        "function f() local a = 1 $ 2 end",
        "function f() local s = \"\\q\" end",
    };

    // ──────────────────────────────────────────
    //  豁免：当前解析器产生不了的 Code
    // ──────────────────────────────────────────

    /// <summary>
    /// 当前解析器（MoonSharp 2.0.0）**产生不了**的 Code，以及它们**预期的模板实参个数**。
    ///
    /// <para><b>为什么留着它们</b>：<c>'=' expected</c> / <c>'&lt;name&gt;' expected</c> 这类是
    /// **Lua 参考实现的标准报错文本**（<c>lparser.c</c> 就是这么报的），但 MoonSharp 把这几种情况
    /// 全归成了 <c>unexpected symbol near 'x'</c> —— 实测（本文件跑出来的）：
    /// <c>local a b = 1</c> / <c>local = 1</c> / <c>if true do end</c> 都报 unexpected symbol。
    /// 一旦换解析器或升版本，它们就会出现；届时若没有对应分支，用户看到的是兜底文案。</para>
    ///
    /// <para><b>但"豁免"不等于"可以不守"</b>：</para>
    /// <list type="bullet">
    ///   <item>字典键照样要有（键覆盖把豁免的 Code 也算进去）—— 换解析器那天立刻要用，
    ///   那时候才补键就晚了；</item>
    ///   <item>模板占位符个数照样要对，所以这里必须连**实参个数**一起登记；</item>
    ///   <item>这份表是**双向**的：一旦某个 Code 真能触发了，它必须从这里搬走，
    ///   否则 <see cref="Probe_CoversEveryDeclaredCode"/> 会红。</item>
    /// </list>
    ///
    /// <para><b>这正是 <c>'&lt;eos&gt;'</c> 那次的教训</b>：一个永不命中的分支可以带着它的字典键
    /// "看起来有覆盖"地活很久。所以豁免必须**显式写在纸上**，而不是让"没触发"这件事悄悄过去。</para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> UnreachableOnCurrentParser =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [LuaSyntaxValidator.SyntaxMissingEqualsCode] = 0,   // 缺少赋值符号 '='
            [LuaSyntaxValidator.SyntaxMissingNameCode] = 0,     // 缺少名称（变量名或函数名）
            [LuaSyntaxValidator.SyntaxExpectedCode] = 1,        // 缺少必要的语法元素：{0}
        };

    /// <summary>需要守卫的**全部** Code：真实触发到的 + 豁免的。</summary>
    private static List<string> GuardedCodes()
        => Probe().Select(e => e.Code).Concat(UnreachableOnCurrentParser.Keys).ToList();

    // ──────────────────────────────────────────
    //  三条判据
    // ──────────────────────────────────────────

    [Fact]
    public void EveryTriggeredCode_ResolvesInBothLanguages()
    {
        // 输入是 GuardedCodes（触发的 + 豁免的）而不是 Probe：豁免的 Code 换解析器就会出现，
        // 字典键必须**提前就位**。
        var missing = MissingKeys(GuardedCodes(), LoadDictionary("zh-CN"), LoadDictionary("en-US"));

        Assert.True(missing.Count == 0,
            "以下脚本校验 Code 的键在字典里查不到 —— 运行时会**回落 Core 的中文原文**"
            + "（英文界面显示中文）。补两本字典的键即可：\n" + string.Join("\n", missing));
    }

    [Fact]
    public void PlaceholderCount_MatchesArgsCount()
    {
        var drift = PlaceholderDrift(Probe(), LoadDictionary("zh-CN"), LoadDictionary("en-US"));

        Assert.True(drift.Count == 0,
            "字典模板的占位符个数与 Core 传的 Args 个数不符 —— Format 不抛异常、只原样返回，"
            + "用户会在状态栏看到 {1} 这种字面量：\n" + string.Join("\n", drift));
    }

    [Fact]
    public void ChineseTemplate_RendersToTheCoreFallback()
    {
        var drift = FallbackDrift(Probe(), LoadDictionary("zh-CN"));

        Assert.True(drift.Count == 0,
            "zh 字典模板渲染后与 Core 的回落原文不一致 —— 这两份必须逐字相同"
            + "（界面显示字典那份、字典缺键时显示回落那份，不同就等于同一句话有两个版本）：\n"
            + string.Join("\n", drift));
    }

    // ──────────────────────────────────────────
    //  自检：声明过的 Code 必须都能被触发
    // ──────────────────────────────────────────

    [Fact]
    public void Probe_CoversEveryDeclaredCode()
    {
        var triggered = Probe().Select(e => e.Code).ToHashSet(StringComparer.Ordinal);

        var declared = DeclaredCodes(typeof(LuaSyntaxValidator), LuaSyntaxValidator.SyntaxErrorCodePrefix)
            .Concat(DeclaredCodes(typeof(LuaDialect), LuaDialect.RuntimeSymbolWarningCodePrefix))
            .ToList();

        Assert.True(declared.Count >= 14,
            $"反射只枚举到 {declared.Count} 个 Code —— 判据的取数方式可能失效了");

        // ① 声明了、触发不出来、又**不在豁免表**里 ⇒ 要么样本漏了，要么是死代码。
        var unexpected = declared.Where(c => !triggered.Contains(c)
                                             && !UnreachableOnCurrentParser.ContainsKey(c))
                                 .OrderBy(c => c, StringComparer.Ordinal)
                                 .ToList();

        Assert.True(unexpected.Count == 0,
            "以下 Code 声明了却触发不出来 —— 要么样本没覆盖到它，要么它是**死代码**"
            + "（它和它的字典键都永远不会被用到，而'看起来有覆盖'会让人以为测到了）。"
            + "两种情况都要处置：补样本，或者**显式登记进 UnreachableOnCurrentParser**：\n"
            + string.Join("\n", unexpected)
            // 失败时把实际触发到的列出来：Code 是按解析器报错文本分类的，光看声明看不出
            // 该写什么样本（这张表就是被它教出来的）。
            + "\n\n实际触发到的（Code | 回落文案）：\n"
            + string.Join("\n", Probe().Select(e => $"  {e.Code}  |  {e.Message}")));

        // ② 反向：豁免表里却**能**触发的 ⇒ 豁免已过期，必须搬走。
        //    没有这一半，豁免表会变成一张只增不减的"免死金牌"，死代码就藏在里面。
        var stale = UnreachableOnCurrentParser.Keys
            .Where(triggered.Contains)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        Assert.True(stale.Count == 0,
            "以下 Code 已被登记为\"当前解析器产生不了\"，但它**能**触发 —— 豁免过期了，"
            + "请把它从 UnreachableOnCurrentParser 里移走（并确认样本已覆盖它）：\n"
            + string.Join("\n", stale));
    }

    /// <summary>
    /// 豁免的 Code 也要守模板：字典键在不在、占位符个数对不对。
    ///
    /// <para>它们无法真触发，所以实参个数只能**手写登记** —— 这是本文件里唯一的手写数据，
    /// 而它被上面那条双向判据看住（一旦能触发就得搬走），所以不会退化成恒绿的摆设。</para>
    /// </summary>
    [Fact]
    public void UnreachableCodes_StillHaveWellFormedTemplates()
    {
        var zh = LoadDictionary("zh-CN");
        var en = LoadDictionary("en-US");

        var drift = new List<string>();
        foreach (var (code, expected) in UnreachableOnCurrentParser)
        {
            var key = ExternalText.ScriptIssueKey(code);
            foreach (var (lang, dict) in new[] { ("zh-CN", zh), ("en-US", en) })
            {
                if (!dict.TryGetValue(key, out var template)) { drift.Add($"{lang} {key}: 缺键"); continue; }
                if (!PlaceholdersMatch(template, expected))
                    drift.Add($"{lang} {key}: 模板占位符与登记的 {expected} 个实参对不上");
            }
        }

        Assert.True(drift.Count == 0,
            "豁免的 Code 模板有问题 —— 它们平时不出现在界面上，所以只有这条判据能发现：\n"
            + string.Join("\n", drift));
    }

    /// <summary>
    /// 反射枚举某个类型里声明的、以 <paramref name="prefix"/> 开头的 <c>const string</c>。
    ///
    /// <para><b>为什么不手写清单</b>：手写清单与实现会分家，而分家之后守卫检查的是**清单**、
    /// 不是代码 —— 那是"被自己的清单喂活"的恒绿判据（本项目登记过这一族失效模式）。</para>
    /// </summary>
    private static IEnumerable<string> DeclaredCodes(Type type, string prefix)
        => type.GetFields(BindingFlags.Public | BindingFlags.Static)
               .Where(f => f.IsLiteral && f.FieldType == typeof(string))
               .Select(f => (string)f.GetRawConstantValue()!)
               .Where(v => v is not null
                           && v.StartsWith(prefix, StringComparison.Ordinal)
                           && v.Length > prefix.Length)   // 前缀常量本身不是 Code
               .Distinct(StringComparer.Ordinal);

    // ──────────────────────────────────────────
    //  判据实现（抽成纯函数 —— 负控要复用，不能只在测试体里内联）
    // ──────────────────────────────────────────

    /// <summary>
    /// 读字典 —— 实现在 <see cref="PluginTextLocalizationGuardTests"/>（同一条本地化守卫族的另一半）。
    /// 这里只做转发，**不另写一份**：两处各读一次盘，迟早有一处先失效（比如换了文件位置只改了一边）。
    /// </summary>
    private static Dictionary<string, string> LoadDictionary(string lang)
        => PluginTextLocalizationGuardTests.LoadDictionary(lang);

    private static List<string> MissingKeys(
        IEnumerable<string> codes,
        Dictionary<string, string> zh,
        Dictionary<string, string> en)
        => codes
            .Select(ExternalText.ScriptIssueKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .Where(k => !zh.ContainsKey(k) || !en.ContainsKey(k))
            .Select(k => k + (zh.ContainsKey(k) ? "" : "  缺 zh-CN") + (en.ContainsKey(k) ? "" : "  缺 en-US"))
            .ToList();

    private static List<string> PlaceholderDrift(
        IReadOnlyList<ScriptError> errors,
        Dictionary<string, string> zh,
        Dictionary<string, string> en)
    {
        var drift = new List<string>();
        foreach (var e in errors)
        {
            var key = ExternalText.ScriptIssueKey(e.Code);
            var expected = e.Args?.Count ?? 0;

            foreach (var (lang, dict) in new[] { ("zh-CN", zh), ("en-US", en) })
            {
                if (!dict.TryGetValue(key, out var template)) { drift.Add($"{lang} {key}: 缺键"); continue; }
                if (!PlaceholdersMatch(template, expected))
                {
                    drift.Add($"{lang} {key}: 模板 {LocalizationPlaceholders.DescribeIndexes(template)} "
                              + $"与 Core 传的 {expected} 个实参对不上");
                }
            }
        }
        return drift.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// 模板与实参个数的判据**不在这里** —— 它住在
    /// <see cref="LocalizationPlaceholders.MatchesArgumentCount"/>（运行期 <c>Format</c> 用的就是它）。
    ///
    /// <para><b>为什么必须同源</b>：本文件曾自带一份实现，与运行期的口径**不一致**
    /// （守卫按"不同索引 + 恰好 0..n-1"，运行期按"占位符出现次数"）⇒ 判据全绿而界面显示
    /// <c>{1}</c> 字面量。<b>守卫与被守的对象用两套规则，等于没守。</b>所以这里只调用、不复制。</para>
    /// </summary>
    private static bool PlaceholdersMatch(string template, int argCount)
        => LocalizationPlaceholders.MatchesArgumentCount(template, argCount);

    private static List<string> FallbackDrift(
        IReadOnlyList<ScriptError> errors,
        Dictionary<string, string> zh)
    {
        var drift = new List<string>();
        foreach (var e in errors)
        {
            var key = ExternalText.ScriptIssueKey(e.Code);
            if (!zh.TryGetValue(key, out var template)) { drift.Add($"{key}: 缺键"); continue; }

            var rendered = e.Args is { Count: > 0 } a ? string.Format(template, a.ToArray()) : template;
            if (rendered != e.Message)
                drift.Add($"{key}\n    字典渲染: {rendered}\n    Core 回落: {e.Message}");
        }
        return drift.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    // ──────────────────────────────────────────
    //  负控（每组都与正控同场）
    // ──────────────────────────────────────────

    /// <summary>
    /// 从字典里**删掉**一个真实用到的键 ⇒ 键覆盖判据必须点名它。
    /// <para>正控同场（真字典下三条判据都空）—— 否则"报出来了"可能只是判据恒真。</para>
    /// </summary>
    [Fact]
    public void NegativeControl_RemovingAKey_IsCaught()
    {
        var errors = Probe();
        var codes = errors.Select(e => e.Code).Concat(UnreachableOnCurrentParser.Keys).ToList();
        var zh = LoadDictionary("zh-CN");
        var en = LoadDictionary("en-US");

        Assert.Empty(MissingKeys(codes, zh, en));               // 正控
        Assert.Empty(PlaceholderDrift(errors, zh, en));         // 正控
        Assert.Empty(FallbackDrift(errors, zh));                // 正控

        var victim = ExternalText.ScriptIssueKey(errors[0].Code);
        Assert.True(zh.ContainsKey(victim), $"探针结果里应当有 {victim}");
        zh.Remove(victim);

        // MissingKeys 返回的是带"缺 zh-CN"后缀的**描述串**，所以按包含匹配、不是等值
        Assert.Contains(MissingKeys(codes, zh, en), d => d.Contains(victim, StringComparison.Ordinal));
    }

    /// <summary>
    /// 模板改歪（多一个占位符 / 少一个字符）⇒ 分别由占位符判据与一致性判据点名。
    /// <para>这两种破坏**键覆盖判据都看不见**（键还在），所以必须有另外两条 ——
    /// 这正是"一条判据只能守一件事"的实例。</para>
    /// </summary>
    [Fact]
    public void NegativeControl_BreakingATemplate_IsCaught()
    {
        var errors = Probe();
        var zh = LoadDictionary("zh-CN");
        var en = LoadDictionary("en-US");

        Assert.Empty(PlaceholderDrift(errors, zh, en));         // 正控
        Assert.Empty(FallbackDrift(errors, zh));                // 正控

        // 挑一条**带占位符**的：只有它才能暴露"占位符个数"这一类破坏
        var sample = errors.First(e => e.Args is { Count: > 0 });
        var key = ExternalText.ScriptIssueKey(sample.Code);
        var backup = zh[key];

        // ① 多一个占位符 ⇒ 键还在、文字还在，只有占位符判据抓得到
        zh[key] = backup + " {9}";
        Assert.Contains(PlaceholderDrift(errors, zh, en), d => d.Contains(key));
        Assert.Empty(MissingKeys(errors.Select(e => e.Code), zh, en));   // 键覆盖确实看不见它

        // ② 文案改歪 ⇒ 只有一致性判据抓得到
        zh[key] = backup + "（改歪）";
        Assert.Contains(FallbackDrift(errors, zh), d => d.Contains(key));
    }
}
