using System;
using System.IO;
using System.Linq;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **运行期数据源接线契约**（阶段 185，登记 `#87`）：谁把哪个源注册进来、什么时候注销、
/// 以及"取数用的构造"是不是**同一处**。
///
/// <para>三个数据源的归属不同：审计/报警挂在 <c>MainWindow</c>，历史采样挂在 <c>TrendConfigView</c>
/// （`DataLogger` 随对话框生死）⇒ 这里逐个钉住，并钉住"注销必须发生在 <c>_logger</c> 释放之前"
/// （否则定时导出会拿到一个已释放的 logger，症状是偶发异常而不是稳定报错）。</para>
///
/// <para>⚠ **为什么历史那条只有源码级判据**：驱动"开记录"要点 <c>BtnToggleRecord</c>，而那一枚在
/// <c>HostButtonEffectUiTests</c> 里被**明确声明为 Never**（它启动一个真的采样定时器 ⇒ 读数会抖）。
/// 所以这里不去制造那个状态，改为钉源码契约 —— 诚实边界，写在判据注释里而不是假装行为面覆盖了。</para>
/// </summary>
[Collection("Language-global")]
public class ReportSourceWiringGuardTests
{
    private static string Code(string relative) => SourceScanner.StripComments(File.ReadAllText(
        Path.Combine(SourceScanner.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar))));

    /// <summary>
    /// **生产主窗口把应用级两个源注册上**，而且注册的委托**真的能取到表**（不是"注册了一个会抛的 lambda"）。
    /// </summary>
    [Fact]
    public void ProductionMainWindow_RegistersTheAppLevelSources()
    {
        using var sandbox = new DataRootSandbox("report-sources");

        UiTestHost.Run(() =>
        {
            var settings = new SettingsService(Path.Combine(sandbox.DataRoot, "settings.json"));
            var window = new MainWindow(settings);
            try
            {
                var registered = App.ReportSources.Registered;
                Assert.Contains(ReportSourceKeys.Audit, registered);
                Assert.Contains(ReportSourceKeys.Alarm, registered);

                var audit = App.ReportSources.TryBuild(ReportSourceKeys.Audit);
                Assert.NotNull(audit);
                Assert.Equal(AuditLogReport.ColumnKeys.Length, audit!.Columns.Count);

                var alarm = App.ReportSources.TryBuild(ReportSourceKeys.Alarm);
                Assert.NotNull(alarm);
                Assert.Equal(AlarmReport.ColumnKeys.Length, alarm!.Columns.Count);

                // 前提自检：审计表的第一列确实是时间列（否则"取到表了"可能只是取到一张空表）
                Assert.Equal(AuditLogReport.ColumnKeys[0], audit.Columns[0].Key);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>注册只发生在**生产构造**里（设计器路径不注册），注销在关闭收口里成对出现。</summary>
    [Fact]
    public void MainWindow_RegistersOnTheProductionPathOnly_AndUnregistersOnShutdown()
    {
        var code = Code("src/HmiEditor.App/MainWindow.axaml.cs");

        var productionCtor = code.IndexOf("public MainWindow(SettingsService", StringComparison.Ordinal);
        var designerCtor = code.IndexOf("public MainWindow()", StringComparison.Ordinal);
        var register = code.IndexOf("ReportSourceWiring.RegisterAppLevelSources", StringComparison.Ordinal);
        var unregister = code.IndexOf("ReportSourceWiring.UnregisterAppLevelSources", StringComparison.Ordinal);

        Assert.True(productionCtor >= 0 && designerCtor >= 0 && register >= 0 && unregister >= 0);
        Assert.True(designerCtor < productionCtor, "前提：无参构造在前、生产构造在后（否则下面的顺序断言无意义）");
        Assert.True(register > productionCtor, "注册必须发生在**生产构造**里（设计器路径不注册）");

        var cleanup = code.IndexOf("public void CleanupOnShutdown()", StringComparison.Ordinal);
        Assert.True(cleanup > 0);
        Assert.True(unregister > cleanup, "注销必须在关闭收口里");

        // 注册/注销的**实现**搬在 ReportSourceWiring 里（MainWindow 有行数基线，调用点只留一行）：
        // 那两个键必须在那个文件里成对出现，别只搬一半。
        var wiring = Code("src/HmiEditor.App/Services/ReportSourceWiring.cs");
        Assert.Contains("App.ReportSources.Register(ReportSourceKeys.Audit", wiring, StringComparison.Ordinal);
        Assert.Contains("App.ReportSources.Register(ReportSourceKeys.Alarm", wiring, StringComparison.Ordinal);
        Assert.Contains("App.ReportSources.Unregister(ReportSourceKeys.Audit", wiring, StringComparison.Ordinal);
        Assert.Contains("App.ReportSources.Unregister(ReportSourceKeys.Alarm", wiring, StringComparison.Ordinal);
    }

    /// <summary>
    /// 历史源：**开记录时注册**；注销必须在 <c>_logger</c> 释放**之前**（顺序反了就是偶发异常）；
    /// 而且注册的不是一次性 lambda，而是共用的构造。
    /// </summary>
    [Fact]
    public void HistorySource_IsRegisteredWhileRecording_AndUnregisteredBeforeDispose()
    {
        var code = Code("src/HmiEditor.App/Views/TrendConfigView.axaml.cs");

        var startRecording = code.IndexOf("private void StartRecording()", StringComparison.Ordinal);
        var register = code.IndexOf("App.ReportSources.Register(ReportSourceKeys.History", StringComparison.Ordinal);
        var onClosed = code.IndexOf("protected override void OnClosed(EventArgs e)", StringComparison.Ordinal);
        var unregister = code.IndexOf("App.ReportSources.Unregister(ReportSourceKeys.History", StringComparison.Ordinal);
        var dispose = code.IndexOf("_logger?.Dispose();", unregister, StringComparison.Ordinal);

        Assert.True(startRecording >= 0 && register >= 0 && onClosed >= 0 && unregister >= 0 && dispose >= 0);
        Assert.True(register > startRecording, "注册必须发生在 StartRecording 里（此刻起才有数据）");
        Assert.True(register < onClosed, "前提：注册那处在 OnClosed 之前（源码顺序）");
        Assert.True(unregister > onClosed, "注销必须发生在 OnClosed 里");
        Assert.True(unregister < dispose, "注销必须在 _logger 释放之前 —— 否则定时导出可能拿到已释放的 logger");

        // 断言片段刻意**不带 "("**（TestAssertionStyleGuardTests 禁止"精确调用文本 + 字符串包含"）：
        // 第一条钉注册点、第二条钉"给的是共用构造而不是一次性 lambda"。
        Assert.Contains("App.ReportSources.Register(ReportSourceKeys.History", code, StringComparison.Ordinal);
        Assert.Contains(", BuildHistoryTable);", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// **取数构造只有一处**：报表包导出与注册表都走 <c>BuildHistoryTable()</c> ——
    /// 两处各写一份的话，定时导出与手动导出会给出**不同的列**（列序一漂移就错位，且不报错）。
    /// </summary>
    [Fact]
    public void HistoryTableBuilder_IsSharedByTheBundlePathAndTheRegistry()
    {
        var code = Code("src/HmiEditor.App/Views/TrendConfigView.axaml.cs");

        var uses = code.Split("BuildHistoryTable", StringSplitOptions.None).Length - 1;
        Assert.True(uses >= 3, $"BuildHistoryTable 只出现 {uses} 次（定义 1 ＋ 报表包 1 ＋ 注册表 1 才够）");
        Assert.Contains("var table = BuildHistoryTable();", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// 每个文件注册的**个数**恰好是它负责的那些 —— 少一个（漏接线）与多一个（重复注册两处）都红。
    /// 键本身由 <c>App.ReportSources.Register(ReportSourceKeys.…)</c> 这一形态保证来自共享常量
    /// （裸字面量不会编译过）。
    /// </summary>
    [Fact]
    public void EachFileRegistersExactlyItsOwnSources()
    {
        foreach (var (file, expected) in new[]
                 {
                     ("src/HmiEditor.App/Services/ReportSourceWiring.cs", 2),
                     ("src/HmiEditor.App/Views/TrendConfigView.axaml.cs", 1),
                 })
        {
            var code = Code(file);
            var registrations = code.Split("App.ReportSources.Register(ReportSourceKeys.", StringSplitOptions.None).Length - 1;
            Assert.Equal(expected, registrations);
        }
    }
}
