using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`ProjectSettingsView` 的「保存时一次性读取」框的**行为面**（TextBox 那一半）**（阶段 198）——
/// 与 <see cref="ProjectSettingsSavePathReadFaceTests"/>（数字框那半）同形：
/// 那一面证"15 个数字框读进了各自那一格"，这一面证**9 个文本输入框**（`TxtAuthor` / `TxtCompany` /
/// `TxtDescription` / `TxtProjectVersion` / `TxtDefaultBgColor` / `TxtHistoryPath` / `TxtOutputDir` /
/// `TxtAuditPath` / `TxtDefaultRoleId`）读进了各自那一格，**并且两条口径都对**：
///
/// <list type="number">
/// <item><b>非空 ⇒ 裁剪后写入</b>（9 条手势都带两侧空白，读数必须是裁过的原文）；</item>
/// <item><b>空 ⇒ 落到"声明的默认"</b>：6 个字段落 <c>null</c>，另外 3 个落**各自的兜底值**
/// （`ProjectVersion → "1.0.0"` · `OutputDirectory → "./export"` · `DefaultRoleId → "role-operator"`）——
/// 这三条兜底只在"清空"这一支才看得见，接线面与结构面都照不到。</item>
/// </list>
///
/// <para><b>另一个框刻意不在这面里</b>：`TxtRuntimeUserPassword` —— 它**不由** `SaveToProject()` 读，
/// 而是「设置口令」按钮（`BtnSetRuntimeUserPassword`）读走（那一跳归运行时用户面）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class ProjectSettingsSavePathTextBoxReadFaceTests
{
    /// <summary>一条手势：把 <paramref name="Text"/> 写进 <paramref name="Name"/>，点应用后断言模型字段读到 <paramref name="Expected"/>。</summary>
    private sealed record Spec(string Name, string Field, Func<HmiProject, string> Read, string Text, string Expected);

    private const string Null = "(null)";

    private static readonly Spec[] Specs =
    {
        new("TxtDescription", "Description", p => p.Description ?? Null, "  夹具工程描述  ", "夹具工程描述"),
        new("TxtAuthor", "Author", p => p.Author ?? Null, "  张三  ", "张三"),
        new("TxtCompany", "Company", p => p.Company ?? Null, "  某公司  ", "某公司"),
        new("TxtProjectVersion", "ProjectVersion", p => p.ProjectVersion ?? Null, "  2.3.4  ", "2.3.4"),
        new("TxtDefaultBgColor", "ScreenDefaultsConfig.DefaultBgColor",
            p => p.ScreenDefaultsConfig.DefaultBgColor ?? Null, "  #112233  ", "#112233"),
        new("TxtHistoryPath", "RuntimeConfig.HistoryStoragePath",
            p => p.RuntimeConfig.HistoryStoragePath ?? Null, "  /data/hist  ", "/data/hist"),
        new("TxtOutputDir", "ExportConfig.OutputDirectory",
            p => p.ExportConfig.OutputDirectory ?? Null, "  ./out  ", "./out"),
        new("TxtAuditPath", "SecurityConfig.AuditLogStoragePath",
            p => p.SecurityConfig.AuditLogStoragePath ?? Null, "  /data/audit  ", "/data/audit"),
        new("TxtDefaultRoleId", "SecurityConfig.DefaultRoleId",
            p => p.SecurityConfig.DefaultRoleId ?? Null, "  role-engineer  ", "role-engineer"),
    };

    /// <summary>清空之后**声明的兜底**（6 个 null ＋ 3 个各自的兜底值）。</summary>
    private static readonly Dictionary<string, string> EmptyFallbacks = new(StringComparer.Ordinal)
    {
        ["TxtDescription"] = Null,
        ["TxtAuthor"] = Null,
        ["TxtCompany"] = Null,
        ["TxtProjectVersion"] = "1.0.0",
        ["TxtDefaultBgColor"] = Null,
        ["TxtHistoryPath"] = Null,
        ["TxtOutputDir"] = "./export",
        ["TxtAuditPath"] = Null,
        ["TxtDefaultRoleId"] = "role-operator",
    };

    private static void Apply(ProjectSettingsView view)
    {
        ControlWiringFace.Find<Button>(view, "BtnApply")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(2);
    }

    [Fact]
    public void EveryDeferredTextBox_LandsInItsOwnModelField()
    {
        using var sandbox = new DataRootSandbox("projectsettings-save-read-text");

        UiTestHost.Run(() =>
        {
            var project = new HmiProject { Name = "夹具工程" };
            project.Screens.Add(ScreenFactory.Create(project, name: "主画面"));

            var view = new ProjectSettingsView(project);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检①：起点都与手势值不同（否则"抄对了"与"压根没抄"在读数上同形）
                foreach (var spec in Specs)
                    Assert.True(!string.Equals(spec.Expected, spec.Read(project), StringComparison.Ordinal),
                        $"{spec.Name}（{spec.Field}）的手势值 '{spec.Expected}' 与起点相同 ⇒ 这条腿是空转");

                foreach (var spec in Specs)
                    ControlWiringFace.Write(view, spec.Name, spec.Text, pump: 1);

                // 前提自检②：点「应用」之前一个都没落盘（落了盘说明它其实有接线，该归接线面）
                foreach (var spec in Specs)
                    Assert.True(!string.Equals(spec.Expected, spec.Read(project), StringComparison.Ordinal),
                        $"{spec.Name} 在点「应用」之前就落进模型了 —— 说明它其实有接线（该归接线面）");

                Apply(view);

                // 逐字段断言：**裁剪后**落进自己那一格
                foreach (var spec in Specs)
                {
                    var after = spec.Read(project);
                    Assert.True(string.Equals(spec.Expected, after, StringComparison.Ordinal),
                        $"{spec.Name} 驱动 '{spec.Text}' 并点「应用」后，{spec.Field} 读到 '{after}'，期望 '{spec.Expected}'"
                        + "（抄错一行时，这一格读到的会是**别的框**的值；漏了 Trim 会读到带空白的原文）");
                }
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    [Fact]
    public void EmptyInputs_FallBackToTheDeclaredDefaults()
    {
        using var sandbox = new DataRootSandbox("projectsettings-save-read-text-empty");

        UiTestHost.Run(() =>
        {
            var project = new HmiProject { Name = "夹具工程" };
            project.Screens.Add(ScreenFactory.Create(project, name: "主画面"));

            // 起点刻意**全非空**：这样"清空之后落兜底"才判得出来
            project.Description = "旧描述";
            project.Author = "旧作者";
            project.Company = "旧公司";
            project.ProjectVersion = "0.9.0";
            project.ScreenDefaultsConfig.DefaultBgColor = "#000000";
            project.RuntimeConfig.HistoryStoragePath = "/data/old-hist";
            project.ExportConfig.OutputDirectory = "./old-out";
            project.SecurityConfig.AuditLogStoragePath = "/data/old-audit";
            project.SecurityConfig.DefaultRoleId = "role-operator";

            var view = new ProjectSettingsView(project);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                foreach (var spec in Specs)
                    ControlWiringFace.Write(view, spec.Name, "", pump: 1);

                Apply(view);

                foreach (var spec in Specs)
                {
                    var expected = EmptyFallbacks[spec.Name];
                    var after = spec.Read(project);
                    Assert.True(string.Equals(expected, after, StringComparison.Ordinal),
                        $"{spec.Name}（{spec.Field}）清空后读到 '{after}'，期望 '{expected}'"
                        + "（三条兜底 —— ProjectVersion/OutputDirectory/DefaultRoleId —— 只有清空这一支才看得见）");
                }
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **表内自检**：9 条手势的文本 / 字段路径 / 框名各自互异（一格的读数能唯一归因到某个框），
    /// 且兜底表与手势表**一一对应**（少写一条兜底会漏判）。
    /// </summary>
    [Fact]
    public void Specs_AreDistinct_AndFallbacksCoverThemAll()
    {
        Assert.Equal(9, Specs.Length);
        Assert.Equal(Specs.Length, Specs.Select(s => s.Text).Distinct().Count());
        Assert.Equal(Specs.Length, Specs.Select(s => s.Field).Distinct().Count());
        Assert.Equal(Specs.Length, Specs.Select(s => s.Name).Distinct().Count());

        Assert.Equal(
            Specs.Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal),
            EmptyFallbacks.Keys.OrderBy(n => n, StringComparer.Ordinal));
    }

    /// <summary>
    /// **前提：这 9 个框确实没有直接的 `TextChanged` 接线**（否则它们该归接线面，本面就重复了）。
    /// 只看这一个文件的代码，判据自带（与接线面那份表互为正反两面）。
    /// </summary>
    [Fact]
    public void TheseBoxes_ReallyHaveNoDirectWiring()
    {
        var axamlPath = Path.Combine(
            SourceScanner.RepoRoot, ControlWiringFace.ViewsRelative, "ProjectSettingsView.axaml.cs");
        var wired = ControlWiringFace.WiredNames(SourceScanner.StripComments(File.ReadAllText(axamlPath)));

        var offenders = Specs.Select(s => s.Name).Where(wired.Contains).ToArray();
        Assert.True(offenders.Length == 0,
            "这些框**接了** TextChanged 接线 —— 它们该归接线面（ProjectSettingsTextBoxWiringTests），"
            + "本面的前提不成立：" + string.Join(", ", offenders));
    }
}
