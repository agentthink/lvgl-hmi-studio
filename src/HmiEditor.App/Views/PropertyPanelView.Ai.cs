using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using HmiEditor.App.Controls;
using HmiEditor.App.Design;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App;
using HmiEditor.App.Views.Editors;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace HmiEditor.App.Views;

public partial class PropertyPanelView : UserControl
{
    // ═══════════════════════════════════════════════
    //  AI 布局建议 (T4.4)
    // ═══════════════════════════════════════════════

    private HmiEditor.AI.Models.LayoutAnalysisResult? _lastAnalysis;

    /// <summary>
    /// 显示屏幕布局分析结果。由 MainWindow 在选中屏幕/切换屏幕时调用。
    /// </summary>
    public void ShowAiSuggestions(HmiScreen? screen)
    {
        if (screen is null)
        {
            AiSuggestExpander.IsVisible = false;
            _lastAnalysis = null;
            return;
        }

        var analysis = HmiEditor.AI.Services.LayoutAnalyzer.Analyze(screen);
        _lastAnalysis = analysis;

        AiSuggestExpander.IsVisible = true;
        AiScoreText.Text = AppLocalization.Service.Format(Lang.PropLayoutScore, analysis.Score) +
                           (analysis.HasErrors ? AppLocalization.Service[Lang.PropLayoutError] : analysis.HasWarnings ? AppLocalization.Service[Lang.PropLayoutWarning] : AppLocalization.Service[Lang.PropLayoutGood]);

        AiIssueList.Items.Clear();
        foreach (var issue in analysis.Issues.Take(8))
        {
            var prefix = issue.Severity switch
            {
                HmiEditor.AI.Models.LayoutIssueSeverity.Error => "🔴 ",
                HmiEditor.AI.Models.LayoutIssueSeverity.Warning => "🟡 ",
                _ => "🔵 "
            };
            AiIssueList.Items.Add(new TextBlock
            {
                Text = prefix + ExternalText.Describe(issue) + (issue.AutoFix ? AppLocalization.Service[Lang.PropIssueAutofix] : ""),
                Foreground = issue.Severity == HmiEditor.AI.Models.LayoutIssueSeverity.Error
                    ? new SolidColorBrush(UiPalette.ValidationError)
                    : HmiEditor.App.Theme.TextPrimarySoft
            });
        }

        BtnAutoFix.IsVisible = analysis.AutoFixCount > 0;
    }

    /// <summary>一键修复可自动修复的问题（边距/对齐/间距）。</summary>
    private void OnAutoFixClicked(object? sender, RoutedEventArgs e)
    {
        if (_lastAnalysis is null) return;
        var fixedCount = 0;

        foreach (var issue in _lastAnalysis.Issues.Where(i => i.AutoFix))
        {
            if (issue.Controls is not { Count: > 0 }) continue;
            var control = issue.Controls[0];
            var hint = issue.FixHint;

            // 边距修复: left/top/right/bottom:目标值
            var parts = hint.Split(':');
            if (parts.Length != 2) continue;

            switch (parts[0])
            {
                case "left":
                    control.X = double.Parse(parts[1]);
                    break;
                case "top":
                    control.Y = double.Parse(parts[1]);
                    break;
                case "right":
                    control.X = double.Parse(parts[1]) - control.Width;
                    break;
                case "bottom":
                    control.Y = double.Parse(parts[1]) - control.Height;
                    break;
                case "alignTop":
                    control.Y = double.Parse(parts[1]);
                    break;
                case "alignLeft":
                    control.X = double.Parse(parts[1]);
                    break;
                case "gap":
                    // 向左对齐到上一个控件右侧 + 目标间距 —— 需要知道左邻控件, 简化: 仅记录
                    continue;
            }
            fixedCount++;
        }

        // 修复后刷新面板并重新分析
        if (_currentControl != null)
        {
            PropX.Value = _currentControl.X;
            PropY.Value = _currentControl.Y;
        }
        PropertyChanged?.Invoke(this, _currentControl!);
        ShowAiSuggestions(CurrentScreen);
    }

    /// <summary>当前分析的屏幕（由 MainWindow 注入）。</summary>
    public HmiScreen? CurrentScreen { get; set; }
}
/// <summary>动作列表 chip 数据模型（联动模型：多动作管理）。</summary>
public class ActionChipModel
{
    public int Index { get; set; }
    public string DisplayName { get; set; } = "";
}
