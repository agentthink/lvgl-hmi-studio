using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HmiEditor.App.Services;

namespace HmiEditor.App.Controls;

/// <summary>
/// 无边框圆角窗口基类 —— 统一封装自定义标题栏（拖拽 + 关闭）和八向缩放手柄。
/// <para>
/// 子窗口 XAML 结构：
/// <code>
/// <cw:ChromeWindow ...>
///   <Border CornerRadius="{StaticResource HmiWindowCornerRadius}" ...>
///     <Grid RowDefinitions="36,*">
///       <Border Grid.Row="0" chrome:WindowDecorationProperties.ElementRole="TitleBar"
///               PointerPressed="OnTitleBarPointerPressed">...</Border>
///       <!-- 内容 -->
///     </Grid>
///   </Border>
/// </cw:ChromeWindow>
/// </code>
/// </para>
/// <para>
/// 缩放手柄由基类在窗口首次打开时自动注入（四边 4px + 四角 10px 透明命中区），
/// 通过 Window.BeginResizeDrag 平台无关 API 驱动缩放（不依赖 WM 装饰角色，
/// Linux/X11 无边框 + 透明窗口下装饰角色不生效，2026-08-18 实机定位）。
/// </para>
/// <para>
/// 窗口几何（位置/大小/最大化）在打开时恢复、关闭时保存（2026-08-18 新增），
/// 由 <see cref="WindowGeometryService"/> 按窗口类型名持久化。
/// </para>
/// </summary>
public abstract class ChromeWindow : Window
{
    private bool _geometryRestored;

    protected ChromeWindow()
    {
        // 无边框 + 透明背景：允许外层 Border 绘制圆角
        WindowDecorations = WindowDecorations.None;
        ExtendClientAreaToDecorationsHint = true;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = null;
        CanResize = true;

        // 子窗口几何持久化：打开后恢复，关闭前保存
        Opened += (_, _) => RestoreGeometry();
        Closing += OnClosingPersistGeometry;
    }

    /// <summary>标题栏左键按下：平台未通过 TitleBar 角色处理时回退到手动拖动。</summary>
    protected void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    /// <summary>标题栏关闭按钮（无系统标题栏，需自绘关闭入口）。</summary>
    protected void OnTitleCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RestoreGeometry()
    {
        if (_geometryRestored) return;
        _geometryRestored = true;

        try
        {
            var geo = WindowGeometryService.Load(GetType().FullName ?? GetType().Name);
            if (geo == null) return;

            // 显式恢复位置 → 禁用 CenterOwner 的自动定位，避免覆盖保存的位置
            if (geo.X != 0 || geo.Y != 0)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new PixelPoint((int)geo.X, (int)geo.Y);
            }
            if (geo.Width > 0) Width = geo.Width;
            if (geo.Height > 0) Height = geo.Height;
            if (Enum.TryParse<WindowState>(geo.WindowState, out var state))
                WindowState = state;
        }
        catch { /* 恢复失败使用默认布局 */ }
    }

    private void OnClosingPersistGeometry(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            var geo = new WindowGeometry
            {
                X = Position.X,
                Y = Position.Y,
                Width = Width,
                Height = Height,
                WindowState = WindowState.ToString(),
            };
            WindowGeometryService.Save(GetType().FullName ?? GetType().Name, geo);
        }
        catch { /* 非关键功能 */ }
    }

    /// <summary>窗口首次打开后注入八向缩放手柄（无边框窗口需自绘命中区才能鼠标缩放）。</summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        InjectResizeGrips();
    }

    private void InjectResizeGrips()
    {
        // 外壳结构固定为：Window.Content = Border(圆角外壳)，Border.Child = Grid(RowDefinitions="36,*")
        if (Content is not Border shell || shell.Child is not Grid root)
            return;

        var overlay = new Grid();
        Grid.SetRowSpan(overlay, 2);

        // 四边手柄：宽度/高度 4px，透明命中区
        AddGrip(overlay, WindowEdge.West, HorizontalAlignment.Left, VerticalAlignment.Stretch, width: 4, height: 0);
        AddGrip(overlay, WindowEdge.East, HorizontalAlignment.Right, VerticalAlignment.Stretch, width: 4, height: 0);
        AddGrip(overlay, WindowEdge.North, HorizontalAlignment.Stretch, VerticalAlignment.Top, width: 0, height: 4);
        AddGrip(overlay, WindowEdge.South, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, width: 0, height: 4);

        // 四角手柄：10×10px，保证拖拽边角缩放时的舒适命中区
        AddGrip(overlay, WindowEdge.NorthWest, HorizontalAlignment.Left, VerticalAlignment.Top, width: 10, height: 10);
        AddGrip(overlay, WindowEdge.NorthEast, HorizontalAlignment.Right, VerticalAlignment.Top, width: 10, height: 10);
        AddGrip(overlay, WindowEdge.SouthWest, HorizontalAlignment.Left, VerticalAlignment.Bottom, width: 10, height: 10);
        AddGrip(overlay, WindowEdge.SouthEast, HorizontalAlignment.Right, VerticalAlignment.Bottom, width: 10, height: 10);

        root.Children.Add(overlay);
    }

    private void AddGrip(
        Grid overlay,
        WindowEdge edge,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical,
        double width,
        double height)
    {
        var grip = new Border
        {
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            Width = width,
            Height = height,
            Background = Brushes.Transparent,
        };
        // 平台无关缩放：BeginResizeDrag 由 Avalonia 后端驱动（X11/Win32/macOS 均支持）
        grip.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
                BeginResizeDrag(edge, e);
        };
        overlay.Children.Add(grip);
    }
}
