using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using HmiEditor.App.Controls;
using HmiEditor.Core.Reporting;

namespace HmiEditor.App.Views;

/// <summary>
/// **报表设计器对话框**（阶段 183）：把某个数据源的列清单编辑成一份
/// <see cref="ReportTemplate"/> —— 勾选（导出这一列）· 直接改列名 · ↑↓ 调顺序 · 恢复默认。
///
/// <para><b>输入是"列清单"而不是"一张表"</b>：设计器只需要列（名称与顺序），不需要行 ——
/// 历史数据报表的行可能上千，为了改个列名去铺一张表是白花钱。列清单由
/// <c>XxxReport.Columns(...)</c> 与导出处**共用同一份构造**（判据 <c>ReportColumnUniverseTests</c> 对拍）。</para>
///
/// <para><b>为什么初始行用 <see cref="ReportTemplate.Spread"/> 铺开</b>：那是"设计器视图"——
/// **被隐藏的列留在原位**，否则用户一旦隐藏过一列，就再也勾不回来（导出视图里它已经不存在了）。
/// 本窗口的行顺序 = 导出顺序 ⇒ <see cref="Selected"/> 直接按行序收拢成模板。</para>
///
/// <para><b>两条界面口径</b>：① 全都不勾时**确定键不可点**（模板把每列都隐藏了，导出会抛 ——
/// 与范围对话框"两端都空不可点"同一形状）；② 一键未动地保存 ⇒ 交回**空模板**
/// （顺序没变、没改名、没隐藏 ＝ 没有任何信息的模板；落盘等于留个空壳，见
/// <c>ReportTemplateSettings.Set</c> 的"空模板等同删除"）。</para>
///
/// <para>本窗口有无参构造（XAML 设计器 ＋ <c>WindowViewSmokeUiTests</c> 的自动收面）。</para>
/// </summary>
public partial class ReportDesignerDialog : ChromeWindow
{
    /// <summary>一行 ＝ 一列（控件与"原列名"一起留着：改名要不要写进模板靠它比对）。</summary>
    private sealed record Row(string Key, string OriginalName, CheckBox Show, TextBox Name, Control Root);

    /// <summary>
    /// 本实例的行与原始列清单。⚠ **实例字段**（不是 static）：① 两个对话框实例不该共享状态；
    /// ② 生产构造与设计器构造因此**可观测地不同**（0 → N 条）—— `WindowProductionPathUiTests`
    /// 正是按"自有字段形状签名"判"生产构造到底装了没装"。
    /// </summary>
    private readonly List<Row> _rows = new();

    private readonly List<ReportColumn> _columns = new();

    /// <summary>仅用于 XAML 设计器与冒烟面。</summary>
    public ReportDesignerDialog() => InitializeComponent();

    public ReportDesignerDialog(IReadOnlyList<ReportColumn> columns, ReportTemplate? template) : this()
    {
        ArgumentNullException.ThrowIfNull(columns);

        _columns.AddRange(columns);
        Build(ReportTemplate.Spread(columns, template));

        BtnReset.Click += (_, _) => Build(ReportTemplate.Spread(_columns, null));   // 恢复默认 ＝ 空模板视图
        BtnCancel.Click += (_, _) => Close(null);
        BtnOk.Click += (_, _) => Close(Selected());
    }

    /// <summary>按给定的全列视图重建行（顺序即传入顺序）。</summary>
    private void Build(IReadOnlyList<ReportTemplateEntry> entries)
    {
        _rows.Clear();
        ColumnList.Children.Clear();

        foreach (var entry in entries)
            _rows.Add(CreateRow(entry));

        foreach (var row in _rows)
            ColumnList.Children.Add(row.Root);

        SyncOkEnabled();
    }

    private Row CreateRow(ReportTemplateEntry entry)
    {
        var show = new CheckBox
        {
            IsChecked = !entry.Hidden,
            VerticalAlignment = VerticalAlignment.Center,
        };
        show.IsCheckedChanged += (_, _) => SyncOkEnabled();

        var name = new TextBox
        {
            Text = entry.DisplayName ?? "",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            Padding = new Avalonia.Thickness(4, 2),
        };

        var key = new TextBlock
        {
            Text = entry.Key,
            FontSize = 11,
            Opacity = 0.55,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        };

        var up = new Button
        {
            Content = "↑",
            FontSize = 12,
            Padding = new Avalonia.Thickness(8, 2),
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var down = new Button
        {
            Content = "↓",
            FontSize = 12,
            Padding = new Avalonia.Thickness(8, 2),
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto") };
        Grid.SetColumn(show, 0);
        Grid.SetColumn(name, 1);
        Grid.SetColumn(key, 2);
        Grid.SetColumn(up, 3);
        Grid.SetColumn(down, 4);
        root.Children.Add(show);
        root.Children.Add(name);
        root.Children.Add(key);
        root.Children.Add(up);
        root.Children.Add(down);

        var row = new Row(entry.Key, entry.DisplayName ?? "", show, name, root);
        up.Click += (_, _) => Move(row, -1);
        down.Click += (_, _) => Move(row, +1);
        return row;
    }

    /// <summary>把一行上/下挪一格（到边界就是不动）。</summary>
    private void Move(Row row, int delta)
    {
        var from = _rows.IndexOf(row);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= _rows.Count) return;

        _rows.RemoveAt(from);
        _rows.Insert(to, row);

        ColumnList.Children.Clear();
        foreach (var r in _rows)
            ColumnList.Children.Add(r.Root);
    }

    /// <summary>
    /// 当前编辑结果：按**行序**收拢成模板；列名与"原列名"一字不差（或空白）⇒ 不写覆盖；
    /// 顺序没动、没改名、没隐藏 ⇒ <see cref="ReportTemplate.Empty"/>（＝"没设过"）。
    /// </summary>
    public ReportTemplate Selected()
    {
        var entries = new List<ReportTemplateEntry>(_rows.Count);
        var renamed = false;

        foreach (var row in _rows)
        {
            var hidden = row.Show.IsChecked != true;
            var text = row.Name.Text?.Trim() ?? "";
            var isRename = !hidden && text.Length > 0 && !string.Equals(text, row.OriginalName, StringComparison.Ordinal);
            renamed |= isRename;
            entries.Add(new ReportTemplateEntry(row.Key, isRename ? text : null, hidden));
        }

        var natural = _columns.Select(c => c.Key).ToList();
        var reordered = !entries.Select(e => e.Key).SequenceEqual(natural, StringComparer.Ordinal);
        var anyHidden = entries.Any(e => e.Hidden);

        return renamed || reordered || anyHidden ? new ReportTemplate(entries) : ReportTemplate.Empty;
    }

    /// <summary>一列都不勾 ⇒ 确定不可点（那种模板会让导出抛 —— 界面上不该产生这种输入）。</summary>
    private void SyncOkEnabled() =>
        BtnOk.IsEnabled = _rows.Any(r => r.Show.IsChecked == true);
}
