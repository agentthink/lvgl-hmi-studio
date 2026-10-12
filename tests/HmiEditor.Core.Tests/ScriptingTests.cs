using HmiEditor.Core.Models.Scripting;
using HmiEditor.Core.Services;
using Xunit;

namespace HmiEditor.Core.Tests;

public class ScriptingTests
{
    // ── HmiScript 模型测试 ──

    [Fact]
    public void HmiScript_DefaultCategory_IsOnClick()
    {
        var script = new HmiScript();
        Assert.Equal("OnClick", script.Category);
        Assert.True(script.IsEnabled);
        Assert.Equal(1, script.Version);
    }

    [Fact]
    public void HmiScript_CanSetCode()
    {
        var script = new HmiScript
        {
            Name = "Toggle Valve",
            Category = "OnClick",
            Code = "function on_click()\n  hmi_write(\"valve\", 1)\nend"
        };
        Assert.Equal("Toggle Valve", script.Name);
        Assert.Contains("function on_click", script.Code);
    }

    // ── ScriptBinding 模型测试 ──

    [Fact]
    public void ScriptBinding_DefaultIsEnabled()
    {
        var binding = new ScriptBinding();
        Assert.True(binding.IsEnabled);
    }

    [Fact]
    public void ScriptBinding_CanBindEvent()
    {
        var binding = new ScriptBinding
        {
            ControlId = "btn-001",
            EventName = "OnClick",
            ScriptId = "script-001",
            FunctionName = "on_button_click"
        };
        Assert.Equal("btn-001", binding.ControlId);
        Assert.Equal("on_button_click", binding.FunctionName);
    }

    // ── ScriptTemplate 测试 ──

    [Fact]
    public void ScriptTemplate_OnClick_ContainsCode()
    {
        var tpl = ScriptTemplate.OnClick;
        Assert.Equal("Button OnClick", tpl.Name);
        Assert.Equal("OnClick", tpl.Category);
        Assert.Contains("function on_click", tpl.Code);
    }

    [Fact]
    public void ScriptTemplate_AllTemplates_Exist()
    {
        var templates = ScriptEngineService.GetTemplates();
        Assert.Equal(4, templates.Count);
        Assert.Contains(templates, t => t.Category == "OnClick");
        Assert.Contains(templates, t => t.Category == "OnValueChanged");
        Assert.Contains(templates, t => t.Category == "Timer");
        Assert.Contains(templates, t => t.Category == "Alarm");
    }

    // ── ScriptEngineService 测试 ──

    [Fact]
    public void Engine_LoadScript_AddsToList()
    {
        var engine = new ScriptEngineService();
        var script = new HmiScript { Name = "Test", Code = "function f() end" };
        engine.LoadScript(script);

        Assert.Single(engine.GetLoadedScripts());
        Assert.Equal("Test", engine.GetLoadedScripts()[0].Name);
    }

    [Fact]
    public void Engine_UnloadScript_RemovesFromList()
    {
        var engine = new ScriptEngineService();
        var script = new HmiScript { Id = "s1", Name = "Test" };
        engine.LoadScript(script);
        engine.UnloadScript("s1");
        Assert.Empty(engine.GetLoadedScripts());
    }

    [Fact]
    public void Engine_GetScript_ReturnsCorrect()
    {
        var engine = new ScriptEngineService();
        var script = new HmiScript { Id = "find-me", Name = "FindMe" };
        engine.LoadScript(script);

        var found = engine.GetScript("find-me");
        Assert.NotNull(found);
        Assert.Equal("FindMe", found!.Name);
    }

    [Fact]
    public void Engine_GetScriptsByCategory_Filters()
    {
        var engine = new ScriptEngineService();
        engine.LoadScript(new HmiScript { Name = "S1", Category = "OnClick" });
        engine.LoadScript(new HmiScript { Name = "S2", Category = "OnClick" });
        engine.LoadScript(new HmiScript { Name = "S3", Category = "Timer" });

        Assert.Equal(2, engine.GetScriptsByCategory("OnClick").Count);
        Assert.Single(engine.GetScriptsByCategory("Timer"));
    }

    // ── 校验测试 ──

