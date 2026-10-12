using Xunit;
using Xunit.Sdk;

namespace HmiEditor.Core.Tests;

/// <summary>
/// <see cref="TestWait"/> 的自测 —— 守卫自身必须会失败，否则「永不失败的守卫」等于没有。
/// 与 <c>TestAssertionStyleGuardTests.GuardItself_DetectsTheBrittlePattern</c> 同一思路。
/// </summary>
public class TestWaitTests
{
    [Fact]
    public void Until_KeepsWaiting_ForConditionThatBecomesTrueLater()
    {
        // 条件在 300ms 后才成立 —— 远晚于「固定 Sleep(100)」所假定的时刻。
        // 这正是修掉的那类 flaky：定时器回调被 ThreadPool 推迟时，
        // 「Sleep 之后条件必然已成立」不成立，而轮询等待则不受影响。
        //
        // ⚠️ 置位必须用**专用线程**，不能用 Task.Run/Task.Delay：
        //    Task.Delay 的续体同样排在 ThreadPool 上，全量并行跑测试时 7 个程序集同抢它，
        //    续体会被推迟到 5s 超时之外 —— 于是这个「用来证明 Until 会等待」的装置
        //    自己变成了 flaky（2026-09-15 实测：全量第 1 轮 Core 耗时 1m14s，本用例超时红，
        //    第 2/3 轮又全绿）。专用线程不占 ThreadPool，因而不受此影响。
        var signal = false;
        var setter = new Thread(() => { Thread.Sleep(300); signal = true; }) { IsBackground = true };
        setter.Start();

        TestWait.Until(() => signal, "条件应在超时前成立");
        Assert.True(signal);

        setter.Join();
    }

    [Fact]
    public void Until_PollsConditionRepeatedly_NotJustOnce()
    {
        // 「会等待」的**机制**证据，且完全零时序依赖：断言 condition 被反复调用。
        // 固定 Sleep 写法只在睡眠结束后取一次布尔值，在这种实现下本用例必然红。
        // （上面那条用例证明「能等到」，这条证明「为什么能」—— 两者互补。）
        var calls = 0;
        TestWait.Until(() => ++calls >= 3, "第 3 次轮询时应成立");
        Assert.True(calls >= 3, $"condition 应被调用 ≥3 次，实际 {calls} 次");
    }

    [Fact]
    public void Until_ThrowsOnTimeout_InsteadOfSilentlyPassing()
    {
        // 反向自测：条件永不成立时必须失败，且失败信息里带上「在等什么」。
        var ex = Assert.Throws<TrueException>(
            () => TestWait.Until(() => false, "这个条件永远不会成立", timeoutMs: 100));

        Assert.Contains("这个条件永远不会成立", ex.Message);
    }
}
