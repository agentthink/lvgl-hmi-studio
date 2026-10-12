using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App;

namespace HmiEditor.App.Views;

/// <summary>
/// 报警配置对话框 —— 管理报警组和报警配置。
/// </summary>
public partial class AlarmConfigView : ChromeWindow
{
    private readonly ObservableCollection<AlarmGroup> _groups;
    private readonly HmiProject _project;
    private AlarmConfig? _currentAlarm;
    private bool _isUpdating;

    /// <summary>
    /// 当报警配置发生变更时触发。
    /// </summary>
    public event EventHandler? AlarmConfigChanged;

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public AlarmConfigView()
    {
        _groups = null!;
        _project = null!;
        InitializeComponent();
    }

    public AlarmConfigView(ObservableCollection<AlarmGroup> groups, HmiProject project) : this()
    {
        _groups = groups;
        _project = project;

        PopulateComboBoxes();

        // ⚠ 订阅必须**早于**首次填充列表：`RefreshGroupList()` 末尾会「默认选中第一个组」，
        //   那次选中若发生在订阅之前，右列永远不会被填充 —— 用户看到组已高亮、
        //   右边却是空的，而且点同一个（已选中的）组不会再触发 SelectionChanged，
        //   只有先点别的组再点回来才看得到报警（阶段 120 修复）。
        GroupList.SelectionChanged += OnGroupSelectionChanged;
        AlarmList.SelectionChanged += OnAlarmSelectionChanged;

        RefreshGroupList();                       // 有组时会把右列填好并显示
        if (_groups.Count == 0) ShowAlarmList(false);
        EditPanel.IsVisible = false;              // 未选中具体报警前，编辑面板保持关闭

        BtnAddGroup.Click += (_, _) => OnAddGroup();
        BtnDeleteGroup.Click += (_, _) => OnDeleteGroup();
        BtnAddAlarm.Click += (_, _) => OnAddAlarm();
        BtnDeleteAlarm.Click += (_, _) => OnDeleteAlarm();

        // 修复（阶段 120）：编辑面板的字段处理器**从未被订阅** —— `OnAlarmFieldChanged` /
        // `OnConditionTypeChanged` 在全仓、全 git 历史里只有声明那一行。后果是用户改
        // 名称 / 描述 / 严重级别 / 变量 / 条件类型 / 阈值 / 死区 / 延时 / 位偏移 /
        // 期望值 / 启用 / 需确认**一个都不落模型**，`AlarmConfigChanged` 也不触发
        // （主窗口 `OpenAlarmConfigDialog` 靠它把工程标脏）⇒ 这个对话框实际上是"能看不能改"。
        // 程序性回填（PopulateEditPanel）已在 `_isUpdating` 窗口内，不会误触发处理器。
        TxtAlarmName.TextChanged += OnAlarmFieldChanged;
        TxtAlarmDescription.TextChanged += OnAlarmFieldChanged;
        TxtAlarmCause.TextChanged += OnAlarmFieldChanged;
        TxtAlarmResponse.TextChanged += OnAlarmFieldChanged;
        TxtAlarmConsequence.TextChanged += OnAlarmFieldChanged;
        TxtAllowedResponseMs.TextChanged += OnAlarmFieldChanged;
        TxtThreshold.TextChanged += OnAlarmFieldChanged;
        TxtDeadband.TextChanged += OnAlarmFieldChanged;
        TxtDelayMs.TextChanged += OnAlarmFieldChanged;
        TxtBitOffset.TextChanged += OnAlarmFieldChanged;
        TxtExpectedValue.TextChanged += OnAlarmFieldChanged;

        CboSeverity.SelectionChanged += OnAlarmFieldChanged;
        CboVariableId.SelectionChanged += OnAlarmFieldChanged;
        ChkEnabled.IsCheckedChanged += OnAlarmFieldChanged;
        ChkRequiresAck.IsCheckedChanged += OnAlarmFieldChanged;

        CboConditionType.SelectionChanged += OnConditionTypeChanged;
    }

    // ──────────────────────────────────────────────
    //  ComboBox 初始化
    // ──────────────────────────────────────────────

