using HmiEditor.Core.Services;
using HmiEditor.Device.Channels;
using HmiEditor.Device.Abstractions;

namespace HmiEditor.Device.Updates;

/// <summary>部署资源条目：本地文件 → 设备相对路径（置于 <c>/mnt/UDISK/hmi/assets/</c> 下）。</summary>
public sealed record HmiDeployAsset(string LocalPath, string RemoteRelativePath);

/// <summary>下载到 HMI 的部署结果。</summary>
public sealed class HmiDeployResult
{
    /// <summary>已上传的源码文件数。</summary>
    public int SourceFilesUploaded { get; init; }

    /// <summary>已上传的资源文件数。</summary>
    public int AssetsUploaded { get; init; }

    /// <summary>是否已部署 libhmi_ui.so 到 /mnt/UDISK/hmi/。</summary>
    public bool SoDeployed { get; init; }

    /// <summary>部署 .so 后 hmi_runtime 是否已重启（procd 自动拉起并加载新 .so）。</summary>
    public bool RuntimeRestarted { get; init; }

    /// <summary>.so 未部署的原因（本地校验失败等）；null 表示无错误。</summary>
    public string? SoDeployError { get; init; }

    /// <summary>使用的本地 .so 路径（未部署时为 null）。</summary>
    public string? SharedLibraryPath { get; init; }
}

/// <summary>
/// 下载到 HMI 完整部署服务 —— 补齐"源码上传 ≠ 界面更新"的链路：
///   <list type="bullet">
///     <item>源码工程 → /mnt/UDISK/hmi/src/（编辑器编译转换产物）；</item>
///     <item>工程资源（图片/字体）→ /mnt/UDISK/hmi/assets/（生成代码 IMG_PATH 引用）；</item>
///     <item>预编译 libhmi_ui.so → /mnt/UDISK/hmi/libhmi_ui.so（宿主唯一 dlopen 路径）；</item>
///     <item>killall hmi_runtime —— procd（S97hmiruntime respawn）自动拉起新进程并加载新 .so。</item>
///   </list>
/// 与固件契约（screen_manager.c hmi_ui_load / main.c 启动加载）保持一致：
/// .so 只在 hmi_runtime 进程启动时加载一次，不部署 .so 则界面不会变化。
/// </summary>
public sealed class HmiDeployService : IHmiDeployService
{
    /// <summary>源码默认部署目录。</summary>
    public const string DefaultSourceDir = "/mnt/UDISK/hmi/src";

    /// <summary>HMI UI 动态库设备端路径（宿主 dlopen 唯一优先路径）。</summary>
    public const string DefaultSoPath = "/mnt/UDISK/hmi/libhmi_ui.so";

    /// <summary>资源默认部署目录（生成代码 SIM_ASSET_DIR）。</summary>
    public const string DefaultAssetDir = "/mnt/UDISK/hmi/assets";

    /// <summary>运行时连接配置文件设备端路径（comm_core.c hmi_comm_init 启动读取，免重编固件）。</summary>
    public const string RuntimeConfigPath = "/mnt/UDISK/hmi/comm_config.ini";

    /// <summary>操作日志（每条一行，供 UI 展示）。</summary>
    public event Action<string>? Log;

