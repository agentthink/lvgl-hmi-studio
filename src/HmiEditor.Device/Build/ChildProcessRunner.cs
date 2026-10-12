using System.Diagnostics;

namespace HmiEditor.Device.Build;

/// <summary>
/// 子进程的**取消语义** —— 三个调用点（模拟器构建 / .so 构建 / FEL 烧录）共用一份。
///
/// <para><b>为什么必须有它：</b><c>using var process</c> + <c>await WaitForExitAsync(ct)</c>
/// 在取消时**只停止等待** —— <c>Process.Dispose()</c> 不会杀子进程。于是界面显示
/// "已取消"，而 <c>cmake</c> / <c>gcc</c> / <c>sunxi-fel</c> 仍在后台跑、继续写共享的
/// 工作目录（下一条命令随后读到半成品）；<c>BuildRunDialog</c> 的 Stop 就走的这条。
/// 取消必须**真的**把子进程杀掉（阶段 120 修复；同仓 <c>GitWorkflowService</c> 早就是这么做的）。</para>
/// </summary>
public static class ChildProcessRunner
{
    /// <summary>取消后等待子进程真正退出的宽限时间。</summary>
    private static readonly TimeSpan KillGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 等待 <paramref name="process"/> 退出。取消时先杀**整棵进程树**、再抛
    /// <see cref="OperationCanceledException"/>（不把取消吞成正常返回）。
    /// </summary>
    public static async Task WaitForExitOrKillAsync(Process process, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(process);

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKillTree(process);
            // 给子进程一点时间真正退出：否则调用方可能在它仍持有工作目录/串口时继续下一步。
            try
            {
                await process.WaitForExitAsync(CancellationToken.None)
                    .WaitAsync(KillGrace).ConfigureAwait(false);
            }
            catch
            {
                // 杀不掉 / 超时：不掩盖真正的取消
            }
            throw;
        }
    }

    private static void TryKillTree(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
            // 已退出、或权限不足：尽力而为，不改变"已取消"这个结论
        }
    }
}
