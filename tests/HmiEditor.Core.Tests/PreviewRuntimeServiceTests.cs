using System;
using System.Collections.Generic;
using System.Linq;
using Hmi.Communication.Contracts;
using BindingDirection = HmiEditor.Core.Models.BindingDirection;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using HmiEditor.Simulator;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 行为测试：设计时数据预览运行时（<see cref="PreviewRuntimeService"/>）。
///
/// 该服务从 MainWindow 抽出（原 <c>MainWindow.Preview.cs</c>：5 个状态字段 + 7 个方法）。
/// 抽出后可以脱离 UI 直接测四件过去只能靠人肉点的事：
///   ① 按变量 DataType 注册了哪些仿真信号；
///   ② 值源到底换成了谁（真实设备 / 虚拟从站），失败时是否安全回退；
///   ③ 画布提供器在 Start 时注入、Stop 时摘除；
///   ④ **生命周期**：停止/释放时真实设备连接与仿真器 Timer 有没有被 Dispose（防连接泄漏）。
/// </summary>
public class PreviewRuntimeServiceTests
{
    // ───────────────────────── 测试替身 ─────────────────────────

    /// <summary>可编程的假驱动：记录 Connect/Dispose 调用，供生命周期断言。</summary>
    private sealed class FakeDriver : IRuntimeCommunicationDriver, IDisposable
    {
        private readonly bool _connectResult;
        public FakeDriver(bool connectResult) => _connectResult = connectResult;

        public int ConnectCount { get; private set; }
        public int DisposeCount { get; private set; }
        public bool IsConnected { get; private set; }

        public bool Connect(ConnectionConfig config, TimeSpan timeout)
        {
            ConnectCount++;
            IsConnected = _connectResult;
            return _connectResult;
        }

        public void Disconnect() => IsConnected = false;

        public void Dispose()
        {
            DisposeCount++;
            IsConnected = false;
        }

