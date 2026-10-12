using System;
using System.Collections.Generic;
using System.IO;
using Xunit.Abstractions;

namespace HmiEditor.TestSupport;

/// <summary>
/// 判据读数的**统一出口** —— 解决登记表 #42：「这一族动作面判据在"绿"的时候没有读数」。
///
/// <para><b>问题</b>：阶段 49/50/51/52 那四层的动作面判据都把「面解析出几个、真点了几个、
/// 各自在哪棵树里」攒进一个 <c>List&lt;string&gt; readings</c>，然后**只**拼进
/// <c>Assert.True(cond, message)</c> 的 message —— 而那个 message **只在红的时候打印**。
/// 判据全绿时这些数字拿不到，于是「65 个解析、58 个真点」只能是推断，不是证据；
/// 要拿到就得**临时注入一个正控把下界抬高**，逼它转红。这既不便宜也不该常态做。</para>
///
/// <para><b>两条出口，同一份内容</b>：</para>
/// <list type="number">
///   <item><see cref="ITestOutputHelper"/>（判据类构造函数里注入，随类实例传进来）——
///     **按需**：用 <c>--logger "console;verbosity=detailed"</c> 跑就看得见。
///     门禁用的是 <c>verbosity=minimal</c>，所以这条路在门禁日志里是看不见的。</item>
///   <item>设了 <c>HMI_READINGS_OUT=&lt;文件&gt;</c> 时**追加落盘** —— 这条路**在门禁的
///     minimal logger 下也拿得到**（沿用 <c>UiTestHost</c> 既有的 <c>HMI_DIAG_OUT</c> 同款约定：
///     一个环境变量，不设就什么都不做）。刻意是**追加**而不是进程退出时汇总：读数按 scope
///     分段，追加比汇总更容易看，也不依赖进程正常退出。</item>
/// </list>
///
/// <para><b>刻意不做成断言</b>（#42 的原文）：把「65 / 58」这类数字写进断言，下一个人就会
/// **为改数字而改测试** —— 判据该断言的是关系（「解析出的个数 &lt; 60 就是主面失效」），
/// 读数只是**证词**。所以本类只输出，不判定。</para>
///
/// <para><b>为什么住在 TestSupport（阶段 264）</b>：原先只在 <c>HmiEditor.App.UiTests</c> 里 —— 那批
/// 动作面判据（#42）是它的第一个用户。阶段 264 起 <c>HmiEditor.Core.Tests</c> 也需要同一个出口
/// （远程访问契约的跨语言样例判据）⇒ 按仓里「同源接缝、不另抄一份」的规矩搬进共用支撑库，
/// 三个测试程序集共用这一份实现（各测试项目都 `<Using Include="HmiEditor.TestSupport" />`）。</para>
///
/// <para><b>默认零副作用</b>：<see cref="Write(ITestOutputHelper?, string, IReadOnlyList{string})"/>
/// 在「没有 output、也没有环境变量」时**直接返回**：不取时刻、不分配、不开文件。
/// 与 <c>UiTestHost.DiagEnabled</c> 同一条理由 —— 观测量本身不许改变被测对象。</para>
/// </summary>
public static class JudgeReadings
{
    /// <summary>落盘开关。设成文件路径即开启；不设 = 关（默认）。</summary>
    private const string EnvVar = "HMI_READINGS_OUT";

    private static readonly object FileGate = new();

    /// <summary>
    /// 报告一组读数。<paramref name="output"/> 允许为 null（判据类没注入时），
    /// 落盘路径从环境变量取。
    /// </summary>
    public static void Write(ITestOutputHelper? output, string scope, IReadOnlyList<string> lines)
        => Write(output, scope, lines, Environment.GetEnvironmentVariable(EnvVar));

    /// <summary>
    /// 报告一组读数，落盘路径显式给出（<c>null</c> = 不落盘）。
    ///
    /// <para>把路径做成参数是为了**判据能确定性地测两条分支** —— 否则「未设环境变量时不落盘」
    /// 是一条同义反复（不设变量，自然不知道往哪儿写），测不出任何东西。</para>
    /// </summary>
    public static void Write(ITestOutputHelper? output, string scope,
                             IReadOnlyList<string> lines, string? filePath)
    {
        // 空读数**不留空块**：否则文件里会出现一堆只有标题的 `=== X ===`，
        // 真实的读数会被噪音淹掉。这也是一条可被判据钉住的分支（见 JudgeReadingsTests）。
        if (lines.Count == 0) return;

        output?.WriteLine(Format(scope, lines));

        if (!string.IsNullOrEmpty(filePath))
            AppendTo(filePath!, scope, lines);
    }

    /// <summary>把一组读数渲染成一段文本。<paramref name="output"/> 与文件出口共用，防两处格式漂移。</summary>
    public static string Format(string scope, IReadOnlyList<string> lines)
        => $"=== {scope} ===" + Environment.NewLine + string.Join(Environment.NewLine, lines);

    /// <summary>
    /// 追加落盘。**fail-soft**：写不出去（目录不存在、无权限、路径被占）不该改判据结论 ——
    /// 读数只是证词，把它变成新的失败源会让「门禁红」这件事失真。与 <c>UiTestHost.WriteDiag</c> 同。
    /// </summary>
    public static void AppendTo(string path, string scope, IReadOnlyList<string> lines)
    {
        try
        {
            lock (FileGate)   // 判据并行跑 ⇒ 不锁会互相截断
                File.AppendAllText(path, Format(scope, lines) + Environment.NewLine);
        }
        catch
        {
            // 见上：读数写不出去不该影响测试结论
        }
    }
}
