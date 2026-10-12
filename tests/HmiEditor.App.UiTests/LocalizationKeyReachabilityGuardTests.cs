using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Services;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 本地化键的**可达性**守卫（登记表 §4 #35 的字典侧）。
///
/// <para><b>它补的是哪半边</b>：P0-5 那个缺陷有两半 ——
/// 常量没人读（<c>LangConstantUsageGuardTests</c> 锁住了，只减不增）**和**
/// 字典键没人查（此前**没有任何守卫**）。<c>LangKeyCoverageTests</c> 查的是
/// 常量→字典（正向），<c>PluginTextLocalizationGuardTests</c> 查派生键→字典（正向），
/// 谁也不查"**字典里这个键到底能不能被任何机制取到**"。于是
/// <c>param.baud.rate</c> 这种"键是死的"条目可以一直躺在两本字典里
/// （P0-5 那对缺陷里，常量名与字典键都被按陈旧的名字写，两边都没人读）。</para>
///
/// <para><b>⚠ 这条历史（#48）已闭环，但本文件仍要干净</b>：阶段 63 起
/// <c>LangConstantUsageGuardTests</c> 只认**代码引用**（剥注释 + 抹字面量内容）
/// ⇒ 在本类注释里写常量名**不再**会把它的基线压低（此前会：实测 1267 → 1266）。
/// 阶段 65 起 M6 的语料也**剥注释**，所以本类的**注释**同样不算了；
/// **但本文件仍必须从 M6 的语料里排除** —— 理由变窄也更精确了：本类的**代码**里就写着
/// 真实键名字符串（<c>mustBeUnreachable</c> 那 10 个"已清理的陈旧键"、
/// <see cref="KeptUnreachable"/> 的 5 条），M6 会把它们算成"有人引用"。
/// **判据自己的清单不许成为判据的证据** —— 这是同一个坑的第三种走法。</para>
///
/// <para><b>M1 的收紧（2026-09-21，阶段 64，闭环登记 #49）</b>：M1 原先直接枚举 <c>Lang</c>
/// 常量**值**，等于「只要还有人**定义**着那条常量，它的键就算可达」——
/// **不问常量本身有没有代码引用**。后果是**死常量给死键做担保**：阶段 62 那句
/// 「字典侧已闭环」实际是**模掉「常量存在」**的闭环。specimen
/// <c>project.settings.category.editor</c>（该键已在阶段 64 随 143 个不可达键一起删除）：
/// 分类清单恰好 6 项、**独缺它**，
/// 而同族另外 6 个常量**就在那 6 行里被代码引用**；该键全仓唯一其他出现处是
/// **补键工具自己的映射表**（不是消费者）。⇒ 现在 M1 = **有代码引用**的常量值，
/// 口径**直接复用** <see cref="LangConstantUsageGuardTests.CodeReferencedConstants"/>
/// （同源，不抄第二份）。</para>
///
/// <para><b>三处收紧的后果与处置（2026-09-21，阶段 64 + 65）</b>：M1 只认代码引用 +
/// M6 排除定义面，把 <b>151</b> 个字典键暴露成不可达 —— 在此之前它们全部靠
/// 「常量存在」或「<c>Lang.cs</c> 里写着键名」自我满足。处置：
/// <b>143 删</b>（两本字典 + 对应 Lang 常量 + 补键工具映射表条目；全仓除定义面外
/// 只有 21 处命中，且全在 <c>docs/</c> 与补键表里，没有一处是消费者）、
/// <b>8 保留</b>（见 <see cref="KeptUnreachable"/>，每条带理由）。
/// 字典 2275 → 2132 键，Lang 常量 1915 → 1772（连带 <c>UnusedBaseline</c> 1272 → 1129）。
/// <b>阶段 65 再收 M6</b>：语料**剥注释**（只剥注释 —— M6 看的就是字面量，
/// 抹掉字面量等于把这条机制删掉），又暴露出 <b>1</b> 个键 <c>param.baud.rate</c>：
/// 它的全部出现点只有两条 XML 文档注释，其"可达"**完全由注释提供**。
/// 它正是阶段 62 记下的「陈旧键（<c>param.baud.rate</c> 等 5 个）」里的第 5 个
/// （另外 4 个早就在差异表里）⇒ 按既定「只增不删」登记，差异表 8 → <b>9</b> 条，
/// M6 面 388 → <b>387</b>，其余六条面一个数没动（<c>M6Face_WithoutComments_IsReported</c>
/// 留着两条口径的读数：改一次口径的收益永远是**可复跑的数字**）。</para>
///
/// <para><b>判据</b>：字典键必须落在**全部可达机制的并集**里，或者进
/// <see cref="KeptUnreachable"/>（差异表，每条带理由）。两侧都做相等判定 ⇒
/// 白名单里的键一旦变得可达、或从字典里消失，本判据也红（防过期抄写）。</para>
///
/// <para><b>机制清单</b>（面各自自检非空，见 <see cref="MechanismFaces_AreNonEmpty"/>）：</para>
/// <list type="number">
///   <item><b>M1</b> <c>Lang</c> 常量值（反射），**只认有代码引用的常量** ——
///     代码里 <c>L(Lang.X)</c> 走这条；</item>
///   <item><b>M2</b> <c>.axaml</c> 里的 <c>{res:Loc K}</c>（<see cref="AppSourceScanner"/>）；</item>
///   <item><b>M3</b> 插件族派生 —— **用真实插件对象**枚举驱动名/描述/配置字段标签/硬件校验码
///     （复用 <c>PluginTextLocalizationGuardTests.DerivedKeys</c>，不抄一份推导规则）；</item>
///   <item><b>M4</b> <c>ConfigFieldInventory.NotWired[].NoteKey</c>（<c>cfg.note.类型.字段</c>）；</item>
///   <item><b>M5</b> 脚本诊断码 —— <c>*Code</c> 常量（反射）拼接 + Core 源码里的
///     <c>Code = "…"</c> 字面量，统一走 <c>script.issue.&lt;code 小写&gt;</c>；</item>
///   <item><b>M8</b> AI 诊断码 —— <c>HmiEditor.AI.AiIssueCodes</c> 的 <c>public const string</c>
///     （反射；**复用** <c>AiIssueLocalizationGuardTests.DeclaredCodes()</c>，不另抄一份取数方式），
///     统一走 <c>ai.issue.&lt;码小写&gt;</c>。AI 面板的聊天详情、生成对话框的状态行、
///     属性面板的「AI 建议」列表三处文案全在这条上 —— 它们是 <c>src/HmiEditor.AI</c>
///     里的字面量，<c>HardcodedLocalizationGuardTests</c> 的扫描面（<c>src/HmiEditor.App</c>）
///     看不见，所以必须有这条机制；</item>
///   <item><b>M9</b> 工程包诊断码（阶段 81）——
///     <c>HmiEditor.Core.Serialization.HmiPackageIssueCodes</c> 的 <c>public const string</c>
///     （反射；**复用** <c>PackageIssueLocalizationGuardTests.DeclaredCodes()</c>，不另抄取数方式），
///     统一走 <c>package.issue.&lt;码小写&gt;</c>。这几条键的值由 <c>ExternalText.FailureReason</c>
///     查、模板里**带占位符**（<c>{0}</c>/<c>{1}</c>）⇒ M6 的"整键字面量"扫描原理上看不见它们，
///     所以不能靠 M6 兜底；</item>
///   <item><b>M10</b> 系统层失败诊断码（阶段 85，登记 <b>#61e</b>）——
///     <see cref="SystemFailureCodes"/> 的 <c>internal const string</c>
///     （反射；**复用** <c>SystemIssueLocalizationGuardTests.DeclaredCodes()</c>，不另抄取数方式），
///     统一走 <c>system.issue.&lt;码小写&gt;</c>。与 M8 / M9 同一条理由：这些键的值由
///     <c>ExternalText.FailureReason</c> 查、**源码里根本不出现整键字面量** ⇒ M6 原理上看不见，
///     不能靠它兜底。⚠ 与 M8/M9 的一处差别：那两个的码由 <c>public</c> 类型声明，
///     这条是 <c>internal</c> ⇒ 反射式取数**必须带 <c>BindingFlags.NonPublic</c>**，
///     否则取到 0 个码、本机制静默退化成空集（<c>MechanismFaces_AreNonEmpty</c> 的下界就是防它）；</item>
///   <item><b>M11</b> SVG 解析失败诊断码（阶段 87，登记 <b>#61a</b>）——
///     <c>HmiEditor.Core.Services.SvgParseIssueCodes</c> 的 <c>public const string</c>
///     （反射；**复用** <c>SvgParseIssueLocalizationGuardTests.DeclaredCodes()</c>，不另抄取数方式），
///     统一走 <c>svg.issue.&lt;码小写&gt;</c>。与 M8 / M9 / M10 同一条理由：这些键的值由
///     <c>ExternalText.Describe(SvgParseError)</c> / <c>FailureReason</c> 查、
///     **源码里根本不出现整键字面量** ⇒ M6 原理上看不见，不能靠它兜底。
///     ⚠ 这一族有**两条搬运方式**（返回值档：<c>SvgParser.Parse</c> 直接返回对象；
///     异常档：AI 侧包成 <c>SvgParseException</c> 抛出），但机制面按「码 → 键」算 ——
///     键**只有一套**，不关心它怎么被搬运；</item>
///   <item><b>M12</b> CSV 变量导入问题诊断码（阶段 88，登记 <b>#61f</b>）——
///     <c>HmiEditor.Core.Services.CsvImportIssueCodes</c> 的 <c>public const string</c>
///     （反射；**复用** <c>CsvImportIssueLocalizationGuardTests.DeclaredCodes()</c>，不另抄取数方式），
///     统一走 <c>csv.issue.&lt;码小写&gt;</c>。与 M8 / M9 / M10 / M11 同一条理由：这些键的值由
///     <c>ExternalText.Describe(CsvImportIssue)</c> 查、
///     **源码里根本不出现整键字面量** ⇒ M6 原理上看不见，不能靠它兜底。
///     ⚠ 它也是**返回值档**（产出方是纯函数、失败是 <c>ImportResult.Errors</c>）——
///     与 M11 同一个教训：站点 catch 到的是 IO，「带码异常」在它的站点上无处安放；</item>
///   <item><b>M13</b> 生成代码验证消息/定位诊断码（阶段 89，登记 <b>#61g</b>）——
///     HmiEditor.Core.Services.GeneratedCodeIssueCodes 与 FindingLocationCodes
///     的 public const string（反射；**复用**
///     GenFindingLocalizationGuardTests.DeclaredCodes() / DeclaredLocationCodes()，
///     不另抄取数方式），键统一走 gen.issue.码小写 与 gen.loc.码小写。
///     与 M8~M12 同一条理由：这些键的值由 ExternalText.Describe(GeneratedCodeFinding) /
///     DescribeLocation 查、**源码里根本不出现整键字面量** ⇒ M6 原理上看不见。
///     ⚠ 这一族有**两个键族**（消息 18 码 + 定位 5 码），且 GENERATOR_WARNING_COMMENT
///     **刻意没有键**（消息体是产物 generated_ui.c 里 // WARNING: 注释的原文，
///     翻译它等于改写产物，登记 #61k）⇒ 取数时按 MessageNoKey 摘掉它；</item>
///   <item><b>M14</b> 表达式语法失败诊断码（阶段 91，登记 <b>#61i</b>）——
///     <c>HmiEditor.Core.Services.ExpressionSyntaxIssueCodes</c> 的 <c>public const string</c>
///     （反射；**复用** <c>ExpressionSyntaxIssueLocalizationGuardTests.DeclaredCodes()</c>，
///     不另抄取数方式），统一走 <c>expr.syntax.&lt;码小写&gt;</c>。
///     与 M8~M13 同一条理由：这些键的值由 <c>ExternalText.Describe(ExpressionSyntaxError)</c> 查、
///     **源码里根本不出现整键字面量** ⇒ M6 原理上看不见。
///     ⚠ 这一族还有个**第二层**的搬运形状：它同时是 <c>gen.issue.expression_syntax</c> 的
///     实参 <c>{1}</c>（嵌套渲染）—— 机制面仍按「码 → 键」算，键只有一套；</item>
///   <item><b>M15</b> 用户管理 / 模拟器失败诊断码（阶段 97，登记 <b>#61d</b>）——
///     <c>HmiEditor.Core.Services.UserAdminIssueCodes</c> 与
///     <c>HmiEditor.Simulator.SimulatorIssueCodes</c> 的 <c>public const string</c>
///     （反射；**复用** <c>UserAdminIssueLocalizationGuardTests.DeclaredCodes()</c> /
///     <c>DeclaredSimulatorCodes()</c>，不另抄取数方式），
///     键统一走 <c>user.issue.&lt;码小写&gt;</c> 与 <c>sim.issue.&lt;码小写&gt;</c>。
///     与 M8~M14 同一条理由：这些键的值由 <c>ExternalText.FailureReason</c> 的 ②′/②″ 两支查、
///     **源码里根本不出现整键字面量** ⇒ M6 原理上看不见。
///     ⚠ 这一族有**两个键前缀**、两个码表还分居两个项目（用户管理在 Core、
///     模拟器在 Simulator —— 与 <c>DeviceChannelException</c> 同形：码定义在产出方所在项目内），
///     所以是**两个**取数出口合成**一个** kind；</item>
///   <item><b>M16</b> 配方克隆失败诊断码（阶段 98，登记 <b>#61t</b>）——
///     <c>HmiEditor.Core.Services.RecipeIssueCodes</c> 的 <c>public const string</c>
///     （反射；**复用** <c>RecipeCloneIssueLocalizationGuardTests.DeclaredCodes()</c>，
///     不另抄取数方式），统一走 <c>recipe.issue.&lt;码小写&gt;</c>。
///     与 M8~M15 同一条理由：这些键的值由 <c>ExternalText.Describe(RecipeCloneIssue)</c> 查、
///     **源码里根本不出现整键字面量** ⇒ M6 原理上看不见。
///     ⚠ 这一族是**返回值档**（<c>RecipeManager.CloneRecipe</c> 返回
///     <c>RecipeCloneResult</c>、**不抛异常**）⇒ 它走 <c>Describe</c> 而不走
///     <c>FailureReason</c>，与 M11/M12/M14 同一条边界。
///     ⚠ <c>REJECTED</c> 那个码**没有"走真实产出路径"的见证**（Id 撞车是概率事件），
///     在守卫里进 <c>NotProducible</c> 并写明理由 —— 但**它的键照样算进本机制**：
///     本机制的语义是「**码 → 键** 的推导链通不通」，不是"今天用户撞不撞得到"。
///     ⚠ 它同时是本项目「**加键不加 <c>Lang</c> 常量**」的**第二例**（第一例是 M15）——
///     前缀拼出来的键老机制看不见，所以新机制是**必须**的，不能靠 M6 兜底；</item>
///   <item><b>M17</b> 登录失败诊断码（阶段 99，登记 <b>#61v</b>）——
///     <c>HmiEditor.Core.Services.LoginIssueCodes</c> 的 <c>public const string</c>
///     （反射；**复用** <c>LoginIssueLocalizationGuardTests.DeclaredCodes()</c>，
///     不另抄取数方式），统一走 <c>login.issue.&lt;码小写&gt;</c>。
///     与 M8~M16 同一条理由：这些键的值由 <c>ExternalText.Describe(LoginIssue)</c> 查、
///     **源码里根本不出现整键字面量** ⇒ M6 原理上看不见。
///     ⚠ 这一族是**返回值档**（<c>AuthenticationService.Login</c> 返回 <c>LoginResult</c>、
///     **不抛异常**）⇒ 走 <c>Describe</c> 而不走 <c>FailureReason</c>，与 M11/M12/M14/M16 同一条边界。
///     ⚠ 它同时是本项目「**加键不加 <c>Lang</c> 常量**」的**第三例**（前两例是 M15 / M16）。
///     ⚠ 与 M15/M16 不同的是它**同时退场了一条旧键**：五档失败原先一律显示同一句
///     「用户名或密码错误」，而锁定 / 禁用 / 过期的处置完全不同（等一会儿 / 找管理员 / 重输）
///     ⇒ <c>login.err</c> 与 <c>Lang.LoginErr</c> 一起退场（见 LangConstantUsageGuardTests）。</item>
///   <item><b>M18</b> 配方 CSV 导入问题诊断码（阶段 100，登记 <b>#61f</b>）——
///     <c>HmiEditor.Core.Services.RecipeImportIssueCodes</c> 的 <c>public const string</c>
///     （反射；**复用** <c>RecipeImportIssueLocalizationGuardTests.DeclaredCodes()</c>，
///     不另抄取数方式），统一走 <c>recipe.import.issue.&lt;码小写&gt;</c>。
///     与 M8~M17 同一条理由：这些键的值由
///     <c>ExternalText.Describe(RecipeImportIssue)</c> 查、**源码里根本不出现整键字面量**
///     ⇒ M6 原理上看不见。
///     ⚠ 本族与 M12（<c>csv.issue.</c>）是**同一登记项下的两刀**（#61f），但**两族键不共用**：
///     那一族的码点名 Name/DataType/Address 三列、指变量名；本族描述配方 CSV（列序固定、
///     值必须是数字）。按产出方分家是本仓库既有约定。
///     ⚠ 这一族的 <c>RECIPE_NOT_APPLIED</c> 是**概率档**（<c>Guid[..8]</c> 撞车），
///     在守卫里进 <c>NotProducible</c> —— 但**它的键照样算进本机制**（同 M16 的 <c>REJECTED</c>）：
///     本机制量的是「**码 → 键**」的推导链通不通，不是"今天用户撞不撞得到"。</item>
///   <item><b>M19</b> 模拟器编译 / 启动失败诊断码（阶段 103，登记 <b>#61c</b>）——
///     <c>HmiEditor.App.Services.SimulatorBuildIssueCodes</c> 的 <c>const string</c>
///     （反射；**复用** <c>SimulatorBuildIssueLocalizationGuardTests.DeclaredCodes()</c>），
///     统一走 <c>sim.build.issue.&lt;码小写&gt;</c>。这是**唯一**一条「码表与产出方同在
///     <c>HmiEditor.App</c>」的族（其余各族的码都在 Core / 插件 / <c>HmiEditor.AI</c>）。
///     ⚠ 与 M15 的 <c>sim.issue.</c>（模拟器**未连接**）**不是**一族：一条前缀一张码表。</item>
///   <item><b>M20</b> 通讯族校验码（阶段 104，登记 <b>#61x</b>）—— 码住在**插件程序集**
///     <c>Hmi.Communication.Builtin</c> 的四个设计期驱动里，App 侧没有码表可反射
///     ⇒ 取数方式是 <c>PluginTextLocalizationGuardTests.TriggeredCommIssueCodes()</c>，
///     它**真触发**一遍校验规则（含 <c>Args</c>）再收码，统一走
///     <c>comm.issue.&lt;码小写&gt;</c>（推导在 <c>PluginTextKeys.CommIssueKey</c>）。
///     ⚠ 与 M3（硬件族 <c>hw.issue.</c>）**同形但不同族**：两个契约程序集各有一份
///     <c>ValidationIssue</c>（连 <c>ValidationSeverity</c> 都有两份）。</item>
///   <item><b>M7</b> 枚举名派生 —— 视图里 <c>$"reslib.type.{t.ToString().ToLowerInvariant()}"</c>
///     这类**模板拼接**；</item>
///   <item><b>M6</b> 源码里的**整键字面量**（<c>src/</c>+<c>tests/</c> 的 <c>.cs</c>/<c>.axaml</c>，
///     语料**剥注释**、排除 <c>Lang.cs</c> 与本类自身 —— 两条都是"自我满足"的入口）。
///     <b>这条是代理，排在最后</b>：拼接出来的键（如上面 M7/M8 那两类）它原理上看不见，
///     所以 M6 只用来**减少误报**，判据对它的依赖写在这里，别当成精确机制。
///     ⚠ **只剥注释，不抹字面量**：M6 看的就是字面量，抹掉内容等于删掉这条机制
///     —— 掩码按机制选（常量侧看"代码引用"，所以它抹字面量）。
///     <b>口径已拍板（阶段 262，登记 `#61s`）</b>：判定口径**保持「认所有非注释字面量」**；
///     「只认可执行语句里的字面量」那条收窄口径只作**读数 + 负控**
///     （<c>M6Face_JudgedCorpusVersusNarrowedCorpus_IsReported</c>）——
///     因为收窄会让"只在断言消息里被点名的键"整批变成不可达，那是**产品决策**，不是判据能自作主张的。</item>
/// </list>
///
/// <para><b>这张清单从阶段 106 起有判据盯着</b>（<see cref="Mechanisms_AreDeclaredInAllThreePlaces"/>）。
/// 在那之前「加机制要同时改**下界表**与**这份清单**」只是一句纪律：阶段 103 的 <b>M19</b>
/// 只进了下界表、没进清单，到阶段 104 才手工补上 —— 而且那次补**没有任何东西会响**。
/// 现在清单、下界表、取数三处的集合双向相等，任一处漏写都点名到具体机制。</para>
/// </summary>
public class LocalizationKeyReachabilityGuardTests
{
    /// <summary>
    /// 已判定为「**保留**」的不可达键 —— 差异表，不是穷举白名单。
    ///
    /// <para>每条都必须给理由；没理由的条目不许进（进表等于把缺陷合法化）。
    /// 表内条目若**变得可达**（有人给它接了消费点）或**从字典里消失**，判据会红，
    /// 逼维护者删掉这一条 —— 否则它会退化成过期抄写。</para>
    /// </summary>
    private static readonly Dictionary<string, string> KeptUnreachable = new(StringComparer.Ordinal)
    {
        // ── 插件配置字段（5）──
        // 字段 Key 由**插件**声明，而插件可由外部程序集加载（src/Hmi.Hardware.Loader/
        // PluginLoadContext）⇒ 仓库内不可枚举。既定结论「只增不删」见
        // docs/2026-09-18-plugin-text-and-lang-usage-guards.md。上面这 4 个键今天没有内置插件
        // 声明（内置 Modbus TCP 的字段已改名为 host），但外部插件可以声明它们。
        ["param.ip.address"] = "插件配置字段（ipAddress）：外部插件可声明，仓库内不可枚举（既定「只增不删」）",
        ["param.tcp.port"] = "插件配置字段（tcpPort）：同 param.ip.address",
        ["param.mqtt.port"] = "插件配置字段（mqttPort）：同 param.ip.address",
        ["param.node.id.format"] = "插件配置字段（nodeIdFormat）：同 param.ip.address",
        // 第 5 个陈旧键（阶段 65 收紧 M6 后暴露）：它此前的"可达"**完全由注释提供** ——
        // 全仓出现点只有两条 XML 文档注释（CommunicationConfigView.axaml.cs:242 /
        // LangConstantUsageGuardTests.cs:18），没有任何消费者。它正是 P0-5 那个标本
        // （字典里错写成 param.baud.rate，字段 Key 其实是 baud），属阶段 62 记下的
        // 「陈旧键（param.baud.rate 等 5 个）」中的最后一个 —— 另外 4 个就在上面。
        ["param.baud.rate"] = "陈旧键（原错写成 param.baud.rate，字段 Key 实为 baud）：按「只增不删」保留（字段 Key 来自外部插件）",

        // ── 角色名（4）已于**阶段 66 全部离开本表** ──
        // ① role.operator / role.engineer / role.administrator —— **接线了**：用户列表副行、
        //    角色列表与角色详情、两个角色下拉、工程设置的运行时用户下拉，全部改走
        //    src/HmiEditor.App/Services/RoleTextKeys.cs 的键推导 ⇒ 键由 M1（有代码引用的
        //    Lang 常量）担保，属"变得可达" ⇒ 按本表规则（反向判定盯着）必须删掉这几行。
        // ② role.maintenance —— **清退**：模型只有 3 个预置角色（UserRole.BuiltInAll）、
        //    固件角色表也是 3 条（firmware/t113/tests/fixtures/generated_security.h:54-57），
        //    没有任何界面能创建第 4 个 ⇒ 它结构上不可能有消费者，属陈旧残留
        //    （处置口径与阶段 62 那 19 个键一致：字典 ×2 + Lang 常量 + 补键表）。
    };

