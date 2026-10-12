using System;
using System.Collections.Generic;
using System.Linq;
using HmiEditor.Core.Reporting;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// **按列键投影判据**（阶段 177）：导出前"选列"的 Core 半边 —— 顺序听调用方的、未知键忽略、
/// 行只留对应单元格、一个都不剩要抛（那是编程错误）。
/// </summary>
public class ReportColumnProjectionTests
{
    private static ReportTable Table() => new()
    {
        Title = "T",
        Columns = new List<ReportColumn>
        {
            new("a", "A列"), new("b", "B列"), new("c", "C列"),
        },
        Rows = new List<IReadOnlyList<string>>
        {
            new[] { "a1", "b1", "c1" },
            new[] { "a2", "b2", "c2" },
        },
    };

    [Fact]
    public void WithColumns_KeepsRequestedOrder_AndProjectsRows()
    {
        var projected = Table().WithColumns(new[] { "c", "a" });

        Assert.Equal(new[] { "c", "a" }, projected.Columns.Select(x => x.Key).ToArray());
        Assert.Equal(new[] { "C列", "A列" }, projected.Columns.Select(x => x.DisplayName).ToArray());
        Assert.Equal(new[] { "c1", "a1" }, projected.Rows[0]);
        Assert.Equal(new[] { "c2", "a2" }, projected.Rows[1]);
        Assert.Equal("T", projected.Title);
    }

    /// <summary>未知列键**忽略**（旧配置里的列名不该把用户挡在门外）；重复键只取一次。</summary>
    [Fact]
    public void WithColumns_IgnoresUnknownKeys_AndDeduplicates()
    {
        var projected = Table().WithColumns(new[] { "zzz", "b", "b", "a" });
        Assert.Equal(new[] { "b", "a" }, projected.Columns.Select(x => x.Key).ToArray());
        Assert.Equal(2, projected.Rows[0].Count);
    }

    /// <summary>一个都不剩 ⇒ 抛（界面上不允许取消掉所有列）。</summary>
    [Fact]
    public void WithColumns_AllUnknownOrEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => Table().WithColumns(Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => Table().WithColumns(new[] { "zzz" }));
    }

    /// <summary>CSV 覆盖形态原样带过去（列选择对它不生效 —— 那是整表的形态）。</summary>
    [Fact]
    public void WithColumns_CarriesCsvOverrideThrough()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var table = new ReportTable
        {
            Title = "T",
            Columns = Table().Columns,
            Rows = Table().Rows,
            CsvOverride = bytes,
        };

        Assert.Equal(bytes, table.WithColumns(new[] { "a" }).CsvOverride);
    }

    [Fact]
    public void WithColumns_IsDeterministic()
    {
        Assert.Equal(
            ReportExporter.ToCsv(Table().WithColumns(new[] { "b", "a" })),
            ReportExporter.ToCsv(Table().WithColumns(new[] { "b", "a" })));
    }
}
