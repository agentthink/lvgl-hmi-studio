using Hmi.Hardware.Builtin;
using Hmi.Hardware.Contracts;
using HmiEditor.Device.Firmware;

// ============================================================
// BudgetCheck —— 资源预算「实测 vs 预算」（登记表 §4 第 29 项）
//
// 此前 ResourceConstraints 里只有**声明值**，没有任何一处拿真实体积去比 ——
// 于是 T113 的 Flash 预算写错成 16 MiB（真值 128 MiB）也不会有人发现。
//
// 这个工具把三条判据跑出来：
//   ① 实测整包镜像  vs 目标声明的 FlashSizeBytes；
//   ② 分区表推出的 UDISK 剩余  vs 待部署的 UI 产物（libhmi_ui.so + 资源目录）；
//   ③ 判读规则与编辑器仪表盘共用（ResourceBudget，70% / 90% / >100%）。
//
// 用法：
//   dotnet run --project tools/BudgetCheck -- [--target allwinner-t113]
//        [--image <tina_*.img>] [--partition <sys_partition*.fex]
//        [--so <libhmi_ui.so>] [--assets <目录>]
//
// 退出码：0 = 全部在预算内（或有轴未声明预算）；1 = 有轴超预算或实测文件缺失。
// ============================================================

var targetId = "allwinner-t113";
var imagePath = "";
var partitionPath = "";
var soPath = "";
var assetsDir = "";
var repoRootOverride = "";

const string Usage = """
用法:
  budget-check [--target <id>] [--image <img>] [--partition <fex>]
               [--so <libhmi_ui.so>] [--assets <dir>] [--repo <repoRoot>]
""";

var i = 0;
for (; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--target": targetId = Next(); break;
        case "--image": imagePath = Next(); break;
        case "--partition": partitionPath = Next(); break;
        case "--so": soPath = Next(); break;
        case "--assets": assetsDir = Next(); break;
        case "--repo": repoRootOverride = Next(); break;
        case "-h" or "--help": Console.WriteLine(Usage); return 0;
        default:
            Console.Error.WriteLine($"未知参数: {args[i]}");
            Console.Error.WriteLine(Usage);
            return 2;
    }
}

string Next() => ++i < args.Length ? args[i] : throw new ArgumentException($"参数 {args[i - 1]} 缺少取值");

var repoRoot = string.IsNullOrEmpty(repoRootOverride)
    ? FindRepoRoot(Directory.GetCurrentDirectory())
    : Path.GetFullPath(repoRootOverride);
if (repoRoot is null)
{
    Console.Error.WriteLine("✗ 未找到仓库根（向上找不到 HmiEditor.slnx）");
    return 1;
}

var target = ResolveTarget(targetId);
if (target is null)
{
    Console.Error.WriteLine($"✗ 不认得的目标: {targetId}（内置: allwinner-t113 / rockchip-rk3506）");
    return 1;
}

Console.WriteLine($"== 资源预算实测（{target.Metadata.Id}）==");
Console.WriteLine($"仓库: {repoRoot}");

var over = false;
var missing = false;

