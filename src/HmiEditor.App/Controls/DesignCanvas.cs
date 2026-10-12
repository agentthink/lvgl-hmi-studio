using Avalonia;
using HmiEditor.App.Design;
using HmiEditor.App.Resources;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using HmiEditor.Core.Design;
using HmiEditor.Core.Models;
using HmiEditor.Core.Services;
using System.IO;
using System.Linq;

namespace HmiEditor.App.Controls;

/// <summary>
/// 设计画布核心控件 —— 控件层的交互逻辑。
/// 使用三层叠加模型：SnapGrid(底层) + DesignCanvas(控件层) + SelectionAdorner(顶层装饰)。
/// 负责：控件渲染、选中、拖拽移动、缩放手柄操作。
/// <para>
/// 2026-09-14 按域拆为 partial 分部，本文件承载依赖属性与字段、渲染主循环与控件分发、
/// 颜色解析，以及文件末尾的事件参数类；其余分部：
/// <c>DesignCanvas.Renderers.cs</c>（基本/图形控件渲染器与绘制 helper）、
/// <c>DesignCanvas.IndustrialRenderers.cs</c>（SVG 与工业设备图形）、
/// <c>DesignCanvas.Input.cs</c>（指针交互、命中测试、选择、拖放）、
/// <c>DesignCanvas.Editing.cs</c>（控件创建、绘制工具、剪贴板、对齐、辅助线）。
/// </para>
/// </summary>
public partial class DesignCanvas : Control
{
    // ── 依赖属性 ──

    public static readonly StyledProperty<HmiScreen?> ScreenProperty =
        AvaloniaProperty.Register<DesignCanvas, HmiScreen?>(nameof(Screen));

    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<DesignCanvas, double>(nameof(ZoomLevel), 1.0);

    /// <summary>
    /// 画布平移偏移量 X（视图坐标像素）。
    /// 作为 StyledProperty 可被 Avalonia 动画系统驱动，实现缩放/平移的平滑过渡。
    /// </summary>
    /// <summary>
    /// **屏幕 DPI 缩放**（阶段 222，登记 `#98` 的后半）：设备密度 ≠ 1 时，画布按
    /// <c>ZoomLevel × DpiScale</c>（见 <see cref="EffectiveZoom"/>）渲染并换算坐标 ——
    /// 渲染与命中共用同一个有效缩放，所以**拖拽不会错位**。默认 1.0（既有工程与判据不受影响）。
    /// </summary>
    public static readonly StyledProperty<double> DpiScaleProperty =
        AvaloniaProperty.Register<DesignCanvas, double>(nameof(DpiScale), 1.0);

    /// <summary>
    /// **屏幕安全区内边距**（模型坐标像素，左/上/右/下）—— 阶段 221 给 `#98` 的"接线"：
    /// 全零表示不画参考线；非零时画布按它画一圈**虚线参考线**（只影响显示，不动几何）。
    /// </summary>
    public static readonly StyledProperty<Thickness> SafePaddingProperty =
        AvaloniaProperty.Register<DesignCanvas, Thickness>(nameof(SafePadding));

    public static readonly StyledProperty<double> PanOffsetXProperty =
        AvaloniaProperty.Register<DesignCanvas, double>(nameof(PanOffsetX), 0.0);

    /// <summary>
    /// 画布平移偏移量 Y（视图坐标像素）。
    /// 作为 StyledProperty 可被 Avalonia 动画系统驱动，实现缩放/平移的平滑过渡。
    /// </summary>
    public static readonly StyledProperty<double> PanOffsetYProperty =
        AvaloniaProperty.Register<DesignCanvas, double>(nameof(PanOffsetY), 0.0);

    public static readonly StyledProperty<SelectionAdorner?> AdornerProperty =
        AvaloniaProperty.Register<DesignCanvas, SelectionAdorner?>(nameof(Adorner));

    // ── R2-T2: 预览模式（设计时数据预览）──
    public static readonly StyledProperty<bool> IsPreviewModeProperty =
        AvaloniaProperty.Register<DesignCanvas, bool>(nameof(IsPreviewMode));

    // ── I9: 分辨率预览 —— "WxH" 目标分辨率；null 表示关闭自适应预览 ──
    public static readonly StyledProperty<string?> PreviewResolutionProperty =
        AvaloniaProperty.Register<DesignCanvas, string?>(nameof(PreviewResolution));

    /// <summary>
    /// 项目主题（HmiTheme）—— 注入设计器渲染层后，兜底色/文本色等主题语义色
    /// 优先取主题值，未设置时回退 DesignPalette 默认值。null 表示使用默认调色板。
    /// </summary>
    public static readonly new StyledProperty<HmiTheme?> ThemeProperty =
        AvaloniaProperty.Register<DesignCanvas, HmiTheme?>(nameof(Theme));

    internal const double MinControlSize = 10;

    /// <summary>
    /// 吸附步长的出厂默认值（像素）。
    /// <para>等于本批次接线之前 <c>SnapThreshold</c> 的硬编码值 —— 默认行为逐像素不变；
    /// 用户值由 <c>DesignCanvasView</c> 从应用级设置（<c>SettingsService.Interaction.SnapStep</c>）注入。</para>
    /// </summary>
    internal const double DefaultSnapStep = 5;

    private double _snapStep = DefaultSnapStep;
    private bool _snapEnabled = true;

    /// <summary>
    /// 拖拽/缩放时的吸附步长（像素）。设为非正数时回退 <see cref="DefaultSnapStep"/>。
    /// </summary>
    public double SnapStep
    {
        get => _snapStep;
        set => _snapStep = value > 0 ? value : DefaultSnapStep;
    }

    /// <summary>是否启用吸附。关闭后 <c>SnapToGrid</c> 原样返回，拖拽不再对齐。</summary>
    public bool SnapEnabled
    {
        get => _snapEnabled;
        set => _snapEnabled = value;
    }

    /// <summary>拖拽数据格式标识 —— 用于 Toolbox → DesignCanvas 的控件拖放。</summary>
    public const string DragFormat = "HmiControlType";

    /// <summary>强类型拖拽格式（进程内）—— Avalonia 12.1 使用 DataFormat 替代字符串格式。</summary>
    public static readonly DataFormat<string> DragDataFormat =
        DataFormat.CreateInProcessFormat<string>(DragFormat);

    /// <summary>模板拖拽数据格式标识 —— 用于 FaceplateLibrary → DesignCanvas 的模板拖放。</summary>
    public const string TemplateDragFormat = "HmiTemplateId";

