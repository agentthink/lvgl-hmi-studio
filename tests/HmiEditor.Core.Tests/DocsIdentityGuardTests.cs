using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **两份大文档的身份守卫**（阶段 215）—— 直接盯住一次真实事故：
///
/// <para><b>事故</b>（2026-10-01，阶段 214 的 docs 提交）：往登记册追加 §130 时，脚本写成了
/// <c>p.write_text(项目状态的内容 + §130)</c> —— 于是
/// <c>docs/2026-09-17-open-items-register.md</c> **整个文件**被
/// <c>docs/project-status.md</c> 的内容覆盖，而且**已经提交**（`50a9061`）。
/// 发现它的不是门禁，而是阶段 215 想给登记册里 `#96`/`#97` 两行补注时
/// <c>next(l for l in lines if l.startswith("| 96 |"))</c> 抛了 <c>StopIteration</c>。
/// 恢复靠 <c>git checkout c966280 -- docs/…register.md</c> ＋ 重放 §130/§131。</para>
///
/// <para><b>为什么值得一条判据</b>：这两份文档各自 1.4 MB、都在"追加式"维护，
/// 一旦互相覆盖，**内容看起来仍然像一份合法文档**（有抬头、有表格），
/// 只有人肉翻才会发现 —— 而这一次差点就那样进了历史。</para>
///
/// <para>三道核对：① 各自的**抬头**与**标志性结构**必须在；② 两份文档**内容不得相同**；
/// ③ 登记册不得以项目状态的抬头开头（反之亦然）。</para>
/// </summary>
public class DocsIdentityGuardTests
{
    private const string RegisterPath = "docs/2026-09-17-open-items-register.md";
    private const string StatusPath = "docs/project-status.md";

    private static string Read(string relative)
    {
        var path = Path.Combine(SourceScanner.RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"文档不存在：{relative}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void Register_IsStillTheRegister()
    {
        var text = Read(RegisterPath);

        Assert.StartsWith("# 未闭环项登记表", text.TrimStart());
        // ⚠ 2026-10-01（阶段 246，登记 `#100`）：§1 的条目号改带节前缀（`§1-#1`），
        //   所以"第 1 行"的锚点从 `| 1 |` 换成 `| §1-#1 |`；§4 的裸号仍是 `| 97 |`。
        Assert.True(text.Contains("| §1-#1 |", StringComparison.Ordinal),
            $"{RegisterPath} 里连 §1 条目表的第 1 行都没有 —— 文件可能被别的内容覆盖了");
        Assert.True(text.Contains("| 97 |", StringComparison.Ordinal),
            $"{RegisterPath} 里找不到最新条目行（阶段 215 时是 `| 97 |`）—— 可能回退或被覆盖");

        var rows = Regex.Matches(text, @"^\| \d+ \|", RegexOptions.Multiline).Count;
        Assert.True(rows >= 90, $"{RegisterPath} 的条目行只剩 {rows} 行 —— 阶段 215 实测 ≥90");
    }

    [Fact]
    public void Status_IsStillTheStatus()
    {
        var text = Read(StatusPath);

        Assert.StartsWith("# 项目状态基线", text.TrimStart());
        Assert.True(text.Contains("## 阶段 215（2026-10-01）", StringComparison.Ordinal),
            $"{StatusPath} 里没有最新阶段小节（阶段 215 时应有 `## 阶段 215（2026-10-01）`）");

        var stages = Regex.Matches(text, @"^## 阶段 \d+", RegexOptions.Multiline).Count;
        Assert.True(stages >= 100, $"{StatusPath} 的阶段小节只剩 {stages} 个 —— 阶段 215 实测 ≥100");
    }

    /// <summary>两份文档**不得互相成为对方的副本**（事故的直接形状）。</summary>
    [Fact]
    public void TheTwoDocs_AreNotCopiesOfEachOther()
    {
        var register = Read(RegisterPath);
        var status = Read(StatusPath);

        Assert.False(register == status,
            $"{RegisterPath} 与 {StatusPath} 内容完全相同 —— 其中一份被另一份覆盖了");

        Assert.False(register.TrimStart().StartsWith("# 项目状态基线", StringComparison.Ordinal),
            $"{RegisterPath} 以「项目状态基线」开头 —— 它被 {StatusPath} 的内容覆盖了");
        Assert.False(status.TrimStart().StartsWith("# 未闭环项登记表", StringComparison.Ordinal),
            $"{StatusPath} 以「未闭环项登记表」开头 —— 它被 {RegisterPath} 的内容覆盖了");

        // 两份都还有自己的正文（不是被截断成一小段）
        Assert.True(register.Length > 500_000, $"{RegisterPath} 只剩 {register.Length} 字节 —— 可能被截断");
        Assert.True(status.Length > 500_000, $"{StatusPath} 只剩 {status.Length} 字节 —— 可能被截断");
    }
}
