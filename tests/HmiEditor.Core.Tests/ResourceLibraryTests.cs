using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// HMI 图库资源库核心逻辑测试 —— 覆盖资源增删、筛选、序列化、版本控制。
/// </summary>
public class ResourceLibraryTests
{
    // ── 内置资源 ──

    [Fact]
    public void CreateBuiltIn_ContainsResources_ForAllScenarios()
    {
        var builtIn = PresetResourceLibrary.CreateBuiltIn();

        Assert.NotEmpty(builtIn);
        // 工业 / 汽车 / 智能家居场景均应有资源
        Assert.Contains(builtIn, r => r.Scenario == ApplicationScenario.Industrial);
        Assert.Contains(builtIn, r => r.Scenario == ApplicationScenario.Automotive);
        Assert.Contains(builtIn, r => r.Scenario == ApplicationScenario.SmartHome);
        // 资源 ID 唯一
        Assert.Equal(builtIn.Count, builtIn.Select(r => r.Id).Distinct().Count());
        // 资源均有名称和主色调
        Assert.All(builtIn, r => Assert.False(string.IsNullOrWhiteSpace(r.Name)));
        Assert.All(builtIn, r => Assert.False(string.IsNullOrWhiteSpace(r.PrimaryColor)));
    }

    // ── 资源库增删 ──

    [Fact]
    public void AddCustom_And_FindById_Works()
    {
        var lib = new HmiResourceLibrary();
        var item = new HmiResourceItem { Id = "custom-1", Name = "自定义图标", Scenario = ApplicationScenario.Industrial };

        lib.AddCustomResource(item);
        Assert.Single(lib.CustomResources);
        Assert.Same(item, lib.FindById("custom-1"));
        Assert.Null(lib.FindById("missing"));
    }

    [Fact]
    public void RemoveCustomResource_RemovesById()
    {
        var lib = new HmiResourceLibrary();
        lib.AddCustomResource(new HmiResourceItem { Id = "c1", Name = "A" });

        Assert.True(lib.RemoveCustomResource("c1"));
        Assert.Empty(lib.CustomResources);
        Assert.False(lib.RemoveCustomResource("c1"));
    }

    // ── 筛选 ──

    [Fact]
    public void FilterByScenario_ReturnsOnlyMatching()
    {
        var lib = new HmiResourceLibrary();
        lib.BuiltInResources.AddRange(PresetResourceLibrary.CreateBuiltIn());

        var industrial = lib.FilterByScenario(ApplicationScenario.Industrial).ToList();
        Assert.NotEmpty(industrial);
        Assert.All(industrial, r => Assert.Equal(ApplicationScenario.Industrial, r.Scenario));
    }

    [Fact]
    public void FilterByType_ReturnsOnlyMatching()
    {
        var lib = new HmiResourceLibrary();
        lib.BuiltInResources.AddRange(PresetResourceLibrary.CreateBuiltIn());

        var indicators = lib.FilterByType(ResourceItemType.StatusIndicator).ToList();
        Assert.NotEmpty(indicators);
        Assert.All(indicators, r => Assert.Equal(ResourceItemType.StatusIndicator, r.Type));
    }

    [Fact]
    public void Search_MatchesName_Description_AndTags()
    {
        var lib = new HmiResourceLibrary();
        lib.AddCustomResource(new HmiResourceItem
        {
            Id = "s1", Name = "电机启动", Description = "工业电机控制",
            Tags = new[] { "motor", "start" }
        });

        Assert.Single(lib.Search("电机"));
        Assert.Single(lib.Search("motor"));
        Assert.Single(lib.Search("工业"));
        Assert.Empty(lib.Search("不存在的内容"));
        // 空关键字返回全部
        Assert.Equal(lib.AllResources.Count(), lib.Search("").Count());
    }

    // ── 序列化 ──

    [Fact]
    public void Serialize_Deserialize_PreservesCustomResources()
    {
        var lib = new HmiResourceLibrary();
        lib.AddCustomResource(new HmiResourceItem
        {
            Id = "c1", Name = "自定义资源", Scenario = ApplicationScenario.SmartHome,
            Type = ResourceItemType.Icon, PrimaryColor = "#ff0000"
        });

        var json = lib.Serialize();

        var restored = new HmiResourceLibrary();
        restored.Deserialize(json);

        Assert.Single(restored.CustomResources);
        Assert.Equal("c1", restored.CustomResources[0].Id);
        Assert.Equal("#ff0000", restored.CustomResources[0].PrimaryColor);
        Assert.Equal(ResourceItemType.Icon, restored.CustomResources[0].Type);
    }

    [Fact]
    public void Serialize_Deserialize_MergesWithoutDuplicating()
    {
        var lib = new HmiResourceLibrary();
        lib.AddCustomResource(new HmiResourceItem { Id = "c1", Name = "旧名称" });
        var json = lib.Serialize();

        // 再次反序列化应更新而非重复添加
        lib.Deserialize(json);
        Assert.Single(lib.CustomResources);
        Assert.Equal("旧名称", lib.CustomResources[0].Name);
    }
}