    /// <summary>强类型模板拖拽格式（进程内）—— 传递模板 ID 字符串。</summary>
    public static readonly DataFormat<string> TemplateDragDataFormat =
        DataFormat.CreateInProcessFormat<string>(TemplateDragFormat);

    /// <summary>资源拖拽数据格式标识 —— 用于 ResourceLibrary → DesignCanvas 的资源拖放。</summary>
    public const string ResourceDragFormat = "HmiResourceId";

    /// <summary>强类型资源拖拽格式（进程内）—— 传递资源 ID 字符串。</summary>
    public static readonly DataFormat<string> ResourceDragDataFormat =
        DataFormat.CreateInProcessFormat<string>(ResourceDragFormat);

    public HmiScreen? Screen
    {
        get => GetValue(ScreenProperty);
        set => SetValue(ScreenProperty, value);
    }

    public double ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    /// <summary>屏幕 DPI 缩放（见 <see cref="DpiScaleProperty"/>）。</summary>
    public double DpiScale
    {
        get => GetValue(DpiScaleProperty);
        set => SetValue(DpiScaleProperty, value);
    }

    /// <summary>
    /// **有效缩放** ＝ 用户缩放 × DPI 缩放（DPI 值非正时按 1 处理，避免除零）。
    /// 画布的**渲染与坐标换算都用它** —— 两边必须成对，否则拖拽会错位。
    /// </summary>
    public double EffectiveZoom
    {
        get
        {
            var dpi = DpiScale;
            return ZoomLevel * (dpi > 0 ? dpi : 1.0);
        }
    }

    /// <summary>视口坐标 → 模型坐标（判据/外部用；与内部命中测试走同一个 <c>Coordinates</c>）。</summary>
    public Point ViewportToModel(Point viewportPoint) => Coordinates.ScreenToModel(viewportPoint);

    /// <summary>模型坐标 → 视口坐标。</summary>
    public Point ModelToViewport(Point modelPoint) => Coordinates.ModelToScreen(modelPoint);

    /// <summary>屏幕安全区内边距（见 <see cref="SafePaddingProperty"/>）。</summary>
    public Thickness SafePadding
    {
        get => GetValue(SafePaddingProperty);
        set => SetValue(SafePaddingProperty, value);
    }

    /// <summary>画布平移偏移量 X（视图坐标像素）。</summary>
    public double PanOffsetX
    {
        get => GetValue(PanOffsetXProperty);
        set => SetValue(PanOffsetXProperty, value);
    }

    /// <summary>画布平移偏移量 Y（视图坐标像素）。</summary>
    public double PanOffsetY
    {
        get => GetValue(PanOffsetYProperty);
        set => SetValue(PanOffsetYProperty, value);
    }

    /// <summary>I9: 分辨率预览（如 "1024x600"），null 关闭。</summary>
    public string? PreviewResolution
    {
        get => GetValue(PreviewResolutionProperty);
        set => SetValue(PreviewResolutionProperty, value);
    }

    public SelectionAdorner? Adorner
    {
        get => GetValue(AdornerProperty);
        set => SetValue(AdornerProperty, value);
    }

    /// <summary>项目主题（见 <see cref="ThemeProperty"/>），null = 使用默认调色板。</summary>
    public new HmiTheme? Theme
    {
        get => GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>R2-T2: 预览模式 —— 预览模式下禁用编辑，仅显示模拟数据。</summary>
    public bool IsPreviewMode
    {
        get => GetValue(IsPreviewModeProperty);
        set => SetValue(IsPreviewModeProperty, value);
    }

    /// <summary>
    /// R2-T2: 预览值提供器 —— 变量ID → 模拟值 的查找委托。
    /// 非空时，预览模式下绑定变量的控件渲染此委托返回的模拟值。
    /// 该委托只读，绝不写回模型（适应度函数 R2-FF：预览模式隔离 0 写操作）。
    /// </summary>
    public Func<string, double?>? PreviewValueProvider { get; set; }

    /// <summary>
    /// R2-T2: 预览变量类型提供器 —— 变量ID → 数据类型（"Bool"/"Int16"/"Float32"/"String" 等）的查找委托。
    /// 供预览值按变量类型格式化（如 Bool 显示 ON/OFF、整数不显示小数）。
    /// </summary>
    public Func<string, string?>? PreviewValueTypeProvider { get; set; }

    /// <summary>
    /// 预览变量单位提供器 —— 变量ID → 单位（如 "℃"/"%"）的查找委托。
    /// 控件未配置 ValueSuffix 时作为后缀兜底，使设计器预览与生成代码显示一致。
    /// </summary>
    public Func<string, string?>? PreviewValueUnitProvider { get; set; }

    // ── 内部状态 ──

    private enum DragMode { None, Move, Resize }
    private DragMode _dragMode = DragMode.None;

    private readonly List<HmiControl> _selectedControls = new();

    /// <summary>当前选中的控件集合（只读视图，外部可主动查询，不再仅能被动收 SelectionChanged 事件）。</summary>
    public IReadOnlyList<HmiControl> SelectedControls => _selectedControls;

    /// <summary>当前选中的第一个控件（兼容旧接口）。</summary>
    private HmiControl? _selectedControl => _selectedControls.Count > 0 ? _selectedControls[0] : null;

    private HmiControl? _dragControl;
    private Point _dragStartPoint;
    private Rect _dragStartBounds;
    private int _activeHandleIndex = -1;

    // ── 多选整体拖拽：记录拖拽起始时所有选中控件的原始位置 ──
    private Dictionary<HmiControl, Rect>? _dragStartAllBounds;
    private bool _historyReplaying;

    // 工具箱拖放预览：仅保存视图态，不写入工程模型。
    private readonly CanvasDragPreviewState _dragPreview = new();

    // 图片位图缓存：AssetItem.Id → 已加载位图（加载失败缓存 null 避免重复尝试）
    private readonly Dictionary<string, Bitmap?> _imageCache = new();

    // 平移偏移量（已迁移至 StyledProperty PanOffsetX/PanOffsetY）
    private bool _isPanning;
    private Point _panStartPoint;

    // 对齐参考线（拖拽过程中有效）
    private List<AlignmentGuide> _currentGuides = new();

    // 框选相关
    private bool _isRubberBanding;
    private Point _rubberBandStart;
    private Rect? _rubberBandRect;

    // ── 长按框选：空白区域按住左键达到阈值后从按下位置开始框选 ──
    /// <summary>长按触发框选的延迟（毫秒）。</summary>
    private static readonly TimeSpan LongPressDelay = TimeSpan.FromMilliseconds(400);
    /// <summary>长按判定前允许的指针移动容差（像素，视图坐标），防止轻微抖动误触发。</summary>
    private const double LongPressMoveTolerance = 6;
    private DispatcherTimer? _longPressTimer;
    private Point _longPressStart;
    private bool _longPressPending;

    // ── 复制粘贴剪贴板（进程内内存存储，支持多控件）──
    private List<HmiControl>? _clipboardControls;
    /// <summary>粘贴偏移量（每次粘贴递增，避免完全重叠）。</summary>
    private const double PasteOffsetStep = 20.0;
    private int _pasteCount;

    /// <summary>选中控件变化事件（通知 PropertyPanel、OutlinePanel）。传递所有选中的控件。</summary>
    public event EventHandler<IReadOnlyList<HmiControl>>? SelectionChanged;

    /// <summary>控件属性变化事件（通知重绘）。</summary>
    public event EventHandler<HmiControl>? ControlPropertyChanged;

    /// <summary>
    /// 通知控件属性已变化（外部修改如预览软键盘输入后调用），
    /// 触发 <see cref="ControlPropertyChanged"/> 并重绘。
    /// </summary>
    public void NotifyControlChanged(HmiControl control)
    {
        ControlPropertyChanged?.Invoke(this, control);
        InvalidateVisual();
    }

    /// <summary>
    /// 控件拖放事件 —— 从工具箱拖拽控件到画布时触发，
    /// 携带控件类型和放置坐标（模型坐标，已补偿缩放和平移）。
    /// </summary>
    public event EventHandler<ControlDroppedEventArgs>? ControlDropped;

    /// <summary>
    /// 模板拖放事件 —— 从模板库拖拽模板到画布时触发，
    /// 携带模板 ID 和放置坐标（模型坐标，已补偿缩放和平移）。
    /// </summary>
    public event EventHandler<TemplateDroppedEventArgs>? TemplateDropped;

    /// <summary>
    /// 资源拖放事件 —— 从图库拖拽资源到画布时触发，
    /// 携带资源 ID 和放置坐标（模型坐标，已补偿缩放和平移）。
    /// </summary>
    public event EventHandler<ResourceDroppedEventArgs>? ResourceDropped;

    /// <summary>
    /// 预览模式控件点击事件 —— 预览交互时触发，宿主负责执行控件动作。
    /// </summary>
    public event EventHandler<HmiControl>? PreviewControlClicked;

    /// <summary>
    /// I10: 绘图提交事件 —— 画布上用绘图工具拖拽/点击绘制完成时触发。
    /// 携带绘制的几何（模型坐标 + 顶点），宿主负责包装为撤销命令并刷新面板。
    /// </summary>
    public event EventHandler<ControlDrawnEventArgs>? ControlDrawn;

    /// <summary>
    /// I10: 当前激活的绘图工具（控件类型）。null = 普通选择/编辑模式。
    /// 非 null 时，画布上的按下/拖拽/释放用于"画图"而非选中/移动控件：
    ///   拖拽 → 以拖拽矩形为边界创建对应图形；单击 → 在点击处创建默认尺寸图形。
    /// </summary>
    public HmiControlType? DrawTool
    {
        get => GetValue(DrawToolProperty);
        set => SetValue(DrawToolProperty, value);
    }

    public static readonly StyledProperty<HmiControlType?> DrawToolProperty =
        AvaloniaProperty.Register<DesignCanvas, HmiControlType?>(nameof(DrawTool));

    /// <summary>工具切换/退出时，先提交未完成的点击式绘制（折线/直线顶点序列）。</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DrawToolProperty)
            FlushClickDraw();
    }

