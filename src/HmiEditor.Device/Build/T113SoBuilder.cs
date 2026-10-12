using System.Diagnostics;
using HmiEditor.Core.Services;

namespace HmiEditor.Device.Build;

/// <summary>libhmi_ui.so 交叉编译选项。</summary>
public sealed class T113SoBuildOptions
{
    /// <summary>待编译的生成源码（generated_hmi.c）。</summary>
    public required string SourcePath { get; init; }

    /// <summary>产物输出目录（libhmi_ui.so 写入此处）。</summary>
    public required string OutputDir { get; init; }

    /// <summary>工程标签契约头 tags.h（由编辑器导出）。为空时回退 SDK 部署目录的 tags.h。</summary>
    public string? TagsHeaderPath { get; init; }

    /// <summary>Tina SDK 根目录（设置项）。为空时自动探测。</summary>
    public string? SdkRoot { get; init; }

    /// <summary>交叉编译器完整路径（设置项）。为空时在 SDK 内自动查找。</summary>
    public string? ToolchainGcc { get; init; }

    /// <summary>资源目录宏（生成代码 IMG_PATH = "A:" SIM_ASSET_DIR "/<file>"）。</summary>
    public string SimAssetDir { get; init; } = "/mnt/UDISK/hmi/assets";
}

/// <summary>交叉编译结果。</summary>
public sealed class T113SoBuildResult
{
    public bool Success { get; init; }
    public string? OutputPath { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> Logs { get; init; } = Array.Empty<string>();
}

/// <summary>解析后的构建上下文（供测试与诊断）。</summary>
public sealed class T113BuildContext
{
    public string SourcePath { get; init; } = "";
    public string OutputDir { get; init; } = "";
    public string CompilerPath { get; init; } = "";
    public string SdkRoot { get; init; } = "";
    public string RepoRoot { get; init; } = "";
    public string LvglRoot { get; init; } = "";
    public string ConfigDir { get; init; } = "";
    public string AppDir { get; init; } = "";
    public string TagsDir { get; init; } = "";
    public string StagingInclude { get; init; } = "";
    public IReadOnlyList<string> Missing { get; init; } = Array.Empty<string>();
}

/// <summary>
/// T113 libhmi_ui.so 交叉编译器 —— 在 PC 端用 Tina SDK 工具链把编辑器生成的
/// SharedLibrary 源码编译为 .so，供"下载到HMI"部署（/mnt/UDISK/hmi/libhmi_ui.so
/// + killall hmi_runtime 生效）。
///
/// 编译模型与固件 app/Makefile 的 UI_SO 目标一致：
///   -std=c99 -fPIC 编译（符号不解析），-shared -nostartfiles 链接；
///   全部 LVGL/hmi 符号由宿主 hmi_runtime（-rdynamic + RTLD_GLOBAL）在 dlopen 时解析，
///   因此只需要头文件（LVGL 源码头 + 固件 app 头 + staging freetype 头），无需链接库。
/// </summary>
public sealed class T113SoBuilder
{
    /// <summary>操作日志（每条一行，供 UI 展示）。</summary>
    public event Action<string>? Log;

    /// <summary>
    /// 自动探测 Tina SDK 根目录：设置值 → 环境变量 ZHSHU_HMI_SDK →
    /// 从应用目录向上查找（仓库内 firmware/Tina-SDK 或 firmware/Tina-Linux）。
    /// </summary>
    public static string? FindSdkRoot(string? configured) => FindSdkRoot(configured, AppContext.BaseDirectory);

    /// <summary>内部重载：指定向上查找的起始目录（测试用）。</summary>
    internal static string? FindSdkRoot(string? configured, string? searchStart)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return Path.GetFullPath(configured);

