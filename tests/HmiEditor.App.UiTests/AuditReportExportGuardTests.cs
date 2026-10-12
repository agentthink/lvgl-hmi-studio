using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **审计报表导出判据**（阶段 172,报表能力 MVP）。
///
/// <para>钉三件：① Core 用的每个键在 App 侧**都映射到 Lang 常量**（不许静默回落成键名显示给用户）
/// 且两本字典都解析；② 造出来的表列名就是那个映射；③ UI 那一跳走 <see cref="UiOperationGuard"/>
/// （失败不会变成"点了没反应"）。另有一条端到端确定性判据（两次导出逐字节相同）。</para>
///
/// <para>⚠ 本类**不切语言**（只读当前语言与字典文件）⇒ 不需要 <c>[Collection("Language-global")]</c>。</para>
/// </summary>
public class AuditReportExportGuardTests
{
    private static readonly Dictionary<string, string> Zh = PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static readonly Dictionary<string, string> En = PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    private static AuditLogEntry Entry(string user, bool ok, string detail) => new()
    {
        Timestamp = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc),
        Username = user,
        Action = AuditAction.Login,
        Success = ok,
        Source = "127.0.0.1",
        Detail = detail,
    };

    /// <summary>Core 用到的**全部键**（列 ＋ 标题 ＋ 结果）—— 判据一的扫描面由它定义。</summary>
    private static IEnumerable<string> CoreKeys() =>
        AuditLogReport.ColumnKeys.Concat(new[] { AuditLogReport.TitleKey, AuditLogReport.ResultOkKey, AuditLogReport.ResultFailKey });

    /// <summary>① 每个 Core 键都要：两本字典都在 ＋ App 映射出来的**不是键名本身**（回落即红）。</summary>
    [Fact]
    public void EveryCoreKey_ResolvesToLocalizedText_InBothDictionaries()
    {
        foreach (var key in CoreKeys())
        {
            Assert.True(Zh.ContainsKey(key), $"zh 字典缺键：{key}");
            Assert.True(En.ContainsKey(key), $"en 字典缺键：{key}");
        }

        UiTestHost.Run(() =>
        {
            foreach (var key in AuditLogReport.ColumnKeys)
            {
                var text = AuditReportText.ColumnName(key);
                Assert.False(string.Equals(text, key, StringComparison.Ordinal),
                    $"列键 {key} 没有映射到 Lang 常量（回落成了键名 —— 用户会看到 {key}）");
                Assert.False(string.IsNullOrWhiteSpace(text), $"列键 {key} 映射到空文本");
            }

            Assert.False(string.Equals(AuditReportText.Title, AuditLogReport.TitleKey, StringComparison.Ordinal),
                "报表标题没有映射到 Lang 常量");
            Assert.False(string.Equals(AuditReportText.ResultText(true), AuditLogReport.ResultOkKey, StringComparison.Ordinal),
                "结果列（成功）没有映射到 Lang 常量");
            Assert.False(string.Equals(AuditReportText.ResultText(false), AuditLogReport.ResultFailKey, StringComparison.Ordinal),
                "结果列（失败）没有映射到 Lang 常量");
        });
    }

    /// <summary>② 造出来的表：列名与 <see cref="AuditReportText.ColumnName"/> 一一对应，**顺序**即 Core 的列序。</summary>
    [Fact]
    public void TableColumns_FollowCoreOrderAndAppText()
    {
        UiTestHost.Run(() =>
        {
            var table = AuditLogReport.From(new[] { Entry("op", true, "登录成功") }, AuditReportText.Localizer);

            Assert.Equal(AuditLogReport.ColumnKeys, table.Columns.Select(c => c.Key).ToArray());
            Assert.Equal(AuditLogReport.ColumnKeys.Select(AuditReportText.ColumnName).ToArray(),
                table.Columns.Select(c => c.DisplayName).ToArray());
            Assert.False(string.IsNullOrWhiteSpace(table.Title));
        });
    }

    /// <summary>
    /// ③ **接线契约**（源码级）：导出那一跳必须走 <see cref="UiOperationGuard"/>，
    /// 且按钮在 XAML 里存在、在代码里被订阅 —— 三处缺一处都会变成"点了没反应"。
    /// </summary>
    [Fact]
    public void ExportPath_GoesThroughUiOperationGuard_AndIsWired()
    {
        var root = SourceScanner.RepoRoot;
        var code = SourceScanner.StripComments(System.IO.File.ReadAllText(System.IO.Path.Combine(
            root, "src", "HmiEditor.App", "Views", "AuditLogView.axaml.cs")));
        var axaml = System.IO.File.ReadAllText(System.IO.Path.Combine(
            root, "src", "HmiEditor.App", "Views", "AuditLogView.axaml"));

        Assert.Contains("UiOperationGuard.Run(Lang.UiOpExportAuditReport", code);
        Assert.Contains("ExportReportBtn.Click +=", code);
        Assert.Contains("x:Name=\"ExportReportBtn\"", axaml);
        Assert.Contains("{res:Loc audit.report.btn}", axaml);
    }

    /// <summary>
    /// ③b **四格式契约**（阶段 173 立、阶段 174 把写盘收进 <see cref="ReportExport"/> 一处后改打在公共出口上）：
    /// 四种扩展名各调自己的写出器、未知扩展名回落 CSV；面板侧**不许**再出现写出器
    /// （后半条由 <c>AlarmReportExportGuardTests.BothPanels_UseTheSharedExport_AndKeepNoWriterSwitch</c> 钉）。
    /// </summary>
    [Fact]
    public void SharedExport_OwnsTheFourFormatMapping()
    {
        var code = SourceScanner.StripComments(System.IO.File.ReadAllText(System.IO.Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Services", "ReportExport.cs")));

        foreach (var (extension, writer) in new[]
                 {
                     (".xlsx", "XlsxReportWriter.Write"),
                     (".pdf", "PdfReportWriter.Write"),
                     (".html", "ReportExporter.ToHtml"),
                 })
        {
            Assert.Contains($"case \"{extension}\"", code, StringComparison.Ordinal);
            Assert.Contains(writer, code, StringComparison.Ordinal);
        }

        Assert.Contains("ReportExporter.ToCsvBytes", code, StringComparison.Ordinal);   // 默认支 = CSV
        Assert.Contains("default:", code, StringComparison.Ordinal);
        Assert.Contains("ReportExportNoFont", code, StringComparison.Ordinal);

        foreach (var filter in new[] { "ReportFilterCsv", "ReportFilterXlsx", "ReportFilterPdf", "ReportFilterHtml" })
            Assert.Contains(filter, code, StringComparison.Ordinal);
    }

    /// <summary>
    /// ④ **端到端确定性**：同一批条目、同一个"生成时间"字符串 ⇒ 两次导出的 CSV 字节与 HTML 文本**逐字节相同**
    /// （写盘只走 <see cref="TestTempDir"/>，不碰用户真实目录）。
    /// </summary>
    [Fact]
    public void Export_IsByteIdentical_ForSameInput()
    {
        UiTestHost.Run(() =>
        {
            var entries = new[] { Entry("op1", true, "a,b"), Entry("op2", false, "say \"hi\"") };

            byte[] CsvOnce() => ReportExporter.ToCsvBytes(AuditLogReport.From(entries, AuditReportText.Localizer));
            string HtmlOnce() => ReportExporter.ToHtml(
                AuditLogReport.From(entries, AuditReportText.Localizer),
                "2026-10-01 08:00:00Z", AuditReportText.GeneratedLabel, AppLocalization.Service.CurrentLanguage);

            var csv1 = CsvOnce();
            var csv2 = CsvOnce();
            Assert.Equal(csv1, csv2);

            var html1 = HtmlOnce();
            var html2 = HtmlOnce();
            Assert.Equal(html1, html2, StringComparer.Ordinal);
            Assert.Contains("a,b", html1, StringComparison.Ordinal);        // 单元格原样进 HTML（已转义规则另判）
            Assert.DoesNotContain("<script", html1, StringComparison.OrdinalIgnoreCase);
        });

        // 真的落一次盘：写进临时目录，验证"字节写出去"这条路（也顺手证明产物能被再读回来）
        var dir = TestTempDir.NewDir("audit-report");
        try
        {
            var path = System.IO.Path.Combine(dir, "audit.csv");
            UiTestHost.Run(() =>
            {
                var bytes = ReportExporter.ToCsvBytes(
                    AuditLogReport.From(new[] { Entry("op", true, "x") }, AuditReportText.Localizer));
                System.IO.File.WriteAllBytes(path, bytes);
            });

            var read = System.IO.File.ReadAllBytes(path);
            Assert.True(read.Length > 3 && read[0] == 0xEF && read[1] == 0xBB && read[2] == 0xBF,
                "落盘的 CSV 丢了 UTF-8 BOM");
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }
}
