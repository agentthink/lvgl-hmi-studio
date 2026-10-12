using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **登记册编号口径守卫**（2026-10-01，阶段 246，闭环 `#100`）—— 盯住一次真实的账本病：
/// 早期编号是**按节独立**的 ⇒ `#1`–`#21` 与 `#27`–`#33` 各有 2~3 个互不相同的含义，
/// 于是"挑一批 `#1`–`#25`"这种话**无法唯一定位**（实测 28 个重复 id）。
///
/// <para><b>口径（用户拍板选项①，阶段 246 执行）</b>：§1 / §2 / §3 / §5 四张表**与本表号冲突**的条目
/// 一律带**节前缀**（`§1-#15` / `§2-#9` / `§3-#9` / `§5-#27` …）；不冲突的号（`§3` 的 `87`、
/// `§5` 的 `40`）与 **§4 的全局号**（`#15`…`#101`）保持裸写 ⇒ 文中**裸写 `#X` 一律指 §4**。</para>
///
/// <para><b>三道</b>：① **全册条目号互不相同**（这条直接对应当年的 28 个重复 id）；
/// ② **§4 的行号必须是裸号**；③ §1/§2/§3/§5 的行号若带前缀，必须是**本表**前缀；
/// 若不带前缀，必须是"§4 里没有的号"（否则与 §4 重号）。另加**面自检**：五张表都要找到、
/// 且各自行数 ≥5（解析失效就红，防"扫不到 ⇒ 恒绿"）。</para>
/// </summary>
public class RegisterNumberingGuardTests
{
    private const string RegisterPath = "docs/2026-09-17-open-items-register.md";

    private static readonly (string Section, string Title)[] Tables =
    {
        ("§1", "## 1. 已闭环"),
        ("§2", "## 2. 仍开着"),
        ("§3", "## 3. 仍开着"),
        ("§4", "## 4. 仍开着"),
        ("§5", "## 5. 仍开着"),
    };

    private static readonly Regex PrefixedId = new(@"^§(\d)-#(\d+[a-z]?)$", RegexOptions.Compiled);
    private static readonly Regex BareId = new(@"^\d+[a-z]?$", RegexOptions.Compiled);

    private sealed record Row(string Section, int Line, string Id);

    private static List<Row> Rows()
    {
        var path = Path.Combine(SourceScanner.RepoRoot, RegisterPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"登记册不存在：{RegisterPath}");
        var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');

        var rows = new List<Row>();
        foreach (var (section, title) in Tables)
        {
            var start = Array.FindIndex(lines, l => l.StartsWith(title, StringComparison.Ordinal));
            Assert.True(start >= 0, $"登记册里找不到表标题「{title}」—— 分节结构变了，本判据会误判");

            var end = lines.Length;
            for (var i = start + 1; i < lines.Length; i++)
                if (lines[i].StartsWith("## ", StringComparison.Ordinal)) { end = i; break; }

            for (var i = start; i < end; i++)
            {
                var m = Regex.Match(lines[i], @"^\|\s*([^|\s]+)\s*\|");
                if (!m.Success) continue;
                var cell = m.Groups[1].Value;
                if (cell == "#" || cell.StartsWith("---", StringComparison.Ordinal)) continue;   // 表头 / 分隔行
                if (!PrefixedId.IsMatch(cell) && !BareId.IsMatch(cell)) continue;                // 不是条目号
                rows.Add(new Row(section, i + 1, cell));
            }
        }

        return rows;
    }

    [Fact]
    public void Face_CoversAllFiveTables()
    {
        var rows = Rows();

        foreach (var (section, _) in Tables)
        {
            var n = rows.Count(r => r.Section == section);
            Assert.True(n >= 5, $"{section} 只解析出 {n} 行 —— 面塌了（表标题/行格式变了），本判据会退化成永远通过");
        }

        Assert.True(rows.Count >= 150, $"全册只解析出 {rows.Count} 行条目号（阶段 246 实测 165）");
    }

