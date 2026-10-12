using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **报表时间范围判据（App 侧）**（阶段 178）：快捷档 → 范围的纯映射 ＋ 三个面板都先问范围再导出 ＋
/// 对话框要的文案两本字典都在。
///
/// <para>⚠ 对话框本身由 <c>WindowViewSmokeUiTests</c> 的**自动收面**覆盖（按类型关系收）。</para>
/// </summary>
public class ReportRangeSelectionGuardTests
{
    private static readonly Dictionary<string, string> Zh = PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static readonly Dictionary<string, string> En = PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    private static readonly DateTimeOffset Now = new(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Compute_MapsEachChoiceToTheExpectedSpan()
    {
        Assert.Null(ReportRangeOptions.Compute(ReportRangeChoice.All, Now));   // 全部 ⇒ 不设范围

        var hour = ReportRangeOptions.Compute(ReportRangeChoice.LastHour, Now)!.Value;
        Assert.Equal(Now.UtcDateTime.AddHours(-1), hour.From);
        Assert.Equal(Now.UtcDateTime, hour.To);

        Assert.Equal(Now.UtcDateTime.AddHours(-24), ReportRangeOptions.Compute(ReportRangeChoice.Last24Hours, Now)!.Value.From);
        Assert.Equal(Now.UtcDateTime.AddDays(-7), ReportRangeOptions.Compute(ReportRangeChoice.Last7Days, Now)!.Value.From);
        Assert.Equal(Now.UtcDateTime.AddDays(-30), ReportRangeOptions.Compute(ReportRangeChoice.Last30Days, Now)!.Value.From);

        // 端点都算在内（闭区间）—— 用 "现在" 自身核对
        Assert.True(hour.Contains(Now.UtcDateTime));
    }

    /// <summary>五个档位各自一个键、两本字典都在（顺序与界面列出的顺序一致）。</summary>
    [Fact]
    public void EveryChoice_HasItsOwnKeyInBothDictionaries()
    {
        var keys = ReportRangeOptions.All.Select(ReportRangeOptions.LabelKey).ToArray();
        Assert.Equal(keys.Length, keys.Distinct().Count());

        foreach (var key in keys.Concat(new[] { "report.range.title", "report.range.hint" }))
        {
            Assert.True(Zh.ContainsKey(key), $"zh 字典缺键：{key}");
            Assert.True(En.ContainsKey(key), $"en 字典缺键：{key}");
        }

        UiTestHost.Run(() =>
        {
            // App 侧确实按 Lang 常量渲染（不是把键名显示给用户）
            foreach (var choice in ReportRangeOptions.All)
            {
                var text = AppLocalization.Service[ReportRangeOptions.LabelKey(choice)];
                Assert.False(string.Equals(text, ReportRangeOptions.LabelKey(choice), StringComparison.Ordinal),
                    $"{choice} 的文案回落成了键名");
            }
        });
    }

    /// <summary>
    /// **接线契约**（源码级）：三个报表面板都要**先问范围**（取消即中止），并把范围传给各自的数据源；
    /// 且都用 <c>ReportRangeOptions.Compute</c> 这一处口径（不许各算一份）。
    /// </summary>
    [Fact]
    public void AllReportPanels_AskForRangeBeforeSaving()
    {
        foreach (var view in new[] { "AuditLogView.axaml.cs", "AlarmListView.axaml.cs", "TrendConfigView.axaml.cs" })
        {
            var code = SourceScanner.StripComments(System.IO.File.ReadAllText(
                System.IO.Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", view)));

            Assert.Contains("ReportExport.ChooseRangeAsync", code, StringComparison.Ordinal);
            Assert.Contains("ReportRangeOptions.Compute", code, StringComparison.Ordinal);
            Assert.Contains("ReportExport.ChooseColumnsAsync", code, StringComparison.Ordinal);
        }

        // 而且**算出来的范围真的传给了各自的数据源**（只问不用 = 白问）
        // ⚠ 断言片段刻意不带括号（TestAssertionStyleGuardTests 禁止"精确调用文本 + 字符串包含"）
        var root = System.IO.Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views");
        string Code(string file) => SourceScanner.StripComments(System.IO.File.ReadAllText(System.IO.Path.Combine(root, file)));

        Assert.Contains("AuditReportText.Localizer, range", Code("AuditLogView.axaml.cs"), StringComparison.Ordinal);
        Assert.Contains("AlarmReportText.Localizer, range", Code("AlarmListView.axaml.cs"), StringComparison.Ordinal);
        Assert.Contains("range: range", Code("TrendConfigView.axaml.cs"), StringComparison.Ordinal);
    }

    /// <summary>范围对话框窗口的控件齐全（XAML 侧契约）。</summary>
    [Fact]
    public void RangeDialog_HasTheExpectedControls()
    {
        var axaml = System.IO.File.ReadAllText(System.IO.Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", "ReportRangeDialog.axaml"));

        foreach (var name in new[]
                 {
                     "ChoiceList", "BtnOk", "BtnCancel", "BtnTitleClose",
                     // 自定义起止日期那一行（阶段 180）
                     "CustomPanel", "LblFrom", "FromPicker", "LblTo", "ToPicker",
                 })
            Assert.Contains($"x:Name=\"{name}\"", axaml, StringComparison.Ordinal);
    }

    /// <summary>
    /// **两份抄本必须一致**（阶段 180）：Core 的键（<see cref="ReportRangeOptions.LabelKey"/>）
    /// 与 App 的 <see cref="ReportRangeText.Key"/> 各列一份映射 —— 漏一档、或把两档指到同一个键，
    /// 界面上就是"少了一档"或"两档同名"，而两者都不会编译失败。
    /// </summary>
    [Fact]
    public void ReportRangeText_AgreesWithCoreKeys()
    {
        var coreKeys = new List<string>();
        foreach (var choice in ReportRangeOptions.All)
        {
            Assert.Equal(ReportRangeOptions.LabelKey(choice), ReportRangeText.Key(choice));
            coreKeys.Add(ReportRangeText.Key(choice));
        }

        Assert.Equal(coreKeys.Count, coreKeys.Distinct().Count());
        Assert.Equal(ReportRangeOptions.All.Length, coreKeys.Count);

        UiTestHost.Run(() =>
        {
            foreach (var choice in ReportRangeOptions.All)
                Assert.False(string.Equals(ReportRangeText.Label(choice), ReportRangeText.Key(choice), StringComparison.Ordinal),
                    $"{choice} 的文案回落成了键名");

            Assert.False(string.IsNullOrWhiteSpace(ReportRangeText.FromLabel));
            Assert.False(string.IsNullOrWhiteSpace(ReportRangeText.ToLabel));
            Assert.NotEqual(ReportRangeText.FromLabel, ReportRangeText.ToLabel);
        });
    }

    /// <summary>
    /// **对话框把自定义日期带出来、且挡住"两端都空"**（阶段 180，走**生产构造路径**）：
    ///
    /// <list type="number">
    /// <item>初始档为"全部"⇒ 自定义面板不显示、确定可点；</item>
    /// <item>切到"自定义"⇒ 面板显示，且**两端都空时确定不可点**（点下去 Core 会抛）；</item>
    /// <item>填上任一端 ⇒ 确定可点，<c>Selected()</c> 把带偏移的日期原样带出来；</item>
    /// <item>切回"全部"⇒ 确定恢复可点，且**日期不再被带出**（切档位的残留不该继续生效）。</item>
    /// </list>
    ///
    /// <para>前置自检：控件都得找到、档位按钮真的建出来了 —— 少一条，"什么都没跑"会假绿。</para>
    /// </summary>
    [Fact]
    public void RangeDialog_CarriesCustomDates_AndBlocksEmptyCustom()
    {
        UiTestHost.Run(() =>
        {
            var dialog = new Views.ReportRangeDialog(new ReportRangeSelection(ReportRangeChoice.All));
            dialog.Show();
            UiTestHost.Pump();

            var choices = dialog.FindControl<StackPanel>("ChoiceList");
            var customPanel = dialog.FindControl<Border>("CustomPanel");
            var ok = dialog.FindControl<Button>("BtnOk");
            var from = dialog.FindControl<DatePicker>("FromPicker");
            var to = dialog.FindControl<DatePicker>("ToPicker");
            Assert.NotNull(choices);
            Assert.NotNull(customPanel);
            Assert.NotNull(ok);
            Assert.NotNull(from);
            Assert.NotNull(to);

            var radios = choices!.Children.OfType<RadioButton>().ToList();
            Assert.Equal(ReportRangeOptions.All.Length, radios.Count);   // 前置自检：档位真的建了

            var customRadio = radios.Single(r =>
                string.Equals(r.Content as string, ReportRangeText.Label(ReportRangeChoice.Custom), StringComparison.Ordinal));

            // ① 初始"全部"
            Assert.False(customPanel!.IsVisible);
            Assert.True(ok!.IsEnabled);
            Assert.Equal(ReportRangeChoice.All, dialog.Selected().Choice);
            Assert.Null(dialog.Selected().CustomFrom);

            // ② 切到"自定义"、两端都空 ⇒ 面板出来、确定挡住
            customRadio.IsChecked = true;
            UiTestHost.Pump(2);
            Assert.True(customPanel.IsVisible);
            Assert.False(ok.IsEnabled, "自定义档两端都空时确定键必须不可点");

            // ③ 填一端 ⇒ 可点，且日期原样带出（偏移一路留着）
            var fromDate = new DateTimeOffset(new DateTime(2026, 9, 28), TimeSpan.FromHours(8));
            from!.SelectedDate = fromDate;
            UiTestHost.Pump(2);
            Assert.True(ok.IsEnabled);
            Assert.Equal(ReportRangeChoice.Custom, dialog.Selected().Choice);
            Assert.Equal(fromDate, dialog.Selected().CustomFrom);
            Assert.Null(dialog.Selected().CustomTo);

            var toDate = new DateTimeOffset(new DateTime(2026, 10, 1), TimeSpan.FromHours(8));
            to!.SelectedDate = toDate;
            UiTestHost.Pump(2);
            Assert.Equal(fromDate, dialog.Selected().CustomFrom);
            Assert.Equal(toDate, dialog.Selected().CustomTo);

            // ④ 切回快捷档 ⇒ 恢复可点、日期不带出
            radios[0].IsChecked = true;
            UiTestHost.Pump(2);
            Assert.False(customPanel.IsVisible);
            Assert.True(ok.IsEnabled);
            Assert.Equal(ReportRangeChoice.All, dialog.Selected().Choice);
            Assert.Null(dialog.Selected().CustomFrom);
            Assert.Null(dialog.Selected().CustomTo);

            dialog.Close();
        });
    }
}
