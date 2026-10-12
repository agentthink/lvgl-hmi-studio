using Avalonia;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Controls;

/// <summary>
/// 画布拖放预览状态。仅描述视图层幽灵控件，不持有或修改工程模型。
/// </summary>
public sealed class CanvasDragPreviewState
{
    public HmiControlType? ControlType { get; private set; }
    public Rect? ViewRect { get; private set; }
    public bool IsValid { get; private set; }
    public bool IsVisible => ViewRect.HasValue;

    public void Show(HmiControlType type, Rect viewRect, bool isValid)
    {
        ControlType = type;
        ViewRect = viewRect;
        IsValid = isValid;
    }

    public void Clear()
    {
        ControlType = null;
        ViewRect = null;
        IsValid = false;
    }
}
