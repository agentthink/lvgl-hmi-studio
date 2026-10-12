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

namespace HmiEditor.App;

/// <summary>各配置面板与对话框入口事件（通讯/用户/报警/趋势/脚本/配方/变量/仿真/预览/登录/审计/视图/屏幕/主题/动画）。</summary>
/// <remarks>
/// 自 MainWindow.WireEvents()（原 992 行单方法）按域拆出，纯代码搬移：
/// 方法体逐行未改，WireEvents() 只保留对这几个分域方法的调用。
/// </remarks>
public partial class MainWindow
{
    private void WirePanelAndDialogEvents()
    {
        // ── 通讯配置 ──
        MenuProjectSettings.Click += (_, _) =>
        {
            var dialog = new Views.ProjectSettingsView(_currentProject);
            dialog.ProjectSettingsChanged += (_, _) => SetUnsavedChanges(true);
            dialog.ShowDialog(this);
        };

        MenuCommConfig.Click += (_, _) =>
        {
            var dialog = new Views.CommunicationConfigView(
                _currentProject.VariableManager, _currentProject);
            dialog.ShowDialog(this);
        };

        MenuDeviceManager.Click += (_, _) =>
        {
            var dialog = new Views.DeviceManagerView();
            dialog.ShowDialog(this);
        };

        // ── Phase 3A: 用户管理 ──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openUserManager = (_, _) =>
        {
            var dialog = new Views.UserManagerView(_authService);
            dialog.ShowDialog(this);
        };
        MenuUserManager.Click += openUserManager;
        BtnUserManager.Click += openUserManager;

        // ── Phase 3A: 报警配置 ──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openAlarmConfig = (_, _) =>
        {
            var dialog = new Views.AlarmConfigView(
                _currentProject.AlarmGroups, _currentProject);
            dialog.AlarmConfigChanged += (_, _) => SetUnsavedChanges(true);
            dialog.ShowDialog(this);
        };
        MenuAlarmConfig.Click += openAlarmConfig;
        BtnAlarmConfig.Click += openAlarmConfig;

        // ── Phase 3A: 报警列表（实时监控） ──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openAlarmList = (_, _) =>
        {
            var dialog = new Views.AlarmListView(_alarmEngine);
            dialog.ShowDialog(this);
        };
        MenuAlarmList.Click += openAlarmList;

        // ── Phase 3A: 趋势图配置 ──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openTrendConfig = (_, _) =>
        {
            var dialog = new Views.TrendConfigView(_currentProject.TrendConfigs);
            dialog.ShowDialog(this);
        };
        MenuTrendConfig.Click += openTrendConfig;
        BtnTrendConfig.Click += openTrendConfig;

        // ── Phase 3B: Lua 脚本编辑器 ──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openScriptEditor = (_, _) =>
        {
            // I5: 调试运行复用预览仿真数据源（PreviewValues 为 VariableSimulator 快照）
            var dialog = new Views.ScriptEditorView(
                _currentProject.Scripts, _currentProject.ScriptBindings, _currentProject,
                debugValueProvider: name => _previewRuntime.PreviewValues.GetValueOrDefault(name),
                aiManager: AiManager);
            dialog.ScriptListChanged += (_, _) => SetUnsavedChanges(true);
            dialog.ShowDialog(this);
        };
        MenuScriptEditor.Click += openScriptEditor;
        BtnScriptEditor.Click += openScriptEditor;

        // ── Phase 3B: 配方编辑器 ──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openRecipeEditor = (_, _) =>
        {
            var dialog = new Views.RecipeEditorView(_recipeManager, _currentProject.VariableManager);
            dialog.ShowDialog(this);
        };
        MenuRecipeEditor.Click += openRecipeEditor;
        BtnRecipeEditor.Click += openRecipeEditor;

        // ── Phase 3C: 变量批量导入 ──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openVariableImport = (_, _) =>
        {
            var dialog = new Views.VariableImportView(_currentProject.VariableManager);
            dialog.ShowDialog(this);
        };
        MenuVariableImport.Click += openVariableImport;
        BtnVariableImport.Click += openVariableImport;

        // ── 变量管理 ──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openVarManager = (_, _) =>
        {
            var dialog = new Views.VariableManagerView(_currentProject.VariableManager);
            dialog.ShowDialog(this);
        };
        MenuVariableManager.Click += openVarManager;
        BtnVariableManager.Click += openVarManager;

        // ── Phase 3B: 离线仿真（仿真器实例由预览运行时拥有，两个入口共用同一份状态）──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openSimulator = (_, _) =>
        {
            var dialog = new Views.SimulatorView(
                _previewRuntime.Simulator, _previewRuntime.CommunicationSimulator);
            dialog.ShowDialog(this);
        };
        MenuSimulator.Click += openSimulator;
        BtnSimulator.Click += openSimulator;

        // ── 标签监视器（运行时绑定诊断）──
        MenuTagMonitor.Click += (_, _) =>
        {
            // 构建最新标签表（服务内部：无引擎则新建，有则重建）
            var engine = _previewRuntime.EnsureBindingEngine();

            var monitor = new Views.TagMonitorView(engine,
                valueProvider: id => _previewRuntime.PreviewValues.TryGetValue(id, out var v) ? v : null);
            monitor.RefreshRows();
            monitor.ShowDialog(this);
        };

        // ── R2-T2: 设计时数据预览 —— 画布预览切换驱动仿真器启停 ──
        // 只订总线：DesignCanvasView 在**同一个处理器**里既 invoke 实例事件、又 raise 总线
        //（DesignCanvasView.axaml.cs:193-194）⇒ DockLayoutState.CanvasPreviewModeChanged
        //（Wiring.DockAggregate.cs:232-235）已覆盖全部触发点。
        //
        // ── 预览交互: 控件点击 → 动作执行 ──
        // 同上（DesignCanvasView.axaml.cs:168-169）→ DockLayoutState.CanvasPreviewControlClicked
        //（Wiring.DockAggregate.cs:236-240）。
        // ⚠ 这一路**非幂等**：两路并存时 ExecuteAction(control, "OnClick") 被调用两遍
        // ⇒ 带副作用的动作（切屏 / 写变量 / 记日志）会执行两次（登记表 #46）。

        // ── Phase 3A: 登录 ──
        MenuLogin.Click += async (_, _) =>
        {
            var dialog = new Views.LoginDialog(_authService);
            await dialog.ShowDialog(this);
            if (dialog.SessionId != null)
            {
                var user = _authService.GetSessionUser(dialog.SessionId);
                StatusText.Text = LocService.Format("status.logged.in", user?.Username ?? LocService["status.unknown.user"]);
            }
        };

        // ── I3: 审计日志面板 ──
        MenuAuditLog.Click += (_, _) => OpenDockTool("AuditLog");

        // ── I2: 交叉引用面板 ──
        MenuCrossReference.Click += (_, _) => OpenDockTool("CrossReference");

        // ── 视图菜单：每个面板单独打开入口 ──
        MenuPanelToolbox.Click += (_, _) => OpenDockTool("Toolbox");
        MenuPanelFaceplate.Click += (_, _) => OpenDockTool("Faceplate");
        MenuPanelCanvas.Click += (_, _) => OpenDockTool("Canvas");
        MenuPanelProjectExplorer.Click += (_, _) => OpenDockTool("ProjectExplorer");
        MenuPanelProperty.Click += (_, _) => OpenDockTool("Property");
        MenuPanelLayers.Click += (_, _) => OpenDockTool("Layers");
        MenuPanelAssets.Click += (_, _) => OpenDockTool("Assets");
        MenuPanelResourceLibrary.Click += (_, _) => OpenDockTool("ResourceLibrary");
        MenuPanelNavigation.Click += (_, _) => OpenDockTool("Navigation");
        MenuPanelAuditLog.Click += (_, _) => OpenDockTool("AuditLog");
        MenuPanelCrossReference.Click += (_, _) => OpenDockTool("CrossReference");
        MenuPanelAiChat.Click += (_, _) => OpenDockTool("AiChat");

        // ── 屏幕管理器 ──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openScreenManager = (_, _) =>
        {
            var dialog = new Views.ScreenManagerView(_currentProject, _currentScreen);
            dialog.ScreenSwitchRequested += (_, screen) =>
            {
                SwitchToScreen(screen);
                NavigationFlowPanel.Refresh();
                SetUnsavedChanges(true);
            };
            dialog.ShowDialog(this);
        };
        MenuScreenManager.Click += openScreenManager;
        BtnScreenManager.Click += openScreenManager;

        // ── 主题编辑器 ──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openStyleEditor = (_, _) =>
        {
            var dialog = new Views.StyleEditorView(_currentProject);
            dialog.ShowDialog(this);
        };
        MenuStyleEditor.Click += openStyleEditor;
        BtnStyleEditor.Click += openStyleEditor;

        // ── 设置对话框 ──
        BtnSettings.Click += (_, _) => ShowSettingsDialog();

        // ── 动画编辑器 ──
        EventHandler<Avalonia.Interactivity.RoutedEventArgs> openAnimationEditor = (_, _) =>
        {
            var dialog = new Views.AnimationEditorView(
                _currentProject.AnimationConfigs, _currentProject);
            dialog.AnimationListChanged += (_, _) => SetUnsavedChanges(true);
            dialog.ShowDialog(this);
        };
        MenuAnimationEditor.Click += openAnimationEditor;

        // ── 工具栏事件 ──
        BtnNewScreen.Click += (_, _) =>
        {
            // 统一走 ScreenFactory —— 此前这里硬编码 1024×600，
            // 与屏幕管理器（读 ScreenDefaultsConfig）两个语义。
            var newScreen = ScreenFactory.Create(_currentProject);
            _currentProject.Screens.Add(newScreen);
            SwitchToScreen(newScreen);
            NavigationFlowPanel.Refresh();
            SetUnsavedChanges(true);
        };

        BtnExportCode.Click += (_, _) => ExportCode();
        BtnZoomIn.Click += (_, _) => SetZoom(_zoomLevel + 0.25);
        BtnZoomOut.Click += (_, _) => SetZoom(_zoomLevel - 0.25);
        BtnDeleteControl.Click += (_, _) => DeleteCurrentControl();

        // ── 旋转/镜像变换（工具栏，操作入撤销栈） ──
        BtnRotateLeft.Click += (_, _) => RotateSelected(-90);
        BtnRotateRight.Click += (_, _) => RotateSelected(90);
        BtnMirrorH.Click += (_, _) => MirrorSelected(mirrorX: true, mirrorY: false);
        BtnMirrorV.Click += (_, _) => MirrorSelected(mirrorX: false, mirrorY: true);
        BtnResetTransform.Click += (_, _) => ResetSelectedTransform();

        // R8-FIX: 手动重新加载所有面板内容（解决 Dock 重组后面板空白）
        MenuReloadPanels.Click += (_, _) => OnDockLayoutChanged();
        BtnReloadPanels.Click += (_, _) => OnDockLayoutChanged();
        MenuDockDiag.Click += (_, _) => RunDockEventRoutingDiagnostic();

        // ── 控件模板 ──
        _templateManager.TemplatesChanged += (_, _) =>
        {
                FaceplatePanel.LoadTemplates(_templateManager.Templates);
        };

        // 属性面板也需要模板管理器（Faceplate 参数编辑）
        PropertyPanel.SetTemplateManager(_templateManager);

        // R-FIX: 手动刷新初始模板到 ToolboxPanel 和 FaceplatePanel。
        // 模板在 InitializeTemplateManager() 中已添加，但 TemplatesChanged
        // 事件在上方才注册，不会为已添加的模板触发，因此需要手动刷新一次。
        FaceplatePanel.LoadTemplates(_templateManager.Templates);
    }

