using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **动作展示名的本地化判据**（阶段 94，闭环登记 <b>#61m</b>）。
///
/// <para><b>本项缺陷的形状</b>：控件动作的展示名在界面上有**三个露出点**，改动前全部是 Core 拼的中文 ——
/// ① 属性面板的动作类型下拉框（<c>HmiAction.GetAvailableActions</c> 的 <c>ActionMeta.DisplayName</c>）；
/// ② 属性面板的动作 chip 列表（<c>ActionTypeMeta.DisplayName</c>）；
/// ③ 交叉引用面板的 <c>Description</c> 列（<c>HmiAction.GetDescription()</c>）。
/// 而同一个映射**另有一个语言要求相反的方向** —— 产物注释
/// （<c>CodeGenerator.Actions.cs</c> 三处 <c>Cmt(</c> 与 <c>SecurityConfigCodeGen.cs</c> 的动作→权限表）
/// 必须**固定中文**（产物可复现性）。⇒ 出口必须拆成两条，界面侧走
/// <see cref="ActionTextKeys"/>，产物侧留在 Core。</para>
///
/// <para><b>为什么必须有本文件</b>：拆完之后「两份孪生」就诞生了 —— Core 的产物表与字典的
/// <c>action.*</c> 是同一件事的两份存储。<b>两份必然漂移</b>（这是本仓库反复量到的形状），
/// 所以要用**相等式**把它们钉在一起：zh 字典值必须逐字等于 Core 的产物表。</para>
///
/// <para><b>判据之间的分工</b>（缺一条就有一件看不见）：
/// <list type="number">
/// <item>J1 一对一（双向）—— 新增动作类型时漏建键 ⇒ 红；</item>
/// <item>J2 每个 Id 都必须**真的翻了** —— 未被测的话，「回落成 Id」会让缺失静默通过；</item>
/// <item>J3 zh **相等式**：短名 ↔ Core 产物表；</item>
/// <item>J4 成对：英文侧不许有汉字，**且**中文侧确实有（否则判据被"夹具没中文"满足）；</item>
/// <item>J5 zh **相等式**：整句描述 ↔ <c>HmiAction.GetDescription()</c>
///   —— 这条同时是「**零用户可见变化**」的证明（不是声明）；</item>
/// <item>J6 英文侧：带得上实参（目标 / 取值）、且无汉字；</item>
/// <item>J7 产物侧**跨语言冻结**（切到 en 后那两串中文逐字不变）；</item>
/// <item>J8 端到端：真实工程 → 真实分析器 → <see cref="ReferenceRow"/> 的渲染；</item>
/// <item>J9 未登记类型回落到 Id（**诚实边界**，不是缺陷）。</item>
/// </list></para>
///
/// <para><b>不在这里守的两件事</b>（避免判据分家）：
/// ① <c>src/HmiEditor.App</c> 里的中文硬编码由 <c>HardcodedLocalizationGuardTests</c> 全局管；
/// ② 键的占位符 ↔ 实参个数、两本字典占位符一致，由 <c>LocalizedFormatCallSiteTests</c>（C1/C3）管 ——
/// 本文件**不复写**那两条规则，只在 J6 里断言实参**真的被填进去了**（那是它看不出的那一半）。</para>
/// </summary>
[Collection("Language-global")]
public class ActionTextLocalizationGuardTests
{
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    private static Dictionary<string, string> Zh =>
        PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");

    private static Dictionary<string, string> En =>
        PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    /// <summary>下界 —— 反射到 0 个常量会让 J1 的闭包**恒真**。</summary>
    private const int ExpectedTypeCount = 13;

    // ═══════════════════════════ 取数（同源出口） ═══════════════════════════

