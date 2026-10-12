using System.Diagnostics;
using HmiEditor.Device.Build;
using HmiEditor.Device.Channels;
using HmiEditor.Device.Abstractions;

namespace HmiEditor.Device.Firmware;

/// <summary>OTA 更新目标类型。</summary>
public enum OtaTarget
{
    /// <summary>仅 HMI 程序（libhmi_ui.so）：单步 raw 写入 + hmi_runtime 重启。</summary>
    Hmi,

    /// <summary>整固件：recovery→kernel→usr 三阶段 + 系统 reboot。</summary>
    Firmware
}

/// <summary>OTA 刷写参数。</summary>
public sealed class OtaFlashOptions
{
    /// <summary>更新目标。</summary>
    public OtaTarget Target { get; init; } = OtaTarget.Hmi;

    /// <summary>.swu 本地路径。</summary>
    public required string SwuPath { get; init; }

    /// <summary>上传到设备的临时路径（绝对路径，直接供 hmi_ota.sh 使用，无需挂载）。</summary>
    public string RemotePath { get; init; } = "/tmp/hmi_update.swu";

    /// <summary>上传超时（整体）。</summary>
    public TimeSpan UploadTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>OTA 执行超时（设备端 swupdate 运行时间）。</summary>
    public TimeSpan OtaTimeout { get; init; } = TimeSpan.FromMinutes(3);
}

/// <summary>FEL 镜像烧录参数（PC 端通过 USB 直接操作设备 FEL 模式）。</summary>
public sealed class FelFlashOptions
{
    /// <summary>sunxi-fel 可执行文件路径。</summary>
    public string ToolPath { get; init; } = "sunxi-fel";

    /// <summary>待烧录镜像路径。</summary>
    public required string ImagePath { get; init; }

    /// <summary>
    /// 命令行模板，<c>{image}</c> 会被替换为镜像路径。
    /// 默认 SPI NOR：<c>-p spiflash-write 0 {image}</c>。
    /// SPI NAND 场景请改用自定义命令（如先 sunxi-fel 加载 uboot 再写入 NAND 分区）。
    /// </summary>
    public string ArgumentsTemplate { get; init; } = "-p spiflash-write 0 {image}";

    public string? WorkingDirectory { get; init; }
}

/// <summary>FEL 烧录结果。</summary>
public sealed class FelFlashResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string Output { get; init; } = "";
}

/// <summary>
/// 固件/HMI 刷写器：
///   OTA 通道 —— 上传 .swu 至设备并触发 hmi_ota.sh（三通道统一 OTA 脚本）；
///   FEL 通道 —— 设备 FEL 模式下由 PC 端 sunxi-fel 直接烧录镜像。
/// 全程通过 <see cref="Log"/> 事件输出操作日志。
/// </summary>
public sealed class DeviceFlasher : IDeviceFlasher
{
    /// <summary>操作日志（每条一行，供 UI 展示/持久化）。</summary>
    public event Action<string>? Log;

