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
    // ── 变量绑定（联动模型）──

    /// <summary>值属性（历史默认）：没有显式选择目标属性时用它。</summary>
    private const string DefaultBindingPropertyName = "Text";

    /// <summary>
    /// 当前「目标属性」下拉选中的属性名。绑定条目按它分开存放，
    /// 因此同一控件可以同时绑 <c>Text</c> 与 <c>TextColor</c> 而不互相覆盖
    /// （此前两处写入都硬编码 <c>PropertyName = "Text"</c>，多属性绑定只能靠手改工程文件）。
    /// </summary>
    private string CurrentBindingPropertyName
        => TargetPropertyCombo.SelectedItem is ComboBoxItem item && item.Tag?.ToString() is { Length: > 0 } tag
            ? tag
            : DefaultBindingPropertyName;

    private void LoadVariableBinding(HmiControl control)
    {
        // 目标属性：跟第一条读绑定的属性名走（不在下拉里则回落默认值属性）。
        // 必须在读取绑定内容**之前**完成 —— 否则会拿默认属性的绑定去填另一个属性的界面。
        var firstRead = control.VariableBindings.FirstOrDefault(b => b.Direction != BindingDirection.WriteOnly);
        SelectTargetProperty(firstRead?.PropertyName);
        var targetBinding = FindReadBinding(control, CurrentBindingPropertyName);

        // 设置当前绑定值（Picker 每次打开时自行刷新变量列表，无需预填充）
        VariablePicker.SelectedVariableName = targetBinding?.VariableId ?? control.PrimaryBindingVariable;

        // P2: 按控件类型过滤可选变量数据类型
        VariablePicker.FilterControlType = control.Type;

        // AI: 控件名 → 智能绑定推荐上下文
        VariablePicker.RecommendationContextName = control.Name;

        // I1: 绑定源（变量/表达式）—— 按「当前目标属性」那条绑定的 Kind 恢复
        var primaryBinding = targetBinding;
        var bindingKind = primaryBinding?.Kind ?? BindingSourceKind.Variable;
        for (int i = 0; i < BindingKindCombo.Items.Count; i++)
        {
            if (BindingKindCombo.Items[i] is ComboBoxItem cbi &&
                string.Equals(cbi.Tag?.ToString(), bindingKind.ToString(), StringComparison.Ordinal))
            {
                BindingKindCombo.SelectedIndex = i;
                break;
            }
        }
        UpdateBindingKindVisibility();
        if (bindingKind == BindingSourceKind.Expression)
            ExpressionInput.Text = primaryBinding?.Expression ?? "";
        else
            ExpressionInput.Text = "";

        // 绑定模式（全局默认）
        for (int i = 0; i < BindingModeCombo.Items.Count; i++)
        {
            if (BindingModeCombo.Items[i] is ComboBoxItem cbi &&
                cbi.Tag?.ToString() == control.BindingMode)
            {
                BindingModeCombo.SelectedIndex = i;
                break;
            }
        }

        // 控制是否显示变量绑定
        // M2: 移除 Image 和 Line —— 这两类控件无数值显示语义
        // M3: 添加 Slider/Switch/CheckBox/Dropdown —— 输入控件也需显示绑定变量状态
        bool isDisplayCtrl = control.Type is HmiControlType.Label
            or HmiControlType.Bar or HmiControlType.Gauge or HmiControlType.Knob
            or HmiControlType.Led or HmiControlType.TextArea or HmiControlType.Table
            or HmiControlType.Slider or HmiControlType.Switch
            or HmiControlType.CheckBox or HmiControlType.Dropdown;
        VariableBindingExpander.IsVisible = isDisplayCtrl;

        // 状态样式 Expander 仅对支持多态的控件显示（Button/Switch/CheckBox）
        // Image 控件类型也支持（用于默认图 + 切换图）
        // 文本控件（Label/TextArea/Table/TabView/Dropdown）也需编辑文本对齐
        bool supportsVisualStates = control.Type is HmiControlType.Button
            or HmiControlType.Switch or HmiControlType.CheckBox
            or HmiControlType.Image
            or HmiControlType.Label or HmiControlType.TextArea
            or HmiControlType.Table or HmiControlType.TabView
            or HmiControlType.Dropdown;
        VisualStateExpander.IsVisible = supportsVisualStates;

        // P1: 显示绑定模式提示
        UpdateBindingHint();
    }

    private void OnVariableSelected(object? sender, string? varName)
    {
        if (_isUpdating || _currentControl == null || Parent == null) return;

        // 联动模型：将变量写入 VariableBindings 集合
        // 如果**当前目标属性**已有读方向绑定，更新它；否则添加新绑定
        var existingReadBinding = FindReadBinding(_currentControl, CurrentBindingPropertyName);

        if (!string.IsNullOrEmpty(varName))
        {
            if (existingReadBinding != null)
            {
                // 更新现有读绑定的变量
                existingReadBinding.VariableId = varName;
            }
            else
            {
                // 添加新绑定 —— PropertyName 必须取「当前目标属性」，
                // 硬编码 "Text" 会让选 TextColor 变量时悄悄改写值通路的绑定。
                var direction = _currentControl.BindingMode == "ReadOnly"
                    ? BindingDirection.ReadOnly
                    : BindingDirection.ReadWrite;
                _currentControl.VariableBindings.Add(new VariableBindingData
                {
                    PropertyName = CurrentBindingPropertyName,
                    VariableId = varName,
                    Direction = direction
                });
            }

            // 选择变量即切回变量绑定源，清除表达式
            existingReadBinding ??= FindReadBinding(_currentControl, CurrentBindingPropertyName);
            if (existingReadBinding != null)
            {
                existingReadBinding.Kind = BindingSourceKind.Variable;
                existingReadBinding.Expression = null;
            }
            HideExpressionError();
            ExpressionInput.Text = "";
        }
        else if (existingReadBinding != null)
        {
            // 清除绑定
            _currentControl.VariableBindings.Remove(existingReadBinding);
            HideExpressionError();
            ExpressionInput.Text = "";
        }

        if (BindingModeCombo.SelectedItem is ComboBoxItem modeCbi)
            _currentControl.BindingMode = modeCbi.Tag?.ToString() ?? "ReadWrite";

        // 联动规则：写动作 Target 自动链接到写方向绑定变量
        ApplyLinkageRules();

        PropertyChanged?.Invoke(this, _currentControl);
    }

    // ── I1: 表达式绑定（变量源 / 表达式源）──

    /// <summary>按「当前目标属性」查找读方向绑定条目。</summary>
    private static VariableBindingData? FindReadBinding(HmiControl control, string propertyName)
        => control.VariableBindings.FirstOrDefault(b =>
            b.Direction != BindingDirection.WriteOnly
            && string.Equals(b.PropertyName, propertyName, StringComparison.Ordinal));

    /// <summary>把「目标属性」下拉切到指定属性名；不在下拉里的属性名回落默认值属性。</summary>
    private void SelectTargetProperty(string? propertyName)
    {
        var wanted = string.IsNullOrEmpty(propertyName) ? DefaultBindingPropertyName : propertyName;
        for (var i = 0; i < TargetPropertyCombo.Items.Count; i++)
        {
            if (TargetPropertyCombo.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), wanted, StringComparison.Ordinal))
            {
                TargetPropertyCombo.SelectedIndex = i;
                return;
            }
        }
        TargetPropertyCombo.SelectedIndex = 0;
    }

    /// <summary>
    /// 目标属性切换 —— 把该属性**自己**的绑定回填到界面。
    /// 不回填的话，切到 TextColor 时输入框里还留着 Text 的表达式，用户一改就写错了条目。
    /// </summary>
    private void OnTargetPropertyChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _currentControl == null || Parent == null) return;

        var binding = FindReadBinding(_currentControl, CurrentBindingPropertyName);
        var kind = binding?.Kind ?? BindingSourceKind.Variable;

        _isUpdating = true;
        try
        {
            for (var i = 0; i < BindingKindCombo.Items.Count; i++)
            {
                if (BindingKindCombo.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Tag?.ToString(), kind.ToString(), StringComparison.Ordinal))
                {
                    BindingKindCombo.SelectedIndex = i;
                    break;
                }
            }
            VariablePicker.SelectedVariableName = binding?.VariableId ?? "";
            ExpressionInput.Text = kind == BindingSourceKind.Expression ? binding?.Expression ?? "" : "";
        }
        finally
        {
            _isUpdating = false;
        }

        HideExpressionError();
        UpdateBindingKindVisibility();
    }

    /// <summary>获取或创建「当前目标属性」的读方向绑定条目（用于表达式/变量写入）。</summary>
    private VariableBindingData? GetOrCreateReadBinding()
    {
        if (_currentControl == null) return null;

        var propertyName = CurrentBindingPropertyName;
        var binding = FindReadBinding(_currentControl, propertyName);
        if (binding != null) return binding;

        var direction = _currentControl.BindingMode == "ReadOnly"
            ? BindingDirection.ReadOnly
            : BindingDirection.ReadWrite;
        binding = new VariableBindingData
        {
            PropertyName = propertyName,
            Direction = direction
        };
        _currentControl.VariableBindings.Add(binding);
        return binding;
    }

    /// <summary>I1: 绑定源（变量/表达式）切换。</summary>
    private void OnBindingKindChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _currentControl == null || Parent == null) return;

        var isExpression = BindingKindCombo.SelectedItem is ComboBoxItem cbi &&
                           cbi.Tag?.ToString() == "Expression";
        var kind = isExpression ? BindingSourceKind.Expression : BindingSourceKind.Variable;

        var binding = GetOrCreateReadBinding();
        if (binding == null) return;

        binding.Kind = kind;
        if (!isExpression)
        {
            binding.Expression = null;
            HideExpressionError();
            ExpressionInput.Text = "";
        }
        else
        {
            ExpressionInput.Text = binding.Expression ?? "";
        }

        UpdateBindingKindVisibility();
        PropertyChanged?.Invoke(this, _currentControl);
    }

    /// <summary>I1: 表达式输入实时校验 —— 合法才写入模型，非法显示错误并保持上次值。</summary>
    private void OnExpressionInputChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _currentControl == null || Parent == null) return;

        var text = ExpressionInput.Text ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowExpressionError(AppLocalization.Service[Lang.PropExprEmpty]);
            return;
        }

        if (!ExpressionEngine.TryCompile(text, out var error))
        {
            // 阶段 91 / #61i：渲染**码化**的语法失败。以前直接把 FormatException.Message
            // 显示在这里 ⇒ 英文界面下这行红字是中文；而 `?? AppLocalization.Service["msg.expr.error"]`
            // 那半是**结构上取不到的死分支**（NotNullWhen(false) 保证 error 非空）
            // ⇒ 那对键与常量随本阶段一起退场（见 LangConstantUsageGuardTests 的 UnusedBaseline）。
            ShowExpressionError(ExternalText.Describe(error));
            return;
        }

        HideExpressionError();

        var binding = GetOrCreateReadBinding();
        if (binding == null) return;

        binding.Kind = BindingSourceKind.Expression;
        binding.Expression = text;

        // 联动规则：写动作 Target 自动链接到写方向绑定变量
        ApplyLinkageRules();
        PropertyChanged?.Invoke(this, _currentControl);
    }

    /// <summary>根据绑定源切换变量选择器与表达式输入框的可见性。</summary>
    private void UpdateBindingKindVisibility()
    {
        var isExpression = BindingKindCombo.SelectedItem is ComboBoxItem cbi &&
                           cbi.Tag?.ToString() == "Expression";
        BindVariableLabel.IsVisible = !isExpression;
        VariablePicker.IsVisible = !isExpression;
        ExpressionLabel.IsVisible = isExpression;
        ExpressionInput.IsVisible = isExpression;
    }

    private void ShowExpressionError(string message)
    {
        ExpressionErrorText.Text = message;
        ExpressionErrorText.IsVisible = true;
        ExpressionInput.BorderBrush = HmiEditor.App.Theme.Danger;
    }

    private void HideExpressionError()
    {
        ExpressionErrorText.IsVisible = false;
        ExpressionInput.BorderBrush = HmiEditor.App.Theme.InputBorder;
    }

    /// <summary>P1: 绑定模式变更处理 —— 保存模式并刷新动作列表。</summary>
    private void OnBindingModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _currentControl == null || Parent == null) return;

        if (BindingModeCombo.SelectedItem is ComboBoxItem modeCbi)
            _currentControl.BindingMode = modeCbi.Tag?.ToString() ?? "ReadWrite";

        // 更新 VariableBindings 中所有条目的方向
        foreach (var binding in _currentControl.VariableBindings)
        {
            // 只更新非写方向绑定的方向
            if (binding.Direction != BindingDirection.WriteOnly)
            {
                binding.Direction = _currentControl.BindingMode == "ReadOnly"
                    ? BindingDirection.ReadOnly
                    : BindingDirection.ReadWrite;
            }
        }

        // 刷新动作列表（ReadOnly 时过滤写操作）
        PopulateActions(_currentControl.Type);

        // 更新提示文本
        UpdateBindingHint();

        // 若当前动作是写操作且切换到 ReadOnly，清除该动作
        if (_currentControl.BindingMode == "ReadOnly"
            && _currentControl.Actions.Count > 0
            && _currentControl.Actions.Any(a => IsWriteAction(a.ActionType)))
        {
            // ObservableCollection 没有 RemoveAll —— 倒序遍历移除
            for (int i = _currentControl.Actions.Count - 1; i >= 0; i--)
            {
                if (IsWriteAction(_currentControl.Actions[i].ActionType))
                    _currentControl.Actions.RemoveAt(i);
            }
            LoadAction(_currentControl);
        }

        // 联动规则：写动作 Target 自动链接到写方向绑定变量
        ApplyLinkageRules();

        PropertyChanged?.Invoke(this, _currentControl);
    }

    /// <summary>
    /// 联动规则：当输入控件有写方向绑定时，写动作 Target 自动链接到该变量。
    /// </summary>
    private void ApplyLinkageRules()
    {
        if (_currentControl == null) return;

        var writeVariable = _currentControl.PrimaryWriteVariable;
        if (string.IsNullOrEmpty(writeVariable)) return;

        foreach (var action in _currentControl.Actions)
        {
            if (HmiAction.IsWriteAction(action.ActionType)
                && string.IsNullOrEmpty(action.Target))
            {
                action.Target = writeVariable;
            }
        }
    }

    /// <summary>P1: 判断动作是否为变量写操作。</summary>
    private static bool IsWriteAction(string? actionType) => actionType switch
    {
        "SetVariable" or "SetBit" or "ResetBit" or "ToggleBit"
            or "MomentaryOn" or "MomentaryOff" => true,
        _ => false
    };

    /// <summary>P1: 更新绑定模式提示文本 + R5 联动一致性警告。</summary>
    private void UpdateBindingHint()
    {
        if (_currentControl == null)
        {
            BindingHint.Text = "";
            return;
        }

        var messages = new List<string>();

        if (_currentControl.BindingMode == "ReadOnly")
            messages.Add(AppLocalization.Service[Lang.PropBindingReadonlyHint]);

        // R5: 联动一致性检查 —— 写动作 Target 与绑定变量不一致时警告
        var writeVariable = _currentControl.PrimaryWriteVariable;
        
        // 检查是否有写动作但没有在变量绑定区绑定
        bool hasWriteActionWithoutBinding = false;
        foreach (var action in _currentControl.Actions)
        {
            if (!HmiAction.IsWriteAction(action.ActionType)) continue;
            if (string.IsNullOrEmpty(action.Target)) continue;

            // 若没有绑定写方向变量，提示用户可选地不在绑定区绑定（因为动作已控制变量）
            if (string.IsNullOrEmpty(writeVariable))
            {
                hasWriteActionWithoutBinding = true;
            }
            else if (!string.Equals(action.Target, writeVariable, StringComparison.Ordinal))
            {
                messages.Add(AppLocalization.Service.Format(Lang.PropBindingTargetMismatch, action.Target, writeVariable));
            }
        }
        
        if (hasWriteActionWithoutBinding)
        {
            messages.Add(AppLocalization.Service[Lang.PropBindingActionOnlyHint]);
        }

        BindingHint.Text = string.Join("\n", messages);
    }
}
