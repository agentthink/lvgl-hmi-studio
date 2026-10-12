using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HmiEditor.App.Services;
using HmiEditor.App.Views;
using HmiEditor.Core.Services;

namespace HmiEditor.App;

public partial class App : Application
{
    /// <summary>全局主题服务，启动时初始化并恢复上次保存的主题。</summary>
    public static AppThemeService ThemeService { get; private set; } = null!;

    /// <summary>全局设置服务，启动时初始化并恢复上次保存的用户偏好。</summary>
    public static SettingsService SettingsService { get; private set; } = null!;

    /// <summary>
    /// **运行期数据源注册表**（阶段 185）：审计/报警在 <c>MainWindow</c> 构造时注册，
    /// 历史采样由 <c>TrendConfigView</c> 开记录时注册、窗口关闭时注销。
    ///
    /// <para>⚠ 与 <see cref="SettingsService"/> 不同，它**内联初始化**（不依赖应用启动）——
    /// 判据里可以随时注册/取数，不必先建一个 App；它本身也不持有应用状态（只是一组委托）。</para>
    /// </summary>
    public static ReportSourceRegistry ReportSources { get; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var dataDir = AppPaths.DataRoot;
        Directory.CreateDirectory(dataDir);

        // 清理上次崩溃 / 被强杀留下的**孤儿会话临时目录**（TempRoot/.sessions/<pid>-<token>）。
        // 正常退出由 MainWindow.CleanupOnShutdown 精确释放，这里兜的是异常路径。
        // 放后台：纯 IO 扫描，不该拖慢启动；清理失败也不阻断（下次再试）。
        // 只扫 .sessions，稳定名工作目录（so-build/compile/download/sim）不在面内，增量复用不受影响。
        var processStartedAt = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        _ = Task.Run(() =>
        {
            try
            {
                AppPaths.PruneStaleSessions();

                // 另一类残留：第十轮～第十一轮之间平铺在临时根**直下**的 <prefix>-<guid> 目录。
                // 现任代码不再产生该形态（唯一名进 .sessions、稳定名不带 guid 后缀）⇒ 按形态清是幂等的。
                // cutoff 取本进程启动时刻：另一个还在运行的实例刚建的目录要留着（它可能正在用）。
                AppPaths.PruneLegacyFlatWorkDirs(processStartedAt);
            }
            catch
            {
                // 孤儿清理失败不影响启动
            }
        });

        var themePath = Path.Combine(dataDir, "theme.json");
        ThemeService = new AppThemeService(themePath);
        ThemeService.Restore();

        var settingsPath = Path.Combine(dataDir, "settings.json");
        SettingsService = new SettingsService(settingsPath);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ── 启动引导流程 ──
            // 首先显示 SplashWindow 引导页面，用户选择"新建工程"或"打开工程"后，
            // 再创建 MainWindow 主窗口并执行对应操作。
            var splash = new SplashWindow(SettingsService);
            desktop.MainWindow = splash;
            splash.Closed += (_, _) =>
            {
                // 用户关闭引导窗口且未选择任何操作 → 退出应用
                if (splash.SelectedAction == SplashWindow.StartupAction.Exit)
                {
                    desktop.Shutdown();
                    return;
                }

                // 创建主窗口
                var mainWindow = new MainWindow(SettingsService);
                desktop.MainWindow = mainWindow;
                mainWindow.Show();

                // 根据用户选择执行对应操作
                switch (splash.SelectedAction)
                {
                    case SplashWindow.StartupAction.NewProject:
                        mainWindow.StartWithNewProject();
                        break;
                    case SplashWindow.StartupAction.OpenProject:
                        mainWindow.StartWithOpenProject();
                        break;
                    case SplashWindow.StartupAction.OpenRecent:
                        if (!string.IsNullOrEmpty(splash.SelectedFilePath))
                            mainWindow.StartWithOpenFile(splash.SelectedFilePath);
                        else
                            mainWindow.StartWithNewProject();
                        break;
                }
            };

            desktop.ShutdownRequested += OnShutdownRequested;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // 确保所有后台服务在应用退出前被清理
        if (sender is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is MainWindow mainWindow)
        {
            mainWindow.CleanupOnShutdown();
        }
    }
}