    /// <summary>本守卫自身文件名 —— 必须从"字面量扫描面"排除：本文件里列着已删键的名字。</summary>
    private const string SelfFile = "LocalizationKeyReachabilityGuardTests.cs";

    /// <summary>读数出口（阶段 61 的 <see cref="JudgeReadings"/>）：门禁跑 minimal 时走文件通道。</summary>
    private readonly ITestOutputHelper _out;

    public LocalizationKeyReachabilityGuardTests(ITestOutputHelper output) => _out = output;

    private static readonly Regex LocRef = new(@"\{res:Loc\s+([^\s,}]+)", RegexOptions.Compiled);
    private static readonly Regex ScriptCodeLiteral = new(@"Code\s*[:=]\s*""([A-Z][A-Z0-9_]*)""", RegexOptions.Compiled);

    // ══════════════════════════════════════════════════════════════════
    //  主判据

    [Fact]
    public void EveryDictionaryKey_IsReachableBySomeMechanism()
    {
        var keys = DictionaryKeys("zh-CN");
        var reachable = ReachableKeys();
        var reachSet = reachable.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

        var unreachable = keys
            .Where(k => !reachSet.Contains(k) && !KeptUnreachable.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(unreachable.Count == 0,
            $"这 {unreachable.Count} 个字典键**全部可达机制都取不到** —— 它们要么是陈旧残留"
            + "（界面已经改用别的键），要么是界面该显示却没接线的文案。"
            + "两种情况都要处置：确认陈旧就删两本字典里的条目；确实该保留就进 KeptUnreachable 并写明理由。\n"
            + string.Join("\n", unreachable));
    }

    /// <summary>
    /// 差异表的**反向**判定（防过期抄写）：表里的键必须仍然存在、且仍然不可达。
    /// </summary>
    [Fact]
    public void KeptUnreachable_EntriesAreStillUnreachableAndStillPresent()
    {
        var keys = DictionaryKeys("zh-CN");
        var reachSet = ReachableKeys().Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

        var stale = KeptUnreachable.Keys
            .Where(k => !keys.Contains(k) || reachSet.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(stale.Count == 0,
            "KeptUnreachable 里这些条目已经**不再适用**（键已可达，或已从字典里删掉）"
            + " —— 请把对应条目删掉，否则这张表会变成过期抄写：\n"
            + string.Join("\n", stale.Select(k => $"{k}   （{KeptUnreachable[k]}）")));
    }

    [Fact]
    public void BothDictionaries_HaveTheSameKeySet()
    {
        var zh = DictionaryKeys("zh-CN");
        var en = DictionaryKeys("en-US");

        var onlyZh = zh.Except(en).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var onlyEn = en.Except(zh).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(onlyZh.Count == 0 && onlyEn.Count == 0,
            "两本字典的键集合不一致（只有 zh / 只有 en）：\n"
            + string.Join("\n", onlyZh.Select(k => "  只有 zh-CN: " + k))
            + "\n" + string.Join("\n", onlyEn.Select(k => "  只有 en-US: " + k)));
    }

    // ══════════════════════════════════════════════════════════════════
    //  元判据与双向自检

    /// <summary>
    /// **下界表** —— 每条机制「面至少这么大」。语义与判据见
    /// <see cref="MechanismFaces_AreNonEmpty"/>。
    ///
    /// <para><b>为什么是字段不是局部变量</b>（阶段 106）：它原先写在那个方法里，
    /// 于是「**类注释的机制清单** ↔ 下界表」这条比对在判据里**做不到**（局部变量看不见）。
    /// 提出来不改变任何一条下界的数值与语义。</para>
    /// </summary>
    private static readonly (string Kind, int Min)[] LowerBounds = new (string, int)[]
    {
        // 收紧后实测 643（= 有代码引用的常量个数：1915 个常量 − 1272 个无引用）。
        // 旧下界 1800 是「枚举**全部**常量值」口径下的读数，收紧后必然撞穿 ——
        // 下界必须跟着口径走，否则它守的是已经不存在的机制。
        ("M1-Lang 常量", 600),
        ("M2-axaml res:Loc", 900),
        ("M3-插件族派生", 25),   // 组成：驱动名 4 + 驱动描述 4 + 配置字段 16 + 硬件校验码 2
        ("M4-cfg.note 清单", 20),
        ("M5-脚本诊断码", 15),
        // 阶段 70 新增：AiIssueCodes 的 41 个码（29 条校验 + 12 条布局建议）。
        // 下界取满值 —— 每少一个码，AiIssueLocalizationGuardTests 的
        // 「声明了但产出不到」那一半会先点名；这里再掉下去说明整条机制没接上。
        ("M8-AI 诊断码", 41),
        // 阶段 81 新增、阶段 105 补齐：HmiPackageIssueCodes 的 **9** 个码
        // （7 条**前置校验** ＋ 2 条**中途失败** —— 阶段 105 补的 `RESLIB_INVALID` /
        // `ASSET_EXTRACT_FAILED`，登记 #62）。
        // 与 M8 同口径取下界满值 —— 少一个码，PackageIssueLocalizationGuardTests 的
        // 「声明了但触发不到」那一半会先点名。
        ("M9-包诊断码", 9),
        // 阶段 85 新增：SystemFailureCodes 的 **7** 个码（= 阶段 85 探针**实测**到的那 7 种
        // BCL 异常类型；没实测过的类型刻意不进码表）。与 M8/M9 同口径取下界满值 ——
        // 少一个码，SystemIssueLocalizationGuardTests 的双向闭包会先点名；
        // 这里再掉下去说明整条机制没接上（最可能是反射少了 BindingFlags.NonPublic）。
        ("M10-系统诊断码", 7),
        // 阶段 87 新增：SvgParseIssueCodes 的 **4** 个码（3 个由 SvgParser.Parse 真实触发 +
        // 1 个由消费方构造 SvgParseError.NoShapes —— 解析成功但零形状，对 AI 那条路是失败）。
        // 与 M8/M9/M10 同口径取下界满值：少一个码，SvgParseIssueLocalizationGuardTests 的
        // 双向闭包会先点名；这里再掉下去说明整条机制没接上（最可能：取数出口改名/挪走了）。
        ("M11-SVG 解析码", 4),
        // 阶段 88 新增：CsvImportIssueCodes 的 **5** 个码（4 个由 Parse 真实触发 +
        // 1 个是行循环的防御性兜底，进 NotProducible 表并写明理由）。
        // 与 M8/M9/M10/M11 同口径取下界满值：少一个码，CsvImportIssueLocalizationGuardTests
        // 的三态闭包会先点名；这里再掉下去说明整条机制没接上（最可能：取数出口改名/挪走了）。
        ("M12-CSV 导入码", 5),
        // 阶段 89 新增：GeneratedCodeIssueCodes **18** 个码 + FindingLocationCodes **5** 个码
        // − 1 个刻意无键的（GENERATOR_WARNING_COMMENT，登记 #61k）= **22**。
        // （定位码 4 → 5 是阶段 93 / #61l：悬空引用的定位串码化，多出
        //   SCREEN_CONTROL_ACTION —— 「动作」是键自身的固定词，不能当实参塞进
        //   SCREEN_CONTROL_PROPERTY。）与 M8~M12 同口径取下界满值：少一个键，
        // GenFindingLocalizationGuardTests 的双向闭包会先点名；这里再掉下去说明整条机制
        // 没接上（最可能：取数出口改名/挪走，或忘了摘掉那个刻意无键的码）。
        ("M13-生成代码验证码", 22),
        // 阶段 91 新增：ExpressionSyntaxIssueCodes 的 **7** 个码
        // （6 个真实产出 + 1 个结构上到不了的兜底 GENERIC，进 NotProducible 表并写明理由）。
        // 与 M8~M13 同口径取下界满值：少一个码，ExpressionSyntaxIssueLocalizationGuardTests
        // 的三态闭包会先点名；这里再掉下去说明整条机制没接上
        // （最可能：取数出口改名/挪走，或忘了那 7 个码不再是 `public const string`）。
        ("M14-表达式语法码", 7),
        // 阶段 97 新增：UserAdminIssueCodes 的 **2** 个码 + SimulatorIssueCodes 的 **1** 个码
        // = **3**。与 M8~M14 同口径取下界满值：少一个码，
        // UserAdminIssueLocalizationGuardTests 的三态闭包（双向闭包 + 两语言渲染对照 +
        // 产出方/站点形状）会先点名；这里再掉下去说明整条机制没接上（最可能：取数出口
        // 改名/挪走，或那个码不再是 `public const string`）。
        // ⚠ 两个码表分居 Core / Simulator 两个项目 ⇒ **两个**取数出口合成**一个** kind。
        ("M15-用户/模拟器码", 3),
        // 阶段 98 新增：RecipeIssueCodes 的 **2** 个码（SOURCE_MISSING / REJECTED）。
        // 与 M8~M15 同口径取下界满值：少一个码，RecipeCloneIssueLocalizationGuardTests
        // 的三态闭包（声明 = 真实产出 ∪ 明确产不出）会先点名；这里再掉下去说明整条机制
        // 没接上（最可能：取数出口改名/挪走，或那个码不再是 `public const string`）。
        // ⚠ REJECTED 在守卫里进 NotProducible（Id 撞车是概率事件）——**但它照样算进本机制**：
        //    本机制量的是「码 → 键」的推导链，不是"今天用户撞不撞得到"。
        ("M16-配方克隆码", 2),
        // 阶段 99 新增：LoginIssueCodes 的 **5** 个码（BAD_CREDENTIALS / LOCKED /
        // LOCKED_NOW / DISABLED / EXPIRED）。与 M8~M16 同口径取下界满值：少一个码，
        // LoginIssueLocalizationGuardTests 的三态闭包（声明 = 真实产出 ∪ 明确产不出）
        // 会先点名；这里再掉下去说明整条机制没接上（最可能：取数出口改名/挪走，
        // 或那个码不再是 `public const string`）。
        // ⚠ 本族与 M16 有一处**不同**：5 个码**全部**走真实产出路径（没有概率档）
        //    ⇒ 守卫的 NotProducible 表是空的，也没有「担保方」要另一条判据钉住。
        ("M17-登录码", 5),
        // 阶段 100 新增：RecipeImportIssueCodes 的 **6** 个码（5 个由 ImportCsv 真实触发 +
        // 1 个是「入库被拒」，靠新建配方的 Guid[..8] 撞车才发生 ⇒ 进 NotProducible 表
        // 并写明理由）。与 M8~M17 同口径取下界满值：少一个码，
        // RecipeImportIssueLocalizationGuardTests 的三态闭包（声明 = 真实产出 ∪ 明确产不出）
        // 会先点名；这里再掉下去说明整条机制没接上（最可能：取数出口改名/挪走，
        // 或那个码不再是 `public const string`）。
        // ⚠ 与 M16 有一处**相同**：那个概率档的码**照样算进本机制** ——
        //    本机制量的是「码 → 键」的推导链，不是"今天用户撞不撞得到"。
        ("M18-配方导入码", 6),
        // 阶段 103 新增：SimulatorBuildIssueCodes 的 **3** 个码
        // （LVGL_SOURCE_INVALID / CMAKE_NOT_FOUND / PROCESS_START_FAILED）。
        // 与 M8~M18 同口径取下界满值：少一个码，
        // SimulatorBuildIssueLocalizationGuardTests 的三态闭包（声明 = 真实产出 ∪ 明确产不出）
        // 会先点名；这里再掉下去说明整条机制没接上（最可能：取数出口改名/挪走，
        // 或那个码不再是 `const string`）。
        // ⚠ 本族是唯一一条「码表与产出方同在 HmiEditor.App」的族 ⇒ 取数出口
        //    （ExternalText.SimulatorBuildIssueKey）与码表同程序集，但**仍然必须**
        //    经 ExternalText 推导，不许在判据里自己拼前缀。
        ("M19-模拟器编译码", 3),
        // 阶段 104 新增：**通讯族**校验码 **9** 个（四个设计期驱动：8 个 Error + 1 个 Warning）。
        // ⚠ 本族的码**不住在 App 里**（在插件程序集 Hmi.Communication.Builtin）⇒ 取数出口是
        //    PluginTextLocalizationGuardTests 的**触发面**（真跑一遍驱动校验把码收上来）。
        //    下界取满值 9：触发面漏掉一条规则本族的面就会掉下来，而那条判据
        //    （CommIssueRegistry_MatchesWhatTheDriversActuallyProduce）会先点名。
        ("M20-通讯校验码", 9),
        ("M6-源码字面量", 100),
        ("M7-枚举名派生", 12),
    };

    /// <summary>每条机制都必须真的扫到东西 —— 否则它悄悄退化成空集，主判据跟着变成"只靠 M6"。</summary>
    [Fact]
    public void MechanismFaces_AreNonEmpty()
    {
        var byKind = ReachableKeys()
            .GroupBy(r => r.Kind)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Key).ToHashSet(StringComparer.Ordinal));

        var minimum = LowerBounds;

        var readings = byKind.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"    {kv.Key,-18} 面 {kv.Value.Count,5}").ToList();
        JudgeReadings.Write(_out, $"本地化键可达性（{byKind.Count} 条机制的面）", readings);

