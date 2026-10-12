using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

public class ImageOptimizerTests
{
    // ── Mode=Original：返回输入副本，不修改输入 ──

    [Fact]
    public void Optimize_Mode_Original_ReturnsCopy_NotSameReference()
    {
        byte[] input = { 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF };
        var options = new OptimizeOptions { Mode = OptimizeMode.Original };

        var result = ImageOptimizer.Optimize(input, options);

        Assert.Equal(input, result.OutputBytes);
        Assert.NotSame(input, result.OutputBytes);
    }

    [Fact]
    public void Optimize_Mode_Original_DoesNotModifyInput()
    {
        byte[] input = { 1, 2, 3, 4, 5, 6 };
        byte[] snapshot = (byte[])input.Clone();
        var options = new OptimizeOptions { Mode = OptimizeMode.Original };

        ImageOptimizer.Optimize(input, options);

        Assert.Equal(snapshot, input);
    }

    [Fact]
    public void Optimize_Mode_Original_HasZeroReduction()
    {
        byte[] input = { 9, 8, 7, 6, 5, 4 };
        var options = new OptimizeOptions { Mode = OptimizeMode.Original };

        var result = ImageOptimizer.Optimize(input, options);

        Assert.Equal(0, result.ReductionPercent);
        Assert.Equal(input.Length, result.OutputSizeBytes);
    }

    // ── Mode=Optimized：输出字节数 <= 输入 ──

    [Fact]
    public void Optimize_Mode_Optimized_OutputSize_LessOrEqual_Input()
    {
        // 9 字节 RGB888 = 3 像素
        byte[] input = { 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF };
        var options = new OptimizeOptions { Mode = OptimizeMode.Optimized };

        var result = ImageOptimizer.Optimize(input, options);

        Assert.True(result.OutputSizeBytes <= result.OriginalSizeBytes,
            $"输出 {result.OutputSizeBytes} 应 <= 输入 {result.OriginalSizeBytes}");
    }

    [Fact]
    public void Optimize_Mode_Optimized_ReducesToRgb565_WhenRawRgb888()
    {
        // 30 字节 = 10 像素 RGB888
        byte[] input = new byte[30];
        for (int i = 0; i < input.Length; i++) input[i] = (byte)(i * 8);
        var options = new OptimizeOptions { Mode = OptimizeMode.Optimized };

        var result = ImageOptimizer.Optimize(input, options);

        Assert.Equal(ImageColorDepth.RGB565, result.OutputColorDepth);
        // RGB565 = 2 字节/像素 -> 20 字节
        Assert.Equal(20, result.OutputSizeBytes);
    }

    // ── ReductionPercent 在 [0, 100] ──

    [Fact]
    public void Optimize_ReductionPercent_InRange_0_100()
    {
        byte[] input = new byte[30];
        for (int i = 0; i < input.Length; i++) input[i] = (byte)(i + 1);
        var options = new OptimizeOptions { Mode = OptimizeMode.Optimized };

        var result = ImageOptimizer.Optimize(input, options);

        Assert.True(result.ReductionPercent >= 0 && result.ReductionPercent <= 100,
            $"ReductionPercent {result.ReductionPercent} 应在 [0,100]");
    }

    [Fact]
    public void Optimize_ReductionPercent_Is33_ForRgb888ToRgb565()
    {
        byte[] input = new byte[30]; // 10 像素 RGB888
        var options = new OptimizeOptions { Mode = OptimizeMode.Optimized };

        var result = ImageOptimizer.Optimize(input, options);

        // (1 - 20/30) * 100 = 33.33...
        Assert.True(result.ReductionPercent > 33.0 && result.ReductionPercent < 34.0,
            $"ReductionPercent {result.ReductionPercent} 应接近 33.33");
    }

    // ── ConvertToRgb565 基本转换 ──

    [Fact]
    public void ConvertToRgb565_OutputLength_IsTwoThirds_OfInput()
    {
        byte[] input = new byte[30]; // 10 像素
        byte[] output = ImageOptimizer.ConvertToRgb565(input);

        Assert.Equal(20, output.Length);
        Assert.Equal(input.Length * 2 / 3, output.Length);
    }

    [Fact]
    public void ConvertToRgb565_WhitePixel_IsFFFF()
    {
        byte[] input = { 0xFF, 0xFF, 0xFF };

        byte[] output = ImageOptimizer.ConvertToRgb565(input);

        Assert.Equal(2, output.Length);
        Assert.Equal(0xFF, output[0]);
        Assert.Equal(0xFF, output[1]);
    }

