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
    // ── 控件功能参数（按控件类型显隐）──

    /// <summary>加载控件的功能参数：按类型显示相关行并恢复字段值。</summary>
    private void LoadControlParameters(HmiControl control)
    {
        // 先全部隐藏
        SetParamRow(LblMinValue, MinValueBox, false);
        SetParamRow(LblMaxValue, MaxValueBox, false);
        SetParamRow(LblStepValue, StepValueBox, false);
        SetParamRow(LblOptions, OptionsBox, false);
        SetParamRow(LblSelectedIndex, SelectedIndexBox, false);
        SetParamRow(LblPlaceholder, PlaceholderBox, false);
        SetParamRow(LblMaxLength, MaxLengthBox, false);
        SetParamRow(LblPassword, PasswordCheck, false);
        SetParamRow(LblTickCount, TickCountBox, false);
        SetParamRow(LblRowCount, RowCountBox, false);
        SetParamRow(LblColCount, ColCountBox, false);
        SetParamRow(LblHeader, HeaderBox, false);
        SetParamRow(LblTabTitles, TabTitlesBox, false);
        SetParamRow(LblStretch, StretchCombo, false);
        SetParamRow(LblBrightness, BrightnessBox, false);
        SetParamRow(LblSpinSpeed, SpinSpeedBox, false);
        SetParamRow(LblLineCap, LineCapCombo, false);
        SetParamRow(LblKeyboardMode, KeyboardModeCombo, false);
        SetParamRow(LblValueMode, ValueModeCombo, false);
        SetParamRow(LblLongMode, LongModeCombo, false);
        SetParamRow(LblImageAsset, ImageAssetCombo, false);
        SetParamRow(LblSvgAsset, SvgAssetCombo, false);
        // I7: 过程图形控件专属字段
        SetParamRow(LblTankShowScale, TankShowScaleCheck, false);
        SetParamRow(LblTankTickCount, TankTickCountBox, false);
        SetParamRow(LblPipeDirection, PipeDirectionCombo, false);
        SetParamRow(LblPipeFlowDirection, PipeFlowDirectionCombo, false);
        SetParamRow(LblPipeFlowSpeed, PipeFlowSpeedBox, false);
        SetParamRow(LblValveType, ValveTypeCombo, false);

        bool anyVisible = true;
        switch (control.Type)
        {
            case HmiControlType.Slider:
                SetParamRow(LblMinValue, MinValueBox, true);
                SetParamRow(LblMaxValue, MaxValueBox, true);
                SetParamRow(LblStepValue, StepValueBox, true);
                SetParamRow(LblValueMode, ValueModeCombo, true);
                SetParamRow(LblShowTicks, ShowTicksCheck, true);
                SetParamRow(LblShowPointer, ShowPointerCheck, true);
                break;
            case HmiControlType.Bar:
                SetParamRow(LblMinValue, MinValueBox, true);
                SetParamRow(LblMaxValue, MaxValueBox, true);
                SetParamRow(LblValueMode, ValueModeCombo, true);
                SetParamRow(LblShowTicks, ShowTicksCheck, true);
                SetParamRow(LblShowPointer, ShowPointerCheck, true);
                break;
            case HmiControlType.Label:
                SetParamRow(LblLongMode, LongModeCombo, true);
                break;
            case HmiControlType.Gauge:
                SetParamRow(LblMinValue, MinValueBox, true);
                SetParamRow(LblMaxValue, MaxValueBox, true);
                SetParamRow(LblTickCount, TickCountBox, true);
                break;
            case HmiControlType.Knob:
                SetParamRow(LblMinValue, MinValueBox, true);
                SetParamRow(LblMaxValue, MaxValueBox, true);
                break;
            case HmiControlType.Dropdown:
                SetParamRow(LblOptions, OptionsBox, true);
                SetParamRow(LblSelectedIndex, SelectedIndexBox, true);
                break;
            case HmiControlType.TextArea:
                SetParamRow(LblPlaceholder, PlaceholderBox, true);
                SetParamRow(LblMaxLength, MaxLengthBox, true);
                SetParamRow(LblPassword, PasswordCheck, true);
                break;
            case HmiControlType.Table:
                SetParamRow(LblRowCount, RowCountBox, true);
                SetParamRow(LblColCount, ColCountBox, true);
                SetParamRow(LblHeader, HeaderBox, true);
                SetParamRow(LblRowHeight, RowHeightBox, true);
                SetParamRow(LblColWidths, ColWidthsBox, true);
                EditCellsBtn.IsVisible = true;
                break;
            case HmiControlType.TabView:
                SetParamRow(LblTabTitles, TabTitlesBox, true);
                SetParamRow(LblSelectedIndex, SelectedIndexBox, true);
                break;
            case HmiControlType.Image:
                SetParamRow(LblStretch, StretchCombo, true);
                SetParamRow(LblImageAsset, ImageAssetCombo, true);
                PopulateImageCombo(control.ImageAssetId);
                break;
            case HmiControlType.Svg:
                SetParamRow(LblSvgAsset, SvgAssetCombo, true);
                PopulateSvgCombo();
                break;
            case HmiControlType.Led:
                SetParamRow(LblBrightness, BrightnessBox, true);
                break;
            case HmiControlType.Spinner:
                SetParamRow(LblSpinSpeed, SpinSpeedBox, true);
                break;
            case HmiControlType.Line:
                SetParamRow(LblLineCap, LineCapCombo, true);
                break;
            case HmiControlType.Keyboard:
                SetParamRow(LblKeyboardMode, KeyboardModeCombo, true);
                break;
            // I7: 过程图形控件
            case HmiControlType.Tank:
                SetParamRow(LblMinValue, MinValueBox, true);
                SetParamRow(LblMaxValue, MaxValueBox, true);
                SetParamRow(LblTankShowScale, TankShowScaleCheck, true);
                SetParamRow(LblTankTickCount, TankTickCountBox, true);
                break;
            case HmiControlType.Pipe:
                SetParamRow(LblPipeDirection, PipeDirectionCombo, true);
                SetParamRow(LblPipeFlowDirection, PipeFlowDirectionCombo, true);
                SetParamRow(LblPipeFlowSpeed, PipeFlowSpeedBox, true);
                break;
            case HmiControlType.Valve:
                SetParamRow(LblMinValue, MinValueBox, true);
                SetParamRow(LblMaxValue, MaxValueBox, true);
                SetParamRow(LblValveType, ValveTypeCombo, true);
                break;
            // I10: 基础绘图图形
            case HmiControlType.Arc:
                // 圆弧角度范围：MinValue/MaxValue 作为起始/结束角（度），指示弧进度取 Value
                SetParamRow(LblMinValue, MinValueBox, true);
                SetParamRow(LblMaxValue, MaxValueBox, true);
                LblMinValue.Text = AppLocalization.Service[Lang.ArcStartAngle];
                LblMaxValue.Text = AppLocalization.Service[Lang.ArcEndAngle];
                break;
            default:
                anyVisible = false;
                break;
        }

        // I10: 非 Arc 类型恢复 Min/Max 行常规标签（避免上一种类型切换遗留的角度标签）
        if (control.Type != HmiControlType.Arc)
        {
            LblMinValue.Text = AppLocalization.Service[Lang.PropParamMin];
            LblMaxValue.Text = AppLocalization.Service[Lang.PropParamMax];
        }

        // 🩸 阶段 220（#96 的"统一可见性"那半）：**字体族/字号/水平对齐三行只对文本类控件显示**。
        //   此前它们对所有类型都显示 ⇒ 形状/Led/Slider 之类改了既不进导出、预览也看不出效果
        //   （用户会以为"设置丢了"）。名单与 `CodeGenerator.Screen.cs` 的文本块一致：
        //   Label / Button / CheckBox / TextArea（＋ Dropdown 在 `EmitDropdownParameters` 里发对齐）。
        var textBearing = control.Type is HmiControlType.Label or HmiControlType.Button
            or HmiControlType.CheckBox or HmiControlType.TextArea or HmiControlType.Dropdown;
        SetParamRow(LblFontFamily, PropFontFamily, textBearing);
        SetParamRow(LblFontSize, PropFontSize, textBearing);
        SetParamRow(LblTextAlign, PropTextAlign, textBearing);

        // 旋转/镜像：所有控件通用（旋转角度 + 方向 + 水平/垂直镜像 + 重置）
        SetParamRow(LblRotation, RotationBox, true);
        SetParamRow(LblRotationDir, RotationDirCombo, true);
        SetParamRow(LblMirrorH, MirrorHCheck, true);
        SetParamRow(LblMirrorV, MirrorVCheck, true);
        BtnResetRotation.IsVisible = true;
        anyVisible = true;   // 保证参数区展开以显示变换设置

        ControlParamExpander.IsVisible = anyVisible;
        if (!anyVisible) return;

        // 恢复字段值
        RotationBox.Value = control.Rotation;
        SelectComboByTag(RotationDirCombo, control.RotationDirection, 0);
        MirrorHCheck.IsChecked = control.MirrorX;
        MirrorVCheck.IsChecked = control.MirrorY;
        MinValueBox.Value = control.MinValue;
        MaxValueBox.Value = control.MaxValue;
        StepValueBox.Value = control.StepValue;
        SelectedIndexBox.Value = control.SelectedIndex;
        OptionsBox.Text = control.Options ?? "";
        PlaceholderBox.Text = control.PlaceholderText ?? "";
        MaxLengthBox.Value = control.MaxLength;
        PasswordCheck.IsChecked = control.PasswordMode;
        TickCountBox.Value = control.MajorTickCount;
        RowCountBox.Value = control.RowCount;
        ColCountBox.Value = control.ColumnCount;
        HeaderBox.Text = control.HeaderTexts ?? "";
        RowHeightBox.Value = control.RowHeight;
        ColWidthsBox.Text = control.ColumnWidths ?? "";
        TabTitlesBox.Text = control.TabTitles ?? "";
        SelectComboByTag(StretchCombo, control.StretchMode, 1);
        BrightnessBox.Value = control.Brightness;
        ShowTicksCheck.IsChecked = control.ShowTicks;
        ShowPointerCheck.IsChecked = control.EffectiveShowPointer();
        SpinSpeedBox.Value = control.SpinSpeed;
        SelectComboByTag(LineCapCombo, control.LineCap, 0);
        SelectComboByTag(KeyboardModeCombo, control.KeyboardMode, 0);
        SelectComboByTag(ValueModeCombo, control.ValueMode, 0);
        SelectComboByTag(LongModeCombo, control.LabelLongMode, 0);

        // I7: 过程图形控件专属字段恢复
        TankShowScaleCheck.IsChecked = control.TankShowScale;
        TankTickCountBox.Value = control.TankTickCount;
        SelectComboByTag(PipeDirectionCombo, control.PipeDirection, 0);
        SelectComboByTag(PipeFlowDirectionCombo, control.PipeFlowDirection, 0);
        PipeFlowSpeedBox.Value = control.PipeFlowSpeed;
        SelectComboByTag(ValveTypeCombo, control.ValveType, 0);
    }

    /// <summary>成对显隐参数行（标签 + 输入控件）。</summary>
    private static void SetParamRow(Control label, Control input, bool visible)
    {
        label.IsVisible = visible;
        input.IsVisible = visible;
    }

    /// <summary>重置旋转角度与镜像（角度归零、镜像关闭），即时预览并通知落盘。</summary>
    private void OnResetRotationClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentControl == null) return;
        _currentControl.Rotation = 0;
        _currentControl.RotationDirection = null;
        _currentControl.MirrorX = false;
        _currentControl.MirrorY = false;
        RotationBox.Value = 0;
        RotationDirCombo.SelectedIndex = 0;
        MirrorHCheck.IsChecked = false;
        MirrorVCheck.IsChecked = false;
        PropertyChanged?.Invoke(this, _currentControl);
    }

    /// <summary>
    /// 打开表格单元格编辑器；编辑完成后通知画布重绘并落盘。
    /// 对话框异常由 UiOperationGuard 兜底（本视图没有状态行，回落到主窗口状态栏）。
    /// </summary>
    private void OnEditCellsClicked(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpEditGridCell, async () =>
        {
            if (_currentControl == null) return;
            if (TopLevel.GetTopLevel(this) is not Window owner) return;

            var variables = _variableManager?.Variables?.ToList() ?? new List<Variable>();
            var dialog = new TableCellEditorDialog(_currentControl, variables);
            await dialog.ShowDialog(owner);

            // 与 ImageAsset/SvgAsset 编辑一致：通知 MainWindow 重绘画布 + 写入撤销栈
            PropertyChanged?.Invoke(this, _currentControl);
        });

    /// <summary>按 Tag 选择 ComboBox 项，未匹配时回退到指定索引。</summary>
    private static void SelectComboByTag(ComboBox combo, string? tag, int fallbackIndex)
    {
        var index = -1;
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }
        combo.SelectedIndex = index >= 0 ? index : fallbackIndex;
    }

    /// <summary>读取 ComboBox 选中项的 Tag。</summary>
    private static string? GetComboTag(ComboBox combo)
        => (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
}
