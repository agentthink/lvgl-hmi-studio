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
/// **`TableCellEditorDialog` 内部按钮的判据**（阶段 250；登记 `#39` 残余面清单里的第 2 格）。
///
/// <para><b>为什么这一格值得做：</b>阶段 247 的残余面实测量出它有 **3 个按钮全部无人点名**
/// （`CancelBtn` / `OkBtn` / `InsertVarBtn`），而 `OkBtn` 是那一批候选里**模型效果最"硬"**的一条 ——
/// 它把整张矩阵写回 <see cref="HmiControl"/> 的**两个字段**（`HeaderTexts` ＋ `Cells`），
/// 且写入规则有三条真实细节（**第 0 行是表头**、**截尾空列**、`{变量}` ⇒ 绑定格）。
///
/// <para><b>为什么不去第三层（`DialogInnerInvocationUiTests`）：</b>§164 原计划是"进第三层"，
/// 但读那一层之后发现**它的宿主定义是「从主窗口菜单打开的 Window」**（`Hosts[].Menu`），
/// 而本对话框的打开链是 `PropertyPanelView.EditCellsBtn → OnEditCellsClicked → ShowDialog(owner)` ——
/// `PropertyPanelView` 是**主窗口内嵌面板**、不是菜单打开的窗 ⇒ 塞不进去。
/// 于是按同一口径（对话框内按钮 ＋ 真效果）**另立本类**：走生产构造路径
/// `new TableCellEditorDialog(control, variables)` ＋ `Show()`，再按名驱动三个按钮。
/// ⚠ 这是"读一层代码"与"真去用那一层"的差别 —— 与 §165.2 那次更正同源。</para>
///
/// <para><b>判据形状（四条腿 + 一条面）：</b>
/// <list type="number">
///   <item><b>面</b>：对话框里**具名**的 <see cref="Button"/> 必须恰好是那三个（双向差集 —— 新增按钮会被点名）；</item>
///   <item><b>`InsertVarBtn`</b>：先证明**没有焦点时是空操作**（提前 `return` 档），再聚焦一个格 ⇒ 只有那个格被写成 `{变量Id}`，**模型一动不动**；</item>
///   <item><b>`OkBtn`</b>：表头 ＋ 单元格写回模型（静态文本 / `{变量}` 两种格，空串跳过），并**关闭**窗口；</item>
///   <item><b>`OkBtn` 的截尾</b>：尾部空表头列被截掉（`温度,压力,` ⇒ `温度,压力`）；</item>
///   <item><b>`CancelBtn`</b>：模型**一字不变**，并关闭窗口。</item>
/// </list></para>
///
/// <para><b>不抓什么（别读成全覆盖）：</b>① 不判"用户真用鼠标点"（不做像素命中测试，同其它层）；
/// ② 不判 `PropertyPanelView.EditCellsBtn` 那条**打开链**（那属第五层的 Button 面；`PropertyPanelView`
/// 在第五层已判定"不扩"，理由见 §163.4 纪律②）；③ 不判真实布局/尺寸/滚动；
/// ④ `VariableCombo` 的选中态只当**夹具前置**用（本类不判它的选择行为）。</para>
/// </summary>
[Collection("Language-global")]
public class TableCellEditorDialogUiTests
{
    private readonly ITestOutputHelper _out;

    public TableCellEditorDialogUiTests(ITestOutputHelper o) => _out = o;

    /// <summary>夹具：一个 2×3 的表控件（第 0 行是表头）＋ 一个变量。</summary>
    private static (HmiControl Control, List<Variable> Variables) Fixture() => (
        new HmiControl
        {
            Id = "tbl1",
            Name = "表格1",
            Type = HmiControlType.Table,
            RowCount = 2,
            ColumnCount = 3,
            HeaderTexts = "温度,压力",
            Cells = new List<TableCellData>
            {
                new() { Row = 1, Col = 0, Text = "静态文本" },
            },
        },
        new List<Variable> { new() { Id = "v1", Name = "液位" } });

    /// <summary>走生产构造路径造窗并显示（本窗没有那条 headless 会挂死的布局路径）。</summary>
    private static TableCellEditorDialog Open(out HmiControl control, out List<Variable> variables)
    {
        (control, variables) = Fixture();
        var dialog = new TableCellEditorDialog(control, variables);
        dialog.Show();
        UiTestHost.Pump(5);
        return dialog;
    }

