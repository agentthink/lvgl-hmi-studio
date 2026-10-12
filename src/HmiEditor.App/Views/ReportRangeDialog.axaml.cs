using System;
using System.Collections.Generic;
using Avalonia.Controls;
using HmiEditor.App.Controls;
using HmiEditor.App.Services;
using HmiEditor.Core.Reporting;

namespace HmiEditor.App.Views;

/// <summary>
/// **报表时间范围对话框**（阶段 178，阶段 180 加自定义）：五个快捷档（全部 / 最近 1 小时 /
/// 24 小时 / 7 天 / 30 天）＋ 自定义起止日期。
///
/// <para>口径：结果回**用户的选择**（<see cref="ReportRangeSelection"/> ＝ 档位 ＋（自定义时）
/// 两个带偏移的日期），时间范围由调用方用**自己的 now** 算
/// （<see cref="ReportRangeOptions.Compute(ReportRangeSelection, DateTimeOffset)"/>）——
/// 这样"当前时刻"只有一个来源，判据也能用固定时刻核对。取消返回 <c>null</c>。
/// 默认选中"全部"（不改行为）。</para>
///
/// <para><b>为什么本窗口不读时钟</b>：自定义档的两个日期全部来自用户（空 = 该侧不设限），
/// 本窗口**不预填**"最近 N 天"—— 预填就要在这里读一次 <c>Now</c>，于是"当前时刻"有了第二个
/// 来源。代价是"自定义 + 两端都空"成为一种可达输入，处置是**确定按钮在零日期时不可点**
/// （与列选择对话框"至少勾一列"同一形状）＋ Core 侧照旧抛（双保险）。</para>
///
/// <para>本窗口有无参构造（XAML 设计器 ＋ <c>WindowViewSmokeUiTests</c> 的自动收面）。</para>
/// </summary>
public partial class ReportRangeDialog : ChromeWindow
{
    /// <summary>
    /// 本实例的档位按钮。⚠ **实例字段**（不是 static）：① 两个对话框实例不该共享状态；
    /// ② 生产构造与设计器构造因此**可观测地不同**（0 → 6 条）—— `WindowProductionPathUiTests`
    /// 正是按"自有字段形状签名"判"生产构造到底装了没装"（我把 static 版本交上去就被它红了一次）。
    /// </summary>
    private readonly List<(ReportRangeChoice Choice, RadioButton Button)> _choices = new();

    /// <summary>仅用于 XAML 设计器与冒烟面。</summary>
    public ReportRangeDialog() => InitializeComponent();

    public ReportRangeDialog(ReportRangeSelection initial) : this()
    {
        foreach (var choice in ReportRangeOptions.All)
        {
            var button = new RadioButton
            {
                GroupName = "range",
                Content = ReportRangeText.Label(choice),
                IsChecked = choice == initial.Choice,
                Foreground = Avalonia.Media.Brushes.White,
                FontSize = 12,
            };
            button.IsCheckedChanged += (_, _) => SyncCustomPanel();
            _choices.Add((choice, button));
            ChoiceList.Children.Add(button);
        }

        LblFrom.Text = ReportRangeText.FromLabel;
        LblTo.Text = ReportRangeText.ToLabel;

        // 初始值：自定义档带日期就填上（"全部"等档位给的是 null ⇒ 两个选择器都空）
        FromPicker.SelectedDate = initial.CustomFrom;
        ToPicker.SelectedDate = initial.CustomTo;
        FromPicker.SelectedDateChanged += (_, _) => SyncOkEnabled();
        ToPicker.SelectedDateChanged += (_, _) => SyncOkEnabled();

        BtnCancel.Click += (_, _) => Close(null);
        BtnOk.Click += (_, _) => Close(Selected());
        SyncCustomPanel();
    }

    /// <summary>当前选中的档位（没有选中时回落「全部」）。</summary>
    public ReportRangeChoice SelectedChoice()
    {
        foreach (var (choice, button) in _choices)
            if (button.IsChecked == true) return choice;

        return ReportRangeChoice.All;
    }

    /// <summary>
    /// 当前选择：档位 ＋（自定义档）两个日期。非自定义档的日期一律 **null** ——
    /// 不把界面上残留的日期带出去（切回快捷档再导出时，那两天不该还生效）。
    /// </summary>
    public ReportRangeSelection Selected()
    {
        var choice = SelectedChoice();
        return choice == ReportRangeChoice.Custom
            ? new ReportRangeSelection(choice, FromPicker.SelectedDate, ToPicker.SelectedDate)
            : new ReportRangeSelection(choice);
    }

    /// <summary>自定义面板的可见性跟着档位走（只有自定义档才显示两个日期选择器）。</summary>
    private void SyncCustomPanel()
    {
        CustomPanel.IsVisible = SelectedChoice() == ReportRangeChoice.Custom;
        SyncOkEnabled();
    }

    /// <summary>
    /// 自定义档且**两端都空**时确定不可点 —— 这种输入到了 Core 会抛
    /// （<see cref="ReportRangeOptions.Compute(ReportRangeSelection, DateTimeOffset)"/>），
    /// 与其让用户点一个必然失败的按钮，不如在这里就挡住。
    /// </summary>
    private void SyncOkEnabled()
    {
        var custom = SelectedChoice() == ReportRangeChoice.Custom;
        BtnOk.IsEnabled = !custom || FromPicker.SelectedDate is not null || ToPicker.SelectedDate is not null;
    }
}
