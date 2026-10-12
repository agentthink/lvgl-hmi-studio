using System;
using Avalonia.Threading;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// Dispatcher 归属契约 —— 同时充当登记表 <b>#47</b> 的哨兵。
///
/// <para><b>为什么专门立这几条</b>：#47 的症状是「<c>DockViewResolutionUiTests</c> 偶发红，
/// 抛在平台初始化」—— 那个类要建 12 个面板才会碰到，代价高、失败信息又不指向原因。
/// 2026-09-21 反编译 Avalonia 12.1.0 把机制查清之后，可以把它压缩成三条**小**判据：
/// 平台初始化窗口期的 <c>s_uiThread</c> 归属（第 1 条）、每次派发都重置全局
/// dispatcher 状态这个事实（第 2 条）、以及诊断读数本身有分辨力（第 3 条）。</para>
///
/// <para><b>机制（全部由 IL 核实，非推断）：</b>
/// ① <c>HeadlessUnitTestSession.StartNew(Type)</c> 的隔离级别是 <c>PerTest</c>（枚举值 1），
/// 所以 <c>EnsureIsolatedApplication</c> **每次派发**都执行
/// <c>AvaloniaLocator.EnterScope()</c> + <c>Dispatcher.ResetBeforeUnitTests()</c> + <c>AppBuilder.SetupUnsafe()</c>；
/// ② <c>ResetGlobalState()</c> 把 <c>Dispatcher.s_uiThread</c> 与 <c>s_currentThreadDispatcher</c> 清成 <c>null</c>；
/// ③ <c>Dispatcher..ctor</c> 在 <c>s_globalLock</c> 内做的是
/// 「<c>if (s_uiThread == null) s_uiThread = this;</c>」—— **谁先构造谁拿走**，不看线程；
/// ④ <c>Dispatcher.CheckAccess()</c> 就是 <c>ReferenceEquals(Thread.CurrentThread, _thread)</c>；
/// ⑤ <c>DefaultRenderLoop.Add</c> 是**当场**读 <c>Dispatcher.UIThread</c> 再 <c>VerifyAccess()</c>。
/// ⇒ 只要别的线程在 ②→③ 之间的窗口里构造了一个 <c>Dispatcher</c>，会话线程随后的
/// <c>Add</c> 就会抛 "different thread owns it"。</para>
///
/// <para><b>⚠ 本文件里有一条自律：不要在派发之外读 <c>Dispatcher.UIThread</c>。</b>
/// 那个 getter 在 <c>s_uiThread == null</c> 时会**当场构造**一个 <c>Dispatcher</c>（见 ③），
/// 于是"观测"本身就会抢走 <c>s_uiThread</c>、把被观测的 #47 造出来。要读环境状态请走
/// <see cref="UiTestHost.DescribeSessionState"/>（只反演 <c>TryGetUIThread()</c>，不构造）。
/// 第 3 条判据就是钉这个陷阱的。</para>
public class DispatcherOwnershipContractTests
{
    /// <summary>平台初始化之后，<c>Dispatcher.UIThread</c> 必须归派发线程所有。</summary>
    [Fact]
    public void UIThreadDispatcher_IsOwnedByTheCallingThread()
    {
        var observed = UiTestHost.Run(() =>
        {
            var ui = UiTestHost.UIThread;
            return (Access: ui.CheckAccess(), State: UiTestHost.DescribeSessionState());
        });

        Assert.True(observed.Access,
            "#47 形态：派发线程不是 Dispatcher.UIThread 的归属线程 —— 平台初始化窗口里 " +
            "s_uiThread 被别的线程抢走了。现场：" + observed.State);
    }

