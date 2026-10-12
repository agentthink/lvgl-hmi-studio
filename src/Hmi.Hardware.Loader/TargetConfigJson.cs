using System.Text.Json.Serialization;
using Hmi.Hardware.Contracts;

namespace Hmi.Hardware.Loader;

/// <summary>
/// JSON 配置文件的数据模型 —— 对应 *.target.json 文件结构。
/// 反序列化后被 ConfigDrivenTarget 包装为 IHardwareTarget。
/// </summary>
public class TargetConfigJson
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("vendor")]
    public string Vendor { get; set; } = "Generic";

    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("display")]
    public DisplayConfigJson Display { get; set; } = new();

    [JsonPropertyName("resources")]
    public ResourceConstraintsJson Resources { get; set; } = new();

    [JsonPropertyName("lvgl")]
    public LvglConfigJson Lvgl { get; set; } = new();

    [JsonPropertyName("toolchain")]
    public ToolchainConfigJson Toolchain { get; set; } = new();

    [JsonPropertyName("communicationInterfaces")]
    public List<string> CommunicationInterfaces { get; set; } = new();

    [JsonPropertyName("communicationDrivers")]
    public List<string> CommunicationDrivers { get; set; } = new();
}

public class DisplayConfigJson
{
    [JsonPropertyName("defaultWidth")]
    public int DefaultWidth { get; set; } = 800;

    [JsonPropertyName("defaultHeight")]
    public int DefaultHeight { get; set; } = 480;

    [JsonPropertyName("colorDepths")]
    public List<string> ColorDepths { get; set; } = new() { "RGB565", "ARGB8888" };

    [JsonPropertyName("interface")]
    public string Interface { get; set; } = "RGB666";

    [JsonPropertyName("maxRefreshRate")]
    public int MaxRefreshRate { get; set; } = 60;
}

public class ResourceConstraintsJson
{
    [JsonPropertyName("flashSizeBytes")]
    public long FlashSizeBytes { get; set; } = 8 * 1024 * 1024;

    [JsonPropertyName("ramSizeBytes")]
    public long RamSizeBytes { get; set; } = 64 * 1024 * 1024;

    [JsonPropertyName("recommendedLvglHeapBytes")]
    public long RecommendedLvglHeapBytes { get; set; } = 2 * 1024 * 1024;

    [JsonPropertyName("maxTextureSize")]
    public int MaxTextureSize { get; set; } = 512;
}

public class LvglConfigJson
{
    [JsonPropertyName("defaultColorDepth")]
    public int DefaultColorDepth { get; set; } = 16;

    [JsonPropertyName("defaultMemorySize")]
    public int DefaultMemorySize { get; set; } = 64;

    [JsonPropertyName("useDoubleBuffering")]
    public bool UseDoubleBuffering { get; set; }

    [JsonPropertyName("enabledFeatures")]
    public List<string> EnabledFeatures { get; set; } = new();

    [JsonPropertyName("customDefines")]
    public Dictionary<string, string> CustomDefines { get; set; } = new();
}

public class ToolchainConfigJson
{
    [JsonPropertyName("crossCompilerPrefix")]
    public string CrossCompilerPrefix { get; set; } = "arm-none-eabi-";

    [JsonPropertyName("compilerPath")]
    public string CompilerPath { get; set; } = "";

    [JsonPropertyName("compileFlags")]
    public List<string> CompileFlags { get; set; } = new();

    [JsonPropertyName("linkerScriptPath")]
    public string LinkerScriptPath { get; set; } = "";

    [JsonPropertyName("libraryDependencies")]
    public List<string> LibraryDependencies { get; set; } = new();
}
