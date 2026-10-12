namespace HmiEditor.Core.Models;

/// <summary>
/// SVG 形状类型。
/// </summary>
public enum SvgShapeKind
{
    Rect,
    Circle,
    Ellipse,
    Line,
    Polyline,
    Polygon,
    Path
}

/// <summary>
/// 渐变类型。
/// </summary>
public enum SvgGradientKind
{
    Linear,
    Radial
}

/// <summary>
/// 渐变停靠点（SVG <c>&lt;stop&gt;</c>）。
/// </summary>
public sealed class SvgGradientStop
{
    /// <summary>位置（已归一化到 0..1；SVG 里的 "50%" 由解析器换算）。</summary>
    public double Offset { get; set; }

    /// <summary>颜色（#RRGGBB）；"none" 表示该处透明。</summary>
    public string Color { get; set; } = "#000000";

    public SvgGradientStop ShallowCopy() => (SvgGradientStop)MemberwiseClone();
}

/// <summary>
/// SVG 渐变定义（<c>&lt;linearGradient&gt;</c> / <c>&lt;radialGradient&gt;</c>）。
///
/// <para><b>坐标语义由 <see cref="UserSpace"/> 决定</b>，消费方不得自行猜测：
/// <c>false</c>（objectBoundingBox，SVG 默认）⇒ <see cref="Coords"/> 是**包围盒相对值**（0..1）；
/// <c>true</c>（userSpaceOnUse）⇒ <see cref="Coords"/> 是 **viewBox 绝对坐标**。
/// 两者的换算必须走 <c>SvgGradientGeometry</c>，不要在两处各写一份。</para>
///
/// <para><b>为什么形状上要存完整定义而不是一个 id</b>：<c>SvgShapes</c> 是挂在
/// <see cref="HmiControl"/> 上的扁平形状列表，控件被复制/展开/存盘时带不走 <c>&lt;defs&gt;</c>；
/// 只留 id 会得到一批永远解析不出来的悬空引用。</para>
/// </summary>
public sealed class SvgGradient
{
    public SvgGradientKind Kind { get; set; }

    /// <summary>
    /// 几何坐标。
    /// Linear: <c>[x1, y1, x2, y2]</c>；
    /// Radial: <c>[cx, cy, r]</c>，当焦点与圆心不同时为 <c>[cx, cy, r, fx, fy]</c>。
    /// </summary>
    public double[] Coords { get; set; } = Array.Empty<double>();

    /// <summary>
    /// <c>true</c> = <c>gradientUnits="userSpaceOnUse"</c>（<see cref="Coords"/> 为 viewBox 坐标）；
    /// <c>false</c> = objectBoundingBox（<see cref="Coords"/> 为 0..1 相对值）。
    /// </summary>
    public bool UserSpace { get; set; }

    /// <summary>
    /// 渐变自身的仿射（<c>gradientTransform</c>，<c>[a,b,c,d,e,f]</c>）；null 表示无。
    /// 按 SVG 语义**叠加在包围盒映射之后**（userSpace 时即直接作用于 viewBox 坐标）。
    /// </summary>
    public double[]? Transform { get; set; }

    /// <summary>停靠点，按 <see cref="SvgGradientStop.Offset"/> 升序。</summary>
    public List<SvgGradientStop> Stops { get; set; } = new();

    /// <summary>
    /// 拷贝 —— <see cref="Coords"/> / <see cref="Transform"/> / <see cref="Stops"/> 全部换成独立实例。
    /// 三个引用型字段**都是可变的**，任何一处共享都会让「改副本」改到源上。
    /// </summary>
    public SvgGradient ShallowCopy()
    {
        var clone = (SvgGradient)MemberwiseClone();
        clone.Coords = (double[])Coords.Clone();
        clone.Transform = Transform == null ? null : (double[])Transform.Clone();
        clone.Stops = Stops.Select(s => s.ShallowCopy()).ToList();
        return clone;
    }
}

/// <summary>
/// 单个 SVG 形状 —— 类型化描述,足够设计画布渲染与 LVGL CodeGen 转换。
/// 坐标均为 SVG viewBox 坐标系原始值（**未**应用 <see cref="Transform"/>）。
/// </summary>
public class SvgShape
{
    /// <summary>形状类型。</summary>
    public SvgShapeKind Kind { get; set; }

