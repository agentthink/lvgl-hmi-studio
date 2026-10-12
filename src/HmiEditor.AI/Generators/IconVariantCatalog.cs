namespace HmiEditor.AI.Generators;

/// <summary>
/// 图标语义分类标准 —— 参考 iconfont.cn 的图标库分类体系,
/// 将 HMI 图标划分为 8 大语义类别, 每类有明确的视觉语言与常见元素。
/// 分类决定了图标应表达的核心语义和默认构图方式。
/// </summary>
public enum IconCategory
{
    /// <summary>设备类: 泵/电机/阀门/压缩机/传感器等实物设备</summary>
    Device,
    /// <summary>动作类: 启动/停止/复位/切换/确认等操作动作</summary>
    Action,
    /// <summary>状态类: 运行/报警/待机/故障/电量等运行状态</summary>
    Status,
    /// <summary>导航类: 返回/前进/首页/菜单/设置等界面导航</summary>
    Navigation,
    /// <summary>过程图形类: 管道/液位/流量/压力/罐体等过程元素</summary>
    Process,
    /// <summary>通信类: 网络/无线/串口/连接/信号等通信元素</summary>
    Communication,
    /// <summary>安全类: 急停/锁定/防护/警告等安全元素</summary>
    Safety,
    /// <summary>环境类: 温度/湿度/光照/空气质量等环境参数</summary>
    Environment,
    /// <summary>通用类: 无法归入上述类别的通用图标</summary>
    General
}

/// <summary>
/// 图标视觉风格变体 —— 参考 iconfont.cn"同一图标多种风格"的设计原则。
/// 每种变体定义了明确的绘制规范(线宽/填充/几何/构图), 用于提示词注入,
/// 确保同一语义图标在不同变体下视觉差异明显。
/// </summary>
public enum IconStyleVariant
{
    /// <summary>线性风格: 细线描边勾勒轮廓, 内部镂空</summary>
    Linear,
    /// <summary>填充风格: 实心填充主体, 细节用镂空表现</summary>
    Filled,
    /// <summary>粗线框风格: 2.5-3 粗线描边, 适合远距离辨识</summary>
    Outline,
    /// <summary>圆角风格: 圆润转角, 亲和现代, 适合触摸屏</summary>
    Rounded,
    /// <summary>极简风格: 1-3 个基本几何形状, 高抽象</summary>
    Minimal,
    /// <summary>几何组合风格: 基础几何形状拼合, 构图对称</summary>
    Geometric,
    /// <summary>线面结合风格: 粗线主体 + 实心核心部件强调</summary>
    Accent
}

/// <summary>
/// 变体设计规范目录 —— 每种变体的绘制规范文本(注入提示词)与推荐生成温度。
/// 规范文本使用英文编写以确保 LLM 遵循(SVG 生成指令为英文)。
/// </summary>
public static class IconVariantCatalog
{
    /// <summary>获取变体的设计规范文本(注入 User 提示词)。</summary>
    public static string GetSpecText(IconStyleVariant variant) => variant switch
    {
        IconStyleVariant.Linear =>
            "[Design Style: LINEAR]\n" +
            "- Draw the icon body using thin outline strokes (stroke-width 1.5-2.0), leave the interior hollow.\n" +
            "- Internal details (holes, marks, ticks) use even thinner strokes or small open shapes.\n" +
            "- Lines should be smooth and continuous; avoid broken fragments.\n" +
            "- Keep a balanced silhouette centered in the 48x48 canvas, occupying about 70-80%.",
        IconStyleVariant.Filled =>
            "[Design Style: FILLED]\n" +
            "- Draw the icon body using solid fill shapes; use cutouts (transparent holes) for internal details.\n" +
            "- Shapes must be crisp and well-defined, with clean edges.\n" +
            "- Prefer symmetric solid blocks that read clearly at small size.\n" +
            "- The filled body should occupy about 70-85% of the canvas.",
        IconStyleVariant.Outline =>
            "[Design Style: BOLD OUTLINE]\n" +
            "- Use thick outline strokes (stroke-width 2.5-3.0) to draw the icon silhouette.\n" +
            "- Internal details use short thick strokes, dots, or small filled marks.\n" +
            "- High visual weight; designed to be recognized from a distance or at small sizes.\n" +
            "- Keep gaps between strokes at least 2px to avoid blobs.",
        IconStyleVariant.Rounded =>
            "[Design Style: ROUNDED]\n" +
            "- All corners and joints must be rounded (rounded rects, smooth arcs, no sharp points).\n" +
            "- Use medium strokes (stroke-width 1.8-2.2) with friendly, modern feel.\n" +
            "- Prefer circular/ellipse-based compositions and smooth curves.\n" +
            "- Ideal for touch interfaces; avoid sharp spikes and harsh angles.",
        IconStyleVariant.Minimal =>
            "[Design Style: MINIMAL]\n" +
            "- Express the meaning using only 1-3 basic geometric shapes (circle, rectangle, triangle, single line).\n" +
            "- Remove ALL non-essential details; maximum abstraction.\n" +
            "- Silhouette must be instantly recognizable from the minimal shapes alone.\n" +
            "- Centered, generous whitespace, occupying about 60-70% of the canvas.",
        IconStyleVariant.Geometric =>
            "[Design Style: GEOMETRIC]\n" +
            "- Compose the icon from basic geometric primitives (circles, rectangles, diamonds, triangles, line segments).\n" +
            "- Leave clear gaps between combined shapes; construction feels engineered.\n" +
            "- Prefer symmetric or balanced composition; grid-aligned where possible.\n" +
            "- Use thin strokes (1.5-2.0) or small fills for the primitives.",
        IconStyleVariant.Accent =>
            "[Design Style: LINE + FILL ACCENT]\n" +
            "- Draw the main body with medium outline strokes (stroke-width 2.0).\n" +
            "- Emphasize ONE core functional part with a solid fill (e.g., valve core, button face, indicator light).\n" +
            "- The filled accent must clearly stand out from the outlined body.\n" +
            "- Strong contrast between filled core and outlined shell.",
        _ => IconVariantCatalog.GetSpecText(IconStyleVariant.Linear)
    };

    /// <summary>获取变体的推荐生成温度 —— 简洁风格低温度保真, 复杂风格略高温度增多样。</summary>
    public static double GetTemperature(IconStyleVariant variant) => variant switch
    {
        IconStyleVariant.Linear => 0.4,
        IconStyleVariant.Filled => 0.5,
        IconStyleVariant.Outline => 0.45,
        IconStyleVariant.Rounded => 0.45,
        IconStyleVariant.Minimal => 0.35,
        IconStyleVariant.Geometric => 0.5,
        IconStyleVariant.Accent => 0.55,
        _ => 0.45
    };

    /// <summary>全部变体(按稳定顺序), 供轮转选择。</summary>
    public static readonly IconStyleVariant[] All =
    {
        IconStyleVariant.Linear,
        IconStyleVariant.Filled,
        IconStyleVariant.Outline,
        IconStyleVariant.Rounded,
        IconStyleVariant.Minimal,
        IconStyleVariant.Geometric,
        IconStyleVariant.Accent
    };
}
