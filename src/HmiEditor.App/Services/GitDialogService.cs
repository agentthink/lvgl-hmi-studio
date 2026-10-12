using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HmiEditor.App.Resources;
using HmiEditor.Core.Services;
using System;

namespace HmiEditor.App.Services;

/// <summary>
/// Git 对话框 —— 提交信息输入与历史查看。
///
/// 从 MainWindow 抽出（原 GitCommitDialog + ShowGitHistory 约 160 行 UI 构造代码）：
/// 这类纯对话框只需 owner / 工程目录 / 状态栏写入 / i18n，
/// 不需要持有主窗口状态，放在服务里可读性更好，也避免主窗口继续膨胀。
/// </summary>
internal static class GitDialogService
{
    private static LocalizationService Loc => AppLocalization.Service;

    /// <summary>提交对话框；提交成功后回调 <paramref name="onCommitted"/>（刷新 Git 状态）。</summary>
    public static void ShowCommitDialog(Window owner, string projectDirectory, Action<string> setStatus, Action onCommitted)
    {
        var info = GitWorkflowService.GetInfo(projectDirectory);
        var input = new TextBox
        {
            PlaceholderText = Loc["dialog.git.commit.placeholder"],
            AcceptsReturn = false,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(13, 21, 38)),
            Foreground = Brushes.White,
            BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(51, 65, 85)),
            FontSize = 13,
            Padding = new Thickness(8, 6),
            MinWidth = 380
        };

        // 两个按钮先建成局部变量，接线时**直接用实例** —— 不再构造完再从 Children 里按下标回找。
        // 「按下标回找」有一条静默失效面：往外层 StackPanel 里插一个 StackPanel，取"第一个
        // StackPanel 行"就会拿到错的那一行，取消/提交会**悄悄互换**；插一个别的控件则硬转直接抛。
        // 同族缺陷与判据见 scripts/check-index-based-control-lookup.py（阶段 76）。
        var cancelBtn = new Button
        {
            Content = Loc["gen.cancel"],
            Background = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 6)
        };
        var commitBtn = new Button
        {
            Content = Loc["dialog.git.commit.button"],
            Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 6)
        };

        var dialog = new Window
        {
            Title = Loc["dialog.git.commit.title"],
            Width = 460,
            SizeToContent = Avalonia.Controls.SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(19, 28, 49)),
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = Loc.Format("dialog.git.commit.uncommitted", info.UncommittedChanges),
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(148, 163, 184)),
                        FontSize = 12
                    },
                    input,
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelBtn, commitBtn }
                    }
                }
            }
        };

        // 按钮事件。原先先 `dialog.Content is StackPanel` 再进 Children 导航：那层一旦类型失配，
        // **整段接线被静默跳过**（两个按钮都点不动，不抛不报）。
        cancelBtn.Click += (_, _) => dialog.Close();
        commitBtn.Click += (_, _) =>
        {
            var message = input.Text?.Trim();
            if (string.IsNullOrEmpty(message))
            {
                setStatus(Loc["status.git.commit.empty"]);
                return;
            }

            var (ok, error) = GitWorkflowService.Commit(projectDirectory!, message);
            if (ok)
            {
                onCommitted();
                setStatus(Loc.Format("status.git.committed", message));
                dialog.Close();
            }
            else
            {
                setStatus(Loc.Format("status.git.commit.failed", error ?? ""));
            }
        };

        dialog.ShowDialog(owner);
    }

    /// <summary>历史对话框（最近 20 条提交）。</summary>
    public static void ShowHistory(Window owner, string projectDirectory, Action<string> setStatus)
    {
        var history = GitWorkflowService.GetHistory(projectDirectory, 20);
        var list = new ListBox
        {
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(13, 21, 38)),
            Foreground = Brushes.White,
            FontFamily = new Avalonia.Media.FontFamily("Consolas, monospace"),
            FontSize = 12,
            MinWidth = 420,
            ItemsSource = history
        };

        // 关闭按钮先建成局部变量，接线时直接用实例 —— 不再 `p2.Children[2]` 按下标 + 硬转。
        // 那条路有一条静默失效面：往面板里插一个控件就把 Close 挂到**错的按钮**上，真关闭按钮点不动；
        // 插进去的若不是 Button 则硬转直接抛。同族缺陷与判据见 scripts/check-index-based-control-lookup.py。
        var closeBtn = new Button
        {
            Content = Loc["gen.close"],
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(51, 65, 85)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 6),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var dialog = new Window
        {
            Title = Loc["dialog.git.history.title"],
            Width = 480,
            Height = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(19, 28, 49)),
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = history.Count > 0 ? Loc.Format("dialog.git.history.recent", history.Count) : Loc["dialog.git.history.empty"],
                        Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(148, 163, 184)),
                        FontSize = 12
                    },
                    list,
                    closeBtn
                }
            }
        };

        closeBtn.Click += (_, _) => dialog.Close();

        dialog.ShowDialog(owner);
    }
}
