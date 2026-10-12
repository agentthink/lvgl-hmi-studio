using HmiEditor.Core.Models;

namespace HmiEditor.Core.Services;

/// <summary>
/// 动作执行结果。
/// </summary>
public class ActionExecutionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string ActionType { get; set; } = "";
    public string? Target { get; set; }
}

/// <summary>
/// 动作执行器 —— 预览/运行时执行控件的 HmiAction。
///
/// 联动模型下动作的执行路径:
///   SetVariable/SetBit/ResetBit/ToggleBit/MomentaryOn/MomentaryOff
///     → RuntimeBindingEngine.WriteValue(variableId, value)  → IRuntimeValueSource
///   SwitchScreen → 通过回调委托通知宿主切换屏幕
///   ExecuteScript → 通过回调委托通知宿主执行脚本
///   ShowAlarms/ShowTrends/Login/Logout → 通过回调委托通知宿主
///
/// 设计要点:
///   - 纯逻辑层，与 UI 解耦：宿主注入回调处理非写变量类动作
///   - 写变量动作直接走绑定引擎（含 Scale/Offset 换算、只读保护）
///   - 动作 Target 为空时自动链接到绑定变量（联动规则）
/// </summary>
public class ActionExecutor
{
    private readonly RuntimeBindingEngine _bindingEngine;

    /// <summary>切换屏幕回调（宿主注入）。</summary>
    public Func<string, bool>? SwitchScreenHandler { get; set; }

    /// <summary>执行脚本回调（宿主注入）。</summary>
    public Func<string, bool>? ExecuteScriptHandler { get; set; }

    /// <summary>通用系统动作回调（ShowAlarms/ShowTrends/Login/Logout）。</summary>
    public Func<string, bool>? SystemActionHandler { get; set; }

    /// <summary>日志输出。</summary>
    public Action<string>? Log { get; set; }

    public ActionExecutor(RuntimeBindingEngine bindingEngine)
    {
        _bindingEngine = bindingEngine ?? throw new ArgumentNullException(nameof(bindingEngine));
    }

    /// <summary>
    /// 执行控件的指定触发事件动作。
    /// 联动模型：遍历 Actions 集合（回退旧 Action 字段），
    /// 执行 TriggerEvent 匹配的所有动作。
    /// </summary>
    /// <returns>至少一个动作成功执行则返回 true。</returns>
    public bool ExecuteControlAction(HmiControl control, string triggerEvent)
    {
        ArgumentNullException.ThrowIfNull(control);

        // 收集该控件的动作（集合优先，回退旧字段）
        var actions = control.Actions.Count > 0
            ? control.Actions.ToList()
            : (control.Action != null && control.Action.ActionType != "None"
                ? new List<HmiAction> { control.Action }
                : new List<HmiAction>());

        bool anyExecuted = false;
        foreach (var action in actions)
        {
            if (action.ActionType == "None") continue;
            if (!string.IsNullOrEmpty(triggerEvent) && action.TriggerEvent != triggerEvent)
                continue;

            // 联动规则：写动作 Target 为空 → 链接到控件写方向绑定变量
            if (HmiAction.IsWriteAction(action.ActionType) && string.IsNullOrEmpty(action.Target))
            {
                var writeVar = control.PrimaryWriteVariable;
                if (!string.IsNullOrEmpty(writeVar))
                    action.Target = writeVar;
            }

            var result = Execute(action);
            if (result.Success) anyExecuted = true;
            Log?.Invoke($"[ActionExecutor] {result.ActionType}: {result.Message}");
        }

        return anyExecuted;
    }

