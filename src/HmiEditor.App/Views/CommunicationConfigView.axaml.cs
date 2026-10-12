using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Hmi.Communication.Contracts;
using Hmi.Communication.Builtin;
using Hmi.Communication.Loader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HmiEditor.App.Resources;
using HmiEditor.App.Controls;
using HmiEditor.App;

using HmiEditor.App.Services;

namespace HmiEditor.App.Views;

/// <summary>
/// 通讯配置对话框 —— 参考主流组态软件（组态王/WinCC/MCGS）的层级结构：
/// 连接（树根）→ 子设备（从站，子节点）→ 连接参数 / 子设备设置 / 子设备变量。
/// 左侧为树形导航（连接含子设备子节点），支持右键菜单添加连接/子设备/删除。
/// </summary>
public partial class CommunicationConfigView : ChromeWindow
{
    private readonly VariableManager _variableManager;
    private readonly HmiProject _project;
    private readonly CommunicationDriverRegistry _driverRegistry;
    private DeviceConnection? _selectedConnection;
    private DeviceSlave? _selectedSlave;
    private bool _isUpdating;
    private bool _isUpdatingTree;

    /// <summary>驱动类型 → 图标映射</summary>
    private static readonly Dictionary<string, string> DriverIcons = new()
    {
        ["modbus-rtu"] = "📡",
        ["modbus-tcp"] = "🌐",
        ["opc-ua"] = "🔌",
        ["mqtt"] = "📨",
    };

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public CommunicationConfigView()
    {
        _variableManager = null!;
        _project = null!;
        _driverRegistry = null!;
        InitializeComponent();
    }

    public CommunicationConfigView(VariableManager variableManager, HmiProject project) : this()
    {
        _variableManager = variableManager;
        _project = project;
        _driverRegistry = new CommunicationDriverRegistry();

        // 加载内置驱动（清单来自 BuiltinDriverCatalog —— 与 Agent 工具的 driver 枚举同源）
        _driverRegistry.LoadBuiltinDrivers(BuiltinDriverCatalog.CreateAll());

        WireEvents();
        PopulateDriverCombo();
        PopulateSlaveVarTypeCombo();
        PopulateSlaveVarRegTypeCombo();
        PopulateBatchCombos();
        RefreshConnectionList();
    }

