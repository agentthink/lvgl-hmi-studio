using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **定时/自动导出的 App 侧判据**（阶段 186，登记 `#87`）：设置三个字段的往返 ·
/// 多源写盘真的落出"每源 × 四格式 ＋ 索引（含缺的源）" · 真接线（<c>DispatcherTimer</c> 起停）·
/// 以及"到点那一拍真的写盘"的端到端。
///
/// <para>⚠ 服务本身的排程口径（只跑一次 / 错过不补 / 失败不断链）在
/// <c>AutoExportServiceTests</c>（纯 Core 判据，不建窗口、不起表）。</para>
/// </summary>
[Collection("Language-global")]
public class AutoExportGuardTests
{
    private static readonly DateTimeOffset Now = new(new DateTime(2026, 10, 1, 8, 30, 0, DateTimeKind.Utc));

    private static ReportTable Table(string title) => new()
    {
        Title = title,
        Columns = new List<ReportColumn> { new("k", "K") },
        Rows = new List<IReadOnlyList<string>> { new[] { title } },
    };

    private static AutoExportWriteResult WriteReal(AutoExportWriteRequest request) =>
        WriteInto(request.Directory, request.Items, request.MissingSources, request.Timestamp);

    private static AutoExportWriteResult WriteInto(
        string directory,
        IReadOnlyList<ReportBundleItem> items,
        IReadOnlyList<string> missing,
        DateTimeOffset timestamp)
    {
        var result = ReportExport.WriteMultiBundle(
            directory, items, missing, source => $"{source}: unavailable", timestamp,
            ReportExporter.FormatUtc(timestamp.UtcDateTime), "generated", "zh-CN",
            "no font", typeface: null);
        Assert.Null(result.Error);
        return new AutoExportWriteResult(result.Written.Count, result.Skipped);
    }

    /// <summary>三个字段都要能往返，且默认值刻意"不干活"。</summary>
    [Fact]
    public void AutoExportSettings_RoundTrip_AndDefaultToOff()
    {
        var path = TestTempDir.NewFilePath("auto-export-settings", ".json");
        try
        {
            using (var svc = new SettingsService(path))
            {
                Assert.False(svc.AutoExport.Enabled);
                Assert.Equal(30, svc.AutoExport.IntervalMinutes);
                Assert.Equal("", svc.AutoExport.Directory);
                Assert.False(svc.AutoExport.Options().CanRun);

                svc.AutoExport.SetEnabled(true);
                svc.AutoExport.SetIntervalMinutes(15);
                svc.AutoExport.SetDirectory("/tmp/zhishu-auto");
            }

            using var reloaded = new SettingsService(path);
            Assert.True(reloaded.AutoExport.Enabled);
            Assert.Equal(15, reloaded.AutoExport.IntervalMinutes);
            Assert.Equal("/tmp/zhishu-auto", reloaded.AutoExport.Directory);
            Assert.True(reloaded.AutoExport.Options().CanRun);
        }
        finally
        {
            TestTempDir.DeleteFile(path);
        }
    }

