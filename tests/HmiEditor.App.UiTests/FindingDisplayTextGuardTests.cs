using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「**同一条 finding 在界面上与复制出去的那行文字必须是同一套语言**」的守卫
/// —— 登记表 §4 <b>#61j</b>（阶段 92）。
///
/// <para><b>它守的是哪一类缺陷</b>：这一族的文案以前有**两条独立实现** ——
/// 对话框里的徽章/分类列走字典（<c>ValidationResultView.CreateItem</c> 的两个 <c>switch</c>），
/// 而「复制全部」拼的是 <c>$"[{f.Severity}] {f.Category}: …"</c>，
/// <b>直接插值枚举名</b> ⇒ 中文界面下界面上写着「结构」、复制出来却是 <c>Structure</c>：
/// 同一条 finding 在两处语言不同。收成一份（<see cref="FindingDisplayText"/>）之后两处**同源**。</para>
///
/// <para><b>三条独立的事，本文件各有一条判据</b>：
/// ① <b>同源</b>（<see cref="CopyLine_IsBuiltFromTheSameTextsAsTheUi_NotFromEnumNames"/>）——
///    复制行必须逐字含界面用的那两份文案，且**一个枚举名都不许出现**；
/// ② <b>枚举取值各有其文</b>（<see cref="EveryEnumValue_HasItsOwnDistinctText"/>）——
///    这是「默认支不许承担真实语义」的**行为面**证据：旧写法里 <c>Info</c> 与 <c>CodeGen</c>
///    正是由 <c>_ =&gt;</c> 兜着的，新增取值会静默落进它 ⇒ 两条 finding 显示同一个标签；
/// ③ <b>品牌档不进字典</b>（<see cref="BrandCategories_AreLanguageNeutral_AndAbsentFromTheDictionaries"/>）——
///    <c>Faceplate</c> / <c>LVGL API</c> 是产品/技术名，翻了反而错，
///    但「判过的不做」必须有判据钉住，否则下一个人顺手就把它翻掉。</para>
///
/// <para><b>诚实边界（本判据断不了的事）</b>：① 它只断「渲染等于字典模板」，断不了**翻译质量**
/// ；② 「视图里不许再冒出第二处 severity→文案 映射」只钉了 <c>FindingCategory.</c> 与手拼形状 ——
/// 视图里再用**裸中文字面量**拼一遍的写法由既有的 <c>HardcodedLocalizationGuardTests</c>
/// 兜着（那个面覆盖全 App），本文件**不重复**那道墙；
/// ③ 本判据**不断**「用户是否真的点了复制按钮」—— 剪贴板那一步不在判据面上。</para>
///
/// <para><b>与 <c>PairingViolations</c> 的一处刻意分家</b>：那条共享纯函数里有一条规则
/// 「两本字典的值一字不差 ⇒ 至少有一本没翻」，而 <c>finding.copy.line</c> 是**结构模板**
/// （方括号/冒号/圆括号 + 4 个槽位），两本**必须同值** —— 它翻的是**实参**，不是标点。
/// 全字典已有 <b>38 个</b>同值键（<c>ai.api.key</c> / <c>data.type.bool</c> /
/// <c>cb.resolution.1024x600</c> 这类技术名与字面量，2026-09-22 实测），本条是同一类的第 39 个；
/// 把「同值」当缺陷的那条规则对它就是**假阳性** ⇒ 本键的成对性由
/// <see cref="CopyLineTemplate_IsOneSkeleton_WithFourSlots"/> **双向**单独钉
/// （缺键 ⇒ 红、两本不同值 ⇒ 也红），而不是硬塞进那条共用的判定。</para>
/// </summary>
[Collection("Language-global")]
public class FindingDisplayTextGuardTests
{
    private readonly ITestOutputHelper _out;

    public FindingDisplayTextGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>毒化记号：拿它当"回落原文"，凡在渲染里冒头就说明字典键没被查到。</summary>
    private const string Poison = "<<中文回落原文未被使用>>";

    /// <summary>CJK 汉字 —— 用来抓「en-US 的值其实没翻」「中文档本身可用」。</summary>
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>唯一显示映射的所在地。</summary>
    internal const string MapFile = "src/HmiEditor.App/Services/FindingDisplayText.cs";

    /// <summary>消费点：列表项（徽章 + 分类列）与「复制全部」。</summary>
    internal const string ViewFile = "src/HmiEditor.App/Views/ValidationResultView.axaml.cs";

    /// <summary>分类键的公共前缀（品牌档**刻意**没有键，所以这个前缀下的键数是可判的）。</summary>
    private const string CategoryPrefix = "finding.category.";

    /// <summary>
    /// **刻意不翻**的两个分类 —— 品牌/技术名，翻了反而错。**双向**受判：
    /// 表里的档必须①不随语言变、②在两本字典里都没有键；
    /// 表外的档必须③随语言变（有人把一个普通分类做成品牌档 ⇒ 红）。
    /// </summary>
    internal static readonly (FindingCategory Value, string Text)[] BrandCategories =
    {
        (FindingCategory.Faceplate, FindingDisplayText.BrandFaceplate),
        (FindingCategory.LvglApi, FindingDisplayText.BrandLvglApi),
    };

    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    /// <summary>干净工程的最小合法代码：含 <c>create_screen_</c>、无未知 <c>lv_*</c>、无 WARNING 残留。</summary>
    private const string CleanCode = "void create_screen_main(void) { }";

