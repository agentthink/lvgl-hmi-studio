using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **「自动导出」设置面的判据**（阶段 187，登记 `#87` 收口片）：控件 ↔ 设置的双向接线
/// （载入 / 回写）· 面板与分类导航 · 键双语 · 以及"设置真的能喂给服务"那条闭环。
///
/// <para>⚠ 本类**不点**「选择目录…」：那一枚走 <c>StorageProvider</c>，headless 下退化为平台桩
/// （<c>HostButtonEffectUiTests</c> 里声明为 <c>Never</c>）—— 它的接线由源码契约与"取消即不改值"
/// 这两条覆盖。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class AutoExportSettingsUiTests
{
    private static T Find<T>(Window window, string name) where T : Control
    {
        var found = window.FindControl<T>(name);
        Assert.NotNull(found);
        return found!;
    }

    /// <summary>把分类导航拨到「自动导出」，并返回那张面板（同时在场的自检：它真的显示出来了）。</summary>
    private static Panel OpenAutoExportPanel(SettingsDialog dialog)
    {
        var list = Find<ListBox>(dialog, "CategoryList");
        var item = list.Items.OfType<ListBoxItem>()
            .Single(i => string.Equals(i.Tag as string, "autoexport", StringComparison.Ordinal));
        list.SelectedItem = item;
        UiTestHost.Pump(2);

        var panel = Find<Panel>(dialog, "PanelAutoExport");
        Assert.True(panel.IsVisible, "「自动导出」面板没有显示出来 —— 下面的断言都不成立");
        return panel;
    }

    /// <summary>控件 ↔ 设置：载入显示当前值，改动立刻回写。</summary>
    [Fact]
    public void Dialog_LoadsAndWritesBackTheAutoExportSettings()
    {
        using var sandbox = new DataRootSandbox("autoexport-ui");
        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            depot.Settings.AutoExport.SetEnabled(true);
            depot.Settings.AutoExport.SetIntervalMinutes(15);
            depot.Settings.AutoExport.SetDirectory("/tmp/zhishu-ui");

            var dialog = new SettingsDialog(depot.Settings, depot.Theme);
            dialog.Show();
            UiTestHost.Pump(2);
            try
            {
                OpenAutoExportPanel(dialog);

                // 载入方向：控件显示的是设置里已有的值
                Assert.True(Find<CheckBox>(dialog, "ChkAutoExport").IsChecked);
                Assert.Equal(15, (int)(Find<NumericUpDown>(dialog, "NumAutoExportInterval").Value ?? 0));
                Assert.Equal("/tmp/zhishu-ui", Find<TextBox>(dialog, "TxtAutoExportDirectory").Text);

                // 回写方向：三条各改一次，设置跟着变
                Find<CheckBox>(dialog, "ChkAutoExport").IsChecked = false;
                Find<NumericUpDown>(dialog, "NumAutoExportInterval").Value = 45;

                // ⚠ 目录那一枚要**按框架的做法补一次事件**：实测（阶段 187）程序化写 `TextBox.Text`
                //    **不会**触发 `TextChanged`（真实输入路径才触发）⇒ 与 HostButtonEffectUiTests 里
                //    "ToggleButton 先翻转 IsChecked 再引发 Click"同一条纪律：判据要把框架会做的事补齐，
                //    否则驱动出来的不是生产路径。接线缺失时补这一下也不会有人接 ⇒ 判据仍然有效。
                var directory = Find<TextBox>(dialog, "TxtAutoExportDirectory");
                directory.Text = "/tmp/zhishu-ui-2";
                directory.RaiseEvent(new TextChangedEventArgs(TextBox.TextChangedEvent));
                UiTestHost.Pump(2);

                Assert.False(depot.Settings.AutoExport.Enabled);
                Assert.Equal(45, depot.Settings.AutoExport.IntervalMinutes);
                Assert.Equal("/tmp/zhishu-ui-2", depot.Settings.AutoExport.Directory);

                // 闭环：界面改完的设置喂给服务时，"能不能跑"的判断也跟着变
                Assert.False(depot.Settings.AutoExport.Options().CanRun);   // 开关被关掉了
                Find<CheckBox>(dialog, "ChkAutoExport").IsChecked = true;
                UiTestHost.Pump(2);
                Assert.True(depot.Settings.AutoExport.Options().CanRun);
            }
            finally
            {
                dialog.Close();
            }
        });
    }

    /// <summary>XAML 契约：面板与四个具名控件都在。</summary>
    [Fact]
    public void Panel_HasTheExpectedControls()
    {
        var axaml = File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", "SettingsDialog.axaml"));

        foreach (var name in new[]
                 {
                     "PanelAutoExport", "ChkAutoExport", "NumAutoExportInterval",
                     "TxtAutoExportDirectory", "BtnPickAutoExportDirectory",
                 })
        {
            Assert.Contains($"x:Name=\"{name}\"", axaml, StringComparison.Ordinal);
        }
    }

    /// <summary>接线契约（源码级）：四条接线 ＋ 载入 ＋ 分类导航三条都在。</summary>
    [Fact]
    public void WiringContract_IsInPlace()
    {
        var code = SourceScanner.StripComments(File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", "SettingsDialog.axaml.cs")));

        foreach (var fragment in new[]
                 {
                     "PopulateAutoExportControls();",
                     "ChkAutoExport.IsCheckedChanged += ",
                     "NumAutoExportInterval.ValueChanged += ",
                     "TxtAutoExportDirectory.TextChanged += ",
                     "BtnPickAutoExportDirectory.Click += OnPickAutoExportDirectory;",
                     "PanelAutoExport.IsVisible = key == \"autoexport\";",
                     "\"settings.autoexport.nav\"",
                 })
        {
            Assert.Contains(fragment, code, StringComparison.Ordinal);
        }
    }

    /// <summary>设置面要的键两本字典都在（⚠ 用**字面量键**：写成常量名会把死常量写活）。</summary>
    [Fact]
    public void SettingsKeys_ExistInBothDictionaries()
    {
        var zh = PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
        var en = PluginTextLocalizationGuardTests.LoadDictionary("en-US");
        foreach (var key in new[]
                 {
                     "settings.autoexport.nav", "settings.autoexport.title", "settings.autoexport.desc",
                     "settings.autoexport.enabled", "settings.autoexport.interval",
                     "settings.autoexport.directory", "settings.autoexport.directory.hint",
                     "settings.autoexport.browse", "settings.autoexport.hint",
                 })
        {
            Assert.True(zh.ContainsKey(key), $"zh 字典缺键：{key}");
            Assert.True(en.ContainsKey(key), $"en 字典缺键：{key}");
        }

        // 诚实边界必须写在界面上（不是只写在文档里）：这条提示里要有"只在编辑器运行期间"这层意思
        Assert.Contains("运行", zh["settings.autoexport.hint"], StringComparison.Ordinal);
    }
}
