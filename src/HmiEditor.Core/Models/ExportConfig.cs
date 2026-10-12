using System.IO;

namespace HmiEditor.Core.Models;

/// <summary>
/// 导出目标语言。
/// </summary>
public enum TargetLanguage
{
    /// <summary>C 语言。</summary>
    C,

    /// <summary>C++ 语言。</summary>
    Cpp,
}

/// <summary>
/// 代码输出模式 —— 决定生成可执行文件还是动态库。
/// </summary>
public enum OutputMode
{
    /// <summary>可执行文件模式：生成含 main() 的完整程序（PC 模拟 / 独立固件）。</summary>
    Executable,

    /// <summary>动态库模式：生成 libhmi_ui.so 代码（无 main，导出 hmi_ui_get_api，供宿主 dlopen 热加载）。</summary>
    SharedLibrary,
}

/// <summary>
/// C 代码风格 —— 影响缩进、花括号位置、命名风格。
/// </summary>
public enum CodeStyle
{
    /// <summary>GNU 99 风格。</summary>
    Gnu99,

    /// <summary>K&amp;R 风格。</summary>
    KAndR,

    /// <summary>Allman 风格。</summary>
    Allman,

    /// <summary>Whitesmiths 风格。</summary>
    Whitesmiths,
}

/// <summary>
/// 资源命名风格 —— 导出时图片/字体/字符串的命名规范。
/// </summary>
public enum ResourceNaming
{
    /// <summary>snake_case 命名。</summary>
    SnakeCase,

    /// <summary>PascalCase 命名。</summary>
    PascalCase,

    /// <summary>匈牙利命名法。</summary>
    Hungarian,
}

/// <summary>
/// 构建系统 —— 是否同时生成构建系统配置文件。
/// </summary>
public enum BuildSystem
{
    /// <summary>不生成构建配置。</summary>
    None,

    /// <summary>生成 CMakeLists.txt。</summary>
    CMake,

    /// <summary>生成 Makefile。</summary>
    Makefile,

    /// <summary>生成 PlatformIO 配置。</summary>
    PlatformIO,
}

/// <summary>
/// 代码生成优化策略。
/// </summary>
public enum CodeGenOptimization
{
    /// <summary>体积优先 —— 精简代码，适合 Flash 紧张的场景。</summary>
    Size,

    /// <summary>速度优先 —— 内联展开、减少函数调用开销。</summary>
    Speed,

    /// <summary>可读性优先 —— 保留注释、宏名称清晰。</summary>
    Readability,
}

/// <summary>
/// 代码导出配置 —— 导出 C/C++ 代码时的生成策略。
/// 包括语言、输出路径、命名规范、构建系统等选项。
/// </summary>
public class ExportConfig
{
    /// <summary>
    /// 导出目标语言：C 或 C++。
    /// </summary>
    public TargetLanguage TargetLanguage { get; set; } = TargetLanguage.C;

    /// <summary>
    /// 导出输出根目录（相对/绝对路径均可）。
    /// </summary>
    public string OutputDirectory { get; set; } = "./export";

    /// <summary>
    /// 解析导出目录的绝对路径。
    /// 相对路径基于工程目录解析；绝对路径直接使用。
    /// </summary>
    /// <param name="projectDirectory">工程所在目录（未保存工程传 null）。</param>
    /// <returns>解析后的绝对路径；若 <see cref="OutputDirectory"/> 未配置或工程目录未知则返回 null（调用方应回退到目录选择对话框）。</returns>
    public string? ResolveOutputDirectory(string? projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(OutputDirectory) || string.IsNullOrEmpty(projectDirectory))
            return null;
        return Path.IsPathRooted(OutputDirectory)
            ? OutputDirectory
            : Path.Combine(projectDirectory, OutputDirectory);
    }

    /// <summary>
    /// 是否在生成的构建配置里追加调试符号开关（<c>-g</c>），供 gdb / addr2line 反查崩溃栈。
    /// <para>
    /// 注意：本项只影响<b>构建配置</b>（CMakeLists.txt / Makefile / platformio.ini）中的编译选项，
    /// 不改变 <c>generated_hmi.c</c> 的任何一行。默认 <c>false</c> ——
    /// 关闭时生成的构建配置与不含该选项时逐字节相同。
    /// </para>
    /// <para>
    /// T113 是 ARM Linux（openixSuit 烧录 / ADB 热更新），调试走 gdb 或 core dump，
    /// 与 J-Link/ST-Link 这类 SWD 探针无关。
    /// </para>
    /// </summary>
    public bool GenerateDebugSymbols { get; set; }

    /// <summary>
    /// C 代码风格，影响缩进、花括号位置、命名风格。
    /// </summary>
    public CodeStyle CodeStyle { get; set; } = CodeStyle.Gnu99;

    /// <summary>
    /// 导出时资源（图片/字体/字符串）的命名风格。
    /// </summary>
    public ResourceNaming ResourceNaming { get; set; } = ResourceNaming.SnakeCase;

    /// <summary>
    /// 是否同时生成构建系统配置文件。
    /// </summary>
    public BuildSystem GenerateBuildSystem { get; set; } = BuildSystem.CMake;

    /// <summary>
    /// 导出代码中是否包含中文注释（控件绑定、变量来源等标注）。
    /// </summary>
    public bool IncludeComments { get; set; } = true;

    /// <summary>
    /// 是否将所有图片/字体合并为单一 .bin 文件，节省 Flash 扇区。
    /// </summary>
    public bool MergeResources { get; set; } = true;

    /// <summary>
    /// 代码生成优化策略：体积优先 / 速度优先 / 可读性优先。
    /// </summary>
    public CodeGenOptimization CodeGenOptimization { get; set; } = CodeGenOptimization.Size;

    /// <summary>
    /// 是否在生成代码头部包含 LGPL 许可证声明（LVGL 要求）。
    /// </summary>
    public bool IncludeLgplDisclaimer { get; set; } = true;

    /// <summary>
    /// 代码输出模式：可执行文件（含 main）或动态库（.so，供宿主 dlopen 热加载）。
    /// 动态库模式用于 T113 等嵌入式固件的 HMI UI OTA 热更新场景。
    /// </summary>
    public OutputMode OutputMode { get; set; } = OutputMode.Executable;
}