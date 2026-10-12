using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using HmiEditor.App.Resources;
using HmiEditor.App.Controls;
using HmiEditor.App;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Views;

/// <summary>
/// 主题与样式编辑器对话框 —— 完全在 code-behind 构建 UI。
/// </summary>
public partial class StyleEditorView : ChromeWindow
{
    private readonly HmiTheme _theme;
    private readonly HmiProject _project;
    private bool _isUpdating;

    // 文本字段
    private TextBox _tbThemeName = null!;
    private readonly Dictionary<string, TextBox> _colorFields = new();
    private TextBox _tbFontSize = null!;
    private TextBox _tbCornerRadius = null!;
    private TextBox _tbBorderWidth = null!;

    public StyleEditorView()
    {
        _theme = null!;
        _project = null!;
        InitializeComponent();
    }

    public StyleEditorView(HmiProject project) : this()
    {
        _project = project;
        _theme = project.Theme;
        BuildUi();
        LoadTheme();
    }

    private void BuildUi()
    {
        var scrollContent = new StackPanel { Spacing = 8 };

        // 标题
        scrollContent.Children.Add(new TextBlock
        {
            Text = AppLocalization.Service[Lang.StyleEditorTitle],
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 12)
        });

        // 主题名称
        _tbThemeName = AddField(scrollContent, AppLocalization.Service["theme.name"], "Dark Industrial");

        // 颜色分组标题
        scrollContent.Children.Add(SectionHeader(AppLocalization.Service["theme.colors"]));

        // 颜色字段
        AddColorField(scrollContent, AppLocalization.Service[Lang.StyleColorPrimary], "PrimaryColor", "#2563eb");
        AddColorField(scrollContent, AppLocalization.Service[Lang.StyleColorSecondary], "SecondaryColor", "#059669");
        AddColorField(scrollContent, AppLocalization.Service[Lang.StyleColorSurface], "SurfaceColor", "#1a2332");
        AddColorField(scrollContent, AppLocalization.Service[Lang.StyleColorBackground], "BackgroundColor", "#131c31");
        AddColorField(scrollContent, AppLocalization.Service[Lang.StyleColorText], "TextColor", "#e2e8f0");
        AddColorField(scrollContent, AppLocalization.Service[Lang.StyleColorTextSecondary], "TextSecondaryColor", "#7c8aa0");
        AddColorField(scrollContent, AppLocalization.Service[Lang.StyleColorBorder], "BorderColor", "#334155");
        AddColorField(scrollContent, AppLocalization.Service["style.success.color"], "SuccessColor", "#22c55e");
        AddColorField(scrollContent, AppLocalization.Service["style.warning.color"], "WarningColor", "#f59e0b");
        AddColorField(scrollContent, AppLocalization.Service["style.error.color"], "ErrorColor", "#ef4444");

        // 尺寸分组标题
        scrollContent.Children.Add(SectionHeader(AppLocalization.Service["theme.dimensions"]));

        _tbFontSize = AddField(scrollContent, AppLocalization.Service[Lang.StyleDimFontSize], "14");
        _tbCornerRadius = AddField(scrollContent, AppLocalization.Service[Lang.StyleDimRadius], "6");
        _tbBorderWidth = AddField(scrollContent, AppLocalization.Service[Lang.StyleDimBorderWidth], "1");

