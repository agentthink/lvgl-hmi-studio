using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 「等一个 git 子进程退出」的窗口单一来源守卫。
///
/// <para><b>背景：</b><c>GitWorkflowService</c> 里有两处「等 git 进程退出」：<c>IsGitAvailable()</c>
/// 等 <c>git --version</c>，<c>RunGit()</c> 等真正的子命令。二者语义相同，曾经却是两个不同的
/// 硬窗口（3 s / 15 s）。不一致的后果不是「两边都慢」，而是<b>满负载下窗口更紧的那个先失效</b>：
/// 2026-09-16 全量门禁（7 个测试宿主并行）连续两跑红在
/// <c>GitWorkflowServiceTests.InitializeRepository_SecondCall_Idempotent</c>，失败点是
/// <c>InitializeRepository</c> 的第一个守卫 <c>!IsGitAvailable()</c> —— 同一个测试里前一次探测为真、
/// 后一次为假。而这个缺陷在单跑时<b>复现不出来</b>（整个 Core 程序集 1894/1894 全绿，
/// 定向 filter 8/8 全绿），所以只能靠结构守卫钉住，不能靠行为用例。</para>
///
/// <para>这与「判据靠相对大小成立、不靠绝对余量」是同一条纪律的另一种形态：两个窗口只要<b>同源</b>，
/// 就没有「谁更紧」这个问题。</para>
/// </summary>
public class GitProcessWaitWindowTests
{
    private const string ServiceRelativePath = "src/HmiEditor.Core/Services/GitWorkflowService.cs";

    /// <summary>写死数字的窗口，形如 <c>WaitForExit(3000)</c>。</summary>
    private static readonly Regex NumericWaitWindow =
        new(@"WaitForExit\s*\(\s*\d+\s*\)", RegexOptions.Compiled);

    /// <summary>走单一来源的窗口，形如 <c>WaitForExit(GitProcessWaitMs)</c>。</summary>
    private static readonly Regex NamedWaitWindow =
        new(@"WaitForExit\s*\(\s*GitProcessWaitMs\s*\)", RegexOptions.Compiled);

    private static string ReadServiceCode()
        => SourceScanner.ReadCode(Path.Combine(SourceScanner.RepoRoot, ServiceRelativePath));

    // ── ① 窗口不得各写各的数字 ──

    [Fact]
    public void GitWorkflowService_DoesNotHardcodeWaitWindows()
    {
        var code = ReadServiceCode();

        var offenders = NumericWaitWindow.Matches(code).Select(m => m.Value).ToArray();
        Assert.True(offenders.Length == 0,
            "GitWorkflowService 里又出现了写死数字的等待窗口：" + string.Join("、", offenders)
            + "。请改为 WaitForExit(GitProcessWaitMs) —— 同一语义两套窗口，满负载下更紧的那个必然先失效"
            + "（2026-09-16 门禁连续两红就是这个原因）。");
    }

    // ── ② 每一处都走同一个常量；数量对不上说明有漏 ──

    [Fact]
    public void GitWorkflowService_AllProcessWaitsGoThroughSingleConstant()
    {
        var code = ReadServiceCode();

        var count = NamedWaitWindow.Matches(code).Count;
        Assert.True(count == 2,
            $"期望恰好 2 处 WaitForExit(GitProcessWaitMs)（IsGitAvailable 与 RunGit 各一处），"
            + $"实际 {count} 处。新增或删除等待点时请同步本判据 —— 数字对不上比数量错更值得停下来看一眼。");
    }

    // ── ③ 契约值本身不得收紧 ──

    [Fact]
    public void GitProcessWaitMs_IsNotTighterThanTheCommandWindow()
    {
        // 15000 是 RunGit 一直以来的命令窗口，也是本常量的契约下限。
        // 收紧它等于把「git 进程启动慢」重新判成「git 不可用」。
        Assert.True(GitWorkflowService.GitProcessWaitMs >= 15_000,
            $"GitWorkflowService.GitProcessWaitMs 被收紧到 {GitWorkflowService.GitProcessWaitMs} ms。"
            + "该值不得小于 15000：它是探活与执行共用的窗口，收紧后满负载下 git 探活会先失败。");
    }

    // ── 守卫自检（正反两面）──

    [Fact]
    public void GuardItself_DistinguishesNumericFromNamedWindows()
    {
        // 反面：写死数字必须被认出（这正是 2026-09-16 那个缺陷的形态）
        Assert.Matches(NumericWaitWindow, "p.WaitForExit(3000);");
        Assert.Matches(NumericWaitWindow, "if (!p.WaitForExit( 15000 )) return;");

        // 正面：走常量不得被误判
        Assert.DoesNotMatch(NumericWaitWindow, "p.WaitForExit(GitProcessWaitMs);");
        Assert.Matches(NamedWaitWindow, "if (!p.WaitForExit(GitProcessWaitMs))");

        // 数字正则不得吃掉常量写法，常量正则也不得吃掉数字写法
        Assert.DoesNotMatch(NamedWaitWindow, "p.WaitForExit(3000);");
    }

    [Fact]
    public void GuardItself_CommentStrippingKeepsRuleFromFailingOnItsOwnDocs()
    {
        // 本文件与被守卫文件的注释里都必然写着被禁的写法（上面 <summary> 与 IsGitAvailable 的
        // 文档注释里都写着 3 s / 15 s 的旧值）。剥注释是判据能长期存活的前提 ——
        // 否则下一个人只会看到一条莫名其妙的红，然后把注释删掉。
        const string docOnly = "/// <summary>此处原为 WaitForExit(3000)，而 RunGit 为 WaitForExit(15000)。</summary>";

        Assert.Matches(NumericWaitWindow, docOnly);                      // 样本有效
        Assert.DoesNotMatch(NumericWaitWindow, SourceScanner.StripComments(docOnly));
    }

    [Fact]
    public void GuardItself_SourcePathIsReal()
    {
        // 路径写错时读不到文件会抛异常，看起来像守卫失败；显式钉一条，让「找不到」与「违规」可区分。
        var path = Path.Combine(SourceScanner.RepoRoot, ServiceRelativePath);
        Assert.True(File.Exists(path), $"被守卫的源文件不存在：{path}");
    }
}
