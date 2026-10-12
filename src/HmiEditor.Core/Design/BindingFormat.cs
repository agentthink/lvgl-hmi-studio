namespace HmiEditor.Core.Design;

/// <summary>
/// 绑定值格式化共享常量与工具 —— 设计器预览与代码生成共用，防止格式化规则漂移。
/// </summary>
public static class BindingFormat
{
    /// <summary>绑定数值显示的最大小数位数（0-6）。</summary>
    public const int MaxDecimals = 6;

    /// <summary>
    /// 刻度数值格式化（Slider/Bar 刻度标签共用）：整数显示整数，小数最多 2 位
    /// （消除浮点累加误差，如 0.1×10 → "1"）。设计器与生成器一致。
    /// </summary>
    public static string FormatTick(double value)
        => Math.Abs(value - Math.Round(value)) < 0.005
            ? ((long)Math.Round(value)).ToString()
            : value.ToString("0.##");
}
