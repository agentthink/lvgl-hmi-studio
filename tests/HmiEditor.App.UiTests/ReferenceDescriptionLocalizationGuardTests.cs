using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **交叉引用面板「描述列」的本地化判据**（阶段 95，闭环登记 <b>#61n</b>）。
///
/// <para><b>本项缺陷的形状</b>：面板的 <c>{Binding Description}</c> 直显 Core 拼好的中文整句
/// （<c>CrossReferenceAnalyzer</c> 的 9 条 <c>Description = ...</c>），而 Core 是库、不引本地化
/// ⇒ 英文界面下那一列是中文。阶段 94（#61m）只搬走了动作那 2 条，其余 <b>7 条</b>仍在直显。</para>
///
/// <para><b>为什么分派依据是新枚举而不是 <c>ReferenceType</c></b>：类型决定不了模板 ——
/// 同一个 <c>VariableBinding</c> 下有两种措辞（带方向 / 不带），同一个 <c>ActionTarget</c> 下也有两种
/// （<c>动作 {0}</c> / <c>{0} → 切换到 {1}</c>）。</para>
///
/// <para><b>为什么必须有本文件</b>：搬完之后「两份孪生」诞生了 —— Core 那 9 条中文（回落）
/// 与字典的 <c>cross.ref.desc.*</c> + 三个实参字段是同一件事的两份存储。<b>两份必然漂移</b>，
/// 所以用**相等式**钉住（zh 渲染逐字等于 Core 回落），并把「实参有没有真的接上」也断在等式里
/// （字段忘赋值 ⇒ 渲染出的方向/函数名与 Core 原文对不上 ⇒ 红）。</para>
///
/// <para><b>判据之间的分工</b>（缺一条就有一件看不见）：
/// <list type="number">
/// <item>J1 每种描述种类都必须**随语言变**（漏分支/写成回落 ⇒ 不变 ⇒ 红）；</item>
/// <item>J2 zh **相等式**：渲染 ↔ 期望（除「绑定方向」那一档外**逐字**等于 Core 回落原文 ——
///   方向是阶段 96/#61o 唯一刻意改掉的中文，它的期望改用「字典模板 ＋ 字典方向」）；</item>
/// <item>J3 成对：en 不许有汉字，**且** zh 确实有（否则判据被"没有中文"满足）；</item>
/// <item>J4 **端到端**：真实工程 → 真实分析器 → <see cref="ReferenceRow"/>，en 下逐行无汉字，
///   且**没有一行是 Unset**（站点忘填即红）；</item>
/// <item>J5 <c>ActionDetail</c> ⇔ <c>Action != null</c>（双向 —— 上半句钉"没漏挂对象"，下半句钉"挂了就要用"）；</item>
/// <item>J6 回落原文**跨语言冻结**（Core 那份中文不许随界面语言变）；</item>
/// <item>J7 <c>Unset</c> 的**诚实边界**：未设种类 ⇒ 原样显示回落（这不是缺陷，写出来免得被当成漏网）。</item>
/// </list></para>
///
/// <para><b>不在这里守的</b>：占位符 ↔ 实参个数、两本字典占位符一致由
/// <c>LocalizedFormatCallSiteTests</c>（C1/C3）管；App 侧中文硬编码由
/// <c>HardcodedLocalizationGuardTests</c> 全局管。本文件不复写那两条规则。</para>
/// </summary>
[Collection("Language-global")]
public class ReferenceDescriptionLocalizationGuardTests
{
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    private static Dictionary<string, string> Zh =>
        PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");

    private static Dictionary<string, string> En =>
        PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    /// <summary>本刀要收的描述种类（<c>Unset</c> 刻意不在内 —— 它是显式回落，见 J7）。</summary>
    private static readonly ReferenceDescriptionKind[] MappedKinds =
    {
        ReferenceDescriptionKind.VariableBinding,
        ReferenceDescriptionKind.LegacyVariableBinding,
        ReferenceDescriptionKind.ActionDetail,
        ReferenceDescriptionKind.ScreenReference,
        ReferenceDescriptionKind.ScriptRun,
        ReferenceDescriptionKind.ScriptBinding,
    };

    /// <summary>下界 —— 反射到 0 个取值会让 J1 的闭包**恒真**。</summary>
    private const int ExpectedKindCount = 6;

