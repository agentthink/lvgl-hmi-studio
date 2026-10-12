using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HmiEditor.Core.CodeGen;
using HmiEditor.Core.Models;

namespace HmiEditor.CodeGen;

/// <summary>
/// CodeGenerator 分部 —— **状态文本 / 状态垂直对齐的运行时回调**（阶段 229，登记 `#92` 的功能那半）。
///
/// <para><b>为什么必须用回调</b>：LVGL 的静态样式**表达不了**"按下时换文本"，而垂直对齐更是
/// 连状态化样式属性都没有（Normal 态那条路是 `LV_SIZE_CONTENT` ＋ 重定位的**变换**实现的，
/// 变换不能带状态选择器）。⇒ 只有 `LV_EVENT_STATE_CHANGED` ＋ 回调里按当前状态改写，
/// 面板上设的东西才在设备上**真的生效**。</para>
///
/// <para><b>零影响</b>：没有任何非 Normal 态覆盖 `Text`/`TextVAlign` 的控件 ⇒ 不生成函数、
/// 不注册事件（生成物逐字节不变）。</para>
/// </summary>
public partial class CodeGenerator
{
    /// <summary>该类型能不能在运行时改文本（各自的 set_text API／Button 的子 label）。</summary>
    private static bool CanEmitStateText(HmiControlType type)
        => type is HmiControlType.Label or HmiControlType.CheckBox or HmiControlType.TextArea or HmiControlType.Button;

    /// <summary>
    /// 该控件是否需要状态运行时回调 —— **只在"这件事真的做得到"时**才生成函数并注册：
    /// 文本要文本类控件，垂直对齐只有 Label 有"内容高度"这个概念。
    /// 做不到的组合**保持注释声明**（不静默丢，也不注册一个空转回调）。
    /// </summary>
    private static bool NeedsVisualStateRuntimeCallback(HmiControl control)
        => control.VisualStates.Any(st =>
            st.State != VisualState.Normal
            && ((!string.IsNullOrEmpty(st.Text) && CanEmitStateText(control.Type))
                || (st.TextVAlign.HasValue && control.Type == HmiControlType.Label)));

    /// <summary>该控件是否属于"文本在控件本体上"的类型（用各自的 set_text API）。</summary>
    private static bool HasOwnTextApi(HmiControlType type)
        => type is HmiControlType.Label or HmiControlType.CheckBox or HmiControlType.TextArea;

    /// <summary>把状态里的文本写成一句 C（按类型选 API；不支持的类型返回 null，由调用方声明）。</summary>
    private string? StateTextStatement(HmiControl control, string objExpr, string text, string indent)
        => control.Type switch
        {
            HmiControlType.Label => $"{indent}lv_label_set_text({objExpr}, \"{EscapeCString(text)}\");",
            HmiControlType.CheckBox => $"{indent}lv_checkbox_set_text({objExpr}, \"{EscapeCString(text)}\");",
            HmiControlType.TextArea => $"{indent}lv_textarea_set_text({objExpr}, \"{EscapeCString(text)}\");",
            HmiControlType.Button =>
                $"{indent}{{ lv_obj_t* _vs_lbl = lv_obj_get_child({objExpr}, 0); if (_vs_lbl) lv_label_set_text(_vs_lbl, \"{EscapeCString(text)}\"); }}",
            _ => null,
        };

    /// <summary>
    /// 状态垂直对齐的重定位语句（与 Normal 态那条路同一手法：`LV_SIZE_CONTENT` ＋ 重定位）。
    /// 只有 Label 走这条路（其它类型没有"内容高度"这个概念）；其余返回 null。
    /// </summary>
    private string? StateVAlignStatements(HmiControl control, string objExpr, TextVAlignMode align, string indent)
    {
        if (control.Type != HmiControlType.Label) return null;

        var h = CIdentifier.Prefixed("_vs_h_", control.Id);
        var expr = align switch
        {
            TextVAlignMode.Top => CDim(control.Y),
            TextVAlignMode.Bottom => $"{CDim(control.Y)} + {CDim(control.Height)} - {h}",
            _ => $"{CDim(control.Y)} + ({CDim(control.Height)} - {h}) / 2",
        };

        return string.Join("\n", new[]
        {
            $"{indent}lv_obj_set_height({objExpr}, LV_SIZE_CONTENT);",
            $"{indent}lv_obj_update_layout({objExpr});",
            $"{indent}int32_t {h} = lv_obj_get_height({objExpr});",
            $"{indent}if ({h} > {CDim(control.Height)}) {{ {h} = {CDim(control.Height)}; lv_obj_set_height({objExpr}, {h}); }}",
            $"{indent}lv_obj_set_y({objExpr}, {expr});",
        });
    }

