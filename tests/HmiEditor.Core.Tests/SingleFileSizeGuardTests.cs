using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HmiEditor.TestSupport;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 大文件行数守卫：防止治理后的巨型单类再次回涨（2026-09-18 增补轮登记表 §4 #30~33）。
///
/// <para><b>为什么需要：</b>MainWindow 在 09-13 完成七步治理（5023 → 2928 行）后，
/// 阶段 13~30 的本地化/设置接线又把它加回 <b>3013 行</b>（+85），且没有任何东西会拦住 ——
/// （阶段 93 因登记 #61l 的调试行注释再 +1 ⇒ <b>3014</b>；涨的是**注释**不是代码，
///   且已刻意把 4 行压成 1 行 —— 本表的纪律是「涨了 = 先把文件改小」。）
/// 治理收益必须由守卫保护。同理，SimulatorProjectExporter（1540 行）尚未治理，
/// 治理后的收益同样需要本表跟踪。</para>
///
/// <para><b>两类判据：</b></para>
/// <list type="bullet">
///   <item><b>相等判定</b>（ExactBaselines）：对仍是"单个巨型单类"的文件 —— 行数涨了报
///   「治理回涨」、行数降了报「必须同步改小基线」。拆分的提交必须同时更新本表，
///   否则"看起来变好了"的那次改动也会被守卫点名（防止守卫变成过期抄写）。</item>
///   <item><b>上限判定</b>（PartialCeilings）：对已拆 partial 的类（PropertyPanelView）——
///   单个分部文件不得超过上限。用上限而不是相等，是因为分部文件可以正常编辑增长，
///   但不得合并回"巨型单类"形态。</item>
/// </list>
/// </summary>
public class SingleFileSizeGuardTests
{
    /// <summary>
    /// 仓库相对路径 → 当前行数基线（相等判定）。
    /// 登记出处：`docs/2026-09-18-mainstream-gap-check-and-optimization-plan.md` §5.2 B1。
    /// </summary>
    public static TheoryData<string, int> ExactBaselines => new()
    {
        // 3014 → 2994（阶段 120：图库的安全加载/保存析出到 MainWindow.ResourceLibrary.cs）
        // → 2992（阶段 121：解包收尾「缺资产告警 + 图库合并」也整块移进同一个 partial）。
        // → 2983（阶段 162：「面板」菜单那套"查找/激活/被关闭后按老家挂回来"的逻辑移进
        //   `HmiDockFactory.OpenDockable` —— 移进工厂才**够得着判据**，顺带本文件变小）。
        // 三次都是**变小**，按本表纪律必须同步改小基线。
        { Path.Combine("src", "HmiEditor.App", "MainWindow.axaml.cs"), 2983 },
    };

    /// <summary>
    /// 已拆 partial 的类：glob 模式 → 单个分部文件行数上限。
    /// PropertyPanelView 于 P0-2（2026-09-18）拆成 11 个分部，最大分部 Binding.cs = 373 行，
    /// 上限取 500（留正常编辑余量，防合并回巨型单类）。
    /// SimulatorProjectExporter 于 P0-3（2026-09-18）拆成 5 个分部，最大分部 MainC.cs = 676 行
    /// —— 那是单个巨型方法 BuildMainC 的整段（方法级分解属 P1/A4 统一发射器范畴），上限取 700。
    /// </summary>
    public static TheoryData<string, int> PartialCeilings => new()
    {
        { Path.Combine("src", "HmiEditor.App", "Views", "PropertyPanelView.*.cs"), 500 },
        { Path.Combine("src", "HmiEditor.Core", "Services", "SimulatorProjectExporter.*.cs"), 700 },
    };

    [Theory]
    [MemberData(nameof(ExactBaselines))]
    public void File_LineCount_MatchesBaseline(string relativePath, int baseline)
    {
        var fullPath = Path.Combine(SourceScanner.RepoRoot, relativePath);
        Assert.True(File.Exists(fullPath), $"未找到文件: {fullPath}");
        var actual = File.ReadAllLines(fullPath).Length;
        Assert.True(
            actual == baseline,
            $"{relativePath}: 行数 {actual} ≠ 基线 {baseline}。"
            + "涨了 = 治理回涨（先把文件改小）；降了 = 拆分完成但没同步本表（必须改小基线）。");
    }

    [Theory]
    [MemberData(nameof(PartialCeilings))]
    public void PartialFiles_StayUnderCeiling(string globPattern, int ceiling)
    {
        var viewDir = Path.GetDirectoryName(Path.Combine(SourceScanner.RepoRoot, globPattern))!;
        var matches = Directory.GetFiles(viewDir, Path.GetFileName(globPattern))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        Assert.True(
            matches.Count >= 4,
            $"glob {globPattern} 只匹配到 {matches.Count} 个文件 —— 防止守卫退化成空跑。");

        var violations = matches
            .Where(f => File.ReadAllLines(f).Length > ceiling)
            .Select(f => $"{Path.GetFileName(f)}: {File.ReadAllLines(f).Length} 行（上限 {ceiling}）")
            .ToList();
        Assert.True(
            violations.Count == 0,
            $"以下分部文件超过上限 {ceiling} 行（可能被合并回巨型单类）：\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void Guard_StillTracksTheBigFiles()
    {
        // 自检：基线表不得萎缩到没东西可守（防守卫因编辑事故变成空表后永远绿，
        // 与 LocalizationGuardTests / LangKeyCoverageTests 的"扫描面自检"同一思路）。
        Assert.True(
            ExactBaselines.Count() >= 1 && PartialCeilings.Count() >= 2,
            "基线表条目过少（防止守卫退化）。");
    }
}
