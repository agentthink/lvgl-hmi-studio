using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`SettingsDialog` 里 `TextBox → 设置` 的驱动面**（阶段 188，闭环登记 `#88`）。
///
/// <para><b>为什么需要它</b>（`#88` 的原文）：程序化写 <c>TextBox.Text</c> **不触发</c> <c>TextChanged</c>
/// （真实输入才触发 —— 阶段 187 探针实测）⇒ 既有判据一条都没驱动过
/// <c>TxtLvglDir</c> / <c>TxtT113SdkRoot</c> / <c>TxtCanvasBgColor</c> / <c>TxtAccentColor</c>
/// 这一族接线：它们**真实输入下工作**，但测试面为零。</para>
///
/// <para><b>判据形状（照本仓"一份面 ＋ 双向差集"）</b>：</para>
/// <list type="number">
/// <item><b>驱动</b>：逐条把文本写进框里 ＋ **按框架的做法补发 <c>TextChangedEvent</c>**，然后读**设置**
/// （不是读界面）断言它变成了什么；每条还要求先切到它所在的那一面（面板没显示就当场红 ⇒
/// 顺带证明"这个控件在界面上真的够得着"）。</item>
/// <item><b>双向差集（XAML 侧）</b>：<c>SettingsDialog.axaml</c> 里**每一个具名 <c>TextBox</c>**
/// 都必须在表里有分类 —— 新增一个没人驱动的输入框会被点名。</item>
/// <item><b>双向差集（代码侧）</b>：<c>SettingsDialog.axaml.cs</c> 里**每一条
/// <c>X.TextChanged += …</c>** 也必须在表里 —— 新增一条接线同样会被点名。</item>
/// </list>
///
/// <para>⚠ 表里的 <c>Unchanged</c> 那些是**校验**的口径（非法十六进制不进设置、空白被裁掉），
/// 它们与 Moves 那些是同一批控件的不同手势 —— 一个控件可以有多条手势。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class SettingsTextBoxWiringTests
{
    /// <summary>一条手势：把 <paramref name="Text"/> 写进 <paramref name="Name"/>，断言设置读到 <paramref name="Expected"/>。</summary>
    /// <param name="NavKey">该控件所在面的分类键（驱动前要先切过去）。</param>
    /// <param name="Unchanged">true ⇒ 期望**一字不变**（校验拒绝/裁剪后与原值相同）。</param>
    private sealed record Spec(
        string Name,
        string NavKey,
        string Setting,
        Func<WindowDepot, string> Read,
        string Text,
        string Expected,
        bool Unchanged = false);

    private static readonly Spec[] Specs =
    {
        // ── 主题：强调色（三条手势：合法 ⇒ 写；非法 ⇒ 不动；留空 ⇒ 恢复默认）──
        new("TxtAccentColor", "theme", "Theme.CustomAccent", d => d.Theme.CustomAccent ?? "",
            "#123456", "#123456"),
        new("TxtAccentColor", "theme", "Theme.CustomAccent", d => d.Theme.CustomAccent ?? "",
            "#zzz", "", Unchanged: true),
        new("TxtAccentColor", "theme", "Theme.CustomAccent", d => d.Theme.CustomAccent ?? "",
            "   ", ""),

        // ── 显示：画布底色（合法 ⇒ 写；非法 ⇒ 不动）──
        new("TxtCanvasBgColor", "display", "Display.CanvasBackgroundColor",
            d => d.Settings.Display.CanvasBackgroundColor, "#0a0b0c", "#0a0b0c"),
        new("TxtCanvasBgColor", "display", "Display.CanvasBackgroundColor",
            d => d.Settings.Display.CanvasBackgroundColor, "nope", "#0a0b0c", Unchanged: true),

        // ── 模拟器：两个目录（**裁剪**是口径的一部分）──
        new("TxtLvglDir", "simulator", "Simulator.LvglDir",
            d => d.Settings.Simulator.LvglDir, "  /opt/lvgl  ", "/opt/lvgl"),
        new("TxtSimWorkDir", "simulator", "Simulator.WorkDir",
            d => d.Settings.Simulator.WorkDir, "  /tmp/sim  ", "/tmp/sim"),

        // ── 自动导出：目标目录（**刻意不裁剪**：空白是用户输入的一部分，由设置自己判断"配没配"）──
        new("TxtAutoExportDirectory", "autoexport", "AutoExport.Directory",
            d => d.Settings.AutoExport.Directory, "  /tmp/auto  ", "  /tmp/auto  "),

        // ── 设备：SDK 与交叉编译器（同样裁剪）──
        new("TxtT113SdkRoot", "device", "Device.T113SdkRoot",
            d => d.Settings.Device.T113SdkRoot, "  /opt/tina  ", "/opt/tina"),
        new("TxtT113Toolchain", "device", "Device.T113ToolchainGcc",
            d => d.Settings.Device.T113ToolchainGcc, "  /opt/gcc  ", "/opt/gcc"),
    };

    private static T Find<T>(Window window, string name) where T : Control =>
        ControlWiringFace.Find<T>(window, name);

    /// <summary>切到某个分类面，并自检那张面板真的显示出来了。</summary>
    private static void OpenPanel(SettingsDialog dialog, string navKey)
    {
        var list = Find<ListBox>(dialog, "CategoryList");
        var item = list.Items.OfType<ListBoxItem>()
            .Single(i => string.Equals(i.Tag as string, navKey, StringComparison.Ordinal));
        list.SelectedItem = item;
        UiTestHost.Pump(2);
    }

    /// <summary>把文本写进框里并按框架的做法补发事件（**这一步是这一族判据的关键**）。</summary>
    private static void Type(SettingsDialog dialog, string name, string text) =>
        ControlWiringFace.Write(dialog, name, text);

    [Fact]
    public void EverySpec_WritesItsSettingAsDeclared()
    {
        using var sandbox = new DataRootSandbox("settings-textbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);

            // 前提自检：两个"非法/留空"手势的起点必须**不是**期望值 —— 否则 Unchanged 那条是空转
            depot.Theme.SetCustomAccent("#abcdef");
            depot.Settings.Display.SetCanvasBackgroundColor("#0a0b0c");

            var dialog = new SettingsDialog(depot.Settings, depot.Theme);
            dialog.Show();
            UiTestHost.Pump(2);
            try
            {
                foreach (var spec in Specs)
                {
                    OpenPanel(dialog, spec.NavKey);

                    var before = spec.Read(depot);
                    Type(dialog, spec.Name, spec.Text);
                    var after = spec.Read(depot);

                    if (spec.Unchanged)
                    {
                        Assert.True(string.Equals(before, after, StringComparison.Ordinal),
                            $"{spec.Name}（{spec.Setting}）本应**不动**：'{spec.Text}' 之后从 '{before}' 变成了 '{after}'");
                        continue;
                    }

                    Assert.True(string.Equals(spec.Expected, after, StringComparison.Ordinal),
                        $"{spec.Name}（{spec.Setting}）驱动 '{spec.Text}' 后读到 '{after}'，期望 '{spec.Expected}'");
                }
            }
            finally
            {
                dialog.Close();
            }
        });
    }

    // ────────────────────────── 双向差集 ──────────────────────────

    /// <summary>
    /// **XAML 侧**：<c>SettingsDialog.axaml</c> 里每一个具名 <c>TextBox</c> 都要在表里 ——
    /// 新增一个没人驱动的输入框会被点名（这正是 `#88` 缺的那一半）。
    /// </summary>
    [Fact]
    public void EveryNamedTextBoxInXaml_IsDrivenByTheTable()
    {
        var axaml = File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", "SettingsDialog.axaml"));

        var declared = NamedTextBoxes(axaml);
        var driven = Specs.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);

        Assert.True(declared.Count >= 7, $"只扫到 {declared.Count} 个具名 TextBox —— 扫描面可能塌了（本阶段实测 7 个）");

        var missing = declared.Where(n => !driven.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var stale = driven.Where(n => !declared.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0, $"这些具名 TextBox 没有被任何驱动条目覆盖：{string.Join(", ", missing)}");
        Assert.True(stale.Length == 0, $"表里写了这些 TextBox，但 XAML 里没有：{string.Join(", ", stale)}");
    }

    /// <summary>
    /// **代码侧**：每一条 <c>X.TextChanged += …</c> 都要在表里 ——
    /// 新增一条接线（哪怕加在一个既有的框上）也会被点名，而"接了线却没人驱动"正是 `#88` 的形状。
    /// </summary>
    [Fact]
    public void EveryTextChangedWiring_IsDrivenByTheTable()
    {
        var code = SourceScanner.StripComments(File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", "SettingsDialog.axaml.cs")));

        var wired = Regex.Matches(code, @"(\w+)\.TextChanged\s*\+=")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var driven = Specs.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);

        Assert.True(wired.Count >= 7, $"只扫到 {wired.Count} 条 TextChanged 接线 —— 扫描面可能塌了（本阶段实测 7 条）");

        var missing = wired.Where(n => !driven.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0,
            $"这些 TextChanged 接线没有驱动条目（接了线却没人驱动 ⇒ 与 #88 同一个形状）：{string.Join(", ", missing)}");

        // 反向：表里声明"这个框接到了 TextChanged"，代码里就必须真有那条接线 ——
        // 少了它，驱动会静默变成"改了文本但设置没动"，而上面的驱动判据会以**别的**理由红（更难读）
        var stale = driven.Where(n => !wired.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.True(stale.Length == 0,
            $"表里声明了这些 TextChanged 接线，代码里却没有：{string.Join(", ", stale)}");
    }

    /// <summary>从 XAML 里取出所有 <c>&lt;TextBox x:Name="…"&gt;</c>（共享实现见 <see cref="ControlWiringFace"/>）。</summary>
    private static HashSet<string> NamedTextBoxes(string axaml) => ControlWiringFace.NamedTextBoxes(axaml);
}