    /// <summary>
    /// 反射取 <see cref="ActionTypeMeta"/> 的公开 <c>const string</c>（当前 12 个动作 Id）。
    /// 与 <c>ActionPermissionMapTests</c> 同一套做法 —— 新增动作会被判据**自动**纳入。
    /// </summary>
    private static List<string> CoreActionTypeIds() =>
        typeof(ActionTypeMeta)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    /// <summary>13 个动作的**真实夹具**（用工厂方法造，不手搓 <c>ActionType</c> 字符串）。</summary>
    private static List<HmiAction> Fixtures() => new()
    {
        HmiAction.None,
        HmiAction.ToggleBit("v_motor"),
        HmiAction.SetBit("v_motor"),
        HmiAction.ResetBit("v_motor"),
        HmiAction.MomentaryOn("v_motor"),
        HmiAction.MomentaryOff("v_motor"),
        HmiAction.SetVariable("v_motor", "42"),
        HmiAction.SwitchScreen("Settings"),
        HmiAction.ExecuteScript("script1"),
        HmiAction.ShowAlarms,
        HmiAction.ShowTrends,
        HmiAction.Login,
        HmiAction.Logout,
    };

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

    // ═══════════════════════════ J1 ═══════════════════════════

    /// <summary>
    /// 表与 Core 的动作 Id 常量**双向**一对一 —— 漏一个 ⇒ 那个动作类型会在界面上静默显示成英文 Id。
    /// </summary>
    [Fact]
    public void MappedActionTypes_AndCoreConstants_AreOneToOne()
    {
        var mapped = ActionTextKeys.MappedActionTypes;
        var core = CoreActionTypeIds();

        Assert.True(mapped.Count >= ExpectedTypeCount,
            $"表里只有 {mapped.Count} 个动作类型（下界 {ExpectedTypeCount}）—— 判据会退化成恒真");
        Assert.Equal(mapped.Count, mapped.Distinct(StringComparer.Ordinal).Count());

        var noneId = HmiAction.None.ActionType;

        // 方向一：表里的每个 Id（除 None）都必须在 Core 的常量集里
        var notInCore = mapped
            .Where(id => !string.Equals(id, noneId, StringComparison.Ordinal) && !core.Contains(id))
            .ToList();
        Assert.True(notInCore.Count == 0,
            "表里出现了 ActionTypeMeta 未声明的动作 Id（改名后没同步）：" + string.Join(", ", notInCore));

        // 方向二：Core 的每个常量都必须在表里
        var notInTable = core.Where(id => !mapped.Contains(id, StringComparer.Ordinal)).ToList();
        Assert.True(notInTable.Count == 0,
            "ActionTypeMeta 声明了动作类型、但 ActionTextKeys 没覆盖它 ⇒ 界面上会显示英文 Id："
            + string.Join(", ", notInTable));

        // None 必须在内（它是下拉框与 chip 的默认项）
        Assert.Contains(noneId, mapped);
    }

    // ═══════════════════════════ J2 ═══════════════════════════

    /// <summary>
    /// 每个 Id 都必须**真的被翻译** —— 未映射的类型会**回落成原始 Id**，而回落的形状
    /// （结构上中性的 ASCII）看起来完全正常 ⇒ 不加这条，「漏映射」是静默的。
    /// </summary>
    [Fact]
    public void EveryMappedType_ReallyTranslates_AndNeverFallsBackToTheId()
    {
        InLanguage("zh-CN", () =>
        {
            var bad = new List<string>();
            foreach (var id in ActionTextKeys.MappedActionTypes)
            {
                var rendered = ActionTextKeys.TypeName(id);
                if (string.IsNullOrWhiteSpace(rendered)) bad.Add($"{id} ⇒ 空串");
                else if (string.Equals(rendered, id, StringComparison.Ordinal))
                    bad.Add($"{id} ⇒ 原样回落成 Id（该类型没有键）");
            }
            Assert.True(bad.Count == 0, "这些动作类型没有真正翻译：\n  " + string.Join("\n  ", bad));
        });
    }

    // ═══════════════════════════ J3 ═══════════════════════════

