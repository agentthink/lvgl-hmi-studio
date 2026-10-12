using Avalonia.Media;
using HmiEditor.Core.Design;

namespace HmiEditor.App.Design;

/// <summary>
/// 设计器渲染层调色板（Avalonia Color）。
/// 值全部来自 PaletteDefaults（与主题模型、代码生成器同源），
/// 避免设计器 RGB 兜底色 / 生成器 hex 兜底色 / HmiTheme 三套颜色互相脱节。
/// </summary>
public static class DesignPalette
{
    // ── 主题派生色 ──
    /// <summary>主色（按钮/滑块/开关激活/勾选/Gauge 指示兜底）</summary>
    public static Color Primary => Color.Parse(PaletteDefaults.Primary);
    /// <summary>次色</summary>
    public static Color Secondary => Color.Parse(PaletteDefaults.Secondary);
    /// <summary>面板/控件中性深灰背景兜底</summary>
    public static Color Surface => Color.Parse(PaletteDefaults.Surface);
    /// <summary>画布背景</summary>
    public static Color Background => Color.Parse(PaletteDefaults.Background);
    /// <summary>文本主色兜底（替代 Colors.White）</summary>
    public static Color Text => Color.Parse(PaletteDefaults.Text);
    /// <summary>次要文本色</summary>
    public static Color TextSecondary => Color.Parse(PaletteDefaults.TextSecondary);
    /// <summary>边框/网格线兜底</summary>
    public static Color Border => Color.Parse(PaletteDefaults.Border);
    /// <summary>成功色（开关开/勾选/LED 亮/预览横幅）</summary>
    public static Color Success => Color.Parse(PaletteDefaults.Success);
    /// <summary>警告色</summary>
    public static Color Warning => Color.Parse(PaletteDefaults.Warning);
    /// <summary>错误色（阀门关闭）</summary>
    public static Color Error => Color.Parse(PaletteDefaults.Error);

    // ── 设计器专色 ──
    /// <summary>指示器/箭头/高光亮蓝兜底</summary>
    public static Color SkyAccent => Color.Parse(PaletteDefaults.SkyAccent);
    /// <summary>输入类控件/表格单元格中性深色兜底</summary>
    public static Color Slate800 => Color.Parse(PaletteDefaults.Slate800);
    /// <summary>管道兜底</summary>
    public static Color Slate500 => Color.Parse(PaletteDefaults.Slate500);
    /// <summary>次级文本/刻度/占位文本色</summary>
    public static Color Slate400 => Color.Parse(PaletteDefaults.Slate400);
    /// <summary>深描边</summary>
    public static Color Slate900 => Color.Parse(PaletteDefaults.Slate900);
    /// <summary>勾选框/键盘未选中底</summary>
    public static Color CheckBoxOff => Color.Parse(PaletteDefaults.CheckBoxOff);
    /// <summary>开关滑钮未选中色</summary>
    public static Color OffKnob => Color.Parse(PaletteDefaults.OffKnob);
    /// <summary>键盘键帽色</summary>
    public static Color KeyboardKey => Color.Parse(PaletteDefaults.KeyboardKey);
    /// <summary>TabView 背景</summary>
    public static Color TabBg => Color.Parse(PaletteDefaults.TabBg);
    /// <summary>TabView 内容区</summary>
    public static Color TabContent => Color.Parse(PaletteDefaults.TabContent);
    /// <summary>LED 灭态外圈色</summary>
    public static Color LedOff => Color.Parse(PaletteDefaults.LedOff);
    /// <summary>SVG 无形状占位描边</summary>
    public static Color SvgPlaceholder => Color.Parse(PaletteDefaults.SvgPlaceholder);
    /// <summary>罐体液位/管道流向兜底</summary>
    public static Color TankLiquid => Color.Parse(PaletteDefaults.TankLiquid);
    /// <summary>Line 控件默认线色</summary>
    public static Color LineFallback => Color.Parse(PaletteDefaults.LineFallback);
    /// <summary>屏幕边界线</summary>
    public static Color ScreenBorder => Color.Parse(PaletteDefaults.ScreenBorder);
    /// <summary>屏幕背景兜底</summary>
    public static Color ScreenBgFallback => Color.Parse(PaletteDefaults.ScreenBgFallback);
}
