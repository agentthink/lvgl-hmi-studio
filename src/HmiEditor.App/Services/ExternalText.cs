using System.Collections.Generic;
using System.Linq;
using HmiEditor.AI.Models;
using HmiEditor.AI.Validation;
using HmiEditor.App.Resources;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using HmiEditor.Simulator;

namespace HmiEditor.App.Services;

/// <summary>
/// App **之外**的程序集（<c>HmiEditor.Core</c> / <c>Hmi.*.Builtin</c> 插件）产出的用户可见文案的
/// **唯一渲染实现**。
///
/// <para><b>它解决什么</b>：Core 与插件里的文案是**中文回落原文**（它们拿不到 App 的字典服务），
/// 而界面要显示的是"按约定推导键 → 查字典模板 → 填参数 → 查不到才用原文"的结果。
/// 这个动作必须**只有一份实现** —— 写两份必然漂移，而漂移的表现正是
/// **守卫绿着、界面却是中文**（这个坑本项目已经踩过一次，见
/// <c>docs/2026-09-18-plugin-text-and-lang-usage-guards.md</c>）。</para>
///
/// <para><b>键约定</b>（每条前缀一张码表，前缀清单由判据钉住）：</para>
/// <list type="bullet">
///   <item>插件校验结果 <c>hw.issue.&lt;Code 小写&gt;</c> —— 推导规则在 <see cref="PluginTextKeys"/>；</item>
///   <item>Core 脚本校验 <c>script.issue.&lt;Code 小写&gt;</c> —— 见 <see cref="ScriptIssueKey"/>；</item>
///   <item>AI 诊断 <c>ai.issue.&lt;码小写&gt;</c> —— 见 <see cref="AiIssueKey"/>，码来自
///   <c>HmiEditor.AI.AiIssueCodes</c>（校验结果 + 布局建议）；</item>
///   <item>工程包解包失败 <c>package.issue.&lt;码小写&gt;</c> —— 见 <see cref="PackageIssueKey"/>，
///   码来自 <c>HmiEditor.Core.Serialization.HmiPackageIssueCodes</c>；</item>
///   <item>BCL / 系统层失败 <c>system.issue.&lt;码小写&gt;</c> —— 见 <see cref="SystemIssueKey"/>，
///   码来自 <see cref="SystemFailureCodes"/>。⚠ 这一条与上面四条**形状不同**：前四条的码由
///   **产出方**（Core / AI / 插件）声明，这一条没有产出方 —— 异常是 BCL 自己抛的，
///   所以「异常类型 → 码」那张表只能维护在 App 侧。</item>
///   <item>SVG 解析失败 <c>svg.issue.&lt;码小写&gt;</c> —— 见 <see cref="SvgIssueKey"/>，
///   码来自 <c>HmiEditor.Core.Services.SvgParseIssueCodes</c>（阶段 87 新增，登记 <b>#61a</b>）。</item>
///   <item>CSV 变量导入问题 <c>csv.issue.&lt;码小写&gt;</c> —— 见 <see cref="CsvIssueKey"/>，
///   码来自 <c>HmiEditor.Core.Services.CsvImportIssueCodes</c>（阶段 88 新增，登记 <b>#61f</b>）。</item>
///   <item>生成代码验证结果 <c>gen.issue.&lt;码小写&gt;</c>（消息）与
///   <c>gen.loc.&lt;码小写&gt;</c>（定位串）—— 见 <see cref="GenIssueKey"/> /
///   <see cref="GenLocationKey"/>，码来自
///   <c>HmiEditor.Core.Services.GeneratedCodeIssueCodes</c> /
///   <c>FindingLocationCodes</c>（阶段 89 新增，登记 <b>#61g</b>）。
///   ⚠ 这一条**两条键族**：定位与消息的生命周期不同（定位还要被复制/筛选）。</item>
///   <item>表达式语法失败 <c>expr.syntax.&lt;码小写&gt;</c> —— 见 <see cref="ExprSyntaxKey"/>，
///   码来自 <c>HmiEditor.Core.Services.ExpressionSyntaxIssueCodes</c>（阶段 91 新增，登记 <b>#61i</b>）。
///   ⚠ 这一族是上面 <c>gen.issue.expression_syntax</c> 那条的**第二层**：它不出现在顶层消息里，
///   而是作为那条 finding 的实参 <c>{1}</c> 出现 ⇒ <see cref="Describe(GeneratedCodeFinding)"/>
///   必须在**填参前**把它渲染掉（否则用户看到的是记录体的 <c>ToString()</c> 转储）。</item>
///   <item>登录失败 <c>login.issue.&lt;码小写&gt;</c> —— 见 <see cref="LoginIssueKey"/>，
///   码来自 <c>HmiEditor.Core.Services.LoginIssueCodes</c>（阶段 99 新增，登记 <b>#61v</b>）。
///   ⚠ 这一族与 <c>svg.issue.</c> / <c>csv.issue.</c> / <c>recipe.issue.</c> /
///   <c>recipe.import.issue.</c> 同类：失败是**返回值**（<see cref="LoginResult"/>），
///   所以走 <see cref="Describe(LoginIssue)"/>、**不走** <see cref="FailureReason"/>。</item>
///   <item>用户管理 / 模拟器失败 <c>user.issue.&lt;码小写&gt;</c> 与
///   <c>sim.issue.&lt;码小写&gt;</c> —— 见 <see cref="UserIssueKey"/> /
///   <see cref="SimulatorIssueKey"/>，码来自
///   <c>HmiEditor.Core.Services.UserAdminIssueCodes</c> /
///   <c>SimulatorIssueCodes</c>（阶段 97 新增，登记 <b>#61d</b>）。
///   ⚠ **两条前缀合成一张码表族**：用户管理码在 Core、模拟器码在 <c>HmiEditor.Simulator</c>
///   ⇒ 两个取数出口合成 M15 一个机制（下界 3 ＝ 2 ＋ 1）。这一条与
///   <c>login.issue.</c> 一样走 <c>ExternalText.Describe</c>／<c>FailureReason</c> 的
///   用户档，**不是**异常档。</item>
///   <item>配方克隆失败 <c>recipe.issue.&lt;码小写&gt;</c> —— 见 <see cref="RecipeIssueKey"/>，
///   码来自 <c>HmiEditor.Core.Services.RecipeIssueCodes</c>（阶段 98 新增，登记 <b>#61t</b>）。
///   ⚠ **本条是「加了族忘了清单」的第二例**（第一例是上面 <c>user.issue.</c> /
///   <c>sim.issue.</c> 那一条，阶段 97 漏的）：阶段 98 加了族却忘了这里，而当时
///   **没有任何判据看着这份清单** ⇒ 一直没红。阶段 100 一次补上这两处，并由
///   <c>EveryKeyPrefix_IsDocumentedInTheClassComment</c>
///   （<c>RecipeImportIssueLocalizationGuardTests</c>）钉住「枚举到的每条前缀都必须在这份清单里」
///   —— 它第一次跑就点名了 <c>user.issue.</c> / <c>sim.issue.</c>（真实仓库正控），
///   这一类从此会当场红。</item>
///   <item>配方 CSV 导入问题 <c>recipe.import.issue.&lt;码小写&gt;</c> —— 见
///   <see cref="RecipeImportIssueKey"/>，码来自
///   <c>HmiEditor.Core.Services.RecipeImportIssueCodes</c>（阶段 100 新增，登记 <b>#61f</b>）。</item>
///   <item>模拟器编译 / 启动失败 <c>sim.build.issue.&lt;码小写&gt;</c> —— 见
///   <see cref="SimulatorBuildIssueKey"/>，码来自 <see cref="SimulatorBuildIssueCodes"/>
///   （阶段 103 新增，登记 <b>#61c</b>）。
///   ⚠ <b>本族是本仓库唯一一条「码表与产出方同在 <c>HmiEditor.App</c>」的族</b> ——
///   产出方是 <c>Services/SimulatorBuildService.cs</c> 与 <c>Views/BuildRunDialog.axaml.cs</c>，
///   其余各族的载体都在 Core / AI / Simulator。**它的形状差异有据可查、不是风格选择**：
///   中文回落原文留在产出方的 <c>throw new</c> 里、不经工厂，因为 App 层的硬编码中文守卫
///   （<c>HardcodedLocalizationGuardTests</c>）对 <c>src/HmiEditor.App</c> 零容忍，
///   而它**本来就豁免** <c>throw new …Exception("…")</c> 那一类 ⇒ 不必给扫描器开文件级后门。
///   别为了「与前几族形状统一」把回落搬进工厂 —— 那会让那条守卫立刻红。</item>
/// </list>
///
/// <para><b>每一条键约定都有判据守着</b>（<c>PluginTextLocalizationGuardTests</c> /
/// <c>ScriptIssueLocalizationGuardTests</c> / <c>AiIssueLocalizationGuardTests</c> /
/// <c>PackageIssueLocalizationGuardTests</c> / <c>SystemIssueLocalizationGuardTests</c> /
/// <c>SvgParseIssueLocalizationGuardTests</c> / <c>CsvImportIssueLocalizationGuardTests</c> /
/// <c>GenFindingLocalizationGuardTests</c> /
/// <c>ExpressionSyntaxIssueLocalizationGuardTests</c> /
/// <c>LoginIssueLocalizationGuardTests</c> /
/// <c>RecipeCloneIssueLocalizationGuardTests</c> /
/// <c>RecipeImportIssueLocalizationGuardTests</c>）：
/// 键必须在两本字典里都能解析，且模板的占位符个数必须等于实参个数 ——
/// <see cref="LocalizationService.Format"/> 在两者不符时**不抛异常**，只原样返回，
/// 用户会看到 <c>{1}</c> 这种字面量。⚠ **这里刻意不写"N 条约定"**：手抄的条数没人看着就会漂
/// （阶段 98 就是加了族而这两处没改，登记 #61w 同类）。</para>
/// </summary>
internal static class ExternalText
{
    /// <summary>Core 脚本校验文案键的前缀。</summary>
    internal const string ScriptIssueKeyPrefix = "script.issue.";

