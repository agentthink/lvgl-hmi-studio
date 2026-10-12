using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using HmiEditor.App.Views;
using HmiEditor.App.Views.Editors;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// P1-1：属性面板「目标属性」下拉的行为测试。
///
/// <para><b>为什么需要它：</b>绑定条目是按 <see cref="VariableBindingData.PropertyName"/> 分开存放的。
/// 在只有一个写死 <c>"Text"</c> 的时候，界面看起来"能选颜色"，实际写进去的还是值通路的绑定 ——
/// 症状是同一个控件想同时绑 <c>Text</c> 与 <c>TextColor</c> 时互相覆盖，而且只能在工程 JSON 里手改。
/// 这类缺陷在 UI 上完全看不出来（下拉选项、表达式框、错误提示全都正常），
/// 只有把真实面板建起来、真的切一次下拉、再去数模型里的绑定条目，才能证明"写对了位置"。</para>
///
/// <para><b>驱动方式：</b>一律走「下拉选 Tag → 断言模型」。不读源码文本、不反射私有字段，
/// 因此重构（改名 / 抽方法 / 换控件类型）不会误伤。</para>
/// </summary>
public class PropertyPanelTargetPropertyTests
{
    private const string TargetCombo = "TargetPropertyCombo";
    private const string KindCombo = "BindingKindCombo";

    private sealed record Fixture(Window Window, PropertyPanelView Panel, HmiControl Control);

    /// <summary>
    /// 把面板挂进真实窗口。<paramref name="configure"/> 在 <c>BindControl</c> **之前**执行 ——
    /// 变量选择器要在绑定前注入 <see cref="VariableManager"/>，否则树是空的。
    ///
    /// <para><b><paramref name="expandBindingSection"/> 必须在 <c>Show()</c> 之前设</b>（实测 2026-09-19）：
    /// Avalonia 的 <c>Expander</c> 在 headless 宿主里**只有首次布局之前就处于展开态**才会创建内容子树。
    /// <c>Show()</c> 之后再改 <c>IsExpanded</c>，内容**永不 attach** ——
    /// <c>ApplyTemplate()</c> + <c>InvalidateMeasure()</c> + 强制 <c>Measure/Arrange</c> + 多轮 Pump
    /// 全都救不回来（实测：Expander 自身 attached、Bounds 停在折叠态高 48、视觉后代恒 10 个）。
    /// 内容不 attach 的控件既进不了可视树、也拿不到焦点。
    /// 故要驱动折叠区内的控件（真实键盘输入），必须在 Show 前展开。</para>
    /// </summary>
    private static Fixture Host(
        HmiControl control,
        Action<PropertyPanelView>? configure = null,
        bool expandBindingSection = false)
    {
        var panel = new PropertyPanelView();
        if (expandBindingSection)
        {
            var expander = panel.FindControl<Expander>("VariableBindingExpander");
            Assert.NotNull(expander);
            expander!.IsExpanded = true;
        }

        var window = new Window { Width = 900, Height = 900, Content = panel };
        window.Show();
        UiTestHost.Pump();

        configure?.Invoke(panel);
        panel.BindControl(control);
        UiTestHost.Pump();

        return new Fixture(window, panel, control);
    }

    /// <summary>
    /// 用**真实键盘输入**写入文本框。
    ///
    /// <para>不能省成「赋值 + 手动触发 <c>TextChanged</c>」：Avalonia 12 里程序化赋值
    /// <c>TextBox.Text</c> **不触发** <c>TextChanged</c>（实测：赋值后模型的 Expression 仍是 null），
    /// 而手动 <c>RaiseEvent</c> 只是模拟、漏掉了真实输入路径上的其它环节。
    /// 前提是目标控件真的在可视树里 —— 见
    /// <see cref="Host"/> 的 <c>expandBindingSection</c>。</para>
    /// </summary>
    private static void TypeInto(Window window, TextBox box, string text)
    {
        Assert.True(box.IsAttachedToVisualTree(),
            "控件不在可视树里，键盘输入送不到（折叠 Expander 的内容在 headless 下不会 attach）");
        box.Focus();
        UiTestHost.Pump();
        box.SelectAll();
        window.KeyTextInput(text);
        UiTestHost.Pump();
    }

    private static HmiControl MakeControl() => new()
    {
        Id = "c1",
        Type = HmiControlType.Label,
        Name = "lbl1",
        Text = "你好",
        X = 10, Y = 20, Width = 120, Height = 30,
    };

    private static ComboBox Combo(PropertyPanelView panel, string name)
    {
        var found = panel.FindControl<ComboBox>(name);
        Assert.NotNull(found);
        return found!;
    }

    private static TextBox Box(PropertyPanelView panel, string name)
    {
        var found = panel.FindControl<TextBox>(name);
        Assert.NotNull(found);
        return found!;
    }

    private static int IndexOfTag(ComboBox combo, string tag)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    private static void SelectByTag(ComboBox combo, string tag)
    {
        var index = IndexOfTag(combo, tag);
        // 找不到就失败，而不是静默留在原来那一项 —— 否则整套断言会拿错误的前提跑下去。
        Assert.True(index >= 0, $"下拉里没有 Tag = {tag} 的项");
        combo.SelectedIndex = index;
        UiTestHost.Pump();
    }

