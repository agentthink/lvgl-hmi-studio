using System;
using System.Collections.Generic;
using System.Linq;
using Hmi.Communication.Builtin;
using Hmi.Communication.Contracts;
using Hmi.Communication.Loader;
using Xunit;

namespace Hmi.Communication.Tests;

public class CommunicationDriverTests
{
    [Fact]
    public void Registry_LoadBuiltin_ReturnsThreeDrivers()
    {
        var registry = new CommunicationDriverRegistry();
        registry.LoadBuiltinDrivers(new ICommunicationDriver[]
        {
            new ModbusRtuDriver(),
            new ModbusTcpDriver(),
            new MqttDriver()
        });

        Assert.Equal(3, registry.GetAllDrivers().Count());
    }

    [Fact]
    public void Registry_GetDriver_ReturnsCorrectDriver()
    {
        var registry = new CommunicationDriverRegistry();
        registry.LoadBuiltinDrivers(new ICommunicationDriver[]
        {
            new ModbusRtuDriver(),
            new ModbusTcpDriver(),
            new MqttDriver()
        });

        var modbusRtu = registry.GetDriver("modbus-rtu");
        Assert.NotNull(modbusRtu);
        Assert.Equal("Modbus RTU", modbusRtu!.Metadata.Name);
    }

    [Fact]
    public void Registry_GetDriversByTransport_Tcp_ReturnsModbusTcpAndMqtt()
    {
        var registry = new CommunicationDriverRegistry();
        registry.LoadBuiltinDrivers(new ICommunicationDriver[]
        {
            new ModbusRtuDriver(),
            new ModbusTcpDriver(),
            new MqttDriver()
        });

        var tcpDrivers = registry.GetDriversByTransport(TransportType.Tcp).ToList();
        Assert.Equal(2, tcpDrivers.Count);
        Assert.Contains(tcpDrivers, d => d.Metadata.Id == "modbus-tcp");
        Assert.Contains(tcpDrivers, d => d.Metadata.Id == "mqtt");
    }

    [Fact]
    public void Registry_GetDriversByTransport_Serial_ReturnsModbusRtu()
    {
        var registry = new CommunicationDriverRegistry();
        registry.LoadBuiltinDrivers(new ICommunicationDriver[]
        {
            new ModbusRtuDriver(),
            new ModbusTcpDriver(),
            new MqttDriver()
        });

        var serialDrivers = registry.GetDriversByTransport(TransportType.Serial).ToList();
        Assert.Single(serialDrivers);
        Assert.Equal("modbus-rtu", serialDrivers[0].Metadata.Id);
    }

    [Fact]
    public void ModbusRtu_ValidateConnection_ValidConfig_ReturnsIsValid()
    {
        var driver = new ModbusRtuDriver();
        var config = new ConnectionConfig("modbus-rtu", new Dictionary<string, string>
        {
            ["port"] = "COM1",
            ["baud"] = "9600",
            ["dataBits"] = "8",
            ["stopBits"] = "1",
            ["parity"] = "None",
            ["slaveId"] = "1",
            ["timeoutMs"] = "1000"
        });

        var result = driver.ValidateConnection(config);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ModbusRtu_ValidateConnection_InvalidSlaveId_ReturnsError()
    {
        var driver = new ModbusRtuDriver();
        var config = new ConnectionConfig("modbus-rtu", new Dictionary<string, string>
        {
            ["port"] = "COM1",
            ["slaveId"] = "300"
        });

        var result = driver.ValidateConnection(config);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "INVALID_SLAVE_ID");
    }