    /// <summary>① 全册条目号**互不相同** —— 当年那 28 个重复 id 的机器版。</summary>
    [Fact]
    public void ItemIds_AreGloballyUnique()
    {
        var rows = Rows();
        var dup = rows.GroupBy(r => r.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}（{string.Join(", ", g.Select(r => $"{r.Section} 第 {r.Line} 行"))}）")
            .ToArray();

        Assert.True(dup.Length == 0,
            "登记册里有重复条目号（跨节或同节）—— 早期编号按节独立正是这么坏的（登记 `#100`）：\n  "
            + string.Join("\n  ", dup));
    }

    /// <summary>② §4 保持全局裸号；③ 其余四节：带前缀必须是本表前缀，裸号必须不与 §4 重号。</summary>
    [Fact]
    public void SectionPrefixes_FollowTheChosenConvention()
    {
        var rows = Rows();
        var section4 = rows.Where(r => r.Section == "§4").Select(r => r.Id)
            .ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var row in rows)
        {
            var prefixed = PrefixedId.Match(row.Id);
            if (row.Section == "§4")
            {
                if (prefixed.Success)
                    problems.Add($"§4 第 {row.Line} 行用了节前缀「{row.Id}」—— §4 是全局裸号表");
                continue;
            }

            if (prefixed.Success)
            {
                if ("§" + prefixed.Groups[1].Value != row.Section)
                    problems.Add($"{row.Section} 第 {row.Line} 行的前缀是 §{prefixed.Groups[1].Value}（必须是本表前缀）");
                continue;
            }

            // 裸号：只有"§4 里没有这个号"才允许（当前只有 §3 的 87 / §5 的 40 属此档）
            if (section4.Contains(row.Id))
                problems.Add($"{row.Section} 第 {row.Line} 行是裸号「{row.Id}」，而 §4 也有「{row.Id}」⇒ 又变成两义（要么加节前缀，要么换号）");
        }

        Assert.True(problems.Count == 0,
            "登记册编号口径不符（阶段 246 定的口径：§1/§2/§3/§5 与本表冲突的号带节前缀、§4 保持裸号）：\n  "
            + string.Join("\n  ", problems));
    }

    /// <summary>
    /// ④ 每张表的条目行**列数必须与表头一致**（转义竖线 <c>\|</c> 不算）——
    /// 盯住 §150 量到的那一类结构病：代码段/文案里一个**裸竖线**就把状态格切开
    /// （`#61j`/`#89` 曾如此；阶段 246 写 `#100` 时又亲手复现过一次，被本判据当场抓住）。
    /// </summary>
    [Fact]
    public void TableRows_MatchTheirHeaderCellCount()
    {
        var path = Path.Combine(SourceScanner.RepoRoot, RegisterPath.Replace('/', Path.DirectorySeparatorChar));
        var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
        var problems = new List<string>();

        static int Cells(string line) =>
            line.Replace("\\|", "\u0001").Split('|').Length;

        foreach (var (section, title) in Tables)
        {
            var start = Array.FindIndex(lines, l => l.StartsWith(title, StringComparison.Ordinal));
            Assert.True(start >= 0, $"登记册里找不到表标题「{title}」");

            var end = lines.Length;
            for (var i = start + 1; i < lines.Length; i++)
                if (lines[i].StartsWith("## ", StringComparison.Ordinal)) { end = i; break; }

            var header = -1;
            for (var i = start; i < end; i++)
                if (lines[i].StartsWith("| # |", StringComparison.Ordinal)) { header = i; break; }
            Assert.True(header >= 0, $"{section} 找不到表头行（`| # |`）—— 表被改坏了");

            var expected = Cells(lines[header]);
            for (var i = header + 1; i < end; i++)
            {
                if (!lines[i].StartsWith("|", StringComparison.Ordinal)) continue;
                if (lines[i].StartsWith("|---", StringComparison.Ordinal)) continue;
                var actual = Cells(lines[i]);
                if (actual != expected)
                    problems.Add($"{section} 第 {i + 1} 行有 {actual - 1} 格，表头是 {expected - 1} 格（裸竖线会把格子切开——要用 `\\|`）");
            }
        }

        Assert.True(problems.Count == 0, "登记册表行结构不符：\n  " + string.Join("\n  ", problems));
    }
}
