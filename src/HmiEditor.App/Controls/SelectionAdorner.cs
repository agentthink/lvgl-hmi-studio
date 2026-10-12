using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HmiEditor.App.Controls;

/// <summary>
/// 选中装饰层 —— 顶层 Canvas，渲染选中框 + 8 点缩放手柄。
/// 只负责绘制，交互由 DesignCanvas 处理。
/// </summary>
public class SelectionAdorner : Control
{
    public static readonly StyledProperty<Rect> SelectionBoundsProperty =
        AvaloniaProperty.Register<SelectionAdorner, Rect>(nameof(SelectionBounds));

    public static readonly StyledProperty<bool> IsSelectionVisibleProperty =
        AvaloniaProperty.Register<SelectionAdorner, bool>(nameof(IsSelectionVisible), false);

    public static readonly StyledProperty<double> HandleSizeProperty =
        AvaloniaProperty.Register<SelectionAdorner, double>(nameof(HandleSize), 8.0);

    public static readonly StyledProperty<bool> ShowMultiSelectionProperty =
        AvaloniaProperty.Register<SelectionAdorner, bool>(nameof(ShowMultiSelection), false);

    public Rect SelectionBounds
    {
        get => GetValue(SelectionBoundsProperty);
        set => SetValue(SelectionBoundsProperty, value);
    }

    public bool IsSelectionVisible
    {
        get => GetValue(IsSelectionVisibleProperty);
        set => SetValue(IsSelectionVisibleProperty, value);
    }

    public double HandleSize
    {
        get => GetValue(HandleSizeProperty);
        set => SetValue(HandleSizeProperty, value);
    }

    public bool ShowMultiSelection
    {
        get => GetValue(ShowMultiSelectionProperty);
        set => SetValue(ShowMultiSelectionProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<AlignmentGuide>> AlignmentGuidesProperty =
        AvaloniaProperty.Register<SelectionAdorner, IReadOnlyList<AlignmentGuide>>(
            nameof(AlignmentGuides), Array.Empty<AlignmentGuide>());

    public IReadOnlyList<AlignmentGuide> AlignmentGuides
    {
        get => GetValue(AlignmentGuidesProperty);
        set => SetValue(AlignmentGuidesProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!IsSelectionVisible) return;

        // 选择框虚线
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1)
        {
            DashStyle = DashStyle.Dash
        };
        context.DrawRectangle(null, pen, SelectionBounds);

        // 多选模式 → 不绘制手柄
        if (ShowMultiSelection) goto DrawGuides;

        // 8 个缩放手柄（白底蓝框）
        var handleBrush = Brushes.White;
        var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1);
        var hs = HandleSize;

        var handles = GetHandlePositions();
        foreach (var pt in handles)
        {
            var rect = new Rect(pt.X - hs / 2, pt.Y - hs / 2, hs, hs);
            context.DrawRectangle(handleBrush, handlePen, rect);
        }

        DrawGuides:

        // 绘制对齐参考线（虚线红色）
        var guidePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 80, 80)), 0.8)
        {
            DashStyle = new DashStyle(new[] { 4.0, 3.0 }, 0)
        };

        var guides = AlignmentGuides;
        if (guides != null && guides.Count > 0)
        {
            foreach (var guide in guides)
            {
                if (guide.IsHorizontal)
                {
                    context.DrawLine(guidePen,
                        new Point(guide.Start, guide.Position),
                        new Point(guide.End, guide.Position));
                }
                else
                {
                    context.DrawLine(guidePen,
                        new Point(guide.Position, guide.Start),
                        new Point(guide.Position, guide.End));
                }
            }
        }
    }

    /// <summary>
    /// 返回 8 个手柄的中心位置。
    /// </summary>
    public Point[] GetHandlePositions()
    {
        var b = SelectionBounds;
        return new[]
        {
            b.TopLeft,                                      // 0: 左上
            new Point(b.Center.X, b.Top),                   // 1: 上中
            b.TopRight,                                     // 2: 右上
            new Point(b.Right, b.Center.Y),                 // 3: 右中
            b.BottomRight,                                  // 4: 右下
            new Point(b.Center.X, b.Bottom),                // 5: 下中
            b.BottomLeft,                                   // 6: 左下
            new Point(b.Left, b.Center.Y),                  // 7: 左中
        };
    }

    /// <summary>
    /// 返回 4 个角手柄的位置索引（0, 2, 4, 6）。
    /// </summary>
    public int[] GetCornerHandleIndices() => new[] { 0, 2, 4, 6 };

    /// <summary>
    /// 返回 4 个边中点手柄的位置索引（1, 3, 5, 7）。
    /// </summary>
    public int[] GetEdgeHandleIndices() => new[] { 1, 3, 5, 7 };
}

/// <summary>对齐参考线数据（视口坐标）。</summary>
public readonly record struct AlignmentGuide(double Position, bool IsHorizontal, double Start, double End);
