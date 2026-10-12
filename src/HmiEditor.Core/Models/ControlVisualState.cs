namespace HmiEditor.Core.Models;

/// <summary>
/// 控件视觉状态枚举（对齐 LVGL 状态机）。
/// Normal = LV_STATE_DEFAULT；
/// Pressed = LV_STATE_PRESSED；
/// Checked = LV_STATE_CHECKED（用于 Toggle 按钮 / Switch / CheckBox 的 IsChecked=true 态）；
/// Disabled = LV_STATE_DISABLED。
/// </summary>
public enum VisualState
{
    Normal,
    Pressed,
    Checked,
    Disabled
}

/// <summary>文本水平对齐（控件内 9 宫格位置的水平分量）。</summary>
public enum TextAlignMode
{
    Left,
    Center,
    Right
}

/// <summary>文本垂直对齐（控件内 9 宫格位置的垂直分量）。</summary>
public enum TextVAlignMode
{
    Top,
    Middle,
    Bottom
}

/// <summary>
/// 单个视觉状态的样式覆盖。所有字段可空 —— null 表示继承默认态（Normal）的对应属性。
/// 这样只记录与默认态有差异的字段，减少冗余。
/// </summary>
public class ControlVisualState
{
    /// <summary>该样式适用的状态。</summary>
    public VisualState State { get; set; }

    /// <summary>背景色（十六进制如 #2563eb）。null = 继承默认态。</summary>
    public string? BgColor { get; set; }

    /// <summary>文本色。null = 继承默认态。</summary>
    public string? TextColor { get; set; }

    /// <summary>边框色。null = 继承默认态。</summary>
    public string? BorderColor { get; set; }

    /// <summary>该状态显示的图片资源 ID（引用 AssetItem.Id）。null = 不显示图片。</summary>
    public string? ImageAssetId { get; set; }

    /// <summary>该状态显示的文本。null = 继承默认态（即控件的 Text 字段）。</summary>
    public string? Text { get; set; }

    /// <summary>该状态文本的水平对齐。null = 继承默认态。</summary>
    public TextAlignMode? TextAlign { get; set; }

    /// <summary>该状态文本的垂直对齐。null = 继承默认态。</summary>
    public TextVAlignMode? TextVAlign { get; set; }

    /// <summary>判断该状态是否未配置任何覆盖字段（用于决定是否从集合中移除）。</summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(BgColor)
        && string.IsNullOrEmpty(TextColor)
        && string.IsNullOrEmpty(BorderColor)
        && string.IsNullOrEmpty(ImageAssetId)
        && string.IsNullOrEmpty(Text)
        && TextAlign == null
        && TextVAlign == null;

    /// <summary>
    /// 浅拷贝 —— 本类字段全是字符串/枚举（无嵌套引用），浅拷贝即等价深拷贝。
    /// 用 <c>MemberwiseClone</c> 而非手工列字段：后者**必然随新增字段漂移**。
    /// </summary>
    public ControlVisualState ShallowCopy() => (ControlVisualState)MemberwiseClone();
}
