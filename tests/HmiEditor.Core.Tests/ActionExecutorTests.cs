using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 动作执行器单元测试 —— 验证预览/运行时动作执行链路：
/// 写变量动作 → 绑定引擎 → 值源；系统动作 → 宿主回调。
/// </summary>
public class ActionExecutorTests
{
    // ── 写变量动作 ──

    [Fact]
    public void Execute_SetVariable_WritesEngineeringValue()
    {
        var (engine, source, project) = CreateWritableEnvironment("v_temp", scale: 2.0, offset: 5.0);

        var control = new HmiControl { Type = HmiControlType.Button };
        control.Actions.Add(new HmiAction
        {
            ActionType = "SetVariable",
            Target = "v_temp",
            Value = "100",
            TriggerEvent = "OnClick"
        });

        var executor = new ActionExecutor(engine);
        var ok = executor.ExecuteControlAction(control, "OnClick");

        Assert.True(ok);
        // 工程值 100 → 原始值 (100-5)/2 = 47.5
        Assert.Equal(47.5, source.Written["v_temp"]);
    }

    [Fact]
    public void Execute_SetBit_WritesOne()
    {
        var (engine, source, _) = CreateWritableEnvironment("v_motor");

        var control = new HmiControl { Type = HmiControlType.Button };
        control.Actions.Add(HmiAction.SetBit("v_motor"));

        var executor = new ActionExecutor(engine);
        Assert.True(executor.ExecuteControlAction(control, "OnClick"));
        Assert.Equal(1.0, source.Written["v_motor"]);
    }

    [Fact]
    public void Execute_ResetBit_WritesZero()
    {
        var (engine, source, _) = CreateWritableEnvironment("v_motor");

        var control = new HmiControl { Type = HmiControlType.Button };
        control.Actions.Add(HmiAction.ResetBit("v_motor"));

        var executor = new ActionExecutor(engine);
        Assert.True(executor.ExecuteControlAction(control, "OnClick"));
        Assert.Equal(0.0, source.Written["v_motor"]);
    }

    [Fact]
    public void Execute_ToggleBit_FlipsCurrentValue()
    {
        var (engine, source, _) = CreateWritableEnvironment("v_motor");
        source.Values["v_motor"] = 1.0; // 当前为 ON

        var control = new HmiControl { Type = HmiControlType.Button };
        control.Actions.Add(HmiAction.ToggleBit("v_motor"));

        var executor = new ActionExecutor(engine);
        Assert.True(executor.ExecuteControlAction(control, "OnClick"));
        Assert.Equal(0.0, source.Written["v_motor"]);
    }

    [Fact]
    public void Execute_WriteAction_EmptyTarget_LinksToBindingVariable()
    {
        var (engine, source, _) = CreateWritableEnvironment("v_motor");

        // 控件绑定 v_motor (ReadWrite) + 写动作无 Target → 联动链接
        var control = new HmiControl { Type = HmiControlType.Button };
        control.VariableBindings.Add(new VariableBindingData
        {
            PropertyName = "Value",
            VariableId = "v_motor",
            Direction = BindingDirection.ReadWrite
        });
        control.Actions.Add(new HmiAction
        {
            ActionType = "SetVariable",
            Value = "7",
            TriggerEvent = "OnClick"
        });

        var executor = new ActionExecutor(engine);
        Assert.True(executor.ExecuteControlAction(control, "OnClick"));
        Assert.Equal(7.0, source.Written["v_motor"]);
    }

    [Fact]
    public void Execute_WriteAction_ReadOnlyBinding_Rejected()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();
        var source = new FakeValueSource();
        engine.ValueSource = source;

        // v_pres 全部绑定为 ReadOnly
        var control = new HmiControl { Type = HmiControlType.Button };
        control.Actions.Add(HmiAction.SetVariable("v_pres", "50"));

