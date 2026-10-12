using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using System.Linq;
using Xunit;

namespace HmiEditor.Core.Tests;

public class TemplateManagerTests
{
    [Fact]
    public void CreateDefaultTemplates_ReturnsFourTemplates()
    {
        var templates = TemplateManager.CreateDefaultTemplates();
        Assert.Equal(4, templates.Count);
    }

    [Fact]
    public void CreateDefaultTemplates_AllHaveControls()
    {
        var templates = TemplateManager.CreateDefaultTemplates();
        Assert.All(templates, t => Assert.NotEmpty(t.Controls));
    }

    [Fact]
    public void CreateFromSelection_WithControls_CreatesTemplate()
    {
        var mgr = new TemplateManager();
        var controls = new[]
        {
            new HmiControl { Type = HmiControlType.Button, Name = "Start", X = 0, Y = 0, Width = 80, Height = 44 },
            new HmiControl { Type = HmiControlType.Button, Name = "Stop", X = 100, Y = 0, Width = 80, Height = 44 },
        };

        var template = mgr.CreateFromSelection("Test", "TestCat", controls);

        Assert.Equal("Test", template.Name);
        Assert.Equal("TestCat", template.Category);
        Assert.Equal(2, template.Controls.Count);
        Assert.Single(mgr.Templates);
    }

    [Fact]
    public void CreateFromSelection_CoordinatesAreRelative()
    {
        var mgr = new TemplateManager();
        var controls = new[]
        {
            new HmiControl { X = 100, Y = 200, Width = 50, Height = 30 },
            new HmiControl { X = 300, Y = 400, Width = 80, Height = 60 },
        };

        var template = mgr.CreateFromSelection("Test", "", controls);

        // First control should be at (0,0) since it's the top-left
        Assert.Equal(0, template.Controls[0].X);
        Assert.Equal(0, template.Controls[0].Y);
        // Second control should be offset from first
        Assert.Equal(200, template.Controls[1].X);
        Assert.Equal(200, template.Controls[1].Y);
    }

    [Fact]
    public void ApplyTemplate_PlacesAtTargetPosition()
    {
        var mgr = new TemplateManager();
        var controls = new[]
        {
            new HmiControl { X = 0, Y = 0, Width = 80, Height = 44 },
            new HmiControl { X = 100, Y = 50, Width = 60, Height = 30 },
        };

        var template = mgr.CreateFromSelection("Test", "", controls);
        var result = mgr.ApplyTemplate(template, 200, 100);

        Assert.Equal(2, result.Count);
        Assert.Equal(200, result[0].X);
        Assert.Equal(100, result[0].Y);
        Assert.Equal(300, result[1].X);
        Assert.Equal(150, result[1].Y);
    }

    [Fact]
    public void RemoveTemplate_RemovesFromList()
    {
        var mgr = new TemplateManager();
        var controls = new[] { new HmiControl { Name = "Test" } };

        var template = mgr.CreateFromSelection("Test", "", controls);
        Assert.Single(mgr.Templates);

        mgr.RemoveTemplate(template);
        Assert.Empty(mgr.Templates);
    }

    [Fact]
    public void CreateFromSelection_EmptyControls_Throws()
    {
        var mgr = new TemplateManager();
        Assert.Throws<ArgumentException>(() =>
            mgr.CreateFromSelection("Test", "", Enumerable.Empty<HmiControl>()));
    }

    [Fact]
    public void SerializeDeserialize_RoundTrip_PreservesData()
    {
        var mgr = new TemplateManager();
        mgr.AddPredefined(TemplateManager.CreateDefaultTemplates());

        var json = mgr.Serialize();
        Assert.False(string.IsNullOrEmpty(json));

        var mgr2 = new TemplateManager();
        mgr2.Deserialize(json);

        Assert.Equal(4, mgr2.Templates.Count);
        Assert.Equal("电机面板", mgr2.Templates[0].Name);
    }

    [Fact]
    public void AddPredefined_MultipleTemplates()
    {
        var mgr = new TemplateManager();
        var templates = new[]
        {
            new ControlTemplate { Name = "T1" },
            new ControlTemplate { Name = "T2" },
        };

        mgr.AddPredefined(templates);
        Assert.Equal(2, mgr.Templates.Count);
    }

    [Fact]
    public void CreateFaceplateInstance_DefaultNames_AreUniqueInTheSameMillisecond()
    {
        var mgr = new TemplateManager();
        var template = new ControlTemplate { Name = "泵模板" };

        // 连着建两个（旧实现用 DateTime.Now.Millisecond:D3 做后缀 ⇒ 同一毫秒内**重名**，
        // 且后缀每秒重复；阶段 121 改成进程内单调序号）
        var a = mgr.CreateFaceplateInstance(template, 0, 0);
        var b = mgr.CreateFaceplateInstance(template, 10, 10);

        Assert.NotEqual(a.Name, b.Name);
        Assert.StartsWith("泵模板 #", a.Name);
        Assert.StartsWith("泵模板 #", b.Name);

        // 显式给名时不覆盖
        var c = mgr.CreateFaceplateInstance(template, 0, 0, "自定义名");
        Assert.Equal("自定义名", c.Name);
    }
}
