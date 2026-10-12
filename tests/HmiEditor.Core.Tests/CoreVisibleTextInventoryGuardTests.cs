using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **Core 侧用户可见文案台账 + 只减不增守卫**（阶段 44）。
///
/// <para><b>为什么需要它</b>：<c>HardcodedLocalizationGuardTests</c> 的扫描面只有
/// <c>src/HmiEditor.App</c>（那份清单早已清零）。<c>HmiEditor.Core</c> 里同样有文案会**
/// 直接显示在编辑器界面上**，却完全在守卫之外 —— 阶段 42 只覆盖了脚本校验这一条通道，
/// 其余「未盘点」。本文件就是那次盘点，并把结论固化：**新写死的界面文案立刻红**。</para>
///
/// <para><b>为什么不直接照搬 App 守卫的规则</b>：Core 是库，同样含中文字面量，
/// 但绝大多数**不是界面文案** —— 生成器往 <c>.c/.h</c> 里写的注释、设备端宏说明、
/// 资源库预设条目、主题名、审计落盘记录……直接套 App 那套规则会得到 1000+ 条假阳性。
/// 因此这里是**先分类、再计数**：排除类别按"这条字面量从哪个出口走"判定，
/// 而不是按"它是中文"判定。</para>
///
/// <para><b>排除类别（每条都有"为什么用户看不到 / 不该翻"的理由）</b>：</para>
/// <list type="bullet">
/// <item><b>生成产物文本</b>（<see cref="GeneratedFile"/> / <see cref="GeneratedWrite"/>）：
/// <c>CodeGen/</c>、<c>SimulatorProjectExporter*</c>、<c>*CodeGen.cs</c> 里的字面量，
/// 以及任何走 <c>sb.AppendLine</c> / <c>Cmt(</c> / <c>printf(</c> / <c>fprintf(</c> /
/// <c>message(</c> 的字面量 —— 它们写进 <c>main.c</c> / <c>generated_*.h</c> /
/// 导出工程的 <c>README.md</c> / CMake 脚本。<b>必须固定中文</b>：产物可复现性要求
/// 同一工程两次导出逐字节一致，跟着编辑器 UI 语言变会把 A/B 归一化直接搞坏
/// （见 <c>tools/ReproHarness</c>）。</item>
/// <item><b>日志 / 调试出口</b>（<see cref="LogSink"/>）：<c>Log?.Invoke</c> /
/// <c>Debug.WriteLine</c> / <c>Console.</c> / <c>Trace.</c>。Core 侧 <c>Log</c> 的**唯一**
/// 编辑器订阅点是 <c>PreviewRuntimeService</c>（<c>RuntimeBindingEngine</c> / <c>ActionExecutor</c>），
/// 其出口 <c>DiagnosticLog</c> 在 <c>MainWindow</c> **不注入** ⇒ 应用里只进 Debug；
/// 其余服务（AlarmEngine / DataLogger / RecipeManager / ScriptEngineService）在 App 里
/// <b>根本没有订阅点</b>（<c>grep "Log =" src/HmiEditor.App</c> 只剩 SimulatorView 与 PreviewRuntimeService）。
/// 同一条实测也写在 App 守卫的 <c>DiagnosticLog</c> 条目里。</item>
/// <item><b>开发者消息</b>（<see cref="DevMessage"/>）：<c>throw new</c> / <c>new</c> + **BCL 自带的
/// 异常类型** —— 面向开发者，翻译后反而无法搜索。<b>我们自己的异常类型不计入这一类</b>：
/// 它们的 <c>Message</c> 是字典缺键时显示的**回落文案**（阶段 81 用一张
/// <c>HmiPackageException</c> 类型名白名单打的补丁；阶段 91 把口径反过来，
/// 只认 BCL 类型、白名单删除 —— 见 <see cref="DevMessage"/>）。</item>
/// <item><b>正则模式</b>（<see cref="RegexPattern"/>）：那是**模式**不是文案，
/// 翻译它会直接改变匹配行为。</item>
/// </list>
///
/// <para><b>两条基线</b>：剩下的按"**是否已核实会到界面**"分两桶，都只减不增。</para>
/// <list type="bullet">
/// <item><see cref="VisibleChannels"/>：**已核实**会显示在编辑器界面上的 15 条通道（101 条文案）。
/// ⚠ 「6 条 / 61 条」这个读数停在阶段 44 定版那一刻 —— 之后阶段 87（SvgParseError）/
/// 88（CsvVariableImporter）/ 89（GeneratedCodeValidator）/ 91（ExpressionSyntaxError）/
/// 93（DanglingRefLocation）各自新增过通道，都没回头改这句。阶段 93 顺手归位为 **10 / 72**，
/// 阶段 94（#61m）归位为 **11 / 85**，阶段 97（#61d）为 **12 / 87**，
/// 阶段 98（#61t）为 **13 / 89**（通道 +1、条数 +2 —— `Services/RecipeIssue.cs` 新通道 2 条；
/// 来历的差异写在下面那条判据的文档注释里）。
/// 阶段 99（#61v）为 **14 / 94**（通道 +1、条数 +5 —— `Services/LoginIssue.cs` 新通道 5 条）。
/// 阶段 100（#61f）为 **15 / 100**（通道 +1、条数 +6 —— `Services/RecipeImportIssue.cs` 新通道 6 条；
/// 与阶段 97/98/99 **同源：新造**，不是搬迁也不是归位）。
/// 阶段 124（#66②）为 **15 / 101**（**通道 ±0**、条数 +1 —— 既有通道
/// `Services/AuthenticationService.cs` 新增一条审计详情 `变更显示名: {old} → {next}`）。
/// ⚠ 这一条与本链前七次**形状不同**：前七次全是"新增一个失败原因通道"（`ExternalText` 那一族），
/// 这一次是**审计口吻的短句**进了**已有的**审计日志通道（与同文件的 `启用/禁用账户` /
/// `变更角色: a → b` 完全同形）⇒ 通道数不动、条数 +1。
/// 🩸 它是**门禁实测**点名出来的（`NoUnclassifiedCoreChinese`：登记 12、实扫 13），不是预估。
/// ⚠ 阶段 105 **没动这条链**：它改的是 `Pending` 桶（`Serialization/HmiPackageService.cs` 7 → 9）。
/// 两个桶**只差一个字段名**（`VisibleChannels` 是 `Channel[]`、`Pending` 是 `Dictionary<string,int>`），
/// 照着"改了两条文案 ⇒ 数字 +2"去改这条链，`Assert.Equal(100, sum)` 当场红 —— 本阶段真踩过。
/// ⚠ 阶段 99 **顺手纠正**：阶段 98 那一行当年把这次改动写成「**通道数不变**、条数 +2」，
/// 而它实际上**加了通道** ⇒ 从那以后这条链的通道数**一直少 1**（行首那句「…条通道」同理）。
/// 数字是给人读的 —— **没判据看着就会漂**，所以本阶段把它钉在 <see cref="VisibleChannels"/> 上
/// （见 `VisibleChannels_CountIsWhatTheDocSays`，它同时管着盘点专题 §0 那句）。
/// 这条与本阶段缺陷无关，同属
/// 「文档里的数字要能指到一条日志」这条纪律。
/// 每条都登记了消费方文件与一句**必含标记**，并有判据检查该文件仍在、标记仍在 ——
/// 防止"我说它到界面"这句话随时间腐烂成一句无据之言。</item>
/// <item><see cref="Pending"/>：尚未定性的（领域显示名 / 落盘数据 / 只进 Debug / 无消费方 …）。
/// 逐文件登记，注释里写明它属于哪一类、证据是什么。</item>
/// </list>
///
/// <para><b>维护方式</b>：本地化掉一批 ⇒ **把对应文件的数字改小**（相等判定，改小了才对得上）；
/// 新增一条未分类的界面文案 ⇒ 这里直接红。若确认某条属于上面的排除类别，
/// 应改进分类规则而不是调数字。</para>
/// </summary>
public class CoreVisibleTextInventoryGuardTests
{
    private const string CoreRootRelative = "src/HmiEditor.Core";

