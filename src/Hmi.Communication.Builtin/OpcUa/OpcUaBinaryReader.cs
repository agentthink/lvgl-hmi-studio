using System.Text;

namespace Hmi.Communication.Builtin.OpcUa;

/// <summary>OPC UA 二进制解码器（Part 6 基本类型解码）。</summary>
public sealed class OpcUaBinaryReader
{
    /// <summary>1601-01-01 (Windows FILETIME 基准) 的 .NET ticks 偏移。</summary>
    public const long WindowsEpochTicks = 504911232000000000;

    private readonly byte[] _buffer;
    private int _offset;

    public OpcUaBinaryReader(byte[] buffer, int offset = 0)
    {
        _buffer = buffer;
        _offset = offset;
    }

    public int Remaining => _buffer.Length - _offset;
    public int Position => _offset;

    public uint ReadUInt32()
    {
        Ensure(4);
        var v = (uint)(_buffer[_offset] | (_buffer[_offset + 1] << 8) |
                      (_buffer[_offset + 2] << 16) | (_buffer[_offset + 3] << 24));
        _offset += 4;
        return v;
    }

    public int ReadInt32() => unchecked((int)ReadUInt32());

    public ushort ReadUInt16()
    {
        Ensure(2);
        var v = (ushort)(_buffer[_offset] | (_buffer[_offset + 1] << 8));
        _offset += 2;
        return v;
    }

    public ulong ReadUInt64()
    {
        Ensure(8);
        ulong v = 0;
        for (int i = 0; i < 8; i++)
            v |= (ulong)_buffer[_offset + i] << (8 * i);
        _offset += 8;
        return v;
    }

    public long ReadInt64() => unchecked((long)ReadUInt64());

    public bool ReadBoolean()
    {
        Ensure(1);
        return _buffer[_offset++] != 0;
    }

