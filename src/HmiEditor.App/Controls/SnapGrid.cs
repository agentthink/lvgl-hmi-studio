using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;

namespace HmiEditor.App.Controls;

/// <summary>
/// 网格吸附背景层 —— 底层 Canvas，渲染对齐网格点。
/// </summary>
public class SnapGrid : Control
{
    /// <summary>
    /// 网格点间距。
    /// <para>默认值 10 必须与 <c>SettingsService.Interaction.GridSize</c> 的默认值一致 ——
    /// 前者是"没人注入时的兜底"，后者是"用户偏好出厂值"，由默认值对齐守卫钉住。</para>
    /// </summary>
    public static readonly StyledProperty<double> GridSizeProperty =
        AvaloniaProperty.Register<SnapGrid, double>(nameof(GridSize), 10.0);

    public static readonly StyledProperty<Color> DotColorProperty =
        AvaloniaProperty.Register<SnapGrid, Color>(nameof(DotColor), Color.FromRgb(60, 70, 90));

    public double GridSize
    {
        get => GetValue(GridSizeProperty);
        set => SetValue(GridSizeProperty, value);
    }

    public Color DotColor
    {
        get => GetValue(DotColorProperty);
        set => SetValue(DotColorProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var dotBrush = new SolidColorBrush(DotColor);
        var dotSize = 1.0;

        for (double x = 0; x < Bounds.Width; x += GridSize)
        {
            for (double y = 0; y < Bounds.Height; y += GridSize)
            {
                context.DrawRectangle(dotBrush, null,
                    new Rect(x - dotSize / 2, y - dotSize / 2, dotSize, dotSize));
            }
        }
    }
}
