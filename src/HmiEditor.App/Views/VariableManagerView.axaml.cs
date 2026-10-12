using Avalonia.Controls;
using Avalonia.Media;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using System.Linq;
using HmiEditor.App.Resources;
using HmiEditor.App.Controls;

namespace HmiEditor.App.Views;

public partial class VariableManagerView : ChromeWindow
{
    private readonly VariableManager _variableManager;
    private VariableGroup? _selectedGroup;
    private Variable? _selectedVariable;
    private UdtDefinition? _selectedUdt;
    private UdtField? _selectedField;
    private VariableAlarmConfig? _selectedAlarm;

    public VariableManagerView()
    {
        _variableManager = null!;
        InitializeComponent();
    }

    public VariableManagerView(VariableManager variableManager) : this()
    {
        _variableManager = variableManager;
        WireEvents();
        LoadGroups();
        RefreshUdtList();
        PopulateDataTypeCombo(CboDataType);
        PopulateDataTypeCombo(CboFieldType);
        PopulateAlarmCombos();
    }

    /// <summary>
    /// 报警类型下拉的「本地化键 ↔ 枚举」配对表（参考主流 HMI 软件报警类型）。
    ///
    /// <para><b>为什么把它提出来</b>：这一层里"标签"和"Tag"是两个必须对齐的事实，而写串了
    /// （例如把 <c>HighLimit</c> 配上"下限报警"）**没有任何东西会报** ——
    /// 界面上只是安静地下拉出一个错的类型名，而 Tag 仍然正确，所以连保存的数据都是对的。
    /// 提成 <c>internal</c> 字段后就能让测试断言配对（见
    /// <c>AlarmOptionLocalizationTests</c>）。</para>
    /// </summary>
    internal static readonly (string LabelKey, AlarmConditionType Type)[] AlarmTypeOptions =
    {
        (Lang.VarAlarmTypeHigh, AlarmConditionType.HighLimit),
        (Lang.VarAlarmTypeLow, AlarmConditionType.LowLimit),
        (Lang.VarAlarmTypeHighHigh, AlarmConditionType.HighHighLimit),
        (Lang.VarAlarmTypeLowLow, AlarmConditionType.LowLowLimit),
        (Lang.VarAlarmTypeBit, AlarmConditionType.BitTrigger),
        (Lang.VarAlarmTypeDeviation, AlarmConditionType.Deviation),
        (Lang.VarAlarmTypeStateChange, AlarmConditionType.StateChange),
        (Lang.VarAlarmTypeRateOfChange, AlarmConditionType.RateOfChange),
    };

    /// <summary>报警级别下拉的「本地化键 ↔ 枚举」配对表（理由同上）。</summary>
    internal static readonly (string LabelKey, AlarmSeverity Severity)[] AlarmSeverityOptions =
    {
        (Lang.VarAlarmSeverityInfo, AlarmSeverity.Info),
        (Lang.VarAlarmSeverityWarning, AlarmSeverity.Warning),
        (Lang.VarAlarmSeverityCritical, AlarmSeverity.Critical),
        (Lang.VarAlarmSeverityEmergency, AlarmSeverity.Emergency),
    };

    /// <summary>构造报警类型下拉项 —— **标签在这一层解析**（键 → 当前语言的文案）。</summary>
    internal static IEnumerable<(string Label, object Tag)> BuildAlarmTypeItems() =>
        AlarmTypeOptions.Select(o => (AppLocalization.Service[o.LabelKey], (object)o.Type));

    /// <summary>构造报警级别下拉项（同上）。</summary>
    internal static IEnumerable<(string Label, object Tag)> BuildAlarmSeverityItems() =>
        AlarmSeverityOptions.Select(o => (AppLocalization.Service[o.LabelKey], (object)o.Severity));

    private void PopulateAlarmCombos()
    {
        PopulateTaggedCombo(CboAlarmType, BuildAlarmTypeItems());
        PopulateTaggedCombo(CboAlarmSeverity, BuildAlarmSeverityItems());
    }

