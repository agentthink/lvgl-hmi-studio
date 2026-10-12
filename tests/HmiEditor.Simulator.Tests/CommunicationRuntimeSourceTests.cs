using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Simulator.Tests;

/// <summary>
/// 通讯运行时值源单元测试 —— 验证 CommunicationSimulator → IRuntimeValueSource 桥接。
/// </summary>
public class CommunicationRuntimeSourceTests
{
    [Fact]
    public void TryRead_ResolvesAddress_AndReadsRegister()
    {
        var commSim = new CommunicationSimulator();
        commSim.WriteRegister(40001, 42.5);

        var source = new CommunicationRuntimeSource(
            commSim,
            variableAddressResolver: v => v == "v_temp" ? "40001" : null);

        Assert.True(source.TryRead("v_temp", out var value));
        Assert.Equal(42.5, value);
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenNoAddressMapping()
    {
        var commSim = new CommunicationSimulator();
        var source = new CommunicationRuntimeSource(
            commSim,
            variableAddressResolver: _ => null);

        Assert.False(source.TryRead("v_unknown", out _));
    }

    [Fact]
    public void TryRead_ReturnsFalse_WhenDisconnected()
    {
        var commSim = new CommunicationSimulator();
        commSim.Disconnect();

        var source = new CommunicationRuntimeSource(commSim, v => "40001");

        Assert.False(source.TryRead("v_temp", out _));
    }

    [Fact]
    public void TryWrite_WritesToRegister()
    {
        var commSim = new CommunicationSimulator();
        var source = new CommunicationRuntimeSource(
            commSim,
            variableAddressResolver: v => v == "v_temp" ? "40001" : null);

        Assert.True(source.TryWrite("v_temp", 88.0));
        Assert.Equal(88.0, commSim.ReadRegister(40001));
    }

    [Fact]
    public void TryWrite_ReturnsFalse_WhenNoAddressMapping()
    {
        var commSim = new CommunicationSimulator();
        var source = new CommunicationRuntimeSource(
            commSim,
            variableAddressResolver: _ => null);

        Assert.False(source.TryWrite("v_unknown", 1.0));
    }

    [Fact]
    public void DefaultAddressParser_ExtractsDigits()
    {
        Assert.Equal(40001, CommunicationRuntimeSource.DefaultAddressParser("40001"));
        // "4x0001" 提取数字得 "40001"（区号 4 + 偏移 0001 = 直接寻址）
        Assert.Equal(40001, CommunicationRuntimeSource.DefaultAddressParser("4x0001"));
        Assert.Equal(400123, CommunicationRuntimeSource.DefaultAddressParser("400123"));
        Assert.Null(CommunicationRuntimeSource.DefaultAddressParser(""));
        Assert.Null(CommunicationRuntimeSource.DefaultAddressParser("abc"));
    }

    [Fact]
    public void DefaultAddressParser_StripsSlavePrefix()
    {
        // 回归：子设备变量显示地址 "站号:地址" 应解析为纯地址寄存器号（而非拼成 140001）
        Assert.Equal(40001, CommunicationRuntimeSource.DefaultAddressParser("1:40001"));
        Assert.Equal(1, CommunicationRuntimeSource.DefaultAddressParser("2:00001"));
        Assert.Equal(30001, CommunicationRuntimeSource.DefaultAddressParser("7:30001"));
        // 纯数字前缀一律剥离（模拟器地址只会有 "40001" / "站号:40001" 两种形式）
        Assert.Equal(5, CommunicationRuntimeSource.DefaultAddressParser("3:5"));
    }

    [Fact]
    public void CustomAddressParser_SupportsModbusPrefix()
    {
        var commSim = new CommunicationSimulator();
        // Modbus 保持寄存器: 40001 → 偏移 0
        var source = new CommunicationRuntimeSource(
            commSim,
            variableAddressResolver: v => v == "v_reg" ? "40001" : null,
            addressParser: addr =>
            {
                if (!int.TryParse(addr, out var value)) return null;
                return value >= 40001 && value <= 49999 ? value - 40001 : null;
            });

        commSim.WriteRegister(0, 7.5);
        Assert.True(source.TryRead("v_reg", out var value));
        Assert.Equal(7.5, value);
    }

    [Fact]
    public void Engine_CommunicationSource_EndToEnd()
    {
        // 端到端: RuntimeBindingEngine + CommunicationRuntimeSource + CommunicationSimulator
        var project = new HmiEditor.Core.Models.HmiProject { Name = "E2E" };
        var conn = new HmiEditor.Core.Models.DeviceConnection
        {
            Id = "conn1",
            Name = "PLC1",
            DriverId = "modbus-tcp"
        };
        project.VariableManager.Connections.Add(conn);
        project.VariableManager.Variables.Add(new HmiEditor.Core.Models.Variable
        {
            Id = "v_speed",
            Name = "speed",
            Address = "40001",
            DataType = "Float",
            ConnectionId = "conn1",
            Scale = 2.0,
            Offset = 5.0
        });
        var screen = new HmiEditor.Core.Models.HmiScreen { Id = "s1", Name = "Main" };
        screen.Controls.Add(new HmiEditor.Core.Models.HmiControl
        {
            Id = "c1",
            Name = "SpeedLabel",
            Type = HmiEditor.Core.Models.HmiControlType.Label,
            VariableBindingId = "v_speed",
            BindingMode = "ReadOnly"
        });
        project.Screens.Add(screen);

        var engine = new RuntimeBindingEngine(project);
        engine.BuildRuntimeTags();

        var commSim = new CommunicationSimulator();
        commSim.WriteRegister(40001, 10.0); // 原始寄存器值

        engine.ValueSource = new CommunicationRuntimeSource(
            commSim,
            v => engine.FindTag(v)?.Address);

        // 工程值 = 10 * 2 + 5 = 25
        var engineering = engine.ReadValue("v_speed");
        Assert.Equal(25.0, engineering);
    }
}
