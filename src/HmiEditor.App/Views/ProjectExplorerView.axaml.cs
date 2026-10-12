using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using HmiEditor.Core.Models.Alarm;
using HmiEditor.Core.Models.Trend;
using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Serialization;
using HmiEditor.Core.Services;

namespace HmiEditor.App.Views;

/// <summary>
/// 项目资源浏览器 —— 树形结构管理所有项目资源：
/// Screens（屏幕）、Variables（变量）、Alarms（报警）、Trends（趋势）、Scripts（脚本）。
/// 替代原有的 OutlinePanelView。
/// 支持右键增强菜单（≥5 项实用操作）、键盘快捷键、管理员可配置菜单项开关。
/// </summary>
public partial class ProjectExplorerView : UserControl
{
    private HmiProject? _project;
    private HmiScreen? _currentScreen;

    /// <summary>控件剪贴板（复制/粘贴）。</summary>
    private HmiControl? _clipboard;

    /// <summary>本地化服务。</summary>
    private static LocalizationService Loc => AppLocalization.Service;

    // 选择事件（兼容旧 OutlinePanel 接口）
    public event EventHandler<HmiControl?>? SelectionChanged;
    public event EventHandler<HmiControl>? DeleteRequested;

    /// <summary>「配置」节点激活 —— 参数为配置项 key（project_settings/communication/...）。</summary>
    public event EventHandler<string>? ConfigNodeActivated;

