using HmiEditor.Core.Models;

namespace HmiEditor.Core.Design;

/// <summary>
/// 属性即时校验器 —— 属性面板保存前逐属性校验，返回 null 表示通过，否则返回错误消息。
/// 可注入到属性面板（PropertyPanelView.CustomValidator）做即时校验；
/// 插件或服务端配置下发可实现本接口替换/扩展校验规则（相对静态 PropertyValidator 的可扩展点）。
/// </summary>
public interface IPropertyValidator
{
    /// <summary>校验单个属性。propertyName 为 HmiControl 属性名（nameof 引用），value 为待写回值。</summary>
    string? Validate(HmiControl control, string propertyName, object? value);
}
