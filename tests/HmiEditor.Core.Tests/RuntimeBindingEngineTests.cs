using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 运行时绑定引擎单元测试 —— 验证绑定 → 通讯标签映射、读写换算、UDT 展开、合并去重。
/// </summary>
public class RuntimeBindingEngineTests
{
    // ── 构建 ──

    [Fact]
    public void BuildRuntimeTags_CollectsBindingsFromAllScreens()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);

        engine.BuildRuntimeTags();

        Assert.Equal(2, engine.TagCount);
    }

    [Fact]
    public void BuildRuntimeTags_MergesDuplicateVariableBindings()
    {
        var project = CreateProject();
        // 两个控件绑定同一变量
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "c3",
            Name = "Label2",
            Type = HmiControlType.Label,
            VariableBindingId = "v_temp"
        });

        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        Assert.Equal(2, engine.TagCount); // v_temp + v_pres，不重复
    }

    [Fact]
    public void BuildRuntimeTags_ResolvesAddressAndConnection()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var tag = engine.FindTag("v_temp");
        Assert.NotNull(tag);
        Assert.Equal("40001", tag.Address);
        Assert.Equal("PLC1", tag.ConnectionName);
        Assert.Equal("modbus-tcp", tag.DriverId);
        Assert.Equal("Main", tag.ScreenName);
        Assert.Equal("TempLabel", tag.ControlName);
    }

    [Fact]
    public void BuildRuntimeTags_ExpandsUdtFieldBinding()
    {
        var project = CreateProject();
        // UDT 实例 + 字段绑定
        var udtDef = new UdtDefinition { Name = "Motor" };
        udtDef.AddField("speed", "Float");
        udtDef.AddField("run", "Bool");
        project.VariableManager.RegisterUdt(udtDef);
        var instance = udtDef.CreateInstance("motor1");
        instance.Address = "500";
        instance.ConnectionId = "conn1";
        project.VariableManager.Variables.Add(instance);

        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "c4",
            Name = "SpeedLabel",
            Type = HmiControlType.Label,
            VariableBindingId = "motor1.speed"
        });

        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var tag = engine.FindTag("motor1.speed");
        Assert.NotNull(tag);
        Assert.Equal("500.speed", tag.Address);
        Assert.Equal("Float", tag.DataType);
        Assert.Equal("conn1", tag.ConnectionId);
    }

    [Fact]
    public void BuildRuntimeTags_SkipsDanglingBindings()
    {
        var project = CreateProject();
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "c5",
            Name = "Broken",
            Type = HmiControlType.Label,
            VariableBindingId = "nonexistent_var"
        });

        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        Assert.Equal(2, engine.TagCount);
        Assert.Null(engine.FindTag("nonexistent_var"));
    }

    [Fact]
    public void BuildRuntimeTags_MergesDirection_AnyWriteWins()
    {
        var project = CreateProject();
        // 第一个控件 ReadOnly 绑定 v_temp，第二个控件 ReadWrite 绑定 v_temp
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "c6",
            Name = "Writable",
            Type = HmiControlType.Slider,
            VariableBindingId = "v_temp",
            BindingMode = "ReadWrite"
        });

        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var tag = engine.FindTag("v_temp");
        Assert.NotNull(tag);
        Assert.Equal(BindingDirection.ReadWrite, tag.Direction);
    }

    [Fact]
    public void BuildRuntimeTags_ReadOnlyBinding_KeepsReadOnly()
    {
        var project = CreateProject();
        // 所有 v_pres 绑定均为只读
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var tag = engine.FindTag("v_pres");
        Assert.NotNull(tag);
        Assert.Equal(BindingDirection.ReadOnly, tag.Direction);
    }

    // ── 读写换算 ──

    [Fact]
    public void ReadValue_AppliesScaleAndOffset()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var source = new FakeValueSource { Values = { ["v_temp"] = 10.0 } };
        engine.ValueSource = source;

        // v_temp: scale=2.0, offset=5.0 → eng = 10*2+5 = 25
        var value = engine.ReadValue("v_temp");
        Assert.Equal(25.0, value);
    }

    [Fact]
    public void ReadValue_ReturnsNull_WhenSourceDisconnected()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        engine.ValueSource = new FakeValueSource { IsConnected = false };

        Assert.Null(engine.ReadValue("v_temp"));
    }

    [Fact]
    public void ReadValue_ReturnsNull_WhenNoSource()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        Assert.Null(engine.ReadValue("v_temp"));
    }

    [Fact]
    public void WriteValue_ConvertsEngineeringToRaw()
    {
        var project = CreateProject();
        // 添加一个 ReadWrite 绑定的控件（Slider 写 v_temp）
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "c7",
            Name = "TempSlider",
            Type = HmiControlType.Slider,
            VariableBindingId = "v_temp",
            BindingMode = "ReadWrite"
        });

        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var source = new FakeValueSource();
        engine.ValueSource = source;

        // v_temp: scale=2.0, offset=5.0 → raw = (100-5)/2 = 47.5
        var ok = engine.WriteValue("v_temp", 100.0);
        Assert.True(ok);
        Assert.Equal(47.5, source.Written["v_temp"]);
    }

    [Fact]
    public void WriteValue_AppliesReverseScaleOffset()
    {
        var project = CreateProject();
        // 添加一个 ReadWrite 绑定的控件（Slider 写 v_pres）
        project.Screens[0].Controls.Add(new HmiControl
        {
            Id = "c8",
            Name = "PresSlider",
            Type = HmiControlType.Slider,
            VariableBindingId = "v_pres",
            BindingMode = "ReadWrite"
        });

        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var source = new FakeValueSource();
        engine.ValueSource = source;

        // v_pres: scale=1.0, offset=0.0 → raw = eng
        var ok = engine.WriteValue("v_pres", 42.0);
        Assert.True(ok);
        Assert.Equal(42.0, source.Written["v_pres"]);
    }

    [Fact]
    public void WriteValue_Rejected_WhenReadOnlyBinding()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var source = new FakeValueSource();
        engine.ValueSource = source;

        // v_pres 绑定为 ReadOnly（见 CreateProject: PresGauge BindingMode=ReadOnly）
        var ok = engine.WriteValue("v_pres", 99.0);
        Assert.False(ok);
        Assert.Empty(source.Written);
    }

    [Fact]
    public void WriteValue_Rejected_WhenNoSource()
    {
        var project = CreateProject();
        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        Assert.False(engine.WriteValue("v_temp", 1.0));
    }

    // ── 辅助 ──

    private static HmiProject CreateProject()
    {
        var project = new HmiProject { Name = "Test" };

        // 连接
        var conn = new DeviceConnection
        {
            Id = "conn1",
            Name = "PLC1",
            DriverId = "modbus-tcp"
        };
        project.VariableManager.Connections.Add(conn);

        // 变量
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_temp",
            Name = "temperature",
            Address = "40001",
            DataType = "Float",
            ConnectionId = "conn1",
            Scale = 2.0,
            Offset = 5.0,
            Unit = "°C"
        });
        project.VariableManager.Variables.Add(new Variable
        {
            Id = "v_pres",
            Name = "pressure",
            Address = "40002",
            DataType = "Int16",
            ConnectionId = "conn1"
        });

        // 屏幕
        var screen = new HmiScreen { Id = "s1", Name = "Main" };
        screen.Controls.Add(new HmiControl
        {
            Id = "c1",
            Name = "TempLabel",
            Type = HmiControlType.Label,
            VariableBindingId = "v_temp",
            BindingMode = "ReadOnly"
        });
        screen.Controls.Add(new HmiControl
        {
            Id = "c2",
            Name = "PresGauge",
            Type = HmiControlType.Gauge,
            VariableBindingId = "v_pres",
            BindingMode = "ReadOnly"
        });
        project.Screens.Add(screen);

        return project;
    }

    /// <summary>测试用值源 —— 内存字典实现 IRuntimeValueSource。</summary>
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