    public ProjectExplorerView()
    {
        InitializeComponent();

        // 转发事件到 DockLayoutState 聚合器，确保 Dock 重建后仍能到达 MainWindow
        SelectionChanged += (_, control) => DockLayoutState.RaiseExplorerSelectionChanged(this, control);
        DeleteRequested += (_, control) => DockLayoutState.RaiseExplorerDeleteRequested(this, control);
        ConfigNodeActivated += (_, key) => DockLayoutState.RaiseConfigNodeActivated(this, key);

        // 右键增强菜单：命中测试 + 动态构建 ContextMenu
        ProjectTree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>加载项目并刷新树。</summary>
    public void LoadProject(HmiProject project, HmiScreen currentScreen)
    {
        _project = project;
        _currentScreen = currentScreen;
        RebuildTree();
    }

    /// <summary>刷新当前屏幕的控件节点（同 OutlinePanel.Refresh）。</summary>
    public void Refresh(HmiScreen screen)
    {
        _currentScreen = screen;
        if (_project != null)
            RebuildTree();
    }

    public void Clear()
    {
        ProjectTree.Items.Clear();
    }

    private void RebuildTree()
    {
        ProjectTree.Items.Clear();

        if (_project == null) return;

        // ── 配置节点（工程级配置功能入口）──
        var configNode = new TreeViewItem
        {
            Header = AppLocalization.Service["project.explorer.config"],
            IsExpanded = true,
            Tag = "CONFIG_ROOT"
        };
        AddConfigItem(configNode, "project_settings", "project.explorer.config.project");
        AddConfigItem(configNode, "communication", "project.explorer.config.comm");
        AddConfigItem(configNode, "alarms", "project.explorer.config.alarms");
        AddConfigItem(configNode, "trends", "project.explorer.config.trends");
        AddConfigItem(configNode, "recipes", "project.explorer.config.recipes");
        AddConfigItem(configNode, "users", "project.explorer.config.users");
        AddConfigItem(configNode, "scripts", "project.explorer.config.scripts");
        AddConfigItem(configNode, "variables", "project.explorer.config.variables");
        AddConfigItem(configNode, "variable_import", "project.explorer.config.variable.import");
        ProjectTree.Items.Add(configNode);

        // ── 屏幕节点 ──
        var screenNode = new TreeViewItem
        {
            Header = $"{AppLocalization.Service["general.screens"]} ({_project.Screens.Count})",
            IsExpanded = true,
            Tag = "SCREENS_ROOT"
        };

        foreach (var screen in _project.Screens)
        {
            var screenItem = new TreeViewItem
            {
                Header = $"{screen.Name} ({screen.Width}x{screen.Height})",
                Tag = screen,
                IsExpanded = screen == _currentScreen
            };

            // 如果是当前屏幕，展开控件
            if (screen == _currentScreen)
            {
                foreach (var control in screen.Controls)
                {
                    screenItem.Items.Add(CreateControlNode(control));
                }
            }

            screenNode.Items.Add(screenItem);
        }
        ProjectTree.Items.Add(screenNode);

        // ── 变量节点 ──
        var varCount = _project.VariableManager?.Variables.Count ?? 0;
        var varNode = new TreeViewItem
        {
            Header = $"{AppLocalization.Service["var.tab.variable"]} ({varCount})",
            IsExpanded = false,
            Tag = "VARIABLES_ROOT"
        };

        if (_project.VariableManager != null)
        {
            foreach (var variable in _project.VariableManager.Variables)
            {
                varNode.Items.Add(new TreeViewItem
                {
                    Header = $"{variable.Name} ({variable.DataType})",
                    Tag = variable
                });
            }
        }
        ProjectTree.Items.Add(varNode);

        // ── 报警节点 ──
        var alarmCount = _project.AlarmGroups?.Sum(g => g.Alarms?.Count ?? 0) ?? 0;
        var alarmNode = new TreeViewItem
        {
            Header = AppLocalization.Service.Format("project.explorer.alarms", alarmCount),
            IsExpanded = false,
            Tag = "ALARMS_ROOT"
        };
        ProjectTree.Items.Add(alarmNode);

        // ── 趋势节点 ──
        var trendCount = _project.TrendConfigs?.Count ?? 0;
        var trendNode = new TreeViewItem
        {
            Header = AppLocalization.Service.Format("project.explorer.trends", trendCount),
            IsExpanded = false,
            Tag = "TRENDS_ROOT"
        };
        ProjectTree.Items.Add(trendNode);

        // ── 脚本节点 ──
        var scriptCount = _project.Scripts?.Count ?? 0;
        var scriptNode = new TreeViewItem
        {
            Header = $"{AppLocalization.Service["cross.ref.script"]} ({scriptCount})",
            IsExpanded = false,
            Tag = "SCRIPTS_ROOT"
        };

        if (_project.Scripts != null)
        {
            foreach (var script in _project.Scripts)
            {
                scriptNode.Items.Add(new TreeViewItem
                {
                    Header = $"{script.Name}",
                    Tag = script
                });
            }
        }
        ProjectTree.Items.Add(scriptNode);
    }

    /// <summary>添加一个配置子节点（Tag 为 "CONFIG:&lt;key&gt;"）。</summary>
    private static void AddConfigItem(TreeViewItem parent, string key, string titleKey)
    {
        parent.Items.Add(new TreeViewItem
        {
            Header = AppLocalization.Service[titleKey],
            Tag = "CONFIG:" + key
        });
    }

    private TreeViewItem CreateControlNode(HmiControl control)
    {
        var node = new TreeViewItem
        {
            // 名字显示本地化别名（默认名 `Button1` → `按钮1`）；工程里存的仍是中性名（登记 #61r，阶段 159）。
            // 类型也走单一来源（`ControlTypeTextKeys`）—— 阶段 160 之前这里直显英文枚举名。
            Header = $"{Services.ControlNameDefaults.Display(control)} ({Services.ControlTypeTextKeys.Describe(control.Type)})",
            Tag = control,
            IsExpanded = true
        };
        foreach (var child in control.Children)
        {
            node.Items.Add(CreateControlNode(child));
        }
        return node;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is TreeViewItem item)
        {
            // 「配置」节点 → 触发配置入口事件
            if (item.Tag is string tag && tag.StartsWith("CONFIG:", StringComparison.Ordinal))
            {
                ConfigNodeActivated?.Invoke(this, tag["CONFIG:".Length..]);
                BtnDelete.IsVisible = false;
                return;
            }

            // 传递 HmiControl 选中事件到外部
            if (item.Tag is HmiControl control)
            {
                SelectionChanged?.Invoke(this, control);
                BtnDelete.IsVisible = true;
                return;
            }
        }

        SelectionChanged?.Invoke(this, null);
        BtnDelete.IsVisible = false;
    }