    /// <summary>
    /// 形状几何参数（随 Kind 含义不同）：
    /// Rect     : x, y, width, height (, rx, ry)
    /// Circle   : cx, cy, r
    /// Ellipse  : cx, cy, rx, ry
    /// Line     : x1, y1, x2, y2
    /// Polyline / Polygon : x0, y0, x1, y1, ...
    /// Path     : 为空（几何在 <see cref="PathData"/>）
    /// </summary>
    public double[] Points { get; set; } = Array.Empty<double>();

    /// <summary>Path 命令原文（Kind == Path 时有效，如 "M10 20 L30 40 Z"）。</summary>
    public string? PathData { get; set; }

    /// <summary>填充色（"#RRGGBB"/"#RGB"/命名色）；"none" 表示无填充。</summary>
    public string? Fill { get; set; }

    /// <summary>描边色；"none" 或 null 表示无描边。</summary>
    public string? Stroke { get; set; }

    /// <summary>描边宽度（viewBox 单位）。</summary>
    public double StrokeWidth { get; set; } = 1.0;

    /// <summary>
    /// 填充渐变；null 表示纯色填充（用 <see cref="Fill"/>）。
    ///
    /// <para><b>非 null 时 <see cref="Fill"/> 仍然有效且必须保留</b>：它是该渐变的**降级色**
    /// （取 50% 处颜色）。LVGL 侧只有轴对齐矩形/圆能画真渐变，其余形状与所有描边都必须
    /// 退回这个颜色 —— 把 <c>Fill</c> 置空会让这些情形直接变成"没有填充"。</para>
    /// </summary>
    public SvgGradient? FillGradient { get; set; }

    /// <summary>描边渐变；null 表示纯色描边。非 null 时 <see cref="Stroke"/> 是降级色（同 Fill 的约定）。</summary>
    public SvgGradient? StrokeGradient { get; set; }

    /// <summary>
    /// 本形状的 2×3 仿射 <c>[a,b,c,d,e,f]</c>（SVG <c>matrix()</c> 顺序，作用在 viewBox 坐标上）；
    /// null 表示恒等（绝大多数形状）。
    ///
    /// <para><b>为什么不在这里烘焙进 <see cref="Points"/></b>：旋转/斜切会把 <c>Rect</c> 变成非轴对齐四边形、
    /// 把 <c>Circle</c> 变成旋转椭圆，而 <see cref="Kind"/> 只能表达轴对齐语义。保留矩阵后，
    /// 两个消费方（画布渲染、CodeGen）都用 <c>SvgAffine</c> 的同一份实现去应用它，
    /// 由 <c>SvgAffine.IsAxisAligned</c> 这一个判据决定"要不要把几何提升成多边形"。</para>
    /// </summary>
    public double[]? Transform { get; set; }

    /// <summary>
    /// 拷贝 —— 引用型字段（<see cref="Points"/>、<see cref="Transform"/>、两个渐变）**全部**换成独立实例，
    /// 其余为值类型/字符串（字符串不可变，可共享）。
    ///
    /// <para><b>不要改回「唯一的引用型字段是 Points」那种写法</b>：底子是
    /// <c>MemberwiseClone</c>（字段新增时自动跟上，这是它相对手工列字段的唯一优势），
    /// 但**每个引用型字段都必须在这里补一次显式克隆**。漏一个的后果不是这里报错，
    /// 而是 Faceplate 展开 / 克隆后"改一个动两个"——见 <c>FaceplateExpanderTests</c> 的属性比对守卫。</para>
    /// </summary>
    public SvgShape ShallowCopy()
    {
        var clone = (SvgShape)MemberwiseClone();
        clone.Points = (double[])Points.Clone();
        clone.Transform = Transform == null ? null : (double[])Transform.Clone();
        clone.FillGradient = FillGradient?.ShallowCopy();
        clone.StrokeGradient = StrokeGradient?.ShallowCopy();
        return clone;
    }
}

/// <summary>
/// 解析后的 SVG 文档 —— 形状列表 + viewBox/尺寸元数据。
/// </summary>
public class SvgDocument
{
    public double ViewBoxX { get; set; }
    public double ViewBoxY { get; set; }
    public double ViewBoxWidth { get; set; } = 100;
    public double ViewBoxHeight { get; set; } = 100;

    /// <summary>文档声明的宽度（px）；缺失时取 viewBox 宽度。</summary>
    public double Width { get; set; }

    /// <summary>文档声明的高度（px）；缺失时取 viewBox 高度。</summary>
    public double Height { get; set; }

    public List<SvgShape> Shapes { get; set; } = new();
}