    /// <summary>Core 脚本校验的 <c>Code</c> → 文案键。</summary>
    internal static string ScriptIssueKey(string code) => ScriptIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>AI 诊断码文案键的前缀。</summary>
    internal const string AiIssueKeyPrefix = "ai.issue.";

    /// <summary>
    /// AI 诊断码（<c>HmiEditor.AI.AiIssueCodes</c> 里那些 <c>const string</c> 的值）→ 文案键。
    ///
    /// <para>与 <see cref="ScriptIssueKey"/> 同形，**单一来源** —— 视图与
    /// <c>AiIssueLocalizationGuardTests</c> / <c>LocalizationKeyReachabilityGuardTests</c>
    /// 都调它，不各写一份（推导规则分家过的亏本项目吃过，
    /// 见 <c>docs/2026-09-18-plugin-text-and-lang-usage-guards.md</c>）。</para>
    /// </summary>
    internal static string AiIssueKey(string code) => AiIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>工程包解包失败的诊断码文案键的前缀。</summary>
    internal const string PackageIssueKeyPrefix = "package.issue.";

    /// <summary>
    /// 工程包诊断码（<c>HmiEditor.Core.Serialization.HmiPackageIssueCodes</c> 里那些
    /// <c>const string</c> 的值）→ 文案键。
    ///
    /// <para>与前两条同形，**单一来源** —— 视图与
    /// <c>PackageIssueLocalizationGuardTests</c> / <c>LocalizationKeyReachabilityGuardTests</c>
    /// 的 M9 都调它，不各写一份推导规则。</para>
    /// </summary>
    internal static string PackageIssueKey(string code) => PackageIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>BCL / 系统层失败诊断码文案键的前缀。</summary>
    internal const string SystemIssueKeyPrefix = "system.issue.";

    /// <summary>
    /// 系统层失败诊断码（<see cref="SystemFailureCodes"/> 里那些 <c>const string</c> 的值）→ 文案键。
    ///
    /// <para>与前三条同形，**单一来源** —— <c>ExternalText</c> 自己、
    /// <c>SystemIssueLocalizationGuardTests</c> 与
    /// <c>LocalizationKeyReachabilityGuardTests</c> 的 M10 都调它，不各写一份推导规则
    /// （推导规则分家过的亏本项目吃过，见
    /// <c>docs/2026-09-18-plugin-text-and-lang-usage-guards.md</c>）。</para>
    /// </summary>
    internal static string SystemIssueKey(string code) => SystemIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>SVG 解析失败诊断码文案键的前缀。</summary>
    internal const string SvgIssueKeyPrefix = "svg.issue.";

