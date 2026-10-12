using System.Text.Json;
using System.Text.Json.Serialization;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

public class AssetManagerTests
{
    /// <summary>与 ProjectSerializer 风格一致的 JSON 选项（camelCase + 字符串枚举）。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    // ── AddAsset / RemoveAsset ──

    [Fact]
    public void AddAsset_IncreasesCount()
    {
        var lib = new AssetLibrary();
        Assert.Empty(lib.Assets);

        lib.AddAsset(new AssetItem { Name = "logo.png", Type = AssetType.Image });
        Assert.Single(lib.Assets);
        Assert.Equal("logo.png", lib.Assets[0].Name);
    }

    [Fact]
    public void RemoveAsset_ById_RemovesItem()
    {
        var lib = new AssetLibrary();
        var item = new AssetItem { Name = "bg.png", Type = AssetType.Image };
        lib.AddAsset(item);

        Assert.True(lib.RemoveAsset(item.Id));
        Assert.Empty(lib.Assets);
    }

    [Fact]
    public void RemoveAsset_ByReference_RemovesItem()
    {
        var lib = new AssetLibrary();
        var item = new AssetItem { Name = "f.ttf", Type = AssetType.Font };
        lib.AddAsset(item);

        Assert.True(lib.RemoveAsset(item));
        Assert.Empty(lib.Assets);
    }

    [Fact]
    public void RemoveAsset_UnknownId_ReturnsFalse()
    {
        var lib = new AssetLibrary();
        Assert.False(lib.RemoveAsset("nonexistent"));
    }

    // ── 引用计数 ──

    [Fact]
    public void IncrementReference_IncreasesCount()
    {
        var lib = new AssetLibrary();
        var item = new AssetItem { Name = "img.png", Type = AssetType.Image };
        lib.AddAsset(item);

        Assert.Equal(0, item.ReferenceCount);
        lib.IncrementReference(item.Id);
        lib.IncrementReference(item.Id);
        Assert.Equal(2, item.ReferenceCount);
    }

    [Fact]
    public void DecrementReference_DecreasesCount_AndClampsAtZero()
    {
        var lib = new AssetLibrary();
        var item = new AssetItem { Name = "img.png", Type = AssetType.Image, ReferenceCount = 2 };
        lib.AddAsset(item);

        lib.DecrementReference(item.Id);
        Assert.Equal(1, item.ReferenceCount);
        lib.DecrementReference(item.Id);
        Assert.Equal(0, item.ReferenceCount);
        // 不会变为负数
        lib.DecrementReference(item.Id);
        Assert.Equal(0, item.ReferenceCount);
    }

    [Fact]
    public void IncrementReference_OnOrphan_RemovesItFromOrphans()
    {
        var lib = new AssetLibrary();
        var item = new AssetItem { Name = "orphan.png", Type = AssetType.Image };
        lib.AddAsset(item);

        Assert.Single(lib.GetOrphans());

        lib.IncrementReference(item.Id);
        Assert.Empty(lib.GetOrphans());
    }

    // ── GetOrphans ──

    [Fact]
    public void GetOrphans_ReturnsOnlyZeroReferenceItems()
    {
        var lib = new AssetLibrary();
        var orphan = new AssetItem { Name = "unused.png", Type = AssetType.Image };
        var used = new AssetItem { Name = "used.png", Type = AssetType.Image, ReferenceCount = 1 };
        lib.AddAsset(orphan);
        lib.AddAsset(used);

        var orphans = lib.GetOrphans();
        Assert.Single(orphans);
        Assert.Equal("unused.png", orphans[0].Name);
    }

    [Fact]
    public void GetOrphans_EmptyLibrary_ReturnsEmpty()
    {
        var lib = new AssetLibrary();
        Assert.Empty(lib.GetOrphans());
    }

    // ── FindByName ──

