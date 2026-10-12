using HmiEditor.AI.Generators;
using HmiEditor.AI.Models;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// NlToScreenGenerator 测试 —— 响应解析 + 模型映射。
/// </summary>
public class NlToScreenGeneratorTests
{
    [Fact]
    public void ParseResponse_PlainJson_ReturnsModel()
    {
        var json = """
            {
              "screen": {
                "name": "温度监控",
                "width": 800,
                "height": 480,
                "bgColor": "#1a1a2e",
                "controls": [
                  { "type": "Label", "name": "温度显示", "x": 20, "y": 60, "width": 120, "height": 40, "text": "25.5°C", "variableBindingId": "temperature_1" }
                ]
              }
            }
            """;

        var parsed = NlToScreenGenerator.ParseResponse(json);
        Assert.NotNull(parsed);
        Assert.Equal("温度监控", parsed!.Screen.Name);
        Assert.Single(parsed.Screen.Controls);
        Assert.Equal("Label", parsed.Screen.Controls[0].Type);
        Assert.Equal("temperature_1", parsed.Screen.Controls[0].VariableName);
    }

    [Fact]
    public void ParseResponse_FencedJson_StripsCodeBlockMarkers()
    {
        var json = "```json\n{ \"screen\": { \"name\": \"A\", \"controls\": [] } }\n```";

        var parsed = NlToScreenGenerator.ParseResponse(json);
        Assert.NotNull(parsed);
        Assert.Equal("A", parsed!.Screen.Name);
    }

    [Fact]
    public void ParseResponse_InvalidJson_ReturnsNull()
    {
        Assert.Null(NlToScreenGenerator.ParseResponse("{ not json"));
        Assert.Null(NlToScreenGenerator.ParseResponse(""));
        Assert.Null(NlToScreenGenerator.ParseResponse("plain text"));
    }

    [Fact]
    public void MapToHmiScreen_ResolvesVariableNameToId()
    {
        var project = new HmiProject();
        var variable = new Variable { Name = "temperature_1", DataType = "Float" };
        project.VariableManager.Variables.Add(variable);

        var generated = new AiGeneratedScreen
        {
            Screen = new AiScreen
            {
                Name = "监控",
                Controls = new List<AiControl>
                {
                    new() { Type = "Gauge", Name = "温度表", X = 10, Y = 10, Width = 200, Height = 200, VariableName = "temperature_1" }
                }
            }
        };

        var screen = NlToScreenGenerator.MapToHmiScreen(generated, project);
        Assert.Equal(variable.Id, screen.Controls[0].VariableBindingId);
        Assert.Equal(HmiControlType.Gauge, screen.Controls[0].Type);
    }

    [Fact]
    public void MapToHmiScreen_UnknownVariableLeavesBindingEmpty()
    {
        var project = new HmiProject();
        var generated = new AiGeneratedScreen
        {
            Screen = new AiScreen
            {
                Name = "监控",
                Controls = new List<AiControl>
                {
                    new() { Type = "Label", Name = "L", X = 0, Y = 0, Width = 10, Height = 10, VariableName = "not_exists" }
                }
            }
        };

        var screen = NlToScreenGenerator.MapToHmiScreen(generated, project);
        Assert.Null(screen.Controls[0].VariableBindingId);
    }

    [Fact]
    public void MapToHmiScreen_UnknownTypeFallsBackToLabel()
    {
        var generated = new AiGeneratedScreen
        {
            Screen = new AiScreen
            {
                Name = "X",
                Controls = new List<AiControl> { new() { Type = "Hologram", Name = "H", X = 0, Y = 0, Width = 10, Height = 10 } }
            }
        };

        var screen = NlToScreenGenerator.MapToHmiScreen(generated, new HmiProject());
        Assert.Equal(HmiControlType.Label, screen.Controls[0].Type);
    }

    [Fact]
    public void MapToHmiScreen_NullGenerated_ReturnsDefaultScreen()
    {
        var screen = NlToScreenGenerator.MapToHmiScreen(null, new HmiProject());
        Assert.Equal("AI 生成屏幕", screen.Name);
        Assert.Empty(screen.Controls);
    }

    // ── 尺寸来源：LLM 没给 ⇒ 工程的屏幕默认模板 ──

    [Fact]
    public void ParseResponse_OmittedSize_StaysNullRatherThanClassDefault()
    {
        // 核心回归钉子：LLM 没给 width/height 时必须反序列化成 null。
        // 曾经 AiScreen.Width/Height 是带默认值 800/480 的 int，
        // 导致「没给」与「给了 800」不可区分，工程默认（1024×600）永远被压掉。
        var parsed = NlToScreenGenerator.ParseResponse("""{"screen":{"name":"X"}}""");

        Assert.NotNull(parsed);
        Assert.Null(parsed!.Screen.Width);
        Assert.Null(parsed.Screen.Height);
    }

    [Fact]
    public void MapToHmiScreen_NoSizeFromLlm_UsesProjectScreenDefaults()
    {
        var project = new HmiProject();
        project.ScreenDefaultsConfig.DefaultWidth = 800;
        project.ScreenDefaultsConfig.DefaultHeight = 480;

        var screen = NlToScreenGenerator.MapToHmiScreen(
            new AiGeneratedScreen { Screen = new AiScreen { Name = "监控" } }, project);

        Assert.Equal(800, screen.Width);
        Assert.Equal(480, screen.Height);
    }

    [Fact]
    public void MapToHmiScreen_ExplicitSizeFromLlm_WinsOverProjectDefaults()
    {
        var project = new HmiProject(); // 默认 1024×600

        var screen = NlToScreenGenerator.MapToHmiScreen(
            new AiGeneratedScreen { Screen = new AiScreen { Name = "监控", Width = 320, Height = 240 } },
            project);

        Assert.Equal(320, screen.Width);
        Assert.Equal(240, screen.Height);
    }

    [Fact]
    public void MapToHmiScreen_TakesAdaptiveModeFromProjectDefaults()
    {
        var project = new HmiProject();
        project.ScreenDefaultsConfig.DefaultAdaptiveMode = ScreenAdaptiveMode.Fit;

        var screen = NlToScreenGenerator.MapToHmiScreen(null, project);

        Assert.Equal(ScreenAdaptiveMode.Fit, screen.AdaptiveMode);
    }
}
