using System.Globalization;
using System.Text;

namespace Hmi.Communication.Builtin.OpcUa;

/// <summary>OPC UA BuiltInType 枚举值（Part 6 Table 8）。</summary>
public static class OpcUaBuiltInType
{
    public const byte Null = 0;
    public const byte Boolean = 1;
    public const byte SByte = 2;
    public const byte Byte = 3;
    public const byte Int16 = 4;
    public const byte UInt16 = 5;
    public const byte Int32 = 6;
    public const byte UInt32 = 7;
    public const byte Int64 = 8;
    public const byte UInt64 = 9;
    public const byte Float = 10;
    public const byte Double = 11;
    public const byte String = 12;
    public const byte DateTime = 13;
    public const byte Guid = 14;
    public const byte ByteString = 15;
    public const byte XmlElement = 16;
    public const byte NodeId = 17;
    public const byte ExpandedNodeId = 18;
    public const byte StatusCode = 19;
    public const byte QualifiedName = 20;
    public const byte LocalizedText = 21;
    public const byte ExtensionObject = 22;
    public const byte DataValue = 23;
    public const byte Variant = 24;
    public const byte DiagnosticInfo = 25;
}

/// <summary>OPC UA 常用属性 ID（Part 4 Table 17）。</summary>
public static class OpcUaAttribute
{
    public const uint Value = 13;
}

/// <summary>
/// OPC UA 节点 ID —— 支持四种编码形式，并按规范生成二进制表示。
/// 解析支持: "ns=2;s=name" / "ns=2;i=100" / "ns=2;g=guid" / "ns=2;b=base64"。
/// 省略 ns 时默认 0。
/// </summary>
public sealed class OpcUaNodeId
{
    public ushort NamespaceIndex { get; }
    public byte IdentifierType { get; }
    public uint Numeric { get; }
    public string? StringIdentifier { get; }
    public byte[]? ByteStringIdentifier { get; }
    public Guid GuidIdentifier { get; }

    private OpcUaNodeId(ushort ns, byte idType,
        uint numeric = 0, string? str = null, Guid guid = default, byte[]? bs = null)
    {
        NamespaceIndex = ns;
        IdentifierType = idType;
        Numeric = numeric;
        StringIdentifier = str;
        GuidIdentifier = guid;
        ByteStringIdentifier = bs;
    }

    // 供同程序集编解码器构造（Guid/ByteString 标识）
    internal OpcUaNodeId(ushort ns, byte idType, Guid guid)
    {
        NamespaceIndex = ns;
        IdentifierType = idType;
        GuidIdentifier = guid;
    }

    internal OpcUaNodeId(ushort ns, byte idType, byte[]? bs)
    {
        NamespaceIndex = ns;
        IdentifierType = idType;
        ByteStringIdentifier = bs;
    }

    public static OpcUaNodeId NumericNode(ushort ns, uint value) =>
        new(ns, 0x00, numeric: value);

    public static OpcUaNodeId StringNode(ushort ns, string value) =>
        new(ns, 0x01, str: value);

    /// <summary>空 NodeId（用于 AuthenticationToken 等）。</summary>
    public static OpcUaNodeId Null => new(0, 0x00);

    public bool IsNull => IdentifierType == 0x00 && NamespaceIndex == 0 && Numeric == 0;

    /// <summary>
    /// 解析标准 OPC UA 节点 ID 字符串。
    /// 成功返回节点；失败返回 null。
    /// </summary>
    public static OpcUaNodeId? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();

        ushort ns = 0;
        string idPart = text;

        // 支持 "ns=X;" 前缀
        var semi = text.IndexOf(';');
        if (semi > 0 && text.StartsWith("ns=", StringComparison.OrdinalIgnoreCase))
        {
            if (!ushort.TryParse(text.AsSpan(3, semi - 3), NumberStyles.None,
                    CultureInfo.InvariantCulture, out ns))
                return null;
            idPart = text[(semi + 1)..];
        }

        // 数字形式（无类型前缀）
        if (uint.TryParse(idPart, NumberStyles.None, CultureInfo.InvariantCulture, out var pureNum))
            return NumericNode(ns, pureNum);

        if (idPart.Length >= 2 && idPart[1] == '=')
        {
            switch (char.ToLowerInvariant(idPart[0]))
            {
                case 'i':
                    if (uint.TryParse(idPart.AsSpan(2), NumberStyles.None,
                            CultureInfo.InvariantCulture, out var num))
                        return NumericNode(ns, num);
                    return null;
                case 's':
                    return StringNode(ns, idPart[2..]);
                case 'g':
                    if (Guid.TryParse(idPart[2..], out var guid))
                        return new OpcUaNodeId(ns, 0x02, guid: guid);
                    return null;
                case 'b':
                    try
                    {
                        return new OpcUaNodeId(ns, 0x03,
                            bs: Convert.FromBase64String(idPart[2..]));
                    }
                    catch
                    {
                        return null;
                    }
            }
        }

        // 兜底: 按字符串节点处理
        return StringNode(ns, idPart);
    }

    /// <summary>规范字符串表示（用于展示与地址往返）。</summary>
    public override string ToString()
    {
        var nsPart = NamespaceIndex == 0 ? "" : $"ns={NamespaceIndex};";
        return IdentifierType switch
        {
            0x00 => $"{nsPart}i={Numeric}",
            0x01 => $"{nsPart}s={StringIdentifier}",
            0x02 => $"{nsPart}g={GuidIdentifier}",
            0x03 => $"{nsPart}b={Convert.ToBase64String(ByteStringIdentifier!)}",
            _ => $"{nsPart}i={Numeric}"
        };
    }
}

/// <summary>OPC UA DataValue —— 值 + 状态码 + 可选时间戳。</summary>
public sealed class OpcUaDataValue
{
    public object? Value { get; set; }
    public byte BuiltInType { get; set; } = OpcUaBuiltInType.Null;
    public uint StatusCode { get; set; }
    public bool HasStatusCode { get; set; }
    public DateTime? SourceTimestamp { get; set; }
    public bool IsArray { get; set; }

    /// <summary>状态码是否表示成功（低 30 位语义）。</summary>
    public bool IsGood => (StatusCode & 0x40000000) == 0; // 最高两位 00=Good, 01=Uncertain, 11=Bad
}
