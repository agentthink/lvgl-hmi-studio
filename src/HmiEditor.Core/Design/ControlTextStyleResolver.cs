using HmiEditor.Core.Models;

namespace HmiEditor.Core.Design;

/// <summary>
/// 控件文本样式解析 —— 设计器渲染时「状态样式优先、控件默认兜底」的唯一事实源。
///
/// 为什么抽成纯函数：这条规则原先只存在于 <c>DesignCanvas</c> 的渲染方法里，
/// 因此只能用「读源码 grep 断言」来防回归（重构即碎、行为不变也会碎）。
/// 抽到 Core 后可以用真正的行为测试覆盖，渲染代码只负责调用。
///
/// 注意：这里只解析「优先级」，最终的颜色解析（hex → Color）与调色板兜底色
/// 仍由渲染层负责（它依赖 Avalonia 的 Color 类型）。
/// </summary>
public static class ControlTextStyleResolver
{
    /// <summary>
    /// 文本颜色优先级：状态样式 TextColor → 控件 TextColor → 主题文本色。
    /// 返回 null 表示三者皆无，由渲染层用调色板兜底色。
    /// 此前通用文本分支硬编码纯白，导致「设计器里设置文本色不生效」。
    /// </summary>
    public static string? PreferHex(
        ControlVisualState? stateStyle, HmiControl control, string? themeTextColor)
    {
        var hex = stateStyle?.TextColor ?? control.TextColor;
        if (!string.IsNullOrEmpty(hex)) return hex;
        // 空串视为「未设置」，与渲染层 ParseColorHex 的判空语义保持一致
        return string.IsNullOrEmpty(themeTextColor) ? null : themeTextColor;
    }

    /// <summary>水平对齐：Normal 预览态取控件值；其它态状态样式优先。</summary>
    public static TextAlignMode HorizontalAlign(
        ControlVisualState? stateStyle, HmiControl control, VisualState previewState)
        => previewState == VisualState.Normal
            ? control.TextAlign
            : (stateStyle?.TextAlign ?? control.TextAlign);

    /// <summary>垂直对齐：Normal 预览态取控件值；其它态状态样式优先。</summary>
    public static TextVAlignMode VerticalAlign(
        ControlVisualState? stateStyle, HmiControl control, VisualState previewState)
        => previewState == VisualState.Normal
            ? control.TextVAlign
            : (stateStyle?.TextVAlign ?? control.TextVAlign);
}