    // ═══════════════════════════ 夹具 ═══════════════════════════

    /// <summary>
    /// 一个真实工程，**五种引用一次到齐**：联动模型绑定 / 回退旧字段 / 动作 / 屏幕跳转 / 脚本
    /// （脚本那条同时产出 `ScriptRun` 与 `ScriptBinding`）。
    ///
    /// <para>用真实模型构造、不用手搓 <c>ReferenceLocation</c>：手搓会让「Core 有没有把实参填进
    /// `Description`」与「有没有填进字段」这两件事分开自证，端到端那条就没有分量了。</para>
    /// </summary>
    private static HmiProject FixtureProject()
    {
        var project = new HmiProject();
        project.VariableManager.Variables.Add(new Variable { Id = "v_tank", Name = "v_tank" });
        project.VariableManager.Variables.Add(new Variable { Id = "v_level", Name = "v_level" });

        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        var host = new HmiControl { Id = "c_host", Name = "HostScreen", Type = HmiControlType.Button };
        host.Actions.Add(HmiAction.SwitchScreen("Settings"));
        // 第二个动作只为把 `ScriptRun` 那一档凑出来（`FindScriptReferences` 挑的是 ExecuteScript）
        host.Actions.Add(HmiAction.ExecuteScript("script1"));
        screen.Controls.Add(host);

        var bound = new HmiControl { Id = "c1", Name = "TankLabel", Type = HmiControlType.Label };
        bound.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Text",
            VariableId = "v_tank",
            // ⚠ 刻意用非默认值：字段忘赋值时默认是 ReadOnly ⇒ J2 的相等式会点名
            Direction = BindingDirection.WriteOnly,
        });
        bound.Actions.Add(HmiAction.SetVariable("v_tank", "42"));
        screen.Controls.Add(bound);

        // 回退旧字段：VariableBindings 为空、只有 VariableBindingId
        screen.Controls.Add(new HmiControl
        {
            Id = "c2",
            Name = "LevelGauge",
            Type = HmiControlType.Gauge,
            VariableBindingId = "v_level",
        });

        project.Screens.Add(screen);

        project.ScriptBindings.Add(new ScriptBinding
        {
            ControlId = "c1",
            EventName = "OnClick",
            ScriptId = "script1",
            FunctionName = "on_tank_click",
        });

        return project;
    }

    /// <summary>把夹具里**能查到的所有引用行**收集起来（走三条真实查询路径）。</summary>
    private static List<ReferenceLocation> AllRows()
    {
        var p = FixtureProject();
        var rows = new List<ReferenceLocation>();
        rows.AddRange(CrossReferenceAnalyzer.FindVariableReferences(p, "v_tank").References);
        rows.AddRange(CrossReferenceAnalyzer.FindVariableReferences(p, "v_level").References);
        rows.AddRange(CrossReferenceAnalyzer.FindScreenReferences(p, "Settings").References);
        rows.AddRange(CrossReferenceAnalyzer.FindScriptReferences(p, "script1").References);
        return rows;
    }

    /// <summary>把语言钉住跑一段，结束后还原（既有本地化判据的共同做法）。</summary>
    private static void InLanguage(string language, Action body)
    {
        var original = AppLocalization.Service.CurrentLanguage;
        try
        {
            AppLocalization.Service.SwitchLanguage(language);
            body();
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }
    }

    private static string RenderZh(ReferenceLocation loc)
    {
        var v = "";
        InLanguage("zh-CN", () => v = ReferenceTextKeys.Describe(loc));
        return v;
    }

    private static string RenderEn(ReferenceLocation loc)
    {
        var v = "";
        InLanguage("en-US", () => v = ReferenceTextKeys.Describe(loc));
        return v;
    }

    // ═══════════════════════════ J1 ═══════════════════════════

    /// <summary>
    /// 每种**已登记**的描述种类都必须**随语言变** —— 这是「真的走了字典」的取证。
    ///
    /// <para>为什么不是断言「等于字典值」：那会把被测出口自己算的当期望。
    /// 换语言后文本必须变，是一个**只有在真的查了字典**时才成立的性质：
    /// 写成 <c>=&gt; location.Description</c>（回落）或漏掉分支落到 <c>Unset</c>，两次渲染都会**相同**。</para>
    /// </summary>
    [Fact]
    public void EveryMappedKind_ChangesWithLanguage()
    {
        var rows = AllRows();
        var bad = new List<string>();
        var seen = new HashSet<ReferenceDescriptionKind>();

        foreach (var kind in MappedKinds)
        {
            var sample = rows.FirstOrDefault(r => r.DescriptionKind == kind);
            if (sample == null)
            {
                bad.Add($"{kind}: 夹具里没有任何一行是这种种类（判据在空转）");
                continue;
            }
            seen.Add(kind);

            var zh = RenderZh(sample);
            var en = RenderEn(sample);
            if (string.Equals(zh, en, StringComparison.Ordinal))
                bad.Add($"{kind}: zh 与 en 渲染相同（「{zh}」）⇒ 这一档没有真的查字典");
        }

        Assert.True(bad.Count == 0,
            "有描述种类没有走字典（漏分支，或分支写成了回落）：\n  " + string.Join("\n  ", bad));
        Assert.True(seen.Count >= ExpectedKindCount,
            $"夹具只覆盖了 {seen.Count} 种描述种类（下界 {ExpectedKindCount}）—— 判据在空转");
    }

    // ═══════════════════════════ J2 ═══════════════════════════

    /// <summary>
    /// zh 下渲染必须与 Core 的回落原文**逐字相同**（**除绑定方向那一档**，见
    /// <see cref="ExpectedZh"/>）。
    ///
    /// <para>两个作用：① 「**零用户可见变化**」的**证明**（不是声明）—— 本刀把面板从 Core 中文
    /// 改成走字典，若中文下不完全一致，就等于顺手改了用户看到的东西；
    /// ② 「**实参真的接上了**」—— 期望值来自 Core 自己拼的那句（另一侧），
    /// 字段忘赋值（如 <c>BindingDirection</c> 落成默认的 <c>ReadOnly</c>）会让渲染与它对不上。</para>
    /// </summary>
    [Fact]
    public void ChineseRendering_IsByteIdenticalToTheCoreFallback()
    {
        var rows = AllRows();
        var bad = new List<string>();

        InLanguage("zh-CN", () =>
        {
            foreach (var r in rows)
            {
                var rendered = ReferenceTextKeys.Describe(r);
                var expected = ExpectedZh(r);
                if (!string.Equals(rendered, expected, StringComparison.Ordinal))
                    bad.Add($"{r.DescriptionKind} [{r.ControlName}]: 界面「{rendered}」 ≠ 期望「{expected}」");
            }
        });

        Assert.True(bad.Count == 0,
            "zh 下界面渲染与期望不一致 ⇒ 要么**改了用户可见文案**（须单独登记），"
            + "要么**实参没接上**：\n  " + string.Join("\n  ", bad));
        Assert.True(rows.Count >= 6,
            $"夹具只产出 {rows.Count} 行引用（下界 6）—— 相等式没有分量");
    }

    /// <summary>
    /// zh 下那句话的**期望值**。
    ///
    /// <para>除「绑定方向」一档外，期望就是 Core 的回落原文**逐字** —— 那是「零用户可见变化」
    /// 的证明。方向那一档是<b>阶段 96（#61o）唯一刻意改掉的中文</b>：改动前 Core 回落里嵌的是
    /// 英文枚举名（<c>WriteOnly</c>，因为 <c>Format</c> 的 <c>object[]</c> 会对它 ToString()），
    /// 阶段 96 把它换成字典里的中文（只写）⇒ <b>不可能再逐字相等</b>。</para>
    ///
    /// <para>期望因此改成「<b>字典</b>里的模板 ＋ <b>字典</b>里的方向」：两个来源都与被测出口
    /// （<see cref="ReferenceTextKeys"/>）**独立** ⇒ 仍然不是「拿被测物自己算的当期望」。</para>
    /// </summary>
    private static string ExpectedZh(ReferenceLocation r)
    {
        if (r.DescriptionKind != ReferenceDescriptionKind.VariableBinding)
            return r.Description;

        var template = Zh[Lang.CrossRefDescBinding];        // "控件 {0} 绑定变量 ({1})"
        var direction = Zh[DirectionKeyOf(r.BindingDirection)];
        return template.Replace("{0}", r.ControlName).Replace("{1}", direction);
    }

    /// <summary>方向 → 键（只在本文件的期望构造里用；产品侧的键推导在 <c>ReferenceTextKeys</c>）。</summary>
    private static string DirectionKeyOf(BindingDirection direction) => direction switch
    {
        BindingDirection.ReadOnly => Lang.CrossRefDirReadOnly,
        BindingDirection.WriteOnly => Lang.CrossRefDirWriteOnly,
        BindingDirection.ReadWrite => Lang.CrossRefDirReadWrite,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction,
            "有绑定方向没有键 —— 阶段 96 的 DirectionName 会先炸，这里只是把话说清楚"),
    };

    // ═══════════════════════════ J3 ═══════════════════════════

    /// <summary>
    /// **成对**断言：en 侧不许含汉字，**并且** zh 侧确实含汉字。
    /// 只断前一半会被「夹具本身就没中文」满足（本项目踩过）。
    /// </summary>
    [Fact]
    public void EnglishRendering_HasNoCjk_AndChineseOneDoes()
    {
        var rows = AllRows()
            .Where(r => r.DescriptionKind != ReferenceDescriptionKind.Unset)
            .ToList();

        var enLeaks = new List<string>();
        var zhWithoutCjk = new List<string>();

        foreach (var r in rows)
        {
            var en = RenderEn(r);
            if (Cjk.IsMatch(en))
                enLeaks.Add($"{r.DescriptionKind}: en「{en}」");
            var zh = RenderZh(r);
            if (!Cjk.IsMatch(zh))
                zhWithoutCjk.Add($"{r.DescriptionKind}: zh「{zh}」");
        }

        Assert.True(enLeaks.Count == 0,
            "英文界面的交叉引用行里出现了中文：\n  " + string.Join("\n  ", enLeaks));
        Assert.True(zhWithoutCjk.Count == 0,
            "这些行的 zh 渲染不含汉字 —— 要么它落到了回落分支，要么字典有问题，"
            + "总之上面那条英文断言因此没有分量：\n  " + string.Join("\n  ", zhWithoutCjk));
    }

    // ═══════════════════════════ J4 ═══════════════════════════

    /// <summary>
    /// **端到端**：真实工程 → 真实分析器 → <see cref="ReferenceRow"/> 的渲染。
    ///
    /// <para>这条同时钉三件事：① 每个站点都**填了种类**（有一行是 <c>Unset</c> ⇒ 点名，
    /// 而 Unset 在英文界面下就是中文 ⇒ 同一句断言也能从文本面抓到）；
    /// ② 行的渲染点确实读了字典；③ 英文下**一行中文都没有**。</para>
    /// </summary>
    [Fact]
    public void ReferenceRows_RenderInEnglish_EndToEnd()
    {
        var rows = AllRows();
        Assert.True(rows.Count >= 6, $"分析器只返回 {rows.Count} 行 —— 端到端判据的前提不成立");

        var unset = rows.Where(r => r.DescriptionKind == ReferenceDescriptionKind.Unset).ToList();
        Assert.True(unset.Count == 0,
            "分析器产出的行里有 DescriptionKind.Unset —— 那说明某个站点忘了填种类"
            + $"（{unset.Count} 行）。界面侧会对它原样显示 Core 的中文回落：\n  "
            // ⚠ 消息里**不许**用 unset[0]：断言消息是**立即求值**的，
            //    绿的时候它照样算 ⇒ 空集合直接 IndexOutOfRange（本判据自己踩过一次）。
            + string.Join("\n  ", unset.Select(r => $"{r.ControlName} / {r.Description}")));

        var rendered = rows.Select(r => new ReferenceRow(r)).ToList();

        InLanguage("en-US", () =>
        {
            var leaks = rendered
                .Where(row => Cjk.IsMatch(row.Description))
                .Select(row => $"「{row.Description}」")
                .ToList();
            Assert.True(leaks.Count == 0,
                "英文界面下交叉引用行仍是中文：\n  " + string.Join("\n  ", leaks));
        });

        // 实参确实被填进模板（不是把占位符原样漏出去）
        var all = rendered.Select(row => row.Description).ToList();
        InLanguage("en-US", () =>
        {
            Assert.Contains(all, d => d.Contains("TankLabel", StringComparison.Ordinal));
            Assert.Contains(all, d => d.Contains("on_tank_click", StringComparison.Ordinal));
            Assert.DoesNotContain(all, d => d.Contains("{0}", StringComparison.Ordinal));
            Assert.DoesNotContain(all, d => d.Contains("{1}", StringComparison.Ordinal));
        });
    }

    // ═══════════════════════════ J5 ═══════════════════════════

    /// <summary>
    /// <c>ActionDetail</c> ⇔ <c>Action != null</c>，**两个方向**都断：
    /// 漏挂对象 ⇒ 界面拿不到动作（退回中文回落）；挂了却不用 ⇒ 那次挂载是死代码。
    /// </summary>
    [Fact]
    public void ActionDetailImpliesAndRequiresAnAction()
    {
        var rows = AllRows();

        var detailWithoutAction = rows
            .Where(r => r.DescriptionKind == ReferenceDescriptionKind.ActionDetail && r.Action == null)
            .Select(r => $"{r.ControlName}: {r.Description}")
            .ToList();
        Assert.True(detailWithoutAction.Count == 0,
            "有行标了 ActionDetail 却没挂 Action ⇒ 界面只能回落成中文：\n  "
            + string.Join("\n  ", detailWithoutAction));

        var actionWithoutDetail = rows
            .Where(r => r.Action != null && r.DescriptionKind != ReferenceDescriptionKind.ActionDetail)
            .Select(r => $"{r.ControlName}: {r.DescriptionKind}")
            .ToList();
        Assert.True(actionWithoutDetail.Count == 0,
            "有行挂了 Action 却没标 ActionDetail ⇒ 那次挂载不会被读到：\n  "
            + string.Join("\n  ", actionWithoutDetail));

        Assert.True(rows.Any(r => r.Action != null),
            "夹具里没有任何动作引用 —— 这条判据在空转");
    }

    // ═══════════════════════════ J6 ═══════════════════════════

    /// <summary>
    /// **回落原文跨语言冻结**：<c>ReferenceLocation.Description</c> 是 Core 的中文，
    /// 两种语言下必须逐字不变。
    ///
    /// <para>守的是「搬出口」的另一半：若哪天有人为了"让界面统一"把 Core 那份也接了字典，
    /// 它就不再是"缺键时的回落原文"，而变成第二份会漂移的显示文案。</para>
    /// </summary>
    [Fact]
    public void CoreFallback_IsFrozen_AcrossLanguages()
    {
        var rows = AllRows();
        var before = rows.Select(r => r.Description).ToList();

        InLanguage("en-US", () =>
        {
            var now = rows.Select(r => r.Description).ToList();
            Assert.Equal(before, now);
        });

        Assert.True(before.All(d => Cjk.IsMatch(d)),
            "回落原文里没有汉字 —— 那它就不该叫「回落」，这条冻结断言也没有分量");
    }

    // ═══════════════════════════ J7 ═══════════════════════════

    /// <summary>
    /// **诚实边界**：<c>Unset</c>（枚举忘了赋值）⇒ 原样显示 Core 回落，两种语言都一样。
    ///
    /// <para>这不是缺陷 —— <c>ReferenceLocation</c> 是公开类型，外部可以自己构造；
    /// 写出来是为了让「未设」与「漏映射」能分开：后者由 J1（随语言变）与 J4（无 Unset）抓。
    /// ⚠ 也正因为这一档存在，J3/J4 的断言都**先排除**它 —— 否则它们会被"未设即中文"满足。</para>
    /// </summary>
    [Fact]
    public void UnsetKind_FallsBackToTheCoreText_InBothLanguages()
    {
        var loc = new ReferenceLocation
        {
            ControlName = "X",
            DescriptionKind = ReferenceDescriptionKind.Unset,
            Description = "控件 X 绑定到变量",
        };

        Assert.Equal("控件 X 绑定到变量", RenderZh(loc));
        Assert.Equal("控件 X 绑定到变量", RenderEn(loc));
        // 顺手钉住"这一档确实不是某个具名档"（xUnit2000：常量作 expected）
        Assert.NotEqual(ReferenceDescriptionKind.VariableBinding, loc.DescriptionKind);
    }

    // ═══════════════════════════ J8 ═══════════════════════════

    /// <summary>
    /// 字典侧：5 个键两本都在、都非空，且 zh/en 不相等（真翻了）。
    /// 键名与 <c>Lang</c> 常量一一对应由 <c>LangKeyCoverageTests</c> 全局管，这里只钉本族这 5 个。
    /// </summary>
    [Fact]
    public void Dictionary_HasTheFiveTemplates_Translated()
    {
        var keys = new[]
        {
            Lang.CrossRefDescBinding,
            Lang.CrossRefDescLegacyBinding,
            Lang.CrossRefDescScreenSwitch,
            Lang.CrossRefDescScriptRun,
            Lang.CrossRefDescScriptBinding,
        };

        var missing = keys.Where(k => !Zh.ContainsKey(k) || !En.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0,
            "字典缺键（英文界面会显示键名）：\n  " + string.Join("\n  ", missing));

        var notTranslated = keys
            .Where(k => string.Equals(Zh[k], En[k], StringComparison.Ordinal))
            .Select(k => $"{k}: 两本同值「{Zh[k]}」")
            .ToList();
        Assert.True(notTranslated.Count == 0,
            "这些模板英文侧与中文侧逐字相同 ⇒ 没翻：\n  " + string.Join("\n  ", notTranslated));
    }

    // ═══════════════════════════ J9 ═══════════════════════════

    /// <summary>
    /// **枚举成员名不许与任何 <c>Lang</c> 常量同名。**
    ///
    /// <para><b>这条判据的来历是一个真实事故</b>（本阶段第二次门禁抓到的）：
    /// <c>LangConstantUsageGuardTests</c> 的引用面（<c>ReferenceFace.Code</c>）是**任意标识符**，
    /// 不限 <c>Lang.</c> 前缀（读数见它自己的 <c>ReferenceFaces_IgnoreProseAndLiteralText</c>：
    /// <c>Lang.FakeCodeRef</c> 里的 <c>FakeCodeRef</c> 算引用）。只要 <c>src/</c>+<c>tests/</c> 里
    /// 出现一个与常量**同名**的标识符，那个常量就算"被代码读过"。
    /// 本阶段第一版把枚举成员写成 <c>ScreenSwitch</c>，而 <c>Lang</c> 里有一个同名的
    /// **无引用**常量（<c>screen.switch</c>，键由 <c>ScreenManagerView.axaml</c> 的
    /// <c>{res:Loc}</c> 担保 ⇒ 落 M2 桶）⇒ 它被**意外写活**、离开「无代码引用」集合
    /// ⇒ 基线 1113 → <b>1112</b>（实测 M2 <b>828 → 827</b>）。
    /// ⚠ 症状出现在**另一条判据**上，而那条判据给的处置建议恰好是"把基线改成 1112" ——
    /// 照做就把事故洗成了"清理"。⇒ 本刀的正确处置是**改名**（改成
    /// <c>ScreenReference</c>），基线不动。</para>
    ///
    /// <para><b>为什么用反射取名字、不写名单</b>：名单里出现常量名会把同一件事再犯一遍
    /// （#48「名字即引用」）。判据只读**两侧的集合**。</para>
    /// </summary>
    [Fact]
    public void KindNames_MustNotCollideWithLangConstantNames()
    {
        var kindNames = typeof(ReferenceDescriptionKind)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => f.Name)
            .ToList();

        Assert.True(kindNames.Count >= ExpectedKindCount + 1,
            $"只反射到 {kindNames.Count} 个枚举成员（下界 {ExpectedKindCount + 1}，含 Unset）—— 判据在空转");

        var langNames = typeof(Lang)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => f.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(langNames.Count >= 1500,
            $"Lang 常量只反射到 {langNames.Count} 个 —— 反射面找错了，下面的断言没有分量");

        var collisions = kindNames.Where(langNames.Contains).ToList();
        Assert.True(collisions.Count == 0,
            "枚举成员名与 Lang 常量名撞了 —— 这会把那个常量**意外写活**"
            + "（引用面是任意标识符，不限 `Lang.` 前缀）⇒ 另一条判据的基线会莫名 −1，"
            + "而编译面与类型面都看不见。改名，别改基线：\n  "
            + string.Join("\n  ", collisions));
    }
}
