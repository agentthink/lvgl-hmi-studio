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
    // ── 属性变更 ──

    private void OnPropertyChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdating || _currentControl == null) return;
        // 防御：Dock 重组期间控件可能已被卸载，延迟的 TextChange 事件不应继续处理
        if (Parent == null) return;
        SaveProperties();
    }

    private void OnNumberChanged(object? sender, FANumberBoxValueChangedEventArgs e)
    {
        if (_isUpdating || _currentControl == null) return;
        if (Parent == null) return;
        SaveProperties();
    }

    /// <summary>
    /// P1-3: 校验属性值后写回 —— CustomValidator 返回错误消息则跳过写回（保留原值）并记录日志。
    /// 校验通过后执行 writeAction 写回模型。
    /// </summary>
    /// <summary>
    /// 校验通过后写回模型。**面板编辑器的所有写回都必须走这里** —— 否则可插拔的
    /// <see cref="IPropertyValidator"/> 会变成"部分属性有效"，插件作者无法预测
    /// 自己的规则何时被调用（P1-3 的完整语义）。
    ///
    /// <para>注意区分两类写入：编辑器改动（本方法）与**程序化命令**（重置变换、
    /// 选择图片资源、切换绑定模式等）。后者是用户明确下达的指令，不该被属性校验器
    /// 静默否决，因此仍直接写模型。</para>
    /// </summary>
    private void ValidateAndWrite<T>(string propertyName, T value, Action<T> writeAction)
    {
        if (_currentControl == null) return;
        var message = CustomValidator?.Validate(_currentControl, propertyName, value);
        if (message != null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[PropertyPanel] 属性校验未通过 {propertyName}: {message}（保留原值）");
            return;
        }
        writeAction(value);
    }

    /// <summary>从所有编辑器控件读取值并写回当前选中的 HmiControl。</summary>
    private void SaveProperties()
    {
        if (_currentControl == null) return;

        // P1-3: 可注入即时校验 —— 对易越界/易出格式错误的属性先校验，失败则跳过写回（保留原值）
        // 校验通过后仍走静态 PropertyValidator 钳制（合法性兜底）。
        // ⚠ 名字再走一次 ControlNameDefaults.ToPersisted：面板里显示的是**本地化别名**
        //   （`按钮1`），落盘的必须是**中性名**（`Button1`）—— 否则"照着自己看到的名字改一遍"
        //   会把工程写回成随语言变的形状（登记 #61r，阶段 159）。
        ValidateAndWrite(nameof(HmiControl.Name), PropName.Text, v =>
            _currentControl.Name = Services.ControlNameDefaults.ToPersisted(
                PropertyValidator.SanitizeName(v ?? "", _currentControl.Name), _currentControl.Type));
        ValidateAndWrite(nameof(HmiControl.BgColor), PropBgColor.Text, v =>
            _currentControl.BgColor = PropertyValidator.SanitizeColor(v));
        ValidateAndWrite(nameof(HmiControl.TextColor), PropTextColor.Text, v =>
            _currentControl.TextColor = PropertyValidator.SanitizeColor(v));
        ValidateAndWrite(nameof(HmiControl.BorderColor), PropBorderColor.Text, v =>
            _currentControl.BorderColor = PropertyValidator.SanitizeColor(v));
        ValidateAndWrite(nameof(HmiControl.IndicatorColor), IndicatorColorBox.Text, v =>
            _currentControl.IndicatorColor = PropertyValidator.SanitizeColor(v));

        // FF6: 所有属性值通过 PropertyValidator 验证后再写回模型
        ValidateAndWrite(nameof(HmiControl.X), PropertyValidator.ClampPosition(PropX.Value), v => _currentControl.X = v);
        ValidateAndWrite(nameof(HmiControl.Y), PropertyValidator.ClampPosition(PropY.Value), v => _currentControl.Y = v);
        ValidateAndWrite(nameof(HmiControl.Width), PropertyValidator.ClampSize(PropWidth.Value), v => _currentControl.Width = v);
        ValidateAndWrite(nameof(HmiControl.Height), PropertyValidator.ClampSize(PropHeight.Value), v => _currentControl.Height = v);
        ValidateAndWrite(nameof(HmiControl.Text), string.IsNullOrEmpty(PropText.Text) ? null : PropText.Text, v => _currentControl.Text = v);
        ValidateAndWrite(nameof(HmiControl.Radius), PropertyValidator.ClampRadius((int)PropRadius.Value), v => _currentControl.Radius = v);
        ValidateAndWrite(nameof(HmiControl.BorderWidth), PropertyValidator.ClampBorderWidth((int)PropBorderWidth.Value), v => _currentControl.BorderWidth = v);
        ValidateAndWrite(nameof(HmiControl.IsChecked), PropIsChecked.IsChecked ?? false, v => _currentControl.IsChecked = v);

        // 字体 & 对齐
        ValidateAndWrite(nameof(HmiControl.FontSize), (int)PropFontSize.Value, v =>
            _currentControl.FontSize = v);
        if (PropTextAlign.SelectedItem is ComboBoxItem ci_h && ci_h.Tag is string tagH)
            ValidateAndWrite(nameof(HmiControl.TextAlign), Enum.Parse<TextAlignMode>(tagH), v => _currentControl.TextAlign = v);
        if (PropTextVAlign.SelectedItem is ComboBoxItem ci_v && ci_v.Tag is string tagV)
            ValidateAndWrite(nameof(HmiControl.TextVAlign), Enum.Parse<TextVAlignMode>(tagV), v => _currentControl.TextVAlign = v);
        ValidateAndWrite(nameof(HmiControl.FontFamily), PropFontFamily.SelectedItem is ComboBoxItem ci_f ? (ci_f.Tag as string) : null, v => _currentControl.FontFamily = v);
        // 指示器颜色（仅显示型控件加载）与不透明度（通用）
        ValidateAndWrite(nameof(HmiControl.Opacity), OpacityBox.Value, v =>
            _currentControl.Opacity = (double)v!);

        // 值显示配置（仅显示型控件加载，面板可见时保存）
        if (ValueDisplayExpander.IsVisible)
        {
            ValidateAndWrite(nameof(HmiControl.ValueDisplayType), ParseValueDisplayType( ValueDisplayTypeCombo.SelectedItem as ComboBoxItem), v => _currentControl.ValueDisplayType = v);
            ValidateAndWrite(nameof(HmiControl.ValueScaleFactor), ValueScaleBox.Value, v => _currentControl.ValueScaleFactor = v);
            ValidateAndWrite(nameof(HmiControl.ValueDecimalPlaces), ParseValueDecimalPlaces( ValueDecimalsCombo.SelectedItem as ComboBoxItem), v => _currentControl.ValueDecimalPlaces = v);
            ValidateAndWrite(nameof(HmiControl.ValuePrefix), string.IsNullOrEmpty(ValuePrefixBox.Text) ? null : ValuePrefixBox.Text, v => _currentControl.ValuePrefix = v);
            ValidateAndWrite(nameof(HmiControl.ValueSuffix), string.IsNullOrEmpty(ValueSuffixBox.Text) ? null : ValueSuffixBox.Text, v => _currentControl.ValueSuffix = v);
        }

        // 控件功能参数（仅显示型面板加载，Expander 可见时保存）
        if (ControlParamExpander.IsVisible)
        {
            ValidateAndWrite(nameof(HmiControl.MinValue), MinValueBox.Value, v => _currentControl.MinValue = v);
            ValidateAndWrite(nameof(HmiControl.MaxValue), MaxValueBox.Value, v => _currentControl.MaxValue = v);
            ValidateAndWrite(nameof(HmiControl.StepValue), StepValueBox.Value, v => _currentControl.StepValue = v);
            ValidateAndWrite(nameof(HmiControl.SelectedIndex), (int)SelectedIndexBox.Value, v => _currentControl.SelectedIndex = v);
            ValidateAndWrite(nameof(HmiControl.Options), string.IsNullOrWhiteSpace(OptionsBox.Text) ? null : OptionsBox.Text.Replace("\r\n", "\n"), v => _currentControl.Options = v);
            ValidateAndWrite(nameof(HmiControl.PlaceholderText), string.IsNullOrEmpty(PlaceholderBox.Text) ? null : PlaceholderBox.Text, v => _currentControl.PlaceholderText = v);
            ValidateAndWrite(nameof(HmiControl.MaxLength), (int)MaxLengthBox.Value, v => _currentControl.MaxLength = v);
            ValidateAndWrite(nameof(HmiControl.PasswordMode), PasswordCheck.IsChecked ?? false, v => _currentControl.PasswordMode = v);
            ValidateAndWrite(nameof(HmiControl.MajorTickCount), (int)TickCountBox.Value, v => _currentControl.MajorTickCount = v);
            ValidateAndWrite(nameof(HmiControl.RowCount), (int)RowCountBox.Value, v => _currentControl.RowCount = v);
            ValidateAndWrite(nameof(HmiControl.ColumnCount), (int)ColCountBox.Value, v => _currentControl.ColumnCount = v);
            ValidateAndWrite(nameof(HmiControl.HeaderTexts), string.IsNullOrWhiteSpace(HeaderBox.Text) ? null : HeaderBox.Text.Trim(), v => _currentControl.HeaderTexts = v);
            ValidateAndWrite(nameof(HmiControl.RowHeight), RowHeightBox.Value, v => _currentControl.RowHeight = v);
            ValidateAndWrite(nameof(HmiControl.ColumnWidths), string.IsNullOrWhiteSpace(ColWidthsBox.Text) ? null : ColWidthsBox.Text.Trim(), v => _currentControl.ColumnWidths = v);
            ValidateAndWrite(nameof(HmiControl.TabTitles), string.IsNullOrWhiteSpace(TabTitlesBox.Text) ? null : TabTitlesBox.Text.Trim(), v => _currentControl.TabTitles = v);
            // StretchMode 仅对图片控件有意义：下拉框隐藏（非图片控件）时不写，避免污染模型
            if (StretchCombo.IsVisible)
                ValidateAndWrite(nameof(HmiControl.StretchMode), GetComboTag(StretchCombo), v => _currentControl.StretchMode = v);
            ValidateAndWrite(nameof(HmiControl.Brightness), (int)BrightnessBox.Value, v => _currentControl.Brightness = v);
            // 显示刻度/指针（仅 Slider/Bar 面板可见时保存；ShowPointer 空值语义=按类型默认，勾选写 true、取消写 false）
            if (ShowTicksCheck.IsVisible)
            {
                ValidateAndWrite(nameof(HmiControl.ShowTicks), ShowTicksCheck.IsChecked ?? false, v => _currentControl.ShowTicks = v);
                ValidateAndWrite(nameof(HmiControl.ShowPointer), ShowPointerCheck.IsChecked ?? false, v => _currentControl.ShowPointer = v);
            }
            // 旋转/镜像（所有控件通用）
            ValidateAndWrite(nameof(HmiControl.Rotation), RotationBox.Value, v => _currentControl.Rotation = v);
            ValidateAndWrite(nameof(HmiControl.RotationDirection), GetComboTag(RotationDirCombo), v => _currentControl.RotationDirection = v);
            ValidateAndWrite(nameof(HmiControl.MirrorX), MirrorHCheck.IsChecked ?? false, v => _currentControl.MirrorX = v);
            ValidateAndWrite(nameof(HmiControl.MirrorY), MirrorVCheck.IsChecked ?? false, v => _currentControl.MirrorY = v);
            ValidateAndWrite(nameof(HmiControl.SpinSpeed), (int)SpinSpeedBox.Value, v => _currentControl.SpinSpeed = v);
            ValidateAndWrite(nameof(HmiControl.LineCap), GetComboTag(LineCapCombo), v => _currentControl.LineCap = v);
            ValidateAndWrite(nameof(HmiControl.KeyboardMode), GetComboTag(KeyboardModeCombo), v => _currentControl.KeyboardMode = v);
            ValidateAndWrite(nameof(HmiControl.ValueMode), GetComboTag(ValueModeCombo), v => _currentControl.ValueMode = v);
            ValidateAndWrite(nameof(HmiControl.LabelLongMode), GetComboTag(LongModeCombo), v => _currentControl.LabelLongMode = v);
            if (ImageAssetCombo.IsVisible)
                ValidateAndWrite(nameof(HmiControl.ImageAssetId), GetComboTag(ImageAssetCombo), v => _currentControl.ImageAssetId = v);
            // I7: 过程图形控件专属字段
            if (TankShowScaleCheck.IsVisible)
                ValidateAndWrite(nameof(HmiControl.TankShowScale), TankShowScaleCheck.IsChecked ?? false, v => _currentControl.TankShowScale = v);
            if (TankTickCountBox.IsVisible)
                ValidateAndWrite(nameof(HmiControl.TankTickCount), (int)TankTickCountBox.Value, v => _currentControl.TankTickCount = v);
            if (PipeDirectionCombo.IsVisible)
                ValidateAndWrite(nameof(HmiControl.PipeDirection), GetComboTag(PipeDirectionCombo), v => _currentControl.PipeDirection = v);
            if (PipeFlowDirectionCombo.IsVisible)
                ValidateAndWrite(nameof(HmiControl.PipeFlowDirection), GetComboTag(PipeFlowDirectionCombo), v => _currentControl.PipeFlowDirection = v);
            if (PipeFlowSpeedBox.IsVisible)
                ValidateAndWrite(nameof(HmiControl.PipeFlowSpeed), (int)PipeFlowSpeedBox.Value, v => _currentControl.PipeFlowSpeed = v);
            if (ValveTypeCombo.IsVisible)
                ValidateAndWrite(nameof(HmiControl.ValveType), GetComboTag(ValveTypeCombo), v => _currentControl.ValveType = v);
        }

        SaveAction();

        // 状态面板 Normal 态的文本与内容区文本保持同步（同一字段 control.Text）
        // 防御：ViewLocator 工厂重建时 VsText 可能尚未初始化
        if (VsText != null && _currentEditingState == VisualState.Normal && VsText.Text != (_currentControl.Text ?? ""))
        {
            _isUpdating = true;
            try { VsText.Text = _currentControl.Text ?? ""; }
            finally { _isUpdating = false; }
        }
    }

    // ── 颜色选择器 ──────────────────────────────────────

    private void OnColorSwatchClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Border swatch && swatch.Tag is string propName)
        {
            _activeColorField = propName;
            var currentColor = propName switch
            {
                "PropBgColor" => PropBgColor.Text ?? "",
                "PropTextColor" => PropTextColor.Text ?? "",
                "PropBorderColor" => PropBorderColor.Text ?? "",
                "IndicatorColor" => IndicatorColorBox.Text ?? "",
                "VsBgColor" => VsBgColor.Text ?? "",
                "VsTextColor" => VsTextColor.Text ?? "",
                "VsBorderColor" => VsBorderColor.Text ?? "",
                "ScreenBgColor" => ScreenBgColor.Text ?? "",
                _ => ""
            };

            _colorPicker.ColorSelected -= OnColorPickerSelected;
            _colorPicker.ColorSelected += OnColorPickerSelected;
            _colorPicker.Open(swatch, currentColor);
        }
    }

    private void OnColorPickerSelected(object? sender, string hex)
    {
        switch (_activeColorField)
        {
            case "PropBgColor":
                PropBgColor.Text = hex;
                break;
            case "PropTextColor":
                PropTextColor.Text = hex;
                break;
            case "PropBorderColor":
                PropBorderColor.Text = hex;
                break;
            case "IndicatorColor":
                IndicatorColorBox.Text = hex;
                break;
            case "VsBgColor":
                VsBgColor.Text = hex;
                UpdateVisualStateSwatches();
                SaveVisualState(_currentControl!, _currentEditingState);
                break;
            case "VsTextColor":
                VsTextColor.Text = hex;
                UpdateVisualStateSwatches();
                SaveVisualState(_currentControl!, _currentEditingState);
                break;
            case "VsBorderColor":
                VsBorderColor.Text = hex;
                UpdateVisualStateSwatches();
                SaveVisualState(_currentControl!, _currentEditingState);
                break;
            case "ScreenBgColor":
                ScreenBgColor.Text = hex;
                UpdateScreenSwatches();
                break;
        }

        UpdateColorSwatches();
        _activeColorField = null;

        // 触发属性变更
        if (_currentControl != null)
            OnPropertyChanged(null, null!);
        else if (CurrentScreen != null)
            SaveScreenProperties();
    }

    private void UpdateColorSwatches()
    {
        try
        {
            if (!string.IsNullOrEmpty(PropBgColor.Text))
            {
                var hex = PropBgColor.Text.StartsWith('#') ? PropBgColor.Text : "#" + PropBgColor.Text;
                SwatchBgColor.Background = new SolidColorBrush(Color.Parse(hex));
            }

            if (!string.IsNullOrEmpty(PropTextColor.Text))
            {
                var hex = PropTextColor.Text.StartsWith('#') ? PropTextColor.Text : "#" + PropTextColor.Text;
                SwatchTextColor.Background = new SolidColorBrush(Color.Parse(hex));
            }

            if (!string.IsNullOrEmpty(PropBorderColor.Text))
            {
                var hex = PropBorderColor.Text.StartsWith('#') ? PropBorderColor.Text : "#" + PropBorderColor.Text;
                SwatchBorderColor.Background = new SolidColorBrush(Color.Parse(hex));
            }

            if (!string.IsNullOrEmpty(IndicatorColorBox.Text))
            {
                var hex = IndicatorColorBox.Text.StartsWith('#') ? IndicatorColorBox.Text : "#" + IndicatorColorBox.Text;
                SwatchIndicatorColor.Background = new SolidColorBrush(Color.Parse(hex));
            }
        }
        catch
        {
            // 忽略无效颜色值
        }
    }
}
