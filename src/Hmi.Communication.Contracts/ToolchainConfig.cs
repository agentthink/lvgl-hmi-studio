namespace Hmi.Communication.Contracts;

/// <summary>
/// 工具链配置（供驱动生成代码时使用）。
/// 从 Hmi.Hardware.Contracts.ToolchainConfig 映射而来。
/// </summary>
public record ToolchainConfig(
    string CrossCompilerPrefix,
    string CompilerPath,
    IReadOnlyList<string> CompileFlags,
    string LinkerScriptPath,
    IReadOnlyList<string> LibraryDependencies
);
