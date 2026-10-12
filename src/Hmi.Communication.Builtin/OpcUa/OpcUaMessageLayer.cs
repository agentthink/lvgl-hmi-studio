using System.Net.Sockets;

namespace Hmi.Communication.Builtin.OpcUa;

/// <summary>OPC UA 安全通道消息头解析结果。</summary>
public sealed class OpcUaMessageFrame
{
    public string MessageType { get; set; } = "";   // "HEL"/"ACK"/"OPN"/"CLO"/"MSG"
    public uint MessageSize { get; set; }
    public uint ChannelId { get; set; }
    public byte[] Body { get; set; } = Array.Empty<byte>(); // 去掉 8 字节头之后的全部
}

/// <summary>OPC UA 安全通道上下文 —— 握手后保存 token 与序号。</summary>
public sealed class OpcUaChannelContext
{
    public uint ChannelId { get; set; }
    public uint TokenId { get; set; }
    public uint SequenceNumber { get; set; } = 1;
    public uint RequestId { get; set; } = 1;
}

/// <summary>
/// OPC UA 消息层 —— 处理安全通道消息分块与握手。
/// 实现子集: HEL/ACK 协商 + OPN/CLO(MessageSecurityMode=None, SecurityPolicy=None) + MSG 数据消息。
/// 单块消息（不实现大消息分块重组 —— 编辑器轮询场景消息远小于缓冲区）。
/// </summary>
public static class OpcUaMessageLayer
{
    public const uint DefaultReceiveBuffer = 65536;
    public const uint DefaultSendBuffer = 65536;
    public const uint DefaultMaxMessageSize = 65536;
    public const uint DefaultMaxChunkCount = 1;

    // 服务 ID
    public const uint ServiceIdOpenSecureChannelRequest = 446;
    public const uint ServiceIdOpenSecureChannelResponse = 447;
    public const uint ServiceIdCloseSecureChannelRequest = 448;
    public const uint ServiceIdCloseSecureChannelResponse = 449;
    public const uint ServiceIdReadRequest = 631;
    public const uint ServiceIdReadResponse = 632;
    public const uint ServiceIdWriteRequest = 633;
    public const uint ServiceIdWriteResponse = 634;

    public const string SecurityPolicyNoneUri = "http://opcfoundation.org/UA/SecurityPolicy#None";

    /// <summary>读取并解析一个完整消息帧（8 字节头 + 体）。</summary>
    public static OpcUaMessageFrame ReadFrame(NetworkStream stream)
    {
        var header = ReadExactly(stream, 8);
        var messageType = System.Text.Encoding.ASCII.GetString(header, 0, 3);
        var messageSize = (uint)(header[4] | (header[5] << 8) | (header[6] << 16) | (header[7] << 24));

        if (messageSize < 8 || messageSize > 16 * 1024 * 1024)
            throw new CommunicationFrameException($"非法消息大小: {messageSize}");

        var body = ReadExactly(stream, (int)messageSize - 8);
        uint channelId = 0;
        if (messageType is "OPN" or "CLO" or "MSG")
        {
            // ⚠ messageSize 8..11 也能过上面的下界检查，但 body 只有 0..3 字节 ⇒
            // `BitConverter.ToUInt32(body, 0)` 会抛 ArgumentOutOfRange（不是本层约定的
            // CommunicationFrameException）。畸形帧必须先被点名为帧错误（阶段 121 修复）。
            if (body.Length < 4)
                throw new CommunicationFrameException(
                    $"消息体过短: {messageType} 需要 ≥4 字节 channelId，实为 {body.Length} 字节");
            channelId = BitConverter.ToUInt32(body, 0);
        }

        return new OpcUaMessageFrame
        {
            MessageType = messageType,
            MessageSize = messageSize,
            ChannelId = channelId,
            Body = body
        };
    }

