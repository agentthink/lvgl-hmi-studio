using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hmi.Communication.Contracts;
using HmiEditor.App.Resources;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using HmiEditor.Simulator;

namespace HmiEditor.App.Services;

/// <summary>
/// 设计时数据预览运行时（R2-T2）—— 预览模式下的**值源生命周期**与状态。
///
/// <para><b>职责：</b>收集当前屏幕的绑定变量并按数据类型注册仿真信号、
/// 构建运行时标签表、选择值源（真实设备 Modbus TCP/RTU/OPC UA &gt; 虚拟从站 &gt; 模拟信号）、
/// 装配动作执行器，并把只读值/类型/单位提供器注入画布；停止时反向释放。</para>
///
/// <para><b>为什么要抽成服务：</b>此前这套状态（<c>_simulator</c>/<c>_bindingEngine</c>/
/// <c>_previewValues</c>/<c>_modbusDriver</c>/<c>_actionExecutor</c>）散在 MainWindow 上，
/// 被脚本调试、标签监视器、画布事件、关停清理四处直接读写，谁也说不清"谁负责释放"。
/// 现在所有权的边界就是本类的 <see cref="Dispose"/>：仿真器的 Timer 与真实设备连接
/// 都由这里释放（关停路径只调 <c>MainWindow.CleanupOnShutdown</c> → <c>Dispose()</c>）。</para>
///
/// <para><b>可测试性：</b>全部宿主依赖都是回调（与 <see cref="BuildPipelineService"/> 同一套路），
/// 因此可以脱离 UI 直接测"注册了哪些信号、值源换成了谁、停止/释放时驱动有没有被 Dispose"。
/// <see cref="DriverFactory"/> 是留给测试的注入点（默认按驱动类型创建真实驱动）。</para>
///
/// <para>变量数据类型/单位的解析规则在 <see cref="VariableBindingInfo"/>（Core），
/// 不在宿主回调里 —— 宿主只需提供工程对象。</para>
/// </summary>
public sealed class PreviewRuntimeService : IDisposable
{
    /// <summary>最新一帧模拟值快照（后台线程更新）。脚本调试与标签监视器直接读它。</summary>
    private volatile Dictionary<string, double> _previewValues = new();

    private IRuntimeCommunicationDriver? _deviceDriver;
    private IRuntimeValueSource? _valueSource;
    private bool _disposed;

    // ───────────────────────── 宿主回调 ─────────────────────────

    /// <summary>当前屏幕（预览注册绑定的来源）。</summary>
    public required Func<HmiScreen?> GetCurrentScreen { get; init; }

    /// <summary>当前工程（标签表与设备连接配置的来源）。</summary>
    public required Func<HmiProject> GetCurrentProject { get; init; }

    /// <summary>写状态栏。</summary>
    public required Action<string> SetStatus { get; init; }

    /// <summary>预览动作：切换屏幕（返回是否成功）。</summary>
    public required Func<string, bool> SwitchScreen { get; init; }

    /// <summary>预览动作：执行脚本（返回是否成功）。</summary>
    public required Func<string, bool> ExecuteScript { get; init; }

    /// <summary>向画布注入只读值提供器（null = 移除）。</summary>
    public required Action<Func<string, double?>?> SetValueProvider { get; init; }

    /// <summary>向画布注入"值显示类型"提供器（null = 移除）。</summary>
    public required Action<Func<string, string?>?> SetValueTypeProvider { get; init; }

    /// <summary>向画布注入"值显示单位"提供器（null = 移除）。</summary>
    public required Action<Func<string, string?>?> SetValueUnitProvider { get; init; }

    /// <summary>请求画布重绘（仿真值更新时从后台线程回调）。</summary>
    public required Action InvalidateCanvas { get; init; }

    /// <summary>
    /// 创建运行时通讯驱动的工厂。默认按 <c>DriverId</c> 创建真实驱动
    /// （modbus-tcp / modbus-rtu / opc-ua）；测试可替换为假驱动。
    /// </summary>
    public Func<DeviceConnection, IRuntimeCommunicationDriver> DriverFactory { get; set; } = CreateDriver;

    /// <summary>
    /// **诊断**日志出口（默认写 Debug 输出；<c>MainWindow</c> **不注入**它 ⇒ 应用里它只进 Debug）。
    ///
    /// <para>名字里带 <c>Diagnostic</c> 是有意的：这里的文案**不该本地化**（诊断信息翻译后无法搜索），
    /// 而"它是不是诊断"必须能从**调用点那一行**看出来 —— 硬编码中文守卫按行判定排除类别，
    /// 而 sink 的定义在别处（本属性）时它看不见（实测：这条消息被判成"待本地化"）。
    /// 与 <c>Debug.WriteLine</c>/<c>Console.</c>/<c>Trace.</c> 同类，见
    /// <c>HardcodedLocalizationGuardTests</c> 的排除表。</para>
    /// </summary>
    public Action<string>? DiagnosticLog { get; set; } = msg => Debug.WriteLine(msg);

