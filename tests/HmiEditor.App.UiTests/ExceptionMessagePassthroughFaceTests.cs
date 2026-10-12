using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HmiEditor.App.Resources;
using HmiEditor.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 「**异常原文直通本地化模板**」的站点面守卫（登记表 §4 <b>#60</b> 的病根，并为 <b>#61</b> 定量）。
///
/// <para><b>它守的是哪一类缺陷</b>：<c>_loc.Format("某键", ex.Message)</c> —— 把异常的
/// <c>Message</c> 直接当模板实参。Core / BCL 抛出来的消息是**英文原文**（#59 的原形状），
/// 而 <c>HmiPackageException</c> 这一类**码化异常**的消息是 Core 的**中文回落原文**
/// （阶段 81 的设计要求"回落自身必须可用"⇒ 它必然是中文）⇒ 界面切成英文时这一半照旧中文。</para>
///
/// <para><b>为什么这条守卫非有不可（而不是"行为判据够了"）</b>：#60-C 的那一处
/// （<c>MainWindow.axaml.cs:1168</c>，文件选择器那条打开路径）在 headless 里**不可驱动** ——
/// <c>IStorageProvider</c> 族被标为"非客户端可实现"，替代基类 <c>NoopStorageProvider</c> 又是
/// <c>internal</c>（两条都是阶段 82 实测的编译错误）。行为判据够不着它，**源码面是唯一能钉住它的地方**。</para>
///
/// <para><b>面口径</b>（<see cref="CollectSites"/>）：<c>src/</c> 下**全部项目**（12 个）的
/// <c>.cs</c> 里每一个 <c>.Format(…)</c> 调用，首实参能解析成字典键（<c>Lang.X</c> 常量或字符串
/// 字面量），且**后续实参**里出现 <c>.Message</c>。**必须先剥注释** —— 否则规则会被自己的文档
/// 满足（本项目已踩三次，见登记 #48）。</para>
///
/// <para><b>面为什么是全 <c>src/</c> 而不是只扫 App（阶段 83 收口）</b>：阶段 82 的面写死在
/// <c>src/HmiEditor.App</c>，于是"同一形状的站点出现在 Core / AI"**在守卫眼里不存在** ——
/// 当时实测 Core / AI 各 0 处（登记 #61 / 寄存器 §39.10 记的"面的边界"），但那是**巧合**不是判据。
/// 现在面从 <c>src/</c> 全量取，并配一条"面必须覆盖每一个含 .cs 的项目"的自检，
/// 新增项目 / 收窄面都会红。</para>
///
/// <para><b>基线不是"豁免"，是"定量清单"</b>：阶段 81 把码化收口在
/// <c>ExternalText.FailureReason</c> + <c>UiOperationGuard.FailureDetail</c> 这条路上，
/// 只覆盖了**经由 <c>UiOperationGuard</c>** 的失败出口；其余直接
/// <c>Format(键, ex.Message)</c> 的站点属**另一个议题**，已登记为 <b>#61</b>。
/// 本表**刻意不逐条写理由**（24 个键逐条编理由就是伪造精度；参见登记 #54 的教训：
/// 豁免表里的"理由"没人去核就会变成把缺陷合法化的字段）。这里的数字化**可复跑**。</para>
///
/// <para><b>🩸 面变红时，正确动作**不是**把它换成 <c>FailureReason</c>（阶段 83 实测定性，
/// 阶段 85 收紧口径）</b>：原来的 37 处**清一色**是 <c>catch (Exception ex)</c> **宽捕获**（36/37），
/// 而 <c>FailureReason</c> 当时**只在** <c>ex is HmiPackageException</c> 上分叉 ⇒ 换过去
/// **只会落进回落分支**，用户所见**一字不变**，而这张面会**绿着变空** —— 即**改名式假闭环**。
/// 所以「面变小」**不等于**迁移成功；真收口在**产出方**（Core / AI / 插件抛带码异常）。
/// 三分类与优先级见 <c>docs/2026-09-23-exception-passthrough-triage.md</c>。</para>
///
/// <para><b>⚠ 但上面那句「只在 <c>ex is HmiPackageException</c> 上分叉」自阶段 85（#61e）起已不成立</b>：
/// 现在 <c>FailureReason</c> 分**三**支 —— ① 包码异常；② <b>BCL / 系统类型码</b>
/// （<c>UnauthorizedAccessException</c> / <c>FileNotFoundException</c> … 共 7 个码）；③ 其余回落原文。
/// ⇒ 这条理由**只是收窄了适用范围，没有消失**：宽捕获里落到 ③ 的（<c>InvalidOperationException</c>
/// 之类没有码的类型）换过去仍然是改名。
/// ⚠ 落到 ② 的那一族（正是 C 类）**不是"裸换一下就有收益"** —— 裸换成 <c>FailureReason(ex)</c>
/// 会把「是哪个文件」丢掉（BCL 原文自带路径，而码化短句不带），只落个「中文 + 没路径」，
/// 仍是亏的；有真实收益的是换到 <c>UiOperationGuard.FailureDetail(操作名, 路径, 异常)</c>
/// 那条**带目标路径**的重载。
/// 判据本身不变 —— 仍以「产出方有没有码」为界，而不是以"面上数字变小"为界。</para>
///
/// <para><b>🩸 剩下的 21 处已经按档处置过，不要照表面数字逐条去迁（阶段 84 实测定性）</b>：
/// 阶段 83 把它们分成三档，阶段 84 逐处**读了现场代码 + 量了产出方**之后，C 类由
/// 「最大收益面」**改期为「#61e 之后才做」** —— 那个前置**阶段 85 已落地**，
/// **阶段 86 已按它迁走 8 处**（37 → **29 处**；留的那 1 处 <c>asset.import.fail</c> 是
/// **批量档**（多文件，结构上就没有"那个文件"）⇒ 归 #61a）。
/// ⚠ 阶段 90 又按 <b>#61h</b> 迁走 5 站（29 → 21 → **15 处**，明细见下面 #61h 那条）。</para>
/// <list type="bullet">
///   <item><b>#61a · A 类（本仓库产出方，语义明确）</b>⇒ 阶段 87 **实测把修法模板换了**：
///     本仓库的 A 类产出方（<c>SvgParser</c> / <c>GeneratedCodeValidator</c> /
///     <c>CsvVariableImporter</c> / <c>RecipeManager</c>）**都是纯函数、都不抛异常** ——
///     它们把失败做成**返回值**，所以「加码表 + 带码异常」这个模板在它们的站点上**无处安放**
///     （那些站点 catch 到的是 IO / UI 的异常）。正确模板是「**返回值带码**」：
///     <c>SvgParseError</c>（码 + 中文回落 + 实参）+ <c>ExternalText.Describe</c>，
///     与 <c>ScriptError</c> / <c>LuaSyntaxValidator</c> 那条路同形。
///     **第一刀已落地** = <c>SvgParser</c> 3 码（<c>svg.issue.*</c>）+ AI 侧把它包成带码
///     <c>SvgParseException</c> ⇒ <c>ai.gen.failed</c> 那 8 处借此**第一次真收口**
///     （产出方有码 ⇒ 换成 <c>FailureReason(ex)</c> 不再等于改名）。逐站实测见专题 §87。</item>
///   <item><b>#61b · C 类（BCL / IO 原文）</b>⇒ <b>已闭环 · 阶段 86</b>：9 站里 8 站迁到
///     <c>FailureDetail(操作名, 路径, 异常)</c>，第 3 站 <c>asset.import.fail</c>（批量）归 #61a。
///     当初记的改期理由（实测，不是推测）：这族的产出方清一色是 BCL / IO，
///     而 <b>BCL 的 IO 原文自带目标路径</b>（<c>Access to the path '…' is denied.</c>），
///     <b>再包一层「—— &lt;路径&gt;」是冗余</b>，且 <c>SaveToPath</c> 那一处会重复出现
///     <c>.tmp</c> 兄弟路径 ⇒ 负收益；而码化出口 <c>FailureReason</c> 当时对 BCL 异常**没有码**
///     ⇒ 换过去是**改名**（恰是上一段钉死的那种假闭环）。<b>两条理由随 #61e 落地而解除</b>
///     （原因那半不再带路径 ⇒ 不冗余；BCL 类型已有码 ⇒ 不是改名）。
///     ⚠ 逐处选**哪个路径**才是真正的判断点：失败点常落在 <c>path + ".tmp"</c> 上。
///     探针 <c>tools/probe-bcl-io-messages.cs</c> / <c>tools/probe-bcl-exception-types.cs</c>，
///     读数见专题 §84–86。</item>
///   <item><b>#61h · C 类漏网的 5 站（阶段 88 量出）</b>⇒ <b>已闭环 · 阶段 90</b>：阶段 86 迁 C 类时
///     只覆盖了 #61b 记下的那 9 站，另有 5 站是**同一个形状**（BCL / IO 原文直通）却没进那份清单 ——
///     阶段 88 用「按产出方类型倒查」量出来（不是照表面数字猜）。5 站全部迁到
///     <c>FailureDetail(操作名, 目标路径, 异常)</c>。⚠ 登记表原本记的「目标对象都在作用域」
///     **对其中 2 站不成立**（<c>CommunicationConfigView</c> 的 <c>files</c>、
///     <c>MainWindow</c> 的 <c>outputPath</c> 都声明在 <c>try</c> **之内**）⇒ 加**承接变量**提到
///     <c>try</c> 外（编译器会当场报 <c>CS0103</c>，**别用 <c>null</c> 糊过去** ——
///     那会同时踩红 <c>UiOperationGuardContractTests.EveryDetailCallSite_PassesAConcreteTargetPath</c>）。
///     「迁完路径不许出现两次」另有判据钉住：
///     <c>UiOperationGuardContractTests.PathAppearsExactlyOnce_AndEachHalfInItsOwnLanguage</c>。</item>
///   <item><b>#61c · B 类（外部工具输出）</b>⇒ 🩸 <b>阶段 102 把这条归因推翻了</b>：
///     它点名的 `compile.failed`×2 与 `download.sharedlibrary.failed` 那两个 <c>catch</c>
///     捕的是**本方法的 IO**（编译器 / 外部产物的失败走 <c>result.Error</c> / <c>SimulatorBuildResult</c>
///     **返回值**、不抛）⇒ 属 C 类，阶段 102 已迁；剩下的 <c>buildrun.log.error</c>，产出方
///     <c>SimulatorBuildService</c> **真抛**中文 <c>InvalidOperationException</c> ⇒ 属 **A 类**
///     （阶段 103 已收口：产出方换 <c>SimulatorBuildException</c> / <c>sim.build.issue.*</c>）。</para>
///     <para>🩸 <b>阶段 103 定论：「B 类」从三分类里删掉</b>（专题 §103.7）。它不是"暂时空着"，
///     而是**结构上不可能有成员**：这张面的谓词要求一个 <c>catch</c> 到的**异常**，而外部工具
///     （cmake / gcc / git / 交叉编译器）的失败**一律走返回值** ——
///     <c>SimulatorBuildService.RunProcessAsync</c> 返回 <c>int</c> 退出码、错误行进
///     <c>SimulatorBuildResult.Errors</c>；<c>BuildPipelineService</c> 读
///     <c>T113SoBuilder</c> 的 <c>result.Error</c>（<c>:313</c>）；
///     <c>GitWorkflowService</c> 返回 <c>(ExitCode, stdout, stderr)</c> 三元组。
///     外部进程的退出码与 stderr 是**数据**、不是异常 ⇒ 这一档**在面上永远不会有站点**。
///     ⇒ 三分类收窄为**两类**：A 类（本仓库产出方，可码化）与 C 类（BCL / IO 原文，
///     <c>#61e</c> 之后同样可码化，但必须走带目标路径的三实参出口）。</para></item>
/// </list>
/// <para>⚠ 还有 7 个键阶段 83 标为【待读】（只有启发式产出方）⇒ <b>不许拿它们当理由表</b>，
/// 它只是排期筛子（登记 #54 的教训）。</para>
/// </summary>
public class ExceptionMessagePassthroughFaceTests
{
    private readonly ITestOutputHelper _out;

