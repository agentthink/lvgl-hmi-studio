using System.IO;
using System.IO.Compression;
using System.Text.Json;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Serialization;

/// <summary>
/// .hmi 工程包元数据（manifest.json）。
/// </summary>
public sealed class HmiPackageManifest
{
    /// <summary>包格式版本（当前 1）。加载时不匹配则拒绝。</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>创建此包的编辑器版本。</summary>
    public string? EditorVersion { get; set; }

    public string ProjectName { get; set; } = "";

    public string ProjectVersion { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>包内条目总数（不含目录）。</summary>
    public int EntryCount { get; set; }

    /// <summary>资产文件数量。</summary>
    public int AssetCount { get; set; }

    /// <summary>是否包含图库数据。</summary>
    public bool HasResourceLibrary { get; set; }

    /// <summary>
    /// 打包时**磁盘上找不到文件**、因而没有进包的资产名（阶段 121）。
    ///
    /// <para>这些资产仍写在 <c>assets.json</c> 里（不丢名字/分类等元数据），但包内没有对应二进制。
    /// 旧行为是打包时静默 <c>continue</c> —— 于是重新打开的工程里出现一堆指向不存在文件的引用，
    /// 而用户看不到任何提示。现在把名字记进清单，`Unpack` 经
    /// <see cref="HmiPackageResult.MissingAssets"/> 报给调用方。</para>
    /// </summary>
    public List<string> MissingAssets { get; set; } = new();
}

/// <summary>
/// 打包输入 —— 需要打包的工程数据及资产文件解析回调。
/// </summary>
public sealed record HmiPackageContent(
    HmiProject Project,
    AssetLibrary AssetLibrary,
    HmiResourceLibrary? ResourceLibrary,
    /// <summary>解析 AssetItem 对应磁盘文件路径；返回 null 表示文件缺失（打包时跳过该资产）。</summary>
    Func<AssetItem, string?> AssetFileResolver);

/// <summary>
/// 解包结果 —— 工程数据 + 资产文件落盘后的资源库。
/// </summary>
/// <param name="MissingAssets">
/// 包清单里列着、但包内没有对应二进制的资产名（打包时磁盘上就缺文件，或包被裁剪过）。
/// 为 null / 空表示没有缺失。调用方应当**提示**用户，而不是让工程静默带着断引用打开。
/// </param>
public sealed record HmiPackageResult(
    HmiProject Project,
    AssetLibrary AssetLibrary,
    HmiResourceLibrary? ResourceLibrary,
    IReadOnlyList<string> AssetFilePaths,
    IReadOnlyList<string>? MissingAssets = null);

/// <summary>
/// .hmi 工程包服务 —— 将工程配置、资产文件与图库数据打包为 ZIP。
///
/// 包内结构（ZIP）：
///   manifest.json  包元数据（格式版本/工程信息/清单）
///   project.json   HmiProject 序列化（屏幕/变量/脚本/报警/趋势/配方模板等）
///   assets.json    AssetLibrary 序列化（资产清单，FilePath 为包内相对路径 assets/&lt;id&gt;.&lt;ext&gt;）
///   assets/        资产二进制文件（图片/SVG/字体）
///   reslib.json    项目图库数据（HmiResourceLibrary，可选）
/// </summary>
public static class HmiPackageService
{
    public const int CurrentFormatVersion = 1;

    private const string ManifestEntry = "manifest.json";
    private const string ProjectEntry = "project.json";
    private const string AssetsEntry = "assets.json";
    private const string ReslibEntry = "reslib.json";
    private const string AssetFolder = "assets/";

    private static readonly JsonSerializerOptions Options = JsonOptionsDefaults.Default;

    /// <summary>将工程打包为 .hmi（ZIP）写入流。</summary>
    public static void Pack(Stream output, HmiPackageContent content, string? editorVersion = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        Pack(output, ProjectSerializer.Serialize(content.Project), content.AssetLibrary,
            content.ResourceLibrary, content.AssetFileResolver, editorVersion);
    }

