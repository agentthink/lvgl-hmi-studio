using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HmiEditor.Simulator;
using HmiEditor.App.Resources;
using HmiEditor.App.Controls;
using HmiEditor.App;
using HmiEditor.App.Services;

namespace HmiEditor.App.Views;

/// <summary>
/// 离线仿真控制对话框 —— 模拟变量值和通讯行为。
/// </summary>
public partial class SimulatorView : ChromeWindow
{
    private readonly VariableSimulator _variableSimulator;
    private readonly CommunicationSimulator _commSimulator;
    private SimulatedVariable? _currentVariable;
    private Dictionary<string, double> _currentValues = new();
    private bool _running;
    private bool _suppressEvents;
    private int _variableCounter;
    private readonly List<RegisterChange> _registerChanges = new();
    private readonly DispatcherTimer _runTimeTimer;
    private readonly DispatcherTimer _uiRefreshTimer;

    // 实时值表格的单元格缓存：id -> 值 TextBlock。行结构（id/类型/提示）固定，刷新时只更新值，
    // 避免每 200ms 全量 Clear+重建 ListBox 造成的性能浪费。
    private readonly Dictionary<string, TextBlock> _valueCells = new();

    /// <summary>信号类型中文标签</summary>
    private static readonly Dictionary<SignalType, string> SignalLabels = new()
    {
        [SignalType.Constant] = AppLocalization.Service["sim.const"],
        [SignalType.Sine] = AppLocalization.Service["sim.sine"],
        [SignalType.Square] = AppLocalization.Service["sim.square"],
        [SignalType.Ramp] = AppLocalization.Service["sim.ramp"],
        [SignalType.Increment] = AppLocalization.Service["sim.increment"],
        [SignalType.Random] = AppLocalization.Service["sim.random"],
        [SignalType.Manual] = AppLocalization.Service["sim.manual"],
    };

    /// <summary>信号类型波形提示</summary>
    private static readonly Dictionary<SignalType, string> WaveformDescriptions = new()
    {
        [SignalType.Constant] = AppLocalization.Service["sim.wave.const"],
        [SignalType.Sine] = AppLocalization.Service[Lang.SimWaveDescSine],
        [SignalType.Square] = AppLocalization.Service[Lang.SimWaveDescSquare],
        [SignalType.Ramp] = AppLocalization.Service[Lang.SimWaveDescSawtooth],
        [SignalType.Increment] = AppLocalization.Service[Lang.SimWaveDescIncrement],
        [SignalType.Random] = AppLocalization.Service[Lang.SimWaveDescRandom],
        [SignalType.Manual] = AppLocalization.Service[Lang.SimWaveDescManual],
    };

    /// <summary>
    /// 仅用于 XAML 设计器，不要直接使用。
    /// </summary>
    public SimulatorView()
    {
        _variableSimulator = null!;
        _commSimulator = null!;
        _runTimeTimer = null!;
        _uiRefreshTimer = null!;
        InitializeComponent();
    }

    public SimulatorView(VariableSimulator variableSimulator, CommunicationSimulator communicationSimulator)
        : this()
    {
        _variableSimulator = variableSimulator;
        _commSimulator = communicationSimulator;

        // 运行时计时器（更新时间显示）
        _runTimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _runTimeTimer.Tick += (_, _) => UpdateRunTime();

        // UI 刷新计时器（200ms 刷新实时值表格）
        _uiRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _uiRefreshTimer.Tick += (_, _) => RefreshRealTimeTable();

        // 订阅变量值更新事件
        _variableSimulator.ValuesUpdated += OnVariableValuesUpdated;

        // 订阅寄存器写入事件
        _commSimulator.RegisterWritten += OnRegisterWritten;

        // 设置日志回调
        _variableSimulator.Log = msg => AppendLog(msg);
        _commSimulator.Log = msg => AppendLog(msg);

        // 默认添加一个示例变量
        AddDefaultVariable();

        // 同步连接状态
        UpdateConnectionStatus();

        WireEvents();

        // 初始化运行时间显示
        RunTimeText.Text = AppLocalization.Service.Format("sim.running.elapsed", "00:00:00");
    }

    // ──────────────────────────────────────────────
    // 事件绑定
    // ──────────────────────────────────────────────

