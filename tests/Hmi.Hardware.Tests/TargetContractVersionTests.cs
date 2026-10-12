using System.IO;
using Hmi.Hardware.Builtin;
using Hmi.Hardware.Contracts;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace Hmi.Hardware.Tests;

/// <summary>
/// **版本标记的跨边界对应**（登记表 §4 第 29 项"版本标记四者对应"）。
///
/// <para>此前这些标记只是**注释里的约稿**：C 头文件写着"与 <c>docs/version-info.md</c> 同步"、
/// "与 <c>AllwinnerT113Target.Metadata.Id</c> 对齐"，但没有任何东西真的去比。
/// 这个类把四组对应关系变成**可执行的判据**（全部按文本解析，不需要交叉编译）：</para>
///
/// <list type="number">
///   <item>固件版本串自洽：<c>HMI_RUNTIME_VERSION_STR</c> == "MAJOR.MINOR.PATCH"；</item>
///   <item>文档 ↔ 代码：<c>firmware/t113/docs/version-info.md</c> 的当前版本 == 上面那个串；</item>
///   <item>固件 ↔ 编辑器：<c>HMI_TARGET_ID</c> == <c>AllwinnerT113Target.Metadata.Id</c>；</item>
///   <item>ABI / LVGL：<c>HMI_UI_ABI_VERSION</c>、<c>HMI_UI_LVGL_VERSION</c> == 目标插件声明的
///         <see cref="LvglConfig.AbiVersion"/> / <see cref="LvglConfig.LvglVersion"/>，
///         且该 LVGL 版本必须出现在编辑器认得的版本清单里。</item>
/// </list>
/// </summary>
public class TargetContractVersionTests
{
    private static string FirmwareDir => Path.Combine(SourceScanner.RepoRoot, "firmware", "t113");

    private static string ReadRuntimeHeader() =>
        File.ReadAllText(Path.Combine(FirmwareDir, "app", "runtime", "hmi_runtime.h"));

    private static string ReadApiHeader() =>
        File.ReadAllText(Path.Combine(FirmwareDir, "app", "runtime", "hmi_ui_api.h"));

    // 解析一律走 FirmwareVersionMarkers —— 发布包清单（tools/ReleasePackager）读的是同一处，
    // 避免"守卫认定的版本"与"发布包里写的版本"各拿一份正则。

    [Fact]
    public void FirmwareVersionString_MatchesItsOwnNumberComponents()
    {
        // 手写的版本串很容易在被 bump 数字时忘记改（反之亦然）——这条把它钉住
        var header = ReadRuntimeHeader();

        Assert.Equal(
            FirmwareVersionMarkers.ExpectedVersionString(header),
            FirmwareVersionMarkers.ParseRuntimeHeader(header).VersionString);
    }

    [Fact]
    public void VersionInfoDoc_MatchesTheFirmwareHeader()
    {
        // 头文件里那句"与 docs/version-info.md 同步"此前只是注释；这条让它可执行
        var version = FirmwareVersionMarkers.ParseRuntimeHeader(ReadRuntimeHeader()).VersionString;
        var doc = File.ReadAllText(Path.Combine(FirmwareDir, "docs", "version-info.md"));

        Assert.Contains($"当前版本：v{version}", doc, StringComparison.Ordinal);
    }

    [Fact]
    public void FirmwareTargetId_MatchesTheEditorSideTargetId()
    {
        // 头文件里那句"与 AllwinnerT113Target.Metadata.Id 对齐"同样只是注释
        var targetId = FirmwareVersionMarkers.ParseRuntimeHeader(ReadRuntimeHeader()).TargetId;
        Assert.Equal(new AllwinnerT113Target().Metadata.Id, targetId);
    }

    [Fact]
    public void T113Target_DeclaresTheAbiAndLvglVersionTheFirmwareRuntimeChecks()
    {
        // 运行时（screen_manager.c）在 dlopen 之后严格比对这两个值，不一致直接拒绝加载 ⇒
        // 目标插件声明的必须就是头文件里的那两个
        var api = FirmwareVersionMarkers.ParseApiHeader(ReadApiHeader());
        var config = new AllwinnerT113Target().LvglConfig;

        Assert.Equal(api.AbiVersion, config.AbiVersion);
        Assert.Equal(api.LvglVersion, config.LvglVersion);
    }

    [Fact]
    public void FirmwareRequiredLvglVersion_IsSelectableInTheEditor()
    {
        // 板子要求的版本必须出现在编辑器认得的清单里 —— 否则用户在工程设置里根本选不到它，
        // 只能靠"表外版本"那条兜底路径（第 18 项修的那个）勉强往返。
        var required = FirmwareVersionMarkers.ParseApiHeader(ReadApiHeader()).LvglVersion;

        Assert.Contains(required, LvglApiCompatChecker.SupportedLvglVersions);
    }

    [Fact]
    public void NewProjectsDefaultToAVersionTheEditorKnows()
    {
        // 新建工程的默认 LVGL 版本也必须在清单里，否则一开局就是"表外版本"
        Assert.Contains(new HmiProject().LvglVersion, LvglApiCompatChecker.SupportedLvglVersions);
    }

    [Fact]
    public void DeclaredTargetVersions_AreAlwaysKnownToTheEditor()
    {
        // 内置目标要么**不声明**（RK3506：仓库里没有它的固件可对照），要么声明一个认得的版本；
        // 不允许出现"填了个没验证过、编辑器也不认识"的版本号。
        foreach (var target in new IHardwareTarget[] { new AllwinnerT113Target(), new RockchipRK3506Target() })
        {
            var version = target.LvglConfig.LvglVersion;
            if (string.IsNullOrEmpty(version)) continue;
            Assert.Contains(version, LvglApiCompatChecker.SupportedLvglVersions);
        }
    }

    // ── 工程 ↔ 目标：不兼容时必须在设置界面里报出来 ──

    [Fact]
    public void ProjectTargetingAnIncompatibleLvglMajor_IsAnError()
    {
        // 8.3 工程 + 9.5 板子：LvglApiCompatChecker 只在 9.x 时才改写 v8→v9 API，
        // 所以生成的 C 里会留着 9.x 已改名/移除的符号 ⇒ 板上根本编译不过
        var result = new AllwinnerT113Target().ValidateProject(new HmiProject { LvglVersion = "8.3" });

        var issue = Assert.Single(result.Issues, i => i.Code == "LVGL_VERSION_MISMATCH");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("9.5")]   // 与板子同版本
    [InlineData("9.4")]   // 同主版本：检查器按主版本分支 ⇒ 兼容
    [InlineData("9.0")]
    public void SameMajorLvglVersion_IsAccepted(string projectVersion)
    {
        var result = new AllwinnerT113Target().ValidateProject(new HmiProject { LvglVersion = projectVersion });
        Assert.DoesNotContain(result.Issues, i => i.Code == "LVGL_VERSION_MISMATCH");
    }

    [Fact]
    public void UndeclaredTarget_SkipsTheVersionCheck()
    {
        // 没声明运行时的目标不做判断（缺依据就不下结论）
        var result = new RockchipRK3506Target().ValidateProject(new HmiProject { LvglVersion = "8.3" });
        Assert.DoesNotContain(result.Issues, i => i.Code == "LVGL_VERSION_MISMATCH");
    }
}
