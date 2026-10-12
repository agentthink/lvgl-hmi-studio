using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「**生成代码验证结果对话框里的文案必须由字典决定**」的守卫 —— 登记表 §4 <b>#61g</b>
/// （阶段 89）。
///
/// <para><b>它守的是哪一类缺陷</b>：<see cref="GeneratedCodeValidator.Validate"/> 是纯函数，
/// 每条 finding 的 <c>Message</c> / <c>Location</c> 此前都在 Core 里**直接拼成中文**，
/// 而唯一的文本出口（<c>App/Views/ValidationResultView</c> 的
/// <c>Text = finding.Message</c> / <c>Text = finding.Location</c>）**原样直显** ⇒
/// 界面切成英文时**整个对话框都是中文**（与 #59 / #61a / #61f 那种"中文串当参数进
/// 已本地化模板"的形状不同，这里是**根本没有模板**）。码化之后 App 层按
/// <c>gen.issue.&lt;码小写&gt;</c> / <c>gen.loc.&lt;码小写&gt;</c> 渲染。</para>
///
/// <para><b>为什么它必须与「直通面守卫」区分开</b>：<c>ExceptionMessagePassthroughFaceTests</c>
/// 数的是 <c>Format(键, ex.Message)</c> 那种站点；本族的产出方**根本不抛异常**
/// （<see cref="GeneratedCodeValidator"/> 是纯函数，findings 是**返回值**）⇒
/// 它在站点面上**看不见**，换成 <c>FailureReason(ex)</c> 也是改名式假闭环。
/// 与 <c>SvgParseIssueLocalizationGuardTests</c>（M11）/ <c>CsvImportIssueLocalizationGuardTests</c>
/// （M12）同属「**返回值带码**」那一族。</para>
///
/// <para><b>码集中在 Core、键集中在 App</b>：<c>HmiEditor.Core</c> 拿不到字典服务
/// （依赖方向 App → Core）⇒ 它只声明**稳定的码** + **中文回落原文**，键由
/// <see cref="ExternalText.GenIssueKey"/> / <see cref="ExternalText.GenLocationKey"/>
/// 统一推导。成对性纯函数**直接复用** <c>CsvImportIssueLocalizationGuardTests</c> 那四条
/// （<c>PairingViolations</c> / <c>ClosureViolations</c> / <c>LeakViolations</c> /
/// <c>CountPlaceholders</c>）—— 两族的规则一字不差，抄第二份必然漂移。</para>
///
/// <para><b>定位为什么要单独一张码表</b>：消息是一句话，定位是"这条 finding 落在哪"，
/// 它自己还会被复制/筛选 ⇒ 生命周期不同。定位分**两档**：
/// ① <b>有码</b>（5 种：屏幕 / 屏幕+控件 / 屏幕+控件+属性 / 屏幕+控件+动作 / 生成代码）
/// ⇒ 走 <c>gen.loc.*</c>；
/// ② <b>中性·直出</b>（<c>-</c> / 屏幕 id 列表 / <c>SecurityConfig.RuntimeUsers[…]</c>
/// 这类**模型路径**）—— 与语言无关，翻了反而对不上代码 ⇒ <c>LocationCode == ""</c>，
/// 判据要求它**不含汉字**。</para>
///
/// <para>⚠ 这里原先还有**第三档「外来定位」** —— 串由 <c>CrossReferenceAnalyzer</c> 拼、
/// 本判据刻意不断它的语言，由一张 <c>ForeignLocation</c> 表双向守着。阶段 93 已闭环
/// （登记 <b>#61l</b>）：产出方改成 <see cref="DanglingRefLocation"/> 记录体
/// （定位码 + 实参 + 中文回落原文）⇒ "外来"这一档**消失**，那张表、它的双向判定
/// 与负控**一起删掉**。留着它就是「把定位没码化合法化」的永久字段（登记 #54 的老病）；
/// 而按本项目的纪律，**做不出负控的判据不发布**。</para>
///
/// <para><b>诚实边界（本判据断不了的事）</b>：① 它只断「渲染等于字典模板」，断不了
/// **翻译质量**；② <b>实参里可以带汉字</b> —— 那是 <c>FACEPLATE_*</c> 的控件文本与绑定 id
/// （用户自己的数据，与界面语言无关），本判据只钉「汉字只许来自实参、不许来自模板」。
/// ⚠ 这一段原先还列着 <c>EXPRESSION_SYNTAX</c>（"第二个实参是 <c>ExpressionEngine</c>
/// 抛的中文 <c>FormatException.Message</c>"）—— 那半<b>阶段 91 已闭环</b>（登记 #61i）：
/// 那一格现在是**记录体**，按用户语言渲染，守卫见
/// <c>ExpressionSyntaxIssueLocalizationGuardTests</c>；
/// ③ <c>GENERATOR_WARNING_COMMENT</c> **刻意没有字典键**（消息体是产物
/// <c>generated_ui.c</c> 里 <c>// WARNING:</c> 注释的原文，翻译它等于改写产物，
/// 登记 <b>#61k</b>）⇒ 它进 <see cref="MessageNoKey"/> 表并在**两侧**受判；
/// ④ 本判据**不断**「用户是否真的看见了」—— 那由 <see cref="ConsumerKeepsTheCodedShape"/>
/// 的源码面证据兜着，不是端到端 UI 断言。</para>
/// </summary>
[Collection("Language-global")]
public class GenFindingLocalizationGuardTests
{
    private readonly ITestOutputHelper _out;

    public GenFindingLocalizationGuardTests(ITestOutputHelper output) => _out = output;

    /// <summary>毒化记号：拿它当"中文回落原文"，凡在渲染里冒头就说明字典键没被查到。</summary>
    private const string Poison = "<<中文回落原文未被使用>>";

    /// <summary>CJK 汉字 —— 用来抓「en-US 的值其实没翻」「中性定位其实是中文」「回落自身可用」。</summary>
    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>产出方 —— 码必须由它真实发出，不许退回裸字符串。</summary>
    private const string Producer = "src/HmiEditor.Core/Services/GeneratedCodeValidator.cs";

    /// <summary>唯一的文本出口（**消费方**）—— 两条通道都必须走码化出口。</summary>
    private const string Consumer = "src/HmiEditor.App/Views/ValidationResultView.axaml.cs";

    /// <summary>键推导与渲染实现的**单一来源**。</summary>
    private const string Outlet = "src/HmiEditor.App/Services/ExternalText.cs";

    /// <summary>
    /// 悬空引用**定位面**的产出方（阶段 93，闭环 <b>#61l</b>）：三个工厂方法各自发一个定位码。
    ///
    /// <para>为什么要单列一个文件：码不再由 <see cref="Producer"/> 字面声明 ⇒
    /// 上面那个 <c>FindingLocationCodes.</c> 计数**对它看不见**（把 <c>loc.Code</c> 删掉
    /// 计数也不会掉）⇒ 必须点名检查这个文件，否则"码被搬走了"这件事在源码面上是隐形的。</para>
    /// </summary>
    private const string LocationProducer = "src/HmiEditor.Core/Services/DanglingRefLocation.cs";

    private static Dictionary<string, string> Zh => PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static Dictionary<string, string> En => PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    /// <summary>干净工程的最小合法代码：含 <c>create_screen_</c>、无未知 <c>lv_*</c>、无 WARNING 残留。</summary>
    private const string CleanCode = "void create_screen_main(void) { }";

    // ══════════════════════════ 取数（同源出口 —— 负控与 M13 都复用它） ══════════════════════════

