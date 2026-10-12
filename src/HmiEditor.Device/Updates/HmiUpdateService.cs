using System.Text.RegularExpressions;
using HmiEditor.Device.Channels;
using HmiEditor.Device.Detection;
using HmiEditor.Device.Firmware;
using HmiEditor.Device.Abstractions;

namespace HmiEditor.Device.Updates;

/// <summary>版本比对结果类型。</summary>
public enum UpdateCheckResultKind
{
    /// <summary>设备已是最新版本。</summary>
    UpToDate,

    /// <summary>设备存在可更新的新版本。</summary>
    UpdateAvailable,

    /// <summary>设备版本比本地新（或本地未提供版本）。</summary>
    DeviceNewer,

    /// <summary>设备未返回版本信息，无法判断。</summary>
    Unknown
}

/// <summary>版本检查结果。</summary>
public sealed class UpdateCheckResult
{
    public required UpdateCheckResultKind Kind { get; init; }
    public string LocalVersion { get; init; } = "";
    public string DeviceVersion { get; init; } = "";

    /// <summary>检测到的设备信息。</summary>
    public DeviceInfo DeviceInfo { get; init; } = new();

    public bool ShouldUpdate => Kind == UpdateCheckResultKind.UpdateAvailable;
}

/// <summary>
/// HMI 程序版本检查与自动更新机制：
///   检测 → 版本比对 → （调用方确认后）上传 .swu 并触发 OTA → 验证。
/// </summary>
public sealed class HmiUpdateService : IHmiUpdateService
{
    private readonly DeviceDetector _detector = new();
    private readonly DeviceFlasher _flasher = new();

    /// <summary>操作日志。</summary>
    public event Action<string>? Log
    {
        add { _flasher.Log += value; }
        remove { _flasher.Log -= value; }
    }

    /// <summary>
    /// 检查设备端 HMI 程序版本与本地版本的关系。
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(
        IDeviceChannel channel,
        string localVersion,
        CancellationToken ct = default)
    {
        var info = await _detector.DetectAsync(channel, ct);
        var deviceVersion = info.HmiVersion;
        var result = CheckForUpdate(localVersion, deviceVersion);
        return new UpdateCheckResult
        {
            Kind = result.Kind,
            LocalVersion = result.LocalVersion,
            DeviceVersion = result.DeviceVersion,
            DeviceInfo = info
        };
    }

    /// <summary>
    /// 执行 HMI 程序更新：上传本地 .swu → 触发 OTA → 验证结果。
    /// </summary>
    /// <returns>更新是否成功触发。</returns>
    public async Task<bool> UpdateAsync(
        IDeviceChannel channel,
        string localSwuPath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            await _flasher.FlashAsync(channel, new OtaFlashOptions
            {
                Target = OtaTarget.Hmi,
                SwuPath = localSwuPath,
                RemotePath = "/tmp/hmi_update.swu"
            }, progress, ct);
            return true;
        }
        catch (DeviceChannelException)
        {
            return false;
        }
    }

    /// <summary>
    /// 比较本地与设备端 HMI 版本，判定是否需要更新。
    /// 版本号经 <see cref="NormalizeVersion"/> 归一化（提取语义化数字段）后逐段比较。
    /// </summary>
    public static UpdateCheckResult CheckForUpdate(string localVersion, string? deviceVersion)
    {
        var local = NormalizeVersion(localVersion);
        var device = NormalizeVersion(deviceVersion ?? "");

        if (string.IsNullOrEmpty(device))
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.Unknown,
                LocalVersion = local,
                DeviceVersion = ""
            };
        }

        if (string.IsNullOrEmpty(local))
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.DeviceNewer,
                LocalVersion = "",
                DeviceVersion = device
            };
        }

        var cmp = CompareVersions(local, device);
        var kind = cmp switch
        {
            > 0 => UpdateCheckResultKind.DeviceNewer,
            0 => UpdateCheckResultKind.UpToDate,
            _ => UpdateCheckResultKind.UpdateAvailable
        };

        return new UpdateCheckResult
        {
            Kind = kind,
            LocalVersion = local,
            DeviceVersion = device
        };
    }

    /// <summary>归一化版本：提取形如 "2.1.0" / "v2.1.0" / "2.1.0-rc1" 的语义化版本段。</summary>
    internal static string NormalizeVersion(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var match = Regex.Match(raw.Trim(), @"(?i)\bv?\s*(\d+\.\d+(?:\.\d+)?(?:[.-][0-9A-Za-z]+)?)");
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    /// <summary>逐数字段比较主版本：2.1.0 与 2.1.0-rc1 同版本；2.1.0 &lt; 2.10.0。</summary>
    internal static int CompareVersions(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
        if (string.IsNullOrEmpty(a)) return -1;
        if (string.IsNullOrEmpty(b)) return 1;

        // 只取主版本（2.1.0），忽略预发布/构建后缀（-rc1 / +build42）
        var pa = SplitNumeric(MajorVersion(a));
        var pb = SplitNumeric(MajorVersion(b));
        var count = Math.Max(pa.Count, pb.Count);
        for (var i = 0; i < count; i++)
        {
            var x = i < pa.Count ? pa[i] : 0;
            var y = i < pb.Count ? pb[i] : 0;
            if (x != y) return x.CompareTo(y);
        }
        return 0;
    }

    private static string MajorVersion(string version)
    {
        var match = Regex.Match(version, @"^\d+(?:\.\d+)+");
        return match.Success ? match.Value : version;
    }

    private static List<long> SplitNumeric(string version)
    {
        var list = new List<long>();
        foreach (Match m in Regex.Matches(version, @"\d+"))
        {
            if (long.TryParse(m.Value, out var n))
                list.Add(n);
        }
        return list;
    }
}
