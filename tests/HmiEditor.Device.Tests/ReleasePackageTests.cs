using Hmi.Hardware.Contracts;
using Hmi.Hardware.Builtin;
using HmiEditor.Core.Services;
using HmiEditor.Device.Release;
using Xunit;

namespace HmiEditor.Device.Tests;

/// <summary>
/// **编辑器发布包**（登记表 §4 第 29 项"发布包 / 升级回滚"）的可执行判据。
///
/// <para>这一层要钉住三件事：</para>
/// <list type="number">
///   <item><b>版本清单真的来自事实源</b> —— 包内清单的固件侧四个值 == 固件头文件，
///         编辑器侧两个值 == 硬件目标插件，且两侧互相一致（这就是"四者对应"落到发布物上）；</item>
///   <item><b>封条能失败</b> —— 改一个字节 / 删一个文件 / 换一份源码状态，自检都必须点名报出来；</item>
///   <item><b>说明里的链接不悬空</b> —— 升级回滚指南引用的固件文档确实随包发出。</item>
/// </list>
/// </summary>
public class ReleasePackageTests
{
    private static readonly AllwinnerT113Target T113 = new();

    private static ReleaseAssemblyOptions Options(string stage, string repoRoot, IHardwareTarget? target = null) => new(
        RepoRoot: repoRoot,
        StageDir: stage,
        Target: target ?? T113,
        EditorVersion: "1.2.3",
        EditorGitCommit: "abc1234",
        Configuration: "Release",
        Runtime: "linux-x64");

    /// <summary>造一个"已经 publish 完"的最小包目录：editor/ 里有产物即可。</summary>
    private static string NewStage()
    {
        var stage = TestTempDir.NewDir("release-stage");
        var editor = Path.Combine(stage, ReleasePackageAssembler.EditorDirName);
        Directory.CreateDirectory(editor);
        File.WriteAllText(Path.Combine(editor, "HmiEditor.App"), "fake editor\n");
        return stage;
    }

    private static string NewFakeRepo(string? versionString = null)
    {
        var repo = TestTempDir.NewDir("release-repo");
        var runtimeDir = Path.Combine(repo, "firmware", "t113", "app", "runtime");
        Directory.CreateDirectory(runtimeDir);
        File.WriteAllText(Path.Combine(runtimeDir, "hmi_runtime.h"), $"""
            #define HMI_RUNTIME_VERSION_MAJOR 9
            #define HMI_RUNTIME_VERSION_MINOR 9
            #define HMI_RUNTIME_VERSION_PATCH 9
            #define HMI_RUNTIME_VERSION_STR "{versionString ?? "9.9.9"}"
            #define HMI_TARGET_ID "allwinner-t113"
            """);
        File.WriteAllText(Path.Combine(runtimeDir, "hmi_ui_api.h"), """
            #define HMI_UI_ABI_VERSION 3u
            #define HMI_UI_LVGL_VERSION 0x905
            """);
        return repo;
    }

    [Fact]
    public void VersionTable_MatchesTheFourVersionMarkers()
    {
        var repo = SourceScanner.RepoRoot;
        var stage = NewStage();
        try
        {
            var result = ReleasePackageAssembler.Assemble(Options(stage, repo));
            Assert.True(result.Ok, string.Join("\n", result.Problems));

            var v = result.Manifest.Versions;
            var runtimeHeader = File.ReadAllText(Path.Combine(
                ReleaseManifestFactory.FirmwareRuntimeDir(repo), "hmi_runtime.h"));
            var apiHeader = File.ReadAllText(Path.Combine(
                ReleaseManifestFactory.FirmwareRuntimeDir(repo), "hmi_ui_api.h"));
            var firmware = FirmwareVersionMarkers.ParseRuntimeHeader(runtimeHeader);
            var api = FirmwareVersionMarkers.ParseApiHeader(apiHeader);

            // 固件侧：包内清单 == 头文件
            Assert.Equal(firmware.VersionString, v.Firmware);
            Assert.Equal(firmware.TargetId, v.FirmwareTargetId);
            Assert.Equal(api.AbiVersion, v.FirmwareAbiVersion);
            Assert.Equal(api.LvglVersion, v.FirmwareLvglVersion);

            // 编辑器侧：包内清单 == 硬件目标插件
            Assert.Equal(T113.Metadata.Id, v.EditorTargetId);
            Assert.Equal(T113.LvglConfig.AbiVersion, v.EditorAbiVersion);
            Assert.Equal(T113.LvglConfig.LvglVersion, v.EditorLvglVersion);

            // 两侧对应（发布物上能一眼看出的那三行）
            Assert.Equal(v.FirmwareTargetId, v.EditorTargetId);
            Assert.Equal(v.FirmwareAbiVersion, v.EditorAbiVersion);
            Assert.Equal(v.FirmwareLvglVersion, v.EditorLvglVersion);
        }
        finally
        {
            TestTempDir.DeleteDir(stage);
        }
    }

