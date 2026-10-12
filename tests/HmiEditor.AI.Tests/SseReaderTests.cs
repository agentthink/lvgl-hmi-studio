using System.Text;
using HmiEditor.AI.Inference;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// SSE 切帧测试 —— 覆盖 OpenAI / Claude 流式响应共同依赖的行语法与边界。
/// </summary>
public class SseReaderTests
{
    private static async Task<List<SseFrame>> ReadAll(string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var frames = new List<SseFrame>();
        await foreach (var frame in SseReader.ReadAsync(stream))
            frames.Add(frame);
        return frames;
    }

    [Fact]
    public async Task ReadAsync_TwoFramesSeparatedByBlankLine_YieldsBoth()
    {
        var frames = await ReadAll("data: one\n\ndata: two\n\n");

        Assert.Equal(2, frames.Count);
        Assert.Equal("one", frames[0].Data);
        Assert.Equal("two", frames[1].Data);
    }

    [Fact]
    public async Task ReadAsync_EventField_IsCarriedOnFrame()
    {
        var frames = await ReadAll("event: content_block_delta\ndata: {\"a\":1}\n\n");

        Assert.Single(frames);
        Assert.Equal("content_block_delta", frames[0].Event);
        Assert.Equal("{\"a\":1}", frames[0].Data);
    }

    [Fact]
    public async Task ReadAsync_MultiLineData_JoinedWithNewline()
    {
        // SSE 规范：同一帧的多条 data 行用 "\n" 连接（不是原样拼接）
        var frames = await ReadAll("data: line1\ndata: line2\n\n");

        Assert.Single(frames);
        Assert.Equal("line1\nline2", frames[0].Data);
    }

    [Theory]
    [InlineData(": heartbeat\n\ndata: x\n\n")]           // 注释行被忽略
    [InlineData("data: x\n\n: trailing comment\n\n")]     // 注释不产生帧
    public async Task ReadAsync_CommentLines_AreIgnored(string text)
    {
        var frames = await ReadAll(text);
        Assert.Single(frames);
        Assert.Equal("x", frames[0].Data);
    }

    [Fact]
    public async Task ReadAsync_DoneToken_IsPreservedForCaller()
    {
        // [DONE] 是 OpenAI 的哨兵，切帧层不解释语义 —— 交给 Provider 决定
        var frames = await ReadAll("data: [DONE]\n\n");

        Assert.Single(frames);
        Assert.Equal(SseReader.DoneToken, frames[0].Data);
    }

    [Fact]
    public async Task ReadAsync_MissingTrailingBlankLine_StillYieldsLastFrame()
    {
        // 部分中转在最后不补空行；丢掉最后一段正文会表现为"回答被截断"
        var frames = await ReadAll("data: incomplete");

        Assert.Single(frames);
        Assert.Equal("incomplete", frames[0].Data);
    }

    [Fact]
    public async Task ReadAsync_CrlfLineEndings_AreAccepted()
    {
        var frames = await ReadAll("data: one\r\n\r\ndata: two\r\n\r\n");

        Assert.Equal(2, frames.Count);
        Assert.Equal("one", frames[0].Data);
        Assert.Equal("two", frames[1].Data);
    }

    [Fact]
    public async Task ReadAsync_FieldWithoutColon_TreatedAsEmptyValueNotException()
    {
        // 一行坏数据（无冒号）不该让整次生成失败；空 data 帧不产出 ——
        // 产出空增量只会让 UI 空刷新，对 Provider 也无意义（"" 解析必然失败）。
        var frames = await ReadAll("data:x\n\ndata\n\ndata: y\n\n");

        Assert.Equal(2, frames.Count);
        Assert.Equal("x", frames[0].Data);
        Assert.Equal("y", frames[1].Data);
    }

    [Fact]
    public async Task ReadAsync_FrameWithoutData_ProducesNoFrame()
    {
        // 只有 event 名没有 data ⇒ 不产出（避免回调空增量）
        var frames = await ReadAll("event: ping\n\ndata: real\n\n");

        Assert.Single(frames);
        Assert.Equal("real", frames[0].Data);
    }

    [Fact]
    public async Task ReadAsync_DataValueWithLeadingSpace_TrimsExactlyOneSpace()
    {
        // 规范：冒号后恰好一个前导空格属于分隔，不算负载
        var frames = await ReadAll("data:  two-spaces\n\n");

        Assert.Single(frames);
        Assert.Equal(" two-spaces", frames[0].Data);
    }

    [Fact]
    public async Task ReadAsync_EmptyStream_YieldsNothing()
    {
        Assert.Empty(await ReadAll(""));
    }

    [Fact]
    public async Task ReadAsync_Utf8ChinesePayload_RoundTrips()
    {
        var frames = await ReadAll("data: {\"text\":\"电机启动\"}\n\n");

        Assert.Single(frames);
        Assert.Contains("电机启动", frames[0].Data);
    }

    /// <summary>
    /// **流式性证明**：拿到第一帧就退出。若实现是"先读完整个流再切帧"
    /// （`ReadToEnd` 式写法），第一次迭代会一直等 <see cref="GateStream"/> 关流而挂到超时。
    /// 这是"首字节延迟"这一用户体验指标在单元测试里唯一的确定性判据。
    /// </summary>
    [Fact]
    public async Task ReadAsync_YieldsFirstFrameBeforeStreamEnds()
    {
        using var stream = new GateStream("data: {\"n\":1}\n\n");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var frames = new List<SseFrame>();
        await foreach (var frame in SseReader.ReadAsync(stream, leaveOpen: true, cts.Token))
        {
            frames.Add(frame);
            break; // 断言"不必等流结束"：继续读到 EOF 就永远等不到
        }

        Assert.Single(frames);
        Assert.Equal("{\"n\":1}", frames[0].Data);
        stream.Release();
    }

    /// <summary>只吐第一段、之后挂起（模拟"连接还开着但后续数据未到"）的流。</summary>
    private sealed class GateStream : Stream
    {
        private readonly byte[] _first;
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _pos;

        public GateStream(string first) => _first = Encoding.UTF8.GetBytes(first);

        public void Release() => _gate.TrySetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_pos < _first.Length)
            {
                var n = Math.Min(buffer.Length, _first.Length - _pos);
                _first.AsMemory(_pos, n).CopyTo(buffer);
                _pos += n;
                return n;
            }

            await _gate.Task.WaitAsync(ct);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _pos; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
