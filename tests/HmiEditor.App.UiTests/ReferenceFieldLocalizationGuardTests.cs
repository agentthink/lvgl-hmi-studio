using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **交叉引用面板「另外三列枚举名」的本地化判据**（阶段 96，闭环登记 <b>#61o</b>）。
///
/// <para><b>本项缺陷的形状</b>：面板一行里有三个枚举名直接 <c>ToString()</c> ——
/// 行首蓝色小字是 <see cref="ReferenceType"/>（<c>VariableBinding</c>）、右侧灰字是
/// <see cref="HmiControlType"/>（<c>Label</c>）、以及 <c>Description</c> 模板 <c>{1}</c> 里嵌的
/// <see cref="BindingDirection"/>（<c>ReadWrite</c>）⇒ 中文界面下这三个都是英文枚举名。
/// 阶段 94（#61m）与 95（#61n）分别收掉了动作行与其余描述行，注释里两次写下
/// 「一次只放开一个变量」，本阶段把剩下的三个一次收掉。</para>
///
/// <para><b>为什么控件类型要另立一个出口</b>：那个映射在界面上有**三个**露出点
/// （工具箱条目名 / 画布新建控件的默认名 / 本面板的类型列），而改动前是**两份手工维护的
/// <c>Lang.Tb*</c> 表**，且第 ② 份**已经分叉**（漏 <c>Screen</c> ⇒ 拖「屏幕」建出来的控件
/// 默认名是英文 <c>Screen1</c>）。所以本刀把它收敛成 <see cref="ControlTypeTextKeys"/> 一处
/// —— K7 就是钉「没有第二份」的那条。</para>
///
/// <para><b>判据之间的分工</b>（缺一条就有一件看不见）：
/// <list type="number">
/// <item>K1 26 个控件类型**每个都有名字、两两不同**（防「都落进同一支」）；</item>
/// <item>K2 这些名字确实**来自字典的 <c>tb.*</c> 值集**（不是就地拼的、也不是英文枚举名）；</item>
/// <item>K3 zh 侧 26 个渲染**都含汉字**（<c>ToString()</c> 必不含 ⇒ 这条同时钉「没有再直显枚举名」）；</item>
/// <item>K4 / K5 <see cref="ReferenceType"/> 四个成员、<see cref="BindingDirection"/> 三个成员逐档随语言变；</item>
/// <item>K6 **端到端**：真实工程 → 真实分析器 → <see cref="ReferenceRow"/>，en 下三列无汉字、zh 下有；</item>
/// <item>K7 **单一出口的接线**（源码面）：三个调用点都调 <see cref="ControlTypeTextKeys.Describe"/>，
///   且工具箱/画布**不再**出现 <c>Lang.Tb*</c> 的控件名索引（防它悄悄长回第二份）；</item>
/// <item>K8 七条新键在两本字典里都翻过（缺 en 会直接显示键名）。</item>
/// </list></para>
///
/// <para><b>不在这里守的</b>：占位符 ↔ 实参个数、两本字典占位符一致由
/// <c>LocalizedFormatCallSiteTests</c>（C1/C3）管；<c>Description</c> 的模板分派与
/// 「zh 相对 Core 回落」由 <c>ReferenceDescriptionLocalizationGuardTests</c>（J1–J9）管
/// —— 本文件不重写那两条规则，只在 K6 里顺带穿过 <c>Description</c> 一次。</para>
/// </summary>
[Collection("Language-global")]
public class ReferenceFieldLocalizationGuardTests
{
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    private static Dictionary<string, string> Zh =>
        PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");

    private static Dictionary<string, string> En =>
        PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    /// <summary>下界 —— 反射到 0 个成员会让下面几条判据**恒真**。</summary>
    private const int ExpectedControlTypeCount = 26;

    private const int ExpectedReferenceTypeCount = 4;

    private const int ExpectedDirectionCount = 3;

    /// <summary>本刀新增的七条键（引用类型 4 ＋ 方向 3）。</summary>
    private static readonly string[] NewKeys =
    {
        "cross.ref.type.variable_binding",
        "cross.ref.type.action_target",
        "cross.ref.type.script_binding",
        "cross.ref.type.expression_variable",
        "cross.ref.dir.readonly",
        "cross.ref.dir.writeonly",
        "cross.ref.dir.readwrite",
    };

