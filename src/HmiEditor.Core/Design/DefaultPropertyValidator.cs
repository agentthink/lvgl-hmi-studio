using HmiEditor.Core.Models;

namespace HmiEditor.Core.Design;

/// <summary>
/// 默认属性校验器：数值范围 + 名称/颜色格式校验（复用静态 PropertyValidator 的规则常量）。
/// 属性面板默认注入本实现；插件可替换或扩展。
/// </summary>
public sealed class DefaultPropertyValidator : IPropertyValidator
{
    public string? Validate(HmiControl control, string propertyName, object? value)
    {
        switch (propertyName)
        {
            case nameof(HmiControl.FontSize):
                if (value is int fs && (fs < 0 || fs > 512)) return "字号需在 0-512 之间（0=默认 14）";
                break;
            case nameof(HmiControl.Opacity):
                if (value is double op && (op < 0 || op > 100)) return "不透明度需在 0-100 之间";
                break;
            case nameof(HmiControl.Brightness):
                if (value is int br && (br < 0 || br > 255)) return "亮度需在 0-255 之间";
                break;
            case nameof(HmiControl.ValueDecimalPlaces):
                if (value is int dp && (dp < 0 || dp > 6)) return "小数位需在 0-6 之间";
                break;
            case nameof(HmiControl.RowCount):
            case nameof(HmiControl.ColumnCount):
                if (value is int rc && (rc < 1 || rc > 100)) return "行列数需在 1-100 之间";
                break;
            case nameof(HmiControl.MajorTickCount):
                if (value is int tc && (tc < 2 || tc > 100)) return "刻度数需在 2-100 之间";
                break;
            case nameof(HmiControl.SpinSpeed):
                if (value is int sp && (sp < 100 || sp > 100000)) return "转速需在 100-100000 之间";
                break;
            case nameof(HmiControl.TankTickCount):
                if (value is int tt && (tt < 0 || tt > 50)) return "罐体刻度数需在 0-50 之间";
                break;
            case nameof(HmiControl.Name):
                if (!PropertyValidator.IsValidName(value as string)) return "名称不能为空且不超过 100 字符";
                break;
            case nameof(HmiControl.BgColor):
            case nameof(HmiControl.TextColor):
            case nameof(HmiControl.BorderColor):
            case nameof(HmiControl.IndicatorColor):
                if (!PropertyValidator.IsValidColor(value as string)) return "颜色格式应为 #RGB / #RRGGBB / #RRGGBBAA";
                break;
        }
        return null;
    }
}
