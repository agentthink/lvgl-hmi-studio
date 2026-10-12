using System;
using System.Collections.Generic;
using System.IO;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **数据源键的接线契约**（阶段 182）：三个面板给报表包的 <c>kind</c> 与**报表模板的存储键**
/// 必须是**同一批**字符串 —— 但传错**不会报错**（模板静默套不上、报表包文件名与模板对不上）。
/// 所以这里按**面板逐个**核对：对的那个键在、另外两个**不在**（抄错行是最可能的错法）。
///
/// <para>⚠ 阶段 183 会把模板套到这三个面板上，那时本判据再加"套用点"那一半。</para>
/// </summary>
public class ReportTemplateSettingsGuardTests
{
    private static string Code(string view) => SourceScanner.StripComments(File.ReadAllText(
        Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", view)));

    private static readonly (string View, string Key)[] Panels =
    {
        ("AuditLogView.axaml.cs", nameof(ReportSourceKeys.Audit)),
        ("AlarmListView.axaml.cs", nameof(ReportSourceKeys.Alarm)),
        ("TrendConfigView.axaml.cs", nameof(ReportSourceKeys.History)),
    };

    /// <summary>每个面板只许出现**自己**那一个数据源键（另两个出现就是抄错了行）。</summary>
    [Fact]
    public void EachPanel_UsesItsOwnSourceKeyOnly()
    {
        foreach (var (view, key) in Panels)
        {
            var code = Code(view);

            Assert.Contains($"ReportSourceKeys.{key}", code, StringComparison.Ordinal);

            foreach (var other in ReportSourceKeys.All)
            {
                if (string.Equals(Capitalize(other), key, StringComparison.Ordinal)) continue;
                Assert.False(code.Contains($"ReportSourceKeys.{Capitalize(other)}", StringComparison.Ordinal),
                    $"{view} 里出现了别的数据源键 ReportSourceKeys.{Capitalize(other)}");
            }
        }
    }

    /// <summary>数据源键确实**传进了**报表包导出（只声明常量而没用 = 白搭）。</summary>
    [Fact]
    public void SourceKeys_ArePassedToTheBundleExport()
    {
        foreach (var (view, key) in Panels)
        {
            var code = Code(view);

            Assert.Contains("ReportExport.SaveBundleAsync", code, StringComparison.Ordinal);
            Assert.Contains($"ReportSourceKeys.{key}", code, StringComparison.Ordinal);

            // 反向：面板里不许再出现旧的字面量（"audit"/"alarm"/"history"）
            Assert.False(code.Contains($"\"{key.ToLowerInvariant()}\"", StringComparison.Ordinal),
                $"{view} 里还留着裸字面量 \"{key.ToLowerInvariant()}\" —— 应当走 ReportSourceKeys");
        }
    }

    /// <summary>三个键与 <c>All</c> 对齐（顺序即界面/文件顺序，判据按它遍历）。</summary>
    [Fact]
    public void SourceKeys_MatchThePanelTable()
    {
        var expected = new List<string>();
        foreach (var (_, key) in Panels) expected.Add(key);

        var actual = new List<string>();
        foreach (var source in ReportSourceKeys.All) actual.Add(Capitalize(source));

        Assert.Equal(expected, actual);
    }

    private static string Capitalize(string source) => char.ToUpperInvariant(source[0]) + source[1..];
}
