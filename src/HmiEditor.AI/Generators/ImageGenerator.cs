using System.Text.RegularExpressions;
using HmiEditor.AI.Inference;
using HmiEditor.Core.Services;

namespace HmiEditor.AI.Generators;

/// <summary>
/// 通用 SVG 图片生成器 —— 用户描述 + 自定义尺寸 → LLM 生成彩色 SVG → SvgParser 验证 → 规范化。
/// 区别于 <see cref="IconGenerator"/>（48×48 单色图标），本类支持任意尺寸的彩色矢量图。
/// </summary>
public sealed partial class ImageGenerator
{
    private readonly ILlmProvider _llm;
    private readonly PromptTemplateEngine _prompts;

    public ImageGenerator(ILlmProvider llm, PromptTemplateEngine prompts)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
    }

    /// <summary>生成结果: SVG 文本 + 形状数 + token 消耗。</summary>
    public sealed record ImageGenerationResult(
        string SvgText, int Width, int Height, int ShapeCount,
        int InputTokens, int OutputTokens);

    /// <summary>
    /// 生成 SVG 图片。默认尺寸 200×200。
    /// </summary>
    /// <param name="textProgress">可选的模型增量回调（流式）；null 时行为与旧版一致。</param>
    public Task<ImageGenerationResult> GenerateAsync(
        string userInput,
        int width = 200,
        int height = 200,
        CancellationToken ct = default,
        IProgress<string>? textProgress = null)
    {
        // 限制尺寸范围，防止过大导致 LLM 输出超长或过小无意义
        width = Math.Clamp(width, 32, 1024);
        height = Math.Clamp(height, 32, 1024);

        return GenerateInternalAsync(userInput, width, height, ct, textProgress);
    }

    private async Task<ImageGenerationResult> GenerateInternalAsync(
        string userInput, int width, int height, CancellationToken ct, IProgress<string>? textProgress)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            throw new ArgumentException("图片描述不能为空", nameof(userInput));

        var messages = _prompts.BuildImageMessages(userInput, width, height);
        var response = await LlmChat.WithProgressAsync(_llm, new LlmRequest
        {
            Messages = messages,
            Temperature = 0.4,
            MaxTokens = 4096
        }, textProgress, ct);

        var svg = ExtractSvg(response.Content);
        var (doc, error) = SvgParser.Parse(svg);
        if (doc is null || doc.Shapes.Count == 0)
        {
            // 带码异常（登记 #61a 第一刀）：原先抛裸 InvalidOperationException + 中文串 ⇒
            // ai.gen.failed 那 8 处的宽捕获拿不到码、只能渲染中文原文（英文界面下中英混排）。
            // SvgParser 解析成功但零形状时 error 为 null ⇒ 用 NoShapes 补齐那一个码。
            throw new SvgParseException(error ?? SvgParseError.NoShapes);
        }

        // 规范化尺寸到用户指定的 width×height
        svg = NormalizeSvgSize(svg, width, height);

        return new ImageGenerationResult(
            svg, width, height, doc.Shapes.Count,
            response.InputTokens, response.OutputTokens);
    }

    /// <summary>
    /// 从 LLM 响应中提取 SVG 文本 —— 容错: 剥离 ```xml/```svg 代码块标记, 提取 &lt;svg&gt;...&lt;/svg&gt; 片段。
    /// 与 IconGenerator.ExtractSvg 逻辑一致，但独立维护以允许未来差异化。
    /// </summary>
    internal static string ExtractSvg(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "";

        var text = content.Trim();
        const string fenceStart = "```";
        if (text.StartsWith(fenceStart, StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline > 0) text = text[(firstNewline + 1)..];
            var fenceEnd = text.LastIndexOf(fenceStart, StringComparison.Ordinal);
            if (fenceEnd > 0) text = text[..fenceEnd];
        }

        var match = SvgTagRegex().Match(text);
        return match.Success ? match.Value : text;
    }

    /// <summary>规范化尺寸: 将根 svg 元素的 width/height/viewBox 重写为指定尺寸。</summary>
    internal static string NormalizeSvgSize(string svg, int width, int height)
    {
        var viewBoxStr = $"\"0 0 {width} {height}\"";
        var widthStr = $"\"{width}\"";
        var heightStr = $"\"{height}\"";

        if (ViewBoxRegex().IsMatch(svg))
            svg = ViewBoxRegex().Replace(svg, $"viewBox={viewBoxStr}");
        else
            svg = SvgTagOpenRegex().Replace(svg, m => m.Value + $" viewBox={viewBoxStr}", 1);

        svg = WidthRegex().IsMatch(svg)
            ? WidthRegex().Replace(svg, $"width={widthStr}")
            : SvgTagOpenRegex().Replace(svg, m => m.Value + $" width={widthStr}", 1);

        svg = HeightRegex().IsMatch(svg)
            ? HeightRegex().Replace(svg, $"height={heightStr}")
            : SvgTagOpenRegex().Replace(svg, m => m.Value + $" height={heightStr}", 1);

        return svg;
    }

    [GeneratedRegex(@"<svg[\s\S]*?</svg>", RegexOptions.IgnoreCase)]
    private static partial Regex SvgTagRegex();

    [GeneratedRegex(@"<svg[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex SvgTagOpenRegex();

    [GeneratedRegex(@"viewBox\s*=\s*""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex ViewBoxRegex();

    [GeneratedRegex(@"\bwidth\s*=\s*""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex WidthRegex();

    [GeneratedRegex(@"\bheight\s*=\s*""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex HeightRegex();
}
