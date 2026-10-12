namespace HmiEditor.Simulator.Tests;

/// <summary>
/// 测试用时钟：**单调时间戳与墙上时间可分别前进**。
///
/// 存在的意义是把「仿真时间轴必须走单调时钟」这条契约变成可断言的东西：
/// 只跳墙上时间（模拟 NTP 校时）不得影响 <see cref="VariableSimulator.ElapsedSeconds"/>。
/// 若产品实现退回 <c>DateTime.UtcNow</c>，<see cref="JumpWallClock"/> 只有它能感知到
/// ⇒ 对应断言变红（这就是它的负向验证）。
///
/// <para>
/// 时间戳单位取 <see cref="TimeSpan.Ticks"/>：把 <see cref="TimestampFrequency"/> 定成
/// <see cref="TimeSpan.TicksPerSecond"/>，基类的 <c>GetElapsedTime</c>（= 差值 / 频率）就正好还原本单位。
/// 注意 <c>GetElapsedTime</c> 本身在 .NET 10 **不是 virtual**，重写它会 CS0506 —— 只能覆写这两个成员。
/// </para>
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;
    private long _timestamp;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override long GetTimestamp() => _timestamp;

    /// <summary>1 个时间戳单位 = 1 个 <see cref="TimeSpan"/> tick（见类型注释）。</summary>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>单调时间前进（墙上时间同步前进）—— 正常的时间流逝。</summary>
    public void Advance(TimeSpan delta)
    {
        _timestamp += delta.Ticks;
        _utcNow += delta;
    }

    /// <summary>只跳墙上时间（模拟 NTP 校时），单调时间戳不动。</summary>
    public void JumpWallClock(TimeSpan delta) => _utcNow += delta;
}
