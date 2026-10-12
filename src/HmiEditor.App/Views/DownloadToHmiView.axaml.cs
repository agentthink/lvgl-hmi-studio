using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Core.Services;
using HmiEditor.Device.Abstractions;
using HmiEditor.Device.Channels;
using HmiEditor.Device.Updates;

namespace HmiEditor.App.Views;

/// <summary>
/// 下载到 HMI 设备对话框 —— 将编译转换后的产物部署到指定 HMI 设备：
///   1) 源码工程 → /mnt/UDISK/hmi/src/（串口 / 以太网通道，带进度反馈）；
///   2) 工程资源 → /mnt/UDISK/hmi/assets/；
///   3) 预编译 libhmi_ui.so → /mnt/UDISK/hmi/libhmi_ui.so 并重启 hmi_runtime
///      （procd 自动拉起，新进程 dlopen 加载新 .so，HMI 界面立即更新）。
/// 未提供 .so 时仅上传源码/资源 —— 界面不会更新，对话框会明确提示。
/// </summary>
public partial class DownloadToHmiView : ChromeWindow
{
    private readonly ObservableCollection<string> _logs = new();
    private readonly IReadOnlyList<string> _outputFiles;
    private readonly string _outputDir;
    private readonly IReadOnlyList<HmiDeployAsset> _assets;

    /// <summary>运行时连接配置文本（comm_config.ini 内容；null=工程无连接需清除设备旧配置，空串由调用方决定）。</summary>
    private readonly string? _runtimeConfigText;
    // ── 注入缝（阶段 207，与 DeviceManagerView 同一套路）：部署服务 ＋ 通道工厂可替换 ──
    private readonly IHmiDeployService _deployService;
    private readonly IDeviceChannelFactory _channelFactory;

    private IDeviceChannel? _channel;
    private string? _soPath;
    private bool _busy;
    private bool _isInitializing;

    /// <summary>仅用于 XAML 设计器，不要直接使用。</summary>
    public DownloadToHmiView()
        : this(Array.Empty<string>(), "", null, null)
    {
    }

    /// <summary>常规构造：部署服务与通道工厂都用真实实现（与加缝之前的行为逐字相同）。</summary>
    public DownloadToHmiView(
        IReadOnlyList<string> outputFiles,
        string outputDir,
        IReadOnlyList<HmiDeployAsset>? assets = null,
        string? runtimeConfigText = null)
        : this(outputFiles, outputDir, assets, runtimeConfigText, null, null)
    {
    }

    /// <summary>
    /// **注入缝**（阶段 207）：部署服务 ＋ 通道工厂可替换，传 null 用真实实现。
    /// ⚠ `internal` 与 `DeviceManagerView` 同因（判据可调用即可，不必多出一条公共构造 ——
    /// 那会让 `WindowProductionPathUiTests` 的「双路径」元检查要求改判 `TwoPaths`）。
    /// </summary>
    internal DownloadToHmiView(
        IReadOnlyList<string> outputFiles,
        string outputDir,
        IReadOnlyList<HmiDeployAsset>? assets,
        string? runtimeConfigText,
        IHmiDeployService? deployService,
        IDeviceChannelFactory? channelFactory)
    {
        _deployService = deployService ?? new HmiDeployService();
        _channelFactory = channelFactory ?? new DeviceChannelFactory();
        _outputFiles = Array.Empty<string>();
        _outputDir = "";
        _assets = Array.Empty<HmiDeployAsset>();
        _runtimeConfigText = null;
        _isInitializing = true;
        InitializeComponent();
        _isInitializing = false;
        _outputFiles = outputFiles ?? Array.Empty<string>();
        _outputDir = outputDir ?? "";
        _assets = assets ?? Array.Empty<HmiDeployAsset>();
        _runtimeConfigText = runtimeConfigText;
        _deployService.Log += Log;
        LogBox.ItemsSource = _logs;
        RefreshPorts();

        TxtOutputPath.Text = string.IsNullOrEmpty(_outputDir) ? "—" : _outputDir;
        TxtOutputFiles.Text = _outputFiles.Count > 0
            ? string.Join(", ", _outputFiles.Select(Path.GetFileName))
            : "—";
        DetectSharedLibrary();
        Log(AppLocalization.Service.Format(Lang.DevicePendingSummary, _outputFiles.Count) +
            (_assets.Count > 0 ? AppLocalization.Service.Format(Lang.DevicePendingAssets, _assets.Count) : "") +
            (string.IsNullOrEmpty(_soPath) ? AppLocalization.Service[Lang.DevicePendingNoSo] : ""));
        _isInitializing = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _deployService.Log -= Log;
        base.OnClosed(e);
    }

    // ─────────────────── 日志 ───────────────────