    private void WireEvents()
    {
        // Group buttons
        BtnAddGroup.Click += (_, _) => AddGroup();
        BtnDeleteGroup.Click += (_, _) => DeleteGroup();
        BtnValidateIntegrity.Click += (_, _) => ValidateAndFixIntegrity();
        GroupList.SelectionChanged += (_, _) => OnGroupSelected();

        // Variable buttons
        BtnAddVariable.Click += (_, _) => AddVariable();
        BtnDeleteVariable.Click += (_, _) => DeleteVariable();
        BtnDuplicateVariable.Click += (_, _) => DuplicateVariable();
        VariableList.SelectionChanged += (_, _) => OnVariableSelected();
        BtnSaveVar.Click += (_, _) => SaveCurrentVariable();
        // 连接变更时联动刷新子设备下拉
        CboConnectionId.SelectionChanged += (_, _) => RefreshSlaveCombo();

        // ── 变量报警设置 ──
        BtnAddAlarm.Click += (_, _) => AddAlarm();
        BtnSaveAlarm.Click += (_, _) => SaveCurrentAlarm();
        BtnDeleteAlarm.Click += (_, _) => DeleteSelectedAlarm();
        AlarmList.SelectionChanged += (_, _) => OnAlarmSelected();

        // ── I4: UDT 类型管理 ──
        BtnAddUdt.Click += (_, _) => AddUdt();
        BtnDeleteUdt.Click += (_, _) => DeleteUdt();
        BtnSaveUdtName.Click += (_, _) => SaveUdtName();
        UdtList.SelectionChanged += (_, _) => OnUdtSelected();
        BtnAddField.Click += (_, _) => AddField();
        BtnCommitField.Click += (_, _) => CommitField();
        BtnDeleteField.Click += (_, _) => DeleteField();
        FieldList.SelectionChanged += (_, _) => OnFieldSelected();
        BtnCreateInstance.Click += (_, _) => CreateInstance();
    }

    private void LoadGroups()
    {
        GroupList.Items.Clear();
        foreach (var group in _variableManager.Groups)
            GroupList.Items.Add(CreateTaggedListItem(group, group.Name));

        // 孤儿变量（GroupId 为空或不匹配任何分组，如 AI 工具早期创建）归入虚拟"未分组"，保证可见可管理。
        var knownIds = _variableManager.Groups.Select(g => g.Id).ToHashSet();
        if (_variableManager.Variables.Any(v => !knownIds.Contains(v.GroupId)))
        {
            // 虚拟分组（Id 为空、不落盘）：名字只用于显示，跟随 UI 语言
            var ungrouped = AppLocalization.Service[Lang.VarPickerUngrouped];
            GroupList.Items.Add(CreateTaggedListItem(new VariableGroup { Id = "", Name = ungrouped }, ungrouped));
        }

        UpdateCounts();
    }

    private void AddGroup()
    {
        var group = new VariableGroup { Name = $"Group{_variableManager.Groups.Count + 1}" };
        _variableManager.Groups.Add(group);
        LoadGroups();
    }

    private void DeleteGroup()
    {
        if (_selectedGroup == null) return;
        // 虚拟"未分组"（Id 为空）不可删除，仅清空列表
        if (_selectedGroup.Id.Length == 0)
        {
            VariableList.Items.Clear();
            return;
        }
        var groupId = _selectedGroup.Id;
        // 级联清理：将属于该分组的变量移入虚拟"未分组"（GroupId 清空）
        var movedCount = _variableManager.Variables
            .Count(v => v.GroupId == groupId);
        foreach (var v in _variableManager.Variables.Where(v => v.GroupId == groupId))
            v.GroupId = "";

        _variableManager.Groups.Remove(_selectedGroup);
        _selectedGroup = null;
        VariableList.Items.Clear();
        LoadGroups();
    }

    private void OnGroupSelected()
    {
        if (GroupList.SelectedItem is ListBoxItem item && item.Tag is VariableGroup group)
        {
            _selectedGroup = group;
            RefreshVariables();
        }
    }

