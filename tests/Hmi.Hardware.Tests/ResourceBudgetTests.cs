using System.IO;
using System.Text.RegularExpressions;
using Hmi.Hardware.Builtin;
using Hmi.Hardware.Contracts;
using Xunit;

namespace Hmi.Hardware.Tests;

/// <summary>
/// **资源预算判据**（登记表 §4 第 29 项"资源预算校准"）。
///
/// <para>这项登记的原文是"<c>ResourceConstraints</c> 只有声明值，无『实测 vs 预算』判据"。
/// 本类钉两件事：</para>
/// <list type="number">
///   <item><b>声明值必须与它的硬件证据对得上</b> —— T113 的 Flash 预算曾写成 16 MiB，
///         而板上是 W25N01GV（1 Gbit = 128 MiB）、真实整包 54.8 MiB；
///         这条用原理图 BOM 与板级 README 的文本把它锁住（错了立刻红）；</item>
///   <item><b>判读规则本身</b> —— <see cref="ResourceBudget"/> 的阈值/未知预算语义
///         （仪表盘与实测工具共用同一处）。</item>
/// </list>
/// <para>真实体积读数（整包 vs 预算、UDISK 剩余 vs UI 产物）由
/// <c>tools/BudgetCheck</c> 在存在 SDK 产物的机器上跑出，见
/// <c>docs/2026-09-18-resource-budget-measurement.md</c>。</para>
/// </summary>
public class ResourceBudgetTests
{
    private static string RepoFile(params string[] parts) =>
        Path.Combine(new[] { SourceScanner.RepoRoot }.Concat(parts).ToArray());

    [Fact]
    public void T113DeclaredFlash_MatchesTheBoardBomAndReadme()
    {
        var declared = new AllwinnerT113Target().Resources.FlashSizeBytes;

        // ① 原理图 BOM：U8 = W25N01GVZEIG（1 Gbit SPI NAND）
        var bom = File.ReadAllText(RepoFile("hardware", "hmi-control-board-t113", "lceda-import", "hmi-board.csv"));
        Assert.Contains("W25N01GVZEIG", bom, StringComparison.Ordinal);

        // ② 板级 README 的容量文本：抓住 "SPI NAND <N>MB" 的那个 N，与常量比
        var readme = File.ReadAllText(RepoFile("hardware", "hmi-control-board-t113", "README.md"));
        var match = Regex.Match(readme, @"SPI\s+NAND\s+(\d+)\s*MB", RegexOptions.IgnoreCase);
        Assert.True(match.Success, "板级 README 里找不到 'SPI NAND <N>MB' 这条容量声明");
        Assert.Equal(declared / 1024 / 1024, long.Parse(match.Groups[1].Value));

        // ③ 固件侧 README 不许再抄一个别的数（它曾经也写着 16MB）
        var firmwareReadme = File.ReadAllText(RepoFile("firmware", "t113", "README.md"));
        var fwMatch = Regex.Match(firmwareReadme, @"(\d+)\s*MB\s+SPI\s+NAND", RegexOptions.IgnoreCase);
        Assert.True(fwMatch.Success, "firmware/t113/README.md 的存储行不再是 '<N>MB SPI NAND' 形状");
        Assert.Equal(declared / 1024 / 1024, long.Parse(fwMatch.Groups[1].Value));

        // 128 MiB 这个具体值也钉一下：上面三条都改错成同一个错值时仍能红
        Assert.Equal(128L * 1024 * 1024, declared);
    }

    [Fact]
    public void UndeclaredTargetBudget_IsUnknown_NotPassing()
    {
        var reading = ResourceBudget.Evaluate("flash", usedBytes: 4096, budgetBytes: 0);

        // 缺依据就不下结论：既不是"通过"也不是"超预算"
        Assert.Equal(BudgetVerdict.Unknown, reading.Verdict);
        Assert.Equal(0, reading.Percent);
        Assert.Equal(0, reading.FreeBytes);
    }

    [Theory]
    [InlineData(0, BudgetVerdict.Ok)]
    [InlineData(699, BudgetVerdict.Ok)]
    [InlineData(700, BudgetVerdict.Elevated)]      // 与仪表盘黄色阈值同源
    [InlineData(899, BudgetVerdict.Elevated)]
    [InlineData(900, BudgetVerdict.NearLimit)]     // 与仪表盘红色阈值同源
    [InlineData(1000, BudgetVerdict.NearLimit)]    // 恰好用满：装得下
    [InlineData(1001, BudgetVerdict.Over)]         // 超出一字节就是装不下
    public void Evaluate_UsesTheSharedThresholds(long used, BudgetVerdict expected)
    {
        var reading = ResourceBudget.Evaluate("axis", used, budgetBytes: 1000);

        Assert.Equal(expected, reading.Verdict);
        Assert.Equal(used / 1000.0 * 100.0, reading.Percent, precision: 6);
    }

    [Fact]
    public void FreeBytes_IsBudgetMinusUsed_AndNeverNegative()
    {
        Assert.Equal(400, ResourceBudget.Evaluate("a", 600, 1000).FreeBytes);
        Assert.Equal(0, ResourceBudget.Evaluate("a", 1500, 1000).FreeBytes);
    }
}