    [Fact]
    public void ConvertToRgb565_BlackPixel_IsZero()
    {
        byte[] input = { 0x00, 0x00, 0x00 };

        byte[] output = ImageOptimizer.ConvertToRgb565(input);

        Assert.Equal(0x00, output[0]);
        Assert.Equal(0x00, output[1]);
    }

    [Fact]
    public void ConvertToRgb565_RedPixel_PreservesHighBits()
    {
        // 纯红 R=0xFF -> r5=0x1F；G=B=0
        byte[] input = { 0xFF, 0x00, 0x00 };

        byte[] output = ImageOptimizer.ConvertToRgb565(input);

        // rgb565 = (0x1F << 11) = 0xF800 -> 高字节 0xF8，低字节 0x00
        Assert.Equal(0xF8, output[0]);
        Assert.Equal(0x00, output[1]);
    }

    [Fact]
    public void ConvertToRgb565_DoesNotModifyInput()
    {
        byte[] input = { 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00 };
        byte[] snapshot = (byte[])input.Clone();

        ImageOptimizer.ConvertToRgb565(input);

        Assert.Equal(snapshot, input);
    }

    // ── DetectDimensions 解析 PNG 头 ──

    [Fact]
    public void DetectDimensions_ParsesPngHeader()
    {
        byte[] png = BuildMinimalPng(width: 64, height: 128);

        var dims = ImageOptimizer.DetectDimensions(png);

        Assert.NotNull(dims);
        Assert.Equal(64, dims!.Value.width);
        Assert.Equal(128, dims.Value.height);
    }

    [Fact]
    public void DetectDimensions_ParsesJpegHeader()
    {
        byte[] jpeg = BuildMinimalJpeg(width: 320, height: 240);

        var dims = ImageOptimizer.DetectDimensions(jpeg);

        Assert.NotNull(dims);
        Assert.Equal(320, dims!.Value.width);
        Assert.Equal(240, dims.Value.height);
    }

    [Fact]
    public void DetectDimensions_ReturnsNull_ForUnknownFormat()
    {
        byte[] data = { 1, 2, 3, 4, 5, 6, 7, 8 };

        var dims = ImageOptimizer.DetectDimensions(data);

        Assert.Null(dims);
    }

    // ── 纯函数隔离：多次调用结果一致，输入不变 ──

    [Fact]
    public void Optimize_MultipleCalls_Consistent_AndInputUnchanged()
    {
        byte[] input = new byte[30];
        for (int i = 0; i < input.Length; i++) input[i] = (byte)(i * 5);
        byte[] snapshot = (byte[])input.Clone();
        var options = new OptimizeOptions { Mode = OptimizeMode.Optimized };

        var r1 = ImageOptimizer.Optimize(input, options);
        var r2 = ImageOptimizer.Optimize(input, options);

        Assert.Equal(r1.OutputSizeBytes, r2.OutputSizeBytes);
        Assert.Equal(r1.OutputBytes, r2.OutputBytes);
        Assert.Equal(r1.ReductionPercent, r2.ReductionPercent);
        Assert.Equal(r1.OutputColorDepth, r2.OutputColorDepth);
        Assert.Equal(snapshot, input); // 输入未变
    }

    [Fact]
    public void ConvertToRgb565_MultipleCalls_Consistent_AndInputUnchanged()
    {
        byte[] input = { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60 };
        byte[] snapshot = (byte[])input.Clone();

        byte[] o1 = ImageOptimizer.ConvertToRgb565(input);
        byte[] o2 = ImageOptimizer.ConvertToRgb565(input);

        Assert.Equal(o1, o2);
        Assert.Equal(snapshot, input);
    }

    // ── Mode=Custom ──

    [Fact]
    public void Optimize_Mode_Custom_Rgb565_ConvertsRawRgb888()
    {
        byte[] input = new byte[30];
        var options = new OptimizeOptions
        {
            Mode = OptimizeMode.Custom,
            TargetColorDepth = ImageColorDepth.RGB565
        };

        var result = ImageOptimizer.Optimize(input, options);

        Assert.Equal(ImageColorDepth.RGB565, result.OutputColorDepth);
        Assert.Equal(20, result.OutputSizeBytes);
    }

