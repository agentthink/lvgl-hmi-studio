using Hmi.Communication.Contracts;

namespace Hmi.Communication.Builtin.OpcUa;

/// <summary>
/// OPC UA Read/Write 服务编解码（Part 4 5.10 / 5.11）。
/// 支持按节点 ID 批量读 / 写（属性 Value=13），轮询驱动模式。
/// </summary>
public static class OpcUaServiceCodec
{
    public static readonly uint GoodResult = 0x00000000;

    // ── Read ──

    /// <summary>构造 ReadRequest 服务体（不含消息级头）。</summary>
    public static byte[] BuildReadRequest(uint requestHandle, IReadOnlyList<OpcUaNodeId> nodes)
    {
        var w = new OpcUaBinaryWriter();
        OpcUaMessageLayer.WriteRequestHeader(w, requestHandle);
        w.WriteDouble(0);        // maxAge
        w.WriteUInt32(0);        // timestampsToReturn = Source
        w.WriteInt32(nodes.Count);
        foreach (var node in nodes)
        {
            w.WriteNodeId(node);
            w.WriteUInt32(OpcUaAttribute.Value);
            w.WriteString(null); // indexRange
            w.WriteNodeId(OpcUaNodeId.Null); // dataEncoding (QualifiedName 用 NodeId 表达不严谨，用空)
        }
        return w.ToArray();
    }

    /// <summary>解析 ReadResponse 服务体 → 每个节点的 DataValue 列表。</summary>
    public static List<OpcUaDataValue> ParseReadResponse(byte[] body, int expectedCount)
    {
        var r = new OpcUaBinaryReader(body);
        OpcUaMessageLayer.ReadResponseHeader(r, out var serviceResult);
        if (serviceResult != 0)
            throw new CommunicationFrameException($"Read 服务失败: 0x{serviceResult:X8}");

        // 布局: results[count] (StatusCode 数组) 之后才是 diagnosticInfos[count]
        var statuses = new uint[expectedCount];
        for (int i = 0; i < expectedCount; i++)
            statuses[i] = r.ReadUInt32();

        var result = new List<OpcUaDataValue>(expectedCount);
        for (int i = 0; i < expectedCount; i++)
        {
            var diagMask = r.ReadByte(); // diagnosticInfos[i]: 0=空诊断
            if (diagMask != 0)
                throw new CommunicationFrameException($"不支持非空 DiagnosticInfo: 0x{diagMask:X2}");

            if (statuses[i] != 0)
            {
                result.Add(new OpcUaDataValue { StatusCode = statuses[i], HasStatusCode = true });
                continue;
            }
            result.Add(r.ReadDataValue());
        }
        return result;
    }

    // ── Write ──

    /// <summary>构造 WriteRequest 服务体。value 按 targetType 编码。</summary>
    public static byte[] BuildWriteRequest(uint requestHandle,
        IReadOnlyList<(OpcUaNodeId Node, object? Value, byte BuiltInType, bool IsArray)> items)
    {
        var w = new OpcUaBinaryWriter();
        OpcUaMessageLayer.WriteRequestHeader(w, requestHandle);
        w.WriteInt32(items.Count);
        foreach (var (node, value, type, isArray) in items)
        {
            w.WriteNodeId(node);
            w.WriteUInt32(OpcUaAttribute.Value);
            w.WriteString(null); // indexRange
            w.WriteDataValue(new OpcUaDataValue
            {
                Value = value,
                BuiltInType = type,
                IsArray = isArray,
                SourceTimestamp = DateTime.UtcNow
            });
        }
        return w.ToArray();
    }

    /// <summary>解析 WriteResponse → 每个写节点的状态码。</summary>
    public static List<uint> ParseWriteResponse(byte[] body, int expectedCount)
    {
        var r = new OpcUaBinaryReader(body);
        OpcUaMessageLayer.ReadResponseHeader(r, out var serviceResult);
        if (serviceResult != 0)
            throw new CommunicationFrameException($"Write 服务失败: 0x{serviceResult:X8}");

        var result = new List<uint>(expectedCount);
        for (int i = 0; i < expectedCount; i++)
            result.Add(r.ReadUInt32()); // results[i]

        for (int i = 0; i < expectedCount; i++)
        {
            var diagMask = r.ReadByte(); // diagnosticInfos[i]
            if (diagMask != 0)
                throw new CommunicationFrameException($"不支持非空 DiagnosticInfo: 0x{diagMask:X2}");
        }
        return result;
    }

