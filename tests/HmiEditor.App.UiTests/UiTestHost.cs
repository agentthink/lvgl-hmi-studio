using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using HmiEditor.App;

namespace HmiEditor.App.UiTests;

/// <summary>
/// headless UI 测试宿主。
///
/// <para><b>为什么需要它：</b>此前所有 UI 逻辑都只能靠"把状态抽成纯类再测纯类"来覆盖
/// （见 <c>CanvasDragPreviewStateTests</c> 等），真正的可视树、XAML 资源解析、事件订阅、
/// 布局与输入处理**零覆盖** —— `{StaticResource}` 写错、控件名对不上、Click 没接上，
/// 只有人肉跑一遍编辑器才能发现。这里用 Avalonia 的 headless 平台把真实窗口/控件
/// 建起来，在不依赖显示器与 GPU 的前提下测这些行为。</para>
///
/// <para><see cref="Avalonia.Headless.HeadlessUnitTestSession"/> 提供一个专用的 UI 线程与
/// 真实 <see cref="HmiEditor.App.App"/> 实例（因此 App.axaml 的主题/图标资源、本地化、
/// 视图定位器都是**真实**的），本类把它包成 <see cref="Run{T}"/> 系列入口。</para>
///
/// <para><b>每次 <c>Run</c> 都会重建一遍平台（登记表 #47 的现场，2026-09-21 反编译核实）：</b>
/// <c>StartNew(Type)</c> 用的隔离级别是 <c>PerTest</c>（枚举值 1），于是每次派发都执行
/// <c>AvaloniaLocator.EnterScope()</c> + <c>Dispatcher.ResetBeforeUnitTests()</c> +
/// <c>AppBuilder.SetupUnsafe()</c>（＝重建整个平台，含一个新的 <c>Compositor</c>）。
/// 这不只是慢：<c>ResetGlobalState()</c> 会把 <c>Dispatcher.s_uiThread</c> 清成 <c>null</c>，
/// 而 <c>Dispatcher..ctor</c> 里是「<c>if (s_uiThread == null) s_uiThread = this;</c>」——
/// 也就是说**每次派发都开了一个"谁先构造 Dispatcher 谁拿走 s_uiThread"的窗口**；
/// 而 <c>CheckAccess()</c> 只是 <c>Thread.CurrentThread == _thread</c> 的引用比较。
/// 别的线程若在这个窗口里构造出 <c>Dispatcher</c>，会话线程随后的
/// <c>DefaultRenderLoop.Add → Dispatcher.UIThread.VerifyAccess()</c> 就会抛
/// "different thread owns it"。详见 <see cref="DescribeSessionState"/> 与
/// <c>docs/2026-09-21-dockpanel-invocation-net.md</c> §8 的 #47。</para>
/// </summary>
public static class UiTestHost
{
    private static readonly Lazy<HeadlessUnitTestSession> Session =
        new(() => HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder)),
            LazyThreadSafetyMode.ExecutionAndPublication);

    private static int _dispatchSeq;

    /// <summary>
    /// 会话线程（每次派发时在派发线程上记一次）。用来把「`s_uiThread` 被**别的线程**持有」
    /// 与「被会话线程持有」区分开 —— 这是「有偷手」与「一切正常」的唯一分界（登记表 #47）。
    /// </summary>
    private static int _sessionThreadId;

    /// <summary>会话线程 id（未派发过时是 0）。只读、不构造任何 Dispatcher。</summary>
    internal static int SessionThreadId => Volatile.Read(ref _sessionThreadId);

    // ────────────────────────── 派发闸门 ──────────────────────────
    //
    // **为什么必须有它**（2026-09-21 阶段 59 实测 + IL 核实）：
    // `HeadlessUnitTestSession.DispatchCore` 是在**调用线程**上直接执行
    // `EnsureIsolatedApplication()`，其实现是
    //   `scope = AvaloniaLocator.EnterScope();`
    //   `Dispatcher.ResetBeforeUnitTests();`
    //   `_appBuilder.SetupUnsafe();`
    // ——三行**全是进程级操作，全程无锁、无重入判定**。而 xunit 默认让不同 collection 并行，
    // 于是同一条 `App.UiTests` 程序集里**最多有 20 个派发同时在飞**（实测读数，见下方诊断区），
    // 也就是 20 个线程各自以为自己在独占"那条 UI 线程"、并互相把对方的平台拆掉重建。
    //
    // 语义上这也站不住：这个 API 的模型就是**一个会话一条 UI 线程**（`Dispatch` 单数）。
    // 并发派发不是"更快"，是同时存在多个"UI 线程"—— 框架没打算支持，也没做任何防护。
    //
    // ⇒ 这里把**派发**串行化（而不是关掉 xunit 的集合并行）：不碰 Avalonia 的测试照旧并行，
    //    只有真正要动平台的那一刻才排队。**自动生效、忘不掉** —— 这比给 30 个测试类
    //    逐个加 `[Collection("headless")]` 可靠：漏一个就静默退回竞态。
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly TimeSpan GateTimeout = TimeSpan.FromMinutes(2);

    /// <summary>同线程重入深度。派发体里再调 <see cref="Run(Action)"/> 不该自锁。</summary>
    [ThreadStatic] private static int _gateDepth;

    private static void EnterGate()
    {
        if (_gateDepth > 0) { _gateDepth++; return; }

        // 只在诊断打开时取时刻：不设 HMI_DIAG_OUT 时这两行不执行，
        // 派发路径与加诊断之前逐指令等价（见 DiagEnabled 的说明）。
        if (DiagEnabled) _gateWaitStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();

        // 带超时：万一真的自锁了，这里抛一条**点名**的异常，而不是让宿主静默僵死到超时。
        if (!Gate.Wait(GateTimeout))
            throw new InvalidOperationException(
                "派发闸门等待超时（疑似自锁）：" + DescribeSessionState());
        _gateDepth = 1;
        if (DiagEnabled) _gateHeldStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static void ExitGate()
    {
        if (_gateDepth <= 0) return;
        if (--_gateDepth != 0) return;
        Gate.Release();
        if (DiagEnabled) RecordGateTiming();
    }

    /// <summary>在 headless UI 线程上同步执行并返回结果；内部异常会原样抛出（含 Assert 失败）。</summary>
    public static T Run<T>(Func<T> body)
    {
        EnterGate();
        try
        {
            return Dispatch(() => Session.Value.Dispatch(
                () => { Volatile.Write(ref _sessionThreadId, Environment.CurrentManagedThreadId); return body(); },
                CancellationToken.None).GetAwaiter().GetResult());
        }
        finally { ExitGate(); }
    }

    /// <summary>在 headless UI 线程上同步执行。</summary>
    public static void Run(Action body)
    {
        EnterGate();
        try
        {
            Dispatch(() => Session.Value.Dispatch(
                () => { Volatile.Write(ref _sessionThreadId, Environment.CurrentManagedThreadId); body(); },
                CancellationToken.None).GetAwaiter().GetResult());
        }
        finally { ExitGate(); }
    }

    /// <summary>
    /// 派发体内取 <c>Dispatcher.UIThread</c> 的**唯一合法入口**（阶段 298）。
    ///
    /// <para><b>为什么要有它</b>：在 <c>s_uiThread == null</c> 的窗口里（＝派发之外）读那个 getter 会
    /// **当场构造**一个 Dispatcher 并把归属抢到调用线程 ⇒ 下一次派发在平台初始化处报
    /// <c>different thread owns it</c>（登记 `#47`；阶段 295/297 已实测并定位）。
    /// 直接写 <c>Dispatcher.UIThread</c> 的项目**字面上看不出**它在不在派发里。
    /// 本属性把那条规则变成**运行期断言**：不在派发体内就抛，并附现场读数。</para>
    ///
    /// <para>判据侧：`AmbientEntryPointInventoryTests` 的静态面禁止测试代码直接引用
    /// <c>Dispatcher.UIThread</c>（剥注释 + 掩字符串后要求 0 命中）。</para>
    /// </summary>
    internal static Dispatcher UIThread
    {
        get
        {
            // ⚠ 判"在不在派发体内"用的是**会话线程**判定（与 `Pump` 同一口径），
            //   不能用本线程的 `_gateDepth`：派发体是会话线程跑的，调用线程的闸门深度在体内是 0
            //   （首版就踩了这个 —— 于是 `UIThreadDispatcher_IsOwnedByTheCallingThread` 当场红）。
            if (Environment.CurrentManagedThreadId != SessionThreadId)
                throw new InvalidOperationException(
                    "派发之外读 Dispatcher.UIThread 会当场抢走 s_uiThread（登记 #47）——"
                    + "请在 UiTestHost.Run/RunAsync/Pump 的体内读它。" + DescribeSessionState());
            return Dispatcher.UIThread;
        }
    }

    /// <summary>在 headless UI 线程上执行异步体并等待其完成。</summary>
    public static async Task RunAsync(Func<Task> body)
    {
        // 异步路径不做同线程重入判定（续体可能换线程），只做普通互斥；
        // 实测两个使用点（`DispatchContinuationContractTests` / `HeadlessInputAndDialogUiTests`）
        // 都不在派发体里再调 `RunAsync`，所以不会自锁。
        if (!await Gate.WaitAsync(GateTimeout).ConfigureAwait(false))
            throw new InvalidOperationException(
                "派发闸门等待超时（疑似自锁）：" + DescribeSessionState());

        try
        {
            // 显式标注 Func<Task<bool>>，避免绑定到 Dispatch<T>(Func<T>) 而拿到嵌套 Task。
            Func<Task<bool>> work = async () =>
            {
                Volatile.Write(ref _sessionThreadId, Environment.CurrentManagedThreadId);
                await body();
                return true;
            };
            await Dispatch(() => Session.Value.Dispatch(work, CancellationToken.None)).ConfigureAwait(false);
        }
        finally { Gate.Release(); }
    }

    /// <summary>
    /// 让派发队列与渲染定时器推进若干轮，使布局/渲染副作用落地。
    /// 输入模拟之后、断言可视状态之前通常需要调用它。
    ///
    /// <para><b>⚠ 它必须和派发一样受闸门保护（2026-09-21 实测，登记表 #47）</b>：这条路径会
    /// **按调用线程**取 Dispatcher —— <c>ForceRenderTimerTick</c> 的支路里有
    /// <c>Dispatcher.get_CurrentDispatcher()</c>，而 <c>DispatcherTimer</c> 的**三个构造函数**也读它
    /// （<c>HeadlessRenderTimer.StartCore</c> 正是用单参构造建的）。实测：在派发之外调一次
    /// <c>Pump(1)</c>，<c>s_uiThread</c> 的归属立刻从 <c>null</c> 变成**调用线程**
    /// （读数：调用线程 13 / 会话线程 7 / before=null / after=13）。
    /// 闸门原先只护 <c>Run</c> ⇒ 这一条是漏网的：它若落在**在飞派发**的窗口里，
    /// 会话线程随后的 <c>VerifyAccess()</c> 就会抛 "different thread owns it"，
    /// 也就是 #47 那条「偶发红、抛点在平台初始化」。</para>
    ///
    /// <para><b>已经站在会话线程上时不走闸门</b>（也就是绝大多数调用点：在派发体**里面**调它）：
    /// 那时「按调用线程取 Dispatcher」取到的正好是会话线程自己 —— 不但无害，还是想要的状态；
    /// 而且闸门此刻被**别的**线程（派发的调用者）持有，再去 Wait 必然自锁。</para>
    /// </summary>
    public static void Pump(int ticks = 3)
    {
        if (Environment.CurrentManagedThreadId == SessionThreadId)
        {
            // 会话线程：本来就在派发体里，不是"并发"。
            // ⚠ 这个判定用的是**全局** `SessionThreadId`（＝最后一次派发的线程）—— 登记 #82：
            // 别的线程可能正处在"reset → 写 _sessionThreadId"之间，此刻本线程若恰好等于上一次
            // 派发的线程，就会在这里**跳过闸门**去碰平台。两个计数器就是这条窄窗的读数。
            Interlocked.Increment(ref _pumpUngated);
            _pumpUngatedOnThisThread++;
            if (Volatile.Read(ref _inFlight) > 0)
                Interlocked.Increment(ref _pumpUngatedDuringDispatch);
            TickRenderTimer(ticks);
            return;
        }

        EnterGate();
        try
        {
            // 拿到闸门后这里必然是 0。它 > 0 只可能是"闸门没护住这条路径" ——
            // 判据 HeadlessDispatchContractTests.Pump_NeverRunsWhileADispatchIsInFlight 就看这个读数。
            if (Volatile.Read(ref _inFlight) > 0)
                Interlocked.Increment(ref _pumpDuringDispatch);
            TickRenderTimer(ticks);
        }
        finally { ExitGate(); }
    }

    private static void TickRenderTimer(int ticks)
    {
        for (var i = 0; i < ticks; i++)
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    // ────────────────────────── 失败现场自证（登记表 #47） ──────────────────────────
    //
    // #47 的症状是"偶发"，而偶发的失败如果只留下一句 "different thread owns it"，下一个人
    // 只能重新从零查一遍。这里把**平台初始化阶段**的失败包一层：判据照旧红（断言不放宽），
    // 但消息里带上现场 —— 派发序号、调用线程、Dispatcher.UIThread 的归属线程。
    // 判定条件是**窄的**（只认这几个与 Dispatcher 归属有关的消息），别的异常原样抛出。

    /// <summary>
    /// 判断异常是不是"Dispatcher 归属"这一类（#47 及其近亲）。
    /// 窄判定：只认 Avalonia 自己那几句消息，避免把普通断言失败也包进诊断层。
    /// </summary>
    internal static bool IsDispatcherOwnershipFailure(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var m = e.Message ?? string.Empty;
            if (m.Contains("different thread owns it", StringComparison.Ordinal) ||
                m.Contains("The current thread already has a dispatcher", StringComparison.Ordinal) ||
                m.Contains("UI thread dispatcher is already initialized", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 一行现场描述。只读、不抛、不改变任何状态。
    ///
    /// <para><b>为什么不能直接读 <c>Dispatcher.UIThread</c>：</b>那个 getter 在
    /// <c>s_uiThread == null</c> 时会走 <c>Dispatcher.CurrentDispatcher</c> 并**当场构造**一个新的
    /// <see cref="Dispatcher"/>，而 <c>Dispatcher..ctor</c> 里有「<c>if (s_uiThread == null) s_uiThread = this;</c>」
    /// —— 于是**探测动作本身就会抢走 <c>s_uiThread</c>，把被观测的 #47 造出来**。
    /// 所以这里只反射读内部的 <c>TryGetUIThread()</c>（它只返回 <c>s_uiThread</c>，不构造、不清空）；
    /// 取不到就报"不可读"，绝不退化成"去 getter 里凑一个"。</para>
    /// </summary>
    internal static string DescribeSessionState()
    {
        var parts = new System.Collections.Generic.List<string>
        {
            $"dispatch#{Volatile.Read(ref _dispatchSeq)}",
            $"sessionCreated={Session.IsValueCreated}",
            $"sessionThread={SessionThreadId}",
            $"callingThread={Environment.CurrentManagedThreadId}",
            $"poolThreads={ThreadPool.ThreadCount}",
            $"ambientUIThread={DescribeAmbientUIThread()}"
        };

        return string.Join(", ", parts);
    }

    /// <summary>
    /// 只读 <c>Dispatcher.s_uiThread</c>（经内部 <c>TryGetUIThread()</c>），**不构造**任何 Dispatcher。
    /// 返回 <c>false</c> 表示读不到（反射未命中或探测抛了），此时 <paramref name="why"/> 是原因 ——
    /// <b>刻意不回退到 <c>UIThread</c> getter</b>（那个 getter 会当场构造并抢走它）。
    /// </summary>
    private static bool TryReadAmbientUIThread(out Dispatcher? ui, out string why)
    {
        ui = null;
        why = string.Empty;
        try
        {
            var probe = typeof(Dispatcher).GetMethod(
                "TryGetUIThread", BindingFlags.NonPublic | BindingFlags.Static);
            if (probe is null)
            {
                why = "<TryGetUIThread 反射未命中；刻意不回退到 UIThread getter（那会构造）>";
                return false;
            }

            ui = probe.Invoke(null, null) as Dispatcher;
            return true;
        }
        catch (Exception ex)
        {
            why = $"<探测失败: {ex.GetType().Name}>";
            return false;
        }
    }

    /// <summary>
    /// 此刻 <c>s_uiThread</c> 的**归属线程** id；没人持有返回 <c>null</c>。
    /// 只读、不构造 —— 判据用它把「被会话线程持有」与「被别的线程抢走」区分开。
    /// </summary>
    internal static int? AmbientUIThreadOwnerId()
    {
        if (!TryReadAmbientUIThread(out var ui, out var why))
            throw new InvalidOperationException("读不到 s_uiThread：" + why);
        if (ui is null) return null;

        var owner = ui.GetType()
            .GetProperty("Thread", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(ui) as Thread;
        return owner?.ManagedThreadId;
    }

    /// <summary>
    /// 把 <c>Dispatcher.s_uiThread</c>（以及 <c>s_currentThreadDispatcher</c>）**写回 null**（阶段 297）。
    ///
    /// <para><b>给谁用</b>：`#47` 的**机制判据**要故意让一条非会话线程抢走归属、以证明"这条入口确实会抢"。
    /// 抢完**必须还**：实测（2026-10-11）不还的话那一抢会污染后续用例 —— 全量套件跑出 **38 条红**，
    /// 全是 `#47` 的平台初始化签名（<c>different thread owns it</c>）。</para>
    ///
    /// <para>返回是否两个字段都写成功（字段名变了 ⇒ false，调用方判据会红并提示重新推导）。</para>
    /// </summary>
    internal static bool ResetAmbientUIThreadOwner()
    {
        var ok = 0;
        foreach (var name in new[] { "s_uiThread", "s_currentThreadDispatcher" })
        {
            var f = typeof(Dispatcher).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            if (f is null) continue;
            f.SetValue(null, null);
            ok++;
        }
        return ok == 2;
    }

    /// <summary>
    /// **持闸门**做一次"跨派发读数"：连续两次派发（把 <c>_sessionThreadId</c> 与 <c>s_uiThread</c>
    /// 都落到本线程），**在本线程仍握着闸门时**读 <c>s_uiThread</c> 的归属线程。
    ///
    /// <para><b>为什么必须持闸门（阶段 166，闭环 `#81`）</b>：实测（阶段 166 的临时探针读数）
    /// 一次派发结束后 <c>s_uiThread</c> **是 <c>null</c>**（PerTest 隔离的 scope 退出时又重置了一遍
    /// ⇒ "窗口大部分时间是开着的"），此后**任何**线程碰平台都会当场构造 Dispatcher 并拿走它。
    /// 而**别的测试类合法地碰平台**（最典型：在派发之外调 <see cref="Pump"/> —— 那条路刻意按调用线程
    /// 取 Dispatcher，见它的注释与实测）就会把归属翻成**那个线程**。旧哨兵在**闸门之外**读数，
    /// 于是报出"归属 ≠ 本会话线程"的**假阳**：
    /// 读数 `owner=19, session=7`（后台线程只是调了一次合法的 <c>Pump(1)</c>，**没有任何非法访问**）。</para>
    ///
    /// <para>把"派发 ＋ 读数"放进**同一段闸门**之后：别人的派发 / <c>Pump</c> 挤不进来 ⇒
    /// 这个读数只可能看到两件事 —— 自己的会话线程（正常）或 <c>null</c>（刚派发完，窗口开着），
    /// 或者**一个绕过闸门去碰 Avalonia 的线程**（<c>#47</c> 真正要抓的那种"偷手"）。
    /// 检测力不降，假阳没了。</para>
    /// </summary>
    internal static (int? Owner, int SessionThread) ObserveAmbientOwnerOutsideDispatch()
    {
        EnterGate();
        try
        {
            Run(() => 0);   // 同线程重入：闸门按 _gateDepth 计数，不会自锁
            Run(() => 0);
            return (AmbientUIThreadOwnerId(), SessionThreadId);
        }
        finally { ExitGate(); }
    }

    /// <summary>
    /// 在**占住派发闸门**的前提下跑一段「派发之外」的工作（阶段 297）。
    ///
    /// <para><b>为什么需要它</b>：`#47` 的机制判据要**故意**在 `s_uiThread == null` 的窗口里
    /// 让一条非会话线程去读 <c>Dispatcher.UIThread</c> 之类（证明"这条入口确实会抢"）。
    /// 若不加闸门，那一抢就会和**并发跑着的别的用例**的派发撞上 —— 制造出真正的失败，
    /// 把"机制判据"变成"间歇红制造机"。</para>
    ///
    /// <para>占住闸门之后：别人的派发挤不进来；本段结束时抢手仍被记在全局，
    /// 调用方应随后跑一次 <see cref="Run(Action)"/>（它开头的平台重置会把归属清回 <c>null</c>）。</para>
    /// </summary>
    internal static void WithoutDispatch(Action body)
    {
        EnterGate();
        try { body(); }
        finally { ExitGate(); }
    }

    private static string DescribeAmbientUIThread()
    {
        if (!TryReadAmbientUIThread(out var ui, out var why))
            return why;

        if (ui is null)
            // 实测（2026-09-21）：**PerTest** 隔离下派发结束后 s_uiThread 仍是 null ——
            // EnsureIsolatedApplication 返回的 scope 在退出时又重置了一遍。
            // 也就是说**窗口大部分时间是开着的**：此刻任何线程读 Dispatcher.UIThread
            // 都会当场构造并抢走它。这正是 #47 需要解释的那件事。
            // ⚠ 这行字符串被既有判据断言，措辞别改。
            return "s_uiThread=null（窗口开着）";

        var owner = ui.GetType()
            .GetProperty("Thread", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(ui) as Thread;

        var ownerId = owner?.ManagedThreadId;
        var sessionId = SessionThreadId;
        var verdict = ownerId is null ? "<归属线程不可读>"
            : ownerId == sessionId ? "会话线程"
            : "**非会话线程＝偷手**";

        return $"ownerThread={ownerId}（{verdict}）, checkAccessOnCallingThread={ui.CheckAccess()}"
             + $", hash={ui.GetHashCode()}";
    }

    // ─────────────────── 诊断：派发并发度（默认关闭） ───────────────────
    //
    // **为什么必须量它**：`HeadlessUnitTestSession.DispatchCore` 是在**调用线程**上直接执行
    // `EnsureIsolatedApplication()`（= `AvaloniaLocator.EnterScope()` +
    // `Dispatcher.ResetBeforeUnitTests()` + `AppBuilder.SetupUnsafe()`），全程**没有任何锁或队列**
    // （IL 核实，见 `docs/2026-09-21-dockpanel-invocation-net.md` 补记之四）。
    // 而这些都是**进程级**操作，xunit 默认又让不同 collection 并行 ⇒
    // 两个测试可以**同时**派发、同时重建同一套平台。这正是登记表 #45（同进程宿主僵死）
    // 与 #47（`s_uiThread` 被"偷手"抢走）共同指向的那个候选机制。
    //
    // 刻意**不去复现偶发挂死**（那要等 90 秒超时，还只能拿到"挂/不挂"一个比特），
    // 而是直接量它的**前提条件**：任一时刻最多有几个派发在飞。这个量是确定性的。
    //
    // 打开方式：设 `HMI_DIAG_OUT=<文件路径>`，进程退出时把汇总写进去。
    // 不设它就只多两次 `Interlocked` 自增，行为与读数都不变。

    private static int _inFlight;
    private static int _maxInFlight;
    private static long _overlapEvents;
    private static int _overlapSampleTaken;
    private static readonly int[] _inFlightThreads = new int[8];
    private static string _overlapSample = string.Empty;
    private static int _diagHooked;

    // ── 诊断：闸门等待 / 派发体执行 两段时间（阶段 60） ──
    //
    // **为什么必须分成两段而不是只记一个总时长**：出现「某段测试突然慢几倍」时，
    // 唯一需要区分的是「**在闸门外排队排久了**」还是「**某一个派发体自己慢**」——
    // 前者指向闸门/线程池调度，后者指向被派发的那段代码或框架本身。只有总时长无法区分，
    // 而且会把两者混成一个数字，事后无从归因（2026-09-21 提交后门禁那次 UI 段 141 秒
    // 就是这么失去现场的：当时没有这两个读数，只剩一个「2 m 21 s」）。
    //
    // ⚠ 这两个读数**只在设了 HMI_DIAG_OUT 时**才被记录（DiagEnabled）。
    //   刻意不做成"总是记、只是不打印"：那会往每次派发里塞两次 Stopwatch 调用，
    //   而本文件里的并发计数判据（MaxConcurrentDispatches / DispatchOverlapEvents）
    //   正是靠"默认路径不加东西"才可信。
    private static readonly bool DiagEnabled =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HMI_DIAG_OUT"));

    [ThreadStatic] private static long _gateWaitStartTicks;
    [ThreadStatic] private static long _gateHeldStartTicks;

    private static long _maxGateWaitMs;
    private static int _maxGateWaitDispatchSeq;
    private static long _maxBodyMs;
    private static int _maxBodyDispatchSeq;
    private static long _totalGateWaitMs;

    /// <summary>本次派发在闸门外等待的毫秒数。只在 <see cref="DiagEnabled"/> 时有意义。</summary>
    private static void RecordGateTiming()
    {
        var heldStart = _gateHeldStartTicks;
        if (heldStart == 0) return;   // 没经过本线程的 EnterGate（理论上不会）

        var waitMs = MsSince(_gateWaitStartTicks, heldStart);
        var bodyMs = MsSince(heldStart, System.Diagnostics.Stopwatch.GetTimestamp());
        var seq = Volatile.Read(ref _dispatchSeq);

        Interlocked.Add(ref _totalGateWaitMs, waitMs);
        UpdateMax(ref _maxGateWaitMs, ref _maxGateWaitDispatchSeq, waitMs, seq);
        UpdateMax(ref _maxBodyMs, ref _maxBodyDispatchSeq, bodyMs, seq);
    }

    private static long MsSince(long from, long to) =>
        from == 0 ? 0 : (long)((to - from) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);

    private static void UpdateMax(ref long max, ref int maxSeq, long value, int seq)
    {
        if (value <= Volatile.Read(ref max)) return;
        Volatile.Write(ref max, value);
        Volatile.Write(ref maxSeq, seq);
    }

    /// <summary>本进程内「同时处于派发体执行中」的最大个数。闸门生效时应**恒为 1</b>。</summary>
    internal static int MaxConcurrentDispatches => Volatile.Read(ref _maxInFlight);

    /// <summary>「进入派发时已经有别的派发在飞」的次数。闸门生效时应**恒为 0**。</summary>
    internal static long DispatchOverlapEvents => Interlocked.Read(ref _overlapEvents);

    /// <summary>本进程内派发总次数（给判据当"计数器确实在工作"的最小正控）。</summary>
    internal static int DispatchCount => Volatile.Read(ref _dispatchSeq);

    /// <summary>
    /// 本进程内「<see cref="Pump"/> 的推进动作与在飞派发并发」的次数。
    /// 闸门护住 <see cref="Pump"/> 之后应**恒为 0**（登记表 #47：Pump 曾是漏网的那条偷手路径）。
    /// </summary>
    internal static long PumpDuringDispatchEvents => Interlocked.Read(ref _pumpDuringDispatch);

    private static long _pumpDuringDispatch;

    // ── 诊断：`Pump` **跳过闸门**的次数（阶段 166 登记 #82 的那条窄窗） ──
    //
    // `Pump` 的跳过判定用的是**全局** `SessionThreadId`（＝最后一次派发的线程），
    // 而不是"本线程正在派发体内"。若本线程恰好等于那个全局值，它就会以为自己在派发体内、
    // **跳过闸门**去碰平台 —— 而此刻可能正有**别的线程**在平台初始化的窗口里
    // （`ResetBeforeUnitTests()` 已把 `s_uiThread` 清成 null，`SetupUnsafe()` 还没建好）。
    // `_pumpUngatedDuringDispatch` 就是"跳过闸门 + 当时有别的派发在飞"的次数：
    // **闸门与判定都正确时应恒为 0**。
    private static long _pumpUngated;
    private static long _pumpUngatedDuringDispatch;

    /// <summary>
    /// **本线程**走"已在会话线程上"（跳过闸门）那条支路的次数。
    ///
    /// <para>为什么除了全局计数还要这个：全局计数在**跨类并行**时会被别的测试类**合法**的
    /// 派发体内 <c>Pump</c> 撑大（阶段 167 实测：判据用全局 delta 得到"非派发线程跳过 107 次"的
    /// **假读数**，其实那 107 次全是别人类在派发体里的合法跳过）⇒ 要判断"**我这个线程**有没有跳过闸门"，
    /// 只能用线程局部的计数。</para>
    /// </summary>
    [ThreadStatic] private static long _pumpUngatedOnThisThread;

    /// <summary>**本线程**跳过闸门的次数（线程局部，不受别的测试类影响）。</summary>
    internal static long PumpUngatedEventsOnCurrentThread => _pumpUngatedOnThisThread;

    /// <summary>`Pump` 走"已在会话线程上"那条路（**跳过闸门**）的总次数。</summary>
    internal static long PumpUngatedEvents => Interlocked.Read(ref _pumpUngated);

    /// <summary>`Pump` **跳过闸门**且当时**有别的派发在飞**的次数 —— 登记 #82 那条窄窗的证据。</summary>
    internal static long PumpUngatedDuringDispatchEvents => Interlocked.Read(ref _pumpUngatedDuringDispatch);

    private static void EnterDispatch()
    {
        EnsureDiagHook();
        var slot = Interlocked.Increment(ref _inFlight) - 1;
        if (slot < _inFlightThreads.Length)
            Volatile.Write(ref _inFlightThreads[slot], Environment.CurrentManagedThreadId);

        var now = slot + 1;
        int cur;
        while (now > (cur = Volatile.Read(ref _maxInFlight)))
            if (Interlocked.CompareExchange(ref _maxInFlight, now, cur) == cur) break;

        if (now <= 1) return;
        Interlocked.Increment(ref _overlapEvents);

        // 只留第一次重叠的现场：把当时所有在飞的线程 id 记下来，用来回答
        // 「同时派发的是不是**不同测试的线程**」——若是，机制就坐实了。
        if (Interlocked.Exchange(ref _overlapSampleTaken, 1) != 0) return;
        var ids = new System.Collections.Generic.List<int>();
        for (var i = 0; i < _inFlightThreads.Length; i++)
        {
            var t = Volatile.Read(ref _inFlightThreads[i]);
            if (t != 0) ids.Add(t);
        }
        _overlapSample = $"线程 [{string.Join(", ", ids)}] 同时在飞（本次进入的是 {Environment.CurrentManagedThreadId}）";
    }

    private static void ExitDispatch()
    {
        var slot = Interlocked.Decrement(ref _inFlight);
        if (slot >= 0 && slot < _inFlightThreads.Length)
            Volatile.Write(ref _inFlightThreads[slot], 0);
    }

    private static void EnsureDiagHook()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HMI_DIAG_OUT"))) return;
        if (Interlocked.Exchange(ref _diagHooked, 1) != 0) return;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => WriteDiag();
    }

    private static void WriteDiag()
    {
        var path = Environment.GetEnvironmentVariable("HMI_DIAG_OUT");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            System.IO.File.WriteAllText(path,
                $"dispatches={Volatile.Read(ref _dispatchSeq)}\n"
                + $"maxInFlight={_maxInFlight}\n"
                + $"overlapEvents={_overlapEvents}\n"
                + $"firstOverlap={(_overlapSample.Length == 0 ? "<无重叠>" : _overlapSample)}\n"
                + $"maxGateWaitMs={Volatile.Read(ref _maxGateWaitMs)}（第 #{Volatile.Read(ref _maxGateWaitDispatchSeq)} 次派发）\n"
                + $"maxBodyMs={Volatile.Read(ref _maxBodyMs)}（第 #{Volatile.Read(ref _maxBodyDispatchSeq)} 次派发）\n"
                + $"totalGateWaitMs={Interlocked.Read(ref _totalGateWaitMs)}\n");
        }
        catch
        {
            // 诊断写不出去不该影响测试结论
        }
    }

    private static T Dispatch<T>(Func<T> work)
    {
        Interlocked.Increment(ref _dispatchSeq);
        EnterDispatch();
        try
        {
            return work();
        }
        catch (Exception ex) when (IsDispatcherOwnershipFailure(ex))
        {
            throw new InvalidOperationException(
                "headless 会话在**平台初始化**阶段失败（登记表 #47）：现场 " + DescribeSessionState(), ex);
        }
        finally
        {
            ExitDispatch();
        }
    }

    private static void Dispatch(Action work)
    {
        Interlocked.Increment(ref _dispatchSeq);
        EnterDispatch();
        try
        {
            work();
        }
        catch (Exception ex) when (IsDispatcherOwnershipFailure(ex))
        {
            throw new InvalidOperationException(
                "headless 会话在**平台初始化**阶段失败（登记表 #47）：现场 " + DescribeSessionState(), ex);
        }
        finally
        {
            ExitDispatch();
        }
    }
}

/// <summary>
/// headless 会话的入口类型（Avalonia 约定：静态 <c>BuildAvaloniaApp</c>）。
/// 刻意复用真实的 <see cref="App"/>，使 XAML 资源与本地化走生产路径。
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            // UseHeadlessDrawing：不需要 GPU/Skia 就能建树与跑布局，测试更稳、更快。
            // 代价：**不产生像素** —— RenderTargetBitmap.CopyPixels 读回的是未初始化内存
            // （实测前 16 字节形如 12-C0-E4-F8-07-…、非零字节全是噪声）。
            // 需要**像素级**判据请用 tests/HmiEditor.RenderTests：那边要的是
            // **一套不同的平台配置**（Skia 而不是 headless 绘制），而
            // `AppBuilder.SetupUnsafe()` 是进程级的、同一进程里换不成第二套 ⇒ 只能靠独立 testhost。
            //
            // ⚠ 2026-09-21 更正一句曾经写在这里的错话：原先写「渲染循环（DefaultRenderLoop）是
            //   进程级静态单例、绑死在第一个会话的 UI 线程上，所以那边必须靠独立 testhost 隔离」。
            //   反编译 Avalonia 12.1.0 核实：`DefaultRenderLoop` **没有任何静态字段**
            //   （只有 _items/_itemsCopy/_tick/_timer/_timerLock/_inTick/_hasItems/_running/_wakeupPending），
            //   全程序集唯一的构造点是 `RenderLoop.FromTimer(IRenderTimer)`；它的 `Add`
            //   是**当场**读 `Dispatcher.UIThread` 再 `VerifyAccess()`，并非构造时捕获。
            //   ⇒ "绑死在第一个会话的 UI 线程上"这个机制描述不成立。独立进程的真正理由是
            //   "平台配置是进程级的，换不了第二套"（见上）。这条错话曾影响了后来的判断，
            //   所以留着更正痕迹而不是悄悄删掉。
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
