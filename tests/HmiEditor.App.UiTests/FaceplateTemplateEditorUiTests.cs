using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using HmiEditor.App.Resources;
using HmiEditor.App.Views;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// <see cref="FaceplateTemplateEditorView"/> 元信息装载/写回的行为测试。
///
/// <para><b>为什么专门测它：</b>该视图的"空值归一化"（空分类 → 通用、空名字 → 新模板）
/// 原先**只**写在 <c>TextChanged</c> 处理器里。Avalonia 12 中程序化赋值
/// <c>TextBox.Text</c> 不触发 <c>TextChanged</c>，于是"装载模板后是否已归一化"
/// 取决于用户有没有碰过输入框 —— 语义不确定。现在装载路径显式调用同一个归一化函数，
/// 这几条测试把该语义钉死（并且不依赖框架未来是否改变事件时机）。</para>
/// </summary>
public class FaceplateTemplateEditorUiTests
{
    private static ControlTemplate MakeTemplate(string name, string category, string description = "说明")
        => new()
        {
            Id = "tpl1",
            Name = name,
            Category = category,
            Description = description,
            Version = 3,
        };

    [Fact]
    public void OpeningEditor_NormalizesBlankMetadataImmediately()
    {
        var result = UiTestHost.Run(() =>
        {
            var tpl = MakeTemplate(name: "   ", category: "", description: "保留我");
            var view = new FaceplateTemplateEditorView(tpl);
            view.Show();
            UiTestHost.Pump();

            // 空名字/空分类在装载时就归一化，不依赖用户是否编辑过输入框
            var normalizedName = tpl.Name;
            var normalizedCategory = tpl.Category;
            var description = tpl.Description;
            // 输入框里也应显示归一化前的原值（用户看到的是模板的真实内容）
            var shownName = view.FindControl<TextBox>("TxtName")!.Text;
            view.Close();
            return (normalizedName, normalizedCategory, description, shownName);
        });

        Assert.Equal(AppLocalization.Service[Lang.FaceplateEditorNewTemplate], result.normalizedName);
        Assert.Equal(AppLocalization.Service[Lang.FaceplateEditorCategoryGeneral], result.normalizedCategory);
        Assert.Equal("保留我", result.description); // 描述不做归一化
        Assert.Equal("   ", result.shownName);
    }

    [Fact]
    public void OpeningEditor_KeepsValidMetadataUntouched()
    {
        var result = UiTestHost.Run(() =>
        {
            var tpl = MakeTemplate(name: "面板A", category: "通用类", description: "d");
            var view = new FaceplateTemplateEditorView(tpl);
            view.Show();
            UiTestHost.Pump();

            var values = (tpl.Name, tpl.Category, tpl.Description);
            view.Close();
            return values;
        });

        // 装载不得把编辑器状态写回（_isLoadingMetadata 守卫 + 显式归一化都不应改动有效值）
        Assert.Equal(("面板A", "通用类", "d"), result);
    }

    [Fact]
    public void TypingMetadata_WritesBackTrimmedValues()
    {
        var result = UiTestHost.Run(() =>
        {
            var tpl = MakeTemplate(name: "旧名", category: "旧类");
            var view = new FaceplateTemplateEditorView(tpl);
            view.Show();
            UiTestHost.Pump();

            var name = view.FindControl<TextBox>("TxtName")!;
            name.Focus();
            UiTestHost.Pump();
            name.SelectAll();
            view.KeyTextInput("  新面板  ");
            UiTestHost.Pump();

            var values = (tpl.Name, tpl.Category);
            view.Close();
            return values;
        });

        Assert.Equal("新面板", result.Name);      // 用户输入路径：trim 后写回
        Assert.Equal("旧类", result.Category);     // 未触碰的字段保持原值
    }

    [Fact]
    public void ClearingName_FallsBackToDefaultName()
    {
        var result = UiTestHost.Run(() =>
        {
            var tpl = MakeTemplate(name: "有名", category: "类");
            var view = new FaceplateTemplateEditorView(tpl);
            view.Show();
            UiTestHost.Pump();

            var name = view.FindControl<TextBox>("TxtName")!;
            name.Focus();
            UiTestHost.Pump();
            name.SelectAll();
            view.KeyTextInput(" ");
            UiTestHost.Pump();

            var value = tpl.Name;
            view.Close();
            return value;
        });

        Assert.Equal(AppLocalization.Service[Lang.FaceplateEditorNewTemplate], result);
    }

    [Fact]
    public void EditModeDoesNotShareStateBetweenInstances()
    {
        var result = UiTestHost.Run(() =>
        {
            var a = MakeTemplate(name: "A", category: "类A");
            var b = MakeTemplate(name: "B", category: "类B");

            var viewA = new FaceplateTemplateEditorView(a);
            viewA.Show();
            UiTestHost.Pump();
            var viewB = new FaceplateTemplateEditorView(b);
            viewB.Show();
            UiTestHost.Pump();

            var values = (a.Name, a.Category, b.Name, b.Category,
                          viewA.FindControl<TextBox>("TxtName")!.Text,
                          viewB.FindControl<TextBox>("TxtName")!.Text);
            viewA.Close();
            viewB.Close();
            return values;
        });

        Assert.Equal(("A", "类A", "B", "类B", "A", "B"), result);
    }
}
