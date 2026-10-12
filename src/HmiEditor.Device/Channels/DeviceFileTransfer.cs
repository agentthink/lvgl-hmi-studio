using System.Text;
using System.Text.RegularExpressions;

namespace HmiEditor.Device.Channels;

/// <summary>
/// 设备文件传输协议 —— 自适应选择后端，兼容不同 busybox 裁剪：
///   <list type="bullet">
///     <item>设备有 base64 → base64 + heredoc 分块传输；</item>
///     <item>设备无 base64（T113 默认裁剪）→ printf '\xHH' 十六进制逐行追加，用 wc -c 校验字节数。</item>
///   </list>
/// 两种后端均仅依赖标准 shell 命令（cat/base64/printf/wc），无需改动固件。
/// </summary>
public sealed class DeviceFileTransfer
{
    private const int Base64ChunkChars = 2048;
    // 单行 printf 命令长度受设备 tty 输入缓冲限制（实测约 512 字符），
    // 96B×4hex + 命令前缀 ≈ 410 字符，留足安全余量。
    private const int PrintfChunkBytes = 96;
    private const int WriteLineDelayMs = 15;

    private readonly Func<string, TimeSpan?, CancellationToken, Task<string>> _sendCommand;
    private readonly Func<string, CancellationToken, Task> _writeLine;
    private readonly bool _hasBase64;

    public DeviceFileTransfer(
        Func<string, TimeSpan?, CancellationToken, Task<string>> sendCommand,
        Func<string, CancellationToken, Task> writeLine,
        bool hasBase64)
    {
        _sendCommand = sendCommand ?? throw new ArgumentNullException(nameof(sendCommand));
        _writeLine = writeLine ?? throw new ArgumentNullException(nameof(writeLine));
        _hasBase64 = hasBase64;
    }

    /// <summary>
    /// 传输本地文件到设备指定路径。传输后校验设备端文件大小一致，不一致抛异常。
    /// </summary>
    public async Task TransferFileAsync(string localPath, string remotePath, IProgress<double>? progress, CancellationToken ct)
    {
        if (!File.Exists(localPath))
            throw new DeviceChannelException($"本地文件不存在: {localPath}");
        if (!Path.IsPathRooted(remotePath))
            throw new DeviceChannelException($"设备路径必须为绝对路径: {remotePath}");

        var data = await File.ReadAllBytesAsync(localPath, ct);

        if (_hasBase64)
            await TransferViaBase64Async(data, remotePath, progress, ct);
        else
            await TransferViaPrintfHexAsync(data, remotePath, progress, ct);

        await VerifySizeAsync(data.Length, remotePath, ct);
        progress?.Report(1.0);
    }

    /// <summary>base64 后端：cat heredoc 接收文本 → base64 -d 还原。</summary>
    private async Task TransferViaBase64Async(byte[] data, string remotePath, IProgress<double>? progress, CancellationToken ct)
    {
        var base64 = Convert.ToBase64String(data);
        var totalChunks = (base64.Length + Base64ChunkChars - 1) / Base64ChunkChars;

        await _sendCommand($"cat > {ShellQuote(remotePath)} << 'HMIUPLOAD'", TimeSpan.FromSeconds(5), ct);
        for (var i = 0; i < base64.Length; i += Base64ChunkChars)
        {
            ct.ThrowIfCancellationRequested();
            var chunk = base64.Substring(i, Math.Min(Base64ChunkChars, base64.Length - i));
            await _writeLine(chunk, ct);
            progress?.Report((double)(i + chunk.Length) / base64.Length * 0.9);
        }
        await _sendCommand("HMIUPLOAD", TimeSpan.FromSeconds(5), ct);

        var decode = await _sendCommand(
            $"base64 -d {ShellQuote(remotePath)} > {ShellQuote(remotePath)}.tmp && mv {ShellQuote(remotePath)}.tmp {ShellQuote(remotePath)}",
            TimeSpan.FromSeconds(30), ct);
        if (ContainsFailure(decode))
            throw new DeviceChannelException($"设备端 base64 解码失败: {Truncate(decode, 200)}");
    }

    /// <summary>printf-hex 后端：逐行 printf '\xNN' 追加，适合无 base64/uudecode 的 busybox。</summary>
    private async Task TransferViaPrintfHexAsync(byte[] data, string remotePath, IProgress<double>? progress, CancellationToken ct)
    {
        // 清空目标文件
        await _sendCommand($": > {ShellQuote(remotePath)}", TimeSpan.FromSeconds(5), ct);

        var sb = new StringBuilder(PrintfChunkBytes * 6);
        for (var i = 0; i < data.Length; i += PrintfChunkBytes)
        {
            ct.ThrowIfCancellationRequested();
            var count = Math.Min(PrintfChunkBytes, data.Length - i);
            sb.Clear();
            sb.Append("printf '");
            for (var j = 0; j < count; j++)
                sb.Append("\\x").Append(data[i + j].ToString("x2"));
            sb.Append("' >> ").Append(ShellQuote(remotePath));

            await _writeLine(sb.ToString(), ct);
            progress?.Report((double)(i + count) / data.Length * 0.9);
        }

        // 同步：等 shell 处理完所有 printf 行
        await _sendCommand("echo HMI_SYNC", TimeSpan.FromSeconds(10), ct);
    }

    private async Task VerifySizeAsync(int expectedSize, string remotePath, CancellationToken ct)
    {
        var sizeOut = await _sendCommand($"wc -c < {ShellQuote(remotePath)}", TimeSpan.FromSeconds(10), ct);
        // 输出含命令回显，提取第一个数字作为实际字节数
        var match = Regex.Match(sizeOut, @"\d+");
        if (match.Success && int.TryParse(match.Value, out var actual) && actual == expectedSize)
            return;
        throw new DeviceChannelException(
            $"设备端文件大小校验失败: 期望 {expectedSize} 字节, 实际 '{Truncate(sizeOut.Trim(), 80)}'（传输可能不完整）");
    }

    private static bool ContainsFailure(string output)
    {
        var lower = output.ToLowerInvariant();
        return lower.Contains("not found", StringComparison.Ordinal) ||
               lower.Contains("denied", StringComparison.Ordinal) ||
               lower.Contains("error", StringComparison.Ordinal);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";

    /// <summary>
    /// 把设备路径安全地放进 sh 命令 —— 单引号包裹，并转义内嵌单引号（<c>'</c> → <c>'\''</c>）。
    ///
    /// <para><b>为什么必须：</b>路径最终来自工程 / 压缩包里的**文件名**
    /// （<c>HmiDeployService</c> 只做了 <c>Path.GetFileName</c>），可以含空格、<c>;</c>、
    /// <c>$()</c>、反引号。原样插进 <c>cat &gt; {p}</c> 这类命令：轻则"带空格的文件名"
    /// 直接部署失败，重则可在设备上以 **root** 执行任意 shell（阶段 120 修复）。
    /// 换行 / NUL 没法在单行命令里安全表达 ⇒ 直接拒绝。</para>
    /// </summary>
    private static string ShellQuote(string path)
    {
        if (path.IndexOfAny(new[] { '\n', '\r', '\0' }) >= 0)
            throw new DeviceChannelException("设备路径含换行或 NUL，拒绝下发");
        return "'" + path.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }
}
