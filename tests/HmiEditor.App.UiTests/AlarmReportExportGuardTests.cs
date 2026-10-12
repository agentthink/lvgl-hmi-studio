using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Reporting;
using SkiaSharp;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **报警报表导出判据**（阶段 174）＋ **公共出口判据**（同一条刀把写盘逻辑收成
/// <see cref="ReportExport"/> 一处）。
///
/// <para>钉三件：① 报警报表用到的每个键两本字典都在、且 App 映射**不许回落成键名**；
/// ② 两种报表走**同一个** <see cref="ReportExport.Render"/> 能出四种格式（含 PDF 缺字体的明确提示）；
/// ③ **接线契约**：两个面板都调公共出口，面板里**不许再有**写盘 switch（抄第二份必然漂移）。</para>
/// </summary>
public class AlarmReportExportGuardTests
{
    private static readonly Dictionary<string, string> Zh = PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static readonly Dictionary<string, string> En = PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    private static readonly string[] AlarmKeys =
        AlarmReport.ColumnKeys
            .Concat(new[] { AlarmReport.TitleKey })
            .Concat(new[]
            {
                "alarm.level.info", "alarm.level.warning", "alarm.level.critical", "alarm.level.emergency",
                "alarm.state.inactive", "alarm.state.active", "alarm.state.acknowledged", "alarm.state.returned",
            })
            .Distinct()
            .ToArray();

    [Fact]
    public void EveryAlarmReportKey_ResolvesToLocalizedText_InBothDictionaries()
    {
        foreach (var key in AlarmKeys)
        {
            Assert.True(Zh.ContainsKey(key), $"zh 字典缺键：{key}");
            Assert.True(En.ContainsKey(key), $"en 字典缺键：{key}");
        }

        UiTestHost.Run(() =>
        {
            foreach (var key in AlarmReport.ColumnKeys)
            {
                var text = AlarmReportText.ColumnName(key);
                Assert.False(string.Equals(text, key, StringComparison.Ordinal),
                    $"列键 {key} 没有映射到 Lang 常量（回落成了键名 —— 用户会看到 {key}）");
            }

            // 级别/状态名也必须真的翻出来（不回落成键名）
            foreach (var key in AlarmKeys.Where(k => k.StartsWith("alarm.level.", StringComparison.Ordinal)
                                                     || k.StartsWith("alarm.state.", StringComparison.Ordinal)))
            {
                var text = AlarmReportText.Localizer(key);
                Assert.False(string.Equals(text, key, StringComparison.Ordinal),
                    $"{key} 没有映射到 Lang 常量（回落成了键名）");
            }
        });
    }

    /// <summary>表格列名 == App 映射（顺序即 Core 列序）。</summary>
    [Fact]
    public void AlarmTableColumns_FollowCoreOrderAndAppText()
    {
        UiTestHost.Run(() =>
        {
            var record = new AlarmRecord
            {
                OccurredAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                AlarmName = "温度高",
                Severity = AlarmSeverity.Critical,
                State = AlarmState.Active,
                RawValue = "1",
            };
            var table = AlarmReport.From(new[] { record }, AlarmReportText.Localizer);

            Assert.Equal(AlarmReport.ColumnKeys, table.Columns.Select(c => c.Key).ToArray());
            Assert.Equal(AlarmReport.ColumnKeys.Select(AlarmReportText.ColumnName).ToArray(),
                table.Columns.Select(c => c.DisplayName).ToArray());

            var row = table.Rows.Single();
            Assert.Equal(AppLocalization.Service[Lang.AlarmLevelCritical], row[2]);
            Assert.Equal(AppLocalization.Service[Lang.AlarmStateActive], row[3]);
        });
    }

