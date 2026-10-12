using System.Globalization;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Design;

/// <summary>可被绑定驱动的视觉属性值域。</summary>
public enum BindingVisualKind
{
    /// <summary>颜色：表达式/变量返回 24 位 RGB 数值（0xRRGGBB）。</summary>
    Color,

    /// <summary>可见性：非零即可见。</summary>
    Visibility,
}

/// <summary>
/// 「可被绑定驱动的视觉属性」注册表 —— 表达式/变量驱动颜色与可见性的**唯一事实源**。
///
/// 背景（2026-09-19，P1-1）：绑定此前只驱动「值」（Text/Value），颜色与可见性在设计器里
/// 是**从模型直读**的（`DesignCanvas` 与 `Renderers` 各自读 `control.BgColor` / `control.IsVisible`），
/// 生成代码里也只在初始化时静态发一次。若各处自行按字符串属性名分派，就会重演
/// <see cref="BindingSemantics"/> 诞生前的跨端漂移（设计器显示 12.34、设备显示 12.3）。
/// 因此：**属性名 → 值域 → 应用方式** 只在这里定义一次，
/// 设计时（<see cref="PreviewValueResolver"/>）与生成期（`CodeGenerator`）都读这张表，
/// 任何一侧都**不得**再写第二份 `PropertyName` 映射。
///
/// <para><b>为什么颜色是数值而不是 hex 字符串</b>：生成的 C 是静态类型语言，
/// 三元 <c>cond ? "#ff0000" : "#00ff00"</c> 在 C 里两个分支类型不同（<c>int</c> vs <c>const char*</c>），
/// **直接编译不过**。若设计器走字符串、设备走数值，两端必然分叉。
/// 故统一约定：表达式返回 <b>24 位 RGB 数值</b>，设计时经 <see cref="ColorFromNumber"/> 变成显示色，
/// 生成期经 <c>lv_color_hex((uint32_t)(expr))</c> 变成 LVGL 颜色 —— 同一套数值规则。</para>
/// </summary>
public sealed record BindingVisualProperty(string PropertyName, BindingVisualKind Kind)
{
    /// <summary>文本颜色（<see cref="HmiControl.TextColor"/>）。</summary>
    public static readonly BindingVisualProperty TextColor =
        new(nameof(HmiControl.TextColor), BindingVisualKind.Color);

    /// <summary>背景颜色（<see cref="HmiControl.BgColor"/>）。</summary>
    public static readonly BindingVisualProperty BgColor =
        new(nameof(HmiControl.BgColor), BindingVisualKind.Color);

    /// <summary>可见性（<see cref="HmiControl.IsVisible"/>）。</summary>
    public static readonly BindingVisualProperty IsVisible =
        new(nameof(HmiControl.IsVisible), BindingVisualKind.Visibility);

    /// <summary>全部可绑定的视觉属性（顺序即属性面板下拉顺序）。</summary>
    public static IReadOnlyList<BindingVisualProperty> All { get; } =
        new[] { TextColor, BgColor, IsVisible };

    /// <summary>按属性名查注册项（大小写敏感，与 <see cref="VariableBindingData.PropertyName"/> 同一口径）。</summary>
    public static BindingVisualProperty? Find(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return null;
        foreach (var p in All)
        {
            if (string.Equals(p.PropertyName, propertyName, StringComparison.Ordinal)) return p;
        }
        return null;
    }

    /// <summary>判定属性名是否属于「视觉属性」—— 即该绑定应由视觉通路而非值通路消费。</summary>
    public static bool IsVisual(string? propertyName) => Find(propertyName) is not null;

    /// <summary>
    /// 绑定的外观值 → 设计器颜色字符串（<c>#RRGGBB</c>）。
    ///
    /// 与生成代码 <c>lv_color_hex((uint32_t)(expr) &amp; 0xFFFFFFu)</c> **同规则**：
    /// 高位与负数一律按位截断，保证同一表达式在设计器与设备上落到同一个颜色。
    /// 非有限值（NaN/Infinity）返回 null，由调用方回退到静态配置色。
    /// </summary>
    public static string? ColorFromNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return null;

        var rgb = unchecked((uint)(long)value) & 0xFFFFFFu;
        return "#" + rgb.ToString("X6", CultureInfo.InvariantCulture);
    }
}