    /// <summary>
    /// **跨派发不许有第三方持有 <c>s_uiThread</c>** —— 这是 #47 的直接否定形式，也是「有偷手」的报警器。
    ///
    /// <para>合法状态只有两种：<c>s_uiThread == null</c>（**PerTest** 隔离：每次派发都清空它；
    /// 此刻任何线程一读 <c>Dispatcher.UIThread</c> 就当场构造并抢走它 —— 这就是那个「窗口」），
    /// 或持有者是**会话线程**（**PerAssembly** 隔离：平台只建一次，dispatcher 跨派发存活）。
    /// 出现第三种状态（持有者是别的线程）就说明存在**派发之外的 Avalonia 访问**：
    /// 它会落进窗口、让下一次平台初始化抛 <c>different thread owns it</c>。</para>
    ///
    /// <para><b>实测（2026-09-21，人为制造偷手）</b>：窗口存在时 **99.81%**（114146/114367）
    /// 的派发死在平台初始化那一步；把隔离级别换成 per-assembly 后同一探针
    /// **0/2000000**（偷手同期读了 5.6 亿次）⇒ 窗口确由「每次派发重置」造成。</para>
    ///
    /// <para>刻意写成**隔离级别无关**的形式：per-assembly 是 #47 的候选修法，
    /// 换过去时这条判据不需要改（它只问「谁持有」，不问「是不是每次都换实例」——
    /// 后者是实现细节，今天那条 <c>Assert.NotSame</c> 就是断在实现细节上）。</para>
    /// </summary>
    [Fact]
    public void CrossDispatch_OnlyTheSessionThreadMayHoldTheUIThread()
    {
        // ⚠ 观察必须**持闸门**（阶段 166，闭环 #81）：闸门之外读数会把"别的测试类合法地碰平台"
        // （最典型：派发之外的 `Pump`，它按调用线程取 Dispatcher）读成"偷手" —— 实测假阳读数
        // `owner=19, session=7`，现场没有任何非法访问。持闸门后这种干扰挤不进来，
        // 还能被读到的就只剩"绕过闸门去碰 Avalonia 的线程"，也就是 #47 真正要抓的那个。
        var observed = UiTestHost.ObserveAmbientOwnerOutsideDispatch();

        Assert.True(observed.Owner is null || observed.Owner == observed.SessionThread,
            $"派发之外有人持有 Dispatcher.UIThread：ownerThread={observed.Owner}，sessionThread={observed.SessionThread}。"
            + "持有者不是会话线程 ⇒ 存在**绕过闸门的** Avalonia 访问（#47 的偷手）："
            + "它会落在「s_uiThread 清空 → 会话线程重建」的窗口里，让下一次平台初始化抛 "
            + "different thread owns it。现场：" + UiTestHost.DescribeSessionState());
    }

    /// <summary>
    /// 诊断读数的两条控制：签名判定要**窄**，环境探测要**不构造**。
    ///
    /// <para>负控 1：无关异常不许被认成 Dispatcher 归属问题（否则诊断层会把真实失败换掉消息）。
    /// 负控 2：环境探测若自己去构造了一个 Dispatcher（即走了 <c>Dispatcher.UIThread</c> 的 getter），
    /// 它会归**当前**线程所有 ⇒ 读数里会出现 <c>checkAccessOnCallingThread=True</c>，这条立刻红。
    /// 这正是"观测动作本身会制造 #47"那个陷阱的报警器。</para>
    ///
    /// <para>实测读数（2026-09-21，本机）：探测在**非派发线程**上得到
    /// <c>ambientUIThread=s_uiThread=null（窗口开着）</c> —— 派发结束后 <c>s_uiThread</c> 就被
    /// <c>EnsureIsolatedApplication</c> 的收尾重置成 null 了。所以这条断言接受两种合法读数：
    /// <c>s_uiThread=null</c>（窗口开着）或 <c>checkAccessOnCallingThread=False</c>（别的线程持有）。</para>
    /// </summary>
    [Fact]
    public void ErrorSignature_IsNarrowAndAmbientProbeDoesNotConstruct()
    {
        // 先确保会话已建立（否则可能连平台都没起过，读数没有意义）
        UiTestHost.Run(() => 0);

        Assert.False(UiTestHost.IsDispatcherOwnershipFailure(
            new InvalidOperationException("一个与 dispatcher 无关的普通失败")));

        Assert.True(UiTestHost.IsDispatcherOwnershipFailure(
            new InvalidOperationException(
                "The calling thread cannot access this object because a different thread owns it.")));

        var state = UiTestHost.DescribeSessionState();
        Assert.True(state.Contains("callingThread=", StringComparison.Ordinal),
            "现场描述里没有调用线程 id，红了等于没信息：" + state);
        Assert.True(state.Contains("sessionThread=", StringComparison.Ordinal),
            "现场描述里没有会话线程 id —— 没有它就分不出「会话线程持有」与「被偷手抢走」：" + state);
        Assert.True(state.Contains("dispatch#", StringComparison.Ordinal),
            "现场描述里没有派发序号：" + state);

        Assert.False(state.Contains("checkAccessOnCallingThread=True", StringComparison.Ordinal),
            "环境探测把 Dispatcher 构造出来了 —— 探测本身会抢走 s_uiThread、制造 #47。现场：" + state);

        Assert.True(state.Contains("ambientUIThread=s_uiThread=null", StringComparison.Ordinal) ||
                    state.Contains("checkAccessOnCallingThread=False", StringComparison.Ordinal),
            "环境探测既没读到\"窗口开着\"也没读到\"别的线程持有\"，读数不可信：" + state);
    }

