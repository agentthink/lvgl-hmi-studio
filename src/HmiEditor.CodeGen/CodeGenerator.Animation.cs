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
/// CodeGenerator 分部 —— 动画发射 —— 动画配置列表 → lv_anim_t + apply_animations()。
/// </summary>
public partial class CodeGenerator
{
    /// <summary>
    /// 生成动画 C 代码 —— 将动画配置列表转换为 LVGL lv_anim_t 调用。
    /// 生成一个 apply_animations() 入口函数，按触发方式分组启动所有启用的动画。
    /// </summary>
    public string GenerateAnimationCode(IReadOnlyList<AnimationConfig> animations)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// ── Animations ──");
        sb.AppendLine($"// Total: {animations.Count}, Enabled: {animations.Count(a => a.IsEnabled)}");
        sb.AppendLine();

        if (animations.Count == 0 || animations.All(a => !a.IsEnabled))
        {
            sb.AppendLine("// No animations configured.");
            return sb.ToString();
        }

        var enabled = animations.Where(a => a.IsEnabled).ToList();

        // 为每个动画生成独立的启动函数
        foreach (var anim in enabled)
        {
            var varName = CIdentifier.ForControlSymbol(anim.Id);
            sb.AppendLine($"/* Animation: {EscapeComment(anim.Name)} (trigger: {EscapeComment(anim.Trigger)}) */");
            sb.AppendLine($"static void start_anim_{varName}(lv_obj_t* target) {{");
            sb.AppendLine("    lv_anim_t a;");
            sb.AppendLine("    lv_anim_init(&a);");
            sb.AppendLine($"    lv_anim_set_exec_cb(&a, {anim.GetExecCbCode()});");
            sb.AppendLine("    lv_anim_set_var(&a, target);");
            sb.AppendLine($"    lv_anim_set_values(&a, {anim.StartValue:F1}, {anim.EndValue:F1});");
            sb.AppendLine($"    lv_anim_set_duration(&a, {anim.DurationMs});");
            sb.AppendLine($"    lv_anim_set_delay(&a, {anim.DelayMs});");
            sb.AppendLine($"    lv_anim_set_path_cb(&a, {anim.GetPathCCode()});");
            sb.AppendLine($"    lv_anim_set_repeat_count(&a, {anim.GetRepeatCCode()});");

            // 关键帧以注释形式输出（LVGL lv_anim 为单段动画，多关键帧需链式 anim，超出当前范围）
            if (anim.Keyframes.Count > 0)
            {
                sb.AppendLine("    /* Keyframes (reference): */");
                foreach (var kf in anim.Keyframes.OrderBy(k => k.TimePercent))
                {
                    var pathStr = kf.PathType.HasValue
                        ? AnimationPathHelper.GetPathCCode(kf.PathType.Value)
                        : anim.GetPathCCode();
                    sb.AppendLine($"    /*   {kf.TimePercent:F0}% -> {kf.Value:F1}  ({pathStr}) */");
                }
            }

            sb.AppendLine("    lv_anim_start(&a);");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // 生成入口函数 —— 按触发方式分组
        sb.AppendLine("void apply_animations(void) {");
        foreach (var anim in enabled)
        {
            var varName = CIdentifier.ForControlSymbol(anim.Id);
            var targetVar = string.IsNullOrEmpty(anim.ControlId)
                ? "lv_screen_active()"
                : $"obj_{CIdentifier.ForControlSymbol(anim.ControlId)}";

            if (anim.Trigger == "OnLoad")
            {
                sb.AppendLine($"    start_anim_{varName}({targetVar});");
            }
            else
            {
                sb.AppendLine($"    /* {anim.Trigger}: call start_anim_{varName}({targetVar}) on event */");
            }
        }
        sb.AppendLine("}");

        return sb.ToString();
    }
}
