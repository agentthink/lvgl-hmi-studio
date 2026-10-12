using System.Text.RegularExpressions;
using HmiEditor.AI.Inference;
using HmiEditor.Core.Services;

namespace HmiEditor.AI.Generators;

/// <summary>
/// NL→SVG 图标生成器 —— 用户描述 → LLM 生成精简 SVG → SvgParser 验证 → 规范化。
/// 采用"LLM 直接生成 SVG path"路线(计划备选方案), 相比 Seedream 图片后处理
/// 更轻量且路径精简, 适合嵌入式渲染。
/// </summary>
public sealed partial class IconGenerator
{
    private readonly ILlmProvider _llm;
    private readonly PromptTemplateEngine _prompts;

    public IconGenerator(ILlmProvider llm, PromptTemplateEngine prompts)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
    }

    /// <summary>生成结果: SVG 文本 + 形状数 + 分类 + 变体 + token 消耗。</summary>
    public sealed record IconGenerationResult(
        string SvgText, int ShapeCount, int InputTokens, int OutputTokens)
    {
        /// <summary>本次生成使用的语义分类。</summary>
        public IconCategory Category { get; init; } = IconCategory.General;

        /// <summary>本次生成使用的视觉风格变体。</summary>
        public IconStyleVariant Variant { get; init; } = IconStyleVariant.Linear;
    }

    /// <summary>
    /// 生成图标。自动进行语义分类, 使用默认变体(Linear)。
    /// </summary>
    public Task<IconGenerationResult> GenerateAsync(
        string userInput,
        CancellationToken ct = default)
    {
        var category = IconCategoryClassifier.Classify(userInput);
        return GenerateAsync(userInput, category, IconVariantSelector.GetDefaultVariant(category), ct);
    }

    /// <summary>
    /// 生成图标 —— 指定分类与视觉风格变体。
    /// 温度依据变体动态调整(见 <see cref="IconVariantCatalog.GetTemperature"/>),
    /// 简洁变体低温度保真, 复杂变体略高温度增加视觉多样。
    /// </summary>
    /// <param name="textProgress">可选的模型增量回调（流式）；null 时行为与旧版一致。</param>
    public async Task<IconGenerationResult> GenerateAsync(
        string userInput,
        IconCategory category,
        IconStyleVariant variant,
        CancellationToken ct = default,
        IProgress<string>? textProgress = null)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            throw new ArgumentException("图标描述不能为空", nameof(userInput));

        var messages = _prompts.BuildIconMessages(userInput, category, variant);
        var temperature = IconVariantCatalog.GetTemperature(variant);
        var response = await LlmChat.WithProgressAsync(_llm, new LlmRequest
        {
            Messages = messages,
            Temperature = temperature,
            MaxTokens = 2048
        }, textProgress, ct);

        var svg = ExtractSvg(response.Content);
        var (doc, error) = SvgParser.Parse(svg);
        if (doc is null || doc.Shapes.Count == 0)
        {
            // 带码异常（登记 #61a 第一刀）：见 ImageGenerator 同处的注释。
            throw new SvgParseException(error ?? SvgParseError.NoShapes);
        }

        // 尺寸规范化: 强制 viewBox 48x48 (SvgParser 已解析尺寸, 重写根属性)
        svg = NormalizeSvgSize(svg);

        return new IconGenerationResult(svg, doc.Shapes.Count, response.InputTokens, response.OutputTokens)
        {
            Category = category,
            Variant = variant
        };
    }

    /// <summary>
    /// 从 LLM 响应中提取 SVG 文本 —— 容错: 剥离 ```xml/```svg 代码块标记, 提取 <svg>...</svg> 片段。
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

        // 提取 <svg ...> ... </svg> 片段
        var match = SvgTagRegex().Match(text);
        return match.Success ? match.Value : text;
    }

    /// <summary>规范化尺寸: 将根 svg 元素的 width/height/viewBox 重写为 48x48; 缺失则插入。</summary>
    internal static string NormalizeSvgSize(string svg)
    {
        // 统一/插入 viewBox —— 插到根 svg 元素属性区
        if (ViewBoxRegex().IsMatch(svg))
        {
            svg = ViewBoxRegex().Replace(svg, "viewBox=\"0 0 48 48\"");
        }
        else
        {
            svg = SvgTagOpenRegex().Replace(svg, m => m.Value + " viewBox=\"0 0 48 48\"", 1);
        }

        svg = WidthRegex().IsMatch(svg)
            ? WidthRegex().Replace(svg, "width=\"48\"")
            : SvgTagOpenRegex().Replace(svg, m => m.Value + " width=\"48\"", 1);

        svg = HeightRegex().IsMatch(svg)
            ? HeightRegex().Replace(svg, "height=\"48\"")
            : SvgTagOpenRegex().Replace(svg, m => m.Value + " height=\"48\"", 1);

        return svg;
    }

    [GeneratedRegex(@"<svg[\s\S]*?</svg>", RegexOptions.IgnoreCase)]
    private static partial Regex SvgTagRegex();

    [GeneratedRegex(@"<svg[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex SvgTagOpenRegex();

    [GeneratedRegex(@"viewBox\s*=\s*""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex ViewBoxRegex();

    [GeneratedRegex(@"width\s*=\s*""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex WidthRegex();

    [GeneratedRegex(@"height\s*=\s*""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex HeightRegex();
}