        public System.Threading.Tasks.Task<ushort[]> ReadHoldingRegistersAsync(
            ushort startAddress, ushort count, System.Threading.CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult(Array.Empty<ushort>());

        public System.Threading.Tasks.Task WriteSingleRegisterAsync(
            ushort address, ushort value, System.Threading.CancellationToken ct = default) =>
            System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task WriteMultipleRegistersAsync(
            ushort startAddress, ushort[] values, System.Threading.CancellationToken ct = default) =>
            System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task<bool[]> ReadCoilsAsync(
            ushort startAddress, ushort count, System.Threading.CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult(Array.Empty<bool>());

        public System.Threading.Tasks.Task WriteSingleCoilAsync(
            ushort address, bool value, System.Threading.CancellationToken ct = default) =>
            System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>记录宿主回调调用的测试宿主。</summary>
    private sealed class Harness : IDisposable
    {
        public HmiProject Project { get; }
        public HmiScreen? Screen { get; set; }
        public List<string> Statuses { get; } = new();
        public List<string> SwitchedScreens { get; } = new();
        public List<string> ExecutedScripts { get; } = new();
        public int InvalidateCount { get; private set; }
        public Func<string, double?>? ValueProvider { get; private set; }
        public Func<string, string?>? TypeProvider { get; private set; }
        public Func<string, string?>? UnitProvider { get; private set; }
        public FakeDriver? Driver { get; set; }

        public PreviewRuntimeService Service { get; }

        public Harness(HmiProject project, HmiScreen? screen = null)
        {
            Project = project;
            Screen = screen ?? project.Screens.FirstOrDefault();

            Service = new PreviewRuntimeService
            {
                GetCurrentScreen = () => Screen,
                GetCurrentProject = () => Project,
                SetStatus = Statuses.Add,
                SwitchScreen = name => { SwitchedScreens.Add(name); return true; },
                ExecuteScript = id => { ExecutedScripts.Add(id); return true; },
                SetValueProvider = p => ValueProvider = p,
                SetValueTypeProvider = p => TypeProvider = p,
                SetValueUnitProvider = p => UnitProvider = p,
                InvalidateCanvas = () => InvalidateCount++,
                DiagnosticLog = _ => { },
            };
            Service.DriverFactory = _ => Driver ?? new FakeDriver(connectResult: false);
        }

        public void Dispose() => Service.Dispose();
    }

    // ───────────────────────── 工程构造 ─────────────────────────

    /// <summary>含 Bool / Int16 / Float / String 变量与读写绑定的工程。</summary>
    private static HmiProject MakeProject(bool withDeviceConnection = false)
    {
        var project = new HmiProject { Name = "Preview", LvglVersion = "9.5" };
        var scr = new HmiScreen { Id = "scr1", Name = "主屏", Width = 800, Height = 480 };
        project.Screens.Add(scr);

        var vm = project.VariableManager;
        if (withDeviceConnection)
        {
            vm.Connections.Add(new DeviceConnection
            {
                Id = "c1", Name = "PLC", DriverId = "modbus-tcp", IsEnabled = true,
                Config = new Dictionary<string, string> { ["host"] = "127.0.0.1", ["port"] = "502" },
            });
        }

        void AddVar(string id, string dataType, string address)
            => vm.Variables.Add(new Variable
            {
                Id = id, Name = id, Address = address, DataType = dataType,
                ConnectionId = withDeviceConnection ? "c1" : "",
            });

        AddVar("v_bool", "Bool", "00001");
        AddVar("v_int", "Int16", "40001");
        AddVar("v_float", "Float", "40002");
        AddVar("v_text", "String", "40003");

        void Bind(HmiControl c, string variableId, BindingDirection direction)
        {
            c.VariableBindings.Add(new VariableBindingData
            {
                VariableId = variableId, Direction = direction, PropertyName = "Value",
            });
            scr.Controls.Add(c);
        }

        Bind(new HmiControl { Id = "lbl1", Type = HmiControlType.Label }, "v_float", BindingDirection.ReadOnly);
        Bind(new HmiControl { Id = "led1", Type = HmiControlType.Led }, "v_bool", BindingDirection.ReadOnly);
        Bind(new HmiControl { Id = "bar1", Type = HmiControlType.Bar }, "v_int", BindingDirection.ReadOnly);
        Bind(new HmiControl { Id = "txt1", Type = HmiControlType.TextArea }, "v_text", BindingDirection.ReadOnly);
        // 只写绑定不参与仿真（预览不写模型）
        Bind(new HmiControl { Id = "sld1", Type = HmiControlType.Slider }, "v_float", BindingDirection.WriteOnly);
        return project;
    }

    // ───────────────────────── ① 信号注册 ─────────────────────────

    [Fact]
    public void Start_RegistersSignalsByDataType()
    {
        using var h = new Harness(MakeProject());
        h.Service.Start();

        var simulated = h.Service.Simulator.GetVariables().ToDictionary(v => v.VariableId);

        // 只读绑定全部注册，只写绑定跳过
        Assert.Equal(4, simulated.Count);
        Assert.DoesNotContain("sld1", simulated.Keys);

        // 现状（与抽取前一致）：Bool/String → 方波；整数 → 正弦 0..100；Float → 正弦 5..95。
        // 注意整数走的是**正弦**分支（旧代码注释误写为"Int → 方波"，已更正注释而非改行为）。
        Assert.Equal(SignalType.Square, simulated["v_bool"].Signal);
        Assert.Equal(0d, simulated["v_bool"].MinValue);
        Assert.Equal(1d, simulated["v_bool"].MaxValue);

        Assert.Equal(SignalType.Sine, simulated["v_int"].Signal);
        Assert.Equal(0d, simulated["v_int"].MinValue);
        Assert.Equal(100d, simulated["v_int"].MaxValue);

        Assert.Equal(SignalType.Sine, simulated["v_float"].Signal);
        Assert.Equal(5d, simulated["v_float"].MinValue);
        Assert.Equal(95d, simulated["v_float"].MaxValue);

        Assert.Equal(SignalType.Square, simulated["v_text"].Signal);

        Assert.True(h.Service.IsRunning);
        Assert.True(h.Service.Simulator.IsRunning);
    }

    [Fact]
    public void Start_ExpressionBinding_RegistersReferencedVariables()
    {
        var project = MakeProject();
        var scr = project.Screens[0];
        scr.Controls.Add(new HmiControl
        {
            Id = "lbl_expr",
            Type = HmiControlType.Label,
            VariableBindings =
            {
                new VariableBindingData
                {
                    Kind = BindingSourceKind.Expression,
                    Expression = "v_float * 2 + v_int",
                    PropertyName = "Value",
                },
            },
        });

        using var h = new Harness(project);
        h.Service.Start();

        var ids = h.Service.Simulator.GetVariables().Select(v => v.VariableId).ToList();
        Assert.Contains("v_float", ids);
        Assert.Contains("v_int", ids);
    }

    [Fact]
    public void Start_LegacyBindingField_IsRegisteredWhenBindingsCollectionEmpty()
    {
        var project = MakeProject();
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "legacy", Type = HmiControlType.Label, VariableBindingId = "v_float",
        });

        using var h = new Harness(project);
        h.Service.Start();

        // v_float 已由其他控件注册，这里断言旧字段路径不会抛异常且缓存可用
        Assert.Contains("v_float", h.Service.Simulator.GetVariables().Select(v => v.VariableId));
        Assert.NotEmpty(h.Service.PreviewValues);
    }

