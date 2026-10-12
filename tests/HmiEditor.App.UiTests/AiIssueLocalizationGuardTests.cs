using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HmiEditor.AI;
using HmiEditor.AI.Generators;
using HmiEditor.AI.Inference;
using HmiEditor.AI.Models;
using HmiEditor.AI.Services;
using HmiEditor.AI.Validation;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **AI 诊断文案**的本地化守卫。
///
/// <para><b>为什么单开一条</b>：<c>HardcodedLocalizationGuardTests</c> 的扫描面是
/// <c>src/HmiEditor.App</c>，<c>ScriptIssueLocalizationGuardTests</c> 管的是 Core 的脚本校验 ——
/// <c>HmiEditor.AI</c> 产出的文案两边都不管。而那批文案<b>真的会显示给用户</b>，
/// 共三条路径：</para>
/// <list type="bullet">
///   <item><c>AiChatPanelView</c> 的聊天条目 <c>Detail</c>（校验失败时逐条列问题）；</item>
///   <item><c>AiGenerateDialogBase</c> 的状态行（生成失败时取前 5 条）；</item>
///   <item><c>PropertyPanelView</c> 的「AI 建议」列表（<c>LayoutAnalyzer</c> 的布局问题）。</item>
/// </list>
/// <para>在把它们接上字典之前，这三处是「本地化的前缀 + 写死中文的正文」——
/// 英文界面下中英混排，而这<b>不会有任何东西提醒你</b>。</para>
///
/// <para><b>三条机制</b>（与 <c>ScriptIssueLocalizationGuardTests</c> 同形，输入来自<b>真实触发</b>）：</para>
/// <list type="number">
///   <item><b>键覆盖（零容忍）</b>：每个真实产出的 <c>Code</c>，其键
///   <c>ai.issue.&lt;码小写&gt;</c> 必须在**两本字典**里能解析；</item>
///   <item><b>占位符个数 == 实参个数</b>：<c>Format</c> 不符时**不抛异常**、只原样返回
///   ⇒ 用户会看到 <c>{1}</c> 字面量。编译器看不见、运行时不抛；</item>
///   <item><b>zh 模板渲染 == AI 层回落原文</b>：两份文案必须逐字相同
///   （界面显示字典那份、字典缺键时显示回落那份 —— 不同就等于同一句话有两个版本）。</item>
/// </list>
///
/// <para><b>还有一条双向判据</b> <see cref="ProducedCodes_AndDeclaredCodesAreTheSameSet"/>：
/// 反射 <see cref="AiIssueCodes"/> 的声明集合，与真正产出到的集合做**双向差集**。
/// 两个方向各挡一件事 —— 「产出了没声明」挡住<b>字面量码</b>（它能编译、能运行，
/// 但键不会进字典）；「声明了没产出」挡住<b>死码</b>（它和它的字典键都永远不会被用到，
/// 而"看起来有覆盖"会让人以为测到了）。后者正是本项目常量侧踩过的坑（登记 #49）。</para>
///
/// <para><b>为什么全 41 条都能真触发</b>：两条编排码（<c>IMAGE_GENERATOR_MISSING</c> /
/// <c>INTENT_UNSUPPORTED</c>）看起来要连 LLM，其实都在「还没轮到调模型」时就返回了
/// （前者是 <c>_imageGenerator is null</c> 的早退、后者是意图分类落到 <c>Unknown</c>）——
/// 所以只要把三个生成器构造出来即可，断言用的 provider <b>被调用就抛</b>。</para>
/// </summary>
public class AiIssueLocalizationGuardTests
{
    /// <summary>一条真实产出的诊断：码 + 回落原文 + 模板实参。</summary>
    internal sealed record Produced(string Code, string Message, IReadOnlyList<object>? Args);

    // ══════════════════════════════════════════════════════════════════
    //  触发面

