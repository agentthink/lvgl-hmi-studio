using System.Text.RegularExpressions;
using HmiEditor.Device.Channels;
using HmiEditor.Device.Abstractions;

namespace HmiEditor.Device.Detection;

/// <summary>
/// 设备信息 —— 硬件型号与版本检测结果。
/// </summary>
public sealed class DeviceInfo
{
    /// <summary>硬件型号（如 "T113-MQ-R"）。</summary>
    public string Model { get; set; } = "";

    /// <summary>芯片架构（如 "armv7l"）。</summary>
    public string Architecture { get; set; } = "";

    /// <summary>内核版本。</summary>
    public string KernelVersion { get; set; } = "";

    /// <summary>固件版本（rootfs / 系统版本）。</summary>
    public string FirmwareVersion { get; set; } = "";

    /// <summary>HMI 应用版本（设备上已安装的 HMI 程序版本）。</summary>
    public string HmiVersion { get; set; } = "";

    /// <summary>通道名（信息来源）。</summary>
    public string ChannelName { get; set; } = "";

    /// <summary>是否检测到有效设备（至少识别出型号或 HMI 版本）。</summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Model) ||
        !string.IsNullOrWhiteSpace(HmiVersion) ||
        !string.IsNullOrWhiteSpace(FirmwareVersion) ||
        !string.IsNullOrWhiteSpace(KernelVersion);

    public override string ToString()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Model)) parts.Add(Model);
        if (!string.IsNullOrWhiteSpace(HmiVersion)) parts.Add($"HMI v{HmiVersion}");
        if (!string.IsNullOrWhiteSpace(FirmwareVersion)) parts.Add($"FW {FirmwareVersion}");
        if (!string.IsNullOrWhiteSpace(KernelVersion)) parts.Add($"Kernel {KernelVersion}");
        return parts.Count > 0 ? string.Join(" / ", parts) : "未知设备";
    }
}

/// <summary>
/// 设备检测器 —— 通过通道执行探测命令，识别硬件型号、固件版本与 HMI 程序版本。
/// 所有命令均容错执行，单条失败不阻断整体检测。
/// </summary>
public sealed class DeviceDetector : IDeviceDetector
{
    /// <summary>单条探测命令超时。</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(6);

    /// <summary>
    /// 探测指定通道上的设备。
    /// </summary>
    public async Task<DeviceInfo> DetectAsync(IDeviceChannel channel, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (!channel.IsOpen)
            await channel.OpenAsync(ct);

        var info = new DeviceInfo { ChannelName = channel.Name };

        // 1. 硬件型号：设备树 model 节点
        var model = await SafeCommandAsync(channel, "cat /proc/device-tree/model", ct);
        info.Model = Clean(model);

        // 2. 架构与内核
        info.Architecture = Clean(await SafeCommandAsync(channel, "uname -m", ct));
        info.KernelVersion = Clean(await SafeCommandAsync(channel, "uname -r", ct));

        // 3. 固件版本（os-release）
        var osRelease = await SafeCommandAsync(channel, "cat /etc/os-release", ct);
        info.FirmwareVersion = ParseOsReleaseVersion(osRelease);

        // 4. HMI 应用版本（优先 UDISK 上已部署的 HMI 程序）
        var hmiVersionRaw = await SafeCommandAsync(
            channel,
            "cat /mnt/UDISK/hmi/version 2>/dev/null; cat /etc/hmi_version 2>/dev/null; cat /usr/share/hmi/version 2>/dev/null",
            ct);
        info.HmiVersion = ParseHmiVersion(hmiVersionRaw);

        return info;
    }

    private async Task<string> SafeCommandAsync(IDeviceChannel channel, string command, CancellationToken ct)
    {
        try
        {
            return await channel.SendCommandAsync(command, CommandTimeout, ct);
        }
        catch (DeviceChannelException)
        {
            return "";
        }
        catch (OperationCanceledException)
        {
            return "";
        }
    }

    /// <summary>清理 busybox cat 输出（\0 结尾、多余空白）。</summary>
    private static string Clean(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        // 设备树节点以 \0 结尾，busybox cat 可能带出
        var text = raw.Trim('\0', '\r', '\n', ' ', '\t');
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    internal static string ParseOsReleaseVersion(string osRelease)
    {
        if (string.IsNullOrWhiteSpace(osRelease)) return "";
        foreach (var line in osRelease.Split('\n'))
        {
            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim();
            if (key is "VERSION_ID" or "VERSION" or "BUILD_ID")
            {
                var value = parts[1].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        return "";
    }

    /// <summary>从 version 文件内容解析 HMI 版本号（支持 "2.1.0"、"v2.1.0"、"2.1.0 (build ...)"）。</summary>
    internal static string ParseHmiVersion(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var text = raw.Trim();
        var match = Regex.Match(text, @"(?i)\bv?\s*(\d+\.\d+(?:\.\d+)?(?:[.-][0-9A-Za-z]+)?)");
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }
}
