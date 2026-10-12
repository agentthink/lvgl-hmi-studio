using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using HmiEditor.App.Resources;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Views;

/// <summary>
/// 导航流可视化 UserControl —— 深色主题，Canvas 绘制节点（矩形 + 屏幕名）
/// 与边（连接线 + 箭头）。顶部工具条提供刷新/保存/加载。
/// </summary>
public partial class NavigationFlowView : UserControl
{
    private NavigationGraph? _graph;
    private HmiProject? _project;

    private const double NodeWidth = 160;
    private const double NodeHeight = 56;

    /// <summary>导航图发生变更时触发。</summary>
    public event EventHandler? GraphChanged;

    /// <summary>双击节点时触发，参数为屏幕 Id（用于跳转对应屏幕）。</summary>
    public event EventHandler<string>? NodeActivated;

    public NavigationFlowView()
    {
        InitializeComponent();

        BtnRefresh.Click += (_, _) => Refresh();
        BtnSave.Click += OnSave;
        BtnLoad.Click += OnLoad;

        // 转发事件到 DockLayoutState 聚合器，确保 Dock 重建后仍能到达 MainWindow
        GraphChanged += (_, _) => DockLayoutState.RaiseNavGraphChanged(this, EventArgs.Empty);
        NodeActivated += (_, screenId) => DockLayoutState.RaiseNavNodeActivated(this, screenId);
    }

