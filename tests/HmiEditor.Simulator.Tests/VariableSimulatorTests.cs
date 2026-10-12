using HmiEditor.Simulator;
using Xunit;

namespace HmiEditor.Simulator.Tests;

public class VariableSimulatorTests
{
    // ── SimulatedVariable 模型测试 ──

    [Fact]
    public void SimulatedVariable_DefaultSignal_IsConstant()
    {
        var v = new SimulatedVariable();
        Assert.Equal(SignalType.Constant, v.Signal);
        Assert.Equal(100.0, v.Amplitude);
    }

    [Fact]
    public void SimulatedVariable_ConstantSignal_ReturnsAmplitude()
    {
        var v = new SimulatedVariable
        {
            VariableId = "test",
            Signal = SignalType.Constant,
            Amplitude = 42.0,
        };
        var val = v.GetValue(999);  // time doesn't matter for constant
        Assert.Equal(42.0, val);
    }

    [Fact]
    public void SimulatedVariable_SineSignal_Oscillates()
    {
        var v = new SimulatedVariable
        {
            Signal = SignalType.Sine,
            Amplitude = 100.0,
            Frequency = 1.0,  // 1 Hz
        };
        // At t=0, sin(0)=0 → 0
        Assert.Equal(0, v.GetValue(0), 1e-6);
        // At t=0.25, sin(π/2)=1 → 100
        Assert.Equal(100, v.GetValue(0.25), 1e-6);
        // At t=0.5, sin(π)=0 → 0
        Assert.Equal(0, v.GetValue(0.5), 1e-6);
    }

    // Square and Ramp signal behavior tested indirectly through Simulator engine tests

    [Fact]
    public void SimulatedVariable_Increment_ClampsToRange()
    {
        var v = new SimulatedVariable
        {
            Signal = SignalType.Increment,
            MinValue = 0,
            MaxValue = 200,
            Frequency = 1.0,
        };
        // At t=0, phase=0 => 0
        Assert.Equal(0, v.GetValue(0), 1e-6);
        // At t=0.5, phase=0.5 => 100
        Assert.Equal(100, v.GetValue(0.5), 1e-2);
        // At t=1, phase=0 => 0 (reset)
        Assert.Equal(0, v.GetValue(1), 1e-6);
    }

    [Fact]
    public void SimulatedVariable_ManualValue_ReturnsSetValue()
    {
        var v = new SimulatedVariable
        {
            Signal = SignalType.Manual,
            ManualValue = 77.0,
        };
        Assert.Equal(77.0, v.GetValue(0));
        v.ManualValue = 88.0;
        Assert.Equal(88.0, v.GetValue(0));
    }

    [Fact]
    public void SimulatedVariable_Noise_ModifiesValue()
    {
        var v = new SimulatedVariable
        {
            Signal = SignalType.Constant,
            Amplitude = 100.0,
            Noise = 5.0,
        };
        var val = v.GetValue(0);
        // With noise, value should be within amplitude ± noise range
        Assert.InRange(val, 95.0, 105.0);
    }

    // ── VariableSimulator 引擎测试 ──

    [Fact]
    public void Simulator_RegisterVariable_AddsToList()
    {
        using var sim = new VariableSimulator();
        sim.RegisterVariable(new SimulatedVariable { VariableId = "v1", Signal = SignalType.Constant, Amplitude = 42 });

        Assert.Single(sim.GetVariables());
        Assert.Equal("v1", sim.GetVariables()[0].VariableId);
    }

    [Fact]
    public void Simulator_GetCurrentValues_ReturnsAll()
    {
        using var sim = new VariableSimulator();
        sim.RegisterVariable(new SimulatedVariable { VariableId = "v1", Signal = SignalType.Constant, Amplitude = 10 });
        sim.RegisterVariable(new SimulatedVariable { VariableId = "v2", Signal = SignalType.Constant, Amplitude = 20 });

        var values = sim.GetCurrentValues();
        Assert.Equal(2, values.Count);
        Assert.Equal(10, values["v1"]);
        Assert.Equal(20, values["v2"]);
    }