    public ExceptionMessagePassthroughFaceTests(ITestOutputHelper output) => _out = output;

    /// <summary>一次命中：(文件, 键)。**不含行号** —— 行号会随任何编辑漂移，那是噪声不是信号。</summary>
    internal sealed record Site(string Relative, string Key);

    private static readonly Regex FormatCall = new(@"\.Format\s*\(", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> KeyByConstant = typeof(Lang)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!, StringComparer.Ordinal);

    /// <summary>
    /// 面：<c>(文件, 键) → 站点数</c>。**阶段 82 实测 27 组合 / 37 站点**，阶段 83 把面从 App
    /// 扩到全 <c>src/</c> 后**实测不变**（Core / AI 各 0 处）—— 路径改成仓库相对，
    /// 于是"这条站点属于哪个项目"在读数里直接可见。
    /// **阶段 86 实测 22 组合 / 29 站点**（#61b 迁走 8 处：5 个组合整条消失 +
    /// <c>asset.import.fail</c> 由 3 减到 1）。
    /// **阶段 87 实测 19 组合 / 21 站点**（#61a 第一刀：<c>ai.gen.failed</c> 的 8 处迁到
    /// <c>ExternalText.FailureReason</c> ⇒ 3 个组合整条消失）。⚠ 这 8 处**不是**改名式假闭环：
    /// 同一阶段给产出方（<c>IconGenerator</c> / <c>ImageGenerator</c>）加了码
    /// （<c>SvgParseException</c>），见类注释里 #61a 那一条。
    /// **阶段 90 实测 14 组合 / 15 站点**（闭环 #61h：阶段 88 量出的 5 站纯 IO 直通 ——
    /// <c>var.import.failed</c> / <c>gen.import.failed</c> / <c>comm.slave.var.import.failed</c> /
    /// <c>font.export.fail</c> / <c>status.simulator.export.failed</c> —— 全部迁到
    /// <c>UiOperationGuard.FailureDetail(操作名, 目标路径, 异常)</c> ⇒ 5 个组合整条消失）。
    /// ⚠ 这 5 处**不是**改名式假闭环：阶段 85（#61e）已给 BCL 异常类型建码，原因那半由码化短句渲染，
    /// 而 <c>system.issue.*</c> 七条**全是 0 实参短句**（不再自带路径）⇒ 迁过去用户**同时**读到
    /// 「哪个文件」+「为什么（他的语言）」—— 阶段 84 当初反对用这个重载的那条理由（路径会显示两次）
    /// 正是被这一点解除的，且**现在有判据反着钉住它**：
    /// <c>UiOperationGuardContractTests.PathAppearsExactlyOnce_AndEachHalfInItsOwnLanguage</c>。
    /// **阶段 97 实测 11 组合 / 12 站点**（登记 #61d 第二刀）：<c>SimulatorView</c> 的
    /// <c>sim.reg.read.failed</c> / <c>sim.reg.write.failed</c> 与 <c>UserManagerView</c> 的
    /// <c>user.mgr.create.failed</c> 三站迁到 <c>ExternalText.FailureReason(ex)</c>。
    /// ⚠ 这三站**不是**改名式假闭环：同一阶段给产出方（<c>AuthenticationService</c> 的
    /// 角色未注册 / 密码长度不足、<c>CommunicationSimulator</c> 的未连接）加了码
    /// （<c>UserAdminException</c> / <c>SimulatorNotConnectedException</c>）。
    /// ⚠ 同一族另外三站**本刀刻意不动**、只登记（专题 §97.9）：<c>login.failed</c> /
    /// <c>recipe.clone.failed</c> 的产出方走 <c>return null</c>，站点根本看不到原因
    /// ⇒ 要的是「给失败档建码」；<c>status.reload.failed</c> 那条 <c>catch</c> 包的是
    /// <c>Dispatcher.UIThread.Post</c>（入队不抛）⇒ **死 catch**，真异常在回调里无人接。
    /// **阶段 98 实测 11 组合 / 12 站点**（登记 #61t / #61u）：条数**一个没动**，
    /// 但有两处**形状变了、位置也变了**，所以基线行必须跟着动 —— 别把它读成"没做事"：
    /// <list type="bullet">
    ///   <item><b><c>recipe.clone.failed</c> 保留、留在原行</b>：产出方
    ///     <c>RecipeManager.CloneRecipe</c> 已改成**返回值带码**（<c>recipe.issue.*</c>），
    ///     失败档由新增的 <c>else if (result.Issue is { } issue)</c> 分支走码化出口；
    ///     这一处 <c>ex.Message</c> 包住的只剩 <c>RefreshGroups</c> / <c>RefreshRecipes</c>
    ///     两行 **UI 刷新**，产出方给不出码 ⇒ 换出口是改名式假闭环，**有意留**。</item>
    ///   <item><b><c>status.reload.failed</c> 搬到 <c>Wiring.Panels.cs</c></b>：那一站原本在
    ///     <c>MainWindow.axaml.cs</c> 的 <c>OnDockLayoutChanged</c> 里，而那个 <c>try</c> 只包了
    ///     一次赋值 ＋ 一次 <c>Post</c> 入队 ⇒ <c>catch</c> **永不触发**、真正的重建异常在回调里
    ///     无人接（面板重建失败时状态栏永久停在「正在重载面板...」，登记 #61u）。
    ///     处置 = 把重建**连它的失败处理一起搬进回调**，出口落在新增的
    ///     <c>Wiring.Panels.RebuildFailureText</c> ⇒ 基线行**换文件**（不是变少）。
    ///     ⚠ 这一站的 <c>ex.Message</c> 同样**有意留**：接的是面板重建内部的意外异常，
    ///     产出方给不出码。本刀的收益是「**异常不再被吞**」，不是换措辞。</item>
    /// </list>
    /// <para><b>阶段 101 实测 9 组合 / 10 站点</b>（#61a 尾巴 4 站）：两站**真迁移**、
    /// 两站**诚实边界**：</para>
    /// <list type="bullet">
    ///   <item><c>status.svg.apply.failed</c> 迁到
    ///     <c>FailureDetail(Lang.UiOpApplySvg, asset.FilePath, ex)</c> —— 那个 <c>try</c>
    ///     的第一行就是 <c>File.ReadAllTextAsync</c>，是**真 IO**；同形的
    ///     <c>asset.import.fail</c> 批量档迁到
    ///     <c>FailureDetail(Lang.UiOpImportImage, currentPath, ex)</c>
    ///     （承接变量提到 <c>try</c> 外 —— 旧登记「那个文件结构上不存在」**实测不成立**，
    ///     <c>path</c> 就在 <c>foreach</c> 里）。⚠ 这两站**不是**改名式假闭环：
    ///     BCL 类型有码（<c>SystemFailureCodes</c>，阶段 85）⇒ 原因那半由码化短句渲染，
    ///     用户**同时**读到「哪个文件」+「为什么（他的语言）」（与阶段 86 / 90 同一判定）。</item>
    ///   <item><c>status.verify.failed</c> / <c>audit.verify.code.failed</c>（同一 <c>catch</c>）
    ///     **有意留**：那个 <c>try</c> 里**没有产出方** —— <c>GenerateFullProject</c> 与
    ///     <c>GeneratedCodeValidator.Validate</c> 全程零 IO、零用户可触发的 <c>throw</c>
    ///     （实测 CodeGen 全项目只有两个 <c>throw</c>，都是程序员错误档）⇒ 换出口只会落
    ///     回落分支、用户所见一字不变（与阶段 98 的 #61t / #61u 同一条边界）。</item>
    /// </list>
    /// <para><b>阶段 103 实测 6 组合 / 6 站点</b>（登记 #61c 收口：<c>buildrun.log.error</c>
    /// 一站迁走）—— 见下面 #61c 那一条与紧随其后的 阶段 103 段。</para>
    /// <para><b>阶段 102 实测 7 组合 / 7 站点</b>（#61 本体 6 站的「量的那一遍」：三站真迁移、
    /// 三站各有归属）：</para>
    /// <list type="bullet">
    ///   <item><c>compile.failed</c>（2 处）/ <c>download.sharedlibrary.failed</c>（1 处）迁到
    ///     <c>UiOperationGuard.FailureDetail(操作名, 目标路径, ex)</c>。⚠ 前一处的同一个
    ///     <c>catch</c> 里**审计那一半早就迁完了**（<c>:208</c>）—— 漏的只是两行状态栏；
    ///     承接变量 <c>targetDir</c> 阶段 86 就已就位。后一处的 <c>string? dir</c> 是本刀新提的
    ///     （<c>TempSubdir</c> 自己抛时自动回落 2 实参版）。操作名一处沿用
    ///     <c>UiOpCompileProject</c>（与工具栏命令、<c>MainWindow</c> 同一操作的 catch 同名），
    ///     而**审计沿用** <c>UiOpExportCode</c>（与成功行 <c>audit.export.c.code</c> 同口径）——
    ///     两个通道各按自己那半的措辞；另一处**新拆** <c>UiOpPrepareSharedLib</c>：
    ///     「下载前准备 .so 产物」与「下载动作本体」是两个操作，共用会让一个键服务多个语义
    ///     （阶段 86 拆 <c>UiOpExportCode</c> / <c>UiOpExportRecipe</c> 同一条理由）。
    ///     ⚠ 这两族**不是**改名式假闭环：BCL 类型有码（<c>SystemFailureCodes</c>，阶段 85）
    ///     ⇒ 原因那半由码化短句渲染，用户**同时**读到「哪个文件」+「为什么（他的语言）」
    ///     （与阶段 86 / 90 / 101 同一判定）。</item>
    ///   <item><c>comm.test.failed</c> / <c>login.failed</c> **有意留**：前者的产出方
    ///     <c>driver.ValidateConnection</c> 返回 <c>ValidationResult</c>（四个内置驱动该接口
    ///     **零 <c>throw</c>**），后者那个 <c>try</c> 里没有任何文件 IO
    ///     （<c>AuthenticationService</c> 零 <c>File.</c> / <c>Directory.</c>，且
    ///     <c>LoginDialog:55</c> 的输入门槛已与 <c>ThrowIfNullOrWhiteSpace</c> 对齐）
    ///     ⇒ 换出口只会落回落分支、用户所见一字不变（与阶段 98 的 #61t / #61u 同一条边界）。</item>
    ///   <item>🩸 <c>buildrun.log.error</c> —— **本阶段把开工前的判断推翻了**：原以为它与上面
    ///     两条同档，实测 <c>SimulatorBuildService</c> **真抛**（<c>InvalidOperationException</c>，
    ///     消息是**中文**的「LVGL 源码无效: …」/「未找到 cmake，请安装 CMake 后重试」）＋ 真 IO
    ///     ⇒ 它是 **A 类异常档（可码化）**，要的是「给产出方建码」，不是「登记边界」。
    ///     阶段 102 **刻意不折进来**、只登记（一次只放开一个变量），另开一刀 —— 那一刀就是
    ///     **阶段 103**：产出方换成带码异常 <c>SimulatorBuildException</c>
    ///     （3 码 / <c>sim.build.issue.*</c>），两处露出点共用同一次
    ///     <c>ExternalText.FailureReason(ex)</c>。</item>
    /// </list>
    /// <para><b>阶段 103 实测 6 组合 / 6 站点</b>（登记 #61c 的最终归位）：<c>buildrun.log.error</c>
    /// 一站迁走 ⇒ 组合整条消失。⚠ **不是**改名式假闭环 —— 同一阶段给产出方加了码（上一条）。
    /// ⚠ 本阶段同时给这张面做了一处**定性收尾**：「B 类（外部工具输出）」这一档**今天一个名目
    /// 都没有**，已从三分类里**删掉**（依据见上面 #61c 那一条）⇒ 三分类收窄为两类。</para>
    /// （生成方式：<c>.workbuddy/tmp/s82-face-probe.py</c> 复刻同一口径扫一遍。）
    /// </summary>
    private static readonly Dictionary<(string File, string Key), int> Baseline = new()
    {
        [("src/HmiEditor.App/MainWindow.axaml.cs", "audit.verify.code.failed")] = 1,
        [("src/HmiEditor.App/MainWindow.axaml.cs", "status.verify.failed")] = 1,
        // ⚠ 阶段 103 删掉的行：`BuildRunDialog.axaml.cs` · `buildrun.log.error`（原 1 处）——
        //    产出方（`SimulatorBuildService` 两条前置校验 + `BuildRunDialog.RunSimulatorAsync`
        //    的进程启动）换成带码异常 `SimulatorBuildException`（3 码 / `sim.build.issue.*`），
        //    两处露出点（`AppendLog` 与同源的 `ShowErrors`）共用**同一次**
        //    `ExternalText.FailureReason(ex)` ⇒ 组合整条消失。
        //    **不是**改名式假闭环：同一阶段给产出方加了码（与 #61a / #61d 同一条判定）。
        [("src/HmiEditor.App/Views/CommunicationConfigView.axaml.cs", "comm.test.failed")] = 1,
        [("src/HmiEditor.App/Views/LoginDialog.axaml.cs", "login.failed")] = 1,
        [("src/HmiEditor.App/Views/RecipeEditorView.axaml.cs", "recipe.clone.failed")] = 1,
        // ⚠ 阶段 98（#61u）换的行：站点从 MainWindow.axaml.cs 搬到这里，**不是变少**。
        [("src/HmiEditor.App/Wiring.Panels.cs", "status.reload.failed")] = 1,
    };