    // ══════════════════════════ 取数（走真实产出路径，不手搓 finding） ══════════════════════════

    /// <summary>
    /// 三组夹具，覆盖三种分类来源：结构档（空项目）、**品牌档**（未知 LVGL API，
    /// 它的分类文案不查字典）、表达式档（实参里带记录体，见 #61i）。
    /// </summary>
    internal static List<GeneratedCodeFinding> Samples()
    {
        var hits = new List<GeneratedCodeFinding>();

        hits.AddRange(GeneratedCodeValidator.Validate(new HmiProject(), CleanCode));

        var brand = new HmiProject();
        NewScreen(brand, "main");
        hits.AddRange(GeneratedCodeValidator.Validate(
            brand, "void create_screen_main(void) { lv_totally_bogus(1); }"));

        var expr = new HmiProject();
        var screen = NewScreen(expr, "main");
        var control = new HmiControl { Id = "c1", Name = "value" };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            Kind = BindingSourceKind.Expression,
            Expression = "(1+2",
        });
        screen.Controls.Add(control);
        hits.AddRange(GeneratedCodeValidator.Validate(expr, CleanCode));

        Assert.NotEmpty(hits);
        return hits;
    }

    private static HmiScreen NewScreen(HmiProject project, string name)
    {
        // ScreenFactory.Create 只**造**屏幕、不加入工程（调用方负责），别漏 Add。
        var screen = ScreenFactory.Create(project, name: name);
        project.Screens.Add(screen);
        return screen;
    }

    /// <summary>枚举面：每个取值与它在**当前语言**下的文案。</summary>
    internal static List<(string Value, string Text)> SeverityTexts()
        => Enum.GetValues<FindingSeverity>()
            .Select(v => ($"FindingSeverity.{v}", FindingDisplayText.SeverityText(v)))
            .ToList();

    /// <summary>枚举面：分类档（含品牌两档）在当前语言下的文案。</summary>
    internal static List<(string Value, string Text)> CategoryTexts()
        => Enum.GetValues<FindingCategory>()
            .Select(v => ($"FindingCategory.{v}", FindingDisplayText.CategoryText(v)))
            .ToList();

    /// <summary>品牌档的取值集合（从 <see cref="BrandCategories"/> 推，不另抄一份）。</summary>
    private static HashSet<FindingCategory> BrandValues()
        => BrandCategories.Select(b => b.Value).ToHashSet();

    // ══════════════════════════ 判据逻辑（纯函数 —— 负控直接调它们） ══════════════════════════

    /// <summary>
    /// <b>枚举取值各有一份自己的文案</b>：非空 + **两两不同**。
    ///
    /// <para><b>为什么"两两不同"是关键那一维</b>：旧写法用 <c>_ =&gt;</c> 兜住
    /// <c>Info</c> / <c>CodeGen</c> 之外的所有取值 ⇒ 新增一个枚举取值时，
    /// 它拿到的正是默认支那份文案，于是**两条 finding 显示同一个标签**
    /// （用户看到一条假分类）。重复文案就是这个症状的可判形状 ——
    /// 而且它**独立于编译器**：即使有人给 switch 补回默认支（CS8509 消失），这条仍然红。</para>
    /// </summary>
    internal static List<string> DistinctnessViolations(IEnumerable<(string Value, string Text)> pairs)
    {
        var bad = new List<string>();
        var list = pairs.ToList();

        foreach (var (value, text) in list)
            if (string.IsNullOrWhiteSpace(text))
                bad.Add($"{value}：文案是空的 ⇒ 界面上那一格会是一片空白");

        foreach (var group in list.GroupBy(p => p.Text, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var owners = string.Join(" / ", group.Select(p => p.Value));
            bad.Add($"这些取值共用了同一份文案「{group.Key}」：{owners}"
                    + " ⇒ 有取值落进了别人的档（默认支承担真实语义的典型症状，用户会看到一条假标签）");
        }

        return bad;
    }

    /// <summary>
    /// <b>品牌档**双向**判定</b>（字典面 + 语言面）：
    /// ① <paramref name="zh"/> / <paramref name="en"/> 里 <c>finding.category.</c> 的键数
    ///    必须**恰好** = 枚举取值数 − 品牌档数（品牌加了键 ⇒ 多了；普通分类漏了键 ⇒ 少了）；
    /// ② 两本字典里**都不许**出现与品牌文案同值的值（名字无关的那种判法：
    ///    不猜品牌键叫什么，直接看有没有值等于 <c>Faceplate</c> / <c>LVGL API</c>）；
    /// ③ 品牌档的文案在当前语言下必须**就是**那个常量（不许随语言变）。
    /// </summary>
    internal static List<string> BrandViolations(
        int enumCount, int brandCount, Dictionary<string, string> zh, Dictionary<string, string> en)
    {
        var bad = new List<string>();
        var expected = enumCount - brandCount;

        foreach (var (label, dict) in new[] { ("zh-CN", zh), ("en-US", en) })
        {
            var keys = dict.Keys.Where(k => k.StartsWith(CategoryPrefix, StringComparison.Ordinal)).ToList();
            if (keys.Count != expected)
                bad.Add($"{label}：{CategoryPrefix}* 有 {keys.Count} 个键，但枚举 {enumCount} 个取值减掉 "
                        + $"{brandCount} 个品牌档应为 {expected} 个 ⇒ 品牌档被建了键，或普通分类漏了键");

            foreach (var brand in BrandCategories)
                if (dict.Any(kv => kv.Key.StartsWith(CategoryPrefix, StringComparison.Ordinal)
                                   && string.Equals(kv.Value, brand.Text, StringComparison.Ordinal)))
                {
                    bad.Add($"{label}：字典里有值为「{brand.Text}」的分类键 ⇒ 品牌档被翻/被建键了"
                            + $"（{brand.Value} 的文案必须是常量 {brand.Text}，不查字典）");
                }
        }

        foreach (var brand in BrandCategories)
        {
            var actual = FindingDisplayText.CategoryText(brand.Value);
            if (!string.Equals(actual, brand.Text, StringComparison.Ordinal))
                bad.Add($"{brand.Value}：文案是「{actual}」，不是品牌常量「{brand.Text}」");
        }

        return bad;
    }

    /// <summary>
    /// <b>复制行模板**双向**判定</b>：缺键 ⇒ 红；两本**不同值** ⇒ **也红**
    /// （同值是这条键的性质：它翻的是实参，标点不随语言变 —— 见类注释里那 38 个同值键的实测）。
    /// 槽位必须恰好是 <c>{0}</c>…<c>{3}</c>：少一个 ⇒ 界面上渲染出字面的花括号，
    /// 多一个 ⇒ 有实参没被用上。
    /// </summary>
    internal static List<string> TemplateViolations(string? zhTemplate, string? enTemplate)
    {
        var bad = new List<string>();

        if (zhTemplate is null) { bad.Add($"{Lang.FindingCopyLine}：zh-CN 缺键"); return bad; }
        if (enTemplate is null) { bad.Add($"{Lang.FindingCopyLine}：en-US 缺键"); return bad; }

        if (!string.Equals(zhTemplate, enTemplate, StringComparison.Ordinal))
            bad.Add($"{Lang.FindingCopyLine}：两本的模板不同值"
                    + $"（zh=「{zhTemplate}」/ en=「{enTemplate}」）⇒ 本键是**结构模板**，"
                    + "两本必须一字不差（要改标点就两本一起改，这正是它进字典的意义）");

        var slots = Regex.Matches(zhTemplate, @"\{(\d+)\}")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var want = new[] { "0", "1", "2", "3" };
        if (!slots.SequenceEqual(want, StringComparer.Ordinal))
            bad.Add($"{Lang.FindingCopyLine}：槽位是 {{{string.Join(", ", slots)}}}，"
                    + $"要求恰好 {{{string.Join(", ", want)}}}（级别 / 分类 / 描述 / 定位）");

        if (CsvImportIssueLocalizationGuardTests.CountPlaceholders(zhTemplate) != 4
            || CsvImportIssueLocalizationGuardTests.CountPlaceholders(enTemplate) != 4)
            bad.Add($"{Lang.FindingCopyLine}：占位符个数不是 4");

        return bad;
    }

    /// <summary>取一段 <c>switch</c> 表达式的体（从锚点之后到 <c>};</c> 之前）。</summary>
    private static string SwitchBody(string text, string anchor)
    {
        var idx = text.IndexOf(anchor, StringComparison.Ordinal);
        if (idx < 0) return "";
        var rest = text[(idx + anchor.Length)..];
        var end = rest.IndexOf("};", StringComparison.Ordinal);
        return end < 0 ? rest : rest[..end];
    }

    /// <summary>
    /// **源码面**：映射唯一、视图只消费不再自己拼、两个 <c>switch</c> 无默认支、抑制面只许 CS8524。
    ///
    /// <para><b>为什么这条非有不可</b>：本缺陷的本质是「同一件事有两条实现」——
    /// 只加一个映射类、视图不接线，用户所见一字不变（复制出来照旧是枚举名）。
    /// 而且"接线"这件事**必须由源码面断**：行为面测不出"视图里还留着一条没人走的老路"。</para>
    ///
    /// <para>剥注释是必须的：讲规则的注释里会写出反例（登记 #48 踩过三次）——
    /// 本阶段自己在映射文件的类注释里就写着旧形状那两段，不剥注释的话
    /// 「旧形状必须消失」会被**自己的文档**满足。</para>
    /// </summary>
    internal static List<string> MapViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();
        var map = sources.ToDictionary(s => s.Relative, s => SourceScanner.StripComments(s.Text),
            StringComparer.Ordinal);

        if (!map.TryGetValue(MapFile, out var mapper))
            bad.Add($"{MapFile}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            foreach (var probe in new[]
            {
                "SeverityText(FindingSeverity severity) => severity switch",
                "CategoryText(FindingCategory category) => category switch",
                "CopyLine(GeneratedCodeFinding finding)",
                "Lang.GenError",
                "Lang.GenWarning",
                "Lang.FindingCopyLine",
                "BrandFaceplate",
                "BrandLvglApi",
            })
                if (!mapper.Contains(probe, StringComparison.Ordinal))
                    bad.Add($"{MapFile}：缺 `{probe}` ⇒ 唯一显示映射少了一块");

            foreach (var anchor in new[]
            {
                "SeverityText(FindingSeverity severity) => severity switch",
                "CategoryText(FindingCategory category) => category switch",
            })
                if (SwitchBody(mapper, anchor).Contains("_ =>", StringComparison.Ordinal))
                    bad.Add($"{MapFile}：`{anchor}` 那个 switch 又出现 `_ =>` 默认支 ⇒ "
                            + "新增枚举取值会静默落进它的文案（用户看到一条假标签）");
        }

        if (!map.TryGetValue(ViewFile, out var view))
            bad.Add($"{ViewFile}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            foreach (var probe in new[]
            {
                "FindingDisplayText.SeverityText(finding.Severity)",
                "FindingDisplayText.CategoryText(finding.Category)",
                "_findings.Select(FindingDisplayText.CopyLine)",
                "#pragma warning disable CS8524",
                "#pragma warning restore CS8524",
            })
                if (!view.Contains(probe, StringComparison.Ordinal))
                    bad.Add($"{ViewFile}：缺 `{probe}` ⇒ 视图没接上唯一映射（缺陷本来的样子）");

            foreach (var shape in new[]
            {
                "f.Severity}]",          // 复制行手拼枚举名（本缺陷的签名）
                "f.Category}:",          // 同上，另一半
                "FindingCategory.",      // 分类映射的第二个实现
                "gen.error",             // 级别文案退回字面量键（已有 Lang 常量）
                "gen.warning",
            })
                if (view.Contains(shape, StringComparison.Ordinal))
                    bad.Add($"{ViewFile}：仍有 `{shape}` ⇒ 视图里还有第二条实现（绕过了唯一映射）");
        }

        // 唯一性：文案映射的**定义**只许有一处（行为面测不出"抄了一份一模一样的"）
        foreach (var signature in new[] { "static string SeverityText(", "static string CategoryText(" })
        {
            var owners = map.Where(kv => kv.Value.Contains(signature, StringComparison.Ordinal))
                .Select(kv => kv.Key)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
            if (owners.Count != 1)
                bad.Add($"{signature} 出现在 {owners.Count} 个文件（{string.Join(" / ", owners)}）"
                        + " ⇒ 显示映射不是唯一的，两条实现早晚漂移");
        }

        // 抑制面：只许抑制 CS8524（枚举 switch 的固定噪声），**CS8509 是新增取值的信号，不许关**
        foreach (var (relative, text) in map)
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("#pragma warning disable", StringComparison.Ordinal)) continue;
                if (!trimmed.Contains("CS8524", StringComparison.Ordinal))
                    bad.Add($"{relative}：`{trimmed}` 抑制的不是 CS8524 ⇒ 本项只许抑制那条噪声");
            }

        return bad;
    }

    // ══════════════════════════ 判据 ══════════════════════════

    /// <summary>
    /// 主判据（**同源**）：复制出去的每一行必须**逐字**由界面用的那两份文案拼成
    /// （级别、分类都走 <see cref="FindingDisplayText"/>），并且中文界面下
    /// **一个枚举名都不许出现** —— 那正是本缺陷的症状。
    /// </summary>
    [Fact]
    public void CopyLine_IsBuiltFromTheSameTextsAsTheUi_NotFromEnumNames()
    {
        var original = AppLocalization.Service.CurrentLanguage;
        try
        {
            AppLocalization.Service.SwitchLanguage("zh-CN");
            var zhTemplate = Zh[Lang.FindingCopyLine];
            AssertCopyLinesAreSourcedFromTheSameTexts(zhTemplate, expectNoEnumNames: true);
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }
    }

    private void AssertCopyLinesAreSourcedFromTheSameTexts(string template, bool expectNoEnumNames)
    {
        // 模板的前导部分（级别 + 分类两格）：拿它做"开头就是那两份文案"的判据，
        // 而不是在判据里再抄一遍方括号/冒号 —— 那些字符的权威在字典里。
        var prefixTemplate = template[..template.IndexOf("{2}", StringComparison.Ordinal)];
        var enumNames = Enum.GetNames<FindingSeverity>().Concat(Enum.GetNames<FindingCategory>()).ToList();
        var rows = new List<string>();
        var leaked = new List<string>();

        foreach (var finding in Samples())
        {
            var severityText = FindingDisplayText.SeverityText(finding.Severity);
            var categoryText = FindingDisplayText.CategoryText(finding.Category);
            var line = FindingDisplayText.CopyLine(finding);

            // ① 整行 == 模板 + 那四个实参（顺序也钉住：级别 / 分类 / 描述 / 定位）
            var expected = string.Format(
                CultureInfo.InvariantCulture, template,
                severityText, categoryText,
                ExternalText.Describe(finding), ExternalText.DescribeLocation(finding));
            Assert.Equal(expected, line);

            // ② 开头就是界面上那两份文案（徽章与分类列用的是同一对函数）
            var prefix = string.Format(CultureInfo.InvariantCulture, prefixTemplate, severityText, categoryText);
            Assert.StartsWith(prefix, line, StringComparison.Ordinal);

            // ③ 缺陷签名：枚举名冒头 ⇒ 与界面语言分家了
            if (expectNoEnumNames)
                foreach (var name in enumNames.Where(n => line.Contains(n, StringComparison.Ordinal)))
                    leaked.Add($"{finding.Code}：复制行里出现了枚举名「{name}」⇒ 复制出去的那行没走字典");

            rows.Add($"    {finding.Code,-44} ⇒ {line}");
        }

        JudgeReadings.Write(_out, "复制全部的行（#61j · finding.copy.line）", rows);

        Assert.True(leaked.Count == 0,
            "复制行与界面文案不同源（登记 #61j / 阶段 92）。正确动作是让它与徽章/分类列**共用**"
            + " FindingDisplayText 的映射，**不是**在 CopyAll 里再拼一遍枚举名：\n  - "
            + string.Join("\n  - ", leaked));
    }

    /// <summary>
    /// 每个枚举取值都必须有**自己的、非空的**文案 —— 见
    /// <see cref="DistinctnessViolations"/> 里"为什么两两不同才是关键那一维"。
    /// </summary>
    [Fact]
    public void EveryEnumValue_HasItsOwnDistinctText()
    {
        var severity = SeverityTexts();
        var category = CategoryTexts();
        Assert.True(severity.Count >= 3, $"只反射到 {severity.Count} 个级别 —— 取数失效（判定会恒真）");
        Assert.True(category.Count >= 8, $"只反射到 {category.Count} 个分类 —— 取数失效（判定会恒真）");

        JudgeReadings.Write(_out, "枚举取值 → 文案（#61j）",
            severity.Concat(category).Select(p => $"    {p.Value,-32} ⇒ {p.Text}").ToList());

        var problems = DistinctnessViolations(severity).Concat(DistinctnessViolations(category)).ToList();
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 品牌档**双向**：不进字典、不随语言变；其余档**必须**随语言变
    /// （否则有人把一个普通分类做成了品牌档 ⇒ 红）。
    /// </summary>
    [Fact]
    public void BrandCategories_AreLanguageNeutral_AndAbsentFromTheDictionaries()
    {
        var zh = Zh;
        var en = En;
        var brands = BrandValues();
        var all = Enum.GetValues<FindingCategory>();

        Assert.Equal(BrandCategories.Length, brands.Count);
        Assert.All(BrandCategories, b => Assert.NotEmpty(b.Text));

        var problems = BrandViolations(all.Length, brands.Count, zh, en);
        var rows = new List<string>();
        var original = AppLocalization.Service.CurrentLanguage;

        try
        {
            // 品牌档：两本字典下都必须**恰好**是那个常量
            foreach (var (value, text) in BrandCategories)
            {
                AppLocalization.Service.SwitchLanguage("en-US");
                var inEn = FindingDisplayText.CategoryText(value);
                AppLocalization.Service.SwitchLanguage("zh-CN");
                var inZh = FindingDisplayText.CategoryText(value);

                rows.Add($"    品牌 {value,-24} zh={inZh,-12} en={inEn}");
                if (!string.Equals(inEn, text, StringComparison.Ordinal)
                    || !string.Equals(inZh, text, StringComparison.Ordinal))
                    problems.Add($"{value}：文案随语言变了（zh=「{inZh}」/ en=「{inEn}」）"
                                 + $"⇒ 品牌名不该进字典，应恒为常量「{text}」");
            }

            // 非品牌档：两本必须不同（同一个值 ⇒ 至少有一本没配）
            foreach (var value in all.Where(v => !brands.Contains(v)))
            {
                AppLocalization.Service.SwitchLanguage("en-US");
                var inEn = FindingDisplayText.CategoryText(value);
                AppLocalization.Service.SwitchLanguage("zh-CN");
                var inZh = FindingDisplayText.CategoryText(value);

                rows.Add($"    普通 {value,-24} zh={inZh,-12} en={inEn}");
                if (string.Equals(inEn, inZh, StringComparison.Ordinal))
                    problems.Add($"{value}：两本语言下文案相同（「{inZh}」）⇒ 它不是品牌档，必须随语言变");
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }

        JudgeReadings.Write(_out, "分类档的语言行为（#61j）", rows);

        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 切语言各渲染一遍：**中文侧必须有汉字、英文侧一个都不许有**（品牌档除外，它两语言同形）。
    /// </summary>
    [Fact]
    public void BothLanguages_RenderInTheirOwnScript()
    {
        var brands = BrandValues();
        var original = AppLocalization.Service.CurrentLanguage;
        try
        {
            AppLocalization.Service.SwitchLanguage("zh-CN");
            var zhTexts = SeverityTexts().Concat(CategoryTexts()).ToList();
            // ⚠ 品牌档在中文侧也**不该**有汉字（它就是 ASCII 技术名），所以先排掉再断言。
            Assert.All(zhTexts.Where(p => !brands.Any(b => p.Value.EndsWith(b.ToString(), StringComparison.Ordinal))),
                p => Assert.Matches(Cjk, p.Text));

            AppLocalization.Service.SwitchLanguage("en-US");
            var enTexts = SeverityTexts().Concat(CategoryTexts()).ToList();

            JudgeReadings.Write(_out, "两种语言下的枚举文案（#61j）",
                zhTexts.Zip(enTexts, (z, e) => $"    {z.Value,-32} zh={z.Text,-14} en={e.Text}").ToList());

            var bad = enTexts.Where(p => Cjk.IsMatch(p.Text)).Select(p => $"{p.Value}：{p.Text}").ToList();
            Assert.True(bad.Count == 0,
                "英文下渲染出汉字（登记 #61j）⇒ 该档的 en 值没翻、或抄了 zh：\n  - " + string.Join("\n  - ", bad));

            // 非品牌档两语言必须真的不同（品牌档由 BrandCategories_… 单独管）
            var same = zhTexts.Zip(enTexts, (z, e) => (z, e))
                .Where(pair => !brands.Any(b => pair.z.Value.EndsWith(b.ToString(), StringComparison.Ordinal))
                               && string.Equals(pair.z.Text, pair.e.Text, StringComparison.Ordinal))
                .Select(pair => $"{pair.z.Value}：zh == en ==「{pair.z.Text}」")
                .ToList();
            Assert.True(same.Count == 0, string.Join("\n  - ", same));
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }
    }

    /// <summary>复制行模板：两本**同值** + 槽位恰好 <c>{0}…{3}</c>（见 <see cref="TemplateViolations"/>）。</summary>
    [Fact]
    public void CopyLineTemplate_IsOneSkeleton_WithFourSlots()
    {
        var zh = Zh;
        var en = En;

        zh.TryGetValue(Lang.FindingCopyLine, out var zhTemplate);
        en.TryGetValue(Lang.FindingCopyLine, out var enTemplate);

        var problems = TemplateViolations(zhTemplate, enTemplate);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));

        // 反向：这个键必须**真的**被映射用上（否则它是一句没人读的文案）。
        // ⚠ 找的是**标识符** `Lang.FindingCopyLine`，不是键的字面值 —— 源码里从不出现
        //   键的字面值（这正是本仓库"键只在字典、代码只引常量"的形态）。
        //   注释已剥 ⇒ 不会被映射文件自己的文档满足（登记 #48：名字即引用类判据的老病）。
        const string constRef = "Lang.FindingCopyLine";
        Assert.Contains(constRef, MapViolationsCorpus(MapFile), StringComparison.Ordinal);
    }

    /// <summary>取单个语料文件的**当前磁盘内容**（已剥注释）。</summary>
    private static string MapViolationsCorpus(string relative)
        => SourceScanner.StripComments(
            AppSourceScanner.ScanAllProductSources().Single(s => s.Relative == relative).Text);

    /// <summary>映射唯一、视图不再自己拼、无默认支、抑制面只许 CS8524。</summary>
    [Fact]
    public void MappingStaysSingleSourced()
    {
        var problems = MapViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "唯一显示映射的接线面（#61j）",
            new[] { MapFile, ViewFile }.Select(f => $"    {f}").ToList());

        Assert.True(problems.Count == 0,
            "显示映射这一半没收口（登记 #61j）。**只加映射类不算收口** —— "
            + "视图不接线时用户所见一字不变：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 抑制面**必须窄**：两个文件的 <c>#pragma</c> 只关 CS8524；
    /// 工程文件（<c>.csproj</c> / <c>.props</c>）里**不许**出现 CS8509 或 CS8524 ——
    /// 项目级 <c>NoWarn</c> 会把「新增枚举取值」的信号在**全仓**范围内静默关掉。
    /// </summary>
    [Fact]
    public void Suppression_StaysNarrow_NeverSilencesCs8509()
    {
        var root = AppSourceScanner.FindRepoRoot();
        var projectFiles = new List<string>();
        foreach (var relative in new[]
        {
            "Directory.Build.props", "Directory.Build.targets",
            "src/Directory.Build.props", "src/Directory.Build.targets",
        })
        {
            var full = System.IO.Path.Combine(root, relative);
            if (System.IO.File.Exists(full)) projectFiles.Add(full);
        }

        projectFiles.AddRange(System.IO.Directory
            .EnumerateFiles(System.IO.Path.Combine(root, "src"), "*.csproj", System.IO.SearchOption.AllDirectories)
            .Where(f => !AppSourceScanner.IsBuildOutput(f)));

        Assert.True(projectFiles.Count >= 2,
            $"只找到 {projectFiles.Count} 个工程文件 —— 扫描面失效（下面的判定会恒真）");

        var bad = new List<string>();
        foreach (var file in projectFiles)
        {
            var text = System.IO.File.ReadAllText(file);
            foreach (var code in new[] { "CS8509", "CS8524" })
                if (text.Contains(code, StringComparison.Ordinal))
                    bad.Add($"{System.IO.Path.GetRelativePath(root, file)}：出现了 {code} ⇒ "
                            + "抑制必须是映射文件里那条**文件级且只针对 CS8524** 的 #pragma，"
                            + "不许升格成项目级 NoWarn（那会把新增枚举取值的信号在全仓关掉）");
        }

        JudgeReadings.Write(_out, "抑制面（#61j）",
            projectFiles.Select(f => $"    {System.IO.Path.GetRelativePath(root, f)}").ToList());

        Assert.True(bad.Count == 0, string.Join("\n  - ", bad));
    }

    /// <summary>
    /// 负控 5 组 —— 每组都要求"该红的红、该绿的绿"，
    /// 否则证明不了判据真的在盯那件事（本项目对负控的一贯要求）。
    /// 这一组的坏形状**都在判据内合成**（不动仓库文件），另外三重落在真实文件上的负控
    /// 由阶段记录里的 NC-A/B/C 承担。
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        var zh = Zh;

        // ⚠ 本条的**所有**出入参都在判据内合成，刻意**不**读磁盘上的真语料/真字典/真枚举：
        //   拿真语料当正控的话，外部负控（把仓库临时改成坏形状）会让这条跟着红 ⇒
        //   读数分不清「判据坏了」与「仓库被注入了」，而本仓库要求两者可分辨。
        //   真语料那一侧由上面每条判据各自的 [Fact] 负责（那是它们的正控）。

        // ── NC1 · 枚举面：默认支把两个取值粘到一份文案上 ⇒ 点名；合规 ⇒ 绿 ──
        var cleanPairs = new[] { ("a", "甲"), ("b", "乙"), ("c", "丙") };
        Assert.Empty(DistinctnessViolations(cleanPairs));

        var glued = new[] { ("a", "甲"), ("b", "乙"), ("c", "甲") };
        Assert.Contains(DistinctnessViolations(glued), v => v.Contains("共用了同一份文案", StringComparison.Ordinal));

        var blank = new[] { ("a", "甲"), ("b", "") };
        Assert.Contains(DistinctnessViolations(blank), v => v.Contains("空的", StringComparison.Ordinal));

        // ── NC2 · 字典面：品牌补键 / 少一个普通分类键 ⇒ 各点名；正控绿 ──
        // 合成一本"6 个普通分类键"的字典 —— 与当前枚举面同形（8 个取值 − 2 个品牌档）。
        const int enumCount = 8;
        const int brandCount = 2;
        var cleanZh = new Dictionary<string, string>(StringComparer.Ordinal);
        var cleanEn = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < enumCount - brandCount; i++)
        {
            cleanZh[CategoryPrefix + "k" + i] = "分类" + i;
            cleanEn[CategoryPrefix + "k" + i] = "Category " + i;
        }

        Assert.Empty(BrandViolations(enumCount, brandCount, cleanZh, cleanEn));

        var withBrandKey = new Dictionary<string, string>(cleanZh, StringComparer.Ordinal)
        {
            [CategoryPrefix + "faceplate"] = FindingDisplayText.BrandFaceplate,
        };
        Assert.Contains(
            BrandViolations(enumCount, brandCount, withBrandKey, cleanEn),
            v => v.Contains("品牌档被翻", StringComparison.Ordinal));

        var minusOne = new Dictionary<string, string>(cleanZh, StringComparer.Ordinal);
        minusOne.Remove(CategoryPrefix + "k0");
        Assert.Contains(
            BrandViolations(enumCount, brandCount, minusOne, cleanEn),
            v => v.Contains("普通分类漏了键", StringComparison.Ordinal));

        // ── NC3 · 模板面：缺键 / 两本不同值 / 槽位不对 ⇒ 各点名；正控绿 ──
        Assert.Empty(TemplateViolations("[{0}] {1}: {2} ({3})", "[{0}] {1}: {2} ({3})"));
        Assert.Contains(TemplateViolations(null, "[{0}] {1}: {2} ({3})"),
            v => v.Contains("zh-CN 缺键", StringComparison.Ordinal));
        Assert.Contains(TemplateViolations("[{0}] {1}: {2} ({3})", "[{0}] {1} {2} ({3})"),
            v => v.Contains("两本的模板不同值", StringComparison.Ordinal));
        Assert.Contains(TemplateViolations("[{0}] {1}: {2}", "[{0}] {1}: {2}"),
            v => v.Contains("槽位", StringComparison.Ordinal));

        // ── NC4 · 源码面：视图退回手拼 / 映射长默认支 / 抑制面放宽 ⇒ 各点名；正控绿 ──
        //
        // ⚠ 正控用的是**合成**的合规语料，刻意**不**取磁盘上的真语料：拿真语料当正控的话，
        //   外部负控（把仓库临时改成坏形状）会让这条内嵌负控跟着红 ⇒ 读数分不清
        //   「判据坏了」与「仓库被注入了」（本仓库的负控纪律要求两者可分辨）。
        string[] mapperLines =
        {
            "static string SeverityText(FindingSeverity severity) => severity switch",
            "{",
            "    FindingSeverity.Error => AppLocalization.Service[Lang.GenError],",
            "    FindingSeverity.Warning => AppLocalization.Service[Lang.GenWarning],",
            "    FindingSeverity.Info => AppLocalization.Service[Lang.FindingLevelInfo],",
            "};",
            "static string CategoryText(FindingCategory category) => category switch",
            "{",
            "    FindingCategory.Structure => AppLocalization.Service[Lang.FindingCategoryStructure],",
            "};",
            "static string CopyLine(GeneratedCodeFinding finding) => F(Lang.FindingCopyLine);",
            "const string BrandFaceplate = \"Faceplate\";",
            "const string BrandLvglApi = \"LVGL API\";",
            "#pragma warning disable CS8524",
        };
        string[] viewLines =
        {
            "a = FindingDisplayText.SeverityText(finding.Severity);",
            "b = FindingDisplayText.CategoryText(finding.Category);",
            "c = _findings.Select(FindingDisplayText.CopyLine);",
            "#pragma warning disable CS8524",
            "#pragma warning restore CS8524",
        };
        var goodMapper = string.Join("\n", mapperLines);
        var goodView = string.Join("\n", viewLines);
        (string Relative, string Text)[] good = { (MapFile, goodMapper), (ViewFile, goodView) };
        (string Relative, string Text)[] Corpus(string target, string body)
            => good.Select(s => s.Relative == target ? (s.Relative, body) : s).ToArray();

        // ① 合规写法 ⇒ 无话可说（证明它不是"永远返回违规"的判据）
        Assert.Empty(MapViolations(good));

        // ② 视图退回手拼枚举名（本缺陷本来的样子）⇒ 手拼形状与"没接上映射"都点名
        var handBuiltBad = MapViolations(Corpus(ViewFile, goodView.Replace(
            "_findings.Select(FindingDisplayText.CopyLine)",
            "_findings.Select(f => $\"[{f.Severity}] {f.Category}: {f.Message}\")",
            StringComparison.Ordinal)));
        Assert.Contains(handBuiltBad, v => v.Contains("f.Severity}]", StringComparison.Ordinal));
        Assert.Contains(handBuiltBad, v => v.Contains("CopyLine", StringComparison.Ordinal));

        // ③ 映射给 switch 补 `_ =>` 默认支 ⇒ 点名（这是"编译器被哄好、用户还是看到假标签"那条路）
        var withDefault = Corpus(MapFile, goodMapper.Replace(
            "    FindingSeverity.Info => AppLocalization.Service[Lang.FindingLevelInfo],",
            "    FindingSeverity.Info => AppLocalization.Service[Lang.FindingLevelInfo],\n    _ => \"别的\",",
            StringComparison.Ordinal));
        Assert.Contains(MapViolations(withDefault), v => v.Contains("默认支", StringComparison.Ordinal));

        // ④ 抑制面放宽成 CS8509 ⇒ 点名（那等于把"新增枚举取值"的信号关掉）
        var widened = Corpus(MapFile, goodMapper.Replace(
            "#pragma warning disable CS8524", "#pragma warning disable CS8509", StringComparison.Ordinal));
        Assert.Contains(MapViolations(widened), v => v.Contains("只许抑制那条噪声", StringComparison.Ordinal));

        // ⑤ 映射被抄成第二份（同一签名出现在两个文件）⇒ 唯一性那条点名
        var duplicated = MapViolations(new[]
        {
            (MapFile, goodMapper),
            (ViewFile, goodView + "\nstatic string CategoryText(FindingCategory category) => category switch { };"),
        });
        Assert.Contains(duplicated, v => v.Contains("不是唯一的", StringComparison.Ordinal));

        // ── NC5 · 语料缺文件 ⇒ 必须报"找不到"（防扫描面失效时静默全绿，这是下界） ──
        Assert.Contains(MapViolations(new[] { (ViewFile, goodView) }),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ── 注释里写的旧形状不算（规则不许被自己的文档满足，登记 #48） ──
        var commented = Corpus(ViewFile,
            goodView + "\n// 反例：这里**不**该写 $\"[{f.Severity}] {f.Category}: ...\"");
        Assert.Empty(MapViolations(commented));

        // ── 毒化面：复制行走的是**字典渲染**，不是把 finding.Message 直出 ──
        //    把回落原文换成记号 ⇒ 复制行必须一个字都不变（字典接不上就会露馅）。
        var sample = Samples()[0];
        var poisonedLine = FindingDisplayText.CopyLine(sample with { Message = Poison });
        Assert.Equal(FindingDisplayText.CopyLine(sample), poisonedLine);
        Assert.DoesNotContain(Poison, poisonedLine, StringComparison.Ordinal);

        // 上面 NC2/NC3 的正控是**合成**字典（刻意不碰真字典），所以这里补一句把真字典钉住：
        // 那一句"结构模板"必须真的在 zh-CN 里。
        Assert.True(zh.ContainsKey(Lang.FindingCopyLine),
            $"{Lang.FindingCopyLine}：zh-CN 里没有这个键 ⇒ 复制行会渲染成字面的花括号");
    }
}
