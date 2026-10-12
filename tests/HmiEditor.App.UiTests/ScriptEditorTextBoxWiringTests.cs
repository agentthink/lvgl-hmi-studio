using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`ScriptEditorView` 的 `TextBox → 脚本模型` 驱动面**（阶段 192，闭环登记 `#89`）——
/// 把「TextBox 接线普查面」（<see cref="AppTextBoxWiringCensusTests"/>）里
/// <c>ScriptEditorView.axaml</c> 的 3 条接线从 `blocked` 升级成 `driven`。
///
/// <para><b>它替代的是什么</b>：阶段 191 本文件是**前提判据**（断言"打了字模型一字不动"），
/// 因为三个字段处理器首行判 <c>Parent == null</c>，而该视图是 <c>ChromeWindow : Window</c> ⇒
/// 生产形状（<c>ShowDialog(owner)</c>）下 <c>Owner</c> 有、<c>Parent</c> 恒 null ⇒
/// **用户打的字一个都不落模型**（`#89`）。阶段 192 修掉守卫之后，同一批手势**真的写模型**了，
/// 所以这里改成驱动面 —— 期望值反过来（**必须变**而不是"必须不变"）。</para>
///
/// <para><b>宿主仍按生产形状建</b>（<c>ShowDialog(owner)</c>），并**继续断言 <c>Parent == null</c>**：
/// 那是 `#89` 的根因现场 —— 留着它证明"修的是处理器不再依赖它"，而不是"<c>Parent</c> 碰巧变非 null 了"。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class ScriptEditorTextBoxWiringTests
{
    /// <summary>一条手势：把 <paramref name="Text"/> 写进 <paramref name="Name"/>，断言脚本模型读到 <paramref name="Expected"/>。</summary>
    private sealed record Spec(
        string Name,
        string Setting,
        Func<WindowDepot, string> Read,
        string Text,
        string Expected);

    private static readonly Spec[] Specs =
    {
        new("TxtScriptName", "Name", d => d.Scripts[0].Name, "开机脚本改名", "开机脚本改名"),

        // 描述**不裁剪**（`_currentScript.Description = TxtDescription.Text;`）—— 照实现的口径写期望值
        new("TxtDescription", "Description", d => d.Scripts[0].Description ?? "",
            "  两侧空白原样保留  ", "  两侧空白原样保留  "),

        // 代码区（`OnCodeTextChanged`）：写 `Code` ＋ `ModifiedAt`，并**点起 500ms 去抖校验**
        new("TxtCodeEditor", "Code", d => d.Scripts[0].Code ?? "", "print('hi')", "print('hi')"),
    };

    /// <summary>
    /// **「没有接线」登记表**：`TxtRunOutput` 是**只写**的输出区（清空按钮 / 调试运行结果写它），
    /// 没有 `TextChanged` 接线 —— 不是缺口，但要显式登记（口径见
    /// <see cref="ControlWiringFace.AssertFullyDriven"/>）。
    /// </summary>
    private static readonly Dictionary<string, string> WithoutWiring = new(StringComparer.Ordinal)
    {
        ["TxtRunOutput"] = "只被输出侧写（清空按钮 / 调试运行结果），没人读它的 TextChanged ⇒ 本来就没接线",
    };

    [Fact]
    public void EverySpec_WritesTheScriptModelAsDeclared()
    {
        using var sandbox = new DataRootSandbox("scripteditor-textbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            var script = depot.Scripts[0];

            var owner = new Window { Width = 400, Height = 300 };
            owner.Show();
            UiTestHost.Pump(2);

            // 生产形状：`ShowDialog(this)`（`MainWindow.OpenScriptEditorDialog` / `Wiring.Panels.cs`）
            var view = new ScriptEditorView(depot.Scripts, depot.Bindings, depot.Project);
            var listChanged = 0;
            view.ScriptListChanged += (_, _) => listChanged++;
            _ = view.ShowDialog(owner);
            UiTestHost.Pump(3);
            try
            {
                // ── 前提（**量出来**的）──
                Assert.NotNull(view.Owner);   // 生产形状：owner 装上了
                Assert.Null(view.Parent);     // `#89` 的根因现场：Parent 仍是 null（修的是处理器不再依赖它）

                ControlWiringFace.Find<ListBox>(view, "ScriptList").SelectedIndex = 0;
                UiTestHost.Pump(2);
                Assert.Equal("开机脚本", ControlWiringFace.Find<TextBox>(view, "TxtScriptName").Text);

                // ── 驱动三条：阶段 191 它们是"一字不动"，现在**必须**写进模型 ──
                foreach (var spec in Specs)
                {
                    ControlWiringFace.Write(view, spec.Name, spec.Text);
                    var after = spec.Read(depot);
                    Assert.True(string.Equals(spec.Expected, after, StringComparison.Ordinal),
                        $"{spec.Name}（{spec.Setting}）驱动 '{spec.Text}' 后读到 '{after}'，期望 '{spec.Expected}'"
                        + "（若这里又变回『一字不动』，说明 `#89` 的 `Parent == null` 守卫被注回去了）");
                }

                Assert.Equal("开机脚本改名", script.Name);
                Assert.Equal("print('hi')", script.Code ?? "");

                // 反面见证：`ScriptListChanged` 真的抬了（脏标记 / 自动保存那条线靠它）——
                //   阶段 191 之前它一次都不抬（处理器首行就返回）
                Assert.True(listChanged > 0,
                    "`ScriptListChanged` 一次都没抬 ⇒ 工程不会标脏、也不会触发自动保存（`#89` 的症状之一）");

                // 集合结构没被手势改动
                Assert.Same(script, depot.Scripts[0]);
                Assert.Single(depot.Scripts);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
                owner.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：3 条接线都有驱动条目；只写的 `TxtRunOutput` 在登记表里。
    /// </summary>
    [Fact]
    public void EveryTextBoxAndWiring_IsAccountedFor()
    {
        ControlWiringFace.AssertFullyDriven(
            "ScriptEditorView.axaml", Specs.Select(s => s.Name).ToArray(), scanFloor: 3, WithoutWiring);
    }
}
