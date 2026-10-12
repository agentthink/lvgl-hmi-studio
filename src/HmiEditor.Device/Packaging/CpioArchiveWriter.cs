using System.Text;

namespace HmiEditor.Device.Packaging;

/// <summary>
/// CPIO "crc"（SVR4 with CRC, magic 070702）归档写入器 —— 与 GNU `cpio -H crc` 兼容，
/// 是 swupdate 要求的 .swu 内部归档格式。
/// 每个条目：110 字节头部 + 文件名（含结尾 NUL，4 字节对齐）+ 文件数据（4 字节对齐）。
/// </summary>
public sealed class CpioArchiveWriter : IDisposable
{
    private const int HeaderSize = 110;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("070702");

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private uint _ino;
    private bool _finished;

    public CpioArchiveWriter(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
    }

    /// <summary>追加一个普通文件条目。</summary>
    public void WriteEntry(string name, ReadOnlySpan<byte> data)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_finished) throw new InvalidOperationException("CPIO 归档已写入结束块（Finish），不能再追加条目");
        if (name.Length == 0) throw new ArgumentException("CPIO 条目名不能为空", nameof(name));

        var nameBytes = Encoding.UTF8.GetBytes(name);
        // swupdate 的 cpio 校验是"文件数据字节累加和"（cpio_utils.c: *checksum += buf[i]），
        // 不是标准 CRC32。使用累加和以保证 swupdate 校验通过。
        var checksum = ComputeChecksum(data);

        // 110 字节头部：6 magic + 13 个 8 位十六进制字段
        var header = new byte[HeaderSize];
        Magic.CopyTo(header, 0);
        WriteField(header, 6, _ino++);
        WriteField(header, 14, 0x81A4);   // S_IFREG (0100000) | 0644
        WriteField(header, 22, 0);        // uid
        WriteField(header, 30, 0);        // gid
        WriteField(header, 38, 1);        // nlink
        WriteField(header, 46, 0);        // mtime
        WriteField(header, 54, (uint)data.Length);
        WriteField(header, 62, 0);        // devmajor
        WriteField(header, 70, 0);        // devminor
        WriteField(header, 78, 0);        // rdevmajor
        WriteField(header, 86, 0);        // rdevminor
        WriteField(header, 94, (uint)(nameBytes.Length + 1));
        WriteField(header, 102, checksum);   // crc 格式：文件数据字节累加和

        _stream.Write(header);

        // 文件名紧接头部（SVR4 规范：文件名不对齐，文件数据才需要 4 字节对齐）
        var nameWithNul = new byte[nameBytes.Length + 1];
        nameBytes.CopyTo(nameWithNul, 0);
        _stream.Write(nameWithNul);

        // 文件名后 padding：使文件数据起点按归档绝对偏移 4 字节对齐
        var namePad = (4 - (HeaderSize + nameWithNul.Length) % 4) % 4;
        if (namePad > 0)
            _stream.Write(new byte[namePad]);

        // 文件数据 → 4 字节对齐
        WriteAligned(data);
    }

    /// <summary>写入 TRAILER 结束块，归档不可再追加。</summary>
    public void Finish()
    {
        if (_finished) return;
        _finished = true;

        var name = Encoding.UTF8.GetBytes("TRAILER!!!");
        var header = new byte[HeaderSize];
        Magic.CopyTo(header, 0);
        WriteField(header, 6, _ino++);
        WriteField(header, 14, 0x81A4);
        WriteField(header, 22, 0);
        WriteField(header, 30, 0);
        WriteField(header, 38, 1);
        WriteField(header, 46, 0);
        WriteField(header, 54, 0);        // filesize = 0
        WriteField(header, 62, 0);
        WriteField(header, 70, 0);
        WriteField(header, 78, 0);
        WriteField(header, 86, 0);
        WriteField(header, 94, (uint)(name.Length + 1));
        WriteField(header, 102, 0);

        _stream.Write(header);
        var nameWithNul = new byte[name.Length + 1];
        name.CopyTo(nameWithNul, 0);
        _stream.Write(nameWithNul);
        var namePad = (4 - (HeaderSize + nameWithNul.Length) % 4) % 4;
        if (namePad > 0)
            _stream.Write(new byte[namePad]);
    }

    private static void WriteField(byte[] header, int offset, uint value)
    {
        var text = value.ToString("x8");
        Encoding.ASCII.GetBytes(text).CopyTo(header, offset);
    }

    /// <summary>swupdate 兼容的字节累加和（uint32 自然溢出）。</summary>
    private static uint ComputeChecksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        foreach (var b in data)
            sum += b;
        return sum;
    }

    private void WriteAligned(ReadOnlySpan<byte> data)
    {
        _stream.Write(data);
        var pad = (4 - (data.Length % 4)) % 4;
        if (pad > 0)
            _stream.Write(new byte[pad]);
    }

    public void Dispose()
    {
        if (!_leaveOpen)
            _stream.Dispose();
    }
}
