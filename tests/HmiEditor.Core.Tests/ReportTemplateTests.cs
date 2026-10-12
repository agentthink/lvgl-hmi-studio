using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **报表模板**判据（阶段 181）：模板是**覆盖层**（顺序 ＋ 改名 ＋ 显式隐藏），不是白名单 ——
/// 这一条是本阶段最重要的取舍，所以专门有一条判据钉住"没列到的列**接在后面**，不丢"。
/// </summary>
public class ReportTemplateTests
{
    private static ReportTable Table(byte[]? csv = null) => new()
    {
        Title = "T",
        Columns = new List<ReportColumn> { new("a", "A"), new("b", "B"), new("c", "C") },
        Rows = new List<IReadOnlyList<string>>
        {
            new[] { "1", "2", "3" },
            new[] { "4", "5", "6" },
        },
        CsvOverride = csv,
    };

    private static ReportTemplate Template(params ReportTemplateEntry[] entries) => new(entries);

    /// <summary>没设模板（<c>null</c>）与空模板都是**恒等**：不是"什么都不要"。</summary>
    [Fact]
    public void NoTemplate_IsTheIdentity()
    {
        var table = Table();

        Assert.Same(table, table.ApplyTemplate(null));
        Assert.Same(table, table.ApplyTemplate(ReportTemplate.Empty));
        Assert.Same(table, table.ApplyTemplate(Template()));
    }

    /// <summary>
    /// 改名只换**显示名**，列键永远不动（键是投影/判据的对齐依据）。
    /// ⚠ 列到的键同时也会**排到前面**（模板列的是"顺序 ＋ 改名"一件事，见类型注释）——
    /// 所以这条同时也核对顺序：<c>b</c> 提到最前，行跟着重排。
    /// </summary>
    [Fact]
    public void Rename_ChangesDisplayNameOnly()
    {
        var table = Table().ApplyTemplate(Template(new ReportTemplateEntry("b", "β")));

        Assert.Equal(new[] { "b", "a", "c" }, table.Columns.Select(c => c.Key).ToArray());
        Assert.Equal(new[] { "β", "A", "C" }, table.Columns.Select(c => c.DisplayName).ToArray());
        Assert.Equal(new[] { "2", "1", "3" }, table.Rows[0]);
    }

    /// <summary>列到的键按模板顺序排前面，行跟着同一顺序重排。</summary>
    [Fact]
    public void Order_MovesListedKeysFirst()
    {
        var table = Table().ApplyTemplate(Template(new ReportTemplateEntry("c"), new ReportTemplateEntry("a")));

        Assert.Equal(new[] { "c", "a", "b" }, table.Columns.Select(c => c.Key).ToArray());
        Assert.Equal(new[] { "3", "1", "2" }, table.Rows[0]);
        Assert.Equal(new[] { "6", "4", "5" }, table.Rows[1]);
    }

    /// <summary>
    /// **没列到的列接在后面、不丢**（＝覆盖层，不是白名单）：
    /// 模板只提了 <c>b</c>，<c>a</c>/<c>c</c> 必须还在（历史数据报表里"用户新加了一支画笔"
    /// 就靠这一条活着）。正控：列数仍是 3。
    /// </summary>
    [Fact]
    public void UnlistedColumns_AreAppendedNotDropped()
    {
        var table = Table().ApplyTemplate(Template(new ReportTemplateEntry("b")));

        Assert.Equal(3, table.Columns.Count);
        Assert.Equal(new[] { "b", "a", "c" }, table.Columns.Select(c => c.Key).ToArray());
        Assert.Equal(new[] { "2", "1", "3" }, table.Rows[0]);
    }

    /// <summary>显式隐藏 ⇒ 拿掉那一列（列与单元格一起）。</summary>
    [Fact]
    public void Hidden_RemovesTheColumn()
    {
        var table = Table().ApplyTemplate(Template(new ReportTemplateEntry("b", Hidden: true)));

        Assert.Equal(new[] { "a", "c" }, table.Columns.Select(c => c.Key).ToArray());
        Assert.Equal(new[] { "1", "3" }, table.Rows[0]);
    }

    /// <summary>全隐藏是"导不出任何东西"⇒ 抛（静默给空表会让用户以为这段时间没数据）。</summary>
    [Fact]
    public void AllHidden_Throws()
    {
        Assert.Throws<ArgumentException>(() => Table().ApplyTemplate(Template(
            new ReportTemplateEntry("a", Hidden: true),
            new ReportTemplateEntry("b", Hidden: true),
            new ReportTemplateEntry("c", Hidden: true))));
    }

    /// <summary>未知键（模板里留着已删掉的变量）忽略，且不占位、不把别的列挤掉。</summary>
    [Fact]
    public void UnknownKeys_AreIgnored()
    {
        var table = Table().ApplyTemplate(Template(
            new ReportTemplateEntry("zzz"),
            new ReportTemplateEntry("b")));

        Assert.Equal(new[] { "b", "a", "c" }, table.Columns.Select(c => c.Key).ToArray());
    }

