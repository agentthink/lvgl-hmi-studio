using HmiEditor.App.Resources;

namespace HmiEditor.App.Design;

/// <summary>
/// 设计器渲染占位/兜底文案 —— 集中管理控件渲染里的固定文案，避免散落硬编码。
///
/// <para><b>用户可见的那些必须是属性而不是 <c>const</c></b>：文案要跟随 UI 语言，
/// 而 <c>const</c> 在编译期就被内联、运行时无法解析。纯符号性的（<c>"100%"</c>、<c>"?"</c>、
/// <c>"Tab1"</c>）仍留作 <c>const</c> —— 它们不进字典。</para>
/// </summary>
public static class DefaultTexts
{
    /// <summary>Image 控件无图时的占位文字（**用户可见**，取用时查字典）。</summary>
    public static string ImagePlaceholder => AppLocalization.Service[Lang.DefaultImagePlaceholder];

    /// <summary>TabView 无页签配置时的默认页签标题。</summary>
    public const string TabFallback = "Tab1";

    /// <summary>罐体刻度顶部标签。</summary>
    public const string ScaleTop = "100%";

    /// <summary>罐体刻度底部标签。</summary>
    public const string ScaleBottom = "0%";

    /// <summary>动作目标未设置时的占位符。</summary>
    public const string ActionTargetEmpty = "?";
}
