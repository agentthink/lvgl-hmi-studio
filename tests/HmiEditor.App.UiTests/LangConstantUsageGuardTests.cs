using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// <c>Lang</c> 常量**使用面**守卫（P0-5，2026-09-18）。
///
/// <para><b>为什么需要：</b>P0-5 找到的那批缺陷里有同一个形状 ——
/// 有人按**陈旧的名字**加了常量与字典键（<c>Lang.ParamBaudRate</c> /
/// <c>param.baud.rate</c>），而界面实际推导的键是 <c>param.baud</c>：
/// 常量没人读、字典键没人查、英文界面回落插件中文。既有守卫都看不见它：
/// <c>LangKeyCoverageTests</c> 只查"常量→两本字典都解析得到"（那条是绿的），
/// 本地化守卫只查"有没有硬编码中文"（也没有）。**没人看"这个常量到底有没有人用"**。</para>
///
/// <para><b>判据：只减不增</b>。没有任何**代码**引用的常量个数锁定为基线 ——
/// 再加一个没人用的常量即红（这正是那个缺陷的入口）；清理掉死常量后必须同步改小基线
/// （否则守卫变成过期抄写）。</para>
///
/// <para><b>「引用」的三种取法（<see cref="ReferenceFace"/>）—— 阶段 63 / #48 收紧：</b>
/// 判据问的是「**代码**有没有引用它」。散文提及（<c>//</c>、<c>///</c>、XML 注释）与
/// 字面量文本（失败信息里的名字、CSV 表头、控件名、合成源码片段）**都不是引用**。
/// 这不是洁癖 —— 这类「名字即引用」的判据在本仓踩过三次，最刺眼的一次是
/// <c>EditorPreferenceWiringTests</c> 里一句
/// <c>Assert.DoesNotContain("ProjectSettingsCategoryEditor", code)</c>：
/// **守卫自己的反断言**把 <c>Lang.ProjectSettingsCategoryEditor</c> 写成了"有人用"。
/// 语义与负控见 <see cref="ReferenceFaces_IgnoreProseAndLiteralText"/>，
/// 单调性见 <see cref="NarrowingFaces_AreMonotone"/>。</para>
///
/// <para><b>实测（2026-09-21，阶段 63）</b>：三档分别是
/// 原文面 <b>20487</b> 个标识符 / <b>1267</b> 个无引用常量；
/// 剥注释面 <b>19521</b> / <b>1267</b>；再抹字面量面 <b>15806</b> / <b>1272</b>。
/// 即：**注释侧今天 0 收益**（唯一那个受害常量已在阶段 62 用「把词改掉」救回，
/// 所以这一档是纯保险）；**字面量侧 5 例**（AssetSvgNoShapesHint、LoginBtn、
/// ProjectSettingsCategoryEditor、RecipeName、RecipeVersion —— 逐条只有字面量提及，
/// 明细见登记表 #48）。</para>
///
/// <para><b>修完之后：注释里可以放心写常量名了</b> —— 本段就写了 5 个。
/// 在此之前，任何想"提醒后人这个常量是死的"的注释都会把它写活，
/// 于是处置只能是**把词改掉**（阶段 62 §16.6 就是这么干的），
/// 结果规则从"有文档"退化成"没文档" —— 与 <see cref="SourceScanner"/> 开头讲的是同一个坑。</para>
///
/// <para><b>诚实边界</b>：本判据是**代理指标**，不是"逐个判定死了"。
/// 它数的是"在 <c>src/</c>+<c>tests/</c> 里找不到代码引用"的常量；其中一部分
/// （如 <c>param.*</c> 家族）的键是**运行时动态推导**的（见
/// <c>PluginTextKeys</c>），常量本身确实没人读，但对应字典键是活的。
/// 所以基线里的名字**不能直接批量删除**，要按名字逐个查证。</para>
///
/// <para><b>阶段 65 把"逐个查证"做成了可复跑的读数</b>：
/// <c>UnusedConstants_ClassifiedByKeyMechanism</c> 按「担保其键的机制」给这批常量分桶
/// （实测 M2 828 / M6 262 / M3 18 / 无机制 5 —— 阶段 92 读数；⚠ 这一行与下面
/// <c>UnusedBaseline</c> 注释里的同一个数此前停在**阶段 89** 的 269，阶段 90/91/92
/// 三次改动都没跟着改，本阶段一并归位）。
/// 结论是**这个数不该也不会降到 0**：前 1113 个的键都活着，只是消费方式不是"代码读常量"
/// ⇒ 常量是键空间镜像。别再按"清理清单"理解这个基线。</para>
///
/// <para><b>⚠ 本轮最刺眼的一处证据</b>：<c>param.baud.rate</c> 在 <c>src/</c>+<c>tests/</c> 里
/// **只有两处**出现点，而**两处都是文档注释** —— 其中一处正是**本文件类注释的第 18 行**
/// （P0-5 写下的那条说明，恰好拿这个键当例子）。也就是说：**为了记录这个缺陷而写的文档，
/// 正是维持它"可达"的唯一证据** —— 与 #48（守卫的反断言把常量写活）同一个形状，
/// 只是这次落在**键**侧、由 M6 那条机制承担。⇒ 阶段 65 把 M6 的语料**剥注释**，
/// 关掉的正是这个入口：不收的话，本段说明本身就会把它写活
/// （读数见 <c>M6Face_WithoutComments_IsReported</c> 的两条口径：原文 388 / 剥注释 387）。</para>
/// </summary>
public class LangConstantUsageGuardTests
{
    /// <summary>
    /// 本守卫自身的文件名 —— 从扫描面排除。
    ///
    /// <para><b>收紧之后这层排除已是双保险</b>（字面量被抹掉，本文件里的名字字符串
    /// 已经不可能把自己写活；<see cref="ReferenceFaces_IgnoreProseAndLiteralText"/>
    /// 正好守这一条）。留着是因为它零成本，并且失败信息本身也就是在这个文件里拼的。</para>
    /// </summary>
    private const string SelfFile = "LangConstantUsageGuardTests.cs";

