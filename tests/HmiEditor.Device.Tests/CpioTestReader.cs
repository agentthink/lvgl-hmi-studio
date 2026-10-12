using System.Text;
using HmiEditor.Device.Packaging;
using Xunit;

namespace HmiEditor.Device.Tests;

/// <summary>测试用 CPIO crc 归档读取器（验证打包器输出结构）。</summary>
internal static class CpioTestReader
{
    public static List<(string Name, byte[] Data)> Read(byte[] archive)
    {
        var entries = new List<(string, byte[])>();
        using var stream = new MemoryStream(archive);
        var entryIndex = 0;
        while (true)
        {
            var header = ReadExact(stream, 110);
            entryIndex++;
            if (header.Length < 110)
                throw new EndOfStreamException("CPIO 头部不完整");

            var magic = Encoding.ASCII.GetString(header, 0, 6);
            if (magic != "070702")
                throw new InvalidOperationException(
                    $"条目 #{entryIndex} magic 非法: '{magic}' 原始={Convert.ToHexString(header)} 流偏移={stream.Position - 110}");

            var fileSize = Convert.ToInt64(Encoding.ASCII.GetString(header, 54, 8), 16);
            var nameSize = Convert.ToInt64(Encoding.ASCII.GetString(header, 94, 8), 16);

            var nameBytes = ReadExact(stream, (int)nameSize);
            var name = Encoding.UTF8.GetString(nameBytes, 0, nameBytes.Length - 1); // 去结尾 NUL
            if (name == "TRAILER!!!")
                break;

            // 文件名后 padding：数据起点按绝对偏移 4 对齐（SVR4 规范）
            var namePad = (4 - (110 + (int)nameSize) % 4) % 4;
            if (namePad > 0)
            {
                var skip = new byte[namePad];
                stream.Read(skip, 0, namePad);
            }

            var data = ReadAligned(stream, (int)fileSize);
            entries.Add((name, data));
        }
        return entries;
    }

    private static byte[] ReadExact(Stream stream, int count)
    {
        var buf = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = stream.Read(buf, read, count - read);
            if (n == 0) throw new EndOfStreamException();
            read += n;
        }
        return buf;
    }

    private static byte[] ReadAligned(Stream stream, int count)
    {
        var buf = ReadExact(stream, count);
        // 跳过 4 字节对齐 padding
        var pad = (4 - (count % 4)) % 4;
        if (pad > 0)
        {
            var skip = new byte[pad];
            stream.ReadExactly(skip, 0, pad);
        }
        return buf;
    }
}
