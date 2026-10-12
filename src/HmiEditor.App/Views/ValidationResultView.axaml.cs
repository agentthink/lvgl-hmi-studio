using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using HmiEditor.Core.Services;
using HmiEditor.App.Services;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App;

namespace HmiEditor.App.Views;

/// <summary>
/// I11: 生成代码验证结果对话框 —— 展示 GeneratedCodeValidator 的问题清单。
/// 按级别着色(Error 红 / Warning 黄 / Info 蓝),支持复制全部。
/// </summary>
public partial class ValidationResultView : ChromeWindow
{
    private readonly IReadOnlyList<GeneratedCodeFinding> _findings;

    public ValidationResultView()
    {
        _findings = Array.Empty<GeneratedCodeFinding>();
        InitializeComponent();
    }

    public ValidationResultView(IReadOnlyList<GeneratedCodeFinding> findings) : this()
    {
        _findings = findings ?? throw new ArgumentNullException(nameof(findings));
        Build();

        BtnCopyAll.Click += (_, _) => CopyAll();
        BtnClose.Click += (_, _) => Close();
    }

    private void Build()
    {
        var errors = _findings.Count(f => f.Severity == FindingSeverity.Error);
        var warnings = _findings.Count(f => f.Severity == FindingSeverity.Warning);

        SummaryText.Text = _findings.Count == 0
            ? AppLocalization.Service[Lang.FindingPassed]
            : AppLocalization.Service.Format("msg.findings", _findings.Count, errors, warnings);

        foreach (var finding in _findings)
            FindingsList.Items.Add(CreateItem(finding));
    }

    private static Control CreateItem(GeneratedCodeFinding finding)
    {
        var badge = new Border
        {
            Background = new SolidColorBrush(SeverityColor(finding.Severity)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = FindingDisplayText.SeverityText(finding.Severity),
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 11
            }
        };

        var category = new TextBlock
        {
            Text = FindingDisplayText.CategoryText(finding.Category),
            Foreground = HmiEditor.App.Theme.TextSecondary,
            FontSize = 11,
            Width = 76
        };

        var message = new TextBlock
        {
            Text = ExternalText.Describe(finding),
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };

        var location = new TextBlock
        {
            Text = ExternalText.DescribeLocation(finding),
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };

        var rightPanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { message, location }
        };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        row.Children.Add(badge);
        Grid.SetColumn(badge, 0);
        row.Children.Add(category);
        Grid.SetColumn(category, 1);
        row.Children.Add(rightPanel);
        Grid.SetColumn(rightPanel, 2);

        var border = new Border
        {
            Background = HmiEditor.App.Theme.BgInput,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6),
            Margin = new Thickness(0, 2),
            Child = row
        };
        return border;
    }

    /// <summary>
    /// 严重级别的**颜色**。文案不在这里 —— 它与「复制全部」共用
    /// <see cref="FindingDisplayText.SeverityText"/>（那是本项缺陷的另一半）。
    ///
    /// <para>颜色**刻意不给 <c>_</c> 支**：新增级别必须显式配色，不许静默落进 Info 的蓝。
    /// 这里只抑制枚举 switch 的固定噪声 <c>CS8524</c>（保留 <c>CS8509</c>）—— 两档码的
    /// 实测对照与完整理由见 <c>FindingDisplayText</c> 的文件头注释。</para>
    /// </summary>
#pragma warning disable CS8524
    private static Color SeverityColor(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Error => UiPalette.SeverityEmergency,
        FindingSeverity.Warning => ((SolidColorBrush)HmiEditor.App.Theme.Warning).Color,
        FindingSeverity.Info => UiPalette.SeverityInfo,
    };
#pragma warning restore CS8524

    private void CopyAll()
    {
        var text = string.Join(Environment.NewLine, _findings.Select(FindingDisplayText.CopyLine));
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
        {
            _ = topLevel.Clipboard.SetTextAsync(text);
            HintText.Text = AppLocalization.Service[Lang.FindingCopied];
        }
    }
}
