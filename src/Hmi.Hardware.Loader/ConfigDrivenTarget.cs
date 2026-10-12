using Hmi.Hardware.Contracts;
using HmiEditor.Core.Models;

namespace Hmi.Hardware.Loader;

/// <summary>
/// JSON 配置适配器 —— 将 TargetConfigJson 包装为 IHardwareTarget 实现。
/// 适用于通过 JSON 配置文件定义的轻量级硬件目标（Tier 3）。
/// ValidateProject 使用通用规则（分辨率/色深检查），不支持自定义初始化代码生成。
/// </summary>
public sealed class ConfigDrivenTarget : IHardwareTarget
{
    private readonly TargetConfigJson _config;

    public ConfigDrivenTarget(TargetConfigJson config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public HardwareTargetMetadata Metadata => new(
        Id: _config.Id,
        Name: _config.Name,
        Vendor: _config.Vendor,
        Architecture: _config.Architecture,
        Version: _config.Version
    );

    public DisplayConfig Display => new(
        DefaultWidth: _config.Display.DefaultWidth,
        DefaultHeight: _config.Display.DefaultHeight,
        SupportedColorDepths: _config.Display.ColorDepths
            .Select(ParseColorDepth)
            .ToList(),
        Interface: ParseDisplayInterface(_config.Display.Interface),
        MaxRefreshRate: _config.Display.MaxRefreshRate
    );

    public ResourceConstraints Resources => new(
        FlashSizeBytes: _config.Resources.FlashSizeBytes,
        RamSizeBytes: _config.Resources.RamSizeBytes,
        RecommendedLvglHeapBytes: _config.Resources.RecommendedLvglHeapBytes,
        MaxTextureSize: _config.Resources.MaxTextureSize
    );

    public LvglConfig LvglConfig => new(
        DefaultColorDepth: _config.Lvgl.DefaultColorDepth,
        DefaultMemorySize: _config.Lvgl.DefaultMemorySize,
        UseDoubleBuffering: _config.Lvgl.UseDoubleBuffering,
        EnabledFeatures: _config.Lvgl.EnabledFeatures,
        CustomDefines: _config.Lvgl.CustomDefines
    );

    public ToolchainConfig Toolchain => new(
        CrossCompilerPrefix: _config.Toolchain.CrossCompilerPrefix,
        CompilerPath: _config.Toolchain.CompilerPath,
        CompileFlags: _config.Toolchain.CompileFlags,
        LinkerScriptPath: _config.Toolchain.LinkerScriptPath,
        LibraryDependencies: _config.Toolchain.LibraryDependencies
    );

    public IReadOnlyList<CommunicationInterface> RecommendedInterfaces =>
        _config.CommunicationInterfaces
            .Select(ParseCommunicationInterface)
            .ToList();

    public IReadOnlyList<string> SupportedCommunicationDrivers =>
        _config.CommunicationDrivers;

    /// <summary>
    /// 通用项目验证 —— 仅检查分辨率是否超过默认值。
    /// JSON 配置目标不支持自定义验证逻辑。
    /// </summary>
    public ValidationResult ValidateProject(HmiProject project)
    {
        var issues = new List<ValidationIssue>();

        foreach (var screen in project.Screens)
        {
            if (screen.Width > Display.DefaultWidth || screen.Height > Display.DefaultHeight)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "RESOLUTION_EXCEEDS_DEFAULT",
                    $"Screen '{screen.Name}' ({screen.Width}x{screen.Height}) " +
                    $"exceeds default resolution ({Display.DefaultWidth}x{Display.DefaultHeight})"));
            }
        }

        return new ValidationResult(issues.All(i => i.Severity != ValidationSeverity.Error), issues);
    }

    /// <summary>
    /// JSON 配置目标的模板参数（包含 id、色深、堆大小、编译器前缀）。
    /// </summary>
    public IReadOnlyDictionary<string, object> GetTemplateParameters() =>
        new Dictionary<string, object>
        {
            ["target_id"] = Metadata.Id,
            ["target_name"] = Metadata.Name,
            ["color_depth"] = LvglConfig.DefaultColorDepth,
            ["lvgl_heap_size"] = Resources.RecommendedLvglHeapBytes,
            ["cross_compiler_prefix"] = Toolchain.CrossCompilerPrefix,
            ["display_width"] = Display.DefaultWidth,
            ["display_height"] = Display.DefaultHeight,
        };

    /// <summary>
    /// JSON 配置目标不支持自定义初始化代码生成。
    /// 返回通用占位符。
    /// </summary>
    public string GenerateInitializationCode(HmiProject project) =>
        $"// {Metadata.Name} ({Metadata.Id}) — JSON config target\n" +
        $"// Display: {Display.DefaultWidth}x{Display.DefaultHeight}\n" +
        $"// LVGL color depth: {LvglConfig.DefaultColorDepth}\n" +
        $"lv_init();\n" +
        $"lv_display_create({Display.DefaultWidth}, {Display.DefaultHeight});\n";

    // ── 解析辅助 ──

    /// <summary>
    /// JSON 里的色深名 → <see cref="ColorDepth"/> 枚举。
    /// <para>
    /// 走 <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/> + <see cref="Enum.IsDefined{TEnum}(TEnum)"/>
    /// 而不是手写 switch：手写清单在枚举增删成员时会静默过期
    /// （新增成员永远解析不出来，直接落到兜底），而且它是同一份信息的第 N 个副本。
    /// </para>
    /// <para>
    /// <see cref="Enum.IsDefined{TEnum}(TEnum)"/> 不可省：<c>TryParse</c> 对数字串也返回 true，
    /// <c>"99"</c> 会变成一个未定义的枚举值。
    /// </para>
    /// </summary>
    private static ColorDepth ParseColorDepth(string value) =>
        Enum.TryParse<ColorDepth>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : ColorDepth.RGB565;

    /// <summary>JSON 里的显示接口名 → <see cref="DisplayInterface"/> 枚举。语义同 <see cref="ParseColorDepth"/>。</summary>
    private static DisplayInterface ParseDisplayInterface(string value) =>
        Enum.TryParse<DisplayInterface>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : DisplayInterface.RGB666;

    /// <summary>JSON 里的通讯接口名 → <see cref="CommunicationInterface"/> 枚举。语义同 <see cref="ParseColorDepth"/>。</summary>
    private static CommunicationInterface ParseCommunicationInterface(string value) =>
        Enum.TryParse<CommunicationInterface>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : CommunicationInterface.Serial;
}