    private static string CurrentTag(ComboBox combo) =>
        combo.SelectedItem is ComboBoxItem item ? item.Tag?.ToString() ?? "" : "";

    // ── 1. 下拉项与 Core 注册表同源 ─────────────────────────────

    /// <summary>
    /// 下拉里的每个属性名都必须能被 Core 注册表认出（以及「哪几个是视觉属性」这件事只有一处定义）。
    /// 这条锁的是 UI ↔ Core 的**漂移**：注册表加了新视觉属性、下拉忘了加，或反之，
    /// 都会让生成端「静默不发射」——没有这条测试就只能靠人去比对两处清单。
    /// </summary>
    [Fact]
    public void TargetPropertyCombo_ItemsMatchCoreRegistry()
    {
        UiTestHost.Run(() =>
        {
            var f = Host(MakeControl());
            var combo = Combo(f.Panel, TargetCombo);

            var tags = Enumerable.Range(0, combo.Items.Count)
                .Select(i => (combo.Items[i] as ComboBoxItem)?.Tag?.ToString() ?? "")
                .ToList();

            // 值属性（历史默认）在前，视觉属性在后
            Assert.Equal("Text", tags[0]);

            var visualTags = tags.Where(BindingVisualProperty.IsVisual).ToList();
            Assert.Equal(
                new[] { "TextColor", "BgColor", "IsVisible" },
                visualTags);

            // 每个视觉属性名都能在注册表里查到完整定义（Kind + 应用方式）
            foreach (var tag in visualTags)
            {
                Assert.NotNull(BindingVisualProperty.Find(tag));
            }

            f.Window.Close();
        });
    }

    // ── 2. 表达式写入落在「当前目标属性」上 ─────────────────────

    [Fact]
    public void ExpressionBinding_IsStoredUnderSelectedTargetProperty()
    {
        UiTestHost.Run(() =>
        {
            var f = Host(MakeControl());

            // 未做任何选择时是值属性（历史行为不回退）
            Assert.Equal("Text", CurrentTag(Combo(f.Panel, TargetCombo)));

            SelectByTag(Combo(f.Panel, TargetCombo), "TextColor");
            SelectByTag(Combo(f.Panel, KindCombo), "Expression");

            var binding = Assert.Single(f.Control.VariableBindings);
            Assert.Equal("TextColor", binding.PropertyName);
            Assert.Equal(BindingSourceKind.Expression, binding.Kind);

            // 值通路**没有**被顺手创建出一条 —— 这正是旧硬编码会犯的错
            Assert.DoesNotContain(f.Control.VariableBindings, b => b.PropertyName == "Text");

            f.Window.Close();
        });
    }

    /// <summary>视觉属性同样可以是「变量」源（不只有表达式），此时绑定的 PropertyName 也要写对。</summary>
    [Fact]
    public void VariableBinding_IsStoredUnderSelectedTargetProperty()
    {
        UiTestHost.Run(() =>
        {
            var f = Host(MakeControl(), p => p.SetVariableManager(BuildVariableManager()));

            SelectByTag(Combo(f.Panel, TargetCombo), "IsVisible");
            UiTestHost.Pump();

            // 通过变量选择器真正选一个变量：点「浏览」→ 树被填充 → 选中叶子节点
            var picker = f.Panel.FindControl<VariablePicker>("VariablePicker");
            Assert.NotNull(picker);
            var browse = picker!.FindControl<Button>("BrowseBtn");
            Assert.NotNull(browse);
            browse!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            UiTestHost.Pump();

            var tree = picker.FindControl<TreeView>("VarTree");
            Assert.NotNull(tree);
            var group = tree!.Items[0] as TreeViewItem;
            Assert.NotNull(group);
            var leaf = group!.Items[0] as TreeViewItem;
            Assert.NotNull(leaf);

            tree.SelectedItem = leaf;
            UiTestHost.Pump();

            var binding = Assert.Single(f.Control.VariableBindings);
            Assert.Equal("IsVisible", binding.PropertyName);
            Assert.Equal("v_temp", binding.VariableId);

            f.Window.Close();
        });
    }

    private static VariableManager BuildVariableManager()
    {
        var vm = new VariableManager();
        vm.Variables.Add(new Variable { Id = "v_temp", Name = "temp", DataType = "Double" });
        return vm;
    }

    // ── 3. 同控件多属性绑定共存，切换时回填各自的内容 ─────────────

    /// <summary>
    /// 一个 Label 同时绑 <c>Text</c>（显示数值）与 <c>TextColor</c>（按阈值变色）——
    /// 这是 P1-1 的主要用例，也是旧实现直接做不到的事。
    /// </summary>
    [Fact]
    public void ValueAndVisualBindings_Coexist_AndEachRefillsItsOwnContent()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            control.VariableBindings.Add(new VariableBindingData
            {
                PropertyName = "Text",
                VariableId = "v_temp",
                Direction = BindingDirection.ReadWrite,
                Kind = BindingSourceKind.Expression,
                Expression = "temp > 100",
            });

