using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HmiEditor.App.Views;
using HmiEditor.Core.Reporting;
using Xunit;
using Xunit.Abstractions;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`ReportColumnDialog`（报表"选列"对话框）的按钮面**（阶段 256；登记 `#39` 残余面）。
///
/// <para><b>为什么单独立判据：</b>它是**导出报表**那条链弹出来的第二个模态窗
/// （`ReportExport.ChooseColumnsAsync(owner, table)` → `dialog.ShowDialog&lt;IReadOnlyList&lt;string&gt;?&gt;(window)`），
/// 不是"菜单打开的窗口"（第三层够不到）、也不是停靠面板（第四层够不到）⇒ 专题判据（第六次）。</para>
///
/// <para><b>关键的一条：这个对话框对调用方的**契约**是返回值</b> ——
/// `Close(SelectedKeys())`（OK）vs `Close(null)`（取消），调用方正是靠 `null` 区分"取消"与"选了空集"。
/// ⇒ 两条"结果"腿走 **`ShowDialog`**（与生产路径逐字同形），断言**返回值本身**，而不是只断言"窗关了"。</para>
///
/// <para><b>5 个具名动作控件与桶</b>（面闭合见 <see cref="Face_MatchesTheDeclaredTable"/>）：
/// `BtnSelectNone` / `BtnSelectAll` = **Moves**（勾选态 ＋ **联动的 `BtnOk.IsEnabled`**）·
/// `BtnOk` / `BtnCancel` = **Moves**（返回值）· `BtnTitleClose` = **Never**（标题栏关闭按钮 ⇒ 与 `BtnCancel`
/// 同后果，保留一条）。</para>
///
/// <para><b>不抓什么：</b>① 不判"选完之后表格怎么被裁剪"（那是 `ReportColumnSelectionGuardTests` 的地盘）；
/// ② 不判文案（列名来自调用方传进来的 `ReportColumn.DisplayName`）；③ 不判真实命中测试。</para>
/// </summary>
[Collection("Language-global")]
public class ReportColumnDialogUiTests
{
    private readonly ITestOutputHelper _out;

    public ReportColumnDialogUiTests(ITestOutputHelper o) => _out = o;

    private static IReadOnlyList<ReportColumn> Columns() => new[]
    {
        new ReportColumn("time", "时间"),
        new ReportColumn("value", "数值"),
        new ReportColumn("unit", "单位"),
    };

    /// <summary>
    /// 造窗（**只构造、不 Show**）：面/勾选态/返回值的腿都不需要显示 —— 与 `LuaHelpDialogUiTests` 同处置。
    /// 两条 `ShowDialog` 腿自己会显示。
    /// </summary>
    private static ReportColumnDialog New() => new(Columns());

    private static Button Btn(Control root, string name) => ControlWiringFace.Find<Button>(root, name);

    private static List<CheckBox> Boxes(ReportColumnDialog dialog)
    {
        var host = dialog.FindControl<StackPanel>("ColumnList")
                   ?? throw new InvalidOperationException("找不到 ColumnList —— 对话框的 XAML 结构变了");
        return host.Children.OfType<CheckBox>().ToList();
    }

