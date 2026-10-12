using Avalonia;
using HmiEditor.App.Resources;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using HmiEditor.App.Views;
using HmiEditor.App.Controls;
using System.Diagnostics;
using System.Linq;

namespace HmiEditor.App.Services;

/// <summary>
/// Dock layout factory for HMI Editor.
///
/// [dock-blank-after-drag] 修复核心思路：
/// 1. 每个面板的**共享** View 实例存在 <c>Tool.Context</c> 里；<c>ViewLocator</c> 优先复用它，
///    只有在 <c>Context</c> 还不是 Control 时才走工厂兜底（<c>_activeViews</c> 记录实际显示的实例）。
///    <b>注册键一律传该 tool 自己的 <c>Id</c></b>，不要另写字面量 —— 两套键空间分叉时不会报错，
///    只会静默回退到工厂或空白。
/// 2. OnDockableActivated 仅触发数据刷新，不再强制重建 Content
/// 3. 各面板在 <c>OnAttachedToVisualTree</c> 里从 DockLayoutState 自初始化
///    （ToolboxView / FaceplateLibraryView / ResourceLibraryView / DesignCanvasView）
///
/// <para><b>⚠️ 注释曾与代码不符，已更正并登记</b>：此处原写着「ViewLocator.Build 时可靠脱离旧 Parent」，
/// 但 <c>ViewLocator.Build</c> 里**没有任何脱离 Parent 的动作**。Dock 重组时共享实例可能仍挂在旧的
/// <c>DeferredContentControl</c> 上，这一条**未验证**（拖拽路径无法在无头环境下模拟），
/// 见 <c>docs/2026-09-15-dock-view-resolution-contract.md</c> 的「遗留」。</para>
/// </summary>
public class HmiDockFactory : Factory
{
    private readonly ToolboxView _toolboxView;
    private readonly FaceplateLibraryView _faceplateView;
    private readonly DesignCanvasView _canvasView;
    private readonly ProjectExplorerView _projectExplorerView;
    private readonly PropertyPanelView _propertyPanelView;
    private readonly LayerPanelView _layerPanelView;
    private readonly AssetManagerView _assetManagerView;
    private readonly NavigationFlowView _navigationFlowView;
    private readonly AuditLogView _auditLogView;
    private readonly CrossReferenceView _crossReferenceView;
    private readonly AiChatPanelView _aiChatView;
    private readonly ResourceLibraryView _resourceLibraryView;
    private readonly UndoRedoManager _undoRedoManager;

    private Tool? _toolboxTool;
    private Tool? _faceplateTool;
    private Tool? _canvasTool;
    private Tool? _projectExplorerTool;
    private Tool? _propertyTool;
    private Tool? _layerTool;
    private Tool? _assetTool;
    private Tool? _navigationTool;
    private Tool? _auditTool;
    private Tool? _crossRefTool;
    private Tool? _aiChatTool;
    private Tool? _resourceLibraryTool;

    /// <summary>
    /// 工具 id → **它原先所在的 dock**（阶段 162）。
    ///
    /// <para>为什么需要：标签栏上的 × 会 `CloseDockable`，把工具**从布局里整个摘掉**
    /// —— 既不在 `VisibleDockables`、也没进任何 `HiddenDockables`（本版只有 `IRootDock` 有那个列表），
    /// 只留下 `Owner` 指针。于是"面板"菜单那条 `OpenDockTool` 连 `FindDockable` 都找不到它，
    /// **点了没反应**，而本仓没有"重置布局"可点 ⇒ 只能重启。这张表让"再打开"能把工具挂回**老家**。</para>
    /// </summary>
    private readonly Dictionary<string, (Tool Tool, IDock Home)> _toolHomes = new(StringComparer.Ordinal);

    /// <summary>
    /// Dock 布局重组（拖拽/浮动/停靠切换）后触发。
    /// MainWindow 订阅此事件，统一刷新所有面板数据。
    /// </summary>
    public event Action? DockLayoutChanged;