    /// <summary>
    /// **判据（阶段 166，闭环 `#81`）**：别的线程**合法地**反复碰平台（派发之外调 <see cref="UiTestHost.Pump"/>，
    /// 那条路按调用线程取 Dispatcher、且**不写** <c>_sessionThreadId</c>）时，
    /// **持闸门**的观察入口 `ObserveAmbientOwnerOutsideDispatch` 必须始终报
    /// "<c>null</c> 或本会话线程"，不许报出"归属 ≠ 会话线程"这个**假阳形状**。
    ///
    /// <para><b>为什么这条重要</b>：`#47` 哨兵原先在**闸门之外**读数，于是它报的"偷手"里混进了
    /// **合法路径**（阶段 164 门禁首跑红过一次：`ownerThread=5, sessionThread=33`）。
    /// 机制已单独量到（新进程里一次合法的后台 <c>Pump(1)</c> 之后：`owner=19, session=7`）——
    /// ⚠ 那个形状**依赖"窗口开着"**（派发结束后 <c>s_uiThread</c> 为 <c>null</c>），而它在完整套件里
    /// **不稳定**（同一进程里有时是本线程持有）⇒ 判据里**不去断言**"形状一定出现"（第一版这么写、
    /// 偶发红：`owner=48, session=48`），只断言**修法那一半**（无条件、确定性）；
    /// "形状确实会误导人"由文档里的实测读数与两支负控承担。</para>
    /// </summary>
    [Fact]
    public void CrossDispatchRead_IsNotDisturbedByOtherThreadsLegalPlatformTouches()
    {
        UiTestHost.Run(() => 0);                       // 会话线程 = 本线程
        var session = UiTestHost.SessionThreadId;
        var ambientAtStart = UiTestHost.AmbientUIThreadOwnerId();

        using var stop = new System.Threading.ManualResetEventSlim(false);
        var worker = new System.Threading.Thread(() =>
        {
            while (!stop.IsSet)
            {
                try { UiTestHost.Pump(1); }            // 合法路径：闸门由它自己取
                catch { /* 干扰线程的异常不是本用例的观察对象 */ }
            }
        }) { IsBackground = true };
        worker.Start();

        var dirty = new System.Collections.Generic.List<string>();
        for (var i = 0; i < 40; i++)
        {
            var o = UiTestHost.ObserveAmbientOwnerOutsideDispatch();
            if (o.Owner is not null && o.Owner != o.SessionThread)
                dirty.Add($"owner={o.Owner}/session={o.SessionThread}");
        }

        stop.Set();
        worker.Join(TimeSpan.FromSeconds(10));

        Assert.True(dirty.Count == 0,
            $"持闸门的观察入口在别的线程合法碰平台的干扰下报出了 {dirty.Count}/40 次「归属 ≠ 会话线程」"
            + $"（前 5 条：{string.Join(" / ", dirty.Take(5))}）—— 假阳没修掉（#81）。"
            + $"现场：ambientAtStart={ambientAtStart}, session={session}, " + UiTestHost.DescribeSessionState());
    }

    /// <summary>
}

