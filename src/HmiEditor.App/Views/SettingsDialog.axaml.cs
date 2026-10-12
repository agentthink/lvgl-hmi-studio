using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using HmiEditor.App.Controls;
using HmiEditor.App.Services;

namespace HmiEditor.App.Views;

/// <summary>
/// 用户界面设置对话框 —— 主题/布局/交互/显示个性化配置。
/// </summary>
public partial class SettingsDialog : ChromeWindow
{
    private readonly SettingsService _settings;
    private readonly AppThemeService _themeService;

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public SettingsDialog()
    {
        _settings = null!;
        _themeService = null!;
        InitializeComponent();
    }

    public SettingsDialog(SettingsService settings, AppThemeService themeService) : this()
    {
        _settings = settings;
        _themeService = themeService;
        PopulateCategoryList();
        PopulateThemeControls();
        PopulateLayoutControls();
        PopulateInteractionControls();
        PopulateDisplayControls();
        PopulateExplorerControls();
        PopulateAutoExportControls();
        PopulateSimulatorControls();
        PopulateDeviceControls();
        WireEvents();
        MarkNotYetEffectiveFields();
    }

    // ── 「面板上有、其实不生效」的标注 ──

    /// <summary>
    /// 按受管清单 <see cref="ConfigFieldInventory"/> 给不生效的控件加标注（灰显 + 悬停原因）。
    ///
    /// <para><b>为什么要走清单而不是就地判断：</b>"哪些设置不生效"是事实，
    /// 应该只写一遍。写在这里的判断和写在文档里的判断必然漂移，
    /// 而这份清单有守卫看着（清单 ↔ 面板标注 ↔ 真实消费点三者对齐）。</para>
    ///
    /// <para><b>键不在清单里时静默跳过</b>：说明该字段已经接了线、条目已被删掉，
    /// 于是面板不需要跟着改一行 —— 这正是我们希望"接线"这件事只有一处改动。</para>
    /// </summary>
    private void MarkNotYetEffectiveFields()
    {
        var any = false;

        any |= MarkNotEffective<SettingsService.LayoutSettings>(
            nameof(SettingsService.LayoutSettings.AutoHidePanels), ChkAutoHide);

        any |= MarkNotEffective<SettingsService.InteractionSettings>(
            nameof(SettingsService.InteractionSettings.EnableAnimations), ChkAnimations);
        any |= MarkNotEffective<SettingsService.InteractionSettings>(
            nameof(SettingsService.InteractionSettings.UndoStackSize), NumUndoStack);
        any |= MarkNotEffective<SettingsService.InteractionSettings>(
            nameof(SettingsService.InteractionSettings.ShowRulers), ChkShowRulers);
        any |= MarkNotEffective<SettingsService.InteractionSettings>(
            nameof(SettingsService.InteractionSettings.EnableLivePreview), ChkLivePreview);

        any |= MarkNotEffective<SettingsService.DisplaySettings>(
            nameof(SettingsService.DisplaySettings.DefaultFont), CmbDefaultFont);
        any |= MarkNotEffective<SettingsService.DisplaySettings>(
            nameof(SettingsService.DisplaySettings.FontStep), NumFontStep);
        any |= MarkNotEffective<SettingsService.DisplaySettings>(
            nameof(SettingsService.DisplaySettings.CanvasBackgroundColor), TxtCanvasBgColor);
        any |= MarkNotEffective<SettingsService.DisplaySettings>(
            nameof(SettingsService.DisplaySettings.ShowToolTips), ChkToolTips);
        any |= MarkNotEffective<SettingsService.DisplaySettings>(
            nameof(SettingsService.DisplaySettings.ShowPreviewOverlay), ChkPreviewOverlay);

        NotEffectiveLegend.IsVisible = any;
    }

    /// <summary>
    /// 给单个控件打标注：<typeparamref name="T"/> 的类型名 + <paramref name="field"/> 组成清单键。
    /// <para>返回是否真的标了（清单里没有这条 = 已接线 ⇒ 返回 false）。</para>
    /// </summary>
    private static bool MarkNotEffective<T>(string field, Control? control)
    {
        if (control is null) return false;
        var entry = ConfigFieldInventory.Find($"{typeof(T).Name}.{field}");
        if (entry is null || entry.Surface != ConfigFieldInventory.AppSurface) return false;
        NotYetEffective.Mark(control, entry.NoteKey);
        return true;
    }

    // ── 分类导航 ──