    /// <summary>
    /// 无**代码**引用的 Lang 常量个数基线（只减不增）。
    ///
    /// <para>1267 → <b>1272</b>（阶段 63）：升高的 5 个不是新增常量，而是原先被**字面量提及**
    /// 假扮成"有引用"的旧常量 —— 详见类注释的「实测」与登记表 #48。</para>
    ///
    /// <para>1272 → <b>1129</b>（阶段 64）：键可达性守卫收紧后暴露 143 个无消费者的字典键，
    /// 驱逐它们时**连带删掉了 143 个死常量**（常量值就是字典键，删键不删常量会让
    /// <c>LangKeyCoverageTests</c> 的「常量→字典」正向判定变红）。143 = 1272 − 1129，
    /// 逐项可对。</para>
    ///
    /// <para><b>⚠ 这批常量不是"待清零的清理清单" —— 阶段 65 量出了它们的构成</b>：
    /// <c>UnusedConstants_ClassifiedByKeyMechanism</c> 按「担保其键的机制」分桶，实测
    /// <b>M2（axaml <c>{res:Loc}</c>）828</b> / <b>M6（源码字面量）262</b> /
    /// <b>M3（插件族派生）18</b> / <b>无任何机制 5</b>（= <c>KeptUnreachable</c> 差异表，
    /// 与常量侧交叉核对过）。前三桶的**键都是活的**，只是消费方式不是"代码读常量"：
    /// 视图用键字面量取值，派生族的键根本没法写成常量。⇒ 这些常量是**键空间镜像**，
    /// 删掉它们，<c>LangKeyCoverageTests</c> 与补键工具**都会把名字再要回来**。
    /// 所以基线的真实语义是 **"新增一个没人读的常量即红"**（防 P0-5 那类
    /// 「按陈旧的名字加常量」复发），**不是"这个数该降到 0"**。</para>
    ///
    /// <para><b>1129 → 1125</b>（阶段 66，闭环 #50 时顺带量的）：接上角色名的接线后，
    /// 三个原来没人读的常量（<c>RoleOperator</c>/<c>RoleEngineer</c>/<c>RoleAdministrator</c>）
    /// 被 <c>RoleTextKeys</c> 代码引用 ⇒ 离开本集；被清退的陈旧键 <c>role.maintenance</c>
    /// 带走它那条常量；新增的 3 个 <c>Role*Desc</c> 常量**都有代码引用** ⇒ 不进本集。
    /// 净变化 **−4**，逐项可对。分桶里变的只有「无任何机制」：**9 → 5**
    /// （4 条角色名键由 M1 接走/清退，与差异表两侧一致 —— 那条交叉核对判据是自动转绿的，
    /// 没有手改任何断言）。</para>
    ///
    /// <para><b>1125 → 1124</b>（阶段 73）：<c>ScriptEditorView</c> 里那一行取帮助正文的代码
    /// 原本用的是**字面量** <c>"script.lua.help.text"</c>（常量在、却没人读 ⇒ 只能靠 M6 桶担保），
    /// 阶段 73 把它改成 <c>Lang.ScriptLuaHelpText</c> —— 起于新判据
    /// <c>LuaHelpDialogUiTests</c> 要读这条常量，顺手把同一文件里唯一那处字面量也统一了。
    /// 该常量离开本集 ⇒ 净变化 **−1**，分桶里变的只有 **M6：274 → 273**
    /// （M2 828 / M3 18 / 无机制 5 都不动）。</para>
    ///
    /// <para><b>1124 → 1123</b>（阶段 82，闭环 #60）：<c>Lang.StatusOpenFailed</c> 的键
    /// <c>status.open.failed</c> 唯一使用点（<c>MainWindow.axaml.cs:1168</c>）写的是**字面量**
    /// ⇒ 该常量本来就没有代码引用、只能靠 M6 桶担保。阶段 82 把两条打开路径的失败渲染收成
    /// 唯一出口 <c>UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)</c> 之后那处字面量消失，
    /// <c>status.open.failed</c> 随之**从两本字典删掉**（否则可达性守卫会报"取不到的键"），
    /// 常量也**连带删除**（<c>LangKeyCoverageTests</c> 的正向判定要求常量与键同生共死）。
    /// 净变化 **−1**，分桶里变的只有 **M6：273 → 272**（M2 828 / M3 18 / 无机制 5 都不动）。
    /// 同时新增的 <c>UiOpFailedDetailFile</c> **有代码引用**（新出口在读它）⇒ 不进本集，
    /// 所以可达性面 **M1 650 → 651**、**M6 386 → 385**，而本基线反而降 1 —— 两者不矛盾：
    /// 前者数"有代码引用的常量"，后者数"无代码引用的常量"。</para>
    ///
    /// <para><b>1123 → 1120</b>（阶段 86，闭环 #61b）：C 类 9 站里 8 站从
    /// <c>Format(键, ex.Message)</c> 迁到 <c>UiOperationGuard.FailureDetail(操作名, 路径, 异常)</c>，
    /// 连带删掉 3 条**已无读者**的键常量（<c>GenExportFailed</c> / <c>StatusSaveFailed</c> /
    /// <c>AuditExportCCodeFailed</c>）—— 那 3 个键的全部使用点写的都是**字面量**，常量本来只靠
    /// M6 桶担保（与上一条 <c>StatusOpenFailed</c> 同一个形状）。净变化 **−3**，
    /// 分桶里变的只有 **M6：272 → 269**（M2 828 / M3 18 / 无机制 5 都不动）。
    /// 同时新增的 4 个操作名常量（<c>UiOpExportRecipe</c> / <c>UiOpExportSlaveVars</c> /
    /// <c>UiOpImportSvg</c> / <c>UiOpImportFont</c>）**都有代码引用**（迁后的站点在读它们）
    /// ⇒ 不进本集；所以「有常量的键 1774 → 1775 而本基线 −3」不矛盾 —— 两者数的集合不同。
    /// 读数：<c>UnusedConstants_ClassifiedByKeyMechanism</c>（落盘实测，不是推断）。</para>
    ///
    /// <para><b>1120 → 1116</b>（阶段 90，闭环 #61h）：阶段 88 用「按产出方类型倒查」量出的
    /// 5 站 IO 未迁（<c>var.import.failed</c> / <c>gen.import.failed</c> /
    /// <c>comm.slave.var.import.failed</c> / <c>font.export.fail</c> /
    /// <c>status.simulator.export.failed</c>）全部迁到
    /// <c>UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)</c> ⇒ 连带删掉 4 条**已无读者**的
    /// 键常量（<c>VarImportFailed</c> / <c>GenImportFailed</c> / <c>FontExportFail</c> /
    /// <c>StatusSimulatorExportFailed</c>）。⚠ 删的是 5 个**键**、只带走 4 个**常量** ——
    /// <c>comm.slave.var.import.failed</c> 那条键**本来就没有常量**（与 <c>StatusOpenFailed</c>
    /// 同一个形状：使用点写的是**字面量**，常量只能靠 M6 桶担保，而它连常量都没建）。
    /// 净变化 **−4**，分桶里变的只有 **M6：269 → 265**（M2 828 / M3 18 / 无机制 5 都不动）。
    /// 同时新增的 4 个操作名常量（<c>UiOpImportVariables</c> / <c>UiOpImportRecipe</c> /
    /// <c>UiOpImportSlaveVars</c> / <c>UiOpExportCharset</c>）**都有代码引用**（迁后的站点在读它们）
    /// ⇒ 不进本集；所以「字典键 2220 → 2219（−1）而本基线 −4」不矛盾 —— 两者数的集合不同。
    /// 读数：<c>UnusedConstants_ClassifiedByKeyMechanism</c>（本次**落盘实测**，不是推断）：
    /// 无代码引用的常量 <b>1116</b> / 字典键 2219 / 其中有常量的键 1776。</para>
    ///
    /// <para><b>阶段 91（登记 #61i）再 −1 ⇒ 1115</b>：删掉 <c>Lang.MsgExprError</c> ——
    /// 它唯一的消费点是 <c>PropertyPanelView</c> 里那个 <c>?? AppLocalization.Service["msg.expr.error"]</c>
    /// 兜底分支，而那半由 <c>ExpressionEngine.TryCompile</c> 的 <c>NotNullWhen(false)</c>
    /// 保证**取不到** ⇒ 键与常量一起退场（字典键 2219 → 2218）。
    /// 分桶里变的仍只有 **M6：265 → 264**（那条键的担保来自 M6 的整键字面量）。
    /// ⚠ 同阶段新增的 7 条 <c>expr.syntax.*</c> 键**没有**对应常量（本族靠
    /// <c>ExternalText.ExprSyntaxKey</c> 推导，与 svg/csv/gen 三族一致）⇒ 不进本集；
    /// 字典键数因此在同阶段内是 2219 − 1 + 7 = <b>2225</b>，而本基线只 −1 ——
    /// 两者数的集合不同，不是对不上。</para>
    ///
    /// <para><b>1115 → 1113</b>（阶段 92，闭环 #61j）：<c>GenError</c> / <c>GenWarning</c>
    /// 这两个常量**早就存在**（阶段 89 建的），但全部使用点写的都是**字面量**
    /// （<c>AppLocalization.Service["gen.error"]</c> / <c>["gen.warning"]</c>）⇒ 它们只能靠 M6
    /// 桶担保。阶段 92 把「生成代码验证结果」这一族的显示文案收成唯一映射
    /// （<c>FindingDisplayText</c>）时顺手改回常量 ⇒ 两个常量离开本集。
    /// 净变化 **−2**，分桶里变的只有 **M6：264 → 262**（M2 828 / M3 18 / 无机制 5 都不动）。
    /// ⚠ 同阶段新增的 <c>finding.copy.line</c> 两本各 +1 键，其中 <c>Lang.FindingCopyLine</c>
    /// **有代码引用**（映射在读它）⇒ 不进本集；所以「字典键 2225 → 2226（+1）而本基线 −2」
    /// 不矛盾 —— 两者数的集合不同。</para>
    ///
    /// <para><b>1113 不动</b>（阶段 93，闭环 #61l）：两本字典各 +1 键
    /// （<c>gen.loc.screen_control_action</c>，2226 → <b>2227</b>），但**没有新增任何常量** ——
    /// 这一族（<c>gen.issue.*</c> / <c>gen.loc.*</c>）的键靠 <c>ExternalText.GenIssueKey</c> /
    /// <c>GenLocationKey</c> **推导**，本就不进本集；可达性由机制 <b>M13</b> 担保
    /// （面 21 → <b>22</b>，下界同步抬满）。⇒ 「字典 +1 而基线不动」不矛盾，两者数的集合不同。</para>
    /// <para><b>与补键工具的方向冲突（已知陷阱，登记 #52）</b>：
    /// <c>scripts/sync_localization.py</c> 第 2 步会给「字典里有、<c>Lang.cs</c> 里没有」的键
    /// **生成常量** —— 干跑实测今天有 <b>360</b> 个这样的键，跑一次就会新增 360 个常量
    /// （且**必然**落进本基线）⇒ 本判据红、基线被"顺手改大"。两个方向必须选一个
    /// （常量集合与键集合同构，或常量集合只装"代码真正用到的键"），
    /// 别让下一个人用改数字的方式解决它。</para>
    ///
    /// <para><b>处置（同日，选「只堵陷阱」）</b>：工具已改成 **默认只干跑**（落盘要显式
    /// <c>--write</c>），而「生成常量」**另外**要显式 <c>--add-consts</c>（那道闸包住整段生成
    /// ⇒ 不开闸时连 <c>Lang.cs</c> 的写入分支都进不去）⇒ 上面那条路径
    /// （顺手跑一次 ⇒ 基线被顶穿 ⇒ 改大基线）**在默认动作下不可达**。
    /// 方向 A/B 仍是决策项（登记 #52），但不再有"手滑就中"的入口。
    /// 这条默认值本身由 <see cref="SyncTool_DefaultsToDryRunAndGatesConstantGeneration"/> 盯着
    /// —— 锚点落在**赋值表达式**上而不是旗标字符串上，否则工具自己的文件头注释就会满足它。</para>
    ///
    /// <para><b>1113 → 1112</b>（阶段 99，闭环 #61v）：删掉 <c>Lang.LoginErr</c> —— 它唯一的
    /// 使用点（<c>LoginDialog.OnLogin</c> 的失败档）写的是**字面量** <c>"login.err"</c>
    /// ⇒ 该常量本来就没有代码引用、只能靠 M6 桶担保（与 <c>StatusOpenFailed</c> 同一个形状）。
    /// 阶段 99 把登录失败改成**按码分派**（<c>login.issue.*</c>，机制 M17）之后那句话不再出现，
    /// 键与常量**一起退场**。净变化 **−1**，分桶里变的只有 **M6：262 → 261**
    /// （M2 828 / M3 18 / 无机制 5 都不动）。⚠ 同阶段新增的 5 条 <c>login.issue.*</c> 键
    /// **没有**对应常量（本族靠 <c>ExternalText.LoginIssueKey</c> 推导，与 svg/csv/gen/recipe 四族一致）
    /// ⇒ 不进本集；所以「字典键 2272 → 2276（+4）」而本基线只 −1，两者数的集合不同。</para>
    ///
    /// <para><b>1112 → 1110</b>（阶段 101，#61a 尾巴那一刀）：<c>asset.import.fail</c> 与
    /// <c>status.svg.apply.failed</c> 两站的失败渲染迁到
    /// <c>UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)</c> ⇒ 两处**字面量**消失，
    /// 两个常量（<c>AssetImportFail</c> / <c>StatusSvgApplyFailed</c>）本来就没有代码引用、
    /// 只能靠 M6 桶担保（与阶段 82 的 <c>StatusOpenFailed</c> 同一个形状）
    /// ⇒ 键与常量**一起退场**。净变化 <b>−2</b>，分桶里变的只有 <b>M6：261 → 259</b>
    /// （M2 828 / M3 18 / 无机制 5 都不动）。同时新增的 2 个操作名常量
    /// （<c>UiOpApplySvg</c> / <c>UiOpImportImage</c>）**都有代码引用**（迁后的站点在读它们）
    /// ⇒ 不进本集；所以「字典键 2283 → 2283（−2+2 净 0）」而本基线 −2，两者数的集合不同。
    ///
    /// <para><b>阶段 102 再 −2（1110 → 1108）</b>：<c>Lang.CompileFailed</c> /
    /// <c>Lang.DownloadSharedLibFail</c> 退场 —— 它们的站点写的是**字面量**（键靠 M6 桶担保），
    /// 迁到 <c>UiOperationGuard.FailureDetail</c> 之后键不可达 ⇒ 键与常量**同生共死**一起删。
    /// 同阶段新增的 <c>UiOpPrepareSharedLib</c> **有代码引用**（同一个理由：不进本集）。</para>
    ///
    /// <para><b>阶段 137 再 −4（1108 → 1104）</b>：两个死视图（<c>VariableBrowserView</c> /
    /// <c>PrototypeCanvas</c>，登记 <c>#71</c> / <c>#73</c>）由用户拍板**下架** ⇒ 7 个常量、
    /// 两本字典各 7 个键一起删。其中**本集里少掉的是 4 个**（<c>BrowserRefresh</c> /
    /// <c>BrowserReady</c> / <c>BrowserTtRefresh</c> / <c>PanelToolbox</c> —— 它们的站点本来
    /// 也写的是**字面量** `{res:Loc …}`，键一直靠 M6 桶担保，所以它们**没有**代码引用）；
    /// 另 3 个（<c>VarBrowserNoManager</c> / <c>VarBrowserMatch</c> / <c>VarBrowserTotal</c>）
    /// 是**有**代码引用的（写在 <c>VariableBrowserView.axaml.cs</c> 里）⇒ 本来就不进本集。
    /// 🩸 这正是本基线"只减不增"纪律的一次正常兑现：**删的必须比加的多**，
    /// 而"删了几个"要按**本集的口径**数（4），不是按删掉的常量总数（7）数。</para>
    ///
    /// <para><b>阶段 172 再 −7（1104 → 1097）</b>：报表 MVP 的 App 侧映射
    /// （<c>Services/AuditReportText.cs</c>）**复用**了既有的两组词 ——
    /// 审计列名 <c>Lang.UserMgrAuditTime/User/Action/Result/Source/Detail</c> **6 个** ＋
    /// 结果词 <c>Lang.GenSuccess</c>/<c>Lang.GenFailed</c> **2 个** ⇒ 这 **8 个**此前"没有代码引用"
    /// 的常量**变活了**（它们此前只被 axaml 的 <c>{res:Loc …}</c> 字面量或动态推导担保，见上文边界）；
    /// 本刀自己新增的 9 个常量里 8 个有代码引用、1 个（<c>AuditReportExportBtn</c>）只出现在 axaml
    /// 的 <c>{res:Loc audit.report.btn}</c> 字面量里 ⇒ **净变化 −8 + 1 = −7**。
    /// ⚠ 不是"顺手删了死常量"，是**复用把死引用变活** —— 复核时按这 8 个名字逐个看即可。</para>
    ///
    /// <para><b>阶段 174 ＋1（1097 → 1098）</b>：报警报表的"导出报表"按钮常量
    /// <c>AlarmReportBtn</c> 只出现在 <c>AlarmListView.axaml</c> 的 <c>{res:Loc alarm.report.btn}</c>
    /// **字面量**里（本集的"代码引用"口径看不见 axaml 花括号 —— 与阶段 172 的
    /// <c>AuditReportExportBtn</c> 同一形状）；同一批新增的其余 13 个常量都有代码引用
    /// （`ReportExport` / `AlarmReportText` 里逐个 `Lang.*` 出现）。</para>
    ///
    /// <para><b>阶段 175 ＋1（1098 → 1099）</b>：同一形状第三次出现 —— 历史数据报表的按钮常量
    /// <c>TrendReportBtn</c> 只出现在 <c>TrendConfigView.axaml</c> 的 <c>{res:Loc trend.report.btn}</c>
    /// 字面量里；同一批新增的其余 4 个常量都有代码引用（`HistoryReportText` / `TrendConfigView`）。</para>
    ///
    /// <para><b>阶段 177 ＋3（1099 → 1102）</b>：报表列选择对话框的三个常量
    /// （<c>ReportColumnsTitle</c> / <c>ReportColumnsNone</c> / <c>ReportColumnsHint</c>）只出现在
    /// <c>ReportColumnDialog.axaml</c> 的 <c>{res:Loc …}</c> 字面量里 —— 与本集"代码引用"口径的
    /// 老问题同一形状（阶段 172 的 <c>AuditReportExportBtn</c>、174 的 <c>AlarmReportBtn</c>、
    /// 175 的 <c>TrendReportBtn</c> 都这样）。</para>
    /// </summary>
    ///
    /// <para><b>阶段 178 ＋7（1102 → 1109）</b>：报告时间范围对话框的七个常量
    /// （标题/提示/五个档位）同样只出现在 <c>ReportRangeDialog.axaml</c> 与
    /// <c>ReportRangeDialog.axaml.cs</c> 的花括号/键名里 —— 与本集"代码引用"口径的老问题同一形状。</para>
    /// </summary>
    ///
    /// <para><b>阶段 179 ＋1（1109 → 1110）</b>：报表包的按钮常量 <c>ReportBundleBtn</c> 只出现在三个面板的
    /// axaml 花括号里（同一形状第六次）；同批新增的另三个常量（另存标题/成功/操作名）都有代码引用。</para>
    ///
    /// <para><b>阶段 180 −5（1110 → 1105）</b>：本集**第一次降**，而且是<b>清理</b>而不是漏加。
    /// 新增的 <c>ReportRangeText</c>（Core 给键、App 推键，同 <c>AuditReportText</c> 一族）把六个档位的
    /// 键都由<b>常量</b>供给 ⇒ 此前"只出现在花括号/键名里"的 <c>ReportRangeAll</c> /
    /// <c>LastHour</c> / <c>Last24h</c> / <c>Last7d</c> / <c>Last30d</c> 五个常量（<b>不含</b>本阶段新增的
    /// <c>ReportRangeCustom</c>，它生下来就有引用）从死常量变成活引用；
    /// 标题/提示仍走 <c>{res:Loc}</c>（那里要的是**语言切换能刷新**，不能改成代码赋值）⇒ 那两个照旧在基线里。
    /// 这就是本集类注释说的"键空间镜像"可被**部分**消化：能收的收进映射，收不了的（XAML 绑定）如实留着。</para>
    ///
    /// <para><b>阶段 183 ＋4（1105 → 1109）</b>：报表设计器对话框的四个常量（标题/提示/面板按钮文案/
    /// 恢复默认）只出现在 <c>{res:Loc}</c> 的花括号里（同一形状第七次）；同批新加的操作名
    /// <c>UiOpReportDesigner</c> 在面板里被读 ⇒ 有引用。
    /// 🩸 <b>本阶段差点漏掉记账</b>：我给新窗口写的"键在两本字典里"判据第一版用了
    /// <c>Lang.ReportDesigner*</c> **常量名** —— 那会把四个死常量**写活**（本集类注释里记的 `#48` 形状：
    /// 守卫自己的断言成了常量唯一的存在理由），于是基线"恰好"没变、差集判据失去意义。
    /// 改成**字面量键**后才如实涨 4。⇒ 规矩：这一族"键在不在字典里"的判据一律用字面量。</para>
    /// </summary>
    internal const int UnusedBaseline = 1109;

