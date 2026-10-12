using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using HmiEditor.App.Resources;
using HmiEditor.Core.Models;
using System.Linq;

namespace HmiEditor.App.Views.Editors;

/// <summary>
/// 变量选择器（R1-T4）—— 树形 + 搜索的变量绑定选择器。
///
/// 复合控件：只读显示框 + 浏览按钮 + 弹出 Popup。
/// Popup 内含搜索框 + TreeView：
///   - 用户变量按 VariableGroup 分组（无 GroupId → "未分组"）
///   - 系统变量按 SystemVariableCategory 分组
/// 选择叶子节点后关闭弹窗并触发 VariableSelected 事件。
///
/// 契约：用户变量返回 Id（M1: 稳定标识, 变量改名不影响绑定）；
/// 系统变量返回 Id（稳定标识）。
/// 旧项目用 Name 绑定的, VariableManager.ResolveBinding 仍支持 Name 回退解析。
/// </summary>
public partial class VariablePicker : UserControl
{
    private VariableManager? _variableManager;
    private bool _isUpdating;

    /// <summary>变量被选中时触发，参数为绑定标识（Id），清除时为 null。</summary>
    public event EventHandler<string?>? VariableSelected;

    private string? _selectedVariableName;

    /// <summary>当前绑定的变量标识。设置时刷新显示但不触发事件。</summary>
    public string? SelectedVariableName
    {
        get => _selectedVariableName;
        set
        {
            _isUpdating = true;
            _selectedVariableName = value;
            UpdateDisplay();
            _isUpdating = false;
        }
    }

    // P2: 控件类型过滤器 —— 设置后仅显示兼容数据类型的变量
    private HmiControlType? _filterControlType;

    /// <summary>
    /// 设置控件类型过滤器 —— 仅显示与该控件类型兼容的数据类型变量。
    /// null 表示不过滤（显示全部）。
    /// </summary>
    public HmiControlType? FilterControlType
    {
        get => _filterControlType;
        set => _filterControlType = value;
    }

    /// <summary>
    /// 控件名称 —— 用于 AI 智能绑定推荐(名称语义匹配)。
    /// 设置后打开选择器时显示推荐区域。
    /// </summary>
    public string? RecommendationContextName { get; set; }

    /// <summary>
    /// P2: 按控件类型获取兼容的变量数据类型集合。
    /// 布尔类控件仅匹配 Bool；数值类控件匹配数值类型；其余匹配全部。
    /// </summary>
    private static HashSet<string> GetCompatibleDataTypes(HmiControlType type) => type switch
    {
        HmiControlType.Led or HmiControlType.Switch or HmiControlType.CheckBox
            => new() { "Bool", "Int16", "Int32", "UInt16", "UInt32" },
        HmiControlType.Gauge or HmiControlType.Bar or HmiControlType.Knob
            or HmiControlType.Slider or HmiControlType.Dropdown
            => new() { "Int16", "Int32", "Float", "Double", "UInt16", "UInt32" },
        _ => new() { "Bool", "Int16", "Int32", "Float", "Double", "UInt16", "UInt32", "String" }
    };

    /// <summary>P2: 判断变量数据类型是否与控件类型兼容。</summary>
    private bool IsDataTypeCompatible(string? dataType)
    {
        if (!_filterControlType.HasValue || string.IsNullOrEmpty(dataType))
            return true;

        var compatible = GetCompatibleDataTypes(_filterControlType.Value);

        // UDT 类型数据类型为 "UDT:xxx"，只允许 Table 绑定
        if (dataType.StartsWith("UDT:", StringComparison.Ordinal))
            return _filterControlType == HmiControlType.Table;

        return compatible.Contains(dataType);
    }

    public VariablePicker()
    {
        InitializeComponent();

        BrowseBtn.Click += OnBrowseClicked;
        ClearBtn.Click += OnClearClicked;
        VarTree.SelectionChanged += OnTreeSelectionChanged;
        SearchBox.TextChanged += OnSearchTextChanged;
    }

    /// <summary>注入变量管理器（由 PropertyPanelView.SetVariableManager 调用）。</summary>
    public void LoadVariables(VariableManager? vm)
    {
        _variableManager = vm;
    }

    // ── 事件处理 ──

    private void OnBrowseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 每次打开时刷新树，反映新增/删除的变量
        _isUpdating = true;
        SearchBox.Text = "";
        RebuildTree("");
        RefreshRecommendations();
        _isUpdating = false;