    private void PopulateCategoryList()
    {
        var loc = HmiEditor.App.Resources.AppLocalization.Service;
        var items = new[]
        {
            (Key: "theme",       Label: loc["settings.theme.nav"]),
            (Key: "layout",      Label: loc["settings.layout.nav"]),
            (Key: "interaction", Label: loc["settings.interaction.nav"]),
            (Key: "display",     Label: loc["settings.display.nav"]),
            (Key: "explorer",    Label: loc["settings.explorer.nav"]),
            (Key: "autoexport",  Label: loc["settings.autoexport.nav"]),
            (Key: "simulator",   Label: loc["settings.simulator.nav"]),
            (Key: "device",      Label: loc["settings.device.nav"]),
        };

        CategoryList.Items.Clear();
        foreach (var (key, label) in items)
        {
            CategoryList.Items.Add(new ListBoxItem { Content = label, Tag = key });
        }
    }

    private void OnCategorySelected(object? sender, RoutedEventArgs e)
    {
        if (CategoryList.SelectedItem is ListBoxItem selected)
        {
            var key = selected.Tag as string ?? "theme";
            PanelTheme.IsVisible = key == "theme";
            PanelLayout.IsVisible = key == "layout";
            PanelInteraction.IsVisible = key == "interaction";
            PanelDisplay.IsVisible = key == "display";
            PanelExplorer.IsVisible = key == "explorer";
            PanelAutoExport.IsVisible = key == "autoexport";
            PanelSimulator.IsVisible = key == "simulator";
            PanelDevice.IsVisible = key == "device";
        }
    }

    // ── 主题设置 ──

    private void PopulateThemeControls()
    {
        var currentTheme = _themeService.Current;
        BtnThemeDark.IsChecked = currentTheme == AppThemeService.HmiTheme.Dark;
        BtnThemeLight.IsChecked = currentTheme == AppThemeService.HmiTheme.Light;
        BtnThemeOcean.IsChecked = currentTheme == AppThemeService.HmiTheme.Ocean;

        // 三个主题各有一句人写的说明，此前**没有任何界面显示它**（只有一条测试断言它非空）
        // ⇒ 挂到按钮悬停提示上，让这份已有数据真的到用户眼前。
        ToolTip.SetTip(BtnThemeDark, AppThemeService.DescribeTheme("Dark"));
        ToolTip.SetTip(BtnThemeLight, AppThemeService.DescribeTheme("Light"));
        ToolTip.SetTip(BtnThemeOcean, AppThemeService.DescribeTheme("Ocean"));

        TxtAccentColor.Text = _themeService.CustomAccent ?? "";
        UpdateAccentSwatch(_themeService.CustomAccent);
    }

