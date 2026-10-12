using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models;

/// <summary>
/// 屏幕背景图片显示模式。
/// </summary>
public enum ScreenBgImageMode
{
    /// <summary>平铺：保持原始尺寸重复填充整个屏幕。</summary>
    Tile,

    /// <summary>拉伸：缩放填满整个屏幕（可能变形）。</summary>
    Stretch,

    /// <summary>居中：保持原始尺寸居中显示，超出的部分裁切。</summary>
    Center
}

/// <summary>
/// 公共页面访问级别 —— 管理员可配置谁有权查看公共页面。
/// </summary>
public enum ScreenPublicAccess
{
    /// <summary>公开：所有用户（含未登录）均可查看。</summary>
    Public = 0,

    /// <summary>登录用户：具备"查看画面"权限的角色可查看。</summary>
    Viewers = 1,

    /// <summary>仅管理员可查看。</summary>
    AdminOnly = 2,
}

public class HmiScreen
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Screen 1";
    public double Width { get; set; } = 800;
    public double Height { get; set; } = 480;
    public string? BgColor { get; set; }

    /// <summary>背景图片资源 ID（引用 AssetItem.Id）。null = 无背景图片。</summary>
    public string? BgImageAssetId { get; set; }

    /// <summary>背景图片显示模式。默认拉伸填满屏幕。</summary>
    public ScreenBgImageMode BgImageMode { get; set; } = ScreenBgImageMode.Stretch;

    /// <summary>背景图片透明度（0-100，100 = 完全不透明）。</summary>
    public int BgImageOpacity { get; set; } = 100;

    /// <summary>I9: 自适应布局模式（None 为默认固定坐标，兼容旧项目）。</summary>
    public ScreenAdaptiveMode AdaptiveMode { get; set; } = ScreenAdaptiveMode.None;

    /// <summary>
    /// 是否为公共页面：多用户共享查看同一屏幕内容，内容实时同步更新。
    /// 管理员可在组态中配置（屏幕管理器 / 属性面板）。
    /// </summary>
    public bool IsPublicScreen { get; set; }

    /// <summary>
    /// 公共页面访问级别（仅 <see cref="IsPublicScreen"/> 为 true 时生效）。
    /// </summary>
    public ScreenPublicAccess PublicAccess { get; set; } = ScreenPublicAccess.Viewers;

    /// <summary>
    /// 引用的图层（公共页面）Id 集合 —— 这些公共页面将作为背景图层嵌入本屏幕。
    /// 约束：仅可引用 IsPublicScreen=true 的屏幕；禁止自引用；被引用的屏幕不再作为独立屏幕切换（纯图层）；
    /// 图层屏幕不能再引用其他图层（仅支持一层嵌套）。
    /// </summary>
    public ObservableCollection<string> ReferencedLayerIds { get; set; } = new();

    public ObservableCollection<HmiControl> Controls { get; set; } = new();
}