    // ────────────────────────── 排除类别 ──────────────────────────

    private static readonly Regex GeneratedFile = new(
        @"^(CodeGen/|Services/SimulatorProjectExporter|Services/CommConfigCodeGen\.cs)",
        RegexOptions.Compiled);

    /// <remarks>
    /// 接收者**必须**是 <c>sb</c>：只写 <c>AppendLine</c> 会连 CSV 导出的列名一起放过
    /// （<c>DataLogger</c> / <c>RecipeManager</c> / <c>CsvVariableImporter</c> 都在用 <c>AppendLine</c>，
    /// 而 CSV 列名是落盘数据、不是生成产物）。**排除是漏报方向**，宁可窄。
    /// </remarks>
    private static readonly Regex GeneratedWrite = new(
        @"sb\.AppendLine|Cmt\(|printf\(|fprintf\(|message\(|Raw\(",
        RegexOptions.Compiled);

    private static readonly Regex LogSink = new(
        @"Log\?\.Invoke|Debug\.WriteLine|Console\.|Trace\.",
        RegexOptions.Compiled);

    /// <summary>BCL 异常类型的**前缀**（正则里接 <c>Exception\b</c>）。</summary>
    /// <remarks>
    /// 新增 BCL 类型而忘了补一行时，方向是**报红**（那条字面量被计入台账），看得见；
    /// 而口径反转前的方向是**静默豁免**。同族两害相权，取前者。
    /// </remarks>
    private const string BclExceptionPrefixes =
        "Argument|ArgumentNull|ArgumentOutOfRange|Aggregate|Application|InvalidProgram"
        + "|InvalidOperation|NotSupported|NotImplemented|PlatformNotSupported|Format|InvalidCast"
        + "|IO|FileNotFound|DirectoryNotFound|PathTooLong|Json|KeyNotFound|IndexOutOfRange"
        + "|Overflow|ObjectDisposed|Timeout|UnauthorizedAccess|OperationCanceled|TaskCanceled"
        + "|InvalidData|EndOfStream|Xml|Cryptographic|Security|TypeInitialization";

    /// <summary>
    /// **开发者消息**：BCL 自带的异常类型 + 中文串 —— 面向开发者，翻译后反而无法搜索。
    ///
    /// <para><b>为什么要列出类型、而不是笼统的 <c>throw new|Exception\(</c></b>（阶段 91 收紧）：
    /// 旧写法把**自有异常**也一并豁免 —— 于是同一个文件里同族的两条回落文案，
    /// 只因写法不同就被分到两个类别（阶段 81 实测：写成 <c>ReadJsonEntry(..., "中文")</c>
    /// 的实参计入，写成 <c>throw new HmiPackageException(code, "中文")</c> 的被吞）。
    /// 阶段 81 的补丁是一张 <c>HmiPackageException</c> 的**类型名白名单**，
    /// 但那是"下次记得加我"的形状：再出现一个面向用户的自有异常，
    /// 它的回落文案会**静默**地从台账里消失（登记 #54 的病）。
    /// 反转之后规则变成「**只有 BCL 类型才算开发者消息**」——
    /// BCL 异常是框架为**编程错误**抛的、消息里没有业务回落文案；
    /// 我们自己的异常类型恰恰相反（<c>HmiPackageException</c> /
    /// <c>ExpressionSyntaxException</c> 的 <c>Message</c> 是字典缺键时要显示的回落原文）。</para>
    ///
    /// <para><b>实测口径（阶段 91，改前改后各跑一遍）</b>：Core 里"自有类型 + 中文实参"的抛错
    /// 只有 6 处（**阶段 91 的读数** —— **阶段 105** 在该文件又加了 2 处包失败回落，
    /// 本读数**未重量**、别当现状引用）——
    /// <c>Serialization/HmiPackageService.cs</c> 的 5 处 <c>HmiPackageException</c>
    /// （阶段 81 起就在台账里，改口径前后**都计入**）与
    /// <c>Security/RuntimePasswordHasher.cs</c> 的 1 处 <c>ArgumentOutOfRangeException</c>
    /// （BCL 类型 ⇒ 照旧豁免）⇒ <b>台账数字一个没动</b>。
    /// 真正涨的是**记录体里的 6 条**（<c>Services/ExpressionSyntaxError.cs</c>）——
    /// 它们本来就不是 <c>throw new</c> 的形状，与本反转无关，是 #61i 落地时"搬出 `throw new`"的副产品。</para>
    /// </summary>
    private static readonly Regex DevMessage = new(
        @"(?:throw new\s+|new\s+)(?:" + BclExceptionPrefixes + @")Exception\b",
        RegexOptions.Compiled);

    private static readonly Regex RegexPattern = new(@"Regex", RegexOptions.Compiled);

    // ⚠ 阶段 91 删掉了 `UserFacingException` 那张类型名白名单：它的职责已被
    //   DevMessage 的新口径**收编**（"只有 BCL 类型才算开发者消息" ⇒ HmiPackageException
    //   本来就落在计入侧，不需要专门捞回来）。留着它就是又一条"下次记得加我"的抄写。

    private static readonly Regex Cjk = new(@"[\u4e00-\u9fff]", RegexOptions.Compiled);

    /// <summary>与 App 守卫同一条：普通字符串字面量（逐字/原始字符串走同一正则的近似匹配）。</summary>
    private static readonly Regex Literal = new("\"((?:[^\"\\\\\n]|\\\\.)*)\"", RegexOptions.Compiled);

    // ────────────────────────── 通道台账（已核实到界面）──────────────────────────

    /// <summary>
    /// 一条「Core 产出 ⇒ 编辑器界面显示」的通道。
    /// </summary>
    /// <param name="File">Core 侧产出该文案的文件（相对 <c>src/HmiEditor.Core</c>）。</param>
    /// <param name="Count">该文件里**已被核实会到界面**的中文字面量条数。</param>
    /// <param name="Consumer">消费方（相对仓库根）—— 判据会检查它存在且含 <paramref name="Marker"/>。</param>
    /// <param name="Marker">消费方里那句"把它显示出来"的代码标记。</param>
    public sealed record Channel(string File, int Count, string Consumer, string Marker);

