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

/// <summary>撤销重做/菜单/工具栏/快捷键/语言主题事件。</summary>
/// <remarks>
/// 自 MainWindow.WireEvents()（原 992 行单方法）按域拆出，纯代码搬移：
/// 方法体逐行未改，WireEvents() 只保留对这几个分域方法的调用。
/// </remarks>
public partial class MainWindow
{
    private void WireCommandAndMenuEvents()
    {
        // ── 撤销/重做（菜单 + 工具栏） ──
        EventHandler<RoutedEventArgs> undoHandler = (_, _) =>
        {
            _historyReplay = true;
            try { _undoRedoManager.Undo(); }
            finally { _historyReplay = false; }
            SyncPropertySnapshots();
            RefreshPanelsAfterEdit();
        };
        EventHandler<RoutedEventArgs> redoHandler = (_, _) =>
        {
            _historyReplay = true;
            try { _undoRedoManager.Redo(); }
            finally { _historyReplay = false; }
            SyncPropertySnapshots();
            RefreshPanelsAfterEdit();
        };
        MenuUndo.Click += undoHandler;
        MenuRedo.Click += redoHandler;
        BtnUndo.Click += undoHandler;
        BtnRedo.Click += redoHandler;

        // ── 菜单事件 ──
        MenuNewProject.Click += (_, _) => NewProject();
        MenuOpenProject.Click += (_, _) => OpenProject();
        MenuSaveProject.Click += (_, _) => SaveProject();
        MenuExportCode.Click += (_, _) => ExportCode();
        MenuCompile.Click += (_, _) => CompileProject();
        BtnCompile.Click += (_, _) => CompileProject();
        MenuBuildRun.Click += (_, _) => BuildAndRunSimulator();
        BtnBuildRun.Click += (_, _) => BuildAndRunSimulator();
        MenuDownload.Click += (_, _) => DownloadToHmi();
        BtnDownloadHmi.Click += (_, _) => DownloadToHmi();
        MenuExit.Click += (_, _) => Close();

        // ── I8: Git 工作流菜单 ──
        MenuGitInit.Click += (_, _) => InitGitRepository();
        MenuGitCommit.Click += (_, _) => GitCommitDialog();
        MenuGitHistory.Click += (_, _) => ShowGitHistory();
        MenuGitRefresh.Click += (_, _) => RefreshGitState();

        // ── I9: 自适应分辨率预览 ──
        CboResolutionPreview.SelectionChanged += (_, _) => OnResolutionPreviewChanged();

        // ── I11: 验证生成代码 / 导出模拟器工程 ──
        MenuValidateCode.Click += (_, _) => ValidateGeneratedCode();
        MenuSimulatorExport.Click += (_, _) => ExportSimulatorProject();

        // ── 工具栏文件按钮 ──
        BtnNewProject.Click += (_, _) => NewProject();
        BtnOpenProject.Click += (_, _) => OpenProject();
        BtnSaveProject.Click += (_, _) => SaveProject();

        MenuDelete.Click += (_, _) => DeleteCurrentControl();
        MenuSelectAll.Click += (_, _) => CanvasPanel.Canvas.SelectAllControls();

        // 窗口级快捷键：Delete 删除控件、Ctrl+A 全选、Ctrl+=/Ctrl+- 缩放、Ctrl+L 聚焦工具箱搜索。
        // 这些手势不再绑定到菜单 InputGesture，统一在此处理，不依赖画布焦点。
        KeyDown += OnWindowKeyDown;

        MenuZoomIn.Click += (_, _) => SetZoom(_zoomLevel + 0.25);
        MenuZoomOut.Click += (_, _) => SetZoom(_zoomLevel - 0.25);
        MenuZoomReset.Click += (_, _) => SetZoom(1.0);

        MenuAbout.Click += (_, _) =>
        {
            var msgBox = new Window
            {
                Title = LocService["menu.help.about"],
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBlock
                {
                    Text = $"{Core.VersionInfo.DisplayVersion}\n\n" +
                           $"{LocService["app.about"]}\n" +
                           LocService["app.slogan"],
                    TextAlignment = Avalonia.Media.TextAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Foreground = Avalonia.Media.Brushes.White,
                    Background = Avalonia.Media.Brushes.Transparent
                }
            };
            msgBox.ShowDialog(this);
        };

        // ── 语言切换 ──
        MenuLangZhCN.Click += (_, _) =>
        {
            LocService.SwitchLanguage("zh-CN");
            MenuLangZhCN.IsChecked = true;
            MenuLangEnUS.IsChecked = false;
            Title = LocService["app.title"] + $" — {_currentProject.Name}";
        };
        MenuLangEnUS.Click += (_, _) =>
        {
            LocService.SwitchLanguage("en-US");
            MenuLangZhCN.IsChecked = false;
            MenuLangEnUS.IsChecked = true;
            Title = LocService["app.title"] + $" — {_currentProject.Name}";
        };

        // ── 主题切换 ──
        UpdateThemeCheckmarks();

        MenuThemeDark.Click += (_, _) =>
        {
            App.ThemeService.SetTheme(AppThemeService.HmiTheme.Dark);
            UpdateThemeCheckmarks();
            Title = LocService["app.title"] + $" — {_currentProject.Name}";
        };
        MenuThemeOcean.Click += (_, _) =>
        {
            App.ThemeService.SetTheme(AppThemeService.HmiTheme.Ocean);
            UpdateThemeCheckmarks();
            Title = LocService["app.title"] + $" — {_currentProject.Name}";
        };
        MenuThemeLight.Click += (_, _) =>
        {
            App.ThemeService.SetTheme(AppThemeService.HmiTheme.Light);
            UpdateThemeCheckmarks();
            Title = LocService["app.title"] + $" — {_currentProject.Name}";
        };
    }
}