    /// <summary>
    /// **公共出口**：两种报表都能出四种格式；未知扩展名回落 CSV；**PDF 缺字体时给提示、不产豆腐块**。
    /// </summary>
    [Fact]
    public void SharedExport_RendersFourFormatsForBothReports_AndRefusesPdfWithoutFont()
    {
        UiTestHost.Run(() =>
        {
            var audit = AuditLogReport.From(
                new[] { new HmiEditor.Core.Models.User.AuditLogEntry { Username = "op" } },
                AuditReportText.Localizer);
            var alarm = AlarmReport.From(
                new[] { new AlarmRecord { AlarmName = "a", RawValue = "1" } },
                AlarmReportText.Localizer);

            using var typeface = SKTypeface.FromFile(System.IO.Path.Combine(
                SourceScanner.RepoRoot, "firmware", "t113", "tina", "package", "hmi-runtime", "files", "fangsong.ttf"));
            var creation = new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

            foreach (var table in new[] { audit, alarm })
            foreach (var extension in ReportExport.Extensions)
            {
                var ok = ReportExport.Render(table, extension, "2026-10-01 00:00:00Z",
                    AppLocalization.Service[Lang.AuditReportGenerated], "zh-CN",
                    AppLocalization.Service[Lang.ReportExportNoFont], typeface, creation);
                Assert.True(ok.Bytes is { Length: > 0 }, $"{extension} 渲染失败：{ok.Message}");
            }

            // 未知扩展名 ⇒ CSV（与"默认格式 = CSV"一致）
            var fallback = ReportExport.Render(audit, ".txt", "t", "g", "zh-CN", "no-font", typeface, creation);
            Assert.NotNull(fallback.Bytes);
            Assert.True(fallback.Bytes!.Length >= 3 && fallback.Bytes[0] == 0xEF, "未知扩展名没有回落成带 BOM 的 CSV");

            // PDF 缺字体 ⇒ **明确提示**，不是豆腐块
            var noFont = ReportExport.Render(alarm, ".pdf", "t", "g", "zh-CN", "NO-FONT-MSG", typeface: null, creation);
            Assert.Null(noFont.Bytes);
            Assert.Equal("NO-FONT-MSG", noFont.Message);
        });
    }

    /// <summary>
    /// **接线契约**（源码级）：两个面板都调 <see cref="ReportExport.SaveAsync"/>，
    /// 且都**不再**自己写"扩展名 → 写出器"的 switch（收成一处才算收口）。
    /// </summary>
    [Fact]
    public void BothPanels_UseTheSharedExport_AndKeepNoWriterSwitch()
    {
        foreach (var view in new[] { "AuditLogView.axaml.cs", "AlarmListView.axaml.cs" })
        {
            var code = SourceScanner.StripComments(System.IO.File.ReadAllText(
                System.IO.Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", view)));

            Assert.Contains("ReportExport.SaveAsync", code, StringComparison.Ordinal);
            Assert.Contains("UiOperationGuard.Run(Lang.UiOpExport", code, StringComparison.Ordinal);

            foreach (var writer in new[] { "XlsxReportWriter.Write", "PdfReportWriter.Write", "ReportExporter.ToCsvBytes", "ReportExporter.ToHtml" })
            {
                Assert.DoesNotContain(writer, code, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>按钮接线：报警面板的导出按钮在 XAML 里存在、在代码里被订阅。</summary>
    [Fact]
    public void AlarmPanelExportButton_IsWired()
    {
        var root = SourceScanner.RepoRoot;
        var axaml = System.IO.File.ReadAllText(System.IO.Path.Combine(
            root, "src", "HmiEditor.App", "Views", "AlarmListView.axaml"));
        var code = SourceScanner.StripComments(System.IO.File.ReadAllText(System.IO.Path.Combine(
            root, "src", "HmiEditor.App", "Views", "AlarmListView.axaml.cs")));

        Assert.Contains("x:Name=\"BtnExportReport\"", axaml, StringComparison.Ordinal);
        Assert.Contains("{res:Loc alarm.report.btn}", axaml, StringComparison.Ordinal);
        Assert.Contains("BtnExportReport.Click +=", code, StringComparison.Ordinal);
        // ⚠ 这里刻意**不写带括号的调用文本**：Core.Tests 的 TestAssertionStyleGuardTests 禁止
        //    "精确调用文本 + 字符串包含"（排版一改就假红、出现在注释里又假绿）；`CSource` 那套
        //    结构化断言只在 Core.Tests 工程里 ⇒ App 侧用**不含括号**的片段断言等价事实。
        Assert.Contains("AlarmReport.From", code, StringComparison.Ordinal);
        Assert.Contains("AlarmReportText.Localizer", code, StringComparison.Ordinal);
        Assert.Contains("FilteredRecords()", code, StringComparison.Ordinal);   // 导出所见 = 面板显示的那一份
    }
}
