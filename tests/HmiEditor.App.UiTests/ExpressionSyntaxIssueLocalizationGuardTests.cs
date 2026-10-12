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
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「**表达式语法失败的原因必须由字典决定**」的守卫 —— 登记表 §4 <b>#61i</b>（阶段 91）。
///
/// <para><b>它守的是哪一类缺陷</b>：<see cref="ExpressionEngine"/> 的语法错误以前是
/// <c>throw new FormatException("缺少右括号 ')'")</c> —— 消息是**中文裸串**，
/// 而 <c>GeneratedCodeValidator</c> 把它当 <c>{1}</c> 实参塞进
/// <c>gen.issue.expression_syntax</c>（<c>表达式语法错误: {0} ({1})</c>）
/// ⇒ 界面切成英文时**括号里的原因那一半仍是中文**
/// （工具栏「生成代码」→ 验证结果对话框里可见）。这既不是"没翻译"也不是"翻译错了"，
/// 而是**失败原因没有码**，字典再怎么补也接不住它。</para>
///
/// <para><b>与 <c>LuaSyntaxValidator</c> 的一处关键差别</b>：那一族的产出方只拿得到
/// MoonSharp 的**字符串**消息 ⇒ 只能 <c>String.Contains</c> 反推码（脆弱，实测被版本差异咬过）。
/// 本族的产出方是**我们自己的递归下降解析器** ⇒ 码在**抛错点**直接给出，
/// 因此本文件里**一处字符串反推都没有** —— 那是这一族比那一族强的地方，别退回去。</para>
///
/// <para><b>它是「返回值带码」那一档</b>：<see cref="ExpressionEngine.Compile"/> 抛
/// <see cref="ExpressionSyntaxException"/> 只是因为它的返回契约是 AST；
/// 真正的消费入口是 <see cref="ExpressionEngine.TryCompile"/>（返回
/// <see cref="ExpressionSyntaxError"/>）。三条搬运路径共用同一套码：</para>
/// <list type="number">
///   <item>编辑器 UI（属性面板的表达式输入框下方红字）—— 走 <c>ExternalText.Describe(error)</c>；</item>
///   <item>生成代码验证对话框（<c>gen.issue.expression_syntax</c> 的 <c>{1}</c>）——
///     走 <c>ExternalText.Describe(GeneratedCodeFinding)</c> 的**嵌套实参渲染**
///     （这是本仓库第一处嵌套，见 <see cref="ExpressionSyntaxFinding_RendersTheNestedReason_NotTheRecordDump"/>）；</item>
///   <item>产物注释（<c>CodeGenerator.Screen.cs</c> 写进 <c>generated_ui.c</c> 的
///     <c>// WARNING:</c>）—— **刻意不本地化**，用中文回落原文
///     （与 <c>GeneratedCodeIssueCodes.GENERATOR_WARNING_COMMENT</c> 同一类"判过的不做"，登记 #61k）。</item>
/// </list>
///
/// <para><b>诚实边界（本判据断不了的事）</b>：① 它只断「渲染等于字典模板」，断不了
/// <b>翻译质量</b>；② <see cref="ExpressionSyntaxIssueCodes.GENERIC"/> 那一档的实参是
/// **BCL / 未知原文**（可能是英文）—— 那一半是**有意留的**（原文本身即排障信息），
/// 本判据只钉「它没被顺手删掉」；③ 本判据**不断**「用户是否真的看见了」——
/// 那由 <see cref="ConsumersKeepTheCodedShape"/> 的源码面证据兜着，不是端到端 UI 断言。</para>
/// </summary>
[Collection("Language-global")]
public class ExpressionSyntaxIssueLocalizationGuardTests
{
    private readonly ITestOutputHelper _out;

    public ExpressionSyntaxIssueLocalizationGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>毒化记号：拿它当"中文回落原文"，凡在渲染里冒头就说明字典键没被查到。</summary>
    private const string Poison = "<<中文回落原文未被使用>>";

    /// <summary>CJK 汉字 —— 用来抓「en-US 的值其实没翻」与「中文回落自身可用」。</summary>
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>产出方 —— 码必须由它真实发出，不许退回 <c>FormatException</c> + 裸中文串。</summary>
    private const string Producer = "src/HmiEditor.Core/Services/ExpressionEngine.cs";

    /// <summary>码表与记录体 —— 码的声明面、回落原文的所在地。</summary>
    private const string CodesFile = "src/HmiEditor.Core/Services/ExpressionSyntaxIssueCodes.cs";
    private const string RecordFile = "src/HmiEditor.Core/Services/ExpressionSyntaxError.cs";

    /// <summary>键推导与渲染实现的**单一来源**。</summary>
    private const string Outlet = "src/HmiEditor.App/Services/ExternalText.cs";

    /// <summary>第二个露出点：生成代码验证对话框那条 finding 的产出方。</summary>
    private const string GenProducer = "src/HmiEditor.Core/Services/GeneratedCodeValidator.cs";

    /// <summary>属性面板（第三个露出点，也是**唯一直接上屏**的那条：不走顶层模板）。</summary>
    private const string PanelConsumer = "src/HmiEditor.App/Views/PropertyPanelView.Binding.cs";

