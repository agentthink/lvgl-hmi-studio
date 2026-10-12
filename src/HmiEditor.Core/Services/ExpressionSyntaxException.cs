namespace HmiEditor.Core.Services;

/// <summary>
/// 「表达式语法不合法」—— 把 <see cref="ExpressionSyntaxError"/> **包成异常**交给调用方
/// （阶段 91，登记 <b>#61i</b>）。
///
/// <para><b>为什么要有这个类型</b>：<see cref="ExpressionEngine.Compile"/> 的返回契约是
/// <c>ExprNode</c>（AST）⇒ 失败**只能**靠异常出来。改动前那三处写的是</para>
/// <code>throw new FormatException("缺少右括号 ')'");</code>
/// <para>而 <c>FormatException</c> **没有码** ⇒ 三个消费点里有两个把
/// <c>ex.Message</c> 直接当界面文案用（<c>GeneratedCodeValidator</c> 塞进 finding 的
/// <c>{1}</c>、<c>PropertyPanelView</c> 直显在属性面板上）⇒ 英文界面下那一半照旧中文。
/// 换成本类型之后，**码这条路才第一次通到界面**。</para>
///
/// <para><b>不给 <c>FormatException</c> 加码表</b>：<c>FormatException</c> 是 BCL 类型、
/// 全仓有别的来源（<c>double.Parse</c> / <c>DateTime.Parse</c> …）⇒ 给它建码等于认领别人的异常。
/// 码表按**产出方**分家（<c>package.issue.</c> / <c>system.issue.</c> / <c>ai.issue.</c> /
/// <c>script.issue.</c> / <c>svg.issue.</c> / <c>csv.issue.</c> / <c>gen.issue.</c> /
/// <c>expr.syntax.</c>），是仓库既定约定。</para>
///
/// <para><b><see cref="Exception.Message"/> 保留中文回落</b>（= <see cref="ExpressionSyntaxError.Message"/>）：
/// 它是任何日志/兜底打印里的可读信息，也是 <c>CodeGenerator.Screen.cs</c> 写进产物注释的那一份。
/// 换成英文原文或码名都是退步。</para>
/// </summary>
public sealed class ExpressionSyntaxException : Exception
{
    /// <summary>用一条语法失败构造。异常消息 = 该失败的中文回落原文。</summary>
    /// <param name="error">见 <see cref="ExpressionSyntaxError"/>。不可以为 <c>null</c>。</param>
    public ExpressionSyntaxException(ExpressionSyntaxError error)
        : base(error.Message)
        => Error = error;

    /// <summary>本异常的**码 + 实参 + 中文回落** —— 消费点渲染的唯一依据。</summary>
    public ExpressionSyntaxError Error { get; }
}
