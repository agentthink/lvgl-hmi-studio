using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;
using SkiaSharp;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **报表包导出判据**（阶段 179）：一个数据源 × 四种格式写进一个目录 ＋ <c>index.txt</c>；
/// 渲染不出来的格式（典型是缺字体时的 PDF）**记进索引而不是静默丢**；两次打包逐字节相同；
/// 三个面板都接了同一个入口。
/// </summary>
public class ReportBundleExportGuardTests
{
    private static readonly Dictionary<string, string> Zh = PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static readonly Dictionary<string, string> En = PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    private static readonly DateTimeOffset Now = new(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

    private static ReportTable Table() => new()
    {
        Title = "T",
        Columns = new List<ReportColumn> { new("a", "A") },
        Rows = new List<IReadOnlyList<string>> { new[] { "1" } },
    };

    /// <summary>缺字体 ⇒ PDF 进 <c>Skipped</c>，其余三种照写，索引里记着原因。</summary>
    [Fact]
    public void WriteBundle_WritesEveryRenderableFormat_AndRecordsSkippedOnes()
    {
        var dir = TestTempDir.NewDir("bundle");
        try
        {
            var result = ReportExport.WriteBundle(
                dir, "audit", Table(), Now,
                "2026-10-01 12:00:00Z", "生成时间", "zh-CN",
                "NO-FONT", typeface: null);

            Assert.Null(result.Error);
            Assert.Equal(3, result.Written.Count);                       // csv/xlsx/html
            Assert.Single(result.Skipped);
            Assert.Contains("NO-FONT", result.Skipped[0], StringComparison.Ordinal);

            foreach (var name in result.Written)
                Assert.True(File.Exists(Path.Combine(dir, name)), $"缺文件：{name}");
            Assert.False(File.Exists(Path.Combine(dir, ReportBundle.FileName("audit", Now, ".pdf"))),
                "缺字体时**不该**产出 PDF（豆腐块）");

            var index = File.ReadAllText(Path.Combine(dir, ReportBundle.IndexFileName));
            Assert.Contains("file\trows", index, StringComparison.Ordinal);
            foreach (var name in result.Written) Assert.Contains(name, index, StringComparison.Ordinal);
            Assert.Contains("skipped", index, StringComparison.Ordinal);
            Assert.Contains("NO-FONT", index, StringComparison.Ordinal);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    /// <summary>有字体时四种格式齐全；两次打包**逐字节相同**（文件名与内容都确定）。</summary>
    [Fact]
    public void WriteBundle_IsByteIdentical_WithAFont()
    {
        using var typeface = SKTypeface.FromFile(Path.Combine(
            SourceScanner.RepoRoot, "firmware", "t113", "tina", "package", "hmi-runtime", "files", "fangsong.ttf"));
        Assert.NotNull(typeface);

        var first = TestTempDir.NewDir("bundle-a");
        var second = TestTempDir.NewDir("bundle-b");
        try
        {
            ReportExport.WriteBundle(first, "alarm", Table(), Now, "t", "g", "zh-CN", "NO-FONT", typeface);
            ReportExport.WriteBundle(second, "alarm", Table(), Now, "t", "g", "zh-CN", "NO-FONT", typeface);

            var names = new[] { ".csv", ".xlsx", ".pdf", ".html" }.Select(e => ReportBundle.FileName("alarm", Now, e)).ToArray();
            foreach (var name in names)
            {
                Assert.True(File.Exists(Path.Combine(first, name)), $"缺 {name}");
                Assert.Equal(File.ReadAllBytes(Path.Combine(first, name)), File.ReadAllBytes(Path.Combine(second, name)));
            }
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(first, ReportBundle.IndexFileName)),
                File.ReadAllBytes(Path.Combine(second, ReportBundle.IndexFileName)));
        }
        finally
        {
            TestTempDir.DeleteDir(first);
            TestTempDir.DeleteDir(second);
        }
    }

    [Fact]
    public void BundleKeys_ExistInBothDictionaries()
    {
        foreach (var key in new[] { "report.bundle.btn", "report.bundle.save.title", "report.bundle.ok", "ui.op.export.bundle" })
        {
            Assert.True(Zh.ContainsKey(key), $"zh 字典缺键：{key}");
            Assert.True(En.ContainsKey(key), $"en 字典缺键：{key}");
        }
    }

    /// <summary>
    /// **接线契约**（源码级）：三个面板的"报表包"按钮都存在且被订阅，且都走**同一处** <c>SaveBundleAsync</c>。
    /// </summary>
    [Fact]
    public void AllPanels_HaveABundleButton_OnTheSharedPath()
    {
        var root = Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views");
        foreach (var (view, axaml, button) in new[]
                 {
                     ("AuditLogView.axaml.cs", "AuditLogView.axaml", "ExportBundleBtn"),
                     ("AlarmListView.axaml.cs", "AlarmListView.axaml", "BtnExportBundle"),
                     ("TrendConfigView.axaml.cs", "TrendConfigView.axaml", "BtnExportBundle"),
                 })
        {
            var code = SourceScanner.StripComments(File.ReadAllText(Path.Combine(root, view)));
            Assert.Contains("ReportExport.SaveBundleAsync", code, StringComparison.Ordinal);
            Assert.Contains($"{button}.Click +=", code, StringComparison.Ordinal);
            Assert.Contains($"x:Name=\"{button}\"", File.ReadAllText(Path.Combine(root, axaml)), StringComparison.Ordinal);
        }
    }
}