    // ── I10: 绘图过程状态（DrawTool 激活时有效）──
    private Point _drawStartPoint;
    private Point _drawCurrentPoint;
    private bool _isDrawing;

    // I10: 点击式绘制（Line / Polyline）—— 每次左键单击追加一个顶点，
    // 移动鼠标显示"当前点 → 鼠标"的橡皮筋预览；Line 第二击提交，
    // Polyline 持续追加直到退出工具（ESC / 切换工具）时提交。
    private readonly List<Point> _clickPoints = new();
    private Point _clickHoverPoint;

    /// <summary>
    /// 撤销/重做管理器。
    /// </summary>
    public UndoRedoManager UndoRedoManager { get; set; } = null!;

    static DesignCanvas()
    {
        AffectsRender<DesignCanvas>(ScreenProperty, ZoomLevelProperty, DpiScaleProperty, SafePaddingProperty, PanOffsetXProperty,
            PanOffsetYProperty, ThemeProperty, IsPreviewModeProperty, PreviewResolutionProperty,
            DrawToolProperty);
        ClipToBoundsProperty.OverrideDefaultValue<DesignCanvas>(true);

        // 注册 DragDrop 类处理器 —— 画布作为拖放目标
        DragDrop.DragOverEvent.AddClassHandler<DesignCanvas>((c, e) => c.HandleDragOver(e));
        DragDrop.DropEvent.AddClassHandler<DesignCanvas>((c, e) => c.HandleDrop(e));
    }

    public DesignCanvas() : this(null)
    {
    }

