using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// CommConfigCodeGen 单元测试 —— IDE 通讯配置 → generated_comm.h 生成。
/// 覆盖 COM→/dev/ttyS 映射、parity 转换、无连接空文件、modbus-tcp/mqtt。
/// </summary>
public class CommConfigCodeGenTests
{
    private static DeviceConnection MakeRtu(Action<Dictionary<string, string>>? cfg = null)
    {
        var c = new DeviceConnection { DriverId = "modbus-rtu" };
        c.Config["port"] = "COM3";
        c.Config["baud"] = "9600";
        c.Config["parity"] = "None";
        c.Config["timeoutMs"] = "300";
        c.Config["slaveId"] = "1";
        cfg?.Invoke(c.Config);
        return c;
    }

    [Fact]
    public void ModbusRtu_Com3_MapsToDevTtyS3()
    {
        var code = CommConfigCodeGen.Generate(new[] { MakeRtu() });
        // 串口号 COM3 → UART3 /dev/ttyS3（RS485）
        Assert.Contains("#define HMI_COMM_RTU_DEV   \"/dev/ttyS3\"", code);
        Assert.Contains("#define HMI_COMM_RTU_BAUD  9600", code);
        Assert.Contains("#define HMI_COMM_RTU_PARITY 'N'", code);
        Assert.Contains("#define HMI_COMM_RTU_TMO_MS 300", code);
        Assert.Contains("#define HMI_COMM_RTU_SLAVES \"1\"", code);
    }

    [Fact]
    public void ModbusRtu_Com1_MapsToDevTtyS1()
    {
        var code = CommConfigCodeGen.Generate(new[] { MakeRtu(c => c["port"] = "COM1") });
        Assert.Contains("#define HMI_COMM_RTU_DEV   \"/dev/ttyS1\"", code);
    }

    [Fact]
    public void ModbusRtu_OddParity_MapsToO()
    {
        var code = CommConfigCodeGen.Generate(new[] { MakeRtu(c => c["parity"] = "Odd") });
        Assert.Contains("#define HMI_COMM_RTU_PARITY 'O'", code);
    }

    [Fact]
    public void ModbusRtu_NonComPath_PassedThrough()
    {
        // 非 COM 输入（如 /dev/ttyUSB0）原样输出
        var code = CommConfigCodeGen.Generate(new[] { MakeRtu(c => c["port"] = "/dev/ttyUSB0") });
        Assert.Contains("#define HMI_COMM_RTU_DEV   \"/dev/ttyUSB0\"", code);
    }

    [Fact]
    public void NoConnections_EmitsEmptyHeader()
    {
        var code = CommConfigCodeGen.Generate(null);
        Assert.Contains("#ifndef HMI_GENERATED_COMM_H", code);
        Assert.DoesNotContain("HMI_COMM_RTU", code);
        Assert.DoesNotContain("HMI_COMM_TCP", code);
        Assert.DoesNotContain("HMI_COMM_MQTT", code);
    }

    [Fact]
    public void DisabledConnection_IsSkipped()
    {
        var conn = MakeRtu();
        conn.IsEnabled = false;
        var code = CommConfigCodeGen.Generate(new[] { conn });
        Assert.DoesNotContain("HMI_COMM_RTU", code);
    }

    [Fact]
    public void ModbusTcp_EmitsHostPort()
    {
        var c = new DeviceConnection
        {
            DriverId = "modbus-tcp",
            Config = new Dictionary<string, string>
            {
                ["host"] = "192.168.1.10", ["port"] = "502", ["timeoutMs"] = "3000", ["slaveId"] = "1"
            }
        };
        var code = CommConfigCodeGen.Generate(new[] { c });
        Assert.Contains("#define HMI_COMM_TCP_HOST   \"192.168.1.10\"", code);
        Assert.Contains("#define HMI_COMM_TCP_PORT   502", code);
        Assert.Contains("#define HMI_COMM_TCP_SLAVES \"1\"", code);
    }

    [Fact]
    public void Mqtt_EmitsBrokerClient()
    {
        var c = new DeviceConnection
        {
            DriverId = "mqtt",
            Config = new Dictionary<string, string>
            {
                ["broker"] = "10.0.0.5", ["port"] = "1883", ["clientId"] = "hmi-01",
                ["username"] = "u", ["password"] = "p", ["keepAlive"] = "60"
            }
        };
        var code = CommConfigCodeGen.Generate(new[] { c });
        Assert.Contains("#define HMI_COMM_MQTT_HOST       \"10.0.0.5\"", code);
        Assert.Contains("#define HMI_COMM_MQTT_PORT       1883", code);
        Assert.Contains("#define HMI_COMM_MQTT_CLIENT_ID  \"hmi-01\"", code);
        Assert.Contains("#define HMI_COMM_MQTT_KEEPALIVE  60", code);
    }

    [Fact]
    public void QuotedHost_IsEscaped()
    {
        // 引号/反斜杠转义，防止破坏 C 字符串字面量
        var c = new DeviceConnection
        {
            DriverId = "mqtt",
            Config = new Dictionary<string, string> { ["broker"] = "a\"b\\c", ["clientId"] = "x", ["keepAlive"] = "60" }
        };
        var code = CommConfigCodeGen.Generate(new[] { c });
        Assert.Contains("#define HMI_COMM_MQTT_HOST       \"a\\\"b\\\\c\"", code);
    }

