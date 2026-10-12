namespace HmiEditor.Core.Models;

/// <summary>
/// 控件值的显示类型 —— 决定绑定变量后如何格式化显示值。
/// Auto 表示按变量数据类型自动判断；其余为强制显示类型。
/// </summary>
public enum ValueDisplayType
{
    /// <summary>按变量数据类型自动判断显示格式。</summary>
    Auto = 0,

    /// <summary>布尔：ON/OFF（非零为 ON）。</summary>
    Bool,

    /// <summary>整数：四舍五入显示。</summary>
    Int,

    /// <summary>小数：按保留位数显示。</summary>
    Float,

    /// <summary>字符串：直接显示值文本。</summary>
    String
}
