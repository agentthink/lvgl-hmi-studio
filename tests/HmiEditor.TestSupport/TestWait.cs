namespace HmiEditor.TestSupport;

/// <summary>
/// 测试专用确定性等待：轮询到条件成立，或超时后失败。
///
/// <para>🩸 <b>阶段 130 从 <c>HmiEditor.Core.Tests</c> 搬到这里</b>（原 `Support/TestWait.cs`）：
/// 它是**量具**，而当时只有 `Core.Tests` 能用 —— `HmiEditor.Simulator.Tests` 因此各自手写了
/// `SpinWait.SpinUntil(...)` 与 `while (…) Thread.Sleep(5)` 的**同一件事**。量具抄成两份就会走样
/// （本仓的老话），所以搬到所有测试程序集都引用的 <c>HmiEditor.TestSupport</c>。
/// ⚠ 代价：`TestSupport` 因此要引 <c>xunit</c>（它原先零依赖）—— 它只被**测试**程序集引用
/// （4 个），不引它的产品项目一个都不受影响。</para>
///
/// <para><b>要解决的问题：</b>本仓库多个引擎（<c>AlarmEngine</c>、<c>DataLogger</c> …）
/// 内部用 <c>System.Threading.Timer</c> 做周期任务，测试里很自然会写成
/// <c>engine.Start(); Thread.Sleep(100); Assert.XXX();</c>。</para>
///
/// <para>这个写法建立在「100ms 足够让定时器跑完一轮」的时序假设上，而
/// <c>System.Threading.Timer</c> 的回调排在 <b>ThreadPool</b> 上：全量并行跑测试时
/// 线程池被占满，回调延迟可以是 <c>EvaluationIntervalMs</c> 的任意倍。
/// 于是这类断言会随机失败，且失败点在不同轮次落在不同用例上 —— 实测
/// <c>AlarmEngineTests</c> 轮番红过 4 个不同用例（<c>Engine_AlarmTriggeredEvent_Fires</c>、
/// <c>Engine_DelayedTrigger_OnlyFiresAfterDelay</c>、
/// <c>Engine_ClearReturnedRecords_RemovesOnlyReturned</c>、
/// <c>Engine_ClearReturnedRecords…</c>），而单独复跑该类 27/27 全绿。
/// 排查成本很高，因为看起来像产品缺陷。</para>
///
/// <para><b>做法：</b>等「产品状态达到期望」而不是「时间过了多久」。
/// 轮询间隔由 <c>SpinWait</c> 自身退避（先 yield，再 <c>Sleep(1)</c>），CPU 占用可忽略。</para>
///
/// <para><b>什么时候不该用：</b>需要断言「某件事在 T 毫秒内<b>没有</b>发生」时，
/// 单纯轮询无法给出确定性同步点。此时应引入一个「探针」——同一变量上必然先发生的
/// 可观测事件（例如同变量上无延迟的兄弟报警）——等探针出现后再断言目标未发生。
/// 见 <c>AlarmEngineTests.Engine_DelayedTrigger_OnlyFiresAfterDelay</c>。</para>
/// </summary>
public static class TestWait
{
    /// <summary>
    /// 轮询直到 <paramref name="condition"/> 成立；超时则断言失败并带上
    /// <paramref name="what"/>（期望的行为描述）便于定位。
    /// </summary>
    /// <param name="condition">期望成立的产品状态。</param>
    /// <param name="what">人类可读的期望描述，失败时用于说明「在等什么」。</param>
    /// <param name="timeoutMs">
    /// 超时。默认 5 秒 —— 远大于被等待的时间间隔（通常 50–500ms），
    /// 但远小于 CI 上任何合理的挂死判定阈值。
    /// </param>
    public static void Until(Func<bool> condition, string what, int timeoutMs = 5000)
        => Xunit.Assert.True(
            SpinWait.SpinUntil(condition, timeoutMs),
            $"等待超时（{timeoutMs}ms）：{what}");
}
