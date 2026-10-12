namespace HmiEditor.Core.Services;

/// <summary>
/// CSV 变量导入的**诊断码**表 —— 「**码 + 中文回落原文**（+ 可选实参）」三段式的码那一半。
/// 与 <c>HmiPackageIssueCodes</c> / <c>SvgParseIssueCodes</c> / <c>AiIssueCodes</c> /
/// <c>ScriptError.Code</c> 同一套约定。
///
/// <para><b>为什么要有这张表</b>（登记 <b>#61f</b>，阶段 88）：<see cref="CsvVariableImporter.Parse"/>
/// 的失败以前是**裸中文字符串**列表（<c>ImportResult.Errors</c> 曾是 <c>List&lt;string&gt;</c>），
/// 而它的两个消费方**一个只取条数、一个整块丢弃**（实测：
/// <c>VariableImportView</c> 只显示 <c>Errors.Count</c>、<c>CommunicationConfigView</c>
/// 连 <c>Errors</c> 都不读）⇒ CSV 里有坏行时用户在两条路上都**看不到是哪几行坏了**，
/// 坏行被静默丢掉。码化之后 App 层才能把「哪一行、为什么」按用户语言渲染出来
/// （渲染出口 <c>ExternalText.Describe(CsvImportIssue)</c>）。</para>
///
/// <para><b>它们都不是异常</b>：<see cref="CsvVariableImporter"/> 是纯函数，契约就是
/// "返回 (变量表, 问题表)"。⇒ 本族走「**返回值带码**」那条搬运方式 ——
/// 与 SVG 解析失败（登记 #61a 第一刀，阶段 87）同形，**不是**带码异常。
/// 这一点是阶段 87/88 两次实测的结论，别再往这个族上套「加码表 + 抛带码异常」。</para>
///
/// <para><b>键由 App 侧统一推导</b>：<c>csv.issue.&lt;码小写&gt;</c>。Core 拿不到字典服务
/// （依赖方向 App → Core）⇒ 这里只声明稳定的码，查表在 App 层。</para>
/// </summary>
public static class CsvImportIssueCodes
{
    /// <summary>整个文件级：有效行不足（连表头带数据都凑不出两行）。0 实参。</summary>
    public const string TOO_FEW_LINES = "TOO_FEW_LINES";

    /// <summary>整个文件级：表头一行都认不出来（Name / DataType / Address 三列一个都没匹配上）。1 实参 = 当前表头原文。</summary>
    public const string NO_HEADER_COLUMNS = "NO_HEADER_COLUMNS";

    /// <summary>行级：该列索引超出本行字段数 ⇒ 取不到 Name。0 实参（行号是结构化字段）。</summary>
    public const string ROW_MISSING_NAME = "ROW_MISSING_NAME";

    /// <summary>行级：Name 列存在但是空白。0 实参。</summary>
    public const string ROW_EMPTY_NAME = "ROW_EMPTY_NAME";

    /// <summary>
    /// 行级：解析这一行时抛了异常（兜底分支）。1 实参 = 该异常的 <c>Message</c>。
    ///
    /// <para>⚠ 实参是 **BCL / 运行时的原文**（英文）—— 它**故意进模板**：这一档的原文本身就是
    /// 排障信息（与 <see cref="SvgParseIssueCodes.XML_INVALID"/> 相反：那一档的 .NET 原文
    /// 连行列号一起抖给用户、刻意不进模板）。这里只是一句短类型名级别的消息，
    /// 丢掉它比留着更难排障。英文界面下这一半仍是英文 —— 属本族的**诚实边界**。</para>
    /// </summary>
    public const string ROW_PARSE_FAILED = "ROW_PARSE_FAILED";
}