    /// <summary>引用集合的取法。生产判据只用 <see cref="Code"/>；另两档是自检/单调性用的对照面。</summary>
    private enum ReferenceFace
    {
        /// <summary>原文（阶段 63 之前的旧口径）：注释与字面量里的名字都算引用。</summary>
        Raw,

        /// <summary>只剥注释，保留字面量内容。</summary>
        NoComments,

        /// <summary>剥注释 <b>+</b> 抹掉字面量内容（插值孔保留）—— 生产判据用这一档。</summary>
        Code,
    }

    private static readonly Regex Identifier = new(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

    [Fact]
    public void UnusedLangConstants_MatchBaseline()
    {
        var unused = UnusedConstants();
        Assert.True(unused.Count == UnusedBaseline,
            $"无代码引用的 Lang 常量个数 {unused.Count} ≠ 基线 {UnusedBaseline}。"
            + "涨了 = 新增了没人读的常量（先确认它在哪个界面被读，或直接别加）；"
            + $"降了 = 清理了死常量，请把 UnusedBaseline 改成 {unused.Count}。\n"
            + "（完整清单很长，前 30 个：）\n" + string.Join("\n", unused.Take(30)));
    }

    /// <summary>
    /// **读数**（不是断言）：把基线里那些「无代码引用的常量」按**担保其键的机制**分桶。
    ///
    /// <para><b>为什么需要它</b>：<see cref="UnusedBaseline"/> 的注释说「清理掉死常量后必须
    /// 同步改小基线」，读起来像"这是一份待清零的清理清单"。但**键的消费方式不止代码**：
    /// 视图用 <c>{res:Loc K}</c> 取键、插件族 / 配置清单 / 脚本诊断码 / 枚举名是**运行时拼**出来的
    /// —— 这几类的键结构上不需要 C# 读常量。而 <c>scripts/sync_localization.py</c> 还会反过来
    /// 给「字典里有、<c>Lang.cs</c> 里没有」的键**生成常量**（工具的不变量是"每个键都有常量"）
    /// ⇒ 常量集合与键集合**同构**，这个基线**结构上到不了 0**。所以它到底是"待清理"还是
    /// "键空间镜像"，只能靠分桶回答 —— 这就是登记 #35 的分类前提。</para>
    ///
    /// <para><b>为什么只读数不断言</b>：见 <see cref="JudgeReadings"/> —— 数字写成断言会逼出
    /// "为改数字而改测试"。保留的断言都不是数字抄写，而是**关系**：
    /// ① 机制面非空（否则整张表会退化成"全部无机制"）；
    /// ② 分类**完备**（各桶之和 = 总数，漏桶/重复计数都会红）；
    /// ③ 交叉核对：**没有机制担保的键**在常量侧的投影必须**恰好**是键可达性守卫的差异表
    ///    （<c>KeptUnreachable</c>）—— 两侧数的是同一批例外。</para>
    /// </summary>
    [Fact]
    public void UnusedConstants_ClassifiedByKeyMechanism()
    {
        // 常量名 → 常量值（= 字典键）。与 LangConstants() 同一套反射口径，只是换个投影。
        var values = typeof(Lang)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!, StringComparer.Ordinal);

        var mechanisms = LocalizationKeyReachabilityGuardTests.MechanismMap();
        var dictionaryKeys = LocalizationKeyReachabilityGuardTests.DictionaryKeysForReading("zh-CN");
        var unused = UnusedConstants();

        // ① 机制面非空（下界只为"反射/扫描失效"兜底；今天实测 2132 个键里绝大多数都有机制）
        Assert.True(mechanisms.Count >= 2000,
            $"机制面只覆盖 {mechanisms.Count} 个键 —— 面找错了，或某条机制整体失效");

        static string Shorten(string kind) => kind.Split('-')[0];

        var byKinds = unused
            .GroupBy(c => mechanisms.TryGetValue(values[c], out var kinds)
                ? string.Join(" + ", kinds.Select(Shorten))
                : "**无任何机制**")
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        // ② 分类完备：桶之和 = 总数（分类写错/重复归桶都会红）
        Assert.Equal(unused.Count, byKinds.Sum(g => g.Count()));

        // ③ 交叉核对（同源）：无机制担保的键 ⇄ 差异表，两个方向都要对得上
        var noMechanism = unused.Where(c => !mechanisms.ContainsKey(values[c]))
            .Select(c => values[c]).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var kept = LocalizationKeyReachabilityGuardTests.KeptUnreachableKeys
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(noMechanism.SequenceEqual(kept),
            "常量侧「键无任何机制」的集合与键可达性守卫的差异表不一致（两侧数的是同一批例外）：\n"
            + $"  常量侧 {noMechanism.Count} 个：{string.Join(", ", noMechanism)}\n"
            + $"  差异表 {kept.Count} 个：{string.Join(", ", kept)}\n"
            + "⇒ 上表少了 = 那个键的常量已不是「没人读」（该从差异表删）；多了 = 有一侧漏了或成了过期抄写");

        var constantKeys = values.Values.ToHashSet(StringComparer.Ordinal);
        var withConstant = dictionaryKeys.Count(constantKeys.Contains);

        var lines = new List<string>
        {
            $"    无代码引用的常量        {unused.Count,5}",
            $"    字典键（zh-CN）          {dictionaryKeys.Count,5}",
            $"    其中**有**常量的键       {withConstant,5}",
            $"    其中**没有**常量的键     {dictionaryKeys.Count - withConstant,5}",
            "    ── 按「担保其键的机制组合」分桶 ──",
        };
        lines.AddRange(byKinds.Select(g =>
            $"    {g.Key,-24} {g.Count(),5}   例：{string.Join(" · ", g.Select(c => values[c]).Take(3))}"));
        JudgeReadings.Write(null, "无代码引用的 Lang 常量（按担保机制分桶）", lines);
    }

