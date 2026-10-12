using System.Diagnostics;
using HmiEditor.Device.Build;
using Xunit;

namespace HmiEditor.Device.Tests;

/// <summary>
/// 子进程取消的**行为判据**（阶段 120）。
///
/// <para>背景：`using var process` + `await WaitForExitAsync(ct)` 在取消时只停止等待 ——
/// `Process.Dispose()` 不杀子进程，于是界面报"已取消"而 cmake/gcc/sunxi-fel 继续跑、
/// 继续写共享工作目录。<see cref="ChildProcessRunner"/> 负责把"已取消"变成"真的停了"。</para>
///
/// <para>判据形状：起一个真的长命子进程（`/bin/sleep 60`）→ 取消 → 断言
/// ①抛出的是取消异常（不吞）、②**子进程已经退出**（`HasExited`）。
/// 少了第②条，旧实现照样"绿" —— 正是它把"还在跑"伪装成"已取消"。</para>
/// </summary>
public class ChildProcessRunnerTests
{
    [Fact]
    public async Task Cancel_KillsTheChildProcess()
    {
        // 仓库的开发与 CI 平台是 Linux（见 README「Linux 为主力开发平台」）。
        // 这里用显式断言而不是 `if (windows) return;` —— 后者是"静默通过"，
        // 会让"子进程没被杀掉"这件事在别的平台上永远看不见。
        Assert.True(OperatingSystem.IsLinux(), "本用例依赖 /bin/sleep；换平台请补等价的长命子进程");

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/sleep",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("60");

        using var process = new Process { StartInfo = psi };
        Assert.True(process.Start(), "无法启动 /bin/sleep");

        using var cts = new CancellationTokenSource();
        var waitTask = ChildProcessRunner.WaitForExitOrKillAsync(process, cts.Token);

        await Task.Delay(200);                 // 让子进程真的跑起来
        Assert.False(process.HasExited, "子进程应当还在跑（否则本用例没测到取消路径）");

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);   // 取消不许被吞成正常返回

        // 🩸 主判据：取消之后子进程必须**已经退出**。
        //    旧实现（裸 WaitForExitAsync）在这里是 false —— sleep 60 还在跑。
        Assert.True(process.HasExited, "取消后子进程仍在运行 ⇒ 它还在写共享工作目录（旧缺陷）");
    }

    [Fact]
    public async Task NormalExit_IsNotAffected()
    {
        Assert.True(OperatingSystem.IsLinux(), "本用例依赖 /bin/true");

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/true",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = new Process { StartInfo = psi };
        Assert.True(process.Start());

        await ChildProcessRunner.WaitForExitOrKillAsync(process, CancellationToken.None);

        Assert.True(process.HasExited);
        Assert.Equal(0, process.ExitCode);
    }
}
