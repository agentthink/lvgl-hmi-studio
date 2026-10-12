using HmiEditor.Simulator;
using Xunit;

namespace HmiEditor.Simulator.Tests;

public class CommunicationSimulatorTests
{
    [Fact]
    public void CommSim_ReadWriteRegister_Works()
    {
        var sim = new CommunicationSimulator();
        sim.WriteRegister(100, 42.0);
        Assert.Equal(42.0, sim.ReadRegister(100));
    }

    [Fact]
    public void CommSim_WriteRegister_RecordsPreviousValue()
    {
        var sim = new CommunicationSimulator();
        sim.WriteRegister(200, 10);
        sim.WriteRegister(200, 55.5);
        var log = sim.GetChangeLog();
        Assert.Equal(2, log.Count);
        Assert.Equal(10, log[0].OldValue);
        Assert.Equal(55.5, log[0].NewValue);
    }

    [Fact]
    public void CommSim_WriteRegister_LogsChange()
    {
        var sim = new CommunicationSimulator();
        sim.WriteRegister(200, 55.5);
        var log = sim.GetChangeLog();
        Assert.Single(log);
        Assert.Equal(200, log[0].Address);
        Assert.Equal(55.5, log[0].NewValue);
    }

    [Fact]
    public void CommSim_InvertMode_NegatesValue()
    {
        var sim = new CommunicationSimulator { Mode = WriteMode.Invert };
        sim.WriteRegister(1, 100.0);
        Assert.Equal(-100.0, sim.ReadRegister(1));
    }

    [Fact]
    public void CommSim_Disconnect_ThrowsOnRead()
    {
        var sim = new CommunicationSimulator();
        sim.Disconnect();
        Assert.False(sim.IsConnected);
        Assert.Throws<SimulatorNotConnectedException>(() => sim.ReadRegister(0));
    }

    [Fact]
    public void CommSim_Reconnect_RestoresAccess()
    {
        var sim = new CommunicationSimulator();
        sim.Disconnect();
        sim.Reconnect();
        Assert.True(sim.IsConnected);
        sim.WriteRegister(0, 99);
        Assert.Equal(99, sim.ReadRegister(0));
    }

    [Fact]
    public void CommSim_WriteRegisters_BatchWorks()
    {
        var sim = new CommunicationSimulator();
        sim.WriteRegisters(new Dictionary<int, double>
        {
            [1] = 10.0,
            [2] = 20.0,
            [3] = 30.0,
        });
        Assert.Equal(10.0, sim.ReadRegister(1));
        Assert.Equal(30.0, sim.ReadRegister(3));
    }

    [Fact]
    public void CommSim_ReadRegisters_BatchWorks()
    {
        var sim = new CommunicationSimulator();
        sim.WriteRegister(10, 100);
        sim.WriteRegister(20, 200);

        var result = sim.ReadRegisters(new[] { 10, 20, 30 });
        Assert.Equal(100, result[10]);
        Assert.Equal(200, result[20]);
        Assert.Equal(0, result[30]);  // unset = 0
    }

    [Fact]
    public void CommSim_Clear_RemovesAll()
    {
        var sim = new CommunicationSimulator();
        sim.WriteRegister(0, 42);
        sim.Clear();
        Assert.Equal(0, sim.ReadRegister(0));
        Assert.Empty(sim.GetChangeLog());
    }

    [Fact]
    public void CommSim_RegisterWrittenEvent_Fires()
    {
        var fired = false;
        var sim = new CommunicationSimulator();
        sim.RegisterWritten += (_, _) => fired = true;
        sim.WriteRegister(0, 1);
        Assert.True(fired);
    }

    [Fact]
    public void CommSim_DefaultMode_IsEcho()
    {
        var sim = new CommunicationSimulator();
        Assert.Equal(WriteMode.Echo, sim.Mode);
    }

    [Fact]
    public void CommSim_DefaultState_IsConnected()
    {
        var sim = new CommunicationSimulator();
        Assert.True(sim.IsConnected);
    }

    // ── 增强能力: 响应延迟 / 错误率注入 ──

    [Fact]
    public void CommSim_LatencyMs_SlowsResponse()
    {
        // 🩸 阶段 123：原判据是 `InRange(elapsed, 30, 200)` —— 上界是**绝对余量**：
        //   机器一忙（本沙箱定时器续体可被推迟到秒级），真实实现也会越过上界 ⇒ **假红**；
        //   反过来，若 LatencyMs 被忽略而机器恰好慢，30..200 也能容下 ⇒ **假绿**。
        //   改成**两次不同延迟的差值**：负载同时抬高两次测量，差值里只剩"延迟真的生效"这一项。
        var none = MeasureWriteMs(latencyMs: 0);
        var slow = MeasureWriteMs(latencyMs: 150);

        Assert.InRange(slow - none, 100, 8000);
    }

    /// <summary>量一次写入的墙钟（毫秒）。两次调用共享同一份负载 ⇒ 差值对负载不敏感。</summary>
    private static long MeasureWriteMs(int latencyMs)
    {
        var sim = new CommunicationSimulator { LatencyMs = latencyMs };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        sim.WriteRegister(1, 10);
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    [Fact]
    public void CommSim_ErrorRateOne_DropsAllWrites()
    {
        var sim = new CommunicationSimulator { ErrorRate = 1.0 };
        sim.WriteRegister(1, 10);
        // 写入被丢弃: 寄存器保持未设置(0)
        Assert.Equal(0.0, sim.ReadRegister(1));
        Assert.Empty(sim.GetChangeLog());
    }

    [Fact]
    public void CommSim_ErrorRateZero_AllWritesPass()
    {
        var sim = new CommunicationSimulator { ErrorRate = 0.0 };
        sim.WriteRegister(1, 10);
        Assert.Equal(10.0, sim.ReadRegister(1));
        Assert.Single(sim.GetChangeLog());
    }
}