    /// <summary>
    /// **静态守卫**：补键工具必须**默认不落盘**，且「生成常量」必须**显式开闸**（登记 #52）。
    ///
    /// <para><b>为什么值得一条判据</b>：这个陷阱的性质是"某天有人顺手跑一次工具"。
    /// 只要默认值被改回写盘、或那道闸被拆掉，`UnusedBaseline` 就会被**一次运行**顶穿
    /// （360 个新常量），而症状是判据红、处置动作却是"改大基线" ⇒ 防护失效。
    /// 也就是说：**这条判据守的是另一条判据的有效性**。</para>
    ///
    /// <para><b>锚点必须落在赋值表达式上</b>（`write = '--write' in sys.argv` 这种），
    /// 不能锚在 `--write` / `--add-consts` 这些**旗标字符串**上：工具的文件头注释里
    /// 就写着它们（那正是本阶段的说明），锚在旗标上等于让**工具的文档**满足这条判据
    /// —— 与本文件主题（#48 / 阶段 65 的「名字即引用」）是同一个病。
    /// 顺带把**旧形状**（`dry_run = '--dry-run' in sys.argv` ⇒ 默认写盘）也反着断言一次。</para>
    /// </summary>
    [Fact]
    public void SyncTool_DefaultsToDryRunAndGatesConstantGeneration()
    {
        var path = Path.Combine(SourceScanner.RepoRoot, "scripts", "sync_localization.py");
        Assert.True(File.Exists(path), $"找不到补键工具：{path}（路径或文件名改了？）");
        var code = File.ReadAllText(path);
        Assert.True(code.Length > 3000, $"补键工具只剩 {code.Length} 字符 —— 面找错了或文件被掏空");

        var missing = new List<string>();
        void Need(string anchor, string why)
        {
            if (!code.Contains(anchor, StringComparison.Ordinal)) missing.Add($"{why}\n      锚点：{anchor}");
        }

        Need("write = '--write' in sys.argv", "落盘开关不见了（默认会写盘 ⇒ #52 的陷阱重新装回来）");
        Need("dry_run = not write", "干跑与落盘没绑在一起（默认可能又变成写盘）");
        Need("add_consts = '--add-consts' in sys.argv", "「生成常量」那道闸不见了");
        Need("if add_consts:", "闸没被用在生成段上");

        Assert.True(missing.Count == 0,
            "补键工具 `scripts/sync_localization.py` 的默认值/闸门被改动了（登记 #52）：\n   - "
            + string.Join("\n   - ", missing));

        Assert.False(code.Contains("dry_run = '--dry-run' in sys.argv", StringComparison.Ordinal),
            "**旧的「默认写盘」开关回来了**：`dry_run = '--dry-run' in sys.argv` 意味着"
            + "不带参数跑就会写盘 —— 那正是 #52 记下的陷阱形状（实测跑一次会新增几百个常量、"
            + $"把 `UnusedBaseline`（现 {UnusedBaseline}）一次顶穿）。"
            + "要落盘必须是显式的 `--write`。");
    }