    [Fact]
    public void ModbusTcp_ValidateConnection_MissingHost_ReturnsError()
    {
        var driver = new ModbusTcpDriver();
        var config = new ConnectionConfig("modbus-tcp", new Dictionary<string, string>
        {
            ["port"] = "502"
        });

        var result = driver.ValidateConnection(config);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "MISSING_HOST");
    }

    [Fact]
    public void Mqtt_ValidateConnection_MissingClientId_ReturnsError()
    {
        var driver = new MqttDriver();
        var config = new ConnectionConfig("mqtt", new Dictionary<string, string>
        {
            ["broker"] = "localhost",
            ["port"] = "1883"
        });

        var result = driver.ValidateConnection(config);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "MISSING_CLIENT_ID");
    }

    [Fact]
    public void Mqtt_ValidateConnection_ValidConfig_ReturnsIsValid()
    {
        var driver = new MqttDriver();
        var config = new ConnectionConfig("mqtt", new Dictionary<string, string>
        {
            ["broker"] = "localhost",
            ["port"] = "1883",
            ["clientId"] = "hmi-test-001"
        });

        var result = driver.ValidateConnection(config);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ModbusRtu_GetConnectionConfigTemplate_HasRequiredFields()
    {
        var driver = new ModbusRtuDriver();
        var template = driver.GetConnectionConfigTemplate();

        var keys = template.Fields.Select(f => f.Key).ToList();
        Assert.Contains("port", keys);
        Assert.Contains("baud", keys);
        Assert.Contains("slaveId", keys);
    }

    [Fact]
    public void ModbusRtu_GeneratedCode_ContainsModbusInclude()
    {
        var driver = new ModbusRtuDriver();
        var code = driver.GenerateRuntimeCode(
            Array.Empty<VariableBinding>(),
            new ToolchainConfig("arm-", "", Array.Empty<string>(), "script.ld", Array.Empty<string>()));

        Assert.Contains("#include \"modbus_rtu.h\"", code);
    }

    [Fact]
    public void Mqtt_GeneratedCode_ContainsMqttInclude()
    {
        var driver = new MqttDriver();
        var code = driver.GenerateRuntimeCode(
            Array.Empty<VariableBinding>(),
            new ToolchainConfig("arm-", "", Array.Empty<string>(), "script.ld", Array.Empty<string>()));

        Assert.Contains("#include \"mqtt_client.h\"", code);
    }

    [Fact]
    public void ModbusRtu_SupportedTransports_ContainsSerial()
    {
        var driver = new ModbusRtuDriver();
        Assert.Contains(TransportType.Serial, driver.SupportedTransports);
    }

    [Fact]
    public void ModbusTcp_SupportedTransports_ContainsTcp()
    {
        var driver = new ModbusTcpDriver();
        Assert.Contains(TransportType.Tcp, driver.SupportedTransports);
    }

    // ── MQTT TLS（阶段 283）：模板露出三档 + **配错就保存不了** ──

    private static ConnectionConfig MqttCfg(params (string Key, string Value)[] kv)
    {
        var p = new Dictionary<string, string>
        {
            ["broker"] = "10.0.0.5", ["port"] = "1883", ["clientId"] = "hmi-01",
        };
        foreach (var (k, v) in kv) p[k] = v;
        return new ConnectionConfig("mqtt", p);
    }

    [Fact]
    public void Mqtt_Template_ExposesTheThreeTlsModes()
    {
        var field = new MqttDriver().GetConnectionConfigTemplate().Fields.Single(f => f.Key == "tls");
        Assert.Equal(ConfigFieldType.Select, field.Type);
        Assert.Equal("off", field.Default);
        Assert.Equal(new[] { "off", "psk", "ca" }, field.Options);

        // 三个配套字段都在（否则选了 psk 也没处填）
        foreach (var key in new[] { "tlsPskId", "tlsPskFile", "tlsCaPath" })
            Assert.Contains(new MqttDriver().GetConnectionConfigTemplate().Fields, f => f.Key == key);
    }

    [Fact]
    public void Mqtt_Validate_TlsOff_AndMissingKeys_AreFine()
    {
        // 老工程：连 tls 键都没有 ⇒ off（向后兼容，不报错）
        Assert.True(new MqttDriver().ValidateConnection(MqttCfg()).IsValid);
        Assert.True(new MqttDriver().ValidateConnection(MqttCfg(("tls", "off"))).IsValid);
    }

    [Fact]
    public void Mqtt_Validate_UnknownMode_IsAnError()
    {
        var r = new MqttDriver().ValidateConnection(MqttCfg(("tls", "ssl")));
        Assert.False(r.IsValid);
        Assert.Contains(r.Issues, i => i.Code == "INVALID_TLS_MODE");
    }

    [Fact]
    public void Mqtt_Validate_Psk_Incomplete_IsAnError()
    {
        var onlyId = new MqttDriver().ValidateConnection(MqttCfg(("tls", "psk"), ("tlsPskId", "hmi-01")));
        Assert.False(onlyId.IsValid);
        Assert.Contains(onlyId.Issues, i => i.Code == "TLS_PSK_INCOMPLETE");

        var onlyFile = new MqttDriver().ValidateConnection(
            MqttCfg(("tls", "psk"), ("tlsPskFile", "/mnt/UDISK/hmi/psk.key")));
        Assert.False(onlyFile.IsValid);

        var complete = new MqttDriver().ValidateConnection(MqttCfg(
            ("tls", "psk"), ("tlsPskId", "hmi-01"), ("tlsPskFile", "/mnt/UDISK/hmi/psk.key")));
        Assert.True(complete.IsValid);
    }

    [Fact]
    public void Mqtt_Validate_Ca_MissingPath_IsAnError()
    {
        var r = new MqttDriver().ValidateConnection(MqttCfg(("tls", "ca")));
        Assert.False(r.IsValid);
        Assert.Contains(r.Issues, i => i.Code == "TLS_CA_MISSING");

        var ok = new MqttDriver().ValidateConnection(MqttCfg(("tls", "ca"), ("tlsCaPath", "/mnt/UDISK/hmi/ca.pem")));
        Assert.True(ok.IsValid);
    }

    [Fact]
    public void Mqtt_Validate_TlsOnPlaintextPort_WarnsButSaves()
    {
        var r = new MqttDriver().ValidateConnection(MqttCfg(
            ("tls", "psk"), ("tlsPskId", "hmi-01"), ("tlsPskFile", "/k"), ("port", "1883")));
        Assert.True(r.IsValid);                                   // 可以自建非标端口 ⇒ 只警告
        Assert.Contains(r.Issues, i => i.Code == "TLS_PORT_LOOKS_PLAINTEXT");

        var standard = new MqttDriver().ValidateConnection(MqttCfg(
            ("tls", "psk"), ("tlsPskId", "hmi-01"), ("tlsPskFile", "/k"), ("port", "8883")));
        Assert.DoesNotContain(standard.Issues, i => i.Code == "TLS_PORT_LOOKS_PLAINTEXT");
    }

    [Fact]
    public void Registry_GetDriver_UnknownId_ReturnsNull()
    {
        var registry = new CommunicationDriverRegistry();
        Assert.Null(registry.GetDriver("non-existent"));
    }
}
