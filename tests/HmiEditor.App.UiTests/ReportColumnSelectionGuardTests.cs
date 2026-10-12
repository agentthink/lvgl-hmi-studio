using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **报表列选择判据**（阶段 177）：导出前的"选列"—— 纯接缝（勾选 → 投影）、三面板都接上、
/// 以及对话框要的两条文案在两本字典里都在。
///
/// <para>⚠ 对话框本身由 <c>WindowViewSmokeUiTests</c> 的**自动收面**覆盖（按类型关系收，
/// 新增窗口自动进来）⇒ 本类不重复造窗口级冒烟。</para>
/// </summary>
public class ReportColumnSelectionGuardTests
{
    private static readonly Dictionary<string, string> Zh = PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static readonly Dictionary<string, string> En = PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    private static ReportTable Table() => new()
    {
        Title = "T",
        Columns = new List<ReportColumn> { new("a", "A"), new("b", "B"), new("c", "C") },
        Rows = new List<IReadOnlyList<string>> { new[] { "1", "2", "3" } },
    };

    [Fact]
    public void NeedsColumnChoice_OnlyWhenMoreThanOneColumn()
    {
        Assert.True(ReportExport.NeedsColumnChoice(Table()));
        var single = new ReportTable
        {
            Title = "T",
            Columns = new List<ReportColumn> { new("a", "A") },
            Rows = new List<IReadOnlyList<string>> { new[] { "1" } },
        };
        Assert.False(ReportExport.NeedsColumnChoice(single));   // 一列没什么好选 ⇒ 别白弹一次对话框
    }

    /// <summary>勾选 → 投影：顺序沿用原表、未勾的丢掉、行跟着投影。</summary>
    [Fact]
    public void ApplyColumnSelection_ProjectsTheTable()
    {
        var projected = ReportExport.ApplyColumnSelection(Table(), new[] { "a", "c" });

        Assert.Equal(new[] { "a", "c" }, projected.Columns.Select(c => c.Key).ToArray());
        Assert.Equal(new[] { "1", "3" }, projected.Rows.Single());
    }

    /// <summary>对话框要的文案：标题/提示/全不选 两本字典都在（全选/确定/取消 复用既有键）。</summary>
    [Fact]
    public void DialogKeys_ExistInBothDictionaries()
    {
        foreach (var key in new[]
                 {
                     "report.columns.title", "report.columns.hint", "report.columns.none",
                     "ctx.select.all", "gen.ok", "gen.cancel",
                 })
        {
            Assert.True(Zh.ContainsKey(key), $"zh 字典缺键：{key}");
            Assert.True(En.ContainsKey(key), $"en 字典缺键：{key}");
        }
    }

    /// <summary>
    /// **接线契约**（源码级）：三个报表面板导出前都要过**同一处** <c>ReportExport.ChooseColumnsAsync</c>
    /// （漏一个 ⇒ 那个面板的"选列"静默失效），且取消必须**中止导出**。
    /// </summary>
    [Fact]
    public void AllReportPanels_AskForColumnsBeforeSaving()
    {
        foreach (var view in new[] { "AuditLogView.axaml.cs", "AlarmListView.axaml.cs", "TrendConfigView.axaml.cs" })
        {
            var code = SourceScanner.StripComments(System.IO.File.ReadAllText(
                System.IO.Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", view)));

            Assert.Contains("ReportExport.ChooseColumnsAsync", code, StringComparison.Ordinal);
            Assert.Contains("is not { } chosen", code, StringComparison.Ordinal);   // 取消 ⇒ 中止
            Assert.Contains("ReportExport.SaveAsync", code, StringComparison.Ordinal);
        }
    }

    /// <summary>对话框窗口存在，且按钮都在（XAML 侧契约）。</summary>
    [Fact]
    public void Dialog_HasTheExpectedControls()
    {
        var axaml = System.IO.File.ReadAllText(System.IO.Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", "ReportColumnDialog.axaml"));

        foreach (var name in new[] { "BtnSelectAll", "BtnSelectNone", "BtnOk", "BtnCancel", "ColumnList" })
            Assert.Contains($"x:Name=\"{name}\"", axaml, StringComparison.Ordinal);
    }
}
