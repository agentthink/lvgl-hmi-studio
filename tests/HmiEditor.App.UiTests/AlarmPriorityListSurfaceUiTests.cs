using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using HmiEditor.App.Views;
using HmiEditor.Core.Models.Alarm;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 报警列表的**优先级面**（ISA-18.2 切片㈡，阶段 292）。
///
/// <para><b>为什么这一片要单独判</b>：登记册原话是「`AlarmPriority` 在 `HmiEditor.App` **0 命中**」——
/// 编辑器算得出优先级（Core 的纯函数、有 11 条判据），但报警列表<b>既不着色也不按它排序</b>，
/// 于是"优先级 ≠ 严重度"这条 ISA-18.2 的核心结论在界面上<b>看不见</b>。</para>
///
/// <para><b>判据三件</b>：① 列表**按优先级排序**（P1 在前，与声明顺序无关）；
/// ② 每项带语言无关的 `P1…P4` 记法；③ 点色按**优先级** —— 一条 Critical 但允许响应时间很宽的
/// 报警必须显示成"更松"的颜色（这正是优先级与严重度分家的地方）。
/// 外加一条**不变式**：**模型顺序一字不动**（生成器的 `config_id` 就是 `group.Alarms` 的遍历序，
/// 重排模型会让既有的确认/搁置 id 全部错位）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class AlarmPriorityListSurfaceUiTests
{
    private static Color DotColor(ListBoxItem item) =>
        item.GetVisualDescendants().OfType<Ellipse>().First().Fill is SolidColorBrush b
            ? b.Color
            : throw new InvalidOperationException("列表项里找不到优先级圆点");

    private static string ItemText(ListBoxItem item) =>
        string.Join("|", item.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? ""));

    [Fact]
    public void List_IsOrderedAndColoredByPriority_ButModelOrderIsUntouched()
    {
        using var sandbox = new DataRootSandbox("alarm-priority-list");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            var group = depot.AlarmGroups[0];
            group.Alarms.Clear();

            // 声明顺序刻意与优先级顺序**不同**：A(P3) / B(P4) / C(P2)
            var a = new AlarmConfig { Name = "A", Severity = AlarmSeverity.Critical, AllowedResponseMs = 600_000 };
            var b = new AlarmConfig { Name = "B", Severity = AlarmSeverity.Info };
            var c = new AlarmConfig { Name = "C", Severity = AlarmSeverity.Critical };
            group.Alarms.Add(a);
            group.Alarms.Add(b);
            group.Alarms.Add(c);

            // 前提自检：三条优先级确实是 P3 / P4 / P2（口径来自 Core 的纯函数，不是这里编的）
            Assert.Equal(3, AlarmPriority.Of(a));
            Assert.Equal(4, AlarmPriority.Of(b));
            Assert.Equal(2, AlarmPriority.Of(c));

            var window = new AlarmConfigView(depot.AlarmGroups, depot.Project);
            window.Show();
            UiTestHost.Pump(5);
            try
            {
                var list = ControlWiringFace.Find<ListBox>(window, "AlarmList");
                Assert.Equal(3, list.Items.Count);
                var items = list.Items.OfType<ListBoxItem>().ToArray();

                // ① 显示顺序 = 优先级顺序（P2 → P3 → P4）
                Assert.Equal(new[] { "C", "A", "B" },
                    items.Select(i => ((AlarmConfig)i.Tag!).Name).ToArray());

                // ② 每项带 P 记法（语言无关，见 AlarmPriority.Label）
                Assert.Contains("P2", ItemText(items[0]));
                Assert.Contains("P3", ItemText(items[1]));
                Assert.Contains("P4", ItemText(items[2]));

                // ③ 点色**按优先级**：A 是 Critical 严重度但 P3 ⇒ 必须是 Warning 色（不是 Critical 色）
                Assert.Equal(UiPalette.SeverityWarning, DotColor(items[1]));
                Assert.NotEqual(UiPalette.SeverityCritical, DotColor(items[1]));
                Assert.Equal(UiPalette.SeverityCritical, DotColor(items[0]));   // P2
                Assert.Equal(UiPalette.SeverityInfo, DotColor(items[2]));       // P4

                // ⚠ 不变式：模型顺序**一字不动**（只排序显示；重排模型 = config_id 全错位）
                Assert.Equal(new[] { "A", "B", "C" }, group.Alarms.Select(x => x.Name).ToArray());
            }
            finally
            {
                window.Close();
            }
        });
    }
}
