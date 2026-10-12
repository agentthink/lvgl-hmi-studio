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
    // ── 状态化样式（完整四态：Normal/Pressed/Checked/Disabled）──

    /// <summary>加载控件的状态样式到编辑器：填充图片与对齐下拉，恢复当前编辑态的字段值。</summary>
    private void LoadVisualStates(HmiControl control)
    {
        PopulateVisualStateImageItems();
        PopulateAlignCombos(isNormal: true);

        // 恢复状态选择到 Normal
        CboVisualState.SelectedIndex = 0;
        _currentEditingState = VisualState.Normal;
        LoadVisualStateFields(control, _currentEditingState);
    }

    // ── 值显示配置（Label 等显示型控件）──

    /// <summary>加载控件的值显示配置：值类型下拉、缩放比例、前后缀。</summary>
    private void LoadValueDisplayFields(HmiControl control)
    {
        // 值类型下拉（按 Tag 匹配）
        var displayTag = control.ValueDisplayType.ToString();
        foreach (var item in ValueDisplayTypeCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), displayTag, StringComparison.Ordinal))
            {
                ValueDisplayTypeCombo.SelectedItem = item;
                break;
            }
        }

        ValueScaleBox.Value = control.ValueScaleFactor;
        ValuePrefixBox.Text = control.ValuePrefix ?? "";
        ValueSuffixBox.Text = control.ValueSuffix ?? "";

        // 小数位数下拉（0-4 位，clamp 到选项范围）
        var decimals = Math.Clamp(control.ValueDecimalPlaces, 0, 4);
        foreach (var item in ValueDecimalsCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), decimals.ToString(), StringComparison.Ordinal))
            {
                ValueDecimalsCombo.SelectedItem = item;
                break;
            }
        }
        UpdateValueDecimalsVisibility();
    }

    /// <summary>小数位数行仅在值类型为 Float 时显示。</summary>
    private void UpdateValueDecimalsVisibility()
    {
        var isFloat = ParseValueDisplayType(ValueDisplayTypeCombo.SelectedItem as ComboBoxItem)
            == ValueDisplayType.Float;
        LblValueDecimals.IsVisible = isFloat;
        ValueDecimalsCombo.IsVisible = isFloat;
    }

    /// <summary>值类型切换时联动小数位数行的可见性。</summary>
    private void OnValueDisplayTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        // XAML 初始化期 SelectedIndex 赋值会先于后续控件创建触发本事件，需判空
        if (LblValueDecimals == null || ValueDecimalsCombo == null) return;
        UpdateValueDecimalsVisibility();
    }

    /// <summary>解析小数位数下拉的选中项。</summary>
    private static int ParseValueDecimalPlaces(ComboBoxItem? item)
    {
        if (item?.Tag is string tag && int.TryParse(tag, out var n))
            return Math.Clamp(n, 0, 6);
        return 2;
    }

    /// <summary>解析值类型下拉的选中项为枚举。</summary>
    private static ValueDisplayType ParseValueDisplayType(ComboBoxItem? item)
    {
        var tag = item?.Tag?.ToString();
        return Enum.TryParse<ValueDisplayType>(tag, out var type) ? type : ValueDisplayType.Auto;
    }
}