    // ───────────────────────── 对外只读状态 ─────────────────────────

    /// <summary>离线仿真器（SimulatorView 与预览共用同一实例）。</summary>
    public VariableSimulator Simulator { get; } = new();

    /// <summary>虚拟 Modbus 从站（SimulatorView 与预览共用同一实例）。</summary>
    public CommunicationSimulator CommunicationSimulator { get; } = new();

    /// <summary>运行时绑定引擎；未构建时为 null（<see cref="EnsureBindingEngine"/> 会创建）。</summary>
    public RuntimeBindingEngine? BindingEngine { get; private set; }

    /// <summary>动作执行器；仅在预览模式运行期间非 null。</summary>
    public ActionExecutor? ActionExecutor { get; private set; }

    /// <summary>最近一帧模拟值快照。</summary>
    public IReadOnlyDictionary<string, double> PreviewValues => _previewValues;

    /// <summary>当前是否处于预览运行状态。</summary>
    public bool IsRunning => Simulator.IsRunning;

    /// <summary>当前值源是否为真实设备（而非虚拟从站/模拟信号）—— 供测试与诊断判断。</summary>
    public bool IsUsingRealDevice { get; private set; }

    // ───────────────────────── 绑定引擎 ─────────────────────────

    /// <summary>
    /// 取得（必要时创建并构建）运行时标签表。标签监视器在非预览状态下也用它。
    /// </summary>
    public RuntimeBindingEngine EnsureBindingEngine()
    {
        if (BindingEngine is null)
        {
            // 预览（非预览动作）下也保留 ValueSource = 通讯模拟器，保证写操作有落点。
            BindingEngine = new RuntimeBindingEngine(GetCurrentProject())
            {
                Log = DiagnosticLog
            };
            SetValueSource(NewCommSource());
        }

        BindingEngine.BuildRuntimeTags();
        return BindingEngine;
    }

    // ───────────────────────── 生命周期 ─────────────────────────

    /// <summary>
    /// 启动预览：注册当前屏幕的绑定变量 → 构建标签表 → 选择值源 → 装配动作执行器
    /// → 注入画布提供器 → 启动仿真器。
    /// 预览只读取模型，不产生任何写操作（R2-FF 适应度函数）。
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var screen = GetCurrentScreen();
        if (screen is null) return;

        Simulator.Stop();
        Simulator.Clear();

