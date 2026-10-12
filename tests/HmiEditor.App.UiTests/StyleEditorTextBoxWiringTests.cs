using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`StyleEditorView` 的面 B 驱动面**（阶段 194）—— 该文件里那条接线是
/// **辅助函数注入**的：`WireTextChanged(tb) => tb.TextChanged += (_, _) => SyncThemeFromUi();`
/// （泛型/局部参数，任何 `x:Name` 都对不上 ⇒ 普查面把它记在面 B）。
///
/// <para><b>一条线 × 十个框</b>：`AddColorField` 对**十个**颜色框各调一次 `WireTextChanged`
/// ⇒ 扫描扫到的是**一行**接线，而它实际管着十个框。判据因此既验"这十个框真的是同一行接的"
/// （数量 10 ＋ 每个都写进模型），也验"这一行确实在跑"（选中其中两个框驱动）。</para>
///
/// <para><b>入口</b>：这些框没有 `x:Name`，但**构造顺序即契约** —— `BuildUi()` 里
/// `AddColorField` 的调用顺序就是 `Primary / Secondary / Surface / Background / Text /
/// TextSecondary / Border / Success / Warning / Error`。判据按"可视树里
/// `PlaceholderText == "#RRGGBB"` 的框、按树序"取它们（顺序断言用计数钉住），并**按那个顺序**
/// 断言模型字段。</para>
///
/// <para><b>读的是哪一面</b>：`_theme = project.Theme`（**同一个对象**，不是克隆）⇒
/// `SyncThemeFromUi` 直接写工程的主题；判据读 `project.Theme`，不需要点「应用」。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class StyleEditorTextBoxWiringTests
{
    /// <summary>
    /// 本面驱动的**接线标识**（`StyleEditorView.axaml.cs` 里那个参数名）。
    /// 普查面的面 B 表按它核对"这条接线有人认账" —— 改名后不更新就红。
    /// </summary>
    private static readonly string[] WiredIdentifiers = { "tb" };

    /// <summary>颜色框的构造顺序（`BuildUi()` 里 `AddColorField` 的调用顺序）。</summary>
    private static readonly string[] ColorKeys =
    {
        "PrimaryColor", "SecondaryColor", "SurfaceColor", "BackgroundColor", "TextColor",
        "TextSecondaryColor", "BorderColor", "SuccessColor", "WarningColor", "ErrorColor",
    };

    /// <summary>按树序取那十个颜色框（`PlaceholderText` 就是 `AddColorField` 里写死的标记）。</summary>
    private static List<TextBox> ColorBoxes(StyleEditorView view) =>
        view.GetLogicalDescendants().OfType<TextBox>()
            .Where(t => string.Equals(t.PlaceholderText, "#RRGGBB", StringComparison.Ordinal))
            .ToList();

    private static string ColorOf(HmiProject project, int index) => index switch
    {
        0 => project.Theme.PrimaryColor,
        1 => project.Theme.SecondaryColor,
        2 => project.Theme.SurfaceColor,
        3 => project.Theme.BackgroundColor,
        4 => project.Theme.TextColor,
        5 => project.Theme.TextSecondaryColor,
        6 => project.Theme.BorderColor,
        7 => project.Theme.SuccessColor,
        8 => project.Theme.WarningColor,
        9 => project.Theme.ErrorColor,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    [Fact]
    public void EveryColorBox_WritesItsOwnModelField()
    {
        using var sandbox = new DataRootSandbox("styleeditor-textbox");

        UiTestHost.Run(() =>
        {
            var project = new HmiProject { Name = "夹具工程" };
            project.Screens.Add(ScreenFactory.Create(project, name: "主画面"));

            var view = new StyleEditorView(project);
            view.Show();
            UiTestHost.Pump(3);
            try
            {
                var theme = project.Theme;   // `_theme == project.Theme`（同一份对象，不是克隆）

                // 前提自检：**一条接线 × 十个框** —— 数量与构造顺序都要对得上
                var boxes = ColorBoxes(view);
                Assert.True(boxes.Count == 10,
                    $"颜色框应当有 10 个（`AddColorField` 调了 10 次），实际 {boxes.Count} 个");

                // 十个框**逐个**驱动成互不相同的颜色 ⇒ 每个模型的颜色字段都必须是"自己那个框"的值
                for (var i = 0; i < boxes.Count; i++)
                {
                    var expected = $"#0000{i:D2}";
                    ControlWiringFace.Write(view, boxes[i], expected, pump: 1);
                    Assert.True(string.Equals(expected, ColorOf(project, i), StringComparison.Ordinal),
                        $"第 {i} 个颜色框（{ColorKeys[i]}）驱动 '{expected}' 后模型读到 '{ColorOf(project, i)}'"
                        + "（顺序即契约：树序要与 `AddColorField` 的调用顺序一致）");
                }

                // 口径：**不裁剪**（`SyncThemeFromUi` 把框里的原文整串抄进模型）
                ControlWiringFace.Write(view, boxes[0], "  #00ff00  ", pump: 1);
                Assert.Equal("  #00ff00  ", project.Theme.PrimaryColor);

                // 反面见证：改的是**这一份**主题对象（`_theme == project.Theme`，不是克隆）
                Assert.Same(theme, project.Theme);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **面 B 逐条认账**：那条辅助函数注入的接线（`tb`）在本面的表里，且与扫描一致。
    /// </summary>
    [Fact]
    public void EveryAnonymousWiring_IsAccountedFor()
    {
        ControlWiringFace.AssertAnonymousFace("StyleEditorView.axaml", WiredIdentifiers, scanFloor: 1);
    }
}
