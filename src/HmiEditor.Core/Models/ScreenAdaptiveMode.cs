namespace HmiEditor.Core.Models;

/// <summary>
/// I9: 屏幕自适应布局模式。
///
///   None — 固定坐标布局（默认，旧项目行为不变）
///   Fit  — 等比缩放 + 居中（letterbox）：保持设计比例，短边贴满，上下/左右黑边
///   Fill — 拉伸铺满：横向/纵向独立缩放至目标分辨率，无黑边但比例可能变形
///
/// 运行时由 CodeGen 生成自适应布局回调（基于 LVGL display 实际分辨率），
/// 编辑器画布提供"分辨率预览"模拟目标设备效果。
/// </summary>
public enum ScreenAdaptiveMode
{
    None = 0,
    Fit = 1,
    Fill = 2
}