    [Fact]
    public void Census_SeesConstantsAndReferences()
    {
        // 自检：两侧都必须真的扫到东西（防枚举/扫描失效后退化成"永远绿"）。
        var constants = LangConstants();
        // 下界只为"反射没扫到"兜底（真失效时会掉到 0/个位数）。读数：阶段 64 驱逐 143 个
        // 死常量后 1772（此前 1915）—— 旧下界 1800 就是这么被撞穿的：没同步读数。
        Assert.True(constants.Count >= 1500, $"Lang 常量只扫到 {constants.Count}");
        var referenced = ReferencedIdentifiers();
        Assert.True(referenced.Count >= 10000, $"标识符只扫到 {referenced.Count}");
        Assert.True(constants.Count(c => referenced.Contains(c)) >= 500,
            "被引用的常量不足 500 —— 扫描面可能漏了大部分源文件");
    }

    /// <summary>
    /// **收紧的语义判据**：散文提及与字面量文本不算引用，代码与插值孔里的算。
    ///
    /// <para><b>两侧都断言</b>：<see cref="ReferenceFace.Raw"/> 面**确实**会被那两处
    /// 非引用提及满足（这就是 #48 那个坑），<see cref="ReferenceFace.Code"/> 面不会。
    /// 只断言后者的话，判据在"两档其实都看不见"时也是绿的 —— 那是假绿。</para>
    ///
    /// <para><b>为什么用合成片段而不是语料</b>：真实语料今天在注释侧恰好是 0 例，
    /// 拿语料当判据等于没有负控（"有一次绿等于零信息"）。这里把两种提及都造出来，
    /// 判据才有牙齿。片段里的名字都是**不存在的常量名**（故意）—— 用具名常量会重演 #48。</para>
    /// </summary>
    [Fact]
    public void ReferenceFaces_IgnoreProseAndLiteralText()
    {
        const string synthetic = """
            // 散文提及 FakeProseOnly —— 不算引用
            var a = "FakeLiteralOnly";                    // 字面量文本，也不算
            var b = Lang.FakeCodeRef;                     // 真代码引用
            var c = $"尾巴 {Lang.FakeInterpHole} 尾巴";   // 插值孔里是代码
            """;

        var raw = NamesIn(ForFace(ReferenceFace.Raw, "Probe.cs", synthetic));
        var code = NamesIn(ForFace(ReferenceFace.Code, "Probe.cs", synthetic));

        // 旧口径：散文与字面量都能把名字"写活"（#48 的病根）
        Assert.Contains("FakeProseOnly", raw);
        Assert.Contains("FakeLiteralOnly", raw);

        // 新口径：只剩真代码
        Assert.DoesNotContain("FakeProseOnly", code);
        Assert.DoesNotContain("FakeLiteralOnly", code);
        Assert.Contains("FakeCodeRef", code);
        Assert.Contains("FakeInterpHole", code);
    }