    public HmiDockFactory(
        ToolboxView toolboxView,
        FaceplateLibraryView faceplateView,
        DesignCanvasView canvasView,
        ProjectExplorerView projectExplorerView,
        PropertyPanelView propertyPanelView,
        LayerPanelView layerPanelView,
        AssetManagerView assetManagerView,
        NavigationFlowView navigationFlowView,
        AuditLogView auditLogView,
        CrossReferenceView crossReferenceView,
        AiChatPanelView aiChatView,
        ResourceLibraryView resourceLibraryView,
        UndoRedoManager undoRedoManager)
    {
        _toolboxView = toolboxView;
        _faceplateView = faceplateView;
        _canvasView = canvasView;
        _projectExplorerView = projectExplorerView;
        _propertyPanelView = propertyPanelView;
        _layerPanelView = layerPanelView;
        _assetManagerView = assetManagerView;
        _navigationFlowView = navigationFlowView;
        _auditLogView = auditLogView;
        _crossReferenceView = crossReferenceView;
        _aiChatView = aiChatView;
        _resourceLibraryView = resourceLibraryView;
        _undoRedoManager = undoRedoManager;
    }

    public override IRootDock CreateLayout()
    {
        var root = CreateRootDock();
        root.Id = "Root";
        root.Title = "HMI Editor";

        // 用户自定义面板比例（UI 设置），无设置时回退到默认值
        var s = App.SettingsService;
        var leftProp = s?.Layout.LeftPanelProportion ?? 0.15;
        var centerProp = s?.Layout.RightPanelProportion is double rp ? 1.0 - leftProp - rp : 0.68;
        var rightProp = s?.Layout.RightPanelProportion ?? 0.17;
        var bottomProp = s?.Layout.BottomPanelProportion ?? 0.3;

        var mainDock = CreateProportionalDock();
        mainDock.Id = "Main";
        mainDock.Title = "Main";
        mainDock.Orientation = Dock.Model.Core.Orientation.Horizontal;

        // 左侧：上区=项目浏览器 ＋ **资产管理**（阶段 225 起同区），下区=工具箱+模板库。
        // 为什么把「资产管理」从右栏挪上来：**单工具 dock 不创建标签项** ⇒ 那个面板没有可拖把手
        // （登记 #26；阶段 224 量到它的标签条只有 1 px 高）。挪一个过来，四条工具 dock 就都有 ≥2 个工具，
        // 每个面板都有把手；右栏仍有 5 个工具，不空。
        var leftDock = CreateProportionalDock();
        leftDock.Id = "LeftDock";
        leftDock.Title = "Left";
        leftDock.Proportion = leftProp;
        leftDock.Orientation = Dock.Model.Core.Orientation.Vertical;

        var explorerTool = (Tool)CreateTool();
        explorerTool.Id = "ProjectExplorer";
        explorerTool.Title = AppLocalization.Service["dock.title.explorer"];
        explorerTool.Context = _projectExplorerView;
        explorerTool.IsOpen = true;
        _projectExplorerTool = explorerTool;

        ViewLocator.RegisterFactory(explorerTool.Id, () =>
        {
            var v = new ProjectExplorerView();
            if (DockLayoutState.CurrentProject != null)
                v.LoadProject(DockLayoutState.CurrentProject, DockLayoutState.CurrentScreen!);
            return v;
        });

        var leftUpperDock = CreateToolDock();
        leftUpperDock.Id = "LeftUpperDock";
        leftUpperDock.Title = "Project";
        leftUpperDock.Proportion = 0.55;
        // ⚠ VisibleDockables 在 assetTool 建好之后再设（见下方「左侧上区」那处赋值）
        leftUpperDock.ActiveDockable = explorerTool;

        var toolboxTool = (Tool)CreateTool();
        toolboxTool.Id = "Toolbox";
        toolboxTool.Title = AppLocalization.Service["dock.title.toolbox"];
        toolboxTool.Context = _toolboxView;
        toolboxTool.IsOpen = true;
        _toolboxView.HostTool = toolboxTool;
        _toolboxTool = toolboxTool;

        // 工厂用于动态场景（Context 为 null 时的回退）
        // 正常情况下优先使用 Context（共享实例）路径
        ViewLocator.RegisterFactory(toolboxTool.Id, () =>
        {
            var v = new ToolboxView();
            v.HostTool = _toolboxTool;
            return v;
        });

        var faceplateTool = (Tool)CreateTool();
        faceplateTool.Id = "Faceplate";
        faceplateTool.Title = Resources.AppLocalization.Service["faceplate.title"];
        faceplateTool.Context = _faceplateView;
        faceplateTool.IsOpen = true;
        _faceplateView.HostTool = faceplateTool;
        _faceplateTool = faceplateTool;

        ViewLocator.RegisterFactory(faceplateTool.Id, () =>
        {
            var v = new FaceplateLibraryView();
            v.HostTool = _faceplateTool;
            return v;
        });

        var leftLowerDock = CreateToolDock();
        leftLowerDock.Id = "LeftLowerDock";
        leftLowerDock.Title = "Palette";
        leftLowerDock.Proportion = 0.45;
        leftLowerDock.VisibleDockables = CreateList<IDockable>(toolboxTool, faceplateTool);
        leftLowerDock.ActiveDockable = toolboxTool;

        var leftSplitter = CreateProportionalDockSplitter();
        leftDock.VisibleDockables = CreateList<IDockable>(leftUpperDock, leftSplitter, leftLowerDock);
        leftDock.ActiveDockable = leftUpperDock;

        var splitter1 = CreateProportionalDockSplitter();

        var centerDock = CreateProportionalDock();
        centerDock.Id = "Center";
        centerDock.Title = "Center";
        centerDock.Orientation = Dock.Model.Core.Orientation.Vertical;
        centerDock.Proportion = centerProp;

        var canvasTool = (Tool)CreateTool();
        canvasTool.Id = "Canvas";
        canvasTool.Title = AppLocalization.Service["dock.title.canvas"];
        canvasTool.Context = _canvasView;
        canvasTool.IsOpen = true;
        canvasTool.CanClose = false;
        _canvasTool = canvasTool;

        ViewLocator.RegisterFactory(canvasTool.Id, () =>
        {
            var v = new DesignCanvasView(_undoRedoManager, App.SettingsService);
            if (DockLayoutState.CurrentScreen != null)
                v.SetScreen(DockLayoutState.CurrentScreen);
            return v;
        });

        var centerDocDock = CreateToolDock();
        centerDocDock.Id = "CenterDocDock";
        centerDocDock.Title = "Documents";
        centerDocDock.Proportion = 0.7;
        centerDocDock.VisibleDockables = CreateList<IDockable>(canvasTool);
        centerDocDock.ActiveDockable = canvasTool;

        var splitter2 = CreateProportionalDockSplitter();

        var bottomDock = CreateToolDock();
        bottomDock.Id = "BottomDock";
        bottomDock.Title = "Bottom";
        bottomDock.Alignment = Dock.Model.Core.Alignment.Bottom;
        bottomDock.GripMode = Dock.Model.Core.GripMode.Visible;
        bottomDock.Proportion = bottomProp;

        var auditTool = (Tool)CreateTool();
        auditTool.Id = "AuditLog";
        auditTool.Title = AppLocalization.Service["dock.title.audit"];
        auditTool.Context = _auditLogView;
        auditTool.IsOpen = true;
        _auditTool = auditTool;

        ViewLocator.RegisterFactory(auditTool.Id, () =>
        {
            var v = new AuditLogView();
            v.Refresh();
            return v;
        });

        var crossRefTool = (Tool)CreateTool();
        crossRefTool.Id = "CrossReference";
        crossRefTool.Title = AppLocalization.Service["dock.title.crossref"];
        crossRefTool.Context = _crossReferenceView;
        crossRefTool.IsOpen = true;
        _crossRefTool = crossRefTool;

        ViewLocator.RegisterFactory(crossRefTool.Id, () =>
        {
            var v = new CrossReferenceView();
            if (DockLayoutState.CurrentProject != null)
                v.LoadProject(DockLayoutState.CurrentProject);
            return v;
        });

        bottomDock.VisibleDockables = CreateList<IDockable>(auditTool, crossRefTool);
        bottomDock.ActiveDockable = auditTool;

        centerDock.VisibleDockables = CreateList<IDockable>(centerDocDock, splitter2, bottomDock);
        centerDock.ActiveDockable = centerDocDock;

        var splitter3 = CreateProportionalDockSplitter();

        var rightDock = CreateToolDock();
        rightDock.Id = "RightDock";
        rightDock.Title = "Right";
        rightDock.Alignment = Dock.Model.Core.Alignment.Right;
        rightDock.GripMode = Dock.Model.Core.GripMode.Visible;
        rightDock.Proportion = rightProp;

        var propertyTool = (Tool)CreateTool();
        propertyTool.Id = "Property";
        propertyTool.Title = AppLocalization.Service["dock.title.property"];
        propertyTool.Context = _propertyPanelView;
        propertyTool.IsOpen = true;
        _propertyTool = propertyTool;

        ViewLocator.RegisterFactory(propertyTool.Id, () =>
        {
            var v = new PropertyPanelView();
            v.CurrentScreen = DockLayoutState.CurrentScreen;
            if (DockLayoutState.CurrentProject != null)
            {
                v.SetProject(DockLayoutState.CurrentProject);
                v.SetVariableManager(DockLayoutState.CurrentProject.VariableManager);
            }
            if (DockLayoutState.SelectedControl != null)
                v.BindControl(DockLayoutState.SelectedControl);
            return v;
        });

        var layerTool = (Tool)CreateTool();
        layerTool.Id = "Layers";
        layerTool.Title = AppLocalization.Service["dock.title.layers"];
        layerTool.Context = _layerPanelView;
        layerTool.IsOpen = true;
        _layerTool = layerTool;

        ViewLocator.RegisterFactory(layerTool.Id, () =>
        {
            var v = new LayerPanelView();
            v.LoadScreen(DockLayoutState.CurrentScreen);
            return v;
        });

        var assetTool = (Tool)CreateTool();
        assetTool.Id = "Assets";
        assetTool.Title = AppLocalization.Service["dock.title.assets"];
        assetTool.Context = _assetManagerView;
        assetTool.IsOpen = true;
        _assetTool = assetTool;

        ViewLocator.RegisterFactory(assetTool.Id, () =>
        {
            var v = new AssetManagerView();
            if (DockLayoutState.AssetLibrary != null)
                v.LoadAssets(DockLayoutState.AssetLibrary);
            return v;
        });

        // 阶段 225（登记 #26 的处置②）：**左侧上区 = 项目浏览器 ＋ 资产管理** ——
        // 这样那条 dock 有 2 个工具 ⇒ Dock 会给出 34 px 的正常标签条与可拖把手（原来是 1 px 且无把手）。
        leftUpperDock.VisibleDockables = CreateList<IDockable>(explorerTool, assetTool);

        var navTool = (Tool)CreateTool();
        navTool.Id = "Navigation";
        navTool.Title = AppLocalization.Service["dock.title.navigation"];
        navTool.Context = _navigationFlowView;
        navTool.IsOpen = true;
        _navigationTool = navTool;

        ViewLocator.RegisterFactory(navTool.Id, () =>
        {
            var v = new NavigationFlowView();
            if (DockLayoutState.CurrentProject != null)
                v.LoadProject(DockLayoutState.CurrentProject);
            return v;
        });

        var aiChatTool = (Tool)CreateTool();
        aiChatTool.Id = "AiChat";
        aiChatTool.Title = AppLocalization.Service["dock.title.aichat"];
        aiChatTool.Context = _aiChatView;
        aiChatTool.IsOpen = true;
        _aiChatTool = aiChatTool;

        ViewLocator.RegisterFactory(aiChatTool.Id, () =>
        {
            if (_aiChatView.CurrentProject != DockLayoutState.CurrentProject)
                _aiChatView.CurrentProject = DockLayoutState.CurrentProject;
            return _aiChatView;
        });

        var resourceLibTool = (Tool)CreateTool();
        resourceLibTool.Id = "ResourceLibrary";
        resourceLibTool.Title = AppLocalization.Service["dock.title.resource.library"];
        resourceLibTool.Context = _resourceLibraryView;
        resourceLibTool.IsOpen = true;
        _resourceLibraryTool = resourceLibTool;

        ViewLocator.RegisterFactory(resourceLibTool.Id, () =>
        {
            var v = new ResourceLibraryView();
            if (DockLayoutState.ResourceLibrary != null)
                v.LoadLibrary(DockLayoutState.ResourceLibrary);
            return v;
        });

        rightDock.VisibleDockables = CreateList<IDockable>(propertyTool, layerTool, navTool, aiChatTool, resourceLibTool);
        rightDock.ActiveDockable = propertyTool;

        mainDock.VisibleDockables = CreateList<IDockable>(
            leftDock, splitter1, centerDock, splitter3, rightDock);
        mainDock.ActiveDockable = centerDock;

        root.VisibleDockables = CreateList<IDockable>(mainDock);
        root.ActiveDockable = mainDock;

        // 记下每个工具的"老家" —— 被关闭（从布局摘掉）之后，"面板"菜单要靠它把工具挂回来（阶段 162）。
        foreach (var dock in EnumerateDocks(root))
            foreach (var tool in (dock.VisibleDockables ?? CreateList<IDockable>()).OfType<Tool>())
                if (!string.IsNullOrEmpty(tool.Id))
                    _toolHomes[tool.Id!] = (tool, dock);

        return root;
    }

