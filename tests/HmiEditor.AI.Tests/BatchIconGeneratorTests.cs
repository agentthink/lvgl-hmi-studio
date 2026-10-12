using HmiEditor.AI.Generators;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// BatchIconGenerator 测试 —— 条目解析。
/// </summary>
public class BatchIconGeneratorTests
{
    [Fact]
    public void ParseItemList_SeparatedByColon_ParsesNameAndDescription()
    {
        var items = BatchIconGenerator.ParseItemList("水泵: 水泵图标带叶片\n电机: 电机图标");
        Assert.Equal(2, items.Count);
        Assert.Equal(("水泵", "水泵图标带叶片"), items[0]);
        Assert.Equal(("电机", "电机图标"), items[1]);
    }

    [Fact]
    public void ParseItemList_NoColon_NameUsedAsDescription()
    {
        var items = BatchIconGenerator.ParseItemList("阀门\n泵");
        Assert.Equal(2, items.Count);
        Assert.Equal(("阀门", "阀门"), items[0]);
        Assert.Equal(("泵", "泵"), items[1]);
    }

    [Fact]
    public void ParseItemList_EmptyLines_Skipped()
    {
        var items = BatchIconGenerator.ParseItemList("水泵\n\n\n电机\n");
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void ParseItemList_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Empty(BatchIconGenerator.ParseItemList(""));
        Assert.Empty(BatchIconGenerator.ParseItemList("   \n  \n"));
        Assert.Empty(BatchIconGenerator.ParseItemList(null!));
    }

    [Fact]
    public void ParseItemList_TrimsWhitespace()
    {
        var items = BatchIconGenerator.ParseItemList("  水泵  :  带叶片的泵  ");
        Assert.Equal(("水泵", "带叶片的泵"), items[0]);
    }
}
