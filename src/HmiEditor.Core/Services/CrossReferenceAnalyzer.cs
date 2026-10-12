using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Scripting;

namespace HmiEditor.Core.Services;

// ═══════════════════════════════════════════════════════════
//  R3-T4: 交叉引用分析器
//  扫描项目中变量/屏幕/脚本的引用关系，
//  支持按目标查找所有引用位置（控件属性、动作、脚本绑定）。
// ═══════════════════════════════════════════════════════════

/// <summary>引用类型。</summary>
public enum ReferenceType
{
    VariableBinding,
    ActionTarget,
    ScriptBinding,
    ExpressionVariable
}

/// <summary>
/// 引用位置的**描述种类** —— 界面侧据此跨语言渲染
/// （<c>HmiEditor.App.Services.ReferenceTextKeys.Describe</c>），
/// <see cref="ReferenceLocation.Description"/> 只作**回落原文**（阶段 95，闭环登记 #61n）。
///
/// <para><b>为什么不按 <see cref="ReferenceType"/> 分派</b>：同一个 <c>ReferenceType</c> 下有**两种措辞**
/// —— <see cref="ReferenceType.VariableBinding"/> 既有「绑定变量 (方向)」（联动模型）也有
/// 「绑定到变量」（回退旧字段）；<see cref="ReferenceType.ActionTarget"/> 既有「动作 {0}」
/// （变量引用）也有「{0} → 切换到 {1}」（屏幕引用）⇒ 类型不足以决定模板。</para>
///
/// <para><b>为什么 <see cref="Unset"/> 排第一（值 0）</b>：枚举字段忘了赋值时落进它、
/// 而**不是**静默落进某个带真实语义的档（阶段 92 的 #61j 实测过：默认支承担真实语义会
/// 显示**假标签**）。<c>Unset</c> 的处置是显式的：界面侧原样显示 Core 回落原文
/// （中文）⇒ 英文界面下一眼可见，且判据钉「真实分析器产出的行不许是 Unset」。</para>
/// </summary>
public enum ReferenceDescriptionKind
{
    /// <summary>未设置 —— 界面侧原样显示 <see cref="ReferenceLocation.Description"/>（诚实边界，不是缺陷）。</summary>
    Unset = 0,

    /// <summary>联动模型：<c>控件 {0} 绑定变量 ({1})</c>。</summary>
    VariableBinding,

    /// <summary>回退旧字段：<c>控件 {0} 绑定到变量</c>。</summary>
    LegacyVariableBinding,

    /// <summary>动作详情：<c>动作 {0}</c>（描述体由 <see cref="ReferenceLocation.Action"/> 提供）。</summary>
    ActionDetail,

    /// <summary>屏幕引用：<c>{0} → 切换到 {1}</c>。</summary>
    ScreenReference,

    /// <summary>脚本引用：<c>{0} → 执行脚本 {1}</c>。</summary>
    ScriptRun,

    /// <summary>脚本绑定：<c>脚本绑定: {0} ({1})</c>。</summary>
    ScriptBinding,
}

/// <summary>单个引用位置。</summary>
public class ReferenceLocation
{
    /// <summary>屏幕名称。</summary>
    public string ScreenName { get; set; } = "";

    /// <summary>屏幕 Id。</summary>
    public string ScreenId { get; set; } = "";

    /// <summary>控件 Id。</summary>
    public string ControlId { get; set; } = "";

    /// <summary>控件名称。</summary>
    public string ControlName { get; set; } = "";

    /// <summary>控件类型。</summary>
    public HmiControlType ControlType { get; set; }

    /// <summary>引用类型。</summary>
    public ReferenceType ReferenceType { get; set; }

    /// <summary>引用的属性名或事件名。</summary>
    public string PropertyOrEvent { get; set; } = "";

    /// <summary>引用的目标 Id（变量 Id / 屏幕名 / 脚本 Id）。</summary>
    public string TargetId { get; set; } = "";