    /// <summary>
    /// SVG 解析失败诊断码（<see cref="SvgParseIssueCodes"/> 里那些 <c>const string</c> 的值）→ 文案键。
    ///
    /// <para>与前四条同形，**单一来源** —— <see cref="Describe(SvgParseError)"/> 自己、
    /// <see cref="FailureReason"/> 的第二支、<c>SvgParseIssueLocalizationGuardTests</c> 与
    /// <c>LocalizationKeyReachabilityGuardTests</c> 的 M11 都调它，不各写一份推导规则。</para>
    ///
    /// <para><b>⚠ 这一族的搬运方式有**两**种</b>（阶段 87 实测，登记 #61a）：
    /// ① <b>返回值档</b> —— <c>SvgParser.Parse</c> 把 <see cref="SvgParseError"/> **返回**，
    /// 消费点调 <see cref="Describe(SvgParseError)"/>（UI 的「应用 SVG 资源」路径）；
    /// ② <b>异常档</b> —— AI 侧（<c>IconGenerator</c> / <c>ImageGenerator</c>）把它包进
    /// <see cref="SvgParseException"/> 抛出，再由 <see cref="FailureReason"/> 拆出来
    /// （<c>ai.gen.failed</c> 那条路径）。两条共用一个键前缀，是因为
    /// **用户在界面上看到的是同一批原因**。</para>
    /// </summary>
    internal static string SvgIssueKey(string code) => SvgIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>用户管理失败诊断码文案键的前缀。</summary>
    internal const string UserIssueKeyPrefix = "user.issue.";

    /// <summary>
    /// 用户管理失败诊断码（<c>HmiEditor.Core.Services.UserAdminIssueCodes</c> 里那些
    /// <c>const string</c> 的值）→ 文案键（阶段 97 新增，登记 <b>#61d</b>）。
    ///
    /// <para><b>与前九条同形，单一来源</b>：<see cref="FailureReason"/> 的 ②′ 支与
    /// <c>UserAdminIssueLocalizationGuardTests</c> 都调它，不各写一份推导规则
    /// （推导规则分家过的亏本项目吃过）。</para>
    /// </summary>
    internal static string UserIssueKey(string code) => UserIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>模拟器失败诊断码文案键的前缀。</summary>
    internal const string SimulatorIssueKeyPrefix = "sim.issue.";

    /// <summary>
    /// 模拟器失败诊断码（<c>HmiEditor.Simulator.SimulatorIssueCodes</c> 里那个
    /// <c>public const string</c> 的值）
    /// → 文案键（阶段 97 新增，登记 <b>#61d</b>）。这一族的产出方在 <c>HmiEditor.Simulator</c>
    /// 项目里（与 <c>DeviceChannelException</c> 同形：码定义在产出方所在项目内）。
    /// </summary>
    internal static string SimulatorIssueKey(string code) => SimulatorIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>配方克隆失败诊断码文案键的前缀。</summary>
    internal const string RecipeIssueKeyPrefix = "recipe.issue.";

    /// <summary>
    /// 配方克隆失败诊断码（<c>HmiEditor.Core.Services.RecipeIssueCodes</c> 里那些
    /// <c>const string</c> 的值）→ 文案键（阶段 98 新增，登记 <b>#61t</b>）。
    ///
    /// <para><b>与前十条同形，单一来源</b>：<see cref="Describe(RecipeCloneIssue)"/> 自己、
    /// <c>RecipeCloneIssueLocalizationGuardTests</c> 与
    /// <c>LocalizationKeyReachabilityGuardTests</c> 的 M16 都调它，不各写一份推导规则。</para>
    ///
    /// <para>⚠ 这一族与 <c>svg.issue.</c> / <c>csv.issue.</c> 同类：失败是**返回值**，
    /// 所以它走 <see cref="Describe(RecipeCloneIssue)"/>、**不走** <see cref="FailureReason"/>。</para>
    /// </summary>
    internal static string RecipeIssueKey(string code) => RecipeIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>CSV 变量导入问题诊断码文案键的前缀。</summary>
    internal const string CsvIssueKeyPrefix = "csv.issue.";

    /// <summary>
    /// CSV 变量导入问题诊断码（<c>HmiEditor.Core.Services.CsvImportIssueCodes</c> 里那些
    /// <c>const string</c> 的值）→ 文案键。
    ///
    /// <para>与前五条同形，**单一来源** —— <see cref="Describe(CsvImportIssue)"/> 自己、
    /// <c>CsvImportIssueLocalizationGuardTests</c> 与
    /// <c>LocalizationKeyReachabilityGuardTests</c> 的 M12 都调它，不各写一份推导规则
    /// （推导规则分家过的亏本项目吃过，见
    /// <c>docs/2026-09-18-plugin-text-and-lang-usage-guards.md</c>）。</para>
    ///
    /// <para><b>⚠ 这一族与 SVG 那条是同一个教训</b>（登记 <b>#61f</b>，阶段 88）：
    /// <see cref="CsvVariableImporter"/> 是**纯函数**、失败是**返回值**
    /// （<see cref="CsvImportIssue"/> 的列表），站点 catch 到的是 IO / UI 的异常
    /// ⇒ 只能走「**返回值带码**」，不能走带码异常。另一个具体动机：码化之前
    /// <c>ImportResult.Errors</c> 是裸中文串，而两个消费方**一个只取条数、一个整块丢弃**
    /// ⇒ 坏行静默丢失，用户看不到是哪几行。</para>
    /// </summary>
    internal static string CsvIssueKey(string code) => CsvIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>生成代码验证结果（消息）诊断码文案键的前缀。</summary>
    internal const string GenIssueKeyPrefix = "gen.issue.";

    /// <summary>
    /// 生成代码验证结果（消息）诊断码（<c>GeneratedCodeIssueCodes</c> 里那些
    /// <c>const string</c> 的值）→ 文案键。
    ///
    /// <para><b>与前六条同形，单一来源</b>：<see cref="Describe(GeneratedCodeFinding)"/> 自己、
    /// <c>GenFindingLocalizationGuardTests</c> 与可达性机制的取数段都调它，不各写一份
    /// （推导规则分家过的亏本项目吃过 —— 分家的表现正是「守卫绿着、界面却是中文」）。</para>
    ///
    /// <para>⚠ <b>有一个码刻意没有键</b>：<c>GENERATOR_WARNING_COMMENT</c> ——
    /// 那条消息的内容是**产物（<c>generated_ui.c</c>）里的注释原文**，翻译它等于改写产物
    /// ⇒ 走 <see cref="Resolve"/> 的回落分支。由判据的 <c>MessageNoKey</c> 表**双向**守住。</para>
    /// </summary>
    internal static string GenIssueKey(string code) => GenIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>finding 定位串文案键的前缀。</summary>
    internal const string GenLocationKeyPrefix = "gen.loc.";

