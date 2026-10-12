using System.Text;

namespace Hmi.Communication.Builtin.OpcUa;

/// <summary>OPC UA 二进制编码写入器（Part 6 基本类型编码）。</summary>
public sealed class OpcUaBinaryWriter
{
    private readonly MemoryStream _stream = new();

    public void WriteUInt32(uint value)
    {
        _stream.WriteByte((byte)(value & 0xFF));
        _stream.WriteByte((byte)((value >> 8) & 0xFF));
        _stream.WriteByte((byte)((value >> 16) & 0xFF));
        _stream.WriteByte((byte)((value >> 24) & 0xFF));
    }

    public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

    public void WriteUInt16(ushort value)
    {
        _stream.WriteByte((byte)(value & 0xFF));
        _stream.WriteByte((byte)((value >> 8) & 0xFF));
    }

    public void WriteUInt64(ulong value)
    {
        for (int i = 0; i < 8; i++)
            _stream.WriteByte((byte)((value >> (8 * i)) & 0xFF));
    }

    public void WriteInt64(long value) => WriteUInt64(unchecked((ulong)value));

    public void WriteBoolean(bool value) => _stream.WriteByte(value ? (byte)1 : (byte)0);

    public void WriteDouble(double value) => WriteInt64(BitConverter.DoubleToInt64Bits(value));

    public void WriteFloat(float value) => WriteUInt32(BitConverter.SingleToUInt32Bits(value));

    public void WriteByte(byte value) => _stream.WriteByte(value);

    public void WriteBytes(byte[] value) => _stream.Write(value, 0, value.Length);

