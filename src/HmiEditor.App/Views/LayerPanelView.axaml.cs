using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.Core.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace HmiEditor.App.Views;

/// <summary>
/// 层管理器面板（R2-T1）—— 按 Z 序展示所有控件，支持锁定/隐藏/重排。
///
/// 控件列表按 LayerIndex 降序排列（上层在前）。
/// 选中控件时同步选中画布上的对应控件。
/// </summary>
public partial class LayerPanelView : UserControl
{
    private HmiScreen? _screen;
    private readonly ObservableCollection<HmiControl> _layers = new();

    /// <summary>层选中变化事件 —— 通知画布高亮对应控件。</summary>
    public event EventHandler<HmiControl?>? SelectionChanged;

    /// <summary>层属性变化事件 —— 通知画布重绘。</summary>
    public event EventHandler? LayerChanged;

    public LayerPanelView()
    {
        InitializeComponent();
        LayerList.ItemsSource = _layers;

        // 转发事件到 DockLayoutState 聚合器，确保 Dock 重建后仍能到达 MainWindow
        SelectionChanged += (_, control) =>
            DockLayoutState.RaiseLayerSelectionChanged(this, control);
        LayerChanged += (_, _) =>
            DockLayoutState.RaiseLayerChanged(this, EventArgs.Empty);
    }

    /// <summary>加载屏幕数据，刷新层列表。</summary>
    public void LoadScreen(HmiScreen? screen)
    {
        _screen = screen;
        Refresh();
    }

    /// <summary>刷新层列表（按 LayerIndex 降序排列）。</summary>
    public void Refresh()
    {
        if (_screen == null) return;

        var selectedId = LayerList.SelectedItem is HmiControl sc ? sc.Id : null;

        _layers.Clear();
        // 按 LayerIndex 降序排列（上层在前），同层按列表顺序倒序（后添加的在上层）
        var sorted = _screen.Controls
            .Select((c, idx) => new { Control = c, ListIndex = idx })
            .OrderByDescending(x => x.Control.LayerIndex)
            .ThenByDescending(x => x.ListIndex)
            .Select(x => x.Control);

        foreach (var ctrl in sorted)
            _layers.Add(ctrl);

        // 恢复选中
        if (selectedId != null)
        {
            var target = _layers.FirstOrDefault(c => c.Id == selectedId);
            if (target != null)
                LayerList.SelectedItem = target;
        }
    }

    // ── 选中同步 ──

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = LayerList.SelectedItem as HmiControl;
        SelectionChanged?.Invoke(this, selected);
    }

    /// <summary>从画布选中变化同步到层面板。</summary>
    public void SelectControl(HmiControl? control)
    {
        if (control == null) return;
        var target = _layers.FirstOrDefault(c => c.Id == control.Id);
        if (target != null && LayerList.SelectedItem != target)
            LayerList.SelectedItem = target;
    }

    // ── 可见性/锁定切换 ──

    private void OnToggleVisible(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            var ctrl = _layers.FirstOrDefault(c => c.Id == id);
            if (ctrl != null)
            {
                ctrl.IsVisible = !ctrl.IsVisible;
                LayerChanged?.Invoke(this, EventArgs.Empty);
                Refresh();
            }
        }
    }

    private void OnToggleLock(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            var ctrl = _layers.FirstOrDefault(c => c.Id == id);
            if (ctrl != null)
            {
                ctrl.IsLocked = !ctrl.IsLocked;
                LayerChanged?.Invoke(this, EventArgs.Empty);
                Refresh();
            }
        }
    }

    // ── 层排序 ──

    private void OnMoveUp(object? sender, RoutedEventArgs e)
    {
        if (LayerList.SelectedItem is HmiControl ctrl)
        {
            ctrl.LayerIndex++;
            LayerChanged?.Invoke(this, EventArgs.Empty);
            Refresh();
        }
    }

    private void OnMoveDown(object? sender, RoutedEventArgs e)
    {
        if (LayerList.SelectedItem is HmiControl ctrl)
        {
            ctrl.LayerIndex--;
            LayerChanged?.Invoke(this, EventArgs.Empty);
            Refresh();
        }
    }

    private void OnLockAll(object? sender, RoutedEventArgs e)
    {
        foreach (var ctrl in _layers)
            ctrl.IsLocked = true;
        LayerChanged?.Invoke(this, EventArgs.Empty);
        Refresh();
    }

    private void OnUnlockAll(object? sender, RoutedEventArgs e)
    {
        foreach (var ctrl in _layers)
            ctrl.IsLocked = false;
        LayerChanged?.Invoke(this, EventArgs.Empty);
        Refresh();
    }
}
