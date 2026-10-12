namespace HmiEditor.Core.Models.Animation;

/// <summary>
/// 动画缓动函数类型 —— 控制动画速度变化曲线。
/// 对应 LVGL 的 lv_anim_path_cb_t 回调。
/// </summary>
public enum AnimationPathType
{
    /// <summary>线性匀速</summary>
    Linear = 0,
    /// <summary>先慢后快再慢（最常用）</summary>
    EaseInOut,
    /// <summary>先慢后快</summary>
    EaseIn,
    /// <summary>先快后慢</summary>
    EaseOut,
    /// <summary>弹性效果，模拟弹簧回弹</summary>
    Bounce,
    /// <summary>过冲效果，超出目标值后回弹</summary>
    Overshoot,
    /// <summary>步进效果，没有过渡动画</summary>
    Step,
}

/// <summary>
/// 动画缓动函数中文名称辅助类。
/// </summary>
public static class AnimationPathHelper
{
    public static string GetDisplayName(AnimationPathType type) => type switch
    {
        AnimationPathType.Linear => "线性匀速",
        AnimationPathType.EaseInOut => "缓入缓出（推荐）",
        AnimationPathType.EaseIn => "缓入（先慢后快）",
        AnimationPathType.EaseOut => "缓出（先快后慢）",
        AnimationPathType.Bounce => "弹性回弹",
        AnimationPathType.Overshoot => "过冲回弹",
        AnimationPathType.Step => "步进（无过渡）",
        _ => "未知",
    };

    /// <summary>
    /// 缓动函数 → LVGL <c>lv_anim_path_cb_t</c> 函数名。
    ///
    /// <para>**唯一实现**：`lv_anim_path_*` 字面量只允许出现在本文件内。
    /// 曾有三份逐字相同的 switch（<see cref="AnimationConfig.GetPathCCode"/>、CodeGenerator 私有静态、
    /// 动画编辑器预览私有静态），任一处补值另两处不会跟着动 —— 由
    /// `AnimationPathSingleSourceTests` 的结构守卫钉住归属。</para>
    /// </summary>
    public static string GetPathCCode(AnimationPathType path) => path switch
    {
        AnimationPathType.Linear => "lv_anim_path_linear",
        AnimationPathType.EaseInOut => "lv_anim_path_ease_in_out",
        AnimationPathType.EaseIn => "lv_anim_path_ease_in",
        AnimationPathType.EaseOut => "lv_anim_path_ease_out",
        AnimationPathType.Bounce => "lv_anim_path_bounce",
        AnimationPathType.Overshoot => "lv_anim_path_overshoot",
        AnimationPathType.Step => "lv_anim_path_step",
        _ => "lv_anim_path_ease_in_out",
    };
}