    /// <summary>
    /// 反射枚举 <see cref="GeneratedCodeIssueCodes"/> 里声明的全部消息码（当前 18 个）。
    ///
    /// <para><b>是同源出口</b>：本文件的多条判据与 <c>LocalizationKeyReachabilityGuardTests</c>
    /// 的 <b>M13</b> 都调它，不各写一份反射 —— 抄两份必然漂移，而漂移的表现是
    /// "一个守卫绿、另一个红"，看不出哪边对。</para>
    ///
    /// <para>⚠ 反射到 0 个码会让双向闭包**恒真**，所以每条判据都有下界断言兜底。</para>
    /// </summary>
    internal static List<string> DeclaredCodes() => ConstStrings(typeof(GeneratedCodeIssueCodes));

    /// <summary>反射枚举 <see cref="FindingLocationCodes"/> 里声明的全部定位码（当前 5 个）。</summary>
    internal static List<string> DeclaredLocationCodes() => ConstStrings(typeof(FindingLocationCodes));

    private static List<string> ConstStrings(Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// 声明了、**刻意不给字典键**的码 —— 逐条带理由。**双向**判定（见
    /// <see cref="KeylessViolations"/>）：给了键 ⇒ 红（该摘掉这一行）；
    /// 别的码缺键 ⇒ 也红。
    /// </summary>
    internal static readonly (string Code, string Why)[] MessageNoKey =
    {
        (GeneratedCodeIssueCodes.GENERATOR_WARNING_COMMENT,
            "消息体就是产物 generated_ui.c 里 `// WARNING:` 注释的原文 ⇒ 翻译它等于改写产物（登记 #61k）"),
    };

    /// <summary>
    /// 声明了、但**当前输入空间里构造不出真实触发**的兜底分支 —— 逐条带理由 + **实参个数**。
    ///
    /// <para><b>为什么允许存在</b>：把「产不出」的码塞进 <c>produced</c> 才是造假；
    /// 允许它进这张表 = 把**判据的不完备**明写出来，而不是把缺陷合法化。
    /// ⚠ 它是**双向**的：一旦哪天真能产出，<c>ClosureViolations</c> 就红。</para>
    ///
    /// <para><b>实参个数为什么写在这</b>：它产不出来 ⇒ 量不到，只能从产出方的源码读 ——
    /// 由 <see cref="NotProducibleArm_IsStillLiveAndStillTakesTwoArgs"/> 钉住
    /// （在产出方源码里从码的位置往后开一个窗口，要求仍写着
    /// <c>new object[] { issue.Index, issue.Username }</c> 且回落文案仍含汉字）。</para>
    /// </summary>
    internal static readonly (string Code, int Args, string Why)[] NotProducible =
    {
        (GeneratedCodeIssueCodes.RUNTIME_USER_UNKNOWN, 2,
            "RuntimeUserIssueKind 恰好 4 个取值、RuntimeUserValidator 只构造这 4 个 ⇒ switch 的 `_ =>` 兜底结构上到不了（它防的是将来新增取值，不是死代码）"),
    };

    // ⚠ 这里原来是一张 `ForeignLocation` 表（"定位串由别的模块拼"的那些码，唯一一行是
    //   DANGLING_REFERENCE）。阶段 93 闭环 #61l 之后它**空了** —— 悬空引用的定位改成
    //   DanglingRefLocation 记录体（定位码 + 实参 + 中文回落原文），走的就是"有码"那一档。
    //   空表 ＋ 恒真判定 ＝ 没有负控的判据 ⇒ 表、它的双向判定与负控**一起删**。
    //   它守的那件事（"定位到底翻没翻"）现在由 EveryLocation_…
    //   与 DanglingRefs_CarryACodedLocation_AndKeepTheOldFallbackFormat 直接断，
    //   并由 NegativeControls 的 NC4 反向证明"断了会红"。

    /// <summary>有字典键的那一档消息码（= 声明的码 − <see cref="MessageNoKey"/>）。</summary>
    internal static List<string> KeyedCodes()
    {
        var keyless = MessageNoKey.Select(n => n.Code).ToHashSet(StringComparer.Ordinal);
        return DeclaredCodes().Where(c => !keyless.Contains(c)).ToList();
    }

    /// <summary>一次真实产出：人读的标签 + 走**真实产出路径**拿到的对象。</summary>
    internal sealed record Hit(string Label, GeneratedCodeFinding Finding);

    /// <summary>
    /// 全 17 个**可产出**消息码各自走**真实产出路径**（不是手搓 <see cref="GeneratedCodeFinding"/>）
    /// —— 手搓的话，"码表与产出方脱节"这件事就永远测不到。
    ///
    /// <para>夹具按"最小触发"分组：每个 <see cref="GeneratedCodeValidator.Validate"/> 调用
    /// 都会跑全部六项子检查，所以一次调用可能带回多条 finding —— 那没关系，
    /// 判定只关心"这个码有没有真的出现"与"每条 finding 渲染成什么"。</para>
    /// </summary>
    internal static List<Hit> RealFindings()
    {
        var hits = new List<Hit>();

        void Collect(string label, HmiProject project, string? code)
        {
            foreach (var f in GeneratedCodeValidator.Validate(project, code))
                hits.Add(new Hit($"{label} · {f.Code}", f));
        }

        // ① 结构：空项目 / 重名屏幕 / 同屏控件 id 重复
        Collect("空项目", new HmiProject(), CleanCode);

        var dup = new HmiProject();
        var dupScreen = NewScreen(dup, "main");
        NewScreen(dup, "main");
        dupScreen.Controls.Add(NewControl("c1", "lamp"));
        dupScreen.Controls.Add(NewControl("c1", "lamp2"));
        Collect("重名屏幕 + 重复控件 id", dup, CleanCode);

        // ② Faceplate 未解析（三档）
        var faceplate = new HmiProject();
        var fpScreen = NewScreen(faceplate, "main");

        var fpText = NewControl("c1", "label");
        fpText.Text = "温度 {{T1}}";
        fpScreen.Controls.Add(fpText);

        var fpBinding = NewControl("c2", "value");
        fpBinding.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            VariableId = "{{V1}}",
        });
        fpScreen.Controls.Add(fpBinding);

        var fpLegacy = NewControl("c3", "legacy");
        fpLegacy.VariableBindingId = "{{V2}}";
        fpScreen.Controls.Add(fpLegacy);

        Collect("Faceplate 未解析", faceplate, CleanCode);

