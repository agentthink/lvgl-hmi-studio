using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;

// 参数：<project.hmi> [输出目录=sim-output] [LVGL 源码目录=环境变量 LVGL_DIR]
// 不再硬编码开发机绝对路径，保证在任意机器/CI 上可运行。
var hmiPath = args.Length > 0 ? args[0] : "";
var outputDir = args.Length > 1 ? args[1] : "sim-output";
var lvglDir = args.Length > 2 ? args[2] : (Environment.GetEnvironmentVariable("LVGL_DIR") ?? "");

Console.WriteLine($"=== HMI 模拟器导出工具 ===");
Console.WriteLine($"输入 HMI: {hmiPath}");
Console.WriteLine($"输出目录: {outputDir}");
Console.WriteLine($"LVGL 源码: {lvglDir}");
Console.WriteLine();

if (string.IsNullOrWhiteSpace(hmiPath) || !File.Exists(hmiPath))
{
    Console.Error.WriteLine($"错误: HMI 文件不存在: {(string.IsNullOrWhiteSpace(hmiPath) ? "<未提供>" : hmiPath)}");
    Console.Error.WriteLine("用法: sim-export <project.hmi> [输出目录] [LVGL 源码目录]");
    Environment.Exit(1);
}

if (string.IsNullOrWhiteSpace(lvglDir) || !Directory.Exists(lvglDir) || !File.Exists(Path.Combine(lvglDir, "lvgl.h")))
{
    Console.Error.WriteLine($"错误: LVGL 源码无效: {(string.IsNullOrWhiteSpace(lvglDir) ? "<未提供>" : lvglDir)}");
    Console.Error.WriteLine("请传入第 3 个参数，或设置环境变量 LVGL_DIR 指向 LVGL 源码根目录。");
    Environment.Exit(1);
}

// 绝对化路径：cmake 的 -S 位置参数相对进程工作目录解析，
// 相对路径会与工作目录叠加导致 CMake 找不到源目录。
outputDir = Path.GetFullPath(outputDir);
lvglDir = Path.GetFullPath(lvglDir);

Console.WriteLine("[1/3] 读取 HMI 项目...");
using var fs = File.OpenRead(hmiPath);
var assetDir = Path.Combine(outputDir, "assets");
Directory.CreateDirectory(assetDir);
var package = HmiPackageService.Unpack(fs, assetDir);
var project = package.Project;

Console.WriteLine($"    项目名称: {project.Name}");
Console.WriteLine($"    屏幕数量: {project.Screens.Count}");
var imgCount = package.AssetLibrary?.Assets.Count(a => a.Type == AssetType.Image) ?? 0;
var fontCount = package.AssetLibrary?.Assets.Count(a => a.Type == AssetType.Font) ?? 0;
Console.WriteLine($"    图片资源: {imgCount}");
Console.WriteLine($"    字体资源: {fontCount}");

if (package.AssetLibrary != null)
{
    foreach (var asset in package.AssetLibrary.Assets)
    {
        Console.WriteLine($"      - [{asset.Type}] {asset.Id}: {asset.Name} ({asset.FilePath})");
    }
}

Console.WriteLine();
Console.WriteLine("[2/3] 生成 C 代码并导出模拟器工程...");
var generator = new HmiEditor.CodeGen.CodeGenerator();
var code = generator.GenerateSimulatorModule(project, package.AssetLibrary);
var bindingEngine = new HmiEditor.Core.Services.RuntimeBindingEngine(project);
bindingEngine.BuildRuntimeTags();

Directory.CreateDirectory(outputDir);
var export = SimulatorProjectExporter.Export(project, code, outputDir, package.AssetLibrary,
    tags: bindingEngine.Tags,
    connections: project.VariableManager.Connections);
Console.WriteLine($"    导出文件数: {export.Files.Count}");
foreach (var f in export.Files)
    Console.WriteLine($"      - {f}");