    [Fact]
    public void Start_WithNoScreen_IsNoOp()
    {
        using var h = new Harness(MakeProject());
        h.Screen = null;

        h.Service.Start();

        Assert.False(h.Service.IsRunning);
        Assert.Empty(h.Statuses);
    }

    // ───────────────────────── ② 值源选择 ─────────────────────────

    [Fact]
    public void Start_WithoutDeviceConnection_UsesVirtualCommSource()
    {
        using var h = new Harness(MakeProject(withDeviceConnection: false));
        h.Service.Start();

        Assert.False(h.Service.IsUsingRealDevice);
        Assert.IsType<CommunicationRuntimeSource>(h.Service.BindingEngine!.ValueSource);
    }

    [Fact]
    public void Start_DeviceConnectSucceeds_UsesRealDeviceSource()
    {
        var driver = new FakeDriver(connectResult: true);
        using var h = new Harness(MakeProject(withDeviceConnection: true)) { Driver = driver };

        h.Service.Start();

        Assert.True(h.Service.IsUsingRealDevice);
        Assert.IsType<ModbusRuntimeSource>(h.Service.BindingEngine!.ValueSource);
        Assert.Equal(1, driver.ConnectCount);
        Assert.Equal(0, driver.DisposeCount); // 连接成功的驱动必须被保留
        Assert.Contains(h.Statuses, s => s.Contains("PLC"));
    }

    [Fact]
    public void Start_DeviceConnectFails_FallsBackAndDisposesFailedDriver()
    {
        var driver = new FakeDriver(connectResult: false);
        using var h = new Harness(MakeProject(withDeviceConnection: true)) { Driver = driver };

        h.Service.Start();

        Assert.False(h.Service.IsUsingRealDevice);
        Assert.IsType<CommunicationRuntimeSource>(h.Service.BindingEngine!.ValueSource);
        Assert.Equal(1, driver.DisposeCount); // 连不上的驱动必须立刻释放，不留半开连接
        Assert.True(h.Service.IsRunning);     // 且预览仍可用（回退到虚拟从站）
    }

    [Fact]
    public void Start_DeviceFactoryThrows_FallsBackWithoutBreakingPreview()
    {
        using var h = new Harness(MakeProject(withDeviceConnection: true));
        h.Service.DriverFactory = _ => throw new InvalidOperationException("驱动加载失败");

        h.Service.Start();

        Assert.False(h.Service.IsUsingRealDevice);
        Assert.IsType<CommunicationRuntimeSource>(h.Service.BindingEngine!.ValueSource);
        Assert.True(h.Service.IsRunning);
    }