    /// <summary>深度枚举布局里的全部 <see cref="IDock"/>（只看可见树 —— 被关闭的工具本来就不在这里）。</summary>
    private static IEnumerable<IDock> EnumerateDocks(IDockable root)
    {
        var seen = new HashSet<IDockable>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<IDockable>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current)) continue;
            if (current is not IDock dock) continue;

            yield return dock;
            foreach (var child in dock.VisibleDockables ?? Array.Empty<IDockable>())
                stack.Push(child);
        }
    }

    /// <summary>
    /// **把盘面状态作用到这张布局上**（阶段 165 从 `MainWindow` 收进来的入口，判据够得着）：
    /// 先 `LayoutSerializer.ApplyState` 覆盖 `Proportion`/`IsOpen`；再按盘面里记的
    /// **"保存时可见的工具"** 裁剪 —— **只打开保存的面板**。
    ///
    /// <para><b>为什么这么定</b>（产品决策，登记 `#80`）：被标签栏的 × 关掉的面板**下次启动保持关闭**，
    /// 而不是"又自己回来"。裁剪走的正是 × 那条路（`CloseDockable`）⇒ 重启后它与"刚被关掉"是**同一个状态**，
    /// 用户随时能从「面板」菜单再打开（`OpenDockable` 那条路，阶段 162/163 已修好）。</para>
    ///
    /// <para>⚠ **旧盘面**（`VisibleToolIds == null`，这一版之前写下的文件）信息缺失 ⇒ **不裁剪**
    /// （保持"全部打开"的旧行为）：不能把"不知道"当成"一个都不要"。</para>
    ///
    /// <para>⚠ 顺带说明**藏起来 / 钉住**的面板：它们也不在可见树里 ⇒ 同样不进这一份 ⇒ 重启后同样**不打开**
    /// （可从那菜单找回）。这与"只打开保存的面板"是同一条规则。</para>
    /// </summary>
    public void RestoreLayoutState(IRootDock layout, LayoutState? state)
    {
        LayoutSerializer.ApplyState(layout, state);

        if (state?.VisibleToolIds is not { } visibleIds) return;   // 旧盘面 ⇒ 保持旧行为（全部打开）

        var open = new HashSet<string>(visibleIds, StringComparer.Ordinal);
        foreach (var (id, home) in _toolHomes.ToList())
        {
            if (!open.Contains(id)) CloseDockable(home.Tool);
        }
    }

    /// <summary>
    /// **"面板"菜单的统一入口**（阶段 162 从 `MainWindow.OpenDockTool` 移进来）：
    /// 把指定工具置顶激活；**若它被标签栏的 × 关闭过**（已从布局里整个摘掉 ⇒ `FindDockable`
    /// 找不到它），就按"老家"挂回去 —— 否则菜单那一项点了**没反应**。
    ///
    /// <para>⚠ 移进工厂不只是为了少几行：这样判据能**直接打产品路径**
    /// （`DockDragRegressionUiTests.CloseThenReopen_LikeTheMenu_KeepsPanelVisible`），
    /// 而 `MainWindow` 那份私有方法在 headless 里够不着。顺带 `MainWindow.axaml.cs` **变小**，
    /// 符合本仓那条"单文件只许变小"的基线纪律。</para>
    /// </summary>
    public void OpenDockable(IRootDock layout, string dockableId)
    {
        var dockable = FindDockable(layout,
            d => string.Equals(d.Id, dockableId, StringComparison.Ordinal));

        if (dockable is Tool tool)
        {
            tool.IsOpen = true;
            // 置顶激活：设父 dock 的 ActiveDockable，使面板切到前台
            if (tool.Owner is IDock owner) owner.ActiveDockable = tool;
            return;
        }

        // 被 × 关闭过：`CloseDockable` 把它从布局里整个摘掉（连 `HiddenDockables` 都不进）⇒ 挂回老家
        TryRestoreClosedTool(layout, dockableId);
    }

    /// <summary>
    /// **把被关闭（已从布局移除）的工具挂回它原来的 dock**（阶段 162）。
    ///
    /// <para>实测（`DockDragRegressionUiTests.CloseThenReopen_LikeTheMenu_KeepsPanelVisible`）：
    /// `CloseDockable` 之后工具既不在 `VisibleDockables`、也不在任何 `HiddenDockables` 里，
    /// `FindDockable` 返回 <c>null</c>、工具数 12 → 11；而按"老家"重新 `AddDockable` 之后
    /// 视图**能重新附着**（面板真的回来了，不是空白壳）。</para>
    ///
    /// <para>**两个入口不一样**（阶段 163 修正）：钩子把面板"藏"起来时工具仍在
    /// `RootDock.HiddenDockables` 里，此时**只能置 `IsOpen`**让 Dock 自己放回去 —— 再 `AddDockable`
    /// 会让同一个 dock 出现两条同名项；只有被 × 摘掉（哪个列表都不在）才由我们挂回老家。</para>
    ///
    /// <returns>真的挂回去了返回 <c>true</c>；工具还在布局里、或这张布局不是它的老家所在的那张，
    /// 返回 <c>false</c>（调用方该走 <c>FindDockable</c> 那条）。</returns>
    /// </summary>
    public bool TryRestoreClosedTool(IRootDock layout, string dockableId)
    {
        if (!_toolHomes.TryGetValue(dockableId, out var home)) return false;
        if (home.Tool.Owner is IDock owner && owner.VisibleDockables?.Contains(home.Tool) == true)
            return false;                                   // 还在布局里，不该走这条路
        if (!EnumerateDocks(layout).Contains(home.Home)) return false;   // 传进来的不是它那张布局

        // ⚠ 三条入口各走各的（阶段 163 逐条实测：混着走都会留下"用户看得见的脏状态"）。
        //   · 钉住（在 `BottomPinnedDockables` 之类里；`FindDockable` 看不见它）：`UnpinDockable` 会把它
        //     放回原 dock **并重新附着视图**（实测 `BottomDock×1` + 视图 38 个后代），钉列表也清空。
        //     若我们自己 `AddDockable`，工具会**同时**留在钉列表和 dock 里（实测 `钉列表=[B]` + `BottomDock×1`）——
        //     菜单里点一下等于"钉住"没被撤销（钉住高亮/钉条条目还在），是脏状态。
        //     两种钉法都实测过：`factory.PinDockable(tool)` 直接钉 **和** 用户点 dock chrome 上的
        //     `PART_PinButton`（`DataContext` = 那张 ToolDock），`IsDockablePinned` 都返回 `true`
        //     （阶段 163 判据 `PinnedViaDockChrome_ReopenedViaMenu_ComesBackAndClearsThePin` 走的是后者）。
        //   · 藏（还在 `RootDock.HiddenDockables` 里）：**只置 `IsOpen = true`** —— Dock 自己会把它放回
        //     原来那个 dock；我们再插一手就**重复**了（实测同一个 dock 里两条同名项）。
        //   · 闭（被 × 从布局里整个摘掉，连 `HiddenDockables` 都不进）：只能由我们挂回老家。
        if (IsPinned(layout, home.Tool))
        {
            UnpinDockable(home.Tool);
        }
        else if (IsHidden(layout, home.Tool))
        {
            home.Tool.IsOpen = true;
        }
        else
        {
            AddDockable(home.Home, home.Tool);
            home.Tool.IsOpen = true;
        }

        // 激活：藏在别处时 `Owner` 指向它真正落回去的那张 dock，按 `Owner` 激活才真的把它顶到前面。
        (home.Tool.Owner as IDock ?? home.Home).ActiveDockable = home.Tool;
        return true;
    }

    /// <summary>
    /// 这个工具现在是不是"钉住"状态。**按 <see cref="IFactory.IsDockablePinned"/> 判**（阶段 163 实测：
    /// 按工具钉、以及用户点 dock chrome 钉按钮这两种钉法，它都为 <c>true</c>）。
    ///
    /// <para>它同时覆盖"工具在 <c>LeftPinnedDockables</c>/<c>Top…</c>/<c>Right…</c>/<c>BottomPinnedDockables</c>
    /// 里"这件事 —— 注意**测试里的 `Walk` 原本不走那四张列表**，所以当时用 `ToolsOf` 枚举出来的
    /// "钉住 = 空"是**枚举口径的假象**，不是 API 判定漏了（阶段 163 自纠）。</para>
    /// </summary>
    private bool IsPinned(IDockable layout, IDockable tool) =>
        layout is IRootDock root && IsDockablePinned(tool, root);

    /// <summary>枚举布局里能到达的全部 <see cref="IRootDock"/>（含窗口里的）。</summary>
    private static IEnumerable<IRootDock> EnumerateRoots(IDockable layout)
    {
        var seen = new HashSet<IDockable>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<IDockable>();
        stack.Push(layout);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current)) continue;

            if (current is IRootDock root)
            {
                yield return root;
                foreach (var window in root.Windows ?? Array.Empty<IDockWindow>())
                    if (window.Layout is { } l) stack.Push(l);
            }

            if (current is IDock dock)
                foreach (var child in dock.VisibleDockables ?? Array.Empty<IDockable>()) stack.Push(child);
        }
    }

    /// <summary>这个工具现在是不是躺在某张 <see cref="IRootDock"/> 的 <c>HiddenDockables</c> 里（"藏"而非"闭"）。</summary>
    private static bool IsHidden(IDockable layout, IDockable tool) =>
        EnumerateRoots(layout).Any(r => r.HiddenDockables?.Contains(tool) == true);

    /// <summary>