    /// <summary>
    /// finding 定位码（<c>FindingLocationCodes</c> 里那些 <c>const string</c> 的值）→ 文案键。
    ///
    /// <para>只有 4 种**面向用户的定位文案**有码；另 4 种定位（<c>-</c> / 屏幕 id 列表 /
    /// <c>SecurityConfig.RuntimeUsers[…]</c> 这类**模型路径**）**与语言无关**、
    /// 刻意不建码 ⇒ <c>LocationCode == ""</c>，直出。那条分界由判据的 <c>NeutralLocation</c>
    /// 表**双向**守住（往这一档塞一条新中文文案 ⇒ 判据红）。</para>
    /// </summary>
    internal static string GenLocationKey(string code) => GenLocationKeyPrefix + code.ToLowerInvariant();

    /// <summary>表达式**语法**失败诊断码文案键的前缀。</summary>
    internal const string ExprSyntaxKeyPrefix = "expr.syntax.";

    /// <summary>
    /// 表达式语法失败诊断码（<c>HmiEditor.Core.Services.ExpressionSyntaxIssueCodes</c> 里那些
    /// <c>const string</c> 的值）→ 文案键（阶段 91 新增，登记 <b>#61i</b>）。
    ///
    /// <para><b>与前八条同形，单一来源</b>：<see cref="Describe(ExpressionSyntaxError)"/> 自己、
    /// <c>ExpressionSyntaxIssueLocalizationGuardTests</c> 与
    /// <c>LocalizationKeyReachabilityGuardTests</c> 的 M14 都调它，不各写一份推导规则。</para>
    ///
    /// <para><b>⚠ 这一族的键是「第二层」</b>：它们不出现在任何顶层消息里，而是作为
    /// <c>gen.issue.expression_syntax</c> 的实参 <c>{1}</c> 出现
    /// ⇒ <see cref="Describe(GeneratedCodeFinding)"/> 必须在**填参前**把它渲染掉，
    /// 见 <see cref="RenderNestedArgs"/>。这层嵌套不是新发明：
    /// <see cref="UiOperationGuard.FailureDetail(string, string, Exception)"/> 对异常做的就是
    /// 同一件事（先 <see cref="FailureReason"/>、再填参）。</para>
    /// </summary>
    internal static string ExprSyntaxKey(string code) => ExprSyntaxKeyPrefix + code.ToLowerInvariant();

    /// <summary>登录失败诊断码文案键的前缀。</summary>
    internal const string LoginIssueKeyPrefix = "login.issue.";

    /// <summary>
    /// 登录失败诊断码（<c>HmiEditor.Core.Services.LoginIssueCodes</c> 里那些
    /// <c>const string</c> 的值）→ 文案键（阶段 99 新增，登记 <b>#61v</b>）。
    ///
    /// <para><b>与前十条同形，单一来源</b>：<see cref="Describe(LoginIssue)"/> 自己、
    /// <c>LoginIssueLocalizationGuardTests</c> 与
    /// <c>LocalizationKeyReachabilityGuardTests</c> 的 M17 都调它，不各写一份推导规则
    /// （推导规则分家过的亏本项目吃过 —— 分家的表现正是「守卫绿着、界面却是中文」）。</para>
    /// </summary>
    internal static string LoginIssueKey(string code) => LoginIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>配方 CSV 导入问题诊断码文案键的前缀。</summary>
    internal const string RecipeImportIssueKeyPrefix = "recipe.import.issue.";

    /// <summary>
    /// 配方 CSV 导入问题诊断码（<c>HmiEditor.Core.Services.RecipeImportIssueCodes</c> 里那些
    /// <c>const string</c> 的值）→ 文案键（阶段 100 新增，登记 <b>#61f</b>）。
    ///
    /// <para><b>与前十一条同形，单一来源</b>：<see cref="Describe(RecipeImportIssue)"/> 自己、
    /// <c>RecipeImportIssueLocalizationGuardTests</c> 与
    /// <c>LocalizationKeyReachabilityGuardTests</c> 的 M18 都调它，不各写一份推导规则
    /// （推导规则分家过的亏本项目吃过 —— 分家的表现正是「守卫绿着、界面却是中文」）。</para>
    ///
    /// <para>⚠ 与 <c>recipe.issue.</c>（M16，配方**克隆**）**不是**一个族：那一族的码只有
    /// 2 个、描述"克隆"两个失败档；本族描述 **CSV 导入**。前缀分家 = 键不会串味，
    /// 一条前缀一张码表的不变量也保持住。</para>
    /// </summary>
    internal static string RecipeImportIssueKey(string code) => RecipeImportIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>模拟器编译 / 启动失败诊断码文案键的前缀。</summary>
    internal const string SimulatorBuildIssueKeyPrefix = "sim.build.issue.";

    /// <summary>
    /// 模拟器编译 / 启动失败诊断码（<see cref="SimulatorBuildIssueCodes"/> 里那些
    /// <c>const string</c> 的值）→ 文案键（阶段 103 新增，登记 <b>#61c</b> 更正后的重归位）。
    ///
    /// <para><b>与前十二条同形，单一来源</b>：<see cref="FailureReason"/> 的 ②‴ 支、
    /// <c>SimulatorBuildIssueLocalizationGuardTests</c> 与
    /// <c>LocalizationKeyReachabilityGuardTests</c> 的 M19 都调它，不各写一份推导规则
    /// （推导规则分家过的亏本项目吃过 —— 分家的表现正是「守卫绿着、界面却是中文」）。</para>
    ///
    /// <para>⚠ <b>这一族走 <see cref="FailureReason"/>（异常档），不走 <c>Describe</c></b> ——
    /// 与 <c>svg.issue.</c> / <c>csv.issue.</c> / <c>gen.issue.</c> / <c>expr.syntax.</c> /
    /// <c>recipe.issue.</c> / <c>recipe.import.issue.</c> / <c>login.issue.</c> 那些
    /// **返回值档**的族相反：本族产出方**真抛**（<see cref="SimulatorBuildException"/>），
    /// 是 <c>user.issue.</c> / <c>sim.issue.</c>（②′ / ②″）之后第三支走异常档的本仓库码族。</para>
    ///
    /// <para>⚠ 前缀**不是** <c>sim.issue.</c> 的兄弟：那一族（M15）是 <c>HmiEditor.Simulator</c> 的
    /// 「模拟器未连接」，描述**通讯**失败；本族描述**编译 / 启动**失败。
    /// 「一条前缀一张码表」的不变量要求它们分家（与 <c>recipe.issue.</c> 对
    /// <c>recipe.import.issue.</c> 同一条理由）。</para>
    /// </summary>
    internal static string SimulatorBuildIssueKey(string code) => SimulatorBuildIssueKeyPrefix + code.ToLowerInvariant();

