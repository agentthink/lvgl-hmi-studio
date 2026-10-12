using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HmiEditor.Device.Packaging;

/// <summary>
/// swu 包内数据文件 —— 源可为本地文件路径或直接字节数组。
/// </summary>
public sealed class SwuPayloadFile
{
    /// <summary>归档内的文件名（sw-description 中引用的 filename 需一致）。</summary>
    public required string CpioName { get; init; }

    /// <summary>本地源文件路径（与 <see cref="Data"/> 二选一）。</summary>
    public string? LocalPath { get; init; }

    /// <summary>直接提供字节内容（与 <see cref="LocalPath"/> 二选一）。</summary>
    public byte[]? Data { get; init; }
}

/// <summary>
/// .swu 升级包打包器 —— 与 Tina SDK 的 <c>swupdate_pack_swu</c> 输出结构一致：
///   sw-description（Libconfig 文本，sha256="@name" 被替换为真实值）
///   &lt;payload 文件&gt;...
///   cpio_item_md5（每行 "<md5>  <name>"）
/// 全部打包为 CPIO crc 归档（swupdate 要求的格式）。
/// </summary>
public sealed class SwuPackageBuilder
{
    /// <summary>
    /// 打包 .swu 文件。
    /// </summary>
    /// <param name="swDescriptionTemplate">
    /// sw-description 模板文本。其中 <c>sha256 = "@name"</c> / <c>size = "@name"</c>
    /// 会按 payload 实际值替换；模板中 <c>{version}</c> 占位符会被 <paramref name="version"/> 替换。
    /// </param>
    /// <param name="payloads">归档内数据文件（sw-description 之后的条目顺序）。</param>
    /// <param name="outputPath">输出 .swu 路径。</param>
    /// <param name="version">软件版本（写入 sw-description 的 version 字段）。</param>
    public async Task BuildAsync(
        string swDescriptionTemplate,
        IReadOnlyList<SwuPayloadFile> payloads,
        string outputPath,
        string version = "1.0.0",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(swDescriptionTemplate);
        ArgumentNullException.ThrowIfNull(payloads);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // 1. 载入所有 payload
        var files = new List<(string Name, byte[] Data)>(payloads.Count);
        foreach (var payload in payloads)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(payload.CpioName))
                throw new ArgumentException("payload 的 CpioName 不能为空");
            var data = payload.Data
                ?? await File.ReadAllBytesAsync(payload.LocalPath!, ct)
                ?? throw new ArgumentException($"payload '{payload.CpioName}' 既无 LocalPath 也无 Data");
            files.Add((payload.CpioName, data));
        }

        // 2. 计算 sha256/size 并替换模板中的 "@name" 引用
        var description = swDescriptionTemplate.Replace("{version}", version, StringComparison.Ordinal);
        foreach (var (name, data) in files)
        {
            ct.ThrowIfCancellationRequested();
            var namePattern = Regex.Escape(name);
            var hash = Convert.ToHexStringLower(SHA256.HashData(data));
            description = Regex.Replace(
                description,
                $@"sha256\s*=\s*""@{namePattern}""",
                $"sha256 = \"{hash}\"");
            description = Regex.Replace(
                description,
                $@"size\s*=\s*""@{namePattern}""",
                $"size = \"{data.Length}\"");
        }

        // 2b. 校验模板引用的所有文件均已提供（残留 "@name" 会导致 swupdate 报缺文件）
        var leftover = Regex.Match(description, @"@[A-Za-z0-9_.-]+");
        if (leftover.Success)
            throw new ArgumentException(
                $"sw-description 引用了未提供的文件 '{leftover.Value}'（缺少对应 SwuPayloadFile）");

        // 3. 生成 cpio_item_md5（与 SDK 中 md5sum 输出格式一致：两个空格分隔）
        var descriptionBytes = Encoding.UTF8.GetBytes(description);
        var md5Builder = new StringBuilder();
        md5Builder.AppendLine($"{Md5Hex(descriptionBytes)}  sw-description");
        foreach (var (name, data) in files)
            md5Builder.AppendLine($"{Md5Hex(data)}  {name}");
        var md5Bytes = Encoding.UTF8.GetBytes(md5Builder.ToString());

        // 4. 写入 CPIO crc 归档
        using var fs = File.Create(outputPath);
        using (var cpio = new CpioArchiveWriter(fs, leaveOpen: true))
        {
            cpio.WriteEntry("sw-description", descriptionBytes);
            foreach (var (name, data) in files)
                cpio.WriteEntry(name, data);
            cpio.WriteEntry("cpio_item_md5", md5Bytes);
            cpio.Finish();
        }
    }

    private static string Md5Hex(byte[] data) =>
        Convert.ToHexStringLower(MD5.HashData(data));
}
