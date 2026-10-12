namespace HmiEditor.Core.Services;

/// <summary>
/// 图片导出位深 —— 「这个 PNG 存成多少位」。
/// <para>
/// <b>不要与 <c>Hmi.Hardware.Contracts.ColorDepth</c> 混用</b>：那一个是
/// 「面板/设备支持哪些显示色深」（<c>RGB565 / ARGB8888 / Indexed8</c>），
/// 这一端管的是图片落地格式（多出 <c>RGB888 / Indexed256 / Indexed16</c>，且没有 ARGB8888）。
/// 两者曾经同名（都叫 <c>ColorDepth</c>）且同时被 <c>ProjectSettingsView</c> 引入，
/// 编译器直接报 CS0104 歧义引用 —— 故此改名。
/// </para>
/// </summary>
public enum ImageColorDepth
{
    /// <summary>16 位/像素（R5 G6 B5），工业 HMI 最常用。</summary>
    RGB565,

    /// <summary>24 位/像素（R8 G8 B8），真彩色。</summary>
    RGB888,

    /// <summary>256 色调色板，8 位/像素。</summary>
    Indexed256,

    /// <summary>16 色调色板，4 位/像素。</summary>
    Indexed16
}

/// <summary>
/// 图片优化模式。
/// </summary>
public enum OptimizeMode
{
    /// <summary>保留原图，不做任何优化。</summary>
    Original,

    /// <summary>自动优化：超大图标记缩放，色深降为 RGB565。</summary>
    Optimized,

    /// <summary>自定义：按指定色深与尺寸处理。</summary>
    Custom
}

/// <summary>
/// 图片优化选项。
/// </summary>
public class OptimizeOptions
{
    /// <summary>优化模式。默认 <see cref="OptimizeMode.Optimized"/>。</summary>
    public OptimizeMode Mode { get; set; } = OptimizeMode.Optimized;

    /// <summary>目标色深（Custom 模式生效）。默认 <see cref="ImageColorDepth.RGB565"/>。</summary>
    public ImageColorDepth TargetColorDepth { get; set; } = ImageColorDepth.RGB565;

    /// <summary>最大宽度（像素）。默认 256。</summary>
    public int MaxWidth { get; set; } = ImageOptimizer.MaxTextureSize;

    /// <summary>最大高度（像素）。默认 256。</summary>
    public int MaxHeight { get; set; } = ImageOptimizer.MaxTextureSize;

    /// <summary>质量 0..100。默认 80。</summary>
    public int Quality { get; set; } = 80;
}

/// <summary>
/// 图片优化结果。
/// </summary>
public class OptimizeResult
{
    /// <summary>输出字节。</summary>
    public byte[] OutputBytes { get; set; } = Array.Empty<byte>();

    /// <summary>输出字节数。</summary>
    public long OutputSizeBytes { get; set; }

    /// <summary>原始字节数。</summary>
    public long OriginalSizeBytes { get; set; }

    /// <summary>输出宽度（像素）。若输入超阈值则为等比缩放后的建议宽度。</summary>
    public int OutputWidth { get; set; }

    /// <summary>输出高度（像素）。若输入超阈值则为等比缩放后的建议高度。</summary>
    public int OutputHeight { get; set; }

    /// <summary>输出色深。</summary>
    public ImageColorDepth OutputColorDepth { get; set; } = ImageColorDepth.RGB888;

    /// <summary>体积缩减百分比，范围 [0, 100]。</summary>
    public double ReductionPercent { get; set; }
}

/// <summary>
/// 纯函数图片优化器。输入字节 → 输出字节 + 元数据。
/// 不修改输入数组，不读写文件系统，不依赖平台图像库（仅纯 .NET 字节操作）。
/// </summary>
public static class ImageOptimizer
{
    /// <summary>工业 HMI 默认最大纹理尺寸（像素）。</summary>
    public const int MaxTextureSize = 256;

