using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hmi.Hardware.Contracts;
using HmiEditor.Core.Services;

namespace HmiEditor.Device.Release;

/// <summary>发布包清单里的一项产物（相对包根的 POSIX 路径 + 字节数 + sha256）。</summary>
public sealed record ReleaseFile(string Path, long SizeBytes, string Sha256);

/// <summary>
/// 发布包的**版本清单** —— 把"编辑器 ↔ 固件"四组标记并排写进产物里
/// （登记表 §4 第 29 项：四个标记都曾只是注释里的约稿）。
///
/// <para>编辑器侧来自硬件目标插件（<see cref="IHardwareTarget"/>），固件侧来自
/// <c>firmware/t113/app/runtime/*.h</c> —— 两侧都经
/// <see cref="FirmwareVersionMarkers"/> / 插件属性读入，不写第二份常量。
/// 对应关系本身由 <c>ReleasePackageTests</c> 钉住。</para>
/// </summary>
public sealed record ReleaseVersionTable(
    string Editor,
    string EditorTargetId,
    uint EditorAbiVersion,
    string EditorLvglVersion,
    string Firmware,
    string FirmwareTargetId,
    uint FirmwareAbiVersion,
    string FirmwareLvglVersion);

/// <summary>发布包清单（<c>manifest.json</c>）。</summary>
public sealed record ReleaseManifest(
    string Format,
    string Created,
    string Configuration,
    string Runtime,
    ReleaseVersionTable Versions,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<ReleaseFile> Files);

/// <summary>构建清单所需的外部输入（编辑器版本 / git 提交 / 目标 / 打包配置）。</summary>
public sealed record ReleaseManifestOptions(
    string RepoRoot,
    string StageDir,
    IHardwareTarget Target,
    string EditorVersion,
    string EditorGitCommit,
    string Configuration,
    string Runtime,
    DateTime? CreatedUtc = null);

/// <summary>清单的 JSON 读写（唯一一处格式定义）。</summary>
public static class ReleaseManifestJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // 清单里可能出现中文路径/说明：不转义，保证产物人可读、diff 可读
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize(ReleaseManifest manifest) => JsonSerializer.Serialize(manifest, Options);

    public static void Write(ReleaseManifest manifest, string path) =>
        File.WriteAllText(path, Serialize(manifest), new UTF8Encoding(false));

    public static ReleaseManifest Read(string path) =>
        JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException($"manifest.json 解析为空: {path}");
}

/// <summary>发布包清单工厂：从仓库事实源派生版本清单 + 枚举包内产物。</summary>
public static class ReleaseManifestFactory
{
    /// <summary>清单格式标识（将来改结构时靠它判别，而不是靠猜字段）。</summary>
    public const string FormatId = "zhishu-hmi-release/1";

    /// <summary>固件版本标记所在目录（仓库内唯一固件）。</summary>
    public static string FirmwareRuntimeDir(string repoRoot) =>
        Path.Combine(repoRoot, "firmware", "t113", "app", "runtime");

    /// <summary>
    /// 生成清单。<paramref name="options"/> 的 <see cref="ReleaseManifestOptions.StageDir"/>
    /// 必须已经装配完毕（编辑器产物、文档、说明都在里面）—— 清单是**对已装配内容**的封条。
    /// </summary>
    public static ReleaseManifest Create(ReleaseManifestOptions options)
    {
        var files = EnumeratePayloadFiles(options.StageDir);
        var created = (options.CreatedUtc ?? DateTime.UtcNow)
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

        return new ReleaseManifest(
            Format: FormatId,
            Created: created,
            Configuration: options.Configuration,
            Runtime: options.Runtime,
            Versions: CreateVersions(options.RepoRoot, options.Target, options.EditorVersion),
            FileCount: files.Count,
            TotalBytes: files.Sum(f => f.SizeBytes),
            Files: files);
    }

    /// <summary>
    /// 只派生**版本清单**（不枚举包内文件）——
    /// 校验器用它把包内清单与当前仓库事实源重新对一遍。
    /// </summary>
    public static ReleaseVersionTable CreateVersions(string repoRoot, IHardwareTarget target, string editorVersion)
    {
        var runtimeDir = FirmwareRuntimeDir(repoRoot);
        var runtimeHeader = File.ReadAllText(Path.Combine(runtimeDir, "hmi_runtime.h"));
        var apiHeader = File.ReadAllText(Path.Combine(runtimeDir, "hmi_ui_api.h"));

        var firmwareRuntime = FirmwareVersionMarkers.ParseRuntimeHeader(runtimeHeader);
        var firmwareApi = FirmwareVersionMarkers.ParseApiHeader(apiHeader);

        return new ReleaseVersionTable(
            Editor: editorVersion,
            EditorTargetId: target.Metadata.Id,
            EditorAbiVersion: target.LvglConfig.AbiVersion,
            EditorLvglVersion: target.LvglConfig.LvglVersion,
            Firmware: firmwareRuntime.VersionString,
            FirmwareTargetId: firmwareRuntime.TargetId,
            FirmwareAbiVersion: firmwareApi.AbiVersion,
            FirmwareLvglVersion: firmwareApi.LvglVersion);
    }

    /// <summary>
    /// 枚举包内**载荷**文件（相对路径、字典序）—— 排除清单自身与校验和文件：
    /// 它们由装配步骤后写，且 <c>SHA256SUMS</c> 覆盖清单，反向包含会自指。
    /// </summary>
    public static IReadOnlyList<ReleaseFile> EnumeratePayloadFiles(string stageDir)
    {
        var result = new List<ReleaseFile>();
        foreach (var full in Directory.EnumerateFiles(stageDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(stageDir, full).Replace('\\', '/');
            if (rel is ChecksumFileName or ManifestFileName) continue;
            result.Add(new ReleaseFile(rel, new FileInfo(full).Length, Sha256File(full)));
        }
        result.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return result;
    }

    /// <summary>发布包清单文件名（校验和与验证器共用同一常量）。</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>发布包校验和文件名（<c>sha256sum -c</c> 兼容格式）。</summary>
    public const string ChecksumFileName = "SHA256SUMS";

    /// <summary>文件 sha256（小写十六进制）。</summary>
    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