    public DesignCanvas(UndoRedoManager? undoRedoManager)
    {
        UndoRedoManager = undoRedoManager ?? new UndoRedoManager();
        DragDrop.SetAllowDrop(this, true);

        // 画布可聚焦：保证点击画布后焦点从 TextBox 等控件转移到这里，
        // 窗口级 Delete/Ctrl+A 快捷键才能按预期生效
        Focusable = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var screen = Screen;
        if (screen == null) return;

        // I9: 自适应分辨率预览 —— 解析目标分辨率并按屏幕模式计算变换
        var previewRes = AdaptiveLayoutCalculator.TryParseResolution(PreviewResolution);
        AdaptiveLayoutCalculator.AdaptiveTransform? adaptive = null;
        if (previewRes.HasValue && screen.AdaptiveMode != ScreenAdaptiveMode.None)
        {
            adaptive = AdaptiveLayoutCalculator.Compute(
                screen.Width, screen.Height,
                previewRes.Value.Width, previewRes.Value.Height,
                screen.AdaptiveMode);
        }

        // 应用平移偏移
        using (context.PushTransform(
            Matrix.CreateTranslation(PanOffsetX, PanOffsetY)))
        {
            // 绘制屏幕边界（预览时显示目标分辨率）
            var screenRect = adaptive.HasValue
                ? new Rect(0, 0, previewRes!.Value.Width * EffectiveZoom, previewRes.Value.Height * EffectiveZoom)
                : new Rect(0, 0, screen.Width * EffectiveZoom, screen.Height * EffectiveZoom);
            // 屏幕背景：未配置 BgColor 时保持透明（透出网格/画布底色），符合"背景透明"期望；
            // 配置了 BgColor（如白色 #FFFFFF）才填充，保证与运行时/模拟器一致。
            if (!string.IsNullOrEmpty(screen.BgColor))
            {
                var screenBg = ParseColor(screen.BgColor, DesignPalette.ScreenBgFallback);
                context.FillRectangle(screenBg, screenRect);
            }

            // 背景图片（按显示模式绘制，透明度作用于整图；随屏幕尺寸自适应）
            DrawScreenBackgroundImage(context, screen, screenRect);

            context.DrawRectangle(
                RenderResourceCache.Pen(DesignPalette.ScreenBorder, 1),
                screenRect);

            // 🩸 阶段 221（#98 的"接线"）：屏幕安全区**参考线**（虚线，只影响显示，不动几何）
            DrawSafeAreaGuides(context);

            // 绘制所有控件（R2-T1: 跳过 IsVisible=false 的控件）
            if (adaptive.HasValue)
            {
                // 预览变换: 先缩放(设计→目标比例), 再平移(letterbox 居中)
                using (context.PushTransform(
                    Matrix.CreateScale(adaptive.Value.ScaleX, adaptive.Value.ScaleY) *
                    Matrix.CreateTranslation(
                        adaptive.Value.OffsetX * EffectiveZoom,
                        adaptive.Value.OffsetY * EffectiveZoom)))
                {
                    foreach (var control in screen.Controls)
                    {
                        if (!EffectiveVisible(control)) continue;
                        RenderControlWithOpacity(context, control);
                    }
                }
            }
            else
            {
                foreach (var control in screen.Controls)
                {
                    if (!EffectiveVisible(control)) continue;
                    RenderControlWithOpacity(context, control);
                }
            }
        }

        // 框选矩形（视口坐标，在 PushTransform 之外绘制）
        if (_rubberBandRect.HasValue)
        {
            var rect = _rubberBandRect.Value;
            var fillBrush = RenderResourceCache.Brush(DesignPalette.SkyAccent, 0.15);
            var borderPen = new Pen(RenderResourceCache.Brush(DesignPalette.SkyAccent), 1)
            {
                DashStyle = DashStyle.Dash
            };
            context.FillRectangle(fillBrush, rect);
            context.DrawRectangle(null, borderPen, rect);
        }

        // 工具箱拖放幽灵预览（视口坐标，不进入模型）。
        if (_dragPreview.IsVisible)
        {
            var preview = _dragPreview.ViewRect!.Value;
            var color = _dragPreview.IsValid ? DesignPalette.SkyAccent : DesignPalette.Error;
            var brush = RenderResourceCache.Brush(color, 0.12);
            var pen = new Pen(RenderResourceCache.Brush(color, 0.9), 1.5)
            {
                DashStyle = DashStyle.Dash
            };
            context.FillRectangle(brush, preview);
            context.DrawRectangle(null, pen, preview);
        }

        // I10: 点击式绘制预览（Line / Polyline）—— 已点顶点连线 + 到鼠标的橡皮筋
        if (DrawTool.HasValue
            && DrawTool.Value is HmiControlType.Line or HmiControlType.Polyline
            && _clickPoints.Count > 0)
        {
            var ghostPen = RenderResourceCache.Pen(DesignPalette.SkyAccent, 1.5, PenLineCap.Round);
            var vertexBrush = RenderResourceCache.Brush(DesignPalette.SkyAccent);
            for (var i = 0; i < _clickPoints.Count; i++)
            {
                var p = _clickPoints[i];
                // 已确认顶点画小方块标记
                context.FillRectangle(vertexBrush, new Rect(p.X - 2, p.Y - 2, 4, 4));
                if (i > 0)
                    context.DrawLine(ghostPen, _clickPoints[i - 1], p);
            }
            // 橡皮筋：最后一个顶点 → 当前鼠标位置
            if (_clickPoints.Count > 0)
            {
                context.DrawLine(ghostPen, _clickPoints[^1], _clickHoverPoint);
            }
        }

        // I10: 绘图预览 —— 拖拽绘制时按当前工具实时显示图形轮廓（视口坐标）
        if (_isDrawing && DrawTool.HasValue)
        {
            var x = Math.Min(_drawStartPoint.X, _drawCurrentPoint.X);
            var y = Math.Min(_drawStartPoint.Y, _drawCurrentPoint.Y);
            var w = Math.Abs(_drawCurrentPoint.X - _drawStartPoint.X);
            var h = Math.Abs(_drawCurrentPoint.Y - _drawStartPoint.Y);
            if (w >= 2 && h >= 2)
            {
                var previewRect = new Rect(x, y, w, h);
                var ghostBrush = RenderResourceCache.Brush(DesignPalette.SkyAccent, 0.15);
                var ghostPen = RenderResourceCache.Pen(DesignPalette.SkyAccent, 1.5, PenLineCap.Round);
                switch (DrawTool.Value)
                {
                    case HmiControlType.Rectangle:
                        context.FillRectangle(ghostBrush, previewRect);
                        context.DrawRectangle(null, ghostPen, previewRect);
                        break;
                    case HmiControlType.Ellipse:
                        context.DrawEllipse(ghostBrush, ghostPen, previewRect);
                        break;
                    case HmiControlType.Polyline:
                    case HmiControlType.Line:
                        context.DrawLine(ghostPen, new Point(x, y), new Point(x + w, y + h));
                        break;
                    case HmiControlType.Arc:
                        var arcPen = RenderResourceCache.Pen(DesignPalette.SkyAccent, 6, PenLineCap.Round);
                        // 预览默认 0..270° 弧（LVGL 角度约定，提交后按 MinValue/MaxValue/Value 呈现）
                        DrawArcPolyline(context, arcPen, previewRect, 0, 270);
                        break;
                    default:
                        context.FillRectangle(ghostBrush, previewRect);
                        context.DrawRectangle(null, ghostPen, previewRect);
                        break;
                }
            }
        }

        // R2-T2: 预览模式横幅（视口坐标，画布顶部居中）
        if (IsPreviewMode)
            DrawPreviewBanner(context);
    }

