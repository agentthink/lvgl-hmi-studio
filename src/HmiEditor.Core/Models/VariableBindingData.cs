namespace HmiEditor.Core.Models;

/// <summary>
/// 变量绑定方向。
/// </summary>
public enum BindingDirection { ReadOnly, WriteOnly, ReadWrite }

/// <summary>
/// 绑定源类型。
/// </summary>
public enum BindingSourceKind
{
    /// <summary>直接绑定单个变量（原行为，默认值保证向后兼容）。</summary>
    Variable,

    /// <summary>绑定表达式（如 "temp > 100 ? 1 : 0"），由 ExpressionEngine 求值。</summary>
    Expression
}

/// <summary>
/// 控件属性到变量的绑定关系 —— 联动模型的核心数据结构。
/// 一个控件可拥有多个 VariableBindingData 条目，每个绑定到不同属性/变量。
/// </summary>
public class VariableBindingData
{
    /// <summary>控件属性名（如 "Text", "Value", "IsChecked"）。</summary>
    public string PropertyName { get; set; } = "";

    /// <summary>变量标识（优先 Variable.Id，兼容旧项目用 Name）。</summary>
    public string VariableId { get; set; } = "";

    /// <summary>绑定方向。</summary>
    public BindingDirection Direction { get; set; } = BindingDirection.ReadWrite;

    /// <summary>
    /// 绑定源类型 —— Variable 直接绑定变量，Expression 使用 <see cref="Expression"/> 表达式。
    /// 旧项目文件无此字段时默认为 Variable，保持向后兼容。
    /// </summary>
    public BindingSourceKind Kind { get; set; } = BindingSourceKind.Variable;

    /// <summary>
    /// 表达式文本（Kind == Expression 时有效）。
    /// 语法与求值由 ExpressionEngine 提供（算术/比较/逻辑/三元，支持 UDT 字段引用）。
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// 浅拷贝 —— 本类字段全是字符串/枚举（无嵌套引用），浅拷贝即等价深拷贝。
    /// 用 <c>MemberwiseClone</c> 而非手工列字段：后者**必然随新增字段漂移**。
    /// </summary>
    public VariableBindingData ShallowCopy() => (VariableBindingData)MemberwiseClone();
}