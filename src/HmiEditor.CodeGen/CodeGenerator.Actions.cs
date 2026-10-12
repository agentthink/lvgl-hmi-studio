using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HmiEditor.Core.Design;
using HmiEditor.Core.Mapping;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Animation;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Services;
using HmiEditor.Core.CodeGen;

namespace HmiEditor.CodeGen;

/// <summary>
/// CodeGenerator 分部 —— 动作与事件发射 —— 事件回调、动作体、标签读写/取反、事件注册、变换。
/// </summary>
public partial class CodeGenerator
{
    // ── M4: 动作代码生成 ──

    /// <summary>
    /// M4: 为屏幕中所有有动作的控件生成事件回调函数。
    /// 回调函数在 create_screen 函数之前声明, 通过 lv_obj_add_event_cb 注册。
    ///
    /// <para><b><paramref name="screen"/> 的用途：</b>只有它能回答「当前屏是不是登录屏」，
    /// 从而决定 <c>Login</c> 动作该发射「提交凭据」还是「跳去登录屏」。
    /// 为 null（无屏上下文）时退回后者，与既有产物逐字节一致。</para>
    /// </summary>
    private string GenerateActionCallbacks(List<HmiControl> controls, IReadOnlyList<RuntimeTag>? tags = null, HmiProject? project = null, HmiScreen? screen = null)
    {
        var sb = new StringBuilder();
        bool hasAny = false;

        foreach (var control in controls)
        {
            // 联动模型：收集该控件的所有动作（集合优先，回退旧字段）
            var controlActions = new List<HmiAction>();
            if (control.Actions.Count > 0)
                controlActions.AddRange(control.Actions.Where(a => a.ActionType != "None"));
            else if (control.Action != null && control.Action.ActionType != "None")
                controlActions.Add(control.Action);

            foreach (var action in controlActions)
            {
                hasAny = true;
                var controlId = CIdentifier.ForControlSymbol(control.Id);
                var actionSuffix = controlActions.Count > 1
                    ? "_" + CIdentifier.ForControlSymbol(action.ActionType)
                    : "";

                // MomentaryOn/MomentaryOff 需要按下和释放两个回调
                if (action.ActionType is "MomentaryOn" or "MomentaryOff")
                {
                    // 按下回调
                    Cmt(sb, $"/* {control.Name}: {action.GetDescription()} (press) */");
                    sb.AppendLine($"static void on_press_obj_{controlId}{actionSuffix}(lv_event_t* e) {{");
                    AppendActionBody(sb, action, isRelease: false, tags, project, controls, screen);
                    sb.AppendLine("}");
                    sb.AppendLine();

                    // 释放回调
                    Cmt(sb, $"/* {control.Name}: {action.GetDescription()} (release) */");
                    sb.AppendLine($"static void on_release_obj_{controlId}{actionSuffix}(lv_event_t* e) {{");
                    AppendActionBody(sb, action, isRelease: true, tags, project, controls, screen);
                    sb.AppendLine("}");
                    sb.AppendLine();
                }
                else
                {
                    var eventName = action.TriggerEvent == "OnPress" ? "press" : "click";
                    Cmt(sb, $"/* {control.Name}: {action.GetDescription()} */");
                    sb.AppendLine($"static void on_{eventName}_obj_{controlId}{actionSuffix}(lv_event_t* e) {{");
                    AppendActionBody(sb, action, isRelease: false, tags, project, controls, screen);
                    sb.AppendLine("}");
                    sb.AppendLine();
                }
            }
        }

        return hasAny ? "// ── Action Callbacks ──\n" + sb.ToString() : "";
    }

