using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views.Editors;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`VariablePicker` 的 `TextBox → 变量树过滤` 驱动面**（阶段 193）——
/// 把「TextBox 接线普查面」（<see cref="AppTextBoxWiringCensusTests"/>）里
/// <c>Editors/VariablePicker.axaml</c> 的那 1 条接线从 `census` 升级成 `driven`。
///
/// <para><b>缺口形状</b>：<c>SearchBox.TextChanged += OnSearchTextChanged</c> 在此之前**零判据** ——
/// 摘掉这条接线、或把 <c>RebuildTree</c> 里那句 <c>filter.Trim().ToLowerInvariant()</c> 的
/// <c>Trim()</c> 删掉，全仓没有一条会红。</para>
///
/// <para><b>读的是哪一面</b>：过滤的可观察效果就是**变量树本身**（<c>VarTree</c> 的两层节点），
/// 所以这里读的是 `RebuildTree` 写进去的那棵树 —— 与 `AuditLogView` 那条面同形。
/// 断言**不读任何本地化文案**：只按夹具自己的变量名（`液位` / `温度`）判，
/// 系统变量分组（标题走字典）只进"总数不塌"的粗判。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class VariablePickerTextBoxWiringTests
{
    /// <summary>一条手势：把 <paramref name="Text"/> 写进 `SearchBox`，树里**必须有** / **必须没有**哪些叶子。</summary>
    private sealed record Spec(string Name, string Text, string[] Must, string[] MustNot);

    private static readonly Spec[] Specs =
    {
        new("SearchBox", "液位", new[] { "液位" }, new[] { "温度" }),
        // 两侧空白由 `RebuildTree` 的 `Trim()` 吃掉 —— 这条手势打的就是那一点
        new("SearchBox", "  温度  ", new[] { "温度" }, new[] { "液位" }),
        new("SearchBox", "zzz-没有这个变量", Array.Empty<string>(), new[] { "液位", "温度" }),
        new("SearchBox", "", new[] { "液位", "温度" }, Array.Empty<string>()),
    };

    /// <summary>
    /// **「没有接线」登记表**：`DisplayField` 是**只读**显示框（`IsReadOnly="True"`），
    /// 由 `UpdateDisplay()` 在选中变量时回填 —— 没有 `TextChanged` 接线，不是缺口。
    /// </summary>
    private static readonly Dictionary<string, string> WithoutWiring = new(StringComparer.Ordinal)
    {
        ["DisplayField"] = "只读显示框（IsReadOnly）：由 `UpdateDisplay()` 写，没有 TextChanged 接线",
    };

    /// <summary>树里所有**两层叶子**的 Header（分组节点 → 变量节点）。</summary>
    private static List<string> Leaves(VariablePicker picker)
    {
        var tree = ControlWiringFace.Find<TreeView>(picker, "VarTree");
        var leaves = new List<string>();
        foreach (var group in tree.Items.OfType<TreeViewItem>())
        {
            // 分组节点自己也是 TreeViewItem：叶子是它的 Items 里那一层
            foreach (var leaf in group.Items.OfType<TreeViewItem>())
                leaves.Add(leaf.Header?.ToString() ?? "");
        }

        return leaves;
    }

    [Fact]
    public void EverySpec_FiltersTheVariableTreeAsDeclared()
    {
        using var sandbox = new DataRootSandbox("variablepicker-textbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            Assert.Equal(2, depot.Variables.Variables.Count);   // 夹具自检：两条具名变量

            var picker = new VariablePicker();
            picker.LoadVariables(depot.Variables);

            var window = new Window { Width = 800, Height = 600, Content = picker };
            window.Show();
            UiTestHost.Pump(3);
            try
            {
                foreach (var spec in Specs)
                {
                    ControlWiringFace.Write(picker, spec.Name, spec.Text);
                    var leaves = Leaves(picker);
                    var blob = string.Join(" | ", leaves);

                    foreach (var must in spec.Must)
                        Assert.True(leaves.Any(l => l.Contains(must, StringComparison.Ordinal)),
                            $"过滤 '{spec.Text}' 后树里应当有「{must}」，实际叶子：{blob}");
                    foreach (var mustNot in spec.MustNot)
                        Assert.True(!leaves.Any(l => l.Contains(mustNot, StringComparison.Ordinal)),
                            $"过滤 '{spec.Text}' 后树里不该有「{mustNot}」，实际叶子：{blob}");
                }

                // 粗判：清空之后总叶子数不低于夹具那两条（系统变量分组的条数随夹具/语言变，不冻死）
                ControlWiringFace.Write(picker, "SearchBox", "");
                Assert.True(Leaves(picker).Count >= 2, "清空过滤后变量树塌了");
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：`SearchBox` 有驱动条目；只读的 `DisplayField` 在登记表里。
    /// </summary>
    [Fact]
    public void EveryTextBoxAndWiring_IsAccountedFor()
    {
        ControlWiringFace.AssertFullyDriven(
            "Editors/VariablePicker.axaml", Specs.Select(s => s.Name).ToArray(), scanFloor: 1, WithoutWiring);
    }
}