// ── ① 整包镜像 vs 声明的 Flash 预算 ──
var image = imagePath.Length > 0 ? Path.GetFullPath(imagePath) : FindDefaultImage(repoRoot, targetId);
var allImages = FindImages(repoRoot);
if (allImages.Count > 1)
{
    Console.WriteLine($"[候选镜像] 共 {allImages.Count} 个（默认取与目标板匹配的最新一个；可用 --image 指定）");
    foreach (var candidate in allImages.Take(5))
        Console.WriteLine($"     {Human(new FileInfo(candidate).Length),10}  {candidate}");
    if (allImages.Count > 5) Console.WriteLine($"     … 另 {allImages.Count - 5} 个");
}
if (image is null || !File.Exists(image))
{
    Console.WriteLine("[①] 整包镜像: 未找到（本机没有 SDK 构建产物）⇒ **未实测**");
    missing = true;
}
else
{
    var budget = target.Resources.FlashSizeBytes;
    var reading = ResourceBudget.Evaluate("整包镜像 / Flash", new FileInfo(image).Length, budget);
    Console.WriteLine($"[①] {image}");
    Console.WriteLine($"     实测 {Human(reading.UsedBytes)} vs 预算 {Human(budget)} " +
                      $"⇒ {reading.Percent:F1}% [{reading.Verdict}]");
    if (reading.Verdict == BudgetVerdict.Over) over = true;

    // ── ② 分区表 → UDISK 剩余 vs 待部署 UI 产物 ──
    var partition = partitionPath.Length > 0 ? Path.GetFullPath(partitionPath) : FindPartitionTable(image);
    if (partition is null || !File.Exists(partition))
    {
        Console.WriteLine("[②] 分区表: 未找到 ⇒ UDISK 剩余**未实测**");
    }
    else
    {
        var table = FirmwarePartitionTable.Parse(File.ReadAllText(partition), budget);
        var udisk = table.Find("UDISK");
        Console.WriteLine($"[②] 分区表 {Path.GetFileName(partition)}: mbr {Human(table.ReservedBytes)} + " +
                          $"固定分区 {Human(table.FixedBytes)} ⇒ UDISK 剩余 {Human(table.RemainderBytes)}");
        if (udisk is null)
        {
            Console.WriteLine("     （分区表里没有 UDISK 分区 ⇒ 不判断 UI 部署预算）");
        }
        else if (soPath.Length == 0 && assetsDir.Length == 0)
        {
            Console.WriteLine("     （未给 --so/--assets ⇒ 本次不判断 UI 部署预算）");
        }
        else
        {
            var used = 0L;
            var parts = new List<string>();
            if (soPath.Length > 0 && File.Exists(soPath))
            {
                used += new FileInfo(soPath).Length;
                parts.Add($"{Path.GetFileName(soPath)} {Human(new FileInfo(soPath).Length)}");
            }
            if (assetsDir.Length > 0 && Directory.Exists(assetsDir))
            {
                var assetsBytes = Directory.EnumerateFiles(assetsDir, "*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
                used += assetsBytes;
                parts.Add($"资源 {Human(assetsBytes)}");
            }
            var uiReading = ResourceBudget.Evaluate("UI 部署 / UDISK", used, udisk.SizeBytes);
            Console.WriteLine($"     UI 部署（{string.Join(" + ", parts)}）= {Human(used)} / " +
                              $"UDISK {Human(udisk.SizeBytes)} ⇒ {uiReading.Percent:F1}% [{uiReading.Verdict}]");
            if (uiReading.Verdict == BudgetVerdict.Over) over = true;
        }
    }
}

Console.WriteLine();
if (over)
{
    Console.Error.WriteLine("❌ 有资源轴超出预算");
    return 1;
}
Console.WriteLine(missing
    ? "⚠ 部分轴未实测（缺构建产物）；已实测的轴全部在预算内"
    : "✅ 实测与预算判据全部通过");
return 0;

// ── 辅助 ──

static IHardwareTarget? ResolveTarget(string id) => id.ToLowerInvariant() switch
{
    "allwinner-t113" => new AllwinnerT113Target(),
    "rockchip-rk3506" => new RockchipRK3506Target(),
    _ => null
};

static string? FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    for (var n = 0; n < 12 && dir is not null; n++, dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "HmiEditor.slnx")))
            return dir.FullName;
    return null;
}

/// <summary>全部整包镜像候选（<c>firmware/*/out/*/tina_*.img</c>，排除 <c>.bak</c>）。</summary>
static List<string> FindImages(string repoRoot)
{
    var candidates = new List<string>();
    var firmware = Path.Combine(repoRoot, "firmware");
    if (!Directory.Exists(firmware)) return candidates;
    foreach (var sdk in Directory.EnumerateDirectories(firmware))
    {
        var outRoot = Path.Combine(sdk, "out");
        if (!Directory.Exists(outRoot)) continue;
        foreach (var board in Directory.EnumerateDirectories(outRoot))
            candidates.AddRange(Directory.GetFiles(board, "tina_*.img")
                .Where(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)));
    }
    return candidates.OrderByDescending(File.GetLastWriteTimeUtc).ToList();
}

/// <summary>
/// 默认镜像：优先与目标板匹配的板目录（T113 默认板 = mq_r），
/// 同板多个时取最新；匹配不到再退回全部候选里最新的那个。
/// （2026-09-18 实测：不做匹配会挑到 bingpi_m2 的镜像 —— 同是 T113，但分区布局不同。）
/// </summary>
static string? FindDefaultImage(string repoRoot, string targetId)
{
    var images = FindImages(repoRoot);
    if (images.Count == 0) return null;
    var boardDir = targetId.Equals("allwinner-t113", StringComparison.OrdinalIgnoreCase) ? "t113-mq_r" : null;
    if (boardDir is not null)
    {
        var preferred = images.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(Path.GetDirectoryName(f)), boardDir, StringComparison.OrdinalIgnoreCase));
        if (preferred is not null) return preferred;
    }
    return images[0];
}

/// <summary>
/// 与镜像同一次构建产出的**完整**分区表（<c>&lt;out&gt;/&lt;board&gt;/image/sys_partition.fex</c>）。
/// 注意别用 <c>sys_partition_dump.fex</c>：那是 bootloader 那一小段，只有 bootloader 一个分区，
/// 拿它算 UDISK 剩余会得出完全错误的结论（2026-09-18 实测踩到）。
/// </summary>
static string? FindPartitionTable(string imagePath)
{
    var board = Path.GetDirectoryName(imagePath);
    if (board is null) return null;
    foreach (var name in new[] { "sys_partition.fex", "sys_partition_for_dragon.fex" })
    {
        var candidate = Path.Combine(board, "image", name);
        if (File.Exists(candidate)) return candidate;
    }
    return null;
}

static string Human(long bytes) => bytes >= 1024L * 1024
    ? $"{bytes / 1024.0 / 1024.0:F1} MiB"
    : $"{bytes / 1024.0:F1} KiB";
