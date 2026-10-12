using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HmiEditor.App.Services;

/// <summary>
/// Dock 事件路由诊断 —— 开发期排障工具，由调试菜单手动触发。
///
/// 为什么单独成文件：这段代码约 240 行，会重建 Dock 布局并模拟触发关键事件，
/// 属于诊断而非产品流程；此前内联在 MainWindow 里，使本已 4900+ 行的主窗口继续膨胀。
/// 行为与内联版本完全一致，只是把宿主依赖（状态栏写入、Dock 重建、对话框所有者）
/// 显式参数化，便于单独阅读与后续按需下线。
/// </summary>
internal static class DockEventRoutingDiagnostics
{
    /// <param name="owner">报告对话框的所有者窗口。</param>
    /// <param name="setStatus">写状态栏文本（如 <c>msg =&gt; StatusText.Text = msg</c>）。</param>
    /// <param name="rebuildLayout">触发 Dock 布局重建（MainWindow.OnDockLayoutChanged）。</param>
    public static async Task RunAsync(Window owner, Action<string> setStatus, Action rebuildLayout)
    {
        var report = new List<string>();
        report.Add("╔════════════════════════════════════════════════════╗");
        report.Add("║     Dock 事件路由诊断报告                           ║");
        report.Add("╚════════════════════════════════════════════════════╝");
        report.Add("");
        report.Add($"诊断时间: {DateTime.Now:O}");
        report.Add("");

        // ── Phase 1: 重建前检查活跃视图 ──
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.Add("Phase 1: 重建前 — 活跃视图检查");
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        var panelIds = new[]
        {
            ("Toolbox",        "ToolboxView"),
            ("Faceplate",      "FaceplateLibraryView"),
            ("Canvas",         "DesignCanvasView"),
            ("ProjectExplorer","ProjectExplorerView"),
            ("Property",       "PropertyPanelView"),
            ("Layers",         "LayerPanelView"),
            ("Assets",         "AssetManagerView"),
            ("Navigation",     "NavigationFlowView"),
            ("AuditLog",       "AuditLogView"),
            ("CrossReference","CrossReferenceView"),
            ("AiChat",         "AiChatPanelView"),
        };

        int beforeCount = 0;
        foreach (var (id, typeName) in panelIds)
        {
            var view = ViewLocator.GetActiveView(id);
            var ok = view != null && view.GetType().Name == typeName;
            report.Add($"  {(ok ? "✅" : "❌")} {id,-16} → {view?.GetType().Name ?? "(null)"}");
            if (ok) beforeCount++;
        }
        report.Add($"  小计: {beforeCount}/{panelIds.Length} 个面板活跃");
        report.Add("");

        // ── Phase 2: 检查 DockLayoutState 事件订阅者 ──
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.Add("Phase 2: DockLayoutState 事件订阅者检查");
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        var eventChecks = new (string Name, bool HasSubscribers)[]
        {
            ("ToolItemSelected",             IsEventSubscribed(nameof(DockLayoutState.ToolItemSelected))),
            ("CanvasControlSelected",         IsEventSubscribed(nameof(DockLayoutState.CanvasControlSelected))),
            ("CanvasControlPropertyChanged",  IsEventSubscribed(nameof(DockLayoutState.CanvasControlPropertyChanged))),
            ("ControlDropped",                IsEventSubscribed(nameof(DockLayoutState.ControlDropped))),
            ("CanvasPreviewModeChanged",      IsEventSubscribed(nameof(DockLayoutState.CanvasPreviewModeChanged))),
            ("CanvasPreviewControlClicked",   IsEventSubscribed(nameof(DockLayoutState.CanvasPreviewControlClicked))),
            ("CanvasZoomLevelChanged",        IsEventSubscribed(nameof(DockLayoutState.CanvasZoomLevelChanged))),
            ("PropertyChanged",              IsEventSubscribed(nameof(DockLayoutState.PropertyChanged))),
            ("LayerSelectionChanged",         IsEventSubscribed(nameof(DockLayoutState.LayerSelectionChanged))),
            ("LayerChanged",                  IsEventSubscribed(nameof(DockLayoutState.LayerChanged))),
            ("ExplorerSelectionChanged",      IsEventSubscribed(nameof(DockLayoutState.ExplorerSelectionChanged))),
            ("ExplorerDeleteRequested",      IsEventSubscribed(nameof(DockLayoutState.ExplorerDeleteRequested))),
            ("FaceplateInstantiateRequested", IsEventSubscribed(nameof(DockLayoutState.FaceplateInstantiateRequested))),
            ("FaceplateCreateTemplateRequested", IsEventSubscribed(nameof(DockLayoutState.FaceplateCreateTemplateRequested))),
            ("FaceplateEditTemplateRequested",  IsEventSubscribed(nameof(DockLayoutState.FaceplateEditTemplateRequested))),
            ("FaceplateDeleteTemplateRequested", IsEventSubscribed(nameof(DockLayoutState.FaceplateDeleteTemplateRequested))),
            ("FaceplateAiTemplateRequested",     IsEventSubscribed(nameof(DockLayoutState.FaceplateAiTemplateRequested))),
            ("AssetSelected",                IsEventSubscribed(nameof(DockLayoutState.AssetSelected))),
            ("FontSubsetRequested",           IsEventSubscribed(nameof(DockLayoutState.FontSubsetRequested))),
            ("AiIconRequested",               IsEventSubscribed(nameof(DockLayoutState.AiIconRequested))),
            ("SvgApplyRequested",             IsEventSubscribed(nameof(DockLayoutState.SvgApplyRequested))),
            ("NavNodeActivated",              IsEventSubscribed(nameof(DockLayoutState.NavNodeActivated))),
            ("NavGraphChanged",               IsEventSubscribed(nameof(DockLayoutState.NavGraphChanged))),
            ("AiPreviewRequested",            IsEventSubscribed(nameof(DockLayoutState.AiPreviewRequested))),
            ("AiApplyRequested",              IsEventSubscribed(nameof(DockLayoutState.AiApplyRequested))),
            ("AiTemplateApplyRequested",      IsEventSubscribed(nameof(DockLayoutState.AiTemplateApplyRequested))),
            ("AiScriptApplyRequested",        IsEventSubscribed(nameof(DockLayoutState.AiScriptApplyRequested))),
            ("AiCancelRequested",             IsEventSubscribed(nameof(DockLayoutState.AiCancelRequested))),
        };

        int subscribedCount = 0;
        foreach (var (name, has) in eventChecks)
        {
            report.Add($"  {(has ? "✅" : "❌")} {name,-35} {(has ? "已订阅" : "未订阅!")}");
            if (has) subscribedCount++;
        }
        report.Add($"  小计: {subscribedCount}/{eventChecks.Length} 个事件已订阅");
        report.Add("");

        // ── Phase 3: 触发 Dock 重建 ──
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.Add("Phase 3: 触发 Dock 重建 (OnDockLayoutChanged)");
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.Add("  正在重建...");
        setStatus("Dock 事件路由诊断中...");

        rebuildLayout();
        // 等待双阶段重建完成
        await Task.Delay(500);

        report.Add("  重建完成");
        report.Add("");

        // ── Phase 4: 重建后验证活跃视图 ──
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.Add("Phase 4: 重建后 — 活跃视图检查");
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        int afterCount = 0;
        foreach (var (id, typeName) in panelIds)
        {
            var view = ViewLocator.GetActiveView(id);
            var ok = view != null && view.GetType().Name == typeName;
            report.Add($"  {(ok ? "✅" : "❌")} {id,-16} → {view?.GetType().Name ?? "(null)"}");
            if (ok) afterCount++;
        }
        report.Add($"  小计: {afterCount}/{panelIds.Length} 个面板活跃");
        report.Add("");

        // ── Phase 5: 重建后验证事件订阅仍存在 ──
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.Add("Phase 5: 重建后 — 事件订阅持久性检查");
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        int afterSubCount = 0;
        foreach (var (name, _) in eventChecks)
        {
            var has = IsEventSubscribed(name);
            report.Add($"  {(has ? "✅" : "❌")} {name,-35} {(has ? "仍已订阅" : "订阅丢失!")}");
            if (has) afterSubCount++;
        }
        report.Add($"  小计: {afterSubCount}/{eventChecks.Length} 个事件仍已订阅");
        report.Add("");

        // ── Phase 6: 模拟触发关键事件 ──
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.Add("Phase 6: 模拟触发关键事件 (验证回调执行)");
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        var eventResults = new List<(string, bool, string)>();

        // 模拟画布选中
        try
        {
            var before = DockLayoutState.SelectedControl;
            DockLayoutState.RaiseCanvasControlSelected(owner, null);
            eventResults.Add(("CanvasControlSelected", true, "触发成功"));
        }
        catch (Exception ex) { eventResults.Add(("CanvasControlSelected", false, ex.Message)); }

        // 模拟属性面板属性变化
        try
        {
            // 不传 control 避免 null 异常
            eventResults.Add(("PropertyChanged(订阅检查)", IsEventSubscribed(nameof(DockLayoutState.PropertyChanged)), "静态事件仍存活"));
        }
        catch (Exception ex) { eventResults.Add(("PropertyChanged", false, ex.Message)); }

        // 模拟层面板选中
        try
        {
            DockLayoutState.RaiseLayerSelectionChanged(owner, null);
            eventResults.Add(("LayerSelectionChanged", true, "触发成功"));
        }
        catch (Exception ex) { eventResults.Add(("LayerSelectionChanged", false, ex.Message)); }

        // 模拟控件树选中
        try
        {
            DockLayoutState.RaiseExplorerSelectionChanged(owner, null);
            eventResults.Add(("ExplorerSelectionChanged", true, "触发成功"));
        }
        catch (Exception ex) { eventResults.Add(("ExplorerSelectionChanged", false, ex.Message)); }

        // 模拟导航图变更
        try
        {
            DockLayoutState.RaiseNavGraphChanged(owner, EventArgs.Empty);
            eventResults.Add(("NavGraphChanged", true, "触发成功"));
        }
        catch (Exception ex) { eventResults.Add(("NavGraphChanged", false, ex.Message)); }

        // 模拟资源选中
        try
        {
            DockLayoutState.RaiseAssetSelected(owner, null);
            eventResults.Add(("AssetSelected", true, "触发成功"));
        }
        catch (Exception ex) { eventResults.Add(("AssetSelected", false, ex.Message)); }

        // 模拟缩放变化
        try
        {
            DockLayoutState.RaiseCanvasZoomLevelChanged(owner, 1.0);
            eventResults.Add(("CanvasZoomLevelChanged", true, "触发成功"));
        }
        catch (Exception ex) { eventResults.Add(("CanvasZoomLevelChanged", false, ex.Message)); }

        foreach (var (name, ok, msg) in eventResults)
        {
            report.Add($"  {(ok ? "✅" : "❌")} {name,-35} {msg}");
        }
        report.Add("");

        // ── 汇总 ──
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.Add("诊断汇总");
        report.Add("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.Add($"  重建前面板活跃:    {beforeCount}/{panelIds.Length}");
        report.Add($"  重建后面板活跃:    {afterCount}/{panelIds.Length}");
        report.Add($"  事件订阅(重建前): {subscribedCount}/{eventChecks.Length}");
        report.Add($"  事件订阅(重建后): {afterSubCount}/{eventChecks.Length}");
        report.Add("");

        var allPass = beforeCount == panelIds.Length
                   && afterCount == panelIds.Length
                   && subscribedCount == eventChecks.Length
                   && afterSubCount == eventChecks.Length;
        report.Add(allPass ? "  ✅ 全部通过 — Dock 重建后事件路由正常" : "  ⚠️ 存在问题 — 请检查上方标记 ❌ 的项");

        // 显示报告
        var reportText = string.Join("\n", report);
        setStatus(allPass ? "Dock 事件路由诊断: 全部通过 ✅" : "Dock 事件路由诊断: 存在问题 ⚠️");

        var dlg = new Window
        {
            Title = "Dock 事件路由诊断报告",
            Width = 720,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(26, 35, 50)),
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = reportText,
                    FontFamily = Avalonia.Media.FontFamily.Parse("Cascadia Code, Consolas, monospace"),
                    FontSize = 12,
                    Foreground = Avalonia.Media.Brushes.LightGreen,
                    Padding = new Thickness(16),
                }
            }
        };
        await dlg.ShowDialog(owner);
    }

    /// <summary>通过反射检查 DockLayoutState 的指定事件是否有订阅者（字段为 null 表示无订阅者）。</summary>
    private static bool IsEventSubscribed(string eventName)
    {
        try
        {
            var field = typeof(DockLayoutState).GetField(eventName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (field == null) return false;
            var value = field.GetValue(null);
            // 事件字段为 null 表示无订阅者
            return value != null;
        }
        catch
        {
            return false;
        }
    }
}
