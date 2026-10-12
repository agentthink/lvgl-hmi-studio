using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`SettingsDialog` 的「数字框 → 设置」驱动面**（阶段 195）——
/// 把「数字框普查面」（<see cref="AppNumberBoxWiringCensusTests"/>）里
/// <c>SettingsDialog.axaml</c> 的那 9 条 `ValueChanged` 接线从 `census` 升级成 `perBox`。
///
/// <para><b>与 TextBox 那一族的纪律相反</b>：数字框**程序化写 <c>Value</c> 就会触发**
/// <c>ValueChanged</c>（阶段 188 在 <c>NumAutoExportInterval</c> 上实测）⇒ 驱动就是写值 +
/// `Pump`，不需要补发事件。⚠ 写**同值**时框架不发事件，所以每条手势都换一个不同的值。</para>
///
/// <para><b>读的是设置（不是界面）</b>，每条手势前先切到该控件所在的分类面 ——
/// 面板没显示就当场红（顺带证明"界面上真的够得着"）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class SettingsDialogNumberBoxWiringTests
{
    /// <summary>一条手势：把 <paramref name="Value"/> 写进 <paramref name="Name"/>，断言设置读到 <paramref name="Expected"/>。</summary>
    /// <param name="NavKey">该控件所在面的分类键（驱动前要先切过去）。</param>
    private sealed record Spec(
        string Name,
        string NavKey,
        string Setting,
        Func<WindowDepot, string> Read,
        double Value,
        string Expected);

    private static readonly Spec[] Specs =
    {
        // ── 布局：面板占比（**除以 100** 才是设置里的比例）──
        new("NumLeftPanel", "layout", "Layout.LeftPanelProportion",
            d => Num(d.Settings.Layout.LeftPanelProportion), 25, "0.25"),
        new("NumRightPanel", "layout", "Layout.RightPanelProportion",
            d => Num(d.Settings.Layout.RightPanelProportion), 30, "0.3"),
        new("NumBottomPanel", "layout", "Layout.BottomPanelProportion",
            d => Num(d.Settings.Layout.BottomPanelProportion), 40, "0.4"),

        // ── 交互 ──
        new("NumGridSize", "interaction", "Interaction.GridSize",
            d => d.Settings.Interaction.GridSize.ToString(CultureInfo.InvariantCulture), 24, "24"),
        new("NumSnapStep", "interaction", "Interaction.SnapStep",
            d => d.Settings.Interaction.SnapStep.ToString(CultureInfo.InvariantCulture), 3, "3"),
        new("NumUndoStack", "interaction", "Interaction.UndoStackSize",
            d => d.Settings.Interaction.UndoStackSize.ToString(CultureInfo.InvariantCulture), 50, "50"),
        new("NumAutoSaveInterval", "interaction", "Interaction.AutoSaveInterval",
            d => d.Settings.Interaction.AutoSaveInterval.ToString(CultureInfo.InvariantCulture), 120, "120"),

        // ── 显示 ──
        new("NumFontStep", "display", "Display.FontStep",
            d => d.Settings.Display.FontStep.ToString(CultureInfo.InvariantCulture), 4, "4"),

        // ── 自动导出（阶段 187 的那一面）──
        new("NumAutoExportInterval", "autoexport", "AutoExport.IntervalMinutes",
            d => d.Settings.AutoExport.IntervalMinutes.ToString(CultureInfo.InvariantCulture), 45, "45"),
    };

    private static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>切到某个分类面，并自检那张面板真的显示出来了。</summary>
    private static void OpenPanel(SettingsDialog dialog, string navKey)
    {
        var list = ControlWiringFace.Find<ListBox>(dialog, "CategoryList");
        var item = list.Items.OfType<ListBoxItem>()
            .Single(i => string.Equals(i.Tag as string, navKey, StringComparison.Ordinal));
        list.SelectedItem = item;
        UiTestHost.Pump(2);
    }

    [Fact]
    public void EverySpec_WritesItsSettingAsDeclared()
    {
        using var sandbox = new DataRootSandbox("settings-numberbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);

            var dialog = new SettingsDialog(depot.Settings, depot.Theme);
            dialog.Show();
            UiTestHost.Pump(2);
            try
            {
                foreach (var spec in Specs)
                {
                    OpenPanel(dialog, spec.NavKey);
                    ControlWiringFace.WriteNumber(dialog, spec.Name, spec.Value);

                    var after = spec.Read(depot);
                    Assert.True(string.Equals(spec.Expected, after, StringComparison.Ordinal),
                        $"{spec.Name}（{spec.Setting}）驱动 {spec.Value} 后读到 '{after}'，期望 '{spec.Expected}'");
                }
            }
            finally
            {
                dialog.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：9 个具名数字框全部有驱动条目（这个文件没有"没接线"的框）。
    /// </summary>
    [Fact]
    public void EveryNumberBoxAndWiring_IsDrivenByTheTable()
    {
        ControlWiringFace.AssertFullyDrivenNumberBoxes(
            "SettingsDialog.axaml", Specs.Select(s => s.Name).ToArray(), scanFloor: 9);
    }
}
