using System.Diagnostics;
using HmiEditor.Device.Channels;
using Xunit;

namespace HmiEditor.Device.Tests;

/// <summary>
/// 收帧判据（<see cref="CommandFrameCollector"/>）的**确定性**单元测试 —— 不碰网络。
/// TCP / 串口两个通道共用同一份实现，所以语义在这里覆盖一遍即可；通道侧只留集成用例。
///
/// <para><b>「不依赖主机负载」这条要靠实现兑现，不能靠愿望</b>：生产者必须用**专用线程**计时
/// （见 <see cref="AppendLater"/>）。原实现用 <c>Task.Run</c> + <c>await Task.Delay</c>，
/// 续体排在 ThreadPool 上 —— 主机满载时会被推迟到秒级，越过 <c>firstByteMs</c> 窗口，
/// 于是"设备起步慢"被断言成"设备没输出"。2026-09-17 门禁实测红过一次
/// （<c>Expected: "ok:SLOW\n"  Actual: ""</c>），本篇注释当时写的却是"不依赖主机负载" ——
/// 描述与实现相反。根因与取证见 docs/2026-09-17-sandbox-newpath-serialization.md。</para>
/// </summary>
public class CommandFrameCollectorTests
{
    /// <summary>
    /// 在后台按指定延迟把一段数据投给收集器（消费侧已进入等待循环之后才到达）。
    ///
    /// <para><b>用专用线程，不用 <c>Task.Run</c> + <c>await Task.Delay</c>：</b>
    /// <c>Task.Delay</c> 的续体是 ThreadPool 工作项，而本套件里大量「往全新路径写文件」的
    /// 阻塞 IO 会长时间占住 ThreadPool 线程（本沙箱给每个首次出现的文件路径收约 60 ms，
    /// 且该开销全局串行）⇒ 续体排队到秒级。专用线程的 <c>Thread.Sleep</c> 只受 OS 调度影响，
    /// 不受 ThreadPool 队列影响，这才让本类"确定性"的自我描述成立。</para>
    /// </summary>
    private static void AppendLater(CommandFrameCollector collector, int delayMs, string text)
    {
        AppendSequence(collector, (text, delayMs));
    }

    /// <summary>
    /// 在**同一个专用线程**上按时间轴依次投递多段数据，段间间隔由 <see cref="Thread.Sleep"/> 保证。
    ///
    /// <para><b>为什么必须同一个线程</b>：给每段各起一个线程时，段间的实际间隔 = 两次
    /// 线程启动/唤醒的抖动**之和** —— 抖动会叠加并吃掉用例留给自己的余量（实测：名义间隔
    /// 70 ms、静默 100 ms，只留 30 ms 余量，满载时第三段被截掉）。同一个线程上依次 Sleep，
    /// 间隔只受单次 Sleep 是否超时影响，不叠加。</para>
    ///
    /// <para>与「不要用 <c>Thread.Sleep(固定值)</c> 等回调」那条禁忌**不冲突**：这里 Sleep 是
    /// **制造刺激的节拍**（确定性输入），不是等待产品状态；等状态一律走 <c>TestWait.Until</c>。</para>
    /// </summary>
    private static void AppendSequence(CommandFrameCollector collector, params (string Text, int AtMs)[] timeline)
    {
        var thread = new Thread(() =>
        {
            var elapsed = 0;
            foreach (var (text, atMs) in timeline)
            {
                if (atMs > elapsed) Thread.Sleep(atMs - elapsed);
                elapsed = atMs;
                collector.Append(text);
            }
        })
        {
            IsBackground = true,
            Name = "test-append-sequence",
        };
        thread.Start();
    }

    [Fact]
    public async Task DataArrivingDuringFrame_IsReturnedAfterSilentPeriod()
    {
        var collector = new CommandFrameCollector();
        var output = await collector.SendAsync(
            writeCommand: _ =>
            {
                collector.Append("ok:AAA\n");       // 模拟读循环在本帧内收到回包
                return Task.CompletedTask;
            },
            silentMs: 60, firstByteMs: 600, ct: default);

        Assert.Equal("ok:AAA\n", output);
    }

    [Fact]
    public async Task FirstByteArrivingAfterSilentPeriod_IsNotMistakenForEmptyOutput()
    {
        // 这条正是 2026-09-16 复发的那条 flaky 的根因：首字节（200ms）晚于静默期（60ms）。
        // 旧实现里静默从**写入时刻**起算 ⇒ 60ms 到点即返回空串，设备只是起步慢就永远拿不到响应。
        var collector = new CommandFrameCollector();
        var output = await collector.SendAsync(
            writeCommand: _ =>
            {
                AppendLater(collector, 200, "ok:SLOW\n");
                return Task.CompletedTask;
            },
            silentMs: 60, firstByteMs: 800, ct: default);

        Assert.Equal("ok:SLOW\n", output);
    }

