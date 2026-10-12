using HmiEditor.AI.Services;
using Xunit;

namespace HmiEditor.AI.Tests;

/// <summary>
/// LuaCompletionService 测试 —— 脚本补全服务。
/// </summary>
public class LuaCompletionServiceTests
{
    [Fact]
    public void GetCompletions_HmiPrefix_ReturnsApis()
    {
        var completions = LuaCompletionService.GetCompletions("hmi_");
        Assert.NotEmpty(completions);
        Assert.Contains(completions, c => c.Label == "hmi_read");
        Assert.Contains(completions, c => c.Label == "hmi_write");
    }

    [Fact]
    public void GetCompletions_HmiReadPrefix_OnlyMatchesRead()
    {
        var completions = LuaCompletionService.GetCompletions("hmi_read");
        Assert.Contains(completions, c => c.Label == "hmi_read");
        Assert.DoesNotContain(completions, c => c.Label == "hmi_write");
    }

    [Fact]
    public void GetCompletions_OnPrefix_ReturnsTemplates()
    {
        var completions = LuaCompletionService.GetCompletions("on_");
        Assert.Contains(completions, c => c.Label.Contains("on_click"));
    }

    [Fact]
    public void GetCompletions_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(LuaCompletionService.GetCompletions(""));
        Assert.Empty(LuaCompletionService.GetCompletions("   "));
        Assert.Empty(LuaCompletionService.GetCompletions(null));
    }

    [Fact]
    public void GetCompletions_GeneralInput_ReturnsAll()
    {
        var completions = LuaCompletionService.GetCompletions("function");
        Assert.NotEmpty(completions);
    }

    [Fact]
    public void GetAllTemplates_ContainsFourCategories()
    {
        var templates = LuaCompletionService.GetAllTemplates();
        Assert.Equal(4, templates.Count);
        Assert.Contains(templates, t => t.Label.Contains("on_click"));
        Assert.Contains(templates, t => t.Label.Contains("on_timer"));
        Assert.Contains(templates, t => t.Label.Contains("alarm"));
    }

    [Fact]
    public void GetAllApis_ContainsSixEntries()
    {
        var apis = LuaCompletionService.GetAllApis();
        Assert.Equal(6, apis.Count);
    }
}
