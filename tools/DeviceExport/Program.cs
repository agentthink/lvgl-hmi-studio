// device-export — 设备侧产物导出 CLI（阶段 309；§12 第 6 项 ①）
//
// 为什么要它：设备树的 `screens.c` / `tags.{h,c}` / `generated_security.h` 一直由**编辑器里的交互式动作**
// 产出（`BuildPipelineService`），于是"夹具能不能再导出""能不能构建期生成"都判不了。
// 这里把四类产物收敛到一个**可脚本化**的入口上（四类各自的出处见 docs/2026-10-11-unfinished-plan-ledger.md §12 第 6 项）。
//
// 用法：
//   dotnet run --project tools/DeviceExport -- <project.json> <outDir> [--assets <dir>]
//   dotnet run --project tools/DeviceExport -- <X.hmi>      <outDir>
//
// ⚠ 已知两点（都写在 `--help` 与输出里，不埋）：
//   · 当前生成器会输出 `// Generator: … +<git sha>` ⇒ 产物**随提交变**，所以"可再导出"的比对要归一化那一行；
//   · 资产库（AssetLibrary）本轮**不重建**（`--assets` 只用于复制 `assets/`），
//     生成器按 `null` 资产库跑 —— 图片路径宏仍然生成，但资产是否齐全不由本工具判。

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("用法: device-export <project.json|X.hmi> <outDir> [--assets <dir>]");
    return 2;
}

var input = Path.GetFullPath(args[0]);
var outDir = Path.GetFullPath(args.Length > 1 ? args[1] : "device-export-out");
var assetsArg = "";
for (var i = 2; i < args.Length; i++)
    if (args[i] == "--assets" && i + 1 < args.Length) assetsArg = args[++i];

var work = Path.Combine(Path.GetTempPath(), "device-export-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(work);
Directory.CreateDirectory(outDir);

try
{
    // ── 1. 取 project.json（输入是 .hmi 就先解开；它是 zip）──
    string projectJsonPath;
    string assetsDir = assetsArg;
    if (input.EndsWith(".hmi", StringComparison.OrdinalIgnoreCase))
    {
        ZipFile.ExtractToDirectory(input, work, overwriteFiles: true);
        projectJsonPath = Path.Combine(work, "project.json");
        if (assetsDir.Length == 0) assetsDir = Path.Combine(work, "assets");
    }
    else
    {
        projectJsonPath = input;
        if (assetsDir.Length == 0)
        {
            var sibling = Path.Combine(Path.GetDirectoryName(input) ?? ".", "assets");
            if (Directory.Exists(sibling)) assetsDir = sibling;
        }
    }
    if (!File.Exists(projectJsonPath)) throw new FileNotFoundException("找不到 project.json", projectJsonPath);

    var project = ProjectSerializer.Deserialize(File.ReadAllText(projectJsonPath))
                  ?? throw new InvalidOperationException("project.json 解析失败");
    Console.WriteLine($"工程：{project.Name} / 屏数={project.Screens.Count}");

    // ── 2. 运行时标签（tags.h / tags.c 的输入）──
    var engine = new RuntimeBindingEngine(project);
    engine.BuildRuntimeTags();
    var tags = engine.Tags;
    Console.WriteLine($"运行时标签数 = {tags.Count}");

    // ── 3. 资产库 ──
    // ⚠ 阶段 313 的教训：不传资产库时 screens.c 里**没有** IMG_PATH_* 资源声明，
    //   与夹具一比就是"假差异"。资产条目从工程自带的 assets.json 读（那里有 id/宽高/位深），
    //   文件本身从 --assets 目录取（仓内就是设备侧那份）。
    var library = new AssetLibrary();
    var assetsJson = Path.Combine(Path.GetDirectoryName(projectJsonPath) ?? ".", "assets.json");
    if (File.Exists(assetsJson))
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(assetsJson));
        foreach (var e in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var file = Path.GetFileName(e.GetProperty("filePath").GetString() ?? "");
            library.AddAsset(new AssetItem
            {
                Id = e.GetProperty("id").GetString() ?? "",
                Name = e.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                Type = (e.GetProperty("type").GetString() ?? "image") == "font" ? AssetType.Font : AssetType.Image,
                FilePath = Path.Combine(assetsDir.Length > 0 ? assetsDir : ".", file),
                SizeBytes = e.TryGetProperty("sizeBytes", out var s) ? s.GetInt64() : 0,
                Width = e.TryGetProperty("width", out var w) ? w.GetInt32() : 0,
                Height = e.TryGetProperty("height", out var h) ? h.GetInt32() : 0,
                ColorDepth = e.TryGetProperty("colorDepth", out var cd) ? cd.GetInt32() : 0,
                ReferenceCount = e.TryGetProperty("referenceCount", out var rc) ? rc.GetInt32() : 0,
            });
        }
    }
    Console.WriteLine($"资产条目 = {library.Assets.Count}");

    // ── 4. 四类产物 ──
    var tagsGen = new RuntimeTagsCodeGen();
    var screens = new CodeGenerator().GenerateSharedLibraryProject(project, library);

    File.WriteAllText(Path.Combine(outDir, "screens.c"), screens);
    File.WriteAllText(Path.Combine(outDir, "tags.h"), tagsGen.GenerateHeader(tags));
    File.WriteAllText(Path.Combine(outDir, "tags.c"), tagsGen.Generate(tags));

    // generated_security.h 由模拟器导出器产出（它在 Core）⇒ 导到临时目录再挑需要的文件
    var simOut = Path.Combine(work, "sim");
    SimulatorProjectExporter.Export(project, screens, simOut, library, tags, null);
    var security = Path.Combine(simOut, "generated_security.h");
    if (File.Exists(security)) File.Copy(security, Path.Combine(outDir, "generated_security.h"), overwrite: true);
    else Console.WriteLine("⚠ 模拟器导出没有产出 generated_security.h");

    // 资产：只复制（`--assets` 指向的那份），不做内容判断
    if (assetsDir.Length > 0 && Directory.Exists(assetsDir))
    {
        var dst = Path.Combine(outDir, "assets");
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.EnumerateFiles(assetsDir))
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), overwrite: true);
    }

    foreach (var f in Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories)
                 .OrderBy(p => p, StringComparer.Ordinal))
    {
        var bytes = File.ReadAllBytes(f);
        Console.WriteLine($"  {Path.GetRelativePath(outDir, f),-24} {bytes.Length,9} B  " +
                          $"{Convert.ToHexString(SHA256.HashData(bytes))[..16]}");
    }
    Console.WriteLine($"输出目录：{outDir}");
    return 0;
}
finally
{
    try { Directory.Delete(work, recursive: true); } catch { /* 临时目录清不掉不算失败 */ }
}
