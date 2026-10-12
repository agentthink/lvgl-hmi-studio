using System;
using System.Threading;
using System.Threading.Tasks;
using HmiEditor.Device.Channels;
using HmiEditor.Device.Detection;
using HmiEditor.Device.Firmware;
using HmiEditor.Device.Packaging;
using HmiEditor.Device.Updates;

namespace HmiEditor.Device.Abstractions;

/// <summary>
/// **设备服务的注入缝**（阶段 206）—— 把 <c>DeviceManagerView</c> 用到的那几个服务抽成接口，
/// 让宿主/测试可以替换实现。
///
/// <para><b>为什么要它</b>（登记 `#94`）：该视图原先把四个服务**直接 new 出来**
/// （<c>new DeviceDetector()</c> / <c>new DeviceFlasher()</c> / <c>new HmiUpdateService()</c> /
/// <c>new UsbUpdatePackager()</c>），通道也是就地 <c>new SerialDeviceChannel(...)</c> ——
/// 于是它那 13 个输入框在 headless 下**只有"打包"那三个驱动得动**（纯本地），
/// 其余的成功路径会去碰串口/网口/外部进程，原理上不可驱动。</para>
///
/// <para>本仓已有两处同类先例：<c>IPropertyValidator</c>（属性面板的即时校验插件点）与
/// <c>TimeProvider</c>（模拟器去 flaky 的时钟注入）。接口只声明**视图真正用到的那几个成员**，
/// 四个实现类原样 <c>implements</c>，行为零变化。</para>
/// </summary>
public interface IDeviceDetector
{
    /// <summary>读取设备信息（型号/架构/内核/固件/HMI 版本）。</summary>
    Task<DeviceInfo> DetectAsync(IDeviceChannel channel, CancellationToken ct = default);
}

/// <summary>HMI 程序 OTA 更新服务（版本比对 ＋ 下发）。</summary>
public interface IHmiUpdateService
{
    /// <summary>操作日志（供 UI 展示）。</summary>
    event Action<string>? Log;

    /// <summary>比对本地版本与设备版本。</summary>
    Task<UpdateCheckResult> CheckAsync(IDeviceChannel channel, string localVersion, CancellationToken ct = default);

    /// <summary>把 .swu 下发给设备并触发安装。返回是否已触发。</summary>
    Task<bool> UpdateAsync(IDeviceChannel channel, string localSwuPath, IProgress<double>? progress = null, CancellationToken ct = default);
}

/// <summary>固件/HMI 刷写器（OTA ＋ FEL 两条通道）。</summary>
public interface IDeviceFlasher
{
    /// <summary>操作日志（供 UI 展示）。</summary>
    event Action<string>? Log;

    /// <summary>OTA 刷写（上传 .swu 并触发设备端脚本）。</summary>
    Task FlashAsync(IDeviceChannel channel, OtaFlashOptions options, IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>FEL 模式烧录（调 PC 端外部工具）。</summary>
    Task<FelFlashResult> FelFlashAsync(FelFlashOptions options, CancellationToken ct = default);
}

/// <summary>USB 更新包生成器（纯本地打包，不碰设备）。</summary>
public interface IUsbUpdatePackager
{
    Task<UsbUpdatePackageResult> BuildAsync(UsbUpdateOptions options, CancellationToken ct = default);
}

/// <summary>完整部署服务（源码 ＋ 资源 ＋ .so ＋ 运行时配置）。</summary>
public interface IHmiDeployService
{
    /// <summary>操作日志（供 UI 展示）。</summary>
    event Action<string>? Log;

    /// <summary>把源码/资源/shared library/运行时配置部署到设备。</summary>
    Task<HmiDeployResult> DeployAsync(
        IDeviceChannel channel,
        IReadOnlyList<string> sourceFiles,
        string? sharedLibraryPath = null,
        IReadOnlyList<HmiDeployAsset>? assets = null,
        string remoteSourceDir = HmiDeployService.DefaultSourceDir,
        string? runtimeConfigText = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// **通道工厂** —— 视图原先就地 <c>new SerialDeviceChannel(...)</c> / <c>new TcpDeviceChannel(...)</c>，
/// 于是"端口号/波特率/主机"这些输入框**读没读**在测试里观测不到（构造完就去开通道了）。
/// 抽成工厂后可以替换成记录参数的假实现。
/// </summary>
public interface IDeviceChannelFactory
{
    /// <summary>按串口名 ＋ 波特率建通道。</summary>
    IDeviceChannel CreateSerial(string portName, int baudRate);

    /// <summary>按主机 ＋ 端口建 TCP 通道。</summary>
    IDeviceChannel CreateTcp(string host, int port);
}
