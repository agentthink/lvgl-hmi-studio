namespace HmiEditor.AI.Services;

/// <summary>
/// Lua 脚本补全建议条目。
/// </summary>
public sealed record LuaCompletion(string Label, string InsertText, string Detail);

/// <summary>
/// Lua 脚本补全服务 —— 基于 HMI API 与常用函数模板的上下文补全。
/// 由 ScriptEditorView 在用户输入触发时调用(按 Tab / Ctrl+Space)。
/// </summary>
public static class LuaCompletionService
{
    /// <summary>可用 HMI API 定义。</summary>
    private static readonly LuaCompletion[] HmiApis =
    {
        new("hmi_read", "hmi_read(\"变量名\")", "读取 HMI 变量值 → number | boolean"),
        new("hmi_write", "hmi_write(\"变量名\", 值)", "写入 HMI 变量值"),
        new("hmi_set_visible", "hmi_set_visible(\"控件ID\", true)", "设置控件可见性"),
        new("hmi_set_text", "hmi_set_text(\"控件ID\", \"文本\")", "设置控件文本"),
        new("hmi_set_color", "hmi_set_color(\"控件ID\", \"#RRGGBB\")", "设置控件颜色"),
        new("print", "print(\"消息\")", "输出调试信息")
    };

    /// <summary>常用函数模板。</summary>
    private static readonly LuaCompletion[] FunctionTemplates =
    {
        new("on_click (按钮点击)", "function on_click_按钮()\n    -- TODO: 在此编写点击逻辑\n    local value = hmi_read(\"变量名\")\n    hmi_write(\"变量名\", value)\nend", "按钮点击事件处理函数模板"),
        new("on_value_changed (值变化)", "function on_value_changed_变量()\n    -- TODO: 在此编写值变化逻辑\n    local value = hmi_read(\"变量名\")\n    if value then\n        hmi_set_text(\"控件ID\", tostring(value))\n    end\nend", "变量值变化事件处理函数模板"),
        new("on_timer (定时器)", "function on_timer()\n    -- TODO: 在此编写周期逻辑\n    local value = hmi_read(\"变量名\")\n    print(\"当前值: \" .. tostring(value))\nend", "定时器周期执行函数模板"),
        new("alarm (报警)", "function on_alarm_触发()\n    -- TODO: 在此编写报警联动逻辑\n    hmi_set_color(\"控件ID\", \"#ef4444\")\n    hmi_set_visible(\"控件ID\", true)\nend", "报警联动处理函数模板")
    };

    /// <summary>
    /// 获取补全建议。按输入前缀过滤,空前缀返回全部(供模板面板使用)。
    /// </summary>
    public static IReadOnlyList<LuaCompletion> GetCompletions(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<LuaCompletion>();

        var prefix = input.Trim().ToLowerInvariant();

        // 以 "hmi_" 开头 → 匹配 HMI API
        if (prefix.StartsWith("hmi_", StringComparison.Ordinal))
        {
            return HmiApis
                .Where(c => c.Label.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // 以 "on_" 开头 → 匹配函数模板
        if (prefix.StartsWith("on_", StringComparison.Ordinal))
        {
            return FunctionTemplates
                .Where(c => c.Label.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // 其他 → 全部 API + 常用函数
        return HmiApis.Concat(FunctionTemplates).ToList();
    }

    /// <summary>常用函数模板列表(供 UI 下拉菜单展示)。</summary>
    public static IReadOnlyList<LuaCompletion> GetAllTemplates() => FunctionTemplates;

    /// <summary>HMI API 列表(供文档提示)。</summary>
    public static IReadOnlyList<LuaCompletion> GetAllApis() => HmiApis;
}