    /// <summary>
    /// 跑一次面板重建；失败时把**原因**写进状态栏并返回 <c>false</c>
    /// （登记 <b>#61u</b>，阶段 98）。
    ///
    /// <para><b>为什么做成静态 ＋ 两个显式依赖</b>：这条「失败档」是本次修复的**全部意义**
    /// 所在，必须验得到；而 Avalonia 的 headless 派发有既知死区 ⇒ 判据不该被迫驱动
    /// Dispatcher。把「重建动作」与「写文案」都做成参数之后，判据喂一个**会抛的委托**
    /// 即可，而生产路径喂的是 <c>RebuildAllPanels</c> 与状态栏 setter。</para>
    /// </summary>
    internal static bool TryRebuildPanels(Action rebuild, Action<string> onError)
    {
        try
        {
            rebuild();
            return true;
        }
        catch (Exception ex)
        {
            onError(RebuildFailureText(ex));
            return false;
        }
    }

    /// <summary>
    /// 面板重建失败时的状态栏文案（登记 <b>#61u</b>，阶段 98）。
    ///
    /// <para>⚠ <b>刻意保留 <c>ex.Message</c>、不走 <c>ExternalText.FailureReason(ex)</c></b>：
    /// 这条接的是**面板重建内部**的意外异常（BCL / 内部原文），**产出方给不出码** ⇒
    /// 换出口只会落进回落分支、用户所见一字不变 —— 那是「**改名式假闭环**」（登记 #61 的
    /// 原始教训、#61b 的「C 类：不该本地化」）。本次的收益是「**异常不再被吞**」，不是换措辞。</para>
    /// </summary>
    internal static string RebuildFailureText(Exception ex) =>
        LocService.Format("status.reload.failed", ex.Message);
}
