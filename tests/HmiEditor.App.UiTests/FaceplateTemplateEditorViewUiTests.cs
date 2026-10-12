using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`FaceplateTemplateEditorView`（模板编辑器）的按钮面**（阶段 258；登记 `#39` 残余面）。
///
/// <para><b>为什么单独立判据：</b>它由模板库那条链 `ShowModalDialog(...)` 打开 ⇒ 不是"菜单打开的窗口"
/// （第三层够不到）、也不是停靠面板（第四层够不到）⇒ 专题判据（第九次）。</para>
///
/// <para><b>5 个具名动作控件与桶</b>：`BtnAddParam` = **Moves**（参数 +1 **并选中它**）；
/// `BtnRemoveParam` = **Inert**（**裸点**：`_selectedParamIndex == -1` ⇒ 首行 if 不成立、一字不变）
/// ＋ **Moves**（在 `BtnAddParam` 之后点：删掉刚加的那个 —— **顺序即语义**）；
/// `BtnSave` = **Moves**（把参数同步回模板 ＋ 编辑时**版本 +1** ＋ `Result` 拿到模板 ＋ 关窗）；
/// `BtnCancel` = **Moves**（`Result = null` ＋ 关窗）；`BtnTitleClose` = **Never**（标题栏关闭按钮）。</para>
///
/// <para><b>不抓什么：</b>① 不判卡片里的输入框（那是 `FaceplateTemplateEditorTextBoxWiringTests` 的地盘）；
/// ② 不判图元/缩略图渲染；③ 不判真实命中测试。</para>
/// </summary>
[Collection("Language-global")]
public class FaceplateTemplateEditorViewUiTests
{
    private readonly ITestOutputHelper _out;

    public FaceplateTemplateEditorViewUiTests(ITestOutputHelper o) => _out = o;

    /// <summary>夹具：一个编辑态模板（2 个参数、版本 3 —— 非 1 才看得出"编辑时递增"）。</summary>
    private static ControlTemplate Template()
    {
        var template = new ControlTemplate
        {
            Id = "tpl-editor", Name = "模板A", Category = "通用", Version = 3,
        };
        template.Parameters.Add(new TemplateParameter { Name = "P1", DisplayName = "参数1" });
        template.Parameters.Add(new TemplateParameter { Name = "P2", DisplayName = "参数2" });
        return template;
    }

    private static FaceplateTemplateEditorView Open(ControlTemplate template)
    {
        var view = new FaceplateTemplateEditorView(template);
        view.Show();
        UiTestHost.Pump(5);
        return view;
    }

    private static void Click(Control root, string name)
    {
        ControlWiringFace.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(3);
    }

    /// <summary>参数列表的**卡片数**（`RefreshParamList` 每个参数一张卡；空列表时是一句提示）。</summary>
    private static int ParamCards(FaceplateTemplateEditorView view)
    {
        var host = view.FindControl<StackPanel>("ParamListPanel")
                   ?? throw new InvalidOperationException("找不到 ParamListPanel —— XAML 结构变了");
        return host.Children.OfType<Border>().Count();
    }

    [Fact]
    public void Face_MatchesTheDeclaredTable()
    {
        UiTestHost.Run(() =>
        {
            var view = Open(Template());
            try
            {
                var actual = view.GetLogicalDescendants().OfType<Button>()
                    .Where(b => b.GetType() == typeof(Button))
                    .Where(b => b.Name is not null)
                    .Select(b => b.Name!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(new[] { "BtnAddParam", "BtnCancel", "BtnRemoveParam", "BtnSave", "BtnTitleClose" }, actual);
            }
            finally { view.Close(); }
        });
    }

    [Fact]
    public void RemoveParam_AloneIsANoOp_ButDeletesTheJustAddedParam()
    {
        UiTestHost.Run(() =>
        {
            var view = Open(Template());
            try
            {
                Assert.Equal(2, ParamCards(view));

                // ① 裸点：没有选中项 ⇒ `_selectedParamIndex == -1` ⇒ 一字不变（Inert 档，如实判）
                Click(view, "BtnRemoveParam");
                Assert.Equal(2, ParamCards(view));

                // ② 先加一个（加完**自动选中**）⇒ 2 → 3
                Click(view, "BtnAddParam");
                Assert.Equal(3, ParamCards(view));

                // ③ 再删 ⇒ 3 → 2（⚠ 顺序即语义：没有 ②，这条也是 no-op）
                Click(view, "BtnRemoveParam");
                Assert.Equal(2, ParamCards(view));

                JudgeReadings.Write(_out, "模板编辑器 · 参数增删（#39 残余面 · 阶段 258）", new[]
                {
                    "面：具名 Button 5 个 —— BtnAddParam / BtnRemoveParam / BtnSave / BtnCancel / BtnTitleClose（双向差集，已断言）",
                    "裸点 BtnRemoveParam（无选中）⇒ 卡片 2 → 2（提前 return 档，如实判）",
                    "BtnAddParam ⇒ 2 → 3（并自动选中新参数）；随后 BtnRemoveParam ⇒ 3 → 2",
                });
            }
            finally { view.Close(); }
        });
    }

    [Fact]
    public void Save_SyncsParametersToTheTemplate_AndBumpsTheVersion()
    {
        UiTestHost.Run(() =>
        {
            var template = Template();
            var view = Open(template);
            var closed = 0;
            view.Closed += (_, _) => closed++;
            try
            {
                Click(view, "BtnAddParam");       // 参数 2 → 3
                Assert.Equal(2, template.Parameters.Count);   // 还没保存 ⇒ 模板没变

                Click(view, "BtnSave");

                Assert.Equal(3, template.Parameters.Count);   // 同步回模板
                Assert.Equal(4, template.Version);            // 编辑时版本 +1（原 3）
                Assert.Same(template, view.Result);
                Assert.Equal(1, closed);
                Assert.False(view.IsVisible);

                JudgeReadings.Write(_out, "模板编辑器 · 保存（#39 残余面 · 阶段 258）", new[]
                {
                    "面：具名 Button 5 个（双向差集，已断言）",
                    $"参数：模板 {template.Parameters.Count} 条（视图里加了 1 条再保存）；版本 {template.Version}（原 3 ⇒ 编辑时 +1）",
                    $"Result={(view.Result is null ? "null" : view.Result.Name)}；Closed 触发 {closed} 次",
                });
            }
            finally { view.Close(); }
        });
    }

    [Fact]
    public void Cancel_ClosesWithoutResult()
    {
        UiTestHost.Run(() =>
        {
            var view = Open(Template());
            var closed = 0;
            view.Closed += (_, _) => closed++;
            try
            {
                Click(view, "BtnCancel");

                Assert.Null(view.Result);
                Assert.Equal(1, closed);
                Assert.False(view.IsVisible);
            }
            finally { view.Close(); }
        });
    }
}