            var f = Host(control, expandBindingSection: true);

            // 载入时：目标属性落在第一条读绑定的属性上，表达式也回填了
            Assert.Equal("Text", CurrentTag(Combo(f.Panel, TargetCombo)));
            Assert.Equal("Expression", CurrentTag(Combo(f.Panel, KindCombo)));
            Assert.Equal("temp > 100", Box(f.Panel, "ExpressionInput").Text);

            // 切到 TextColor：该属性自己还没有绑定 ⇒ 界面清空成「变量」源，而不是留着 Text 的表达式
            SelectByTag(Combo(f.Panel, TargetCombo), "TextColor");
            Assert.Equal("Variable", CurrentTag(Combo(f.Panel, KindCombo)));
            Assert.Equal("", Box(f.Panel, "ExpressionInput").Text);

            // 在 TextColor 下写一条表达式 ⇒ 两条并存，且文本落在 TextColor 条目上（不是 Text 的）。
            // 走**真实键盘输入**（Host 于 Show 之前展开了折叠区，所以内容在可视树里）。
            SelectByTag(Combo(f.Panel, KindCombo), "Expression");
            TypeInto(f.Window, Box(f.Panel, "ExpressionInput"), "temp > 200");
            Assert.Equal("temp > 200", Box(f.Panel, "ExpressionInput").Text);

            Assert.Equal(2, control.VariableBindings.Count);

            var text = control.VariableBindings.Single(b => b.PropertyName == "Text");
            var color = control.VariableBindings.Single(b => b.PropertyName == "TextColor");
            Assert.Equal("temp > 100", text.Expression);
            Assert.Equal("temp > 200", color.Expression);

            // 切回 Text：回填的是 **Text 自己** 的内容，不是刚编辑的 TextColor
            SelectByTag(Combo(f.Panel, TargetCombo), "Text");
            Assert.Equal("Expression", CurrentTag(Combo(f.Panel, KindCombo)));
            Assert.Equal("temp > 100", Box(f.Panel, "ExpressionInput").Text);

            // 再切到 TextColor：回填的是它自己的表达式
            SelectByTag(Combo(f.Panel, TargetCombo), "TextColor");
            Assert.Equal("Expression", CurrentTag(Combo(f.Panel, KindCombo)));
            Assert.Equal("temp > 200", Box(f.Panel, "ExpressionInput").Text);

            f.Window.Close();
        });
    }

    // ── 4. 负控：切换目标属性本身不写模型 ───────────────────────

    /// <summary>
    /// 切下拉是「换个视角看同一条控件」，不是编辑动作 —— 不得创建/改写/删除任何绑定。
    /// （<c>PropertyPanelWiringTests</c> 把所有编辑器都要求「改了模型就变」，这条是它豁免
    /// <c>TargetPropertyCombo</c> 的依据：它是真的不写。）
    /// </summary>
    [Fact]
    public void SwitchingTargetProperty_DoesNotMutateModel()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            control.VariableBindings.Add(new VariableBindingData
            {
                PropertyName = "Text",
                VariableId = "v_temp",
                Direction = BindingDirection.ReadWrite,
                Kind = BindingSourceKind.Expression,
                Expression = "temp > 100",
            });

            var f = Host(control);
            var combo = Combo(f.Panel, TargetCombo);

            foreach (var tag in new[] { "TextColor", "BgColor", "IsVisible", "Text" })
            {
                SelectByTag(combo, tag);
                var binding = Assert.Single(control.VariableBindings);
                Assert.Equal("Text", binding.PropertyName);
                Assert.Equal("v_temp", binding.VariableId);
                Assert.Equal("temp > 100", binding.Expression);
            }

            f.Window.Close();
        });
    }

    // ── 5. 下拉里没有的属性名 ⇒ 回落值属性（已知局限，锁住现状）────

    /// <summary>
    /// 旧工程里可能有 <c>PropertyName = "Value"</c>（Slider/Knob 的值通路）之类的条目，
    /// 而当前下拉只列 4 项。此时必须**回落**到值属性并把该绑定的变量显示出来，
    /// 而不是让下拉停在空白或不存在的项上 —— 否则用户一进面板就看到"没绑定"。
    /// </summary>
    [Fact]
    public void UnknownPropertyName_FallsBackToValueProperty_AndStillShowsItsVariable()
    {
        UiTestHost.Run(() =>
        {
            var control = MakeControl();
            control.VariableBindings.Add(new VariableBindingData
            {
                PropertyName = "Value",
                VariableId = "v_level",
                Direction = BindingDirection.ReadWrite,
            });

            var f = Host(control);

            Assert.Equal("Text", CurrentTag(Combo(f.Panel, TargetCombo)));

            var picker = f.Panel.FindControl<VariablePicker>("VariablePicker");
            Assert.NotNull(picker);
            Assert.Equal("v_level", picker!.SelectedVariableName);

            f.Window.Close();
        });
    }
}