    /// <summary>**刻意不本地化**的那条：产物里的 <c>// WARNING:</c> 注释。</summary>
    private const string CommentProducer = "src/HmiEditor.CodeGen/CodeGenerator.Screen.cs";

    /// <summary>干净工程的最小合法代码：含 <c>create_screen_</c>、无未知 <c>lv_*</c>、无 WARNING 残留。</summary>
    private const string CleanCode = "void create_screen_main(void) { }";

    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    // ══════════════════════════ 取数（同源出口 —— 负控与 M14 都复用它） ══════════════════════════

    /// <summary>
    /// 反射枚举 <see cref="ExpressionSyntaxIssueCodes"/> 里声明的全部码（当前 7 个）。
    ///
    /// <para><b>是同源出口</b>：本文件的多条判据与 <c>LocalizationKeyReachabilityGuardTests</c>
    /// 的 <b>M14</b> 都调它，不各写一份反射 —— 抄两份必然漂移，而漂移的表现是
    /// "一个守卫绿、另一个红"，看不出哪边对。</para>
    ///
    /// <para>⚠ 反射到 0 个码会让双向闭包**恒真**，所以每条判据都有下界断言兜底。</para>
    /// </summary>
    internal static List<string> DeclaredCodes()
        => typeof(ExpressionSyntaxIssueCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    /// <summary>一次真实失败：人读的标签 + **触发它的输入** + 走**真实产出路径**拿到的记录体。</summary>
    internal sealed record Hit(string Label, string Input, ExpressionSyntaxError Error);

    /// <summary>
    /// **6 个码 / 7 个输入**各自走**真实产出路径**（<see cref="ExpressionEngine.TryCompile"/>，
    /// 不是手搓 <see cref="ExpressionSyntaxError"/>）—— 手搓的话，"码表与解析器脱节"
    /// 这件事就永远测不到（空表达式给两种写法：空串与纯空白，它们走同一条判空）。
    ///
    /// <para><b>输入为什么是这些</b>（逐条与解析器的分支对应，阶段 91 逐行读过）：</para>
    /// <list type="bullet">
    ///   <item><c>(1+2</c> —— <c>ParsePrimary</c> 消费完 <c>(</c> 之后要求 <c>)</c>，撞上 EOF 哨兵；</item>
    ///   <item><c>1 +</c> —— <c>ParseAdditive</c> 吃掉 <c>+</c> 之后要一个 primary，而输入没了
    ///     ⇒ 走**空 Value 的 EOF 哨兵**那一支（这是与上一档分家的判据，见记录体的 <c>UnexpectedEnd</c>）；</item>
    ///   <item><c>@</c> —— <c>ReadOperator</c> 的 <c>_ =&gt; (TokenType.EOF, 1)</c> 把**未知字符**
    ///     也标成 EOF，但它的 Value 是 <c>"@"</c>（非空）⇒ 必须仍归"意外的符号"，
    ///     否则会报成"表达式意外结束"（用户看到的是一句**错的**诊断）；</item>
    ///   <item><c>1 &gt; 0 ? "a"</c> —— 三元里 <c>:</c> 缺失；</item>
    ///   <item><c>1.2.3</c> —— tokenizer 按 <c>[0-9.]+</c> 收集，解析时以前走 <c>double.Parse</c>
    ///     ⇒ 抛的是 .NET 的**英文** <c>FormatException</c>（中英界面下都是英文）；</item>
    ///   <item><c>""</c> / 纯空白 —— 以前抛 <c>ArgumentNullException</c>（消息是 .NET 英文原文），
    ///     而 <c>GeneratedCodeValidator</c> 写的是 <c>binding.Expression ?? ""</c>
    ///     ⇒ "Kind = Expression 但表达式为空"的工程**真的**会走到这里。</item>
    /// </list>
    /// </summary>
    internal static List<Hit> RealFailures()
    {
        var hits = new List<Hit>();

        void Fail(string label, string input, string expectedCode)
        {
            var ok = ExpressionEngine.TryCompile(input, out var error);
            Assert.False(ok, $"输入「{input}」本该编译失败，实际过了 —— 解析器被放宽了？");
            Assert.True(error is not null,
                $"输入「{input}」：TryCompile 返回 false 却没有给出错误记录（NotNullWhen(false) 被拆了？）");
            Assert.Equal(expectedCode, error!.Code);
            hits.Add(new Hit(label, input, error));
        }

        Fail("空表达式", "", ExpressionSyntaxIssueCodes.EMPTY_EXPRESSION);
        Fail("纯空白表达式", "   ", ExpressionSyntaxIssueCodes.EMPTY_EXPRESSION);
        Fail("缺右括号", "(1+2", ExpressionSyntaxIssueCodes.MISSING_RPAREN);
        Fail("输入中途结束", "1 +", ExpressionSyntaxIssueCodes.UNEXPECTED_END);
        Fail("未知字符", "@", ExpressionSyntaxIssueCodes.UNEXPECTED_SYMBOL);
        Fail("三元缺冒号", "1 > 0 ? \"a\"", ExpressionSyntaxIssueCodes.TERNARY_MISSING_COLON);
        Fail("数字形状不对", "1.2.3", ExpressionSyntaxIssueCodes.MALFORMED_NUMBER);

        return hits;
    }

    /// <summary>
    /// 声明了、但**当前输入空间里构造不出真实触发**的兜底分支 —— 逐条带理由 + **实参个数**。
    ///
    /// <para><b>为什么允许存在</b>：把「产不出」的码塞进 <c>produced</c> 才是造假；
    /// 允许它进这张表 = 把**判据的不完备**明写出来（与 <c>KeptUnreachable</c> 同一个做法），
    /// 而不是把缺陷合法化。⚠ 它是**双向**的：一旦哪天真能产出，
    /// <c>ClosureViolations</c> 就红，逼着把这一行摘掉。</para>
    ///
    /// <para><b>实参个数为什么写在这</b>：它产不出来 ⇒ 量不到，只能从产出方的源码读 ——
    /// 由 <see cref="GenericArm_IsStillLiveAndStillCarriesTheRawMessage"/> 钉住
    /// （在产出方源码里找到 <c>catch (Exception ex)</c> 那一支，要求仍把 <c>ex.Message</c>
    /// 当**实参**传进 <c>Generic(...)</c>）。</para>
    /// </summary>
    internal static readonly (string Code, int Args, string Why)[] NotProducible =
    {
        (ExpressionSyntaxIssueCodes.GENERIC, 1,
            "结构上到不了：解析器现在**全部**失败都抛 ExpressionSyntaxException（4 处），"
            + "tokenizer 的四个读取方法一个 throw 都没有、Current 有 EOF 哨兵兜底、"
            + "数字改走 double.TryParse 后也不再抛 ⇒ TryCompile 的 catch (Exception) 只是防线。"
            + "码留着（防线要留着），只是它进不了「真实产出」那一档"),
    };

    // ══════════════════════════ 判据逻辑（纯函数 —— 负控直接调它们） ══════════════════════════

    /// <summary>
    /// **源码面**：解析器必须还在发码、出口必须仍是唯一键推导点、三条露出点各就各位。
    ///
    /// <para><b>为什么这条非有不可</b>：本缺陷的本质不是"少翻译一个词"，而是
    /// **原因没有码** —— 只加码表、站点不接线，用户所见一字不变
    /// （英文界面下括号里照旧是中文）。四个文件的检查缺一不可。</para>
    ///
    /// <para>剥注释是必须的：讲规则的注释里会写出反例（登记 #48 踩过三次）——
    /// 本阶段自己在 <c>ExpressionSyntaxIssueCodes</c> 的类注释里就写着
    /// <c>throw new FormatException("缺少右括号 ')'")</c> 那句旧写法，
    /// 不剥注释的话下面第一条"旧写法必须消失"会被**自己的文档**满足。</para>
    /// </summary>
    internal static List<string> WiringViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();
        var map = sources.ToDictionary(s => s.Relative, s => SourceScanner.StripComments(s.Text),
            StringComparer.Ordinal);

        // ① 解析器：旧写法必须**整个消失**，新写法必须**真的在用**
        if (!map.TryGetValue(Producer, out var producer))
            bad.Add($"{Producer}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            if (producer.Contains("throw new FormatException", StringComparison.Ordinal))
                bad.Add($"{Producer}：又出现 `throw new FormatException` ⇒ 语法错误退回裸中文串了"
                        + "（它没有码，消费点只能拿 Message 当中文文案用）");

            if (producer.Contains("double.Parse(tok.Value", StringComparison.Ordinal))
                bad.Add($"{Producer}：数字又走 double.Parse 了 ⇒ `1.2.3` 这类输入会抛 .NET 的**英文**原文");

            var thrown = Regex.Matches(producer, @"throw new ExpressionSyntaxException\(").Count;
            if (thrown < 4)
                bad.Add($"{Producer}：只有 {thrown} 处抛码化异常（要求 ≥ 4：空表达式 / 三元缺冒号 / "
                        + "缺右括号 / 意外的符号或输入结束两档）");

            // ⚠ 同上：**不带** `\(` —— 三档 0 实参的码是静态字段，没有工厂可数。
            var coded = Regex.Matches(producer, @"ExpressionSyntaxError\.\w+").Count;
            if (coded < 7)
                bad.Add($"{Producer}：只有 {coded} 处用到记录体（要求 ≥ 7）"
                        + " ⇒ 有分支又退回自己拼字符串/用别的类型了");
        }

        // ② 码表：声明面必须是 `public const string`
        //    （M14 与本地 DeclaredCodes() 都靠反射，要求 IsLiteral && FieldType == string
        //     ⇒ 改成 static readonly 会让整条机制**静默退化成空集**）
        if (!map.TryGetValue(CodesFile, out var codesFile))
            bad.Add($"{CodesFile}：语料里找不到这个文件（扫描面失效？）");
        else if (Regex.Matches(codesFile, @"public const string \w+").Count < 7)
        {
            bad.Add($"{CodesFile}：`public const string` 声明少于 7 条 ⇒ 反射取数会退化成空集"
                    + "（M14 与本地 DeclaredCodes() 都要求 IsLiteral）");
        }

        // ③ 记录体：码常量必须真的被用（否则码表与记录体分家）
        //    ⚠ 计数**不许要求** `\(`：本族的码一半以**静态字段**形式出现
        //    （`ExpressionSyntaxError.MissingRParen` —— 0 实参的那三档没有工厂），
        //    带括号的正则实测只数到 4、会把正确答案判红（本轮首写就踩了）。
        if (!map.TryGetValue(RecordFile, out var record))
            bad.Add($"{RecordFile}：语料里找不到这个文件（扫描面失效？）");
        else if (Regex.Matches(record, @"ExpressionSyntaxIssueCodes\.\w+").Count < 7)
        {
            bad.Add($"{RecordFile}：码常量引用少于 7 处 ⇒ 有档位没接码表（码表成了摆设）");
        }

        // ③ 出口：键推导 + 渲染 + **嵌套实参渲染**三件套
        if (!map.TryGetValue(Outlet, out var outlet))
            bad.Add($"{Outlet}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            if (!outlet.Contains("ExprSyntaxKey(string code)", StringComparison.Ordinal))
                bad.Add($"{Outlet}：expr.syntax. 的键推导出口没了");
            if (!outlet.Contains("Describe(ExpressionSyntaxError error)", StringComparison.Ordinal))
                bad.Add($"{Outlet}：Describe(ExpressionSyntaxError) 渲染出口没了 ⇒ 原因会以中文回落原文直接上屏");
            if (!outlet.Contains("RenderNestedArgs(finding.Args)", StringComparison.Ordinal))
                bad.Add($"{Outlet}：gen 族的填参没走 RenderNestedArgs ⇒ EXPRESSION_SYNTAX 会把记录体的"
                        + " ToString() 转储打到界面上");
        }

        // ④ 三条露出点：两条接出口、一条**必须不接**
        if (!map.TryGetValue(GenProducer, out var gen))
            bad.Add($"{GenProducer}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            if (!gen.Contains("Args: new object[] { Truncate(expr), error }", StringComparison.Ordinal))
                bad.Add($"{GenProducer}：EXPRESSION_SYNTAX 的实参不再是记录体 ⇒ 原因回不去字典");
            if (gen.Contains("error ?? \"\"", StringComparison.Ordinal))
                bad.Add($"{GenProducer}：又把错误记录转成字符串塞进实参了（那是英文界面下中英混排的老路）");
        }

        if (!map.TryGetValue(PanelConsumer, out var panel))
            bad.Add($"{PanelConsumer}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            if (!panel.Contains("ExternalText.Describe(error)", StringComparison.Ordinal))
                bad.Add($"{PanelConsumer}：表达式输入框的实时校验没走 ExternalText.Describe"
                        + " ⇒ 英文界面下那行红字仍是中文");
            if (panel.Contains("msg.expr.error", StringComparison.Ordinal))
                bad.Add($"{PanelConsumer}：又出现 msg.expr.error —— 那条键与它的 Lang 常量已随本项退场");
        }

        // ⑤ 产物注释那条**必须保持不本地化**（判过的不做，登记 #61k）：
        //    它一调 ExternalText，产物就会随编辑器 UI 语言变 ⇒ 可复现性直接坏掉。
        if (!map.TryGetValue(CommentProducer, out var comment))
            bad.Add($"{CommentProducer}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            if (!comment.Contains("{exprError.Message} (skipped)", StringComparison.Ordinal))
                bad.Add($"{CommentProducer}：产物注释不再用回落原文了（要求仍写着 exprError.Message）");
            if (comment.Contains("ExternalText", StringComparison.Ordinal))
                bad.Add($"{CommentProducer}：产物注释接上了本地化出口 ⇒ 生成的 C 会随界面语言变，"
                        + "产物可复现性（A/B 逐字节一致）直接坏掉");
        }

        return bad;
    }