    /// <summary>多源写盘：每个源 × 四种格式 ＋ 索引里有它们 ＋ **缺的源进 skipped**。</summary>
    [Fact]
    public void WriteMultiBundle_WritesEverySource_AndRecordsMissingOnes()
    {
        var dir = TestTempDir.NewDir("auto-export-bundle");
        try
        {
            var result = WriteInto(
                dir,
                new[]
                {
                    new ReportBundleItem(ReportSourceKeys.Audit, Table("audit")),
                    new ReportBundleItem(ReportSourceKeys.Alarm, Table("alarm")),
                },
                new[] { ReportSourceKeys.History },
                Now);

            Assert.Equal(4, ReportExport.Extensions.Length);   // 前提自检：渲染器认识四种格式

            foreach (var source in new[] { ReportSourceKeys.Audit, ReportSourceKeys.Alarm })
            {
                var written = ReportExport.Extensions.Count(e =>
                    File.Exists(Path.Combine(dir, ReportBundle.FileName(source, Now, e))));
                Assert.True(written >= 3, $"{source} 只写出 {written} 种格式（PDF 可能因缺字体而跳过）");
            }

            var index = File.ReadAllText(Path.Combine(dir, ReportBundle.IndexFileName));
            Assert.Contains(ReportBundle.FileName(ReportSourceKeys.Audit, Now, ".csv"), index, StringComparison.Ordinal);
            Assert.Contains(ReportBundle.FileName(ReportSourceKeys.Alarm, Now, ".csv"), index, StringComparison.Ordinal);
            Assert.Contains(ReportSourceKeys.History, index, StringComparison.Ordinal);
            Assert.Contains(result.Skipped, s => s.Contains(ReportSourceKeys.History, StringComparison.Ordinal));
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    /// <summary>单源那条与多源那条**同一份实现**：同一个源、同一时刻，产物集合一致。</summary>
    [Fact]
    public void SingleSourceBundle_AndMultiSourceBundle_Agree()
    {
        var single = TestTempDir.NewDir("bundle-single");
        var multi = TestTempDir.NewDir("bundle-multi");
        try
        {
            var table = Table("audit");
            var one = ReportExport.WriteBundle(
                single, ReportSourceKeys.Audit, table, Now,
                ReportExporter.FormatUtc(Now.UtcDateTime), "generated", "zh-CN", "no font", typeface: null);

            var many = ReportExport.WriteMultiBundle(
                multi, new[] { new ReportBundleItem(ReportSourceKeys.Audit, table) },
                Array.Empty<string>(), _ => "", Now,
                ReportExporter.FormatUtc(Now.UtcDateTime), "generated", "zh-CN", "no font", typeface: null);

            Assert.Equal(one.Written.OrderBy(n => n, StringComparer.Ordinal),
                         many.Written.OrderBy(n => n, StringComparer.Ordinal));

            foreach (var name in one.Written)
                Assert.True(File.Exists(Path.Combine(multi, name)), $"{name} 在多源目录里没有");
        }
        finally
        {
            TestTempDir.DeleteDir(single);
            TestTempDir.DeleteDir(multi);
        }
    }

    /// <summary>
    /// **端到端**（假时钟 ＋ 真写盘 ＋ 真文件夹）：到点那一拍在配置目录里落出四种格式与索引，
    /// 索引里还写着"这次缺了哪个源"。
    /// </summary>
    [Fact]
    public void DueTick_WritesRealFilesIntoTheConfiguredDirectory()
    {
        var dir = TestTempDir.NewDir("auto-export-e2e");
        try
        {
            var now = Now;
            var service = new AutoExportService
            {
                Now = () => now,
                Options = () => new AutoExportOptions(true, 30, dir),
                RegisteredSources = () => new[] { ReportSourceKeys.Audit },
                MissingSources = () => new[] { ReportSourceKeys.Alarm, ReportSourceKeys.History },
                BuildSource = _ => Table("audit"),
                Write = WriteReal,
            };

            Assert.False(service.TickOnce());     // 起跑那一刻不导
            now = now.AddMinutes(30);
            Assert.True(service.TickOnce());      // 到点 ⇒ 真的写

            Assert.True(File.Exists(Path.Combine(dir, ReportBundle.FileName(ReportSourceKeys.Audit, now, ".csv"))));
            Assert.True(File.Exists(Path.Combine(dir, ReportBundle.FileName(ReportSourceKeys.Audit, now, ".html"))));
            Assert.True(File.Exists(Path.Combine(dir, ReportBundle.IndexFileName)));

            var index = File.ReadAllText(Path.Combine(dir, ReportBundle.IndexFileName));
            Assert.Contains(ReportSourceKeys.Alarm, index, StringComparison.Ordinal);
            Assert.Equal(1, service.Runs);
        }
        finally
        {
            TestTempDir.DeleteDir(dir);
        }
    }

    /// <summary>
    /// 真接线：<c>AutoExportWiring</c> 起停成对、且真的读注册表与设置（不是空转的表）。
    /// ⚠ <c>DispatcherTimer</c> 要 UI 线程 ⇒ 这条必须在 headless 宿主里跑。
    /// </summary>
    [Fact]
    public void Wiring_StartsAndStopsOnTheUiThread()
    {
        UiTestHost.Run(() =>
        {
            AutoExportWiring.Stop();   // 前提：从干净状态开始（别的用例可能经过 MainWindow 把它起过）
            try
            {
                Assert.Null(AutoExportWiring.Current);

                AutoExportWiring.Start();
                var service = AutoExportWiring.Current;
                Assert.NotNull(service);
                Assert.NotNull(service!.AnchorUtc);    // 起表时就记锚点 ⇒ 第一拍不该立刻导

                AutoExportWiring.Start();              // 重复起步是空操作
                Assert.Same(service, AutoExportWiring.Current);

                AutoExportWiring.Stop();
                Assert.Null(AutoExportWiring.Current);
            }
            finally
            {
                AutoExportWiring.Stop();
            }
        });
    }

    /// <summary>接线的源码契约：读注册表/设置、走多源写盘、由 <c>ReportSourceWiring</c> 起停（先停表再注销）。</summary>
    [Fact]
    public void WiringContract_ReadsTheRegistryAndIsDrivenByTheCompositionRoot()
    {
        var wiring = SourceScanner.StripComments(File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Services", "AutoExportWiring.cs")));

        foreach (var fragment in new[]
                 {
                     "App.SettingsService", "App.ReportSources.Registered", "App.ReportSources.Missing",
                     "App.ReportSources.TryBuild", "ReportExport.WriteMultiBundle", "UiOperationGuard.UiSink",
                 })
        {
            Assert.Contains(fragment, wiring, StringComparison.Ordinal);
        }

        var sources = SourceScanner.StripComments(File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Services", "ReportSourceWiring.cs")));

        Assert.Contains("AutoExportWiring.Start()", sources, StringComparison.Ordinal);
        Assert.Contains("AutoExportWiring.Stop()", sources, StringComparison.Ordinal);

        // 先停表、再注销数据源：反过来会让竞态中的那一拍读到"正在被撤走的"源
        var stop = sources.IndexOf("AutoExportWiring.Stop()", StringComparison.Ordinal);
        var unregister = sources.IndexOf("App.ReportSources.Unregister(ReportSourceKeys.Audit", StringComparison.Ordinal);
        Assert.True(stop > 0 && unregister > 0 && stop < unregister, "停表必须早于注销数据源");
    }

    /// <summary>四种结论各自的说法都在两本字典里，而"还没到点"刻意**不发声**。</summary>
    [Fact]
    public void OutcomeMessages_ExistInBothLanguages_AndNotDueIsSilent()
    {
        var zh = PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
        var en = PluginTextLocalizationGuardTests.LoadDictionary("en-US");
        foreach (var key in new[]
                 {
                     "report.auto.ok", "report.auto.failed",
                     "report.auto.not.configured", "report.auto.no.sources", "report.auto.missing.source",
                 })
        {
            Assert.True(zh.ContainsKey(key), $"zh 字典缺键：{key}");
            Assert.True(en.ContainsKey(key), $"en 字典缺键：{key}");
        }

        UiTestHost.Run(() =>
        {
            Assert.Equal("", AutoExportWiring.Describe(new AutoExportTick(AutoExportOutcome.NotDue, 0, null)));
            Assert.False(string.IsNullOrWhiteSpace(
                AutoExportWiring.Describe(new AutoExportTick(AutoExportOutcome.Written, 3, null))));
            Assert.False(string.IsNullOrWhiteSpace(
                AutoExportWiring.Describe(new AutoExportTick(AutoExportOutcome.Failed, 0, "boom"))));
            Assert.False(string.IsNullOrWhiteSpace(
                AutoExportWiring.Describe(new AutoExportTick(AutoExportOutcome.NoSources, 0, null))));
            Assert.False(string.IsNullOrWhiteSpace(
                AutoExportWiring.Describe(new AutoExportTick(AutoExportOutcome.NotConfigured, 0, null))));
        });
    }
}
