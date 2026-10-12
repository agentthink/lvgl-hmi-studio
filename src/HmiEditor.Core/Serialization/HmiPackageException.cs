using System.IO;

namespace HmiEditor.Core.Serialization;

/// <summary>
/// 工程包（<c>.hmi</c>）解包失败 —— 携带**诊断码 + 实参 + 中文回落原文**的异常。
///
/// <para><b>为什么需要一个专门的异常类型</b>：Core 拿不到字典服务（依赖方向 App → Core），
/// 所以它只能把「哪一类失败」（<see cref="Code"/>）、「模板要填什么」（<see cref="Args"/>）、
/// 以及「字典缺键时给用户看什么」（<see cref="Message"/>）三件东西交出去，
/// 由 App 侧的 <c>ExternalText.FailureReason</c> 渲染成界面文案。
/// 这与 <c>AiValidationIssue</c> / <c>ScriptError</c> 的「码 + 实参 + 回落」是同一个形状。</para>
///
/// <para><b>为什么不继承 <see cref="InvalidDataException"/></b>：那个类型在 .NET 里是
/// <b>sealed</b>（实测 <c>CS0509: 无法从密封类型“InvalidDataException”派生</c>）——
/// 而改动前 <c>Unpack</c> 抛的正是它，所以不能靠"换个子类"保住类型兼容。
/// 好在全仓**没有任何** <c>catch (InvalidDataException)</c> 落在解包路径上
/// （查过：<c>InvalidDataException</c> 的命中全是 throw 点 + 一条断言），
/// ⇒ 基类换成 <see cref="Exception"/> 不改变任何现有捕获行为。⚠ xunit 的
/// <c>Assert.Throws&lt;T&gt;</c> 是**精确**类型匹配（<c>ThrowsAny&lt;T&gt;</c> 才认派生），
/// 所以既有那条 <c>HmiPackageServiceTests.Unpack_NonZip_Throws</c> 必须改成断言本类型
/// —— 它同时顺势多钉了一条：<see cref="Code"/> == <c>ZIP_UNREADABLE</c>。</para>
///
/// <para><b><see cref="Args"/> 的语义</b>：与模板占位符**一一对应**（0 实参的码这里是 <c>null</c>）。
/// App 层查字典时用 <c>LocalizationService.Format</c> 填它，而那个方法在
/// 「模板占位符个数 ≠ 实参个数」时**不抛异常、只原样返回** ⇒ 用户会看到 <c>{1}</c> 这种字面量。
/// 所以这条对应关系由 <c>PackageIssueLocalizationGuardTests.PlaceholderCount_MatchesArgsCount</c>
/// 单独钉住（编译器看不见它）。</para>
/// </summary>
public sealed class HmiPackageException : Exception
{
    /// <summary>
    /// 构造一条包失败。
    /// </summary>
    /// <param name="code">诊断码，取自 <see cref="HmiPackageIssueCodes"/> 的常量值。</param>
    /// <param name="fallbackMessage">**产出方自带的中文回落原文** —— 字典缺键时原样显示它
    /// （宁可显示中文，也不能把码名或英文异常原文显示给用户）。</param>
    /// <param name="args">模板占位符实参；不传 / 空 = 模板不含占位符。</param>
    public HmiPackageException(string code, string fallbackMessage, params object[]? args)
        : base(fallbackMessage)
    {
        Code = code;
        Args = args is { Length: > 0 } ? args : null;
    }

    /// <summary>
    /// 构造一条包失败，并保留**原始异常**（<paramref name="inner"/>）。
    ///
    /// <para><b>为什么要把 inner 留住</b>：中途失败的原因（<c>IOException</c> /
    /// <c>UnauthorizedAccessException</c> / <c>JsonException</c>）**不进**用户文案，
    /// 但它是排查时唯一能指到"哪个文件、哪个路径、哪个字节偏移"的东西 ——
    /// 码化时把它丢掉，等于顺手把取证手段也丢了（阶段 105）。</para>
    /// </summary>
    /// <param name="code">诊断码，取自 <see cref="HmiPackageIssueCodes"/> 的常量值。</param>
    /// <param name="fallbackMessage">**产出方自带的中文回落原文**。</param>
    /// <param name="inner">原始异常（只进 <see cref="Exception.InnerException"/>，不进文案）。</param>
    /// <param name="args">模板占位符实参；不传 / 空 = 模板不含占位符。</param>
    public HmiPackageException(string code, string fallbackMessage, Exception inner, params object[]? args)
        : base(fallbackMessage, inner)
    {
        Code = code;
        Args = args is { Length: > 0 } ? args : null;
    }

    /// <summary>诊断码（<see cref="HmiPackageIssueCodes"/> 里那些 <c>const string</c> 的值）。</summary>
    public string Code { get; }

    /// <summary>模板占位符实参；<c>null</c> = 模板不含占位符。</summary>
    public IReadOnlyList<object>? Args { get; }
}
