using System.Text.Json.Serialization;

namespace HmiEditor.Core.Models;

/// <summary>
/// 模板参数方向 —— 决定参数在 Faceplate 实例中的数据流向。
/// </summary>
public enum ParameterDirection
{
    /// <summary>外部传入，模板内部只读（最常见）。</summary>
    Input,

    /// <summary>模板内部写入，外部可读。</summary>
    Output,

    /// <summary>双向读写。</summary>
    InOut
}

/// <summary>
/// 模板参数值类型 —— 限制参数可接受的数据种类和编辑器类型。
/// </summary>
public enum ParameterType
{
    /// <summary>任意字符串（如标签文本、屏幕名）。</summary>
    String,

    /// <summary>数值（如阈值、尺寸）。</summary>
    Number,

    /// <summary>布尔（如可见性、初始状态）。</summary>
    Boolean,

    /// <summary>颜色十六进制字符串（如 #RRGGBB）。</summary>
    Color,

    /// <summary>变量 ID（绑定到 VariableManager 中的变量）。</summary>
    VariableId
}

/// <summary>
/// Faceplate 模板参数定义 —— 对标 WinCC Faceplate 接口属性。
/// 实例化时由 ParameterBindings 提供实际值；未绑定的参数使用 DefaultValue。
/// </summary>
public class TemplateParameter
{
    public string Name { get; set; } = "";

    /// <summary>显示名（中文友好名称）。</summary>
    public string DisplayName { get; set; } = "";

    public ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    public ParameterType Type { get; set; } = ParameterType.String;

    /// <summary>默认值 —— 实例未提供绑定时使用。</summary>
    public string? DefaultValue { get; set; }

    /// <summary>参数描述（显示在属性面板提示中）。</summary>
    public string Description { get; set; } = "";

    /// <summary>数值类型的最小值（可选约束）。</summary>
    public double? MinValue { get; set; }

    /// <summary>数值类型的最大值（可选约束）。</summary>
    public double? MaxValue { get; set; }
}

/// <summary>
/// Faceplate 实例的参数绑定 —— 将模板参数名映射到实际值。
/// 存储在 HmiControl.ParameterBindings 中。
/// </summary>
public class ParameterBinding
{
    /// <summary>模板参数名（对应 TemplateParameter.Name）。</summary>
    public string ParameterName { get; set; } = "";

    /// <summary>绑定的实际值（字符串形式，按参数类型解析）。</summary>
    public string? Value { get; set; }

    /// <summary>
    /// 绑定的变量 ID（仅 ParameterType.VariableId 时使用）。
    /// 当此字段非空时，Value 表示变量名而非字面值。
    /// </summary>
    public string? VariableId { get; set; }

    /// <summary>
    /// 浅拷贝 —— 本类字段全是字符串/枚举（无嵌套引用），浅拷贝即等价深拷贝。
    /// 用 <c>MemberwiseClone</c> 而非手工列字段：后者**必然随新增字段漂移**。
    /// </summary>
    public ParameterBinding ShallowCopy() => (ParameterBinding)MemberwiseClone();
}