        var executor = new ActionExecutor(engine);
        Assert.False(executor.ExecuteControlAction(control, "OnClick"));
        Assert.Empty(source.Written);
    }

    [Fact]
    public void Execute_WriteAction_NoSource_Fails()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();
        // 无值源

        var control = new HmiControl { Type = HmiControlType.Button };
        control.Actions.Add(HmiAction.SetBit("v_temp"));

        var executor = new ActionExecutor(engine);
        Assert.False(executor.ExecuteControlAction(control, "OnClick"));
    }

    // ── 系统动作 ──

    [Fact]
    public void Execute_SwitchScreen_InvokesHandler()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var control = new HmiControl { Type = HmiControlType.Button };
        control.Actions.Add(HmiAction.SwitchScreen("Setup"));

        var executor = new ActionExecutor(engine);
        string? switchedTo = null;
        executor.SwitchScreenHandler = s => { switchedTo = s; return true; };

        Assert.True(executor.ExecuteControlAction(control, "OnClick"));
        Assert.Equal("Setup", switchedTo);
    }

    [Fact]
    public void Execute_SwitchScreen_NoHandler_Fails()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var control = new HmiControl { Type = HmiControlType.Button };
        control.Actions.Add(HmiAction.SwitchScreen("Setup"));

        var executor = new ActionExecutor(engine);
        Assert.False(executor.ExecuteControlAction(control, "OnClick"));
    }

    [Fact]
    public void Execute_ExecuteScript_InvokesHandler()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var control = new HmiControl { Type = HmiControlType.Button };
        control.Actions.Add(HmiAction.ExecuteScript("script_1"));

        var executor = new ActionExecutor(engine);
        string? scriptId = null;
        executor.ExecuteScriptHandler = s => { scriptId = s; return true; };

        Assert.True(executor.ExecuteControlAction(control, "OnClick"));
        Assert.Equal("script_1", scriptId);
    }

    [Fact]
    public void Execute_ShowAlarms_InvokesSystemHandler()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var control = new HmiControl { Type = HmiControlType.Button };
        control.Actions.Add(HmiAction.ShowAlarms);

        var executor = new ActionExecutor(engine);
        string? systemAction = null;
        executor.SystemActionHandler = a => { systemAction = a; return true; };

        Assert.True(executor.ExecuteControlAction(control, "OnClick"));
        Assert.Equal("ShowAlarms", systemAction);
    }

    // ── 触发事件过滤 ──

    [Fact]
    public void Execute_EventFilter_OnlyRunsMatchingTrigger()
    {
        var (engine, source, _) = CreateWritableEnvironment("v_motor");

        var control = new HmiControl { Type = HmiControlType.Button };
        control.Actions.Add(new HmiAction
        {
            ActionType = "MomentaryOn",
            Target = "v_motor",
            TriggerEvent = "OnPress"
        });

        var executor = new ActionExecutor(engine);

        // 触发 OnClick 不匹配 OnPress → 不执行
        Assert.False(executor.ExecuteControlAction(control, "OnClick"));
        Assert.Empty(source.Written);

        // 触发 OnPress → 执行
        Assert.True(executor.ExecuteControlAction(control, "OnPress"));
        Assert.Equal(1.0, source.Written["v_motor"]);
    }

    // ── 辅助 ──

    private static (RuntimeBindingEngine, FakeValueSource, HmiProject) CreateWritableEnvironment(
        string variableId, double scale = 1.0, double offset = 0.0)
    {
        var project = CreateProject();
        // 添加 ReadWrite 绑定
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "c_write",
            Name = "WriteControl",
            Type = HmiControlType.Slider,
            VariableBindingId = variableId,
            BindingMode = "ReadWrite"
        });

        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();
        var source = new FakeValueSource();
        engine.ValueSource = source;
        return (engine, source, project);
    }

    private static HmiProject CreateProject()
    {
        var project = new HmiProject { Name = "Test" };
        var conn = new DeviceConnection { Id = "conn1", Name = "PLC1", DriverId = "modbus-tcp" };
        project.VariableManager.Connections.Add(conn);
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_temp", Name = "temperature", Address = "40001",
            DataType = "Float", ConnectionId = "conn1",
            Scale = 2.0, Offset = 5.0
        });
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_motor", Name = "motor", Address = "40002",
            DataType = "Bool", ConnectionId = "conn1"
        });
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_pres", Name = "pressure", Address = "40003",
            DataType = "Int16", ConnectionId = "conn1"
        });

        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1", Name = "TempLabel", Type = HmiControlType.Label,
            VariableBindingId = "v_temp", BindingMode = "ReadOnly"
        });
        screen.Controls.Add(new HmiControl
        {
            Id = "c2", Name = "PresGauge", Type = HmiControlType.Gauge,
            VariableBindingId = "v_pres", BindingMode = "ReadOnly"
        });
        project.Screens.Add(screen);
        return project;
    }

    private sealed class FakeValueSource : IRuntimeValueSource
    {
        public Dictionary<string, double> Values { get; } = new();
        public Dictionary<string, double> Written { get; } = new();
        public bool IsConnected { get; set; } = true;
        public string Name => "FakeSource";

        public bool TryRead(string variableId, out double value) =>
            Values.TryGetValue(variableId, out value);

        public bool TryWrite(string variableId, double value)
        {
            Written[variableId] = value;
            Values[variableId] = value;
            return true;
        }
    }
}