    private void OnThemePresetClicked(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button btn && btn.Tag is string tag && Enum.TryParse<AppThemeService.HmiTheme>(tag, true, out var theme))
        {
            _themeService.SetTheme(theme, _themeService.CustomAccent);
            PopulateThemeControls();
        }
    }

    private void OnAccentColorTextChanged(object? sender, TextChangedEventArgs e)
    {
        var hex = TxtAccentColor.Text?.Trim();
        UpdateAccentSwatch(hex);
        if (string.IsNullOrEmpty(hex))
        {
            // 留空恢复主题默认强调色
            _themeService.SetCustomAccent(null);
        }
        else if (IsValidHex(hex))
        {
            _themeService.SetCustomAccent(hex);
        }
    }

    private void OnAccentSwatchClicked(object? sender, PointerPressedEventArgs e)
    {
        // 打开系统颜色选择器（平台相关，此处简化为输入框焦点）
        TxtAccentColor.Focus();
        TxtAccentColor.SelectAll();
    }

    private void UpdateAccentSwatch(string? hex)
    {
        if (AccentSwatch is null) return;
        if (!string.IsNullOrEmpty(hex) && Color.TryParse(hex, out var c))
            AccentSwatch.Background = new SolidColorBrush(c);
        else if (Application.Current?.Resources.TryGetValue("HmiAccentBrush", out var res) == true &&
                 res is IBrush brush)
            AccentSwatch.Background = brush;
    }

    private static bool IsValidHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return false;
        return hex.StartsWith("#") && hex.Length is 7 or 9 &&
               hex[1..].All(Uri.IsHexDigit);
    }

    // ── 布局设置 ──

    private void PopulateLayoutControls()
    {
        NumLeftPanel.Value = (decimal)Math.Round(_settings.Layout.LeftPanelProportion * 100);
        NumRightPanel.Value = (decimal)Math.Round(_settings.Layout.RightPanelProportion * 100);
        NumBottomPanel.Value = (decimal)Math.Round(_settings.Layout.BottomPanelProportion * 100);
        ChkAutoHide.IsChecked = _settings.Layout.AutoHidePanels;
    }

    // ── 自动导出（阶段 187，登记 #87）──

    private void PopulateAutoExportControls()
    {
        ChkAutoExport.IsChecked = _settings.AutoExport.Enabled;
        NumAutoExportInterval.Value = _settings.AutoExport.IntervalMinutes;
        TxtAutoExportDirectory.Text = _settings.AutoExport.Directory;
    }

    /// <summary>
    /// 「选择目录…」：选到就写进输入框（<c>TextChanged</c> 会把它一起落进设置）；
    /// 取消或平台桩返回空 ⇒ **什么都不做**（选择失败也保持原值：与"取消"同义，弹个错框没有意义）。
    /// </summary>
    private async void OnPickAutoExportDirectory(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not { } top) return;

            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = HmiEditor.App.Resources.AppLocalization.Service["settings.autoexport.directory"],
                AllowMultiple = false,
            });

            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            TxtAutoExportDirectory.Text = path;
        }
        catch
        {
            // 目录选择是"可选辅助"：失败保持原值即可（不改设置、不打断对话框）
        }
    }

    // ── 交互设置 ──

    private void PopulateInteractionControls()
    {
        ChkAnimations.IsChecked = _settings.Interaction.EnableAnimations;
        ChkSnapToGrid.IsChecked = _settings.Interaction.EnableSnapToGrid;
        NumGridSize.Value = _settings.Interaction.GridSize;
        NumSnapStep.Value = _settings.Interaction.SnapStep;
        ChkShowGrid.IsChecked = _settings.Interaction.ShowGrid;
        ChkShowRulers.IsChecked = _settings.Interaction.ShowRulers;
        NumUndoStack.Value = _settings.Interaction.UndoStackSize;
        ChkLivePreview.IsChecked = _settings.Interaction.EnableLivePreview;
        ChkAutoSave.IsChecked = _settings.Interaction.EnableAutoSave;
        NumAutoSaveInterval.Value = _settings.Interaction.AutoSaveInterval;
    }

    // ── 显示设置 ──

    private void PopulateDisplayControls()
    {
        CmbDefaultFont.Items.Clear();
        var fonts = FontManager.Current.SystemFonts.OrderBy(f => f.Name).ToArray();
        foreach (var f in fonts)
            CmbDefaultFont.Items.Add(new ComboBoxItem { Content = f.Name, Tag = f.Name });

        var currentFont = _settings.Display.DefaultFont;
        var matching = fonts.FirstOrDefault(f => f.Name == currentFont);
        CmbDefaultFont.SelectedItem = matching != null
            ? CmbDefaultFont.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag is string s && s == currentFont)
            : null;

        TxtCanvasBgColor.Text = _settings.Display.CanvasBackgroundColor;
        if (Color.TryParse(_settings.Display.CanvasBackgroundColor, out var canvasColor))
            CanvasBgSwatch.Background = new SolidColorBrush(canvasColor);

        NumFontStep.Value = _settings.Display.FontStep;

        CmbPreviewRes.Items.Clear();
        var resolutions = new[]
        {
            (Tag: "", Label: "Off"),
            (Tag: "800x480", Label: "800×480"),
            (Tag: "1024x600", Label: "1024×600"),
            (Tag: "1280x800", Label: "1280×800"),
            (Tag: "480x800", Label: "480×800"),
        };
        foreach (var r in resolutions)
            CmbPreviewRes.Items.Add(new ComboBoxItem { Content = r.Label, Tag = r.Tag });
        var selIndex = Array.FindIndex(resolutions, r => r.Tag == _settings.Display.PreviewResolution);
        CmbPreviewRes.SelectedIndex = selIndex < 0 ? 0 : selIndex;

        ChkToolTips.IsChecked = _settings.Display.ShowToolTips;
        ChkPreviewOverlay.IsChecked = _settings.Display.ShowPreviewOverlay;
    }

    // ── 项目浏览器设置（右键增强菜单，管理员可配置）──

    private void PopulateExplorerControls()
    {
        var explorer = _settings.Explorer;
        ChkExplorerCtx.IsChecked = explorer.EnableContextMenu;
        ChkCtxCreate.IsChecked = explorer.ShowCreate;
        ChkCtxOpen.IsChecked = explorer.ShowOpenScreen;
        ChkCtxStartup.IsChecked = explorer.ShowStartupScreen;
        ChkCtxPublic.IsChecked = explorer.ShowPublicPage;
        ChkCtxRename.IsChecked = explorer.ShowRename;
        ChkCtxDuplicate.IsChecked = explorer.ShowDuplicate;
        ChkCtxDelete.IsChecked = explorer.ShowDelete;
        ChkCtxCopyPaste.IsChecked = explorer.ShowCopyPaste;
        ChkCtxMoveLayer.IsChecked = explorer.ShowMoveLayer;
    }

    private void OnCanvasBgSwatchClicked(object? sender, PointerPressedEventArgs e)
    {
        TxtCanvasBgColor.Focus();
        TxtCanvasBgColor.SelectAll();
    }

    // ── PC 模拟器设置（LVGL 源码目录 / 工作目录）──

    private void PopulateSimulatorControls()
    {
        TxtLvglDir.Text = _settings.Simulator.LvglDir;
        TxtSimWorkDir.Text = _settings.Simulator.WorkDir;
    }

    // ── 真机部署设置（T113 SDK / 交叉工具链）──

    private void PopulateDeviceControls()
    {
        TxtT113SdkRoot.Text = _settings.Device.T113SdkRoot;
        TxtT113Toolchain.Text = _settings.Device.T113ToolchainGcc;
    }

    // ── 事件绑定 ──

    private void WireEvents()
    {
        CategoryList.SelectionChanged += OnCategorySelected;
        // 注：三个主题按钮的 Click 在 .axaml 里声明过（同一个 OnThemePresetClicked），
        // 这里曾经再 += 一遍 ⇒ 每次点按 SetTheme 执行 2 次（缺陷 #43）。
        TxtAccentColor.TextChanged += OnAccentColorTextChanged;
        BtnOk.Click += OnOkClicked;
        BtnCancel.Click += OnCancelClicked;
        BtnApply.Click += OnApplyClicked;

        // 自动导出（阶段 187）
        ChkAutoExport.IsCheckedChanged += (_, _) => _settings.AutoExport.SetEnabled(ChkAutoExport.IsChecked == true);
        NumAutoExportInterval.ValueChanged += (_, _) => _settings.AutoExport.SetIntervalMinutes((int)(NumAutoExportInterval.Value ?? 30));
        TxtAutoExportDirectory.TextChanged += (_, _) => _settings.AutoExport.SetDirectory(TxtAutoExportDirectory.Text ?? "");
        BtnPickAutoExportDirectory.Click += OnPickAutoExportDirectory;

        // 布局
        NumLeftPanel.ValueChanged += (_, _) => _settings.Layout.SetLeftPanelProportion((double)(NumLeftPanel.Value ?? 0) / 100.0);
        NumRightPanel.ValueChanged += (_, _) => _settings.Layout.SetRightPanelProportion((double)(NumRightPanel.Value ?? 0) / 100.0);
        NumBottomPanel.ValueChanged += (_, _) => _settings.Layout.SetBottomPanelProportion((double)(NumBottomPanel.Value ?? 0) / 100.0);
        ChkAutoHide.IsCheckedChanged += (_, _) => _settings.Layout.SetAutoHidePanels(ChkAutoHide.IsChecked == true);

        // 交互
        ChkAnimations.IsCheckedChanged += (_, _) => _settings.Interaction.SetEnableAnimations(ChkAnimations.IsChecked == true);
        ChkSnapToGrid.IsCheckedChanged += (_, _) => _settings.Interaction.SetEnableSnapToGrid(ChkSnapToGrid.IsChecked == true);
        NumGridSize.ValueChanged += (_, _) => _settings.Interaction.SetGridSize((int)(NumGridSize.Value ?? 0));
        NumSnapStep.ValueChanged += (_, _) => _settings.Interaction.SetSnapStep((int)(NumSnapStep.Value ?? 0));
        ChkShowGrid.IsCheckedChanged += (_, _) => _settings.Interaction.SetShowGrid(ChkShowGrid.IsChecked == true);
        ChkShowRulers.IsCheckedChanged += (_, _) => _settings.Interaction.SetShowRulers(ChkShowRulers.IsChecked == true);
        NumUndoStack.ValueChanged += (_, _) => _settings.Interaction.SetUndoStackSize((int)(NumUndoStack.Value ?? 0));
        ChkLivePreview.IsCheckedChanged += (_, _) => _settings.Interaction.SetEnableLivePreview(ChkLivePreview.IsChecked == true);
        ChkAutoSave.IsCheckedChanged += (_, _) => _settings.Interaction.SetEnableAutoSave(ChkAutoSave.IsChecked == true);
        NumAutoSaveInterval.ValueChanged += (_, _) => _settings.Interaction.SetAutoSaveInterval((int)(NumAutoSaveInterval.Value ?? 0));

        // 显示
        CmbDefaultFont.SelectionChanged += (_, _) =>
        {
            if (CmbDefaultFont.SelectedItem is ComboBoxItem item && item.Tag is string font)
                _settings.Display.SetDefaultFont(font);
        };
        TxtCanvasBgColor.TextChanged += (_, _) =>
        {
            var hex = TxtCanvasBgColor.Text?.Trim() ?? "";
            if (IsValidHex(hex))
            {
                _settings.Display.SetCanvasBackgroundColor(hex);
                CanvasBgSwatch.Background = new SolidColorBrush(Color.Parse(hex));
            }
        };
        CmbPreviewRes.SelectionChanged += (_, _) =>
        {
            if (CmbPreviewRes.SelectedItem is ComboBoxItem ri && ri.Tag is string res)
                _settings.Display.SetPreviewResolution(res);
        };
        ChkToolTips.IsCheckedChanged += (_, _) => _settings.Display.SetShowToolTips(ChkToolTips.IsChecked == true);
        ChkPreviewOverlay.IsCheckedChanged += (_, _) => _settings.Display.SetShowPreviewOverlay(ChkPreviewOverlay.IsChecked == true);
        NumFontStep.ValueChanged += (_, _) => _settings.Display.SetFontStep((int)(NumFontStep.Value ?? 0));

        // 项目浏览器右键菜单
        ChkExplorerCtx.IsCheckedChanged += (_, _) => _settings.Explorer.SetEnableContextMenu(ChkExplorerCtx.IsChecked == true);
        ChkCtxCreate.IsCheckedChanged += (_, _) => _settings.Explorer.SetShowCreate(ChkCtxCreate.IsChecked == true);
        ChkCtxOpen.IsCheckedChanged += (_, _) => _settings.Explorer.SetShowOpenScreen(ChkCtxOpen.IsChecked == true);
        ChkCtxStartup.IsCheckedChanged += (_, _) => _settings.Explorer.SetShowStartupScreen(ChkCtxStartup.IsChecked == true);
        ChkCtxPublic.IsCheckedChanged += (_, _) => _settings.Explorer.SetShowPublicPage(ChkCtxPublic.IsChecked == true);
        ChkCtxRename.IsCheckedChanged += (_, _) => _settings.Explorer.SetShowRename(ChkCtxRename.IsChecked == true);
        ChkCtxDuplicate.IsCheckedChanged += (_, _) => _settings.Explorer.SetShowDuplicate(ChkCtxDuplicate.IsChecked == true);
        ChkCtxDelete.IsCheckedChanged += (_, _) => _settings.Explorer.SetShowDelete(ChkCtxDelete.IsChecked == true);
        ChkCtxCopyPaste.IsCheckedChanged += (_, _) => _settings.Explorer.SetShowCopyPaste(ChkCtxCopyPaste.IsChecked == true);
        ChkCtxMoveLayer.IsCheckedChanged += (_, _) => _settings.Explorer.SetShowMoveLayer(ChkCtxMoveLayer.IsChecked == true);

        // 模拟器
        TxtLvglDir.TextChanged += (_, _) => _settings.Simulator.SetLvglDir(TxtLvglDir.Text?.Trim() ?? "");
        TxtSimWorkDir.TextChanged += (_, _) => _settings.Simulator.SetWorkDir(TxtSimWorkDir.Text?.Trim() ?? "");

        // 真机部署
        TxtT113SdkRoot.TextChanged += (_, _) => _settings.Device.SetT113SdkRoot(TxtT113SdkRoot.Text?.Trim() ?? "");
        TxtT113Toolchain.TextChanged += (_, _) => _settings.Device.SetT113ToolchainGcc(TxtT113Toolchain.Text?.Trim() ?? "");
    }

    // ── 按钮操作 ──

    private void OnApplyClicked(object? sender, RoutedEventArgs e)
    {
        // 所有设置项均为即时保存，Apply 仅提供确认反馈
    }

    private void OnOkClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}