    /// <summary>执行单个动作。</summary>
    public ActionExecutionResult Execute(HmiAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        switch (action.ActionType)
        {
            // ── 写变量类动作 → 绑定引擎（联动模型核心）──
            case "SetVariable":
                return ExecuteSetVariable(action);
            case "SetBit":
                return ExecuteWriteVariable(action, 1, "置位");
            case "ResetBit":
                return ExecuteWriteVariable(action, 0, "复位");
            case "ToggleBit":
                return ExecuteToggleBit(action);
            case "MomentaryOn":
                return ExecuteWriteVariable(action, 1, "按下置位");
            case "MomentaryOff":
                return ExecuteWriteVariable(action, 0, "按下复位");

            // ── 系统动作 → 宿主回调 ──
            case "SwitchScreen":
                return ExecuteHandler(action, SwitchScreenHandler, "切换屏幕");
            case "ExecuteScript":
                return ExecuteHandler(action, ExecuteScriptHandler, "执行脚本");
            case "ShowAlarms":
            case "ShowTrends":
            case "Login":
            case "Logout":
                // 系统动作无 Target —— 回调传 ActionType 标识
                return ExecuteHandler(action, SystemActionHandler, action.ActionType,
                    passActionType: true);

            default:
                return new ActionExecutionResult
                {
                    Success = false,
                    ActionType = action.ActionType,
                    Message = $"未支持的动作类型: {action.ActionType}"
                };
        }
    }

    // ── 写变量执行 ──

    private ActionExecutionResult ExecuteSetVariable(HmiAction action)
    {
        var target = ResolveTarget(action);
        if (string.IsNullOrEmpty(target))
            return Fail(action, "SetVariable 缺少目标变量");

        if (!double.TryParse(action.Value, out var engineeringValue))
            return Fail(action, $"SetVariable 值无效: '{action.Value}'");

        return WriteToVariable(action, target, engineeringValue, "设置变量");
    }

    private ActionExecutionResult ExecuteWriteVariable(HmiAction action, double value, string opName)
    {
        var target = ResolveTarget(action);
        if (string.IsNullOrEmpty(target))
            return Fail(action, $"{opName} 缺少目标变量");

        return WriteToVariable(action, target, value, opName);
    }

    private ActionExecutionResult ExecuteToggleBit(HmiAction action)
    {
        var target = ResolveTarget(action);
        if (string.IsNullOrEmpty(target))
            return Fail(action, "取反缺少目标变量");

        // 读取当前值（工程值），取反后写回
        var current = _bindingEngine.ReadValue(target);
        var newValue = (current ?? 0) > 0.5 ? 0.0 : 1.0;

        return WriteToVariable(action, target, newValue, "取反");
    }

    /// <summary>写入变量 —— 统一走绑定引擎（含只读保护、Scale/Offset 换算）。</summary>
    private ActionExecutionResult WriteToVariable(
        HmiAction action, string target, double engineeringValue, string opName)
    {
        if (!_bindingEngine.WriteValue(target, engineeringValue))
        {
            // 判断失败原因（只读 / 无值源）
            var tag = _bindingEngine.FindTag(target);
            var reason = tag?.Direction == BindingDirection.ReadOnly
                ? "目标变量为只读绑定"
                : "无可用值源或未连接";
            return Fail(action, $"{opName} {target} 失败: {reason}");
        }

        return new ActionExecutionResult
        {
            Success = true,
            ActionType = action.ActionType,
            Target = target,
            Message = $"{opName} {target} = {engineeringValue:0.###}"
        };
    }

    // ── 系统动作执行 ──

    private ActionExecutionResult ExecuteHandler(
        HmiAction action, Func<string, bool>? handler, string opName,
        bool passActionType = false)
    {
        if (handler == null)
            return Fail(action, $"{opName} 未注册处理回调");

        var parameter = passActionType ? action.ActionType : (action.Target ?? "");
        if (handler(parameter))
        {
            return new ActionExecutionResult
            {
                Success = true,
                ActionType = action.ActionType,
                Target = parameter,
                Message = $"{opName} → {parameter}"
            };
        }
        return Fail(action, $"{opName} {parameter} 失败");
    }

    // ── 辅助 ──

    /// <summary>解析动作目标 —— 显式 Target 优先（空 Target 已由控件联动规则填充）。</summary>
    private static string? ResolveTarget(HmiAction action) =>
        string.IsNullOrEmpty(action.Target) ? null : action.Target;

    private static ActionExecutionResult Fail(HmiAction action, string message) =>
        new()
        {
            Success = false,
            ActionType = action.ActionType,
            Target = action.Target,
            Message = message
        };
}