        var env = Environment.GetEnvironmentVariable("ZHSHU_HMI_SDK");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        if (string.IsNullOrEmpty(searchStart)) return null;
        var dir = new DirectoryInfo(searchStart);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            foreach (var name in new[] { "Tina-SDK", "Tina-Linux" })
            {
                var candidate = Path.Combine(dir.FullName, "firmware", name);
                if (Directory.Exists(Path.Combine(candidate, "out")))
                    return candidate;
            }
            // 独立交叉编译工具包（t113-cross-kit，由 tools/T113KitBuilder 从 SDK 提取，
            // 客户机无需完整 SDK）：目录内含 kit.json 标记
            if (IsKitRoot(dir.FullName))
                return dir.FullName;
        }
        return null;
    }

    /// <summary>交叉编译工具包标记：目录内含 kit.json 且 kind 匹配。</summary>
    public static bool IsKitRoot(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var marker = Path.Combine(path, "kit.json");
        if (!File.Exists(marker)) return false;
        try
        {
            var text = File.ReadAllText(marker);
            return text.Contains("\"zhishu-hmi-t113-cross-kit\"", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>仓库根目录：SDK 位于 &lt;repo&gt;/firmware/&lt;SDK&gt; 时取 &lt;repo&gt;；工具包则自身即根。</summary>
    public static string? FindRepoRoot(string? sdkRoot)
    {
        if (string.IsNullOrEmpty(sdkRoot)) return null;
        if (IsKitRoot(sdkRoot)) return sdkRoot;
        var firmware = Directory.GetParent(sdkRoot)?.FullName;
        if (firmware is null) return null;
        var repo = Directory.GetParent(firmware)?.FullName;
        if (repo is null) return null;
        return Directory.Exists(Path.Combine(repo, "firmware", "t113", "app", "runtime"))
            ? repo
            : null;
    }

    /// <summary>在 SDK/工具包中查找交叉编译器（out/*/staging_dir 或 &lt;kit&gt;/toolchain/bin）。</summary>
    public static string? FindToolchainGcc(string? configured, string? sdkRoot)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return Path.GetFullPath(configured);
        if (string.IsNullOrEmpty(sdkRoot)) return null;

        var binDirs = new List<string>();
        // 工具包：toolchain/bin（预编译工具链可整体重定位）
        var kitBin = Path.Combine(sdkRoot, "toolchain", "bin");
        if (Directory.Exists(kitBin)) binDirs.Add(kitBin);
        // SDK：out/*/staging_dir/toolchain/bin
        var outRoot = Path.Combine(sdkRoot, "out");
        if (Directory.Exists(outRoot))
        {
            foreach (var outDir in Directory.EnumerateDirectories(outRoot))
            {
                var staging = Path.Combine(outDir, "staging_dir", "toolchain", "bin");
                if (Directory.Exists(staging)) binDirs.Add(staging);
            }
        }
        // 优先 muslgnueabi 的 plain 名（软链，非 -gcc-6.4.1 版本文件）
        foreach (var binDir in binDirs.OrderByDescending(b => b.Contains("musl")))
        {
            var matches = Directory.EnumerateFiles(binDir, "arm-openwrt-linux-*gcc")
                .Where(f => !Path.GetFileName(f).Contains("-gcc-"))
                .OrderBy(f => Path.GetFileName(f).Contains("musl") ? 0 : 1)
                .ToList();
            if (matches.Count > 0) return matches[0];
        }
        return null;
    }

    /// <summary>LVGL 源码根（含 lvgl.h；工具包为 &lt;kit&gt;/lvgl）。</summary>
    public static string? FindLvglRoot(string? sdkRoot)
    {
        if (string.IsNullOrEmpty(sdkRoot)) return null;
        var candidates = IsKitRoot(sdkRoot)
            ? new[] { Path.Combine(sdkRoot, "lvgl") }
            : new[] { Path.Combine(sdkRoot, "package", "gui", "lvgl") };
        foreach (var lvgl in candidates)
            if (File.Exists(Path.Combine(lvgl, "lvgl.h"))) return lvgl;
        return null;
    }

    /// <summary>staging/sysroot 目标 include 目录（含 freetype2）。</summary>
    public static string? FindStagingInclude(string? sdkRoot)
    {
        if (string.IsNullOrEmpty(sdkRoot)) return null;
        // 工具包：sysroot/usr/include
        if (IsKitRoot(sdkRoot))
        {
            var kitUsr = Path.Combine(sdkRoot, "sysroot", "usr", "include");
            return Directory.Exists(Path.Combine(kitUsr, "freetype2")) ? kitUsr : null;
        }
        var outRoot = Path.Combine(sdkRoot, "out");
        if (!Directory.Exists(outRoot)) return null;
        foreach (var outDir in Directory.EnumerateDirectories(outRoot))
        {
            var staging = Path.Combine(outDir, "staging_dir");
            if (!Directory.Exists(staging)) continue;
            foreach (var target in Directory.EnumerateDirectories(staging, "target*"))
            {
                var usr = Path.Combine(target, "usr", "include");
                if (Directory.Exists(Path.Combine(usr, "freetype2")))
                    return usr;
            }
        }
        return null;
    }

    /// <summary>解析完整构建上下文（不执行编译），缺失项列入 <see cref="T113BuildContext.Missing"/>。</summary>
    public static T113BuildContext ResolveContext(T113SoBuildOptions options) =>
        ResolveContext(options, AppContext.BaseDirectory);

    /// <summary>内部重载：指定 SDK 向上查找起始目录（测试用）。</summary>
    internal static T113BuildContext ResolveContext(T113SoBuildOptions options, string? searchStart)
    {
        var missing = new List<string>();
        var sourcePath = Path.GetFullPath(options.SourcePath);
        var outputDir = Path.GetFullPath(options.OutputDir);

        if (!File.Exists(sourcePath)) missing.Add($"生成源码不存在: {sourcePath}");
        if (!Directory.Exists(outputDir)) missing.Add($"输出目录不存在: {outputDir}");

        var sdkRoot = FindSdkRoot(options.SdkRoot, searchStart);
        if (sdkRoot is null) missing.Add("未找到 Tina SDK/交叉编译工具包（设置 T113 SDK 目录或环境变量 ZHSHU_HMI_SDK）");

        var isKit = IsKitRoot(sdkRoot ?? "");
        var repoRoot = FindRepoRoot(sdkRoot);
        if (repoRoot is null) missing.Add("未找到固件头契约（.so 编译需要 screen_manager.h/hmi_ui_api.h；工具包为 <kit>/firmware/app）");

        var compiler = FindToolchainGcc(options.ToolchainGcc, sdkRoot);
        if (compiler is null) missing.Add("未找到交叉编译器 arm-openwrt-linux-*-gcc（SDK out/staging 或工具包 toolchain/bin）");

        var lvgl = FindLvglRoot(sdkRoot);
        if (lvgl is null) missing.Add("未找到 LVGL 源码根（SDK package/gui/lvgl 或工具包 lvgl）");

        var staging = FindStagingInclude(sdkRoot);
        if (staging is null) missing.Add("未找到 freetype2 头文件（SDK staging 或工具包 sysroot/usr/include）");

        // 固件配置/应用/标签头（工具包布局 <kit>/firmware/...，SDK 布局 <repo>/firmware/t113/...）
        var fwBase = isKit && repoRoot is not null
            ? Path.Combine(repoRoot, "firmware")
            : repoRoot is null ? "" : Path.Combine(repoRoot, "firmware", "t113");
        var configDir = fwBase is "" ? "" : Path.Combine(fwBase, "config");
        if (configDir is "" || !File.Exists(Path.Combine(configDir, "lv_conf.h")))
            missing.Add("未找到固件 LVGL 配置 lv_conf.h（SDK firmware/t113/config 或工具包 firmware/config）");

        var appDir = fwBase is "" ? "" : Path.Combine(fwBase, "app");
        if (appDir is "" || !File.Exists(Path.Combine(appDir, "runtime", "screen_manager.h")))
            missing.Add("未找到固件运行头文件 screen_manager.h（SDK firmware/t113/app/runtime 或工具包 firmware/app/runtime）");

        var tagsDir = "";
        if (!string.IsNullOrWhiteSpace(options.TagsHeaderPath) && File.Exists(options.TagsHeaderPath))
        {
            tagsDir = Path.GetDirectoryName(Path.GetFullPath(options.TagsHeaderPath)) ?? "";
        }
        else if (fwBase is not "" &&
                 File.Exists(Path.Combine(fwBase, "generated", "tags.h")))
        {
            tagsDir = Path.Combine(fwBase, "generated");
        }
        else
        {
            missing.Add("未找到标签契约头 tags.h（请先编译转换生成工程 tags.h）");
        }

        return new T113BuildContext
        {
            SourcePath = sourcePath,
            OutputDir = outputDir,
            CompilerPath = compiler ?? "",
            SdkRoot = sdkRoot ?? "",
            RepoRoot = repoRoot ?? "",
            LvglRoot = lvgl ?? "",
            ConfigDir = configDir,
            AppDir = appDir,
            TagsDir = tagsDir,
            StagingInclude = staging ?? "",
            Missing = missing
        };
    }

    /// <summary>由 staging include 路径反推 sysroot 根。
    /// SDK 布局 .../staging_dir/target/usr/include → staging_dir；
    /// 工具包布局 &lt;kit&gt;/sysroot/usr/include → sysroot（target 仅 SDK 有）。</summary>
    internal static string? SysrootRootOf(string? stagingInclude)
    {
        if (string.IsNullOrEmpty(stagingInclude)) return null;
        var full = Path.GetFullPath(stagingInclude);
        var usr = Path.GetDirectoryName(full);          // .../usr
        var mid = Path.GetDirectoryName(usr);           // .../target 或 .../sysroot
        if (mid is null) return null;
        if (Path.GetFileName(mid).Equals("target", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(mid);          // staging_dir
        return mid;                                     // sysroot（工具包）
    }

    /// <summary>构建编译参数（不含编译器本身），供诊断与测试。</summary>
    public static IReadOnlyList<string> BuildArguments(T113BuildContext ctx, string simAssetDir, string workDir)
    {
        return new[]
        {
            "-std=c99", "-D_GNU_SOURCE", "-Wno-unused-parameter", "-DCONF_G2D_VERSION_NEW",
            $"-DSIM_ASSET_DIR=\"{simAssetDir}\"",
            "-I" + ctx.ConfigDir,
            "-I" + ctx.LvglRoot,
            "-I" + ctx.AppDir,
            "-I" + Path.Combine(ctx.AppDir, "runtime"),
            "-I" + Path.Combine(ctx.AppDir, "comm"),
            "-I" + Path.Combine(ctx.AppDir, "drivers"),
            "-I" + Path.Combine(ctx.AppDir, "settings"),
            "-I" + ctx.TagsDir,
            "-I" + ctx.StagingInclude,
            "-I" + Path.Combine(ctx.StagingInclude, "freetype2"),
            "-fPIC",
            "-c", ctx.SourcePath,
            "-o", Path.Combine(workDir, "generated_hmi.o")
        };
    }

    /// <summary>
    /// 交叉编译 generated_hmi.c → libhmi_ui.so。
    /// 任一前置缺失时返回失败并给出明确原因（不抛异常）。
    /// </summary>
    public Task<T113SoBuildResult> BuildAsync(T113SoBuildOptions options, IProgress<double>? progress = null, CancellationToken ct = default) =>
        BuildAsync(options, AppContext.BaseDirectory, progress, ct);

    /// <summary>内部重载：指定 SDK 向上查找起始目录（测试用）。</summary>
    internal async Task<T113SoBuildResult> BuildAsync(T113SoBuildOptions options, string? searchStart, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var logs = new List<string>();
        void Info(string msg) { logs.Add(msg); Log?.Invoke(msg); }

        try
        {
            var ctx = ResolveContext(options, searchStart);
            if (ctx.Missing.Count > 0)
            {
                foreach (var m in ctx.Missing) Info($"[.so] ✗ {m}");
                return new T113SoBuildResult { Success = false, Error = string.Join("；", ctx.Missing), Logs = logs };
            }

            Info($"[.so] 工具链: {ctx.CompilerPath}");
            Info($"[.so] LVGL: {ctx.LvglRoot}");
            Info($"[.so] SDK: {ctx.SdkRoot}");

            var workDir = AppPaths.TempSubdir("so-build");
            Directory.CreateDirectory(workDir);
            Directory.CreateDirectory(ctx.OutputDir);

            // OpenWrt 工具链要求 STAGING_DIR 指向 sysroot 根（staging_dir / 工具包的 sysroot）
            // （否则每次调用告警、链接库场景无法定位 sysroot）。staging include 路径以
            // "/usr/include" 结尾时，其上两级即 sysroot 根（SDK 与工具包同构）。
            var toolchainEnv = new Dictionary<string, string>();
            var stagingRoot = SysrootRootOf(ctx.StagingInclude);
            if (stagingRoot is not null)
                toolchainEnv["STAGING_DIR"] = stagingRoot;

            // 工具链自带的 lib 目录（libgcc_s 等）：链接时显式加入搜索路径。
            // SDK 的 staging wrapper 自带 -L；独立工具包（t113-cross-kit）的 wrapper
            // 只有 --sysroot + rpath，libgcc_s 会落到宿主 /usr/lib → 链接失败。
            var toolchainLibs = new List<string>();
            var tcRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ctx.CompilerPath) ?? ".", ".."));
            foreach (var lib in new[] { Path.Combine(tcRoot, "lib"), Path.Combine(tcRoot, "usr", "lib") })
            {
                if (Directory.Exists(lib)) toolchainLibs.Add(lib);
            }

            // 1. 编译
            progress?.Report(0.1);
            var compileArgs = BuildArguments(ctx, options.SimAssetDir, workDir);
            Info($"[.so] 编译 {Path.GetFileName(ctx.SourcePath)} ...");
            var (compileExit, compileOut) = await RunAsync(ctx.CompilerPath, compileArgs, ct, toolchainEnv);
            foreach (var line in SplitLines(compileOut))
                Info($"[.so] {line}");
            if (compileExit != 0)
            {
                var err = $"交叉编译失败 (exit {compileExit})";
                Info($"[.so] ✗ {err}");
                return new T113SoBuildResult { Success = false, Error = err, Logs = logs };
            }
            progress?.Report(0.6);

            // 2. 链接（符号由宿主 dlopen 时解析，-nostartfiles 不链接 CRT）
            var outputPath = Path.Combine(ctx.OutputDir, "libhmi_ui.so");
            Info("[.so] 链接 libhmi_ui.so ...");
            var linkArgs = new List<string> { "-shared", "-nostartfiles" };
            foreach (var lib in toolchainLibs)
            {
                linkArgs.Add("-L");
                linkArgs.Add(lib);
            }
            linkArgs.Add("-o");
            linkArgs.Add(outputPath);
            linkArgs.Add(Path.Combine(workDir, "generated_hmi.o"));
            var (linkExit, linkOut) = await RunAsync(ctx.CompilerPath, linkArgs, ct, toolchainEnv);
            foreach (var line in SplitLines(linkOut))
                Info($"[.so] {line}");
            if (linkExit != 0 || !File.Exists(outputPath))
            {
                var err = $"链接失败 (exit {linkExit})";
                Info($"[.so] ✗ {err}");
                return new T113SoBuildResult { Success = false, Error = err, Logs = logs };
            }
            progress?.Report(1.0);
            Info($"[.so] ✓ 已生成 {outputPath}");
            return new T113SoBuildResult { Success = true, OutputPath = outputPath, Logs = logs };
        }
        catch (OperationCanceledException)
        {
            Info("[.so] 已取消");
            return new T113SoBuildResult { Success = false, Error = "已取消", Logs = logs };
        }
        catch (Exception ex)
        {
            Info($"[.so] ✗ {ex.Message}");
            return new T113SoBuildResult { Success = false, Error = ex.Message, Logs = logs };
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName,
        IReadOnlyList<string> args,
        CancellationToken ct,
        IReadOnlyDictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        if (env != null)
            foreach (var (key, value) in env)
                psi.Environment[key] = value;

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
            return (-1, "进程启动失败");

        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        // 取消时**真的杀掉**子进程（gcc/ld 会继续写共享工作目录）—— 见 ChildProcessRunner
        await ChildProcessRunner.WaitForExitOrKillAsync(process, ct);
        await Task.WhenAll(stdout, stderr);
        return (process.ExitCode, (stdout.Result + stderr.Result).Trim());
    }

    private static IEnumerable<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