    /// <summary>
    /// 查模板 → 填参数 → 查不到回落 <paramref name="fallback"/>。
    /// </summary>
    /// <param name="key">要查的键。</param>
    /// <param name="fallback">**产出方自带的中文回落原文** —— 字典缺键时原样返回它
    /// （宁可显示中文，也不能把键名显示给用户）。</param>
    /// <param name="args">模板占位符实参；<c>null</c> / 空 = 模板不含占位符。</param>
    internal static string Resolve(string key, string fallback, IReadOnlyList<object>? args)
    {
        var text = AppLocalization.Service[key];
        if (text == key) return fallback;

        return args is { Count: > 0 } a
            ? AppLocalization.Service.Format(key, a.ToArray())
            : text;
    }

    /// <summary>
    /// 把一条脚本校验信息（<c>Errors</c> / <c>Warnings</c> 共用）渲染成用户可读文本。
    ///
    /// <para><b>行号在这里加，不在 <see cref="ScriptError.Message"/> 里</b>：
    /// <see cref="ScriptError.Line"/> 已经是结构化字段，而 <see cref="ScriptError.Message"/>
    /// 是字典缺键时的回落 —— 行号若同时存在于两处，就必然有一处先过期
    /// （"第 N 行"的措辞改样式时尤其明显）。</para>
    /// </summary>
    internal static string Describe(ScriptError error)
    {
        var body = Resolve(ScriptIssueKey(error.Code), error.Message, error.Args);

        return error.Line > 0
            ? AppLocalization.Service.Format(Lang.ScriptLinePrefix, error.Line, body)
            : body;
    }

    /// <summary>
    /// 把一条 AI 输出校验结果渲染成用户可读文本。
    ///
    /// <para><b>它为什么在 App 层而不在 AI 层</b>：<c>HmiEditor.AI</c> 拿不到字典服务
    /// （依赖方向是 App → AI），所以 AI 层只产出「码 + 实参 + 中文回落」，
    /// 查字典这一步必须在 App 侧。三条消费点（聊天面板 <c>Detail</c>、生成对话框状态行、
    /// 属性面板建议列表）都调这里，不各写一份 —— 分家的表现是"某一条路径没接上字典"，
    /// 而那正是这个缺口本来的样子（英文界面下这三处全是中文）。</para>
    /// </summary>
    internal static string Describe(AiValidationIssue issue)
        => Resolve(AiIssueKey(issue.Code), issue.Message, issue.Args);

    /// <summary>同上，布局建议版（<c>PropertyPanelView</c> 的「AI 建议」列表用）。</summary>
    internal static string Describe(LayoutIssue issue)
        => Resolve(AiIssueKey(issue.Code), issue.Message, issue.Args);

    /// <summary>
    /// 把一条 **SVG 解析失败**渲染成用户可读文本 —— **返回值档**的渲染出口（登记 #61a，阶段 87）。
    ///
    /// <para><b>为什么它必须存在</b>：<c>SvgParser.Parse</c> 是纯函数、失败是**返回值**，
    /// 所以它走不到 <see cref="FailureReason"/>（那里收的是 <c>Exception</c>）。
    /// 这条是本族唯一的归一化点：两个 UI 站点（<c>Wiring.DockAggregate</c> /
    /// <c>AssetManagerView</c>）都调它，不各写一份查表。</para>
    ///
    /// <para><b>与 <see cref="Describe(ScriptError)"/> 同形</b>：码 → 键 → 模板 → 填
    /// <c>Args</c>，字典缺键才回落 <see cref="SvgParseError.Message"/>（中文）。
    /// 占用符个数与实参个数的对应由 <c>SvgParseIssueLocalizationGuardTests</c> 钉住 ——
    /// <c>LocalizationService.Format</c> 在两者不符时**不抛异常**，用户会看到字面的 <c>{0}</c>。</para>
    /// </summary>
    internal static string Describe(SvgParseError error)
        => Resolve(SvgIssueKey(error.Code), error.Message, error.Args);

    /// <summary>
    /// 配方克隆失败的渲染出口（阶段 98 新增，登记 <b>#61t</b>）。
    ///
    /// <para><b>与 <see cref="Describe(SvgParseError)"/> 同形</b>：<c>RecipeManager.CloneRecipe</c>
    /// 是纯函数式产出方、**失败是返回值**（<see cref="RecipeCloneResult"/>），
    /// 所以它走不到 <see cref="FailureReason"/>（那里收的是 <c>Exception</c>）。</para>
    ///
    /// <para><b>为什么必须有这条出口</b>：改动前那个站点在克隆失败时**什么都不显示** ——
    /// <c>CloneRecipe</c> 的返回类型声明成非空、内部却 <c>return null!</c>，
    /// 消费点写的是 <c>if (clone != null)</c> 而**没有 <c>else</c>**。本次把它接上。</para>
    /// </summary>
    internal static string Describe(RecipeCloneIssue issue)
        => Resolve(RecipeIssueKey(issue.Code), issue.Message, issue.Args);

    /// <summary>
    /// 把一条**配方 CSV 导入问题**渲染成用户可读文本 —— **返回值档**的渲染出口
    /// （登记 <b>#61f</b>，阶段 100）。
    ///
    /// <para><b>它为什么必须存在</b>：<see cref="RecipeManager.ImportCsv"/> 是产线服务、
    /// 失败是**返回值**（<see cref="RecipeImportResult.Issues"/>），走不到
    /// <see cref="FailureReason"/>（那里收的是 <c>Exception</c>）。本方法之前那条路
    /// **根本没有出口** —— 方法的签名只是个 <c>int</c>，坏行/坏值/入库被拒全被静默吞掉，
    /// 而站点 <c>RecipeEditorView.OnImportCsv</c> 用 <c>if (count &gt; 0)</c> 把它盖成
    /// 「成功导入 N 个配方」。</para>
    ///
    /// <para><b>行号在这里加，不在 <see cref="RecipeImportIssue.Message"/> 里</b>
    /// （与 <see cref="Describe(CsvImportIssue)"/> 同一条理由：两处都存必然有一处先过期）。
    /// 行号前缀**复用** <c>csv.line.prefix</c> —— 那张表就是 CSV 的行前缀，不分变量还是配方
    /// （同一族显示文案只许一处映射）。<c>Line == 0</c> = 文件级问题，不附前缀。</para>
    /// </summary>
    internal static string Describe(RecipeImportIssue issue)
    {
        var body = Resolve(RecipeImportIssueKey(issue.Code), issue.Message, issue.Args);

        return issue.Line > 0
            ? AppLocalization.Service.Format(Lang.CsvLinePrefix, issue.Line, body)
            : body;
    }

