using System.Collections.Generic;

namespace HmiEditor.Core.Models;

/// <summary>
/// 控件动作定义 —— 对标 WinCC / EasyBuilder Pro 的控件事件系统。
/// 每个控件可绑定一个动作，运行时触发。动作按控件类型分发不同的可选集合。
/// </summary>
public class HmiAction
{
    /// <summary>动作类型。</summary>
    public string ActionType { get; set; } = "None";

    /// <summary>目标参数（屏幕名 / 变量ID / 脚本ID 等）。</summary>
    public string? Target { get; set; }

    /// <summary>附加参数（设置变量时的值、脚本参数等）。</summary>
    public string? Value { get; set; }

    /// <summary>触发事件类型。</summary>
    public string TriggerEvent { get; set; } = "OnClick";

    /// <summary>
    /// 浅拷贝 —— 本类字段全是字符串/枚举（无嵌套引用），浅拷贝即等价深拷贝。
    /// 用 <c>MemberwiseClone</c> 而非手工列字段：后者**必然随新增字段漂移**
    /// （FaceplateExpander.CloneControl 手工拷贝时就漏过 Kind/Expression 一类字段）。
    /// </summary>
    public HmiAction ShallowCopy() => (HmiAction)MemberwiseClone();

    // ── 工厂方法 ──

    public static HmiAction SwitchScreen(string screenName) => new()
    {
        ActionType = "SwitchScreen", Target = screenName, TriggerEvent = "OnClick"
    };

    public static HmiAction SetVariable(string variableId, string value) => new()
    {
        ActionType = "SetVariable", Target = variableId, Value = value, TriggerEvent = "OnClick"
    };

    public static HmiAction ExecuteScript(string scriptId) => new()
    {
        ActionType = "ExecuteScript", Target = scriptId, TriggerEvent = "OnClick"
    };

    public static HmiAction SetBit(string variableId) => new()
    {
        ActionType = "SetBit", Target = variableId, Value = "1", TriggerEvent = "OnClick"
    };

    public static HmiAction ResetBit(string variableId) => new()
    {
        ActionType = "ResetBit", Target = variableId, Value = "0", TriggerEvent = "OnClick"
    };

    public static HmiAction ToggleBit(string variableId) => new()
    {
        ActionType = "ToggleBit", Target = variableId, TriggerEvent = "OnClick"
    };

    public static HmiAction MomentaryOn(string variableId) => new()
    {
        ActionType = "MomentaryOn", Target = variableId, TriggerEvent = "OnPress"
    };

    public static HmiAction MomentaryOff(string variableId) => new()
    {
        ActionType = "MomentaryOff", Target = variableId, TriggerEvent = "OnPress"
    };

    public static HmiAction None => new() { ActionType = "None" };
    public static HmiAction ShowAlarms => new() { ActionType = "ShowAlarms" };
    public static HmiAction ShowTrends => new() { ActionType = "ShowTrends" };
    public static HmiAction Login => new() { ActionType = "Login" };
    public static HmiAction Logout => new() { ActionType = "Logout" };

    // ── 联动模型辅助方法 ──

    /// <summary>判断动作是否为写变量操作。</summary>
    public static bool IsWriteAction(string? actionType) => actionType switch
    {
        "SetVariable" or "SetBit" or "ResetBit" or "ToggleBit"
            or "MomentaryOn" or "MomentaryOff" => true,
        _ => false
    };

    /// <summary>所有写变量动作类型集合。</summary>
    public static readonly HashSet<string> WriteActionTypes = new()
    {
        "SetVariable", "SetBit", "ResetBit", "ToggleBit",
        "MomentaryOn", "MomentaryOff"
    };

    /// <summary>将动作 Target 自动链接到指定变量（联动推导）。</summary>
    public HmiAction AutoLinkTarget(string variableId)
    {
        if (IsWriteAction(ActionType) && string.IsNullOrEmpty(Target))
            Target = variableId;
        return this;
    }

    // ── 动作元数据 ──

    /// <summary>
    /// 所有可能的动作定义（供 UI 下拉框使用）。
    ///
    /// <para><b>刻意不带 <c>DisplayName</c></b>（阶段 94，闭环登记 #61m）：下拉框的标签是
    /// <b>界面文案</b>，必须跟界面语言走；而 Core 是库、不引本地化 ⇒ 标签由 App 层按
    /// <see cref="Tag"/> 推导（<c>HmiEditor.App.Services.ActionTextKeys.TypeName</c>，
    /// 与 <c>RoleTextKeys</c> 同形）。改前这里带着中文标签（35 条字面量），
    /// 与 <see cref="GetDescription"/> 一起被错归成「DATA：落盘数据」—— 实测**无一条落盘**
    /// （落盘的是 <see cref="ActionType"/> / <see cref="Target"/> / <see cref="Value"/> 这些 Id）。</para>
    /// </summary>
    public record ActionMeta(string Tag, bool NeedsTarget, bool NeedsValue);

