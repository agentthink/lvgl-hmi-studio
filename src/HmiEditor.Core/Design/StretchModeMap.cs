namespace HmiEditor.Core.Design;

/// <summary>图片缩放模式语义分类（与 LVGL lv_image_align_t 对齐）。</summary>
public enum StretchModeKind
{
    /// <summary>拉伸填满（LV_IMAGE_ALIGN_STRETCH），默认。</summary>
    Stretch,
    /// <summary>等比缩放完整显示（LV_IMAGE_ALIGN_CONTAIN）。</summary>
    Contain,
    /// <summary>原始尺寸居中（LV_IMAGE_ALIGN_CENTER），对应 HmiControl.StretchMode 的 None/Center。</summary>
    Center,
    /// <summary>平铺（LV_IMAGE_ALIGN_TILE）。</summary>
    Tile
}

/// <summary>
/// 图片缩放模式解析器 —— 设计器渲染（DesignCanvas.TryDrawImage）与代码生成
/// （CodeGenerator.LvglImageAlign）共用，消除两处各自维护的字符串 switch 漂移。
/// 语义：null/空/Fill/Stretch/未知 → Stretch；None/Center → Center；其余大小写不敏感。
/// </summary>
public static class StretchModeMap
{
    /// <summary>解析 HmiControl.StretchMode 字符串（大小写不敏感，null/空 = Stretch）。</summary>
    public static StretchModeKind Parse(string? mode) => (mode ?? "").Trim().ToLowerInvariant() switch
    {
        "contain" => StretchModeKind.Contain,
        "center" or "none" => StretchModeKind.Center,
        "tile" => StretchModeKind.Tile,
        _ => StretchModeKind.Stretch   // null/空/fill/stretch/未知
    };
}
