using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;
using System.Linq;

namespace HmiEditor.App.Views;

public partial class DesignCanvasView : UserControl
{
    public event EventHandler<double>? ZoomLevelChanged;

    /// <summary>控件拖放事件 —— 从工具箱拖拽到画布时触发。</summary>
    public event EventHandler<Controls.ControlDroppedEventArgs>? ControlDropped;

    /// <summary>模板拖放事件 —— 从模板库拖拽到画布时触发。</summary>
    public event EventHandler<Controls.TemplateDroppedEventArgs>? TemplateDropped;

    /// <summary>资源拖放事件 —— 从图库拖拽资源到画布时触发。</summary>
    public event EventHandler<Controls.ResourceDroppedEventArgs>? ResourceDropped;

    /// <summary>
    /// R2-T2: 预览模式切换事件 —— MainWindow 据此启停 VariableSimulator
    /// 并向 CanvasLayer 注入 PreviewValueProvider。
    /// </summary>
    public event EventHandler<bool>? PreviewModeChanged;

    /// <summary>预览模式控件点击事件 —— 串联底层 DesignCanvas 的事件。</summary>
    public event EventHandler<HmiControl>? PreviewControlClicked;

    /// <summary>I10: 绘图提交事件 —— 串联底层 DesignCanvas 的事件（供 MainWindow 撤销/刷新）。</summary>
    public event EventHandler<Controls.ControlDrawnEventArgs>? ControlDrawn;

    /// <summary>I10: 绘图工具变化订阅委托（Detached 时对称解绑，见 OnDetachedFromVisualTree）。</summary>
    private EventHandler<HmiControlType?>? _drawToolChangedHandler;

    /// <summary>应用级设置变化订阅委托（Detached 时对称解绑）。</summary>
    private EventHandler<string>? _settingChangedHandler;

    /// <summary>注入的设置服务；null 时运行时回退 <see cref="App.SettingsService"/>。</summary>
    private readonly SettingsService? _settingsService;

    /// <summary>本视图实际使用的设置服务（注入优先，其次全局）。</summary>
    private SettingsService? Settings => _settingsService ?? App.SettingsService;

    /// <summary>网格点阵层（供接线测试断言外观，不对外暴露）。</summary>
    internal SnapGrid GridControl => GridLayer;

    // R8-FIX: 防抖标记
    private bool _sizeChangedScheduled;

    // ── R2-T2: 预览模式软键盘 ──
    /// <summary>当前软键盘聚焦的输入框控件（预览模式下点击 TextArea 时设置）。</summary>
    private HmiControl? _keyboardTarget;
    /// <summary>键盘大写锁定状态。</summary>
    private bool _kbdShiftOn;

    public DesignCanvasView() : this(null)
    {
    }

    public DesignCanvasView(UndoRedoManager? undoRedoManager) : this(undoRedoManager, null)
    {
    }