    /// <summary>重复键**第一条生效**（顺序与改名都一样）—— 同一份模板不许给出两种列序。</summary>
    [Fact]
    public void DuplicateKeys_FirstEntryWins()
    {
        var table = Table().ApplyTemplate(Template(
            new ReportTemplateEntry("b", "β"),
            new ReportTemplateEntry("b", "BB"),
            new ReportTemplateEntry("b", Hidden: true)));

        Assert.Equal(new[] { "b", "a", "c" }, table.Columns.Select(c => c.Key).ToArray());
        Assert.Equal("β", table.Columns[0].DisplayName);
    }

    /// <summary>标题与 CSV 覆盖形态原样带过去（CSV 有自己的列序，模板对它不生效）。</summary>
    [Fact]
    public void TitleAndCsvOverride_AreCarriedThrough()
    {
        var csv = new byte[] { 1, 2, 3 };
        var table = Table(csv).ApplyTemplate(Template(new ReportTemplateEntry("c")));

        Assert.Equal("T", table.Title);
        Assert.Same(csv, table.CsvOverride);
    }

    /// <summary>模板不改原表（套用是纯函数：原表的列/行/列名一个都不许动）。</summary>
    [Fact]
    public void ApplyTemplate_DoesNotMutateTheSource()
    {
        var source = Table();
        var after = source.ApplyTemplate(Template(new ReportTemplateEntry("c", "γ"), new ReportTemplateEntry("b", Hidden: true)));

        Assert.Equal(new[] { "a", "b", "c" }, source.Columns.Select(c => c.Key).ToArray());
        Assert.Equal(new[] { "A", "B", "C" }, source.Columns.Select(c => c.DisplayName).ToArray());
        Assert.Equal(new[] { "1", "2", "3" }, source.Rows[0]);
        Assert.NotSame(source, after);
    }

    // ────────────────────────── 设计器视图（Spread，阶段 183） ──────────────────────────

    private static IReadOnlyList<ReportColumn> Columns() => new List<ReportColumn>
    {
        new("a", "A"), new("b", "B"), new("c", "C"),
    };

    /// <summary>没有模板 ⇒ 原序、原名、全可见。</summary>
    [Fact]
    public void Spread_WithoutTemplate_IsTheColumnListItself()
    {
        var entries = ReportTemplate.Spread(Columns(), null);

        Assert.Equal(new[] { "a", "b", "c" }, entries.Select(e => e.Key).ToArray());
        Assert.Equal(new[] { "A", "B", "C" }, entries.Select(e => e.DisplayName).ToArray());
        Assert.All(entries, e => Assert.False(e.Hidden));
    }

    /// <summary>
    /// **隐藏的列留在原位**（这是本方法存在的理由）：设计器若拿"导出视图"当输入，
    /// 被隐藏的列就再也勾不回来。
    /// </summary>
    [Fact]
    public void Spread_KeepsHiddenColumnsInPlace()
    {
        var template = Template(
            new ReportTemplateEntry("c", "第三"),
            new ReportTemplateEntry("b", Hidden: true));

        var entries = ReportTemplate.Spread(Columns(), template);

        Assert.Equal(new[] { "c", "b", "a" }, entries.Select(e => e.Key).ToArray());
        Assert.Equal(new[] { "第三", "B", "A" }, entries.Select(e => e.DisplayName).ToArray());
        Assert.Equal(new[] { false, true, false }, entries.Select(e => e.Hidden).ToArray());
    }

    /// <summary>模板里没列到的列接在后面（新列/没动过的列不许消失）。</summary>
    [Fact]
    public void Spread_AppendsUnlistedColumns()
    {
        var entries = ReportTemplate.Spread(Columns(), Template(new ReportTemplateEntry("b")));

        Assert.Equal(new[] { "b", "a", "c" }, entries.Select(e => e.Key).ToArray());
    }

    /// <summary>陈旧键（列清单里已经没有）忽略 —— 否则设计器会显示一列不存在的东西。</summary>
    [Fact]
    public void Spread_IgnoresStaleKeys()
    {
        var entries = ReportTemplate.Spread(Columns(), Template(
            new ReportTemplateEntry("gone", "旧的"),
            new ReportTemplateEntry("b", Hidden: true)));

        Assert.Equal(new[] { "b", "a", "c" }, entries.Select(e => e.Key).ToArray());
        Assert.True(entries[0].Hidden);
    }

    /// <summary>**铺开 → 收拢**是恒等**（设计器"打开又直接保存"不该改动模板）**。</summary>
    [Fact]
    public void SpreadThenRebuild_RoundTripsTheTemplate()
    {
        var template = Template(
            new ReportTemplateEntry("c", "第三"),
            new ReportTemplateEntry("b", Hidden: true));

        var rebuilt = new ReportTemplate(ReportTemplate.Spread(Columns(), template)
            .Select(e => e.Hidden
                ? new ReportTemplateEntry(e.Key, Hidden: true)
                : new ReportTemplateEntry(e.Key, e.DisplayName))
            .ToList());

        var table = Table().ApplyTemplate(rebuilt);
        Assert.Equal(new[] { "c", "a" }, table.Columns.Select(c => c.Key).ToArray());
        Assert.Equal(new[] { "第三", "A" }, table.Columns.Select(c => c.DisplayName).ToArray());
    }
}
