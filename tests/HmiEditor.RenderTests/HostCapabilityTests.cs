using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Xunit;

namespace HmiEditor.RenderTests;

/// <summary>
/// **元判据**：本宿主真的能出像素吗？
///
/// <para>这条测试守护的不是产品行为，而是**上面那些像素判据的有效性**。
/// <c>UseHeadlessDrawing = true</c> 时 <c>CopyPixels</c> 读回的是**未初始化内存**
/// （实测前 16 字节形如 <c>12-C0-E4-F8-07-…</c>、非零字节 648/1024 全是噪声），
/// 那种情况下像素断言要么偶然通过、要么随机失败 —— 比没有判据更糟。
/// 所以：谁把后端配置改回 no-op，这条必须先红。</para>
/// </summary>
public class HostCapabilityTests
{
    [Fact]
    public void Rasterization_ProducesRealPixels()
    {
        RenderTestHost.Run(() =>
        {
            const int W = 16, H = 16;

            using var rtb = new RenderTargetBitmap(new PixelSize(W, H));
            using (var dc = rtb.CreateDrawingContext())
                dc.FillRectangle(new SolidColorBrush(Color.FromRgb(255, 0, 0)), new Rect(0, 0, W, H));

            var pixels = PixelReader.ReadAll(rtb);
            var center = PixelReader.At(pixels, W, 8, 8);

            Assert.Equal(255, center.R);
            Assert.Equal(0, center.G);
            Assert.Equal(0, center.B);
            Assert.Equal(255, center.A);
        });
    }
}
