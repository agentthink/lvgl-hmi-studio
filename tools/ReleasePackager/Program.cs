using System.Diagnostics;
using System.Text.Json;
using Hmi.Hardware.Builtin;
using HmiEditor.Device.Release;

// ============================================================
// ReleasePackager —— 编辑器侧「一键出包」（登记表 §4 第 29 项）
//
// 做什么：publish 编辑器 → （可选）构建 T113 交叉编译工具包 → 装配用户文档 /
//         升级回滚说明 / manifest.json（版本清单）/ SHA256SUMS → （可选）打 tar.gz。
// 判据：包内版本清单由**事实源**派生（固件头文件 + 硬件目标插件），装配完立刻自检；
//       `--verify <dir>` 可对任意已装好的包重跑同一套校验。
//
// 用法：
//   dotnet run --project tools/ReleasePackager -- --out release
//   dotnet run --project tools/ReleasePackager -- --out release --with-kit
//   dotnet run --project tools/ReleasePackager -- --verify release/<包名>
//   dotnet run --project tools/ReleasePackager -- --self-check     （门禁用：不 publish 的最小装配+自检）
//
// 参数：
//   --out <dir>        输出根目录（默认 release/）
//   --runtime <rid>    .NET RID（默认 linux-x64；Windows 用 win-x64）
//   --configuration    构建配置（默认 Release）
//   --with-kit         同时构建 t113-cross-kit（需要 Tina SDK；失败则整包失败）
//   --no-archive       不打 tar.gz
//   --verify <dir>     只校验一个已装配的包目录，不重新打包
//   --self-check       在临时目录里装配一个最小包并跑同一套校验（CI 门禁防 CLI 接线腐烂）
// ============================================================

var outRoot = "release";
var runtime = "linux-x64";
var configuration = "Release";
var withKit = false;
var archive = true;
var verifyDir = "";
var selfCheck = false;

const string Usage = """
用法:
  release-packager [--out <dir>] [--runtime <rid>] [--configuration <cfg>]
                   [--with-kit] [--no-archive]
  release-packager --verify <已装配的包目录>
  release-packager --self-check
""";

var i = 0;
for (; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--out": outRoot = Next(); break;
        case "--runtime": runtime = Next(); break;
        case "--configuration": configuration = Next(); break;
        case "--with-kit": withKit = true; break;
        case "--no-archive": archive = false; break;
        case "--verify": verifyDir = Next(); break;
        case "--self-check": selfCheck = true; break;
        case "-h" or "--help":
            Console.WriteLine(Usage);
            return 0;
        default:
            Console.Error.WriteLine($"未知参数: {args[i]}");
            Console.Error.WriteLine(Usage);
            return 2;
    }
}

string Next() => ++i < args.Length ? args[i] : throw new ArgumentException($"参数 {args[i - 1]} 缺少取值");

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
if (repoRoot is null)
{
    Console.Error.WriteLine("✗ 未找到仓库根（向上找不到 HmiEditor.slnx）");
    return 1;
}

// ── 自检模式：不 publish、不碰仓库，装配一个最小包并跑同一套校验（门禁用，防 CLI 接线腐烂）──
if (selfCheck)
{
    var temp = Directory.CreateTempSubdirectory("zhishu-release-selfcheck-").FullName;
    try
    {
        var stage = Path.Combine(temp, "pkg");
        Directory.CreateDirectory(Path.Combine(stage, ReleasePackageAssembler.EditorDirName));
        File.WriteAllText(Path.Combine(stage, ReleasePackageAssembler.EditorDirName, "HmiEditor.App"), "self-check\n");
        var result = ReleasePackageAssembler.Assemble(new ReleaseAssemblyOptions(
            RepoRoot: repoRoot,
            StageDir: stage,
            Target: new AllwinnerT113Target(),
            EditorVersion: "0.0.0-selfcheck",
            EditorGitCommit: "selfcheck",
            Configuration: configuration,
            Runtime: runtime));
        foreach (var problem in result.Problems) Console.Error.WriteLine($"   ✗ {problem}");
        if (!result.Ok)
        {
            Console.Error.WriteLine("❌ 出包自检失败");
            return 1;
        }
        Console.WriteLine($"✅ 出包自检通过（{result.Manifest.FileCount} 个载荷文件，" +
                          $"固件 {result.Manifest.Versions.Firmware} / ABI {result.Manifest.Versions.FirmwareAbiVersion}）");
        return 0;
    }
    finally
    {
        Directory.Delete(temp, recursive: true);
    }
}

// ── 校验模式：对已装好的包重跑同一套判据 ──
if (!string.IsNullOrEmpty(verifyDir))
{
    var verification = ReleasePackageVerifier.Verify(
        Path.GetFullPath(verifyDir), repoRoot, new AllwinnerT113Target());
    if (verification.Ok)
    {
        Console.WriteLine($"✅ 发布包自检通过: {verifyDir}");
        return 0;
    }
    Console.Error.WriteLine($"❌ 发布包自检失败（{verification.Problems.Count} 项）: {verifyDir}");
    foreach (var problem in verification.Problems) Console.Error.WriteLine($"   - {problem}");
    return 1;
}

// ── 打包模式 ──
var editorVersion = ResolveEditorVersion(repoRoot);
var gitCommit = RunCapture("git", repoRoot, "rev-parse", "--short", "HEAD") ?? "unknown";
var packageName = $"zhishu-hmi-editor-{editorVersion}-{gitCommit}-{DateTime.Now:yyyyMMdd}";
var stageDir = Path.Combine(Path.GetFullPath(outRoot), packageName);
var editorDir = Path.Combine(stageDir, ReleasePackageAssembler.EditorDirName);