    /// <summary>
    /// 引用的详细描述（<b>中文回落原文</b>）。
    ///
    /// <para>界面上的正常路径由 App 层渲染 —— 见 <see cref="Action"/>（阶段 94，登记 #61m）。
    /// 本字段在这些情况下才冒头：字典缺键、或消费方不是 App（Core 自己的测试与
    /// 「不经界面」的消费者）。</para>
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 动作引用（<see cref="ReferenceType.ActionTarget"/> 时非空）：界面据此**跨语言**渲染描述
    /// （<c>HmiEditor.App.Services.ActionTextKeys.DescribeReference</c>），<see cref="Description"/>
    /// 只作回落。
    ///
    /// <para><b>为什么挂对象而不是「码 ＋ 实参」</b>：与 <c>DanglingRefLocation</c>（#61l）不同，
    /// 这里的实参组合**按动作类型而异**（0/1/2 个），用固定实参数组会把
    /// <c>LocalizedFormatCallSiteTests</c> 的 C1（实参个数 == 占位符个数）搞红；
    /// 挂对象则让 App 侧按类型分派，每个分支的实参个数都是**字面量**。</para>
    /// </summary>
    public HmiAction? Action { get; set; }

    /// <summary>
    /// 描述种类 —— 界面侧渲染的**唯一分派依据**（阶段 95，闭环登记 #61n）。
    /// 忘了赋值 ⇒ 落 <see cref="ReferenceDescriptionKind.Unset"/>，界面显示本类的
    /// <see cref="Description"/>（中文回落）⇒ 英文界面下可见，判据也点名。
    /// </summary>
    public ReferenceDescriptionKind DescriptionKind { get; set; }

    /// <summary>
    /// 绑定方向（<see cref="ReferenceDescriptionKind.VariableBinding"/> 时有效）。
    ///
    /// <para><b>为什么必须是字段、不能从 <see cref="Description"/> 里反解</b>：改动前面板措辞
    /// 是 Core 拼好的整句，方向**只存在那句中文里** —— 界面侧要重建模板就得有它的原始取值。
    /// ⚠ 方向目前仍按**枚举名**直显（<c>ReadWrite</c>），与登记 #61o 同族、本刀刻意不动
    /// （「一次只放开一个变量」）。</para>
    /// </summary>
    public BindingDirection BindingDirection { get; set; }

    /// <summary>
    /// 脚本入口函数名（<see cref="ReferenceDescriptionKind.ScriptBinding"/> 时有效）。
    ///
    /// <para>同 <see cref="BindingDirection"/>：改动前它只出现在拼好的中文句子里
    /// （<c>脚本绑定: {函数名} ({事件名})</c>），没有分列字段可读。</para>
    /// </summary>
    public string ScriptFunctionName { get; set; } = "";
}

/// <summary>交叉引用分析结果。</summary>
public class CrossReferenceResult
{
    /// <summary>搜索的目标类型。</summary>
    public string TargetType { get; set; } = "";

    /// <summary>搜索的目标 Id 或名称。</summary>
    public string TargetId { get; set; } = "";

    /// <summary>所有引用位置。</summary>
    public List<ReferenceLocation> References { get; set; } = new();

    /// <summary>引用总数。</summary>
    public int Count => References.Count;

    /// <summary>是否有引用。</summary>
    public bool HasReferences => References.Count > 0;
}