    /// <param name="undoRedoManager">撤销/重做管理器，可为 null。</param>
    /// <param name="settingsService">
    /// 应用级设置服务。传 null 表示"运行时从 <see cref="App.SettingsService"/> 取" ——
    /// 生产路径走这条（Dock 工厂用无参构造）；测试注入自己的一份以便断言接线。
    /// </param>
    public DesignCanvasView(UndoRedoManager? undoRedoManager, SettingsService? settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        if (undoRedoManager != null)
        {
            CanvasLayer.UndoRedoManager = undoRedoManager;
        }

        // R8-FIX: Dock 重组后可见性变化时自愈（重绘画布）。
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty)
            {
                // 不再直接调用——用 Dispatcher 延迟到布局完成后
                Dispatcher.UIThread.Post(() =>
                {
                    if (IsVisible && Parent != null) RefreshForDock();
                }, DispatcherPriority.Loaded);
            }
        };
        // Dock 重组时尺寸变化也是最可靠的自愈信号。
        // 注意：不使用 LayoutUpdated——它过于频繁，会破坏画布上的拖拽/选中交互。
        SizeChanged += (_, _) =>
        {
            if (_sizeChangedScheduled || Bounds.Width <= 1) return;
            _sizeChangedScheduled = true;
            Dispatcher.UIThread.Post(() =>
            {
                _sizeChangedScheduled = false;
                if (Bounds.Width > 10 && Parent != null) RefreshForDock();
            }, DispatcherPriority.Loaded);
        };

        // R8-FIX: Loaded 事件在模板完全应用后触发
        Loaded += (_, _) =>
        {
            Dispatcher.UIThread.Post(() => { if (Parent != null) RefreshForDock(); }, DispatcherPriority.Background);
        };

        CanvasLayer.PropertyChanged += (_, e) =>
        {
            if (e.Property == Controls.DesignCanvas.ZoomLevelProperty)
            {
                var zoom = CanvasLayer.ZoomLevel;
                ZoomLevelChanged?.Invoke(this, zoom);
                DockLayoutState.RaiseCanvasZoomLevelChanged(this, zoom);
            }
        };

        // 串联 DesignCanvas 的拖放事件
        // [dock-blank-after-drag] 工厂模式下每次 Build 创建新实例，
        // MainWindow.CanvasPanel 字段可能指向旧实例，故同时经 DockLayoutState
        // 静态聚合器路由，确保实际显示的画布实例的拖放事件能到达 MainWindow。
        CanvasLayer.ControlDropped += (_, args) =>
        {
            ControlDropped?.Invoke(this, args);
            DockLayoutState.RaiseControlDropped(this, args);
        };

        CanvasLayer.TemplateDropped += (_, args) =>
        {
            TemplateDropped?.Invoke(this, args);
            DockLayoutState.RaiseTemplateDropped(this, args);
        };

        CanvasLayer.ResourceDropped += (_, args) =>
        {
            ResourceDropped?.Invoke(this, args);
            DockLayoutState.RaiseResourceDropped(this, args);
        };

        // 串联选中变化事件 —— 同时通过 DockLayoutState 聚合器路由
        CanvasLayer.SelectionChanged += (_, controls) =>
        {
            DockLayoutState.RaiseCanvasControlSelected(this, controls.FirstOrDefault());
        };

        // 串联属性变化事件 —— 通过 DockLayoutState 聚合器路由
        CanvasLayer.ControlPropertyChanged += (_, _) =>
        {
            DockLayoutState.RaiseCanvasControlPropertyChanged(this, EventArgs.Empty);
        };

        // 串联预览点击事件 —— 同时通过 DockLayoutState 聚合器路由
        CanvasLayer.PreviewControlClicked += (_, control) =>
        {
            // 预览模式点击输入框 → 弹出软键盘；点击其他 → 关闭键盘
            if (control.Type == HmiControlType.TextArea)
                ShowSoftKeyboard(control);
            else
                HideSoftKeyboard();

            PreviewControlClicked?.Invoke(this, control);
            DockLayoutState.RaiseCanvasPreviewControlClicked(this, control);
        };

        // I10: 串联绘图提交事件 —— 同时通过 DockLayoutState 聚合器路由
        CanvasLayer.ControlDrawn += (_, args) =>
        {
            ControlDrawn?.Invoke(this, args);
            DockLayoutState.RaiseControlDrawn(this, args);
        };

        // I10: 绘图工具状态同步 —— 订阅统一放到 OnAttachedToVisualTree（与 Detached 对称），
        // 保证 Dock 隐藏/显示（同一实例 detach→attach）后工具切换仍能到达本画布；
        // 此处仅初始化当前工具状态。
        CanvasLayer.DrawTool = DockLayoutState.ActiveDrawTool;

        // R2-T2: 预览模式切换 → 同步画布状态 + 通知宿主启停仿真器
        PreviewToggleButton.IsCheckedChanged += (_, _) =>
        {
            var isPreview = PreviewToggleButton.IsChecked == true;
            CanvasLayer.IsPreviewMode = isPreview;
            // 切换时立即清除选中，避免编辑态选中框残留
            if (isPreview) CanvasLayer.ClearSelection();
            else HideSoftKeyboard(); // 退出预览时关闭软键盘
            CanvasLayer.InvalidateVisual();
            PreviewModeChanged?.Invoke(this, isPreview);
            DockLayoutState.RaiseCanvasPreviewModeChanged(this, isPreview);
        };

        // ── 右键上下文菜单 ──
        BuildContextMenu();

        // R2-T2: 预览模式软键盘初始化
        InitializeSoftKeyboard();

        // 应用级编辑器偏好（网格点间距 / 显示网格 / 吸附步长 / 吸附开关）
        ApplyEditorPreferences();
    }

    // ── 应用级编辑器偏好 → 画布三层 ──

    /// <summary>
    /// 把应用级编辑器偏好应用到画布三层。
    ///
    /// <para><b>为什么在这里读而不是绑 XAML：</b>这三个值属于"应用级用户偏好"
    /// （<c>SettingsService</c>），不是工程数据，画布控件本身不该知道它们从哪来。
    /// <c>DesignCanvasView</c> 是唯一同时看得到"设置服务"和"三层画布"的地方。</para>
    ///
    /// <para><b>默认值铁律：</b>这里所有兜底值都等于接线之前的硬编码值
    /// （网格 10 / 吸附步长 5 / 两个开关 true）——
    /// 所以"设置服务取不到"与"用户没改过"两种情况下，画布外观与手感完全一致。</para>
    /// </summary>
    private void ApplyEditorPreferences()
    {
        var settings = Settings;
        var interaction = settings?.Interaction;

        var gridSize = interaction?.GridSize ?? 10;
        GridLayer.GridSize = gridSize > 0 ? gridSize : 10;
        GridLayer.IsVisible = interaction?.ShowGrid ?? true;

        CanvasLayer.SnapStep = interaction?.SnapStep ?? Controls.DesignCanvas.DefaultSnapStep;
        CanvasLayer.SnapEnabled = interaction?.EnableSnapToGrid ?? true;

        GridLayer.InvalidateVisual();
    }

    /// <summary>设置变化后重算画布外观（只关心本画布用到的几个键）。</summary>
    private void OnSettingChanged(object? sender, string key)
    {
        switch (key)
        {
            case nameof(SettingsService.InteractionSettings.GridSize):
            case nameof(SettingsService.InteractionSettings.ShowGrid):
            case nameof(SettingsService.InteractionSettings.SnapStep):
            case nameof(SettingsService.InteractionSettings.EnableSnapToGrid):
                ApplyEditorPreferences();
                break;
        }
    }

    // 右键菜单项引用（代码构建，非 XAML）
    private MenuItem? _ctxCopy, _ctxPaste, _ctxDuplicate, _ctxDelete;
    private MenuItem? _ctxAlign, _ctxSameSize, _ctxDistribute, _ctxGroup, _ctxUngroup;
    private ContextMenu? _canvasContextMenu;

    // ── R2-T2: 预览模式软键盘 ──

    /// <summary>键盘行布局（与 DesignCanvas.RenderKeyboardPlaceholder 一致）。</summary>
    private static readonly string[] KbdRows =
    {
        "1234567890",
        "QWERTYUIOP",
        "ASDFGHJKL",
        "ZXCVBNM",
    };

    /// <summary>初始化软键盘键位（在代码中构建键按钮，避免 XAML 重复粘贴）。</summary>
    private void InitializeSoftKeyboard()
    {
        Button MakeKey(string label, string? text)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 14,
                Height = 34,
                MinWidth = 34,
                Padding = new Avalonia.Thickness(4, 0),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Tag = text ?? label, // 保存原始（小写）字符，供 Shift 切换
            };
            Avalonia.Controls.ToolTip.SetTip(btn, AppLocalization.Service["sim.keyboard.char"]);
            btn.Click += (_, _) => OnKbdCharClicked(text ?? label);
            return btn;
        }

        void FillRow(Avalonia.Controls.Primitives.UniformGrid grid, string chars)
        {
            foreach (var ch in chars)
            {
                var label = ch.ToString();
                grid.Children.Add(MakeKey(label, label));
            }
        }

        FillRow(KbdRowDigits, KbdRows[0]);
        FillRow(KbdRowTop, KbdRows[1]);
        FillRow(KbdRowHome, KbdRows[2]);
        // 底行：Shift + 字母 + 退格（退格已在功能键区，此行使键盘更贴近真实布局）
        foreach (var ch in KbdRows[3])
        {
            var label = ch.ToString();
            KbdRowBottom.Children.Add(MakeKey(label, label));
        }
    }

    /// <summary>弹出软键盘并聚焦到指定输入框。</summary>
    private void ShowSoftKeyboard(HmiControl textArea)
    {
        _keyboardTarget = textArea;
        SoftKeyboardPanel.IsVisible = true;
        KbdTargetLabel.Text = $"{AppLocalization.Service["sim.keyboard.target"]} {textArea.Name}";
        UpdateKbdShiftState();
    }

    /// <summary>隐藏软键盘（不改变输入框内容）。</summary>
    private void HideSoftKeyboard()
    {
        _keyboardTarget = null;
        SoftKeyboardPanel.IsVisible = false;
    }

    /// <summary>同步 Shift 状态（大写时字母键显示大写，Shift 键高亮）。</summary>
    private void UpdateKbdShiftState()
    {
        KbdShiftBtn.Background = _kbdShiftOn
            ? (Avalonia.Media.IBrush?)new Avalonia.Media.SolidColorBrush(
                Avalonia.Media.Color.Parse("#3b82f6"))
            : null;

        // 更新字母键标签：Tag 保存原始字符，Shift 开启时显示大写
        foreach (var grid in new[] { KbdRowTop, KbdRowHome, KbdRowBottom })
        {
            foreach (var child in grid.Children)
            {
                if (child is Button btn && btn.Tag is string raw)
                {
                    btn.Content = _kbdShiftOn ? raw.ToUpperInvariant() : raw.ToLowerInvariant();
                }
            }
        }
    }

    /// <summary>键盘字符键点击：向聚焦输入框追加字符。</summary>
    private void OnKbdCharClicked(string ch)
    {
        if (_keyboardTarget == null) return;
        var text = _keyboardTarget.Text ?? "";
        var actual = _kbdShiftOn ? ch.ToUpperInvariant() : ch.ToLowerInvariant();
        if (_keyboardTarget.MaxLength > 0 && text.Length >= _keyboardTarget.MaxLength) return;
        _keyboardTarget.Text = text + actual;
        CanvasLayer.NotifyControlChanged(_keyboardTarget);
    }

    private void OnKbdSpaceClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_keyboardTarget == null) return;
        var text = _keyboardTarget.Text ?? "";
        if (_keyboardTarget.MaxLength > 0 && text.Length >= _keyboardTarget.MaxLength) return;
        _keyboardTarget.Text = text + " ";
        CanvasLayer.NotifyControlChanged(_keyboardTarget);
    }

    private void OnKbdShiftClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _kbdShiftOn = !_kbdShiftOn;
        UpdateKbdShiftState();
    }

    private void OnKbdBackspaceClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_keyboardTarget == null) return;
        var text = _keyboardTarget.Text ?? "";
        if (text.Length > 0)
        {
            _keyboardTarget.Text = text[..^1];
            CanvasLayer.NotifyControlChanged(_keyboardTarget);
        }
    }

    private void OnKbdDoneClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HideSoftKeyboard();
    }

    /// <summary>
    /// 构建画布右键菜单（Avalonia 1.2 中 Control.ContextMenu 在 XAML 解析存在兼容问题）。
    /// 事件通过 DockLayoutState 静态聚合器路由，确保 Dock 重组后仍能到达 MainWindow。
    /// </summary>
    private void BuildContextMenu()
    {
        static string L(string key) => AppLocalization.Service[key];

        _canvasContextMenu = new ContextMenu();

        // 编辑组
        _ctxCopy = new MenuItem { Header = L("canvas.ctx.copy") };
        _ctxPaste = new MenuItem { Header = L("canvas.ctx.paste") };
        _ctxDuplicate = new MenuItem { Header = L("canvas.ctx.duplicate") };
        _ctxDelete = new MenuItem { Header = L("canvas.ctx.delete") };

        // 对齐子菜单
        _ctxAlign = new MenuItem { Header = L("canvas.ctx.align") };
        var alignLeft = new MenuItem { Header = L("canvas.ctx.align.left") };
        var alignCenterH = new MenuItem { Header = L("canvas.ctx.align.centerH") };
        var alignRight = new MenuItem { Header = L("canvas.ctx.align.right") };
        var alignTop = new MenuItem { Header = L("canvas.ctx.align.top") };
        var alignCenterV = new MenuItem { Header = L("canvas.ctx.align.centerV") };
        var alignBottom = new MenuItem { Header = L("canvas.ctx.align.bottom") };
        _ctxAlign.Items.Add(alignLeft);
        _ctxAlign.Items.Add(alignCenterH);
        _ctxAlign.Items.Add(alignRight);
        _ctxAlign.Items.Add(new Separator());
        _ctxAlign.Items.Add(alignTop);
        _ctxAlign.Items.Add(alignCenterV);
        _ctxAlign.Items.Add(alignBottom);

        // 统一尺寸
        _ctxSameSize = new MenuItem { Header = L("canvas.ctx.sameSize") };

        // 等距排列子菜单
        _ctxDistribute = new MenuItem { Header = L("canvas.ctx.distribute") };
        var distH = new MenuItem { Header = L("canvas.ctx.distribute.h") };
        var distV = new MenuItem { Header = L("canvas.ctx.distribute.v") };
        _ctxDistribute.Items.Add(distH);
        _ctxDistribute.Items.Add(distV);

        // 组合/拆分
        _ctxGroup = new MenuItem { Header = L("canvas.ctx.group") };
        _ctxUngroup = new MenuItem { Header = L("canvas.ctx.ungroup") };

        // 添加到菜单
        _canvasContextMenu.Items.Add(_ctxCopy);
        _canvasContextMenu.Items.Add(_ctxPaste);
        _canvasContextMenu.Items.Add(_ctxDuplicate);
        _canvasContextMenu.Items.Add(_ctxDelete);
        _canvasContextMenu.Items.Add(new Separator());
        _canvasContextMenu.Items.Add(_ctxAlign);
        _canvasContextMenu.Items.Add(_ctxSameSize);
        _canvasContextMenu.Items.Add(_ctxDistribute);
        _canvasContextMenu.Items.Add(new Separator());
        _canvasContextMenu.Items.Add(_ctxGroup);
        _canvasContextMenu.Items.Add(_ctxUngroup);

        // 设置到画布控件
        CanvasLayer.ContextMenu = _canvasContextMenu;

        // 菜单打开前动态显隐
        _canvasContextMenu.Opening += (_, _) => UpdateContextMenuVisibility();

        // 绑定 Click 事件 → 通过 DockLayoutState 路由
        _ctxCopy.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.Copy);
        _ctxPaste.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.Paste);
        _ctxDuplicate.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.Duplicate);
        _ctxDelete.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.Delete);

        alignLeft.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.AlignLeft);
        alignCenterH.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.AlignCenterH);
        alignRight.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.AlignRight);
        alignTop.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.AlignTop);
        alignCenterV.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.AlignCenterV);
        alignBottom.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.AlignBottom);

        _ctxSameSize.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.SameSize);

        distH.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.DistributeH);
        distV.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.DistributeV);

        _ctxGroup.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.Group);
        _ctxUngroup.Click += (_, _) => DockLayoutState.RaiseCanvasContextAction(this, CanvasContextAction.Ungroup);
    }

    /// <summary>
    /// 根据当前选中状态动态控制右键菜单项可见性：
    ///   无选中 → 隐藏全部操作项（仅保留粘贴）
    ///   ≥1 选中 → 显示编辑组（复制/多重复制/删除）
    ///   ≥2 选中 → 额外显示对齐/统一尺寸/组合
    ///   ≥3 选中 → 额外显示等距排列
    ///   选中含组合 → 额外显示拆分
    /// </summary>
    private void UpdateContextMenuVisibility()
    {
        var hasSelection = CanvasLayer.GetSelectedControls().Count > 0;
        var multiSelected = CanvasLayer.HasMultiSelection;
        var hasGroup = CanvasLayer.SelectionContainsGroup;
        var count = CanvasLayer.GetSelectedControls().Count;

        // 编辑组
        if (_ctxCopy != null) _ctxCopy.IsVisible = hasSelection;
        if (_ctxPaste != null) _ctxPaste.IsVisible = true;
        if (_ctxDuplicate != null) _ctxDuplicate.IsVisible = hasSelection;
        if (_ctxDelete != null) _ctxDelete.IsVisible = hasSelection;

        // 对齐 + 统一尺寸 + 组合（≥2选中）
        var multiVisible = hasSelection && multiSelected;
        if (_ctxAlign != null) _ctxAlign.IsVisible = multiVisible;
        if (_ctxSameSize != null) _ctxSameSize.IsVisible = multiVisible;
        if (_ctxGroup != null) _ctxGroup.IsVisible = multiVisible;

        // 等距排列（≥3选中）
        var distributeVisible = multiVisible && count >= 3;
        if (_ctxDistribute != null) _ctxDistribute.IsVisible = distributeVisible;

        // 拆分（选中含组合）
        if (_ctxUngroup != null) _ctxUngroup.IsVisible = hasSelection && hasGroup;
    }

    public DesignCanvas Canvas => CanvasLayer;

    /// <summary>
    /// R8-FIX: Dock 框架重新停靠/重组后，画布可能因尺寸/渲染失效而显示空白。
    /// 重新测量 + 重绘画布 + 同步空态提示。
    /// 注意：不再检查 IsVisible——Dock 重组期间该值可能短暂为 false。
    /// </summary>
    public void RefreshForDock()
    {
        var screen = CanvasLayer.Screen;
        if (screen != null)
        {
            EmptyHint.IsVisible = screen.Controls.Count == 0;
        }
        InvalidateMeasure();
        InvalidateVisual();
        CanvasLayer.InvalidateMeasure();
        CanvasLayer.InvalidateVisual();
    }

    /// <summary>
    /// R8-FIX: OnAttachedToVisualTree 在模板应用前触发，不在这里刷新。
    /// 改由 Loaded 事件处理器处理。
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // I10: 订阅绘图工具变化（Dock 重建/重新停靠后恢复同步）+ 恢复当前工具状态
        _drawToolChangedHandler ??= (_, tool) =>
        {
            CanvasLayer.DrawTool = tool;
            CanvasLayer.InvalidateVisual();
        };
        DockLayoutState.DrawToolChanged += _drawToolChangedHandler;
        CanvasLayer.DrawTool = DockLayoutState.ActiveDrawTool;

        // 编辑器偏好变化 → 实时重算（否则改完设置得重启，等于没接线）
        var settings = Settings;
        if (settings != null)
        {
            _settingChangedHandler ??= OnSettingChanged;
            settings.SettingChanged -= _settingChangedHandler;
            settings.SettingChanged += _settingChangedHandler;
        }
        ApplyEditorPreferences();
    }

    /// <summary>与 Attached 对称：Dock 面板重建/分离时解绑订阅，避免静态事件累积。</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_drawToolChangedHandler != null)
            DockLayoutState.DrawToolChanged -= _drawToolChangedHandler;
        if (_settingChangedHandler != null)
            Settings?.SettingChanged -= _settingChangedHandler;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// 设置当前编辑的屏幕；<paramref name="project"/> 非空时顺带应用工程的屏幕默认值
    /// （阶段 221：安全区 ⇒ 画布参考线）。
    ///
    /// <para>⚠ 用**可选参数**而不是在 `MainWindow` 里多写一行调用：那个文件有"行数不许涨"的
    /// 治理守卫（`SingleFileSizeGuardTests`，基线 2983）—— 接线要长在这里。</para>
    /// </summary>
    public void SetScreen(HmiScreen screen, HmiProject? project = null)
    {
        CanvasLayer.Screen = screen;
        EmptyHint.IsVisible = screen.Controls.Count == 0;
        ApplyProjectScreenDefaults(project);
        CanvasLayer.InvalidateVisual();
    }

    /// <summary>
    /// **应用工程的屏幕默认值**（阶段 221，`#98` 的"接线"）：安全区内边距 ⇒ 画布参考线。
    ///
    /// <para>⚠ 目前只接**安全区**（看得见效果、且不动几何）；`DpiScale`（屏幕 DPI 缩放）
    /// 的接线单独做 —— 它要接进画布坐标换算（`Coordinates`）与渲染比例，牵动命中测试，
    /// 与这一条的风险不是一个量级。</para>
    /// </summary>
    public void ApplyProjectScreenDefaults(HmiProject? project)
    {
        var padding = project?.ScreenDefaultsConfig?.SafeAreaPadding;
        CanvasLayer.SafePadding = padding is null
            ? default
            : new Thickness(padding.Left, padding.Top, padding.Right, padding.Bottom);
        // 阶段 222：DPI 缩放也接上（默认 1.0 ⇒ 既有工程与判据不受影响）
        CanvasLayer.DpiScale = project?.ScreenDefaultsConfig?.DpiScale ?? 1.0;
        CanvasLayer.InvalidateVisual();
    }

    /// <summary>
    /// 刷新画布（重绘所有控件）。
    /// </summary>
    public void Refresh()
    {
        var screen = CanvasLayer.Screen;
        if (screen != null)
        {
            EmptyHint.IsVisible = screen.Controls.Count == 0;
        }
        CanvasLayer.InvalidateVisual();
    }

    /// <summary>
    /// 添加控件（从工具箱调用）。
    /// </summary>
    public HmiControl AddControl(HmiControlType type, double centerX, double centerY)
    {
        var ctrl = CanvasLayer.AddControl(type, centerX, centerY);
        EmptyHint.IsVisible = false;
        return ctrl;
    }

    /// <summary>
    /// 获取当前选中的控件。
    /// </summary>
    public HmiControl? GetSelectedControl() => CanvasLayer.GetSelectedControl();

    /// <summary>
    /// 清除选中状态。
    /// </summary>
    public void ClearSelection() => CanvasLayer.ClearSelection();

    /// <summary>
    /// 删除选中的控件。
    /// </summary>
    public void DeleteSelectedControl() => CanvasLayer.DeleteSelectedControl();

    // ── 复制粘贴 ──
    public void CopySelectedControls() => CanvasLayer.CopySelectedControls();
    public IReadOnlyList<HmiControl>? PasteControls() => CanvasLayer.PasteControls();
    public bool HasClipboardContent => CanvasLayer.HasClipboardContent;

    // ── 多重复制 ──
    public IReadOnlyList<HmiControl>? DuplicateSelected(int count, double spacingX, double spacingY)
        => CanvasLayer.DuplicateSelected(count, spacingX, spacingY);

    // ── 组合与拆分 ──
    public string? GroupSelectedControls() => CanvasLayer.GroupSelectedControls();
    public int UngroupSelectedControls() => CanvasLayer.UngroupSelectedControls();
    public bool HasMultiSelection => CanvasLayer.HasMultiSelection;
    public bool SelectionContainsGroup => CanvasLayer.SelectionContainsGroup;

    // ── 对齐功能（6种）──
    public bool AlignSelected(DesignCanvas.MultiAlignDirection direction)
        => CanvasLayer.AlignSelectedControls(direction);

    // ── 统一尺寸 ──
    public bool MakeSameSize() => CanvasLayer.MakeSameSize();

    // ── 等距排列 ──
    public bool DistributeHorizontal() => CanvasLayer.DistributeSelectedHorizontal();
    public bool DistributeVertical() => CanvasLayer.DistributeSelectedVertical();

    /// <summary>返回所有选中控件（多选操作）。</summary>
    public IReadOnlyList<HmiControl> GetSelectedControls() => CanvasLayer.GetSelectedControls();

    /// <summary>
    /// R2-T2: 设置预览值提供器（由 MainWindow 注入，桥接 VariableSimulator）。
    /// </summary>
    public void SetPreviewValueProvider(Func<string, double?>? provider)
    {
        CanvasLayer.PreviewValueProvider = provider;
        if (CanvasLayer.IsPreviewMode)
            CanvasLayer.InvalidateVisual();
    }

    /// <summary>
    /// R2-T2: 设置预览变量类型提供器（变量ID → 数据类型，用于预览值格式化）。
    /// </summary>
    public void SetPreviewValueTypeProvider(Func<string, string?>? typeProvider)
    {
        CanvasLayer.PreviewValueTypeProvider = typeProvider;
        if (CanvasLayer.IsPreviewMode)
            CanvasLayer.InvalidateVisual();
    }

    /// <summary>
    /// 设置预览变量单位提供器（变量ID → 单位，作为预览文本后缀兜底，与生成代码一致）。
    /// </summary>
    public void SetPreviewValueUnitProvider(Func<string, string?>? unitProvider)
    {
        CanvasLayer.PreviewValueUnitProvider = unitProvider;
        if (CanvasLayer.IsPreviewMode)
            CanvasLayer.InvalidateVisual();
    }
}
