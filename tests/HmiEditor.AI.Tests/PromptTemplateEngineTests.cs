using HmiEditor.AI.Inference;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// Prompt 模板引擎测试 —— 用户输入清洗 + 上下文注入。
/// </summary>
public class PromptTemplateEngineTests
{
    [Fact]
    public void SanitizeUserInput_StripsControlCharacters()
    {
        var input = "创建温度界面\u0000\u0001\u001f结束";
        var result = PromptTemplateEngine.SanitizeUserInput(input);
        Assert.Equal("创建温度界面结束", result);
    }

    [Fact]
    public void SanitizeUserInput_StripsZeroWidthCharacters()
    {
        var input = "温度\u200b监控\u200c界面\u200d";
        var result = PromptTemplateEngine.SanitizeUserInput(input);
        Assert.Equal("温度监控界面", result);
    }

    [Fact]
    public void SanitizeUserInput_KeepsNewlinesAndTabs()
    {
        var input = "第一行\n第二行\t带缩进";
        var result = PromptTemplateEngine.SanitizeUserInput(input);
        Assert.Equal("第一行\n第二行\t带缩进", result);
    }

    [Fact]
    public void SanitizeUserInput_TruncatesOverlongInput()
    {
        var input = new string('a', 5000);
        var result = PromptTemplateEngine.SanitizeUserInput(input);
        Assert.Equal(2000, result.Length);
    }

    [Fact]
    public void SanitizeUserInput_EmptyInputReturnsEmpty()
    {
        Assert.Equal("", PromptTemplateEngine.SanitizeUserInput(""));
        Assert.Equal("", PromptTemplateEngine.SanitizeUserInput(null!));
    }
}