/// <summary>
/// 派发**串行化**契约 —— 守的是 <see cref="UiTestHost"/> 的派发闸门（2026-09-21，阶段 59）。
///
/// <para><b>被守的性质：</b>「平台重建从不重叠」。<c>HeadlessUnitTestSession.DispatchCore</c>
/// 在**调用线程**上直接执行 <c>EnsureIsolatedApplication()</c>，其实现是
/// <c>EnterScope()</c> + <c>Dispatcher.ResetBeforeUnitTests()</c> + <c>AppBuilder.SetupUnsafe()</c>
/// —— 三行**全是进程级操作，全程无锁、无重入判定**（IL 核实）。而 xunit 默认让不同 collection
/// 并行 ⇒ 实测同一条程序集里**最多 20 个派发同时在飞**（见下方读数），
/// 即 20 个线程各自以为自己在独占"那条 UI 线程"、并互相把对方的平台拆掉重建。</para>
///
/// <para><b>这条判据自己制造条件</b>：起 N 个线程**同时**调 <see cref="UiTestHost.Run(Action)"/>。
/// 闸门在，它们排队 ⇒ 最大并发恒为 1；闸门被摘掉，它们会一起冲进平台初始化 ⇒ 立刻 ≥2。
/// 所以它同时是"闸门有效"与"计数器活着"两条的正控：
/// 若计数器坏了（不递增），下面第一条断言就会红 —— 不会出现"计数器死了所以恒为 1 所以永远绿"。</para>
///
/// <para><b>负控（已实测）</b>：把闸门摘掉跑同一条程序集 ⇒ <c>maxInFlight=20</c> /
/// <c>overlapEvents=149</c>（195 次派发里 149 次是在别的派发未结束时进入的）。</para>
/// </summary>
public class HeadlessDispatchContractTests
{
    [Fact]
    public void ConcurrentDispatchRequests_AreNeverInFlightTogether()
    {
        var dispatchesBefore = UiTestHost.DispatchCount;
        var overlapsBefore = UiTestHost.DispatchOverlapEvents;

        var workerCount = Math.Max(4, Environment.ProcessorCount);
        var start = new System.Threading.ManualResetEventSlim(false);
        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var workers = new System.Threading.Thread[workerCount];

        for (var i = 0; i < workerCount; i++)
        {
            workers[i] = new System.Threading.Thread(() =>
            {
                try
                {
                    start.Wait();               // 尽量让 N 个线程**同时**冲进派发
                    UiTestHost.Run(() => { });
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            })
            { IsBackground = true };
            workers[i].Start();
        }

        start.Set();
        foreach (var t in workers)
            Assert.True(t.Join(TimeSpan.FromMinutes(1)),
                "并发派发请求没有在 1 分钟内返回 —— 闸门疑似自锁（闸门本身带 2 分钟超时，"
                + "这里 1 分钟先报，现场：" + UiTestHost.DescribeSessionState() + "）");

        Assert.Empty(errors);

        // ① 计数器活着：这 N 次请求必须真的被数到（否则 ②③ 是"计数器死了所以永远绿"）。
        Assert.True(UiTestHost.DispatchCount - dispatchesBefore >= workerCount,
            $"并发派发没有被计到：本判据起了 {workerCount} 个线程，"
            + $"派发计数只涨了 {UiTestHost.DispatchCount - dispatchesBefore}。"
            + "计数器坏了 ⇒ 后两条断言失去意义。");

        // ② 核心性质：平台重建从不重叠。
        Assert.True(UiTestHost.MaxConcurrentDispatches == 1,
            $"有派发**同时**在飞（本进程历史最大值 {UiTestHost.MaxConcurrentDispatches}）—— "
            + "多个线程会同时执行 `EnterScope` + `ResetBeforeUnitTests` + `SetupUnsafe`，"
            + "而这三行是进程级、无锁的（IL 核实）。这正是登记表 #45（同进程僵死）与 #47"
            + "（s_uiThread 被偷）共同指向的竞态前提。");

        // ③ ②的另一种表述，单独报出来是为了让失败信息直接指向"重叠了多少次"。
        Assert.True(UiTestHost.DispatchOverlapEvents == overlapsBefore,
            $"发生了 {UiTestHost.DispatchOverlapEvents - overlapsBefore} 次派发重叠 —— "
            + "派发闸门没有生效。");
    }

    /// <summary>
    /// **<see cref="UiTestHost.Pump"/> 的推进动作不得与在飞派发并发**（登记表 #47 的另一条偷手路径）。
    ///
    /// <para><b>为什么要单独守它</b>：<c>Pump</c> 会**按调用线程**取 Dispatcher ——
    /// IL 普查（2026-09-21）显示 <c>AvaloniaHeadlessPlatform.ForceRenderTimerTick</c> 的支路里有
    /// <c>Dispatcher.get_CurrentDispatcher()</c>，而 <c>DispatcherTimer</c> 的**三个构造函数**
    /// 也都读它（<c>HeadlessRenderTimer.StartCore</c> 正是用单参构造建的）。
    /// 实测：在派发之外调一次 <c>Pump(1)</c>，<c>s_uiThread</c> 的归属立刻从 <c>null</c> 变成
    /// **调用线程**（读数：调用线程 13 / 会话线程 7 / before=null / after=13）。
    /// 闸门原先只护 <c>Run</c> ⇒ <c>Pump</c> 是漏网的那一条；它若落在在飞派发的窗口里，
    /// 会话线程随后的 <c>VerifyAccess()</c> 就抛 "different thread owns it"，
    /// 也就是 #47「偶发红、抛点在平台初始化」。</para>
    ///
    /// <para><b>本判据自己制造条件</b>：起一个"体内睡住"的派发（把窗口敞开 400 ms），
    /// 同时从本线程反复 <c>Pump</c>。闸门护住 <c>Pump</c> 时并发计数恒 0；
    /// 把闸门从 <c>Pump</c> 上摘掉（负控）则立刻 &gt; 0。</para>
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task Pump_NeverRunsWhileADispatchIsInFlight()
    {
        var eventsBefore = UiTestHost.PumpDuringDispatchEvents;

        // 体内睡 400 ms：让"平台重建窗口"敞开足够久，好让 Pump 有机会挤进去。
        var dispatch = System.Threading.Tasks.Task.Run(
            () => UiTestHost.Run(() => System.Threading.Thread.Sleep(400)));

        System.Threading.Thread.Sleep(80);      // 等它真的进入派发体
        for (var i = 0; i < 12; i++)
        {
            UiTestHost.Pump(1);
            System.Threading.Thread.Sleep(10);
        }

        // 用 WhenAny 而不是 Wait/Result：本仓门禁是"警告即失败"，xUnit1031 会拦阻塞式等待。
        var finished = await System.Threading.Tasks.Task.WhenAny(
            dispatch, System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(1)));
        Assert.True(ReferenceEquals(finished, dispatch),
            "派发没有在 1 分钟内返回 —— 闸门疑似自锁。现场：" + UiTestHost.DescribeSessionState());

        Assert.True(UiTestHost.PumpDuringDispatchEvents == eventsBefore,
            $"Pump 的推进动作与在飞派发并发了 {UiTestHost.PumpDuringDispatchEvents - eventsBefore} 次 —— "
            + "Pump 没有受派发闸门保护：它会在调用线程上构造并抢走 s_uiThread（登记表 #47 的偷手动作）。"
            + "现场：" + UiTestHost.DescribeSessionState());
    }

