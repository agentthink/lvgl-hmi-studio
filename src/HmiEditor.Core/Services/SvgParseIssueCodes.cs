namespace HmiEditor.Core.Services;

/// <summary>
/// SVG 解析失败的**用户可见诊断码** —— 单一来源。
///
/// <para><b>它解决什么</b>：<c>SvgParser.Parse</c> 以前把失败做成**裸中文字符串**
/// （<c>"SVG 内容为空"</c> / <c>"根元素不是 &lt;svg&gt;"</c> / <c>"XML 解析失败: {ex.Message}"</c>），
/// 由 App 的两处站点直接拼进模板 <c>asset.svg.parse.fail</c>（<c>SVG 解析失败: {0}</c>）；
/// 而 <c>IconGenerator</c> / <c>ImageGenerator</c> 又把它包进 <c>InvalidOperationException</c>
/// 抛出，最终由 <c>ai.gen.failed</c> 的宽捕获直通 <c>ex.Message</c>。
/// ⇒ 界面语言切成英文时，这一半照旧是**中文**（第三档还夹着 .NET 的英文原文）。</para>
///
/// <para><b>⚠ 这是「返回值档」而不是「异常档」</b>（阶段 87 实测，登记 <b>#61a</b>）：
/// 本仓库的 A 类产出方（<c>SvgParser</c> / <c>GeneratedCodeValidator</c> /
/// <c>CsvVariableImporter</c> / <c>RecipeManager</c>）**都是纯函数、都不抛异常** ——
/// 它们把失败做成**返回值**。<c>SvgParser</c> 这条路径的异常是**下游**
/// （<c>IconGenerator</c> / <c>ImageGenerator</c>）自己包出来的，见
/// <see cref="SvgParseException"/>。⇒ 所以这张码表与 <c>HmiPackageIssueCodes</c> /
/// <c>AiIssueCodes</c> 同形，但消费方式有两档：
/// <b>返回值档</b>走 <c>ExternalText.Describe(SvgParseError)</c>、
/// <b>异常档</b>走 <c>ExternalText.FailureReason(ex)</c>。</para>
///
/// <para><b>为什么必须是 <c>public const string</c></b>：本地化守卫靠**反射枚举**这个类型，
/// 拿"声明了哪些码"与"真正产出的码"做**双向差集** —— 声明了产不出的是死码
/// （它和它的字典键都永远不会被用到），产出了没声明的是漏登记（键不会进字典 ⇒ 回落中文）。
/// 反射要求 <c>IsLiteral &amp;&amp; FieldType == typeof(string)</c>，所以不能是
/// <c>static readonly</c>。</para>
///
/// <para><b>命名</b>：大写下划线。字典键统一落在 <c>svg.issue.</c> 之下
/// （由 <c>ExternalText.SvgIssueKey</c> 统一推导，码表本身不认识字典）。</para>
///
/// <para><b>刻意不码化的失败</b>：解析成功但一条形状都没提取出来，**不是**解析失败
/// （空 <c>&lt;svg/&gt;</c> 是合法输入）—— 那一档在 UI 上走既有的
/// <c>asset.svg.no.shapes.hint</c>。码表里的 <see cref="NO_SHAPES"/> 只服务于
/// **AI 侧**：对"模型刚生成的 SVG"来说，零形状与解析失败是同一件事（都不可用），
/// 而 <c>SvgParser</c> 对它返回成功 ⇒ 那一个码由消费方构造（<c>SvgParseError.NoShapes</c>）。</para>
/// </summary>
public static class SvgParseIssueCodes
{
    /// <summary>输入为空白（<c>null</c> / 空串 / 全空白）。0 实参。</summary>
    public const string CONTENT_EMPTY = "CONTENT_EMPTY";

    /// <summary>XML 能解析，但根元素不是 <c>&lt;svg&gt;</c>。0 实参。</summary>
    public const string ROOT_NOT_SVG = "ROOT_NOT_SVG";

    /// <summary>内容不是合法 XML（<c>XDocument.Parse</c> 抛）。0 实参。
    /// ⚠ .NET 的原文<b>不进模板</b>（它是英文、且带行号列号），但**不许丢** ——
    /// 它留在 <see cref="SvgParseError.Message"/> 里当回落与排障信息。</summary>
    public const string XML_INVALID = "XML_INVALID";

    /// <summary>解析成功但一条形状都没有。0 实参。**由消费方构造**，不由 <see cref="SvgParser"/> 返回。</summary>
    public const string NO_SHAPES = "NO_SHAPES";
}