    /// <summary>
    /// 已核实的通道。<b>每一行的"消费方 + 标记"都是判据</b>：文件没了、或那句显示代码改了，
    /// 这里就红 —— 因为那时"它到界面"的结论需要重新取证，而不是继续当真。
    /// </summary>
    public static readonly Channel[] VisibleChannels =
    {
        // 生成代码验证结果对话框（阶段 89 / 登记 #61g）：Message 与 Location 以前是
        // `Text = finding.Message` / `Text = finding.Location` **直显**；现在两条都走
        // `ExternalText` 的码化出口 ⇒ 消费标记落在**出口**那一层
        // （与阶段 87 的 SvgParseError / 阶段 88 的 CsvVariableImporter 同一形状）。
        // ⚠ 23 **一条都没少**：18 条消息 + 5 条定位的中文**按设计必须留在 Core**
        //   （它们是字典缺键时的回落原文）—— 变的是它们**到用户眼里的路径**，不是数量。
        new("Services/GeneratedCodeValidator.cs", 23,
            "src/HmiEditor.App/Services/ExternalText.cs", "GenIssueKey(finding.Code)"),

        // 交叉引用面板：这 9 条中文以前由 XAML 的 `Text="{Binding Description}"` **直显**。
        // ⚠ 阶段 93（#61l）从 13 降到 **9**：那 4 条 `[屏名] 控件 '名' …` **不是**面板文案
        //   （面板显示的是 `ReferenceLocation.Description`，另一套措辞），而是给
        //   `GeneratedCodeFinding.Location` 用的定位串 ⇒ 它们搬去了
        //   `Services/DanglingRefLocation.cs`（见下面那条新通道）。
        // ⚠ 阶段 95（#61n）**9 条一条没少**，但到用户眼里的路径整体换了：动作 2 条走
        //   `ActionTextKeys`（阶段 94，#61m），其余 7 条走 `ReferenceTextKeys`
        //   （本阶段，#61n）—— 模板里的 `{Binding Description}` 现在绑的是 `ReferenceRow`
        //   的转发属性，已不是"直显"。⇒ 消费标记与阶段 89/93 同形，落到**出口那一层**。
        //   这是一次**路径替换**、不是条数变化（与阶段 93 的"搬迁"、阶段 94 的"归位"都不同）。
        new("Services/CrossReferenceAnalyzer.cs", 9,
            "src/HmiEditor.App/Services/ReferenceTextKeys.cs", "Lang.CrossRefDescScreenSwitch"),

        // 悬空引用的**定位串**（阶段 93，闭环 #61l）：这 3 条中文是**从上一条搬过来的**
        // （`CrossReferenceAnalyzer` 13 → 9 少的正是它们）。改动前它们是
        // `FindDanglingReferences` 拼的中文串、由调用方**原样直出**（`LocationCode` 留空）
        // ⇒ 英文界面下这一半中英混排；现在它们是 `DanglingRefLocation` 记录体的
        // **回落原文**，正常路径走 `gen.loc.*`。消费标记落在出口那一层
        // （与 SvgParseError / CsvVariableImporter / ExpressionSyntaxError 同形）。
        // ⚠ 为什么单列一条通道、不并进 `GeneratedCodeValidator`：**产出方不同** ——
        //   定位码由本文件的三个工厂方法发（`OfBinding` / `OfLegacyBinding` / `OfAction`）。
        //   顺带更正登记里的一处表述：那 4 条与这里的 3 条**不是同一批**字面量
        //   （前者是面板措辞 `控件 名 绑定变量 (方向)`）⇒「同一串在两个地方露出」**不成立**。
        new("Services/DanglingRefLocation.cs", 3,
            "src/HmiEditor.App/Services/ExternalText.cs", "DescribeLocation(GeneratedCodeFinding"),

        // 动作展示名（阶段 94，闭环登记 #61m）：`HmiAction.GetDescription()` 的 13 条中文。
        // ⚠ 这批字面量**改动前就在 Core 里**，但它在 Pending 里被错归成「DATA：落盘数据」——
        //   理由是"翻它会让工程文件随 UI 语言变"，而**实测无一条落盘**（落盘的是
        //   `ActionType` / `Target` / `Value` 这些 Id）⇒ 与登记 #54 同病：豁免表里的"理由"
        //   没人去代码里核，它就变成一个能把缺陷合法化的字段。
        // ⚠ 这 13 条现在是**回落原文**（正常路径走 App 的 `ActionTextKeys.Describe`）⇒
        //   与 `Services/SvgParseError.cs` 那条同形，所以消费标记落在**出口**那一层。
        // ⚠ 同一个方法还有**第二个方向**：`CodeGenerator.Actions.cs` 三处 `Cmt(` 把它写进产物注释，
        //   那一侧**必须固定中文**（产物可复现性）⇒ 这 13 条同时是产物文本，但**产物侧不计入本表**
        //   （本表记的是"到界面"）。
        new("Models/HmiAction.cs", 13,
            "src/HmiEditor.App/Services/ActionTextKeys.cs", "Lang.ActionDescSwitchScreen"),

        // 审计日志面板：订阅 AuditLogged，列表项显示 AuditLogEntry.Detail。
        // 阶段 124（#66②）：12 → **13** —— 新增一条 `UpdateUserDisplayName` 的审计详情
        // `变更显示名: {old} → {next}`。⚠ 它**与上面那些失败原因不同**：不走 `ExternalText`、
        // 也不是回落原文，而是与 `SetUserEnabled`（`启用/禁用账户`）、`ChangeUserRole`
        // （`变更角色: a → b`）**完全同形**的审计口吻短句 ⇒ 同一个消费通道（审计日志面板）。
        // 这正是本表要求"新增界面文案要么本地化、要么登记"的那一类：它是**给审计看的**，
        // 不随 UI 语言变（落盘 JSONL、要能跨语言对账）。
        new("Services/AuthenticationService.cs", 13,
            "src/HmiEditor.App/Views/AuditLogView.axaml.cs", "AuditLogEntry"),

        // 脚本运行输出：`result.Error` 与 `result.Log` 都拼进 TxtRunOutput。
        new("Services/ScriptSandboxExecutor.cs", 7,
            "src/HmiEditor.App/Views/ScriptEditorView.axaml.cs", "result.Error ?? \"\""),

        // SVG 解析失败：错误串被当作**参数**塞进已本地化的模板 ⇒ 英文界面下是中英混排。
        // 消费方于 2026-09-21（阶段 54「只留总线」，缺陷 #46）从 Wiring.Canvas.cs 移到
        // Wiring.DockAggregate.cs —— 宿主对资产面板实例的直订已删除，SvgApplyRequested 只剩总线一路。
        new("Services/SvgParser.cs", 3,
            "src/HmiEditor.App/Wiring.DockAggregate.cs", "asset.svg.parse.fail"),

        // SVG 解析失败的**回落原文**（阶段 87 新增的记录体）。`SvgParseError.Message` 只在
        // 字典缺 `svg.issue.*` 时冒头 —— 那条路正是 `ExternalText.Describe` 的第二实参，
        // 所以消费标记落在那句话上（与上一条同族：都是"中文串当参数进已本地化模板"）。
        new("Services/SvgParseError.cs", 1,
            "src/HmiEditor.App/Services/ExternalText.cs", "SvgIssueKey(error.Code)"),

        // CSV 变量导入的失败原因（阶段 88 新增的记录体，登记 #61f）：5 条中文回落原文
        // 由 `ExternalText.Describe(CsvImportIssue)` 渲染成界面文本。改动前它们只是
        // `ImportResult.Errors` 里的裸中文串，而两个消费方**一个只取条数、一个整块丢弃**
        // ⇒ 那时它们在这张表的 Pending 里（归类 LATENT），**到不了界面**。
        new("Services/CsvVariableImporter.cs", 5,
            "src/HmiEditor.App/Services/ExternalText.cs", "CsvIssueKey(issue.Code)"),

        // Git 提交失败：同上，`error` 作为参数进 status.git.commit.failed。
        new("Services/GitWorkflowService.cs", 3,
            "src/HmiEditor.App/Services/GitDialogService.cs", "status.git.commit.failed"),

        // 表达式语法失败的**回落原文**（阶段 91 新增的记录体，登记 #61i）：6 条中文回落
        // 由 `ExternalText.Describe(ExpressionSyntaxError)` 渲染（属性面板表达式输入框下方的红字），
        // 并作为 `gen.issue.expression_syntax` 的 `{1}` 实参**嵌套渲染**。
        // ⚠ 这 6 条**改动前就在 Core 里**（写法是 `throw new FormatException("缺少右括号 ')'")`），
        //   只是被 DevMessage 的 `throw new` 豁免挡在台账之外 —— 本阶段把口径反过来
        //   （见 DevMessage 的注释），于是它们**第一次**出现在这张表上。
        //   这不是"新增了 6 条界面文案"，而是"6 条一直在的界面文案第一次被看见"。
        new("Services/ExpressionSyntaxError.cs", 6,
            "src/HmiEditor.App/Services/ExternalText.cs", "ExprSyntaxKey(error.Code)"),

        // 用户管理失败的**回落原文**（阶段 97 新增的记录体，登记 #61d）：2 条中文回落
        // 由 `ExternalText.FailureReason` 的 ②′ 支渲染（`Resolve(UserIssueKey(...))`），
        // 与 `Services/SvgParseError.cs` / `Services/CsvVariableImporter.cs` /
        // `Services/ExpressionSyntaxError.cs` 同形 —— 都是「码在 Core、键在 App、
        // 字典缺键才回落中文」。⚠ 与阶段 91 的那两条**来源不同**：这 2 条是**本阶段新造的**
        // （改动前那两处抛的是裸 `ArgumentException`，不计入本表）⇒ Core 里的中文**真的多了 2 条**。
        new("Services/UserAdminIssue.cs", 2,
            "src/HmiEditor.App/Services/ExternalText.cs", "UserIssueKey(userAdmin.Issue.Code)"),

        // 配方克隆失败的**回落原文**（阶段 98 新增的记录体，登记 #61t）：2 条中文回落
        // 由 `ExternalText.Describe(RecipeCloneIssue)` 渲染（`Resolve(RecipeIssueKey(...))`），
        // 与上面 `Services/UserAdminIssue.cs` / `Services/SvgParseError.cs` /
        // `Services/CsvVariableImporter.cs` / `Services/ExpressionSyntaxError.cs` 同形 ——
        // 都是「码在 Core、键在 App、字典缺键才回落中文」。
        // ⚠ 与阶段 97 的那条**来源相同**（都是**本阶段新造的**回落文案，不是搬迁、不是归位）：
        //   改动前 `CloneRecipe` 失败走 `return null!`，**根本没有任何文案**（界面一声不响）
        //   ⇒ Core 里的中文**真的多了 2 条**。
        // ⚠ 这一族是**返回值档**（`RecipeCloneResult`）⇒ 消费标记是 `Describe` 那一支的
        //   `RecipeIssueKey(issue.Code)`，**不是** `FailureReason` 的 `UserIssueKey(...)`。
        new("Services/RecipeIssue.cs", 2,
            "src/HmiEditor.App/Services/ExternalText.cs", "RecipeIssueKey(issue.Code)"),

        // 登录失败的**回落原文**（阶段 99 新增的记录体，登记 #61v）：5 条中文回落
        // 由 `ExternalText.Describe(LoginIssue)` 渲染（`Resolve(LoginIssueKey(...))`），
        // 与上面 `Services/RecipeIssue.cs` / `Services/UserAdminIssue.cs` /
        // `Services/SvgParseError.cs` / `Services/CsvVariableImporter.cs` /
        // `Services/ExpressionSyntaxError.cs` 同形 —— 都是「码在 Core、键在 App、
        // 字典缺键才回落中文」。
        // ⚠ 与阶段 97/98 那两条**来源相同**（都是**本阶段新造的**回落文案，不是搬迁、不是归位）：
        //   `AuthenticationService` 里原本只有**审计口吻的短句**（`账户已锁定` /
        //   `密码错误 (n/m)` / `用户不存在: X`），那些是给审计日志看的、不是给用户看的失败原因；
        //   而界面侧原本只有一句写死的中文（`login.err`，五档共用）⇒ 这 5 句是**新写的**。
        //   （界面侧那句随 `login.err` 与本阶段一起从两本字典退场。）
        // ⚠ 这一族是**返回值档**（`LoginResult`）⇒ 消费标记是 `Describe` 那一支的
        //   `LoginIssueKey(issue.Code)`，**不是** `FailureReason` 的任何一支。
        new("Services/LoginIssue.cs", 5,
            "src/HmiEditor.App/Services/ExternalText.cs", "LoginIssueKey(issue.Code)"),

        // 配方 CSV 导入问题的**回落原文**（阶段 100 新增的记录体，登记 #61f 的第三刀）：6 条中文回落
        // 由 `ExternalText.Describe(RecipeImportIssue)` 渲染（`Resolve(RecipeImportIssueKey(...))`），
        // 与上面 `Services/LoginIssue.cs` / `Services/RecipeIssue.cs` / `Services/UserAdminIssue.cs` /
        // `Services/SvgParseError.cs` / `Services/CsvVariableImporter.cs` /
        // `Services/ExpressionSyntaxError.cs` 同形 —— 都是「码在 Core、键在 App、字典缺键才回落中文」。
        // ⚠ 与阶段 97/98/99 **来源相同**（都是**本阶段新造的**回落文案，不是搬迁、不是归位）：
        //   改动前 `ImportCsv` 的 7 处静默里连一句可读的失败原因都没有（`return 0` / `continue` 了事）
        //   ⇒ Core 里的中文**真的多了 6 条**。
        // ⚠ 这一族是**返回值档**（`RecipeImportResult`）⇒ 消费标记是 `Describe` 那一支的
        //   `RecipeImportIssueKey(issue.Code)`，**不是** `FailureReason` 的任何一支，
        //   也**不是**上一条 `Services/RecipeIssue.cs` 的 `RecipeIssueKey(...)`（两族键前缀不同：
        //   `recipe.import.issue.` vs `recipe.issue.`）。
        new("Services/RecipeImportIssue.cs", 6,
            "src/HmiEditor.App/Services/ExternalText.cs", "RecipeImportIssueKey(issue.Code)"),
    };

