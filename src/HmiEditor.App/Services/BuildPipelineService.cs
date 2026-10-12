using HmiEditor.App.Resources;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.Device.Build;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HmiEditor.App.Services;

/// <summary>
/// 编译/导出流水线 —— 「导出代码」「编译转换」以及「下载前的 .so 产物准备」。
///
/// 从 MainWindow 抽出（原先 <c>ExportCodeCore</c> / <c>CompileProjectCore</c> /
/// <c>DownloadToHmiCore</c> 的前半段 + 三个成员字段，约 280 行）：
/// 这些逻辑与主窗口的 UI 状态强耦合（进度条、按钮可用性、状态栏），
/// 但业务本身是纯粹的「解析输出目录 → 生成代码 → 写盘 → 交叉编译」。
/// 抽出后宿主只需注入回调，主窗口不再持有编译产物状态。
///
/// 产物状态（<see cref="CompiledOutputDir"/> / <see cref="CompiledOutputFiles"/> /
/// <see cref="LastSoBuildError"/>）由本服务独占，避免散落在窗口字段里。
/// </summary>
public sealed class BuildPipelineService
{
    // ── 产物状态 ──

    /// <summary>最近一次编译转换的输出目录（下载对话框据此定位产物）。</summary>
    public string CompiledOutputDir { get; private set; } = "";

    /// <summary>最近一次编译转换的产物文件清单。</summary>
    public List<string> CompiledOutputFiles { get; } = new();

    /// <summary>最近一次 .so 交叉编译错误（null 表示工具链缺失而非编译失败）。</summary>
    public string? LastSoBuildError { get; private set; }

    // ── 宿主回调 ──

    public required Func<HmiProject?> GetProject { get; init; }
    public required Func<AssetLibrary> GetAssetLibrary { get; init; }
    public required Func<string?> GetProjectDirectory { get; init; }

    /// <summary>写状态栏文本。</summary>
    public required Action<string> SetStatus { get; init; }

    /// <summary>跨线程写状态栏（交叉编译在后台线程回调，需 Post 回 UI 线程）。</summary>
    public required Action<string> PostStatus { get; init; }

    public required Action<bool> SetCompileBarVisible { get; init; }
    public required Action<int> SetCompileBarValue { get; init; }
    public required Action<string> SetCompileStatusText { get; init; }

    /// <summary>编译进行中：禁用/恢复编译与下载入口。</summary>
    public required Action<bool> SetCompileBusy { get; init; }

    /// <summary>是否允许下载（编译成功后启用）。</summary>
    public required Action<bool> SetDownloadEnabled { get; init; }

    /// <summary>选择导出目录；用户取消返回 null。</summary>
    public required Func<Task<string?>> PickExportDirectory { get; init; }

    /// <summary>审计记录（消息, 是否成功）。</summary>
    public required Action<string, bool> AuditExport { get; init; }

    public required Func<string?> GetT113SdkRoot { get; init; }
    public required Func<string?> GetT113ToolchainGcc { get; init; }

    private static LocalizationService Loc => AppLocalization.Service;

    /// <summary>导出 C/C++ 源码 + 构建文件（输出目录来自 ExportConfig，否则弹目录选择）。</summary>
    public async Task ExportAsync()
    {
        var project = GetProject();
        if (project == null) return;

        // ExportConfig.OutputDirectory 配置驱动：非空且工程已保存则直接用（自动建目录），否则回退目录选择对话框
        var targetDir = project.ExportConfig.ResolveOutputDirectory(GetProjectDirectory());
        if (targetDir == null)
        {
            targetDir = await PickExportDirectory();
            if (targetDir == null) return;
        }
        else
        {
            Directory.CreateDirectory(targetDir);
        }

        try
        {
            var generator = new CodeGenerator();
            // ExportConfig.OutputMode 决定生成可执行文件（含 main）或动态库（.so，供宿主 dlopen 热加载）
            var code = project.ExportConfig.OutputMode == OutputMode.SharedLibrary
                ? generator.GenerateSharedLibraryProject(project, GetAssetLibrary())
                : generator.GenerateFullProject(project, GetAssetLibrary());
            // ExportConfig.TargetLanguage 决定文件扩展名（C→.c / Cpp→.cpp）
            var ext = project.ExportConfig.TargetLanguage == TargetLanguage.Cpp ? "cpp" : "c";
            var outputPath = Path.Combine(targetDir, $"generated_hmi.{ext}");
            await File.WriteAllTextAsync(outputPath, code);

            // 按 ExportConfig.GenerateBuildSystem 额外生成构建文件（CMake/Makefile/PlatformIO）
            var buildArtifact = generator.GenerateBuildSystem(project);
            if (buildArtifact != null)
            {
                var buildPath = Path.Combine(targetDir, buildArtifact.FileName);
                await File.WriteAllTextAsync(buildPath, buildArtifact.Content);
            }

            SetStatus(Loc["project.status.exported"] + $": {outputPath}");
            AuditExport(Loc.Format("audit.export.c.code", outputPath), true);
        }
        catch (Exception ex)
        {
            SetStatus(UiOperationGuard.FailureDetail(Lang.UiOpExportCode, targetDir, ex));
            AuditExport(UiOperationGuard.FailureDetail(Lang.UiOpExportCode, targetDir, ex), false);
        }
    }

