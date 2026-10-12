using HmiEditor.Core.Models;
using System.Linq;
using Xunit;

namespace HmiEditor.Core.Tests;

public class StylePresetTests
{
    [Fact]
    public void DefaultPresets_ContainsFivePresets()
    {
        var presets = StylePreset.DefaultPresets;
        Assert.Equal(5, presets.Count);
    }

    [Fact]
    public void DefaultPresets_AllHaveNonNullTheme()
    {
        var presets = StylePreset.DefaultPresets;
        Assert.All(presets, p => Assert.NotNull(p.Theme));
    }

    [Theory]
    [InlineData("工业灰")]
    [InlineData("海洋蓝")]
    [InlineData("高对比度")]
    [InlineData("暗夜绿")]
    [InlineData("暖色调")]
    public void DefaultPresets_ContainsPreset(string name)
    {
        var presets = StylePreset.DefaultPresets;
        Assert.Contains(presets, p => p.Name == name);
    }

    [Fact]
    public void DefaultPresets_AllColorsAreHexFormat()
    {
        var presets = StylePreset.DefaultPresets;
        foreach (var preset in presets)
        {
            var theme = preset.Theme;
            var colors = new[] {
                theme.PrimaryColor, theme.SecondaryColor,
                theme.SurfaceColor, theme.BackgroundColor,
                theme.TextColor, theme.BorderColor,
                theme.SuccessColor, theme.WarningColor, theme.ErrorColor
            };
            Assert.All(colors, c => Assert.StartsWith("#", c));
        }
    }

    [Theory]
    [InlineData(0, "工业灰", "#2563eb")]
    [InlineData(1, "海洋蓝", "#0284c7")]
    [InlineData(2, "高对比度", "#1d4ed8")]
    [InlineData(3, "暗夜绿", "#22c55e")]
    [InlineData(4, "暖色调", "#ea580c")]
    public void DefaultPresets_CorrectPrimaryColor(int index, string name, string expectedPrimary)
    {
        var preset = StylePreset.DefaultPresets[index];
        Assert.Equal(name, preset.Name);
        Assert.Equal(expectedPrimary, preset.Theme.PrimaryColor);
    }
}
