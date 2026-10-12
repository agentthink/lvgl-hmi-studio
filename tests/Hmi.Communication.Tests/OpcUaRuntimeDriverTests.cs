using Hmi.Communication.Builtin;
using Hmi.Communication.Contracts;
using Xunit;

namespace Hmi.Communication.Tests;

/// <summary>
/// I7: OPC UA 运行时驱动端到端测试 —— 真实 TCP 连接到内存模拟服务器，
/// 验证 HEL/ACK 握手、OpenSecureChannel、Read/Write 全链路。
/// </summary>
public class OpcUaRuntimeDriverTests
{
    private static ConnectionConfig BuildConfig(int port) => new(
        "opc-ua",
        new Dictionary<string, string>
        {
            ["endpointUrl"] = $"opc.tcp://127.0.0.1:{port}",
            ["timeoutMs"] = "3000"
        });

    [Fact]
    public void Connect_ToFakeServer_HandshakeSucceeds()
    {
        using var server = new FakeOpcUaServer();
        using var driver = new OpcUaRuntimeDriver { TimeoutMs = 3000, ConnectTimeoutMs = 3000 };

        Assert.True(driver.Connect(BuildConfig(server.Port), TimeSpan.FromSeconds(3)));
        Assert.True(driver.IsConnected);
        Assert.Equal(1, server.ConnectCount);
        driver.Disconnect();
        Assert.False(driver.IsConnected);
    }

    [Fact]
    public void Connect_BadEndpoint_Throws()
    {
        using var driver = new OpcUaRuntimeDriver { TimeoutMs = 1000, ConnectTimeoutMs = 1000 };
        var config = new ConnectionConfig("opc-ua",
            new Dictionary<string, string> { ["endpointUrl"] = "http://bad" });

        Assert.Throws<CommunicationRuntimeException>(() =>
            driver.Connect(config, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Connect_UnreachablePort_Throws()
    {
        using var driver = new OpcUaRuntimeDriver { TimeoutMs = 1500, ConnectTimeoutMs = 1500 };
        // 使用必然不可达的高端口（保留端口）
        var config = new ConnectionConfig("opc-ua",
            new Dictionary<string, string> { ["endpointUrl"] = "opc.tcp://127.0.0.1:1" });

        Assert.Throws<CommunicationRuntimeException>(() =>
            driver.Connect(config, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task ReadValue_ReturnsServerNodeValue()
    {
        using var server = new FakeOpcUaServer();
        server.SetNode("ns=2;s=Line3.Temp", 78.5);

        using var driver = new OpcUaRuntimeDriver { TimeoutMs = 3000 };
        Assert.True(driver.Connect(BuildConfig(server.Port), TimeSpan.FromSeconds(3)));

        var value = await driver.ReadValueAsync("ns=2;s=Line3.Temp");
        Assert.NotNull(value);
        Assert.Equal(78.5, value!.Value, 4);
    }

    [Fact]
    public async Task ReadValue_UnknownNode_ReturnsNull()
    {
        using var server = new FakeOpcUaServer();
        using var driver = new OpcUaRuntimeDriver { TimeoutMs = 3000 };
        Assert.True(driver.Connect(BuildConfig(server.Port), TimeSpan.FromSeconds(3)));

        var value = await driver.ReadValueAsync("ns=2;s=DoesNotExist");
        Assert.Null(value);
    }

    [Fact]
    public async Task WriteValue_ThenReadBack_Matches()
    {
        using var server = new FakeOpcUaServer();
        server.SetNode("ns=2;i=100", 0);

        using var driver = new OpcUaRuntimeDriver { TimeoutMs = 3000 };
        Assert.True(driver.Connect(BuildConfig(server.Port), TimeSpan.FromSeconds(3)));

        var ok = await driver.WriteValueAsync("ns=2;i=100", 42.5);
        Assert.True(ok);
        Assert.Equal(42.5, server.GetNode("ns=2;i=100"));

        var read = await driver.ReadValueAsync("ns=2;i=100");
        Assert.Equal(42.5, read!.Value, 4);
    }

    [Fact]
    public void ReadValues_Batch_ReturnsAll()
    {
        using var server = new FakeOpcUaServer();
        server.SetNode("ns=2;s=A", 1.0);
        server.SetNode("ns=2;s=B", 2.0);

        using var driver = new OpcUaRuntimeDriver { TimeoutMs = 3000 };
        Assert.True(driver.Connect(BuildConfig(server.Port), TimeSpan.FromSeconds(3)));

        var nodes = new[]
        {
            Hmi.Communication.Builtin.OpcUa.OpcUaNodeId.StringNode(2, "A"),
            Hmi.Communication.Builtin.OpcUa.OpcUaNodeId.StringNode(2, "B")
        };
        var values = driver.ReadValuesAsync(nodes);

        Assert.Equal(2, values.Count);
        Assert.Equal(1.0, values[0]!.Value, 4);
        Assert.Equal(2.0, values[1]!.Value, 4);
    }

    [Fact]
    public void StringAddressContract_Implemented()
    {
        using var server = new FakeOpcUaServer();
        using var driver = new OpcUaRuntimeDriver();

        // 值源适配检测
        Assert.IsAssignableFrom<IStringAddressRuntimeDriver>(driver);
    }
}