    /// <summary>
    /// 把一条 **CSV 变量导入问题**渲染成用户可读文本 —— **返回值档**的渲染出口
    /// （登记 <b>#61f</b>，阶段 88）。
    ///
    /// <para><b>它为什么必须存在</b>：<see cref="CsvVariableImporter.Parse"/> 是纯函数、
    /// 失败是**返回值**，所以它走不到 <see cref="FailureReason"/>（那里收的是
    /// <c>Exception</c>）。码化之前两个消费方都拿不到人可读的原因 ——
    /// <c>VariableImportView</c> 只显示 <c>Errors.Count</c>、<c>CommunicationConfigView</c>
    /// 连 <c>Errors</c> 都不读 ⇒ **坏行被静默丢掉**。本方法是本族唯一的归一化点，
    /// 两个消费点都调它，不各写一份查表。</para>
    ///
    /// <para><b>行号在这里加，不在 <see cref="CsvImportIssue.Message"/> 里</b>
    /// （与 <see cref="Describe(ScriptError)"/> 同一条理由）：<see cref="CsvImportIssue.Line"/>
    /// 已是结构化字段，而 <c>Message</c> 是字典缺键时的回落 —— 行号若同时存在于两处，
    /// 就必然有一处先过期。<c>Line == 0</c> = 文件级问题，不附前缀。</para>
    /// </summary>
    internal static string Describe(CsvImportIssue issue)
    {
        var body = Resolve(CsvIssueKey(issue.Code), issue.Message, issue.Args);

        return issue.Line > 0
            ? AppLocalization.Service.Format(Lang.CsvLinePrefix, issue.Line, body)
            : body;
    }

    /// <summary>
    /// 把一条**生成代码验证结果**的消息渲染成用户可读文本（登记 <b>#61g</b>，阶段 89）。
    ///
    /// <para><b>为什么它必须存在</b>：<c>GeneratedCodeValidator.Validate</c> 是纯函数、
    /// 失败是**返回值**（findings 列表），走不到 <see cref="FailureReason"/>。
    /// 这条是本族唯一的归一化点：<c>ValidationResultView</c> 的列表项与「复制全部」
    /// 都调它，不各写一份。</para>
    ///
    /// <para><b>与 <see cref="Describe(CsvImportIssue)"/> 同形</b>：码 → 键 → 模板 → 填
    /// <c>Args</c>，字典缺键才回落 <c>Message</c>（中文）。<c>GENERATOR_WARNING_COMMENT</c>
    /// **本来就没有键**（产物注释原文），走的就是这条回落 —— 那是有意的，不是缺翻译。</para>
    ///
    /// <para><b>⚠ 实参里有**记录体**时先渲染</b>（阶段 91 / #61i）：<c>EXPRESSION_SYNTAX</c> 的
    /// <c>Args[1]</c> 装的是 <see cref="ExpressionSyntaxError"/> —— Core 拿不到字典服务，
    /// 只能给「码 + 中文回落 + 实参」。这就是 <see cref="RenderNestedArgs"/> 的用途；
    /// 少了它，用户看到的是 <c>ExpressionSyntaxError { Code = …, Message = … }</c> 这种转储。</para>
    /// </summary>
    internal static string Describe(GeneratedCodeFinding finding)
        => Resolve(GenIssueKey(finding.Code), finding.Message, RenderNestedArgs(finding.Args));

    /// <summary>
    /// 把一条**表达式语法失败**渲染成用户可读文本 —— **返回值档**的渲染出口
    /// （登记 <b>#61i</b>，阶段 91）。
    ///
    /// <para><b>为什么它必须存在</b>：<c>ExpressionEngine.TryCompile</c> 是纯函数、失败是
    /// <c>out</c> 返回值，走不到 <see cref="FailureReason"/>（那里收的是 <c>Exception</c>）。
    /// 码化之前它抛的是裸 <c>FormatException</c>，消息只有中文；两个站点直接拿
    /// <c>ex.Message</c> 当文案用 —— <c>GeneratedCodeValidator</c> 塞进 finding 的 <c>{1}</c>、
    /// <c>PropertyPanelView</c> 直显在表达式输入框下方的红字里 ⇒ 英文界面下那一半照旧中文。</para>
    ///
    /// <para>⚠ 第三条路径（<c>CodeGenerator.Screen.cs</c> 写进产物 <c>generated_ui.c</c> 的
    /// <c>// WARNING:</c> 注释）**刻意不调它**：那句注释的读者是看 C 代码的人、不随界面语言变，
    /// 用的是 <see cref="ExpressionSyntaxError.Message"/> 的**中文回落原文** ——
    /// 与 <c>GeneratedCodeIssueCodes.GENERATOR_WARNING_COMMENT</c> 同一类"判过的不做"（登记 #61k）。</para>
    /// </summary>
    internal static string Describe(ExpressionSyntaxError error)
        => Resolve(ExprSyntaxKey(error.Code), error.Message, error.Args);

    /// <summary>
    /// 登录失败的渲染出口 —— **返回值档**（阶段 99 新增，登记 <b>#61v</b>）。
    ///
    /// <para><b>为什么它必须存在</b>：<c>AuthenticationService.Login</c> 是产线服务、
    /// **失败是返回值**（<see cref="LoginResult"/>），所以它走不到
    /// <see cref="FailureReason"/>（那里收的是 <c>Exception</c>）。这条是本族唯一的
    /// 归一化点：<c>LoginDialog</c> 调它，不自己拼文案。</para>
    ///
    /// <para><b>改动前那一档长什么样</b>：<c>Login</c> 返回 <c>string?</c>，
    /// 五种失败**都返回 <c>null</c>** ⇒ 站点只能写死一句
    /// <c>login.err</c>（「用户名或密码错误」）。用户被锁定了也不知道要等多久、
    /// 账户被禁用了也不知道该找谁。原因其实早就写进审计了，只是送不回界面。</para>
    ///
    /// <para>⚠ <b>「用户不存在」与「密码错误」刻意共用一个码</b>
    /// （<see cref="LoginIssueCodes.BAD_CREDENTIALS"/>）：分开等于把界面变成
    /// **用户名枚举**预言机，而改动前两者本来就显示同一句 ⇒ 分开属**范围外**的行为变更。</para>
    /// </summary>
    internal static string Describe(LoginIssue issue)
        => Resolve(LoginIssueKey(issue.Code), issue.Message, issue.Args);

