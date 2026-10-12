namespace HmiEditor.Core.Models;

/// <summary>
/// 表格单元格数据（Table 控件）。
/// 稀疏存储：只保存被配置过的单元格，未配置的单元格留空。
/// </summary>
public class TableCellData
{
    /// <summary>行索引（0 起）。</summary>
    public int Row { get; set; }

    /// <summary>列索引（0 起）。</summary>
    public int Col { get; set; }

    /// <summary>
    /// 静态文本 —— VariableId 为空时生效，单元格显示固定字符串。
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// 绑定变量 ID —— 非空时单元格内容随该变量值实时更新（优先于 Text）。
    /// </summary>
    public string? VariableId { get; set; }

    /// <summary>值前缀（绑定变量显示时，如 "温度: "）。</summary>
    public string? Prefix { get; set; }

    /// <summary>值后缀（绑定变量显示时，如 " ℃"）。</summary>
    public string? Suffix { get; set; }

    /// <summary>小数显示位数（0-6，绑定变量为数值时生效）。默认 1。</summary>
    public int DecimalPlaces { get; set; } = 1;

    /// <summary>是否为绑定单元格（VariableId 非空且非表达式占位）。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsBound => !string.IsNullOrEmpty(VariableId) && !VariableId.Contains("{{");

    /// <summary>
    /// 浅拷贝 —— 本类字段全是字符串/数值（无嵌套引用），浅拷贝即等价深拷贝。
    /// 用 <c>MemberwiseClone</c> 而非手工列字段：后者**必然随新增字段漂移**。
    /// </summary>
    public TableCellData ShallowCopy() => (TableCellData)MemberwiseClone();
}