    /// <summary>
    /// 生成动作回调函数体 —— 标签表已知时写变量动作调用 set_tag_value；
    /// 屏幕切换/脚本调用/系统动作调用固件运行时 API（hmi_screen_switch 等）。
    /// <paramref name="project"/> 非空时校验 SwitchScreen/ExecuteScript 目标是否存在，
    /// 悬空目标保留 TODO 注释（与 AppendTagWrite 悬空标签一致）。
    ///
    /// <para><b><paramref name="screenControls"/> / <paramref name="screen"/> 只服务于登录动作：</b>
    /// 登录屏上的「登录」按钮要把输入框里的凭据交出去，而回调是**文件级 static 函数**、
    /// 拿不到 <c>create_screen_*()</c> 的局部变量 —— 好在 <c>obj_*</c> 本来就是文件级 static
    /// （见 <c>CodeGenerator.Screen.cs</c> 的声明段），只要在**生成期**认出是哪两个控件即可。
    /// 两者为 null 时 Login 退回「跳去登录屏」的历史语义。</para>
    /// </summary>
    private void AppendActionBody(
        StringBuilder sb,
        HmiAction action,
        bool isRelease,
        IReadOnlyList<RuntimeTag>? tags = null,
        HmiProject? project = null,
        List<HmiControl>? screenControls = null,
        HmiScreen? screen = null)
    {
        AppendPermissionGate(sb, action, project);

        var target = action.Target ?? "";
        var value = action.Value ?? "";

        switch (action.ActionType)
        {
            case "SwitchScreen":
            {
                // 目标是屏幕名（HmiScreen.Name），固件 hmi_screen_switch 按名称切换
                var screenFound = project == null || project.Screens.Any(s => s.Name == target);
                Cmt(sb, $"    /* SwitchScreen: target = \"{target}\" */");
                if (screenFound)
                {
                    sb.AppendLine($"    hmi_screen_switch(\"{EscapeCString(target)}\");");
                }
                else
                {
                    sb.AppendLine($"    /* TODO: Switch to screen '{EscapeComment(target)}' (not in screen list) */");
                }
                break;
            }

            case "SetVariable":
                AppendTagWrite(sb, target, NormalizeActionValue(value), $"/* SetVariable: {target} = {value} */", tags);
                break;

            case "SetBit":
                AppendTagWrite(sb, target, "1.0f", $"/* SetBit: {target} = 1 */", tags);
                break;

            case "ResetBit":
                AppendTagWrite(sb, target, "0.0f", $"/* ResetBit: {target} = 0 */", tags);
                break;

            case "ToggleBit":
                AppendTagToggle(sb, target, tags);
                break;

            case "MomentaryOn":
                if (isRelease)
                    AppendTagWrite(sb, target, "0.0f", $"/* MomentaryOn (release): {target} = 0 */", tags);
                else
                    AppendTagWrite(sb, target, "1.0f", $"/* MomentaryOn (press): {target} = 1 */", tags);
                break;

            case "MomentaryOff":
                if (isRelease)
                    AppendTagWrite(sb, target, "1.0f", $"/* MomentaryOff (release): {target} = 1 */", tags);
                else
                    AppendTagWrite(sb, target, "0.0f", $"/* MomentaryOff (press): {target} = 0 */", tags);
                break;

            case "ExecuteScript":
            {
                // 目标是脚本 Id（HmiScript.Id），由脚本分发表 hmi_run_script 派发
                var scriptFound = project == null || project.Scripts.Any(s => s.Id == target);
                Cmt(sb, $"    /* ExecuteScript: {target} */");
                if (scriptFound)
                {
                    sb.AppendLine($"    hmi_run_script(\"{EscapeCString(target)}\");");
                }
                else
                {
                    sb.AppendLine($"    /* TODO: Run script '{EscapeComment(target)}' (not in script list) */");
                }
                break;
            }

            case "ShowAlarms":
                Cmt(sb, "    /* ShowAlarms: navigate to alarm view */");
                sb.AppendLine("    hmi_show_alarms();");
                break;

            case "ShowTrends":
                Cmt(sb, "    /* ShowTrends: navigate to trend view */");
                sb.AppendLine("    hmi_show_trends();");
                break;

            case "Login":
                AppendLoginAction(sb, screenControls, screen, project);
                break;

            case "Logout":
                Cmt(sb, "    /* Logout: clear current user session */");
                sb.AppendLine("    hmi_logout();");
                break;

            default:
                Cmt(sb, $"    /* Unknown action: {action.ActionType} */");
                break;
        }
    }

