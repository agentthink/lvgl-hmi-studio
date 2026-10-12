using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using HmiEditor.App;
using HmiEditor.App.Controls;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// Dock 视图解析契约守卫（headless）。
///
/// <para><b>为什么需要它</b>：<c>[dock-blank-after-drag]</c>（归档问题单，状态 OPEN）的症状是
/// 「拖动面板后内容变空白」。此前**零自动化覆盖**：<see cref="ViewSmokeUiTests"/> 只把视图
/// 单独构造出来跑一次布局，**完全不经过** <c>ViewLocator</c>/<c>HmiDockFactory</c>/<c>DockControl</c>
/// 这条链路，所以解析契约坏了没有任何测试会红。</para>
///
/// <para><b>本次修掉的真缺陷</b>：<c>ViewLocator.Match</c> 曾写成
/// <c>data is IDockable { Context: Control }</c> —— 那是 <c>Build</c> 的**前置条件**。
/// 于是 <c>Match</c> 返回 false 时 Avalonia 不会调 <c>Build</c>，<c>Build</c> 里那个
/// 「Context 还不是 Control 时用工厂兜底」的分支**按构造不可达**：12 处 <c>RegisterFactory</c>
/// 全是死代码。本文件把「<c>Match</c> 与 <c>Build</c> 判据一致」变成受测不变量。</para>
/// </summary>
public class DockViewResolutionUiTests
{
    /// <summary>布局里应有的面板 dockable 数量（<c>HmiDockFactory</c> 里 12 个 <c>Tool</c>）。</summary>
    private const int ExpectedToolDockables = 12;

    // ────────────────────────── 布局构建 ──────────────────────────

    /// <summary>按 <c>MainWindow.InitializeDocking</c> 的顺序真建一次布局（无头 UI 线程上）。</summary>
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

    /// <summary>深度优先遍历整棵树。用**引用相等**去重（Id 可能重复，不能拿 Id 当键）。</summary>
    private static List<IDockable> WalkAll(IDockable root)
    {
        var result = new List<IDockable>();
        var seen = new HashSet<IDockable>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<IDockable>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (!seen.Add(d)) continue;
            result.Add(d);

            foreach (var child in ChildrenOf(d))
                stack.Push(child);
        }