    /// <summary>
    /// zh 字典的 <c>action.type.*</c> 必须**逐字等于** Core 的产物表
    /// <see cref="ActionTypeMeta.DisplayName"/> —— 这两份是"同一件事的两条消费路径"，
    /// 必然漂移，所以用相等式钉住。**两侧都断言**：改字典 ⇒ 红；改 Core ⇒ 也红。
    /// </summary>
    [Fact]
    public void ChineseTypeNames_MatchTheProductTable_Verbatim()
    {
        var zh = Zh;
        InLanguage("zh-CN", () =>
        {
            var bad = new List<string>();
            foreach (var id in ActionTextKeys.MappedActionTypes)
            {
                if (string.Equals(id, HmiAction.None.ActionType, StringComparison.Ordinal)) continue;
                var product = ActionTypeMeta.DisplayName(id);
                var rendered = ActionTextKeys.TypeName(id);
                if (!string.Equals(rendered, product, StringComparison.Ordinal))
                    bad.Add($"{id}: 界面「{rendered}」 ≠ 产物「{product}」");
            }
            Assert.True(bad.Count == 0,
                "界面与产物两套措辞漂移了（二者必须逐字一致）：\n  " + string.Join("\n  ", bad));

            // 下界：至少查到了 12 个（反射失败 ⇒ 循环空转 ⇒ 恒真）
            Assert.True(zh.Keys.Count(k => k.StartsWith("action.type.", StringComparison.Ordinal))
                        >= ExpectedTypeCount - 1,
                "zh 字典里 action.type.* 的键太少 —— 判据可能空转");
        });
    }

    // ═══════════════════════════ J4 ═══════════════════════════

    /// <summary>
    /// **成对**断言：英文侧不许含汉字，**并且**中文侧确实含汉字。
    /// 只断前一半会被「夹具/字典本身就没有中文」满足（本项目踩过）。
    /// </summary>
    [Fact]
    public void EnglishTypeNames_HaveNoCjk_AndChineseOnesDo()
    {
        var zhWithoutCjk = new List<string>();
        var enLeaks = new List<string>();

        foreach (var id in ActionTextKeys.MappedActionTypes)
        {
            InLanguage("zh-CN", () =>
            {
                var v = ActionTextKeys.TypeName(id);
                if (!Cjk.IsMatch(v)) zhWithoutCjk.Add($"{id} ⇒ 「{v}」");
            });
            InLanguage("en-US", () =>
            {
                var v = ActionTextKeys.TypeName(id);
                if (Cjk.IsMatch(v)) enLeaks.Add($"{id} 的 en 值「{v}」含汉字");
            });
        }

        Assert.True(enLeaks.Count == 0,
            "英文界面上出现了中文动作名：\n  " + string.Join("\n  ", enLeaks));
        // ⚠ 这条是"成对"的关键：zh 侧**必须**是中文，否则上面那条可能只是没东西可查
        Assert.True(zhWithoutCjk.Count == 0,
            "这些动作的 zh 值不含汉字 —— 要么它落到了别的分支（比如原样回落的 Id），"
            + "要么字典/夹具有问题，总之上面那条英文断言因此没有分量：\n  "
            + string.Join("\n  ", zhWithoutCjk));
    }

    // ═══════════════════════════ J5 ═══════════════════════════

    /// <summary>
    /// zh 下 <c>Describe(action)</c> 必须与 <c>HmiAction.GetDescription()</c> **逐字相同**。
    ///
    /// <para>这条是「**零用户可见变化**」的**证明**（不是声明）：本刀把面板从 Core 的中文
    /// 改成走字典，若两者在中文下不完全一致，就等于顺手改了用户看到的东西 ——
    /// 那种改动必须单独登记，而不是混在本地化里悄悄发生。</para>
    ///
    /// <para>⚠ 期望值来自 **Core 的产物表**（另一侧），不是被测出口自己算的。</para>
    /// </summary>
    [Fact]
    public void ChineseDescriptions_AreByteIdenticalToTheCoreFallback()
    {
        InLanguage("zh-CN", () =>
        {
            var bad = new List<string>();
            foreach (var a in Fixtures())
            {
                var rendered = ActionTextKeys.Describe(a);
                var core = a.GetDescription();
                if (!string.Equals(rendered, core, StringComparison.Ordinal))
                    bad.Add($"{a.ActionType}: 界面「{rendered}」 ≠ 回落「{core}」");
            }
            Assert.True(bad.Count == 0,
                "zh 下界面渲染与 Core 回落不一致 ⇒ 本刀**改了用户可见文案**（须单独登记）：\n  "
                + string.Join("\n  ", bad));
            Assert.True(Fixtures().Count >= ExpectedTypeCount,
                $"夹具只有 {Fixtures().Count} 个动作（下界 {ExpectedTypeCount}）—— 判据在空转");
        });
    }

