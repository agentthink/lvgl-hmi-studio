using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HmiEditor.App.Controls;
using HmiEditor.App.Resources;
using HmiEditor.App.Services;
using HmiEditor.Device.Abstractions;
using HmiEditor.Device.Channels;
using HmiEditor.Device.Detection;
using HmiEditor.Device.Firmware;
using HmiEditor.Device.Packaging;
using HmiEditor.Device.Updates;

namespace HmiEditor.App.Views;

/// <summary>
/// 设备管理对话框 —— 真实硬件联通：连接通道（串口/以太网）、硬件型号自动检测、
/// HMI 程序更新（OTA）、固件刷写（OTA + FEL 烧录）、USB 更新包生成、操作日志。
/// </summary>
public partial class DeviceManagerView : ChromeWindow
{
    private readonly ObservableCollection<string> _logs = new();

    // ── 注入缝（阶段 206，登记 #94）：四个设备服务 ＋ 通道工厂都可被宿主/测试替换 ──
    //    此前它们是就地 `new` 的 ⇒ 除"打包"外的成功路径在 headless 下原理上不可驱动。
    private readonly IDeviceDetector _detector;
    private readonly IDeviceFlasher _flasher;
    private readonly IHmiUpdateService _updateService;
    private readonly IUsbUpdatePackager _packager;
    private readonly IDeviceChannelFactory _channelFactory;

    private IDeviceChannel? _channel;
    private bool _busy;

    /// <summary>
    /// 常规构造：五个服务都用真实实现（与加缝之前的行为逐字相同）。
    /// ⚠ **必须是真的无参构造**（不能只给下面那个带默认值）—— Avalonia 的 XAML 加载器
    /// 要求类型有真正的无参构造，否则设计器/`avares` 装载会报 `AVLN3001`（门禁 warnings-as-errors）。
    /// </summary>
    public DeviceManagerView()
        : this(null, null, null, null, null)
    {
    }

    /// <summary>
    /// **注入缝**（阶段 206，登记 `#94`）：四个设备服务 ＋ 通道工厂可替换，传 null 用真实实现。
    /// 此前它们是就地 `new` 的 ⇒ 除"打包"外的成功路径在 headless 下原理上不可驱动。
    ///
    /// <para>⚠ **`internal` 是有意的**（只为判据/宿主可调用，`HmiEditor.App.csproj` 里
    /// <c>InternalsVisibleTo HmiEditor.App.UiTests</c> —— 与 <c>ToolboxView</c> 那条先例同形）：
    /// 做成 <c>public</c> 会让窗口多出一条公共构造，`WindowProductionPathUiTests` 的
    /// 「双路径」元检查立刻要求本窗口改判成 <c>TwoPaths=true</c>，而那条判据还要求
    /// **生产构造与设计器构造的字段形状不同** —— 本视图两条路都装同一批服务，形状必然一致。
    /// 无参构造仍是**唯一的生产入口**（守卫里的 `TwoPaths=false` 依旧成立）。</para>
    /// </summary>
    internal DeviceManagerView(
        IDeviceDetector? detector,
        IDeviceFlasher? flasher,
        IHmiUpdateService? updateService,
        IUsbUpdatePackager? packager,
        IDeviceChannelFactory? channelFactory)
    {
        _detector = detector ?? new DeviceDetector();
        _flasher = flasher ?? new DeviceFlasher();
        _updateService = updateService ?? new HmiUpdateService();
        _packager = packager ?? new UsbUpdatePackager();
        _channelFactory = channelFactory ?? new DeviceChannelFactory();

        InitializeComponent();
        _flasher.Log += Log;
        LogBox.ItemsSource = _logs;
        RefreshPorts();

        // InitializeComponent() 期间那次 OnChannelTypeChanged 是空跑（原因见其注释），
        // 初始状态在这里落地 —— 不依赖 XAML 里各面板的 IsVisible 默认值碰巧对得上。
        ApplyChannelPanels();

        Log(AppLocalization.Service[Lang.DeviceLogReady]);
    }

    protected override void OnClosed(EventArgs e)
    {
        _flasher.Log -= Log;
        base.OnClosed(e);
    }

    // ─────────────────── 日志 ───────────────────