Console.WriteLine();
Console.WriteLine("[3/3] 编译模拟器 (cmake)...");
// 直接调用底层的生成和导出逻辑，传入 AssetLibrary
var buildWorkDir = outputDir;
Directory.CreateDirectory(buildWorkDir);
var logBuilder = new StringBuilder();
var errors = new List<string>();
void LogLine(string line)
{
    Console.WriteLine($"    {line}");
    logBuilder.AppendLine(line);
    if (line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("fatal", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("undefined reference", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("CMake Error", StringComparison.OrdinalIgnoreCase))
    {
        errors.Add(line);
    }
}

// 检查 cmake
var cmakeExe = FindCmake();
if (cmakeExe == null)
{
    Console.Error.WriteLine("错误: 未找到 cmake, 请安装 CMake 后重试（Linux: sudo apt install cmake）");
    Environment.Exit(3);
}
Console.WriteLine($"    使用 CMake: {cmakeExe}");

// cmake configure
var buildDir = Path.Combine(buildWorkDir, "build");
Directory.CreateDirectory(buildDir);
LogLine("[cmake configure]...");
var exitCode = await RunProcessAsync(cmakeExe,
    $"\"{buildWorkDir}\" -B \"{buildDir}\" -DLVGL_DIR=\"{lvglDir}\"",
    buildWorkDir, LogLine);

var buildExitCode = 0;
if (exitCode == 0)
{
    // cmake build
    LogLine("[cmake build]...");
    buildExitCode = await RunProcessAsync(cmakeExe, $"--build \"{buildDir}\" -j", buildWorkDir, LogLine);
}

// 查找可执行文件
var exe = LocateExe(buildDir);

Console.WriteLine();
if (exitCode == 0 && buildExitCode == 0 && exe != null)
{
    Console.WriteLine($"✅ 编译成功!");
    Console.WriteLine($"   可执行文件: {exe}");
    Console.WriteLine();
    Console.WriteLine($"运行方式:");
    Console.WriteLine($"  {exe}");
    Console.WriteLine($"  {exe} --scale 2");
    Console.WriteLine($"  {exe} --selftest");
}
else
{
    Console.WriteLine($"❌ 编译失败!");
    if (errors.Count > 0)
    {
        Console.WriteLine($"错误信息 (前 20 条):");
        foreach (var err in errors.Take(20))
            Console.WriteLine($"  - {err}");
    }
    Environment.Exit(2);
}

// 辅助方法
static string? FindCmake()
{
    var fromEnv = Environment.GetEnvironmentVariable("CMAKE_EXECUTABLE");
    if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv)) return fromEnv;
    return Which("cmake") ?? Which("cmake.exe");
}

static string? Which(string name)
{
    var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
    foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        try
        {
            var full = Path.Combine(dir, name);
            if (File.Exists(full)) return full;
        }
        catch { }
    }
    return null;
}

static async Task<int> RunProcessAsync(
    string fileName, string arguments, string workDir,
    Action<string> onLine)
{
    var psi = new System.Diagnostics.ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        WorkingDirectory = workDir,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = System.Text.Encoding.UTF8,
        StandardErrorEncoding = System.Text.Encoding.UTF8,
        CreateNoWindow = true
    };
    using var process = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };
    process.Start();
    var outTask = ReadStreamAsync(process.StandardOutput, onLine);
    var errTask = ReadStreamAsync(process.StandardError, onLine);
    await process.WaitForExitAsync();
    await Task.WhenAll(outTask, errTask);
    return process.ExitCode;
}

static async Task ReadStreamAsync(System.IO.StreamReader reader, Action<string> onLine)
{
    while (true)
    {
        var line = await reader.ReadLineAsync();
        if (line == null) break;
        onLine(line);
    }
}

static string? LocateExe(string buildDir)
{
    if (!Directory.Exists(buildDir)) return null;
    var names = OperatingSystem.IsWindows()
        ? new[] { "hmi_sim.exe", "hmi_sim" }
        : new[] { "hmi_sim" };
    foreach (var name in names)
    {
        var path = Path.Combine(buildDir, name);
        if (File.Exists(path)) return path;
    }
    return null;
}
