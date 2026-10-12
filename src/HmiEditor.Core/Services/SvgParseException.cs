namespace HmiEditor.Core.Services;

/// <summary>
/// 「本仓库产出的 SVG 不可用」—— 把 <see cref="SvgParseError"/> **包成异常**交给上层。
///
/// <para><b>为什么必须有这个类型（登记 #61a 的第一刀）</b>：<c>IconGenerator</c> /
/// <c>ImageGenerator</c> 的路径上，失败必须先变成异常才能穿过 AI 层的返回契约，
/// 改动前那两处写的是</para>
/// <code>throw new InvalidOperationException($"SVG 生成无效: {error}");</code>
/// <para>而 <c>InvalidOperationException</c> **没有码** ⇒ App 侧
/// <c>ExternalText.FailureReason</c> 的三支分叉（包码异常 / BCL 类型码 / 回落原文）
/// 一支都接不住它，只能落第 3 支把**中文原文**原样渲染 ⇒ 英文界面下
/// <c>ai.gen.failed</c>（<c>生成失败: {0}</c>）的原因那一半照旧中文。
/// 把裸异常换成带码异常，<b>码这条路才第一次通到界面</b>；否则"把站点改成
/// <c>FailureReason(ex)</c>"就只是阶段 83 钉死的**改名式假闭环**。</para>
///
/// <para><b>为什么不用 <c>HmiPackageException</c></b>：那是工程包（<c>.hmi</c>）解包的码，
/// 与 SVG 无关；两者共用键前缀也就成了撒谎。码表按**产出方**分家
/// （<c>package.issue.</c> / <c>system.issue.</c> / <c>ai.issue.</c> / <c>script.issue.</c> /
/// 本类型对应的 <c>svg.issue.</c>），是仓库既定约定。</para>
///
/// <para><b><see cref="Exception.Message"/> 保留中文回落</b>（= <see cref="SvgParseError.Message"/>）：
/// 它是 <c>FailureReason</c> 走不到码化分支时的显示内容，也是任何日志/兜底打印里的可读信息 ——
/// 换成英文原文或码名都是退步。</para>
/// </summary>
public sealed class SvgParseException : Exception
{
    /// <summary>用一条失败构造。异常消息 = 该失败的中文回落原文。</summary>
    /// <param name="error">见 <see cref="SvgParseError"/>。不可以为 <c>null</c>。</param>
    public SvgParseException(SvgParseError error)
        : base(error.Message)
        => Error = error;

    /// <summary>本异常的**码 + 实参 + 中文回落** —— App 侧渲染的唯一依据。</summary>
    public SvgParseError Error { get; }
}
