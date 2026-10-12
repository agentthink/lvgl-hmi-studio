using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Layout;
using HmiEditor.App.Resources;

namespace HmiEditor.App.Controls;

/// <summary>颜色选择弹窗 —— 12 色预置色板 + 自定义 #RRGGBB 输入。</summary>
public class ColorPickerPopup : ContentControl
{
    // 深色面板配色：与 App.axaml 深色主题保持一致
    // ⚠ 静态字段用**不可变**画刷（阶段 295）：类型初始化器跑在"谁先碰到这个类型"的线程上，
    //    可变画刷（SolidColorBrush）在 Dispatcher 尚未建立时会把 s_uiThread 抢到该线程 ⇒
    //    下一次派发就报 "different thread owns it"（登记 #47 的一条可复现根因）。
    private static readonly IBrush DarkPanelBg   = new ImmutableSolidColorBrush(Color.FromRgb(26, 35, 50));    // HmiBgPanel
    private static readonly IBrush DarkBorder    = new ImmutableSolidColorBrush(Color.FromRgb(42, 58, 85));     // HmiBorder
    private static readonly IBrush DarkInputBg   = new ImmutableSolidColorBrush(Color.FromRgb(15, 23, 42));     // HmiBgDeep
    private static readonly IBrush SwatchBorder  = new ImmutableSolidColorBrush(Color.FromRgb(71, 85, 105));
    private static readonly IBrush MutedText     = new ImmutableSolidColorBrush(Color.FromRgb(148, 163, 184)); // HmiTextSecondary
    private static readonly IBrush White         = new ImmutableSolidColorBrush(Colors.White);

    public string SelectedColor { get; private set; } = "";
    public event EventHandler<string>? ColorSelected;

    private readonly Popup _popup;
    private readonly TextBox _customInput;

    private static readonly (string Name, string Hex)[] PresetColors = new[]
    {
        ("color.blue", "#2563eb"), ("color.sky", "#38bdf8"), ("color.cyan", "#06b6d4"), ("color.green", "#22c55e"),
        ("color.yellow", "#eab308"), ("color.orange", "#f97316"), ("color.red", "#ef4444"), ("color.pink", "#ec4899"),
        ("color.purple", "#a855f7"), ("color.white", "#ffffff"), ("color.gray", "#64748b"), ("color.dark", "#0f172a"),
    };

    public ColorPickerPopup()
    {
        _popup = new Popup { Placement = PlacementMode.Bottom, HorizontalOffset = 4 };

        var mainPanel = new Border
        {
            Background = DarkPanelBg, BorderBrush = DarkBorder,
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10), Child = new StackPanel { Spacing = 8 }
        };
        var panel = (StackPanel)mainPanel.Child;

        panel.Children.Add(new TextBlock
        {
            Text = AppLocalization.Service["color.picker.title"],
            Foreground = White, FontSize = 13, FontWeight = FontWeight.Bold
        });

        var wrapPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var (name, hex) in PresetColors)
        {
            var swatch = new Border
            {
                Width = 36, Height = 28, CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.Parse(hex)),
                BorderThickness = new Thickness(1), BorderBrush = SwatchBorder,
                Cursor = new Cursor(StandardCursorType.Hand), Tag = hex, Margin = new Thickness(2)
            };
            ToolTip.SetTip(swatch, $"{AppLocalization.Service[name]} ({hex})");
            swatch.PointerPressed += (_, _) => SelectColor(hex);
            wrapPanel.Children.Add(swatch);
        }
        panel.Children.Add(wrapPanel);

        var clearSwatch = new Border
        {
            Width = 36, Height = 28, CornerRadius = new CornerRadius(4),
            Background = DarkPanelBg, BorderThickness = new Thickness(1),
            BorderBrush = SwatchBorder, Cursor = new Cursor(StandardCursorType.Hand),
            Tag = "", Margin = new Thickness(2)
        };
        ToolTip.SetTip(clearSwatch, AppLocalization.Service["color.picker.clear"]);
        clearSwatch.Child = new TextBlock
        {
            Text = "✕", Foreground = MutedText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, FontSize = 12
        };
        clearSwatch.PointerPressed += (_, _) => SelectColor("");
        var clearWrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
        clearWrap.Children.Add(clearSwatch);
        panel.Children.Add(clearWrap);

        panel.Children.Add(new Border { Height = 1, Background = DarkBorder, Margin = new Thickness(0, 2) });

        var inputPanel = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 4
        };
        inputPanel.Children.Add(new TextBlock
        {
            Text = "#", Foreground = MutedText,
            VerticalAlignment = VerticalAlignment.Center, FontSize = 14
        });

        _customInput = new TextBox
        {
            Background = DarkInputBg, Foreground = White,
            BorderBrush = DarkBorder, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3),
            FontSize = 13, MaxLength = 6, PlaceholderText = "RRGGBB"
        };
        Grid.SetColumn(_customInput, 1);
        inputPanel.Children.Add(_customInput);

        var btnApply = new Button
        {
            Content = AppLocalization.Service["gen.apply"],
            Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            Foreground = White, BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 3), FontSize = 12,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        btnApply.Click += (_, _) =>
        {
            var hex = _customInput.Text?.Trim();
            if (!string.IsNullOrEmpty(hex))
            {
                if (!hex.StartsWith("#")) hex = "#" + hex;
                try { Color.Parse(hex); SelectColor(hex); }
                catch { _customInput.Text = ""; }
            }
        };
        Grid.SetColumn(btnApply, 2);
        inputPanel.Children.Add(btnApply);

        panel.Children.Add(inputPanel);
        _popup.Child = mainPanel;
    }

    public void Open(Control placementTarget, string currentColor)
    {
        _customInput.Text = currentColor.TrimStart('#');
        _popup.PlacementTarget = placementTarget;
        _popup.IsOpen = true;
    }

    private void SelectColor(string hex)
    {
        SelectedColor = hex;
        ColorSelected?.Invoke(this, hex);
        _popup.IsOpen = false;
    }
}