    [Fact]
    public void FindByName_ReturnsMatchingItem()
    {
        var lib = new AssetLibrary();
        lib.AddAsset(new AssetItem { Name = "icon.png", Type = AssetType.Image });
        lib.AddAsset(new AssetItem { Name = "outfit.ttf", Type = AssetType.Font });

        var found = lib.FindByName("icon.png");
        Assert.NotNull(found);
        Assert.Equal(AssetType.Image, found!.Type);
    }

    [Fact]
    public void FindByName_NotFound_ReturnsNull()
    {
        var lib = new AssetLibrary();
        lib.AddAsset(new AssetItem { Name = "icon.png", Type = AssetType.Image });

        Assert.Null(lib.FindByName("missing.png"));
    }

    // ── JSON 往返序列化 ──

    [Fact]
    public void Json_RoundTrip_PreservesAssets()
    {
        var lib = new AssetLibrary();
        var img = new AssetItem
        {
            Name = "hero.png",
            Type = AssetType.Image,
            FilePath = "assets/hero.png",
            SizeBytes = 1024 * 512,
            Width = 800,
            Height = 600,
            ColorDepth = 24,
            ReferenceCount = 3,
            ImportedAt = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc)
        };
        var font = new AssetItem
        {
            Name = "outfit.ttf",
            Type = AssetType.Font,
            FilePath = "assets/outfit.ttf",
            SizeBytes = 96000,
            ColorDepth = 0,
            ReferenceCount = 1
        };
        lib.AddAsset(img);
        lib.AddAsset(font);

        var json = JsonSerializer.Serialize(lib, JsonOptions);
        Assert.False(string.IsNullOrEmpty(json));
        Assert.Contains("\"image\"", json);
        Assert.Contains("\"font\"", json);

        var restored = JsonSerializer.Deserialize<AssetLibrary>(json, JsonOptions);
        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Assets.Count);

        var r0 = restored.Assets[0];
        Assert.Equal("hero.png", r0.Name);
        Assert.Equal(AssetType.Image, r0.Type);
        Assert.Equal("assets/hero.png", r0.FilePath);
        Assert.Equal(1024 * 512, r0.SizeBytes);
        Assert.Equal(800, r0.Width);
        Assert.Equal(600, r0.Height);
        Assert.Equal(24, r0.ColorDepth);
        Assert.Equal(3, r0.ReferenceCount);
        Assert.Equal(img.ImportedAt, r0.ImportedAt);

        var r1 = restored.Assets[1];
        Assert.Equal("outfit.ttf", r1.Name);
        Assert.Equal(AssetType.Font, r1.Type);
        Assert.Equal(96000, r1.SizeBytes);
        Assert.Equal(1, r1.ReferenceCount);
    }

    [Fact]
    public void Json_Deserialize_MissingFields_UsesDefaults()
    {
        // 模拟旧 JSON：缺少 width/height/colorDepth/referenceCount 等字段
        var json = @"{""assets"":[{""id"":""ab12cd34"",""name"":""legacy.png"",""type"":""image"",""filePath"":""x.png""}]}";

        var restored = JsonSerializer.Deserialize<AssetLibrary>(json, JsonOptions);

        Assert.NotNull(restored);
        Assert.Single(restored!.Assets);
        var item = restored.Assets[0];
        Assert.Equal("legacy.png", item.Name);
        Assert.Equal(AssetType.Image, item.Type);
        Assert.Equal("x.png", item.FilePath);
        // 缺失字段填 C# 默认值（向后兼容）
        Assert.Equal(0, item.Width);
        Assert.Equal(0, item.Height);
        Assert.Equal(0, item.ColorDepth);
        Assert.Equal(0, item.ReferenceCount);
        Assert.Equal(0, item.SizeBytes);
    }

    [Fact]
    public void Json_RoundTrip_EmptyLibrary()
    {
        var lib = new AssetLibrary();

        var json = JsonSerializer.Serialize(lib, JsonOptions);
        var restored = JsonSerializer.Deserialize<AssetLibrary>(json, JsonOptions);

        Assert.NotNull(restored);
        Assert.Empty(restored!.Assets);
    }
}
