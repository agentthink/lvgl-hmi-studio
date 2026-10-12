using System;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// 数据根迁移的**接线**守卫：<c>%APPDATA%/HmiEditor</c> → <c>%APPDATA%/zhishu-hmi</c> 之后，
/// 两处持久化都必须满足「**读回退、写不回退**」。
///
/// <para><b>为什么用静态扫描：</b>这条是**接线**性质（哪一行用哪个路径），而它的触发点在
/// Avalonia 生命周期的深处 —— 审计日志恢复发生在 <c>MainWindow</c> 构造函数中段。
/// 行为测试要构造整个 <c>MainWindow</c> 才够得着，成本高且脆弱；而写错方向的后果是**静默**的：
/// 审计日志会一直写进旧目录（新目录永远空、旧目录一直长），界面上完全看不出来。</para>
///
/// <para>LLM 配置侧的同名性质由 <see cref="LlmConfigStoreTests"/> 用行为测试覆盖 ——
/// 它有两个可注入的路径属性，验真实文件字节比扫源码强得多。</para>
/// </summary>
public class DataRootMigrationWiringTests
{
    /// <summary><c>_authService.AuditLogFilePath = &lt;expr&gt;;</c> —— 审计日志**写侧**的唯一入口。</summary>
    private static readonly Regex AuditPathAssignment = new(
        @"AuditLogFilePath\s*=\s*([^;]+);", RegexOptions.Compiled);

    /// <summary>写侧必须落在当前数据根；写回历史位置会让新旧两个目录各自长出一份审计日志。</summary>
    [Fact]
    public void AuditLog_WritesToCurrentRoot_NotTheHistoricalOne()
    {
        var main = AppSourceScanner.Single("MainWindow.axaml.cs");

        // 自检：判据依据的那行还在（否则下面的断言可能只是在读一段无关文本）。
        Assert.True(main.Text.Contains("audit.log.jsonl", StringComparison.Ordinal),
            "MainWindow 里找不到 audit.log.jsonl —— 改名后请同步本守卫的判据");

        var match = AuditPathAssignment.Match(main.Text);
        Assert.True(match.Success, "MainWindow: 未找到 AuditLogFilePath 赋值（改名了？）");

        // 路径通常是"先算再赋"的局部变量，所以再追一跳定义，而不是要求写成一行。
        var expr = ResolveSource(main.Text, match.Groups[1].Value.Trim());
        Assert.True(expr.Contains("AppPaths.DataFile", StringComparison.Ordinal),
            $"审计日志写入位置必须来自当前数据根，实际是：{expr.Trim()}");
        Assert.False(expr.Contains("LegacyDataFile", StringComparison.Ordinal),
            $"审计日志不得写回历史位置（数据会分裂到两个目录）：{expr.Trim()}");
    }

    /// <summary>
    /// 解析赋值右侧的**来源表达式**。<c>AuditLogFilePath = auditPath;</c> 里的
    /// <c>auditPath</c> 通常是先算出来的局部变量，再追一跳定义才能看到真正的来源 ——
    /// 不追的话判据就绑死在"必须写成一行"上（本守卫初版正是这么红的）。
    /// 只追一跳：更多跳属于刻意绕，不值得为它把判据做复杂。
    /// </summary>
    private static string ResolveSource(string text, string target)
    {
        if (target.Contains('.', StringComparison.Ordinal))
            return target;                       // 已是成员访问/方法调用，直接用

        var local = Regex.Match(text, $@"(?:var|string)\s+{Regex.Escape(target)}\s*=\s*([^;]+);");
        return local.Success ? local.Groups[1].Value : target;
    }

    /// <summary>读侧必须**同时**加载当前与历史 —— 后者是升级兼容唯一的理由。</summary>
    [Fact]
    public void AuditLog_AlsoLoadsTheHistoricalFile_SoUpgradeHistorySurvives()
    {
        var main = AppSourceScanner.Single("MainWindow.axaml.cs");

        Assert.True(main.Text.Contains("AppPaths.LegacyDataFile(", StringComparison.Ordinal),
            "MainWindow 没有从历史位置加载审计日志 —— 升级用户的审计历史会读不到");

        var loads = Regex.Matches(main.Text, @"LoadAuditLogFromFile\s*\(").Count;
        Assert.True(loads >= 2,
            $"MainWindow 只加载了 {loads} 次审计日志 —— 升级兼容需要**当前 + 历史**两次"
            + "（LoadAuditLogFromFile 按 Id 去重，重复加载安全）。");
    }

    /// <summary>扫描面自检：这类守卫最大的盲区是"扫了个空所以全绿"。</summary>
    [Fact]
    public void Scanner_ActuallyCoversTheMigrationFiles()
    {
        var files = AppSourceScanner.ScanAppSources().Select(s => s.Relative).ToList();

        Assert.True(files.Count >= 50, $"App 扫描面过小（{files.Count} 个 .cs），疑似根目录解析错误");

        foreach (var required in new[] { "MainWindow.axaml.cs", "Services/LlmConfigStore.cs" })
        {
            Assert.True(files.Any(f => f.EndsWith(required, StringComparison.Ordinal)),
                $"扫描面漏了 {required}（实际 {files.Count} 个文件）");
        }
    }
}
