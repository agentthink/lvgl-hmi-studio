using System.Globalization;
using HmiEditor.Core.Models;

namespace HmiEditor.Core.Design;

/// <summary>绑定值的显示/判真类别（设计器、模拟器、固件三端共用）。</summary>
public enum BindingDisplayKind
{
    /// <summary>布尔：ON/OFF。</summary>
    Bool,

    /// <summary>整数：四舍五入。</summary>
    Int,

    /// <summary>定点小数：按 <see cref="BindingSemantics.Decimals"/> 位。</summary>
    Float,

    /// <summary>文本：固定 2 位小数。</summary>
    Text,
}

/// <summary>
/// 绑定值语义 —— 设计器预览、PC 模拟器、固件 <c>.so</c> 生成代码的**唯一事实源**。
///
/// 背景（2026-09-13 审查 P1-1）：三端原先各写一套求值规则，导致
/// <c>ValueDisplayType / ValueScaleFactor / ValueDecimalPlaces / ValuePrefix / ValueSuffix</c>
/// 在设计器生效、在生成代码里只以注释出现（设计器显示 12.34，设备显示 12.3），
/// 布尔判定也不一致（设计器“非零即 ON” vs 固件 <c>&gt; 0.5f</c>）。
///
/// 本类型把「缩放 → 小数位 → 前后缀 → 判真」收敛到一处：
/// <list type="bullet">
///   <item><see cref="FormatDisplay"/> —— 设计器预览字符串；</item>
///   <item><see cref="CFormatLiteral"/> —— 生成代码里 printf 格式串的字面量部分（已转义 <c>%</c>）；</item>
///   <item><see cref="CConversion"/> —— 数值转换说明（Bool 无，用 ON/OFF 文本表达）；</item>
///   <item><see cref="IsOn"/> —— 布尔判定的唯一定义。</item>
/// </list>
/// </summary>
public sealed record BindingSemantics(
    BindingDisplayKind Kind,
    int Decimals,
    double ScaleFactor,
    string? Prefix,
    string? Suffix)
{
    /// <summary>
    /// 布尔判定阈值：<c>!= BoolThreshold</c> 视为 ON。
    /// 取 0 表示“非零即 ON”，与 <see cref="ValueDisplayType.Bool"/> 的既有文档语义一致。
    /// </summary>
    public const double BoolThreshold = 0.0;

    /// <summary>按控件配置 + 变量数据类型解析显示语义。</summary>
    /// <param name="control">目标控件（只读）。</param>
    /// <param name="variableDataType">绑定变量的数据类型（如 "Float"/"Bool"/"Int16"），用于 Auto 推断，可为 null。</param>
    /// <param name="variableUnit">绑定变量的单位（如 "℃"/"%"）；控件 ValueSuffix 为空时作为后缀兜底。</param>
    public static BindingSemantics For(HmiControl control, string? variableDataType, string? variableUnit = null)
    {
        ArgumentNullException.ThrowIfNull(control);

        var displayType = control.ValueDisplayType;
        if (displayType == ValueDisplayType.Auto)
            displayType = InferFromDataType(variableDataType);

        var decimals = Math.Clamp(control.ValueDecimalPlaces, 0, BindingFormat.MaxDecimals);
        var scale = control.ValueScaleFactor;
        var prefix = control.ValuePrefix;
        // 后缀优先取控件配置；未配置时回退变量单位（生成代码历史行为，设计器现在同步）
        var suffix = !string.IsNullOrEmpty(control.ValueSuffix) ? control.ValueSuffix : variableUnit;

        return displayType switch
        {
            ValueDisplayType.Bool => new(BindingDisplayKind.Bool, 0, 1.0, prefix, suffix),
            // 缩放为 1 → 整除显示；缩放引入小数 → 按用户小数位显示（避免取整丢值）
            ValueDisplayType.Int => new(BindingDisplayKind.Int, scale == 1.0 ? 0 : decimals, scale, prefix, suffix),
            ValueDisplayType.Float => new(BindingDisplayKind.Float, decimals, scale, prefix, suffix),
            ValueDisplayType.String => new(BindingDisplayKind.Text, 2, scale, prefix, suffix),
            _ => ForControlType(control.Type, scale, prefix, suffix),
        };
    }

    /// <summary>Auto 且无法从变量类型推断时的控件类型兜底规则（与历史设计器行为一致）。</summary>
    private static BindingSemantics ForControlType(HmiControlType type, double scale, string? prefix, string? suffix)
        => type switch
        {
            HmiControlType.Led or HmiControlType.Switch or HmiControlType.CheckBox
                or HmiControlType.Valve => new(BindingDisplayKind.Bool, 0, 1.0, prefix, suffix),

            HmiControlType.Gauge or HmiControlType.Bar or HmiControlType.Knob
                or HmiControlType.Tank => new(BindingDisplayKind.Float, 1, scale, prefix, suffix),

            HmiControlType.Slider or HmiControlType.Dropdown => new(BindingDisplayKind.Int, 0, scale, prefix, suffix),

            _ => new(BindingDisplayKind.Float, 2, scale, prefix, suffix),
        };

    /// <summary>非零即 ON。</summary>
    public bool IsOn(double rawValue) => rawValue != BoolThreshold;

    /// <summary>应用缩放系数（1.0 时原样返回，避免无意义乘法）。</summary>
    public double ApplyScale(double rawValue) => ScaleFactor == 1.0 ? rawValue : rawValue * ScaleFactor;

    /// <summary>设计器预览字符串（与生成代码的格式保持一致，使用 InvariantCulture 保证跨区域确定）。</summary>
    public string FormatDisplay(double rawValue)
    {
        if (double.IsNaN(rawValue) || double.IsInfinity(rawValue)) return "---";

        string body;
        switch (Kind)
        {
            case BindingDisplayKind.Bool:
                body = IsOn(rawValue) ? "ON" : "OFF";
                break;
            case BindingDisplayKind.Int:
            {
                var scaled = ApplyScale(rawValue);
                if (Decimals == 0)
                {
                    // 纯整数显式类型：四舍五入（与生成代码的 %.0f 一致）
                    body = ((long)Math.Round(scaled)).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    // 缩放引入小数：恰为整数时显示整数，否则保留小数，避免取整丢值
                    var rounded = Math.Round(scaled);
                    body = Math.Abs(scaled - rounded) < 1e-9
                        ? ((long)rounded).ToString(CultureInfo.InvariantCulture)
                        : scaled.ToString("0.######", CultureInfo.InvariantCulture);
                }
                break;
            }
            case BindingDisplayKind.Text:
                body = ApplyScale(rawValue).ToString("F2", CultureInfo.InvariantCulture);
                break;
            default:
                body = ApplyScale(rawValue).ToString("F" + Decimals, CultureInfo.InvariantCulture);
                break;
        }

        if (string.IsNullOrEmpty(Prefix) && string.IsNullOrEmpty(Suffix)) return body;
        return $"{Prefix}{body}{Suffix}";
    }

    /// <summary>
    /// printf 数值转换说明（不含前后缀）。Bool 没有数值表达（用 ON/OFF 文本），返回 null。
    /// </summary>
    public string? CConversion => Kind switch
    {
        BindingDisplayKind.Bool => null,
        // Int 在缩放为 1 时 Decimals=0（整数），缩放引入小数时保留用户小数位，避免取整丢值
        BindingDisplayKind.Int => "%." + Decimals + "f",
        BindingDisplayKind.Text => "%.2f",
        _ => "%." + Decimals + "f",
    };

    /// <summary>
    /// 生成代码中 printf 格式串的字面量部分：前后缀 + 转换说明。
    /// 前后缀里的 <c>%</c> 会转义为 <c>%%</c>（单位写成 "%" 时旧实现产出非法的 "%.1f%"）。
    /// </summary>
    public string CFormatLiteral()
    {
        var conv = CConversion ?? string.Empty;
        return EscapeFormatText(Prefix) + conv + EscapeFormatText(Suffix);
    }

    /// <summary>从变量数据类型推断显示类型（仅识别明确类型；未知返回 Auto 交由控件类型兜底）。</summary>
    public static ValueDisplayType InferFromDataType(string? variableDataType)
    {
        if (string.IsNullOrEmpty(variableDataType)) return ValueDisplayType.Auto;
        var t = variableDataType.ToLowerInvariant();
        if (t == "bool") return ValueDisplayType.Bool;
        if (t is "int8" or "uint8" or "int16" or "uint16" or "int32" or "uint32") return ValueDisplayType.Int;
        if (t is "float32" or "float64" or "float" or "double") return ValueDisplayType.Float;
        if (t == "string") return ValueDisplayType.String;
        return ValueDisplayType.Auto;
    }

    /// <summary>转义 printf 格式串中的字面量文本（C 转义 + '%' → '%%'）。</summary>
    private static string EscapeFormatText(string? s)
        => (s ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("%", "%%");
}
