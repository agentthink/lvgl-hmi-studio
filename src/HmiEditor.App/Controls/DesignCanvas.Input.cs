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
/// DesignCanvas 分部 —— 指针交互、命中测试、选择与拖放。
/// 含 OnPointer* 重写、框选（marquee）、长按选中、选择状态维护，
/// 以及 Toolbox / FaceplateLibrary / 资源面板拖入画布的处理。
/// </summary>
public partial class DesignCanvas : Control
{
    // ── 命中测试 ──

    private HmiControl? HitTestControl(Point point)
    {
        var screen = Screen;
        if (screen == null) return null;

        // 补偿平移偏移
        var adjustedPoint = new Point(point.X - PanOffsetX, point.Y - PanOffsetY);
        var z = EffectiveZoom;
        // 倒序遍历（上层控件优先选中）
        // R2-T1: 按 LayerIndex 降序排序后命中测试，跳过锁定/隐藏控件
        var sorted = screen.Controls
            .Select((c, idx) => new { Control = c, ListIndex = idx })
            .Where(x => !x.Control.IsLocked && x.Control.IsVisible)
            .OrderByDescending(x => x.Control.LayerIndex)
            .ThenByDescending(x => x.ListIndex);

        foreach (var item in sorted)
        {
            var ctrl = item.Control;
            var rect = new Rect(ctrl.X * z, ctrl.Y * z, ctrl.Width * z, ctrl.Height * z);
            if (ctrl.HasTransform)
            {
                // 旋转/镜像后控件不再轴对齐：把点击点逆变换到控件局部坐标判断命中。
                // 旋转 90/270 后局部区域仍为 [0,W]x[0,H]（内容映射对称、无偏移）。
                var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
                var m = BuildControlTransform(center, ctrl.EffectiveRotation(), ctrl.MirrorX, ctrl.MirrorY);
                var inv = m.Invert();
                var local = adjustedPoint.Transform(inv);
                if (local.X >= rect.X && local.X <= rect.X + rect.Width &&
                    local.Y >= rect.Y && local.Y <= rect.Y + rect.Height)
                    return ctrl;
            }
            else if (rect.Contains(adjustedPoint))
            {
                return ctrl;
            }
        }
        return null;
    }

    private int HitTestHandle(Point point)
    {
        var adorner = Adorner;
        if (adorner == null || !adorner.IsSelectionVisible) return -1;

        var hs = adorner.HandleSize + 4; // 加 4px 容差
        var positions = adorner.GetHandlePositions();
        for (int i = 0; i < positions.Length; i++)
        {
            var handleRect = new Rect(
                positions[i].X - hs / 2, positions[i].Y - hs / 2,
                hs, hs);
            if (handleRect.Contains(point))
                return i;
        }
        return -1;
    }

    // ── 指针事件处理 ──

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // 请求焦点：点击画布时接管键盘输入（用于 Delete 删除控件等快捷键）
        Focus();

