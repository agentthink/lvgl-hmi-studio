namespace HmiEditor.Core.Design;

/// <summary>
/// 全局默认调色板 —— 设计器渲染、代码生成、主题模型三方共用的单一颜色来源。
/// HmiTheme 的属性默认值引用本类（避免三套颜色语义漂移）；
/// App 层 DesignPalette 从这里派生 Avalonia Color。
/// </summary>
public static class PaletteDefaults
{
    // ── 主题派生色（HmiTheme 默认值与本类同源）──
    /// <summary>主色：按钮/滑块/开关激活/勾选/Gauge 指示</summary>
    public const string Primary = "#2563eb";
    /// <summary>次色：导出/确认操作</summary>
    public const string Secondary = "#059669";
    /// <summary>面板/控件中性深灰背景（未设 BgColor 时的兜底）</summary>
    public const string Surface = "#1a2332";
    /// <summary>画布背景</summary>
    public const string Background = "#131c31";
    /// <summary>文本主色（设计器文本兜底统一用此，替代 Colors.White）</summary>
    public const string Text = "#e2e8f0";
    /// <summary>禁用/次要文本色</summary>
    public const string TextSecondary = "#7c8aa0";
    /// <summary>边框/表格网格线兜底</summary>
    public const string Border = "#334155";
    /// <summary>成功色：开关打开/勾选/LED 亮</summary>
    public const string Success = "#22c55e";
    /// <summary>警告色</summary>
    public const string Warning = "#f59e0b";
    /// <summary>错误色：阀门关闭</summary>
    public const string Error = "#ef4444";

    // ── 设计器专色（无主题对应，固定值，供设计器/生成器兜底共用）──
    /// <summary>指示器/箭头/高光亮蓝兜底</summary>
    public const string SkyAccent = "#38bdf8";
    /// <summary>输入类控件/表格单元格中性深色兜底</summary>
    public const string Slate800 = "#1e293b";
    /// <summary>管道兜底</summary>
    public const string Slate500 = "#64748b";
    /// <summary>次级文本/刻度/占位文本色</summary>
    public const string Slate400 = "#94a3b8";
    /// <summary>深描边（滑钮/开关外圈）</summary>
    public const string Slate900 = "#0f172a";
    /// <summary>勾选框/键盘未选中底</summary>
    public const string CheckBoxOff = "#1e283c";
    /// <summary>开关滑钮未选中色</summary>
    public const string OffKnob = "#c8d2e1";
    /// <summary>键盘键帽色</summary>
    public const string KeyboardKey = "#374155";
    /// <summary>TabView 背景</summary>
    public const string TabBg = "#192332";
    /// <summary>TabView 内容区</summary>
    public const string TabContent = "#141c2a";
    /// <summary>LED 灭态外圈色</summary>
    public const string LedOff = "#505a6e";
    /// <summary>SVG 无形状占位描边</summary>
    public const string SvgPlaceholder = "#50648c";
    /// <summary>罐体液位/管道流向兜底</summary>
    public const string TankLiquid = "#0ea5e9";
    /// <summary>Line 控件默认线色（保持纯白，与生成器一致）</summary>
    public const string LineFallback = "#ffffff";
    /// <summary>屏幕边界线</summary>
    public const string ScreenBorder = "#3c465a";
    /// <summary>屏幕背景兜底</summary>
    public const string ScreenBgFallback = "#191e2d";
}