        // 底部按钮
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var btnPreset = new Button
        {
            Content = AppLocalization.Service[Lang.StylePresetTemplate],
            Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6),
            FontSize = 13
        };
        btnPreset.Click += async (_, _) =>
        {
            var picker = new StylePresetPicker();
            await picker.ShowDialog(this);
            if (picker.SelectedTheme != null)
            {
                LoadTheme(picker.SelectedTheme);
            }
        };
        btnPanel.Children.Add(btnPreset);

        var btnReset = new Button
        {
            Content = AppLocalization.Service["theme.reset"],
            Background = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6),
            FontSize = 13
        };
        btnReset.Click += async (_, _) =>
        {
            // 重置会丢弃当前未保存的编辑，先确认。
            var ok = await AppDialogs.ConfirmAsync(this, AppLocalization.Service["theme.confirm.reset"]);
            if (ok) LoadTheme(new HmiTheme());
        };
        btnPanel.Children.Add(btnReset);

        var btnApply = new Button
        {
            Content = AppLocalization.Service["theme.apply"],
            Background = HmiEditor.App.Theme.AccentHover,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6),
            FontSize = 13
        };
        btnApply.Click += (_, _) => OnApply();
        btnPanel.Children.Add(btnApply);

        // 组装
        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        var scrollViewer = new ScrollViewer
        {
            Content = scrollContent
        };
        Grid.SetRow(scrollViewer, 1);
        rootGrid.Children.Add(scrollViewer);

        Grid.SetRow(btnPanel, 2);
        rootGrid.Children.Add(btnPanel);

        RootBorder.Child = rootGrid;

        // 连接 TextChanged 事件
        WireTextChanged(_tbThemeName);
        WireTextChanged(_tbFontSize);
        WireTextChanged(_tbCornerRadius);
        WireTextChanged(_tbBorderWidth);
    }

    private void LoadTheme(HmiTheme? theme = null)
    {
        _isUpdating = true;
        var t = theme ?? _theme;

        _tbThemeName.Text = t.Name;
        SetColorField("PrimaryColor", t.PrimaryColor);
        SetColorField("SecondaryColor", t.SecondaryColor);
        SetColorField("SurfaceColor", t.SurfaceColor);
        SetColorField("BackgroundColor", t.BackgroundColor);
        SetColorField("TextColor", t.TextColor);
        SetColorField("TextSecondaryColor", t.TextSecondaryColor);
        SetColorField("BorderColor", t.BorderColor);
        SetColorField("SuccessColor", t.SuccessColor);
        SetColorField("WarningColor", t.WarningColor);
        SetColorField("ErrorColor", t.ErrorColor);
        _tbFontSize.Text = t.FontSize.ToString();
        _tbCornerRadius.Text = t.CornerRadius.ToString();
        _tbBorderWidth.Text = t.BorderWidth.ToString();

        if (theme != null)
        {
            _theme.Name = theme.Name;
            _theme.PrimaryColor = theme.PrimaryColor;
            _theme.SecondaryColor = theme.SecondaryColor;
            _theme.SurfaceColor = theme.SurfaceColor;
            _theme.BackgroundColor = theme.BackgroundColor;
            _theme.TextColor = theme.TextColor;
            _theme.TextSecondaryColor = theme.TextSecondaryColor;
            _theme.BorderColor = theme.BorderColor;
            _theme.SuccessColor = theme.SuccessColor;
            _theme.WarningColor = theme.WarningColor;
            _theme.ErrorColor = theme.ErrorColor;
            _theme.FontSize = theme.FontSize;
            _theme.CornerRadius = theme.CornerRadius;
            _theme.BorderWidth = theme.BorderWidth;
        }

        _isUpdating = false;
    }

    private void SyncThemeFromUi()
    {
        if (_isUpdating) return;

        _theme.Name = _tbThemeName.Text ?? "";
        _theme.PrimaryColor = GetColorField("PrimaryColor");
        _theme.SecondaryColor = GetColorField("SecondaryColor");
        _theme.SurfaceColor = GetColorField("SurfaceColor");
        _theme.BackgroundColor = GetColorField("BackgroundColor");
        _theme.TextColor = GetColorField("TextColor");
        _theme.TextSecondaryColor = GetColorField("TextSecondaryColor");
        _theme.BorderColor = GetColorField("BorderColor");
        _theme.SuccessColor = GetColorField("SuccessColor");
        _theme.WarningColor = GetColorField("WarningColor");
        _theme.ErrorColor = GetColorField("ErrorColor");
        _theme.FontSize = ParseInt(_tbFontSize.Text, 14);
        _theme.CornerRadius = ParseInt(_tbCornerRadius.Text, 6);
        _theme.BorderWidth = ParseInt(_tbBorderWidth.Text, 1);
    }

    private void OnApply()
    {
        SyncThemeFromUi();
        _project.Theme = _theme;
        Close();
    }

    // ── UI 辅助 ──

    private static TextBlock SectionHeader(string text) => new()
    {
        Text = text,
        Foreground = HmiEditor.App.Theme.TextHint,
        FontSize = 11,
        Margin = new Thickness(4, 8, 0, 2)
    };

    private TextBox AddField(StackPanel parent, string label, string defaultValue)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("140,*"), Margin = new Thickness(4, 0) };
        grid.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center
        });
        var tb = new TextBox { Text = defaultValue, Margin = new Thickness(0, 2) };
        Grid.SetColumn(tb, 1);
        grid.Children.Add(tb);
        parent.Children.Add(grid);
        return tb;
    }

    private void AddColorField(StackPanel parent, string label, string key, string defaultColor)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("140,*"), Margin = new Thickness(4, 0) };
        grid.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center
        });
        var tb = new TextBox
        {
            Text = defaultColor,
            PlaceholderText = "#RRGGBB",
            Margin = new Thickness(0, 2)
        };
        Grid.SetColumn(tb, 1);
        grid.Children.Add(tb);
        parent.Children.Add(grid);
        _colorFields[key] = tb;
        WireTextChanged(tb);
    }

    private void SetColorField(string key, string value)
    {
        if (_colorFields.TryGetValue(key, out var tb))
            tb.Text = value;
    }

    private string GetColorField(string key) =>
        _colorFields.TryGetValue(key, out var tb) ? tb.Text ?? "" : "";

    private void WireTextChanged(TextBox tb) =>
        tb.TextChanged += (_, _) => SyncThemeFromUi();

    private static int ParseInt(string? text, int fallback) =>
        int.TryParse(text, out var v) ? v : fallback;
}
