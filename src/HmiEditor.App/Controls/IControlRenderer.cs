using Avalonia;
using Avalonia.Media;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Controls;

/// <summary>
/// 控件渲染器扩展点 —— 插件/外部调用方可为指定控件类型注册自定义渲染器，
/// 覆盖 DesignCanvas 内置渲染或为新增控件类型提供渲染（无需修改 DesignCanvas 的 switch 分发）。
/// 通过 <see cref="DesignCanvas.RegisterRenderer"/> 注册。
/// </summary>
public interface IControlRenderer
{
    /// <summary>是否负责渲染该控件类型。</summary>
    bool CanRender(HmiControlType type);

    /// <summary>
    /// 渲染控件。rect 为控件在画布上的目标矩形（已含缩放），zoom 为当前画布缩放系数。
    /// 实现方只做绘制，不应修改模型数据。
    /// </summary>
    void Render(DrawingContext context, HmiControl control, Rect rect, double zoom);
}