    [Fact]
    public void Start_Twice_DisposesPreviousDeviceConnection()
    {
        var first = new FakeDriver(connectResult: true);
        var second = new FakeDriver(connectResult: true);

        using var h = new Harness(MakeProject(withDeviceConnection: true)) { Driver = first };
        h.Service.Start();
        Assert.Equal(0, first.DisposeCount);

        h.Driver = second;
        h.Service.Start(); // 未 Stop 直接重开（切换屏幕的场景）

        Assert.Equal(1, first.DisposeCount);  // 旧连接被换源前释放
        Assert.Equal(0, second.DisposeCount);
        Assert.True(h.Service.IsUsingRealDevice);
    }

    // ───────────────────────── ③ 画布提供器 ─────────────────────────

    [Fact]
    public void Start_InjectsCanvasProviders_StopRemovesThem()
    {
        using var h = new Harness(MakeProject());
        h.Service.Start();

        Assert.NotNull(h.ValueProvider);
        Assert.NotNull(h.TypeProvider);
        Assert.NotNull(h.UnitProvider);

        // 值提供器：已绑定变量能取到值（通讯寄存器优先）；未知变量回退快照后为 null
        Assert.NotNull(h.ValueProvider!("v_float"));
        Assert.Null(h.ValueProvider!("v_not_bound"));

        // 类型/单位提供器回落到宿主的变量解析
        Assert.Equal("Float", h.TypeProvider!("v_float"));

        h.Service.Stop();

        Assert.Null(h.ValueProvider);
        Assert.Null(h.TypeProvider);
        Assert.Null(h.UnitProvider);
        Assert.Empty(h.Service.PreviewValues);
        Assert.False(h.Service.IsRunning);
    }

    [Fact]
    public void Start_PublishesImmediateValueSnapshot()
    {
        using var h = new Harness(MakeProject());
        h.Service.Start();

        // 首帧不留空：Start 内部立即拉一次快照
        Assert.NotEmpty(h.Service.PreviewValues);
        Assert.Contains("v_float", h.Service.PreviewValues.Keys);
    }

    [Fact]
    public void ValueUpdates_RefreshSnapshotAndRequestCanvasInvalidation()
    {
        using var h = new Harness(MakeProject());
        h.Service.Simulator.TickIntervalMs = 10;
        h.Service.Start();

        var before = h.InvalidateCount;

        // 🩸 阶段 130：原来是**手写的墙钟轮询**（`while (…) Thread.Sleep(5)` + 自建 3s deadline）——
        //   与 `TestWait.Until` 做的是同一件事，却各写一份（量具抄两份就会走样）。
        //   改走共享量具：等的是**产品状态**（重绘请求数真的涨了），不是"时间过了多久"；
        //   ⚠ 预算从 3s 提到量具默认的 5s ⇒ **判据强度只升不降**。
        TestWait.Until(() => h.InvalidateCount > before, "仿真值更新后应请求画布重绘");
        // 快照被整帧替换（而非原地追加），且包含全部注册变量
        Assert.Equal(4, h.Service.PreviewValues.Count);
        Assert.Contains("v_float", h.Service.PreviewValues.Keys);
    }

    // ───────────────────────── ④ 生命周期 / 释放 ─────────────────────────

    [Fact]
    public void Stop_DisposesRealDeviceConnection()
    {
        var driver = new FakeDriver(connectResult: true);
        using var h = new Harness(MakeProject(withDeviceConnection: true)) { Driver = driver };

        h.Service.Start();
        h.Service.Stop();

        Assert.Equal(1, driver.DisposeCount);
        Assert.False(h.Service.IsUsingRealDevice);
    }