    // ────────────────────────── 待定性台账 ──────────────────────────

    /// <summary>
    /// 扣掉排除类别与已核实通道后**剩余**的计数。<b>只减不增，且相等判定</b>
    /// （数字既不能变大，也不能"忘了跟着改小"）。每条的归类理由见行内注释。
    /// </summary>
    public static readonly Dictionary<string, int> Pending = new(StringComparer.Ordinal)
    {
        // 领域数据（DATA）：拖进工程后落盘/进产物，翻它会让工程文件随 UI 语言变。
        ["Services/PresetResourceLibrary.cs"] = 357,   // 资源库预设条目（Name/Description/Category/SubCategory）
        ["Models/SystemVariable.cs"] = 34,             // 系统变量名与描述（变量面板）
        ["Models/StylePreset.cs"] = 15,                // 主题名与描述（主题选择器）
        // 动作类型的中文名。⚠ 阶段 94（登记 #61m）之前它写的是「动作元数据（图标/分组/展示名）」，
        // 而那是**两处共用**的真相（属性面板 + 代码生成）；拆走后只剩**生成产物**一个方向
        // （`CodeGenerator.Actions.cs` 的权限门注释与 `SecurityConfigCodeGen.cs` 的动作→权限表），
        // 属上面那条 GEN 类的理由。
        ["Design/ActionTypeMeta.cs"] = 12,
        ["Models/Animation/AnimationPathType.cs"] = 8, // 缓动曲线展示名
        ["Models/Scripting/ScriptTemplate.cs"] = 4,    // 脚本模板描述
        // "Models/User/UserRole.cs" 的 3 条**已于阶段 66 清退** —— 登记 #54：原归类
        // 「数据（DATA）：落盘角色表的输入」在代码里不成立：生成头 `HMI_SEC_ROLES_INIT`
        // 只发射 Id/Name/Level/掩码（SecurityConfigCodeGen.AppendRoles），描述**既不下发**
        // 也**不落盘**（SecurityConfig 里根本没有 Roles 字段，角色只在内存里由
        // AuthenticationService 建）⇒ 那 3 条其实是**该翻而没翻的界面文案**
        // （用户管理→角色列表与角色详情的只读展示，RoleNameBox 还是 IsReadOnly）。
        // 处置 = 搬进字典 role.*.desc（消费方 RoleTextKeys.DisplayDescription）。
        ["Models/Alarm/VariableAlarmConfig.cs"] = 1,   // 落盘默认名 "变量报警-{variableId}"
        ["Models/ControlTemplate.cs"] = 1,             // 分类名 "通用"
        ["Models/TableLayoutHelper.cs"] = 1,           // 布局模式名 "自适应"

        // 只进 Debug / 无订阅点（DEV）：看起来像界面提示，实测到不了。
        ["Design/DefaultPropertyValidator.cs"] = 10,   // PropertyPanelView.Changes.cs:61 —— 只 Debug.WriteLine 后 return
        ["Models/VariableManager.cs"] = 4,             // MainWindow.axaml.cs:1251 —— SuggestedFix 只 Debug.WriteLine
        ["Services/LocalizationService.cs"] = 2,       // 1 条 Debug 诊断 + 1 条语言显示名
        ["Services/LocalizationPlaceholders.cs"] = 1,  // 诊断文本 "无占位符"

        // 数据在、但当前**没有产品消费方**（LATENT）：留给将来接的时候一并本地化。
        // 16 -> 11（阶段 88，登记 #61f）：那 5 条**错误文案**已码化并接上渲染出口，
        // 搬到上面的 VisibleChannels；剩下的 11 条是 `BuildColumnMap` 的同义词表
        // （"变量名" / "数据类型" / "地址" …）—— 它们是**匹配用的键**、不是给人看的文案，
        // 所以按定义就该留在这里。
        // ⚠ 上一版这条的理由写的是「两个消费方都只显示 Errors.Count」——
        //   对 `CommunicationConfigView` **不成立**（它连 Errors 都不读）。同一类病见 #54：
        //   豁免表里的"理由"没人去代码里核，它就变成一个能把缺陷合法化的字段。
        ["Services/CsvVariableImporter.cs"] = 11,
        ["Services/ActionExecutor.cs"] = 18,           // PreviewRuntimeService 只取 bool；消息经 Log → DiagnosticLog（未注入）
        ["Services/RecipeManager.cs"] = 2,             // ValidateRecipe 只在测试里被调用

        // 消费方是**生成器**：最终进生成代码的注释（GEN），不是编辑器界面。
        ["Services/ScreenLayerRefs.cs"] = 4,           // CodeGenerator.Screen.cs:410 Cmt(sb, "// WARNING: 图层引用校验失败 …")

        // 已由阶段 42 的字典通道覆盖：Message 是回落文案，界面走 script.issue.<code>。
        ["Services/LuaSyntaxValidator.cs"] = 8,
        ["Services/ScriptEngineService.cs"] = 3,

        // 已由阶段 81 的字典通道覆盖（闭环登记 #59）：Core 不引本地化，`HmiPackageException`
        // 的 Message 是**回落文案**（字典缺键时才显示），界面正常走 package.issue.<code>。
        // 消费点：src/HmiEditor.App/Services/ExternalText.cs 的 FailureReason（码 ⇒ 键 ⇒ 字典）。
        // 注：这 7 条**本来就该计入**——阶段 81 曾用一张 `HmiPackageException` 类型名白名单
        // 把它们从 `throw new` 豁免里**捞回来**；阶段 91 把口径反过来（只有 BCL 类型才算
        // 开发者消息，见 DevMessage），白名单随之删除 ⇒ **结论不变、失效方向反了过来**。
        // 本文件实测有 10 条同族文案 —— 阶段 105 给**中途失败**又补了 2 条回落
        // （`RESLIB_INVALID` / `ASSET_EXTRACT_FAILED`，登记 #62：`CreateDirectory` 之后的失败
        // 原先既无码也无判据）。另一条是 `throw new ArgumentException("资产目录不能为空")`，
        // 那是**编程错误**（不是回落文案），照旧留在 DevMessage 豁免里。
        ["Serialization/HmiPackageService.cs"] = 9,

        // 内部表：方言特性/符号对照，供编辑器自身判定用（其中进界面的提示走 LuaDialectHint 的 Code）。
        ["Services/LuaDialect.cs"] = 39,
        // 设计令牌与设计稿的"有意分歧"说明，给维护者看的。
        ["Design/DesignTokens.cs"] = 13,
    };