    /// <summary>
    /// 跑一遍真实路径，收集所有会产出的诊断。
    ///
    /// <para>刻意**不写"预期码清单"**：那等于把实现抄一遍，抄出来的清单一旦与实现分家，
    /// 守卫检查的就是清单而不是代码。覆盖完整性交给反射那条双向判据兜底。</para>
    /// </summary>
    internal static List<Produced> Probe()
    {
        var all = new List<Produced>();

        void Drain(AiValidationResult r)
            => all.AddRange(r.Issues.Select(i => new Produced(i.Code, i.Message, i.Args)));

        void Layout(HmiScreen s)
            => all.AddRange(LayoutAnalyzer.Analyze(s).Issues.Select(i => new Produced(i.Code, i.Message, i.Args)));

        // ── 屏幕族 ──
        Drain(AiOutputValidator.ValidateScreen(null!, null));
        Drain(AiOutputValidator.ValidateScreen(new HmiScreen { Width = 0, Height = 0 }, null));
        Drain(AiOutputValidator.ValidateScreen(
            new HmiScreen { Width = 800, Height = 480, BgColor = "红色" }, null));
        Drain(AiOutputValidator.ValidateScreen(ManyControls(21), null));

        // ── 控件族（逐个造，避免互相掩盖）──
        Drain(AiOutputValidator.ValidateScreen(ScreenWith(
            new HmiControl { Type = (HmiControlType)999, Name = "BadType", X = 10, Y = 10, Width = 100, Height = 40 }), null));
        Drain(AiOutputValidator.ValidateScreen(ScreenWith(
            new HmiControl { Type = HmiControlType.Button, Name = "BadSize", X = 10, Y = 10, Width = 0, Height = 40 }), null));
        Drain(AiOutputValidator.ValidateScreen(ScreenWith(
            new HmiControl { Type = HmiControlType.Button, Name = "BadPos", X = -5, Y = 10, Width = 100, Height = 40 }), null));
        Drain(AiOutputValidator.ValidateScreen(ScreenWith(
            new HmiControl { Type = HmiControlType.Button, Name = "OutOfBounds", X = 700, Y = 10, Width = 200, Height = 40 }), null));
        Drain(AiOutputValidator.ValidateScreen(ScreenWith(
            new HmiControl { Type = HmiControlType.Button, Name = "BadBg", X = 10, Y = 10, Width = 100, Height = 40, BgColor = "zzz" }), null));
        Drain(AiOutputValidator.ValidateScreen(ScreenWith(
            new HmiControl { Type = HmiControlType.Button, Name = "BadFg", X = 10, Y = 10, Width = 100, Height = 40, TextColor = "zzz" }), null));
        // 绑定的变量在工程里不存在（project 给了、但 VariableManager 是空的）
        // ⚠ 用 VariableBindingId 而不是 PrimaryBindingVariable —— 后者是**计算属性**
        //   （遍历 VariableBindings，集合为空时回退到 VariableBindingId），赋不了值。
        Drain(AiOutputValidator.ValidateScreen(ScreenWith(
            new HmiControl
            {
                Type = HmiControlType.Button, Name = "Bound", X = 10, Y = 10, Width = 100, Height = 40,
                VariableBindingId = "v_nope"
            }), new HmiProject()));
        // 严重重叠：两个控件完全重合
        Drain(AiOutputValidator.ValidateScreen(ScreenWith(
            new HmiControl { Type = HmiControlType.Button, Name = "OvA", X = 10, Y = 10, Width = 100, Height = 100 },
            new HmiControl { Type = HmiControlType.Button, Name = "OvB", X = 10, Y = 10, Width = 100, Height = 100 }), null));

        // ── 模板族 ──
        Drain(AiOutputValidator.ValidateTemplate(null!));
        Drain(AiOutputValidator.ValidateTemplate(new ControlTemplate { Name = "  " }));
        Drain(AiOutputValidator.ValidateTemplate(ManyControlsTemplate(16)));
        Drain(AiOutputValidator.ValidateTemplate(new ControlTemplate
        {
            Name = "T",
            Controls = { Button("C1") },
            Parameters = { new TemplateParameter { Name = "   " } }
        }));
        Drain(AiOutputValidator.ValidateTemplate(new ControlTemplate
        {
            Name = "T",
            Controls = { Button("C1") },
            Parameters = { new TemplateParameter { Name = "p" }, new TemplateParameter { Name = "p" } }
        }));
        Drain(AiOutputValidator.ValidateTemplate(new ControlTemplate
        {
            Name = "T",
            Controls =
            {
                new HmiControl
                {
                    Type = HmiControlType.Button, Name = "C1", X = 0, Y = 0, Width = 20, Height = 20,
                    Text = "{{Parameters.missing}}"
                }
            }
        }));

        // ── 脚本族 ──
        Drain(AiOutputValidator.ValidateScript(null!));
        Drain(AiOutputValidator.ValidateScript(new HmiScript { Name = "   ", Code = "function f() end" }));
        Drain(AiOutputValidator.ValidateScript(new HmiScript { Name = "S", Code = "" }));
        Drain(AiOutputValidator.ValidateScript(new HmiScript
        {
            Name = "S",
            Code = "function f() end" + new string('a', 9000)
        }));
        Drain(AiOutputValidator.ValidateScript(new HmiScript { Name = "S", Code = "local a = 1" }));
        Drain(AiOutputValidator.ValidateScript(new HmiScript
        {
            Name = "S",
            Code = "function f() os.execute('ls') end"
        }));
        Drain(AiOutputValidator.ValidateScript(new HmiScript
        {
            Name = "S",
            Code = "function f() end",
            Category = "Whatever"
        }));

        // ── 编排族（两条都走"还没轮到调模型"的早退，见类注释）──
        var orchestrator = OrchestratorWithoutImageGenerator();
        Drain(orchestrator.ProcessAsync("生成一个图标", new HmiProject()).GetAwaiter().GetResult().Validation);
        Drain(orchestrator.ProcessAsync("", new HmiProject()).GetAwaiter().GetResult().Validation);

        // ── 布局族 ──
        Layout(new HmiScreen());                                        // 空屏：评分不适用
        Layout(new HmiScreen                                           // 四方向贴边
        {
            Width = 800,
            Height = 480,
            Controls =
            {
                Ctl("L", 0, 200, 60, 30),
                Ctl("T", 200, 0, 60, 30),
                Ctl("R", 780, 200, 60, 30),
                Ctl("B", 200, 470, 60, 30)
            }
        });
        Layout(new HmiScreen                                           // 四个方向各一条未对齐
        {
            Width = 1024,
            Height = 600,
            Controls =
            {
                // Y=300 是共享顶对齐线，A3 偏 7px；Y+Height=330 是共享底对齐线，A3 同偏 7px
                Ctl("A1", 100, 300, 50, 30),
                Ctl("A2", 200, 300, 50, 30),
                Ctl("A3", 400, 307, 50, 30),
                // X=500 是共享左对齐线，B3 偏 7px；X+Width=550 是共享右对齐线，B3 同偏 7px
                Ctl("B1", 500, 400, 50, 30),
                Ctl("B2", 500, 450, 50, 30),
                Ctl("B3", 507, 500, 50, 30)
            }
        });
        Layout(new HmiScreen                                           // 同行间距不均（gaps 10 与 290，均值 150）
        {
            Width = 1024,
            Height = 600,
            Controls =
            {
                Ctl("S1", 100, 200, 50, 30),
                Ctl("S2", 160, 200, 50, 30),
                Ctl("S3", 500, 200, 50, 30)
            }
        });
        Layout(new HmiScreen                                           // 对比度低于 WCAG AA
        {
            Width = 1024,
            Height = 600,
            Controls =
            {
                Ctl("LowContrast", 100, 100, 200, 200, text: "Hello", bg: "#FFFFFF", fg: "#EEEEEE")
            }
        });

        return all;
    }