    [Fact]
    public void Optimize_Mode_Custom_Rgb888_KeepsRaw()
    {
        byte[] input = new byte[30];
        var options = new OptimizeOptions
        {
            Mode = OptimizeMode.Custom,
            TargetColorDepth = ImageColorDepth.RGB888
        };

        var result = ImageOptimizer.Optimize(input, options);

        Assert.Equal(ImageColorDepth.RGB888, result.OutputColorDepth);
        Assert.Equal(30, result.OutputSizeBytes);
        Assert.Equal(0, result.ReductionPercent);
    }

    [Fact]
    public void Optimize_Mode_Optimized_MarksSuggestedSize_WhenPngExceedsMaxTexture()
    {
        // 512x512 PNG，超过默认 256x256
        byte[] png = BuildMinimalPng(width: 512, height: 512);
        var options = new OptimizeOptions { Mode = OptimizeMode.Optimized };

        var result = ImageOptimizer.Optimize(png, options);

        Assert.True(result.OutputWidth <= 256, $"建议宽度 {result.OutputWidth} 应 <= 256");
        Assert.True(result.OutputHeight <= 256, $"建议高度 {result.OutputHeight} 应 <= 256");
        Assert.Equal(ImageColorDepth.RGB565, result.OutputColorDepth);
    }

    // ── 防御：null 输入 ──

    [Fact]
    public void Optimize_NullInput_ThrowsArgumentNullException()
    {
        var options = new OptimizeOptions();

        Assert.Throws<ArgumentNullException>(() => ImageOptimizer.Optimize(null!, options));
    }

    [Fact]
    public void Optimize_NullOptions_ThrowsArgumentNullException()
    {
        byte[] input = { 1, 2, 3 };

        Assert.Throws<ArgumentNullException>(() => ImageOptimizer.Optimize(input, null!));
    }

    [Fact]
    public void ConvertToRgb565_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ImageOptimizer.ConvertToRgb565(null!));
    }

    [Fact]
    public void DetectDimensions_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ImageOptimizer.DetectDimensions(null!));
    }

    // ── 辅助：构造最小 PNG / JPEG 字节序列 ──

    private static byte[] BuildMinimalPng(int width, int height)
    {
        byte[] png = new byte[24];
        // PNG 签名
        png[0] = 0x89; png[1] = 0x50; png[2] = 0x4E; png[3] = 0x47;
        png[4] = 0x0D; png[5] = 0x0A; png[6] = 0x1A; png[7] = 0x0A;
        // IHDR chunk length = 13
        png[8] = 0x00; png[9] = 0x00; png[10] = 0x00; png[11] = 0x0D;
        // "IHDR"
        png[12] = 0x49; png[13] = 0x48; png[14] = 0x44; png[15] = 0x52;
        // width（大端，offset 16）
        png[16] = (byte)(width >> 24);
        png[17] = (byte)(width >> 16);
        png[18] = (byte)(width >> 8);
        png[19] = (byte)width;
        // height（大端，offset 20）
        png[20] = (byte)(height >> 24);
        png[21] = (byte)(height >> 16);
        png[22] = (byte)(height >> 8);
        png[23] = (byte)height;
        return png;
    }

    private static byte[] BuildMinimalJpeg(int width, int height)
    {
        using var ms = new System.IO.MemoryStream();
        // SOI
        ms.WriteByte(0xFF); ms.WriteByte(0xD8);
        // APP0 marker（FF E0），段长度 16
        ms.WriteByte(0xFF); ms.WriteByte(0xE0);
        ms.WriteByte(0x00); ms.WriteByte(0x10);
        // "JFIF\0" + 9 字节填充，凑满段载荷 14 字节
        ms.Write(new byte[] { 0x4A, 0x46, 0x49, 0x46, 0x00 }, 0, 5);
        ms.Write(new byte[9], 0, 9);
        // SOF0 marker（FF C0），段长度 17
        ms.WriteByte(0xFF); ms.WriteByte(0xC0);
        ms.WriteByte(0x00); ms.WriteByte(0x11);
        ms.WriteByte(0x08); // 精度 8 位
        ms.WriteByte((byte)(height >> 8)); ms.WriteByte((byte)height); // 高度
        ms.WriteByte((byte)(width >> 8)); ms.WriteByte((byte)width);   // 宽度
        ms.WriteByte(0x03); // 3 个分量
        ms.Write(new byte[9], 0, 9); // 分量规格
        return ms.ToArray();
    }
}
