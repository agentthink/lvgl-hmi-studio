using System.Net.Sockets;
using Hmi.Communication.Builtin.OpcUa;
using Hmi.Communication.Contracts;

namespace Hmi.Communication.Builtin;

/// <summary>
/// OPC UA 运行时驱动 —— 编辑器内真实连接 OPC UA 服务器（轮询读写）。
///
/// 协议实现（OPC UA Part 4/6 二进制子集）:
///   - 握手: HEL/ACK 协商 + OpenSecureChannel(MessageSecurityMode=None,
///     SecurityPolicy=None) + CloseSecureChannel
///   - 服务: Read(631)/Write(633), 属性 Value(13), 单块消息
///   - 寻址: "ns=2;s=temp" / "ns=2;i=100" / "i=85" / "ns=0;i=2258"
///
/// 设计要点（与 ModbusTcpRuntimeDriver 对齐）:
///   - 每请求独立事务 + 同步等待, 超时保护
///   - 实现 IRuntimeCommunicationDriver（Modbus 语义方法抛 NotSupported,
///     供连接检测/生命周期使用）+ IStringAddressRuntimeDriver
///     （字符串节点寻址的读/写, 值源实际调用路径）
///   - 无 OPCFoundation 依赖 —— 自包含二进制编码, 兼容 NativeAOT 与嵌入式定位
///
/// 已知限制: 不支持安全策略(签名/加密)、订阅推送、浏览节点树、
/// 大消息分块。None 安全模式 + 轮询覆盖多数工程调试与数据采集场景。
/// </summary>
public sealed class OpcUaRuntimeDriver : IRuntimeCommunicationDriver,
    IStringAddressRuntimeDriver, IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly object _lock = new();
    private OpcUaChannelContext _channel = new();
    private uint _requestHandle;
    private int _timeoutMs = 3000;

    /// <summary>连接超时（毫秒）。</summary>
    public int ConnectTimeoutMs { get; set; } = 3000;

    /// <summary>请求超时（毫秒）。</summary>
    public int TimeoutMs
    {
        get => _timeoutMs;
        set => _timeoutMs = value > 0 ? value : 3000;
    }

    /// <summary>日志输出。</summary>
    public Action<string>? Log { get; set; }

    public bool IsConnected => _client?.Connected == true && _stream != null && _channel.TokenId != 0;

    /// <summary>连接配置参数（用于日志）。</summary>
    private string _endpointUrl = "";

    public bool Connect(ConnectionConfig config, TimeSpan timeout)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        Disconnect();

        var p = config.Parameters;
        if (!p.TryGetValue("endpointUrl", out var endpoint) || string.IsNullOrWhiteSpace(endpoint))
            throw new CommunicationRuntimeException("缺少连接参数: endpointUrl (如 opc.tcp://192.168.1.1:4840)");
        if (!p.TryGetValue("timeoutMs", out var toStr) || !int.TryParse(toStr, out var to))
            to = 3000;
        TimeoutMs = to;
        _endpointUrl = endpoint;

        try
        {
            var (host, port) = ParseEndpoint(endpoint);

            var client = new TcpClient();
            // ⚠ 不用 `connectTask.Wait(ms)`（同仓 ModbusTcpRuntimeDriver 已把它当作缺陷修掉）：
            //   超时后底层连接任务仍在跑、异常无人观察，而且 Dispose 取消不了它。
            //   用 CancellationToken 才能真正停下这次连接尝试（阶段 120 与兄弟驱动对齐）。
            var effectiveTimeout = timeout > TimeSpan.Zero
                ? timeout
                : TimeSpan.FromMilliseconds(Math.Max(1, ConnectTimeoutMs));
            try
            {
                using var cts = new CancellationTokenSource(effectiveTimeout);
                client.ConnectAsync(host, port, cts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                client.Dispose();
                throw new CommunicationRuntimeException($"连接超时: {host}:{port}");
            }
            catch
            {
                client.Dispose();   // 本地 client 还没挂到字段上，外层 Dispose() 管不到它
                throw;
            }

            _client = client;
            _stream = client.GetStream();
            _stream.ReadTimeout = TimeoutMs;
            _stream.WriteTimeout = TimeoutMs;

            // 1. HEL 握手
            var hello = OpcUaMessageLayer.BuildHello(endpoint);
            _stream.Write(hello, 0, hello.Length);
            _stream.Flush();

            var ack = OpcUaMessageLayer.ReadFrame(_stream);
            if (ack.MessageType != "ACK")
                throw new CommunicationRuntimeException($"握手失败: 期望 ACK, 收到 {ack.MessageType}");

            // 2. OpenSecureChannel
            var opn = OpcUaMessageLayer.BuildOpenSecureChannel(_channel, NextRequestHandle());
            _stream.Write(opn, 0, opn.Length);
            _stream.Flush();

            var opnResp = OpcUaMessageLayer.ReadFrame(_stream);
            if (opnResp.MessageType != "OPN")
                throw new CommunicationRuntimeException($"OpenSecureChannel 失败: 收到 {opnResp.MessageType}");
            _channel = OpcUaMessageLayer.ParseOpenResponse(opnResp.Body);

            Log?.Invoke($"[OpcUa] 已连接 {endpoint} (channel={_channel.ChannelId}, token={_channel.TokenId})");
            return true;
        }
        catch (CommunicationRuntimeException)
        {
            Dispose();
            throw;
        }
        catch (Exception ex)
        {
            Dispose();
            throw new CommunicationRuntimeException($"OPC UA 连接失败 {endpoint}: {ex.Message}", ex);
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            // 尽力发送 CloseSecureChannel（失败忽略）
            if (_client?.Connected == true && _stream != null && _channel.TokenId != 0)
            {
                try
                {
                    var clo = OpcUaMessageLayer.BuildCloseSecureChannel(_channel, NextRequestHandle());
                    _stream.Write(clo, 0, clo.Length);
                    _stream.Flush();
                }
                catch
                {
                    // 忽略关闭握手错误
                }
            }

            _stream?.Dispose();
            _stream = null;
            _client?.Dispose();
            _client = null;
            _channel = new OpcUaChannelContext();
        }
        Log?.Invoke("[OpcUa] 已断开");
    }

    // ── IStringAddressRuntimeDriver ──

    /// <summary>按节点地址读取工程值（失败返回 null）。</summary>
    public Task<double?> ReadValueAsync(string address, CancellationToken ct = default)
    {
        var node = OpcUaNodeId.TryParse(address);
        if (node == null)
            throw new CommunicationRuntimeException($"无效的 OPC UA 节点地址: {address}");

        var values = ReadValuesAsync(new[] { node }, ct);
        return Task.FromResult(values.Count > 0 ? values[0] : null);
    }

    public Task<bool> WriteValueAsync(string address, double value, CancellationToken ct = default)
    {
        var node = OpcUaNodeId.TryParse(address);
        if (node == null)
            throw new CommunicationRuntimeException($"无效的 OPC UA 节点地址: {address}");

        var statuses = WriteValuesAsync(
            new[] { (node, (object?)value, OpcUaBuiltInType.Double, false) }, ct);
        return Task.FromResult(statuses.Count > 0 && statuses[0] == OpcUaServiceCodec.GoodResult);
    }

    // ── OPC UA 服务调用 ──

    /// <summary>批量读取节点值。</summary>
    public IReadOnlyList<double?> ReadValuesAsync(IReadOnlyList<OpcUaNodeId> nodes,
        CancellationToken ct = default)
    {
        if (nodes.Count == 0) return Array.Empty<double?>();

        lock (_lock)
        {
            EnsureConnected();
            var requestHandle = NextRequestHandle();
            var serviceBody = OpcUaServiceCodec.BuildReadRequest(requestHandle, nodes);
            var frame = OpcUaMessageLayer.BuildMessage(_channel, serviceBody);
            _stream!.Write(frame, 0, frame.Length);
            _stream.Flush();

            var resp = OpcUaMessageLayer.ReadFrame(_stream);
            if (resp.MessageType != "MSG")
                throw new CommunicationRuntimeException($"Read 响应异常: {resp.MessageType}");

            var body = OpcUaMessageLayer.ExtractMessageBody(resp.Body);
            var values = OpcUaServiceCodec.ParseReadResponse(body, nodes.Count);

            return values.Select(OpcUaServiceCodec.ToDouble).ToList();
        }
    }

    /// <summary>批量写入节点值。</summary>
    public IReadOnlyList<uint> WriteValuesAsync(
        IReadOnlyList<(OpcUaNodeId Node, object? Value, byte BuiltInType, bool IsArray)> items,
        CancellationToken ct = default)
    {
        if (items.Count == 0) return Array.Empty<uint>();

        lock (_lock)
        {
            EnsureConnected();
            var requestHandle = NextRequestHandle();
            var serviceBody = OpcUaServiceCodec.BuildWriteRequest(requestHandle, items);
            var frame = OpcUaMessageLayer.BuildMessage(_channel, serviceBody);
            _stream!.Write(frame, 0, frame.Length);
            _stream.Flush();

            var resp = OpcUaMessageLayer.ReadFrame(_stream);
            if (resp.MessageType != "MSG")
                throw new CommunicationRuntimeException($"Write 响应异常: {resp.MessageType}");

            var body = OpcUaMessageLayer.ExtractMessageBody(resp.Body);
            return OpcUaServiceCodec.ParseWriteResponse(body, items.Count);
        }
    }

    // ── IRuntimeCommunicationDriver（Modbus 语义, 不适用）──

    public Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count, CancellationToken ct = default)
        => throw new CommunicationRuntimeException("OPC UA 不使用寄存器寻址，请用 ReadValueAsync(节点ID)");

    public Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken ct = default)
        => throw new CommunicationRuntimeException("OPC UA 不使用寄存器寻址，请用 WriteValueAsync(节点ID, 值)");

    public Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, CancellationToken ct = default)
        => throw new CommunicationRuntimeException("OPC UA 不使用寄存器寻址");

    public Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, CancellationToken ct = default)
        => throw new CommunicationRuntimeException("OPC UA 不使用线圈寻址");

    public Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken ct = default)
        => throw new CommunicationRuntimeException("OPC UA 不使用线圈寻址");

    // ── 辅助 ──

    private uint NextRequestHandle() => ++_requestHandle;

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new CommunicationRuntimeException("未连接 OPC UA 服务器");
    }

    /// <summary>解析 opc.tcp://host:port 端点。</summary>
    public static (string Host, int Port) ParseEndpoint(string endpointUrl)
    {
        var s = endpointUrl;
        if (s.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
            s = s["opc.tcp://".Length..];

        var slash = s.IndexOf('/');
        if (slash >= 0) s = s[..slash];

        var colon = s.LastIndexOf(':');
        if (colon > 0 && int.TryParse(s[(colon + 1)..], out var port) && port is > 0 and <= 65535)
            return (s[..colon], port);

        return (s, 4840);
    }

    public void Dispose() => Disconnect();
}
