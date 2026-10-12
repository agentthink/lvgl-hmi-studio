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

/// <summary>Faceplate 模板库、对齐工具、画布滚轮缩放、AI 助手事件。</summary>
/// <remarks>
/// 自 MainWindow.WireEvents()（原 992 行单方法）按域拆出，纯代码搬移：
/// 方法体逐行未改，WireEvents() 只保留对这几个分域方法的调用。
/// </remarks>
public partial class MainWindow
{
    private void WireFaceplateAndAiEvents()
    {
        // ── Faceplate 模板库事件 ──
        //
        // 本方法**不直订** FaceplatePanel 的任何实例事件
        //（Instantiate / CreateTemplate / EditTemplate / DeleteTemplate / AiTemplate 共 5 个）：
        // 该视图在自己的构造函数里把这 5 个事件**全部**转发到 DockLayoutState
        //（FaceplateLibraryView.axaml.cs:65-69），宿主订阅总线那一路即可
        //（Wiring.DockAggregate.cs 的 Faceplate*Requested）。
        //
        // 两路并存曾经的后果（缺陷 #46，实测读数）：`BtnAiTemplate` 点一次弹 **2 个**
        // AiTemplateDialog（+4 → 删实例直订前 +2 → 只剩总线 +1）、双击模板创建 **2 个**实例。
        // Avalonia/CLR 的 `+=` 都不去重，同一个处理逻辑被接两路就是执行两遍。
        //
        // 为什么**留总线、删直订**（而不是反过来）：直订订的是**构造 MainWindow 那一刻**的视图实例，
        // Dock 重建换实例后就失效了；而视图内部转发是随**新实例的构造**自动重建的，能自愈。
        // 即"注释里那句'确保 Dock 重建后仍能到达 MainWindow'要防的正是直订这一路"。

        // ── 菜单：Faceplate 操作 ──
        MenuCreateTemplate.Click += (_, _) => CreateTemplateFromSelection();
        MenuFaceplateEditor.Click += (_, _) =>
        {
            var selected = FaceplatePanel.GetSelectedTemplate();
            if (selected != null)
                OpenTemplateEditor(selected);
            else if (_templateManager.Templates.Count > 0)
                OpenTemplateEditor(_templateManager.Templates.First());
        };

        // ── 对齐工具 ──
        WireAlignmentButtons();

        // ── 画布滚轮缩放同步 ──
        // 不直订 CanvasPanel.ZoomLevelChanged：DesignCanvasView 在 ZoomLevelProperty 变化处
        // **同一个位置**既 invoke 实例事件、又 raise 总线（DesignCanvasView.axaml.cs:120-121），
        // 所以总线那一路（DockAggregate 的 CanvasZoomLevelChanged）已覆盖全部触发场景。

        // ── AI 助手事件 ──
        AiChatPanel.CurrentProject = _currentProject;
        AiChatPanel.AgentHost = _agentHost;
        // Provider 配置变更时同步 Agent 引擎（复用同一 Provider 实例）
        // ⚠ 这一条**必须直订**：AiChatPanelView 只把 Preview / Apply / TemplateApply / ScriptApply /
        // Cancel 这五件事转发到总线（AiChatPanelView.axaml.cs:77-81），**没有**转发 ConfigChanged
        // ⇒ 它是这里唯一的单路事件，删掉就没人处理了。
        AiChatPanel.ConfigChanged += (_, _) =>
        {
            var cfg = AiManager.Config;
            _agentHost?.SetProvider(AiManager.CurrentProvider, cfg.ForceXmlProtocol);
        };
        // 其余 5 个（Preview / Apply / TemplateApply / ScriptApply / Cancel）**不直订** ——
        // 视图已把事件转发到总线，消费在 Wiring.DockAggregate.cs 的 Ai*Requested。
        // 曾经两路并存 ⇒ 每次预览/应用都执行两遍（见本文件上方 Faceplate 段的说明与登记表 #46）。
    }
}