    // ══════════════════════════ 面的取数（纯函数 —— 负控要复用） ══════════════════════════

    /// <summary>语料 → 站点清单。语料**当参数传入**，负控才能喂合成语料（不碰磁盘）。</summary>
    internal static List<Site> CollectSites(IEnumerable<(string Relative, string Text)> sources)
    {
        var sites = new List<Site>();

        foreach (var (relative, text) in sources)
        {
            // 先剥注释：讲规则的注释里会写出 `.Format("k", ex.Message)` 这种示例，
            // 不剥的话规则会被自己的文档满足（登记 #48 踩过三次）。
            var code = SourceScanner.StripComments(text);

            foreach (Match m in FormatCall.Matches(code))
            {
                var args = LocalizedLogCallSiteTests.SplitTopLevelArguments(code, m.Index + m.Length - 1);
                if (args.Count == 0) continue;

                var key = ResolveKey(args[0]);
                if (key is null) continue;   // 首实参不是键（string.Format / 变量）⇒ 不属本面

                if (!args.Skip(1).Any(a => a.Contains(".Message", StringComparison.Ordinal))) continue;

                sites.Add(new Site(relative, key));
            }
        }

        return sites;
    }

    /// <summary>
    /// 首实参 → 字典键；不是键则 <c>null</c>（该调用点就不属于本面）。
    ///
    /// <para><b>字符串字面量必须先过 <c>LooksLikeKey</c> 的形状判定</b>：只判"是不是双引号包起来的"
    /// 会把 <c>string.Format("{0}", ex.Message)</c> 里的 <c>"{0}"</c> 当成键 ——
    /// 这不是假想，是**本判据第一版负控实测报出来的**（<c>NegativeControls</c> 的 NC3 立刻红）。
    /// 形状规则的唯一出处是 <see cref="LocalizedFormatCallSiteTests.LooksLikeKey"/>，此处复用。</para>
    /// </summary>
    private static string? ResolveKey(string expression)
    {
        var e = expression.Trim();

        if (e.StartsWith("Lang.", StringComparison.Ordinal))
            return KeyByConstant.GetValueOrDefault(e["Lang.".Length..]);

        if (e.Length >= 3 && e[0] == '"' && e[^1] == '"')
        {
            var literal = e[1..^1];
            return LocalizedFormatCallSiteTests.LooksLikeKey.IsMatch(literal) ? literal : null;
        }

        return null;
    }

