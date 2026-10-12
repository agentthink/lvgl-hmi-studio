using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// headless 派发的**异步续体落点**契约。
///
/// <para><b>为什么立这条</b>：查登记表 <b>#47</b>（平台初始化偶发红）时，"谁是抢走
/// <c>Dispatcher.s_uiThread</c> 的那个线程"是核心问题。<c>EnsureIsolatedApplication</c>
/// 会把**调用方线程**的 <c>ExecutionContext</c>（含 xUnit 自己的 <c>SynchronizationContext</c>）
/// 恢复到会话线程上执行派发体，所以一个自然怀疑是：
/// <c>await</c> 的续体被投递回 **xUnit 的同步上下文**＝worker 线程 ⇒ worker 线程上一碰 Avalonia
/// 就会去读 <c>Dispatcher.UIThread</c> ⇒ 在 <c>s_uiThread == null</c> 的时刻把它抢走
/// （<c>Dispatcher..ctor</c>：<c>if (s_uiThread == null) s_uiThread = this;</c>）。</para>
///
/// <para><b>实测结论（2026-09-21）：这条候选被排除。</b><c>Task.Yield</c> 与 <c>Task.Delay</c>
/// 之后的续体**都回到派发线程**（两条探针均通过）。⇒ 至少这两个形态不会把 Avalonia 调用
/// 带到 worker 线程上；查 #47 不必再走这条路。留着这两条是为了把这条契约钉住 ——
/// 哪天续体真的逃逸了，是会**直接**喂出 #47 的。</para>
/// </summary>
public class DispatchContinuationContractTests
{
    [Fact]
    public async Task ContinuationAfterYield_ReturnsToTheDispatchThread()
    {
        var seen = new int[2];

        await UiTestHost.RunAsync(async () =>
        {
            seen[0] = Environment.CurrentManagedThreadId;
            await Task.Yield();
            seen[1] = Environment.CurrentManagedThreadId;
        });

        Assert.True(seen[0] == seen[1],
            $"await 续体没有回到派发线程：before={seen[0]}, after={seen[1]} —— "
            + "续体落在别的线程上时，任何 Avalonia 调用都会去抢 s_uiThread（#47 的窗口）。");
    }

    /// <summary>真正挂起的等待（取消用例走的就是 <c>Task.Delay(Timeout.Infinite, ct)</c> 这条路）。</summary>
    [Fact]
    public async Task ContinuationAfterDelay_ReturnsToTheDispatchThread()
    {
        var seen = new int[2];

        await UiTestHost.RunAsync(async () =>
        {
            seen[0] = Environment.CurrentManagedThreadId;
            await Task.Delay(1);
            seen[1] = Environment.CurrentManagedThreadId;
        });

        Assert.True(seen[0] == seen[1],
            $"Task.Delay 之后的续体没有回到派发线程：before={seen[0]}, after={seen[1]}。");
    }
}