    /// <summary>按控件类型获取可选动作列表。</summary>
    public static IReadOnlyList<ActionMeta> GetAvailableActions(HmiControlType controlType)
    {
        return controlType switch
        {
            // ── 显示控件：显示变量值 ──
            HmiControlType.Label or HmiControlType.Gauge or HmiControlType.Knob
                or HmiControlType.Led or HmiControlType.Bar or HmiControlType.Line
                or HmiControlType.Image
                => ShowAvailableActions(),

            // ── 输入控件：布尔操作 + 通用 + 切换屏幕 ──
            HmiControlType.Button => BoolAvailableActions(),

            // ── 开关/复选框：布尔操作 ──
            HmiControlType.Switch or HmiControlType.CheckBox
                => new List<ActionMeta>
                {
                    new("None", false, false),
                    new("ToggleBit", true, false),
                    new("SetBit", true, false),
                    new("ResetBit", true, false),
                    new("SwitchScreen", true, false),
                },

            // ── 滑块/下拉框：设值 ──
            HmiControlType.Slider or HmiControlType.Dropdown
                => new List<ActionMeta>
                {
                    new("None", false, false),
                    new("SetVariable", true, true),
                    new("SwitchScreen", true, false),
                },

            // ── 文本区域/键盘/表格：通用操作 ──
            HmiControlType.TextArea or HmiControlType.Keyboard or HmiControlType.Table
                => new List<ActionMeta>
                {
                    new("None", false, false),
                    new("SwitchScreen", true, false),
                    new("SetVariable", true, true),
                    new("ExecuteScript", true, false),
                },

            // ── 屏幕/容器/选项卡/Spinner：无动作或切换 ──
            _ => new List<ActionMeta>
            {
                new("None", false, false),
                new("SwitchScreen", true, false),
                new("ShowAlarms", false, false),
                new("ShowTrends", false, false),
            }
        };
    }

    private static List<ActionMeta> ShowAvailableActions() => new()
    {
        new("None", false, false),
        new("SwitchScreen", true, false),
        new("ShowAlarms", false, false),
        new("ShowTrends", false, false),
        new("Login", false, false),
        new("Logout", false, false),
    };

    private static List<ActionMeta> BoolAvailableActions() => new()
    {
        new("None", false, false),
        // 布尔操作
        new("SetBit", true, false),
        new("ResetBit", true, false),
        new("ToggleBit", true, false),
        new("MomentaryOn", true, false),
        new("MomentaryOff", true, false),
        // 设值
        new("SetVariable", true, true),
        // 画面
        new("SwitchScreen", true, false),
        // 通用
        new("ExecuteScript", true, false),
        new("ShowAlarms", false, false),
        new("ShowTrends", false, false),
        new("Login", false, false),
        new("Logout", false, false),
    };

    /// <summary>
    /// 简要描述（**中文**）。
    ///
    /// <para><b>本方法的两个方向，语言要求相反</b>（阶段 94 之前它是同一个方法被两处共用，
    /// 登记 #61m）：</para>
    /// <list type="number">
    /// <item><b>生成产物注释</b>（<c>CodeGenerator.Actions.cs</c> 三处 <c>Cmt(</c>）⇒ 必须**固定中文**：
    /// 产物可复现性要求同一工程两次导出逐字节一致，跟着编辑器 UI 语言变会把 A/B 归一化搞坏；</item>
    /// <item><b>编辑器的面板</b>（交叉引用面板与属性面板）⇒ 必须**跟界面语言走**，
    /// 正常路径已改走 <c>HmiEditor.App.Services.ActionTextKeys.Describe</c>，
    /// 本方法只剩**回落**职责（字典缺 <c>action.desc.*</c> 时用它）。</item>
    /// </list>
    /// <para>⚠ 因此**新增动作类型时**：这里加一条中文（产物侧），App 侧
    /// <c>ActionTextKeys</c> 同步加 <c>action.type.*</c> / <c>action.desc.*</c> 两个键 ——
    /// 由 <c>ActionTextLocalizationGuardTests</c> 的**双向**一对一判据守着（漏一个就红）。</para>
    /// </summary>
    public string GetDescription()
    {
        return ActionType switch
        {
            "None" => "无动作",
            "SwitchScreen" => $"切换到屏幕: {Target ?? "?"}",
            "SetVariable" => $"设置 {Target ?? "?"} = {Value ?? "?"}",
            "ExecuteScript" => $"执行脚本: {Target ?? "?"}",
            "SetBit" => $"置位: {Target ?? "?"}",
            "ResetBit" => $"复位: {Target ?? "?"}",
            "ToggleBit" => $"取反: {Target ?? "?"}",
            "MomentaryOn" => $"按1松0: {Target ?? "?"}",
            "MomentaryOff" => $"按0松1: {Target ?? "?"}",
            "ShowAlarms" => "显示报警列表",
            "ShowTrends" => "显示趋势图",
            "Login" => "弹出登录",
            "Logout" => "登出",
            _ => ActionType
        };
    }
}