    private void Log(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (LogBox != null && LogBox.ItemCount > 0)
                LogBox.ScrollIntoView(_logs[^1]);
        });
    }

    /// <summary>
    /// 日志段落标签：<c>[标签] 正文</c> —— 标签与正文都必须跟着 UI 语言走。
    ///
    /// <para>**不叫 <c>Tag</c>**：那会隐藏继承来的 <c>Control.Tag</c>（CS0108），
    /// 而这两个类都是控件 —— 名字撞上框架属性后，读代码的人会以为它在动那个属性。</para>
    /// </summary>
    private static string LogTag(string key) => $"[{AppLocalization.Service[key]}]";

    /// <summary>写一行带段落标签的日志：**标签与正文都走字典**。</summary>
    private void LogTagged(string tagKey, string messageKey, params object[] args) =>
        Log($"{LogTag(tagKey)} {AppLocalization.Service.Format(messageKey, args)}");

    private void LogError(string tagKey, Exception ex) =>
        Log($"{LogTag(tagKey)} {AppLocalization.Service[Lang.DeviceErr]}: {ex.Message}");

    // ─────────────────── libhmi_ui.so 选择 ───────────────────

    /// <summary>自动检测预编译 libhmi_ui.so：编译输出目录 → 强制 SharedLibrary 临时目录。</summary>
    private void DetectSharedLibrary()
    {
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(_outputDir))
            candidates.Add(Path.Combine(_outputDir, "libhmi_ui.so"));
        candidates.Add(AppPaths.TempPath("download", "libhmi_ui.so"));

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                _soPath = candidate;
                TxtSoPath.Text = candidate;
                TxtSoHint.Text = AppLocalization.Service.Format("download.so.auto.found",
                    Path.GetFileName(candidate));
                Log($"{LogTag(Lang.DeviceLogTagDeploy)} {AppLocalization.Service.Format(Lang.DownloadSoAutoFound, candidate)}");
                return;
            }
        }

        TxtSoPath.Text = "";
        TxtSoHint.Text = AppLocalization.Service["download.so.missing.hint"];
        LogTagged(Lang.DeviceLogTagDeploy, Lang.DeviceLogDeployNoSo);
    }

    /// <summary>
    /// 选择 HMI 程序（.so）。选择器在平台侧可能失败（Portal 不可用、权限被拒），
    /// 统一由 UiOperationGuard 兜底：落盘日志 + 本视图日志面板提示。
    /// </summary>
    private void OnBrowseSoClicked(object? sender, RoutedEventArgs e) =>
        UiOperationGuard.Run(Lang.UiOpPickHmi, async () =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = AppLocalization.Service[Lang.DownloadSoBrowseTitle],
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("libhmi_ui.so") { Patterns = new[] { "*.so" } },
                    new FilePickerFileType(AppLocalization.Service[Lang.FileTypeAll]) { Patterns = new[] { "*" } }
                }
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            _soPath = path;
            TxtSoPath.Text = path;
            var isNamed = Path.GetFileName(path).Equals("libhmi_ui.so", StringComparison.OrdinalIgnoreCase);
            TxtSoHint.Text = isNamed
                ? AppLocalization.Service.Format("download.so.auto.found", Path.GetFileName(path))
                : AppLocalization.Service.Format("download.so.renamed.hint", Path.GetFileName(path));
            LogTagged(Lang.DeviceLogTagDeploy, Lang.DeviceLogDeploySelectSo, path);
        }, Log);

    // ─────────────────── 连接 ───────────────────

    private void RefreshPorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames().OrderBy(p => p, StringComparer.Ordinal).ToList();
            CmbPort.ItemsSource = ports;
            if (ports.Count > 0 && CmbPort.SelectedItem is null)
                CmbPort.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            LogError(Lang.DeviceLogTagConnect, ex);
        }
    }

    private void OnChannelTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        var isSerial = CmbChannelType.SelectedIndex == 0;
        if (SerialPanel != null) SerialPanel.IsVisible = isSerial;
        if (TcpPanel != null) TcpPanel.IsVisible = !isSerial;
    }

    private void OnRefreshPortsClicked(object? sender, RoutedEventArgs e)
    {
        RefreshPorts();
        LogTagged(Lang.DeviceLogTagConnect, Lang.DeviceLogPortsRefreshed, CmbPort.ItemCount);
    }

    private async void OnOpenCloseClicked(object? sender, RoutedEventArgs e)
    {
        if (_channel is { IsOpen: true })
        {
            await _channel.CloseAsync();
            _channel = null;
            BtnOpenClose.Content = AppLocalization.Service[Lang.DeviceOpen];
            LogTagged(Lang.DeviceLogTagConnect, Lang.DeviceLogClosed);
            return;
        }

        try
        {
            if (CmbChannelType.SelectedIndex == 0)
            {
                var port = CmbPort.SelectedItem?.ToString() ?? "";
                if (string.IsNullOrEmpty(port))
                {
                    LogTagged(Lang.DeviceLogTagConnect, Lang.DeviceLogSelectPort);
                    return;
                }
                var baud = int.TryParse(TxtBaudRate.Text, out var b) && b > 0
                    ? b : SerialDeviceChannel.DefaultBaudRate;
                _channel = _channelFactory.CreateSerial(port, baud);
            }
            else
            {
                var host = TxtHost.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(host))
                {
                    LogTagged(Lang.DeviceLogTagConnect, Lang.DeviceLogEnterHost);
                    return;
                }
                var portNum = int.TryParse(TxtTcpPort.Text, out var p) && p > 0 ? p : 23;
                _channel = _channelFactory.CreateTcp(host, portNum);
            }

            await _channel.OpenAsync();
            BtnOpenClose.Content = AppLocalization.Service[Lang.DeviceClose];
            LogTagged(Lang.DeviceLogTagConnect, Lang.DeviceLogConnected, _channel.Name);
        }
        catch (Exception ex)
        {
            _channel = null;
            LogError(Lang.DeviceLogTagConnect, ex);
        }
    }

    private IDeviceChannel? RequireChannel()
    {
        if (_channel is null || !_channel.IsOpen)
        {
            Log($"{LogTag(Lang.DeviceLogTagHint)} {AppLocalization.Service[Lang.DeviceConnectFirst]}");
            return null;
        }
        return _channel;
    }

    // ─────────────────── 下载 / 部署 ───────────────────

    private async void OnDownloadClicked(object? sender, RoutedEventArgs e)
    {
        var channel = RequireChannel();
        if (channel is null) return;
        if (_outputFiles.Count == 0 && string.IsNullOrEmpty(_soPath))
        {
            Log($"{LogTag(Lang.DeviceLogTagDownload)} {AppLocalization.Service[Lang.DeviceNoOutput]}");
            return;
        }
        if (_busy) return;
        _busy = true;
        SetBusyUi(true);

        try
        {
            // 🩸 阶段 207 修 #95：`Text?.Trim() ?? 默认` 只在 **null** 时回退 —— 用户把这一格
            //   **清空**（`Text` 变成 `""`，非 null）时会把**空串**当目标目录传给部署服务：
            //   `mkdir -p ` 没有参数、文件被传到设备的当前目录（静默走错地方）。
            //   口径改成「空/空白都回退默认」（与其它输入框的"空白视为未填"一致）。
            var remoteDir = string.IsNullOrWhiteSpace(TxtRemoteDir.Text)
                ? HmiDeployService.DefaultSourceDir
                : TxtRemoteDir.Text.Trim();
            var progress = new Progress<double>(p =>
                Dispatcher.UIThread.Post(() => ProgDownload.Value = p * 100));

            var result = await _deployService.DeployAsync(
                channel,
                _outputFiles,
                _soPath,
                _assets,
                remoteDir,
                _runtimeConfigText,
                progress,
                CancellationToken.None);

            Log($"{LogTag(Lang.DeviceLogTagDownload)} " +
                AppLocalization.Service.Format(Lang.DeviceLogDownloadSummary,
                    result.SourceFilesUploaded, result.AssetsUploaded,
                    AppLocalization.Service[result.SoDeployed ? Lang.DeviceSoDeployed : Lang.DeviceSoNotDeployed]));

            if (result.SoDeployed && result.RuntimeRestarted)
            {
                TxtStatus.Text = AppLocalization.Service[Lang.DeviceDeployDone];
                Log($"{LogTag(Lang.DeviceLogTagDownload)} {AppLocalization.Service[Lang.DeviceDeployDone]}");
            }
            else if (result.SoDeployed)
            {
                TxtStatus.Text = AppLocalization.Service[Lang.DeviceDeployRestartFailed];
                Log($"{LogTag(Lang.DeviceLogTagDownload)} {AppLocalization.Service[Lang.DeviceDeployRestartFailed]}");
            }
            else if (!string.IsNullOrEmpty(result.SoDeployError))
            {
                TxtStatus.Text = AppLocalization.Service[Lang.DeviceSoInvalid];
                Log($"{LogTag(Lang.DeviceLogTagDownload)} {AppLocalization.Service[Lang.DeviceSoInvalid]}: {result.SoDeployError}");
            }
            else
            {
                TxtStatus.Text = AppLocalization.Service[Lang.DeviceSourceOnlyDone];
                Log($"{LogTag(Lang.DeviceLogTagDownload)} {AppLocalization.Service[Lang.DeviceSourceOnlyDone]}");
                Log($"{LogTag(Lang.DeviceLogTagDownload)} {AppLocalization.Service[Lang.DeviceSoBuildHint]}");
            }
        }
        catch (Exception ex)
        {
            TxtStatus.Text = AppLocalization.Service[Lang.DeviceDownloadFailed];
            LogError(Lang.DeviceLogTagDownload, ex);
        }
        finally
        {
            _busy = false;
            SetBusyUi(false);
            ProgDownload.Value = 0;
        }
    }

    private void SetBusyUi(bool busy)
    {
        BtnOpenClose.IsEnabled = !busy;
        BtnDownload.IsEnabled = !busy;
        BtnBrowseSo.IsEnabled = !busy;
        CmbChannelType.IsEnabled = !busy;
        CmbPort.IsEnabled = !busy;
        BtnRefreshPorts.IsEnabled = !busy;
        TxtBaudRate.IsEnabled = !busy;
        TxtHost.IsEnabled = !busy;
        TxtTcpPort.IsEnabled = !busy;
        TxtRemoteDir.IsEnabled = !busy;
    }
}