    // ═══════════════════════════ 夹具 ═══════════════════════════

    /// <summary>反射取一个枚举的全部**具名**成员（不写名单 —— 名单即引用，见 J9 的注释）。</summary>
    private static List<T> Members<T>() where T : struct, Enum =>
        typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (T)f.GetRawConstantValue()!)
            .ToList();

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

    /// <summary>
    /// 一个**真实**工程：一个屏幕、一个罐体控件、一条读写方向的变量绑定
    /// ⇒ <c>FindVariableReferences</c> 产出恰好一行（<c>VariableBinding</c>，
    /// 且 <c>ControlType = Tank</c>）。
    ///
    /// <para>用真实模型构造、不用手搓 <see cref="ReferenceLocation"/>：手搓会让
    /// 「Core 有没有把字段填进去」与「App 有没有读它」分开自证，K6 就没有分量了。</para>
    /// </summary>
    private static HmiProject FixtureProject()
    {
        var project = new HmiProject();
        project.VariableManager.Variables.Add(new Variable { Id = "v_tank", Name = "v_tank" });

        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        var control = new HmiControl { Id = "c1", Name = "TankLabel", Type = HmiControlType.Tank };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Text",
            VariableId = "v_tank",
            // ⚠ 刻意用非默认值：字段忘赋值时默认是 ReadOnly ⇒ K5/K6 会点名
            Direction = BindingDirection.WriteOnly,
        });
        screen.Controls.Add(control);
        project.Screens.Add(screen);

        return project;
    }

    private static List<ReferenceLocation> FixtureRows() =>
        CrossReferenceAnalyzer.FindVariableReferences(FixtureProject(), "v_tank").References;

    // ═══════════════════════════ K1 ═══════════════════════════

    /// <summary>
    /// 26 个控件类型**每个都有名字**、且这些名字**两两不同**。
    ///
    /// <para>第二半是防「漏分支落到同一支」的取证：若某几支被写成同一个键（或都回落到
    /// 枚举名），它们会**成对相同** ⇒ 在这里点名。</para>
    /// </summary>
    [Fact]
    public void EveryControlType_HasItsOwnDictionaryName()
    {
        var types = Members<HmiControlType>();
        Assert.True(types.Count >= ExpectedControlTypeCount,
            $"只反射到 {types.Count} 个 HmiControlType 成员（下界 {ExpectedControlTypeCount}）—— 判据在空转");

        var duplicates = new List<string>();
        // 必须**先初始化**：`InLanguage` 只在运行期调 body，编译器不认「lambda 一定跑过」
        // ⇒ 留空声明会报 CS0165（本判据第一版就是这么挂的）。
        var names = Array.Empty<string>();

        InLanguage("zh-CN", () =>
        {
            names = types.Select(ControlTypeTextKeys.Describe).ToArray();
            var grouped = names.GroupBy(n => n, StringComparer.Ordinal).Where(g => g.Count() > 1);
            foreach (var g in grouped)
                duplicates.Add($"「{g.Key}」被 {g.Count()} 个控件类型共用");
        });

        var empty = names.Where(string.IsNullOrWhiteSpace).ToList();

        Assert.True(empty.Count == 0, $"这些控件类型渲染成了空串：{empty.Count} 个");
        Assert.True(duplicates.Count == 0,
            "有控件类型共用同一个显示名（漏分支 / 写到同一支 / 回落成枚举名会让某一对相同）：\n  "
            + string.Join("\n  ", duplicates));
    }

    // ═══════════════════════════ K2 ═══════════════════════════

    /// <summary>
    /// 这些名字确实**来自字典的 <c>tb.*</c> 值集** —— 且**英文枚举名不在那个值集里**的成员
    /// 也一并点名。
    ///
    /// <para>为什么用「值集」而不是「逐键对拍」：逐键对拍要在判据里再写一份
    /// 「枚举成员 → 键」的映射，那就是**第三份表**（两处一起改就都绿了，判据没有分量）。
    /// 用字典的**值**当输入面，判据不依赖任何名单。</para>
    /// </summary>
    [Fact]
    public void ControlTypeNames_ComeFromTheToolboxValueSet()
    {
        var types = Members<HmiControlType>();

        // tb.* 里排除 tb.cat.*（分类名不是控件名）
        var zhValues = Zh.Where(kv => kv.Key.StartsWith("tb.", StringComparison.Ordinal)
                                      && !kv.Key.StartsWith("tb.cat.", StringComparison.Ordinal))
            .Select(kv => kv.Value)
            .ToHashSet(StringComparer.Ordinal);
        var enValues = En.Where(kv => kv.Key.StartsWith("tb.", StringComparison.Ordinal)
                                      && !kv.Key.StartsWith("tb.cat.", StringComparison.Ordinal))
            .Select(kv => kv.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(zhValues.Count >= 20,
            $"只从 zh 字典收到 {zhValues.Count} 个 tb.* 值 —— 扫描面找错了");

        var notFromZh = new List<string>();
        var notFromEn = new List<string>();

        InLanguage("zh-CN", () =>
        {
            foreach (var t in types)
            {
                var n = ControlTypeTextKeys.Describe(t);
                if (!zhValues.Contains(n)) notFromZh.Add($"{t} → 「{n}」");
            }
        });
        InLanguage("en-US", () =>
        {
            foreach (var t in types)
            {
                var n = ControlTypeTextKeys.Describe(t);
                if (!enValues.Contains(n)) notFromEn.Add($"{t} → 「{n}」");
            }
        });

        Assert.True(notFromZh.Count == 0,
            "这些控件类型的 zh 显示名不在字典 tb.* 的值集里 ⇒ 它是就地拼的、或已漂出字典：\n  "
            + string.Join("\n  ", notFromZh));
        Assert.True(notFromEn.Count == 0,
            "这些控件类型的 en 显示名不在字典 tb.* 的值集里：\n  " + string.Join("\n  ", notFromEn));
    }

    // ═══════════════════════════ K3 ═══════════════════════════

    /// <summary>
    /// zh 侧 26 个渲染**都含汉字**。
    ///
    /// <para>改动前那一列是 <c>ControlType.ToString()</c>（纯 ASCII 枚举名）⇒ 全都不含汉字；
    /// 所以这条同时钉住「没有再直显枚举名」，而且**不需要**写任何枚举名清单。</para>
    /// </summary>
    [Fact]
    public void ChineseControlTypeNames_AllCarryCjk()
    {
        var types = Members<HmiControlType>();
        var ascii = new List<string>();

        InLanguage("zh-CN", () =>
        {
            foreach (var t in types)
            {
                var n = ControlTypeTextKeys.Describe(t);
                if (!Cjk.IsMatch(n)) ascii.Add($"{t} → 「{n}」（不含汉字）");
            }
        });

        Assert.True(ascii.Count == 0,
            "中文界面下这些控件类型的名字不含汉字 ⇒ 这一档没有走字典（多半是回落成枚举名）：\n  "
            + string.Join("\n  ", ascii));
    }

    // ═══════════════════════════ K4 ═══════════════════════════

    /// <summary>引用类型四个成员逐档随语言变（含当前**零产出**的 <c>ExpressionVariable</c>）。</summary>
    [Fact]
    public void EveryReferenceType_ChangesWithLanguage()
    {
        var types = Members<ReferenceType>();
        Assert.True(types.Count >= ExpectedReferenceTypeCount,
            $"只反射到 {types.Count} 个 ReferenceType 成员（下界 {ExpectedReferenceTypeCount}）");

        var bad = new List<string>();
        foreach (var t in types)
        {
            var zh = "";
            var en = "";
            InLanguage("zh-CN", () => zh = ReferenceTextKeys.TypeName(t));
            InLanguage("en-US", () => en = ReferenceTextKeys.TypeName(t));

            if (string.Equals(zh, en, StringComparison.Ordinal))
                bad.Add($"{t}: zh 与 en 都是「{zh}」⇒ 这一档没有查字典");
            else if (!Cjk.IsMatch(zh))
                bad.Add($"{t}: zh「{zh}」不含汉字");
            else if (Cjk.IsMatch(en))
                bad.Add($"{t}: en「{en}」含汉字");
        }

        Assert.True(bad.Count == 0,
            "有引用类型没有真的走字典：\n  " + string.Join("\n  ", bad));
    }

    // ═══════════════════════════ K5 ═══════════════════════════

    /// <summary>
    /// 绑定方向三个成员逐档随语言变 —— 尤其是 <c>WriteOnly</c>：改动前它作为
    /// <c>Format</c> 的实参被装箱 ToString()，界面显示 <c>WriteOnly</c>。
    /// </summary>
    [Fact]
    public void EveryBindingDirection_ChangesWithLanguage()
    {
        var directions = Members<BindingDirection>();
        Assert.True(directions.Count >= ExpectedDirectionCount,
            $"只反射到 {directions.Count} 个 BindingDirection 成员（下界 {ExpectedDirectionCount}）");

        var bad = new List<string>();
        foreach (var d in directions)
        {
            var zh = "";
            var en = "";
            InLanguage("zh-CN", () => zh = ReferenceTextKeys.DirectionName(d));
            InLanguage("en-US", () => en = ReferenceTextKeys.DirectionName(d));

            if (string.Equals(zh, en, StringComparison.Ordinal))
                bad.Add($"{d}: zh 与 en 都是「{zh}」⇒ 这一档没有查字典");
            else if (!Cjk.IsMatch(zh))
                bad.Add($"{d}: zh「{zh}」不含汉字");
        }

        Assert.True(bad.Count == 0,
            "有绑定方向没有真的走字典：\n  " + string.Join("\n  ", bad));
    }

    // ═══════════════════════════ K6 ═══════════════════════════

    /// <summary>
    /// **端到端**：真实工程 → 真实分析器 → <see cref="ReferenceRow"/> 的**三列**。
    ///
    /// <para>用 <see cref="ReferenceRow"/> 而不是直接调三个出口：这一层才是模板真正绑定的东西
    /// （<c>CrossReferenceView.axaml</c> 的 <c>x:DataType</c>），只测出口会漏掉「属性没转发」。</para>
    /// </summary>
    [Fact]
    public void ReferenceRows_RenderAllThreeFields_InEnglish()
    {
        var rows = FixtureRows();
        Assert.True(rows.Count >= 1, $"分析器没返回任何行（{rows.Count}）—— 端到端判据的前提不成立");

        var sample = rows[0];
        var row = new ReferenceRow(sample);

        var zhType = "";
        var enType = "";
        var zhCtrl = "";
        var enCtrl = "";
        var enDesc = "";
        var zhDesc = "";

        InLanguage("zh-CN", () =>
        {
            zhType = row.ReferenceType;
            zhCtrl = row.ControlType;
            zhDesc = row.Description;
        });
        InLanguage("en-US", () =>
        {
            enType = row.ReferenceType;
            enCtrl = row.ControlType;
            enDesc = row.Description;
        });

        var leaks = new List<string>();
        if (Cjk.IsMatch(enType)) leaks.Add($"ReferenceType: en「{enType}」");
        if (Cjk.IsMatch(enCtrl)) leaks.Add($"ControlType: en「{enCtrl}」");
        if (Cjk.IsMatch(enDesc)) leaks.Add($"Description: en「{enDesc}」");

        Assert.True(leaks.Count == 0,
            "英文界面下交叉引用行仍有中文（三列都要无汉字，Description 那一列由上两刀的判据负责细节）：\n  "
            + string.Join("\n  ", leaks));

        // 成对：zh 侧确实含汉字，否则上面那条被「夹具根本没中文」满足
        var zhMissing = new List<string>();
        if (!Cjk.IsMatch(zhType)) zhMissing.Add($"ReferenceType zh「{zhType}」");
        if (!Cjk.IsMatch(zhCtrl)) zhMissing.Add($"ControlType zh「{zhCtrl}」");
        if (!Cjk.IsMatch(zhDesc)) zhMissing.Add($"Description zh「{zhDesc}」");
        Assert.True(zhMissing.Count == 0,
            "zh 侧这些列不含汉字 ⇒ 英文那条断言没有分量：\n  " + string.Join("\n  ", zhMissing));

        // 三列都随语言真的变了（不是两边都恰好相同）
        Assert.True(!string.Equals(zhType, enType, StringComparison.Ordinal),
            $"ReferenceType 不随语言变：「{zhType}」/「{enType}」");
        Assert.True(!string.Equals(zhCtrl, enCtrl, StringComparison.Ordinal),
            $"ControlType 不随语言变：「{zhCtrl}」/「{enCtrl}」");
    }

    // ═══════════════════════════ K7 ═══════════════════════════

    /// <summary>
    /// **单一出口的接线**（源码面）—— 三件事一起钉：
    /// <list type="number">
    /// <item>三个调用点都**调了** <c>ControlTypeTextKeys.Describe</c>；</item>
    /// <item>工具箱与画布**不再**出现「<c>Lang.Tb*</c> 控件名」的字典索引（分类键 <c>Lang.TbCat*</c>
    ///   不在此列）⇒ 防它悄悄长回第二份手工表；</item>
    /// <item><see cref="ReferenceRow"/> 的两个属性**不再**是 <c>ToString()</c>。</item>
    /// </list>
    ///
    /// <para><b>为什么不写成「文件里不许出现 <c>Lang.Tb</c> 字样」</b>：那会把分类键与注释一起误伤。
    /// 这里只在**剥掉注释**后的代码面上匹配「字典索引 + <c>Lang.Tb</c> 且后一个字符不是 <c>C</c>」。</para>
    /// </summary>
    [Fact]
    public void SingleOutlet_IsWiredFromAllThreeCallSites()
    {
        var secondTable = new Regex(@"AppLocalization\.Service\[Lang\.Tb(?!Cat)", RegexOptions.Compiled);

        var checks = new (string File, bool RequireDescribe, bool NoSecondTable)[]
        {
            ("ToolboxView.axaml.cs", true, true),
            ("DesignCanvas.Editing.cs", true, true),
            ("ReferenceRow.cs", true, false),
        };

        var problems = new List<string>();

        foreach (var (file, requireDescribe, noSecondTable) in checks)
        {
            var (relative, text) = AppSourceScanner.Single(file);
            var code = SourceScanner.StripComments(text);

            if (requireDescribe && !code.Contains("ControlTypeTextKeys.Describe", StringComparison.Ordinal))
                problems.Add($"{relative}: 没有调用 ControlTypeTextKeys.Describe");

            if (noSecondTable && secondTable.IsMatch(code))
                problems.Add($"{relative}: 仍有 Lang.Tb* 的控件名索引 ⇒ 第二份手工表长回来了");
        }

        // ReferenceRow 的两个属性必须转发到出口，而不是 ToString()
        var (rowRelative, rowText) = AppSourceScanner.Single("ReferenceRow.cs");
        var rowCode = SourceScanner.StripComments(rowText);
        foreach (var prop in new[] { "ReferenceType", "ControlType" })
        {
            var pattern = new Regex($"public string {prop} =>[^;]*;", RegexOptions.Compiled);
            var m = pattern.Match(rowCode);
            if (!m.Success)
            {
                problems.Add($"{rowRelative}: 找不到 {prop} 的属性体");
                continue;
            }
            if (m.Value.Contains("ToString()", StringComparison.Ordinal))
                problems.Add($"{rowRelative}: {prop} 还在 ToString()");
        }

        Assert.True(problems.Count == 0,
            "控件类型显示名的**单一出口**没有接齐：\n  " + string.Join("\n  ", problems));
    }

    // ═══════════════════════════ K8 ═══════════════════════════

    /// <summary>七条新键在两本字典里都翻过（缺 en ⇒ 切英文后界面直接显示键名）。</summary>
    [Fact]
    public void SevenNewKeys_AreTranslatedInBothDictionaries()
    {
        var missing = new List<string>();
        foreach (var key in NewKeys)
        {
            if (!Zh.ContainsKey(key)) missing.Add($"{key}: zh-CN 缺失");
            else if (!En.ContainsKey(key)) missing.Add($"{key}: en-US 缺失");
            else if (string.Equals(Zh[key], En[key], StringComparison.Ordinal))
                missing.Add($"{key}: zh 与 en 同值（「{Zh[key]}」）⇒ 等于没翻");
        }

        Assert.True(NewKeys.Length >= 7, $"新键清单只有 {NewKeys.Length} 条 —— 本判据在空转");
        Assert.True(missing.Count == 0,
            "本刀新增的键没有翻全：\n  " + string.Join("\n  ", missing));
    }
}
