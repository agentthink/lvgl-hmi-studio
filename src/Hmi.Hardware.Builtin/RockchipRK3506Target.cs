using Hmi.Hardware.Contracts;
using HmiEditor.Core.Models;

namespace Hmi.Hardware.Builtin;

/// <summary>
/// Rockchip RK3506 硬件目标实现。
/// 480x272 RGB666, 64MB DDR2, 8MB SPI Flash。
/// </summary>
public sealed class RockchipRK3506Target : IHardwareTarget
{
    public HardwareTargetMetadata Metadata => new(
        Id: "rockchip-rk3506",
        Name: "Rockchip RK3506",
        Vendor: "Rockchip",
        Architecture: "ARM Cortex-A7",
        Version: "1.0.0"
    );

    public DisplayConfig Display => new(
        DefaultWidth: 480,
        DefaultHeight: 272,
        SupportedColorDepths: new[] { ColorDepth.RGB565 },
        Interface: DisplayInterface.RGB666,
        MaxRefreshRate: 60
    );

    // ⚠ 本组数值**未经实测核对**：仓库里既没有 RK3506 的固件镜像，也没有它的原理图/BOM，
    // 无法像 T113 那样用"硬件型号 + 真实整包体积 + 分区表"三条证据钉住（T113 的 16 MiB 错值
    // 就是靠这三条查出来的，见 AllwinnerT113Target 的注释与 ResourceBudgetTests）。
    // 在拿到该板证据之前不要把它当成已验证的预算使用。
    public ResourceConstraints Resources => new(
        FlashSizeBytes: 8 * 1024 * 1024,
        RamSizeBytes: 64 * 1024 * 1024,
        RecommendedLvglHeapBytes: 2 * 1024 * 1024,
        MaxTextureSize: 512
    );

    public LvglConfig LvglConfig => new(
        DefaultColorDepth: 16,
        DefaultMemorySize: 64,
        UseDoubleBuffering: false,
        EnabledFeatures: new[] { "LV_USE_PNG" },
        CustomDefines: new Dictionary<string, string>
        {
            ["LV_COLOR_DEPTH"] = "16",
            ["LV_MEM_SIZE"] = "0x200000"
        }
        // LvglVersion/AbiVersion **刻意不声明**：本仓库没有 RK3506 的固件与
        // hmi_ui_api.h 可对照（只有 docs 里提到"LVGL v9 on ARM"）——
        // 填一个没验证过的版本号比留空更糟。要声明，先把该板固件纳入仓库。
    );

    public ToolchainConfig Toolchain => new(
        CrossCompilerPrefix: "arm-linux-gnueabihf-",
        CompilerPath: "",
        CompileFlags: new[] { "-mcpu=cortex-a7", "-mfpu=neon-vfpv4", "-mfloat-abi=hard" },
        LinkerScriptPath: "rk3506.lds",
        LibraryDependencies: new[] { "lvgl", "m" }
    );

    public IReadOnlyList<CommunicationInterface> RecommendedInterfaces =>
        new[] { CommunicationInterface.Serial, CommunicationInterface.CanBus };

    public IReadOnlyList<string> SupportedCommunicationDrivers =>
        new[] { "modbus-rtu" };

    public ValidationResult ValidateProject(HmiProject project)
    {
        var issues = new List<ValidationIssue>();

        foreach (var screen in project.Screens)
        {
            if (screen.Width > Display.DefaultWidth || screen.Height > Display.DefaultHeight)
            {
                issues.Add(new(ValidationSeverity.Warning,
                    "RESOLUTION_EXCEEDS_DEFAULT",
                    $"Screen '{screen.Name}' 分辨率超过 RK3506 默认值",
                    new object[] { screen.Name, screen.Width, screen.Height }));
            }
        }

        // 工程 LVGL 目标 ↔ 本板运行时（未声明 LvglVersion 的目标自动跳过）
        if (TargetValidation.CheckProjectLvglVersion(LvglConfig, project.LvglVersion) is { } lvglIssue)
            issues.Add(lvglIssue);

        return new(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    public IReadOnlyDictionary<string, object> GetTemplateParameters() => new Dictionary<string, object>
    {
        ["target_id"] = Metadata.Id,
        ["color_depth"] = LvglConfig.DefaultColorDepth,
        ["lvgl_heap_size"] = Resources.RecommendedLvglHeapBytes,
        ["cross_compiler_prefix"] = Toolchain.CrossCompilerPrefix
    };

    public string GenerateInitializationCode(HmiProject project) =>
        "// RK3506 display init\n" +
        "rk_display_init();\n" +
        "lv_init();\n" +
        "lv_display_create(480, 272);\n";
}
