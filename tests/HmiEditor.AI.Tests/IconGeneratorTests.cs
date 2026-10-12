using HmiEditor.AI.Generators;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// IconGenerator 测试 —— SVG 提取与尺寸规范化。
/// </summary>
public class IconGeneratorTests
{
    [Fact]
    public void ExtractSvg_PlainSvg_ReturnsSvg()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 48 48\"><path d=\"M0 0\"/></svg>";
        Assert.Equal(svg, IconGenerator.ExtractSvg(svg));
    }

    [Fact]
    public void ExtractSvg_FencedXml_StripsMarkers()
    {
        const string svg = "<svg viewBox=\"0 0 48 48\"><path d=\"M0 0\"/></svg>";
        var input = "```xml\n" + svg + "\n```";
        Assert.Equal(svg, IconGenerator.ExtractSvg(input));
    }

    [Fact]
    public void ExtractSvg_WithSurroundingText_ExtractsSvgFragment()
    {
        const string svg = "<svg viewBox=\"0 0 48 48\"><path d=\"M0 0\"/></svg>";
        var input = "这是图标:\n" + svg + "\n完";
        Assert.Equal(svg, IconGenerator.ExtractSvg(input));
    }

    [Fact]
    public void ExtractSvg_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Equal("", IconGenerator.ExtractSvg(""));
        Assert.Equal("", IconGenerator.ExtractSvg(null));
        Assert.Equal("", IconGenerator.ExtractSvg("   "));
    }

    [Fact]
    public void NormalizeSvgSize_OverridesDimensions()
    {
        const string svg = "<svg viewBox=\"0 0 64 64\" width=\"64\" height=\"64\"><path d=\"M0 0\"/></svg>";
        var normalized = IconGenerator.NormalizeSvgSize(svg);
        Assert.Contains("viewBox=\"0 0 48 48\"", normalized);
        Assert.Contains("width=\"48\"", normalized);
        Assert.Contains("height=\"48\"", normalized);
        Assert.DoesNotContain("64", normalized.Replace("<path d=\"M0 0\"/>", ""));
    }

    [Fact]
    public void NormalizeSvgSize_NoAttributes_AddsDefaults()
    {
        const string svg = "<svg><path d=\"M0 0\"/></svg>";
        var normalized = IconGenerator.NormalizeSvgSize(svg);
        Assert.Contains("viewBox=\"0 0 48 48\"", normalized);
        Assert.Contains("width=\"48\"", normalized);
        Assert.Contains("height=\"48\"", normalized);
    }
}