        var registered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ctrl in screen.Controls)
        {
            // 联动模型：遍历 VariableBindings 集合，回退旧字段
            foreach (var binding in ctrl.VariableBindings)
            {
                if (binding.Direction == HmiEditor.Core.Models.BindingDirection.WriteOnly) continue;

                // I1: 表达式绑定 —— 注册表达式引用的全部变量参与仿真
                if (binding.Kind == BindingSourceKind.Expression)
                {
                    foreach (var varName in ExpressionBindingEvaluator.ExtractVariables(binding))
                    {
                        if (registered.Add(varName))
                            RegisterSimVariable(varName);
                    }
                    continue;
                }

                if (!registered.Add(binding.VariableId)) continue;
                RegisterSimVariable(binding.VariableId);
            }

            // 回退旧字段
            if (ctrl.VariableBindings.Count == 0)
            {
                var bindingId = ctrl.VariableBindingId;
                if (!string.IsNullOrEmpty(bindingId) && registered.Add(bindingId))
                    RegisterSimVariable(bindingId);
            }
        }

        // 幂等订阅：重复 Start（未 Stop）不应导致回调叠加。
        Simulator.ValuesUpdated -= OnValuesUpdated;
        Simulator.ValuesUpdated += OnValuesUpdated;
        // 立即填充缓存，避免首帧空白
        _previewValues = Simulator.GetCurrentValues();

        // 运行时绑定引擎：构建标签表，验证绑定可解析性（悬空绑定计数）
        BindingEngine = new RuntimeBindingEngine(GetCurrentProject()) { Log = DiagnosticLog };
        BindingEngine.BuildRuntimeTags();

        // 值源选择: 真实设备(Modbus TCP/RTU / OPC UA) > 虚拟从站 > 模拟信号
        var deviceConnection = GetCurrentProject().VariableManager.Connections
            .FirstOrDefault(c => c.IsEnabled
                && (c.DriverId == "modbus-tcp" || c.DriverId == "modbus-rtu" || c.DriverId == "opc-ua"));
        if (deviceConnection is not null)
            TryConnectDevice(deviceConnection);
        else
            FallbackToVirtualCommSource();

        // 动作执行器：预览点击控件执行动作，写操作写入虚拟寄存器
        ActionExecutor = new ActionExecutor(BindingEngine)
        {
            SwitchScreenHandler = target => SwitchScreen(target),
            ExecuteScriptHandler = target => ExecuteScript(target),
            SystemActionHandler = target =>
            {
                SetStatus(AppLocalization.Service.Format("status.preview.system.action", target));
                return true;
            },
            Log = msg => DiagnosticLog?.Invoke(msg)
        };

        var engineLog = BindingEngine.Tags.Count > 0
            ? AppLocalization.Service.Format("status.preview.tags", BindingEngine.TagCount)
            : "";

        SetValueProvider(id =>
        {
            // 通讯寄存器值优先（含 Scale/Offset 工程值换算）
            var engValue = BindingEngine?.ReadValue(id);
            if (engValue.HasValue) return engValue.Value;
            // 回退模拟信号值
            return _previewValues.TryGetValue(id, out var v) ? v : null;
        });

        // 预览值格式化：变量类型提供器（Auto 值显示类型按变量 DataType 判断格式）
        SetValueTypeProvider(id => VariableBindingInfo.ResolveDataType(GetCurrentProject().VariableManager, id));
        // 预览文本后缀：控件未配置 ValueSuffix 时回退变量单位，使设计器与生成代码显示一致
        SetValueUnitProvider(id => VariableBindingInfo.ResolveUnit(GetCurrentProject().VariableManager, id));
        Simulator.Start();

        SetStatus(AppLocalization.Service.Format("status.preview.mode.bound", registered.Count, engineLog));
    }

    /// <summary>
    /// 停止预览：停仿真器、摘掉画布提供器、清空值缓存、**断开并释放真实设备连接**。
    /// 可重复调用；未启动时是无害的（仍会释放可能残留的设备连接）。
    /// </summary>
    public void Stop()
    {
        Simulator.Stop();
        Simulator.ValuesUpdated -= OnValuesUpdated;
        SetValueProvider(null);
        SetValueTypeProvider(null);
        SetValueUnitProvider(null);
        _previewValues.Clear();
        ActionExecutor = null;

        // 断开真实 Modbus/OPC UA 设备（只有实现 IDisposable 的驱动需要释放）
        (_deviceDriver as IDisposable)?.Dispose();
        _deviceDriver = null;
        // 值源也可能带后台资源（ModbusRuntimeSource 的后台刷新泵）—— 必须一起停
        (_valueSource as IDisposable)?.Dispose();
        _valueSource = null;
        IsUsingRealDevice = false;
    }

    /// <summary>预览模式下执行控件动作（写操作写入虚拟寄存器/真实设备）。</summary>
    public bool ExecuteAction(HmiControl control, string eventName = "OnClick")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ActionExecutor?.ExecuteControlAction(control, eventName) ?? false;
    }

    /// <summary>
    /// 释放后台资源：仿真器 Timer 与真实设备连接。关停路径必须调用（否则进程可能因
    /// Timer/套接字残留而无法正常退出）。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            Stop();
        }
        finally
        {
            Simulator.Dispose();
        }
    }

    // ───────────────────────── 内部实现 ─────────────────────────

    /// <summary>
    /// 按变量 DataType 注册仿真信号。
    /// 与抽取前逐字一致：<c>Bool</c>/<c>String</c> → 方波，其余（含各整数类型）→ 正弦，
    /// 并按类型取不同量程（Bool 0..1、整数 0..100、Float/Double 5..95）。
    /// 注：旧注释曾写"Int → 方波 0-100"，与实现不符（整数走的是正弦分支），此处按实际行为更正。
    /// </summary>
    private void RegisterSimVariable(string bindingId)
    {
        var dataType = VariableBindingInfo.ResolveDataType(GetCurrentProject().VariableManager, bindingId);
        var signal = dataType is "Bool" or "String"
            ? SignalType.Square
            : SignalType.Sine;

        double amplitude = 50, offset = 50, minVal = 0, maxVal = 100;
        switch (dataType)
        {
            case "Bool":
                amplitude = 1; offset = 0; minVal = 0; maxVal = 1;
                break;
            case "Int16" or "Int32" or "UInt16" or "UInt32":
                amplitude = 50; offset = 50; minVal = 0; maxVal = 100;
                break;
            case "Float" or "Double":
                amplitude = 45; offset = 50; minVal = 5; maxVal = 95;
                break;
        }

        Simulator.RegisterVariable(new SimulatedVariable
        {
            VariableId = bindingId,
            Signal = signal,
            Amplitude = amplitude,
            Offset = offset,
            MinValue = minVal,
            MaxValue = maxVal,
            Frequency = 0.2
        });
    }

    /// <summary>
    /// 尝试连接真实设备作为值源（Modbus TCP/RTU/OPC UA）。
    /// 连接成功 → 引擎用真实设备值；失败 → 回退虚拟从站。
    /// </summary>
    private void TryConnectDevice(DeviceConnection connection)
    {
        IRuntimeCommunicationDriver? driver = null;
        try
        {
            var config = new ConnectionConfig(connection.DriverId, connection.Config);
            driver = DriverFactory(connection);

            if (driver.Connect(config, TimeSpan.FromSeconds(2)))
            {
                // 换源前先释放上一台设备，避免连接泄漏
                (_deviceDriver as IDisposable)?.Dispose();
                _deviceDriver = driver;
                IsUsingRealDevice = true;
                SetValueSource(new ModbusRuntimeSource(
                    driver,
                    v => BindingEngine!.FindTag(v)?.Address));
                SetStatus(AppLocalization.Service.Format(
                    "status.preview.mode.connected", connection.Name, connection.DriverId));
                return;
            }

            (driver as IDisposable)?.Dispose();
            FallbackToVirtualCommSource();
        }
        catch (Exception ex)
        {
            // 连接失败/驱动构造失败都不应打断预览：记录并回退虚拟从站。
            DiagnosticLog?.Invoke($"[Preview] 连接 {connection.Name}（{connection.DriverId}）失败: {ex.Message}");
            (driver as IDisposable)?.Dispose();
            FallbackToVirtualCommSource();
        }
    }

    /// <summary>回退到虚拟从站值源（无映射变量仍回退模拟信号）。</summary>
    private void FallbackToVirtualCommSource()
    {
        IsUsingRealDevice = false;
        if (BindingEngine is null) return;
        SetValueSource(NewCommSource());
    }

    /// <summary>
    /// 换值源。**唯一入口** —— 旧的若是 <see cref="IDisposable"/>（<see cref="ModbusRuntimeSource"/>
    /// 带后台泵）必须在这里释放，否则每换一次源就泄漏一个常驻 Task。
    /// </summary>
    private void SetValueSource(IRuntimeValueSource source)
    {
        if (ReferenceEquals(_valueSource, source)) return;
        (_valueSource as IDisposable)?.Dispose();
        _valueSource = source;
        if (BindingEngine != null) BindingEngine.ValueSource = source;
    }

    private CommunicationRuntimeSource NewCommSource() =>
        new(CommunicationSimulator,
            variableAddressResolver: v => BindingEngine?.FindTag(v)?.Address);

    /// <summary>默认驱动工厂：按 DriverId 创建真实运行时驱动。</summary>
    private static IRuntimeCommunicationDriver CreateDriver(DeviceConnection connection) =>
        connection.DriverId switch
        {
            "modbus-rtu" => CreateRtuDriver(connection),
            "opc-ua" => new Hmi.Communication.Builtin.OpcUaRuntimeDriver
            {
                TimeoutMs = 1500,
                ConnectTimeoutMs = 2000
            },
            _ => new Hmi.Communication.Builtin.ModbusTcpRuntimeDriver
            {
                TimeoutMs = 1500,
                ConnectTimeoutMs = 2000
            }
        };

    /// <summary>创建 Modbus RTU 运行时驱动（串口传输）。</summary>
    private static IRuntimeCommunicationDriver CreateRtuDriver(DeviceConnection connection)
    {
        var p = connection.Config;
        var portName = p.TryGetValue("port", out var port) && !string.IsNullOrWhiteSpace(port)
            ? port
            : "COM1";
        var baud = p.TryGetValue("baud", out var baudStr) && int.TryParse(baudStr, out var b)
            ? b
            : 9600;

        var transport = new Hmi.Communication.Builtin.SerialRtuTransport(portName, baud);
        return new Hmi.Communication.Builtin.ModbusRtuRuntimeDriver(transport);
    }

    /// <summary>仿真器定时回调（后台线程）—— 更新缓存并切回 UI 线程重绘画布。</summary>
    private void OnValuesUpdated(object? sender, Dictionary<string, double> values)
    {
        _previewValues = values;
        InvalidateCanvas();
    }
}