    /// <summary>
    /// 发射 <c>Login</c> 动作 —— **两种语义**，按「当前屏是不是登录屏、且屏上认不认得出凭据输入框」分流：
    ///
    /// <list type="number">
    /// <item><b>提交凭据</b>：当前屏就是登录屏，且屏上同时找到用户名框与密码框 ⇒ 发射
    /// <c>hmi_login_submit(lv_textarea_get_text(obj_user), lv_textarea_get_text(obj_pass))</c>。
    /// 这是本轮补上的缺口：在这之前，登录屏上无论填什么（哪怕是空的）都会直接授权成功 ——
    /// 因为动作是**无参**的 <c>hmi_login()</c>，输入框里的值根本没有去路。</item>
    /// <item><b>跳去登录屏</b>：其余情况一律退回 <c>hmi_login()</c>（历史语义）。于是没配登录屏、
    /// 或登录屏上没按约定放输入框的既有工程，产物**逐字节不变**。</item>
    /// </list>
    ///
    /// <para><b>为什么认不出输入框就退回旧语义而不是报错：</b>登录屏的形状是用户自己画的，
    /// 编辑器不该因为"没按约定起名"就让整个工程编译不过。产物注释里会写明本次发射的是哪一种，
    /// 用户从生成代码一眼能看出"我的登录屏没被认出来"。</para>
    /// </summary>
    private void AppendLoginAction(StringBuilder sb, List<HmiControl>? screenControls, HmiScreen? screen, HmiProject? project)
    {
        if (ResolveLoginFields(screenControls, screen, project) is { } fields)
        {
            Cmt(sb, $"    /* Login: 提交凭据（用户名 {fields.Username.Name} → {fields.UsernameSymbol}，"
                  + $"密码 {fields.Password.Name} → {fields.PasswordSymbol}） */");
            sb.AppendLine($"    hmi_login_submit(lv_textarea_get_text({fields.UsernameSymbol}), "
                        + $"lv_textarea_get_text({fields.PasswordSymbol}));");
        }
        else
        {
            Cmt(sb, "    /* Login: show login dialog */");
            sb.AppendLine("    hmi_login();");
        }
    }

    /// <summary>
    /// 生成期解析「登录屏上的凭据输入框」—— 认不出来返回 <c>null</c>（调用方退回旧语义）。
    ///
    /// <para>三重前提缺一不可：有屏上下文、<paramref name="screen"/> 就是
    /// <see cref="ScreenNavTargets"/> 解析出的登录屏、屏上同时找得到用户名框与密码框。
    /// 只找到密码框而没有用户名框时也返回 null —— 那种形态无法确定"哪个框是账号"，
    /// 硬猜会把别的输入框当成用户名。</para>
    /// </summary>
    private static (HmiControl Username, string UsernameSymbol, HmiControl Password, string PasswordSymbol)? ResolveLoginFields(
        List<HmiControl>? screenControls, HmiScreen? screen, HmiProject? project)
    {
        if (screenControls is null || screen is null || project is null) return null;

        // "当前屏是不是登录屏"必须用与 hmi_login 同一份候选表判定 —— 否则会长出
        // "hmi_login 认为登录屏是 A、发射侧认为当前屏才是登录屏"这种分叉
        var loginScreen = ScreenNavTargets.Resolve(project, ScreenNavTargets.Login);
        if (loginScreen is null || !string.Equals(loginScreen, screen.Name, StringComparison.Ordinal))
            return null;

        var password = LoginFieldTargets.FindPasswordField(screenControls);
        if (password is null) return null;
        var username = LoginFieldTargets.FindUsernameField(screenControls, password);
        if (username is null) return null;

        return (username, CIdentifier.ControlObject(username.Id), password, CIdentifier.ControlObject(password.Id));
    }

    /// <summary>
    /// 发射动作权限门 —— 仅当工程显式配成**非**设计模式（<c>SecurityConfig.OperationMode</c>）。
    ///
    /// <para><b>为什么默认不发：</b>设计模式的语义就是"所有操作可用"，且历史导出产物都不带门
    /// ⇒ 默认产物逐字节不变，既有对拍/回归测试零改动。项目切到 Operator/Maintenance/ReadOnly
    /// 之后才开始门控：此时未登录只折算"查看画面"（见固件 <c>hmi_has_permission</c>），
    /// 要求 OperateControl 的动作会被拒。</para>
    ///
    /// <para><b>无工程上下文（<paramref name="project"/> 为 null）时也不发</b> ——
    /// 与既有 <c>SwitchScreen/ExecuteScript</c> 的悬空目标校验同一约定（null = 不校验），
    /// 免得"没传工程"从"少一条注释"升级成"顺手改变了行为"。</para>
    ///
    /// <para>权限取自 <see cref="ActionPermissionMap"/>（与生成的 generated_security.h 同源），
    /// 宏名取自 <see cref="PermissionMacros"/> —— 生成器内**不得**自己拼 <c>"HMI_PERM_…"</c>。</para>
    /// </summary>
    private static void AppendPermissionGate(StringBuilder sb, HmiAction action, HmiProject? project)
    {
        if (project == null) return;
        if (project.SecurityConfig.OperationMode == OperationMode.Designer) return;
        if (ActionPermissionMap.Required(action.ActionType) is not { } permission) return;

        sb.AppendLine(
            $"    if (!hmi_require_permission({PermissionMacros.For(permission)})) return;  " +
            $"/* {ActionTypeMeta.DisplayName(action.ActionType)} 需要 {permission} */");
    }

