using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using HmiEditor.App.Services;
using HmiEditor.Core.Diagnostics;
using System;
using System.Threading.Tasks;

namespace HmiEditor.App;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // 最早时机安装日志与全局异常处理：启动阶段（含 Avalonia 初始化）的异常
        // 之前完全没有落点，用户侧表现为"闪退且无任何日志"。
        InstallLogging();
        InstallGlobalExceptionHandling();

        try
        {
            // Avalonia 12.x 在 Linux/Wayland 下弹窗定位异常的修复：
            // 启用独立窗口弹出模式（而非嵌入视图），使弹窗能正确定位到屏幕坐标。
            AppContext.SetSwitch("Avalonia.Popups.UsePopupWindow", true);

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLog.Error("Avalonia 启动失败", ex);
            throw;
        }
    }

    /// <summary>安装文件日志器（写 %APPDATA%/zhishu-hmi/logs）。</summary>
    private static void InstallLogging()
    {
        AppLog.Current = new FileLogger(FileLogger.DefaultLogDirectory);
        AppLog.Info($"=== zhishu-hmi 启动 (pid={Environment.ProcessId}, " +
                    $"os={Environment.OSVersion}, clr={Environment.Version}) ===");
    }

    /// <summary>
    /// 安装三层全局异常处理。UI 线程异常记录后标记 Handled，避免一次未处理异常
    /// 直接终止整个编辑器。
    ///
    /// 注意这是「最后一道防线」而非主要手段：视图层的事件处理器异常已由
    /// <see cref="HmiEditor.App.Services.UiOperationGuard"/> 收敛（落盘日志 + 界面可见），
    /// 目前仓库内 35 处 <c>async void</c> 全部自带 try/catch 或走该守卫。
    /// </summary>
    private static void InstallGlobalExceptionHandling()
    {
        // 1) UI 线程（Dispatcher）—— async void 事件处理器的异常最终会到这里
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            AppLog.Error("UI 线程未处理异常（已拦截，编辑器继续运行）", e.Exception);
            e.Handled = true;
        };

        // 2) 后台线程未捕获异常（无法阻止进程终止，但至少留下日志）
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            AppLog.Error(
                $"非 UI 线程致命异常 (terminating={e.IsTerminating})",
                e.ExceptionObject as Exception);
        };

        // 3) 未被观察的 Task 异常（async void / 忘记 await 的常见后果）
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("未观察的 Task 异常（已标记观察，避免进程终止）", e.Exception);
            e.SetObserved();
        };
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // Avalonia 12.1 + FluentAvaloniaUI 3.0.2 在 Linux 下存在默认字体($Default)创建失败
        // 的兼容问题（Could not create glyphTypeface）。仅 Linux 需要显式指定系统字体族
        // 绕过；Windows/macOS 无此问题，保持平台默认字体。
        if (OperatingSystem.IsLinux())
        {
            var fontOptions = new FontManagerOptions
            {
                DefaultFamilyName = "Noto Sans CJK SC"
            };
            builder = builder.With(fontOptions);
        }

        return builder;
    }
}
