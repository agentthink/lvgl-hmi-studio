using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **报表设计器判据**（阶段 183）：对话框把"列清单 ＋ 模板"编辑成新模板的四条口径
/// （顺序 · 改名 · 隐藏 · 恢复默认）＋ 三个面板的**接线契约**（设计器按钮 · 套用点只有一处 ·
/// 报表包刻意不套模板）。
///
/// <para>⚠ 对话框本身进 <c>WindowViewSmokeUiTests</c> 的自动收面；本类判的是**行为**。</para>
/// </summary>
public class ReportDesignerGuardTests
{
    private static readonly Dictionary<string, string> Zh = PluginTextLocalizationGuardTests.LoadDictionary("zh-CN");
    private static readonly Dictionary<string, string> En = PluginTextLocalizationGuardTests.LoadDictionary("en-US");

    private static List<ReportColumn> Columns() => new()
    {
        new ReportColumn("a", "A"),
        new ReportColumn("b", "B"),
        new ReportColumn("c", "C"),
    };

    // ────────────────────────── 对话框行为（生产构造路径） ──────────────────────────

    private static StackPanel List(Views.ReportDesignerDialog dialog)
    {
        var list = dialog.FindControl<StackPanel>("ColumnList");
        Assert.NotNull(list);
        return list!;
    }

    private static Grid Row(StackPanel list, int index) => (Grid)list.Children[index]!;

    private static T Cell<T>(Grid row, int column) where T : Control =>
        row.Children.OfType<T>().Single(c => Grid.GetColumn(c) == column);

