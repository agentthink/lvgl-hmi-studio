using System.Text.RegularExpressions;

namespace HmiEditor.Device.Firmware;

/// <summary>分区表里的一项。<see cref="IsRemainder"/> = 分区表里 <c>size = 0</c>（吃剩余空间，如 UDISK）。</summary>
public sealed record FirmwarePartition(string Name, long SizeBytes, bool IsRemainder);

/// <summary>
/// Allwinner 分区表（<c>sys_partition.fex</c>）解析结果。
///
/// <para>为什么需要它：HMI 工程（<c>libhmi_ui.so</c> + 资源）落在 SPI NAND 的 <b>UDISK</b>
/// 分区上，而 UDISK 的大小 = 整片 Flash − mbr − 各固定分区。整片 Flash 是
/// <see cref="Hmi.Hardware.Contracts.ResourceConstraints.FlashSizeBytes"/>，
/// UDISK 剩余则要读分区表才知道 —— 否则"UI 装不装得下"只能靠猜。</para>
/// </summary>
public sealed record FirmwarePartitionTable(
    IReadOnlyList<FirmwarePartition> Partitions,
    long ReservedBytes,
    long FixedBytes,
    long RemainderBytes)
{
    /// <summary>解析分区表文本。<paramref name="totalFlashBytes"/> 用于算剩余分区大小。</summary>
    public static FirmwarePartitionTable Parse(string fexText, long totalFlashBytes)
    {
        const long sector = 512;
        var partitions = new List<FirmwarePartition>();
        long mbrSectors = 0, fixedSectors = 0;
        var remainderPartitions = new List<string>();

        var inMbr = false;
        var inPartition = false;
        string? name = null;
        long sectors = 0;
        var hasSize = false;

        void Flush()
        {
            if (!inPartition || string.IsNullOrEmpty(name)) return;
            if (!hasSize || sectors == 0)
            {
                // size = 0（或缺失）= 吃剩余空间
                remainderPartitions.Add(name!);
            }
            else
            {
                fixedSectors += sectors;
                partitions.Add(new FirmwarePartition(name!, sectors * sector, IsRemainder: false));
            }
            name = null;
            sectors = 0;
            hasSize = false;
        }

        foreach (var rawLine in fexText.Split('\n'))
        {
            // 分区表用 ';' 注释；值里不会出现分号
            var line = rawLine.Split(';')[0].Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith('['))
            {
                var section = line.Trim('[', ']').Trim().ToLowerInvariant();
                if (section == "partition")
                {
                    Flush();
                    inPartition = true;
                    inMbr = false;
                }
                else
                {
                    Flush();
                    inPartition = false;
                    inMbr = section == "mbr";
                }
                continue;
            }

            var match = Regex.Match(line, @"^([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+)$");
            if (!match.Success) continue;
            var key = match.Groups[1].Value.ToLowerInvariant();
            var value = match.Groups[2].Value.Trim();

            if (inMbr && key == "size" && long.TryParse(value, out var mbr))
            {
                mbrSectors = mbr;
            }
            else if (inPartition)
            {
                if (key == "name") name = value;
                else if (key == "size" && long.TryParse(value, out var size))
                {
                    sectors = size;
                    hasSize = true;
                }
            }
        }
        Flush();

        var reserved = mbrSectors * sector;
        var fixedBytes = fixedSectors * sector;
        var remainder = Math.Max(0, totalFlashBytes - reserved - fixedBytes);
        foreach (var remainderName in remainderPartitions)
            partitions.Add(new FirmwarePartition(remainderName, remainder, IsRemainder: true));

        return new FirmwarePartitionTable(partitions, reserved, fixedBytes, remainder);
    }

    /// <summary>按名字取分区（找不到返回 null）。</summary>
    public FirmwarePartition? Find(string name) =>
        Partitions.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
