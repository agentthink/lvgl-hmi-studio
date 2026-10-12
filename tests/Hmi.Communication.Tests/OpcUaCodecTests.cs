using Hmi.Communication.Builtin;
using Hmi.Communication.Builtin.OpcUa;
using Hmi.Communication.Contracts;
using Xunit;

namespace Hmi.Communication.Tests;

/// <summary>
/// I7: OPC UA 编解码与节点寻址单元测试。
/// </summary>
public class OpcUaCodecTests
{
    // ── NodeId 解析 ──

    [Theory]
    [InlineData("ns=2;s=Line3.Temp", 2, "Line3.Temp")]
    [InlineData("s=temp", 0, "temp")]
    [InlineData("temp", 0, "temp")]
    public void ParseNodeId_StringForms(string text, ushort ns, string id)
    {
        var node = OpcUaNodeId.TryParse(text);
        Assert.NotNull(node);
        Assert.Equal(ns, node!.NamespaceIndex);
        Assert.Equal(id, node.StringIdentifier);
    }

    [Theory]
    [InlineData("ns=2;i=100", 2u, 100u)]
    [InlineData("i=85", 0u, 85u)]
    [InlineData("2258", 0u, 2258u)]
    public void ParseNodeId_NumericForms(string text, ushort ns, uint id)
    {
        var node = OpcUaNodeId.TryParse(text);
        Assert.NotNull(node);
        Assert.Equal(ns, node!.NamespaceIndex);
        Assert.Equal(id, node.Numeric);
    }

