using System.Text;
using HmiEditor.Device.Abstractions;

namespace HmiEditor.Device.Packaging;

/// <summary>USB 更新包生成选项。</summary>
public sealed class UsbUpdateOptions
{
    /// <summary>输出目录（U 盘 / TF 卡挂载点或普通文件夹）。</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>HMI 程序版本（用于文件名与 sw-description version 字段）。</summary>
    public string HmiVersion { get; init; } = "1.0.0";

    /// <summary>
    /// 是否在 sw-description 中写入 sha256 校验值。
    /// 默认 false：兼容未启用 CONFIG_HASH_VERIFY 的 swupdate（如 T113 当前固件），
    /// 校验由 CPIO checksum（swupdate 强制）承担；设为 true 生成带 sha256 的标准包。
    /// </summary>
    public bool IncludeHashes { get; init; }

    /// <summary>
    /// 是否在更新包内附带 version 文件（写入设备 /mnt/UDISK/hmi/version，
    /// 供设备检测 HMI 程序版本）。默认 true。
    /// </summary>
    public bool IncludeVersionFile { get; init; } = true;

    /// <summary>libhmi_ui.so 本地路径。提供时生成 hmi-&lt;version&gt;.swu。</summary>
    public string? HmiLibraryPath { get; init; }

    /// <summary>固件版本（用于文件名与 sw-description version 字段）。</summary>
    public string FirmwareVersion { get; init; } = "1.0.0";

    /// <summary>
    /// 固件镜像列表（CPIO 名 → 本地镜像路径）。提供时生成 firmware-&lt;version&gt;.swu。
    /// CPIO 名需为 SwuTemplates.FirmwareImageNames 中的名称（recovery/uboot/boot0/kernel/rootfs）。
    /// </summary>
    public IReadOnlyDictionary<string, string>? FirmwareImages { get; init; }
}

/// <summary>USB 更新包生成结果。</summary>
public sealed class UsbUpdatePackageResult
{
    public required string OutputDirectory { get; init; }
    public string? HmiSwuPath { get; init; }
    public string? FirmwareSwuPath { get; init; }
    public required string AutoInstallScriptPath { get; init; }
    public required string ReadmePath { get; init; }
    public IReadOnlyList<string> GeneratedFiles { get; init; } = [];
}

/// <summary>
/// HMI 程序 USB 更新文件生成工具 —— 根据当前 HMI 程序版本生成符合设备要求的 USB 更新包：
///   1) <c>hmi-&lt;ver&gt;.swu</c> / <c>firmware-&lt;ver&gt;.swu</c>（swupdate 升级包，含 SHA256 校验）
///   2) <c>auto_install.sh</c> 自动安装引导程序（busybox 兼容，插入 U 盘/TF 卡后一键刷写）
///   3) <c>README.txt</c> 使用说明
/// 生成的 .swu 可直接通过 hmi_ota.sh（usb/tf/net 三通道）或设备端 swupdate 安装。
/// </summary>
public sealed class UsbUpdatePackager : IUsbUpdatePackager
{
    private readonly SwuPackageBuilder _swuBuilder = new();

