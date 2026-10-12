using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **历史数据报表导出判据**（阶段 175）：标题/时间列名两本字典都在；变量列名**保持 Id 原文**；
/// 公共出口能渲染它；趋势面板走公共出口且**列名取采样时的配置**（不是当前画笔，否则列与值错位）。
/// </summary>
public class HistoryReportExportGuardTests
{
    private static readonly Dictionary<string, string> Zh = PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static readonly Dictionary<string, string> En = PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    [Fact]
    public void TitleAndTimeColumnKey_ExistInBothDictionaries()
    {
        foreach (var key in new[] { HistoryReport.TitleKey, HistoryReport.TimeColumnKey })
        {
            Assert.True(Zh.ContainsKey(key), $"zh 字典缺键：{key}");
            Assert.True(En.ContainsKey(key), $"en 字典缺键：{key}");
        }

        UiTestHost.Run(() =>
        {
            Assert.False(string.Equals(HistoryReportText.Title, HistoryReport.TitleKey, StringComparison.Ordinal),
                "标题没有映射到 Lang 常量（回落成了键名）");
            Assert.False(string.Equals(HistoryReportText.Localizer(HistoryReport.TimeColumnKey), HistoryReport.TimeColumnKey,
                    StringComparison.Ordinal),
                "时间列名没有映射到 Lang 常量（回落成了键名）");
            // 变量列名**必须**保持 Id 原文（数据标识符不翻）
            Assert.Equal("Temp", HistoryReportText.Localizer("Temp"));
        });
    }

    [Fact]
    public void SharedExport_RendersTheHistoryReport()
    {
        UiTestHost.Run(() =>
        {
            var samples = new[]
            {
                new TrendSample
                {
                    Timestamp = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    Values = new List<double> { 1.25, 2.5 },
                },
            };
            var table = HistoryReport.From(samples, new[] { "Temp", "Pressure" }, HistoryReportText.Localizer);

            foreach (var extension in ReportExport.Extensions)
            {
                var rendered = ReportExport.Render(
                    table, extension, "2026-10-01 00:00:00Z",
                    AppLocalization.Service[Lang.AuditReportGenerated], "zh-CN",
                    AppLocalization.Service[Lang.ReportExportNoFont],
                    typeface: null,   // 这里只关心"非 PDF 的三种能出字节"，PDF 的缺字体路径另有判据
                    creation: DateTimeOffset.UnixEpoch);

                if (extension == ".pdf")
                    Assert.Equal(AppLocalization.Service[Lang.ReportExportNoFont], rendered.Message);
                else
                    Assert.True(rendered.Bytes is { Length: > 0 }, $"{extension} 渲染失败：{rendered.Message}");
            }
        });
    }

    /// <summary>
    /// **CSV 覆盖形态的接缝**（阶段 176）：历史数据的 CSV 走 `DataLogger.ExportCsvBytes()`
    /// （设备列序 ＋ 表头写明时间基），而 xlsx/pdf/html 仍走报表列 —— 覆盖**只影响 CSV**。
    /// </summary>
    [Fact]
    public void CsvBranch_PrefersTheTablesOwnCsvForm()
    {
        UiTestHost.Run(() =>
        {
            var samples = new[]
            {
                new TrendSample
                {
                    Timestamp = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    Values = new List<double> { 1.0 },
                },
            };
            var ids = new[] { "Temp" };
            var plain = HistoryReport.From(samples, ids, HistoryReportText.Localizer);
            var overrideBytes = new byte[] { 0xEF, 0xBB, 0xBF, 0x41, 0x0A };   // "A\n" 带 BOM
            var special = HistoryReport.From(samples, ids, HistoryReportText.Localizer, csvOverride: overrideBytes);

            var renderedPlain = ReportExport.Render(plain, ".csv", "t", "g", "zh-CN", "no-font", null, DateTimeOffset.UnixEpoch);
            var renderedSpecial = ReportExport.Render(special, ".csv", "t", "g", "zh-CN", "no-font", null, DateTimeOffset.UnixEpoch);

            Assert.Equal(ReportExporter.ToCsvBytes(plain), renderedPlain.Bytes);   // 没带覆盖 ⇒ 通用 CSV
            Assert.Equal(overrideBytes, renderedSpecial.Bytes);                    // 带了 ⇒ 用它的

            // 覆盖**只影响 CSV**：xlsx 仍然按报表列渲染（不是那份覆盖字节）
            var xlsx = ReportExport.Render(special, ".xlsx", "t", "g", "zh-CN", "no-font", null, DateTimeOffset.UnixEpoch);
            Assert.True(xlsx.Bytes is { Length: > 0 });
            Assert.NotEqual(overrideBytes, xlsx.Bytes);
        });
    }

    /// <summary>
    /// **接线契约**（源码级）：趋势面板走公共出口、**不再自己写盘**，
    /// 且列名取 **logger 的配置**（`_logger?.Config.VariableIds`）而不是当前画笔 —— 后者会让列与值错位。
    /// </summary>
    [Fact]
    public void TrendPanel_UsesSharedExport_AndLabelsColumnsFromTheLoggerConfig()
    {
        var code = SourceScanner.StripComments(System.IO.File.ReadAllText(System.IO.Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", "TrendConfigView.axaml.cs")));

        Assert.Contains("ReportExport.SaveAsync", code, StringComparison.Ordinal);
        Assert.Contains("UiOperationGuard.Run(Lang.UiOpExportTrendReport", code, StringComparison.Ordinal);
        Assert.Contains("_logger?.Config.VariableIds", code, StringComparison.Ordinal);
        Assert.Contains("HistoryReport.From", code, StringComparison.Ordinal);
        Assert.Contains("ExportCsvBytes", code, StringComparison.Ordinal);   // CSV 走设备列序那一份
        Assert.Contains("csvOverride", code, StringComparison.Ordinal);

        foreach (var writer in new[] { "XlsxReportWriter.Write", "PdfReportWriter.Write", "ReportExporter.ToCsvBytes", "ReportExporter.ToHtml" })
            Assert.DoesNotContain(writer, code, StringComparison.Ordinal);

        // "画笔 → 变量 Id 顺序"只能有一处（开记录与导报表共用），否则顺序分叉 ⇒ 列与值错位
        var axaml = System.IO.File.ReadAllText(System.IO.Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", "TrendConfigView.axaml"));
        Assert.Contains("x:Name=\"BtnExportReport\"", axaml, StringComparison.Ordinal);
        Assert.Contains("{res:Loc trend.report.btn}", axaml, StringComparison.Ordinal);
        Assert.Contains("BtnExportReport.Click +=", code, StringComparison.Ordinal);
    }
}
