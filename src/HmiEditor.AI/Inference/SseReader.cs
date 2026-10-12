using System.Runtime.CompilerServices;
using System.Text;

namespace HmiEditor.AI.Inference;

/// <summary>SSE 单帧：可选 event 名 + data 负载（多行 data 已按规范用 "\n" 拼接）。</summary>
public readonly record struct SseFrame(string? Event, string Data);

/// <summary>
/// Server-Sent Events 解析器 —— OpenAI 与 Claude 的流式响应共用同一套行语法。
///
/// <para><b>为什么单独成一个类型：</b>「按 SSE 规范切帧」是协议层的事，与「哪个字段是正文、
/// 哪个字段是 token 用量」无关。两个 Provider 各写一份切帧代码的必然结局是分叉
/// —— 项目里已有多处「同一规则两份实现后漂移」的历史（见 <c>docs/project-status.md</c> 阶段 6）。
/// 这里只做切帧，语义留给调用方。</para>
///
/// <para><b>刻意容忍的三种现实：</b>① 部分中转不补帧尾空行 ⇒ 流结束时把残留 data 也交出去；
/// ② 心跳/注释行（以 <c>:</c> 开头）⇒ 忽略；③ 无冒号的字段行 ⇒ 按空值处理而不是抛异常
/// （一行坏数据不该让整次生成失败）。</para>
/// </summary>
public static class SseReader
{
    /// <summary>OpenAI 流式结束哨兵帧。</summary>
    public const string DoneToken = "[DONE]";

    /// <summary>
    /// 逐帧读取 SSE。<paramref name="leaveOpen"/> 为 true 时不关闭底层流（响应流由调用方释放）。
    /// </summary>
    public static async IAsyncEnumerable<SseFrame> ReadAsync(
        Stream stream,
        bool leaveOpen = true,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(
            stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: leaveOpen);

        string? eventName = null;
        var data = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            // 空行 = 帧分隔符
            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    yield return new SseFrame(eventName, data.ToString());
                    eventName = null;
                    data.Clear();
                }
                continue;
            }

            // 注释 / 心跳
            if (line[0] == ':') continue;

            var colon = line.IndexOf(':');
            string field;
            string value;
            if (colon < 0)
            {
                field = line;
                value = "";
            }
            else
            {
                field = line[..colon];
                value = line[(colon + 1)..];
                if (value.StartsWith(' ')) value = value[1..];
            }

            switch (field)
            {
                case "event":
                    eventName = value;
                    break;
                case "data":
                    if (data.Length > 0) data.Append('\n');
                    data.Append(value);
                    break;
            }
        }

        // 流在最后一个帧分隔符之前结束：把残留 data 交出去（部分中转如此）
        if (data.Length > 0)
            yield return new SseFrame(eventName, data.ToString());
    }
}