    /// <summary>
    /// 预览模式横幅。与控件文本走**同一条容错路径** —— 平台侧文本对象失效
    /// （渲染后端重建 / 字体服务重载 / headless 会话结束）时，一次绘制失败不能让整个
    /// 画布渲染把异常抛回 UI 线程。这条约定本来就写在 <c>DrawAlignedText</c> 上，
    /// 而横幅此前是画布上**唯一**的裸 <c>DrawText</c> 调用 —— 本沙箱实测：
    /// headless 下横幅那次 <c>DrawText</c> 会抛（<c>CmapFormat4Table.TryGetGlyph</c>），
    /// 结果整块画布渲染失败，而其它文本因为被兜住所以看不出来。
    /// </summary>
    private void DrawPreviewBanner(DrawingContext context)
    {
        var bannerText = AppLocalization.Service["canvas.preview.banner"];
        var brush = RenderResourceCache.Brush(DesignPalette.Text);
        try
        {
            DrawPreviewBannerCore(context, bannerText, brush);
        }
        catch
        {
            RenderResourceCache.InvalidateTexts();
            try
            {
                DrawPreviewBannerCore(context, bannerText, brush);
            }
            catch
            {
                // 二次失败说明不是缓存陈旧所致；绘制路径不能把异常抛回 UI 线程。
            }
        }
    }

    private void DrawPreviewBannerCore(DrawingContext context, string bannerText, IBrush brush)
    {
        var ft = RenderResourceCache.Text(bannerText, 13, brush);
        var bannerW = ft.Width + 24;
        const int bannerH = 26;
        var bannerX = (Bounds.Width - bannerW) / 2;
        var bannerRect = new Rect(bannerX, 8, bannerW, bannerH);
        context.FillRectangle(
            RenderResourceCache.Brush(DesignPalette.Success, 0.9), bannerRect, 4);
        context.DrawText(ft, new Point(bannerX + 12, 8 + (bannerH - ft.Height) / 2));
    }

    /// <summary>
    /// **本轮渲染真正画出的安全区参考线数量**（判据用：证明渲染真的消费了 `SafePadding`，
    /// 而不是只有"算得出来"）。每次渲染都会重算，所以 0 表示这次没画。
    /// </summary>
    internal int SafeGuidesDrawnCount { get; private set; }

    /// <summary>
    /// **安全区参考线**（**画布坐标**＝模型坐标 × `EffectiveZoom`，不含平移 —— 平移由渲染的
    /// `PushTransform` 统一施加，与其它绘制一致）。四条边围绕
    /// `(Left, Top)`–`(W-Right, H-Bottom)` 这个内缩矩形；全零内边距 ⇒ 空集合。
    ///
    /// <para>抽成公开方法是为了让判据能直接断言它（渲染与判据共用同一处计算）。</para>
    /// </summary>
    public IReadOnlyList<(Point From, Point To)> SafeAreaGuides()
    {
        var screen = Screen;
        if (screen == null) return Array.Empty<(Point From, Point To)>();

        var pad = SafePadding;
        if (pad.Left <= 0 && pad.Top <= 0 && pad.Right <= 0 && pad.Bottom <= 0)
            return Array.Empty<(Point From, Point To)>();

        var z = EffectiveZoom;
        var l = pad.Left * z;
        var tp = pad.Top * z;
        var r = (screen.Width - pad.Right) * z;
        var b = (screen.Height - pad.Bottom) * z;
        if (r < l) (l, r) = (r, l);
        if (b < tp) (tp, b) = (b, tp);

        return new (Point, Point)[]
        {
            (new Point(l, tp), new Point(r, tp)),
            (new Point(r, tp), new Point(r, b)),
            (new Point(r, b), new Point(l, b)),
            (new Point(l, b), new Point(l, tp)),
        };
    }

    /// <summary>画安全区参考线（虚线；只影响显示）。</summary>
    private void DrawSafeAreaGuides(DrawingContext context)
    {
        var guides = SafeAreaGuides();
        SafeGuidesDrawnCount = guides.Count;
        if (guides.Count == 0) return;

        var pen = new Pen(RenderResourceCache.Brush(DesignPalette.Success, 0.6), 1,
            new DashStyle(new double[] { 6, 4 }, 0));
        foreach (var (from, to) in guides)
            context.DrawLine(pen, from, to);
    }

    /// <summary>按显示模式绘制屏幕背景图片（平铺/拉伸/居中），透明度作用于整图。</summary>
    private void DrawScreenBackgroundImage(DrawingContext context, HmiScreen screen, Rect screenRect)
    {
        if (string.IsNullOrEmpty(screen.BgImageAssetId)) return;

        var bitmap = ResolveBitmap(screen.BgImageAssetId);
        if (bitmap == null) return;

        var z = EffectiveZoom;
        var opacity = Math.Clamp(screen.BgImageOpacity, 0, 100) / 100.0;
        using (context.PushOpacity(opacity))
        using (context.PushClip(screenRect))
        {
            switch (screen.BgImageMode)
            {
                case ScreenBgImageMode.Tile:
                    var tileW = Math.Max(1, bitmap.PixelSize.Width * z);
                    var tileH = Math.Max(1, bitmap.PixelSize.Height * z);
                    for (double y = 0; y < screenRect.Height; y += tileH)
                    {
                        for (double x = 0; x < screenRect.Width; x += tileW)
                        {
                            context.DrawImage(bitmap, new Rect(screenRect.X + x, screenRect.Y + y, tileW, tileH));
                        }
                    }
                    break;
                case ScreenBgImageMode.Center:
                    var iw = bitmap.PixelSize.Width * z;
                    var ih = bitmap.PixelSize.Height * z;
                    context.DrawImage(bitmap, new Rect(
                        screenRect.X + (screenRect.Width - iw) / 2,
                        screenRect.Y + (screenRect.Height - ih) / 2,
                        iw, ih));
                    break;
                default: // Stretch：缩放填满整个屏幕
                    context.DrawImage(bitmap, screenRect);
                    break;
            }
        }
    }

    /// <summary>按控件不透明度渲染（Opacity<100 时压入透明度层）。</summary>
    private void RenderControlWithOpacity(DrawingContext context, HmiControl control)
    {
        if (control.Opacity >= 99.5)
        {
            RenderControl(context, control);
            return;
        }
        using (context.PushOpacity(Math.Clamp(control.Opacity, 0, 100) / 100.0))
            RenderControl(context, control);
    }

    // ── 外部渲染器注册表（插件扩展点）──

    private static readonly Dictionary<HmiControlType, IControlRenderer> _extraRenderers = new();