    private void WireEvents()
    {
        BtnAddVariable.Click += (_, _) => OnAddVariable();
        BtnDeleteVariable.Click += async (_, _) => await OnDeleteVariable();
        VariableList.SelectionChanged += OnVariableSelectionChanged;

        // 参数编辑事件
        SignalTypeCombo.SelectionChanged += OnSignalTypeChanged;
        AmplitudeInput.TextChanged += OnVariablePropertyChanged;
        OffsetInput.TextChanged += OnVariablePropertyChanged;
        FrequencyInput.TextChanged += OnVariablePropertyChanged;
        MinValueInput.TextChanged += OnVariablePropertyChanged;
        MaxValueInput.TextChanged += OnVariablePropertyChanged;
        NoiseInput.TextChanged += OnVariablePropertyChanged;
        ManualValueInput.TextChanged += OnManualValueChanged;

        // 仿真控制
        BtnStartStop.Click += (_, _) => OnToggleSimulation();
        TickIntervalCombo.SelectionChanged += OnTickIntervalChanged;
        ClockMultiplierCombo.SelectionChanged += OnClockMultiplierChanged;
        BtnReset.Click += (_, _) => OnReset();

        // 通讯控制
        BtnDisconnect.Click += (_, _) => OnDisconnect();
        BtnReconnect.Click += (_, _) => OnReconnect();
        WriteModeCombo.SelectionChanged += OnWriteModeChanged;
        BtnReadRegister.Click += (_, _) => OnReadRegister();
        BtnWriteRegister.Click += (_, _) => OnWriteRegister();
    }

    // ──────────────────────────────────────────────
    // 变量管理
    // ──────────────────────────────────────────────

    private void AddDefaultVariable()
    {
        var variable = new SimulatedVariable
        {
            VariableId = "Var_1",
            VariableName = AppLocalization.Service.Format(Lang.SimVarName, 1),
            Signal = SignalType.Sine,
            Amplitude = 100.0,
            Offset = 0.0,
            Frequency = 0.1,
            MinValue = 0.0,
            MaxValue = 100.0,
            Noise = 2.0,
        };
        _variableSimulator.RegisterVariable(variable);
        _variableCounter = 1;
        RefreshVariableList();
        SelectVariable(variable);
    }

    private void OnAddVariable()
    {
        if (_running) return;

        _variableCounter++;
        var variable = new SimulatedVariable
        {
            VariableId = $"Var_{_variableCounter}",
            VariableName = AppLocalization.Service.Format(Lang.SimVarName, _variableCounter),
            Signal = SignalType.Constant,
            Amplitude = 100.0,
            Offset = 0.0,
            Frequency = 0.1,
            MinValue = 0.0,
            MaxValue = 100.0,
            Noise = 0.0,
        };
        _variableSimulator.RegisterVariable(variable);
        RefreshVariableList();
        SelectVariable(variable);
        AppendLog(AppLocalization.Service.Format(Lang.SimVarAdded, variable.VariableId));
    }

    private async Task OnDeleteVariable()
    {
        if (_running || _currentVariable == null) return;

        // 删除为不可逆操作，先确认。
        var ok = await AppDialogs.ConfirmAsync(
            this, AppLocalization.Service.Format("sim.confirm.delete.var", _currentVariable.VariableId));
        if (!ok) return;

        var id = _currentVariable.VariableId;
        _variableSimulator.UnregisterVariable(id);
        _currentVariable = null;
        RefreshVariableList();
        UpdateVariableEditor();
        AppendLog(AppLocalization.Service.Format("sim.del.var.log", id));
    }

    private void RefreshVariableList()
    {
        VariableList.Items.Clear();
        var variables = _variableSimulator.GetVariables();

        foreach (var v in variables)
        {
            var displayName = string.IsNullOrEmpty(v.VariableName) ? v.VariableId : v.VariableName;
            var nameText = new TextBlock
            {
                Text = displayName,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var signalLabel = SignalLabels.GetValueOrDefault(v.Signal, AppLocalization.Service["sim.const"]);
            var valueText = new TextBlock
            {
                Text = _currentValues.GetValueOrDefault(v.VariableId, 0.0).ToString("F2", CultureInfo.InvariantCulture),
                Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)),
                FontSize = 11
            };

            var signalHint = new TextBlock
            {
                Text = signalLabel,
                Foreground = HmiEditor.App.Theme.TextHint,
                FontSize = 11
            };

            var infoPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Children = { nameText, signalHint }
            };