    /// <summary>
    /// 加载项目并重建导航图（从 SwitchScreen 动作提取边）。
    /// </summary>
    public void LoadProject(HmiProject project)
    {
        _project = project;
        _graph = NavigationGraph.BuildFromProject(project);
        DrawGraph();
        GraphChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>重绘当前导航图；若项目已加载则从项目重建。</summary>
    public void Refresh()
    {
        if (_project != null)
        {
            _graph = NavigationGraph.BuildFromProject(_project);
        }
        DrawGraph();
        GraphChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>保存导航图到文件（由宿主可通过 GraphChanged 监听处理具体路径）。</summary>
    private void OnSave(object? sender, EventArgs e)
    {
        if (_graph == null)
        {
            StatusText.Text = AppLocalization.Service[Lang.NavFlowNothing];
            return;
        }
        GraphChanged?.Invoke(this, EventArgs.Empty);
        StatusText.Text = AppLocalization.Service.Format(Lang.NavFlowSaved, _graph.Nodes.Count, _graph.Edges.Count);
    }

    /// <summary>加载导航图文件（触发事件由宿主处理具体路径）。</summary>
    private void OnLoad(object? sender, EventArgs e)
    {
        GraphChanged?.Invoke(this, EventArgs.Empty);
        StatusText.Text = AppLocalization.Service[Lang.NavFlowLoaded];
    }

    // ═══════════════════════════════════════════════
    //  绘制
    // ═══════════════════════════════════════════════

    private void DrawGraph()
    {
        GraphCanvas.Children.Clear();

        if (_graph == null || _graph.Nodes.Count == 0)
        {
            EmptyHint.IsVisible = true;
            GraphCanvas.Width = 400;
            GraphCanvas.Height = 300;
            StatusText.Text = "";
            return;
        }

        EmptyHint.IsVisible = false;

        // 先画边（位于下层）
        foreach (var edge in _graph.Edges)
        {
            DrawEdge(edge);
        }

        // 再画节点（位于上层）
        foreach (var node in _graph.Nodes)
        {
            DrawNode(node);
        }

        // 调整 Canvas 尺寸以适配滚动
        var maxX = _graph.Nodes.Max(n => n.X) + NodeWidth + 60;
        var maxY = _graph.Nodes.Max(n => n.Y) + NodeHeight + 60;
        GraphCanvas.Width = Math.Max(400, maxX);
        GraphCanvas.Height = Math.Max(300, maxY);

        StatusText.Text = AppLocalization.Service.Format(Lang.NavFlowSummary, _graph.Nodes.Count, _graph.Edges.Count);
    }

    private void DrawNode(NavigationNode node)
    {
        var border = new Border
        {
            Width = NodeWidth,
            Height = NodeHeight,
            Background = HmiEditor.App.Theme.BgPanel,
            BorderBrush = HmiEditor.App.Theme.Link,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Tag = node
        };

        var text = new TextBlock
        {
            Text = node.ScreenName,
            Foreground = HmiEditor.App.Theme.TextPrimary,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        border.Child = text;

        Canvas.SetLeft(border, node.X);
        Canvas.SetTop(border, node.Y);

        // 双击跳转对应屏幕
        border.PointerPressed += (sender, e) =>
        {
            if (e.ClickCount >= 2 && sender is Border b && b.Tag is NavigationNode n)
            {
                NodeActivated?.Invoke(this, n.ScreenId);
                e.Handled = true;
            }
        };

        GraphCanvas.Children.Add(border);
    }

    private void DrawEdge(NavigationEdge edge)
    {
        var source = _graph!.Nodes.FirstOrDefault(n => n.Id == edge.SourceNodeId);
        var target = _graph.Nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
        if (source == null || target == null) return;

        var sourceCenter = new Point(source.X + NodeWidth / 2, source.Y + NodeHeight / 2);
        var targetCenter = new Point(target.X + NodeWidth / 2, target.Y + NodeHeight / 2);

        // 计算连接线在目标矩形边界上的交点（避免穿透节点）
        var targetEdgePoint = GetRectBorderPoint(targetCenter, sourceCenter, NodeWidth, NodeHeight);
        var sourceEdgePoint = GetRectBorderPoint(sourceCenter, targetCenter, NodeWidth, NodeHeight);

        var line = new Line
        {
            StartPoint = sourceEdgePoint,
            EndPoint = targetEdgePoint,
            Stroke = HmiEditor.App.Theme.Link,
            StrokeThickness = 1.5
        };
        GraphCanvas.Children.Add(line);

        // 箭头
        var arrow = CreateArrow(sourceEdgePoint, targetEdgePoint);
        GraphCanvas.Children.Add(arrow);

        // 边标签
        if (!string.IsNullOrEmpty(edge.Label))
        {
            var midX = (sourceEdgePoint.X + targetEdgePoint.X) / 2;
            var midY = (sourceEdgePoint.Y + targetEdgePoint.Y) / 2;
            var label = new TextBlock
            {
                Text = edge.Label,
                Foreground = HmiEditor.App.Theme.TextSecondary,
                FontSize = 10,
                Padding = new Thickness(3, 0)
            };
            Canvas.SetLeft(label, midX - 30);
            Canvas.SetTop(label, midY - 8);
            GraphCanvas.Children.Add(label);
        }
    }

    /// <summary>计算从矩形中心射向外部某点时，与矩形边界的交点。</summary>
    private static Point GetRectBorderPoint(Point rectCenter, Point external, double w, double h)
    {
        var dx = external.X - rectCenter.X;
        var dy = external.Y - rectCenter.Y;

        var hw = w / 2;
        var hh = h / 2;

        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
            return rectCenter;

        var scaleX = Math.Abs(dx) > 0.001 ? hw / Math.Abs(dx) : double.MaxValue;
        var scaleY = Math.Abs(dy) > 0.001 ? hh / Math.Abs(dy) : double.MaxValue;
        var scale = Math.Min(scaleX, scaleY);

        return new Point(rectCenter.X + dx * scale, rectCenter.Y + dy * scale);
    }

    /// <summary>在线段终点创建三角形箭头。</summary>
    private static Polygon CreateArrow(Point from, Point to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001) len = 1;

        var ux = dx / len;
        var uy = dy / len;
        // 垂直方向
        var px = -uy;
        var py = ux;

        const double arrowLen = 9;
        const double arrowHalfWidth = 5;

        var tip = new Point(to.X, to.Y);
        var base1 = new Point(
            to.X - ux * arrowLen + px * arrowHalfWidth,
            to.Y - uy * arrowLen + py * arrowHalfWidth);
        var base2 = new Point(
            to.X - ux * arrowLen - px * arrowHalfWidth,
            to.Y - uy * arrowLen - py * arrowHalfWidth);

        return new Polygon
        {
            Points = new[] { tip, base1, base2 },
            Fill = HmiEditor.App.Theme.Link
        };
    }
}