        // ③ 表达式语法（`(1+2` ⇒ ExpressionEngine 报「缺少右括号 ')'」）
        var expr = new HmiProject();
        var exprScreen = NewScreen(expr, "main");
        var exprControl = NewControl("c1", "value");
        exprControl.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            Kind = BindingSourceKind.Expression,
            Expression = "(1+2",
        });
        exprScreen.Controls.Add(exprControl);
        Collect("表达式语法", expr, CleanCode);

        // ④ 悬空引用 —— **四个站点各来一个**（阶段 93 / #61l 起定位码化）。
        //   为什么必须铺满：DeclaredCodes_AndProducedCodes_AreTheSameSet 的**定位码**闭包
        //   只认"真实产出的码" ⇒ 只产一个的话，SCREEN_CONTROL / SCREEN_CONTROL_ACTION
        //   会被判成**死码**（那是判据在正确报红）。
        var dangling = new HmiProject();
        var danglingScreen = NewScreen(dangling, "main");

        var dBinding = NewControl("c1", "value");
        dBinding.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            VariableId = "v_not_there",
        });
        danglingScreen.Controls.Add(dBinding);

        var dLegacy = NewControl("c2", "legacy");
        dLegacy.VariableBindingId = "v_not_there";
        danglingScreen.Controls.Add(dLegacy);

        var dAction = NewControl("c3", "start");
        dAction.Actions.Add(new HmiAction { ActionType = "SetVariable", Target = "v_not_there" });
        danglingScreen.Controls.Add(dAction);

        var dLegacyAction = NewControl("c4", "start2");
        dLegacyAction.Action = new HmiAction { ActionType = "SetVariable", Target = "v_not_there" };
        danglingScreen.Controls.Add(dLegacyAction);

        Collect("悬空引用", dangling, CleanCode);

        // ⑤ 运行时用户表：4 个单条档 + 整表档
        Collect("用户名空", UserProject(User(""), User("op")), CleanCode);
        Collect("用户名重复", UserProject(User("op"), User("op")), CleanCode);
        Collect("角色表外", UserProject(User("ghost", "role-x"), User("op")), CleanCode);
        Collect("启用但无密码", UserProject(User("nopass", passwordHash: ""), User("op")), CleanCode);
        Collect("整表不可用", UserProject(User("a", "role-x"), User("b", "role-y")), CleanCode);

        // ⑥ 生成代码面
        var gen = new HmiProject();
        NewScreen(gen, "main");
        Collect("生成代码为空", gen, "");
        Collect("缺 create_screen", gen, "void nothing(void) { }");
        Collect("WARNING 注释残留", gen, "// WARNING: 图层引用校验失败\nvoid create_screen_main(void) { }");
        Collect("未知 LVGL API", gen, "void create_screen_main(void) { lv_totally_bogus(1); }");

        return hits;
    }

    private static HmiScreen NewScreen(HmiProject project, string name)
    {
        // ScreenFactory.Create 只**造**屏幕、不加入工程（调用方负责），别漏 Add。
        var screen = ScreenFactory.Create(project, name: name);
        project.Screens.Add(screen);
        return screen;
    }

    private static HmiControl NewControl(string id, string name) => new() { Id = id, Name = name };

    private const string SomeHash = "AAECAwQFBgcICQoLDA0ODw==:wNu1hDVTcb2XOXidGgkb4c9GEr6wxjDGAGaH2VWrOLk=";

    private static RuntimeUser User(
        string username,
        string roleId = "role-operator",
        string passwordHash = SomeHash,
        bool enabled = true) => new()
        {
            Username = username,
            RoleId = roleId,
            PasswordHash = passwordHash,
            IsEnabled = enabled,
        };

    private static HmiProject UserProject(params RuntimeUser[] users)
    {
        var project = new HmiProject();
        // 结构校验要求至少有一个屏幕；不加屏幕会让 ValidateStructure 报 Error，
        // 把读数淹在无关的 finding 里。
        NewScreen(project, "main");
        project.SecurityConfig.RuntimeUsers = users.ToList();
        return project;
    }

    /// <summary>
    /// 两本字典各算一份"期望串"：带实参的先填实参。
    ///
    /// <para>⚠ <b>实参里的记录体要渲染，不能 <c>ToString()</c></b>（阶段 91 / #61i）：
    /// <c>EXPRESSION_SYNTAX</c> 的 <c>Args[1]</c> 现在是 <see cref="ExpressionSyntaxError"/>
    /// —— 用户在界面上看到的是**渲染后**的短句，不是记录体转储。
    /// 这里借 <see cref="ExternalText.Describe(ExpressionSyntaxError)"/> 渲染，
    /// 因此**期望串与当前语言绑定** ⇒ 用它的那条判据必须把语言钉住
    /// （见 <see cref="EveryFinding_RendersTheDictionaryTemplate_NeverTheChineseFallback"/>）。</para>
    /// </summary>
    private static string Expected(Dictionary<string, string> dict, string key, IReadOnlyList<object>? args)
    {
        if (!dict.TryGetValue(key, out var body)) return "";
        return args is { Count: > 0 } a
            ? string.Format(CultureInfo.InvariantCulture, body, a.Select(RenderArg).ToArray())
            : body;
    }

    /// <summary>
    /// 实参在界面上的**可见文本**：记录体走渲染出口（按当前语言），其余照旧 <c>ToString()</c>。
    /// 与 <c>ExternalText.RenderNestedArgs</c> 是**同一套分派** —— 判据这一侧不许另发明一套，
    /// 否则"期望串"与"实际渲染"会用两个口径描述同一件事。
    /// </summary>
    private static string RenderArg(object? arg) => arg switch
    {
        ExpressionSyntaxError expr => ExternalText.Describe(expr),
        null => "",
        _ => arg.ToString() ?? "",
    };

    /// <summary>
    /// 把 finding 的实参逐条取成**用户可见文本**（用来从渲染结果里"剥掉实参"）。
    /// 走 <see cref="RenderArg"/> —— 拿裸 <c>ToString()</c> 比会把记录体转储当期望值，
    /// 一个字符也剥不掉（阶段 91 之前正是如此，只是那时实参还是字符串）。
    /// </summary>
    private static List<string> ArgTexts(GeneratedCodeFinding finding)
        => (finding.Args ?? Array.Empty<object>())
            .Select(RenderArg)
            .Where(s => s.Length > 0)
            .ToList();

    // ══════════════════════════ 判据逻辑（纯函数 —— 负控直接调它们） ══════════════════════════

    /// <summary>
    /// 「刻意无键」那张表的**双向**判定：
    /// ① 表里的码在**两本字典里都不许有键**（有 ⇒ 表过期 → 该摘掉这一行）；
    /// ② 表外的每个声明码在两本字典里**都必须有键**（缺 ⇒ 界面上会回落中文原文）。
    /// </summary>
    internal static List<string> KeylessViolations(
        IEnumerable<string> declared, IEnumerable<string> noKey,
        Dictionary<string, string> zh, Dictionary<string, string> en)
    {
        var d = new HashSet<string>(declared, StringComparer.Ordinal);
        var n = new HashSet<string>(noKey, StringComparer.Ordinal);
        var bad = new List<string>();

        foreach (var code in n.Except(d).OrderBy(x => x, StringComparer.Ordinal))
            bad.Add($"Keyless 表里的 {code} 根本没声明 ⇒ 表里多了一行");

        foreach (var code in n)
        {
            var key = ExternalText.GenIssueKey(code);
            if (zh.ContainsKey(key) || en.ContainsKey(key))
                bad.Add($"{key}：Keyless 表说它刻意无键，但字典里已经有它了 ⇒ 请把那一行摘掉（那张表过期了）");
        }

        foreach (var code in d.Except(n).OrderBy(x => x, StringComparer.Ordinal))
        {
            var key = ExternalText.GenIssueKey(code);
            if (!zh.ContainsKey(key)) bad.Add($"{key}：zh-CN 缺键 ⇒ 界面会回落中文原文");
            if (!en.ContainsKey(key)) bad.Add($"{key}：en-US 缺键 ⇒ 界面会回落中文原文");
        }

        return bad;
    }

    /// <summary>悬空族**允许的**三个定位码（顺序无关；<see cref="DanglingLocationViolations"/> 用）。</summary>
    internal static readonly string[] DanglingLocationCodes =
    {
        FindingLocationCodes.SCREEN_CONTROL_PROPERTY,
        FindingLocationCodes.SCREEN_CONTROL,
        FindingLocationCodes.SCREEN_CONTROL_ACTION,
    };

    /// <summary>
    /// 阶段 93 之前 <c>CrossReferenceAnalyzer.FindDanglingReferences</c> 拼的三种形状 ——
    /// <c>Location</c>（**字典缺键时的回落原文**）必须仍是其中之一。
    /// </summary>
    private static readonly Regex DanglingFallbackShape = new(
        @"^\[[^\]]+\] 控件 '[^']+'( \([^)]+\)| 动作)?$", RegexOptions.Compiled);

    /// <summary>
    /// **悬空引用的定位必须已码化**（阶段 93，闭环 <b>#61l</b>）—— 三件事一起断：
    /// ① <c>LocationCode</c> 非空、且落在**悬空族的那三个档位**内；
    /// ② 实参个数与档位匹配（<c>SCREEN_CONTROL_PROPERTY</c> 3 个、另两个 2 个）；
    /// ③ <c>Location</c>（回落原文）仍是**改动前的格式** —— 方括号 + 单引号 +
    ///   可选的 <c> (属性)</c> / <c> 动作</c> 尾巴。字典缺键时用户看到的东西一字不变，
    ///   这才是「只改了正常路径」的证明。
    ///
    /// <para><b>为什么非有不可</b>：这条定位的码**不由 <c>GeneratedCodeValidator</c> 自己拼**
    /// （来自 <see cref="DanglingRefLocation"/> 的三个工厂方法）⇒ 源码面的
    /// <c>FindingLocationCodes.</c> 计数对它**看不见**（那条计数只会数到本文件自己的站点）。
    /// 少了这一条，把 <c>loc.Code</c> 改回 <c>""</c> 就只剩"<c>SCREEN_CONTROL_ACTION</c> 变死码"
    /// 这一条**顺带**的间接信号。</para>
    ///
    /// <para>⚠ 判据的输入面是**真实产出**（<see cref="RealFindings"/>），不是本文件造的夹具 ——
    /// 手搓 finding 等于拿判据自己的定义当证据（本项目踩过）。</para>
    /// </summary>
    internal static List<string> DanglingLocationViolations(IEnumerable<GeneratedCodeFinding> findings)
    {
        var bad = new List<string>();
        foreach (var f in findings.Where(f => f.Code == GeneratedCodeIssueCodes.DANGLING_REFERENCE))
        {
            if (string.IsNullOrEmpty(f.LocationCode))
            {
                bad.Add($"DANGLING_REFERENCE：LocationCode 为空 ⇒ 英文界面下这条定位中英混排（登记 #61l 正是这件事）");
                continue;
            }

            if (!DanglingLocationCodes.Contains(f.LocationCode, StringComparer.Ordinal))
            {
                bad.Add($"DANGLING_REFERENCE：定位码 {f.LocationCode} 不在悬空族内"
                        + $"（{string.Join(" / ", DanglingLocationCodes)}）");
                continue;
            }

            var wantArgs = f.LocationCode == FindingLocationCodes.SCREEN_CONTROL_PROPERTY ? 3 : 2;
            var gotArgs = f.LocationArgs?.Count ?? 0;
            if (gotArgs != wantArgs)
                bad.Add($"DANGLING_REFERENCE / {f.LocationCode}：实参 {gotArgs} 个，模板要 {wantArgs} 个");

            if (!DanglingFallbackShape.IsMatch(f.Location))
                bad.Add($"DANGLING_REFERENCE / {f.LocationCode}：回落原文不再保留旧格式「{f.Location}」"
                        + "（字典缺键时用户看到的东西必须与改动前逐字一致）");
        }

        return bad;
    }

    /// <summary>
    /// **源码面**：产出方必须还在发码、出口必须仍是唯一键推导点、消费方必须**走码化出口**
    /// 且**不再直显**。
    ///
    /// <para><b>为什么这条非有不可</b>：本缺陷的本质不是"少翻译一个词"，而是
    /// **界面那两条通道根本没经过字典** —— 只加码表、消费点不接线，用户所见一字不变。
    /// 三个文件的检查缺一不可（与 M11 / M12 同一形状）。</para>
    ///
    /// <para>剥注释是必须的：讲规则的注释里会写出反例（登记 #48 踩过三次）——
    /// 本阶段自己在码表与视图的注释里就写了两遍 <c>finding.Message</c>，
    /// 不剥注释的话这条判据会被自己的文档满足。</para>
    /// </summary>
    internal static List<string> WiringViolations(IEnumerable<(string Relative, string Text)> sources)
    {
        var bad = new List<string>();
        var map = sources.ToDictionary(s => s.Relative, s => SourceScanner.StripComments(s.Text),
            StringComparer.Ordinal);

        // ① 产出方：每个站点都要发码
        if (!map.TryGetValue(Producer, out var producer))
            bad.Add($"{Producer}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            var messages = Regex.Matches(producer, @"GeneratedCodeIssueCodes\.").Count;
            if (messages < 18)
                bad.Add($"{Producer}：只有 {messages} 处发消息码（要求 ≥ 18）⇒ 有 finding 的文案又退回 Core 里直接拼中文了");

            var locations = Regex.Matches(producer, @"FindingLocationCodes\.").Count;
            if (locations < 7)
                bad.Add($"{Producer}：只有 {locations} 处发定位码（要求 ≥ 7）⇒ 有定位又退回 Core 里直接拼中文了");
        }

        // ①b 悬空引用的定位产出方：三个工厂方法各发一个码；且调用方真的把码接了进去
        if (!map.TryGetValue(LocationProducer, out var locProducer))
            bad.Add($"{LocationProducer}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            foreach (var name in new[]
                     {
                         "FindingLocationCodes.SCREEN_CONTROL_PROPERTY",
                         "FindingLocationCodes.SCREEN_CONTROL",
                         "FindingLocationCodes.SCREEN_CONTROL_ACTION",
                     })
                if (!locProducer.Contains(name, StringComparison.Ordinal))
                    bad.Add($"{LocationProducer}：{name} 不发了 ⇒ 悬空引用的定位又退回拼中文串");
        }

        if (!map.TryGetValue(Producer, out var producerForLoc)
            || !producerForLoc.Contains("loc.Code", StringComparison.Ordinal))
            bad.Add($"{Producer}：悬空那条 finding 没把记录体的码接进 LocationCode（登记 #61l）"
                    + " ⇒ LocationCode 留空时英文界面下这条定位中英混排");

        // ② 出口：键推导必须还在（它是"码 → 键"的唯一实现）
        if (!map.TryGetValue(Outlet, out var outlet))
            bad.Add($"{Outlet}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            if (!outlet.Contains("GenIssueKey(string code)", StringComparison.Ordinal))
                bad.Add($"{Outlet}：gen.issue. 的键推导出口没了");
            if (!outlet.Contains("GenLocationKey(string code)", StringComparison.Ordinal))
                bad.Add($"{Outlet}：gen.loc. 的键推导出口没了");
        }

        // ③ 消费方：两条通道都走出口，且**不再直显**
        if (!map.TryGetValue(Consumer, out var consumer))
            bad.Add($"{Consumer}：语料里找不到这个文件（扫描面失效？）");
        else
        {
            if (!consumer.Contains("ExternalText.Describe(finding)", StringComparison.Ordinal))
                bad.Add($"{Consumer}：列表项没走 ExternalText.Describe ⇒ 消息会以中文回落原文直接上屏（这正是本项缺陷本来的样子）");
            if (!consumer.Contains("ExternalText.DescribeLocation(finding)", StringComparison.Ordinal))
                bad.Add($"{Consumer}：定位没走 ExternalText.DescribeLocation ⇒ 定位串会直接上屏");

            foreach (var shape in new[] { "finding.Message", "finding.Location", "f.Message", "f.Location" })
                if (consumer.Contains(shape, StringComparison.Ordinal))
                    bad.Add($"{Consumer}：仍有 `{shape}` 直显站点 ⇒ 码化出口被绕过了");
        }

        return bad;
    }

    // ══════════════════════════ 判据 ══════════════════════════

    /// <summary>
    /// 主判据：**每一条真实 finding** 的文案经 <c>ExternalText.Describe</c> 渲染后，
    /// 结果都必须来自字典（含实参填充），**不许出现回落原文**。
    ///
    /// <para><b>为什么用毒化而不是拿真原文比对</b>：拿真原文只能测"今天恰好翻了没"，
    /// 毒化测的是**"渲染这一半确实由字典决定"** —— 后者才是这条链路的性质。</para>
    /// </summary>
    [Fact]
    public void EveryFinding_RendersTheDictionaryTemplate_NeverTheChineseFallback()
    {
        // ⚠ 语言必须**钉住**（阶段 91 / #61i）：实参里有记录体，而它的期望串按**当前语言**渲染
        //   ⇒ 不钉的话 zh / en 两个候选串都随环境漂移，"取并集"这一招就失去意义了。
        //   （语言那一维由 BothLanguages_… 专门负责，这里只负责"渲染确实来自字典"。）
        var original = AppLocalization.Service.CurrentLanguage;
        try
        {
            AppLocalization.Service.SwitchLanguage("zh-CN");
            AssertEveryFindingRendersFromTheDictionary();
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }
    }

    /// <summary>上面那条判据的实现（单独一个方法只为把语言钉在 <c>zh-CN</c> 之外不重排缩进）。</summary>
    private void AssertEveryFindingRendersFromTheDictionary()
    {
        var zh = Zh;
        var en = En;
        var keyless = MessageNoKey.Select(n => n.Code).ToHashSet(StringComparer.Ordinal);
        var rows = new List<(string Label, string Rendered, string Key, string[] Candidates)>();
        var noKeyRows = new List<string>();

        foreach (var hit in RealFindings())
        {
            var key = ExternalText.GenIssueKey(hit.Finding.Code);

            if (keyless.Contains(hit.Finding.Code))
            {
                // 刻意无键的那一档：渲染**必须原样回落**（毒化值就是证据），
                // 而"它真的没有键"由 KeylessViolations 双向守着。
                var fallen = ExternalText.Describe(hit.Finding with { Message = Poison });
                noKeyRows.Add($"    {hit.Label,-52} {key,-44} ⇒ {(fallen == Poison ? "回落原文（有意）" : fallen)}");
                Assert.True(fallen == Poison,
                    $"{hit.Finding.Code}：Keyless 表说它没有字典键、应回落原文，实际渲染成「{fallen}」");
                continue;
            }

            var candidates = new[] { Expected(zh, key, hit.Finding.Args), Expected(en, key, hit.Finding.Args) }
                .Where(v => v.Length > 0)
                .ToArray();

            rows.Add((hit.Label, ExternalText.Describe(hit.Finding with { Message = Poison }), key, candidates));
        }

        JudgeReadings.Write(_out, "生成代码验证结果的文案（#61g · gen.issue.*）",
            rows.Select(r => $"    {r.Label,-52} {r.Key,-44} ⇒ {r.Rendered}")
                .Concat(noKeyRows).ToList());

        var problems = CsvImportIssueLocalizationGuardTests.LeakViolations(rows, Poison);
        Assert.True(problems.Count == 0,
            "有 finding 的文案没走字典（登记 #61g / 阶段 89）。正确动作是补 "
            + "`gen.issue.<码小写>` 的字典键，**不是**把中文串再拼回 Core"
            + "（那会让英文界面下整个对话框都是中文）：\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 「刻意无键」那一档的**双向**自检 + 它必须真的还在产出（否则那条回落就是凭空存在）。
    /// </summary>
    [Fact]
    public void KeylessMessages_AreExactlyTheOnesWeDeclared()
    {
        var declared = DeclaredCodes();
        Assert.True(declared.Count >= 18, $"只反射到 {declared.Count} 个消息码 —— 取数失效（下面的判定会恒真）");

        var problems = KeylessViolations(declared, MessageNoKey.Select(n => n.Code), Zh, En);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));

        // 表里的码必须真的产得出来 —— 否则它跟 NotProducible 一样该进那张表，
        // 而不是占着"刻意无键"这个更体面的位置。
        var produced = RealFindings().Select(h => h.Finding.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var (code, _) in MessageNoKey)
            Assert.True(produced.Contains(code), $"{code} 在 Keyless 表里，但当前夹具产不出它 ⇒ 它该进 NotProducible");
    }

    /// <summary>
    /// 定位面：**有码的那一档**必须渲染成字典模板；**<c>LocationCode == ""</c> 的那一档**
    /// 必须直出且**与语言无关**（不含汉字）—— 阶段 93 起**没有第三种可能**了
    /// （原来的"外来定位"档随 #61l 闭环消失）。
    ///
    /// <para>这一条同时守两侧：往"直出"档塞一条新的中文定位文案 ⇒ 红；
    /// 给"中性定位"补了个字典键却又忘了设 <c>LocationCode</c> ⇒ 渲染仍等于原文 ⇒ 也红。</para>
    /// </summary>
    [Fact]
    public void EveryLocation_RendersTheDictionaryTemplate_OrIsLanguageNeutral()
    {
        var zh = Zh;
        var en = En;
        var rows = new List<(string Label, string Rendered, string Key, string[] Candidates)>();
        var neutral = new List<string>();

        foreach (var hit in RealFindings())
        {
            var f = hit.Finding;

            if (string.IsNullOrEmpty(f.LocationCode))
            {
                // 中性定位（模型路径 / "-" / 屏幕 id 列表）：直出，且不许含汉字
                Assert.Equal(f.Location, ExternalText.DescribeLocation(f));
                Assert.False(Cjk.IsMatch(f.Location),
                    $"{hit.Label}：LocationCode 为空表示与语言无关，但定位串里有汉字：「{f.Location}」"
                    + " ⇒ 要么给它建码走 gen.loc.*，要么它本来就不该是中文");
                neutral.Add($"    {hit.Label,-52} ⇒ {f.Location}   [中性·直出]");
                continue;
            }

            var key = ExternalText.GenLocationKey(f.LocationCode);
            var candidates = new[]
                {
                    Expected(zh, key, f.LocationArgs), Expected(en, key, f.LocationArgs),
                }
                .Where(v => v.Length > 0)
                .ToArray();

            rows.Add((hit.Label, ExternalText.DescribeLocation(f with { Location = Poison }), key, candidates));
        }

        JudgeReadings.Write(_out, "生成代码验证结果的定位（#61g · gen.loc.*）",
            rows.Select(r => $"    {r.Label,-52} {r.Key,-40} ⇒ {r.Rendered}")
                .Concat(neutral).ToList());

        var problems = CsvImportIssueLocalizationGuardTests.LeakViolations(rows, Poison);
        Assert.True(problems.Count == 0,
            "有 finding 的定位串没走字典（登记 #61g）：\n  - " + string.Join("\n  - ", problems));
    }


    /// <summary>
    /// 切到两种语言各渲染一遍：**中文那一遍必须有汉字、英文那一遍的汉字只许来自实参**，
    /// 且两遍不同。
    ///
    /// <para>与主判据成对：主判据走"两本字典的期望串取并集"，分不出"渲染出的是哪一本"；
    /// 这条把语言真的切过去，钉住「en-US 不是抄的 zh」。</para>
    ///
    /// <para><b>为什么是"剥掉实参之后不许剩汉字"而不是"渲染里不许有汉字"</b>：
    /// <c>FACEPLATE_TEXT_UNRESOLVED</c> 的实参就是控件文本本身、
    /// <c>FACEPLATE_BINDING_*</c> 的实参里也有绑定 id（用户数据，与界面语言无关）
    /// ⇒ 直接断言"无汉字"会把**已知的诚实边界**判成红。
    /// ⚠ 阶段 91 起 <c>EXPRESSION_SYNTAX</c> **不再**属于这条边界（登记 #61i 已闭环：
    /// 它的第二个实参改成按语言渲染的记录体），所以下面那支剥实参的弱化只覆盖 FACEPLATE 那一档。
    /// 剥实参这个动作只会**移除**汉字（不会凭空造），所以这条断言是上面那条的弱化版、
    /// 不会假红 —— 而"模板本身没翻"由
    /// <see cref="BothDictionaries_CarryEveryCode_AndAreActuallyPaired"/> 的
    /// en-无汉字那一支独立兜住。</para>
    /// </summary>
    [Fact]
    public void BothLanguages_RenderDistinctly_AndOnlyArgsMayCarryForeignText()
    {
        var keyless = MessageNoKey.Select(n => n.Code).ToHashSet(StringComparer.Ordinal);
        var hits = RealFindings()
            .GroupBy(h => h.Finding.Code, StringComparer.Ordinal)
            .Select(g => g.First())
            .Where(h => !keyless.Contains(h.Finding.Code))
            .OrderBy(h => h.Finding.Code, StringComparer.Ordinal)
            .ToList();

        var original = AppLocalization.Service.CurrentLanguage;

        try
        {
            AppLocalization.Service.SwitchLanguage("zh-CN");
            var zhRenders = hits.Select(h => ExternalText.Describe(h.Finding)).ToList();
            Assert.All(zhRenders, r => Assert.Matches(Cjk, r));

            AppLocalization.Service.SwitchLanguage("en-US");
            var enRenders = hits.Select(h => ExternalText.Describe(h.Finding)).ToList();

            JudgeReadings.Write(_out, "两种语言下的渲染（#61g）",
                hits.Select((h, i) => $"    {h.Finding.Code,-44} zh={zhRenders[i]} | en={enRenders[i]}").ToList());

            for (var i = 0; i < enRenders.Count; i++)
            {
                Assert.NotEqual(zhRenders[i], enRenders[i]);

                var skeleton = ArgTexts(hits[i].Finding)
                    .Aggregate(enRenders[i], (s, a) => s.Replace(a, "", StringComparison.Ordinal));

                Assert.False(Cjk.IsMatch(skeleton),
                    $"{hits[i].Finding.Code}：把实参剥掉之后 en-US 的渲染里仍有汉字「{skeleton}」"
                    + " ⇒ 模板本身没翻（实参里的中文是登记 #61i 的诚实边界，模板里的不是）");
            }
        }
        finally
        {
            AppLocalization.Service.SwitchLanguage(original);
        }
    }

    /// <summary>悬空引用的定位**已码化**且**回落原文保旧格式**（见 <see cref="DanglingLocationViolations"/>）。</summary>
    [Fact]
    public void DanglingRefs_CarryACodedLocation_AndKeepTheOldFallbackFormat()
    {
        var hits = RealFindings();
        var dangling = hits.Where(h => h.Finding.Code == GeneratedCodeIssueCodes.DANGLING_REFERENCE).ToList();
        Assert.True(dangling.Count > 0, "夹具产不出任何悬空引用 ⇒ 下面的判定恒真");
        foreach (var code in DanglingLocationCodes)
            Assert.True(dangling.Any(h => h.Finding.LocationCode == code),
                $"夹具里没有一条 DANGLING_REFERENCE 用 {code} ⇒ 那个档位没有被真实产出（闭包会误报死码）");

        JudgeReadings.Write(_out, "悬空引用的定位（#61l）",
            dangling.Select(h => $"    {h.Label,-52} {h.Finding.LocationCode,-28} ⇒ {h.Finding.Location}").ToList());

        var problems = DanglingLocationViolations(hits.Select(h => h.Finding));
        Assert.True(problems.Count == 0,
            "悬空引用的定位没有码化（登记 #61l）：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>两本字典对每个**有键**的码都齐 + 成对 + **占位符个数等于产出方实测的实参个数**。</summary>
    [Fact]
    public void BothDictionaries_CarryEveryCode_AndAreActuallyPaired()
    {
        var keyed = KeyedCodes();
        Assert.True(keyed.Count >= 17, $"只有 {keyed.Count} 个有键的消息码 —— 取数失效（下面的判定会恒真）");

        // 实参个数**实测**：产得出的码从真实 finding 里读；兜底那一档读不到 ⇒ 由表供给，
        // 并由 NotProducibleArm_IsStillLiveAndStillTakesTwoArgs 在源码面钉住。
        var argCounts = RealFindings()
            .GroupBy(h => h.Finding.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Finding.Args?.Count ?? 0, StringComparer.Ordinal);

        var (npCode, npArgs, _) = NotProducible.Single();
        argCounts.TryAdd(npCode, npArgs);

        var problems = CsvImportIssueLocalizationGuardTests.PairingViolations(
            keyed, argCounts, Zh, En, ExternalText.GenIssueKey);
        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));

        // 定位码单独走一遍 —— 占位符个数必须等于 LocationArgs 个数（3 个的那一档最容易漏）。
        var locArgCounts = RealFindings()
            .Where(h => h.Finding.LocationCode.Length > 0)
            .GroupBy(h => h.Finding.LocationCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Finding.LocationArgs?.Count ?? 0, StringComparer.Ordinal);
        foreach (var code in DeclaredLocationCodes())
            locArgCounts.TryAdd(code, 0);

        var locProblems = CsvImportIssueLocalizationGuardTests.PairingViolations(
            DeclaredLocationCodes(), locArgCounts, Zh, En, ExternalText.GenLocationKey);
        Assert.True(locProblems.Count == 0, string.Join("\n  - ", locProblems));
    }

    /// <summary>
    /// 码表**三态闭包**：声明的码都真的产得出（或如实进兜底表），产出的码都声明了。
    /// 消息码与定位码各走一遍（定位码当前没有兜底档 ⇒ 兜底集合刻意为空）。
    /// </summary>
    [Fact]
    public void DeclaredCodes_AndProducedCodes_AreTheSameSet()
    {
        var hits = RealFindings();
        var produced = hits.Select(h => h.Finding.Code).Distinct(StringComparer.Ordinal).ToList();
        var producedLocations = hits.Select(h => h.Finding.LocationCode)
            .Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).ToList();

        var problems = CsvImportIssueLocalizationGuardTests.ClosureViolations(
                DeclaredCodes(), produced, NotProducible.Select(n => n.Code))
            .Concat(CsvImportIssueLocalizationGuardTests.ClosureViolations(
                DeclaredLocationCodes(), producedLocations, Array.Empty<string>()))
            .ToList();

        JudgeReadings.Write(_out, "生成代码验证码表（#61g）",
            new List<string>
            {
                $"    消息码：声明 {DeclaredCodes().Count} 个 / 真实产出 {produced.Count} 个"
                + $" / 兜底 {NotProducible.Length} 个 / 刻意无键 {MessageNoKey.Length} 个",
                $"    定位码：声明 {DeclaredLocationCodes().Count} 个 / 真实产出 {producedLocations.Count} 个"
                + $"（{string.Join(", ", producedLocations.OrderBy(x => x, StringComparer.Ordinal))}）",
            });

        Assert.True(problems.Count == 0, string.Join("\n  - ", problems));
    }

    /// <summary>
    /// **兜底那一档不许变成免费通行证**：它必须①仍在产出方源码里；
    /// ②那处仍带着 2 个实参；③回落文案仍是可用中文。
    ///
    /// <para>这是把 <see cref="NotProducible"/> 里手写的"实参个数 = 2"钉回源码的唯一出口 ——
    /// 没有它，那张表就是一个没人核的数字（豁免表里的"理由"会变成能把缺陷合法化的字段，
    /// 登记 #54 的老病）。</para>
    /// </summary>
    [Fact]
    public void NotProducibleArm_IsStillLiveAndStillTakesTwoArgs()
    {
        var sources = AppSourceScanner.ScanAllProductSources();
        var producer = sources.FirstOrDefault(s => s.Relative == Producer);
        Assert.False(producer.Text is null, $"语料里找不到 {Producer}（扫描面失效？）");

        var code = SourceScanner.StripComments(producer.Text);
        var marker = "GeneratedCodeIssueCodes." + GeneratedCodeIssueCodes.RUNTIME_USER_UNKNOWN;
        var idx = code.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(idx >= 0, $"产出方里找不到兜底码 {marker} —— 那条防线被拆了？");

        var window = code.Substring(idx, Math.Min(400, code.Length - idx));

        Assert.Contains("new object[] { issue.Index, issue.Username }", window, StringComparison.Ordinal);
        Assert.Matches(Cjk, window);   // 回落文案仍是中文（字典缺键时用户还能读到东西）
    }

    /// <summary>回落文案必须**自身可用**：非空、含汉字、不是码名（字典缺键时用户还能读到东西）。</summary>
    [Fact]
    public void FallbackMessages_StayUsableChinese()
    {
        foreach (var hit in RealFindings())
        {
            var f = hit.Finding;
            Assert.False(string.IsNullOrWhiteSpace(f.Message), $"{f.Code}：回落原文是空的");
            Assert.Matches(Cjk, f.Message);
            Assert.DoesNotContain(f.Code, f.Message, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(f.Location), $"{f.Code}：定位串是空的");
        }
    }

    /// <summary>产出方还在发码；出口仍是唯一键推导点；消费方**走码化出口且不再直显**（反回归钉）。</summary>
    [Fact]
    public void ConsumerKeepsTheCodedShape()
    {
        var problems = WiringViolations(AppSourceScanner.ScanAllProductSources());

        JudgeReadings.Write(_out, "生成代码验证结果的接线面（#61g）",
            new[] { Producer, Outlet, Consumer }.Select(f => $"    {f}").ToList());

        Assert.True(problems.Count == 0,
            "文案到界面的这条链断了（登记 #61g）。**只加码表不算收口** —— "
            + "消费点不接线时用户所见一字不变：\n  - " + string.Join("\n  - ", problems));
    }

    /// <summary>
    /// 负控 5 组 —— 每组都要求"该红的红、该绿的绿"，
    /// 否则证明不了判据真的在盯那件事（本项目对负控的一贯要求）。
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        var zh = Zh;
        var en = En;
        var keyed = KeyedCodes();
        var declared = DeclaredCodes();
        var findings = RealFindings();
        var argCounts = findings
            .GroupBy(h => h.Finding.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Finding.Args?.Count ?? 0, StringComparer.Ordinal);
        {
            var (npCode, npArgs, _) = NotProducible.Single();
            argCounts.TryAdd(npCode, npArgs);
        }

        var key0 = ExternalText.GenIssueKey(keyed[0]);
        var produced = findings.Select(h => h.Finding.Code).ToList();
        var notProducible = NotProducible.Select(n => n.Code).ToList();

        // ── NC1 · 成对性：正控绿，三种坏法各点名 ──
        Assert.Empty(CsvImportIssueLocalizationGuardTests.PairingViolations(
            keyed, argCounts, zh, en, ExternalText.GenIssueKey));
        Assert.Empty(CsvImportIssueLocalizationGuardTests.ClosureViolations(declared, produced, notProducible));

        var enCopy1 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy1[key0] = zh[key0];
        Assert.Contains(CsvImportIssueLocalizationGuardTests.PairingViolations(
                keyed, argCounts, zh, enCopy1, ExternalText.GenIssueKey),
            v => v.Contains("中文字符", StringComparison.Ordinal));

        var enCopy2 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy2.Remove(key0);
        Assert.Contains(CsvImportIssueLocalizationGuardTests.PairingViolations(
                keyed, argCounts, zh, enCopy2, ExternalText.GenIssueKey),
            v => v.Contains("缺键", StringComparison.Ordinal));

        // 带实参的那个码当成 0 实参 ⇒ 占位符那一支点名（这正是"字典与产出方对不上"
        // 会渲染出字面 {0} 的那条路）。⚠ 反例**必须**挑"实测确实带实参"的码 ——
        // 按位置取下标会挑到 0 实参的那一档、静默跳过这一支（本项目踩过）。
        var withArgs = keyed.First(c => argCounts.TryGetValue(c, out var n) && n > 0);
        var argCopy = new Dictionary<string, int>(argCounts, StringComparer.Ordinal) { [withArgs] = 0 };
        Assert.Contains(CsvImportIssueLocalizationGuardTests.PairingViolations(
                keyed, argCopy, zh, en, ExternalText.GenIssueKey),
            v => v.Contains("占位符", StringComparison.Ordinal));

        // ── NC2 · 三态闭包：三支都要报 ──
        Assert.Contains(CsvImportIssueLocalizationGuardTests.ClosureViolations(
                declared.Concat(new[] { "NOT_A_REAL_CODE" }), produced, notProducible),
            v => v.Contains("死码", StringComparison.Ordinal));
        Assert.Contains(CsvImportIssueLocalizationGuardTests.ClosureViolations(
                declared, produced.Concat(new[] { "NOT_A_REAL_CODE" }), notProducible),
            v => v.Contains("没声明", StringComparison.Ordinal));
        Assert.Contains(CsvImportIssueLocalizationGuardTests.ClosureViolations(
                declared, produced, new[] { produced[0] }),
            v => v.Contains("已经能真实产出", StringComparison.Ordinal));

        // 定位码那一侧：声明了却产出不到 ⇒ 死码（防"建了码但忘了发"）
        Assert.Contains(CsvImportIssueLocalizationGuardTests.ClosureViolations(
                DeclaredLocationCodes(), Array.Empty<string>(), Array.Empty<string>()),
            v => v.Contains("死码", StringComparison.Ordinal));

        // ── NC3 · 刻意无键那张表：正控绿、两支坏法各点名 ──
        var noKey = MessageNoKey.Select(n => n.Code).ToList();
        Assert.Empty(KeylessViolations(declared, noKey, zh, en));

        var zhCopy = new Dictionary<string, string>(zh, StringComparer.Ordinal)
            { [ExternalText.GenIssueKey(MessageNoKey.Single().Code)] = "不该有这个键" };
        Assert.Contains(KeylessViolations(declared, noKey, zhCopy, en),
            v => v.Contains("请把那一行摘掉", StringComparison.Ordinal));

        var enCopy3 = new Dictionary<string, string>(en, StringComparer.Ordinal);
        enCopy3.Remove(ExternalText.GenIssueKey(keyed[1]));
        Assert.Contains(KeylessViolations(declared, noKey, zh, enCopy3),
            v => v.Contains("缺键", StringComparison.Ordinal));

        Assert.Contains(KeylessViolations(declared, noKey.Concat(new[] { "NOT_A_REAL_CODE" }), zh, en),
            v => v.Contains("多了一行", StringComparison.Ordinal));

        // ── NC4 · 悬空引用的定位（#61l）：正控绿、四种坏法各点名 ──
        Assert.Empty(DanglingLocationViolations(findings.Select(h => h.Finding)));

        var danglingReal = findings.First(h => h.Finding.Code == GeneratedCodeIssueCodes.DANGLING_REFERENCE);
        // ① 码被抹掉（本刀之前的样子）⇒ 点名"中英混排"
        Assert.Contains(DanglingLocationViolations(new[]
                { danglingReal.Finding with { LocationCode = "", LocationArgs = null } }),
            v => v.Contains("LocationCode 为空", StringComparison.Ordinal));
        // ② 码不属于本族 ⇒ 点名（防"随便塞一个码把判据糊过去"）
        Assert.Contains(DanglingLocationViolations(new[]
                { danglingReal.Finding with { LocationCode = FindingLocationCodes.GENERATED_CODE } }),
            v => v.Contains("不在悬空族内", StringComparison.Ordinal));
        // ③ 实参个数与档位不匹配 ⇒ 点名（渲染会留字面 {2}）。
        //    ⚠ 取样必须钉在**3 实参那一档**：`danglingReal` 是"第一条悬空 finding"，
        //    若它恰好落在 2 实参的档位上，塞 2 个实参就**合法**了 ⇒ 这条负控会静默变成
        //    一个恒真的断言（本项目踩过"按位置取下标挑到 0 实参档"的坑）。
        var danglingProps = findings.First(h =>
            h.Finding.Code == GeneratedCodeIssueCodes.DANGLING_REFERENCE
            && h.Finding.LocationCode == FindingLocationCodes.SCREEN_CONTROL_PROPERTY);
        Assert.Contains(DanglingLocationViolations(new[]
                { danglingProps.Finding with { LocationArgs = new object[] { "s", "c" } } }),
            v => v.Contains("模板要 3 个", StringComparison.Ordinal));
        // ④ 回落原文被改格式 ⇒ 点名（缺键时用户看到的东西变了，才是"顺手改了别的"）
        Assert.Contains(DanglingLocationViolations(new[]
                { danglingReal.Finding with { Location = "main.value" } }),
            v => v.Contains("回落原文不再保留旧格式", StringComparison.Ordinal));

        // ── NC5 · 渲染泄漏：回落原文冒头 ⇒ 点名；字典期望串 ⇒ 绿 ──
        var hit0 = findings.First(h => h.Finding.Code == keyed[0]);
        var expected0 = Expected(zh, key0, hit0.Finding.Args);
        var leak = new[] { ("演示", $"提示: {Poison}", key0, new[] { expected0 }) };
        Assert.Contains(CsvImportIssueLocalizationGuardTests.LeakViolations(leak, Poison),
            v => v.Contains("中文回落原文", StringComparison.Ordinal));

        var clean = new[] { ("演示", expected0, key0, new[] { expected0 }) };
        Assert.Empty(CsvImportIssueLocalizationGuardTests.LeakViolations(clean, Poison));

        // 实参没填 ⇒ 期望串与渲染串不等 ⇒ 一样点名（否则"实参"这一维没人管）
        Assert.NotEqual(zh[key0], expected0);   // 前置：这一对本来就该不同
        var wrongArgs = new[] { ("演示", zh[key0], key0, new[] { expected0 }) };
        Assert.Contains(CsvImportIssueLocalizationGuardTests.LeakViolations(wrongArgs, Poison),
            v => v.Contains("有别的来源", StringComparison.Ordinal));

        // ── NC5 · 接线面：四个文件各有坏法；语料缺文件也要报 ──
        var all = new[] { Producer, Outlet, Consumer, LocationProducer };
        var goodProducer = string.Concat(Enumerable.Repeat(
                "f(GeneratedCodeIssueCodes.NO_SCREENS, X);\n", 18))
            + string.Concat(Enumerable.Repeat("g(FindingLocationCodes.SCREEN, Y);\n", 7))
            + "n.LocationCode = loc.Code;";
        var goodOutlet = "static string GenIssueKey(string code) => p + code;"
            + "static string GenLocationKey(string code) => p + code;";
        var goodConsumer = "a = ExternalText.Describe(finding); b = ExternalText.DescribeLocation(finding);";
        var goodLocationProducer =
            "FindingLocationCodes.SCREEN_CONTROL_PROPERTY; FindingLocationCodes.SCREEN_CONTROL;"
            + " FindingLocationCodes.SCREEN_CONTROL_ACTION;";

        (string Relative, string Text)[] Corpus(string target, string body)
            => all.Select(f => (f, f == target ? body
                : f == Producer ? goodProducer
                : f == Outlet ? goodOutlet
                : f == Consumer ? goodConsumer : goodLocationProducer)).ToArray();

        // ① 产出方退回裸中文串（一条码都不发）⇒ 两条都点名
        var dropped = WiringViolations(Corpus(Producer, "findings.Add(new(sev, cat, \"项目没有屏幕\"));"));
        Assert.Contains(dropped, v => v.Contains("消息码", StringComparison.Ordinal));
        Assert.Contains(dropped, v => v.Contains("定位码", StringComparison.Ordinal));

        // ② 合规写法 ⇒ 无话可说（证明它不是"永远返回违规"的判据）
        Assert.Empty(WiringViolations(Corpus(Producer, goodProducer)));

        // ③ 消费方退回直显（本缺陷本来的样子）⇒ 两条直显形状都点名，出口也点名
        var direct = WiringViolations(Corpus(Consumer, "a = finding.Message; b = finding.Location;"));
        Assert.Contains(direct, v => v.Contains("finding.Message", StringComparison.Ordinal));
        Assert.Contains(direct, v => v.Contains("finding.Location", StringComparison.Ordinal));
        Assert.Contains(direct, v => v.Contains("没走 ExternalText.Describe", StringComparison.Ordinal));

        // ③b 定位产出方被掏空（三个码一个都不发）⇒ 三个码名各点名
        var noLocProducer = WiringViolations(Corpus(LocationProducer, "// 什么都不发"));
        foreach (var name in new[] { "SCREEN_CONTROL_PROPERTY", "SCREEN_CONTROL_ACTION" })
            Assert.Contains(noLocProducer, v => v.Contains(name, StringComparison.Ordinal));

        // ③c 调用方不再接线（loc.Code 丢掉）⇒ 点名"没把记录体的码接进 LocationCode"
        Assert.Contains(WiringViolations(Corpus(Producer,
                string.Concat(Enumerable.Repeat("f(GeneratedCodeIssueCodes.NO_SCREENS, X);\n", 18))
                + string.Concat(Enumerable.Repeat("g(FindingLocationCodes.SCREEN, Y);\n", 7)))),
            v => v.Contains("没把记录体的码接进 LocationCode", StringComparison.Ordinal));

        // ④ 语料里**缺**一个目标文件 ⇒ 必须报"找不到" ——
        //    否则扫描面整体失效（返回空语料）时会**静默全绿**：这是本函数的下界，不能省。
        Assert.Contains(WiringViolations(new[] { (Producer, goodProducer) }),
            v => v.Contains("语料里找不到这个文件", StringComparison.Ordinal));

        // ⑤ 注释里写的反例不算（规则不许被自己的文档满足，登记 #48）
        Assert.Empty(WiringViolations(Corpus(Consumer,
            goodConsumer + "\n// 反例：这里**不**走字典，直接 finding.Message / finding.Location")));
    }
}