    private static void Click(Control root, string name)
    {
        Btn(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(3);
    }

    [Fact]
    public void Face_MatchesTheDeclaredTable()
    {
        UiTestHost.Run(() =>
        {
            var dialog = New();
            try
            {
                var actual = dialog.GetLogicalDescendants().OfType<Button>()
                    .Where(b => b.GetType() == typeof(Button))   // CheckBox 也是 Button 的子类 ⇒ 按精确类型收
                    .Where(b => b.Name is not null)
                    .Select(b => b.Name!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();

                // 双向差集：少一个（控件被删/改名）与多一个（新按钮没人分类）都红
                Assert.Equal(new[] { "BtnCancel", "BtnOk", "BtnSelectAll", "BtnSelectNone", "BtnTitleClose" }, actual);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void SelectNone_ClearsEveryColumn_AndDisablesOk()
    {
        UiTestHost.Run(() =>
        {
            var dialog = New();
            try
            {
                Assert.Equal(3, Boxes(dialog).Count);
                Assert.All(Boxes(dialog), b => Assert.True(b.IsChecked == true));   // 默认全选
                Assert.Equal(3, dialog.SelectedKeys().Count);
                Assert.True(Btn(dialog, "BtnOk").IsEnabled);

                // 记下**点击前**的两个数（读数在点击之后才写，别把"默认态"写成"点了之后"）
                var initialKeys = dialog.SelectedKeys().Count;
                var initialOkEnabled = Btn(dialog, "BtnOk").IsEnabled;

                Click(dialog, "BtnSelectNone");

                Assert.All(Boxes(dialog), b => Assert.True(b.IsChecked != true));
                Assert.Empty(dialog.SelectedKeys());
                Assert.False(Btn(dialog, "BtnOk").IsEnabled);   // ⚠ **联动**：一列都没选 ⇒ OK 关掉

                JudgeReadings.Write(_out, "报表选列 · 全不选（#39 残余面 · 阶段 256）", new[]
                {
                    "面：具名 Button 5 个 —— BtnSelectAll / BtnSelectNone / BtnOk / BtnCancel / BtnTitleClose（双向差集，已断言）",
                    $"三列默认全选 ⇒ `SelectedKeys()`={initialKeys} 个；BtnOk.IsEnabled={initialOkEnabled}",
                    $"点「全不选」后：勾选 {Boxes(dialog).Count(b => b.IsChecked == true)} / {Boxes(dialog).Count}；"
                        + $"SelectedKeys()={dialog.SelectedKeys().Count} 个；BtnOk.IsEnabled={Btn(dialog, "BtnOk").IsEnabled}",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void SelectAll_ReChecksEveryColumn_AndEnablesOk()
    {
        UiTestHost.Run(() =>
        {
            var dialog = New();
            try
            {
                Click(dialog, "BtnSelectNone");
                Assert.Empty(dialog.SelectedKeys());

                Click(dialog, "BtnSelectAll");

                Assert.All(Boxes(dialog), b => Assert.True(b.IsChecked == true));
                Assert.Equal(Columns().Select(c => c.Key), dialog.SelectedKeys());   // 顺序沿用原表
                Assert.True(Btn(dialog, "BtnOk").IsEnabled);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public async Task Ok_ReturnsTheSelectedKeys()
    {
        await UiTestHost.RunAsync(async () =>
        {
            var owner = new Window { Width = 400, Height = 300 };
            owner.Show();
            UiTestHost.Pump(3);
            try
            {
                var dialog = new ReportColumnDialog(Columns());
                // 与生产路径逐字同形（`ReportExport.ChooseColumnsAsync` 就是这么调的）
                var pending = dialog.ShowDialog<IReadOnlyList<string>?>(owner);
                UiTestHost.Pump(3);

                Click(dialog, "BtnOk");

                var keys = await pending;
                Assert.Equal(new[] { "time", "value", "unit" }, keys);

                JudgeReadings.Write(_out, "报表选列 · 确定（#39 残余面 · 阶段 256）", new[]
                {
                    "面：具名 Button 5 个（双向差集，已断言）",
                    $"`ShowDialog` 的返回值：{keys!.Count} 个键 —— `{string.Join(", ", keys)}`",
                    "调用方契约：OK ⇒ 勾选的键；Cancel ⇒ `null`（见下一条）",
                });
            }
            finally { owner.Close(); }
        });
    }

    [Fact]
    public async Task Cancel_ReturnsNull()
    {
        await UiTestHost.RunAsync(async () =>
        {
            var owner = new Window { Width = 400, Height = 300 };
            owner.Show();
            UiTestHost.Pump(3);
            try
            {
                var dialog = new ReportColumnDialog(Columns());
                var pending = dialog.ShowDialog<IReadOnlyList<string>?>(owner);
                UiTestHost.Pump(3);

                Click(dialog, "BtnCancel");

                var keys = await pending;
                Assert.Null(keys);     // ⚠ 与"选了空集"必须可区分 —— 那正是 `null` 这一档的意义

                JudgeReadings.Write(_out, "报表选列 · 取消（#39 残余面 · 阶段 256）", new[]
                {
                    "面：具名 Button 5 个（双向差集，已断言）",
                    $"`ShowDialog` 的返回值：{(keys is null ? "null" : keys.Count + " 个键")}",
                    "⚠ 这一档不能用「空集」冒充：调用方 `ChooseColumnsAsync` 判的就是 `keys is null ⇒ 取消`",
                });
            }
            finally { owner.Close(); }
        });
    }
}
