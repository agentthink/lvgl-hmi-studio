using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using HmiEditor.App.Views;
using HmiEditor.Core.Models.Recipe;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`RecipeEditorView` 的 `TextBox → 配方参数模型` 驱动面**（阶段 193）——
/// 把「TextBox 接线普查面」（<see cref="AppTextBoxWiringCensusTests"/>）里
/// <c>RecipeEditorView.axaml</c> 的那 6 条接线从 `census` 升级成 `driven`。
///
/// <para><b>缺口形状</b>：6 个参数编辑框都接在 <c>SyncParamEdit()</c> 上（同一个处理器），
/// 但在此之前**零判据** —— 摘掉任意一条接线、或改动"解析失败 ⇒ 抬到极值"那条口径，都不会红。</para>
///
/// <para><b>两条非平凡口径写进期望值</b>：① <c>TxtParamMin</c> 解析失败 ⇒
/// <c>MinValue = double.MinValue</c>（不是"不动"）；<c>TxtParamMax</c> 同形 ⇒ <c>double.MaxValue</c>；
/// ② <c>TxtParamUnit</c> **不裁剪**（`= TxtParamUnit.Text`）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class RecipeEditorTextBoxWiringTests
{
    /// <summary>一条手势：把 <paramref name="Text"/> 写进 <paramref name="Name"/>，断言参数模型读到 <paramref name="Expected"/>。</summary>
    private sealed record Spec(
        string Name,
        string Setting,
        Func<RecipeEditorTextBoxWiringTests.Fixture, string> Read,
        string Text,
        string Expected);

    /// <summary>夹具：真 `RecipeManager` ＋ 一条配方 ＋ 一个参数。</summary>
    private sealed class Fixture
    {
        public RecipeItem Param { get; } = new()
        {
            Label = "温度设定",
            DefaultValue = 25,
            MinValue = 0,
            MaxValue = 100,
            Unit = "℃",
            SortOrder = 1,
            DataType = "Float",
        };
    }

    private static readonly Spec[] Specs =
    {
        new("TxtParamLabel", "Label", f => f.Param.Label, "设定温度", "设定温度"),
        new("TxtParamDefault", "DefaultValue", f => Num(f.Param.DefaultValue), "26.5", "26.5"),
        new("TxtParamUnit", "Unit", f => f.Param.Unit ?? "", "  ℃  ", "  ℃  "),   // **不裁剪**
        new("TxtParamMin", "MinValue", f => Num(f.Param.MinValue), "5", "5"),
        new("TxtParamMax", "MaxValue", f => Num(f.Param.MaxValue), "88", "88"),
        new("TxtParamSortOrder", "SortOrder",
            f => f.Param.SortOrder.ToString(CultureInfo.InvariantCulture), "7", "7"),

        // 解析失败 ⇒ **抬到极值**（不是"不动"）—— 这两条是这一族里最容易写反的口径
        new("TxtParamMin", "MinValue", f => Num(f.Param.MinValue), "abc", "(double.MinValue)"),
        new("TxtParamMax", "MaxValue", f => Num(f.Param.MaxValue), "abc", "(double.MaxValue)"),
    };

    /// <summary>
    /// **「没有接线」登记表**：配方名与描述走"点保存（`BtnSaveRecipe`）"那条路一次性读取，
    /// 没有 `TextChanged` 接线 —— 不是缺口。
    /// </summary>
    private static readonly Dictionary<string, string> WithoutWiring = new(StringComparer.Ordinal)
    {
        ["TxtRecipeName"] = "配方名由「保存配方」按钮一次性读取（`OnSaveRecipe`），没有 TextChanged 接线",
        ["TxtDescription"] = "同上：描述也走保存那条路",
    };

    private static string Num(double value) => value switch
    {
        double.MinValue => "(double.MinValue)",
        double.MaxValue => "(double.MaxValue)",
        _ => value.ToString(CultureInfo.InvariantCulture),
    };

    [Fact]
    public void EverySpec_WritesTheRecipeParamAsDeclared()
    {
        using var sandbox = new DataRootSandbox("recipe-textbox");

        UiTestHost.Run(() =>
        {
            var depot = new WindowDepot(sandbox.DataRoot);
            var fixture = new Fixture();

            // 夹具：真 `RecipeManager` ＋ 一条配方（`AddRecipe` 会顺手建分组）
            var recipe = new Recipe { Name = "配方甲", GroupName = "罐区" };
            recipe.Items.Add(fixture.Param);
            Assert.True(depot.Recipes.AddRecipe(recipe));

            var window = new RecipeEditorView(depot.Recipes, depot.Variables);
            window.Show();
            UiTestHost.Pump(5);
            try
            {
                // 前提自检①：分组 → 配方 → 参数 三跳选中之后，参数编辑面板必须真的开着
                ControlWiringFace.Find<ListBox>(window, "GroupList").SelectedIndex = 0;
                UiTestHost.Pump(2);
                ControlWiringFace.Find<ListBox>(window, "RecipeList").SelectedIndex = 0;
                UiTestHost.Pump(2);
                ControlWiringFace.Find<ListBox>(window, "ParamList").SelectedIndex = 0;
                UiTestHost.Pump(2);

                Assert.True(ControlWiringFace.Find<Border>(window, "ParamEditPanel").IsVisible,
                    "参数编辑面板没打开 ⇒ 下面的断言不成立（`SyncParamEdit` 首行判 `_selectedParam == null`）");

                // 前提自检②：面板里回填的必须是**模型里的**值
                Assert.Equal("温度设定", ControlWiringFace.Find<TextBox>(window, "TxtParamLabel").Text);

                foreach (var spec in Specs)
                {
                    ControlWiringFace.Write(window, spec.Name, spec.Text);
                    var after = spec.Read(fixture);
                    Assert.True(string.Equals(spec.Expected, after, StringComparison.Ordinal),
                        $"{spec.Name}（{spec.Setting}）驱动 '{spec.Text}' 后读到 '{after}'，期望 '{spec.Expected}'");
                }

                // 反面见证：改的是夹具手里那条参数（不是视图自己造了一份），集合结构没被动
                Assert.Same(fixture.Param, recipe.Items[0]);
                Assert.Single(recipe.Items);
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **单文件双向差集**：6 个参数框都有驱动条目；两个走"保存"的框在登记表里。
    /// </summary>
    [Fact]
    public void EveryTextBoxAndWiring_IsAccountedFor()
    {
        ControlWiringFace.AssertFullyDriven(
            "RecipeEditorView.axaml", Specs.Select(s => s.Name).ToArray(), scanFloor: 6, WithoutWiring);
    }
}
