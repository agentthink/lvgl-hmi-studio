namespace HmiEditor.Core.Services;

/// <summary>
/// 一条 SVG 解析失败 —— 「**码 + 中文回落原文**（+ 可选实参）」三段式，
/// 与 <c>HmiPackageException</c> / <c>AiValidationIssue</c> / <c>ScriptError</c> 同形。
///
/// <para><b>为什么是<b>记录</b>而不是异常</b>：产出方 <see cref="SvgParser.Parse"/> 是纯函数，
/// 契约就是"返回 (文档, 失败)"—— 阶段 84 的教训是<b>不许为了让它能抛异常而改契约</b>
/// （那会把 40 个调用点全卷进来，其中 35 个是测试/内部消费，根本不需要码）。
/// 需要异常的那条路（AI 生成）由 <see cref="SvgParseException"/> 把本对象**包起来**，
/// 于是「码」只有一个来源、两种搬运方式。</para>
///
/// <para><b><see cref="Message"/> 是回落、不是文案</b>：字典缺键时原样显示它
/// （宁可显示中文，也不能把码名显示给用户）。它同时是**排障信息的载体** ——
/// <see cref="SvgParseIssueCodes.XML_INVALID"/> 那一档里带着 .NET 的原始消息
/// （含行列号），界面上不显示它，但异常对象/回落路径里必须还在。</para>
/// </summary>
/// <param name="Code">诊断码，取自 <see cref="SvgParseIssueCodes"/> 的常量值。</param>
/// <param name="Message">**产出方自带的中文回落原文**。</param>
/// <param name="Args">模板占位符实参；<c>null</c> = 模板不含占位符。</param>
public sealed record SvgParseError(string Code, string Message, IReadOnlyList<object>? Args = null)
{
    /// <summary>
    /// 「解析成功但一条形状都没有」—— UI 侧**不用**它（那里走既有的
    /// <c>asset.svg.no.shapes.hint</c>）；它服务的是 AI 侧：对"模型刚生成的 SVG"
    /// 来说零形状与解析失败是同一件事。
    /// </summary>
    public static readonly SvgParseError NoShapes = new(SvgParseIssueCodes.NO_SHAPES, "无可用形状");
}