    /// <summary>
    /// 编译转换 —— 生成可部署源码工程（libhmi_ui.so 源码 + 构建文件）并交叉编译 .so。
    /// 展示进度与结果状态；成功后启用「下载到HMI」。
    /// </summary>
    public async Task CompileAsync()
    {
        var project = GetProject();
        if (project == null) return;

        SetCompileBarVisible(true);
        SetCompileBarValue(0);
        SetCompileStatusText(Loc["compile.progress.start"]);
        SetStatus(Loc["compile.progress.start"]);
        SetCompileBusy(true);
        SetDownloadEnabled(false);

        // 目标目录在 try 内才确定 ⇒ 用外层承接变量交给 catch（阶段 86：#61b「带上对象」）
        string? targetDir = null;
        try
        {
            // 1. 解析输出目录：优先 ExportConfig.OutputDirectory（工程已保存），否则回退到应用可清理临时根
            targetDir = project.ExportConfig.ResolveOutputDirectory(GetProjectDirectory());
            if (string.IsNullOrEmpty(targetDir))
                targetDir = AppPaths.TempSubdir("compile");
            Directory.CreateDirectory(targetDir);

            // 2. 生成源码
            SetCompileBarValue(20);
            SetCompileStatusText(Loc["compile.progress.generating"]);
            var generator = new CodeGenerator();
            var code = project.ExportConfig.OutputMode == OutputMode.SharedLibrary
                ? generator.GenerateSharedLibraryProject(project, GetAssetLibrary())
                : generator.GenerateFullProject(project, GetAssetLibrary());
            var ext = project.ExportConfig.TargetLanguage == TargetLanguage.Cpp ? "cpp" : "c";
            var outputPath = Path.Combine(targetDir, $"generated_hmi.{ext}");

            // 3. 保存源码 + 构建文件
            SetCompileBarValue(60);
            SetCompileStatusText(Loc["compile.progress.saving"]);
            await File.WriteAllTextAsync(outputPath, code);
            var buildArtifact = generator.GenerateBuildSystem(project);
            if (buildArtifact != null)
                await File.WriteAllTextAsync(Path.Combine(targetDir, buildArtifact.FileName), buildArtifact.Content);

            // 4. 记录产物，解锁下载
            CompiledOutputDir = targetDir;
            CompiledOutputFiles.Clear();
            CompiledOutputFiles.Add(outputPath);
            if (buildArtifact != null)
                CompiledOutputFiles.Add(Path.Combine(targetDir, buildArtifact.FileName));

            // 5. 交叉编译 libhmi_ui.so（T113 真机部署；工具链缺失时仅提示，不影响源码生成）。
            //    无论导出模式都产出 .so：Executable 模式的产物含 main() 不适用于 .so 架构，
            //    故额外生成 SharedLibrary 变体（generated_hmi_so.c）用于交叉编译，
            //    与「下载到HMI」强制重建行为一致。
            SetCompileBarValue(80);
            SetCompileStatusText(Loc["compile.so.building"]);
            var soSourcePath = outputPath;
            if (project.ExportConfig.OutputMode != OutputMode.SharedLibrary)
            {
                var soCode = generator.GenerateSharedLibraryProject(project, GetAssetLibrary());
                var soVariantPath = Path.Combine(targetDir, "generated_hmi_so.c");
                await File.WriteAllTextAsync(soVariantPath, soCode);
                CompiledOutputFiles.Add(soVariantPath);
                soSourcePath = soVariantPath;
            }
            var soPath = await BuildDeviceSharedLibraryAsync(project, soSourcePath, targetDir);
            if (soPath != null)
            {
                CompiledOutputFiles.Add(soPath);
                SetCompileStatusText(Loc.Format("compile.so.success", Path.GetFileName(soPath)));
            }
            else
            {
                SetCompileStatusText(!string.IsNullOrEmpty(LastSoBuildError)
                    ? Loc.Format("compile.so.failed", LastSoBuildError)
                    : Loc["compile.so.toolchain.missing"]);
            }

            SetCompileBarValue(100);
            SetStatus(Loc.Format("compile.output", targetDir));
            SetDownloadEnabled(true);
            AuditExport(Loc.Format("audit.export.c.code", outputPath), true);
        }
        catch (Exception ex)
        {
            SetCompileBarValue(0);
            // 状态栏用「编译工程」（与工具栏命令、MainWindow 的同一操作 catch 同名）；审计沿用
            // 「导出代码」（与成功行 audit.export.c.code 同口径）—— 两个通道各按自己那半的措辞。
            var reason = UiOperationGuard.FailureDetail(Lang.UiOpCompileProject, targetDir, ex);
            SetCompileStatusText(reason);
            SetStatus(reason);
            AuditExport(UiOperationGuard.FailureDetail(Lang.UiOpExportCode, targetDir, ex), false);
        }
        finally
        {
            SetCompileBusy(false);
        }
    }