    /// <summary>初始化批量添加区的类型/寄存器类型下拉，与单条添加区保持同步。</summary>
    private void PopulateBatchCombos()
    {
        // 类型下拉（复用单条添加的类型集合）
        foreach (var item in CboSlaveVarType.Items.OfType<ComboBoxItem>())
            CboBatchVarType.Items.Add(new ComboBoxItem { Content = item.Content, Tag = item.Tag });
        if (CboBatchVarType.Items.Count > 0) CboBatchVarType.SelectedIndex = 1;

        // 寄存器类型下拉
        foreach (var item in CboSlaveVarRegType.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is (string p, string k))
                CboBatchVarRegType.Items.Add(new ComboBoxItem { Content = item.Content, Tag = (p, k) });
        }
        if (CboBatchVarRegType.Items.Count > 0) CboBatchVarRegType.SelectedIndex = 0;
    }

    private void PopulateSlaveVarTypeCombo()
    {
        CboSlaveVarType.Items.Clear();
        string[] types = { "Bool", "Int16", "UInt16", "Int32", "UInt32", "Float32", "Float64", "String" };
        foreach (var t in types)
            CboSlaveVarType.Items.Add(new ComboBoxItem { Content = t, Tag = t });
        if (CboSlaveVarType.Items.Count > 0)
            CboSlaveVarType.SelectedIndex = 1; // 默认 Int16
    }

    /// <summary>
    /// Modbus 寄存器类型下拉（对标组态王/WinCC/MCGS 的寄存器选择）。
    /// Tag 为 (前缀, 说明)：0=线圈 1=离散输入 3=输入寄存器 4=保持寄存器。
    /// </summary>
    private void PopulateSlaveVarRegTypeCombo()
    {
        CboSlaveVarRegType.Items.Clear();
        CboSlaveVarRegType.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service["comm.slave.var.reg.holding"],
            Tag = (Prefix: "4", Kind: "holding"),
        });
        CboSlaveVarRegType.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service["comm.slave.var.reg.input"],
            Tag = (Prefix: "3", Kind: "input"),
        });
        CboSlaveVarRegType.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service["comm.slave.var.reg.discrete"],
            Tag = (Prefix: "1", Kind: "discrete"),
        });
        CboSlaveVarRegType.Items.Add(new ComboBoxItem
        {
            Content = AppLocalization.Service["comm.slave.var.reg.coil"],
            Tag = (Prefix: "0", Kind: "coil"),
        });
        if (CboSlaveVarRegType.Items.Count > 0)
            CboSlaveVarRegType.SelectedIndex = 0; // 默认保持寄存器
    }

    private void WireEvents()
    {
        BtnAddConnection.Click += (_, _) => OnAddConnection();
        BtnDeleteSelection.Click += (_, _) => OnDeleteSelection();
        CommTree.SelectionChanged += (_, _) => OnTreeSelectionChanged();
        CboDriverType.SelectionChanged += (_, _) => OnDriverTypeChanged();
        BtnTestConnection.Click += (_, _) => OnTestConnection();
        ChkConnEnabled.IsCheckedChanged += (_, _) => PersistConnectionEnabled();

        // 连接名失焦写回
        TxtConnName.LostFocus += (_, _) => PersistConnectionName();

        // ── 通讯树右键菜单 ──
        CtxAddConnection.Click += (_, _) => OnAddConnection();
        CtxAddSlave.Click += (_, _) => OnAddSlave();
        CtxDelete.Click += (_, _) => OnDeleteSelection();
        CommTreeContextMenu.Opening += (_, _) => UpdateContextMenuState();

        // ── 子设备操作 ──
        BtnAddSlave.Click += (_, _) => OnAddSlave();
        BtnSaveSlave.Click += (_, _) => OnSaveSlave();

        // ── 子设备变量操作 ──
        BtnAddSlaveVar.Click += (_, _) => OnAddSlaveVar();
        BtnSaveEditSlaveVar.Click += (_, _) => OnSaveEditSlaveVar();
        BtnDeleteSlaveVar.Click += (_, _) => OnDeleteSlaveVar();
        SlaveVarList.SelectionChanged += (_, _) => OnSlaveVarSelected();
        CboSlaveVarRegType.SelectionChanged += (_, _) => UpdateSlaveVarAddressHint();
        BtnBatchAddSlaveVars.Click += (_, _) => BatchAddPanel.IsVisible = !BatchAddPanel.IsVisible;
        BtnApplyBatchVars.Click += (_, _) => OnApplyBatchVars();
        BtnImportSlaveVars.Click += async (_, _) => await OnImportSlaveVarsAsync();
        BtnExportSlaveVars.Click += (_, _) => OnExportSlaveVars();
        // 批量添加表单变更 → 实时预览
        TxtBatchNamePrefix.TextChanged += (_, _) => UpdateBatchPreview();
        TxtBatchStartIndex.TextChanged += (_, _) => UpdateBatchPreview();
        TxtBatchStartAddr.TextChanged += (_, _) => UpdateBatchPreview();
        TxtBatchCount.TextChanged += (_, _) => UpdateBatchPreview();
        CboBatchVarType.SelectionChanged += (_, _) => UpdateBatchPreview();
        CboBatchVarRegType.SelectionChanged += (_, _) => UpdateBatchPreview();
    }

    /// <summary>右键菜单打开前：按当前选中项启用/禁用菜单项（连接节点可加子设备）。</summary>
    private void UpdateContextMenuState()
    {
        // 连接节点（未选子设备）：可添加子设备；否则禁用
        bool canAddSlave = _selectedConnection != null && _selectedSlave == null;
        CtxAddSlave.IsEnabled = canAddSlave;
        // 无选中时不显示删除（由 IsVisible 控制；此处简单置灰）
        bool hasSelection = _selectedConnection != null || _selectedSlave != null;
        CtxDelete.IsEnabled = hasSelection;
    }

    private void PersistConnectionName()
    {
        if (_selectedConnection == null) return;
        var newName = TxtConnName.Text?.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            TxtConnName.Text = _selectedConnection.Name;
            return;
        }
        if (newName == _selectedConnection.Name) return;

        _selectedConnection.Name = newName;
        RefreshConnectionList();
    }

    private void PersistConnectionEnabled()
    {
        if (_isUpdating || _selectedConnection == null) return;
        _selectedConnection.IsEnabled = ChkConnEnabled.IsChecked == true;
    }

    private void PopulateDriverCombo()
    {
        CboDriverType.Items.Clear();
        foreach (var driver in _driverRegistry.GetAllDrivers())
        {
            CboDriverType.Items.Add(new ComboBoxItem
            {
                Content = $"{LocalizeDriverName(driver.Metadata.Id)} — {LocalizeDriverDescription(driver.Metadata.Id, driver.Metadata.Description)}",
                Tag = driver.Metadata.Id,
            });
        }
        if (CboDriverType.Items.Count > 0)
            CboDriverType.SelectedIndex = 0;
    }

    /// <summary>驱动 ID（如 modbus-rtu）→ 本地化显示名。</summary>
    private static string LocalizeDriverName(string driverId)
    {
        var key = PluginTextKeys.DriverNameKey(driverId);
        var localized = AppLocalization.Service[key];
        return localized == key ? driverId : localized;
    }

    /// <summary>
    /// 驱动 ID → 本地化描述（<c>driver.&lt;id&gt;.description</c>），查不到回落插件自带描述。
    ///
    /// <para>与 <see cref="LocalizeDriverName"/> 同一套路：描述住在**插件程序集**里
    /// （拿不到 App 字典），所以键由 App 侧按驱动 ID 推导。没有这条查键时，
    /// 英文界面会直接显示插件里的中文描述 —— 属"用户可见文案不在扫描面内"的同族缺陷。</para>
    /// </summary>
    private static string LocalizeDriverDescription(string driverId, string pluginDescription)
    {
        var key = PluginTextKeys.DriverDescriptionKey(driverId);
        var localized = AppLocalization.Service[key];
        return localized == key ? pluginDescription : localized;
    }

    /// <summary>配置字段 key（如 baudRate）→ 本地化标签（param.baud.rate）。</summary>
    private static string LocalizeFieldLabel(ConfigField field)
    {
        var key = PluginTextKeys.FieldLabelKey(field.Key);
        var localized = AppLocalization.Service[key];
        return localized == key ? field.Label : localized;
    }

    // ── 通讯树（连接 + 子设备）操作 ──

    /// <summary>刷新通讯树：连接为根节点，子设备为子节点，并恢复选中状态。</summary>
    private void RefreshConnectionList()
    {
        var selectedConnId = _selectedConnection?.Id;
        var selectedSlaveId = _selectedSlave?.Id;

        _isUpdatingTree = true;
        try
        {
            CommTree.Items.Clear();
            foreach (var conn in _variableManager.Connections)
            {
                var node = new CommTreeNode { Connection = conn };
                foreach (var slave in conn.Slaves)
                    node.Children.Add(new CommTreeNode { Slave = slave });
                CommTree.Items.Add(node);
            }

            // 恢复选中：优先子设备，其次连接（通过数据对象选中，触发 SelectionChanged）
            if (selectedSlaveId != null)
            {
                var slaveNode = FindNodeBySlaveId(selectedSlaveId);
                if (slaveNode != null) { CommTree.SelectedItem = slaveNode; return; }
            }
            if (selectedConnId != null)
            {
                var connNode = FindNodeByConnectionId(selectedConnId);
                if (connNode != null) CommTree.SelectedItem = connNode;
            }
        }
        finally
        {
            _isUpdatingTree = false;
        }
    }

    /// <summary>按连接 Id 查找树节点。</summary>
    private CommTreeNode? FindNodeByConnectionId(string connId)
    {
        foreach (var item in CommTree.Items)
            if (item is CommTreeNode n && n.Connection?.Id == connId)
                return n;
        return null;
    }

    /// <summary>按子设备 Id 查找树节点（递归子节点）。</summary>
    private CommTreeNode? FindNodeBySlaveId(string slaveId)
    {
        foreach (var item in CommTree.Items)
        {
            if (item is CommTreeNode n)
            {
                if (n.Slave?.Id == slaveId) return n;
                var found = n.Children.FirstOrDefault(c => c.Slave?.Id == slaveId);
                if (found != null) return found;
            }
        }
        return null;
    }

    private void OnAddConnection()
    {
        var newConn = new DeviceConnection
        {
            Name = AppLocalization.Service.Format("comm.conn.new", _variableManager.Connections.Count + 1),
            DriverId = _driverRegistry.GetDriverIds().FirstOrDefault() ?? "modbus-rtu",
        };
        _variableManager.Connections.Add(newConn);
        RefreshConnectionList();
        _selectedConnection = newConn;
        _selectedSlave = null;
        SelectTreeByConnection(newConn.Id);
        OnConnectionSelected(newConn);
        StatusText.Text = AppLocalization.Service["comm.added"];
    }

    /// <summary>按连接 Id 选中树节点（用于新建连接后自动选中）。</summary>
    private void SelectTreeByConnection(string connId)
    {
        var node = FindNodeByConnectionId(connId);
        if (node != null)
            CommTree.SelectedItem = node;
    }

    /// <summary>按子设备 Id 选中树节点。</summary>
    private void SelectTreeBySlave(string slaveId)
    {
        var node = FindNodeBySlaveId(slaveId);
        if (node != null)
            CommTree.SelectedItem = node;
    }

    /// <summary>删除当前选中的连接或子设备（树节点或底部按钮触发）。</summary>
    private void OnDeleteSelection()
    {
        if (_selectedSlave != null)
        {
            OnDeleteSlave();
        }
        else if (_selectedConnection != null)
        {
            OnDeleteConnection();
        }
        else
        {
            StatusText.Text = AppLocalization.Service["comm.select.first"];
        }
    }

    private void OnDeleteConnection()
    {
        if (_selectedConnection == null)
        {
            StatusText.Text = AppLocalization.Service["comm.select.first"];
            return;
        }
        var connId = _selectedConnection.Id;
        if (_variableManager.Connections.Remove(_selectedConnection))
        {
            // 级联清理：引用此连接的变量清空 ConnectionId 与 SlaveId，避免孤儿引用
            var orphaned = _variableManager.Variables
                .Where(v => v.ConnectionId == connId)
                .ToList();
            foreach (var v in orphaned)
            {
                v.ConnectionId = "";
                v.SlaveId = "";
            }

            _selectedConnection = null;
            _selectedSlave = null;
            RefreshConnectionList();
            RefreshSlaveVars();
            ShowEmptyState();
            StatusText.Text = orphaned.Count > 0
                ? AppLocalization.Service.Format("comm.deleted.cascade", orphaned.Count)
                : AppLocalization.Service["comm.deleted"];
        }
    }

    /// <summary>通讯树选中变化：连接或子设备。</summary>
    private void OnTreeSelectionChanged()
    {
        if (_isUpdatingTree) return;   // 树刷新中，忽略中间态

        if (CommTree.SelectedItem is CommTreeNode node)
        {
            if (node.IsConnection)
            {
                _selectedSlave = null;
                OnConnectionSelected(node.Connection!);
            }
            else
            {
                _selectedSlave = node.Slave;
                _selectedConnection = _variableManager.Connections
                    .FirstOrDefault(c => c.Slaves.Contains(node.Slave!));
                OnSlaveSelected(node.Slave!);
                if (_selectedConnection != null)
                    OnConnectionSelected(_selectedConnection);
            }
        }
        else
        {
            _selectedConnection = null;
            _selectedSlave = null;
            ShowEmptyState();
        }
    }

    private void OnConnectionSelected(DeviceConnection conn)
    {
        _selectedConnection = conn;
        _isUpdating = true;
        try
        {
            TxtConnName.Text = conn.Name;
            ChkConnEnabled.IsChecked = conn.IsEnabled;

            var driverId = conn.DriverId;
            for (int i = 0; i < CboDriverType.Items.Count; i++)
            {
                if (CboDriverType.Items[i] is ComboBoxItem item && item.Tag is string id && id == driverId)
                {
                    CboDriverType.SelectedIndex = i;
                    break;
                }
            }

            RenderConfigFields(conn);
            BtnTestConnection.IsVisible = true;
            StatusText.Text = AppLocalization.Service.Format("comm.selected", conn.Name);
            StatusText.Foreground = HmiEditor.App.Theme.TextPrimary;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnDriverTypeChanged()
    {
        if (_isUpdating || _selectedConnection == null) return;

        if (CboDriverType.SelectedItem is ComboBoxItem item && item.Tag is string driverId)
        {
            _selectedConnection.DriverId = driverId;
            RenderConfigFields(_selectedConnection);
            RefreshConnectionList();
            StatusText.Text = AppLocalization.Service.Format("comm.driver.switched", driverId);
            StatusText.Foreground = HmiEditor.App.Theme.TextPrimary;
        }
    }

    private void RenderConfigFields(DeviceConnection connection)
    {
        ParamPanel.Children.Clear();

        var driver = _driverRegistry.GetDriver(connection.DriverId);
        if (driver == null)
        {
            TxtDriverDesc.Text = AppLocalization.Service.Format("comm.unknown.driver", connection.DriverId);
            return;
        }

        TxtDriverDesc.Text = LocalizeDriverDescription(driver.Metadata.Id, driver.Metadata.Description);

        var template = driver.GetConnectionConfigTemplate();
        foreach (var field in template.Fields)
        {
            var row = CreateConfigFieldRow(field, connection.Config);
            ParamPanel.Children.Add(row);
        }
    }

    private Grid CreateConfigFieldRow(ConfigField field, Dictionary<string, string> config)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("90,*"),
            Margin = new Thickness(0, 0, 0, 6),
        };

        var label = new TextBlock
        {
            Text = LocalizeFieldLabel(field),
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        grid.Children.Add(label);

        var currentValue = config.TryGetValue(field.Key, out var existing)
            ? existing
            : (field.Default ?? "");

        Control valueControl = field.Type switch
        {
            ConfigFieldType.Select => new ComboBox
            {
                Background = HmiEditor.App.Theme.BgPanel,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                BorderBrush = HmiEditor.App.Theme.InputBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4),
                FontSize = 13,
            },
            _ => new TextBox
            {
                Background = HmiEditor.App.Theme.BgPanel,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                BorderBrush = HmiEditor.App.Theme.InputBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 5),
                FontSize = 13,
                Text = currentValue,
                PlaceholderText = field.Default,
            },
        };

        if (valueControl is ComboBox cbo && field.Options != null)
        {
            foreach (var opt in field.Options)
                cbo.Items.Add(new ComboBoxItem { Content = opt });
            for (int i = 0; i < cbo.Items.Count; i++)
            {
                if (cbo.Items[i] is ComboBoxItem item && item.Content?.ToString() == currentValue)
                {
                    cbo.SelectedIndex = i;
                    break;
                }
            }
        }

        void SyncValue()
        {
            if (_selectedConnection == null) return;

            var newValue = valueControl switch
            {
                ComboBox cb => cb.SelectedItem is ComboBoxItem ci ? ci.Content?.ToString() ?? "" : "",
                TextBox tb => tb.Text ?? "",
                _ => "",
            };

            if (!string.IsNullOrEmpty(newValue))
                _selectedConnection.Config[field.Key] = newValue;
            else
                _selectedConnection.Config.Remove(field.Key);
        }

        switch (valueControl)
        {
            case ComboBox cb:
                cb.SelectionChanged += (_, _) => SyncValue();
                break;
            case TextBox tb:
                tb.LostFocus += (_, _) => SyncValue();
                break;
        }

        grid.Children.Add(valueControl);
        Grid.SetColumn(valueControl, 1);

        return grid;
    }

    // ── 子设备（从站）操作 ──

    private void OnAddSlave()
    {
        if (_selectedConnection == null)
        {
            StatusText.Text = AppLocalization.Service["comm.select.first"];
            return;
        }
        var slave = new DeviceSlave
        {
            Name = AppLocalization.Service.Format("comm.slave.new", _selectedConnection.Slaves.Count + 1),
            ConnectionId = _selectedConnection.Id,
            SlaveAddress = _selectedConnection.Slaves.Count + 1,
        };
        _selectedConnection.Slaves.Add(slave);
        _selectedSlave = slave;
        RefreshConnectionList();
        // 树刷新时已按 _selectedSlave 恢复选中（_isUpdatingTree 跳过事件）；
        // 此处手动填充表单与变量列表，避免依赖 SelectionChanged 的时序。
        SelectTreeBySlave(slave.Id);
        OnSlaveSelected(slave);
        StatusText.Text = AppLocalization.Service["comm.slave.added"];
    }

    private void OnDeleteSlave()
    {
        if (_selectedConnection == null || _selectedSlave == null)
        {
            StatusText.Text = AppLocalization.Service["comm.slave.select.first"];
            return;
        }
        var slaveId = _selectedSlave.Id;
        if (_selectedConnection.Slaves.Remove(_selectedSlave))
        {
            // 级联清理：该子设备下的变量清空 SlaveId（归到连接下）
            var orphaned = _variableManager.Variables
                .Where(v => v.SlaveId == slaveId)
                .ToList();
            foreach (var v in orphaned)
                v.SlaveId = "";

            _selectedSlave = null;
            RefreshConnectionList();
            RefreshSlaveVars();
            StatusText.Text = orphaned.Count > 0
                ? AppLocalization.Service.Format("comm.slave.deleted.cascade", orphaned.Count)
                : AppLocalization.Service["comm.slave.deleted"];
        }
    }

    private void OnSlaveSelected(DeviceSlave slave)
    {
        _selectedSlave = slave;
        _isUpdating = true;
        try
        {
            TxtSlaveName.Text = slave.Name;
            TxtSlaveAddress.Text = slave.SlaveAddress.ToString();
            ChkSlaveEnabled.IsChecked = slave.IsEnabled;
            TxtSlaveDesc.Text = slave.Description ?? "";
        }
        finally
        {
            _isUpdating = false;
        }
        RefreshSlaveVars();
    }

    private void OnSaveSlave()
    {
        if (_selectedSlave == null)
        {
            StatusText.Text = AppLocalization.Service["comm.slave.select.first"];
            return;
        }
        var name = TxtSlaveName.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            StatusText.Text = AppLocalization.Service["comm.slave.name.empty"];
            return;
        }
        _selectedSlave.Name = name;
        int.TryParse(TxtSlaveAddress.Text, out var addr);
        _selectedSlave.SlaveAddress = addr > 0 ? addr : 1;
        _selectedSlave.IsEnabled = ChkSlaveEnabled.IsChecked == true;
        _selectedSlave.Description = string.IsNullOrWhiteSpace(TxtSlaveDesc.Text) ? null : TxtSlaveDesc.Text.Trim();

        RefreshConnectionList();
        SelectTreeBySlave(_selectedSlave.Id);
        StatusText.Text = AppLocalization.Service.Format("comm.slave.saved", _selectedSlave.Name);
    }

    // ── 子设备变量操作 ──

    private void RefreshSlaveVars()
    {
        SlaveVarList.Items.Clear();
        SlaveVarsHint.Text = AppLocalization.Service["comm.slave.vars.hint"];

        if (_selectedConnection == null || _selectedSlave == null)
        {
            SlaveVarsHint.Text = AppLocalization.Service["comm.slave.select.first"];
            return;
        }

        var vars = _variableManager.Variables
            .Where(v => v.SlaveId == _selectedSlave.Id)
            .ToList();
        foreach (var v in vars)
            SlaveVarList.Items.Add(v);

        // 空列表引导：提示如何添加变量
        if (vars.Count == 0)
            SlaveVarsHint.Text = AppLocalization.Service["comm.slave.vars.empty.hint"];
    }

    private void OnAddSlaveVar()
    {
        if (_selectedConnection == null || _selectedSlave == null)
        {
            StatusText.Text = AppLocalization.Service["comm.slave.select.first"];
            return;
        }
        var name = TxtSlaveVarName.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            StatusText.Text = AppLocalization.Service["comm.slave.var.name.empty"];
            return;
        }
        if (_variableManager.Variables.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText.Text = AppLocalization.Service.Format("comm.slave.var.exists", name);
            return;
        }

        var dataType = CboSlaveVarType.SelectedItem is ComboBoxItem cbi
            ? cbi.Tag?.ToString() ?? "Int16"
            : "Int16";
        // Modbus 完整地址 = 寄存器类型前缀 + 偏移量（如保持寄存器 1 → "40001"）
        var address = BuildModbusAddress(CboSlaveVarRegType, TxtSlaveVarAddress.Text);

        var variable = new Variable
        {
            Name = name,
            DataType = dataType,
            Address = address,
            ConnectionId = _selectedConnection.Id,
            SlaveId = _selectedSlave.Id,
        };
        _variableManager.Variables.Add(variable);
        TxtSlaveVarName.Text = "";
        TxtSlaveVarAddress.Text = "";
        RefreshSlaveVars();
        StatusText.Text = AppLocalization.Service.Format("comm.slave.var.added", name);
    }

    /// <summary>
    /// 组合 Modbus 完整地址：取寄存器类型前缀 + 用户输入的偏移量。
    /// 用户可输入完整地址（如 40001）或纯偏移（如 1），统一规范化为「前缀 + 5 位」。
    /// </summary>
    private static string BuildModbusAddress(ComboBox regTypeCombo, string? input)
    {
        var prefix = regTypeCombo.SelectedItem is ComboBoxItem rci && rci.Tag is (string p, _)
            ? p
            : "4";
        var raw = input?.Trim() ?? "";

        // 已含前缀（4xxxx/3xxxx/1xxxx/0xxxx）→ 直接规范化
        if (raw.Length >= 5 && int.TryParse(raw, out var full))
            return full.ToString("D5");

        // 纯偏移 → 补前缀（保持 40001、输入 30001、离散 10001、线圈 00001）
        if (int.TryParse(raw, out var offset))
            return (int.Parse(prefix) * 10000 + offset).ToString("D5");

        return raw; // 非数字（如带位号 40001.0）原样保留
    }

    /// <summary>根据当前寄存器类型更新地址输入提示（占位符 + 说明文字）。</summary>
    private void UpdateSlaveVarAddressHint()
    {
        var kind = CboSlaveVarRegType.SelectedItem is ComboBoxItem rci && rci.Tag is (_, string k)
            ? k
            : "holding";
        TxtSlaveVarAddress.PlaceholderText = kind switch
        {
            "coil" => AppLocalization.Service["comm.slave.var.reg.coil.addr"],
            "discrete" => AppLocalization.Service["comm.slave.var.reg.discrete.addr"],
            "input" => AppLocalization.Service["comm.slave.var.reg.input.addr"],
            _ => AppLocalization.Service["comm.slave.var.reg.holding.addr"],
        };
        TxtSlaveVarAddressHint.Text = kind switch
        {
            "coil" => AppLocalization.Service["comm.slave.var.reg.coil.hint"],
            "discrete" => AppLocalization.Service["comm.slave.var.reg.discrete.hint"],
            "input" => AppLocalization.Service["comm.slave.var.reg.input.hint"],
            _ => AppLocalization.Service["comm.slave.var.reg.holding.hint"],
        };
    }

    /// <summary>按 Tag 值选中 ComboBox 对应项。</summary>
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

    private void OnDeleteSlaveVar()
    {
        if (SlaveVarList.SelectedItem is not Variable v)
        {
            StatusText.Text = AppLocalization.Service["comm.slave.var.select.first"];
            return;
        }
        _variableManager.Variables.Remove(v);
        RefreshSlaveVars();
        StatusText.Text = AppLocalization.Service["comm.slave.var.deleted"];
    }

    private void OnSlaveVarSelected()
    {
        if (SlaveVarList.SelectedItem is Variable v)
        {
            TxtSlaveVarName.Text = v.Name;
            TxtSlaveVarAddress.Text = v.Address;
            SelectComboBoxByTag(CboSlaveVarType, v.DataType);
            // 从地址首数字反推寄存器类型（4=保持 3=输入 1=离散 0=线圈）
            SelectRegTypeFromAddress(v.Address);
        }
    }

    /// <summary>根据变量地址首数字选中对应的寄存器类型。</summary>
    private void SelectRegTypeFromAddress(string address)
    {
        var first = address.TrimStart().FirstOrDefault();
        var prefix = first switch
        {
            '3' => "3",
            '1' => "1",
            '0' => "0",
            _ => "4", // 4 或未识别默认保持寄存器
        };
        for (int i = 0; i < CboSlaveVarRegType.Items.Count; i++)
        {
            if (CboSlaveVarRegType.Items[i] is ComboBoxItem item
                && item.Tag is (string p, _) && p == prefix)
            {
                CboSlaveVarRegType.SelectedIndex = i;
                break;
            }
        }
        UpdateSlaveVarAddressHint();
    }

    // ── 子设备变量：编辑保存 / 批量添加 / CSV 导入导出 ──

    /// <summary>保存对已选变量的修改（名称/类型/地址/寄存器类型）。</summary>
    private void OnSaveEditSlaveVar()
    {
        if (SlaveVarList.SelectedItem is not Variable v)
        {
            StatusText.Text = AppLocalization.Service["comm.slave.var.select.first"];
            return;
        }
        var name = TxtSlaveVarName.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            StatusText.Text = AppLocalization.Service["comm.slave.var.name.empty"];
            return;
        }
        // 重名检查（排除自身）
        if (_variableManager.Variables.Any(x => x.Id != v.Id && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText.Text = AppLocalization.Service.Format("comm.slave.var.exists", name);
            return;
        }

        var dataType = CboSlaveVarType.SelectedItem is ComboBoxItem cbi
            ? cbi.Tag?.ToString() ?? "Int16"
            : "Int16";

        v.Name = name;
        v.DataType = dataType;
        v.Address = BuildModbusAddress(CboSlaveVarRegType, TxtSlaveVarAddress.Text);
        v.ConnectionId = _selectedConnection?.Id ?? v.ConnectionId;
        v.SlaveId = _selectedSlave?.Id ?? v.SlaveId;

        RefreshSlaveVars();
        SelectListItemByTag(SlaveVarList, v.Id);
        StatusText.Text = AppLocalization.Service.Format("comm.slave.var.edit.saved", v.Name);
    }

    /// <summary>批量添加：每行一个变量，格式「名称,地址[,类型]」，逗号分隔（地址可为偏移或完整 Modbus 地址）。</summary>
    /// <summary>
    /// 批量添加：按「名称前缀 + 起始序号 + 起始地址 + 数量 + 类型/寄存器类型」自动生成变量。
    /// 变量名 = 前缀 + (起始序号 + i)，地址 = 起始地址 + i（自动补寄存器前缀）。
    /// </summary>
    private void OnApplyBatchVars()
    {
        if (_selectedConnection == null || _selectedSlave == null)
        {
            StatusText.Text = AppLocalization.Service["comm.slave.select.first"];
            return;
        }

        var prefix = TxtBatchNamePrefix.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(prefix))
        {
            StatusText.Text = AppLocalization.Service["comm.slave.var.batch.name.empty"];
            return;
        }

        if (!int.TryParse(TxtBatchCount.Text, out var count) || count < 1 || count > 500)
        {
            StatusText.Text = AppLocalization.Service["comm.slave.var.batch.count.invalid"];
            return;
        }

        int startIndex = 1;
        int.TryParse(TxtBatchStartIndex.Text, out startIndex);
        if (startIndex < 0) startIndex = 0;

        var startAddr = TxtBatchStartAddr.Text?.Trim() ?? "1";
        int.TryParse(startAddr, out var startAddrNum);

        var dataType = CboBatchVarType.SelectedItem is ComboBoxItem tbi
            ? tbi.Tag?.ToString() ?? "Int16"
            : "Int16";

        int added = 0, skipped = 0;
        for (int i = 0; i < count; i++)
        {
            var name = $"{prefix}{startIndex + i}";
            if (_variableManager.Variables.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            { skipped++; continue; }

            var address = startAddrNum > 0
                ? BuildModbusAddress(CboBatchVarRegType, (startAddrNum + i).ToString())
                : BuildModbusAddress(CboBatchVarRegType, startAddr);

            _variableManager.Variables.Add(new Variable
            {
                Name = name,
                DataType = dataType,
                Address = address,
                ConnectionId = _selectedConnection.Id,
                SlaveId = _selectedSlave.Id,
            });
            added++;
        }

        RefreshSlaveVars();
        StatusText.Text = added > 0
            ? AppLocalization.Service.Format("comm.slave.var.batch.added", added, skipped)
            : AppLocalization.Service.Format("comm.slave.var.batch.skipped", skipped);
    }

    /// <summary>实时预览批量生成的变量名与地址。</summary>
    private void UpdateBatchPreview()
    {
        if (TxtBatchPreview == null) return;
        try
        {
            var prefix = TxtBatchNamePrefix.Text?.Trim() ?? "";
            int startIndex = 1, count = 0;
            int.TryParse(TxtBatchStartIndex.Text, out startIndex);
            int.TryParse(TxtBatchCount.Text, out count);
            var startAddr = TxtBatchStartAddr.Text?.Trim() ?? "";
            int.TryParse(startAddr, out var startAddrNum);

            if (string.IsNullOrEmpty(prefix) || count < 1 || count > 500)
            {
                TxtBatchPreview.Text = "";
                return;
            }

            var sampleCount = Math.Min(count, 3);
            var samples = new List<string>();
            for (int i = 0; i < sampleCount; i++)
            {
                var name = $"{prefix}{startIndex + i}";
                var address = startAddrNum > 0
                    ? BuildModbusAddress(CboBatchVarRegType, (startAddrNum + i).ToString())
                    : startAddr;
                samples.Add($"{name} ({address})");
            }
            var ellipsis = count > sampleCount ? " …" : "";
            TxtBatchPreview.Text = AppLocalization.Service.Format(
                "comm.slave.var.batch.preview", count, string.Join("  ", samples) + ellipsis);
        }
        catch { TxtBatchPreview.Text = ""; }
    }

    /// <summary>导入 CSV：选择文件 → 解析 → 归属当前子设备批量添加。</summary>
    private async Task OnImportSlaveVarsAsync()
    {
        if (_selectedConnection == null || _selectedSlave == null)
        {
            StatusText.Text = AppLocalization.Service["comm.slave.select.first"];
            return;
        }
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // 目标文件必须在 try 外承接：`files` 声明在 try 内（文件选择器那一步），
        // catch 里取不到 —— 编译器会当场报 CS0103（登记 #61h 第二刀，阶段 90）。
        // 选择器自身失败时它仍是 null ⇒ FailureDetail 回落到「不带文件」的模板，
        // 那一刻用户**确实**还没选到任何文件，留空是正确表达（同 MainWindow.OpenProject 的理由）。
        string? targetPath = null;
        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = AppLocalization.Service["comm.slave.var.import.title"],
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } },
            });
            if (files.Count == 0) return;

            // 赋值与读取共用同一个变量 ⇒ 报给用户的路径**就是**读过的那个
            targetPath = files[0].Path.LocalPath;
            var csvText = System.IO.File.ReadAllText(targetPath);
            var result = CsvVariableImporter.Parse(csvText, _selectedConnection.Id, _selectedSlave.Id);

            int added = 0, skipped = 0;
            foreach (var v in result.Variables)
            {
                if (_variableManager.Variables.Any(x => string.Equals(x.Name, v.Name, StringComparison.OrdinalIgnoreCase)))
                { skipped++; continue; }
                _variableManager.Variables.Add(v);
                added++;
            }

            RefreshSlaveVars();
            StatusText.Text = added > 0
                ? AppLocalization.Service.Format("comm.slave.var.import.ok", added, skipped)
                : AppLocalization.Service.Format("comm.slave.var.import.skipped", skipped);

            // 坏行以前在这里是**静默丢掉**的 —— 本方法连 result.Errors 都不读（阶段 88 量的
            // 结果，登记 #61f）。现在至少让用户看到「有几行没进来、分别是哪几行」：
            // 条数上状态行、逐条原因挂提示，与 VariableImportView 同一套渲染出口。
            if (result.HasErrors)
                StatusText.Text += AppLocalization.Service.Format(Lang.VarImportWarnings, result.Errors.Count);
            ToolTip.SetTip(StatusText, result.HasErrors
                ? string.Join("\n", result.Errors.Select(ExternalText.Describe))
                : null);
        }
        catch (Exception ex)
        {
            StatusText.Text = UiOperationGuard.FailureDetail(Lang.UiOpImportSlaveVars, targetPath, ex);
        }
    }

    /// <summary>导出 CSV：当前子设备变量写入 CSV 文件。</summary>
    private async void OnExportSlaveVars()
    {
        if (_selectedSlave == null)
        {
            StatusText.Text = AppLocalization.Service["comm.slave.select.first"];
            return;
        }
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        string? targetPath = null;
        try
        {
            var vars = _variableManager.Variables
                .Where(v => v.SlaveId == _selectedSlave.Id)
                .ToList();
            if (vars.Count == 0)
            {
                StatusText.Text = AppLocalization.Service["comm.slave.var.export.empty"];
                return;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = AppLocalization.Service["comm.slave.var.export.title"],
                DefaultExtension = "csv",
                SuggestedFileName = $"{_selectedSlave.Name}_variables.csv",
                FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } },
            });
            if (file == null) return;
            targetPath = file.Path.LocalPath;

            var csv = CsvVariableImporter.Export(vars);
            await System.IO.File.WriteAllTextAsync(file.Path.LocalPath, csv);
            StatusText.Text = AppLocalization.Service.Format("comm.slave.var.export.ok", vars.Count);
        }
        catch (Exception ex)
        {
            StatusText.Text = UiOperationGuard.FailureDetail(Lang.UiOpExportSlaveVars, targetPath, ex);
        }
    }

    /// <summary>按 Tag 值选中 ListBox 项。</summary>
    private static void SelectListItemByTag(ListBox list, string? tag)
    {
        foreach (var item in list.Items)
        {
            if (item is Variable v && v.Id == tag)
            {
                list.SelectedItem = item;
                return;
            }
        }
        list.SelectedItem = null;
    }

    // ── 状态/测试 ──

    private void ShowEmptyState()
    {
        BtnTestConnection.IsVisible = false;
        RefreshSlaveVars();
    }

    private async void OnTestConnection()
    {
        if (_selectedConnection == null)
        {
            StatusText.Text = AppLocalization.Service["comm.select.first"];
            return;
        }

        StatusText.Text = AppLocalization.Service["comm.testing"];
        BtnTestConnection.IsEnabled = false;

        try
        {
            var driver = _driverRegistry.GetDriver(_selectedConnection.DriverId);
            if (driver == null)
            {
                StatusText.Text = AppLocalization.Service.Format("comm.unknown.driver", _selectedConnection.DriverId);
                return;
            }

            var config = new ConnectionConfig(_selectedConnection.DriverId, _selectedConnection.Config);
            var result = await Task.Run(() => driver.ValidateConnection(config));

            if (result.IsValid)
            {
                StatusText.Text = AppLocalization.Service["comm.valid"];
                StatusText.Foreground = new SolidColorBrush(UiPalette.Connected);
            }
            else
            {
                var issues = string.Join(", ", result.Issues.Select(LocalizeIssue));
                StatusText.Text = AppLocalization.Service.Format("comm.invalid", issues);
                StatusText.Foreground = new SolidColorBrush(UiPalette.Disconnected);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = AppLocalization.Service.Format("comm.test.failed", ex.Message);
            StatusText.Foreground = new SolidColorBrush(UiPalette.Disconnected);
        }
        finally
        {
            BtnTestConnection.IsEnabled = true;
        }
    }

    /// <summary>
    /// 校验文案按 <c>Code</c> 查字典，查不到就回落驱动自带的消息。
    ///
    /// <para>规则住在**插件程序集**（<c>Hmi.Communication.Builtin</c>）里，那里拿不到 App 的
    /// 字典，所以文案由 App 侧按 <c>comm.issue.&lt;code 小写&gt;</c> 提供，驱动原文只作回落 ——
    /// 与硬件族 <c>ProjectSettingsView.LocalizeIssue</c> 同一个形状（两个薄封装、
    /// 同一个出口）。</para>
    /// </summary>
    private static string LocalizeIssue(ValidationIssue issue)
        // 查模板 → 填参数 → 查不到回落驱动原文。这段逻辑收口在 ExternalText（**只有一份**）：
        // 两份必然漂移，而漂移的表现正是"守卫绿着、界面却是中文"。
        // ⚠ 前缀由 PluginTextKeys 推导，**不在这里手写** "comm.issue." 字面量。
        => ExternalText.Resolve(PluginTextKeys.CommIssueKey(issue.Code), issue.Message, issue.Args);
}

/// <summary>
/// 通讯树节点 —— 包装连接或子设备，用于 TreeView 层级展示。
/// <see cref="Connection"/> 非空时为连接节点（可含子设备子节点）；否则为子设备节点。
/// </summary>
public class CommTreeNode
{
    /// <summary>所属连接（连接节点非空；子设备节点为 null）。</summary>
    public DeviceConnection? Connection { get; init; }

    /// <summary>子设备（子设备节点非空；连接节点为 null）。</summary>
    public DeviceSlave? Slave { get; init; }

    /// <summary>子设备子节点集合（仅连接节点使用）。</summary>
    public List<CommTreeNode> Children { get; } = new();

    /// <summary>是否连接节点。</summary>
    public bool IsConnection => Connection != null;

    /// <summary>显示文本。</summary>
    public string Display
    {
        get
        {
            if (Connection != null)
                return $"{Connection.Name}  ({Connection.Slaves.Count})";
            if (Slave != null)
                return AppLocalization.Service.Format(Lang.CommSlaveStation, Slave.Name, Slave.SlaveAddress);
            return "";
        }
    }
}