    /// <summary>
    /// 将工程打包为 .hmi（ZIP）写入流（使用调用方预先序列化的 projectJson）。
    /// 适用于需要在固定时间点快照工程数据（如自动保存）的场景，避免后台线程读取到不一致状态。
    /// </summary>
    public static void Pack(
        Stream output,
        string projectJson,
        AssetLibrary assetLibrary,
        HmiResourceLibrary? resourceLibrary,
        Func<AssetItem, string?>? assetFileResolver,
        string? editorVersion = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(projectJson);
        ArgumentNullException.ThrowIfNull(assetLibrary);

        assetFileResolver ??= static item => item.FilePath;
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        // 1. assets.json（FilePath 统一改写为包内相对路径）
        var packedAssets = BuildPackedAssetLibrary(assetLibrary);
        WriteJsonEntry(zip, AssetsEntry, packedAssets);

        // 2. 资产文件
        var assetCount = 0;
        var missingAssets = new List<string>();
        foreach (var item in assetLibrary.Assets)
        {
            var sourcePath = assetFileResolver(item);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                // ⚠ 不静默跳过：条目仍在 assets.json 里（元数据不丢），但**记名**进清单，
                //    由 Unpack 经 HmiPackageResult.MissingAssets 报给调用方（阶段 121）。
                missingAssets.Add(string.IsNullOrWhiteSpace(item.Name)
                    ? GetAssetEntryFileName(item) : item.Name);
                continue;
            }

            var entryName = AssetFolder + GetAssetEntryFileName(item);
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var dest = entry.Open())
            using (var src = File.OpenRead(sourcePath))
            {
                src.CopyTo(dest);
            }
            assetCount++;
        }

        // 3. project.json（使用调用方快照）
        WriteTextEntry(zip, ProjectEntry, projectJson);

        // 4. reslib.json（可选，写入原始 JSON 文本，避免双重序列化）
        var hasReslib = resourceLibrary is { CustomResources.Count: > 0 };
        if (hasReslib)
        {
            var entry = zip.CreateEntry(ReslibEntry, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(resourceLibrary!.Serialize());
        }

        // 5. manifest.json（最后写入，包含清单统计）
        var (projectName, projectVersion) = ExtractProjectMeta(projectJson);
        var manifest = new HmiPackageManifest
        {
            FormatVersion = CurrentFormatVersion,
            EditorVersion = editorVersion,
            ProjectName = projectName,
            ProjectVersion = projectVersion,
            CreatedAt = DateTimeOffset.Now,
            EntryCount = 4 + assetCount,
            AssetCount = assetCount,
            HasResourceLibrary = hasReslib,
            MissingAssets = missingAssets
        };
        WriteJsonEntry(zip, ManifestEntry, manifest);
    }