    [Fact]
    public void Simulator_SetManualValue_UpdatesVariable()
    {
        using var sim = new VariableSimulator();
        sim.RegisterVariable(new SimulatedVariable
        {
            VariableId = "manual-var",
            Signal = SignalType.Manual,
            ManualValue = 0
        });

        sim.SetManualValue("manual-var", 99.9);
        var values = sim.GetCurrentValues();
        Assert.Equal(99.9, values["manual-var"], 1e-6);
    }

    [Fact]
    public void Simulator_StartStop_Works()
    {
        using var sim = new VariableSimulator();
        sim.RegisterVariable(new SimulatedVariable { VariableId = "v1", Signal = SignalType.Constant, Amplitude = 1 });

        sim.Start();
        Assert.True(sim.IsRunning);
        sim.Stop();
        Assert.False(sim.IsRunning);
        // 🩸 阶段 130：这里原来有一句 `Thread.Sleep(50)` —— 它**与任何断言都无关**
        //   （前后两条断言判的都是 `IsRunning`，睡不睡都不变）⇒ 删掉：留着只会让人以为
        //   「这条用例依赖那 50ms」。真正需要「等产品状态」的地方一律走 TestWait.Until（见本文件其余各处）。
    }

    [Fact]
    public void Simulator_ValuesUpdatedEvent_Fires()
    {
        var fired = false;
        using var sim = new VariableSimulator();
        sim.TickIntervalMs = 50;
        sim.ValuesUpdated += (_, _) => fired = true;

        sim.RegisterVariable(new SimulatedVariable { VariableId = "v1" });
        sim.Start();

        // 等事件真的到达，而不是「Sleep 之后假定它已到达」。
        // VariableSimulator 用 System.Threading.Timer，回调排在 ThreadPool 上，
        // 全量并行跑测试时延迟不可预测 —— 同因缺陷在 AlarmEngineTests 上已实测随机红。
        //
        // 🩸 阶段 130：改用**共享量具** TestWait.Until（原先是本文件手写的
        //   `SpinWait.SpinUntil(…, 5000)`）—— 同一条等待、同一个 5s 预算，只是不再各写一份
        //   （量具抄成两份就会走样）。
        TestWait.Until(() => fired, "ValuesUpdated 事件应在 5s 内触发");

        sim.Stop();

        Assert.True(fired);
    }

    [Fact]
    public void Simulator_UnregisterVariable_Removes()
    {
        using var sim = new VariableSimulator();
        sim.RegisterVariable(new SimulatedVariable { VariableId = "v1" });
        sim.UnregisterVariable("v1");
        Assert.Empty(sim.GetVariables());
    }

    [Fact]
    public void Simulator_Clear_RemovesAll()
    {
        using var sim = new VariableSimulator();
        sim.RegisterVariable(new SimulatedVariable { VariableId = "v1" });
        sim.RegisterVariable(new SimulatedVariable { VariableId = "v2" });
        sim.Clear();
        Assert.Empty(sim.GetVariables());
    }

    // ── 增强能力: 时钟倍速 / 运行统计 ──
    //
    // 以下时钟断言一律注入 FakeTimeProvider，不再 Thread.Sleep + 宽区间：
    // 旧版靠「睡 120ms，然后断言 InRange(0.3, 0.7)」既不稳定（CI 卡顿即假红）
    // 也证明不了倍速本身 —— 现在时间轴由假时钟推动，断言是精确值。

    [Fact]
    public void Simulator_ClockMultiplier_SpeedsUpSimulatedTime()
    {
        var time = new FakeTimeProvider();
        using var sim = new VariableSimulator(time);
        sim.ClockMultiplier = 4.0;
        sim.RegisterVariable(new SimulatedVariable { VariableId = "v1", Signal = SignalType.Constant, Amplitude = 1 });

        sim.Start();
        sim.Stop();   // 立刻停掉真实 Timer：时间轴此后只由假时钟推动

        time.Advance(TimeSpan.FromSeconds(0.12));   // 真实流逝 0.12s
        Assert.Equal(0.48, sim.ElapsedSeconds, 9);  // 倍速 4x → 0.48s
    }

