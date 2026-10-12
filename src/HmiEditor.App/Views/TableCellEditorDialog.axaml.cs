using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Views;

/// <summary>
/// 表格单元格编辑器 —— 以矩阵方式编辑表格每个单元格：
/// 直接填文本 = 静态内容；填 <c>{变量名}</c> = 绑定变量（运行时随变量值实时更新）。
/// </summary>
public partial class TableCellEditorDialog : ChromeWindow
{
    private readonly HmiControl _control;
    private TextBox[,]? _cells;
    private int _rows;
    private int _cols;
    private TextBox? _focused;

    /// <summary>仅用于 XAML 设计器。</summary>
    public TableCellEditorDialog()
    {
        InitializeComponent();
        _control = null!;
    }

    public TableCellEditorDialog(HmiControl control, IReadOnlyList<Variable> variables)
    {
        InitializeComponent();
        _control = control;
        BuildVariables(variables);
        BuildMatrix();
    }

    private void BuildVariables(IReadOnlyList<Variable> variables)
    {
        foreach (var v in variables)
            VariableCombo.Items.Add(new ComboBoxItem { Content = $"{v.Name} ({v.Id})", Tag = v.Id });
        if (VariableCombo.ItemCount > 0) VariableCombo.SelectedIndex = 0;
    }

    /// <summary>根据控件行列数动态构建文本框矩阵，并回填现有内容。</summary>
    private void BuildMatrix()
    {
        _rows = Math.Max(1, _control.RowCount);
        _cols = Math.Max(1, _control.ColumnCount);
        _cells = new TextBox[_rows, _cols];

        var grid = new Grid { ColumnSpacing = 4, RowSpacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
        for (var c = 0; c < _cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var r = 0; r < _rows; r++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var r = 0; r < _rows; r++)
        {
            for (var c = 0; c < _cols; c++)
            {
                var tb = new TextBox
                {
                    Text = GetCellText(r, c),
                    FontSize = 12,
                    MinHeight = 28,
                    Background = HmiEditor.App.Theme.BgInput,
                    Foreground = HmiEditor.App.Theme.TextPrimary,
                    BorderBrush = HmiEditor.App.Theme.InputBorder,
                    BorderThickness = new Avalonia.Thickness(1)
                };
                var row = r; var col = c;
                tb.GotFocus += (_, _) => _focused = tb;
                Grid.SetRow(tb, r);
                Grid.SetColumn(tb, c);
                _cells[r, c] = tb;
                grid.Children.Add(tb);
            }
        }

        MatrixHost.Children.Add(grid);
    }

    private string GetCellText(int row, int col)
    {
        if (row == 0)
        {
            var headers = (_control.HeaderTexts ?? "").Split(',', System.StringSplitOptions.TrimEntries);
            return col < headers.Length ? headers[col] : "";
        }

        var cell = _control.Cells?.FirstOrDefault(x => x.Row == row && x.Col == col);
        if (cell == null) return "";
        return cell.IsBound ? $"{{{cell.VariableId}}}" : (cell.Text ?? "");
    }

    private void OnInsertVariableClicked(object? sender, RoutedEventArgs e)
    {
        if (_focused == null) return;
        if (VariableCombo.SelectedItem is not ComboBoxItem { Tag: string id }) return;
        _focused.Text = $"{{{id}}}";
    }

    private void OnOkClicked(object? sender, RoutedEventArgs e)
    {
        if (_cells == null) return;

        // 第 0 行 → HeaderTexts（保留列顺序，截断尾部空列）
        var headerArr = new string[_cols];
        for (var c = 0; c < _cols; c++)
            headerArr[c] = (_cells[0, c].Text ?? "").Trim();
        var last = _cols - 1;
        while (last >= 0 && string.IsNullOrEmpty(headerArr[last])) last--;
        _control.HeaderTexts = last >= 0 ? string.Join(",", headerArr.Take(last + 1)) : null;

        // 第 1 行起 → Cells
        var cells = new List<TableCellData>();
        for (var r = 1; r < _rows; r++)
        {
            for (var c = 0; c < _cols; c++)
            {
                var text = (_cells[r, c].Text ?? "").Trim();
                if (string.IsNullOrEmpty(text)) continue;
                cells.Add(ParseCell(r, c, text));
            }
        }
        _control.Cells = cells.Count > 0 ? cells : null;

        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private static TableCellData ParseCell(int row, int col, string text)
    {
        var m = Regex.Match(text, @"^\s*\{(.+)\}\s*$");
        if (m.Success)
            return new TableCellData { Row = row, Col = col, VariableId = m.Groups[1].Value.Trim() };
        return new TableCellData { Row = row, Col = col, Text = text };
    }
}