    // ══════════════════════════════════════════════════════════════════
    //  声明面

    /// <summary>
    /// 反射 <see cref="AiIssueCodes"/> 里声明的**全部**码。
    ///
    /// <para><b>为什么不手写清单</b>：手写清单与实现会分家，分家之后守卫检查的是**清单**
    /// 而不是代码 —— 那是"被自己的清单喂活"的恒绿判据。</para>
    /// </summary>
    internal static List<string> DeclaredCodes()
        => typeof(AiIssueCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

    // ══════════════════════════════════════════════════════════════════
    //  四条判据

    [Fact]
    public void EveryProducedCode_ResolvesInBothLanguages()
    {
        var missing = MissingKeys(
            Probe().Select(p => p.Code),
            LoadDictionary("zh-CN"),
            LoadDictionary("en-US"));

        Assert.True(missing.Count == 0,
            "以下 AI 诊断码的键在字典里查不到 —— 运行时会**回落 AI 层的中文原文**"
            + "（英文界面下中英混排）。补两本字典的键即可：\n" + string.Join("\n", missing));
    }

    [Fact]
    public void PlaceholderCount_MatchesArgsCount()
    {
        var drift = PlaceholderDrift(Probe(), LoadDictionary("zh-CN"), LoadDictionary("en-US"));

        Assert.True(drift.Count == 0,
            "字典模板的占位符个数与 AI 层传的实参个数不符 —— Format 不抛异常、只原样返回，"
            + "用户会在 AI 面板里看到 {1} 这种字面量：\n" + string.Join("\n", drift));
    }

    [Fact]
    public void ChineseTemplate_RendersToTheAiFallback()
    {
        var drift = FallbackDrift(Probe(), LoadDictionary("zh-CN"));

        Assert.True(drift.Count == 0,
            "zh 字典模板渲染后与 AI 层的回落原文不一致 —— 这两份必须逐字相同"
            + "（界面显示字典那份、字典缺键时显示回落那份）：\n" + string.Join("\n", drift));
    }

    /// <summary>
    /// **双向**判据：产出的码集合必须**恰好**等于声明的码集合。
    ///
    /// <para><b>为什么两个方向都要</b>：「产出了没声明」= 有人在调用点写了字面量码
    /// （能编译、能运行、键不会进字典）；「声明了没产出」= 死码，或 Probe 漏了样本
    /// —— 两种情况都必须被人看见，而它们**各自的修法完全不同**，所以失败消息分开写。</para>
    /// </summary>
    [Fact]
    public void ProducedCodes_AndDeclaredCodesAreTheSameSet()
    {
        var declared = DeclaredCodes();

        // 元判据：反射取数方式一旦失效（比如字段不再 IsLiteral），下面会变成"两边都空"的恒绿
        Assert.True(declared.Count >= 41,
            $"反射只枚举到 {declared.Count} 个码 —— 判据的取数方式可能失效了");

        var drift = CodeSetDrift(Probe(), declared);

        Assert.True(drift.Count == 0,
            "AI 诊断码的『产出集合』与『声明集合』对不上：\n" + string.Join("\n", drift)
            + "\n\n  产出了没声明 ⇒ 把调用点改成用 AiIssueCodes 的常量；"
            + "\n  声明了没产出 ⇒ 要么 Probe 漏了样本，要么它确实是死码（那与它的字典键一起处置）。");
    }

    // ══════════════════════════════════════════════════════════════════
    //  判据实现（抽成纯函数 —— 负控要复用，不能只在测试体里内联）

    /// <summary>
    /// 读字典 —— 实现在 <see cref="PluginTextLocalizationGuardTests"/>（同一条本地化守卫族的另一半）。
    /// 这里只转发，**不另写一份**：两处各读一次盘，迟早有一处先失效（比如换了文件位置只改了一边）。
    /// </summary>
    private static Dictionary<string, string> LoadDictionary(string lang)
        => PluginTextLocalizationGuardTests.LoadDictionary(lang);

    internal static List<string> MissingKeys(
        IEnumerable<string> codes,
        Dictionary<string, string> zh,
        Dictionary<string, string> en)
        => codes
            .Select(ExternalText.AiIssueKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .Where(k => !zh.ContainsKey(k) || !en.ContainsKey(k))
            .Select(k => k + (zh.ContainsKey(k) ? "" : "  缺 zh-CN") + (en.ContainsKey(k) ? "" : "  缺 en-US"))
            .ToList();

    internal static List<string> PlaceholderDrift(
        IReadOnlyList<Produced> produced,
        Dictionary<string, string> zh,
        Dictionary<string, string> en)
    {
        var drift = new List<string>();
        foreach (var p in produced)
        {
            var key = ExternalText.AiIssueKey(p.Code);
            var expected = p.Args?.Count ?? 0;

            foreach (var (lang, dict) in new[] { ("zh-CN", zh), ("en-US", en) })
            {
                if (!dict.TryGetValue(key, out var template)) { drift.Add($"{lang} {key}: 缺键"); continue; }
                if (!PlaceholdersMatch(template, expected))
                {
                    drift.Add($"{lang} {key}: 模板 {LocalizationPlaceholders.DescribeIndexes(template)} "
                              + $"与 AI 层传的 {expected} 个实参对不上");
                }
            }
        }
        return drift.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// 模板与实参个数的判据**不在这里** —— 它住在
    /// <see cref="LocalizationPlaceholders.MatchesArgumentCount"/>（运行期 <c>Format</c> 用的就是它）。
    /// 守卫与被守的对象用两套规则等于没守，这条本项目在脚本校验那一族上已经吃过亏。
    /// </summary>
    private static bool PlaceholdersMatch(string template, int argCount)
        => LocalizationPlaceholders.MatchesArgumentCount(template, argCount);

    internal static List<string> FallbackDrift(
        IReadOnlyList<Produced> produced,
        Dictionary<string, string> zh)
    {
        var drift = new List<string>();
        foreach (var p in produced)
        {
            var key = ExternalText.AiIssueKey(p.Code);
            if (!zh.TryGetValue(key, out var template)) { drift.Add($"{key}: 缺键"); continue; }

            var rendered = p.Args is { Count: > 0 } a ? string.Format(template, a.ToArray()) : template;
            if (rendered != p.Message)
                drift.Add($"{key}\n    字典渲染: {rendered}\n    AI 回落: {p.Message}");
        }
        return drift.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    internal static List<string> CodeSetDrift(IEnumerable<Produced> produced, IEnumerable<string> declared)
    {
        var producedCodes = produced.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
        var declaredCodes = declared.ToHashSet(StringComparer.Ordinal);

        var drift = new List<string>();
        foreach (var c in producedCodes.Except(declaredCodes).OrderBy(c => c, StringComparer.Ordinal))
            drift.Add($"  产出了但没声明（调用点是不是写了字面量码？）：{c}");
        foreach (var c in declaredCodes.Except(producedCodes).OrderBy(c => c, StringComparer.Ordinal))
            drift.Add($"  声明了但产出不到（死码？还是 Probe 漏了样本？）：{c}");
        return drift;
    }

    // ══════════════════════════════════════════════════════════════════
    //  负控（每组都与正控同场）

    /// <summary>
    /// 从字典里**删掉**一个真实用到的键 ⇒ 键覆盖判据必须点名它。
    /// <para>正控同场（真字典下三条判据都空）—— 否则"报出来了"可能只是判据恒真。</para>
    /// </summary>
    [Fact]
    public void NegativeControl_RemovingAKey_IsCaught()
    {
        var produced = Probe();
        var codes = produced.Select(p => p.Code).ToList();
        var zh = LoadDictionary("zh-CN");
        var en = LoadDictionary("en-US");

        Assert.Empty(MissingKeys(codes, zh, en));                 // 正控
        Assert.Empty(PlaceholderDrift(produced, zh, en));         // 正控
        Assert.Empty(FallbackDrift(produced, zh));                // 正控

        var victim = ExternalText.AiIssueKey(produced[0].Code);
        Assert.True(zh.ContainsKey(victim), $"探针结果里应当有 {victim}");
        zh.Remove(victim);

        // MissingKeys 返回的是带"缺 zh-CN"后缀的**描述串**，所以按包含匹配、不是等值
        Assert.Contains(MissingKeys(codes, zh, en), d => d.Contains(victim, StringComparison.Ordinal));
    }

    /// <summary>
    /// 模板改歪 ⇒ 分别由占位符判据与一致性判据点名。
    /// <para>这两种破坏**键覆盖判据都看不见**（键还在），所以必须有另外两条 ——
    /// 这正是"一条判据只能守一件事"的实例。</para>
    /// </summary>
    [Fact]
    public void NegativeControl_BreakingATemplate_IsCaught()
    {
        var produced = Probe();
        var zh = LoadDictionary("zh-CN");
        var en = LoadDictionary("en-US");

        Assert.Empty(PlaceholderDrift(produced, zh, en));         // 正控
        Assert.Empty(FallbackDrift(produced, zh));                // 正控

        // 挑一条**带占位符**的：只有它才能暴露"占位符个数"这一类破坏
        var sample = produced.First(p => p.Args is { Count: > 0 });
        var key = ExternalText.AiIssueKey(sample.Code);
        var backup = zh[key];

        // ① 多一个占位符 ⇒ 键还在、文字还在，只有占位符判据抓得到
        zh[key] = backup + " {9}";
        Assert.Contains(PlaceholderDrift(produced, zh, en), d => d.Contains(key));
        Assert.Empty(MissingKeys(produced.Select(p => p.Code), zh, en));   // 键覆盖确实看不见它

        // ② 文案改歪 ⇒ 只有一致性判据抓得到
        zh[key] = backup + "（改歪）";
        Assert.Contains(FallbackDrift(produced, zh), d => d.Contains(key));
    }

    /// <summary>
    /// 双向判据的两个方向**各自**能独立命中。
    ///
    /// <para>注入用的是**不存在的码名**（<c>FakeCodeYyy</c> / <c>FakeCodeZzz</c>）——
    /// 用真码名会重演本项目登记过的 #48：语义判据里的字面量反过来把自己喂饱。</para>
    /// </summary>
    [Fact]
    public void NegativeControl_CodeSetDrift_IsCaughtInBothDirections()
    {
        var produced = Probe();
        var declared = DeclaredCodes();

        Assert.Empty(CodeSetDrift(produced, declared));           // 正控

        // ① 声明多了（死码 / 样本漏了）
        var withGhost = declared.Concat(new[] { "FakeCodeYyy" }).ToList();
        var d1 = CodeSetDrift(produced, withGhost);
        Assert.Contains(d1, x => x.Contains("FakeCodeYyy", StringComparison.Ordinal));
        Assert.Contains(d1, x => x.Contains("声明了但产出不到", StringComparison.Ordinal));

        // ② 产出了没声明（调用点写了字面量码）
        var withLiteral = produced.Concat(new[] { new Produced("FakeCodeZzz", "假", null) }).ToList();
        var d2 = CodeSetDrift(withLiteral, declared);
        Assert.Contains(d2, x => x.Contains("FakeCodeZzz", StringComparison.Ordinal));
        Assert.Contains(d2, x => x.Contains("产出了但没声明", StringComparison.Ordinal));
    }

    // ══════════════════════════════════════════════════════════════════
    //  夹具

    private static HmiScreen ScreenWith(params HmiControl[] controls)
    {
        var screen = new HmiScreen { Name = "S", Width = 800, Height = 480 };
        foreach (var c in controls) screen.Controls.Add(c);
        return screen;
    }

    private static HmiScreen ManyControls(int count)
    {
        var screen = new HmiScreen { Name = "S", Width = 2000, Height = 2000 };
        for (var i = 0; i < count; i++)
        {
            // 错开摆放，免得顺带造出一堆重叠（重叠本身无害，但会让失败信息变吵）
            screen.Controls.Add(new HmiControl
            {
                Type = HmiControlType.Label, Name = $"C{i}", X = 20 + i * 40, Y = 20, Width = 30, Height = 30
            });
        }
        return screen;
    }

    private static ControlTemplate ManyControlsTemplate(int count)
    {
        var template = new ControlTemplate { Name = "T" };
        for (var i = 0; i < count; i++)
        {
            template.Controls.Add(new HmiControl
            {
                Type = HmiControlType.Label, Name = $"C{i}", X = 0, Y = i * 30, Width = 30, Height = 30
            });
        }
        return template;
    }

    private static HmiControl Button(string name) => new()
    {
        Type = HmiControlType.Button, Name = name, X = 0, Y = 0, Width = 40, Height = 24
    };

    private static HmiControl Ctl(
        string name, double x, double y, double w = 80, double h = 40,
        string? text = null, string? bg = null, string? fg = null) => new()
    {
        Type = HmiControlType.Label, Name = name, X = x, Y = y, Width = w, Height = h,
        Text = text, BgColor = bg, TextColor = fg
    };

    /// <summary>
    /// 造一个**没装图片生成器**的编排器 —— 两条编排码都在"还没轮到调模型"时返回，
    /// 所以只要把三个生成器构造出来。<see cref="UnusedLlmProvider"/> 负责证明这一点。
    /// </summary>
    private static AiOrchestrator OrchestratorWithoutImageGenerator()
    {
        var llm = new UnusedLlmProvider();
        var prompts = new PromptTemplateEngine();
        return new AiOrchestrator(
            new NlToScreenGenerator(llm, prompts),
            new NlToTemplateGenerator(llm, prompts),
            new NlToScriptGenerator(llm, prompts));
    }

    /// <summary>
    /// 只为把生成器构造出来而存在 —— **一旦被真的调用就抛**。
    ///
    /// <para>它不是"永远不抛的桩"：那会让"守卫其实走到了 LLM 调用"这件事静默发生，
    /// 而那时产出的码就不是本文件登记的那两条了（失败会以"码集合对不上"的形式暴露，
    /// 但信息量远不如直接在这里抛）。</para>
    /// </summary>
    private sealed class UnusedLlmProvider : ILlmProvider
    {
        public string ProviderName => "unused-in-guard";

        public Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "本地化守卫不该真的调用 LLM —— 编排类的两条码都在调模型之前就返回了");
    }
}