    // ────────────────────────── 扫描 ──────────────────────────

    /// <summary>字面量是否落在排除类别里。与 App 守卫同一套"往回找包裹调用"的判定思路。</summary>
    internal static bool IsExcluded(string file, string code, int literalStart)
    {
        if (GeneratedFile.IsMatch(file)) return true;

        // 往回找包住该字面量的最近一个 '('；已配对的括号跳过，语句/块边界直接停
        var depth = 0;
        var open = -1;
        for (var i = literalStart - 1; i >= 0; i--)
        {
            var c = code[i];
            if (c == ')') depth++;
            else if (c == '(')
            {
                if (depth == 0) { open = i; break; }
                depth--;
            }
            else if (depth == 0 && (c == ';' || c == '{' || c == '}')) break;
        }

        var anchor = open < 0 ? literalStart : open;
        var start = anchor;
        // 边界含 `,`：对象/集合初始化器里的兄弟属性必须切断，否则会继承邻居的豁免
        while (start > 0 && code[start - 1] != ';' && code[start - 1] != '{'
               && code[start - 1] != '}' && code[start - 1] != '(' && code[start - 1] != ',')
            start--;

        var head = code.Substring(start, anchor - start);

        return GeneratedWrite.IsMatch(head)
               || LogSink.IsMatch(head)
               || DevMessage.IsMatch(head)
               || RegexPattern.IsMatch(head);
    }