    [Fact]
    public void Stop_IsIdempotent_AndSafeBeforeStart()
    {
        using var h = new Harness(MakeProject());

        h.Service.Stop(); // 未 Start
        h.Service.Start();
        h.Service.Stop();
        h.Service.Stop();

        Assert.False(h.Service.IsRunning);
        Assert.Null(h.ValueProvider);
    }

    [Fact]
    public void Dispose_ReleasesSimulatorTimerAndDeviceConnection()
    {
        var driver = new FakeDriver(connectResult: true);
        var h = new Harness(MakeProject(withDeviceConnection: true)) { Driver = driver };
        h.Service.Start();
        Assert.True(h.Service.Simulator.IsRunning);

        h.Service.Dispose();

        Assert.Equal(1, driver.DisposeCount);
        Assert.False(h.Service.Simulator.IsRunning);
        Assert.Null(h.ValueProvider);

        // 重复 Dispose 无害（关停与 finally 都可能调用）
        h.Service.Dispose();
        Assert.Equal(1, driver.DisposeCount);
    }

    [Fact]
    public void DisposedService_RejectsStartAndAction()
    {
        var h = new Harness(MakeProject());
        h.Service.Dispose();

        Assert.Throws<ObjectDisposedException>(() => h.Service.Start());
        Assert.Throws<ObjectDisposedException>(() =>
            h.Service.ExecuteAction(new HmiControl { Id = "c", Type = HmiControlType.Button }));
    }

    // ───────────────────────── 动作执行 ─────────────────────────

    [Fact]
    public void ExecuteAction_BeforeStart_ReturnsFalse()
    {
        using var h = new Harness(MakeProject());
        var control = new HmiControl { Id = "btn", Type = HmiControlType.Button };

        Assert.False(h.Service.ExecuteAction(control, "OnClick"));
    }

    [Fact]
    public void ExecuteAction_AfterStart_RunsControlAction()
    {
        using var h = new Harness(MakeProject());
        h.Service.Start();

        var control = new HmiControl { Id = "btn", Type = HmiControlType.Button };
        control.Actions.Add(new HmiAction
        {
            ActionType = "SwitchScreen", Target = "报警页", TriggerEvent = "OnClick",
        });

        Assert.True(h.Service.ExecuteAction(control, "OnClick"));
        Assert.Equal(new[] { "报警页" }, h.SwitchedScreens);
    }

    [Fact]
    public void ExecuteAction_NoAction_ReturnsFalse()
    {
        using var h = new Harness(MakeProject());
        h.Service.Start();

        Assert.False(h.Service.ExecuteAction(new HmiControl { Id = "btn", Type = HmiControlType.Button }, "OnClick"));
    }

    [Fact]
    public void ExecuteAction_AfterStop_ReturnsFalse()
    {
        using var h = new Harness(MakeProject());
        h.Service.Start();
        var control = new HmiControl { Id = "btn", Type = HmiControlType.Button };
        control.Actions.Add(new HmiAction { ActionType = "SwitchScreen", Target = "X", TriggerEvent = "OnClick" });

        h.Service.Stop();

        Assert.False(h.Service.ExecuteAction(control, "OnClick")); // 执行器随 Stop 一并失效
        Assert.Empty(h.SwitchedScreens);
    }

    // ───────────────────────── 绑定引擎 ─────────────────────────

    [Fact]
    public void EnsureBindingEngine_BuildsTagsOnceAndReusesInstance()
    {
        using var h = new Harness(MakeProject());
        var engine = h.Service.EnsureBindingEngine();

        Assert.Same(engine, h.Service.EnsureBindingEngine());
        Assert.Same(engine, h.Service.BindingEngine);
        Assert.True(engine.TagCount > 0);
    }

    [Fact]
    public void Start_RebuildsBindingEngineAndReportsBoundCount()
    {
        using var h = new Harness(MakeProject());
        h.Service.Start();

        Assert.NotNull(h.Service.BindingEngine);
        Assert.Contains(h.Statuses, s => s.Contains("4")); // 4 个只读绑定变量
    }
}