    // ═══════════════════════════ J6 ═══════════════════════════

    /// <summary>
    /// 英文侧：描述必须**带得上实参**（目标 / 取值），且不含汉字。
    /// <c>LocalizedFormatCallSiteTests</c> 只管「实参个数对不对」，管不到「实参有没有被填进模板」——
    /// 这一条补的就是那半边。
    /// </summary>
    [Fact]
    public void EnglishDescriptions_CarryTheArguments_AndHaveNoCjk()
    {
        InLanguage("en-US", () =>
        {
            var leaks = new List<string>();
            foreach (var a in Fixtures())
            {
                var v = ActionTextKeys.Describe(a);
                if (Cjk.IsMatch(v)) leaks.Add($"{a.ActionType}: 「{v}」");
            }
            Assert.True(leaks.Count == 0,
                "英文界面的动作描述里出现了中文：\n  " + string.Join("\n  ", leaks));

            var withTarget = ActionTextKeys.Describe(HmiAction.ToggleBit("v_motor"));
            Assert.Contains("v_motor", withTarget, StringComparison.Ordinal);

            var withTwoArgs = ActionTextKeys.Describe(HmiAction.SetVariable("v_motor", "42"));
            Assert.Contains("v_motor", withTwoArgs, StringComparison.Ordinal);
            Assert.Contains("42", withTwoArgs, StringComparison.Ordinal);

            // 无目标时用占位符 —— 不许留 `{0}` 字面量在界面上
            var noTarget = ActionTextKeys.Describe(new HmiAction { ActionType = ActionTypeMeta.ToggleBit });
            Assert.DoesNotContain("{0}", noTarget, StringComparison.Ordinal);
            Assert.DoesNotContain("{1}", noTarget, StringComparison.Ordinal);
        });
    }

    // ═══════════════════════════ J7 ═══════════════════════════

    /// <summary>
    /// **产物侧跨语言冻结**：切到英文界面后，Core 那两串中文（<see cref="ActionTypeMeta.DisplayName"/>
    /// 与 <see cref="HmiAction.GetDescription"/>）必须**逐字不变**。
    ///
    /// <para>这条守的是「拆出口」的另一半：如果哪天有人为了"让界面统一"顺手把 Core 那份也接了字典，
    /// 产物注释就会随编辑器语言变 ⇒ 同一工程两次导出不再逐字节一致（A/B 归一化直接坏掉）。
    /// 产物可复现性由 <c>tools/ReproHarness</c> 在另一个面上守，这里是**单元级**的那道闸。</para>
    /// </summary>
    [Fact]
    public void ProductSide_IsFrozen_AcrossLanguages()
    {
        var snapshot = new List<(string Type, string Desc)>();
        foreach (var a in Fixtures())
            snapshot.Add((ActionTypeMeta.DisplayName(a.ActionType), a.GetDescription()));

        InLanguage("en-US", () =>
        {
            var now = Fixtures()
                .Select(a => (Type: ActionTypeMeta.DisplayName(a.ActionType), Desc: a.GetDescription()))
                .ToList();
            for (var i = 0; i < snapshot.Count; i++)
            {
                Assert.Equal(snapshot[i].Type, now[i].Type);
                Assert.Equal(snapshot[i].Desc, now[i].Desc);
            }
        });

        // 下界：快照里必须真的有中文（否则"没变"是因为本来就没东西）
        Assert.True(snapshot.Count(a => Cjk.IsMatch(a.Type)) >= 12,
            $"产物表里只有 {snapshot.Count(a => Cjk.IsMatch(a.Type))} 条含汉字 —— 冻结判据没有分量");
        Assert.True(snapshot.Count(a => Cjk.IsMatch(a.Desc)) >= 13,
            $"产物回落里只有 {snapshot.Count(a => Cjk.IsMatch(a.Desc))} 条含汉字 —— 冻结判据没有分量");
    }

    // ═══════════════════════════ J8 ═══════════════════════════