    /// <summary>
    /// **判据（阶段 167，复核 `#82` 的前提）**：`Pump` 那条"我已在派发体内"的**跳过闸门**支路
    /// **只可能**由**会话线程**走进来 —— 因为
    /// ① <c>HeadlessUnitTestSession</c> 有一个**专用会话线程**：不论谁调 <see cref="UiTestHost.Run(Action)"/>，
    ///    派发体都跑在**同一个**线程上（本判据从 3 个不同调用线程各派发若干次，看派发体线程集合是不是只有 1 个）；
    /// ② <c>SessionThreadId</c> 记的就是它，**别的线程不可能等于它** ⇒ 从非派发线程调 `Pump` 一律走闸门；
    /// ③ 正控：在**派发体里**调 `Pump` 一定会走跳过支路（否则计数器是死的、②就成了"恒 0"的假绿）。
    ///
    /// <para><b>为什么值得一条判据</b>：登记 `#82` 怀疑"本线程恰好等于上一次派发的线程时会跳过闸门"。
    /// 本判据把那个怀疑的**前提**钉住 —— 前提不成立，`#82` 就不成立（不是"没复现"，是"够不着"）。
    /// 将来若有人把会话改成"在调用线程上跑派发体"，① 会立刻红，提醒重新评估 `#82`。</para>
    ///
    /// <para><b>⚠ 读数必须用线程局部计数</b>：全局计数在**跨类并行**时会被别的测试类**合法**的
    /// 派发体内 <c>Pump</c> 撑大 —— 阶段 167 实测：用全局 delta 曾得到"非派发线程跳过闸门 **107** 次"
    /// 的**假读数**（那 107 次全是别人类在派发体里的合法跳过）⇒ 判据只看**本线程**的计数
    /// （<c>UiTestHost.PumpUngatedEventsOnCurrentThread</c>）。</para>
    ///
    /// <para><b>⚠ 这条跳过支路不是优化，是必需的</b>：派发体会在**持有闸门**的情况下调 `Pump`；
    /// 若 `Pump` 那时去抢闸门，就是抢自己握着的锁 —— 实测（s167 负控）把判定改成"恒不跳过"之后
    /// 立刻自锁：`派发闸门等待超时（疑似自锁）：sessionThread=13, callingThread=13`，
    /// 2 分钟后才抛。所以判据钉的是"**谁**能走进来"，不是"要不要有这条支路"。</para>
    /// </summary>
    [Fact]
    public void PumpGateSkip_IsReachableOnlyFromTheDedicatedSessionThread()
    {
        var bodyThreads = new System.Collections.Generic.HashSet<int>();
        var callerThreads = new System.Collections.Generic.HashSet<int>();

        void DispatchAndRecord()
        {
            callerThreads.Add(Environment.CurrentManagedThreadId);
            UiTestHost.Run(() =>
            {
                lock (bodyThreads) bodyThreads.Add(Environment.CurrentManagedThreadId);
            });
        }

        DispatchAndRecord();                                  // 本线程
        var workers = new System.Threading.Thread[2];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new System.Threading.Thread(() =>
            {
                for (var k = 0; k < 5; k++) DispatchAndRecord();
            }) { IsBackground = true };
            workers[i].Start();
        }
        foreach (var t in workers)
            Assert.True(t.Join(TimeSpan.FromSeconds(30)), "后台派发线程没在 30 秒内结束 —— 闸门疑似自锁");