    /// <summary>构造 HEL 握手请求。</summary>
    public static byte[] BuildHello(string endpointUrl, uint protocolVersion = 0,
        uint receiveBuffer = DefaultReceiveBuffer, uint sendBuffer = DefaultSendBuffer,
        uint maxMessageSize = DefaultMaxMessageSize, uint maxChunkCount = DefaultMaxChunkCount)
    {
        var w = new OpcUaBinaryWriter();
        w.WriteUInt32(protocolVersion);
        w.WriteUInt32(receiveBuffer);
        w.WriteUInt32(sendBuffer);
        w.WriteUInt32(maxMessageSize);
        w.WriteUInt32(maxChunkCount);
        w.WriteString(endpointUrl);
        return WrapHeaderless("HEL", w.ToArray());
    }

    /// <summary>构造 OPN(OpenSecureChannel) 请求 —— SecurityMode=None。</summary>
    public static byte[] BuildOpenSecureChannel(OpcUaChannelContext ctx, uint requestHandle,
        uint requestedLifetime = 600000)
    {
        var w = new OpcUaBinaryWriter();
        // AsymmetricAlgorithmSecurityHeader
        w.WriteString(SecurityPolicyNoneUri);
        w.WriteByteString(null); // senderCertificate
        w.WriteByteString(null); // receiverCertificateThumbprint
        // SequenceHeader
        w.WriteUInt32(ctx.SequenceNumber++);
        w.WriteUInt32(ctx.RequestId++);
        // RequestHeader
        WriteRequestHeader(w, requestHandle);
        // OpenSecureChannelRequest
        w.WriteUInt32(0);   // clientProtocolVersion
        w.WriteUInt32(0);   // requestType = Issue
        w.WriteUInt32(1);   // securityMode = None
        w.WriteByteString(null); // clientNonce
        w.WriteUInt32(requestedLifetime);
        return Wrap("OPN", ctx.ChannelId, w.ToArray());
    }

    /// <summary>构造 CLO(CloseSecureChannel) 请求。</summary>
    public static byte[] BuildCloseSecureChannel(OpcUaChannelContext ctx, uint requestHandle)
    {
        var w = new OpcUaBinaryWriter();
        w.WriteString(SecurityPolicyNoneUri);
        w.WriteByteString(null);
        w.WriteByteString(null);
        w.WriteUInt32(ctx.SequenceNumber++);
        w.WriteUInt32(ctx.RequestId++);
        WriteRequestHeader(w, requestHandle);
        return Wrap("CLO", ctx.ChannelId, w.ToArray());
    }

    /// <summary>构造 MSG 数据消息（Read/Write 请求体）。</summary>
    public static byte[] BuildMessage(OpcUaChannelContext ctx, byte[] serviceBody)
    {
        var w = new OpcUaBinaryWriter();
        w.WriteUInt32(ctx.TokenId); // SymmetricAlgorithmSecurityHeader
        w.WriteUInt32(ctx.SequenceNumber++);
        w.WriteUInt32(ctx.RequestId++);
        w.WriteBytes(serviceBody);
        return Wrap("MSG", ctx.ChannelId, w.ToArray());
    }

    /// <summary>解析 OPN 响应,提取通道 ID 与 TokenId。</summary>
    public static OpcUaChannelContext ParseOpenResponse(byte[] body)
    {
        var r = new OpcUaBinaryReader(body);
        var channelId = r.ReadUInt32();
        // AsymmetricAlgorithmSecurityHeader
        r.ReadString();              // securityPolicyUri
        r.ReadByteString();          // senderCertificate
        r.ReadByteString();          // receiverCertificateThumbprint
        // SequenceHeader
        r.ReadUInt32();              // sequenceNumber
        r.ReadUInt32();              // requestId
        // ResponseHeader
        ReadResponseHeader(r, out var serviceResult);
        if (serviceResult != 0)
            throw new CommunicationFrameException($"OpenSecureChannel 失败: 0x{serviceResult:X8}");
        // OpenSecureChannelResponse
        r.ReadUInt32();              // serverProtocolVersion
        var tokenId = r.ReadUInt32(); // securityToken.tokenId
        r.ReadDateTime();            // securityToken.createdAt
        r.ReadUInt32();              // securityToken.revisedLifetime
        r.ReadByteString();          // serverNonce
        return new OpcUaChannelContext { ChannelId = channelId, TokenId = tokenId };
    }

