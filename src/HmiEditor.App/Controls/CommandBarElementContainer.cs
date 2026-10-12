using Avalonia.Controls;

namespace HmiEditor.App.Controls;

/// <summary>
/// 自定义命令栏元素容器 —— 实现 ICommandBarElement 接口，
/// 允许在 Avalonia 原生 CommandBar 中嵌入任意非按钮 UI 元素（如 TextBlock、ComboBox）。
/// </summary>
public class CommandBarElementContainer : ContentControl, ICommandBarElement
{
    /// <summary>
    /// 是否紧凑模式（仅图标，无标签）。
    /// </summary>
    public bool IsCompact { get; set; }

    /// <summary>
    /// 是否当前在溢出弹出菜单中显示。由 CommandBar 自动设置。
    /// </summary>
    public bool IsInOverflow { get; set; }
}