    /// <summary>
    /// 生成所有控件的状态运行时回调函数定义（**文件级**，必须排在 `create_screen_*` 之前）。
    /// 声明放在这里、注册放在 <see cref="GenerateVisualStates"/> 里（控件建好之后）。
    /// </summary>
    private string GenerateVisualStateRuntimeCallbacks(List<HmiControl> controls)
    {
        var sb = new StringBuilder();

        foreach (var control in controls.Where(NeedsVisualStateRuntimeCallback))
        {
            var obj = CIdentifier.ControlObject(control.Id);
            var fn = CIdentifier.Prefixed("_vs_state_", control.Id);

            sb.AppendLine();
            Cmt(sb, $"/* 状态文本 / 状态垂直对齐的运行时切换（LV_EVENT_STATE_CHANGED，控件 {EscapeComment(control.Name)}）*/");
            sb.AppendLine($"static void {fn}(lv_event_t* e)");
            sb.AppendLine("{");
            sb.AppendLine($"    lv_obj_t* obj = lv_event_get_target(e);");
            sb.AppendLine($"    lv_state_t st = lv_obj_get_state(obj);");

            var branches = new List<string>();
            foreach (var st in control.VisualStates)
            {
                var lvState = st.State switch
                {
                    VisualState.Pressed => "LV_STATE_PRESSED",
                    VisualState.Checked => "LV_STATE_CHECKED",
                    VisualState.Disabled => "LV_STATE_DISABLED",
                    _ => null,
                };
                if (lvState == null) continue;

                var body = new List<string>();
                if (!string.IsNullOrEmpty(st.Text))
                {
                    var textStmt = StateTextStatement(control, "obj", st.Text!, "        ");
                    body.Add(textStmt ?? $"        /* 状态文本 \"{EscapeComment(st.Text!)}\"：控件类型 {control.Type} 不支持运行时改文本 */");
                }
                if (st.TextVAlign.HasValue)
                {
                    var alignStmt = StateVAlignStatements(control, "obj", st.TextVAlign.Value, "        ");
                    body.Add(alignStmt ?? $"        /* 状态垂直对齐 {st.TextVAlign.Value}：控件类型 {control.Type} 不支持运行时重定位 */");
                }

                if (body.Count == 0) continue;   // 该态没有需要运行时处理的东西 ⇒ 不生成空分支
                branches.Add($"    if (st & {lvState})\n    {{\n{string.Join("\n", body)}\n    }}");
            }

            // 回到默认态：把基础文本 / 基础垂直对齐复位（否则"按下改的字"会一直留在控件上）
            var reset = new List<string>();
            if (!string.IsNullOrEmpty(control.Text))
            {
                var baseStmt = StateTextStatement(control, "obj", control.Text!, "        ");
                if (baseStmt != null) reset.Add(baseStmt);
            }
            if (control.Type == HmiControlType.Label && control.TextVAlign != TextVAlignMode.Middle)
            {
                var baseAlign = StateVAlignStatements(control, "obj", control.TextVAlign, "        ");
                if (baseAlign != null) reset.Add(baseAlign);
            }

            if (branches.Count > 0)
            {
                sb.AppendLine(branches[0]);
                for (var i = 1; i < branches.Count; i++)
                {
                    var firstLine = branches[i].Split('\n')[0];
                    sb.AppendLine($"    else {firstLine.TrimStart()}");
                    sb.AppendLine(string.Join("\n", branches[i].Split('\n').Skip(1)));
                }
            }

            if (reset.Count > 0)
            {
                sb.AppendLine(branches.Count > 0 ? "    else" : "    if (true)");
                sb.AppendLine("    {");
                sb.AppendLine(string.Join("\n", reset));
                sb.AppendLine("    }");
            }

            sb.AppendLine("}");
        }

        return sb.ToString();
    }
}
