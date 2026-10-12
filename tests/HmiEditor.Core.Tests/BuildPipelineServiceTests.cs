using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 行为测试：编译/导出流水线（<see cref="BuildPipelineService"/>）。
///
/// 该服务从 MainWindow 抽出（原 ExportCodeCore / CompileProjectCore /
/// DownloadToHmiCore 前半段 + 三个产物状态字段）。抽出后可以脱离 UI 直接测试：
/// 用临时目录 + 宿主回调桩驱动，断言"生成文件落到正确位置、产物清单正确、
/// 状态与审计回调被触发、工具链缺失时降级不抛异常"。
/// </summary>
public class BuildPipelineServiceTests
{
    private static HmiProject SimpleProject()
    {
        var project = new HmiProject { Name = "Pipeline", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "s1", Name = "主屏", Width = 800, Height = 480 };
        scr.Controls.Add(new HmiControl
        {
            Id = "l1", Name = "标签", Type = HmiControlType.Label,
            X = 10, Y = 10, Width = 120, Height = 30, Text = "Hello",
        });
        project.Screens.Add(scr);
        return project;
    }

    private sealed class Harness
    {
        public List<string> Statuses { get; } = new();
        public List<string> CompileStatuses { get; } = new();
        public List<int> BarValues { get; } = new();
        public List<bool> Busy { get; } = new();
        public List<bool> DownloadEnabled { get; } = new();
        public List<(string Message, bool Success)> Audits { get; } = new();
        public BuildPipelineService Service { get; }

        public Harness(HmiProject project, string outputDir)
        {
            Service = new BuildPipelineService
            {
                GetProject = () => project,
                GetAssetLibrary = () => new AssetLibrary(),
                GetProjectDirectory = () => outputDir,
                SetStatus = Statuses.Add,
                PostStatus = _ => { },
                SetCompileBarVisible = _ => { },
                SetCompileBarValue = BarValues.Add,
                SetCompileStatusText = CompileStatuses.Add,
                SetCompileBusy = Busy.Add,
                SetDownloadEnabled = DownloadEnabled.Add,
                PickExportDirectory = () => Task.FromResult<string?>(outputDir),
                AuditExport = (message, success) => Audits.Add((message, success)),
                // 故意不给工具链：交叉编译应优雅降级而不是抛异常
                GetT113SdkRoot = () => null,
                GetT113ToolchainGcc = () => null,
            };
        }
    }

    [Fact]
    public async Task ExportAsync_WritesGeneratedCode_AndAuditsSuccess()
    {
        var project = SimpleProject();
        // 置空输出目录 → 走「弹目录选择」回退分支（由 PickExportDirectory 提供）
        project.ExportConfig.OutputDirectory = "";
        var tmp = TestTempDir.NewDir("hmi-export");
        try
        {
            var h = new Harness(project, tmp);
            await h.Service.ExportAsync();

            Assert.True(File.Exists(Path.Combine(tmp, "generated_hmi.c")), "未生成 generated_hmi.c");
            Assert.Contains(h.Audits, a => a.Success);
            Assert.Contains(h.Statuses, s => s.Contains("generated_hmi.c"));
        }
        finally
        {
            TestTempDir.DeleteDir(tmp);
        }
    }

    [Fact]
    public async Task CompileAsync_CollectsOutputFiles_AndTogglesUi()
    {
        var project = SimpleProject();
        var tmp = TestTempDir.NewDir("hmi-out");
        project.ExportConfig.OutputDirectory = tmp;
        try
        {
            var h = new Harness(project, tmp);
            await h.Service.CompileAsync();

            // 产物状态由服务独占，且记录到输出目录
            Assert.Equal(tmp, h.Service.CompiledOutputDir);
            Assert.Contains(h.Service.CompiledOutputFiles,
                f => Path.GetFileName(f) == "generated_hmi.c");
            Assert.All(h.Service.CompiledOutputFiles, f => Assert.True(File.Exists(f), $"产物不存在: {f}"));

            // UI 契约：进度从 0 走到 100，编译中置忙后恢复，成功后解锁下载
            Assert.Equal(0, h.BarValues.First());
            Assert.Equal(100, h.BarValues.Last());
            Assert.Equal(new[] { true, false }, h.Busy);
            Assert.Equal(new[] { false, true }, h.DownloadEnabled);
            Assert.Contains(h.Audits, a => a.Success);

            // 状态栏依次经过各阶段（工具链是否存在取决于环境，不做环境相关断言）
            Assert.NotEmpty(h.CompileStatuses);
            Assert.Contains(h.CompileStatuses, s => s.Contains("交叉编译") || s.Contains("生成代码"));
        }
        finally
        {
            TestTempDir.DeleteDir(tmp);
        }
    }

    [Fact]
    public async Task EnsureSharedLibraryArtifacts_SharedLibraryMode_IsNoop()
    {
        var project = SimpleProject();
        project.ExportConfig.OutputMode = OutputMode.SharedLibrary;
        var tmp = TestTempDir.NewDir("hmi-deploy");
        try
        {
            var h = new Harness(project, tmp);
            var ok = await h.Service.EnsureSharedLibraryArtifactsAsync();

            Assert.True(ok);
            Assert.Empty(h.Service.CompiledOutputFiles);   // SharedLibrary 模式无需重建产物
        }
        finally
        {
            TestTempDir.DeleteDir(tmp);
        }
    }

    [Fact]
    public async Task EnsureSharedLibraryArtifacts_ReusesExistingSoArtifacts()
    {
        var project = SimpleProject();   // 默认 Executable 模式 → 需要 SharedLibrary 变体
        var tmp = TestTempDir.NewDir("hmi-deploy2");
        try
        {
            File.WriteAllText(Path.Combine(tmp, "generated_hmi_so.c"), "/* so */");
            File.WriteAllText(Path.Combine(tmp, "libhmi_ui.so"), "not-a-real-so");

            var h = new Harness(project, tmp);
            // 通过编译一次让服务记住输出目录，再清空产物清单模拟"旧产物"场景
            project.ExportConfig.OutputDirectory = tmp;
            await h.Service.CompileAsync();

            var ok = await h.Service.EnsureSharedLibraryArtifactsAsync();

            Assert.True(ok);
            Assert.Contains(h.Service.CompiledOutputFiles,
                f => Path.GetFileName(f) == "generated_hmi_so.c");
            Assert.Contains(h.Service.CompiledOutputFiles,
                f => Path.GetFileName(f) == "libhmi_ui.so");
        }
        finally
        {
            TestTempDir.DeleteDir(tmp);
        }
    }
}