    [Fact]
    public void Assemble_WritesSealedPackage_AndUpgradeGuidePointsAtRealDocs()
    {
        var stage = NewStage();
        try
        {
            var result = ReleasePackageAssembler.Assemble(Options(stage, SourceScanner.RepoRoot));
            Assert.True(result.Ok, string.Join("\n", result.Problems));

            Assert.True(File.Exists(Path.Combine(stage, ReleaseManifestFactory.ManifestFileName)));
            Assert.True(File.Exists(Path.Combine(stage, ReleaseManifestFactory.ChecksumFileName)));

            // 随包固件文档必须在（升级说明里的链接不许悬空）
            foreach (var name in ReleasePackageAssembler.FirmwareDocs)
                Assert.True(File.Exists(Path.Combine(stage, ReleasePackageAssembler.DocsDirName, name)), name);

            // 升级/回滚说明引用的是**真实存在**的包内路径与设备端真实加载顺序
            var guide = File.ReadAllText(Path.Combine(stage, ReleasePackageAssembler.UpgradeDocName));
            Assert.Contains("docs/flashing-guide.md", guide, StringComparison.Ordinal);
            Assert.Contains("/mnt/UDISK/hmi/libhmi_ui.so", guide, StringComparison.Ordinal);
            Assert.Contains("/usr/lib/hmi/libhmi_ui.so", guide, StringComparison.Ordinal);

            // 校验和覆盖 manifest.json（它是载荷），清单不登记两个封条文件自身
            var checksums = File.ReadAllText(Path.Combine(stage, ReleaseManifestFactory.ChecksumFileName));
            Assert.Contains(ReleaseManifestFactory.ManifestFileName, checksums, StringComparison.Ordinal);
            Assert.DoesNotContain(result.Manifest.Files, f =>
                f.Path is ReleaseManifestFactory.ManifestFileName or ReleaseManifestFactory.ChecksumFileName);

            // 清单逐文件登记的字节数/哈希与磁盘一致（装配时已自检，这里独立复核一项）
            var editorDll = result.Manifest.Files.Single(f => f.Path == "editor/HmiEditor.App");
            Assert.Equal(new FileInfo(Path.Combine(stage, "editor", "HmiEditor.App")).Length, editorDll.SizeBytes);
        }
        finally
        {
            TestTempDir.DeleteDir(stage);
        }
    }

    [Fact]
    public void Verify_PassesOnFreshPackage_AndDetectsTamperedPayload()
    {
        var stage = NewStage();
        try
        {
            Assert.True(ReleasePackageAssembler.Assemble(Options(stage, SourceScanner.RepoRoot)).Ok);

            // 先证明它可以绿
            Assert.True(ReleasePackageVerifier.Verify(stage).Ok);

            // 改一个字节 —— 必须点名那个文件（可失败性）
            var doc = Path.Combine(stage, ReleasePackageAssembler.DocsDirName, "version-info.md");
            File.AppendAllText(doc, "\n篡改一行\n");

            var verification = ReleasePackageVerifier.Verify(stage);
            Assert.False(verification.Ok);
            Assert.Contains(verification.Problems, p => p.Contains("version-info.md", StringComparison.Ordinal));
        }
        finally
        {
            TestTempDir.DeleteDir(stage);
        }
    }

    [Fact]
    public void Verify_DetectsDeletedPayloadFile()
    {
        var stage = NewStage();
        try
        {
            Assert.True(ReleasePackageAssembler.Assemble(Options(stage, SourceScanner.RepoRoot)).Ok);
            File.Delete(Path.Combine(stage, ReleasePackageAssembler.DocsDirName, "flashing-guide.md"));

            var verification = ReleasePackageVerifier.Verify(stage);
            Assert.False(verification.Ok);
            Assert.Contains(verification.Problems, p =>
                p.Contains("flashing-guide.md", StringComparison.Ordinal) && p.Contains("不存在", StringComparison.Ordinal));
        }
        finally
        {
            TestTempDir.DeleteDir(stage);
        }
    }

    [Fact]
    public void Verify_DetectsVersionTableThatNoLongerMatchesTheRepo()
    {
        // 包是用"当前源码状态"装出来的；拿另一个版本的源码去核 ⇒ 必须报"版本清单不一致"。
        // 这条同时也是"版本对应判据真的会红"的证据（把版本标记换掉就能让它失败）。
        var stage = NewStage();
        var fakeRepo = NewFakeRepo(versionString: "9.9.9");
        try
        {
            Assert.True(ReleasePackageAssembler.Assemble(Options(stage, SourceScanner.RepoRoot)).Ok);

            var verification = ReleasePackageVerifier.Verify(stage, fakeRepo, T113);
            Assert.False(verification.Ok);
            Assert.Contains(verification.Problems, p => p.Contains("版本清单", StringComparison.Ordinal));
        }
        finally
        {
            TestTempDir.DeleteDir(stage);
            TestTempDir.DeleteDir(fakeRepo);
        }
    }

    [Fact]
    public void Assemble_ReportsMissingFirmwareDoc()
    {
        var stage = NewStage();
        var fakeRepo = NewFakeRepo(); // 只有头文件，没有 docs/
        try
        {
            var result = ReleasePackageAssembler.Assemble(Options(stage, fakeRepo));
            Assert.False(result.Ok);
            Assert.Contains(result.Problems, p => p.Contains("随包固件文档缺失", StringComparison.Ordinal));
        }
        finally
        {
            TestTempDir.DeleteDir(stage);
            TestTempDir.DeleteDir(fakeRepo);
        }
    }

    [Fact]
    public void Assemble_ReportsMissingEditorArtifacts()
    {
        var stage = TestTempDir.NewDir("release-empty"); // 没有 editor/
        try
        {
            var result = ReleasePackageAssembler.Assemble(Options(stage, SourceScanner.RepoRoot));
            Assert.False(result.Ok);
            Assert.Contains(result.Problems, p => p.Contains("editor/", StringComparison.Ordinal));
        }
        finally
        {
            TestTempDir.DeleteDir(stage);
        }
    }
}
