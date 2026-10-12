using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Models;

namespace HmiEditor.App.Views;

/// <summary>
/// "编译并运行"对话框 —— 一键完成：生成代码 → CMake 编译 → 启动模拟器 → 运行监控。
/// 编译阶段显示进度与实时日志，失败时高亮错误摘要；模拟器进程输出与退出状态实时展示。
/// </summary>
public partial class BuildRunDialog : ChromeWindow
{
    private readonly HmiProject _project;
    private readonly string _workDir;
    private readonly string _lvglDir;
    private readonly AssetLibrary? _assetLibrary;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _logLock = new();
    private Process? _simProcess;

    /// <summary>仅用于 XAML 设计器，不要直接使用。</summary>
    public BuildRunDialog()
    {
        _project = null!;
        _workDir = null!;
        _lvglDir = null!;
        InitializeComponent();
    }

    public BuildRunDialog(HmiProject project, string workDir, string lvglDir, AssetLibrary? assetLibrary = null) : this()
    {
        _project = project;
        _workDir = workDir;
        _lvglDir = lvglDir;
        _assetLibrary = assetLibrary;
        Loaded += async (_, _) => await RunBuildAndSimulatorAsync();
    }

    /// <summary>编译并运行主流程：编译成功自动衔接模拟器，失败展示错误。</summary>
    private async Task RunBuildAndSimulatorAsync()
    {
        SetStopEnabled(true);

        try
        {
            // 编译前先杀所有 hmi_sim —— 无论本次编译成败，用户都不应再看到上次残留的
            // 旧进程窗口（例如上次 demo、另一个失败工程的遗留窗口）而误以为新代码已执行。
            try
            {
                foreach (var p in Process.GetProcessesByName("hmi_sim"))
                {
                    try { p.Kill(entireProcessTree: true); }
                    catch { /* 忽略杀进程失败 */ }
                }
                await Task.Delay(250, _cts.Token);
            }
            catch { /* 平台不支持时忽略 */ }

            // 1. 编译（生成 → cmake configure → build）
            var result = await SimulatorBuildService.BuildAsync(
                _project, _workDir, _lvglDir, _assetLibrary, AppendLog, _cts.Token,
                _project.VariableManager.Connections);

            if (!result.Success)
            {
                SetProgress(100);
                ShowErrors(result.Errors);
                Finish(AppLocalization.Service["buildrun.status.failed"]);

                // 明确提示：编译失败 → 不会启动模拟器，避免用户把"其它残留窗口 / LVGL demo 启动页"
                // 误读为"本次编译输出但内容不对"。
                AppendLog("");
                AppendLog("■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■");
                AppendLog(AppLocalization.Service[Lang.BuildrunLogNoAutostart]);
                AppendLog(AppLocalization.Service[Lang.BuildrunLogStaleWindow]);
                AppendLog("■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■");
                return;
            }

            // 2. 编译成功 → 自动运行模拟器
            await RunSimulatorAsync(result.ExecutablePath!);
        }
        catch (OperationCanceledException)
        {
            AppendBoxedLog(AppLocalization.Service[Lang.BuildrunLogCancelled]);
            Finish(AppLocalization.Service["buildrun.status.cancelled"]);
        }
        catch (Exception ex)
        {
            // 原因那一半走**码化出口**（登记 #61c 更正后重归 A 类异常档；阶段 103）：
            // 改动前这里是 Format(Lang.BuildrunLogError, ex.Message)，产出方
            // （SimulatorBuildService 的两条前置校验 + 本文件 RunSimulatorAsync 的进程启动）
            // 抛的是中文 InvalidOperationException，而它没有码 ⇒ 英文界面下这一半照旧中文。
            // ⚠ **只取一次原因、两处共用**：下一行的 ShowErrors 是同源**第二露出点** ——
            //   两处各取一次 ex.Message 的话，码化就只覆盖了一半。
            var reason = ExternalText.FailureReason(ex);
            AppendLog(AppLocalization.Service.Format(Lang.BuildrunLogError, reason));
            ShowErrors(new[] { reason });
            Finish(AppLocalization.Service["buildrun.status.failed"]);
        }
    }

