using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Scripting;

namespace HmiEditor.App;

/// <summary>
/// Dock 布局全局状态 + 事件聚合。
/// 
/// [dock-blank-after-drag] 最终方案核心：
/// 1. 数据状态：CurrentProject、CurrentScreen 等供工厂创建的新 View 读取
/// 2. 事件聚合：View 的事件转发到此处，MainWindow 订阅一次即可，
///    无需在每次工厂创建新实例后重新连接事件
/// 
/// 所有面板事件均经此聚合器路由，确保 Dock 重建后事件仍能正确到达 MainWindow。
/// </summary>
public static class DockLayoutState
{
    // ── 数据状态 ──
    public static HmiProject? CurrentProject { get; set; }
    public static HmiScreen? CurrentScreen { get; set; }
    public static HmiControl? SelectedControl { get; set; }
    public static AssetLibrary? AssetLibrary { get; set; }

    // ── I10: 绘图工具栏状态 ──

    /// <summary>
    /// 当前激活的绘图工具（控件类型）。null = 普通选择/编辑模式。
    /// Dock 工厂会反复重建画布视图，此静态值保证新画布实例自动恢复工具状态。
    /// </summary>
    public static HmiControlType? ActiveDrawTool { get; private set; }

    /// <summary>绘图工具变化事件 —— MainWindow（工具栏按钮状态）与画布（光标/交互）订阅。</summary>
    public static event EventHandler<HmiControlType?>? DrawToolChanged;

    /// <summary>设置激活绘图工具并通知所有订阅者（工具栏高亮 + 画布交互同步）。</summary>
    public static void SetDrawTool(HmiControlType? tool)
    {
        if (ActiveDrawTool == tool) return;
        ActiveDrawTool = tool;
        DrawToolChanged?.Invoke(null, tool);
    }

    /// <summary>共享 HMI 图库资源库 —— 供 ResourceLibraryView 恢复数据。</summary>
    public static HmiResourceLibrary? ResourceLibrary { get; set; }
    public static IEnumerable<ControlTemplate>? Templates { get; set; }

    /// <summary>项目文件所在目录（绝对路径），用于解析 AssetItem.FilePath 等相对路径。null 表示未打开项目。</summary>
    public static string? ProjectDirectory { get; set; }

    // ── 事件聚合 ──

    // ToolboxView 事件
    public static event EventHandler<HmiControlType>? ToolItemSelected;

    // DesignCanvasView 事件
    public static event EventHandler<HmiControl?>? CanvasControlSelected;
    public static event EventHandler? CanvasControlPropertyChanged;
    public static event EventHandler<Controls.ControlDroppedEventArgs>? ControlDropped;
    public static event EventHandler<Controls.TemplateDroppedEventArgs>? TemplateDropped;
    public static event EventHandler<Controls.ResourceDroppedEventArgs>? ResourceDropped;
    public static event EventHandler<Controls.ControlDrawnEventArgs>? ControlDrawn;
    public static event EventHandler<bool>? CanvasPreviewModeChanged;
    public static event EventHandler<HmiControl>? CanvasPreviewControlClicked;
    public static event EventHandler<double>? CanvasZoomLevelChanged;

    // PropertyPanelView 事件
    public static event EventHandler<HmiControl>? PropertyChanged;
    public static event EventHandler<HmiScreen>? ScreenPropertyChanged;

    // LayerPanelView 事件
    public static event EventHandler<HmiControl?>? LayerSelectionChanged;
    public static event EventHandler? LayerChanged;

    // ProjectExplorerView 事件
    public static event EventHandler<HmiControl?>? ExplorerSelectionChanged;
    public static event EventHandler<HmiControl>? ExplorerDeleteRequested;

    // ProjectExplorerView 右键增强菜单动作
    public static event EventHandler<HmiScreen>? ExplorerScreenSwitchRequested;
    public static event EventHandler<HmiScreen>? ExplorerScreenStartupChanged;
    public static event EventHandler<HmiScreen>? ExplorerScreenPublicToggled;
    public static event EventHandler<HmiScreen>? ExplorerScreenRenamed;
    public static event EventHandler<HmiScreen>? ExplorerScreenDuplicated;
    public static event EventHandler<HmiScreen>? ExplorerScreenDeleted;
    public static event EventHandler<HmiControl>? ExplorerControlDuplicated;
    public static event EventHandler<(HmiControl Control, int Direction)>? ExplorerControlLayerMoved;