    /// <summary>
    /// **单调性**：越往后剥，标识符集合只许变小。若某一档反而变**大**，
    /// 说明剥的过程在制造标识符（状态机错位把后面的代码重复扫了/把行吃串了）——
    /// 那会让基线凭空变化，且变化方向是"看起来更干净"的假象。
    ///
    /// <para>顺带守住"三档不是同一个集合"（否则收紧根本没生效，判据会静默退化）。</para>
    /// </summary>
    [Fact]
    public void NarrowingFaces_AreMonotone()
    {
        var raw = CollectIdentifiers(ReferenceFace.Raw);
        var noComments = CollectIdentifiers(ReferenceFace.NoComments);
        var code = CollectIdentifiers(ReferenceFace.Code);

        Assert.True(noComments.IsSubsetOf(raw),
            $"剥注释后多出了 {noComments.Count(x => !raw.Contains(x))} 个原文里没有的标识符 —— 剥的过程在造标识符");
        Assert.True(code.IsSubsetOf(noComments),
            $"抹字面量后多出了 {code.Count(x => !noComments.Contains(x))} 个上游没有的标识符");

        // 读数（#42）：三档必须逐级变窄
        Assert.True(noComments.Count < raw.Count, $"剥注释没让集合变窄：{noComments.Count} vs {raw.Count}");
        Assert.True(code.Count < noComments.Count, $"抹字面量没让集合变窄：{code.Count} vs {noComments.Count}");
    }