    [Fact]
    public async Task SilenceIsMeasuredFromLastDataChunk_NotFromFirst()
    {
        // 三段数据（0 / 360 / 720ms）：**帧内相邻间隔**（360ms）都小于静默期 600ms，
        // 但"从第一段起算"的累计时长（720ms）超过它。若静默从"第一段"起算，t=600 就会返回，
        // 第三段（720ms）被截掉 —— 输出只剩两行。正确语义是每收到一段就重置静默，
        // 直到 720 + 600 = 1320ms 才结束。
        //
        // 尺度为什么是 0.6 × 静默期、而不是原先的 60/130 对 100：原先「间隔 70ms vs 静默 100ms」
        // 只留 30ms 余量，属于**靠绝对余量**成立 —— 满载时一次线程启动抖动就能吃穿它。
        // 2026-09-17 门禁实测被截成 "line1\nline2\n"（专题文档 §3.2）。
        // 现在两处余量都是 240ms（间隔 360 vs 静默 600），判别力（累计 720 > 静默 600）不变。
        const int silentMs = 600;
        const int gapMs = 360;

        var collector = new CommandFrameCollector();
        var output = await collector.SendAsync(
            writeCommand: _ =>
            {
                AppendSequence(collector,
                    ("line1\n", 0),
                    ("line2\n", gapMs),
                    ("line3\n", gapMs * 2));
                return Task.CompletedTask;
            },
            silentMs: silentMs, firstByteMs: 2000, ct: default);

        Assert.Equal("line1\nline2\nline3\n", output);
    }

    /// <summary>
    /// 无数据时**按首字节窗口返回**，而不是空等一个与窗口无关的固定超时。
    ///
    /// <para><b>判据为什么改成"两次不同窗口的差值"而不是绝对毫秒区间（2026-09-17）：</b>
    /// 原判据是 <c>InRange(elapsed, 150, 2000)</c> —— 上界是**绝对余量**，而本沙箱存在
    /// 全局串行的资源竞争，定时器续体可被推迟到秒级（见
    /// <c>docs/2026-09-17-sandbox-newpath-serialization.md</c>），同族判据已被撞穿过几条。
    /// 差值对负载**不敏感**：两次测量承受同样的负载，而"等的是首字节窗口"与"等的是一个固定值"
    /// 在差值上截然不同（窗口差 1000ms ⇒ 差值≈1000ms；固定超时 ⇒ 差值≈0）。</para>
    /// </summary>
    [Fact]
    public async Task NoData_ReturnsEmptyAfterFirstByteWindow_NotAfterCommandTimeout()
    {
        var fast = await MeasureNoDataAsync(silentMs: 40, firstByteMs: 200);
        var slow = await MeasureNoDataAsync(silentMs: 40, firstByteMs: 1200);

        // 下界：窗口真的在支配等待（不是立刻返回）；上界刻意宽松（只排除"完全无界/串到别的超时"），
        // 因为差值的抖动主要来自调度，而两次测量共享同一份负载。
        Assert.InRange(slow - fast, 600, 8000);
    }

    /// <summary>测一次"命令无任何输出"的返回耗时；顺带钉住它确实返回空串。</summary>
    private static async Task<long> MeasureNoDataAsync(int firstByteMs)
    {
        var collector = new CommandFrameCollector();
        var sw = Stopwatch.StartNew();
        var output = await collector.SendAsync(
            writeCommand: _ => Task.CompletedTask,
            silentMs: 40, firstByteMs: firstByteMs, ct: default);
        sw.Stop();

        Assert.Equal("", output);
        return sw.ElapsedMilliseconds;
    }

    [Fact]
    public async Task FirstByteWindow_IsNeverShorterThanSilentPeriod()
    {
        // 判据自洽性防线：既然容忍 500ms 的帧内行间隙，就不能只容忍 10ms 的响应起步。
        //
        // 🩸 阶段 123：原判据是 `elapsed >= 400`（对 silentMs=500/firstByteMs=10）—— **绝对毫秒**。
        //   负载会把真实实现推过下界（无害），但也会把"规则丢了、只等 10ms"的坏实现推过下界
        //   ⇒ **假绿**。改成两次不同静默期的**差值**：规则在 ⇒ 差值≈500ms；规则丢 ⇒ 两次都≈0。
        var small = await MeasureNoDataAsync(silentMs: 100, firstByteMs: 10);
        var big = await MeasureNoDataAsync(silentMs: 600, firstByteMs: 10);

        Assert.InRange(big - small, 300, 8000);
    }

    /// <summary>测一次「命令无任何输出」的返回耗时；顺带钉住它确实返回空串。</summary>
    private static async Task<long> MeasureNoDataAsync(int silentMs, int firstByteMs)
    {
        var collector = new CommandFrameCollector();
        var sw = Stopwatch.StartNew();
        var output = await collector.SendAsync(
            writeCommand: _ => Task.CompletedTask,
            silentMs: silentMs, firstByteMs: firstByteMs, ct: default);
        sw.Stop();

        Assert.Equal("", output);
        return sw.ElapsedMilliseconds;
    }

    [Fact]
    public async Task Cancellation_PropagatesOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CommandFrameCollector().SendAsync(
                writeCommand: _ => Task.CompletedTask,
                silentMs: 40, firstByteMs: 40, ct: cts.Token));
    }
}