    private void PopulateComboBoxes()
    {
        // 严重级别
        CboSeverity.Items.Clear();
        foreach (var sev in Enum.GetValues<AlarmSeverity>())
        {
            var item = new ComboBoxItem
            {
                Content = sev switch
                {
                    AlarmSeverity.Info => AppLocalization.Service["alarm.level.info"],
                    AlarmSeverity.Warning => AppLocalization.Service["alarm.level.warning"],
                    AlarmSeverity.Critical => AppLocalization.Service["alarm.level.critical"],
                    AlarmSeverity.Emergency => AppLocalization.Service["alarm.level.emergency"],
                    _ => sev.ToString()
                },
                Tag = sev
            };
            CboSeverity.Items.Add(item);
        }

        // 条件类型
        CboConditionType.Items.Clear();
        foreach (var ct in Enum.GetValues<AlarmConditionType>())
        {
            var item = new ComboBoxItem
            {
                Content = ct switch
                {
                    AlarmConditionType.HighLimit => AppLocalization.Service["alarm.cond.high.limit"],
                    AlarmConditionType.LowLimit => AppLocalization.Service["alarm.cond.low.limit"],
                    AlarmConditionType.BitTrigger => AppLocalization.Service["alarm.cond.bit.trigger"],
                    AlarmConditionType.StateChange => AppLocalization.Service["alarm.cond.state.change"],
                    AlarmConditionType.Deviation => AppLocalization.Service["alarm.cond.deviation"],
                    _ => ct.ToString()
                },
                Tag = ct
            };
            CboConditionType.Items.Add(item);
        }

        // 变量列表
        CboVariableId.Items.Clear();
        foreach (var variable in _project.VariableManager.Variables)
        {
            var item = new ComboBoxItem
            {
                Content = $"{variable.Name}  ({variable.Id})",
                Tag = variable.Id
            };
            CboVariableId.Items.Add(item);
        }
    }

    // ──────────────────────────────────────────────
    //  左侧：分组管理
    // ──────────────────────────────────────────────

    private void RefreshGroupList()
    {
        GroupList.Items.Clear();

        foreach (var group in _groups)
        {
            var item = CreateGroupListItem(group);
            GroupList.Items.Add(item);
        }

        if (_groups.Count > 0 && GroupList.SelectedItem == null)
        {
            GroupList.SelectedIndex = 0;
        }

        UpdateRightPanel();
    }

    /// <summary>
    /// 分组列表行。**把「刷新时要就地更新的控件」在构造时捕获**，
    /// 而不是日后按位置钻进 <c>Content</c> 的 <c>Children</c> 反查 ——
    /// 那种写法在结构一变就**静默失效**：不抛、不报错，只是不再刷新。
    /// <see cref="Control.Tag"/> 仍是 <see cref="AlarmGroup"/>，
    /// 于是其余按 <c>Tag is AlarmGroup</c> 取模型的地方语义一行都不用改。
    /// </summary>
    private sealed class AlarmGroupRow : ListBoxItem
    {
        public AlarmGroupRow(AlarmGroup group, TextBlock countText)
        {
            Group = group;
            CountText = countText;
            Tag = group;
        }

        public AlarmGroup Group { get; }

        public TextBlock CountText { get; }
    }

    private ListBoxItem CreateGroupListItem(AlarmGroup group)
    {
        var nameText = new TextBlock
        {
            Text = group.Name,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var countText = new TextBlock
        {
            Text = AppLocalization.Service.Format("alarm.count", group.Alarms.Count),
            Foreground = HmiEditor.App.Theme.TextHint,
            FontSize = 11
        };

        var infoPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children = { nameText, countText }
        };

        return new AlarmGroupRow(group, countText)
        {
            Content = infoPanel,
            Padding = new Thickness(12, 8)
        };
    }

