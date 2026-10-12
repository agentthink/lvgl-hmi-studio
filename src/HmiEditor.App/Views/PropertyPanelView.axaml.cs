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
    private HmiControl? _currentControl;
    private bool _isUpdating;
    private string? _activeColorField;
    private int _selectedActionIndex;
    private readonly ColorPickerPopup _colorPicker = new();
    private VariableManager? _variableManager;
    private TemplateManager? _templateManager;
    private HmiProject? _currentProject;
    // 动作目标下拉选中时暂存对应的 BindingId，避免 UI 显示友好名称后误把名称写回模型
    private string? _pendingActionTargetId;
    // 状态样式：当前正在编辑的视觉状态（Normal/Pressed/Checked/Disabled）
    private VisualState _currentEditingState = VisualState.Normal;

    public new event EventHandler<HmiControl>? PropertyChanged;

    /// <summary>屏幕页面属性变更（未选中控件时编辑屏幕属性触发）。</summary>
    public event EventHandler<HmiScreen>? ScreenPropertyChanged;

    /// <summary>
    /// 属性即时校验器（P1-3）：保存前逐属性校验，返回错误消息则跳过写回并记录日志。
    /// 默认 DefaultPropertyValidator（数值范围/名称/颜色格式）；插件或配置下发可实现 IPropertyValidator 替换。
    /// </summary>
    public IPropertyValidator? CustomValidator { get; set; } = new DefaultPropertyValidator();

    public PropertyPanelView()
    {
        InitializeComponent();

        // 订阅自身 PropertyChanged 事件并转发到 DockLayoutState 聚合器，
        // 确保 Dock 重建后事件仍能到达 MainWindow
        PropertyChanged += (_, control) =>
        {
            if (control != null)
                DockLayoutState.RaisePropertyChanged(this, control);
        };

        // 屏幕属性变更同样转发到聚合器
        ScreenPropertyChanged += (_, screen) =>
        {
            if (screen != null)
                DockLayoutState.RaiseScreenPropertyChanged(this, screen);
        };

        // FluentAvalonia 3.0.2 FANumberBox.ValueChanged 使用 TypedEventHandler 委托，
        // XAML 编译器无法自动转换字符串处理程序名，需在代码中订阅
        PropX.ValueChanged += OnNumberChanged;
        PropY.ValueChanged += OnNumberChanged;
        PropWidth.ValueChanged += OnNumberChanged;
        PropHeight.ValueChanged += OnNumberChanged;
        PropRadius.ValueChanged += OnNumberChanged;
        PropBorderWidth.ValueChanged += OnNumberChanged;
        PropFontSize.ValueChanged += OnNumberChanged;

        // 外观样式：不透明度数字框（此前无事件，修改不落盘）
        OpacityBox.ValueChanged += OnNumberChanged;

        // 值显示配置数字框
        ValueScaleBox.ValueChanged += OnNumberChanged;

        // 控件功能参数数字框（此前无事件，修改后切换控件即丢失）
        MinValueBox.ValueChanged += OnNumberChanged;
        MaxValueBox.ValueChanged += OnNumberChanged;
        StepValueBox.ValueChanged += OnNumberChanged;
        SelectedIndexBox.ValueChanged += OnNumberChanged;
        MaxLengthBox.ValueChanged += OnNumberChanged;
        TickCountBox.ValueChanged += OnNumberChanged;
        RowCountBox.ValueChanged += OnNumberChanged;
        ColCountBox.ValueChanged += OnNumberChanged;
        RowHeightBox.ValueChanged += OnNumberChanged;
        BrightnessBox.ValueChanged += OnNumberChanged;
        SpinSpeedBox.ValueChanged += OnNumberChanged;
        TankTickCountBox.ValueChanged += OnNumberChanged;
        RotationBox.ValueChanged += OnNumberChanged;
        PipeFlowSpeedBox.ValueChanged += OnNumberChanged;

        // 屏幕页面属性：数值输入
        ScreenWidth.ValueChanged += (_, _) => OnScreenNumberChanged();
        ScreenHeight.ValueChanged += (_, _) => OnScreenNumberChanged();
        ScreenDpi.ValueChanged += (_, _) => OnScreenNumberChanged();
        ScreenBgImageOpacity.ValueChanged += (_, _) => OnScreenNumberChanged();
        SafeLeft.ValueChanged += (_, _) => OnScreenNumberChanged();
        SafeTop.ValueChanged += (_, _) => OnScreenNumberChanged();
        SafeRight.ValueChanged += (_, _) => OnScreenNumberChanged();
        SafeBottom.ValueChanged += (_, _) => OnScreenNumberChanged();

        // R1-T4: 变量选择器（树形+搜索）
        VariablePicker.VariableSelected += OnVariableSelected;

        // 动作目标下拉框：根据动作类型填充变量/屏幕/脚本候选项
        // （SelectionChanged 的接线在 .axaml 里声明，这里不得再 += 一次 —— 缺陷 #43）

        // P1: 绑定模式变更 —— 刷新可用动作列表
        BindingModeCombo.SelectionChanged += OnBindingModeChanged;

        // I1: 表达式绑定 —— 绑定源切换 + 表达式实时校验
        BindingKindCombo.SelectionChanged += OnBindingKindChanged;
        ExpressionInput.TextChanged += OnExpressionInputChanged;
    }

    /// <summary>设置变量管理器引用（用于变量下拉列表和动作目标列表）。</summary>
    public void SetVariableManager(VariableManager? variableManager)
    {
        _variableManager = variableManager;
        VariablePicker.LoadVariables(variableManager);
    }

    /// <summary>设置当前项目引用（用于动作目标中的屏幕/脚本列表）。</summary>
    public void SetProject(HmiProject? project)
    {
        _currentProject = project;
    }

    /// <summary>设置模板管理器引用（用于 Faceplate 参数面板）。</summary>
    public void SetTemplateManager(TemplateManager? templateManager)
    {
        _templateManager = templateManager;
    }

    public void BindControl(HmiControl? control)
    {
        _currentControl = control;
        _isUpdating = true;
        _selectedActionIndex = 0;

        // 屏幕属性面板与控件属性面板互斥：未选中控件时显示当前屏幕属性
        ScreenPropsPanel.IsVisible = control == null;

        if (control == null)
        {
            InfoText.Text = CurrentScreen == null
                ? AppLocalization.Service["prop.no.selection"]
                : AppLocalization.Service.Format(Lang.PropScreen, CurrentScreen.Name);
            PropX.Value = 0; PropY.Value = 0; PropWidth.Value = 0; PropHeight.Value = 0;
            PropName.Text = ""; PropText.Text = "";
            PropBgColor.Text = ""; PropTextColor.Text = "";
            PropRadius.Value = 0; PropBorderWidth.Value = 0; PropBorderColor.Text = "";
            PropFontSize.Value = 14;
            PropTextAlign.SelectedIndex = 1;  // Center
            PropTextVAlign.SelectedIndex = 1; // Middle
            PropIsChecked.IsChecked = false;
            LblIsChecked.IsVisible = false;
            PropIsChecked.IsVisible = false;
            LblIndicatorColor.IsVisible = false;
            IndicatorColorBox.IsVisible = false;
            SwatchIndicatorColor.IsVisible = false;
            OpacityBox.Value = 100;
            FaceplateParamExpander.IsVisible = false;
            VisualStateExpander.IsVisible = false;
            ValueDisplayExpander.IsVisible = false;
            ControlParamExpander.IsVisible = false;

            // 参考 Figma/Sketch/XD：空白区域点击 → 属性栏切换到页面/屏幕属性
            if (CurrentScreen != null)
                BindScreen(CurrentScreen);
            else
                IsEnabled = false;
        }
        else
        {
            // ⚠ 名字显示的是**本地化别名**（默认名 `Button1` → `按钮1`），落盘的仍是中性名 ——
            //   写回路径在 PropertyPanelView.Changes.cs 里做左逆（登记 #61r，阶段 159）。
            // ⚠ 类型也走单一来源（`ControlTypeTextKeys`）：阶段 160 之前这里直显英文枚举名。
            InfoText.Text = string.IsNullOrEmpty(control.Name)
                    ? $"({AppLocalization.Service["general.untitled"]} {Services.ControlTypeTextKeys.Describe(control.Type)})"
                    : $"{Services.ControlNameDefaults.Display(control)} ({Services.ControlTypeTextKeys.Describe(control.Type)})";
            PropX.Value = control.X;
            PropY.Value = control.Y;
            PropWidth.Value = control.Width;
            PropHeight.Value = control.Height;
            PropName.Text = Services.ControlNameDefaults.Display(control);
            PropText.Text = control.Text ?? "";
            PropBgColor.Text = control.BgColor ?? "";
            PropTextColor.Text = control.TextColor ?? "";
            PropRadius.Value = control.Radius;
            PropBorderWidth.Value = control.BorderWidth;
            PropBorderColor.Text = control.BorderColor ?? "";

            // 字体 & 对齐
            PropFontSize.Value = control.FontSize > 0 ? control.FontSize : 14;
            PropTextAlign.SelectedIndex = control.TextAlign switch
            {
                TextAlignMode.Left => 0,
                TextAlignMode.Right => 2,
                _ => 1
            };
            PropTextVAlign.SelectedIndex = control.TextVAlign switch
            {
                TextVAlignMode.Top => 0,
                TextVAlignMode.Bottom => 2,
                _ => 1
            };
            PopulateFontFamilyCombo(control.FontFamily);

            // 布尔状态编辑器 —— 仅 CheckBox/Switch 显示
            bool supportsChecked = control.Type is HmiControlType.CheckBox
                or HmiControlType.Switch;
            PropIsChecked.IsChecked = control.IsChecked;
            LblIsChecked.IsVisible = supportsChecked;
            PropIsChecked.IsVisible = supportsChecked;

            // 指示器颜色（LV_PART_INDICATOR）—— Slider/Bar/Switch/Gauge/Knob
            // 以及过程图形控件（Tank 液位填充色 / Pipe 流动段色 / Valve 开态色）
            // I10: Arc 指示弧色
            bool supportsIndicator = control.Type is HmiControlType.Slider
                or HmiControlType.Bar or HmiControlType.Switch
                or HmiControlType.Gauge or HmiControlType.Knob
                or HmiControlType.Tank or HmiControlType.Pipe or HmiControlType.Valve
                or HmiControlType.Arc;
            LblIndicatorColor.IsVisible = supportsIndicator;
            IndicatorColorBox.IsVisible = supportsIndicator;
            SwatchIndicatorColor.IsVisible = supportsIndicator;
            IndicatorColorBox.Text = control.IndicatorColor ?? "";

            // 不透明度（通用）
            OpacityBox.Value = control.Opacity;

            IsEnabled = true;

            // 按控件类型刷新动作列表
            PopulateActions(control.Type);

            // 加载变量绑定
            LoadVariableBinding(control);

            // 加载动作
            LoadAction(control);

            // 加载 Faceplate 参数面板
            LoadFaceplateParams(control);

            // 加载状态样式（Button/Switch/CheckBox 可见）
            LoadVisualStates(control);

            // 加载值显示配置（Label 等显示型控件可见）
            // 过程图形控件也支持：Tank 液位 / Valve 开度 / Pipe 流量百分比
            bool supportsValueDisplay = control.Type is HmiControlType.Label
                or HmiControlType.Gauge or HmiControlType.Bar or HmiControlType.Knob
                or HmiControlType.Led or HmiControlType.Slider or HmiControlType.CheckBox
                or HmiControlType.Switch or HmiControlType.Dropdown
                or HmiControlType.Tank or HmiControlType.Pipe or HmiControlType.Valve;
            ValueDisplayExpander.IsVisible = supportsValueDisplay;
            if (supportsValueDisplay)
                LoadValueDisplayFields(control);

            // 加载控件功能参数（按类型显示相关行）
            LoadControlParameters(control);
        }

        _isUpdating = false;

        if (control != null)
            UpdateColorSwatches();
        else if (CurrentScreen != null)
            UpdateScreenSwatches();
    }
}