    // ── 服务器端辅助（测试模拟服务器用）──

    /// <summary>构造 ACK 握手响应。</summary>
    public static byte[] BuildHelloReply()
    {
        var w = new OpcUaBinaryWriter();
        w.WriteUInt32(0);                            // protocolVersion
        w.WriteUInt32(DefaultReceiveBuffer);         // receiveBufferSize
        w.WriteUInt32(DefaultSendBuffer);            // sendBufferSize
        w.WriteUInt32(DefaultMaxMessageSize);        // maxMessageSize
        w.WriteUInt32(DefaultMaxChunkCount);         // maxChunkCount
        return WrapHeaderless("ACK", w.ToArray());
    }

    /// <summary>解析 OPN 请求（返回通道上下文 + channelId）。</summary>
    public static OpcUaChannelContext ParseOpenRequest(byte[] body, out uint channelId)
    {
        var r = new OpcUaBinaryReader(body);
        channelId = r.ReadUInt32();
        r.ReadString();          // securityPolicyUri
        r.ReadByteString();      // senderCertificate
        r.ReadByteString();      // receiverCertificateThumbprint
        r.ReadUInt32();          // sequenceNumber
        r.ReadUInt32();          // requestId
        // RequestHeader
        r.ReadNodeId();
        r.ReadDateTime();
        r.ReadUInt32();          // requestHandle
        r.ReadUInt32();
        r.ReadString();
        r.ReadUInt32();
        r.ReadNodeId();
        r.ReadUInt32();          // clientProtocolVersion
        r.ReadUInt32();          // requestType
        r.ReadUInt32();          // securityMode
        r.ReadByteString();      // clientNonce
        r.ReadUInt32();          // requestedLifetime
        return new OpcUaChannelContext { ChannelId = channelId, TokenId = 42 };
    }

    /// <summary>构造 OPN 响应（服务器）。</summary>
    public static byte[] BuildOpenResponse(uint channelId, OpcUaChannelContext ctx, out uint tokenId)
    {
        tokenId = 0xA1B2C3D4;
        var w = new OpcUaBinaryWriter();
        w.WriteString(SecurityPolicyNoneUri);
        w.WriteByteString(null);
        w.WriteByteString(null);
        w.WriteUInt32(1);       // sequenceNumber
        w.WriteUInt32(1);       // requestId
        WriteResponseHeader(w, 1, 0);
        w.WriteUInt32(0);       // serverProtocolVersion
        w.WriteUInt32(tokenId); // securityToken.tokenId
        w.WriteDateTime(DateTime.UtcNow);
        w.WriteUInt32(600000);  // revisedLifetime
        w.WriteByteString(null); // serverNonce
        return Wrap("OPN", channelId, w.ToArray());
    }

    /// <summary>服务器端: 写 ResponseHeader。</summary>
    public static void WriteResponseHeader(OpcUaBinaryWriter w, uint requestHandle, uint serviceResult)
    {
        w.WriteDateTime(DateTime.UtcNow);
        w.WriteUInt32(requestHandle);
        w.WriteUInt32(serviceResult);
        w.WriteByte(0);            // serviceDiagnostics: 空 DiagnosticInfo
        w.WriteInt32(0);           // stringTable: 空数组
        w.WriteNodeId(OpcUaNodeId.Null); // additionalHeader
    }

