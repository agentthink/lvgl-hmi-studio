using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Controls;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;

namespace HmiEditor.App.Views;

/// <summary>
/// **报表列选择对话框**（阶段 177）：导出前勾选要保留的列（至少一列）。
///
/// <para>口径：结果只回**列键**（显示名与翻译由调用方的 <c>ReportTable</c> 决定）；取消返回 <c>null</c>。
/// 勾选顺序**不参与**结果顺序 —— 顺序沿用原表（用户在对话框里只是"取舍"，不是"重排"）。
/// 一列都不勾时"确定"不可点（Core 的 <see cref="ReportTable.WithColumns"/> 对这种输入是抛异常）。</para>
///
/// <para>⚠ 本窗口有无参构造（XAML 设计器 ＋ <c>WindowViewSmokeUiTests</c> 的自动收面都要它）。</para>
/// </summary>
public partial class ReportColumnDialog : ChromeWindow
{
    private readonly List<(string Key, CheckBox Box)> _boxes = new();

    /// <summary>仅用于 XAML 设计器与冒烟面。</summary>
    public ReportColumnDialog() => InitializeComponent();

    public ReportColumnDialog(IReadOnlyList<ReportColumn> columns) : this()
    {
        foreach (var column in columns)
        {
            var box = new CheckBox
            {
                Content = column.DisplayName,
                IsChecked = true,                      // 默认全选：最常见的诉求是"少勾几列"
                Foreground = Avalonia.Media.Brushes.White,
                FontSize = 12,
            };
            box.IsCheckedChanged += (_, _) => UpdateOkEnabled();
            _boxes.Add((column.Key, box));
            ColumnList.Children.Add(box);
        }

        BtnSelectAll.Click += (_, _) => SetAll(true);
        BtnSelectNone.Click += (_, _) => SetAll(false);
        BtnCancel.Click += (_, _) => Close(null);
        BtnOk.Click += (_, _) => Close(SelectedKeys());
        UpdateOkEnabled();
    }

    /// <summary>勾选结果（列键，顺序沿用原表）。</summary>
    public IReadOnlyList<string> SelectedKeys() =>
        _boxes.Where(b => b.Box.IsChecked == true).Select(b => b.Key).ToList();

    private void SetAll(bool value)
    {
        foreach (var (_, box) in _boxes) box.IsChecked = value;
    }

    private void UpdateOkEnabled() =>
        BtnOk.IsEnabled = _boxes.Any(b => b.Box.IsChecked == true);   // 至少一列
}