    /// <summary>
    /// 注册自定义控件渲染器（覆盖内置渲染或为新增控件类型提供渲染）。
    /// 注册后立即对全部 DesignCanvas 实例生效；重复注册同类型以后者为准。
    /// </summary>
    public static void RegisterRenderer(HmiControlType type, IControlRenderer renderer)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        _extraRenderers[type] = renderer;
    }

    private void RenderControl(DrawingContext context, HmiControl control)
    {
        var z = EffectiveZoom;
        var rect = new Rect(control.X * z, control.Y * z, control.Width * z, control.Height * z);

        // 旋转/镜像：围绕控件中心应用变换（先镜像后旋转），内容随变换绘制、画布尺寸不变。
        // 无变换时零开销直接绘制（渲染热路径）。
        if (control.HasTransform)
        {
            var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            using (context.PushTransform(BuildControlTransform(
                center, control.EffectiveRotation(), control.MirrorX, control.MirrorY)))
            {
                RenderControlCore(context, control, rect, z);
            }
            return;
        }
        RenderControlCore(context, control, rect, z);
    }

    /// <summary>
    /// 构建控件旋转/镜像变换矩阵（围绕给定中心，先镜像后旋转）。
    /// 纯函数（不依赖实例状态），供渲染与命中测试共用、可单测。
    /// Avalonia 坐标系 y 向下：CreateRotation 正角度 = 顺时针；EffectiveRotation 正 = 顺时针，语义一致。
    /// </summary>
    internal static Matrix BuildControlTransform(Point center, double rotationDeg, bool mirrorX, bool mirrorY)
    {
        var m = Matrix.Identity;
        m *= Matrix.CreateTranslation(-center.X, -center.Y);
        if (mirrorX) m *= Matrix.CreateScale(-1, 1);   // 水平镜像（左右翻转）
        if (mirrorY) m *= Matrix.CreateScale(1, -1);   // 垂直镜像（上下翻转）
        if (rotationDeg != 0)
            m *= Matrix.CreateRotation(rotationDeg * Math.PI / 180);
        m *= Matrix.CreateTranslation(center.X, center.Y);
        return m;
    }

    private void RenderControlCore(DrawingContext context, HmiControl control, Rect rect, double z)
    {
        // 外部注册的渲染器优先（插件扩展点，可覆盖内置实现或为新控件类型提供渲染）
        if (_extraRenderers.TryGetValue(control.Type, out var ext) && ext.CanRender(control.Type))
        {
            ext.Render(context, control, rect, z);
            return;
        }

        // Wave 2 控件使用专用渲染器
        switch (control.Type)
        {
            case HmiControlType.Gauge:
                RenderGauge(context, control, rect, z);
                return;
            case HmiControlType.Knob:
                RenderKnob(context, control, rect, z);
                return;
            case HmiControlType.Led:
                RenderLed(context, control, rect, z);
                return;
            case HmiControlType.Spinner:
                RenderSpinner(context, control, rect, z);
                return;
            case HmiControlType.Line:
                RenderLineShape(context, control, rect, z);
                return;
            case HmiControlType.Keyboard:
                RenderKeyboardPlaceholder(context, control, rect, z);
                return;
            case HmiControlType.TabView:
                RenderTabViewPlaceholder(context, control, rect, z);
                return;
            case HmiControlType.Table:
                RenderTablePlaceholder(context, control, rect, z);
                return;
            case HmiControlType.Svg:
                RenderSvg(context, control, rect, z);
                return;
            // I7: 过程图形控件
            case HmiControlType.Tank:
                RenderTank(context, control, rect, z);
                return;
            case HmiControlType.Pipe:
                RenderPipe(context, control, rect, z);
                return;
            case HmiControlType.Valve:
                RenderValve(context, control, rect, z);
                return;
            // I10: 基础绘图图形
            case HmiControlType.Rectangle:
                RenderRectangleShape(context, control, rect, z);
                return;
            case HmiControlType.Ellipse:
                RenderEllipseShape(context, control, rect, z);
                return;
            case HmiControlType.Polyline:
                RenderPolylineShape(context, control, rect, z);
                return;
            case HmiControlType.Arc:
                RenderArcShape(context, control, rect, z);
                return;
            case HmiControlType.Button:
                RenderButton(context, control, rect, z);
                return;
            case HmiControlType.Switch:
                RenderSwitch(context, control, rect, z);
                return;
            case HmiControlType.CheckBox:
                RenderCheckBox(context, control, rect, z);
                return;
            case HmiControlType.Image:
                RenderImage(context, control, rect, z);
                return;
            case HmiControlType.Dropdown:
                RenderDropdown(context, control, rect, z);
                return;
        }

        // 背景色：仅配置了 BgColor 时填充；未配置保持透明（只显示边框/文字/进度），
        // 避免深灰控件叠在深色画布上"看起来没有颜色"。
        // Dropdown/TextArea 例外：LVGL 默认（未设 BgColor）有主题背景色，设计器用中性深色近似，
        // 否则下拉框/输入框在设计器里透明不可见，与模拟器不一致。
        var defaultBg = control.Type is HmiControlType.Dropdown or HmiControlType.TextArea
            ? ThemeFallback(Theme?.SurfaceColor, DesignPalette.Slate800)
            : (Color?)null;
        // 预览模式下 BgColor 可能由绑定表达式驱动 —— 取值必须走同一个接缝，
        // 否则「没配静态 BgColor 但绑了 BgColor」的控件在预览里仍然透明。
        var bgHex = EffectiveVisualColor(control, nameof(HmiControl.BgColor), control.BgColor);
        var effectiveBg = string.IsNullOrEmpty(bgHex) ? defaultBg : (Color?)null;
        if (effectiveBg.HasValue)
        {
            var bgBrush = RenderResourceCache.Brush(effectiveBg.Value);
            // Slider/Bar 未显式设 Radius 时默认胶囊圆角（高度一半），与生成器 LV_PART_MAIN 一致
            var bgRadius = control.Type is HmiControlType.Slider or HmiControlType.Bar
                && control.Radius <= 0 ? control.Height / 2 : control.Radius;
            context.FillRectangle(bgBrush, rect, (float)(bgRadius * z));
        }
        else if (!string.IsNullOrEmpty(bgHex))
        {
            var bgBrush = ParseColor(bgHex, DesignPalette.Surface);
            // Slider/Bar 未显式设 Radius 时默认胶囊圆角（高度一半），与生成器 LV_PART_MAIN 一致
            var bgRadius = control.Type is HmiControlType.Slider or HmiControlType.Bar
                && control.Radius <= 0 ? control.Height / 2 : control.Radius;
            context.FillRectangle(bgBrush, rect, (float)(bgRadius * z));
        }

        // 边框
        if (control.BorderWidth > 0)
        {
            var borderBrush = ParseColor(control.BorderColor, DesignPalette.Border);
            context.DrawRectangle(
                RenderResourceCache.Pen(borderBrush, control.BorderWidth * z),
                rect,
                (float)(control.Radius * z));
        }

        // Slider/Bar 进度填充：预览态用绑定模拟值，编辑态用控件当前 Value
        if (control.Type is HmiControlType.Slider or HmiControlType.Bar)
        {
            double? progress = null;
            if (IsPreviewMode)
                progress = PreviewValueResolver.ResolveProgress(control, PreviewValueProvider,
                    control.MinValue, control.MaxValue);
            else if (control.MaxValue > control.MinValue)
                progress = Math.Clamp(
                    (control.Value - control.MinValue) / (control.MaxValue - control.MinValue), 0, 1);

            if (progress.HasValue)
            {
                // 指示器兜底：主题 PrimaryColor（与生成器 Gauge 指示器兜底同源，收敛主题主色）
                var fillBrush = RenderResourceCache.Brush(
                    ParseColorHex(control.IndicatorColor ?? EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor),
                        ThemeFallback(Theme?.PrimaryColor, DesignPalette.Primary)));
                // 圆角：用户显式设置 Radius 时尊重；Slider/Bar 未设置时默认胶囊（高度一半），
                // 与生成器 LV_PART_MAIN/INDICATOR 圆角语义一致（此前 Radius=0 时设计器直角、模拟器胶囊，显示不同）。
                var fillRadius = control.Type is HmiControlType.Slider or HmiControlType.Bar
                    && control.Radius <= 0 ? control.Height / 2 : control.Radius;
                context.FillRectangle(fillBrush,
                    new Rect(rect.X, rect.Y, Math.Max(2 * z, rect.Width * progress.Value), rect.Height),
                    (float)(fillRadius * z));
            }

            // 刻度线 + 数值标签：ShowTicks 时沿轨道均布（数量按步长档位计算，上限 21 条含两端）。
            // 刻度画在轨道上边缘外侧（图形/指针圆形之外，像尺子刻度），数值标签在刻度线之上，
            // 与模拟器生成代码（lv_obj 刻度 + lv_label 数值）语义一致。
            if (control.ShowTicks && control.MaxValue > control.MinValue && control.StepValue > 0)
            {
                var stepCount = (int)((control.MaxValue - control.MinValue) / control.StepValue);
                var tickCount = Math.Clamp(stepCount + 1, 2, 21);
                var tickColor = ParseColorHex(EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor),
                    ThemeFallback(Theme?.TextSecondaryColor, DesignPalette.Slate400));
                var tickPen = RenderResourceCache.Pen(tickColor, 1 * z);
                var tickLen = 5 * z;
                // 数值标签字号与模拟器内置 lv_font_montserrat_12 一致（设计器-模拟器对齐）
                var labelFontSz = 12 * z;
                for (var i = 0; i < tickCount; i++)
                {
                    var ratio = (double)i / (tickCount - 1);
                    var tx = rect.X + rect.Width * ratio;
                    // 刻度线（轨道上方外侧）
                    context.DrawLine(tickPen,
                        new Point(tx, rect.Y),
                        new Point(tx, rect.Y - tickLen));
                    // 刻度数值标签（刻度线上方，水平居中于刻度）
                    var tickValue = control.MinValue + i * control.StepValue;
                    var tickLabel = BindingFormat.FormatTick(tickValue);
                    try
                    {
                        var ft = RenderResourceCache.Text(tickLabel, labelFontSz, RenderResourceCache.Brush(tickColor));
                        context.DrawText(ft, new Point(
                            tx - ft.Width / 2,
                            rect.Y - tickLen - ft.Height - 2 * z));
                    }
                    catch { }
                }
            }

            // 指针/滑块：Slider 显示圆形 knob；Bar 显示指示器末端游标。
            // 显隐由 EffectiveShowPointer 控制（null=类型默认：Slider 显示 / Bar 隐藏）。
            if (control.EffectiveShowPointer() && progress.HasValue)
            {
                var knobD = Math.Min(rect.Width, rect.Height) * z;
                var knobFillColor = ParseColorHex(control.IndicatorColor ?? EffectiveVisualColor(control, nameof(HmiControl.TextColor), control.TextColor),
                    ThemeFallback(Theme?.PrimaryColor, DesignPalette.Primary));
                if (control.Type == HmiControlType.Slider && knobD >= 4 * z)
                {
                    var knobCenterX = rect.X + rect.Width * progress.Value;
                    var knobRect = new Rect(
                        knobCenterX - knobD / 2,
                        rect.Y + (rect.Height - knobD) / 2,
                        knobD, knobD);
                    // knob 背景：IndicatorColor 兜底 TextColor，再兜底主题主色；外圈描边增强可见性
                    context.DrawEllipse(RenderResourceCache.Brush(knobFillColor),
                        RenderResourceCache.Pen(DesignPalette.Slate900, 1.5 * z),
                        knobRect);
                    // 指向刻度位置的小三角形（尖端朝下指向圆心 = 轨道刻度线），与圆形高对比
                    DrawPointerTriangle(context, new Point(knobCenterX, knobRect.Center.Y),
                        knobD / 2, knobFillColor, z);
                }
                else if (control.Type == HmiControlType.Bar && knobD >= 4 * z)
                {
                    // Bar 末端游标：位于填充终点，直径 = 控件高度 × 0.7
                    var curD = knobD * 0.7;
                    var curX = rect.X + rect.Width * progress.Value;
                    var curRect = new Rect(
                        curX - curD / 2,
                        rect.Y + (rect.Height - curD) / 2,
                        curD, curD);
                    context.DrawEllipse(RenderResourceCache.Brush(knobFillColor),
                        RenderResourceCache.Pen(DesignPalette.Slate900, 1 * z),
                        curRect);
                    // 指向刻度位置的小三角形（尖端朝下指向圆心 = 轨道刻度线），与圆形高对比
                    DrawPointerTriangle(context, new Point(curX, curRect.Center.Y),
                        curD / 2, knobFillColor, z);
                }
            }
        }

        // 绘制控件文本（优先 Text，回退到**显示名**；预览模式下显示型控件渲染模拟值）
        var label = control.Text ?? Services.ControlNameDefaults.Display(control);
        if (string.IsNullOrEmpty(label)) label = Services.ControlTypeTextKeys.Describe(control.Type);

        if (IsPreviewMode && PreviewValueResolver.IsDisplayControl(control.Type))
        {
            var previewText = PreviewValueResolver.ResolveDisplayText(
                control, PreviewValueProvider, PreviewValueTypeProvider, PreviewValueUnitProvider);
            if (!string.IsNullOrEmpty(previewText))
                label = previewText;
        }

        // Dropdown：显示选中项文本（选项列表优先于静态文本）
        if (control.Type == HmiControlType.Dropdown && !string.IsNullOrEmpty(control.Options))
        {
            var opts = control.Options.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (opts.Length > 0)
                label = opts[Math.Clamp(control.SelectedIndex, 0, opts.Length - 1)];
        }

        // TextArea：无文本时显示占位文本
        if (control.Type == HmiControlType.TextArea &&
            string.IsNullOrEmpty(control.Text) && !string.IsNullOrEmpty(control.PlaceholderText))
        {
            label = control.PlaceholderText;
        }

        // 文本颜色：状态样式覆盖优先（Checked 预览态），回退到控件 TextColor，最后主题文本色兜底。
        // 与 RenderButton 分支语义一致 —— 此前硬编码纯白导致设计器内设置文本色不生效。
        var previewState = control.IsChecked ? VisualState.Checked : VisualState.Normal;
        var stateStyle = control.GetStateStyle(previewState);
        // 文本颜色/对齐：规则抽到 ControlTextStyleResolver（状态样式优先 → 控件默认 → 主题/调色板兜底），
        // 由行为测试覆盖，不再依赖对渲染源码的 grep 断言。
        // 绑定表达式驱动的文本色优先级最高（表达式就是要覆盖设计值）；
        // 其后才是 状态样式 → 控件默认 → 主题/调色板兜底。
        var textColor = EffectiveVisualColor(control, nameof(HmiControl.TextColor), null)
            ?? ControlTextStyleResolver.PreferHex(stateStyle, control, Theme?.TextColor);
        var hAlign = ControlTextStyleResolver.HorizontalAlign(stateStyle, control, previewState);
        var vAlign = ControlTextStyleResolver.VerticalAlign(stateStyle, control, previewState);

        var fontSz = (control.FontSize > 0 ? control.FontSize : HmiControl.DefaultFontSize) * z;
        DrawAlignedText(context, label, rect, fontSz,
            ParseColorHex(textColor, ThemeFallback(Theme?.TextColor, DesignPalette.Text)), hAlign, vAlign);
    }

    // ── 表达式驱动的视觉属性（P1-1）──
    //  预览模式下，颜色/可见性可由绑定表达式驱动（如 temp > 100 ? 16711680 : 65280）。
    //  编辑态一律用静态设计值 —— 与 DisplayText 同一隔离契约（PreviewValueResolver 不写模型）。
    //  这两个方法是**画布侧的唯一接缝**：所有直读 control.XxxColor / control.IsVisible 的渲染分支
    //  都必须经它们取值，否则会出现「有的控件预览变色、有的不变」这种半生效状态。

    /// <summary>预览模式下由绑定驱动的颜色（<c>#RRGGBB</c>）；未绑定/求值失败时回退静态设计值。</summary>
    private string? EffectiveVisualColor(HmiControl control, string propertyName, string? staticValue)
        => IsPreviewMode
            ? PreviewValueResolver.ResolveVisualColor(control, PreviewValueProvider, propertyName) ?? staticValue
            : staticValue;

    /// <summary>预览模式下由绑定驱动的可见性；未绑定/求值失败时回退 <see cref="HmiControl.IsVisible"/>。</summary>
    private bool EffectiveVisible(HmiControl control)
        => IsPreviewMode
            ? PreviewValueResolver.ResolveVisualVisibility(control, PreviewValueProvider) ?? control.IsVisible
            : control.IsVisible;

    private static IBrush ParseColor(string? hexColor, Color fallback)
    {
        if (string.IsNullOrEmpty(hexColor)) return RenderResourceCache.Brush(fallback);
        try
        {
            if (hexColor.StartsWith('#'))
            {
                return RenderResourceCache.Brush(Color.Parse(hexColor));
            }
            return RenderResourceCache.Brush(Color.Parse("#" + hexColor.TrimStart('#')));
        }
        catch
        {
            return RenderResourceCache.Brush(fallback);
        }
    }

    private static Color ParseColorHex(string? hexColor, Color fallback)
    {
        if (string.IsNullOrEmpty(hexColor)) return fallback;
        try
        {
            return Color.Parse(hexColor.StartsWith('#') ? hexColor : "#" + hexColor);
        }
        catch { return fallback; }
    }

    /// <summary>
    /// 主题优先取色：主题对应色（非空）→ 默认兜底。
    /// 供渲染层消费已注入的 HmiTheme（P1-2：设计器渲染层接入主题，兜底色随主题变化）。
    /// </summary>
    private static Color ThemeFallback(string? themeColor, Color fallback)
        => string.IsNullOrEmpty(themeColor) ? fallback : ParseColorHex(themeColor, fallback);
}

