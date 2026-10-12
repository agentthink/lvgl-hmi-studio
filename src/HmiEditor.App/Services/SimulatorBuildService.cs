using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.App.Resources;
using HmiEditor.Device.Build;

namespace HmiEditor.App.Services;

/// <summary>
/// 模拟器编译结果。
/// </summary>
public sealed record SimulatorBuildResult(
    bool Success,
    string? ExecutablePath,
    string Log,
    IReadOnlyList<string> Errors);

/// <summary>
/// PC 模拟器编译服务 —— 将 HMI 工程一键编译为 LVGL+SDL2 模拟器可执行程序。
///
/// 流程：生成模拟器工程文件（main.c / sim_config.h / CMakeLists.txt / lv_conf.h / generated_hmi.c）
///       → cmake configure → cmake build → 产出 hmi_sim 可执行文件。
/// 编译输出实时回调；失败时从编译器输出中解析 error 行。
/// </summary>
public static class SimulatorBuildService
{
    /// <summary>识别编译器错误行的正则（GCC/Clang 与 CMake 输出）。</summary>
    private static readonly Regex ErrorLineRegex = new(
        @"(?:error:|错误:|fatal error|undefined reference|multiple definition|ld returned|No such file|cannot find|CMake Error)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 探测 LVGL 源码目录。返回 null 表示未找到。
    /// 探测顺序：用户配置 → 环境变量 LVGL_DIR → 工作目录上级 → 常用开发路径。
    /// </summary>
    public static string? FindLvglDirectory(string? configured, string workDir)
    {
        var candidates = new List<string?>();
        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
        candidates.Add(Environment.GetEnvironmentVariable("LVGL_DIR"));

        // 工作目录上级的 lvgl（与生成模板默认 ../lvgl 对齐）
        var parent = string.IsNullOrEmpty(workDir)
            ? null
            : Path.GetDirectoryName(workDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.IsNullOrEmpty(parent))
        {
            candidates.Add(Path.Combine(parent, "lvgl"));
            candidates.Add(Path.Combine(parent, "..", "lvgl"));
        }

        // 常用开发路径兜底
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Git", "lvgl"));

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                var norm = Path.GetFullPath(candidate);
                if (File.Exists(Path.Combine(norm, "lvgl.h"))
                    && File.Exists(Path.Combine(norm, "CMakeLists.txt")))
                {
                    return norm;
                }
            }
            catch { /* 忽略非法路径 */ }
        }
        return null;
    }

    /// <summary>
    /// 编译模拟器工程。
    /// </summary>
    /// <param name="project">HMI 工程。</param>
    /// <param name="workDir">模拟器工程输出目录（复用可做增量编译）。</param>
    /// <param name="lvglDir">LVGL 源码目录。</param>
    /// <param name="assetLibrary">资源库（图片/字体），用于条件代码生成；null 则不生成图片引用。</param>
    /// <param name="onLog">编译输出实时回调（按行）。</param>
    /// <param name="ct">取消令牌。</param>
    public static async Task<SimulatorBuildResult> BuildAsync(
        HmiProject project,
        string workDir,
        string lvglDir,
        AssetLibrary? assetLibrary = null,
        Action<string>? onLog = null,
        CancellationToken ct = default,
        IReadOnlyList<DeviceConnection>? connections = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(workDir);
        ArgumentNullException.ThrowIfNull(lvglDir);

        // 两条前置校验是**本族唯一的产出方**（登记 #61c 更正后重归 A 类异常档；阶段 103）：
        // 改动前抛的是裸 InvalidOperationException + 中文串，而它**没有码**
        // （SystemFailureCodes 的表是探针实测过才加的，里面没有 InvalidOperationException）
        // ⇒ 站点换成码化出口只会落回落分支、用户所见一字不变。这里把码补上。
        // ⚠ 回落原文**留在抛点**、不搬进工厂：规约是「产出方给码 + 实参 + 中文回落」，而产出方就是我；
        //   App 层的硬编码中文守卫（HardcodedLocalizationGuardTests）本来就豁免 throw new 那一类
        //   ⇒ 不必给扫描器开文件级后门。理由全文见 SimulatorBuildException 的类注释。
        if (!File.Exists(Path.Combine(lvglDir, "lvgl.h")) || !File.Exists(Path.Combine(lvglDir, "CMakeLists.txt")))
            throw new SimulatorBuildException(
                SimulatorBuildIssueCodes.LVGL_SOURCE_INVALID,
                $"LVGL 源码无效: {lvglDir}（缺少 lvgl.h 或 CMakeLists.txt）",
                lvglDir);

        if (string.IsNullOrEmpty(FindCmake()))
            throw new SimulatorBuildException(
                SimulatorBuildIssueCodes.CMAKE_NOT_FOUND,
                "未找到 cmake，请安装 CMake 后重试（Linux: sudo apt install cmake）");

        // 统一转为绝对路径。cmake 的 -S 位置参数相对「进程工作目录」解析，
        // 而 RunProcessAsync 又把 WorkingDirectory 也设为 workDir；若 workDir 是
        // 相对路径（CLI/测试常这样传），就会叠加成 "sim/sim" 导致 CMake 报
        // "The source directory ... does not exist"。
        workDir = Path.GetFullPath(workDir);
        lvglDir = Path.GetFullPath(lvglDir);

        var log = new StringBuilder();
        var errors = new List<string>();
        void Log(string line)
        {
            log.AppendLine(line);
            onLog?.Invoke(line);
            if (ErrorLineRegex.IsMatch(line)) errors.Add(line);
        }

        Directory.CreateDirectory(workDir);

        // 强制清理整个 build 目录, 彻底防止 CMake 增量编译缓存、
        // 旧 CMakeCache、遗留的 lvgl_demos/lvgl_examples 目标复用等问题。
        // 仅删 .o 不够彻底: CMake 仍可能按旧 Makefile2/CMakeCache 复用过期配置。
        var buildDir = Path.Combine(workDir, "build");
        try
        {
            if (Directory.Exists(buildDir))
            {
                Directory.Delete(buildDir, recursive: true);
            }
        }
        catch
        {
            // 极端情况(文件被占用)退化为文件级清理
            try
            {
                if (Directory.Exists(buildDir))
                {
                    // CMake 配置缓存是根因, 必须优先清
                    var cmakeCache = Path.Combine(buildDir, "CMakeCache.txt");
                    if (File.Exists(cmakeCache)) File.Delete(cmakeCache);
                    foreach (var f in Directory.GetFiles(buildDir, "*.o", SearchOption.AllDirectories))
                        try { File.Delete(f); } catch { }
                    foreach (var f in Directory.GetFiles(buildDir, "*.o.d", SearchOption.AllDirectories))
                        try { File.Delete(f); } catch { }
                    var exePath = OperatingSystem.IsWindows()
                        ? Path.Combine(buildDir, "hmi_sim.exe")
                        : Path.Combine(buildDir, "hmi_sim");
                    if (File.Exists(exePath))
                        try { File.Delete(exePath); } catch { }
                }
            }
            catch { /* 最后兜底: 不阻断构建 */ }
        }

        // 1. 生成模拟器工程文件
        Log(AppLocalization.Service.Format(Lang.SimBuildStep1, workDir));
        var generator = new HmiEditor.CodeGen.CodeGenerator();
        var code = generator.GenerateSimulatorModule(project, assetLibrary);
        // 构建运行时标签表（绑定变量）—— 模拟器主循环据此模拟刷新显示控件
        var bindingEngine = new RuntimeBindingEngine(project);
        bindingEngine.BuildRuntimeTags();
        var export = SimulatorProjectExporter.Export(project, code, workDir, assetLibrary, bindingEngine.Tags, connections);
        Log(AppLocalization.Service.Format(Lang.SimBuildFiles, string.Join(", ", export.Files)));

        // 2. cmake configure
        Log(AppLocalization.Service[Lang.SimBuildStep2]);
        Directory.CreateDirectory(buildDir);
        var cmake = FindCmake()!;
        var configureResult = await RunProcessAsync(
            cmake, $"\"{workDir}\" -B \"{buildDir}\" -DLVGL_DIR=\"{lvglDir}\"",
            workDir, Log, ct);

        // 3. cmake build
        Log(AppLocalization.Service[Lang.SimBuildStep3]);
        var buildResult = await RunProcessAsync(
            cmake, $"--build \"{buildDir}\" -j", workDir, Log, ct);

        // 4. 汇总
        var exe = LocateExecutable(buildDir);
        if (configureResult == 0 && buildResult == 0 && exe != null)
        {
            Log(AppLocalization.Service.Format(Lang.SimBuildOk, exe));
            return new SimulatorBuildResult(true, exe, log.ToString(), errors);
        }

        Log(AppLocalization.Service[Lang.SimBuildFailed]);
        // 去除重复错误行，取前 20 条供界面展示
        var distinctErrors = errors
            .Select(e => e.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(20)
            .ToList();
        if (distinctErrors.Count == 0)
            distinctErrors.Add(AppLocalization.Service.Format(Lang.SimBuildFailedDetail, configureResult, buildResult));
        return new SimulatorBuildResult(false, null, log.ToString(), distinctErrors);
    }

    /// <summary>在构建目录中定位模拟器可执行文件。</summary>
    public static string? LocateExecutable(string buildDir)
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

    /// <summary>定位 cmake 可执行文件。</summary>
    private static string? FindCmake()
    {
        var path = Environment.GetEnvironmentVariable("CMAKE_EXECUTABLE");
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
        return Which("cmake") ?? Which("cmake.exe");
    }

    private static string? Which(string name)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(dir, name);
                if (File.Exists(full)) return full;
            }
            catch { /* 忽略非法路径 */ }
        }
        return null;
    }

    /// <summary>异步运行进程，实时转发 stdout/stderr，返回退出码。</summary>
    private static async Task<int> RunProcessAsync(
        string fileName, string arguments, string workDir,
        Action<string>? onLine, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();

        var outputTask = ReadStreamAsync(process.StandardOutput, onLine, ct);
        var errorTask = ReadStreamAsync(process.StandardError, onLine, ct);

        // 取消时**真的杀掉**子进程（cmake/gcc 会继续写共享工作目录）—— 见 ChildProcessRunner
        await ChildProcessRunner.WaitForExitOrKillAsync(process, ct);
        await Task.WhenAll(outputTask, errorTask);
        return process.ExitCode;
    }

    private static async Task ReadStreamAsync(
        StreamReader reader, Action<string>? onLine, CancellationToken ct)
    {
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync();
                if (line == null) break;
                onLine?.Invoke(line);
            }
        }
        catch (OperationCanceledException) { /* 取消时静默结束 */ }
    }
}