    public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());

    public float ReadFloat() => BitConverter.UInt32BitsToSingle(ReadUInt32());

    public byte ReadByte()
    {
        Ensure(1);
        return _buffer[_offset++];
    }

    public byte[] ReadBytes(int count)
    {
        Ensure(count);
        var result = new byte[count];
        Array.Copy(_buffer, _offset, result, 0, count);
        _offset += count;
        return result;
    }

    public string? ReadString()
    {
        var len = ReadInt32();
        if (len < 0) return null;
        if (len == 0) return "";
        Ensure(len);
        var s = Encoding.UTF8.GetString(_buffer, _offset, len);
        _offset += len;
        return s;
    }

    public byte[]? ReadByteString()
    {
        var len = ReadInt32();
        if (len < 0) return null;
        return ReadBytes(len);
    }

    public DateTime? ReadDateTime()
    {
        var filetime = ReadInt64();
        if (filetime == 0) return null;
        try
        {
            return new DateTime(filetime + WindowsEpochTicks, DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }

    public Guid ReadGuid()
    {
        var bytes = ReadBytes(16);
        return new Guid(bytes);
    }

    public OpcUaNodeId ReadNodeId()
    {
        var encoding = ReadByte();
        switch (encoding)
        {
            case 0x00:
            {
                var v = ReadByte();
                return v == 0 ? OpcUaNodeId.Null : OpcUaNodeId.NumericNode(0, v);
            }
            case 0x01:
            {
                var ns = ReadUInt16();
                var v = ReadUInt16();
                return OpcUaNodeId.NumericNode(ns, v);
            }
            case 0x02:
            {
                var ns = ReadUInt16();
                var v = ReadUInt32();
                return OpcUaNodeId.NumericNode(ns, v);
            }
            case 0x03:
            {
                var ns = ReadUInt16();
                var s = ReadString();
                return OpcUaNodeId.StringNode(ns, s ?? "");
            }
            case 0x04:
            {
                var ns = ReadUInt16();
                var g = ReadGuid();
                return new OpcUaNodeId(ns, 0x02, g);
            }
            case 0x05:
            {
                var ns = ReadUInt16();
                var bs = ReadByteString();
                return new OpcUaNodeId(ns, 0x03, bs);
            }
            default:
                throw new FormatException($"未知 NodeId 编码: 0x{encoding:X2}");
        }
    }

    /// <summary>读 Variant → (值, BuiltInType, IsArray)。标量或一维数组。</summary>
    public (object? Value, byte BuiltInType, bool IsArray) ReadVariant()
    {
        var mask = ReadByte();
        bool isArray = (mask & 0x80) != 0;
        var type = (byte)(mask & 0x3F);

        if (isArray)
        {
            var count = ReadInt32();
            if (count <= 0) return (Array.Empty<object>(), type, true);
            if (count > 1_000_000) throw new FormatException("Variant 数组长度异常");

            switch (type)
            {
                case OpcUaBuiltInType.Boolean:
                {
                    var arr = new bool[count];
                    for (int i = 0; i < count; i++) arr[i] = ReadBoolean();
                    return (arr, type, true);
                }
                case OpcUaBuiltInType.Byte:
                {
                    var arr = new byte[count];
                    for (int i = 0; i < count; i++) arr[i] = ReadByte();
                    return (arr, type, true);
                }
                case OpcUaBuiltInType.Int16:
                {
                    var arr = new short[count];
                    for (int i = 0; i < count; i++) arr[i] = (short)ReadInt32();
                    return (arr, type, true);
                }
                case OpcUaBuiltInType.UInt16:
                {
                    var arr = new ushort[count];
                    for (int i = 0; i < count; i++) arr[i] = ReadUInt16();
                    return (arr, type, true);
                }
                case OpcUaBuiltInType.Int32:
                {
                    var arr = new int[count];
                    for (int i = 0; i < count; i++) arr[i] = ReadInt32();
                    return (arr, type, true);
                }
                case OpcUaBuiltInType.UInt32:
                {
                    var arr = new uint[count];
                    for (int i = 0; i < count; i++) arr[i] = ReadUInt32();
                    return (arr, type, true);
                }
                case OpcUaBuiltInType.Float:
                {
                    var arr = new float[count];
                    for (int i = 0; i < count; i++) arr[i] = ReadFloat();
                    return (arr, type, true);
                }
                case OpcUaBuiltInType.Double:
                {
                    var arr = new double[count];
                    for (int i = 0; i < count; i++) arr[i] = ReadDouble();
                    return (arr, type, true);
                }
                case OpcUaBuiltInType.String:
                {
                    var arr = new string?[count];
                    for (int i = 0; i < count; i++) arr[i] = ReadString();
                    return (arr, type, true);
                }
                default:
                    throw new NotSupportedException($"数组 Variant 类型不支持解码: {type}");
            }
        }

        switch (type)
        {
            case OpcUaBuiltInType.Null: return (null, type, false);
            case OpcUaBuiltInType.Boolean: return (ReadBoolean(), type, false);
            case OpcUaBuiltInType.SByte: return ((sbyte)ReadInt32(), type, false);
            case OpcUaBuiltInType.Byte: return (ReadByte(), type, false);
            case OpcUaBuiltInType.Int16: return ((short)ReadInt32(), type, false);
            case OpcUaBuiltInType.UInt16: return (ReadUInt16(), type, false);
            case OpcUaBuiltInType.Int32: return (ReadInt32(), type, false);
            case OpcUaBuiltInType.UInt32: return (ReadUInt32(), type, false);
            case OpcUaBuiltInType.Int64: return (ReadInt64(), type, false);
            case OpcUaBuiltInType.UInt64: return (ReadUInt64(), type, false);
            case OpcUaBuiltInType.Float: return (ReadFloat(), type, false);
            case OpcUaBuiltInType.Double: return (ReadDouble(), type, false);
            case OpcUaBuiltInType.String: return (ReadString(), type, false);
            case OpcUaBuiltInType.ByteString: return (ReadByteString(), type, false);
            case OpcUaBuiltInType.DateTime: return (ReadDateTime(), type, false);
            default:
                throw new NotSupportedException($"Variant 类型不支持解码: {type}");
        }
    }

    /// <summary>读 DataValue。</summary>
    public OpcUaDataValue ReadDataValue()
    {
        var dv = new OpcUaDataValue();
        var mask = ReadByte();

        if ((mask & 0x01) != 0)
        {
            var (value, type, isArray) = ReadVariant();
            dv.Value = value;
            dv.BuiltInType = type;
            dv.IsArray = isArray;
        }
        if ((mask & 0x02) != 0)
        {
            dv.StatusCode = ReadUInt32();
            dv.HasStatusCode = true;
        }
        if ((mask & 0x04) != 0)
            dv.SourceTimestamp = ReadDateTime();
        if ((mask & 0x08) != 0)
            ReadUInt16(); // sourcePicoseconds
        if ((mask & 0x10) != 0)
            ReadDateTime(); // serverTimestamp
        if ((mask & 0x20) != 0)
            ReadUInt16(); // serverPicoseconds
        return dv;
    }

    private void Ensure(int count)
    {
        // ⚠ 不能写 `_offset + count > Length`：`count` 来自**服务端**给的长度，
        // 接近 int.MaxValue 时加法会溢出成负数 ⇒ 检查通过 ⇒ 后面的读取越界/巨量分配
        // （阶段 121 修复）。用减法 + 负数判据，两端都封死。
        if (count < 0 || count > _buffer.Length - _offset)
            throw new FormatException($"OPC UA 缓冲区不足: 需要 {count} 字节, 剩余 {Remaining}");
    }
}
