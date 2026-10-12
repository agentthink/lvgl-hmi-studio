using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models.Animation;

/// <summary>
/// 动画配置 —— 定义控件属性的动画效果。
/// 
/// 对应 LVGL v9 动画系统：
/// - lv_anim_init → 初始化动画
/// - lv_anim_set_exec_cb → 设置属性变更回调
/// - lv_anim_set_values → 设置起止值
/// - lv_anim_set_duration → 设置时长
/// - lv_anim_set_delay → 设置延迟
/// - lv_anim_set_path_cb → 设置缓动函数
/// - lv_anim_set_repeat_count → 设置循环次数
/// - lv_anim_start → 启动动画
/// </summary>
public class AnimationConfig
{
    /// <summary>唯一标识（8位十六进制）</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>动画名称，如「按钮呼吸效果」</summary>
    public string Name { get; set; } = "";

    /// <summary>目标控件 ID（绑定的控件）</summary>
    public string ControlId { get; set; } = "";

    /// <summary>动画属性（如 X/Y/Width/Height/Opacity/Radius 等）</summary>
    public string PropertyName { get; set; } = "Opacity";

    /// <summary>起始值</summary>
    public double StartValue { get; set; } = 0;

    /// <summary>结束值</summary>
    public double EndValue { get; set; } = 255;

    /// <summary>动画时长（毫秒）</summary>
    public int DurationMs { get; set; } = 500;

    /// <summary>延迟启动（毫秒）</summary>
    public int DelayMs { get; set; } = 0;

    /// <summary>缓动函数类型</summary>
    public AnimationPathType PathType { get; set; } = AnimationPathType.EaseInOut;

    /// <summary>重复次数（0=不重复，-1=无限循环，N=重复N次）</summary>
    public int RepeatCount { get; set; } = 0;

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>触发方式（OnLoad/OnClick/OnValueChange/OnPress/OnRelease）</summary>
    public string Trigger { get; set; } = "OnLoad";

    /// <summary>关键帧列表（为空时使用 StartValue→EndValue 线性过渡）</summary>
    public ObservableCollection<AnimationKeyframe> Keyframes { get; set; } = new();

    /// <summary>描述</summary>
    public string? Description { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后修改时间</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>获取缓动函数的 LVGL C 代码字符串（委托 <see cref="AnimationPathHelper.GetPathCCode"/> —— 唯一实现）</summary>
    public string GetPathCCode() => AnimationPathHelper.GetPathCCode(PathType);

    /// <summary>获取重复次数的 LVGL 宏</summary>
    public string GetRepeatCCode() => RepeatCount switch
    {
        -1 => "LV_ANIM_REPEAT_INFINITE",
        0 => "0",
        _ => RepeatCount.ToString(),
    };

    /// <summary>获取属性设置的 LVGL exec_cb 函数名</summary>
    public string GetExecCbCode() => PropertyName switch
    {
        "X" => "(lv_anim_exec_xcb_t)lv_obj_set_x",
        "Y" => "(lv_anim_exec_xcb_t)lv_obj_set_y",
        "Width" => "(lv_anim_exec_xcb_t)lv_obj_set_width",
        "Height" => "(lv_anim_exec_xcb_t)lv_obj_set_height",
        "Opacity" => "(lv_anim_exec_xcb_t)lv_obj_set_style_opa",
        "Radius" => "(lv_anim_exec_xcb_t)lv_obj_set_style_radius",
        "BorderWidth" => "(lv_anim_exec_xcb_t)lv_obj_set_style_border_width",
        "Value" => "(lv_anim_exec_xcb_t)lv_bar_set_value",
        _ => "(lv_anim_exec_xcb_t)lv_obj_set_x",
    };
}
