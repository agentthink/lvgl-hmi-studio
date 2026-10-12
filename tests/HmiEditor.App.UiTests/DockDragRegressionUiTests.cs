using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using HmiEditor.App.Controls;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **真实拖拽**回归：把一个工具面板的标签拖进另一个工具 dock 后，面板内容必须还在（不能空白）。
///
/// <para><b>这一条为什么此前一直缺</b>：归档问题单 <c>dock-blank-after-drag</c>（2026-08-03，**状态仍 OPEN**）
/// 的症状是「拖动项目浏览器到工具箱 dock 后，工具箱内容变空白」，而
/// `DockViewResolutionUiTests` 只钉住了**解析契约**（Match/Build 一致、Context 丢失仍能兜底），
/// 明确写着"真实拖拽路径仍未模拟 …… 在 headless 里模拟 DragDrop 序列是另一件事，**且不一定可行**"。</para>
///
/// <para><b>本轮取证结论：可行。</b>Dock.Avalonia 12.1 的拖拽**不走平台的 <c>DragDrop</c> API**，
/// 而是**指针事件**驱动 —— 其程序集里是 <c>OnPointerPressed</c> / <c>OnPointerMoved</c> /
/// <c>OnPointerReleased</c> + <c>DockDragContext</c>（<c>ToolTabStripItem</c> 是被按下的把手）。
/// 于是 headless 的 <c>MouseDown/MouseMove/MouseUp</c> 就能驱动它 —— 实测：拖工具箱标签到另一 dock 后，
/// <c>Toolbox</c> 的父 dock 真的从 <c>LeftLowerDock</c> 变成目标 dock，且视图仍有 100+ 个可视后代。
/// 那条"不一定可行"的假设因此**不成立**（这也解释了为什么值得先探路再决定做不做）。</para>
///
/// <para><b>判据为什么看"是否挂在可视树上"而不是"解析出非 null"</b>：归档问题单的现场证据是
/// <c>[ToolboxView] DETACHED from visual tree. WasParent=DeferredContentControl</c> 且**此后再无 attached**
/// —— 即"解析得出来、但没挂上去"。所以必须看可视树附着状态与后代数量，
/// 光断言 <c>Build(...) is not null</c> 打不到这个缺陷。</para>
/// </summary>
public class DockDragRegressionUiTests
{
    private const int ExpectedToolDockables = 12;

    /// <summary>面板 id → 归档问题单里点名的那个。</summary>
    private const string ToolboxId = "Toolbox";

    // ────────────────────────── 夹具 ──────────────────────────

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

    /// <summary>
    /// 挂进窗口并**把布局真正跑完**：<c>DeferredContentPresenter</c> 是惰性的，
    /// headless 下没有真实渲染循环，必须显式排空 Load 队列 + 手动 Measure/Arrange。
    /// </summary>
    private static void Settle(Window window, DockControl dock)
    {
        for (var i = 0; i < 3; i++)
        {
            UiTestHost.UIThread.RunJobs(DispatcherPriority.Loaded);
            window.Measure(new Size(1280, 800));
            window.Arrange(new Rect(0, 0, 1280, 800));
            UiTestHost.Pump(4);
        }
        UiTestHost.UIThread.RunJobs(DispatcherPriority.Background);
    }