    private static Dictionary<(string File, string Key), int> Tally(IEnumerable<Site> sites)
        => sites.GroupBy(s => (s.Relative, s.Key))
                .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>面与基线的**双向**差异（多了红、少了也红 —— 少了必须同步改小基线）。</summary>
    private static List<string> Diff(IEnumerable<Site> sites)
    {
        var actual = Tally(sites);
        var problems = new List<string>();

        foreach (var (k, n) in actual.OrderBy(x => x.Key.File, StringComparer.Ordinal))
            if (!Baseline.TryGetValue(k, out var want))
                problems.Add($"**新增**  {k.File}  ·  {k.Key}  （{n} 处；基线里没有）");
            else if (want != n)
                problems.Add($"**变多**  {k.File}  ·  {k.Key}  {want} → {n}");

        foreach (var (k, n) in Baseline.OrderBy(x => x.Key.File, StringComparer.Ordinal))
            if (!actual.TryGetValue(k, out var got))
                problems.Add($"**已消失**  {k.File}  ·  {k.Key}  （基线 {n} 处；迁移完成了请把这一行删掉）");
            else if (got < n)
                problems.Add($"**变少**  {k.File}  ·  {k.Key}  {n} → {got}");

        return problems;
    }

    // ══════════════════════════ 判据 ══════════════════════════

