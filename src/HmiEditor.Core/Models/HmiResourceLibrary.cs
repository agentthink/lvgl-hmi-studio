using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models;

/// <summary>
/// HMI 资源类型 —— 图库资源库支持的资源种类。
/// </summary>
public enum ResourceItemType
{
    /// <summary>矢量图标（PNG 位图，通常 48×48 或 64×64）</summary>
    Icon,
    /// <summary>矢量图标（SVG 内联几何）</summary>
    SvgIcon,
    /// <summary>控件模板（Faceplate 复用单元）</summary>
    Template,
    /// <summary>状态指示符（LED 颜色/状态组合）</summary>
    StatusIndicator,
    /// <summary>过程图形元素（阀门、管道、仪表等）</summary>
    ProcessGraphic,
    /// <summary>面板布局（页面级布局模板）</summary>
    PanelLayout,
    /// <summary>图表系列（预定义样式的趋势图/柱状图等）</summary>
    ChartSeries,
}

/// <summary>
/// HMI 应用场景 —— 资源所面向的终端行业。
/// </summary>
public enum ApplicationScenario
{
    Industrial,       // 工业自动化
    Automotive,       // 汽车信息娱乐
    SmartHome,        // 智能家居
    Medical,          // 医疗仪器
    Energy,           // 能源管理
}

/// <summary>
/// 单个 HMI 图库资源条目。
/// </summary>
public class HmiResourceItem
{
    /// <summary>全局唯一 ID（项目内）。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>资源名称。</summary>
    public string Name { get; set; } = "";

    /// <summary>资源显示名称（可多语言）。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>资源类型。</summary>
    public ResourceItemType Type { get; set; }

    /// <summary>应用场景标签。</summary>
    public ApplicationScenario Scenario { get; set; }

    /// <summary>细分分类（如 "按钮-标准"、"仪表-圆形"）。</summary>
    public string SubCategory { get; set; } = "";

    /// <summary>描述。</summary>
    public string Description { get; set; } = "";

    /// <summary>默认尺寸（宽，模型单位）。</summary>
    public double DefaultWidth { get; set; } = 48;

    /// <summary>默认尺寸（高，模型单位）。</summary>
    public double DefaultHeight { get; set; } = 48;

    /// <summary>关联的 HmiControlType（对 Template 类型，指定实例化时的控件类型）。</summary>
    public HmiControlType? DefaultControlType { get; set; }

    /// <summary>矢量图标 Path 数据（viewBox 0 0 24 24）；Type == Icon 时有值。</summary>
    public string? IconPathData { get; set; }

    /// <summary>SVG 内联几何定义（Type == SvgIcon 时有效，序列化为 JSON）。</summary>
    public string? SvgGeometry { get; set; }

    /// <summary>
    /// 关联的 Faceplate 模板 ID（Type == Template / PanelLayout 时有效）。
    /// null 表示直接放置，非 null 表示通过模板实例化。
    /// </summary>
    public string? AssociatedTemplateId { get; set; }

    /// <summary>
    /// 主色调十六进制字符串（如 "#3b82f6"），用于卡片着色和预览高亮。
    /// </summary>
    public string? PrimaryColor { get; set; }

    /// <summary>
    /// 次色调十六进制字符串（如 "#1d4ed8"），用于渐变/点缀。
    /// </summary>
    public string? SecondaryColor { get; set; }

    /// <summary>标签，用于搜索过滤。</summary>
    public string[] Tags { get; set; } = [];

    /// <summary>是否为用户自定义资源（非内置）。</summary>
    public bool IsCustom { get; set; }

    /// <summary>
    /// 资源版本 —— 每次修改模板定义时递增。
    /// 用于追踪资源库变更，支持回退。
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 上次修改时间（UTC）。
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// HMI 图库资源库 —— 跨项目共享的预置资源集合。
/// 资源库分为：内置预置（只读）+ 用户自定义（可读写）。
/// </summary>
public class HmiResourceLibrary
{
    /// <summary>
    /// 内置预置资源 —— 由系统定义，用户不可编辑，可被多个项目共享。
    /// </summary>
    public List<HmiResourceItem> BuiltInResources { get; } = new();