    /// <summary>PNG 文件签名。</summary>
    private static readonly byte[] s_pngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>
    /// 优化图片。纯函数：绝不修改 <paramref name="inputBytes"/>。
    /// </summary>
    /// <param name="inputBytes">输入图片字节（PNG/JPEG 文件 或 已解码的 RGB888 原始像素数据）。</param>
    /// <param name="options">优化选项。</param>
    /// <returns>优化结果，<see cref="OptimizeResult.OutputBytes"/> 为新数组副本。</returns>
    public static OptimizeResult Optimize(byte[] inputBytes, OptimizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(inputBytes);
        ArgumentNullException.ThrowIfNull(options);

        // 永不修改输入：创建副本进行所有后续操作
        byte[] working = inputBytes.ToArray();
        long originalSize = working.Length;

        var result = new OptimizeResult
        {
            OriginalSizeBytes = originalSize,
            OutputBytes = working,
            OutputSizeBytes = originalSize,
            OutputColorDepth = ImageColorDepth.RGB888,
            OutputWidth = 0,
            OutputHeight = 0,
            ReductionPercent = 0
        };

        switch (options.Mode)
        {
            case OptimizeMode.Original:
                HandleOriginal(working, result);
                break;
            case OptimizeMode.Optimized:
                HandleOptimized(working, options, result);
                break;
            case OptimizeMode.Custom:
                HandleCustom(working, options, result);
                break;
        }

        result.ReductionPercent = ComputeReduction(originalSize, result.OutputSizeBytes);
        return result;
    }

    /// <summary>
    /// 简单解析 PNG/JPEG 头获取尺寸。
    /// <para>PNG: IHDR chunk 宽度位于 offset 16，高度位于 offset 20（大端）。</para>
    /// <para>JPEG: 扫描 SOF0(0xC0)/SOF2(0xC2) marker。</para>
    /// </summary>
    /// <returns>解析失败或无法识别的格式返回 null。</returns>
    public static (int width, int height)? DetectDimensions(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length >= 24 && IsPng(bytes))
        {
            int width = ReadBigEndianInt32(bytes, 16);
            int height = ReadBigEndianInt32(bytes, 20);
            if (width > 0 && height > 0)
                return (width, height);
            return null;
        }

        if (bytes.Length >= 4 && IsJpeg(bytes))
        {
            return ParseJpegDimensions(bytes);
        }