        var bad = minimum.Where(m => !byKind.TryGetValue(m.Kind, out var s) || s.Count < m.Min)
                         .Select(m => $"{m.Kind}：实测 {(byKind.TryGetValue(m.Kind, out var s) ? s.Count : 0)} < 下界 {m.Min}")
                         .ToList();

        Assert.True(bad.Count == 0, "有机制的面掉到下界以下（该机制可能整体失效）：\n" + string.Join("\n", bad));
    }

    /// <summary>
    /// **一个机制必须在三处同时申报**（阶段 106）—— 闭合阶段 104 如实登记的那条
    /// **已知判据盲区**：「加机制要同时改**下界表**与**类注释的机制清单**」这句纪律，
    /// 当时**没有任何东西盯着** ⇒ 阶段 103 的 <b>M19</b> 只进了下界表、没进清单，
    /// 到阶段 104 才被**手工**补上。手工补的地方必然再烂一次，所以立这条判据。
    ///
    /// <para><b>三处指：</b>① 类注释的**机制清单**（<c>&lt;item&gt;&lt;b&gt;M…&lt;/b&gt;</c>）；
    /// ② <see cref="LowerBounds"/> 下界表；③ <see cref="ReachableKeys"/> 的**取数**。
    /// ①↔② 按 **id** 比（清单只写 <c>M19</c>，表里写 <c>M19-模拟器编译码</c>），
    /// ②↔③ 按 **kind** 比（判据的 <c>byKind.TryGetValue</c> 就靠那个字符串对上）。</para>
    ///
    /// <para><b>②↔③ 为什么两个方向都要判</b>：「下界表有、取数造不出」那一向的现象是
    /// 面恒为 0 &lt; 下界，已被 <see cref="MechanismFaces_AreNonEmpty"/> 抓到；
    /// 反方向（**取数造了一个下界表里没有的 kind**）此前**完全静默** ——
    /// 那条机制没有下界，它退化成空集也没人管。</para>
    ///
    /// <para><b>为什么不干脆让清单自动生成</b>：清单是 XML 文档注释，每条带一段人话
    /// （阶段 / 码表 / 下界为什么取这个数），那部分正是它的价值、生成不出来。
    /// 所以做法是**用判据夹住集合**，不是消灭重复。</para>
    /// </summary>
    [Fact]
    public void Mechanisms_AreDeclaredInAllThreePlaces()
    {
        // ① 类注释的机制清单（读**本文件源码** —— XML 文档注释不进元数据，只能读文本）
        var docItems = DocMechanismItem.Matches(SelfSourceText())
            .Select(m => m.Groups[1].Value)
            .ToList();
        var docIds = docItems.Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();

        // ② 下界表
        var bounded = LowerBounds.Select(m => m.Kind).OrderBy(k => k, StringComparer.Ordinal).ToList();

        // ③ 取数（行为面 —— 这是判据真跑出来的，不是抄的）
        var produced = ReachableKeys().Select(r => r.Kind)
            .Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();

        // 空集守卫：任一处取到空，下面的差集会变成恒真（假门禁的经典形状）。
        Assert.True(docIds.Count >= 20 && bounded.Count >= 20 && produced.Count >= 20,
            $"三条来源至少应各含 20 条机制，实测 清单 {docIds.Count} / 下界表 {bounded.Count} / 取数 {produced.Count}"
            + " —— 有一处取空了，下面的差集检查会变成恒真。最可能是类注释清单的写法变了"
            + "（本判据按 `///   <item><b>M19</b>` 这个形状读），或取数出口改名后没跟上下界表。");

        // 自检：kind 必须能抠出 `M<数字>-` 前缀，否则 ①↔② 的 id 比对会静默错位。
        var boundedIds = bounded.Select(k => KindId.Match(k).Groups[1].Value).ToList();
        Assert.True(boundedIds.All(id => id.Length > 0),
            "下界表里有 kind 不符合 `M<数字>-<名>` 形状（id 抠不出来 ⇒ 与清单的比对会静默错位）：\n  "
            + string.Join("\n  ", bounded));

        // 清单里同一条写两遍：Distinct 会把它吃掉、差集看不出来 ⇒ 单列一条。
        var dupDoc = docItems.GroupBy(x => x).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}({g.Count()} 条)").OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.True(dupDoc.Count == 0,
            $"类注释的机制清单里有重复条目（同一条机制写了两遍，差集看不见）：[{string.Join(", ", dupDoc)}]");

        var onlyProduced = produced.Except(bounded, StringComparer.Ordinal).ToList();
        var onlyBounded = bounded.Except(produced, StringComparer.Ordinal).ToList();
        Assert.True(onlyProduced.Count == 0 && onlyBounded.Count == 0,
            "**取数与下界表不是同一个 kind 集合**：\n"
            + string.Join("\n", onlyProduced.Select(k => $"  取数有、下界表没有 —— 这条机制没有下界，退化成空集也没人管：{k}"))
            + (onlyProduced.Count > 0 && onlyBounded.Count > 0 ? "\n" : "")
            + string.Join("\n", onlyBounded.Select(k => $"  下界表有、取数没有 —— 面恒为 0、该机制整体失效：{k}")));

        var onlyDoc = docIds.Except(boundedIds, StringComparer.Ordinal).ToList();
        var onlyTable = boundedIds.Except(docIds, StringComparer.Ordinal).ToList();
        Assert.True(onlyDoc.Count == 0 && onlyTable.Count == 0,
            "**类注释的机制清单 与 下界表的 id 集合不一致**（阶段 103 的 M19 就漏在这一条上）：\n"
            + string.Join("\n", onlyDoc.Select(id => $"  清单里有 {id}、下界表里没有 —— 清单写了个不存在的机制？"))
            + (onlyDoc.Count > 0 && onlyTable.Count > 0 ? "\n" : "")
            + string.Join("\n", onlyTable.Select(id =>
                $"  下界表里有 {id}、**机制清单里没有** —— 加机制时忘了改类注释："
                + string.Join(" / ", LowerBounds.Select(m => m.Kind).Where(k => KindId.Match(k).Groups[1].Value == id)))));
    }

    /// <summary>本文件的源码文本。读**工作树**（不是编译产物）—— 类注释只在源码里。</summary>
    private static string SelfSourceText() =>
        File.ReadAllText(Path.Combine(
            AppSourceScanner.FindRepoRoot(), "tests", "HmiEditor.App.UiTests", SelfFile));

    /// <summary><c>M19-模拟器编译码</c> ⇒ <c>M19</c>；供「kind ↔ 清单 id」比对用。</summary>
    private static readonly Regex KindId = new(@"^(M\d+)-", RegexOptions.Compiled);

    /// <summary>类注释里的机制清单条目，形如 <c>///   &lt;item&gt;&lt;b&gt;M19&lt;/b&gt; …</c>。</summary>
    private static readonly Regex DocMechanismItem =
        new(@"^\s*///\s*<item><b>(M\d+)</b>", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// **读数 + 一条单调性判据**：M6 的面在「原文语料」与「**剥注释**语料」下各是多少。
    ///
    /// <para><b>为什么要量</b>：M6 原先的语料是 <c>src/</c>+<c>tests/</c> 的**原文**（不剥注释）
    /// —— 于是在注释里写一句这个键名，就等于"有人取过它"。这与 #48（常量侧
    /// 「名字即引用」）是**同一个病**：判据被自己的文档/注释养着。常量侧阶段 63 已用
    /// <c>ReferenceFace</c> 三档收掉；键侧这条 M6 由本阶段（阶段 65）收成 <b>剥注释语料</b>。</para>
    ///
    /// <para><b>两条口径都留着</b>：主判据走剥注释（<c>stripped</c>），对照面留在读数里
    /// （<c>raw</c>）—— 这样"注释洞今天还剩几个"永远是一个**可复跑的数字**，而不是一次性的结论
    /// （阶段 63 那条"注释侧 0 收益"的读数是同一套做法）。本轮实测 <c>raw 388 / stripped 387</c>，
    /// 差的那 1 个就是 <c>param.baud.rate</c>（全部出现点只有两条 XML 文档注释）。</para>
    ///
    /// <para>⚠ **只剥注释，不抹字面量**：M6 看的就是"整键字面量"，再抹掉内容等于把这条机制删掉
    /// —— 与常量侧要抹字面量恰好相反。**掩码要按机制选**：看「代码引用」的抹字面量、看
    /// 「字面量本身」的只剥注释。</para>
    ///
    /// <para><b>单调性</b>：剥注释只许让面变小。变大 ⇒ 剥的过程在**造**键
    /// （状态机错位把后面的行吃串了），同 <c>LangConstantUsageGuardTests.NarrowingFaces_AreMonotone</c>。</para>
    /// </summary>
    [Fact]
    public void M6Face_WithoutComments_IsReported()
    {
        // M6 只对**残差**键做匹配（前面各条机制没覆盖到）—— 复用唯一实现，不另抄一遍。
        var covered = ReachableKeys().Where(r => r.Kind != "M6-源码字面量")
            .Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        var residual = DictionaryKeys("zh-CN").Where(k => !covered.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();

        // 语料只建一次（逐键重建 = 388 × 6 MB 的纯浪费）。
        var rawCorpus = SourceCorpus();
        var strippedCorpus = SourceCorpus(stripComments: true);
        var raw = residual.Where(k => rawCorpus.Contains(k, StringComparison.Ordinal)).ToList();
        var stripped = residual.Where(k => strippedCorpus.Contains(k, StringComparison.Ordinal)).ToList();
        var onlyInComments = raw.Except(stripped, StringComparer.Ordinal).ToList();

        Assert.True(stripped.Count <= raw.Count,
            $"剥注释后 M6 面反而变大（{raw.Count} → {stripped.Count}）—— 剥的过程在造键");

        var readings = new List<string>
        {
            $"    残差键（前面各条机制没覆盖）      {residual.Count,5}",
            $"    M6 面（**判据口径**：剥注释语料）  {stripped.Count,5}",
            $"    M6 面（旧口径：原文语料，对照）    {raw.Count,5}",
            $"    注释洞收益（只在注释里出现过）     {onlyInComments.Count,5}",
        };
        readings.AddRange(onlyInComments.Take(20).Select(k => $"        · {k}"));
        if (onlyInComments.Count > 20) readings.Add($"        …还有 {onlyInComments.Count - 20} 个");
        JudgeReadings.Write(_out, "M6 面的注释敏感度", readings);
    }

    /// <summary>
    /// M6 的**口径敏感度**（阶段 262；登记 `#61s` 用户拍板）：判定口径（剥注释、**认所有非注释字面量**）
    /// 与收窄口径（再抹掉断言/消息行的字面量）**差多少**。
    ///
    /// <para><b>为什么要有这一条</b>：判据自己给键担保是**已经撞到过两次**的真事
    /// —— 阶段 97 的 NC-5 与阶段 99 的 NC4 都见证过：摘掉 M17（登录码）的取数后，本该点名全部 5 个
    /// <c>login.issue.*</c> 键，实测只点名 4 个，`login.issue.bad_credentials` 被
    /// `LoginIssueLocalizationGuardTests` 的**断言消息**写活了。口径怎么定是产品决策（用户拍板：
    /// **判定口径不动**）⇒ 这条判据把**收窄的代价**钉成数字，并**当负控用**：收窄必须真的动到东西，
    /// 否则掩码没生效、读数不可信。</para>
    ///
    /// <para>⚠ 本判据**不**断言"收窄是对的/错的"，只断言两件事：① 两口径**有差异**（负控）；
    /// ② 那两个**见证键**的差异**看得见**（下一条断言逐个点名）。任一为假 ⇒ 要么掩码失效、要么
    /// 语料变了 —— 两种都该被人看到。</para>
    /// </summary>
    [Fact]
    public void M6Face_JudgedCorpusVersusNarrowedCorpus_IsReported()
    {
        var covered = ReachableKeys().Where(r => r.Kind != "M6-源码字面量")
            .Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        var residual = DictionaryKeys("zh-CN").Where(k => !covered.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();

        var judged = SourceCorpus(stripComments: true);
        var narrowed = SourceCorpus(stripComments: true, narrowToExecutable: true);

        var judgedHit = residual.Where(k => judged.Contains(k, StringComparison.Ordinal)).ToList();
        var narrowedHit = residual.Where(k => narrowed.Contains(k, StringComparison.Ordinal)).ToList();
        var lost = judgedHit.Except(narrowedHit, StringComparer.Ordinal).ToList();

        // 全部字典键上"收窄会失去哪些键"（不只残差 —— 见证键可能已被别的机制覆盖）
        var allKeys = DictionaryKeys("zh-CN").OrderBy(k => k, StringComparer.Ordinal).ToList();
        var allJudgedHit = allKeys.Where(k => judged.Contains(k, StringComparison.Ordinal)).ToList();
        var allNarrowedHit = allKeys.Where(k => narrowed.Contains(k, StringComparison.Ordinal)).ToList();
        var allLost = allJudgedHit.Except(allNarrowedHit, StringComparer.Ordinal).ToList();

        const string Witness = "login.issue.bad_credentials";
        var witnessJudged = judged.Contains(Witness, StringComparison.Ordinal);
        var witnessNarrowed = narrowed.Contains(Witness, StringComparison.Ordinal);

        var readings = new List<string>
        {
            $"    语料是否被收窄改写                {(judged == narrowed ? "否（!!）" : "是"),5}",
            $"    残差键（前面各条机制没覆盖）           {residual.Count,5}",
            $"    M6 面（**判定口径**：剥注释，认所有非注释字面量）{judgedHit.Count,5}",
            $"    M6 面（**收窄口径**：再抹断言/消息行 → 只认可执行语句）{narrowedHit.Count,5}",
            $"    收窄在**残差**上的代价              {lost.Count,5}",
            $"    收窄在**全部字典键**上的代价        {allLost.Count,5}",
            $"    见证键 {Witness}：判定语料 {witnessJudged} / 收窄语料 {witnessNarrowed}（跨行消息 ⇒ 行级近似抓不到，见 ④）",
            "    ⚠ 失去的那个 `sim.running.stats` 是**假见证**：该键由 M1 真覆盖"
            + "（`SimulatorView.axaml.cs:559` 引用 `Lang.SimRunningStats`），断言字面量只是重复提及。",
        };
        readings.AddRange(allLost.Take(30).Select(k => $"        · 失去 {k}"));
        if (allLost.Count > 30) readings.Add($"        …还有 {allLost.Count - 30} 个");
        JudgeReadings.Write(_out, "M6 面的口径敏感度（判定口径 vs 收窄口径）", readings);

        // ① 负控：收窄必须**真的改写语料**；否则掩码没生效，这条读数就是假的
        Assert.False(judged == narrowed,
            "收窄口径没有改写语料（两个语料逐字节相同）—— 掩码没生效，读数不可信（负控）");

        // ② 本阶段真正量到的事：**收窄在残差上的代价是 0**
        //    ⇒ 今天没有任何「靠断言消息活着」的**残差**键 ⇒ M6 目前只起"减少误报"的作用
        //    （与类注释里那句"这条是代理"一致）。这条也是**防漂移的读数**：
        //    哪天它不为 0，说明收窄开始吃掉真·残差键 ⇒ 请更新登记 `#61s` 的代价表。
        Assert.True(lost.Count == 0,
            $"收窄在**残差**上的代价不再是 0（{lost.Count} 个）—— 出现「只靠断言/消息行活着」的残差键了："
            + string.Join(", ", lost.Take(10)) + " ⇒ 请更新登记 #61s 的代价表与本节结论");

        // ③ 语料里唯一"只在断言/消息行出现"的字典键是 `sim.running.stats` —— 但它是**假见证**：
        //    该键由 M1 真覆盖（`Lang.SimRunningStats` 有代码引用，见 `SimulatorView.axaml.cs:559`），
        //    那处断言字面量只是**重复提及** ⇒ 判定不依赖它。这条断言把"语料长什么样"钉住。
        Assert.Contains("sim.running.stats", allLost);
        Assert.Contains(allJudgedHit, k => k == "sim.running.stats" && !allNarrowedHit.Contains(k));

        // ④ 近似口径的**边界**（红了是好事，不是缺陷）：`login.issue.bad_credentials`
        //    在收窄语料里**仍找得到** —— 因为它的断言消息跨行（`Assert.…(` 在上一行、消息在下一行），
        //    行级近似抓不到。哪天这条红了 ⇒ 收窄变精确了 ⇒ 请更新本判据与登记 `#61s`。
        Assert.True(witnessJudged,
            $"见证键 {Witness} 在判定语料里找不到 —— 语料口径变了，请同步本判据的前提");
        Assert.True(witnessNarrowed,
            $"见证键 {Witness} 在**收窄**语料里消失了 ⇒ 收窄口径变精确了（行级近似已能抓到跨行消息）"
            + " —— 这是好事：请更新本判据的边界说明与登记 #61s（阶段 97 NC-5 / 阶段 99 NC4 的旧见证）");
    }

    /// <summary>
    /// 双向自检：每条机制点名的"已知活着的"键必须被判活；
    /// 而**已清理掉的陈旧键**必须被判成不可达（若哪条机制凭空产出了它们，说明机制算错了）。
    /// </summary>
    [Fact]
    public void KnownLiveAndKnownDead_SamplesAreJudgedCorrectly()
    {
        var reachSet = ReachableKeys().Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

        var mustBeReachable = new[]
        {
            "menu.file",                          // M1
            "reslib.search.hint",                 // M2（只在 axaml 里出现）
            "param.baud",                         // M3（字段 Key "baud" 推导）
            "driver.modbus.rtu.description",      // M3（驱动 id 推导）
            "cfg.note.LayoutSettings.ShowMenu",   // M4（清单推导）
            "script.issue.lua_syntax_missing_end",// M5（LUA_SYNTAX_MISSING_END 小写）
            "script.issue.empty_code",            // M5（Core 源码里的 Code = "EMPTY_CODE"）
        };
        var missed = mustBeReachable.Where(k => !reachSet.Contains(k)).ToList();
        Assert.True(missed.Count == 0,
            "这些键**应该**被判为可达，实际没有 —— 对应机制可能失效或面选错了：\n"
            + string.Join("\n", missed));

        // 已清理的陈旧键（2026-09-21，阶段 62）：机制**不许**凭空产出它们。
        var mustBeUnreachable = new[]
        {
            "app.splash.title", "app.splash.new_project", "app.splash.open_failed",
            "prop.param.pipe.direction.diagonal", "prop.param.pipe.flowdirection.both",
            "prop.param.valve.type.check",
            "comm.slave.list", "comm.slave.empty",
            "reslib.tt.builtin", "reslib.place.title",
        };
        var ghosts = mustBeUnreachable.Where(reachSet.Contains).ToList();
        Assert.True(ghosts.Count == 0,
            "这些键已判定为陈旧并清理，但某条机制却把它们算成可达 —— 机制算错了：\n"
            + string.Join("\n", ghosts));
    }

    // ══════════════════════════════════════════════════════════════════
    //  可达机制的实现

    private static List<(string Kind, string Key)> ReachableKeys()
    {
        var list = new List<(string, string)>();

        // ── M1：**有代码引用**的 Lang 常量值（阶段 64 收紧，见类注释 / 登记 #49）──
        // 原先直接枚举常量值 ⇒ 只要常量还被定义着就算可达 ⇒ 死常量给死键做担保。
        var liveConstants = LangConstantUsageGuardTests.CodeReferencedConstants(SelfFile);
        foreach (var f in typeof(Lang).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f.IsLiteral && f.FieldType == typeof(string)
                                 && liveConstants.Contains(f.Name)))
            list.Add(("M1-Lang 常量", (string)f.GetRawConstantValue()!));

        // ── M2：axaml 的 {res:Loc K} ──
        foreach (var (_, text) in AppSourceScanner.ScanAppXaml())
            foreach (Match m in LocRef.Matches(text))
                list.Add(("M2-axaml res:Loc", m.Groups[1].Value));

        // ── M3：插件族派生（复用守卫的推导面，不另抄一份规则）──
        foreach (var (_, key) in PluginTextLocalizationGuardTests.DerivedKeys())
            list.Add(("M3-插件族派生", key));

        // ── M4：配置字段清单的 cfg.note 键 ──
        foreach (var entry in ConfigFieldInventory.NotWired)
            list.Add(("M4-cfg.note 清单", entry.NoteKey));

        // ── M5：脚本诊断码 ──
        foreach (var code in ScriptDiagnosticCodes())
            list.Add(("M5-脚本诊断码", ExternalText.ScriptIssueKey(code)));

        // ── M8：AI 诊断码（阶段 70）──
        // 码在 HmiEditor.AI 的 AiIssueCodes 里声明、键由 ai.issue. 前缀统一推导；
        // 取数方式**复用** AiIssueLocalizationGuardTests.DeclaredCodes()，不另抄一份反射
        // —— 抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"，看不出哪边对。
        foreach (var code in AiIssueLocalizationGuardTests.DeclaredCodes())
            list.Add(("M8-AI 诊断码", ExternalText.AiIssueKey(code)));

        // ── M9：工程包诊断码（阶段 81）──
        // 码在 HmiEditor.Core 的 HmiPackageIssueCodes 里声明、键由 package.issue. 前缀统一推导；
        // 取数方式**复用** PackageIssueLocalizationGuardTests.DeclaredCodes()（同 M8 的理由：
        // 抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"，看不出哪边对）。
        foreach (var code in PackageIssueLocalizationGuardTests.DeclaredCodes())
            list.Add(("M9-包诊断码", ExternalText.PackageIssueKey(code)));

        // ── M10：系统层失败诊断码（阶段 85，登记 #61e）──
        // 码在 App 侧的 SystemFailureCodes 里声明（BCL 异常没有产出方，表只能维护在这一层）、
        // 键由 system.issue. 前缀统一推导；取数方式**复用**
        // SystemIssueLocalizationGuardTests.DeclaredCodes()（同 M8/M9 的理由：抄两份必然漂移）。
        // ⚠ 那条取数带 BindingFlags.NonPublic —— SystemFailureCodes 是 internal，
        //    少了它这里会静默收到空集（下界断言是最后的防线）。
        foreach (var code in SystemIssueLocalizationGuardTests.DeclaredCodes())
            list.Add(("M10-系统诊断码", ExternalText.SystemIssueKey(code)));

        // ── M11：SVG 解析失败诊断码（阶段 87，登记 #61a）──
        // 码在 Core 的 SvgParseIssueCodes 里声明、键由 svg.issue. 前缀统一推导；
        // 取数方式**复用** SvgParseIssueLocalizationGuardTests.DeclaredCodes()（同 M8/M9/M10 的理由：
        // 抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"，看不出哪边对）。
        // ⚠ 这一族的搬运方式有**两档**（返回值档 SvgParser.Parse 直接返回 / 异常档
        //    SvgParseException 包起来），但机制面按「码 → 键」算 —— 键只有一套。
        foreach (var code in SvgParseIssueLocalizationGuardTests.DeclaredCodes())
            list.Add(("M11-SVG 解析码", ExternalText.SvgIssueKey(code)));

        // ── M12：CSV 变量导入问题诊断码（阶段 88，登记 #61f）──
        // 码在 Core 的 CsvImportIssueCodes 里声明、键由 csv.issue. 前缀统一推导；
        // 取数方式**复用** CsvImportIssueLocalizationGuardTests.DeclaredCodes()（同 M8~M11 的理由：
        // 抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"，看不出哪边对）。
        foreach (var code in CsvImportIssueLocalizationGuardTests.DeclaredCodes())
            list.Add(("M12-CSV 导入码", ExternalText.CsvIssueKey(code)));

        // ── M13：生成代码验证消息/定位诊断码（阶段 89，登记 #61g）──
        // 码在 Core 的 GeneratedCodeIssueCodes / FindingLocationCodes 里声明、
        // 键由 gen.issue. / gen.loc. **两个**前缀统一推导；取数方式**复用**
        // GenFindingLocalizationGuardTests.DeclaredCodes() / DeclaredLocationCodes()
        // （同 M8~M12 的理由：抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"）。
        // ⚠ 必须按 MessageNoKey 摘掉那个**刻意无键**的码（GENERATOR_WARNING_COMMENT ——
        //    消息体是产物里 // WARNING: 注释的原文，翻译等于改写产物，登记 #61k）：
        //    否则这里会声明一个字典里不存在的键，"面"会虚高 1 —— 面是下界断言的输入。
        var noKeyMessages = GenFindingLocalizationGuardTests.MessageNoKey
            .Select(n => n.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var code in GenFindingLocalizationGuardTests.DeclaredCodes()
                     .Where(c => !noKeyMessages.Contains(c)))
            list.Add(("M13-生成代码验证码", ExternalText.GenIssueKey(code)));
        foreach (var code in GenFindingLocalizationGuardTests.DeclaredLocationCodes())
            list.Add(("M13-生成代码验证码", ExternalText.GenLocationKey(code)));

        // ── M14：表达式语法失败诊断码（阶段 91，登记 #61i）──
        // 码在 Core 的 ExpressionSyntaxIssueCodes 里声明、键由 expr.syntax. 前缀统一推导；
        // 取数方式**复用** ExpressionSyntaxIssueLocalizationGuardTests.DeclaredCodes()
        // （同 M8~M13 的理由：抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"）。
        foreach (var code in ExpressionSyntaxIssueLocalizationGuardTests.DeclaredCodes())
            list.Add(("M14-表达式语法码", ExternalText.ExprSyntaxKey(code)));

        // ── M15：用户管理 / 模拟器失败诊断码（阶段 97，登记 #61d）──
        // 码在 Core 的 UserAdminIssueCodes 与 Simulator 的 SimulatorIssueCodes 里各自声明、
        // 键由 user.issue. / sim.issue. **两个**前缀统一推导；取数方式**复用**
        // UserAdminIssueLocalizationGuardTests.DeclaredCodes() / DeclaredSimulatorCodes()
        // （同 M8~M14 的理由：抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"）。
        // ⚠ 模拟器那族的**中文回落**住在 SimulatorNotConnectedException 内部（private const）
        //    —— 刻意不让它进码表同族：那样反射会把回落原文也当成一条码，机制面虚高 1。
        foreach (var code in UserAdminIssueLocalizationGuardTests.DeclaredCodes())
            list.Add(("M15-用户/模拟器码", ExternalText.UserIssueKey(code)));
        foreach (var code in UserAdminIssueLocalizationGuardTests.DeclaredSimulatorCodes())
            list.Add(("M15-用户/模拟器码", ExternalText.SimulatorIssueKey(code)));

        // ── M16：配方克隆失败诊断码（阶段 98，登记 #61t）──
        // 码在 Core 的 RecipeIssueCodes 里声明、键由 recipe.issue. 前缀统一推导；
        // 取数方式**复用** RecipeCloneIssueLocalizationGuardTests.DeclaredCodes()
        // （同 M8~M15 的理由：抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"）。
        // ⚠ 这一族是**返回值档**（CloneRecipe 返回 RecipeCloneResult、不抛异常）⇒ 键由
        //    ExternalText.Describe(RecipeCloneIssue) 查，而不是 FailureReason 的 ②′/②″ 支。
        // ⚠ 两个码里 REJECTED 在守卫里进 NotProducible（Id 撞车是概率事件、不可确定性复现），
        //    但它**照样算进本机制** —— 本机制量的是「码 → 键」推导链通不通。
        foreach (var code in RecipeCloneIssueLocalizationGuardTests.DeclaredCodes())
            list.Add(("M16-配方克隆码", ExternalText.RecipeIssueKey(code)));

        // ── M17：登录失败诊断码（阶段 99，登记 #61v）──
        // 码在 Core 的 LoginIssueCodes 里声明、键由 login.issue. 前缀统一推导；
        // 取数方式**复用** LoginIssueLocalizationGuardTests.DeclaredCodes()
        // （同 M8~M16 的理由：抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"）。
        // ⚠ 这一族是**返回值档**（Login 返回 LoginResult、不抛异常）⇒ 键由
        //    ExternalText.Describe(LoginIssue) 查，而不是 FailureReason 的任何一支。
        foreach (var code in LoginIssueLocalizationGuardTests.DeclaredCodes())
            list.Add(("M17-登录码", ExternalText.LoginIssueKey(code)));

        // ── M18：配方 CSV 导入问题诊断码（阶段 100，登记 #61f）──
        // 码在 Core 的 RecipeImportIssueCodes 里声明、键由 recipe.import.issue. 前缀统一推导；
        // 取数方式**复用** RecipeImportIssueLocalizationGuardTests.DeclaredCodes()
        // （同 M8~M17 的理由：抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"）。
        // ⚠ 这一族是**返回值档**（ImportCsv 返回 RecipeImportResult、不抛异常）⇒ 键由
        //    ExternalText.Describe(RecipeImportIssue) 查，而不是 FailureReason 的任何一支。
        // ⚠ 与 M12 的 csv.issue. 同登记项但**不同族**（前者变量导入、后者配方导入）。
        foreach (var code in RecipeImportIssueLocalizationGuardTests.DeclaredCodes())
            list.Add(("M18-配方导入码", ExternalText.RecipeImportIssueKey(code)));

        // ── M19：模拟器编译 / 启动失败诊断码（阶段 103，登记 #61c）──
        // 码在 App 的 SimulatorBuildIssueCodes 里声明、键由 sim.build.issue. 前缀统一推导；
        // 取数方式**复用** SimulatorBuildIssueLocalizationGuardTests.DeclaredCodes()
        // （同 M8~M18 的理由：抄两份必然漂移，而漂移的表现是"一个守卫绿、另一个红"）。
        // ⚠ 这一族是**异常档**（产出方真抛 SimulatorBuildException）⇒ 键由
        //    ExternalText.FailureReason 的 ②‴ 支查，而不是 Describe/工厂。
        // ⚠ 与 M15 的 sim.issue.（模拟器**未连接**，HmiEditor.Simulator）**不是**一族：
        //    前缀 sim.build.issue. 正是为了让两者不串味（一条前缀一张码表）。
        foreach (var code in SimulatorBuildIssueLocalizationGuardTests.DeclaredCodes())
            list.Add(("M19-模拟器编译码", ExternalText.SimulatorBuildIssueKey(code)));

        // ── M20：通讯族校验码（阶段 104，登记 #61x）──
        // 码在**插件程序集**（Hmi.Communication.Builtin 的四个设计期驱动）里、键由 comm.issue.
        // 前缀统一推导（PluginTextKeys.CommIssueKey）；取数方式**复用**
        // PluginTextLocalizationGuardTests 的触发面（同 M8~M19 的理由：抄两份必然漂移，
        // 而漂移的表现是"一个守卫绿、另一个红"）。
        // ⚠ 这一族是**返回值档**（ValidateConnection 返回 ValidationResult、不抛异常）⇒
        //    键由视图的薄封装 CommunicationConfigView.LocalizeIssue 查，
        //    与 hw.issue.（ProjectSettingsView.LocalizeIssue）同一个形状。
        foreach (var code in PluginTextLocalizationGuardTests.TriggeredCommIssueCodes())
            list.Add(("M20-通讯校验码", PluginTextKeys.CommIssueKey(code)));

        // ── M7：枚举名派生 —— 必须排在 M6 **之前** ──
        // 理由：M6 只对"前面机制没覆盖到"的键做子串匹配（省掉 2000+ 键 × 6 MB 语料的
        // 纯浪费）。若 M7 排在 M6 后面，那 12 个 reslib.* 键就会先被 M6 判成不可达、
        // 再无谓地扫一遍语料，最后靠 M7 补回来 —— 并集不变但读数会带上 M6 的假来源。
        list.AddRange(EnumDerivedKeys());

        // ── M6：源码字面量（代理，见类注释）──
        // 语料**剥注释**（2026-09-21，阶段 65）：在注释里写一句键名**不是**"有人取过它"
        // —— 这与 #48（常量侧「名字即引用」）是同一个病，常量侧阶段 63 已收，键侧收在这条。
        // 实测收益 1 例：`param.baud.rate` 的全部出现点只有两条 XML 文档注释
        // （CommunicationConfigView.axaml.cs:242 / LangConstantUsageGuardTests.cs:18），
        // 它本来就属于阶段 62 记下的「陈旧键（param.baud.rate 等 5 个）」，只是被注释撑着
        // 没进差异表 ⇒ 收紧后按既定「只增不删」登记进去（M6Face_WithoutComments_IsReported
        // 就是量这一档的尺子）。
        // ⚠ **只剥注释，不抹字面量**：M6 看的就是"整键字面量"，再抹掉内容等于把这条机制删掉
        //   （与常量侧 `ReferenceFace.Code` 要抹字面量恰好相反 —— 掩码按机制选）。
        var dictionary = DictionaryKeys("zh-CN");
        var covered = list.Select(t => t.Item2).ToHashSet(StringComparer.Ordinal);
        var corpus = SourceCorpus(stripComments: true);
        foreach (var key in dictionary.Where(k => !covered.Contains(k)))
            if (corpus.Contains(key, StringComparison.Ordinal))
                list.Add(("M6-源码字面量", key));

        return list;
    }

    // ══════════════════════════════════════════════════════════════════
    //  同源接缝（供别的守卫复用，**不另抄一份机制 / 不另抄一份路径**）

    /// <summary>
    /// **键 → 担保它的机制集合**（一个键可能同时被多条机制担保）。
    ///
    /// <para><b>为什么做成接缝</b>：常量使用面守卫要回答「基线里这些**无代码引用**的常量，
    /// 它们的键是靠谁活着的」（登记 #35 的分类前提）—— 机制清单抄第二份必然漂移，
    /// 上面 <see cref="ReachableKeys"/> 才是唯一实现。</para>
    /// </summary>
    internal static Dictionary<string, SortedSet<string>> MechanismMap()
    {
        var map = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var (kind, key) in ReachableKeys())
        {
            if (!map.TryGetValue(key, out var kinds))
                map[key] = kinds = new SortedSet<string>(StringComparer.Ordinal);
            kinds.Add(kind);
        }
        return map;
    }

    /// <summary>两本字典的键集合（含**没有常量**的键）—— 同上，避免第三份路径副本。</summary>
    internal static HashSet<string> DictionaryKeysForReading(string language) => DictionaryKeys(language);

    /// <summary>
    /// 差异表的键（<b>只读视图</b>）—— 供常量侧守卫做**同一批例外**的交叉核对：
    /// 「键全部可达机制都取不到」在常量侧应当**恰好**表现为「这些键的常量也没人读」。
    /// 两边数的是同一批例外，不一致说明有一侧漏了或成了过期抄写。
    /// </summary>
    internal static IReadOnlyCollection<string> KeptUnreachableKeys => KeptUnreachable.Keys;

    /// <summary>
    /// <b>M7：枚举名小写派生</b>。视图里有几处把枚举成员名直接小写拼进键
    /// （<c>$"reslib.scenario.{s.ToString().ToLowerInvariant()}"</c>）——
    /// 这类键在源码里**只以模板形式存在**，M6 那种"整键字面量"扫描原理上看不见。
    ///
    /// <para><b>为什么做成"前缀→枚举"的显式两列表</b>：判据的完备性就压在这张表上，
    /// 所以要能一眼看完、一眼看出缺了谁。新增一处枚举名派生而这里没登记时，
    /// 主判据会**红在那些新键上**（因为所有机制都取不到它们）⇒ 逼维护者补一行。
    /// 这正是想要的失效方向：**表不全 ⇒ 报红**，而不是悄悄放过。</para>
    /// </summary>
    private static readonly (string Prefix, Type EnumType)[] EnumDerivedPrefixes =
    {
        ("reslib.scenario.", typeof(HmiEditor.Core.Models.ApplicationScenario)),
        ("reslib.type.", typeof(HmiEditor.Core.Models.ResourceItemType)),
    };

    private static IEnumerable<(string Kind, string Key)> EnumDerivedKeys()
    {
        foreach (var (prefix, enumType) in EnumDerivedPrefixes)
            foreach (var name in Enum.GetNames(enumType))
                yield return ("M7-枚举名派生", prefix + name.ToLowerInvariant());
    }

    /// <summary>
    /// 脚本诊断码：<c>*Code</c> 常量（<c>LuaSyntaxValidator</c> / <c>LuaDialect</c> 上的
    /// <c>public const string</c>）+ Core 源码里直接写的 <c>Code = "…"</c> 字面量。
    ///
    /// <para>两条都要：前者是"拼接出来的码"（<c>LUA_SYNTAX_MISSING_END</c>），
    /// 后者是"直接写在 <c>ScriptError</c> 上的码"（<c>EMPTY_CODE</c> 等三个不属于任何前缀族）。</para>
    /// </summary>
    private static HashSet<string> ScriptDiagnosticCodes()
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var t in new[] { typeof(LuaSyntaxValidator), typeof(LuaDialect) })
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static)
                         .Where(f => f.IsLiteral && f.FieldType == typeof(string)
                                     && f.Name.EndsWith("Code", StringComparison.Ordinal)))
                codes.Add((string)f.GetRawConstantValue()!);

        foreach (Match m in ScriptCodeLiteral.Matches(CoreSourceText()))
            codes.Add(m.Groups[1].Value);

        return codes;
    }

    // ══════════════════════════════════════════════════════════════════
    //  数据读取

    private static HashSet<string> DictionaryKeys(string language)
    {
        var path = Path.Combine(SourceScanner.RepoRoot,
            "src", "HmiEditor.Core", "Resources", $"Strings.{language}.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject().Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string CoreSourceText() => string.Join("\n",
        EnumerateSourceFiles(Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.Core"))
            .Select(File.ReadAllText));

    /// <summary>
    /// M6 的语料：<c>src/</c>+<c>tests/</c> 的 <c>.cs</c>/<c>.axaml</c> 原文。
    ///
    /// <para><b><c>Lang.cs</c> 必须排除</b>（2026-09-21，阶段 64）：它按定义写着**每一个**
    /// 字典键（常量值就是键）⇒ 留着它，M6 对"任何有常量的键"**恒真**。
    /// 实测：M1 收紧后，被释放的键**没有一个**被判不可达，全部改由 M6 担保
    /// （M6 面 113 → 539，这是**尚未排除 <c>Lang.cs</c> 时**的中间读数）——
    /// 自我满足只是从 M1 挪到了 M6。排除定义面后 M6 面落到 388，151 个键终于无处可躲
    /// （处置见类注释「两处收紧的后果与处置」）。
    /// 规矩：**判据的输入面不能包含"定义面"** —— 定义面证明的是「这个键存在」，
    /// 不是「有人在查它」。</para>
    ///
    /// <para>排除之后仍能命中的键，才是真的有消费者用**字符串键**取它
    /// （<c>LocService["login.btn"]</c> 这类）—— 那正是 M6 想看的。</para>
    ///
    /// <para><paramref name="stripComments"/> = <c>true</c> 时额外**剥注释**（<c>.cs</c> 走
    /// <see cref="SourceScanner.StripComments"/>、<c>.axaml</c> 走 <see cref="SourceScanner.StripXmlComments"/>）
    /// —— 只给 <c>M6Face_WithoutComments_IsReported</c> 这条读数用：它量的是「有多少键的"活"
    /// 完全由注释提供」。**不许**把它接进 M6 主判据（见该读数的说明）。</para>
    /// </summary>
    private static string SourceCorpus(bool stripComments = false, bool narrowToExecutable = false)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var root in new[] { "src", "tests" })
            foreach (var file in EnumerateSourceFiles(Path.Combine(SourceScanner.RepoRoot, root)))
            {
                var name = Path.GetFileName(file);
                if (name == SelfFile || name == "Lang.cs") continue;
                var text = File.ReadAllText(file);
                if (stripComments)
                    text = Path.GetExtension(file) == ".cs"
                        ? SourceScanner.StripComments(text)
                        : SourceScanner.StripXmlComments(text);
                if (narrowToExecutable)
                    text = MaskAssertionMessageLiterals(text);
                sb.AppendLine(text);
            }
        return sb.ToString();
    }

    /// <summary>「断言 / 消息行」的判据（**收窄口径**用，见 <see cref="MaskAssertionMessageLiterals"/>）。</summary>
    private static readonly Regex AssertionLine = new(
        @"(Assert\.|Fail\(|JudgeReadings\.Write)", RegexOptions.Compiled);

    /// <summary>
    /// M6 的**收窄口径**（阶段 262；登记 `#61s` 用户拍板「认所有非注释字面量 ＋ 给收窄加一条负控」）：
    /// 把「断言 / 消息行」里的字面量内容抹掉，只留**可执行语句里**的字面量。
    ///
    /// <para><b>它为什么只当读数与负控、不当判定口径</b>：判据**自己给键担保**（在断言消息里写一句键名）
    /// 在语料上**无法与真引用区分** —— 收窄能盖住这个洞，但代价是"只在断言消息里被点名的键"整批
    /// 变成不可达 ⇒ 那批键要么补**真机制**、要么进 <see cref="KeptUnreachable"/>。
    /// 口径怎么定是产品决策：**判定口径仍是「所有非注释字面量」**（<c>stripComments: true</c>），
    /// 本方法只用来把"收窄要付多少代价"变成一个**可复跑的数字**。</para>
    ///
    /// <para><b>⚠ 近似口径（明写边界）</b>：按**行**判定 —— 一行里出现 <c>Assert.</c> / <c>Fail(</c> /
    /// <c>JudgeReadings.Write</c> 就把该行的字面量抹掉（复用 <see cref="SourceScanner.MaskLiterals"/>，
    /// 它剥注释并抹字面量内容、保留插值孔）。已知漏网：消息**跨行拼接**、
    /// 或"消息在一行、断言在另一行"的写法。所以它只进读数，不参与判定。</para>
    /// </summary>
    private static string MaskAssertionMessageLiterals(string text)
    {
        if (!AssertionLine.IsMatch(text)) return text;
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            if (AssertionLine.IsMatch(lines[i]))
                lines[i] = SourceScanner.MaskLiterals(lines[i]);
        return string.Join("\n", lines);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => !SourceScanner.IsBuildOutput(f))
            .Where(f => Path.GetExtension(f) is ".cs" or ".axaml");
}