    /// <summary>解析 MSG 响应体 → 服务体(跳过 SymmetricHeader + SequenceHeader)。</summary>
    public static byte[] ExtractMessageBody(byte[] body)
    {
        var r = new OpcUaBinaryReader(body);
        var channelId = r.ReadUInt32();
        var tokenId = r.ReadUInt32();
        r.ReadUInt32(); // sequenceNumber
        r.ReadUInt32(); // requestId
        var start = r.Position;
        var result = new byte[body.Length - start];
        Array.Copy(body, start, result, 0, result.Length);
        return result;
    }

    // ── RequestHeader / ResponseHeader ──

    /// <summary>写 RequestHeader（authenticationToken = Null NodeId）。</summary>
    public static void WriteRequestHeader(OpcUaBinaryWriter w, uint requestHandle)
    {
        w.WriteNodeId(OpcUaNodeId.Null);  // authenticationToken
        w.WriteDateTime(DateTime.UtcNow); // timestamp
        w.WriteUInt32(requestHandle);     // requestHandle
        w.WriteUInt32(0);                 // returnDiagnostics
        w.WriteString("");                // auditEntryId
        w.WriteUInt32(10000);             // timeoutHint
        w.WriteNullExtensionObject();     // additionalHeader
    }

    /// <summary>读 ResponseHeader,返回 serviceResult。</summary>
    public static void ReadResponseHeader(OpcUaBinaryReader r, out uint serviceResult)
    {
        r.ReadDateTime();    // timestamp
        r.ReadUInt32();      // requestHandle
        serviceResult = r.ReadUInt32();
        // serviceDiagnostics: DiagnosticInfo（单字节 encodingMask=0）
        r.ReadByte();
        // stringTable: String[]
        var count = r.ReadInt32();
        if (count > 0)
        {
            for (int i = 0; i < count; i++) r.ReadString();
        }
        // additionalHeader: Null ExtensionObject
        var nodeId = r.ReadNodeId();
        // 注意: 若 additionalHeader 非 null 会带 encodingMask, 此处仅接受 Null
        if (!nodeId.IsNull)
            throw new CommunicationFrameException("additionalHeader 非空（不支持）");
    }

    /// <summary>包装消息头: 3 字母类型 + 保留字节 0 + messageSize + channelId + body。</summary>
    private static byte[] Wrap(string type, uint channelId, byte[] body)
    {
        var result = new byte[12 + body.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(result, 0);
        result[3] = 0x46; // 'F' = isFinal
        result[4] = (byte)(result.Length & 0xFF);
        result[5] = (byte)((result.Length >> 8) & 0xFF);
        result[6] = (byte)((result.Length >> 16) & 0xFF);
        result[7] = (byte)((result.Length >> 24) & 0xFF);
        BitConverter.GetBytes(channelId).CopyTo(result, 8);
        body.CopyTo(result, 12);
        return result;
    }

    /// <summary>包装无 channelId 的消息（HEL/ACK）: 8 字节头 + body。</summary>
    private static byte[] WrapHeaderless(string type, byte[] body)
    {
        var result = new byte[8 + body.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(result, 0);
        result[3] = 0x46;
        result[4] = (byte)(result.Length & 0xFF);
        result[5] = (byte)((result.Length >> 8) & 0xFF);
        result[6] = (byte)((result.Length >> 16) & 0xFF);
        result[7] = (byte)((result.Length >> 24) & 0xFF);
        body.CopyTo(result, 8);
        return result;
    }

    private static byte[] ReadExactly(NetworkStream stream, int count)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = stream.Read(buffer, offset, count - offset);
            if (read <= 0)
                throw new CommunicationFrameException("OPC UA 连接已关闭");
            offset += read;
        }
        return buffer;
    }
}

/// <summary>OPC UA 帧/协议错误。</summary>
public sealed class CommunicationFrameException : Exception
{
    public CommunicationFrameException(string message) : base(message) { }
    public CommunicationFrameException(string message, Exception inner) : base(message, inner) { }
}