    [Fact]
    public void ModbusRtu_MultipleSlaves_EmitsSlaveList()
    {
        // 一条连接下多个子设备（从站）→ SLAVES 列表按站号去重排序输出
        var conn = MakeRtu();
        conn.Slaves.Add(new DeviceSlave { Name = "站1", SlaveAddress = 1, ConnectionId = conn.Id });
        conn.Slaves.Add(new DeviceSlave { Name = "站3", SlaveAddress = 3, ConnectionId = conn.Id });
        conn.Slaves.Add(new DeviceSlave { Name = "站2", SlaveAddress = 2, ConnectionId = conn.Id });
        conn.Slaves.Add(new DeviceSlave { Name = "停用站", SlaveAddress = 9, ConnectionId = conn.Id, IsEnabled = false });

        var code = CommConfigCodeGen.Generate(new[] { conn });
        Assert.Contains("#define HMI_COMM_RTU_SLAVES \"1, 2, 3\"", code);
    }

    [Fact]
    public void ModbusTcp_SingleSlave_FallsBackToConfigSlaveId()
    {
        // 无子设备时回退连接参数 slaveId（向后兼容）
        var c = new DeviceConnection
        {
            DriverId = "modbus-tcp",
            Config = new Dictionary<string, string>
            {
                ["host"] = "192.168.1.10", ["port"] = "502", ["timeoutMs"] = "3000", ["slaveId"] = "7"
            }
        };
        var code = CommConfigCodeGen.Generate(new[] { c });
        Assert.Contains("#define HMI_COMM_TCP_SLAVES \"7\"", code);
    }

    // ── 运行时连接配置（comm_config.ini，下载到实机免重编固件） ──

    [Fact]
    public void RuntimeConfig_EmitsAllProtocols()
    {
        var rtu = MakeRtu(c => { c["port"] = "COM3"; c["baud"] = "19200"; });
        var tcp = new DeviceConnection
        {
            DriverId = "modbus-tcp",
            Config = new Dictionary<string, string>
            {
                ["host"] = "192.168.1.10", ["port"] = "502", ["timeoutMs"] = "3000", ["slaveId"] = "1"
            }
        };
        var mqtt = new DeviceConnection
        {
            DriverId = "mqtt",
            Config = new Dictionary<string, string>
            {
                ["broker"] = "10.0.0.5", ["port"] = "1883", ["clientId"] = "hmi-01",
                ["username"] = "u", ["password"] = "p", ["keepAlive"] = "60"
            }
        };

        var ini = CommConfigCodeGen.GenerateRuntimeConfigText(new[] { rtu, tcp, mqtt });

        Assert.Contains("rtu.dev=/dev/ttyS3", ini);
        Assert.Contains("rtu.baud=19200", ini);
        Assert.Contains("rtu.parity=N", ini);
        Assert.Contains("tcp.host=192.168.1.10", ini);
        Assert.Contains("tcp.port=502", ini);
        Assert.Contains("mqtt.host=10.0.0.5", ini);
        Assert.Contains("mqtt.client_id=hmi-01", ini);
        Assert.Contains("mqtt.keepalive=60", ini);
    }

    [Fact]
    public void RuntimeConfig_NoConnections_ReturnsEmpty()
    {
        Assert.Equal("", CommConfigCodeGen.GenerateRuntimeConfigText(null));
        Assert.Equal("", CommConfigCodeGen.GenerateRuntimeConfigText(Array.Empty<DeviceConnection>()));
    }

    [Fact]
    public void RuntimeConfig_DisabledConnection_IsSkipped()
    {
        var conn = MakeRtu();
        conn.IsEnabled = false;
        Assert.Equal("", CommConfigCodeGen.GenerateRuntimeConfigText(new[] { conn }));
    }

    [Fact]
    public void RuntimeConfig_SameProtocol_TakesFirstEnabled()
    {
        var tcp1 = new DeviceConnection
        {
            DriverId = "modbus-tcp",
            Config = new Dictionary<string, string> { ["host"] = "10.0.0.1", ["port"] = "502", ["slaveId"] = "1" }
        };
        var tcp2 = new DeviceConnection
        {
            DriverId = "modbus-tcp",
            Config = new Dictionary<string, string> { ["host"] = "10.0.0.2", ["port"] = "502", ["slaveId"] = "2" }
        };
        var ini = CommConfigCodeGen.GenerateRuntimeConfigText(new[] { tcp1, tcp2 });
        Assert.Contains("tcp.host=10.0.0.1", ini);
        Assert.DoesNotContain("10.0.0.2", ini);
    }

    [Fact]
    public void RuntimeConfig_ValueWithNewline_IsSanitized()
    {
        var mqtt = new DeviceConnection
        {
            DriverId = "mqtt",
            Config = new Dictionary<string, string> { ["broker"] = "10.0.0.5", ["clientId"] = "x\r\ny", ["keepAlive"] = "60" }
        };
        var ini = CommConfigCodeGen.GenerateRuntimeConfigText(new[] { mqtt });
        // 换行/回车替换为空格，防止注入额外键值行
        Assert.Contains("mqtt.client_id=x  y", ini);
    }
}