    /// <summary>
    /// 矩阵里的文本框，**按 (行, 列) 排好序** —— 不按 <c>Children</c> 的下标猜顺序，
    /// 而是读 <see cref="Grid"/> 真正挂上去的附加属性（下标一变也不会假绿）。
    /// </summary>
    private static IReadOnlyList<TextBox> Cells(TableCellEditorDialog dialog)
    {
        var host = dialog.FindControl<StackPanel>("MatrixHost")
                   ?? throw new InvalidOperationException("找不到 MatrixHost —— 对话框的 XAML 结构变了");
        var grid = host.Children.OfType<Grid>().Single();
        return grid.Children.OfType<TextBox>()
            .OrderBy(Grid.GetRow).ThenBy(Grid.GetColumn)
            .ToList();
    }

    /// <summary>单元格下标（矩阵按行优先铺）：<c>(row, col)</c> ⇒ 扁平下标。</summary>
    private static int At(int columns, int row, int col) => row * columns + col;

    private static void Click(Control control) =>
        control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public void Face_IsExactlyTheThreeDeclaredButtons()
    {
        UiTestHost.Run(() =>
        {
            var dialog = Open(out _, out _);
            try
            {
                var named = dialog.GetLogicalDescendants().OfType<Button>()
                    .Where(b => b.GetType() == typeof(Button) && b.Name is not null)
                    .Select(b => b.Name!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray();

                Assert.Equal(new[] { "CancelBtn", "InsertVarBtn", "OkBtn" }, named);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void InsertVariable_WritesOnlyTheFocusedCell_AndIsANoOpWithoutFocus()
    {
        UiTestHost.Run(() =>
        {
            var dialog = Open(out var control, out var variables);
            try
            {
                var cells = Cells(dialog);
                Assert.Equal(6, cells.Count);                    // 2 行 × 3 列的自检

                var combo = dialog.FindControl<ComboBox>("VariableCombo")
                            ?? throw new InvalidOperationException("找不到 VariableCombo");
                Assert.Equal(variables[0].Id,
                    ((ComboBoxItem)combo.SelectedItem!).Tag as string);   // 夹具前置：选中了 v1

                var insert = dialog.FindControl<Button>("InsertVarBtn")!;
                var target = cells[At(3, 1, 1)];
                Assert.Equal("", target.Text ?? "");

                // ── ① 没有焦点时：提前 return（本类如实记这一档，不让它假绿）──
                Click(insert);
                UiTestHost.Pump(2);
                Assert.Equal("", target.Text ?? "");
                var textWithoutFocus = target.Text ?? "";
                Assert.Equal("温度,压力", control.HeaderTexts);
                Assert.Single(control.Cells!);

                // ── ② 聚焦之后：只有这一个格被写 ──
                Assert.True(target.Focus(), "聚焦失败 —— 后面那条就变成恒真了");
                UiTestHost.Pump(2);
                Assert.True(target.IsFocused, "焦点没落到目标格上（判据的前置就断了）");

                Click(insert);
                UiTestHost.Pump(2);

                Assert.Equal("{v1}", cells[At(3, 1, 1)].Text);       // ← 焦点那一格
                Assert.Equal("压力", cells[At(3, 0, 1)].Text);       // 表头那一格保持夹具值（没被动）
                Assert.Equal("", cells[At(3, 1, 2)].Text ?? "");     // 第二列也没被动
                // 模型面：插入变量**不落模型**（要等 Ok）
                Assert.Equal("温度,压力", control.HeaderTexts);
                Assert.Single(control.Cells!);
                Assert.Equal("静态文本", control.Cells![0].Text);

                JudgeReadings.Write(_out, "表格单元格编辑器 · InsertVarBtn（#39 残余面 · 阶段 250）", new[]
                {
                    "面：具名 Button 3 个 —— CancelBtn, InsertVarBtn, OkBtn（双向差集，已断言）",
                    $"① 无焦点时点它：目标格 `{textWithoutFocus}`（应为空 ⇒ 处理器首行 `if (_focused == null) return;`）",
                    $"② 聚焦后点它：目标格 `{cells[At(3, 1, 1)].Text}`，另两格 `{cells[At(3, 0, 1)].Text}` / "
                        + $"`{cells[At(3, 1, 2)].Text ?? "(空)"}`",
                    $"模型面：HeaderTexts=`{control.HeaderTexts}`；Cells {control.Cells!.Count} 格（插入变量不落模型，等 Ok）",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Ok_WritesHeaderTextsAndCells_ThenCloses()
    {
        UiTestHost.Run(() =>
        {
            var dialog = Open(out var control, out _);
            try
            {
                var cells = Cells(dialog);

                // 布景：第 0 行补一个表头「备注」；第 1 行第 1 列填 {v1}；第 2 列填空白（应被跳过）
                cells[At(3, 0, 2)].Text = "备注";
                cells[At(3, 1, 1)].Text = "{v1}";
                cells[At(3, 1, 2)].Text = "   ";

                Click(dialog.FindControl<Button>("OkBtn")!);
                UiTestHost.Pump(5);

                // ① 表头：第 0 行整行写回（`温度,压力,备注`）
                Assert.Equal("温度,压力,备注", control.HeaderTexts);

                // ② 单元格：静态格保留、绑定格转成 VariableId、空白格被跳过
                var written = control.Cells!;
                Assert.Equal(2, written.Count);
                Assert.Equal((1, 0), (written[0].Row, written[0].Col));
                Assert.Equal("静态文本", written[0].Text);
                Assert.False(written[0].IsBound);
                Assert.Equal((1, 1), (written[1].Row, written[1].Col));
                Assert.Equal("v1", written[1].VariableId);
                Assert.True(written[1].IsBound);
                Assert.Null(written[1].Text);

                // ③ 关闭（`Close()` 只有处理器会调）
                Assert.False(dialog.IsVisible, "OkBtn 点完窗口应已关闭");

                JudgeReadings.Write(_out, "表格单元格编辑器 · OkBtn（#39 残余面 · 阶段 250）", new[]
                {
                    "面：具名 Button 3 个 —— CancelBtn, InsertVarBtn, OkBtn（双向差集，已断言）",
                    $"写回表头：`{control.HeaderTexts}`（第 0 行整行 ⇒ 截尾空列）",
                    "写回单元格：" + string.Join(" / ", written.Select(w =>
                        $"({w.Row},{w.Col})={(w.IsBound ? "{" + w.VariableId + "}" : w.Text)}")),
                    "关闭：OkBtn 点完 IsVisible=false（`Close()` 只有处理器会调）",
                });
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Ok_TruncatesTrailingEmptyHeaderColumns()
    {
        UiTestHost.Run(() =>
        {
            var dialog = Open(out var control, out _);
            try
            {
                var cells = Cells(dialog);

                // 布景：把第 2 个表头清空 ⇒ 只剩「温度」
                cells[At(3, 0, 1)].Text = "";
                cells[At(3, 0, 2)].Text = "   ";      // 尾部空白也算空（Trim 之后）

                Click(dialog.FindControl<Button>("OkBtn")!);
                UiTestHost.Pump(5);

                Assert.Equal("温度", control.HeaderTexts);   // 「温度,压力」⇒「温度」（截尾）
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void Cancel_LeavesTheModelUntouched_ThenCloses()
    {
        UiTestHost.Run(() =>
        {
            var dialog = Open(out var control, out _);
            try
            {
                var cells = Cells(dialog);

                // 布景：改一处表头 ＋ 一处单元格 —— 若 Cancel 误走 Ok 的写回，下面两条就会红
                cells[At(3, 0, 2)].Text = "备注";
                cells[At(3, 1, 1)].Text = "{v1}";

                Click(dialog.FindControl<Button>("CancelBtn")!);
                UiTestHost.Pump(5);

                Assert.Equal("温度,压力", control.HeaderTexts);        // 与夹具逐字相同
                Assert.Single(control.Cells!);                        // 仍只有夹具那一格
                Assert.Equal("静态文本", control.Cells![0].Text);
                Assert.False(dialog.IsVisible, "CancelBtn 点完窗口应已关闭");
            }
            finally { dialog.Close(); }
        });
    }
}
