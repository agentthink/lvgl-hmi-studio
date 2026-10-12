using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using HmiEditor.App.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using HmiEditor.App.Services;

using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **每个工具面板都必须有可拖把手**（阶段 225 起的不变量；阶段 224 量到的"单工具 dock 只有 1 px
/// 标签条且不创建标签项"已被**处置②**修掉：把「资产管理」挪进项目浏览器那条 dock）。
///
/// <para><b>为什么值得一条判据</b>：阶段 224 量到归档问题单原文那条"逐字方向"（拖**项目浏览器**到工具箱）
/// 打不到的**确切原因** —— 那条 dock 里**只有一个工具** ⇒ `ToolTabStrip` 只有 **1 px 高**、
/// 且**根本不创建 `ToolTabStripItem`** ⇒ 没有可按下、可拖的把手（不是"拖不动"，是"没有东西可拖"）。
/// 用户选了处置②：**给那条 dock 加第二个工具**（把「资产管理」从右栏挪进左侧上区）⇒
/// 四条工具 dock 都有 ≥2 个工具、每个面板都有把手、原文那条方向也**可判**了。</para>
///
/// <para>这一面把新不变量钉住：**没有单工具工具 dock** ＋ **每个工具都在把手名单里** ＋
/// 那条 1 px 的标签条**不再存在**（标签条高度 ≥20 px）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class DockSingleToolTabStripTests
{
    private static (HmiDockFactory Factory, IRootDock Layout) BuildLayout()
    {
        var factory = new HmiDockFactory(
            new ToolboxView(), new FaceplateLibraryView(), new DesignCanvasView(),
            new ProjectExplorerView(), new PropertyPanelView(), new LayerPanelView(),
            new AssetManagerView(), new NavigationFlowView(), new AuditLogView(),
            new CrossReferenceView(), new AiChatPanelView(), new ResourceLibraryView(),
            new UndoRedoManager());

        var layout = factory.CreateLayout();
        factory.InitLayout(layout);
        return (factory, layout);
    }

    private static void Settle(Window window, DockControl dock)
    {
        for (var i = 0; i < 3; i++)
        {
            UiTestHost.UIThread.RunJobs(Avalonia.Threading.DispatcherPriority.Loaded);
            window.Measure(new Size(1280, 800));
            window.Arrange(new Rect(0, 0, 1280, 800));
            UiTestHost.Pump(4);
        }
        // ⚠ 最后这一下（Background）不能省：DeferredContentPresenter 的惰性加载排在 Background，
        //   少了它标签条根本不会创建（本判据第一版就是这么"什么都没扫到"的）。
        UiTestHost.UIThread.RunJobs(Avalonia.Threading.DispatcherPriority.Background);
    }

    private static IEnumerable<IDockable> Walk(IDockable root)
    {
        yield return root;
        if (root is IDock dock)
        {
            foreach (var child in dock.VisibleDockables ?? new List<IDockable>())
                foreach (var d in Walk(child))
                    yield return d;
        }
    }

    [Fact]
    public void EveryToolDock_HasAtLeastTwoTools_AndEveryToolHasAHandle()
    {
        var report = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();
            Settle(window, dock);

            try
            {
                // ⚠ 只算**工具**（`Tool`）：`CenterDocDock` 里装的是画布 `Document`，
                //   文档的标签条是另一族（DocumentTabStrip），不在本不变量范围内（如实写清，不凑数）。
                var toolDocks = Walk(layout)
                    .OfType<Dock.Model.Mvvm.Controls.ToolDock>()
                    .Select(d => (d.Id, Tools: (d.VisibleDockables ?? new List<IDockable>())
                        .OfType<Dock.Model.Mvvm.Controls.Tool>().Select(x => x.Id).ToArray()))
                    .Where(d => d.Tools.Length > 0)
                    .ToArray();

                // 唯一允许的"单工具 dock"：中央的**画布视口**（`Canvas` 是 `Tool` 但它是视口不是面板，
                //   `CanClose=false`；它没有兄弟工具是设计如此）。**只此一个**，其它任何单工具 dock 都要红。
                var allowedSingleToolDock = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["CenterDocDock"] = "画布视口（`Canvas`，CanClose=false）—— 不是面板，没有兄弟工具是设计如此",
                };

                var singleTool = toolDocks.Where(d => d.Tools.Length == 1)
                    .Where(d => !(allowedSingleToolDock.TryGetValue(d.Id ?? "", out _) && d.Tools[0] == "Canvas"))
                    .Select(d => $"{d.Id}:[{string.Join(",", d.Tools)}]")
                    .ToArray();

                // 把手名单只覆盖**面板**：画布视口（`Canvas`）走的是另一族的标签（与上面同一条例外）
                var allTools = toolDocks.SelectMany(d => d.Tools)
                    .Where(id => id != "Canvas")
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                var tabItems = dock.GetVisualDescendants().OfType<ToolTabStripItem>()
                    .Select(i => (i.DataContext as IDockable)?.Id ?? "?")
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();

                var stripHeights = dock.GetVisualDescendants().OfType<ToolTabStrip>()
                    .Select(s => s.Bounds.Height)
                    .OrderBy(h => h)
                    .ToArray();

                var explorerView = Walk(layout).Select(d => d.Context).OfType<ProjectExplorerView>().FirstOrDefault();

                return (singleTool, allTools, tabItems, stripHeights,
                        Attached: explorerView is not null && TopLevel.GetTopLevel(explorerView) is not null,
                        Descendants: explorerView?.GetVisualDescendants().OfType<Control>().Count() ?? 0);
            }
            finally
            {
                window.Close();
                UiTestHost.Pump();
            }
        });

        // ① **面板**所在的工具 dock 没有一个是单工具的 —— 这正是当初那条 1 px 标签条（无把手）的成因。
        //    唯一例外是中央画布视口（上面登记，且断言它确实是 Canvas）。
        Assert.True(report.singleTool.Length == 0,
            "仍有单工具 dock（那里的面板没有可拖把手）：" + string.Join(", ", report.singleTool));

        // ② 每个工具都在把手名单里（含 ProjectExplorer —— 阶段 224 时它**不在**）
        Assert.Equal(report.allTools, report.tabItems);
        Assert.Contains("ProjectExplorer", report.tabItems);

        // ③ 四条**工具** dock 的标签条都是正常高度（≥20 px）—— 那条 1 px 的已经不存在
        Assert.True(report.stripHeights.Count(h => h >= 20) >= 4,
            $"正常高度的标签条不足 4 条：[{string.Join(", ", report.stripHeights)}]");

        // ④ 面板本身照旧：附着且有内容
        Assert.True(report.Attached, "ProjectExplorerView 没有附着在可视树上");
        Assert.True(report.Descendants > 5, $"ProjectExplorerView 只有 {report.Descendants} 个可视子控件 —— 面板是空的？");
    }

}