        PickerPopup.IsOpen = true;
        SearchBox.Focus();
    }

    /// <summary>
    /// 刷新 AI 智能推荐区 —— 基于控件类型 + 名称调用 VariableMatchService。
    /// </summary>
    private void RefreshRecommendations()
    {
        RecommendList.Items.Clear();
        RecommendPanel.IsVisible = false;

        if (_variableManager is null ||
            string.IsNullOrWhiteSpace(RecommendationContextName) ||
            !_filterControlType.HasValue)
        {
            return;
        }

        var control = new HmiControl
        {
            Type = _filterControlType.Value,
            Name = RecommendationContextName
        };

        var matches = HmiEditor.AI.Services.VariableMatchService.Recommend(control, _variableManager, maxResults: 3);
        if (matches.Count == 0) return;

        RecommendTitle.Text = AppLocalization.Service.Format(Lang.VarPickerRecommendTitle, RecommendationContextName);
        foreach (var match in matches)
        {
            var btn = new Button
            {
                Content = $"{match.Variable.Name}  ({match.Variable.DataType})  ★{match.Score:F0}%",
                Tag = match.Variable.Id
            };
            ToolTip.SetTip(btn, AppLocalization.Service.Format(Lang.VarPickerRecommendReason, match.Reason));
            btn.Click += (_, _) =>
            {
                if (btn.Tag is string varId)
                {
                    _selectedVariableName = varId;
                    UpdateDisplay();
                    PickerPopup.IsOpen = false;
                    VariableSelected?.Invoke(this, varId);
                }
            };
            RecommendList.Items.Add(btn);
        }
        RecommendPanel.IsVisible = true;
    }

    private void OnClearClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdating) return;

        _selectedVariableName = null;
        UpdateDisplay();
        PickerPopup.IsOpen = false;
        VariableSelected?.Invoke(this, null);
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;

        // 仅叶子节点携带 Tag（绑定标识）；分组节点 Tag 为 null，忽略
        if (VarTree.SelectedItem is TreeViewItem tvi && tvi.Tag is string varName)
        {
            _selectedVariableName = varName;
            UpdateDisplay();
            PickerPopup.IsOpen = false;
            VariableSelected?.Invoke(this, varName);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;
        RebuildTree(SearchBox.Text ?? "");
    }

    // ── 内部逻辑 ──

    private void UpdateDisplay()
    {
        // M1: 绑定标识为 Id, 显示时解析为变量名便于用户识别
        DisplayField.Text = ResolveDisplayName(_selectedVariableName);
    }

    /// <summary>
    /// M1: 将绑定标识（Id 或旧项目的 Name）解析为可读的变量名。
    /// 先按 Id 查找用户变量, 再按 Name 回退, 最后检查系统变量。
    /// 无法解析时原样返回（可能是 UDT compound id 或已删除的变量）。
    /// </summary>
    private string? ResolveDisplayName(string? bindingId)
    {
        if (string.IsNullOrEmpty(bindingId))
            return null;

        if (_variableManager == null)
            return AppLocalization.Service[Lang.VarPickerNotLoaded];

        // UDT 字段 compound id（含点号）—— 返回原值
        if (bindingId.Contains('.'))
            return bindingId;

        // 按_Id 查找用户变量
        var userVar = _variableManager.Variables
            .FirstOrDefault(v => v.Id == bindingId);
        if (userVar != null)
            return userVar.Name;

        // 旧项目回退: 按 Name 查找
        var byName = _variableManager.Variables
            .FirstOrDefault(v => v.Name == bindingId);
        if (byName != null)
            return byName.Name;

        // 系统变量按 Id 查找
        var sysVar = _variableManager.SystemVariables
            .FirstOrDefault(s => s.Id == bindingId);
        if (sysVar != null)
            return sysVar.Name;

        // 无法解析 —— 悬空引用
        return AppLocalization.Service[Lang.VarPickerRefLost];
    }

    /// <summary>根据搜索过滤词重建树。filter 为空时显示全部。</summary>
    private void RebuildTree(string filter)
    {
        _isUpdating = true;
        try
        {
            VarTree.Items.Clear();
            if (_variableManager == null) return;

            var f = filter.Trim().ToLowerInvariant();

            // ── 用户变量：按 GroupId 分组 ──
            var groups = _variableManager.Groups.ToList();

            foreach (var grp in _variableManager.Variables
                         .GroupBy(v => v.GroupId)
                         .OrderBy(g => g.Key))
            {
                var groupName = groups.FirstOrDefault(g => g.Id == grp.Key)?.Name
                                ?? (string.IsNullOrEmpty(grp.Key) ? AppLocalization.Service[Lang.VarPickerUngrouped] : grp.Key);

                var groupNode = new TreeViewItem
                {
                    Header = groupName,
                    IsExpanded = true
                };

                foreach (var v in grp.OrderBy(v => v.Name))
                {
                    // P2: 按控件类型过滤不兼容数据类型的变量
                    if (!IsDataTypeCompatible(v.DataType))
                        continue;

                    if (!string.IsNullOrEmpty(f)
                        && !v.Name.ToLowerInvariant().Contains(f)
                        && !(v.Address?.ToLowerInvariant().Contains(f) ?? false))
                        continue;

                    var suffix = string.IsNullOrEmpty(v.Unit) ? "" : $" {v.Unit}";
                    groupNode.Items.Add(new TreeViewItem
                    {
                        Header = $"{v.Name}  ({v.DataType}){suffix}",
                        Tag = v.Id  // M1: 绑定标识 = Id（稳定, 变量改名不影响绑定）
                    });
                }

                if (groupNode.Items.Count > 0)
                    VarTree.Items.Add(groupNode);
            }

            // ── 系统变量：按 Category 分组 ──
            foreach (var cat in _variableManager.SystemVariables
                         .GroupBy(s => s.Category)
                         .OrderBy(g => g.Key.ToString()))
            {
                var catNode = new TreeViewItem
                {
                    Header = AppLocalization.Service.Format(Lang.VarPickerSystemPrefix, cat.Key),
                    IsExpanded = false
                };

                foreach (var sv in cat.OrderBy(s => s.Name))
                {
                    // P2: 按控件类型过滤不兼容数据类型的系统变量
                    if (!IsDataTypeCompatible(sv.DataType))
                        continue;

                    if (!string.IsNullOrEmpty(f)
                        && !sv.Name.ToLowerInvariant().Contains(f)
                        && !sv.Id.ToLowerInvariant().Contains(f))
                        continue;

                    catNode.Items.Add(new TreeViewItem
                    {
                        Header = $"{sv.Name}  ({sv.DataType})",
                        Tag = sv.Id  // 系统变量绑定标识 = Id
                    });
                }

                if (catNode.Items.Count > 0)
                    VarTree.Items.Add(catNode);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }
}
