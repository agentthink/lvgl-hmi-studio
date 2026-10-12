namespace HmiEditor.Core.Models;

/// <summary>
/// 表格列宽解析辅助 —— 把 <see cref="HmiControl.ColumnWidths"/> 字符串解析为逐列宽度语义，
/// 供代码生成（C）与设计器渲染（像素）共用同一套解析，避免两侧语义不一致。
/// </summary>
public static class TableLayoutHelper
{
    /// <summary>列宽值：该列按内容自适应（对应 LVGL LV_COORD_MAX）。</summary>
    public const double Auto = -1;

    /// <summary>列宽值：该列均分剩余空间（未显式指定时的默认）。</summary>
    public const double Fill = 0;

    /// <summary>
    /// 解析列宽规格字符串。
    /// 空 / 全空 → 所有列 Fill（均分）；"auto" → Auto；数值 → 绝对 px。
    /// 返回长度恰为 <paramref name="colCount"/>（不足补 Fill，超出截断）。
    /// </summary>
    public static IReadOnlyList<double> ParseColumnWidths(string? spec, int colCount)
    {
        var result = new double[Math.Max(1, colCount)];
        for (var i = 0; i < result.Length; i++) result[i] = Fill;

        if (string.IsNullOrWhiteSpace(spec)) return result;

        var parts = spec.Split(',', StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts.Length && i < result.Length; i++)
        {
            var p = parts[i].ToLowerInvariant();
            if (p is "auto" or "自适应")
                result[i] = Auto;
            else if (double.TryParse(p, out var w) && w > 0)
                result[i] = w;
            else
                result[i] = Fill;
        }
        return result;
    }
}