    [Fact]
    public void ExceptionMessagePassthroughFace_MatchesBaseline()
    {
        var sites = CollectSites(AppSourceScanner.ScanAllProductSources());
        var problems = Diff(sites);

        var readings = sites.GroupBy(s => (s.Relative, s.Key))
            .OrderBy(g => g.Key.Relative, StringComparer.Ordinal)
            .Select(g => $"    {g.Key.Relative}  ·  {g.Key.Key,-30} {g.Count(),2}")
            .ToList();
        readings.Insert(0, $"    站点 {sites.Count} 个 / (文件,键) 组合 {readings.Count} 个");
        JudgeReadings.Write(_out, "异常原文直通本地化模板（#60 病根 / #61 定量）", readings);

        Assert.True(problems.Count == 0,
            "「异常原文直通本地化模板」的站点面变了（登记 #60 / #61）。\n"
            + "**判「裸换算不算收口」的标准是「产出方有没有码」，不是「面有没有变小」**（阶段 87）：\n"
            + "  · **新增**的那一处 ⇒ 先给**产出方**加码，再让它走 `ExternalText.FailureReason`。"
            + "⚠ 产出方若是**本仓库纯函数**（SvgParser / GeneratedCodeValidator / CsvVariableImporter / "
            + "RecipeManager），它把失败做成**返回值**、不抛异常，「带码异常」在它的站点上无处安放 ⇒ "
            + "走「返回值带码」那条（`SvgParseError` + `ExternalText.Describe`）。\n"
            + "  · **变少/消失、且同期产出方新增了码** ⇒ 真迁移，请把基线改成实测值（这条方向是好消息，"
            + "但必须同步，否则「减少」会被下一次「增加」悄悄抵掉）。\n"
            + "  · **变少/消失、但产出方没加码** ⇒ **改名式假闭环**（用户所见一字不变），**不许**改基线：\n  - "
            + string.Join("\n  - ", problems));
    }