    /// <summary>
    /// 执行完整部署：
    /// 源码 + 资源上传后，若提供 <paramref name="sharedLibraryPath"/>（预编译 libhmi_ui.so）
    /// 则安装到设备并重启 hmi_runtime 使其生效；未提供时仅上传源码/资源，
    /// 界面不会更新（调用方应据此提示用户）。
    /// <paramref name="runtimeConfigText"/> 为运行时连接配置文本（comm_config.ini，
    /// CommConfigCodeGen.GenerateRuntimeConfigText 生成）：非空写设备文件
    /// /mnt/UDISK/hmi/comm_config.ini（宿主启动读取，改连接参数免重编固件）；
    /// 为空串表示工程无启用连接 → 删除设备旧配置文件，避免残留配置继续生效。
    /// </summary>
    public async Task<HmiDeployResult> DeployAsync(
        IDeviceChannel channel,
        IReadOnlyList<string> sourceFiles,
        string? sharedLibraryPath = null,
        IReadOnlyList<HmiDeployAsset>? assets = null,
        string remoteSourceDir = DefaultSourceDir,
        string? runtimeConfigText = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(sourceFiles);

        // 1. 源码工程
        if (sourceFiles.Count > 0)
        {
            Log?.Invoke($"[部署] 创建目录 {remoteSourceDir}");
            await channel.SendCommandAsync($"mkdir -p {remoteSourceDir}", TimeSpan.FromSeconds(8), ct);
            await UploadManyAsync(channel, sourceFiles, remoteSourceDir, progress, ct,
                phaseStart: 0.0, phaseEnd: 0.35, step: "源码");
        }

        // 2. 工程资源（图片/字体，生成代码 IMG_PATH = A:/mnt/UDISK/hmi/assets/&lt;文件名&gt;）
        var validAssets = assets?
            .Where(a => !string.IsNullOrWhiteSpace(a.LocalPath) && File.Exists(a.LocalPath))
            .ToList();
        if (validAssets is { Count: > 0 })
        {
            Log?.Invoke($"[部署] 创建目录 {DefaultAssetDir}");
            await channel.SendCommandAsync($"mkdir -p {DefaultAssetDir}", TimeSpan.FromSeconds(8), ct);
            var uploaded = 0;
            var totalAssets = validAssets.Count;
            foreach (var asset in validAssets)
            {
                var remotePath = $"{DefaultAssetDir}/{asset.RemoteRelativePath.TrimStart('/')}";
                Log?.Invoke($"[部署] 资源: {Path.GetFileName(asset.LocalPath)} → {remotePath}");
                var step = new Progress<double>(v =>
                    progress?.Report(0.35 + (uploaded + v) / totalAssets * 0.25));
                await channel.TransferFileAsync(asset.LocalPath, remotePath, step, ct);
                uploaded++;
            }
        }

        // 3. 运行时连接配置：IDE 通讯设置 → /mnt/UDISK/hmi/comm_config.ini，
        //    宿主 comm_core hmi_comm_init 启动读取（免重编固件）。工程无启用连接时
        //    删除设备旧配置文件，避免残留配置继续生效。
        if (runtimeConfigText != null)
        {
            if (string.IsNullOrWhiteSpace(runtimeConfigText))
            {
                Log?.Invoke($"[部署] 工程无启用通讯连接，删除设备旧连接配置 {RuntimeConfigPath}");
                await channel.SendCommandAsync(
                    $"rm -f {RuntimeConfigPath}; echo HMI_CFG_CLEARED", TimeSpan.FromSeconds(8), ct);
            }
            else
            {
                // 中转文件：唯一名（两个实例同时下载不会互相覆盖）+ 用完即删。
                // 原实现是共享临时目录里的固定名 "zhishu_hmi_comm_config.ini"：
                // 并发会串内容，且 tmpfs 满时 WriteAllTextAsync 抛
                // IOException(No space left on device)。
                var tmpCfg = AppPaths.NewTempFilePath("comm-config", ".ini");
                try
                {
                    await File.WriteAllTextAsync(tmpCfg, runtimeConfigText, ct);
                    Log?.Invoke($"[部署] 下发运行时连接配置 → {RuntimeConfigPath}（连接参数免重编固件）");
                    await channel.SendCommandAsync(
                        $"mkdir -p /mnt/UDISK/hmi", TimeSpan.FromSeconds(8), ct);
                    await channel.TransferFileAsync(tmpCfg, RuntimeConfigPath, null, ct);
                }
                finally
                {
                    // 清理失败不能改写部署结论（同 TestTempDir.DeleteFile 的理由）。
                    try { if (File.Exists(tmpCfg)) File.Delete(tmpCfg); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        // 4. libhmi_ui.so：安装到宿主 dlopen 唯一优先路径
        var soDeployed = false;
        var runtimeRestarted = false;
        string? soDeployError = null;
        if (!string.IsNullOrWhiteSpace(sharedLibraryPath) && File.Exists(sharedLibraryPath))
        {
            // 4a. 本地校验：必须是 ARM ELF（T113 Cortex-A7），否则 dlopen 必然失败（黑屏/无变化）
            if (!IsArmElf(sharedLibraryPath))
            {
                soDeployError = $"所选 .so 不是 ARM ELF（{Path.GetFileName(sharedLibraryPath)}），无法在 T113 加载，已跳过部署";
                Log?.Invoke($"[部署] ✗ {soDeployError}");
            }
            else
            {
                Log?.Invoke($"[部署] 创建目录 /mnt/UDISK/hmi");
                await channel.SendCommandAsync("mkdir -p /mnt/UDISK/hmi", TimeSpan.FromSeconds(8), ct);
                Log?.Invoke($"[部署] 上传 {Path.GetFileName(sharedLibraryPath)} → {DefaultSoPath}");
                var soProgress = new Progress<double>(v => progress?.Report(0.6 + v * 0.35));
                await channel.TransferFileAsync(sharedLibraryPath, DefaultSoPath, soProgress, ct);
                soDeployed = true;
                progress?.Report(0.95);

                // 5. 重启运行时：killall 后由 procd respawn 自动拉起，新进程 dlopen 新 .so。
                //    注意 killall 与 pgrep 必须分开执行——通道按"静默期"判定命令结束，
                //    "killall; sleep 3; pgrep" 中 sleep 后的输出会被静默判定截断，结果永远误判。
                Log?.Invoke("[部署] 重启 hmi_runtime 以加载新界面");
                try
                {
                    await channel.SendCommandAsync(
                        "killall hmi_runtime 2>/dev/null; echo HMI_KILLED",
                        TimeSpan.FromSeconds(10), ct);
                    Log?.Invoke($"[部署] 已发送重启信号，等待 hmi_runtime 自动拉起（procd respawn，约 {RestartProbeDelay.TotalSeconds:0} 秒）...");
                    await Task.Delay(RestartProbeDelay, ct);

                    runtimeRestarted = await CheckRuntimeAliveAsync(channel, ct);
                    if (!runtimeRestarted)
                    {
                        // 兜底：procd 未托管（旧固件/手动启动）时 killall 后无人拉起 → init 脚本重启（兼容 S97/S99）
                        Log?.Invoke("[部署] hmi_runtime 未自动重启，尝试 /etc/init.d/S97hmiruntime restart 兜底");
                        await channel.SendCommandAsync(
                            "ls /etc/init.d/S97hmiruntime >/dev/null 2>&1 && /etc/init.d/S97hmiruntime restart || /etc/init.d/S99hmiruntime restart; echo HMI_SERVICE_DONE",
                            TimeSpan.FromSeconds(20), ct);
                        await Task.Delay(RestartProbeDelay, ct);
                        runtimeRestarted = await CheckRuntimeAliveAsync(channel, ct);
                        if (!runtimeRestarted)
                        {
                            Log?.Invoke("[部署] 警告: hmi_runtime 仍未运行，请检查设备 /etc/init.d/S97hmiruntime 或手动启动 hmi_runtime");
                        }
                    }

                    if (runtimeRestarted)
                    {
                        // 验证新 .so 已加载：宿主启动日志输出 "screens loaded: N (ui_loaded=1)"
                        try
                        {
                            var logOut = await channel.SendCommandAsync(
                                "tail -n 10 /mnt/UDISK/hmi_runtime.log 2>/dev/null || tail -n 10 /var/log/hmi_runtime.log 2>/dev/null",
                                TimeSpan.FromSeconds(10), ct);
                            var loadedLine = SplitLines(logOut)
                                .FirstOrDefault(l => l.Contains("screens loaded", StringComparison.OrdinalIgnoreCase));
                            if (loadedLine is null)
                            {
                                Log?.Invoke("[部署] hmi_runtime 已重启（未在日志中找到 screens loaded，设备日志路径可能不同）");
                            }
                            else if (loadedLine.Contains("ui_loaded=0", StringComparison.OrdinalIgnoreCase))
                            {
                                Log?.Invoke($"[部署] ⚠ hmi_runtime 已重启但新 HMI 程序加载失败（{Truncate(loadedLine, 120)}），" +
                                            "请检查 .so 与设备固件的 ABI/标签表是否匹配（设备端需同工程固件或先 OTA 宿主）");
                            }
                            else
                            {
                                Log?.Invoke($"[部署] 新 HMI 程序已加载: {Truncate(loadedLine, 160)}");
                            }
                        }
                        catch
                        {
                            // 日志验证失败不影响部署结果
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log?.Invoke($"[部署] 重启 hmi_runtime 失败: {ex.Message}");
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(sharedLibraryPath))
        {
            Log?.Invoke($"[部署] 跳过 .so 部署：文件不存在 {sharedLibraryPath}");
        }

        progress?.Report(1.0);

        return new HmiDeployResult
        {
            SourceFilesUploaded = sourceFiles.Count,
            AssetsUploaded = validAssets?.Count ?? 0,
            SoDeployed = soDeployed,
            RuntimeRestarted = runtimeRestarted,
            SharedLibraryPath = soDeployed ? sharedLibraryPath : null,
            SoDeployError = soDeployError
        };
    }

    /// <summary>killall 后等待 procd respawn / init 兜底启动的探测间隔（测试可缩短）。</summary>
    public TimeSpan RestartProbeDelay { get; set; } = TimeSpan.FromSeconds(6);

    /// <summary>探测 hmi_runtime 进程是否存活。</summary>
    private async Task<bool> CheckRuntimeAliveAsync(IDeviceChannel channel, CancellationToken ct)
    {
        // 注意：busybox pgrep 的 -x 在本设备上匹配完整命令行（含路径
        // "/mnt/UDISK/hmi/hmi_runtime"），按进程名匹配永远失败 → 误报未重启；
        // 改用无 -x 的 pgrep（匹配 comm/cmdline 子串）。
        var output = await channel.SendCommandAsync(
            "pgrep hmi_runtime >/dev/null 2>&1 && echo HMI_RESTART_OK || echo HMI_RESTART_FAIL",
            TimeSpan.FromSeconds(10), ct);
        return output.Contains("HMI_RESTART_OK", StringComparison.Ordinal);
    }

    /// <summary>
    /// 校验本地文件是否为 ARM ELF（T113 Cortex-A7 EABI5 可 dlopen）。
    /// ELF 头：0x7F 'E' 'L' 'F' + EI_CLASS=1(32位) + EI_DATA=1(小端) + e_machine(偏移18, LE) == 40 (EM_ARM)。
    /// </summary>
    public static bool IsArmElf(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            if (fs.Length < 20) return false;
            var header = new byte[20];
            fs.ReadExactly(header);
            if (header[0] != 0x7F || header[1] != (byte)'E' || header[2] != (byte)'L' || header[3] != (byte)'F')
                return false;
            if (header[4] != 1) return false; // ELFCLASS32
            if (header[5] != 1) return false; // 小端
            var machine = header[18] | (header[19] << 8);
            return machine == 40; // EM_ARM
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async Task UploadManyAsync(
        IDeviceChannel channel,
        IReadOnlyList<string> files,
        string remoteDir,
        IProgress<double>? progress,
        CancellationToken ct,
        double phaseStart,
        double phaseEnd,
        string step)
    {
        var completed = 0;
        var total = files.Count;
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            Log?.Invoke($"[部署] {step}: {fileName} → {remoteDir}/");
            var fileProgress = new Progress<double>(v =>
                progress?.Report(phaseStart + (completed + v) / total * (phaseEnd - phaseStart)));
            await channel.TransferFileAsync(file, $"{remoteDir}/{fileName}", fileProgress, ct);
            completed++;
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
