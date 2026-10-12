using HmiEditor.Core.Models;
using HmiEditor.Core.Serialization;
using Xunit;

namespace HmiEditor.Core.Tests;

/// <summary>
/// 导航流图（NavigationGraph）单元测试 —— 覆盖构建、基本操作、查找、
/// 自动布局、JSON 往返序列化与边提取逻辑。
/// </summary>
public class NavigationGraphTests
{
    // ═══════════════════════════════════════════════
    //  辅助构造
    // ═══════════════════════════════════════════════

    /// <summary>构造一个含 Main / Settings / Alarms 三个屏幕的项目，带 SwitchScreen 跳转。</summary>
    private static HmiProject CreateSampleProject()
    {
        var project = new HmiProject { Name = "NavTest" };

        var main = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        var settings = new HmiScreen { Id = "s2", Name = "Settings", Width = 800, Height = 480 };
        var alarms = new HmiScreen { Id = "s3", Name = "Alarms", Width = 800, Height = 480 };

        // Main → Settings
        main.Controls.Add(new HmiControl
        {
            Id = "c1",
            Name = "BtnSettings",
            Type = HmiControlType.Button,
            Action = HmiAction.SwitchScreen("Settings")
        });
        // Main → Alarms
        main.Controls.Add(new HmiControl
        {
            Id = "c2",
            Name = "BtnAlarms",
            Type = HmiControlType.Button,
            Action = HmiAction.SwitchScreen("Alarms")
        });
        // Settings → Main（返回）
        settings.Controls.Add(new HmiControl
        {
            Id = "c3",
            Name = "BtnBack",
            Type = HmiControlType.Button,
            Action = HmiAction.SwitchScreen("Main")
        });

        project.Screens.Add(main);
        project.Screens.Add(settings);
        project.Screens.Add(alarms);

        return project;
    }

    // ═══════════════════════════════════════════════
    //  BuildFromProject
    // ═══════════════════════════════════════════════

    [Fact]
    public void BuildFromProject_CreatesCorrectNodesAndEdges()
    {
        var project = CreateSampleProject();

        var graph = NavigationGraph.BuildFromProject(project);

        // 节点数 = 屏幕数
        Assert.Equal(3, graph.Nodes.Count);

        // 边数 = SwitchScreen 动作数（Main→Settings, Main→Alarms, Settings→Main = 3）
        Assert.Equal(3, graph.Edges.Count);
    }

    [Fact]
    public void BuildFromProject_NodeScreenIdMatchesScreen()
    {
        var project = CreateSampleProject();

        var graph = NavigationGraph.BuildFromProject(project);

        var mainNode = graph.FindNodeByScreenId("s1");
        Assert.NotNull(mainNode);
        Assert.Equal("Main", mainNode!.ScreenName);
    }

    [Fact]
    public void BuildFromProject_EdgeTargetsByScreenName()
    {
        var project = CreateSampleProject();

        var graph = NavigationGraph.BuildFromProject(project);

        // Main 节点应有两条出边
        var mainNode = graph.FindNodeByScreenId("s1");
        Assert.NotNull(mainNode);

        var outEdges = graph.Edges
            .Where(e => e.SourceNodeId == mainNode!.Id)
            .ToList();
        Assert.Equal(2, outEdges.Count);

        // 验证目标节点分别是 Settings 和 Alarms
        var targetNodeIds = outEdges.Select(e => e.TargetNodeId).ToList();
        var settingsNode = graph.FindNodeByScreenId("s2");
        var alarmsNode = graph.FindNodeByScreenId("s3");
        Assert.Contains(settingsNode!.Id, targetNodeIds);
        Assert.Contains(alarmsNode!.Id, targetNodeIds);
    }

    [Fact]
    public void BuildFromProject_NonSwitchScreenActionsIgnored()
    {
        var project = new HmiProject { Name = "Test" };
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        var target = new HmiScreen { Id = "s2", Name = "Target", Width = 800, Height = 480 };

        // 非 SwitchScreen 动作不应产生边
        screen.Controls.Add(new HmiControl
        {
            Id = "c1",
            Name = "BtnSet",
            Type = HmiControlType.Button,
            Action = HmiAction.SetVariable("var1", "1")
        });
        // SwitchScreen 动作应产生边
        screen.Controls.Add(new HmiControl
        {
            Id = "c2",
            Name = "BtnGo",
            Type = HmiControlType.Button,
            Action = HmiAction.SwitchScreen("Target")
        });

        project.Screens.Add(screen);
        project.Screens.Add(target);

        var graph = NavigationGraph.BuildFromProject(project);

        Assert.Equal(2, graph.Nodes.Count);
        Assert.Single(graph.Edges);
    }

    [Fact]
    public void BuildFromProject_TargetScreenNotFound_NoEdgeCreated()
    {
        var project = new HmiProject { Name = "Test" };
        var screen = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };

        // 目标屏幕不存在
        screen.Controls.Add(new HmiControl
        {
            Id = "c1",
            Name = "BtnGhost",
            Type = HmiControlType.Button,
            Action = HmiAction.SwitchScreen("NonExistent")
        });

        project.Screens.Add(screen);

        var graph = NavigationGraph.BuildFromProject(project);

        Assert.Single(graph.Nodes);
        Assert.Empty(graph.Edges);
    }

    // ═══════════════════════════════════════════════
    //  边提取：同一目标的多条边
    // ═══════════════════════════════════════════════

    [Fact]
    public void BuildFromProject_TwoControlsSameTarget_CreatesTwoEdges()
    {
        var project = new HmiProject { Name = "Test" };
        var main = new HmiScreen { Id = "s1", Name = "Main", Width = 800, Height = 480 };
        var target = new HmiScreen { Id = "s2", Name = "Target", Width = 800, Height = 480 };

        // 两个按钮都切换到同一目标屏幕
        main.Controls.Add(new HmiControl
        {
            Id = "c1",
            Name = "BtnA",
            Type = HmiControlType.Button,
            Action = HmiAction.SwitchScreen("Target")
        });
        main.Controls.Add(new HmiControl
        {
            Id = "c2",
            Name = "BtnB",
            Type = HmiControlType.Button,
            Action = HmiAction.SwitchScreen("Target")
        });

        project.Screens.Add(main);
        project.Screens.Add(target);

        var graph = NavigationGraph.BuildFromProject(project);

        Assert.Equal(2, graph.Nodes.Count);
        // 两个控件各产生一条边 → 2 条边
        Assert.Equal(2, graph.Edges.Count);

        // 两条边的源控件 Id 不同
        var controlIds = graph.Edges.Select(e => e.SourceControlId).ToList();
        Assert.Contains("c1", controlIds);
        Assert.Contains("c2", controlIds);
    }

    // ═══════════════════════════════════════════════
    //  基本操作：AddNode / AddEdge / RemoveNode / RemoveEdge
    // ═══════════════════════════════════════════════

    [Fact]
    public void AddNode_AddsToCollection()
    {
        var graph = new NavigationGraph();

        var node = graph.AddNode("s1", "Main", 10, 20);

        Assert.Single(graph.Nodes);
        Assert.Equal("s1", node.ScreenId);
        Assert.Equal("Main", node.ScreenName);
        Assert.Equal(10, node.X);
        Assert.Equal(20, node.Y);
    }

    [Fact]
    public void AddEdge_AddsToCollection()
    {
        var graph = new NavigationGraph();
        var n1 = graph.AddNode("s1", "Main");
        var n2 = graph.AddNode("s2", "Settings");

        var edge = graph.AddEdge(n1.Id, n2.Id, "c1", "BtnSettings", "→ Settings");

        Assert.Single(graph.Edges);
        Assert.Equal(n1.Id, edge.SourceNodeId);
        Assert.Equal(n2.Id, edge.TargetNodeId);
        Assert.Equal("c1", edge.SourceControlId);
        Assert.Equal("→ Settings", edge.Label);
    }

    [Fact]
    public void RemoveNode_RemovesNodeAndRelatedEdges()
    {
        var graph = new NavigationGraph();
        var n1 = graph.AddNode("s1", "Main");
        var n2 = graph.AddNode("s2", "Settings");
        graph.AddEdge(n1.Id, n2.Id, "c1", "BtnSettings", "→ Settings");
        graph.AddEdge(n2.Id, n1.Id, "c2", "BtnBack", "→ Main");

        Assert.Equal(2, graph.Nodes.Count);
        Assert.Equal(2, graph.Edges.Count);

        var removed = graph.RemoveNode(n1);

        Assert.True(removed);
        Assert.Single(graph.Nodes);
        // 两条边都关联 n1，应被级联移除
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void RemoveEdge_RemovesFromCollection()
    {
        var graph = new NavigationGraph();
        var n1 = graph.AddNode("s1", "Main");
        var n2 = graph.AddNode("s2", "Settings");
        var edge = graph.AddEdge(n1.Id, n2.Id, "c1", "BtnSettings", "→ Settings");

        var removed = graph.RemoveEdge(edge);

        Assert.True(removed);
        Assert.Empty(graph.Edges);
    }

    // ═══════════════════════════════════════════════
    //  FindNodeByScreenId
    // ═══════════════════════════════════════════════

    [Fact]
    public void FindNodeByScreenId_ReturnsMatchingNode()
    {
        var graph = new NavigationGraph();
        graph.AddNode("s1", "Main");
        graph.AddNode("s2", "Settings");

        var found = graph.FindNodeByScreenId("s2");

        Assert.NotNull(found);
        Assert.Equal("Settings", found!.ScreenName);
    }

    [Fact]
    public void FindNodeByScreenId_NotFound_ReturnsNull()
    {
        var graph = new NavigationGraph();
        graph.AddNode("s1", "Main");

        var found = graph.FindNodeByScreenId("s999");

        Assert.Null(found);
    }

    // ═══════════════════════════════════════════════
    //  AutoLayout
    // ═══════════════════════════════════════════════

    [Fact]
    public void AutoLayout_ReturnsCorrectCount()
    {
        Assert.Empty(NavigationGraph.AutoLayout(0));
        Assert.Single(NavigationGraph.AutoLayout(1));
        Assert.Equal(5, NavigationGraph.AutoLayout(5).Count);
        Assert.Equal(10, NavigationGraph.AutoLayout(10).Count);
    }

    [Fact]
    public void AutoLayout_CoordinatesAreProgressive()
    {
        var positions = NavigationGraph.AutoLayout(4);

        Assert.Equal(4, positions.Count);

        // 第一个节点在起始偏移位置
        Assert.True(positions[0].X >= 0);
        Assert.True(positions[0].Y >= 0);

        // 不同节点坐标不完全相同
        var distinct = positions.Distinct().Count();
        Assert.Equal(4, distinct);
    }

    [Fact]
    public void AutoLayout_ZeroCount_ReturnsEmpty()
    {
        var positions = NavigationGraph.AutoLayout(0);

        Assert.Empty(positions);
    }

    // ═══════════════════════════════════════════════
    //  JSON 往返序列化
    // ═══════════════════════════════════════════════

    [Fact]
    public void Serialize_RoundTrip_PreservesNodesAndEdges()
    {
        var project = CreateSampleProject();
        var graph = NavigationGraph.BuildFromProject(project);

        var json = NavigationGraphSerializer.Serialize(graph);
        Assert.False(string.IsNullOrEmpty(json));

        var restored = NavigationGraphSerializer.Deserialize(json);
        Assert.NotNull(restored);

        Assert.Equal(graph.Nodes.Count, restored!.Nodes.Count);
        Assert.Equal(graph.Edges.Count, restored.Edges.Count);
    }

    [Fact]
    public void Serialize_RoundTrip_PreservesNodeFields()
    {
        var graph = new NavigationGraph();
        graph.AddNode("s1", "Main", 100, 200);

        var json = NavigationGraphSerializer.Serialize(graph);
        var restored = NavigationGraphSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Single(restored!.Nodes);

        var node = restored.Nodes[0];
        Assert.Equal("s1", node.ScreenId);
        Assert.Equal("Main", node.ScreenName);
        Assert.Equal(100, node.X);
        Assert.Equal(200, node.Y);
    }

    [Fact]
    public void Serialize_RoundTrip_PreservesEdgeFields()
    {
        var graph = new NavigationGraph();
        var n1 = graph.AddNode("s1", "Main");
        var n2 = graph.AddNode("s2", "Settings");
        graph.AddEdge(n1.Id, n2.Id, "c1", "BtnSettings", "→ Settings");

        var json = NavigationGraphSerializer.Serialize(graph);
        var restored = NavigationGraphSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Single(restored!.Edges);

        var edge = restored.Edges[0];
        Assert.Equal(n1.Id, edge.SourceNodeId);
        Assert.Equal(n2.Id, edge.TargetNodeId);
        Assert.Equal("c1", edge.SourceControlId);
        Assert.Equal("BtnSettings", edge.SourceControlName);
        Assert.Equal("→ Settings", edge.Label);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsNull()
    {
        var result = NavigationGraphSerializer.Deserialize("{invalid json}");
        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_NullJson_ReturnsNull()
    {
        var result = NavigationGraphSerializer.Deserialize(null!);
        Assert.Null(result);
    }

    // ═══════════════════════════════════════════════
    //  文件读写
    // ═══════════════════════════════════════════════

    [Fact]
    public void SaveToFile_LoadFromFile_RoundTrip()
    {
        var project = CreateSampleProject();
        var graph = NavigationGraph.BuildFromProject(project);

        var tempPath = TestTempDir.NewFilePath("nav_test", ".nav.json");
        try
        {
            NavigationGraphSerializer.SaveToFile(graph, tempPath);
            Assert.True(File.Exists(tempPath));

            var loaded = NavigationGraphSerializer.LoadFromFile(tempPath);
            Assert.NotNull(loaded);
            Assert.Equal(graph.Nodes.Count, loaded!.Nodes.Count);
            Assert.Equal(graph.Edges.Count, loaded.Edges.Count);
        }
        finally
        {
            TestTempDir.DeleteFile(tempPath);
        }
    }

    [Fact]
    public void LoadFromFile_NonExistentPath_ReturnsNull()
    {
        var result = NavigationGraphSerializer.LoadFromFile(
            TestTempDir.NewFilePath("nonexistent", ".nav.json"));
        Assert.Null(result);
    }
}