    /// <summary>轻量解析 projectJson（camelCase），提取工程名与版本（用于 manifest）。</summary>
    private static (string Name, string Version) ExtractProjectMeta(string projectJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(projectJson);
            var root = doc.RootElement;
            var name = root.TryGetProperty("name", out var n)
                ? n.GetString() ?? ""
                : root.TryGetProperty("Name", out var n2) ? n2.GetString() ?? "" : "";
            var version = root.TryGetProperty("projectVersion", out var v)
                ? v.GetString() ?? ""
                : root.TryGetProperty("ProjectVersion", out var v2) ? v2.GetString() ?? "" : "";
            return (name, version);
        }
        catch
        {
            return ("", "");
        }
    }

    /// <summary>
    /// 解包 .hmi（ZIP）。资产文件写入 targetAssetDirectory，并更新 AssetLibrary 中
    /// FilePath 为该目录内的绝对路径。
    ///
    /// <para><b>落点契约：</b><paramref name="targetAssetDirectory"/> 由<b>本方法</b>在**第一次真正
    /// 写盘之前**自建（<c>Directory.CreateDirectory(assetDir)</c> 在全部前置校验之后）——
    /// 调用方只需算出路径，**不要**提前把它建出来。包是坏的时本方法抛异常，
    /// 磁盘上**不留任何东西**；提前建的话那个目录一个文件都收不到、也没人再引用它，
    /// 而会话目录的兜底释放在进程退出 ⇒ 反复打开坏包会攒一串空目录（阶段 80 实测：5 次失败 = 5 个）。
    /// 判据：<c>HmiPackageServiceTests.Unpack_BrokenPackage_LeavesNoTargetDirectoryBehind</c>。</para>
    ///
    /// <para><b>失败契约：</b>**全部**失败（前置校验 ＋ 中途写盘）**一律**抛
    /// <see cref="HmiPackageException"/> —— 它带「诊断码 + 实参 + 中文回落原文」三件东西
    /// （<see cref="HmiPackageIssueCodes"/>），App 层据此渲染字典文案。
    /// **界面上不该出现 .NET 的英文异常原文**：非 ZIP / 坏 JSON / 资产提取失败 / 图库坏 JSON
    /// 几档的异常本来是 BCL / System.Text.Json 抛的，都在这里被包成码
    /// （<c>OpenZip</c> / <c>ReadJsonEntry</c> / 本方法的 <c>catch</c> 各自一层）。
    /// 判据：<c>PackageIssueLocalizationGuardTests</c>。</para>
    ///
    /// <para><b>中途失败的契约（阶段 105）：</b><c>Directory.CreateDirectory(assetDir)</c>
    /// **之后**的失败同样码化，**并且回滚本方法自己建出来的目录** ⇒
    /// 「失败的解包在磁盘上不留任何还要人收拾的东西」这条不变量对**全部**失败档成立，
    /// 不再只对前置校验那几条成立（阶段 80 §8.6 / §38.10 第 2 条留下的缺口）。
    /// 判据：<c>HmiPackageServiceTests.Unpack_MidFailure_LeavesNoTargetDirectoryBehind</c>。</para>
    ///
    /// <para>⚠ **刻意不码化**的那条路径见 <see cref="HmiPackageIssueCodes"/> 的类注释：
    /// 参数为空白（编程错误，仍是 <c>ArgumentException</c>）。</para>
    /// </summary>
    public static HmiPackageResult Unpack(Stream input, string targetAssetDirectory)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(targetAssetDirectory))
            throw new ArgumentException("资产目录不能为空", nameof(targetAssetDirectory));

        using var zip = OpenZip(input);

        var manifest = ReadJsonEntry<HmiPackageManifest>(
                           zip, ManifestEntry,
                           HmiPackageIssueCodes.MANIFEST_INVALID,
                           "manifest.json 不是有效的 JSON（工程包已损坏）")
                       ?? throw new HmiPackageException(
                           HmiPackageIssueCodes.MANIFEST_MISSING,
                           "工程包里缺少 manifest.json，不是有效的 .hmi 工程包");
        if (manifest.FormatVersion != CurrentFormatVersion)
        {
            throw new HmiPackageException(
                HmiPackageIssueCodes.FORMAT_VERSION_UNSUPPORTED,
                $"不支持的工程包格式版本 {manifest.FormatVersion}（当前支持 {CurrentFormatVersion}）",
                manifest.FormatVersion, CurrentFormatVersion);
        }

        var projectJson = ReadEntryText(zip, ProjectEntry)
                          ?? throw new HmiPackageException(
                              HmiPackageIssueCodes.PROJECT_MISSING,
                              "工程包里缺少 project.json（工程包已损坏）");
        var project = ProjectSerializer.Deserialize(projectJson)
                      ?? throw new HmiPackageException(
                          HmiPackageIssueCodes.PROJECT_INVALID,
                          "project.json 不是有效的工程数据（工程包已损坏）");

        // 资产清单（FilePath 为包内相对路径 assets/<id>.<ext>）
        var assets = ReadJsonEntry<AssetLibrary>(
                         zip, AssetsEntry,
                         HmiPackageIssueCodes.ASSETS_INVALID,
                         "assets.json 不是有效的 JSON（工程包已损坏）")
                     ?? new AssetLibrary();

        // 解压资产文件并改写 FilePath 为落盘绝对路径。
        //
        // 落点契约的另一半（阶段 105）：**只删自己建出来的目录** —— 先把"建之前存不存在"
        // 记下来，失败时才知道该回滚什么（调用方**已经**建好的目标目录一律不动，
        // 那是它自己的东西；tools/SimExport 就是先建再传进来的）。
        var assetPaths = new List<string>();
        // 缺失资产 = 打包时磁盘上就缺的（清单里记着）+ 清单列了但包里没有的（包被裁剪/损坏）。
        // 两侧都去重：同一个资产同时命中两条来源时只报一次（阶段 121 实测会重复）。
        var missingAssets = new List<string>();
        var missingSeen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in manifest.MissingAssets ?? new List<string>())
        {
            if (missingSeen.Add(m)) missingAssets.Add(m);
        }
        var assetDir = Path.Combine(targetAssetDirectory, "assets");
        var targetExistedBefore = Directory.Exists(targetAssetDirectory);
        var assetDirExistedBefore = Directory.Exists(assetDir);
        Directory.CreateDirectory(assetDir);

        var currentAsset = "";
        try
        {
            foreach (var item in assets.Assets)
            {
                if (string.IsNullOrWhiteSpace(item.FilePath)) continue;

                var entryName = NormalizeEntryName(item.FilePath);
                var entry = zip.GetEntry(entryName);
                if (entry == null)
                {
                    // 旧行为是静默 continue ⇒ 断引用。这里记名（阶段 121）。
                    var label = string.IsNullOrWhiteSpace(item.Name) ? entryName : item.Name;
                    if (missingSeen.Add(label)) missingAssets.Add(label);
                    continue;
                }

                var fileName = Path.GetFileName(entryName);
                // 报**包内条目名**而不是文件名：条目名以 `/` 结尾时（资产指向目录条目）
                // Path.GetFileName 是空串，文案里会留下一个「资产文件「」」的空壳。
                currentAsset = entryName;
                var destPath = Path.Combine(assetDir, fileName);
                entry.ExtractToFile(destPath, overwrite: true);

                item.FilePath = destPath;
                assetPaths.Add(destPath);
            }

            // 图库数据（可选，使用自定义 Serialize/Deserialize 格式）。
            // **只有这一支解析 JSON**（资产那一支是纯字节拷贝）⇒ JsonException 只可能来自它，
            // 所以这一层 catch 里"类型即码"是安全的，不必再去嗅消息。
            HmiResourceLibrary? reslib = null;
            if (ReadEntryText(zip, ReslibEntry) is { } reslibJson)
            {
                try
                {
                    reslib = new HmiResourceLibrary();
                    reslib.Deserialize(reslibJson);
                }
                catch (JsonException ex)
                {
                    throw new HmiPackageException(
                        HmiPackageIssueCodes.RESLIB_INVALID,
                        "图库数据不是有效的 JSON（工程包已损坏）", ex);
                }
            }

            return new HmiPackageResult(project, assets, reslib, assetPaths, missingAssets);
        }
        catch (Exception ex)
        {
            // 回滚**无条件**走（两条中途失败都要清），码化只在"还没码化"的那一支做。
            RollbackCreatedDirectories(assetDir, targetAssetDirectory,
                assetDirExistedBefore, targetExistedBefore);

            if (ex is HmiPackageException) throw;

            throw new HmiPackageException(
                HmiPackageIssueCodes.ASSET_EXTRACT_FAILED,
                $"资产文件「{currentAsset}」提取失败（工程包已损坏或磁盘不可写）", ex, currentAsset);
        }
    }

    /// <summary>
    /// 中途失败的回滚：**只删本方法自己建出来的**目录（阶段 105）。
    ///
    /// <para>顺序是先子后父（删了父目录，子目录就无从谈起了）。删不掉就留着 ——
    /// 这里**刻意吞掉**删除异常：回滚是尽力而为，**不能**把"解包失败"这个真实原因
    /// 替换成"回滚失败"（用户要看到的是包坏了，不是清理不干净）。</para>
    /// </summary>
    private static void RollbackCreatedDirectories(
        string assetDir, string targetAssetDirectory, bool assetDirExisted, bool targetExisted)
    {
        try
        {
            if (!assetDirExisted && Directory.Exists(assetDir))
                Directory.Delete(assetDir, recursive: true);
            if (!targetExisted && Directory.Exists(targetAssetDirectory))
                Directory.Delete(targetAssetDirectory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ── 内部辅助 ──

    /// <summary>生成资产清单副本，FilePath 改写为包内相对路径。</summary>
    private static AssetLibrary BuildPackedAssetLibrary(AssetLibrary source)
    {
        var copy = new AssetLibrary();
        foreach (var item in source.Assets)
        {
            copy.AddAsset(new AssetItem
            {
                Id = item.Id,
                Name = item.Name,
                Type = item.Type,
                FilePath = AssetFolder + GetAssetEntryFileName(item),
                SizeBytes = item.SizeBytes,
                Width = item.Width,
                Height = item.Height,
                ColorDepth = item.ColorDepth,
                ReferenceCount = item.ReferenceCount,
                ImportedAt = item.ImportedAt
            });
        }
        return copy;
    }

    /// <summary>生成包内资产文件名：&lt;id&gt;.&lt;ext&gt;。</summary>
    private static string GetAssetEntryFileName(AssetItem item)
    {
        var ext = string.IsNullOrWhiteSpace(item.FilePath)
            ? null
            : Path.GetExtension(item.FilePath).TrimStart('.');

        ext ??= item.Type switch
        {
            AssetType.Image => "png",
            AssetType.Svg => "svg",
            AssetType.Font => "ttf",
            _ => "bin"
        };
        if (string.IsNullOrWhiteSpace(ext)) ext = "bin";

        return $"{item.Id}.{ext}";
    }

    /// <summary>规范化包内条目路径（统一正斜杠）。</summary>
    private static string NormalizeEntryName(string name) =>
        name.Replace('\\', '/').TrimStart('/');

    private static void WriteJsonEntry(ZipArchive zip, string entryName, object value)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, Options);
    }

    /// <summary>写入原始文本条目（避免字符串被二次 JSON 转义）。</summary>
    private static void WriteTextEntry(ZipArchive zip, string entryName, string text)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }

    /// <summary>
    /// 打开 ZIP 读取视图 —— <c>ZipArchive</c> 的构造失败**也要码化**。
    ///
    /// <para>①「非 ZIP」那一档的异常是 **BCL 抛的**
    /// （<c>InvalidDataException</c> + <c>End of Central Directory record could not be found.</c>），
    /// 不是我们抛的 —— 不包这一层，它就绕过码化路径、把英文原文直接送到状态栏
    /// （这正是登记 #59 的头号样本）。</para>
    /// </summary>
    private static ZipArchive OpenZip(Stream input)
    {
        try
        {
            return new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            throw new HmiPackageException(
                HmiPackageIssueCodes.ZIP_UNREADABLE,
                "工程包不是有效的 ZIP 文件（已损坏或不是 .hmi 包）");
        }
    }

    /// <summary>
    /// 读 JSON 条目。条目**不存在**返回 <c>null</c>（那意味着什么由调用方定：必填项缺 = 另一种码，
    /// 选填项缺 = 用默认值）；条目**在、内容坏 JSON** 则抛码化异常
    /// （<paramref name="invalidCode"/> + <paramref name="invalidFallback"/>）。
    ///
    /// <para>不加这一层的话，<c>System.Text.Json</c> 会把内部措辞连
    /// <c>LineNumber</c> / <c>BytePositionInLine</c> 一起抖到用户可见的状态栏上（实测样本见 #59）。</para>
    /// </summary>
    private static T? ReadJsonEntry<T>(
        ZipArchive zip, string entryName, string invalidCode, string invalidFallback) where T : class
    {
        var entry = zip.GetEntry(entryName);
        if (entry == null) return null;
        using var stream = entry.Open();
        try
        {
            return JsonSerializer.Deserialize<T>(stream, Options);
        }
        catch (JsonException)
        {
            throw new HmiPackageException(invalidCode, invalidFallback);
        }
    }

    private static string? ReadEntryText(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName);
        if (entry == null) return null;
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