        return result;
    }

    /// <summary>
    /// 子节点。注意 Dock 12.1 的 API 分工：<c>VisibleDockables</c> 在 <see cref="IDock"/>，
    /// <c>HiddenDockables</c> / <c>PinnedDock</c> / <c>Windows</c> 在 <see cref="IRootDock"/>，
    /// 且 <c>Windows</c> 的元素是 <see cref="IDockWindow"/>（取其 <c>Layout</c>）。
    /// </summary>
    private static IEnumerable<IDockable> ChildrenOf(IDockable d)
    {
        if (d is IDock dock && dock.VisibleDockables is { } visible)
            foreach (var c in visible) yield return c;

        if (d is IRootDock root)
        {
            if (root.HiddenDockables is { } hidden)
                foreach (var c in hidden) yield return c;
            if (root.PinnedDock is { } pinned)
                yield return pinned;
            if (root.Windows is { } windows)
                foreach (var w in windows)
                    if (w.Layout is { } windowLayout)
                        yield return windowLayout;
        }
    }

    private static List<IDockable> ToolsOf(IEnumerable<IDockable> all) =>
        all.Where(d => d is Tool).ToList();

    // ────────────────────────── 走查器自检 ──────────────────────────

    /// <summary>
    /// 走查器自检：若遍历写错（漏掉子树 / 去重键用错），下面所有守卫都会变成**空跑**而恒绿。
    /// 这条先证明「真的走到了那 12 个面板」。
    /// </summary>
    [Fact]
    public void Walker_ActuallyFindsEveryToolDockable()
    {
        var ids = UiTestHost.Run(() =>
        {
            var (_, layout) = BuildLayout();
            return ToolsOf(WalkAll(layout)).Select(t => t.Id!).OrderBy(x => x, StringComparer.Ordinal).ToList();
        });

        Assert.True(ids.Count == ExpectedToolDockables,
            $"只走到 {ids.Count} 个 Tool（应为 {ExpectedToolDockables}）: [{string.Join(", ", ids)}]");
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    // ────────────────────────── 核心契约 ──────────────────────────

    /// <summary>
    /// 元守卫：<b><c>Match(d)</c> 必须与「<c>Build(d)</c> 是否产出内容」逐一一致</b>。
    ///
    /// <para>这是本轮那个缺陷的**直接否定形式**：两者判据一旦分叉，就会有一条分支永远走不到。</para>
    ///
    /// <para><b>必须在两种状态下各查一次</b>：刚建好的布局里 12 个面板的 <c>Context</c> 都是非空的，
    /// 此时只看"能不能 <c>Build</c> 出内容"两种判据都会通过 —— 那样这条守卫**打不到产品侧**
    /// （实测：把 <c>Match</c> 退回只认 <c>Context: Control</c>，只看原始状态的版本照样绿）。
    /// <c>Context</c> 缺失才是回退分支唯一有意义的状态，所以清空后要再查一次。</para>
    ///
    /// <para>另注意求值顺序 —— 必须先记 <c>Match</c> 再调 <c>Build</c>：<c>Build</c> 会把
    /// <c>Context</c> 填上，之后 <c>Match</c> 必然为真，顺序反了就成恒绿。</para>
    /// </summary>
    [Fact]
    public void Match_AgreesWithBuild_ForEveryDockable()
    {
        var mismatches = UiTestHost.Run(() =>
        {
            var (_, layout) = BuildLayout();
            var locator = new ViewLocator();
            var bad = new List<string>();

            void Check(IDockable d, string phase)
            {
                var matched = locator.Match(d);                 // 先 Match
                var built = locator.Build(d);                   // 再 Build
                if (matched != (built is not null))
                    bad.Add($"[{phase}] {d.Id}: Match={matched} 但 Build={(built is null ? "null" : built.GetType().Name)}");
            }

            foreach (var d in WalkAll(layout))
            {
                Check(d, "原始状态");

                // 模拟 Dock 重组后 Context 丢失（正是归档问题单假设 D 的状态）
                d.Context = null;
                Check(d, "Context 缺失");
            }

            return bad;
        });

        Assert.True(mismatches.Count == 0,
            "ViewLocator 的 Match 与 Build 判据不一致（Match 为假时 Avalonia 不会调 Build，" +
            "于是能产出内容的那条分支成为死代码）:\n  " + string.Join("\n  ", mismatches));
    }

    /// <summary>
    /// 每个**叶子** dockable（<see cref="Tool"/>，即真正承载面板内容的那些）都必须解析出非空视图。
    ///
    /// <para>刻意**不**要求容器 dock（<c>Root</c>/<c>Main</c>/<c>LeftDock</c>/<c>CenterDocDock</c>…）
    /// 也能解析：它们的 <c>Context</c> 为空、也没注册工厂，由 Dock 自己的控件模板渲染 chrome ——
    /// 要求它们可解析是**断言写宽了**（实测这 9 个容器就是唯一"解析不出"的一批）。</para>
    /// </summary>
    [Fact]
    public void EveryToolDockable_ResolvesToANonNullView()
    {
        var report = UiTestHost.Run(() =>
        {
            var (_, layout) = BuildLayout();
            var locator = new ViewLocator();
            var bad = new List<string>();

            foreach (var tool in ToolsOf(WalkAll(layout)))
                if (locator.Build(tool) is null)
                    bad.Add(tool.Id ?? "<null id>");

            return bad;
        });

        Assert.True(report.Count == 0,
            "以下面板解析不出内容视图（面板会空白）:\n  " + string.Join("\n  ", report));
    }

    /// <summary>
    /// 工厂**不能在布局里落空**，也不能留**死注册** —— 两侧集合必须相等。
    ///
    /// <para>注册键与 <c>dockable.Id</c> 是两套手写的键空间，分叉时不会报错，只会静默回退或空白。
    /// 现在 <c>HmiDockFactory</c> 一律传 <c>xTool.Id</c>，这条守卫把该不变量钉住。</para>
    /// </summary>
    [Fact]
    public void RegisteredFactoryIds_MatchLayoutDockableIds()
    {
        // 两侧集合**全部**从真实布局与真实注册表推导，测试里不得再手抄一份 ID 清单
        // （手抄清单恰好对得上正是本项目反复踩的坑）。
        var (toolIds, registeredIds) = UiTestHost.Run(() =>
        {
            var (_, layout) = BuildLayout();
            var tools = ToolsOf(WalkAll(layout)).Select(t => t.Id!).ToList();
            return (tools, ViewLocator.RegisteredFactoryIds);
        });

        // 正向：每个 Tool 面板都必须有工厂兜底
        var missing = toolIds.Where(id => !registeredIds.Contains(id, StringComparer.Ordinal)).ToList();
        Assert.True(missing.Count == 0,
            "以下面板没有注册兜底工厂 —— Context 一旦丢失就永久空白: " + string.Join(", ", missing));

        // 反向：不能有注册了但布局里不存在的键（死注册 = 键空间已分叉）
        var dead = registeredIds.Where(id => !toolIds.Contains(id, StringComparer.Ordinal)).ToArray();
        Assert.True(dead.Length == 0,
            "RegisterFactory 注册了布局里不存在的 dockable.Id（死注册，键空间已分叉）: " + string.Join(", ", dead));
    }

    /// <summary>
    /// 归档问题单假设 D 的**直接复现 + 钉住**：Dock 重组可能把 <c>dockable.Context</c> 换掉/抹掉。
    /// 此时解析必须**仍然成功**（走工厂兜底），而不是「Match 为假 ⇒ 不渲染 ⇒ 永久空白」。
    /// </summary>
    [Fact]
    public void ContextCleared_StillResolves_ThroughFactoryFallback()
    {
        var report = UiTestHost.Run(() =>
        {
            var (_, layout) = BuildLayout();
            var locator = new ViewLocator();
            var lines = new List<string>();

            foreach (var tool in ToolsOf(WalkAll(layout)))
            {
                tool.Context = null;                     // 模拟重组后 Context 丢失
                var matched = locator.Match(tool);       // 先 Match
                var built = locator.Build(tool);         // 再 Build
                lines.Add($"{tool.Id}: Match={matched} Build={(built is null ? "null" : built.GetType().Name)}");
            }

            return lines;
        });

        var broken = report.Where(l => l.Contains("Match=False") || l.Contains("Build=null")).ToList();
        Assert.True(broken.Count == 0,
            "Context 被抹掉后面板解析失败（正是 dock-blank-after-drag 的机制 —— 解析器已放弃，谁都不再渲染）:\n  "
            + string.Join("\n  ", broken));
    }

    // ────────────────────────── 端到端冒烟 ──────────────────────────

    /// <summary>
    /// 端到端：真把 <c>DockControl</c> 挂进窗口跑一次。证明这条链路在无头环境下确实能产出可视内容
    /// （Dock 主题资源、Factory/Layout 绑定顺序、DeferredContent 物化都参与其中）。
    /// </summary>
    [Fact]
    public void DockControl_ActuallyMaterializesPanelViews()
    {
        var report = UiTestHost.Run(() =>
        {
            var (factory, layout) = BuildLayout();

            var dock = new DockControl { Factory = factory, Layout = layout };
            var window = new Window { Width = 1280, Height = 800, Content = dock };
            window.Show();

            // DeferredContentPresenter 是**惰性**的：它要等布局跑完、Load 队列排空后才加载内容。
            // headless 下没有真实渲染循环，必须显式把派发队列推到底，否则可视树里只剩 Dock 自己的 chrome。
            for (var i = 0; i < 3; i++)
            {
                UiTestHost.UIThread.RunJobs(DispatcherPriority.Loaded);
                window.Measure(new Size(1280, 800));
                window.Arrange(new Rect(0, 0, 1280, 800));
                UiTestHost.Pump(4);
            }
            UiTestHost.UIThread.RunJobs(DispatcherPriority.Background);

            var all = dock.GetVisualDescendants().OfType<Control>().ToList();
            var viewNames = all.Select(c => c.GetType().Name)
                .Where(n => n.EndsWith("View", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            var summary = $"descendants={all.Count}, registryViews={ViewLocator.RegisteredFactoryIds.Count}, " +
                          $"resolvedViews={ViewLocator.RegisteredFactoryIds.Count(id => ViewLocator.GetActiveView(id) is not null)}, " +
                          $"topTypes=[{string.Join(", ", all.Take(12).Select(c => c.GetType().Name))}]";
            window.Close();
            return (viewNames, summary);
        });

        Assert.True(report.viewNames.Count > 0,
            "DockControl 里一个面板视图都没有物化出来 —— Dock 内容区没有渲染（面板空白）。诊断: " + report.summary);
    }
}
