using System.Linq;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.CodeGen;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Models.User;
using HmiEditor.Core.Diagnostics;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;
using HmiEditor.Device.Build;
using Hmi.Hardware.Builtin;
using Hmi.Hardware.Contracts;
using Hmi.Hardware.Loader;

namespace HmiEditor.App;

public partial class MainWindow : Window
{
    private HmiProject _currentProject = new()
    {
        Name = "Untitled",
        LvglVersion = "9.5",
        HardwareTargetId = "allwinner-t113" // 型号由固件决定（firmware/t113/firmware.json）
    };

    /// <summary>硬件目标注册表（资源仪表盘取预算用；首次需要时加载一次）。</summary>
    private HardwareTargetRegistry? _hardwareTargetRegistry;

    private HmiScreen _currentScreen = new()
    {
        Name = "Screen 1",
        Width = 1024,
        Height = 600
    };

    private readonly AuthenticationService _authService = new();
    private readonly AlarmEngine _alarmEngine = new();
    private readonly RecipeManager _recipeManager = new();
    private readonly UndoRedoManager _undoRedoManager = new();
    private readonly Dictionary<HmiControl, string> _propertyEditSnapshots = new();
    private readonly Dictionary<HmiScreen, string> _screenEditSnapshots = new();
    private bool _historyReplay;
    private AssetLibrary _assetLibrary = new();
    private HmiResourceLibrary _resourceLibrary = new();
    private ResourceLibraryStore? _resourceLibraryStore;
    private TemplateManager _templateManager = null!;

    // ── I8: Git 工作流 —— 当前项目文件目录的版本控制状态 ──
    private string? _projectDirectory;

    // 编译/导出流水线（产物状态与交叉编译逻辑见 BuildPipelineService）
    private readonly BuildPipelineService _buildPipeline;

    // 设计时数据预览运行时（仿真器/绑定引擎/真实设备连接/动作执行器的所有权见 PreviewRuntimeService）
    private readonly PreviewRuntimeService _previewRuntime;

    private GitWorkflowState _gitState = GitWorkflowState.NotAvailable;

    // ── .hmi 打包：资源工作目录（打开/新建工程后建立，资产文件在此目录下 assets/ 集中管理）──
    private string? _assetDirectory;

    // ── 自动保存（定时将内存工程打包写回 .hmi；全部编辑在内存完成）──
    private string? _currentHmiPath;
    private Avalonia.Threading.DispatcherTimer? _autoSaveTimer;
    private bool _isAutoSaving;
    private bool _autoSavePending;
    private int _modalDialogCount;

    // ── I9: 分辨率预览状态 ──
    private bool _switchingPreview;

    private bool _hasUnsavedChanges;
    private double _zoomLevel = 1.0;
    private DispatcherTimer? _zoomAnimationTimer;
    private double _zoomAnimationStart;
    private double _zoomAnimationTarget;
    private DateTime _zoomAnimationStarted;

    // ── Panel Views（数据源独立原则 / Dock 三步思路之一）──
    // 所有面板 View 在 MainWindow 构造期一次性创建为字段，传给 HmiDockFactory 赋值
    // 给 Tool.Context。Dock.Avalonia 的 ControlRecycling 只回收视觉容器（ContentPresenter
    // 里的视觉树），绝不重新创建 View 实例。View 内部数据（_allItems / _allTemplates /
    // _selectedTemplate 等）保存在 View 私有字段中，不依赖 DataContext 继承链 —— 这是
    // Dock 重组后能通过 RecreateVisualTree 重建视觉树而恢复内容的前提。
    // 切勿将面板数据放在 MainWindow.DataContext 上靠继承传给 View，否则 Dock 回收视觉
    // 树时会丢失数据。
    // 所有面板属性通过 ViewLocator 动态获取当前活跃视图，
    // 确保 Dock 重建后事件处理器操作的是实际显示的视图实例，
    // 而非初始化时创建的旧实例。
    private ToolboxView ToolboxPanel => GetActiveView<ToolboxView>("Toolbox") ?? _toolboxPanel!;
    private FaceplateLibraryView FaceplatePanel => GetActiveView<FaceplateLibraryView>("Faceplate") ?? _faceplatePanel!;
    private DesignCanvasView CanvasPanel => GetActiveView<DesignCanvasView>("Canvas") ?? _canvasPanel!;
    private ProjectExplorerView ProjectExplorerPanel => GetActiveView<ProjectExplorerView>("ProjectExplorer") ?? _projectExplorerPanel!;
    private PropertyPanelView PropertyPanel => GetActiveView<PropertyPanelView>("Property") ?? _propertyPanel!;
    private LayerPanelView LayerPanel => GetActiveView<LayerPanelView>("Layers") ?? _layerPanel!;
    private AssetManagerView AssetPanel => GetActiveView<AssetManagerView>("Assets") ?? _assetPanel!;
    private NavigationFlowView NavigationFlowPanel => GetActiveView<NavigationFlowView>("Navigation") ?? _navigationFlowPanel!;

    private AuditLogView AuditPanel => GetActiveView<AuditLogView>("AuditLog") ?? _auditPanel!;
    private CrossReferenceView CrossReferencePanel => GetActiveView<CrossReferenceView>("CrossReference") ?? _crossReferenceView!;
    private AiChatPanelView AiChatPanel => GetActiveView<AiChatPanelView>("AiChat") ?? _aiChatPanel!;
    private ResourceLibraryView ResourceLibraryPanel => GetActiveView<ResourceLibraryView>("ResourceLibrary") ?? _resourceLibraryPanel!;

    // AI 预览管理器（非面板，不经过 ViewLocator）
    private AiPreviewManager AiManager { get; }

    // 后备字段 — 初始化时创建的原始实例，在 ViewLocator 尚无活跃视图时使用
    private ToolboxView? _toolboxPanel;
    private FaceplateLibraryView? _faceplatePanel;
    private DesignCanvasView? _canvasPanel;
    private ProjectExplorerView? _projectExplorerPanel;
    private PropertyPanelView? _propertyPanel;
    private LayerPanelView? _layerPanel;
    private AssetManagerView? _assetPanel;
    private NavigationFlowView? _navigationFlowPanel;
    private AuditLogView? _auditPanel;
    private CrossReferenceView? _crossReferenceView;
    private AiChatPanelView? _aiChatPanel;
    private ResourceLibraryView? _resourceLibraryPanel;

    // Agent 智能体宿主（桥接 MainWindow 与工具执行）
    private AgentHost? _agentHost;

    // ── Dock 布局（R1-T2 恢复）──
    private HmiDockFactory? _dockFactory;
    private IRootDock? _dockLayout;

    // Dock 重建后的事件防重入标志 —— 避免实例事件和 DockLayoutState 事件双触发
    private bool _suppressSelectionEvent;
    private bool _suppressPropertyEvent;
    private bool _suppressLayerEvent;
    private bool _suppressAiIconEvent;

    /// <summary>全局本地化服务实例（统一入口）。</summary>
    internal static readonly LocalizationService LocService = HmiEditor.App.Resources.AppLocalization.Service;

    /// <summary>用户界面设置服务。</summary>
    private readonly SettingsService _settingsService;

