using System.Text;
using HmiEditor.Core.Design;

namespace HmiEditor.CodeGen;

/// <summary>
/// 视觉属性绑定（颜色 / 可见性）刷新语句的**唯一实现** —— PC 模拟器与设备端 .so 共用。
///
/// <para>与 <see cref="BindingSemantics"/> 同一个动机：三端各写一套求值规则必然漂移
/// （设计器显示 12.34、设备显示 12.3）。所以「属性名 → 视觉类别 → LVGL 调用」
/// 只在注册表（<see cref="BindingVisualProperty"/>）+ 本文件这一处定义，两端只做
/// 「取值来源」的差异（模拟器正弦波 / 设备 get_tag_value）。</para>
///
/// <para><b>颜色为什么是数值</b>：生成的 C 是静态类型，三元 <c>cond ? "#ff0000" : "#00ff00"</c>
/// 两个分支类型不同（<c>int</c> vs <c>const char*</c>）**直接编译不过**。
/// 故表达式一律返回 24 位 RGB 数值，这里按 <c>lv_color_hex((uint32_t)(v))</c> 咬合 ——
/// 与设计器的 <see cref="BindingVisualProperty.ColorFromNumber"/> 是同一套数值规则
/// （高位与负数都按位截断）。</para>
/// </summary>
internal static class VisualBindingCodeGen
{
    /// <summary>
    /// 发射一条视觉属性刷新语句。
    /// </summary>
    /// <param name="sb">目标缓冲。</param>
    /// <param name="indent">缩进（含前导空格）。</param>
    /// <param name="objExpr">控件对象 C 表达式，如 <c>obj_btn_1</c>。</param>
    /// <param name="valueExpr">数值型 C 表达式（绑定求值结果）。</param>
    /// <param name="property">目标视觉属性（注册表条目）。</param>
    public static void EmitRefresh(
        StringBuilder sb,
        string indent,
        string objExpr,
        string valueExpr,
        BindingVisualProperty property)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentNullException.ThrowIfNull(property);

        if (property == BindingVisualProperty.TextColor)
        {
            sb.AppendLine($"{indent}lv_obj_set_style_text_color({objExpr}, lv_color_hex((uint32_t)({valueExpr})), 0);");
            return;
        }

        if (property == BindingVisualProperty.BgColor)
        {
            sb.AppendLine($"{indent}lv_obj_set_style_bg_color({objExpr}, lv_color_hex((uint32_t)({valueExpr})), 0);");
            // Label 等控件默认 bg_opa=0，只设颜色看不见 —— 与初始化发射（CodeGenerator.Screen）保持一致。
            sb.AppendLine($"{indent}lv_obj_set_style_bg_opa({objExpr}, LV_OPA_COVER, 0);");
            return;
        }

        if (property == BindingVisualProperty.IsVisible)
        {
            sb.AppendLine($"{indent}if ({valueExpr} != 0) lv_obj_clear_flag({objExpr}, LV_OBJ_FLAG_HIDDEN);");
            sb.AppendLine($"{indent}else lv_obj_add_flag({objExpr}, LV_OBJ_FLAG_HIDDEN);");
            return;
        }

        // 注册表新增了条目却忘了在这里派发 —— 静默不发射会比编译错误更难查（产物"看着对但不生效"）。
        throw new NotSupportedException(
            $"视觉属性 '{property.PropertyName}' 未在 VisualBindingCodeGen 中登记发射方式。");
    }
}