    /// <summary>
    /// 执行 OTA 刷写：本地 .swu → 上传设备 → 触发 hmi_ota.sh &lt;fw|hmi&gt; usb &lt;path&gt;。
    /// </summary>
    public async Task FlashAsync(
        IDeviceChannel channel,
        OtaFlashOptions options,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(options);
        if (!File.Exists(options.SwuPath))
            throw new FileNotFoundException($"更新包不存在: {options.SwuPath}");

        var targetName = options.Target == OtaTarget.Hmi ? "HMI 程序" : "整固件";
        Log?.Invoke($"[刷写] 目标: {targetName}, 更新包: {options.SwuPath}");

        if (!channel.IsOpen)
        {
            Log?.Invoke($"[刷写] 打开通道 {channel.Name} ...");
            await channel.OpenAsync(ct);
        }

        // 1. 上传 .swu
        Log?.Invoke($"[刷写] 上传更新包至 {options.RemotePath} ...");
        try
        {
            await channel.TransferFileAsync(options.SwuPath, options.RemotePath, progress, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DeviceChannelException($"上传更新包失败: {ex.Message}", ex);
        }
        Log?.Invoke("[刷写] 上传完成");

        // 2. 预创建 raw 安装目标文件（swupdate raw handler 使用 open(O_RDWR) 无 O_CREAT，
        //    目标文件必须已存在，否则 ENOENT）
        await PrepareRawTargetsAsync(channel, options, ct);

        // 3. 触发 OTA
        var script = options.Target == OtaTarget.Hmi ? "hmi" : "fw";
        var command = $"hmi_ota.sh {script} usb {options.RemotePath}";
        Log?.Invoke($"[刷写] 执行: {command}");

        string output;
        try
        {
            output = await channel.SendCommandAsync(command, options.OtaTimeout, ct);
        }
        catch (DeviceChannelException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new DeviceChannelException("OTA 执行超时，请检查设备状态（固件三阶段更新可能需数分钟）");
        }

        Log?.Invoke(output.Length > 0 ? output : "[刷写] (无输出)");

        // 3. hmi_ota.sh 缺失或脚本本身有语法错误（rootfs 只读无法修复）→ 直接调用 swupdate 兜底
        if (IsScriptBroken(output))
        {
            Log?.Invoke("[刷写] hmi_ota.sh 不可用，改用 swupdate 直接触发");
            var mode = options.Target == OtaTarget.Hmi ? "upgrade_hmi_ui" : "upgrade_recovery";
            var fallback = $"swupdate -v -i {options.RemotePath} -e stable,{mode}";
            try
            {
                output = await channel.SendCommandAsync(fallback, options.OtaTimeout, ct);
                Log?.Invoke(output.Length > 0 ? output : "[刷写] (swupdate 无输出)");
            }
            catch (OperationCanceledException)
            {
                throw new DeviceChannelException("OTA 执行超时，请检查设备状态（固件三阶段更新可能需数分钟）");
            }
        }

        // 4. 结果验证（swupdate 失败会输出 [ERROR]）
        var lower = output.ToLowerInvariant();
        // 成功标志优先：bootloader env 不可用等场景会输出 [ERROR] 噪音但不影响主流程
        if (lower.Contains("swupdate successful") || lower.Contains("updated successfully"))
        {
            Log?.Invoke($"[刷写] {targetName}更新已触发成功");
            await RestartHmiRuntimeIfNeededAsync(channel, options, ct);
            return;
        }
        if (lower.Contains("[error]") || lower.Contains("fail") || lower.Contains("not found") ||
            lower.Contains("cannot") || lower.Contains("no such") || lower.Contains("syntax error"))
        {
            throw new DeviceChannelException($"OTA 执行失败: {Truncate(output, 300)}");
        }

        Log?.Invoke($"[刷写] {targetName}更新已触发成功");
        await RestartHmiRuntimeIfNeededAsync(channel, options, ct);
    }

    /// <summary>
    /// HMI 更新完成后重启 hmi_runtime：bootenv 不可用的固件中 swupdate 不会触发
    /// swu_next=hmi_restart，手动 killall 由 procd（S97hmiruntime）自动拉起并加载新 .so。
    /// </summary>
    private async Task RestartHmiRuntimeIfNeededAsync(IDeviceChannel channel, OtaFlashOptions options, CancellationToken ct)
    {
        if (options.Target != OtaTarget.Hmi) return;
        Log?.Invoke("[刷写] 重启 hmi_runtime 以加载新 HMI 程序");
        await channel.SendCommandAsync(
            "killall hmi_runtime 2>/dev/null; echo HMI_RESTART_OK",
            TimeSpan.FromSeconds(10), ct);
    }

    /// <summary>预创建 HMI 更新的 raw 安装目标目录与文件（/mnt/UDISK/hmi）。</summary>
    private async Task PrepareRawTargetsAsync(IDeviceChannel channel, OtaFlashOptions options, CancellationToken ct)
    {
        if (options.Target != OtaTarget.Hmi) return;
        Log?.Invoke("[刷写] 预创建 /mnt/UDISK/hmi 目标目录与文件");
        await channel.SendCommandAsync(
            "mkdir -p /mnt/UDISK/hmi && touch /mnt/UDISK/hmi/libhmi_ui.so /mnt/UDISK/hmi/version",
            TimeSpan.FromSeconds(5), ct);
    }

    /// <summary>hmi_ota.sh 不可用（命令不存在 / 脚本语法错误）的判定。</summary>
    private static bool IsScriptBroken(string output)
    {
        var lower = output.ToLowerInvariant();
        return lower.Contains("syntax error") ||
               lower.Contains("not found") ||
               lower.Contains("unknown target");
    }

    /// <summary>
    /// 通过 sunxi-fel 在设备 FEL 模式烧录镜像（PC 端 USB 直连）。
    /// </summary>
    public async Task<FelFlashResult> FelFlashAsync(FelFlashOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!File.Exists(options.ImagePath))
            throw new FileNotFoundException($"镜像不存在: {options.ImagePath}");

        Log?.Invoke($"[FEL] 工具: {options.ToolPath}, 镜像: {options.ImagePath}");
        var tokens = Tokenize(options.ArgumentsTemplate, options.ImagePath);
        Log?.Invoke($"[FEL] 命令: {options.ToolPath} {string.Join(' ', tokens)}");

        var psi = new ProcessStartInfo
        {
            FileName = options.ToolPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = options.WorkingDirectory ?? Environment.CurrentDirectory
        };
        foreach (var token in tokens)
            psi.ArgumentList.Add(token);

        using var process = new Process { StartInfo = psi };
        try
        {
            if (!process.Start())
                throw new DeviceChannelException("sunxi-fel 进程启动失败");
        }
        catch (Exception ex) when (ex is not DeviceChannelException)
        {
            throw new DeviceChannelException($"无法启动 sunxi-fel（{options.ToolPath}），请确认已安装 sunxi-tools: {ex.Message}", ex);
        }

        var output = new System.Text.StringBuilder();
        var stdout = ReadAllAsync(process.StandardOutput, output, Log, ct);
        var stderr = ReadAllAsync(process.StandardError, output, Log, ct);

        // 取消时**真的杀掉**子进程（sunxi-fel 会继续写 Flash）
        await ChildProcessRunner.WaitForExitOrKillAsync(process, ct);
        await Task.WhenAll(stdout, stderr);

        var result = new FelFlashResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Output = output.ToString()
        };
        Log?.Invoke(result.Success
            ? $"[FEL] 烧录完成 (exit {result.ExitCode})"
            : $"[FEL] 烧录失败 (exit {result.ExitCode}): {Truncate(result.Output, 300)}");
        return result;
    }

    private static async Task ReadAllAsync(
        StreamReader reader,
        System.Text.StringBuilder output,
        Action<string>? log,
        CancellationToken ct)
    {
        try
        {
            while (await reader.ReadLineAsync(ct) is { } line)
            {
                output.AppendLine(line);
                log?.Invoke($"[FEL] {line}");
            }
        }
        catch (OperationCanceledException)
        {
            // 取消：由调用方处理
        }
    }

    private static string[] Tokenize(string template, string imagePath)
    {
        var tokens = new List<string>();
        foreach (var token in template.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            tokens.Add(token == "{image}" ? imagePath : token);
        return tokens.ToArray();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