/// Dockable 被激活（拖拽停靠/切换标签页/浮动）时触发。
/// 延迟到 Loaded 优先级，确保 DeferredContentControl 完成 Content 创建后，
/// 由 MainWindow 统一刷新所有面板数据。
/// </summary>
    public override void OnDockableActivated(IDockable? dockable)
    {
        base.OnDockableActivated(dockable);

        Dispatcher.UIThread.Post(() =>
        {
            DockLayoutChanged?.Invoke();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// **浮动窗口：本仓不支持 ⇒ 明确拒绝**（阶段 161，闭环归档问题单 `dock-blank-after-drag`）。
    ///
    /// <para><b>为什么拒绝而不是实现</b>：本工厂继承的是**视图无关**的
    /// <c>Dock.Model.Mvvm.Factory</c> —— <c>FloatDockable</c> 会造出一个 <c>DockWindow</c>
    /// （`RootDock.Windows` 里确实多一条），但**没有任何东西给它建宿主窗口**
    /// （实测 `IDockWindow.Host` 恒为 `null`）。而"停靠区之外"是**用户真的点得到**的地方
    /// （`MainWindow.axaml` 里 `DockControl` 只占主窗口第 3 行，上面是菜单/工具栏、下面是状态栏）
    /// ⇒ 把面板标签拖到那些区域，面板就会**从界面上消失**：视图 DETACHED、没有任何窗口能显示它，
    /// 而且**回不来**（没有浮动窗口 UI、也没有"窗口"菜单，布局持久化里也不含 Windows）。
    /// 这正是归档问题单 `debug-dock-blank-after-drag` 的现场签名
    /// （`DETACHED from visual tree` 且此后再无 attached）。本仓没有实现浮动所需的那一层
    /// （宿主窗口 / 位置持久化 / 关闭语义），所以正确处理是**不让它发生**。</para>
    ///
    /// <para>⚠ 与父类实现的差别：父类会 `RemoveDockable` ＋ `AddWindow` ＋ `InitLayout`。
    /// 这里**什么都不做** ⇒ 拖拽的放置被丢弃、面板留在原处（实测：面板仍挂在可视树上、12 个面板一个不少）。
    /// 判据见 `DockDragRegressionUiTests.DraggingToolboxTabOutsideDockControl_NeverLosesPanel`。</para>
    /// </summary>
    public override void FloatDockable(IDockable dockable)
    {
        // 故意空实现：见上面的 xmldoc。**不要**改成 base.FloatDockable(dockable)。
    }
}