using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`FaceplateTemplateEditorView` 的面 B 驱动面**（阶段 194）——
/// 该文件的 6 条接线**全部是按名找不到的**（代码里在 `BuildParamCard(param, index)` 里现造的
/// 参数卡片输入框：`txtParamName` / `txtDisplayName` / `txtDefault` / `txtMin` / `txtMax` / `txtDesc`），
/// 普查面把它们记在**面 B**；这一阶段用**另一个入口**驱动它们。
///
/// <para><b>入口是什么</b>：这些框没有 `x:Name`，但它们在可视树里、而且**位置是稳定的契约** ——
/// 卡片是 <c>Border</c>（`Tag = index`），里面一个 `Grid`，每个框用
/// <c>[Grid.RowProperty]</c> / <c>[Grid.ColumnProperty]</c> 定位。所以按
/// **（行, 列）**取框，取到 `Assert.Single` 就说明那个契约还成立（结构一变即红，比"按子元素下标"
/// 那种取法稳）。</para>
///
/// <para><b>读的是哪一面</b>：卡片闭包写的是 <c>_params[i]</c>（构造时对
/// <c>template.Parameters</c> 的**克隆**），点「保存」时 <c>OnSaveClicked</c> 把它整体写回
/// `template.Parameters` 并置 `Result` ⇒ 判据驱动完点一次保存，再读**模板模型**
/// （`template.Parameters[0]`）——读的是产物，不是界面。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class FaceplateTemplateEditorTextBoxWiringTests
{
    /// <summary>
    /// 本面驱动的**接线标识**（`FaceplateTemplateEditorView.axaml.cs` 里的局部变量名）。
    /// 普查面的面 B 表按这些名字核对"这条接线有人认账" —— 重命名后不更新就红。
    /// </summary>
    private static readonly string[] WiredIdentifiers =
    {
        "txtParamName", "txtDisplayName", "txtDefault", "txtMin", "txtMax", "txtDesc",
    };

    /// <summary>一条手势：把 <paramref name="Text"/> 写进卡片里 (行,列) 那个框，断言模板参数读到 <paramref name="Expected"/>。</summary>
    private sealed record Spec(int Row, int Column, Func<TemplateParameter, string> Read, string Text, string Expected);

    private static readonly Spec[] Specs =
    {
        new(0, 1, p => p.Name, "扭矩上限", "扭矩上限"),
        new(0, 3, p => p.DisplayName, "扭矩上限显示", "扭矩上限显示"),
        new(2, 1, p => p.DefaultValue ?? "(null)", "25.5", "25.5"),
        new(3, 1, p => Num(p.MinValue), "3.5", "3.5"),
        new(3, 3, p => Num(p.MaxValue), "88.5", "88.5"),
        new(4, 1, p => p.Description, "最大扭矩", "最大扭矩"),
    };

    private static string Num(double? value) =>
        value is null ? "(null)" : value.Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>按 **(行, 列)** 取参数卡片里的那个 `TextBox`（唯一命中，否则当场红）。</summary>
    private static TextBox BoxAt(FaceplateTemplateEditorView view, int paramIndex, int row, int column)
    {
        var panel = ControlWiringFace.Find<StackPanel>(view, "ParamListPanel");
        var card = panel.Children.OfType<Border>().ElementAt(paramIndex);
        var grid = Assert.IsType<Grid>(card.Child);

        var matches = grid.Children.OfType<TextBox>()
            .Where(t => Grid.GetRow(t) == row && Grid.GetColumn(t) == column)
            .ToArray();

        return Assert.Single(matches);
    }

    private static ControlTemplate Template()
    {
        var template = new ControlTemplate { Name = "夹具模板", Category = "通用" };
        template.Parameters.Add(new TemplateParameter
        {
            Name = "扭矩",
            DisplayName = "扭矩",
            Type = ParameterType.Number,   // ⚠ txtMin / txtMax 只在 Number 类型下才建出来
            DefaultValue = "10",
            MinValue = 0,
            MaxValue = 100,
            Description = "夹具参数",
        });
        return template;
    }

    private static void Save(FaceplateTemplateEditorView view)
    {
        ControlWiringFace.Find<Button>(view, "BtnSave")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(2);
    }

    [Fact]
    public void EverySpec_WritesTheTemplateParameterAsDeclared()
    {
        using var sandbox = new DataRootSandbox("faceplate-textbox");

        UiTestHost.Run(() =>
        {
            var template = Template();

            var view = new FaceplateTemplateEditorView(template);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检：卡片建出来了，且回填的是**模型里的**值
                Assert.Single(template.Parameters);
                Assert.Equal("扭矩", BoxAt(view, 0, 0, 1).Text);

                foreach (var spec in Specs)
                    ControlWiringFace.Write(view, BoxAt(view, 0, spec.Row, spec.Column), spec.Text, pump: 1);

                // 此刻模型（模板）还没动 —— 卡片写的是构造时那份**克隆**
                Assert.Equal("扭矩", template.Parameters[0].Name);

                Save(view);

                var saved = Assert.Single(template.Parameters);
                foreach (var spec in Specs)
                {
                    var after = spec.Read(saved);
                    Assert.True(string.Equals(spec.Expected, after, StringComparison.Ordinal),
                        $"卡片 ({spec.Row},{spec.Column}) 驱动 '{spec.Text}' 后模板参数读到 '{after}'，期望 '{spec.Expected}'");
                }

                Assert.Same(template, view.Result);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// 两条**清空口径**：`txtDefault` 置空 ⇒ `DefaultValue = null`；
    /// `txtMin` 写了不是数字 ⇒ `MinValue = null`（不是"保留旧值"）。
    /// </summary>
    [Fact]
    public void EmptyOrUnparsableValues_ClearTheOptionalFields()
    {
        using var sandbox = new DataRootSandbox("faceplate-textbox-clear");

        UiTestHost.Run(() =>
        {
            var template = Template();

            var view = new FaceplateTemplateEditorView(template);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                Assert.Equal("10", template.Parameters[0].DefaultValue);
                Assert.Equal(0, template.Parameters[0].MinValue);

                ControlWiringFace.Write(view, BoxAt(view, 0, 2, 1), "", pump: 1);
                ControlWiringFace.Write(view, BoxAt(view, 0, 3, 1), "abc", pump: 1);

                Save(view);

                var saved = Assert.Single(template.Parameters);
                Assert.Null(saved.DefaultValue);
                Assert.Null(saved.MinValue);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **面 B 逐条认账**：本文件那 6 条接线（按名找不到）都在本面的表里 ——
    /// 数量与标识都与扫描一致（名字写歪/少认一条都会红）。
    /// </summary>
    [Fact]
    public void EveryAnonymousWiring_IsAccountedFor()
    {
        ControlWiringFace.AssertAnonymousFace(
            "FaceplateTemplateEditorView.axaml", WiredIdentifiers, scanFloor: 6);
    }
}