            var valuePanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Spacing = 8,
                Children = { valueText }
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Margin = new Thickness(4, 2)
            };
            grid.Children.Add(infoPanel);
            Grid.SetColumn(infoPanel, 0);
            grid.Children.Add(valuePanel);
            Grid.SetColumn(valuePanel, 1);

            var item = new ListBoxItem
            {
                Content = grid,
                Tag = v,
                Padding = new Thickness(0)
            };
            VariableList.Items.Add(item);

            if (_currentVariable != null && v.VariableId == _currentVariable.VariableId)
                VariableList.SelectedItem = item;
        }

        // 变量结构变化时同步重建实时值表格的行结构（值仍由刷新循环更新）。
        RebuildRealTimeTable();
    }

    private void SelectVariable(SimulatedVariable variable)
    {
        _currentVariable = variable;
        UpdateVariableEditor();

        // 选中列表中的对应项
        foreach (var item in VariableList.Items.OfType<ListBoxItem>())
        {
            if (item.Tag is SimulatedVariable v && v.VariableId == variable.VariableId)
            {
                VariableList.SelectedItem = item;
                break;
            }
        }
    }

    private void OnVariableSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ListBoxItem item && item.Tag is SimulatedVariable variable)
        {
            _currentVariable = variable;
            UpdateVariableEditor();
        }
    }

    // ──────────────────────────────────────────────
    // 变量编辑
    // ──────────────────────────────────────────────

    private void UpdateVariableEditor()
    {
        _suppressEvents = true;

        if (_currentVariable == null)
        {
            SignalTypeCombo.IsEnabled = false;
            AmplitudeInput.IsEnabled = false;
            OffsetInput.IsEnabled = false;
            FrequencyInput.IsEnabled = false;
            MinValueInput.IsEnabled = false;
            MaxValueInput.IsEnabled = false;
            NoiseInput.IsEnabled = false;
            ManualValueInput.IsEnabled = false;
            WaveformHint.Text = AppLocalization.Service["sim.select.var"];
            return;
        }

        SignalTypeCombo.IsEnabled = !_running;
        AmplitudeInput.IsEnabled = !_running;
        OffsetInput.IsEnabled = !_running;
        MinValueInput.IsEnabled = !_running;
        MaxValueInput.IsEnabled = !_running;
        NoiseInput.IsEnabled = !_running;

        // 信号类型
        var signalType = _currentVariable.Signal;
        var signalIndex = SignalTypeCombo.Items.OfType<ComboBoxItem>()
            .Select((item, idx) => new { item, idx })
            .FirstOrDefault(x => x.item.Tag?.ToString() == signalType.ToString())
            ?.idx ?? 0;
        SignalTypeCombo.SelectedIndex = signalIndex;

        // 参数值
        AmplitudeInput.Text = _currentVariable.Amplitude.ToString(CultureInfo.InvariantCulture);
        OffsetInput.Text = _currentVariable.Offset.ToString(CultureInfo.InvariantCulture);
        FrequencyInput.Text = _currentVariable.Frequency.ToString(CultureInfo.InvariantCulture);
        MinValueInput.Text = _currentVariable.MinValue.ToString(CultureInfo.InvariantCulture);
        MaxValueInput.Text = _currentVariable.MaxValue.ToString(CultureInfo.InvariantCulture);
        NoiseInput.Text = _currentVariable.Noise.ToString(CultureInfo.InvariantCulture);
        ManualValueInput.Text = _currentVariable.ManualValue.ToString(CultureInfo.InvariantCulture);

        // 频率启用条件：正弦波或方波
        FrequencyInput.IsEnabled = !_running && (signalType == SignalType.Sine || signalType == SignalType.Square);

        // 手动值启用条件：Manual 类型
        ManualValueInput.IsEnabled = signalType == SignalType.Manual;

        // 波形提示
        WaveformHint.Text = WaveformDescriptions.GetValueOrDefault(signalType, "");

        _suppressEvents = false;
    }

    private void OnSignalTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _currentVariable == null) return;

        if (SignalTypeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
        {
            if (Enum.TryParse<SignalType>(tagStr, out var signalType))
            {
                _currentVariable.Signal = signalType;

                // 频率启用
                FrequencyInput.IsEnabled = signalType == SignalType.Sine || signalType == SignalType.Square;

                // 手动值启用
                ManualValueInput.IsEnabled = signalType == SignalType.Manual;

                // 波形提示
                WaveformHint.Text = WaveformDescriptions.GetValueOrDefault(signalType, "");

                RefreshVariableList();
            }
        }
    }

    private void OnVariablePropertyChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents || _currentVariable == null) return;

        TryUpdateDouble(AmplitudeInput, val => _currentVariable.Amplitude = val);
        TryUpdateDouble(OffsetInput, val => _currentVariable.Offset = val);
        TryUpdateDouble(FrequencyInput, val => _currentVariable.Frequency = val);
        TryUpdateDouble(MinValueInput, val => _currentVariable.MinValue = val);
        TryUpdateDouble(MaxValueInput, val => _currentVariable.MaxValue = val);
        TryUpdateDouble(NoiseInput, val => _currentVariable.Noise = val);

        RefreshVariableList();
    }

    private void OnManualValueChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents || _currentVariable == null) return;
        if (_currentVariable.Signal != SignalType.Manual) return;

        TryUpdateDouble(ManualValueInput, val =>
        {
            _currentVariable.ManualValue = val;
            _variableSimulator.SetManualValue(_currentVariable.VariableId, val);
        });
    }

    private static void TryUpdateDouble(TextBox textBox, Action<double> setter)
    {
        if (double.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
        {
            setter(val);
        }
    }

    // ──────────────────────────────────────────────
    // 仿真控制
    // ──────────────────────────────────────────────

    private void OnToggleSimulation()
    {
        if (_running)
        {
            StopSimulation();
        }
        else
        {
            StartSimulation();
        }
    }

    private void StartSimulation()
    {
        if (_variableSimulator.GetVariables().Count == 0)
        {
            AppendLog(AppLocalization.Service[Lang.SimNoVars]);
            return;
        }

        _running = true;
        _variableSimulator.TickIntervalMs = GetSelectedTickInterval();
        _variableSimulator.Start();

        _runTimeTimer.Start();
        _uiRefreshTimer.Start();

        // 更新按钮状态（颜色由 .running 类样式驱动，避免 code-behind 双来源漂移）
        BtnStartStop.Content = AppLocalization.Service[Lang.SimStop];
        BtnStartStop.Classes.Set("running", true);

        // 禁用编辑
        BtnAddVariable.IsEnabled = false;
        BtnDeleteVariable.IsEnabled = false;
        TickIntervalCombo.IsEnabled = false;
        UpdateVariableEditor();

        AppendLog(AppLocalization.Service[Lang.SimStarted]);
    }

    private void StopSimulation()
    {
        _running = false;
        _variableSimulator.Stop();

        _runTimeTimer.Stop();
        _uiRefreshTimer.Stop();

        // 更新按钮状态（颜色由 .running 类样式驱动，避免 code-behind 双来源漂移）
        BtnStartStop.Content = AppLocalization.Service["sim.start"];
        BtnStartStop.Classes.Set("running", false);

        // 恢复编辑
        BtnAddVariable.IsEnabled = true;
        BtnDeleteVariable.IsEnabled = true;
        TickIntervalCombo.IsEnabled = true;
        UpdateVariableEditor();

        AppendLog(AppLocalization.Service[Lang.SimStopped]);
    }

    private int GetSelectedTickInterval()
    {
        if (TickIntervalCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
        {
            if (int.TryParse(tagStr, out var ms)) return ms;
        }
        return 100;
    }

    private void OnTickIntervalChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_running)
        {
            _variableSimulator.TickIntervalMs = GetSelectedTickInterval();
            AppendLog(AppLocalization.Service.Format(Lang.SimTickChanged, _variableSimulator.TickIntervalMs));
        }
    }

    /// <summary>仿真倍速变更 → 写入时钟倍速（加速/减速仿真时间轴）。</summary>
    private void OnClockMultiplierChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ClockMultiplierCombo.SelectedItem is not ComboBoxItem item) return;
        if (!double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier))
            return;
        _variableSimulator.ClockMultiplier = Math.Clamp(multiplier, 0.1, 10.0);
        AppendLog(AppLocalization.Service.Format(Lang.SimClockChanged, _variableSimulator.ClockMultiplier.ToString("0.#")));
    }

    private void OnReset()
    {
        var wasRunning = _running;
        if (wasRunning)
            StopSimulation();

        _variableSimulator.Clear();
        _commSimulator.Clear();
        _currentValues.Clear();
        _registerChanges.Clear();
        _variableCounter = 0;

        // 清空 UI
        VariableList.Items.Clear();
        RealTimeTable.Items.Clear();
        RegisterLogList.Items.Clear();
        LogTextBlock.Text = AppLocalization.Service["sim.log.hint"];
        RunTimeText.Text = AppLocalization.Service.Format("sim.running.elapsed", "00:00:00");
        _currentVariable = null;
        UpdateVariableEditor();

        // 添加默认变量
        AddDefaultVariable();

        // 恢复连接状态
        UpdateConnectionStatus();

        AppendLog(AppLocalization.Service[Lang.SimResetDone]);
    }

    private void UpdateRunTime()
    {
        if (_running)
        {
            var elapsed = _variableSimulator.ElapsedSeconds;
            var ts = TimeSpan.FromSeconds(elapsed);
            // 运行统计: 时长 · 倍速 · 变量数 · tick 数
            RunTimeText.Text = AppLocalization.Service.Format(
                Lang.SimRunningStats,
                ts.ToString("hh\\:mm\\:ss"),
                _variableSimulator.ClockMultiplier.ToString("0.#"),
                _variableSimulator.VariableCount,
                _variableSimulator.TickCount);
        }
    }

    // ──────────────────────────────────────────────
    // 实时值更新
    // ──────────────────────────────────────────────

    private void OnVariableValuesUpdated(object? sender, Dictionary<string, double> values)
    {
        _currentValues = values;
    }

    private void RefreshRealTimeTable()
    {
        if (!_running && _currentValues.Count == 0) return;

        // 行结构在变量列表变化时才需要重建；定时刷新仅更新已缓存的值单元格。
        if (RealTimeTable.Items.Count == 0)
        {
            RebuildRealTimeTable();
        }

        foreach (var (id, cell) in _valueCells)
        {
            var value = _currentValues.GetValueOrDefault(id, 0.0);
            cell.Text = value.ToString("F2", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>构建实时值表格行结构（id/类型/提示固定），并缓存值单元格以便高频刷新。</summary>
    private void RebuildRealTimeTable()
    {
        RealTimeTable.Items.Clear();
        _valueCells.Clear();

        var variables = _variableSimulator.GetVariables();
        foreach (var v in variables)
        {
            var signalLabel = SignalLabels.GetValueOrDefault(v.Signal, AppLocalization.Service["sim.const"]);
            var waveformHint = WaveformDescriptions.GetValueOrDefault(v.Signal, "");

            var displayId = string.IsNullOrEmpty(v.VariableName) ? v.VariableId : v.VariableName;
            var idText = new TextBlock
            {
                Text = displayId,
                Foreground = HmiEditor.App.Theme.TextPrimary,
                FontSize = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var valueText = new TextBlock
            {
                Text = "0.00",
                Foreground = SignalValueBrush(v.Signal),
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            _valueCells[v.VariableId] = valueText;

            var signalText = new TextBlock
            {
                Text = signalLabel,
                Foreground = HmiEditor.App.Theme.TextHint,
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var hintText = new TextBlock
            {
                Text = waveformHint,
                Foreground = HmiEditor.App.Theme.TextMuted,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,120,100,80"),
                Margin = new Thickness(0)
            };
            grid.Children.Add(idText);
            Grid.SetColumn(idText, 0);
            grid.Children.Add(valueText);
            Grid.SetColumn(valueText, 1);
            grid.Children.Add(signalText);
            Grid.SetColumn(signalText, 2);
            grid.Children.Add(hintText);
            Grid.SetColumn(hintText, 3);

            RealTimeTable.Items.Add(new ListBoxItem
            {
                Content = grid,
                Padding = new Thickness(0)
            });
        }
    }

    private static IBrush SignalValueBrush(SignalType signal) => signal switch
    {
        SignalType.Manual => new SolidColorBrush(Color.FromRgb(251, 191, 36)),
        SignalType.Constant => new SolidColorBrush(Color.FromRgb(148, 163, 184)),
        _ => new SolidColorBrush(Color.FromRgb(52, 211, 153))
    };

    // ──────────────────────────────────────────────
    // 通讯模拟
    // ──────────────────────────────────────────────

    private void UpdateConnectionStatus()
    {
        var connected = _commSimulator.IsConnected;
        ConnectionStatus.Background = connected
            ? new SolidColorBrush(UiPalette.Connected)
            : new SolidColorBrush(UiPalette.Disconnected);
        ConnectionStatusText.Text = connected ? AppLocalization.Service["sim.connected"] : AppLocalization.Service[Lang.SimDisconnected];
        ConnectionStatusText.Foreground = connected
            ? new SolidColorBrush(UiPalette.Connected)
            : new SolidColorBrush(UiPalette.Disconnected);

        BtnDisconnect.IsEnabled = connected;
        BtnReconnect.IsEnabled = !connected;
    }

    private void OnDisconnect()
    {
        _commSimulator.Disconnect();
        UpdateConnectionStatus();
        AppendLog(AppLocalization.Service[Lang.SimCommDisconnected]);
    }

    private void OnReconnect()
    {
        _commSimulator.Reconnect();
        UpdateConnectionStatus();
        AppendLog(AppLocalization.Service[Lang.SimCommReconnected]);
    }

    private void OnWriteModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (WriteModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
        {
            if (Enum.TryParse<WriteMode>(tagStr, out var mode))
            {
                _commSimulator.Mode = mode;
                AppendLog(AppLocalization.Service.Format(Lang.SimRespModeChanged, mode));
            }
        }
    }

    private void OnReadRegister()
    {
        if (!int.TryParse(RegAddressInput.Text, out var address))
        {
            AppendLog(AppLocalization.Service[Lang.SimInvalidAddr]);
            return;
        }

        try
        {
            var value = _commSimulator.ReadRegister(address);
            RegValueInput.Text = value.ToString("F2", CultureInfo.InvariantCulture);
            AppendLog(AppLocalization.Service.Format(Lang.SimRegRead, address, value));
        }
        catch (Exception ex)
        {
            AppendLog(AppLocalization.Service.Format(Lang.SimRegReadFailed, ExternalText.FailureReason(ex)));
        }
    }

    private void OnWriteRegister()
    {
        if (!int.TryParse(RegAddressInput.Text, out var address))
        {
            AppendLog(AppLocalization.Service[Lang.SimInvalidAddr]);
            return;
        }

        if (!double.TryParse(RegValueInput.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            AppendLog(AppLocalization.Service[Lang.SimInvalidValue]);
            return;
        }

        try
        {
            _commSimulator.WriteRegister(address, value);
            AppendLog(AppLocalization.Service.Format(Lang.SimRegWrite, address, value));
        }
        catch (Exception ex)
        {
            AppendLog(AppLocalization.Service.Format(Lang.SimRegWriteFailed, ExternalText.FailureReason(ex)));
        }
    }

    private void OnRegisterWritten(object? sender, RegisterChange change)
    {
        _registerChanges.Add(change);
        if (_registerChanges.Count > 100)
            _registerChanges.RemoveRange(0, _registerChanges.Count - 100);

        Dispatcher.UIThread.Post(() => RefreshRegisterLog());
    }

    private void RefreshRegisterLog()
    {
        RegisterLogList.Items.Clear();

        foreach (var change in _registerChanges.AsEnumerable().Reverse())
        {
            var timeStr = change.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var text = AppLocalization.Service.Format(Lang.SimRegLog, timeStr, change.Address, change.OldValue, change.NewValue);

            RegisterLogList.Items.Add(new ListBoxItem
            {
                Content = text,
                Padding = new Thickness(4, 1)
            });
        }
    }

    // ──────────────────────────────────────────────
    // 日志
    // ──────────────────────────────────────────────

    private void AppendLog(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var currentText = LogTextBlock.Text ?? "";
            var newLine = $"[{timestamp}] {message}";

            // 保留最近 500 行
            var lines = currentText.Split('\n');
            if (lines.Length > 500)
                currentText = string.Join("\n", lines[^500..]);

            LogTextBlock.Text = currentText + (currentText == AppLocalization.Service["sim.log.hint"] ? "" : "\n") + newLine;
            LogScrollViewer.ScrollToEnd();
        });
    }

    // ──────────────────────────────────────────────
    // 窗口生命周期
    // ──────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        // 无参构造是 XAML 设计器路径：四个字段（两个模拟器 + 两个计时器）全是 null，
        // 也没有订阅过任何东西 —— 收尾必须容得下它，否则关闭设计器预览就是一次 NRE。
        // 见 WindowViewSmokeUiTests。
        if (_variableSimulator is not null && _commSimulator is not null)
        {
            if (_running)
            {
                _variableSimulator.Stop();
            }

            _runTimeTimer.Stop();
            _uiRefreshTimer.Stop();

            // 生命周期对称：退订长生命周期模拟器的事件，避免关闭后回调操作已销毁的控件。
            _variableSimulator.ValuesUpdated -= OnVariableValuesUpdated;
            _commSimulator.RegisterWritten -= OnRegisterWritten;

            _variableSimulator.Log = null;
            _commSimulator.Log = null;
        }

        base.OnClosed(e);
    }
}