    /// <summary>项目浏览器资源动作 —— category: screen/variables/alarms/trends/scripts；action: create/open/delete；payload 为资源实例。</summary>
    public static event EventHandler<(string Category, string Action, object? Payload)>? ExplorerResourceAction;

    // FaceplateLibraryView 事件
    public static event EventHandler<ControlTemplate>? FaceplateInstantiateRequested;
    public static event EventHandler? FaceplateCreateTemplateRequested;
    public static event EventHandler<ControlTemplate>? FaceplateEditTemplateRequested;
    public static event EventHandler<ControlTemplate>? FaceplateDeleteTemplateRequested;
    public static event EventHandler? FaceplateAiTemplateRequested;

    // ResourceLibraryView 事件
    public static event EventHandler<HmiResourceItem>? ResourcePlaced;

    // AssetManagerView 事件
    public static event EventHandler<AssetItem?>? AssetSelected;
    public static event EventHandler<AssetItem>? FontSubsetRequested;
    public static event EventHandler? AiIconRequested;
    public static event EventHandler<AssetItem>? SvgApplyRequested;

    // NavigationFlowView 事件
    public static event EventHandler<string>? NavNodeActivated;
    public static event EventHandler? NavGraphChanged;

    // AiChatPanelView 事件
    public static event EventHandler<HmiScreen>? AiPreviewRequested;
    public static event EventHandler<HmiScreen>? AiApplyRequested;
    public static event EventHandler<ControlTemplate>? AiTemplateApplyRequested;
    public static event EventHandler<HmiScript>? AiScriptApplyRequested;
    public static event EventHandler? AiCancelRequested;

    /// <summary>项目管理器「配置」节点激活 —— 参数为配置项 key（project_settings/communication/alarms/...）。</summary>
    public static event EventHandler<string>? ConfigNodeActivated;

    /// <summary>画布右键菜单操作请求 —— 参数为操作类型枚举。</summary>
    public static event EventHandler<CanvasContextAction>? CanvasContextActionRequested;

    // ── Raise 方法 ──

    internal static void RaiseToolItemSelected(object sender, HmiControlType type)
        => ToolItemSelected?.Invoke(sender, type);

    internal static void RaiseCanvasControlSelected(object sender, HmiControl? control)
        => CanvasControlSelected?.Invoke(sender, control);

    internal static void RaiseCanvasControlPropertyChanged(object sender, EventArgs e)
        => CanvasControlPropertyChanged?.Invoke(sender, e);

    internal static void RaiseControlDropped(object sender, Controls.ControlDroppedEventArgs args)
        => ControlDropped?.Invoke(sender, args);

    internal static void RaiseTemplateDropped(object sender, Controls.TemplateDroppedEventArgs args)
        => TemplateDropped?.Invoke(sender, args);

    internal static void RaiseResourceDropped(object sender, Controls.ResourceDroppedEventArgs args)
        => ResourceDropped?.Invoke(sender, args);

    internal static void RaiseControlDrawn(object sender, Controls.ControlDrawnEventArgs args)
        => ControlDrawn?.Invoke(sender, args);

    internal static void RaiseCanvasPreviewModeChanged(object sender, bool isPreview)
        => CanvasPreviewModeChanged?.Invoke(sender, isPreview);

    internal static void RaiseCanvasPreviewControlClicked(object sender, HmiControl control)
        => CanvasPreviewControlClicked?.Invoke(sender, control);

    internal static void RaiseCanvasZoomLevelChanged(object sender, double zoom)
        => CanvasZoomLevelChanged?.Invoke(sender, zoom);

    internal static void RaisePropertyChanged(object sender, HmiControl control)
        => PropertyChanged?.Invoke(sender, control);

    internal static void RaiseScreenPropertyChanged(object sender, HmiScreen screen)
        => ScreenPropertyChanged?.Invoke(sender, screen);

    internal static void RaiseLayerSelectionChanged(object sender, HmiControl? control)
        => LayerSelectionChanged?.Invoke(sender, control);

    internal static void RaiseLayerChanged(object sender, EventArgs e)
        => LayerChanged?.Invoke(sender, e);

    internal static void RaiseExplorerSelectionChanged(object sender, HmiControl? control)
        => ExplorerSelectionChanged?.Invoke(sender, control);

    internal static void RaiseExplorerDeleteRequested(object sender, HmiControl control)
        => ExplorerDeleteRequested?.Invoke(sender, control);

    internal static void RaiseExplorerScreenSwitchRequested(object sender, HmiScreen screen)
        => ExplorerScreenSwitchRequested?.Invoke(sender, screen);

