using System;
using System.IO;
using System.Text.Json;

namespace HmiEditor.App.Services;

/// <summary>
/// **BCL / 系统层失败**的诊断码 —— 单一来源（登记表 §4 <b>#61e</b>）。
///
/// <para><b>它解决什么</b>：界面在中文环境下长期是**中英混排** —— 保存失败时用户看到的是
/// <c>保存失败: Access to the path '/…/demo.hmi.tmp' is denied.</c>。阶段 84 用探针实测
/// （<c>tools/probe-bcl-io-messages.cs</c>）把 <c>CurrentUICulture</c> 切成 <c>zh-CN</c> /
/// <c>en-US</c>，消息**一字不变** ⇒ BCL 的 IO 原文**不可翻译**，唯一出路是**按异常类型码化**：
/// 「这是什么毛病」由字典决定（可翻译），「是哪个文件」由模板实参决定（#61b）。</para>
///
/// <para><b>与 <see cref="HmiEditor.Core.Serialization.HmiPackageIssueCodes"/> 的区别</b>：
/// 那套的产出方是**本仓库**（Core 主动声明码 + 抛带码异常）；这一套没有产出方 —— 异常是
/// **BCL 自己抛的**，所以「类型 → 码」这张表只能在 App 侧（<see cref="ExternalText.FailureReason"/>
/// 所在层）维护。这是**唯一**一处「码不是由产出方声明」的地方。</para>
///
/// <para><b>码表为什么是这 7 个 —— 来自阶段 85 探针实测，不是凭类型清单抄的</b>
/// （<c>tools/probe-bcl-exception-types.cs</c>，20 个场景，覆盖本仓库真实调用面
/// <c>File.*</c> / <c>Directory.*</c> / <c>new StreamWriter|StreamReader</c> /
/// <c>ZipArchive</c> / <c>JsonDocument</c>）：</para>
/// <list type="bullet">
///   <item><c>UnauthorizedAccessException</c>（实测 7 次；<c>Access to the path '…' is denied.</c>）——
///     目标是目录、或文件名超 <c>NAME_MAX</c> 时都是它；</item>
///   <item><c>FileNotFoundException</c>（6 次；<c>Could not find file '…'.</c>）；</item>
///   <item><c>DirectoryNotFoundException</c>（5 次；<c>Could not find a part of the path '…'.</c>）；</item>
///   <item><c>PathTooLongException</c>（1 次；整条路径超 <c>PATH_MAX</c>）；</item>
///   <item><c>InvalidDataException</c>（1 次；<c>ZipArchive</c> 撞非 ZIP 字节）；</item>
///   <item><c>JsonException</c>（1 次；⚠ 实测抛出的**具体类型是 <c>JsonReaderException</c>**，
///     它派生自 <c>JsonException</c> ⇒ 表里挂**基类**才能覆盖两者，见
///     <see cref="TypeMap"/> 的顺序约定）；</item>
///   <item><c>IOException</c>（3 次；三种语义完全不同的情形都落在这里 ——
///     <c>The file '…' already exists.</c> / <c>Directory not empty : '…'</c> /
///     <c>No space left on device : '/dev/full'</c>）⇒ 只能给一句通用的
///     「读写文件时发生错误」，**不假装能区分磁盘满**。</item>
/// </list>
///
/// <para><b>刻意不码化的两类</b>（诚实边界，不是遗漏）：</para>
/// <list type="number">
///   <item><c>ArgumentException</c>（实测 2 次：<c>Null character in path.</c> /
///     <c>The value cannot be an empty string.</c>）—— 这是**调用方的编程错误**，
///     不是用户能撞到的运行条件；而且它是 <c>ArgumentNullException</c> /
///     <c>ArgumentOutOfRangeException</c> 的基类，码化它会**连带吞掉**编程错误。
///     与 <c>HmiPackageIssueCodes</c> 里「<c>targetAssetDirectory</c> 空白仍抛
///     <c>ArgumentException</c>」是同一条边界；</item>
///   <item>**没实测到的类型**（<c>SecurityException</c> / <c>EndOfStreamException</c> /
///     <c>NotSupportedException</c> 等）—— 「看着像 IO」不算依据（阶段 85 的纪律：
///     先量真实形状、不预设）⇒ 它们今天仍回落 <c>ex.Message</c>，要收就**先补探针场景**。</item>
/// </list>
///
/// <para><b>为什么必须是 <c>const string</c> 而不是枚举</b>：与另外四套码
/// （<c>AiIssueCodes</c> / <c>HmiPackageIssueCodes</c> / 脚本诊断码 / 插件校验码）同一形状 ——
/// 守卫靠**反射枚举**这个类型，拿"声明了哪些码"与"表里挂没挂上"做**双向差集**
/// （声明了没人挂 = 死码，它和它的字典键都永远不会被用到）。反射要求
/// <c>IsLiteral &amp;&amp; FieldType == typeof(string)</c>。</para>
///
/// <para><b>命名</b>：大写下划线。字典键统一落在 <c>system.issue.</c> 之下，由
/// <see cref="ExternalText.SystemIssueKey"/> 推导（**不各写一份** —— 推导规则分家过的亏
/// 本项目吃过，见 <c>docs/2026-09-18-plugin-text-and-lang-usage-guards.md</c>）。</para>
/// </summary>
internal static class SystemFailureCodes
{
    /// <summary>目标是目录 / 文件名过长 / 权限不足。字典文案：没有权限访问该文件。</summary>
    internal const string UNAUTHORIZED_ACCESS = "UNAUTHORIZED_ACCESS";

