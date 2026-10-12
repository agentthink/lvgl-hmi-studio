namespace HmiEditor.Core.Models.Scripting;

/// <summary>
/// 预置脚本模板 —— 提供给用户的快速起步模板。
/// </summary>
public class ScriptTemplate
{
    /// <summary>模板名称</summary>
    public string Name { get; set; } = "";

    /// <summary>模板分类</summary>
    public string Category { get; set; } = "";

    /// <summary>模板描述</summary>
    public string? Description { get; set; }

    /// <summary>模板 Lua 代码</summary>
    public string Code { get; set; } = "";

    // ── 预置模板 ──

    /// <summary>
    /// 全部内置模板 —— **脚本分类清单的唯一来源**
    /// （AI 脚本生成 JSON Schema 的 <c>category</c> enum 由此派生，见
    /// <c>PromptTemplateEngine.RenderSchema</c>）。
    ///
    /// <para>加上这个集合之前，4 个分类名同时存在于两处：这里的 4 个静态属性，
    /// 与 <c>ScriptGenerationSchema.json</c> 里手抄的 <c>["OnClick","OnValueChanged","Timer","Alarm"]</c>。
    /// 当时两者恰好一致，所以**没有任何东西会提醒你已经不同步**。
    /// 这里的分类名不是随手编的标签 —— 它必须与模板 <c>Code</c> 里的
    /// <c>function on_click / on_value_changed / on_timer / on_alarm</c> 钩子名对应，
    /// 所以约束本身是合理的，只是缺一个同步机制。</para>
    /// </summary>
    public static IReadOnlyList<ScriptTemplate> Defaults { get; } =
        [OnClick, OnValueChanged, Timer, Alarm];

    /// <summary>内置模板分类名清单（顺序 = <see cref="Defaults"/> 的声明顺序）。</summary>
    public static IReadOnlyList<string> Categories { get; } =
        Defaults.Select(t => t.Category).ToArray();

    /// <summary>按钮点击回调模板</summary>
    public static ScriptTemplate OnClick => new()
    {
        Name = "Button OnClick",
        Category = "OnClick",
        Description = "按钮点击事件回调，切换变量值",
        Code = @"-- Button OnClick callback
-- Available APIs:
--   hmi_read(variable_id) -> value
--   hmi_write(variable_id, value)
--   hmi_set_control_property(control_id, property, value)

function on_click()
    local current = hmi_read(""VARIABLE_ID"")
    hmi_write(""VARIABLE_ID"", not current)
end
",
    };

    /// <summary>值变化回调模板</summary>
    public static ScriptTemplate OnValueChanged => new()
    {
        Name = "Value Change Handler",
        Category = "OnValueChanged",
        Description = "变量值变化事件回调，联动其他控件",
        Code = @"-- Value Changed callback
-- new_value: the new value of the variable

function on_value_changed(new_value)
    if new_value > 100 then
        hmi_set_control_property(""ALARM_CTRL"", ""color"", ""red"")
    else
        hmi_set_control_property(""ALARM_CTRL"", ""color"", ""green"")
    end
end
",
    };

    /// <summary>定时器脚本模板</summary>
    public static ScriptTemplate Timer => new()
    {
        Name = "Timer Handler",
        Category = "Timer",
        Description = "周期性定时器回调，数据采集或周期检查",
        Code = @"-- Timer callback (runs every INTERVAL_MS ms)
-- Use for periodic data collection or background checks

function on_timer()
    local temp = hmi_read(""TEMP_VAR"")
    if temp > 150 then
        hmi_write(""ALARM_TRIGGER"", 1)
    end
end
",
    };

    /// <summary>报警联动模板</summary>
    public static ScriptTemplate Alarm => new()
    {
        Name = "Alarm Linkage",
        Category = "Alarm",
        Description = "报警触发联动动作，启动排风/停止设备",
        Code = @"-- Alarm linkage callback
-- alarm_id: the triggered alarm ID
-- severity: 0=Info, 1=Warning, 2=Critical, 3=Emergency

function on_alarm(alarm_id, severity)
    if severity >= 2 then
        hmi_write(""EMERGENCY_STOP"", 1)
        hmi_write(""ALARM_LAMP"", 1)
    end
end
",
    };
}