    private void OnAddGroup()
    {
        var newGroup = new AlarmGroup
        {
            Name = $"Group {_groups.Count + 1}"
        };
        _groups.Add(newGroup);
        RefreshGroupList();

        // 选中新创建的组
        foreach (var item in GroupList.Items)
        {
            if (item is ListBoxItem lbi && lbi.Tag == newGroup)
            {
                GroupList.SelectedItem = item;
                break;
            }
        }

        StatusText.Text = AppLocalization.Service.Format("alarm.group.created", newGroup.Name);
        AlarmConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeleteGroup()
    {
        if (GroupList.SelectedItem is not ListBoxItem selectedItem ||
            selectedItem.Tag is not AlarmGroup group)
            return;

        if (_groups.Count <= 1)
        {
            StatusText.Text = AppLocalization.Service["alarm.keep.at.least.one"];
            return;
        }

        var groupId = group.Id;
        // 级联清理：将被删除分组的报警移入空分组（GroupId 清空），保持数据一致性
        var movedCount = _project.AlarmGroups.SelectMany(g => g.Alarms)
            .Count(a => a.GroupId == groupId);
        foreach (var alarm in _project.AlarmGroups.SelectMany(g => g.Alarms)
                     .Where(a => a.GroupId == groupId))
            alarm.GroupId = "";

        _groups.Remove(group);
        RefreshGroupList();
        StatusText.Text = movedCount > 0
            ? AppLocalization.Service.Format("alarm.deleted.group.cascade", group.Name, movedCount)
            : AppLocalization.Service.Format("alarm.deleted.group", group.Name);
        AlarmConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnGroupSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (GroupList.SelectedItem is ListBoxItem item && item.Tag is AlarmGroup group)
        {
            RefreshAlarmList(group);
            ShowAlarmList(true);
            StatusText.Text = AppLocalization.Service.Format("alarm.selected.group", group.Name, group.Alarms.Count);
        }
        else
        {
            ShowAlarmList(false);
        }
    }

    // ──────────────────────────────────────────────
    //  右侧面板切换
    // ──────────────────────────────────────────────

    private void ShowAlarmList(bool visible)
    {
        AlarmList.IsVisible = visible;
        EmptyHint.IsVisible = !visible;
    }

    private void UpdateRightPanel()
    {
        if (_groups.Count == 0)
        {
            ShowAlarmList(false);
            EditPanel.IsVisible = false;
        }
    }

    // ──────────────────────────────────────────────
    //  右侧：报警管理
    // ──────────────────────────────────────────────

    private void RefreshAlarmList(AlarmGroup group)
    {
        AlarmList.Items.Clear();
        _currentAlarm = null;
        EditPanel.IsVisible = false;

        // ISA-18.2 切片㈡（阶段 292）：**显示顺序**按优先级（P1 在前）。
        // ⚠ 只排序**显示**，绝不动 `group.Alarms` 的模型顺序 —— 生成器的 `config_id`
        //   就是那个遍历序（`_alarm_lookup` / 确认 / 搁置全按它），重排模型会让既有 id 全部错位。
        //   `OrderBy` 是稳定排序 ⇒ 同优先级内保持模型顺序。
        foreach (var alarm in group.Alarms.OrderBy(AlarmPriority.Of))
        {
            var item = CreateAlarmListItem(alarm);
            AlarmList.Items.Add(item);
        }
    }

    private ListBoxItem CreateAlarmListItem(AlarmConfig alarm)
    {
        var severityColor = alarm.Severity switch
        {
            AlarmSeverity.Info => UiPalette.SeverityInfo,
            AlarmSeverity.Warning => UiPalette.SeverityWarning,
            AlarmSeverity.Critical => UiPalette.SeverityCritical,
            AlarmSeverity.Emergency => UiPalette.SeverityEmergency,
            _ => UiPalette.ValidationIdle
        };

        // ISA-18.2 切片㈡（阶段 292）：点色与 P 记法按**优先级**（= f(严重度, 允许响应时间)）；
        // 严重度**继续**以文字标签保留 —— "优先级 ≠ 严重度"正是这一片要让人一眼看见的东西。
        var priority = AlarmPriority.Of(alarm);
        var priorityColor = PriorityColor(priority);

        var severityDot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(priorityColor),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var nameText = new TextBlock
        {
            Text = alarm.Name,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var severityLabel = new TextBlock
        {
            Text = alarm.Severity.ToString(),
            Foreground = new SolidColorBrush(severityColor),
            FontSize = 11,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        // P1…P4：语言无关（`AlarmPriority.Label` 的注释写了为什么不进字典）
        var priorityLabel = new TextBlock
        {
            Text = AlarmPriority.Label(priority),
            Foreground = new SolidColorBrush(priorityColor),
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var enabledBadge = new Border
        {
            Background = alarm.IsEnabled
                ? new SolidColorBrush(Color.FromRgb(6, 95, 70))
                : new SolidColorBrush(Color.FromRgb(75, 85, 99)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = alarm.IsEnabled ? AppLocalization.Service["alarm.enabled"] : AppLocalization.Service["user.mgr.disable"],
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 10
            }
        };

        var panel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children = { severityDot, priorityLabel, nameText, severityLabel, enabledBadge }
        };

        return new ListBoxItem
        {
            Content = panel,
            Tag = alarm,
            Padding = new Thickness(12, 8)
        };
    }

    /// <summary>
    /// 优先级 → 颜色（ISA-18.2 切片㈡，阶段 292）：**按名次复用既有严重度色板**
    /// （P1 最严 ↔ Emergency 色 … P4 ↔ Info 色）。
    /// 刻意**不新增**设计常量：色板的单一来源仍是 <see cref="UiPalette"/>，这里只做名次映射。
    /// </summary>
    private static Color PriorityColor(int priority) => priority switch
    {
        AlarmPriority.Highest => UiPalette.SeverityEmergency,
        2 => UiPalette.SeverityCritical,
        3 => UiPalette.SeverityWarning,
        _ => UiPalette.SeverityInfo
    };

    private void OnAddAlarm()
    {
        if (GroupList.SelectedItem is not ListBoxItem groupItem ||
            groupItem.Tag is not AlarmGroup group)
        {
            StatusText.Text = AppLocalization.Service["alarm.select.group"];
            return;
        }

        var newAlarm = new AlarmConfig
        {
            Name = $"Alarm {group.Alarms.Count + 1}",
            GroupId = group.Id
        };
        group.Alarms.Add(newAlarm);
        RefreshAlarmList(group);

        // 选中新报警
        foreach (var item in AlarmList.Items)
        {
            if (item is ListBoxItem lbi && lbi.Tag == newAlarm)
            {
                AlarmList.SelectedItem = item;
                break;
            }
        }

        // 刷新左侧组列表项（更新报警计数）
        RefreshGroupList();

        StatusText.Text = AppLocalization.Service.Format("alarm.created", newAlarm.Name);
        AlarmConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeleteAlarm()
    {
        if (GroupList.SelectedItem is not ListBoxItem groupItem ||
            groupItem.Tag is not AlarmGroup group)
            return;

        if (AlarmList.SelectedItem is not ListBoxItem alarmItem ||
            alarmItem.Tag is not AlarmConfig alarm)
            return;

        group.Alarms.Remove(alarm);
        RefreshAlarmList(group);
        RefreshGroupList();

        StatusText.Text = AppLocalization.Service.Format("alarm.deleted", alarm.Name);
        AlarmConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnAlarmSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (AlarmList.SelectedItem is ListBoxItem item && item.Tag is AlarmConfig alarm)
        {
            PopulateEditPanel(alarm);
        }
        else
        {
            EditPanel.IsVisible = false;
            _currentAlarm = null;
        }
    }

    // ──────────────────────────────────────────────
    //  编辑面板
    // ──────────────────────────────────────────────

    private void PopulateEditPanel(AlarmConfig alarm)
    {
        _currentAlarm = alarm;
        var wasUpdating = _isUpdating;   // 可重入：OnAlarmFieldChanged 里会经选中变化再进来
        _isUpdating = true;

        // 基本信息
        TxtAlarmName.Text = alarm.Name;
        TxtAlarmDescription.Text = alarm.Description ?? "";
        TxtAlarmCause.Text = alarm.Cause ?? "";
        TxtAlarmResponse.Text = alarm.Response ?? "";
        TxtAlarmConsequence.Text = alarm.Consequence ?? "";
        // 允许响应时间（ISA-18.2 切片㈡；阶段 292）：**可空** ⇒ 空串表示"用该严重度的默认值"
        TxtAllowedResponseMs.Text = alarm.AllowedResponseMs?.ToString() ?? "";
        ChkEnabled.IsChecked = alarm.IsEnabled;
        ChkRequiresAck.IsChecked = alarm.RequiresAck;

        // 严重级别
        foreach (var item in CboSeverity.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag is AlarmSeverity sev && sev == alarm.Severity)
            {
                CboSeverity.SelectedItem = item;
                break;
            }
        }

        // 触发条件 - 变量
        foreach (var item in CboVariableId.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag is string vid && vid == alarm.Condition.VariableId)
            {
                CboVariableId.SelectedItem = item;
                break;
            }
        }

        // 触发条件 - 类型
        foreach (var item in CboConditionType.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Tag is AlarmConditionType ct && ct == alarm.Condition.Type)
            {
                CboConditionType.SelectedItem = item;
                break;
            }
        }

        // 条件数值字段
        TxtThreshold.Text = alarm.Condition.Threshold.ToString("F2");
        TxtDeadband.Text = alarm.Condition.Deadband.ToString("F2");
        TxtDelayMs.Text = alarm.Condition.DelayMs.ToString();
        TxtBitOffset.Text = alarm.Condition.BitOffset.ToString();
        TxtExpectedValue.Text = alarm.Condition.ExpectedValue.ToString("F2");

        UpdateConditionalFieldVisibility(alarm.Condition.Type);

        EditPanel.IsVisible = true;
        _isUpdating = wasUpdating;
    }

    private void UpdateConditionalFieldVisibility(AlarmConditionType type)
    {
        RowThreshold.IsVisible = type is AlarmConditionType.HighLimit
                                     or AlarmConditionType.LowLimit
                                     or AlarmConditionType.Deviation;
        RowBitOffset.IsVisible = type == AlarmConditionType.BitTrigger;
        RowExpectedValue.IsVisible = type is AlarmConditionType.StateChange
                                         or AlarmConditionType.Deviation;
    }

    // ──────────────────────────────────────────────
    //  字段变更
    // ──────────────────────────────────────────────

    private void OnAlarmFieldChanged(object? sender, EventArgs e)
    {
        if (_isUpdating || _currentAlarm == null) return;

        _isUpdating = true;

        var alarm = _currentAlarm;

        // 基本信息
        alarm.Name = TxtAlarmName.Text ?? "";
        alarm.Description = TxtAlarmDescription.Text;
        // ISA-18.2 职责字段（阶段 266）：面板**原样**写回（不裁剪）—— 与名称/描述同一口径；
        // 主报警表导出时才 `Trim`（那是显示口径，见 AlarmPhilosophyReport.Cell）。
        alarm.Cause = TxtAlarmCause.Text;
        alarm.Response = TxtAlarmResponse.Text;
        alarm.Consequence = TxtAlarmConsequence.Text;
        // ISA-18.2 切片㈡（阶段 292）：允许响应时间（ms）。**空 ⇒ null**（用严重度默认值）；
        // 非法文本 ⇒ **保持原值**（与兄弟数值字段"TryParse 失败就不写"同口径）。
        // ⚠ 面板**不算**优先级：那是 Core `AlarmPriority` 的职责（存字段会陈旧）。
        var allowedText = TxtAllowedResponseMs.Text;
        if (string.IsNullOrWhiteSpace(allowedText)) alarm.AllowedResponseMs = null;
        else if (int.TryParse(allowedText, out var allowedMs)) alarm.AllowedResponseMs = allowedMs;
        alarm.IsEnabled = ChkEnabled.IsChecked ?? true;
        alarm.RequiresAck = ChkRequiresAck.IsChecked ?? true;

        // 严重级别
        if (CboSeverity.SelectedItem is ComboBoxItem sevItem && sevItem.Tag is AlarmSeverity severity)
            alarm.Severity = severity;

        // 变量
        if (CboVariableId.SelectedItem is ComboBoxItem varItem && varItem.Tag is string varId)
            alarm.Condition.VariableId = varId;

        // 条件类型
        if (CboConditionType.SelectedItem is ComboBoxItem ctItem && ctItem.Tag is AlarmConditionType ct)
        {
            alarm.Condition.Type = ct;
            UpdateConditionalFieldVisibility(ct);
        }

        // 数值字段
        if (double.TryParse(TxtThreshold.Text, out var threshold))
            alarm.Condition.Threshold = threshold;
        if (double.TryParse(TxtDeadband.Text, out var deadband))
            alarm.Condition.Deadband = deadband;
        if (int.TryParse(TxtDelayMs.Text, out var delayMs))
            alarm.Condition.DelayMs = delayMs;
        if (int.TryParse(TxtBitOffset.Text, out var bitOffset))
            alarm.Condition.BitOffset = Math.Clamp(bitOffset, 0, 63);   // 见 AlarmEngine：位移按 64 取模
        if (double.TryParse(TxtExpectedValue.Text, out var expectedValue))
            alarm.Condition.ExpectedValue = expectedValue;

        // 刷新报警列表项（更新颜色/状态显示）
        if (GroupList.SelectedItem is ListBoxItem groupItem && groupItem.Tag is AlarmGroup group)
        {
            var idx = 0;
            foreach (var item in AlarmList.Items)
            {
                if (item is ListBoxItem lbi && lbi.Tag == alarm)
                {
                    AlarmList.Items[idx] = CreateAlarmListItem(alarm);
                    AlarmList.SelectedItem = AlarmList.Items[idx];
                    break;
                }
                idx++;
            }

            // 更新左侧组列表项中的报警计数（不触发 SelectionChanged）
            foreach (var gi in GroupList.Items)
            {
                if (gi is AlarmGroupRow row && row.Group == group)
                {
                    row.CountText.Text = AppLocalization.Service.Format("alarm.count", group.Alarms.Count);
                    break;
                }
            }
        }

        StatusText.Text = AppLocalization.Service.Format("alarm.updated", alarm.Name);
        AlarmConfigChanged?.Invoke(this, EventArgs.Empty);

        _isUpdating = false;
    }

    private void OnConditionTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _currentAlarm == null) return;

        if (CboConditionType.SelectedItem is ComboBoxItem ctItem && ctItem.Tag is AlarmConditionType ct)
        {
            _currentAlarm.Condition.Type = ct;
            UpdateConditionalFieldVisibility(ct);
        }
    }
}
