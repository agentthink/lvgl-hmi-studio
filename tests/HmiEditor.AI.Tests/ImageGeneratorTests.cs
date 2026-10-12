using HmiEditor.AI.Generators;
using HmiEditor.AI.Inference;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// ImageGenerator 单元测试 —— SVG 提取、尺寸规范化、验证。
/// </summary>
public class ImageGeneratorTests
{
    private static readonly string ValidSvg =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 200" width="200" height="200"><rect x="10" y="10" width="180" height="180" fill="#1e293b" stroke="#3b82f6" stroke-width="2"/><circle cx="100" cy="100" r="50" fill="#3b82f6"/><path d="M50 150 L100 100 L150 150 Z" fill="#22c55e"/></svg>""";

    [Fact]
    public async Task GenerateAsync_ValidSvg_ReturnsNormalizedResult()
    {
        var llm = new FakeLlmProvider(_ => ValidSvg);
        var prompts = new PromptTemplateEngine();
        var gen = new ImageGenerator(llm, prompts);

        var result = await gen.GenerateAsync("一个蓝色圆形", 300, 200);

        Assert.Contains("<svg", result.SvgText);
        Assert.Equal(300, result.Width);
        Assert.Equal(200, result.Height);
        Assert.True(result.ShapeCount > 0);
        // 确认尺寸规范化写入
        Assert.Contains("width=\"300\"", result.SvgText);
        Assert.Contains("height=\"200\"", result.SvgText);
        Assert.Contains("viewBox=\"0 0 300 200\"", result.SvgText);
    }

    [Fact]
    public async Task GenerateAsync_ExtractsSvgFromCodeFence()
    {
        var llm = new FakeLlmProvider(_ => $"```svg\n{ValidSvg}\n```");
        var prompts = new PromptTemplateEngine();
        var gen = new ImageGenerator(llm, prompts);

        var result = await gen.GenerateAsync("test");

        Assert.Contains("<svg", result.SvgText);
        Assert.Contains("</svg>", result.SvgText);
        Assert.DoesNotContain("```", result.SvgText);
    }

    [Fact]
    public async Task GenerateAsync_ExtractsSvgFromPrefixedText()
    {
        var llm = new FakeLlmProvider(_ => $"好的，这是您要的图片：\n{ValidSvg}\n希望满意！");
        var prompts = new PromptTemplateEngine();
        var gen = new ImageGenerator(llm, prompts);

        var result = await gen.GenerateAsync("test");

        Assert.StartsWith("<svg", result.SvgText);
        Assert.EndsWith("</svg>", result.SvgText);
    }

    [Fact]
    public async Task GenerateAsync_InvalidSvg_Throws()
    {
        var llm = new FakeLlmProvider(_ => "这不是有效的SVG");
        var prompts = new PromptTemplateEngine();
        var gen = new ImageGenerator(llm, prompts);

        // 阶段 87（登记 #61a）：改断**带码**的 SvgParseException（原为裸 InvalidOperationException）。
        var ex = await Assert.ThrowsAsync<SvgParseException>(() => gen.GenerateAsync("test"));
        Assert.Equal(SvgParseIssueCodes.XML_INVALID, ex.Error.Code);
    }

    [Fact]
    public async Task GenerateAsync_EmptyInput_Throws()
    {
        var llm = new FakeLlmProvider(_ => ValidSvg);
        var prompts = new PromptTemplateEngine();
        var gen = new ImageGenerator(llm, prompts);

        await Assert.ThrowsAsync<ArgumentException>(() => gen.GenerateAsync(""));
    }

    [Fact]
    public async Task GenerateAsync_ClampsSizeToValidRange()
    {
        var llm = new FakeLlmProvider(_ => ValidSvg);
        var prompts = new PromptTemplateEngine();
        var gen = new ImageGenerator(llm, prompts);

        // 过小 → 限制到最小值 32
        var result = await gen.GenerateAsync("test", 1, 1);
        Assert.Equal(32, result.Width);
        Assert.Equal(32, result.Height);

        // 过大 → 限制到最大值 1024
        var result2 = await gen.GenerateAsync("test", 9999, 9999);
        Assert.Equal(1024, result2.Width);
        Assert.Equal(1024, result2.Height);
    }

    [Fact]
    public void ExtractSvg_HandlesMarkdownFence()
    {
        var svg = ImageGenerator.ExtractSvg($"```xml\n{ValidSvg}\n```");
        Assert.StartsWith("<svg", svg);
        Assert.DoesNotContain("```", svg);
    }

    [Fact]
    public void ExtractSvg_HandlesNull()
    {
        Assert.Equal("", ImageGenerator.ExtractSvg(null));
        Assert.Equal("", ImageGenerator.ExtractSvg(""));
    }

    [Fact]
    public void NormalizeSvgSize_OverwritesExistingDimensions()
    {
        var svg = ImageGenerator.NormalizeSvgSize(ValidSvg, 400, 300);
        Assert.Contains("width=\"400\"", svg);
        Assert.Contains("height=\"300\"", svg);
        Assert.Contains("viewBox=\"0 0 400 300\"", svg);
        // 确保旧尺寸被覆盖
        Assert.DoesNotContain("width=\"200\"", svg);
        Assert.DoesNotContain("height=\"200\"", svg);
    }
}