        return null;
    }

    /// <summary>
    /// 纯字节转换：RGB888（3 字节/像素）→ RGB565（2 字节/像素）。
    /// 不修改输入。输出长度 = 输入长度 * 2 / 3（仅处理完整像素，余数忽略）。
    /// </summary>
    public static byte[] ConvertToRgb565(byte[] rgb888Data)
    {
        ArgumentNullException.ThrowIfNull(rgb888Data);

        int pixelCount = rgb888Data.Length / 3;
        byte[] output = new byte[pixelCount * 2];

        for (int i = 0; i < pixelCount; i++)
        {
            byte r = rgb888Data[i * 3];
            byte g = rgb888Data[i * 3 + 1];
            byte b = rgb888Data[i * 3 + 2];

            // 取高位：R5 / G6 / B5
            int r5 = r >> 3;
            int g6 = g >> 2;
            int b5 = b >> 3;

            ushort rgb565 = (ushort)((r5 << 11) | (g6 << 5) | b5);

            // 大端存储：高字节在前（嵌入式常见约定）
            output[i * 2] = (byte)(rgb565 >> 8);
            output[i * 2 + 1] = (byte)(rgb565 & 0xFF);
        }

        return output;
    }

    // ── 各模式处理 ──

    private static void HandleOriginal(byte[] working, OptimizeResult result)
    {
        // 直接返回输入副本，不做任何优化
        result.OutputBytes = working;
        result.OutputSizeBytes = working.Length;
        result.OutputColorDepth = ImageColorDepth.RGB888;

        var dims = DetectDimensions(working);
        if (dims is { } d)
        {
            result.OutputWidth = d.width;
            result.OutputHeight = d.height;
        }
    }

    private static void HandleOptimized(byte[] working, OptimizeOptions options, OptimizeResult result)
    {
        var dims = DetectDimensions(working);

        if (dims is { } d)
        {
            // PNG/JPEG 压缩格式：不依赖平台图像库无法解码像素做色深转换。
            // 占位返回原图字节，但记录建议尺寸（若超阈值）与目标色深。
            var suggested = ComputeSuggestedSize(d.width, d.height, options);
            result.OutputWidth = suggested.width;
            result.OutputHeight = suggested.height;
            result.OutputColorDepth = ImageColorDepth.RGB565;
            result.OutputBytes = working;
            result.OutputSizeBytes = working.Length;
        }
        else
        {
            // 视为已解码的 RGB888 原始像素数据，实际执行色深转换
            byte[] converted = ConvertToRgb565(working);
            result.OutputBytes = converted;
            result.OutputSizeBytes = converted.Length;
            result.OutputColorDepth = ImageColorDepth.RGB565;
            result.OutputWidth = 0;
            result.OutputHeight = 0;
        }
    }

    private static void HandleCustom(byte[] working, OptimizeOptions options, OptimizeResult result)
    {
        var dims = DetectDimensions(working);
        var depth = options.TargetColorDepth;

        if (dims is { } d)
        {
            // 压缩格式：占位返回原图字节，记录建议尺寸与目标色深
            var suggested = ComputeSuggestedSize(d.width, d.height, options);
            result.OutputWidth = suggested.width;
            result.OutputHeight = suggested.height;
            result.OutputColorDepth = depth;
            result.OutputBytes = working;
            result.OutputSizeBytes = working.Length;
            return;
        }

        // RGB888 原始像素数据
        switch (depth)
        {
            case ImageColorDepth.RGB565:
                byte[] converted = ConvertToRgb565(working);
                result.OutputBytes = converted;
                result.OutputSizeBytes = converted.Length;
                result.OutputColorDepth = ImageColorDepth.RGB565;
                break;

            case ImageColorDepth.RGB888:
                result.OutputBytes = working;
                result.OutputSizeBytes = working.Length;
                result.OutputColorDepth = ImageColorDepth.RGB888;
                break;

            default:
                // Indexed256 / Indexed16：本版仅标记目标色深，占位返回原图
                result.OutputBytes = working;
                result.OutputSizeBytes = working.Length;
                result.OutputColorDepth = depth;
                break;
        }
    }

    // ── 尺寸建议 ──

    /// <summary>
    /// 等比缩放使最长边不超过阈值。若已满足则原样返回。
    /// </summary>
    private static (int width, int height) ComputeSuggestedSize(int width, int height, OptimizeOptions options)
    {
        int maxW = options.MaxWidth > 0 ? options.MaxWidth : MaxTextureSize;
        int maxH = options.MaxHeight > 0 ? options.MaxHeight : MaxTextureSize;

        if (width <= maxW && height <= maxH)
            return (width, height);

        double scaleW = (double)maxW / width;
        double scaleH = (double)maxH / height;
        double scale = Math.Min(scaleW, scaleH);

        int newW = Math.Max(1, (int)Math.Round(width * scale));
        int newH = Math.Max(1, (int)Math.Round(height * scale));
        return (newW, newH);
    }

    // ── 格式识别与解析 ──

    private static bool IsPng(byte[] bytes)
    {
        if (bytes.Length < 8) return false;
        for (int i = 0; i < 8; i++)
            if (bytes[i] != s_pngSignature[i]) return false;
        return true;
    }

    private static bool IsJpeg(byte[] bytes)
    {
        return bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;
    }

    private static (int width, int height)? ParseJpegDimensions(byte[] bytes)
    {
        int i = 2; // 跳过 SOI (FF D8)
        while (i < bytes.Length - 1)
        {
            if (bytes[i] != 0xFF)
            {
                i++;
                continue;
            }

            // 跳过填充 FF
            while (i < bytes.Length && bytes[i] == 0xFF) i++;
            if (i >= bytes.Length) break;

            byte marker = bytes[i];
            i++;

            // SOF0 (C0) / SOF2 (C2) 携带尺寸信息
            if (marker == 0xC0 || marker == 0xC2)
            {
                // 长度(2) + 精度(1) + 高度(2) + 宽度(2)
                if (i + 7 > bytes.Length) return null;
                int height = ReadBigEndianInt16(bytes, i + 3);
                int width = ReadBigEndianInt16(bytes, i + 5);
                if (width > 0 && height > 0)
                    return (width, height);
                return null;
            }

            // 无载荷 marker
            if (marker == 0xD8 || marker == 0xD9 // SOI / EOI
                || (marker >= 0xD0 && marker <= 0xD7) // RSTn
                || marker == 0x01)
            {
                continue;
            }

            // 其它 marker：读取段长度并跳过
            if (i + 2 > bytes.Length) return null;
            int len = ReadBigEndianInt16(bytes, i);
            if (len < 2) return null;
            i += len;
        }

        return null;
    }

    // ── 大端读取辅助 ──

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24)
             | (bytes[offset + 1] << 16)
             | (bytes[offset + 2] << 8)
             | bytes[offset + 3];
    }

    private static int ReadBigEndianInt16(byte[] bytes, int offset)
    {
        return (bytes[offset] << 8) | bytes[offset + 1];
    }

    private static double ComputeReduction(long originalSize, long outputSize)
    {
        if (originalSize <= 0) return 0;
        double pct = (1.0 - (double)outputSize / originalSize) * 100;
        return Math.Max(0, Math.Min(100, pct));
    }
}