    /// <summary>OPC UA String: Int32 长度(-1=null) + UTF-8。</summary>
    public void WriteString(string? value)
    {
        if (value == null)
        {
            WriteInt32(-1);
            return;
        }
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteInt32(bytes.Length);
        _stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>OPC UA ByteString: Int32 长度(-1=null) + 原始字节。</summary>
    public void WriteByteString(byte[]? value)
    {
        if (value == null)
        {
            WriteInt32(-1);
            return;
        }
        WriteInt32(value.Length);
        _stream.Write(value, 0, value.Length);
    }

    /// <summary>OPC UA DateTime: Int64 自 1601-01-01 的 100ns 计数；null 写 0。</summary>
    public void WriteDateTime(DateTime? value)
    {
        WriteInt64(value.HasValue
            ? value.Value.ToUniversalTime().Ticks - OpcUaBinaryReader.WindowsEpochTicks
            : 0);
    }

    /// <summary>OPC UA Guid: 16 字节（Data1/Data2/Data3 小端 + Data4 原始）。</summary>
    public void WriteGuid(Guid value)
    {
        var bytes = value.ToByteArray();
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void WriteNodeId(OpcUaNodeId? nodeId)
    {
        if (nodeId == null || nodeId.IsNull)
        {
            // Null NodeId = TwoByte 格式, identifier=0 → 0x00 0x00
            WriteByte(0x00);
            WriteByte(0x00);
            return;
        }

        switch (nodeId.IdentifierType)
        {
            case 0x00:
                // TwoByte: 值 1-255 且 ns=0
                if (nodeId.NamespaceIndex == 0 && nodeId.Numeric <= 255 && nodeId.Numeric >= 1)
                {
                    WriteByte(0x00);
                    WriteByte((byte)nodeId.Numeric);
                    return;
                }
                // FourByte: ns + 16 位值
                if (nodeId.Numeric <= ushort.MaxValue)
                {
                    WriteByte(0x01);
                    WriteUInt16(nodeId.NamespaceIndex);
                    WriteUInt16((ushort)nodeId.Numeric);
                    return;
                }
                // Numeric: ns + 32 位值
                WriteByte(0x02);
                WriteUInt16(nodeId.NamespaceIndex);
                WriteUInt32(nodeId.Numeric);
                return;

            case 0x01:
                WriteByte(0x03);
                WriteUInt16(nodeId.NamespaceIndex);
                WriteString(nodeId.StringIdentifier);
                return;

            case 0x02:
                WriteByte(0x04);
                WriteUInt16(nodeId.NamespaceIndex);
                WriteGuid(nodeId.GuidIdentifier);
                return;

            case 0x03:
                WriteByte(0x05);
                WriteUInt16(nodeId.NamespaceIndex);
                WriteByteString(nodeId.ByteStringIdentifier);
                return;
        }
    }

    public void WriteStatusCode(uint value) => WriteUInt32(value);

    /// <summary>
    /// 写 Variant（标量或非空数组；数组元素必须同类型且非 null）。
    /// </summary>
    public void WriteVariant(object? value, byte builtInType, bool isArray = false)
    {
        if (value == null && !isArray)
        {
            WriteByte(OpcUaBuiltInType.Null);
            return;
        }

        byte mask = builtInType;
        if (isArray) mask |= 0x80;
        WriteByte(mask);

        if (!isArray)
        {
            WriteVariantValue(value, builtInType);
            return;
        }

        var array = (System.Collections.IList)value!;
        WriteInt32(array.Count);
        foreach (var item in array)
            WriteVariantValue(item, builtInType);
    }

    private void WriteVariantValue(object? value, byte builtInType)
    {
        switch (builtInType)
        {
            case OpcUaBuiltInType.Boolean: WriteBoolean(value is bool b && b); break;
            case OpcUaBuiltInType.SByte: WriteInt32(Convert.ToSByte(value)); break;
            case OpcUaBuiltInType.Byte: WriteByte(Convert.ToByte(value)); break;
            case OpcUaBuiltInType.Int16: WriteInt32(Convert.ToInt16(value)); break;
            case OpcUaBuiltInType.UInt16: WriteUInt32(Convert.ToUInt16(value)); break;
            case OpcUaBuiltInType.Int32: WriteInt32(Convert.ToInt32(value)); break;
            case OpcUaBuiltInType.UInt32: WriteUInt32(Convert.ToUInt32(value)); break;
            case OpcUaBuiltInType.Int64: WriteInt64(Convert.ToInt64(value)); break;
            case OpcUaBuiltInType.UInt64: WriteUInt64(Convert.ToUInt64(value)); break;
            case OpcUaBuiltInType.Float: WriteFloat(Convert.ToSingle(value)); break;
            case OpcUaBuiltInType.Double: WriteDouble(Convert.ToDouble(value)); break;
            case OpcUaBuiltInType.String: WriteString(value as string); break;
            case OpcUaBuiltInType.ByteString: WriteByteString(value as byte[]); break;
            case OpcUaBuiltInType.DateTime:
                WriteDateTime(value is DateTime dt ? dt : null);
                break;
            default:
                throw new NotSupportedException($"Variant 类型不支持写入: {builtInType}");
        }
    }

    /// <summary>写 DataValue（encodingMask + value + statusCode + 时间戳）。</summary>
    public void WriteDataValue(OpcUaDataValue dv)
    {
        byte mask = 0;
        if (dv.Value != null) mask |= 0x01;
        if (dv.HasStatusCode) mask |= 0x02;
        if (dv.SourceTimestamp.HasValue) mask |= 0x04;
        WriteByte(mask);

        if (dv.Value != null)
            WriteVariant(dv.Value, dv.BuiltInType, dv.IsArray);
        if (dv.HasStatusCode)
            WriteStatusCode(dv.StatusCode);
        if (dv.SourceTimestamp.HasValue)
            WriteDateTime(dv.SourceTimestamp);
    }

    /// <summary>写 ExtensionObject：Null 扩展对象（TypeId=Null + 无 body）。</summary>
    public void WriteNullExtensionObject()
    {
        WriteNodeId(OpcUaNodeId.Null);
    }

    /// <summary>输出编码后的完整字节流。</summary>
    public byte[] ToArray() => _stream.ToArray();
}
