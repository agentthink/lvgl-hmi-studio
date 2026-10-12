using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HmiEditor.Device.Channels;
using Xunit;

namespace HmiEditor.Device.Tests;

/// <summary>
/// TCP 通道回归测试（2026-09-13 审查 P1-5）：
///   - 整帧串行化：并发命令不得在同一 socket 上交错，响应不得张冠李戴；
///   - 收帧判据：设备起步慢（首字节晚于静默期）不得被判成"命令无输出"；
///   - 超时统一为 <see cref="DeviceChannelException"/>，调用方取消保持 <see cref="OperationCanceledException"/>；
///   - 关闭后立即失败、不挂死。
/// 用 loopback 假服务器，不依赖真实设备。
/// </summary>
public class DeviceChannelTests
{
    /// <summary>
    /// 极简行服务器：每收到一行回 "ok:&lt;line&gt;"。
    /// <para><c>silent = true</c> 只读不回；<c>replyDelayMs</c> 在写出每条回包前先延迟 ——
    /// 用来**确定性**构造"设备起步慢"（首字节晚于静默期）这一场景，
    /// 而不是靠全量并行的负载去撞。</para>
    /// </summary>
    private sealed class LineServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly bool _silent;
        private readonly int _replyDelayMs;
        private readonly Task _loop;

        public int Port { get; }

