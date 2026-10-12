using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models;

/// <summary>
/// 资源类型枚举（图片 / 字体 / 语言 / SVG）。
/// </summary>
public enum AssetType
{
    Image,
    Font,
    Language,

    // I6: SVG 矢量图形（末尾追加，保持既有枚举数值兼容）
    Svg
}

/// <summary>
/// 单个资源条目。所有字段均使用 C# 默认值，保证 System.Text.Json
/// 反序列化缺失字段时填默认值（向后兼容）。
/// </summary>
public class AssetItem
{
    /// <summary>资源唯一 ID。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>显示名称。</summary>
    public string Name { get; set; } = "";

    /// <summary>资源类型。</summary>
    public AssetType Type { get; set; }

    /// <summary>相对项目根的文件路径。</summary>
    public string FilePath { get; set; } = "";

    /// <summary>文件字节数。</summary>
    public long SizeBytes { get; set; }

    /// <summary>图片像素宽度（非图片资源为 0）。</summary>
    public int Width { get; set; }

    /// <summary>图片像素高度（非图片资源为 0）。</summary>
    public int Height { get; set; }

    /// <summary>色深（位），如 8/16/24/32。</summary>
    public int ColorDepth { get; set; }

    /// <summary>被控件引用的次数。0 表示悬空引用。</summary>
    public int ReferenceCount { get; set; }

    /// <summary>导入时间（UTC）。</summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 资源库 —— 作为独立 Core 模型管理图片/字体/语言资源，
/// 不侵入 .hmi.json（独立资源，可独立管理）。
/// </summary>
public class AssetLibrary
{
    public ObservableCollection<AssetItem> Assets { get; set; } = new();

    /// <summary>添加资源条目。</summary>
    public void AddAsset(AssetItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Assets.Add(item);
    }

    /// <summary>按 ID 移除资源条目，返回是否成功。</summary>
    public bool RemoveAsset(string id)
    {
        var item = Assets.FirstOrDefault(a => a.Id == id);
        if (item is null) return false;
        return Assets.Remove(item);
    }

    /// <summary>移除指定资源条目。</summary>
    public bool RemoveAsset(AssetItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return RemoveAsset(item.Id);
    }

    /// <summary>按名称查找首个匹配资源（区分大小写，精确匹配）。</summary>
    public AssetItem? FindByName(string name)
    {
        return Assets.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.Ordinal));
    }

    /// <summary>递增引用计数。</summary>
    public void IncrementReference(string id)
    {
        var item = Assets.FirstOrDefault(a => a.Id == id);
        if (item is not null) item.ReferenceCount++;
    }

    /// <summary>递减引用计数（不低于 0）。</summary>
    public void DecrementReference(string id)
    {
        var item = Assets.FirstOrDefault(a => a.Id == id);
        if (item is not null && item.ReferenceCount > 0)
            item.ReferenceCount--;
    }

    /// <summary>返回所有悬空引用（ReferenceCount == 0）。</summary>
    public IReadOnlyList<AssetItem> GetOrphans()
    {
        return Assets.Where(a => a.ReferenceCount == 0).ToList();
    }
}