    private void RefreshVariables()
    {
        VariableList.Items.Clear();
        if (_selectedGroup == null) return;

        var varsInGroup = _variableManager.Variables
            .Where(v => v.GroupId == _selectedGroup.Id)
            .ToList();

        foreach (var variable in varsInGroup)
        {
            var fullLabel = $"{variable.Name} ({variable.DataType})";
            VariableList.Items.Add(CreateTaggedListItem(variable, fullLabel));
        }
        UpdateCounts();
    }

    // ── 通用 UI 初始化 helper（集中处理重复的列表/下拉初始化逻辑） ──

    /// <summary>创建带 Tag 的列表项；Content 为文本时自动用省略号截断并附加完整名 ToolTip。</summary>
    private static ListBoxItem CreateTaggedListItem(object tag, object? content = null)
        => new()
        {
            Content = content is string s ? CreateTrimmedLabel(s) : (content ?? tag),
            Tag = tag,
            Foreground = HmiEditor.App.Theme.TextPrimary
        };

    /// <summary>按 <b>Id</b> 在 ListBox 中选中对应项（列表项的 Tag 一律是 <see cref="IIdentifiedModel"/>）；未匹配则取消选中。</summary>
    /// <remarks>
    /// ⚠ **比对的是 Tag 的 <c>Id</c>，不是 <c>ToString()</c>** —— 这些模型都没覆写 <c>ToString()</c>，
    /// 拿它去比 Id **恒不相等** ⇒ 选中静默失败（登记 #64：「新建后自动选中」声明过但不存在）。
    /// ⚠ 也**别**改成按引用同一性比：「未分组」那一项每次刷新都**新建实例**，旧引用反而匹配不上。
    /// </remarks>
    private static void SelectListItemByTag(ListBox list, string? tag)
    {
        if (tag == null)
        {
            list.SelectedItem = null;
            return;
        }
        foreach (var item in list.Items)
        {
            if (item is ListBoxItem { Tag: IIdentifiedModel model } && model.Id == tag)
            {
                list.SelectedItem = item;
                return;
            }
        }
        list.SelectedItem = null;
    }

    /// <summary>用数据源填充 ComboBox 的 Tag 项（每项显示 label、承载 value 于 Tag）；返回 true 表示填充了至少一项。</summary>
    private static void PopulateTaggedCombo(ComboBox cbo, IEnumerable<(string Label, object Tag)> options)
    {
        cbo.Items.Clear();
        foreach (var (label, tag) in options)
            cbo.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
        if (cbo.Items.Count > 0)
            cbo.SelectedIndex = 0;
    }

