using System.Collections.ObjectModel;

namespace HmiEditor.Core.Models;

/// <summary>
/// 导航流图中的节点 —— 对应一个屏幕。
/// X/Y 为节点在图中的布局坐标。
/// </summary>
public class NavigationNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>关联屏幕 Id。</summary>
    public string ScreenId { get; set; } = "";

    /// <summary>关联屏幕名称（冗余存储，便于脱机展示）。</summary>
    public string ScreenName { get; set; } = "";

    /// <summary>节点在图中的 X 坐标。</summary>
    public double X { get; set; }

    /// <summary>节点在图中的 Y 坐标。</summary>
    public double Y { get; set; }
}

/// <summary>
/// 导航流图中的边 —— 对应一个控件的 SwitchScreen 动作。
/// </summary>
public class NavigationEdge
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>源节点 Id。</summary>
    public string SourceNodeId { get; set; } = "";

    /// <summary>目标节点 Id。</summary>
    public string TargetNodeId { get; set; } = "";

    /// <summary>触发跳转的控件 Id。</summary>
    public string SourceControlId { get; set; } = "";

    /// <summary>触发跳转的控件名称。</summary>
    public string SourceControlName { get; set; } = "";

    /// <summary>边标签（如「→ Settings」）。</summary>
    public string Label { get; set; } = "";
}

/// <summary>
/// 导航流图 —— 从项目屏幕与 SwitchScreen 动作自动提取的导航拓扑。
/// 独立存储为 .nav.json，不侵入 .hmi.json。
/// </summary>
public class NavigationGraph
{
    public ObservableCollection<NavigationNode> Nodes { get; set; } = new();
    public ObservableCollection<NavigationEdge> Edges { get; set; } = new();

    // ── 节点操作 ──

    public NavigationNode AddNode(NavigationNode node)
    {
        Nodes.Add(node);
        return node;
    }

    public NavigationNode AddNode(string screenId, string screenName, double x = 0, double y = 0)
    {
        var node = new NavigationNode
        {
            ScreenId = screenId,
            ScreenName = screenName,
            X = x,
            Y = y
        };
        Nodes.Add(node);
        return node;
    }

    /// <summary>移除节点并级联移除所有关联的边。</summary>
    public bool RemoveNode(NavigationNode node)
    {
        var related = Edges
            .Where(e => e.SourceNodeId == node.Id || e.TargetNodeId == node.Id)
            .ToList();

        foreach (var edge in related)
            Edges.Remove(edge);

        return Nodes.Remove(node);
    }

    /// <summary>按屏幕 Id 查找节点。</summary>
    public NavigationNode? FindNodeByScreenId(string screenId)
    {
        return Nodes.FirstOrDefault(n => n.ScreenId == screenId);
    }

    // ── 边操作 ──

    public NavigationEdge AddEdge(NavigationEdge edge)
    {
        Edges.Add(edge);
        return edge;
    }

    public NavigationEdge AddEdge(
        string sourceNodeId,
        string targetNodeId,
        string sourceControlId,
        string sourceControlName,
        string label)
    {
        var edge = new NavigationEdge
        {
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            SourceControlId = sourceControlId,
            SourceControlName = sourceControlName,
            Label = label
        };
        Edges.Add(edge);
        return edge;
    }

    public bool RemoveEdge(NavigationEdge edge) => Edges.Remove(edge);

    // ── 构建 / 布局 ──

    /// <summary>
    /// 从项目构建导航图：每个屏幕一个节点，每个 SwitchScreen 动作一条边。
    /// 边的目标通过屏幕名（HmiAction.Target）匹配。节点按网格自动布局。
    /// </summary>
    public static NavigationGraph BuildFromProject(HmiProject project)
    {
        var graph = new NavigationGraph();

        // 先按网格布局生成坐标
        var positions = AutoLayout(project.Screens.Count);

        // 建节点
        for (var i = 0; i < project.Screens.Count; i++)
        {
            var screen = project.Screens[i];
            var pos = positions[i];
            graph.AddNode(screen.Id, screen.Name, pos.X, pos.Y);
        }

        // 建边：遍历每个屏幕的控件，提取 SwitchScreen 动作
        foreach (var screen in project.Screens)
        {
            var sourceNode = graph.FindNodeByScreenId(screen.Id);
            if (sourceNode == null) continue;

            foreach (var control in screen.Controls)
            {
                // 联动模型：遍历 Actions 集合
                foreach (var action in control.Actions)
                {
                    if (action.ActionType != "SwitchScreen") continue;
                    if (string.IsNullOrEmpty(action.Target)) continue;

                    var targetScreen = project.Screens.FirstOrDefault(s => s.Name == action.Target);
                    if (targetScreen == null) continue;

                    var targetNode = graph.FindNodeByScreenId(targetScreen.Id);
                    if (targetNode == null) continue;

                    graph.AddEdge(
                        sourceNode.Id,
                        targetNode.Id,
                        control.Id,
                        control.Name,
                        $"→ {action.Target}");
                }

                // 回退旧字段
                if (control.Actions.Count == 0
                    && control.Action != null
                    && control.Action.ActionType == "SwitchScreen"
                    && !string.IsNullOrEmpty(control.Action.Target))
                {
                    var targetScreen = project.Screens.FirstOrDefault(s => s.Name == control.Action.Target);
                    if (targetScreen == null) continue;

                    var targetNode = graph.FindNodeByScreenId(targetScreen.Id);
                    if (targetNode == null) continue;

                    graph.AddEdge(
                        sourceNode.Id,
                        targetNode.Id,
                        control.Id,
                        control.Name,
                        $"→ {control.Action.Target}");
                }
            }
        }

        return graph;
    }

    /// <summary>
    /// 简单网格布局：colCount = ceil(sqrt(count))，返回 count 个坐标。
    /// </summary>
    public static List<(double X, double Y)> AutoLayout(int count)
    {
        var result = new List<(double X, double Y)>(count);
        if (count <= 0) return result;

        var colCount = (int)Math.Ceiling(Math.Sqrt(count));
        if (colCount < 1) colCount = 1;

        const double spacingX = 220;
        const double spacingY = 140;
        const double offsetX = 40;
        const double offsetY = 40;

        for (var i = 0; i < count; i++)
        {
            var col = i % colCount;
            var row = i / colCount;
            result.Add((offsetX + col * spacingX, offsetY + row * spacingY));
        }

        return result;
    }
}
