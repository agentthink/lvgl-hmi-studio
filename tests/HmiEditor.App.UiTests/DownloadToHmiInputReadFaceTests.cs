using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HmiEditor.App.Views;
using HmiEditor.Core.Services;
using HmiEditor.Device.Abstractions;
using HmiEditor.Device.Channels;
using HmiEditor.Device.Updates;
using Xunit;

namespace HmiEditor.App.UiTests;

/// <summary>
/// **`DownloadToHmiView` 那 4 个输入框的读点行为面**（阶段 207）—— 「读点在动作处理器」这一族的
/// 第二个视图（第一个是 `DeviceManagerView`，阶段 205/206）。
///
/// <para><b>读点</b>：`TxtHost` / `TxtTcpPort` / `TxtBaudRate` 在 <c>OnOpenCloseClicked</c> 里
/// 被读来**建通道**（原先就地 <c>new SerialDeviceChannel/TcpDeviceChannel</c>）；
/// `TxtRemoteDir` 在 <c>OnDownloadClicked</c> 里被读来当**部署目标目录**。</para>
///
/// <para><b>怎么驱动</b>：阶段 206 抽出的 <see cref="IDeviceChannelFactory"/> 直接复用；
/// 这一阶段给本视图补了 <see cref="IHmiDeployService"/> 这条缝（原先 <c>_deployService = new()</c>）——
/// 于是两根"假实现"就能把 4 个框全部驱动：假工厂记 (host,port)/(port,baud)，
/// 假部署服务记 <c>remoteSourceDir</c>。全部断言**不碰真设备**。</para>
/// </summary>
// DataRootSandbox 会改进程级数据根，必须与大队串行（见 LocalizationRefreshUiTests 的集合定义）
[Collection("Language-global")]
public class DownloadToHmiInputReadFaceTests
{
    private sealed class FakeChannel : IDeviceChannel
    {
        public string Name { get; init; } = "fake://deploy";
        public bool IsOpen { get; private set; }
        public string Kind => "fake";

