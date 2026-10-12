using System.Diagnostics;
using Hmi.Communication.Contracts;
using HmiEditor.App.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// <see cref="ModbusRuntimeSource"/> 的**非阻塞取值**判据（阶段 120）。
///
/// <para>背景：`TryRead` 由渲染路径逐帧调用（`DesignCanvas.Render` 对每个带标签的显示控件）。
/// 旧实现直接 `GetAwaiter().GetResult()` 发 Modbus 请求 ⇒ 接上真实设备后，每个控件每帧
/// 都要等一个完整往返（预览超时 1500ms）⇒ **UI 无限冻结**。</para>
///
/// <para>判据：①`TryRead` 必须在**慢驱动**下也立刻返回（不许阻塞调用线程）；
/// ②真实读取由后台泵完成、随后能从缓存里读到值。少了 ①，旧实现照样"绿"。</para>
/// </summary>
public class ModbusRuntimeSourceTests
{
    /// <summary>每次读都要 400ms 的假驱动 —— 模拟"真实设备 + 一个完整往返"。</summary>
    private sealed class SlowDriver : IRuntimeCommunicationDriver
    {
        private int _reads;
        public int Reads => Volatile.Read(ref _reads);

        public bool IsConnected => true;
        public bool Connect(ConnectionConfig config, TimeSpan timeout) => true;
        public void Disconnect() { }

        public async Task<ushort[]> ReadHoldingRegistersAsync(
            ushort startAddress, ushort count, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _reads);
            await Task.Delay(400, ct).ConfigureAwait(false);
            return new ushort[] { 42 };
        }

        public Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, CancellationToken ct = default)
            => Task.FromResult(new[] { true });
        public Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    [Fact]
    public void TryRead_DoesNotBlock_AndIsServedFromTheBackgroundPump()
    {
        var driver = new SlowDriver();
        using var source = new ModbusRuntimeSource(driver, _ => "40001");

        var sw = Stopwatch.StartNew();
        var firstOk = source.TryRead("v1", out var first);
        sw.Stop();

        // ① 主判据：慢驱动下也不许阻塞调用线程（旧实现这里要等 400ms+）
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"TryRead 阻塞了 {sw.ElapsedMilliseconds}ms —— 渲染路径会逐帧冻结 UI（旧缺陷）");

        // 首帧还没有值：返回 false ⇒ 上层回退模拟信号（这是设计行为，不是失败）
        Assert.False(firstOk);
        Assert.Equal(0, first);

        // ② 后台泵随后必须把真实值填进缓存
        var got = 0.0;
        var served = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (source.TryRead("v1", out got) && got == 42) { served = true; break; }
            Thread.Sleep(20);
        }

        Assert.True(served, "后台泵没有把值填进缓存");
        Assert.True(driver.Reads > 0, "后台泵从未向驱动发起读取");
    }
}