    /// <summary>自检：语料与面都必须真的读到东西 —— 否则"面 == 基线"会在扫描整体失效时恒真。</summary>
    [Fact]
    public void Face_IsNotDegenerate()
    {
        var sources = AppSourceScanner.ScanAllProductSources().ToList();
        var sites = CollectSites(sources);

        Assert.True(sources.Count >= 300, $"只扫到 {sources.Count} 个产品源文件 —— 扫描面失效了");
        Assert.True(sources.Sum(s => s.Text.Length) >= 400_000,
            $"语料总长只有 {sources.Sum(s => s.Text.Length)} 字符 —— 读到的可能不是真源码");

        // 面必须覆盖 src/ 下**每一个含 .cs 的项目**。只数文件数做不到这件事：App 单独就有
        // 122 个文件，足以骗过任何总数下界（阶段 82 的面正是这样只覆盖 App，登记 §39.10）。
        // 这里拿**另一条枚举器**（`SourceScanner.ProductSources()`，仓库里的唯一全量产品面）
        // 与判据实际吃到的语料对拍 ⇒ 「把面收窄回单个项目」「新增项目没进面」两种退化都会红。
        static List<string> ProjectsOf(IEnumerable<string> repoRelative) => repoRelative
            .Select(p => p.Split('/'))
            .Where(parts => parts.Length >= 3 && parts[0] == "src")
            .Select(parts => parts[1])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var inCorpus = ProjectsOf(sources.Select(s => s.Relative));
        var onDisk = ProjectsOf(SourceScanner.ProductSources()
            .Select(f => SourceScanner.Relative(f)));

        Assert.True(onDisk.Count >= 10, $"src/ 下只找到 {onDisk.Count} 个含 .cs 的项目 —— 枚举失效了");
        Assert.Equal(onDisk, inCorpus);

        // ⚠ 下界**刻意不贴着实测值**（阶段 86 实测 29 → 阶段 87 的 21 → 阶段 90 的 15 → 阶段 97 的 12
        // → 阶段 101 的 10 → 阶段 102 的 7 → 阶段 103 的 6）：
        // 口径失效时面会掉到**个位数**，而"面变小"这一幕已经由基线**双向**判定管住
        // （少了必须同步基线、并写清是不是真迁移）。
        // 若把下界跟着实测一路收紧，每次**正确**的迁移都会多红一条 —— 那是噪声不是信号。
        // 这里只做"扫描没瞎"的量级判断：6 的约 2/3（阶段 103 往下放一档，**不贴实测值**）。
        Assert.True(sites.Count >= 4, $"面只解析出 {sites.Count} 个站点 —— 解析口径可能失效了");
        Assert.True(KeyByConstant.Count >= 1500,
            $"Lang 常量只反射到 {KeyByConstant.Count} 个 —— `Lang.X` 的键解析会大面积落空");
    }