        // R2-T2: 预览模式下禁用所有编辑交互，仅路由控件点击 → 动作执行
        if (IsPreviewMode)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && Screen != null)
            {
                var previewPos = e.GetPosition(this);
                var previewHit = HitTestControl(previewPos);
                if (previewHit != null)
                {
                    PreviewControlClicked?.Invoke(this, previewHit);
                    e.Handled = true;
                }
            }
            return;
        }

        var pointProps = e.GetCurrentPoint(this);
        if (pointProps.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition(this);
            e.Handled = true;
            return;
        }

        if (Screen == null) return;

        var pos = e.GetPosition(this);
        var adorner = Adorner;

        // I10: 绘图工具模式 —— 左键按下即开始绘制（跳过选中/手柄命中，允许在已有控件上叠画）
        if (DrawTool.HasValue && pointProps.Properties.IsLeftButtonPressed)
        {
            // Line / Polyline 采用"点击式"绘制：每次左键单击追加一个顶点，
            // 不进入拖拽（_isDrawing）。其余工具保持原有拖拽式。
            if (DrawTool.Value is HmiControlType.Line or HmiControlType.Polyline)
            {
                AppendClickPoint(pos);
                e.Handled = true;
                return;
            }

            _isDrawing = true;
            _drawStartPoint = pos;
            _drawCurrentPoint = pos;
            e.Handled = true;
            return;
        }

        // 1. 先检测是否点击了缩放手柄（仅单选时显示手柄）
        if (adorner != null && adorner.IsSelectionVisible && _selectedControls.Count == 1)
        {
            int handleIndex = HitTestHandle(pos);
            if (handleIndex >= 0 && _selectedControl != null)
            {
                _dragMode = DragMode.Resize;
                _dragControl = _selectedControl;
                _dragStartPoint = pos;
                _dragStartBounds = new Rect(
                    _selectedControl.X, _selectedControl.Y,
                    _selectedControl.Width, _selectedControl.Height);
                _activeHandleIndex = handleIndex;
                e.Handled = true;
                return;
            }
        }

        // 2. 检测是否点击了控件
        var hit = HitTestControl(pos);
        if (hit != null)
        {
            // Ctrl+点击 → 切换选中状态（组合模式：切换整个组合）
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var targets = ResolveSelectionTargets(hit);
                foreach (var t in targets)
                {
                    if (_selectedControls.Contains(t))
                        _selectedControls.Remove(t);
                    else
                        _selectedControls.Add(t);
                }
            }
            else
            {
                // 非 Ctrl 点击 → 如果当前选中集未完全覆盖该控件所属组合，则替换
                var targets = ResolveSelectionTargets(hit);
                var alreadySelected = targets.All(t => _selectedControls.Contains(t));
                if (!alreadySelected)
                {
                    _selectedControls.Clear();
                    foreach (var t in targets)
                        _selectedControls.Add(t);
                }
            }

            FireSelectionChanged();
            UpdateMultiAdorner();

            _dragMode = DragMode.Move;
            _dragControl = hit;
            _dragStartPoint = pos;
            _dragStartBounds = new Rect(hit.X, hit.Y, hit.Width, hit.Height);
            // 记录所有选中控件的起始位置（多选整体移动）
            _dragStartAllBounds = new Dictionary<HmiControl, Rect>();
            foreach (var sc in _selectedControls)
                _dragStartAllBounds[sc] = new Rect(sc.X, sc.Y, sc.Width, sc.Height);

            e.Handled = true;
            return;
        }

        // 3. 点击空白区域
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Ctrl+空白 → 立即开始框选
            _isRubberBanding = true;
            _rubberBandStart = pos;
            _selectedControls.Clear();
            FireSelectionChanged();
            UpdateMultiAdorner();
            e.Handled = true;
            return;
        }

        // 普通左键空白 → 启动长按检测：按住达到阈值后从按下位置开始框选；
        // 快速点击或移动超容差则按普通点击空白处理（取消选中）。
        StartLongPressSelection(pos);
        e.Handled = true;
        return;
    }

    /// <summary>启动长按框选检测：达到阈值未移动则进入框选模式。</summary>
    private void StartLongPressSelection(Point pos)
    {
        _longPressStart = pos;
        _longPressPending = true;

        _longPressTimer?.Stop();
        _longPressTimer = new DispatcherTimer { Interval = LongPressDelay };
        _longPressTimer.Tick += (_, _) => OnLongPressTriggered();
        _longPressTimer.Start();
    }

    /// <summary>长按阈值到达：从按下位置开始框选（清除当前选中集）。</summary>
    private void OnLongPressTriggered()
    {
        _longPressTimer?.Stop();
        if (!_longPressPending) return;

        _longPressPending = false;
        _isRubberBanding = true;
        _rubberBandStart = _longPressStart;
        _rubberBandRect = null;
        _selectedControls.Clear();
        FireSelectionChanged();
        UpdateMultiAdorner();
        InvalidateVisual();
    }

    /// <summary>取消待定的长按框选（快速点击/移动时调用），并执行普通点击空白的取消选中。</summary>
    private void CancelLongPressSelection()
    {
        _longPressPending = false;
        _longPressTimer?.Stop();
        _longPressTimer = null;

        _selectedControls.Clear();
        FireSelectionChanged();
        ClearSelectionVisuals();

        // 清除参考线
        _currentGuides.Clear();
        if (Adorner != null)
        {
            Adorner.AlignmentGuides = Array.Empty<AlignmentGuide>();
            Adorner.InvalidateVisual();
        }

        _dragMode = DragMode.None;
        _dragControl = null;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var pos = e.GetPosition(this);

        // I10: 点击式绘制（Line/Polyline）—— 更新橡皮筋预览终点
        if (DrawTool.HasValue
            && DrawTool.Value is HmiControlType.Line or HmiControlType.Polyline
            && _clickPoints.Count > 0)
        {
            _clickHoverPoint = pos;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // I10: 绘图拖拽中 —— 更新预览矩形
        if (_isDrawing)
        {
            _drawCurrentPoint = pos;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // 长按框选等待中：指针移动超过容差 → 取消长按（按普通点击空白处理）
        if (_longPressPending)
        {
            if (Math.Abs(pos.X - _longPressStart.X) > LongPressMoveTolerance ||
                Math.Abs(pos.Y - _longPressStart.Y) > LongPressMoveTolerance)
            {
                CancelLongPressSelection();
                UpdateCursor(pos);
                e.Handled = true;
                return;
            }
            return;
        }

        // 框选模式
        if (_isRubberBanding)
        {
            var start = _rubberBandStart;
            var x = Math.Min(start.X, pos.X);
            var y = Math.Min(start.Y, pos.Y);
            var w = Math.Abs(pos.X - start.X);
            var h = Math.Abs(pos.Y - start.Y);
            _rubberBandRect = new Avalonia.Rect(x, y, w, h);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            var panPos = e.GetPosition(this);
            PanOffsetX += panPos.X - _panStartPoint.X;
            PanOffsetY += panPos.Y - _panStartPoint.Y;
            _panStartPoint = panPos;
            InvalidateVisual();
            UpdateAdorner(GetSelectedControl());
            e.Handled = true;
            return;
        }

        var ctrl = _dragControl;

        if (ctrl == null || _dragMode == DragMode.None)
        {
            UpdateCursor(pos);
            return;
        }

        if (_dragMode == DragMode.Move)
        {
            var z = EffectiveZoom;
            var dx = (pos.X - _dragStartPoint.X) / z;
            var dy = (pos.Y - _dragStartPoint.Y) / z;

            // 多选整体移动：所有选中控件按相同偏移量平移
            if (_dragStartAllBounds != null && _dragStartAllBounds.Count > 1)
            {
                foreach (var kv in _dragStartAllBounds)
                {
                    var sc = kv.Key;
                    var orig = kv.Value;
                    sc.X = SnapToGrid(orig.X + dx);
                    sc.Y = SnapToGrid(orig.Y + dy);
                    ControlPropertyChanged?.Invoke(this, sc);
                }
                // 多选时仍以主控件为参考做吸附参考线检测
                ctrl.X = SnapToGrid(_dragStartBounds.X + dx);
                ctrl.Y = SnapToGrid(_dragStartBounds.Y + dy);
            }
            else
            {
                // 单选：标准移动逻辑（带吸附）
                ctrl.X = SnapToGrid(_dragStartBounds.X + dx);
                ctrl.Y = SnapToGrid(_dragStartBounds.Y + dy);
                SnapToGuides(ctrl);
                ControlPropertyChanged?.Invoke(this, ctrl);
            }

            // 检测并更新参考线显示（以主拖拽控件为基准）
            _currentGuides = DetectAlignmentGuides(ctrl, pos);
            if (Adorner != null)
            {
                Adorner.AlignmentGuides = _currentGuides;
                Adorner.InvalidateVisual();
            }
        }
        else if (_dragMode == DragMode.Resize && _activeHandleIndex >= 0)
        {
            ApplyResize(ctrl, pos);
            ControlPropertyChanged?.Invoke(this, ctrl);
        }

        UpdateMultiAdorner();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        // I10: 绘图拖拽结束 —— 提交绘制的图形
        if (_isDrawing)
        {
            CommitDraw(e.GetPosition(this));
            e.Handled = true;
            return;
        }

        // 长按框选等待中快速释放 → 按普通点击空白处理（取消选中）
        if (_longPressPending)
        {
            CancelLongPressSelection();
            e.Handled = true;
            return;
        }

        // 框选结束
        if (_isRubberBanding)
        {
            _isRubberBanding = false;
            _rubberBandRect = null;
            var pos = e.GetPosition(this);
            var selRect = new Avalonia.Rect(
                Math.Min(_rubberBandStart.X, pos.X),
                Math.Min(_rubberBandStart.Y, pos.Y),
                Math.Abs(pos.X - _rubberBandStart.X),
                Math.Abs(pos.Y - _rubberBandStart.Y));

            // 框选语义：从左向右要求完全包含，从右向左允许相交。
            // 隐藏/锁定控件不进入普通选择，避免用户无法编辑或看不到的对象被误选。
            if (Screen != null)
            {
                _selectedControls.Clear();
                var matches = CanvasSelectionPolicy.SelectFromRubberBand(
                    Screen.Controls,
                    selRect,
                    pos.X >= _rubberBandStart.X,
                    EffectiveZoom,
                    PanOffsetX,
                    PanOffsetY);
                _selectedControls.AddRange(matches);
                FireSelectionChanged();
                UpdateMultiAdorner();
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            _isPanning = false;
            _currentGuides.Clear();
            if (Adorner != null)
            {
                Adorner.AlignmentGuides = Array.Empty<AlignmentGuide>();
                Adorner.InvalidateVisual();
            }
            e.Handled = true;
            return;
        }

        if (_dragMode != DragMode.None && _dragControl != null)
        {
            // 拖拽过程实时修改模型，释放时只提交一条历史记录。
            CommitDragHistory();
            ControlPropertyChanged?.Invoke(this, _dragControl);
        }

        _dragMode = DragMode.None;
        _dragControl = null;
        _activeHandleIndex = -1;
        _dragStartAllBounds = null; // 清除多选移动临时状态
        _currentGuides.Clear();
        if (Adorner != null)
        {
            Adorner.AlignmentGuides = Array.Empty<AlignmentGuide>();
            Adorner.InvalidateVisual();
        }
        InvalidateVisual();
    }

    /// <summary>取消当前移动/缩放事务，恢复按下时的全部边界。</summary>
    public void CancelActiveGesture()
    {
        if (_dragStartAllBounds == null) return;
        _historyReplaying = true;
        try
        {
            foreach (var pair in _dragStartAllBounds)
            {
                pair.Key.X = pair.Value.X; pair.Key.Y = pair.Value.Y;
                pair.Key.Width = pair.Value.Width; pair.Key.Height = pair.Value.Height;
            }
        }
        finally
        {
            _historyReplaying = false;
            _dragMode = DragMode.None;
            _dragControl = null;
            _dragStartAllBounds = null;
            _activeHandleIndex = -1;
            _currentGuides.Clear();
            if (Adorner != null) Adorner.AlignmentGuides = Array.Empty<AlignmentGuide>();
            UpdateMultiAdorner();
            InvalidateVisual();
        }
    }

    private void CommitDragHistory()
    {
        if (_historyReplaying || _dragStartAllBounds == null || _dragStartAllBounds.Count == 0) return;

        var before = _dragStartAllBounds.ToDictionary(k => k.Key, v => v.Value);
        var after = before.ToDictionary(pair => pair.Key, pair => new Rect(pair.Key.X, pair.Key.Y, pair.Key.Width, pair.Key.Height));
        if (!before.Any(pair => pair.Value != after[pair.Key])) return;

        void Restore(IReadOnlyDictionary<HmiControl, Rect> values)
        {
            _historyReplaying = true;
            try
            {
                foreach (var pair in values)
                {
                    pair.Key.X = pair.Value.X; pair.Key.Y = pair.Value.Y;
                    pair.Key.Width = pair.Value.Width; pair.Key.Height = pair.Value.Height;
                }
                UpdateMultiAdorner();
                InvalidateVisual();
            }
            finally { _historyReplaying = false; }
        }

        var operation = _dragMode == DragMode.Resize ? AppLocalization.Service[Lang.UndoResizeControl] : AppLocalization.Service[Lang.UndoMoveControl];
        UndoRedoManager.Execute(new SnapshotCommand(operation, () => Restore(before), () => Restore(after)));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var delta = e.Delta.Y;
        if (delta == 0) return;

        // Ctrl+滚轮 缩放
        var keyModifiers = e.KeyModifiers;
        if (keyModifiers.HasFlag(KeyModifiers.Control))
        {
            var mousePos = e.GetPosition(this);
            var viewport = new CanvasViewportState(ZoomLevel, PanOffsetX, PanOffsetY)
                .ZoomAt(mousePos, ZoomLevel + delta * 0.1);
            ZoomLevel = viewport.Zoom;
            PanOffsetX = viewport.PanX;
            PanOffsetY = viewport.PanY;

            InvalidateVisual();
            UpdateAdorner(GetSelectedControl());
            e.Handled = true;
        }
    }

    // ── 缩放操作 ──

    private void ApplyResize(HmiControl ctrl, Point currentPos)
    {
        var z = EffectiveZoom;
        var dx = (currentPos.X - _dragStartPoint.X) / z;
        var dy = (currentPos.Y - _dragStartPoint.Y) / z;

        var x = _dragStartBounds.X;
        var y = _dragStartBounds.Y;
        var w = _dragStartBounds.Width;
        var h = _dragStartBounds.Height;

        switch (_activeHandleIndex)
        {
            case 0: // 左上
                x += dx; y += dy; w -= dx; h -= dy;
                break;
            case 1: // 上中
                y += dy; h -= dy;
                break;
            case 2: // 右上
                y += dy; w += dx; h -= dy;
                break;
            case 3: // 右中
                w += dx;
                break;
            case 4: // 右下
                w += dx; h += dy;
                break;
            case 5: // 下中
                h += dy;
                break;
            case 6: // 左下
                x += dx; w -= dx; h += dy;
                break;
            case 7: // 左中
                x += dx; w -= dx;
                break;
        }

        // 最小尺寸约束：左/上手柄触及最小时固定右/下边，避免鼠标继续移动时控件反向漂移。
        var right = _dragStartBounds.X + _dragStartBounds.Width;
        var bottom = _dragStartBounds.Y + _dragStartBounds.Height;
        if (w < MinControlSize)
        {
            w = MinControlSize;
            if (_activeHandleIndex is 0 or 6 or 7) x = right - w;
        }
        if (h < MinControlSize)
        {
            h = MinControlSize;
            if (_activeHandleIndex is 0 or 1 or 2) y = bottom - h;
        }

        ctrl.X = SnapToGrid(x);
        ctrl.Y = SnapToGrid(y);
        ctrl.Width = SnapToGrid(w);
        ctrl.Height = SnapToGrid(h);

        // 始终基于按下瞬间的起始矩形计算，避免网格吸附误差在每个 PointerMoved 中累积。
        // _dragStartPoint / _dragStartBounds 只在 PointerPressed 时初始化。
    }

    // ── 辅助方法 ──

    private void UpdateCursor(Point pos)
    {
        // I10: 绘图工具激活时使用十字准星
        if (DrawTool.HasValue)
        {
            Cursor = new Cursor(StandardCursorType.Cross);
            return;
        }

        var handleIndex = HitTestHandle(pos);
        Cursor = handleIndex switch
        {
            0 or 2 or 4 or 6 => new Cursor(StandardCursorType.SizeAll), // 角手柄用 SizeAll
            1 or 5 => new Cursor(StandardCursorType.SizeNorthSouth),
            3 or 7 => new Cursor(StandardCursorType.SizeWestEast),
            _ => HitTestControl(pos) != null
                ? new Cursor(StandardCursorType.Hand)
                : new Cursor(StandardCursorType.Arrow)
        };
    }

    /// <summary>
    /// 根据点击命中的控件解析出实际应该选中的控件集合。
    /// 若控件属于某个组合（GroupId 非空），则返回该组合的所有成员；
    /// 否则仅返回控件自身。
    /// </summary>
    private List<HmiControl> ResolveSelectionTargets(HmiControl hit)
    {
        var result = new List<HmiControl>();
        if (hit == null || Screen == null)
        {
            if (hit != null) result.Add(hit);
            return result;
        }

        // 不属于组合 → 返回自身
        if (string.IsNullOrEmpty(hit.GroupId))
        {
            result.Add(hit);
            return result;
        }

        // 属于组合 → 返回组合所有成员
        foreach (var c in Screen.Controls)
        {
            if (c.GroupId == hit.GroupId)
                result.Add(c);
        }

        // 兜底：Screen.Controls 可能暂时找不到同组成员（罕见时序），至少返回命中控件
        if (result.Count == 0)
            result.Add(hit);
        return result;
    }

    private void SelectControl(HmiControl? control)
    {
        _selectedControls.Clear();
        if (control != null)
            _selectedControls.Add(control);
        FireSelectionChanged();
        UpdateMultiAdorner();
    }

    private void UpdateAdorner(HmiControl? control)
    {
        var adorner = Adorner;
        if (adorner == null) return;

        if (control != null)
        {
            var z = EffectiveZoom;
            adorner.SelectionBounds = new Rect(
                control.X * z - 2 + PanOffsetX, control.Y * z - 2 + PanOffsetY,
                control.Width * z + 4, control.Height * z + 4);
            adorner.IsSelectionVisible = true;
        }
        else
        {
            adorner.IsSelectionVisible = false;
        }
        adorner.InvalidateVisual();
    }

    public HmiControl? GetSelectedControl() => _selectedControl;

    /// <summary>返回所有选中的控件。</summary>
    public IReadOnlyList<HmiControl> GetSelectedControls() => _selectedControls.AsReadOnly();

    /// <summary>
    /// 清除所有选中状态。
    /// </summary>
    public void ClearSelection()
    {
        _selectedControls.Clear();
        FireSelectionChanged();
        ClearSelectionVisuals();
    }

    /// <summary>
    /// 全选画布上所有可见控件（Ctrl+A）。
    /// </summary>
    public void SelectAllControls()
    {
        if (Screen == null) return;

        _selectedControls.Clear();
        _selectedControls.AddRange(Screen.Controls.Where(c => c.IsVisible));
        FireSelectionChanged();
        UpdateMultiAdorner();
    }

    /// <summary>触发选中变化事件。</summary>
    private void FireSelectionChanged()
    {
        SelectionChanged?.Invoke(this, _selectedControls.AsReadOnly());
    }

    /// <summary>更新多选装饰器。</summary>
    private void UpdateMultiAdorner()
    {
        if (Adorner == null) return;

        if (_selectedControls.Count == 0)
        {
            Adorner.IsSelectionVisible = false;
            Adorner.InvalidateVisual();
            return;
        }

        if (_selectedControls.Count == 1)
        {
            // 单选 → 显示常规选择框+手柄
            var ctrl = _selectedControls[0];
            Adorner.SelectionBounds = GetControlViewRect(ctrl);
            Adorner.IsSelectionVisible = true;
            Adorner.ShowMultiSelection = false;
            Adorner.InvalidateVisual();
            return;
        }

        // 多选 → 显示组合包围盒（无缩放手柄）
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        var z = EffectiveZoom;

        foreach (var ctrl in _selectedControls)
        {
            var left = ctrl.X * z + PanOffsetX;
            var top = ctrl.Y * z + PanOffsetY;
            var right = left + ctrl.Width * z;
            var bottom = top + ctrl.Height * z;

            if (left < minX) minX = left;
            if (top < minY) minY = top;
            if (right > maxX) maxX = right;
            if (bottom > maxY) maxY = bottom;
        }

        Adorner.SelectionBounds = new Avalonia.Rect(minX - 2, minY - 2, maxX - minX + 4, maxY - minY + 4);
        Adorner.IsSelectionVisible = true;
        Adorner.ShowMultiSelection = true;
        Adorner.InvalidateVisual();
    }

    /// <summary>获取控件视口坐标矩形。</summary>
    private Avalonia.Rect GetControlViewRect(HmiControl ctrl)
    {
        var z = EffectiveZoom;
        return new Avalonia.Rect(
            ctrl.X * z - 2 + PanOffsetX,
            ctrl.Y * z - 2 + PanOffsetY,
            ctrl.Width * z + 4,
            ctrl.Height * z + 4);
    }

    /// <summary>清除选中视觉。</summary>
    private void ClearSelectionVisuals()
    {
        if (Adorner != null)
        {
            Adorner.IsSelectionVisible = false;
            Adorner.ShowMultiSelection = false;
            Adorner.InvalidateVisual();
        }
    }

    /// <summary>
    /// 把坐标对齐到吸附栅格。<see cref="SnapEnabled"/> 关闭时原样返回。
    /// <para>非 static：步长与开关来自应用级设置，由 <c>DesignCanvasView</c> 注入实例。</para>
    /// </summary>
    private double SnapToGrid(double value)
    {
        if (!SnapEnabled) return value;
        return Math.Round(value / SnapStep) * SnapStep;
    }

    // ── 拖放目标处理 ──

    /// <summary>DragOver —— 验证数据格式并设置拖放效果。</summary>
    private void HandleDragOver(DragEventArgs e)
    {
        var typeStr = GetDragData(e.DataTransfer);
        if (typeStr != null && Enum.TryParse<HmiControlType>(typeStr, out var type) && Screen != null)
        {
            var pointer = e.GetPosition(this);
            var model = Coordinates.ScreenToModel(pointer);
            var (width, height) = DefaultSize(type);
            var x = SnapToGrid(model.X - width / 2);
            var y = SnapToGrid(model.Y - height / 2);
            var viewRect = new Rect(x * EffectiveZoom + PanOffsetX, y * EffectiveZoom + PanOffsetY,
                width * EffectiveZoom, height * EffectiveZoom);
            var isValid = x >= 0 && y >= 0 && x + width <= Screen.Width && y + height <= Screen.Height;
            _dragPreview.Show(type, viewRect, isValid);
            InvalidateVisual();
            e.DragEffects = _dragPreview.IsValid ? DragDropEffects.Copy : DragDropEffects.None;
        }
        else
        {
            ClearDragPreview();
            e.DragEffects = (HasTemplateDragFormat(e.DataTransfer) || HasResourceDragFormat(e.DataTransfer))
                ? DragDropEffects.Copy : DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void ClearDragPreview()
    {
        _dragPreview.Clear();
        InvalidateVisual();
    }

    /// <summary>Drop —— 提取控件类型 / 模板 ID / 资源 ID，计算放置坐标并触发相应事件。</summary>
    private void HandleDrop(DragEventArgs e)
    {
        try
        {
        // 先尝试控件格式
        var typeStr = GetDragData(e.DataTransfer);
        if (typeStr != null)
        {
            if (Enum.TryParse<HmiControlType>(typeStr, out var type))
            {
                var pos = e.GetPosition(this);
                var modelPoint = Coordinates.ScreenToModel(pos);
                var modelX = modelPoint.X;
                var modelY = modelPoint.Y;
                ControlDropped?.Invoke(this, new ControlDroppedEventArgs(type, modelX, modelY));
                e.DragEffects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }

        // 再尝试模板格式
        var templateId = GetTemplateDragData(e.DataTransfer);
        if (templateId != null)
        {
            var pos = e.GetPosition(this);
            var (modelX, modelY) = CoordinateTransform.ScreenToModel(pos.X, pos.Y, PanOffsetX, PanOffsetY, EffectiveZoom);
            TemplateDropped?.Invoke(this, new TemplateDroppedEventArgs(templateId, modelX, modelY));
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        // 最后尝试资源库格式
        var resourceId = GetResourceDragData(e.DataTransfer);
        if (resourceId != null)
        {
            var pos = e.GetPosition(this);
            var (modelX, modelY) = CoordinateTransform.ScreenToModel(pos.X, pos.Y, PanOffsetX, PanOffsetY, EffectiveZoom);
            ResourceDropped?.Invoke(this, new ResourceDroppedEventArgs(resourceId, modelX, modelY));
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        e.DragEffects = DragDropEffects.None;
        }
        finally
        {
            ClearDragPreview();
        }
    }

    /// <summary>检查 DataTransfer 是否包含我们的拖拽格式。</summary>
    private static bool HasDragFormat(IDataTransfer dataTransfer)
        => dataTransfer.Formats.Contains(DragDataFormat);

    /// <summary>检查 DataTransfer 是否包含模板拖拽格式。</summary>
    private static bool HasTemplateDragFormat(IDataTransfer dataTransfer)
        => dataTransfer.Formats.Contains(TemplateDragDataFormat);

    /// <summary>检查 DataTransfer 是否包含资源拖拽格式。</summary>
    private static bool HasResourceDragFormat(IDataTransfer dataTransfer)
        => dataTransfer.Formats.Contains(ResourceDragDataFormat);

    /// <summary>从 DataTransfer 中提取拖拽数据（控件类型字符串）。</summary>
    private static string? GetDragData(IDataTransfer dataTransfer)
    {
        foreach (var item in dataTransfer.Items)
        {
            if (item.Formats.Contains(DragDataFormat))
                return item.TryGetRaw(DragDataFormat) as string;
        }
        return null;
    }

    /// <summary>从 DataTransfer 中提取模板拖拽数据（模板 ID 字符串）。</summary>
    private static string? GetTemplateDragData(IDataTransfer dataTransfer)
    {
        foreach (var item in dataTransfer.Items)
        {
            if (item.Formats.Contains(TemplateDragDataFormat))
                return item.TryGetRaw(TemplateDragDataFormat) as string;
        }
        return null;
    }

    /// <summary>从 DataTransfer 中提取资源拖拽数据（资源 ID 字符串）。</summary>
    private static string? GetResourceDragData(IDataTransfer dataTransfer)
    {
        foreach (var item in dataTransfer.Items)
        {
            if (item.Formats.Contains(ResourceDragDataFormat))
            {
                var raw = item.TryGetRaw(ResourceDragDataFormat) as string;
                // 格式："resourceId:<ID>"，去除前缀
                if (raw != null && raw.StartsWith("resourceId:", StringComparison.Ordinal))
                    return raw["resourceId:".Length..];
            }
        }
        return null;
    }
}
