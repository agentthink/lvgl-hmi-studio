using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HmiEditor.App.Services;

namespace HmiEditor.App.Views;

/// <summary>
/// 启动引导窗口 —— 应用启动时首先显示，提供"新建工程"和"打开工程"两个主要入口。
/// 用户做出选择后，窗口关闭并通过 <see cref="SelectedAction"/> 返回结果给 App。
/// </summary>
public partial class SplashWindow : Window
{
    /// <summary>用户在引导页面选择的操作类型。</summary>
    public enum StartupAction
    {
        /// <summary>用户关闭了窗口（退出应用）。</summary>
        Exit,
        /// <summary>选择新建工程。</summary>
        NewProject,
        /// <summary>选择打开工程（弹出文件选择对话框）。</summary>
        OpenProject,
        /// <summary>选择打开指定路径的工程文件（从最近列表）。</summary>
        OpenRecent,
    }

    /// <summary>用户选择的操作。在窗口关闭后由 App 读取。</summary>
    public StartupAction SelectedAction { get; private set; } = StartupAction.Exit;

    /// <summary>当 SelectedAction == OpenRecent 时，要打开的工程文件路径。</summary>
    public string? SelectedFilePath { get; private set; }

    private readonly SettingsService _settings;

    /// <summary>仅用于 XAML 设计器，不要直接使用。</summary>
    public SplashWindow()
    {
        _settings = null!;
        InitializeComponent();
    }

    public SplashWindow(SettingsService settings) : this()
    {
        _settings = settings;
        LoadRecentProjects();
        VersionText.Text = $"v{GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";
    }

    // ── 标题栏拖动与关闭 ──

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.IsPrimary)
            BeginMoveDrag(e);
    }

    private void OnTitleCloseClicked(object? sender, RoutedEventArgs e)
    {
        SelectedAction = StartupAction.Exit;
        Close();
    }

    // ── 主操作卡片 ──

    private void OnNewProjectClicked(object? sender, PointerPressedEventArgs e)
    {
        SelectedAction = StartupAction.NewProject;
        Close();
    }

    private void OnOpenProjectClicked(object? sender, PointerPressedEventArgs e)
    {
        SelectedAction = StartupAction.OpenProject;
        Close();
    }

    // ── 最近工程 ──

    private void LoadRecentProjects()
    {
        var recent = _settings.RecentProjects.List;
        if (recent.Count == 0) return;

        var items = new List<RecentProjectItem>();
        foreach (var path in recent)
        {
            if (!File.Exists(path)) continue;
            var info = new FileInfo(path);
            items.Add(new RecentProjectItem
            {
                FilePath = path,
                FileName = Path.GetFileNameWithoutExtension(path),
                Directory = Path.GetDirectoryName(path) ?? "",
                ModifiedText = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
            });
        }

        if (items.Count > 0)
        {
            RecentSection.IsVisible = true;
            RecentList.ItemsSource = items;
        }
    }

    private void OnRecentProjectClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: RecentProjectItem item })
        {
            SelectedAction = StartupAction.OpenRecent;
            SelectedFilePath = item.FilePath;
            Close();
        }
    }
}

/// <summary>最近工程列表项数据模型（顶级类，供 XAML DataTemplate 绑定）。</summary>
public sealed class RecentProjectItem
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Directory { get; set; } = "";
    public string ModifiedText { get; set; } = "";
}