    /// <summary>启动模拟器进程并监控运行状态与输出。</summary>
    private async Task RunSimulatorAsync(string exe)
    {
        SetPhase(AppLocalization.Service["buildrun.phase.run"]);
        SetProgress(85);

        // 启动前强制杀掉所有已存在的 hmi_sim 进程, 避免用户看到
        // 旧窗口 (残留的 demo/另一个工程) 而误以为新构建没生效。
        try
        {
            foreach (var p in Process.GetProcessesByName("hmi_sim"))
            {
                try { p.Kill(entireProcessTree: true); }
                catch { /* 忽略 */ }
            }
            await Task.Delay(300, _cts.Token);
        }
        catch { /* 平台不支持时忽略 */ }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? _workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        _simProcess = Process.Start(psi);
        if (_simProcess == null)
            throw new SimulatorBuildException(
                SimulatorBuildIssueCodes.PROCESS_START_FAILED,
                "无法启动模拟器进程（Process.Start 返回 null）");

        AppendLog($"{AppLocalization.Service[Lang.BuildrunLogTagSim]} {AppLocalization.Service.Format(Lang.BuildrunLogStarted, _simProcess.Id)}");
        SetPhase(AppLocalization.Service.Format("buildrun.status.running", _simProcess.Id));

        var outTask = ReadStreamAsync(_simProcess.StandardOutput);
        var errTask = ReadStreamAsync(_simProcess.StandardError);
        await _simProcess.WaitForExitAsync(_cts.Token);
        await Task.WhenAll(outTask, errTask);

        var code = _simProcess.ExitCode;
        AppendLog($"{AppLocalization.Service[Lang.BuildrunLogTagSim]} {AppLocalization.Service.Format(Lang.BuildrunLogExited, code)}");
        SetProgress(100);
        SetExitCode(AppLocalization.Service.Format("buildrun.status.exited", code));

        // 窗口正常关闭退出码 0；自检失败等非 0 视为异常
        Finish(code == 0
            ? AppLocalization.Service["buildrun.status.completed"]
            : AppLocalization.Service["buildrun.status.exited.abnormal"]);
        _simProcess.Dispose();
        _simProcess = null;
    }

    private async Task ReadStreamAsync(StreamReader reader)
    {
        try
        {
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;
                AppendLog(line);
            }
        }
        catch (OperationCanceledException) { /* 停止时静默结束 */ }
    }

    // ── UI 更新（线程安全：统一经 Dispatcher）──

    private void AppendLog(string line)
    {
        lock (_logLock)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var current = OutputLog.Text ?? "";
                OutputLog.Text = current.Length > 0 ? current + "\n" + line : line;
                OutputLog.CaretIndex = OutputLog.Text.Length;
            });
        }
    }

    /// <summary>按**显示列宽**算字符串宽度：CJK 全角算 2 列，其余 1 列。</summary>
    internal static int DisplayWidth(string s) => s.Sum(c => c > 0x2E7F ? 2 : 1);

    /// <summary>构造"框起来"的日志内容行，右侧补齐到 <paramref name="inner"/> 列。</summary>
    internal static string BoxedContentLine(string text, int inner = 44) =>
        "│ " + text + new string(' ', Math.Max(0, inner - 2 - DisplayWidth(text))) + " │";

    /// <summary>
    /// 写一条"框起来"的日志。
    ///
    /// <para><b>为什么按列宽算而不是把空格写死</b>：原实现把 padding 直接写进字符串，
    /// 而中文是**双列**字符 —— 实测那条 <c>[已取消]…</c> 的内容宽 47 列，上下边框只有 44 列，
    /// 右边框比上边框**多出 3 列（本来就是歪的）**；而且文案一改就继续歪，没人会发现。
    /// 换成按列宽计算后，中文、英文（甚至中英混排）都能对齐。</para>
    /// </summary>
    private void AppendBoxedLog(string text, int inner = 44)
    {
        var bar = new string('─', inner);
        AppendLog("┌" + bar + "┐");
        AppendLog(BoxedContentLine(text, inner));
        AppendLog("└" + bar + "┘");
    }

    private void SetPhase(string text) =>
        Dispatcher.UIThread.Post(() => PhaseText.Text = text);

    private void SetProgress(double value) =>
        Dispatcher.UIThread.Post(() => BuildProgress.Value = value);

    private void SetExitCode(string text) =>
        Dispatcher.UIThread.Post(() => ExitCodeText.Text = text);

    private void SetStopEnabled(bool enabled) =>
        Dispatcher.UIThread.Post(() => BtnStop.IsEnabled = enabled);

    private void ShowErrors(System.Collections.Generic.IEnumerable<string> errors)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ErrorPanel.IsVisible = true;
            ErrorList.ItemsSource = errors;
        });
    }

    private void Finish(string statusText)
    {
        Dispatcher.UIThread.Post(() =>
        {
            PhaseText.Text = statusText;
            BtnStop.IsEnabled = false;
            BtnClose.IsEnabled = true;
        });
    }

    // ── 按钮事件 ──

    private void OnStopClicked(object? sender, RoutedEventArgs e)
    {
        if (_simProcess != null && !_simProcess.HasExited)
        {
            try { _simProcess.Kill(entireProcessTree: true); } catch { /* 已退出 */ }
        }
        else
        {
            _cts.Cancel();
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _cts.Cancel();
        if (_simProcess != null && !_simProcess.HasExited)
        {
            try { _simProcess.Kill(entireProcessTree: true); } catch { }
        }
        base.OnClosing(e);
    }
}
