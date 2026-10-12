namespace HmiEditor.Core.Models;

/// <summary>
/// 屏幕方向。
/// </summary>
public enum ScreenOrientation
{
    /// <summary>横屏。</summary>
    Landscape,

    /// <summary>竖屏。</summary>
    Portrait,
}

/// <summary>
/// 触摸屏校准参数 —— 3x3 仿射变换矩阵，用于原始触摸坐标到屏幕坐标的映射。
/// 默认值为单位矩阵（无校准）。
/// </summary>
public class TouchCalibration
{
    /// <summary>矩阵元素 M11。</summary>
    public double M11 { get; set; } = 1;

    /// <summary>矩阵元素 M12。</summary>
    public double M12 { get; set; }

    /// <summary>矩阵元素 M13。</summary>
    public double M13 { get; set; }

    /// <summary>矩阵元素 M21。</summary>
    public double M21 { get; set; }

    /// <summary>矩阵元素 M22。</summary>
    public double M22 { get; set; } = 1;

    /// <summary>矩阵元素 M23。</summary>
    public double M23 { get; set; }

    /// <summary>矩阵元素 M31。</summary>
    public double M31 { get; set; }

    /// <summary>矩阵元素 M32。</summary>
    public double M32 { get; set; }

    /// <summary>矩阵元素 M33。</summary>
    public double M33 { get; set; } = 1;

    /// <summary>单位矩阵（无校准）。</summary>
    public static TouchCalibration Identity => new();
}

/// <summary>
/// 四边内边距 —— 用于安全区域避让（异形屏/刘海屏）。
/// </summary>
public class ScreenPadding
{
    /// <summary>左内边距（像素）。</summary>
    public double Left { get; set; }

    /// <summary>上内边距（像素）。</summary>
    public double Top { get; set; }

    /// <summary>右内边距（像素）。</summary>
    public double Right { get; set; }

    /// <summary>下内边距（像素）。</summary>
    public double Bottom { get; set; }

    /// <summary>默认构造，四边均为 0。</summary>
    public ScreenPadding() { }

    /// <summary>均匀四边内边距。</summary>
    public ScreenPadding(double uniform) => Left = Top = Right = Bottom = uniform;

    /// <summary>是否为零值（所有边均为 0）。</summary>
    public bool IsZero => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;
}

/// <summary>
/// 屏幕默认模板配置 —— 新建屏幕时的默认值模板。
/// <para>
/// <b>语义契约：本配置只影响「后续新建的屏幕」，不改动工程内已存在的屏幕。</b>
/// 能改动已存在屏幕尺寸的入口只有一个：工程设置「显示设置」分组里的
/// 「应用到全部屏幕」按钮（显式动作）。不要在保存工程设置时隐式覆盖已存在屏幕 ——
/// 那会把用户逐屏手工调过的尺寸静默改掉。
/// </para>
/// <para>
/// 想构造一个新屏幕时，不要直接 <c>new HmiScreen { Width = ..., Height = ... }</c>，
/// 走 <see cref="HmiEditor.Core.Services.ScreenFactory"/>，否则本配置的其余字段
/// （方向/自适应/背景色/色深）会再次被漏掉。
/// </para>
/// </summary>
public class ScreenDefaultsConfig
{
    /// <summary>
    /// 新建屏幕的默认宽度（像素）。默认对齐固件型号（T113 7寸 1024×600）。
    /// </summary>
    public int DefaultWidth { get; set; } = 1024;

    /// <summary>
    /// 新建屏幕的默认高度（像素）。默认对齐固件型号（T113 7寸 1024×600）。
    /// </summary>
    public int DefaultHeight { get; set; } = 600;

    /// <summary>
    /// 新建屏幕的默认色深 —— 存 <c>Hmi.Hardware.Contracts.ColorDepth</c> 的<b>成员名</b>字符串
    /// （<c>RGB565</c> / <c>ARGB8888</c> / <c>Indexed8</c>）。
    /// <para>
    /// <b>为什么是 string 而不是枚举：</b>老工程的 project.hmi 里已经存的是字符串，
    /// 改成枚举会改变 JSON 形态。合法值集合的唯一来源是那个枚举本身 ——
    /// 面板候选项由 <c>Enum.GetNames</c> 生成，不要再手写一份。
    /// </para>
    /// <para>
    /// <b>全局一共四处「色深」，别拿错：</b>
    /// <list type="table">
    ///   <item><term>本字段 / <c>Hmi.Hardware.Contracts.ColorDepth</c></term>
    ///     <description>屏幕默认色深 + 设备支持的显示色深（枚举，3 个成员）。</description></item>
    ///   <item><term><c>HmiEditor.Core.Services.ImageColorDepth</c></term>
    ///     <description>图片导出位深（枚举，4 个成员，含 RGB888/Indexed256/Indexed16，无 ARGB8888）。
    ///     语义不同，曾与本字段同名的那个枚举已因此改名。</description></item>
    ///   <item><term><c>Hmi.Hardware.Contracts.LvglConfig.DefaultColorDepth</c></term>
    ///     <description>LVGL 编译期 <c>LV_COLOR_DEPTH</c>（int，16 / 32）。</description></item>
    ///   <item><term><c>AssetItem.ColorDepth</c></term>
    ///     <description>单个资源的位深（int，32 / 24 / 0）。</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public string DefaultColorDepth { get; set; } = "RGB565";

    /// <summary>
    /// 新建屏幕的默认方向。
    /// </summary>
    public ScreenOrientation DefaultOrientation { get; set; } = ScreenOrientation.Landscape;

    /// <summary>
    /// 新建屏幕的默认自适应模式。
    /// </summary>
    public ScreenAdaptiveMode DefaultAdaptiveMode { get; set; } = ScreenAdaptiveMode.Fit;

    /// <summary>
    /// 新建屏幕的默认背景色。null 则使用 Theme.BackgroundColor。
    /// </summary>
    public string? DefaultBgColor { get; set; } = "#131c31";

    /// <summary>
    /// 屏幕 DPI 缩放因子。用于高 DPI 屏幕的坐标换算。
    /// </summary>
    public double DpiScale { get; set; } = 1.0;

    /// <summary>
    /// 触摸屏校准参数。存储 3x3 仿射变换矩阵。
    /// </summary>
    public TouchCalibration TouchCalibration { get; set; } = TouchCalibration.Identity;

    /// <summary>
    /// 安全区域内边距（用于异形屏/刘海屏避让）。单位：像素。
    /// null 表示无安全区域。
    /// </summary>
    public ScreenPadding? SafeAreaPadding { get; set; }
}