        // 从**非派发**线程（本线程 + 两个后台线程）调 Pump：**每个线程自己**一次都不许走跳过支路。
        // ⚠ 必须用**线程局部**计数：全局计数在跨类并行时会被别的测试类**合法**的派发体内 Pump 撑大
        // （阶段 167 实测：用全局 delta 得到过"非派发线程跳过 107 次"的**假读数**）。
        var pumpReports = new System.Collections.Generic.List<(int Thread, long Before, long After)>();
        var pumpCallers = new System.Collections.Generic.HashSet<int>();
        void PumpOnCurrentThread()
        {
            lock (pumpCallers) pumpCallers.Add(Environment.CurrentManagedThreadId);
            var before = UiTestHost.PumpUngatedEventsOnCurrentThread;
            UiTestHost.Pump(1);
            lock (pumpReports)
                pumpReports.Add((Environment.CurrentManagedThreadId, before, UiTestHost.PumpUngatedEventsOnCurrentThread));
        }

        PumpOnCurrentThread();
        for (var i = 0; i < 2; i++)
        {
            var th = new System.Threading.Thread(() =>
            {
                for (var k = 0; k < 3; k++) PumpOnCurrentThread();
            }) { IsBackground = true };
            th.Start();
            Assert.True(th.Join(TimeSpan.FromSeconds(60)), "后台 Pump 线程没在 60 秒内结束 —— 闸门疑似自锁");
        }
        var nonDispatchSkips = pumpReports.Count(r => r.After != r.Before);

        // 正控：派发体里调 Pump 必须走跳过支路（**在派发体里**读线程局部计数才看得见）
        var insideSkip = UiTestHost.Run(() =>
        {
            var before = UiTestHost.PumpUngatedEventsOnCurrentThread;
            UiTestHost.Pump(1);
            return UiTestHost.PumpUngatedEventsOnCurrentThread - before;
        });

        Assert.True(callerThreads.Count >= 3,
            $"本判据本该从 {callerThreads.Count} 个不同线程派发过 —— 调用线程太少，① 的说服力不够");
        Assert.True(bodyThreads.Count == 1,
            $"派发体跑在 {bodyThreads.Count} 个线程上（{string.Join(",", bodyThreads)}）—— "
            + "「有一个专用会话线程」这个前提不成立 ⇒ 登记 #82 要重新量");
        Assert.True(nonDispatchSkips == 0,
            $"非派发线程的 Pump 跳过了闸门 {nonDispatchSkips}/{pumpReports.Count} 次"
            + $"（调用线程 {string.Join(",", pumpCallers)}，会话线程 {string.Join(",", bodyThreads)}）—— #82 那条窄窗是真的，"
            + "读数明细：" + string.Join(" | ", pumpReports.Select(r => $"t{r.Thread}:{r.Before}→{r.After}")));
        Assert.True(insideSkip > 0,
            "正控失败：派发体里的 Pump 都没走跳过支路 ⇒ 计数器是死的，上面那条断言恒 0（假绿）");
    }
}