    private static IEnumerable<IDockable> Walk(IDockable root)
    {
        var seen = new HashSet<IDockable>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<IDockable>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (!seen.Add(d)) continue;
            yield return d;

            if (d is IDock dock && dock.VisibleDockables is { } visible)
                foreach (var c in visible) stack.Push(c);

            if (d is IRootDock r)
            {
                if (r.HiddenDockables is { } hidden)
                    foreach (var c in hidden) stack.Push(c);
                if (r.PinnedDock is { } pinned) stack.Push(pinned);

                // ⚠ 四张"钉住"列表也必须走（阶段 163 自纠）：只走 `VisibleDockables` 会让
                // `ToolsOf` / `PanelState` 这类读数**看不见被钉走的面板** —— 我一度据此以为
                // `IsDockablePinned` 漏判，实际是**枚举口径的假象**。
                foreach (var pinnedList in new[] { r.LeftPinnedDockables, r.TopPinnedDockables,
                                                   r.RightPinnedDockables, r.BottomPinnedDockables })
                    if (pinnedList is { } list)
                        foreach (var c in list) stack.Push(c);

                if (r.Windows is { } windows)
                    foreach (var w in windows)
                        if (w.Layout is { } l)
                            stack.Push(l);
            }
        }
    }

    private static List<Tool> ToolsOf(IDockable layout) => Walk(layout).OfType<Tool>().ToList();

    /// <summary>承载该 dockable 的 <see cref="IDock"/>（找不到 = 在隐藏/窗口树里）。</summary>
    private static IDock? ParentDockOf(IDockable layout, IDockable tool) =>
        Walk(layout).OfType<IDock>()
            .FirstOrDefault(d => d.VisibleDockables?.Contains(tool) == true);

    /// <summary>某个工具的面板视图当前是否**挂在可视树上且真有内容**（这就是"没空白"的判据）。</summary>
    private static (bool Attached, int Descendants) PanelState(IDockable tool)
    {
        if (tool.Context is not Control view) return (false, 0);

        // 用 TopLevel.GetTopLevel 而不是 GetVisualParent：前者对"未附着"返回 null，
        // 与归档问题单里那句 DETACHED from visual tree 是同一件事。
        var attached = TopLevel.GetTopLevel(view) is not null;
        var descendants = view.GetVisualDescendants().OfType<Control>().Count();
        return (attached, descendants);
    }

    /// <summary>按标签中心做一次指针拖拽：按下 → 分步移动 → 松开（每步都推事件队列）。</summary>
    private static void DragToolTab(Window window, DockControl dock, string toolId, Point target)
    {
        var tab = dock.GetVisualDescendants().OfType<ToolTabStripItem>()
            .FirstOrDefault(t => (t.DataContext as IDockable)?.Id == toolId);

        Assert.True(tab is not null,
            $"可视树里找不到面板 '{toolId}' 的标签（ToolTabStripItem）—— 拖拽把手变了？");

        var from = tab!.TranslatePoint(new Point(tab.Bounds.Width / 2, tab.Bounds.Height / 2), window);
        Assert.True(from is not null, $"面板 '{toolId}' 的标签不在窗口坐标内");

        window.MouseDown(from!.Value, MouseButton.Left);
        UiTestHost.Pump(2);

        const int steps = 8;
        for (var i = 1; i <= steps; i++)
        {
            var p = new Point(
                from.Value.X + (target.X - from.Value.X) * i / steps,
                from.Value.Y + (target.Y - from.Value.Y) * i / steps);
            window.MouseMove(p, RawInputModifiers.LeftMouseButton);
            UiTestHost.Pump(2);
        }

        window.MouseUp(target, MouseButton.Left);
        UiTestHost.Pump(3);
        Settle(window, dock);
    }

    // ────────────────────────── 用例 ──────────────────────────

    /// <summary>
    /// 把工具箱的标签拖进**装项目浏览器的那个 dock**（归档问题单的镜像：原文是反向把项目浏览器
    /// 拖到工具箱 dock —— 两者都会让两个 Tool 共处一个 dock，走同一条重组路径），
    /// 然后断言工具箱**仍然挂在可视树上且非空**。
    /// </summary>
    [Fact]
    public void DraggingToolboxTabIntoAnotherToolDock_KeepsPanelPopulated()
    {
        var result = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();
            Settle(window, dock);

            var toolbox = ToolsOf(layout).Single(t => t.Id == ToolboxId);
            var before = PanelState(toolbox);
            var parentBefore = ParentDockOf(layout, toolbox);

            // LeftUpperDock 在 (0,0)-(216,437)，取中心作为放置点
            DragToolTab(window, dock, ToolboxId, new Point(108, 200));

            var tools = ToolsOf(layout);
            var afterToolbox = PanelState(toolbox);
            var toolboxResolves = new ViewLocator().Build(toolbox) is not null;

            // **自检：拖拽真的发生了吗？**
            // 判据用"承载它的 dock 换了对象"，而不是"并入某个已有 dock" ——
            // 实测 Dock 对这次放置的处置是**新建/切分出一个 dock**（父 dock 的 Id 都变了），
            // 不是加进目标 dock 的标签组。若拖拽根本没被驱动，父 dock 引用不变 ⇒ 这里会红，
            // 避免"拖完还有内容"变成空转恒绿（本项目对守卫的一贯要求：先证明它真的走到了）。
            var parentAfter = ParentDockOf(layout, toolbox);
            var moved = !ReferenceEquals(parentBefore, parentAfter);
            var siblingsAfter = parentAfter?.VisibleDockables?.Count ?? 0;
            var parentAfterId = parentAfter?.Id ?? "<none>";

            window.Close();
            return (before, afterToolbox, toolboxResolves, toolCount: tools.Count,
                    moved, parentAfterId, siblingsAfter);
        });

        // 前提自检：拖之前它本来是好的（否则"拖完还是好的"可能是两边都坏）
        Assert.True(result.before.Attached && result.before.Descendants > 0,
            $"拖拽前工具箱就没内容（{result.before}），本用例失去意义");

        // 拖拽自检：必须观察到承载 dock 变化，否则这条用例什么都没验到
        Assert.True(result.moved,
            "指针拖拽没有改变工具箱的承载 dock —— 拖拽路径没被驱动，用例空转（把手变了？）");

        Assert.Equal(ExpectedToolDockables, result.toolCount);       // 拖拽不得丢面板
        Assert.True(result.toolboxResolves, "拖拽后工具箱解析不出视图（解析链断了）");
        Assert.True(result.afterToolbox.Attached,
            "拖拽后工具箱视图**不挂在可视树上** —— 正是归档问题单 dock-blank-after-drag 的现场签名" +
            "（DETACHED from visual tree 且此后再无 attached）。" +
            $"现场：落在 dock '{result.parentAfterId}'（{result.siblingsAfter} 个面板）");
        Assert.True(result.afterToolbox.Descendants > 0,
            "拖拽后工具箱视图挂在树上但一个可视后代都没有（渲染为空白）");
    }

    /// <summary>
    /// 拖拽后**所有**面板的解析链仍然可用（12 个 Tool 逐个 <c>Build</c> 非 null）。
    /// <para>与上一条互补：上一条看"被拖的那个还活着"，这条看"整条解析链没被重组破坏"。</para>
    /// </summary>
    [Fact]
    public void AfterDrag_EveryToolStillResolves()
    {
        var broken = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();
            Settle(window, dock);

            DragToolTab(window, dock, ToolboxId, new Point(108, 200));

            var locator = new ViewLocator();
            var bad = ToolsOf(layout).Where(t => locator.Build(t) is null).Select(t => t.Id ?? "?").ToList();
            window.Close();
            return bad;
        });

        Assert.True(broken.Count == 0,
            "拖拽重组后这些面板解析不出视图（会永久空白）: " + string.Join(", ", broken));
    }

    // ────────────────────────── 未覆盖路线（阶段 161） ──────────────────────────

    /// <summary>
    /// **路线②（归档问题单未覆盖）：拖到中心区** —— 把工具箱标签拖进 `CenterDocDock`
    /// （⚠ 本仓的中心区是 **`ToolDock`** 而不是 `DocumentDock`，所以放置点取"中心区里那幅画布"的中心），
    /// 断言它仍挂在可视树上且非空。与上一条同规矩：先自检"拖拽真的被驱动了"（承载 dock 变了）。
    /// </summary>
    [Fact]
    public void DraggingToolboxTabIntoCenterArea_KeepsPanelPopulated()
    {
        var result = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();
            Settle(window, dock);

            var toolbox = ToolsOf(layout).Single(t => t.Id == ToolboxId);
            var before = PanelState(toolbox);
            var parentBefore = ParentDockOf(layout, toolbox);
            var target = CenterOfCenterArea(dock, window);

            DragToolTab(window, dock, ToolboxId, target);

            var after = PanelState(toolbox);
            var parentAfter = ParentDockOf(layout, toolbox);
            var toolCount = ToolsOf(layout).Count;
            window.Close();
            return (before, after, moved: !ReferenceEquals(parentBefore, parentAfter),
                    toolCount, dockId: parentAfter?.Id ?? "<none>", target);
        });

        Assert.True(result.before.Attached && result.before.Descendants > 0,
            $"拖拽前工具箱就没内容（{result.before}），本用例失去意义");
        Assert.True(result.moved,
            $"指针拖到中心区（{result.target}）没有改变工具箱的承载 dock —— 拖拽路径没被驱动，用例空转");
        Assert.Equal(ExpectedToolDockables, result.toolCount);
        Assert.True(result.after.Attached,
            "拖到中心区后工具箱视图**不挂在可视树上** —— 归档问题单 dock-blank-after-drag 的现场签名"
            + $"（落在 dock '{result.dockId}'）");
        Assert.True(result.after.Descendants > 0,
            "拖到中心区后工具箱挂在树上但一个可视后代都没有（渲染为空白）");
    }

    /// <summary>
    /// **路线③：把标签拖到 `DockControl` 之外**（Dock 里"拖出成浮动窗口"就是这条指针路径）。
    ///
    /// <para>用户可见的底线只有一条：**拖完之后面板必须仍然看得见** —— 要么还在某个 dock 里，
    /// 要么进了一个真的浮动窗口。`RootDock.Windows` 里多出一条而视图**不挂在任何可视树上**
    /// 也算"面板消失"（这正是归档问题单 dock-blank-after-drag 的现场签名）。</para>
    ///
    /// <para>⚠ 夹具上把 <c>DockControl</c> 做得**比窗口小**，指针才有"dock 之外"可落。</para>
    /// </summary>
    [Fact]
    public void DraggingToolboxTabOutsideDockControl_NeverLosesPanel()
    {
        var r = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout, Width = 1000, Height = 700 };
            var window = new Window { Width = 1280, Height = 800, Content = new Panel { Children = { dock } } };
            window.Show();
            Settle(window, dock);

            var toolbox = ToolsOf(layout).Single(t => t.Id == ToolboxId);
            var before = PanelState(toolbox);
            var parentBefore = ParentDockOf(layout, toolbox);

            // ① DockControl 之外（右下角）、窗口之内 ⇒ 期望**被拒**：面板留在原地、不建窗口
            DragToolTab(window, dock, ToolboxId, new Point(1230, 760));

            var after = PanelState(toolbox);
            var parentAfter = ParentDockOf(layout, toolbox);
            var windowCount = layout.Windows?.Count ?? 0;
            var windowToolCount = (layout.Windows ?? new List<IDockWindow>())
                .Sum(w => w.Layout is null ? 0 : ToolsOf(w.Layout).Count);
            var toolCount = ToolsOf(layout).Count;

            // ② **前提自检**：同一个夹具、同一套手势，拖到**中心区**必须真的改变承载 dock ——
            //    否则"拖到外面之后面板还在"可能只是因为**手势根本没被驱动**（空转恒绿）。
            DragToolTab(window, dock, ToolboxId, CenterOfCenterArea(dock, window));
            var movedByInsideDrag = !ReferenceEquals(parentAfter, ParentDockOf(layout, toolbox));

            window.Close();
            return (before, after, windowCount, windowToolCount, toolCount,
                    refusedParentChanged: !ReferenceEquals(parentBefore, parentAfter),
                    movedByInsideDrag,
                    dockId: parentAfter?.Id ?? "<none>");
        });

        Assert.True(r.before.Attached && r.before.Descendants > 0,
            $"拖拽前工具箱就没内容（{r.before}），本用例失去意义");
        Assert.Equal(ExpectedToolDockables, r.toolCount);
        // 修复后的契约：拖到停靠区之外**不浮动**（否则面板会进一个没有宿主窗口的 DockWindow）
        Assert.True(r.windowCount == 0,
            $"拖到 DockControl 之外建出了 {r.windowCount} 条 DockWindow（其中 {r.windowToolCount} 个面板）"
            + " —— 本仓没有宿主窗口那层，面板会从界面上消失（归档问题单的现场签名）");
        Assert.False(r.refusedParentChanged,
            $"被拒的那次放置仍然移动了面板（落入 dock '{r.dockId}'）—— 面板应当留在原地");
        Assert.True(r.after.Attached,
            "拖到 DockControl 之外后工具箱视图**不挂在任何可视树上** —— 面板消失了"
            + $"（承载 dock='{r.dockId}'）");
        Assert.True(r.after.Descendants > 0,
            "拖到 DockControl 之外后工具箱挂在树上但一个可视后代都没有（渲染为空白）");
        // 前提自检（**放在最后**：它会把面板真的拖走）
        Assert.True(r.movedByInsideDrag,
            "同一个夹具里把标签拖进中心区居然没改变承载 dock —— 这条手势在本用例里没被驱动，"
            + "上面那几条「没消失」的断言全部不作数（空转恒绿）");
    }

    /// <summary>
    /// **契约：浮动被明确拒绝**（阶段 161 的修复所钉住的形状）。
    ///
    /// <para>本仓的工厂是**视图无关**的 `Dock.Model.Mvvm.Factory` ⇒ 浮出来的 `DockWindow`
    /// **没有宿主窗口**（`IDockWindow.Host` 恒为 `null`），面板会从界面上消失且回不来 —— 所以
    /// `HmiDockFactory.FloatDockable` 被覆写成**空实现**。本用例直接调它（这是"拖到停靠区之外"那条
    /// 指针路径的落点），断言：**没有窗口被建出来**、**面板留在原处**（父 dock 对象不变）、
    /// **仍然挂在可视树上且非空**、12 个面板一个不少。</para>
    ///
    /// <para>⚠ 与指针那条互补：指针那条判"用户这么拖之后面板还在"；这条判"工厂层面根本没浮起来"。
    /// 两条都红才算真回归（只删指针那条的话，别人换回 `base.FloatDockable` 仍会被这条抓住）。</para>
    /// </summary>
    [Fact]
    public void FloatDockable_IsRefused_AndPanelStaysWhereItWas()
    {
        var result = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();
            Settle(window, dock);

            var toolbox = ToolsOf(layout).Single(t => t.Id == ToolboxId);
            var before = PanelState(toolbox);
            var parentBefore = ParentDockOf(layout, toolbox);

            factory.FloatDockable(toolbox);
            Settle(window, dock);

            var after = PanelState(toolbox);
            var parentAfter = ParentDockOf(layout, toolbox);
            var toolCount = ToolsOf(layout).Count;
            var windowCount = layout.Windows?.Count ?? 0;
            window.Close();
            return (before, after, parentChanged: !ReferenceEquals(parentBefore, parentAfter),
                    toolCount, windowCount, dockId: parentAfter?.Id ?? "<none>");
        });

        Assert.True(result.before.Attached && result.before.Descendants > 0,
            $"浮动前工具箱就没内容（{result.before}），本用例失去意义");
        Assert.True(result.windowCount == 0,
            $"FloatDockable 建出了 {result.windowCount} 条 DockWindow —— 本仓没有宿主窗口那层，"
            + "浮出来的面板会从界面上消失（归档问题单 dock-blank-after-drag 的现场）");
        Assert.False(result.parentChanged,
            $"FloatDockable 之后面板换了承载 dock（{result.dockId}）—— 它应当**留在原地**");
        Assert.Equal(ExpectedToolDockables, result.toolCount);
        Assert.True(result.after.Attached, "FloatDockable 之后工具箱视图不挂在可视树上（面板消失了）");
        Assert.True(result.after.Descendants > 0, "FloatDockable 之后工具箱挂在树上但没有可视后代");
    }

    /// <summary>
    /// 中心区（`CenterDocDock`，本仓是 **`ToolDock`**）在窗口坐标里的放置点：
    /// 取"中心区里那幅画布"的中心 —— 它一定渲染在中心区内部。
    /// </summary>
    private static Point CenterOfCenterArea(DockControl dock, Window window)
    {
        var canvas = dock.GetVisualDescendants().OfType<DesignCanvasView>().FirstOrDefault();
        Assert.True(canvas is not null, "可视树里找不到中心区的画布 —— 放置点算不出来");
        var p = canvas!.TranslatePoint(new Point(canvas.Bounds.Width / 2, canvas.Bounds.Height / 2), window);
        Assert.True(p is not null, "中心区画布的中心不在窗口坐标里");
        return p!.Value;
    }

    // ────────────────────────── 面板生命周期：关闭 → 从菜单再打开 ──────────────────────────

    /// <summary>
    /// **关闭 → 从「面板」菜单再打开**：面板必须真的回来（阶段 162）。
    ///
    /// <para>🩸 改动前是**静默失效**：标签栏的 × 走 `CloseDockable`，把工具**从布局里整个摘掉**
    /// —— 既不在 `VisibleDockables`、也没进任何 `HiddenDockables`（本版只有 `IRootDock` 有那个列表），
    /// 只留下 `Owner` 指针 ⇒ `FindDockable` 返回 `null`、菜单那一项**点了没反应**；
    /// 而本仓没有"重置布局"可点 ⇒ 只能重启（布局持久化只存 `Id/Proportion/IsOpen`，不含被摘掉的工具）。</para>
    ///
    /// <para>判的是**产品路径** <see cref="HmiDockFactory.OpenDockable"/> —— 阶段 162 把那段逻辑
    /// 从 `MainWindow.OpenDockTool` 移进工厂，正是为了让这条判据**够得着**它（MainWindow 的私有方法
    /// 在 headless 里打不到），顺带 `MainWindow.axaml.cs` 变小。</para>
    /// </summary>
    [Fact]
    public void CloseThenReopenViaMenu_RestoresThePanel()
    {
        var r = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();
            Settle(window, dock);

            var audit = ToolsOf(layout).Single(t => t.Id == "AuditLog");
            var before = PanelState(audit);
            var canClose = audit.CanClose;

            factory.CloseDockable(audit);
            Settle(window, dock);
            var afterClose = PanelState(audit);
            var visibleAfterClose = ParentDockOf(layout, audit) is not null;
            var foundAfterClose = factory.FindDockable(layout,
                d => string.Equals(d.Id, "AuditLog", StringComparison.Ordinal)) is not null;
            var toolsAfterClose = ToolsOf(layout).Count;

            // 「面板」菜单那一跳 = 产品路径
            factory.OpenDockable(layout, "AuditLog");
            Settle(window, dock);

            var after = PanelState(audit);
            var parentAfter = ParentDockOf(layout, audit);
            var toolsAfter = ToolsOf(layout).Count;
            window.Close();
            return (canClose, before, afterClose, visibleAfterClose, foundAfterClose,
                    toolsAfterClose, after, toolsAfter,
                    visibleAfter: parentAfter is not null,
                    dockId: parentAfter?.Id ?? "<none>");
        });

        Assert.True(r.canClose, "前置：这个工具本该可关闭（否则用户点不到 ×，本用例无意义）");
        Assert.True(r.before.Attached && r.before.Descendants > 0,
            $"关闭前工具箱就没内容（{r.before}），本用例失去意义");
        // **前提自检**：关闭真的把它从布局里摘掉了（否则"再打开成功"什么都没证明）
        Assert.False(r.visibleAfterClose || r.foundAfterClose,
            "关闭之后工具仍在可见布局里 / 仍能被 FindDockable 找到 —— 关闭路径没被驱动，本用例空转");
        Assert.True(r.toolsAfterClose == ExpectedToolDockables - 1,
            $"关闭后工具数应当 {ExpectedToolDockables - 1}，实测 {r.toolsAfterClose}");
        // 修复后的契约：菜单那一跳必须把它**挂回老家**，且视图**真的重新附着**（不是空白壳）
        Assert.True(r.visibleAfter,
            "「面板」菜单那一跳之后工具仍不在任何 dock 里 —— 没挂回来（用户点了没反应）");
        Assert.True(r.after.Attached,
            $"挂回来了但视图**不挂在可视树上**（空白壳），落在 dock '{r.dockId}'");
        Assert.True(r.after.Descendants > 0, "挂回来了但一个可视后代都没有");
        Assert.True(r.toolsAfter == ExpectedToolDockables,
            $"挂回来之后工具数应当 {ExpectedToolDockables}，实测 {r.toolsAfter}");
    }

    /// <summary>
    /// **接线契约**：`MainWindow.OpenDockTool`（「面板」菜单那一跳）必须走
    /// <see cref="HmiDockFactory.OpenDockable"/> —— 否则工厂里那半修好了、菜单那条路又回到
    /// "被关闭的面板点了没反应"（`MainWindow` 的私有方法在 headless 里够不着，故这一半用**源码契约**钉）。
    /// </summary>
    [Fact]
    public void MainWindowMenu_GoesThroughTheFactoryOpenDockable()
    {
        var code = SourceScanner.StripComments(System.IO.File.ReadAllText(System.IO.Path.Combine(
            SourceScanner.RepoRoot, "src", "HmiEditor.App", "MainWindow.axaml.cs")));

        Assert.Contains("_dockFactory.OpenDockable(", code);
        Assert.DoesNotContain("_dockFactory.FindDockable(", code);   // 内联那套已整段移进工厂
    }

    /// <summary>
    /// **判据（阶段 163）**：面板被"藏"起来（自动隐藏那条入口 ⇒ <c>HiddenDockables</c>）之后，
    /// 「面板」菜单那一跳（<see cref="HmiDockFactory.OpenDockable"/>）应当把它**放回来、且只放一条**。
    ///
    /// <para><b>为什么单独一条</b>：阶段 162 的修法（`AddDockable` + `IsOpen = true`）只在"被 × 摘掉"
    /// 那条路上验过；碰上"还在 `HiddenDockables` 里"的工具时，`IsOpen = true` 这一步 Dock 自己就会
    /// 把它放回 owner，于是同一个 dock 里出现**两条同名项**（阶段 163 实测读数 `BottomDock×2`）。
    /// 「藏」与「闭」是**两个入口**，必须各有一条判据。</para>
    /// </summary>
    [Fact]
    public void HiddenTool_ReopenedViaMenu_AppearsExactlyOnce()
    {
        var r = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();
            Settle(window, dock);

            var audit = ToolsOf(layout).Single(t => t.Id == "AuditLog");
            var before = PanelState(audit);

            factory.HideDockable(audit);
            Settle(window, dock);

            var hiddenAfterHide = (layout.HiddenDockables ?? new List<IDockable>()).Contains(audit);
            var whereBefore = DocksContaining(layout, "AuditLog");

            factory.OpenDockable(layout, "AuditLog");   // 产品路径
            Settle(window, dock);

            var hiddenAfter = (layout.HiddenDockables ?? new List<IDockable>()).Contains(audit);
            var whereAfter = DocksContaining(layout, "AuditLog");
            var after = PanelState(audit);
            var occurrences = VisibleOccurrences(layout, "AuditLog");
            var tools = ToolsOf(layout).Count;

            window.Close();
            return (before, hiddenAfterHide, whereBefore, hiddenAfter, whereAfter, after, occurrences, tools);
        });

        // **前提自检**：藏真的生效了（否则"只出现一条"什么都没证明）
        Assert.True(r.hiddenAfterHide, "HideDockable 之后工具没进 HiddenDockables —— 隐藏路径没被驱动，本用例空转");
        Assert.True(r.whereBefore == "(无)",
            $"藏起来之后工具仍留在某个 dock 的可见列表里（{r.whereBefore}）—— 本用例失去意义");
        Assert.True(r.before.Attached, "藏之前工具箱就没内容，本用例失去意义");

        Assert.True(r.occurrences == 1,
            $"「面板」菜单那一跳之后工具在可见布局里出现 {r.occurrences} 次（{r.whereAfter}）—— "
            + (r.occurrences == 0 ? "没放回来" : "重复插入"));
        Assert.False(r.hiddenAfter, "菜单那一跳之后工具还留在 HiddenDockables 里 —— 没真的放回来");
        Assert.True(r.after.Attached, "放回来了但视图**不挂在可视树上**（空白壳）");
        Assert.True(r.tools == ExpectedToolDockables, $"工具数应当 {ExpectedToolDockables}，实测 {r.tools}");
    }

    /// <summary>
    /// **判据（阶段 163）**：用户**真的点** dock chrome 上那个钉按钮（`PART_PinButton`，DataContext = 那张
    /// <see cref="ToolDock"/>）把面板钉走之后，从「面板」菜单那一跳（<see cref="HmiDockFactory.OpenDockable"/>）
    /// 应当把它**放回原 dock、且把钉条条目清掉**。
    ///
    /// <para><b>为什么这条必须走手势、而不是调 <c>factory.PinDockable</c></b>：两者落到的状态**不一样**——
    /// 按工具钉时 <c>IsDockablePinned == true</c>；点 dock chrome 那个按钮之后它返回 **<c>false</c>**，
    /// 而工具确实躺在 <c>BottomPinnedDockables</c> 里（实测）。只看 API 会把它当"被 × 摘掉"，
    /// 于是又走 `AddDockable`：面板是回来了，但钉条里那条**留着不清**（脏状态）。</para>
    /// </summary>
    [Fact]
    public void PinnedViaDockChrome_ReopenedViaMenu_ComesBackAndClearsThePin()
    {
        var r = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();
            Settle(window, dock);

            var audit = ToolsOf(layout).Single(t => t.Id == "AuditLog");
            var auditDock = ParentDockOf(layout, audit);
            if (auditDock is not null) auditDock.ActiveDockable = audit;
            Settle(window, dock);

            // 找到**这张 dock 的**钉按钮（不是别的 dock 的），居中心点一下
            var pin = window.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Name == "PART_PinButton" && b.IsVisible
                                     && ReferenceEquals(b.DataContext, auditDock));
            var canClick = pin is { Bounds.Width: > 0, Bounds.Height: > 0 };
            if (pin is { Bounds.Width: > 0, Bounds.Height: > 0 } pinButton)
            {
                var pt = pinButton.TranslatePoint(
                    new Point(pinButton.Bounds.Width / 2, pinButton.Bounds.Height / 2), window);
                window.MouseDown(pt!.Value, MouseButton.Left);
                window.MouseUp(pt.Value, MouseButton.Left);
                Settle(window, dock);
            }

            var pinnedAfterGesture = PinnedListsContaining(layout, audit);
            var whereAfterGesture = DocksContaining(layout, "AuditLog");
            var afterGesture = PanelState(audit);
            var toolsAfterGesture = ToolsOf(layout).Count;

            factory.OpenDockable(layout, "AuditLog");   // 产品路径（面板菜单那一跳）
            Settle(window, dock);

            var pinnedAfter = PinnedListsContaining(layout, audit);
            var whereAfter = DocksContaining(layout, "AuditLog");
            var occurrences = VisibleOccurrences(layout, "AuditLog");
            var after = PanelState(audit);
            var toolsAfter = ToolsOf(layout).Count;

            window.Close();
            return (canClick, pinnedAfterGesture, whereAfterGesture, afterGesture, toolsAfterGesture,
                    pinnedAfter, whereAfter, occurrences, after, toolsAfter);
        });

        // **前提自检**：手势真的把面板钉走了（否则后面全是空转）
        Assert.True(r.canClick, "可视树里找不到这张 dock 的钉按钮（PART_PinButton）—— 钉住手势够不着，本用例空转");
        Assert.True(r.pinnedAfterGesture.Length > 0,
            "点了钉按钮但工具没进任何 PinnedDockables —— 手势没被驱动，本用例空转");
        Assert.True(r.whereAfterGesture == "(无)" && !r.afterGesture.Attached,
            $"钉住之后工具仍停在 {r.whereAfterGesture} / 视图仍附着（{r.afterGesture}）—— 前提不成立");
        Assert.True(r.toolsAfterGesture == ExpectedToolDockables,
            $"钉住之后布局模型里的工具数应当仍是 {ExpectedToolDockables}（钉住只是把它移出可见区，不该丢工具），实测 {r.toolsAfterGesture}");

        // 契约：菜单那一跳把它放回原 dock、**只放一条**、并把钉条清干净
        Assert.True(r.occurrences == 1,
            $"菜单那一跳之后工具在可见布局里出现 {r.occurrences} 次（{r.whereAfter}）—— "
            + (r.occurrences == 0 ? "没放回来" : "重复插入"));
        Assert.True(r.pinnedAfter.Length == 0,
            $"放回来了但钉条里还留着它（{r.pinnedAfter}）—— 钉住状态没被撤销（脏状态）");
        Assert.True(r.after.Attached, "放回来了但视图**不挂在可视树上**（空白壳）");
        Assert.True(r.toolsAfter == ExpectedToolDockables, $"工具数应当 {ExpectedToolDockables}，实测 {r.toolsAfter}");
    }

    /// <summary>工具现在躺在哪几张钉列表里（`L`/`T`/`R`/`B`，空串 = 没钉住）。</summary>
    private static string PinnedListsContaining(IDockable layout, IDockable tool)
    {
        if (layout is not IRootDock root) return string.Empty;
        var names = new List<string>();
        if (root.LeftPinnedDockables?.Contains(tool) == true) names.Add("L");
        if (root.TopPinnedDockables?.Contains(tool) == true) names.Add("T");
        if (root.RightPinnedDockables?.Contains(tool) == true) names.Add("R");
        if (root.BottomPinnedDockables?.Contains(tool) == true) names.Add("B");
        return string.Join(",", names);
    }

    /// <summary>
    /// **判据（阶段 165，产品决策）**：重启后**只打开保存的面板** —— 被 × 关掉的面板**保持关闭**，
    /// 但**没丢**（能从「面板」菜单再打开）。
    ///
    /// <para><b>决策原文</b>：「被 × 关掉的面板，下次启动只打开保存的面板」。实现：
    /// `HmiDockFactory.RestoreLayoutState` 先 `ApplyState`，再按盘面里的 `VisibleToolIds` 裁剪 ——
    /// 裁剪走的正是 × 那条路（`CloseDockable`）⇒ 重启后与"刚被关掉"是**同一个状态**。</para>
    ///
    /// <para>藏起来（`HiddenDockables`）/ 钉住的面板也不在可见树里 ⇒ 不进 `VisibleToolIds` ⇒ 同样不打开
    /// （同一条规则；也能从那菜单找回）。</para>
    /// </summary>
    [Fact]
    public void Restart_OpensExactlyTheSavedPanels()
    {
        var r = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();
            Settle(window, dock);

            var audit = ToolsOf(layout).Single(t => t.Id == "AuditLog");
            var toolbox = ToolsOf(layout).Single(t => t.Id == ToolboxId);

            // 基线：**初始布局**里（12 个都在）除这两个之外有多少视图真的附着 ——
            // ⚠ 不是"每个都附着"：Dock 只实例化**活动标签**的内容，非活动标签是惰性的（实测 5/10）。
            var attachedInitial = ToolsOf(layout)
                .Where(t => t.Id != "AuditLog" && t.Id != ToolboxId)
                .Count(t => PanelState(t).Attached);

            factory.HideDockable(audit);        // 藏一个
            factory.CloseDockable(toolbox);     // 闭一个
            Settle(window, dock);
            var toolsBefore = ToolsOf(layout).Count;

            var state = LayoutSerializer.CaptureState(layout);
            var visibleIds = state.VisibleToolIds;

            // "重启"：**新的工厂实例**重建布局（与 MainWindow 装载同一条路径），再作用盘面状态
            var (factory2, layout2) = BuildLayout();
            factory2.RestoreLayoutState(layout2, state);
            var dock2 = new DockControl { Factory = factory2, Layout = layout2 };
            var window2 = new Window { Width = 1280, Height = 800, Content = dock2 };
            window2.Show();
            Settle(window2, dock2);

            var ids2 = ToolsOf(layout2).Select(t => t.Id ?? "?").ToList();
            var whereAudit2 = DocksContaining(layout2, "AuditLog");
            var whereToolbox2 = DocksContaining(layout2, ToolboxId);
            var hiddenAfter = (layout2.HiddenDockables ?? new List<IDockable>())
                .Any(d => d.Id == "AuditLog" || d.Id == ToolboxId);
            var openWithoutClosed = ids2.Count(id => id != "AuditLog" && id != ToolboxId);
            var othersInDocks = ToolsOf(layout2)
                .Where(t => t.Id != "AuditLog" && t.Id != ToolboxId)
                .Count(t => ParentDockOf(layout2, t) is not null);
            var attachedOthers = ToolsOf(layout2)
                .Where(t => t.Id != "AuditLog" && t.Id != ToolboxId)
                .Count(t => PanelState(t).Attached);

            // 恢复腿：被关掉的那一个**没丢** —— 从「面板」菜单那一跳再打开
            factory2.OpenDockable(layout2, ToolboxId);
            Settle(window2, dock2);
            var reopened = DocksContaining(layout2, ToolboxId);
            var reopenedTool = ToolsOf(layout2).SingleOrDefault(t => t.Id == ToolboxId);
            var reopenedAttached = reopenedTool is not null && PanelState(reopenedTool).Attached;
            var occurrences = VisibleOccurrences(layout2, ToolboxId);

            window2.Close();
            window.Close();
            return (toolsBefore, attachedInitial, visibleCount: visibleIds?.Count ?? -1,
                    capturedAudit: visibleIds?.Contains("AuditLog") == true,
                    capturedToolbox: visibleIds?.Contains(ToolboxId) == true,
                    capturedCanvas: visibleIds?.Contains("Canvas") == true,
                    ids2Count: ids2.Count, distinct: ids2.Distinct().Count(),
                    whereAudit2, whereToolbox2, hiddenAfter, openWithoutClosed, othersInDocks, attachedOthers,
                    reopened, reopenedAttached, occurrences);
        });

        // **前提自检**：藏 / 闭真的生效，且盘面里**有一份可见工具清单**、不含这两个
        Assert.True(r.toolsBefore == ExpectedToolDockables - 1,
            $"存盘前布局模型里应当只有 {ExpectedToolDockables - 1} 个工具（藏起来的那一个还在模型里、被 × 摘掉的不在），实测 {r.toolsBefore}");
        Assert.True(r.visibleCount > 0, "盘面里没有「可见工具」清单（`VisibleToolIds`）—— 本用例失去意义");
        Assert.False(r.capturedAudit, "藏起来的面板进了「可见工具」清单 —— 重启后它不该被打开");
        Assert.False(r.capturedToolbox, "被 × 关掉的面板进了「可见工具」清单 —— 重启后它不该被打开");
        Assert.True(r.capturedCanvas, "画布不在「可见工具」清单里 —— 采集口径不对（它 `CanClose = false`，一定在）");

        // 契约：**只打开保存的面板**
        // ⚠ 先判"落在哪个状态"再判数量：`HideDockable` 也把面板移出可见 dock，但它仍在模型里
        // ⇒ 数量会露馅（12 而不是 10）；先判 `HiddenDockables` 才能让"裁剪走错路"红得**指名道姓**。
        Assert.False(r.hiddenAfter, "重启后它们被放进了 `HiddenDockables` —— 裁剪走的不是 × 那条路（应是 `CloseDockable`）");
        Assert.True(r.ids2Count == ExpectedToolDockables - 2,
            $"重启后应当只开 {ExpectedToolDockables - 2} 个面板，实测 {r.ids2Count}（藏={r.whereAudit2} / 闭={r.whereToolbox2}）");
        Assert.True(r.distinct == r.ids2Count, $"重启后工具 id 有重复：{r.ids2Count} 个里只有 {r.distinct} 个唯一");
        Assert.True(r.whereAudit2 == "(无)" && r.whereToolbox2 == "(无)",
            $"重启后藏 / 闭的面板又回到可见 dock 了（{r.whereAudit2} / {r.whereToolbox2}）—— 决策没落实");
        Assert.True(r.openWithoutClosed == ExpectedToolDockables - 2, "其余面板数量不对");
        Assert.True(r.othersInDocks == ExpectedToolDockables - 2,
            $"留下的 {ExpectedToolDockables - 2} 个面板里有 {ExpectedToolDockables - 2 - r.othersInDocks} 个不在任何 dock 里"
            + "（被藏/被钉也算没打开）");
        // ⚠ 不要求"每个留下的面板都附着"：**非活动标签是惰性的**（初始布局实测 5/10）。
        // 要守的是"裁剪**不会**让留下的面板比初始布局更少被附着"（也就是别裁出空白壳）。
        Assert.True(r.attachedOthers >= r.attachedInitial,
            $"重启后附着的视图比初始布局还少（{r.attachedOthers} < {r.attachedInitial}）—— 裁剪裁出了空白壳");

        // **没丢**：被关掉的还能从「面板」菜单那一跳找回来（阶段 162/163 那条路）
        Assert.True(r.occurrences == 1,
            $"「面板」菜单再打开之后工具在可见布局里出现 {r.occurrences} 次（{r.reopened}）");
        Assert.True(r.reopenedAttached, "再打开了但视图没附着（空白壳）");
    }

    /// <summary>
    /// **向后兼容判据（阶段 165）**：**旧盘面**（这一版之前写下的 `.layout.json`：`VisibleToolIds` 缺失）
    /// ⇒ **不裁剪**（保持"全部打开"的旧行为）。
    ///
    /// <para>⚠ 这条守的是一个**很容易搞反**的语义：`null` ＝ **"信息缺失"**（旧文件），
    /// **不是**"一个都不要打开" —— 后者会让老用户升级后**所有面板消失**。</para>
    /// </summary>
    [Fact]
    public void Restart_WithLegacyStateWithoutVisibleToolIds_OpensEveryPanel()
    {
        var r = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();
            Settle(window, dock);

            var toolbox = ToolsOf(layout).Single(t => t.Id == ToolboxId);
            factory.CloseDockable(toolbox);      // 关一个，再"写出一份旧格式盘面"
            Settle(window, dock);

            var state = LayoutSerializer.CaptureState(layout);
            state.VisibleToolIds = null;         // 模拟旧文件：那份信息**根本不存在**

            var (factory2, layout2) = BuildLayout();
            factory2.RestoreLayoutState(layout2, state);
            var dock2 = new DockControl { Factory = factory2, Layout = layout2 };
            var window2 = new Window { Width = 1280, Height = 800, Content = dock2 };
            window2.Show();
            Settle(window2, dock2);

            var ids2 = ToolsOf(layout2).Select(t => t.Id ?? "?").ToList();
            var whereToolbox2 = DocksContaining(layout2, ToolboxId);
            var toolbox2 = ToolsOf(layout2).SingleOrDefault(t => t.Id == ToolboxId);

            window2.Close();
            window.Close();
            return (count: ids2.Count, distinct: ids2.Distinct().Count(), whereToolbox2,
                    attached: toolbox2 is not null && PanelState(toolbox2).Attached);
        });

        Assert.True(r.count == ExpectedToolDockables,
            $"旧盘面（没有「可见工具」清单）重启后应当**全部打开** {ExpectedToolDockables} 个，实测 {r.count}"
            + " —— 把「信息缺失」当成「一个都不要」会让老用户升级后所有面板消失");
        Assert.True(r.distinct == r.count, $"工具 id 有重复：{r.count} 个里只有 {r.distinct} 个唯一");
        Assert.True(r.whereToolbox2 != "(无)" && r.attached,
            $"旧盘面里被关掉的那一个没被打开（{r.whereToolbox2}，附着={r.attached}）");
    }

    /// <summary>
    /// **判据（阶段 164）**：`LayoutSerializer.ApplyState` 面对**盘面里带 `IsOpen = false` 的项**不许抛异常。
    ///
    /// <para><b>怎么发现的</b>：为 `#80`（"藏 / 钉状态不进持久化"）试写"把藏起来的也存进去"那一版时，
    /// `ApplyState` 抛 <c>InvalidOperationException: Collection was modified</c> ——
    /// `ApplyRecursive` **边枚举 `VisibleDockables` 边应用状态**，而 `SetIsOpen(false)` 会驱动 Dock
    /// 自己把该 dockable 从那张列表里摘掉。`IsOpen = false` 是这个盘面格式**本来就允许**的值
    /// （`DockableState.IsOpen` 是 `bool`），而调用点在 <c>MainWindow</c> 构造期、**没有 try/catch**
    /// ⇒ 一个这样的 `.layout.json` 就是**启动期异常**。修法：应用前先**快照**子项列表。</para>
    /// </summary>
    [Fact]
    public void ApplyState_WithIsOpenFalseEntry_DoesNotThrow()
    {
        var r = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();

            var state = new LayoutState
            {
                Dockables = new List<DockableState>
                {
                    new() { Id = "AuditLog", Proportion = 0.25, IsOpen = false },
                },
            };

            string? error = null;
            try
            {
                LayoutSerializer.ApplyState(layout, state);
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
            }

            var ids = ToolsOf(layout).Select(t => t.Id ?? "?").ToList();
            var where = DocksContaining(layout, "AuditLog");
            return (error, count: ids.Count, distinct: ids.Distinct().Count(), where);
        });

        Assert.True(r.error is null,
            $"ApplyState 面对 `IsOpen = false` 的盘面项抛了异常（{r.error}）—— 盘面格式允许这个值，"
            + "而调用点在 MainWindow 构造期且无保护 ⇒ 会变成启动期异常");
        // 工具**不丢**（`IsOpen = false` 只是把它移出可见区，不是从模型里删掉）
        Assert.True(r.count == ExpectedToolDockables,
            $"应用之后布局模型里应当仍有 {ExpectedToolDockables} 个工具（关掉 ≠ 丢掉），实测 {r.count}");
        Assert.True(r.distinct == r.count, $"工具 id 出现重复：{r.count} 个里只有 {r.distinct} 个唯一");
        // 而且这个标志**真的被应用了**（否则"没抛异常"这条会恒绿）
        Assert.True(r.where == "(无)",
            $"盘面里 `IsOpen = false` 的那一项应用之后仍留在某个 dock 的可见列表里（{r.where}）—— 标志没生效");
    }

    /// <summary>某 id 的工具在**全部** dock 的 `VisibleDockables` 里一共出现几次（抓重复插入）。</summary>
    private static int VisibleOccurrences(IDockable layout, string id) =>
        Walk(layout).OfType<IDock>().Sum(d => (d.VisibleDockables ?? new List<IDockable>()).Count(t => t.Id == id));

    /// <summary>某 id 的工具出现在哪些 dock 的 `VisibleDockables` 里、各几次（找重复插入用）。</summary>
    private static string DocksContaining(IDockable layout, string id)
    {
        var parts = new List<string>();
        foreach (var d in Walk(layout).OfType<IDock>())
        {
            var n = (d.VisibleDockables ?? new List<IDockable>()).Count(t => t.Id == id);
            if (n > 0) parts.Add($"{d.Id ?? "?"}×{n}");
        }
        return parts.Count == 0 ? "(无)" : string.Join(", ", parts);
    }

    // ────────────────────────── 判据自检（双向） ──────────────────────────

    /// <summary>
    /// <b>判据自检</b>：证明 <see cref="PanelState"/> 真的能识别归档问题单的现场签名
    /// （"视图解析得出来、但没挂在可视树上"），而不是恒绿。
    ///
    /// <para><b>为什么必须有这条</b>：本轮实测了一个反直觉的结果 —— 把 <c>ViewLocator.CanResolve</c>
    /// 退回**原始缺陷判据**（只认 <c>Context is Control</c>）后，上面两条拖拽用例**照样全绿**。
    /// 也就是说：Dock 12.1 的真实标签拖拽**不会**清空 <c>Context</c>，
    /// 归档问题单假设 D 的机制在这条路径上**不复现**（那条缺陷当年可能由别的路径或别的 Dock 版本触发）。
    /// 于是上面两条是**回归网**（能挡住将来变空白），但**没有**在已知缺陷上被证明会红 ——
    /// 所以这里单独把"判据能失败"钉住：负例（未附着的视图必须判为 Attached=false）
    /// 与正例（已附着的视图必须判为 true）各一条。</para>
    /// </summary>
    [Fact]
    public void PanelState_JudgeSelfCheck_DetectsBothAttachedAndDetached()
    {
        UiTestHost.Run(() =>
        {
            // 负例：构造出"解析得出来但没挂上树"的形态 —— 正是问题单里的 DETACHED
            var detached = new Tool { Id = "Detached", Context = new ToolboxView() };
            var detachedState = PanelState(detached);
            Assert.False(detachedState.Attached,
                "PanelState 把未附着的视图判成了已附着 —— 上面两条拖拽用例的'不空白'断言会恒绿");

            // 正例：真的挂进窗口后必须判为已附着（否则判据是"永远 false"的另一种恒绿）
            var attachedView = new ToolboxView();
            var window = new Window { Width = 400, Height = 300, Content = attachedView };
            window.Show();
            Settle(window, new DockControl());

            var attached = new Tool { Id = "Attached", Context = attachedView };
            var attachedState = PanelState(attached);
            window.Close();

            Assert.True(attachedState.Attached,
                "PanelState 把已附着的视图判成了未附着 —— 判据方向反了");
        });
    }

    /// <summary>
    /// **归档问题单的逐字方向**（阶段 225 才第一次可判）：拖动**项目浏览器**的标签，放到**工具箱**
    /// 那条 dock 上，面板必须仍然附着且有内容。
    ///
    /// <para><b>为什么此前打不到</b>：项目浏览器独占一条 dock（单工具）⇒ Dock 的 `ToolTabStrip`
    /// 只有 1 px 高、且**不创建 `ToolTabStripItem`** ⇒ **没有东西可拖**（阶段 224 量清，
    /// 登记 `#26`）。阶段 225 按处置②把「资产管理」挪进那条 dock ⇒ 它有了正常的把手，
    /// 于是这条**原文方向**终于能被指针事件驱动。</para>
    ///
    /// <para>与镜像方向那条（拖工具箱到左上区）走的是同一条重组路径 —— 两条一起，`#26` 的覆盖
    /// 不再靠"同形方向"顶替。</para>
    /// </summary>
    [Fact]
    public void DraggingProjectExplorerTabIntoTheToolboxDock_KeepsPanelPopulated()
    {
        const string explorerId = "ProjectExplorer";

        var result = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();
            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();
            Settle(window, dock);

            var explorer = ToolsOf(layout).Single(t => t.Id == explorerId);
            var before = PanelState(explorer);
            var parentBefore = ParentDockOf(layout, explorer);

            // 工具箱那条 dock 在左下（左上区是 (0,0)-(216,437)）⇒ 取它的中心。
            // ⚠ 先把把手坐标印出来（阶段 224 的教训：没有把手时 `DragToolTab` 会明确断言，
            //   不会静默空转）。
            DragToolTab(window, dock, explorerId, new Point(108, 620));

            var afterExplorer = PanelState(explorer);
            var parentAfter = ParentDockOf(layout, explorer);

            window.Close();
            return (before, afterExplorer,
                    Resolves: new ViewLocator().Build(explorer) is not null,
                    ToolCount: ToolsOf(layout).Count,
                    Moved: !ReferenceEquals(parentBefore, parentAfter),
                    ParentAfterId: parentAfter?.Id ?? "<none>");
        });

        Assert.True(result.before.Attached && result.before.Descendants > 0,
            $"拖拽前项目浏览器就没内容（{result.before}），本用例失去意义");
        Assert.True(result.Moved,
            "指针拖拽没有改变项目浏览器的承载 dock —— 拖拽路径没被驱动（把手又没了？）");
        Assert.Equal(ExpectedToolDockables, result.ToolCount);
        Assert.True(result.Resolves, "拖拽后项目浏览器解析不出视图");
        Assert.True(result.afterExplorer.Attached,
            $"拖拽后项目浏览器**不挂在可视树上**（落在 dock '{result.ParentAfterId}'）—— 正是归档问题单的现场签名");
        Assert.True(result.afterExplorer.Descendants > 0,
            "拖拽后项目浏览器挂在树上但一个可视后代都没有");
    }
}
