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
    // ── 动作（联动模型）──

    private void PopulateActions(HmiControlType controlType)
    {
        CboActionType.Items.Clear();

        var actions = HmiAction.GetAvailableActions(controlType);

        // P1: ReadOnly 绑定模式下过滤写操作动作
        bool isReadOnly = _currentControl?.BindingMode == "ReadOnly";

        foreach (var action in actions)
        {
            if (isReadOnly && IsWriteAction(action.Tag))
                continue;

            CboActionType.Items.Add(new ComboBoxItem
            {
                // 标签按 Tag 推导（阶段 94，登记 #61m）—— Core 的 ActionMeta 不再带中文标签。
                Content = ActionTextKeys.TypeName(action.Tag),
                Tag = action.Tag
            });
        }
        CboActionType.SelectedIndex = 0;
    }

    private void LoadAction(HmiControl control)
    {
        // 联动模型：刷新动作列表并加载选中项
        RefreshActionList(control);

        var actions = control.Actions;
        var primaryAction = actions.Count > 0
            ? (_selectedActionIndex < actions.Count ? actions[_selectedActionIndex] : actions[0])
            : control.Action;

        string? actionType = null;
        if (primaryAction != null)
        {
            for (int i = 0; i < CboActionType.Items.Count; i++)
            {
                if (CboActionType.Items[i] is ComboBoxItem cbi &&
                    cbi.Tag?.ToString() == primaryAction.ActionType)
                {
                    CboActionType.SelectedIndex = i;
                    actionType = primaryAction.ActionType;
                    break;
                }
            }
            // 优先显示友好名称：将 ID 解析为“变量名 (类型)”，解析失败则回退原值
            CboActionTarget.Text = ResolveTargetDisplayName(primaryAction.Target) ?? "";
            TxtActionValue.Text = primaryAction.Value ?? "";
        }
        else
        {
            CboActionType.SelectedIndex = 0;
            CboActionTarget.Text = "";
            TxtActionValue.Text = "";
            _pendingActionTargetId = null;
        }
        UpdateActionFieldVisibility();
        RefreshActionTargetItems(actionType);
    }

    /// <summary>刷新动作列表 chips（联动模型：多动作管理）。</summary>
    private void RefreshActionList(HmiControl? control)
    {
        if (ActionList == null) return;

        var items = new List<object>();
        if (control != null)
        {
            // 集合优先，回退旧字段
            var actions = control.Actions.Count > 0
                ? control.Actions.ToList()
                : (control.Action != null
                    ? new List<HmiAction> { control.Action }
                    : new List<HmiAction>());

            for (int i = 0; i < actions.Count; i++)
            {
                var a = actions[i];
                // 动作类型显示名统一走 ActionTextKeys（阶段 94，登记 #61m）——
                // 改前走 ActionTypeMeta.DisplayName，那是**产物侧**的固定中文表，
                // 于是 chip 在英文界面下仍是中文、且对「无动作」显示英文 Id `None`。
                var typeName = ActionTextKeys.TypeName(a.ActionType);
                string chipTarget;
                if (string.IsNullOrEmpty(a.Target))
                {
                    chipTarget = DefaultTexts.ActionTargetEmpty;
                }
                else if (ActionTypeMeta.IsTargetLiteral(a.ActionType))
                {
                    // 屏幕名/脚本 ID 直接显示即可（屏幕名本就友好，脚本 ID 在下拉里带括号）
                    chipTarget = a.Target;
                }
                else
                {
                    // 写变量类动作优先显示友好名称，解析失败回退原始 ID
                    chipTarget = ResolveTargetDisplayName(a.Target) ?? a.Target;
                }
                items.Add(new ActionChipModel
                {
                    Index = i,
                    DisplayName = $"{i + 1}.{typeName}:{chipTarget}"
                });
            }
        }
        ActionList.ItemsSource = items;
    }

    /// <summary>动作 chip 点击 —— 切换选中、添加或删除动作。</summary>
    private void OnActionChipClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentControl == null) return;

        if (sender is Button btn)
        {
            if (btn.Tag?.ToString() == "__add__")
            {
                // 添加新动作
                _selectedActionIndex = _currentControl.Actions.Count;
                _currentControl.Actions.Add(new HmiAction { ActionType = "None" });
                LoadAction(_currentControl);
            }
            else if (btn.Tag is ActionChipModel chip)
            {
                _selectedActionIndex = chip.Index;
                LoadAction(_currentControl);
            }
        }
    }

    private void OnActionTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _currentControl == null || Parent == null) return;

        var actionType = CboActionType.SelectedItem is ComboBoxItem cbi
            ? cbi.Tag?.ToString()
            : null;

        UpdateActionFieldVisibility();
        RefreshActionTargetItems(actionType);
        SaveAction();
    }

    /// <summary>动作目标下拉框选择后，将对应 BindingId 暂存并以友好名称显示在文本框，最后保存动作。</summary>
    private void OnActionTargetSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _currentControl == null || Parent == null) return;

        if (CboActionTarget.SelectedItem is ComboBoxItem cbi && cbi.Tag is string target)
        {
            // 暂存真正的 BindingId，确保 SaveAction 写入模型的是 ID 而非显示名称
            _pendingActionTargetId = target;
            // 文本框显示友好名称（变量树里已经是“名 (类型)”，屏幕/脚本也同理）
            CboActionTarget.Text = cbi.Content?.ToString() ?? target;
            SaveAction();
        }
    }

    /// <summary>根据当前动作类型，为动作目标下拉框填充变量/屏幕/脚本候选项。</summary>
    private void RefreshActionTargetItems(string? actionType)
    {
        if (_isUpdating) return;

        _isUpdating = true;
        var previousText = CboActionTarget.Text ?? "";
        CboActionTarget.Items.Clear();

        switch (actionType)
        {
            case "SetBit":
            case "ResetBit":
            case "ToggleBit":
            case "MomentaryOn":
            case "MomentaryOff":
            case "SetVariable":
                PopulateVariableTargets();
                break;
            case "SwitchScreen":
                PopulateScreenTargets();
                break;
            case "ExecuteScript":
                PopulateScriptTargets();
                break;
        }

        CboActionTarget.Text = previousText;
        _isUpdating = false;
    }

    private void PopulateVariableTargets()
    {
        if (_variableManager == null) return;

        foreach (var item in _variableManager.GetAllBindableItems())
        {
            CboActionTarget.Items.Add(new ComboBoxItem
            {
                Content = $"{item.DisplayName}  ({item.DataType})",
                Tag = item.BindingId
            });
        }

        foreach (var sv in _variableManager.SystemVariables)
        {
            CboActionTarget.Items.Add(new ComboBoxItem
            {
                Content = $"{sv.Name}  ({sv.DataType})",
                Tag = sv.Id
            });
        }
    }

    private void PopulateScreenTargets()
    {
        if (_currentProject == null) return;

        foreach (var screen in _currentProject.Screens)
        {
            CboActionTarget.Items.Add(new ComboBoxItem
            {
                Content = screen.Name,
                Tag = screen.Name
            });
        }
    }

    private void PopulateScriptTargets()
    {
        if (_currentProject == null) return;

        foreach (var script in _currentProject.Scripts)
        {
            CboActionTarget.Items.Add(new ComboBoxItem
            {
                Content = $"{script.Name}  ({script.Id})",
                Tag = script.Id
            });
        }
    }

    private void UpdateActionFieldVisibility()
    {
        if (CboActionType.SelectedItem is ComboBoxItem cbi && cbi.Tag?.ToString() is string tag)
        {
            var actions = _currentControl != null
                ? HmiAction.GetAvailableActions(_currentControl.Type)
                : new List<HmiAction.ActionMeta>();

            var meta = actions.FirstOrDefault(a => a.Tag == tag);
            CboActionTarget.IsVisible = meta?.NeedsTarget ?? false;
            LblActionValue.IsVisible = meta?.NeedsValue ?? false;
            TxtActionValue.IsVisible = meta?.NeedsValue ?? false;
        }
    }

    private void SaveAction()
    {
        if (_currentControl == null) return;

        // 联动模型：写入 Actions 集合
        if (CboActionType.SelectedItem is ComboBoxItem actionCbi &&
            actionCbi.Tag?.ToString() is string actionType && actionType != "None")
        {
            // 优先使用下拉选中时暂存的 ID（用户在 UI 上看到的是友好名称，但模型应保留 ID）
            string? target = null;
            if (!string.IsNullOrEmpty(_pendingActionTargetId))
            {
                target = _pendingActionTargetId;
            }
            else if (!string.IsNullOrWhiteSpace(CboActionTarget.Text))
            {
                target = CboActionTarget.Text.Trim();
            }

            var action = new HmiAction
            {
                ActionType = actionType,
                Target = target,
                Value = string.IsNullOrWhiteSpace(TxtActionValue.Text) ? null : TxtActionValue.Text.Trim(),
                TriggerEvent = actionType switch
                {
                    "MomentaryOn" or "MomentaryOff" => "OnPress",
                    _ => "OnClick"
                }
            };

            // 联动规则：写动作 Target 自动链接到写方向绑定变量（若已在绑定区绑定了写方向变量）
            // 注意：如果用户明确指定了 target（且非空），则优先使用用户指定的，不强制覆盖
            if (HmiAction.IsWriteAction(actionType) && string.IsNullOrEmpty(action.Target))
            {
                var writeVar = _currentControl.PrimaryWriteVariable;
                if (!string.IsNullOrEmpty(writeVar))
                {
                    action.Target = writeVar;
                }
                // 若未绑定写方向变量，则保留 target 为空或用户之前的输入，由 UpdateBindingHint 给出提示
            }

            // 替换选中索引的动作或添加新动作
            if (_currentControl.Actions.Count > 0)
            {
                var index = Math.Min(_selectedActionIndex, _currentControl.Actions.Count - 1);
                _currentControl.Actions[index] = action;
            }
            else
                _currentControl.Actions.Add(action);

            // 保存动作后刷新动作列表和联动一致性提示
            RefreshActionList(_currentControl);
            UpdateBindingHint();
        }
        else
        {
            // 当前选中动作设为 None —— 从集合中移除该动作
            if (_currentControl.Actions.Count > 0)
            {
                var index = Math.Min(_selectedActionIndex, _currentControl.Actions.Count - 1);
                _currentControl.Actions.RemoveAt(index);
                _selectedActionIndex = Math.Max(0, index - 1);
            }
            else
                _currentControl.Action = null;

            RefreshActionList(_currentControl);
            UpdateBindingHint();
        }

        // 一轮保存结束，清除本次下拉选中暂存
        _pendingActionTargetId = null;

        UpdateActionFieldVisibility();
        PropertyChanged?.Invoke(this, _currentControl);
    }
}