    /// <summary>
    /// 将 CLR 类型映射到 OPC UA BuiltInType。
    /// 返回 (BuiltInType, 是否数组)。
    /// </summary>
    public static (byte Type, bool IsArray) MapType(object? value)
    {
        if (value == null) return (OpcUaBuiltInType.Null, false);

        return value switch
        {
            bool => (OpcUaBuiltInType.Boolean, false),
            sbyte => (OpcUaBuiltInType.SByte, false),
            byte => (OpcUaBuiltInType.Byte, false),
            short => (OpcUaBuiltInType.Int16, false),
            ushort => (OpcUaBuiltInType.UInt16, false),
            int => (OpcUaBuiltInType.Int32, false),
            uint => (OpcUaBuiltInType.UInt32, false),
            long => (OpcUaBuiltInType.Int64, false),
            ulong => (OpcUaBuiltInType.UInt64, false),
            float => (OpcUaBuiltInType.Float, false),
            double => (OpcUaBuiltInType.Double, false),
            string => (OpcUaBuiltInType.String, false),
            byte[] => (OpcUaBuiltInType.ByteString, false),
            DateTime => (OpcUaBuiltInType.DateTime, false),
            bool[] => (OpcUaBuiltInType.Boolean, true),
            short[] => (OpcUaBuiltInType.Int16, true),
            ushort[] => (OpcUaBuiltInType.UInt16, true),
            int[] => (OpcUaBuiltInType.Int32, true),
            uint[] => (OpcUaBuiltInType.UInt32, true),
            float[] => (OpcUaBuiltInType.Float, true),
            double[] => (OpcUaBuiltInType.Double, true),
            string[] => (OpcUaBuiltInType.String, true),
            _ => throw new NotSupportedException($"不支持的值类型: {value.GetType().Name}")
        };
    }

    /// <summary>
    /// 将 DataValue 值转换为 double（供值源使用）。
    /// 数值类型直接转换; 布尔 → 0/1; 字符串 → 尝试解析。
    /// </summary>
    public static double? ToDouble(OpcUaDataValue dv)
    {
        if (dv.Value == null) return null;
        try
        {
            return dv.Value switch
            {
                bool b => b ? 1 : 0,
                sbyte v => v,
                byte v => v,
                short v => v,
                ushort v => v,
                int v => v,
                uint v => v,
                long v => v,
                ulong v => v,
                float v => v,
                double v => v,
                string s => double.TryParse(s, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>为写入构造一个值: double → 最合适的数值类型。</summary>
    public static object? FromDouble(double value, byte targetType)
    {
        return targetType switch
        {
            OpcUaBuiltInType.Boolean => value > 0.5,
            OpcUaBuiltInType.SByte => (sbyte)Math.Round(Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue)),
            OpcUaBuiltInType.Byte => (byte)Math.Round(Math.Clamp(value, byte.MinValue, byte.MaxValue)),
            OpcUaBuiltInType.Int16 => (short)Math.Round(Math.Clamp(value, short.MinValue, short.MaxValue)),
            OpcUaBuiltInType.UInt16 => (ushort)Math.Round(Math.Clamp(value, ushort.MinValue, ushort.MaxValue)),
            OpcUaBuiltInType.Int32 => (int)Math.Round(Math.Clamp(value, int.MinValue, int.MaxValue)),
            OpcUaBuiltInType.UInt32 => (uint)Math.Round(Math.Clamp(value, uint.MinValue, uint.MaxValue)),
            OpcUaBuiltInType.Float => (float)value,
            OpcUaBuiltInType.Double => value,
            OpcUaBuiltInType.String => value.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture),
            _ => value
        };
    }
}