        public LineServer(bool silent = false, int replyDelayMs = 0)
        {
            _silent = silent;
            _replyDelayMs = replyDelayMs;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _loop = Task.Run(AcceptLoopAsync);
        }

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                    _ = Task.Run(() => ServeAsync(client));
                }
            }
            catch { /* 停止时忽略 */ }
        }

        private async Task ServeAsync(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var buf = new byte[1024];
                    var pending = new StringBuilder();
                    while (!_cts.IsCancellationRequested)
                    {
                        var n = await stream.ReadAsync(buf, _cts.Token).ConfigureAwait(false);
                        if (n == 0) return;
                        pending.Append(Encoding.UTF8.GetString(buf, 0, n));
                        var s = pending.ToString();
                        int idx;
                        while ((idx = s.IndexOf('\n')) >= 0)
                        {
                            var line = s[..idx].TrimEnd('\r');
                            s = s[(idx + 1)..];
                            if (_silent) continue;
                            if (_replyDelayMs > 0)
                                await Task.Delay(_replyDelayMs, _cts.Token).ConfigureAwait(false);
                            var payload = Encoding.UTF8.GetBytes("ok:" + line + "\n");
                            await stream.WriteAsync(payload, _cts.Token).ConfigureAwait(false);
                            await stream.FlushAsync(_cts.Token).ConfigureAwait(false);
                        }
                        pending.Clear();
                        pending.Append(s);
                    }
                }
            }
            catch { /* 连接结束忽略 */ }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try { _listener.Stop(); } catch { /* 忽略 */ }
            try { await _loop.ConfigureAwait(false); } catch { /* 忽略 */ }
            _cts.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentCommands_AreSerialized_NoCrossTalk()
    {
        // 回包延迟 400ms 故意大于默认静默期 300ms —— 这正是 2026-09-16 门禁首跑红的场景：
        // 旧实现把"设备起步慢"判成"命令无输出"，那条迟到的回包再落进下一条命令的窗口，
        // 于是第一条拿到空串、第二条拿到两行。
        //
        // 首字节窗口同样显式放宽（默认 1000ms 与 400ms 的回包延迟只差 600ms，同一个余量问题）。
        await using var server = new LineServer(replyDelayMs: 400);
        using var ch = new TcpDeviceChannel("127.0.0.1", server.Port)
        {
            FirstByteTimeoutMs = 5_000,   // SilentTimeoutMs 保持默认 300
        };
        await ch.OpenAsync();

        var t1 = ch.SendCommandAsync("AAA");
        var t2 = ch.SendCommandAsync("BBB");
        var results = await Task.WhenAll(t1, t2);

        // 串行化后每个命令只拿到自己的回显；若交错，任一侧会带出对方的 ok: 行
        Assert.Equal(new[] { "ok:AAA", "ok:BBB" }, results.OrderBy(x => x).ToArray());
        // 串帧的两种形态都必须不存在：一条命令同时看到两条回包、或有命令拿到空串
        Assert.DoesNotContain(results, r => r.Contains("ok:AAA") && r.Contains("ok:BBB"));
        Assert.DoesNotContain(results, r => string.IsNullOrEmpty(r));
    }

    [Fact]
    public async Task SlowFirstByte_WithinGraceWindow_IsNotMistakenForEmptyOutput()
    {
        // 首字节 600ms > 默认静默期 300ms。
        // 旧实现里静默从"写入时刻"起算 ⇒ 300ms 到点即返回空串（哪怕回包下一秒就到）。
        //
        // 首字节窗口**显式**给到 5000ms（默认 1000 不够）：这条用例的分辨力只来自相对大小
        // 「静默期 300 < 回包延迟 600 < 首字节窗口」，与绝对时长无关；
        // 而窗口与回包延迟只差 400ms 时，全量并行下会被负载吃掉 —— 实测同一程序集
        // 单跑 3s、门禁里 37s（约 12×），600ms 的定时器滑到 1000ms 之后，
        // 于是**正确的实现被误判成"命令无输出"**（假红，2026-09-16 第 20 轮门禁首跑即此场景）。
        // 判据仍然有效：实现若按静默期判帧结束，300ms 就返回空串，这里照样红。
        await using var server = new LineServer(replyDelayMs: 600);
        using var ch = new TcpDeviceChannel("127.0.0.1", server.Port)
        {
            // SilentTimeoutMs 保持默认 300 —— "回包慢过静默期"这个前提不能丢
            FirstByteTimeoutMs = 5_000,
        };
        await ch.OpenAsync();

        var output = await ch.SendCommandAsync("AAA");

        Assert.Equal("ok:AAA", output);
    }

    [Fact]
    public async Task NoReply_ReturnsEmptyAfterFirstByteWindow_InsteadOfFullCommandTimeout()
    {
        // 设备完全不回显时，仍应在"首字节窗口"后返回空 —— 保留原设计意图，不空等满命令超时。
        await using var server = new LineServer(silent: true);
        using var ch = new TcpDeviceChannel("127.0.0.1", server.Port)
        {
            FirstByteTimeoutMs = 300,
            CommandTimeoutSeconds = 30,
        };
        await ch.OpenAsync();

        var sw = Stopwatch.StartNew();
        var output = await ch.SendCommandAsync("PING");
        sw.Stop();

        Assert.Equal("", output);
        Assert.True(sw.ElapsedMilliseconds < 10_000,
            $"设备不回显时应在一个首字节窗口后返回空，实际用了 {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task UnresponsiveServer_TimesOut_AsDeviceChannelException()
    {
        await using var server = new LineServer(silent: true);
        using var ch = new TcpDeviceChannel("127.0.0.1", server.Port)
        {
            // 两个窗口都远大于命令超时：强制走"命令超时"分支，
            // 而不是"首字节窗口到点、返回空串"分支
            SilentTimeoutMs = 30_000,
            FirstByteTimeoutMs = 30_000,
            CommandTimeoutSeconds = 1,
        };
        await ch.OpenAsync();

        var ex = await Assert.ThrowsAsync<DeviceChannelException>(() => ch.SendCommandAsync("PING"));
        Assert.Contains("超时", ex.Message);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesOperationCanceled_NotDeviceError()
    {
        await using var server = new LineServer(silent: true);
        using var ch = new TcpDeviceChannel("127.0.0.1", server.Port)
        {
            SilentTimeoutMs = 30_000,
            FirstByteTimeoutMs = 30_000,
        };
        await ch.OpenAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // 调用方取消必须原样抛 OperationCanceledException，便于上层区分"用户取消"与"设备无响应"
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ch.SendCommandAsync("PING", null, cts.Token));
    }

    [Fact]
    public async Task CloseAsync_ThenCommand_FailsFast_AndCloseDoesNotHang()
    {
        await using var server = new LineServer();
        var ch = new TcpDeviceChannel("127.0.0.1", server.Port);
        await ch.OpenAsync();
        Assert.True(ch.IsOpen);

        var closeTask = ch.CloseAsync();
        var done = await Task.WhenAny(closeTask, Task.Delay(2000));
        Assert.Same(closeTask, done);       // 关闭必须在 2s 内返回（读循环可取消）
        Assert.False(ch.IsOpen);

        await Assert.ThrowsAsync<DeviceChannelException>(() => ch.SendCommandAsync("X"));
        ch.Dispose();
    }
}
