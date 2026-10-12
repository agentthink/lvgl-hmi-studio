using System;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;

namespace HmiEditor.RenderTests;

/// <summary>
/// 真光栅化的 headless 宿主（Skia 后端）。
///
/// <para>与 <c>HmiEditor.App.UiTests</c> 的 <c>UiTestHost</c> 的唯一区别是渲染后端：
/// 那边是 <c>UseHeadlessDrawing = true</c>（no-op 绘制，快、稳，但**不产生像素**），
/// 这边是 <c>UseHeadlessDrawing = false</c> + <c>UseSkia()</c>（真光栅化，慢一些，有像素）。</para>
///
/// <para><b>为什么必须分程序集：</b>平台配置是进程级的、同进程换不了第二套（2026-09-21 更正：原写
/// 「渲染循环（<c>DefaultRenderLoop</c>）是进程级静态单例、绑死在第一个会话的 UI 线程上」，经 IL 核实不成立，见专题 §8 的 #47）（实测抛
/// <c>InvalidOperationException: The calling thread cannot access this object</c>）。
/// VSTest 为每个测试程序集起独立 testhost 进程 ⇒ 这里能安全地拿到自己的配置。</para>
/// </summary>
public static class RenderTestHost
{
    private static readonly Lazy<HeadlessUnitTestSession> Session =
        new(() => HeadlessUnitTestSession.StartNew(typeof(RenderTestAppBuilder)),
            LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>在 headless（Skia）UI 线程上同步执行并返回结果；内部异常原样抛出（含 Assert 失败）。</summary>
    public static T Run<T>(Func<T> body) =>
        Session.Value.Dispatch(body, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>在 headless（Skia）UI 线程上同步执行。</summary>
    public static void Run(Action body) =>
        Session.Value.Dispatch(body, CancellationToken.None).GetAwaiter().GetResult();
}

/// <summary>本程序集的入口类型（Avalonia 约定：静态 <c>BuildAvaloniaApp</c>）。</summary>
public static class RenderTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        // 注意写成全限定名：本程序集的命名空间不在 HmiEditor.App 里面，
        // 裸写 App 会被解析成**命名空间** HmiEditor.App（CS0118）。
        AppBuilder.Configure<HmiEditor.App.App>()
            .UseSkia()
            // Skia 下没有默认字体时，凡是**排版文本**的路径都会抛
            // "Could not create glyphTypeface. Font family: $Default"，故显式带上 Inter。
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// 读回像素的唯一入口。刻意收成一处：像素格式与通道顺序（Skia 下是 <c>Bgra8888</c>）
/// 是"读错了也不报错、只是断言变成对噪声的断言"的那类细节。
/// </summary>
public static class PixelReader
{
    /// <summary>读整张位图，返回 BGRA 排列的字节数组（行优先，无 padding）。</summary>
    public static byte[] ReadAll(RenderTargetBitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var length = stride * size.Height;
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), buffer, length, stride);
            var bytes = new byte[length];
            Marshal.Copy(buffer, bytes, 0, length);
            return bytes;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>取某点颜色（BGRA 字节 + 尺寸）。</summary>
    public static (byte B, byte G, byte R, byte A) At(byte[] pixels, int width, int x, int y)
    {
        var i = 4 * (y * width + x);
        return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }
}