    /// <summary>
    /// 填参前把实参里的**记录体**先渲染掉。
    ///
    /// <para><b>为什么需要</b>：<c>gen.issue.expression_syntax</c> 的 <c>{1}</c> 装的是
    /// <see cref="ExpressionSyntaxError"/>（Core 侧没有字典服务，只能给码 + 实参 + 中文回落）
    /// ⇒ 直接丢给 <c>Format</c> 会把记录体的 <c>ToString()</c> 转储打到界面上。
    /// 与 <see cref="UiOperationGuard.FailureDetail(string, string, Exception)"/> 先
    /// <see cref="FailureReason"/> 再填参是同一条做法。</para>
    ///
    /// <para><b>刻意是白名单式的类型分派</b>，别改成"凡是对象都 <c>ToString()</c>" ——
    /// 那会把将来新增的、忘了渲染的记录体也悄悄放过去：现在的表现（转储）一眼看得出坏，
    /// 改完的表现是看不懂但看不出坏。</para>
    /// </summary>
    private static IReadOnlyList<object>? RenderNestedArgs(IReadOnlyList<object>? args)
    {
        if (args is not { Count: > 0 }) return args;

        object[]? copy = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] is not ExpressionSyntaxError expr) continue;
            if (copy is null)
            {
                copy = new object[args.Count];
                for (var j = 0; j < args.Count; j++) copy[j] = args[j];
            }
            copy[i] = Describe(expr);
        }
        return copy ?? args;
    }

    /// <summary>
    /// 把一条 finding 的**定位串**渲染成用户可读文本。
    ///
    /// <para><b><c>LocationCode</c> 为空 ⇒ 直出</b>：那表示该定位**与语言无关**
    /// （<c>-</c> / 屏幕 id 列表 / <c>SecurityConfig.RuntimeUsers[…]</c> 这类模型路径）。
    /// 走字典反而会让界面语言影响一个"指向代码里某个位置"的坐标。</para>
    /// </summary>
    internal static string DescribeLocation(GeneratedCodeFinding finding)
        => string.IsNullOrEmpty(finding.LocationCode)
            ? finding.Location
            : Resolve(GenLocationKey(finding.LocationCode), finding.Location, finding.LocationArgs);

    /// <summary>
    /// 把一条**操作失败**的异常渲染成用户可读的「原因」那一半。
    ///
    /// <para><b>它挡的是什么</b>：此前各处把 <c>ex.Message</c> 直接交给
    /// <see cref="UiOperationGuard.FailureDetail(string, string)"/> ⇒ 凡是 Core / BCL
    /// 抛的异常，用户看到的就是 .NET 的**原始英文消息**。最刺眼的两档是工程包解包：
    /// ① 非 ZIP ⇒ <c>End of Central Directory record could not be found.</c>；
    /// ⑤ <c>manifest.json</c> 坏 JSON ⇒ <c>System.Text.Json</c> 的内部措辞连
    /// <c>LineNumber</c> / <c>BytePositionInLine</c> 一起抖进状态栏（阶段 80 探针实测，登记 #59）。
    /// 规约是「产出方给码 + 实参 + 中文回落，App 层查字典」。</para>
    ///
    /// <para><b>未进码表的异常仍然回落 <c>ex.Message</c></b>：这是**有意的诚实边界**，不是遗漏 ——
    /// 宁可显示原文，也比统统换成一句"操作失败"（丢掉全部信息）更可用。边界清单见
    /// <c>HmiPackageIssueCodes</c> 与 <see cref="SystemFailureCodes"/> 的类注释。</para>
    ///
    /// <para><b>分支与各支的判据</b>（阶段 97 起**不止四支**：带码异常一支对应一个产出方族）：
    /// ① <c>HmiPackageException</c> 走**产出方给的码**（Core 声明、随异常携带）；
    /// ② <see cref="SvgParseException"/> 同上 —— 这条是**返回值档**的产出方借异常把码送上来
    /// （AI 侧包出来的，登记 <b>#61a</b>，阶段 87）；
    /// ②′ <see cref="HmiEditor.Core.Services.UserAdminException"/>（用户管理两档：
    /// 角色未注册 / 密码长度不足，登记 <b>#61d</b>，阶段 97）；
    /// ②″ <see cref="HmiEditor.Simulator.SimulatorNotConnectedException"/>（模拟器未连接，同上）；
    /// ③ 其余异常走 <see cref="SystemFailureCodes.CodeFor"/> ——
    /// 按**异常类型**取码（BCL 没有产出方，表只能维护在 App 侧；登记 <b>#61e</b>，阶段 85）；
    /// ④ 以上都取不到码的回落 <c>ex.Message</c>（就是上一段那条诚实边界）。
    /// ⚠ <b>第三支刻意不带实参</b>：码化后的短句里**没有目标文件**（<c>Resolve(键, ex.Message, null)</c>）
    /// —— "是哪个文件"必须由调用方走
    /// <see cref="UiOperationGuard.FailureDetail(string, string, Exception)"/> 的三实参模板补回来。
    /// 这一步**阶段 86 已在 C 类 8 处落地**（登记 <b>#61b</b> 已闭环）；第 9 处原本是
    /// <c>asset.import.fail</c> 的**批量档**，当时按「一次导入多个文件 ⇒ 那个文件结构上不存在」
    /// 归了 <b>#61a</b> —— <b>阶段 101 实测那句话不成立</b>（<c>path</c> 就在 <c>foreach</c> 里，
    /// 承接变量一提就拿到），已与 <c>status.svg.apply.failed</c> 一并迁到三实参出口。</para>
    ///
    /// <para><b>⚠ 别把「把 <c>ex.Message</c> 换成 <c>FailureReason(ex)</c>」当成迁移</b>
    /// （阶段 83 定性、阶段 85 复核、阶段 86 按此迁完 C 类，登记 <b>#61</b>）：本仓库另有
    /// <c>Format(某键, ex.Message)</c> 的直通站点，它们**清一色**是 <c>catch (Exception ex)</c>
    /// **宽捕获**（原 37 处里 36 处如此；唯一例外是 <c>UserManagerView</c> 的
    /// <c>ArgumentException</c>）—— 阶段 86 迁走 C 类 8 处、阶段 87 迁走 <c>ai.gen.failed</c> 8 处后
    /// 剩 <b>21 处</b>（阶段 87 实测；那 8 处**确实**是真迁移，理由见下面第三段）。
    /// 阶段 83 写的"换过去用户所见一字不变"当时成立（本方法只对 <c>HmiPackageException</c> 分叉）；
    /// <b>阶段 85 落地 #61e 之后这句话要加限定 —— 对 BCL 类型会变，但裸换不是净收益</b>：
    /// <c>FailureReason(ex)</c> 拿不到路径实参 ⇒ "英文 + 有路径"换成"中文 + 没路径"，
    /// 而站点面守卫会**绿着变空**（站点形状没变、只是实参换了）—— 仍是**改名式假闭环**。
    /// ⇒ 那些站点的正确动作是走**带目标路径的三实参重载**，让用户**同时**读到
    /// 「哪个文件」+「为什么（他的语言）」—— 阶段 86 对 C 类做的就是这件事。
    /// ⚠ 但站点面**分不出**"带了路径"与"三实参里填了 <c>null</c>"（后者用户一样看不到文件）
    /// ⇒ 那件事有**两条**判据盯着（第二条是阶段 101 补的）：
    /// <c>UiOperationGuardContractTests.EveryDetailCallSite_PassesAConcreteTargetPath</c>
    /// 管**字面量** <c>null</c> / <c>default</c>（形状可判）；
    /// <c>…EveryConveyedPathVariable_IsGivenARealValue</c> 管**承接变量**档 ——
    /// 那种写法**站点面完全看不见**，阶段 101 的真仓库注入实测过（把 <c>currentPath = path;</c>
    /// 改成 <c>= null;</c> 时前一条判据**全绿 0 红**）。⚠ 本段此前只写了前一条判据，
    /// 那句话说**过头**了。</para>
    ///
    /// <para><b>✅ 判「裸换算不算收口」的标准是「产出方有没有码」，不是「面有没有变小」</b>
    /// （阶段 87 实测，登记 #61a）：<c>ai.gen.failed</c> 的 8 处阶段 87 就是**直接**换成本方法的
    /// —— 因为**同一阶段**给产出方（AI 侧）加了码（<see cref="SvgParseException"/>，第 ② 支）。
    /// 反过来说：**产出方没给码时换它才是改名**。⇒ 两份读数要一起看才有意义：
    /// 「面变小」×「产出方新增了码」= 真迁移；只有前者 = 假闭环。</para>
    ///
    /// <para><b>⚠ 剩下的诚实边界</b>（不许当成"这条路已经清完"）：宽捕获里凡**没码**的异常
    /// 仍走第 ④ 支回落原文。本仓库 A 类的产出方是**纯函数**（<c>SvgParser</c> /
    /// <c>GeneratedCodeValidator</c> / <c>CsvVariableImporter</c> / <c>RecipeManager</c>）——
    /// 它们把失败做成**返回值**、根本不抛异常，所以「加码表 + 带码异常」这个模板在它们的站点上
    /// **无处安放**（站点 catch 到的是 IO / UI 的异常），得走「**返回值带码**」那条
    /// （<see cref="Describe(SvgParseError)"/> 就是它的渲染出口）。逐站实测表与三条支线的重排见
    /// <c>docs/2026-09-23-exception-passthrough-triage.md</c> §87。</para>
    /// </summary>
    internal static string FailureReason(Exception ex)
    {
        if (ex is HmiPackageException package)
            return Resolve(PackageIssueKey(package.Code), package.Message, package.Args);

        // ② SVG 解析失败（登记 #61a 第一刀，阶段 87）：AI 侧把「模型生成的 SVG 不可用」
        //    包成 SvgParseException 抛出。改动前那里抛的是裸 InvalidOperationException +
        //    中文串 ⇒ 本方法接不住它、只能落 ④ 渲染中文原文（英文界面下中英混排）。
        if (ex is SvgParseException svgParse)
            return Describe(svgParse.Error);

        // ②′ 用户管理（登记 #61d，阶段 97）：AuthenticationService 自建的两档失败
        //     （角色未注册 / 密码长度不足）原先抛**裸 ArgumentException + 中文原文** ⇒
        //     英文界面下那一半照旧中文，中文界面下则是**作者口吻**
        //     （「请先 RegisterRole 或修正 SecurityConfig.DefaultRoleId」）。
        //     ⚠ 该类的 ThrowIfNullOrWhiteSpace 两档**保持 ArgumentException 不动** ——
        //     那是调用方编程错误，与 SystemFailureCodes 刻意不码化 ArgumentException 同一条边界。
        if (ex is UserAdminException userAdmin)
            return Resolve(UserIssueKey(userAdmin.Issue.Code), userAdmin.Issue.Message, userAdmin.Issue.Args);

        // ②″ 模拟器未连接（登记 #61d，阶段 97）：CommunicationSimulator 原先抛
        //     new InvalidOperationException("Simulator not connected") —— 英文硬编码、无码，
        //     SystemFailureCodes 的表里也没有它（那张表是**实测过才加**的）。
        if (ex is SimulatorNotConnectedException simNotConnected)
            return Resolve(SimulatorIssueKey(simNotConnected.Code), simNotConnected.Message, null);

        // ②‴ 模拟器编译 / 启动（登记 #61c 更正后重归 A 类异常档；阶段 103）：产出方
        //     SimulatorBuildService 的两条前置校验 + BuildRunDialog 的进程启动原先抛
        //     new InvalidOperationException("<中文>")，而 InvalidOperationException **没有码**
        //     —— SystemFailureCodes 那张表是探针**实测过才加**的（阶段 85），里面没有它
        //     ⇒ 只能落 ④ 渲染中文原文（英文界面下中英混排；中文界面下则是**作者口吻**的
        //     「请安装 CMake 后重试（Linux: sudo apt install cmake）」）。
        //     ⚠ 本条与 ②″ 的区别是**码的来源**：②″ 的码定义在产出方所在项目
        //     （HmiEditor.Simulator），本条的产出方**就在余下这一支所在的程序集里**。
        if (ex is SimulatorBuildException simBuild)
            return Resolve(SimulatorBuildIssueKey(simBuild.Code), simBuild.Message, simBuild.Args);

        if (SystemFailureCodes.CodeFor(ex) is { } code)
            return Resolve(SystemIssueKey(code), ex.Message, null);

        return ex.Message;
    }
}
