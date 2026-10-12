using System.Net;
using System.Net.Sockets;
using Hmi.Communication.Builtin.OpcUa;

namespace Hmi.Communication.Tests;

/// <summary>
/// I7: 内存模拟 OPC UA 服务器（None 安全模式子集）—— 供端到端测试。
/// 支持: HEL/ACK、OpenSecureChannel、Read/Write（Value 属性，节点表驱动）。
/// 仅用于测试，非生产代码。
/// </summary>
public sealed class FakeOpcUaServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;
    private readonly Dictionary<string, double> _nodes = new();

    public int Port { get; }

    /// <summary>连接计数（用于验证握手）。</summary>
    public int ConnectCount { get; private set; }

    public FakeOpcUaServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(AcceptLoop);
    }

    /// <summary>设置节点值。</summary>
    public void SetNode(string nodeIdText, double value) => _nodes[nodeIdText] = value;

    public double? GetNode(string nodeIdText)
        => _nodes.TryGetValue(nodeIdText, out var v) ? v : null;

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => ServeClient(client));
        }
    }

    private void ServeClient(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            ConnectCount++;

            // HEL → ACK
            var hel = OpcUaMessageLayer.ReadFrame(stream);
            if (hel.MessageType != "HEL") return;

            var ack = OpcUaMessageLayer.BuildHelloReply();
            stream.Write(ack, 0, ack.Length);
            stream.Flush();

            // OPN → OPN
            var opn = OpcUaMessageLayer.ReadFrame(stream);
            if (opn.MessageType != "OPN") return;
            var ctx = OpcUaMessageLayer.ParseOpenRequest(opn.Body, out var channelId);

            var opnResp = OpcUaMessageLayer.BuildOpenResponse(channelId, ctx, out _);
            stream.Write(opnResp, 0, opnResp.Length);
            stream.Flush();

            // MSG 循环
            while (true)
            {
                var frame = OpcUaMessageLayer.ReadFrame(stream);
                if (frame.MessageType == "CLO") return;
                if (frame.MessageType != "MSG") continue;

                var serviceBody = OpcUaMessageLayer.ExtractMessageBody(frame.Body);
                var response = HandleService(serviceBody);
                if (response == null) continue;

                var msg = OpcUaMessageLayer.BuildMessage(ctx, response);
                stream.Write(msg, 0, msg.Length);
                stream.Flush();
            }
        }
        catch (Exception)
        {
            // 客户端断开或协议错误: 关闭连接
        }
        finally
        {
            client.Dispose();
        }
    }

    /// <summary>
    /// 分发 Read/Write 服务请求。
    /// 判定: ReadRequest 以 maxAge(double)=0 开头（首字节 0x00），
    /// WriteRequest 以节点数(Int32)开头（首字节 ≥ 1）。
    /// </summary>
    private byte[]? HandleService(byte[] serviceBody)
    {
        var r = new OpcUaBinaryReader(serviceBody);
        // RequestHeader
        r.ReadNodeId();
        r.ReadDateTime();
        var requestHandle = r.ReadUInt32();
        r.ReadUInt32();
        r.ReadString();
        r.ReadUInt32();
        r.ReadNodeId();

        var firstByte = r.ReadByte();
        r = new OpcUaBinaryReader(serviceBody); // 重读以从头解析

        // 跳过 RequestHeader 再次定位
        r.ReadNodeId();
        r.ReadDateTime();
        requestHandle = r.ReadUInt32();
        r.ReadUInt32();
        r.ReadString();
        r.ReadUInt32();
        r.ReadNodeId();

        return firstByte == 0x00 ? HandleRead(r, requestHandle) : HandleWrite(r, requestHandle);
    }

    private byte[] HandleRead(OpcUaBinaryReader r, uint requestHandle)
    {
        r.ReadDouble();  // maxAge
        r.ReadUInt32();  // timestampsToReturn
        var count = r.ReadInt32();

        var nodes = new OpcUaNodeId[count];
        for (int i = 0; i < count; i++)
        {
            nodes[i] = r.ReadNodeId();
            r.ReadUInt32(); // attributeId
            r.ReadString(); // indexRange
            r.ReadNodeId(); // dataEncoding
        }

        // 构造响应
        var w = new OpcUaBinaryWriter();
        OpcUaMessageLayer.WriteResponseHeader(w, requestHandle, 0);

        // results
        for (int i = 0; i < count; i++)
        {
            var key = nodes[i].ToString();
            w.WriteUInt32(_nodes.ContainsKey(key) ? 0 : 0x80340000); // BadNodeIdUnknown
        }
        // diagnosticInfos
        for (int i = 0; i < count; i++)
        {
            w.WriteByte(0);
            if (_nodes.ContainsKey(nodes[i].ToString()))
            {
                w.WriteDataValue(new OpcUaDataValue
                {
                    Value = _nodes[nodes[i].ToString()],
                    BuiltInType = OpcUaBuiltInType.Double,
                    SourceTimestamp = DateTime.UtcNow
                });
            }
        }
        return w.ToArray();
    }

    private byte[] HandleWrite(OpcUaBinaryReader r, uint requestHandle)
    {
        var count = r.ReadInt32();
        var statuses = new uint[count];

        for (int i = 0; i < count; i++)
        {
            var node = r.ReadNodeId();
            r.ReadUInt32(); // attributeId
            r.ReadString(); // indexRange
            var dv = r.ReadDataValue();

            var key = node.ToString();
            if (dv.Value is double d)
            {
                _nodes[key] = d;
                statuses[i] = 0;
            }
            else
            {
                statuses[i] = 0x80740000; // BadTypeMismatch
            }
        }

        var w = new OpcUaBinaryWriter();
        OpcUaMessageLayer.WriteResponseHeader(w, requestHandle, 0);
        foreach (var s in statuses) w.WriteUInt32(s);
        for (int i = 0; i < count; i++) w.WriteByte(0);
        return w.ToArray();
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { /* 忽略 */ }
    }
}