    /// <summary>两本字典各算一份"期望串"：带实参的先填实参。</summary>
    private static string Expected(Dictionary<string, string> dict, string code, IReadOnlyList<object>? args)
    {
        var key = ExternalText.ExprSyntaxKey(code);
        if (!dict.TryGetValue(key, out var body)) return "";
        return args is { Count: > 0 } a
            ? string.Format(CultureInfo.InvariantCulture, body, a.ToArray())
            : body;
    }

    // ══════════════════════════ 判据 ══════════════════════════

    /// <summary>
    /// 主判据：**每一条真实失败**经 <c>ExternalText.Describe(ExpressionSyntaxError)</c> 渲染后，
    /// 结果都必须来自字典（含实参填充），**不许出现回落原文**。
    ///
    /// <para><b>为什么用毒化而不是拿真原文比对</b>：拿真原文只能测"今天恰好翻了没"，
    /// 毒化测的是**"渲染这一半确实由字典决定"** —— 后者才是这条链路的性质。</para>
    /// </summary>
    [Fact]
    public void EveryFailure_RendersTheDictionaryTemplate_NeverTheChineseFallback()
    {
        var zh = Zh;
        var en = En;
        var rows = new List<(string Label, string Rendered, string Key, string[] Candidates)>();

        foreach (var hit in RealFailures())
        {
            var key = ExternalText.ExprSyntaxKey(hit.Error.Code);
            var candidates = new[] { Expected(zh, hit.Error.Code, hit.Error.Args),
                                     Expected(en, hit.Error.Code, hit.Error.Args) }
                .Where(v => v.Length > 0)
                .ToArray();

            var poisoned = hit.Error with { Message = Poison };
            rows.Add(($"{hit.Label}（{hit.Input}）", ExternalText.Describe(poisoned), key, candidates));
        }

        JudgeReadings.Write(_out, "表达式语法失败的原因（#61i · expr.syntax.*）",
            rows.Select(r => $"    {r.Label,-26} {r.Key,-42} ⇒ {r.Rendered}").ToList());

        var problems = CsvImportIssueLocalizationGuardTests.LeakViolations(rows, Poison);
        Assert.True(problems.Count == 0,
            "有失败原因没走字典（登记 #61i / 阶段 91）。正确动作是补 `expr.syntax.<码小写>` 的字典键，"
            + "**不是**把中文串再拼回解析器（那会让英文界面下括号里仍是中文）：\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 切到两种语言各渲染一遍：**中文那一遍必须有汉字、英文那一遍必须一个汉字都没有**，
    /// 且两遍不同。
    ///
    /// <para><b>⚠ 本族的英文那一遍可以直接断言"无汉字"</b>（不像 <c>gen.issue.*</c> 那一族
    /// 得先把实参剥掉再断言）：本族三种实参分别是符号原文、数字记号、位置下标，
    /// **都不可能是中文** —— 位置是 <c>int</c>、数字记号取自 <c>[0-9.]</c>、
    /// 符号原文取自 <c>ReadOperator</c> 的单字符分支（而汉字走的是 <c>ReadIdentifier</c>）。
    /// 所以这条比同族其他守卫更强：它连"实参里混进中文"都一并挡掉。</para>
    /// </summary>
    [Fact]
    public void BothLanguages_RenderDistinctly_AndInTheirOwnScript()
    {
        var hits = RealFailures();
        var original = AppLocalization.Service.CurrentLanguage;

        try
        {
            AppLocalization.Service.SwitchLanguage("zh-CN");
            var zhRenders = hits.Select(h => ExternalText.Describe(h.Error with { Message = Poison })).ToList();
            Assert.All(zhRenders, r => Assert.Matches(Cjk, r));

            AppLocalization.Service.SwitchLanguage("en-US");
            var enRenders = hits.Select(h => ExternalText.Describe(h.Error with { Message = Poison })).ToList();

            JudgeReadings.Write(_out, "两种语言下的渲染（#61i）",
                hits.Select((h, i) => $"    {h.Error.Code,-24} zh={zhRenders[i]} | en={enRenders[i]}").ToList());

            for (var i = 0; i < enRenders.Count; i++)
            {
                Assert.False(Cjk.IsMatch(enRenders[i]),
                    $"{hits[i].Error.Code}：en-US 下渲染出汉字「{enRenders[i]}」"
                    + " ⇒ 该码的 en 值没翻、或是抄的 zh、或实参里混进了中文");
                Assert.NotEqual(zhRenders[i], enRenders[i]);
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }
    }

    /// <summary>
    /// **本仓库第一处「嵌套实参」**（阶段 91）：<c>gen.issue.expression_syntax</c> 的
    /// <c>{1}</c> 装的是记录体本身，渲染时必须**先把它渲染成本地化短句**再填参。
    ///
    /// <para><b>为什么必须有一条判据</b>：这是新引入的搬运形状 —— Core 拿不到字典服务，
    /// 只能给「码 + 中文回落 + 实参」，于是那一格在 <c>Args</c> 里是个**对象**。
    /// 少了 <c>RenderNestedArgs</c>，用户看到的就是
    /// <c>ExpressionSyntaxError { Code = …, Message = … }</c> 这种转储；</para>
    ///
    /// <para><b>三条断言各自挡什么</b>：① 实参那一格**确实是记录体**（有人把它改回字符串 ⇒ 红）；
    /// ② 渲染里**没有记录体转储、也没有码名**（码名是给程序用的，不是给用户看的）；
    /// ③ <c>en-US</c> 下**一个汉字都没有** —— 这才是本项缺陷的症状本身。</para>
    /// </summary>
    [Fact]
    public void ExpressionSyntaxFinding_RendersTheNestedReason_NotTheRecordDump()
    {
        var finding = ExpressionSyntaxFinding();
        var nested = Assert.IsType<ExpressionSyntaxError>(finding.Args![1]);
        Assert.Equal(ExpressionSyntaxIssueCodes.MISSING_RPAREN, nested.Code);

        var original = AppLocalization.Service.CurrentLanguage;
        try
        {
            AppLocalization.Service.SwitchLanguage("en-US");
            var rendered = ExternalText.Describe(finding with { Message = Poison });

            Assert.DoesNotContain("ExpressionSyntaxError {", rendered, StringComparison.Ordinal);
            Assert.DoesNotContain(nested.Code, rendered, StringComparison.Ordinal);
            Assert.DoesNotContain(Poison, rendered, StringComparison.Ordinal);
            Assert.False(Cjk.IsMatch(rendered),
                $"英文界面下这条 finding 里仍有汉字：「{rendered}」 —— 嵌套实参没被渲染，"
                + "或 RenderNestedArgs 的类型分派漏了这一族");

            AppLocalization.Service.SwitchLanguage("zh-CN");
            var zhRendered = ExternalText.Describe(finding with { Message = Poison });
            Assert.Contains(Expected(Zh, nested.Code, nested.Args), zhRendered, StringComparison.Ordinal);
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }
    }

    /// <summary>走**真实产出路径**拿一条 <c>EXPRESSION_SYNTAX</c> finding（不手搓 finding）。</summary>
    private static GeneratedCodeFinding ExpressionSyntaxFinding()
    {
        var project = new HmiProject();
        var screen = ScreenFactory.Create(project, name: "main");
        project.Screens.Add(screen);

        var control = new HmiControl { Id = "c1", Name = "value" };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            Kind = BindingSourceKind.Expression,
            Expression = "(1+2",
        });
        screen.Controls.Add(control);

        return GeneratedCodeValidator.Validate(project, CleanCode)
            .Single(f => f.Code == GeneratedCodeIssueCodes.EXPRESSION_SYNTAX);
    }

    /// <summary>两本字典对每个码都齐 + 成对 + **占位符个数等于产出方实测的实参个数**。</summary>
    [Fact]
    public void BothDictionaries_CarryEveryCode_AndAreActuallyPaired()
    {
        var codes = DeclaredCodes();
        Assert.True(codes.Count >= 7,
            $"只反射到 {codes.Count} 个码 —— 取数失效（反射面空了，下面的判定会恒真）");

        // 实参个数**实测**：产得出的码从真实失败里读；兜底那一档读不到 ⇒ 由表供给，
        // 并由 GenericArm_IsStillLiveAndStillCarriesTheRawMessage 在源码面钉住。
        var argCounts = RealFailures()
            .GroupBy(h => h.Error.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Error.Args?.Count ?? 0, StringComparer.Ordinal);

        foreach (var (code, args, _) in NotProducible)
            argCounts.TryAdd(code, args);

        var problems = CsvImportIssueLocalizationGuardTests.PairingViolations(
            codes, argCounts, Zh, En, ExternalText.ExprSyntaxKey);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>码表**三态闭包**：声明的码都真的产得出（或如实进兜底表），产出的码都声明了。</summary>
    [Fact]
    public void DeclaredCodes_AndProducedCodes_AreTheSameSet()
    {
        var hits = RealFailures();
        var produced = hits.Select(h => h.Error.Code).Distinct(StringComparer.Ordinal).ToList();
        var inputs = hits.GroupBy(h => h.Error.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Input, StringComparer.Ordinal);

        var problems = CsvImportIssueLocalizationGuardTests.ClosureViolations(
            DeclaredCodes(), produced, NotProducible.Select(n => n.Code));

        JudgeReadings.Write(_out, "表达式语法码表（#61i）",
            new List<string>
            {
                $"    声明 {DeclaredCodes().Count} 个 / 真实产出 {produced.Count} 个"
                + $" / 兜底 {NotProducible.Length} 个",
            }.Concat(produced.OrderBy(x => x, StringComparer.Ordinal)
                .Select(c => $"      {c,-24} ← 输入「{inputs[c]}」")).ToList());

        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **兜底那一档不许变成免费通行证**：它必须①仍在产出方源码里（那道 <c>catch</c> 还在）；
    /// ②那处仍把 <c>ex.Message</c> 当**实参**传进 <c>Generic(...)</c>（排障信息没被顺手删掉）。
    ///
    /// <para>这是把 <see cref="NotProducible"/> 里手写的"实参个数 = 1"钉回源码的唯一出口 ——
    /// 没有它，那张表就是一个没人核的数字（豁免表里的"理由"会变成能把缺陷合法化的字段，
    /// 登记 #54 的老病）。</para>
    /// </summary>
    [Fact]
    public void GenericArm_IsStillLiveAndStillCarriesTheRawMessage()
    {
        var sources = AppSourceScanner.ScanAllProductSources();
        var producer = sources.FirstOrDefault(s => s.Relative == Producer);
        Assert.False(producer.Text is null, $"语料里找不到 {Producer}（扫描面失效？）");

        var code = SourceScanner.StripComments(producer.Text);
        Assert.Contains("catch (Exception ex)", code, StringComparison.Ordinal);
        Assert.Contains("ExpressionSyntaxError.Generic(ex.Message)", code, StringComparison.Ordinal);
    }

    /// <summary>回落文案必须**自身可用**：非空、含汉字、不是码名（字典缺键时用户还能读到东西）。</summary>
    [Fact]
    public void FallbackMessages_StayUsableChinese()
    {
        foreach (var hit in RealFailures())
        {
            Assert.False(string.IsNullOrWhiteSpace(hit.Error.Message), $"{hit.Error.Code}：回落原文是空的");
            Assert.Matches(Cjk, hit.Error.Message);
            Assert.DoesNotContain(hit.Error.Code, hit.Error.Message, StringComparison.Ordinal);
        }

        // 兜底那一档的回落是**原始异常消息**（可能是英文）⇒ 只断非空，不断汉字。
        var generic = ExpressionSyntaxError.Generic("Value cannot be null. (Parameter 'x')");
        Assert.False(string.IsNullOrWhiteSpace(generic.Message));
        Assert.Equal(generic.Message, generic.Args!.Single());
    }

    /// <summary>解析器还在发码；出口三件套齐全；三条露出点各就各位（反回归钉）。</summary>
    [Fact]
    public void ConsumersKeepTheCodedShape()
    {
        var problems = WiringViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "表达式语法失败的接线面（#61i）",
            new[] { Producer, CodesFile, RecordFile, Outlet, GenProducer, PanelConsumer, CommentProducer }
                .Select(f => $"    {f}").ToList());

        Assert.True(problems.Count == 0,
            "原因到界面的这条链断了（登记 #61i）。**只加码表不算收口** —— "
            + "站点不接线时用户所见一字不变：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 负控 5 组，两两成对 —— 每组都要求"该红的红、该绿的绿"，
    /// 否则证明不了判据真的在盯那件事（本项目对负控的一贯要求）。
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        var codes = DeclaredCodes();
        var zh = Zh;
        var en = En;
        var hits = RealFailures();
        var argCounts = hits
            .GroupBy(h => h.Error.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Error.Args?.Count ?? 0, StringComparer.Ordinal);
        foreach (var (code, args, _) in NotProducible)
            argCounts.TryAdd(code, args);

        var key0 = ExternalText.ExprSyntaxKey(codes[0]);
        var produced = hits.Select(h => h.Error.Code).ToList();
        var notProducible = NotProducible.Select(n => n.Code).ToList();

        // ── NC1 · 成对性：正控绿，三种坏法各点名 ──
        Assert.Empty(CsvImportIssueLocalizationGuardTests.PairingViolations(
            codes, argCounts, zh, en, ExternalText.ExprSyntaxKey));
        Assert.Empty(CsvImportIssueLocalizationGuardTests.ClosureViolations(codes, produced, notProducible));

        var enCopy1 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy1[key0] = zh[key0];
        Assert.Contains(CsvImportIssueLocalizationGuardTests.PairingViolations(
                codes, argCounts, zh, enCopy1, ExternalText.ExprSyntaxKey),
            v => v.Contains("中文字符", StringComparison.Ordinal));

        var enCopy2 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy2.Remove(key0);
        Assert.Contains(CsvImportIssueLocalizationGuardTests.PairingViolations(
                codes, argCounts, zh, enCopy2, ExternalText.ExprSyntaxKey),
            v => v.Contains("缺键", StringComparison.Ordinal));

        // 带实参的那个码当成 0 实参 ⇒ 占位符那一支点名。⚠ 反例**必须**挑"实测确实带实参"的码。
        var withArgs = hits.First(h => (h.Error.Args?.Count ?? 0) > 0).Error.Code;
        var argCopy = new Dictionary<string, int>(argCounts, StringComparer.Ordinal) { [withArgs] = 0 };
        Assert.Contains(CsvImportIssueLocalizationGuardTests.PairingViolations(
                codes, argCopy, zh, en, ExternalText.ExprSyntaxKey),
            v => v.Contains("占位符", StringComparison.Ordinal));

        // ── NC2 · 三态闭包：四支都要报 ──
        Assert.Contains(CsvImportIssueLocalizationGuardTests.ClosureViolations(
                codes.Concat(new[] { "NOT_A_REAL_CODE" }), produced, notProducible),
            v => v.Contains("死码", StringComparison.Ordinal));
        Assert.Contains(CsvImportIssueLocalizationGuardTests.ClosureViolations(
                codes, produced.Concat(new[] { "NOT_A_REAL_CODE" }), notProducible),
            v => v.Contains("没声明", StringComparison.Ordinal));
        Assert.Contains(CsvImportIssueLocalizationGuardTests.ClosureViolations(
                codes, produced, new[] { produced[0] }),
            v => v.Contains("已经能真实产出", StringComparison.Ordinal));
        Assert.Contains(CsvImportIssueLocalizationGuardTests.ClosureViolations(
                codes, produced, new[] { "NOT_A_REAL_CODE" }),
            v => v.Contains("根本没声明", StringComparison.Ordinal));

        // ── NC3 · 渲染泄漏：回落原文冒头 ⇒ 点名；字典期望串 ⇒ 绿 ──
        var hit0 = hits[0];
        var expected0 = Expected(zh, hit0.Error.Code, hit0.Error.Args);
        var leak = new[] { ("演示", $"语法: {Poison}", key0, new[] { expected0 }) };
        Assert.Contains(CsvImportIssueLocalizationGuardTests.LeakViolations(leak, Poison),
            v => v.Contains("中文回落原文", StringComparison.Ordinal));

        var clean = new[] { ("演示", expected0, key0, new[] { expected0 }) };
        Assert.Empty(CsvImportIssueLocalizationGuardTests.LeakViolations(clean, Poison));

        // 实参没填 ⇒ 期望串与渲染串不等 ⇒ 一样点名（否则"实参"这一维没人管）。
        // ⚠ 必须挑一个**带实参**的样本：codes[0] = EMPTY_EXPRESSION 是 0 实参的，
        //    拿它测这条会静默跳过（同族守卫踩过这个坑）。
        var argsHit = hits.First(h => (h.Error.Args?.Count ?? 0) == 2);
        var argsKey = ExternalText.ExprSyntaxKey(argsHit.Error.Code);
        Assert.NotEqual(zh[argsKey], Expected(zh, argsHit.Error.Code, argsHit.Error.Args)); // 前置
        var wrongArgs = new[] { ("演示", zh[argsKey], argsKey, new[] { Expected(zh, argsHit.Error.Code, argsHit.Error.Args) }) };
        Assert.Contains(CsvImportIssueLocalizationGuardTests.LeakViolations(wrongArgs, Poison),
            v => v.Contains("有别的来源", StringComparison.Ordinal));

        // ── NC4 · 接线面：六个文件各有坏法，合规写法必须无话可说 ──
        var all = new[] { Producer, CodesFile, RecordFile, Outlet, GenProducer, PanelConsumer, CommentProducer };
        var goodProducer = string.Concat(Enumerable.Repeat(
            "throw new ExpressionSyntaxException(ExpressionSyntaxError.MissingRParen);\n", 4))
            + string.Concat(Enumerable.Repeat("if (x) return ExpressionSyntaxError.Empty;\n", 3));
        var goodCodesFile = string.Concat(Enumerable.Repeat("public const string X = \"X\";\n", 7));
        var goodRecord = string.Concat(Enumerable.Repeat("= ExpressionSyntaxIssueCodes.X;\n", 7));
        var goodOutlet = "static string ExprSyntaxKey(string code) => p + code;"
            + "static string Describe(ExpressionSyntaxError error) => d(error.Code);"
            + "static string D(GeneratedCodeFinding f) => Resolve(k, m, RenderNestedArgs(finding.Args));";
        var goodGen = "Args: new object[] { Truncate(expr), error }";
        var goodPanel = "ShowExpressionError(ExternalText.Describe(error));";
        var goodComment = @"Cmt(sb, $""... {exprError.Message} (skipped)"");";
        string Body(string f) => f == Producer ? goodProducer
            : f == CodesFile ? goodCodesFile
            : f == RecordFile ? goodRecord
            : f == Outlet ? goodOutlet
            : f == GenProducer ? goodGen
            : f == PanelConsumer ? goodPanel
            : goodComment;
        (string Relative, string Text)[] Corpus(string target, string body)
            => all.Select(f => (f, f == target ? body : Body(f))).ToArray();

        // ② 合规写法 ⇒ 无话可说（证明它不是"永远返回违规"的判据）
        Assert.Empty(WiringViolations(all.Select(f => (f, Body(f))).ToArray()));

        // ① 解析器退回裸 FormatException（本缺陷本来的样子）⇒ 点名
        var raw = WiringViolations(Corpus(Producer,
            "throw new FormatException(\"缺少右括号 ')'\");\n"
            + "var v = double.Parse(tok.Value, CultureInfo.InvariantCulture);"));
        Assert.Contains(raw, v => v.Contains("throw new FormatException", StringComparison.Ordinal));
        Assert.Contains(raw, v => v.Contains("double.Parse", StringComparison.Ordinal));
        Assert.Contains(raw, v => v.Contains("抛码化异常", StringComparison.Ordinal));

        // ③ 出口丢了嵌套渲染 ⇒ 点名（那正是记录体转储会上屏的地方）
        Assert.Contains(WiringViolations(Corpus(Outlet, goodOutlet.Replace("RenderNestedArgs(finding.Args)", "finding.Args"))),
            v => v.Contains("RenderNestedArgs", StringComparison.Ordinal));

        // ④ 属性面板退回直显 / 产物注释接上本地化 ⇒ 各点名
        Assert.Contains(WiringViolations(Corpus(PanelConsumer, "ShowExpressionError(error);")),
            v => v.Contains("ExternalText.Describe", StringComparison.Ordinal));
        Assert.Contains(WiringViolations(Corpus(CommentProducer,
                @"Cmt(sb, $""... {ExternalText.Describe(exprError)} ..."");")),
            v => v.Contains("产物注释接上了本地化出口", StringComparison.Ordinal));
        // ⚠ 这一格刻意**先把语料算出来再断言**：`TestAssertionStyleGuardTests` 会拦
        //   「同一行里既有 `Assert.Contains(` 又有含 `(` 与 `,` 的字面量」（那是"精确调用文本"
        //   断言的形状）。这里要的字符串本身就是一句调用文本，所以只能先落成局部变量。
        var commentBodyWithoutFallback =
            WiringViolations(Corpus(CommentProducer, @"Cmt(sb, ""... {exprError} ..."");"));
        Assert.Contains(commentBodyWithoutFallback,
            v => v.Contains("回落原文", StringComparison.Ordinal));

        // ⑤ 语料里**缺**一个目标文件 ⇒ 必须报"找不到" ——
        //    否则扫描面整体失效（返回空语料）时会**静默全绿**：这是本函数的下界，不能省。
        Assert.Contains(WiringViolations(new[] { (Producer, goodProducer) }),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ⑥ 注释里写的旧写法不算（规则不许被自己的文档满足，登记 #48）——
        //    解析器语料**既有合规写法、又在注释里写一句旧写法** ⇒ 剥注释后只该看前者。
        Assert.Empty(WiringViolations(Corpus(Producer,
            goodProducer + "\n// 反例：这里**不**该写 throw new FormatException(\"缺少右括号 ')'\")")));
    }
}