/// <summary>
/// R3-T4: 交叉引用分析器 —— 扫描项目，查找变量/屏幕/脚本的所有引用位置。
/// 用于：删除前检查、影响分析、跳转到引用处。
/// </summary>
public static class CrossReferenceAnalyzer
{
    /// <summary>
    /// 查找指定变量的所有引用位置。
    /// 支持普通变量 Id/名称 和 UDT 字段 compound id（如 "motor1.speed"）。
    /// </summary>
    public static CrossReferenceResult FindVariableReferences(
        HmiProject project, string variableIdOrName)
    {
        var result = new CrossReferenceResult
        {
            TargetType = "Variable",
            TargetId = variableIdOrName
        };

        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                // 控件变量绑定（联动模型：遍历 VariableBindings 集合，回退旧字段）
                foreach (var binding in control.VariableBindings)
                {
                    if (MatchesVariable(binding.VariableId, variableIdOrName, project))
                    {
                        result.References.Add(new ReferenceLocation
                        {
                            ScreenName = screen.Name,
                            ScreenId = screen.Id,
                            ControlId = control.Id,
                            ControlName = control.Name,
                            ControlType = control.Type,
                            ReferenceType = ReferenceType.VariableBinding,
                            PropertyOrEvent = binding.PropertyName,
                            TargetId = binding.VariableId,
                            Description = $"控件 {control.Name} 绑定变量 ({binding.Direction})",
                            DescriptionKind = ReferenceDescriptionKind.VariableBinding,
                            BindingDirection = binding.Direction
                        });
                    }
                }

                // 回退旧字段
                if (control.VariableBindings.Count == 0
                    && MatchesVariable(control.VariableBindingId, variableIdOrName, project))
                {
                    result.References.Add(new ReferenceLocation
                    {
                        ScreenName = screen.Name,
                        ScreenId = screen.Id,
                        ControlId = control.Id,
                        ControlName = control.Name,
                        ControlType = control.Type,
                        ReferenceType = ReferenceType.VariableBinding,
                        PropertyOrEvent = "VariableBindingId",
                        TargetId = control.VariableBindingId ?? "",
                        Description = $"控件 {control.Name} 绑定到变量",
                        DescriptionKind = ReferenceDescriptionKind.LegacyVariableBinding
                    });
                }

                // 动作中的变量引用（联动模型：遍历 Actions 集合，回退旧字段）
                if (control.Actions.Count > 0)
                {
                    foreach (var action in control.Actions)
                    {
                        if (ActionReferencesVariable(action, variableIdOrName, project))
                        {
                            result.References.Add(new ReferenceLocation
                            {
                                ScreenName = screen.Name,
                                ScreenId = screen.Id,
                                ControlId = control.Id,
                                ControlName = control.Name,
                                ControlType = control.Type,
                                ReferenceType = ReferenceType.ActionTarget,
                                PropertyOrEvent = action.TriggerEvent,
                                TargetId = action.Target ?? "",
                                Description = $"动作 {action.GetDescription()}",
                                DescriptionKind = ReferenceDescriptionKind.ActionDetail,
                                Action = action
                            });
                        }
                    }
                }
                else if (control.Action != null && ActionReferencesVariable(control.Action, variableIdOrName, project))
                {
                    result.References.Add(new ReferenceLocation
                    {
                        ScreenName = screen.Name,
                        ScreenId = screen.Id,
                        ControlId = control.Id,
                        ControlName = control.Name,
                        ControlType = control.Type,
                        ReferenceType = ReferenceType.ActionTarget,
                        PropertyOrEvent = control.Action.TriggerEvent,
                        TargetId = control.Action.Target ?? "",
                        Description = $"动作 {control.Action.GetDescription()}",
                        DescriptionKind = ReferenceDescriptionKind.ActionDetail,
                        Action = control.Action
                    });
                }
            }
        }

        // 脚本绑定不直接引用变量（通过脚本代码间接引用），
        // 变量引用已通过控件 VariableBindingId 和动作 Target 覆盖

        return result;
    }

    /// <summary>
    /// 查找指定屏幕的所有引用位置（SwitchScreen 动作）。
    /// </summary>
    public static CrossReferenceResult FindScreenReferences(
        HmiProject project, string screenName)
    {
        var result = new CrossReferenceResult
        {
            TargetType = "Screen",
            TargetId = screenName
        };

        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                // 联动模型：遍历 Actions 集合
                foreach (var action in control.Actions)
                {
                    if (action.ActionType != "SwitchScreen") continue;
                    if (!string.Equals(action.Target, screenName, StringComparison.Ordinal))
                        continue;

                    result.References.Add(new ReferenceLocation
                    {
                        ScreenName = screen.Name,
                        ScreenId = screen.Id,
                        ControlId = control.Id,
                        ControlName = control.Name,
                        ControlType = control.Type,
                        ReferenceType = ReferenceType.ActionTarget,
                        PropertyOrEvent = action.TriggerEvent,
                        TargetId = action.Target ?? "",
                        Description = $"{control.Name} → 切换到 {screenName}",
                        DescriptionKind = ReferenceDescriptionKind.ScreenReference
                    });
                }

                // 回退旧字段
                if (control.Action != null
                    && control.Action.ActionType == "SwitchScreen"
                    && string.Equals(control.Action.Target, screenName, StringComparison.Ordinal)
                    && control.Actions.Count == 0)
                {
                    result.References.Add(new ReferenceLocation
                    {
                        ScreenName = screen.Name,
                        ScreenId = screen.Id,
                        ControlId = control.Id,
                        ControlName = control.Name,
                        ControlType = control.Type,
                        ReferenceType = ReferenceType.ActionTarget,
                        PropertyOrEvent = control.Action.TriggerEvent,
                        TargetId = control.Action.Target ?? "",
                        Description = $"{control.Name} → 切换到 {screenName}",
                        DescriptionKind = ReferenceDescriptionKind.ScreenReference
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 查找指定脚本的所有引用位置（ExecuteScript 动作 + 脚本绑定）。
    /// </summary>
    public static CrossReferenceResult FindScriptReferences(
        HmiProject project, string scriptId)
    {
        var result = new CrossReferenceResult
        {
            TargetType = "Script",
            TargetId = scriptId
        };

        // 动作中的脚本引用（联动模型：遍历 Actions 集合）
        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                foreach (var action in control.Actions)
                {
                    if (action.ActionType != "ExecuteScript") continue;
                    if (!string.Equals(action.Target, scriptId, StringComparison.Ordinal))
                        continue;

                    result.References.Add(new ReferenceLocation
                    {
                        ScreenName = screen.Name,
                        ScreenId = screen.Id,
                        ControlId = control.Id,
                        ControlName = control.Name,
                        ControlType = control.Type,
                        ReferenceType = ReferenceType.ActionTarget,
                        PropertyOrEvent = action.TriggerEvent,
                        TargetId = action.Target ?? "",
                        Description = $"{control.Name} → 执行脚本 {scriptId}",
                        DescriptionKind = ReferenceDescriptionKind.ScriptRun
                    });
                }

                // 回退旧字段
                if (control.Actions.Count == 0
                    && control.Action != null
                    && control.Action.ActionType == "ExecuteScript"
                    && string.Equals(control.Action.Target, scriptId, StringComparison.Ordinal))
                {
                    result.References.Add(new ReferenceLocation
                    {
                        ScreenName = screen.Name,
                        ScreenId = screen.Id,
                        ControlId = control.Id,
                        ControlName = control.Name,
                        ControlType = control.Type,
                        ReferenceType = ReferenceType.ActionTarget,
                        PropertyOrEvent = control.Action.TriggerEvent,
                        TargetId = control.Action.Target ?? "",
                        Description = $"{control.Name} → 执行脚本 {scriptId}",
                        DescriptionKind = ReferenceDescriptionKind.ScriptRun
                    });
                }
            }
        }

        // 脚本绑定
        foreach (var binding in project.ScriptBindings)
        {
            if (!string.Equals(binding.ScriptId, scriptId, StringComparison.Ordinal))
                continue;

            // 通过 ControlId 查找所属屏幕
            var screen = project.Screens.FirstOrDefault(s =>
                s.Controls.Any(c => c.Id == binding.ControlId));
            var control = screen?.Controls.FirstOrDefault(c => c.Id == binding.ControlId);

            result.References.Add(new ReferenceLocation
            {
                ScreenName = screen?.Name ?? "",
                ScreenId = screen?.Id ?? "",
                ControlId = binding.ControlId,
                ControlName = control?.Name ?? "",
                ControlType = control?.Type ?? HmiControlType.Button,
                ReferenceType = ReferenceType.ScriptBinding,
                PropertyOrEvent = binding.EventName,
                TargetId = binding.ScriptId,
                Description = $"脚本绑定: {binding.FunctionName} ({binding.EventName})",
                DescriptionKind = ReferenceDescriptionKind.ScriptBinding,
                ScriptFunctionName = binding.FunctionName
            });
        }

        return result;
    }

    /// <summary>
    /// 检查变量是否可安全删除（无引用）。
    /// </summary>
    public static bool CanSafelyDeleteVariable(HmiProject project, string variableIdOrName)
    {
        return !FindVariableReferences(project, variableIdOrName).HasReferences;
    }

    /// <summary>
    /// 检查屏幕是否可安全删除（无其他屏幕跳转到它）。
    /// </summary>
    public static bool CanSafelyDeleteScreen(HmiProject project, string screenName)
    {
        return !FindScreenReferences(project, screenName).HasReferences;
    }

    /// <summary>
    /// 获取项目中所有被引用的变量 Id/名称集合（用于悬空引用检测）。
    /// </summary>
    public static HashSet<string> GetAllReferencedVariableIds(HmiProject project)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                // 联动模型：遍历 VariableBindings 集合
                foreach (var binding in control.VariableBindings)
                {
                    if (!string.IsNullOrEmpty(binding.VariableId))
                        result.Add(binding.VariableId);
                }

                // 回退旧字段
                if (control.VariableBindings.Count == 0 && !string.IsNullOrEmpty(control.VariableBindingId))
                    result.Add(control.VariableBindingId);

                // 动作中的变量引用
                foreach (var action in control.Actions)
                {
                    if (ActionTargetsVariable(action.ActionType)
                        && !string.IsNullOrEmpty(action.Target))
                        result.Add(action.Target);
                }

                if (control.Actions.Count == 0
                    && control.Action != null
                    && ActionTargetsVariable(control.Action.ActionType)
                    && !string.IsNullOrEmpty(control.Action.Target))
                {
                    result.Add(control.Action.Target);
                }
            }
        }

        return result;
    }

    // ── P3: 悬空引用检测 ──

    /// <summary>
    /// 检测项目中所有悬空变量引用 —— 指向已删除变量的绑定。
    /// 返回 (位置, 绑定标识) 列表，不阻止保存，仅提示用户。
    ///
    /// <para><b>位置是记录体、不是拼好的中文串</b>（阶段 93，闭环登记 #61l）：
    /// 它带<b>定位码</b>与<b>实参</b>，由 App 侧 <c>ExternalText.DescribeLocation</c> 按界面语言
    /// 渲染（<c>gen.loc.*</c>）；<see cref="DanglingRefLocation.Message"/> 是**字典缺键时的回落原文**。
    /// 改动前这里直接拼中文串、调用方给 <c>LocationCode</c> 留空 ⇒ 英文界面下中英混排。</para>
    /// </summary>
    public static List<(DanglingRefLocation Location, string BindingId)> FindDanglingReferences(HmiProject project)
    {
        var result = new List<(DanglingRefLocation, string)>();

        // 收集所有有效的变量标识（Id + Name + UDT compound id + 系统变量 Id）
        var validIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in project.VariableManager.Variables)
        {
            validIds.Add(v.Id);
            validIds.Add(v.Name);

            if (v.IsUdtInstance)
            {
                var def = project.VariableManager.FindUdtDefinition(v.UdtTypeId);
                if (def != null)
                {
                    foreach (var field in def.Fields)
                        validIds.Add($"{v.Name}.{field.Name}");
                }
            }
        }

        foreach (var sv in project.VariableManager.SystemVariables)
            validIds.Add(sv.Id);

        // 检查所有控件的绑定引用
        foreach (var screen in project.Screens)
        {
            foreach (var control in screen.Controls)
            {
                // 联动模型：检查 VariableBindings 集合
                foreach (var binding in control.VariableBindings)
                {
                    if (!string.IsNullOrEmpty(binding.VariableId)
                        && !validIds.Contains(binding.VariableId))
                    {
                        result.Add((DanglingRefLocation.OfBinding(
                                screen.Name, control.Name, binding.PropertyName),
                            binding.VariableId));
                    }
                }

                // 回退旧字段
                if (control.VariableBindings.Count == 0
                    && !string.IsNullOrEmpty(control.VariableBindingId)
                    && !validIds.Contains(control.VariableBindingId))
                {
                    result.Add((DanglingRefLocation.OfLegacyBinding(screen.Name, control.Name),
                        control.VariableBindingId));
                }

                // 动作 Target 悬空检测（联动模型：遍历 Actions 集合）
                foreach (var action in control.Actions)
                {
                    if (!string.IsNullOrEmpty(action.Target)
                        && ActionTargetsVariable(action.ActionType)
                        && !validIds.Contains(action.Target))
                    {
                        result.Add((DanglingRefLocation.OfAction(screen.Name, control.Name),
                            action.Target));
                    }
                }

                // 回退旧字段
                if (control.Actions.Count == 0
                    && control.Action != null
                    && !string.IsNullOrEmpty(control.Action.Target)
                    && ActionTargetsVariable(control.Action.ActionType)
                    && !validIds.Contains(control.Action.Target))
                {
                    result.Add((DanglingRefLocation.OfAction(screen.Name, control.Name),
                        control.Action.Target));
                }
            }
        }

        return result;
    }

    // ── 报警分组引用校验 ──

    /// <summary>
    /// 检查报警配置中的 GroupId 引用是否指向有效的报警分组。
    /// 返回 (报警名称, 悬空 GroupId) 列表。
    /// </summary>
    public static List<(string AlarmName, string GroupId)> FindDanglingAlarmGroupRefs(HmiProject project)
    {
        var result = new List<(string, string)>();
        var validGroupIds = project.AlarmGroups.Select(g => g.Id).ToHashSet();

        foreach (var group in project.AlarmGroups)
        {
            foreach (var alarm in group.Alarms)
            {
                if (!string.IsNullOrEmpty(alarm.GroupId) && !validGroupIds.Contains(alarm.GroupId))
                    result.Add((alarm.Name, alarm.GroupId));
            }
        }

        return result;
    }

    // ── 内部辅助 ──

    private static bool MatchesVariable(
        string? bindingId, string targetIdOrName, HmiProject project)
    {
        if (string.IsNullOrEmpty(bindingId)) return false;

        // 直接匹配
        if (string.Equals(bindingId, targetIdOrName, StringComparison.Ordinal))
            return true;

        // 通过变量名匹配（bindingId 可能是 Id，target 可能是名称，反之亦然）
        var variable = project.VariableManager.Variables.FirstOrDefault(v =>
            v.Id == targetIdOrName || v.Name == targetIdOrName);
        if (variable != null)
        {
            if (string.Equals(bindingId, variable.Id, StringComparison.Ordinal))
                return true;
            if (string.Equals(bindingId, variable.Name, StringComparison.Ordinal))
                return true;
            // UDT 字段：bindingId 可能是 "motor1.speed"，target 可能是变量 Id
            if (bindingId.Contains('.') && variable.IsUdtInstance)
            {
                var dotIndex = bindingId.IndexOf('.');
                var instanceName = bindingId[..dotIndex];
                if (string.Equals(instanceName, variable.Name, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private static bool ActionReferencesVariable(
        HmiAction action, string variableIdOrName, HmiProject project)
    {
        if (!ActionTargetsVariable(action.ActionType)) return false;
        if (string.IsNullOrEmpty(action.Target)) return false;

        return MatchesVariable(action.Target, variableIdOrName, project);
    }

    private static bool ActionTargetsVariable(string actionType) => actionType switch
    {
        "SetVariable" or "SetBit" or "ResetBit" or "ToggleBit"
            or "MomentaryOn" or "MomentaryOff" => true,
        _ => false
    };
}
