using Hmi.Hardware.Contracts;
using HmiEditor.Core.Models;

namespace Hmi.Hardware.Builtin;

/// <summary>
/// Allwinner T113-S3 硬件目标实现。
/// 1024x600 RGB666, 128MB DDR3, 128MB SPI NAND。
/// </summary>
public sealed class AllwinnerT113Target : IHardwareTarget
{
    public HardwareTargetMetadata Metadata => new(
        Id: "allwinner-t113",
        Name: "Allwinner T113-S3",
        Vendor: "Allwinner",
        Architecture: "ARM Cortex-A7",
        Version: "1.0.0"
    );

    public DisplayConfig Display => new(
        DefaultWidth: 1024,
        DefaultHeight: 600,
        SupportedColorDepths: new[] { ColorDepth.RGB565, ColorDepth.ARGB8888 },
        Interface: DisplayInterface.RGB666,
        MaxRefreshRate: 60
    );

    // Flash: W25N01GVZEIG = 1 Gbit **SPI NAND = 128 MiB**（不是 16 MiB —— 那是 2026-09-18
    // 之前写在这里的错值，同族错值还抄进了 firmware/t113/README.md 与 version-info.md）。
    // 三条独立证据：
    //   ① 原理图 BOM `hardware/hmi-control-board-t113/lceda-import/hmi-board.csv` U8 = W25N01GVZEIG；
    //      板级 README 写明「SPI NAND 128MB，启动盘」；
    //   ② 真实整包 `out/t113-mq_r/tina_t113-mq_r_uart1.img` = 57,490,432 B（≈54.8 MiB）——
    //      16 MiB 根本装不下，128 MiB 装得下；
    //   ③ 分区表 `device/config/chips/t113/configs/mq_r/sys_partition.fex` 固定分区合计
    //      186,178 扇区（≈90.9 MiB）+ mbr 16,384 扇区，剩余即 UDISK（≈30.5 MiB）。
    // 判据（防再漂）：ResourceBudgetTests 用 BOM/README 的型号与容量文本对回本常量。
    public ResourceConstraints Resources => new(
        FlashSizeBytes: 128L * 1024 * 1024,
        RamSizeBytes: 128 * 1024 * 1024,
        RecommendedLvglHeapBytes: 4 * 1024 * 1024,
        MaxTextureSize: 1024
    );

    public LvglConfig LvglConfig => new(
        DefaultColorDepth: 32,
        DefaultMemorySize: 128,
        UseDoubleBuffering: true,
        EnabledFeatures: new[] { "LV_USE_FREETYPE", "LV_USE_PNG" },
        CustomDefines: new Dictionary<string, string>
        {
            ["LV_COLOR_DEPTH"] = "32",
            ["LV_MEM_SIZE"] = "0x400000"
        },
        LvglVersion: "9.5",
        AbiVersion: 3u
    );

    public ToolchainConfig Toolchain => new(
        CrossCompilerPrefix: "arm-linux-gnueabihf-",
        CompilerPath: "",
        CompileFlags: new[] { "-mcpu=cortex-a7", "-mfpu=neon-vfpv4", "-mfloat-abi=hard" },
        LinkerScriptPath: "t113-s3.ld",
        LibraryDependencies: new[] { "lvgl", "m", "dl" }
    );

    public IReadOnlyList<CommunicationInterface> RecommendedInterfaces =>
        new[] { CommunicationInterface.Serial, CommunicationInterface.Ethernet, CommunicationInterface.CanBus, CommunicationInterface.WiFi };

    public IReadOnlyList<string> SupportedCommunicationDrivers =>
        new[] { "modbus-rtu", "modbus-tcp", "mqtt" };

    public ValidationResult ValidateProject(HmiProject project)
    {
        var issues = new List<ValidationIssue>();

        foreach (var screen in project.Screens)
        {
            if (screen.Width > Display.DefaultWidth || screen.Height > Display.DefaultHeight)
            {
                issues.Add(new(ValidationSeverity.Warning,
                    "RESOLUTION_EXCEEDS_DEFAULT",
                    $"Screen '{screen.Name}' ({screen.Width}x{screen.Height}) 超过默认分辨率",
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
        "// T113-S3 display init\n" +
        "sunxi_display_init();\n" +
        "lv_init();\n" +
        "lv_display_create(1024, 600);\n";
}