    private static HashSet<string> NamesIn(string text) =>
        Identifier.Matches(text).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);

    private static List<string> LangConstants() =>
        typeof(Lang)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    private static List<string> UnusedConstants()
    {
        var referenced = ReferencedIdentifiers();
        return LangConstants().Where(c => !referenced.Contains(c)).ToList();
    }

    /// <summary>生产判据：**代码引用**面。扫描 <c>src/</c> + <c>tests/</c>（排除 Lang.cs 与本守卫自身）。</summary>
    private static HashSet<string> ReferencedIdentifiers() => CollectIdentifiers(ReferenceFace.Code);

    /// <summary>
    /// 「**有代码引用**」的 <c>Lang</c> 常量名集合 —— 供别的守卫复用。
    ///
    /// <para><b>为什么做成公开接缝</b>：本仓纪律是「守卫与被守对象必须同源」。
    /// 键可达性守卫要收紧 M1（登记 #49：只剩一个常量定义、没人读它，也能替键做担保），
    /// 它需要的就是"哪些常量真的被代码读过"这一条口径 —— 抄第二份必然漂移。</para>
    ///
    /// <para>调用方传**自己那份要排除的文件名**：判据自己的文件里列着符号名，
    /// 而"判据的文档/清单被算成引用"正是 #48 那个坑（本方法已排除本文件自身）。</para>
    /// </summary>
    internal static HashSet<string> CodeReferencedConstants(params string[] extraExcludedFiles)
    {
        var excluded = extraExcludedFiles.ToHashSet(StringComparer.Ordinal);
        var referenced = CollectIdentifiers(ReferenceFace.Code, excluded);
        return LangConstants().Where(referenced.Contains).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// 收集标识符。<paramref name="extraExcludedFiles"/> = 调用方额外要排除的文件名（见上）。
    /// </summary>
    private static HashSet<string> CollectIdentifiers(ReferenceFace face, IReadOnlySet<string>? extraExcludedFiles = null)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var roots = new[]
        {
            Path.Combine(SourceScanner.RepoRoot, "src"),
            Path.Combine(SourceScanner.RepoRoot, "tests"),
        };
        foreach (var root in roots)
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            if (SourceScanner.IsBuildOutput(file)) continue;
            var ext = Path.GetExtension(file);
            if (ext != ".cs" && ext != ".axaml") continue;
            var name = Path.GetFileName(file);
            if (name == "Lang.cs" || name == SelfFile
                || (extraExcludedFiles is not null && extraExcludedFiles.Contains(name))) continue;
            foreach (var n in NamesIn(ForFace(face, file, File.ReadAllText(file))))
                set.Add(n);
        }
        return set;
    }

    /// <summary>
    /// 按「取法」把一段文本变成该面的可观测量。<see cref="ReferenceFace.Code"/> 用
    /// <see cref="SourceScanner.MaskLiterals"/>：它剥注释、抹字面量内容，**保留插值孔**
    /// —— 孔里是代码（<c>$"…{Lang.X}…"</c> 这种引用一旦被整串抹掉就会把活常量判成死的）。
    /// <c>.axaml</c> 走 <see cref="SourceScanner.StripXmlComments"/>（两种语言同一条道理）。
    /// </summary>
    private static string ForFace(ReferenceFace face, string path, string text)
    {
        if (face == ReferenceFace.Raw) return text;
        var isCSharp = Path.GetExtension(path) == ".cs";
        return face == ReferenceFace.Code && isCSharp
            ? SourceScanner.MaskLiterals(text)
            : isCSharp ? SourceScanner.StripComments(text) : SourceScanner.StripXmlComments(text);
    }
}
