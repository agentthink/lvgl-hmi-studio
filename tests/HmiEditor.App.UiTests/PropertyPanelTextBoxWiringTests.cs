using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`PropertyPanelView` 的 `ExpressionInput`（表达式绑定）驱动面**（阶段 193）——
/// 把「TextBox 接线普查面」（<see cref="AppTextBoxWiringCensusTests"/>）里
/// <c>PropertyPanelView.axaml</c> 的那 1 条接线从 `census` 升级成 `driven`。
///
/// <para><b>缺口形状</b>：<c>ExpressionInput.TextChanged += OnExpressionInputChanged</c>
/// 在此之前**零判据** —— 摘掉这条接线，或把"编译不过 ⇒ 不写绑定、只报错"那条口径改掉，
/// 都不会红。（`PropertyPanelWiringTests` 枚举的是**编辑器**族；表达式框不在它那一面里。）</para>
///
/// <para><b>够得着才叫驱动面</b>：这个框出厂是 <c>IsVisible="False"</c> —— 只有把
/// `BindingKindCombo` 拨到「表达式」（走生产处理器 `OnBindingKindChanged`）它才显示出来，
/// 所以每条手势前先做这一步并断言它真的可见。</para>
///
/// <para><b>读的是哪一面</b>：写的是选中控件上的 <see cref="VariableBindingData"/>
/// （`Kind = Expression` ＋ `Expression = 文本`）—— 读模型，不读界面。
/// 两条口径写进期望值：合法表达式**不裁剪**（原样存）；编译失败 / 空白 ⇒ **一个字段都不写**。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class PropertyPanelTextBoxWiringTests
{
    /// <summary>一条手势：把 <paramref name="Text"/> 写进 `ExpressionInput`，断言绑定上的表达式读到 <paramref name="Expected"/>。</summary>
    /// <param name="Unchanged">true ⇒ 期望**一字不变**（编译失败 / 空白）。</param>
    private sealed record Spec(string Text, string Expected, bool Unchanged = false, string? Why = null);

    private static readonly Spec[] Specs =
    {
        new("A + 1", "A + 1"),
        // 合法表达式**不裁剪**（`binding.Expression = text`，text 就是框里的原文）
        new("  B * 2  ", "  B * 2  "),
        // 编译不过 ⇒ 不写绑定（只把错误提示显示出来）
        new("A +", "", Unchanged: true, Why: "语法错 ⇒ 不落绑定"),
        // 空白 ⇒ 同样不写
        new("   ", "", Unchanged: true, Why: "空白 ⇒ 不落绑定"),
        // 再给一条合法的：证明前面两条"没写"不是"这条接线死了"
        new("C - 3", "C - 3"),
    };

    /// <summary>
    /// **「不在本面」登记表**（阶段 199 更正过口径）：属性面板里另外 20 个具名框
    /// **不是"没有接线"** —— 它们走的是**第三个接线形态：XAML 属性式**
    /// （<c>TextChanged="OnPropertyChanged"</c> / <c>OnScreenPropertyChanged</c> / <c>OnVisualStateChanged</c>，
    /// 见 <c>AppXamlAttributeWiringFaceTests</c> 的登记表）。本面（<c>X.TextChanged += …</c> 那个形态）
    /// 扫不到它们，这是**口径**而不是缺口；而它们自己的**点名驱动判据**目前还没有
    /// （`PropertyPanelWiringTests` 枚举的是编辑器族 `FANumberBox/ComboBox/CheckBox`，不含 TextBox；
    /// `PropertyPanelUiTests` / `ControlDefaultNameTests` 只**读**这些框的显示值）——
    /// 那一笔欠账记在 <c>AppXamlAttributeWiringFaceTests</c> 里（`Driver = null` ＋ 理由）。
    /// </summary>
    private static readonly Dictionary<string, string> WithoutWiring = new(StringComparer.Ordinal)
    {
        ["PropName"] = "控件名：XAML 属性式接线（`TextChanged=\"OnPropertyChanged\"`）⇒ 不在本面（本面只扫 code-behind `+=`）；写回走 `SaveProperties`，判据欠账见 AppXamlAttributeWiringFaceTests",
        ["PropText"] = "文本：同上（**XAML 属性式接线**，不在本面）",
        ["PropTextColor"] = "文本色：同上（**XAML 属性式接线**，不在本面）",
        ["PropBgColor"] = "背景色：同上（**XAML 属性式接线**，不在本面）",
        ["PropBorderColor"] = "边框色：同上（**XAML 属性式接线**，不在本面）",
        ["ScreenName"] = "屏幕名：同上（**XAML 属性式接线**，不在本面）",
        ["ScreenBgColor"] = "屏幕底色：同上（**XAML 属性式接线**，不在本面）",
        ["ColWidthsBox"] = "列宽：同上（**XAML 属性式接线**，不在本面）",
        ["HeaderBox"] = "表头：同上（**XAML 属性式接线**，不在本面）",
        ["OptionsBox"] = "选项：同上（**XAML 属性式接线**，不在本面）",
        ["PlaceholderBox"] = "占位符：同上（**XAML 属性式接线**，不在本面）",
        ["TxtActionValue"] = "动作值：同上（**XAML 属性式接线**，不在本面）",
        ["ValuePrefixBox"] = "数值前缀：同上（**XAML 属性式接线**，不在本面）",
        ["ValueSuffixBox"] = "数值后缀：同上（**XAML 属性式接线**，不在本面）",
        ["IndicatorColorBox"] = "指示色：同上（**XAML 属性式接线**，不在本面）",
        ["TabTitlesBox"] = "页签标题：同上（**XAML 属性式接线**，不在本面）",
        ["VsText"] = "视觉状态文本：同上（**XAML 属性式接线**，不在本面）",
        ["VsTextColor"] = "视觉状态文本色：同上（**XAML 属性式接线**，不在本面）",
        ["VsBgColor"] = "视觉状态底色：同上（**XAML 属性式接线**，不在本面）",
        ["VsBorderColor"] = "视觉状态边框色：同上（**XAML 属性式接线**，不在本面）",
    };

    /// <summary>把绑定面板切到「表达式」源（**走生产处理器**），并自检那个框真显示出来了。</summary>
    private static void SwitchToExpression(PropertyPanelView panel)
    {
        var combo = ControlWiringFace.Find<ComboBox>(panel, "BindingKindCombo");
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>()
            .Single(i => string.Equals(i.Tag as string, "Expression", StringComparison.Ordinal));
        UiTestHost.Pump(2);
        Assert.True(ControlWiringFace.Find<TextBox>(panel, "ExpressionInput").IsVisible,
            "表达式框没显示出来 ⇒ 手感够不着（`OnBindingKindChanged` 没把可见性切过去）");
    }

    private static string Expression(HmiControl control) =>
        control.VariableBindings.FirstOrDefault()?.Expression ?? "(无)";

    [Fact]
    public void EverySpec_WritesTheExpressionBindingAsDeclared()
    {
        using var sandbox = new DataRootSandbox("propertypanel-textbox");

        UiTestHost.Run(() =>
        {
            var control = new HmiControl { Name = "状态灯", Type = HmiControlType.Label };

            var panel = new PropertyPanelView();
            var window = new Window { Width = 1000, Height = 900, Content = panel };
            window.Show();
            UiTestHost.Pump(3);
            try
            {
                panel.BindControl(control);
                UiTestHost.Pump(2);
                SwitchToExpression(panel);

                // 前提自检：切到「表达式」这一跳**本身**就会建出那条读绑定（`GetOrCreateReadBinding`），
                // 但 `Expression` 还是空的 ⇒ 后面"合法就写进去 / 非法原地不动"才判得出东西
                var seeded = Assert.Single(control.VariableBindings);
                Assert.Null(seeded.Expression);
                Assert.Equal(HmiEditor.Core.Models.BindingSourceKind.Expression, seeded.Kind);

                foreach (var spec in Specs)
                {
                    var before = Expression(control);
                    ControlWiringFace.Write(panel, "ExpressionInput", spec.Text);
                    var after = Expression(control);

                    if (spec.Unchanged)
                    {
                        Assert.True(string.Equals(before, after, StringComparison.Ordinal),
                            $"表达式 '{spec.Text}'（{spec.Why}）本应**不动**，却从 '{before}' 变成了 '{after}'");
                        continue;
                    }

                    Assert.True(string.Equals(spec.Expected, after, StringComparison.Ordinal),
                        $"表达式 '{spec.Text}' 驱动后绑定里读到 '{after}'，期望 '{spec.Expected}'");

                    // 另一条口径：写表达式的**同时**把绑定源标成 Expression
                    Assert.Equal(HmiEditor.Core.Models.BindingSourceKind.Expression,
                        control.VariableBindings.First().Kind);
                }

                // 反面见证：合法表达式之后错误提示要收回去（不然"报错"就成了常驻）
                Assert.False(ControlWiringFace.Find<TextBlock>(panel, "ExpressionErrorText").IsVisible,
                    "最后一条是合法表达式，错误提示不该还挂着");
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：`ExpressionInput` 有驱动条目；其余 20 个走"批量写回"的框在登记表里。
    /// </summary>
    [Fact]
    public void EveryTextBoxAndWiring_IsAccountedFor()
    {
        ControlWiringFace.AssertFullyDriven(
            "PropertyPanelView.axaml", new[] { "ExpressionInput" }, scanFloor: 1, WithoutWiring);
    }
}
