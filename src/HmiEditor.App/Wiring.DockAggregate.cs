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
using HmiEditor.App.Resources;

namespace HmiEditor.App;

/// <summary>DockLayoutState 聚合事件订阅（Dock 重建后仍能到达 MainWindow）。</summary>
/// <remarks>
/// 自 MainWindow.WireEvents()（原 992 行单方法）按域拆出，纯代码搬移：
/// 方法体逐行未改，WireEvents() 只保留对这几个分域方法的调用。
/// </remarks>
public partial class MainWindow
{
    private void WireDockAggregateEvents()
    {
        // ════════════════════════════════════════════════════════════════════
        // DockLayoutState 聚合事件订阅（**本窗口唯一的事件消费路径**）
        // 确保 Dock 重建后（ViewLocator 创建新实例），事件仍能正确到达 MainWindow：
        // 面板视图在**自己的构造函数里**把内部事件转发到 DockLayoutState（新实例构造即重建转发），
        // 本窗口只订阅总线这一路。
        //
        // ⚠ 历史坑（阶段 52 实测）：这里曾经与「宿主直订面板实例」**并行存在**，注释当时写的是
        //   "每个聚合事件与上方对应的实例事件执行相同的处理逻辑，通过防重入标志避免双触发" ——
        //   但**只有 AiIconRequested 一处真做了防重入标志**，其余 13 处全是纯双触发：
        //   `BtnAiTemplate` 点一次弹 **2 个** AiTemplateDialog（实测），双击模板会创建 **2 个**实例。
        //   而且两份副本已经开始**走样**：本文件原来的 ExplorerSelectionChanged 漏了
        //   `SetTransformButtonsVisible(control != null)`（靠实例直订那一份兜着，删直订前必须先补齐）。
        //   ⇒ 实例直订一律删除；消费方只剩本文件。判据见 tests 的 DockPanelInvocationUiTests
        //   （Popup 桶断言**窗口数量**）与 docs/2026-09-17-open-items-register.md 的 #43/#46。
        // ════════════════════════════════════════════════════════════════════

        // 资源管理器
        DockLayoutState.AssetSelected += (_, asset) =>
        {
            if (asset != null)
                StatusText.Text = LocService.Format("status.asset.info", asset.Name, asset.Type, AssetRow.FormatBytes(asset.SizeBytes));
        };
        DockLayoutState.FontSubsetRequested += (_, asset) =>
        {
            if (_currentProject == null)
            {
                StatusText.Text = LocService["gen.create.or.open.project"];
                return;
            }
            var dialog = new Views.FontSubsetView(_currentProject);
            dialog.ShowDialog(this);
        };
        DockLayoutState.AiIconRequested += async (_, _) => await OpenAiIconDialogAsync();
        DockLayoutState.SvgApplyRequested += async (_, asset) =>
        {
            var selected = CanvasPanel.Canvas.GetSelectedControls();
            var svgCtrl = selected.FirstOrDefault(c => c.Type == HmiControlType.Svg);
            if (svgCtrl == null) { StatusText.Text = LocService["status.select.svg.first"]; return; }
            try
            {
                var svgText = await System.IO.File.ReadAllTextAsync(asset.FilePath);
                var (doc, error) = SvgParser.Parse(svgText);
                if (doc == null || doc.Shapes.Count == 0)
                {
                    // 返回值档（登记 #61a，阶段 87）：error 是「码 + 中文回落」，渲染必须走
                    // 唯一出口 ExternalText.Describe —— 直通 error.Message 会让英文界面下这一半是中文。
                    // error == null 只出现在"解析成功但零形状"那一档（空 <svg/> 是合法输入）。
                    StatusText.Text = LocService.Format("asset.svg.parse.fail",
                        error is null ? LocService["asset.svg.no.shapes.hint"] : ExternalText.Describe(error));
                    return;
                }
                svgCtrl.SvgShapes = doc.Shapes;
                svgCtrl.SvgViewBoxWidth = doc.ViewBoxWidth;
                svgCtrl.SvgViewBoxHeight = doc.ViewBoxHeight;
                CanvasPanel.Refresh();
                SetUnsavedChanges(true);
                StatusText.Text = LocService.Format("status.svg.applied", asset.Name, doc.Shapes.Count);
            }
            catch (Exception ex)
            {
                // 三实参出口（#61a 尾巴，阶段 101）：本 try 的第一行就是 asset.FilePath 的**读盘**
                // —— 那是真 IO（文件在导入与本步之间被删／权限被改），BCL 类型有码
                // （SystemFailureCodes，阶段 85）⇒ 走「操作名 + 目标文件 + 码化原因」出口，
                // 用户同时读到「哪个文件」与「为什么（他的语言）」。
                StatusText.Text = UiOperationGuard.FailureDetail(Lang.UiOpApplySvg, asset.FilePath, ex);
            }
        };

        // 导航流
        DockLayoutState.NavNodeActivated += (_, screenId) =>
        {
            var screen = _currentProject.Screens.FirstOrDefault(s => s.Id == screenId);
            if (screen != null) SwitchToScreen(screen);
        };
        DockLayoutState.NavGraphChanged += (_, _) => SetUnsavedChanges(true);

        // 控件树
        DockLayoutState.ExplorerSelectionChanged += (_, control) =>
        {
            PropertyPanel.BindControl(control);
            BtnDeleteControl.IsVisible = control != null;
            // 补漂移：实例直订那一份有此行，本文件此前漏了（两份副本走样，见方法头注释）
            SetTransformButtonsVisible(control != null);
            SetAlignmentGroupVisible(control != null);
            UpdateStatusBar();
        };
        DockLayoutState.ExplorerDeleteRequested += (_, control) =>
        {
            var index = _currentScreen.Controls.IndexOf(control);
            if (index < 0) return;
            _undoRedoManager.Execute(new SnapshotCommand(
                AppLocalization.Service.Format(Lang.UndoDeleteControlNamed,
                    Services.ControlNameDefaults.Display(control)),
                () =>
                {
                    if (!_currentScreen.Controls.Contains(control))
                        _currentScreen.Controls.Insert(Math.Min(index, _currentScreen.Controls.Count), control);
                },
                () => _currentScreen.Controls.Remove(control)));
            PropertyPanel.BindControl(null);
            ProjectExplorerPanel.Refresh(_currentScreen);
            LayerPanel.Refresh();
            CanvasPanel.ClearSelection();
            CanvasPanel.Refresh();
            BtnDeleteControl.IsVisible = false;
            SetUnsavedChanges(true);
            UpdateStatusBar();
        };

        // ── 项目浏览器右键增强菜单动作 ──
        DockLayoutState.ExplorerScreenSwitchRequested += (_, screen) => SwitchToScreen(screen);
        DockLayoutState.ExplorerScreenStartupChanged += (_, screen) =>
        {
            SetUnsavedChanges(true);
            PropertyPanel.BindScreen(screen);
            UpdateStatusBar();
        };
        DockLayoutState.ExplorerScreenPublicToggled += (_, screen) =>
        {
            SetUnsavedChanges(true);
            PropertyPanel.BindScreen(screen);
            ProjectExplorerPanel.Refresh(_currentScreen);
            UpdateStatusBar();
        };
        DockLayoutState.ExplorerScreenRenamed += (_, screen) =>
        {
            SetUnsavedChanges(true);
            PropertyPanel.BindScreen(screen);
            ProjectExplorerPanel.Refresh(_currentScreen);
            UpdateStatusBar();
        };
        DockLayoutState.ExplorerScreenDuplicated += (_, screen) =>
        {
            var clone = JsonClone(screen);
            clone.Id = Guid.NewGuid().ToString("N")[..8];
            clone.Name = screen.Name + LocService["explorer.duplicated.suffix"];
            _currentProject.Screens.Add(clone);
            SetUnsavedChanges(true);
            ProjectExplorerPanel.Refresh(_currentScreen);
            UpdateStatusBar();
        };
        DockLayoutState.ExplorerScreenDeleted += (_, screen) =>
        {
            if (_currentProject.Screens.Count <= 1) return;
            _currentProject.Screens.Remove(screen);
            if (_currentScreen == screen)
            {
                var next = _currentProject.Screens.FirstOrDefault();
                if (next != null) SwitchToScreen(next);
            }
            else
            {
                ProjectExplorerPanel.Refresh(_currentScreen);
            }
            SetUnsavedChanges(true);
            UpdateStatusBar();
        };
        DockLayoutState.ExplorerControlLayerMoved += (_, _) =>
        {
            SetUnsavedChanges(true);
            CanvasPanel.Refresh();
            LayerPanel.Refresh();
            UpdateStatusBar();
        };
        DockLayoutState.ExplorerResourceAction += (_, args) =>
        {
            var (category, action, payload) = args;
            switch (category)
            {
                case "screen":
                    OpenScreenManagerDialog();
                    break;
                case "variables":
                    if (action == "delete" && payload is Variable variable)
                    {
                        _currentProject.VariableManager.Variables.Remove(variable);
                        ProjectExplorerPanel.Refresh(_currentScreen);
                        SetUnsavedChanges(true);
                        UpdateStatusBar();
                    }
                    else
                    {
                        OpenVariableManagerDialog();
                    }
                    break;
                case "alarms":
                    OpenAlarmConfigDialog();
                    break;
                case "trends":
                    OpenTrendConfigDialog();
                    break;
                case "scripts":
                    if (action == "delete" && payload is HmiScript script)
                    {
                        _currentProject.Scripts.Remove(script);
                        ProjectExplorerPanel.Refresh(_currentScreen);
                        SetUnsavedChanges(true);
                        UpdateStatusBar();
                    }
                    else
                    {
                        OpenScriptEditorDialog();
                    }
                    break;
            }
        };

        // 画布预览/缩放
        DockLayoutState.CanvasPreviewModeChanged += (_, isPreview) =>
        {
            if (isPreview) _previewRuntime.Start(); else _previewRuntime.Stop();
        };
        DockLayoutState.CanvasPreviewControlClicked += (_, control) =>
        {
            if (_previewRuntime.ExecuteAction(control, "OnClick")) return;
            StatusText.Text = LocService.Format("status.preview.no.action",
                Services.ControlNameDefaults.Display(control));
        };
        DockLayoutState.CanvasZoomLevelChanged += (_, level) =>
        {
            _zoomLevel = level;
            ZoomLevelText.Text = $"{(_zoomLevel * 100):F0}%";
        };

        // Faceplate 模板库
        DockLayoutState.FaceplateInstantiateRequested += (_, template) =>
        {
            var canvas = CanvasPanel.Canvas;
            var (centerX, centerY) = CanvasCenter(canvas);
            var instance = _templateManager.CreateFaceplateInstance(template, centerX, centerY);
            _undoRedoManager.Execute(new AddControlCommand(_currentScreen.Controls, instance));
            CanvasPanel.Canvas.ClearSelection();
            CanvasPanel.Refresh();
            ProjectExplorerPanel.Refresh(_currentScreen);
            SetUnsavedChanges(true);
            UpdateStatusBar();
        };
        DockLayoutState.FaceplateCreateTemplateRequested += (_, _) => CreateTemplateFromSelection();
        DockLayoutState.FaceplateEditTemplateRequested += (_, template) => OpenTemplateEditor(template);
        DockLayoutState.FaceplateDeleteTemplateRequested += (_, template) =>
        {
            _templateManager.RemoveTemplate(template);
            SetUnsavedChanges(true);
        };
        DockLayoutState.FaceplateAiTemplateRequested += async (_, _) =>
        {
            var dialog = new AiTemplateDialog(AiManager) { CurrentProject = _currentProject };
            var template = await dialog.ShowDialog<ControlTemplate?>(this);
            if (template is not null)
            {
                _templateManager.AddPredefined(new[] { template });
                        FaceplatePanel.LoadTemplates(_templateManager.Templates);
                SetUnsavedChanges(true);
            }
        };

        // AI 助手
        DockLayoutState.AiPreviewRequested += (_, screen) => OnAiPreviewRequested(null, screen);
        DockLayoutState.AiApplyRequested += (_, screen) => OnAiApplyRequested(null, screen);
        DockLayoutState.AiTemplateApplyRequested += (_, template) => OnAiTemplateApplyRequested(null, template);
        DockLayoutState.AiScriptApplyRequested += (_, script) => OnAiScriptApplyRequested(null, script);
        DockLayoutState.AiCancelRequested += (_, _) => OnAiCancelRequested();

        // 项目管理器「配置」节点 → 打开对应配置窗口
        DockLayoutState.ConfigNodeActivated += (_, key) => OpenConfigWindow(key);

        // R1-T2: 面板折叠由 Dock 原生 Pin/AutoHide 替代，WirePanelCollapse 已移除
    }
}
