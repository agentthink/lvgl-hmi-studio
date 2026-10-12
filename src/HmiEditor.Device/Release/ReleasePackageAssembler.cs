using System.Text;
using Hmi.Hardware.Contracts;

namespace HmiEditor.Device.Release;

/// <summary>装配输入：编辑器产物已由调用方写入 <see cref="StageDir"/>（发布/打包步骤在 CLI 层）。</summary>
public sealed record ReleaseAssemblyOptions(
    string RepoRoot,
    string StageDir,
    IHardwareTarget Target,
    string EditorVersion,
    string EditorGitCommit,
    string Configuration,
    string Runtime,
    DateTime? CreatedUtc = null);

/// <summary>装配结果：清单 + 自检问题（问题为空才算装配成功）。</summary>
public sealed record ReleaseAssemblyResult(
    string StageDir,
    ReleaseManifest Manifest,
    IReadOnlyList<string> Problems)
{
    public bool Ok => Problems.Count == 0;
}

/// <summary>发布包自检结果。</summary>
public sealed record ReleaseVerification(IReadOnlyList<string> Problems)
{
    public bool Ok => Problems.Count == 0;
}

/// <summary>
/// 编辑器发布包装配器（登记表 §4 第 29 项"发布包 / 升级回滚"的编辑器侧）。
///
/// <para>做什么：往已发布的包目录里补<b>用户文档</b>与<b>升级/回滚说明</b>，
/// 然后写 <c>manifest.json</c>（版本清单 + 逐文件 sha256）与 <c>SHA256SUMS</c>
/// （<c>sha256sum -c</c> 兼容）。装配完立刻自检一遍 —— 与下载方拿到的东西
/// 走的是<b>同一条</b>校验路径，避免"生成时算一套、校验时算另一套"。</para>
///
/// <para>不做什么：不跑 <c>dotnet publish</c>、不构建交叉工具包 —— 那些是外部进程，
/// 留在 CLI（<c>tools/ReleasePackager</c>）里，好让这一层可以纯文件测试。</para>
/// </summary>
public static class ReleasePackageAssembler
{
    /// <summary>随包分发的固件文档（缺任何一份都判失败：说明里的链接不允许悬空）。</summary>
    public static readonly IReadOnlyList<string> FirmwareDocs = new[]
    {
        "flashing-guide.md",
        "wavepi-s6-flash-guide.md",
        "version-info.md",
        "touch-hardware-diagnosis.md"
    };

    /// <summary>编辑器产物目录名（CLI 的 publish 输出落点，也是自检的必检项）。</summary>
    public const string EditorDirName = "editor";

    /// <summary>随包固件文档目录名。</summary>
    public const string DocsDirName = "docs";

    /// <summary>升级/回滚说明文件名。</summary>
    public const string UpgradeDocName = "UPGRADE.md";

    public static ReleaseAssemblyResult Assemble(ReleaseAssemblyOptions options)
    {
        var problems = new List<string>();
        var stage = Path.GetFullPath(options.StageDir);
        if (!Directory.Exists(stage))
            throw new DirectoryNotFoundException($"包目录不存在: {stage}");

        var editorDir = Path.Combine(stage, EditorDirName);
        if (!Directory.Exists(editorDir) || !Directory.EnumerateFileSystemEntries(editorDir).Any())
            problems.Add($"未找到编辑器产物目录 {EditorDirName}/（先跑 dotnet publish）");

        CopyFirmwareDocs(options.RepoRoot, stage, problems);

        // 说明写在清单之前 —— 它自己也是包内载荷，需要出现在 Files 与 SHA256SUMS 里
        var versions = ReleaseManifestFactory.CreateVersions(
            options.RepoRoot, options.Target, options.EditorVersion);
        File.WriteAllText(
            Path.Combine(stage, UpgradeDocName),
            ReleaseNotes.Render(versions, options.Configuration, options.Runtime, options.EditorGitCommit),
            new UTF8Encoding(false));

        var manifest = ReleaseManifestFactory.Create(new ReleaseManifestOptions(
            RepoRoot: options.RepoRoot,
            StageDir: stage,
            Target: options.Target,
            EditorVersion: options.EditorVersion,
            EditorGitCommit: options.EditorGitCommit,
            Configuration: options.Configuration,
            Runtime: options.Runtime,
            CreatedUtc: options.CreatedUtc));
        ReleaseManifestJson.Write(manifest, Path.Combine(stage, ReleaseManifestFactory.ManifestFileName));
        WriteChecksums(stage);

        // 装配即自检：走与下游同一个验证器
        problems.AddRange(ReleasePackageVerifier.Verify(stage, options.RepoRoot, options.Target).Problems);
        return new ReleaseAssemblyResult(stage, manifest, problems);
    }