    [Fact]
    public void ParseNodeId_GuidForm()
    {
        var node = OpcUaNodeId.TryParse("ns=2;g=8d4b2e10-1c9f-4f9a-8c3d-2f6e9a1b3c4d");
        Assert.NotNull(node);
        Assert.Equal(2, node!.NamespaceIndex);
        Assert.Equal(Guid.Parse("8d4b2e10-1c9f-4f9a-8c3d-2f6e9a1b3c4d"), node.GuidIdentifier);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ns=abc;x=1")]
    public void ParseNodeId_Invalid_ReturnsNull(string? text)
    {
        Assert.Null(OpcUaNodeId.TryParse(text));
    }

    [Fact]
    public void NodeId_ToString_RoundTrips()
    {
        var original = OpcUaNodeId.TryParse("ns=2;s=Line3.Temp");
        var text = original!.ToString();
        var reparsed = OpcUaNodeId.TryParse(text);
        Assert.Equal("ns=2;s=Line3.Temp", text);
        Assert.Equal(original.NamespaceIndex, reparsed!.NamespaceIndex);
        Assert.Equal(original.StringIdentifier, reparsed.StringIdentifier);
    }

    // ── 编解码往返 ──

    [Fact]
    public void WriterReader_BasicTypes_RoundTrip()
    {
        var w = new OpcUaBinaryWriter();
        w.WriteUInt32(0xDEADBEEF);
        w.WriteInt32(-12345);
        w.WriteDouble(3.14159);
        w.WriteBoolean(true);
        w.WriteString("温度 传感器");
        w.WriteByteString(new byte[] { 1, 2, 3 });
        w.WriteDateTime(new DateTime(2026, 8, 2, 10, 30, 0, DateTimeKind.Utc));
        w.WriteGuid(Guid.Parse("8d4b2e10-1c9f-4f9a-8c3d-2f6e9a1b3c4d"));

        var r = new OpcUaBinaryReader(w.ToArray());
        Assert.Equal(0xDEADBEEFu, r.ReadUInt32());
        Assert.Equal(-12345, r.ReadInt32());
        Assert.Equal(3.14159, r.ReadDouble(), 6);
        Assert.True(r.ReadBoolean());
        Assert.Equal("温度 传感器", r.ReadString());
        Assert.Equal(new byte[] { 1, 2, 3 }, r.ReadByteString());
        Assert.Equal(new DateTime(2026, 8, 2, 10, 30, 0, DateTimeKind.Utc), r.ReadDateTime());
        Assert.Equal(Guid.Parse("8d4b2e10-1c9f-4f9a-8c3d-2f6e9a1b3c4d"), r.ReadGuid());
    }

    [Fact]
    public void NodeId_BinaryEncoding_RoundTrip()
    {
        // TwoByte (ns=0, 值 5)
        var w1 = new OpcUaBinaryWriter();
        w1.WriteNodeId(OpcUaNodeId.NumericNode(0, 5));
        var r1 = new OpcUaBinaryReader(w1.ToArray());
        Assert.Equal("i=5", r1.ReadNodeId().ToString());

        // FourByte (ns=2, 值 100)
        var w2 = new OpcUaBinaryWriter();
        w2.WriteNodeId(OpcUaNodeId.NumericNode(2, 100));
        var r2 = new OpcUaBinaryReader(w2.ToArray());
        Assert.Equal("ns=2;i=100", r2.ReadNodeId().ToString());

        // Numeric (ns=2, 值 70000 > 16bit)
        var w3 = new OpcUaBinaryWriter();
        w3.WriteNodeId(OpcUaNodeId.NumericNode(2, 70000));
        var r3 = new OpcUaBinaryReader(w3.ToArray());
        Assert.Equal("ns=2;i=70000", r3.ReadNodeId().ToString());

        // String (ns=2, s=temp)
        var w4 = new OpcUaBinaryWriter();
        w4.WriteNodeId(OpcUaNodeId.StringNode(2, "temp"));
        var r4 = new OpcUaBinaryReader(w4.ToArray());
        var parsed = r4.ReadNodeId();
        Assert.Equal(2, parsed.NamespaceIndex);
        Assert.Equal("temp", parsed.StringIdentifier);
    }

    [Theory]
    [InlineData(OpcUaBuiltInType.Boolean, true, 1.0)]
    [InlineData(OpcUaBuiltInType.Double, 3.25, 3.25)]
    [InlineData(OpcUaBuiltInType.Int32, -7, -7.0)]
    [InlineData(OpcUaBuiltInType.UInt16, (ushort)42, 42.0)]
    [InlineData(OpcUaBuiltInType.String, "3.14", 3.14)]
    public void Variant_RoundTrip_AndToDouble(byte type, object value, double expected)
    {
        var w = new OpcUaBinaryWriter();
        w.WriteVariant(value, type);
        var r = new OpcUaBinaryReader(w.ToArray());
        var (decoded, decodedType, isArray) = r.ReadVariant();
        Assert.False(isArray);
        Assert.Equal(type, decodedType);

        var dv = new OpcUaDataValue { Value = decoded, BuiltInType = decodedType };
        Assert.Equal(expected, OpcUaServiceCodec.ToDouble(dv)!.Value, 4);
    }

    [Fact]
    public void Variant_DoubleArray_RoundTrip()
    {
        var w = new OpcUaBinaryWriter();
        w.WriteVariant(new double[] { 1.5, 2.5, 3.5 }, OpcUaBuiltInType.Double, isArray: true);
        var r = new OpcUaBinaryReader(w.ToArray());
        var (decoded, type, isArray) = r.ReadVariant();
        Assert.True(isArray);
        Assert.Equal(OpcUaBuiltInType.Double, type);
        var arr = Assert.IsType<double[]>(decoded);
        Assert.Equal(new[] { 1.5, 2.5, 3.5 }, arr);
    }

    [Fact]
    public void DataValue_RoundTrip_WithStatusAndTimestamp()
    {
        var w = new OpcUaBinaryWriter();
        w.WriteDataValue(new OpcUaDataValue
        {
            Value = 42.5,
            BuiltInType = OpcUaBuiltInType.Double,
            HasStatusCode = true,
            StatusCode = 0,
            SourceTimestamp = new DateTime(2026, 8, 2, 8, 0, 0, DateTimeKind.Utc)
        });

        var r = new OpcUaBinaryReader(w.ToArray());
        var dv = r.ReadDataValue();
        Assert.Equal(42.5, (double)dv.Value!, 4);
        Assert.True(dv.HasStatusCode);
        Assert.Equal(0u, dv.StatusCode);
        Assert.Equal(new DateTime(2026, 8, 2, 8, 0, 0, DateTimeKind.Utc), dv.SourceTimestamp);
        Assert.True(dv.IsGood);
    }

    // ── Read/Write 服务编解码 ──

    [Fact]
    public void ReadRequest_Build_ParseableBySelf()
    {
        var body = OpcUaServiceCodec.BuildReadRequest(
            7, new[] { OpcUaNodeId.StringNode(2, "temp"), OpcUaNodeId.NumericNode(0, 85) });

        var r = new OpcUaBinaryReader(body);
        // RequestHeader
        r.ReadNodeId();
        r.ReadDateTime();
        Assert.Equal(7u, r.ReadUInt32()); // requestHandle
        r.ReadUInt32();                   // returnDiagnostics
        r.ReadString();                   // auditEntryId
        r.ReadUInt32();                   // timeoutHint
        r.ReadNodeId();                   // additionalHeader (null)
        // maxAge / timestampsToReturn
        r.ReadDouble();
        Assert.Equal(0u, r.ReadUInt32());
        // nodesToRead
        Assert.Equal(2, r.ReadInt32());
        var n1 = r.ReadNodeId();
        Assert.Equal(13u, r.ReadUInt32()); // attributeId = Value
        Assert.Null(r.ReadString());       // indexRange
        r.ReadNodeId();                    // dataEncoding null
        var n2 = r.ReadNodeId();
        Assert.Equal("ns=2;s=temp", n1.ToString());
        Assert.Equal("i=85", n2.ToString());
    }

    // ── 端点解析 ──
    [Theory]
    [InlineData("opc.tcp://192.168.1.1:4840", "192.168.1.1", 4840)]
    [InlineData("opc.tcp://plc.siemens.local:50000/path", "plc.siemens.local", 50000)]
    [InlineData("opc.tcp://10.0.0.5", "10.0.0.5", 4840)]
    public void ParseEndpoint_Variants(string url, string host, int port)
    {
        var (h, p) = OpcUaRuntimeDriver.ParseEndpoint(url);
        Assert.Equal(host, h);
        Assert.Equal(port, p);
    }

    // ── 畸形帧硬化（阶段 121）──

    [Fact]
    public void ReadString_HugeDeclaredLength_ThrowsFormatException()
    {
        // 4 字节长度前缀 = int.MaxValue（长度来自服务端），后面没有数据。
        // 旧 `Ensure` 写的是 `_offset + count > Length` ⇒ 加法**溢出成负数**、检查通过，
        // 随后 `Encoding.UTF8.GetString(buffer, 4, int.MaxValue)` 抛 ArgumentOutOfRange
        // （不是本层约定的 FormatException）。判据：必须是 FormatException。
        var reader = new OpcUaBinaryReader(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F });

        var ex = Assert.Throws<FormatException>(() => reader.ReadString());
        Assert.Contains("缓冲区不足", ex.Message);
    }

    [Fact]
    public void ReadFrame_MessageSizeEight_ThrowsFrameException()
    {
        // messageSize=8 ⇒ body 0 字节，而 MSG 需要 ≥4 字节 channelId。
        // 旧代码直接 `BitConverter.ToUInt32(body, 0)` ⇒ ArgumentOutOfRange（未按帧错误点名）。
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            using var client = new System.Net.Sockets.TcpClient();
            client.Connect(System.Net.IPAddress.Loopback, port);
            using var serverSide = listener.AcceptTcpClient();

            var frame = new byte[] { (byte)'M', (byte)'S', (byte)'G', 0x00, 0x08, 0x00, 0x00, 0x00 };
            serverSide.GetStream().Write(frame, 0, frame.Length);

            var ex = Assert.Throws<CommunicationFrameException>(
                () => OpcUaMessageLayer.ReadFrame(client.GetStream()));
            Assert.Contains("过短", ex.Message);
        }
        finally
        {
            listener.Stop();
        }
    }
}