/// <summary>
/// 控件拖放事件参数 —— 携带控件类型和模型坐标。
/// </summary>
public sealed class ControlDroppedEventArgs : EventArgs
{
    public HmiControlType ControlType { get; }
    public double X { get; }
    public double Y { get; }

    public ControlDroppedEventArgs(HmiControlType controlType, double x, double y)
    {
        ControlType = controlType;
        X = x;
        Y = y;
    }
}

/// <summary>
/// 模板拖放事件参数 —— 携带模板 ID 和模型坐标。
/// </summary>
public sealed class TemplateDroppedEventArgs : EventArgs
{
    public string TemplateId { get; }
    public double X { get; }
    public double Y { get; }

    public TemplateDroppedEventArgs(string templateId, double x, double y)
    {
        TemplateId = templateId;
        X = x;
        Y = y;
    }
}

/// <summary>
/// 资源拖放事件参数 —— 携带资源 ID 和模型坐标。
/// </summary>
public sealed class ResourceDroppedEventArgs : EventArgs
{
    public string ResourceId { get; }
    public double X { get; }
    public double Y { get; }

    public ResourceDroppedEventArgs(string resourceId, double x, double y)
    {
        ResourceId = resourceId;
        X = x;
        Y = y;
    }
}

/// <summary>
/// I10: 绘图提交事件参数 —— 携带已创建的控件实例（已加入 Screen.Controls，宿主负责撤销/刷新）。
/// 控件已按网格吸附并配置默认几何；顶点为控件局部坐标（0..Width / 0..Height）。
/// </summary>
public sealed class ControlDrawnEventArgs : EventArgs
{
    public HmiControl Control { get; }
    public HmiControlType ControlType => Control.Type;
    public IReadOnlyList<PointData>? Points => Control.Points;

    public ControlDrawnEventArgs(HmiControl control)
    {
        Control = control;
    }
}