    [Fact]
    public void Validate_EmptyName_ReturnsError()
    {
        var engine = new ScriptEngineService();
        var result = engine.Validate(new HmiScript { Name = "", Code = "function f() end" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "EMPTY_NAME");
    }

    [Fact]
    public void Validate_EmptyCode_ReturnsError()
    {
        var engine = new ScriptEngineService();
        var result = engine.Validate(new HmiScript { Name = "Test", Code = "" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "EMPTY_CODE");
    }

    [Fact]
    public void Validate_NoFunction_ReturnsError()
    {
        var engine = new ScriptEngineService();
        var result = engine.Validate(new HmiScript
        {
            Name = "Test",
            Code = "local x = 1"
        });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "NO_FUNCTION");
    }

    [Fact]
    public void Validate_ValidScript_ReturnsOk()
    {
        var engine = new ScriptEngineService();
        var result = engine.Validate(new HmiScript
        {
            Name = "Valid",
            Code = "function on_click()\n  print(\"hello\")\nend"
        });
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    // ── 事件绑定测试 ──

    [Fact]
    public void AddBinding_AddsToList()
    {
        var engine = new ScriptEngineService();
        engine.AddBinding(new ScriptBinding
        {
            ControlId = "btn-1",
            EventName = "OnClick",
            FunctionName = "on_click_handler"
        });

        Assert.Single(engine.GetAllBindings());
    }

    [Fact]
    public void RemoveBinding_Removes()
    {
        var engine = new ScriptEngineService();
        var binding = new ScriptBinding { Id = "b1", ControlId = "btn-1" };
        engine.AddBinding(binding);
        engine.RemoveBinding("b1");
        Assert.Empty(engine.GetAllBindings());
    }

    [Fact]
    public void GetBindingsForControl_Filters()
    {
        var engine = new ScriptEngineService();
        engine.AddBinding(new ScriptBinding { ControlId = "btn-1", EventName = "OnClick" });
        engine.AddBinding(new ScriptBinding { ControlId = "btn-1", EventName = "OnPress" });
        engine.AddBinding(new ScriptBinding { ControlId = "btn-2", EventName = "OnClick" });

        Assert.Equal(2, engine.GetBindingsForControl("btn-1").Count);
        Assert.Single(engine.GetBindingsForControl("btn-2"));
    }

    // ── 模板创建测试 ──

    [Fact]
    public void CreateFromTemplate_CreatesNewScript()
    {
        var engine = new ScriptEngineService();
        var script = engine.CreateFromTemplate(ScriptTemplate.OnClick);

        Assert.StartsWith("Button OnClick", script.Name);
        Assert.Equal("OnClick", script.Category);
        Assert.Contains("function on_click", script.Code);
        Assert.Equal(1, script.Version);
    }

    // ── 事件测试 ──

    [Fact]
    public void ScriptLoadedEvent_Fires()
    {
        var fired = false;
        var engine = new ScriptEngineService();
        engine.ScriptLoaded += (_, _) => fired = true;
        engine.LoadScript(new HmiScript { Name = "EventTest" });
        Assert.True(fired);
    }

    [Fact]
    public void BindingChangedEvent_Fires()
    {
        var fired = false;
        var engine = new ScriptEngineService();
        engine.BindingChanged += (_, _) => fired = true;
        engine.AddBinding(new ScriptBinding { ControlId = "btn" });
        Assert.True(fired);
    }

    // ── 初始化/清理测试 ──

    [Fact]
    public void InitializeAndShutdown_Works()
    {
        var engine = new ScriptEngineService();
        engine.Initialize();
        Assert.True(engine.IsInitialized);

        engine.LoadScript(new HmiScript { Id = "s1", Name = "test" });
        engine.AddBinding(new ScriptBinding { Id = "b1" });

        engine.Shutdown();
        Assert.False(engine.IsInitialized);
        Assert.Empty(engine.GetLoadedScripts());
        Assert.Empty(engine.GetAllBindings());
    }

    // ── 脚本卸载同时清理绑定的测试 ──

    [Fact]
    public void UnloadScript_RemovesAssociatedBindings()
    {
        var engine = new ScriptEngineService();
        engine.LoadScript(new HmiScript { Id = "script-1", Name = "S1" });

        engine.AddBinding(new ScriptBinding { ControlId = "btn-1", ScriptId = "script-1" });
        engine.AddBinding(new ScriptBinding { ControlId = "btn-2", ScriptId = "script-1" });

        engine.UnloadScript("script-1");
        Assert.Empty(engine.GetAllBindings());
    }
}