    /// <summary>
    /// 仅用于 XAML 设计器/运行时资源加载器，不要直接使用。
    /// </summary>
    public MainWindow()
    {
        _settingsService = null!;
        AiManager = null!;
        InitializeComponent();

        // 视图层异步操作的全局失败出口：属性面板/图元库等 UserControl 没有自己的
        // 状态行，失败信息回落到主窗口状态栏，保证「点了没反应」不再发生。
        UiOperationGuard.UiSink = ShowGlobalStatus;

        // 编译/导出流水线：宿主回调注入 UI/i18n/工具链依赖，
        // 产物状态由服务独占（原先散落在 _compiledOutputFiles/_compiledOutputDir/_lastSoBuildError）
        _buildPipeline = new BuildPipelineService
        {
            GetProject = () => _currentProject,
            GetAssetLibrary = () => _assetLibrary,
            GetProjectDirectory = () => _projectDirectory,
            SetStatus = msg => StatusText.Text = msg,
            PostStatus = msg => Dispatcher.UIThread.Post(() => StatusText.Text = msg),
            SetCompileBarVisible = v => CompileBarPanel.IsVisible = v,
            SetCompileBarValue = v => CompileBar.Value = v,
            SetCompileStatusText = msg => CompileStatusText.Text = msg,
            SetCompileBusy = busy =>
            {
                BtnCompile.IsEnabled = !busy;
                MenuCompile.IsEnabled = !busy;
            },
            SetDownloadEnabled = enabled =>
            {
                BtnDownloadHmi.IsEnabled = enabled;
                MenuDownload.IsEnabled = enabled;
            },
            PickExportDirectory = async () =>
            {
                var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = LocService["dialog.select.export.dir"]
                });
                return folder.Count == 1 ? folder[0].Path.LocalPath : null;
            },
            AuditExport = (message, success) =>
                _authService.RecordAudit(AuditAction.DataExported, message, success),
            GetT113SdkRoot = () => _settingsService.Device.T113SdkRoot,
            GetT113ToolchainGcc = () => _settingsService.Device.T113ToolchainGcc,
        };

        // 设计时数据预览运行时：宿主回调注入 UI/画布/模型解析依赖，
        // 仿真器与真实设备连接的生命周期由服务独占（关闭时 Dispose 统一释放）。
        _previewRuntime = new PreviewRuntimeService
        {
            GetCurrentScreen = () => _currentScreen,
            GetCurrentProject = () => _currentProject,
            SetStatus = msg => StatusText.Text = msg,
            SwitchScreen = SwitchToScreenByName,
            ExecuteScript = ExecuteScriptById,
            SetValueProvider = provider => CanvasPanel.SetPreviewValueProvider(provider),
            SetValueTypeProvider = provider => CanvasPanel.SetPreviewValueTypeProvider(provider),
            SetValueUnitProvider = provider => CanvasPanel.SetPreviewValueUnitProvider(provider),
            // 仿真值来自后台线程：仍按原语义切回 UI 线程再重绘
            InvalidateCanvas = () => Dispatcher.UIThread.Post(() => CanvasPanel.Canvas.InvalidateVisual()),
        };
    }

    public MainWindow(SettingsService settingsService) : this()
    {
        _settingsService = settingsService;
        ReportSourceWiring.RegisterAppLevelSources(_authService, _alarmEngine);   // 运行期数据源（阶段 185）

        // Create panel Views (存入后备字段，属性通过 ViewLocator 动态获取)
        _toolboxPanel = new ToolboxView();
        _faceplatePanel = new FaceplateLibraryView();
        _canvasPanel = new DesignCanvasView(_undoRedoManager, _settingsService);
        _projectExplorerPanel = new ProjectExplorerView();
        _propertyPanel = new PropertyPanelView();
        _layerPanel = new LayerPanelView();
        _assetPanel = new AssetManagerView();
        _navigationFlowPanel = new NavigationFlowView();
        _auditPanel = new AuditLogView();
        _crossReferenceView = new CrossReferenceView();
        AiManager = new AiPreviewManager();
        _agentHost = CreateAgentHost();
        _aiChatPanel = new AiChatPanelView(AiManager);
        _resourceLibraryPanel = new ResourceLibraryView();

        // I3: 审计日志 —— 持久化到应用数据目录，启动时恢复历史
        try
        {
            const string auditFileName = "audit.log.jsonl";

            var auditPath = AppPaths.DataFile(auditFileName);
            _authService.AuditLogFilePath = auditPath; AlarmAuditWiring.Attach(_alarmEngine, _authService);  // 搁置审计（阶段 278）
            _authService.LoadAuditLogFromFile(auditPath);

            // 升级兼容：早期版本写在 %APPDATA%/HmiEditor/audit.log.jsonl。
            // 只**读**历史位置 —— LoadAuditLogFromFile 按 Id 去重，重复加载安全；
            // 写入始终走新位置，旧文件冻结。这样没有"搬移途中崩溃"的丢数据窗口。
            var legacyAuditPath = AppPaths.LegacyDataFile(auditFileName);
            if (File.Exists(legacyAuditPath))
                _authService.LoadAuditLogFromFile(legacyAuditPath);
        }
        catch
        {
            // 审计目录不可用时仅保留内存日志，不阻断启动
        }
        AuditPanel.Attach(_authService);

        // 初始化图库存储并加载自定义资源（读失败 ⇒ 本次会话只读；见 MainWindow.ResourceLibrary.cs）
        LoadResourceLibrary();

        InitializeDocking();
        InitializeLocalization();
        InitializeTemplateManager();
        InitializeProject();
        ApplyUserSettings();
        InitializeAutoSave();
        WireEvents();
    }

    /// <summary>
    /// 启动时应用用户界面设置（缩放、状态栏可见性等）。
    /// 面板数据在 InitializeProject 之后注入，缩放依赖画布已就绪。
    /// </summary>
    private void ApplyUserSettings()
    {
        try
        {
            // 状态栏可见性
            StatusBarPanel.IsVisible = _settingsService.Layout.ShowStatusBar;

            // 恢复上次缩放级别
            var zoom = _settingsService.Display.ZoomLevel;
            if (zoom is >= 0.25 and <= 4.0)
                SetZoom(zoom);
        }
        catch
        {
            // 设置应用失败不影响启动
        }
    }

    /// <summary>
    /// 初始化 Dock 布局（模型层）：创建 Factory、构建 RootDock、调用 InitLayout、应用保存的布局状态。
    /// Factory/Layout 在 OnOpened 中再绑定到 DockControl（控件附加到可视化树后绑定，
    /// 避免构造期 DockControl 尚未附加导致模板未应用——这是此前 DockControl 渲染为空的原因之一）。
    /// 模型阶段失败时记日志，由 OnOpened 阶段决定是否回退到 ContentControl 固定布局。
    /// </summary>
    private void InitializeDocking()
    {
        var log = new List<string> { $"[Dock] Start {DateTime.Now:O}" };
        try
        {
            _dockFactory = new HmiDockFactory(
                ToolboxPanel, FaceplatePanel, CanvasPanel,
                ProjectExplorerPanel, PropertyPanel, LayerPanel,
                AssetPanel, NavigationFlowPanel, AuditPanel, CrossReferencePanel,
                AiChatPanel, ResourceLibraryPanel, _undoRedoManager);

            // R8-FIX: Dock 布局重组后统一重新加载所有面板数据，
            // 解决 ControlRecycling 回收视觉容器后任意面板内容空白的问题。
            _dockFactory.DockLayoutChanged += OnDockLayoutChanged;

            _dockLayout = _dockFactory.CreateLayout();
            log.Add($"[Dock] CreateLayout: id={_dockLayout.Id}, " +
                    $"dockables={_dockLayout.VisibleDockables?.Count ?? 0}");

            _dockFactory.InitLayout(_dockLayout);
            log.Add("[Dock] InitLayout OK");

            var savedState = LayoutSerializer.Load();
            _dockFactory.RestoreLayoutState(_dockLayout, savedState);   // 含"只打开保存的面板"（阶段 165）
            log.Add($"[Dock] ApplyState OK (saved={savedState != null})");

            // 关键时序：在构造期立即绑定 Factory + Layout 到 DockControl。
            // DockControl.OnApplyTemplate 会在模板应用时调用
            // DockControlFactoryService.InitializeControlRecycling，该方法读取
            // control.Layout.Factory 与 ControlRecycling 附加属性；若此刻 Layout
            // 尚未设置，方法会因 factory==null 提前返回，ControlRecycling 不会注册，
            // dockable 内容区（Context）将永不渲染。官方示例通过 XAML 绑定在模板
            // 应用前完成绑定，此处等价于构造期赋值。
            DockControl.Factory = _dockFactory;
            DockControl.Layout = _dockLayout;
            log.Add("[Dock] Factory + Layout bound to DockControl (constructor phase)");
        }
        catch (Exception ex)
        {
            log.Add($"[Dock] FATAL (model phase): {ex}");
            _dockFactory = null;
            _dockLayout = null;
        }
        finally
        {
            WriteDiag("dock-init.log", log);
        }
    }

    /// <summary>
    /// 窗口打开后无需额外操作：Factory + Layout 已在构造期绑定到 DockControl，
    /// DockControl 模板应用时会自动注册 ControlRecycling 并渲染所有 dockable。
    /// 注意：不要在此处延迟校验渲染结果或切换回退布局——Dock 的
    /// DeferredContentPresenter 会惰性创建视觉内容，1s 后视觉树可能尚未构建完毕，
    /// 误判会导致共享 View 实例被同时挂到两棵 visual tree（崩溃 / 面板空白）。
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 主窗口几何持久化：恢复上次位置/大小/最大化状态（2026-08-18 新增）
        RestoreMainWindowGeometry();

        // 修复 CommandBar 中 PART_PrimaryCommands 右对齐导致的左侧空白问题
        FixCommandBarAlignment();

        // [dock-blank-after-drag] 启动兜底：DockControl 的 DeferredContentPresenter 在模板
        // 首次应用时惰性创建各面板视觉内容，此时 ViewLocator.Build 才会真正把视图挂入视觉树。
        // 若在构造期绑定 Factory/Layout 的时序与模板应用发生竞争，面板可能停留在空白态。
        // 因此在窗口打开后延迟到 Background 优先级再注入一次面板数据，确保工具箱/画布/属性
        // 面板在首次渲染后内容非空。
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                RebuildAllPanels();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] OnOpened RebuildAllPanels error: {ex.Message}");
            }
        }, DispatcherPriority.Background);
    }
    
    /// <summary>
    /// 主窗口几何持久化：恢复上次保存的位置/大小/最大化状态。
    /// 主窗口非 ChromeWindow（普通 Window），单独处理（ChromeWindow 子窗口已由基类统一处理）。
    /// </summary>
    private void RestoreMainWindowGeometry()
    {
        try
        {
            var geo = WindowGeometryService.Load(GetType().FullName ?? "MainWindow");
            if (geo == null) return;

            if (geo.X != 0 || geo.Y != 0)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new Avalonia.PixelPoint((int)geo.X, (int)geo.Y);
            }
            if (geo.Width > 0) Width = geo.Width;
            if (geo.Height > 0) Height = geo.Height;
            if (Enum.TryParse<WindowState>(geo.WindowState, out var state))
                WindowState = state;
        }
        catch { /* 恢复失败使用默认布局 */ }
    }

    /// <summary>保存主窗口几何到用户配置目录。</summary>
    private void PersistMainWindowGeometry()
    {
        try
        {
            WindowGeometryService.Save(GetType().FullName ?? "MainWindow", new WindowGeometry
            {
                X = Position.X,
                Y = Position.Y,
                Width = Width,
                Height = Height,
                WindowState = WindowState.ToString(),
            });
        }
        catch { /* 非关键功能 */ }
    }

    /// <summary>
    /// 修复 CommandBar 中主要命令区的对齐方式。
    /// Fluent 主题默认将 PART_PrimaryCommands 设置为右对齐，
    /// 这会导致按钮全部靠右排列，左侧出现大片空白。
    /// 此方法在模板应用后查找该元素并将其改为左对齐。
    /// </summary>
    private void FixCommandBarAlignment()
    {
        // 延迟到 Loaded 事件后执行，确保模板已应用
        Dispatcher.UIThread.Post(() =>
        {
            // 查找 CommandBar 控件
            var commandBar = FindVisualChild<CommandBar>(this);
            
            if (commandBar != null)
            {
                commandBar.ApplyTemplate();
                // 在模板子元素中查找 PART_PrimaryCommands
                var primaryCommands = FindVisualChildByName(commandBar, "PART_PrimaryCommands") as ItemsControl;
                if (primaryCommands != null)
                {
                    primaryCommands.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                }
            }
        }, DispatcherPriority.Loaded);
    }
    
    /// <summary>
    /// 在可视化树中按名称查找子控件。
    /// </summary>
    private static Visual? FindVisualChildByName(Visual parent, string name)
    {
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is Control control && control.Name == name)
            {
                return control;
            }

            var result = FindVisualChildByName(child, name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// 显示用户界面设置对话框。
    /// </summary>
    private void ShowSettingsDialog()
    {
        var dialog = new SettingsDialog(_settingsService, App.ThemeService);
        dialog.ShowDialog(this);
    }
    
    /// <summary>
    /// 递归查找可视化树中指定类型的子控件。
    /// </summary>
    private static T? FindVisualChild<T>(Visual parent) where T : Visual
    {
        if (parent == null) return null;
        
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is T typedChild)
            {
                return typedChild;
            }
            
            var result = FindVisualChild<T>(child);
            if (result != null)
            {
                return result;
            }
        }
        
        return null;
    }

    /// <summary>
    /// R8-FIX: Dock 布局重组（拖动/浮动/停靠切换）后统一重新加载所有面板数据。
    /// 双阶段：Phase 1 (Loaded) 布局完成后立即重建；
    /// Phase 2 (Background) 再重建一次（覆盖 ControlRecycling 的延迟回收）。
    ///
    /// 🩸 登记 #61u（阶段 98）：失败处理**必须在回调里** —— 改动前这个 try 里只有一次赋值
    /// ＋ 一次 Post 入队，而 Post 是不抛的 ⇒ 那条 catch **永不触发**；RebuildAllPanels 的
    /// 真异常发生在回调里、无人接 ⇒ 面板重建失败时状态栏**永久停在「正在重载面板...」**。
    /// </summary>
    private void OnDockLayoutChanged()
    {
        StatusText.Text = LocService["status.reloading.panels"];
        PostPanelRebuild(rebuildAgain: true);
    }

    /// <summary>
    /// 把一次（或两次）面板重建排进 UI 线程队列。
    /// **重建与它的失败处理都在回调内** —— 理由见 <see cref="OnDockLayoutChanged"/>。
    /// </summary>
    private void PostPanelRebuild(bool rebuildAgain) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                if (!TryRebuildPanels(RebuildAllPanels, t => StatusText.Text = t)) return;
                if (rebuildAgain) PostPanelRebuild(rebuildAgain: false);
                else StatusText.Text = LocService["status.panels.reloaded"];
            },
            rebuildAgain ? DispatcherPriority.Loaded : DispatcherPriority.Background);

    /// <summary>
    /// 重组后重新注入所有面板数据。
    /// 使用 GetActiveView 获取实际显示的视图（而非 MainWindow 字段引用的旧实例）。
    /// </summary>
    /// <summary>
    /// 更新 View 菜单中主题选项的选中状态，使界面与 <see cref="App.ThemeService"/> 保持一致。
    /// </summary>
    private void UpdateThemeCheckmarks()
    {
        MenuThemeDark.IsChecked = App.ThemeService.Current == AppThemeService.HmiTheme.Dark;
        MenuThemeOcean.IsChecked = App.ThemeService.Current == AppThemeService.HmiTheme.Ocean;
        MenuThemeLight.IsChecked = App.ThemeService.Current == AppThemeService.HmiTheme.Light;
    }

    private void RebuildAllPanels()
    {
        try
        {
            // Faceplate 模板库（工具箱已不再承载模板库，模板由 Faceplate 面板统一管理）
            if (GetActiveView<FaceplateLibraryView>("Faceplate") is { } faceplate)
                faceplate.LoadTemplates(_templateManager.Templates);

            // 画布
            if (GetActiveView<DesignCanvasView>("Canvas") is { } canvas)
                canvas.RefreshForDock();

            // 项目浏览器
            if (GetActiveView<ProjectExplorerView>("ProjectExplorer") is { } explorer && _currentScreen != null)
                explorer.Refresh(_currentScreen);

            // 层管理
            if (GetActiveView<LayerPanelView>("Layers") is { } layers)
                layers.Refresh();

            // 属性
            var selected = CanvasPanel.Canvas.GetSelectedControls();
            if (GetActiveView<PropertyPanelView>("Property") is { } prop)
            {
                prop.CurrentScreen = _currentScreen;
                if (_currentProject != null)
                {
                    prop.SetProject(_currentProject);
                    prop.SetVariableManager(_currentProject.VariableManager);
                }
                prop.BindControl(selected.Count > 0 ? selected[0] : null);
            }

            // 资源
            if (GetActiveView<AssetManagerView>("Assets") is { } assets && _assetLibrary != null)
            {
                assets.AssetDirectory = _assetDirectory;
                assets.LoadAssets(_assetLibrary);
            }

            // HMI 图库
            if (GetActiveView<ResourceLibraryView>("ResourceLibrary") is { } resLib && _resourceLibrary != null)
                resLib.LoadLibrary(_resourceLibrary);

            // 导航
            if (GetActiveView<NavigationFlowView>("Navigation") is { } nav && _currentProject != null)
                nav.LoadProject(_currentProject);

            // 交叉引用
            if (GetActiveView<CrossReferenceView>("CrossReference") is { } cross && _currentProject != null)
                cross.LoadProject(_currentProject);

            // AI 助手（Dock 重建后新实例需重新注入项目与 Agent 宿主）
            if (GetActiveView<AiChatPanelView>("AiChat") is { } aiChat)
            {
                aiChat.CurrentProject = _currentProject;
                aiChat.AgentHost = _agentHost;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] RebuildAllPanels error: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取指定面板的活跃视图实例（Dock 重建后通过 ViewLocator 获取实际显示的视图，而非字段中的旧实例）。
    /// </summary>
    private T? GetActiveView<T>(string id) where T : Control
        => ViewLocator.GetActiveView(id) as T;

    /// <summary>画布中心点坐标（画布未布局时回退默认 400/240）。</summary>
    private static (double X, double Y) CanvasCenter(DesignCanvas canvas)
        => (canvas.Bounds.Width > 0 ? canvas.Bounds.Width / 2 : 400,
            canvas.Bounds.Height > 0 ? canvas.Bounds.Height / 2 : 240);

    /// <summary>
    /// 将图库资源放置到画布中心（双击图库面板时）。
    /// </summary>
    private void PlaceResourceAtCanvasCenter(HmiResourceItem resource)
    {
        var canvas = CanvasPanel.Canvas;
        if (canvas.Screen == null) return;
        var (centerX, centerY) = CanvasCenter(canvas);
        PlaceResourceOnCanvas(canvas.Screen, resource, centerX, centerY);
    }

    /// <summary>
    /// 将图库资源放置到画布 —— 根据资源类型创建对应控件。
    /// </summary>
    private void PlaceResourceOnCanvas(HmiScreen screen, HmiResourceItem resource, double x, double y)
    {
        // 状态指示 / 过程图形 → 作为控件放置（Led 或默认类型）
        var type = resource.Type switch
        {
            HmiEditor.Core.Models.ResourceItemType.StatusIndicator => HmiControlType.Led,
            _ => resource.DefaultControlType ?? HmiControlType.Led,
        };

        var control = new HmiControl
        {
            Type = type,
            Name = resource.DisplayName,
            X = x,
            Y = y,
            Width = resource.DefaultWidth,
            Height = resource.DefaultHeight,
            Text = resource.DisplayName,
            TextColor = resource.PrimaryColor ?? "#22c55e",
            BgColor = resource.SecondaryColor ?? "#1e293b",
        };

        var command = new AddControlCommand(screen.Controls, control);
        _undoRedoManager.Execute(command);

        CanvasPanel.Canvas.ClearSelection();
        CanvasPanel.Refresh();
        ProjectExplorerPanel.Refresh(screen);
        LayerPanel.Refresh();
        SetUnsavedChanges(true);
        UpdateStatusBar();
    }

    /// <summary>
    /// 打开 AI 图标生成对话框。
    /// 有项目时保存到项目资源目录；无项目时使用临时目录兜底，
    /// 生成结果始终加入资产库与资源库。
    /// </summary>
    private async Task OpenAiIconDialogAsync()
    {
        // 防重入。原注释写的是"实例事件与 DockLayoutState 聚合事件双路触发时只弹一次"——
        // `AssetPanel.AiIconRequested` 的实例直订已随 #46 删除，现在**只剩聚合事件一条路**。
        // 标志本身保留（最小改动，且它现在防的是事件链上的嵌套触发）；若要连同删除，
        // 需先证明没有"对话框结果回填 → 再 raise"的路径。
        if (_suppressAiIconEvent) return;
        _suppressAiIconEvent = true;
        try
        {
            // 有项目时保存到项目资源目录；无项目时落到**持久**产物目录（原为系统临时目录，
            // 会被系统清理 ⇒ 用户"保存"的图标会消失）
            var iconsDir = !string.IsNullOrEmpty(_projectDirectory)
                ? Path.Combine(_projectDirectory, "assets", "icons")
                : AppPaths.AssetDir("ai-icons");

            var iconDialog = new Views.AiIconDialog(AiManager) { AssetDirectory = iconsDir };
            var results = await iconDialog.ShowDialog<List<AssetItem>?>(this);
            if (results is not { Count: > 0 }) return;

            foreach (var item in results)
            {
                _assetLibrary.AddAsset(item);
                AddAiIconToResourceLibrary(item);
            }
            AssetPanel.LoadAssets(_assetLibrary);
            ResourceLibraryPanel.LoadLibrary(_resourceLibrary);
            StatusText.Text = LocService.Format("status.icons.generated", results.Count);
            SetUnsavedChanges(true);
        }
        finally
        {
            _suppressAiIconEvent = false;
        }
    }

    /// <summary>
    /// 将 AI 生成的 SVG 图标同步加入 HMI 图库（资源库），
    /// 使生成的图标可直接从图库面板拖放/双击使用。
    /// </summary>
    private void AddAiIconToResourceLibrary(AssetItem asset)
    {
        if (asset.Type != AssetType.Svg) return;
        if (_resourceLibrary.CustomResources.Any(r => string.Equals(r.Name, asset.Name, StringComparison.Ordinal)))
            return;

        string? svgText = null;
        try
        {
            if (!string.IsNullOrEmpty(asset.FilePath) && System.IO.File.Exists(asset.FilePath))
                svgText = System.IO.File.ReadAllText(asset.FilePath);
        }
        catch { /* 读取失败仅跳过 SVG 内联内容，不影响资源条目 */ }

        _resourceLibrary.CustomResources.Add(new HmiResourceItem
        {
            Id = asset.Id,
            Name = asset.Name,
            DisplayName = asset.Name,
            Type = ResourceItemType.SvgIcon,
            Scenario = ApplicationScenario.Industrial,
            SubCategory = LocService[Lang.ResourceSubCategoryAi],
            Description = LocService[Lang.ResourceDescAi],
            // 图标默认 48×48；AI 图片按实际生成尺寸入库
            DefaultWidth = asset.Width > 0 ? asset.Width : 48,
            DefaultHeight = asset.Height > 0 ? asset.Height : 48,
            SvgGeometry = svgText,
            // 预览图标：Sparkles 形状（AI 生成资源统一标记）
            IconPathData = "M12 3l1.9 5.7L19.5 10.5l-5.6 1.8L12 18l-1.9-5.7L4.5 10.5l5.6-1.8L12 3zM19 15l.9 2.6 2.6.9-2.6.9L19 22l-.9-2.6-2.6-.9 2.6-.9L19 15zM5 15l.9 2.6 2.6.9-2.6.9L5 22l-.9-2.6-2.6-.9 2.6-.9L5 15z",
            PrimaryColor = "#8b5cf6",
            SecondaryColor = "#6d28d9",
            IsCustom = true,
        });

        // 保存资源库变更（用户自定义资源持久化）
        PersistResourceLibrary();
    }

    private static void WriteDiag(string fileName, IEnumerable<string> lines)
    {
        try
        {
            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, fileName), lines);
        }
        catch
        {
            // 诊断写入失败不影响主流程
        }
    }

    /// <summary>
    /// 初始化模板管理器 —— 绑定到当前项目的 Templates 集合。
    /// 保证 TemplateManager 操作直接作用于 _currentProject.Templates，
    /// 确保代码生成和序列化读取到同一份数据。
    /// </summary>
    private void InitializeTemplateManager()
    {
        _templateManager = new TemplateManager(_currentProject.Templates);

        // 注入预置模板（去重：仅添加项目中尚不存在的模板）
        var existingIds = new HashSet<string>(_currentProject.Templates.Select(t => t.Id));
        var existingNames = new HashSet<string>(
            _currentProject.Templates.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var tpl in TemplateManager.CreateDefaultTemplates())
        {
            if (!existingIds.Contains(tpl.Id) && !existingNames.Contains(tpl.Name))
            {
                _currentProject.Templates.Add(tpl);
            }
        }
    }

    /// <summary>
    /// 切换项目时重新绑定模板管理器。
    /// </summary>
    private void RebindTemplateManager()
    {
        _templateManager = new TemplateManager(_currentProject.Templates);

        // R-FIX: 新 TemplateManager 需要重新绑定 TemplatesChanged 事件，
        // 否则 NewProject/OpenProject 后添加/删除模板不会刷新 ToolboxPanel/FaceplatePanel。
        _templateManager.TemplatesChanged += (_, _) =>
        {
                FaceplatePanel.LoadTemplates(_templateManager.Templates);
        };

        // 同步共享模板状态：Faceplate 视图由 ViewLocator 工厂延迟创建，
        // 附加时从 DockLayoutState.Templates 自初始化（见 FaceplateLibraryView.OnAttachedToVisualTree）。
        DockLayoutState.Templates = _templateManager.Templates;

        PropertyPanel.SetTemplateManager(_templateManager);
        FaceplatePanel.LoadTemplates(_templateManager.Templates);
    }

    private void InitializeLocalization()
    {
        // 默认资源已通过 Core 程序集的 EmbeddedResource 加载
        LocService.InitializeDefaults();

        // 语言切换时刷新代码里**直接取值**的文本（状态栏、窗口标题、菜单勾选）。
        // XAML 里的 {res:Loc key} 绑定自 2026-09-17 起会经 LocalizedString 的 INPC 自动刷新，
        // 不再需要为了绑定而重建面板；保留下面的重建是为了覆盖"代码里读 LocService[...] 再赋值"的部分。
        LocService.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// 本地化修复：语言切换后刷新**非绑定**的 UI 文本（窗口标题、菜单/主题勾选、状态栏）。
    ///
    /// <para><b>与 XAML 绑定的分工：</b>XAML 用 <c>{res:Loc key}</c>，会经
    /// <see cref="HmiEditor.App.Resources.LocalizedString"/> 的属性通知**自动**刷新
    /// （此前用的是索引器绑定 <c>{Binding [key], Source=…}</c>，那种写法在 Avalonia 里
    /// 不可观察 —— 菜单栏切语言后一直是旧语言，已全量迁移）。</para>
    ///
    /// <para>剩下的这部分是**代码里读资源再赋值**的文本（<c>UpdateStatusBar</c>、窗口标题、
    /// 勾选状态）以及面板视图里同样由代码赋值的文案 —— 它们不经过绑定，只能显式刷新，
    /// 因此仍保留"重建面板 + 刷新状态栏"。</para>
    /// </summary>
    private void OnLanguageChanged(object? sender, string language)
    {
        try
        {
            // 刷新菜单选中状态
            MenuLangZhCN.IsChecked = language == "zh-CN";
            MenuLangEnUS.IsChecked = language == "en-US";

            // 刷新主题选中状态
            UpdateThemeCheckmarks();

            // 刷新窗口标题
            if (_currentProject != null)
            {
                Title = _hasUnsavedChanges
                    ? $"{LocService["app.title"]} — {_currentProject.Name} *"
                    : $"{LocService["app.title"]} — {_currentProject.Name}";
            }

            // 重建所有面板（与 Dock 布局变更使用相同的刷新机制）
            Dispatcher.UIThread.Post(() =>
            {
                RebuildAllPanels();
                UpdateStatusBar();
            }, DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] OnLanguageChanged error: {ex.Message}");
        }
    }

    private void InitializeProject()
    {
        // 初始化内置 HMI 图库资源（仅一次，跨项目共享）
        if (_resourceLibrary.BuiltInResources.Count == 0)
        {
            foreach (var r in HmiEditor.Core.Services.PresetResourceLibrary.CreateBuiltIn())
                _resourceLibrary.BuiltInResources.Add(r);
        }

        _currentProject.Screens.Add(_currentScreen);
        ProjectExplorerPanel.LoadProject(_currentProject, _currentScreen);
        LoadProjectIntoPanels();
        UpdateDockLayoutState();
        UpdateStatusBar();

        Title = LocService["app.title"] + $" — {_currentProject.Name}";
    }

    /// <summary>
    /// 将当前项目/屏幕注入所有面板。
    /// NewProject / OpenProject / SwitchToScreen 等路径共用此序列，避免初始化顺序漂移。
    /// </summary>
    private void LoadProjectIntoPanels(bool syncAiChat = true)
    {
        CanvasPanel.SetScreen(_currentScreen, _currentProject);
        ProjectExplorerPanel.Refresh(_currentScreen);
        LayerPanel.LoadScreen(_currentScreen);
        PropertyPanel.SetVariableManager(_currentProject.VariableManager);
        PropertyPanel.SetProject(_currentProject);
        PropertyPanel.CurrentScreen = _currentScreen;
        PropertyPanel.BindControl(null);
        PropertyPanel.ShowAiSuggestions(_currentScreen);
        CanvasPanel.ClearSelection();
        AssetPanel.AssetDirectory = _assetDirectory;
        AssetPanel.LoadAssets(_assetLibrary);
        NavigationFlowPanel.LoadProject(_currentProject);
        CrossReferencePanel.LoadProject(_currentProject);
        if (syncAiChat) AiChatPanel.CurrentProject = _currentProject;
    }

    /// <summary>
    /// 集中订阅主窗口全部事件。
    ///
    /// 原为 992 行单方法，2026-09-13 按域拆到 Wiring.*.cs 的 partial 方法
    /// （纯搬移，行为不变），此处只保留调用清单，便于快速定位某类事件在哪。
    /// </summary>
    private void WireEvents()
    {
        WireCanvasAndSelectionEvents();
        WireCommandAndMenuEvents();
        WirePanelAndDialogEvents();
        WireFaceplateAndAiEvents();
        WireDockAggregateEvents();
    }

    // ════════════════════════════════════════════════════════════════════
    // Dock 事件路由诊断
    // 模拟 Dock 重建场景，验证所有面板事件订阅在重建后是否仍然有效。
    // 诊断结果输出到状态栏 + 弹窗报告。
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 调试菜单入口 —— Dock 事件路由诊断。
    /// 实现见 <see cref="Services.DockEventRoutingDiagnostics"/>（诊断代码不留在主窗口内）。
    /// </summary>
    private async void RunDockEventRoutingDiagnostic()
    {
        try
        {
            await Services.DockEventRoutingDiagnostics.RunAsync(
                this, msg => StatusText.Text = msg, OnDockLayoutChanged);
        }
        catch (Exception ex) { ReportOperationFailure(Lang.UiOpDockDiagnostics, ex); }
    }

    // ── 文件操作 ──

    /// <summary>
    /// 启动入口：从引导页面选择"新建工程"后调用。
    /// 在窗口 Show() 之后由 App 调用，触发新建工程流程。
    /// </summary>
    public void StartWithNewProject()
    {
        NewProject();
    }

    /// <summary>
    /// 启动入口：从引导页面选择"打开工程"后调用。
    /// 在窗口 Show() 之后由 App 调用，触发文件选择对话框。
    /// </summary>
    public void StartWithOpenProject()
    {
        OpenProject();
    }

    /// <summary>
    /// 启动入口：从引导页面选择"最近工程"后调用。
    /// 直接打开指定路径的 .hmi 文件，无需弹出文件选择对话框。
    /// </summary>
    public async void StartWithOpenFile(string filePath)
    {
        try { await StartWithOpenFileCore(filePath); }
        catch (Exception ex) { ReportOperationFailure(Lang.UiOpOpenProject, ex, filePath); }
    }

    private async Task StartWithOpenFileCore(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            StatusText.Text = LocService.Format(Lang.StatusFileNotFound, filePath);
            return;
        }

        try
        {
            await OpenHmiFile(filePath);
        }
        catch (Exception ex)
        {
            StatusText.Text = UiOperationGuard.FailureDetail(Lang.UiOpOpenProject, filePath, ex);
        }
    }

    /// <summary>
    /// 打开指定路径的 .hmi 或 .hmi.json 工程文件（共用加载逻辑）。
    /// </summary>
    private async Task OpenHmiFile(string filePath)
    {
        var isLegacyJson = filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        HmiProject project;

        if (isLegacyJson)
        {
            var json = await File.ReadAllTextAsync(filePath);
            project = ProjectSerializer.Deserialize(json)
                      ?? throw new InvalidDataException(LocService["status.open.parse.failed"]);
            _assetLibrary = new AssetLibrary();
            _assetDirectory = CreateWorkspaceDirectory(project);
        }
        else
        {
            var tempDir = CreateWorkspaceDirectory(_currentProject, create: false);   // 不提前建：包坏了不留空目录
            HmiPackageResult result;
            await using (var fs = File.OpenRead(filePath))
            {
                result = await Task.Run(() => HmiPackageService.Unpack(fs, tempDir));
            }
            project = result.Project;
            _assetLibrary = result.AssetLibrary;
            _assetDirectory = CreateWorkspaceDirectory(project);
            // 缺资产告警 + 图库合并（两件事都在 ResourceLibrary partial 里，行数守卫不涨）
            InitializePackageState(result);
        }

        _currentProject = project;
        _authService.ApplySecurityConfig(_currentProject.SecurityConfig);
        _currentHmiPath = isLegacyJson ? null : filePath;
        _projectDirectory = Path.GetDirectoryName(filePath);

        // 记录到最近工程列表
        _settingsService.RecentProjects.Record(filePath);

        RefreshGitState();
        _currentScreen = project.Screens.Count > 0 ? project.Screens[0] : ScreenFactory.Create(project);
        if (!project.Screens.Contains(_currentScreen))
            project.Screens.Add(_currentScreen);

        InitializeTemplateManager();
        RebindTemplateManager();
        ProjectExplorerPanel.LoadProject(_currentProject, _currentScreen);
        LoadProjectIntoPanels();
        UpdateDockLayoutState();
        SetUnsavedChanges(false);
        Title = LocService["app.title"] + $" — {_currentProject.Name}";
        ResetResolutionPreview();

        var dangling = CrossReferenceAnalyzer.FindDanglingReferences(_currentProject);
        if (dangling.Count > 0)
        {
            StatusText.Text = LocService.Format("status.project.loaded", dangling.Count);
        }
        else
        {
            UpdateStatusBar();
        }
    }

    private async void NewProject()
    {
        try { await NewProjectCore(); }
        catch (Exception ex) { ReportOperationFailure(Lang.UiOpNewProject, ex); }
    }

    private async Task NewProjectCore()
    {
        // 如果有未保存的更改，提示用户保存、放弃或取消
        if (_hasUnsavedChanges)
        {
            var confirm = new NewProjectConfirmDialog
            {
                Message = LocService.Format("confirm.discard.changes", _currentProject.Name)
            };
            await confirm.ShowDialog(this);
            if (!confirm.Confirmed)
            {
                StatusText.Text = LocService["status.project.cancelled"];
                return;
            }
        }

        _currentProject = new HmiProject { Name = "Untitled", LvglVersion = "9.5" };
        // 应用工程安全配置到认证服务（会话超时/密码策略/锁定策略）
        _authService.ApplySecurityConfig(_currentProject.SecurityConfig);
        _currentScreen = ScreenFactory.Create(_currentProject, name: "Screen 1");
        _currentProject.Screens.Add(_currentScreen);

        // 新工程建立资源工作目录与空白资产库
        _assetLibrary = new AssetLibrary();
        _assetDirectory = CreateWorkspaceDirectory(_currentProject);

        // 新工程与任何目录无关：清空旧目录引用，避免 git 状态误指向旧工程
        _projectDirectory = null;
        _currentHmiPath = null;   // 新工程需手动另存后才启用自动保存
        RefreshGitState();

        // 重新绑定模板管理器到新项目，并注入预置模板
        InitializeTemplateManager();
        RebindTemplateManager();

        // 重置所有面板到新项目/屏幕
        // 先重新绑定项目浏览器到新项目（切换项目必须 LoadProject，否则其内部 _project
        // 仍指向旧工程对象，导致树显示旧内容 —— 历史遗留 Bug 修复）
        ProjectExplorerPanel.LoadProject(_currentProject, _currentScreen);
        LoadProjectIntoPanels();
        SetUnsavedChanges(false);
        Title = LocService["app.title"] + " — " + LocService["project.untitled"];
        UpdateStatusBar();
        StatusText.Text = LocService["status.project.created"];
    }

    private async void OpenProject()
    {
        try { await OpenProjectCore(); }
        catch (Exception ex) { ReportOperationFailure(Lang.UiOpOpenProject, ex); }
    }

    private async Task OpenProjectCore()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocService["dialog.open.hmi.project"],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(LocService["file.type.hmi.package"])
                {
                    Patterns = new[] { "*.hmi" }
                },
                new FilePickerFileType(LocService["file.type.hmi.project"])
                {
                    Patterns = new[] { "*.hmi.json" }
                },
                new FilePickerFileType(LocService["file.type.all"])
                {
                    Patterns = new[] { "*" }
                }
            }
        });

        if (files.Count != 1) return;

        var filePath = files[0].Path.LocalPath;
        try
        {
            await OpenHmiFile(filePath);
        }
        catch (Exception ex)
        {
            StatusText.Text = UiOperationGuard.FailureDetail(Lang.UiOpOpenProject, filePath, ex);
        }
    }

    private async void SaveProject()
    {
        try { await SaveProjectCore(); }
        catch (Exception ex) { ReportOperationFailure(Lang.UiOpSaveProject, ex); }
    }

    private async Task SaveProjectCore()
    {
        // 已打开过 .hmi 或已保存过 → 直接保存回原路径，不再弹保存对话框
        if (_currentHmiPath is not null)
        {
            await SaveToPath(_currentHmiPath, Path.GetFileName(_currentHmiPath));
            return;
        }

        // 新工程 / legacy JSON（未绑定文件路径）→ 需选择保存位置
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocService["dialog.save.hmi.project"],
            DefaultExtension = ".hmi",
            ShowOverwritePrompt = true,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(LocService["file.type.hmi.package"])
                {
                    Patterns = new[] { "*.hmi" }
                }
            }
        });

        if (file == null) return;

        await SaveToPath(file.Path.LocalPath, file.Name);
    }

    /// <summary>将当前工程打包保存到指定路径（内部处理悬空引用警告、最近工程记录与状态栏提示）。</summary>
    private async Task SaveToPath(string path, string displayName)
    {
        try
        {
            // P3: 保存前检测悬空变量引用、通讯/分组引用完整性、报警分组引用（不阻止保存，仅警告）
            var dangling = CrossReferenceAnalyzer.FindDanglingReferences(_currentProject);
            var integrityIssues = _currentProject.VariableManager.ValidateIntegrity();
            var alarmGroupIssues = VariableManager.ValidateAlarmGroupRefs(_currentProject);
            // 级联修复已知问题，减少用户手动干预
            if (integrityIssues.Any())
                _currentProject.VariableManager.AutoFixIntegrityIssues();
            if (alarmGroupIssues.Any())
                VariableManager.AutoFixAlarmGroupRefs(_currentProject);

            var tempPath = path + ".tmp";
            // 内存快照（UI 线程序列化保证一致性）→ 后台打包
            var projectJson = ProjectSerializer.Serialize(_currentProject);
            await using (var fs = File.Create(tempPath))
            {
                await Task.Run(() => HmiPackageService.Pack(
                    fs,
                    projectJson,
                    _assetLibrary,
                    _resourceLibrary,
                    item => !string.IsNullOrWhiteSpace(item.FilePath) && File.Exists(item.FilePath)
                        ? item.FilePath
                        : null,
                    HmiEditor.Core.VersionInfo.FullVersion));
            }
            File.Move(tempPath, path, overwrite: true);

            _currentHmiPath = path;
            _projectDirectory = Path.GetDirectoryName(path);
            _settingsService.RecentProjects.Record(path);
            RefreshGitState();
            SetUnsavedChanges(false);
            Title = LocService["app.title"] + $" — {_currentProject.Name}";

            if (dangling.Count > 0 || integrityIssues.Count > 0 || alarmGroupIssues.Count > 0)
            {
                // 输出详细问题到状态栏和调试输出
                var warnCount = dangling.Count + integrityIssues.Count + alarmGroupIssues.Count;
                StatusText.Text = LocService.Format("status.project.saved", warnCount, displayName);
                // 走回落原文（不是 ExternalText）：调试行不随界面语言变，兄弟行也仍是中文。
                foreach (var (loc, bindingId) in dangling)
                    System.Diagnostics.Debug.WriteLine($"[DanglingRef] {loc.Message} → '{bindingId}'");
                foreach (var issue in integrityIssues)
                    System.Diagnostics.Debug.WriteLine($"[Integrity] 变量 '{issue.VariableName}' 悬空引用: {issue.IssueType} → '{issue.StaleRef}' ({issue.SuggestedFix})");
                foreach (var (alarmName, groupId) in alarmGroupIssues)
                    System.Diagnostics.Debug.WriteLine($"[AlarmGroup] 报警 '{alarmName}' 悬空 GroupId → '{groupId}'");
            }
            else
            {
                StatusText.Text = LocService["project.status.saved"] + $": {displayName}";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = UiOperationGuard.FailureDetail(Lang.UiOpSaveProject, path, ex);
        }
    }

    /// <summary>创建工程资源工作目录（资产文件统一存放于该目录 assets/ 下）。</summary>
    private static string CreateWorkspaceDirectory(HmiProject project, bool create = true)
    {
        var name = SanitizeFileName(project?.Name ?? "Untitled");
        // 每次打开工程一个独立目录：并发/多实例不会互相踩（原实现自己在名字里拼 guid，
        // 但拼法带一个易错的截断式；统一交给 AppPaths 生成唯一名）。create:false ⇒ 只给路径不建目录。
        return create ? AppPaths.NewTempSubdir(name) : AppPaths.NewTempSubdirPath(name);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "project" : name;
    }

    /// <summary>将 .hmi 包内图库合并进全局图库（按名称去重）。</summary>
    private void MergePackageResourceLibrary(HmiResourceLibrary packageLibrary)
    {
        foreach (var item in packageLibrary.CustomResources)
        {
            if (_resourceLibrary.CustomResources.All(r =>
                    !string.Equals(r.Name, item.Name, StringComparison.Ordinal)))
            {
                _resourceLibrary.CustomResources.Add(item);
            }
        }
        PersistResourceLibrary();
    }

    // ─────────────────────────── 自动保存 ───────────────────────────

    /// <summary>初始化自动保存定时器（间隔可在设置中调整，最小 5 秒）。</summary>
    private void InitializeAutoSave()
    {
        _autoSaveTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(5, _settingsService.Interaction.AutoSaveInterval))
        };
        _autoSaveTimer.Tick += (_, _) => OnAutoSaveTick();
        _autoSaveTimer.Start();
    }

    /// <summary>
    /// 自动保存到点回调（DispatcherTimer 在 UI 线程执行：若用户正在执行同步操作，
    /// 回调会自然排队、等待当前操作完成后再触发，避免打断操作）。
    /// </summary>
    private void OnAutoSaveTick()
    {
        // 间隔可配置：设置变化后下一拍自动生效
        var intervalSec = Math.Max(5, _settingsService.Interaction.AutoSaveInterval);
        if (_autoSaveTimer is { } timer && Math.Abs(timer.Interval.TotalSeconds - intervalSec) > 0.5)
        {
            timer.Interval = TimeSpan.FromSeconds(intervalSec);
        }

        if (!_settingsService.Interaction.EnableAutoSave) return;
        if (_currentHmiPath is null) return;      // 未保存过的工程不自动保存
        if (!_hasUnsavedChanges) return;          // 无变更不写盘

        if (_isAutoSaving || _modalDialogCount > 0)
        {
            // 正在保存或有模态对话框：标记待保存，待当前操作/保存完成后再执行
            _autoSavePending = true;
            return;
        }

        _ = AutoSaveAsync();
    }

    /// <summary>
    /// 执行自动保存：UI 线程对工程做序列化快照（保证与编辑状态一致），
    /// 资产收集与 ZIP 打包在后台线程完成，不阻塞界面。
    /// </summary>
    private async Task AutoSaveAsync()
    {
        if (_isAutoSaving)
        {
            _autoSavePending = true;
            return;
        }

        _isAutoSaving = true;
        try
        {
            var path = _currentHmiPath;
            if (string.IsNullOrWhiteSpace(path)) return;

            // 内存快照 → 后台打包 → 原子替换写回 .hmi
            var projectJson = ProjectSerializer.Serialize(_currentProject);
            var tempPath = path + ".autosave.tmp";
            await using (var fs = File.Create(tempPath))
            {
                await Task.Run(() => HmiPackageService.Pack(
                    fs,
                    projectJson,
                    _assetLibrary,
                    _resourceLibrary,
                    item => !string.IsNullOrWhiteSpace(item.FilePath) && File.Exists(item.FilePath)
                        ? item.FilePath
                        : null,
                    HmiEditor.Core.VersionInfo.FullVersion));
            }
            File.Move(tempPath, path, overwrite: true);

            SetUnsavedChanges(false);
            StatusText.Text = LocService.Format("status.autosaved", DateTime.Now.ToString("HH:mm:ss"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoSave] 失败: {ex.Message}");
        }
        finally
        {
            _isAutoSaving = false;
            if (_autoSavePending)
            {
                _autoSavePending = false;
                _ = AutoSaveAsync();
            }
        }
    }

    /// <summary>
    /// 以模态方式打开对话框（自动保存期间跳过，待对话框关闭后再保存，
    /// 避免打断用户正在进行的配置编辑）。
    /// </summary>
    private void ShowModalDialog(Window dialog)
    {
        _modalDialogCount++;
        dialog.Closed += (_, _) =>
        {
            _modalDialogCount = Math.Max(0, _modalDialogCount - 1);
            if (_modalDialogCount == 0 && _autoSavePending)
            {
                _autoSavePending = false;
                _ = AutoSaveAsync();
            }
        };
        dialog.ShowDialog(this);
    }

    private async void ExportCode()
    {
        try { await ExportCodeCore(); }
        catch (Exception ex) { ReportOperationFailure(Lang.UiOpExportCode, ex); }
    }

    private Task ExportCodeCore() => _buildPipeline.ExportAsync();

    // ── 编译转换 + 下载到 HMI ──

    /// <summary>
    /// 编译转换 —— 将工程编译转换为可部署的源码工程（libhmi_ui.so 源码 + 构建文件）。
    /// 展示进度与结果状态；成功后启用"下载到HMI"。
    /// </summary>
    private async void CompileProject()
    {
        try { await CompileProjectCore(); }
        catch (Exception ex) { ReportOperationFailure(Lang.UiOpCompileProject, ex); }
    }

    private Task CompileProjectCore() => _buildPipeline.CompileAsync();

    /// <summary>
    /// 下载到 HMI —— 打开下载对话框，将编译产物传输到指定 HMI 设备。
    /// 仅在编译转换成功后可用。
    /// </summary>
    private async void DownloadToHmi()
    {
        try { await DownloadToHmiCore(); }
        catch (Exception ex) { ReportOperationFailure(Lang.UiOpDownloadHmi, ex); }
    }

    private async Task DownloadToHmiCore()
    {
        if (_currentProject == null) return;
        if (_buildPipeline.CompiledOutputFiles.Count == 0)
        {
            StatusText.Text = LocService["download.need.compile"];
            return;
        }

        // 确保存在可部署的 libhmi_ui.so 产物：SharedLibrary 模式直接复用编译产物，
        // 否则以 SharedLibrary 变体重建并交叉编译（固件 .so 架构强制逻辑见 BuildPipelineService）。
        if (!await _buildPipeline.EnsureSharedLibraryArtifactsAsync()) return;

        // 部署资源清单：仅部署被控件引用的资源（生成代码 IMG_PATH 引用其文件名，
        // 设备端路径固定 /mnt/UDISK/hmi/assets/<文件名>，与 SIM_ASSET_DIR 契约一致）
        var deployAssets = new List<HmiEditor.Device.Updates.HmiDeployAsset>();
        foreach (var asset in _assetLibrary.Assets)
        {
            if (asset.ReferenceCount <= 0) continue;
            if (string.IsNullOrWhiteSpace(asset.FilePath)) continue;
            var normalized = asset.FilePath.Replace('\\', '/');
            var localPath = Path.IsPathRooted(asset.FilePath)
                ? asset.FilePath
                : System.IO.Path.Combine(_projectDirectory ?? "", asset.FilePath);
            if (!System.IO.File.Exists(localPath)) continue;
            deployAssets.Add(new HmiEditor.Device.Updates.HmiDeployAsset(
                localPath, System.IO.Path.GetFileName(normalized)));
        }

        // 运行时连接配置（comm_config.ini）：IDE 通讯设置随下载下发，宿主启动读取，
        // 改连接参数（TCP host/端口、RTU 串口/波特率等）免重编固件。
        var runtimeConfigText = HmiEditor.Core.Services.CommConfigCodeGen
            .GenerateRuntimeConfigText(_currentProject.VariableManager.Connections);

        var dialog = new Views.DownloadToHmiView(
            _buildPipeline.CompiledOutputFiles, _buildPipeline.CompiledOutputDir,
            deployAssets, runtimeConfigText);
        await dialog.ShowDialog(this);
    }

    // ── I11: 生成代码验证 + 模拟器工程导出 ──

    /// <summary>验证生成代码 —— 运行静态质量门并展示问题清单。</summary>
    private void ValidateGeneratedCode()
    {
        if (_currentProject == null)
        {
            StatusText.Text = LocService["gen.create.or.open.project"];
            return;
        }

        try
        {
            var generator = new CodeGenerator();
            var code = generator.GenerateFullProject(_currentProject, _assetLibrary);
            var findings = HmiEditor.Core.Services.GeneratedCodeValidator.Validate(_currentProject, code);

            var errors = findings.Count(f => f.Severity == HmiEditor.Core.Services.FindingSeverity.Error);
            var warnings = findings.Count(f => f.Severity == HmiEditor.Core.Services.FindingSeverity.Warning);

            var dialog = new Views.ValidationResultView(findings);
            dialog.ShowDialog(this);

            StatusText.Text = LocService.Format("status.verify.completed", errors, warnings);
            _authService.RecordAudit(AuditAction.DataExported,
                LocService.Format("status.verify.generated", errors, warnings), errors == 0);
        }
        // ⚠ 这两条**刻意保留 ex.Message**（登记 #61a 尾巴，阶段 101 —— 与 #61t / #61u 同款
        //   「诚实边界」）：本 try 体内**没有产出方** —— GenerateFullProject 与
        //   GeneratedCodeValidator.Validate 全程零 IO、零用户可触发 throw（实测 CodeGen
        //   只有两个 throw，都是程序员错误档）⇒ 换出口只会落回落分支、用户所见一字不变。
        catch (Exception ex)
        {
            StatusText.Text = LocService.Format("status.verify.failed", ex.Message);
            _authService.RecordAudit(AuditAction.DataExported, LocService.Format("audit.verify.code.failed", ex.Message), false);
        }
    }

    /// <summary>
    /// 编译并运行 —— 一键将工程编译为 LVGL+SDL2 PC 模拟器并自动运行。
    /// 打开 BuildRunDialog，由其内部完成 生成 → 编译 → 运行监控 的完整衔接。
    /// </summary>
    private void BuildAndRunSimulator()
    {
        if (_currentProject == null)
        {
            StatusText.Text = LocService["gen.create.or.open.project"];
            return;
        }

        // 1. 确定模拟器工作目录（可配置，默认可清理临时根，支持增量编译复用）
        var workDir = _settingsService.Simulator.WorkDir;
        if (string.IsNullOrWhiteSpace(workDir))
        {
            var safeName = string.Concat(_currentProject.Name.Split(Path.GetInvalidFileNameChars()));
            workDir = AppPaths.TempSubdir("sim", safeName);
        }

        // 2. 探测 LVGL 源码目录（用户配置 → 环境变量 → 工作目录上级 → 常用路径）
        var lvglDir = SimulatorBuildService.FindLvglDirectory(_settingsService.Simulator.LvglDir, workDir);
        if (string.IsNullOrEmpty(lvglDir))
        {
            StatusText.Text = LocService["buildrun.no.lvgl"];
            return;
        }

        // 3. 打开编译并运行对话框（Load 后自动开始编译）
        var dialog = new Views.BuildRunDialog(_currentProject, workDir, lvglDir, _assetLibrary);
        dialog.ShowDialog(this);
    }

    private async void ExportSimulatorProject()
    {
        try { await ExportSimulatorProjectCore(); }
        catch (Exception ex) { ReportOperationFailure(Lang.UiOpExportSimulator, ex); }
    }

    private async Task ExportSimulatorProjectCore()
    {
        if (_currentProject == null)
        {
            StatusText.Text = LocService["gen.create.or.open.project"];
            return;
        }

        var folder = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = LocService["dialog.select.simulator.dir"]
        });
        if (folder.Count != 1) return;
        var outputPath = System.IO.Path.Combine(folder[0].Path.LocalPath, "hmi_simulator");   // try 外承接（登记 #61h 第二刀）
        try
        {
            var generator = new CodeGenerator();
            // 模拟器工程使用不含 main() 的 HMI 模块代码（main.c 提供 LVGL+SDL2 入口）
            var code = generator.GenerateSimulatorModule(_currentProject, _assetLibrary);

            // 导出前自动验证,Error 级问题阻断
            var findings = HmiEditor.Core.Services.GeneratedCodeValidator.Validate(_currentProject, code);
            if (HmiEditor.Core.Services.GeneratedCodeValidator.HasErrors(findings))
            {
                StatusText.Text = LocService["status.simulator.blocked.errors"];
                _authService.RecordAudit(AuditAction.DataExported,
                    LocService.Format("status.simulator.blocked.count", findings.Count(f => f.Severity == HmiEditor.Core.Services.FindingSeverity.Error)),
                    false);
                return;
            }

            var result = HmiEditor.Core.Services.SimulatorProjectExporter.Export(_currentProject, code, outputPath);

            StatusText.Text = LocService.Format("status.simulator.exported", result.DirectoryPath, result.ScreenFunctions.Count);
            _authService.RecordAudit(AuditAction.DataExported,
                LocService.Format("audit.simulator.export", result.DirectoryPath), true);
        }
        catch (Exception ex)
        {
            var failure = UiOperationGuard.FailureDetail(Lang.UiOpExportSimulator, outputPath, ex);   // 状态栏与审计行共用同一句
            StatusText.Text = failure;
            _authService.RecordAudit(AuditAction.DataExported, failure, false);
        }
    }

    // ── 辅助方法 ──

    private void DeleteCurrentControl()
    {
        var selectedControls = CanvasPanel.Canvas.GetSelectedControls();
        if (selectedControls.Count == 0) return;

        var controls = selectedControls
            .Select(control => (Control: control, Index: _currentScreen.Controls.IndexOf(control)))
            .Where(item => item.Index >= 0)
            .OrderByDescending(item => item.Index)
            .ToList();
        if (controls.Count == 0) return;

        _undoRedoManager.Execute(new SnapshotCommand(
            controls.Count == 1 ? LocService[Lang.UndoDeleteControl] : LocService.Format(Lang.UndoDeleteControls, controls.Count),
            () =>
            {
                foreach (var item in controls.OrderBy(item => item.Index))
                    if (!_currentScreen.Controls.Contains(item.Control))
                        _currentScreen.Controls.Insert(item.Index, item.Control);
            },
            () =>
            {
                foreach (var item in controls)
                    _currentScreen.Controls.Remove(item.Control);
            }));

        CanvasPanel.Canvas.ClearSelection();
        PropertyPanel.BindControl(null);
        CanvasPanel.Refresh();
        ProjectExplorerPanel.Refresh(_currentScreen);
        BtnDeleteControl.IsVisible = false;
        SetAlignmentGroupVisible(false);
        SetUnsavedChanges(true);
        UpdateStatusBar();
    }

    // ── 旋转/镜像变换（工具栏，操作入撤销栈） ──

    /// <summary>按选中状态显隐变换工具栏按钮。</summary>
    private void SetTransformButtonsVisible(bool visible)
    {
        BtnRotateLeft.IsVisible = visible;
        BtnRotateRight.IsVisible = visible;
        BtnMirrorH.IsVisible = visible;
        BtnMirrorV.IsVisible = visible;
        BtnResetTransform.IsVisible = visible;
    }

    /// <summary>
    /// 旋转选中控件（delta 度，工具栏 90° 步进）：顺时针 +90、逆时针 -90。
    /// 角度归一化到 [0,360)，多次操作正确叠加；入撤销栈可 Ctrl+Z 撤销。
    /// </summary>
    private void RotateSelected(double delta)
    {
        var controls = CanvasPanel.Canvas.GetSelectedControls();
        if (controls.Count == 0) return;
        var snapshot = controls.Select(c => new ControlTransformSnapshot(c)).ToList();
        foreach (var c in controls)
            c.Rotation = HmiControl.NormalizeRotation(c.Rotation + delta);
        ApplyTransformCommand(snapshot, LocService.Format(Lang.UndoRotate, delta));
    }

    /// <summary>切换选中控件镜像（mirrorX/mirrorY 二选一翻转），可重复叠加（再点还原）。</summary>
    private void MirrorSelected(bool mirrorX, bool mirrorY)
    {
        var controls = CanvasPanel.Canvas.GetSelectedControls();
        if (controls.Count == 0) return;
        var snapshot = controls.Select(c => new ControlTransformSnapshot(c)).ToList();
        foreach (var c in controls)
        {
            if (mirrorX) c.MirrorX = !c.MirrorX;
            if (mirrorY) c.MirrorY = !c.MirrorY;
        }
        ApplyTransformCommand(snapshot, LocService[mirrorX ? Lang.UndoMirrorH : Lang.UndoMirrorV]);
    }

    /// <summary>重置选中控件变换（旋转归零 + 镜像关闭），入撤销栈可还原。</summary>
    private void ResetSelectedTransform()
    {
        var controls = CanvasPanel.Canvas.GetSelectedControls();
        if (controls.Count == 0) return;
        var snapshot = controls.Select(c => new ControlTransformSnapshot(c)).ToList();
        foreach (var c in controls)
        {
            c.Rotation = 0;
            c.RotationDirection = null;
            c.MirrorX = false;
            c.MirrorY = false;
        }
        ApplyTransformCommand(snapshot, LocService[Lang.UndoResetTransform]);
    }

    private void ApplyTransformCommand(List<ControlTransformSnapshot> snapshot, string description)
    {
        _undoRedoManager.Execute(new TransformCommand(snapshot, description));
        CanvasPanel.Refresh();
        // 属性面板同步显示（重新绑定首个选中控件刷新旋转/镜像编辑器）
        PropertyPanel.BindControl(CanvasPanel.Canvas.GetSelectedControls().FirstOrDefault());
        SetUnsavedChanges(true);
        UpdateStatusBar();
    }

    /// <summary>控件变换快照（撤销/重做恢复用）。</summary>
    private sealed record ControlTransformSnapshot(HmiControl Control)
    {
        public double Rotation { get; } = Control.Rotation;
        public string? RotationDirection { get; } = Control.RotationDirection;
        public bool MirrorX { get; } = Control.MirrorX;
        public bool MirrorY { get; } = Control.MirrorY;

        /// <summary>把快照值写回控件（Undo/Redo 调用）。</summary>
        public void Restore()
        {
            Control.Rotation = Rotation;
            Control.RotationDirection = RotationDirection;
            Control.MirrorX = MirrorX;
            Control.MirrorY = MirrorY;
        }
    }

    /// <summary>变换撤销命令：恢复旋转/镜像属性并重绘画布。</summary>
    private sealed class TransformCommand : IUndoableCommand
    {
        private readonly List<ControlTransformSnapshot> _before;
        private readonly List<ControlTransformSnapshot> _after;
        public string Description { get; }

        public TransformCommand(List<ControlTransformSnapshot> before, string description)
        {
            _before = before;
            _after = before.Select(s => new ControlTransformSnapshot(s.Control)).ToList();
            Description = description;
        }

        public void Execute() => Redo();

        public void Undo()
        {
            foreach (var s in _before) s.Restore();
        }

        public void Redo()
        {
            foreach (var s in _after) s.Restore();
        }
    }

    /// <summary>
    /// 窗口级快捷键处理：
    ///  基础: Delete 删除、Ctrl+A 全选、Ctrl+=/-/0 缩放
    ///  元件操作: Ctrl+C 复制、Ctrl+V 粘贴、Ctrl+D 多重复制(1份+20px偏移)
    ///  组合拆分: Ctrl+G 组合、Ctrl+Shift+G 拆分
    ///  布局对齐: Alt+H 左/Alt+J 中/Alt+K 右(水平); Alt+T 上/Alt+M 中/Alt+B 下(垂直)
    ///  等距排列: Ctrl+Shift+H 水平等距、Ctrl+Shift+V 垂直等距
    ///  统一尺寸: Ctrl+Shift+S 匹配锚点控件宽高
    ///  主题切换: Ctrl+J 循环切换 Dark/Ocean/Light 主题
    ///  工具箱搜索: Ctrl+L 聚焦搜索框并选中当前筛选文本
    /// 焦点位于文本输入控件时跳过，避免干扰文本编辑。
    /// </summary>
    /// <summary>
    /// 全局快捷键入口：只做"是否文本编辑场景 → 查表 → 分发"三件事。
    /// 绑定表与解析逻辑在 <see cref="EditorCommandCatalog"/>（可脱离 UI 测试）。
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // 文本编辑场景（TextBox/ComboBox/NumberBox）不拦截全局快捷键；
        // Toolbox 搜索框自行处理 Ctrl+L/Ctrl+A，避免破坏文本编辑语义。
        if (EditorCommandCatalog.IsTextEditingContext(FocusManager?.GetFocusedElement()))
            return;

        if (EditorCommandCatalog.Resolve(e.Key, e.KeyModifiers) is not { } binding)
            return;

        ExecuteEditorCommand(binding.Command);
        e.Handled = true;
    }

    /// <summary>
    /// **编辑器命令的唯一实现** —— 全局快捷键与画布右键菜单都走这里。
    /// 此前两条路径各写了一份 switch（覆盖同样 15 个操作），已经开始漂移
    /// （Ctrl+Shift+V 被 Paste 分支抢先命中，导致"垂直等距排列"快捷键失效）。
    /// </summary>
    private void ExecuteEditorCommand(EditorCommand command)
    {
        switch (command)
        {
            // ── 编辑 ──
            case EditorCommand.FocusSearch:
                ToolboxPanel.FocusSearch();
                break;
            case EditorCommand.CancelGesture:
                CanvasPanel.Canvas.CancelActiveGesture();
                if (DockLayoutState.ActiveDrawTool != null)
                    SetDrawTool(null);
                break;
            case EditorCommand.Delete:
                DeleteCurrentControl();
                break;
            case EditorCommand.SelectAll:
                CanvasPanel.Canvas.SelectAllControls();
                break;
            case EditorCommand.Copy:
                CanvasPanel.CopySelectedControls();
                break;
            case EditorCommand.Paste:
                CanvasPanel.PasteControls();
                SetUnsavedChanges(true);
                RefreshPanelsAfterEdit();
                break;
            case EditorCommand.Duplicate:
                DuplicateCurrentControl();
                break;

            // ── 缩放 ──
            case EditorCommand.ZoomIn:
                SetZoom(_zoomLevel + 0.25);
                break;
            case EditorCommand.ZoomOut:
                SetZoom(_zoomLevel - 0.25);
                break;
            case EditorCommand.ZoomReset:
                SetZoom(1.0);
                break;

            // ── 组合 / 拆分 ──
            case EditorCommand.Group:
                ApplySelectionEdit(EditorCommandCatalog.Describe(command),
                    () => CanvasPanel.GroupSelectedControls() != null);
                SetUnsavedChanges(true);
                RefreshPanelsAfterEdit();
                break;
            case EditorCommand.Ungroup:
                ApplySelectionEdit(EditorCommandCatalog.Describe(command),
                    () => CanvasPanel.UngroupSelectedControls() > 0);
                SetUnsavedChanges(true);
                RefreshPanelsAfterEdit();
                break;

            // ── 对齐 ──
            case EditorCommand.AlignLeft:
            case EditorCommand.AlignCenterH:
            case EditorCommand.AlignRight:
            case EditorCommand.AlignTop:
            case EditorCommand.AlignCenterV:
            case EditorCommand.AlignBottom:
                ApplySelectionEdit(EditorCommandCatalog.Describe(command),
                    () => CanvasPanel.AlignSelected(ToAlignDirection(command)));
                SetUnsavedChanges(true);
                RefreshPanelsAfterEdit();
                break;

            // ── 等距排列 / 统一尺寸 ──
            case EditorCommand.DistributeH:
                ApplySelectionEdit(EditorCommandCatalog.Describe(command), () => CanvasPanel.DistributeHorizontal());
                SetUnsavedChanges(true);
                RefreshPanelsAfterEdit();
                break;
            case EditorCommand.DistributeV:
                ApplySelectionEdit(EditorCommandCatalog.Describe(command), () => CanvasPanel.DistributeVertical());
                SetUnsavedChanges(true);
                RefreshPanelsAfterEdit();
                break;
            case EditorCommand.SameSize:
                ApplySelectionEdit(EditorCommandCatalog.Describe(command), () => CanvasPanel.MakeSameSize());
                SetUnsavedChanges(true);
                RefreshPanelsAfterEdit();
                break;

            // ── 主题循环（Dark → Ocean → Light → Dark）──
            case EditorCommand.CycleTheme:
                var nextTheme = App.ThemeService.CycleTheme();
                UpdateThemeCheckmarks();
                var themeName = nextTheme switch
                {
                    AppThemeService.HmiTheme.Light => LocService["menu.theme.light"],
                    AppThemeService.HmiTheme.Ocean => LocService["menu.theme.ocean"],
                    _ => LocService["menu.theme.dark"]
                };
                ShowTip(LocService.Format(Lang.StatusThemeSwitched, themeName));
                break;
        }
    }

    /// <summary>对齐命令 → 画布对齐方向。</summary>
    private static DesignCanvas.MultiAlignDirection ToAlignDirection(EditorCommand command) => command switch
    {
        EditorCommand.AlignLeft => DesignCanvas.MultiAlignDirection.Left,
        EditorCommand.AlignCenterH => DesignCanvas.MultiAlignDirection.CenterHorizontal,
        EditorCommand.AlignRight => DesignCanvas.MultiAlignDirection.Right,
        EditorCommand.AlignTop => DesignCanvas.MultiAlignDirection.Top,
        EditorCommand.AlignCenterV => DesignCanvas.MultiAlignDirection.CenterVertical,
        _ => DesignCanvas.MultiAlignDirection.Bottom,
    };

    /// <summary>
    /// 多重复制辅助：默认复制 1 份、向右下各偏移 20px（用户可通过属性面板调整间距）。
    /// </summary>
    private void DuplicateCurrentControl()
    {
        var newCtrls = CanvasPanel.DuplicateSelected(count: 1, spacingX: 20, spacingY: 20);
        if (newCtrls != null && newCtrls.Count > 0)
        {
            SetUnsavedChanges(true);
            RefreshPanelsAfterEdit();
        }
    }

    /// <summary>
    /// 元件编辑操作后刷新关联面板（项目树、图层、属性等）。
    /// </summary>
    private void RefreshPanelsAfterEdit()
    {
        try
        {
            ProjectExplorerPanel?.Refresh(_currentScreen);
            LayerPanel?.Refresh();
            CanvasPanel?.Refresh();
        }
        catch { /* 面板未就绪时静默忽略 */ }
    }

    private void SwitchToScreen(HmiScreen screen)
    {
        _currentScreen = screen;
        CanvasPanel.SetScreen(screen, _currentProject);
        ProjectExplorerPanel.Refresh(screen);
        PropertyPanel.CurrentScreen = screen;
        PropertyPanel.BindControl(null);
        PropertyPanel.ShowAiSuggestions(screen);
        CanvasPanel.ClearSelection();
        UpdateStatusBar();
        UpdateDockLayoutState();

        // R2-T2: 预览模式激活时，切换屏幕需重新注册新屏幕的绑定变量
        if (CanvasPanel.Canvas.IsPreviewMode)
        {
            _previewRuntime.Stop();
            _previewRuntime.Start();
        }
    }

    /// <summary>JSON 深拷贝（用于屏幕/控件复制）。</summary>
    private static T JsonClone<T>(T source) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(
            System.Text.Json.JsonSerializer.Serialize(source, HmiEditor.Core.Serialization.JsonOptionsDefaults.Default),
            HmiEditor.Core.Serialization.JsonOptionsDefaults.Default)!;

    // ── 项目浏览器右键"新建/打开"对话框入口 ──

    private void OpenScreenManagerDialog()
    {
        var dialog = new Views.ScreenManagerView(_currentProject, _currentScreen);
        dialog.ScreenSwitchRequested += (_, screen) =>
        {
            SwitchToScreen(screen);
            NavigationFlowPanel.Refresh();
        };
        dialog.ShowDialog(this);
    }

    private void OpenVariableManagerDialog()
    {
        var dialog = new Views.VariableManagerView(_currentProject.VariableManager);
        dialog.ShowDialog(this);
    }

    private void OpenAlarmConfigDialog()
    {
        var dialog = new Views.AlarmConfigView(_currentProject.AlarmGroups, _currentProject);
        dialog.AlarmConfigChanged += (_, _) => SetUnsavedChanges(true);
        dialog.ShowDialog(this);
    }

    private void OpenTrendConfigDialog()
    {
        var dialog = new Views.TrendConfigView(_currentProject.TrendConfigs);
        dialog.ShowDialog(this);
    }

    private void OpenScriptEditorDialog()
    {
        var dialog = new Views.ScriptEditorView(
            _currentProject.Scripts, _currentProject.ScriptBindings, _currentProject,
            debugValueProvider: name => _previewRuntime.PreviewValues.GetValueOrDefault(name),
            aiManager: AiManager);
        dialog.ScriptListChanged += (_, _) => SetUnsavedChanges(true);
        dialog.ShowDialog(this);
    }

    // ── AI 助手生成结果处理 (预览→确认→应用 非破坏性工作流) ──

    /// <summary>AI 预览前的当前屏幕, 用于取消时恢复。</summary>
    private HmiScreen? _screenBeforeAiPreview;

    private void OnAiPreviewRequested(object? sender, HmiScreen screen)
    {
        // 首次预览记录原屏幕; 预览不写入项目集合, 取消可无损恢复
        _screenBeforeAiPreview ??= _currentScreen;
        CanvasPanel.SetScreen(screen, _currentProject);
        UpdateStatusBar();
    }

    private void OnAiApplyRequested(object? sender, HmiScreen screen)
    {
        // 与 BtnNewScreen 一致: 直接加入项目并切换
        _currentProject.Screens.Add(screen);
        SwitchToScreen(screen);
        NavigationFlowPanel.Refresh();
        SetUnsavedChanges(true);
        _screenBeforeAiPreview = null;
    }

    private void OnAiCancelRequested()
    {
        if (_screenBeforeAiPreview is not null)
        {
            SwitchToScreen(_screenBeforeAiPreview);
            _screenBeforeAiPreview = null;
        }
    }

    /// <summary>AI 生成的 Faceplate 模板 → 加入模板库。</summary>
    private void OnAiTemplateApplyRequested(object? sender, ControlTemplate template)
    {
        _templateManager.AddPredefined(new[] { template });
        FaceplatePanel.LoadTemplates(_templateManager.Templates);
        SetUnsavedChanges(true);
    }

    /// <summary>AI 生成的 Lua 脚本 → 加入脚本集合。</summary>
    private void OnAiScriptApplyRequested(object? sender, HmiEditor.Core.Models.Scripting.HmiScript script)
    {
        _currentProject.Scripts.Add(script);
        SetUnsavedChanges(true);
    }

    /// <summary>项目管理器「配置」节点 → 打开对应配置窗口（与菜单入口共用同一逻辑）。</summary>
    private void OpenConfigWindow(string key)
    {
        if (_currentProject is null) return;

        switch (key)
        {
            case "project_settings":
                var ps = new Views.ProjectSettingsView(_currentProject);
                ps.ProjectSettingsChanged += (_, _) => SetUnsavedChanges(true);
                ShowModalDialog(ps);
                break;
            case "communication":
                ShowModalDialog(new Views.CommunicationConfigView(_currentProject.VariableManager, _currentProject));
                break;
            case "alarms":
                var ac = new Views.AlarmConfigView(_currentProject.AlarmGroups, _currentProject);
                ac.AlarmConfigChanged += (_, _) => SetUnsavedChanges(true);
                ShowModalDialog(ac);
                break;
            case "trends":
                ShowModalDialog(new Views.TrendConfigView(_currentProject.TrendConfigs));
                break;
            case "recipes":
                ShowModalDialog(new Views.RecipeEditorView(_recipeManager, _currentProject.VariableManager));
                break;
            case "users":
                ShowModalDialog(new Views.UserManagerView(_authService));
                break;
            case "scripts":
                ShowModalDialog(new Views.ScriptEditorView(
                    _currentProject.Scripts, _currentProject.ScriptBindings, _currentProject,
                    debugValueProvider: name => _previewRuntime.PreviewValues.GetValueOrDefault(name),
                    aiManager: AiManager));
                break;
            case "variables":
                ShowModalDialog(new Views.VariableManagerView(_currentProject.VariableManager));
                break;
            case "variable_import":
                ShowModalDialog(new Views.VariableImportView(_currentProject.VariableManager));
                break;
        }
    }

    // ─────────────────────────── Agent 智能体 ───────────────────────────

    /// <summary>创建 Agent 宿主并绑定 MainWindow 回调桥。</summary>
    private AgentHost CreateAgentHost()
    {
        var host = new AgentHost(new AgentHostBridge
        {
            GetProject = () => _currentProject,
            ReplaceProject = ReplaceProjectForAgent,
            MarkChanged = () => SetUnsavedChanges(true),
            RefreshPanels = RefreshAgentPanels,
            OpenPanel = OpenDockTool,
            SwitchScreen = name => SwitchToScreenByName(name),
            GetAuthService = () => _authService,
            GetRecipeManager = () => _recipeManager,
            SaveProject = () => { SaveProject(); return Task.CompletedTask; },
            ExportCode = () => { ExportCode(); return Task.CompletedTask; }
        });
        host.SetProvider(AiManager.CurrentProvider, AiManager.Config.ForceXmlProtocol);
        return host;
    }

    /// <summary>Agent 回滚后整体替换项目对象并刷新所有面板。</summary>
    private void ReplaceProjectForAgent(HmiProject project)
    {
        _currentProject = project;
        _authService.ApplySecurityConfig(project.SecurityConfig);
        LoadProjectIntoPanels();
        var first = project.Screens.FirstOrDefault();
        if (first is not null) SwitchToScreen(first);
        SetUnsavedChanges(true);
    }

    /// <summary>Agent 写操作后刷新依赖项目数据的面板。</summary>
    private void RefreshAgentPanels()
    {
        CanvasPanel.Refresh();
        LayerPanel.Refresh();
        NavigationFlowPanel.Refresh();
        if (_currentScreen is not null)
            ProjectExplorerPanel.Refresh(_currentScreen);
        if (_currentScreen is not null)
            PropertyPanel.ShowAiSuggestions(_currentScreen);
    }

    /// <summary>按屏幕名切换屏幕（预览动作 SwitchScreen 回调）。</summary>
    private bool SwitchToScreenByName(string screenName)
    {
        var target = _currentProject?.Screens.FirstOrDefault(s =>
            string.Equals(s.Name, screenName, StringComparison.Ordinal));
        if (target == null)
        {
            StatusText.Text = LocService.Format("status.preview.screen.missing", screenName);
            return false;
        }
        StatusText.Text = LocService.Format("status.preview.screen.switched", screenName);
        SwitchToScreen(target);
        return true;
    }

    /// <summary>按脚本 Id 执行脚本（预览动作 ExecuteScript 回调）。</summary>
    private bool ExecuteScriptById(string scriptId)
    {
        var script = _currentProject?.Scripts.FirstOrDefault(s => s.Id == scriptId);
        if (script == null)
        {
            StatusText.Text = LocService.Format("status.preview.script.missing", scriptId);
            return false;
        }
        StatusText.Text = LocService.Format("status.preview.script.executing", script.Name);
        return true;
    }

    // ── I3: Dock 工具定位（菜单项打开已注册的停靠面板）──

    /// <summary>
    /// 「面板」菜单：打开/置顶某个面板。
    /// 查找、激活、以及"被标签栏 × 关闭后按老家挂回来"的逻辑都在
    /// <see cref="Services.HmiDockFactory.OpenDockable"/>（阶段 162 移进工厂 —— 那边的判据够得着）。
    /// </summary>
    private void OpenDockTool(string dockableId)
    {
        if (_dockFactory is null || _dockLayout is null) return;
        try { _dockFactory.OpenDockable(_dockLayout, dockableId); }
        catch { /* 布局异常时静默失败（面板仍可从"面板"菜单再试） */ }
    }

    // ── R2-T2: 设计时数据预览模式 ──

    /// <summary>
    /// L3: 解析绑定标识对应的变量 DataType, 用于选择仿真信号类型。
    /// 支持 Id/Name 回退查找, 以及 UDT 字段 compound id。
    /// </summary>
    /// <summary>
    /// async void 关键操作失败时的统一兜底：落盘日志 + 状态栏提示。
    /// 与 Program 中的全局 Dispatcher 处理器互补 —— 全局处理器防止崩溃，
    /// 这里保证「保存/编译/下载」等关键操作失败时用户可见，避免"点了没反应"。
    /// </summary>
    /// <param name="operationKey">
    /// 操作名的**本地化键**（<c>Lang.UiOp*</c>）。与 <c>UiOperationGuard</c> 用**同一套模板**
    /// （<see cref="UiOperationGuard.FailureSummary"/> / <see cref="UiOperationGuard.FailureDetail"/>）——
    /// 这里原本抄了一份逐字相同的模板，本地化时两处必然漏一处。
    /// </param>
    private void ReportOperationFailure(string operationKey, Exception ex, string? targetPath = null)
    {
        AppLog.Error(UiOperationGuard.FailureSummary(operationKey), ex);
        ShowGlobalStatus(UiOperationGuard.FailureDetail(operationKey, targetPath, ex));
    }

    /// <summary>
    /// 把消息写到主窗口状态栏。状态栏不可用时静默忽略 —— 调用方必已落盘日志。
    /// 同时用作 <see cref="UiOperationGuard.UiSink"/>，供没有本地状态行的视图回落。
    /// </summary>
    private void ShowGlobalStatus(string message)
    {
        try { StatusText.Text = message; }
        catch { /* 状态栏不可用（窗口销毁中），日志已落盘 */ }
    }

    private void SetZoom(double zoom)
    {
        var target = Math.Clamp(zoom, 0.25, 4.0);
        if (Math.Abs(target - _zoomLevel) < 0.001)
            return;

        _zoomAnimationTimer?.Stop();
        _zoomAnimationStart = _zoomLevel;
        _zoomAnimationTarget = target;
        _zoomAnimationStarted = DateTime.UtcNow;
        _zoomAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _zoomAnimationTimer.Tick += OnZoomAnimationTick;
        _zoomAnimationTimer.Start();
    }

    private void OnZoomAnimationTick(object? sender, EventArgs e)
    {
        const double durationMs = 140.0;
        var elapsed = (DateTime.UtcNow - _zoomAnimationStarted).TotalMilliseconds;
        var t = Math.Clamp(elapsed / durationMs, 0.0, 1.0);
        // Cubic ease-out：快速响应后平滑落位，避免画布缩放突跳。
        var eased = 1.0 - Math.Pow(1.0 - t, 3.0);
        _zoomLevel = _zoomAnimationStart + (_zoomAnimationTarget - _zoomAnimationStart) * eased;
        CanvasPanel.Canvas.ZoomLevel = _zoomLevel;
        ZoomLevelText.Text = $"{(_zoomLevel * 100):F0}%";
        CanvasPanel.Refresh();

        if (t >= 1.0)
        {
            _zoomAnimationTimer?.Stop();
            _zoomAnimationTimer = null;
            _zoomLevel = _zoomAnimationTarget;
            try { _settingsService.Display.SetZoomLevel(_zoomLevel); } catch { }
        }
    }

    private void SetUnsavedChanges(bool hasChanges)
    {
        _hasUnsavedChanges = hasChanges;
        Title = hasChanges
            ? $"{LocService["app.title"]} — {_currentProject.Name} *"
            : $"{LocService["app.title"]} — {_currentProject.Name}";
    }

    /// <summary>
    /// 更新 DockLayoutState 供工厂模式创建新 View 时恢复数据。
    /// </summary>
    private void UpdateDockLayoutState()
    {
        DockLayoutState.CurrentProject = _currentProject;
        DockLayoutState.CurrentScreen = _currentScreen;
        DockLayoutState.SelectedControl = null; // 选中状态在切换屏幕时重置
        DockLayoutState.AssetLibrary = _assetLibrary;
        DockLayoutState.Templates = _templateManager?.Templates;
        DockLayoutState.ProjectDirectory = _projectDirectory;
        DockLayoutState.ResourceLibrary = _resourceLibrary;
    }

    /// <summary>处理画布选中变化 → 属性面板 + 控件树 + 层面板（由 <c>DockLayoutState</c> 转发，见 #46）。</summary>
    private void HandleCanvasSelectionChanged(HmiControl? firstControl)
    {
        if (firstControl != null)
            _propertyEditSnapshots[firstControl] = SerializeControl(firstControl);

        if (firstControl == null)
            _screenEditSnapshots[_currentScreen] = SerializeScreen(_currentScreen);

        PropertyPanel.CurrentScreen = _currentScreen;
        PropertyPanel.BindControl(firstControl);
        LayerPanel.SelectControl(firstControl);
        BtnDeleteControl.IsVisible = firstControl != null;
        SetAlignmentGroupVisible(firstControl != null);
        UpdateStatusBar();
        DockLayoutState.SelectedControl = firstControl;
    }

    /// <summary>
    /// 处理属性面板修改 → 撤销快照 + 画布重绘。
    /// </summary>
    /// <remarks>
    /// 由 <c>Wiring.Canvas.cs</c> 从 <c>DockLayoutState.PropertyChanged</c> 转发进来。
    /// 此前还有一个实例事件包装 <c>OnPropertyPanelChanged</c>（订 <c>PropertyPanel.PropertyChanged</c>）
    /// 直调本方法，两条路各执行一遍 —— 已随 #46 删除。
    /// </remarks>
    private void HandlePropertyPanelChanged(HmiControl control)
    {
        if (!_historyReplay)
        {
            var after = SerializeControl(control);
            if (!_propertyEditSnapshots.TryGetValue(control, out var before))
                before = after;

            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                _historyReplay = true;
                try
                {
                    _undoRedoManager.Execute(new SnapshotCommand(
                        LocService.Format(Lang.UndoModifyControl, Services.ControlNameDefaults.Display(control)),
                        () => RestoreControl(control, before),
                        () => RestoreControl(control, after)));
                }
                finally { _historyReplay = false; }
                _propertyEditSnapshots[control] = after;
            }
        }

        CanvasPanel.Refresh();
        ProjectExplorerPanel.Refresh(_currentScreen);
        SetUnsavedChanges(true);
    }

    private static string SerializeControl(HmiControl control) =>
        System.Text.Json.JsonSerializer.Serialize(control);

    private static string SerializeScreen(HmiScreen screen) =>
        System.Text.Json.JsonSerializer.Serialize(screen);

    private static void RestoreScreen(HmiScreen target, string json)
    {
        var source = System.Text.Json.JsonSerializer.Deserialize<HmiScreen>(json);
        if (source == null) return;
        foreach (var property in typeof(HmiScreen).GetProperties()
                     .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0))
            property.SetValue(target, property.GetValue(source));
    }

    private static void RestoreControl(HmiControl target, string json)
    {
        var source = System.Text.Json.JsonSerializer.Deserialize<HmiControl>(json);
        if (source == null) return;

        foreach (var property in typeof(HmiControl).GetProperties()
                     .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0))
        {
            property.SetValue(target, property.GetValue(source));
        }
    }

    /// <summary>
    /// 屏幕页面属性修改（未选中控件时编辑）→ 画布/项目浏览器刷新。
    /// </summary>
    private void OnScreenPropertyPanelChanged(object? sender, HmiScreen screen)
    {
        if (!_historyReplay)
        {
            var after = SerializeScreen(screen);
            if (!_screenEditSnapshots.TryGetValue(screen, out var before))
                before = after;
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                _historyReplay = true;
                try
                {
                    _undoRedoManager.Execute(new SnapshotCommand(
                        LocService.Format(Lang.UndoModifyScreen, screen.Name),
                        () => RestoreScreen(screen, before),
                        () => RestoreScreen(screen, after)));
                }
                finally { _historyReplay = false; }
                _screenEditSnapshots[screen] = after;
            }
        }

        CanvasPanel.Refresh();
        ProjectExplorerPanel.Refresh(_currentScreen);
        UpdateStatusBar();
        SetUnsavedChanges(true);
    }

    /// <summary>
    /// 层面板选中 → 画布高亮 + 属性面板。
    /// </summary>
    /// <remarks>
    /// 由 <c>Wiring.Canvas.cs</c> 从 <c>DockLayoutState.LayerSelectionChanged</c> 转发进来。
    /// </remarks>
    private void HandleLayerSelectionChanged(HmiControl? control)
    {
        PropertyPanel.BindControl(control);
        BtnDeleteControl.IsVisible = control != null;
        SetAlignmentGroupVisible(control != null);
        UpdateStatusBar();
    }

    /// <summary>
    /// 工具箱控件选中 → 在当前屏幕中心添加控件。
    /// </summary>
    /// <remarks>
    /// 由 <c>Wiring.Canvas.cs</c> 从 <c>DockLayoutState.ToolItemSelected</c> 转发进来。
    /// 工具栏控件快捷按钮同样走此路径。此前这里还兼作 <c>ToolboxPanel.ToolItemSelected</c>
    /// 实例事件的处理器（共享同一个方法），该直订已随 #46 删除。
    /// </remarks>
    private void OnToolItemSelected(object? sender, HmiControlType type)
    {
        var canvas = CanvasPanel.Canvas;
        // 无当前屏幕（未打开项目 / 画布尚未注入 Screen）时不添加，避免抛异常
        if (canvas.Screen == null) return;

        var (centerX, centerY) = CanvasCenter(canvas);

        var control = canvas.AddControl(type, centerX, centerY);
        var command = new AddControlCommand(_currentScreen.Controls, control);
        _undoRedoManager.Execute(command);

        // 刷新所有面板（通过 GetActiveView 获取实际显示的实例）
        RebuildAllPanels();
        SetUnsavedChanges(true);
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        var totalControls = _currentScreen.Controls.Count;
        ControlCountText.Text = $"{LocService["general.controls"]}: {totalControls} | {LocService["general.screens"]}: {_currentProject.Screens.Count}";

        var selectedControls = CanvasPanel.Canvas.GetSelectedControls();
        if (selectedControls.Count > 0)
        {
            var first = selectedControls[0];
            StatusText.Text = selectedControls.Count > 1
                ? LocService.Format("status.controls.selected", selectedControls.Count, Services.ControlNameDefaults.Display(first), Services.ControlTypeTextKeys.Describe(first.Type))
                : $"{LocService["status.selected"]}: {Services.ControlNameDefaults.Display(first)} ({Services.ControlTypeTextKeys.Describe(first.Type)}) — X:{first.X:F0} Y:{first.Y:F0} W:{first.Width:F0} H:{first.Height:F0}";
        }
        else
        {
            StatusText.Text = $"{LocService["project.status.ready"]}{LocService.Format("status.canvas.drop.hint", LocService["status.current"], _currentScreen.Name, _currentScreen.Width, _currentScreen.Height)}";
        }

        // I8: 状态栏展示 Git 工作流状态
        UpdateGitStatusText();

        UpdateResourceDashboard();
    }

    /// <summary>
    /// 临时在状态栏显示提示文字，2 秒后自动恢复原始状态。
    /// 用于主题切换等瞬时操作反馈。
    /// </summary>
    private DispatcherTimer? _tipTimer;
    private string? _tipOriginalText;
    private Avalonia.Media.Brush? _tipOriginalForeground;

    private void ShowTip(string message)
    {
        // 保存当前状态栏文字以备恢复
        _tipOriginalText ??= StatusText.Text;
        _tipOriginalForeground = (Avalonia.Media.Brush?)StatusText.Foreground;

        StatusText.Text = message;
        StatusText.Foreground = (Avalonia.Media.Brush)Application.Current!.Resources["HmiLinkBrush"]!;

        _tipTimer?.Stop();
        _tipTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _tipTimer.Tick += (_, _) =>
        {
            if (_tipOriginalText != null)
                StatusText.Text = _tipOriginalText;
            if (_tipOriginalForeground != null)
                StatusText.Foreground = _tipOriginalForeground;
            _tipOriginalText = null;
            _tipOriginalForeground = null;
            _tipTimer?.Stop();
        };
        _tipTimer.Start();
    }

    /// <summary>
    /// 资源仪表盘：预算取自**硬件目标契约**（<see cref="IHardwareTarget.Resources"/>），
    /// 判读走 <see cref="ResourceBudget"/>（阈值与下面三个画刷的唯一来源）。
    ///
    /// <para>2026-09-18 修掉的真缺陷：这里原先写死 `8MB Flash / 64MB RAM` 两个"默认值"
    /// （注释还写着"可从 HardwareTarget 获取实际值"，但从未获取过）—— T113 实际是
    /// 128MB Flash，仪表盘把占用率显示成真值的 **8 倍**，而 RAM 显示成 2 倍。
    /// 这与资源预算写错成 16MB 是同一类问题：声明值没有任何一处真的被用起来。</para>
    ///
    /// <para>目标未声明预算时**不显示百分比**（显示绝对量与"未声明预算"），而不是拿一个
    /// 猜出来的分母充数 —— 缺依据就不下结论。</para>
    /// </summary>
    private void UpdateResourceDashboard()
    {
        var estimator = new ResourceEstimator();
        var constraints = ResolveCurrentHardwareTarget()?.Resources;

        ApplyResourceReading(FlashBar, FlashText, ResourceBudget.Evaluate(
            "flash", estimator.EstimateFlashUsage(_currentProject), constraints?.FlashSizeBytes ?? 0));
        ApplyResourceReading(RamBar, RamText, ResourceBudget.Evaluate(
            "ram", estimator.EstimateRamUsage(_currentProject), constraints?.RamSizeBytes ?? 0));

        TargetText.Text = LocService.Format(Lang.StatusTarget, _currentProject.HardwareTargetId ?? "generic");
    }

    private void ApplyResourceReading(ProgressBar bar, TextBlock text, BudgetReading reading)
    {
        var kb = reading.UsedBytes / 1024;
        if (reading.Verdict == BudgetVerdict.Unknown)
        {
            bar.Value = 0;
            bar.Foreground = ResourceBrushes.Muted;
            text.Text = $"{kb:F0}KB ({LocService[Lang.StatusResourceNoBudget]})";
            return;
        }

        bar.Value = Math.Min(100, reading.Percent);
        bar.Foreground = reading.Verdict switch
        {
            BudgetVerdict.Over or BudgetVerdict.NearLimit => ResourceBrushes.Danger,
            BudgetVerdict.Elevated => ResourceBrushes.Warning,
            _ => ResourceBrushes.Normal
        };
        text.Text = $"{reading.Percent:F1}% ({kb:F0}KB)";
    }

    /// <summary>
    /// 解析当前工程选中的硬件目标（用于资源预算）。
    /// 型号由 `firmware/*/firmware.json` 决定 ⇒ 与工程设置面板走**同一条**加载路径；
    /// 固件目录缺失时回落到内置 T113，保证型号始终可用。
    /// 无 `HardwareTargetId`（历史工程/显式未指定）或 id 已失效时返回 null。
    /// </summary>
    private IHardwareTarget? ResolveCurrentHardwareTarget()
    {
        var id = _currentProject.HardwareTargetId;
        if (string.IsNullOrEmpty(id)) return null;
        _hardwareTargetRegistry ??= LoadHardwareTargets();
        return _hardwareTargetRegistry.GetTarget(id);
    }

    private static HardwareTargetRegistry LoadHardwareTargets()
    {
        var registry = new HardwareTargetRegistry();
        registry.LoadFirmwareTargets(Path.Combine(AppContext.BaseDirectory, "firmware"));
        if (!registry.GetAllTargets().Any())
            registry.Register(new AllwinnerT113Target());
        return registry;
    }

    /// <summary>仪表盘画刷（三个阈值色只在这里定义一次）。⚠ 阶段 295：静态字段必须用 Immutable* —— 类型初始化器跑在任意线程上，可变画刷会抢走 Dispatcher（登记 #47）。</summary>
    private static class ResourceBrushes
    {
        public static readonly IBrush Normal = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb(34, 197, 94));
        public static readonly IBrush Warning = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb(245, 158, 11));
        public static readonly IBrush Danger = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb(239, 68, 68));
        public static readonly IBrush Muted = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb(148, 163, 184));
    }

    // ── I8: Git 工作流 ──

    /// <summary>刷新当前项目的 Git 状态（保存/打开后调用）。</summary>
    private void RefreshGitState()
    {
        _gitState = GitWorkflowService.GetState(_projectDirectory ?? "");
        UpdateGitStatusText();
    }

    private void UpdateGitStatusText()
    {
        if (GitStatusText == null) return;

        if (string.IsNullOrEmpty(_projectDirectory) || _gitState == GitWorkflowState.NotAvailable)
        {
            GitStatusText.Text = LocService["status.git.not.ready"];
            GitStatusText.Foreground = HmiEditor.App.Theme.TextSecondary;
            return;
        }

        if (_gitState == GitWorkflowState.Unversioned)
        {
            GitStatusText.Text = LocService["status.git.unversioned"];
            GitStatusText.Foreground = HmiEditor.App.Theme.Warning;
            return;
        }

        var info = GitWorkflowService.GetInfo(_projectDirectory!);
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(info.Branch)) parts.Add(LocService.Format("git.branch", info.Branch));
        if (info.Ahead > 0) parts.Add($"↑{info.Ahead}");
        if (info.Behind > 0) parts.Add($"↓{info.Behind}");
        if (info.UncommittedChanges > 0) parts.Add(LocService.Format("git.uncommitted", info.UncommittedChanges));
        else if (info.LastCommit != null) parts.Add(info.LastCommit.Length > 24 ? info.LastCommit[..24] + "…" : info.LastCommit);

        GitStatusText.Text = LocService.Format("status.git.info", string.Join(" · ", parts));
        GitStatusText.Foreground = HmiEditor.App.Theme.TextSecondary;
    }

    /// <summary>初始化项目目录为 Git 仓库。</summary>
    private void InitGitRepository()
    {
        if (string.IsNullOrEmpty(_projectDirectory))
        {
            StatusText.Text = LocService["status.save.first.for.git"];
            return;
        }

        if (!GitWorkflowService.IsGitAvailable())
        {
            StatusText.Text = LocService["status.git.not.detected"];
            return;
        }

        if (GitWorkflowService.InitializeRepository(_projectDirectory))
        {
            // 生成 .gitignore 与 README，随后完成首次暂存
            EnsureProjectMetaFiles(_projectDirectory);
            GitWorkflowService.StageAll(_projectDirectory);
            RefreshGitState();
            StatusText.Text = LocService["status.git.initialized"];
        }
        else
        {
            StatusText.Text = LocService["status.git.init.failed"];
        }
    }

    /// <summary>为项目目录生成 .gitignore（若缺失）。</summary>
    private static void EnsureProjectMetaFiles(string directory)
    {
        var gitignore = Path.Combine(directory, ".gitignore");
        if (!File.Exists(gitignore))
        {
            File.WriteAllText(gitignore, """
                # HMI Editor 项目辅助文件
                *.tmp
                *.bak
                .app.pid
                """);
        }
    }

    /// <summary>Git 提交对话框 —— 输入提交信息并提交。</summary>
    /// <summary>Git 提交对话框（实现见 <see cref="Services.GitDialogService"/>）。</summary>
    private void GitCommitDialog()
    {
        if (string.IsNullOrEmpty(_projectDirectory) || _gitState != GitWorkflowState.Repository)
        {
            StatusText.Text = LocService["status.git.no.repo"];
            return;
        }

        Services.GitDialogService.ShowCommitDialog(
            this, _projectDirectory, msg => StatusText.Text = msg, RefreshGitState);
    }

    /// <summary>Git 历史对话框 —— 展示最近提交。</summary>
    /// <summary>Git 历史对话框（实现见 <see cref="Services.GitDialogService"/>）。</summary>
    private void ShowGitHistory()
    {
        if (string.IsNullOrEmpty(_projectDirectory) || _gitState != GitWorkflowState.Repository)
        {
            StatusText.Text = LocService["status.git.no.repo"];
            return;
        }

        Services.GitDialogService.ShowHistory(this, _projectDirectory, msg => StatusText.Text = msg);
    }

    // ── I9: 自适应分辨率预览 ──

    /// <summary>分辨率预览切换：激活自适应预览（仅对非 None 模式屏幕有效）。</summary>
    private void OnResolutionPreviewChanged()
    {
        if (_switchingPreview) return;

        var tag = (CboResolutionPreview.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (string.IsNullOrEmpty(tag))
        {
            CanvasPanel.Canvas.PreviewResolution = null;
            CanvasPanel.Canvas.IsEnabled = true;
            CanvasPanel.Canvas.ClearSelection();
            StatusText.Text = $"{LocService["project.status.ready"]}{LocService.Format("status.canvas.drop.hint", LocService["status.current"], _currentScreen.Name, _currentScreen.Width, _currentScreen.Height)}";
            return;
        }

        if (_currentScreen.AdaptiveMode == ScreenAdaptiveMode.None)
        {
            _switchingPreview = true;
            CboResolutionPreview.SelectedIndex = 0;
            _switchingPreview = false;
            StatusText.Text = LocService["status.adaptive.not.enabled"];
            return;
        }

        CanvasPanel.Canvas.PreviewResolution = tag;
        CanvasPanel.Canvas.IsEnabled = false;
        CanvasPanel.Canvas.ClearSelection();
        StatusText.Text = LocService.Format("status.resolution.preview", tag, _currentScreen.AdaptiveMode);
    }

    /// <summary>复位分辨率预览（打开/新建项目时调用）。</summary>
    private void ResetResolutionPreview()
    {
        _switchingPreview = true;
        CboResolutionPreview.SelectedIndex = 0;
        _switchingPreview = false;
        CanvasPanel.Canvas.PreviewResolution = null;
        CanvasPanel.Canvas.IsEnabled = true;
    }

    private void SyncPropertySnapshots()
    {
        foreach (var control in CanvasPanel.Canvas.GetSelectedControls())
            _propertyEditSnapshots[control] = SerializeControl(control);
    }

    private bool ApplySelectionEdit(string description, Func<bool> edit)
    {
        var controls = CanvasPanel.Canvas.GetSelectedControls().ToList();
        if (controls.Count == 0) return false;
        var before = controls.ToDictionary(c => c, SerializeControl);
        if (!edit()) return false;
        var after = controls.ToDictionary(c => c, SerializeControl);
        if (before.All(p => string.Equals(p.Value, after[p.Key], StringComparison.Ordinal))) return false;

        _undoRedoManager.Execute(new SnapshotCommand(
            description,
            () =>
            {
                foreach (var pair in before) RestoreControl(pair.Key, pair.Value);
                CanvasPanel.Refresh();
                SyncPropertySnapshots();
            },
            () =>
            {
                foreach (var pair in after) RestoreControl(pair.Key, pair.Value);
                CanvasPanel.Refresh();
                SyncPropertySnapshots();
            }));
        return true;
    }

    private void UpdateUndoRedoMenu()
    {
        MenuUndo.IsEnabled = _undoRedoManager.CanUndo;
        MenuRedo.IsEnabled = _undoRedoManager.CanRedo;
        MenuUndo.Header = _undoRedoManager.CanUndo
            ? $"{LocService["project.undo"]} {_undoRedoManager.UndoDescription}"
            : LocService["project.undo"];
        MenuRedo.Header = _undoRedoManager.CanRedo
            ? $"{LocService["project.redo"]} {_undoRedoManager.RedoDescription}"
            : LocService["project.redo"];

        BtnUndo.IsEnabled = _undoRedoManager.CanUndo;
        BtnRedo.IsEnabled = _undoRedoManager.CanRedo;
    }

    // ── 对齐工具 ──

    // ── I10: 绘图工具栏 ──

    /// <summary>
    /// 激活指定绘图工具（null = 返回选择模式）。
    /// 设置后同步按钮高亮 —— 即使工具状态未变化（点击已激活工具）也恢复按钮选中态，
    /// 避免 ToggleButton 自动翻转导致高亮与真实状态不一致。
    /// </summary>
    private void SetDrawTool(HmiControlType? tool)
    {
        DockLayoutState.SetDrawTool(tool);
        SyncDrawToolbar(DockLayoutState.ActiveDrawTool);
    }

    /// <summary>
    /// 控件快捷添加：退出绘图模式并在画布中心添加控件（与工具箱单击行为一致，支持撤销）。
    /// </summary>
    private void QuickAddControl(HmiControlType type)
    {
        SetDrawTool(null);
        OnToolItemSelected(this, type);
    }

    /// <summary>同步绘图工具栏按钮高亮与提示文本（工具状态变化时调用）。</summary>
    private void SyncDrawToolbar(HmiControlType? tool)
    {
        BtnToolSelect.IsChecked = tool == null;
        BtnToolRectangle.IsChecked = tool == HmiControlType.Rectangle;
        BtnToolEllipse.IsChecked = tool == HmiControlType.Ellipse;
        BtnToolPolyline.IsChecked = tool == HmiControlType.Polyline;
        BtnToolArc.IsChecked = tool == HmiControlType.Arc;
        BtnToolLine.IsChecked = tool == HmiControlType.Line;
        BtnToolText.IsChecked = tool == HmiControlType.Label;

        DrawHintText.IsVisible = tool != null;
        DrawHintText.Text = tool switch
        {
            null => "",
            // 直线/折线为点击式绘制：提示单击放置起点、再击结束/追加转折点
            HmiControlType.Line or HmiControlType.Polyline
                => $"{LocService["draw.hint.click"]} · {LocService["draw.esc.exit"]}",
            _ => $"{LocService["draw.hint"]} · {LocService["draw.esc.exit"]}",
        };
    }

    /// <summary>
    /// 处理画布右键菜单操作（通过 DockLayoutState 路由）。
    /// 获取当前显示的画布实例执行对应操作。
    /// </summary>
    /// <summary>
    /// 画布右键菜单入口：只做"动作 → 命令"映射，实现复用 <see cref="ExecuteEditorCommand"/>。
    /// </summary>
    private void HandleCanvasContextAction(CanvasContextAction action)
        => ExecuteEditorCommand(EditorCommandCatalog.ToCommand(action));

    // ── Faceplate 模板操作 ──

    /// <summary>从当前画布选中控件创建新模板。</summary>
    private void CreateTemplateFromSelection()
    {
        var selectedControls = CanvasPanel.Canvas.GetSelectedControls();
        if (selectedControls.Count == 0)
        {
            StatusText.Text = LocService["status.select.controls.first"];
            return;
        }

        var dialog = new FaceplateTemplateEditorView(selectedControls);
        dialog.ShowDialog(this);
        dialog.Closed += (_, _) =>
        {
            if (dialog.Result != null)
            {
                _templateManager.AddPredefined(new[] { dialog.Result });
                SetUnsavedChanges(true);
                StatusText.Text = LocService.Format("status.template.created", dialog.Result.Name, dialog.Result.Parameters.Count);
            }
        };
    }

    /// <summary>打开模板编辑器编辑现有模板。</summary>
    private void OpenTemplateEditor(ControlTemplate template)
    {
        var dialog = new FaceplateTemplateEditorView(template);
        dialog.ShowDialog(this);
        dialog.Closed += (_, _) =>
        {
            if (dialog.Result != null)
            {
                FaceplatePanel.RefreshTemplate(dialog.Result);
                SetUnsavedChanges(true);
                StatusText.Text = LocService.Format("status.template.updated", dialog.Result.Name, dialog.Result.Version);
            }
        };
    }

    /// <summary>
    /// 窗口关闭前保存 Dock 布局状态（Proportion + IsOpen）到 .layout.json。
    /// 第三步：序列化整个布局 —— 见 LayoutSerializer.CaptureState / Save。
    ///
    /// 选择 OnClosing 而非 OnClosed：
    ///  - OnClosing 在视觉树和 Dock 模型尚未释放前触发，状态最完整；
    ///  - 同时覆盖用户点 X 关闭和应用 TryShutdown 两条路径；
    ///  - 在 CleanupOnShutdown（释放 Timer / Modbus 驱动）之前完成持久化，语义清晰。
    ///
    /// 布局持久化是 nice-to-have：捕获或写入失败均不阻断关闭流程。
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        try
        {
            PersistMainWindowGeometry();

            if (_dockLayout is not null)
            {
                var state = LayoutSerializer.CaptureState(_dockLayout);
                LayoutSerializer.Save(state);
            }
        }
        catch
        {
            // 布局持久化非关键功能，不阻断关闭
        }
    }

    /// <summary>
    /// 窗口关闭时清理资源。
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        CleanupOnShutdown();
        base.OnClosed(e);
    }

    /// <summary>应用关闭时执行清理工作：释放后台服务的 Timer 和线程资源，确保进程能正常退出。</summary>
    public void CleanupOnShutdown()
    {
        ReportSourceWiring.UnregisterAppLevelSources();   // 运行期数据源（阶段 185）

        // 释放持有 System.Threading.Timer 的后台服务
        _autoSaveTimer?.Stop();
        _autoSaveTimer = null;
        _alarmEngine.Dispose();
        // 预览运行时独占仿真器 Timer 与真实设备连接，统一在这里释放
        _previewRuntime.Dispose();
        AiManager.Dispose();

        // 持久化 HMI 图库资源（内置 + 自定义）到应用数据目录
        // 图库保存失败不影响退出；读取失败时 PersistResourceLibrary 会拒绝覆盖并落日志
        PersistResourceLibrary();

        // 清理撤销/重做历史，释放内存
        _undoRedoManager.Clear();

        // 释放本进程的会话临时目录（工程解包 / 资产工作目录 / 部署中转文件）。
        // 这些目录是"本次运行专用"的，进程退出即无主；不释放就会在 TempRoot 下无限堆积
        // （每次打开工程两个）。被占用而删不掉也没关系 —— 下次启动的孤儿清理会兜住。
        try
        {
            AppPaths.ReleaseSessionTemp();
        }
        catch
        {
            // 临时目录释放失败不影响退出
        }
    }

    // ── 自定义标题栏窗口控制 ── 已移除，使用系统窗口栏

    /// <summary>
    /// 新建工程确认对话框 —— 询问用户是否放弃当前未保存的更改。
    /// </summary>
    private class NewProjectConfirmDialog : Window
    {
        public bool Confirmed { get; private set; }
        public string Message { get; set; } = "";

        public NewProjectConfirmDialog()
        {
            Width = 420;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(19, 28, 49));
            WindowDecorations = WindowDecorations.BorderOnly;
            Title = LocService["dialog.confirm.new.project"];

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                Margin = new Thickness(16)
            };

            var messageText = new TextBlock
            {
                Text = Message,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            // Message 在构造后才设置，用 Loaded 事件同步
            Loaded += (_, _) => messageText.Text = Message;

            var btnPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 8,
                [Grid.RowProperty] = 1
            };

            var cancelBtn = new Button
            {
                Content = LocService["gen.cancel"],
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Padding = new Thickness(14, 6),
                FontSize = 12
            };

            var discardBtn = new Button
            {
                Content = LocService["dialog.button.discard.changes"],
                Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 6),
                FontSize = 12
            };

            cancelBtn.Click += (_, _) => Close();
            discardBtn.Click += (_, _) =>
            {
                Confirmed = true;
                Close();
            };

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(discardBtn);

            grid.Children.Add(messageText);
            grid.Children.Add(btnPanel);
            Content = grid;
        }
    }
}