    private static void CopyFirmwareDocs(string repoRoot, string stage, List<string> problems)
    {
        var sourceDir = Path.Combine(repoRoot, "firmware", "t113", "docs");
        var destDir = Path.Combine(stage, DocsDirName);
        Directory.CreateDirectory(destDir);
        foreach (var name in FirmwareDocs)
        {
            var source = Path.Combine(sourceDir, name);
            if (!File.Exists(source))
            {
                problems.Add($"随包固件文档缺失: firmware/t113/docs/{name}");
                continue;
            }
            File.Copy(source, Path.Combine(destDir, name), overwrite: true);
        }
    }

    /// <summary>写 <c>SHA256SUMS</c>：覆盖包内除它自己以外的全部文件（含 manifest.json）。</summary>
    public static void WriteChecksums(string stageDir)
    {
        var stage = Path.GetFullPath(stageDir);
        var lines = new List<string>();
        foreach (var full in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(stage, full).Replace('\\', '/');
            if (rel == ReleaseManifestFactory.ChecksumFileName) continue;
            lines.Add($"{ReleaseManifestFactory.Sha256File(full)}  {rel}");
        }
        lines.Sort(StringComparer.Ordinal);
        File.WriteAllText(
            Path.Combine(stage, ReleaseManifestFactory.ChecksumFileName),
            string.Join('\n', lines) + "\n",
            new UTF8Encoding(false));
    }
}

/// <summary>发布包自检器：清单/校验和/版本对应三者都对得上才算通过。</summary>
public static class ReleasePackageVerifier
{
    /// <summary>
    /// 校验一个已装配的包目录。
    /// <paramref name="repoRoot"/> 与 <paramref name="target"/> 给出时会**重新派生**版本清单并要求与
    /// 包内清单逐字相同 —— 用来发现"包是用另一份源码状态装出来的"（不传则跳过这一层）。
    /// </summary>
    public static ReleaseVerification Verify(string stageDir, string? repoRoot = null, IHardwareTarget? target = null)
    {
        var problems = new List<string>();
        var stage = Path.GetFullPath(stageDir);
        var manifestPath = Path.Combine(stage, ReleaseManifestFactory.ManifestFileName);
        var checksumPath = Path.Combine(stage, ReleaseManifestFactory.ChecksumFileName);

        if (!File.Exists(manifestPath)) problems.Add($"缺少 {ReleaseManifestFactory.ManifestFileName}");
        if (!File.Exists(checksumPath)) problems.Add($"缺少 {ReleaseManifestFactory.ChecksumFileName}");

        var editorDir = Path.Combine(stage, ReleasePackageAssembler.EditorDirName);
        if (!Directory.Exists(editorDir) || !Directory.EnumerateFileSystemEntries(editorDir).Any())
            problems.Add($"缺少 {ReleasePackageAssembler.EditorDirName}/ 产物");

        ReleaseManifest? manifest = null;
        if (File.Exists(manifestPath))
        {
            try
            {
                manifest = ReleaseManifestJson.Read(manifestPath);
            }
            catch (Exception ex)
            {
                problems.Add($"{ReleaseManifestFactory.ManifestFileName} 解析失败: {ex.Message}");
            }
        }

        if (manifest is not null)
        {
            if (manifest.Format != ReleaseManifestFactory.FormatId)
                problems.Add($"清单格式不认得: {manifest.Format}（期望 {ReleaseManifestFactory.FormatId}）");

            foreach (var file in manifest.Files)
                VerifyOne(stage, file.Path, file.SizeBytes, file.Sha256, problems);

            // 清单覆盖的载荷集合 == 磁盘上的载荷集合（防"漏登记一个文件"）。
            // 两侧都排除 manifest.json / SHA256SUMS：它们由装配步骤后写，且 SHA256SUMS
            // 反向覆盖 manifest.json —— 对"清单是否漏登记"这一问题它们不是载荷。
            var expected = manifest.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
            foreach (var actual in ListPayloadPaths(stage))
                if (!expected.Contains(actual))
                    problems.Add($"清单未登记包内文件: {actual}");

            if (repoRoot is not null && target is not null)
                VerifyVersions(manifest, repoRoot, target, problems);
        }

        VerifyChecksumFile(stage, problems);
        return new ReleaseVerification(problems);
    }