    private void Log(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (LogBox.ItemCount > 0)
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

    /// <summary>
    /// 通道类型切换 —— 串口面板与以太网面板互斥显示。
    ///
    /// <para><b>为什么有 null 前置检查（2026-09-20 补）：</b>XAML 里 <c>CmbChannelType</c> 的
    /// <c>SelectedIndex="0"</c> 会在 <c>InitializeComponent()</c> **期间**就触发本处理器
    /// （经 <c>SelectingItemsControl.EndInit</c>），而 <c>SerialPanel</c> / <c>BaudPanel</c> /
    /// <c>TcpPanel</c> 声明在它**后面** —— 字段按 XAML 声明顺序赋值，那一刻三个都还是 null。
    /// 构造函数末尾会主动调一次 <see cref="ApplyChannelPanels"/> 落初始状态，所以这里提前返回是安全的。</para>
    ///
    /// <para>没有这个检查时症状是 <c>NullReferenceException</c> 从 <c>new DeviceManagerView()</c>
    /// 抛出（<c>!XamlIlPopulate</c> 内），调用方 <c>Wiring.Panels.cs</c> 的
    /// <c>dialog.ShowDialog(this)</c> 根本执行不到 ⇒ **"设备管理"菜单点了没反应**。
    /// 判据：<c>WindowViewSmokeUiTests.Window_LoadsAndLaysOut</c>。</para>
    /// </summary>
    private void OnChannelTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SerialPanel is null || BaudPanel is null || TcpPanel is null) return;
        ApplyChannelPanels();
    }

    /// <summary>按当前通道类型落地面板可见性 —— 构造期与事件处理器共用同一实现。</summary>
    private void ApplyChannelPanels()
    {
        var isSerial = CmbChannelType.SelectedIndex == 0;
        SerialPanel.IsVisible = isSerial;
        BaudPanel.IsVisible = isSerial;
        TcpPanel.IsVisible = !isSerial;
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

    // ─────────────────── 通用操作保护 ───────────────────

    private bool BeginOperation()
    {
        if (_busy)
        {
            Log($"{LogTag(Lang.DeviceLogTagHint)} {AppLocalization.Service[Lang.DeviceOperBusy]}");
            return false;
        }
        _busy = true;
        SetBusyUi(true);
        return true;
    }

    private void EndOperation()
    {
        _busy = false;
        SetBusyUi(false);
    }

    private void SetBusyUi(bool busy)
    {
        BtnOpenClose.IsEnabled = !busy;
        BtnDetect.IsEnabled = !busy;
        BtnCheckUpdate.IsEnabled = !busy;
        BtnUpdateHmi.IsEnabled = !busy;
        BtnFlashFw.IsEnabled = !busy;
        BtnGenerate.IsEnabled = !busy;
        BtnFelFlash.IsEnabled = !busy;
    }

    // ─────────────────── 设备检测 ───────────────────

    private async void OnDetectClicked(object? sender, RoutedEventArgs e)
    {
        var channel = RequireChannel();
        if (channel is null || !BeginOperation()) return;

        try
        {
            Log($"{LogTag(Lang.DeviceLogTagDetect)} {AppLocalization.Service[Lang.DeviceDetecting]}");
            var info = await _detector.DetectAsync(channel);
            TxtModel.Text = string.IsNullOrWhiteSpace(info.Model) ? "—" : info.Model;
            TxtArch.Text = string.IsNullOrWhiteSpace(info.Architecture) ? "—" : info.Architecture;
            TxtKernel.Text = string.IsNullOrWhiteSpace(info.KernelVersion) ? "—" : info.KernelVersion;
            TxtFw.Text = string.IsNullOrWhiteSpace(info.FirmwareVersion) ? "—" : info.FirmwareVersion;
            TxtHmiVer.Text = string.IsNullOrWhiteSpace(info.HmiVersion) ? "—" : info.HmiVersion;
            if (!string.IsNullOrWhiteSpace(info.HmiVersion))
                TxtDeviceVersion.Text = info.HmiVersion;
            LogTagged(Lang.DeviceLogTagDetect, Lang.DeviceLogDetectDone, info);
        }
        catch (Exception ex)
        {
            LogError(Lang.DeviceLogTagDetect, ex);
        }
        finally
        {
            EndOperation();
        }
    }

    // ─────────────────── 版本检查与 HMI 更新 ───────────────────

    private async void OnCheckUpdateClicked(object? sender, RoutedEventArgs e)
    {
        var channel = RequireChannel();
        if (channel is null || !BeginOperation()) return;

        try
        {
            var local = TxtLocalVersion.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(local))
            {
                LogTagged(Lang.DeviceLogTagUpdate, Lang.DeviceLogUpdateEnterLocal);
                return;
            }
            TxtUpdateStatus.Text = AppLocalization.Service[Lang.DeviceChecking];
            LogTagged(Lang.DeviceLogTagUpdate, Lang.DeviceLogUpdateChecking, local);
            var result = await _updateService.CheckAsync(channel, local);
            if (!string.IsNullOrEmpty(result.DeviceVersion))
                TxtDeviceVersion.Text = result.DeviceVersion;

            var status = result.Kind switch
            {
                UpdateCheckResultKind.UpToDate => AppLocalization.Service[Lang.DeviceUpToDate],
                UpdateCheckResultKind.UpdateAvailable => AppLocalization.Service[Lang.DeviceUpdateAvailable],
                UpdateCheckResultKind.DeviceNewer => AppLocalization.Service[Lang.DeviceDeviceNewer],
                _ => AppLocalization.Service[Lang.DeviceUnknownVer]
            };
            TxtUpdateStatus.Text = status;
            Log($"{LogTag(Lang.DeviceLogTagUpdate)} {status}");
        }
        catch (Exception ex)
        {
            LogError(Lang.DeviceLogTagUpdate, ex);
        }
        finally
        {
            EndOperation();
        }
    }

    private async void OnUpdateHmiClicked(object? sender, RoutedEventArgs e)
    {
        var channel = RequireChannel();
        if (channel is null || !BeginOperation()) return;

        try
        {
            var swu = TxtHmiSwu.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(swu) || !File.Exists(swu))
            {
                LogTagged(Lang.DeviceLogTagUpdate, Lang.DeviceLogUpdateSelectSwu);
                return;
            }
            var progress = new Progress<double>(v => ProgHmi.Value = v * 100);
            Log($"{LogTag(Lang.DeviceLogTagUpdate)} {AppLocalization.Service[Lang.DeviceUpdating]} {Path.GetFileName(swu)}");
            var ok = await _updateService.UpdateAsync(channel, swu, progress);
            Log($"{LogTag(Lang.DeviceLogTagUpdate)} " +
                AppLocalization.Service[ok ? Lang.DeviceLogUpdateTriggered : Lang.DeviceLogUpdateFailed]);
        }
        catch (Exception ex)
        {
            LogError(Lang.DeviceLogTagUpdate, ex);
        }
        finally
        {
            EndOperation();
            ProgHmi.Value = 0;
        }
    }

    // ─────────────────── 固件刷写 ───────────────────

    private async void OnFlashFwClicked(object? sender, RoutedEventArgs e)
    {
        var channel = RequireChannel();
        if (channel is null || !BeginOperation()) return;

        try
        {
            var swu = TxtFwSwu.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(swu) || !File.Exists(swu))
            {
                LogTagged(Lang.DeviceLogTagFirmware, Lang.DeviceLogFwSelectSwu);
                return;
            }
            var progress = new Progress<double>(v => ProgFw.Value = v * 100);
            Log($"{LogTag(Lang.DeviceLogTagFirmware)} {AppLocalization.Service[Lang.DeviceFwUpdating]} {Path.GetFileName(swu)}");
            await _flasher.FlashAsync(channel, new OtaFlashOptions
            {
                Target = OtaTarget.Firmware,
                SwuPath = swu,
                RemotePath = "/tmp/fw_update.swu",
                OtaTimeout = TimeSpan.FromMinutes(5)
            }, progress);
            LogTagged(Lang.DeviceLogTagFirmware, Lang.DeviceLogFwTriggered);
        }
        catch (Exception ex)
        {
            LogError(Lang.DeviceLogTagFirmware, ex);
        }
        finally
        {
            EndOperation();
            ProgFw.Value = 0;
        }
    }

    // ─────────────────── USB 更新包生成 ───────────────────

    private async void OnGenerateClicked(object? sender, RoutedEventArgs e)
    {
        if (!BeginOperation()) return;

        try
        {
            var lib = TxtHmiLib.Text?.Trim() ?? "";
            var outDir = TxtOutputDir.Text?.Trim() ?? "";
            var version = TxtPkgVersion.Text?.Trim() ?? "1.0.0";
            if (string.IsNullOrEmpty(lib) || !File.Exists(lib))
            {
                LogTagged(Lang.DeviceLogTagUsb, Lang.DeviceLogUsbSelectSo);
                return;
            }
            if (string.IsNullOrEmpty(outDir))
            {
                LogTagged(Lang.DeviceLogTagUsb, Lang.DeviceLogUsbSelectOutDir);
                return;
            }

            Log($"{LogTag(Lang.DeviceLogTagUsb)} {AppLocalization.Service[Lang.DeviceGenerating]} v{version} → {outDir}");
            var result = await _packager.BuildAsync(new UsbUpdateOptions
            {
                OutputDirectory = outDir,
                HmiLibraryPath = lib,
                HmiVersion = version
            });

            var message = $"{AppLocalization.Service[Lang.DeviceGenerated]}: {result.HmiSwuPath}";
            TxtGenerateResult.Text = message;
            Log($"{LogTag(Lang.DeviceLogTagUsb)} {message}");
        }
        catch (Exception ex)
        {
            TxtGenerateResult.Text = ex.Message;
            LogError(Lang.DeviceLogTagUsb, ex);
        }
        finally
        {
            EndOperation();
        }
    }

    // ─────────────────── FEL 镜像烧录 ───────────────────

    private async void OnFelFlashClicked(object? sender, RoutedEventArgs e)
    {
        if (!BeginOperation()) return;

        try
        {
            var tool = TxtFelTool.Text?.Trim() ?? "sunxi-fel";
            var image = TxtFelImage.Text?.Trim() ?? "";
            var args = TxtFelArgs.Text?.Trim() ?? "-p spiflash-write 0 {image}";
            if (string.IsNullOrEmpty(image) || !File.Exists(image))
            {
                LogTagged(Lang.DeviceLogTagFel, Lang.DeviceLogFelSelectImage);
                return;
            }

            Log($"{LogTag(Lang.DeviceLogTagFel)} {AppLocalization.Service[Lang.DeviceFelRunning]}");
            var result = await _flasher.FelFlashAsync(new FelFlashOptions
            {
                ToolPath = tool,
                ImagePath = image,
                ArgumentsTemplate = args
            });

            if (result.Success)
                Log($"{LogTag(Lang.DeviceLogTagFel)} {AppLocalization.Service[Lang.DeviceFelDone]}");
            else
                LogTagged(Lang.DeviceLogTagFel, Lang.DeviceLogFelFlashFailed, result.ExitCode);
        }
        catch (Exception ex)
        {
            LogError("FEL", ex);
        }
        finally
        {
            EndOperation();
        }
    }

    // ─────────────────── 文件/目录选择 ───────────────────

    private async Task<string?> PickFileAsync(string title, string[] patterns)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(title) { Patterns = patterns },
                new FilePickerFileType(AppLocalization.Service[Lang.FileTypeAll]) { Patterns = new[] { "*" } }
            }
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    /// <summary>
    /// 文件选择的统一入口：进入选择器后把路径写回目标输入框。
    /// 失败只写本视图日志面板（AppLog 已落盘），不再让异常走 async void 的兜底路径。
    /// </summary>
    private void PickFileInto(string titleKey, string[] patterns, Action<string> assign) =>
        UiOperationGuard.Run(Lang.UiOpPickFile, async () =>
        {
            var path = await PickFileAsync(AppLocalization.Service[titleKey], patterns);
            if (path is not null) assign(path);
        }, Log);

    private void PickFolderInto(string titleKey, Action<string> assign) =>
        UiOperationGuard.Run(Lang.UiOpPickFolder, async () =>
        {
            var path = await PickFolderAsync(AppLocalization.Service[titleKey]);
            if (path is not null) assign(path);
        }, Log);

    private void OnBrowseHmiSwuClicked(object? sender, RoutedEventArgs e) =>
        PickFileInto(Lang.DeviceSwuPath, new[] { "*.swu" }, p => TxtHmiSwu.Text = p);

    private void OnBrowseFwSwuClicked(object? sender, RoutedEventArgs e) =>
        PickFileInto(Lang.DeviceFwSwuPath, new[] { "*.swu" }, p => TxtFwSwu.Text = p);

    private void OnBrowseHmiLibClicked(object? sender, RoutedEventArgs e) =>
        PickFileInto(Lang.DeviceHmiLibPath, new[] { "*.so" }, p => TxtHmiLib.Text = p);

    private void OnBrowseOutputClicked(object? sender, RoutedEventArgs e) =>
        PickFolderInto(Lang.DeviceSelectDir, p => TxtOutputDir.Text = p);

    private void OnBrowseFelToolClicked(object? sender, RoutedEventArgs e) =>
        PickFileInto(Lang.DeviceFelTool, new[] { "sunxi-fel", "sunxi-fel.exe" }, p => TxtFelTool.Text = p);

    private void OnBrowseFelImageClicked(object? sender, RoutedEventArgs e) =>
        PickFileInto(Lang.DeviceFelImage, new[] { "*.img", "*.fex", "*.bin" }, p => TxtFelImage.Text = p);
}