    internal static void RaiseExplorerScreenStartupChanged(object sender, HmiScreen screen)
        => ExplorerScreenStartupChanged?.Invoke(sender, screen);

    internal static void RaiseExplorerScreenPublicToggled(object sender, HmiScreen screen)
        => ExplorerScreenPublicToggled?.Invoke(sender, screen);

    internal static void RaiseExplorerScreenRenamed(object sender, HmiScreen screen)
        => ExplorerScreenRenamed?.Invoke(sender, screen);

    internal static void RaiseExplorerScreenDuplicated(object sender, HmiScreen screen)
        => ExplorerScreenDuplicated?.Invoke(sender, screen);

    internal static void RaiseExplorerScreenDeleted(object sender, HmiScreen screen)
        => ExplorerScreenDeleted?.Invoke(sender, screen);

    internal static void RaiseExplorerControlDuplicated(object sender, HmiControl control)
        => ExplorerControlDuplicated?.Invoke(sender, control);

    internal static void RaiseExplorerControlLayerMoved(object sender, HmiControl control, int direction)
        => ExplorerControlLayerMoved?.Invoke(sender, (control, direction));

    internal static void RaiseExplorerResourceAction(object sender, string category, string action, object? payload = null)
        => ExplorerResourceAction?.Invoke(sender, (category, action, payload));

    internal static void RaiseFaceplateInstantiateRequested(object sender, ControlTemplate template)
        => FaceplateInstantiateRequested?.Invoke(sender, template);

    internal static void RaiseFaceplateCreateTemplateRequested(object sender, EventArgs e)
        => FaceplateCreateTemplateRequested?.Invoke(sender, e);

    internal static void RaiseFaceplateEditTemplateRequested(object sender, ControlTemplate template)
        => FaceplateEditTemplateRequested?.Invoke(sender, template);

    internal static void RaiseFaceplateDeleteTemplateRequested(object sender, ControlTemplate template)
        => FaceplateDeleteTemplateRequested?.Invoke(sender, template);

    internal static void RaiseFaceplateAiTemplateRequested(object sender, EventArgs e)
        => FaceplateAiTemplateRequested?.Invoke(sender, e);

    internal static void RaiseResourcePlaced(object sender, HmiResourceItem item)
        => ResourcePlaced?.Invoke(sender, item);

    internal static void RaiseAssetSelected(object sender, AssetItem? asset)
        => AssetSelected?.Invoke(sender, asset);

    internal static void RaiseFontSubsetRequested(object sender, AssetItem asset)
        => FontSubsetRequested?.Invoke(sender, asset);

    internal static void RaiseAiIconRequested(object sender, EventArgs e)
        => AiIconRequested?.Invoke(sender, e);

    internal static void RaiseSvgApplyRequested(object sender, AssetItem asset)
        => SvgApplyRequested?.Invoke(sender, asset);

    internal static void RaiseNavNodeActivated(object sender, string screenId)
        => NavNodeActivated?.Invoke(sender, screenId);

    internal static void RaiseNavGraphChanged(object sender, EventArgs e)
        => NavGraphChanged?.Invoke(sender, e);

    internal static void RaiseAiPreviewRequested(object sender, HmiScreen screen)
        => AiPreviewRequested?.Invoke(sender, screen);

    internal static void RaiseAiApplyRequested(object sender, HmiScreen screen)
        => AiApplyRequested?.Invoke(sender, screen);

    internal static void RaiseAiTemplateApplyRequested(object sender, ControlTemplate template)
        => AiTemplateApplyRequested?.Invoke(sender, template);

    internal static void RaiseAiScriptApplyRequested(object sender, HmiScript script)
        => AiScriptApplyRequested?.Invoke(sender, script);

    internal static void RaiseAiCancelRequested(object sender, EventArgs e)
        => AiCancelRequested?.Invoke(sender, e);

    internal static void RaiseConfigNodeActivated(object sender, string configKey)
        => ConfigNodeActivated?.Invoke(sender, configKey);

    internal static void RaiseCanvasContextAction(object sender, CanvasContextAction action)
        => CanvasContextActionRequested?.Invoke(sender, action);
}

/// <summary>画布右键菜单操作类型枚举。</summary>
public enum CanvasContextAction
{
    Copy,
    Paste,
    Duplicate,
    Delete,
    AlignLeft,
    AlignCenterH,
    AlignRight,
    AlignTop,
    AlignCenterV,
    AlignBottom,
    SameSize,
    DistributeH,
    DistributeV,
    Group,
    Ungroup,
}