    public async Task<UsbUpdatePackageResult> BuildAsync(UsbUpdateOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var output = options.OutputDirectory;
        if (string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("输出目录不能为空", nameof(options));

        Directory.CreateDirectory(output);
        var generated = new List<string>();

        // 1. HMI 程序包
        string? hmiSwu = null;
        if (!string.IsNullOrEmpty(options.HmiLibraryPath))
        {
            if (!File.Exists(options.HmiLibraryPath))
                throw new FileNotFoundException($"HMI 程序文件不存在: {options.HmiLibraryPath}");
            hmiSwu = Path.Combine(output, $"hmi-{options.HmiVersion}.swu");
            var payloads = new List<SwuPayloadFile>
            {
                new() { CpioName = "libhmi_ui.so", LocalPath = options.HmiLibraryPath }
            };
            if (options.IncludeVersionFile)
            {
                payloads.Add(new() { CpioName = "version", Data = Encoding.UTF8.GetBytes(options.HmiVersion + "\n") });
            }
            await _swuBuilder.BuildAsync(
                options.IncludeHashes ? SwuTemplates.HmiUi : SwuTemplates.HmiUiNoHash,
                payloads,
                hmiSwu,
                options.HmiVersion,
                ct);
            generated.Add(hmiSwu);
        }

        // 2. 整固件包
        string? fwSwu = null;
        if (options.FirmwareImages is { Count: > 0 })
        {
            // 校验镜像名
            foreach (var (cpioName, path) in options.FirmwareImages)
            {
                if (!SwuTemplates.FirmwareImageNames.Contains(cpioName, StringComparer.Ordinal))
                    throw new ArgumentException($"固件镜像名 '{cpioName}' 无效（应为 {string.Join("/", SwuTemplates.FirmwareImageNames)}）");
                if (!File.Exists(path))
                    throw new FileNotFoundException($"固件镜像不存在: {path}");
            }

            fwSwu = Path.Combine(output, $"firmware-{options.FirmwareVersion}.swu");
            var payloads = SwuTemplates.FirmwareImageNames
                .Where(options.FirmwareImages.ContainsKey)
                .Select(name => new SwuPayloadFile { CpioName = name, LocalPath = options.FirmwareImages[name] })
                .ToArray();
            await _swuBuilder.BuildAsync(SwuTemplates.FirmwareUbi, payloads, fwSwu, options.FirmwareVersion, ct);
            generated.Add(fwSwu);
        }

        if (hmiSwu is null && fwSwu is null)
            throw new ArgumentException("至少需提供 HmiLibraryPath 或 FirmwareImages 之一", nameof(options));

        // 3. 自动安装引导脚本
        var scriptPath = Path.Combine(output, "auto_install.sh");
        await File.WriteAllTextAsync(scriptPath, BuildAutoInstallScript(), Encoding.UTF8, ct);
        generated.Add(scriptPath);

        // 4. 使用说明
        var readmePath = Path.Combine(output, "README.txt");
        await File.WriteAllTextAsync(readmePath, BuildReadme(options), Encoding.UTF8, ct);
        generated.Add(readmePath);

        return new UsbUpdatePackageResult
        {
            OutputDirectory = output,
            HmiSwuPath = hmiSwu,
            FirmwareSwuPath = fwSwu,
            AutoInstallScriptPath = scriptPath,
            ReadmePath = readmePath,
            GeneratedFiles = generated
        };
    }

    /// <summary>
    /// 自动安装引导程序（busybox 兼容：无 head/tail，用 sed 替代）。
    /// 先刷 HMI（重启 hmi_runtime），再刷固件（三阶段 + 系统 reboot），固件放最后。
    /// </summary>
    private static string BuildAutoInstallScript()
    {
        return """"
            #!/bin/sh
            # ============================================================================
            # zhishu-HMI USB / TF 卡自动更新引导程序
            #
            # 用法（设备端 root 执行）:
            #   sh /mnt/usb/auto_install.sh        # U 盘
            #   sh /mnt/SDCARD/auto_install.sh     # TF 卡
            #
            # 规则:
            #   优先调用 hmi_ota.sh（三通道 OTA 统一脚本）；
            #   若不存在则直接调用 swupdate 兜底。
            #   先安装 hmi-*.swu（HMI 程序，重启 hmi_runtime），
            #   再安装 firmware-*.swu（整固件三阶段，系统 reboot）。
            # ============================================================================
            set -e

            MNT=$(cd "$(dirname "$0")" && pwd)

            run_swu() {
                TARGET=$1
                FILE=$2
                echo "[AUTO] 安装 $TARGET: $FILE"
                if command -v hmi_ota.sh >/dev/null 2>&1; then
                    hmi_ota.sh "$TARGET" usb "$FILE"
                else
                    MODE=upgrade_recovery
                    [ "$TARGET" = "hmi" ] && MODE=upgrade_hmi_ui
                    swupdate -v -i "$FILE" -e "stable,$MODE"
                fi
            }

            HMI=$(ls "$MNT"/hmi-*.swu 2>/dev/null | sed -n '1p')
            FW=$(ls "$MNT"/firmware-*.swu 2>/dev/null | sed -n '1p')

            if [ -z "$HMI$FW" ]; then
                echo "[AUTO] 未找到 .swu 更新包（期望 hmi-*.swu 或 firmware-*.swu）"
                exit 1
            fi

            [ -n "$HMI" ] && run_swu hmi "$HMI"
            [ -n "$FW" ] && run_swu fw "$FW"

            echo "[AUTO] 更新流程结束，HMI 程序已重启 / 固件已进入升级流程"
            """";
    }

    private static string BuildReadme(UsbUpdateOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("zhishu-HMI USB 更新包");
        sb.AppendLine("====================");
        sb.AppendLine();
        sb.AppendLine($"HMI 程序版本: {options.HmiVersion}");
        sb.AppendLine($"固件版本:     {options.FirmwareVersion}");
        sb.AppendLine();
        sb.AppendLine("文件清单:");
        if (!string.IsNullOrEmpty(options.HmiLibraryPath))
            sb.AppendLine($"  hmi-{options.HmiVersion}.swu        HMI 程序升级包（含 SHA256 校验）");
        if (options.FirmwareImages is { Count: > 0 })
            sb.AppendLine($"  firmware-{options.FirmwareVersion}.swu  整固件升级包（recovery/kernel/usr 三阶段）");
        sb.AppendLine("  auto_install.sh      自动安装引导程序");
        sb.AppendLine("  README.txt           本说明");
        sb.AppendLine();
        sb.AppendLine("使用方法:");
        sb.AppendLine("  1. 将本目录所有文件复制到 U 盘（FAT32）根目录或 TF 卡根目录；");
        sb.AppendLine("  2. 将 U 盘/TF 卡插入设备，以 root 登录设备控制台；");
        sb.AppendLine("  3. 执行: sh /mnt/usb/auto_install.sh（U 盘）或 sh /mnt/SDCARD/auto_install.sh（TF 卡）；");
        sb.AppendLine("  4. 等待提示安装完成；HMI 程序更新会重启 hmi_runtime，");
        sb.AppendLine("     整固件更新会自动重启设备（三阶段升级）。");
        sb.AppendLine();
        sb.AppendLine("高级用法（网络通道）:");
        sb.AppendLine("  将 .swu 放到 HTTP 服务器，在设备端执行:");
        sb.AppendLine($"    hmi_ota.sh hmi net http://<ip>/hmi-{options.HmiVersion}.swu");
        sb.AppendLine($"    hmi_ota.sh fw  net http://<ip>/firmware-{options.FirmwareVersion}.swu");
        return sb.ToString();
    }
}