    /// <summary>
    /// 用户自定义资源 —— 用户导入或创建的工程资源。
    /// </summary>
    public ObservableCollection<HmiResourceItem> CustomResources { get; } = new();

    /// <summary>
    /// 获取所有资源（内置 + 自定义），内置优先排在前面。
    /// </summary>
    public IEnumerable<HmiResourceItem> AllResources
        => BuiltInResources.Concat(CustomResources);

    /// <summary>
    /// 按 ID 查找资源（先查内置，再查自定义）。
    /// </summary>
    public HmiResourceItem? FindById(string id)
    {
        var item = BuiltInResources.FirstOrDefault(r => r.Id == id);
        if (item != null) return item;
        return CustomResources.FirstOrDefault(r => r.Id == id);
    }

    /// <summary>
    /// 添加自定义资源。
    /// </summary>
    public void AddCustomResource(HmiResourceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        CustomResources.Add(item);
    }

    /// <summary>
    /// 移除自定义资源（内置资源不可移除）。
    /// </summary>
    public bool RemoveCustomResource(string id)
    {
        var item = CustomResources.FirstOrDefault(r => r.Id == id);
        return item != null && CustomResources.Remove(item);
    }

    /// <summary>
    /// 按分类筛选资源。
    /// </summary>
    public IEnumerable<HmiResourceItem> FilterByType(ResourceItemType type)
        => AllResources.Where(r => r.Type == type);

    /// <summary>
    /// 按应用场景筛选资源。
    /// </summary>
    public IEnumerable<HmiResourceItem> FilterByScenario(ApplicationScenario scenario)
        => AllResources.Where(r => r.Scenario == scenario);

    /// <summary>
    /// 文本搜索（名称 / 描述 / 标签）。
    /// </summary>
    public IEnumerable<HmiResourceItem> Search(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return AllResources;
        var kw = keyword.ToLowerInvariant();
        return AllResources.Where(r =>
            r.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
            r.DisplayName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
            r.Description.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
            r.Tags.Any(t => t.Contains(kw, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// 按场景分组，便于分类浏览。
    /// </summary>
    public Dictionary<ApplicationScenario, List<HmiResourceItem>> GroupByScenario()
    {
        var groups = new Dictionary<ApplicationScenario, List<HmiResourceItem>>();
        foreach (var r in AllResources)
        {
            if (!groups.TryGetValue(r.Scenario, out var list))
            {
                list = new List<HmiResourceItem>();
                groups[r.Scenario] = list;
            }
            list.Add(r);
        }
        return groups;
    }

    /// <summary>
    /// 序列化到 JSON（含所有资源）。
    /// </summary>
    public string Serialize()
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            builtIn = BuiltInResources,
            custom = CustomResources
        }, Serialization.JsonOptionsDefaults.Default);
    }

    /// <summary>
    /// 从 JSON 反序列化并合并到现有库。
    /// </summary>
    public void Deserialize(string json)
    {
        var wrapper = System.Text.Json.JsonSerializer.Deserialize<JsonResourceWrapper>(json, Serialization.JsonOptionsDefaults.Default);
        if (wrapper == null) return;

        // 只合并自定义资源，保留内置资源不变
        foreach (var item in wrapper.Custom ?? [])
        {
            var existing = CustomResources.FirstOrDefault(r => r.Id == item.Id);
            if (existing != null)
            {
                // 更新现有自定义资源
                existing.Name = item.Name;
                existing.DisplayName = item.DisplayName;
                existing.Description = item.Description;
                existing.DefaultWidth = item.DefaultWidth;
                existing.DefaultHeight = item.DefaultHeight;
                existing.IconPathData = item.IconPathData;
                existing.SvgGeometry = item.SvgGeometry;
                existing.PrimaryColor = item.PrimaryColor;
                existing.SecondaryColor = item.SecondaryColor;
                existing.Tags = item.Tags;
                existing.Version = item.Version;
                existing.LastModified = item.LastModified;
            }
            else
            {
                CustomResources.Add(item);
            }
        }
    }

    /// <summary>内部反序列化包装器。</summary>
    private class JsonResourceWrapper
    {
        public List<HmiResourceItem>? BuiltIn { get; set; }
        public List<HmiResourceItem>? Custom { get; set; }
    }
}
