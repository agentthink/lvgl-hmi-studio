using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Resources;
using HmiEditor.App.Views;
using HmiEditor.Core.Services;
using HmiEditor.Device.Abstractions;
using HmiEditor.Device.Channels;
using HmiEditor.Device.Detection;
using HmiEditor.Device.Firmware;
using HmiEditor.Device.Packaging;
using HmiEditor.Device.Updates;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`DeviceManagerView` 那 13 个具名输入框的**读点行为面****（阶段 205）—— 「输入框『谁读』面」
/// （阶段 196）里最后一块**没有任何行为判据**的读点。
///
/// <para><b>这 13 个框的读点全在"动作处理器"里</b>（不是保存路径）：`OnOpenCloseClicked`
/// （Host/TcpPort/BaudRate）· `OnCheckUpdateClicked`（LocalVersion）· `OnUpdateHmiClicked`（HmiSwu）·
/// `OnFlashFwClicked`（FwSwu）· `OnGenerateClicked`（HmiLib/OutputDir/PkgVersion）·
/// `OnFelFlashClicked`（FelTool/FelImage/FelArgs）· `OnDetectClicked`（写 DeviceVersion，只读展示）。</para>
///
/// <para><b>能驱动的与不能驱动的，按"有没有可观测的失败分支"分</b>：</para>
/// <list type="number">
/// <item><b>driven（5 个）</b>：`TxtHmiLib` / `TxtOutputDir` / `TxtPkgVersion` —— 走
/// <c>UsbUpdatePackager</c>（**纯本地**打包，不需要硬件也没外部工具）⇒ 驱动后断言
/// **真的产出了 `hmi-&lt;版本&gt;.swu`**，以及两条校验分支；`TxtHost` —— TCP 分支的"主机为空"校验；
/// `TxtFelImage` —— 镜像不存在的校验。后两条证明"读点被到达"（没有读就没有那句提示）。</item>
/// <item><b>registered（7 个）</b>：`TxtTcpPort` / `TxtBaudRate`（要真串口）· `TxtLocalVersion` /
/// `TxtHmiSwu` / `TxtFwSwu`（`RequireChannel()` 先挡：**没连设备时处理器在读到它们之前就返回了**）·
/// `TxtFelTool` / `TxtFelArgs`（要真的跑 `sunxi-fel`）—— 逐条写明为什么 headless 下不可驱动。</item>
/// </list>
///
/// <para>⚠ **阶段 205 时**这 13 个框只有 5 个驱动得动：那几个处理器直连真实服务
/// （`new DeviceFlasher()` / `new HmiUpdateService()`）且**没有注入缝**，一旦真的开通道/刷机就会去碰
/// 串口/网口/外部进程。**阶段 206 给视图加了注入缝**（`IDeviceDetector` / `IDeviceFlasher` /
/// `IHmiUpdateService` / `IUsbUpdatePackager` / `IDeviceChannelFactory`，见 `DeviceServiceAbstractions`）
/// ⇒ 8 个框全部可用**假实现**驱动，`Registered` 台账**清零**（棘轮）。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class DeviceManagerInputReadFaceTests
{
    /// <summary>
    /// 被**驱动**的框 —— **阶段 206 起是全部 13 个**：加了注入缝（四个服务 ＋ 通道工厂）之后，
    /// 原先"要真设备才可观测"的那 8 个框都能用假实现驱动。
    /// </summary>
    private static readonly string[] Driven =
    {
        // 打包（纯本地）
        "TxtHmiLib", "TxtOutputDir", "TxtPkgVersion",
        // 通道参数（假通道工厂记参数）
        "TxtHost", "TxtTcpPort", "TxtBaudRate",
        // OTA 三个入口（假更新服务/假刷写器记参数）
        "TxtLocalVersion", "TxtHmiSwu", "TxtFwSwu",
        // FEL 三格（假刷写器记参数）＋ 只读展示（假检测器/假更新服务回填）
        "TxtFelTool", "TxtFelImage", "TxtFelArgs", "TxtDeviceVersion",
    };

    /// <summary>
    /// 登记为"headless 下不可驱动"的框：键＝框名，值＝为什么。
    /// ⚠ **阶段 206 起这张表是空的** —— 注入缝补齐后 13 个框全部可驱动（判据里是棘轮）。
    /// 真要往里加一条，得先确认"连假实现也观测不到"，并把理由写足。
    /// </summary>
    private static readonly Dictionary<string, string> Registered = new(StringComparer.Ordinal);

    /// <summary>登记为"headless 下不可驱动"的框：键＝框名，值＝为什么。</summary>

    private static void Click(Control root, string name)
    {
        ControlWiringFace.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(2);
    }

    private static string[] Logs(Control view) =>
        ControlWiringFace.Find<ListBox>(view, "LogBox").Items.Cast<object?>()
            .Select(o => o?.ToString() ?? "")
            .ToArray();

    /// <summary>等产品状态到位（`TestWait.Until` 是同步轮询，会把派发线程占住 ⇒ `async void` 处理器回不来）。</summary>
    private static async Task UntilAsync(Func<bool> condition, string what, int timeoutMs = 10000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            UiTestHost.Pump(2);
            if (condition()) return;
            await Task.Delay(20);
        }

        Assert.Fail($"等不到：{what}（已等 {timeoutMs} ms）");
    }

    private static DeviceManagerView Open()
    {
        var view = new DeviceManagerView();
        view.Show();
        UiTestHost.Pump(5);
        return view;
    }

    [Fact]
    public void EveryNamedInputBox_IsDrivenOrRegistered()
    {
        var axaml = File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, ControlWiringFace.ViewsRelative, "DeviceManagerView.axaml"));
        var named = ControlWiringFace.NamedTextBoxes(axaml);

        Assert.True(named.Count >= 13,
            $"只扫到 {named.Count} 个具名 TextBox —— 扫描面塌了（阶段 205 实测 13 个）");

        var accounted = Driven.Concat(Registered.Keys).ToHashSet(StringComparer.Ordinal);
        var missing = named.Where(n => !accounted.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var ghosts = accounted.Where(n => !named.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0,
            "这些具名输入框既没被驱动、也没登记理由（新加的框必须二选一）：" + string.Join(", ", missing));
        Assert.True(ghosts.Length == 0,
            "台账里这些框在 XAML 里不存在了（改名/删除后要同步）：" + string.Join(", ", ghosts));

        // 登记的理由要写清"为什么不可驱动"，不能是空话
        var thin = Registered.Where(kv => kv.Value.Length < 30).Select(kv => kv.Key).ToArray();
        Assert.True(thin.Length == 0, "这些登记理由太短，等于没写：" + string.Join(", ", thin));

        // 🎯 棘轮（阶段 206）：注入缝补齐后这张台账**应当是空的**
        Assert.True(Registered.Count == 0,
            "有框被登记为「不可驱动」—— 阶段 206 已给 DeviceManagerView 加了注入缝（四个服务 ＋ 通道工厂），"
            + "13 个框都能用假实现驱动；真要登记，必须写清「连假实现也观测不到」的理由："
            + string.Join(", ", Registered.Keys));
        Assert.Equal(13, Driven.Length);
    }

    [Fact]
    public async Task PackagingInputs_ProduceThePackageAndValidateBothBranches()
    {
        using var sandbox = new DataRootSandbox("devicemanager-packaging");

        await UiTestHost.RunAsync(async () =>
        {
            var src = Path.Combine(sandbox.DataRoot, "src");
            var outDir = Path.Combine(sandbox.DataRoot, "usb");
            Directory.CreateDirectory(src);
            Directory.CreateDirectory(outDir);
            var lib = Path.Combine(src, "libhmi_ui.so");
            File.WriteAllBytes(lib, new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F', 1, 2, 3, 4 });

            var view = Open();
            try
            {
                // 前置自检：起点都是空的（XAML 没写 Text ⇒ 可能是 null）
                Assert.True(string.IsNullOrEmpty(ControlWiringFace.Find<TextBox>(view, "TxtHmiLib").Text));
                Assert.True(string.IsNullOrEmpty(ControlWiringFace.Find<TextBox>(view, "TxtOutputDir").Text));

                // ① 空 so ⇒ 校验分支（**证明读点被到达**）
                var before = Logs(view).Length;
                Click(view, "BtnGenerate");
                await UntilAsync(() => Logs(view).Length > before, "空 .so 的校验提示进日志");
                Assert.Contains(Logs(view), l => l.Contains(AppLocalization.Service[Lang.DeviceLogUsbSelectSo], StringComparison.Ordinal));

                // ② 有 so、空输出目录 ⇒ 另一条校验分支
                ControlWiringFace.Write(view, "TxtHmiLib", lib);
                before = Logs(view).Length;
                Click(view, "BtnGenerate");
                await UntilAsync(() => Logs(view).Length > before, "空输出目录的校验提示进日志");
                Assert.Contains(Logs(view), l => l.Contains(AppLocalization.Service[Lang.DeviceLogUsbSelectOutDir], StringComparison.Ordinal));

                // ③ 三格齐全 ⇒ 真的产出 hmi-<版本>.swu（**版本与输出目录都来自那两个框**）
                ControlWiringFace.Write(view, "TxtOutputDir", outDir);
                ControlWiringFace.Write(view, "TxtPkgVersion", "9.9.9");

                before = Logs(view).Length;
                Click(view, "BtnGenerate");
                await UntilAsync(() => Logs(view).Length > before, "打包动作有输出");
                await UntilAsync(
                    () => !string.IsNullOrWhiteSpace(ControlWiringFace.Find<TextBlock>(view, "TxtGenerateResult").Text),
                    "打包结果文本被写上");

                var result = ControlWiringFace.Find<TextBlock>(view, "TxtGenerateResult").Text ?? "";
                var produced = Path.Combine(outDir, "hmi-9.9.9.swu");
                Assert.True(File.Exists(produced),
                    $"没产出 `{produced}`；结果文本：{result}；日志尾：{string.Join(" | ", Logs(view).TakeLast(3))}");

                // 结果文本里带的是**产出路径**（语言无关：只判路径片段）
                Assert.Contains(produced, result, StringComparison.Ordinal);
                // 日志里那句 `v9.9.9 → <outDir>` 也是语言无关的
                Assert.Contains(Logs(view), l => l.Contains("9.9.9", StringComparison.Ordinal)
                                                 && l.Contains(outDir, StringComparison.Ordinal));
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    [Fact]
    public async Task HostBox_IsValidatedBeforeAnyConnectionIsAttempted()
    {
        using var sandbox = new DataRootSandbox("devicemanager-host");

        await UiTestHost.RunAsync(async () =>
        {
            var view = Open();
            try
            {
                // 切到以太网分支（index 1），并把主机写成空白
                var combo = ControlWiringFace.Find<ComboBox>(view, "CmbChannelType");
                combo.SelectedIndex = 1;
                UiTestHost.Pump(2);
                ControlWiringFace.Write(view, "TxtHost", "   ");

                var before = Logs(view).Length;
                Click(view, "BtnOpenClose");
                await UntilAsync(() => Logs(view).Length > before, "主机为空的校验提示进日志");

                // 读点被到达 ⇒ 那句"请输入主机地址"进日志（没有读就不会有这句）
                Assert.Contains(Logs(view), l => l.Contains(AppLocalization.Service[Lang.DeviceLogEnterHost], StringComparison.Ordinal));
                // 而且**没有真的去连**（`BtnOpenClose` 仍是"打开"，说明没进到 Open 成功那一支）
                Assert.Equal(AppLocalization.Service[Lang.DeviceOpen],
                    ControlWiringFace.Find<Button>(view, "BtnOpenClose").Content?.ToString());
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    [Fact]
    public async Task FelImage_IsValidatedBeforeTheExternalToolRuns()
    {
        using var sandbox = new DataRootSandbox("devicemanager-fel");

        await UiTestHost.RunAsync(async () =>
        {
            var view = Open();
            try
            {
                // 镜像路径不存在 ⇒ 在调起 `sunxi-fel` **之前**就该被拦下
                ControlWiringFace.Write(view, "TxtFelImage", Path.Combine(sandbox.DataRoot, "nope.img"));

                var before = Logs(view).Length;
                Click(view, "BtnFelFlash");
                await UntilAsync(() => Logs(view).Length > before, "镜像不存在的校验提示进日志");

                Assert.Contains(Logs(view), l => l.Contains(AppLocalization.Service[Lang.DeviceLogFelSelectImage], StringComparison.Ordinal));
                // 没跑到"正在烧录"那一步（那句是外部工具真的被调起后才写的）
                Assert.DoesNotContain(Logs(view), l => l.Contains(AppLocalization.Service[Lang.DeviceFelRunning], StringComparison.Ordinal));
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    // ───────────────────────── 假实现（阶段 206 的注入缝用）─────────────────────────

    private sealed class FakeChannel : IDeviceChannel
    {
        public string Name { get; init; } = "fake://dev";
        public bool IsOpen { get; private set; }
        public string Kind => "fake";
        public int OpenCount { get; private set; }

        public Task OpenAsync(CancellationToken ct = default)
        {
            IsOpen = true;
            OpenCount++;
            return Task.CompletedTask;
        }

        public Task CloseAsync()
        {
            IsOpen = false;
            return Task.CompletedTask;
        }

        public Task<string> SendCommandAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
            => Task.FromResult("");

        public Task TransferFileAsync(string localPath, string remotePath, IProgress<double>? progress = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public void Dispose() { }
    }

    private sealed class FakeChannelFactory : IDeviceChannelFactory
    {
        public (string Port, int Baud)? Serial { get; private set; }
        public (string Host, int Port)? Tcp { get; private set; }
        public FakeChannel Last { get; } = new();

        public IDeviceChannel CreateSerial(string portName, int baudRate)
        {
            Serial = (portName, baudRate);
            return Last;
        }

        public IDeviceChannel CreateTcp(string host, int port)
        {
            Tcp = (host, port);
            return Last;
        }
    }

    private sealed class FakeDetector : IDeviceDetector
    {
        public string HmiVersion { get; set; } = "3.2.1";
        public int Calls { get; private set; }

        public Task<DeviceInfo> DetectAsync(IDeviceChannel channel, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new DeviceInfo
            {
                Model = "T113-fake",
                Architecture = "arm",
                KernelVersion = "5.4-fake",
                FirmwareVersion = "1.2.3-fake",
                HmiVersion = HmiVersion,
            });
        }
    }

    private sealed class FakeUpdateService : IHmiUpdateService
    {
#pragma warning disable CS0067   // 事件在假实现里不需要真的触发
        public event Action<string>? Log;
#pragma warning restore CS0067

        public string? CheckedLocal { get; private set; }
        public string? UpdatedSwu { get; private set; }
        public string DeviceVersion { get; set; } = "7.7.7";

        public Task<UpdateCheckResult> CheckAsync(IDeviceChannel channel, string localVersion, CancellationToken ct = default)
        {
            CheckedLocal = localVersion;
            return Task.FromResult(new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.UpdateAvailable,
                LocalVersion = localVersion,
                DeviceVersion = DeviceVersion,
            });
        }

        public Task<bool> UpdateAsync(IDeviceChannel channel, string localSwuPath, IProgress<double>? progress = null, CancellationToken ct = default)
        {
            UpdatedSwu = localSwuPath;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeFlasher : IDeviceFlasher
    {
#pragma warning disable CS0067
        public event Action<string>? Log;
#pragma warning restore CS0067

        public string? OtaSwu { get; private set; }
        public string? FelTool { get; private set; }
        public string? FelImage { get; private set; }
        public string? FelArgs { get; private set; }

        public Task FlashAsync(IDeviceChannel channel, OtaFlashOptions options, IProgress<double>? progress = null, CancellationToken ct = default)
        {
            OtaSwu = options.SwuPath;
            return Task.CompletedTask;
        }

        public Task<FelFlashResult> FelFlashAsync(FelFlashOptions options, CancellationToken ct = default)
        {
            FelTool = options.ToolPath;
            FelImage = options.ImagePath;
            FelArgs = options.ArgumentsTemplate;
            return Task.FromResult(new FelFlashResult { Success = true, ExitCode = 0 });
        }
    }

    private static string Touch(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        return path;
    }

    [Fact]
    public async Task TcpInputs_ReachTheChannelFactory()
    {
        using var sandbox = new DataRootSandbox("devicemanager-tcp-inputs");

        await UiTestHost.RunAsync(async () =>
        {
            var factory = new FakeChannelFactory();
            var view = new DeviceManagerView(null, null, null, null, factory);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                ControlWiringFace.Find<ComboBox>(view, "CmbChannelType").SelectedIndex = 1;   // 以太网
                UiTestHost.Pump(2);

                // ① 正常值：工厂收到 (host, port) —— 这两格的值真的被读了
                ControlWiringFace.Write(view, "TxtHost", "10.0.0.5");
                ControlWiringFace.Write(view, "TxtTcpPort", "2323");
                Click(view, "BtnOpenClose");
                await UntilAsync(() => factory.Tcp is not null, "通道工厂收到 TCP 参数");
                Assert.Equal(("10.0.0.5", 2323), factory.Tcp);

                // ② 关闭通道（第二次点击），换一个非法端口值 ⇒ **静默回退 23**（把这条口径钉住）
                Click(view, "BtnOpenClose");
                ControlWiringFace.Write(view, "TxtTcpPort", "abc");
                Click(view, "BtnOpenClose");
                await UntilAsync(() => factory.Tcp is { Port: 23 }, "非法端口回退到 23");
                Assert.Equal(23, factory.Tcp!.Value.Port);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    [Fact]
    public async Task SerialInputs_ReachTheChannelFactory()
    {
        using var sandbox = new DataRootSandbox("devicemanager-serial-inputs");

        await UiTestHost.RunAsync(async () =>
        {
            var factory = new FakeChannelFactory();
            var view = new DeviceManagerView(null, null, null, null, factory);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                // 串口分支要求先选中一个端口 —— 容器里 `SerialPort.GetPortNames()` 多半是空的，
                // 所以这里把端口列表换成夹具自己的（视图只读 `SelectedItem`）。
                var ports = ControlWiringFace.Find<ComboBox>(view, "CmbPort");
                ports.ItemsSource = new[] { "COM_TEST" };
                ControlWiringFace.Find<ComboBox>(view, "CmbChannelType").SelectedIndex = 0;   // 串口
                ports.SelectedIndex = 0;
                UiTestHost.Pump(2);

                // ① 正常值：工厂收到 (port, baud) —— 波特率那格真的被读了
                ControlWiringFace.Write(view, "TxtBaudRate", "9600");
                Click(view, "BtnOpenClose");
                await UntilAsync(() => factory.Serial is not null, "通道工厂收到串口参数");
                Assert.Equal(("COM_TEST", 9600), factory.Serial);

                // ② 非法波特率 ⇒ 回退 `SerialDeviceChannel.DefaultBaudRate`（口径钉住）
                Click(view, "BtnOpenClose");
                ControlWiringFace.Write(view, "TxtBaudRate", "0");
                Click(view, "BtnOpenClose");
                await UntilAsync(() => factory.Serial is { Baud: SerialDeviceChannel.DefaultBaudRate }, "非法波特率回退到默认值");
                Assert.Equal(SerialDeviceChannel.DefaultBaudRate, factory.Serial!.Value.Baud);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    [Fact]
    public async Task OtaInputs_ReachTheUpdateServiceAndFlasher()
    {
        using var sandbox = new DataRootSandbox("devicemanager-ota-inputs");

        await UiTestHost.RunAsync(async () =>
        {
            var root = Path.Combine(sandbox.DataRoot, "files");
            var hmiSwu = Touch(root, "hmi-9.9.9.swu");
            var fwSwu = Touch(root, "firmware-9.9.9.swu");

            var detector = new FakeDetector();
            var update = new FakeUpdateService();
            var flasher = new FakeFlasher();
            var factory = new FakeChannelFactory();
            var view = new DeviceManagerView(detector, flasher, update, null, factory);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                // 先连上（假通道）
                ControlWiringFace.Find<ComboBox>(view, "CmbChannelType").SelectedIndex = 1;
                UiTestHost.Pump(2);
                ControlWiringFace.Write(view, "TxtHost", "127.0.0.1");
                Click(view, "BtnOpenClose");
                await UntilAsync(() => factory.Last.IsOpen, "假通道已打开");

                // ① 检测 ⇒ **只读展示框**被回填（`TxtDeviceVersion` 也有判据了）
                Click(view, "BtnDetect");
                await UntilAsync(() => ControlWiringFace.Find<TextBox>(view, "TxtDeviceVersion").Text == detector.HmiVersion,
                    "检测结果回填到只读展示框");
                Assert.Equal(1, detector.Calls);

                // ② 本地版本 ⇒ 更新服务收到它；返回的设备版本进展示框
                ControlWiringFace.Write(view, "TxtLocalVersion", "8.8.8");
                Click(view, "BtnCheckUpdate");
                await UntilAsync(() => update.CheckedLocal is not null, "更新服务收到本地版本");
                Assert.Equal("8.8.8", update.CheckedLocal);
                await UntilAsync(() => ControlWiringFace.Find<TextBox>(view, "TxtDeviceVersion").Text == update.DeviceVersion,
                    "比对结果里的设备版本进展示框");

                // ③ HMI 更新包路径 ⇒ 更新服务收到它
                ControlWiringFace.Write(view, "TxtHmiSwu", hmiSwu);
                Click(view, "BtnUpdateHmi");
                await UntilAsync(() => update.UpdatedSwu is not null, "更新服务收到 .swu 路径");
                Assert.Equal(hmiSwu, update.UpdatedSwu);

                // ④ 固件包路径 ⇒ 刷写器收到它
                ControlWiringFace.Write(view, "TxtFwSwu", fwSwu);
                Click(view, "BtnFlashFw");
                await UntilAsync(() => flasher.OtaSwu is not null, "刷写器收到固件包路径");
                Assert.Equal(fwSwu, flasher.OtaSwu);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    [Fact]
    public async Task FelInputs_ReachTheFlasher()
    {
        using var sandbox = new DataRootSandbox("devicemanager-fel-inputs");

        await UiTestHost.RunAsync(async () =>
        {
            var image = Touch(Path.Combine(sandbox.DataRoot, "files"), "boot.img");

            var flasher = new FakeFlasher();
            var view = new DeviceManagerView(null, flasher, null, null, null);
            view.Show();
            UiTestHost.Pump(5);
            try
            {
                ControlWiringFace.Write(view, "TxtFelTool", "/opt/sunxi-fel-fake");
                ControlWiringFace.Write(view, "TxtFelImage", image);
                ControlWiringFace.Write(view, "TxtFelArgs", "-p flash-write 0 {image}");

                Click(view, "BtnFelFlash");
                await UntilAsync(() => flasher.FelImage is not null, "刷写器收到 FEL 三格");

                Assert.Equal("/opt/sunxi-fel-fake", flasher.FelTool);
                Assert.Equal(image, flasher.FelImage);
                Assert.Equal("-p flash-write 0 {image}", flasher.FelArgs);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }
}
