using HmiEditor.Device.Firmware;
using Xunit;

namespace HmiEditor.Device.Tests;

/// <summary>
/// <c>sys_partition.fex</c> 解析判据 —— 用来把"UI 产物能不能装进 UDISK"变成可算的判据。
/// 样本照抄 t113-mq_r 的真实分区表结构（含注释、<c>[mbr]</c>、<c>[partition_start]</c>、
/// <c>size = 0</c> 的剩余分区），不依赖本机 SDK 产物。
/// </summary>
public class FirmwarePartitionTableTests
{
    private const long Sector = 512;

    private const string Sample = """
        ;----------------------------------------------------------------------------
        ; 说明：脚本中的字符串不区分大小写
        ;----------------------------------------------------------------------------

        [mbr]
        size = 16384

        [partition_start]

        [partition]
            name         = boot-resource
            size         = 3686
            downloadfile = "boot-resource.fex"
            user_type    = 0x8000

        [partition]
            name         = env
            size         = 504

        [partition]
            name         = env-redund
            size         = 504

        [partition]
            name         = boot
            size         = 11446

        [partition]
            name         = rootfs
            size         = 162304

        [partition]
            name         = rootfs_data
            size         = 7734

        [partition]
            name         = UDISK
            user_type    = 0x8100
        """;

    /// <summary>总容量 128 MiB（T113 的 W25N01GV）。</summary>
    private const long TotalFlash = 128L * 1024 * 1024;

    [Fact]
    public void Parse_ComputesReservedFixedAndRemainder()
    {
        var table = FirmwarePartitionTable.Parse(Sample, TotalFlash);

        Assert.Equal(16384 * Sector, table.ReservedBytes);
        Assert.Equal((3686 + 504 + 504 + 11446 + 162304 + 7734) * Sector, table.FixedBytes);
        Assert.Equal(TotalFlash - table.ReservedBytes - table.FixedBytes, table.RemainderBytes);

        // 128 MiB − 8 MiB(mbr) − 90.9 MiB(固定分区) ≈ 29.1 MiB 留给 UDISK
        Assert.InRange(table.RemainderBytes, 29L * 1024 * 1024, 30L * 1024 * 1024);
    }

    [Fact]
    public void Parse_MarksTheZeroSizedPartitionAsRemainder()
    {
        var table = FirmwarePartitionTable.Parse(Sample, TotalFlash);

        var udisk = table.Find("UDISK");
        Assert.NotNull(udisk);
        Assert.True(udisk!.IsRemainder);
        Assert.Equal(table.RemainderBytes, udisk.SizeBytes);

        // 固定分区不是剩余分区，且大小来自自己的 size 行
        var rootfs = table.Find("rootfs");
        Assert.NotNull(rootfs);
        Assert.False(rootfs!.IsRemainder);
        Assert.Equal(162304 * Sector, rootfs.SizeBytes);

        Assert.Null(table.Find("no-such-partition"));
    }

    [Fact]
    public void Parse_ToleratesCommentsQuotesAndMissingFields()
    {
        var table = FirmwarePartitionTable.Parse(Sample, TotalFlash);

        // env-redund 没有 downloadfile / user_type，也照常入表
        Assert.Equal(6, table.Partitions.Count(p => !p.IsRemainder));
        Assert.Single(table.Partitions, p => p.IsRemainder);
    }

    [Fact]
    public void Parse_ClampsRemainderToZero_WhenTableOverflowsTheChip()
    {
        // 表比芯片还大 ⇒ 剩余为 0，绝不返回负数（否则"装得下"会被算成永远成立）
        var table = FirmwarePartitionTable.Parse(Sample, totalFlashBytes: 1024);

        Assert.Equal(0, table.RemainderBytes);
        Assert.Equal(0, table.Find("UDISK")!.SizeBytes);
    }
}