    /// <summary>按 Tag 值选中 ComboBox 对应项（Tag 可为 string 或枚举等任意对象）。</summary>
    private static void SelectComboBoxByTag(ComboBox cbo, object? tag)
    {
        for (int i = 0; i < cbo.Items.Count; i++)
        {
            if (cbo.Items[i] is ComboBoxItem cbi && cbi.Tag?.ToString() == tag?.ToString())
            {
                cbo.SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>为 DataType 下拉框填充 7 种数据类型选项（CboDataType 与 CboFieldType 共用，避免重复定义）。</summary>
    private static void PopulateDataTypeCombo(ComboBox cbo)
    {
        cbo.Items.Clear();
        cbo.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.DataTypeBool], Tag = "Bool" });
        cbo.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.DataTypeInt16], Tag = "Int16" });
        cbo.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.DataTypeInt32], Tag = "Int32" });
        cbo.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.DataTypeUInt16], Tag = "UInt16" });
        cbo.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.DataTypeUInt32], Tag = "UInt32" });
        cbo.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.DataTypeFloat32], Tag = "Float32" });
        cbo.Items.Add(new ComboBoxItem { Content = AppLocalization.Service[Lang.DataTypeString], Tag = "String" });
    }

    /// <summary>创建带省略号截断 + 悬停完整名提示的列表项文本。</summary>
    private static TextBlock CreateTrimmedLabel(string text)
    {
        // TextBlock 的属性路径使用 ToolTip.SetTip 附加属性设置。
        var label = new TextBlock
        {
            Text = text,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ToolTip.SetTip(label, text);
        return label;
    }

    private void OnVariableSelected()
    {
        if (VariableList.SelectedItem is ListBoxItem item && item.Tag is Variable variable)
        {
            _selectedVariable = variable;
            LoadVariableToForm(variable);
        }
    }

    private void LoadVariableToForm(Variable v)
    {
        TxtVarName.Text = v.Name;
        SelectComboBoxByTag(CboDataType, v.DataType);
        TxtVarAddress.Text = v.Address;

        // ConnectionId: 动态加载连接列表
        var connOptions = new List<(string Label, object Tag)>
        {
            (AppLocalization.Service["var.unassigned"], "")
        };
        foreach (var conn in _variableManager.Connections)
            connOptions.Add((conn.Name, conn.Id));
        PopulateTaggedCombo(CboConnectionId, connOptions);
        SelectComboBoxByTag(CboConnectionId, v.ConnectionId);
        if (CboConnectionId.SelectedIndex < 0)
            CboConnectionId.SelectedIndex = 0;

        // SlaveId: 根据连接加载该连接下的子设备
        RefreshSlaveCombo(v);

        TxtVarUnit.Text = v.Unit ?? "";
        TxtVarScale.Text = v.Scale.ToString("F1");
        TxtVarPollRate.Text = v.PollRateMs.ToString();
        TxtVarSimValue.Text = v.SimulatedValue ?? "";

        // ── 变量报警设置 ──
        RefreshAlarmList();
    }

    /// <summary>刷新变量表单的子设备下拉（按当前连接过滤；并选中变量的 SlaveId）。</summary>
    private void RefreshSlaveCombo(Variable? v = null)
    {
        v ??= _selectedVariable;
        var connId = v?.ConnectionId ?? "";
        var slaveOptions = new List<(string Label, object Tag)>
        {
            (AppLocalization.Service["var.unassigned"], "")
        };
        var conn = _variableManager.Connections.FirstOrDefault(c => c.Id == connId);
        if (conn != null)
        {
            foreach (var slave in conn.Slaves)
                slaveOptions.Add(($"{slave.Name} {AppLocalization.Service.Format(Lang.VarSlaveStation, slave.SlaveAddress)}", slave.Id));
        }
        PopulateTaggedCombo(CboSlaveId, slaveOptions);
        if (v != null)
            SelectComboBoxByTag(CboSlaveId, v.SlaveId);
    }

    // ── 变量报警设置 ──

    private void RefreshAlarmList()
    {
        AlarmList.Items.Clear();
        _selectedAlarm = null;
        if (_selectedVariable == null) return;

        foreach (var alarm in _selectedVariable.Alarms)
        {
            var typeName = AlarmTypeDisplayName(alarm.Type);
            var label = $"{alarm.Name} · {typeName}";
            if (!alarm.IsEnabled) label += AppLocalization.Service[Lang.VarAlarmDisabledSuffix];
            AlarmList.Items.Add(CreateTaggedListItem(alarm, label));
        }
    }

    /// <summary>报警类型的**短显示名**（报警列表里拼 <c>名称 · 类型</c> 用），纯显示。</summary>
    private static string AlarmTypeDisplayName(AlarmConditionType type) => type switch
    {
        AlarmConditionType.HighLimit => AppLocalization.Service[Lang.VarAlarmShortHigh],
        AlarmConditionType.LowLimit => AppLocalization.Service[Lang.VarAlarmShortLow],
        AlarmConditionType.HighHighLimit => AppLocalization.Service[Lang.VarAlarmShortHighHigh],
        AlarmConditionType.LowLowLimit => AppLocalization.Service[Lang.VarAlarmShortLowLow],
        AlarmConditionType.BitTrigger => AppLocalization.Service[Lang.VarAlarmShortBit],
        AlarmConditionType.Deviation => AppLocalization.Service[Lang.VarAlarmShortDeviation],
        AlarmConditionType.StateChange => AppLocalization.Service[Lang.VarAlarmShortStateChange],
        AlarmConditionType.RateOfChange => AppLocalization.Service[Lang.VarAlarmShortRateOfChange],
        _ => AppLocalization.Service[Lang.VarAlarmShortUnknown],
    };

    private void AddAlarm()
    {
        if (_selectedVariable == null)
        {
            StatusText.Text = AppLocalization.Service["var.select.first"];
            return;
        }
        var alarm = new VariableAlarmConfig
        {
            Name = AppLocalization.Service.Format(Lang.VarAlarmDefaultName, _selectedVariable.Alarms.Count + 1),
            Type = AlarmConditionType.HighLimit,
            Threshold = 0,
        };
        _selectedVariable.Alarms.Add(alarm);
        RefreshAlarmList();
        SelectListItemByTag(AlarmList, alarm.Id);
        StatusText.Text = AppLocalization.Service["var.alarm.added"];
    }

    private void OnAlarmSelected()
    {
        if (AlarmList.SelectedItem is ListBoxItem item && item.Tag is VariableAlarmConfig alarm)
        {
            _selectedAlarm = alarm;
            TxtAlarmName.Text = alarm.Name;
            SelectComboBoxByTag(CboAlarmType, alarm.Type);
            TxtAlarmThreshold.Text = alarm.Threshold.ToString("0.###");
            TxtAlarmDeadband.Text = alarm.Deadband.ToString("0.###");
            TxtAlarmDelay.Text = alarm.DelayMs.ToString();
            TxtAlarmBitOffset.Text = alarm.BitOffset.ToString();
            TxtAlarmExpected.Text = alarm.ExpectedValue.ToString("0.###");
            SelectComboBoxByTag(CboAlarmSeverity, alarm.Severity);
            TxtAlarmMessage.Text = alarm.Message ?? "";
            ChkAlarmEnabled.IsChecked = alarm.IsEnabled;
            ChkAlarmRequiresAck.IsChecked = alarm.RequiresAck;
        }
    }

    private void SaveCurrentAlarm()
    {
        if (_selectedVariable == null || _selectedAlarm == null)
        {
            StatusText.Text = AppLocalization.Service["var.alarm.select.first"];
            return;
        }

        _selectedAlarm.Name = string.IsNullOrWhiteSpace(TxtAlarmName.Text)
            ? AppLocalization.Service[Lang.VarAlarmUnnamed]
            : TxtAlarmName.Text.Trim();
        if (CboAlarmType.SelectedItem is ComboBoxItem typeCbi)
            _selectedAlarm.Type = (AlarmConditionType)(typeCbi.Tag ?? AlarmConditionType.HighLimit);
        double.TryParse(TxtAlarmThreshold.Text, out var threshold); _selectedAlarm.Threshold = threshold;
        double.TryParse(TxtAlarmDeadband.Text, out var deadband); _selectedAlarm.Deadband = deadband;
        int.TryParse(TxtAlarmDelay.Text, out var delay); _selectedAlarm.DelayMs = Math.Max(0, delay);
        int.TryParse(TxtAlarmBitOffset.Text, out var bitOffset);
        _selectedAlarm.BitOffset = Math.Clamp(bitOffset, 0, 63);   // 与 AlarmEngine 的 0..63 一致（位移按 64 取模）
        double.TryParse(TxtAlarmExpected.Text, out var expected); _selectedAlarm.ExpectedValue = expected;
        if (CboAlarmSeverity.SelectedItem is ComboBoxItem sevCbi)
            _selectedAlarm.Severity = (AlarmSeverity)(sevCbi.Tag ?? AlarmSeverity.Warning);
        _selectedAlarm.Message = string.IsNullOrWhiteSpace(TxtAlarmMessage.Text) ? null : TxtAlarmMessage.Text.Trim();
        _selectedAlarm.IsEnabled = ChkAlarmEnabled.IsChecked == true;
        _selectedAlarm.RequiresAck = ChkAlarmRequiresAck.IsChecked == true;

        RefreshAlarmList();
        SelectListItemByTag(AlarmList, _selectedAlarm.Id);
        StatusText.Text = AppLocalization.Service.Format("var.alarm.saved", _selectedAlarm.Name);
    }

    private void DeleteSelectedAlarm()
    {
        if (_selectedVariable == null || _selectedAlarm == null)
        {
            StatusText.Text = AppLocalization.Service["var.alarm.select.first"];
            return;
        }
        _selectedVariable.Alarms.Remove(_selectedAlarm);
        _selectedAlarm = null;
        RefreshAlarmList();
        StatusText.Text = AppLocalization.Service["var.alarm.deleted"];
    }

    private void SaveCurrentVariable()
    {
        if (_selectedVariable == null) return;
        _selectedVariable.Name = TxtVarName.Text?.Trim() ?? "";
        _selectedVariable.Address = TxtVarAddress.Text?.Trim() ?? "";

        // ConnectionId
        if (CboConnectionId.SelectedItem is ComboBoxItem connCbi)
            _selectedVariable.ConnectionId = connCbi.Tag?.ToString() ?? "";

        // SlaveId（连接变更后子设备下拉已联动刷新）
        if (CboSlaveId.SelectedItem is ComboBoxItem slaveCbi)
            _selectedVariable.SlaveId = slaveCbi.Tag?.ToString() ?? "";

        _selectedVariable.Unit = string.IsNullOrWhiteSpace(TxtVarUnit.Text) ? null : TxtVarUnit.Text.Trim();
        double.TryParse(TxtVarScale.Text, out var scale); _selectedVariable.Scale = scale;
        int.TryParse(TxtVarPollRate.Text, out var pr); _selectedVariable.PollRateMs = pr > 0 ? pr : 500;

        if (CboDataType.SelectedItem is ComboBoxItem cbi)
            _selectedVariable.DataType = cbi.Tag?.ToString() ?? "Int16";

        // SimulatedValue
        _selectedVariable.SimulatedValue = string.IsNullOrWhiteSpace(TxtVarSimValue.Text) ? null : TxtVarSimValue.Text.Trim();

        RefreshVariables();
        StatusText.Text = AppLocalization.Service.Format("var.saved", _selectedVariable.Name);
    }

    private void AddVariable()
    {
        // 如果没有选中分组，先创建一个默认分组并自动选中，
        // 避免用户首次点击「添加变量」时只看到新建了分组却未添加变量，误以为无响应。
        if (_selectedGroup == null)
        {
            if (_variableManager.Groups.Count == 0)
            {
                AddGroup();
            }

            _selectedGroup = _variableManager.Groups.FirstOrDefault();
            if (_selectedGroup == null) return;

            // 在 UI 中同步选中该分组
            SelectListItemByTag(GroupList, _selectedGroup.Id);
        }

        var variable = new Variable
        {
            Name = $"Var{_variableManager.Variables.Count + 1}",
            DataType = "Int16",
            GroupId = _selectedGroup.Id
        };
        _variableManager.Variables.Add(variable);
        RefreshVariables();

        // 自动选中新添加的变量，提升反馈感
        SelectListItemByTag(VariableList, variable.Id);

        StatusText.Text = AppLocalization.Service.Format("var.added", variable.Name);
    }

    private void DeleteVariable()
    {
        if (_selectedVariable == null) return;
        _variableManager.Variables.Remove(_selectedVariable);
        _selectedVariable = null;
        RefreshVariables();
        StatusText.Text = AppLocalization.Service["var.deleted"];
    }

    private void DuplicateVariable()
    {
        if (_selectedVariable == null) return;
        var clone = new Variable
        {
            Name = $"{_selectedVariable.Name}_Copy",
            DataType = _selectedVariable.DataType,
            Address = _selectedVariable.Address,
            Unit = _selectedVariable.Unit,
            Scale = _selectedVariable.Scale,
            Offset = _selectedVariable.Offset,
            PollRateMs = _selectedVariable.PollRateMs,
            GroupId = _selectedVariable.GroupId,
            ConnectionId = _selectedVariable.ConnectionId,
            SimulatedValue = _selectedVariable.SimulatedValue
        };
        _variableManager.Variables.Add(clone);
        RefreshVariables();
        StatusText.Text = AppLocalization.Service.Format("var.copied", clone.Name);
    }

    private void UpdateCounts()
    {
        LblTitle.Text = AppLocalization.Service.Format("var.list.title.count", _variableManager.Variables.Count);
    }

    // ── I4: UDT 类型管理 ──

    /// <summary>刷新 UDT 定义列表与实例化下拉框。</summary>
    private void RefreshUdtList()
    {
        var selectedId = _selectedUdt?.Id;

        UdtList.Items.Clear();
        foreach (var def in _variableManager.UdtDefinitions)
            UdtList.Items.Add(CreateTaggedListItem(def, def.Name));
        SelectListItemByTag(UdtList, selectedId);

        LblUdtTitle.Text = AppLocalization.Service.Format("var.udt.title.count", _variableManager.UdtDefinitions.Count);

        // 实例化下拉框
        var udtOptions = _variableManager.UdtDefinitions
            .Select(def => (def.Name, (object)def))
            .ToList();
        PopulateTaggedCombo(CboUdtInstanceType, udtOptions);

        if (_selectedUdt == null && UdtList.Items.Count > 0)
            UdtList.SelectedIndex = 0;
        else if (_selectedUdt == null)
            RefreshFields();
    }

    private void AddUdt()
    {
        var def = new UdtDefinition { Name = $"Type{_variableManager.UdtDefinitions.Count + 1}" };
        _variableManager.UdtDefinitions.Add(def);
        _selectedUdt = def;
        RefreshUdtList();

        // 选中新定义
        SelectListItemByTag(UdtList, def.Id);
        UdtStatusText.Text = AppLocalization.Service.Format("var.udt.created.type", def.Name);
    }

    private void DeleteUdt()
    {
        if (_selectedUdt == null) return;

        // 名字先捕获：下面会把 _selectedUdt 置 null，之后再取名字只能拿到 null
        var deletedUdtName = _selectedUdt.Name;

        if (!_variableManager.RemoveUdtDefinition(_selectedUdt.Id))
        {
            UdtStatusText.Text = AppLocalization.Service.Format("var.udt.cannot.delete", deletedUdtName);
            return;
        }

        _selectedUdt = null;
        _selectedField = null;
        RefreshUdtList();
        UdtStatusText.Text = AppLocalization.Service.Format("var.udt.deleted.type", deletedUdtName);
    }

    private void SaveUdtName()
    {
        if (_selectedUdt == null) return;
        var name = TxtUdtName.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            UdtStatusText.Text = AppLocalization.Service["var.udt.name.empty"];
            return;
        }
        _selectedUdt.Name = name;
        RefreshUdtList();
        UdtStatusText.Text = AppLocalization.Service.Format("var.udt.saved.name", name);
    }

    private void OnUdtSelected()
    {
        if (UdtList.SelectedItem is ListBoxItem item && item.Tag is UdtDefinition def)
        {
            _selectedUdt = def;
            _selectedField = null;
            TxtUdtName.Text = def.Name;
            RefreshFields();
        }
    }

    /// <summary>刷新当前 UDT 的字段列表。</summary>
    private void RefreshFields()
    {
        FieldList.Items.Clear();
        if (_selectedUdt == null)
        {
            TxtUdtName.Text = "";
            return;
        }

        foreach (var field in _selectedUdt.Fields)
            FieldList.Items.Add(CreateTaggedListItem(field, $"{field.Name} ({field.DataType})"));
    }

    private void AddField()
    {
        if (_selectedUdt == null)
        {
            UdtStatusText.Text = AppLocalization.Service["var.udt.select.or.create"];
            return;
        }
        // 清空编辑区，准备输入
        TxtFieldName.Text = "";
        if (CboFieldType.Items.Count > 0) CboFieldType.SelectedIndex = 0;
        TxtFieldName.Focus();
        UdtStatusText.Text = AppLocalization.Service["var.udt.enter.field.hint"];
    }

    private void CommitField()
    {
        if (_selectedUdt == null)
        {
            UdtStatusText.Text = AppLocalization.Service["var.udt.select.or.create"];
            return;
        }

        var name = TxtFieldName.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            UdtStatusText.Text = AppLocalization.Service["var.udt.field.name.empty"];
            return;
        }

        var dataType = CboFieldType.SelectedItem is ComboBoxItem cbi
            ? cbi.Tag?.ToString() ?? "Int16"
            : "Int16";

        // 同名存在 → 更新；否则新增
        var existing = _selectedUdt.FindField(name);
        if (existing != null)
        {
            existing.DataType = dataType;
            UdtStatusText.Text = AppLocalization.Service.Format("var.udt.updated.field", name);
        }
        else
        {
            _selectedUdt.AddField(name, dataType);
            UdtStatusText.Text = AppLocalization.Service.Format("var.udt.added.field", name, dataType);
        }

        RefreshFields();
    }

    private void DeleteField()
    {
        if (_selectedUdt == null || _selectedField == null) return;

        // 名字先捕获：下面会把 _selectedField 置 null，之后再取只能拿到 null
        var deletedFieldName = _selectedField.Name;

        _selectedUdt.RemoveField(_selectedField);
        _selectedField = null;
        RefreshFields();
        UdtStatusText.Text = AppLocalization.Service.Format("var.udt.deleted.field", deletedFieldName);
    }

    private void OnFieldSelected()
    {
        if (FieldList.SelectedItem is ListBoxItem item && item.Tag is UdtField field)
        {
            _selectedField = field;
            TxtFieldName.Text = field.Name;
            SelectComboBoxByTag(CboFieldType, field.DataType);
        }
    }

    /// <summary>创建 UDT 实例（点号寻址: 实例名.字段名）。</summary>
    private void CreateInstance()
    {
        if (CboUdtInstanceType.SelectedItem is not ComboBoxItem cbi || cbi.Tag is not UdtDefinition def)
        {
            UdtStatusText.Text = AppLocalization.Service["var.udt.create.udt.first"];
            return;
        }

        var instanceName = TxtInstanceName.Text?.Trim();
        if (string.IsNullOrEmpty(instanceName))
        {
            UdtStatusText.Text = AppLocalization.Service["var.udt.instance.name.empty"];
            return;
        }

        if (_variableManager.Variables.Any(v => string.Equals(v.Name, instanceName, StringComparison.Ordinal)))
        {
            UdtStatusText.Text = AppLocalization.Service.Format("var.udt.instance.exists", instanceName);
            return;
        }

        var instance = _variableManager.CreateUdtInstance(def, instanceName);
        UdtStatusText.Text = AppLocalization.Service.Format("var.udt.created.instance", instanceName);
        TxtInstanceName.Text = "";
    }

    // ── 数据一致性校验 ──

    private void ValidateAndFixIntegrity()
    {
        var issues = _variableManager.ValidateIntegrity();
        if (issues.Count == 0)
        {
            StatusText.Text = AppLocalization.Service["var.integrity.ok"];
            return;
        }

        // 自动修复
        var fixedCount = _variableManager.AutoFixIntegrityIssues();
        LoadGroups();
        RefreshVariables();

        var connOrphans = issues.Count(i => i.IssueType == "OrphanedConnection");
        var groupOrphans = issues.Count(i => i.IssueType == "OrphanedGroup");
        StatusText.Text = AppLocalization.Service.Format("var.integrity.fixed", fixedCount, connOrphans, groupOrphans);
    }
}
