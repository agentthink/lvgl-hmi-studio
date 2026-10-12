namespace Hmi.Hardware.Contracts;

public record ToolchainConfig(
    string CrossCompilerPrefix,
    string CompilerPath,
    IReadOnlyList<string> CompileFlags,
    string LinkerScriptPath,
    IReadOnlyList<string> LibraryDependencies
);
