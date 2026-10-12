using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HmiEditor.App.Resources;

namespace HmiEditor.App.Controls;

/// <summary>
/// 应用级轻量对话框工具。
/// </summary>
public static class AppDialogs
{
    /// <summary>当前台窗口（用于锚点为 null 时的对话框宿主）。</summary>
    private static Window? AppWindow =>
        Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow
                ?? desktop.Windows.FirstOrDefault(w => w.IsActive),
            _ => null
        };

    /// <summary>
    /// 弹出"确认"对话框；确定返回 true，取消返回 false。
    /// 复刻 RecipeEditorView 内部 ConfirmDialog 的视觉，抽取为公共实现以便跨 View/控件复用。
    /// </summary>
    public static Task<bool> ConfirmAsync(Visual? anchor, string message)
    {
        // 对话框必须在 UI 线程展示。若调用来自非 UI 线程则切回 UI 线程。
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.InvokeAsync(() => ConfirmCoreAsync(anchor, message));
        }
        return ConfirmCoreAsync(anchor, message);
    }

    private static async Task<bool> ConfirmCoreAsync(Visual? anchor, string message)
    {
        // 从锚点解析宿主 Window；未找到时回退到应用当前前台窗口。
        var owner = TopLevel.GetTopLevel(anchor) as Window
                    ?? AppWindow;
        owner ??= new Window(); // 极端兜底：无宿主则创建游离窗口
        var confirmed = false;
        var dialog = new Window
        {
            Width = DialogMetrics.ConfirmWidth,
            Height = DialogMetrics.ConfirmHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = HmiEditor.App.Theme.BgWindow,
            WindowDecorations = WindowDecorations.BorderOnly,
        };

        var messageText = new TextBlock
        {
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            Margin = new Thickness(16, 16, 16, 12),
            TextWrapping = TextWrapping.Wrap
        };
        messageText.Text = message;

        void Close(bool ok)
        {
            confirmed = ok;
            dialog.Close();
        }

        var cancelBtn = new Button
        {
            Content = AppLocalization.Service["gen.cancel"],
            Background = HmiEditor.App.Theme.BgElevated,
            Foreground = HmiEditor.App.Theme.TextSecondary,
            BorderThickness = new Thickness(1),
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            Padding = new Thickness(14, 6),
            FontSize = 12
        };
        cancelBtn.Click += (_, _) => Close(false);

        var okBtn = new Button
        {
            Content = AppLocalization.Service["gen.confirm.delete"],
            Background = HmiEditor.App.Theme.Danger,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6),
            FontSize = 12
        };
        okBtn.Click += (_, _) => Close(true);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(16, 0, 16, 16)
        };
        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(okBtn);

        var titleText = new TextBlock
        {
            Text = AppLocalization.Service[Lang.GenConfirm],
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var titleBar = new Border
        {
            Background = HmiEditor.App.Theme.BgPanel,
            Height = DialogMetrics.ButtonHeight,
            Child = titleText
        };

        var contentPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { messageText, btnPanel }
        };

        var root = new DockPanel
        {
            Children =
            {
                titleBar,
                contentPanel
            }
        };
        DockPanel.SetDock(titleBar, Avalonia.Controls.Dock.Top);
        dialog.Content = root;

        await dialog.ShowDialog(owner);
        return confirmed;
    }

    /// <summary>
    /// 弹出"输入"对话框（用于重命名等）；确定返回输入文本（trim 后），取消返回 null。
    /// </summary>
    public static Task<string?> PromptAsync(Visual? anchor, string title, string initialValue = "")
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.InvokeAsync(() => PromptCoreAsync(anchor, title, initialValue));
        }
        return PromptCoreAsync(anchor, title, initialValue);
    }

    private static async Task<string?> PromptCoreAsync(Visual? anchor, string title, string initialValue)
    {
        var owner = TopLevel.GetTopLevel(anchor) as Window ?? AppWindow;
        owner ??= new Window();
        string? result = null;
        var dialog = new Window
        {
            Width = DialogMetrics.InputWidth,
            Height = DialogMetrics.InputHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = HmiEditor.App.Theme.BgWindow,
            WindowDecorations = WindowDecorations.BorderOnly,
        };

        var input = new TextBox
        {
            Text = initialValue,
            FontSize = 13,
            Background = HmiEditor.App.Theme.BgInput,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            Margin = new Thickness(16, 12, 16, 4),
            PlaceholderText = title
        };
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter) { result = input.Text?.Trim(); dialog.Close(); }
            else if (e.Key == Avalonia.Input.Key.Escape) { dialog.Close(); }
        };

        void CloseOk()
        {
            result = input.Text?.Trim();
            dialog.Close();
        }

        var cancelBtn = new Button
        {
            Content = AppLocalization.Service["gen.cancel"],
            Background = HmiEditor.App.Theme.BgElevated,
            Foreground = HmiEditor.App.Theme.TextSecondary,
            BorderThickness = new Thickness(1),
            BorderBrush = HmiEditor.App.Theme.InputBorder,
            Padding = new Thickness(14, 6),
            FontSize = 12
        };
        cancelBtn.Click += (_, _) => dialog.Close();

        var okBtn = new Button
        {
            Content = AppLocalization.Service["gen.ok"],
            Background = HmiEditor.App.Theme.Accent,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6),
            FontSize = 12
        };
        okBtn.Click += (_, _) => CloseOk();

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(16, 0, 16, 16)
        };
        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(okBtn);

        var titleText = new TextBlock
        {
            Text = title,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var titleBar = new Border
        {
            Background = HmiEditor.App.Theme.BgPanel,
            Height = DialogMetrics.ButtonHeight,
            Child = titleText
        };

        var contentPanel = new StackPanel
        {
            Children = { input, btnPanel }
        };

        var root = new DockPanel
        {
            Children = { titleBar, contentPanel }
        };
        DockPanel.SetDock(titleBar, Avalonia.Controls.Dock.Top);
        dialog.Content = root;

        await dialog.ShowDialog(owner);
        return string.IsNullOrEmpty(result) ? null : result;
    }
}