    /// <summary>
    /// 动作的值 → **合法的 C 浮点字面量**。
    ///
    /// <para><b>整数必须补出小数点</b>：C 里 <c>1f</c> 不是合法浮点常量（<c>f</c> 后缀要求字面量本身
    /// 带小数点或指数），而 <c>"0.########"</c> 格式化出来的整数恰好没有小数点 ⇒ 会产出 <c>1f</c>，
    /// 让**任何带「设置变量」动作的工程**在编译期就报「整数常量的 'f' 后缀无效」。
    /// 所以格式串强制一位小数（<c>"0.0########"</c>）。</para>
    ///
    /// <para><b>非有限值要挡掉</b>：<c>double.TryParse</c> 认 <c>NaN</c>/<c>Infinity</c>，
    /// 直接格式化会产出 <c>NaNf</c> 同样编不过（<c>1e999</c> 这类也会溢出成 Infinity 走到这里）。</para>
    /// </summary>
    private static string NormalizeActionValue(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number))
            return number.ToString("0.0########", CultureInfo.InvariantCulture) + "f";
        if (bool.TryParse(value, out var boolean))
            return boolean ? "1.0f" : "0.0f";
        return "0.0f";
    }

    /// <summary>
    /// 生成写标签调用 —— 标签表已知且目标变量在表内时输出 set_tag_value(TAG_x, value)。
    /// 否则保留 TODO 注释（悬空绑定或标签表未生成）。
    /// </summary>
    private void AppendTagWrite(StringBuilder sb, string variableId, string valueExpr, string comment, IReadOnlyList<RuntimeTag>? tags)
    {
        var tag = tags?.FirstOrDefault(t => t.VariableId == variableId);
        if (tag != null)
        {
            var tagConst = "TAG_" + RuntimeTagsCodeGen.SanitizeIdentifierPublic(tag.VariableId);
            sb.AppendLine($"    {EscapeComment(comment)}");
            sb.AppendLine($"    set_tag_value({tagConst}, {valueExpr});");
        }
        else
        {
            sb.AppendLine($"    {EscapeComment(comment)}");
            sb.AppendLine($"    /* TODO: Write {valueExpr} to variable '{EscapeComment(variableId)}' (not in tag table) */");
        }
    }

    /// <summary>生成取反写标签调用 —— ToggleBit 先读再写回。</summary>
    private void AppendTagToggle(StringBuilder sb, string variableId, IReadOnlyList<RuntimeTag>? tags)
    {
        var tag = tags?.FirstOrDefault(t => t.VariableId == variableId);
        if (tag != null)
        {
            var tagConst = "TAG_" + RuntimeTagsCodeGen.SanitizeIdentifierPublic(tag.VariableId);
            Cmt(sb, $"    /* ToggleBit: {variableId} */");
            sb.AppendLine($"    float _v = get_tag_value({tagConst});");
            sb.AppendLine($"    set_tag_value({tagConst}, _v > 0.5f ? 0.0f : 1.0f);");
        }
        else
        {
            Cmt(sb, $"    /* ToggleBit: {variableId} */");
            sb.AppendLine($"    /* TODO: Toggle bit in variable '{EscapeComment(variableId)}' (not in tag table) */");
        }
    }

    /// <summary>在 create_screen 函数中为控件追加事件注册调用。</summary>
    /// <remarks>多动作场景下回调名带 _&lt;ActionType&gt; 后缀，须与 GenerateActionCallbacks 的命名保持一致。</remarks>
    private void AppendEventRegistration(StringBuilder sb, HmiControl control, HmiAction action)
    {
        var controlId = CIdentifier.ForControlSymbol(control.Id);

        // 与 GenerateActionCallbacks 相同的多动作后缀逻辑：控件有 >1 个非 None 动作时加 _<ActionType>
        var nonNoneActions = control.Actions.Count > 0
            ? control.Actions.Where(a => a.ActionType != "None").ToList()
            : (control.Action != null && control.Action.ActionType != "None"
                ? new List<HmiAction> { control.Action }
                : new List<HmiAction>());
        var actionSuffix = nonNoneActions.Count > 1 ? "_" + CIdentifier.ForControlSymbol(action.ActionType) : "";

        if (action.ActionType is "MomentaryOn" or "MomentaryOff")
        {
            // 需要注册按下和释放两个事件
            sb.AppendLine($"    lv_obj_add_event_cb({CIdentifier.ControlObject(control.Id)}, on_press_obj_{controlId}{actionSuffix}, LV_EVENT_PRESSED, NULL);");
            sb.AppendLine($"    lv_obj_add_event_cb({CIdentifier.ControlObject(control.Id)}, on_release_obj_{controlId}{actionSuffix}, LV_EVENT_RELEASED, NULL);");
        }
        else
        {
            var eventName = action.TriggerEvent == "OnPress" ? "press" : "click";
            var lvEvent = action.TriggerEvent == "OnPress" ? "LV_EVENT_PRESSED" : "LV_EVENT_CLICKED";
            sb.AppendLine($"    lv_obj_add_event_cb({CIdentifier.ControlObject(control.Id)}, on_{eventName}_obj_{controlId}{actionSuffix}, {lvEvent}, NULL);");
        }
    }

    /// <summary>
    /// 生成控件旋转/镜像变换（围绕控件中心，与设计器 BuildControlTransform 语义一致）。
    /// - 旋转：transform_rotation（0.1° 单位，900 = 90°），模拟器已验证生效；逆时针用负角度。
    /// - 镜像：**真的发射** `transform_scale_x/y = -256`。
    ///   ⚠ **阶段 203 勘误**：这段注释此前写着「LVGL 9 对象级 transform_scale_x/y 负值存在绘制区域缺陷
    ///   （实验确认整个对象不渲染）⇒ 只输出注释声明、固件侧需在渲染矩阵层实现」——
    ///   但**代码从来没有那样做过**：它一直照发那两个调用，且 `TransformFeatureTests` 有三条判据
    ///   （`Gen_MirrorX_EmitsScaleXNegative` / `Gen_MirrorY_…` / `Gen_MirrorBoth_…`）钉的正是"真发射"。
    ///   ⇒ 结论：**注释过时**，以代码与判据为准。
    /// </summary>
    private static void AppendTransform(StringBuilder sb, string objName, HmiControl control)
    {
        if (!control.HasTransform) return;

        var rotationDeg = control.EffectiveRotation();
        var hasRotation = rotationDeg != 0;
        var hasMirror = control.MirrorX || control.MirrorY;
        if (!hasRotation && !hasMirror) return;

        // pivot 恒为控件中心：旋转/镜像都围绕中心（与设计器 BuildControlTransform 一致）
        sb.AppendLine($"    /* 旋转/镜像（围绕控件中心，与设计器一致） */");
        sb.AppendLine($"    lv_obj_set_style_transform_pivot_x({objName}, {(int)(control.Width / 2)}, 0);");
        sb.AppendLine($"    lv_obj_set_style_transform_pivot_y({objName}, {(int)(control.Height / 2)}, 0);");
        if (hasRotation)
            sb.AppendLine($"    lv_obj_set_style_transform_rotation({objName}, {(int)Math.Round(rotationDeg * 10)}, 0);");
        if (control.MirrorX)
            sb.AppendLine($"    lv_obj_set_style_transform_scale_x({objName}, -256, 0);");
        if (control.MirrorY)
            sb.AppendLine($"    lv_obj_set_style_transform_scale_y({objName}, -256, 0);");
        sb.AppendLine($"    lv_obj_add_flag({objName}, LV_OBJ_FLAG_OVERFLOW_VISIBLE);");
    }
}
