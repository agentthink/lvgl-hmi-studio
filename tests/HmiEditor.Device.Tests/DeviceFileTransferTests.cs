using HmiEditor.Device.Channels;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.Device.Tests;

/// <summary>
/// 设备文件传输的**命令构造判据**（阶段 120）。
///
/// <para>背景：远端路径最终来自工程 / 压缩包里的**文件名**（`HmiDeployService` 只做了
/// <c>Path.GetFileName</c>），可含空格、<c>;</c>、<c>$()</c>、反引号。原样插进
/// <c>cat &gt; {p}</c> 这类 shell 命令：带空格就部署失败，带元字符则可**在设备上以 root
/// 执行任意 shell**。修法是单引号包裹 + 转义内嵌单引号。</para>
///
/// <para>判据读的是**真正发出去的命令字符串**（注入的 sendCommand 捕获），不是源码形状：
/// 断言"危险路径必须出现在单引号里"，且"未加引号的原文不许出现在任何命令里"。</para>
/// </summary>
public class DeviceFileTransferTests
{
    /// <summary>跑一次传输，返回捕获到的所有命令。sendCommand 对 `wc -c` 回一个合法数字（走完校验）。</summary>
    private static async Task<List<string>> CaptureCommandsAsync(bool hasBase64, string remotePath)
    {
        var commands = new List<string>();
        var local = TestTempDir.NewFilePath("devtransfer", ".bin");
        await File.WriteAllBytesAsync(local, new byte[] { 0x00, 0x01, 0x02, 0xFF });
        try
        {
            var transfer = new DeviceFileTransfer(
                sendCommand: (cmd, _, _) =>
                {
                    commands.Add(cmd);
                    return Task.FromResult(cmd.Contains("wc -c", StringComparison.Ordinal) ? "4" : "");
                },
                writeLine: (_, _) => Task.CompletedTask,
                hasBase64: hasBase64);

            await transfer.TransferFileAsync(local, remotePath, null, CancellationToken.None);
            return commands;
        }
        finally
        {
            TestTempDir.DeleteFile(local);
        }
    }

    private static string Quote(string path) =>
        "'" + path.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    [Theory]
    [InlineData(true)]    // base64 后端
    [InlineData(false)]   // printf-hex 后端（走 append 那一处）
    public async Task RemotePath_WithMetacharacters_IsSingleQuoted(bool hasBase64)
    {
        const string remote = "/mnt/UDISK/hmi/assets/evil; touch /tmp/pwned.png";

        var commands = await CaptureCommandsAsync(hasBase64, remote);
        var quoted = Quote(remote);

        // 必须出现过加引号的形态（否则 shell 会把 `;` 当命令分隔符）
        Assert.Contains(commands, c => c.Contains(quoted, StringComparison.Ordinal));

        // 🩸 主判据：没有任何一条命令里出现**未加引号**的原文
        Assert.DoesNotContain(commands, c => c.Contains(remote, StringComparison.Ordinal) &&
                                             !c.Contains(quoted, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RemotePath_WithSingleQuote_EscapesIt()
    {
        const string remote = "/mnt/UDISK/hmi/assets/a'b.png";

        var commands = await CaptureCommandsAsync(hasBase64: true, remote);
        Assert.Contains(commands, c => c.Contains(Quote(remote), StringComparison.Ordinal));
        // 内嵌单引号被转义成 '\''（否则引号会提前闭合、后面的内容变成 shell 语法）
        Assert.Contains(commands, c => c.Contains("a'\\''b.png", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/mnt/UDISK/hmi/assets/a\nb.png")]
    [InlineData("/mnt/UDISK/hmi/assets/a\rb.png")]
    public async Task RemotePath_WithNewline_IsRejected(string remote)
    {
        await Assert.ThrowsAsync<DeviceChannelException>(
            () => CaptureCommandsAsync(hasBase64: true, remote));
    }
}