    /// <summary>
    /// 下载前确保存在可部署的 libhmi_ui.so 产物：
    /// 已有 SharedLibrary 产物则复用；否则以 SharedLibrary 重新生成并交叉编译。
    /// 返回 false 表示硬失败（调用方应中止下载）。
    /// </summary>
    public async Task<bool> EnsureSharedLibraryArtifactsAsync()
    {
        var project = GetProject();
        if (project == null) return false;
        if (project.ExportConfig.OutputMode == OutputMode.SharedLibrary) return true;

        // 编译转换已生成 SharedLibrary 变体（generated_hmi_so.c）与 libhmi_ui.so → 直接复用
        var soVariantPath = Path.Combine(CompiledOutputDir, "generated_hmi_so.c");
        var existingSo = Path.Combine(CompiledOutputDir, "libhmi_ui.so");
        if (File.Exists(soVariantPath) && File.Exists(existingSo))
        {
            CompiledOutputFiles.Clear();
            CompiledOutputFiles.Add(soVariantPath);
            CompiledOutputFiles.Add(existingSo);
            SetStatus(Loc.Format("download.auto.sharedlibrary", project.ExportConfig.OutputMode));
            return true;
        }

        // 兜底：强制以 SharedLibrary 重新生成并交叉编译（旧产物或无产物场景）
        // 目标目录在 try 内才确定 ⇒ 外层承接变量交给 catch（阶段 102：#61「带上对象」）
        string? dir = null;
        try
        {
            var generator = new CodeGenerator();
            var code = generator.GenerateSharedLibraryProject(project, GetAssetLibrary());
            dir = AppPaths.TempSubdir("download");
            Directory.CreateDirectory(dir);
            var outPath = Path.Combine(dir, "generated_hmi.c");
            File.WriteAllText(outPath, code);
            CompiledOutputFiles.Clear();
            CompiledOutputFiles.Add(outPath);
            var buildArtifact = generator.GenerateBuildSystem(project);
            if (buildArtifact != null)
            {
                var buildPath = Path.Combine(dir, buildArtifact.FileName);
                File.WriteAllText(buildPath, buildArtifact.Content);
                CompiledOutputFiles.Add(buildPath);
            }
            CompiledOutputDir = dir;
            SetStatus(Loc.Format("download.auto.sharedlibrary", project.ExportConfig.OutputMode));

            // 同步交叉编译 libhmi_ui.so（下载对话框将自动检测到产物）
            var soPath = await BuildDeviceSharedLibraryAsync(project, outPath, dir);
            if (soPath != null)
            {
                CompiledOutputFiles.Add(soPath);
                SetStatus(Loc.Format("compile.so.success", Path.GetFileName(soPath)));
            }
            else if (!string.IsNullOrEmpty(LastSoBuildError))
            {
                SetStatus(Loc.Format("compile.so.failed", LastSoBuildError));
            }
            return true;
        }
        catch (Exception ex)
        {
            // 承接变量可能仍为 null（TempSubdir 自己就抛了）⇒ 出口自动回落 2 实参版
            SetStatus(UiOperationGuard.FailureDetail(Lang.UiOpPrepareSharedLib, dir, ex));
            return false;
        }
    }

    /// <summary>
    /// 交叉编译生成 libhmi_ui.so：
    ///   1) 导出工程标签契约头 tags.h（TAG_* 枚举与宿主一致，.so 才能编译/运行）；
    ///   2) 用 T113 工具链把 SharedLibrary 源码编译为 libhmi_ui.so（写回 targetDir）。
    /// 成功返回 .so 路径；工具链缺失或编译失败返回 null 并记录原因（不抛异常）。
    /// </summary>
    private async Task<string?> BuildDeviceSharedLibraryAsync(HmiProject project, string sourcePath, string targetDir)
    {
        LastSoBuildError = null;
        try
        {
            var tagsHeader = Path.Combine(targetDir, "tags.h");
            var bindingEngine = new RuntimeBindingEngine(project);
            bindingEngine.BuildRuntimeTags();
            await File.WriteAllTextAsync(tagsHeader,
                new RuntimeTagsCodeGen().GenerateHeader(bindingEngine.Tags));

            var builder = new T113SoBuilder();
            builder.Log += msg => PostStatus(msg);
            var result = await builder.BuildAsync(new T113SoBuildOptions
            {
                SourcePath = sourcePath,
                OutputDir = targetDir,
                TagsHeaderPath = tagsHeader,
                SdkRoot = GetT113SdkRoot(),
                ToolchainGcc = GetT113ToolchainGcc()
            });
            LastSoBuildError = result.Error;
            return result.Success ? result.OutputPath : null;
        }
        catch (Exception ex)
        {
            LastSoBuildError = ex.Message;
            return null;
        }
    }
}