        public Task OpenAsync(CancellationToken ct = default)
        {
            IsOpen = true;
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

    private sealed class FakeDeployService : IHmiDeployService
    {
#pragma warning disable CS0067   // 假实现不需要真的触发日志事件
        public event Action<string>? Log;
#pragma warning restore CS0067

        public string? RemoteSourceDir { get; private set; }
        public string? RuntimeConfigText { get; private set; }
        public int Calls { get; private set; }

        public Task<HmiDeployResult> DeployAsync(
            IDeviceChannel channel,
            IReadOnlyList<string> sourceFiles,
            string? sharedLibraryPath = null,
            IReadOnlyList<HmiDeployAsset>? assets = null,
            string remoteSourceDir = HmiDeployService.DefaultSourceDir,
            string? runtimeConfigText = null,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            Calls++;
            RemoteSourceDir = remoteSourceDir;
            RuntimeConfigText = runtimeConfigText;
            return Task.FromResult(new HmiDeployResult { SourceFilesUploaded = sourceFiles.Count });
        }
    }

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

    private static void Click(Control root, string name)
    {
        ControlWiringFace.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UiTestHost.Pump(2);
    }

    private static (DownloadToHmiView View, FakeChannelFactory Factory, FakeDeployService Deploy) Open(
        string dataRoot, string mainC)
    {
        var factory = new FakeChannelFactory();
        var deploy = new FakeDeployService();
        var view = new DownloadToHmiView(
            new[] { mainC },
            dataRoot,
            new[] { new HmiDeployAsset(mainC, "app/main.c") },
            "runtime-config-text",
            deploy,
            factory);
        view.Show();
        UiTestHost.Pump(5);
        return (view, factory, deploy);
    }

    [Fact]
    public async Task ChannelInputs_ReachTheChannelFactory()
    {
        using var sandbox = new DataRootSandbox("downloadtohmi-channel-inputs");
        Directory.CreateDirectory(sandbox.DataRoot);
        var mainC = Path.Combine(sandbox.DataRoot, "main.c");
        File.WriteAllText(mainC, "int main(void){return 0;}");

        await UiTestHost.RunAsync(async () =>
        {
            var (view, factory, _) = Open(sandbox.DataRoot, mainC);
            try
            {
                // ① 以太网：主机 ＋ 端口两格真的被读（工厂收到它们）
                ControlWiringFace.Find<ComboBox>(view, "CmbChannelType").SelectedIndex = 1;
                UiTestHost.Pump(2);
                ControlWiringFace.Write(view, "TxtHost", "10.1.2.3");
                ControlWiringFace.Write(view, "TxtTcpPort", "2023");
                Click(view, "BtnOpenClose");
                await UntilAsync(() => factory.Tcp is not null, "通道工厂收到 TCP 参数");
                Assert.Equal(("10.1.2.3", 2023), factory.Tcp);

                // ② 串口：波特率那格真的被读（夹具自备端口项 —— 容器的串口列表多半是空的）
                Click(view, "BtnOpenClose");   // 先关掉
                var ports = ControlWiringFace.Find<ComboBox>(view, "CmbPort");
                ports.ItemsSource = new[] { "COM_DEPLOY" };
                ControlWiringFace.Find<ComboBox>(view, "CmbChannelType").SelectedIndex = 0;
                ports.SelectedIndex = 0;
                UiTestHost.Pump(2);
                ControlWiringFace.Write(view, "TxtBaudRate", "57600");
                Click(view, "BtnOpenClose");
                await UntilAsync(() => factory.Serial is not null, "通道工厂收到串口参数");
                Assert.Equal(("COM_DEPLOY", 57600), factory.Serial);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    [Fact]
    public async Task RemoteDir_ReachesTheDeployService()
    {
        using var sandbox = new DataRootSandbox("downloadtohmi-remote-dir");
        Directory.CreateDirectory(sandbox.DataRoot);
        var mainC = Path.Combine(sandbox.DataRoot, "main.c");
        File.WriteAllText(mainC, "int main(void){return 0;}");

        await UiTestHost.RunAsync(async () =>
        {
            var (view, factory, deploy) = Open(sandbox.DataRoot, mainC);
            try
            {
                // 前置自检：没连设备时点「下载」只会提示（`RequireChannel()` 先挡）
                Click(view, "BtnDownload");
                await Task.Delay(50);
                Assert.Equal(0, deploy.Calls);

                // 连上（假通道）
                ControlWiringFace.Find<ComboBox>(view, "CmbChannelType").SelectedIndex = 1;
                UiTestHost.Pump(2);
                ControlWiringFace.Write(view, "TxtHost", "127.0.0.1");
                Click(view, "BtnOpenClose");
                await UntilAsync(() => factory.Last.IsOpen, "假通道已打开");

                // ① 驱动目标目录 ⇒ 部署服务收到它
                ControlWiringFace.Write(view, "TxtRemoteDir", "/mnt/UDISK/hmi/src-fake");
                Click(view, "BtnDownload");
                await UntilAsync(() => deploy.Calls > 0, "部署服务被调用");
                Assert.Equal("/mnt/UDISK/hmi/src-fake", deploy.RemoteSourceDir);

                // ② 清空 ⇒ 回退默认目录（`HmiDeployService.DefaultSourceDir`）—— 把回退口径也钉住
                ControlWiringFace.Write(view, "TxtRemoteDir", "   ");
                Click(view, "BtnDownload");
                await UntilAsync(() => deploy.RemoteSourceDir == HmiDeployService.DefaultSourceDir, "空目标目录回退到默认");
                Assert.Equal(2, deploy.Calls);
            }
            finally
            {
                view.Close();
                UiTestHost.Pump();
            }
        });
    }

    /// <summary>
    /// **二选一台账**：该视图的具名输入框要么被上面两条判据驱动、要么登记理由。
    /// （与 `DeviceManagerInputReadFaceTests` 同形；这一面只管本视图。）
    /// </summary>
    [Fact]
    public void EveryNamedInputBox_IsDrivenOrRegistered()
    {
        var axaml = File.ReadAllText(Path.Combine(
            SourceScanner.RepoRoot, ControlWiringFace.ViewsRelative, "DownloadToHmiView.axaml"));
        var named = ControlWiringFace.NamedTextBoxes(axaml)
            .Concat(ControlWiringFace.NamedNumberBoxes(axaml))
            .ToHashSet(StringComparer.Ordinal);

        var driven = new[] { "TxtHost", "TxtTcpPort", "TxtBaudRate", "TxtRemoteDir" };
        var registered = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TxtSoPath"] = "只读展示框（`IsReadOnly=\"True\"`，由「选择 .so」回填；真正下发用的是选中的路径变量）"
                + " —— 已在「输入框『谁读』面」的 DisplayOnly 登记表里（阶段 196/197）",
        };

        Assert.True(named.Count >= 5, $"只扫到 {named.Count} 个具名输入框 —— 扫描面塌了（阶段 207 实测 5 个）");

        var accounted = driven.Concat(registered.Keys).ToHashSet(StringComparer.Ordinal);
        var missing = named.Where(n => !accounted.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var ghosts = accounted.Where(n => !named.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0,
            "这些具名输入框既没被驱动、也没登记理由：" + string.Join(", ", missing));
        Assert.True(ghosts.Length == 0,
            "台账里这些框在 XAML 里不存在了：" + string.Join(", ", ghosts));
        Assert.True(registered.Values.All(v => v.Length >= 30), "登记理由太短，等于没写");
    }
}