    /// <summary>文件不存在。字典文案：文件不存在。</summary>
    internal const string FILE_NOT_FOUND = "FILE_NOT_FOUND";

    /// <summary>路径中的某一级目录不存在。字典文案：路径中的目录不存在。</summary>
    internal const string DIRECTORY_NOT_FOUND = "DIRECTORY_NOT_FOUND";

    /// <summary>整条路径超过系统上限。字典文案：文件路径太长。</summary>
    internal const string PATH_TOO_LONG = "PATH_TOO_LONG";

    /// <summary>通用 IO 失败（已存在 / 目录非空 / 磁盘写满 / 共享冲突…）。字典文案：读写文件时发生错误。</summary>
    internal const string IO_ERROR = "IO_ERROR";

    /// <summary>数据损坏或格式不对（<c>InvalidDataException</c>）。字典文案：文件内容已损坏或格式不正确。</summary>
    internal const string INVALID_DATA = "INVALID_DATA";

    /// <summary>内容不是合法 JSON（<c>JsonException</c>）。字典文案：文件内容不是合法的 JSON。</summary>
    internal const string INVALID_JSON = "INVALID_JSON";

    /// <summary>
    /// 「异常类型 → 码」的映射表。**顺序即语义**：<see cref="CodeFor"/> 返回**第一个**
    /// 命中的条目，所以**派生类型必须排在它的基类型之前**。
    ///
    /// <para><c>IOException</c> 是 <c>FileNotFoundException</c> /
    /// <c>DirectoryNotFoundException</c> / <c>PathTooLongException</c> /
    /// <c>EndOfStreamException</c> 的基类 ⇒ 它**必须排在这三条之后**；
    /// <c>JsonReaderException</c> 派生自 <c>JsonException</c> ⇒ 挂基类即可覆盖。
    /// 这条不变量有判据守着（<c>SystemIssueLocalizationGuardTests</c> 的
    /// 「祖先不许出现在后代之后」那一条）—— 容易在加类型时被破坏，破坏后**没有编译错误、
    /// 只是静默匹配错**，所以必须钉住。</para>
    /// </summary>
    internal static readonly (Type ExceptionType, string Code)[] TypeMap =
    {
        (typeof(UnauthorizedAccessException), UNAUTHORIZED_ACCESS),
        (typeof(DirectoryNotFoundException), DIRECTORY_NOT_FOUND),
        (typeof(FileNotFoundException), FILE_NOT_FOUND),
        (typeof(PathTooLongException), PATH_TOO_LONG),
        (typeof(InvalidDataException), INVALID_DATA),
        (typeof(JsonException), INVALID_JSON),

        // ⚠ 基类，**必须最后**：它前面三条都是它的派生类型。
        (typeof(IOException), IO_ERROR),
    };

    /// <summary>
    /// 给异常找码；**没实测过的类型返回 <c>null</c>** ⇒ 调用方回落 <c>ex.Message</c>
    /// （宁可显示原文，也不能统统换成一句"操作失败"丢掉全部信息）。
    /// </summary>
    internal static string? CodeFor(Exception ex) => CodeFor(TypeMap, ex);

    /// <summary>
    /// 同上，但按**传入的码表**求值。
    ///
    /// <para><b>为什么要有这个重载</b>：顺序是这张表的语义（见 <see cref="TypeMap"/>），
    /// 而"顺序真的在起作用"这件事**只能靠换一张乱序表来验** —— 判据若自己再实现一遍
    /// 遍历，它验的就是那份副本、不是生产代码（本项目登记过这一族失效模式）。
    /// 所以遍历**只此一份**，判据把坏形状塞进参数。</para>
    /// </summary>
    internal static string? CodeFor((Type ExceptionType, string Code)[] map, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        ArgumentNullException.ThrowIfNull(map);

        foreach (var (type, code) in map)
            if (type.IsInstanceOfType(ex))
                return code;

        return null;
    }
}