    /// <summary>
    /// 4 组注入，两两成对 —— 每对里**同一个注入**分别在"新口径"与"旧口径"下必须一绿一红，
    /// 否则证明不了口径真的在起作用（本项目对负控的一贯要求）。
    /// </summary>
    [Fact]
    public void NegativeControls_EachBreakIsCaughtByTheRightJudgment()
    {
        const string Path_ = "Views/Synthetic.cs";

        // ── NC1 / NC1b：**同一个注入**，一处在代码里、一处在注释里 ──
        var inCode = ($"{Path_}\n", "void M() { _loc.Format(\"demo.key\", ex.Message); }");
        var inComment = ($"{Path_}\n", "// 反例：void M() { _loc.Format(\"demo.key\", ex.Message); } 不许这么写");

        var codeSites = CollectSites(new[] { inCode });
        var commentSites = CollectSites(new[] { inComment });

        Assert.Single(codeSites);                                    // 代码里 ⇒ 是站点
        Assert.Equal("demo.key", codeSites[0].Key);
        Assert.Contains(Diff(codeSites), d => d.Contains("新增", StringComparison.Ordinal)
                                              && d.Contains("demo.key", StringComparison.Ordinal));
        Assert.Empty(commentSites);                                  // 注释里 ⇒ 不是（剥注释生效）
        Assert.DoesNotContain(Diff(commentSites), d => d.Contains("demo.key", StringComparison.Ordinal));

        // ── NC2：把 `.Message` 换成码化出口 ⇒ 同一行不再是站点（证明判据的谓词就是 `.Message`） ──
        var fixedUp = ($"{Path_}\n",
            "void M() { _loc.Format(\"demo.key\", ExternalText.FailureReason(ex)); }");
        Assert.Empty(CollectSites(new[] { fixedUp }));

        // ── NC3：首实参不是键（string.Format / 变量）⇒ 不进面（证明面只在"模板 + 键"上） ──
        var notAKey = ($"{Path_}\n", "void M() { string.Format(\"{0}\", ex.Message); }");
        var variableKey = ($"{Path_}\n", "void M() { _loc.Format(someKey, ex.Message); }");
        Assert.Empty(CollectSites(new[] { notAKey }));
        Assert.Empty(CollectSites(new[] { variableKey }));

        // ── NC4：面缩小时**也必须报**（这条判据不是单向"只许增"的豁免表） ──
        var shrunk = CollectSites(AppSourceScanner.ScanAppSources())
            .Where(s => !(s.Relative.EndsWith("LoginDialog.axaml.cs", StringComparison.Ordinal)
                          && s.Key == "login.failed"))
            .ToList();
        Assert.Contains(Diff(shrunk), d => d.Contains("已消失", StringComparison.Ordinal)
                                           && d.Contains("login.failed", StringComparison.Ordinal));
    }
}