Console.WriteLine($"[包] {packageName}");
Console.WriteLine($"[仓库] {repoRoot}");
Console.WriteLine($"[输出] {stageDir}");

if (Directory.Exists(stageDir)) Directory.Delete(stageDir, recursive: true);
Directory.CreateDirectory(stageDir);

Console.WriteLine($"[1/4] dotnet publish ({configuration} / {runtime}) ...");
var appProject = Path.Combine(repoRoot, "src", "HmiEditor.App", "HmiEditor.App.csproj");
if (Run("dotnet", repoRoot, "publish", appProject, "-c", configuration,
        "-r", runtime, "--self-contained", "false", "-o", editorDir, "--nologo") != 0)
{
    Console.Error.WriteLine("✗ dotnet publish 失败");
    return 1;
}
if (!Directory.Exists(editorDir) || !Directory.EnumerateFileSystemEntries(editorDir).Any())
{
    Console.Error.WriteLine($"✗ 编辑器产物目录为空: {editorDir}");
    return 1;
}

if (withKit)
{
    Console.WriteLine("[2/4] 构建 t113-cross-kit（T113KitBuilder）...");
    var kitProject = Path.Combine(repoRoot, "tools", "T113KitBuilder", "T113KitBuilder.csproj");
    var kitOut = Path.Combine(stageDir, "t113-cross-kit");
    if (Run("dotnet", repoRoot, "run", "--project", kitProject, "-c", configuration,
            "--", "--out", kitOut) != 0)
    {
        Console.Error.WriteLine("✗ t113-cross-kit 构建失败（--with-kit 显式要求，整包失败）");
        return 1;
    }
}
else
{
    Console.WriteLine("[2/4] 跳过交叉工具包（未指定 --with-kit）");
}

Console.WriteLine("[3/4] 装配文档 / 清单 / 校验和 ...");
var assembly = ReleasePackageAssembler.Assemble(new ReleaseAssemblyOptions(
    RepoRoot: repoRoot,
    StageDir: stageDir,
    Target: new AllwinnerT113Target(),
    EditorVersion: editorVersion,
    EditorGitCommit: gitCommit,
    Configuration: configuration,
    Runtime: runtime));

foreach (var problem in assembly.Problems) Console.Error.WriteLine($"   ✗ {problem}");
if (!assembly.Ok)
{
    Console.Error.WriteLine("✗ 装配自检未通过，不产出压缩包");
    return 1;
}
var v = assembly.Manifest.Versions;
Console.WriteLine($"   版本清单: 编辑器 {v.Editor} (git {gitCommit}) / 固件 {v.Firmware} / " +
                  $"目标 {v.FirmwareTargetId} / ABI {v.FirmwareAbiVersion} / LVGL {v.FirmwareLvglVersion}");
Console.WriteLine($"   载荷 {assembly.Manifest.FileCount} 个文件 / {assembly.Manifest.TotalBytes / 1024} KB");

if (archive)
{
    Console.WriteLine("[4/4] 打包 tar.gz ...");
    var archivePath = Path.Combine(Path.GetFullPath(outRoot), packageName + ".tar.gz");
    if (Run("tar", Path.GetFullPath(outRoot), "-czf", archivePath, packageName) != 0)
    {
        Console.Error.WriteLine("✗ tar 打包失败（可加 --no-archive 跳过）");
        return 1;
    }
    Console.WriteLine($"✅ 完成: {archivePath}");
}
else
{
    Console.WriteLine("[4/4] 跳过压缩包（--no-archive）");
}

Console.WriteLine($"✅ 完成: {stageDir}");
return 0;

// ── 辅助 ──

static string? FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    for (var i = 0; i < 12 && dir is not null; i++, dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx")))
            return dir.FullName;
    return null;
}

/// <summary>编辑器版本：优先 MSBuild 的 InformationalVersion，回落 Version（不读已发布的 dll，
/// 这样 --no-publish 与 publish 两条路取到同一个值）。</summary>
static string ResolveEditorVersion(string repoRoot)
{
    var project = Path.Combine(repoRoot, "src", "HmiEditor.App", "HmiEditor.App.csproj");
    var json = RunCapture("dotnet", repoRoot, "msbuild", project,
        "-getProperty:InformationalVersion", "-getProperty:Version", "-nologo");
    if (json is not null)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var props = doc.RootElement.GetProperty("Properties");
            foreach (var key in new[] { "InformationalVersion", "Version" })
                if (props.TryGetProperty(key, out var value) &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString()!;
        }
        catch (JsonException)
        {
            // 版本探测失败不该让打包崩：回落"未知"并在清单里如实写着
        }
    }
    return "0.0.0-unknown";
}

static int Run(string fileName, string workDir, params string[] arguments)
{
    var psi = new ProcessStartInfo(fileName) { WorkingDirectory = workDir, UseShellExecute = false };
    foreach (var a in arguments) psi.ArgumentList.Add(a);
    using var process = Process.Start(psi);
    if (process is null) return -1;
    process.WaitForExit();
    return process.ExitCode;
}

static string? RunCapture(string fileName, string workDir, params string[] arguments)
{
    var psi = new ProcessStartInfo(fileName)
    {
        WorkingDirectory = workDir,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (var a in arguments) psi.ArgumentList.Add(a);
    using var process = Process.Start(psi);
    if (process is null) return null;
    var stdout = process.StandardOutput.ReadToEnd();
    process.StandardError.ReadToEnd();
    process.WaitForExit();
    return process.ExitCode == 0 ? stdout.Trim() : null;
}
