using System;
using System.Collections.Generic;

namespace HmiEditor.App.Services;

/// <summary>
/// 「**模拟器编译 / 启动失败**」—— 把码化的失败交给上层（阶段 103 新增，登记 <b>#61c</b>）。
///
/// <para><b>为什么必须有这个类型</b>：<c>BuildRunDialog</c> 那个唯一的 <c>catch (Exception ex)</c>
/// 接的是产出方**真抛**的中文 <c>InvalidOperationException</c>（<c>SimulatorBuildService</c> 的两条
/// 前置校验、以及本对话框里 <c>Process.Start</c> 返回 <c>null</c> 那一支），而
/// <c>InvalidOperationException</c> **没有码**（<c>SystemFailureCodes</c> 的表里没有它，
/// 那张表是实测过才加的）⇒ <see cref="ExternalText.FailureReason"/> 的四支分叉一支都接不住它，
/// 只能落第 ④ 支回落原文 ⇒ 英文界面下这一半照旧中文。换成带码异常，**码这条路才第一次通到界面**；
/// 否则「把 <c>ex.Message</c> 换成 <c>FailureReason(ex)</c>」就只是阶段 83 钉死的
/// **改名式假闭环**（用户所见一字不变，而站点面会绿着变空）。</para>
///
/// <para><b>为什么继承 <see cref="Exception"/> 而不是 <c>InvalidOperationException</c></b>：
/// ① 语义上它不是「无效操作」，而是一档**具名的运行条件**；
/// ② 继承 <c>InvalidOperationException</c> 会让既有的 <c>catch (InvalidOperationException)</c>
/// 无意中继续命中 ⇒ 站点「收口了」这件事在**编译面**上看不出来（本项目登记过的失效模式）。
/// ⇒ 换类型的代价是**编译器当场点出全部消费点**，这正是要的（实测：源码面 3 个抛点、
/// 测试面 0 处 <c>Assert.Throws&lt;InvalidOperationException&gt;</c>）。</para>
///
/// <para><b>⚠ 中文回落原文**不走这个类型**，而是留在产出方的 <c>throw new</c> 里</b> ——
/// 这是本族与 <c>HmiEditor.Core.Services.UserAdminIssue</c> 族（工厂造「码 + 实参 + 回落」）
/// 唯一的形状差异，理由有两条，缺一条都不成立：</para>
/// <list type="number">
///   <item><b>「产出方给码 + 实参 + 中文回落」这条规约说的是产出方，不是中转类型</b> ——
///     本族的产出方就在 <c>HmiEditor.App</c> 里（<c>SimulatorBuildService</c> / <c>BuildRunDialog</c>），
///     所以回落原文写在它的 <c>throw new SimulatorBuildException(...)</c> 里，
///     与改动前那一行**同一个位置**，diff 因此只有「多一个码 + 换个异常类型」。</item>
///   <item>🩸 <b>App 层的硬编码中文守卫会看着这个目录</b>（<c>HardcodedLocalizationGuardTests</c>
///     扫 <c>src/HmiEditor.App</c>）：它对「该本地化却还没本地化」的中文是**零容忍**的，
///     但它**本来就豁免** <c>throw new …Exception("…")</c> 那一类（理由写在它的类注释里：
///     异常消息面向开发者）。把回落留在 <c>throw new</c> 里 ⇒ 不必给那一层扫描器开
///     **文件级后门**（那会让整个文件的任何中文都免检）。⚠ 别为了「形状统一」把回落搬进工厂：
///     <c>UserAdminIssue</c> / <c>RecipeImportIssue</c> 那几族的载体在 <c>HmiEditor.Core</c>，
///     **根本不在那张扫描面上** —— 本族是第一条「码表与产出方同在 App」的族，
///     这个差异是**扫描面造成的**，不是风格选择。</item>
/// </list>
///
/// <para><b><see cref="Exception.Message"/> 保留中文回落</b>：它是 <c>FailureReason</c>
/// 走不到码化分支时的显示内容（字典缺键时原样显示），也是任何日志 / 兜底打印里的可读信息。</para>
/// </summary>
internal sealed class SimulatorBuildException : Exception
{
    /// <summary>用「码 + 中文回落原文（+ 可选实参）」构造。</summary>
    /// <param name="code">见 <see cref="SimulatorBuildIssueCodes"/>。不可以是未声明的码 ——
    /// 码表与抛点的一致性由 <c>SimulatorBuildIssueLocalizationGuardTests</c> 的产出方判据钉住。</param>
    /// <param name="message">**产出方自带的中文回落原文**（作者口吻 + 排障细节；字典缺键时原样显示）。
    /// 字典里的面向用户措辞与它**刻意不同** —— 见 <c>docs/2026-09-23-exception-passthrough-triage.md</c> §103。</param>
    /// <param name="args">模板占位符实参；不传 = 模板不含占位符。</param>
    internal SimulatorBuildException(string code, string message, params object[] args)
        : base(message)
    {
        Code = code;
        Args = args.Length > 0 ? args : null;
    }

    /// <summary>本异常的诊断码 —— App 侧渲染时查字典的唯一依据。</summary>
    internal string Code { get; }

    /// <summary>模板占位符实参（<c>null</c> = 模板不含占位符）。</summary>
    internal IReadOnlyList<object>? Args { get; }
}