    /// <param name="ignoreExclusions">
    /// 为 <c>true</c> 时不套用排除类别 —— 只给自检用（见 <c>Scanner_StillWorks_…</c>）。
    /// </param>
    private static Dictionary<string, int> Scan(bool ignoreExclusions = false)
    {
        var root = Path.Combine(SourceScanner.RepoRoot, CoreRootRelative);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                     .Where(p => !SourceScanner.IsBuildOutput(p)))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            // 剥注释用 SourceScanner 的单遍状态机（它认逐字/原始/字符字面量，
            // 且有"长度与行数不变"的不变量测试兜着）—— 不在本文件里再抄一份。
            var code = SourceScanner.StripComments(File.ReadAllText(path));

            var hits = 0;
            foreach (Match m in Literal.Matches(code))
            {
                if (!Cjk.IsMatch(m.Groups[1].Value)) continue;
                if (!ignoreExclusions && IsExcluded(relative, code, m.Index)) continue;
                hits++;
            }

            if (hits > 0) counts[relative] = hits;
        }

        return counts;
    }

    /// <summary>通道台账与待定性台账合并出来的**期望计数**。</summary>
    private static Dictionary<string, int> Expected()
    {
        var expected = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, value) in Pending) expected[key] = value;
        foreach (var ch in VisibleChannels)
            expected[ch.File] = expected.GetValueOrDefault(ch.File) + ch.Count;
        return expected;
    }

    // ────────────────────────── 判据 ──────────────────────────

    [Fact]
    public void Scanner_StillWorks_WhenExclusionsAreDisabled()
    {
        // 仪器自检，不挂在基线上：关掉排除类别再扫一次，还有东西 ⇒ 扫描器活着。
        var raw = Scan(ignoreExclusions: true);

        Assert.True(raw.Count >= 40,
            $"关掉排除项只扫到 {raw.Count} 个文件含中文 —— 扫描器可能失效");
        Assert.True(raw.Values.Sum() >= 900,
            $"关掉排除项只扫到 {raw.Values.Sum()} 条 —— 扫描器可能失效");
    }

    [Fact]
    public void NoUnclassifiedCoreChinese()
    {
        var actual = Scan();
        var expected = Expected();

        var missing = expected.Where(kv => !actual.ContainsKey(kv.Key))
            .Select(kv => $"  - {kv.Key}: 登记 {kv.Value}，实扫 0").ToList();
        var added = actual.Where(kv => !expected.ContainsKey(kv.Key))
            .Select(kv => $"  + {kv.Key}: 登记 0，实扫 {kv.Value}").ToList();
        var changed = actual.Where(kv => expected.ContainsKey(kv.Key) && expected[kv.Key] != kv.Value)
            .Select(kv => $"  ~ {kv.Key}: 登记 {expected[kv.Key]}，实扫 {kv.Value}").ToList();

        Assert.True(missing.Count + added.Count + changed.Count == 0,
            "Core 侧中文字面量台账与实扫不一致。"
            + "新增的**界面文案**要么本地化，要么在 VisibleChannels/Pending 里登记一条并写明归类理由；"
            + "本地化掉一批后请把数字**改小**（相等判定）：\n"
            + string.Join("\n", missing.Concat(added).Concat(changed)));
    }

    /// <summary>
    /// 通道台账的**证据防腐**：说"它会到界面"，就必须指得出消费方文件里那句显示代码。
    /// 文件被删/改名/那句代码被改走 ⇒ 红 —— 此时"到界面"的结论需要重新取证。
    /// </summary>
    [Fact]
    public void VisibleChannels_EvidenceStillExists()
    {
        foreach (var ch in VisibleChannels)
        {
            var path = Path.Combine(SourceScanner.RepoRoot, ch.Consumer.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"通道 {ch.File} 的消费方不存在了：{ch.Consumer}");

            var text = File.ReadAllText(path);
            Assert.True(text.Contains(ch.Marker, StringComparison.Ordinal),
                $"通道 {ch.File} 声称经由 {ch.Consumer} 显示，但那里已经没有标记：{ch.Marker}");
        }
    }

    /// <summary>通道里的文件必须真的在扫描面上，且条数不超过该文件的实扫计数。</summary>
    [Fact]
    public void VisibleChannels_AreCoveredByTheInventory()
    {
        var actual = Scan();

        foreach (var ch in VisibleChannels)
        {
            Assert.True(actual.ContainsKey(ch.File), $"通道文件在 Core 里扫不到：{ch.File}");
            Assert.True(ch.Count > 0, $"通道 {ch.File} 的条数为 0 —— 要么已本地化（请删掉这条通道），要么登记错了");
            Assert.True(ch.Count <= actual[ch.File],
                $"通道 {ch.File} 登记 {ch.Count} 条，但该文件实扫只有 {actual[ch.File]} 条");
        }
    }

    /// <summary>
    /// 通道条数之和就是那张"真缺口"清单的规模 —— 给自己看的，不设阈值。
    ///
    /// <para><b>镜像的是专题 §3 那张活表</b>（不是 §6 的阶段 44 快照）：专题里改了数字、
    /// 这里没跟 ⇒ 红。<b>73</b> 的来历 = 阶段 44 的 61 + 阶段 87 新增的
    /// <c>Services/SvgParseError.cs</c>（1 条：<c>svg.issue.*</c> 的中文回落）
    /// + 阶段 88 新增的 <c>Services/CsvVariableImporter.cs</c>（5 条：<c>csv.issue.*</c>
    /// 的中文回落，登记 #61f）
    /// + 阶段 91 新增的 <c>Services/ExpressionSyntaxError.cs</c>（6 条：<c>expr.syntax.*</c>
    /// 的中文回落，登记 #61i）。
    /// ⚠ 最后一次与上面两次**来源不同**：那两次是"新造出来的回落文案"，
    /// 这一次是"**原本就被 DevMessage 豁免挡在台账之外的**回落文案第一次被看见"
    /// （口径反转，见 <see cref="DevMessage"/>）—— 所以数字涨了、Core 里的中文**一条没多**。
    ///
    /// <para>⇒ 阶段 93（#61l）**73 → 72**：这次是**搬迁**，不是新增也不是减少 ——
    /// 悬空引用的定位串 3 条从 <c>Services/CrossReferenceAnalyzer.cs</c>（13 → 9）
    /// 搬进 <c>Services/DanglingRefLocation.cs</c>（新通道 3 条）⇒ 净 **−1**
    /// （搬走 4、落新 3：那 4 条里 3 条是**去重后的文案**、第 4 条是同一文案的第二个站点）。
    /// ⇒ 「总数变了」不等于「Core 里的中文多了或少了」—— 这是**第三次**这条注释要说明
    /// 数字为什么动（前两次分别是"新造"与"第一次被看见"）。
    ///
    /// <para>⇒ 阶段 94（#61m）**72 → 85**：这一次是**归类归位**，不是新增 ——
    /// <c>Models/HmiAction.cs</c> 的 13 条从 <see cref="Pending"/>（错归 DATA）
    /// 搬进<see cref="VisibleChannels"/>（它们真的到界面）。<b>Core 里的中文字面量一条没多</b>：
    /// 同一个文件改动前实扫 **48** 条、改动后 **13** 条（那 35 条属性面板下拉框的标签
    /// 已随 <c>ActionMeta.DisplayName</c> 拆走、搬进字典）。⇒ 这是**第四次**这条注释要
    /// 说明数字为什么动（前三次见上）。⚠ 与阶段 91/93 的区别：那两次是"新造/搬迁**回落的文案**"，
    /// 这次是"**把误分类的字面量放到正确的那一栏**"。</para>
    ///
    /// <para>⇒ 阶段 97（#61d）**85 → 87**：这一次是**新造**（与阶段 87/88/91 同一来源，
    /// 与阶段 93 的"搬迁"、94 的"归位"都不同）—— `Services/UserAdminIssue.cs` 新通道 2 条：
    /// 那两处改动前抛的是裸 `ArgumentException`（消息是中文，但**不进本表**：本表的扫描面
    /// 只认"到界面"的文案，而裸异常的原文此前既不可达、也无键）⇒ Core 里的中文**真的多了 2 条**。
    /// （生成方式：门禁实测红 `+ Services/UserAdminIssue.cs: 登记 0，实扫 2`，不是预估。）</para>
    ///
    /// <para>⇒ 阶段 98（#61t）**87 → 89**：这一次**与阶段 97 同源**（新造，不是搬迁也不是归位）——
    /// `Services/RecipeIssue.cs` 新通道 2 条。⚠ 但它的来历比阶段 97 更"空"：改动前
    /// `RecipeManager.CloneRecipe` 失败走 `return null!`、消费点连 `else` 都没写
    /// ⇒ 界面上**一个字都没有**（阶段 97 那两处至少有裸中文异常原文可得）。这是**第五次**
    /// 这条注释要说明数字为什么动（前四次见上）。</para>
    ///
    /// <para>⇒ 阶段 99（#61v）**89 → 94**：**与阶段 97/98 同源**（新造，不是搬迁也不是归位）——
    /// `Services/LoginIssue.cs` 新通道 5 条。⚠ 来历要说清：`AuthenticationService` 里原本
    /// 只有**审计口吻的短句**（`账户已锁定` / `密码错误 (n/m)` / `用户不存在: X`），那些是给
    /// 审计日志看的、不是给用户看的失败原因；界面侧原本只有一句**写死的中文**
    /// （`login.err`，五档失败共用）⇒ Core 里这 5 句是**真的新写的**。
    /// 这是**第六次**这条注释要说明数字为什么动（前五次见上）。</para>
    ///
    /// <para>⇒ 阶段 100（#61f）**94 → 100**：**与阶段 97/98/99 同源**（新造，不是搬迁也不是归位）——
    /// `Services/RecipeImportIssue.cs` 新通道 6 条。⚠ 它的来历与阶段 98 一样"空"：
    /// 改动前 `ImportCsv` 的 7 处静默连一句可读的失败原因都没有（`return 0` / `continue` 了事），
    /// 而最要命的那处是**非数字的值静默丢一个配方项**、界面照样报「成功导入 N 个配方」。
    /// 这是**第七次**这条注释要说明数字为什么动（前六次见上）。
    /// （生成方式：门禁实测红 `+ Services/RecipeImportIssue.cs: 登记 0，实扫 6`，不是预估。）</para>
    ///
    /// <para><b>⇒ 阶段 99（#61v）顺手补上"谁看着这些数字"</b>：这条链的**通道数**从阶段 98 起
    /// 就**漂了 1**（那一行把「加了通道」写成「通道数不变」），而**没有任何判据看着它** ——
    /// 于是同一时刻盘点专题 §0 写 **13 条通道**、本文件的行首写 **13 条通道**、
    /// 而 <see cref="VisibleChannels"/> 里**实实在在是 14 条**。数字是给人读的，
    /// 漂了没人提醒 ⇒ 本阶段把两处都钉在**算出来的**值上（下面那个 <c>[Fact]</c>）。
    /// ⚠ 落地前先量：`VisibleChannels.Length` 是 **15**、逐条相加 **100**（真值以表为准，
    /// 不是以哪一处的措辞为准）。</para>
    /// </summary>
    [Fact]
    public void VisibleChannels_CountIsWhatTheDocSays()
    {
        // ⚠ `VisibleChannels` 是**数组** ⇒ 通道数用 `.Length`（`.Count` 在这里会解析成
        // LINQ 的扩展方法 → 方法组 → `CS8917`，编译器当场拦下）。
        var count = VisibleChannels.Length;
        var sum = VisibleChannels.Sum(c => c.Count);
        Assert.Equal(101, sum);

        // ── 文档里的**两个**数字（通道数 / 条数）都必须等于算出来的值（阶段 99 补）──
        // 两处都手抄 ⇒ 必须由判据看着，否则阶段 98 那种「把 +1 通道写成通道数不变」会再发生。
        var problems = new List<string>();

        // ⚠ 两处的**语序不同**（本文件是「通道 / 条数」、盘点专题是「条数 / 通道」）
        //   ⇒ 用**具名组**取值，不靠位置 —— 位置一错这条判据会自己报出一堆假异常
        //   （落地时就踩过：先按位置写，盘点专题那处立刻假红一次）。
        void Check(string what, string text, string pattern)
        {
            var m = Regex.Match(text, pattern);
            if (!m.Success)
            {
                problems.Add($"{what}：那句数字所在的句子找不到了 —— 措辞被改过，本条判据随之失效，"
                    + $"必须跟着改（找的是 {pattern}）");
                return;
            }
            var docCount = int.Parse(m.Groups["c"].Value);
            var docSum = int.Parse(m.Groups["n"].Value);
            if (docCount != count || docSum != sum)
                problems.Add($"{what}：写的是 {docCount} / {docSum}，实测 {count} / {sum}"
                    + "（通道数 / 条数 —— 真值以 VisibleChannels 为准）");
        }

        var self = File.ReadAllText(Path.Combine(SourceScanner.RepoRoot, "tests",
            "HmiEditor.Core.Tests", "CoreVisibleTextInventoryGuardTests.cs"));
        Check("本文件 · 通道摘要句", self,
            @"已核实\*\*会显示在编辑器界面上的 (?<c>\d+) 条通道（(?<n>\d+) 条文案）");

        // 归位链只钉**末段**（历史各段各写各的、不追认）—— 「链的尾巴 = 今天」这一条就够了。
        // ⚠ 标签字符类含**圈码**（`①`…）：本仓的登记号写法就是 `#66②` / `#61d` 两种混用，
        //   为了迁就正则去改登记号会让同一条缺陷在文档里出现两个名字。阶段 124 第一次用到圈码。
        var chain = Regex.Matches(self, @"阶段 \d+（#[0-9a-z①-⑳]+）为 \*\*(?<c>\d+) / (?<n>\d+)\*\*");
        if (chain.Count == 0)
            problems.Add("本文件 · 归位链末段：整条链一句都找不到 —— 措辞被改过，本条判据随之失效");
        else
        {
            var last = (Match)chain[^1];
            var docCount = int.Parse(last.Groups["c"].Value);
            var docSum = int.Parse(last.Groups["n"].Value);
            if (docCount != count || docSum != sum)
                problems.Add($"本文件 · 归位链末段（{last.Value}）：写的是 {docCount} / {docSum}，"
                    + $"实测 {count} / {sum}（通道数 / 条数）");
        }

        var inventory = File.ReadAllText(Path.Combine(SourceScanner.RepoRoot, "docs",
            "2026-09-20-core-visible-text-inventory.md"));
        Check("盘点专题 · §0 摘要句", inventory, @"已核实的真缺口 (?<n>\d+) 条 / (?<c>\d+) 条通道");

        Assert.True(problems.Count == 0,
            "文档里的通道数 / 条数与 VisibleChannels 对不上（改了表就要回来改这两处、反之亦然）：\n"
            + string.Join("\n", problems));
    }

    // ────────────────────────── 判定逻辑自检 ──────────────────────────

    /// <summary>把源码里第一个中文字面量的位置喂给 <see cref="IsExcluded"/>。</summary>
    private static bool ExcludedIn(string file, string source)
    {
        var code = SourceScanner.StripComments(source);
        foreach (Match m in Literal.Matches(code))
            if (Cjk.IsMatch(m.Groups[1].Value))
                return IsExcluded(file, code, m.Index);
        throw new InvalidOperationException("这段源码里没有中文字面量，测不到东西");
    }

    [Fact]
    public void IsExcluded_CoversTheFourDeclaredCategories()
    {
        // 生成产物：写进 .c/.h 的一切
        Assert.True(ExcludedIn("Services/SimulatorProjectExporter.MainC.cs", """sb.AppendLine("/* 中文 */");"""));
        Assert.True(ExcludedIn("Services/Whatever.cs", """sb.AppendLine("/* 中文 */");"""));
        // 日志出口
        Assert.True(ExcludedIn("Services/Whatever.cs", """Log?.Invoke("中文");"""));
        Assert.True(ExcludedIn("Services/Whatever.cs", """System.Diagnostics.Debug.WriteLine("中文");"""));
        // 开发者消息
        Assert.True(ExcludedIn("Services/Whatever.cs", """throw new InvalidOperationException("中文");"""));
        // 正则模式
        Assert.True(ExcludedIn("Services/Whatever.cs", """return new Regex("中文");"""));
    }

    [Fact]
    public void IsExcluded_LeavesRealUserVisibleTextCounted()
    {
        // 正例：这四种都是"到界面"的写法，必须计入
        Assert.False(ExcludedIn("Services/Whatever.cs", """errors.Add("屏幕名重复");"""));
        Assert.False(ExcludedIn("Services/Whatever.cs", """return "项目目录不是 Git 仓库";"""));
        Assert.False(ExcludedIn("Services/Whatever.cs", """var m = $"值 {x} 低于最小值 {y}";"""));
        // 只有**生成器的**写入出口（接收者是 sb）才豁免 ——
        // 普通 AppendLine 是 CSV 导出/日志拼装，它拼出来的中文一样可能落盘或上界面。
        Assert.False(ExcludedIn("Services/Whatever.cs", """list.AppendLine("csv 列名");"""));
        Assert.True(ExcludedIn("Services/Whatever.cs", """sb.AppendLine("/* 生成产物注释 */");"""));
    }

    [Fact]
    public void IsExcluded_DoesNotLeakIntoTheNextStatement()
    {
        const string src = """
            Log?.Invoke("日志里的中文");
            errors.Add("界面上的中文");
            """;
        var code = SourceScanner.StripComments(src);
        var matches = Literal.Matches(code).Cast<Match>().Where(m => Cjk.IsMatch(m.Groups[1].Value)).ToList();
        Assert.Equal(2, matches.Count);
        Assert.True(IsExcluded("Services/Whatever.cs", code, matches[0].Index), "日志出口应被排除");
        Assert.False(IsExcluded("Services/Whatever.cs", code, matches[1].Index), "下一条语句不该被连带豁免");
    }

    /// <summary>
    /// 「**自有异常**的回落文案必须计入，BCL 异常的开发者消息照旧豁免」。
    ///
    /// <para>⚠ 阶段 91 换了口径：以前靠 <c>UserFacingException</c> 那张类型名白名单把
    /// <c>HmiPackageException</c> **捞回来**，现在是反过来的默认
    /// （只有 BCL 类型算开发者消息 ⇒ 自有类型自动计入，见 <see cref="DevMessage"/>）。
    /// 断言一个字没改 —— 这正是要的：**口径换了、结论不变**，变的是失效方向。</para>
    /// </summary>
    [Fact]
    public void IsExcluded_DoesNotSwallowUserFacingExceptionFallbacks()
    {
        // 计入侧：面向用户的自有异常，Message 是回落文案。
        Assert.False(ExcludedIn("Serialization/HmiPackageService.cs",
            """throw new HmiPackageException("ZIP_UNREADABLE", "工程包不是有效的 ZIP 文件");"""));
        // 同一类型写在新对象里（不是 throw 表达式）同样要计入 ——
        // 否则「抽成 helper 就漂移」这个洞还在（阶段 81 实测的正是这个形状）。
        Assert.False(ExcludedIn("Serialization/HmiPackageService.cs",
            """var ex = new HmiPackageException(code, "回落文案");"""));

        // 豁免侧（对照组）：**BCL 类型的开发者消息**照旧豁免 —— 否则这条规则会被
        // 「看着像异常就放过」满足，等于把 DevMessage 整类取消了。
        Assert.True(ExcludedIn("Serialization/HmiPackageService.cs",
            """throw new ArgumentException("资产目录不能为空");"""));
        Assert.True(ExcludedIn("Services/Whatever.cs",
            """throw new InvalidOperationException("参数不合法");"""));
    }

    [Fact]
    public void IsExcluded_DoesNotInheritASiblingPropertysExemption()
    {
        // 兄弟属性里的 Log 不能豁免本属性 —— 否则对象初始化器整片连坐
        const string src = """
            var x = new AuditLogEntry { Detail = "界面上的中文", Note = Log?.Invoke("中文") };
            """;
        var code = SourceScanner.StripComments(src);
        var first = Literal.Matches(code).Cast<Match>().First(m => Cjk.IsMatch(m.Groups[1].Value));
        Assert.False(IsExcluded("Services/Whatever.cs", code, first.Index),
            "兄弟属性里的 Log 调用不该豁免本属性的中文");
    }
}
