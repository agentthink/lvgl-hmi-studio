namespace HmiEditor.Core.Models.Animation;

/// <summary>
/// 动画关键帧 —— 定义动画时间轴上的一个状态点。
/// 对应 LVGL 动画中通过 lv_anim_set_start_values / lv_anim_set_end_values 定义的起止值。
/// 支持多关键帧的线性插值。
/// </summary>
public class AnimationKeyframe
{
    /// <summary>关键帧时间点（占动画总时长的百分比，0-100）</summary>
    public double TimePercent { get; set; }

    /// <summary>目标属性值</summary>
    public double Value { get; set; }

    /// <summary>此帧到下一帧的缓动函数（null 表示使用动画的全局设置）</summary>
    public AnimationPathType? PathType { get; set; }

    /// <summary>备注说明（可选）</summary>
    public string? Description { get; set; }
}
