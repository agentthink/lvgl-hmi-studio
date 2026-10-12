namespace HmiEditor.Core.Services;

/// <summary>
/// 一条 CSV 变量导入的问题 —— 「**码 + 中文回落原文**（+ 可选实参 + 可选行号）」四段式，
/// 与 <see cref="SvgParseError"/> / <see cref="ScriptError"/> / <c>AiValidationIssue</c> /
/// <c>HmiPackageException</c> 同形。
///
/// <para><b>为什么是<b>记录</b>而不是异常</b>：产出方
/// <see cref="CsvVariableImporter.Parse"/> 是纯函数，契约就是「返回 (变量表, 问题表)」——
/// 阶段 87/88 两次实测都指向同一件事：<b>不许为了让它能抛异常而改契约</b>。
/// 需要它的那条路（App 界面）由 <c>ExternalText.Describe(CsvImportIssue)</c> 渲染，
/// 于是「码」只有一个来源、键只有一套（<c>csv.issue.&lt;码小写&gt;</c>）。</para>
///
/// <para><b><see cref="Message"/> 是回落、不是文案</b>：字典缺键时原样显示它
/// （宁可显示中文，也不能把码名显示给用户）。它是**给界面兜底用的**，
/// 与 <see cref="SvgParseError.Message"/> 那种「兼作排障载体」的用法不同 —— 见下面那条。</para>
///
/// <para><b><see cref="Line"/> 是结构化字段、<b>不进</b> <see cref="Message"/></b>
/// （照 <see cref="ScriptError"/> 的既有约定）：行号前缀由 App 侧渲染时加
/// （<c>ExternalText.Describe</c> 走 <c>csv.line.prefix</c>）。两处都存必然有一处先过期 ——
/// 「第 N 行」的措辞一改样式，落盘/回落里那份就悄悄不对了。
/// <c>0</c> = 整个文件级的问题（不附行号）。</para>
///
/// <para><b>⚠ 诚实边界</b>：<see cref="CsvImportIssueCodes.ROW_PARSE_FAILED"/> 那一档的
/// <see cref="Args"/> 里带的是 **BCL / 运行时原文**（英文），英文界面下这一半照旧是英文。
/// 那是有意留的：原文本身是排障信息，丢掉它比留着更难定位。</para>
/// </summary>
/// <param name="Code">诊断码，取自 <see cref="CsvImportIssueCodes"/> 的常量值。</param>
/// <param name="Message">**产出方自带的中文回落原文**，不含行号。</param>
/// <param name="Line">1 起的行号；<c>0</c> = 文件级问题（渲染时不加前缀）。</param>
/// <param name="Args">模板占位符实参；<c>null</c> = 模板不含占位符。</param>
public sealed record CsvImportIssue(
    string Code, string Message, int Line = 0, IReadOnlyList<object>? Args = null);