    private void OnDeleteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProjectTree.SelectedItem is TreeViewItem item && item.Tag is HmiControl control)
        {
            DeleteRequested?.Invoke(this, control);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  右键增强菜单 + 键盘快捷键（管理员可配置菜单项开关）
    // ══════════════════════════════════════════════════════════════

    /// <summary>右键按下 → 命中测试选中节点并打开增强菜单。</summary>
    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed)
            return;

        var node = HitTestTreeItem(e.GetPosition(ProjectTree));
        if (node == null) return;

        node.IsSelected = true;
        ShowContextMenu(node);
        e.Handled = true;
    }

    private TreeViewItem? HitTestTreeItem(Point p)
    {
        var hit = ProjectTree.InputHitTest(p) as Visual;
        while (hit != null && hit is not TreeViewItem && !ReferenceEquals(hit, ProjectTree))
            hit = hit.GetVisualParent();
        return hit as TreeViewItem;
    }

    /// <summary>按节点类型动态构建右键增强菜单。</summary>
    private void ShowContextMenu(TreeViewItem node)
    {
        var explorer = App.SettingsService?.Explorer;
        if (explorer == null || !explorer.EnableContextMenu) return;

        var menu = new ContextMenu();
        switch (node.Tag)
        {
            case HmiScreen screen:
                BuildScreenMenu(menu, screen, explorer);
                break;
            case HmiControl control:
                BuildControlMenu(menu, control, explorer);
                break;
            case Variable variable:
                BuildVariableMenu(menu, variable, explorer);
                break;
            case HmiScript script:
                BuildScriptMenu(menu, script, explorer);
                break;
            case string tag when tag is "SCREENS_ROOT" or "VARIABLES_ROOT" or "ALARMS_ROOT" or "TRENDS_ROOT" or "SCRIPTS_ROOT":
                BuildGroupMenu(menu, tag, explorer);
                break;
            case string tag when tag.StartsWith("CONFIG:", StringComparison.Ordinal):
                menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxOpen], "Enter", true,
                    () => ConfigNodeActivated?.Invoke(this, tag["CONFIG:".Length..])));
                break;
        }

        if (menu.Items.Count == 0) return;

        node.ContextMenu = menu;
        menu.Open(node);
    }

    private void BuildScreenMenu(ContextMenu menu, HmiScreen screen, SettingsService.ExplorerSettings explorer)
    {
        if (explorer.ShowOpenScreen)
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxOpen], "Enter", true, () => OpenScreen(screen)));
        if (explorer.ShowStartupScreen)
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxStartup], "Ctrl+Shift+S", true, () => ToggleStartupScreen(screen)));
        if (explorer.ShowPublicPage)
        {
            var publicText = screen.IsPublicScreen ? Loc[Lang.ExplorerCtxPublicOff] : Loc[Lang.ExplorerCtxPublic];
            var publicItem = CtxItem(publicText, "Ctrl+Shift+P", true, () => TogglePublicScreen(screen));
            publicItem.IsChecked = screen.IsPublicScreen;
            menu.Items.Add(publicItem);
        }
        if (explorer.ShowRename)
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxRename], "F2", true, () => RenameScreen(screen)));
        if (explorer.ShowDuplicate)
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxDuplicate], "Ctrl+D", true, () => DuplicateScreen(screen)));
        if (explorer.ShowDelete)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxDelete], "Delete",
                _project != null && _project.Screens.Count > 1, () => DeleteScreen(screen), danger: true));
        }
    }

    private void BuildControlMenu(ContextMenu menu, HmiControl control, SettingsService.ExplorerSettings explorer)
    {
        if (explorer.ShowCopyPaste)
        {
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxCopy], "Ctrl+C", true, () => CopyControl(control)));
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxPaste], "Ctrl+V", _clipboard != null, () => PasteControl()));
        }
        if (explorer.ShowRename)
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxRename], "F2", true, () => RenameControl(control)));
        if (explorer.ShowMoveLayer)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxMoveUp], "Ctrl+Up", true, () => MoveLayer(control, -1)));
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxMoveDown], "Ctrl+Down", true, () => MoveLayer(control, 1)));
        }
        if (explorer.ShowDelete)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxDelete], "Delete", true, () => DeleteControl(control), danger: true));
        }
    }

    /// <summary>统一菜单项构造（文本 + 快捷键提示 + 启用状态 + 动作）。</summary>
    private static MenuItem CtxItem(string text, string? gesture, bool enabled, Action action, bool danger = false)
    {
        var item = new MenuItem
        {
            Header = text,
            IsEnabled = enabled,
            InputGesture = gesture != null ? KeyGesture.Parse(gesture) : null
        };
        if (danger) item.Classes.Add("danger");
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>分组根节点菜单（屏幕/变量/报警/趋势/脚本）：新建 + 打开 + 删除（分类根不可删除，禁用）。</summary>
    private void BuildGroupMenu(ContextMenu menu, string tag, SettingsService.ExplorerSettings explorer)
    {
        var category = tag switch
        {
            "SCREENS_ROOT" => "screen",
            "VARIABLES_ROOT" => "variables",
            "ALARMS_ROOT" => "alarms",
            "TRENDS_ROOT" => "trends",
            _ => "scripts"
        };
        if (explorer.ShowCreate)
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxNew], null, true,
                () => DockLayoutState.RaiseExplorerResourceAction(this, category, "create")));
        if (explorer.ShowOpenScreen)
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxOpen], "Enter", true,
                () => DockLayoutState.RaiseExplorerResourceAction(this, category, "open")));
        if (explorer.ShowDelete)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxDelete], null, false, () => { }, danger: true));
        }
    }

    /// <summary>变量子节点菜单：打开变量管理 + 删除变量。</summary>
    private void BuildVariableMenu(ContextMenu menu, Variable variable, SettingsService.ExplorerSettings explorer)
    {
        if (explorer.ShowOpenScreen)
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxOpen], "Enter", true,
                () => DockLayoutState.RaiseExplorerResourceAction(this, "variables", "open", variable)));
        if (explorer.ShowDelete)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxDelete], "Delete", true,
                () => DockLayoutState.RaiseExplorerResourceAction(this, "variables", "delete", variable), danger: true));
        }
    }

    /// <summary>脚本子节点菜单：打开脚本编辑器 + 删除脚本。</summary>
    private void BuildScriptMenu(ContextMenu menu, HmiScript script, SettingsService.ExplorerSettings explorer)
    {
        if (explorer.ShowOpenScreen)
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxOpen], "Enter", true,
                () => DockLayoutState.RaiseExplorerResourceAction(this, "scripts", "open", script)));
        if (explorer.ShowDelete)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(CtxItem(Loc[Lang.ExplorerCtxDelete], "Delete", true,
                () => DockLayoutState.RaiseExplorerResourceAction(this, "scripts", "delete", script), danger: true));
        }
    }

    /// <summary>TreeView 快捷键：F2 重命名 / Delete 删除 / Ctrl+C 复制 / Ctrl+V 粘贴 / Ctrl+D 复制屏幕 / Ctrl+Shift+P 公共页面。</summary>
    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (_project == null || ProjectTree.SelectedItem is not TreeViewItem node || node.Tag == null)
            return;

        switch (node.Tag)
        {
            case HmiScreen screen:
                if (e.Key == Key.F2) { e.Handled = true; RenameScreen(screen); }
                else if (e.Key == Key.Delete) { e.Handled = true; DeleteScreen(screen); }
                else if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { e.Handled = true; DuplicateScreen(screen); }
                else if (e.Key == Key.P && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                { e.Handled = true; TogglePublicScreen(screen); }
                break;
            case HmiControl control:
                if (e.Key == Key.F2) { e.Handled = true; RenameControl(control); }
                else if (e.Key == Key.Delete) { e.Handled = true; DeleteControl(control); }
                else if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { e.Handled = true; CopyControl(control); }
                else if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { e.Handled = true; PasteControl(); }
                break;
        }
    }

    // ── 动作实现 ──

    private void OpenScreen(HmiScreen screen)
        => DockLayoutState.RaiseExplorerScreenSwitchRequested(this, screen);

    private void ToggleStartupScreen(HmiScreen screen)
    {
        if (_project == null) return;
        _project.RuntimeConfig.BootScreenId = _project.RuntimeConfig.BootScreenId == screen.Id ? null : screen.Id;
        DockLayoutState.RaiseExplorerScreenStartupChanged(this, screen);
        ShowStatus(Loc.Format(Lang.ExplorerStartupDone, screen.Name));
        RebuildTree();
    }

    private void TogglePublicScreen(HmiScreen screen)
    {
        screen.IsPublicScreen = !screen.IsPublicScreen;
        DockLayoutState.RaiseExplorerScreenPublicToggled(this, screen);
        ShowStatus(Loc.Format(screen.IsPublicScreen ? Lang.ExplorerPublicDone : Lang.ExplorerPublicOffDone, screen.Name));
        RebuildTree();
    }

    /// <summary>
    /// 重命名画面。输入对话框与随后的树重建都可能失败（宿主窗口已关闭、
    /// 项目状态不一致），统一由 UiOperationGuard 兜底：失败写状态栏而不是静默。
    /// </summary>
    private void RenameScreen(HmiScreen screen) =>
        UiOperationGuard.Run(Lang.UiOpRenameScreen, async () =>
        {
            var name = await AppDialogs.PromptAsync(this, Loc[Lang.ExplorerRenameTitle], screen.Name);
            if (string.IsNullOrWhiteSpace(name) || name.Trim() == screen.Name) return;
            screen.Name = name.Trim();
            DockLayoutState.RaiseExplorerScreenRenamed(this, screen);
            ShowStatus(Loc.Format(Lang.ExplorerRenamedDone, screen.Name));
            RebuildTree();
        }, ShowStatus);

    private void DuplicateScreen(HmiScreen screen)
    {
        DockLayoutState.RaiseExplorerScreenDuplicated(this, screen);
        ShowStatus(Loc.Format(Lang.ExplorerDuplicatedDone, screen.Name));
    }

    private void DeleteScreen(HmiScreen screen) =>
        UiOperationGuard.Run(Lang.UiOpDeleteScreen, async () =>
        {
            if (_project == null || _project.Screens.Count <= 1)
            {
                ShowStatus(Loc[Lang.ExplorerMinScreen]);
                return;
            }
            if (!await AppDialogs.ConfirmAsync(this, Loc.Format(Lang.ExplorerDeleteConfirm, screen.Name)))
                return;
            DockLayoutState.RaiseExplorerScreenDeleted(this, screen);
            ShowStatus(Loc.Format(Lang.ExplorerDeletedDone, screen.Name));
        }, ShowStatus);

    private void CopyControl(HmiControl control)
    {
        _clipboard = JsonClone(control);
        ShowStatus(Loc[Lang.ExplorerCopyDone]);
    }

    private void PasteControl()
    {
        if (_clipboard == null)
        {
            ShowStatus(Loc[Lang.ExplorerNoClipboard]);
            return;
        }
        if (_currentScreen == null) return;

        var copy = JsonClone(_clipboard);
        copy.Id = Guid.NewGuid().ToString("N")[..8];
        copy.X += 12;
        copy.Y += 12;
        _currentScreen.Controls.Add(copy);
        DockLayoutState.RaiseCanvasControlPropertyChanged(this, EventArgs.Empty);
        ShowStatus(Loc[Lang.ExplorerPasteDone]);
        RebuildTree();
    }

    private void RenameControl(HmiControl control) =>
        UiOperationGuard.Run(Lang.UiOpRenameControl, async () =>
        {
            // 输入框预填、比较、状态提示**都用显示名**；落盘走 ToPersisted ⇒ 用户看到什么、写回的就是
            // 它的持久化形式（别名 → 中性名）。见 ControlNameDefaults（登记 #61r，阶段 159）。
            var shown = Services.ControlNameDefaults.Display(control);
            var name = await AppDialogs.PromptAsync(this, Loc[Lang.ExplorerRenameTitle], shown);
            if (string.IsNullOrWhiteSpace(name) || name.Trim() == shown) return;
            control.Name = Services.ControlNameDefaults.ToPersisted(name.Trim(), control.Type);
            DockLayoutState.RaiseCanvasControlPropertyChanged(this, EventArgs.Empty);
            ShowStatus(Loc.Format(Lang.ExplorerRenamedDone, Services.ControlNameDefaults.Display(control)));
            RebuildTree();
        }, ShowStatus);

    private void MoveLayer(HmiControl control, int direction)
    {
        var list = FindParentList(control);
        if (list == null) return;
        var idx = list.IndexOf(control);
        var target = idx + direction;
        if (target < 0 || target >= list.Count) return;
        list.Move(idx, target);
        DockLayoutState.RaiseExplorerControlLayerMoved(this, control, direction);
        ShowStatus(Loc[Lang.ExplorerMoveDone]);
    }

    private void DeleteControl(HmiControl control)
        => DeleteRequested?.Invoke(this, control);

    /// <summary>查找包含指定控件的兄弟列表（屏幕根或任一子容器）。</summary>
    private ObservableCollection<HmiControl>? FindParentList(HmiControl control)
    {
        if (_currentScreen == null) return null;
        if (_currentScreen.Controls.Contains(control)) return _currentScreen.Controls;
        foreach (var c in _currentScreen.Controls)
        {
            var r = FindInChildren(c.Children, control);
            if (r != null) return r;
        }
        return null;
    }

    private static ObservableCollection<HmiControl>? FindInChildren(ObservableCollection<HmiControl> list, HmiControl target)
    {
        if (list.Contains(target)) return list;
        foreach (var c in list)
        {
            var r = FindInChildren(c.Children, target);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>状态提示（右键菜单动作反馈）。</summary>
    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusText.IsVisible = true;
    }

    /// <summary>JSON 深拷贝（HmiControl 可由工程序列化器正常序列化）。</summary>
    private static T JsonClone<T>(T source) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(
            System.Text.Json.JsonSerializer.Serialize(source, JsonOptionsDefaults.Default),
            JsonOptionsDefaults.Default)!;
}
