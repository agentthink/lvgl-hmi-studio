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
/// CodeGenerator 分部 —— 状态化样式覆盖发射 —— GenerateVisualStates。
/// </summary>
public partial class CodeGenerator
{
    /// <summary>
    /// 生成状态化样式覆盖 —— 遍历 VisualStates 输出 LV_STATE_PRESSED/CHECKED/DISABLED 的样式设置。
    /// 仅生成该态显式配置的字段，未配置字段继承默认态（LVGL 行为）。
    /// 同时以注释形式声明图片资源依赖（实际路径由构建系统拷贝）。
    /// </summary>
    /// <param name="availableImageIds">可用图片资源 sanitized ID 集合；null 表示不过滤（兼容旧调用）。</param>
    private void GenerateVisualStates(StringBuilder sb, HmiControl control, HmiTheme? theme, HashSet<string>? availableImageIds = null)
    {
        foreach (var st in control.VisualStates)
        {
            var lvState = st.State switch
            {
                VisualState.Pressed => "LV_STATE_PRESSED",
                VisualState.Checked => "LV_STATE_CHECKED",
                VisualState.Disabled => "LV_STATE_DISABLED",
                _ => null
            };
            // Normal 态由默认属性（上方已生成）处理，跳过
            if (lvState == null) continue;

            Cmt(sb, $"    /* Visual state: {st.State} */");

            if (!string.IsNullOrEmpty(st.BgColor))
            {
                sb.AppendLine($"    lv_obj_set_style_bg_color({CIdentifier.ControlObject(control.Id)}, {theme?.ToLvglColor(st.BgColor) ?? $"lv_color_hex(0x{st.BgColor.TrimStart('#')})"}, {lvState});");
            }
            if (!string.IsNullOrEmpty(st.TextColor))
            {
                sb.AppendLine($"    lv_obj_set_style_text_color({CIdentifier.ControlObject(control.Id)}, {theme?.ToLvglColor(st.TextColor) ?? $"lv_color_hex(0x{st.TextColor.TrimStart('#')})"}, {lvState});");
            }
            if (!string.IsNullOrEmpty(st.BorderColor))
            {
                sb.AppendLine($"    lv_obj_set_style_border_color({CIdentifier.ControlObject(control.Id)}, {theme?.ToLvglColor(st.BorderColor) ?? $"lv_color_hex(0x{st.BorderColor.TrimStart('#')})"}, {lvState});");
            }
            if (!string.IsNullOrEmpty(st.ImageAssetId))
            {
                var sanitizedId = CIdentifier.ForAsset(st.ImageAssetId);
                if (availableImageIds == null || availableImageIds.Contains(sanitizedId))
                {
                    Cmt(sb, $"    // State image asset: {st.ImageAssetId} — 使用 FS_STDIO 路径加载");
                    sb.AppendLine($"    lv_image_set_src({CIdentifier.ControlObject(control.Id)}, {CResourceDeclarations.ImagePathMacro(sanitizedId)});");
                }
                else
                {
                    Cmt(sb, $"    // 跳过：图片资源 {st.ImageAssetId} 不在 AssetLibrary 中");
                }
            }
            // 状态文本切换需运行时事件处理（LVGL 静态样式无法表达），以注释声明
            if (!string.IsNullOrEmpty(st.Text))
            {
                Cmt(sb, CanEmitStateText(control.Type)
                    ? $"    // State text: \"{st.Text}\" —— 运行时由 _vs_state_ 回调切换（见上方注册）"
                    : $"    // State text: \"{st.Text}\" —— 控件类型 {control.Type} 不支持运行时改文本，本行**仅声明**");
            }
            // 状态水平对齐：text_align 是样式属性，可带状态生效
            if (st.TextAlign.HasValue)
            {
                sb.AppendLine($"    lv_obj_set_style_text_align({CIdentifier.ControlObject(control.Id)}, {LvglTextAlign(st.TextAlign.Value)}, {lvState});");
            }
            // 🩸 阶段 202（登记 #92）：状态**垂直**对齐 —— LVGL **没有** vertical-align 样式属性
            //   （Normal 态那条路是用 `LV_SIZE_CONTENT` + 重定位的**变换**实现的，
            //    见 `CodeGenerator.Screen.cs` 的 Label 分支），而**变换不能带状态选择器**
            //   ⇒ 非 Normal 态的垂直对齐**结构上无法**用静态样式表达，只能声明意图、由运行时回调实现。
            //   此前这一格**连注释都没有**：面板给用户设了、预览也照着渲染了，导出却彻底丢失
            //   （`Text` 至少有上面那条注释）。现在两侧都**声明**出来，账就明了。
            if (st.TextVAlign.HasValue)
            {
                Cmt(sb, $"    // State vertical align: {st.TextVAlign.Value} "
                    + "—— LVGL 无状态化 vertical-align，运行时由 _vs_state_ 回调重定位");
            }
        }

        // 阶段 229（#92 功能那半）：有非 Normal 态的 `Text`/`TextVAlign` 覆盖 ⇒ **注册运行时回调**
        //   （函数定义由 `GenerateVisualStateRuntimeCallbacks` 排在 create_screen_* 之前）
        if (NeedsVisualStateRuntimeCallback(control))
        {
            var fn = CIdentifier.Prefixed("_vs_state_", control.Id);
            Cmt(sb, $"    // 状态文本/垂直对齐：运行时由 {fn} 按当前状态切换（静态样式表达不了）");
            sb.AppendLine($"    lv_obj_add_event_cb({CIdentifier.ControlObject(control.Id)}, {fn}, LV_EVENT_STATE_CHANGED, NULL);");
        }

        // 主图（Normal 态）：加载图片资源并设置到控件
        if (!string.IsNullOrEmpty(control.ImageAssetId))
        {
            var sanitizedId = CIdentifier.ForAsset(control.ImageAssetId);
            if (availableImageIds == null || availableImageIds.Contains(sanitizedId))
            {
                sb.AppendLine($"    /* Load image from asset: {EscapeComment(control.ImageAssetId)} — 使用 FS_STDIO 路径加载实际文件 */");
                sb.AppendLine($"    lv_image_set_src({CIdentifier.ControlObject(control.Id)}, {CResourceDeclarations.ImagePathMacro(sanitizedId)});");
            }
            else
            {
                Cmt(sb, $"    // 跳过：主图资源 {control.ImageAssetId} 不在 AssetLibrary 中");
            }
        }

        // Normal 态文本位置：通过按钮 label 子对象（child 0）的 9 宫格对齐实现
        if (control.Type == HmiControlType.Button
            && (control.TextAlign != TextAlignMode.Center || control.TextVAlign != TextVAlignMode.Middle))
        {
            var lbl = CIdentifier.Prefixed("_lbl_", control.Id);
            sb.AppendLine($"    lv_obj_t* {lbl} = lv_obj_get_child({CIdentifier.ControlObject(control.Id)}, 0);");
            sb.AppendLine($"    if ({lbl}) lv_obj_set_align({lbl}, {LvglAlign(control.TextAlign, control.TextVAlign)});");
        }
    }
}