    private static void VerifyVersions(
        ReleaseManifest manifest, string repoRoot, IHardwareTarget target, List<string> problems)
    {
        var expected = ReleaseManifestFactory.CreateVersions(repoRoot, target, manifest.Versions.Editor);
        if (expected != manifest.Versions)
        {
            problems.Add(
                "版本清单与当前仓库事实源不一致（包可能是另一份源码状态装出来的）: " +
                $"包内={manifest.Versions} 重新派生={expected}");
        }
    }

    /// <param name="expectedSize">清单里的字节数；<c>null</c> 表示只有 sha256（校验和文件的行）。</param>
    private static void VerifyOne(string stage, string rel, long? expectedSize, string sha, List<string> problems)
    {
        var full = Path.Combine(stage, rel.Replace('/', Path.DirectorySeparatorChar));
        var info = new FileInfo(full);
        if (!info.Exists)
        {
            problems.Add($"登记的文件不存在: {rel}");
            return;
        }
        if (expectedSize is { } size && info.Length != size)
            problems.Add($"文件大小不符: {rel}（磁盘 {info.Length} / 清单 {size}）");
        var actual = ReleaseManifestFactory.Sha256File(full);
        if (!string.Equals(actual, sha, StringComparison.OrdinalIgnoreCase))
            problems.Add($"sha256 不符: {rel}");
    }

    private static void VerifyChecksumFile(string stage, List<string> problems)
    {
        var checksumPath = Path.Combine(stage, ReleaseManifestFactory.ChecksumFileName);
        if (!File.Exists(checksumPath)) return;

        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(checksumPath))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || parts[0].Length != 64)
            {
                problems.Add($"{ReleaseManifestFactory.ChecksumFileName} 行格式不合法: {raw}");
                continue;
            }
            var rel = parts[1].TrimStart('*').Trim();
            listed.Add(rel);
            VerifyOne(stage, rel, expectedSize: null, parts[0], problems);
        }

        // 校验和覆盖 manifest.json（它也是载荷），但不覆盖它自己
        foreach (var actual in ListPackagePaths(stage, ReleaseManifestFactory.ChecksumFileName))
            if (!listed.Contains(actual))
                problems.Add($"{ReleaseManifestFactory.ChecksumFileName} 未覆盖包内文件: {actual}");
    }

    /// <summary>清单登记的载荷：除 <c>manifest.json</c> / <c>SHA256SUMS</c> 之外的全部包内文件。</summary>
    private static IEnumerable<string> ListPayloadPaths(string stage) =>
        ListPackagePaths(stage, ReleaseManifestFactory.ManifestFileName, ReleaseManifestFactory.ChecksumFileName);

    /// <summary>全部包内文件（相对 POSIX 路径），排除给定文件名。</summary>
    private static IEnumerable<string> ListPackagePaths(string stage, params string[] excluded)
    {
        var skip = new HashSet<string>(excluded, StringComparer.Ordinal);
        foreach (var full in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(stage, full).Replace('\\', '/');
            if (skip.Contains(rel)) continue;
            yield return rel;
        }
    }
}

