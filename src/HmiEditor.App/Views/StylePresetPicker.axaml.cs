using Avalonia;
using HmiEditor.App.Resources;
using HmiEditor.App;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Views;

public partial class StylePresetPicker : ChromeWindow
{
    /// <summary>用户选择的预设主题，null = 取消。</summary>
    public HmiTheme? SelectedTheme { get; private set; }

    public StylePresetPicker()
    {
        InitializeComponent();
        BuildPresetCards();
        BtnCancel.Click += (_, _) => Close();
    }

    private void BuildPresetCards()
    {
        foreach (var preset in StylePreset.DefaultPresets)
        {
            var card = new Border
            {
                Width = 260,
                Background = HmiEditor.App.Theme.BgPanel,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = HmiEditor.App.Theme.Border,
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 4),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var stack = new StackPanel { Spacing = 6 };

            // Name
            stack.Children.Add(new TextBlock
            {
                Text = preset.Name,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 16,
                FontWeight = FontWeight.Bold
            });

            // Description
            stack.Children.Add(new TextBlock
            {
                Text = preset.Description,
                Foreground = HmiEditor.App.Theme.TextHint,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });

            // Color swatches row
            var swatchPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Margin = new Thickness(0, 6, 0, 0)
            };

            var theme = preset.Theme;
            foreach (var hex in new[] { theme.PrimaryColor, theme.SecondaryColor,
                theme.SurfaceColor, theme.SuccessColor, theme.WarningColor, theme.ErrorColor })
            {
                var color = ParseHexColor(hex, Colors.Gray);
                swatchPanel.Children.Add(new Border
                {
                    Width = 20, Height = 20,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(color),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(60, 70, 90))
                });
            }

            stack.Children.Add(swatchPanel);

            // Apply button
            var btnApply = new Button
            {
                Content = AppLocalization.Service["style.preset.apply"],
                Background = HmiEditor.App.Theme.AccentHover,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 4),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0),
                Tag = preset
            };
            btnApply.Click += (_, _) =>
            {
                SelectedTheme = preset.Theme;
                Close();
            };
            stack.Children.Add(btnApply);

            card.Child = stack;

            // Click on card also selects
            card.PointerPressed += (_, _) =>
            {
                SelectedTheme = preset.Theme;
                Close();
            };

            (PresetList.Items as System.Collections.IList)?.Add(card);
        }
    }

    private static Color ParseHexColor(string? hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex)) return fallback;
        try { return Color.Parse(hex.StartsWith('#') ? hex : "#" + hex); }
        catch { return fallback; }
    }
}
