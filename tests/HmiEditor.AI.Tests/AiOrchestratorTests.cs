using HmiEditor.AI;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// AiOrchestrator 意图分类测试。
/// </summary>
public class AiOrchestratorTests
{
    [Theory]
    [InlineData("创建一个温度监控界面")]
    [InlineData("生成一个电机控制屏幕")]
    [InlineData("添加一个新页面")]
    [InlineData("设计一个报警画面")]
    public void ClassifyIntent_ScreenKeywords_ReturnsScreen(string input)
    {
        Assert.Equal(AiOrchestrator.AiIntent.Screen, AiOrchestrator.ClassifyIntent(input));
    }

    [Theory]
    [InlineData("创建一个电机面盘模板")]
    [InlineData("生成可复用的阀门组件")]
    [InlineData("封装一个水泵控制模板")]
    public void ClassifyIntent_TemplateKeywords_ReturnsTemplate(string input)
    {
        Assert.Equal(AiOrchestrator.AiIntent.Template, AiOrchestrator.ClassifyIntent(input));
    }

    [Theory]
    [InlineData("写个脚本让温度超过80时报警")]
    [InlineData("生成lua联动逻辑")]
    [InlineData("按钮点击时切换变量")]
    public void ClassifyIntent_ScriptKeywords_ReturnsScript(string input)
    {
        Assert.Equal(AiOrchestrator.AiIntent.Script, AiOrchestrator.ClassifyIntent(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void ClassifyIntent_EmptyOrNull_ReturnsUnknown(string? input)
    {
        Assert.Equal(AiOrchestrator.AiIntent.Unknown, AiOrchestrator.ClassifyIntent(input!));
    }

    [Theory]
    [InlineData("生成一个温度计图片")]
    [InlineData("画一个svg图标")]
    [InlineData("生成一张工业设备矢量图")]
    [InlineData("create an svg image of a pump")]
    [InlineData("帮我生成一个 200x200 的图片")]
    public void ClassifyIntent_ImageKeywords_ReturnsImage(string input)
    {
        Assert.Equal(AiOrchestrator.AiIntent.Image, AiOrchestrator.ClassifyIntent(input));
    }

    [Theory]
    [InlineData("生成svg屏幕", AiOrchestrator.AiIntent.Image)]       // 图片关键词优先于屏幕
    [InlineData("生成电机面盘模板", AiOrchestrator.AiIntent.Template)] // 无图片关键词 → 模板
    [InlineData("生成lua脚本", AiOrchestrator.AiIntent.Script)]       // 无图片关键词 → 脚本
    public void ClassifyIntent_PriorityOrder(string input, AiOrchestrator.AiIntent expected)
    {
        Assert.Equal(expected, AiOrchestrator.ClassifyIntent(input));
    }
}
