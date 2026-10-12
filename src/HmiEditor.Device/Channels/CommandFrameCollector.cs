using System.Text;
using System.Text.RegularExpressions;

namespace HmiEditor.Device.Channels;

/// <summary>
/// 命令帧收集器 —— "一帧响应从哪开始、到哪结束"的**唯一实现**，TCP / 串口两个通道共用。
///
/// <para><b>职责边界（这是修掉历史缺陷的关键）：</b></para>
/// <list type="bullet">
///   <item><b>生产者</b>（各通道的后台读循环）只做两件事：把收到的字节追加进当前帧、记录"最后一段
///     数据"到达的时刻。它**不判定帧结束**，也不维护任何"正在收数据"的标志。</item>
///   <item><b>消费者</b>（通道的 <c>SendCommandCoreAsync</c>）开一帧、写命令、等这一帧结束。</item>
/// </list>
///
/// <para><b>结束判据</b>（两条互斥）：</para>
/// <list type="number">
///   <item>收到过数据 ⇒ 从<b>最后一段数据到达的时刻</b>起算静默 <c>silentMs</c> 即结束；</item>
///   <item>一字节未到 ⇒ 从写入时刻起算过 <c>firstByteMs</c> 即判"本命令无输出"，返回空串
///     （保留"设备不回显时不空等满超时"的原语义）。</item>
/// </list>
///
/// <para><b>为什么两条判据必须分开</b>：静默期管的是"帧内两段数据之间的间隙"，首字节窗口管的是
/// "从写完命令到设备吐出第一段数据"。设备响应起步受 shell 调度、进程抢占、主机负载影响，
/// 天然慢于帧内间隙。2026-09-13 起两者共用同一个 300 ms 的静默期、且静默<b>从写入时刻</b>起算
/// ⇒ 设备只是起步慢了半点就被判成"无输出"（返回空串），迟到的回包再落进下一条命令的窗口
/// ⇒ 一条命令拿到空串、另一条拿到两行响应（响应张冠李戴）。</para>
///
/// <para><b>每帧独立</b>：<c>SawData</c> / <c>LastDataTicks</c> 挂在帧实例上，不跨帧共享。
/// 上一帧的迟到数据既不会让本帧被误认为"已经收到数据"、也不会污染本帧的静默起算点。</para>
/// </summary>
public sealed class CommandFrameCollector
{
    /// <summary>轮询间隔（毫秒）—— 静默判定的实际时间精度由它决定。</summary>
    public const int PollIntervalMs = 20;

    /// <summary>一帧的接收状态：生产者写、消费者读。</summary>
    private sealed class Frame
    {
        public readonly StringBuilder Buffer = new();
        public bool SawData;
        public long LastDataTicks;
    }

    private readonly object _sync = new();
    private Frame _current = new();

    /// <summary>
    /// 生产者接口：把收到的字节追加进**当前帧**。由各通道的后台读循环调用。
    /// <para>只追加与打时间戳 —— 不判定帧结束、不翻转任何状态标志。</para>
    /// </summary>
    public void Append(string text)
    {
        lock (_sync)
        {
            _current.Buffer.Append(text);
            _current.SawData = true;
            _current.LastDataTicks = Environment.TickCount64;
        }
    }

    /// <summary>
    /// 消费者接口：开一帧 → 写命令 → 等这一帧结束，返回该帧收到的原始文本。
    /// <para>开帧在写入**之前**，保证命令回显一定落在本帧内。</para>
    /// </summary>
    /// <param name="writeCommand">把命令写进底层流（在开帧之后调用，可异步）。</param>
    /// <param name="silentMs">帧内静默期。</param>
    /// <param name="firstByteMs">首字节宽限窗口；实际生效值不小于 <paramref name="silentMs"/>。</param>
    /// <param name="ct">命令级取消（含整体超时）。</param>
    public async Task<string> SendAsync(
        Func<CancellationToken, Task> writeCommand,
        int silentMs,
        int firstByteMs,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(writeCommand);

        // 首字节窗口不得小于静默期：有数据时我们都容忍 silentMs 的输出间隙，
        // 那么"连第一个字节都还没出来"这个更慢的阶段就更不该被更早判死。
        // 否则判据自相矛盾：容忍 300 ms 的行间隙，却不容忍 100 ms 的起步。
        var effectiveFirstByte = Math.Max(firstByteMs, silentMs);

        Frame frame;
        lock (_sync) { frame = _current = new Frame(); }

        await writeCommand(ct).ConfigureAwait(false);

        var firstByteDeadline = Environment.TickCount64 + effectiveFirstByte;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (frame.SawData)
                {
                    if (Environment.TickCount64 - frame.LastDataTicks >= silentMs)
                        return frame.Buffer.ToString();
                }
                else if (Environment.TickCount64 >= firstByteDeadline)
                {
                    return string.Empty;
                }
            }
            await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// 命令输出清洗 —— TCP / 串口两个通道共用（原来各持有一份逐字相同的私有副本）。
/// </summary>
internal static class CommandOutput
{
    /// <summary>去除回显中的 CR / 尾部控制字符与 busybox 提示符行，便于解析。</summary>
    public static string Sanitize(string raw)
    {
        var text = raw.Replace("\r", "").TrimEnd('\0', '\n', '\r', ' ');
        // 去掉 busybox 提示符行
        text = Regex.Replace(text, @"\n[#$\s]+\n", "\n");
        return text.Trim();
    }
}