    /// <summary>
    /// **端到端**：真实工程 → <see cref="CrossReferenceAnalyzer"/> → <see cref="ReferenceRow"/> 的渲染。
    ///
    /// <para>这条走的是**真实接缝**（不是手搓 <c>ReferenceLocation</c>）：它同时验证
    /// ① 分析器确实把动作挂到了 <c>Action</c> 上（挂漏了 ⇒ 行会回落成 Core 中文 ⇒ 英文侧断言红）；
    /// ② 行的渲染点确实读了字典。</para>
    /// </summary>
    [Fact]
    public void ReferenceRow_RendersTheActionDescription_EndToEnd()
    {
        var project = new HmiProject();
        project.VariableManager.Variables.Add(new Variable { Id = "v_motor", Name = "v_motor" });
        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        var control = new HmiControl
        {
            Id = "c1",
            Name = "StartBtn",
            Type = HmiControlType.Button,
            Action = HmiAction.SwitchScreen("Settings"),
        };
        // 让动作指向被查的变量：SwitchScreen 的目标是屏幕名，所以另加一个写变量动作
        control.Actions.Add(HmiAction.SetVariable("v_motor", "1"));
        screen.Controls.Add(control);
        project.Screens.Add(screen);

        var result = CrossReferenceAnalyzer.FindVariableReferences(project, "v_motor");
        var actionRefs = result.References.Where(r => r.ReferenceType == ReferenceType.ActionTarget).ToList();
        Assert.True(actionRefs.Count >= 1, "分析器没有返回动作引用 —— 端到端判据的前提不成立");
        Assert.True(actionRefs.All(r => r.Action != null),
            "分析器返回了动作引用，但没有把动作挂到 `ReferenceLocation.Action` 上 "
            + "⇒ 界面侧无法跨语言渲染（会回落成 Core 的中文）");

        var rows = actionRefs.Select(r => new ReferenceRow(r)).ToList();
        var coreFallbacks = actionRefs.Select(r => r.Description).ToList();

        InLanguage("zh-CN", () =>
        {
            foreach (var row in rows)
                Assert.StartsWith("动作 ", row.Description, StringComparison.Ordinal);
        });

        InLanguage("en-US", () =>
        {
            foreach (var row in rows)
            {
                Assert.False(Cjk.IsMatch(row.Description),
                    $"英文界面下交叉引用行仍是中文：「{row.Description}」");
                Assert.StartsWith("Action ", row.Description, StringComparison.Ordinal);
            }
            var withTarget = rows.Find(r => r.Description.Contains("v_motor", StringComparison.Ordinal));
            Assert.NotNull(withTarget);
        });

        // 回落原文不许被改写（它是 Core 的中文，两种语言下都一样）
        var after = actionRefs.Select(r => r.Description).ToList();
        Assert.Equal(coreFallbacks, after);
        Assert.True(coreFallbacks.All(d => Cjk.IsMatch(d)),
            "回落原文里没有汉字 —— 那它就不该叫「回落」，这条断言也没有分量");
    }

    // ═══════════════════════════ J9 ═══════════════════════════

    /// <summary>
    /// **诚实边界**：未登记的动作类型回落成原始 Id（结构上中性的 ASCII）。
    /// 这不是缺陷（原 <c>ActionTypeMeta.DisplayName</c> / <c>GetDescription</c> 同口径），
    /// 记录下来是为了让"回落到 Id"与"漏映射"能区分开 —— 后者由 J2 抓。
    /// </summary>
    [Fact]
    public void UnknownActionType_FallsBackToTheRawId()
    {
        const string unknown = "ZzzUnknownAction";
        Assert.DoesNotContain(unknown, ActionTextKeys.MappedActionTypes, StringComparer.Ordinal);

        InLanguage("zh-CN", () =>
        {
            Assert.Equal(unknown, ActionTextKeys.TypeName(unknown));
            var a = new HmiAction { ActionType = unknown };
            Assert.Equal(a.GetDescription(), ActionTextKeys.Describe(a));
            Assert.Equal(unknown, a.GetDescription()); // Core 侧的兜底：原样返回 Id
        });
    }
}