    private static Button Ok(Views.ReportDesignerDialog dialog)
    {
        var ok = dialog.FindControl<Button>("BtnOk");
        Assert.NotNull(ok);
        return ok!;
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>模板铺开成行：**顺序按模板、被隐藏的列留在原位且未勾选**。</summary>
    [Fact]
    public void Dialog_ShowsEveryColumn_InTemplateOrder()
    {
        UiTestHost.Run(() =>
        {
            var dialog = new Views.ReportDesignerDialog(Columns(), new ReportTemplate(new[]
            {
                new ReportTemplateEntry("c", "第三"),
                new ReportTemplateEntry("b", Hidden: true),
            }));
            dialog.Show();
            UiTestHost.Pump();

            var list = List(dialog);
            Assert.Equal(3, list.Children.Count);   // 前提自检：隐藏的列也在（否则"勾回来"无从谈起）

            Assert.Equal("第三", Cell<TextBox>(Row(list, 0), 1).Text);
            Assert.True(Cell<CheckBox>(Row(list, 0), 0).IsChecked);

            Assert.Equal("B", Cell<TextBox>(Row(list, 1), 1).Text);
            Assert.False(Cell<CheckBox>(Row(list, 1), 0).IsChecked);   // 隐藏 ⇒ 未勾

            Assert.Equal("A", Cell<TextBox>(Row(list, 2), 1).Text);

            dialog.Close();
        });
    }

    /// <summary>改名字与调顺序都会进模板；没动过的列**不写覆盖**。</summary>
    [Fact]
    public void Dialog_RenameAndMove_AreCarriedIntoTheTemplate()
    {
        UiTestHost.Run(() =>
        {
            var dialog = new Views.ReportDesignerDialog(Columns(), null);
            dialog.Show();
            UiTestHost.Pump();

            var list = List(dialog);
            Cell<TextBox>(Row(list, 0), 1).Text = "第一列";
            Click(Cell<Button>(Row(list, 0), 4));   // ↓：把第一行挪到第二
            UiTestHost.Pump(2);

            var template = dialog.Selected();

            // 顺序：b, a, c（a 被挪到 b 后面）；改名只落在 a 上
            Assert.Equal(new[] { "b", "a", "c" }, template.Entries.Select(e => e.Key).ToArray());
            Assert.Null(template.Entries[0].DisplayName);
            Assert.Equal("第一列", template.Entries[1].DisplayName);
            Assert.Null(template.Entries[2].DisplayName);

            // 真的能作用到报表上（"设计器给的东西导得出"这条闭环）
            var table = new ReportTable
            {
                Columns = Columns(),
                Rows = new List<IReadOnlyList<string>> { new[] { "1", "2", "3" } },
            }.ApplyTemplate(template);

            Assert.Equal(new[] { "b", "a", "c" }, table.Columns.Select(c => c.Key).ToArray());
            Assert.Equal(new[] { "B", "第一列", "C" }, table.Columns.Select(c => c.DisplayName).ToArray());
            Assert.Equal(new[] { "2", "1", "3" }, table.Rows[0]);

            dialog.Close();
        });
    }

    /// <summary>一列都不勾 ⇒ 确定不可点（这种模板会让导出抛）；勾回来立刻可点。</summary>
    [Fact]
    public void Dialog_AllHidden_DisablesOk()
    {
        UiTestHost.Run(() =>
        {
            var dialog = new Views.ReportDesignerDialog(Columns(), null);
            dialog.Show();
            UiTestHost.Pump();

            var list = List(dialog);
            for (var i = 0; i < list.Children.Count; i++)
                Cell<CheckBox>(Row(list, i), 0).IsChecked = false;
            UiTestHost.Pump(2);

            Assert.False(Ok(dialog).IsEnabled, "全都不勾时确定键必须不可点");

            Cell<CheckBox>(Row(list, 0), 0).IsChecked = true;
            UiTestHost.Pump(2);
            Assert.True(Ok(dialog).IsEnabled);

            dialog.Close();
        });
    }

    /// <summary>恢复默认：原序、原名、全可见 —— 而且此时"保存"等于**没设模板**（不落空壳）。</summary>
    [Fact]
    public void Dialog_Reset_RestoresDefaults_AndSavesNothing()
    {
        UiTestHost.Run(() =>
        {
            var dialog = new Views.ReportDesignerDialog(Columns(), new ReportTemplate(new[]
            {
                new ReportTemplateEntry("c", "第三"),
                new ReportTemplateEntry("a", Hidden: true),
            }));
            dialog.Show();
            UiTestHost.Pump();

            var reset = dialog.FindControl<Button>("BtnReset");
            Assert.NotNull(reset);
            Click(reset!);
            UiTestHost.Pump(2);

            var list = List(dialog);
            Assert.Equal(new[] { "A", "B", "C" }, new[]
            {
                Cell<TextBox>(Row(list, 0), 1).Text,
                Cell<TextBox>(Row(list, 1), 1).Text,
                Cell<TextBox>(Row(list, 2), 1).Text,
            });
            Assert.All(list.Children, c => Assert.True(((Grid)c!).Children.OfType<CheckBox>().Single().IsChecked));

            Assert.True(dialog.Selected().IsEmpty, "一键未动的结果必须交回空模板（＝没设过）");

            dialog.Close();
        });
    }

    /// <summary>没动过就保存 ⇒ 空模板（"存了个空壳"与"没设过"在行为上等价）。</summary>
    [Fact]
    public void Dialog_Untouched_SavesNothing()
    {
        UiTestHost.Run(() =>
        {
            var dialog = new Views.ReportDesignerDialog(Columns(), null);
            dialog.Show();
            UiTestHost.Pump();

            Assert.True(dialog.Selected().IsEmpty);
            Assert.Equal(3, List(dialog).Children.Count);

            dialog.Close();
        });
    }

    /// <summary>
    /// 设计器要的五条文案两本字典都在。
    ///
    /// <para>⚠ 这里刻意用**字面量键**、不写 <c>Lang.ReportDesigner*</c>：那几个常量在**产品代码里**
    /// 只出现在 <c>{res:Loc …}</c> 的花括号里，写常量名会把它们从
    /// <c>LangConstantUsageGuardTests</c> 的"无代码引用"基线里**写活** —— 正是那份守卫类注释里
    /// 记的 `#48` 形状（守卫自己的断言成了常量唯一的存在理由）。前面的对话框判据都照此办理。</para>
    /// </summary>
    [Fact]
    public void DesignerKeys_ExistInBothDictionaries()
    {
        foreach (var key in new[]
                 {
                     "report.designer.title", "report.designer.hint", "report.designer.btn",
                     "report.designer.reset", "ui.op.report.designer",
                 })
        {
            Assert.True(Zh.ContainsKey(key), $"zh 字典缺键：{key}");
            Assert.True(En.ContainsKey(key), $"en 字典缺键：{key}");
        }
    }

    // ────────────────────────── 三个面板的接线契约（源码级） ──────────────────────────

    private static readonly (string View, string Axaml, string Button, string Source)[] Panels =
    {
        ("AuditLogView.axaml.cs", "AuditLogView.axaml", "DesignerBtn", "Audit"),
        ("AlarmListView.axaml.cs", "AlarmListView.axaml", "BtnReportDesigner", "Alarm"),
        ("TrendConfigView.axaml.cs", "TrendConfigView.axaml", "BtnReportDesigner", "History"),
    };

    private static string Code(string view) => SourceScanner.StripComments(File.ReadAllText(
        Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", view)));

    /// <summary>每个面板：设计器按钮在 XAML 里、被订阅、走同一处出口、用自己的数据源键。</summary>
    [Fact]
    public void EveryPanel_HasTheDesignerWired()
    {
        foreach (var (view, axaml, button, source) in Panels)
        {
            var code = Code(view);
            var xaml = File.ReadAllText(Path.Combine(SourceScanner.RepoRoot, "src", "HmiEditor.App", "Views", axaml));

            Assert.Contains($"x:Name=\"{button}\"", xaml, StringComparison.Ordinal);
            Assert.Contains($"{button}.Click += OnEditReportTemplate", code, StringComparison.Ordinal);
            Assert.Contains("ReportExport.EditTemplateAsync", code, StringComparison.Ordinal);
            Assert.Contains($"ReportSourceKeys.{source}", code, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// **套用点只有一处，而且只在「导出报表」那条路上**：报表包是"留档/交付"（全列全时段）
    /// ⇒ 刻意**不**套模板。这条分工是设计决定，所以用判据钉住（别顺手加一行）。
    /// </summary>
    [Fact]
    public void TemplateIsAppliedOnlyOnTheReportPath()
    {
        foreach (var (view, _, _, _) in Panels)
        {
            var code = Code(view);
            var applied = code.Split("ReportExport.ApplyTemplate", StringSplitOptions.None).Length - 1;

            Assert.Equal(1, applied);
            Assert.Contains("ReportExport.SaveBundleAsync", code, StringComparison.Ordinal);
        }
    }
}