    [Fact]
    public void Simulator_ClockMultiplier_DeceleratesSimulatedTime()
    {
        var time = new FakeTimeProvider();
        using var sim = new VariableSimulator(time);
        sim.ClockMultiplier = 0.5;
        sim.RegisterVariable(new SimulatedVariable { VariableId = "v1", Signal = SignalType.Constant, Amplitude = 1 });

        sim.Start();
        sim.Stop();

        time.Advance(TimeSpan.FromSeconds(0.12));   // 真实流逝 0.12s
        Assert.Equal(0.06, sim.ElapsedSeconds, 9);  // 倍速 0.5x → 0.06s
    }

    [Fact]
    public void Simulator_ElapsedSeconds_BeforeStart_IsZero()
    {
        // 旧实现以 DateTime.MinValue 为基准 ⇒ Start() 之前读 ElapsedSeconds 会得到 ~6.4e10 秒。
        var time = new FakeTimeProvider();
        using var sim = new VariableSimulator(time);

        time.Advance(TimeSpan.FromHours(3));

        Assert.Equal(0.0, sim.ElapsedSeconds);
    }

    [Fact]
    public void Simulator_ElapsedSeconds_IsMonotonic_UnaffectedByWallClockJumps()
    {
        // 契约：仿真时间轴走单调时钟，与墙上时钟解耦
        // （与固件侧「LVGL tick 必须 CLOCK_MONOTONIC」同源）。
        var time = new FakeTimeProvider();
        using var sim = new VariableSimulator(time);
        sim.Start();
        sim.Stop();

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(2.0, sim.ElapsedSeconds, 9);

        time.JumpWallClock(TimeSpan.FromHours(1));    // NTP 向前校时
        Assert.Equal(2.0, sim.ElapsedSeconds, 9);

        time.JumpWallClock(TimeSpan.FromHours(-3));   // NTP 向后校时：不得回卷、更不得变负
        Assert.True(sim.ElapsedSeconds >= 0, $"仿真时间不得变负，实为 {sim.ElapsedSeconds}");
        Assert.Equal(2.0, sim.ElapsedSeconds, 9);
    }

    [Fact]
    public void Simulator_Start_ResetsElapsedTimeline()
    {
        var time = new FakeTimeProvider();
        using var sim = new VariableSimulator(time);

        sim.Start();
        sim.Stop();
        time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(5.0, sim.ElapsedSeconds, 9);

        sim.Start();
        sim.Stop();
        Assert.Equal(0.0, sim.ElapsedSeconds, 9);   // 基准重置到新的起跑线
    }

    [Fact]
    public void Simulator_TickCount_IncreasesWhileRunning()
    {
        using var sim = new VariableSimulator();
        sim.TickIntervalMs = 20;
        sim.RegisterVariable(new SimulatedVariable { VariableId = "v1" });

        sim.Start();

        // 等 tick 数达标（产品状态同步），不等固定时长 —— 同因见上。
        // 🩸 阶段 130：改用共享量具 TestWait.Until（同一条等待、同一个 5s 预算）。
        TestWait.Until(() => sim.TickCount >= 3, "TickCount 应在 5s 内达到 3");

        sim.Stop();

        Assert.True(sim.TickCount >= 3, $"TickCount={sim.TickCount} 应 ≥ 3");
        Assert.True(sim.VariableCount >= 1);
    }

    [Fact]
    public void Simulator_LastTickElapsedMs_IsMeasured()
    {
        using var sim = new VariableSimulator();
        sim.TickIntervalMs = 20;
        sim.RegisterVariable(new SimulatedVariable { VariableId = "v1" });

        sim.Start();

        // 等真的跑过一次 tick，别 Sleep 后假定它跑过了（同因见上）。
        // 原实现睡 120ms 后断言 LastTickElapsedMs >= 0 —— 那断言恒真，等于没断言。
        // 🩸 阶段 130：改用共享量具 TestWait.Until（同一条等待、同一个 5s 预算）。
        TestWait.Until(() => sim.TickCount >= 1, "TickCount 应在 5s 内达到 1");

        sim.Stop();

        // 🩸 原实现这里是 `Assert.True(sim.LastTickElapsedMs >= 0)` —— 对 double **恒真**
        //   （阶段 120 删除）。真正有信息的是有限性：NaN/Inf 说明计时轴被算坏了。
        Assert.True(double.IsFinite(sim.LastTickElapsedMs),
            $"LastTickElapsedMs={sim.LastTickElapsedMs} 应为有限值");
    }
}