/// <summary>包内 <c>UPGRADE.md</c> 的渲染（升级 / 回滚说明）。</summary>
public static class ReleaseNotes
{
    public static string Render(ReleaseVersionTable v, string configuration, string runtime, string gitCommit)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# zhishu-HMI 编辑器发布包 —— 安装 / 升级 / 回滚");
        sb.AppendLine();
        sb.AppendLine("> 本文件由打包脚本生成，内容只描述**包内实际存在**的路径与设备侧既有的加载顺序。");
        sb.AppendLine();
        sb.AppendLine("## 版本清单");
        sb.AppendLine();
        sb.AppendLine("| 项 | 值 |");
        sb.AppendLine("| --- | --- |");
        sb.AppendLine($"| 编辑器 | `{v.Editor}`（git `{gitCommit}` / {configuration} / {runtime}） |");
        sb.AppendLine($"| 编辑器硬件目标 | `{v.EditorTargetId}`，声明 ABI `{v.EditorAbiVersion}`、LVGL `{Fmt(v.EditorLvglVersion)}` |");
        sb.AppendLine($"| 固件 | `{v.Firmware}`（`{v.FirmwareTargetId}`），ABI `{v.FirmwareAbiVersion}`、LVGL `{Fmt(v.FirmwareLvglVersion)}` |");
        sb.AppendLine();
        sb.AppendLine("固件侧的四个值直接读自 `firmware/t113/app/runtime/hmi_runtime.h` 与 `hmi_ui_api.h`；");
        sb.AppendLine("编辑器侧读自硬件目标插件。两侧不一致时**不要**混用本包的编辑器与旧固件 ——");
        sb.AppendLine("`.so` 的 ABI / LVGL 版本不匹配时设备端 `dlopen` 会直接拒绝加载。");
        sb.AppendLine();
        sb.AppendLine("## 完整性校验");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("cd <解包后的包根>");
        sb.AppendLine("sha256sum -c SHA256SUMS");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("逐文件清单（路径 / 字节数 / sha256）另见 `manifest.json`。");
        sb.AppendLine();
        sb.AppendLine("## 安装");
        sb.AppendLine();
        sb.AppendLine("- 依赖 .NET **10** 运行时（框架依赖发布，包内不含运行时）。");
        sb.AppendLine("- Linux 下启动：`./editor/HmiEditor.App`；Windows 下启动 `editor/HmiEditor.App.exe`。");
        sb.AppendLine("- 包内 `docs/` 是随包的固件文档（烧录指南 / 版本信息 / 触摸诊断）。");
        sb.AppendLine();
        sb.AppendLine("## 升级（编辑器）");
        sb.AppendLine();
        sb.AppendLine("1. 校验新包（上一节）。");
        sb.AppendLine("2. 把旧包目录整体改名保留（例如 `zhishu-hmi-editor-<旧版本>`），不要就地覆盖。");
        sb.AppendLine("3. 解包新包，用新目录里的 `editor/` 启动。");
        sb.AppendLine();
        sb.AppendLine("工程文件（`*.hmi`）与发布包相互独立：升级不迁移、不覆盖用户工程。");
        sb.AppendLine();
        sb.AppendLine("## 回滚（编辑器）");
        sb.AppendLine();
        sb.AppendLine("发布包是**按目录自包含**的：回滚 = 用旧目录里的 `editor/` 启动，逐字节回退，");
        sb.AppendLine("不需要卸载/安装动作。这就是升级时要求「保留旧目录」的原因。");
        sb.AppendLine();
        sb.AppendLine("## 设备侧：UI 工程热更新与回滚");
        sb.AppendLine();
        sb.AppendLine("`libhmi_ui.so` 走的是设备端既有的**两级加载**（`firmware/t113/app/runtime/screen_manager.c`）：");
        sb.AppendLine();
        sb.AppendLine("1. `/mnt/UDISK/hmi/libhmi_ui.so` —— OTA / IDE 部署位置；");
        sb.AppendLine("2. `/usr/lib/hmi/libhmi_ui.so` —— 出厂回退位置。");
        sb.AppendLine();
        sb.AppendLine("- **升级**：把新 `libhmi_ui.so` 部署到 1，重启 `hmi_runtime`。");
        sb.AppendLine("- **回滚**：删除或替换 1 处的文件，重启后运行时自动回退到出厂的 2。");
        sb.AppendLine();
        sb.AppendLine("## 设备侧：整机固件");
        sb.AppendLine();
        sb.AppendLine("整机固件**没有** A/B 分区或整包 OTA 回滚 —— 回滚 = 重刷上一版整包镜像，");
        sb.AppendLine("步骤见包内 `docs/flashing-guide.md`，版本对应见 `docs/version-info.md`。");
        sb.AppendLine();
        sb.AppendLine("## 交叉编译（可选）");
        sb.AppendLine();
        sb.AppendLine("若包内含 `t113-cross-kit/`，在编辑器「设置 → 真机部署 (T113) → Tina SDK / 工具包目录」");
        sb.AppendLine("填入该目录即可交叉编译 `libhmi_ui.so`（无需完整 Tina SDK）；留空则自动探测");
        sb.AppendLine("`ZHSHU_HMI_SDK` 与仓库内 `firmware/Tina-SDK`、`firmware/Tina-Linux`。");
        return sb.ToString();
    }

    private static string Fmt(string version) =>
        string.IsNullOrEmpty(version) ? "（未声明）" : version;
}
