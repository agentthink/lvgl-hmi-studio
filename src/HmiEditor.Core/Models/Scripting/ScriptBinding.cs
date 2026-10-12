namespace HmiEditor.Core.Models.Scripting;

/// <summary>
/// 控件的脚本绑定 —— 将控件事件映射到 Lua 函数。
/// </summary>
public class ScriptBinding
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>绑定的控件 ID</summary>
    public string ControlId { get; set; } = "";

    /// <summary>事件名称（OnClick / OnValueChanged / OnPress / OnRelease）</summary>
    public string EventName { get; set; } = "";

    /// <summary>绑定的脚本 ID</summary>
    public string ScriptId { get; set; } = "";

    /// <summary>入口函数名（Lua 函数名，如 "on_button_click"）</summary>
    public string FunctionName { get; set; } = "";

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;
}
